using System;
using System.Collections.Generic;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// A map's own wind obstacles (p7c-wind, the map's wind; WindSetup.ForMap): the wake grid's obstacles from the placed
    /// objects' wind volumes and porosity (houses, cars, fences, poles, single trees), and the tree belt (the rows of the
    /// generator's "posadka" group) read once, as wooded strips. Derived from the world, never drawn: the same map gives
    /// the same bits on every runtime (sums run in the map's row order, never in a sort's). Labels: derived (computed
    /// from the placed volumes), recalled (a published rule, not re-read in this task), assumed (a named constant).
    /// - **The belt's sections:** the belt's volumes along its principal axis (the eigenvector of their centres' plan
    ///   covariance, derived), each over its footprint's extent along it; a clear stretch of the axis wider than
    ///   SectionGap between them (a field track through the belt) ends a section. Each section is one CanopyStrip and
    ///   one porous wake obstacle, so a gap stays open to the wind.
    /// - **A section's strip** (derived): its footprint's extent along and across the axis (the crowns', not the
    ///   trunks'); each plant's frontal area A = (1 − β)·D·(top − base), β its optical porosity and D its footprint's
    ///   mean width (perimeter / π, Cauchy's: the wind comes from any bearing), so a crown counts by the light it stops;
    ///   λ = ΣA / (length × width); the canopy's height h = ΣA·top / ΣA (assumed: the frontal-area-weighted mean top, so
    ///   the tall trees that carry the drag set it).
    /// - **A section's wake obstacle** (WakeGrid's input, so the wake grid gives the lee its porous wake): one upright box
    ///   over the strip's footprint, base the lowest plant's, top h. Its path porosity across the belt is the aerodynamic
    ///   porosity α = β_belt^AerodynamicPower (recalled: Guan et al. 2003, tree windbreaks' bleed against their optical
    ///   porosity; p7c-wind 1.5's porosity rule), with β_belt = e^(−λ·W/h) the optical porosity across its width W
    ///   (derived: crowns of mean frontal density λ/h scattered over the width, Beer–Lambert), never the product of the
    ///   crowns' cells (which multiplies a shrub at 1 m and a crown at 9 m on the same plan path). K§10.3's rule gives a
    ///   path of length D through a volume its porosity, so the box carries α^(D_box/W).
    /// - **Never counted twice:** the belt's plants never enter the wake grid themselves; WindField gives the strip's
    ///   canopy its plan weight's share inside and over it, and the wake grid the rest (WindField.Air).
    /// </summary>
    public sealed class MapWind
    {
        /// <summary>The placement group whose rows are the tree belt (the test range's generator, tools/map).</summary>
        public const string BeltGroup = "posadka";

        /// <summary>A clear stretch of the belt's axis wider than this ends a section, m (derived: the wake grid's cell,
        /// below which it cannot hold a gap open).</summary>
        public const double SectionGap = WakeObstacles.Cell;

        /// <summary>α = β^this, a deep belt's aerodynamic porosity from its optical one (recalled: Guan et al. 2003).</summary>
        public const double AerodynamicPower = 0.4;

        /// <summary>One section of the belt and the numbers derived for it.</summary>
        public sealed class Belt
        {
            public CanopyStrip Strip;

            /// <summary>Its wake obstacle in the layout.</summary>
            public WindVolume Obstacle;

            /// <summary>The belt's volumes it holds, their frontal area (m²), the tallest top (m), the optical porosity across
            /// the width and the aerodynamic porosity α (derived).</summary>
            public int Volumes;
            public double FrontalArea, TallestTop, OpticalPorosity, AerodynamicPorosity;
        }

        /// <summary>The wake grid's obstacles over the map's extent: the placed volumes off the belt, and each section's.</summary>
        public readonly WakeLayout Wakes;

        /// <summary>The belt's sections in order along its axis (none on a map without one).</summary>
        public readonly Belt[] Belts;

        /// <summary>The sections' strips, for WindSetup.Canopies (null on a map without a belt).</summary>
        public CanopyStrip[] Strips => Belts.Length == 0 ? null : Array.ConvertAll(Belts, b => b.Strip);

        MapWind(WakeLayout wakes, Belt[] belts)
        {
            Wakes = wakes;
            Belts = belts;
        }

        /// <summary>The map's wind obstacles. Load time: it allocates.</summary>
        public static MapWind Of(MapWorld map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            ReadOnlySpan<WindVolume> all = map.WindVolumes;
            var others = new List<WindVolume>();
            var belt = new List<WindVolume>();
            for (int k = 0; k < all.Length; k++)
                (map.ObjectGroup(map.WindVolumeObject(k)) == BeltGroup ? belt : others).Add(all[k]);
            Belt[] belts = Sections(belt);
            foreach (Belt b in belts) others.Add(b.Obstacle);
            var wakes = new WakeLayout { OriginX = map.ExtentX, OriginZ = map.ExtentZ, NX = map.CellsX, NZ = map.CellsZ, Volumes = others.ToArray() };
            return new MapWind(wakes.Checked(), belts);
        }

        static Belt[] Sections(List<WindVolume> v)
        {
            int n = v.Count;
            if (n == 0) return Array.Empty<Belt>();

            // The principal axis: the major eigenvector of the centres' plan covariance (a, b; b, c).
            double mx = 0, mz = 0;
            foreach (WindVolume w in v) { mx += w.X; mz += w.Z; }
            mx /= n;
            mz /= n;
            double a = 0, b = 0, c = 0;
            foreach (WindVolume w in v)
            {
                double dx = w.X - mx, dz = w.Z - mz;
                a += dx * dx;
                b += dx * dz;
                c += dz * dz;
            }
            double half = (a - c) / 2, major = (a + c) / 2 + DetMath.Sqrt(half * half + b * b);
            double ex = major - c, ez = b;
            if (!(DetMath.Abs(ex) + DetMath.Abs(ez) > 1e-12 * (a + c))) { ex = a >= c ? 1 : 0; ez = a >= c ? 0 : 1; }
            double len = DetMath.Sqrt(ex * ex + ez * ez);
            ex /= len;
            ez /= len;
            double bearing = DetMath.Atan2(ex, ez); // from north towards east, so (sin, cos) = (ex, ez)
            if (bearing < 0) { bearing += 3.141592653589793; ex = -ex; ez = -ez; }
            if (bearing >= 3.141592653589793) { bearing -= 3.141592653589793; ex = -ex; ez = -ez; }
            double nx = -ez, nz = ex; // across: north of an east axis

            // Each volume's footprint along (u) and across (v) the axis, about the centroid.
            var u0 = new double[n];
            var u1 = new double[n];
            var v0 = new double[n];
            var v1 = new double[n];
            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                WindVolume w = v[i];
                double ra, rn;
                if (w.Kind == WindVolumeKind.Cylinder) ra = rn = w.Radius;
                else
                {
                    DetMath.SinCos(w.Yaw, out double sin, out double cos); // its local x is (cos, −sin), its z (sin, cos)
                    ra = DetMath.Abs(w.HalfX * (cos * ex - sin * ez)) + DetMath.Abs(w.HalfZ * (sin * ex + cos * ez));
                    rn = DetMath.Abs(w.HalfX * (cos * nx - sin * nz)) + DetMath.Abs(w.HalfZ * (sin * nx + cos * nz));
                }
                double du = (w.X - mx) * ex + (w.Z - mz) * ez, dv = (w.X - mx) * nx + (w.Z - mz) * nz;
                u0[i] = du - ra;
                u1[i] = du + ra;
                v0[i] = dv - rn;
                v1[i] = dv + rn;
                order[i] = i;
            }

            // The sections: a sweep along the axis in the order of the footprints' starts (ties in any order: the
            // sections are the union's runs either way).
            Array.Sort((double[])u0.Clone(), order);
            var section = new int[n];
            int s = -1;
            double end = double.NegativeInfinity;
            foreach (int i in order)
            {
                if (u0[i] > end + SectionGap) s++;
                section[i] = s;
                if (u1[i] > end) end = u1[i];
            }
            int count = s + 1;

            // Each section's extent, frontal area, its moment of height and its lowest base, summed in the map's order.
            var lo = new double[count];
            var hi = new double[count];
            var left = new double[count];
            var right = new double[count];
            var area = new double[count];
            var moment = new double[count];
            var bottom = new double[count];
            var tallest = new double[count];
            var held = new int[count];
            for (int k = 0; k < count; k++)
            {
                lo[k] = left[k] = bottom[k] = double.PositiveInfinity;
                hi[k] = right[k] = double.NegativeInfinity;
            }
            for (int i = 0; i < n; i++)
            {
                WindVolume w = v[i];
                int k = section[i];
                lo[k] = DetMath.Min(lo[k], u0[i]);
                hi[k] = DetMath.Max(hi[k], u1[i]);
                left[k] = DetMath.Min(left[k], v0[i]);
                right[k] = DetMath.Max(right[k], v1[i]);
                double width = w.Kind == WindVolumeKind.Cylinder ? 2 * w.Radius : 4 * (w.HalfX + w.HalfZ) / 3.141592653589793;
                double frontal = (1 - w.Porosity) * width * (w.TopM - w.BaseM);
                area[k] += frontal;
                moment[k] += frontal * w.TopM;
                bottom[k] = DetMath.Min(bottom[k], w.BaseM);
                tallest[k] = DetMath.Max(tallest[k], w.TopM);
                held[k]++;
            }

            var belts = new Belt[count];
            for (int k = 0; k < count; k++)
            {
                double length = hi[k] - lo[k], width = right[k] - left[k];
                double h = area[k] > 0 ? moment[k] / area[k] : tallest[k];
                double lambda = area[k] / (length * width);
                double cu = (lo[k] + hi[k]) / 2, cv = (left[k] + right[k]) / 2;
                double cx = mx + ex * cu + nx * cv, cz = mz + ez * cu + nz * cv;
                double optical = DetMath.Exp(-lambda * width / h);
                double alpha = DetMath.Exp(AerodynamicPower * DetMath.Log(optical));
                double boxD = 2 * (length + width) / 3.141592653589793;
                var strip = new CanopyStrip
                {
                    CentreX = cx, CentreZ = cz, AxisDeg = bearing * 57.29577951308232, Length = length, Width = width, Height = h,
                    FrontalAreaIndex = lambda,
                };
                belts[k] = new Belt
                {
                    Strip = strip.Checked(),
                    Obstacle = new WindVolume
                    {
                        Kind = WindVolumeKind.Box, X = cx, Z = cz, BaseM = bottom[k], TopM = h, HalfX = width / 2, HalfZ = length / 2,
                        Yaw = bearing, Porosity = DetMath.Exp(boxD / width * DetMath.Log(alpha)),
                    },
                    Volumes = held[k], FrontalArea = area[k], TallestTop = tallest[k], OpticalPorosity = optical, AerodynamicPorosity = alpha,
                };
            }
            return belts;
        }
    }
}
