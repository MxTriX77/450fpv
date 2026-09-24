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
    public ushort Material;            // the shape's own material, else the asset's
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
    public bool IsWire;
    public ushort Material;       // Catalog.NoMaterial for visual-only assets
    public double WireSegment;    // wires: capsule_chain segment length along the sagged curve, m
    public ShapeDef[] Collision;  // empty for visual-only assets and wires
    public ShapeDef[] WindVolume; // the wind_volume shapes, else the collision shapes
    public double WindPorosity;   // optical porosity of the wind volume seen side-on
    public GapDef[] Gaps;
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
    /// No catalog material: visual-only assets, and terrain ray hits and misses. WorldQuery.Material gives null for it.
    public const ushort NoMaterial = ushort.MaxValue;

    public readonly string[] MaterialIds;
    public readonly MaterialParams[] Materials; // by material id, as MaterialIds
    public readonly Dictionary<string, AssetDef> Assets = new();

    Catalog(string[] materialIds, MaterialParams[] materials) => (MaterialIds, Materials) = (materialIds, materials);

    public ushort MaterialId(string id)
    {
        int i = Array.IndexOf(MaterialIds, id);
        return i >= 0 ? (ushort)i : throw new JsonException($"unknown material '{id}'");
    }

    public static Catalog Parse(string json)
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
        var catalog = new Catalog(ids.ToArray(), materials.ToArray());
        foreach (JsonProperty a in root.GetProperty("assets").EnumerateObject())
        {
            JsonElement e = a.Value;
            var asset = new AssetDef
            {
                Id = a.Name,
                IsWire = e.GetProperty("type").GetString() == "wire",
                Material = e.TryGetProperty("material", out JsonElement material) ? catalog.MaterialId(material.GetString()) : NoMaterial,
                WindPorosity = e.GetProperty("wind_porosity").GetDouble(),
                Collision = Array.Empty<ShapeDef>(),
            };
            bool visualOnly = e.TryGetProperty("visual_only", out JsonElement v) && v.GetBoolean();
            if (asset.IsWire)
                asset.WireSegment = e.GetProperty("collision")[0].GetProperty("segment_m").GetDouble();
            else if (!visualOnly)
                asset.Collision = catalog.Shapes(e.GetProperty("collision"), asset.Material);
            asset.WindVolume = e.TryGetProperty("wind_volume", out JsonElement wind) ? catalog.Shapes(wind, asset.Material) : asset.Collision;
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
            catalog.Assets[a.Name] = asset;
        }
        return catalog;
    }

    ShapeDef[] Shapes(JsonElement list, ushort assetMaterial)
    {
        var shapes = new ShapeDef[list.GetArrayLength()];
        int n = 0;
        foreach (JsonElement s in list.EnumerateArray())
        {
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
                Material = s.TryGetProperty("material", out JsonElement m) ? MaterialId(m.GetString()) : assetMaterial,
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
