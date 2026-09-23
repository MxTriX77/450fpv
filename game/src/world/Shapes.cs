using System;

/// The geometry behind StaticContacts, Raycast and the wind grid: closest points between a capsule core and each
/// primitive, ray entries and support points. Pure C#, only correctly rounded IEEE operations (W-13), no allocation.
public sealed partial class WorldQuery
{
    /// The closest approach of a capsule to one primitive.
    struct Near
    {
        public double Distance;  // signed, surface to surface: negative = overlap
        public Double3 Point;    // on the primitive's surface
        public Double3 Normal;   // unit, out of the primitive toward the capsule
        public double WireParam; // along a wire's polyline by length, 0–1; −1 for other shapes
    }

    /// The closest approach of the capsule with core p–q and radius r to `s`. Wire segments outside `reach` are skipped,
    /// because they are farther than the query cares about; with none left, Distance is +∞.
    void Closest(in Prim s, Double3 p, Double3 q, double r, in Bounds reach, out Near near)
    {
        near = default;
        near.WireParam = -1;
        switch (s.Kind)
        {
            case ShapeKind.Sphere:
                Round(OnSegment(p, q, s.C), s.C, s.Radius, r, q - p, ref near);
                break;
            case ShapeKind.Capsule:
            {
                Double3 a = s.C - s.R.Y * s.Half.Y, b = s.C + s.R.Y * s.Half.Y;
                SegmentSegment(p, q, a, b, out double u, out double v);
                Round(p + (q - p) * u, a + (b - a) * v, s.Radius, r, s.R.Y, ref near);
                break;
            }
            case ShapeKind.Wire:
            {
                double best = double.PositiveInfinity, bu = 0, bv = 0;
                int at = -1;
                for (int i = s.First; i < s.First + s.Count - 1; i++)
                {
                    Double3 a = _wirePoints[i], b = _wirePoints[i + 1];
                    if (!Bounds.Of(a, b, s.Radius).Overlaps(reach))
                        continue;
                    SegmentSegment(p, q, a, b, out double u, out double v);
                    Double3 d = p + (q - p) * u - (a + (b - a) * v);
                    if (Double3.Dot(d, d) < best)
                        (best, at, bu, bv) = (Double3.Dot(d, d), i, u, v);
                }
                if (at < 0)
                {
                    near.Distance = double.PositiveInfinity;
                    break;
                }
                Double3 a0 = _wirePoints[at], b0 = _wirePoints[at + 1];
                Round(p + (q - p) * bu, a0 + (b0 - a0) * bv, s.Radius, r, b0 - a0, ref near);
                near.WireParam = (_wireAlong[at] + (_wireAlong[at + 1] - _wireAlong[at]) * bv) / _wireAlong[s.First + s.Count - 1];
                break;
            }
            default:
            {
                // Box or cylinder: minimise its signed distance along the core, in its own frame.
                Double3 lp = s.R.ToLocal(p - s.C), lq = s.R.ToLocal(q - s.C);
                double u = Double3.Dot(lq - lp, lq - lp) > 0 ? MinimizeAlong(s, lp, lq) : 0;
                near.Distance = LocalSdf(s, lp + (lq - lp) * u, out Double3 surface, out Double3 normal) - r;
                near.Point = s.C + s.R.ToWorld(surface);
                near.Normal = s.R.ToWorld(normal);
                break;
            }
        }
    }

    /// Core point x against a round primitive around its centre or axis point y with radius R. When x lies exactly on the
    /// centre or axis, the normal is any direction perpendicular to `axis`.
    static void Round(Double3 x, Double3 y, double R, double r, Double3 axis, ref Near near)
    {
        Double3 v = x - y;
        double length = v.Length();
        Double3 n = length > 0 ? v * (1 / length) : Radial(axis);
        near.Distance = length - R - r;
        near.Point = y + n * R;
        near.Normal = n;
    }

    static Double3 Radial(Double3 axis)
    {
        double length = axis.Length();
        Double3 a = length > 0 ? axis * (1 / length) : new Double3(0, 1, 0);
        Double3 c = Double3.Cross(a, Math.Abs(a.X) < 0.9 ? new Double3(1, 0, 0) : new Double3(0, 0, 1));
        return c * (1 / c.Length());
    }

    /// NaN goes to 0.
    static double Clamp01(double t) => t > 0 ? t < 1 ? t : 1 : 0;

    static double Sign(double v) => v < 0 ? -1 : 1;

    static Double3 Unit(Double3 v) => v * (1 / v.Length());

    static Double3 OnSegment(Double3 p, Double3 q, Double3 c)
    {
        Double3 d = q - p;
        double dd = Double3.Dot(d, d);
        return dd > 0 ? p + d * Clamp01(Double3.Dot(c - p, d) / dd) : p;
    }

    /// Closest points of segments p1–q1 and p2–q2 at parameters s and t (Ericson, Real-Time Collision Detection, 5.1.9).
    static void SegmentSegment(Double3 p1, Double3 q1, Double3 p2, Double3 q2, out double s, out double t)
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

    /// The u in [0, 1] that minimises the signed distance of a box or cylinder at lp + u·(lq − lp). The signed distance
    /// of a convex solid is convex along a line, so a golden-section search finds it (the bracket shrinks to 4e-9); both
    /// ends are checked too, so a capsule end resting on a face is exact.
    static double MinimizeAlong(in Prim s, Double3 lp, Double3 lq)
    {
        const double g = 0.6180339887498949;
        Double3 d = lq - lp;
        double lo = 0, hi = 1, u1 = 1 - g, u2 = g;
        double f1 = LocalSdf(s, lp + d * u1, out _, out _), f2 = LocalSdf(s, lp + d * u2, out _, out _);
        for (int i = 0; i < 40; i++)
        {
            if (f1 <= f2)
            {
                (hi, u2, f2) = (u2, u1, f1);
                u1 = hi - g * (hi - lo);
                f1 = LocalSdf(s, lp + d * u1, out _, out _);
            }
            else
            {
                (lo, u1, f1) = (u1, u2, f2);
                u2 = lo + g * (hi - lo);
                f2 = LocalSdf(s, lp + d * u2, out _, out _);
            }
        }
        (double u, double f) = f1 <= f2 ? (u1, f1) : (u2, f2);
        double start = LocalSdf(s, lp, out _, out _), end = LocalSdf(s, lq, out _, out _);
        if (start <= f)
            (u, f) = (0, start);
        return end < f ? 1 : u;
    }

    /// Signed distance of a box or a cylinder (along local Y) at the local point x, with the nearest surface point and
    /// the outward normal there. Inside, the nearest face wins (ties go to x, then y, then z; a cylinder's side before
    /// its cap).
    static double LocalSdf(in Prim s, Double3 x, out Double3 surface, out Double3 normal)
    {
        if (s.Kind == ShapeKind.Box)
        {
            Double3 h = s.Half;
            double qx = Math.Abs(x.X) - h.X, qy = Math.Abs(x.Y) - h.Y, qz = Math.Abs(x.Z) - h.Z;
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
        double radius = s.Radius, half = s.Half.Y, rho = Math.Sqrt(x.X * x.X + x.Z * x.Z);
        double dr = rho - radius, dy = Math.Abs(x.Y) - half, sy = Sign(x.Y);
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

    /// Where the ray o + t·d (d unit) first enters `s`, if at some t ≤ `t` (then `t` and `normal` are updated; call it
    /// with t = +∞). A ray that starts inside a primitive does not hit it. Wire segments outside `reach` are skipped.
    void RayPrim(in Prim s, Double3 o, Double3 d, in Bounds reach, ref double t, ref Double3 normal)
    {
        switch (s.Kind)
        {
            case ShapeKind.Sphere:
                RaySphere(o, d, s.C, s.Radius, ref t, ref normal);
                break;
            case ShapeKind.Capsule:
                RayCapsule(o, d, s.C - s.R.Y * s.Half.Y, s.C + s.R.Y * s.Half.Y, s.Radius, ref t, ref normal);
                break;
            case ShapeKind.Wire:
                for (int i = s.First; i < s.First + s.Count - 1; i++)
                {
                    if (Bounds.Of(_wirePoints[i], _wirePoints[i + 1], s.Radius).Overlaps(reach))
                        RayCapsule(o, d, _wirePoints[i], _wirePoints[i + 1], s.Radius, ref t, ref normal);
                }
                break;
            case ShapeKind.Box:
                RayBox(s, o, d, ref t, ref normal);
                break;
            default:
                RayCylinder(s, o, d, ref t, ref normal);
                break;
        }
    }

    static void RaySphere(Double3 o, Double3 d, Double3 c, double radius, ref double t, ref Double3 normal)
    {
        Double3 oc = o - c;
        double b = Double3.Dot(oc, d), k = Double3.Dot(oc, oc) - radius * radius, h = b * b - k;
        if (k < 0 || h < 0)
            return;
        double hit = -b - Math.Sqrt(h);
        if (hit >= 0 && hit <= t)
            (t, normal) = (hit, Unit(oc + d * hit));
    }

    /// Ray and capsule a–b (Quílez's closed form for the side, then both end spheres).
    static void RayCapsule(Double3 o, Double3 d, Double3 a, Double3 b, double radius, ref double t, ref Double3 normal)
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
                double hit = (-k1 - Math.Sqrt(h)) / k2, y = baoa + hit * bard;
                if (hit >= 0 && hit <= t && y > 0 && y < baba)
                    (t, normal) = (hit, Unit(oa + d * hit - ba * (y / baba)));
            }
        }
        RaySphere(o, d, a, radius, ref t, ref normal);
        RaySphere(o, d, b, radius, ref t, ref normal);
    }

    static void RayBox(in Prim s, Double3 o, Double3 d, ref double t, ref Double3 normal)
    {
        Double3 lo = s.R.ToLocal(o - s.C), ld = s.R.ToLocal(d);
        double near = double.NegativeInfinity, far = double.PositiveInfinity;
        int axis = -1;
        if (!Slab(lo.X, ld.X, s.Half.X, 0, ref near, ref far, ref axis) || !Slab(lo.Y, ld.Y, s.Half.Y, 1, ref near, ref far, ref axis)
            || !Slab(lo.Z, ld.Z, s.Half.Z, 2, ref near, ref far, ref axis) || axis < 0 || near > far || near < 0 || near > t)
            return;
        t = near;
        normal = s.R.ToWorld(axis == 0 ? new Double3(-Sign(ld.X), 0, 0) : axis == 1 ? new Double3(0, -Sign(ld.Y), 0) : new Double3(0, 0, -Sign(ld.Z)));
    }

    static bool Slab(double o, double d, double h, int i, ref double near, ref double far, ref int axis)
    {
        if (d == 0)
            return Math.Abs(o) <= h;
        double t1 = (-h - o) / d, t2 = (h - o) / d;
        if (t1 > t2)
            (t1, t2) = (t2, t1);
        if (t1 > near)
            (near, axis) = (t1, i);
        far = Math.Min(far, t2);
        return true;
    }

    static void RayCylinder(in Prim s, Double3 o, Double3 d, ref double t, ref Double3 normal)
    {
        Double3 lo = s.R.ToLocal(o - s.C), ld = s.R.ToLocal(d);
        double radius = s.Radius, half = s.Half.Y, c = lo.X * lo.X + lo.Z * lo.Z - radius * radius;
        if (c < 0 && Math.Abs(lo.Y) < half)
            return;
        double a = ld.X * ld.X + ld.Z * ld.Z;
        if (a > 1e-24)
        {
            double b = lo.X * ld.X + lo.Z * ld.Z, h = b * b - a * c;
            if (h >= 0)
            {
                double hit = (-b - Math.Sqrt(h)) / a;
                if (hit >= 0 && hit <= t && Math.Abs(lo.Y + hit * ld.Y) <= half)
                    (t, normal) = (hit, s.R.ToWorld(Unit(new Double3(lo.X + hit * ld.X, 0, lo.Z + hit * ld.Z))));
            }
        }
        if (ld.Y != 0)
        {
            double hit = ((ld.Y > 0 ? -half : half) - lo.Y) / ld.Y, px = lo.X + hit * ld.X, pz = lo.Z + hit * ld.Z;
            if (hit >= 0 && hit <= t && px * px + pz * pz <= radius * radius)
                (t, normal) = (hit, s.R.ToWorld(new Double3(0, -Sign(ld.Y), 0)));
        }
    }

    /// The point of `s` farthest along the unit direction `dir` (on a tie, the + side of each axis).
    static Double3 Support(in Prim s, Double3 dir)
    {
        switch (s.Kind)
        {
            case ShapeKind.Box:
                return s.C + s.R.X * (Sign(Double3.Dot(dir, s.R.X)) * s.Half.X) + s.R.Y * (Sign(Double3.Dot(dir, s.R.Y)) * s.Half.Y)
                    + s.R.Z * (Sign(Double3.Dot(dir, s.R.Z)) * s.Half.Z);
            case ShapeKind.Sphere:
                return s.C + dir * s.Radius;
            case ShapeKind.Capsule:
                return s.C + s.R.Y * (Sign(Double3.Dot(dir, s.R.Y)) * s.Half.Y) + dir * s.Radius;
            default:
            {
                Double3 end = s.C + s.R.Y * (Sign(Double3.Dot(dir, s.R.Y)) * s.Half.Y), across = dir - s.R.Y * Double3.Dot(dir, s.R.Y);
                double length = across.Length();
                return length > 0 ? end + across * (s.Radius / length) : end;
            }
        }
    }
}
