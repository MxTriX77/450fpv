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
// latches every first touch during the step (IWorldQuery). Task 1.5: a box's closest
// approach along a core is exact (BoxMinimizer), where Shapes.cs searched it, and a box
// sweep that provably cannot touch ends early (Clear). Task 1.4: the sweep is
// generic over the shape's closest approach (IClosest), so a primitive and a wire share
// one Time rule; a wire's candidate segments are found along its chord (WireRange); a
// ray that starts inside any segment of a wire does not hit that wire. QA's review B3 (QueryVersion 3): a
// separating plane bounds every kind's sweep (Bound), so a sweep that cannot touch ends instead of creeping,
// the plane's first touch is exact, and a sweep the advancement cannot settle counts no first touch.
// QA's re-check B4 (QueryVersion 4): unless its current pose touches; then it bisects to that touch (Touch).
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

        /// <summary>Bounds that overlap nothing (an object without shapes).</summary>
        public static readonly Bounds Empty = new Bounds(new Double3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity),
            new Double3(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity));

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
        /// <summary>A grid world's cell of the bounds' minimum corner (MapWorld, a shape listed in the grid itself).</summary>
        public int CellX, CellZ;
        /// <summary>A wire: its chain's points in the world's wire arrays (MapWorld).</summary>
        public int WireFirst, WireCount;
    }

    /// <summary>One shape's closest approach to a capsule core, as the sweep asks it (ShapeMath.Sweep).</summary>
    internal interface IClosest
    {
        void Closest(Double3 p, Double3 q, double r, out Near near);

        /// <summary>True only if the swept capsule stays more than `beyond` from the shape from `t` to the step's end (ShapeMath.Clear).</summary>
        bool Clear(in Capsule previous, in Capsule current, double t, double beyond);

        /// <summary>
        /// The separating-plane bound of the sweep from `t` (ShapeMath.Bound): the first t' in [t, 1] at which the swept
        /// core can reach the shape (+∞ if it cannot before the step ends), and whether it provably stays more than
        /// TouchDistance away for the rest of the step. `near` is a closest approach of the shape at some pose of the step.
        /// </summary>
        double Bound(in Capsule previous, in Capsule current, double t, in Near near, out bool apart);

        /// <summary>A plane: its bound is its distance, so the sweep steps by the bound from the start and lands on the first touch.</summary>
        bool Flat { get; }
    }

    /// <summary>A primitive given by its geometry (a copy).</summary>
    internal readonly struct GeometryClosest : IClosest
    {
        private readonly ShapeGeometry _s;

        public GeometryClosest(in ShapeGeometry s) => _s = s;

        public void Closest(Double3 p, Double3 q, double r, out Near near) => ShapeMath.Closest(_s, p, q, r, out near);

        public bool Clear(in Capsule previous, in Capsule current, double t, double beyond) => ShapeMath.Clear(_s, previous, current, t, beyond);

        public double Bound(in Capsule previous, in Capsule current, double t, in Near near, out bool apart) =>
            ShapeMath.Bound(_s, previous, current, t, near, out apart);

        public bool Flat => _s.Kind == ShapeKind.Plane;
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

        public bool Clear(in Capsule previous, in Capsule current, double t, double beyond) =>
            ShapeMath.Clear(_prims[_index].Geometry, previous, current, t, beyond);

        public double Bound(in Capsule previous, in Capsule current, double t, in Near near, out bool apart) =>
            ShapeMath.Bound(_prims[_index].Geometry, previous, current, t, near, out apart);

        public bool Flat => _prims[_index].Geometry.Kind == ShapeKind.Plane;
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

        public bool Clear(in Capsule previous, in Capsule current, double t, double beyond) => false;

        public double Bound(in Capsule previous, in Capsule current, double t, in Near near, out bool apart) =>
            ShapeMath.WireBound(this, previous, current, t, out apart);

        public bool Flat => false;
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
        /// <summary>Conservative advancement steps by distance / speed this many times, then by the separating plane's bound.</summary>
        private const int MaxAdvance = 64;
        /// <summary>It gives up after this many steps in all: a touching current pose is then bisected to (Touch), else no first touch (IWorldQuery).</summary>
        private const int MaxSteps = 2 * MaxAdvance;
        /// <summary>Touch's halvings at most: t in (0, 1] has no more bits.</summary>
        private const int MaxHalvings = 64;
        /// <summary>Clear's margin over TouchDistance for its own rounding (coordinates of a few hundred metres), m.</summary>
        private const double ClearSlack = 1e-9;
        /// <summary>The largest double below 1: a Time below 1 never reads as the current pose.</summary>
        private const double BelowOne = 0.99999999999999989;

        /// <summary>
        /// The contact of a capsule swept from `previous` to `current` with `s`, under
        /// IWorldQuery's Time rule. `speed` is the larger of the two core ends' moves, m.
        /// Conservative advancement keeps 1.4's distance / speed steps for its first MaxAdvance
        /// (so every sweep they settle keeps its bits), then steps by the separating plane's
        /// bound, a Newton step on the distance that ends a graze's creep; a plane steps by its
        /// bound from the start. At every step the bound may prove the rest clear.
        /// </summary>
        public static bool Sweep(in ShapeGeometry s, in Capsule previous, in Capsule current, double margin, double speed,
            out Near near, out double time) =>
            Sweep(new GeometryClosest(s), previous, current, margin, speed, out near, out time);

        /// <summary>The sweep of any shape that gives its closest approach (a primitive, a wire's chain).</summary>
        public static bool Sweep<T>(T s, in Capsule previous, in Capsule current, double margin, double speed,
            out Near near, out double time) where T : struct, IClosest
        {
            double r = current.Radius;
            time = 1;
            // A box that the whole sweep stays clear of and that the current pose is beyond the margin of: no contact,
            // without a closest approach (the AABB check passes yawed and tilted boxes that are far in their own frame).
            bool clear = s.Clear(previous, current, 0, TouchDistance);
            if (clear && s.Clear(current, current, 0, margin))
            {
                near = default;
                return false;
            }
            s.Closest(current.A, current.B, r, out near);
            // A box the whole sweep stays clear of is never touched: the current contact alone, without the start pose's.
            if (clear)
                return near.Distance <= margin;
            if (speed > 0)
            {
                // The plane through the current pose's closest approach separates the whole sweep from the shape: never
                // touched, again without the start pose's closest approach.
                if (near.Distance > TouchDistance)
                {
                    s.Bound(previous, current, 0, near, out bool apart);
                    if (apart)
                        return near.Distance <= margin;
                }
                // Conservative advancement: the distance changes by at most `speed` per unit of t, and it stays above the
                // separating plane's bound (Bound), so stepping to the later of the two never skips a touch. Along a plane
                // the bound is the distance itself, so the step lands on the first touch.
                double t = 0;
                Near first;
                for (int i = 0; ; i++)
                {
                    s.Closest(previous.A + (current.A - previous.A) * t, previous.B + (current.B - previous.B) * t, r, out first);
                    if (first.Distance <= TouchDistance)
                        break;
                    double bound = s.Bound(previous, current, t, first, out bool apart);
                    if (apart || s.Clear(previous, current, t, TouchDistance))
                        return near.Distance <= margin; // never touched: a bound proves it for the rest of the step
                    if (i == MaxSteps)
                    {
                        // Unsettled: a core pitching across a curved shape, its far ends swinging at the plane of the bound
                        // while its middle creeps in (QA's re-check B4). A current pose that touches proves a touch in
                        // (t, 1]: bisect to it. Anything else counts none, never a first touch.
                        if (!(near.Distance <= TouchDistance))
                            return near.Distance <= margin;
                        t = Touch(s, previous, current, r, t, near, out first);
                        break;
                    }
                    t = i < MaxAdvance && !s.Flat ? t + first.Distance / speed : DetMath.Max(t + first.Distance / speed, bound);
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
        /// The touch of a sweep clear at `lo` whose current pose touches (`end`, within TouchDistance): bisection on (lo, 1]
        /// to a pose whose distance lies in [TouchDistance / 2, TouchDistance], short of the surface by more than a closest
        /// approach's rounding (a cylinder's search reads a few nm long), or to the last bit of t. Returns its t. The pose at
        /// `lo` is clear and the one at 1 touches, so a touch lies between; where the distance is convex in t (a translation
        /// past a convex shape) it is the first.
        /// </summary>
        private static double Touch<T>(T s, in Capsule previous, in Capsule current, double r, double lo, in Near end, out Near touch)
            where T : struct, IClosest
        {
            double hi = 1;
            touch = end;
            for (int k = 0; k < MaxHalvings && touch.Distance < TouchDistance / 2; k++)
            {
                double mid = lo + (hi - lo) * 0.5;
                if (!(mid > lo && mid < hi))
                    break;
                s.Closest(previous.A + (current.A - previous.A) * mid, previous.B + (current.B - previous.B) * mid, r, out Near at);
                if (at.Distance <= TouchDistance)
                {
                    hi = mid;
                    touch = at;
                }
                else
                    lo = mid;
            }
            return hi;
        }

        /// <summary>
        /// Whether the capsule swept from `previous` to `current` provably stays more than `beyond` from the box `s` from
        /// `t` to 1 (false for every other kind: the sweep then advances as before). A box's signed distance is at least
        /// max_i(|x_i| − h_i) in its own frame, the segment's least |x_i| at `t` is exact, and |x_i| of any core point
        /// changes by at most the larger of the core ends' moves along axis i per unit of t; so the bound at t = 1 holds
        /// for the whole rest of the step. It only ends the advancement early where the Lipschitz step would creep (a
        /// capsule sliding along a face): a sweep it ends would never have touched, so every result is the same, except
        /// that a slide that used to give up after MaxAdvance steps (a touch counted where there was none) is now clear.
        /// </summary>
        public static bool Clear(in ShapeGeometry s, in Capsule previous, in Capsule current, double t, double beyond)
        {
            if (s.Kind != ShapeKind.Box)
                return false;
            Double3 da = current.A - previous.A, db = current.B - previous.B;
            Double3 lp = ToLocal(s, previous.A + da * t - s.Center), lq = ToLocal(s, previous.B + db * t - s.Center);
            Double3 la = ToLocal(s, da), lb = ToLocal(s, db), h = s.HalfExtents;
            double rest = 1 - t;
            double bound = DetMath.Max(AxisBound(lp.X, lq.X, la.X, lb.X, h.X, rest),
                DetMath.Max(AxisBound(lp.Y, lq.Y, la.Y, lb.Y, h.Y, rest), AxisBound(lp.Z, lq.Z, la.Z, lb.Z, h.Z, rest)));
            return bound - current.Radius > beyond + ClearSlack;
        }

        /// <summary>
        /// The separating-plane bound of the sweep from `t` for a convex primitive (task 1.6, review B3). Any unit n gives
        /// a plane that holds the whole shape below support(n) = max over the shape of x·n, so a core point x is at least
        /// x·n − support(n) from it; the core's least x·n is one of its ends', and each end's x·n is linear in t. Along the
        /// normal of a closest approach at t the bound equals the distance at t, and only the move along that normal
        /// lowers it. So it ends a slide past a sphere, a capsule, a cylinder or a box, where the distance / speed step
        /// creeps, and along a plane (its bound is its distance) it is the exact first touch. Returns the first t' in
        /// [t, 1] where the bound reaches 0 (+∞ if it stays above), and whether it stays above TouchDistance throughout.
        /// </summary>
        public static double Bound(in ShapeGeometry s, in Capsule previous, in Capsule current, double t, in Near near, out bool apart) =>
            Bound(near.Normal, Support(s, near.Normal), previous, current, t, out apart);

        /// <summary>The largest x·n over a primitive (n a unit vector); a plane's only along its normal, +∞ otherwise.</summary>
        private static double Support(in ShapeGeometry s, Double3 n)
        {
            double c = Double3.Dot(s.Center, n);
            switch (s.Kind)
            {
                case ShapeKind.Plane:
                    return n.X == s.AxisY.X && n.Y == s.AxisY.Y && n.Z == s.AxisY.Z ? c : double.PositiveInfinity;
                case ShapeKind.Sphere:
                    return c + s.Radius;
                case ShapeKind.Capsule:
                    return c + s.HalfLength * DetMath.Abs(Double3.Dot(s.AxisY, n)) + s.Radius;
                case ShapeKind.Box:
                    return c + s.HalfExtents.X * DetMath.Abs(Double3.Dot(s.AxisX, n)) + s.HalfExtents.Y * DetMath.Abs(Double3.Dot(s.AxisY, n))
                        + s.HalfExtents.Z * DetMath.Abs(Double3.Dot(s.AxisZ, n));
                case ShapeKind.Cylinder:
                {
                    double along = Double3.Dot(s.AxisY, n), side = 1 - along * along;
                    return c + s.HalfLength * DetMath.Abs(along) + s.Radius * DetMath.Sqrt(side > 0 ? side : 0);
                }
                default:
                    return double.PositiveInfinity;
            }
        }

        /// <summary>The bound along a plane of unit normal n that holds the shape below `support` (Bound).</summary>
        private static double Bound(Double3 n, double support, in Capsule previous, in Capsule current, double t, out bool apart)
        {
            double r = current.Radius;
            Double3 a = previous.A + (current.A - previous.A) * t, b = previous.B + (current.B - previous.B) * t;
            double at = Double3.Dot(a, n) - support - r, a1 = Double3.Dot(current.A, n) - support - r;
            double bt = Double3.Dot(b, n) - support - r, b1 = Double3.Dot(current.B, n) - support - r;
            apart = DetMath.Min(DetMath.Min(at, a1), DetMath.Min(bt, b1)) > TouchDistance + ClearSlack;
            return DetMath.Min(Crossing(at, a1, t), Crossing(bt, b1, t));
        }

        /// <summary>The first t' in [t, 1] where a height linear from ht (at t) to h1 (at 1) reaches 0; +∞ if it stays above.</summary>
        private static double Crossing(double ht, double h1, double t) =>
            !(ht > 0) ? t : h1 > 0 ? double.PositiveInfinity : t + ht / (ht - h1) * (1 - t);

        /// <summary>
        /// Bound for a wire's chain: the chain is not convex, so each candidate segment (a capsule) is bounded along the
        /// normal of its own closest approach at t, and the chain's bound is the least of theirs.
        /// </summary>
        public static double WireBound(in WireChain w, in Capsule previous, in Capsule current, double t, out bool apart)
        {
            Double3 p = previous.A + (current.A - previous.A) * t, q = previous.B + (current.B - previous.B) * t;
            double bound = double.PositiveInfinity;
            apart = true;
            for (int i = w.Lo; i <= w.Hi; i++)
            {
                Double3 a = w.Points[i], b = w.Points[i + 1];
                if (!Bounds.Of(a, b, w.Radius).Overlaps(w.Reach))
                    continue;
                SegmentSegment(p, q, a, b, out double u, out double v);
                Double3 d = p + (q - p) * u - (a + (b - a) * v);
                double length = d.Length();
                if (!(length > 0))
                {
                    apart = false; // the core meets this segment's axis: no bound
                    return t;
                }
                Double3 n = d * (1 / length);
                double bi = Bound(n, DetMath.Max(Double3.Dot(a, n), Double3.Dot(b, n)) + w.Radius, previous, current, t, out bool clear);
                apart &= clear;
                bound = DetMath.Min(bound, bi);
            }
            return bound;
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
        /// lp + u·(lq − lp). Outside a box it is exact (BoxMinimizer). Otherwise the signed
        /// distance of a convex solid is convex along a line, so a golden-section search finds
        /// it (the bracket shrinks to 4e-9); both ends are checked too, so a capsule end resting
        /// on a face is exact. Inside a box the deepest point lies where two faces are equally
        /// near, so the search's point beside it, not the tie itself, keeps the face (and the
        /// Time rule's normal test) steady from one pose to the next.
        /// </summary>
        private static double MinimizeAlong(in ShapeGeometry s, Double3 lp, Double3 lq)
        {
            if (s.Kind == ShapeKind.Box)
            {
                double outside = BoxMinimizer(s.HalfExtents, lp, lq - lp);
                if (outside >= 0)
                    return outside;
            }
            const double g = 0.6180339887498949;
            Double3 d = lq - lp;
            double lo = 0, hi = 1, u1 = 1 - g, u2 = g;
            double f1 = Sdf(s, lp + d * u1), f2 = Sdf(s, lp + d * u2);
            for (int i = 0; i < 40; i++)
            {
                if (f1 <= f2)
                {
                    hi = u2;
                    u2 = u1;
                    f2 = f1;
                    u1 = hi - g * (hi - lo);
                    f1 = Sdf(s, lp + d * u1);
                }
                else
                {
                    lo = u1;
                    u1 = u2;
                    f1 = f2;
                    u2 = lo + g * (hi - lo);
                    f2 = Sdf(s, lp + d * u2);
                }
            }
            double u = f1 <= f2 ? u1 : u2, f = f1 <= f2 ? f1 : f2;
            double start = Sdf(s, lp), end = Sdf(s, lq);
            if (start <= f)
            {
                u = 0;
                f = start;
            }
            return end < f ? 1 : u;
        }

        /// <summary>
        /// The u in [0, 1] that minimises a box's distance at p + u·d (its own frame) when the segment stays outside the
        /// box, in closed form, where the golden-section search took 44 evaluations (task 1.5); −1 when it enters the box.
        /// max_i(|x_i| − h_i) is convex and piecewise linear in u, so its least value lies at an end, where an x_i is 0,
        /// or where two |x_i| − h_i meet; not above 0, the segment enters the box. Otherwise the squared distance
        /// Σ max(|x_i| − h_i, 0)² is a convex quadratic between the u where an x_i crosses ±h_i, minimised in closed form
        /// on each piece. On a tie the smaller u wins: a flat stretch's start, A itself when A lies over the face
        /// (IWorldQuery's line contact).
        /// </summary>
        private static double BoxMinimizer(Double3 h, Double3 p, Double3 d)
        {
            double best = Inner(h, p, d, 0), at = 0;
            // Both ends beyond one face's plane: outside all along, so the entry test below is settled (its every candidate
            // lies between the ends, beyond that plane too, rounding included), and only its sign was used.
            if (!Beyond(p.X, d.X, h.X) && !Beyond(p.Y, d.Y, h.Y) && !Beyond(p.Z, d.Z, h.Z))
                best = EntryTest(h, p, d, best, ref at);
            if (best <= 0)
                return -1;
            return Outside(h, p, d);
        }

        private static bool Beyond(double p, double d, double h) => p > h && p + d > h || p < -h && p + d < -h;

        /// <summary>The least max_i(|x_i| − h_i) over the segment's candidates: not above 0, the segment enters the box.</summary>
        private static double EntryTest(Double3 h, Double3 p, Double3 d, double best, ref double at)
        {
            Candidate(h, p, d, 1, ref best, ref at);
            for (int i = 0; i < 3; i++)
            {
                double pi = Axis(p, i), di = Axis(d, i);
                if (di != 0)
                    Candidate(h, p, d, -pi / di, ref best, ref at);
                for (int j = i + 1; j < 3; j++)
                {
                    for (int si = -1; si <= 1; si += 2)
                    {
                        for (int sj = -1; sj <= 1; sj += 2)
                        {
                            double den = si * di - sj * Axis(d, j);
                            if (den != 0)
                                Candidate(h, p, d, (Axis(h, i) - Axis(h, j) - si * pi + sj * Axis(p, j)) / den, ref best, ref at);
                        }
                    }
                }
            }
            return best;
        }

        /// <summary>BoxMinimizer's u for a segment outside the box all along: the pieces between the u where an axis crosses a face's plane.</summary>
        private static double Outside(Double3 h, Double3 p, Double3 d)
        {
            double best, at = 0;
            Span<double> cuts = stackalloc double[8];
            int n = 0;
            cuts[n++] = 0;
            cuts[n++] = 1;
            for (int i = 0; i < 3; i++)
            {
                double pi = Axis(p, i), di = Axis(d, i), hi = Axis(h, i);
                if (di == 0)
                    continue;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    double u = (sign * hi - pi) / di;
                    if (u > 0 && u < 1)
                        cuts[n++] = u;
                }
            }
            for (int k = 1; k < n; k++) // insertion sort, ascending
            {
                double v = cuts[k];
                int m = k - 1;
                for (; m >= 0 && cuts[m] > v; m--)
                    cuts[m + 1] = cuts[m];
                cuts[m + 1] = v;
            }
            best = double.PositiveInfinity;
            for (int k = 0; k + 1 < n; k++)
            {
                double u0 = cuts[k], u1 = cuts[k + 1];
                if (!(u1 > u0))
                    continue;
                double mid = 0.5 * (u0 + u1), a = 0, b = 0;
                for (int i = 0; i < 3; i++)
                {
                    double pi = Axis(p, i), di = Axis(d, i), hi = Axis(h, i), x = pi + mid * di;
                    if (x > hi || x < -hi)
                    {
                        a += di * di;
                        b += di * (pi - (x > hi ? hi : -hi));
                    }
                }
                double u = a > 0 ? -b / a : u0;
                u = u > u0 ? u < u1 ? u : u1 : u0;
                double f = Outer(h, p, d, u);
                if (f < best)
                {
                    best = f;
                    at = u;
                }
            }
            return at;
        }

        private static double Axis(Double3 v, int i) => i == 0 ? v.X : i == 1 ? v.Y : v.Z;

        /// <summary>max_i(|x_i| − h_i) at p + u·d: the box's signed distance inside it, a lower bound outside.</summary>
        private static double Inner(Double3 h, Double3 p, Double3 d, double u) => DetMath.Max(DetMath.Abs(p.X + u * d.X) - h.X,
            DetMath.Max(DetMath.Abs(p.Y + u * d.Y) - h.Y, DetMath.Abs(p.Z + u * d.Z) - h.Z));

        /// <summary>Σ max(|x_i| − h_i, 0)² at p + u·d: the squared distance outside the box.</summary>
        private static double Outer(Double3 h, Double3 p, Double3 d, double u)
        {
            double x = DetMath.Max(DetMath.Abs(p.X + u * d.X) - h.X, 0), y = DetMath.Max(DetMath.Abs(p.Y + u * d.Y) - h.Y, 0);
            double z = DetMath.Max(DetMath.Abs(p.Z + u * d.Z) - h.Z, 0);
            return x * x + y * y + z * z;
        }

        private static void Candidate(Double3 h, Double3 p, Double3 d, double u, ref double best, ref double at)
        {
            if (!(u >= 0 && u <= 1))
                return;
            double v = Inner(h, p, d, u);
            if (v < best || v == best && u < at)
            {
                best = v;
                at = u;
            }
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

        /// <summary>LocalSdf's value alone, the same bits: the search's evaluations, without a point and a normal.</summary>
        private static double Sdf(in ShapeGeometry s, Double3 x)
        {
            if (s.Kind == ShapeKind.Box)
            {
                Double3 h = s.HalfExtents;
                double qx = DetMath.Abs(x.X) - h.X, qy = DetMath.Abs(x.Y) - h.Y, qz = DetMath.Abs(x.Z) - h.Z;
                if (qx > 0 || qy > 0 || qz > 0)
                {
                    double vx = x.X - Limit(x.X, h.X), vy = x.Y - Limit(x.Y, h.Y), vz = x.Z - Limit(x.Z, h.Z);
                    return DetMath.Sqrt(vx * vx + vy * vy + vz * vz);
                }
                return qx >= qy && qx >= qz ? qx : qy >= qz ? qy : qz;
            }
            double radius = s.Radius, half = s.HalfLength, rho = DetMath.Sqrt(x.X * x.X + x.Z * x.Z);
            double dr = rho - radius, dy = DetMath.Abs(x.Y) - half;
            if (dr > 0 && dy > 0)
            {
                double ux = rho > 0 ? x.X / rho : 1, uz = rho > 0 ? x.Z / rho : 0;
                double vx = x.X - ux * radius, vy = x.Y - Sign(x.Y) * half, vz = x.Z - uz * radius;
                return DetMath.Sqrt(vx * vx + vy * vy + vz * vz);
            }
            return dr > 0 || dy <= 0 && dr >= dy ? dr : dy;
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
