// MapWorld: the world of a map's placement file (p6-world-query-gallery task 1.4, design
// 4 and 5; p6-test-range design 7, D-044, D-047), behind IWorldQuery. It is the test
// range's world, the release's one map: maps/test-range/placements.json and the
// .assetmeta.json of every variant it places (MapWorldLoad.cs reads them).
// - Objects: placed object i is the file's instance row i, named by its variant (such as
//   "L01_straight"). Its shapes are its metadata's collision shapes in order, then its
//   wires in order, each in world space at the row's position and yaw. Runtime objects
//   (the launch stand) follow the placed ones.
// - Shapes: boxes, spheres, capsules and cylinders exactly as the metadata gives them; a
//   wire is a capsule chain along its point list at half its diameter (design 4).
// - The ground (design 5, review-physics W3, W4 option a): the plane of the file's
//   ground record, absent over each ground patch's footprint (S04, E01) and inside each
//   hole band (Z04's shaft, E01's run and niches), where the shapes there are the only
//   ground. SampleGround reports Hole at such a point; StaticContacts and Raycast keep
//   the plane's contact or hit only where its point lies outside them.
// - The index: a uniform 2 m grid over the file's extent (design 4), and a tree inside each
//   object of more than TreeShapes shapes (D-054's broad phase inside each object: H01's
//   houses, E01, V01's crowns). The grid lists, in canonical order, each such object by its
//   bounds and every other shape by its own, in every cell they meet (clamped to the edge
//   cells); a query visits each once, in the first cell it shares with the query's bounds,
//   and an object's tree leads it through the nodes whose bounds meet its own to its
//   shapes. Every shape is tested after a bounds check (the AABB early reject that
//   p6-flight-core 1.7 asked for), so a query tests exactly the shapes whose bounds meet
//   its own, however it finds them, and results are in canonical order. A sparse object
//   (S04's patch of long, crossing strips) stays in the grid, where its tree would visit
//   more nodes than the grid walks shapes. Runtime objects are checked by their bounds
//   first.
// - Ids: materials are the names given at load (physics_materials.json's rows in file
//   order, 1 … n); surfaces are S01–S13 (SurfaceIds, 1 … 13).
// - Identity (design 6): FNV-1a over the placement file, every referenced .assetmeta.json
//   (by path, ordinal order) and the material names, CRLF read as LF; QueryVersion
//   beside it. Runtime objects are outside it (the log records them).
// Engine-free, allocation-free after loading, reentrant: queries change nothing.
// New in this change; its sweep, grid walk and wire chain follow refs/godot/main's
// Contacts.cs and WorldObjects.cs (8 m broadphase cells there, 2 m here; see ShapeMath).

using System;

namespace Fpv450.Sim.World
{
    public sealed partial class MapWorld : IWorldQuery
    {
        /// <summary>The index's cell side, m (design 4).</summary>
        public const double CellSize = 2.0;

        /// <summary>
        /// The ground surfaces S01–S13 (knowledge §7.2; tools/blender/lib/meta.py SURFACE_IDS): surface id i + 1 is
        /// SurfaceIds[i], so S04 belt_straw is 4. A metadata shape names one as "surface:&lt;id&gt;".
        /// </summary>
        public static readonly string[] SurfaceIds =
        {
            "meadow_sod", "dry_crust", "crater_spoil", "belt_straw", "belt_bare", "tilled", "burnt_field",
            "weeds", "yard_litter", "rubble", "asphalt", "concrete_slab", "dirt_track",
        };

        /// <summary>An object gets a tree above this many shapes; its leaves hold LeafSize; the queries' stack holds MaxDepth.</summary>
        private const int TreeShapes = 256, LeafSize = 4, MaxDepth = 48;

        /// <summary>
        /// A node of an object's tree: its prims' bounds; an inner node's first child is the next node and its second is
        /// Right; a leaf's prims are _leafPrims[First .. First + Count).
        /// </summary>
        private struct Node
        {
            public Bounds Box;
            public int Right, First, Count;
        }

        /// <summary>A ground patch's footprint (BandCount 0) or a hole band (its centre line in _band, half its width).</summary>
        private struct Gap
        {
            public double X0, Z0, X1, Z1;
            public int BandFirst, BandCount;
            public double Radius;
        }

        private readonly string[] _materials; // id − 1
        private readonly ShapeGeometry _ground;
        private readonly Prim[] _prims;        // the placed objects' shapes, object by object
        private readonly int[] _first;         // each placed object's first shape in _prims; one more entry, the end
        private readonly string[] _names;      // each placed object's variant
        private readonly Double3[] _wirePoints;
        private readonly double[] _wireAlong;  // length along its wire from the wire's first point, m
        private readonly double[] _wireProj;   // distance along its wire's chord (AxisY) from the first point, m, rising
        private readonly double _gridX, _gridZ;
        private readonly int _nx, _nz;
        private readonly int[] _cellStart, _cellItems; // per cell, row-major from (_gridX, _gridZ): shape i, or ~o for object o's tree
        private readonly Bounds[] _objBox;             // a tree object's bounds (its root's)
        private readonly int[] _objCellX, _objCellZ;   // the cell of a tree object's bounds' minimum corner
        private readonly int[] _objRoot;               // each object's root node; -1 for none (its shapes are in the grid)
        private readonly Node[] _nodes;
        private readonly int[] _leafPrims;             // the trees' prims, leaf by leaf
        private readonly Gap[] _gaps;
        private readonly XZ[] _band;
        private readonly int[] _gapStart, _gapItems;

        private Prim[] _runtime = new Prim[8];
        private int _runtimeCount;
        private int[] _runtimeFirst = new int[4];
        private Bounds[] _runtimeBox = new Bounds[4];
        private string[] _runtimeNames = new string[4];
        private int _runtimeObjects;

        public ulong ContentHash { get; }

        public int QueryVersion => WorldHash.QueryVersion;

        /// <summary>The map's id (the file's "map").</summary>
        public string MapId { get; }

        /// <summary>The placed objects' count: runtime objects get the ids from here on.</summary>
        public int PlacedObjects => _names.Length;

        /// <summary>The map's start points (WLD-08, D-022), in the file's order.</summary>
        public StartPoint[] StartPoints { get; }

        // ---- SampleGround ------------------------------------------------------------------------------------------

        public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results)
        {
            for (int i = 0; i < points.Length; i++)
            {
                results[i] = InGap(points[i].X, points[i].Z)
                    ? new GroundSample { Height = double.NegativeInfinity, Normal = new Double3(0, 1, 0), Surface = WorldIds.NoSurface, Flags = GroundFlags.Hole }
                    : new GroundSample { Height = _ground.Center.Y, Normal = _ground.AxisY, Surface = _ground.Surface, Flags = GroundFlags.None };
            }
        }

        /// <summary>Whether (x, z) lies over a ground patch or inside a hole band (closed: an edge belongs to it).</summary>
        private bool InGap(double x, double z)
        {
            int cell = CellZ(z) * _nx + CellX(x);
            for (int k = _gapStart[cell]; k < _gapStart[cell + 1]; k++)
            {
                ref readonly Gap g = ref _gaps[_gapItems[k]];
                if (!(x >= g.X0 && x <= g.X1 && z >= g.Z0 && z <= g.Z1))
                    continue;
                if (g.BandCount == 0)
                    return true;
                double rr = g.Radius * g.Radius;
                for (int i = g.BandFirst; i < g.BandFirst + g.BandCount - 1; i++)
                {
                    XZ a = _band[i], b = _band[i + 1];
                    double dx = b.X - a.X, dz = b.Z - a.Z, px = x - a.X, pz = z - a.Z, dd = dx * dx + dz * dz;
                    double t = dd > 0 ? (px * dx + pz * dz) / dd : 0;
                    t = t > 0 ? t < 1 ? t : 1 : 0;
                    double ex = px - dx * t, ez = pz - dz * t;
                    if (ex * ex + ez * ez <= rr)
                        return true;
                }
            }
            return false;
        }

        // ---- StaticContacts ----------------------------------------------------------------------------------------

        public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results)
        {
            double pad = current.Radius + margin;
            Bounds reach = Bounds.Of(previous.A, previous.B, pad).Union(Bounds.Of(current.A, current.B, pad));
            double speed = DetMath.Max((current.A - previous.A).Length(), (current.B - previous.B).Length());
            int count = 0;
            // The ground answers only where its contact point lies outside every patch and band (W3: per contact point).
            if (ShapeMath.Sweep(_ground, previous, current, margin, speed, out Near near, out double time) && !InGap(near.Point.X, near.Point.Z))
                ShapeMath.Insert(results, ref count, ContactOf(near, time, WorldIds.Ground, 0, _ground));

            Span<int> stack = stackalloc int[MaxDepth + 2];
            CellRange(reach, out int x0, out int x1, out int z0, out int z1);
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cz * _nx + cx;
                    for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    {
                        int item = _cellItems[k];
                        if (item >= 0)
                        {
                            ref readonly Prim p = ref _prims[item];
                            if ((p.CellX > x0 ? p.CellX : x0) == cx && (p.CellZ > z0 ? p.CellZ : z0) == cz && p.Box.Overlaps(reach))
                                SweepPrim(item, previous, current, margin, speed, reach, results, ref count);
                            continue;
                        }
                        int o = ~item;
                        if ((_objCellX[o] > x0 ? _objCellX[o] : x0) != cx || (_objCellZ[o] > z0 ? _objCellZ[o] : z0) != cz || !_objBox[o].Overlaps(reach))
                            continue;
                        int top = 0;
                        stack[top++] = _objRoot[o];
                        while (top > 0)
                        {
                            int at = stack[--top];
                            ref readonly Node node = ref _nodes[at];
                            if (!node.Box.Overlaps(reach))
                                continue;
                            if (node.Count == 0)
                            {
                                stack[top++] = node.Right;
                                stack[top++] = at + 1;
                                continue;
                            }
                            for (int m = node.First; m < node.First + node.Count; m++)
                                if (_prims[_leafPrims[m]].Box.Overlaps(reach))
                                    SweepPrim(_leafPrims[m], previous, current, margin, speed, reach, results, ref count);
                        }
                    }
                }
            }
            for (int r = 0; r < _runtimeObjects; r++)
            {
                if (!_runtimeBox[r].Overlaps(reach))
                    continue;
                for (int k = _runtimeFirst[r], end = RuntimeEnd(r); k < end; k++)
                {
                    ref readonly Prim p = ref _runtime[k];
                    if (p.Box.Overlaps(reach) && ShapeMath.Sweep(new PrimClosest(_runtime, k), previous, current, margin, speed, out near, out time))
                        ShapeMath.Insert(results, ref count, ContactOf(near, time, p.Object, p.Shape, p.Geometry));
                }
            }
            return count;
        }

        /// <summary>Placed shape i's sweep, its contact inserted in canonical order.</summary>
        private void SweepPrim(int i, in Capsule previous, in Capsule current, double margin, double speed, in Bounds reach,
            Span<StaticContact> results, ref int count)
        {
            ref readonly Prim p = ref _prims[i];
            bool hit;
            Near near = default;
            double time = 1;
            if (p.Geometry.Kind == ShapeKind.Wire)
            {
                ShapeMath.WireRange(_wireProj, p.WireFirst, p.WireCount, _wirePoints[p.WireFirst], p.Geometry.AxisY, p.Geometry.Radius, reach,
                    out int lo, out int hi);
                hit = lo <= hi && ShapeMath.Sweep(new WireChain(_wirePoints, _wireAlong, p.WireFirst, p.WireCount, p.Geometry.Radius, lo, hi, reach),
                    previous, current, margin, speed, out near, out time);
            }
            else
                hit = ShapeMath.Sweep(new PrimClosest(_prims, i), previous, current, margin, speed, out near, out time);
            if (hit)
                ShapeMath.Insert(results, ref count, ContactOf(near, time, p.Object, p.Shape, p.Geometry));
        }

        private static StaticContact ContactOf(in Near near, double time, int obj, int shape, in ShapeGeometry s) => new StaticContact
        {
            Point = near.Point,
            Normal = near.Normal,
            Distance = near.Distance,
            Object = obj,
            Shape = shape,
            Material = s.Material,
            Surface = s.Surface,
            WireParam = near.WireParam,
            Time = time,
        };

        // ---- Raycast -----------------------------------------------------------------------------------------------

        public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits)
        {
            for (int i = 0; i < rays.Length; i++)
                hits[i] = Cast(rays[i], maxDistance);
        }

        private RayHit Cast(in Ray ray, double maxDistance)
        {
            var hit = new RayHit { Distance = double.PositiveInfinity, Object = WorldIds.Ground };
            Double3 o = ray.Origin, d = ShapeMath.Unit(ray.Direction);
            Bounds reach = Bounds.Of(o, o + d * maxDistance, 0);
            double best = maxDistance;
            Double3 normal = default;
            bool found = false;
            double t = double.PositiveInfinity;
            Double3 n = default;
            // The ground from above only, and never over a patch or a band: the ray goes on to the shapes there.
            ShapeMath.RayPrim(_ground, o, d, ref t, ref n);
            if (t <= best)
            {
                Double3 at = o + d * t;
                if (!InGap(at.X, at.Z))
                {
                    found = true;
                    best = t;
                    normal = n;
                    hit.Surface = _ground.Surface;
                }
            }

            Span<int> stack = stackalloc int[MaxDepth + 2];
            CellRange(reach, out int x0, out int x1, out int z0, out int z1);
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cz * _nx + cx;
                    for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                    {
                        int item = _cellItems[k];
                        if (item >= 0)
                        {
                            ref readonly Prim p = ref _prims[item];
                            if ((p.CellX > x0 ? p.CellX : x0) == cx && (p.CellZ > z0 ? p.CellZ : z0) == cz && p.Box.Overlaps(reach))
                                RayPrim(item, o, d, reach, ref found, ref best, ref normal, ref hit);
                            continue;
                        }
                        int ob = ~item;
                        if ((_objCellX[ob] > x0 ? _objCellX[ob] : x0) != cx || (_objCellZ[ob] > z0 ? _objCellZ[ob] : z0) != cz || !_objBox[ob].Overlaps(reach))
                            continue;
                        int top = 0;
                        stack[top++] = _objRoot[ob];
                        while (top > 0)
                        {
                            int at = stack[--top];
                            ref readonly Node node = ref _nodes[at];
                            if (!node.Box.Overlaps(reach))
                                continue;
                            if (node.Count == 0)
                            {
                                stack[top++] = node.Right;
                                stack[top++] = at + 1;
                                continue;
                            }
                            for (int m = node.First; m < node.First + node.Count; m++)
                                if (_prims[_leafPrims[m]].Box.Overlaps(reach))
                                    RayPrim(_leafPrims[m], o, d, reach, ref found, ref best, ref normal, ref hit);
                        }
                    }
                }
            }
            for (int r = 0; r < _runtimeObjects; r++)
            {
                if (!_runtimeBox[r].Overlaps(reach))
                    continue;
                for (int k = _runtimeFirst[r], end = RuntimeEnd(r); k < end; k++)
                {
                    ref readonly Prim p = ref _runtime[k];
                    if (!p.Box.Overlaps(reach))
                        continue;
                    t = double.PositiveInfinity;
                    ShapeMath.RayPrim(p.Geometry, o, d, ref t, ref n);
                    Take(p, t, n, ref found, ref best, ref normal, ref hit);
                }
            }

            if (found)
            {
                hit.Distance = best;
                hit.Point = o + d * best;
                hit.Normal = normal;
            }
            return hit;
        }

        /// <summary>Placed shape i's entry by the ray o + t·d, kept by Take.</summary>
        private void RayPrim(int i, Double3 o, Double3 d, in Bounds reach, ref bool found, ref double best, ref Double3 normal, ref RayHit hit)
        {
            ref readonly Prim p = ref _prims[i];
            double t = double.PositiveInfinity;
            Double3 n = default;
            if (p.Geometry.Kind == ShapeKind.Wire)
            {
                ShapeMath.WireRange(_wireProj, p.WireFirst, p.WireCount, _wirePoints[p.WireFirst], p.Geometry.AxisY, p.Geometry.Radius, reach,
                    out int lo, out int hi);
                if (lo <= hi)
                    ShapeMath.WireRay(new WireChain(_wirePoints, _wireAlong, p.WireFirst, p.WireCount, p.Geometry.Radius, lo, hi, reach), o, d, ref t, ref n);
            }
            else
                ShapeMath.RayPrim(p.Geometry, o, d, ref t, ref n);
            Take(p, t, n, ref found, ref best, ref normal, ref hit);
        }

        /// <summary>Keeps a nearer entry; a tie keeps the first in canonical order (the ground, then object, then shape).</summary>
        private static void Take(in Prim p, double t, Double3 n, ref bool found, ref double best, ref Double3 normal, ref RayHit hit)
        {
            if (!(t <= best))
                return;
            // t == best with a hit already found is a tie: the ground (object −1) or the earlier (object, shape) stays.
            if (found && !(t < best) && (hit.Object == WorldIds.Ground || p.Object > hit.Object || p.Object == hit.Object && p.Shape >= hit.Shape))
                return;
            found = true;
            best = t;
            normal = n;
            hit.Object = p.Object;
            hit.Shape = p.Shape;
            hit.Material = p.Geometry.Material;
            hit.Surface = p.Geometry.Surface;
        }

        // ---- Geometry, runtime objects and names -------------------------------------------------------------------

        public bool Geometry(int obj, int shape, out ShapeGeometry geometry)
        {
            if (obj == WorldIds.Ground && shape == 0)
            {
                geometry = _ground;
                return true;
            }
            int placed = _names.Length;
            if (obj >= 0 && obj < placed && shape >= 0 && _first[obj] + shape < _first[obj + 1])
            {
                geometry = _prims[_first[obj] + shape].Geometry;
                return true;
            }
            int r = obj - placed;
            if (r >= 0 && r < _runtimeObjects && shape >= 0 && _runtimeFirst[r] + shape < RuntimeEnd(r))
            {
                geometry = _runtime[_runtimeFirst[r] + shape].Geometry;
                return true;
            }
            geometry = default;
            return false;
        }

        /// <summary>
        /// A placed wire's chain in world space (its 0.5 m point list, review-physics W2), or an empty span for any other
        /// (object, shape). WireParam p lies at length p × (the chain's length) along it.
        /// </summary>
        public ReadOnlySpan<Double3> WirePoints(int obj, int shape)
        {
            if (obj < 0 || obj >= _names.Length || shape < 0 || _first[obj] + shape >= _first[obj + 1])
                return ReadOnlySpan<Double3>.Empty;
            ref readonly Prim p = ref _prims[_first[obj] + shape];
            return p.Geometry.Kind == ShapeKind.Wire ? new ReadOnlySpan<Double3>(_wirePoints, p.WireFirst, p.WireCount) : ReadOnlySpan<Double3>.Empty;
        }

        public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("a runtime object needs a name", nameof(name));
            for (int i = 0; i < shapes.Length; i++)
            {
                string problem = ShapeMath.RuntimeShapeProblem(shapes[i], _materials.Length, SurfaceIds.Length);
                if (problem != null)
                    throw new ArgumentException($"shape {i} {problem}", nameof(shapes));
            }
            int r = _runtimeObjects;
            if (r == _runtimeFirst.Length)
            {
                Array.Resize(ref _runtimeFirst, 2 * r);
                Array.Resize(ref _runtimeNames, 2 * r);
                Array.Resize(ref _runtimeBox, 2 * r);
            }
            _runtimeFirst[r] = _runtimeCount;
            _runtimeNames[r] = name;
            _runtimeBox[r] = Bounds.Empty;
            _runtimeObjects++;
            int obj = _names.Length + r;
            for (int i = 0; i < shapes.Length; i++)
            {
                if (_runtimeCount == _runtime.Length)
                    Array.Resize(ref _runtime, 2 * _runtimeCount);
                _runtime[_runtimeCount++] = new Prim { Geometry = shapes[i], Box = Bounds.OfShape(shapes[i]), Object = obj, Shape = i };
                _runtimeBox[r] = _runtimeBox[r].Union(_runtime[_runtimeCount - 1].Box);
            }
            return obj;
        }

        /// <summary>One past runtime object r's last prim.</summary>
        private int RuntimeEnd(int r) => r + 1 < _runtimeObjects ? _runtimeFirst[r + 1] : _runtimeCount;

        public void ResetRuntimeObjects()
        {
            _runtimeObjects = 0;
            _runtimeCount = 0;
        }

        public string ObjectName(int obj)
        {
            if (obj >= 0 && obj < _names.Length)
                return _names[obj];
            int r = obj - _names.Length;
            return r >= 0 && r < _runtimeObjects ? _runtimeNames[r] : null;
        }

        public string MaterialName(ushort material) =>
            material != WorldIds.NoMaterial && material <= _materials.Length ? _materials[material - 1] : null;

        public ushort MaterialId(string name)
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                if (string.Equals(_materials[i], name, StringComparison.Ordinal))
                    return (ushort)(i + 1);
            }
            return WorldIds.NoMaterial;
        }

        public string SurfaceName(byte surface) =>
            surface != WorldIds.NoSurface && surface <= SurfaceIds.Length ? SurfaceIds[surface - 1] : null;

        /// <summary>The surface id of a name of SurfaceIds; NoSurface for any other.</summary>
        public static byte SurfaceId(string name)
        {
            for (int i = 0; i < SurfaceIds.Length; i++)
            {
                if (string.Equals(SurfaceIds[i], name, StringComparison.Ordinal))
                    return (byte)(i + 1);
            }
            return WorldIds.NoSurface;
        }

        // ---- The grid ----------------------------------------------------------------------------------------------

        // Monotone in x, so a box's cells and its points' cells agree; outside the extent the edge cell; NaN cell 0.
        private int CellX(double x)
        {
            double f = (x - _gridX) * (1 / CellSize);
            return f >= _nx - 1 ? _nx - 1 : f >= 0 ? (int)f : 0;
        }

        private int CellZ(double z)
        {
            double f = (z - _gridZ) * (1 / CellSize);
            return f >= _nz - 1 ? _nz - 1 : f >= 0 ? (int)f : 0;
        }

        private void CellRange(in Bounds b, out int x0, out int x1, out int z0, out int z1)
        {
            x0 = CellX(b.Min.X);
            x1 = CellX(b.Max.X);
            z0 = CellZ(b.Min.Z);
            z1 = CellZ(b.Max.Z);
        }
    }

    /// <summary>A start point of the map (its placement file's start_points): where a flight begins.</summary>
    public readonly struct StartPoint
    {
        public readonly string Id;
        /// <summary>On the ground, m.</summary>
        public readonly Double3 Position;
        /// <summary>D-019's yaw: positive turns north towards east, degrees.</summary>
        public readonly double HeadingDeg;
        /// <summary>Take-off with the legs on (from the ground) or off (from the launch stand, D-022).</summary>
        public readonly bool LegsOn;
        /// <summary>The launch stand stands here.</summary>
        public readonly bool Stand;

        public StartPoint(string id, Double3 position, double headingDeg, bool legsOn, bool stand)
        {
            Id = id;
            Position = position;
            HeadingDeg = headingDeg;
            LegsOn = legsOn;
            Stand = stand;
        }
    }
}
