using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The own wake's unsteadiness by flight state (p7b-airframe design 3f; D-064's first source): a rotor held at a speed in
    /// a steady flow wanders with the hover's inflow σ times (v_i/v_h)², v_i Glauert's at its in-plane and axial flow (the
    /// axial held at Johnson's VzD in a descent), its correlation time R/(2π·0.2·V_t), V_t = v_h²/v_i the wake's transport
    /// velocity. Model 90's law (Propulsion.HoverWakeLaw), faded by v_i/v_h in the plane only, is the known-wrong.
    /// </summary>
    public class OwnWakeTests
    {
        const int Steps = 120000;

        /// <summary>One rotor's wake over Steps at 920 rad/s in a steady flow, body m/s (+y a climb): its inflow's σ over
        /// v_h, its autocorrelation at the law's τ, and the law's own numbers.</summary>
        struct Reading
        {
            public double Sigma, Expected, Correlation, Mu, Axial, Tau;
        }

        static Reading Fly(DroneSpec spec, Double3 velocity, bool hoverLaw)
        {
            RotorWakeSpec wake = spec.BuildRotorWake();
            var p = new Propulsion(spec.BuildPropulsion(), null, null, default, null, default, wake, new FlightRandom(7).Open("rotor.wake"))
            {
                HoverWakeLaw = hoverLaw,
            };
            var s = new FlightState { Attitude = Quat.Identity, Velocity = velocity };
            for (int i = 0; i < 4; i++) s.RotorSpeed[i] = 920;
            var x = new double[Steps];
            double thrust = 0;
            for (int k = 0; k < Steps; k++)
            {
                p.BeginStep(s);
                thrust += p.Thrust(0) / Steps;
                p.AdvanceWake(ref s, FlightCore.Dt);
                x[k] = s.WakeInflow[0];
                s.Step++;
            }
            double radius = spec.Props.Diameter / 2;
            double vh = Math.Sqrt(thrust / (2 * wake.AirDensity * Math.PI * radius * radius));
            var r = new Reading { Mu = Math.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z) / vh, Axial = velocity.Y / vh };
            double v = RotorWakeSpec.Induced(r.Mu, r.Axial);
            r.Expected = p.WakeSigma * v * v;
            r.Tau = radius / (2 * Math.PI * wake.CornerFrequency * vh / v);
            double mean = 0, c0 = 0, c1 = 0;
            foreach (double a in x) mean += a / Steps;
            int lag = (int)Math.Round(r.Tau / FlightCore.Dt);
            for (int k = 0; k < Steps; k++)
            {
                c0 += (x[k] - mean) * (x[k] - mean);
                if (k + lag < Steps) c1 += (x[k] - mean) * (x[k + lag] - mean);
            }
            r.Sigma = Math.Sqrt(c0 / Steps);
            r.Correlation = c1 / c0;
            return r;
        }

        static string Describe(in Reading r) =>
            $"μ {r.Mu:F2}, a {r.Axial:F2}: σ {r.Sigma:F4} v_h against the law's {r.Expected:F4}; at τ {r.Tau * 1000:F2} ms the autocorrelation {r.Correlation:F3}";

        // The law in each state, within 10 % of its σ and 15 % of 1/e at its τ: the hover (3e's), forward flight at μ 1 and
        // the cruise's μ 2 (the wake swept back), a climb at 0.3 v_h (it leaves faster), a descent past VzD (held there).
        [TestCase(0, 0, TestName = "TheOwnWakeFollowsItsFlowState(hover)")]
        [TestCase(1, 0, TestName = "TheOwnWakeFollowsItsFlowState(forward, mu 1)")]
        [TestCase(2, 0, TestName = "TheOwnWakeFollowsItsFlowState(the cruise, mu 2)")]
        [TestCase(0, 0.3, TestName = "TheOwnWakeFollowsItsFlowState(climb, 0.3 v_h)")]
        [TestCase(0, -0.6, TestName = "TheOwnWakeFollowsItsFlowState(descent, 0.6 v_h)")]
        public void TheOwnWakeFollowsItsFlowState(double mu, double axial)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            // The hover's v_h at 920 rad/s sets the flow; each reading takes its own mean thrust's.
            double vh = HoverInduced(spec);
            Reading r = Fly(spec, new Double3(mu * vh, axial * vh, 0), false);
            TestContext.WriteLine(Describe(r));
            Assert.That(r.Sigma, Is.EqualTo(r.Expected).Within(0.1 * r.Expected));
            Assert.That(r.Correlation, Is.EqualTo(1 / Math.E).Within(0.15 / Math.E));
            double v = RotorWakeSpec.Induced(r.Mu, r.Axial);
            if (mu >= 2) Assert.That(v * v, Is.LessThan(0.25), "at the cruise the hover's wander is a quarter or less");
            if (axial < -RotorWakeSpec.DescentHold)
                Assert.That(v, Is.EqualTo(RotorWakeSpec.Induced(r.Mu, -RotorWakeSpec.DescentHold)), "held at VzD: deeper is the ring's");
        }

        // The known-wrong, model 90's law: at the cruise its σ is the first power of v_i/v_h (twice the law's), and its
        // correlation time the hover's, so it fails both of the law's checks there.
        [Test]
        public void ModelNinetysLawFailsTheCruise()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double vh = HoverInduced(spec);
            Reading r = Fly(spec, new Double3(2 * vh, 0, 0), true);
            TestContext.WriteLine("model 90's law: " + Describe(r));
            Assert.That(Math.Abs(r.Sigma - r.Expected) > 0.1 * r.Expected || Math.Abs(r.Correlation - 1 / Math.E) > 0.15 / Math.E, Is.True);
            Assert.That(r.Sigma / r.Expected, Is.GreaterThan(1.8), "the first power, about 2× the law's σ");
        }

        // Glauert's root: v·√(μ² + (a + v)²) = 1 to the last bits over the flight envelope, 1 in a hover, the momentum
        // branch's closed forms in the plane (v⁴ + μ²v² = 1) and on the axis, and held below −DescentHold.
        [Test]
        public void TheInducedVelocityIsGlauerts()
        {
            Assert.That(RotorWakeSpec.Induced(0, 0), Is.EqualTo(1).Within(1e-15));
            foreach (double mu in new[] { 0, 0.05, 0.5, 1, 2, 5, 30 })
                foreach (double a in new[] { -0.2, -0.1, 0, 0.3, 1, 5, 10 })
                {
                    double v = RotorWakeSpec.Induced(mu, a), u = a + v;
                    Assert.That(v * Math.Sqrt(mu * mu + u * u), Is.EqualTo(1).Within(1e-14), $"μ {mu}, a {a}");
                }
            Assert.That(RotorWakeSpec.Induced(2, 0), Is.EqualTo(RotorWakeSpec.Fade(2)).Within(1e-15), "in the plane it is model 90's v_i/v_h");
            Assert.That(RotorWakeSpec.Induced(0, 0.3), Is.EqualTo(-0.15 + Math.Sqrt(0.0225 + 1)).Within(1e-15), "a climb's closed form");
            Assert.That(RotorWakeSpec.Induced(0, -3), Is.EqualTo(0.1 + Math.Sqrt(1.01)).Within(1e-15), "a deep descent held at VzD");
        }

        /// <summary>One rotor's thrust over Steps at 920 rad/s in a steady flow, body m/s, with its own wake at the spec's level
        /// (or model 92's): each step's thrust, and its v_h from the mean.</summary>
        static double[] Thrusts(DroneSpec spec, Double3 velocity, bool level92, out double vh)
        {
            RotorWakeSpec wake = spec.BuildRotorWake();
            if (level92) GoldenFlights.Model92WakeLevel(wake);
            var p = new Propulsion(spec.BuildPropulsion(), null, null, default, null, default, wake, new FlightRandom(7).Open("rotor.wake"));
            var s = new FlightState { Attitude = Quat.Identity, Velocity = velocity };
            for (int i = 0; i < 4; i++) s.RotorSpeed[i] = 920;
            var t = new double[Steps];
            double mean = 0;
            for (int k = 0; k < Steps; k++)
            {
                p.BeginStep(s);
                t[k] = p.Thrust(0);
                mean += t[k] / Steps;
                p.AdvanceWake(ref s, FlightCore.Dt);
                s.Step++;
            }
            double radius = spec.Props.Diameter / 2;
            vh = Math.Sqrt(mean / (2 * wake.AirDensity * Math.PI * radius * radius));
            return t;
        }

        static double Share(double[] t, int from, int n, out double peakToPeak)
        {
            double mean = 0, var = 0, lo = double.MaxValue, hi = double.MinValue;
            for (int k = from; k < from + n; k++) mean += t[k] / n;
            for (int k = from; k < from + n; k++)
            {
                var += (t[k] - mean) * (t[k] - mean) / n;
                lo = Math.Min(lo, t[k]);
                hi = Math.Max(hi, t[k]);
            }
            peakToPeak = (hi - lo) / mean;
            return Math.Sqrt(var) / mean;
        }

        // Design 3g: the hover's thrust read as Stack, Caradonna and Savaş 2004 read theirs (rotor-thrust-spectrum.md §2): in
        // windows as long as their record, 35 R/v_h (90 revolutions over their τ of 0.78 R/v_h), the record's own σ, 1.72 % of
        // the mean, lies inside the middle 80 % of the sim's windows; model 92's level (2.9 %, the record's peak to peak read as
        // 4.2σ) puts it under every one of its windows but a few, so it fails. The window's peak to peak against its own σ is
        // printed: the record's 7.3 is a kurtosis of 4.3, which a Gaussian process does not carry.
        [Test]
        public void TheHoversLevelIsTheRecords()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            const double record = 0.0172, recordSpan = 7.3, recordLength = 35;
            foreach (bool level92 in new[] { false, true })
            {
                double[] t = Thrusts(spec, default, level92, out double vh);
                int n = (int)Math.Round(recordLength * spec.Props.Diameter / 2 / vh / FlightCore.Dt), windows = Steps / n;
                var sigmas = new double[windows];
                int wide = 0;
                for (int w = 0; w < windows; w++)
                {
                    sigmas[w] = Share(t, w * n, n, out double span);
                    if (span / sigmas[w] >= recordSpan) wide++;
                }
                Array.Sort(sigmas);
                double p10 = sigmas[windows / 10], p50 = sigmas[windows / 2], p90 = sigmas[windows * 9 / 10];
                int under = 0;
                foreach (double x in sigmas) if (x <= record) under++;
                TestContext.WriteLine($"{(level92 ? "model 92's level" : "the measured level")}: {windows} windows of {n} steps (35 R/v_h at v_h {vh:F2} m/s): "
                    + $"σ 10 / 50 / 90 % {p10:P2} / {p50:P2} / {p90:P2} of the thrust, the record's {record:P2} at or over {under} of them; "
                    + $"a peak to peak of 7.3σ or more in {wide}");
                if (level92) Assert.That(record, Is.LessThan(p10), "model 92's level is too lively for its own record");
                else Assert.That(record, Is.InRange(p10, p90), "the record's σ is a window of the sim's");
            }
        }

        // Design 3g's one measured point away from the hover: Stack et al.'s fast shallow descent (their Fig. 8, V_x/v_h 1.62,
        // V_z/v_h −0.93, the disc level, assumed; v_h the run's own), whose trimmed σ is the hover's (1.32–1.41 % against 1.36–
        // 1.50 %). The law's thrust σ there over the hover's, and its corner, printed beside the record's; nothing is fitted.
        [Test, Explicit("a printed comparison: one point")]
        public void TheFastShallowDescentAgainstTheLaw()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double[] hover = Thrusts(spec, default, false, out double vh0);
            double h = Share(hover, 0, Steps, out _);
            // The flow is set on the hover's v_h, then read on the run's own (its thrust differs).
            double[] t = Thrusts(spec, new Double3(1.62 * vh0, -0.93 * vh0, 0), false, out double vh);
            double d = Share(t, 0, Steps, out _), mu = 1.62 * vh0 / vh, a = -0.93 * vh0 / vh, v = RotorWakeSpec.Induced(mu, a);
            TestContext.WriteLine($"hover: σ {h:P2} of the thrust at v_h {vh0:F2} m/s");
            TestContext.WriteLine($"the descent: μ {mu:F2}, a {a:F2} (held at {-RotorWakeSpec.DescentHold:F1}): v_i/v_h {v:F3}, the inflow's σ × {v * v:F3}, "
                + $"the corner × {1 / v:F2}; the thrust's σ {d:P2} of it at v_h {vh:F2} m/s, {d / h:F2} of the hover's (the record: 0.92–0.97 trimmed alike, "
                + $"1.23 whole with its drift); model 90's fade {RotorWakeSpec.Fade(mu):F2}");
            // What reaches the thrust: in a descent past the props' held branch (Propulsion.DescentAdvanceRatioLimit, J −0.05)
            // the inflow's wander moves the thrust on one side only. An axial descent at 0.59 v_h (OwnWakeTests' case), and a
            // shallow one inside the branch, against the law's inflow ratio.
            foreach (double axial in new[] { -0.15, -0.59 })
            {
                double[] x = Thrusts(spec, new Double3(0, axial * vh0, 0), false, out double vx);
                double r = Share(x, 0, Steps, out _), va = RotorWakeSpec.Induced(0, axial * vh0 / vx);
                TestContext.WriteLine($"an axial descent at {-axial:F2} v_h (J {axial * vh0 / (920 / (2 * Math.PI) * spec.Props.Diameter):F3}): the inflow's σ × {va * va:F3}, "
                    + $"the thrust's σ {r:P2}, {r / h:F2} of the hover's");
            }
        }

        static double HoverInduced(DroneSpec spec)
        {
            var p = new Propulsion(spec.BuildPropulsion(), null, null, default, null, default, null, default);
            var s = new FlightState { Attitude = Quat.Identity };
            for (int i = 0; i < 4; i++) s.RotorSpeed[i] = 920;
            p.BeginStep(s);
            double radius = spec.Props.Diameter / 2;
            return Math.Sqrt(p.Thrust(0) / (2 * 1.225 * Math.PI * radius * radius));
        }
    }
}
