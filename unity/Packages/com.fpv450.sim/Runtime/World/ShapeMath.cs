// ShapeMath: the geometry behind StaticContacts and Raycast, shared by every world
// (the floor stub now, the gallery in task 1.4): a capsule core's closest approach to
// each primitive, the swept first touch under IWorldQuery's Time rule, ray entries,
// and the canonical-order insert. Only IEEE + - * / and sqrt (DetMath); no allocation.
//
// Ported from the first attempt (D-027, read from refs/godot/main and never merged):
// - refs/godot/main:game/src/world/Shapes.cs: Near, Closest, Round, Radial, Clamp01,
//   Sign, Unit, OnSegment, SegmentSegment (Ericson 5.1.9), MinimizeAlong, LocalSdf,
//   RayPrim, RaySphere, RayCapsule (Quílez), RayBox, Slab, RayCylinder
// - refs/godot/main:game/src/world/Contacts.cs: Contact (the sweep), Insert, TouchDistance, MaxAdvance
// - refs/godot/main:game/src/world/WorldObjects.cs: Bounds
// - refs/godot/main:game/src/world/WorldQuery.cs: Limit
// - refs/godot/main:game/src/world/Shapes.cs (task 1.4): the wire case of Closest and
//   RayPrim (a capsule chain along the point list, segments outside the reach skipped)
// Changes: System.Math goes through DetMath; the plane (the ground's half-space) is
// new; a shape's rotation is its ShapeGeometry axes; Time is a double; the Time rule
// latches every first touch during the step (IWorldQuery). Task 1.4: the sweep is
// generic over the shape's closest approach (IClosest), so a primitive and a wire share
// one Time rule; a wire's candidate segments are found along its chord (WireRange); a
// ray that starts inside any segment of a wire does not hit that wire.
// Frame: none of this depends on handedness (IWorldQuery, Frame).

using System;

namespace Fpv450.Sim.World
{
    /// <summary>An axis-aligned box, m.</summary>
    internal readonly struct Bounds
    {
        public readonly Double3 Min, Max;

        public Bounds(Double3 min, Double3 max)
        {
            Min = min;
            Max = max;
        }

        public static Bounds Of(Double3 a, Double3 b, double pad) => new Bounds(
            new Double3(DetMath.Min(a.X, b.X) - pad, DetMath.Min(a.Y, b.Y) - pad, DetMath.Min(a.Z, b.Z) - pad),
            new Double3(DetMath.Max(a.X, b.X) + pad, DetMath.Max(a.Y, b.Y) + pad, DetMath.Max(a.Z, b.Z) + pad));

        public Bounds Union(in Bounds b) => new Bounds(
            new Double3(DetMath.Min(Min.X, b.Min.X), DetMath.Min(Min.Y, b.Min.Y), DetMath.Min(Min.Z, b.Min.Z)),
            new Double3(DetMath.Max(Max.X, b.Max.X), DetMath.Max(Max.Y, b.Max.Y), DetMath.Max(Max.Z, b.Max.Z)));

        public bool Overlaps(in Bounds b) =>
            Min.X <= b.Max.X && b.Min.X <= Max.X && Min.Y <= b.Max.Y && b.Min.Y <= Max.Y && Min.Z <= b.Max.Z && b.Min.Z <= Max.Z;

        /// <summary>The world bounds of a box, sphere, capsule or cylinder (a cylinder's are its capsule's: conservative).</summary>
        public static Bounds OfShape(in ShapeGeometry s)
        {
            switch (s.Kind)
            {
                case ShapeKind.Box:
                {
                    Double3 h = s.HalfExtents;
                    var e = new Double3(
                        DetMath.Abs(s.AxisX.X) * h.X + DetMath.Abs(s.AxisY.X) * h.Y + DetMath.Abs(s.AxisZ.X) * h.Z,
                        DetMath.Abs(s.AxisX.Y) * h.X + DetMath.Abs(s.AxisY.Y) * h.Y + DetMath.Abs(s.AxisZ.Y) * h.Z,
                        DetMath.Abs(s.AxisX.Z) * h.X + DetMath.Abs(s.AxisY.Z) * h.Y + DetMath.Abs(s.AxisZ.Z) * h.Z);
                    return new Bounds(s.Center - e, s.Center + e);
                }
                case ShapeKind.Sphere:
                    return Of(s.Center, s.Center, s.Radius);
                default:
                    return Of(s.Center - s.AxisY * s.HalfLength, s.Center + s.AxisY * s.HalfLength, s.Radius);
            }
        }
    }

    /// <summary>A shape placed in a world: its geometry, bounds and ids.</summary>
    internal struct Prim
    {
        public ShapeGeometry Geometry;
        public Bounds Box;
        public int Object, Shape;
        /// <summary>A grid world's cell of the bounds' minimum corner (MapWorld).</summary>
        public int CellX, CellZ;
        /// <summary>A wire: its chain's points in the world's wire arrays (MapWorld).</summary>
        public int WireFirst, WireCount;
    }

    /// <summary>One shape's closest approach to a capsule core, as the sweep asks it (ShapeMath.Sweep).</summary>
    internal interface IClosest
    {
        void Closest(Double3 p, Double3 q, double r, out Near near);

        /// <summary>True only if the swept capsule cannot touch the shape from `t` to the step's end (ShapeMath.Clear).</summary>
        bool Clear(in Capsule previous, in Capsule current, double t);
    }

    /// <summary>A primitive given by its geometry (a copy).</summary>
    internal readonly struct GeometryClosest : IClosest
    {
        private readonly ShapeGeometry _s;

        public GeometryClosest(in ShapeGeometry s) => _s = s;

        public void Closest(Double3 p, Double3 q, double r, out Near near) => ShapeMath.Closest(_s, p, q, r, out near);

        public bool Clear(in Capsule previous, in Capsule current, double t) => ShapeMath.Clear(_s, previous, current, t);
    }

    /// <summary>A primitive in a world's array, by its index (no copy).</summary>
    internal readonly struct PrimClosest : IClosest
    {
        private readonly Prim[] _prims;
        private readonly int _index;

        public PrimClosest(Prim[] prims, int index)
        {
            _prims = prims;
            _index = index;
        }

        public void Closest(Double3 p, Double3 q, double r, out Near near) => ShapeMath.Closest(_prims[_index].Geometry, p, q, r, out near);

        public bool Clear(in Capsule previous, in Capsule current, double t) => ShapeMath.Clear(_prims[_index].Geometry, previous, current, t);
    }

    /// <summary>
    /// A wire's capsule chain: its points (world space) and their lengths along it from its first point, with the
    /// candidate segments [Lo, Hi] of one query (ShapeMath.WireRange) and that query's reach.
    /// </summary>
    internal readonly struct WireChain : IClosest
    {
        public readonly Double3[] Points;
        public readonly double[] Along;
        public readonly int First, Count, Lo, Hi;
        public readonly double Radius;
        public readonly Bounds Reach;

        public WireChain(Double3[] points, double[] along, int first, int count, double radius, int lo, int hi, in Bounds reach)
        {
            Points = points;
            Along = along;
            First = first;
            Count = count;
            Radius = radius;
            Lo = lo;
            Hi = hi;
            Reach = reach;
        }

        public void Closest(Double3 p, Double3 q, double r, out Near near) => ShapeMath.WireClosest(this, p, q, r, out near);

        public bool Clear(in Capsule previous, in Capsule current, double t) => false;
    }

    /// <summary>The closest approach of a capsule to one primitive.</summary>
    internal struct Near
    {
        public double Distance;  // signed, surface to surface: negative = overlap
        public Double3 Point;    // on the primitive's surface
        public Double3 Normal;   // unit, out of the primitive toward the capsule
        public double WireParam; // along a wire's polyline by length, 0–1; −1 for other shapes
    }

    internal static class ShapeMath
    {
        /// <summary>A sweep touches a shape once it comes this close (conservative advancement stops there), m.</summary>
        private const double TouchDistance = 1e-6;
        /// <summary>Conservative advancement gives up after this many steps and counts the shape as touched.</summary>
        private const int MaxAdvance = 64;
        /// <summary>Clear's margin over TouchDistance for its own rounding (coordinates of a few hundred metres), m.</summary>
        private const double ClearSlack = 1e-9;
        /// <summary>The largest double below 1: a Time below 1 never reads as the current pose.</summary>
        private const double BelowOne = 0.99999999999999989;

        /// <summary>
        /// The contact of a capsule swept from `previous` to `current` with `s`, under
        /// IWorldQuery's Time rule. `speed` is the larger of the two core ends' moves, m.
        /// </summary>
        public static bool Sweep(in ShapeGeometry s, in Capsule previous, in Capsule current, double margin, double speed,
            out Near near, out double time) =>
            Sweep(new GeometryClosest(s), previous, current, margin, speed, out near, out time);

        /// <summary>The sweep of any shape that gives its closest approach (a primitive, a wire's chain).</summary>
        public static bool Sweep<T>(T s, in Capsule previous, in Capsule current, double margin, double speed,
            out Near near, out double time) where T : struct, IClosest
        {
            double r = current.Radius;
            s.Closest(current.A, current.B, r, out near);
            time = 1;
            if (speed > 0)
            {
                // The distance changes by at most `speed` per unit of t, so stepping by distance / speed never skips a touch.
                double t = 0;
                Near first;
                for (int i = 0; ; i++)
                {
                    s.Closest(previous.A + (current.A - previous.A) * t, previous.B + (current.B - previous.B) * t, r, out first);
                    if (first.Distance <= TouchDistance)
                        break;
                    if (s.Clear(previous, current, t))
                        return near.Distance <= margin; // never touched: a box's per-axis bound (Clear) proves it for the rest
                    if (i == MaxAdvance)
                        break;
                    t += first.Distance / speed;
                    if (!(t <= 1))
                        return near.Distance <= margin; // never touched
                }
                if (t > 0)
                {
                    near = first; // the first touch during the step
                    time = DetMath.Min(t, BelowOne);
                    return true;
                }
                if (!(Double3.Dot(near.Normal, first.Normal) > 0))
                {
                    near = first; // touching since the step began, and now through the shape
                    time = 0;
                    return true;
                }
            }
            return near.Distance <= margin;
        }

        /// <summary>
        /// Whether the capsule swept from `previous` to `current` provably stays beyond TouchDistance of the box `s` from
        /// `t` to 1 (false for every other kind: the sweep then advances as before). A box's signed distance is at least
        /// max_i(|x_i| − h_i) in its own frame, the segment's least |x_i| at `t` is exact, and |x_i| of any core point
        /// changes by at most the larger of the core ends' moves along axis i per unit of t; so the bound at t = 1 holds
        /// for the whole rest of the step. It only ends the advancement early where the Lipschitz step would creep (a
        /// capsule sliding along a face): a sweep it ends would never have touched, so every result is the same, except
        /// that a slide that used to give up after MaxAdvance steps (a touch counted where there was none) is now clear.
        /// </summary>
        public static bool Clear(in ShapeGeometry s, in Capsule previous, in Capsule current, double t)
        {
            if (s.Kind != ShapeKind.Box)
                return false;
            Double3 da = current.A - previous.A, db = current.B - previous.B;
            Double3 lp = ToLocal(s, previous.A + da * t - s.Center), lq = ToLocal(s, previous.B + db * t - s.Center);
            Double3 la = ToLocal(s, da), lb = ToLocal(s, db), h = s.HalfExtents;
            double rest = 1 - t;
            double bound = DetMath.Max(AxisBound(lp.X, lq.X, la.X, lb.X, h.X, rest),
                DetMath.Max(AxisBound(lp.Y, lq.Y, la.Y, lb.Y, h.Y, rest), AxisBound(lp.Z, lq.Z, la.Z, lb.Z, h.Z, rest)));
            return bound - current.Radius > TouchDistance + ClearSlack;
        }

        /// <summary>A lower bound of |x| − h over the core segment p–q along one axis, for the rest of the step.</summary>
        private static double AxisBound(double p, double q, double movep, double moveq, double h, double rest) =>
            (p * q <= 0 ? -h : DetMath.Min(DetMath.Abs(p), DetMath.Abs(q)) - h) - DetMath.Max(DetMath.Abs(movep), DetMath.Abs(moveq)) * rest;

        /// <summary>Inserts in canonical order (object, shape); on overflow the buffer keeps the first ones. `count` is the true count.</summary>
        public static void Insert(Span<StaticContact> results, ref int count, in StaticContact c)
        {
            int filled = count < results.Length ? count : results.Length, at = filled;
            while (at > 0 && (c.Object < results[at - 1].Object || c.Object == results[at - 1].Object && c.Shape < results[at - 1].Shape))
                at--;
            if (at < results.Length)
            {
                for (int k = filled < results.Length - 1 ? filled : results.Length - 1; k > at; k--)
                    results[k] = results[k - 1];
                results[at] = c;
            }
            count++;
        }

        /// <summary>The closest approach of the capsule with core p–q and radius r to `s`.</summary>
        public static void Closest(in ShapeGeometry s, Double3 p, Double3 q, double r, out Near near)
        {
            near = default;
            near.WireParam = -1;
            switch (s.Kind)
            {
                case ShapeKind.Plane:
                {
                    // The lower core end (on a tie, p) is the closest; the plane's point is its foot.
                    double dp = Double3.Dot(p - s.Center, s.AxisY), dq = Double3.Dot(q - s.Center, s.AxisY);
                    bool atQ = dq < dp;
                    Double3 x = atQ ? q : p;
                    double d = atQ ? dq : dp;
                    near.Distance = d - r;
                    near.Point = x - s.AxisY * d;
                    near.Normal = s.AxisY;
                    break;
                }
                case ShapeKind.Sphere:
                    Round(OnSegment(p, q, s.Center), s.Center, s.Radius, r, q - p, ref near);
                    break;
                case ShapeKind.Capsule:
                {
                    Double3 a = s.Center - s.AxisY * s.HalfLength, b = s.Center + s.AxisY * s.HalfLength;
                    SegmentSegment(p, q, a, b, out double u, out double v);
                    Round(p + (q - p) * u, a + (b - a) * v, s.Radius, r, s.AxisY, ref near);
                    break;
                }
                case ShapeKind.Box:
                case ShapeKind.Cylinder:
                {
                    // Minimise its signed distance along the core, in its own frame.
                    Double3 lp = ToLocal(s, p - s.Center), lq = ToLocal(s, q - s.Center);
                    double u = Double3.Dot(lq - lp, lq - lp) > 0 ? MinimizeAlong(s, lp, lq) : 0;
                    near.Distance = LocalSdf(s, lp + (lq - lp) * u, out Double3 surface, out Double3 normal) - r;
                    near.Point = s.Center + ToWorld(s, surface);
                    near.Normal = ToWorld(s, normal);
                    break;
                }
                default:
                    near.Distance = double.PositiveInfinity; // a wire is a chain: WireClosest
                    break;
            }
        }

        /// <summary>
        /// The candidate segments [lo, hi] of a wire's chain for `reach` (empty when hi &lt; lo): the segments i, from
        /// point i to i + 1, whose stretch along the chord, grown by the radius, meets the reach's. `proj` holds each
        /// point's distance along the unit chord from `origin`, strictly rising (MapWorld checks it), so two binary
        /// searches find them. Conservative by 1 µm: every segment whose bounds meet the reach is among them.
        /// </summary>
        public static void WireRange(double[] proj, int first, int count, Double3 origin, Double3 chord, double radius, in Bounds reach,
            out int lo, out int hi)
        {
            const double slack = 1e-6;
            Double3 centre = (reach.Min + reach.Max) * 0.5, half = (reach.Max - reach.Min) * 0.5;
            double mid = Double3.Dot(centre - origin, chord);
            double spread = DetMath.Abs(chord.X) * half.X + DetMath.Abs(chord.Y) * half.Y + DetMath.Abs(chord.Z) * half.Z + radius + slack;
            double from = mid - spread, to = mid + spread;
            // lo: the first segment whose far end reaches `from`; hi: the last one whose near end is within `to`.
            int a = first, b = first + count - 1; // segments first .. first + count − 2; b is one past the last
            while (a < b)
            {
                int m = a + (b - a) / 2;
                if (proj[m + 1] >= from) b = m;
                else a = m + 1;
            }
            lo = a;
            a = first - 1;
            b = first + count - 2;
            while (a < b)
            {
                int m = b - (b - a) / 2;
                if (proj[m] <= to) a = m;
                else b = m - 1;
            }
            hi = a;
        }

        /// <summary>
        /// A capsule's closest approach to a wire's chain (refs/godot/main Shapes.cs, the wire case): the nearest of its
        /// candidate segments that meet the reach (the first on a tie), its distance the true signed one deep inside,
        /// and WireParam the point's length along the chain over the chain's length. +∞ with no segment in reach.
        /// </summary>
        public static void WireClosest(in WireChain w, Double3 p, Double3 q, double r, out Near near)
        {
            near = default;
            near.WireParam = -1;
            double best = double.PositiveInfinity, bu = 0, bv = 0;
            int at = -1;
            for (int i = w.Lo; i <= w.Hi; i++)
            {
                Double3 a = w.Points[i], b = w.Points[i + 1];
                if (!Bounds.Of(a, b, w.Radius).Overlaps(w.Reach))
                    continue;
                SegmentSegment(p, q, a, b, out double u, out double v);
                Double3 d = p + (q - p) * u - (a + (b - a) * v);
                double dd = Double3.Dot(d, d);
                if (dd < best)
                {
                    best = dd;
                    at = i;
                    bu = u;
                    bv = v;
                }
            }
            if (at < 0)
            {
                near.Distance = double.PositiveInfinity;
                return;
            }
            Double3 a0 = w.Points[at], b0 = w.Points[at + 1];
            Round(p + (q - p) * bu, a0 + (b0 - a0) * bv, w.Radius, r, b0 - a0, ref near);
            near.WireParam = (w.Along[at] + (w.Along[at + 1] - w.Along[at]) * bv) / w.Along[w.First + w.Count - 1];
        }

        /// <summary>
        /// Where the ray o + t·d (d unit) first enters a wire's chain, as RayPrim does for a primitive. A ray that starts
        /// inside any of its candidate segments does not hit the wire (a re-cast from inside a conductor goes on).
        /// </summary>
        public static void WireRay(in WireChain w, Double3 o, Double3 d, ref double t, ref Double3 normal)
        {
            double rr = w.Radius * w.Radius;
            for (int i = w.Lo; i <= w.Hi; i++)
            {
                Double3 a = w.Points[i], b = w.Points[i + 1];
                if (!Bounds.Of(a, b, w.Radius).Overlaps(w.Reach))
                    continue;
                Double3 v = o - OnSegment(a, b, o);
                if (Double3.Dot(v, v) < rr)
                    return;
            }
            for (int i = w.Lo; i <= w.Hi; i++)
            {
                Double3 a = w.Points[i], b = w.Points[i + 1];
                if (Bounds.Of(a, b, w.Radius).Overlaps(w.Reach))
                    RayCapsule(o, d, a, b, w.Radius, ref t, ref normal);
            }
        }

        /// <summary>
        /// Where the ray o + t·d (d unit) first enters `s`, if at some t ≤ `t` (then `t` and
        /// `normal` are updated; call it with t = +∞). A ray that starts inside does not hit.
        /// </summary>
        public static void RayPrim(in ShapeGeometry s, Double3 o, Double3 d, ref double t, ref Double3 normal)
        {
            switch (s.Kind)
            {
                case ShapeKind.Plane:
                    RayPlane(o, d, s.Center, s.AxisY, ref t, ref normal);
                    break;
                case ShapeKind.Sphere:
                    RaySphere(o, d, s.Center, s.Radius, ref t, ref normal);
                    break;
                case ShapeKind.Capsule:
                    RayCapsule(o, d, s.Center - s.AxisY * s.HalfLength, s.Center + s.AxisY * s.HalfLength, s.Radius, ref t, ref normal);
                    break;
                case ShapeKind.Box:
                    RayBox(s, o, d, ref t, ref normal);
                    break;
                case ShapeKind.Cylinder:
                    RayCylinder(s, o, d, ref t, ref normal);
                    break;
            }
        }

        public static Double3 Unit(Double3 v) => v * (1 / v.Length());

        /// <summary>
        /// Why AddObject refuses a runtime shape, or null: a box, sphere, capsule or cylinder with exactly one of a
        /// material (1 … materials) and a soil surface (1 … surfaces), orthonormal axes with AxisX x AxisY = AxisZ, and
        /// no negative size.
        /// </summary>
        public static string RuntimeShapeProblem(in ShapeGeometry s, int materials, int surfaces)
        {
            if (s.Kind != ShapeKind.Box && s.Kind != ShapeKind.Sphere && s.Kind != ShapeKind.Capsule && s.Kind != ShapeKind.Cylinder)
                return $"kind {s.Kind}: a runtime object holds boxes, spheres, capsules and cylinders";
            if ((s.Material == WorldIds.NoMaterial) == (s.Surface == WorldIds.NoSurface))
                return "needs exactly one of a material and a soil surface";
            if (s.Material > materials || s.Surface > surfaces)
                return "names a material or surface this world does not know";
            if (!Orthonormal(s.AxisX, s.AxisY, s.AxisZ))
                return "axes are not orthonormal with AxisX x AxisY = AxisZ";
            if (!(s.Radius >= 0 && s.HalfLength >= 0 && s.HalfExtents.X >= 0 && s.HalfExtents.Y >= 0 && s.HalfExtents.Z >= 0))
                return "has a negative size";
            return null;
        }

        private static bool Orthonormal(Double3 x, Double3 y, Double3 z)
        {
            const double tolerance = 1e-9;
            return DetMath.Abs(Double3.Dot(x, x) - 1) <= tolerance && DetMath.Abs(Double3.Dot(y, y) - 1) <= tolerance
                && DetMath.Abs(Double3.Dot(z, z) - 1) <= tolerance && DetMath.Abs(Double3.Dot(x, y)) <= tolerance
                && DetMath.Abs(Double3.Dot(y, z)) <= tolerance && DetMath.Abs(Double3.Dot(z, x)) <= tolerance
                && Double3.Dot(Double3.Cross(x, y), z) > 0;
        }

        private static Double3 ToLocal(in ShapeGeometry s, Double3 v) =>
            new Double3(Double3.Dot(v, s.AxisX), Double3.Dot(v, s.AxisY), Double3.Dot(v, s.AxisZ));

        private static Double3 ToWorld(in ShapeGeometry s, Double3 v) => s.AxisX * v.X + s.AxisY * v.Y + s.AxisZ * v.Z;

        /// <summary>
        /// Core point x against a round primitive around its centre or axis point y with
        /// radius R. When x lies exactly on the centre or axis, the normal is any direction
        /// perpendicular to `axis`.
        /// </summary>
        private static void Round(Double3 x, Double3 y, double R, double r, Double3 axis, ref Near near)
        {
            Double3 v = x - y;
            double length = v.Length();
            Double3 n = length > 0 ? v * (1 / length) : Radial(axis);
            near.Distance = length - R - r;
            near.Point = y + n * R;
            near.Normal = n;
        }

        private static Double3 Radial(Double3 axis)
        {
            double length = axis.Length();
            Double3 a = length > 0 ? axis * (1 / length) : new Double3(0, 1, 0);
            Double3 c = Double3.Cross(a, DetMath.Abs(a.X) < 0.9 ? new Double3(1, 0, 0) : new Double3(0, 0, 1));
            return c * (1 / c.Length());
        }

        /// <summary>NaN goes to 0.</summary>
        private static double Clamp01(double t) => t > 0 ? t < 1 ? t : 1 : 0;

        private static double Sign(double v) => v < 0 ? -1 : 1;

        /// <summary>NaN goes to the low bound.</summary>
        private static double Limit(double v, double bound) => v > bound ? bound : v >= -bound ? v : -bound;

        private static Double3 OnSegment(Double3 p, Double3 q, Double3 c)
        {
            Double3 d = q - p;
            double dd = Double3.Dot(d, d);
            return dd > 0 ? p + d * Clamp01(Double3.Dot(c - p, d) / dd) : p;
        }

        /// <summary>Closest points of segments p1–q1 and p2–q2 at parameters s and t (Ericson, Real-Time Collision Detection, 5.1.9).</summary>
        private static void SegmentSegment(Double3 p1, Double3 q1, Double3 p2, Double3 q2, out double s, out double t)
        {
            Double3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            double a = Double3.Dot(d1, d1), e = Double3.Dot(d2, d2), f = Double3.Dot(d2, r);
            if (!(a > 0) && !(e > 0))
            {
                s = t = 0;
                return;
            }
            if (!(a > 0))
            {
                s = 0;
                t = Clamp01(f / e);
                return;
            }
            double c = Double3.Dot(d1, r);
            if (!(e > 0))
            {
                t = 0;
                s = Clamp01(-c / a);
                return;
            }
            double b = Double3.Dot(d1, d2), denominator = a * e - b * b;
            s = denominator > 0 ? Clamp01((b * f - c * e) / denominator) : 0; // parallel: any s is as good
            t = (b * s + f) / e;
            if (t < 0)
            {
                t = 0;
                s = Clamp01(-c / a);
            }
            else if (t > 1)
            {
                t = 1;
                s = Clamp01((b - c) / a);
            }
        }

        /// <summary>
        /// The u in [0, 1] that minimises the signed distance of a box or cylinder at
        /// lp + u·(lq − lp). The signed distance of a convex solid is convex along a line, so
        /// a golden-section search finds it (the bracket shrinks to 4e-9); both ends are
        /// checked too, so a capsule end resting on a face is exact.
        /// </summary>
        private static double MinimizeAlong(in ShapeGeometry s, Double3 lp, Double3 lq)
        {
            const double g = 0.6180339887498949;
            Double3 d = lq - lp;
            double lo = 0, hi = 1, u1 = 1 - g, u2 = g;
            double f1 = LocalSdf(s, lp + d * u1, out _, out _), f2 = LocalSdf(s, lp + d * u2, out _, out _);
            for (int i = 0; i < 40; i++)
            {
                if (f1 <= f2)
                {
                    hi = u2;
                    u2 = u1;
                    f2 = f1;
                    u1 = hi - g * (hi - lo);
                    f1 = LocalSdf(s, lp + d * u1, out _, out _);
                }
                else
                {
                    lo = u1;
                    u1 = u2;
                    f1 = f2;
                    u2 = lo + g * (hi - lo);
                    f2 = LocalSdf(s, lp + d * u2, out _, out _);
                }
            }
            double u = f1 <= f2 ? u1 : u2, f = f1 <= f2 ? f1 : f2;
            double start = LocalSdf(s, lp, out _, out _), end = LocalSdf(s, lq, out _, out _);
            if (start <= f)
            {
                u = 0;
                f = start;
            }
            return end < f ? 1 : u;
        }

        /// <summary>
        /// Signed distance of a box or a cylinder (along local Y) at the local point x, with
        /// the nearest surface point and the outward normal there. Inside, the nearest face
        /// wins (ties go to x, then y, then z; a cylinder's side before its cap).
        /// </summary>
        private static double LocalSdf(in ShapeGeometry s, Double3 x, out Double3 surface, out Double3 normal)
        {
            if (s.Kind == ShapeKind.Box)
            {
                Double3 h = s.HalfExtents;
                double qx = DetMath.Abs(x.X) - h.X, qy = DetMath.Abs(x.Y) - h.Y, qz = DetMath.Abs(x.Z) - h.Z;
                if (qx > 0 || qy > 0 || qz > 0)
                {
                    surface = new Double3(Limit(x.X, h.X), Limit(x.Y, h.Y), Limit(x.Z, h.Z));
                    Double3 v = x - surface;
                    double length = v.Length();
                    normal = v * (1 / length);
                    return length;
                }
                surface = x;
                if (qx >= qy && qx >= qz)
                {
                    normal = new Double3(Sign(x.X), 0, 0);
                    surface.X = Sign(x.X) * h.X;
                    return qx;
                }
                if (qy >= qz)
                {
                    normal = new Double3(0, Sign(x.Y), 0);
                    surface.Y = Sign(x.Y) * h.Y;
                    return qy;
                }
                normal = new Double3(0, 0, Sign(x.Z));
                surface.Z = Sign(x.Z) * h.Z;
                return qz;
            }
            double radius = s.Radius, half = s.HalfLength, rho = DetMath.Sqrt(x.X * x.X + x.Z * x.Z);
            double dr = rho - radius, dy = DetMath.Abs(x.Y) - half, sy = Sign(x.Y);
            double ux = rho > 0 ? x.X / rho : 1, uz = rho > 0 ? x.Z / rho : 0;
            if (dr > 0 && dy > 0)
            {
                surface = new Double3(ux * radius, sy * half, uz * radius);
                Double3 v = x - surface;
                double length = v.Length();
                normal = v * (1 / length);
                return length;
            }
            if (dr > 0 || dy <= 0 && dr >= dy)
            {
                surface = new Double3(ux * radius, x.Y, uz * radius);
                normal = new Double3(ux, 0, uz);
                return dr;
            }
            surface = new Double3(x.X, sy * half, x.Z);
            normal = new Double3(0, sy, 0);
            return dy;
        }

        /// <summary>The ground's half-space, entered only from above (or at its surface, going down).</summary>
        private static void RayPlane(Double3 o, Double3 d, Double3 c, Double3 n, ref double t, ref Double3 normal)
        {
            double height = Double3.Dot(o - c, n), rate = Double3.Dot(d, n);
            if (!(height >= 0) || !(rate < 0))
                return;
            double hit = height / -rate;
            if (hit <= t)
            {
                t = hit;
                normal = n;
            }
        }

        private static void RaySphere(Double3 o, Double3 d, Double3 c, double radius, ref double t, ref Double3 normal)
        {
            Double3 oc = o - c;
            double b = Double3.Dot(oc, d), k = Double3.Dot(oc, oc) - radius * radius, h = b * b - k;
            if (k < 0 || h < 0)
                return;
            double hit = -b - DetMath.Sqrt(h);
            if (hit >= 0 && hit <= t)
            {
                t = hit;
                normal = Unit(oc + d * hit);
            }
        }

        /// <summary>Ray and capsule a–b (Quílez's closed form for the side, then both end spheres).</summary>
        private static void RayCapsule(Double3 o, Double3 d, Double3 a, Double3 b, double radius, ref double t, ref Double3 normal)
        {
            Double3 ba = b - a, oa = o - a;
            double baba = Double3.Dot(ba, ba), baoa = Double3.Dot(ba, oa);
            Double3 w = oa - ba * (baba > 0 ? Clamp01(baoa / baba) : 0);
            if (Double3.Dot(w, w) < radius * radius)
                return;
            double bard = Double3.Dot(ba, d), k2 = baba - bard * bard;
            if (k2 > 1e-12 * baba)
            {
                double k1 = baba * Double3.Dot(oa, d) - baoa * bard, k0 = baba * Double3.Dot(oa, oa) - baoa * baoa - radius * radius * baba;
                double h = k1 * k1 - k2 * k0;
                if (h >= 0)
                {
                    double hit = (-k1 - DetMath.Sqrt(h)) / k2, y = baoa + hit * bard;
                    if (hit >= 0 && hit <= t && y > 0 && y < baba)
                    {
                        t = hit;
                        normal = Unit(oa + d * hit - ba * (y / baba));
                    }
                }
            }
            RaySphere(o, d, a, radius, ref t, ref normal);
            RaySphere(o, d, b, radius, ref t, ref normal);
        }

        private static void RayBox(in ShapeGeometry s, Double3 o, Double3 d, ref double t, ref Double3 normal)
        {
            Double3 lo = ToLocal(s, o - s.Center), ld = ToLocal(s, d);
            double near = double.NegativeInfinity, far = double.PositiveInfinity;
            int axis = -1;
            if (!Slab(lo.X, ld.X, s.HalfExtents.X, 0, ref near, ref far, ref axis) || !Slab(lo.Y, ld.Y, s.HalfExtents.Y, 1, ref near, ref far, ref axis)
                || !Slab(lo.Z, ld.Z, s.HalfExtents.Z, 2, ref near, ref far, ref axis) || axis < 0 || near > far || near < 0 || near > t)
                return;
            t = near;
            normal = ToWorld(s, axis == 0 ? new Double3(-Sign(ld.X), 0, 0) : axis == 1 ? new Double3(0, -Sign(ld.Y), 0) : new Double3(0, 0, -Sign(ld.Z)));
        }

        private static bool Slab(double o, double d, double h, int i, ref double near, ref double far, ref int axis)
        {
            if (d == 0)
                return DetMath.Abs(o) <= h;
            double t1 = (-h - o) / d, t2 = (h - o) / d;
            if (t1 > t2)
            {
                double swap = t1;
                t1 = t2;
                t2 = swap;
            }
            if (t1 > near)
            {
                near = t1;
                axis = i;
            }
            far = DetMath.Min(far, t2);
            return true;
        }

        private static void RayCylinder(in ShapeGeometry s, Double3 o, Double3 d, ref double t, ref Double3 normal)
        {
            Double3 lo = ToLocal(s, o - s.Center), ld = ToLocal(s, d);
            double radius = s.Radius, half = s.HalfLength, c = lo.X * lo.X + lo.Z * lo.Z - radius * radius;
            if (c < 0 && DetMath.Abs(lo.Y) < half)
                return;
            double a = ld.X * ld.X + ld.Z * ld.Z;
            if (a > 1e-24)
            {
                double b = lo.X * ld.X + lo.Z * ld.Z, h = b * b - a * c;
                if (h >= 0)
                {
                    double hit = (-b - DetMath.Sqrt(h)) / a;
                    if (hit >= 0 && hit <= t && DetMath.Abs(lo.Y + hit * ld.Y) <= half)
                    {
                        t = hit;
                        normal = ToWorld(s, Unit(new Double3(lo.X + hit * ld.X, 0, lo.Z + hit * ld.Z)));
                    }
                }
            }
            if (ld.Y != 0)
            {
                double hit = ((ld.Y > 0 ? -half : half) - lo.Y) / ld.Y, px = lo.X + hit * ld.X, pz = lo.Z + hit * ld.Z;
                if (hit >= 0 && hit <= t && px * px + pz * pz <= radius * radius)
                {
                    t = hit;
                    normal = ToWorld(s, new Double3(0, -Sign(ld.Y), 0));
                }
            }
        }
    }
}
