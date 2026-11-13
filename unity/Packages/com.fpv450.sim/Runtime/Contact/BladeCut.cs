using System;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// Where a blade meets a shape (p7f-damage design 4): the shape cut by the disc's slab, the plane through the hub normal to
    /// the axis grown by the blade's half-thickness envelope h along the axis (the pitched blade's height, drone.json's
    /// half_thickness_m). A blade at azimuth ψ is the radial ribbon {hub + r·ê(ψ) + z·axis : |z| ≤ h}, so it meets the shape
    /// at the smallest r at which the ribbon's segment there touches it, r_in(ψ); the shape's region in the plane is convex
    /// for a convex shape (the projection of the shape within the slab), so r_in is unimodal in ψ and its least value, the
    /// meeting radius of a pass across it, is the region's nearest point to the hub.
    /// - The nearest point: the shape's closest approach to the hub's own segment {hub + z·axis}; when that point lies within
    ///   the slab it is the region's nearest point exactly (the in-plane distance there is the 3D one, and nowhere in the slab
    ///   is nearer). A plane (the ground's half-space) is closed form. Otherwise (the shape lies above or below the disc at
    ///   the hub and cuts the slab farther out: a face under a tilted disc, a sphere or a limb over it) the slab's cut is met
    ///   along that point's in-plane direction, r_in there: exact where the cut's nearest point lies along it (a face, whose
    ///   steepest line it is; a sphere; an upright round shape), an upper bound on r_in elsewhere (a limb sloping across the
    ///   disc), which the pass then meets a little farther out (assumed: no search over the azimuth, which costs 60 lines'
    ///   Newton solves a pair a step). Nothing nearer than the shape's 3D distance from the hub's segment can be met, so a
    ///   shape farther than the reach is refused at once.
    /// - r_in and the exit r_out along a line: Newton's method on the ribbon segment's distance to the shape (ShapeMath's
    ///   exact closest approach for each primitive, a wire's chain segment by segment), which is convex in r for a convex
    ///   shape, so the steps close on the first root from either side without passing it.
    /// Only + − × ÷ √ and DetMath; no allocation.
    /// </summary>
    public static class BladeCut
    {
        /// <summary>A ribbon this close to a shape meets it, m; Newton stops there.</summary>
        public const double Touch = 1e-12;

        /// <summary>Newton's steps at most along one line (each reaches the root quadratically; a grazing line halves its gap).</summary>
        public const int Steps = 64;

        /// <summary>Where a blade meets a shape: the radius along the blade, m; the blade's unit direction there (in the disc's
        /// plane); the point on the shape's surface; its unit normal, out of the shape.</summary>
        public struct Meeting
        {
            public double Radius;
            public Double3 Direction, Point, Normal;
        }

        /// <summary>
        /// The nearest point of a shape's cut by the disc's slab (hub, unit axis, half-thickness h) to the hub, within reach (m);
        /// false when the slab's cut does not come within reach. chain is a wire's segment points (empty for a primitive). A
        /// shape holding the hub's own segment meets it at radius 0.
        /// </summary>
        public static bool Nearest(in ShapeGeometry s, ReadOnlySpan<Double3> chain, Double3 hub, Double3 axis, double h, double reach, out Meeting m)
        {
            m = default;
            if (s.Kind == ShapeKind.Plane) return NearestPlane(s, hub, axis, h, reach, out m);
            Closest(s, chain, hub - axis * h, hub + axis * h, out Near near);
            if (!(near.Distance < reach)) return false;
            if (near.Distance <= 0)
            {
                m.Radius = 0;
                m.Direction = InPlane(-near.Normal, axis);
                m.Point = near.Point;
                m.Normal = near.Normal;
                return true;
            }
            Double3 v = near.Point - hub;
            double z = Double3.Dot(v, axis);
            if (z <= h && z >= -h)
            {
                Double3 d = v - axis * z;
                double r = d.Length();
                if (!(r < reach)) return false;
                m.Radius = r;
                m.Direction = d * (1 / r);
                m.Point = near.Point;
                m.Normal = near.Normal;
                return true;
            }
            Double3 dir = InPlane(v, axis);
            if (!Entry(s, chain, hub, axis, dir, h, reach, out double rin, out Double3 point, out Double3 normal)) return false;
            m.Radius = rin;
            m.Direction = dir;
            m.Point = point;
            m.Normal = normal;
            return true;
        }

        /// <summary>
        /// A shape the disc's slab passed through within one step (w9-village-physics 2.6's addendum): in neither the slab at the
        /// step's start hub (from) nor at its end (now), but in the slab at a hub between, the slab swept along the step's motion
        /// (a thin branch or wire met along the axis faster than its thickness and the slab's 2h a step). The slab is sampled at
        /// most h apart along the move; m and hub are the first sample that cuts the shape, and [lo, hi] the step's share during
        /// which the slab held it (each sample standing for its own share). False when no sample between cuts it.
        /// </summary>
        public static bool Crossing(in ShapeGeometry s, ReadOnlySpan<Double3> chain, Double3 from, Double3 now, Double3 axis, double h, double reach,
            out Meeting m, out Double3 hub, out double lo, out double hi)
        {
            m = default;
            hub = now;
            lo = hi = 0;
            Double3 move = now - from;
            double travel = DetMath.Abs(Double3.Dot(move, axis));
            if (!(travel > h) || Nearest(s, chain, from, axis, h, reach, out _)) return false;
            int n = (int)(travel / h) + 1;
            bool found = false;
            for (int k = 1; k < n; k++)
            {
                double t = k / (double)n;
                Double3 at = from + move * t;
                if (!Nearest(s, chain, at, axis, h, reach, out Meeting mk)) continue;
                if (!found)
                {
                    found = true;
                    m = mk;
                    hub = at;
                    lo = t - 0.5 / n;
                }
                hi = t + 0.5 / n;
            }
            return found;
        }

        /// <summary>The shape along one blade line (unit dir, in the disc's plane): where its ribbon first meets it, rIn, and
        /// where it leaves it, rOut (+∞ when it is still inside at reach); false when the line misses it within reach.</summary>
        public static bool Along(in ShapeGeometry s, ReadOnlySpan<Double3> chain, Double3 hub, Double3 axis, Double3 dir, double h, double reach,
            out double rIn, out double rOut, out Double3 point, out Double3 normal)
        {
            rOut = double.PositiveInfinity;
            if (!Entry(s, chain, hub, axis, dir, h, reach, out rIn, out point, out normal)) return false;
            rOut = Exit(s, chain, hub, axis, dir, h, reach);
            return true;
        }

        /// <summary>
        /// Whether a box the blade line along dir leaves again at rOut, inside the tip's radius rTip, still reaches the tip's circle
        /// beside that line (w3-farmland-ground 2.7): the ribbon at rTip at the two azimuths whose points lie (rIn + rOut)/2 along
        /// dir, the middle of the line's cut. A wall's thin skin (a strip across the disc) holds them, so the blade's tip meets
        /// its face first, as a slab's; a post wholly inside the rim holds neither, and its leading edge meets it. Round shapes and
        /// wires keep the leading edge's rule (a rod, a pole, a conductor, a limb: centre d, radius a, d + a &lt; rTip, the points
        /// lying √(rTip² − d²) &gt; a aside).
        /// </summary>
        public static bool Wide(in ShapeGeometry s, ReadOnlySpan<Double3> chain, Double3 hub, Double3 axis, Double3 dir, double h, double rIn,
            double rOut, double rTip)
        {
            if (s.Kind != ShapeKind.Box) return false;
            double along = 0.5 * (rIn + rOut);
            if (!(along < rTip)) return true;
            double aside = DetMath.Sqrt(rTip * rTip - along * along);
            Double3 across = Double3.Cross(axis, dir);
            for (int side = -1; side <= 1; side += 2)
            {
                Double3 at = hub + dir * along + across * (side * aside);
                Closest(s, chain, at - axis * h, at + axis * h, out Near near);
                if (near.Distance <= 0) return true;
            }
            return false;
        }

        /// <summary>A wire's chain segments within reach of the hub (lo .. hi, segment i from point i to i + 1; empty when hi &lt;
        /// lo), so a pass reads only those.</summary>
        public static void WireWindow(ReadOnlySpan<Double3> chain, double radius, Double3 hub, double reach, out int lo, out int hi)
        {
            lo = chain.Length;
            hi = -1;
            double r = reach + radius;
            for (int i = 0; i + 1 < chain.Length; i++)
            {
                Double3 d = hub - OnSegment(chain[i], chain[i + 1], hub);
                if (!(Double3.Dot(d, d) <= r * r)) continue;
                if (i < lo) lo = i;
                hi = i;
            }
        }

        // ---- the lines ----------------------------------------------------------------------------------------------------------

        static bool Entry(in ShapeGeometry s, ReadOnlySpan<Double3> chain, Double3 hub, Double3 axis, Double3 dir, double h, double reach,
            out double r, out Double3 point, out Double3 normal)
        {
            r = 0;
            point = normal = default;
            if (s.Kind == ShapeKind.Plane) return EntryPlane(s, hub, axis, dir, h, reach, out r, out point, out normal);
            Near near = default;
            for (int k = 0; k < Steps; k++)
            {
                Double3 at = hub + dir * r;
                Closest(s, chain, at - axis * h, at + axis * h, out near);
                double f = near.Distance;
                if (f <= Touch)
                {
                    point = near.Point;
                    normal = near.Normal;
                    return true;
                }
                double g = Double3.Dot(near.Normal, dir);
                if (!(g < 0)) return false; // moving away: a convex shape is never met past here
                double next = r - f / g;
                if (!(next < reach)) return false;
                if (!(next > r)) break;
                r = next;
            }
            point = near.Point;
            normal = near.Normal;
            return near.Distance <= 1e-9; // a line grazing the region's edge, closed to within a nanometre
        }

        // Where the line leaves the shape, from the reach inward; +∞ when the shape still holds the ribbon at the reach.
        static double Exit(in ShapeGeometry s, ReadOnlySpan<Double3> chain, Double3 hub, Double3 axis, Double3 dir, double h, double reach)
        {
            if (s.Kind == ShapeKind.Plane) return double.PositiveInfinity; // a half-space never ends along a line that enters it
            double r = reach;
            for (int k = 0; k < Steps; k++)
            {
                Double3 at = hub + dir * r;
                Closest(s, chain, at - axis * h, at + axis * h, out Near near);
                double f = near.Distance;
                if (f <= Touch) return k == 0 ? double.PositiveInfinity : r;
                double g = Double3.Dot(near.Normal, dir);
                if (!(g > 0)) return double.PositiveInfinity;
                double next = r - f / g;
                if (!(next < r)) return r;
                r = next;
            }
            return r;
        }

        // ---- the plane, closed form ---------------------------------------------------------------------------------------------

        // The half-space below the plane through C with normal N: the ribbon at r along ê is in it when N·(hub − C) + r·N·ê −
        // h·|N·a| ≤ 0, so r_in(ê) = (N·(hub − C) − h·|N·a|)/(−N·ê) and the nearest is along −N's in-plane part.
        static bool NearestPlane(in ShapeGeometry s, Double3 hub, Double3 axis, double h, double reach, out Meeting m)
        {
            m = default;
            Double3 n = s.AxisY;
            double na = Double3.Dot(n, axis), above = Double3.Dot(n, hub - s.Center) - h * DetMath.Abs(na);
            Double3 inPlane = n - axis * na;
            double length = inPlane.Length();
            if (above <= 0)
            {
                m.Radius = 0;
                m.Direction = length > 0 ? inPlane * (-1 / length) : InPlane(new Double3(1, 0, 0), axis);
            }
            else
            {
                if (!(length > 0)) return false;
                double r = above / length;
                if (!(r < reach)) return false;
                m.Radius = r;
                m.Direction = inPlane * (-1 / length);
            }
            Double3 corner = hub + m.Direction * m.Radius - axis * (na < 0 ? -h : h);
            m.Point = corner - n * Double3.Dot(n, corner - s.Center);
            m.Normal = n;
            return true;
        }

        static bool EntryPlane(in ShapeGeometry s, Double3 hub, Double3 axis, Double3 dir, double h, double reach, out double r, out Double3 point,
            out Double3 normal)
        {
            Double3 n = s.AxisY;
            double na = Double3.Dot(n, axis), above = Double3.Dot(n, hub - s.Center) - h * DetMath.Abs(na), toward = -Double3.Dot(n, dir);
            normal = n;
            r = above <= 0 ? 0 : toward > 0 ? above / toward : double.PositiveInfinity;
            Double3 corner = hub + dir * (r < double.PositiveInfinity ? r : 0) - axis * (na < 0 ? -h : h);
            point = corner - n * Double3.Dot(n, corner - s.Center);
            return r < reach;
        }

        // ---- the shapes' closest approach -----------------------------------------------------------------------------------------

        // The segment p–q's closest approach to the shape: ShapeMath's for a primitive, the nearest of the chain's segments for a
        // wire (each a capsule of the wire's radius).
        static void Closest(in ShapeGeometry s, ReadOnlySpan<Double3> chain, Double3 p, Double3 q, out Near near)
        {
            if (s.Kind != ShapeKind.Wire)
            {
                ShapeMath.Closest(s, p, q, 0, out near);
                return;
            }
            near = default;
            near.Distance = double.PositiveInfinity;
            for (int i = 0; i + 1 < chain.Length; i++)
            {
                Double3 a = chain[i], b = chain[i + 1], d = b - a;
                double length = d.Length();
                if (!(length > 0)) continue;
                var segment = new ShapeGeometry
                {
                    Kind = ShapeKind.Capsule, Center = (a + b) * 0.5, AxisY = d * (1 / length), HalfLength = 0.5 * length, Radius = s.Radius,
                    Material = s.Material,
                };
                ShapeMath.Closest(segment, p, q, 0, out Near n);
                if (n.Distance < near.Distance) near = n;
            }
        }

        static Double3 InPlane(Double3 v, Double3 axis)
        {
            Double3 d = v - axis * Double3.Dot(v, axis);
            double length = d.Length();
            if (length > 0) return d * (1 / length);
            Double3 c = Double3.Cross(axis, DetMath.Abs(axis.X) < 0.9 ? new Double3(1, 0, 0) : new Double3(0, 0, 1));
            return c * (1 / c.Length());
        }

        static Double3 OnSegment(Double3 a, Double3 b, Double3 x)
        {
            Double3 d = b - a;
            double dd = Double3.Dot(d, d);
            if (!(dd > 0)) return a;
            double t = Double3.Dot(x - a, d) / dd;
            return a + d * (t < 0 ? 0 : t > 1 ? 1 : t);
        }
    }
}
