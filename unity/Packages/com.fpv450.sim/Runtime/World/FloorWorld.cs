// FloorWorld: the stub world of p6-world-query-gallery task 1.1. An endless floor plane
// with one surface, plus runtime objects (the launch stand), behind IWorldQuery. The
// flight core's tests run on it until the gallery world lands (task 1.4).
// - The floor is the ground (WorldIds.Ground, shape 0): SampleGround, StaticContacts
//   and Raycast all see it, as a half-space below y = height with normal +Y.
// - Runtime objects are tested one by one in canonical order, with a bounds check: a
//   stand has a handful of shapes, so there is no broadphase (the gallery's 2 m grid is
//   task 1.4's).
// - The content hash covers the constructor's arguments, so two stubs that answer
//   differently never share one. Runtime objects are outside it (the log records them).
// New in this change; its geometry is ShapeMath's (ported, see there).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fpv450.Sim.World
{
    public sealed class FloorWorld : IWorldQuery
    {
        private const byte FloorSurface = 1;

        private readonly double _height;
        private readonly string _surfaceName;
        private readonly string[] _materials; // id − 1
        private readonly ShapeGeometry _floor;
        private Prim[] _prims = new Prim[8];
        private int _primCount;
        private int[] _objectFirst = new int[4]; // each runtime object's first primitive; its shapes follow in order
        private string[] _objectNames = new string[4];
        private int _objectCount;

        /// <summary>
        /// A floor at `height` (m) of surface `floorSurface` (its id is 1), and the
        /// material names its runtime objects may use (their ids are 1, 2, … in this order).
        /// </summary>
        public FloorWorld(double height, string floorSurface, params string[] materials)
        {
            if (string.IsNullOrEmpty(floorSurface))
                throw new ArgumentException("the floor needs a surface name", nameof(floorSurface));
            if (materials.Length >= ushort.MaxValue)
                throw new ArgumentException("too many materials", nameof(materials));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string m in materials)
            {
                if (string.IsNullOrEmpty(m) || !seen.Add(m))
                    throw new ArgumentException($"material names must be non-empty and unique: '{m}'", nameof(materials));
            }
            _height = height;
            _surfaceName = floorSurface;
            _materials = (string[])materials.Clone();
            _floor = new ShapeGeometry
            {
                Kind = ShapeKind.Plane,
                Center = new Double3(0, height, 0),
                AxisX = new Double3(1, 0, 0),
                AxisY = new Double3(0, 1, 0),
                AxisZ = new Double3(0, 0, 1),
                Material = WorldIds.NoMaterial,
                Surface = FloorSurface,
            };
            var text = new StringBuilder("FloorWorld\nheight ");
            text.Append(BitConverter.DoubleToInt64Bits(height).ToString("X16", CultureInfo.InvariantCulture));
            text.Append("\nsurface ").Append(floorSurface);
            foreach (string m in materials)
                text.Append("\nmaterial ").Append(m);
            ContentHash = WorldHash.HashFile(WorldHash.Offset, Encoding.UTF8.GetBytes(text.ToString()), true);
        }

        public ulong ContentHash { get; }

        public int QueryVersion => WorldHash.QueryVersion;

        public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results)
        {
            for (int i = 0; i < points.Length; i++)
            {
                results[i] = new GroundSample
                {
                    Height = _height,
                    Normal = new Double3(0, 1, 0),
                    Surface = FloorSurface,
                    Flags = GroundFlags.None,
                };
            }
        }

        public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results)
        {
            double pad = current.Radius + margin;
            Bounds reach = Bounds.Of(previous.A, previous.B, pad).Union(Bounds.Of(current.A, current.B, pad));
            double speed = DetMath.Max((current.A - previous.A).Length(), (current.B - previous.B).Length());
            int count = 0;
            if (ShapeMath.Sweep(_floor, previous, current, margin, speed, out Near near, out double time))
                ShapeMath.Insert(results, ref count, ContactOf(near, time, WorldIds.Ground, 0, _floor));
            for (int k = 0; k < _primCount; k++)
            {
                ref readonly Prim p = ref _prims[k];
                if (p.Box.Overlaps(reach) && ShapeMath.Sweep(p.Geometry, previous, current, margin, speed, out near, out time))
                    ShapeMath.Insert(results, ref count, ContactOf(near, time, p.Object, p.Shape, p.Geometry));
            }
            return count;
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
            ShapeMath.RayPrim(_floor, o, d, ref t, ref n);
            if (t <= best)
            {
                found = true;
                best = t;
                normal = n;
                hit.Surface = FloorSurface;
            }
            for (int k = 0; k < _primCount; k++)
            {
                ref readonly Prim p = ref _prims[k];
                if (!p.Box.Overlaps(reach))
                    continue;
                t = double.PositiveInfinity;
                ShapeMath.RayPrim(p.Geometry, o, d, ref t, ref n);
                if (t <= best && (!found || t < best)) // a tie keeps the first in canonical order
                {
                    found = true;
                    best = t;
                    normal = n;
                    hit.Object = p.Object;
                    hit.Shape = p.Shape;
                    hit.Material = p.Geometry.Material;
                    hit.Surface = p.Geometry.Surface;
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

        public bool Geometry(int obj, int shape, out ShapeGeometry geometry)
        {
            if (obj == WorldIds.Ground && shape == 0)
            {
                geometry = _floor;
                return true;
            }
            if (obj >= 0 && obj < _objectCount && shape >= 0
                && _objectFirst[obj] + shape < (obj + 1 < _objectCount ? _objectFirst[obj + 1] : _primCount))
            {
                geometry = _prims[_objectFirst[obj] + shape].Geometry;
                return true;
            }
            geometry = default;
            return false;
        }

        public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("a runtime object needs a name", nameof(name));
            for (int i = 0; i < shapes.Length; i++)
                Check(shapes[i], i);
            int obj = _objectCount;
            if (obj == _objectFirst.Length)
            {
                Array.Resize(ref _objectFirst, 2 * obj);
                Array.Resize(ref _objectNames, 2 * obj);
            }
            _objectFirst[obj] = _primCount;
            _objectNames[obj] = name;
            _objectCount++;
            for (int i = 0; i < shapes.Length; i++)
            {
                if (_primCount == _prims.Length)
                    Array.Resize(ref _prims, 2 * _primCount);
                _prims[_primCount++] = new Prim { Geometry = shapes[i], Box = Bounds.OfShape(shapes[i]), Object = obj, Shape = i };
            }
            return obj;
        }

        private void Check(in ShapeGeometry s, int index)
        {
            string problem = null;
            if (s.Kind != ShapeKind.Box && s.Kind != ShapeKind.Sphere && s.Kind != ShapeKind.Capsule && s.Kind != ShapeKind.Cylinder)
                problem = $"kind {s.Kind}: a runtime object holds boxes, spheres, capsules and cylinders";
            else if ((s.Material == WorldIds.NoMaterial) == (s.Surface == WorldIds.NoSurface))
                problem = "needs exactly one of a material and a soil surface";
            else if (s.Material > _materials.Length || s.Surface > FloorSurface)
                problem = "names a material or surface this world does not know";
            else if (!Orthonormal(s.AxisX, s.AxisY, s.AxisZ))
                problem = "axes are not orthonormal with AxisX x AxisY = AxisZ";
            else if (!(s.Radius >= 0 && s.HalfLength >= 0 && s.HalfExtents.X >= 0 && s.HalfExtents.Y >= 0 && s.HalfExtents.Z >= 0))
                problem = "has a negative size";
            if (problem != null)
                throw new ArgumentException($"shape {index} {problem}", "shapes");
        }

        private static bool Orthonormal(Double3 x, Double3 y, Double3 z)
        {
            const double tolerance = 1e-9;
            return DetMath.Abs(Double3.Dot(x, x) - 1) <= tolerance && DetMath.Abs(Double3.Dot(y, y) - 1) <= tolerance
                && DetMath.Abs(Double3.Dot(z, z) - 1) <= tolerance && DetMath.Abs(Double3.Dot(x, y)) <= tolerance
                && DetMath.Abs(Double3.Dot(y, z)) <= tolerance && DetMath.Abs(Double3.Dot(z, x)) <= tolerance
                && Double3.Dot(Double3.Cross(x, y), z) > 0;
        }

        public void ResetRuntimeObjects()
        {
            _objectCount = 0;
            _primCount = 0;
        }

        public string ObjectName(int obj) => obj >= 0 && obj < _objectCount ? _objectNames[obj] : null;

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

        public string SurfaceName(byte surface) => surface == FloorSurface ? _surfaceName : null;
    }
}
