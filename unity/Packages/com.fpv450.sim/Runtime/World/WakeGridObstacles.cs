// WakeObstacles: the wind-obstacle grid of K§10.3, the wake grid's input (p7c-wind 1.3, design 4; w7-farmland-wind 1.2).
// - On the 2 m grid, per cell: TopM and BaseM (heights above the local ground, m), the highest top and the lowest base of
//   the placed wind volumes (an asset's `wind_volumes` records, placed) whose footprint overlaps the cell, and the list
//   of those volumes. Porosity 1 is no obstacle (K§10.3).
// - A volume overlaps a cell when its footprint and the cell share an area (exact: a box by the separating axes, a
//   cylinder by its distance to the square), so a part thinner than any sampling (a 0.24 m wall) is never lost.
// - The obstacle's porosity is the wake grid's to ask, per lane and direction (Transmission): the optical porosity of
//   the obstacle's frontal area across a 2 m lane, from its volumes' own geometry. That is K§10.3's "optical porosity
//   of a horizontal path" made exact (w7-farmland-wind 1.2, design 4): a solid part is opaque however thin, a porous
//   one passes β^(chord / D) (Beer–Lambert over the chord, D = footprint perimeter / π, the mean width its porosity was
//   measured over: tools/blender/lib/plant.py), parts at the same height multiply and parts at different heights
//   stand side by side. Version 1's rule (a cell's covered fraction f on 4 × 4 samples, β_cell = 1 − f·(1 −
//   β^(2 m / D)), multiplied over overlaps) diluted a thin solid wall to f, missed one between the samples, and
//   multiplied a wall's stacked courses and a trunk under its crown as if they stood in series; it is kept bit for bit
//   (the constructor's version 1: WakeLayout.Version) for its logs and the kept audits of older models.
// - Obstacle cells (a top above 0) are grouped into 4-connected groups in row-major order (an iterative fill), so the
//   wake grid knows each obstacle's crosswind width per direction (the bluff-body cavity, the wake's lateral spread).
// Upright volumes only: a box turns about the vertical (its yaw), a cylinder stands upright; the stand-ins' volumes are
// all upright but for two small parts of the cellar head (task 1.3's handoff names it).
// Frame (D-019): +X east, +Z north; cell (i, j) spans X0 + i·2 m .. + 2 m and Z0 + j·2 m .. + 2 m.

using System;

namespace Fpv450.Sim.World
{
    public enum WindVolumeKind : byte { Box = 0, Cylinder = 1 }

    /// <summary>One placed wind volume (K§10.2's `wind_volume` with its porosity), in world metres.</summary>
    public struct WindVolume
    {
        public WindVolumeKind Kind;

        /// <summary>The footprint's centre, world X (east) and Z (north), m.</summary>
        public double X, Z;

        /// <summary>The volume's bottom and top above the local ground, m.</summary>
        public double BaseM, TopM;

        /// <summary>A box's half extents along its own x and z axes, m.</summary>
        public double HalfX, HalfZ;

        /// <summary>A cylinder's radius, m.</summary>
        public double Radius;

        /// <summary>A box's yaw, rad (D-019: positive turns north towards east).</summary>
        public double Yaw;

        /// <summary>The volume's optical porosity, 0 (solid) to 1 (open).</summary>
        public double Porosity;

        /// <summary>
        /// An asset's upright wind volume (its local centre and size, as `.assetmeta.json` holds them) placed at a world
        /// position with a yaw (D-019) and a uniform scale; the placement's ground is the volumes' ground.
        /// </summary>
        public static WindVolume Placed(WindVolumeKind kind, Double3 localCenter, double sizeX, double sizeY, double sizeZ,
            double radius, double porosity, double placeX, double placeZ, double placeYaw, double scale)
        {
            DetMath.SinCos(placeYaw, out double sin, out double cos);
            double lx = localCenter.X * scale, lz = localCenter.Z * scale;
            double halfY = sizeY * scale * 0.5;
            return new WindVolume
            {
                Kind = kind,
                X = placeX + lx * cos + lz * sin,
                Z = placeZ - lx * sin + lz * cos,
                BaseM = localCenter.Y * scale - halfY,
                TopM = localCenter.Y * scale + halfY,
                HalfX = sizeX * scale * 0.5,
                HalfZ = sizeZ * scale * 0.5,
                Radius = radius * scale,
                Yaw = placeYaw,
                Porosity = porosity,
            };
        }
    }

    public sealed class WakeObstacles
    {
        /// <summary>The grid's cell, m (K§6.7, K§10.3).</summary>
        public const double Cell = 2.0;

        /// <summary>The lines across a lane that an obstacle's frontal porosity is averaged over (numerical: lines 0.25 m
        /// apart, so a wall 0.24 m thick along the wind is seen about as often as it blocks). Over the height the average
        /// is exact, between the parts' own bases and tops.</summary>
        public const int LaneLines = 8;

        public readonly double OriginX, OriginZ;
        public readonly int NX, NZ;

        /// <summary>The number of obstacle groups; group ids run 0 .. Groups − 1, −1 for an open cell.</summary>
        public readonly int Groups;

        /// <summary>The tallest top in the grid, m.</summary>
        public readonly double MaxTop;

        readonly double[] _top, _base;
        readonly int[] _group;
        readonly bool[] _near; // within NearCells (Chebyshev) of an obstacle cell

        // The obstacles' volumes (porosity below 1), each one's yaw's sine and cosine, ln β (−∞ for a solid) over its D,
        // and each cell's list of the volumes overlapping it: cell k's are _list[_first[k] .. _first[k + 1]).
        readonly WindVolume[] _volumes;
        readonly double[] _sin, _cos, _lnBetaOverD;
        readonly int[] _first, _list;

        // Version 1's grid (WakeLayout.Version 1): each cell's 2 m path porosity by K§10.3's covered fraction.
        readonly double[] _porosity;

        /// <summary>How far, in cells, the near mask reaches around obstacle cells (the wake grid's side probes).</summary>
        public const int NearCells = 8;

        /// <summary>The wake grid's rules the grid is built for (WakeLayout.Version): WakeGrid.Version, or 1.</summary>
        public readonly int Version;

        public WakeObstacles(double originX, double originZ, int nx, int nz, ReadOnlySpan<WindVolume> volumes, int version = WakeGrid.Version)
        {
            if (nx < 1 || nz < 1) throw new ArgumentException("the grid needs at least one cell");
            if (version < 1 || version > WakeGrid.Version) throw new ArgumentException("no wake grid has version " + version);
            OriginX = originX;
            OriginZ = originZ;
            NX = nx;
            NZ = nz;
            Version = version;
            int n = nx * nz;
            _top = new double[n];
            _base = new double[n];
            if (version == 1)
            {
                _porosity = Version1(volumes);
                _volumes = Array.Empty<WindVolume>();
                _sin = _cos = _lnBetaOverD = Array.Empty<double>();
                _first = new int[n + 1];
                _list = Array.Empty<int>();
                MaxTop = Grouped(out Groups, out _group, out _near);
                return;
            }
            for (int k = 0; k < n; k++) _base[k] = double.PositiveInfinity;

            // The obstacles' volumes, in the layout's order.
            int count = 0;
            for (int v = 0; v < volumes.Length; v++)
            {
                WindVolume w = volumes[v];
                if (!(w.Porosity >= 0 && w.Porosity <= 1) || !(w.TopM > w.BaseM) || !(w.BaseM >= 0))
                    throw new ArgumentException("wind volume " + v + " is out of its bounds");
                if (w.Kind == WindVolumeKind.Cylinder ? !(w.Radius > 0) : !(w.HalfX > 0 && w.HalfZ > 0))
                    throw new ArgumentException("wind volume " + v + " has no size");
                if (w.Porosity < 1) count++; // K§10.3: porosity 1 is no obstacle
            }
            _volumes = new WindVolume[count];
            _sin = new double[count];
            _cos = new double[count];
            _lnBetaOverD = new double[count];
            count = 0;
            for (int v = 0; v < volumes.Length; v++)
            {
                WindVolume w = volumes[v];
                if (!(w.Porosity < 1)) continue;
                DetMath.SinCos(w.Yaw, out _sin[count], out _cos[count]);
                double perimeter = w.Kind == WindVolumeKind.Cylinder ? 2 * 3.141592653589793 * w.Radius : 4 * (w.HalfX + w.HalfZ);
                _lnBetaOverD[count] = w.Porosity > 0 ? WakeGridMath.Ln(w.Porosity) / (perimeter / 3.141592653589793) : double.NegativeInfinity;
                _volumes[count++] = w;
            }

            // Each cell's volumes: counted, then listed, in the volumes' order.
            _first = new int[n + 1];
            for (int pass = 0; pass < 2; pass++)
            {
                int[] at = pass == 0 ? null : (int[])_first.Clone();
                for (int v = 0; v < _volumes.Length; v++)
                {
                    WindVolume w = _volumes[v];
                    double reachX = w.Kind == WindVolumeKind.Cylinder ? w.Radius : DetMath.Abs(w.HalfX * _cos[v]) + DetMath.Abs(w.HalfZ * _sin[v]);
                    double reachZ = w.Kind == WindVolumeKind.Cylinder ? w.Radius : DetMath.Abs(w.HalfX * _sin[v]) + DetMath.Abs(w.HalfZ * _cos[v]);
                    int i0 = Floor((w.X - reachX - originX) / Cell), i1 = Floor((w.X + reachX - originX) / Cell);
                    int j0 = Floor((w.Z - reachZ - originZ) / Cell), j1 = Floor((w.Z + reachZ - originZ) / Cell);
                    if (i0 < 0) i0 = 0;
                    if (j0 < 0) j0 = 0;
                    if (i1 > nx - 1) i1 = nx - 1;
                    if (j1 > nz - 1) j1 = nz - 1;
                    for (int j = j0; j <= j1; j++)
                        for (int i = i0; i <= i1; i++)
                        {
                            if (!Overlaps(v, originX + (i + 0.5) * Cell, originZ + (j + 0.5) * Cell)) continue;
                            int k = j * nx + i;
                            if (pass == 0)
                            {
                                _first[k + 1]++;
                                if (w.TopM > _top[k]) _top[k] = w.TopM;
                                if (w.BaseM < _base[k]) _base[k] = w.BaseM;
                            }
                            else _list[at[k]++] = v;
                        }
                }
                if (pass == 0)
                {
                    for (int k = 0; k < n; k++) _first[k + 1] += _first[k];
                    _list = new int[_first[n]];
                }
            }

            MaxTop = Grouped(out Groups, out _group, out _near);
            _porosity = null;
        }

        // Version 1's cells (the rule before w7-farmland-wind 1.2, kept for its logs and the kept audits): per volume, f its
        // covered fraction of the cell on 4 × 4 samples and D = footprint perimeter / π, β_cell = Π 1 − f·(1 − β^(2 m / D)) over
        // the volumes covering it, its top the highest and its base the lowest of theirs.
        double[] Version1(ReadOnlySpan<WindVolume> volumes)
        {
            int nx = NX, nz = NZ, n = nx * nz;
            double originX = OriginX, originZ = OriginZ;
            var porosity = new double[n];
            for (int k = 0; k < n; k++) { porosity[k] = 1; _base[k] = double.PositiveInfinity; }
            for (int v = 0; v < volumes.Length; v++)
            {
                WindVolume w = volumes[v];
                if (!(w.Porosity >= 0 && w.Porosity <= 1) || !(w.TopM > w.BaseM) || !(w.BaseM >= 0))
                    throw new ArgumentException("wind volume " + v + " is out of its bounds");
                if (w.Porosity == 1) continue; // K§10.3: porosity 1 is no obstacle
                double reachX, reachZ, perimeter;
                DetMath.SinCos(w.Yaw, out double sin, out double cos);
                if (w.Kind == WindVolumeKind.Cylinder)
                {
                    if (!(w.Radius > 0)) throw new ArgumentException("wind volume " + v + " has no radius");
                    reachX = reachZ = w.Radius;
                    perimeter = 2 * 3.141592653589793 * w.Radius;
                }
                else
                {
                    if (!(w.HalfX > 0 && w.HalfZ > 0)) throw new ArgumentException("wind volume " + v + " has no size");
                    reachX = DetMath.Abs(w.HalfX * cos) + DetMath.Abs(w.HalfZ * sin);
                    reachZ = DetMath.Abs(w.HalfX * sin) + DetMath.Abs(w.HalfZ * cos);
                    perimeter = 4 * (w.HalfX + w.HalfZ);
                }
                double d = perimeter / 3.141592653589793;
                double cellBeta = WakeGridMath.Pow(w.Porosity, Cell / d); // β_obj^(2 m / D); 0 for a solid volume
                int i0 = Floor((w.X - reachX - originX) / Cell), i1 = Floor((w.X + reachX - originX) / Cell);
                int j0 = Floor((w.Z - reachZ - originZ) / Cell), j1 = Floor((w.Z + reachZ - originZ) / Cell);
                if (i0 < 0) i0 = 0;
                if (j0 < 0) j0 = 0;
                if (i1 > nx - 1) i1 = nx - 1;
                if (j1 > nz - 1) j1 = nz - 1;
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        int inside = 0;
                        for (int b = 0; b < 4; b++)
                            for (int a = 0; a < 4; a++)
                            {
                                double px = originX + (i + (a + 0.5) / 4) * Cell - w.X;
                                double pz = originZ + (j + (b + 0.5) / 4) * Cell - w.Z;
                                if (w.Kind == WindVolumeKind.Cylinder)
                                {
                                    if (px * px + pz * pz <= w.Radius * w.Radius) inside++;
                                }
                                else
                                {
                                    double lx = px * cos - pz * sin, lz = px * sin + pz * cos;
                                    if (DetMath.Abs(lx) <= w.HalfX && DetMath.Abs(lz) <= w.HalfZ) inside++;
                                }
                            }
                        if (inside == 0) continue;
                        double f = inside / (double)(4 * 4);
                        int k = j * nx + i;
                        porosity[k] *= 1 - f * (1 - cellBeta);
                        if (w.TopM > _top[k]) _top[k] = w.TopM;
                        if (w.BaseM < _base[k]) _base[k] = w.BaseM;
                    }
            }
            return porosity;
        }

        // The tallest top (an open cell's base set to 0), the 4-connected groups of obstacle cells in row-major order (an
        // iterative fill), and the near mask: cells within NearCells of an obstacle cell (the side probes run only there).
        double Grouped(out int groupCount, out int[] groupOf, out bool[] nearMask)
        {
            int nx = NX, nz = NZ, n = nx * nz;
            double maxTop = 0;
            for (int k = 0; k < n; k++)
            {
                if (_top[k] > maxTop) maxTop = _top[k];
                if (!(_top[k] > 0)) _base[k] = 0;
            }

            // Groups: 4-connected obstacle cells, labelled in row-major order by an iterative fill.
            var group = new int[n];
            for (int k = 0; k < n; k++) group[k] = -1;
            var stack = new int[n];
            int groups = 0;
            for (int k0 = 0; k0 < n; k0++)
            {
                if (group[k0] >= 0 || !(_top[k0] > 0)) continue;
                int top = 0;
                stack[top++] = k0;
                group[k0] = groups;
                while (top > 0)
                {
                    int k = stack[--top], i = k % nx, j = k / nx;
                    if (i > 0) Push(k - 1);
                    if (i < nx - 1) Push(k + 1);
                    if (j > 0) Push(k - nx);
                    if (j < nz - 1) Push(k + nx);
                }
                groups++;

                void Push(int q)
                {
                    if (group[q] >= 0 || !(_top[q] > 0)) return;
                    group[q] = groups;
                    stack[top++] = q;
                }
            }

            // The near mask: cells within NearCells of an obstacle cell (the side probes run only there).
            var near = new bool[n];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (!(_top[j * nx + i] > 0)) continue;
                    int a0 = i > NearCells ? i - NearCells : 0, a1 = i + NearCells < nx ? i + NearCells : nx - 1;
                    int b0 = j > NearCells ? j - NearCells : 0, b1 = j + NearCells < nz ? j + NearCells : nz - 1;
                    for (int b = b0; b <= b1; b++)
                        for (int a = a0; a <= a1; a++)
                            near[b * nx + a] = true;
                }
            groupCount = groups;
            groupOf = group;
            nearMask = near;
            return maxTop;
        }

        /// <summary>The largest integer at or below x (|x| below 2^31), by a cast and one comparison.</summary>
        internal static int Floor(double x) { int f = (int)x; return f > x ? f - 1 : f; }

        /// <summary>The cell holding a world point, or −1 outside the grid.</summary>
        public int CellAt(double x, double z)
        {
            double fx = (x - OriginX) / Cell, fz = (z - OriginZ) / Cell;
            if (!(fx >= 0 && fz >= 0 && fx < NX && fz < NZ)) return -1;
            return (int)fz * NX + (int)fx;
        }

        /// <summary>Cell k's top above the ground, m (0: open).</summary>
        public double Top(int k) => _top[k];

        /// <summary>Cell k's base above the ground, m (0 for an open cell).</summary>
        public double Base(int k) => _base[k];

        /// <summary>Version 1's 2 m path porosity of cell k (1: open).</summary>
        internal double CellPorosity(int k) => _porosity[k];

        /// <summary>Whether volume v's footprint shares an area with the cell centred at (cx, cz): a cylinder by its distance
        /// to the square, a box by the separating axes (the world's and its own).</summary>
        bool Overlaps(int v, double cx, double cz)
        {
            WindVolume w = _volumes[v];
            const double h = Cell / 2;
            double dx = cx - w.X, dz = cz - w.Z;
            if (w.Kind == WindVolumeKind.Cylinder)
            {
                double ex = DetMath.Max(DetMath.Abs(dx) - h, 0), ez = DetMath.Max(DetMath.Abs(dz) - h, 0);
                return ex * ex + ez * ez < w.Radius * w.Radius;
            }
            double sin = _sin[v], cos = _cos[v];
            if (!(DetMath.Abs(dx) < h + DetMath.Abs(w.HalfX * cos) + DetMath.Abs(w.HalfZ * sin))) return false;
            if (!(DetMath.Abs(dz) < h + DetMath.Abs(w.HalfX * sin) + DetMath.Abs(w.HalfZ * cos))) return false;
            double r = h * (DetMath.Abs(cos) + DetMath.Abs(sin)); // the square's half width along either of the box's axes
            return DetMath.Abs(dx * cos - dz * sin) < w.HalfX + r && DetMath.Abs(dx * sin + dz * cos) < w.HalfZ + r;
        }

        /// <summary>One thread's working memory for Transmission (allocates; a grid's rebuild reuses its own).</summary>
        public sealed class Lane
        {
            internal readonly int[] Stamp, Picked;
            internal readonly double[] Lo, Hi, Ln, Cuts;
            internal int Mark, Count;

            internal Lane(int volumes)
            {
                Stamp = new int[volumes];
                Picked = new int[volumes];
                Lo = new double[volumes];
                Hi = new double[volumes];
                Ln = new double[volumes];
                Cuts = new double[2 * volumes + 2];
            }
        }

        /// <summary>A working memory for Transmission on one thread: allocates.</summary>
        public Lane NewLane() => new Lane(_volumes.Length);

        /// <summary>
        /// The frontal porosity of the obstacle met at (px, pz) along a lane, 0 (opaque) to 1 (nothing on the lane): the
        /// volumes of the obstacle cells from there upwind to the obstacle's windward edge (the run the wake grid crosses),
        /// seen along the wind (upX, upZ: towards the wind's source; acrossX, acrossZ the lane's crosswind axis) through a
        /// lane Cell wide centred on (px, pz), averaged over LaneLines lines across it and over the height from bottom to
        /// top. On each line at each height the volumes it crosses there multiply: a solid one passes nothing, a porous one
        /// β^(chord / D). A chord counts from `from` m upwind of (px, pz) (−∞: all of it). Allocation-free with a
        /// caller's Lane; deterministic (the volumes in each cell's order, the lines and heights in theirs).
        /// </summary>
        public double Transmission(Lane lane, double px, double pz, double upX, double upZ, double acrossX, double acrossZ,
            double from, double bottom, double top)
        {
            // The run's volumes, each once.
            if (++lane.Mark == int.MaxValue) { Array.Clear(lane.Stamp, 0, lane.Stamp.Length); lane.Mark = 1; }
            lane.Count = 0;
            for (int s = 0; ; s++)
            {
                int q = CellAt(px + s * upX, pz + s * upZ);
                if (q < 0 || !IsObstacle(q)) break;
                for (int e = _first[q]; e < _first[q + 1]; e++)
                {
                    int v = _list[e];
                    if (lane.Stamp[v] == lane.Mark) continue;
                    lane.Stamp[v] = lane.Mark;
                    lane.Picked[lane.Count++] = v;
                }
            }

            double sum = 0;
            for (int l = 0; l < LaneLines; l++)
            {
                double offset = ((l + 0.5) / LaneLines - 0.5) * Cell;
                double ox = px + offset * acrossX, oz = pz + offset * acrossZ;

                // The volumes this line crosses: their heights within [bottom, top] and ln of what they pass (−∞: solid),
                // and the heights where that changes, sorted (insertion: a few per line).
                int m = 0, cuts = 0;
                lane.Cuts[cuts++] = bottom;
                lane.Cuts[cuts++] = top;
                for (int p = 0; p < lane.Count; p++)
                {
                    int v = lane.Picked[p];
                    double chord = Chord(v, ox, oz, upX, upZ, acrossX, acrossZ, from);
                    if (!(chord > 0)) continue;
                    WindVolume w = _volumes[v];
                    double lo = DetMath.Max(w.BaseM, bottom), hi = DetMath.Min(w.TopM, top);
                    if (!(hi > lo)) continue;
                    lane.Lo[m] = lo;
                    lane.Hi[m] = hi;
                    lane.Ln[m++] = chord * _lnBetaOverD[v];
                    lane.Cuts[cuts++] = lo;
                    lane.Cuts[cuts++] = hi;
                }
                for (int i = 1; i < cuts; i++)
                {
                    double c = lane.Cuts[i];
                    int j = i - 1;
                    while (j >= 0 && lane.Cuts[j] > c) { lane.Cuts[j + 1] = lane.Cuts[j]; j--; }
                    lane.Cuts[j + 1] = c;
                }

                // Between consecutive heights the line passes the product of what the volumes there pass.
                double line = 0, lastLn = 0, lastT = 1;
                for (int i = 0; i + 1 < cuts; i++)
                {
                    double z0 = lane.Cuts[i], z1 = lane.Cuts[i + 1];
                    if (!(z1 > z0)) continue;
                    double mid = 0.5 * (z0 + z1), ln = 0;
                    for (int e = 0; e < m; e++)
                        if (lane.Lo[e] <= mid && mid < lane.Hi[e]) ln += lane.Ln[e];
                    if (double.IsNegativeInfinity(ln)) continue;
                    if (!(ln == lastLn)) { lastT = WakeGridMath.Exp(ln); lastLn = ln; }
                    line += lastT * (z1 - z0);
                }
                sum += line / (top - bottom);
            }
            return sum / LaneLines;
        }

        // The length of the line o + s·up, s ≥ from, inside volume v's footprint (0 when it misses).
        double Chord(int v, double ox, double oz, double upX, double upZ, double acrossX, double acrossZ, double from)
        {
            WindVolume w = _volumes[v];
            double dx = w.X - ox, dz = w.Z - oz, s0, s1;
            if (w.Kind == WindVolumeKind.Cylinder)
            {
                double off = dx * acrossX + dz * acrossZ, h2 = w.Radius * w.Radius - off * off;
                if (!(h2 > 0)) return 0;
                double mid = dx * upX + dz * upZ, half = DetMath.Sqrt(h2);
                s0 = mid - half;
                s1 = mid + half;
            }
            else
            {
                // In the box's own axes (x along (cos, −sin), z along (sin, cos)): the line's start and direction.
                double sin = _sin[v], cos = _cos[v];
                double lx = -(dx * cos - dz * sin), lz = -(dx * sin + dz * cos), ux = upX * cos - upZ * sin, uz = upX * sin + upZ * cos;
                s0 = double.NegativeInfinity;
                s1 = double.PositiveInfinity;
                if (!Slab(lx, ux, w.HalfX, ref s0, ref s1) || !Slab(lz, uz, w.HalfZ, ref s0, ref s1)) return 0;
            }
            if (s0 < from) s0 = from;
            return s1 > s0 ? s1 - s0 : 0;
        }

        // Narrows [s0, s1] to where o + s·u lies within ±half on one axis; false when the line misses the slab.
        static bool Slab(double o, double u, double half, ref double s0, ref double s1)
        {
            if (u == 0) return DetMath.Abs(o) < half;
            double a = (-half - o) / u, b = (half - o) / u;
            if (a > b) { double t = a; a = b; b = t; }
            if (a > s0) s0 = a;
            if (b < s1) s1 = b;
            return s1 > s0;
        }

        /// <summary>Cell k's obstacle group, or −1.</summary>
        public int Group(int k) => _group[k];

        /// <summary>Whether cell k is an obstacle cell (a top above the ground).</summary>
        public bool IsObstacle(int k) => _top[k] > 0;

        /// <summary>Whether cell k lies within NearCells of an obstacle cell.</summary>
        public bool IsNear(int k) => _near[k];

        /// <summary>Cell k's centre, world X and Z, m.</summary>
        public void Centre(int k, out double x, out double z)
        {
            x = OriginX + (k % NX + 0.5) * Cell;
            z = OriginZ + (k / NX + 0.5) * Cell;
        }
    }
}
