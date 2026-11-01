using Fpv450.Sim.Flight;
using static Fpv450.Sim.Tests.FlappingScenario;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // airframe-effects' "Rotor edgewise-flow moments" (p7b-airframe 1.3c): blade flapping and the dissymmetry of lift.
    public sealed class FlappingTests
    {
        [Test]
        public void TheFlapAndHubMomentAreJohnsonsAtThreeAdvanceRatios()
        {
            foreach ((string name, double hz, double sw) in new[] { ("articulated", 0.0, 1.0), ("hingeless", 150.0, 1.193), ("stiff", 1500.0, 1.193) })
            {
                RotorFlappingSpec f = Johnsons(hz, sw);
                double w0 = 2 * System.Math.PI * hz, ib2 = Inertia * Speed * Speed;
                double nu2 = 1 + Inertia * (w0 * w0 + (sw - 1) * Speed * Speed) / ib2;
                foreach (double mu in AdvanceRatios)
                {
                    Inflow(f, mu, out double lambda, out double lx);
                    Published(nu2, mu, lambda, lx, out double b0, out double b1c, out double b1s);
                    f.Solve(Speed, mu * Speed * Radius, 0, Thrust, out double m1c, out double m1s, out double c0, out double c1c, out double c1s);
                    double kb = ib2 * (nu2 - 1);
                    TestContext.Out.WriteLine($"{name} nu {System.Math.Sqrt(nu2):F3} mu {mu:F2}: beta0 {c0:E4} ({b0:E4}), beta1c {c1c:E4} ({b1c:E4}), "
                        + $"beta1s {c1s:E4} ({b1s:E4}); hub roll {m1s:E4} ({kb * b1s:E4}) N m, pitch {-m1c:E4} ({-kb * b1c:E4}) N m");
                    Assert.That(c0, Is.EqualTo(b0).Within(1e-9 * System.Math.Abs(b0)), name + " coning at " + mu);
                    Assert.That(c1c, Is.EqualTo(b1c).Within(1e-9 * System.Math.Abs(b1c)), name + " beta1c at " + mu);
                    Assert.That(c1s, Is.EqualTo(b1s).Within(1e-9 * System.Math.Abs(b1s)), name + " beta1s at " + mu);
                    Assert.That(m1c, Is.EqualTo(kb * b1c).Within(1e-9 * System.Math.Abs(kb * b1c) + 1e-15), name + " pitch moment at " + mu);
                    Assert.That(m1s, Is.EqualTo(kb * b1s).Within(1e-9 * System.Math.Abs(kb * b1s) + 1e-15), name + " roll moment at " + mu);
                    if (hz == 0)
                    {
                        Assert.That(c1c, Is.EqualTo(-mu * (8 * Theta0 / 3 + 2 * ThetaTw - 2 * lambda) / (1 - mu * mu / 2)).Within(1e-12), "Johnson's back flapping");
                        Assert.That(c1s, Is.EqualTo(-(4 * mu * c0 / 3 + lx) / (1 + mu * mu / 2)).Within(1e-12), "Johnson's lateral flapping");
                    }
                }
            }
        }

        // The rigid limit (ν → ∞): the hub's rolling moment is the dissymmetry of lift, C_Mx/σa = μ(θ₀/6 + θ_tw/8 − λ/8), and its
        // pitching moment the skewed inflow's, C_My/σa = λ_x/16 (Johnson's forms at β = 0). The known-wrong rigid rotor without
        // flapping, model 10's (no hub moment, the H-force alone), fails both by all of it.
        [Test]
        public void TheRigidLimitIsTheDissymmetryOfLiftAndARotorWithoutFlappingFailsIt()
        {
            RotorFlappingSpec f = Johnsons(1e7, 1.193);
            double sigma = 2 * Chord / (System.Math.PI * Radius), norm = Rho * System.Math.PI * Radius * Radius * Speed * Speed * Radius * Radius * Radius * sigma * Slope;
            foreach (double mu in AdvanceRatios)
            {
                Inflow(f, mu, out double lambda, out double lx);
                f.Solve(Speed, mu * Speed * Radius, 0, Thrust, out double m1c, out double m1s, out _, out _, out _);
                double roll = mu * (Theta0 / 6 + ThetaTw / 8 - lambda / 8) * norm, pitch = lx / 16 * norm;
                TestContext.Out.WriteLine($"rigid mu {mu:F2}: hub roll {m1s:E5} N m (published {roll:E5}), pitch {-m1c:E5} N m (published {pitch:E5}); "
                    + "model 10 (no flapping): 0 and 0");
                Assert.That(m1s, Is.EqualTo(roll).Within(1e-6 * roll), "the dissymmetry of lift at " + mu);
                Assert.That(-m1c, Is.EqualTo(pitch).Within(1e-6 * pitch), "the skewed inflow's pitch at " + mu);
                Assert.That(System.Math.Abs(0 - roll), Is.GreaterThan(0.5 * roll), "the rotor without flapping fails the roll");
                Assert.That(System.Math.Abs(0 - pitch), Is.GreaterThan(0.5 * pitch), "the rotor without flapping fails the pitch");
            }
            Assert.That(new Propulsion(RepositoryDrone.Spec().BuildPropulsion()).HubMoment(0, Speed, Thrust), Is.EqualTo(default(Double3)), "model 10: no hub moment");
        }

        // On the props' sheet at the hover's speed: the moments at the three advance ratios, the rolling parts opposite for
        // opposite spins and the pitching parts alike, and none in hover.
        [Test]
        public void TheSheetsMomentsAddInPitchAndCancelInRoll()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            RotorFlappingSpec f = spec.BuildFlapping();
            f.Integrals(out double i1, out double i2, out double i3, out double j1, out double j2, out double j3);
            TestContext.Out.WriteLine($"sheet: I1..3 {i1:E4} {i2:E4} {i3:E4} m, J1..3 {j1:E4} {j2:E4} {j3:E4} m rad, blade inertia {f.BladeInertia:E3} kg m2");
            var p = new Propulsion(spec.BuildPropulsion()) { Flapping = f };
            double speed = 8800 * 2 * System.Math.PI / 60, vt = speed * f.Radius;
            foreach (double mu in AdvanceRatios)
            {
                // The air from the north (toward −z): each hub moves +z through it.
                var s = new FlightState { Attitude = Quat.Identity, Velocity = new Double3(0, 0, mu * vt), RotorSpeed = new Corners() };
                p.RotorInflow(s, null);
                f.Solve(speed, mu * vt, 0, 12, out double m1c, out double m1s, out double b0, out double b1c, out double b1s);
                Double3 rr = p.HubMoment(0, speed, 12), fr = p.HubMoment(1, speed, 12), rl = p.HubMoment(2, speed, 12), fl = p.HubMoment(3, speed, 12);
                Double3 sum = rr + fr + rl + fl;
                TestContext.Out.WriteLine($"sheet mu {mu:F2} ({mu * vt:F1} m/s): beta0 {b0 * 57.29578:F3} deg, beta1c {b1c * 57.29578:F3} deg, beta1s "
                    + $"{b1s * 57.29578:F3} deg; per rotor roll {m1s:F4} N m, pitch {-m1c:F4} N m; the four: x {sum.X:F4} z {sum.Z:F4} N m");
                Assert.That(rr.Z, Is.EqualTo(-rl.Z).Within(1e-12), "the dissymmetry's roll changes sign with the spin");
                Assert.That(rr.X, Is.EqualTo(rl.X).Within(1e-12), "the back flapping's pitch does not");
                Assert.That(sum.Z, Is.EqualTo(0).Within(1e-12), "the rolls cancel over the quad");
                Assert.That(sum.X, Is.LessThan(0), "the discs tilt away from the air from the north: a nose-up moment (about −x)");
            }
            p.RotorInflow(new FlightState { Attitude = Quat.Identity }, null);
            Assert.That(p.HubMoment(0, speed, 12), Is.EqualTo(default(Double3)), "none in hover");
        }

        [Test]
        public void AGustStepTiltsTheDroneHarderWithFlapping()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (double from in new[] { 0.0, 90.0 })
            {
                FlappingScenario.Response on = FlappingScenario.GustStep(spec, from, true), off = FlappingScenario.GustStep(spec, from, false);
                TestContext.Out.WriteLine(FlappingScenario.Describe(from, on, off));
                if (from == 0) Assert.That(on.PitchPeak, Is.GreaterThan(1.5 * off.PitchPeak), "a head gust pitches harder");
                else Assert.That(on.RollPeak, Is.GreaterThan(1.5 * off.RollPeak), "a side gust rolls harder");
            }
        }
    }
}
