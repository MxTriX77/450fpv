using System;
using System.Numerics;

/// One stem, straw, twig or leaf from the micro-detail generator (world-query W-5, W-6). Blittable, 64 bytes.
public struct MicroElement
{
    public ulong Id;           // stable: map seed bits, 0.25 m cell, kind and slot (see ElementId)
    public Double3 Base;       // root, m: standing grass at GroundHeight, lying elements on SupportTop
    public Vector3 Direction;  // unit, base to tip
    public float Length;       // m
    public float Diameter;     // m
    public float TipStiffness; // N/m at its own tip: table × (d/d̄)⁴ × (L̄/L)³
    public float HookRelease;  // N, the pull at which a hooked element lets go
    public CoverKind Kind;
    public byte Surface;
    public bool Hooks;
}

/// Micro-detail: individual elements are never stored. Each 0.25 m world cell draws, from an integer hash of its
/// coordinates, the kind, the slot and the map seed, how many elements it holds and every parameter of each, scaled by
/// the cover density of the surface cell it lies in. The renderer and physics use this same generator.
public sealed partial class WorldQuery
{
    public const double MicroCell = 0.25;
    /// Standing grass leans up to this far from vertical, in turns (20°).
    const double GrassMaxLean = 20.0 / 360.0;
    /// Ordered 4 × 4 thresholds: over each 1 m block the fractional counts come out exact, so densities match the
    /// table even for sparse kinds. Each block turns the pattern by its own hash.
    static readonly byte[] Bayer4 = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

    readonly uint[] _elementKey = new uint[4], _blockKey = new uint[4];
    readonly double[] _reach = new double[4]; // per kind: the farthest an element reaches sideways from its base
    ulong _idSeed;

    void InitMicroDetail()
    {
        DetMath.SinCosTurns(GrassMaxLean, out double sinLean, out _);
        for (int k = 0; k < 4; k++)
        {
            _elementKey[k] = DetMath.Key(Seed, 0x800u + (uint)k);
            _blockKey[k] = DetMath.Key(Seed, 0x900u + (uint)k);
            foreach (SurfaceParams s in Surfaces)
            {
                CoverParams c = s.Cover[k];
                if (c != null && c.Density > 0)
                    _reach[k] = Math.Max(_reach[k], c.LengthMax * (k == (int)CoverKind.Grass ? sinLean : 1.0) + c.DiameterMax / 2);
            }
        }
        _idSeed = (ulong)(DetMath.Hash(Seed) & 0x7FFFF) << 45;
    }

    /// Stable element id: 19 bits of the seed hash, then the 0.25 m cell z and x (each + 65536, 17 bits), kind (2 bits)
    /// and slot (9 bits). Unique within a map up to 16 km across.
    ulong ElementId(int cx, int cz, int kind, int slot) =>
        _idSeed | (ulong)((uint)(cz + 65536) & 0x1FFFF) << 28 | (ulong)((uint)(cx + 65536) & 0x1FFFF) << 11
        | (ulong)(uint)kind << 9 | (uint)slot;

    /// World-query W-5: every element of the kinds in `kinds` whose segment, thickened by its radius, meets the sphere
    /// (`center`, `radius`), even when its base lies outside it. Results go into `results` in the canonical order
    /// (cell z, cell x, kind, slot). Returns the true count, which exceeds `results.Length` when the buffer overflowed.
    public int MicroDetailNear(Double3 center, double radius, KindMask kinds, Span<MicroElement> results)
    {
        double cxw = Limit(center.X, Half + Margin), czw = Limit(center.Z, Half + Margin);
        double reach = 0;
        for (int k = 0; k < 4; k++)
        {
            if (((int)kinds & 1 << k) != 0)
                reach = Math.Max(reach, _reach[k]);
        }
        double scan = radius + reach;
        int x0 = (int)Math.Floor((cxw - scan) / MicroCell), x1 = (int)Math.Floor((cxw + scan) / MicroCell);
        int z0 = (int)Math.Floor((czw - scan) / MicroCell), z1 = (int)Math.Floor((czw + scan) / MicroCell);
        int count = 0;
        for (int cz = z0; cz <= z1; cz++)
        {
            double dz = Math.Max(Math.Max(cz * MicroCell - czw, czw - (cz + 1) * MicroCell), 0);
            for (int cx = x0; cx <= x1; cx++)
            {
                double dx = Math.Max(Math.Max(cx * MicroCell - cxw, cxw - (cx + 1) * MicroCell), 0);
                double cellDistance2 = dx * dx + dz * dz;
                int own = OwnCell((cx + 0.5) * MicroCell, (cz + 0.5) * MicroCell);
                byte surface = _surface[own];
                for (int k = 0; k < 4; k++)
                {
                    if (((int)kinds & 1 << k) == 0)
                        continue;
                    CoverParams c = _byIndex[surface].Cover[k];
                    double limit = radius + _reach[k];
                    if (c == null || cellDistance2 > limit * limit)
                        continue;
                    int n = ElementCount(c, _cover[own * 4 + k], cx, cz, k);
                    for (int slot = 0; slot < n; slot++)
                    {
                        if (!Element(c, surface, cx, cz, k, slot, center, radius, out MicroElement e))
                            continue;
                        if (count < results.Length)
                            results[count] = e;
                        count++;
                    }
                }
            }
        }
        return count;
    }

    int ElementCount(CoverParams c, int channel, int cx, int cz, int kind)
    {
        double expected = c.Density * channel / 255.0 * (MicroCell * MicroCell);
        double whole = Math.Floor(expected);
        uint turn = DetMath.Hash(cx >> 2, cz >> 2, _blockKey[kind]);
        uint fine = DetMath.Draw(DetMath.Hash(cx, cz, _elementKey[kind]), 0xFFFFu);
        double threshold = (((Bayer4[(cz & 3) * 4 + (cx & 3)] + turn) & 15) + DetMath.Unit(fine)) / 16.0;
        return (int)whole + (threshold < expected - whole ? 1 : 0);
    }

    /// Draws element `slot` of cell (cx, cz) and reports whether it meets the query sphere.
    bool Element(CoverParams c, byte surface, int cx, int cz, int kind, int slot, Double3 center, double radius, out MicroElement e)
    {
        e = default;
        uint h = DetMath.Draw(DetMath.Hash(cx, cz, _elementKey[kind]), (uint)slot);
        double bx = (cx + DetMath.Unit(DetMath.Draw(h, 0))) * MicroCell;
        double bz = (cz + DetMath.Unit(DetMath.Draw(h, 1))) * MicroCell;
        double length = c.LengthMin + (c.LengthMax - c.LengthMin) * DetMath.Unit(DetMath.Draw(h, 2));
        double diameter = c.DiameterMin + (c.DiameterMax - c.DiameterMin) * DetMath.Unit(DetMath.Draw(h, 3));
        DetMath.SinCosTurns(DetMath.Unit(DetMath.Draw(h, 4)), out double sinAz, out double cosAz);
        bool standing = kind == (int)CoverKind.Grass;
        double sinLean = 0, cosLean = 1;
        if (standing)
            DetMath.SinCosTurns(GrassMaxLean * DetMath.Unit(DetMath.Draw(h, 5)), out sinLean, out cosLean);

        // Sideways reach first, so most elements are rejected before the ground is evaluated.
        double reach = radius + (standing ? length * sinLean : length) + diameter / 2;
        double ox = bx - center.X, oz = bz - center.Z;
        if (!(ox * ox + oz * oz <= reach * reach))
            return false;

        Sample(bx, bz, out GroundSample g, !standing);
        double dx, dy, dz;
        if (standing)
        {
            dx = sinLean * cosAz;
            dy = cosLean;
            dz = sinLean * sinAz;
        }
        else
        {
            // Lying: the drawn heading, laid in the ground's tangent plane.
            double nx = g.Normal.X, ny = g.Normal.Y, nz = g.Normal.Z;
            double along = cosAz * nx + sinAz * nz;
            dx = cosAz - along * nx;
            dy = -along * ny;
            dz = sinAz - along * nz;
            double norm = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            dx /= norm;
            dy /= norm;
            dz /= norm;
        }
        double by = standing ? g.GroundHeight : g.SupportTop;

        // Segment meets sphere: the closest point of the axis to the centre is within radius + element radius.
        double rx = center.X - bx, ry = center.Y - by, rz = center.Z - bz;
        double t = rx * dx + ry * dy + rz * dz;
        t = t < 0 ? 0 : t > length ? length : t;
        double qx = rx - t * dx, qy = ry - t * dy, qz = rz - t * dz;
        double touch = radius + diameter / 2;
        if (!(qx * qx + qy * qy + qz * qz <= touch * touch))
            return false;

        double meanDiameter = (c.DiameterMin + c.DiameterMax) / 2, meanLength = (c.LengthMin + c.LengthMax) / 2;
        double dr = diameter / meanDiameter, lr = meanLength / length;
        e.Id = ElementId(cx, cz, kind, slot);
        e.Base = new Double3(bx, by, bz);
        e.Direction = new Vector3((float)dx, (float)dy, (float)dz);
        e.Length = (float)length;
        e.Diameter = (float)diameter;
        e.TipStiffness = (float)(c.TipStiffness * (dr * dr) * (dr * dr) * (lr * lr * lr));
        e.HookRelease = (float)(c.HookReleaseMin + (c.HookReleaseMax - c.HookReleaseMin) * DetMath.Unit(DetMath.Draw(h, 6)));
        e.Hooks = DetMath.Unit(DetMath.Draw(h, 7)) < c.HookProbability;
        e.Kind = (CoverKind)kind;
        e.Surface = surface;
        return true;
    }
}
