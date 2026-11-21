using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fpv450.HullMass
{
    /// <summary>
    /// The airframe hull's potential-flow added mass (p7c-wind design 12). The hull is the union of drone.json's closed parts
    /// named in hull.json (with the canister's cone and nozzle), voxelised on a grid of cell h, its gaps narrower than
    /// hull.json's gap_closed_m closed (a morphological closing) and its enclosed voids filled. Its surface, the faces between
    /// the solid and the outside air, carries constant panels. For each unit translation e_k the exterior Neumann problem
    /// ∂φ/∂n = n·e_k is solved in the direct (Green's identity) form at the panels' centres,
    ///   ½φ_i − Σ_j φ_j ∫_j ∂G/∂n_y dS = −Σ_j (n_j·e_k) ∫_j G dS,  G = 1/(4π|x − y|), n out of the body,
    /// by GMRES; the added mass per unit density is then A_kl/ρ = −∮ φ_k n_l dS (Lamb 1932 §121; Newman, Marine
    /// Hydrodynamics 1977 §4.13). Far panels by their centre, near ones (within 3 h) by 4 × 4 Gauss points, a panel on itself
    /// by the square's closed form ∫ dA/r = 4 h ln(1 + √2). Checked against a sphere's ½V and Lamb's prolate spheroids
    /// (--check).
    /// Usage: hullmass --check | hullmass [--drone assets-src/drone/drone.json] [--hull tools/hullmass/hull.json] [--cell m]
    ///        [--gap m]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string drone = "assets-src/drone/drone.json", hull = "tools/hullmass/hull.json";
            double cell = double.NaN, gap = double.NaN;
            bool check = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--check": check = true; break;
                    case "--drone": drone = args[++i]; break;
                    case "--hull": hull = args[++i]; break;
                    case "--cell": cell = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--gap": gap = double.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    default: Console.Error.WriteLine("unknown argument " + args[i]); return 2;
                }
            }
            return check ? Check(double.IsNaN(cell) ? 0.005 : cell) : Hull(drone, hull, cell, gap);
        }

        // ---- the closed forms -------------------------------------------------------------------------------------------

        /// <summary>Lamb's coefficients of a prolate spheroid of length over diameter r (§115): along its axis and across.</summary>
        public static void Spheroid(double r, out double k1, out double k2)
        {
            double e = Math.Sqrt(1 - 1 / (r * r)), e2 = e * e, e3 = e2 * e, l = Math.Log((1 + e) / (1 - e));
            double a0 = 2 * (1 - e2) / e3 * (0.5 * l - e), b0 = 1 / e2 - (1 - e2) / (2 * e3) * l;
            k1 = a0 / (2 - a0);
            k2 = b0 / (2 - b0);
        }

        static int Check(double h)
        {
            bool ok = true;
            // A sphere: ½V on the diagonal, nothing off it.
            double R = 0.06;
            var sphere = new List<Shape> { Shape.Ellipsoid(new V3(0, 0, 0), R, R, R, 0) };
            double[,] a = SolveBoth(sphere, h, 0, out double vol, out int n, out int iters);
            double half = 0.5 * (4.0 / 3 * Math.PI * R * R * R);
            Console.WriteLine($"sphere R {R} m at h {h} and {h / 2} m: {n} panels, {iters} GMRES iterations; voxel volume {vol / (2 * half):F4} of the sphere's");
            ok &= Report("sphere", a, Diag(half, half, half), 0.015);

            // Prolate spheroids along z, L/D 2 and 1.5 (the hull's range), and the 2:1 one tilted 15° about x (off-diagonal terms).
            foreach (var (ld, tilt) in new[] { (2.0, 0.0), (1.5, 0.0), (2.0, 15.0) })
            {
                double c = 0.11, b = c / ld;
                var s = new List<Shape> { Shape.Ellipsoid(new V3(0, 0, 0), b, b, c, tilt * Math.PI / 180) };
                a = SolveBoth(s, h, 0, out vol, out n, out iters);
                Spheroid(ld, out double k1, out double k2);
                double v = 4.0 / 3 * Math.PI * b * b * c;
                double t = tilt * Math.PI / 180, cy = Math.Sin(t), cz = Math.Cos(t); // the axis (0, sin, cos)
                double ax = k1 * v, ac = k2 * v, d = ax - ac;
                var exact = new double[3, 3] { { ac, 0, 0 }, { 0, ac + d * cy * cy, d * cy * cz }, { 0, d * cy * cz, ac + d * cz * cz } };
                Console.WriteLine($"spheroid L/D {ld} tilted {tilt}° at h {h} and {h / 2} m: {n} panels, {iters} iterations; k1 {k1:F4} k2 {k2:F4} (Lamb); voxel volume {vol / v:F4}");
                ok &= Report($"spheroid {ld}/{tilt}", a, exact, 0.015);
                // The couple's coefficient, A_across − A_along, which the hull's moment rests on, within 3 % of Lamb's.
                double across = a[0, 0], along = cz * cz * a[2, 2] + cy * cy * a[1, 1] + 2 * cy * cz * a[1, 2];
                double dk = (across - along) / v, dkExact = k2 - k1;
                bool pass = Math.Abs(dk - dkExact) <= 0.03 * dkExact;
                Console.WriteLine($"  (A_across − A_along)/V {dk:F4} against Lamb's k2 − k1 {dkExact:F4}: {(pass ? "PASS" : "FAIL")}");
                ok &= pass;
            }
            Console.WriteLine(ok ? "check: PASS" : "check: FAIL");
            return ok ? 0 : 1;
        }

        static double[,] Diag(double x, double y, double z) => new double[3, 3] { { x, 0, 0 }, { 0, y, 0 }, { 0, 0, z } };

        static bool Report(string what, double[,] a, double[,] exact, double tol)
        {
            double scale = (exact[0, 0] + exact[1, 1] + exact[2, 2]) / 3;
            bool ok = true;
            for (int i = 0; i < 3; i++)
                for (int j = i; j < 3; j++)
                {
                    double err = (a[i, j] - exact[i, j]) / scale;
                    bool pass = Math.Abs(err) <= tol;
                    ok &= pass;
                    Console.WriteLine($"  {what} A{"xyz"[i]}{"xyz"[j]}/ρ {a[i, j]:E4} m³ against {exact[i, j]:E4}: {err * 100:+0.00;-0.00} % of the mean diagonal {(pass ? "PASS" : "FAIL")}");
                }
            return ok;
        }

        // ---- the hull ---------------------------------------------------------------------------------------------------

        static int Hull(string dronePath, string hullPath, double cell, double gap)
        {
            using var hullDoc = JsonDocument.Parse(File.ReadAllText(hullPath));
            using var droneDoc = JsonDocument.Parse(File.ReadAllText(dronePath));
            JsonElement hp = hullDoc.RootElement;
            if (double.IsNaN(cell)) cell = hp.GetProperty("cell_m").GetDouble();
            if (double.IsNaN(gap)) gap = hp.GetProperty("gap_closed_m").GetDouble();
            var parts = new Dictionary<string, JsonElement>();
            foreach (JsonElement p in droneDoc.RootElement.GetProperty("parts").EnumerateArray())
                parts[p.GetProperty("name").GetString()] = p;
            var shapes = new List<Shape>();
            foreach (JsonElement name in hp.GetProperty("parts").EnumerateArray())
            {
                JsonElement p = parts[name.GetString()];
                V3 pos = Vec(p, "position_m");
                if (p.GetProperty("kind").GetString() == "box")
                    shapes.Add(Shape.Box(pos, Vec(p, "size_m")));
                else
                {
                    V3 axis = Vec(p, "axis").Unit();
                    double len = Val(p, "length_m"), r = 0.5 * Val(p, "diameter_m");
                    shapes.Add(Shape.Frustum(pos - axis * (0.5 * len), pos + axis * (0.5 * len), r, r));
                }
            }
            // The tails: frusta continuing a cylinder part aft of its rear end, along its axis.
            foreach (JsonElement t in hp.GetProperty("tails").EnumerateArray())
            {
                JsonElement p = parts[t.GetProperty("of").GetString()];
                V3 axis = Vec(p, "axis").Unit();
                V3 end = Vec(p, "position_m") - axis * (0.5 * Val(p, "length_m"));
                foreach (JsonElement seg in t.GetProperty("segments").EnumerateArray())
                {
                    double len = seg.GetProperty("length_m").GetDouble();
                    V3 next = end - axis * len;
                    shapes.Add(Shape.Frustum(end, next, 0.5 * seg.GetProperty("d0_m").GetDouble(), 0.5 * seg.GetProperty("d1_m").GetDouble()));
                    end = next;
                }
            }
            double[,] a = SolveBoth(shapes, cell, gap, out double vol, out int n, out int iters);
            Voxels fine = Voxels.Of(shapes, 0.5 * cell, gap);
            var bounds = fine.SolidBounds();
            fine.Front(out double front, out double width, out double height);
            Console.WriteLine($"hull: {shapes.Count} shapes, cells {cell} and {cell / 2} m (extrapolated), gaps under {gap} m closed: {n} panels, {iters} GMRES iterations");
            Console.WriteLine($"  volume {vol:E4} m³; extent x {bounds.lo.X:F3}..{bounds.hi.X:F3}, y {bounds.lo.Y:F3}..{bounds.hi.Y:F3}, z {bounds.lo.Z:F3}..{bounds.hi.Z:F3} m");
            Console.WriteLine("  added mass per unit density, body axes (m³): " +
                $"xx {a[0, 0]:E4} yy {a[1, 1]:E4} zz {a[2, 2]:E4} xy {a[0, 1]:E4} xz {a[0, 2]:E4} yz {a[1, 2]:E4}");
            Console.WriteLine("  over the volume: " +
                $"xx {a[0, 0] / vol:F4} yy {a[1, 1] / vol:F4} zz {a[2, 2] / vol:F4} xy {a[0, 1] / vol:F4} xz {a[0, 2] / vol:F4} yz {a[1, 2] / vol:F4}");
            Console.WriteLine($"  front silhouette {front:F5} m², width {width:F3} m, height {height:F3} m; mean length (volume over it) {vol / front:F3} m;"
                + $" L/H {vol / front / height:F3}, L/W {vol / front / width:F3}");
            Console.WriteLine("JSON size " + JsonSerializer.Serialize(new[] { Round(front), Round(width), Round(height), Round(vol / front) }));
            Console.WriteLine("JSON added " + JsonSerializer.Serialize(new[]
            {
                Round(a[0, 0]), Round(a[1, 1]), Round(a[2, 2]), Round(a[0, 1]), Round(a[0, 2]), Round(a[1, 2])
            }));
            return 0;
        }

        static double Round(double v) => double.Parse(v.ToString("E4", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        static double Val(JsonElement p, string key)
        {
            JsonElement v = p.GetProperty(key);
            return v.ValueKind == JsonValueKind.Object ? v.GetProperty("value").GetDouble() : v.GetDouble();
        }

        static V3 Vec(JsonElement p, string key)
        {
            JsonElement v = p.GetProperty(key);
            if (v.ValueKind == JsonValueKind.Object) v = v.GetProperty("value");
            return new V3(v[0].GetDouble(), v[1].GetDouble(), v[2].GetDouble());
        }

        // ---- the boundary-element solve ---------------------------------------------------------------------------------

        static readonly double[] GaussX = { -0.8611363115940526, -0.3399810435848563, 0.3399810435848563, 0.8611363115940526 };
        static readonly double[] GaussW = { 0.3478548451374538, 0.6521451548625461, 0.6521451548625461, 0.3478548451374538 };

        /// <summary>
        /// The added mass per unit density (m³) of the shapes' union, Richardson-extrapolated from cells h and h/2: the
        /// staircase surface's error is first order in h (measured: a sphere's +9.1 % at R/h 12 and +4.75 % at 24), so
        /// 2·A(h/2) − A(h) removes it (the sphere then +0.44 %). vol is the h/2 solid's volume.
        /// </summary>
        public static double[,] SolveBoth(List<Shape> shapes, double h, double gap, out double vol, out int n, out int iterations)
        {
            double[,] coarse = Solve(Voxels.Of(shapes, h, gap), out _, out _, out int i1);
            double[,] fine = Solve(Voxels.Of(shapes, 0.5 * h, gap), out vol, out n, out int i2);
            iterations = Math.Max(i1, i2);
            var a = new double[3, 3];
            for (int k = 0; k < 3; k++)
                for (int l = 0; l < 3; l++) a[k, l] = 2 * fine[k, l] - coarse[k, l];
            return a;
        }

        /// <summary>The added mass per unit density (m³) of the voxels' outer surface; vol is the solid's volume.</summary>
        public static double[,] Solve(Voxels vox, out double vol, out int count, out int iterations)
        {
            List<Panel> panels = vox.Panels();
            int n = panels.Count;
            count = n;
            vol = vox.SolidCount * vox.H * vox.H * vox.H;
            double h = vox.H, area = h * h, near2 = 9 * h * h;
            double selfG = h * Math.Log(1 + Math.Sqrt(2)) / Math.PI; // ∫ dA/(4πr) over the square, from its centre
            var c = new V3[n];
            var nn = new V3[n];
            for (int i = 0; i < n; i++) { c[i] = panels[i].C; nn[i] = panels[i].N; }

            // Near pairs by quadrature, kept sparse.
            var nearJ = new List<int>[n];
            var nearK = new List<double>[n];
            var nearG = new List<double>[n];
            Parallel.For(0, n, i =>
            {
                var js = new List<int>(); var ks = new List<double>(); var gs = new List<double>();
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    V3 d = c[i] - c[j];
                    if (d.Dot(d) > near2) continue;
                    panels[j].Tangents(out V3 t1, out V3 t2);
                    double k = 0, g = 0;
                    for (int u = 0; u < 4; u++)
                        for (int v = 0; v < 4; v++)
                        {
                            V3 y = c[j] + t1 * (0.5 * h * GaussX[u]) + t2 * (0.5 * h * GaussX[v]);
                            V3 r = c[i] - y;
                            double r2 = r.Dot(r), rl = Math.Sqrt(r2), w = GaussW[u] * GaussW[v] * 0.25 * area / (4 * Math.PI);
                            g += w / rl;
                            k += w * nn[j].Dot(r) / (r2 * rl);
                        }
                    js.Add(j); ks.Add(k); gs.Add(g);
                }
                nearJ[i] = js; nearK[i] = ks; nearG[i] = gs;
            });

            // y = op(x): kernel K (∂G/∂n_y) or G applied to x, far by centres, near by the table, self by its closed form.
            void Apply(double[] x, double[] y, bool doubleLayer)
            {
                Parallel.For(0, n, i =>
                {
                    double s = 0;
                    V3 ci = c[i];
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        double dx = ci.X - c[j].X, dy = ci.Y - c[j].Y, dz = ci.Z - c[j].Z, r2 = dx * dx + dy * dy + dz * dz;
                        if (r2 <= near2) continue;
                        double rl = Math.Sqrt(r2);
                        s += doubleLayer ? x[j] * (nn[j].X * dx + nn[j].Y * dy + nn[j].Z * dz) / (r2 * rl) : x[j] / rl;
                    }
                    s *= area / (4 * Math.PI);
                    List<int> js = nearJ[i]; List<double> w = doubleLayer ? nearK[i] : nearG[i];
                    for (int m = 0; m < js.Count; m++) s += w[m] * x[js[m]];
                    if (!doubleLayer) s += selfG * x[i];
                    y[i] = s;
                });
            }

            var result = new double[3, 3];
            iterations = 0;
            var phi = new double[3][];
            for (int k = 0; k < 3; k++)
            {
                var sigma = new double[n];
                for (int i = 0; i < n; i++) sigma[i] = k == 0 ? nn[i].X : k == 1 ? nn[i].Y : nn[i].Z;
                var b = new double[n];
                Apply(sigma, b, false);
                for (int i = 0; i < n; i++) b[i] = -b[i];
                var tmp = new double[n];
                phi[k] = Gmres(x => { Apply(x, tmp, true); var y = new double[n]; for (int i = 0; i < n; i++) y[i] = 0.5 * x[i] - tmp[i]; return y; },
                    b, 1e-8, 200, out int it);
                iterations = Math.Max(iterations, it);
            }
            for (int k = 0; k < 3; k++)
                for (int l = 0; l < 3; l++)
                {
                    double s = 0;
                    for (int i = 0; i < n; i++) s -= phi[k][i] * (l == 0 ? nn[i].X : l == 1 ? nn[i].Y : nn[i].Z) * area;
                    result[k, l] = s;
                }
            // The tensor is symmetric in theory; its discrete form nearly so: report the symmetric part.
            for (int k = 0; k < 3; k++)
                for (int l = k + 1; l < 3; l++)
                    result[k, l] = result[l, k] = 0.5 * (result[k, l] + result[l, k]);
            return result;
        }

        static double[] Gmres(Func<double[], double[]> op, double[] b, double tol, int maxIt, out int its)
        {
            int n = b.Length;
            double bn = Math.Sqrt(Dot(b, b));
            var V = new List<double[]>();
            var H = new double[maxIt + 1, maxIt];
            var cs = new double[maxIt]; var sn = new double[maxIt]; var g = new double[maxIt + 1];
            var v0 = (double[])b.Clone();
            for (int i = 0; i < n; i++) v0[i] /= bn;
            V.Add(v0);
            g[0] = bn;
            int k = 0;
            for (; k < maxIt; k++)
            {
                double[] w = op(V[k]);
                for (int j = 0; j <= k; j++)
                {
                    H[j, k] = Dot(w, V[j]);
                    for (int i = 0; i < n; i++) w[i] -= H[j, k] * V[j][i];
                }
                H[k + 1, k] = Math.Sqrt(Dot(w, w));
                for (int i = 0; i < n; i++) w[i] /= H[k + 1, k];
                V.Add(w);
                for (int j = 0; j < k; j++)
                {
                    double t = cs[j] * H[j, k] + sn[j] * H[j + 1, k];
                    H[j + 1, k] = -sn[j] * H[j, k] + cs[j] * H[j + 1, k];
                    H[j, k] = t;
                }
                double den = Math.Sqrt(H[k, k] * H[k, k] + H[k + 1, k] * H[k + 1, k]);
                cs[k] = H[k, k] / den; sn[k] = H[k + 1, k] / den;
                H[k, k] = den; H[k + 1, k] = 0;
                g[k + 1] = -sn[k] * g[k]; g[k] = cs[k] * g[k];
                if (Math.Abs(g[k + 1]) < tol * bn) { k++; break; }
            }
            its = k;
            var yv = new double[k];
            for (int i = k - 1; i >= 0; i--)
            {
                double s = g[i];
                for (int j = i + 1; j < k; j++) s -= H[i, j] * yv[j];
                yv[i] = s / H[i, i];
            }
            var x = new double[n];
            for (int j = 0; j < k; j++)
                for (int i = 0; i < n; i++) x[i] += yv[j] * V[j][i];
            return x;
        }

        static double Dot(double[] a, double[] b) { double s = 0; for (int i = 0; i < a.Length; i++) s += a[i] * b[i]; return s; }
    }

    public readonly struct V3
    {
        public readonly double X, Y, Z;
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, double s) => new V3(a.X * s, a.Y * s, a.Z * s);
        public double Dot(V3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public V3 Unit() { double l = Math.Sqrt(Dot(this)); return this * (1 / l); }
    }

    public readonly struct Panel
    {
        public readonly V3 C, N;
        public Panel(V3 c, V3 n) { C = c; N = n; }
        public void Tangents(out V3 t1, out V3 t2)
        {
            if (N.X != 0) { t1 = new V3(0, 1, 0); t2 = new V3(0, 0, 1); }
            else if (N.Y != 0) { t1 = new V3(1, 0, 0); t2 = new V3(0, 0, 1); }
            else { t1 = new V3(1, 0, 0); t2 = new V3(0, 1, 0); }
        }
    }

    /// <summary>A solid: an axis-aligned box, a frustum between two points (a cylinder when its radii match) or an ellipsoid
    /// with its semi-axes along x, y and z turned about x.</summary>
    public sealed class Shape
    {
        int _kind;
        V3 _a, _b;
        double _r0, _r1, _tilt;

        public static Shape Box(V3 centre, V3 size) => new Shape { _kind = 0, _a = centre - size * 0.5, _b = centre + size * 0.5 };
        public static Shape Frustum(V3 p0, V3 p1, double r0, double r1) => new Shape { _kind = 1, _a = p0, _b = p1, _r0 = r0, _r1 = r1 };
        public static Shape Ellipsoid(V3 centre, double ax, double ay, double az, double tilt) =>
            new Shape { _kind = 2, _a = centre, _b = new V3(ax, ay, az), _tilt = tilt };

        public bool Inside(V3 q)
        {
            switch (_kind)
            {
                case 0: return q.X >= _a.X && q.X <= _b.X && q.Y >= _a.Y && q.Y <= _b.Y && q.Z >= _a.Z && q.Z <= _b.Z;
                case 1:
                {
                    V3 ax = _b - _a; double len2 = ax.Dot(ax), t = (q - _a).Dot(ax) / len2;
                    if (t < 0 || t > 1) return false;
                    V3 radial = q - _a - ax * t; double r = _r0 + (_r1 - _r0) * t;
                    return radial.Dot(radial) <= r * r;
                }
                default:
                {
                    V3 d = q - _a; double s = Math.Sin(_tilt), co = Math.Cos(_tilt);
                    // into the ellipsoid's frame: its z axis is (0, sin, cos)
                    double y = co * d.Y - s * d.Z, z = s * d.Y + co * d.Z;
                    return d.X * d.X / (_b.X * _b.X) + y * y / (_b.Y * _b.Y) + z * z / (_b.Z * _b.Z) <= 1;
                }
            }
        }

        public void Bounds(out V3 lo, out V3 hi)
        {
            if (_kind == 0) { lo = _a; hi = _b; return; }
            if (_kind == 2) { double m = Math.Max(_b.X, Math.Max(_b.Y, _b.Z)); lo = _a - new V3(m, m, m); hi = _a + new V3(m, m, m); return; }
            double r = Math.Max(_r0, _r1);
            lo = new V3(Math.Min(_a.X, _b.X) - r, Math.Min(_a.Y, _b.Y) - r, Math.Min(_a.Z, _b.Z) - r);
            hi = new V3(Math.Max(_a.X, _b.X) + r, Math.Max(_a.Y, _b.Y) + r, Math.Max(_a.Z, _b.Z) + r);
        }
    }

    /// <summary>The shapes' union on a grid of cell H: closed over gaps narrower than the closing width, its voids filled.</summary>
    public sealed class Voxels
    {
        public double H;
        V3 _origin;
        int _nx, _ny, _nz;
        bool[] _solid;
        public int SolidCount;

        int Index(int i, int j, int k) => (k * _ny + j) * _nx + i;
        V3 Centre(int i, int j, int k) => _origin + new V3((i + 0.5) * H, (j + 0.5) * H, (k + 0.5) * H);

        public static Voxels Of(List<Shape> shapes, double h, double gap)
        {
            V3 lo = new V3(1e9, 1e9, 1e9), hi = new V3(-1e9, -1e9, -1e9);
            foreach (Shape s in shapes)
            {
                s.Bounds(out V3 a, out V3 b);
                lo = new V3(Math.Min(lo.X, a.X), Math.Min(lo.Y, a.Y), Math.Min(lo.Z, a.Z));
                hi = new V3(Math.Max(hi.X, b.X), Math.Max(hi.Y, b.Y), Math.Max(hi.Z, b.Z));
            }
            int rc = (int)Math.Ceiling(0.5 * gap / h); // the closing's radius in cells
            double margin = (rc + 3) * h;
            // The grid's planes on multiples of h, so a body symmetric about a coordinate plane voxelises symmetrically.
            V3 o = lo - new V3(margin, margin, margin);
            var v = new Voxels { H = h, _origin = new V3(Math.Floor(o.X / h) * h, Math.Floor(o.Y / h) * h, Math.Floor(o.Z / h) * h) };
            v._nx = (int)Math.Ceiling((hi.X + margin - v._origin.X) / h);
            v._ny = (int)Math.Ceiling((hi.Y + margin - v._origin.Y) / h);
            v._nz = (int)Math.Ceiling((hi.Z + margin - v._origin.Z) / h);
            v._solid = new bool[v._nx * v._ny * v._nz];
            for (int k = 0; k < v._nz; k++)
                for (int j = 0; j < v._ny; j++)
                    for (int i = 0; i < v._nx; i++)
                    {
                        V3 q = v.Centre(i, j, k);
                        foreach (Shape s in shapes)
                            if (s.Inside(q)) { v._solid[v.Index(i, j, k)] = true; break; }
                    }
            if (gap > 0)
            {
                // A closing by a ball of radius gap/2: a gap narrower than gap fills.
                double r = 0.5 * gap;
                v._solid = v.Morph(v.Morph(v._solid, r, true), r, false);
            }
            v.FillVoids();
            foreach (bool b in v._solid) if (b) v.SolidCount++;
            return v;
        }

        bool[] Morph(bool[] src, double r, bool dilate)
        {
            int rc = (int)Math.Floor(r / H);
            var offs = new List<(int, int, int)>();
            for (int a = -rc; a <= rc; a++)
                for (int b = -rc; b <= rc; b++)
                    for (int c = -rc; c <= rc; c++)
                        if ((a * a + b * b + c * c) * H * H <= r * r + 1e-12) offs.Add((a, b, c));
            var dst = new bool[src.Length];
            Parallel.For(0, _nz, k =>
            {
                for (int j = 0; j < _ny; j++)
                    for (int i = 0; i < _nx; i++)
                    {
                        bool any = false, all = true;
                        foreach (var (a, b, c) in offs)
                        {
                            int ii = i + a, jj = j + b, kk = k + c;
                            bool s = ii >= 0 && jj >= 0 && kk >= 0 && ii < _nx && jj < _ny && kk < _nz && src[Index(ii, jj, kk)];
                            any |= s; all &= s;
                        }
                        dst[Index(i, j, k)] = dilate ? any : all;
                    }
            });
            return dst;
        }

        void FillVoids()
        {
            var outside = new bool[_solid.Length];
            var stack = new Stack<int>();
            stack.Push(0); outside[0] = true;
            while (stack.Count > 0)
            {
                int id = stack.Pop(), i = id % _nx, j = id / _nx % _ny, k = id / (_nx * _ny);
                foreach (var (a, b, c) in Faces)
                {
                    int ii = i + a, jj = j + b, kk = k + c;
                    if (ii < 0 || jj < 0 || kk < 0 || ii >= _nx || jj >= _ny || kk >= _nz) continue;
                    int m = Index(ii, jj, kk);
                    if (outside[m] || _solid[m]) continue;
                    outside[m] = true; stack.Push(m);
                }
            }
            for (int m = 0; m < _solid.Length; m++) _solid[m] = !outside[m];
        }

        static readonly (int, int, int)[] Faces = { (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1) };

        public List<Panel> Panels()
        {
            var list = new List<Panel>();
            for (int k = 0; k < _nz; k++)
                for (int j = 0; j < _ny; j++)
                    for (int i = 0; i < _nx; i++)
                    {
                        if (!_solid[Index(i, j, k)]) continue;
                        foreach (var (a, b, c) in Faces)
                        {
                            int ii = i + a, jj = j + b, kk = k + c;
                            if (_solid[Index(ii, jj, kk)]) continue; // the margin keeps the neighbours on the grid
                            var n = new V3(a, b, c);
                            list.Add(new Panel(Centre(i, j, k) + n * (0.5 * H), n));
                        }
                    }
            return list;
        }

        /// <summary>The silhouette seen along z (the front): its area, and its width (x) and height (y) extents.</summary>
        public void Front(out double area, out double width, out double height)
        {
            int count = 0, i0 = int.MaxValue, i1 = -1, j0 = int.MaxValue, j1 = -1;
            for (int j = 0; j < _ny; j++)
                for (int i = 0; i < _nx; i++)
                    for (int k = 0; k < _nz; k++)
                        if (_solid[Index(i, j, k)])
                        {
                            count++;
                            i0 = Math.Min(i0, i); i1 = Math.Max(i1, i); j0 = Math.Min(j0, j); j1 = Math.Max(j1, j);
                            break;
                        }
            area = count * H * H;
            width = (i1 - i0 + 1) * H;
            height = (j1 - j0 + 1) * H;
        }

        public (V3 lo, V3 hi) SolidBounds()
        {
            V3 lo = new V3(1e9, 1e9, 1e9), hi = new V3(-1e9, -1e9, -1e9);
            for (int k = 0; k < _nz; k++)
                for (int j = 0; j < _ny; j++)
                    for (int i = 0; i < _nx; i++)
                        if (_solid[Index(i, j, k)])
                        {
                            V3 a = Centre(i, j, k) - new V3(0.5 * H, 0.5 * H, 0.5 * H), b = a + new V3(H, H, H);
                            lo = new V3(Math.Min(lo.X, a.X), Math.Min(lo.Y, a.Y), Math.Min(lo.Z, a.Z));
                            hi = new V3(Math.Max(hi.X, b.X), Math.Max(hi.Y, b.Y), Math.Max(hi.Z, b.Z));
                        }
            return (lo, hi);
        }
    }
}
