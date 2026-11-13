using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// w9-village-physics 2.12's addendum (design.md "## 2.12", "Addendum"): a liftoff from inside standing straw, on the range's
    /// S04 patch and in Village's densest tall cell, dry, in Rain and in Severe rain, flown by the liftoff harness's own flight
    /// (LiftoffScenarios.Fly, a vertical touch and a glide); and the step's guard that a state that is not finite fails in the step
    /// it appears, naming the part.
    /// </summary>
    public class StemLiftoffTests
    {
        static readonly RainLevel[] Weathers = { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain };

        static void Liftoffs(string name, Func<FlightWorld> open, Func<FlightWorld, Double3> foot)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (RainLevel rain in Weathers)
                for (ulong seed = 1; seed <= 2; seed++)
                {
                    FlightWorld world = open();
                    Double3 at = foot(world);
                    LiftoffScenarios.Liftoff r = LiftoffScenarios.Fly(spec, world, at, 0.478, rain, seed, seed % 2 == 0);
                    TestContext.Out.WriteLine($"{name}, {rain}, seed {seed} ({(seed % 2 == 0 ? "glide" : "vertical")}): off at {r.Off:F3} s, throttle {r.OffThrottle:F3}; "
                        + $"sink {Max(r.Sink) * 1000:F2} mm; lone {(r.Lone >= 0 ? LegDamage.Names[r.Lone] : "--")} {r.LoneHold * 1000:F0} ms; pull {r.Pull:F1} N; "
                        + $"crashes {r.Crashes}, tunnels {r.Tunnels}");
                    Assert.That(double.IsNaN(r.Off), Is.False, $"{name}, {rain}, seed {seed}: the drone lifts off");
                    Assert.That(r.Crashes, Is.EqualTo(0));
                    Assert.That(r.Tunnels, Is.EqualTo(0));
                }
        }

        static double Max(double[] a) => Math.Max(Math.Max(a[0], a[1]), Math.Max(a[2], a[3]));

        // The range's S04 patch: its straw mat's pieces stand 0.125 m on a soil box set in a hole in the ground plane, and its
        // stalks stand in the mat. The drone starts with its skids 2 mm over the highest face under each (LiftoffScenarios.Over).
        [Test]
        public void ALiftoffFromTheRangesStrawIsFlown()
        {
            Liftoffs("S04 patch", () => new FlightWorld(SoilScenarios.Range()), SoilScenarios.PatchCentre);
        }

        // Village's densest tall cell: the field's stems stand round the legs and under the discs as the rotors spin up.
        [Test]
        public void ALiftoffFromVillagesTallStemsIsFlown()
        {
            Liftoffs("Village's densest tall cell", () => new FlightWorld(SlopeScenarios.Farmland()), _ =>
                new Double3(StemScenarios.Dense.X, StemScenarios.Ground(StemScenarios.Dense), StemScenarios.Dense.Z));
        }

        // The defect's first link: a start over the S04 patch's hole read the ground as −∞ and put the body there. The core now
        // refuses a start that is not finite, naming the field.
        [Test]
        public void AStartThatIsNotFiniteIsRefused()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var world = new FlightWorld(SoilScenarios.Range());
            FlightSetup setup = SoilScenarios.Setup(spec, world, SoilScenarios.PatchCentre(world), RainLevel.Rain, 1);
            setup.StartPosition = new Double3(0, double.NegativeInfinity, 0);
            var e = Assert.Throws<ArgumentException>(() => new FlightCore(setup));
            TestContext.Out.WriteLine(e.Message);
            Assert.That(e.Message, Does.Contain("StartPosition"));
        }

        // The step's guard: a body rate the step cannot carry (1e308 rad/s) overflows in its first step, and the step throws
        // there, naming the quantity and the part that made it, before anything downstream (the rain film's mass) reads it.
        [Test]
        public void AValueThatIsNotFiniteFailsInItsStep()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var world = new FlightWorld(SoilScenarios.Range());
            FlightSetup setup = SoilScenarios.Setup(spec, world, SoilScenarios.TrackStart(world) + new Double3(0, 2, 0), RainLevel.Rain, 1);
            setup.StartBodyRate = new Double3(1e308, 0, 0);
            var core = new FlightCore(setup);
            var e = Assert.Throws<NonFiniteStateException>(() =>
            {
                for (int k = 0; k < 10; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
            });
            TestContext.Out.WriteLine(e.Message);
            Assert.That(e.Step, Is.EqualTo(0).Or.EqualTo(1), "in the step it appears");
            Assert.That(e.Part, Is.Not.Empty);
        }
    }
}
