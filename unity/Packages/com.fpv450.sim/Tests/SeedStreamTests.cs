using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-randomness: "One flight seed, independent named streams" (design 9).
    public sealed class SeedStreamTests
    {
        const ulong Seed = 0x0123456789ABCDEFUL;
        const int Draws = 1000;

        static ulong[] Sequence(FlightRandom random, string name, int count)
        {
            SeedStream s = random.Open(name);
            var bits = new ulong[count];
            for (int i = 0; i < count; i++) bits[i] = s.NextBits();
            return bits;
        }

        // Scenario "Adding a stream shifts nothing": a new stream, opened first and drawn from in between, and the
        // existing streams opened in another order, leave every existing stream's draws bit-identical.
        [Test]
        public void AddingAStreamShiftsNothing()
        {
            string[] existing = { "wind.direction", "wind.strength", "wind.turbulence" };
            var before = new FlightRandom(Seed);
            var reference = new ulong[existing.Length][];
            for (int k = 0; k < existing.Length; k++) reference[k] = Sequence(before, existing[k], Draws);

            var after = new FlightRandom(Seed);
            SeedStream added = after.Open("motor.kv_spread");
            var streams = new SeedStream[existing.Length];
            for (int k = existing.Length - 1; k >= 0; k--) streams[k] = after.Open(existing[k]);
            for (int i = 0; i < Draws; i++)
            {
                added.NextBits();
                after.Draw(ref added, "motor.kv_spread.m1", -0.015, 0.015);
                for (int k = 0; k < existing.Length; k++)
                    Assert.That(streams[k].NextBits(), Is.EqualTo(reference[k][i]), existing[k] + " draw " + i);
            }
        }

        // The key and the draws are fixed forever, since old logs replay through them. The expected values come from
        // an independent Python implementation of FNV-1a 64 and SplitMix64 (derived).
        [TestCase(Seed, "wind.direction", 0x609740D3FE199702UL, 0x84717CA596C16804UL, 0xD8E8D5228C7C1B8AUL, 0xC53390979165AA25UL)]
        [TestCase(Seed, "wind.turbulence", 0x134F4694D9A35ED9UL, 0xA05CF165C4BCD47CUL, 0x7B00953AD4EAF412UL, 0x7FB2CBF630E6BF21UL)]
        [TestCase(1UL, "wind.direction", 0x9E1DAAA2079B5522UL, 0x9D592F4DE239C8FFUL, 0x6A1270A84B9A2233UL, 0xFDE761C2DD85DD4AUL)]
        public void KeysAndDrawsArePinned(ulong seed, string name, ulong key, ulong d0, ulong d1, ulong d2)
        {
            Assert.That(FlightRandom.StreamKey(seed, name), Is.EqualTo(key));
            SeedStream s = new FlightRandom(seed).Open(name);
            Assert.That(s.NextBits(), Is.EqualTo(d0));
            Assert.That(s.NextBits(), Is.EqualTo(d1));
            Assert.That(s.NextBits(), Is.EqualTo(d2));
            Assert.That(s.BitsAt(1), Is.EqualTo(d1), "counter-based: draw i is a function of (key, i) alone");
        }

        [Test]
        public void SeedsAndNamesGiveDifferentStreams()
        {
            Assert.That(FlightRandom.StreamKey(Seed, "wind.direction"), Is.Not.EqualTo(FlightRandom.StreamKey(Seed + 1, "wind.direction")));
            Assert.That(FlightRandom.StreamKey(Seed, "wind.direction"), Is.Not.EqualTo(FlightRandom.StreamKey(Seed, "wind.strength")));
        }

        [Test]
        public void PerFlightDrawsAreRecordedInsideTheirBound()
        {
            var random = new FlightRandom(Seed);
            SeedStream s = random.Open("wind.strength");
            for (int i = 0; i < Draws; i++)
            {
                double v = random.Draw(ref s, "wind.strength", 0.6, 1.0);
                Assert.That(v, Is.InRange(0.6, 1.0));
            }
            Assert.That(random.Draws.Count, Is.EqualTo(Draws));
            ParameterDraw last = random.Draws[Draws - 1];
            Assert.That(last.Stream, Is.EqualTo("wind.strength"));
            Assert.That(last.Parameter, Is.EqualTo("wind.strength"));
            Assert.That(last.Lo, Is.EqualTo(0.6));
            Assert.That(last.Hi, Is.EqualTo(1.0));
            Assert.That(last.Index, Is.EqualTo((ulong)(Draws - 1)));
        }

        [Test]
        public void UnitDrawsStayInTheHalfOpenInterval()
        {
            SeedStream s = new FlightRandom(Seed).Open("test.unit");
            for (int i = 0; i < 100000; i++)
            {
                double u = s.NextUnit();
                Assert.That(u >= 0 && u < 1, "draw " + i + " = " + u);
            }
        }

        [Test]
        public void AStreamOpensOnceAndNamesAreAscii()
        {
            var random = new FlightRandom(Seed);
            random.Open("wind.direction");
            Assert.Throws<InvalidOperationException>(() => random.Open("wind.direction"));
            Assert.Throws<ArgumentException>(() => random.Open("vent.напрям"));
            Assert.Throws<ArgumentException>(() => random.Open(""));
        }
    }
}
