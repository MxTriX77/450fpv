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
    static readonly string[] PackageFiles =
        { "map.json", "height.r16", "surface.png", "cover.png", WorldQuery.HolesLayer, "objects.json" };

    /// Every surface and material against surfaces.json and catalog.json read independently (the README's defaults for
    /// material fields left out), the soil reference diameter (also of a world built in memory, which defaults to it),
    /// the terrain's NoMaterial (null, not an exception), and 10,000 lookups allocating nothing.
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

        // A terrain ray carries Catalog.NoMaterial: Material gives null for it, also without a catalog, and its surface is known.
        var down = new[] { new Ray(new Double3(-100, 50, 100), new Double3(0, -1, 0)) };
        var hit = new RayHit[1];
        world.Raycast(down, 100, hit);
        bool terrainMaterial = hit[0].Object == -1 && hit[0].Material == Catalog.NoMaterial && world.Material(hit[0].Material) == null
            && world.Surface(hit[0].Surface) != null && Uniform(parsed, parsed[0].Index, world.Seed).Material(Catalog.NoMaterial) == null;
        bad += terrainMaterial ? 0 : 1;

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
            + $"a terrain ray hits object {hit[0].Object}, material {hit[0].Material}, Material() null {world.Material(hit[0].Material) == null}, "
            + $"surface {world.Surface(hit[0].Surface)?.Id}; "
            + $"10000 lookups allocate {allocated} bytes");
    }

    /// The content hash (W-12). The loaded world's equals ContentHashOf and a second load's. A copy of the package and
    /// the tables in another folder, with CRLF line endings in the JSON files and stray files beside them, hashes the
    /// same. In memory, every single-byte change to a package file changes it: each byte of the files up to 16 KB set to
    /// CR, to LF and with bit 0 flipped (bits 7, all and 0 for the binary files), and the same at 257 evenly spread bytes
    /// of the larger files. The shared tables count only where this map uses them: a value changed in a surface it paints,
    /// in an asset it places or in a material that asset uses changes the hash, while an unrelated asset, an unused
    /// surface and reformatting the tables leave it alone (asset-pipeline, "New assets don't disturb existing maps").
    static bool ContentHashScenarios(WorldQuery world, string dir, string surfacesPath)
    {
        string catalogPath = ProjectSettings.GlobalizePath(CatalogPath);
        string[] layers = PackageFiles.Where(n => File.Exists(Path.Combine(dir, n))).ToArray();
        ulong hash = world.ContentHash, fromFiles = WorldQuery.ContentHashOf(dir, surfacesPath, catalogPath);
        ulong reloaded = WorldQuery.Load(dir, surfacesPath, catalogPath).ContentHash;

        // The same package and tables in another folder, with CRLF in the JSON files and files beside them that never count.
        string copy = Path.Combine(OS.GetUserDataDir(), "worldquery_hash_copy"), package = Path.Combine(copy, "another_name");
        if (Directory.Exists(copy))
            Directory.Delete(copy, true);
        Directory.CreateDirectory(package);
        string copiedSurfaces = Path.Combine(copy, "surfaces.json"), copiedCatalog = Path.Combine(copy, "catalog.json");
        foreach ((string from, string to) in layers.Select(n => (Path.Combine(dir, n), Path.Combine(package, n)))
            .Append((surfacesPath, copiedSurfaces)).Append((catalogPath, copiedCatalog)))
        {
            var bytes = new List<byte>(File.ReadAllBytes(from));
            if (IsJson(from))
            {
                for (int at = bytes.Count - 1; at >= 0; at--)
                {
                    if (bytes[at] == '\n' && (at == 0 || bytes[at - 1] != '\r'))
                        bytes.Insert(at, (byte)'\r');
                }
            }
            File.WriteAllBytes(to, bytes.ToArray());
        }
        File.WriteAllText(Path.Combine(package, "notes.txt"), "not a package file");
        File.Copy(Path.Combine(dir, "cover.png.import"), Path.Combine(package, "cover.png.import"));
        ulong elsewhere = WorldQuery.ContentHashOf(package, copiedSurfaces, copiedCatalog);

        // Every single-byte change to a package file changes the hash: the layers folded in order, then the tables' part,
        // which is held at its good value here (the layers' own framing is what this checks).
        byte[][] files = layers.Select(n => File.ReadAllBytes(Path.Combine(dir, n))).ToArray();
        bool[] text = layers.Select(IsJson).ToArray();
        string surfacesJson = File.ReadAllText(surfacesPath), catalogJson = File.ReadAllText(catalogPath);
        string objectsJson = File.ReadAllText(Path.Combine(dir, "objects.json"));
        byte[] surfaceLayer = MapPng.Read(Path.Combine(dir, "surface.png"), 1, out _, out _);
        ulong Rest(ulong h, int from)
        {
            for (int i = from; i < files.Length; i++)
                h = WorldQuery.HashFile(h, files[i], text[i]);
            return WorldQuery.HashTables(h, surfacesJson, catalogJson, objectsJson, surfaceLayer);
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
            perFile += $"{layers[k]} {tried - before} ({(every ? "every byte" : "257 bytes")}), ";
            prefix = WorldQuery.HashFile(prefix, f, text[k]);
        }

        // What the tables contribute: a used surface, a placed asset and a material it uses count; anything else does not.
        string scoped = "";
        int wrongScope = 0;
        void Scope(string what, bool expectChange, Action<JsonNode, JsonNode> edit)
        {
            JsonNode table = JsonNode.Parse(surfacesJson), catalog = JsonNode.Parse(catalogJson);
            edit(table, catalog);
            File.WriteAllText(copiedSurfaces, table.ToJsonString());
            File.WriteAllText(copiedCatalog, catalog.ToJsonString());
            bool changed = WorldQuery.ContentHashOf(package, copiedSurfaces, copiedCatalog) != elsewhere;
            wrongScope += changed == expectChange ? 0 : 1;
            scoped += $"{what} {(changed ? "changes" : "leaves")} it ({(expectChange ? "must change" : "must not")}); ";
        }
        static JsonNode Surface(JsonNode table, string id) => table["surfaces"].AsArray().First(s => (string)s["id"] == id);
        Scope("meadow_sod's bearing", true, (table, _) => Surface(table, "meadow_sod")["soil"]["bearing_n_per_m3"] = 1.1e7);
        Scope("house_box's size", true, (_, catalog) => catalog["assets"]["house_box"]["collision"][0]["size_m"][1] = 5.5);
        Scope("timber's friction", true, (_, catalog) => catalog["materials"]["timber"]["friction_static"] = 0.5);
        Scope("belt_bare's bearing (a filler's soil)", true,
            (table, _) => Surface(table, "belt_bare")["soil"]["bearing_n_per_m3"] = 2.2e7);
        Scope("an unplaced asset", false, (_, catalog) => catalog["assets"]["not_placed_here"] = JsonNode.Parse(
            "{ \"type\": \"object\", \"scene\": \"res://x\", \"material\": \"timber\", \"snag_hazard\": false, "
            + "\"wind_porosity\": 0.0, \"gaps\": [], \"collision\": [ { \"shape\": \"sphere\", \"radius_m\": 0.5 } ] }"));
        Scope("burnt_field's bearing (a surface the map does not paint)", false,
            (table, _) => Surface(table, "burnt_field")["soil"]["bearing_n_per_m3"] = 3.3e7);
        Scope("a new surface", false, (table, _) =>
        {
            JsonNode extra = JsonNode.Parse(Surface(table, "burnt_field").ToJsonString());
            extra["id"] = "not_used_here";
            extra["index"] = 200;
            table["surfaces"].AsArray().Add(extra);
        });
        Scope("reformatting both tables", false, (_, _) => { });
        Directory.Delete(copy, true);

        return Check("any change is detected", hash != 0 && fromFiles == hash && reloaded == hash && folded == hash
                && elsewhere == hash && unchanged == 0 && wrongScope == 0,
            $"content hash {hash:x16}: from the files alone {fromFiles:x16}, a second load {reloaded:x16}; a copy in another "
            + $"folder with CRLF JSON and stray files {elsewhere:x16}; {tried} single-byte changes to the layers in memory "
            + $"({perFile.TrimEnd(',', ' ')}): {unchanged} leave it unchanged; the tables' scope: {scoped}{wrongScope} wrong");
    }

    static bool IsJson(string path) => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
}
