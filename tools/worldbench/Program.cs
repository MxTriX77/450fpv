using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

/// The world-query benchmark (world-query spec, "Physics-grade performance"; define-map-format task 3.6):
///
///     dotnet run -c Release --project tools/worldbench
///
/// It loads game/maps/sample_patch with the launch rails at its example start, without Godot, as a headless replay does.
/// Each workload is warmed up until the JIT has optimised it, then timed over 21 runs, and the median is judged against
/// its budget. The managed bytes allocated across all timed runs of a workload must be 0. Prints one PASS/FAIL line per
/// budget and exits 1 on any failure.
static class Program
{
    const int Runs = 21;
    const string Package = "game/maps/sample_patch", SurfacesPath = "game/maps/surfaces.json", CatalogPath = "game/assets/catalog.json";
    /// sample_patch's example start (game/maps/README.md), where the game places the launch rails when legs are off.
    const double StartX = 12, StartZ = -30, StartYaw = 90;
    /// Contact margin of the capsule queries, m.
    const double Margin = 0.02;

    static bool _pass = true;

    static int Main()
    {
#if DEBUG
        Console.WriteLine("worldbench: this is a Debug build; the budgets are for Release: dotnet run -c Release --project tools/worldbench");
        return 2;
#else
        string root = FindRoot();
        GetSystemPowerStatus(out PowerStatus power);
        Console.WriteLine($"worldbench: Release build, {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}, "
            + $"{Environment.ProcessorCount} logical processors, power {(power.AcLineStatus == 1 ? "AC" : power.AcLineStatus == 0 ? "battery" : "unknown")} "
            + $"(battery {power.BatteryLifePercent} %), {Runs} timed runs per workload after a warm-up of at least 1 s");
        SurfaceParams[] table = SurfaceParams.ParseTable(File.ReadAllText(Path.Combine(root, SurfacesPath)));
        WorldQuery world = WorldQuery.Load(Path.Combine(root, Package), Path.Combine(root, SurfacesPath), Path.Combine(root, CatalogPath));
        var start = new Double3(StartX, Ground(world, StartX, StartZ).TerrainHeight, StartZ);
        world.AddObject("launch_rails", start, StartYaw);

        GroundSamples(world, table);
        MicroDetail(world, table);
        Objects(world, start);
        CompositeStep(world, start);
        Console.WriteLine($"worldbench: {(_pass ? "ALL PASS" : "FAILED")}");
        return _pass ? 0 : 1;
#endif
    }

    static string FindRoot()
    {
        foreach (string from in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (DirectoryInfo d = new(from); d != null; d = d.Parent)
            {
                if (File.Exists(Path.Combine(d.FullName, SurfacesPath)))
                    return d.FullName;
            }
        }
        throw new DirectoryNotFoundException($"no {SurfacesPath} above the current or the program directory");
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    static extern bool GetSystemPowerStatus(out PowerStatus status);

    /// Warms `body` up for at least 30 calls and 1 s, so the JIT has promoted it to its optimised tier, then times `Runs`
    /// calls. Returns each call's time in ms, sorted, and the managed bytes allocated across the timed calls.
    static double[] Time(Action body, out long allocated)
    {
        var warm = Stopwatch.StartNew();
        for (int i = 0; i < 30 || warm.ElapsedMilliseconds < 1000; i++)
            body();
        var ms = new double[Runs];
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < Runs; i++)
        {
            long t0 = Stopwatch.GetTimestamp();
            body();
            ms[i] = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Array.Sort(ms);
        return ms;
    }

    /// One budget line: the median of the runs, scaled per unit, against the budget, and no allocation.
    static void Judge(string name, double[] ms, double scale, string unit, double budget, long allocated, string detail)
    {
        double median = ms[Runs / 2] * scale;
        bool ok = median < budget && allocated == 0;
        _pass &= ok;
        Console.WriteLine($"worldbench: {name}: median {median:0.000} {unit} (min {ms[0] * scale:0.000}, max {ms[^1] * scale:0.000}), "
            + $"budget < {budget} {unit}; {allocated} B allocated in the timed runs; {detail}{(ok ? "PASS" : "FAIL")}");
    }

    static void Info(string name, double[] ms, double scale, string unit, long allocated, string detail) =>
        Console.WriteLine($"worldbench:   {name}: median {ms[Runs / 2] * scale:0.000} {unit} (min {ms[0] * scale:0.000}, max "
            + $"{ms[^1] * scale:0.000}); {allocated} B allocated; {detail}(information)");

    static GroundSample Ground(WorldQuery world, double x, double z)
    {
        var g = new GroundSample[1];
        world.SampleGround(new[] { new XZ(x, z) }, g);
        return g[0];
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

    /// 100,000 random points over sample_patch (judged), then over each surface's own uniform world.
    static void GroundSamples(WorldQuery world, SurfaceParams[] table)
    {
        var random = new Random(5);
        var points = new XZ[100000];
        for (int i = 0; i < points.Length; i++)
            points[i] = new XZ(-world.Half + random.NextDouble() * world.SizeM, -world.Half + random.NextDouble() * world.SizeM);
        var results = new GroundSample[points.Length];
        double[] ms = Time(() => world.SampleGround(points, results), out long allocated);
        Judge("100,000 SampleGround, random points over sample_patch", ms, 1, "ms", 10, allocated, "");
        foreach (SurfaceParams s in table)
        {
            WorldQuery uniform = Uniform(table, s.Index, world.Seed);
            ms = Time(() => uniform.SampleGround(points, results), out allocated);
            Info($"100,000 SampleGround on {s.Id} alone", ms, 1, "ms", allocated, "");
        }
    }

    /// MicroDetailNear, r = 2 m, all kinds, 0.3 m above the ground, at 20 centres 0.5 m apart (physics re-queries after
    /// 0.5 m), on each covered surface's uniform world at cover 1.0. The densest surface (most elements per m²) is judged.
    static void MicroDetail(WorldQuery world, SurfaceParams[] table)
    {
        var buffer = new MicroElement[40000];
        SurfaceParams densest = table.OrderByDescending(s => s.Cover.Sum(c => c?.Density ?? 0)).First();
        foreach (SurfaceParams s in table.OrderBy(s => s != densest))
        {
            if (s.Cover.All(c => c == null))
                continue;
            WorldQuery uniform = Uniform(table, s.Index, world.Seed);
            var centres = new Double3[20];
            for (int i = 0; i < centres.Length; i++)
            {
                double x = -4.63 + 0.5 * i, z = 3.21 - 0.1 * i;
                centres[i] = new Double3(x, Ground(uniform, x, z).GroundHeight + 0.3, z);
            }
            long elements = 0;
            double[] ms = Time(() =>
            {
                elements = 0;
                foreach (Double3 c in centres)
                    elements += uniform.MicroDetailNear(c, 2.0, KindMask.All, buffer);
            }, out long allocated);
            string detail = $"{elements / centres.Length} elements per query, {s.Cover.Sum(c => c?.Density ?? 0)} per m² at cover 1.0; ";
            if (s == densest)
                Judge($"MicroDetailNear r 2 m, all kinds, densest surface {s.Id}", ms, 1.0 / centres.Length, "ms", 0.25, allocated, detail);
            else
                Info($"MicroDetailNear r 2 m, all kinds, {s.Id}", ms, 1.0 / centres.Length, "ms", allocated, detail);
        }
    }

    /// 2000 capsules and 2000 rays around sample_patch's objects and the rails, as in the selftest: within 3 m of the
    /// house, shed, gate, a tree, a pole, the cable and the rails; up to 0.3 m long, radius 2–32 mm, moved up to 50 mm
    /// since the previous step; rays in random directions. 50 passes make 100,000 queries.
    static void Objects(WorldQuery world, Double3 start)
    {
        Double3[] near = { new(52, 3, 30), new(38, 1.5, 44), new(30.5, 1.5, 20), new(-40, 3, -50), new(-30, 4, 0), new(-15, 6.6, 0),
            new(start.X, start.Y + 0.3, start.Z) };
        var random = new Random(107);
        var previous = new Capsule[2000];
        var current = new Capsule[2000];
        var rays = new Ray[2000];
        var hits = new RayHit[2000];
        var buffer = new StaticContact[32];
        for (int i = 0; i < current.Length; i++)
        {
            Double3 at = near[i % near.Length] + RandomUnit(random) * (3 * random.NextDouble());
            Double3 arm = RandomUnit(random) * (0.3 * random.NextDouble()), move = RandomUnit(random) * (0.05 * random.NextDouble());
            double r = 0.002 + 0.03 * random.NextDouble();
            current[i] = new Capsule(at, at + arm, r);
            previous[i] = new Capsule(at - move, at + arm - move, r);
            rays[i] = new Ray(at, RandomUnit(random));
        }
        long contacts = 0;
        double[] ms = Time(() =>
        {
            contacts = 0;
            for (int k = 0; k < 50; k++)
            {
                for (int i = 0; i < current.Length; i++)
                    contacts += world.StaticContacts(previous[i], current[i], Margin, buffer);
            }
        }, out long allocated);
        Judge("100,000 swept capsule queries near sample_patch objects", ms, 1, "ms", 100, allocated,
            $"{contacts / 50.0 / current.Length:0.00} contacts per query; ");
        ms = Time(() =>
        {
            for (int k = 0; k < 50; k++)
            {
                for (int i = 0; i < current.Length; i++)
                    world.StaticContacts(current[i], Margin, buffer);
            }
        }, out allocated);
        Info("100,000 static capsule queries at the same poses", ms, 1, "ms", allocated, "");
        long hitCount = 0;
        ms = Time(() =>
        {
            hitCount = 0;
            for (int k = 0; k < 50; k++)
            {
                world.Raycast(rays, 2, hits);
                foreach (RayHit h in hits)
                    hitCount += h.Distance <= 2 ? 1 : 0;
            }
        }, out allocated);
        Judge("100,000 rays of 2 m near sample_patch objects", ms, 1, "ms", 100, allocated, $"{hitCount / 50.0 / rays.Length * 100:0.0} % hit; ");
    }

    static Double3 RandomUnit(Random random)
    {
        while (true)
        {
            var v = new Double3(2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1);
            double length = v.Length();
            if (length > 0.1 && length <= 1)
                return v * (1 / length);
        }
    }

    const int Feet = 4, Parts = 14, Fiber = 32, Capsules = Parts + Fiber, Samples = 44, Rotors = 4, Steps = 1000;

    /// The worst-case 1 kHz physics step of the physics review (§6.1): 44 ground samples, 46 swept capsules and 8 rays of
    /// 2 m. The drone, a 450 mm X-frame (README, launch_rails), rests on the launch rails at the example start, where
    /// every part is within the contact margin of a steel box: 4 arms 0.1 mm into the bars, 4 feet beside them, 4 prop-disk
    /// bounding spheres 50 mm above the bar tops, the body, the spool hanging between the bars, and 32 fiber segments
    /// paying out between the bars to the ground behind. Over 1000 steps it jitters ±1.5 mm vertically, yaws ±1° and
    /// slides 0.1 mm per step toward its nose, as at lift-off. The ground samples are the 4 feet, the 4 rotor centres, 4
    /// body points and the 32 fiber nodes; the rays go up and down the thrust axis from each rotor.
    static void CompositeStep(WorldQuery world, Double3 start)
    {
        Axes rails = Axes.FromEuler(StartYaw, 0, 0);
        Double3 ToWorld(Double3 local) => start + rails.ToWorld(local);
        // Rails asset space: origin on the ground at the rails' centre, bars along Z, nose toward −Z.
        var capsules = new Capsule[(Steps + 1) * Capsules];
        var points = new XZ[(Steps + 1) * Samples];
        var rays = new Ray[(Steps + 1) * 2 * Rotors];
        var fiber = new Double3[Fiber + 1];
        for (int i = 1; i <= Fiber; i++)
        {
            double z = 0.1 * i;
            fiber[i] = ToWorld(new Double3(0, z <= 0.6 ? 0.14 * (1 - z / 0.6) : 0, z));
            if (z > 0.6)
                fiber[i].Y = Ground(world, fiber[i].X, fiber[i].Z).GroundHeight + 0.00025;
        }
        for (int k = 0; k <= Steps; k++)
        {
            double jitter = 0.0015 * Math.Sin(2 * Math.PI * k / 37.0), yaw = Math.PI / 180 * Math.Sin(2 * Math.PI * k / 53.0);
            var offset = new Double3(0, 0.262 - 0.0001 + jitter, -0.0001 * k);
            double c = Math.Cos(yaw), s = Math.Sin(yaw);
            // Drone frame: origin on the arm axis at the frame centre.
            Double3 Pose(double x, double y, double z) => ToWorld(new Double3(x * c + z * s, y, z * c - x * s) + offset);
            int at = k * Capsules, p = k * Samples, r = k * 2 * Rotors;
            for (int m = 0; m < Rotors; m++)
            {
                double mx = m % 2 == 0 ? -0.159 : 0.159, mz = m < 2 ? -0.159 : 0.159;
                Double3 motor = Pose(mx, 0, mz), foot = Pose(mx, -0.09, mz);
                capsules[at + m] = new Capsule(Pose(0, 0, 0), motor, 0.012);                    // arm
                capsules[at + 4 + m] = new Capsule(Pose(mx, -0.012, mz), foot, 0.008);         // foot
                capsules[at + 8 + m] = new Capsule(Pose(mx, 0.038, mz), Pose(mx, 0.038, mz), 0.127); // prop disk
                points[p + m] = new XZ(foot.X, foot.Z);
                points[p + 4 + m] = new XZ(motor.X, motor.Z);
                rays[r + 2 * m] = new Ray(motor, new Double3(0, 1, 0));
                rays[r + 2 * m + 1] = new Ray(motor, new Double3(0, -1, 0));
            }
            capsules[at + 12] = new Capsule(Pose(0, 0.048, 0.12), Pose(0, 0.048, -0.12), 0.045); // body
            capsules[at + 13] = new Capsule(Pose(-0.05, -0.072, 0), Pose(0.05, -0.072, 0), 0.05); // spool
            Double3[] body = { Pose(0, 0.048, 0.12), Pose(0, 0.048, -0.12), Pose(0, -0.072, 0), Pose(0, 0, 0) };
            for (int i = 0; i < 4; i++)
                points[p + 8 + i] = new XZ(body[i].X, body[i].Z);
            // The fiber leaves the spool's bottom; its first nodes follow the drone.
            Double3 previous = Pose(0, -0.122, 0), moved = offset - new Double3(0, 0.262 - 0.0001, 0);
            for (int i = 1; i <= Fiber; i++)
            {
                double follow = Math.Max(0, 1 - i / 6.0);
                Double3 node = fiber[i] + rails.ToWorld(moved) * follow;
                capsules[at + Parts + i - 1] = new Capsule(previous, node, 0.00025);
                points[p + 12 + i - 1] = new XZ(node.X, node.Z);
                previous = node;
            }
        }
        var ground = new GroundSample[Samples];
        var hits = new RayHit[2 * Rotors];
        var buffer = new StaticContact[16];
        long contacts = 0;
        void Step(int k)
        {
            world.SampleGround(points.AsSpan(k * Samples, Samples), ground);
            for (int j = 0; j < Capsules; j++)
                contacts += world.StaticContacts(capsules[(k - 1) * Capsules + j], capsules[k * Capsules + j], Margin, buffer);
            world.Raycast(rays.AsSpan(k * 2 * Rotors, 2 * Rotors), 2, hits);
        }
        double[] ms = Time(() =>
        {
            contacts = 0;
            for (int k = 1; k <= Steps; k++)
                Step(k);
        }, out long allocated);
        // Single steps, for the spread (information).
        var single = new double[Steps];
        for (int k = 1; k <= Steps; k++)
        {
            long t0 = Stopwatch.GetTimestamp();
            Step(k);
            single[k - 1] = (Stopwatch.GetTimestamp() - t0) * 1e6 / Stopwatch.Frequency;
        }
        Array.Sort(single);
        Judge("composite worst-case physics step (44 ground samples + 46 swept capsules + 8 rays), drone on the launch rails", ms,
            1000.0 / Steps, "µs", 60, allocated, $"{contacts / (double)Steps:0.0} contacts per step; single steps p50 "
            + $"{single[Steps / 2]:0.0} µs, p99 {single[Steps * 99 / 100]:0.0} µs, max {single[^1]:0.0} µs; ");
    }
}
