using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;

/// The lookup and content-hash scenarios of `-- --selftest worldquery` (task 3.5, W-12). The references are read here
/// from the JSON files with JsonNode, not taken from the code under test.
public static partial class WorldQuerySelfTest
{
    /// The files the content hash covers, in its order, in the package folder and then the shared tables.
    static readonly string[] PackageFiles = { "map.json", "height.r16", "surface.png", "cover.png", "objects.json" };

    /// Every surface and material against surfaces.json and catalog.json read independently (the README's defaults for
    /// material fields left out), the soil reference diameter (also of a world built in memory, which defaults to it),
    /// and 10,000 lookups allocating nothing.
    static bool Lookups(WorldQuery world, string surfacesPath)
    {
        JsonNode table = JsonNode.Parse(File.ReadAllText(surfacesPath));
        JsonNode catalog = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(CatalogPath)));
        int values = 0, bad = 0;
        string first = "";
        void Expect(string what, double got, double expected)
        {
            values++;
            if (got != expected && bad++ == 0)
                first = $"; first: {what} {got} vs {expected}";
        }
        double D(JsonNode n) => n.GetValue<double>();
        int surfaces = 0, mats = 0;
        foreach (JsonNode s in table["surfaces"].AsArray())
        {
            SurfaceParams p = world.Surface((byte)s["index"].GetValue<int>());
            string id = s["id"].GetValue<string>();
            JsonNode soil = s["soil"];
            if (p == null || p.Id != id)
            {
                bad++;
                continue;
            }
            surfaces++;
            Expect($"{id} bearing", p.Bearing, D(soil["bearing_n_per_m3"]));
            Expect($"{id} damping", p.Damping, D(soil["damping_ns_per_m3"]));
            Expect($"{id} static friction", p.FrictionStatic, D(soil["friction_static"]));
            Expect($"{id} kinetic friction", p.FrictionKinetic, D(soil["friction_kinetic"]));
            Expect($"{id} max sink", p.MaxSink, D(soil["max_sink_m"]));
            Expect($"{id} unload ratio", p.UnloadRatio, D(soil["unload_stiffness_ratio"]));
            Expect($"{id} dust", p.DustEmission, D(s["dust_emission"]));
            foreach (JsonNode c in s["cover"].AsArray())
            {
                JsonNode mat = c["mat"];
                CoverParams cp = p.Cover[(int)Enum.Parse<CoverKind>(c["type"].GetValue<string>(), true)];
                if (mat == null)
                {
                    bad += cp.HasMat ? 1 : 0;
                    continue;
                }
                mats++;
                Expect($"{id} mat modulus", cp.MatModulus, D(mat["modulus_pa"]));
                Expect($"{id} mat damping", cp.MatDampingRatio, D(mat["damping_ratio"]));
                Expect($"{id} mat static friction", cp.MatFrictionStatic, D(mat["friction_static"]));
                Expect($"{id} mat kinetic friction", cp.MatFrictionKinetic, D(mat["friction_kinetic"]));
            }
        }
        int materials = 0;
        foreach (KeyValuePair<string, JsonNode> m in catalog["materials"].AsObject())
        {
            MaterialParams p = world.Material(world.Catalog.MaterialId(m.Key));
            materials++;
            Expect($"{m.Key} static friction", p.FrictionStatic, D(m.Value["friction_static"]));
            Expect($"{m.Key} kinetic friction", p.FrictionKinetic, D(m.Value["friction_kinetic"]));
            Expect($"{m.Key} stiffness", p.Stiffness, m.Value["stiffness_n_per_m"] is JsonNode k ? D(k) : double.PositiveInfinity);
            Expect($"{m.Key} damping", p.DampingRatio, m.Value["damping_ratio"] is JsonNode d ? D(d) : 0.05);
            Expect($"{m.Key} edge radius", p.EdgeRadius, m.Value["edge_radius_m"] is JsonNode e ? D(e) : 0.002);
        }
        Expect("soil reference diameter", world.SoilReferenceDiameter, D(table["soil_reference_diameter_m"]));
        SurfaceParams[] parsed = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        double inMemory = Uniform(parsed, parsed[0].Index, world.Seed).SoilReferenceDiameter;
        Expect("soil reference diameter of a world built in memory", inMemory, D(table["soil_reference_diameter_m"]));

        byte[] indices = world.Surfaces.Select(s => s.Index).ToArray();
        int materialCount = world.Catalog.Materials.Length;
        double sum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
            sum += world.Surface(indices[i % indices.Length]).Bearing + world.Material((ushort)(i % materialCount)).FrictionStatic + world.SoilReferenceDiameter;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return Check("surface, material and soil-reference lookups",
            bad == 0 && surfaces == table["surfaces"].AsArray().Count && materials > 0 && allocated == 0 && sum > 0,
            $"{surfaces} surfaces ({mats} with a mat), {materials} materials and the soil reference diameter "
            + $"{world.SoilReferenceDiameter} m (in memory {inMemory} m): {values} values vs surfaces.json and catalog.json, {bad} wrong{first}; "
            + $"10000 lookups allocate {allocated} bytes");
    }

    /// The content hash (W-12). The loaded world's equals ContentHashOf and a second load's. A copy of the seven files in
    /// another folder, with CRLF line endings in the JSON files and stray files beside them, hashes the same. One byte
    /// changed on disk in each copied file changes it. In memory, every single-byte change tried changes it: each byte of
    /// the files up to 16 KB set to CR, to LF and with bit 0 flipped (bits 7, all and 0 for the binary files), and the
    /// same at 257 evenly spread bytes of the larger files.
    static bool ContentHashScenarios(WorldQuery world, string dir, string surfacesPath)
    {
        string[] paths = PackageFiles.Select(n => Path.Combine(dir, n)).Append(surfacesPath)
            .Append(ProjectSettings.GlobalizePath(CatalogPath)).ToArray();
        ulong hash = world.ContentHash, fromFiles = WorldQuery.ContentHashOf(dir, paths[5], paths[6]);
        ulong reloaded = WorldQuery.Load(dir, paths[5], paths[6]).ContentHash;

        string copy = Path.Combine(OS.GetUserDataDir(), "worldquery_hash_copy"), package = Path.Combine(copy, "another_name");
        if (Directory.Exists(copy))
            Directory.Delete(copy, true);
        Directory.CreateDirectory(package);
        string[] copied = PackageFiles.Select(n => Path.Combine(package, n)).Append(Path.Combine(copy, "surfaces.json"))
            .Append(Path.Combine(copy, "catalog.json")).ToArray();
        for (int i = 0; i < paths.Length; i++)
        {
            var bytes = new List<byte>(File.ReadAllBytes(paths[i]));
            if (IsJson(paths[i]))
            {
                for (int at = bytes.Count - 1; at >= 0; at--)
                {
                    if (bytes[at] == '\n' && (at == 0 || bytes[at - 1] != '\r'))
                        bytes.Insert(at, (byte)'\r');
                }
            }
            File.WriteAllBytes(copied[i], bytes.ToArray());
        }
        File.WriteAllText(Path.Combine(package, "notes.txt"), "not a package file");
        File.Copy(Path.Combine(dir, "cover.png.import"), Path.Combine(package, "cover.png.import"));
        ulong elsewhere = WorldQuery.ContentHashOf(package, copied[5], copied[6]);
        int changedOnDisk = 0;
        foreach (string path in copied)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bytes[bytes.Length / 2] ^= 1;
            File.WriteAllBytes(path, bytes);
            changedOnDisk += WorldQuery.ContentHashOf(package, copied[5], copied[6]) != elsewhere ? 1 : 0;
            bytes[bytes.Length / 2] ^= 1;
            File.WriteAllBytes(path, bytes);
        }

        byte[][] files = paths.Select(File.ReadAllBytes).ToArray();
        bool[] text = paths.Select(IsJson).ToArray();
        ulong Rest(ulong h, int from)
        {
            for (int i = from; i < files.Length; i++)
                h = WorldQuery.HashFile(h, files[i], text[i]);
            return h;
        }
        ulong folded = Rest(WorldQuery.FnvOffset, 0), prefix = WorldQuery.FnvOffset;
        long tried = 0, unchanged = 0;
        string perFile = "";
        for (int k = 0; k < files.Length; k++)
        {
            byte[] f = files[k];
            bool every = f.Length <= 16384;
            long before = tried;
            for (int j = 0, positions = every ? f.Length : 257; j < positions; j++)
            {
                int at = every ? j : (int)((long)j * (f.Length - 1) / 256);
                byte old = f[at];
                for (int v = 0; v < 3; v++)
                {
                    f[at] = (text[k], v) switch
                    {
                        (true, 0) => (byte)'\r',
                        (true, 1) => (byte)'\n',
                        (false, 0) => (byte)(old ^ 0x80),
                        (false, 1) => (byte)~old,
                        _ => (byte)(old ^ 1),
                    };
                    if (f[at] == old)
                        continue;
                    tried++;
                    unchanged += Rest(WorldQuery.HashFile(prefix, f, text[k]), k + 1) == folded ? 1 : 0;
                }
                f[at] = old;
            }
            perFile += $"{Path.GetFileName(paths[k])} {tried - before} ({(every ? "every byte" : "257 bytes")}), ";
            prefix = WorldQuery.HashFile(prefix, f, text[k]);
        }
        return Check("any change is detected", hash != 0 && fromFiles == hash && reloaded == hash && folded == hash && elsewhere == hash
                && changedOnDisk == paths.Length && unchanged == 0,
            $"content hash {hash:x16}: from the files alone {fromFiles:x16}, a second load {reloaded:x16}; a copy in another folder "
            + $"with CRLF JSON and stray files {elsewhere:x16}; one byte changed on disk changes it in {changedOnDisk} of "
            + $"{paths.Length} files; {tried} single-byte changes in memory ({perFile.TrimEnd(',', ' ')}): {unchanged} leave it unchanged");
    }

    static bool IsJson(string path) => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}
