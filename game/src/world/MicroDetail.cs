using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

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
/// the cover density of the surface cell it lies in. The renderer and physics use this same generator. An element's
/// parameters are 16-bit fields of two 64-bit mixes of its cell's hash and its slot: a 3.8 µm step in position, 1/65536
/// turn in azimuth, and finer than 1e-4 of every other range.
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
    double _sinMaxLean;

    /// Test only (the determinism check, WorldQueryGolden): draws every element one at a time, with no 4-wide pass and
    /// no per-cell ground cache (each base from GroundHeightAt), as the reference the fast paths must match bit for bit.
    /// Never changed while a query runs.
    internal bool ScalarReference;

    void InitMicroDetail()
    {
        DetMath.SinCosTurns(GrassMaxLean, out _sinMaxLean, out _);
        for (int k = 0; k < 4; k++)
        {
            _elementKey[k] = DetMath.Key(Seed, 0x800u + (uint)k);
            _blockKey[k] = DetMath.Key(Seed, 0x900u + (uint)k);
            foreach (SurfaceParams s in Surfaces)
            {
                CoverParams c = s.Cover[k];
                if (c != null && c.Density > 0)
                    _reach[k] = Math.Max(_reach[k], c.LengthMax * (k == (int)CoverKind.Grass ? _sinMaxLean : 1.0) + c.DiameterMax / 2);
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
        Drawn batch = default;
        CellGround ground = default; // refilled per cell, never cleared: only the fields a valid cell uses are read
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
                    uint cell = DetMath.Hash(cx, cz, _elementKey[k]);
                    int n = ElementCount(c, _cover[own * 4 + k], cx, cz, k, cell);
                    // Standing elements share their cell's ground once there are a few of them.
                    ground.Valid = k == (int)CoverKind.Grass && n >= 4 && !ScalarReference && CellGroundOf(cx, cz, ref ground);
                    for (int first = 0; first < n; first += Batch)
                        count = Elements(c, surface, cx, cz, k, cell, first, Math.Min(n, first + Batch), in ground, in center, radius,
                            ref batch, results, count);
                }
            }
        }
        return count;
    }

    /// `cell` is the cell's element hash, DetMath.Hash(cx, cz, _elementKey[kind]).
    int ElementCount(CoverParams c, int channel, int cx, int cz, int kind, uint cell)
    {
        double expected = c.Density * channel / 255.0 * (MicroCell * MicroCell);
        double whole = Math.Floor(expected);
        uint turn = DetMath.Hash(cx >> 2, cz >> 2, _blockKey[kind]);
        uint fine = DetMath.Draw(cell, 0xFFFFu);
        double threshold = (((Bayer4[(cz & 3) * 4 + (cx & 3)] + turn) & 15) + DetMath.Unit(fine)) / 16.0;
        return (int)whole + (threshold < expected - whole ? 1 : 0);
    }

    /// [0, 1) from the low 16 bits.
    static double Unit16(uint bits) => (bits & 0xFFFF) * (1.0 / 65536);

    /// Elements drawn by one call of Elements, between its passes. A multiple of 4, the vector width of the passes.
    const int Batch = 32;

    [InlineArray(Batch)]
    struct Lanes
    {
        double _element;
    }

    [InlineArray(Batch)]
    struct Words
    {
        uint _element;
    }

    /// The elements of one batch between the passes of Elements, one lane each (a structure of arrays, so that the passes
    /// can work on 4 elements at a time).
    struct Drawn
    {
        public Words Slot, Shape, Tail, Met; // Shape and Tail: the second mix, lean and hook release, then azimuth and hooks
        public Lanes X, Y, Z, Length, Diameter, SinLean, Dx, Dy, Dz;
    }

    /// Draws elements `first` to `end` − 1 of cell (cx, cz), whose element hash is `cell`, and appends those that meet the
    /// query sphere to `results` at `count` in slot order; returns the new count. Short passes over `batch` rather than
    /// one long chain per element, so the processor works on several elements at once: the base and reach (an element out
    /// of reach costs two hashes), then the heading and the ground, then the sphere test, then the results. The heading and
    /// ground of standing elements and the sphere test run on 4 elements per vector, with the same IEEE operations in the
    /// same order as one at a time, so the bits are the same (ScalarReference runs them one at a time, to check that).
    int Elements(CoverParams c, byte surface, int cx, int cz, int kind, uint cell, int first, int end, in CellGround ground,
        in Double3 center, double radius, ref Drawn batch, Span<MicroElement> results, int count)
    {
        // Everything that does not change per element is read once, so the loops keep it in registers.
        bool standing = kind == (int)CoverKind.Grass;
        double far2 = (radius + _reach[kind]) * (radius + _reach[kind]), x0 = cx, z0 = cz, sx = center.X, sy = center.Y, sz = center.Z;
        double lengthMin = c.LengthMin, lengthSpan = c.LengthMax - c.LengthMin;
        double diameterMin = c.DiameterMin, diameterSpan = c.DiameterMax - c.DiameterMin, sinMaxLean = _sinMaxLean;
        ulong key = (ulong)cell << 32;
        int m = 0;
        for (int slot = first; slot < end; slot++)
        {
            // Every slot is drawn and written; only those within reach advance m (no branch to mispredict).
            ulong first64 = DetMath.Mix64(key | (uint)slot), second64 = DetMath.Mix64(first64 + 0x9E3779B97F4A7C15UL);
            uint place = (uint)first64, size = (uint)(first64 >> 32), shape = (uint)second64;
            double bx = (x0 + Unit16(place)) * MicroCell, bz = (z0 + Unit16(place >> 16)) * MicroCell;
            double ox = bx - sx, oz = bz - sz, horizontal = ox * ox + oz * oz;
            double length = lengthMin + lengthSpan * Unit16(size);
            double diameter = diameterMin + diameterSpan * Unit16(size >> 16);
            double sinLean = standing ? sinMaxLean * Unit16(shape) : 0; // standing grass: the sine of its lean is uniform
            double reach = radius + (standing ? length * sinLean : length) + diameter / 2;
            batch.Slot[m] = (uint)slot;
            batch.Shape[m] = shape;
            batch.Tail[m] = (uint)(second64 >> 32);
            batch.X[m] = bx;
            batch.Z[m] = bz;
            batch.Length[m] = length;
            batch.Diameter[m] = diameter;
            batch.SinLean[m] = sinLean;
            m += (horizontal <= far2 ? 1 : 0) & (horizontal <= reach * reach ? 1 : 0);
        }
        if (m == 0)
            return count;
        // Lanes past m up to the next multiple of 4 repeat the last element, so every lane of a vector is a real base.
        for (int pad = m; (pad & 3) != 0; pad++)
        {
            batch.X[pad] = batch.X[m - 1];
            batch.Z[pad] = batch.Z[m - 1];
        }
        int i = 0;
        if (standing && ground.Valid && ground.Ridges == null)
        {
            for (; i < m; i += 4)
                Standing4(ref batch, i, in ground);
        }
        for (; i < m; i++)
        {
            Azimuth(batch.Tail[i], out double cosAz, out double sinAz);
            double bx = batch.X[i], bz = batch.Z[i];
            if (standing)
            {
                double sinLean = batch.SinLean[i];
                batch.Y[i] = ground.Valid ? CellHeight(in ground, bx, bz) : GroundHeightAt(bx, bz);
                batch.Dx[i] = sinLean * cosAz;
                batch.Dy[i] = Math.Sqrt(1 - sinLean * sinLean);
                batch.Dz[i] = sinLean * sinAz;
            }
            else
                batch.Y[i] = Lying(bx, bz, cosAz, sinAz, out batch.Dx[i], out batch.Dy[i], out batch.Dz[i]);
        }
        // Segment meets sphere: the closest point of the axis to the centre is within radius + element radius. The
        // elements that meet it are listed in Met.
        int met = 0;
        if (ScalarReference)
        {
            for (i = 0; i < m; i++)
            {
                double rx = sx - batch.X[i], ry = sy - batch.Y[i], rz = sz - batch.Z[i], dx = batch.Dx[i], dy = batch.Dy[i], dz = batch.Dz[i];
                double t = Math.Max(Math.Min(rx * dx + ry * dy + rz * dz, batch.Length[i]), 0);
                double qx = rx - t * dx, qy = ry - t * dy, qz = rz - t * dz, touch = radius + batch.Diameter[i] / 2.0;
                batch.Met[met] = (uint)i;
                met += qx * qx + qy * qy + qz * qz <= touch * touch ? 1 : 0;
            }
        }
        else
        {
            var cxv = Vector256.Create(sx);
            var cyv = Vector256.Create(sy);
            var czv = Vector256.Create(sz);
            var radiusv = Vector256.Create(radius);
            var two = Vector256.Create(2.0);
            for (i = 0; i < m; i += 4)
            {
                Vector256<double> dx = Load(ref batch.Dx, i), dy = Load(ref batch.Dy, i), dz = Load(ref batch.Dz, i);
                Vector256<double> rx = cxv - Load(ref batch.X, i), ry = cyv - Load(ref batch.Y, i), rz = czv - Load(ref batch.Z, i);
                Vector256<double> t = rx * dx + ry * dy + rz * dz;
                t = Vector256.Max(Vector256.Min(t, Load(ref batch.Length, i)), Vector256<double>.Zero); // t is finite
                Vector256<double> qx = rx - t * dx, qy = ry - t * dy, qz = rz - t * dz;
                Vector256<double> touch = radiusv + Load(ref batch.Diameter, i) / two;
                uint meets = Vector256.LessThanOrEqual(qx * qx + qy * qy + qz * qz, touch * touch).ExtractMostSignificantBits();
                for (int lane = 0, lanes = Math.Min(4, m - i); lane < lanes; lane++)
                {
                    batch.Met[met] = (uint)(i + lane);
                    met += (int)(meets >> lane & 1);
                }
            }
        }
        double perMeanDiameter = 2 / (c.DiameterMin + c.DiameterMax), meanLength = (c.LengthMin + c.LengthMax) / 2;
        double stiffness = c.TipStiffness, releaseMin = c.HookReleaseMin, releaseSpan = c.HookReleaseMax - c.HookReleaseMin;
        double hookProbability = c.HookProbability;
        ulong idBase = ElementId(cx, cz, kind, 0);
        for (int j = 0; j < met; j++, count++)
        {
            if (count >= results.Length)
                continue;
            int e = (int)batch.Met[j];
            double diameter = batch.Diameter[e], length = batch.Length[e];
            double dr = diameter * perMeanDiameter, lr = meanLength / length;
            results[count] = new MicroElement
            {
                Id = idBase | batch.Slot[e],
                Base = new Double3(batch.X[e], batch.Y[e], batch.Z[e]),
                Direction = new Vector3((float)batch.Dx[e], (float)batch.Dy[e], (float)batch.Dz[e]),
                Length = (float)length,
                Diameter = (float)diameter,
                TipStiffness = (float)(stiffness * (dr * dr) * (dr * dr) * (lr * lr * lr)),
                HookRelease = (float)(releaseMin + releaseSpan * Unit16(batch.Shape[e] >> 16)),
                Hooks = Unit16(batch.Tail[e] >> 16) < hookProbability,
                Kind = (CoverKind)kind,
                Surface = surface,
            };
        }
        return count;
    }

    static Vector256<double> Load(ref Lanes lanes, int at) => Vector256.LoadUnsafe(ref lanes[0], (nuint)at);

    /// Elements `at` to `at` + 3 of `batch`, standing, in a cell whose ground is `g` (valid, without ridges): the heading
    /// (Azimuth), the base's GroundHeight (CellHeight) and the direction, as the one-at-a-time path computes them.
    void Standing4(ref Drawn batch, int at, in CellGround g)
    {
        // Azimuth
        uint b0 = batch.Tail[at], b1 = batch.Tail[at + 1], b2 = batch.Tail[at + 2], b3 = batch.Tail[at + 3];
        Vector256<double> a = Vector256.Create((double)(b0 & 0xF), b1 & 0xF, b2 & 0xF, b3 & 0xF) * Vector256.Create(2 * Math.PI / 65536);
        Vector256<double> a2 = a * a, one = Vector256.Create(1.0);
        Vector256<double> s = a * (one - a2 * Vector256.Create(1.0 / 6));
        Vector256<double> c = one - a2 * (Vector256.Create(1.0 / 2) - a2 * Vector256.Create(1.0 / 24));
        double[] turns = Turns4096;
        int k0 = (int)(b0 >> 4 & 0xFFF) * 2, k1 = (int)(b1 >> 4 & 0xFFF) * 2, k2 = (int)(b2 >> 4 & 0xFFF) * 2, k3 = (int)(b3 >> 4 & 0xFFF) * 2;
        Vector256<double> ck = Vector256.Create(turns[k0], turns[k1], turns[k2], turns[k3]);
        Vector256<double> sk = Vector256.Create(turns[k0 + 1], turns[k1 + 1], turns[k2 + 1], turns[k3 + 1]);
        Vector256<double> cos = ck * c - sk * s, sin = sk * c + ck * s;

        // CellHeight: the terrain triangle plus the relief
        Vector256<double> x = Load(ref batch.X, at), z = Load(ref batch.Z, at), half = Vector256.Create(Half), perStep = Vector256.Create(_perHeightStep);
        Vector256<double> u = (x + half) * perStep - Vector256.Create((double)g.Ci), v = (z + half) * perStep - Vector256.Create((double)g.Cj);
        Vector256<double> h00 = Vector256.Create(g.H00), h10 = Vector256.Create(g.H10), h01 = Vector256.Create(g.H01), h11 = Vector256.Create(g.H11);
        Vector256<double> terrain = Vector256.ConditionalSelect(Vector256.LessThanOrEqual(u + v, one),
            h00 + u * (h10 - h00) + v * (h01 - h00), h11 + (one - u) * (h01 - h11) + (one - v) * (h10 - h11));
        Vector256<double> scale = Vector256.Create(g.Scale), px = x * scale, pz = z * scale, fx = Vector256.Floor(px), fz = Vector256.Floor(pz);
        int n0 = NodeIndex(fx, fz, 0, in g), n1 = NodeIndex(fx, fz, 1, in g), n2 = NodeIndex(fx, fz, 2, in g), n3 = NodeIndex(fx, fz, 3, in g);
        Vector256<double> v00 = Vector256.Create(g.Nodes[n0], g.Nodes[n1], g.Nodes[n2], g.Nodes[n3]);
        Vector256<double> v10 = Vector256.Create(g.Nodes[n0 + 1], g.Nodes[n1 + 1], g.Nodes[n2 + 1], g.Nodes[n3 + 1]);
        Vector256<double> v01 = Vector256.Create(g.Nodes[n0 + 4], g.Nodes[n1 + 4], g.Nodes[n2 + 4], g.Nodes[n3 + 4]);
        Vector256<double> v11 = Vector256.Create(g.Nodes[n0 + 5], g.Nodes[n1 + 5], g.Nodes[n2 + 5], g.Nodes[n3 + 5]);
        Vector256<double> fa = Fade5(px - fx), fb = Fade5(pz - fz);
        Vector256<double> noise = v00 + fa * (v10 - v00) + fb * (v01 - v00) + fa * fb * (v00 - v10 - v01 + v11);
        (terrain + Vector256.Create(g.ReliefScale) * noise).StoreUnsafe(ref batch.Y[0], (nuint)at);

        // Direction
        Vector256<double> lean = Load(ref batch.SinLean, at);
        (lean * cos).StoreUnsafe(ref batch.Dx[0], (nuint)at);
        Vector256.Sqrt(one - lean * lean).StoreUnsafe(ref batch.Dy[0], (nuint)at);
        (lean * sin).StoreUnsafe(ref batch.Dz[0], (nuint)at);
    }

    /// The index in CellGround.Nodes of the lattice node below lane `lane` of (fx, fz), the floored node coordinates.
    static int NodeIndex(Vector256<double> fx, Vector256<double> fz, int lane, in CellGround g) =>
        (DetMath.ToInt(fz.GetElement(lane)) - g.NodeZ) * 4 + DetMath.ToInt(fx.GetElement(lane)) - g.NodeX;

    /// DetMath.Fade5 on 4 lanes.
    static Vector256<double> Fade5(Vector256<double> t) =>
        t * t * t * (t * (t * Vector256.Create(6.0) - Vector256.Create(15.0)) + Vector256.Create(10.0));

    /// A lying element at (bx, bz): its base on the mat (SupportTop), and its direction, the drawn heading laid in the
    /// ground's tangent plane.
    double Lying(double bx, double bz, double cosAz, double sinAz, out double dx, out double dy, out double dz)
    {
        Sample(bx, bz, out GroundSample g, true);
        double nx = g.Normal.X, ny = g.Normal.Y, nz = g.Normal.Z;
        double along = cosAz * nx + sinAz * nz;
        dx = cosAz - along * nx;
        dy = -along * ny;
        dz = sinAz - along * nz;
        double norm = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        dx /= norm;
        dy /= norm;
        dz /= norm;
        return g.SupportTop;
    }

    /// cos and sin of k/4096 turn at [2k] and [2k + 1], k = 0–4095, for Azimuth.
    static readonly double[] Turns4096 = MakeTurns4096();

    static double[] MakeTurns4096()
    {
        var table = new double[8192];
        for (int k = 0; k < 4096; k++)
            DetMath.SinCosTurns(k / 4096.0, out table[2 * k + 1], out table[2 * k]);
        return table;
    }

    /// A uniform horizontal unit vector (cos, sin of the azimuth) from the low 16 bits of `bits`, a fraction of a turn: the
    /// top 12 from Turns4096, the rest (under 1/4096 turn, 1.6e-3 rad) by short series (error below 1e-16), then the angle
    /// sum.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void Azimuth(uint bits, out double cos, out double sin)
    {
        int k = (int)(bits >> 4 & 0xFFF) * 2;
        double a = (bits & 0xF) * (2 * Math.PI / 65536), a2 = a * a;
        double s = a * (1 - a2 * (1.0 / 6)), c = 1 - a2 * (1.0 / 2 - a2 * (1.0 / 24));
        double ck = Turns4096[k], sk = Turns4096[k + 1];
        cos = ck * c - sk * s;
        sin = sk * c + ck * s;
    }
    /// What the standing elements of one 0.25 m micro cell share for their GroundHeight (see CellGroundOf).
    struct CellGround
    {
        public bool Valid;
        public SurfaceParams Ridges;        // the surface, when it has ridges
        public double Scale, ReliefScale;   // its node scale and relief scale
        public int Ci, Cj;                  // the height cell
        public double H00, H10, H01, H11;   // its corners
        public int NodeX, NodeZ;            // the relief node at Nodes[0]
        public Nodes16 Nodes;               // relief nodes (NodeX + i, NodeZ + j) at [j * 4 + i]
    }

    [InlineArray(16)]
    struct Nodes16
    {
        double _element;
    }

    /// Fills `g` with the shared ground of micro cell (cx, cz) and returns true when every base the cell can draw lies inside
    /// the map, in one height cell and in one blend quad whose 4 corners are the same surface, in a 1 m cell that no
    /// pitfall reaches, and its relief nodes span at most 4 × 4. Then CellHeight gives exactly GroundHeightAt's bits there.
    bool CellGroundOf(int cx, int cz, ref CellGround g)
    {
        const double Last = 65535.0 / 65536; // the largest Unit16
        double x0 = cx * MicroCell, z0 = cz * MicroCell, x1 = (cx + Last) * MicroCell, z1 = (cz + Last) * MicroCell;
        if (!(x0 >= -Half && x1 <= Half && z0 >= -Half && z1 <= Half))
            return false;
        // Each index below is a non-decreasing function of the coordinate, so equal at both ends means equal between.
        int ci = Math.Min(DetMath.ToInt((x0 + Half) * _perHeightStep), Samples - 2), cj = Math.Min(DetMath.ToInt((z0 + Half) * _perHeightStep), Samples - 2);
        if (Math.Min(DetMath.ToInt((x1 + Half) * _perHeightStep), Samples - 2) != ci
            || Math.Min(DetMath.ToInt((z1 + Half) * _perHeightStep), Samples - 2) != cj)
            return false;
        if (Math.Floor((x0 + Half) * _perCell - 0.5) != Math.Floor((x1 + Half) * _perCell - 0.5)
            || Math.Floor((z0 + Half) * _perCell - 0.5) != Math.Floor((z1 + Half) * _perCell - 0.5))
            return false;
        var b = new Blend(this, x0, z0);
        if (!b.Uniform)
            return false;
        if (_pitSearch > 0 && (Math.Floor(x0) != Math.Floor(x1) || Math.Floor(z0) != Math.Floor(z1) || PitMayReach(x0, z0)))
            return false;
        double scale = _nodeScale[b.S0];
        int nx = DetMath.ToInt(Math.Floor(x0 * scale)), nz = DetMath.ToInt(Math.Floor(z0 * scale));
        int wide = DetMath.ToInt(Math.Floor(x1 * scale)) + 1 - nx, deep = DetMath.ToInt(Math.Floor(z1 * scale)) + 1 - nz;
        if (wide > 3 || deep > 3)
            return false;
        uint key = _reliefKey[b.S0];
        for (int j = 0; j <= deep; j++)
        {
            for (int i = 0; i <= wide; i++)
                g.Nodes[j * 4 + i] = Node(NodeInput(nx + i, nz + j, key));
        }
        int k0 = cj * Samples + ci;
        g.H00 = _heights[k0];
        g.H10 = _heights[k0 + 1];
        g.H01 = _heights[k0 + Samples];
        g.H11 = _heights[k0 + Samples + 1];
        SurfaceParams s = _byIndex[b.S0];
        g.Ridges = s.RidgeAmplitude > 0 ? s : null;
        g.Scale = scale;
        g.ReliefScale = _reliefScale[b.S0];
        g.Ci = ci;
        g.Cj = cj;
        g.NodeX = nx;
        g.NodeZ = nz;
        return true;
    }

    /// GroundHeightAt(x, z) for a point of a valid CellGround's cell, from its shared values with the same arithmetic:
    /// the terrain triangle plus the surface's relief (the pitfall depth there is 0).
    double CellHeight(in CellGround g, double x, double z)
    {
        double terrain = Triangle((x + Half) * _perHeightStep - g.Ci, (z + Half) * _perHeightStep - g.Cj, g.H00, g.H10, g.H01, g.H11);
        double px = x * g.Scale, pz = z * g.Scale, fx = Math.Floor(px), fz = Math.Floor(pz);
        int at = (DetMath.ToInt(fz) - g.NodeZ) * 4 + DetMath.ToInt(fx) - g.NodeX;
        double relief = g.ReliefScale * Interpolate(DetMath.Fade5(px - fx), DetMath.Fade5(pz - fz), g.Nodes[at], g.Nodes[at + 1],
            g.Nodes[at + 4], g.Nodes[at + 5]);
        if (g.Ridges != null)
            relief += Ridge(g.Ridges, x, z, out _);
        return terrain + relief;
    }
}
