using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind, the map's wind (WindSetup.ForMap, MapWind): the test range's own obstacles in the field. Its placed wind
    // volumes give the wake grid's obstacles, and its belt (the "posadka" rows) is read once, as wooded strips with one
    // porous obstacle each for their lee, so the belt is never counted twice. The numbers behind H01_D are graded against
    // Fackrell's reattachment and Perera's far wake, evaluated here from the house's own volumes; inside the belt against
    // the canopy's σ/u* and its exponential profile; and every check also fails the known-wrong field without the map's
    // obstacles (WindSetup.Of, what the player flew before).
    public sealed class MapWindTests
    {
        const double DegToRad = Math.PI / 180;

        static MapWorld Map => RangeScenarios.World;

        static double Wrap(double deg) => deg - 360 * Math.Floor((deg + 180) / 360);

        // The first seed whose drawn wind blows from within `spread` of `fromDeg` and holds within 4° of its first bearing
        // for `seconds`, so the wake grid keeps its first build (WakeGridWorker asks for another past 5°).
        static ulong Seed(WindLevel level, double fromDeg, double spread, int seconds)
        {
            for (ulong s = 1; s < 5000; s++)
            {
                var w = new WindField(WindSetup.Of(level), new FlightRandom(s));
                double d0 = w.FromDegAt(0);
                if (Math.Abs(Wrap(d0 - fromDeg)) > spread) continue;
                bool held = true;
                for (int t = 1; t <= seconds && held; t++) held = Math.Abs(w.FromDegAt(t) - d0) < 4;
                if (held) return s;
            }
            throw new InvalidOperationException("no seed holds its wind");
        }

        static double Profile(WindField w, double z)
        {
            WindPreset p = w.Preset;
            return Math.Log((Math.Min(z, p.ProfileTop) - p.Displacement) / p.Roughness) / Math.Log((p.ReferenceHeight - p.Displacement) / p.Roughness);
        }

        // The wind's along-wind speed over the open field's mean at the point's height, and the horizontal turbulence over
        // the open field's σ, at fixed points over 30 s (every 0.1 s): the map's field less the no-obstacle field of the same
        // seed, whose open-air turbulence is the same bits, so only the wakes are left.
        static void Measure(WindField field, WindField open, IList<Double3> points, out double[] speed, out double[] turbulence)
        {
            int n = points.Count;
            var su = new double[n];
            var mx = new double[n];
            var mz = new double[n];
            var sq = new double[n];
            int samples = 0;
            for (long step = 0; step < 30000; step += 100, samples++)
            {
                field.Begin(step, default, 0);
                open.Begin(step, default, 0);
                if (field.Wakes != null) Assert.That(field.Wakes.Fading, Is.Null, "the wake grid keeps its first build");
                Double3 mean = field.Mean;
                double mag = mean.Length();
                Double3 along = mean * (1 / mag);
                for (int i = 0; i < n; i++)
                {
                    Double3 d = field.At(points[i]) - open.At(points[i]);
                    su[i] += Double3.Dot(d, along) / (mag * Profile(field, points[i].Y));
                    mx[i] += d.X;
                    mz[i] += d.Z;
                    sq[i] += d.X * d.X + d.Z * d.Z;
                }
            }
            speed = new double[n];
            turbulence = new double[n];
            for (int i = 0; i < n; i++)
            {
                speed[i] = 1 + su[i] / samples;
                double var = (sq[i] - (mx[i] * mx[i] + mz[i] * mz[i]) / samples) / samples / 2;
                double taper = Math.Min(1, (points[i].Y - field.Preset.Displacement) / field.Preset.TaperHeight);
                turbulence[i] = Math.Sqrt(1 + var / (field.SigmaH * field.SigmaH * taper * taper));
            }
        }

        // An object's wind volumes' footprint along the downwind unit vector (dx, dz) and across it (−dz, dx), and its top.
        static void Footprint(MapWorld map, string name, double dx, double dz, out double front, out double lee, out double c0, out double c1, out double top)
        {
            front = c0 = double.PositiveInfinity;
            lee = c1 = double.NegativeInfinity;
            top = 0;
            for (int k = 0; k < map.WindVolumes.Length; k++)
            {
                if (map.ObjectName(map.WindVolumeObject(k)) != name) continue;
                WindVolume v = map.WindVolumes[k];
                double sin = Math.Sin(v.Yaw), cos = Math.Cos(v.Yaw);
                for (int corner = 0; corner < 4; corner++)
                {
                    double lx = (corner & 1) == 0 ? -v.HalfX : v.HalfX, lz = (corner & 2) == 0 ? -v.HalfZ : v.HalfZ;
                    if (v.Kind == WindVolumeKind.Cylinder) { lx = (corner & 1) == 0 ? -v.Radius : v.Radius; lz = (corner & 2) == 0 ? -v.Radius : v.Radius; }
                    double x = v.X + lx * cos + lz * sin, z = v.Z - lx * sin + lz * cos;
                    double a = x * dx + z * dz, c = -x * dz + z * dx;
                    front = Math.Min(front, a);
                    lee = Math.Max(lee, a);
                    c0 = Math.Min(c0, c);
                    c1 = Math.Max(c1, c);
                }
                top = Math.Max(top, v.TopM);
            }
        }

        // Perera (1981)'s relative speed reduction (the shelter model of the European Wind Atlas), held at its peak below it
        // (WakeGrid's reading near the ground), at X and Z in obstacle heights for a porosity β and a height H over z0 0.03 m.
        static double Perera(double X, double Z, double beta, double H)
        {
            double k = 2 * 0.4 * 0.4 / Math.Log(H / 0.03), eta = Math.Max(0.99667, Z / Math.Pow(k * X, 1 / 2.14));
            return 9.75 * (1 - beta) * eta * Math.Exp(-0.67 * Math.Pow(eta, 1.5)) / X;
        }

        // The wake's lateral weight at crosswind c for an obstacle spanning [c0, c1], x metres behind it (σ = 0.135·x + 1 m).
        static double Spread(double c, double c0, double c1, double x)
        {
            double sigma = 0.135 * x + 1;
            return WakeGridMath.Phi((c - c0) / sigma) - WakeGridMath.Phi((c - c1) / sigma);
        }

        [Test]
        public void TheMapsBeltIsItsStripsAndEveryOtherVolumeAnObstacle()
        {
            MapWorld map = Map;
            MapWind wind = MapWind.Of(map);
            int belt = 0;
            for (int k = 0; k < map.WindVolumes.Length; k++)
                if (map.ObjectGroup(map.WindVolumeObject(k)) == MapWind.BeltGroup) belt++;
            TestContext.Out.WriteLine($"the test range: {map.WindVolumes.Length} placed wind volumes, {belt} of them the belt's; the layout {wind.Wakes.Volumes.Length} "
                + $"volumes on {wind.Wakes.NX} × {wind.Wakes.NZ} cells");
            foreach (MapWind.Belt b in wind.Belts)
            {
                CanopyStrip s = b.Strip;
                TestContext.Out.WriteLine($"  section: {b.Volumes} volumes about {s.CentreX:F2}, {s.CentreZ:F2} m, axis {s.AxisDeg:F3}°, {s.Length:F1} × {s.Width:F2} m; "
                    + $"h {s.Height:F2} m (tallest {b.TallestTop:F2}), λ {s.FrontalAreaIndex:F3} ({b.FrontalArea:F0} m²), optical porosity across {b.OpticalPorosity:F4}, α {b.AerodynamicPorosity:F3}");
            }

            // Two sections either side of the field track's gap (tools/map: 12 m between trunks at x −20).
            Assert.That(wind.Belts, Has.Length.EqualTo(2));
            CanopyStrip west = wind.Belts[0].Strip, east = wind.Belts[1].Strip;
            Assert.That(west.CentreX + west.Length / 2, Is.LessThan(-20 - 2), "the gap stays open");
            Assert.That(east.CentreX - east.Length / 2, Is.GreaterThan(-20 + 2));
            Assert.That(wind.Belts[0].Volumes + wind.Belts[1].Volumes, Is.EqualTo(belt), "every plant of the belt in one section");
            foreach (MapWind.Belt b in wind.Belts)
            {
                Assert.That(Math.Abs(b.Strip.AxisDeg - 90), Is.LessThan(0.5), "along the belt (east)");
                Assert.That(b.Strip.Width, Is.InRange(14, 20), "the rows' 12.7 m and the crowns");
                Assert.That(b.Strip.Height, Is.InRange(8, b.TallestTop), "between the young trees and the ash");
                Assert.That(b.Strip.FrontalAreaIndex, Is.InRange(0.5, 3), "a dense belt");
                Assert.That(b.AerodynamicPorosity, Is.InRange(0.2, 0.5), "a living multi-row belt: porous, no standing bubble");
            }
            Assert.That(wind.Wakes.Volumes.Length, Is.EqualTo(map.WindVolumes.Length - belt + 2), "the belt's plants never in the wake grid");
            Assert.That(Array.IndexOf(wind.Wakes.Volumes, wind.Belts[0].Obstacle), Is.GreaterThanOrEqualTo(0));

            // Through K§10.3's rule a path across a section reads α.
            WakeObstacles o = wind.Wakes.Obstacles();
            double path = 1;
            for (double z = -12; z <= 12; z += 1)
            {
                int k = o.CellAt(-80, z);
                if (o.IsObstacle(k)) path *= Math.Sqrt(o.Porosity(k));
            }
            Assert.That(path, Is.EqualTo(wind.Belts[0].AerodynamicPorosity).Within(0.05), "the belt's bleed across it");

            // ForMap and the header: the layout and the strips go into the log and back.
            WindSetup setup = WindSetup.ForMap(map, WindLevel.Severe);
            Assert.That(setup.Canopies, Has.Length.EqualTo(2));
            Assert.That(WindSetup.ForMap(map, WindLevel.Still).Wakes, Is.Null, "still air has no wakes");
            FlightSetup flight = WindTargets.BenchFlight(WindLevel.Severe, 5);
            flight.Wind = setup;
            var core = new FlightCore(flight);
            LogHeader h = LogHeader.For(flight, core);
            core.Wind.Dispose();
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "test-range";
            LogHeader back = LogHeader.Read(new MemoryStream(h.Encode()));
            Assert.That(back.Wind.Wakes.Volumes, Is.EqualTo(setup.Wakes.Volumes));
            Assert.That(back.Wind.Canopies, Has.Length.EqualTo(2));
            Assert.That(back.Wind.Canopies[1].FrontalAreaIndex, Is.EqualTo(setup.Canopies[1].FrontalAreaIndex));
        }

        [Test]
        public void BehindH01DTheWakeIsFackrellsAndPererasAtSevere()
        {
            MapWorld map = Map;
            ulong seed = Seed(WindLevel.Severe, 0, 40, 31); // from the north: its lee is the open field south of the plots
            using var field = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(seed));
            var open = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed));
            double from = field.Wakes.Active.FromDeg;
            double dx = -Math.Sin(from * DegToRad), dz = -Math.Cos(from * DegToRad);
            Footprint(map, "H01_D", dx, dz, out double front, out double lee, out double c0, out double c1, out double H);
            double W = c1 - c0, L = lee - front, cm = (c0 + c1) / 2;
            double xr = 1.8 * (W / H) / (Math.Pow(L / H, 0.3) * (1 + 0.24 * W / H)); // Fackrell (1984)
            Double3 At(double X, double c, double Z)
            {
                double a = lee + X * H;
                return new Double3(a * dx - c * dz, Z * H, a * dz + c * dx);
            }
            // The house's path porosity, the wake grid's input from its volumes (K§10.3), on its centre line at 5 H.
            WakeObstacles o = field.Wakes.Active.Obstacles;
            int cell = o.CellAt(At(5, cm, 0).X, At(5, cm, 0).Z);
            field.Wakes.Active.Describe(cell % o.NX, cell / o.NX, out _, out double gridH, out _, out double beta, out _, out _, out _);

            // Perera's far wake: X 5–15 H, Z 0.3–0.55 H (below the reduction's peak), the middle 60 % of the width. The 2 m
            // grid holds the house as the cells its volumes reach (K§10.3), its footprint half a cell wider on every side.
            const double half = WakeObstacles.Cell / 2;
            var far = new List<Double3>();
            var farExpected = new List<double>();
            for (double X = 5; X <= 15; X += 1)
                for (double f = -0.3; f <= 0.31; f += 0.15)
                    foreach (double Z in new[] { 0.3, 0.42, 0.55 })
                    {
                        far.Add(At(X, cm + f * W, Z));
                        double x = X * H - half;
                        farExpected.Add(1 - Spread(cm + f * W, c0 - half, c1 + half, x) * Perera(x / H, Z, beta, H));
                    }
            // Fackrell's cavity: at 0.25 H along the middle 40 %, every 0.1 X_R to 2 X_R.
            var cavity = new List<Double3>();
            for (int i = 1; i <= 20; i++)
                for (double f = -0.2; f <= 0.21; f += 0.1)
                    cavity.Add(At(0.1 * i * xr, cm + f * W, 0.25));
            // The turbulence: the bluff body's boost, 1 + 2·φ·g(X), g peaking at 4 H (WakeGrid's labelled, assumed), at 0.5 H.
            var boost = new List<Double3>();
            var boostExpected = new List<double>();
            for (double X = 2; X <= 8; X += 0.5)
            {
                boost.Add(At(X, cm, 0.5));
                double r = X / 4;
                boostExpected.Add(1 + 2.0 * Spread(cm, c0, c1, X * H) * r * r * Math.Exp(2 * (1 - r)));
            }

            foreach (bool wrong in new[] { false, true })
            {
                WindField w = wrong ? new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed)) : field;
                Measure(w, open, far, out double[] u, out _);
                Measure(w, open, cavity, out double[] uc, out _);
                Measure(w, open, boost, out _, out double[] t);
                double bias = 0, rms = 0;
                for (int i = 0; i < u.Length; i++)
                {
                    bias += u[i] - farExpected[i];
                    rms += (u[i] - farExpected[i]) * (u[i] - farExpected[i]);
                }
                bias /= u.Length;
                rms = Math.Sqrt(rms / u.Length);
                double meanFar = 0, meanExpected = 0;
                for (int i = 0; i < u.Length; i++) { meanFar += u[i] / u.Length; meanExpected += farExpected[i] / u.Length; }
                // The measured reattachment: where the band's mean, reversed before, crosses back to forward for good
                // (interpolated between its 0.1 X_R steps).
                double reattach = double.NaN, deepest = 0, last = 0;
                bool reversed = false;
                for (int i = 0; i < 20; i++)
                {
                    double m = 0;
                    for (int j = 0; j < 5; j++) m += uc[i * 5 + j] / 5;
                    deepest = Math.Min(deepest, m);
                    if (m < 0) { reversed = true; reattach = double.NaN; }
                    else if (reversed && double.IsNaN(reattach)) reattach = 0.1 * i + 0.1 * -last / (m - last);
                    last = m;
                }
                double ratio = 0, peak = 0;
                for (int i = 0; i < t.Length; i++)
                {
                    ratio += (t[i] - 1) / (boostExpected[i] - 1) / t.Length;
                    peak = Math.Max(peak, t[i]);
                }
                TestContext.Out.WriteLine($"{(wrong ? "known-wrong, no map obstacles" : "the map's wind")}, seed {seed}, U {field.MeanSpeed:F2} m/s from {from:F1}°: "
                    + $"H01_D H {H:F2} m (grid {gridH:F2}), W {W:F2} m, L {L:F2} m, β {beta:F4}; Fackrell's X_R {xr:F2} H");
                TestContext.Out.WriteLine($"  far wake 5–15 H: u/U {meanFar:F3} against Perera's {meanExpected:F3} (bias {bias:+0.000;-0.000}, rms {rms:F3}, {u.Length} points × 300 times)");
                TestContext.Out.WriteLine($"  cavity at 0.25 H: deepest {deepest:F3}, forward again at {reattach:F2} X_R");
                TestContext.Out.WriteLine($"  turbulence 2–8 H at 0.5 H: (T − 1) over the boost's {ratio:F3}, peak T {peak:F2}");
                if (!wrong)
                {
                    Assert.That(Math.Abs(bias), Is.LessThan(0.04), "Perera's far wake");
                    Assert.That(rms, Is.LessThan(0.08));
                    Assert.That(deepest, Is.LessThan(-0.08), "reverse flow behind the house");
                    Assert.That(reattach, Is.InRange(0.8, 1.35), "reattaching at Fackrell's X_R (and the 2 m cells' half cell)");
                    Assert.That(ratio, Is.InRange(0.8, 1.2), "the bluff body's turbulence");
                    Assert.That(peak, Is.GreaterThan(2));
                }
                else
                {
                    Assert.That(Math.Abs(bias), Is.GreaterThan(0.15), "the known-wrong field has no deficit");
                    Assert.That(reversed, Is.False, "nor a cavity");
                    Assert.That(ratio, Is.LessThan(0.2), "nor a boost");
                }
            }
        }

        [Test]
        public void TheBeltsLeeIsItsPorousWake()
        {
            MapWorld map = Map;
            ulong seed = Seed(WindLevel.Severe, 180, 20, 31); // from the south: its lee is the open north field
            using var field = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(seed));
            var open = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed));
            MapWind.Belt west = MapWind.Of(map).Belts[0];
            double h = west.Strip.Height, alpha = west.AerodynamicPorosity, edge = west.Strip.CentreZ + west.Strip.Width / 2;
            double from = field.Wakes.Active.FromDeg, dx = -Math.Sin(from * DegToRad), dz = -Math.Cos(from * DegToRad);
            var points = new List<Double3>();
            var expected = new List<double>();
            var xs = new List<double>();
            for (double X = 2; X <= 15; X += 1)
                for (double x = -100; x <= -70; x += 5)
                    foreach (double Z in new[] { 0.3, 0.45 })
                    {
                        // X heights downwind of the leeward edge, along the wind (it crosses the belt within 30° of north).
                        double along = X * h / dz;
                        points.Add(new Double3(x + along * dx, Z * h, edge + along * dz));
                        expected.Add(X >= 5 ? 1 - Perera(X, Z, alpha, h) : double.NaN);
                        xs.Add(X);
                    }
            foreach (bool wrong in new[] { false, true })
            {
                WindField w = wrong ? new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed)) : field;
                Measure(w, open, points, out double[] u, out double[] t);
                double bias = 0, low = double.PositiveInfinity, boost = 0;
                int nf = 0, nb = 0;
                for (int i = 0; i < u.Length; i++)
                {
                    low = Math.Min(low, u[i]);
                    if (xs[i] >= 5) { bias += u[i] - expected[i]; nf++; }
                    if (xs[i] >= 5 && xs[i] <= 15) { boost += t[i]; nb++; }
                }
                bias /= nf;
                boost /= nb;
                TestContext.Out.WriteLine($"{(wrong ? "known-wrong, no map obstacles" : "the map's wind")}, seed {seed} from {from:F1}°: the west section's lee (h {h:F2} m, α {alpha:F3}): "
                    + $"u/U against Perera's at 5–15 H bias {bias:+0.000;-0.000}, lowest u/U 2–15 H {low:F3}, mean T 5–15 H {boost:F2}");
                if (!wrong)
                {
                    Assert.That(Math.Abs(bias), Is.LessThan(0.05), "Perera's far wake at the belt's α");
                    Assert.That(low, Is.GreaterThan(0.1), "porous: no reverse flow behind the belt");
                    Assert.That(boost, Is.InRange(1.6, 2.4), "the shelterbelt literature's about 2× over 5–15 H");
                }
                else
                {
                    Assert.That(Math.Abs(bias), Is.GreaterThan(0.15));
                    Assert.That(boost, Is.LessThan(1.1));
                }
            }
        }

        [Test]
        public void InsideTheBeltTheCanopyFlows()
        {
            MapWorld map = Map;
            const ulong seed = 11;
            using var field = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(seed));
            var open = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed));
            CanopyFlow c = field.Canopies[0];
            double h = c.Height, ustar = c.FrictionVelocity;
            Double3 along = WindField.Toward(field.FromDeg, 1), across = new Double3(-along.Z, 0, along.X);
            double[] zs = { 0.25, 0.5, 0.75, 1.0 };

            foreach (bool wrong in new[] { false, true })
            {
                // Each pass its own map field: the wake grid's worker runs forward in steps only.
                using var map2 = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(seed));
                WindField reference = wrong ? map2 : field;
                WindField flown = wrong ? new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed)) : field;
                // The middle of both sections (plan weight ≥ 0.9), 40 snapshots 10 s apart: frozen eddies, space for time.
                // The open field of the same seed has the same open modes' bits: less its mean, they are the open turbulence.
                var mean = new double[zs.Length];
                var expected = new double[zs.Length];
                double su = 0, sv = 0, sw = 0, weight = 0, total = 0;
                int n = 0, top = 0;
                for (long step = 0; step < 400000; step += 10000)
                {
                    flown.Begin(step, default, 0);
                    if (wrong) reference.Begin(step, default, 0);
                    open.Begin(step, default, 0);
                    if (reference.Wakes.Fading != null) continue;
                    Double3 m = reference.Mean, wind = m * (1 / m.Length());
                    foreach (CanopyFlow s in reference.Canopies)
                        for (double a = -s.Strip.Length / 2 + 15; a <= s.Strip.Length / 2 - 15; a += 0.9)
                            foreach (double b in new[] { -1.5, 0, 1.5 })
                                for (int k = 0; k < zs.Length; k++)
                                {
                                    var p = new Double3(s.Strip.CentreX + a, zs[k] * h, s.Strip.CentreZ + b);
                                    double ws = s.Weight(p);
                                    if (ws < 0.9) continue;
                                    double P = Profile(field, p.Y), r = s.Decay(p.Y), taper = Math.Min(1, (p.Y - field.Preset.Displacement) / field.Preset.TaperHeight);
                                    reference.Wakes.Active.Sample(p.X, p.Z, p.Y, out WakeSample g);
                                    double rule = (1 - ws) * P * g.Speed + ws * s.Speed(p.Y) / field.MeanSpeed;
                                    Double3 v = flown.At(p), turbulence = open.At(p) - m * P;
                                    // The mean: less the open turbulence as the canopy scales it and the canopy's own eddies (the
                                    // known-wrong field's: all of its turbulence, its mean the open profile's).
                                    Double3 still = wrong ? v - turbulence : v - turbulence * (1 - ws * (1 - r)) - s.Eddies(p) * (taper * r * Math.Sqrt(ws * s.Layer(p.Y)));
                                    mean[k] += Double3.Dot(still, wind); // along the mean wind of the moment
                                    expected[k] += m.Length() * rule;
                                    if (k == zs.Length - 1)
                                    {
                                        // At h the canopy's eddies, √w of them, are the field less the open field and the rule's mean.
                                        Double3 e = (v - open.At(p) - m * ((wrong ? P : rule) - P)) * (1 / Math.Sqrt(ws));
                                        su += Math.Pow(Double3.Dot(e, along), 2);
                                        sv += Math.Pow(Double3.Dot(e, across), 2);
                                        sw += e.Y * e.Y;
                                        total += Math.Pow(Double3.Dot(v, along), 2);
                                        weight += ws;
                                        top++;
                                    }
                                    if (k == 0) n++;
                                }
                }
                double tu = Math.Sqrt(su / top + field.SigmaH * field.SigmaH) / ustar, tv = Math.Sqrt(sv / top + field.SigmaH * field.SigmaH) / ustar,
                    tw = Math.Sqrt(sw / top + field.SigmaV * field.SigmaV) / ustar;
                double U(int k) => mean[k] / n;
                double E(int k) => expected[k] / n;
                TestContext.Out.WriteLine($"{(wrong ? "known-wrong, no map obstacles" : "the map's wind")}, seed {seed}, U {field.MeanSpeed:F2} m/s: h {h:F2} m, u* {ustar:F3} m/s, "
                    + $"α {c.Attenuation:F3}, plan weight {weight / top:F3}; at h the eddies' σ/u* {tu:F2} / {tv:F2} / {tw:F2} with the open field's (the canopy's "
                    + $"{field.Preset.CanopySigmaU} / {field.Preset.CanopySigmaV} / {field.Preset.CanopySigmaW}); U(z)/U(h) at 0.25 / 0.5 / 0.75 h {U(0) / U(3):F3} / "
                    + $"{U(1) / U(3):F3} / {U(2) / U(3):F3}, the canopy's e^(α(z/h − 1)) {Math.Exp(c.Attenuation * -0.75):F3} / {Math.Exp(c.Attenuation * -0.5):F3} / "
                    + $"{Math.Exp(c.Attenuation * -0.25):F3}; U(h) {U(3):F2} m/s against the canopy's u*/β {c.TopSpeed:F2}; {n} points a height");
                if (!wrong)
                {
                    Assert.That(tu, Is.InRange(1.8, 2.2), "σ_u/u* at the canopy's top");
                    Assert.That(tv, Is.InRange(1.45, 1.75), "σ_v/u*");
                    Assert.That(tw, Is.InRange(0.95, 1.25), "σ_w/u*");
                    for (int k = 0; k < zs.Length; k++)
                        Assert.That(U(k) / E(k), Is.EqualTo(1).Within(1e-6), "the rule's mean at " + zs[k] + " h");
                    for (int k = 0; k < 3; k++)
                        Assert.That(U(k) / U(3), Is.InRange(0.85 * Math.Exp(c.Attenuation * (zs[k] - 1)), 1.35 * Math.Exp(c.Attenuation * (zs[k] - 1))),
                            "the canopy's exponential, with the edge's share of the open field");
                    Assert.That(U(3) / c.TopSpeed, Is.InRange(0.9, 1.2), "U(h) is the canopy's u*/β");
                }
                else
                {
                    Assert.That(tu, Is.LessThan(1.5), "the open field's σ_u/u* is the surface layer's");
                    Assert.That(U(0) / U(3), Is.GreaterThan(1.5 * Math.Exp(c.Attenuation * -0.75)), "the log law, not the canopy's");
                }
            }
        }

        [Test]
        public void OutsideTheStripsTheMapsFieldIsItsWakesBitForBit()
        {
            MapWorld map = Map;
            MapWind m = MapWind.Of(map);
            using var full = new WindField(WindSetup.ForMap(map, WindLevel.Windy), new FlightRandom(5));
            using var wakes = new WindField(new WindSetup { Level = WindLevel.Windy, Wakes = m.Wakes }, new FlightRandom(5));
            int outside = 0, inside = 0;
            foreach (long step in new long[] { 0, 777, 2500 })
            {
                full.Begin(step, default, 0);
                wakes.Begin(step, default, 0);
                for (double x = -250; x <= 250; x += 13)
                    for (double z = -60; z <= 60; z += 3.1)
                        foreach (double y in new[] { 2.0, 6, 15, 40 })
                        {
                            var p = new Double3(x, y, z);
                            bool any = false;
                            foreach (CanopyFlow c in full.Canopies) any |= c.Weight(p) > 0 && y < c.BlendingHeight;
                            if (any) { inside++; Assert.That(full.At(p), Is.Not.EqualTo(wakes.At(p))); }
                            else { outside++; Assert.That(full.At(p), Is.EqualTo(wakes.At(p)), "outside every strip at " + p); }
                        }
            }
            Assert.That(inside, Is.GreaterThan(100));
            Assert.That(outside, Is.GreaterThan(1000));
        }

        [Test]
        public void TheSameSeedGivesTheSameWindWithoutAllocating()
        {
            MapWorld map = Map;
            using var a = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(21));
            using var b = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(21));
            using var other = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(22));
            // Points in the belt, over it, in its lee, behind the yard and in the open; a step's seven points.
            Double3[] points =
            {
                new Double3(-80, 5, 0), new Double3(-80, 14, 0.5), new Double3(60, 3, 20), new Double3(60, 3, -25),
                new Double3(-122, 2.5, -75), new Double3(-170, 4, -40), new Double3(200, 30, 200),
            };
            int differ = 0;
            long allocated = 0;
            for (long step = 0; step < 6000; step++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                a.Begin(step, points[0], 0);
                Double3 sum = default;
                foreach (Double3 p in points) sum = sum + a.At(p);
                if (step >= 100) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                b.Begin(step, points[0], 0);
                other.Begin(step, points[0], 0);
                Double3 again = default;
                foreach (Double3 p in points)
                {
                    again = again + b.At(p);
                    if (!other.At(p).Equals(a.At(p))) differ++;
                }
                Assert.That(again, Is.EqualTo(sum), "step " + step);
            }
            Assert.That(allocated, Is.EqualTo(0), "the step only samples");
            Assert.That(differ, Is.GreaterThan(6000 * points.Length * 9 / 10), "another seed, another wind");
        }
    }
}
