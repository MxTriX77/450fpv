using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // airframe-effects' "The rotors in each other's wake" (p7b-airframe 1.3d, D-060): Coleman's skewed vortex cylinder and its
    // interference factors.
    public sealed class RotorWakeTests
    {
        [Test]
        public void TheWakeGivesColemansInflowOverTheDisc()
        {
            // Coleman, Feingold and Stempin (1945): over the disc v = v_i·(1 + tan(χ/2)·x/R) along the flow, flat across it.
            foreach (double deg in new[] { 30.0, 60.0, 75.0 })
            {
                double u = System.Math.Tan(deg * System.Math.PI / 360);
                double slope = (RotorWakeSpec.PointFactor(u, 0.02, 0) - RotorWakeSpec.PointFactor(u, -0.02, 0)) / 0.04;
                double across = RotorWakeSpec.PointFactor(u, 0, 0.5);
                TestContext.Out.WriteLine($"chi {deg}: slope {slope:F5} (tan(chi/2) {u:F5}), across 0.5 R {across:F5}");
                Assert.That(slope, Is.EqualTo(u).Within(2e-3 * u), "the longitudinal gradient at " + deg);
                Assert.That(across, Is.EqualTo(1).Within(1e-3), "flat across the disc at " + deg);
            }
        }

        [Test]
        public void AnUnskewedWakeInducesNothingInThePlaneOutsideTheDisc()
        {
            // The semi-infinite vortex cylinder's end plane: no normal velocity outside it (exact). The known-wrong check: a
            // skewed one does induce it behind.
            foreach (double s in new[] { 1.6, 2.5, 3.5 })
                Assert.That(RotorWakeSpec.PointFactor(1e-9, s, 0.3), Is.EqualTo(0).Within(2e-3), "outside at " + s + " R");
            Assert.That(RotorWakeSpec.PointFactor(0.5, 2.5, 0), Is.GreaterThan(0.2), "a skewed wake's downwash behind");
        }

        // The known-wrong model, no interaction, has the rear rotors' thrust equal to the front's and fails the published order.
        [Test]
        public void TheRearRotorsLoseThrustInForwardFlight()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach ((double pitch, double speed) in RotorWakeScenario.Forward)
            {
                RotorWakeScenario.Result on = RotorWakeScenario.Cruise(spec, pitch, speed), off = RotorWakeScenario.Cruise(spec, pitch, speed, wake: false);
                TestContext.Out.WriteLine("wake: " + RotorWakeScenario.Describe(on) + "; none: " + RotorWakeScenario.Describe(off));
                Assert.That(on.RearUnderFront, Is.InRange(RotorWakeScenario.LeastLoss, RotorWakeScenario.MostLoss), "the published order at " + speed);
                Assert.That(on.RearLoss, Is.GreaterThan(0), "a downwash on the rear pair");
                Assert.That(on.FrontLoss, Is.LessThan(0), "an upwash on the front pair, from beside and behind");
                Assert.That(on.Speed.RearRight, Is.GreaterThan(off.Speed.RearRight), "the loop spins the rear rotors up to hold the pitch");
                Assert.That(off.RearUnderFront, Is.Not.InRange(RotorWakeScenario.LeastLoss, RotorWakeScenario.MostLoss), "the known-wrong fails it");
            }
        }

        [Test]
        public void HoverAndSlowFlightKeepTheirThrust()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            RotorWakeScenario.Result hover = RotorWakeScenario.Hover(spec), bare = RotorWakeScenario.Hover(spec, wake: false);
            RotorWakeScenario.Result slow = RotorWakeScenario.Cruise(spec, RotorWakeScenario.SlowPitch, RotorWakeScenario.SlowSpeed);
            TestContext.Out.WriteLine("hover: " + RotorWakeScenario.Describe(hover) + "; slow: " + RotorWakeScenario.Describe(slow));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(hover.Inflow[i], Is.EqualTo(0).Within(1e-3), "a hover in still air takes no wake");
                Assert.That(hover.Speed[i], Is.EqualTo(bare.Speed[i]).Within(1e-3 * bare.Speed[i]), "nor changes its rotors");
            }
            Assert.That(System.Math.Abs(slow.RearUnderFront), Is.LessThan(0.02), "slow flight's wake skews little");
        }

        [Test, Explicit("prints the table's cost and its readings")]
        public void TableReadings()
        {
            RotorWakeSpec spec = RepositoryDrone.Spec().BuildRotorWake();
            var hubs = new Double3[4];
            PropulsionSpec p = RepositoryDrone.Spec().BuildPropulsion();
            for (int i = 0; i < 4; i++) hubs[i] = p.Motors[i].Position;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            RotorWakeTable t = spec.Table(hubs);
            TestContext.Out.WriteLine($"built in {clock.Elapsed.TotalSeconds:F2} s; distances {string.Join(", ", t.Distances)}");
            foreach (double deg in new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 55.0, 60.0, 65.0, 70.0, 75.0, 80.0 })
            {
                double u = System.Math.Tan(deg * System.Math.PI / 360);
                // Flow along −z (forward flight): the front right (1) over the rear right (0), the front left (3) over the rear
                // right (diagonal), the rear right over the rear left (2, beside), the rear right over the front right (ahead).
                TestContext.Out.WriteLine($"chi {deg}: behind {t.Factor(1, 0, u, 0, -1):F4}, diagonal {t.Factor(3, 0, u, 0, -1):F4}, "
                    + $"beside {t.Factor(0, 2, u, 0, -1):F4}, ahead {t.Factor(0, 1, u, 0, -1):F4}; exact behind {spec.DiscFactor(u, t.Distances[0], 1, 0):F4}");
            }
        }
            [Test, Explicit("prints the wake's readings in held-pitch flight")]
        public void CruiseReadings()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach ((double pitch, double speed) in new[] { (-1.0, 2.0), (-3.0, 5.0), (-6.0, 10.0), (-12.0, 15.0), (-22.7, 20.6), (-30.0, 25.0) })
                foreach (bool wake in new[] { true, false })
                {
                    RotorWakeScenario.Result r = RotorWakeScenario.Cruise(spec, pitch, speed, wake);
                    TestContext.Out.WriteLine($"pitch {pitch} released {speed} wake {wake}: airspeed {r.Airspeed:F2} m/s, pitch {r.PitchDeg:F2}; "
                        + $"inflow rr/fr/rl/fl {r.Inflow[0]:F3} {r.Inflow[1]:F3} {r.Inflow[2]:F3} {r.Inflow[3]:F3} m/s; loss {100 * r.Loss[0]:F2} {100 * r.Loss[1]:F2} {100 * r.Loss[2]:F2} {100 * r.Loss[3]:F2} %; "
                        + $"speed {r.Speed[0]:F1} {r.Speed[1]:F1} {r.Speed[2]:F1} {r.Speed[3]:F1} rad/s; v_i {r.Induced[1]:F2} m/s, skew {r.SkewDeg[1]:F1} deg");
                }
        }
    }
}
