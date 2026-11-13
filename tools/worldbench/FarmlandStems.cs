using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Fpv450.Sim.World;

namespace Fpv450.WorldBench
{
    // The farmland's stem field (w9-village-physics 2.1, design.md "## 2.1"): StemsNear over maps/farmland's grass.
    // - Golden: the three cells (the densest tall cell, a bare tilled cell, a cell on the map's east edge with its circle
    //   half off the map) at five heights; 10 000 random circles over the map and past its edge, run again on four threads
    //   (the same bits); and the whole field against the export's counts: every grass cell asked at its centre, each layer's
    //   stems summed over the map against map.json's instances x the plants' stems per instance.
    // - Bench: a step's four rotor discs and four leg points over the densest tall cell's field, 0 B, shown against PLT-04's
    //   60 us line; the field's file read and SHA-256 checked, the part of the load it adds.
    internal static partial class Program
    {
        /// <summary>A 10-inch prop's disc radius, m (254 mm; carried, the drone's).</summary>
        private const double RotorRadius = 0.127;

        private static readonly (string Name, double X, double Z, double Radius)[] StemCells =
        {
            ("the densest tall cell", 768.06640625, 917.48046875, RotorRadius),
            ("a bare tilled cell", 229.98046875, 257.32421875, RotorRadius),
            ("a cell on the map's east edge, the circle half off the map", 999.9, 908.69140625, 0.6),
        };

        private static readonly double[] StemHeights = { 0, 0.3, 0.6, 1.0, 1.5 };

        private static List<(string Name, string Value, bool Agree)> FarmlandStemCases(MapWorld world)
        {
            var cases = new List<(string, string, bool)>();
            var found = new StemPatch[64];
            var cellHash = new Hasher();
            int bare = 0, tallLayers = 0;
            foreach (var c in StemCells)
            {
                foreach (double h in StemHeights)
                {
                    int n = world.StemsNear(new XZ(c.X, c.Z), c.Radius, h, found);
                    cellHash.Add(n);
                    double stems = 0;
                    var parts = new List<string>();
                    for (int k = 0; k < Math.Min(n, found.Length); k++)
                    {
                        cellHash.Add(found[k]);
                        stems += found[k].Density * found[k].Area;
                        parts.Add(string.Format(CultureInfo.InvariantCulture, "L{0} {1} {2:F0}/m2 x {3:F5} m2 (tops {4:F2}-{5:F2} m)",
                            found[k].Layer, found[k].Kind, found[k].Density, found[k].Area, found[k].HeightMin, found[k].HeightMax));
                    }
                    bare += c.Name.StartsWith("a bare", StringComparison.Ordinal) ? n : 0;
                    tallLayers += c.Name.StartsWith("the densest", StringComparison.Ordinal) && h == 0 ? n : 0;
                    Console.WriteLine($"  farmland.stems.cells: {c.Name} ({c.X}, {c.Z}), r {c.Radius} m, h {h} m: {n} layers, {stems:F1} stems in the circle"
                        + (parts.Count > 0 ? "; " + string.Join("; ", parts) : ""));
                }
            }
            cases.Add(("farmland.stems.cells", Hex(cellHash.Value), bare == 0 && tallLayers == 11));

            // 10 000 circles over the map and 5 m past its edges, radii 0.01-1.5 m, heights -0.1-2 m; then on four threads.
            const int asked = GoldenCount;
            var centres = new XZ[asked];
            var radii = new double[asked];
            var heights = new double[asked];
            var s = new Stream(2100);
            for (int i = 0; i < asked; i++)
            {
                centres[i] = new XZ(s.In(-5, 1005), s.In(-5, 1005));
                radii[i] = s.In(0.01, 1.5);
                heights[i] = s.In(-0.1, 2);
            }
            ulong Run(int from, int step)
            {
                var buffer = new StemPatch[64];
                var hash = new Hasher();
                for (int i = from; i < asked; i += step)
                {
                    int n = world.StemsNear(centres[i], radii[i], heights[i], buffer);
                    hash.Add(n);
                    for (int k = 0; k < Math.Min(n, buffer.Length); k++)
                        hash.Add(buffer[k]);
                }
                return hash.Value;
            }
            ulong one = Run(0, 1);
            var threads = new ulong[4];
            Parallel.For(0, 4, t => threads[t] = Run(0, 1));
            bool same = Array.TrueForAll(threads, v => v == one);
            int some = 0, most = 0;
            for (int i = 0; i < asked; i++)
            {
                int n = world.StemsNear(centres[i], radii[i], heights[i], found);
                some += n > 0 ? 1 : 0;
                most = Math.Max(most, n);
            }
            cases.Add(("farmland.stems.random", Hex(one), same));
            Console.WriteLine($"  farmland.stems.random: {asked} circles, {some} meet stems, at most {most} (cell, layer) pairs; four threads {(same ? "agree" : "DIFFER")}");

            // The whole field against the export's counts.
            string root = RepositoryRoot();
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "maps", "farmland", "map.json")));
            using JsonDocument numbers = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, MapWorld.PlantNumbers)));
            JsonElement grass = manifest.RootElement.GetProperty("grass");
            int res = grass.GetProperty("resolution").GetInt32();
            byte[] counts = File.ReadAllBytes(Path.Combine(root, "maps", "farmland", grass.GetProperty("file").GetString()));
            int layers = grass.GetProperty("layers").GetArrayLength(), plane = res * res;
            double side = 1000.0 / res;
            var sums = new double[layers];
            for (int cell = 0; cell < plane; cell++)
            {
                bool any = false;
                for (int l = 0; l < layers && !any; l++)
                    any = counts[l * plane + cell] != 0;
                if (!any)
                    continue;
                int row = cell / res, col = cell % res;
                int n = world.StemsNear(new XZ((col + 0.5) * side, 1000 - (row + 0.5) * side), 0.1, 0, found);
                for (int k = 0; k < n; k++)
                    sums[found[k].Layer] += found[k].Density * side * side;
            }
            var fieldHash = new Hasher();
            int off = 0;
            for (int l = 0; l < layers; l++)
            {
                JsonElement layer = grass.GetProperty("layers")[l];
                double want = layer.GetProperty("instances").GetInt64()
                    * numbers.RootElement.GetProperty("grass").GetProperty("prefabs").GetProperty(layer.GetProperty("prefab").GetString()).GetProperty("stems").GetInt64();
                fieldHash.Add(sums[l]);
                bool ok = Math.Abs(sums[l] - want) <= 1e-9 * want;
                off += ok ? 0 : 1;
                Console.WriteLine($"  farmland.stems.field: layer {l} ({layer.GetProperty("prefab").GetString()}, {layer.GetProperty("group").GetString()}): "
                    + $"{sums[l]:F1} stems over the map, the export's {want:F0} ({(ok ? "agree" : "DIFFER")})");
            }
            cases.Add(("farmland.stems.field", Hex(fieldHash.Value), off == 0));
            return cases;
        }

        /// <summary>A step's StemsNear calls (four rotor discs, four leg points) over the densest tall cell's field: 0 B, timed.</summary>
        private static int FarmlandStemBench(MapWorld world)
        {
            string root = RepositoryRoot();
            var read = Stopwatch.StartNew();
            byte[] raw = File.ReadAllBytes(Path.Combine(root, "maps", "farmland", "grass", "map.raw"));
            using (SHA256 sha = SHA256.Create())
                sha.ComputeHash(raw);
            Console.WriteLine($"  farmland.stems.load: grass/map.raw read and its SHA-256 checked in {read.ElapsedMilliseconds} ms ({raw.Length / 1048576.0:F1} MiB held), "
                + "the field's part of farmland.load (shown)");

            const int steps = 20000;
            double us = 1e6 / Stopwatch.Frequency;
            var found = new StemPatch[64];
            var times = new long[steps];
            int failures = 0;
            // The rotors at ±0.16 m (a 10-inch quad's half diagonal, carried), the legs at ±0.12 m; the drone wanders over
            // 20 m of the tall dry field about the densest tall cell, the discs 0-1.8 m over the ground.
            (double dx, double dz)[] rotors = { (0.16, 0.16), (-0.16, 0.16), (-0.16, -0.16), (0.16, -0.16) };
            (double dx, double dz)[] legs = { (0.12, 0.12), (-0.12, 0.12), (-0.12, -0.12), (0.12, -0.12) };
            foreach ((string name, bool discs) in new[] { ("4 rotor discs r 0.127 m at 0-1.8 m", true), ("4 leg points r 0.01 m at 0 m", false) })
            {
                long allocated = 0, records = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    long before = Fpv450.Sim.Tests.AllocProbe.Before();
                    records = 0;
                    for (int st = 0; st < steps; st++)
                    {
                        double x = 758 + 20 * Tri(st, 2 * 4111), z = 907 + 20 * Tri(st, 2 * 3313), h = 1.8 * Tri(st, 2 * 709);
                        long start = Stopwatch.GetTimestamp();
                        foreach ((double dx, double dz) in discs ? rotors : legs)
                            records += world.StemsNear(new XZ(x + dx, z + dz), discs ? RotorRadius : 0.01, discs ? h : 0, found);
                        times[st] = Stopwatch.GetTimestamp() - start;
                    }
                    allocated = Fpv450.Sim.Tests.AllocProbe.After(before);
                }
                Array.Sort(times);
                failures += allocated == 0 ? 0 : 1;
                Console.WriteLine($"  farmland.stems, {name}, a step: median {times[steps / 2] * us:F2} us, p99 {times[steps * 99 / 100] * us:F2} us, "
                    + $"max {times[steps - 1] * us:F1} us; {(double)records / steps / 4:F1} (cell, layer) pairs a query; {allocated} B (shown against PLT-04's 60 us)");
            }
            return failures;
        }
    }
}
