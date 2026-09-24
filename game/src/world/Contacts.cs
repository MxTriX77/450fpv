using System;
using System.Numerics;

/// A query capsule: core segment A–B and radius, m. A sphere has A = B.
public readonly struct Capsule
{
    public readonly Double3 A, B;
    public readonly double Radius;

    public Capsule(Double3 a, Double3 b, double radius)
    {
        A = a;
        B = b;
        Radius = radius;
    }
}

/// A capsule's contact with one catalog shape or wire (world-query W-7).
public struct StaticContact
{
    public Double3 Point;   // on the object's surface, m
    public Vector3 Normal;  // unit, out of the object toward the capsule
    public double Distance; // signed, surface to surface, m: negative = penetration, positive = within the margin
    public int Object;      // index in objects.json, or a runtime object's index after them
    public ushort Shape;    // collision shape index in the catalog entry; 0 for a wire
    public ushort Material; // catalog material id: the shape's own, else the asset's
    public float WireParam; // along a wire's polyline by length, 0–1; −1 for other shapes
    public float Time;      // 1 = at the current pose; below 1 = the sweep went into or through the shape (StaticContacts)
}

/// A ray from Origin along Direction (normalised by Raycast), m.
public readonly struct Ray
{
    public readonly Double3 Origin, Direction;

    public Ray(Double3 origin, Double3 direction)
    {
        Origin = origin;
        Direction = direction;
    }
}

public struct RayHit
{
    public double Distance; // m along the ray, +∞ for a miss
    public Double3 Point;
    public Vector3 Normal;  // unit, out of the surface hit
    public int Object;      // −1 for terrain and for a miss
    public ushort Material; // catalog material id; Catalog.NoMaterial for terrain and for a miss
    public byte Surface;    // terrain hits: the surface under the point; 0 otherwise
}

/// Contacts and rays against the placed objects and wires (world-query W-7, W-8, D-010): pure C#, reentrant, no
/// allocation, the same bits on every x64 machine.
public sealed partial class WorldQuery
{
    /// A sweep touches a shape once it comes this close (conservative advancement stops there), m.
    const double TouchDistance = 1e-6;
    /// Conservative advancement gives up after this many steps and counts the shape as touched (only a sweep that
    /// grazes within a hair of a shape for its whole length gets there).
    const int MaxAdvance = 64;
    /// Rays walk the terrain at most this far, m.
    const double TerrainRayLimit = 10000;

    public int StaticContacts(in Capsule capsule, double margin, Span<StaticContact> results) =>
        StaticContacts(capsule, capsule, margin, results);

    /// World-query W-7: the contacts of a capsule (a sphere when A = B) that moved from `previous` to `current` during
    /// this step, with every catalog shape, wire and runtime object within `margin` (m): at most one per (object, shape),
    /// in canonical order (object, shape). Returns the true count, which exceeds `results.Length` on overflow; the buffer
    /// then holds the first contacts in canonical order. The capsule's radius is `current.Radius`.
    ///
    /// The sweep moves both core ends linearly, and conservative advancement finds the first time it touches each shape,
    /// so no shape is missed however thin it is and however far the capsule moved. Then, per shape:
    /// - never touched during the step: the contact at the current pose, if within the margin (Time = 1)
    /// - touched, and the current pose is within the margin on the side it touched from: the current contact (Time = 1)
    /// - touched at the very start of the step, and now clear on that same side: none (it moved away)
    /// - otherwise the capsule went into or through the shape and now sits past it, or on its far side where the closest
    ///   point would push it the wrong way: the contact at the first touch, with Time = that fraction of the step (below
    ///   1). The physics decides the response, for example by rewinding to that time.
    public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results)
    {
        double pad = current.Radius + margin;
        Bounds reach = Bounds.Of(previous.A, previous.B, pad).Union(Bounds.Of(current.A, current.B, pad));
        double speed = Math.Max((current.A - previous.A).Length(), (current.B - previous.B).Length());
        int count = 0;
        BroadRange(reach, out int x0, out int x1, out int z0, out int z1);
        for (int cz = z0; cz <= z1; cz++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                int cell = cz * _broadCells + cx;
                for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                {
                    ref readonly Prim s = ref _prims[_cellItems[k]];
                    // Each primitive is tested once: in the first cell it shares with the query.
                    if (Math.Max(s.CellX, x0) != cx || Math.Max(s.CellZ, z0) != cz || !s.Box.Overlaps(reach))
                        continue;
                    if (Contact(s, previous, current, margin, speed, reach, out StaticContact contact))
                        Insert(results, ref count, contact);
                }
            }
        }
        return count;
    }

    bool Contact(in Prim s, in Capsule previous, in Capsule current, double margin, double speed, in Bounds reach, out StaticContact contact)
    {
        double r = current.Radius;
        Closest(s, current.A, current.B, r, reach, out Near now);
        Near first = now, use = now;
        double t = 1;
        bool touched = false;
        if (speed > 0)
        {
            // The distance changes by at most `speed` per unit of t, so stepping by distance / speed never skips a touch.
            t = 0;
            for (int i = 0; ; i++)
            {
                Closest(s, previous.A + (current.A - previous.A) * t, previous.B + (current.B - previous.B) * t, r, reach, out first);
                if (first.Distance <= TouchDistance || i == MaxAdvance)
                {
                    touched = true;
                    break;
                }
                t += first.Distance / speed;
                if (!(t <= 1))
                    break;
            }
        }
        bool sameSide = Double3.Dot(now.Normal, first.Normal) > 0;
        float time = 1;
        if (touched && !(sameSide && now.Distance <= margin))
        {
            if (sameSide && t == 0)
            {
                contact = default;
                return false;
            }
            use = first;
            time = (float)Math.Min(t, 0.99999994); // the float below 1: never reads as the current pose
        }
        else if (!(now.Distance <= margin))
        {
            contact = default;
            return false;
        }
        contact = new StaticContact
        {
            Point = use.Point,
            Normal = use.Normal.ToVector3(),
            Distance = use.Distance,
            Object = s.Object,
            Shape = s.Shape,
            Material = s.Material,
            WireParam = (float)use.WireParam,
            Time = time,
        };
        return true;
    }

    /// Inserts in canonical order (object, shape); on overflow the buffer keeps the first ones. `count` is the true count.
    static void Insert(Span<StaticContact> results, ref int count, in StaticContact c)
    {
        int filled = Math.Min(count, results.Length), at = filled;
        while (at > 0 && (c.Object < results[at - 1].Object || c.Object == results[at - 1].Object && c.Shape < results[at - 1].Shape))
            at--;
        if (at < results.Length)
        {
            for (int k = Math.Min(filled, results.Length - 1); k > at; k--)
                results[k] = results[k - 1];
            results[at] = c;
        }
        count++;
    }

    /// World-query W-8: the first surface each ray enters within `maxDistance` (m, finite): catalog shapes, wires and
    /// runtime objects, and the rendered terrain triangles (which it enters only from above). A ray that starts inside a
    /// shape does not hit that shape. Micro-relief and pitfalls are not in the triangles; SampleGround gives them.
    public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits)
    {
        for (int i = 0; i < rays.Length; i++)
            hits[i] = Cast(rays[i], maxDistance);
    }

    RayHit Cast(in Ray ray, double maxDistance)
    {
        var hit = new RayHit { Distance = double.PositiveInfinity, Object = -1, Material = Catalog.NoMaterial };
        Double3 o = ray.Origin, d = Unit(ray.Direction);
        Bounds reach = Bounds.Of(o, o + d * maxDistance, 0);
        double best = maxDistance;
        Double3 normal = default;
        bool found = false;
        BroadRange(reach, out int x0, out int x1, out int z0, out int z1);
        for (int cz = z0; cz <= z1; cz++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                int cell = cz * _broadCells + cx;
                for (int k = _cellStart[cell]; k < _cellStart[cell + 1]; k++)
                {
                    ref readonly Prim s = ref _prims[_cellItems[k]];
                    if (Math.Max(s.CellX, x0) != cx || Math.Max(s.CellZ, z0) != cz || !s.Box.Overlaps(reach))
                        continue;
                    double t = double.PositiveInfinity;
                    Double3 n = default;
                    RayPrim(s, o, d, reach, ref t, ref n);
                    if (t <= best && (!found || t < best)) // a tie keeps the first hit found
                        (found, best, normal, hit.Object, hit.Material) = (true, t, n, s.Object, s.Material);
                }
            }
        }
        if (RayTerrain(o, d, Math.Min(best, TerrainRayLimit), out double terrain, out Double3 terrainNormal) && (!found || terrain < best))
        {
            (found, best, normal, hit.Object, hit.Material) = (true, terrain, terrainNormal, -1, Catalog.NoMaterial);
        }
        if (found)
        {
            hit.Distance = best;
            hit.Point = o + d * best;
            hit.Normal = normal.ToVector3();
            if (hit.Object < 0)
                hit.Surface = _surface[OwnCell(hit.Point.X, hit.Point.Z)];
        }
        return hit;
    }

    /// The first t in [0, maxT] where the ray passes from above the rendered terrain triangles to below them: a walk over
    /// the height cells its (x, z) track crosses. Cells outside the map repeat the edge samples, as SampleGround does.
    bool RayTerrain(Double3 o, Double3 d, double maxT, out double t, out Double3 normal)
    {
        t = 0;
        normal = default;
        if (!double.IsFinite(o.X + o.Y + o.Z + d.X + d.Y + d.Z + maxT))
            return false;
        // Only where the ray is within the terrain's height range (padded 1 mm, so flat terrain keeps an interval) can it hit.
        double t0 = 0, end = maxT, low = _minHeight - 1e-3, high = _maxHeight + 1e-3;
        if (d.Y != 0)
        {
            double ta = (high - o.Y) / d.Y, tb = (low - o.Y) / d.Y;
            (t0, end) = (Math.Max(0, Math.Min(ta, tb)), Math.Min(maxT, Math.Max(ta, tb)));
        }
        else if (o.Y < low || o.Y > high)
            return false;
        double fx = (o.X + Half) * _perHeightStep, fz = (o.Z + Half) * _perHeightStep;
        double dx = d.X * _perHeightStep, dz = d.Z * _perHeightStep;
        double ci = Math.Floor(fx + t0 * dx), cj = Math.Floor(fz + t0 * dz);
        while (t0 <= end)
        {
            double nextX = dx > 0 ? (ci + 1 - fx) / dx : dx < 0 ? (ci - fx) / dx : double.PositiveInfinity;
            double nextZ = dz > 0 ? (cj + 1 - fz) / dz : dz < 0 ? (cj - fz) / dz : double.PositiveInfinity;
            double t1 = Math.Min(Math.Min(nextX, nextZ), end);
            if (CellHit(ci, cj, fx, fz, dx, dz, o.Y, d.Y, t0, t1, out t, out normal))
                return true;
            if (t1 >= end)
                return false;
            if (nextX <= nextZ)
                ci += Sign(dx);
            else
                cj += Sign(dz);
            t0 = t1;
        }
        return false;
    }

    /// The ray against the two triangles of height cell (ci, cj) over [t0, t1]; the cell's diagonal splits the interval.
    bool CellHit(double ci, double cj, double fx, double fz, double dx, double dz, double oy, double dy, double t0, double t1,
        out double t, out Double3 normal)
    {
        int i0 = SampleIndex(ci), i1 = SampleIndex(ci + 1), j0 = SampleIndex(cj) * Samples, j1 = SampleIndex(cj + 1) * Samples;
        double h00 = _heights[j0 + i0], h10 = _heights[j0 + i1], h01 = _heights[j1 + i0], h11 = _heights[j1 + i1];
        double s0 = fx + t0 * dx - ci + (fz + t0 * dz - cj) - 1, rate = dx + dz; // u + v − 1 at t0, and its rate
        double split = rate != 0 ? t0 - s0 / rate : double.PositiveInfinity;
        if (split > t0 && split < t1)
        {
            return Piece(t0, split, out t, out normal) || Piece(split, t1, out t, out normal);
        }
        return Piece(t0, t1, out t, out normal);

        bool Piece(double ta, double tb, out double hit, out Double3 n)
        {
            bool lower = s0 + rate * ((ta + tb) / 2 - t0) <= 0; // triangle a-b-c (u + v ≤ 1), else b-d-c
            double ga = oy + ta * dy - Height(ta, lower), gb = oy + tb * dy - Height(tb, lower);
            hit = 0;
            n = default;
            if (!(ga >= 0 && gb <= 0))
                return false;
            hit = ga > gb ? ta + (tb - ta) * (ga / (ga - gb)) : ta;
            double gx = (lower ? h10 - h00 : h11 - h01) * _perHeightStep, gz = (lower ? h01 - h00 : h11 - h10) * _perHeightStep;
            n = Unit(new Double3(-gx, 1, -gz));
            return true;
        }

        double Height(double at, bool lower)
        {
            double u = fx + at * dx - ci, v = fz + at * dz - cj;
            return lower ? h00 + u * (h10 - h00) + v * (h01 - h00) : h11 + (1.0 - u) * (h01 - h11) + (1.0 - v) * (h10 - h11);
        }
    }

    int SampleIndex(double c) => c <= 0 ? 0 : c >= Samples - 1 ? Samples - 1 : (int)c;
}
