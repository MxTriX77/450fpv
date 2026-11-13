using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7c-wind, the map's wind (WindSetup.ForMap, MapWind): the test range's own obstacles in the field. Its placed wind
    // volumes give the wake grid's obstacles, and its belt (the "posadka" rows) is read once, as wooded strips with one
    // porous obstacle each for their lee, so the belt is never counted twice. The numbers behind H01_I are graded against
    // Fackrell's reattachment and Perera's far wake, evaluated here from the house's own volumes (since w7-farmland-wind 1.2:
    // H01_D's stripped roof leaves its frontal porosity about 0.25, a porous house in the single-porosity wake model, and it
    // stands in the posadka's lee, whose wake now goes on over its own); inside the belt against
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

            // A lane across a section, the wind from the north, reads α (the box's porosity over its chord across the width).
            WakeObstacles o = wind.Wakes.Obstacles();
            int edge = o.CellAt(-80, -12);
            while (!o.IsObstacle(edge)) edge += o.NX;
            o.Centre(edge, out double ex, out double ez);
            double lane = o.Transmission(o.NewLane(), ex, ez, 0, 1, 1, 0, double.NegativeInfinity, o.Base(edge), o.Top(edge));
            Assert.That(lane, Is.EqualTo(wind.Belts[0].AerodynamicPorosity).Within(1e-7), "the belt's bleed across it (its axis 0.012° off east: a chord W/cos)");

            // ForMap and the header: the layout and the strips go into the log and back. The setup's cost (reported): the
            // obstacles from the map, then the field's first two grids, on the caller's thread before the flight.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            WindSetup setup = WindSetup.ForMap(map, WindLevel.Severe);
            double forMap = clock.Elapsed.TotalMilliseconds;
            using (var timed = new WindField(setup, new FlightRandom(5)))
                TestContext.Out.WriteLine($"ForMap {forMap:F1} ms; the field with its worker's two grids {clock.Elapsed.TotalMilliseconds - forMap:F0} ms");
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
        public void BehindH01ITheWakeIsFackrellsAndPererasAtSevere()
        {
            MapWorld map = Map;
            ulong seed = Seed(WindLevel.Severe, 0, 40, 31); // from the north: its lee is the open field south of the plots
            using var field = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(seed));
            var open = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed));
            double from = field.Wakes.Active.FromDeg;
            double dx = -Math.Sin(from * DegToRad), dz = -Math.Cos(from * DegToRad);
            Footprint(map, "H01_I", dx, dz, out double front, out double lee, out double c0, out double c1, out double H);
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

            // The cavity's reattachment is read from the mean flow: the same fields without turbulence (σ 0, so no open modes
            // and no wake eddies, both zero-mean). At 0.25 H the wake eddies carry √(T² − 1)·σ_u, and σ_u over the local
            // mean is 0.40 there over class 4's z0 (D-060; 0.27 over the stubble's): 30 s of samples read them into the
            // band's mean, which put the crossing at 1.25 X_R before D-060 and 1.45 after it, against the mean flow's 1.07
            // in both (the merge's check, p7c-wind--map-wakes onto d-060).
            WindPreset still = WindPreset.Of(WindLevel.Severe);
            still.Intensity = 0;
            using var quiet = new WindField(WindSetup.ForMap(map, WindLevel.Severe), new FlightRandom(seed), still);
            var quietOpen = new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed), still);
            foreach (bool wrong in new[] { false, true })
            {
                WindField w = wrong ? new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed)) : field;
                Measure(w, open, far, out double[] u, out _);
                Measure(wrong ? new WindField(WindSetup.Of(WindLevel.Severe), new FlightRandom(seed), still) : quiet, quietOpen, cavity, out double[] uc, out _);
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
                    + $"H01_I H {H:F2} m (grid {gridH:F2}), W {W:F2} m, L {L:F2} m, β {beta:F4}; Fackrell's X_R {xr:F2} H");
                TestContext.Out.WriteLine($"  far wake 5–15 H: u/U {meanFar:F3} against Perera's {meanExpected:F3} (bias {bias:+0.000;-0.000}, rms {rms:F3}, {u.Length} points × 300 times)");
                TestContext.Out.WriteLine($"  cavity at 0.25 H, the mean flow: deepest {deepest:F3}, forward again at {reattach:F2} X_R");
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
                long before = AllocProbe.Before();
                a.Begin(step, points[0], 0);
                Double3 sum = default;
                foreach (Double3 p in points) sum = sum + a.At(p);
                if (step >= 100) allocated += AllocProbe.After(before);
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
            Assert.That(allocated, Is.EqualTo(0), "the step only samples" + AllocProbe.Report());
            Assert.That(differ, Is.GreaterThan(6000 * points.Length * 9 / 10), "another seed, another wind");
        }

        // The map's wind at seven points over 4 s of Severe, every bit of it: pinned on .NET 10 and checked on Mono by the
        // Unity EditMode run (the same test), so the field over the map is the same bits on both runtimes. Model 21 on
        // p7c-wind--map-wakes: 0x9E8D9108B60D91B8. Model 22 (merged onto D-060's field): re-pinned; with z0 0.03 m, σ_u/U
        // 0.135 and the 1 m cut (a temporary switch, reverted) it is model 21's bit for bit, so D-060's field is the change.
        // Model 88 (w7-farmland-wind 1.2, the wake grid's version 2): re-pinned; with version 1 it is the pin before.
        const ulong PinnedMapWind = 0x5406364FFBB28D46UL, PinnedMapWindVersion1 = 0x0EF8A67AC398C411UL;

        [Test]
        public void TheMapsWindIsTheSameBitsOnEveryRuntime()
        {
            foreach (int version in new[] { 1, WakeGrid.Version })
                Assert.That(WindOverMap(version), Is.EqualTo(version == 1 ? PinnedMapWindVersion1 : PinnedMapWind), "the wake grid's version " + version);
        }

        static ulong WindOverMap(int version)
        {
            WindSetup setup = WindSetup.ForMap(Map, WindLevel.Severe);
            setup.Wakes.Version = version;
            using var wind = new WindField(setup, new FlightRandom(21));
            Double3[] points =
            {
                new Double3(-80, 5, 0), new Double3(-80, 14, 0.5), new Double3(60, 3, 20), new Double3(60, 3, -25),
                new Double3(-122, 2.5, -75), new Double3(-170, 4, -40), new Double3(-20, 6, 12),
            };
            ulong hash = WorldHash.Offset;
            for (long step = 0; step < 4000; step++)
            {
                wind.Begin(step, points[0], 0);
                foreach (Double3 p in points)
                {
                    Double3 v = wind.At(p);
                    foreach (double x in new[] { v.X, v.Y, v.Z })
                    {
                        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(x);
                        for (int b = 0; b < 8; b++) hash = (hash ^ (byte)(bits >> 8 * b)) * 0x100000001B3UL;
                    }
                }
            }
            TestContext.Out.WriteLine($"the map's wind, Severe seed 21, 4000 steps at 7 points, the wake grid's version {version}: 0x{hash:X16}");
            return hash;
        }

        // ---- The wind inside a cut (p7c-wind 1.10, design 10, D-071): E01's run on the test range ----

        // QA's spots (review-integration.md S5, reproduced at model 26 to its printed digits): E01's run floor at
        // RangeScenarios.Run, and the plane at the track's start point.
        static Double3 PlaneSpot => RangeScenarios.Start("track").Position;

        // The middle of E01's straight leg between its centre line's points 24 and 26, (61.214, −14.210) to (62.974,
        // −13.720): the opening's axis there and the across direction (the frame's n̂).
        static readonly Double3 Leg = new Double3(61.654, 0, -14.0875);
        static readonly Double3 LegAxis = new Double3(1.760, 0, 0.490) * (1 / Math.Sqrt(1.760 * 1.760 + 0.490 * 0.490));
        static Double3 LegAcross => new Double3(-LegAxis.Z, 0, LegAxis.X);

        // The field's ground under a body's origin as the core gives it (FlightCore.BeginWind): over a hole the soil a ray
        // straight down meets (FlightWorld.SoilUnder), elsewhere SampleGround's height.
        sealed class Ground
        {
            readonly FlightWorld _w = new FlightWorld(Map);
            readonly XZ[] _xz = new XZ[1];
            readonly GroundSample[] _g = new GroundSample[1];
            readonly Ray[] _ray = new Ray[1];
            readonly RayHit[] _hit = new RayHit[1];

            public double Under(Double3 p)
            {
                _xz[0] = new XZ(p.X, p.Z);
                _w.SampleGround(_xz, _g);
                return (_g[0].Flags & GroundFlags.Hole) != 0 ? _w.SoilUnder(p, _ray, _hit, out _) : _g[0].Height;
            }
        }

        // The map's wind without its cuts: model 26's field, the open profile restarted at a cut's floor (the known-wrong).
        static WindSetup WithoutCuts(WindLevel level)
        {
            WindSetup s = WindSetup.ForMap(Map, level);
            s.Cuts = null;
            return s;
        }

        [Test]
        public void TheMapsCutsAreItsHoleRecords()
        {
            CutSpec[] cuts = MapWind.Cuts(Map);
            Assert.That(cuts, Has.Length.EqualTo(Map.Cuts.Length).And.Length.EqualTo(4));
            for (int k = 0; k < cuts.Length; k++)
            {
                CutRecord r = Map.Cuts[k];
                Assert.That(cuts[k].CentreLine, Is.EqualTo(r.CentreLine));
                Assert.That(new[] { cuts[k].Width, cuts[k].Depth, cuts[k].EndFirst, cuts[k].EndLast }, Is.EqualTo(new[] { r.LipWidth, r.Depth, r.EndFirst, r.EndLast }));
                Assert.That(cuts[k].Covered, Is.EqualTo(r.Covered));
            }
            Assert.That(WindSetup.ForMap(Map, WindLevel.Calm).Cuts, Has.Length.EqualTo(4));
            Assert.That(WindSetup.ForMap(Map, WindLevel.Steady).Cuts, Is.Null, "steady air never reads the ground");

            // The frame at E01's run, by its wall (D the run's own, never the wall's local depth), its niche's mouth (the run
            // takes it) and deep in the niche (past the run's reach), and Z04's shaft over its stair (its own 1.8 m).
            using var field = new WindField(WindSetup.ForMap(Map, WindLevel.Severe), new FlightRandom(3));
            var ground = new Ground();
            Double3 niche = new Double3(-0.363, 0, 1.031) * (1 / 1.093); // niche_0's axis, from its first point by the run's line
            foreach ((string where, Double3 p, bool active, bool covered, double depth) in new[]
            {
                ("E01's run floor", RangeScenarios.Run + new Double3(0, 0.5, 0), true, false, 1.5),
                ("the leg's middle", Leg + new Double3(0, -0.75, 0), true, false, 1.5),
                ("the leg over its wall's slope", Leg + LegAcross * 0.42 + new Double3(0, 0.5, 0), true, false, 1.5),
                ("niche_0's mouth in the run", new Double3(52.98, -0.7, -14.102) + niche * -0.2, true, false, 1.5),
                ("deep in niche_0", new Double3(52.98, -1.1, -14.102) + niche * 0.75, true, true, double.NaN),
                ("Z04's shaft over its stair", new Double3(-162.75, -1.0, -60.25), true, false, 1.8),
                ("the plane by the run", Leg + LegAcross * 3, false, false, double.NaN),
            })
            {
                double g = ground.Under(p);
                field.Begin(0, p, g);
                CutFrame f = field.Cut;
                TestContext.Out.WriteLine($"{where}: ground {g:F4}; frame {(f.Active ? $"{(f.Covered ? "covered" : "open")}, lip {f.Lip}, W {f.Width:F4}, D {f.Depth:F4}, S {f.Length:F3}" : "none")}");
                Assert.That(f.Active, Is.EqualTo(active), where);
                if (!active) continue;
                Assert.That(f.Covered, Is.EqualTo(covered), where);
                if (!double.IsNaN(depth)) Assert.That(f.Depth, Is.EqualTo(depth).Within(1e-9), where);
            }
        }

        // The plan distance from (x, z) to a centre line that runs on past its end points by its ends.
        static double FromLine(Double3[] line, double endFirst, double endLast, double x, double z)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                double dx = line[i + 1].X - line[i].X, dz = line[i + 1].Z - line[i].Z, len = Math.Sqrt(dx * dx + dz * dz);
                double px = x - line[i].X, pz = z - line[i].Z, u = (px * dx + pz * dz) / len, v = (pz * dx - px * dz) / len;
                double lo = i == 0 ? -endFirst : 0, hi = i + 2 == line.Length ? len + endLast : len, du = u - Math.Min(Math.Max(u, lo), hi);
                best = Math.Min(best, Math.Sqrt(du * du + v * v));
            }
            return best;
        }

        [Test]
        public void ACutsOpeningIsTheWorldsOwn()
        {
            // Over E01's run and Z04's shaft, every 1 cm out to 0.75 m from the centre line and past its ends, the body 0.5 m
            // over the lip: the frame holds the origin exactly where the world's ground under it lies below the lip (the
            // world's own opening, crumble and all), always an open cut's from above (the niches' roofs are at the lip), and
            // that opening lies inside the cuts' reach.
            using var field = new WindField(new WindSetup { Level = WindLevel.Calm, Cuts = MapWind.Cuts(Map) }, new FlightRandom(1));
            var ground = new Ground();
            foreach (int c in new[] { 1, 0 })
            {
                CutRecord r = Map.Cuts[c];
                Double3[] line = r.CentreLine;
                Assert.That(Array.TrueForAll(line, p => p.Y == 0), r.Record + "'s lip is the plane's 0");
                int samples = 0, open = 0, wrong = 0;
                double reach = 0;
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    Double3 a = line[i], b = line[i + 1], t = (b - a) * (1 / (b - a).Length()), n = new Double3(-t.Z, 0, t.X);
                    double lo = i == 0 ? -r.EndFirst - 0.3 : 0, hi = (b - a).Length() + (i + 2 == line.Length ? r.EndLast + 0.3 : 0);
                    for (int iu = 0; lo + 0.01 * iu <= hi; iu++)
                        for (int iv = -75; iv <= 75; iv++)
                        {
                            Double3 o = a + t * (lo + 0.01 * iu) + n * (0.01 * iv) + new Double3(0, 0.5, 0);
                            double g = ground.Under(o);
                            field.Begin(0, o, g);
                            samples++;
                            bool below = g > double.NegativeInfinity && g < 0;
                            if (below) { open++; reach = Math.Max(reach, FromLine(line, r.EndFirst, r.EndLast, o.X, o.Z)); }
                            if (field.Cut.Active != below || (below && field.Cut.Covered)) wrong++;
                        }
                }
                TestContext.Out.WriteLine($"{r.Record}: the frame holds the origin at {open} of {samples} points, where the ground is below the lip, wrong at {wrong}; "
                    + $"the world's opening reaches {reach:F4} m from the line, {reach - r.LipWidth / 2:F4} past W/2 (the reach {CutFrame.OpeningReach})");
                Assert.That(wrong, Is.EqualTo(0), r.Record + ": the opening is the world's");
                Assert.That(reach, Is.LessThan(r.LipWidth / 2 + CutFrame.OpeningReach), r.Record + ": the world's opening lies inside the cut's reach");
            }
        }

        [Test]
        public void AcrossE01sRunTheCutShelters()
        {
            // QA's measurement (S5): the map's Severe wind on seeds 1–3 for 120 s (every 10 ms), the mean horizontal speed at
            // 0.5 and 1.0 m over E01's run floor and over the plane at the track. The known-wrong is model 26's field.
            var ground = new Ground();
            Double3 run = RangeScenarios.Run, plane = PlaneSpot;
            double floor = ground.Under(run + new Double3(0, 1, 0));
            double lo = double.PositiveInfinity, hi = 0, lo26 = double.PositiveInfinity, hi26 = 0;
            for (ulong seed = 1; seed <= 3; seed++)
                foreach (bool wrong in new[] { false, true })
                {
                    using var field = new WindField(wrong ? WithoutCuts(WindLevel.Severe) : WindSetup.ForMap(Map, WindLevel.Severe), new FlightRandom(seed));
                    var inRun = new double[2];
                    var onPlane = new double[2];
                    int n = 0;
                    for (long step = 0; step < 120000; step += 10, n++)
                    {
                        field.Begin(step, run + new Double3(0, 0.75, 0), floor);
                        for (int k = 0; k < 2; k++)
                        {
                            Double3 v = field.At(new Double3(run.X, floor + 0.5 * (k + 1), run.Z));
                            inRun[k] += Math.Sqrt(v.X * v.X + v.Z * v.Z);
                        }
                        field.Begin(step, plane + new Double3(0, 0.75, 0), 0);
                        for (int k = 0; k < 2; k++)
                        {
                            Double3 v = field.At(new Double3(plane.X, 0.5 * (k + 1), plane.Z));
                            onPlane[k] += Math.Sqrt(v.X * v.X + v.Z * v.Z);
                        }
                    }
                    double r05 = inRun[0] / onPlane[0], r10 = inRun[1] / onPlane[1];
                    TestContext.Out.WriteLine($"{(wrong ? "known-wrong, model 26's open profile from the floor" : "the cavity")}, Severe seed {seed} ({field.MeanSpeed:F2} m/s from "
                        + $"{field.FromDeg:F0}°): the run / the plane at 0.5 m {inRun[0] / n:F2} / {onPlane[0] / n:F2} m/s ({r05:F3}), at 1.0 m {inRun[1] / n:F2} / {onPlane[1] / n:F2} ({r10:F3})");
                    if (wrong)
                    {
                        lo26 = Math.Min(lo26, Math.Min(r05, r10));
                        hi26 = Math.Max(hi26, Math.Max(r05, r10));
                    }
                    else
                    {
                        lo = Math.Min(lo, Math.Min(r05, r10));
                        hi = Math.Max(hi, Math.Max(r05, r10));
                    }
                }
            TestContext.Out.WriteLine($"the run over the plane: the cavity {lo:F3}–{hi:F3}; model 26 {lo26:F3}–{hi26:F3}");
            Assert.That(hi, Is.LessThanOrEqualTo(0.25), "the cut shelters its floor");
            Assert.That(hi26, Is.GreaterThan(0.25), "the known-wrong fails");
            Assert.That(lo26, Is.GreaterThan(0.4), "QA's 0.47–1.20");
        }

        // A level drone's hubs (drone.json) with its nose along `axis`: body +Z the nose, +X its right, turned about +Y.
        static Double3[] Hubs(Double3 axis)
        {
            PropulsionSpec propulsion = RepositoryDrone.Spec().BuildPropulsion();
            var right = new Double3(axis.Z, 0, -axis.X);
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++)
            {
                Double3 b = propulsion.Motors[i].Position;
                hubs[i] = right * b.X + new Double3(0, b.Y, 0) + axis * b.Z;
            }
            return hubs;
        }

        static WindPreset CalmStill()
        {
            WindPreset calm = WindPreset.Of(WindLevel.Calm);
            calm.Intensity = 0;
            return calm;
        }

        [Test]
        public void OverE01sOpeningTheLipIsTheGround()
        {
            // The drone carried across E01's run at 0.5 m over the plane at 1 m/s (1 mm a step), level, its nose along the
            // run, in the map's Calm wind with σ 0, then held 0.5 m over the opening's middle: over the opening (the world's,
            // where the ground under the body lies below the lip) the field at every hub is the field of the same setup with
            // the lip passed as the ground, bit for bit. The known-wrong,
            // model 26's field, reads the field over the floor there.
            Double3[] hubs = Hubs(LegAxis);
            var ground = new Ground();
            foreach (bool wrong in new[] { false, true })
            {
                using var field = new WindField(wrong ? WithoutCuts(WindLevel.Calm) : WindSetup.ForMap(Map, WindLevel.Calm), new FlightRandom(1), CalmStill());
                using var lip = new WindField(WithoutCuts(WindLevel.Calm), new FlightRandom(1), CalmStill());
                int over = 0, same = 0;
                long step = 0;
                for (int k = 0; k <= 4000; k++, step++)
                {
                    double n = -2 + 0.001 * k;
                    Double3 o = Leg + LegAcross * n + new Double3(0, 0.5, 0);
                    double g = ground.Under(o);
                    field.Begin(step, o, g);
                    lip.Begin(step, o, 0);
                    if (!(g < 0)) continue; // over the world's opening: the ground under the body below the lip
                    for (int i = 0; i < 4; i++)
                    {
                        over++;
                        if (field.At(o + hubs[i]).Equals(lip.At(o + hubs[i]))) same++;
                    }
                }
                // Held over the opening's middle for 2 s.
                int held = 0, heldSame = 0;
                double speed = 0, lipSpeed = 0;
                for (int k = 0; k < 2000; k++, step++)
                {
                    Double3 o = Leg + new Double3(0, 0.5, 0);
                    field.Begin(step, o, ground.Under(o));
                    lip.Begin(step, o, 0);
                    for (int i = 0; i < 4; i++)
                    {
                        Double3 v = field.At(o + hubs[i]), l = lip.At(o + hubs[i]);
                        held++;
                        if (v.Equals(l)) heldSame++;
                        speed += Horizontal(v);
                        lipSpeed += Horizontal(l);
                    }
                }
                TestContext.Out.WriteLine($"{(wrong ? "known-wrong, model 26's field" : "the cavity")}, Calm seed 1 ({field.MeanSpeed:F3} m/s, σ 0): carried across, the lip's field "
                    + $"at {same} of {over} hub samples over the opening; held, {heldSame} of {held}, the hubs {speed / held:F4} m/s against the lip's {lipSpeed / held:F4}");
                if (!wrong)
                {
                    Assert.That(same, Is.EqualTo(over), "over the opening the field is the lip's, bit for bit");
                    Assert.That(heldSame, Is.EqualTo(held));
                }
                else
                {
                    Assert.That(same, Is.LessThan(over), "it reads the field over the floor");
                    Assert.That(speed, Is.GreaterThan(lipSpeed), "1.5 m higher");
                }
            }
        }

        [Test]
        public void AtE01sOpeningsEdgeNoHubStepsTheWind()
        {
            // The drone carried across E01's run at 0.5 m over the plane at 1 m/s, from 1 m one side to 1 m the other, at the
            // middle of every other centre-line segment, in the map's Calm and Severe winds with σ 0: the largest change of a
            // hub's horizontal speed in one step. The spec's bound is 0.01 m/s. The known-wrong is the opening as a strip
            // within W/2 (the mean lip width's half) of the centre line, as built first: E01's own opening leaves it by up to
            // 5 cm, and there the ground the field uses steps from the lip to the wall's slope. Model 26's field follows the
            // walls' slope at 1 mm a step: under the bound in Calm (QA's 0.2 → 0.6 m/s is that change over many steps), past
            // it in Severe.
            CutRecord run = Map.Cuts[1];
            Double3[] line = run.CentreLine;
            var ground = new Ground();
            Assert.That(Array.TrueForAll(line, p => p.Y == 0), "the run's lip is the plane's 0");
            foreach (WindLevel level in new[] { WindLevel.Calm, WindLevel.Severe })
            {
                WindPreset smooth = WindPreset.Of(level);
                smooth.Intensity = 0;
                using var field = new WindField(WindSetup.ForMap(Map, level), new FlightRandom(1), smooth);
                using var lip = new WindField(WithoutCuts(level), new FlightRandom(1), smooth);
                using var open = new WindField(WithoutCuts(level), new FlightRandom(1), smooth);
                string[] names = { "the cavity, the world's opening", "known-wrong, the strip within W/2", "model 26's field" };
                var worst = new double[3];
                var where = new string[3];
                long step = 0;
                for (int s = 0; s + 1 < line.Length; s += 2)
                {
                    Double3 a = line[s], b = line[s + 1], axis = (b - a) * (1 / (b - a).Length()), across = new Double3(-axis.Z, 0, axis.X);
                    Double3[] hubs = Hubs(axis);
                    var last = new Double3[3, 4];
                    for (int k = 0; k <= 2000; k++, step++)
                    {
                        double n = -1 + 0.001 * k;
                        Double3 o = (a + b) * 0.5 + across * n + new Double3(0, 0.5, 0);
                        double g = ground.Under(o);
                        field.Begin(step, o, g);
                        lip.Begin(step, o, 0);
                        open.Begin(step, o, g);
                        WindField strip = InStrip(run, o.X, o.Z) ? lip : open; // the first build's frame: the lip as the ground in the strip
                        for (int m = 0; m < 3; m++)
                        {
                            WindField f = m == 0 ? field : m == 1 ? strip : open;
                            for (int i = 0; i < 4; i++)
                            {
                                Double3 v = f.At(o + hubs[i]);
                                double change = Math.Abs(Horizontal(v) - Horizontal(last[m, i]));
                                if (k > 0 && change > worst[m])
                                {
                                    worst[m] = change;
                                    where[m] = $"segment {s} at {n:+0.000;-0.000} m (ground {g:F3})";
                                }
                                last[m, i] = v;
                            }
                        }
                    }
                }
                for (int m = 0; m < 3; m++)
                    TestContext.Out.WriteLine($"{level} seed 1, σ 0, {names[m]}: the largest one-step change of a hub's horizontal speed {worst[m]:F4} m/s, {where[m]}");
                Assert.That(worst[0], Is.LessThanOrEqualTo(0.01), level + ": no step at the opening's edge");
                Assert.That(worst[1], Is.GreaterThan(0.01), level + ": the strip's step where the world's opening leaves it");
                if (level == WindLevel.Calm) Assert.That(worst[2], Is.LessThanOrEqualTo(0.01), "model 26 in Calm follows the slope under the bound");
                else Assert.That(worst[2], Is.GreaterThan(0.01), "model 26 in Severe steps past the bound on the slope");
            }
        }

        // The opening as first built (the known-wrong of the edge): within W/2 of an open cut's centre line, its end segments
        // running square past their end points by its ends, with a disc of W/2 about each inner joint.
        static bool InStrip(CutRecord r, double x, double z)
        {
            Double3[] line = r.CentreLine;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                double dx = line[i + 1].X - line[i].X, dz = line[i + 1].Z - line[i].Z, len = Math.Sqrt(dx * dx + dz * dz);
                double px = x - line[i].X, pz = z - line[i].Z, u = (px * dx + pz * dz) / len, v = (pz * dx - px * dz) / len;
                double lo = i == 0 ? -r.EndFirst : 0, hi = i + 2 == line.Length ? len + r.EndLast : len;
                if ((u < lo && i == 0) || (u > hi && i + 2 == line.Length)) continue;
                double du = u - Math.Min(Math.Max(u, lo), hi);
                if (Math.Sqrt(du * du + v * v) <= r.LipWidth / 2) return true;
            }
            return false;
        }

        static double Horizontal(Double3 v) => Math.Sqrt(v.X * v.X + v.Z * v.Z);

        // A flight on the test range in its own Severe wind (the bench's airframe: propulsion and drag), held at `hold` for
        // `heldSeconds`, then climbing to 5 m over the plane for 10 s; the FNV hash of every hub's wind at every step, the
        // bytes allocated on the step after the first second, and the steps a hub flew below a cut's lip.
        static ulong FlyRange(Double3 hold, WindSetup wind, int heldSeconds, out long allocated, out int inCut)
        {
            FlightSetup setup = CrosswindScenario.Setup(RepositoryDrone.Spec(), wind, 0x450F);
            setup.World = new FlightWorld(Map);
            setup.StartPosition = hold;
            var core = new FlightCore(setup);
            try
            {
                CrosswindScenario.Arm(core);
                var pilot = new SimPilot(core.Controller, hold) { PositionIntegral = 0.1 };
                ulong hash = WorldHash.Offset;
                allocated = 0;
                inCut = 0;
                int steps = (heldSeconds + 10) * 1000;
                for (int k = 0; k < steps; k++)
                {
                    if (k == heldSeconds * 1000) pilot.Target = new Double3(hold.X, 5, hold.Z);
                    StickSample stick = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                    long before = AllocProbe.Before();
                    core.Advance(stick);
                    if (k >= 1000) allocated += AllocProbe.After(before);
                    CutFrame f = core.Wind.Cut;
                    if (f != null && f.Active && !f.Covered && core.State.Position.Y < f.Lip) inCut++;
                    for (int i = 0; i < 4; i++)
                    {
                        Double3 v = core.Trace.RotorWind[i];
                        foreach (double x in new[] { v.X, v.Y, v.Z })
                        {
                            ulong bits = (ulong)BitConverter.DoubleToInt64Bits(x);
                            for (int b = 0; b < 8; b++) hash = (hash ^ (byte)(bits >> 8 * b)) * 0x100000001B3UL;
                        }
                    }
                }
                Assert.That(core.State.Crashed, Is.False);
                return hash;
            }
            finally
            {
                core.Wind.Dispose();
            }
        }

        [Test]
        public void InE01sRunTheSameSeedGivesTheSameWindWithoutAllocating()
        {
            // Two flights with the same seed held 0.75 m over the run's floor for 60 s, then climbing out: the same wind at
            // every hub, bit for bit, and 0 B on the step. A flight that never enters a cut's opening (held over the plane
            // 3 m off the run) steps as model 26's field.
            Double3 hold = Leg + new Double3(0, -0.75, 0);
            // The second flight's allocation is the steady state's: the first one runs the step's code for the first time in
            // this process (the JIT's tiers), and reads it (reported).
            ulong a = FlyRange(hold, WindSetup.ForMap(Map, WindLevel.Severe), 60, out long first, out int inCut);
            ulong b = FlyRange(hold, WindSetup.ForMap(Map, WindLevel.Severe), 60, out long allocated, out int inCut2);
            Double3 off = Leg + LegAcross * 3 + new Double3(0, 0.75, 0);
            ulong outside = FlyRange(off, WindSetup.ForMap(Map, WindLevel.Severe), 20, out _, out int never);
            ulong outside26 = FlyRange(off, WithoutCuts(WindLevel.Severe), 20, out _, out _);
            ulong inside26 = FlyRange(hold, WithoutCuts(WindLevel.Severe), 60, out _, out _);
            TestContext.Out.WriteLine($"held in E01's run 60 s, then out: 0x{a:X16} and 0x{b:X16}, {inCut} steps below the lip, {allocated} B on the step ({first} B the first time); "
                + $"model 26's field there 0x{inside26:X16}; held off the run 0x{outside:X16}, model 26's 0x{outside26:X16} ({never} steps in a cut)");
            Assert.That(b, Is.EqualTo(a), "the same seed, the same wind at every hub");
            Assert.That(inCut, Is.GreaterThan(59000).And.EqualTo(inCut2), "the flight held in the run");
            Assert.That(allocated, Is.EqualTo(0), AllocProbe.Report());
            Assert.That(never, Is.EqualTo(0));
            Assert.That(outside, Is.EqualTo(outside26), "a flight that never enters a cut's opening steps as at model 26");
            Assert.That(inside26, Is.Not.EqualTo(a), "the cut's own flow");
        }

        // The cavity's wind in E01's run at seven points over 4 s of Severe, every bit of it, pinned on .NET 10 for the Unity
        // EditMode run on Mono (the same test), as PinnedMapWind: the body's origin 0.75 m over the floor of the leg's middle. Since
        // w7-farmland-wind 1.2 (model 88) the field around the run carries the wake grid's version 2; with version 1 it is
        // the pin before (the audit: the wake grid is the whole move).
        const ulong PinnedCutWind = 0x3A0EDD30EF795FB3UL, PinnedCutWindVersion1 = 0xC73E7B7D2F312D6EUL;

        [Test]
        public void TheCutsWindIsTheSameBitsOnEveryRuntime()
        {
            foreach (int version in new[] { 1, WakeGrid.Version })
                Assert.That(CutWind(version), Is.EqualTo(version == 1 ? PinnedCutWindVersion1 : PinnedCutWind), "the wake grid's version " + version);
        }

        static ulong CutWind(int version)
        {
            WindSetup setup = WindSetup.ForMap(Map, WindLevel.Severe);
            setup.Wakes.Version = version;
            using var wind = new WindField(setup, new FlightRandom(21));
            Double3 o = Leg + new Double3(0, -0.75, 0);
            Double3[] points =
            {
                o, o + LegAxis * 0.2 + new Double3(0, 0.03, 0), o - LegAcross * 0.2, o + LegAcross * 0.3 + new Double3(0, -0.5, 0),
                o + new Double3(0, 0.7, 0), o + new Double3(0, 1.2, 0), o + LegAxis * 0.3 + new Double3(0, 0.9, 0),
            };
            var ground = new Ground();
            double g = ground.Under(o);
            ulong hash = WorldHash.Offset;
            for (long step = 0; step < 4000; step++)
            {
                wind.Begin(step, o, g);
                foreach (Double3 p in points)
                {
                    Double3 v = wind.At(p);
                    foreach (double x in new[] { v.X, v.Y, v.Z })
                    {
                        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(x);
                        for (int b = 0; b < 8; b++) hash = (hash ^ (byte)(bits >> 8 * b)) * 0x100000001B3UL;
                    }
                }
            }
            TestContext.Out.WriteLine($"the cavity's wind in E01's run, Severe seed 21, 4000 steps at 7 points, the wake grid's version {version}: 0x{hash:X16}");
            return hash;
        }
    }
}
