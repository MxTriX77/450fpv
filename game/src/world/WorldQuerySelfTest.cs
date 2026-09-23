using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

/// `-- --selftest worldquery`: every world-query scenario of SampleGround (task 3.1) on game/maps/sample_patch, plus
/// uniform in-memory worlds for the per-surface checks. Prints one PASS/FAIL line per scenario with its numbers and exits
/// non-zero on any failure.
public static class WorldQuerySelfTest
{
    const string Package = "res://maps/sample_patch";
    const string SurfacesPath = "res://maps/surfaces.json";

    public static async void Run(Node3D sandbox)
    {
        SceneTree tree = sandbox.GetTree();
        bool pass = false;
        try
        {
            string dir = ProjectSettings.GlobalizePath(Package), surfaces = ProjectSettings.GlobalizePath(SurfacesPath);
            WorldQuery world = WorldQuery.Load(dir, surfaces);

            // The renderer and its Jolt collision, built from the same package.
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "map.json")));
            JsonElement height = manifest.RootElement.GetProperty("height");
            byte[] r16 = File.ReadAllBytes(Path.Combine(dir, "height.r16"));
            float offset = height.GetProperty("offset_m").GetSingle(), scale = height.GetProperty("scale_m").GetSingle();
            var terrain = new HeightmapTerrain { Name = "Terrain" };
            sandbox.AddChild(terrain);
            terrain.Build(r16, world.Samples, (float)world.HeightResolution, offset, scale);
            for (int i = 0; i < 3; i++)
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            PhysicsDirectSpaceState3D space = sandbox.GetWorld3D().DirectSpaceState;

            pass = MatchesRendered(world, r16, offset, scale, space);
            pass &= Continuity(world);
            pass &= Normals(world);
            pass &= ReliefRms(world, surfaces);
            pass &= Pitfalls(world, dir, surfaces);
            pass &= OutsideMap(world);
            Timings(world);
        }
        catch (Exception e)
        {
            GD.PrintErr($"ERROR: selftest worldquery: {e}");
            pass = false;
        }
        GD.Print($"selftest worldquery: {(pass ? "ALL PASS" : "FAILED")}");
        tree.Quit(pass ? 0 : 1);
    }

    static bool Check(string scenario, bool ok, string numbers)
    {
        GD.Print($"selftest worldquery: {scenario}: {numbers} {(ok ? "PASS" : "FAIL")}");
        return ok;
    }

    static GroundSample[] SampleAll(WorldQuery world, XZ[] points)
    {
        var results = new GroundSample[points.Length];
        world.SampleGround(points, results);
        return results;
    }

    // ---------------------------------------------------------------- 3.1 SampleGround

    /// 10,000 points: random, on cell diagonals, on cell edges, and on the collision-chunk seams, which for a 256 m map
    /// are its edges and corners. TerrainHeight vs an independent barycentric evaluation of the renderer's triangle, and
    /// vs a downward Jolt ray on the renderer's HeightMapShape3D collision.
    static bool MatchesRendered(WorldQuery world, byte[] r16, float offset, float scale, PhysicsDirectSpaceState3D space)
    {
        var random = new Random(31);
        double half = world.Half, res = world.HeightResolution;
        int cellsPerSide = world.Samples - 1;
        var points = new XZ[10000];
        for (int i = 0; i < points.Length; i++)
        {
            double x, z;
            int ci = random.Next(cellsPerSide), cj = random.Next(cellsPerSide);
            double t = random.NextDouble();
            switch (i % 4)
            {
                case 0: x = -half + random.NextDouble() * world.SizeM; z = -half + random.NextDouble() * world.SizeM; break;
                case 1: x = -half + (ci + t) * res; z = -half + (cj + 1 - t) * res; break; // the split diagonal
                case 2: x = -half + (ci + (i % 8 == 2 ? 0 : t)) * res; z = -half + (cj + (i % 8 == 2 ? t : 0)) * res; break;
                default: // on or just inside an edge; every 10th along-edge coordinate is a corner
                    double side = i / 4 % 2 == 0 ? -half : half;
                    double inset = (i / 8 % 3) switch { 0 => 0, 1 => 1e-3, _ => random.NextDouble() * 0.01 };
                    double edge = side - Math.Sign(side) * inset;
                    double along = i / 24 % 10 == 0 ? side : -half + t * world.SizeM;
                    (x, z) = i / 48 % 2 == 0 ? (edge, along) : (along, edge);
                    break;
            }
            points[i] = new XZ((float)x, (float)z); // Jolt takes float coordinates
        }
        GroundSample[] samples = SampleAll(world, points);

        double worstTriangle = 0, worstRay = 0;
        int misses = 0;
        for (int i = 0; i < points.Length; i++)
        {
            double expected = RendererTriangle(r16, offset, scale, world.Samples, res, half, points[i].X, points[i].Z);
            worstTriangle = Math.Max(worstTriangle, Math.Abs(samples[i].TerrainHeight - expected));
            var ray = PhysicsRayQueryParameters3D.Create(new Vector3((float)points[i].X, 500f, (float)points[i].Z),
                new Vector3((float)points[i].X, -500f, (float)points[i].Z));
            Godot.Collections.Dictionary hit = space.IntersectRay(ray);
            if (hit.Count == 0)
                misses++;
            else
                worstRay = Math.Max(worstRay, Math.Abs(samples[i].TerrainHeight - ((Vector3)hit["position"]).Y));
        }
        return Check("matches the rendered surface", worstTriangle <= 1e-3 && worstRay <= 1e-3 && misses == 0,
            $"10000 points (2500 random, 2500 on diagonals, 2500 on cell edges, 2500 on the map/chunk edges): "
            + $"worst vs renderer triangle {worstTriangle * 1000:0.000000} mm, worst vs Jolt ray {worstRay * 1000:0.0000} mm, "
            + $"{misses} ray misses (limit 1 mm)");
    }

    /// The renderer's triangle, evaluated independently: its three vertices as HeightmapTerrain builds them (cell corners
    /// a, b, c, d; triangles a-b-c and b-d-c) and barycentric weights.
    static double RendererTriangle(byte[] r16, float offset, float scale, int samples, double res, double half, double x, double z)
    {
        int ci = Math.Clamp((int)Math.Floor((x + half) / res), 0, samples - 2), cj = Math.Clamp((int)Math.Floor((z + half) / res), 0, samples - 2);
        (double X, double Y, double Z) V(int i, int j)
        {
            int k = j * samples + i;
            return (-half + i * res, offset + (r16[2 * k] | r16[2 * k + 1] << 8) * scale, -half + j * res);
        }
        var a = V(ci, cj);
        var b = V(ci + 1, cj);
        var c = V(ci, cj + 1);
        var d = V(ci + 1, cj + 1);
        foreach (var (p, q, r) in new[] { (a, b, c), (b, d, c) })
        {
            double den = (q.Z - r.Z) * (p.X - r.X) + (r.X - q.X) * (p.Z - r.Z);
            double l1 = ((q.Z - r.Z) * (x - r.X) + (r.X - q.X) * (z - r.Z)) / den;
            double l2 = ((r.Z - p.Z) * (x - r.X) + (p.X - r.X) * (z - r.Z)) / den;
            double l3 = 1 - l1 - l2;
            if (l1 >= -1e-9 && l2 >= -1e-9 && l3 >= -1e-9)
                return l1 * p.Y + l2 * q.Y + l3 * r.Y;
        }
        throw new InvalidOperationException($"no renderer triangle holds ({x}, {z})");
    }

    /// Every border between surface cells of different surfaces, walked in 1 mm steps along the border and across it
    /// (±0.5 m, the whole blend band). Reports the largest step of GroundHeight and SupportTop, and the largest jump:
    /// the step minus what the local slope predicts, which is what a discontinuity would show.
    static bool Continuity(WorldQuery world)
    {
        byte[] surface = SurfaceLayer(world);
        int cells = world.Cells;
        double res = world.CellResolution, half = world.Half;
        var walks = new List<(double x, double z, double dx, double dz, int steps)>();
        for (int r = 0; r < cells; r++)
        {
            for (int c = 0; c < cells; c++)
            {
                byte s = surface[r * cells + c];
                double x0 = -half + c * res, z0 = -half + r * res;
                if (c + 1 < cells && surface[r * cells + c + 1] != s) // border line x = x0 + res, z from z0 to z0 + res
                {
                    walks.Add((x0 + res, z0, 0, 1e-3, 500));
                    walks.Add((x0 + res - 0.5, z0 + res / 2, 1e-3, 0, 1000));
                }
                if (r + 1 < cells && surface[(r + 1) * cells + c] != s)
                {
                    walks.Add((x0, z0 + res, 1e-3, 0, 500));
                    walks.Add((x0 + res / 2, z0 + res - 0.5, 0, 1e-3, 1000));
                }
            }
        }
        double worstStep = 0, worstOutsidePits = 0, worstGentle = 0, worstSupport = 0, worstJump = 0, steepest = 0;
        long steepSteps = 0;
        string where = "", whereOutside = "";
        var points = new XZ[1001];
        var results = new GroundSample[1001];
        long count = 0;
        foreach (var (x, z, dx, dz, steps) in walks)
        {
            for (int i = 0; i <= steps; i++)
                points[i] = new XZ(x + i * dx, z + i * dz);
            world.SampleGround(points.AsSpan(0, steps + 1), results.AsSpan(0, steps + 1));
            count += steps + 1;
            for (int i = 1; i <= steps; i++)
            {
                double step = Math.Abs(results[i].GroundHeight - results[i - 1].GroundHeight);
                double slope = (Slope(results[i - 1], dx, dz) + Slope(results[i], dx, dz)) / 2;
                double jump = Math.Abs(results[i].GroundHeight - results[i - 1].GroundHeight - slope * 1e-3);
                worstSupport = Math.Max(worstSupport, Math.Abs(results[i].SupportTop - results[i - 1].SupportTop));
                steepest = Math.Max(steepest, Math.Abs(slope));
                if (Math.Max(Math.Abs(Slope(results[i - 1], dx, dz)), Math.Abs(Slope(results[i], dx, dz))) <= 1)
                    worstGentle = Math.Max(worstGentle, step);
                else
                    steepSteps++;
                if (results[i].Feature != GroundFeature.Pitfall && results[i - 1].Feature != GroundFeature.Pitfall && step > worstOutsidePits)
                {
                    worstOutsidePits = step;
                    whereOutside = $"x={points[i].X:0.000}, z={points[i].Z:0.000}, surfaces {results[i - 1].Surface}->{results[i].Surface}, slope {slope:0.00}";
                }
                if (step > worstStep)
                {
                    worstStep = step;
                    where = $"x={points[i].X:0.000}, z={points[i].Z:0.000}, surfaces {results[i - 1].Surface}->{results[i].Surface}, "
                        + $"{(results[i].Feature == GroundFeature.Pitfall ? "in a pitfall" : "no pitfall")}, slope {slope:0.00}";
                }
                worstJump = Math.Max(worstJump, jump);
            }
        }
        // Where the ground is steeper than 1 m/m (pitfall walls, rubble relief) a continuous surface changes by more than
        // 1 mm per 1 mm step. There continuity is judged by the jump beyond the local slope; elsewhere the literal limit holds.
        return Check("continuous across surface borders", worstGentle <= 1e-3 && worstJump <= 1e-3,
            $"{walks.Count / 2} border edges, {count} samples at 1 mm: largest step with the slope ≤ 1 at both ends {worstGentle * 1000:0.000} mm; "
            + $"largest jump beyond the local slope {worstJump * 1000:0.000000} mm; {steepSteps} steps steeper than 1 m/m, "
            + $"largest step anywhere {worstStep * 1000:0.000} mm (at {where}); largest outside pitfalls "
            + $"{worstOutsidePits * 1000:0.000} mm (at {whereOutside}); largest SupportTop step {worstSupport * 1000:0.000} mm "
            + "(limit 1 mm)");
    }

    /// dGroundHeight per metre along (dx, dz), from the analytic normal.
    static double Slope(GroundSample g, double dx, double dz)
    {
        double length = Math.Sqrt(dx * dx + dz * dz);
        return -(g.Normal.X * dx + g.Normal.Z * dz) / (g.Normal.Y * length);
    }

    static byte[] SurfaceLayer(WorldQuery world)
    {
        int cells = world.Cells;
        var points = new XZ[cells * cells];
        for (int r = 0; r < cells; r++)
        {
            for (int c = 0; c < cells; c++)
                points[r * cells + c] = new XZ(-world.Half + (c + 0.5) * world.CellResolution, -world.Half + (r + 0.5) * world.CellResolution);
        }
        return SampleAll(world, points).Select(g => g.Surface).ToArray();
    }

    /// The spec check: 10,000 uniform points, analytic normal vs a 1 mm central difference of GroundHeight. Then 1,000
    /// points inside pitfalls, where a wall a few cm wide curves too sharply for a 1 mm difference to be a 1e-3 rad
    /// reference: there the reference is a 1 µm difference, and the 1 mm figure is printed for information.
    static bool Normals(WorldQuery world)
    {
        var random = new Random(47);
        var uniform = new List<XZ>();
        while (uniform.Count < 10000)
            uniform.Add(new XZ(-world.Half + random.NextDouble() * world.SizeM, -world.Half + random.NextDouble() * world.SizeM));
        var inPits = new List<XZ>();
        var probe = new XZ[1];
        var probeResult = new GroundSample[1];
        while (inPits.Count < 1000)
        {
            probe[0] = new XZ(-world.Half + random.NextDouble() * world.SizeM, -world.Half + random.NextDouble() * world.SizeM);
            world.SampleGround(probe, probeResult);
            if (probeResult[0].Feature == GroundFeature.Pitfall)
                inPits.Add(probe[0]);
        }
        bool pass = NormalsAt(world, uniform, 1e-3, "normal accuracy, 10000 uniform points, 1 mm difference", true);
        NormalsAt(world, inPits, 1e-3, "normal inside pitfalls, 1000 points, 1 mm difference (information)", false);
        pass &= NormalsAt(world, inPits, 1e-6, "normal inside pitfalls, 1000 points, 1 µm difference", true);
        return pass;
    }

    /// Points whose ±h stencil crosses a crease are skipped: a facet edge of the rendered triangles, a line of cell centres
    /// where the blended surfaces differ, or a pitfall rim.
    static bool NormalsAt(WorldQuery world, List<XZ> points, double h, string scenario, bool judged)
    {
        var stencil = new XZ[5];
        var s = new GroundSample[5];
        double worst = 0, worstInPit = 0;
        int skipped = 0, checkedInPit = 0;
        string where = "";
        foreach (XZ p in points)
        {
            stencil[0] = p;
            stencil[1] = new XZ(p.X + h, p.Z);
            stencil[2] = new XZ(p.X - h, p.Z);
            stencil[3] = new XZ(p.X, p.Z + h);
            stencil[4] = new XZ(p.X, p.Z - h);
            world.SampleGround(stencil, s);
            if (Crease(world, stencil, s))
            {
                skipped++;
                continue;
            }
            double gx = (s[1].GroundHeight - s[2].GroundHeight) / (2 * h), gz = (s[3].GroundHeight - s[4].GroundHeight) / (2 * h);
            double n = Math.Sqrt(gx * gx + 1 + gz * gz);
            double ax = -gx / n, ay = 1 / n, az = -gz / n;
            double bx = s[0].Normal.X, by = s[0].Normal.Y, bz = s[0].Normal.Z;
            double cross = Math.Sqrt(Math.Pow(ay * bz - az * by, 2) + Math.Pow(az * bx - ax * bz, 2) + Math.Pow(ax * by - ay * bx, 2));
            double angle = Math.Atan2(cross, ax * bx + ay * by + az * bz);
            if (s[0].Feature == GroundFeature.Pitfall)
            {
                checkedInPit++;
                worstInPit = Math.Max(worstInPit, angle);
            }
            if (angle > worst)
            {
                worst = angle;
                where = $"x={p.X:0.0000}, z={p.Z:0.0000}, surface {s[0].Surface}, "
                    + $"{(s[0].Feature == GroundFeature.Pitfall ? $"pitfall 0x{s[0].FeatureId:x8} depth {s[0].FeatureDepth:0.000} m" : "no pitfall")}, "
                    + $"slope {Math.Sqrt(gx * gx + gz * gz):0.00}";
            }
        }
        string numbers = $"{points.Count - skipped} of {points.Count} points checked ({skipped} skipped at creases), "
            + $"{checkedInPit} inside pitfalls: worst {worst:0.000000} rad at {where}; worst inside pitfalls {worstInPit:0.000000} rad "
            + "(limit 1e-3 rad)";
        if (!judged)
        {
            GD.Print($"selftest worldquery: {scenario}: {numbers}");
            return true;
        }
        return Check(scenario, worst <= 1e-3, numbers);
    }

    static bool Crease(WorldQuery world, XZ[] stencil, GroundSample[] s)
    {
        (int, int, bool) Facet(XZ p)
        {
            double fx = (p.X + world.Half) / world.HeightResolution, fz = (p.Z + world.Half) / world.HeightResolution;
            int ci = (int)Math.Floor(fx), cj = (int)Math.Floor(fz);
            return (ci, cj, fx - ci + fz - cj > 1);
        }
        (int, int) BlendCell(XZ p) => ((int)Math.Floor((p.X + world.Half) / world.CellResolution - 0.5),
            (int)Math.Floor((p.Z + world.Half) / world.CellResolution - 0.5));
        bool mixed = false;
        for (int q = 1; q < 4; q++)
            mixed |= s[0].BlendSurface[q] != s[0].BlendSurface[0];
        for (int i = 1; i < 5; i++)
        {
            if (Facet(stencil[i]) != Facet(stencil[0]) || s[i].FeatureId != s[0].FeatureId
                || mixed && BlendCell(stencil[i]) != BlendCell(stencil[0]))
                return true;
        }
        return false;
    }

    /// Every surface of the table on its own uniform, flat in-memory world, plus the meadow of sample_patch itself:
    /// GroundHeight − TerrainHeight on a 5 cm grid over 100 m², pitfalls excluded, has RMS √(amplitude² + ridge RMS²) ± 20 %.
    static bool ReliefRms(WorldQuery sample, string surfacesPath)
    {
        SurfaceParams[] table = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        bool pass = true;
        foreach (SurfaceParams s in table)
            pass &= ReliefRmsOn(Uniform(table, s.Index, sample.Seed), s, 10, 20, $"{s.Id} (uniform world)");
        pass &= ReliefRmsOn(sample, table.First(s => s.Id == "meadow_sod"), -100, -30, "meadow_sod (sample_patch)");
        return pass;
    }

    static bool ReliefRmsOn(WorldQuery world, SurfaceParams s, double x0, double z0, string label)
    {
        var points = new XZ[200 * 200];
        for (int r = 0; r < 200; r++)
        {
            for (int c = 0; c < 200; c++)
                points[r * 200 + c] = new XZ(x0 + (c + 0.5) * 0.05, z0 + (r + 0.5) * 0.05);
        }
        GroundSample[] samples = SampleAll(world, points);
        double sum = 0;
        int n = 0;
        foreach (GroundSample g in samples)
        {
            if (g.Feature == GroundFeature.Pitfall || g.Surface != s.Index)
                continue;
            double d = g.GroundHeight - g.TerrainHeight;
            sum += d * d;
            n++;
        }
        double rms = Math.Sqrt(sum / n);
        double ridge = s.RidgeAmplitude * WorldQuery.RidgeProfileRms;
        double expected = Math.Sqrt(s.ReliefAmplitude * s.ReliefAmplitude + ridge * ridge);
        return Check($"micro-relief bounded, {label}", n > 0 && Math.Abs(rms - expected) <= 0.2 * expected,
            $"RMS {rms * 1000:0.00} mm vs expected {expected * 1000:0.00} mm ({(rms / expected - 1) * 100:+0.0;-0.0} %) "
            + $"over {n} points at 5 cm, {points.Length - n} in pitfalls or other surfaces (limit ±20 %)");
    }

    /// A flat 256 m world with one surface everywhere and every cover channel at 1.0.
    static WorldQuery Uniform(SurfaceParams[] table, byte index, uint seed)
    {
        const int samples = 257, cells = 512;
        var surface = new byte[cells * cells];
        Array.Fill(surface, index);
        var cover = new byte[cells * cells * 4];
        Array.Fill(cover, (byte)255);
        return new WorldQuery(table, 256, seed, samples, new float[samples * samples], cells, surface, cover);
    }

    /// Pitfalls on a 5 cm grid over 60 × 60 m of sample_patch meadow: each hit names a pitfall id and depth; the id
    /// decodes to a generator cell within reach of the point; the depth is exactly the drop against the same world
    /// without pitfalls; and a second instance, sampling each row in reverse order, gives the same id and depth.
    static bool Pitfalls(WorldQuery world, string dir, string surfacesPath)
    {
        SurfaceParams[] noPits = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        foreach (SurfaceParams s in noPits)
            s.PitDensity = 0;
        WorldQuery again = WorldQuery.Load(dir, surfacesPath);
        var withoutPits = new WorldQuery(noPits, world.SizeM, world.Seed, world.Samples, HeightsOf(world), world.Cells,
            SurfaceLayer(world), CoverLayer(dir));

        const int n = 1200;
        var row = new XZ[n];
        var reversed = new XZ[n];
        GroundSample[] samples = new GroundSample[n], bare = new GroundSample[n], replay = new GroundSample[n];
        var pits = new Dictionary<uint, (int hits, double deepest, double x, double z)>();
        double worstDrop = 0, maxReach = 0;
        bool stable = true, identified = true;
        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
            {
                row[c] = new XZ(-120 + (c + 0.5) * 0.05, -40 + (r + 0.5) * 0.05);
                reversed[n - 1 - c] = row[c];
            }
            world.SampleGround(row, samples);
            withoutPits.SampleGround(row, bare);
            again.SampleGround(reversed, replay);
            for (int i = 0; i < n; i++)
            {
                GroundSample g = samples[i], other = replay[n - 1 - i];
                stable &= other.FeatureId == g.FeatureId && other.FeatureDepth == g.FeatureDepth && other.GroundHeight == g.GroundHeight;
                if (g.Feature != GroundFeature.Pitfall)
                {
                    worstDrop = Math.Max(worstDrop, Math.Abs(bare[i].GroundHeight - g.GroundHeight));
                    continue;
                }
                identified &= g.FeatureId != 0 && g.FeatureDepth > 0;
                int cx = (int)(g.FeatureId & 0xFFFF) - 32768, cz = (int)(g.FeatureId >> 16) - 32768;
                double reachX = Math.Max(Math.Max(cx - row[i].X, row[i].X - (cx + 1)), 0);
                double reachZ = Math.Max(Math.Max(cz - row[i].Z, row[i].Z - (cz + 1)), 0);
                maxReach = Math.Max(maxReach, Math.Sqrt(reachX * reachX + reachZ * reachZ));
                worstDrop = Math.Max(worstDrop, Math.Abs(bare[i].GroundHeight - g.GroundHeight - g.FeatureDepth));
                pits.TryGetValue(g.FeatureId, out var seen);
                pits[g.FeatureId] = (seen.hits + 1, Math.Max(seen.deepest, g.FeatureDepth), row[i].X, row[i].Z);
            }
        }
        double pitRadiusMax = world.Surfaces.Where(s => s.PitDensity > 0).Max(s => s.PitRadiusMax);
        foreach (var (id, p) in pits.OrderByDescending(p => p.Value.deepest).Take(3))
            GD.Print($"selftest worldquery:   pitfall 0x{id:x8} (cell {(int)(id & 0xFFFF) - 32768}, {(int)(id >> 16) - 32768}): "
                + $"{p.hits} samples at 5 cm, deepest {p.deepest:0.000} m, e.g. at x={p.x:0.00}, z={p.z:0.00}");
        return Check("pitfall is identifiable", pits.Count > 0 && identified && stable && worstDrop <= 1e-6 && maxReach <= pitRadiusMax,
            $"{pits.Count} pitfalls in 3600 m² (expected about {3600 * 0.01:0} for meadow at 0.01/m²); every hit has an id and depth: "
            + $"{identified}; FeatureDepth = drop vs the same world without pitfalls within {worstDrop * 1000:0.000000} mm; "
            + $"hits lie at most {maxReach:0.000} m from their id's cell (radius max {pitRadiusMax} m); same in reverse order: {stable}");
    }

    static float[] HeightsOf(WorldQuery world)
    {
        var points = new XZ[world.Samples * world.Samples];
        for (int j = 0; j < world.Samples; j++)
        {
            for (int i = 0; i < world.Samples; i++)
                points[j * world.Samples + i] = new XZ(-world.Half + i * world.HeightResolution, -world.Half + j * world.HeightResolution);
        }
        return SampleAll(world, points).Select(g => (float)g.TerrainHeight).ToArray();
    }

    static byte[] CoverLayer(string dir) => MapPng.Read(Path.Combine(dir, "cover.png"), 4, out _, out _);

    /// Points beyond every edge and corner, far away and not a number: nothing throws, OutsideMap is set, and terrain and
    /// surface are those of the nearest edge point.
    static bool OutsideMap(WorldQuery world)
    {
        double h = world.Half;
        var points = new[]
        {
            new XZ(h + 5, 10), new XZ(-h - 5, -20), new XZ(30, h + 0.001), new XZ(-40, -h - 12), new XZ(h + 3, h + 3),
            new XZ(-h - 1e6, 1e6), new XZ(1e12, -1e12), new XZ(double.NaN, 0), new XZ(0, double.PositiveInfinity),
        };
        var edges = points.Select(p => new XZ(Clamp(p.X, h), Clamp(p.Z, h))).ToArray();
        bool ok = true;
        string numbers = "";
        try
        {
            GroundSample[] outside = SampleAll(world, points), inside = SampleAll(world, edges);
            for (int i = 0; i < points.Length; i++)
            {
                GroundSample o = outside[i], e = inside[i];
                bool flagged = (o.Flags & GroundFlags.OutsideMap) != 0, notFlagged = (e.Flags & GroundFlags.OutsideMap) == 0;
                bool finite = double.IsFinite(o.GroundHeight) && double.IsFinite(o.SupportTop) && float.IsFinite(o.Normal.Y);
                bool clamped = !double.IsFinite(points[i].X + points[i].Z)
                    || o.TerrainHeight == e.TerrainHeight && o.Surface == e.Surface;
                ok &= flagged && notFlagged && finite && clamped;
                numbers += $"({points[i].X:g4}, {points[i].Z:g4}) flag {flagged}, terrain {o.TerrainHeight:0.000} = edge "
                    + $"{e.TerrainHeight:0.000}, surface {o.Surface} = {e.Surface}; ";
            }
        }
        catch (Exception e)
        {
            ok = false;
            numbers = $"threw {e.GetType().Name}: {e.Message}";
        }
        return Check("outside the map", ok, numbers);
    }

    static double Clamp(double v, double bound) => double.IsNaN(v) ? -bound : Math.Clamp(v, -bound, bound);

    /// Informational: the formal benchmark with allocation counts is task 3.6.
    static void Timings(WorldQuery world)
    {
        var random = new Random(5);
        var points = new XZ[100000];
        for (int i = 0; i < points.Length; i++)
            points[i] = new XZ(-world.Half + random.NextDouble() * world.SizeM, -world.Half + random.NextDouble() * world.SizeM);
        var results = new GroundSample[points.Length];
        world.SampleGround(points, results);
        var watch = Stopwatch.StartNew();
        world.SampleGround(points, results);
        double groundMs = watch.Elapsed.TotalMilliseconds;
#if DEBUG
        const string build = "Debug";
#else
        const string build = "Release";
#endif
        GD.Print($"selftest worldquery: timings ({build} build, informational; the benchmark is task 3.6): 100000 random ground samples {groundMs:0.0} ms "
            + $"(budget 10 ms)");
    }
}
