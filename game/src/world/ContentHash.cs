using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

/// World-query "Content hash" (W-12): a 64-bit hash of the data a world is built from. The physics log records it, so a
/// replay can refuse to run on a mismatched world. The rule (game/maps/README.md, Content hash):
/// - The package's own files, in this order: map.json, height.r16, surface.png, cover.png, holes.png (when it exists)
///   and objects.json. Each adds its bytes, then their count as 8 bytes little-endian. In the JSON files every CR LF
///   pair counts as a single LF (and the count is after that), so CRLF and LF checkouts hash the same.
/// - Then the parts of the shared tables this map uses, each as canonical JSON (keys sorted, no whitespace, every
///   scalar's text as the file has it) hashed the same way: the soil reference diameter, the surfaces the map uses, the
///   catalog entries its objects name, and the materials those entries use with their ids. Adding an asset or a surface
///   a map does not use therefore leaves that map's hash alone (asset-pipeline, "New assets don't disturb existing maps").
/// Every FNV-1a step is a bijection of the state, so two streams of the same length that differ in one byte always hash
/// differently: a change to one byte of a binary layer, or to a JSON byte that neither is nor becomes a CR or LF, always
/// changes the hash. Any other change changes it with probability 1 − 2⁻⁶⁴.
public sealed partial class WorldQuery
{
    internal const ulong FnvOffset = 0xCBF29CE484222325UL;
    const ulong FnvPrime = 0x100000001B3UL;

    /// The optional hole layer of map format 1.1 (game/maps/README.md).
    public const string HolesLayer = "holes.png";

    /// The package files the hash covers, in its order. holes.png is optional; every other one is required.
    static readonly string[] HashedFiles = { "map.json", "height.r16", "surface.png", "cover.png", HolesLayer, "objects.json" };

    /// The version of the query math. The content hash covers only the data, so this is raised with every code change
    /// that alters any query result for the same data, and the golden file is re-recorded with it. The physics log
    /// records it next to ContentHash, and a replay refuses a log whose version differs. 1: the math of task 3.5.
    /// 2: lying elements lie straight from the mat top at the root to the mat top at the tip (API review F1).
    /// 3: terrain holes (map format 1.1): the Hole flag and its sample values, rays that pass over a hole cell, no
    /// micro-detail in one, and Surface on contacts and ray hits.
    public const int QueryVersion = 3;

    /// The content hash of the world this instance was loaded from (ContentHashOf); 0 for a world built in memory.
    public ulong ContentHash { get; private set; }

    /// The content hash of a map package with the shared tables, from the files alone: a replay can check it before
    /// loading anything.
    public static ulong ContentHashOf(string packageDir, string surfacesPath, string catalogPath)
    {
        ulong hash = FnvOffset;
        foreach (string name in HashedFiles)
        {
            string path = Path.Combine(packageDir, name);
            if (name == HolesLayer && !File.Exists(path))
                continue;
            hash = HashFile(hash, File.ReadAllBytes(path), name.EndsWith(".json", StringComparison.Ordinal));
        }
        return HashTables(hash, File.ReadAllText(surfacesPath), File.ReadAllText(catalogPath),
            File.ReadAllText(Path.Combine(packageDir, "objects.json")),
            MapPng.Read(Path.Combine(packageDir, "surface.png"), 1, out _, out _));
    }

    /// Continues `hash` over one file: its bytes (with CR LF as LF when `text`), then their count.
    internal static ulong HashFile(ulong hash, ReadOnlySpan<byte> bytes, bool text)
    {
        long count = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (text && bytes[i] == '\r' && i + 1 < bytes.Length && bytes[i + 1] == '\n')
                continue;
            hash = (hash ^ bytes[i]) * FnvPrime;
            count++;
        }
        for (int k = 0; k < 8; k++)
            hash = (hash ^ (byte)(count >> 8 * k)) * FnvPrime;
        return hash;
    }

    /// Continues `hash` over the parts of the shared tables that this map uses: the soil reference diameter, then each
    /// surface of `surfaceLayer` and of the placed assets' soil shapes by rising index, then each asset that
    /// `objectsJson` names by rising id, then each material those assets use by rising material id.
    internal static ulong HashTables(ulong hash, string surfacesJson, string catalogJson, string objectsJson,
        ReadOnlySpan<byte> surfaceLayer)
    {
        SurfaceParams[] table = SurfaceParams.ParseTable(surfacesJson);
        Catalog catalog = Catalog.Parse(catalogJson, table);
        var assets = new SortedSet<string>(StringComparer.Ordinal);
        using (JsonDocument objects = JsonDocument.Parse(objectsJson))
        {
            foreach (JsonElement o in objects.RootElement.GetProperty("objects").EnumerateArray())
                assets.Add(o.GetProperty("asset").GetString());
        }
        var surfaces = new SortedSet<byte>();
        foreach (byte index in surfaceLayer)
            surfaces.Add(index);
        var materials = new SortedSet<ushort>();
        foreach (string id in assets)
        {
            AssetDef asset = catalog.Assets[id];
            Use(asset.Material, asset.Surface);
            foreach (ShapeDef shape in asset.Collision)
                Use(shape.Material, shape.Surface);
        }

        using JsonDocument surfaceTable = JsonDocument.Parse(surfacesJson);
        using JsonDocument catalogTable = JsonDocument.Parse(catalogJson);
        hash = Item(hash, surfaceTable.RootElement.GetProperty("soil_reference_diameter_m").GetRawText());
        foreach (byte index in surfaces)
        {
            foreach (JsonElement surface in surfaceTable.RootElement.GetProperty("surfaces").EnumerateArray())
            {
                if (surface.GetProperty("index").GetInt32() == index)
                    hash = Item(hash, Canonical(surface));
            }
        }
        JsonElement entries = catalogTable.RootElement.GetProperty("assets");
        foreach (string id in assets)
        {
            hash = Item(hash, id);
            hash = Item(hash, Canonical(entries.GetProperty(id)));
        }
        JsonElement materialTable = catalogTable.RootElement.GetProperty("materials");
        foreach (ushort id in materials)
        {
            hash = Item(hash, $"{id} {catalog.MaterialIds[id]}");
            hash = Item(hash, Canonical(materialTable.GetProperty(catalog.MaterialIds[id])));
        }
        return hash;

        void Use(ushort material, byte surface)
        {
            if (material != Catalog.NoMaterial)
                materials.Add(material);
            if (surface != 0)
                surfaces.Add(surface);
        }
    }

    /// One item of the table part of the stream: its UTF-8 bytes, then their count.
    static ulong Item(ulong hash, string text) => HashFile(hash, Encoding.UTF8.GetBytes(text), false);

    /// A JSON value as canonical text: object keys sorted by their UTF-16 code units, no whitespace, and every number,
    /// string, boolean and null exactly as the file writes it, so no value is reformatted.
    static string Canonical(JsonElement value)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(writer, value);
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);

        static void Write(Utf8JsonWriter writer, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    var properties = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (JsonProperty p in value.EnumerateObject())
                        properties[p.Name] = p.Value;
                    foreach (KeyValuePair<string, JsonElement> p in properties)
                    {
                        writer.WritePropertyName(p.Key);
                        Write(writer, p.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (JsonElement item in value.EnumerateArray())
                        Write(writer, item);
                    writer.WriteEndArray();
                    break;
                default:
                    writer.WriteRawValue(value.GetRawText());
                    break;
            }
        }
    }
}
