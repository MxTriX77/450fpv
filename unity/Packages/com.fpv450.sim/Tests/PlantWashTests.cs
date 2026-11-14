using System;
using System.Diagnostics;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The props' wash on the ground's plants and dust (w16-plants-in-the-air 1.2, design.md §4–§5): a hover over grass at 0.5, 1, 2
    /// and 4 m against tools/flightbench/handcalc/plants_air.py (design 19's air worked independently, stem_wash.py's), the grid's
    /// patch and ring, the dust's onset per surface, determinism, 0 B and the tick's cost.
    /// </summary>
    public class PlantWashTests
    {
        const double R = 0.127, Hub = 0.1598, Mass = 4.735, Rho = 1.225;

        static void Print(string line) => TestContext.Out.WriteLine(line);

        static readonly Double3[] Motors = { new Double3(Hub, 0, -Hub), new Double3(Hub, 0, Hub), new Double3(-Hub, 0, -Hub), new Double3(-Hub, 0, Hub) };

        static double Thrust => Mass * 9.80665 / 4;

        // The stem scenarios' hover (StemWashTests): still air, flat ground at 0, the hubs h up.
        static RotorAir Hover(double h)
        {
            var air = new RotorAir(SoilScenarios.StemAir(), R, true, null, false);
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = Motors[i] + new Double3(0, h, 0);
            air.Begin(hubs, new Double3(0, 1, 0), new[] { Thrust, Thrust, Thrust, Thrust }, new Double3[4], new double[4]);
            return air;
        }

        static PoseFrame Pose(long step, double h) => new PoseFrame
        {
            Step = step, Position = new Double3(0, h, 0), Attitude = new Quat(0, 0, 0, 1), Armed = true,
            RotorThrust = new Corners { RearRight = Thrust, FrontRight = Thrust, RearLeft = Thrust, FrontLeft = Thrust },
        };

        const int TickSteps = 40; // PlantWash's default

        static PlantWash Grid(string surface, RainLevel rain, PlantMechanics[] bands, out PlantAir plants, int size = 32, double cell = 0.15)
        {
            var still = new WindField(WindSetup.Of(WindLevel.Still), new FlightRandom(1));
            plants = new PlantAir(still);
            return new PlantWash(SoilScenarios.StemAir(), R, Motors, new FloorWorld(0, surface, "steel"), plants, bands, SoilScenarios.Table(),
                SurfaceTable.MoistureOf(rain), size, cell);
        }

        // plants_air.py: the bend (rad, from the rest under its weight) of a green 0.6 m and a dry 0.9 m culm along the drone's
        // diagonal (through a hub) at r = 0, 0.2, …, 4 m, the hubs 0.5, 1, 2 and 4 m up; each from its natural lean outward.
        static readonly (double H, double[] Green, double[] Dry)[] HandHover =
        {
            (0.5, new[] { -0.018, 1.396, 0.105, 0.042, 0.023, 0.015, 0.010, 0.008, 0.006, 0.005, 0.004, 0.003, 0.003, 0.002, 0.002, 0.002, 0.002, 0.001, 0.001, 0.001, 0.001 },
                  new[] { -0.003, 1.425, 0.012, 0.005, 0.003, 0.002, 0.001, 0.001, 0.001, 0.001, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000 }),
            (1.0, new[] { 0.010, 1.335, 0.045, 0.068, 0.055, 0.048, 0.044, 0.040, 0.038, 0.036, 0.034, 0.032, 0.031, 0.030, 0.029, 0.028, 0.027, 0.026, 0.025, 0.024, 0.023 },
                  new[] { 0.001, 1.222, 0.005, 0.008, 0.007, 0.006, 0.005, 0.005, 0.004, 0.004, 0.004, 0.004, 0.004, 0.004, 0.003, 0.003, 0.003, 0.003, 0.003, 0.003, 0.003 }),
            (2.0, new[] { 0.951, 0.821, 0.064, 0.062, 0.084, 0.075, 0.069, 0.064, 0.061, 0.058, 0.055, 0.053, 0.051, 0.049, 0.048, 0.046, 0.045, 0.043, 0.042, 0.040, 0.039 },
                  new[] { 0.082, 0.026, 0.008, 0.007, 0.010, 0.009, 0.008, 0.008, 0.007, 0.007, 0.007, 0.006, 0.006, 0.006, 0.006, 0.006, 0.005, 0.005, 0.005, 0.005, 0.005 }),
            (4.0, new[] { 0.165, 0.083, 0.042, 0.038, 0.051, 0.073, 0.081, 0.075, 0.071, 0.067, 0.064, 0.062, 0.059, 0.057, 0.055, 0.053, 0.051, 0.049, 0.048, 0.046, 0.044 },
                  new[] { 0.017, 0.013, 0.007, 0.005, 0.006, 0.009, 0.010, 0.009, 0.008, 0.008, 0.008, 0.007, 0.007, 0.007, 0.007, 0.006, 0.006, 0.006, 0.006, 0.006, 0.006 }),
        };

        // A stand's steady bend in a hover's wash at (x, z), outward from its natural lean, by the law's own step on the grid's
        // heights (8 s at the grid's 20 ms).
        static double Steady(in PlantMechanics m, RotorAir air, ReadOnlySpan<double> heights, double x, double z)
        {
            double r = Math.Sqrt(x * x + z * z), ux = x / r, uz = z / r;
            var hs = heights.ToArray();
            var samples = new Double3[hs.Length];
            for (int k = 0; k < hs.Length; k++) samples[k] = air.At(new Double3(x, hs[k], z), 0, out _);
            double rest = PlantLaw.Rest(m, m.NaturalLean);
            var s = new PlantState { X = rest * ux, Z = rest * uz };
            for (int k = 0; k < 200; k++) PlantLaw.Step(m, ref s, m.NaturalLean * ux, m.NaturalLean * uz, hs, samples, hs.Length, 0.04);
            return (s.X * ux + s.Z * uz) - rest;
        }

        // A hover over grass, the law against the hand calculation along the diagonal: within 0.03 rad where it barely leans and
        // 0.05 rad where the columns fold it (the grid's 11 heights against the hand's 300).
        [Test]
        public void AHoverBendsTheGrassAsTheHandCalculation()
        {
            PlantMechanics green = PlantAirTests.Green(0.6), dry = PlantAirTests.Dry(0.9);
            PlantWash grid = Grid("belt_bare", RainLevel.Sunny, new[] { green }, out _);
            double worst = 0;
            foreach (var (h, handGreen, handDry) in HandHover)
            {
                RotorAir air = Hover(h);
                var lineG = new System.Text.StringBuilder();
                var lineD = new System.Text.StringBuilder();
                for (int i = 0; i < handGreen.Length; i++)
                {
                    double r = i == 0 ? 1e-4 : 0.2 * i, x = r * Math.Sqrt(0.5), z = r * Math.Sqrt(0.5);
                    double g = Steady(green, air, grid.Heights, x, z), d = Steady(dry, air, grid.Heights, x, z);
                    lineG.Append($" {g:F3}");
                    lineD.Append($" {d:F3}");
                    double tolG = handGreen[i] > 0.5 ? 0.05 : 0.03, tolD = handDry[i] > 0.5 ? 0.05 : 0.03;
                    worst = Math.Max(worst, Math.Max(Math.Abs(g - handGreen[i]) - tolG, Math.Abs(d - handDry[i]) - tolD));
                    Assert.That(g, Is.EqualTo(handGreen[i]).Within(tolG), $"green 0.6 m, hubs {h} m up, r {r:F1} m");
                    Assert.That(d, Is.EqualTo(handDry[i]).Within(tolD), $"dry 0.9 m, hubs {h} m up, r {r:F1} m");
                }
                Print($"hubs {h} m up, green 0.6 m by 0.2 m along the diagonal:{lineG}");
                Print($"hubs {h} m up, dry 0.9 m:{lineD}");
            }
        }

        // The grids in a still-air hover: each band's stands settle; the patch (pressed past 0.5 rad, flat past 1 rad, on a fine
        // 5 cm grid: the near columns are 18 cm across), the leaning ring (0.05 rad, on the 15 cm grid) and the largest bend, by
        // height. The patch is the four columns' footprints flat low down, the merged jet's landing pressed at 2 m and nothing by
        // 4 m; the ring reaches past 1.8 m from 2 m (the wall jet carries far, thin).
        [Test]
        public void AHoverFlattensItsPatchAndLeansItsRing()
        {
            PlantMechanics green = PlantAirTests.Green(0.6);
            foreach (double h in new[] { 0.5, 1.0, 2.0, 4.0 })
            {
                PlantWash fine = Grid("belt_bare", RainLevel.Sunny, new[] { green }, out _, 48, 0.05);
                PlantWash coarse = Grid("belt_bare", RainLevel.Sunny, new[] { green }, out _);
                for (long step = 0; step <= 8000; step += TickSteps)
                {
                    fine.Advance(Pose(step, h));
                    coarse.Advance(Pose(step, h));
                }
                Survey(fine, 0.05, 23, 1.0, out int flatNodes, out double flat, out double most, out double mostR);
                Survey(fine, 0.05, 23, 0.5, out int pressedNodes, out double pressed, out _, out _);
                Survey(coarse, 0.15, 15, 0.05, out _, out double ring, out _, out _);
                Print($"hubs {h} m up: flat (≥ 1 rad) {flatNodes * 0.0025:F3} m² out to {flat:F2} m, pressed (≥ 0.5 rad) {pressedNodes * 0.0025:F3} m² out to "
                    + $"{pressed:F2} m, the most {most:F3} rad at {mostR:F2} m; leaning ≥ 0.05 rad out to {ring:F2} m");
                if (h <= 1.0) Assert.That(flatNodes * 0.0025, Is.GreaterThan(0.01).And.LessThan(0.2), $"{h} m: the columns' footprints lie flat");
                if (h <= 1.0) Assert.That(pressed, Is.LessThan(0.35), $"{h} m: under the discs only");
                if (h == 2.0) Assert.That(pressedNodes, Is.GreaterThan(0), "2 m: the merged jet's landing presses it");
                if (h == 2.0) Assert.That(pressed, Is.LessThan(0.5), "2 m: within the landing");
                if (h == 4.0) Assert.That(pressedNodes, Is.Zero, "4 m: nothing pressed");
                if (h >= 2.0) Assert.That(ring, Is.GreaterThan(1.8), $"{h} m: the ring leans past 1.8 m");
            }
        }

        // The nodes bent past a threshold, the farthest of them, and the most bent.
        static void Survey(PlantWash grid, double cell, int half, double threshold, out int nodes, out double farthest, out double most, out double mostR)
        {
            nodes = 0;
            farthest = most = mostR = 0;
            for (int i = -half; i < half; i++)
                for (int j = -half; j < half; j++)
                {
                    double x = (i + 0.5) * cell, z = (j + 0.5) * cell;
                    if (!grid.Bend(0, x, z, 1, out PlantBend b, out _)) continue;
                    double bend = Math.Sqrt(b.X * b.X + b.Z * b.Z), r = Math.Sqrt(x * x + z * z);
                    if (bend >= threshold) { nodes++; farthest = Math.Max(farthest, r); }
                    if (bend > most) { most = bend; mostR = r; }
                }
        }

        // plants_air.py's onset per surface: the highest of 0.3 … 15 m from which the wash's u* anywhere passes the surface's
        // threshold, dry and wet (0: none), u* the largest over the grid's own nodes.
        static readonly (string Surface, double Dry, double Wet)[] HandDust =
        {
            ("crater_spoil", 10, 2), ("dirt_track", 8, 2), ("belt_bare", 6, 1.5), ("burnt_field", 8, 1.5), ("tilled", 4, 0.75),
            ("dry_crust", 1.5, 0.5), ("rubble", 4, 0.75), ("asphalt", 6, 0.75), ("concrete_slab", 6, 0.75),
        };

        static readonly double[] DustHeights = { 0.3, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0, 10.0, 12.0, 15.0 };

        // The dust rises as the drone comes lower: per surface, the grid's onset height dry and wet the hand
        // calculation's (its 12 heights against the hand's 300 for the jet's peak); covered ground and saturated ground none.
        [Test]
        public void DustRisesAsTheDroneComesLower()
        {
            var cache = new System.Collections.Generic.Dictionary<string, double[]>();
            foreach (var (surface, handDry, handWet) in HandDust)
            {
                double[] onset = new double[2];
                int c = 0;
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain })
                {
                    double on = 0;
                    var most = new double[DustHeights.Length];
                    for (int k = 0; k < DustHeights.Length; k++)
                    {
                        PlantWash grid = Grid(surface, rain, new PlantMechanics[0], out _, 32);
                        grid.Advance(Pose(0, DustHeights[k]));
                        for (int i = -15; i < 15; i++)
                            for (int j = -15; j < 15; j++)
                                if (grid.DustAt((i + 0.5) * 0.15, (j + 0.5) * 0.15, out DustSample d))
                                {
                                    most[k] = Math.Max(most[k], d.Friction);
                                    if (d.Lifts) on = Math.Max(on, DustHeights[k]);
                                }
                    }
                    onset[c++] = on;
                    cache[surface + rain] = most;
                }
                Print($"{surface}: dust from {onset[0]} m dry (hand {handDry}), {onset[1]} m wet (hand {handWet}); u* by height dry "
                    + string.Join(" ", Array.ConvertAll(cache[surface + RainLevel.Sunny], v => v.ToString("F3"))));
                Assert.That(onset[0], Is.EqualTo(handDry), surface + ": the dry onset");
                Assert.That(onset[1], Is.EqualTo(handWet), surface + ": the wet onset");
            }
            foreach (string covered in new[] { "meadow_sod", "weeds", "belt_straw", "yard_litter" })
            {
                PlantWash grid = Grid(covered, RainLevel.Sunny, new PlantMechanics[0], out _, 16);
                grid.Advance(Pose(0, 0.3));
                Assert.That(grid.DustAt(0.3, 0.3, out DustSample d) && !d.Lifts && double.IsPositiveInfinity(d.Threshold), covered + ": covered, no dust");
            }
            PlantWash soaked = Grid("crater_spoil", RainLevel.SevereRain, new PlantMechanics[0], out _, 16);
            soaked.Advance(Pose(0, 0.3));
            Assert.That(soaked.DustAt(0.3, 0.3, out DustSample wet) && !wet.Lifts, "saturated ground, no dust");
        }


        // One law, one answer: two grids over the same poses hold the same stands bit for bit, a tick and a frame's samples allocate
        // nothing, and the tick's cost on this machine is printed (it runs off the flight's thread; a timing counts only on AC
        // power).
        [Test]
        public void TheWashIsDeterministicAllocatesNothingAndItsCostIsKnown()
        {
            PlantMechanics[] bands = { PlantAirTests.Green(0.45), PlantAirTests.Green(0.97), PlantAirTests.Dry(0.6), PlantAirTests.Dry(1.2) };
            PlantWash a = Grid("belt_bare", RainLevel.Sunny, bands, out PlantAir pa), b = Grid("belt_bare", RainLevel.Sunny, bands, out _);
            for (long step = 0; step <= 2000; step += TickSteps)
            {
                PoseFrame p = Pose(step, 1.0 + 0.0005 * step);
                p.Position = new Double3(0.002 * step, p.Position.Y, 0);
                a.Advance(p);
                b.Advance(p);
            }
            for (int band = 0; band < bands.Length; band++)
                for (int i = -20; i < 20; i++)
                {
                    double x = 4 + 0.09 * i, z = 0.07 * i;
                    Assert.That(a.Bend(band, x, z, 0.5, out PlantBend u, out _) && b.Bend(band, x, z, 0.5, out PlantBend v, out _)
                        && u.X == v.X && u.Z == v.Z && u.RateX == v.RateX && u.RateZ == v.RateZ, $"band {band}, node {i}");
                }
            Ticks(a, bands, pa, 2040, 3);
            long before = AllocProbe.Before();
            Ticks(a, bands, pa, 2160, 20);
            long bytes = AllocProbe.After(before);
            Print($"{bytes} B over 20 ticks and their frames' samples");
            Assert.That(bytes, Is.Zero, AllocProbe.Report());
            var clock = Stopwatch.StartNew();
            Ticks(a, bands, pa, 2960, 100);
            double perTick = clock.Elapsed.TotalMilliseconds / 100;
            var one = new[] { bands[3] };
            PlantWash single = Grid("belt_bare", RainLevel.Sunny, one, out PlantAir pb);
            Ticks(single, one, pb, 2040, 3);
            clock.Restart();
            Ticks(single, one, pb, 2160, 100);
            double oneBand = clock.Elapsed.TotalMilliseconds / 100, perBand = (perTick - oneBand) / 3;
            Print($"a 32 × 32 grid of 4 bands, hubs about 2 m up: {perTick:F2} ms a tick ({perTick * 25 / 10:F1} % of a core at 25 Hz): "
                + $"the air ({single.Heights.Length} heights a node) {oneBand - perBand:F2} ms, each band {perBand:F2} ms; the frames' samples in");
        }

        static void Ticks(PlantWash grid, PlantMechanics[] bands, PlantAir plants, long from, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                long step = from + TickSteps * t;
                PoseFrame p = Pose(step, 2.0);
                p.Position = new Double3(4 + 0.001 * step, 2.0, 0);
                grid.Advance(p);
                plants.Begin(step * 0.001);
                for (int i = 0; i < 8 && bands.Length > 0; i++)
                    plants.Sample(bands[i % bands.Length], i % bands.Length, grid, new Double3(4 + 0.3 * i, 0, 0.2 * i), 0, out _);
            }
        }
    }
}
