using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Fpv450.Sim;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.WorldBench
{
    // The farmland's wind rows (w7-farmland-wind 1.1, golden/farmland-wind.txt): the hills' flow (HillFlow) and the field over
    // them (WindField.At(point, ground)) at a crest, a lee slope, below the ledge, an open field and a laid node, each printed
    // against its reference:
    // - The crest and the lee slope: the map's highest point and the wind from the west, across its ridge. References (both
    //   computed here, independently of HillFlow's grids): Jackson and Hunt's linear theory in two dimensions on the measured
    //   profile along the wind (the outer layer's potential flow by quadrature of the profile's slope, the layers of Hunt et al.
    //   at the hill's own L1), and Taylor and Lee (1984)'s guidelines from the profile's h and L1 (2.0 and 1.6 h/L1, ±15 %,
    //   falling as e^(−A z/L1), A 3 and 4).
    // - Below the ledge: the east step at z = 500 with the wind blowing out over it. References: the reattachment at 7.2 D
    //   (Hotta et al. 1998; the step literature's 7.0–7.5) and the cell's return flow; the unseparated field is the known-wrong.
    // - An open field: the flattest spot of the map and its surround; references the flat ground's profile (ΔS 0) and Jackson and
    //   Hunt in 2-D on its profile (the layers at L 200 m, assumed: the farmland's hill scale).
    // - A laid node: a node lying on the crest while the drone hovers over the village: its own ground (the cover's still air)
    //   against the drone's (the known-wrong, model 82's rule).
    // The golden hashes every printed number's bits, the hills' grid on a lattice, and the full Severe field at the rows' points.
    internal static partial class Program
    {
        private const double ProfileStep = 2, ProfileReach = 2000;

        private static readonly double[] RowHeights = { 2, 5, 10, 20 };

        private static List<(string Name, string Value, bool Agree)> FarmlandWindCases()
        {
            var cases = new List<(string Name, string Value, bool Agree)>();
            MapWorld map = Farmland();
            WindPreset p = WindPreset.Of(WindLevel.Severe);
            var clock = Stopwatch.StartNew();
            HillFlow hills = HillFlow.Of(map);
            Console.WriteLine($"  farmland_wind.hills: built in {clock.ElapsedMilliseconds} ms (HillFlow {HillFlow.Version}): {HillFlow.Size}² nodes "
                + $"{HillFlow.Cell:F0} m apart from ({hills.OriginX:F0}, {hills.OriginZ:F0}), {HillFlow.Levels} levels; the grid's steepest slope "
                + $"{hills.SteepestSlope:F3}, {hills.Steeper} nodes past {HillFlow.SeparationSlope}; {hills.Steps} steps separate (the ledge's)");
            cases.Add(("farmland_wind.version", HillFlow.Version + "." + LogFormatModel(), true));

            // The grid on a lattice: three levels, 64 × 64 points over the map and its surround.
            var grid = new Hasher();
            for (int level = 0; level < 3; level++)
                for (int j = 0; j < 64; j++)
                    for (int i = 0; i < 64; i++)
                    {
                        hills.Sample(-1200 + i * 3200.0 / 63, -1200 + j * 3200.0 / 63, level * 2.5, out HillSample s);
                        grid.Add(s.S11); grid.Add(s.S12); grid.Add(s.S22); grid.Add(s.G1); grid.Add(s.G2);
                    }
            cases.Add(("farmland_wind.grid", Hex(grid.Value), true));

            // The crest: the map's highest point (2 m grid), the wind from the west (toward +x).
            double cx = 0, cz = 0, top = double.NegativeInfinity;
            for (double z = 0; z <= 1000; z += 2)
                for (double x = 0; x <= 1000; x += 2)
                {
                    double h = GroundAt(map, x, z);
                    if (h > top) { top = h; cx = x; cz = z; }
                }
            int n = (int)(2 * ProfileReach / ProfileStep) + 1, mid = n / 2;
            var profile = new double[n];
            for (int k = 0; k < n; k++) profile[k] = GroundAt(map, cx + (k - mid) * ProfileStep, cz);
            Hill(profile, mid, -1, out double hu, out double l1u, out int footU);
            Hill(profile, mid, 1, out double hd, out double l1d, out int footD);
            var rows = new Hasher();
            Console.WriteLine($"  farmland_wind.crest: the map's highest point ({cx:F0}, {cz:F0}) {top:F2} m, the wind from the west across it: h {hu:F2} m over "
                + $"the upwind foot {(mid - footU) * ProfileStep:F0} m away, L1 {l1u:F0} m, h/L1 {hu / l1u:F3}, L1/z0 {l1u / p.Roughness:F0}, the steepest slope "
                + $"from foot to foot {Steepest(profile, footU, footD):F3} (Taylor and Lee's range: slopes < 0.3, L1/z0 > 100, L1 < 2 km)");
            foreach (double h in RowHeights)
            {
                double model = Along(hills, cx, cz, h, p), jh = JacksonHunt(profile, mid, h, l1u, p);
                double ridge = 2.0 * hu / l1u * Math.Exp(-3 * h / l1u), round = 1.6 * hu / l1u * Math.Exp(-4 * h / l1u);
                rows.Add(model); rows.Add(jh); rows.Add(ridge); rows.Add(round);
                Console.WriteLine($"    {h,4:F0} m: ΔS {model:F3}; Jackson and Hunt in 2-D on the profile {jh:F3} (model/JH {model / jh:F2}); Taylor and Lee "
                    + $"{round:F3}–{ridge:F3} ± 15 % (3-D hill – 2-D ridge); the plane 0");
            }
            cases.Add(("farmland_wind.crest", Hex(rows.Value), true));

            // The lee slope: downwind at the half-height point (L1 of the lee) and at twice that.
            rows = new Hasher();
            int half = mid + (int)Math.Round(l1d / ProfileStep);
            foreach ((string name, int k) in new[] { ("its half height", half), ("twice as far", mid + 2 * (half - mid)) })
            {
                double x = cx + (k - mid) * ProfileStep, g = profile[k], local = (profile[k + 1] - profile[k - 1]) / (2 * ProfileStep);
                string line = "";
                foreach (double h in RowHeights)
                {
                    double model = Along(hills, x, cz, h, p), jh = JacksonHunt(profile, k, h, l1d, p);
                    rows.Add(model); rows.Add(jh);
                    line += $" {h:F0} m ΔS {model:F3} (JH 2-D {jh:F3});";
                }
                Console.WriteLine($"  farmland_wind.lee: {name}, {(k - mid) * ProfileStep:F0} m downwind ({x:F0}, {cz:F0}), {g:F2} m, its slope {local:F3} "
                    + $"(the lee's h {hd:F2} m over its foot {(footD - mid) * ProfileStep:F0} m downwind, L1 {l1d:F0} m):{line} the plane 0");
            }
            cases.Add(("farmland_wind.lee", Hex(rows.Value), true));

            // Below the ledge: the east step at z = 500, the wind blowing out over it (toward +x).
            rows = new Hasher();
            ulong seed = SeedBlowing(new Double3(1, 0, 0), p, hills, out double off);
            using (WindField wind = MeanField(seed, p, hills), unseparated = MeanField(seed, p, HillFlow.Build(map.SampleGround, map.ContentHash,
                hills.OriginX + HillFlow.Size * HillFlow.Cell / 2, hills.OriginZ + HillFlow.Size * HillFlow.Cell / 2, null, p.Roughness, p.Displacement, p.ProfileTop)))
            {
                const double edge = 2000, along = 500;
                double lip = GroundAt(map, edge - 0.01, along), floor = GroundAt(map, edge + 0.5, along), depth = lip - floor;
                double xr = Cavity.StepReattachment * depth;
                wind.Begin(0, new Double3(edge - 50, lip + 30, along), lip);
                unseparated.Begin(0, new Double3(edge - 50, lip + 30, along), lip);
                Double3 drive = wind.At(new Double3(edge + 0.5, lip + Cavity.DriveHeightOverDepth * depth, along), lip);
                Console.WriteLine($"  farmland_wind.ledge: the east step at z = {along:F0}, its lip {lip:F3} m over the far ring's {floor:F3} m (D {depth:F3} m), "
                    + $"the wind blowing out over it (seed {seed}, {off:F1}° off east; drive {drive.X:F2} m/s at {Cavity.DriveHeightOverDepth} D over the lip); "
                    + $"X_R = {Cavity.StepReattachment} D = {xr:F1} m (Hotta et al. 1998; the literature's 7.0–7.5 D)");
                foreach (double over in new[] { 0.5, 1.0 })
                {
                    string line = "";
                    double least = 0, turn = double.NaN, previous = double.NaN;
                    foreach (double f in new[] { 0.1, 0.25, 0.5, 0.75, 0.9, 1.0, 1.25, 1.5, 2.0, 2.5 })
                    {
                        double x = edge + f * xr, g = GroundAt(map, x, along);
                        Double3 a = wind.At(new Double3(x, g + over, along), g), b = unseparated.At(new Double3(x, g + over, along), g);
                        rows.Add(a); rows.Add(b);
                        if (f < 1) least = Math.Min(least, a.X / drive.X);
                        if (double.IsNaN(turn) && previous < 0 && a.X >= 0) turn = f;
                        previous = a.X;
                        line += $" {f:F2} X_R {a.X / drive.X:F2} (unseparated {b.X / drive.X:F2});";
                    }
                    Console.WriteLine($"    {over:F1} m over the floor, u/U_drive:{line} least {least:F2} (the cell's floor return −{Cavity.CellStrength:F2}·cos(πη/D)); "
                        + (double.IsNaN(turn) ? "no reverse flow at this height" : $"the flow turns forward by {turn:F2} X_R"));
                }
            }
            cases.Add(("farmland_wind.ledge", Hex(rows.Value), true));

            // An open field: the flattest spot of the map and its surround (the least relief within 200 m, on a 20 m grid more than
            // 300 m inside the ledge).
            rows = new Hasher();
            double ox = 0, oz = 0, least2 = double.PositiveInfinity;
            for (double z = -700; z <= 1700; z += 20)
                for (double x = -700; x <= 1700; x += 20)
                {
                    double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                    for (double dz = -200; dz <= 200; dz += 25)
                        for (double dx = -200; dx <= 200; dx += 25)
                        {
                            if (dx * dx + dz * dz > 200 * 200) continue;
                            double h = GroundAt(map, x + dx, z + dz);
                            lo = Math.Min(lo, h);
                            hi = Math.Max(hi, h);
                        }
                    if (hi - lo < least2) { least2 = hi - lo; ox = x; oz = z; }
                }
            string open = "";
            var flat = new double[n];
            for (int k = 0; k < n; k++) flat[k] = GroundAt(map, ox + (k - mid) * ProfileStep, oz);
            foreach (double h in RowHeights)
            {
                double model = Along(hills, ox, oz, h, p), jh = JacksonHunt(flat, mid, h, 200, p);
                rows.Add(model); rows.Add(jh);
                open += $" {h:F0} m ΔS {model:F3} (JH 2-D at L 200 m {jh:F3});";
            }
            Console.WriteLine($"  farmland_wind.open: the flattest spot ({ox:F0}, {oz:F0}), {least2:F2} m of relief within 200 m, the wind from the west:{open} "
                + "the plane 0 (no spot on the farmland is flat: its relief and the hills upwind set it)");
            cases.Add(("farmland_wind.open", Hex(rows.Value), true));

            // A laid node on the crest, 0.3 mm over the ground, the drone 30 m over the village's track (552, 556).
            rows = new Hasher();
            using (WindField severe = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(1), hills))
            {
                double village = GroundAt(map, 552, 556);
                severe.Begin(0, new Double3(552, village + 30, 556), village);
                var node = new Double3(cx, top + 3e-4, cz);
                Double3 own = severe.At(node, top), wrong = severe.At(node);
                rows.Add(own); rows.Add(wrong);
                Console.WriteLine($"  farmland_wind.node: a node lying on the crest {top - village:F1} m above the drone's ground: its own ground {own.Length():F3} m/s "
                    + $"(the cover's still air under d + z0 = {p.Displacement + p.Roughness:F3} m); the drone's ground (model 82, known-wrong) {wrong.Length():F2} m/s");
                // The full field at the rows' points, Severe, seed 1, with its turbulence.
                var field = new Hasher();
                foreach (Double3 at in new[] { new Double3(cx, top + 5, cz), new Double3(cx + l1d, profile[half] + 5, cz), new Double3(2003, GroundAt(map, 2003, 500) + 0.5, 500),
                    new Double3(ox, GroundAt(map, ox, oz) + 5, oz) })
                    field.Add(severe.At(at, GroundAt(map, at.X, at.Z)));
                rows.Add(field.Value);
            }
            cases.Add(("farmland_wind.node", Hex(rows.Value), true));
            return cases;
        }

        private static int LogFormatModel() => Fpv450.Sim.Log.LogFormat.ModelVersion;

        // The along-wind fractional speed-up at (x, z), h over the ground, of a wind toward +x.
        private static double Along(HillFlow hills, double x, double z, double h, in WindPreset p)
        {
            hills.Sample(x, z, Math.Log((Math.Min(h, p.ProfileTop) - p.Displacement) / p.Roughness), out HillSample s);
            return s.S11;
        }

        // A hill's height over its foot (the lowest point within 1 km on one side, dir −1 upwind, +1 downwind), L1 (the distance
        // from the crest to where the profile first falls to the foot plus half that height) and the foot's index.
        private static void Hill(double[] profile, int crest, int dir, out double h, out double l1, out int foot)
        {
            foot = crest;
            for (int k = crest; Math.Abs(k - crest) * ProfileStep <= 1000; k += dir)
                if (profile[k] < profile[foot]) foot = k;
            h = profile[crest] - profile[foot];
            int at = crest;
            while (profile[at] > profile[foot] + h / 2) at += dir;
            l1 = Math.Abs(at - crest) * ProfileStep;
        }

        private static double Steepest(double[] profile, int from, int to)
        {
            double s = 0;
            for (int k = from + 1; k <= to; k++) s = Math.Max(s, Math.Abs(profile[k] - profile[k - 1]) / ProfileStep);
            return s;
        }

        // Jackson and Hunt's 2-D fractional speed-up at profile index k, h over the ground: the outer layer's potential flow
        // u'(ζ)/U = (1/π)·∫ h'(ξ)·(x − ξ)/((x − ξ)² + ζ²) dξ (midpoint quadrature of the profile's slope), times the layers'
        // (ln(h_m/z0)/ln(ζ/z0))² below h_m, held at l below it, ln(h_m/z0)/ln(ζ/z0) above; l and h_m of L = L1 (Hunt et al.).
        private static double JacksonHunt(double[] profile, int k, double h, double l1, in WindPreset p)
        {
            double z0 = p.Roughness, zeta = Math.Min(h, p.ProfileTop) - p.Displacement;
            double l = l1, hm = l1;
            for (int i = 0; i < 60; i++)
            {
                double q = Math.Log(l / z0);
                l -= (l * q - 2 * WindPreset.Karman * WindPreset.Karman * l1) / (q + 1);
                double s = Math.Sqrt(Math.Log(hm / z0));
                hm -= (hm * s - l1) / (s + 1 / (2 * s));
            }
            hm = Math.Max(hm, l);
            double at = Math.Max(zeta, l), x = k * ProfileStep, u = 0;
            for (int i = 0; i + 1 < profile.Length; i++)
            {
                double xi = (i + 0.5) * ProfileStep, slope = (profile[i + 1] - profile[i]) / ProfileStep, r = x - xi;
                u += slope * r / (r * r + at * at) * ProfileStep;
            }
            u /= Math.PI;
            double lm = Math.Log(hm / z0), lz = Math.Log(at / z0);
            return zeta > hm ? u * lm / lz : u * (lm / lz) * (lm / lz);
        }

        // The first seed whose field blows within 5° of `toward` at its first step (searched from 1), and how far off it is.
        private static ulong SeedBlowing(Double3 toward, in WindPreset p, HillFlow hills, out double off)
        {
            for (ulong seed = 1; ; seed++)
                using (WindField w = MeanField(seed, p, hills))
                {
                    w.Begin(0, default, 0);
                    Double3 m = w.Mean;
                    off = Math.Acos(Math.Max(-1, Math.Min(1, (m.X * toward.X + m.Z * toward.Z) / m.Length()))) * 180 / Math.PI;
                    if (off < 5) return seed;
                }
        }

        // A Severe field of a seed over the hills with its turbulence off (the mean alone shows).
        private static WindField MeanField(ulong seed, in WindPreset severe, HillFlow hills)
        {
            WindPreset p = severe;
            p.Intensity = 0;
            return new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed), p, hills);
        }
    }
}
