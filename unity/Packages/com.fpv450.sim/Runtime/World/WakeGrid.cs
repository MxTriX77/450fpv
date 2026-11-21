// WakeGrid: the obstacle wakes of one wind direction on the 2 m grid (p7c-wind 1.3, design 4, K§6.7 layer 3).
//
// Built (Build, any thread, deterministic: the same obstacles and direction give the same bits) by marching upwind from
// every cell centre, in 1 m steps, to the nearest obstacle whose wake reaches the cell (x ≤ 30 H, capped at 300 m,
// K§6.7). The march crosses the obstacle to read its height H (the highest top on the path), its base B (the lowest
// base) and its along-wind depth L; its porosity β is its frontal porosity across the cell's 2 m lane, from its
// volumes' own geometry (WakeObstacles.Transmission, w7-farmland-wind 1.2: solids opaque however thin, porous parts by
// Beer–Lambert over their chord, parts at different heights side by side), and an obstacle the lane misses is no
// obstacle there. A cell inside an obstacle reads the lane from the obstacle's windward edge to its own centre, at
// x = 0. Where the cell's own path is clear but an obstacle lies within 16 m to a side (the side probes, every 2 m of
// the march), that obstacle's wake spreads in sideways, and with obstacles on both sides the path runs through a gap.
// The overlying wake (w7-farmland-wind 1.2, design 5): past that nearest obstacle the march goes on, on the cell's own
// path, to the nearest obstacle taller than it whose wake reaches the cell. A lower obstacle never hides the wake of a
// taller one upwind (a hedge 15 m behind a 12 m tree stands in the tree's wake, and the tree's wake goes on over it).
// The two wakes compose as each obstacle's wake relative to the flow that reaches it (derived, as porosities multiply
// along a path, K§10.3): the speeds multiply (but in the overlying obstacle's reverse flow, which the nearer one cannot
// turn, that reverse flow), and the turbulence's excess variances add (independent eddies), the eddies at the height of
// the obstacle carrying the larger excess. Not composed (a truncation, its size measured in w7-farmland-wind's design):
// a third obstacle, and a farther one no taller than the nearest.
// Version 1's rules (WakeObstacles.Version 1: each cell's porosity by K§10.3's covered fraction, the path's β the product of
// √β_cell over its 1 m steps, the nearest wake alone, a porosity-free boost) are kept bit for bit for its logs and the kept
// audits of older models.
// Each cell then holds its wakes' coefficients; Sample evaluates them at a height and interpolates the four nearest
// cell centres bilinearly. An obstacle outside the grid is not seen: the grid must reach 300 m upwind of the flying.
//
// The wake at a point x behind the obstacle's leeward edge, at height z, with X = x/H, Z = z/H (every function and
// constant is named below; "published" ones cite their source, "assumed" ones are this model's, "derived" follow):
// - The far wake (X ≥ 5): Perera (1981)'s shelter model, as the European Wind Atlas uses it (Troen and Petersen 1989,
//   the shelter model; recalled, not re-read in this task), the relative speed reduction
//       R = 9.75·(1 − β)·η·exp(−0.67·η^1.5) / X,   η = Z / (K·X)^(1/(n+2)),   K = 2κ²/ln(H/z0),  n = 0.14,  κ = 0.4,
//   read as the reduction of the undisturbed speed at the same height. Below the peak (η < 0.997) R is held at its
//   peak (assumed: the far-wake theory does not resolve the surface layer, and porous belts shelter most near the
//   ground). The wake grows as x^0.47, to 2–3 H tall at 10–20 H (K§8.2).
// - The near wake (X < 5): from the bleed through the obstacle at X = 0 (u/U = β between its base and top, assumed:
//   the optical porosity as the through-flow fraction) linearly to Perera's wake at X = 5 (assumed).
// - The speed-up over the top: +0.2·(1 − β) at Z = 1.2 (K§6.7's "about 1.2 H"), a Gaussian of σ 0.3 in Z and 1.5 in X
//   (assumed: about 20 % over a solid barrier).
// - The jet under crowns with a base (B > 0): +0.2·(1 − β)·exp(−X/2) below the base (assumed), with the bleed's shear
//   layers 0.1 H thick at the base and the top (assumed).
// - Bluff-body recirculation, at full strength for β ≤ 0.1 and gone at β ≥ 0.3 (K§6.7; Perera 1981 found the bubble
//   vanishes above a porosity of about 0.3), a smooth step between: reverse flow in an elliptic cavity from the
//   leeward face (height H) to the reattachment X_R = 1.8·(W/H) / ((L/H)^0.3·(1 + 0.24·W/H)) (Fackrell 1984, recalled;
//   W the crosswind width, L the depth), with u/U = −0.35·4ξ(1 − ξ)·(1 − Z/Z_c), ξ = X/X_R (assumed shape; cube
//   wakes reach about −0.3 to −0.4 of the approach speed, Martinuzzi and Tropea 1993, recalled).
// - The lateral spread: the deficit, the speed-up, the jet under crowns and the turbulence excess are weighted by
//   φ = Φ((y − y0)/σ) − Φ((y − y1)/σ), σ = 0.135·x + 1 m, y the cell's crosswind position and [y0, y1] the obstacle
//   group's crosswind extent (assumed: the wake's edges spread at K§6.7's σ_v/U), so a house's wake is 3-D and a
//   belt's ends and gaps fill in downstream.
// - A jet through a gap (obstacles on both sides within 16 m of a clear path): +0.3·(1 − β)·exp(−x/(5·w)) below the
//   sides' height, gone by 1.5 of it, w the gap's width (assumed: gaps run faster than the open field, Heisler and
//   DeWalle 1988's review, recalled).
// - The turbulence: σ/σ_open = 1 + A·φ·g(X)·v, g(X) = (X/X_p)²·exp(2·(1 − X/X_p)), one at its peak X_p; for a porous
//   obstacle X_p = 9 and A = 1.15·(1 − β)/(1 − 0.45), which makes the mean over 5–15 H 2.0 behind the fixture's belt of
//   porosity 0.45 (fitted: the shelterbelt literature's about 2× from 5 to 15 H, K§8.2) and falls with the obstacle's
//   deficit to none at β = 1 (derived, w7-farmland-wind 1.2: the wake's excess turbulence is made by its momentum
//   deficit, (1 − β) as Perera's; denser belts make more, Brandle et al. 2002, read; before 1.2 a crown's grazed edge,
//   β 0.99, boosted as much as the belt); for a bluff body X_p = 4 and A = 2.0 (assumed), blended by the recirculation's step;
//   v = 1 to η = 1.5 and 0 from η = 3 (smooth step), with η from the far wake's scale held at X ≥ 5 (assumed: the
//   wake 2–3 H tall, K§8.2). g(0) = 0: a quiet zone behind the obstacle and inside it (McNaughton 1988's quiet zone).
// - The approach roughness z0 = 0.03 m (published: Wieringa's class of open flat land with low crops, as stubble).
// Not modelled (task 1.3's handoff): the slow-down upwind of an obstacle, terrain (the wind field reads each point's
// height over its own ground, and HillFlow the hills).

using System;
using System.Threading.Tasks;

namespace Fpv450.Sim.World
{
    /// <summary>The wake at one point (WakeGrid.Sample).</summary>
    public struct WakeSample
    {
        /// <summary>The along-wind mean speed over the undisturbed mean at the same height: 1 in the open, below 1 in
        /// shelter, above 1 over a top or in a jet, below 0 in reverse flow.</summary>
        public double Speed;

        /// <summary>The turbulence's σ over the open field's σ at the same height: 1 in the open.</summary>
        public double Turbulence;

        /// <summary>The height of the obstacle whose wake this is, m (0 in the open): the scale of its eddies (K§8.2:
        /// the belt's eddies are the size of the trees).</summary>
        public double ObstacleHeight;
    }

    public sealed class WakeGrid
    {
        /// <summary>The version of the wake math: raised with every change that alters a grid for the same obstacles,
        /// logged beside the grid's hash (as WorldHash.QueryVersion is for the queries). 1: p7c-wind 1.3. 2: w7-farmland-wind
        /// 1.2, the obstacles' frontal porosity per lane and the overlying wake.</summary>
        public const int Version = 2;

        // ---- The labelled constants ----------------------------------------------------------------------------------

        /// <summary>The march's reach in obstacle heights and its cap, m (K§6.7).</summary>
        public const double ReachHeights = 30, ReachCapM = 300;

        /// <summary>The march's step, m (derived: half the 2 m cell, so no cell is stepped over).</summary>
        public const double MarchStep = 1.0;

        /// <summary>The side probes' reach, cells each side (assumed: 16 m, a field road's gap up to about 3 H).</summary>
        public const int SideCells = WakeObstacles.NearCells;

        /// <summary>A side obstacle counts only where its spread weight φ reaches this (assumed).</summary>
        public const double SideMinWeight = 0.1;

        /// <summary>Perera (1981): the amplitude, the exponent's factor, the approach profile's power n, von Kármán's κ.</summary>
        public const double PereraC = 9.75, PereraA = 0.67, PereraN = 0.14, Karman = 0.4;

        /// <summary>Where R peaks in η: (1/(1.5·0.67))^(2/3) (derived).</summary>
        public const double EtaPeak = 0.99667;

        /// <summary>The far wake's start in obstacle heights (published: the shelter model's lower bound).</summary>
        public const double FarStart = 5;

        /// <summary>The approach roughness length, m (published: open flat land with low crops).</summary>
        public const double ApproachZ0 = 0.03;

        /// <summary>The speed-up over the top: its amplitude, height (K§6.7), and widths in Z and X (assumed).</summary>
        public const double SpeedUpAmp = 0.2, SpeedUpZ = 1.2, SpeedUpSigmaZ = 0.3, SpeedUpSigmaX = 1.5;

        /// <summary>The jet under crowns: its amplitude and decay length in heights (assumed).</summary>
        public const double UnderJetAmp = 0.2, UnderJetDecay = 2;

        /// <summary>The shear layers' thickness at the base and the top, in heights (assumed).</summary>
        public const double ShearLayer = 0.1;

        /// <summary>Recirculation's porosity band: full at or below Low, none at or above High (K§6.7; Perera 1981).</summary>
        public const double BluffLow = 0.1, BluffHigh = 0.3;

        /// <summary>Fackrell (1984)'s reattachment coefficients: 1.8, the depth's power 0.3, the width's 0.24.</summary>
        public const double CavityA = 1.8, CavityDepthPower = 0.3, CavityWidthB = 0.24;

        /// <summary>The cavity's peak reverse flow over the local mean (assumed, after Martinuzzi and Tropea 1993).</summary>
        public const double ReverseFlow = 0.35;

        /// <summary>The lateral spread rate (K§6.7's σ_v/U, the middle of 0.12–0.15) and its start, m (assumed).</summary>
        public const double SpreadRate = 0.135, SpreadStartM = 1.0;

        /// <summary>The gap jet: its amplitude and decay in gap widths (assumed).</summary>
        public const double GapJetAmp = 0.3, GapJetDecayWidths = 5;

        /// <summary>The turbulence boost: the porous and bluff peaks X_p and amplitudes A (fitted / assumed), and the
        /// vertical fade in η (assumed).</summary>
        public const double TurbPeakPorous = 9, TurbAmpPorous = 1.15, TurbPeakBluff = 4, TurbAmpBluff = 2.0,
            TurbEtaFull = 1.5, TurbEtaGone = 3;

        /// <summary>The porosity the porous amplitude was fitted at: WakeGridFixture's belt of maple crowns (porosity 0.45).</summary>
        public const double TurbFitPorosity = 0.45;

        // ---- The grid --------------------------------------------------------------------------------------------------

        /// <summary>One cell's wake coefficients for this direction (H = 0: open).</summary>
        struct Cell
        {
            public double X, Beta, Phi;  // the inputs, kept for Describe: x/H, the path porosity, the spread weight
            public double H, InvH, ZBase;
            public double Bleed;        // (1 − β)·φ·(1 − w): the near wake's bleed deficit between base and top
            public double FarAmp;       // w·9.75·(1 − β)·φ / max(X, 5)
            public double InvS;         // 1 / (K·max(X, 5))^(1/(n+2))
            public double SpeedUp;      // 0.2·(1 − β)·φ·exp(−(X/1.5)²)
            public double UnderJet;     // 0.2·(1 − β)·φ·exp(−X/2), when the base is above the ground
            public double CavityZ, CavityRev, CavityW; // the cavity's top (Z), its reverse flow at the ground, its weight
            public double GapJet;       // the gap jet at this cell
            public double TurbAmp;      // A·φ·g(X)
        }

        public readonly WakeObstacles Obstacles;

        /// <summary>The bearing the wind blows from, degrees (0 from the north, 90 from the east), as WindField's.</summary>
        public double FromDeg { get; private set; }

        /// <summary>The upwind unit vector (towards the wind's source) and the crosswind one, world X and Z.</summary>
        public double UpX { get; private set; }
        public double UpZ { get; private set; }
        public double AcrossX { get; private set; }
        public double AcrossZ { get; private set; }

        // The cells (each one's nearest wake and its overlying wake), each group's crosswind extent for this direction, and
        // the lane's working memory: kept, so a rebuild (the worker's) allocates nothing.
        readonly Cell[] _cells, _over;
        readonly double[] _y0, _y1;
        readonly WakeObstacles.Lane _lane;

        WakeGrid(WakeObstacles obstacles)
        {
            Obstacles = obstacles;
            _cells = new Cell[obstacles.NX * obstacles.NZ];
            _over = new Cell[obstacles.NX * obstacles.NZ];
            _y0 = new double[obstacles.Groups];
            _y1 = new double[obstacles.Groups];
            _lane = obstacles.NewLane();
        }

        /// <summary>How many cells have a wake (H > 0) for this direction.</summary>
        public int WakeCells
        {
            get
            {
                int n = 0;
                for (int k = 0; k < _cells.Length; k++) n += _cells[k].H > 0 ? 1 : 0;
                return n;
            }
        }

        /// <summary>How many cells have an overlying wake for this direction.</summary>
        public int OverlyingCells
        {
            get
            {
                int n = 0;
                for (int k = 0; k < _over.Length; k++) n += _over[k].H > 0 ? 1 : 0;
                return n;
            }
        }

        // ---- Build -----------------------------------------------------------------------------------------------------

        /// <summary>One obstacle crossed by a path: its leeward edge's distance, height, base, depth, porosity, group, and
        /// the point its lane is centred on.</summary>
        struct Hit
        {
            public double X, H, B, L, Beta, PX, PZ;
            public int Group;
        }

        /// <summary>
        /// The wakes of every cell for wind from `fromDeg` (degrees), the cells' rows spread over the host's cores (load
        /// time: it allocates). Deterministic: each cell's coefficients are computed from the obstacles and the direction
        /// alone, by the same operations on any thread, so the grid's bits are Rebuild's (one thread). Call it before a
        /// flight (WakeGridCache); the worker's in-flight builds are Rebuild's.
        /// </summary>
        public static WakeGrid Build(WakeObstacles obstacles, double fromDeg)
        {
            if (obstacles == null) throw new ArgumentNullException(nameof(obstacles));
            var grid = new WakeGrid(obstacles);
            grid.Direction(fromDeg);
            int steps = grid.Extents();
            Parallel.For(0, obstacles.NZ, obstacles.NewLane, (j, _, lane) =>
            {
                for (int k = j * obstacles.NX, end = k + obstacles.NX; k < end; k++) grid.FillCell(k, steps, lane);
                return lane;
            }, grid.Release);
            return grid;
        }

        // A build's thread is done with its lane memory (Parallel.For's last call per thread): nothing to give back.
        void Release(WakeObstacles.Lane lane) { }

        /// <summary>A grid of these obstacles with nothing built (the worker's spare, which only a Rebuild fills before it
        /// is read): allocates.</summary>
        public static WakeGrid Spare(WakeObstacles obstacles) =>
            new WakeGrid(obstacles ?? throw new ArgumentNullException(nameof(obstacles)));

        /// <summary>A copy of this grid, bit for bit (WakeGridCache's): allocates.</summary>
        public WakeGrid Copy()
        {
            var copy = new WakeGrid(Obstacles);
            copy.Direction(FromDeg);
            Array.Copy(_cells, copy._cells, _cells.Length);
            Array.Copy(_over, copy._over, _over.Length);
            Array.Copy(_y0, copy._y0, _y0.Length);
            Array.Copy(_y1, copy._y1, _y1.Length);
            return copy;
        }

        /// <summary>Rebuilds this grid in place for another direction, on this thread, allocating nothing (the worker's
        /// spare grid). Never while another thread samples it.</summary>
        public void Rebuild(double fromDeg)
        {
            Direction(fromDeg);
            int steps = Extents();
            for (int k = 0, n = _cells.Length; k < n; k++) FillCell(k, steps, _lane);
        }

        /// <summary>
        /// A rebuild in place shared by threads (the worker's and its helpers', WakeGridWorker), allocating nothing: BeginRebuild
        /// sets the direction and the extents every cell reads and returns the march's steps; then each of the obstacles' NZ rows
        /// is filled once (FillRow), on any thread, each with its own lane (WakeObstacles.NewLane). Each cell is computed by the
        /// same operations from the obstacles and the direction alone, so the grid's bits are Rebuild's (as Build's).
        /// </summary>
        public int BeginRebuild(double fromDeg)
        {
            Direction(fromDeg);
            return Extents();
        }

        /// <summary>Row j's cells (BeginRebuild), with this thread's lane memory.</summary>
        public void FillRow(int j, int steps, WakeObstacles.Lane lane)
        {
            for (int k = j * Obstacles.NX, end = k + Obstacles.NX; k < end; k++) FillCell(k, steps, lane);
        }

        void Direction(double fromDeg)
        {
            FromDeg = fromDeg;
            DetMath.SinCos(fromDeg * 0.017453292519943295, out double sin, out double cos);
            UpX = sin;
            UpZ = cos;
            AcrossX = cos;
            AcrossZ = -sin;
        }

        // Clears the cells, sets each group's crosswind extent for the direction and returns the march's steps: the part
        // of a build every cell reads, before any cell is filled.
        int Extents()
        {
            WakeObstacles o = Obstacles;
            int nCells = o.NX * o.NZ;
            Array.Clear(_cells, 0, _cells.Length);
            Array.Clear(_over, 0, _over.Length);

            // Each group's crosswind extent for this direction: its cells' centres ± half a cell's projection.
            double[] y0 = _y0, y1 = _y1;
            for (int g = 0; g < o.Groups; g++) { y0[g] = double.PositiveInfinity; y1[g] = double.NegativeInfinity; }
            double halfCell = 0.5 * WakeObstacles.Cell * (DetMath.Abs(AcrossX) + DetMath.Abs(AcrossZ));
            for (int k = 0; k < nCells; k++)
            {
                int g = o.Group(k);
                if (g < 0) continue;
                o.Centre(k, out double cx, out double cz);
                double y = cx * AcrossX + cz * AcrossZ;
                if (y - halfCell < y0[g]) y0[g] = y - halfCell;
                if (y + halfCell > y1[g]) y1[g] = y + halfCell;
            }

            double reach = DetMath.Min(ReachCapM, ReachHeights * o.MaxTop);
            return (int)(reach / MarchStep);
        }

        // Cell k's coefficients: reads the obstacles, the direction and the extents, writes cell k alone (its nearest wake
        // and its overlying one), with this thread's lane memory.
        void FillCell(int k, int steps, WakeObstacles.Lane lane)
        {
            WakeObstacles o = Obstacles;
            double[] y0 = _y0, y1 = _y1;
            o.Centre(k, out double cx, out double cz);
            double yc = cx * AcrossX + cz * AcrossZ;
            if (o.IsObstacle(k))
            {
                // Inside: the lane from the obstacle's windward edge to this cell's centre, at X = 0, this cell's own top and
                // base; then the overlying wake past the obstacle's windward edge.
                Through(cx, cz, 0, out Hit inside);
                inside.X = 0;
                inside.H = o.Top(k);
                inside.B = o.Base(k);
                Porosity(ref inside, lane, 0);
                _cells[k] = Coefficients(inside, 1, 0, 0, Obstacles.Version);
                Overlying(k, cx, cz, yc, (int)(inside.L / MarchStep), inside.H, steps, lane);
                return;
            }

            Hit direct = default, side = default;
            bool haveDirect = false, haveSide = false;
            double sideWeight = 0, gapJet = 0;
            int stop = 0;
            for (int s = 1; s <= steps; s++)
            {
                double d = s * MarchStep, px = cx + d * UpX, pz = cz + d * UpZ;
                int q = o.CellAt(px, pz);
                if (q < 0) break; // beyond the grid: open
                if (o.IsObstacle(q))
                {
                    Through(px, pz, d - 0.5 * MarchStep, out Hit hit);
                    if (hit.X <= ReachHeights * hit.H && Porosity(ref hit, lane, double.NegativeInfinity))
                    {
                        direct = hit;
                        haveDirect = true;
                        stop = s + (int)(hit.L / MarchStep);
                        break;
                    }
                    s += (int)(hit.L / MarchStep); // past this obstacle: its wake does not reach, or its lane is clear
                    continue;
                }
                if ((s & 1) != 0 || !o.IsNear(q)) continue;

                // The side probes: the nearest obstacle cell to each side of the clear path at this distance.
                int left = Probe(px, pz, -1), right = Probe(px, pz, 1);
                if (left == 0 && right == 0) continue;
                double phiL = 0, phiR = 0;
                Hit hl = default, hr = default;
                if (left > 0)
                {
                    Through(px - left * WakeObstacles.Cell * AcrossX, pz - left * WakeObstacles.Cell * AcrossZ, d, out hl);
                    phiL = hl.X <= ReachHeights * hl.H ? Spread(yc, hl, y0, y1) : 0;
                }
                if (right > 0)
                {
                    Through(px + right * WakeObstacles.Cell * AcrossX, pz + right * WakeObstacles.Cell * AcrossZ, d, out hr);
                    phiR = hr.X <= ReachHeights * hr.H ? Spread(yc, hr, y0, y1) : 0;
                }
                bool gap = left > 0 && right > 0 && hl.X <= ReachHeights * hl.H && hr.X <= ReachHeights * hr.H;
                if (!gap && phiL < SideMinWeight && phiR < SideMinWeight) continue;
                bool useLeft = left > 0 && (right == 0 || phiL >= phiR);
                side = useLeft ? hl : hr;
                if (!Porosity(ref side, lane, double.NegativeInfinity)) continue; // its lane is clear: no obstacle there
                haveSide = true;
                stop = s + 1;
                sideWeight = DetMath.Min(1, phiL + phiR);
                if (gap)
                {
                    double width = (left + right - 1) * WakeObstacles.Cell;
                    gapJet = GapJetAmp * (1 - side.Beta) * WakeGridMath.Exp(-d / (GapJetDecayWidths * width));
                }
                break; // the nearest obstacle: a side one, met while the cell's own path is still clear
            }

            if (haveSide)
                _cells[k] = Coefficients(side, sideWeight, gapJet, 0, Obstacles.Version);
            else if (haveDirect)
                _cells[k] = Coefficients(direct, Spread(yc, direct, y0, y1), 0, Width(direct, y0, y1), Obstacles.Version);
            else return;
            Overlying(k, cx, cz, yc, stop, haveSide ? side.H : direct.H, steps, lane);
        }

        /// <summary>
        /// Cell k's overlying wake (see the header): on the cell's own path from step `start` on, the nearest obstacle
        /// taller than `below` m whose wake reaches the cell and whose lane is not clear.
        /// </summary>
        void Overlying(int k, double cx, double cz, double yc, int start, double below, int steps, WakeObstacles.Lane lane)
        {
            WakeObstacles o = Obstacles;
            if (o.Version == 1) return; // version 1: the nearest wake alone
            for (int s = start < 1 ? 1 : start; s <= steps; s++)
            {
                double d = s * MarchStep, px = cx + d * UpX, pz = cz + d * UpZ;
                int q = o.CellAt(px, pz);
                if (q < 0) return;
                if (!o.IsObstacle(q)) continue;
                Through(px, pz, d - 0.5 * MarchStep, out Hit hit);
                if (hit.H > below && hit.X <= ReachHeights * hit.H && Porosity(ref hit, lane, double.NegativeInfinity))
                {
                    _over[k] = Coefficients(hit, Spread(yc, hit, _y0, _y1), 0, Width(hit, _y0, _y1), Obstacles.Version);
                    return;
                }
                s += (int)(hit.L / MarchStep);
            }
        }

        /// <summary>Sets the hit's porosity β to its obstacle's frontal porosity across the lane centred on the hit's point
        /// (WakeObstacles.Transmission), chords from `from` m upwind of it; false when the lane is clear (β = 1).</summary>
        bool Porosity(ref Hit hit, WakeObstacles.Lane lane, double from)
        {
            if (Obstacles.Version == 1) return true; // version 1: the path's product of its cells' porosities (Through's)
            hit.Beta = Obstacles.Transmission(lane, hit.PX, hit.PZ, UpX, UpZ, AcrossX, AcrossZ, from, hit.B, hit.H);
            return hit.Beta < 1;
        }

        /// <summary>The obstacle's crosswind width, m (its group's extent).</summary>
        static double Width(in Hit hit, double[] y0, double[] y1) => y1[hit.Group] - y0[hit.Group];

        /// <summary>The lateral spread weight φ of an obstacle's wake at crosswind position y (see the header).</summary>
        static double Spread(double y, in Hit hit, double[] y0, double[] y1)
        {
            double sigma = SpreadRate * hit.X + SpreadStartM;
            return WakeGridMath.Phi((y - y0[hit.Group]) / sigma) - WakeGridMath.Phi((y - y1[hit.Group]) / sigma);
        }

        /// <summary>
        /// Marches upwind from a point inside an obstacle (its x from the sample being `x`) through it: the highest top,
        /// the lowest base and the depth; its porosity is Porosity's (β 1 until then). Hit.X is the distance x, in metres
        /// here (Coefficients takes it as metres too).
        /// </summary>
        void Through(double px, double pz, double x, out Hit hit)
        {
            WakeObstacles o = Obstacles;
            hit = new Hit { X = x, H = 0, B = double.PositiveInfinity, L = 0, Beta = 1, PX = px, PZ = pz, Group = -1 };
            for (int s = 0; ; s++)
            {
                double d = s * MarchStep;
                int q = o.CellAt(px + d * UpX, pz + d * UpZ);
                if (q < 0 || !o.IsObstacle(q)) break;
                if (hit.Group < 0) hit.Group = o.Group(q);
                if (o.Top(q) > hit.H) hit.H = o.Top(q);
                if (o.Base(q) < hit.B) hit.B = o.Base(q);
                if (o.Version == 1) hit.Beta *= DetMath.Sqrt(o.CellPorosity(q));
                hit.L += MarchStep;
            }
            if (hit.Group < 0) { hit.B = 0; return; }
        }

        /// <summary>The nearest obstacle cell beside a point, 1..SideCells cells along the crosswind axis (sign −1 or +1),
        /// or 0 for none.</summary>
        int Probe(double px, double pz, int sign)
        {
            WakeObstacles o = Obstacles;
            for (int j = 1; j <= SideCells; j++)
            {
                double a = sign * j * WakeObstacles.Cell;
                int q = o.CellAt(px + a * AcrossX, pz + a * AcrossZ);
                if (q < 0) break;
                if (o.IsObstacle(q)) return j;
            }
            return 0;
        }

        /// <summary>A cell's coefficients from its obstacle (x in metres), the spread weight, the gap jet and, for a
        /// direct hit, the obstacle's crosswind width (0: none).</summary>
        static Cell Coefficients(in Hit hit, double phi, double gapJet, double width, int version)
        {
            double h = hit.H;
            if (!(h > 0) || (!(phi > 0) && gapJet == 0)) return default;
            double x = hit.X / h, beta = hit.Beta, open = 1 - beta;
            double w = x < FarStart ? x / FarStart : 1, xf = x < FarStart ? FarStart : x;
            double kk = 2 * Karman * Karman / WakeGridMath.Ln(h / ApproachZ0);
            double s = WakeGridMath.Pow(kk * xf, 1 / (PereraN + 2));
            double bluff = WakeGridMath.Smooth((BluffHigh - beta) / (BluffHigh - BluffLow));
            var c = new Cell
            {
                X = x,
                Beta = beta,
                Phi = phi,
                H = h,
                InvH = 1 / h,
                ZBase = hit.B / h,
                Bleed = open * phi * (1 - w),
                FarAmp = w * PereraC * open * phi / xf,
                InvS = 1 / s,
                SpeedUp = SpeedUpAmp * open * phi * WakeGridMath.Exp(-(x / SpeedUpSigmaX) * (x / SpeedUpSigmaX)),
                UnderJet = hit.B > 0 ? UnderJetAmp * open * phi * WakeGridMath.Exp(-x / UnderJetDecay) : 0,
                GapJet = gapJet,
            };
            double xp = TurbPeakPorous + bluff * (TurbPeakBluff - TurbPeakPorous);
            double porous = version == 1 ? TurbAmpPorous : TurbAmpPorous * open / (1 - TurbFitPorosity);
            double amp = porous + bluff * (TurbAmpBluff - porous);
            double r = x / xp;
            c.TurbAmp = amp * phi * r * r * WakeGridMath.Exp(2 * (1 - r));
            if (bluff > 0 && width > 0)
            {
                double wh = width / h, lh = hit.L / h;
                double xr = CavityA * wh / (WakeGridMath.Pow(lh, CavityDepthPower) * (1 + CavityWidthB * wh));
                if (x < xr)
                {
                    double xi = x / xr;
                    c.CavityZ = DetMath.Sqrt(1 - xi * xi);
                    c.CavityRev = ReverseFlow * 4 * xi * (1 - xi);
                    c.CavityW = bluff * phi;
                }
            }
            return c;
        }

        /// <summary>Fackrell (1984)'s reattachment, in obstacle heights, for a body of crosswind width W and depth L, m.</summary>
        public static double Reattachment(double height, double width, double depth) =>
            CavityA * (width / height) / (WakeGridMath.Pow(depth / height, CavityDepthPower) * (1 + CavityWidthB * width / height));

        // ---- Sample ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// The wake at world (x, z) and a height above the ground (m): the four nearest cell centres' wakes at that height,
        /// interpolated bilinearly (outside the grid: open). Allocation-free, reentrant, deterministic.
        /// </summary>
        public void Sample(double x, double z, double heightAboveGround, out WakeSample sample)
        {
            WakeObstacles o = Obstacles;
            double fx = (x - o.OriginX) / WakeObstacles.Cell - 0.5, fz = (z - o.OriginZ) / WakeObstacles.Cell - 0.5;
            int i = WakeObstacles.Floor(fx), j = WakeObstacles.Floor(fz);
            double tx = fx - i, tz = fz - j;
            Corner(i, j, heightAboveGround, out double s00, out double t00, out double h00);
            Corner(i + 1, j, heightAboveGround, out double s10, out double t10, out double h10);
            Corner(i, j + 1, heightAboveGround, out double s01, out double t01, out double h01);
            Corner(i + 1, j + 1, heightAboveGround, out double s11, out double t11, out double h11);
            double a = (1 - tx) * (1 - tz), b = tx * (1 - tz), c = (1 - tx) * tz, d = tx * tz;
            sample.Speed = a * s00 + b * s10 + c * s01 + d * s11;
            sample.Turbulence = a * t00 + b * t10 + c * t01 + d * t11;
            double hm = h00;
            if (h10 > hm) hm = h10;
            if (h01 > hm) hm = h01;
            if (h11 > hm) hm = h11;
            sample.ObstacleHeight = hm;
        }

        // A cell centre's wake at a height: its nearest wake, and over it its overlying wake (the header's composition).
        void Corner(int i, int j, double height, out double speed, out double turbulence, out double obstacle)
        {
            WakeObstacles o = Obstacles;
            if (i < 0 || j < 0 || i >= o.NX || j >= o.NZ) { speed = 1; turbulence = 1; obstacle = 0; return; }
            int k = j * o.NX + i;
            Evaluate(_cells[k], height, out speed, out turbulence);
            obstacle = _cells[k].H;
            if (!(_over[k].H > 0)) return;
            Evaluate(_over[k], height, out double over, out double t2);
            speed = over > 0 ? speed * over : over;
            double e1 = turbulence * turbulence - 1, e2 = t2 * t2 - 1;
            turbulence = DetMath.Sqrt(1 + e1 + e2);
            if (e2 > e1) obstacle = _over[k].H;
        }

        static void Evaluate(in Cell c, double height, out double speed, out double turbulence)
        {
            if (!(c.H > 0)) { speed = 1; turbulence = 1; return; }
            double z = height * c.InvH;
            double eta = z * c.InvS;
            double etaR = eta < EtaPeak ? EtaPeak : eta;
            double far = c.FarAmp * etaR * WakeGridMath.Exp(-PereraA * etaR * DetMath.Sqrt(etaR));
            double inBase = c.ZBase > 0 ? WakeGridMath.Smooth((z - c.ZBase) / ShearLayer + 0.5) : 1;
            double belowTop = 1 - WakeGridMath.Smooth((z - 1) / ShearLayer + 0.5);
            double u = 1 - c.Bleed * inBase * belowTop - far;
            double dz = (z - SpeedUpZ) / SpeedUpSigmaZ;
            u += c.SpeedUp * WakeGridMath.Exp(-dz * dz);
            u += c.UnderJet * (1 - inBase);
            if (z < c.CavityZ) u += c.CavityW * (-c.CavityRev * (1 - z / c.CavityZ) - u);
            if (c.GapJet != 0) u += c.GapJet * (1 - WakeGridMath.Smooth((z - 1) * 2));
            speed = u;
            double et = eta < 0 ? 0 : eta;
            turbulence = 1 + c.TurbAmp * (1 - WakeGridMath.Smooth((et - TurbEtaFull) / (TurbEtaGone - TurbEtaFull)));
        }

        /// <summary>The obstacle behind cell (i, j)'s wake for this direction (for tests, the bench and the field's
        /// review): x/H, H (m, 0 in the open), B/H, β, φ, the gap jet, and the cavity's top at the face (0: none).</summary>
        public void Describe(int i, int j, out double x, out double height, out double baseZ, out double beta, out double phi,
            out double gapJet, out double cavityZ)
        {
            Cell c = _cells[j * Obstacles.NX + i];
            x = c.X;
            height = c.H;
            baseZ = c.ZBase;
            beta = c.Beta;
            phi = c.Phi;
            gapJet = c.GapJet;
            cavityZ = c.CavityZ;
        }

        /// <summary>The overlying obstacle of cell (i, j) for this direction (for tests and the bench): x/H, H (m, 0 for none),
        /// β and φ.</summary>
        public void DescribeOver(int i, int j, out double x, out double height, out double beta, out double phi)
        {
            Cell c = _over[j * Obstacles.NX + i];
            x = c.X;
            height = c.H;
            beta = c.Beta;
            phi = c.Phi;
        }

        /// <summary>The wake of cell (i, j)'s centre at a height (no interpolation): for tests and the bench.</summary>
        public void SampleCell(int i, int j, double heightAboveGround, out WakeSample sample)
        {
            Corner(i, j, heightAboveGround, out sample.Speed, out sample.Turbulence, out sample.ObstacleHeight);
        }

        /// <summary>The nearest wake alone at cell (i, j)'s centre, without the overlying one (version 1's rule): the known-wrong
        /// of the farmland's shelter rows, for tests and the bench.</summary>
        public void SampleNearest(int i, int j, double heightAboveGround, out WakeSample sample)
        {
            WakeObstacles o = Obstacles;
            if (i < 0 || j < 0 || i >= o.NX || j >= o.NZ) { sample = new WakeSample { Speed = 1, Turbulence = 1 }; return; }
            Evaluate(_cells[j * o.NX + i], heightAboveGround, out sample.Speed, out sample.Turbulence);
            sample.ObstacleHeight = _cells[j * o.NX + i].H;
        }

        /// <summary>The FNV-1a 64 hash of every cell's coefficients' bits, in cell order (each cell's nearest wake, then its
        /// overlying one): the grid's identity.</summary>
        public ulong Hash()
        {
            ulong hash = WorldHash.Offset;
            for (int k = 0; k < _cells.Length; k++)
            {
                hash = Mix(hash, _cells[k]);
                if (Obstacles.Version > 1) hash = Mix(hash, _over[k]);
            }
            return hash;
        }

        static ulong Mix(ulong hash, in Cell c)
        {
            hash = Mix(hash, c.X); hash = Mix(hash, c.Beta); hash = Mix(hash, c.Phi); hash = Mix(hash, c.H); hash = Mix(hash, c.InvH); hash = Mix(hash, c.ZBase); hash = Mix(hash, c.Bleed);
            hash = Mix(hash, c.FarAmp); hash = Mix(hash, c.InvS); hash = Mix(hash, c.SpeedUp); hash = Mix(hash, c.UnderJet);
            hash = Mix(hash, c.CavityZ); hash = Mix(hash, c.CavityRev); hash = Mix(hash, c.CavityW);
            hash = Mix(hash, c.GapJet); hash = Mix(hash, c.TurbAmp);
            return hash;
        }

        static ulong Mix(ulong hash, double v)
        {
            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(v);
            for (int b = 0; b < 8; b++) hash = (hash ^ (byte)(bits >> 8 * b)) * 0x100000001B3UL;
            return hash;
        }
    }
}
