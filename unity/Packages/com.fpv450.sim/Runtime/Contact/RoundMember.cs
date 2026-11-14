using System;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>What a latched pair reads on its member (RoundMember.Read).</summary>
    public enum MemberReading : byte
    {
        /// <summary>No round member there (a box, a plane, a cylinder's cap or rim): the latched plane is its face.</summary>
        Face,

        /// <summary>The line along the latched normal meets the member: the depth is its exit.</summary>
        Along,

        /// <summary>The line misses the member: the part's core is beside it, off its side.</summary>
        Beside,
    }

    /// <summary>
    /// A latched pair on a round member (w9-village-physics 2.6d, ContactSpec.RollOff). A sphere, a capsule, a wire (a chain of
    /// capsules) and a cylinder's side have no face: their surface leaves the tangent plane at once, and a part whose give is more
    /// than the member is thick passes its axis while it presses (P1 keeps the side it came in by). Its depth is then the member's
    /// own along the latched normal n: the line c + s·n from the part's core point c nearest the member's core (the sphere's centre,
    /// the capsule's or cylinder's axis segment, the wire's nearest chain segment) meets the member grown by the part's radius r,
    /// the set within ρ = R + r of its core, over [s_in, s_out]; the depth is s_out (the part's give along n) and the contact point
    /// c + n·(s_out − r). Pressed straight in it is the plane's, ρ − h, through the axis and past it; with the core w across n,
    /// √(ρ² − w²) − h. A line that misses the set leaves the part beside the member: it rolled off its side.
    /// A cylinder's side pair has n square to the axis (beyond 1e-6 it is a cap's or rim's, a face), so the line keeps its axial
    /// place y: ρ is R + r for |y| ≤ H and R + √(r² − (|y| − H)²) over the rim's quarter circle to H + r (exact for that line).
    /// Only + − × ÷ √; no allocation.
    /// </summary>
    public static class RoundMember
    {
        /// <summary>A latched normal this far from square to a cylinder's axis is a cap's or a rim's (n·axis; rounding of a side's
        /// radial normal through the shape's frame is about 1e-16).</summary>
        public const double SideTolerance = 1e-6;

        /// <summary>
        /// The part with core a–b and radius r, latched with unit normal n on member g (chain: a wire's points, else empty): Along
        /// with its depth along n and the contact point, Beside with its clearance as a negative depth and the member's surface point
        /// nearest it, or Face (depth and point untouched) where the member has a face there.
        /// </summary>
        public static MemberReading Read(in ShapeGeometry g, ReadOnlySpan<Double3> chain, Double3 a, Double3 b, double r, Double3 n,
            ref double depth, ref Double3 point)
        {
            switch (g.Kind)
            {
                case ShapeKind.Sphere:
                {
                    Double3 c = OnSegment(a, b, g.Center);
                    double rho = g.Radius + r, lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                    Ball(c, n, g.Center, rho, ref lo, ref hi);
                    return Result(c, n, r, lo, hi, g.Center, g.Radius, ref depth, ref point);
                }
                case ShapeKind.Capsule:
                {
                    Double3 p0 = g.Center - g.AxisY * g.HalfLength, p1 = g.Center + g.AxisY * g.HalfLength;
                    Closest(a, b, p0, p1, out Double3 c, out Double3 m);
                    double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
                    Rod(c, n, p0, p1, g.Radius + r, ref lo, ref hi);
                    return Result(c, n, r, lo, hi, m, g.Radius, ref depth, ref point);
                }
                case ShapeKind.Wire:
                {
                    if (chain.Length < 2) return MemberReading.Face;
                    // The chain segment nearest the part's core, and its neighbours (a segment is far longer than ρ, so the line's
                    // exit lies on these three).
                    int near = 0;
                    double best = double.PositiveInfinity;
                    Double3 c = a, m = chain[0];
                    for (int i = 0; i + 1 < chain.Length; i++)
                    {
                        Closest(a, b, chain[i], chain[i + 1], out Double3 ci, out Double3 mi);
                        Double3 d = ci - mi;
                        double dd = Double3.Dot(d, d);
                        if (!(dd < best)) continue;
                        best = dd;
                        near = i;
                        c = ci;
                        m = mi;
                    }
                    double lo = double.PositiveInfinity, hi = double.NegativeInfinity, rho = g.Radius + r;
                    int from = near > 0 ? near - 1 : 0, to = near + 2 < chain.Length - 1 ? near + 2 : chain.Length - 1;
                    for (int i = from; i < to; i++) Rod(c, n, chain[i], chain[i + 1], rho, ref lo, ref hi);
                    return Result(c, n, r, lo, hi, m, g.Radius, ref depth, ref point);
                }
                case ShapeKind.Cylinder:
                {
                    Double3 u = g.AxisY;
                    double nu = Double3.Dot(n, u);
                    if (nu > SideTolerance || nu < -SideTolerance) return MemberReading.Face;
                    Double3 p0 = g.Center - u * g.HalfLength, p1 = g.Center + u * g.HalfLength;
                    Closest(a, b, p0, p1, out Double3 c, out Double3 m);
                    double y = Double3.Dot(c - g.Center, u), over = (y < 0 ? -y : y) - g.HalfLength;
                    double rim = over > 0 ? r * r - over * over : r * r;
                    Double3 off = c - g.Center - u * y;
                    if (rim >= 0)
                    {
                        // The cross-section at y: a disc about the axis, n in its plane.
                        double rho = g.Radius + DetMath.Sqrt(rim);
                        double hb = Double3.Dot(n, off), disc = hb * hb - (Double3.Dot(off, off) - rho * rho);
                        if (disc >= 0)
                        {
                            double root = DetMath.Sqrt(disc);
                            return Result(c, n, r, -hb - root, -hb + root, m, g.Radius, ref depth, ref point);
                        }
                    }
                    // Beside: c's clearance from the cylinder itself (its radial and axial excess), less r.
                    double radial = off.Length(), across = radial - g.Radius, outR = across > 0 ? across : 0, outY = over > 0 ? over : 0;
                    depth = r - DetMath.Sqrt(outR * outR + outY * outY);
                    Double3 side = radial > 0 ? off * (1 / radial) : default;
                    double yOn = y > g.HalfLength ? g.HalfLength : y < -g.HalfLength ? -g.HalfLength : y;
                    point = g.Center + u * yOn + side * (radial < g.Radius ? radial : g.Radius);
                    return MemberReading.Beside;
                }
                default:
                    return MemberReading.Face;
            }
        }

        // The line's exit, or beside the member: its clearance from the core point m (radius R) as a negative depth.
        static MemberReading Result(Double3 c, Double3 n, double r, double lo, double hi, Double3 m, double radius, ref double depth, ref Double3 point)
        {
            if (hi >= lo)
            {
                depth = hi;
                point = c + n * (hi - r);
                return MemberReading.Along;
            }
            Double3 d = c - m;
            double length = d.Length();
            depth = radius + r - length;
            point = length > 0 ? m + d * (radius / length) : m;
            return MemberReading.Beside;
        }

        // The line c + s·n against the ball of radius rho about o: its span joined to [lo, hi].
        static void Ball(Double3 c, Double3 n, Double3 o, double rho, ref double lo, ref double hi)
        {
            Double3 m = c - o;
            double hb = Double3.Dot(n, m), disc = hb * hb - (Double3.Dot(m, m) - rho * rho);
            if (!(disc >= 0)) return;
            double root = DetMath.Sqrt(disc);
            Join(-hb - root, -hb + root, ref lo, ref hi);
        }

        // The line c + s·n against the capsule of radius rho about p0–p1 (convex: its span is the join of the round body's and
        // the end balls').
        static void Rod(Double3 c, Double3 n, Double3 p0, Double3 p1, double rho, ref double lo, ref double hi)
        {
            Ball(c, n, p0, rho, ref lo, ref hi);
            Ball(c, n, p1, rho, ref lo, ref hi);
            Double3 d = p1 - p0;
            double dd = Double3.Dot(d, d);
            if (!(dd > 0)) return;
            Double3 m = c - p0;
            double nd = Double3.Dot(n, d), md = Double3.Dot(m, d);
            Double3 nPerp = n - d * (nd / dd), mPerp = m - d * (md / dd);
            double qa = Double3.Dot(nPerp, nPerp), hb = Double3.Dot(nPerp, mPerp), qc = Double3.Dot(mPerp, mPerp) - rho * rho;
            double s0, s1;
            if (qa > 0)
            {
                double disc = hb * hb - qa * qc;
                if (!(disc >= 0)) return;
                double root = DetMath.Sqrt(disc);
                s0 = (-hb - root) / qa;
                s1 = (-hb + root) / qa;
            }
            else
            {
                if (!(qc <= 0)) return; // along the axis, outside the round body
                s0 = double.NegativeInfinity;
                s1 = double.PositiveInfinity;
            }
            // Within the segment's span along its axis: 0 ≤ (m + s·n)·d ≤ |d|².
            if (nd != 0)
            {
                double t0 = -md / nd, t1 = (dd - md) / nd;
                if (t0 > t1)
                {
                    double t = t0;
                    t0 = t1;
                    t1 = t;
                }
                if (t0 > s0) s0 = t0;
                if (t1 < s1) s1 = t1;
            }
            else if (md < 0 || md > dd) return;
            if (s1 >= s0) Join(s0, s1, ref lo, ref hi);
        }

        static void Join(double s0, double s1, ref double lo, ref double hi)
        {
            if (s0 < lo) lo = s0;
            if (s1 > hi) hi = s1;
        }

        static Double3 OnSegment(Double3 a, Double3 b, Double3 x)
        {
            Double3 d = b - a;
            double dd = Double3.Dot(d, d);
            if (!(dd > 0)) return a;
            double t = Double3.Dot(x - a, d) / dd;
            return a + d * (t < 0 ? 0 : t > 1 ? 1 : t);
        }

        // The closest points of segments a–b (the part's core: c) and p–q (the member's: m) (Ericson, Real-Time Collision
        // Detection, 5.1.9; a parallel pair takes a's end).
        static void Closest(Double3 a, Double3 b, Double3 p, Double3 q, out Double3 c, out Double3 m)
        {
            Double3 d1 = b - a, d2 = q - p, w = a - p;
            double e1 = Double3.Dot(d1, d1), e2 = Double3.Dot(d2, d2), f = Double3.Dot(d2, w);
            double s, t;
            if (!(e1 > 0) && !(e2 > 0))
            {
                s = t = 0;
            }
            else if (!(e1 > 0))
            {
                s = 0;
                t = Clamp(f / e2);
            }
            else
            {
                double k = Double3.Dot(d1, w);
                if (!(e2 > 0))
                {
                    t = 0;
                    s = Clamp(-k / e1);
                }
                else
                {
                    double bb = Double3.Dot(d1, d2), den = e1 * e2 - bb * bb;
                    s = den > 0 ? Clamp((bb * f - k * e2) / den) : 0;
                    t = (bb * s + f) / e2;
                    if (t < 0)
                    {
                        t = 0;
                        s = Clamp(-k / e1);
                    }
                    else if (t > 1)
                    {
                        t = 1;
                        s = Clamp((bb - k) / e1);
                    }
                }
            }
            c = a + d1 * s;
            m = p + d2 * t;
        }

        static double Clamp(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
    }
}
