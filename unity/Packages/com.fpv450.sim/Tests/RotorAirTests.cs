using System;
using System.Diagnostics;
using Fpv450.Sim.Tether;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // fiber-tether (p7d-tether 1.6b, D-075): RotorAir's fountain and ground vortex against the measurements they come from,
    // on those experiments' own rotors; the figures print for the as-built note.
    public sealed class RotorAirTests
    {
        static void Print(string line) => TestContext.Progress.WriteLine(line);

        static TetherTable Table => TetherScenarios.Table();

        // A field of up to four rotors over a floor at y = 0, their discs level: each hub, its thrust, the air at it relative
        // to it (the rest at rest, far away, no thrust).
        static RotorAir Field(double radius, Double3[] hubs, double[] thrust, Double3[] air, bool recirculation = true)
        {
            var field = new RotorAir(Table, radius, recirculation);
            var h = new Double3[4];
            var t = new double[4];
            var a = new Double3[4];
            var g = new double[4];
            for (int i = 0; i < 4; i++)
            {
                h[i] = i < hubs.Length ? hubs[i] : new Double3(1e3 * (i + 1), 1, 0);
                t[i] = i < thrust.Length ? thrust[i] : 0;
                a[i] = i < air.Length ? air[i] : default;
            }
            field.Begin(h, new Double3(0, 1, 0), t, a, g);
            return field;
        }

        static double Rise(RotorAir field, double x, double y, double z = 0) => field.At(new Double3(x, y, z), 0, out _).Y;

        // The thrust whose hover induced velocity is vh on a disc of radius r (momentum theory, the table's air).
        static double Thrust(double r, double vh) => 2 * Table.Number("fiber.air_density_kg_m3") * Math.PI * r * r * vh * vh;

        // Dekker, Baars, Scarano, Tuinstra and Ragni (Flow 3, E30, 2023): two APC 6×4 rotors, R 76.2 mm, 2.14 N each, one
        // diameter apart tip to tip; w_ind 14.25 m/s. At H/R 4 the fountain (w ≥ 0.25 w_ind) reaches 3R and stays below the
        // discs; at H/R 2 it reaches a similar height, above the discs, and is re-ingested. The fountain's momentum ratio is
        // derived from the first (so the reach is 3R by construction, within the table's rounding); the second is the check:
        // the field does not depend on the stand-off while both wakes land, and at H/R 2 it still rises past 0.25 w_ind at the
        // discs' plane, where their intake takes it.
        [Test]
        public void TheFountainRisesAsMeasured()
        {
            const double r = 0.0762, thrust = 2.14, wInd = 14.25, gap = 2 * r;
            double x = r + gap / 2, threshold = 0.25 * wInd;
            RotorAir high = Field(r, new[] { new Double3(-x, 3.999 * r, 0), new Double3(x, 3.999 * r, 0) }, new[] { thrust, thrust }, new Double3[0]);
            RotorAir low = Field(r, new[] { new Double3(-x, 2 * r, 0), new Double3(x, 2 * r, 0) }, new[] { thrust, thrust }, new Double3[0]);
            Assert.That(high.Lands(0) && high.Lands(1) && low.Lands(0) && low.Lands(1), "both wakes land at H/R 4 and 2");
            double lo = 0.5 * r, hi = 3.999 * r;
            Assert.That(Rise(high, 0, lo), Is.GreaterThan(threshold));
            for (int k = 0; k < 60; k++)
            {
                double mid = 0.5 * (lo + hi);
                if (Rise(high, 0, mid) > threshold) lo = mid;
                else hi = mid;
            }
            double reach = lo / r;
            double atPlane = Rise(low, 0, 2 * r * (1 - 1e-9)), same = Math.Abs(Rise(high, 0, r) - Rise(low, 0, r));
            Print($"Dekker et al.: the fountain's w ≥ 0.25 w_ind ({threshold:F3} m/s) reaches {reach:F3} R at H/R 4 (measured 3 R, below the discs); "
                + $"at H/R 2, {atPlane:F3} m/s at the discs' plane ({atPlane / wInd:F3} w_ind: above the discs, re-ingested); the two fields at 1 R differ by {same:E1} m/s; "
                + $"v_h {high.Hover(0):F3} m/s");
            Assert.That(reach, Is.EqualTo(3.0).Within(0.1), "the fountain reaches 3R at H/R 4");
            Assert.That(reach, Is.LessThan(3.999), "below the discs");
            Assert.That(atPlane, Is.GreaterThan(threshold), "at H/R 2 it reaches the discs, which re-ingest it");
            Assert.That(same, Is.LessThan(1e-12), "the same fountain at both stand-offs while both wakes land");
            Assert.That(Rise(high, 0, 4.001 * r), Is.LessThanOrEqualTo(0), "above the discs' plane their intake, not the fountain");
        }

        // Lee, Chae, Shin and Kim (J. Fluid Mech. 1031, A42, 2026): twin rotors, R 50.8 mm, 1.14 N each, at h/R 1: the wall jets'
        // stagnation line at x/R −2.1, −1.5 and −1.2 from rotor 1's centre for tip gaps s/R 2.20, 0.95 and 0.45 (the field's
        // fountain peaks on the line of equal jets, their midpoint for equal rotors); the fountain's peak upwash at z/R −0.08,
        // 1.2, 1.4 and 0.8 v_h, which sits in the discs' near field (the tips' suction and vortices, not in design 4's field
        // under the discs' plane) and is compared, not matched: it needs more momentum than the colliding jets carry at the two
        // wider gaps (the momentum ratio would pass 1). A rotor 10 % faster in v_i moves the line toward the weaker, to the
        // Apollonius point r₂/r₁ = v₂/v₁ ("positioning ... largely dependent upon the momentum ratio of the wall jets").
        [TestCase(2.20, -2.1, 1.2)]
        [TestCase(0.95, -1.5, 1.4)]
        [TestCase(0.45, -1.2, 0.8)]
        public void TheWallJetsMeetWhereMeasured(double gap, double stagnation, double measured)
        {
            const double r = 0.0508, thrust = 1.14;
            double other = -(2 + gap) * r;
            RotorAir field = Field(r, new[] { new Double3(0, r, 0), new Double3(other, r, 0) }, new[] { thrust, thrust }, new Double3[0]);
            double peak = Peak(field, other, 0.5 * r);
            double vh = field.Hover(0), tip = Rise(field, other / 2, r - 0.08 * r) / vh;
            RotorAir uneven = Field(r, new[] { new Double3(0, r, 0), new Double3(other, r, 0) }, new[] { thrust * 1.21, thrust }, new Double3[0]);
            double k = uneven.Induced(1) / uneven.Induced(0), apollonius = other * 1 / (1 + k); // r₁ = |other|/(1 + k) from rotor 1
            double shifted = Peak(uneven, other, 0.5 * r);
            Print($"Lee et al. s/R {gap:F2}: the fountain peaks at x/R {peak / r:F3} (measured stagnation {stagnation:F2}); at z/R −0.08 "
                + $"{tip:F3} v_h against the measured {measured:F1} ({tip / measured:F2} of it); v_h {vh:F3} m/s; rotor 1 at 1.21 × thrust: "
                + $"x/R {shifted / r:F3} (the Apollonius point {apollonius / r:F3})");
            Assert.That(peak / r, Is.EqualTo(stagnation).Within(0.05), "the wall jets meet where measured");
            Assert.That(tip / measured, Is.InRange(0.4, 2.5), "the near-tip upwash of the measured order");
            Assert.That(shifted / r, Is.EqualTo(apollonius / r).Within(0.02), "toward the weaker rotor, as the jets' momentum");
            Assert.That(shifted, Is.LessThan(peak), "the stronger rotor (at 0) pushes the line toward the weaker");
        }

        // The x of the fountain's peak between 0 and other at height y (a scan, then golden-section on the bracket).
        static double Peak(RotorAir field, double other, double y)
        {
            double best = 0, at = 0;
            for (int i = 1; i < 2000; i++)
            {
                double x = other * i / 2000, w = Rise(field, x, y);
                if (w > best) { best = w; at = x; }
            }
            double a = at + other / 2000, b = at - other / 2000;
            for (int k = 0; k < 80; k++)
            {
                double c = b - (b - a) * 0.618034, d = a + (b - a) * 0.618034;
                if (Rise(field, c, y) > Rise(field, d, y)) b = d;
                else a = c;
            }
            return 0.5 * (a + b);
        }

        // Wolf, Weiss, Schwarz, Braukmann, Koch and Raffel (J. AHS 67, 012010, 2022): the Bo105 (R 4.92 m) in ground effect, hub
        // at 0.96–1.02 R, v_h 10.98 m/s (6.04 m/s at μ* 0.55): the near-ground speed's peak at y/R 0.19 ahead of the hub,
        // 1.5 v_h at x/R 1.07 (μ* 0.55, the recirculation regime) and 1.75 v_h at 0.49 (μ* 0.80, the ground vortex under the
        // disc); the regimes after Sheridan and Wiesner (1977) and Curtiss et al. (1984): hover-like to μ* 0.4, the vortex
        // under the disc between the bow and the tip for 0.7–0.9, gone beyond 0.9 (none at 1.19). The position's two constants
        // and the circulation are derived from the two points; the regimes are the check. On design 4's field there is none.
        [Test]
        public void TheGroundVortexStandsWhereMeasured()
        {
            const double r = 4.92, vh = 10.98;
            double thrust = Thrust(r, vh);
            RotorAir At(double mu, bool recirculation = true) =>
                Field(r, new[] { new Double3(0, r, 0) }, new[] { thrust }, new[] { new Double3(-mu * vh, 0, 0) }, recirculation);
            foreach ((double mu, double x, double speed) in new[] { (0.55, 1.07, 1.5), (0.80, 0.49, 1.75) })
            {
                RotorAir f = At(mu);
                Double3 c = f.VortexCentre(0), under = f.At(new Double3(c.X, 0.19 * r, c.Z), 0, out _);
                double u = Math.Sqrt(under.X * under.X + under.Z * under.Z) / vh;
                Print($"Wolf et al. μ* {mu:F2}: the vortex's centre {c.X / r:F3} R ahead of the hub (measured {x:F2}), {c.Y / r:F3} R up, "
                    + $"Γ {f.VortexCirculation(0) / (vh * r):F3} v_h·R; the speed under it at y/R 0.19 {u:F3} v_h (measured {speed:F2}, {u / speed - 1:+0.0%;-0.0%})");
                Assert.That(c.X / r, Is.EqualTo(x).Within(0.01), "where measured (the position's constants derived from these two)");
                Assert.That(u / speed, Is.InRange(0.9, 1.1), "the speed under it within 10 %");
            }
            string regimes = "";
            foreach (double mu in new[] { 0.0, 0.2, 0.4, 0.55, 0.7, 0.8, 0.9, 1.0, 1.19, 1.5 })
            {
                RotorAir f = At(mu);
                double g = f.VortexCirculation(0), x = g != 0 ? f.VortexCentre(0).X / r : double.NaN;
                regimes += $" μ* {mu:F2}: {(g != 0 ? $"{x:F2} R, Γ {g / (vh * r):F2}" : "none")};";
                if (mu == 0 || mu >= 1.19) Assert.That(g, Is.EqualTo(0), "no vortex at hover, nor beyond the flight test's 1.19");
                else Assert.That(g, Is.GreaterThan(0));
                if (mu >= 0.7 && mu <= 0.9) Assert.That(x, Is.InRange(0.0, 1.0), "under the disc ahead of the hub in the ground vortex regime");
                if (mu > 0 && mu <= 0.55) Assert.That(x, Is.GreaterThan(1.0), "ahead of the disc in the hover-like and early recirculation regimes");
                Assert.That(At(mu, false).VortexCirculation(0), Is.EqualTo(0), "none on design 4's field");
            }
            Print("the regimes:" + regimes);
        }

        // The vortex's sense and its image: under it the flow runs upstream along the ground (the forward wall jet), up on its
        // upstream side, back over its top; at the ground no flow through it. A quad reversing low over its line: the leading
        // pair's wall jets roll up, the trailing pair's meet the leading wakes and form none.
        [Test]
        public void TheGroundVortexTurnsAsTheWallJet()
        {
            const double r = 0.127;
            double vh = 10.1, thrust = Thrust(r, vh), h = 2.27 * r, s = 0.15981;
            // flying −z (a reverse) at μ* 0.55: the air at the hubs moves +z
            var air = new Double3(0, 0, 0.55 * vh);
            RotorAir f = Field(r, new[] { new Double3(s, h, -s), new Double3(-s, h, -s), new Double3(s, h, s), new Double3(-s, h, s) },
                new[] { thrust, thrust, thrust, thrust }, new[] { air, air, air, air });
            Assert.That(f.VortexCirculation(0), Is.GreaterThan(0), "the rear (leading) pair rolls up its jets");
            Assert.That(f.VortexCirculation(1), Is.GreaterThan(0));
            Assert.That(f.VortexCirculation(2), Is.EqualTo(0), "the front pair's jets meet the rear wakes");
            Assert.That(f.VortexCirculation(3), Is.EqualTo(0));
            Double3 c = f.VortexCentre(0);
            Assert.That(c.Z, Is.LessThan(f.Landing(0).Z), "ahead of the wake, upstream");
            RotorAir wrong = Field(r, new[] { new Double3(s, h, -s), new Double3(-s, h, -s), new Double3(s, h, s), new Double3(-s, h, s) },
                new[] { thrust, thrust, thrust, thrust }, new[] { air, air, air, air }, false);
            Double3 Vortex(Double3 p) => f.At(p, 0, out _) - wrong.At(p, 0, out _); // the vortices' share (the fountain has faded)
            Double3 under = Vortex(new Double3(c.X, 0.25 * c.Y, c.Z)), ahead = Vortex(new Double3(c.X, c.Y, c.Z - 0.5 * c.Y)),
                over = Vortex(new Double3(c.X, 1.75 * c.Y, c.Z)), ground = Vortex(new Double3(c.X, 0, c.Z - 0.3 * c.Y));
            Print($"the reverse at μ* 0.55: the rear vortices Γ {f.VortexCirculation(0):F3} and {f.VortexCirculation(1):F3} m²/s, the centre "
                + $"{f.Landing(0).Z - c.Z:F3} m ahead of the landing, {c.Y * 1000:F1} mm up; the vortices' air under it {under.Z:F2} m/s along z, "
                + $"at its upstream side {ahead.Y:F2} m/s up, over it {over.Z:F2} m/s along z, at the ground {ground.Y:E1} m/s up; fountain shares "
                + $"{f.FountainShare(0):F3}");
            Assert.That(under.Z, Is.LessThan(0), "under it the flow runs upstream (−z, the way the drone flies)");
            Assert.That(ahead.Y, Is.GreaterThan(0), "up at its upstream side");
            Assert.That(over.Z, Is.GreaterThan(0), "back downstream over its top");
            Assert.That(Math.Abs(ground.Y), Is.LessThan(1e-12), "no flow through the ground (the image)");
        }

        // A quad's four hubs, square at motor distance l (the diagonal), at height h over the floor, each at thrust t, still air.
        static RotorAir Quad(double r, double l, double h, double t, bool cutOff = false)
        {
            double s = l / (2 * Math.Sqrt(2));
            var field = new RotorAir(Table, r, true, null, cutOff);
            field.Begin(new[] { new Double3(s, h, -s), new Double3(-s, h, -s), new Double3(s, h, s), new Double3(-s, h, s) },
                new Double3(0, 1, 0), new[] { t, t, t, t }, new Double3[4], new double[4]);
            return field;
        }

        // Design 19 against Bauersfeld, Muller, Ziegler, Coletti and Scaramuzza (IEEE RA-L 2024, arXiv 2403.13321): the Matrice 300
        // (6.3 kg, 53.34 cm props, l 89.4 cm; U_H 5.36 m/s in their Table I) hovering 100 m up: from 2.5 l the merged jet's
        // centreline is U_H·10.11/(s/l + 5.817) and its half-width 0.07668·(s/l + 5.817)·l (their eq. 9: the law is their fit, so
        // this checks it is built as published), the profile half at r½. The known-wrong (model 94's columns without end) puts
        // nothing on the axis between the discs and 2 v_i under each hub at any depth: it fails the measured centreline.
        [Test]
        public void TheMergedJetDecaysAsMeasured()
        {
            const double r = 0.2667, l = 0.894, mass = 6.3;
            double t = mass * 9.80665 / 4;
            RotorAir f = Quad(r, l, 100, t), wrong = Quad(r, l, 100, t, cutOff: true);
            double uh = f.FarSpeed, rho = Table.Number("fiber.air_density_kg_m3");
            Print($"Bauersfeld et al.'s Matrice 300: U_H {uh:F3} m/s (their 5.36 at their air), l {f.MotorDistance:F4} m; the jet's momentum "
                + $"flux {Math.PI * Math.Pow(10.11 * 0.07668 * l, 2) * uh * uh / (3 * (Math.Sqrt(2) - 1)) * rho / (4 * t):F3} of the thrust");
            Assert.That(uh / 5.36, Is.InRange(0.98, 1.02), "U_H as their Table I (their air slightly thinner)");
            Assert.That(f.MotorDistance, Is.EqualTo(l).Within(1e-12));
            foreach (double st in new[] { 2.5, 3.0, 5.0, 10.0 })
            {
                double y = 100 - st * l, uc = -f.At(new Double3(0, y, 0), 0, out _).Y, measured = uh * 10.11 / (st + 5.817);
                double half = 0.07668 * (st + 5.817) * l, atHalf = -f.At(new Double3(half, y, 0), 0, out _).Y;
                double wrongAxis = -wrong.At(new Double3(0, y, 0), 0, out _).Y;
                double wrongHub = -wrong.At(new Double3(l / (2 * Math.Sqrt(2)), y, -l / (2 * Math.Sqrt(2))), 0, out _).Y;
                Print($"s/l {st:F1}: U_C {uc:F3} m/s (measured {measured:F3}), at r½ {atHalf / uc:F4} of it; the cut-off: {wrongAxis:F3} on the axis, {wrongHub:F3} under a hub");
                Assert.That(uc / measured - 1, Is.EqualTo(0).Within(1e-12), "the centreline as published");
                Assert.That(atHalf / uc, Is.EqualTo(0.5).Within(1e-12), "half at the half-width");
                Assert.That(Math.Abs(wrongAxis / measured - 1), Is.GreaterThan(0.5), "the cut-off fails the measured centreline");
                Assert.That(wrongHub / measured, Is.GreaterThan(1.5), "and keeps 2 v_i under a hub at any depth");
            }
        }

        // Design 19's landing: the session's drone (R 0.127 m, l 0.452 m, v_h 10.1 m/s) hovering at 1.5 to 10 m. The merged jet's
        // momentum turns into the wall jet at r_i (derived: the wall jet's momentum flux there equals the jet's, ∫f² checked by
        // quadrature); beyond, the peak falls as r^−1.1 (Poreh et al. 1967) and is nearly independent of the height (Fradenburgh
        // 1960: the momentum flux is the same from any height); a line lying at 0.3 mm feels it. The known-wrong 4 R cut-off puts
        // nothing on the ground from 1.5 m. Below one motor distance the field is the cut-off's (model 94's) to the bit.
        [Test]
        public void TheOutwashLandsFromAnyHeight()
        {
            const double r = 0.127, l = 0.452;
            double t = Thrust(r, 10.1), a = Math.Sqrt(2) - 1;
            double[] profile = Table.Row("rotor_air.wall_jet_profile").Values;
            double F(double eta) => profile[1] * Math.Pow(eta, 1.0 / 7) * (1 - Erf(profile[0] * eta));
            double square = 0;
            for (int k = 0; k < 200000; k++) square += Math.Pow(F((k + 0.5) * 5e-5), 2) * 5e-5;
            Print($"Verhoff's ∫f² {square:F5} (the table's {Table.Number("rotor_air.wall_jet_profile_square_integral")}), f(1) {F(1):F4}");
            Assert.That(Table.Number("rotor_air.wall_jet_profile_square_integral"), Is.EqualTo(square).Within(1e-4));
            double peak = 0.1648, previous = double.NaN;
            foreach (double h in new[] { 1.5, 3.0, 6.0, 10.0 })
            {
                RotorAir f = Quad(r, l, h, t);
                Assert.That(f.FarLands && f.FarLandingShare == 1 && f.NearShare == 0, $"the merged jet alone lands from {h} m");
                double ri = f.FarStart, ug = f.FarGroundSpeed, x = h / l + 5.817, half = 0.07668 * x * l, hv = h + 5.817 * l;
                double Um(double rr)
                {
                    double big = Math.Max(rr, ri), delta = 0.098 * big * Math.Pow(big / hv, -0.1);
                    f.At(new Double3(rr, peak * delta, 0), 0, out Double3 jet);
                    return jet.X;
                }
                double deltaI = 0.098 * ri * Math.Pow(ri / hv, -0.1);
                double wall = 2 * Math.PI * ri * ug * ug * deltaI * square, free = Math.PI * ug * ug * half * half / (3 * a);
                double u2 = Um(2), u4 = Um(4);
                f.At(new Double3(2, 0.0003, 0), 0, out Double3 line);
                Print($"hub at {h} m: U_g {ug:F3} m/s, r_i {ri:F3} m ({ri / hv:F3} H); the wall jet's momentum at r_i {wall / free:F4} of the jet's; "
                    + $"u_m {Um(ri):F3} at r_i, {u2:F3} at 2 m, {u4:F3} at 4 m (ratio {u4 / u2:F4}, r^−1.1 {Math.Pow(2, -1.1):F4}); at a line's 0.3 mm at 2 m {line.X:F3} m/s");
                Assert.That(wall / free, Is.EqualTo(1).Within(2e-3), "the wall jet carries the landing jet's momentum flux");
                Assert.That(Um(ri) / ug, Is.EqualTo(1).Within(1e-3), "it starts at the jet's centreline speed");
                if (ri < 2) Assert.That(u4 / u2, Is.EqualTo(Math.Pow(2, -1.1)).Within(1e-3), "the peak falls as r^−1.1");
                Assert.That(line.X, Is.GreaterThan(0.5), "a line lying at 0.3 mm feels the outwash");
                if (!double.IsNaN(previous)) Assert.That(u2 / previous, Is.InRange(0.8, 1.25), "nearly independent of the height");
                previous = u2;
            }
            Quad(r, l, 1.5, t, cutOff: true).At(new Double3(2, 0.0003, 0), 0, out Double3 none);
            Assert.That(none.X, Is.Zero, "the known-wrong 4 R cut-off: no outwash from 1.5 m");
            RotorAir low = Quad(r, l, 0.3, t), cut = Quad(r, l, 0.3, t, cutOff: true);
            for (int k = 0; k < 200; k++)
            {
                var p = new Double3(-1 + 0.01 * k, 0.0003 + 0.002 * (k % 7), 0.003 * k - 0.3);
                Double3 x = low.At(p, 0, out Double3 j1), y = cut.At(p, 0, out Double3 j2);
                Assert.That(x.X == y.X && x.Y == y.Y && x.Z == y.Z && j1.X == j2.X && j1.Z == j2.Z, $"below l the cut-off's field to the bit at {p.X}, {p.Y}, {p.Z}");
            }
            // 0 B with the merged jet landing (the far path's Log and Exp).
            RotorAir high = Quad(r, l, 1.5, t);
            double sink = 0;
            for (int k = 0; k < 2000; k++) sink += high.At(new Double3(0.01 * (k % 300), 0.0003 + 0.001 * (k % 13), 0.2), 0, out _).X;
            long before = AllocProbe.Before();
            for (int k = 0; k < 32000; k++) sink += high.At(new Double3(0.01 * (k % 300), 0.0003 + 0.001 * (k % 13), 0.2), 0, out _).X;
            long allocated = AllocProbe.After(before);
            Print($"the merged jet's At: {allocated} B ({sink:E1})");
            Assert.That(allocated, Is.Zero, "0 B" + AllocProbe.Report());
        }

        // erf by its series to rounding (the test's own, not the field's Abramowitz and Stegun fit).
        static double Erf(double x)
        {
            if (x > 5) return 1; // erfc(5) = 1.5e-12
            double sum = 0, term = x;
            for (int n = 0; n < 200; n++)
            {
                sum += term / (2 * n + 1);
                term *= -x * x / (n + 1);
            }
            return 2 / Math.Sqrt(Math.PI) * sum;
        }

        // The field's cost over the line's 32 nodes and its allocation: At at the nodes of a line laid under a hovering drone and
        // trailing ahead of a reversing one, with the fountain and the vortex and on design 4's field (Release; the machine
        // shared, so under load: the orchestrator re-measures on an idle machine).
        [Test]
        public void TheFieldCostsLittleAndAllocatesNothing()
        {
            const double r = 0.127;
            double vh = 10.1, thrust = Thrust(r, vh), h = 2.27 * r, s = 0.15981;
            var hubs = new[] { new Double3(s, h, -s), new Double3(-s, h, -s), new Double3(s, h, s), new Double3(-s, h, s) };
            var thrusts = new[] { thrust, thrust, thrust, thrust };
            var nodes = new Double3[32];
            for (int i = 0; i < 32; i++) nodes[i] = new Double3(0.002 * i, i < 8 ? 0.03 * i : 0.0002, -0.1 - 0.05 * i);
            foreach ((string name, Double3 air) in new[] { ("hover", default(Double3)), ("reverse at μ* 0.55", new Double3(0, 0, 0.55 * vh)) })
            {
                var stream = new[] { air, air, air, air };
                RotorAir on = Field(r, hubs, thrusts, stream), off = Field(r, hubs, thrusts, stream, false);
                double sink = 0;
                foreach (RotorAir f in new[] { on, off })
                    for (int k = 0; k < 2000; k++)
                        for (int i = 0; i < 32; i++) sink += f.At(nodes[i], 0, out _).Y; // the JIT's warm-up
                // the least of 20 alternating rounds of 1000 steps each (the least is the field's own cost on a shared machine)
                double[] ns = { double.PositiveInfinity, double.PositiveInfinity };
                long before = AllocProbe.Before();
                for (int round = 0; round < 40; round++)
                {
                    RotorAir f = round % 2 == 0 ? on : off;
                    long t0 = Stopwatch.GetTimestamp();
                    for (int k = 0; k < 1000; k++)
                        for (int i = 0; i < 32; i++) sink += f.At(nodes[i], 0, out _).Y;
                    double t = (Stopwatch.GetTimestamp() - t0) * 1e9 / Stopwatch.Frequency / (1000.0 * 32);
                    if (t < ns[round % 2]) ns[round % 2] = t;
                }
                long allocated = AllocProbe.After(before);
                Print($"{name}: At {ns[0]:F1} ns a node with the fountain and the ground vortex, {ns[1]:F1} ns on design 4's field "
                    + $"(+{ns[0] - ns[1]:F1} ns, {32 * (ns[0] - ns[1]) / 1000:F2} µs a step over 32 nodes; the least of 20 rounds, under load); {allocated} B ({sink:E1})");
                Assert.That(allocated, Is.Zero, "0 B in the steps" + AllocProbe.Report());
            }
        }
    }
}
