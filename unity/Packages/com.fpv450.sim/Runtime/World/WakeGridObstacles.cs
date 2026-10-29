// WakeObstacles: the wind-obstacle grid of K§10.3, the wake grid's input (p7c-wind 1.3, design 4).
// - On the 2 m grid, per cell: TopM and BaseM (heights above the local ground, m) and Porosity, the optical porosity of
//   a 2 m horizontal path through the cell.
// - Per placed wind volume (an asset's `wind_volumes` record, placed), with f its covered fraction of the cell (4 × 4
//   sample points per cell) and D = footprint perimeter / π: β_cell = 1 − f·(1 − β_obj^(2 m / D)) (K§10.3), so a path
//   across the whole volume (length about D) keeps the volume's own porosity.
// - Overlaps multiply porosity and take the highest top and the lowest base (K§10.3). Porosity 1 is no obstacle.
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

        /// <summary>Sample points per cell side for the covered fraction f (assumed: 16 points, f in steps of 1/16).</summary>
        const int Sub = 4;

        public readonly double OriginX, OriginZ;
        public readonly int NX, NZ;

        /// <summary>The number of obstacle groups; group ids run 0 .. Groups − 1, −1 for an open cell.</summary>
        public readonly int Groups;

        /// <summary>The tallest top in the grid, m.</summary>
        public readonly double MaxTop;

        readonly double[] _top, _base, _porosity;
        readonly int[] _group;
        readonly bool[] _near; // within NearCells (Chebyshev) of an obstacle cell

        /// <summary>How far, in cells, the near mask reaches around obstacle cells (the wake grid's side probes).</summary>
        public const int NearCells = 8;

        public WakeObstacles(double originX, double originZ, int nx, int nz, ReadOnlySpan<WindVolume> volumes)
        {
            if (nx < 1 || nz < 1) throw new ArgumentException("the grid needs at least one cell");
            OriginX = originX;
            OriginZ = originZ;
            NX = nx;
            NZ = nz;
            int n = nx * nz;
            _top = new double[n];
            _base = new double[n];
            _porosity = new double[n];
            for (int k = 0; k < n; k++) { _porosity[k] = 1; _base[k] = double.PositiveInfinity; }

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
                        for (int b = 0; b < Sub; b++)
                            for (int a = 0; a < Sub; a++)
                            {
                                double px = originX + (i + (a + 0.5) / Sub) * Cell - w.X;
                                double pz = originZ + (j + (b + 0.5) / Sub) * Cell - w.Z;
                                if (w.Kind == WindVolumeKind.Cylinder)
                                {
                                    if (px * px + pz * pz <= w.Radius * w.Radius) inside++;
                                }
                                else
                                {
                                    // Into the box's own axes (the inverse of Placed's yaw).
                                    double lx = px * cos - pz * sin, lz = px * sin + pz * cos;
                                    if (DetMath.Abs(lx) <= w.HalfX && DetMath.Abs(lz) <= w.HalfZ) inside++;
                                }
                            }
                        if (inside == 0) continue;
                        double f = inside / (double)(Sub * Sub);
                        int k = j * nx + i;
                        _porosity[k] *= 1 - f * (1 - cellBeta);
                        if (w.TopM > _top[k]) _top[k] = w.TopM;
                        if (w.BaseM < _base[k]) _base[k] = w.BaseM;
                    }
            }

            double maxTop = 0;
            for (int k = 0; k < n; k++)
            {
                if (_top[k] > maxTop) maxTop = _top[k];
                if (!(_top[k] > 0)) _base[k] = 0;
            }
            MaxTop = maxTop;

            // Groups: 4-connected obstacle cells, labelled in row-major order by an iterative fill.
            _group = new int[n];
            for (int k = 0; k < n; k++) _group[k] = -1;
            var stack = new int[n];
            int groups = 0;
            for (int k0 = 0; k0 < n; k0++)
            {
                if (_group[k0] >= 0 || !(_top[k0] > 0)) continue;
                int top = 0;
                stack[top++] = k0;
                _group[k0] = groups;
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
                    if (_group[q] >= 0 || !(_top[q] > 0)) return;
                    _group[q] = groups;
                    stack[top++] = q;
                }
            }
            Groups = groups;

            // The near mask: cells within NearCells of an obstacle cell (the side probes run only there).
            _near = new bool[n];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    if (!(_top[j * nx + i] > 0)) continue;
                    int a0 = i > NearCells ? i - NearCells : 0, a1 = i + NearCells < nx ? i + NearCells : nx - 1;
                    int b0 = j > NearCells ? j - NearCells : 0, b1 = j + NearCells < nz ? j + NearCells : nz - 1;
                    for (int b = b0; b <= b1; b++)
                        for (int a = a0; a <= a1; a++)
                            _near[b * nx + a] = true;
                }
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

        /// <summary>Cell k's 2 m path porosity (1: open).</summary>
        public double Porosity(int k) => _porosity[k];

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
