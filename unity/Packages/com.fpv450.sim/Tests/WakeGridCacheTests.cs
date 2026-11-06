using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p6-flight-core, the build's freeze: a new flight's first wake grid is WakeGrid.Build spread over the cores, and the last
    // one is kept (WakeGridCache) for the next flight with the same obstacles and bearing. The grid is a pure function of
    // them, so a restart's copy, the parallel build and the worker's one-thread Rebuild give the same bits, and a restart
    // flies the first flight's bits.
    public sealed class WakeGridCacheTests
    {
        static WakeLayout Range() => WindSetup.ForMap(RangeScenarios.World, WindLevel.Windy).Wakes;

        // The one-thread build (the worker's Rebuild into a spare), the reference for the others.
        static ulong Sequential(WakeLayout layout, double fromDeg)
        {
            WakeGrid grid = WakeGrid.Spare(layout.Obstacles());
            grid.Rebuild(fromDeg);
            return grid.Hash();
        }

        [Test]
        public void TheKeptGridIsTheBuildsBitsAndOnlyForItsLayoutAndBearing()
        {
            WakeGridCache.Clear();
            foreach (double deg in new[] { 211.25, 37.0 })
            {
                ulong reference = Sequential(Range(), deg);
                int hits = WakeGridCache.Hits, misses = WakeGridCache.Misses;
                using (WakeGridWorker built = WakeGridCache.Worker(Range(), deg))
                {
                    Assert.That(WakeGridCache.Misses, Is.EqualTo(misses + 1), $"a new bearing, {deg} deg, is built");
                    Assert.That(built.Active.Hash(), Is.EqualTo(reference), $"the parallel build at {deg} deg is the one-thread build's bits");
                    Assert.That(built.Active.WakeCells, Is.GreaterThan(1000), "the range's obstacles have wakes");
                }
                using (WakeGridWorker kept = WakeGridCache.Worker(Range(), deg))
                {
                    Assert.That(WakeGridCache.Hits, Is.EqualTo(hits + 1), $"the same layout (a new object, the same content) and bearing reuse the grid");
                    Assert.That(kept.Active.Hash(), Is.EqualTo(reference), $"the kept copy at {deg} deg");
                    Assert.That(kept.Active.FromDeg, Is.EqualTo(deg));
                }
            }

            // One bit of the bearing, or one volume moved, is another grid.
            double next = System.BitConverter.Int64BitsToDouble(System.BitConverter.DoubleToInt64Bits(37.0) + 1);
            int before = WakeGridCache.Misses;
            using (WakeGridWorker w = WakeGridCache.Worker(Range(), next)) Assert.That(w.Active.FromDeg, Is.EqualTo(next));
            Assert.That(WakeGridCache.Misses, Is.EqualTo(before + 1), "the bearing's next double");
            WakeLayout moved = Range();
            moved.Volumes[moved.Volumes.Length / 2].TopM += 0.5;
            using (WakeGridWorker w = WakeGridCache.Worker(moved, next))
            {
                Assert.That(WakeGridCache.Misses, Is.EqualTo(before + 2), "one volume's top");
                Assert.That(w.Active.Hash(), Is.EqualTo(Sequential(moved, next)));
            }
        }

        [Test]
        public void ARestartReusesTheGridAndFliesTheFirstFlightsBits()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            WakeGridCache.Clear();
            int hits = WakeGridCache.Hits, misses = WakeGridCache.Misses;
            ulong first = GoldenFlights.FlyRange(spec, out FlightCore a, wind: WindSetup.ForMap(RangeScenarios.World, WindLevel.Windy));
            Assert.That(WakeGridCache.Misses, Is.EqualTo(misses + 1), "the first flight builds its grid");
            Assert.That(WakeGridCache.Hits, Is.EqualTo(hits));

            // The restart: the same setup made again (as the app's SessionSetup.Build does), the same seed.
            ulong restart = GoldenFlights.FlyRange(spec, out FlightCore b, wind: WindSetup.ForMap(RangeScenarios.World, WindLevel.Windy));
            Assert.That(WakeGridCache.Hits, Is.EqualTo(hits + 1), "the restart reuses the grid");
            Assert.That(WakeGridCache.Misses, Is.EqualTo(misses + 1));
            Assert.That(b.Wind.Wakes.Active, Is.Not.SameAs(a.Wind.Wakes.Active), "each flight's worker owns its grid");
            Assert.That(restart, Is.EqualTo(first), "the restart's log is the first flight's, bit for bit");
            Assert.That(b.State.Step, Is.EqualTo(GoldenFlights.RangeSteps));

            // The grid is in this flight's air: at the start, 1 m over the straw, the wake is not the open field's.
            Double3 p = RangeScenarios.Straw;
            a.Wind.Wakes.Active.Sample(p.X, p.Z, 1, out WakeSample s);
            Assert.That(s.Speed != 1 || s.Turbulence != 1, Is.True, $"a wake at the start (speed {s.Speed}, turbulence {s.Turbulence})");
        }
    }
}
