using System;

/// One 2 m cell of the wind-obstacle grid (world-query W-9). Open ground is (0, 0, 1).
public struct WindCell
{
    public float TopM;     // highest obstacle top above the terrain at the cell centre, m
    public float BaseM;    // lowest obstacle base above the terrain (a crown's underside; 0 for walls), m
    public float Porosity; // optical porosity of a 2 m horizontal path through the cell: n cells in a row give β₁·β₂·…·βₙ
}

/// The wind-obstacle grid, derived while objects are placed from each asset's wind volume (or its collision shapes when
/// it has none). Nothing derived is stored in the map, so it never goes stale.
public sealed partial class WorldQuery
{
    public const double WindCellSize = 2.0;
    /// A footprint is the convex hull of the shape's support points in this many horizontal directions.
    const int FootprintDirections = 64;

    WindCell[] _wind;

    /// Cells per side of the wind grid.
    public int WindCells { get; private set; }

    /// World-query W-9, read-only: row-major, row 0 north (smallest z), column 0 west. Cell (row, column) is centred at
    /// x = −size/2 + (column + 0.5)·2 m, z = −size/2 + (row + 0.5)·2 m.
    public ReadOnlySpan<WindCell> WindGrid => _wind;

    void InitWind()
    {
        WindCells = (int)Math.Round(SizeM / WindCellSize);
        _wind = new WindCell[WindCells * WindCells];
        Array.Fill(_wind, new WindCell { Porosity = 1 });
    }

    /// Merges one wind-volume shape of an object with optical porosity β_obj into every cell its footprint covers:
    /// β_cell = 1 − f·(1 − β_obj^(2 m / D)), with f the covered fraction of the cell and D the shape's mean horizontal
    /// extent, the footprint's mean width over all directions (perimeter / π, Cauchy): a round crown's diameter. Top and
    /// base are the shape's highest and lowest points above the terrain at each cell centre, never below 0. Overlaps
    /// multiply porosity and take the highest top and the lowest base. A shape with β_obj = 1 blocks nothing.
    void AddWind(in Prim s, double porosity)
    {
        if (!(porosity < 1))
            return;
        double top = Support(s, new Double3(0, 1, 0)).Y, bottom = Support(s, new Double3(0, -1, 0)).Y;
        Span<double> xs = stackalloc double[FootprintDirections], zs = stackalloc double[FootprintDirections];
        int n = 0;
        for (int k = 0; k < FootprintDirections; k++)
        {
            DetMath.SinCosTurns((double)k / FootprintDirections, out double sin, out double cos);
            Double3 p = Support(s, new Double3(cos, 0, sin));
            if (n > 0 && p.X == xs[n - 1] && p.Z == zs[n - 1])
                continue;
            xs[n] = p.X;
            zs[n++] = p.Z;
        }
        if (n > 1 && xs[n - 1] == xs[0] && zs[n - 1] == zs[0])
            n--;
        double perimeter = 0, minX = xs[0], maxX = xs[0], minZ = zs[0], maxZ = zs[0];
        for (int i = 0; i < n; i++)
        {
            int j = i + 1 < n ? i + 1 : 0;
            double dx = xs[j] - xs[i], dz = zs[j] - zs[i];
            perimeter += Math.Sqrt(dx * dx + dz * dz);
            (minX, maxX, minZ, maxZ) = (Math.Min(minX, xs[i]), Math.Max(maxX, xs[i]), Math.Min(minZ, zs[i]), Math.Max(maxZ, zs[i]));
        }
        if (!(perimeter > 0))
            return;
        double full = DetMath.Pow(porosity, WindCellSize * Math.PI / perimeter); // a whole 2 m path inside the volume
        int x0 = WindIndex(minX), x1 = WindIndex(maxX), z0 = WindIndex(minZ), z1 = WindIndex(maxZ);
        for (int row = z0; row <= z1; row++)
        {
            for (int column = x0; column <= x1; column++)
            {
                double cx = -Half + column * WindCellSize, cz = -Half + row * WindCellSize;
                double f = ClippedArea(xs[..n], zs[..n], cx, cx + WindCellSize, cz, cz + WindCellSize) / (WindCellSize * WindCellSize);
                if (!(f > 0))
                    continue;
                Sample(cx + WindCellSize / 2, cz + WindCellSize / 2, out GroundSample g, false);
                ref WindCell c = ref _wind[row * WindCells + column];
                if (!_loading)
                    Append(ref _windUndo, ref _windUndoCount, (row * WindCells + column, c));
                float cellTop = (float)Math.Max(top - g.TerrainHeight, 0), cellBase = (float)Math.Max(bottom - g.TerrainHeight, 0);
                double beta = 1 - f * (1 - full);
                if (c.TopM == 0 && c.Porosity == 1)
                    c = new WindCell { TopM = cellTop, BaseM = cellBase, Porosity = (float)beta };
                else
                    c = new WindCell { TopM = Math.Max(c.TopM, cellTop), BaseM = Math.Min(c.BaseM, cellBase), Porosity = (float)(c.Porosity * beta) };
            }
        }
    }

    /// NaN goes to cell 0.
    int WindIndex(double v)
    {
        double f = Math.Floor((v + Half) / WindCellSize);
        return f >= WindCells - 1 ? WindCells - 1 : f >= 0 ? (int)f : 0;
    }

    /// Area of a convex polygon inside the rectangle [x0, x1] × [z0, z1] (Sutherland–Hodgman, then the shoelace).
    static double ClippedArea(ReadOnlySpan<double> xs, ReadOnlySpan<double> zs, double x0, double x1, double z0, double z1)
    {
        int size = xs.Length + 8;
        Span<double> ax = stackalloc double[size], az = stackalloc double[size], bx = stackalloc double[size], bz = stackalloc double[size];
        xs.CopyTo(ax);
        zs.CopyTo(az);
        int n = Clip(ax, az, xs.Length, bx, bz, false, x0, true);
        n = Clip(bx, bz, n, ax, az, false, x1, false);
        n = Clip(ax, az, n, bx, bz, true, z0, true);
        n = Clip(bx, bz, n, ax, az, true, z1, false);
        double twice = 0;
        for (int i = 0; i < n; i++)
        {
            int j = i + 1 < n ? i + 1 : 0;
            twice += ax[i] * az[j] - ax[j] * az[i];
        }
        return Math.Abs(twice) / 2;
    }

    /// Keeps the part of the polygon whose x (or z) is ≥ `bound` when `above`, else ≤ `bound`.
    static int Clip(Span<double> xs, Span<double> zs, int n, Span<double> outX, Span<double> outZ, bool alongZ, double bound, bool above)
    {
        int m = 0;
        for (int i = 0; i < n; i++)
        {
            int j = i + 1 < n ? i + 1 : 0;
            double ci = (alongZ ? zs[i] : xs[i]) - bound, cj = (alongZ ? zs[j] : xs[j]) - bound;
            if (!above)
                (ci, cj) = (-ci, -cj);
            if (ci >= 0)
            {
                outX[m] = xs[i];
                outZ[m++] = zs[i];
            }
            if ((ci >= 0) != (cj >= 0))
            {
                double t = ci / (ci - cj);
                outX[m] = xs[i] + (xs[j] - xs[i]) * t;
                outZ[m++] = zs[i] + (zs[j] - zs[i]) * t;
            }
        }
        return m;
    }
}
