using System;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Wind
{
    /// <summary>The hills' perturbation of the mean wind at a point (HillFlow.Sample): the fractional speed-up tensor S
    /// (horizontal, symmetric: Δu = S·u) and the streamlines' slope G (w = G·u), u the undisturbed mean there.</summary>
    public struct HillSample
    {
        public double S11, S12, S22, G1, G2;
    }

    /// <summary>The ground under a batch of plan points, as IWorldQuery.SampleGround answers it.</summary>
    public delegate void GroundSampler(ReadOnlySpan<XZ> points, Span<GroundSample> results);

    /// <summary>The lee of a terrain step at a point (HillFlow.Lee): ξ downwind of the face, the across direction n̂ from the
    /// high side to the low (world plan unit vector), and the lip's height over the point.</summary>
    public struct LeeFrame
    {
        public double Xi, NX, NZ, Lip;
    }

    /// <summary>
    /// The wind over low hills (w7-farmland-wind 1.1, design 2–3; PHY-13), a pure function of the world's ground and the
    /// field's profile (z0, d, its top), built once per terrain and shared by every flight over it (Of, its cache). Labels:
    /// published (cited), recalled (a published rule, not re-read in this task), derived, numerical (the method's own).
    /// - **The model** (published): the linear theory of a turbulent boundary layer over low hills, Jackson and Hunt (1975),
    ///   as Hunt, Leibovich and Richards (1988) layered it and Mason and Sykes (1979) took it to three dimensions, in the
    ///   spectral form MS3DJH (Walmsley, Salmon and Taylor 1982) and WAsP (Troen and Petersen 1989) evaluate on real
    ///   terrain. Each Fourier mode k of the heights, L = 1/|k| its length (recalled: the spectral models' scale):
    ///   - the outer layer's potential flow: the surface's horizontal perturbation per unit speed (k_i k_j / |k|)·ĥ and the
    ///     streamlines' slope i·k_i·ĥ, each falling as e^(−|k|ζ) (derived: Laplace's equation over the ground);
    ///   - the inner layer's depth l, (l/L)·ln(l/z0) = 2κ², and the middle layer's height h_m, (h_m/L)·√ln(h_m/z0) = 1
    ///     (recalled: Jackson and Hunt's l, Hunt et al.'s h_m);
    ///   - the fractional speed-up of the mode, ΔS = σ·A(ζ): in the middle layer the perturbation's momentum holds,
    ///     A = e^(−|k|ζ)·(ln(h_m/z0)/ln(ζ/z0))², constant through the inner layer at its value at l, and in the outer layer
    ///     A = e^(−|k|ζ)·ln(h_m/z0)/ln(ζ/z0) (recalled: Jackson and Hunt's ΔS ∝ U0²(h_m)/U0²(ζ) below h_m, the outer flow
    ///     driven by U0(h_m)); ζ = z − d over the cover, z0 the field's.
    ///   - Its check (published): Taylor and Lee (1984)'s guidelines from this theory and the field data, ΔS_max = 2.0 h/L1
    ///     over a 2-D ridge and 1.6 h/L1 over a 3-D axisymmetric hill, ±15 % (Kaimal and Finnigan 1994), L1 the distance from
    ///     the crest to half its height, falling as e^(−A z/L1) (A = 3 and 4, recalled). HillFlowTests flies both shapes.
    /// - **Its validity** (published): slopes under 0.3 (separation never below 0.27, always from 0.32: Finnigan 1988's
    ///   compilation), L1/z0 over 100 and L1 under 2 km (Taylor and Lee 1984, as Lubitz and White 2007 give them). So a mode
    ///   of L under 2·ShortestOverRoughness·z0 is rolled off by a raised cosine, gone at L = 100·z0 (numerical: no ringing);
    ///   the domain's longest mode is 652 m. Past the slope, the separated lee of the terrain's steps (Lee).
    /// - **The grid** (numerical): Size² nodes Cell apart about the map's centre, each the mean of SubSamples² ground samples
    ///   over its cell (no aliasing of the 1 m terrain), less their mean and tapered to it over Border at the domain's edge
    ///   (the FFT's period), its output faded to 0 over the same band. Levels levels from ζ = z0 to the profile's top in
    ///   equal steps of ln(ζ/z0), the field's own log, read linearly between them and bilinearly between the nodes.
    /// - **Direction-free:** the along-wind perturbation of a mean u is S·u with S_ij the inverse transform of
    ///   ĥ·A·k_i k_j/|k|, and w = G·u with G_i that of ĥ·e^(−|k|ζ)·i·k_i: five grids a level for every bearing, so nothing
    ///   is rebuilt when the wind turns.
    /// - **The steps** (Lee): a terrain step taller than the cover's still layer (d + z0) is a backward-facing step for the
    ///   wind from its high side, its lee separated (WindField.InLee).
    /// DetMath and the basic operations only, in double precision: the transforms are radix-2 FFTs on DetMath's twiddles.
    /// Load time: about 24 MB and a fraction of a second; Sample and Lee allocate nothing.
    /// </summary>
    public sealed class HillFlow
    {
        /// <summary>The model's version, with the model number (LogFormat.ModelVersion 84): 1, w7-farmland-wind 1.1.</summary>
        public const int Version = 1;

        /// <summary>The grid's cell, m (numerical: the shortest mode kept, 2π·10 m, spans 3.9 cells, and the fully kept ones,
        /// from 2π·20 m, 7.9).</summary>
        public const double Cell = 16;

        /// <summary>Nodes a side (numerical: 4096 m, a power of two; on the farmland the map, its surround and the ledge's lee).</summary>
        public const int Size = 256;

        /// <summary>The domain edge's band, m, over which the heights taper to their mean and the perturbation fades to 0
        /// (numerical: the FFT's period).</summary>
        public const double Border = 256;

        /// <summary>Ground samples a side averaged into a node (numerical: 4 m apart over the 16 m cell).</summary>
        public const int SubSamples = 4;

        /// <summary>The shortest valid half-length over z0 (published: Taylor and Lee 1984's L1/z0 &gt; 100).</summary>
        public const double ShortestOverRoughness = 100;

        /// <summary>The levels from ζ = z0 to the profile's top (numerical: steps of 0.86 in ln(ζ/z0), a factor 2.4 in ζ).</summary>
        public const int Levels = 9;

        /// <summary>The slope from which the flow separates (published: Finnigan 1988's compilation, always from 0.32, never
        /// below 0.27; Lubitz and White 2007). The terrain's steps are vertical; Steeper counts the cells past it.</summary>
        public const double SeparationSlope = 0.32;

        /// <summary>The lip's samples along a step, m (numerical: the map's 0.977 m heights).</summary>
        public const double LipSpacing = 0.5;

        const double Pi = 3.141592653589793, TwoPi = 6.283185307179586;
        const int Values = 5;

        /// <summary>The grid's south-west node (world X, Z, m) and the profile it was built for (z0, d, top, m).</summary>
        public readonly double OriginX, OriginZ, Roughness, Displacement, Top;

        /// <summary>The levels' step in ln(ζ/z0).</summary>
        public readonly double LevelStep;

        /// <summary>The world's content hash the grid was built from.</summary>
        public readonly ulong WorldHash;

        /// <summary>The steepest node-to-node slope of the grid's heights, and the nodes past SeparationSlope there (derived,
        /// for the validity's record; the steps are not in it: their faces fall within one cell).</summary>
        public readonly double SteepestSlope;
        public readonly int Steeper;

        readonly double[] _v;   // ((level·Size + j)·Size + i)·Values + c: S11, S12, S22, G1, G2
        readonly Step[] _steps; // the steps taller than the still layer

        sealed class Step
        {
            public bool AlongZ;
            public double C, From, To, Most;
            public int Side;      // +1: the high side lies at greater across coordinates
            public double[] Lip;  // every LipSpacing from From
        }

        HillFlow(double ox, double oz, double z0, double d, double top, ulong hash, double[] v, Step[] steps, double steepest, int steeper)
        {
            OriginX = ox;
            OriginZ = oz;
            Roughness = z0;
            Displacement = d;
            Top = top;
            LevelStep = DetMath.Log((top - d) / z0) / (Levels - 1);
            WorldHash = hash;
            _v = v;
            _steps = steps;
            SteepestSlope = steepest;
            Steeper = steeper;
        }

        /// <summary>The steps whose lee is separated (taller than the cover's still layer).</summary>
        public int Steps => _steps.Length;

        static readonly object CacheLock = new object();
        static HillFlow _last;

        /// <summary>
        /// The hills of a map's terrain for the field's profile (every level's: z0, d and the top are the same), or null on a
        /// plane. Kept for the next flight over the same world (its content hash). Load time: allocates.
        /// </summary>
        public static HillFlow Of(MapWorld map)
        {
            if (map == null || map.Geometry(WorldIds.Ground, 0, out _)) return null;
            WindPreset p = WindPreset.Of(WindLevel.Severe);
            lock (CacheLock)
            {
                HillFlow last = _last;
                if (last != null && last.WorldHash == map.ContentHash && last.Roughness == p.Roughness && last.Displacement == p.Displacement
                    && last.Top == p.ProfileTop)
                    return last;
                double cx = map.ExtentX + map.CellsX * WakeObstacles.Cell / 2, cz = map.ExtentZ + map.CellsZ * WakeObstacles.Cell / 2;
                _last = Build(map.SampleGround, map.ContentHash, cx, cz, map.TerrainSteps, p.Roughness, p.Displacement, p.ProfileTop);
                return _last;
            }
        }

        /// <summary>
        /// The hills of a ground (a world's SampleGround) over the grid centred on (cx, cz), with its steps, for a profile's z0,
        /// d and top (m); hash names the world. Deterministic: the same ground gives the same bits on every runtime. Load time:
        /// allocates.
        /// </summary>
        public static HillFlow Build(GroundSampler world, ulong hash, double cx, double cz, TerrainStep[] steps, double z0, double d, double top)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!(z0 > 0) || !(d >= 0) || !(top > d + z0)) throw new ArgumentException("the profile is out of its bounds");
            const int n = Size;
            double ox = cx - n * Cell / 2, oz = cz - n * Cell / 2;

            // The heights: each node the mean of its cell's samples (holes and off the world left out), less the mean,
            // tapered to it at the edge.
            var h = new double[n * n];
            var at = new XZ[SubSamples * SubSamples];
            var g = new GroundSample[at.Length];
            double sum = 0;
            int finite = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    for (int b = 0; b < SubSamples; b++)
                        for (int a = 0; a < SubSamples; a++)
                            at[b * SubSamples + a] = new XZ(ox + i * Cell + ((a + 0.5) / SubSamples - 0.5) * Cell, oz + j * Cell + ((b + 0.5) / SubSamples - 0.5) * Cell);
                    world(at, g);
                    double s = 0;
                    int c = 0;
                    for (int k = 0; k < g.Length; k++)
                    {
                        if ((g[k].Flags & GroundFlags.Hole) != 0 || !(g[k].Height > double.NegativeInfinity && g[k].Height < double.PositiveInfinity)) continue;
                        s += g[k].Height;
                        c++;
                    }
                    h[j * n + i] = c > 0 ? s / c : double.NaN;
                    if (c > 0)
                    {
                        sum += h[j * n + i];
                        finite++;
                    }
                }
            double mean = finite > 0 ? sum / finite : 0;
            double steepest = 0;
            int steeper = 0;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    if (double.IsNaN(h[k])) h[k] = mean;
                }
            for (int j = 1; j < n; j++)
                for (int i = 1; i < n; i++)
                {
                    int k = j * n + i;
                    double sx = DetMath.Abs(h[k] - h[k - 1]) / Cell, sz = DetMath.Abs(h[k] - h[k - n]) / Cell, sl = DetMath.Max(sx, sz);
                    if (sl > steepest) steepest = sl;
                    if (sl > SeparationSlope) steeper++;
                }
            var taper = new double[n];
            for (int i = 0; i < n; i++) taper[i] = Smooth(DetMath.Min(i, n - 1 - i) * Cell / Border);
            var re = new double[n * n];
            var im = new double[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    re[j * n + i] = (h[j * n + i] - mean) * (taper[i] * taper[j]);
            var fft = new Fft2(n);
            fft.Transform(re, im, false);

            // Each mode's wavenumber, window and layers.
            var kx = new double[n];
            for (int m = 0; m < n; m++) kx[m] = TwoPi * (m < n / 2 ? m : m - n) / (n * Cell);
            double kappa2 = 2 * WindPreset.Karman * WindPreset.Karman, lmin = ShortestOverRoughness * z0;
            var kk = new double[n * n];
            var window = new double[n * n];
            var lnL = new double[n * n];
            var lnM = new double[n * n];
            var eL = new double[n * n];
            for (int b = 0; b < n; b++)
                for (int a = 0; a < n; a++)
                {
                    int k = b * n + a;
                    double K = DetMath.Sqrt(kx[a] * kx[a] + kx[b] * kx[b]);
                    kk[k] = K;
                    if (!(K > 0) || a == n / 2 || b == n / 2) continue; // the mean, and the Nyquist rows (far past the window)
                    double L = 1 / K;
                    double w = L >= 2 * lmin ? 1 : L <= lmin ? 0 : Smooth((L - lmin) / lmin);
                    if (!(w > 0)) continue;
                    window[k] = w;
                    double l = Inner(kappa2 * L, z0), hm = DetMath.Max(Middle(L, z0), l);
                    lnL[k] = DetMath.Log(l / z0);
                    lnM[k] = DetMath.Log(hm / z0);
                    eL[k] = DetMath.Exp(-K * l);
                }

            // Each level's five grids: (S11 + i·S12), (S22 + i·G1) and G2 from three inverse transforms.
            double step = DetMath.Log((top - d) / z0) / (Levels - 1);
            var v = new double[Levels * n * n * Values];
            var r1 = new double[n * n];
            var i1 = new double[n * n];
            var r2 = new double[n * n];
            var i2 = new double[n * n];
            var r3 = new double[n * n];
            var i3 = new double[n * n];
            for (int level = 0; level < Levels; level++)
            {
                double lambda = level * step, zeta = z0 * DetMath.Exp(lambda);
                for (int b = 0; b < n; b++)
                    for (int a = 0; a < n; a++)
                    {
                        int k = b * n + a;
                        double w = window[k];
                        if (!(w > 0))
                        {
                            r1[k] = i1[k] = r2[k] = i2[k] = r3[k] = i3[k] = 0;
                            continue;
                        }
                        double K = kk[k], e = DetMath.Exp(-K * zeta), A;
                        if (!(lambda > lnL[k]))
                        {
                            double q = lnM[k] / lnL[k];
                            A = eL[k] * (q * q);
                        }
                        else if (!(lambda > lnM[k]))
                        {
                            double q = lnM[k] / lambda;
                            A = e * (q * q);
                        }
                        else A = e * (lnM[k] / lambda);
                        double hr = re[k], hi = im[k], s = w * A / K, t = w * e;
                        double s11 = s * kx[a] * kx[a], s12 = s * kx[a] * kx[b], s22 = s * kx[b] * kx[b], g1 = t * kx[a], g2 = t * kx[b];
                        // ĥ·s11 + i·(ĥ·s12); ĥ·s22 + i·(ĥ·i·g1); ĥ·i·g2.
                        r1[k] = hr * s11 - hi * s12;
                        i1[k] = hi * s11 + hr * s12;
                        r2[k] = hr * s22 - hr * g1;
                        i2[k] = hi * s22 - hi * g1;
                        r3[k] = -hi * g2;
                        i3[k] = hr * g2;
                    }
                fft.Transform(r1, i1, true);
                fft.Transform(r2, i2, true);
                fft.Transform(r3, i3, true);
                double scale = 1.0 / (n * (double)n);
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int k = j * n + i, o = ((level * n + j) * n + i) * Values;
                        double fade = taper[i] * taper[j] * scale;
                        v[o] = r1[k] * fade;
                        v[o + 1] = i1[k] * fade;
                        v[o + 2] = r2[k] * fade;
                        v[o + 3] = i2[k] * fade;
                        v[o + 4] = r3[k] * fade;
                    }
            }
            return new HillFlow(ox, oz, z0, d, top, hash, v, SeparatedSteps(world, steps, d + z0), steepest, steeper);
        }

        // The steps taller than the still layer, each with its high side and its lip every LipSpacing.
        static Step[] SeparatedSteps(GroundSampler world, TerrainStep[] steps, double still)
        {
            if (steps == null) return new Step[0];
            int kept = 0;
            foreach (TerrainStep t in steps) if (t.Least > still) kept++;
            var result = new Step[kept];
            var at = new XZ[2];
            var g = new GroundSample[2];
            kept = 0;
            foreach (TerrainStep t in steps)
            {
                if (!(t.Least > still)) continue;
                double mid = 0.5 * (t.From + t.To);
                at[0] = t.AlongZ ? new XZ(t.C + 1, mid) : new XZ(mid, t.C + 1);
                at[1] = t.AlongZ ? new XZ(t.C - 1, mid) : new XZ(mid, t.C - 1);
                world(at, g);
                int side = g[0].Height > g[1].Height ? 1 : -1;
                int count = (int)((t.To - t.From) / LipSpacing) + 2;
                var lip = new double[count];
                var one = new XZ[1];
                var gs = new GroundSample[1];
                for (int k = 0; k < count; k++)
                {
                    double a = DetMath.Min(t.From + k * LipSpacing, t.To), c = t.C + side * 0.01; // 1 cm inside the high side
                    one[0] = t.AlongZ ? new XZ(c, a) : new XZ(a, c);
                    world(one, gs);
                    lip[k] = gs[0].Height;
                }
                result[kept++] = new Step { AlongZ = t.AlongZ, C = t.C, From = t.From, To = t.To, Most = t.Most, Side = side, Lip = lip };
            }
            return result;
        }

        /// <summary>
        /// The hills' perturbation at world (x, z) and λ = ln((min(h, top) − d)/z0) (h the height over the ground, the field's
        /// own log): false, and none, outside the grid. Allocation-free.
        /// </summary>
        public bool Sample(double x, double z, double lambda, out HillSample s)
        {
            s = default;
            double fx = (x - OriginX) / Cell, fz = (z - OriginZ) / Cell;
            if (!(fx >= 0 && fx < Size - 1 && fz >= 0 && fz < Size - 1)) return false;
            int i = (int)fx, j = (int)fz, level;
            double u = fx - i, v = fz - j, t = lambda / LevelStep, f;
            if (!(t > 0))
            {
                level = 0;
                f = 0;
            }
            else if (!(t < Levels - 1))
            {
                level = Levels - 2;
                f = 1;
            }
            else
            {
                level = (int)t;
                f = t - level;
            }
            int a = ((level * Size + j) * Size + i) * Values, b = a + Size * Size * Values;
            s.S11 = Mix(a, b, 0, u, v, f);
            s.S12 = Mix(a, b, 1, u, v, f);
            s.S22 = Mix(a, b, 2, u, v, f);
            s.G1 = Mix(a, b, 3, u, v, f);
            s.G2 = Mix(a, b, 4, u, v, f);
            return true;
        }

        // Value c bilinear in the cell at node offsets a (level) and b (the next level), then linear between the levels.
        double Mix(int a, int b, int c, double u, double v, double f)
        {
            double p = Bilinear(a + c, u, v), q = Bilinear(b + c, u, v);
            return p + (q - p) * f;
        }

        double Bilinear(int o, double u, double v)
        {
            const int row = Size * Values;
            double v00 = _v[o], v10 = _v[o + Values], v01 = _v[o + row], v11 = _v[o + row + Values];
            double s0 = v00 + (v10 - v00) * u, s1 = v01 + (v11 - v01) * u;
            return s0 + (s1 - s0) * v;
        }

        /// <summary>
        /// The lee of a step at world (x, z) for a mean wind `mean` (world, m/s): a step whose span holds the point, the point
        /// on its low side less than (1 + Cavity.RecoveryLengths)·Cavity.StepReattachment of its tallest face downwind, the
        /// wind blowing from its high side; false where none. Allocation-free.
        /// </summary>
        public bool Lee(double x, double z, in Double3 mean, out LeeFrame frame)
        {
            frame = default;
            foreach (Step s in _steps)
            {
                double along = s.AlongZ ? z : x, across = s.AlongZ ? x : z;
                if (!(along >= s.From && along <= s.To)) continue;
                double xi = (s.C - across) * s.Side;
                if (!(xi > 0 && xi < (1 + Cavity.RecoveryLengths) * Cavity.StepReattachment * s.Most)) continue;
                double un = -s.Side * (s.AlongZ ? mean.X : mean.Z);
                if (!(un > 0)) continue;
                double fa = (along - s.From) / LipSpacing;
                int k = (int)fa;
                if (k >= s.Lip.Length - 1) k = s.Lip.Length - 2;
                double lip = s.Lip[k] + (s.Lip[k + 1] - s.Lip[k]) * (fa - k);
                frame = new LeeFrame { Xi = xi, NX = s.AlongZ ? -s.Side : 0, NZ = s.AlongZ ? 0 : -s.Side, Lip = lip };
                return true;
            }
            return false;
        }

        // A raised cosine from 0 at t = 0 to 1 at t = 1.
        static double Smooth(double t) => t >= 1 ? 1 : t <= 0 ? 0 : 0.5 - 0.5 * DetMath.Cos(Pi * t);

        // l with l·ln(l/z0) = c (Newton from the right, convex: monotone).
        static double Inner(double c, double z0)
        {
            double l = c;
            for (int k = 0; k < 40; k++)
            {
                double q = DetMath.Log(l / z0);
                l -= (l * q - c) / (q + 1);
            }
            return l;
        }

        // h with h·√ln(h/z0) = L (Newton from the right, convex where ln(h/z0) > ½: monotone).
        static double Middle(double L, double z0)
        {
            double h = L;
            for (int k = 0; k < 40; k++)
            {
                double s = DetMath.Sqrt(DetMath.Log(h / z0));
                h -= (h * s - L) / (s + 1 / (2 * s));
            }
            return h;
        }

        /// <summary>A 2-D radix-2 FFT of Size² points, rows then columns, on DetMath's twiddles; no scaling.</summary>
        sealed class Fft2
        {
            readonly int _n;
            readonly double[] _cos, _sin, _re, _im;
            readonly int[] _rev;

            public Fft2(int n)
            {
                _n = n;
                _cos = new double[n / 2];
                _sin = new double[n / 2];
                for (int k = 0; k < n / 2; k++) DetMath.SinCos(TwoPi * k / n, out _sin[k], out _cos[k]);
                _rev = new int[n];
                int bits = 0;
                while ((1 << bits) < n) bits++;
                for (int i = 0; i < n; i++)
                {
                    int r = 0;
                    for (int b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b);
                    _rev[i] = r;
                }
                _re = new double[n];
                _im = new double[n];
            }

            // Forward: e^(−i·k·x); inverse: e^(+i·k·x).
            public void Transform(double[] re, double[] im, bool inverse)
            {
                int n = _n;
                for (int j = 0; j < n; j++)
                {
                    Array.Copy(re, j * n, _re, 0, n);
                    Array.Copy(im, j * n, _im, 0, n);
                    Line(inverse);
                    Array.Copy(_re, 0, re, j * n, n);
                    Array.Copy(_im, 0, im, j * n, n);
                }
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        _re[j] = re[j * n + i];
                        _im[j] = im[j * n + i];
                    }
                    Line(inverse);
                    for (int j = 0; j < n; j++)
                    {
                        re[j * n + i] = _re[j];
                        im[j * n + i] = _im[j];
                    }
                }
            }

            void Line(bool inverse)
            {
                int n = _n;
                double[] re = _re, im = _im;
                for (int i = 0; i < n; i++)
                {
                    int j = _rev[i];
                    if (j <= i) continue;
                    double tr = re[i], ti = im[i];
                    re[i] = re[j];
                    im[i] = im[j];
                    re[j] = tr;
                    im[j] = ti;
                }
                for (int len = 2; len <= n; len <<= 1)
                {
                    int half = len >> 1, stride = n / len;
                    for (int s = 0; s < n; s += len)
                        for (int k = 0; k < half; k++)
                        {
                            double wr = _cos[k * stride], wi = inverse ? _sin[k * stride] : -_sin[k * stride];
                            int a = s + k, b = a + half;
                            double xr = re[b] * wr - im[b] * wi, xi = re[b] * wi + im[b] * wr;
                            re[b] = re[a] - xr;
                            im[b] = im[a] - xi;
                            re[a] += xr;
                            im[a] += xi;
                        }
                }
            }
        }
    }
}
