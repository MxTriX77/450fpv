using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;

/// Cover kinds, in cover.png channel order (R, G, B, A).
public enum CoverKind : byte { Grass, Straw, Twigs, Litter }

[Flags]
public enum KindMask : byte { None = 0, Grass = 1, Straw = 2, Twigs = 4, Litter = 8, All = 15 }

public enum GroundFeature : byte { None, Pitfall }

[Flags]
public enum GroundFlags : byte { None = 0, OutsideMap = 1 }

[InlineArray(4)]
public struct Bytes4
{
    byte _element;
}

/// A horizontal world position, m.
public readonly record struct XZ(double X, double Z);

public struct Double3
{
    public double X, Y, Z;

    public Double3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Double3 operator +(Double3 a, Double3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Double3 operator -(Double3 a, Double3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Double3 operator -(Double3 a) => new(-a.X, -a.Y, -a.Z);
    public static Double3 operator *(Double3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static double Dot(Double3 a, Double3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Double3 Cross(Double3 a, Double3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    public readonly double Length() => Math.Sqrt(X * X + Y * Y + Z * Z);
    public readonly Vector3 ToVector3() => new((float)X, (float)Y, (float)Z);

    public override readonly string ToString() => $"({X}, {Y}, {Z})";
}

/// The ground under one (x, z) point, world frame, Y up, metres (world-query W-1).
public struct GroundSample
{
    public double TerrainHeight; // the rendered triangle (HeightmapTerrain and its Jolt collision)
    public double GroundHeight;  // terrain + micro-relief + ridges − pitfall: the soil surface
    public double SupportTop;    // GroundHeight + ΣMatDepth
    public Vector3 Normal;       // unit normal of GroundHeight
    public Vector4 MatDepth;     // uncompressed mat per kind (X grass, Y straw, Z twigs, W litter), m
    public Vector4 CoverDensity; // elements per m² per kind in this point's own cell (table × cover channel)
    public Vector4 BlendWeight;  // bilinear weights of BlendSurface, summing to 1
    public Bytes4 BlendSurface;  // surfaces of the 4 nearest cell centres: (−x, −z), (+x, −z), (−x, +z), (+x, +z)
    public byte Surface;         // this point's own cell
    public GroundFeature Feature;
    public GroundFlags Flags;
    public uint FeatureId;       // pitfall: its 1 m generator cell, (x + 32768) in the low 16 bits, (z + 32768) in the high
    public float FeatureDepth;   // pitfall depth at this point, m
}

/// The runtime questions physics asks about the ground (world-query spec). Pure C#: no Godot, no scene tree, no
/// allocation per query and no shared scratch state, so it is reentrant and runs in a headless replay (D-010). Every
/// result is a deterministic function of position, the map seed and the tables (W-13, see DetMath).
///
/// Ground = the renderer's triangle + micro-relief + ridges − pitfalls. Relief and mat blend bilinearly over the 4
/// nearest surface-cell centres; pitfalls come from 1 m generator cells and are never clipped at a cell border.
public sealed partial class WorldQuery
{
    const double Margin = 64;        // beyond this far outside the map, relief and pitfalls stop changing
    const double PitCell = 1.0;      // one candidate pitfall per 1 m cell, so density ≤ 1 per m²
    const double PitWall = 0.25;     // the outer fraction of the radius smoothed into a wall
    /// RMS of value noise with independent uniform [−1, 1] nodes and quintic fades: (181/231)/√3.
    static readonly double NoiseRms = 181.0 / 231.0 / Math.Sqrt(3.0);
    /// RMS of the ridge profile 1 − 2·smoothstep(2q): √(17/35).
    public static readonly double RidgeProfileRms = Math.Sqrt(17.0 / 35.0);

    public readonly double SizeM, Half;
    public readonly uint Seed;
    public readonly int Samples;
    public readonly double HeightResolution;
    public readonly int Cells;
    public readonly double CellResolution;
    readonly double _perHeightStep, _perCell; // 1 / HeightResolution, 1 / CellResolution
    readonly float[] _heights;
    readonly byte[] _surface;
    readonly byte[] _cover;
    readonly SurfaceParams[] _byIndex = new SurfaceParams[256];
    readonly uint[] _reliefKey = new uint[256];
    readonly uint[] _matKey = new uint[256 * 4];
    readonly double[] _reliefScale = new double[256], _nodeScale = new double[256];
    readonly double[] _ridgePhase = new double[256], _ridgeNx = new double[256], _ridgeNz = new double[256];
    readonly uint _pitKey;
    readonly double _pitSearch, _pitMaxDensity;
    ulong[] _pitReach; // see InitPitReach
    int _pitLow, _pitSide;
    readonly byte[] _matKinds = new byte[256]; // per surface: a bit per cover kind that has a mat
    readonly double _minHeight = double.PositiveInfinity, _maxHeight = double.NegativeInfinity; // of the height samples

    public IReadOnlyList<SurfaceParams> Surfaces { get; }

    /// Loads a map package (game/maps/README.md) with its objects, the shared surface table and the shared catalog.
    public static WorldQuery Load(string packageDir, string surfacesPath, string catalogPath)
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageDir, "map.json")));
        JsonElement root = manifest.RootElement, height = root.GetProperty("height");
        int samples = height.GetProperty("samples_per_side").GetInt32();
        float offset = height.GetProperty("offset_m").GetSingle(), scale = height.GetProperty("scale_m").GetSingle();
        byte[] r16 = File.ReadAllBytes(Path.Combine(packageDir, "height.r16"));
        if (r16.Length != samples * samples * 2)
            throw new InvalidDataException($"{packageDir}: height.r16 is {r16.Length} bytes, expected {samples * samples * 2}");
        var heights = new float[samples * samples];
        for (int i = 0; i < heights.Length; i++)
            heights[i] = offset + (r16[2 * i] | r16[2 * i + 1] << 8) * scale; // the same float arithmetic as HeightmapTerrain
        byte[] surface = MapPng.Read(Path.Combine(packageDir, "surface.png"), 1, out int cells, out _);
        byte[] cover = MapPng.Read(Path.Combine(packageDir, "cover.png"), 4, out int coverCells, out _);
        if (coverCells != cells)
            throw new InvalidDataException($"{packageDir}: cover.png is {coverCells} wide, surface.png {cells}");
        var world = new WorldQuery(SurfaceParams.ParseTable(File.ReadAllText(surfacesPath)), root.GetProperty("size_m").GetDouble(),
            root.GetProperty("seed").GetUInt32(), samples, heights, cells, surface, cover, Catalog.Parse(File.ReadAllText(catalogPath)));
        world.LoadObjects(Path.Combine(packageDir, "objects.json"));
        return world;
    }

    /// `heights` are samples² world Y values, `surface` cells² surface indices and `cover` cells² RGBA bytes, all
    /// row-major from the north-west corner. Objects need a `catalog`.
    public WorldQuery(SurfaceParams[] table, double sizeM, uint seed, int samples, float[] heights, int cells, byte[] surface,
        byte[] cover, Catalog catalog = null)
    {
        SizeM = sizeM;
        Half = sizeM / 2;
        Seed = seed;
        Samples = samples;
        HeightResolution = sizeM / (samples - 1);
        Cells = cells;
        CellResolution = sizeM / cells;
        _perHeightStep = 1.0 / HeightResolution;
        _perCell = 1.0 / CellResolution;
        _heights = heights;
        _surface = surface;
        _cover = cover;
        Surfaces = table;
        foreach (SurfaceParams s in table)
        {
            _byIndex[s.Index] = s;
            _reliefKey[s.Index] = DetMath.Key(seed, 0x100u | s.Index);
            _reliefScale[s.Index] = s.ReliefAmplitude / NoiseRms;
            _nodeScale[s.Index] = 2.0 / s.ReliefWavelength; // nodes half a wavelength apart
            for (int k = 0; k < 4; k++)
            {
                _matKey[s.Index * 4 + k] = DetMath.Key(seed, (uint)(0x200 + k * 0x100) | s.Index);
                if (s.Cover[k] != null && s.Cover[k].HasMat)
                    _matKinds[s.Index] |= (byte)(1 << k);
            }
            DetMath.SinCosTurns(s.RidgeAzimuthDeg / 360.0, out double sin, out double cos);
            _ridgeNx[s.Index] = sin; // across the crests; the crests run along (cos a, −sin a)
            _ridgeNz[s.Index] = cos;
            _ridgePhase[s.Index] = DetMath.Unit(DetMath.Hash(DetMath.Key(seed, 0x600u | s.Index)));
            if (s.PitDensity > 0)
            {
                _pitSearch = Math.Max(_pitSearch, s.PitRadiusMax);
                _pitMaxDensity = Math.Max(_pitMaxDensity, s.PitDensity);
            }
        }
        _pitKey = DetMath.Key(seed, 0x700u);
        for (int i = 0; i < surface.Length; i++)
        {
            if (_byIndex[surface[i]] == null)
                throw new InvalidDataException($"surface index {surface[i]} at cell {i % cells}, {i / cells} is not in the table");
        }
        foreach (float h in heights)
        {
            _minHeight = Math.Min(_minHeight, h);
            _maxHeight = Math.Max(_maxHeight, h);
        }
        InitPitReach();
        Catalog = catalog;
        InitMicroDetail();
        InitObjects();
    }

    public SurfaceParams Surface(byte index) => _byIndex[index];

    /// World-query W-1: one GroundSample per (x, z) point. Points outside the map clamp to the edge and are flagged.
    public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results)
    {
        for (int i = 0; i < points.Length; i++)
            Sample(points[i].X, points[i].Z, out results[i], true);
    }

    /// NaN goes to the low bound, so no input can index outside the layers or loop without end.
    static double Limit(double v, double bound) => v > bound ? bound : v >= -bound ? v : -bound;

    int ClampCell(int i) => i < 0 ? 0 : i >= Cells ? Cells - 1 : i;

    /// The cell that contains (x, z); the far edge belongs to the last cell.
    int OwnCell(double x, double z) =>
        ClampCell((int)Math.Floor((x + Half) * _perCell)) + ClampCell((int)Math.Floor((z + Half) * _perCell)) * Cells;

    void Sample(double x, double z, out GroundSample g, bool withMat)
    {
        g = default;
        if (!(Math.Abs(x) <= Half && Math.Abs(z) <= Half))
            g.Flags = GroundFlags.OutsideMap;
        x = Limit(x, Half + Margin);
        z = Limit(z, Half + Margin);
        double terrain = Terrain(x, z, out double gx, out double gz);

        var b = new Blend(this, x, z);
        var lattice = new Lattice(x, z, _nodeScale[b.S0]); // shared by the relief and the mat of the first corner's surface
        g.BlendSurface[0] = b.S0;
        g.BlendSurface[1] = b.S1;
        g.BlendSurface[2] = b.S2;
        g.BlendSurface[3] = b.S3;
        g.BlendWeight = new Vector4((float)b.W0, (float)b.W1, (float)b.W2, (float)b.W3);
        double relief, rdx, rdz;
        if (b.Uniform)
            relief = Relief(b.S0, in lattice, x, z, out rdx, out rdz); // the weights sum to 1 and their slopes cancel
        else
            relief = BlendedRelief(in b, in lattice, x, z, out rdx, out rdz);
        gx += rdx;
        gz += rdz;

        double pit = Pitfall(x, z, out double pdx, out double pdz, out uint pitId);
        if (pitId != 0)
        {
            g.Feature = GroundFeature.Pitfall;
            g.FeatureId = pitId;
            g.FeatureDepth = (float)pit;
            gx -= pdx;
            gz -= pdz;
        }

        g.TerrainHeight = terrain;
        g.GroundHeight = terrain + relief - pit;
        double inverse = 1.0 / Math.Sqrt(gx * gx + 1.0 + gz * gz);
        g.Normal = new Vector3((float)(-gx * inverse), (float)inverse, (float)(-gz * inverse));

        int own = OwnCell(x, z);
        g.Surface = _surface[own];
        if (!withMat)
            return;
        SurfaceParams ownSurface = _byIndex[g.Surface];
        g.CoverDensity = new Vector4(Density(ownSurface, own, 0), Density(ownSurface, own, 1), Density(ownSurface, own, 2),
            Density(ownSurface, own, 3));
        // A kind without a mat on any of the 4 corners has depth 0 exactly, so it is not evaluated.
        int mats = _matKinds[b.S0] | _matKinds[b.S1] | _matKinds[b.S2] | _matKinds[b.S3];
        double m0 = (mats & 1) != 0 ? Mat(in b, in lattice, 0, x, z) : 0, m1 = (mats & 2) != 0 ? Mat(in b, in lattice, 1, x, z) : 0;
        double m2 = (mats & 4) != 0 ? Mat(in b, in lattice, 2, x, z) : 0, m3 = (mats & 8) != 0 ? Mat(in b, in lattice, 3, x, z) : 0;
        g.MatDepth = new Vector4((float)m0, (float)m1, (float)m2, (float)m3);
        g.SupportTop = g.GroundHeight + (m0 + m1 + m2 + m3);
    }

    /// GroundHeight alone, the same bits as Sample's, for the micro-detail generator: terrain + relief − pitfall.
    double GroundHeightAt(double x, double z)
    {
        x = Limit(x, Half + Margin);
        z = Limit(z, Half + Margin);
        double terrain = Terrain(x, z, out _, out _);
        var b = new Blend(this, x, z);
        var lattice = new Lattice(x, z, _nodeScale[b.S0]);
        double relief = b.Uniform ? Relief(b.S0, in lattice, x, z, out _, out _) : BlendedRelief(in b, in lattice, x, z, out _, out _);
        return terrain + relief - Pitfall(x, z, out _, out _, out _);
    }

    /// The rendered terrain at (x, z), within Half + Margin, and its gradient. HeightmapTerrain splits each cell (a, b /
    /// c, d) into triangles a-b-c (u + v ≤ 1) and b-d-c. Outside the map the edge height continues flat.
    double Terrain(double x, double z, out double gx, out double gz)
    {
        double tx = Limit(x, Half), tz = Limit(z, Half);
        double fx = (tx + Half) * _perHeightStep, fz = (tz + Half) * _perHeightStep;
        int ci = Math.Min((int)fx, Samples - 2), cj = Math.Min((int)fz, Samples - 2);
        double u = fx - ci, v = fz - cj;
        int k0 = cj * Samples + ci;
        double h00 = _heights[k0], h10 = _heights[k0 + 1], h01 = _heights[k0 + Samples], h11 = _heights[k0 + Samples + 1];
        bool lower = u + v <= 1.0;
        gx = Select(lower, h10 - h00, h11 - h01) * _perHeightStep;
        gz = Select(lower, h01 - h00, h11 - h10) * _perHeightStep;
        if (tx != x)
            gx = 0;
        if (tz != z)
            gz = 0;
        return Triangle(u, v, h00, h10, h01, h11);
    }

    /// The height at (u, v) in a height cell with corners a = h00, b = h10, c = h01, d = h11: triangle a-b-c or b-d-c.
    static double Triangle(double u, double v, double h00, double h10, double h01, double h11) =>
        Select(u + v <= 1.0, h00 + u * (h10 - h00) + v * (h01 - h00), h11 + (1.0 - u) * (h01 - h11) + (1.0 - v) * (h10 - h11));

    /// `c ? a : b` with both evaluated, picked by their bits: the processor gets a select, never a branch to mispredict
    /// (the height triangle and the ridge side are a coin toss from point to point).
    static double Select(bool c, double a, double b) =>
        BitConverter.Int64BitsToDouble(c ? BitConverter.DoubleToInt64Bits(a) : BitConverter.DoubleToInt64Bits(b));

    /// The 4 nearest cell centres of a point, (−x, −z), (+x, −z), (−x, +z), (+x, +z): cells, surfaces and bilinear weights.
    readonly struct Blend
    {
        public readonly int C0, C1, C2, C3;
        public readonly byte S0, S1, S2, S3;
        public readonly double W0, W1, W2, W3, Wx, Wz;
        public bool Uniform => S0 == S1 && S0 == S2 && S0 == S3;

        public Blend(WorldQuery w, double x, double z)
        {
            double sx = (x + w.Half) * w._perCell - 0.5, sz = (z + w.Half) * w._perCell - 0.5;
            double fi = Math.Floor(sx), fj = Math.Floor(sz);
            Wx = sx - fi;
            Wz = sz - fj;
            int i0 = w.ClampCell((int)fi), i1 = w.ClampCell((int)fi + 1);
            int j0 = w.ClampCell((int)fj) * w.Cells, j1 = w.ClampCell((int)fj + 1) * w.Cells;
            C0 = j0 + i0;
            C1 = j0 + i1;
            C2 = j1 + i0;
            C3 = j1 + i1;
            S0 = w._surface[C0];
            S1 = w._surface[C1];
            S2 = w._surface[C2];
            S3 = w._surface[C3];
            W0 = (1 - Wx) * (1 - Wz);
            W1 = Wx * (1 - Wz);
            W2 = (1 - Wx) * Wz;
            W3 = Wx * Wz;
        }
    }

    /// Relief where the corners differ: Σ w·R and its gradient Σ (∇w·R + w·∇R), one evaluation per distinct surface.
    /// `lattice` is the first corner's.
    double BlendedRelief(in Blend b, in Lattice lattice, double x, double z, out double dx, out double dz)
    {
        double r0 = Relief(b.S0, in lattice, x, z, out double x0, out double z0);
        double r1, x1, z1, r2, x2, z2, r3, x3, z3;
        if (b.S1 == b.S0) (r1, x1, z1) = (r0, x0, z0);
        else r1 = Relief(b.S1, new Lattice(x, z, _nodeScale[b.S1]), x, z, out x1, out z1);
        if (b.S2 == b.S0) (r2, x2, z2) = (r0, x0, z0);
        else if (b.S2 == b.S1) (r2, x2, z2) = (r1, x1, z1);
        else r2 = Relief(b.S2, new Lattice(x, z, _nodeScale[b.S2]), x, z, out x2, out z2);
        if (b.S3 == b.S0) (r3, x3, z3) = (r0, x0, z0);
        else if (b.S3 == b.S1) (r3, x3, z3) = (r1, x1, z1);
        else if (b.S3 == b.S2) (r3, x3, z3) = (r2, x2, z2);
        else r3 = Relief(b.S3, new Lattice(x, z, _nodeScale[b.S3]), x, z, out x3, out z3);
        double inv = _perCell;
        dx = inv * ((1 - b.Wz) * (r1 - r0) + b.Wz * (r3 - r2)) + b.W0 * x0 + b.W1 * x1 + b.W2 * x2 + b.W3 * x3;
        dz = inv * ((1 - b.Wx) * (r2 - r0) + b.Wx * (r3 - r1)) + b.W0 * z0 + b.W1 * z1 + b.W2 * z2 + b.W3 * z3;
        return b.W0 * r0 + b.W1 * r1 + b.W2 * r2 + b.W3 * r3;
    }

    float Density(SurfaceParams s, int cell, int kind)
    {
        CoverParams c = s.Cover[kind];
        return c == null ? 0f : (float)(c.Density * _cover[cell * 4 + kind] / 255.0);
    }

    /// Uncompressed mat depth of one kind: Σ over the corners of weight × cover channel × depth noise of that corner's
    /// surface. The noise is evaluated once per distinct surface; `lattice` is the first corner's.
    double Mat(in Blend b, in Lattice lattice, int kind, double x, double z)
    {
        double n0 = MatNoise(b.S0, kind, in lattice);
        double n1 = b.S1 == b.S0 ? n0 : MatNoise(b.S1, kind, new Lattice(x, z, _nodeScale[b.S1]));
        double n2 = b.S2 == b.S0 ? n0 : b.S2 == b.S1 ? n1 : MatNoise(b.S2, kind, new Lattice(x, z, _nodeScale[b.S2]));
        double n3 = b.S3 == b.S0 ? n0 : b.S3 == b.S1 ? n1 : b.S3 == b.S2 ? n2 : MatNoise(b.S3, kind, new Lattice(x, z, _nodeScale[b.S3]));
        return b.W0 * _cover[b.C0 * 4 + kind] / 255.0 * n0 + b.W1 * _cover[b.C1 * 4 + kind] / 255.0 * n1
            + b.W2 * _cover[b.C2 * 4 + kind] / 255.0 * n2 + b.W3 * _cover[b.C3 * 4 + kind] / 255.0 * n3;
    }

    /// Mat depth of a surface's cover at channel 1.0: smooth noise between the depth range's ends (0 without a mat).
    /// `lattice` is the surface's.
    double MatNoise(byte s, int kind, in Lattice lattice)
    {
        CoverParams c = _byIndex[s].Cover[kind];
        if (c == null || !c.HasMat)
            return 0;
        double noise = Noise(in lattice, _matKey[s * 4 + kind]);
        return c.MatDepthMin + (c.MatDepthMax - c.MatDepthMin) * (0.5 + 0.5 * noise);
    }

    /// Micro-relief plus ridges of one surface at (x, z), with its gradient. `lattice` is the surface's.
    double Relief(byte index, in Lattice lattice, double x, double z, out double dx, out double dz)
    {
        double scale = _reliefScale[index];
        double h = scale * Noise(in lattice, _reliefKey[index], _nodeScale[index], out dx, out dz);
        dx *= scale;
        dz *= scale;
        SurfaceParams s = _byIndex[index];
        if (s.RidgeAmplitude > 0)
        {
            h += Ridge(s, x, z, out double slope);
            dx += slope * _ridgeNx[index];
            dz += slope * _ridgeNz[index];
        }
        return h;
    }

    /// The ridges of a surface that has them at (x, z), and their slope across the crests (along _ridgeNx, _ridgeNz): a C²
    /// periodic profile with crests at whole phases, 1 − 2·smoothstep(2q), q = distance to the crest.
    double Ridge(SurfaceParams s, double x, double z, out double slope)
    {
        double phase = (x * _ridgeNx[s.Index] + z * _ridgeNz[s.Index]) / s.RidgeSpacing + _ridgePhase[s.Index];
        double f = phase - Math.Floor(phase);
        double q = Select(f <= 0.5, f, 1.0 - f);
        slope = s.RidgeAmplitude * -4.0 * DetMath.Smooth3Slope(2.0 * q) * Select(f <= 0.5, 1.0, -1.0) / s.RidgeSpacing;
        return s.RidgeAmplitude * (1.0 - 2.0 * DetMath.Smooth3(2.0 * q));
    }

    /// Where (x, z) falls in a value-noise lattice of nodes 1/`scale` apart on the world grid: its lattice cell and the
    /// quintic fades across it. The relief and mat noises of one surface share it.
    readonly struct Lattice
    {
        public readonly int Ix, Iz;
        public readonly double Tx, Tz, A, B;

        public Lattice(double x, double z, double scale)
        {
            double px = x * scale, pz = z * scale, fx = Math.Floor(px), fz = Math.Floor(pz);
            (Ix, Iz, Tx, Tz) = ((int)fx, (int)fz, px - fx, pz - fz);
            A = DetMath.Fade5(Tx);
            B = DetMath.Fade5(Tz);
        }
    }

    /// Value noise in [−1, 1] of the nodes under `key` at a lattice point.
    static double Noise(in Lattice l, uint key)
    {
        uint row0 = DetMath.Hash((uint)l.Iz + key), row1 = DetMath.Hash((uint)(l.Iz + 1) + key); // DetMath.Hash(x, z, key)'s inner hash
        return Interpolate(l.A, l.B, Node(l.Ix, row0), Node(l.Ix + 1, row0), Node(l.Ix, row1), Node(l.Ix + 1, row1));
    }

    /// The same with its gradient per metre, for nodes 1/`scale` apart.
    static double Noise(in Lattice l, uint key, double scale, out double dx, out double dz)
    {
        uint row0 = DetMath.Hash((uint)l.Iz + key), row1 = DetMath.Hash((uint)(l.Iz + 1) + key);
        double v00 = Node(l.Ix, row0), v10 = Node(l.Ix + 1, row0), v01 = Node(l.Ix, row1), v11 = Node(l.Ix + 1, row1);
        double cross = v00 - v10 - v01 + v11;
        dx = DetMath.Fade5Slope(l.Tx) * (v10 - v00 + l.B * cross) * scale;
        dz = DetMath.Fade5Slope(l.Tz) * (v01 - v00 + l.A * cross) * scale;
        return Interpolate(l.A, l.B, v00, v10, v01, v11);
    }

    /// Value noise between the nodes of one lattice cell, at fades (a, b).
    static double Interpolate(double a, double b, double v00, double v10, double v01, double v11) =>
        v00 + a * (v10 - v00) + b * (v01 - v00) + a * b * (v00 - v10 - v01 + v11);

    static double Node(int x, uint row) => DetMath.Unit(DetMath.Hash((uint)x + row)) * 2.0 - 1.0;

    /// The pitfall of generator cell (cx, cz), if it has one: its centre, radius, surface and hash. Each 1 m cell holds at
    /// most one candidate, which exists with the probability of the density of the surface under its centre.
    bool PitCandidate(int cx, int cz, out double px, out double pz, out double radius, out SurfaceParams s, out uint h)
    {
        h = DetMath.Hash(cx, cz, _pitKey);
        double chance = DetMath.Unit(h);
        px = pz = radius = 0;
        s = null;
        if (chance >= _pitMaxDensity)
            return false;
        px = (cx + DetMath.Unit(DetMath.Draw(h, 1))) * PitCell;
        pz = (cz + DetMath.Unit(DetMath.Draw(h, 2))) * PitCell;
        s = _byIndex[_surface[OwnCell(px, pz)]];
        if (chance >= s.PitDensity)
            return false;
        radius = s.PitRadiusMin + (s.PitRadiusMax - s.PitRadiusMin) * DetMath.Unit(DetMath.Draw(h, 3));
        return true;
    }

    /// Pitfall pre-rejection: one bit per 1 m cell (x and z floored, from −Half − Margin) that is set when some pitfall's
    /// disc overlaps that cell. A point whose cell bit is clear lies in no pitfall, so the search is skipped; the result
    /// is the same as searching.
    void InitPitReach()
    {
        if (_pitSearch <= 0)
            return;
        _pitLow = (int)Math.Floor(-Half - Margin);
        _pitSide = (int)Math.Floor(Half + Margin) - _pitLow + 1;
        _pitReach = new ulong[((long)_pitSide * _pitSide + 63) / 64];
        int reach = (int)Math.Ceiling(_pitSearch), last = _pitLow + _pitSide - 1;
        for (int cz = _pitLow - reach; cz <= last + reach; cz++)
        {
            for (int cx = _pitLow - reach; cx <= last + reach; cx++)
            {
                if (!PitCandidate(cx, cz, out double px, out double pz, out double radius, out _, out _))
                    continue;
                int i0 = Math.Max((int)Math.Floor(px - radius), _pitLow), i1 = Math.Min((int)Math.Floor(px + radius), last);
                int j0 = Math.Max((int)Math.Floor(pz - radius), _pitLow), j1 = Math.Min((int)Math.Floor(pz + radius), last);
                for (int j = j0; j <= j1; j++)
                {
                    for (int i = i0; i <= i1; i++)
                    {
                        long bit = (long)(j - _pitLow) * _pitSide + (i - _pitLow);
                        _pitReach[bit >> 6] |= 1UL << (int)(bit & 63);
                    }
                }
            }
        }
    }

    /// The deepest pitfall at (x, z), both within Half + Margin: depth ≥ 0, its gradient and id (0 = none). Its floor is
    /// flat and its wall is a smoothstep over the outer 25 % of the radius.
    double Pitfall(double x, double z, out double dx, out double dz, out uint id)
    {
        double depth = 0;
        dx = dz = 0;
        id = 0;
        if (_pitSearch <= 0)
            return 0;
        long cell = (long)((int)Math.Floor(z) - _pitLow) * _pitSide + ((int)Math.Floor(x) - _pitLow);
        if ((_pitReach[cell >> 6] >> (int)(cell & 63) & 1) == 0)
            return 0;
        int x0 = (int)Math.Floor(x - _pitSearch), x1 = (int)Math.Floor(x + _pitSearch);
        int z0 = (int)Math.Floor(z - _pitSearch), z1 = (int)Math.Floor(z + _pitSearch);
        for (int cz = z0; cz <= z1; cz++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                if (!PitCandidate(cx, cz, out double px, out double pz, out double radius, out SurfaceParams s, out uint h))
                    continue;
                double ox = x - px, oz = z - pz, r2 = ox * ox + oz * oz;
                if (!(r2 < radius * radius))
                    continue;
                double full = s.PitDepthMin + (s.PitDepthMax - s.PitDepthMin) * DetMath.Unit(DetMath.Draw(h, 4));
                double r = Math.Sqrt(r2), wall = PitWall * radius, t = (r - (radius - wall)) / wall;
                double d = t <= 0 ? full : full * (1.0 - DetMath.Smooth3(t));
                if (d <= depth)
                    continue;
                double slope = t <= 0 ? 0 : -full * DetMath.Smooth3Slope(t) / wall / r;
                depth = d;
                dx = slope * ox;
                dz = slope * oz;
                id = (uint)(cx + 32768) & 0xFFFF | ((uint)(cz + 32768) & 0xFFFF) << 16;
            }
        }
        return depth;
    }
}
