using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// The micro-detail near the camera, drawn from the world query's own generator, MicroDetailNear. The stems, straws, twigs
/// and leaves on screen are the ones physics touches, with the same bases, directions, lengths and diameters.
///
/// Frame budget: the ground is cut into 4 m tiles. A tile is generated once, on a worker thread, when it comes within
/// Radius of the camera, and kept until it is farther than Keep. The main thread only hands finished tiles to a
/// MultiMesh. One query per tile finds everything rooted in it: a sphere round the tile's terrain range widened by
/// WorldQuery.BaseBelowTerrain and BaseAboveTerrain. Elements rooted in other tiles are dropped, so each is drawn once.
/// Beyond Radius nothing is drawn: a placeholder until UAT-1's far vegetation.
public partial class MicroDetailView : Node3D
{
    /// Tile side, m: a power of two and a whole number of WorldQuery.MicroCell, so that floor(x / Tile) of an element's
    /// base is exactly its cell's tile.
    public const double Tile = 4;
    /// Tiles nearer the camera than this (m, from the camera's height above the terrain and the horizontal distance to
    /// the tile) are drawn.
    public const double Radius = 8;
    const double Keep = 12;    // m, tiles farther than this are dropped
    const int TilesPerJob = 8; // nearest first

    /// Placeholder colours per CoverKind (grass, straw, twigs, litter) until UAT-1's vegetation art.
    static readonly Color[] KindColor =
        { Color.FromHtml("#6b7a3a").SrgbToLinear(), Color.FromHtml("#c2ad6e").SrgbToLinear(), Color.FromHtml("#4f3d2c").SrgbToLinear(), Color.FromHtml("#8a5a30").SrgbToLinear() };

    WorldQuery _world;
    ArrayMesh _mesh;
    StandardMaterial3D _material;
    readonly Dictionary<(int X, int Z), MultiMeshInstance3D> _tiles = new(); // null for a tile with no elements
    Task<List<TileData>> _job;
    MicroElement[] _elements = new MicroElement[1 << 16]; // the job's buffer; one job runs at a time

    /// True once every tile within Radius of the camera is drawn and no job is running.
    public bool Settled { get; private set; }

    struct TileData
    {
        public (int X, int Z) Key;
        public Vector3 Origin;  // the MultiMesh's position: the tile's north-west corner at its lowest possible base
        public float[] Buffer;  // per element a 3 × 4 transform (rows) relative to Origin and a colour
        public int Count;
        public Aabb Box;        // relative to Origin
    }

    public void Init(WorldQuery world)
    {
        _world = world;
        _mesh = Cross();
        _material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
    }

    public override void _Process(double delta)
    {
        Camera3D camera = GetViewport().GetCamera3D();
        if (_world == null || camera == null)
            return;
        Vector3 eye = camera.GlobalPosition;
        Span<XZ> point = stackalloc XZ[] { new XZ(eye.X, eye.Z) };
        Span<GroundSample> ground = stackalloc GroundSample[1];
        _world.SampleGround(point, ground);
        double lift = Math.Max(eye.Y - ground[0].TerrainHeight, 0);

        if (_job is { IsCompleted: true })
        {
            Task<List<TileData>> done = _job;
            _job = null;
            foreach (TileData tile in done.Result)
            {
                if (!_tiles.ContainsKey(tile.Key) && Distance(tile.Key, eye, lift) < Keep)
                    _tiles[tile.Key] = Show(tile);
            }
        }
        foreach ((int, int) key in _tiles.Keys.Where(key => Distance(key, eye, lift) > Keep).ToList())
        {
            _tiles[key]?.QueueFree();
            _tiles.Remove(key);
        }

        var missing = new List<((int X, int Z) Key, double Distance)>();
        int x0 = (int)Math.Floor((eye.X - Radius) / Tile), x1 = (int)Math.Floor((eye.X + Radius) / Tile);
        int z0 = (int)Math.Floor((eye.Z - Radius) / Tile), z1 = (int)Math.Floor((eye.Z + Radius) / Tile);
        for (int tz = z0; tz <= z1; tz++)
        {
            for (int tx = x0; tx <= x1; tx++)
            {
                double d = Distance((tx, tz), eye, lift);
                bool onMap = tx * Tile < _world.Half && (tx + 1) * Tile > -_world.Half && tz * Tile < _world.Half && (tz + 1) * Tile > -_world.Half;
                if (d < Radius && onMap && !_tiles.ContainsKey((tx, tz)))
                    missing.Add(((tx, tz), d));
            }
        }
        Settled = _job == null && missing.Count == 0;
        if (_job == null && missing.Count > 0)
        {
            (int, int)[] keys = missing.OrderBy(m => m.Distance).Take(TilesPerJob).Select(m => m.Key).ToArray();
            _job = Task.Run(() => keys.Select(Generate).ToList());
        }
    }

    /// From the camera to the nearest point of the tile, horizontally, combined with the camera's height above the terrain.
    static double Distance((int X, int Z) key, Vector3 eye, double lift)
    {
        double dx = Math.Max(Math.Max(key.X * Tile - eye.X, eye.X - (key.X + 1) * Tile), 0);
        double dz = Math.Max(Math.Max(key.Z * Tile - eye.Z, eye.Z - (key.Z + 1) * Tile), 0);
        return Math.Sqrt(dx * dx + dz * dz + lift * lift);
    }

    MultiMeshInstance3D Show(TileData tile)
    {
        if (tile.Count == 0)
            return null;
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = _mesh,
            InstanceCount = tile.Count,
            CustomAabb = tile.Box,
        };
        multimesh.Buffer = tile.Buffer;
        var node = new MultiMeshInstance3D
        {
            Name = $"Tile {tile.Key.X} {tile.Key.Z}",
            Multimesh = multimesh,
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = tile.Origin,
        };
        AddChild(node);
        return node;
    }

    /// Worker thread: every element rooted in tile `key`, as instance data.
    TileData Generate((int X, int Z) key)
    {
        double x0 = key.X * Tile, z0 = key.Z * Tile, half = _world.Half, res = _world.HeightResolution;
        // The terrain's range over the tile: its height samples on and around the tile, since it is linear between them.
        int i0 = (int)Math.Floor((x0 + half) / res), i1 = (int)Math.Ceiling((x0 + Tile + half) / res);
        int j0 = (int)Math.Floor((z0 + half) / res), j1 = (int)Math.Ceiling((z0 + Tile + half) / res);
        var points = new XZ[(i1 - i0 + 1) * (j1 - j0 + 1)];
        for (int j = j0, n = 0; j <= j1; j++)
        {
            for (int i = i0; i <= i1; i++)
                points[n++] = new XZ(Math.Clamp(-half + i * res, -half, half), Math.Clamp(-half + j * res, -half, half));
        }
        var samples = new GroundSample[points.Length];
        _world.SampleGround(points, samples);
        double lo = samples.Min(s => s.TerrainHeight) - _world.BaseBelowTerrain - 1e-3;
        double hi = samples.Max(s => s.TerrainHeight) + _world.BaseAboveTerrain + 1e-3;
        var center = new Double3(x0 + Tile / 2, (lo + hi) / 2, z0 + Tile / 2);
        double radius = Math.Sqrt(Tile * Tile / 2 + (hi - lo) * (hi - lo) / 4);
        int found;
        while ((found = _world.MicroDetailNear(center, radius, KindMask.All, _elements)) > _elements.Length)
            _elements = new MicroElement[found];

        var tile = new TileData { Key = key, Origin = new Vector3((float)x0, (float)lo, (float)z0), Buffer = new float[found * 16] };
        Vector3 min = Vector3.Inf, max = -Vector3.Inf;
        for (int k = 0; k < found; k++)
        {
            ref readonly MicroElement e = ref _elements[k];
            if (Math.Floor(e.Base.X / Tile) != key.X || Math.Floor(e.Base.Z / Tile) != key.Z)
                continue; // rooted in another tile, which draws it
            var dir = new Vector3(e.Direction.X, e.Direction.Y, e.Direction.Z);
            Vector3 side = Vector3.Up.Cross(dir);
            side = side.LengthSquared() > 1e-6f ? side.Normalized() : Vector3.Right;
            Vector3 x, y, z;
            if (e.Kind == CoverKind.Litter)
            {
                // A leaf: flat on the mat (1 mm thick), as wide as its diameter and at least as long.
                (x, y, z) = (side * e.Diameter, dir * Math.Max(e.Length, e.Diameter), side.Cross(dir) * 1e-3f);
            }
            else
            {
                (x, y, z) = (side * e.Diameter, dir * e.Length, side.Cross(dir) * e.Diameter);
            }
            var origin = new Vector3((float)(e.Base.X - x0), (float)(e.Base.Y - tile.Origin.Y), (float)(e.Base.Z - z0));
            Color color = KindColor[(int)e.Kind];
            int at = tile.Count++ * 16;
            float[] b = tile.Buffer;
            (b[at], b[at + 1], b[at + 2], b[at + 3]) = (x.X, y.X, z.X, origin.X);
            (b[at + 4], b[at + 5], b[at + 6], b[at + 7]) = (x.Y, y.Y, z.Y, origin.Y);
            (b[at + 8], b[at + 9], b[at + 10], b[at + 11]) = (x.Z, y.Z, z.Z, origin.Z);
            (b[at + 12], b[at + 13], b[at + 14], b[at + 15]) = (color.R, color.G, color.B, 1f);
            Vector3 tip = origin + y;
            min = min.Min(origin.Min(tip));
            max = max.Max(origin.Max(tip));
        }
        Array.Resize(ref tile.Buffer, tile.Count * 16);
        Vector3 pad = Vector3.One * 0.1f; // wider than any element's diameter
        tile.Box = tile.Count > 0 ? new Aabb(min - pad, max - min + 2 * pad) : default;
        return tile;
    }

    /// Two crossed unit quads along +Y from 0 to 1, 1 across, one facing Z and one facing X: an element's instance
    /// transform scales them to its length along Direction and its diameter across. A leaf flattens Z, which leaves the
    /// quad facing Z; its normal stays right because instances transform normals by their basis, and a single scaled axis
    /// keeps its direction.
    static ArrayMesh Cross()
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        foreach ((Vector3 across, Vector3 normal) in new[] { (Vector3.Right, Vector3.Back), (Vector3.Back, Vector3.Right) })
        {
            // Clockwise seen from the normal's side, Godot's front face, so double-sided lighting flips the normal only
            // on the back.
            Vector3 a = -across / 2, b = across / 2;
            foreach (Vector3 v in new[] { a, b + Vector3.Up, b, a, a + Vector3.Up, b + Vector3.Up })
            {
                vertices.Add(v);
                normals.Add(normal);
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
