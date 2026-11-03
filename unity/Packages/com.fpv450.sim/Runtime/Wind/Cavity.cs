using System;

namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// A cut in the ground whose wind is a cavity flow (p7c-wind design 10, D-071): a trench's run, a niche, a cellar's
    /// shaft. Its centre line (world, m, each point's Y the lip's height there), its opening's width at the lip, its depth
    /// under the lip (the floor the field takes where the core finds no soil under the body), the opening past the first
    /// and the last centre-line points along the end segments, m, and whether a roof covers it below the lip (still air
    /// under it). Derived from the world's hole records (MapWind.Cuts), never drawn; the log header records every value.
    /// </summary>
    public sealed class CutSpec
    {
        public Double3[] CentreLine;
        public double Width, Depth, EndFirst, EndLast;
        public bool Covered;

        internal CutSpec Checked()
        {
            bool ok = CentreLine != null && CentreLine.Length >= 2 && CentreLine.Length <= 4096 && Width > 0 && Width < 100
                && Depth > 0 && Depth < 100 && EndFirst >= 0 && EndFirst < 100 && EndLast >= 0 && EndLast < 100;
            for (int i = 0; ok && i < CentreLine.Length; i++)
            {
                Double3 p = CentreLine[i];
                ok = DetMath.Abs(p.X) < 1e7 && DetMath.Abs(p.Y) < 1e4 && DetMath.Abs(p.Z) < 1e7;
                if (ok && i > 0)
                {
                    double dx = p.X - CentreLine[i - 1].X, dz = p.Z - CentreLine[i - 1].Z;
                    ok = dx * dx + dz * dz > 1e-12;
                }
            }
            if (!ok) throw new ArgumentException("the cut is out of its bounds");
            return this;
        }
    }

    /// <summary>
    /// The flow inside a cut (p7c-wind design 10, D-071; PHY-13, WLD-16), a pure function of the cut, the depth below its
    /// lip and the field's mean above it. The cut's own flow replaces model 20's open profile restarted at its floor.
    /// Labels: measured (a published experiment's reading), published (a published value), derived (computed from those),
    /// assumed (a named constant).
    /// - **The drive:** the field's own mean at h_o = min(W, 1.5·D) over the lip, horizontal, split into the component
    ///   across the cut and the one along it (Soulhac, Perkins and Salizzoni 2008: each drives its own flow).
    /// - **One 2-D cavity per component** (Cell), by its streamwise length L over the depth D (Hotta, Hatanaka, Tanaka and
    ///   Horikawa 1998, a trench in a wind tunnel): one cell below L/D 5, a bubble reattaching on the floor from 6, a blend
    ///   between. The cell is Roshko (1955)'s square-cavity vortex in solid-body rotation, and below a square it falls by
    ///   the slot's e^−π per width (Chew, Aliabadi and Norford 2018: one vortex at full scale). The along component also
    ///   falls by e^(−πη/W), the side walls' hold on it.
    /// - **The turbulence** inside an open cell is √0.1 of the field's (Di Bernardino et al. 2015), falling with the cell;
    ///   a closed cut's reattaching layer brings the field's own.
    /// - **The lip:** the mean and the turbulence grow from 0 at the lip over the separated layer's spread W/7.2, where the
    ///   field above (its cover's still layer) is 0.
    /// The cavity adds no draw: its randomness is the field's.
    /// </summary>
    public static class Cavity
    {
        const double Pi = 3.141592653589793;

        /// <summary>c_v, the cell's strength over its drive, A = c_v·U (published: the mean of Roshko (1955)'s maxima in a
        /// square cavity, u/U∞ 0.27 by the upstream wall, 0.41 by the downstream wall and 0.30 over the floor, NACA TN 3488).</summary>
        public const double CellStrength = 0.33;

        /// <summary>The drive's height over the lip over the depth: h_o = min(W, this·D) (derived from the two experiments'
        /// references: Hotta's log-law layer ended 1.5 H over the lip, and Roshko's free stream stood above a floor layer
        /// thinner than the breadth; reading the field's own mean there is assumed: in the field's thick layer the cell
        /// answers to the flow at its own scale above the lip).</summary>
        public const double DriveHeightOverDepth = 1.5;

        /// <summary>τ_in, an open cell's turbulence over the field's, √0.1 (published: Di Bernardino, Monti, Leuzzi and
        /// Querzoli 2015, the streamwise variance inside canyons of W/H 1–2 nearly an order of magnitude below the flow's
        /// above them, water channel; assumed for v and w and for the fall with the cell).</summary>
        public const double InsideTurbulence = 0.31622776601683794;

        /// <summary>The regimes' bounds on L/D (measured: Hotta et al. 1998, one circulation cell at L/D 4 and below 5, the
        /// shear layer reattaching on the floor from 6); between them a linear blend (derived).</summary>
        public const double OpenBelow = 5, ClosedFrom = 6;

        /// <summary>The reattachment length X_R/D at L/D 6, 8 and 10 (measured: Hotta et al. 1998, linear between), and
        /// behind a step (measured: Hotta's 7.2, the literature's 7.0–7.5), which beyond L/D 10 it reaches as
        /// 7.2 − (7.2 − 6.8)·10/(L/D) (derived: through 6.8 at 10).</summary>
        public const double Reattach6 = 5.2, Reattach8 = 5.8, Reattach10 = 6.8, StepReattachment = 7.2;

        /// <summary>Past reattachment the floor's profile grows in over this many bubble lengths (assumed: the step
        /// literature's recovery takes tens of heights; kept short so a long cut's floor reads the open profile).</summary>
        public const double RecoveryLengths = 1;

        /// <summary>Within this many depths of the downwind wall the reattached flow stalls linearly (assumed: a wall's
        /// stagnation spans about its own height; Hotta's L/D 6 has the wall 0.8 D past reattachment).</summary>
        public const double WallDepths = 1;

        /// <summary>The separated layer's spread over the lip: δ_s = W/this (derived: one depth in Hotta's 7.2).</summary>
        public const double ShearSpread = 7.2;

        /// <summary>X_R/D for a closed cut of L/D = r: Hotta's table (linear between), the step's beyond 10. Below 6, read only
        /// by the transitional band's closed share, it holds at 6's 5.2 (assumed: the first measured point); the caller
        /// keeps X_R ≤ L.</summary>
        public static double Reattachment(double r)
        {
            if (r <= 6) return Reattach6;
            if (r <= 8) return Reattach6 + (Reattach8 - Reattach6) * (r - 6) / 2;
            if (r <= 10) return Reattach8 + (Reattach10 - Reattach8) * (r - 8) / 2;
            return StepReattachment - (StepReattachment - Reattach10) * 10 / r;
        }

        /// <summary>
        /// The 2-D cavity C(L, D, ξ, η, U) of design 10: u along the drive (positive with it) and w upward, m/s, and τ, the
        /// field's turbulence carried there, at ξ from the upwind wall (0 to L) and η below the lip (0 to D), for a drive
        /// U ≥ 0 at h_o over the lip, the field's profile shape over the floor its displacement d and roughness z0 (m),
        /// with D + h_o − d over z0.
        /// - **Open** (L/D below OpenBelow): ψ = A·(D/π)·sin(πξ/L)·sin(πη/D)·e^(−aη), u = ∂ψ/∂η, w = ∂ψ/∂ξ (divergence-free),
        ///   A = c_v·U, a = π·max(0, D − L)/(L·D) (derived: Roshko's square cell on top, below it the one full-scale vortex
        ///   falling by the slot's e^−π per width); τ = τ_in·e^(−aη).
        /// - **Closed** (from ClosedFrom): before X_R a bubble, the same cell with L = X_R and a = 0; past it the floor's
        ///   profile u = U·P(D − η)/P(D + h_o), P the field's log law over the floor (0 in its cover), grown in over
        ///   RecoveryLengths bubble lengths and stalled within WallDepths depths of the downwind wall, w from continuity
        ///   (∫P in closed form, 0 at the floor); τ = 1 (assumed: the reattaching layer brings the outer eddies down).
        /// - **Transitional:** (1 − f)·open + f·closed for each of u, w and τ, f = (L/D − 5)/(6 − 5).
        /// DetMath only; no allocation.
        /// </summary>
        public static void Cell(double L, double D, double xi, double eta, double U, double ho, double d, double z0,
            out double u, out double w, out double tau)
        {
            double r = L / D;
            double f = r < OpenBelow ? 0 : r >= ClosedFrom ? 1 : (r - OpenBelow) / (ClosedFrom - OpenBelow);
            DetMath.SinCos(Pi * eta / D, out double se, out double ce);
            u = w = tau = 0;
            if (f < 1)
            {
                double a = Pi * DetMath.Max(0, D - L) / (L * D), ea = a > 0 ? DetMath.Exp(-a * eta) : 1, A = CellStrength * U;
                DetMath.SinCos(Pi * xi / L, out double sx, out double cx);
                u = (1 - f) * (A * (ce - a * D / Pi * se) * sx * ea);
                w = (1 - f) * (A * D / L * cx * se * ea);
                tau = (1 - f) * (InsideTurbulence * ea);
            }
            if (f > 0)
            {
                double xr = DetMath.Min(Reattachment(r) * D, L), uc, wc;
                if (xi < xr)
                {
                    double A = CellStrength * U;
                    DetMath.SinCos(Pi * xi / xr, out double sb, out double cb);
                    uc = A * ce * sb;
                    wc = A * D / xr * cb * se;
                }
                else
                {
                    double recovery = RecoveryLengths * xr, wall = WallDepths * D;
                    double ramp = DetMath.Min(1, (xi - xr) / recovery), stall = DetMath.Min(1, (L - xi) / wall);
                    double slope = (xi - xr < recovery ? stall / recovery : 0) - (L - xi < wall ? ramp / wall : 0); // d(ramp·stall)/dξ
                    double z = D - eta, reference = DetMath.Log((D + ho - d) / z0), pz = 0, iz = 0;
                    if (z - d > z0)
                    {
                        pz = DetMath.Log((z - d) / z0);
                        iz = (z - d) * (pz - 1) + z0; // ∫ from the cover's top d + z0 to z of ln((z' − d)/z0) dz'
                    }
                    uc = U * ramp * stall * pz / reference;
                    wc = -U * slope * iz / reference;
                }
                u += f * uc;
                w += f * wc;
                tau += f;
            }
        }
    }

    /// <summary>
    /// A flight's cuts and the step's frame in them (p7c-wind design 10): built at the flight's start from the setup's
    /// cuts (a bounding box per cut, each centre-line segment's axis and arc), then Locate at the step's start and Flow at
    /// each point. The frame is the nearest centre-line segment of the cut whose opening holds the body's origin, an open
    /// cut before a covered one (a niche's end reaches across the run's opening: the run takes that point). Every point of
    /// the step uses it (assumed: the hubs lie within 0.4 m of the origin, and E01's bends have a 3.7 m radius). It lives
    /// in the step's frame, never in FlightState, so a replay recomputes it. Zero allocations after the constructor.
    /// </summary>
    public sealed class CutFrame
    {
        const double Pi = 3.141592653589793;

        readonly CutSpec[] _cuts;
        readonly int[] _first; // each cut's first segment; the last cut's end at the array's end
        readonly double[] _x0, _z0, _x1, _z1, _length; // each cut's plan box and its opening's length S
        readonly double[] _ax, _az, _ay, _dy, _tx, _tz, _len, _arc; // each segment's start, lip rise, axis, length, arc at its start

        // The step's frame.
        bool _active, _covered, _driven;
        double _lip, _width, _depth, _length0, _half, _fx, _fz, _ftx, _ftz, _s0;
        double _across, _along, _ho, _d, _z0p;

        internal CutFrame(CutSpec[] cuts)
        {
            int n = cuts.Length, segments = 0;
            foreach (CutSpec c in cuts) segments += c.Checked().CentreLine.Length - 1;
            _cuts = cuts;
            _first = new int[n + 1];
            _x0 = new double[n];
            _z0 = new double[n];
            _x1 = new double[n];
            _z1 = new double[n];
            _length = new double[n];
            _ax = new double[segments];
            _az = new double[segments];
            _ay = new double[segments];
            _dy = new double[segments];
            _tx = new double[segments];
            _tz = new double[segments];
            _len = new double[segments];
            _arc = new double[segments];
            int k = 0;
            for (int c = 0; c < n; c++)
            {
                CutSpec cut = cuts[c];
                Double3[] line = cut.CentreLine;
                _first[c] = k;
                double arc = cut.EndFirst, margin = cut.Width / 2 + DetMath.Max(cut.EndFirst, cut.EndLast);
                _x0[c] = _z0[c] = double.PositiveInfinity;
                _x1[c] = _z1[c] = double.NegativeInfinity;
                for (int i = 0; i < line.Length; i++)
                {
                    _x0[c] = DetMath.Min(_x0[c], line[i].X - margin);
                    _z0[c] = DetMath.Min(_z0[c], line[i].Z - margin);
                    _x1[c] = DetMath.Max(_x1[c], line[i].X + margin);
                    _z1[c] = DetMath.Max(_z1[c], line[i].Z + margin);
                    if (i + 1 == line.Length) break;
                    double dx = line[i + 1].X - line[i].X, dz = line[i + 1].Z - line[i].Z, len = DetMath.Sqrt(dx * dx + dz * dz);
                    _ax[k] = line[i].X;
                    _az[k] = line[i].Z;
                    _ay[k] = line[i].Y;
                    _dy[k] = line[i + 1].Y - line[i].Y;
                    _tx[k] = dx / len;
                    _tz[k] = dz / len;
                    _len[k] = len;
                    _arc[k] = arc;
                    arc += len;
                    k++;
                }
                _length[c] = arc + cut.EndLast;
            }
            _first[n] = k;
        }

        /// <summary>Whether the step's origin lies in a cut's opening (Locate).</summary>
        public bool Active => _active;

        /// <summary>The frame's cut is covered: still air below its lip.</summary>
        public bool Covered => _covered;

        /// <summary>The frame's lip height (world Y), the opening's width W, the depth D under the lip and the opening's
        /// length S, m.</summary>
        public double Lip => _lip;
        public double Width => _width;
        public double Depth => _depth;
        public double Length => _length0;

        /// <summary>The frame's axis t̂ and across n̂ = (−t̂z, t̂x), world plan unit vectors.</summary>
        public Double3 Axis => new Double3(_ftx, 0, _ftz);
        public Double3 Across => new Double3(-_ftz, 0, _ftx);

        /// <summary>The drive's components across (along n̂) and along (along t̂) the cut, m/s, and its height over the lip, m.</summary>
        public double DriveAcross => _across;
        public double DriveAlong => _along;
        public double DriveHeight => _ho;

        /// <summary>
        /// The frame at the body's origin (world plan x, z) over `ground` (the field's ground under it: over a hole the
        /// soil's, FlightWorld.SoilUnder; −∞ for none): the nearest segment of the cut whose opening holds it, open before
        /// covered, ties to the first in the setup's order. A segment's opening is the strip within W/2 of it, the end
        /// segments' reaching past their end points by the cut's ends (square: the end walls), with a disc of W/2 about each
        /// inner joint (a bend's outer corner). D is the lip less the ground there, the cut's own depth where there is none.
        /// False, and no frame, where no opening holds the origin.
        /// </summary>
        internal bool Locate(double x, double z, double ground)
        {
            _active = _driven = false;
            int bestCut = -1, bestSeg = -1;
            double bestDist = double.PositiveInfinity, bestU = 0;
            bool bestCovered = true;
            for (int c = 0; c < _cuts.Length; c++)
            {
                if (!(x >= _x0[c] && x <= _x1[c] && z >= _z0[c] && z <= _z1[c])) continue;
                CutSpec cut = _cuts[c];
                bool covered = cut.Covered;
                if (covered && !bestCovered) continue;
                double half = cut.Width / 2;
                int first = _first[c], last = _first[c + 1] - 1;
                for (int i = first; i <= last; i++)
                {
                    double dx = x - _ax[i], dz = z - _az[i];
                    double u = dx * _tx[i] + dz * _tz[i], v = dz * _tx[i] - dx * _tz[i];
                    double lo = i == first ? -cut.EndFirst : 0, hi = i == last ? _len[i] + cut.EndLast : _len[i], dist;
                    if (u < lo)
                    {
                        if (i == first) continue;
                        dist = DetMath.Sqrt(u * u + v * v);
                    }
                    else if (u > hi)
                    {
                        if (i == last) continue;
                        double du = u - hi;
                        dist = DetMath.Sqrt(du * du + v * v);
                    }
                    else dist = DetMath.Abs(v);
                    if (!(dist <= half)) continue;
                    if (bestCut >= 0 && covered == bestCovered && !(dist < bestDist)) continue;
                    bestCut = c;
                    bestSeg = i;
                    bestDist = dist;
                    bestU = u;
                    bestCovered = covered;
                }
            }
            if (bestCut < 0) return false;
            CutSpec best = _cuts[bestCut];
            int s = bestSeg;
            double along = DetMath.Min(DetMath.Max(bestU, 0), _len[s]);
            _active = true;
            _covered = best.Covered;
            _lip = _ay[s] + _dy[s] * along / _len[s];
            _width = best.Width;
            _half = best.Width / 2;
            _length0 = _length[bestCut];
            _depth = ground > double.NegativeInfinity ? _lip - ground : best.Depth;
            _fx = _ax[s];
            _fz = _az[s];
            _ftx = _tx[s];
            _ftz = _tz[s];
            _s0 = _arc[s];
            return true;
        }

        /// <summary>The step's drive: the field's mean at ho over the lip (world, horizontal, m/s), and the field's profile
        /// shape (d, z0, m) the closed form's floor reads; driven false for none (a slit inside the cover's still layer).</summary>
        internal void Drive(in Double3 mean, double ho, double d, double z0, bool driven)
        {
            _driven = driven;
            _ho = ho;
            _d = d;
            _z0p = z0;
            _across = driven ? mean.Z * _ftx - mean.X * _ftz : 0;
            _along = driven ? mean.X * _ftx + mean.Z * _ftz : 0;
        }

        /// <summary>
        /// The cavity's mean at a point below the lip (world, m/s), and `scale`, τ·Λ, the share of the field's fluctuation
        /// it carries (0 where the air is still). Every point uses the step's frame: its offset n across the axis and its
        /// arc s along the opening. In the walls (|n| over W/2, s outside 0 to S), under the floor (η ≥ D), under a roof or
        /// without a drive the air is still. Otherwise Λ·(u⊥·n̂ + u∥·t̂ + (w⊥ + w∥)·ŷ), the across cell at ξ = W/2 + n·sign(U⊥)
        /// and the along one at s or S − s by the sign of U∥, times e^(−πη/W); Λ = min(1, η/δ_s), δ_s = W/7.2; and
        /// τ² = (U⊥²·τ⊥² + U∥²·τ∥²)/(U⊥² + U∥²) (assumed: each component's share is its drive's energy).
        /// </summary>
        internal Double3 Flow(in Double3 point, out double scale)
        {
            scale = 0;
            double eta = _lip - point.Y;
            if (_covered || !_driven || !(eta > 0 && eta < _depth)) return default;
            double dx = point.X - _fx, dz = point.Z - _fz;
            double n = dz * _ftx - dx * _ftz, s = _s0 + dx * _ftx + dz * _ftz;
            if (!(DetMath.Abs(n) <= _half && s >= 0 && s <= _length0)) return default;
            double ua = DetMath.Abs(_across), ul = DetMath.Abs(_along);
            Cavity.Cell(_width, _depth, _half + (_across < 0 ? -n : n), eta, ua, _ho, _d, _z0p, out double u1, out double w1, out double t1);
            Cavity.Cell(_length0, _depth, _along < 0 ? _length0 - s : s, eta, ul, _ho, _d, _z0p, out double u2, out double w2, out double t2);
            double hold = DetMath.Exp(-Pi * eta / _width); // the side walls' hold on the along flow
            u2 *= hold;
            w2 *= hold;
            t2 *= hold;
            if (_across < 0) u1 = -u1;
            if (_along < 0) u2 = -u2;
            double lambda = DetMath.Min(1, eta * Cavity.ShearSpread / _width), energy = ua * ua + ul * ul;
            scale = energy > 0 ? DetMath.Sqrt((ua * ua * t1 * t1 + ul * ul * t2 * t2) / energy) * lambda : 0;
            return new Double3(-u1 * _ftz + u2 * _ftx, w1 + w2, u1 * _ftx + u2 * _ftz) * lambda;
        }
    }
}
