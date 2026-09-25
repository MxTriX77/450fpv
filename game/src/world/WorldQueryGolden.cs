using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

/// The world-query determinism checks (world-query spec, "Determinism and threading"; W-13, W-14). Pure C#, so the Godot
/// selftest (`-- --selftest worldquery`, Debug) and `tools/worldbench -- --golden` (Debug and Release, no Godot) run the
/// same code:
/// - Golden file: the fixed inputs of worldquery_golden.json give the output hashes recorded there, on sample_patch with
///   the launch rails and on a flat world of each surface. A hash is FNV-1a 64 over every field of every result in
///   order (see Fnv), so struct padding never counts. The file also records sample_patch's content hash, so a changed
///   world reads as that and not as changed math, and WorldQuery.QueryVersion, which must be raised when the math changes.
/// - Batched vs single-point: each ground and ray batch equals its points and rays queried one at a time, and every
///   micro-detail query equals its WorldQuery.ScalarReference run (no 4-wide pass, no per-cell ground cache), bit for bit.
/// - Concurrent use: two threads run the sample_patch cases on one world at the same time; every result equals the
///   single-threaded one.
public static class WorldQueryGolden
{
    /// The golden file, in the Godot project folder.
    public const string GoldenFile = "src/world/worldquery_golden.json";
    const string SamplePatch = "sample_patch", Flat = "flat:";

    /// Checks every case of the golden file and prints one line per case through `check` (scenario, pass, numbers).
    /// With `record`, writes the counts and hashes computed here into the file instead, after an intended change to the
    /// world data or the query math. It still refuses when a batched result differs from its single-point reference, and
    /// when results changed on the same world data while QueryVersion is still the recorded one.
    public static bool Golden(string gameDir, Func<string, bool, string, bool> check, bool record = false)
    {
        string path = Path.Combine(gameDir, GoldenFile);
        JsonNode golden = JsonNode.Parse(File.ReadAllText(path));
        var worlds = new Worlds(gameDir, golden);
        ulong content = worlds.Get(SamplePatch).ContentHash;
        bool sameData = Hex(content) == (string)golden["content_hash"];
        int version = (int?)golden["query_version"] ?? 0;
        bool pass = record || check("golden: world files as recorded", sameData,
            $"sample_patch content hash {content:x16}, recorded {golden["content_hash"]} (a difference means the world data changed "
            + "since recording, not the math)");
        pass &= record || check("golden: query math version as recorded", version == WorldQuery.QueryVersion,
            $"WorldQuery.QueryVersion {WorldQuery.QueryVersion}, recorded {version} (a difference means the file was not re-recorded "
            + "after the math changed)");
        var buffers = new Buffers();
        bool changed = false;
        foreach (Case c in Cases(golden))
        {
            WorldQuery world = worlds.Get(c.World);
            (long count, ulong hash) = Run(world, c, buffers);
            ulong? single = Single(world, c, buffers);
            string singleText = single == null ? "no batched form" : single == hash ? "single-point identical" : $"single-point {single:x16} DIFFERS";
            if (record)
            {
                changed |= Hex(hash) != c.Hash || count != c.Count;
                c.Node["count"] = count;
                c.Node["hash"] = Hex(hash);
                pass &= check($"golden record: {c.Name}", single == null || single == hash, $"{count} results, hash {hash:x16}; {singleText}");
                continue;
            }
            pass &= check($"golden: {c.Name}", Hex(hash) == c.Hash && count == c.Count && (single == null || single == hash),
                $"{c.Call} on {c.World}, {c.Inputs} inputs: {count} results (recorded {c.Count}), hash {hash:x16} (recorded {c.Hash}); {singleText}");
        }
        if (record)
        {
            // Results that changed on the same world data mean the math changed: an old log must not replay under it.
            pass &= check("golden record: query math version", !(changed && sameData && WorldQuery.QueryVersion == version),
                $"results {(changed ? "changed" : "unchanged")} on {(sameData ? "the same" : "changed")} world data; WorldQuery.QueryVersion "
                + $"{WorldQuery.QueryVersion}, recorded {version} (raise it when the math changes the results)");
        }
        if (record && pass)
        {
            golden["content_hash"] = Hex(content);
            golden["query_version"] = WorldQuery.QueryVersion;
            Write(path, golden);
        }
        return pass;
    }

    /// World-query "Concurrent use" (W-14): a physics thread runs every sample_patch case of the golden file in turn, and
    /// the calling thread (in Godot the main thread, where the renderer draws micro-detail from the generator) runs the
    /// micro-detail and ground cases in reverse order, on one world at the same time for `seconds`, each thread with its
    /// own buffers. Every result must equal the single-threaded result computed before the threads start.
    public static bool Concurrent(string gameDir, double seconds, Func<string, bool, string, bool> check)
    {
        JsonNode golden = JsonNode.Parse(File.ReadAllText(Path.Combine(gameDir, GoldenFile)));
        WorldQuery world = new Worlds(gameDir, golden).Get(SamplePatch);
        Case[] physicsCases = Cases(golden).Where(c => c.World == SamplePatch).ToArray();
        var reference = new Buffers();
        foreach (Case c in physicsCases)
            c.Reference = Run(world, c, reference).Hash;
        Case[] renderCases = physicsCases.Where(c => c.Call is "MicroDetailNear" or "SampleGround").Reverse().ToArray();

        var clock = new Stopwatch();
        (long Runs, long Differ) Loop(Case[] cases)
        {
            var buffers = new Buffers();
            long runs = 0, differ = 0;
            for (int i = 0; clock.Elapsed.TotalSeconds < seconds; i = (i + 1) % cases.Length, runs++)
                differ += Run(world, cases[i], buffers).Hash == cases[i].Reference ? 0 : 1;
            return (runs, differ);
        }
        (long Runs, long Differ) physics = default;
        Exception failure = null;
        using var start = new Barrier(2, _ => clock.Start());
        var thread = new Thread(() =>
        {
            try
            {
                start.SignalAndWait();
                physics = Loop(physicsCases);
            }
            catch (Exception e)
            {
                failure = e;
            }
        }) { Name = "physics", IsBackground = true };
        thread.Start();
        start.SignalAndWait();
        (long Runs, long Differ) main = Loop(renderCases);
        thread.Join();
        return check("concurrent use", failure == null && physics.Runs > 0 && main.Runs > 0 && physics.Differ + main.Differ == 0,
            $"{clock.Elapsed.TotalSeconds:0.0} s on one sample_patch world: a physics thread ran {physics.Runs} cases "
            + $"({physicsCases.Length} in turn: ground, micro-detail, contacts, rays, gaps, wind grid), {physics.Differ} differing from "
            + $"the single-threaded results; the calling thread ran {main.Runs} ({renderCases.Length} micro-detail and ground cases in "
            + $"reverse), {main.Differ} differing{(failure == null ? "" : $"; the physics thread threw {failure}")}");
    }

    static string Hex(ulong v) => v.ToString("x16");

    // ---------------------------------------------------------------- cases

    /// One case of the golden file: a call on a world with its inputs.
    sealed class Case
    {
        public JsonNode Node;
        public string Name, World, Call, Hash;
        public long Count, Inputs;
        public XZ[] Points;                                                  // SampleGround
        public (Double3 Centre, double Radius, KindMask Kinds)[] Queries;     // MicroDetailNear, GapsNear
        public Capsule[] Previous, Current;                                  // StaticContacts
        public Ray[] Rays;                                                   // Raycast
        public double Limit;                                                 // StaticContacts margin, Raycast max distance
        public ulong Reference;                                              // Concurrent: the single-threaded hash
    }

    /// A number of the file: JSON numbers, and "NaN", "Infinity" and "-Infinity" as strings.
    static double Number(JsonNode n) =>
        n.GetValueKind() == JsonValueKind.String ? double.Parse(n.GetValue<string>(), CultureInfo.InvariantCulture) : n.GetValue<double>();

    static double[][] Rows(JsonNode list) => list.AsArray().Select(row => row.AsArray().Select(Number).ToArray()).ToArray();

    static IEnumerable<Case> Cases(JsonNode golden)
    {
        foreach (JsonNode n in golden["cases"].AsArray())
        {
            var c = new Case
            {
                Node = n, Name = (string)n["name"], World = (string)n["world"], Call = (string)n["call"],
                Hash = (string)n["hash"], Count = (long)n["count"],
            };
            double[][] rows = null;
            switch (c.Call)
            {
                case "SampleGround":
                    rows = Rows(n["points"]);
                    c.Points = rows.Select(r => new XZ(r[0], r[1])).ToArray();
                    break;
                case "MicroDetailNear":
                case "GapsNear":
                    rows = Rows(n["queries"]);
                    c.Queries = rows.Select(r => (new Double3(r[0], r[1], r[2]), r[3], r.Length > 4 ? (KindMask)(int)r[4] : KindMask.None)).ToArray();
                    break;
                case "StaticContacts":
                    rows = Rows(n["capsules"]);
                    c.Limit = Number(n["margin"]);
                    c.Previous = rows.Select(r => new Capsule(new Double3(r[0], r[1], r[2]), new Double3(r[3], r[4], r[5]), r[12])).ToArray();
                    c.Current = rows.Select(r => new Capsule(new Double3(r[6], r[7], r[8]), new Double3(r[9], r[10], r[11]), r[12])).ToArray();
                    break;
                case "Raycast":
                    rows = Rows(n["rays"]);
                    c.Limit = Number(n["max_distance"]);
                    c.Rays = rows.Select(r => new Ray(new Double3(r[0], r[1], r[2]), new Double3(r[3], r[4], r[5]))).ToArray();
                    break;
                case "WindGrid":
                    break;
                default:
                    throw new InvalidDataException($"golden case '{c.Name}': unknown call '{c.Call}'");
            }
            c.Inputs = rows?.Length ?? 0;
            yield return c;
        }
    }

    /// The worlds of the file, built once: sample_patch with the file's `rails` placed, and "flat:<surface id>", a flat
    /// 256 m world of that surface with every cover channel at 255 and sample_patch's seed.
    sealed class Worlds
    {
        readonly Dictionary<string, WorldQuery> _built = new();
        readonly string _game;
        readonly JsonNode _golden;

        public Worlds(string gameDir, JsonNode golden) => (_game, _golden) = (gameDir, golden);

        public WorldQuery Get(string name)
        {
            if (_built.TryGetValue(name, out WorldQuery world))
                return world;
            string surfaces = Path.Combine(_game, "maps", "surfaces.json");
            if (name == SamplePatch)
            {
                world = WorldQuery.Load(Path.Combine(_game, "maps", SamplePatch), surfaces, Path.Combine(_game, "assets", "catalog.json"));
                JsonNode rails = _golden["rails"];
                double[] at = rails["position_m"].AsArray().Select(Number).ToArray();
                world.AddObject((string)rails["asset"], new Double3(at[0], at[1], at[2]), Number(rails["yaw_deg"]));
            }
            else
            {
                const int samples = 257, cells = 512;
                SurfaceParams[] table = SurfaceParams.ParseTable(File.ReadAllText(surfaces));
                var surface = new byte[cells * cells];
                Array.Fill(surface, table.Single(s => s.Id == name[Flat.Length..]).Index);
                var cover = new byte[cells * cells * 4];
                Array.Fill(cover, (byte)255);
                world = new WorldQuery(table, 256, Get(SamplePatch).Seed, samples, new float[samples * samples], cells, surface, cover);
            }
            return _built[name] = world;
        }
    }

    /// Result buffers, one set per thread.
    sealed class Buffers
    {
        public readonly GroundSample[] Ground = new GroundSample[4096];
        public readonly MicroElement[] Elements = new MicroElement[65536];
        public readonly StaticContact[] Contacts = new StaticContact[64];
        public readonly RayHit[] Hits = new RayHit[4096];
        public readonly Gap[] Gaps = new Gap[16];
    }

    /// Runs a case: the number of results (ground samples, elements, contacts, ray hits, gaps, wind cells with an
    /// obstacle) and the hash of every result in order, each variable-length list preceded by its true count.
    static (long Count, ulong Hash) Run(WorldQuery world, Case c, Buffers b)
    {
        var h = Fnv.Start;
        long count = 0;
        switch (c.Call)
        {
            case "SampleGround":
                world.SampleGround(c.Points, b.Ground.AsSpan(0, c.Points.Length));
                foreach (ref readonly GroundSample g in b.Ground.AsSpan(0, c.Points.Length))
                    Add(ref h, g);
                count = c.Points.Length;
                break;
            case "MicroDetailNear":
                foreach (var q in c.Queries)
                {
                    int n = world.MicroDetailNear(q.Centre, q.Radius, q.Kinds, b.Elements);
                    h.U((ulong)n);
                    foreach (ref readonly MicroElement e in b.Elements.AsSpan(0, Math.Min(n, b.Elements.Length)))
                        Add(ref h, e);
                    count += n;
                }
                break;
            case "StaticContacts":
                for (int i = 0; i < c.Current.Length; i++)
                {
                    int n = world.StaticContacts(c.Previous[i], c.Current[i], c.Limit, b.Contacts);
                    h.U((ulong)n);
                    foreach (ref readonly StaticContact contact in b.Contacts.AsSpan(0, Math.Min(n, b.Contacts.Length)))
                        Add(ref h, contact);
                    count += n;
                }
                break;
            case "Raycast":
                world.Raycast(c.Rays, c.Limit, b.Hits.AsSpan(0, c.Rays.Length));
                foreach (ref readonly RayHit hit in b.Hits.AsSpan(0, c.Rays.Length))
                {
                    Add(ref h, hit);
                    count += hit.Distance <= c.Limit ? 1 : 0;
                }
                break;
            case "GapsNear":
                foreach (var q in c.Queries)
                {
                    int n = world.GapsNear(q.Centre, q.Radius, b.Gaps);
                    h.U((ulong)n);
                    foreach (ref readonly Gap gap in b.Gaps.AsSpan(0, Math.Min(n, b.Gaps.Length)))
                        Add(ref h, gap);
                    count += n;
                }
                break;
            default: // WindGrid
                foreach (ref readonly WindCell cell in world.WindGrid)
                {
                    h.F(cell.TopM);
                    h.F(cell.BaseM);
                    h.F(cell.Porosity);
                    count += cell.Porosity < 1 ? 1 : 0;
                }
                break;
        }
        return (count, h.Value);
    }

    /// The hash of a batched case recomputed as its single-point reference: ground points and rays one at a time, and
    /// micro-detail with WorldQuery.ScalarReference. Null for calls without a batched form.
    static ulong? Single(WorldQuery world, Case c, Buffers b)
    {
        var h = Fnv.Start;
        switch (c.Call)
        {
            case "SampleGround":
                for (int i = 0; i < c.Points.Length; i++)
                {
                    world.SampleGround(c.Points.AsSpan(i, 1), b.Ground.AsSpan(0, 1));
                    Add(ref h, b.Ground[0]);
                }
                return h.Value;
            case "Raycast":
                for (int i = 0; i < c.Rays.Length; i++)
                {
                    world.Raycast(c.Rays.AsSpan(i, 1), c.Limit, b.Hits.AsSpan(0, 1));
                    Add(ref h, b.Hits[0]);
                }
                return h.Value;
            case "MicroDetailNear":
                world.ScalarReference = true;
                try
                {
                    return Run(world, c, b).Hash;
                }
                finally
                {
                    world.ScalarReference = false;
                }
            default:
                return null;
        }
    }

    // ---------------------------------------------------------------- output hashes

    /// FNV-1a 64 over values, each as 8 bytes little-endian: floating-point values by their IEEE bits (a float's 32 bits
    /// zero-extended), integers and enums as unsigned 64-bit values (a negative int sign-extended first).
    struct Fnv
    {
        public ulong Value;

        public static Fnv Start => new() { Value = WorldQuery.FnvOffset };

        public void U(ulong v)
        {
            for (int i = 0; i < 8; i++)
                Value = (Value ^ (byte)(v >> 8 * i)) * 0x100000001B3UL;
        }

        public void F(double v) => U(BitConverter.DoubleToUInt64Bits(v));
        public void F(float v) => U(BitConverter.SingleToUInt32Bits(v));

        public void F(Double3 v)
        {
            F(v.X);
            F(v.Y);
            F(v.Z);
        }

        public void F(Vector3 v)
        {
            F(v.X);
            F(v.Y);
            F(v.Z);
        }

        public void F(Vector4 v)
        {
            F(v.X);
            F(v.Y);
            F(v.Z);
            F(v.W);
        }
    }

    static void Add(ref Fnv h, in GroundSample g)
    {
        h.F(g.TerrainHeight);
        h.F(g.GroundHeight);
        h.F(g.SupportTop);
        h.F(g.Normal);
        h.F(g.MatDepth);
        h.F(g.CoverDensity);
        h.F(g.BlendWeight);
        for (int i = 0; i < 4; i++)
            h.U(g.BlendSurface[i]);
        h.U(g.Surface);
        h.U((ulong)g.Feature);
        h.U((ulong)g.Flags);
        h.U(g.FeatureId);
        h.F(g.FeatureDepth);
    }

    static void Add(ref Fnv h, in MicroElement e)
    {
        h.U(e.Id);
        h.F(e.Base);
        h.F(e.Direction);
        h.F(e.Length);
        h.F(e.Diameter);
        h.F(e.TipStiffness);
        h.F(e.HookRelease);
        h.U((ulong)e.Kind);
        h.U(e.Surface);
        h.U(e.Hooks ? 1UL : 0UL);
    }

    static void Add(ref Fnv h, in StaticContact c)
    {
        h.F(c.Point);
        h.F(c.Normal);
        h.F(c.Distance);
        h.U((ulong)(long)c.Object);
        h.U(c.Shape);
        h.U(c.Material);
        h.F(c.WireParam);
        h.F(c.Time);
    }

    static void Add(ref Fnv h, in RayHit r)
    {
        h.F(r.Distance);
        h.F(r.Point);
        h.F(r.Normal);
        h.U((ulong)(long)r.Object);
        h.U(r.Material);
        h.U(r.Surface);
    }

    static void Add(ref Fnv h, in Gap g)
    {
        h.F(g.Center);
        h.F(g.Normal);
        h.F(g.Up);
        h.F(g.Width);
        h.F(g.Height);
        h.U((ulong)(long)g.Object);
        h.U((ulong)g.Name.Length);
        foreach (char ch in g.Name)
            h.U(ch);
    }

    // ---------------------------------------------------------------- recording

    /// Writes the golden file with one case per line, so a re-record shows as one changed line per changed case. The
    /// inputs keep their text: numbers are written back as they were read.
    static void Write(string path, JsonNode golden)
    {
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var parts = new List<string>();
        foreach (KeyValuePair<string, JsonNode> p in golden.AsObject())
        {
            string value = p.Key != "cases" ? p.Value.ToJsonString(options)
                : "[\n    " + string.Join(",\n    ", p.Value.AsArray().Select(c => c.ToJsonString(options))) + "\n  ]";
            parts.Add($"  {JsonSerializer.Serialize(p.Key)}: {value}");
        }
        File.WriteAllText(path, "{\n" + string.Join(",\n", parts) + "\n}\n", new UTF8Encoding(false));
    }
}
