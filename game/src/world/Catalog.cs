using System;
using System.Collections.Generic;
using System.Text.Json;

public enum ShapeKind : byte { Box, Sphere, Capsule, Cylinder, Wire }

/// One primitive of an asset's collision or wind volume, in asset space (game/maps/README.md). Capsules and
/// cylinders stand along asset +Y.
public sealed class ShapeDef
{
    public ShapeKind Kind;
    public Double3 Size;               // box: full size, m
    public double Radius, Height;      // sphere, capsule, cylinder, m; a capsule's height includes its caps
    public Double3 Position;           // m
    public double Yaw, Pitch, Roll;    // degrees, the object order
    public ushort Material;            // the shape's own material, else the asset's; NoMaterial for a soil shape
    public byte Surface;               // soil shape ("surface:<id>"): that surface's index; 0 for every other shape
}

/// A named fly-through opening in asset space. It faces asset ±Z turned by `Yaw` (degrees).
public sealed class GapDef
{
    public string Name;
    public Double3 Center;
    public double Width, Height, Yaw;
}

public sealed class AssetDef
{
    public string Id;
    public string Scene;          // res:// path of the visual; for a wire, the unit segment the loader stretches
    public bool IsWire;
    public ushort Material;       // Catalog.NoMaterial for visual-only assets and for soil ("surface:<id>") assets
    public byte Surface;          // a soil asset's surface index; 0 otherwise
    public double WireSegment;    // wires: capsule_chain segment length along the sagged curve, m
    public ShapeDef[] Collision;  // empty for visual-only assets and wires
    public ShapeDef[] WindVolume; // the wind_volume shapes, else the collision shapes
    public double WindPorosity;   // optical porosity of the wind volume seen side-on
    public GapDef[] Gaps;
    public double[] LodSwitch;    // camera distances (m) where the visual's LODs change over, rising; empty for none
}

/// A contact material of the catalog (game/maps/README.md, Materials), with the README's defaults for fields left out.
public sealed class MaterialParams
{
    public double FrictionStatic, FrictionKinetic;
    public double Stiffness;    // N/m, the object's local stiffness under a point load; +∞ for a rigid material
    public double DampingRatio; // of that stiffness
    public double EdgeRadius;   // m, of box edges where the fiber bends over
}

/// The shared asset catalog (game/assets/catalog.json) as the world query uses it. Material ids are the materials'
/// positions in the catalog's `materials` object.
public sealed class Catalog
{
    /// No catalog material: visual-only assets, soil shapes, and terrain ray hits and misses. WorldQuery.Material gives
    /// null for it.
    public const ushort NoMaterial = ushort.MaxValue;

    /// A material that names a surface of surfaces.json instead of a catalog material: cut soil, the walls and floor of
    /// a hole's fillers. Its contacts and ray hits carry Surface = that surface's index and Material = NoMaterial.
    public const string SurfacePrefix = "surface:";

    public readonly string[] MaterialIds;
    public readonly MaterialParams[] Materials; // by material id, as MaterialIds
    public readonly Dictionary<string, AssetDef> Assets = new();
    readonly SurfaceParams[] _surfaces;         // the surface table, for "surface:<id>" materials; null if none was given

    Catalog(string[] materialIds, MaterialParams[] materials, SurfaceParams[] surfaces) =>
        (MaterialIds, Materials, _surfaces) = (materialIds, materials, surfaces);

    public ushort MaterialId(string id)
    {
        int i = Array.IndexOf(MaterialIds, id);
        return i >= 0 ? (ushort)i : throw new JsonException($"unknown material '{id}'");
    }

    /// A material string as (catalog material id, surface index): "surface:<id>" gives (NoMaterial, that index).
    (ushort Material, byte Surface) Resolve(string material)
    {
        if (!material.StartsWith(SurfacePrefix, StringComparison.Ordinal))
            return (MaterialId(material), 0);
        string id = material[SurfacePrefix.Length..];
        SurfaceParams surface = Array.Find(_surfaces ?? Array.Empty<SurfaceParams>(), s => s.Id == id)
            ?? throw new JsonException($"material '{material}' names no surface of surfaces.json");
        return (NoMaterial, surface.Index);
    }

    /// `surfaces` is surfaces.json's table, needed only by assets with a "surface:<id>" material.
    public static Catalog Parse(string json, SurfaceParams[] surfaces = null)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        var ids = new List<string>();
        var materials = new List<MaterialParams>();
        foreach (JsonProperty m in root.GetProperty("materials").EnumerateObject())
        {
            ids.Add(m.Name);
            materials.Add(new MaterialParams
            {
                FrictionStatic = m.Value.GetProperty("friction_static").GetDouble(),
                FrictionKinetic = m.Value.GetProperty("friction_kinetic").GetDouble(),
                Stiffness = Optional(m.Value, "stiffness_n_per_m", double.PositiveInfinity),
                DampingRatio = Optional(m.Value, "damping_ratio", 0.05),
                EdgeRadius = Optional(m.Value, "edge_radius_m", 0.002),
            });
        }
        var catalog = new Catalog(ids.ToArray(), materials.ToArray(), surfaces);
        foreach (JsonProperty a in root.GetProperty("assets").EnumerateObject())
        {
            JsonElement e = a.Value;
            (ushort material, byte surface) = e.TryGetProperty("material", out JsonElement m)
                ? catalog.Resolve(m.GetString()) : (NoMaterial, (byte)0);
            var asset = new AssetDef
            {
                Id = a.Name,
                Scene = e.GetProperty("scene").GetString(),
                IsWire = e.GetProperty("type").GetString() == "wire",
                Material = material,
                Surface = surface,
                WindPorosity = e.GetProperty("wind_porosity").GetDouble(),
                Collision = Array.Empty<ShapeDef>(),
            };
            bool visualOnly = e.TryGetProperty("visual_only", out JsonElement v) && v.GetBoolean();
            if (asset.IsWire)
                asset.WireSegment = e.GetProperty("collision")[0].GetProperty("segment_m").GetDouble();
            else if (!visualOnly)
                asset.Collision = catalog.Shapes(e.GetProperty("collision"), asset.Material, asset.Surface);
            asset.WindVolume = e.TryGetProperty("wind_volume", out JsonElement wind)
                ? catalog.Shapes(wind, asset.Material, asset.Surface) : asset.Collision;
            var gaps = new List<GapDef>();
            foreach (JsonElement g in e.GetProperty("gaps").EnumerateArray())
            {
                gaps.Add(new GapDef
                {
                    Name = g.GetProperty("name").GetString(),
                    Center = Vec3(g, "center_m"),
                    Width = g.GetProperty("width_m").GetDouble(),
                    Height = g.GetProperty("height_m").GetDouble(),
                    Yaw = g.GetProperty("yaw_deg").GetDouble(),
                });
            }
            asset.Gaps = gaps.ToArray();
            var switches = new List<double>();
            if (e.TryGetProperty("lod_switch_m", out JsonElement lod))
            {
                foreach (JsonElement d in lod.EnumerateArray())
                {
                    if (d.GetDouble() <= (switches.Count > 0 ? switches[^1] : 0))
                        throw new JsonException($"asset '{a.Name}': lod_switch_m must rise from 0, and {d} does not");
                    switches.Add(d.GetDouble());
                }
            }
            asset.LodSwitch = switches.ToArray();
            catalog.Assets[a.Name] = asset;
        }
        return catalog;
    }

    ShapeDef[] Shapes(JsonElement list, ushort assetMaterial, byte assetSurface)
    {
        var shapes = new ShapeDef[list.GetArrayLength()];
        int n = 0;
        foreach (JsonElement s in list.EnumerateArray())
        {
            (ushort material, byte surface) = s.TryGetProperty("material", out JsonElement m)
                ? Resolve(m.GetString()) : (assetMaterial, assetSurface);
            var shape = new ShapeDef
            {
                Kind = s.GetProperty("shape").GetString() switch
                {
                    "box" => ShapeKind.Box,
                    "sphere" => ShapeKind.Sphere,
                    "capsule" => ShapeKind.Capsule,
                    "cylinder" => ShapeKind.Cylinder,
                    string other => throw new JsonException($"unknown shape '{other}'"),
                },
                Position = s.TryGetProperty("position_m", out _) ? Vec3(s, "position_m") : default,
                Material = material,
                Surface = surface,
            };
            if (shape.Kind == ShapeKind.Box)
                shape.Size = Vec3(s, "size_m");
            else
                shape.Radius = s.GetProperty("radius_m").GetDouble();
            if (shape.Kind is ShapeKind.Capsule or ShapeKind.Cylinder)
                shape.Height = s.GetProperty("height_m").GetDouble();
            if (s.TryGetProperty("rotation_deg", out _))
            {
                Double3 r = Vec3(s, "rotation_deg");
                (shape.Yaw, shape.Pitch, shape.Roll) = (r.X, r.Y, r.Z);
            }
            shapes[n++] = shape;
        }
        return shapes;
    }

    static double Optional(JsonElement parent, string name, double fallback) =>
        parent.TryGetProperty(name, out JsonElement v) ? v.GetDouble() : fallback;

    public static Double3 Vec3(JsonElement parent, string name)
    {
        JsonElement v = parent.GetProperty(name);
        return new Double3(v[0].GetDouble(), v[1].GetDouble(), v[2].GetDouble());
    }
}
