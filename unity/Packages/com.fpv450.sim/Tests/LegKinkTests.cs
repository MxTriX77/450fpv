using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // w9-village-physics 2.6 (design.md 2.6): the legs' failure law on the section's own capacities. The clamp edge's K_t lowers
    // only the edge's local yield (the crack, which whitens a ductile tube and keeps its stiffness); the section sets from its
    // own yield and is lost at its kink, before its edge tears. Then G2: across a 10° slope the cut landing's legs outlast the
    // drone's roll, which is the soft legs' sway, not a leg that parts under the static load.
    public sealed class LegKinkTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        // The legs' law with every strength at its nominal value (each bound collapsed onto it).
        static LegDamage Nominal()
        {
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            LegDamageSpec s = Spec.BuildContacts(Spec.Nominal, m, h).Legs.Clone();
            LegBound Pin(LegBound b) => new LegBound { Value = b.Value, Min = b.Value, Max = b.Value };
            s.YieldStress = Pin(s.YieldStress);
            s.Elongation = Pin(s.Elongation);
            s.EdgeFactor = Pin(s.EdgeFactor);
            return new LegDamage(s, new FlightRandom(1));
        }

        [Test]
        public void TheTubeKinksBeforeItTearsAndTheRaiserLowersOnlyTheEdge()
        {
            LegDamage d = Nominal();
            LegDamageSpec s = d.Spec;
            double i = s.SecondMoment, c = s.OuterDiameter / 2, sy = s.YieldStress.Value, e = s.Modulus, kt = s.EdgeFactor.Value;
            double my = sy * i / c, t = (s.OuterDiameter - s.InnerDiameter) / 2;
            // The hand calculation's numbers (design.md 2.6 §1), each from its closed form.
            double kink = (LegDamage.KinkCoefficient * e * t / s.OuterDiameter + sy) * i / c;
            double ky = my / (e * i), g = s.Elongation.Value / (kt * c) / ky, tear = (s.ShapeFactor - (s.ShapeFactor - 1) / (g * g)) * my;
            double model90 = (s.ShapeFactor - (s.ShapeFactor - 1) / Math.Pow(s.Elongation.Value / (kt * c) / (my / kt / (e * i)), 2)) * my / kt;
            TestContext.Out.WriteLine($"edge {d.CrackMoment(0):F3}, section yield {d.YieldMoment(0):F3}, kink {kink:F3}, tear {tear:F3}, "
                + $"lost at {d.PartingMoment(0):F3} N·m; model 90's parting (K_t on the section) {model90:F3}");
            Assert.That(d.CrackMoment(0), Is.EqualTo(my / kt).Within(1e-12), "the crack: the edge's local yield");
            Assert.That(d.YieldMoment(0), Is.EqualTo(my).Within(1e-12), "the set: the section's own yield");
            Assert.That(kink, Is.LessThan(tear), "a 10/8 PVC-U tube kinks before its edge tears");
            Assert.That(d.PartingMoment(0), Is.EqualTo(kink).Within(1e-9), "lost at its kink");
            Assert.That(d.PartingMoment(0), Is.EqualTo(3.55).Within(0.01), "3.55 N·m, the hand calculation's");
            Assert.That(model90, Is.LessThan(my), "the known-wrong (model 90's K_t on the section) parts before the section yields");
            Assert.That(s.CrackedFactor, Is.EqualTo(1), "the crack keeps the tube's stiffness (drone.json)");
        }

        // The cut landing across the package's 9.98° slope (`--slopes`' row): the drone's tilt from upright when a leg first keeps a
        // set (bends past its section's yield) and when it is first lost.
        static double TiltAtTheFirst(DroneSpec spec, EventKind kind, out SlopeScenarios.Touchdown t)
        {
            double at = double.NaN;
            t = SlopeScenarios.Land(spec, 9.98, RainLevel.Sunny, 90, observe: core =>
            {
                if (!double.IsNaN(at)) return;
                for (int k = 0; k < core.Events.Count; k++)
                    if (core.Events[k].Kind == kind && core.Events[k].Index != (byte)LegMode.SkidFold)
                    {
                        double up = core.State.Attitude.Rotate(new Double3(0, 1, 0)).Y;
                        at = Math.Acos(Math.Max(-1, Math.Min(1, up))) * 57.29577951308232;
                        return;
                    }
            });
            return at;
        }

        [Test]
        public void AcrossATenDegreeSlopeTheLegsOutlastTheRoll()
        {
            // The known-wrong: model 90's crack, the tube's stiffness halved at the edge's first yield (its lever's sway load with it).
            string text = RepositoryDrone.Text();
            const string now1 = "\"cracked_stiffness_factor\": {\"value\": 1.0";
            int at = text.IndexOf(now1, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "drone.json's crack factor");
            DroneSpec halved = DroneSpec.Parse(text.Substring(0, at) + "\"cracked_stiffness_factor\": {\"value\": 0.5" + text.Substring(at + now1.Length));
            double setNow = TiltAtTheFirst(Spec, EventKind.LegSet, out SlopeScenarios.Touchdown t), lostNow = TiltAtTheFirst(Spec, EventKind.LegBroken, out _);
            double setWrong = TiltAtTheFirst(halved, EventKind.LegSet, out SlopeScenarios.Touchdown w), lostWrong = TiltAtTheFirst(halved, EventKind.LegBroken, out _);
            TestContext.Out.WriteLine($"a leg first set at a tilt of {setNow:F1}°, first lost at {lostNow:F1}° (end {t.EndTilt:F1}°); with the halved crack "
                + $"{setWrong:F1}° and {lostWrong:F1}° (end {w.EndTilt:F1}°)");
            Assert.That(double.IsNaN(lostNow) || lostNow > 30, "no leg is lost before the drone has rolled past 30°: the legs part in the fall, if at all");
            // The soft legs' quasi-static roll tip, 14.40° (p7g-legs 1.3, LandingStability.SoftRollTipDeg, derived): a leg that first
            // yields past it yields in the roll; one that yields before it yields under the landing's load on the slope.
            const double tip = 14.40;
            Assert.That(double.IsNaN(setNow) || setNow > tip, "no leg yields before the drone has rolled past its soft legs' tip");
            Assert.That(setWrong, Is.LessThan(tip), "the known-wrong (model 90's halved crack) yields a leg on the slope, before the roll");
        }
    }
}
