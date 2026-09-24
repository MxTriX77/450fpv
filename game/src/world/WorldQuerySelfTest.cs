using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;

/// `-- --selftest worldquery`: every world-query scenario of SampleGround (task 3.1) and MicroDetailNear (task 3.2) on
/// game/maps/sample_patch, plus uniform in-memory worlds for the per-surface checks. Prints one PASS/FAIL line per
/// scenario with its numbers and exits non-zero on any failure. `-- --selftest worldquery-digest --digest-out <file>` is
/// the second process of the replay-identity check.
public static partial class WorldQuerySelfTest
{
    const string Package = "res://maps/sample_patch";
    const string SurfacesPath = "res://maps/surfaces.json";
    const string CatalogPath = "res://assets/catalog.json";

    public static async void Run(Node3D sandbox)
    {
        SceneTree tree = sandbox.GetTree();
        bool pass = false;
        try
        {
            string dir = ProjectSettings.GlobalizePath(Package), surfaces = ProjectSettings.GlobalizePath(SurfacesPath);
            WorldQuery world = WorldQuery.Load(dir, surfaces, ProjectSettings.GlobalizePath(CatalogPath));

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
            pass &= ReplayIdentity(world);
            pass &= Overlap(world);
            pass &= CrossingStraw(world);
            pass &= Density(world, surfaces);
            pass &= Overflow(world);
            pass &= ElementRules(world);
            pass &= ObjectScenarios(world, dir, surfaces);
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

    /// The second process of the replay-identity check: prints the digest and writes it to `--digest-out`.
    public static void Digest(Node sandbox, string outPath)
    {
        WorldQuery world = WorldQuery.Load(ProjectSettings.GlobalizePath(Package), ProjectSettings.GlobalizePath(SurfacesPath),
            ProjectSettings.GlobalizePath(CatalogPath));
        string digest = $"{ReplayDigest(world):x16}-{ObjectDigest():x16}";
        GD.Print($"selftest worldquery-digest: {digest}");
        if (outPath != null)
            File.WriteAllText(outPath, digest);
        sandbox.GetTree().Quit(0);
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
    static WorldQuery Uniform(SurfaceParams[] table, byte index, uint seed, Catalog catalog = null)
    {
        const int samples = 257, cells = 512;
        var surface = new byte[cells * cells];
        Array.Fill(surface, index);
        var cover = new byte[cells * cells * 4];
        Array.Fill(cover, (byte)255);
        return new WorldQuery(table, 256, seed, samples, new float[samples * samples], cells, surface, cover, catalog);
    }

    /// Pitfalls on a 5 cm grid over 60 × 60 m of sample_patch meadow: each hit names a pitfall id and depth; the id
    /// decodes to a generator cell within reach of the point; the depth is exactly the drop against the same world
    /// without pitfalls; and a second instance, sampling each row in reverse order, gives the same id and depth.
    static bool Pitfalls(WorldQuery world, string dir, string surfacesPath)
    {
        SurfaceParams[] noPits = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        foreach (SurfaceParams s in noPits)
            s.PitDensity = 0;
        WorldQuery again = WorldQuery.Load(dir, surfacesPath, ProjectSettings.GlobalizePath(CatalogPath));
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

    // ---------------------------------------------------------------- 3.2 MicroDetailNear

    /// Query centres on every sample_patch surface with cover, 2 m radius, all kinds.
    static readonly Double3[] Centres =
    {
        new(-100, 0, -80), new(0, 0, -60), new(-20, 0, -54), new(40, 0, 20), new(64, 0, 40), new(-60, 0, 58.5), new(100, 0, 100),
    };

    /// FNV-1a over the raw bytes of every element list and ground sample of a fixed set of queries.
    static ulong ReplayDigest(WorldQuery world)
    {
        ulong hash = 14695981039346656037UL;
        void Add(ReadOnlySpan<byte> bytes)
        {
            foreach (byte b in bytes)
                hash = (hash ^ b) * 1099511628211UL;
        }
        var buffer = new MicroElement[40000];
        var ground = new GroundSample[64];
        var points = new XZ[64];
        foreach (Double3 c in Centres)
        {
            Double3 centre = At(world, c);
            int count = world.MicroDetailNear(centre, 2.0, KindMask.All, buffer);
            Add(BitConverter.GetBytes(count));
            Add(MemoryMarshal.AsBytes(buffer.AsSpan(0, Math.Min(count, buffer.Length))));
            for (int i = 0; i < points.Length; i++)
                points[i] = new XZ(c.X + 0.37 * i, c.Z - 0.21 * i);
            world.SampleGround(points, ground);
            Add(MemoryMarshal.AsBytes(ground.AsSpan()));
        }
        return hash;
    }

    /// A centre at the ground height under (x, z) + 0.3 m.
    static Double3 At(WorldQuery world, Double3 c)
    {
        var g = new GroundSample[1];
        world.SampleGround(new[] { new XZ(c.X, c.Z) }, g);
        return new Double3(c.X, g[0].GroundHeight + 0.3, c.Z);
    }

    /// The same queries in a second, separate Godot process must give bit-identical results.
    static bool ReplayIdentity(WorldQuery world)
    {
        string mine = $"{ReplayDigest(world):x16}-{ObjectDigest():x16}";
        string outPath = Path.Combine(OS.GetUserDataDir(), "worldquery_digest.txt");
        File.Delete(outPath);
        var output = new Godot.Collections.Array();
        var watch = Stopwatch.StartNew();
        int exit = OS.Execute(OS.GetExecutablePath(), new[] { "--headless", "--path", ProjectSettings.GlobalizePath("res://"), "--",
            "--selftest", "worldquery-digest", "--digest-out", outPath }, output, true);
        string theirs = File.Exists(outPath) ? File.ReadAllText(outPath).Trim() : "(none)";
        return Check("replay identity across two processes", exit == 0 && theirs == mine,
            $"this process {mine}, a separate Godot process ({watch.ElapsedMilliseconds} ms, exit {exit}) {theirs} over "
            + $"{Centres.Length} MicroDetailNear queries (r 2 m, all kinds) and {Centres.Length * 64} ground samples, then "
            + "static contacts, rays, gaps and the wind grid of sample_patch with launch rails added");
    }

    /// Two overlapping queries: every element of the first that meets the second sphere (tested independently here) is
    /// in the second, byte for byte, and the shared elements keep the same relative order.
    static bool Overlap(WorldQuery world)
    {
        bool pass = true;
        foreach (Double3 c in new[] { Centres[0], Centres[2], Centres[3] })
        {
            Double3 a = At(world, c), b = new(a.X + 1.3, a.Y + 0.2, a.Z - 0.9);
            MicroElement[] first = Query(world, a, 2.0, KindMask.All), second = Query(world, b, 1.5, KindMask.All);
            var index = new Dictionary<ulong, int>();
            for (int i = 0; i < second.Length; i++)
                index[second[i].Id] = i;
            int shared = 0, missing = 0, differing = 0, last = -1;
            bool ordered = true;
            foreach (MicroElement e in first)
            {
                bool meets = Meets(e, b, 1.5 - 1e-6); // the stored direction is float; stay clear of the boundary
                if (!index.TryGetValue(e.Id, out int j))
                {
                    missing += meets ? 1 : 0;
                    continue;
                }
                shared++;
                differing += Bytes(e).SequenceEqual(Bytes(second[j])) ? 0 : 1;
                ordered &= j > last;
                last = j;
            }
            pass &= Check($"overlapping queries agree at ({c.X}, {c.Z})", shared > 0 && missing == 0 && differing == 0 && ordered,
                $"{first.Length} and {second.Length} elements, {shared} in both, {missing} that meet both spheres but are "
                + $"missing from the second, {differing} differing, same relative order {ordered}");
        }
        return pass;
    }

    static byte[] Bytes(MicroElement e) => MemoryMarshal.AsBytes(new ReadOnlySpan<MicroElement>(in e)).ToArray();

    static MicroElement[] Query(WorldQuery world, Double3 centre, double radius, KindMask kinds)
    {
        var buffer = new MicroElement[200000];
        int count = world.MicroDetailNear(centre, radius, kinds, buffer);
        if (count > buffer.Length)
            throw new InvalidOperationException($"test buffer too small for {count} elements");
        return buffer[..count];
    }

    static bool Meets(MicroElement e, Double3 c, double radius)
    {
        double rx = c.X - e.Base.X, ry = c.Y - e.Base.Y, rz = c.Z - e.Base.Z;
        double t = Math.Clamp(rx * e.Direction.X + ry * e.Direction.Y + rz * e.Direction.Z, 0, e.Length);
        double qx = rx - t * e.Direction.X, qy = ry - t * e.Direction.Y, qz = rz - t * e.Direction.Z;
        return Math.Sqrt(qx * qx + qy * qy + qz * qz) <= radius + e.Diameter / 2;
    }

    /// A lying straw of at least 0.9 m in belt_straw; a 0.1 m sphere on its axis 80 % of the way to the tip, so its base
    /// is well outside the sphere. The straw must be returned.
    static bool CrossingStraw(WorldQuery world)
    {
        MicroElement straw = Query(world, At(world, Centres[1]), 2.0, KindMask.Straw).First(e => e.Length >= 0.9f);
        double t = 0.8 * straw.Length;
        var centre = new Double3(straw.Base.X + t * straw.Direction.X, straw.Base.Y + t * straw.Direction.Y,
            straw.Base.Z + t * straw.Direction.Z);
        MicroElement[] found = Query(world, centre, 0.1, KindMask.Straw);
        double baseDistance = Math.Sqrt(Math.Pow(straw.Base.X - centre.X, 2) + Math.Pow(straw.Base.Y - centre.Y, 2)
            + Math.Pow(straw.Base.Z - centre.Z, 2));
        return Check("crossing straw is found", found.Any(e => e.Id == straw.Id) && baseDistance > 0.1 + straw.Length * 0.5,
            $"straw 0x{straw.Id:x16}, {straw.Length:0.000} m long, rooted {baseDistance:0.000} m from the centre of a 0.1 m "
            + $"sphere it crosses: returned among {found.Length} straws");
    }

    /// Elements per m² by base position over 100 m² (10 × 10 m) of belt_straw at cover density 1.0, on a uniform world and
    /// on the straw channel of sample_patch (which paints straw at 1.0), vs the table, ±5 %.
    static bool Density(WorldQuery sample, string surfacesPath)
    {
        SurfaceParams[] table = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        SurfaceParams belt = table.First(s => s.Id == "belt_straw");
        bool pass = DensityOn(Uniform(table, belt.Index, sample.Seed), belt, 20, 20, 10, KindMask.All, "uniform world");
        pass &= DensityOn(sample, belt, -60, -63.5, 20, KindMask.Straw, "sample_patch straw band, 20 × 5 m");
        return pass;
    }

    static bool DensityOn(WorldQuery world, SurfaceParams belt, double x0, double z0, double width, KindMask kinds, string label)
    {
        double depth = 100 / width;
        Double3 centre = At(world, new Double3(x0 + width / 2, 0, z0 + depth / 2));
        MicroElement[] all = Query(world, centre, Math.Sqrt(width * width + depth * depth) / 2 + 0.5, kinds);
        bool pass = true;
        string numbers = "";
        for (int k = 0; k < 4; k++)
        {
            if (((int)kinds & 1 << k) == 0 || belt.Cover[k] == null)
                continue;
            int n = all.Count(e => (int)e.Kind == k && e.Base.X >= x0 && e.Base.X < x0 + width && e.Base.Z >= z0 && e.Base.Z < z0 + depth);
            double perM2 = n / 100.0, expected = belt.Cover[k].Density;
            pass &= Math.Abs(perM2 - expected) <= 0.05 * expected;
            numbers += $"{(CoverKind)k} {perM2:0.00}/m² vs {expected}/m² ({(perM2 / expected - 1) * 100:+0.00;-0.00} %); ";
        }
        return Check($"density matches the surface, belt_straw ({label})", pass, numbers + "over 100 m² (limit ±5 %)");
    }

    /// A buffer smaller than the result: it holds the first elements in canonical order and the count exceeds it.
    static bool Overflow(WorldQuery world)
    {
        Double3 centre = At(world, Centres[0]);
        MicroElement[] full = Query(world, centre, 2.0, KindMask.All);
        var small = new MicroElement[full.Length / 3];
        int count = world.MicroDetailNear(centre, 2.0, KindMask.All, small);
        bool same = small.Select(Bytes).SequenceEqual(full[..small.Length].Select(Bytes), new BytesComparer());
        return Check("overflow reported", count == full.Length && count > small.Length && same,
            $"buffer {small.Length}, returned count {count} (full query {full.Length}), buffer equals the first {small.Length} "
            + $"of the full list: {same}");
    }

    sealed class BytesComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
        public int GetHashCode(byte[] a) => a.Length;
    }

    /// Per-element rules (W-5, W-6) on every returned element of the query centres: canonical order, unique ids, base on
    /// GroundHeight (standing grass) or SupportTop (lying), unit direction, ranges, scaled tip stiffness, hook release.
    static bool ElementRules(WorldQuery world)
    {
        int total = 0, bad = 0;
        string first = "";
        var ids = new HashSet<ulong>();
        var ground = new GroundSample[1];
        foreach (Double3 c in Centres)
        {
            MicroElement[] elements = Query(world, At(world, c), 2.0, KindMask.All);
            ulong previousKey = 0;
            foreach (MicroElement e in elements)
            {
                total++;
                CoverParams p = world.Surface(e.Surface).Cover[(int)e.Kind];
                world.SampleGround(new[] { new XZ(e.Base.X, e.Base.Z) }, ground);
                double baseY = e.Kind == CoverKind.Grass ? ground[0].GroundHeight : ground[0].SupportTop;
                double dr = e.Diameter / ((p.DiameterMin + p.DiameterMax) / 2), lr = (p.LengthMin + p.LengthMax) / 2 / e.Length;
                double stiffness = p.TipStiffness * Math.Pow(dr, 4) * Math.Pow(lr, 3);
                int cx = (int)((e.Id >> 11) & 0x1FFFF) - 65536, cz = (int)((e.Id >> 28) & 0x1FFFF) - 65536;
                ulong key = (ulong)(cz + 65536) << 40 | (ulong)(cx + 65536) << 20 | (e.Id & 0x7FF);
                string why =
                    !ids.Add(e.Id) ? "duplicate id" :
                    key <= previousKey && previousKey != 0 ? "out of canonical order" :
                    Math.Floor(e.Base.X / WorldQuery.MicroCell) != cx || Math.Floor(e.Base.Z / WorldQuery.MicroCell) != cz ? "base outside its cell" :
                    e.Base.Y != baseY ? $"base y {e.Base.Y} vs {baseY}" :
                    Math.Abs(e.Direction.Length() - 1) > 1e-6 ? "direction not unit" :
                    e.Length < p.LengthMin - 1e-6 || e.Length > p.LengthMax + 1e-6 ? "length out of range" :
                    e.Diameter < p.DiameterMin - 1e-7 || e.Diameter > p.DiameterMax + 1e-7 ? "diameter out of range" :
                    Math.Abs(e.TipStiffness - stiffness) > 1e-5 * stiffness ? $"tip stiffness {e.TipStiffness} vs {stiffness}" :
                    e.HookRelease < p.HookReleaseMin - 1e-5 || e.HookRelease > p.HookReleaseMax + 1e-5 ? "hook release out of range" :
                    e.Kind == CoverKind.Grass && e.Direction.Y < Math.Cos(20.0 / 180 * Math.PI) - 1e-6 ? "grass leans too far" :
                    null;
                previousKey = key;
                if (why != null && bad++ == 0)
                    first = $"; first: 0x{e.Id:x16} {why}";
            }
        }
        return Check("element rules", bad == 0 && total > 0, $"{total} elements, {bad} breaking a rule{first}");
    }

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
        Double3 meadow = At(world, Centres[0]);
        var buffer = new MicroElement[40000];
        int count = world.MicroDetailNear(meadow, 2.0, KindMask.All, buffer);
        watch.Restart();
        for (int i = 0; i < 20; i++)
            world.MicroDetailNear(meadow, 2.0, KindMask.All, buffer);
#if DEBUG
        const string build = "Debug";
#else
        const string build = "Release";
#endif
        GD.Print($"selftest worldquery: timings ({build} build, informational; the benchmark is task 3.6): 100000 random ground samples {groundMs:0.0} ms "
            + $"(budget 10 ms); MicroDetailNear r 2 m on meadow_sod, {count} elements, {watch.Elapsed.TotalMilliseconds / 20:0.000} ms "
            + "(budget 0.25 ms)");
    }
}
