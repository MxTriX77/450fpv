using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Fpv450.Sim;
using Fpv450.Sim.World;

namespace Fpv450.WorldBench
{
    // World-query golden files and benchmarks (WLD-11, K§10.4).
    // --golden            runs every case and checks it against golden/<world>.txt
    // --golden --record   writes those files (a re-record is audited case by case, with a QueryVersion bump)
    // --bench             times a physics step's batch (K§10.5) and 100 000 capsules and rays, on AC only (PLT-07)
    // Worlds: the floor stub (p6-world-query-gallery task 1.1, golden/floor.txt) and the test range (tasks 1.4 and 1.5,
    // golden/test-range.txt; TestRange.cs), the map of D-047.
    internal static partial class Program
    {
        private const int GoldenCount = 10000;

        private static int Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "--golden" && (args.Length == 1 || args.Length == 2 && args[1] == "--record"))
                return Golden(args.Length == 2);
            if (args.Length == 1 && args[0] == "--bench")
                return Bench();
            Console.Error.WriteLine("usage: worldbench --golden [--record] | --bench");
            return 2;
        }

        // ---- The floor stub's world and inputs -------------------------------------------------------------------

        /// <summary>The stub with one probe object holding each runtime shape kind, turned off the world axes.</summary>
        private static FloorWorld FloorWithProbe()
        {
            var world = new FloorWorld(0, "meadow_sod", "steel", "timber");
            ushort steel = world.MaterialId("steel"), timber = world.MaterialId("timber");
            Double3 x = new Double3(1, 0, 0), y = new Double3(0, 1, 0), z = new Double3(0, 0, 1);
            // Exact 3-4-5 rotations, so the axes are orthonormal to the last bit.
            Double3 yawX = new Double3(0.8, 0, -0.6), yawZ = new Double3(0.6, 0, 0.8);
            Double3 tiltY = new Double3(0, 0.8, 0.6), tiltZ = new Double3(0, -0.6, 0.8);
            world.AddObject("probe", new[]
            {
                new ShapeGeometry { Kind = ShapeKind.Box, Center = new Double3(1, 0.5, 2), AxisX = yawX, AxisY = y, AxisZ = yawZ,
                    HalfExtents = new Double3(0.4, 0.3, 0.2), Material = steel },
                new ShapeGeometry { Kind = ShapeKind.Sphere, Center = new Double3(-1, 0.6, 1.5), AxisX = x, AxisY = y, AxisZ = z,
                    Radius = 0.3, Material = steel },
                new ShapeGeometry { Kind = ShapeKind.Capsule, Center = new Double3(0, 1.5, -1), AxisX = x, AxisY = tiltY, AxisZ = tiltZ,
                    Radius = 0.1, HalfLength = 1.0, Material = timber },
                new ShapeGeometry { Kind = ShapeKind.Cylinder, Center = new Double3(-1.5, 0.25, -1.5), AxisX = yawX, AxisY = y, AxisZ = yawZ,
                    Radius = 0.3, HalfLength = 0.25, Material = steel },
                new ShapeGeometry { Kind = ShapeKind.Box, Center = new Double3(2, 0.1, -2), AxisX = x, AxisY = y, AxisZ = z,
                    HalfExtents = new Double3(0.5, 0.1, 0.5), Surface = 1 }, // a soil shape
            });
            return world;
        }

        /// <summary>SplitMix64 (Steele, Lea and Flood, 2014): the inputs' integer stream, the same on every run.</summary>
        private struct Stream
        {
            private ulong _state;

            public Stream(ulong seed) => _state = seed;

            public double Next()
            {
                ulong z = _state += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return ((z ^ (z >> 31)) >> 11) * (1.0 / 9007199254740992.0); // [0, 1)
            }

            public double In(double lo, double hi) => lo + (hi - lo) * Next();

            public Double3 Point() => new Double3(In(-3, 3), In(-0.2, 3), In(-3, 3));

            public Double3 Move(double reach) => new Double3(In(-reach, reach), In(-reach, reach), In(-reach, reach));
        }

        private static XZ[] GroundPoints(int n)
        {
            var s = new Stream(1);
            var points = new XZ[n];
            for (int i = 0; i < n; i++)
                points[i] = new XZ(s.In(-50, 50), s.In(-50, 50));
            return points;
        }

        /// <summary>Swept capsules and spheres (one in four) around the probe, moving up to 0.5 m a step.</summary>
        private static void Capsules(int n, out Capsule[] previous, out Capsule[] current)
        {
            var s = new Stream(2);
            previous = new Capsule[n];
            current = new Capsule[n];
            for (int i = 0; i < n; i++)
            {
                double r = s.In(0.005, 0.1);
                Double3 a = s.Point(), b = i % 4 == 0 ? a : a + s.Move(0.15);
                Double3 move = s.Move(0.3);
                previous[i] = new Capsule(a, b, r);
                current[i] = new Capsule(a + move, b + move + s.Move(0.02), r);
            }
        }

        private static Ray[] Rays(int n)
        {
            var s = new Stream(3);
            var rays = new Ray[n];
            for (int i = 0; i < n; i++)
            {
                Double3 d = s.Move(1);
                if (d.Length() < 1e-3)
                    d = new Double3(0, -1, 0);
                rays[i] = new Ray(s.Point(), d);
            }
            return rays;
        }

        // ---- Golden ------------------------------------------------------------------------------------------------

        private sealed class Hasher
        {
            private ulong _hash = WorldHash.Offset;

            public ulong Value => _hash;

            public void Add(ulong v)
            {
                for (int k = 0; k < 8; k++)
                    _hash = (_hash ^ (byte)(v >> 8 * k)) * 0x100000001B3UL;
            }

            public void Add(double v) => Add((ulong)BitConverter.DoubleToInt64Bits(v));
            public void Add(Double3 v) { Add(v.X); Add(v.Y); Add(v.Z); }
            public void Add(long v) => Add((ulong)v);

            public void Add(in GroundSample g) { Add(g.Height); Add(g.Normal); Add(g.Surface); Add((long)g.Flags); }

            public void Add(in StaticContact c)
            {
                Add(c.Point); Add(c.Normal); Add(c.Distance); Add(c.Object); Add(c.Shape); Add(c.Material); Add(c.Surface);
                Add(c.WireParam); Add(c.Time);
            }

            public void Add(in RayHit h) { Add(h.Distance); Add(h.Point); Add(h.Normal); Add(h.Object); Add(h.Shape); Add(h.Material); Add(h.Surface); }
        }

        private static string Hex(ulong v) => v.ToString("x16", CultureInfo.InvariantCulture);

        /// <summary>The floor stub's cases: each case's hash, and whether its batched run equals its one-by-one run.</summary>
        private static List<(string Name, string Value, bool Agree)> FloorCases()
        {
            FloorWorld world = FloorWithProbe();
            var cases = new List<(string, string, bool)>
            {
                ("query_version", world.QueryVersion.ToString(CultureInfo.InvariantCulture), true),
                ("content_hash", Hex(world.ContentHash), true),
            };

            XZ[] points = GroundPoints(GoldenCount);
            var batch = new GroundSample[points.Length];
            var single = new GroundSample[1];
            world.SampleGround(points, batch);
            Hasher batched = new Hasher(), oneByOne = new Hasher();
            for (int i = 0; i < points.Length; i++)
            {
                batched.Add(batch[i]);
                world.SampleGround(points.AsSpan(i, 1), single);
                oneByOne.Add(single[0]);
            }
            cases.Add(("floor.ground", Hex(batched.Value), batched.Value == oneByOne.Value));

            Capsules(GoldenCount, out Capsule[] previous, out Capsule[] current);
            var contacts = new StaticContact[8];
            var contactHash = new Hasher();
            int touched = 0, early = 0;
            for (int i = 0; i < previous.Length; i++)
            {
                int n = world.StaticContacts(previous[i], current[i], 0.02, contacts);
                contactHash.Add(n);
                for (int k = 0; k < Math.Min(n, contacts.Length); k++)
                {
                    contactHash.Add(contacts[k]);
                    early += contacts[k].Time < 1 ? 1 : 0;
                }
                touched += n > 0 ? 1 : 0;
            }
            cases.Add(("floor.contacts", Hex(contactHash.Value), true));
            Console.WriteLine($"  floor.contacts: {touched} of {previous.Length} capsules in contact, {early} contacts with Time < 1");

            Ray[] rays = Rays(GoldenCount);
            var hits = new RayHit[rays.Length];
            var hit = new RayHit[1];
            world.Raycast(rays, 5, hits);
            Hasher rayBatched = new Hasher(), rayOne = new Hasher();
            int ground = 0, probe = 0;
            for (int i = 0; i < rays.Length; i++)
            {
                rayBatched.Add(hits[i]);
                world.Raycast(rays.AsSpan(i, 1), 5, hit);
                rayOne.Add(hit[0]);
                if (hits[i].Distance < double.PositiveInfinity)
                {
                    ground += hits[i].Object == WorldIds.Ground ? 1 : 0;
                    probe += hits[i].Object == WorldIds.Ground ? 0 : 1;
                }
            }
            cases.Add(("floor.rays", Hex(rayBatched.Value), rayBatched.Value == rayOne.Value));
            Console.WriteLine($"  floor.rays: {ground} of {rays.Length} rays hit the floor, {probe} the probe's shapes");
            return cases;
        }

        private static string GoldenPath(string world)
        {
            // The project folder, found from the build output (bin/<config>/<tfm>/), so any working directory works.
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "worldbench.csproj")))
                dir = dir.Parent;
            if (dir == null)
                throw new DirectoryNotFoundException("worldbench.csproj not found above " + AppContext.BaseDirectory);
            return Path.Combine(dir.FullName, "golden", world + ".txt");
        }

        private static int Golden(bool record)
        {
#if DEBUG
            const string config = "Debug";
#else
            const string config = "Release";
#endif
            Console.WriteLine($"worldbench --golden ({config}, .NET {Environment.Version}, {RuntimeInformation.OSDescription}, {RuntimeInformation.ProcessArchitecture})");
            int failures = GoldenFile("floor", "the floor stub (p6-world-query-gallery 1.1)", FloorCases(), record)
                + GoldenFile("test-range", "the test range (p6-world-query-gallery 1.4, D-047) with a fixed stand", TestRangeCases(), record);
            if (record)
                return failures == 0 ? 0 : 1;
            Console.WriteLine(failures == 0 ? "worldbench: ALL PASS" : $"worldbench: {failures} FAILED");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>Checks one world's cases against golden/&lt;world&gt;.txt, or records them there; returns the failures.</summary>
        private static int GoldenFile(string world, string what, List<(string Name, string Value, bool Agree)> cases, bool record)
        {
            string path = GoldenPath(world);
            int failures = 0;
            foreach (var c in cases)
            {
                if (!c.Agree)
                {
                    Console.WriteLine($"  FAIL {c.Name}: batched and one-by-one results differ");
                    failures++;
                }
            }
            if (record)
            {
                if (failures > 0)
                    return failures;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var lines = new List<string>
                {
                    $"# worldbench golden: {what}. FNV-1a 64 over every result field's bits.",
                    "# Re-record only with a QueryVersion bump, audited case by case: dotnet run --project tools/worldbench -- --golden --record",
                };
                foreach (var c in cases)
                    lines.Add(c.Name + " " + c.Value);
                File.WriteAllText(path, string.Join("\n", lines) + "\n");
                Console.WriteLine($"worldbench: recorded {cases.Count} cases to {path}");
                return 0;
            }
            if (!File.Exists(path))
            {
                Console.WriteLine($"  FAIL no golden file at {path}");
                return failures + 1;
            }
            var expected = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;
                string[] parts = line.Split(' ');
                expected[parts[0]] = parts[1];
            }
            foreach (var c in cases)
            {
                bool match = expected.TryGetValue(c.Name, out string want) && want == c.Value;
                Console.WriteLine($"  {(match ? "PASS" : "FAIL")} {world}/{c.Name} {c.Value}{(match ? "" : " (golden " + (want ?? "missing") + ")")}");
                failures += match ? 0 : 1;
            }
            if (expected.Count != cases.Count)
            {
                Console.WriteLine($"  FAIL {world}: the golden file holds {expected.Count} cases, the run {cases.Count}");
                failures++;
            }
            return failures;
        }

        // ---- Bench -------------------------------------------------------------------------------------------------

        private static int Bench()
        {
            Console.WriteLine($"worldbench --bench: {Power()}");
            FloorWorld world = FloorWithProbe();

            // K§10.5's batch per physics step: 44 ground samples, 46 swept capsules, 8 rays, near the probe.
            XZ[] points = GroundPoints(44);
            Capsules(46, out Capsule[] previous, out Capsule[] current);
            Ray[] rays = Rays(8);
            var samples = new GroundSample[points.Length];
            var contacts = new StaticContact[16];
            var hits = new RayHit[rays.Length];
            const int steps = 20000;
            var times = new long[steps];
            for (int pass = 0; pass < 2; pass++) // the first pass warms the JIT
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int s = 0; s < steps; s++)
                {
                    long start = Stopwatch.GetTimestamp();
                    world.SampleGround(points, samples);
                    for (int i = 0; i < previous.Length; i++)
                        world.StaticContacts(previous[i], current[i], 0.02, contacts);
                    world.Raycast(rays, 5, hits);
                    times[s] = Stopwatch.GetTimestamp() - start;
                }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (pass == 0)
                    continue;
                Array.Sort(times);
                double us = 1e6 / Stopwatch.Frequency;
                Console.WriteLine($"  floor.step (44 + 46 + 8): median {times[steps / 2] * us:F2} us, p99 {times[steps * 99 / 100] * us:F2} us, "
                    + $"{allocated} B allocated over {steps} steps (budget 60 us, task 1.5 judges the gallery)");
                if (allocated != 0)
                {
                    Console.WriteLine("worldbench: FAIL, the queries allocated");
                    return 1;
                }
            }

            Capsules(100000, out previous, out current);
            rays = Rays(100000);
            hits = new RayHit[rays.Length];
            long a0 = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < previous.Length; i++)
                world.StaticContacts(previous[i], current[i], 0.02, contacts);
            long t1 = Stopwatch.GetTimestamp();
            world.Raycast(rays, 2, hits);
            long t2 = Stopwatch.GetTimestamp(), a1 = GC.GetAllocatedBytesForCurrentThread();
            double ms = 1e3 / Stopwatch.Frequency;
            Console.WriteLine($"  floor.capsules 100000: {(t1 - t0) * ms:F2} ms (budget 100 ms)");
            Console.WriteLine($"  floor.rays 100000 of <= 2 m: {(t2 - t1) * ms:F2} ms (budget 100 ms); {a1 - a0} B allocated by both");
            if (a1 - a0 != 0)
            {
                Console.WriteLine("worldbench: FAIL, the queries allocated");
                return 1;
            }
            return TestRangeBench();
        }

        // ---- Power state (PLT-07), as tools/hygiene/bench.py prints it ---------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemPowerStatus
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        private static extern int GetSystemPowerStatus(out SystemPowerStatus status);

        [DllImport("powrprof.dll")]
        private static extern uint PowerGetEffectiveOverlayScheme(out Guid overlay);

        private static string Power()
        {
            if (!OperatingSystem.IsWindows())
                return "power state unknown";
            string source = GetSystemPowerStatus(out SystemPowerStatus status) != 0
                ? status.ACLineStatus switch { 0 => "battery, not evidence (PLT-07)", 1 => "AC", _ => "unknown source" }
                : "unknown source";
            string plan = "unknown plan";
            try
            {
                using Process p = Process.Start(new ProcessStartInfo("powercfg", "/getactivescheme") { RedirectStandardOutput = true, UseShellExecute = false });
                string text = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();
                int open = text.IndexOf('('), close = text.LastIndexOf(')');
                if (open >= 0 && close > open)
                    plan = text.Substring(open + 1, close - open - 1);
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            string mode = "unknown mode";
            if (PowerGetEffectiveOverlayScheme(out Guid overlay) == 0)
            {
                mode = overlay.ToString() switch
                {
                    "961cc777-2547-4f9d-8174-7d86181b8a7a" => "best power efficiency",
                    "00000000-0000-0000-0000-000000000000" => "balanced",
                    "3af9b8d9-7c97-431d-ad78-34a8bfea439f" => "better performance",
                    "ded574b5-45a0-4f42-8737-46345c09c238" => "best performance",
                    _ => "unknown mode",
                };
            }
            return $"{source}, {plan} plan, {mode} mode";
        }
    }
}
