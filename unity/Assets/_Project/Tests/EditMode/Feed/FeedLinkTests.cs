using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The receiver's reading of the fiber link (p7d-tether 1.16; the analog-feed spec's "A dip and a break on the feed"):
    /// the physics' dips and break, as the physics publishes them field by field, through the port's loss machine.
    /// Unity's EditMode runs these, and so does <c>dotnet test tools/feedtests</c>.
    /// </summary>
    public sealed class FeedLinkTests
    {
        /// One flight's link, field by field, as the physics publishes it: what the receiver reads before each field.
        sealed class Flight
        {
            public readonly StubFeedSignals Signals = new StubFeedSignals();
            public readonly FeedEvents Events;
            public readonly FeedLink Link = new FeedLink();
            public readonly List<string> Log = new List<string>();

            public Flight(int seed, bool timer) => Events = new FeedEvents(seed) { DropoutTimer = timer };

            /// The dip begins in `field` (counted, on, on the twig).
            public void DipBegins()
            {
                Signals.LinkDips++;
                Signals.LinkInDip = true;
                Signals.LinkLossCause = "Twig";
            }

            public void DipEnds() => Signals.LinkInDip = false;

            public void Breaks(string mechanism)
            {
                Signals.LinkInDip = false;
                Signals.LinkState = LinkState.Broken;
                Signals.TetherBreakMechanism = mechanism;
            }

            /// Reads the link and steps one field, as the rig does.
            public FeedLoss Step()
            {
                string said = Link.Apply(Signals, Events.Loss, Events.Field + 1);
                if (said != null)
                    Log.Add(said);
                Events.AdvanceTo(Events.Field + 1, 0f);
                return Events.Loss;
            }
        }

        static bool Clean(FeedLoss l) => !l.Busy && l.Current == FeedLoss.State.Picture && l.Tell == 0f && l.TearAmp == 0f
                                         && l.GrainBoost == 0f && l.BlackA == 0f && l.BlackB == 1f;

        static bool Out(FeedLoss l) => l.Current == FeedLoss.State.Blue || l.Current == FeedLoss.State.Black;

        /// The analog-feed spec's "A dip and a break on the feed": a dip of 0.5 s (25 fields) and then a break. The
        /// dropout starts in the dip's first field with the port's tell, the picture is out until the dip's last field
        /// and back clean in the next; the break's loss starts in its own field with the stages and lengths of the port's
        /// machine, and the screen is blue to the flight's end. The log names each loss's cause. With Godot's timer off,
        /// so nothing but the fiber loses the picture; 40 seeds reach black and blue outages and both terminal branches.
        [Test]
        public void ADipAndABreakOnTheFeed()
        {
            const int Dip = 600, DipFields = 25, Break = 1200;
            int staged = 0, hard = 0, black = 0, blue = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var flight = new Flight(seed, timer: false);
                while (flight.Events.Field < Dip - 1)
                    Assert.That(Clean(flight.Step()), Is.True, "nothing before the dip");

                flight.DipBegins();
                FeedLoss l = flight.Step();
                Assert.That(l.Busy, Is.True, "the dropout starts in the dip's field");
                Assert.That(l.Current == FeedLoss.State.Picture && (l.Tell > 0f || l.TearAmp > 0f), Is.True, "with the port's tell");
                int tell = 1, outage = 0;
                bool wasBlack = false;
                for (int f = Dip + 1; f < Dip + DipFields; f++)
                {
                    l = flight.Step();
                    Assert.That(l.Busy, Is.True, $"seed {seed}: lost through the dip (field {f})");
                    if (outage == 0 && l.Current == FeedLoss.State.Picture)
                        tell++;
                    else
                    {
                        Assert.That(Out(l), Is.True, $"seed {seed}: the outage holds until the dip ends (field {f})");
                        wasBlack |= l.Current == FeedLoss.State.Black;
                        outage++;
                    }
                }
                Assert.That(tell, Is.InRange(2, 5), "the port's tell: 2–5 fields");
                flight.DipEnds();
                l = flight.Step(); // the dip ended within this field: its last
                Assert.That(Out(l), Is.True, "the dip's last field is still out");
                Assert.That(Clean(flight.Step()), Is.True, "the picture back clean in the field after the dip's end");
                black += wasBlack ? 1 : 0;
                blue += wasBlack ? 0 : 1;

                while (flight.Events.Field < Break - 1)
                    Assert.That(Clean(flight.Step()), Is.True, "nothing between the dip and the break");
                flight.Breaks("Prop");
                l = flight.Step();
                Assert.That(l.Busy, Is.True, "the break's loss starts in its field");
                var states = new List<FeedLoss.State> { l.Current };
                for (int f = 0; f < 3000; f++)
                    states.Add(flight.Step().Current);
                Assert.That(flight.Events.Loss.Current, Is.EqualTo(FeedLoss.State.Blue), "blue to the flight's end");
                if (flight.Events.Loss.Staged)
                {
                    staged++;
                    AssertStaged(states);
                }
                else
                {
                    hard++;
                    int glitch = states.IndexOf(FeedLoss.State.Blue);
                    Assert.That(glitch, Is.InRange(2, 5), "the hard cut's glitch: 2–5 fields");
                    Assert.That(states.GetRange(0, glitch), Has.All.EqualTo(FeedLoss.State.Picture));
                }
                Assert.That(states.GetRange(states.IndexOf(FeedLoss.State.Blue) + 30, 100), Has.All.EqualTo(FeedLoss.State.Blue));

                Assert.That(flight.Log, Has.Count.EqualTo(3), string.Join(" | ", flight.Log));
                StringAssert.Contains($"field {Dip}: link dip 1 on Twig: a recovering dropout", flight.Log[0]);
                StringAssert.Contains($"field {Dip + DipFields}: link dip 1 over", flight.Log[1]);
                StringAssert.Contains($"field {Break}: the fiber broke by Prop, the margin gone within the field", flight.Log[2]);
            }
            TestContext.WriteLine($"40 seeds: outages {black} black, {blue} blue; breaks {staged} staged, {hard} hard cuts");
            Assert.That(new[] { black, blue, staged, hard }, Has.All.GreaterThan(0), "both outages and both terminal branches reached");
        }

        /// The staged loss's stages and lengths (FeedLoss.cs:53–60): partial snow 1–5, blue 5–8, snow 10–15, then blue.
        static void AssertStaged(List<FeedLoss.State> states)
        {
            var runs = new List<(FeedLoss.State S, int N)>();
            foreach (FeedLoss.State s in states)
                if (runs.Count > 0 && runs[runs.Count - 1].S == s)
                    runs[runs.Count - 1] = (s, runs[runs.Count - 1].N + 1);
                else
                    runs.Add((s, 1));
            Assert.That(runs.ConvertAll(r => r.S), Is.EqualTo(new[]
                { FeedLoss.State.PartialSnow, FeedLoss.State.Blue, FeedLoss.State.Snow, FeedLoss.State.Blue }));
            Assert.That(runs[0].N, Is.InRange(1, 5));
            Assert.That(runs[1].N, Is.InRange(5, 8));
            Assert.That(runs[2].N, Is.InRange(10, 15));
        }

        /// With no fiber loss the receiver changes nothing: Godot's schedule, the timer's dropouts included, digest for
        /// digest (the pinned A/B of FeedScheduleTests), at a steady flying margin.
        [TestCase(1, 0f, 0xB476A0243BF62FAAUL)]
        [TestCase(2, 0f, 0xE3FF6DA934271FC8UL)]
        [TestCase(3, 0.6f, 0x2EACABDA0123AC9EUL)]
        public void WithoutAFiberLossTheScheduleIsGodots(int seed, float current, ulong godot)
        {
            var rows = new List<string>();
            var expected = new List<string>();
            FeedSchedule.Run(seed, 15000, current, 0, false, e => expected.Add(FeedSchedule.Row(e)));
            var events = new FeedEvents(seed);
            var link = new FeedLink();
            var flying = new StubFeedSignals { LinkMarginDb = 9.5f };
            for (int f = 1; f <= 15000; f++)
            {
                Assert.That(link.Apply(flying, events.Loss, f), Is.Null);
                events.AdvanceTo(f, current);
                rows.Add(FeedSchedule.Row(events));
            }
            Assert.That(rows, Is.EqualTo(expected));
            Assert.That(FeedSchedule.Digest(seed, 15000, current, 0, false), Is.EqualTo(godot));
        }

        /// An abrupt break through the receiver is the Godot feed's `+hardcut` in the same field, digest for digest
        /// (FeedScheduleTests' pinned Godot runs; seed 2 draws the hard cut with a grain rise, seed 3 with a bottom tear).
        [TestCase(2, 0x0FE80CD5C7846F39UL)]
        [TestCase(3, 0x13E9F3C10E108C1AUL)]
        public void AnAbruptBreakIsGodotsHardcut(int seed, ulong godot)
        {
            Assert.That(Digest(seed, 7500), Is.EqualTo(godot));
        }

        static ulong Digest(int seed, int breakField)
        {
            var flight = new Flight(seed, timer: true);
            var rows = new List<string>();
            for (int f = 1; f <= 15000; f++)
            {
                if (f == breakField)
                    flight.Breaks("Tension");
                flight.Step();
                rows.Add(FeedSchedule.Row(flight.Events));
            }
            var godot = new List<string>();
            FeedSchedule.Run(seed, 15000, 0f, breakField, true, e => godot.Add(FeedSchedule.Row(e)));
            Assert.That(rows, Is.EqualTo(godot), "field for field");
            return FeedSchedule.Digest(seed, 15000, 0f, breakField, true);
        }

        /// A dip's dropout is the port's own (StartDropout from the same state: its tell, its black or blue) until the dip
        /// decides its length.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(8)]
        public void TheDipsDropoutIsThePortsUntilTheDipEnds(int seed)
        {
            var dip = new Flight(seed, timer: false);
            var port = new FeedEvents(seed) { DropoutTimer = false };
            dip.Events.AdvanceTo(299, 0f);
            port.AdvanceTo(299, 0f);
            dip.DipBegins();
            port.Loss.StartDropout();
            for (int f = 300; f < 300 + 60; f++)
            {
                dip.Step();
                port.AdvanceTo(f, 0f);
                if (!port.Loss.Busy)
                    break; // the port's drawn outage is over; the dip's runs on
                Assert.That(FeedSchedule.Row(dip.Events), Is.EqualTo(FeedSchedule.Row(port)), $"field {f}");
            }
        }

        /// The timer (D-085) and the fiber share the machine without one cancelling the other: a dip during a timer's
        /// dropout holds its outage past its drawn end, or leaves it to its drawn end when the dip is the shorter; the
        /// timer never starts a dropout while a dip holds one; a dip on the terminal blue changes nothing; a break during a
        /// dip is the terminal loss, faded; the next flight's whole line takes the blue away.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheTimerAndTheFiberShareTheMachine(int seed)
        {
            // The timer's first dropout, and its drawn end (its first clean field).
            int start = 0, end = 0;
            FeedSchedule.Run(seed, 15000, 0f, 0, false, e =>
            {
                if (start == 0 && e.Loss.Busy) start = e.Field;
                if (start > 0 && end == 0 && !e.Loss.Busy) end = e.Field;
            });
            Assert.That(end - start, Is.GreaterThanOrEqualTo(4), "a tell and an outage");

            // A long dip from the timer's tell on: out until the dip's end, then clean.
            var flight = new Flight(seed, timer: true);
            while (flight.Events.Field < start + 1)
                flight.Step();
            Assert.That(flight.Events.Loss.Busy, Is.True, "the timer's dropout runs");
            flight.DipBegins();
            int dipEnd = end + 40;
            while (flight.Events.Field < dipEnd - 1)
                Assert.That(flight.Step().Busy, Is.True, $"held to the dip's end (field {flight.Events.Field})");
            Assert.That(Out(flight.Events.Loss), Is.True);
            flight.DipEnds();
            Assert.That(Out(flight.Step()), Is.True);
            Assert.That(Clean(flight.Step()), Is.True, "back clean after the dip");

            // A short dip in the timer's outage (on in its second field, over within its third): out to the later of the
            // timer's drawn end and the field after the dip's.
            flight = new Flight(seed, timer: true);
            while (!Out(flight.Step()))
            {
            }
            int first = flight.Events.Field;
            flight.DipBegins();
            flight.Step();
            flight.DipEnds();
            int clean = Math.Max(end, first + 3);
            while (flight.Events.Field < clean - 1)
                Assert.That(Out(flight.Step()), Is.True, $"out to field {clean - 1} (field {flight.Events.Field})");
            Assert.That(Clean(flight.Step()), Is.True, $"clean at field {clean}: the timer's drawn end {end}, the dip's {first + 2}");

            // A dip held for 2000 fields: the timer starts nothing on top and the picture never comes back; a break in the
            // dip is faded; a dip read on the blue does nothing; the next flight's whole line takes the blue away.
            flight = new Flight(seed, timer: true);
            while (flight.Events.Field < 99)
                flight.Step();
            flight.DipBegins();
            bool wasOut = false;
            for (int f = 0; f < 2000; f++)
            {
                FeedLoss l = flight.Step();
                Assert.That(l.Busy, Is.True);
                Assert.That(!wasOut || Out(l), Is.True, "once out, out while the dip holds");
                wasOut |= Out(l);
            }
            flight.Breaks("Bend");
            flight.Step();
            StringAssert.Contains("already in a dip", flight.Log[flight.Log.Count - 1]);
            for (int f = 0; f < 200; f++)
                flight.Step();
            flight.Signals.LinkInDip = true; // no dip is read after a break, but one would change nothing
            flight.Signals.LinkDips++;
            Assert.That(flight.Step().Current, Is.EqualTo(FeedLoss.State.Blue), "blue until reset");

            var next = new StubFeedSignals(); // the next flight's: whole, no dips
            string said = flight.Link.Apply(next, flight.Events.Loss, flight.Events.Field + 1);
            StringAssert.Contains("the line is whole again", said);
            bool back = false;
            for (int f = 0; f < 40 && !back; f++)
            {
                flight.Events.AdvanceTo(flight.Events.Field + 1, 0f);
                back = Clean(flight.Events.Loss);
            }
            Assert.That(back, Is.True, "a new line: the picture back");
        }

        /// A faded break (a dip on at the last reading) draws the staged loss with p 0.8, an abrupt one with p 0.2 (K§11.3,
        /// FeedLoss.cs:71): over 400 seeds each.
        [Test]
        public void HowFastTheMarginFellPicksTheVariant()
        {
            int fadedStaged = 0, abruptStaged = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var faded = new Flight(seed, timer: false);
                faded.Events.AdvanceTo(99, 0f);
                faded.DipBegins();
                faded.Step();
                faded.Breaks("Bend");
                faded.Step();
                fadedStaged += faded.Events.Loss.Staged ? 1 : 0;

                var abrupt = new Flight(seed, timer: false);
                abrupt.Events.AdvanceTo(100, 0f);
                abrupt.DipBegins(); // the dip and the break in one field: the margin vanished within it
                abrupt.Breaks("Bend");
                abrupt.Step();
                abruptStaged += abrupt.Events.Loss.Staged ? 1 : 0;
            }
            TestContext.WriteLine($"staged: faded {fadedStaged} of 400, abrupt {abruptStaged} of 400");
            Assert.That(fadedStaged, Is.InRange(290, 350), "p 0.8");
            Assert.That(abruptStaged, Is.InRange(50, 110), "p 0.2");
        }

        /// A dip shorter than its tell never takes the picture: the tell alone, then the picture back the field after.
        [Test]
        public void ADipShorterThanItsTellShowsTheTellAlone()
        {
            var flight = new Flight(4, timer: false);
            flight.Events.AdvanceTo(99, 0f);
            flight.DipBegins();
            flight.DipEnds(); // began and ended within field 100
            FeedLoss l = flight.Step();
            Assert.That(l.Busy && l.Current == FeedLoss.State.Picture && l.Tell + l.TearAmp > 0f, Is.True, "the tell");
            Assert.That(Clean(flight.Step()), Is.True);
            StringAssert.Contains("the dip already over", flight.Log[0]);
        }

        /// The review overlay names the link's causes beside its state.
        [Test]
        public void TheOverlayNamesTheCauses()
        {
            var s = new StubFeedSignals();
            Assert.That(FeedSignalLog.Value(s, FeedSignal.LinkState), Is.EqualTo("Intact"));
            s.LinkDips = 2;
            s.LinkInDip = true;
            s.LinkLossCause = "Lip";
            Assert.That(FeedSignalLog.Value(s, FeedSignal.LinkState), Is.EqualTo("Intact, dip 2 on Lip"));
            s.LinkInDip = false;
            Assert.That(FeedSignalLog.Value(s, FeedSignal.LinkState), Is.EqualTo("Intact, 2 dips"));
            s.LinkState = LinkState.Broken;
            s.TetherBreakMechanism = "Prop";
            Assert.That(FeedSignalLog.Value(s, FeedSignal.LinkState), Is.EqualTo("Broken by Prop"));
        }
    }
}
