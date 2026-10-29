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
        /// grain rise (seed 2) and with a bottom tear (seed 3, abrupt).
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
            Assert.That(FeedSchedule.Digest(seed, Fields, motorCurrent, cutField, abrupt), Is.EqualTo(godot));
        }

        /// Terminal losses are events, never a timer (D-008, D-026): left to its own timers the feed only ever
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
            });
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
            flying.Osd[0, 0] = 'S';
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.MotorCurrent), Is.EqualTo("41.5 A, max 52.0 A; 10.4 0.0 0.0 0.0 A"));
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.LinkMargin), Is.EqualTo("9.5 dB"));
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.CameraAngularVelocity), Is.EqualTo("(0.50, -1.25, 0.00) rad/s"));
            Assert.That(FeedSignalLog.Value(flying, FeedSignal.OsdState), Is.EqualTo("1 of 480 cells"));
        }
    }
}
