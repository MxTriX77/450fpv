using System;
using System.IO;
using System.Numerics;
using System.Text.Json;

/// An orthonormal rotation: the world directions of local X, Y and Z.
public struct Axes
{
    public Double3 X, Y, Z;

    /// Yaw about +Y, then pitch about the new +X, then roll about the new +Z, in degrees: Godot's YXZ order, as objects
    /// and shapes are placed (game/maps/README.md). Positive yaw turns +X toward −Z.
    public static Axes FromEuler(double yaw, double pitch, double roll)
    {
        DetMath.SinCosTurns(yaw / 360.0, out double sy, out double cy);
        DetMath.SinCosTurns(pitch / 360.0, out double sp, out double cp);
        DetMath.SinCosTurns(roll / 360.0, out double sr, out double cr);
        Double3 Rotate(double x, double y, double z)
        {
            (x, y) = (x * cr - y * sr, x * sr + y * cr);
            (y, z) = (y * cp - z * sp, y * sp + z * cp);
            (x, z) = (x * cy + z * sy, z * cy - x * sy);
            return new Double3(x, y, z);
        }
        return new Axes { X = Rotate(1, 0, 0), Y = Rotate(0, 1, 0), Z = Rotate(0, 0, 1) };
    }

    public readonly Double3 ToWorld(Double3 v) => X * v.X + Y * v.Y + Z * v.Z;
    public readonly Double3 ToLocal(Double3 v) => new(Double3.Dot(v, X), Double3.Dot(v, Y), Double3.Dot(v, Z));

    /// This rotation applied after `inner`.
    public readonly Axes Compose(in Axes inner) => new() { X = ToWorld(inner.X), Y = ToWorld(inner.Y), Z = ToWorld(inner.Z) };
}

public struct Bounds
{
    public Double3 Min, Max;

    public static Bounds Of(Double3 a, Double3 b, double pad) => new()
    {
        Min = new Double3(Math.Min(a.X, b.X) - pad, Math.Min(a.Y, b.Y) - pad, Math.Min(a.Z, b.Z) - pad),
        Max = new Double3(Math.Max(a.X, b.X) + pad, Math.Max(a.Y, b.Y) + pad, Math.Max(a.Z, b.Z) + pad),
    };

    public readonly Bounds Union(in Bounds b) => new()
    {
        Min = new Double3(Math.Min(Min.X, b.Min.X), Math.Min(Min.Y, b.Min.Y), Math.Min(Min.Z, b.Min.Z)),
        Max = new Double3(Math.Max(Max.X, b.Max.X), Math.Max(Max.Y, b.Max.Y), Math.Max(Max.Z, b.Max.Z)),
    };

    public readonly bool Overlaps(in Bounds b) =>
        Min.X <= b.Max.X && b.Min.X <= Max.X && Min.Y <= b.Max.Y && b.Min.Y <= Max.Y && Min.Z <= b.Max.Z && b.Min.Z <= Max.Z;
}

/// A fly-through opening in world space (world-query W-10).
public struct Gap
{
    public Double3 Center;       // m
    public Vector3 Normal;       // unit, the way the opening faces: asset +Z turned by the gap's yaw
    public Vector3 Up;           // unit, along the opening's height
    public float Width, Height;  // m
    public int Object;
    public string Name;
}

/// The geometry of one collision shape or wire of a placed object (world-query "Shape and wire geometry", WorldQuery.
/// Geometry), for what a contact alone does not give physics: a wire's compliance, T = w·L²/(8·sag), and the fiber's bend
/// radius over what it touches. World frame, m.
public struct ShapeGeometry
{
    public ShapeKind Kind;
    public ushort Material;     // catalog material id: the shape's own, else the asset's
    public Double3 Center;      // not set for a wire
    public Axes Axes;           // the shape's rotation; capsules and cylinders run along Axes.Y. Not set for a wire
    public Double3 HalfExtents; // along Axes.X, Y, Z: a box's half size, (r, r, r), capsule (r, h/2, r), cylinder (r, h/2, r)
    public double Radius;       // sphere, capsule, cylinder and wire; 0 for a box, whose edges bend the fiber at its
                                // material's EdgeRadius
    public double Height;       // capsule (with its caps) and cylinder, along Axes.Y; 0 otherwise
    // A wire: the span that holds the wire parameter asked for.
    public Double3 SpanA, SpanB; // its attachment points
    public double SpanLength;    // |SpanB − SpanA|
    public double Sag;           // mid-span drop below the line A–B: the point at t drops 4·Sag·t·(1 − t)
    public double Diameter;
    public double SpanT;         // where the parameter falls on the span, as that t: 0 at SpanA, 1 at SpanB
}

/// Placed catalog objects and wires in world space, with a broadphase grid for the contact and ray queries, and the
/// fly-through gaps. Objects are added while the map loads and, before a flight, by the game (W-11); from then on they
/// are static and every query only reads them. ResetRuntimeObjects removes the game's again before the next flight.
public sealed partial class WorldQuery
{
    /// One collision primitive in world space: a catalog shape of a placed object, or a whole wire.
    struct Prim
    {
        public ShapeKind Kind;
        public ushort Shape, Material;
        public int Object;
        public Double3 C;        // centre
        public Axes R;           // capsules and cylinders run along R.Y
        public Double3 Half;     // box: half size; capsule: Half.Y = half the core length; cylinder: Half.Y = half height
        public double Radius;    // sphere, capsule, cylinder, wire
        public int First, Count; // wire: its polyline points in _wirePoints
        public int FirstAnchor, Anchors; // wire: its attachment points, as polyline indices in _wireAnchors
        public double Sag;       // wire: the mid-span drop of every span
        public Bounds Box;       // world bounds
        public int CellX, CellZ; // broadphase cell of the bounds' minimum corner
    }

    /// Broadphase cell side, m.
    const double BroadCell = 8.0;

    public Catalog Catalog { get; }
    /// Objects so far: those of objects.json, in its order, then the runtime objects.
    public int ObjectCount { get; private set; }

    Prim[] _prims = new Prim[16];
    int _primCount;
    Double3[] _wirePoints = new Double3[64];
    double[] _wireAlong = new double[64]; // length along the wire's polyline up to each point, m
    int _wirePointCount;
    int[] _wireAnchors = new int[8];
    int _wireAnchorCount;
    int[] _objectPrim = new int[16]; // each object's first primitive; its shapes follow in order
    Gap[] _gaps = new Gap[4];
    int _gapCount;
    int _broadCells;
    int[] _cellStart, _cellItems; // primitives per broadphase cell, row-major from the north-west, each in primitive order
    /// The objects.json state that ResetRuntimeObjects returns to: the counts when loading ended (all 0 in memory).
    int _loadedObjects, _loadedPrims, _loadedWirePoints, _loadedAnchors, _loadedGaps;
    bool _loading; // objects.json is being placed: its wind changes are never undone
    /// Each wind cell as it was before a runtime object changed it, in order, so that a reset can undo them exactly.
    (int Cell, WindCell Before)[] _windUndo = new (int, WindCell)[16];
    int _windUndoCount;

    void InitObjects()
    {
        _broadCells = (int)Math.Ceiling(SizeM / BroadCell);
        InitWind();
        RebuildBroadphase();
    }

    /// World-query W-11: adds a catalog object (not a wire) before a flight starts, for example the launch rails at a
    /// start point, and returns its object index. It is static from then on and takes part in StaticContacts, Raycast,
    /// the wind grid and GapsNear. Not thread-safe: call it before any query runs.
    public int AddObject(string assetId, Double3 position, double yaw, double pitch = 0, double roll = 0, double scale = 1)
    {
        int index = Place(Catalog.Assets[assetId], position, yaw, pitch, roll, scale);
        RebuildBroadphase();
        return index;
    }

    /// Adds a wire through `points` (m) whose every span sags `sag` (m) at mid-span, and returns its object index.
    public int AddWire(string assetId, ReadOnlySpan<Double3> points, double sag, double diameter)
    {
        int index = PlaceWire(Catalog.Assets[assetId], points, sag, diameter);
        RebuildBroadphase();
        return index;
    }

    /// World-query "Runtime objects": returns the world to its objects.json state before each flight. Every object added
    /// since loading (AddObject, AddWire) leaves the contacts, rays, gaps and the wind grid, and the next one added gets
    /// the first index after objects.json again. Not thread-safe: call it before any query of the flight runs.
    public void ResetRuntimeObjects()
    {
        for (int i = _windUndoCount - 1; i >= 0; i--)
            _wind[_windUndo[i].Cell] = _windUndo[i].Before;
        _windUndoCount = 0;
        ObjectCount = _loadedObjects;
        _primCount = _loadedPrims;
        _wirePointCount = _loadedWirePoints;
        _wireAnchorCount = _loadedAnchors;
        _gapCount = _loadedGaps;
        RebuildBroadphase();
    }

    void LoadObjects(string path)
    {
        _loading = true;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement o in document.RootElement.GetProperty("objects").EnumerateArray())
        {
            AssetDef asset = Catalog.Assets[o.GetProperty("asset").GetString()];
            if (asset.IsWire)
            {
                JsonElement list = o.GetProperty("points_m");
                var points = new Double3[list.GetArrayLength()];
                int n = 0;
                foreach (JsonElement p in list.EnumerateArray())
                    points[n++] = new Double3(p[0].GetDouble(), p[1].GetDouble(), p[2].GetDouble());
                PlaceWire(asset, points, o.GetProperty("sag_m").GetDouble(), o.GetProperty("diameter_m").GetDouble());
            }
            else
            {
                Double3 r = Catalog.Vec3(o, "rotation_deg");
                Place(asset, Catalog.Vec3(o, "position_m"), r.X, r.Y, r.Z, o.GetProperty("scale").GetDouble());
            }
        }
        _loading = false;
        (_loadedObjects, _loadedPrims, _loadedWirePoints, _loadedAnchors, _loadedGaps) =
            (ObjectCount, _primCount, _wirePointCount, _wireAnchorCount, _gapCount);
        RebuildBroadphase();
    }

    int Place(AssetDef asset, Double3 position, double yaw, double pitch, double roll, double scale)
    {
        if (asset.IsWire)
            throw new ArgumentException($"asset '{asset.Id}' is a wire: add it with AddWire");
        int index = ObjectCount++;
        StartObject(index);
        Axes r = Axes.FromEuler(yaw, pitch, roll);
        for (int i = 0; i < asset.Collision.Length; i++)
            Append(ref _prims, ref _primCount, MakePrim(asset.Collision[i], position, r, scale, index, i));
        foreach (ShapeDef shape in asset.WindVolume)
            AddWind(MakePrim(shape, position, r, scale, index, 0), asset.WindPorosity);
        foreach (GapDef g in asset.Gaps)
        {
            Axes facing = r.Compose(Axes.FromEuler(g.Yaw, 0, 0));
            Append(ref _gaps, ref _gapCount, new Gap
            {
                Center = position + r.ToWorld(g.Center) * scale,
                Normal = facing.Z.ToVector3(),
                Up = facing.Y.ToVector3(),
                Width = (float)(g.Width * scale),
                Height = (float)(g.Height * scale),
                Object = index,
                Name = g.Name,
            });
        }
        return index;
    }

    static Prim MakePrim(ShapeDef d, Double3 origin, in Axes r, double scale, int obj, int shape)
    {
        var p = new Prim
        {
            Kind = d.Kind,
            Shape = (ushort)shape,
            Material = d.Material,
            Object = obj,
            C = origin + r.ToWorld(d.Position) * scale,
            R = r.Compose(Axes.FromEuler(d.Yaw, d.Pitch, d.Roll)),
            Radius = d.Radius * scale,
            Half = d.Kind switch
            {
                ShapeKind.Box => d.Size * (0.5 * scale),
                ShapeKind.Capsule => new Double3(0, Math.Max(d.Height / 2 - d.Radius, 0) * scale, 0),
                ShapeKind.Cylinder => new Double3(0, d.Height / 2 * scale, 0),
                _ => default,
            },
        };
        Double3 x = p.R.X, y = p.R.Y, z = p.R.Z, h = p.Half;
        double radius = p.Radius;
        Double3 extent = p.Kind switch
        {
            ShapeKind.Box => new Double3(Math.Abs(x.X) * h.X + Math.Abs(y.X) * h.Y + Math.Abs(z.X) * h.Z,
                Math.Abs(x.Y) * h.X + Math.Abs(y.Y) * h.Y + Math.Abs(z.Y) * h.Z, Math.Abs(x.Z) * h.X + Math.Abs(y.Z) * h.Y + Math.Abs(z.Z) * h.Z),
            ShapeKind.Sphere => new Double3(radius, radius, radius),
            ShapeKind.Capsule => new Double3(Math.Abs(y.X) * h.Y + radius, Math.Abs(y.Y) * h.Y + radius, Math.Abs(y.Z) * h.Y + radius),
            _ => new Double3(Math.Abs(y.X) * h.Y + radius * Math.Sqrt(Math.Max(1 - y.X * y.X, 0)),
                Math.Abs(y.Y) * h.Y + radius * Math.Sqrt(Math.Max(1 - y.Y * y.Y, 0)),
                Math.Abs(y.Z) * h.Y + radius * Math.Sqrt(Math.Max(1 - y.Z * y.Z, 0))),
        };
        p.Box = new Bounds { Min = p.C - extent, Max = p.C + extent };
        return p;
    }

    /// A wire is the polyline of its sagged spans: each span from point a to b drops 4·sag·t·(1 − t) below the straight
    /// line at t = 0–1, in ⌈|b − a| / segment⌉ equal steps of t (the catalog's capsule_chain segment length).
    int PlaceWire(AssetDef asset, ReadOnlySpan<Double3> points, double sag, double diameter)
    {
        int index = ObjectCount++, first = _wirePointCount, firstAnchor = _wireAnchorCount;
        StartObject(index);
        double along = 0;
        AddWirePoint(points[0], 0);
        Append(ref _wireAnchors, ref _wireAnchorCount, first);
        for (int i = 0; i + 1 < points.Length; i++)
        {
            Double3 a = points[i], b = points[i + 1];
            int steps = Math.Max(1, (int)Math.Ceiling((b - a).Length() / asset.WireSegment));
            for (int k = 1; k <= steps; k++)
            {
                double t = (double)k / steps;
                Double3 p = k == steps ? b : a + (b - a) * t;
                p.Y -= 4 * sag * t * (1 - t);
                along += (p - _wirePoints[_wirePointCount - 1]).Length();
                AddWirePoint(p, along);
            }
            Append(ref _wireAnchors, ref _wireAnchorCount, _wirePointCount - 1);
        }
        var wire = new Prim
        {
            Kind = ShapeKind.Wire,
            Material = asset.Material,
            Object = index,
            Radius = diameter / 2,
            First = first,
            Count = _wirePointCount - first,
            FirstAnchor = firstAnchor,
            Anchors = _wireAnchorCount - firstAnchor,
            Sag = sag,
            Box = Bounds.Of(points[0], points[0], diameter / 2),
        };
        for (int i = first; i < _wirePointCount; i++)
            wire.Box = wire.Box.Union(Bounds.Of(_wirePoints[i], _wirePoints[i], diameter / 2));
        Append(ref _prims, ref _primCount, wire);
        return index;
    }

    void AddWirePoint(Double3 p, double along)
    {
        if (_wirePointCount == _wirePoints.Length)
        {
            Array.Resize(ref _wirePoints, _wirePointCount * 2);
            Array.Resize(ref _wireAlong, _wirePointCount * 2);
        }
        _wirePoints[_wirePointCount] = p;
        _wireAlong[_wirePointCount++] = along;
    }

    /// Object `index` starts at the next primitive.
    void StartObject(int index)
    {
        if (index == _objectPrim.Length)
            Array.Resize(ref _objectPrim, index * 2);
        _objectPrim[index] = _primCount;
    }

    static void Append<T>(ref T[] array, ref int count, in T item)
    {
        if (count == array.Length)
            Array.Resize(ref array, count * 2);
        array[count++] = item;
    }

    /// The broadphase cell range of a box, clamped to the map (a query outside it looks at the edge cells).
    void BroadRange(in Bounds b, out int x0, out int x1, out int z0, out int z1)
    {
        x0 = BroadIndex(b.Min.X);
        x1 = BroadIndex(b.Max.X);
        z0 = BroadIndex(b.Min.Z);
        z1 = BroadIndex(b.Max.Z);
    }

    /// NaN goes to cell 0.
    int BroadIndex(double v)
    {
        double f = Math.Floor((v + Half) / BroadCell);
        return f >= _broadCells - 1 ? _broadCells - 1 : f >= 0 ? (int)f : 0;
    }

    void RebuildBroadphase()
    {
        int n = _broadCells;
        var start = new int[n * n + 1];
        for (int pass = 0; pass < 2; pass++)
        {
            var fill = pass == 0 ? null : (int[])start.Clone();
            for (int i = 0; i < _primCount; i++)
            {
                ref Prim p = ref _prims[i];
                BroadRange(p.Box, out int x0, out int x1, out int z0, out int z1);
                (p.CellX, p.CellZ) = (x0, z0);
                for (int cz = z0; cz <= z1; cz++)
                {
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        if (pass == 0)
                            start[cz * n + cx + 1]++;
                        else
                            _cellItems[fill[cz * n + cx]++] = i;
                    }
                }
            }
            if (pass == 0)
            {
                for (int c = 0; c < n * n; c++)
                    start[c + 1] += start[c];
                _cellItems = new int[start[n * n]];
            }
        }
        _cellStart = start;
    }

    /// World-query W-10: every fly-through gap whose rectangle comes within `radius` (m) of `center`, in canonical order
    /// (object, gap). Returns the true count, which exceeds `results.Length` on overflow.
    public int GapsNear(Double3 center, double radius, Span<Gap> results)
    {
        int count = 0;
        for (int i = 0; i < _gapCount; i++)
        {
            ref readonly Gap g = ref _gaps[i];
            Double3 normal = new(g.Normal.X, g.Normal.Y, g.Normal.Z), up = new(g.Up.X, g.Up.Y, g.Up.Z);
            Double3 across = Double3.Cross(up, normal), v = center - g.Center;
            Double3 off = v - across * Limit(Double3.Dot(v, across), g.Width / 2.0) - up * Limit(Double3.Dot(v, up), g.Height / 2.0);
            if (!(Double3.Dot(off, off) <= radius * radius))
                continue;
            if (count < results.Length)
                results[count] = g;
            count++;
        }
        return count;
    }

    /// World-query "Shape and wire geometry": shape `shape` of object `obj` (a StaticContact's Object and Shape) in world
    /// space, and for a wire the span that holds `wireParam` (the contact's WireParam). Read-only and allocation-free.
    /// Returns false, with `geometry` empty, when the object has no such collision shape: terrain (−1), a visual-only
    /// object, or an index out of range.
    public bool Geometry(int obj, int shape, double wireParam, out ShapeGeometry geometry)
    {
        geometry = default;
        if (obj < 0 || obj >= ObjectCount || shape < 0
            || _objectPrim[obj] + shape >= (obj + 1 < ObjectCount ? _objectPrim[obj + 1] : _primCount))
            return false;
        ref readonly Prim s = ref _prims[_objectPrim[obj] + shape];
        double r = s.Radius;
        (geometry.Kind, geometry.Material, geometry.Radius) = (s.Kind, s.Material, r);
        if (s.Kind != ShapeKind.Wire)
        {
            (geometry.Center, geometry.Axes) = (s.C, s.R);
            geometry.HalfExtents = s.Kind switch
            {
                ShapeKind.Box => s.Half,
                ShapeKind.Sphere => new Double3(r, r, r),
                ShapeKind.Capsule => new Double3(r, s.Half.Y + r, r),
                _ => new Double3(r, s.Half.Y, r),
            };
            geometry.Height = s.Kind is ShapeKind.Capsule or ShapeKind.Cylinder ? 2 * geometry.HalfExtents.Y : 0;
            return true;
        }
        // The span whose stretch of the polyline holds the parameter's length along the wire, then the piece of it.
        double along = Clamp01(wireParam) * _wireAlong[s.First + s.Count - 1];
        int k = s.FirstAnchor;
        while (k + 2 < s.FirstAnchor + s.Anchors && _wireAlong[_wireAnchors[k + 1]] < along)
            k++;
        int a = _wireAnchors[k], b = _wireAnchors[k + 1], i = a;
        while (i + 1 < b && _wireAlong[i + 1] < along)
            i++;
        double piece = _wireAlong[i + 1] - _wireAlong[i];
        (geometry.SpanA, geometry.SpanB) = (_wirePoints[a], _wirePoints[b]);
        geometry.SpanLength = (geometry.SpanB - geometry.SpanA).Length();
        geometry.Sag = s.Sag;
        geometry.Diameter = 2 * r;
        geometry.SpanT = (i - a + (piece > 0 ? Clamp01((along - _wireAlong[i]) / piece) : 0)) / (b - a);
        return true;
    }
}
