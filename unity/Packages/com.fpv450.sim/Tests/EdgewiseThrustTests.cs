using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The climb after a pitch-back (D-087, model 76): each rotor's thrust and torque in the edgewise flow through its disc,
    // the props' measured axial tables times the blade elements' ratio in combined flow (RotorFlappingSpec.CombinedFlow,
    // Propulsion's edgewise tables). The known-wrong is model 75's axial-only rotor (EdgewiseThrust off).
    public sealed class EdgewiseThrustTests
    {
        static RotorFlappingSpec Blade() => RepositoryDrone.Spec().BuildFlapping();

        // K·I₁' = N_b·a·I₁/(2πR): the blade elements' thrust slope on λ with the inflow held (aσ/4 on a constant chord).
        static double Slope(RotorFlappingSpec f)
        {
            f.Integrals(out double i1, out _, out _, out _, out _, out _);
            return f.Blades * f.LiftSlope * i1 / (2 * System.Math.PI * f.Radius);
        }

        static double DctDlc(RotorFlappingSpec f, double lc, double mu)
        {
            const double d = 1e-5;
            f.CombinedFlow(lc + d, mu, out double c1, out _, out _);
            f.CombinedFlow(lc - d, mu, out double c0, out _, out _);
            return (c1 - c0) / (2 * d);
        }

        [Test]
        public void TheBladeElementsAxialRatioIsThePropsPublishedTable()
        {
            RotorFlappingSpec f = Blade();
            DroneProps p = RepositoryDrone.Spec().Props;
            f.CombinedFlow(0, 0, out double ct0, out _, out _);
            for (int k = 0; k < p.InflowAdvanceRatio.Length; k++)
            {
                double j = p.InflowAdvanceRatio[k];
                if (j > 0.3) break;
                f.CombinedFlow(j / System.Math.PI, 0, out double ct, out _, out _);
                TestContext.Out.WriteLine($"J {j:F4}: blade elements {ct / ct0:F4}, the props' table {p.InflowThrustRatio[k]:F4}");
                Assert.That(ct / ct0, Is.EqualTo(p.InflowThrustRatio[k]).Within(0.03), "the axial ratio at J " + j);
            }
        }

        // Glauert's inflow on the blade elements has Padfield's heave damping in closed form: in hover
        // dC_T/dλ_c = −2·s·λ₀/(4λ₀ + s), s = K·I₁' (Padfield's −2aσλ₀/(16λ₀ + aσ) on a constant chord), and at speed, as λ²
        // becomes small against μ², −2·s·μ/(2μ + s) (his −2aσμ/(8μ + aσ)). The axial-only rotor (model 75) keeps the hover's
        // slope at every μ and fails the forward form.
        [Test]
        public void TheClimbDampingIsPadfieldsInHoverAndInForwardFlightAndTheAxialRotorFailsIt()
        {
            RotorFlappingSpec f = Blade();
            double s = Slope(f);
            f.CombinedFlow(0, 0, out _, out _, out double l0);
            double hover = -2 * s * l0 / (4 * l0 + s), at0 = DctDlc(f, 0, 0);
            TestContext.Out.WriteLine($"hover: dCT/dlc {at0:E5}, Padfield {hover:E5} (lambda0 {l0:F4}, s {s:F4})");
            Assert.That(at0, Is.EqualTo(hover).Within(1e-4 * System.Math.Abs(hover)), "the hover's heave damping");
            foreach (double mu in new[] { 0.2, 0.3, 0.4 })
            {
                f.CombinedFlow(0, mu, out double ct, out _, out double l);
                double v = System.Math.Sqrt(mu * mu + l * l), g = s / (2 * v) + ct * l / (2 * v * v * v);
                double exact = -s / (1 + g), forward = -2 * s * mu / (2 * mu + s), at = DctDlc(f, 0, mu);
                TestContext.Out.WriteLine($"mu {mu:F2}: dCT/dlc {at:E5}, momentum closed form {exact:E5}, Padfield's forward {forward:E5}, axial-only {at0:E5}");
                Assert.That(at, Is.EqualTo(exact).Within(1e-4 * System.Math.Abs(exact)), "the combined flow's damping at " + mu);
                Assert.That(at, Is.EqualTo(forward).Within(0.08 * System.Math.Abs(forward)), "Padfield's forward form at " + mu);
                Assert.That(System.Math.Abs(at0 / forward - 1), Is.GreaterThan(0.25), "the axial-only rotor fails the forward form at " + mu);
            }
        }

        [Test]
        public void ThePropulsionTakesTheRatioOnTheTableAndTheKnownWrongDoesNot()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var on = new Propulsion(spec.BuildPropulsion()) { Flapping = spec.BuildFlapping() };
            RotorFlappingSpec offSpec = spec.BuildFlapping();
            offSpec.EdgewiseThrust = false;
            var off = new Propulsion(spec.BuildPropulsion()) { Flapping = offSpec };
            Assert.That(on.HasEdgewise, Is.True);
            Assert.That(off.HasEdgewise, Is.False);
            RotorFlappingSpec f = spec.BuildFlapping();
            double w = 1000, vt = w * f.Radius;
            foreach ((double va, double vp) in new[] { (0.0, 0.0), (5.0, 0.0), (0.0, 20.0), (10.7, 19.7), (2.0, 10.0) })
            {
                on.Aero(w, va, vp, out double t, out double q);
                off.Aero(w, va, vp, out double t0, out double q0);
                on.Aero(w, va, out double ta, out double qa);
                f.CombinedFlow(va / vt, vp / vt, out double ct, out double cq, out _);
                f.CombinedFlow(va / vt, 0, out double ct0, out double cq0, out _);
                TestContext.Out.WriteLine($"va {va} vp {vp}: thrust {t:F4} N (axial {ta:F4}, ratio {t / ta:F4}, blade elements {ct / ct0:F4}), "
                    + $"torque ratio {q / qa:F4} ({cq / cq0:F4}); the known-wrong {t0:F4} N");
                Assert.That(t0, Is.EqualTo(ta), "the known-wrong is the axial table");
                Assert.That(q0, Is.EqualTo(qa), "the known-wrong's torque is the axial table");
                if (vp == 0)
                {
                    Assert.That(t, Is.EqualTo(ta), "no edgewise flow: the axial table, bit for bit");
                    Assert.That(q, Is.EqualTo(qa));
                    continue;
                }
                // The bilinear tables against the blade elements at the point: within 1 %.
                Assert.That(t / ta, Is.EqualTo(ct / ct0).Within(0.01), "the thrust's ratio");
                Assert.That(q / qa, Is.EqualTo(cq / cq0).Within(0.01), "the torque's ratio");
                Assert.That(t / ta, Is.GreaterThan(1.05), "translational lift");
            }
        }
    }
}
