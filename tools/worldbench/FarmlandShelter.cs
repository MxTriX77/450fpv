using System;
using System.Collections.Generic;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.WorldBench
{
    // The farmland's shelter rows (w7-farmland-wind 1.2, golden/farmland-shelter.txt): the map's wake grid (WakeGrid version 2)
    // behind its hedges and trees and among the village's ruins, each row against its reference. u/u0 is the wake grid's speed
    // over the open field's at the same height (the hills scale both alike), at a cell's centre. A wind from bearing θ has the
    // wake grid's axes: up (sin θ, cos θ) toward its source and across (cos θ, −sin θ); a point's along-coordinate s = p·up grows
    // upwind, so an obstacle's leeward edge is its least s, and X·H behind it lies at s = edge − X·H.
    // - The hedge: F7's south hedge, its bushes from x 140 to 215 along z ≈ 773, the wind from the south across it; the lanes
    //   every metre along its middle half. Its density is 1 − the lanes' mean frontal porosity (WakeObstacles.Transmission over
    //   its own volumes), printed beside version 1's cell rule (the known-wrong). Reference: Brandle, Zhou and Hodges (2002, University of
    //   Nebraska–Lincoln Extension EC02-1763, read): the percent of the open wind at 5, 10, 15, 20 and 30 H behind windbreaks
    //   of 25–35, 40–60, 60–80 and 100 % density; below about 0.5 H the relative speed hardly depends on height (Zhou et al.
    //   2004, Agroforestry Systems 61, read, of Heisler and DeWalle 1988's field data), so the rows read 0.25 and 0.5 H.
    // - The trees: F6|F7's row of four trees (x 300–395 along z ≈ 758, H about 12 m), the wind from the south, F7's hedge 14 m
    //   behind its east pair. The same rows behind each pair, and the known-wrong: the nearest wake alone (version 1: the
    //   hedge hid the trees' wake).
    // - The village: each ruin's frontal porosity across its middle third, the wind from the north and from the west, against
    //   version 1's cell rule; behind Ruin_RoofCollapsed and Ruin_Walls from the north, across their middle third, the cavity
    //   against Fackrell (1984)'s reattachment (recalled) and the lee against the solid fence's row; among the ruins,
    //   Ruin_RoofCollapsed's wake over Ruin_Stump.
    // The golden hashes every printed number's bits and the two grids.
    internal static partial class Program
    {
        // Brandle, Zhou and Hodges (2002), Figure 2: percent of the open wind speed at 5, 10, 15, 20 and 30 H.
        private static readonly double[] BrandleX = { 5, 10, 15, 20, 30 };

        private static readonly (string Name, double Lo, double Hi, double[] Percent)[] BrandleClasses =
        {
            ("25–35 % dense (deciduous)", 0.25, 0.35, new double[] { 50, 65, 80, 85, 100 }),
            ("40–60 % (conifer)", 0.40, 0.60, new double[] { 30, 50, 60, 75, 95 }),
            ("60–80 % (multi-row)", 0.60, 0.80, new double[] { 25, 35, 65, 85, 95 }),
            ("100 % (solid fence)", 1.00, 1.00, new double[] { 25, 70, 90, 95, 100 }),
        };

        /// <summary>A wind's axes in the wake grid's convention (its bearing, degrees from the north).</summary>
        private readonly struct Axes
        {
            public readonly double UX, UZ, AX, AZ;

            public Axes(double fromDeg)
            {
                DetMath.SinCos(fromDeg * Math.PI / 180, out double sin, out double cos);
                UX = sin;
                UZ = cos;
                AX = cos;
                AZ = -sin;
            }

            public void World(double across, double s, out double x, out double z)
            {
                x = across * AX + s * UX;
                z = across * AZ + s * UZ;
            }
        }

        /// <summary>A selection of wind volumes seen along a wind: its extent across (C0..C1) and along (S0, the leeward edge, to
        /// S1), and H, the mean top of its lanes' obstacle runs (Lanes sets it: the height the wake grid scales its wake with).</summary>
        private sealed class Body
        {
            public readonly List<WindVolume> Parts = new List<WindVolume>();
            public double C0 = double.PositiveInfinity, C1 = double.NegativeInfinity, S0 = double.PositiveInfinity, S1 = double.NegativeInfinity, H;
        }

        private static List<(string Name, string Value, bool Agree)> FarmlandShelterCases()
        {
            var cases = new List<(string Name, string Value, bool Agree)>();
            MapWorld map = Farmland();
            MapWind wind = MapWind.Of(map);
            WakeObstacles o = wind.Wakes.Obstacles();
            cases.Add(("farmland_shelter.version", WakeGrid.Version + "." + LogFormatModel(), true));
            var clock = Stopwatch.StartNew();
            WakeGrid south = WakeGrid.Build(o, 180), north = WakeGrid.Build(o, 0);
            Console.WriteLine($"  farmland_shelter.grids: {o.NX} × {o.NZ} cells, {map.WindVolumes.Length} wind volumes; two directions in "
                + $"{clock.ElapsedMilliseconds} ms; from the south {south.WakeCells} cells in a wake, {south.OverlyingCells} with an overlying one");
            var grids = new Hasher();
            grids.Add(south.Hash());
            grids.Add(north.Hash());
            cases.Add(("farmland_shelter.grids", Hex(grids.Value), true));
            var fromSouth = new Axes(180);
            var fromNorth = new Axes(0);
            var fromWest = new Axes(270);

            // ---- The hedge: F7's south hedge, the wind from the south ----
            var rows = new Hasher();
            Body hedge = Select(map, k => map.ObjectName(map.WindVolumeObject(k)).Contains("Bush") && Inside(map.WindVolumes[k], 140, 215, 768, 779), fromSouth);
            double c0 = hedge.C0 + (hedge.C1 - hedge.C0) / 4, c1 = hedge.C1 - (hedge.C1 - hedge.C0) / 4;
            Lanes(o, hedge, c0, c1, fromSouth, out double hBeta, out double hOld);
            rows.Add(hBeta);
            rows.Add(hOld);
            Console.WriteLine($"  farmland_shelter.hedge: F7's south hedge, {hedge.Parts.Count} bushes {hedge.C1 - hedge.C0:F0} m long, H {hedge.H:F2} m (its lanes' "
                + $"mean top), {hedge.S1 - hedge.S0:F1} m deep; the wind from the south. Across its middle half: frontal porosity {hBeta:F3}, "
                + $"{100 * (1 - hBeta):F0} % dense (version 1's cell rule, known-wrong: {hOld:F3}); Brandle et al.'s class {Classes(1 - hBeta)}");
            Lee(o, south, hedge, c0, c1, fromSouth, 1 - hBeta, rows, false);
            cases.Add(("farmland_shelter.hedge", Hex(rows.Value), true));

            // ---- The trees: F6|F7's row, the wind from the south, the hedge behind its east pair ----
            rows = new Hasher();
            Body trees = Select(map, k => map.ObjectName(map.WindVolumeObject(k)).Contains("Tree") && Inside(map.WindVolumes[k], 295, 400, 750, 766), fromSouth);
            Console.WriteLine($"  farmland_shelter.trees: F6|F7's tree row, {trees.Parts.Count} volumes over {trees.C1 - trees.C0:F0} m; the wind from the south "
                + "(F7's hedge 14 m behind the east pair)");
            foreach ((string name, double a, double b) in new[] { ("the west pair", 304.0, 322.0), ("the east pair, the hedge behind it", 376.0, 391.0) })
            {
                // Across is −x for a wind from the south.
                Lanes(o, trees, -b, -a, fromSouth, out double beta, out double old);
                rows.Add(beta);
                rows.Add(old);
                Console.WriteLine($"    {name}, lanes x {a:F0}–{b:F0}, H {trees.H:F2} m: frontal porosity {beta:F3} ({100 * (1 - beta):F0} % dense; version 1's cell rule {old:F3}); "
                    + $"Brandle et al.'s class {Classes(1 - beta)} (for a belt at least 10 H long; this pair's crowns span {(b - a + 11) / trees.H:F1} H)");
                Lee(o, south, trees, -b, -a, fromSouth, 1 - beta, rows, true);
            }
            cases.Add(("farmland_shelter.trees", Hex(rows.Value), true));

            // ---- The village ----
            rows = new Hasher();
            Console.WriteLine("  farmland_shelter.ruins: each ruin's frontal porosity across its middle third, model (version 1's cell rule, known-wrong); "
                + $"recirculation full at β ≤ {WakeGrid.BluffLow} and gone at {WakeGrid.BluffHigh} (Perera 1981, recalled)");
            int walls = -1, roof = -1;
            for (int obj = 0; obj < map.PlacedObjects; obj++)
            {
                string name = map.ObjectName(obj);
                if (!name.StartsWith("Ruin_", StringComparison.Ordinal)) continue;
                if (name == "Ruin_RoofCollapsed") roof = obj;
                string line = $"    {name} #{obj}:";
                foreach ((string from, Axes axes) in new[] { ("north", fromNorth), ("west", fromWest) })
                {
                    Body ruin = Select(map, k => map.WindVolumeObject(k) == obj, axes);
                    double w = ruin.C1 - ruin.C0;
                    Lanes(o, ruin, ruin.C0 + w / 3, ruin.C1 - w / 3, axes, out double beta, out double old);
                    rows.Add(beta);
                    rows.Add(old);
                    line += $" from the {from} {beta:F3} ({old:F3}), W {w:F1} m, L {ruin.S1 - ruin.S0:F1} m, H {ruin.H:F2} m;";
                    if (name == "Ruin_Walls" && from == "north" && Math.Abs((ruin.C0 + ruin.C1) / 2 - 500) < 5) walls = obj;
                }
                Console.WriteLine(line);
            }

            // Behind two ruins from the north: u/u0 at 0.25 H across the middle third against Fackrell's reattachment, and the lee
            // at 0.5 H.
            foreach ((string name, int obj) in new[] { ("Ruin_RoofCollapsed", roof), ("Ruin_Walls", walls) })
            {
                Body body = Select(map, k => map.WindVolumeObject(k) == obj, fromNorth);
                double w = body.C1 - body.C0;
                Lanes(o, body, body.C0 + w / 3, body.C1 - w / 3, fromNorth, out double beta, out _);
                double xr = WakeGrid.Reattachment(body.H, w, body.S1 - body.S0);
                string cavity = "";
                double lastReverse = 0;
                foreach (double X in new[] { 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0, 4.0 })
                {
                    double u = Across(north, o, body, fromNorth, X, 0.25);
                    rows.Add(u);
                    if (u < 0) lastReverse = X;
                    cavity += $" {X:F2} H {u:F2};";
                }
                double at5 = Across(north, o, body, fromNorth, 5, 0.5), at10 = Across(north, o, body, fromNorth, 10, 0.5);
                rows.Add(at5);
                rows.Add(at10);
                Console.WriteLine($"  farmland_shelter.cavity: behind {name} #{obj} from the north, W {w:F1} m, L {body.S1 - body.S0:F1} m, H {body.H:F2} m, "
                    + $"frontal porosity {beta:F3}: Fackrell's reattachment {xr:F2} H (recalled); u/u0 at 0.25 H across the middle third:{cavity} reverse "
                    + $"flow to {lastReverse:F2} H; at 0.5 H, 5 H {at5:F2} and 10 H {at10:F2} (a long solid fence's 0.25 and 0.70, Brandle et al.; this "
                    + $"ruin is {w / body.H:F1} H wide)");
            }

            // Among the ruins: Ruin_RoofCollapsed's wake over Ruin_Stump, the wind from the north, on x = 537.
            string among = "";
            foreach (double z in new[] { 606.0, 600, 594, 590, 582, 576, 570, 562 })
            {
                among += $" z {z:F0}:";
                foreach (double h in new[] { 1.0, 2.0, 4.0 })
                {
                    SampleAt(north, o, 537, z, h, out WakeSample s, out WakeSample near);
                    rows.Add(s.Speed);
                    rows.Add(near.Speed);
                    among += $" {h:F0} m {s.Speed:F2} ({near.Speed:F2})";
                }
                Cell(o, 537, z, out int i, out int j);
                north.Describe(i, j, out double nx, out double nh, out _, out _, out _, out _, out _);
                north.DescribeOver(i, j, out double ox, out double oh, out _, out _);
                among += oh > 0 ? $" [{nh:F1} m at {nx:F1} H, over it {oh:F1} m at {ox:F1} H];" : $" [{nh:F1} m at {nx:F1} H];";
            }
            Console.WriteLine("  farmland_shelter.among: Ruin_RoofCollapsed (539, 617) and Ruin_Stump (536, 586) south of it, the wind from the north, on "
                + $"x 537, u/u0 (the nearest wake alone, known-wrong):{among}");
            cases.Add(("farmland_shelter.village", Hex(rows.Value), true));
            return cases;
        }

        private static bool Inside(in WindVolume w, double x0, double x1, double z0, double z1) => w.X >= x0 && w.X <= x1 && w.Z >= z0 && w.Z <= z1;

        private static Body Select(MapWorld map, Func<int, bool> pick, in Axes axes)
        {
            var body = new Body();
            ReadOnlySpan<WindVolume> all = map.WindVolumes;
            for (int k = 0; k < all.Length; k++)
            {
                if (!pick(k)) continue;
                WindVolume w = all[k];
                body.Parts.Add(w);
                Reach(w, axes.AX, axes.AZ, out double a0, out double a1);
                Reach(w, axes.UX, axes.UZ, out double s0, out double s1);
                body.C0 = Math.Min(body.C0, a0);
                body.C1 = Math.Max(body.C1, a1);
                body.S0 = Math.Min(body.S0, s0);
                body.S1 = Math.Max(body.S1, s1);
            }
            return body;
        }

        // A volume's extent along a unit axis (a, b) in plan.
        private static void Reach(in WindVolume w, double a, double b, out double lo, out double hi)
        {
            double c = w.X * a + w.Z * b, r;
            if (w.Kind == WindVolumeKind.Cylinder) r = w.Radius;
            else
            {
                double sin = Math.Sin(w.Yaw), cos = Math.Cos(w.Yaw); // its local x is (cos, −sin), its z (sin, cos)
                r = Math.Abs(w.HalfX * (cos * a - sin * b)) + Math.Abs(w.HalfZ * (sin * a + cos * b));
            }
            lo = c - r;
            hi = c + r;
        }

        private static void Cell(WakeObstacles o, double x, double z, out int i, out int j)
        {
            i = (int)Math.Floor((x - o.OriginX) / WakeObstacles.Cell);
            j = (int)Math.Floor((z - o.OriginZ) / WakeObstacles.Cell);
        }

        // The lanes across c0..c1 (every metre) through a body alone (its own volumes on the map's grid, so a neighbour touching it
        // stays out): each lane's frontal porosity as the wake grid reads an obstacle (from the body's first cell on the lane upwind
        // while cells are obstacles: their highest top H and lowest base, then WakeObstacles.Transmission; a lane that meets none is
        // open), the mean H of the lanes that meet it, and version 1's cell rule over the same volumes (the known-wrong). Sets the
        // body's H to that mean, the height its wake scales with.
        private static void Lanes(WakeObstacles grid, Body body, double c0, double c1, in Axes axes, out double beta, out double old)
        {
            var o = new WakeObstacles(grid.OriginX, grid.OriginZ, grid.NX, grid.NZ, body.Parts.ToArray());
            WakeObstacles.Lane lane = o.NewLane();
            double sum = 0, sumOld = 0, sumH = 0;
            int n = 0, hit = 0;
            for (double c = c0; c <= c1 + 1e-9; c += 1)
            {
                double s = body.S0 - 0.5, x, z;
                int k;
                do
                {
                    s += 1;
                    axes.World(c, s, out x, out z);
                    k = o.CellAt(x, z);
                } while (!o.IsObstacle(k) && s < body.S1);
                double top = 0, bottom = double.PositiveInfinity;
                for (int q = k, t = 1; q >= 0 && o.IsObstacle(q); t++)
                {
                    top = Math.Max(top, o.Top(q));
                    bottom = Math.Min(bottom, o.Base(q));
                    q = o.CellAt(x + t * axes.UX, z + t * axes.UZ);
                }
                sum += top > 0 ? o.Transmission(lane, x, z, axes.UX, axes.UZ, axes.AX, axes.AZ, double.NegativeInfinity, bottom, top) : 1;
                if (top > 0) { sumH += top; hit++; }
                sumOld += OldPath(body, c, axes);
                n++;
            }
            beta = sum / n;
            old = sumOld / n;
            body.H = sumH / hit;
        }

        // Version 1's rule (WakeObstacles before w7 1.2, the known-wrong): each 2 m cell's β = Π over the volumes covering a
        // fraction f of its 4 × 4 samples of 1 − f·(1 − β^(2 m / D)), and a lane's β = Π √β_cell over its 1 m steps across the
        // body's depth (the farmland's grid starts at the origin).
        private static double OldPath(Body body, double c, in Axes axes)
        {
            double beta = 1;
            for (double s = body.S0 + 0.5; s < body.S1; s += 1)
            {
                axes.World(c, s, out double x, out double z);
                double cx = Math.Floor(x / 2) * 2, cz = Math.Floor(z / 2) * 2, cell = 1;
                foreach (WindVolume w in body.Parts)
                {
                    int inside = 0;
                    double sin = Math.Sin(w.Yaw), cos = Math.Cos(w.Yaw);
                    for (int b = 0; b < 4; b++)
                        for (int a = 0; a < 4; a++)
                        {
                            double qx = cx + (a + 0.5) / 2 - w.X, qz = cz + (b + 0.5) / 2 - w.Z;
                            if (w.Kind == WindVolumeKind.Cylinder ? qx * qx + qz * qz <= w.Radius * w.Radius
                                : Math.Abs(qx * cos - qz * sin) <= w.HalfX && Math.Abs(qx * sin + qz * cos) <= w.HalfZ)
                                inside++;
                        }
                    if (inside == 0) continue;
                    double d = (w.Kind == WindVolumeKind.Cylinder ? 2 * Math.PI * w.Radius : 4 * (w.HalfX + w.HalfZ)) / Math.PI;
                    cell *= 1 - inside / 16.0 * (1 - Math.Pow(w.Porosity, 2 / d));
                }
                beta *= Math.Sqrt(cell);
            }
            return beta;
        }

        // The wake at a world point's cell centre, with and without the overlying wake.
        private static void SampleAt(WakeGrid g, WakeObstacles o, double x, double z, double h, out WakeSample s, out WakeSample nearest)
        {
            Cell(o, x, z, out int i, out int j);
            g.SampleCell(i, j, h, out s);
            g.SampleNearest(i, j, h, out nearest);
        }

        // The mean u/u0 across a body's middle third, X·H behind it, at Z·H.
        private static double Across(WakeGrid g, WakeObstacles o, Body body, in Axes axes, double X, double Z)
        {
            double w = body.C1 - body.C0, sum = 0;
            int n = 0;
            for (double c = body.C0 + w / 3; c <= body.C1 - w / 3 + 1e-9; c += 1)
            {
                axes.World(c, body.S0 - X * body.H, out double x, out double z);
                SampleAt(g, o, x, z, Z * body.H, out WakeSample s, out _);
                sum += s.Speed;
                n++;
            }
            return sum / n;
        }

        // The rows behind a body across c0..c1: u/u0 at 2, 5, 10, 15, 20 and 30 H and 0.25, 0.5 and 1 H, the lanes' mean, least
        // and most (and the nearest wake alone's mean when asked), against Brandle et al.'s classes bracketing its density.
        private static void Lee(WakeObstacles o, WakeGrid g, Body body, double c0, double c1, in Axes axes, double density, Hasher rows, bool nearest)
        {
            int lower = 0, upper = BrandleClasses.Length - 1;
            for (int k = 0; k < BrandleClasses.Length; k++)
            {
                if (BrandleClasses[k].Lo <= density) lower = k;
                if (BrandleClasses[BrandleClasses.Length - 1 - k].Hi >= density) upper = BrandleClasses.Length - 1 - k;
            }
            foreach (double Z in new[] { 0.25, 0.5, 1.0 })
            {
                string line = $"    {Z:F2} H ({Z * body.H:F1} m), u/u0 mean [least–most]{(nearest ? " (the nearest wake alone)" : "")}:";
                foreach (double X in new[] { 2.0, 5, 10, 15, 20, 30 })
                {
                    double sum = 0, sumNear = 0, least = double.PositiveInfinity, most = double.NegativeInfinity;
                    int n = 0;
                    for (double c = c0; c <= c1 + 1e-9; c += 1)
                    {
                        axes.World(c, body.S0 - X * body.H, out double x, out double z);
                        SampleAt(g, o, x, z, Z * body.H, out WakeSample s, out WakeSample near);
                        sum += s.Speed;
                        sumNear += near.Speed;
                        least = Math.Min(least, s.Speed);
                        most = Math.Max(most, s.Speed);
                        n++;
                    }
                    double mean = sum / n;
                    rows.Add(mean);
                    rows.Add(least);
                    rows.Add(most);
                    line += $" {X:F0} H {mean:F2} [{least:F2}–{most:F2}]";
                    if (nearest)
                    {
                        rows.Add(sumNear / n);
                        line += $" ({sumNear / n:F2})";
                    }
                    int at = Array.IndexOf(BrandleX, X);
                    if (at >= 0 && Z <= 0.5)
                    {
                        double a = BrandleClasses[lower].Percent[at] / 100, b = BrandleClasses[upper].Percent[at] / 100;
                        line += $" vs {Math.Min(a, b):F2}–{Math.Max(a, b):F2}";
                    }
                    line += ";";
                }
                Console.WriteLine(line);
            }
        }

        // The Brandle et al. classes a density falls in or between.
        private static string Classes(double density)
        {
            string below = null, above = null;
            foreach (var c in BrandleClasses)
            {
                if (density >= c.Lo && density <= c.Hi) return c.Name;
                if (c.Hi < density) below = c.Name;
                else if (above == null) above = c.Name;
            }
            return below == null ? $"under {above}" : $"between {below} and {above ?? "none"}";
        }
    }
}
