using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

/// A map package built into a scene (map-loading spec): the terrain with its collision and a material per surface, every
/// object and wire of objects.json with Jolt collision tagged with its material, and the micro-detail near the camera.
/// Everything is drawn from `World`, the WorldQuery that physics uses, so the renderer and physics share one world:
/// object poses, wire polylines, collision shapes, the wind grid (filled from the wind volumes as World loads) and the
/// stems all come from it.
///
/// Every collider carries metadata: `object` (its index in objects.json; −1 for the terrain), and on each
/// CollisionShape3D `shape`, `material` (the catalog material id) and `material_id` (its number, for World.Material).
public partial class MapScene : Node3D
{
    const string MapsDir = "res://maps", SurfacesPath = "res://maps/surfaces.json", CatalogPath = "res://assets/catalog.json";
    /// Real textures drop in here with no code change, per surface id: `<id>_albedo.png`, `<id>_normal.png` (OpenGL
    /// convention, u east, v south) and `<id>_roughness.png` (red channel). A surface without one gets the placeholder.
    const string TextureDir = "res://assets/textures/terrain";
    const float TextureTile = 2f; // m per texture repeat
    const int PlaceholderSize = 128; // px per layer when no real texture sets a larger size

    static readonly string[] PackageFiles = { "map.json", "height.r16", "surface.png", "cover.png", "objects.json" };

    public WorldQuery World { get; private set; }
    public string Id { get; private set; }

    /// Loads game/maps/<id>/. On failure returns null, with `error` saying why in one line.
    public static MapScene Load(string id, out string error)
    {
        if (!Regex.IsMatch(id, "^[A-Za-z0-9_-]+$"))
        {
            error = $"'{id}' is not a map id (letters, digits, '_' and '-')";
            return null;
        }
        string dir = ProjectSettings.GlobalizePath($"{MapsDir}/{id}");
        if (!Directory.Exists(dir))
        {
            error = $"there is no map package game/maps/{id}/";
            return null;
        }
        return LoadPackage(id, dir, ProjectSettings.GlobalizePath(SurfacesPath), ProjectSettings.GlobalizePath(CatalogPath), out error);
    }

    /// Loads the package in folder `dir` with the given surface table and catalog.
    public static MapScene LoadPackage(string id, string dir, string surfacesPath, string catalogPath, out string error)
    {
        MapScene map = null;
        try
        {
            error = Check(dir, catalogPath);
            if (error != null)
                return null;
            WorldQuery world = WorldQuery.Load(dir, surfacesPath, catalogPath);
            map = new MapScene { Name = "Map", Id = id, World = world };
            map.Build(dir, surfacesPath);
            return map;
        }
        catch (Exception e)
        {
            map?.Free();
            error = $"{e.Message} (`python tools/map/validate_map.py {dir}` checks every rule)";
            return null;
        }
    }

    /// The rules the loader relies on, with their own messages: the five files, the format major, the size and the
    /// grids that follow from it, and catalog assets for every object. The validator checks the rest.
    static string Check(string dir, string catalogPath)
    {
        foreach (string file in PackageFiles)
        {
            if (!File.Exists(Path.Combine(dir, file)))
                return $"the package has no {file}";
        }
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "map.json")));
        JsonElement root = manifest.RootElement;
        string version = Field(root, "format_version").GetString();
        if (version?.Split('.')[0] != "1")
            return $"map.json format_version is {version}, and this build reads 1.x";
        double size = Field(root, "size_m").GetDouble();
        if (!(size >= 256 && size <= 8192 && size % 256 == 0))
            return $"map.json size_m is {size} m, and it must be a multiple of 256 m from 256 to 8192";
        foreach ((string layer, string count, int extra) in new[] { ("height", "samples_per_side", 1), ("surface", "cells_per_side", 0) })
        {
            JsonElement grid = Field(root, layer);
            double expected = size / Field(grid, "resolution_m").GetDouble() + extra;
            int actual = Field(grid, count).GetInt32();
            if (actual != expected)
                return $"map.json {layer}.{count} is {actual}, and size_m / resolution_m{(extra > 0 ? " + 1" : "")} is {expected}";
        }
        using JsonDocument catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
        JsonElement assets = catalog.RootElement.GetProperty("assets");
        using JsonDocument objects = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "objects.json")));
        int index = 0;
        foreach (JsonElement o in Field(objects.RootElement, "objects").EnumerateArray())
        {
            string asset = Field(o, "asset").GetString();
            if (!assets.TryGetProperty(asset, out _))
                return $"object {index} names asset '{asset}', which is not in the catalog";
            index++;
        }
        return null;
    }

    static JsonElement Field(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out JsonElement value) ? value : throw new InvalidDataException($"'{name}' is missing");

    void Build(string dir, string surfacesPath)
    {
        using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "map.json"))))
        {
            JsonElement height = manifest.RootElement.GetProperty("height");
            var terrain = new HeightmapTerrain { Name = "Terrain" };
            AddChild(terrain);
            terrain.Build(File.ReadAllBytes(Path.Combine(dir, "height.r16")), World.Samples, (float)World.HeightResolution,
                height.GetProperty("offset_m").GetSingle(), height.GetProperty("scale_m").GetSingle());
            terrain.GetNode("Collision").SetMeta("object", -1);
            Texture2DArray[] layers = SurfaceLayers(surfacesPath);
            terrain.SetSurfaces(World.SurfaceIds, World.Cells, (float)World.CellResolution, layers[0], layers[1], layers[2], TextureTile);
        }
        BuildObjects();
        var detail = new MicroDetailView { Name = "MicroDetail" };
        detail.Init(World);
        AddChild(detail);
    }

    // ---------------------------------------------------------------- surfaces

    /// Albedo, normal and roughness arrays with one layer per surface index (layer 0, which no cell uses, is blank).
    /// Each layer is the surface's texture from TextureDir when there is one, else a placeholder: PlaceholderAlbedo, a
    /// flat normal and the table's roughness. All layers are resized to the largest image.
    static Texture2DArray[] SurfaceLayers(string surfacesPath)
    {
        using JsonDocument table = JsonDocument.Parse(File.ReadAllText(surfacesPath));
        Dictionary<int, JsonElement> byIndex = table.RootElement.GetProperty("surfaces").EnumerateArray()
            .ToDictionary(s => s.GetProperty("index").GetInt32());
        string[] maps = { "albedo", "normal", "roughness" };
        var flatNormal = new Color(0.5f, 0.5f, 1f);
        var layers = new List<Image>[] { new(), new(), new() };
        for (int index = 0; index <= byIndex.Keys.Max(); index++)
        {
            bool known = byIndex.TryGetValue(index, out JsonElement s);
            for (int m = 0; m < 3; m++)
            {
                string path = known ? $"{TextureDir}/{s.GetProperty("id").GetString()}_{maps[m]}.png" : null;
                Image image;
                if (path != null && ResourceLoader.Exists(path))
                {
                    image = GD.Load<Texture2D>(path).GetImage();
                    if (image.IsCompressed())
                        image.Decompress();
                }
                else if (!known)
                {
                    image = Solid(m == 1 ? flatNormal : Colors.Gray);
                }
                else
                {
                    JsonElement material = s.GetProperty("material");
                    float roughness = material.GetProperty("roughness").GetSingle();
                    image = m == 0 ? PlaceholderAlbedo(Color.FromHtml(material.GetProperty("albedo_srgb").GetString()), index)
                        : Solid(m == 1 ? flatNormal : new Color(roughness, roughness, roughness));
                }
                layers[m].Add(image);
            }
        }
        int size = layers.SelectMany(images => images).Max(image => image.GetWidth());
        return layers.Select((images, m) =>
        {
            foreach (Image image in images)
            {
                image.Convert(Image.Format.Rgba8);
                if (image.GetWidth() != size || image.GetHeight() != size)
                    image.Resize(size, size, Image.Interpolation.Lanczos);
                image.GenerateMipmaps(m == 1);
            }
            var array = new Texture2DArray();
            array.CreateFromImages(new Godot.Collections.Array<Image>(images));
            return array;
        }).ToArray();
    }

    static Image Solid(Color color)
    {
        Image image = Image.CreateEmpty(PlaceholderSize, PlaceholderSize, false, Image.Format.Rgba8);
        image.Fill(color);
        return image;
    }

    /// Placeholder until UAT-1's textures: the table's albedo_srgb with up to ±15 % value noise. Its grain, 16 or 32
    /// lattice cells per tile, depends on the surface index. Noise, not stripes: a regular stripe of 0.5–1 m aliased into
    /// bands across the whole field from 60 m up, and a coarser grain shows the tile's 2 m repeat from there.
    static Image PlaceholderAlbedo(Color color, int index)
    {
        const int size = PlaceholderSize;
        int cells = 16 << index % 2;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = 0.7f * TileNoise(x, y, cells, index) + 0.3f * TileNoise(x, y, 2 * cells, index + 256);
                float v = 1f + 0.3f * (n - 0.5f);
                int at = (y * size + x) * 4;
                pixels[at] = (byte)Math.Clamp(color.R * v * 255f, 0f, 255f);
                pixels[at + 1] = (byte)Math.Clamp(color.G * v * 255f, 0f, 255f);
                pixels[at + 2] = (byte)Math.Clamp(color.B * v * 255f, 0f, 255f);
                pixels[at + 3] = 255;
            }
        }
        return Image.CreateFromData(size, size, false, Image.Format.Rgba8, pixels);
    }

    /// Value noise in 0–1 at pixel (x, y) of a placeholder tile: hashed lattice values, `cells` × `cells` per tile and
    /// wrapped at its edges so that the tile repeats seamlessly, blended with smoothstep fades.
    static float TileNoise(int x, int y, int cells, int seed)
    {
        float fx = (x + 0.5f) * cells / PlaceholderSize, fy = (y + 0.5f) * cells / PlaceholderSize;
        int i = (int)fx, j = (int)fy;
        float tx = fx - i, ty = fy - j;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        float Lattice(int a, int b)
        {
            uint h = (uint)(a % cells) * 0x9E3779B1u ^ (uint)(b % cells) * 0x85EBCA77u ^ (uint)seed * 0xC2B2AE3Du;
            h = (h ^ h >> 15) * 0x2C1B3C6Du;
            h = (h ^ h >> 12) * 0x297A2D39u;
            return (h ^ h >> 15) / (float)uint.MaxValue;
        }
        float top = Lattice(i, j) + (Lattice(i + 1, j) - Lattice(i, j)) * tx;
        float bottom = Lattice(i, j + 1) + (Lattice(i + 1, j + 1) - Lattice(i, j + 1)) * tx;
        return top + (bottom - top) * ty;
    }

    // ---------------------------------------------------------------- objects and wires

    void BuildObjects()
    {
        var objects = new Node3D { Name = "Objects" };
        AddChild(objects);
        var scenes = new Dictionary<string, PackedScene>();
        for (int i = 0; i < World.ObjectCount; i++)
        {
            Placement p = World.PlacementOf(i);
            if (!scenes.TryGetValue(p.Asset.Scene, out PackedScene scene))
            {
                scene = ResourceLoader.Exists(p.Asset.Scene) ? ResourceLoader.Load(p.Asset.Scene) as PackedScene : null;
                scenes[p.Asset.Scene] = scene ?? throw new InvalidDataException($"asset '{p.Asset.Id}' has no scene at {p.Asset.Scene}");
            }
            if (p.Asset.IsWire)
            {
                AddWire(objects, i, scene);
                continue;
            }
            Node3D visual = scene.Instantiate<Node3D>();
            visual.Name = $"{i} {p.Asset.Id}";
            visual.Transform = new Transform3D(ToBasis(p.Rotation, p.Scale), ToGodot(p.Position));
            visual.SetMeta("object", i);
            objects.AddChild(visual);

            StaticBody3D body = null;
            for (int shape = 0; World.Geometry(i, shape, 0, out ShapeGeometry g); shape++)
            {
                if (body == null)
                {
                    body = new StaticBody3D { Name = $"{i} {p.Asset.Id} collision" };
                    body.SetMeta("object", i);
                    objects.AddChild(body);
                }
                Shape3D primitive = g.Kind switch
                {
                    ShapeKind.Box => new BoxShape3D { Size = ToGodot(g.HalfExtents) * 2 },
                    ShapeKind.Sphere => new SphereShape3D { Radius = (float)g.Radius },
                    ShapeKind.Capsule => new CapsuleShape3D { Radius = (float)g.Radius, Height = (float)g.Height },
                    _ => new CylinderShape3D { Radius = (float)g.Radius, Height = (float)g.Height },
                };
                body.AddChild(Tagged(new CollisionShape3D { Shape = primitive, Transform = new Transform3D(ToBasis(g.Axes, 1), ToGodot(g.Center)) },
                    shape, g.Material));
            }
        }
    }

    /// A wire along World's polyline: the catalog's unit segment (1 m along +Z, 1 m across) stretched over each piece,
    /// and a capsule per piece for collision, all tagged as shape 0 of the wire, as the world query reports it.
    void AddWire(Node3D objects, int index, PackedScene segment)
    {
        World.Geometry(index, 0, 0, out ShapeGeometry g);
        ReadOnlySpan<Double3> points = World.WirePoints(index);
        var visual = new Node3D { Name = $"{index} {World.PlacementOf(index).Asset.Id}" };
        visual.SetMeta("object", index);
        objects.AddChild(visual);
        var body = new StaticBody3D { Name = $"{visual.Name} collision" };
        body.SetMeta("object", index);
        objects.AddChild(body);
        float diameter = (float)g.Diameter;
        for (int k = 0; k + 1 < points.Length; k++)
        {
            Vector3 a = ToGodot(points[k]), b = ToGodot(points[k + 1]), along = b - a;
            float length = along.Length();
            Vector3 z = along / length, x = Vector3.Up.Cross(z);
            x = x.LengthSquared() > 1e-6f ? x.Normalized() : Vector3.Right;
            Vector3 y = z.Cross(x);
            Node3D piece = segment.Instantiate<Node3D>();
            piece.Transform = new Transform3D(new Basis(x * diameter, y * diameter, z * length), a);
            visual.AddChild(piece);
            var capsule = new CapsuleShape3D { Radius = diameter / 2, Height = length + diameter };
            body.AddChild(Tagged(new CollisionShape3D { Shape = capsule, Transform = new Transform3D(new Basis(x, z, -y), (a + b) / 2) },
                0, g.Material));
        }
    }

    CollisionShape3D Tagged(CollisionShape3D node, int shape, ushort material)
    {
        node.SetMeta("shape", shape);
        node.SetMeta("material", World.Catalog.MaterialIds[material]);
        node.SetMeta("material_id", material);
        return node;
    }

    static Vector3 ToGodot(Double3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

    static Basis ToBasis(in Axes r, double scale) =>
        new(ToGodot(r.X) * (float)scale, ToGodot(r.Y) * (float)scale, ToGodot(r.Z) * (float)scale);
}
