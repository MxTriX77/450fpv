using System;
using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The feed's scheduler and loss machine against the Godot feed (p4-feed, analog-feed spec, "The same seed, the
    /// same schedule"; FEED-00, D-026). Unity's EditMode runs these, and so does <c>dotnet test tools/feedtests</c>,
    /// so both runtimes are held to the Godot schedule.
    /// </summary>
    public sealed class FeedScheduleTests
    {
        const int Fields = 15000; // 5 minutes at 50 fields/s

        /// The digests are of the Godot feed's own code: <c>docs/legacy/video-feed-godot/src/FeedEvents.cs</c> and
        /// <c>FeedLoss.cs</c> compiled verbatim against Godot 4.7.2's GodotSharp and run headless over the same
        /// fields, digested exactly as <see cref="FeedSchedule.Digest"/> does (p4-feed task 1.1's A/B). Between
        /// them the cases reach every loss state and every tell: the timer's dropouts (blue and black, the
        /// brightening and the top tear), the staged terminal loss (seeds 1 and 3, faded), and the hard cut with a
        /// grain rise (seed 2) and with a bottom tear (seed 3, abrupt). Run with Godot's link artefacts, its
        /// free-running dropouts, which D-065 took out of the feed: the port's own A/B stays on record.
        [TestCase(1, 0f, 0, false, 0xB476A0243BF62FAAUL)]
        [TestCase(2, 0f, 0, false, 0xE3FF6DA934271FC8UL)]
        [TestCase(3, 0f, 0, false, 0xC3D7A4784E241541UL)]
        [TestCase(1, 0.6f, 0, false, 0xDEDAEEED7F353462UL)]
        [TestCase(2, 0.6f, 0, false, 0xF2ED6E62C6A8A09EUL)]
        [TestCase(3, 0.6f, 0, false, 0x2EACABDA0123AC9EUL)]
        [TestCase(1, 0f, 7500, false, 0xCFF9A2F99F68239AUL)]
        [TestCase(2, 0f, 7500, true, 0x0FE80CD5C7846F39UL)]
        [TestCase(3, 0f, 7500, false, 0x15A0CE4F9BEC30DBUL)]
        [TestCase(3, 0f, 7500, true, 0x13E9F3C10E108C1AUL)]
        public void TheSameSeedGivesTheGodotSchedule(int seed, float motorCurrent, int cutField, bool abrupt,
            ulong godot)
        {
            Assert.That(FeedSchedule.Digest(seed, Fields, motorCurrent, cutField, abrupt, godot: true),
                Is.EqualTo(godot));
        }

        /// The feed's own schedule since D-065 (the noise is the analog camera's, and only the fiber loses the
        /// picture), pinned from <c>feedbench schedule</c> without <c>--godot</c>, so any later drift shows: N3, N4
        /// and the quality drops with no free-running dropout, and the cut reaching the staged loss (seeds 1 and 3,
        /// faded) and the hard cut with a grain rise (seeds 2 and 3, abrupt) and with a bottom tear (seed 5,
        /// abrupt).
        [TestCase(1, 0f, 0, false, 0x9E9F49F99D786D2DUL)]
        [TestCase(2, 0f, 0, false, 0x7DD6B34951EB7430UL)]
        [TestCase(3, 0f, 0, false, 0x74F90FE2518D07DCUL)]
        [TestCase(1, 0.6f, 0, false, 0x39B2EAFA94D12ABDUL)]
        [TestCase(2, 0.6f, 0, false, 0xAD6ED49D469B0DEDUL)]
        [TestCase(3, 0.6f, 0, false, 0x8773A2B8F8CD6BEFUL)]
        [TestCase(1, 0f, 7500, false, 0x5712D2D85C03AB31UL)]
        [TestCase(2, 0f, 7500, true, 0xCE1EF22CC44D958CUL)]
        [TestCase(3, 0f, 7500, false, 0x44F7B7002431242FUL)]
        [TestCase(3, 0f, 7500, true, 0x1EB0D9926AFF5B46UL)]
        [TestCase(5, 0f, 7500, true, 0x181F2C2106E553F5UL)]
        public void TheSameSeedGivesTheSameSchedule(int seed, float motorCurrent, int cutField, bool abrupt,
            ulong pinned)
        {
            Assert.That(FeedSchedule.Digest(seed, Fields, motorCurrent, cutField, abrupt), Is.EqualTo(pinned));
        }

        /// D-065: left to itself the feed never loses the picture. No dropout, no tell and no tear in five minutes;
        /// only N3, N4 and the quality drops run.
        [TestCase(1, 0f)]
        [TestCase(2, 0f)]
        [TestCase(3, 0f)]
        [TestCase(1, 0.6f)]
        public void TheFeedNeverLosesThePictureOnItsOwn(int seed, float motorCurrent)
        {
            int lost = 0, drops = 0, n3 = 0, n4 = 0;
            FeedSchedule.Run(seed, Fields, motorCurrent, 0, false, e =>
            {
                FeedLoss l = e.Loss;
                if (l.Busy || l.Current != FeedLoss.State.Picture || l.Tell > 0f || l.TearAmp > 0f || l.GrainBoost > 0f)
                    lost++;
                drops += e.Degrade > 0f ? 1 : 0;
                n3 += e.N3Amp > 0f ? 1 : 0;
                n4 += e.N4Amp > 0f ? 1 : 0;
            });
            Assert.That(lost, Is.Zero);
            Assert.That(new[] { drops, n3, n4 }, Has.All.GreaterThan(Fields / 10), "the camera's noise still runs");
        }

        /// With Godot's timer off, the schedule is Godot's field for field until the field where Godot's timer
        /// fired its first dropout: nothing else in the scheduler moved.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void UntilGodotsFirstDropoutTheScheduleIsGodots(int seed)
        {
            var godot = new System.Collections.Generic.List<string>();
            FeedSchedule.Run(seed, Fields, 0.6f, 0, false, e => godot.Add(FeedSchedule.Row(e)), godot: true);
            int first = godot.FindIndex(row => row.Split(' ')[2] == "1");
            Assert.That(first, Is.GreaterThan(0), "Godot's timer fires a dropout");
            int field = 0;
            FeedSchedule.Run(seed, first, 0.6f, 0, false, e => Assert.That(FeedSchedule.Row(e), Is.EqualTo(godot[field++])));
            Assert.That(field, Is.EqualTo(first));
        }

        /// The fiber's two entry points stay (D-065, p7d-tether drives them): a margin dip's recovering dropout,
        /// with its tell, an outage in Godot's range and the picture back clean (FEED-06), and the break's terminal
        /// loss, which never clears.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TheFibersEntryPointsStillLoseThePicture(int seed)
        {
            var events = new FeedEvents(seed);
            events.AdvanceTo(600, 0f);
            events.Loss.StartDropout();
            int tell = 0, black = 0, blue = 0;
            while (events.Field < 700)
            {
                events.AdvanceTo(events.Field + 1, 0f);
                FeedLoss l = events.Loss;
                if (!l.Busy && l.Current == FeedLoss.State.Picture)
                    break;
                if (l.Current == FeedLoss.State.Picture)
                    tell++;
                black += l.Current == FeedLoss.State.Black ? 1 : 0;
                blue += l.Current == FeedLoss.State.Blue ? 1 : 0;
            }
            FeedUniforms u = FeedUniforms.From(events, seed, 1f, 1f, 0f);
            Assert.That(tell, Is.InRange(2, 5), "always a tell before the picture goes");
            Assert.That(black > 0 ? black : blue, black > 0 ? Is.InRange(2, 4) : Is.InRange(17, 35));
            Assert.That(new[] { u.LossState, u.Tell, u.TearAmp, u.GrainBoost, u.BlackA, u.BlackB },
                Is.EqualTo(new[] { 0f, 0f, 0f, 0f, 0f, 1f }), "the picture comes back clean");

            events.TriggerCut(seed % 2 == 0);
            events.AdvanceTo(events.Field + 60, 0f);
            Assert.That(events.Loss.Current, Is.EqualTo(FeedLoss.State.Blue));
            events.AdvanceTo(events.Field + 3000, 0f);
            Assert.That(events.Loss.Current, Is.EqualTo(FeedLoss.State.Blue), "a break never clears");
        }

        /// Terminal losses are events, never a timer (D-008, D-026): left to its own timers the Godot feed only ever
        /// shows recovering dropouts, whose blue lasts at most 35 fields (FeedLoss.cs:48), and never snow.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheTimerNeverFiresATerminalLoss(int seed)
        {
            int blueRun = 0, longestBlue = 0, snow = 0;
            FeedSchedule.Run(seed, Fields, 0f, 0, false, e =>
            {
                FeedLoss.State s = e.Loss.Current;
                blueRun = s == FeedLoss.State.Blue ? blueRun + 1 : 0;
                longestBlue = Math.Max(longestBlue, blueRun);
                if (s == FeedLoss.State.Snow || s == FeedLoss.State.PartialSnow)
                    snow++;
            }, godot: true);
            Assert.That(snow, Is.Zero);
            Assert.That(longestBlue, Is.InRange(18, 35));
        }

        /// Every K§11.8 signal is a labelled stub until the physics publishes it, and the log says so (FEED-03).
        [Test]
        public void EveryStubIsLabelled()
        {
            var stub = new StubFeedSignals();
            string log = FeedSignalLog.Describe(stub);
            foreach (FeedSignal s in (FeedSignal[])Enum.GetValues(typeof(FeedSignal)))
            {
                Assert.That(stub.SourceOf(s), Is.EqualTo(SignalSource.Stub), s.ToString());
                StringAssert.Contains(s.ToString(), log);
            }
            StringAssert.EndsWith("physics []", log);
        }

        /// The review overlay prints every signal's value in its K§11.8 unit beside its source (analog-feed spec,
        /// "A stub is visible as a stub"): the stubs' own values, an unmodelled fiber's infinite margin as "inf".
        [Test]
        public void TheOverlayShowsEverySignalsValue()
        {
            var parked = new StubFeedSignals();
            foreach (FeedSignal s in (FeedSignal[])Enum.GetValues(typeof(FeedSignal)))
                Assert.That(FeedSignalLog.Value(parked, s), Is.Not.Empty, s.ToString());
            Assert.That(FeedSignalLog.Value(parked, FeedSignal.PackVolts), Is.EqualTo("25.20 V"));
            Assert.That(FeedSignalLog.Value(parked, FeedSignal.LinkMargin), Is.EqualTo("inf dB"));
            Assert.That(FeedSignalLog.Value(parked, FeedSignal.LinkState), Is.EqualTo("Intact"));
            Assert.That(FeedSignalLog.Value(parked, FeedSignal.OsdState), Is.EqualTo("blank"));

            var flying = new StubFeedSignals { MotorCurrentTotalA = 41.5f, MotorCurrentTotalMaxA = 52f, LinkMarginDb = 9.5f,
                CameraAngularVelocity = new System.Numerics.Vector3(0.5f, -1.25f, 0f) };
            flying.MotorAmps[0] = 10.4f;
            flying.Osd.Grid[0, 0] = 'S';
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.MotorCurrent), Is.EqualTo("41.5 A, max 52.0 A; 10.4 0.0 0.0 0.0 A"));
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.LinkMargin), Is.EqualTo("9.5 dB"));
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.CameraAngularVelocity), Is.EqualTo("(0.50, -1.25, 0.00) rad/s"));
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.OsdState), Is.EqualTo("1 of 480 cells"));
        }
    }
}
