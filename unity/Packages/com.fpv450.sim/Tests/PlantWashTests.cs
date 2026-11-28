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
                PlantWash fine = Grid("belt_bare", RainLevel.Sunny, new[] { green }, out PlantAir plants, 48, 0.05);
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
                // The drawn side reads the fine grid where it holds the place, the coarse one beyond it (still air: the wind adds none).
                plants.Begin(8.0);
                plants.Sample(green, 0, fine, coarse, new Double3(Hub, 0, Hub), 0, out PlantBend under);
                plants.Sample(green, 0, fine, coarse, new Double3(2.0, 0, 0.3), 0, out PlantBend far);
                fine.Bend(0, Hub, Hub, 1, out PlantBend fineUnder, out _);
                coarse.Bend(0, 2.0, 0.3, 1, out PlantBend coarseFar, out _);
                Assert.That(Math.Abs(under.X - fineUnder.X) + Math.Abs(under.Z - fineUnder.Z), Is.LessThan(1e-12), $"{h} m: under a hub, the fine grid's");
                Assert.That(Math.Abs(far.X - coarseFar.X) + Math.Abs(far.Z - coarseFar.Z), Is.LessThan(1e-12), $"{h} m: 2 m out, the coarse grid's");
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
            ("crater_spoil", 10, 0), ("dirt_track", 8, 0), ("belt_bare", 6, 0), ("burnt_field", 8, 0), ("tilled", 4, 0),
            ("dry_crust", 1.5, 0), ("rubble", 4, 0), ("asphalt", 6, 0), ("concrete_slab", 6, 0),
        };

        static readonly double[] DustHeights = { 0.3, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0, 10.0, 12.0, 15.0 };

        // The dust rises as the drone comes lower: per surface, the grid's onset height the hand calculation's, dry (its 11 heights
        // against the hand's 300 for the jet's peak) and wet (none: past Fécan's range); covered and saturated ground none.
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
            // Bare loam in rain: a hover 0.25 m up lifts nothing anywhere (the wet topsoil's cohesion holds its grains, design.md
            // §5), where the same loam dry lifts dust over the whole footprint and from 6 m up (above).
            foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
            {
                PlantWash low = Grid("belt_bare", rain, new PlantMechanics[0], out _, 32);
                low.Advance(Pose(0, 0.25));
                int lifting = 0;
                double most = 0;
                for (int i = -15; i < 15; i++)
                    for (int j = -15; j < 15; j++)
                        if (low.DustAt((i + 0.5) * 0.15, (j + 0.5) * 0.15, out DustSample d))
                        {
                            if (d.Lifts) lifting++;
                            most = Math.Max(most, d.Friction);
                        }
                Print($"belt_bare, hubs 0.25 m up in {rain}: {lifting} nodes lift dust, u* up to {most:F3} m/s (ρu*² {Rho * most * most:F1} Pa)");
                if (rain == RainLevel.Sunny) Assert.That(lifting, Is.GreaterThan(100), "dry loam lifts dust under a low hover");
                else Assert.That(lifting, Is.Zero, rain + ": wet loam lifts none");
            }
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

        // ---- the loose plant bits (ChaffLaw, 1.4a; design.md §5b) ----

        static readonly string[] Covered = { "meadow_sod", "weeds", "belt_straw", "yard_litter" };

        // plant_bits.py: each class's piece, dry and wet: mass (mg), settling (m/s), start and keep (u*, m/s).
        static readonly (BitClass C, bool Wet, double Mg, double Settling, double Start, double Keep)[] HandPieces =
        {
            (BitClass.Chaff, false, 1.04, 0.8676, 0.4199, 0.3359), (BitClass.Chaff, true, 2.83, 1.4327, 6.7745, 5.4196),
            (BitClass.Straw, false, 81.28, 2.9069, 0.3880, 0.3104), (BitClass.Straw, true, 105.66, 3.3144, 1.1331, 0.9065),
            (BitClass.Leaf, false, 77.75, 0.9504, 0.4600, 0.3680), (BitClass.Leaf, true, 212.06, 1.5695, 2.3827, 1.9062),
        };

        // plant_bits.py: each covered surface's loose pieces per m² (chaff, straw, leaves).
        static readonly (string Surface, double Chaff, double Straw, double Leaf)[] HandSupply =
        {
            ("meadow_sod", 5305.2, 357.1, 0), ("weeds", 2652.6, 357.1, 42.4), ("belt_straw", 2652.6, 2142.9, 84.9), ("yard_litter", 5305.2, 0, 509.3),
        };

        // The pieces against the hand, and their numbers against the surface table's own straw and leaf rows (one set of numbers).
        [Test]
        public void TheBitsAreTheHandCalculation()
        {
            foreach (var (c, wet, mg, settling, start, keep) in HandPieces)
            {
                BitPiece p = ChaffLaw.Piece(c, wet ? Moisture.Wet : Moisture.Dry);
                Print($"{c} {(wet ? "wet" : "dry")}: {p.Length * 1e3:F1} × {p.Width * 1e3:F1} × {p.Thickness * 1e3:F2} mm, {p.Mass * 1e6:F2} mg, "
                    + $"settling {p.Settling:F4} m/s, start {p.Start:F4} m/s, keep {p.Keep:F4} m/s, spread {p.Spread}");
                Assert.That(p.Mass * 1e6, Is.EqualTo(mg).Within(0.006), c + " mass");
                Assert.That(p.Settling, Is.EqualTo(settling).Within(1e-4), c + " settling");
                Assert.That(p.Start, Is.EqualTo(start).Within(1e-4), c + " start");
                Assert.That(p.Keep, Is.EqualTo(keep).Within(1e-4), c + " keep");
                BitPiece s = ChaffLaw.Piece(c, Moisture.Saturated);
                Assert.That(wet ? s.Start == p.Start && s.Mass == p.Mass : s.Start > p.Start, c + ": saturated is the wet set");
            }
            foreach (var (surface, chaff, straw, leaf) in HandSupply)
            {
                Assert.That(ChaffLaw.Supply(surface, BitClass.Chaff), Is.EqualTo(chaff).Within(0.06), surface + " chaff");
                Assert.That(ChaffLaw.Supply(surface, BitClass.Straw), Is.EqualTo(straw).Within(0.06), surface + " straw");
                Assert.That(ChaffLaw.Supply(surface, BitClass.Leaf), Is.EqualTo(leaf).Within(0.06), surface + " leaves");
            }
            foreach (string bare in new[] { "belt_bare", "tilled", "dirt_track", "asphalt" })
                for (int c = 0; c < ChaffLaw.Classes; c++)
                    Assert.That(ChaffLaw.Supply(bare, (BitClass)c), Is.Zero, bare + ": no loose cover");
            SurfaceTable table = SoilScenarios.Table();
            CoverSet strawRow = default, leafRow = default;
            foreach (CoverRow row in table.Cover)
            {
                if (row.Kind == CoverKind.Straw) strawRow = row.Dry;
                if (row.Kind == CoverKind.Leaf) leafRow = row.Dry;
            }
            Assert.That(ChaffLaw.StrawWallShare, Is.EqualTo(strawRow.WallShare), "the straw row's wall");
            Assert.That(ChaffLaw.StrawDensity, Is.EqualTo(strawRow.Density), "the straw row's density");
            Assert.That(ChaffLaw.LeafMassPerArea, Is.EqualTo(leafRow.DryMassPerArea), "the leaf row's mass per area");
            Assert.That(ChaffLaw.StrawWet, Is.EqualTo(table.Wetting.StrawDensityWet), "the straw's wet factor");
            Assert.That(ChaffLaw.Shelter(0), Is.EqualTo(1));
            Assert.That(ChaffLaw.Shelter(0.3), Is.EqualTo(1 / Math.Sqrt(1 + 0.5 * 90 * 0.3)).Within(1e-12), "Raupach's partition");
        }

        static PoseFrame Pose(long step, double h, double throttle)
        {
            PoseFrame p = Pose(step, h);
            double t = Thrust * throttle;
            p.RotorThrust = new Corners { RearRight = t, FrontRight = t, RearLeft = t, FrontLeft = t };
            return p;
        }

        // The pieces a grid's last tick lifted per class over its window (pieces: rate × tick × cell²), and its most u*.
        static double[] Lifted(PlantWash grid, double cell, out double most)
        {
            WashGround g = grid.TakeGround(double.MaxValue);
            var total = new double[ChaffLaw.Classes];
            most = 0;
            for (int i = 0; i < g.Size * g.Size; i++)
            {
                most = Math.Max(most, g.BitsFriction[i]);
                for (int c = 0; c < ChaffLaw.Classes; c++) total[c] += g.Rate(i, (BitClass)c) * g.TickSeconds * cell * cell;
            }
            return total;
        }

        // plant_bits.py: the pieces (chaff, straw, leaves) a hover from rest lifts in its first tick over the 32 × 0.15 m window,
        // by stem_wash.py's field at 200 heights to 0.1 m a node and design.md §5's peak height; dry and wet, the hubs at BitHeights[k].
        static readonly double[] BitHeights = { 0.3, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 8.0 };

        static readonly (string Surface, double[,] Dry, double[,] Wet)[] HandHoverBits =
        {
            ("meadow_sod",
                new[,] { { 41868.3, 3212.6, 0 }, { 41464.5, 3181.5, 0 }, { 39716.8, 3045.6, 0 }, { 25553.3, 1963.3, 0 }, { 23964.9, 1866.7, 0 }, { 22987.9, 1811.5, 0 }, { 20098.2, 1631.0, 0 }, { 16494.5, 1385.7, 0 }, { 9532.0, 862.6, 0 }, { 4751.0, 461.6, 0 } },
                new[,] { { 3.6, 382.3, 0 }, { 2.5, 377.1, 0 }, { 0.2, 329.1, 0 }, { 0, 150.2, 0 }, { 0, 88.7, 0 }, { 0, 57.4, 0 }, { 0, 23.3, 0 }, { 0, 9.2, 0 }, { 0, 1.4, 0 }, { 0, 0.2, 0 } }),
            ("weeds",
                new[,] { { 20934.2, 3212.6, 285.6 }, { 20732.2, 3181.5, 282.9 }, { 19858.4, 3045.6, 271.4 }, { 12776.6, 1963.3, 174.6 }, { 11982.4, 1866.7, 160.5 }, { 11494.0, 1811.5, 151.4 }, { 10049.1, 1631.0, 127.2 }, { 8247.2, 1385.7, 99.8 }, { 4766.0, 862.6, 52.5 }, { 2375.5, 461.6, 24.0 } },
                new[,] { { 1.8, 382.3, 5.8 }, { 1.3, 377.1, 5.2 }, { 0.1, 329.1, 2.3 }, { 0, 150.2, 0.3 }, { 0, 88.7, 0.1 }, { 0, 57.4, 0 }, { 0, 23.3, 0 }, { 0, 9.2, 0 }, { 0, 1.4, 0 }, { 0, 0.2, 0 } }),
            ("belt_straw",
                new[,] { { 25721.9, 23392.2, 710.8 }, { 25474.1, 23172.2, 703.9 }, { 24385.2, 22192.3, 674.0 }, { 15070.1, 13806.4, 415.1 }, { 14381.8, 13310.6, 390.2 }, { 13988.0, 13061.3, 374.7 }, { 12660.7, 12106.9, 328.0 }, { 10813.2, 10653.3, 269.2 }, { 6784.4, 7151.5, 155.0 }, { 3644.4, 4109.3, 76.6 } },
                new[,] { { 4.3, 3038.4, 17.7 }, { 3.1, 3010.7, 16.3 }, { 0.3, 2752.2, 8.4 }, { 0, 1338.8, 1.4 }, { 0, 843.4, 0.5 }, { 0, 577.8, 0.2 }, { 0, 257.2, 0 }, { 0, 109.7, 0 }, { 0, 18.9, 0 }, { 0, 3.3, 0 } }),
            ("yard_litter",
                new[,] { { 58823.6, 0, 4928.0 }, { 58273.0, 0, 4880.5 }, { 55815.4, 0, 4671.9 }, { 33599.5, 0, 2790.5 }, { 32368.3, 0, 2655.5 }, { 31722.3, 0, 2574.7 }, { 29293.9, 0, 2310.1 }, { 25635.5, 0, 1950.4 }, { 16951.2, 0, 1189.6 }, { 9566.6, 0, 619.7 } },
                new[,] { { 15.4, 0, 140.1 }, { 11.3, 0, 130.9 }, { 1.1, 0, 74.9 }, { 0, 0, 14.1 }, { 0, 0, 5.4 }, { 0, 0, 2.3 }, { 0, 0, 0.5 }, { 0, 0, 0.1 }, { 0, 0, 0 }, { 0, 0, 0 } }),
        };

        // A hover from rest over each covered surface lifts in its first tick what the hand does, dry and wet, more the lower it
        // hovers and the harder it pushes (the wall jet's own scaling, no knob); bare ground lifts none.
        [Test]
        public void GrassBitsLiftMoreAsTheDroneComesLowerAndThrottlesUp()
        {
            foreach (var (surface, handDry, handWet) in HandHoverBits)
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain })
                {
                    double[,] hand = rain == RainLevel.Sunny ? handDry : handWet;
                    double[] last = null;
                    for (int k = 0; k < BitHeights.Length; k++)
                    {
                        PlantWash grid = Grid(surface, rain, new PlantMechanics[0], out _, 32);
                        grid.Advance(Pose(0, BitHeights[k]));
                        double[] got = Lifted(grid, 0.15, out double most);
                        Print($"{surface} {rain}, hubs {BitHeights[k]} m: chaff {got[0]:F1} (hand {hand[k, 0]}), straw {got[1]:F1} ({hand[k, 1]}), "
                            + $"leaves {got[2]:F1} ({hand[k, 2]}); u* up to {most:F3} m/s");
                        for (int c = 0; c < ChaffLaw.Classes; c++)
                        {
                            // The grid reads the jet at its own 5 heights to 0.1 m, the hand at 200: within 6 % or 3 pieces (4.4 % at most here).
                            Assert.That(got[c], Is.EqualTo(hand[k, c]).Within(Math.Max(3, 0.06 * hand[k, c])), $"{surface} {rain} {BitHeights[k]} m, {(BitClass)c}");
                            if (last != null && hand[k, c] > 0) Assert.That(got[c], Is.LessThan(last[c]), $"{surface} {rain}: lower lifts more, {(BitClass)c}");
                        }
                        last = got;
                    }
                }
            foreach (string surface in Covered)
                foreach (double h in new[] { 1.0, 3.0, 6.0 })
                {
                    PlantWash normal = Grid(surface, RainLevel.Sunny, new PlantMechanics[0], out _, 32), hard = Grid(surface, RainLevel.Sunny, new PlantMechanics[0], out _, 32);
                    normal.Advance(Pose(0, h, 1.0));
                    hard.Advance(Pose(0, h, 1.4));
                    double[] a = Lifted(normal, 0.15, out double ua), b = Lifted(hard, 0.15, out double ub);
                    Print($"{surface}, hubs {h} m, thrust ×1 → ×1.4: u* {ua:F3} → {ub:F3} m/s; pieces {a[0] + a[1] + a[2]:F0} → {b[0] + b[1] + b[2]:F0}");
                    Assert.That(ub, Is.GreaterThan(ua), surface + ": more thrust, more u*");
                    for (int c = 0; c < ChaffLaw.Classes; c++)
                        if (a[c] > 0) Assert.That(b[c], Is.GreaterThan(a[c]), $"{surface} {h} m: more thrust lifts more {(BitClass)c}");
                }
            foreach (string bare in new[] { "belt_bare", "tilled", "dirt_track" })
            {
                PlantWash grid = Grid(bare, RainLevel.Sunny, new PlantMechanics[0], out _, 32);
                grid.Advance(Pose(0, 0.3));
                double[] got = Lifted(grid, 0.15, out double most);
                Assert.That(got[0] + got[1] + got[2], Is.Zero, bare + ": bare ground lifts dust, no bits");
                Assert.That(most, Is.Zero, bare + ": no cover's u*");
            }
        }

        // Supply-limited: a hover lifts its whiff in its first tick and then nothing; a throttle punch lifts a new whiff, and the two
        // whiffs together are what a hover from rest at the punch's thrust lifts at once. A wet straw mat lifts only low.
        [Test]
        public void AHoverLiftsItsWhiffThenClearsAndAPunchLiftsAnother()
        {
            PlantWash grid = Grid("belt_straw", RainLevel.Sunny, new PlantMechanics[0], out _, 32);
            grid.Advance(Pose(0, 1.5));
            double[] first = Lifted(grid, 0.15, out _);
            double later = 0;
            for (int t = 1; t <= 25; t++)
            {
                grid.Advance(Pose(t * TickSteps, 1.5));
                double[] got = Lifted(grid, 0.15, out _);
                later += got[0] + got[1] + got[2];
            }
            grid.Advance(Pose(26 * TickSteps, 1.5, 1.4));
            double[] punch = Lifted(grid, 0.15, out _);
            PlantWash fresh = Grid("belt_straw", RainLevel.Sunny, new PlantMechanics[0], out _, 32);
            fresh.Advance(Pose(0, 1.5, 1.4));
            double[] once = Lifted(fresh, 0.15, out _);
            Print($"belt_straw, hubs 1.5 m: the first tick lifts {first[0]:F0} chaff, {first[1]:F0} straws, {first[2]:F0} leaves; the next 1 s {later}; "
                + $"a ×1.4 punch {punch[0]:F0} / {punch[1]:F0} / {punch[2]:F0}; from rest at ×1.4 {once[0]:F0} / {once[1]:F0} / {once[2]:F0}");
            Assert.That(first[0] + first[1] + first[2], Is.GreaterThan(1000), "the first whiff");
            Assert.That(later, Is.Zero, "a steady hover has nothing more to lift");
            for (int c = 0; c < ChaffLaw.Classes; c++)
            {
                Assert.That(punch[c], Is.GreaterThan(0), (BitClass)c + ": the punch lifts a new whiff");
                Assert.That(first[c] + punch[c], Is.EqualTo(once[c]).Within(1e-9 * once[c]), (BitClass)c + ": the whiffs add up to the supply's share");
            }
        }

        // A world of flat ground under a uniform stand of green grass: n culms per m² of drawn tops h (StemsNear's patch over the
        // query circle), the rest FloorWorld's.
        sealed class StandWorld : IWorldQuery
        {
            readonly FloorWorld _floor;
            readonly double _n, _h;

            public StandWorld(string surface, double n, double h)
            {
                _floor = new FloorWorld(0, surface, "steel");
                _n = n;
                _h = h;
            }

            public int StemsNear(XZ centre, double radius, double height, Span<StemPatch> results)
            {
                if (results.Length < 1 || !(height < _h)) return 0;
                results[0] = new StemPatch { Kind = StemKind.GreenBlade, Area = Math.PI * radius * radius, Density = _n, HeightMin = _h, HeightMax = _h };
                return 1;
            }

            public ulong ContentHash => _floor.ContentHash;
            public int QueryVersion => _floor.QueryVersion;
            public void SampleGround(ReadOnlySpan<XZ> points, Span<GroundSample> results) => _floor.SampleGround(points, results);
            public int StaticContacts(in Capsule previous, in Capsule current, double margin, Span<StaticContact> results, QueryFilter filter = QueryFilter.None) =>
                _floor.StaticContacts(previous, current, margin, results, filter);
            public void Raycast(ReadOnlySpan<Ray> rays, double maxDistance, Span<RayHit> hits, QueryFilter filter = QueryFilter.None) =>
                _floor.Raycast(rays, maxDistance, hits, filter);
            public bool Geometry(int obj, int shape, out ShapeGeometry geometry) => _floor.Geometry(obj, shape, out geometry);
            public bool ShapeContact(int obj, int shape, in Capsule previous, in Capsule current, double margin, out StaticContact contact) =>
                _floor.ShapeContact(obj, shape, previous, current, margin, out contact);
            public int MicroDetailNear(Double3 centre, double radius, MicroKind kinds, Span<MicroElement> results) => _floor.MicroDetailNear(centre, radius, kinds, results);
            public int WiresNear(Double3 centre, double radius, Span<Conductor> results) => _floor.WiresNear(centre, radius, results);
            public int SnagsNear(Double3 centre, double radius, Span<SnagSegment> results) => _floor.SnagsNear(centre, radius, results);
            public bool Feature(int obj, int shape, out GroundFeature feature) => _floor.Feature(obj, shape, out feature);
            public int AddObject(string name, ReadOnlySpan<ShapeGeometry> shapes) => _floor.AddObject(name, shapes);
            public void ResetRuntimeObjects() => _floor.ResetRuntimeObjects();
            public string ObjectName(int obj) => _floor.ObjectName(obj);
            public string MaterialName(ushort material) => _floor.MaterialName(material);
            public ushort MaterialId(string name) => _floor.MaterialId(name);
            public string SurfaceName(byte surface) => _floor.SurfaceName(surface);
        }

        // Standing grass shelters the loose pieces in its thatch (Raupach et al. 1993 on the world's own stems): 400 green culms
        // of 0.6 m a m² (a sward's order, stem-properties.md §3) take u* at the pieces to Shelter(λ) of the bare mat's, λ by the
        // cover row's allometry; so a hover lifts its first bits from lower down.
        [Test]
        public void StandingGrassSheltersTheBits()
        {
            SurfaceTable table = SoilScenarios.Table();
            double d1 = 0;
            foreach (CoverRow row in table.Cover)
                if (row.Kind == CoverKind.GrassBlade) d1 = row.Dry.Diameter;
            double lambda = 400 * d1 * Math.Pow(0.6, 1 / 1.53) * 0.6, shelter = ChaffLaw.Shelter(lambda);
            var plants = new PlantAir(new WindField(WindSetup.Of(WindLevel.Still), new FlightRandom(1)));
            double[] open = new double[BitHeights.Length], stand = new double[BitHeights.Length];
            for (int k = 0; k < BitHeights.Length; k++)
            {
                var bare = new PlantWash(SoilScenarios.StemAir(), R, Motors, new FloorWorld(0, "meadow_sod", "steel"), plants, new PlantMechanics[0], table, Moisture.Dry);
                var grassy = new PlantWash(SoilScenarios.StemAir(), R, Motors, new StandWorld("meadow_sod", 400, 0.6), plants, new PlantMechanics[0], table, Moisture.Dry);
                bare.Advance(Pose(0, BitHeights[k]));
                grassy.Advance(Pose(0, BitHeights[k]));
                WashGround a = bare.TakeGround(1), b = grassy.TakeGround(1);
                for (int i = 0; i < a.Size * a.Size; i++)
                    Assert.That(b.BitsFriction[i], Is.EqualTo(shelter * a.BitsFriction[i]).Within(1e-12 + 1e-12 * a.BitsFriction[i]), "u* at the pieces");
                double[] x = Lifted(bare, 0.15, out _), y = Lifted(grassy, 0.15, out _);
                open[k] = x[0] + x[1];
                stand[k] = y[0] + y[1];
            }
            Print($"400 culms of 0.6 m a m²: λ {lambda:F3}, the pieces feel {shelter:F3} of u*; pieces a first tick lifts by hub height "
                + string.Join(", ", Array.ConvertAll(BitHeights, h => h.ToString())) + " m: bare thatch "
                + string.Join(" ", Array.ConvertAll(open, v => v.ToString("F0"))) + "; under the stand " + string.Join(" ", Array.ConvertAll(stand, v => v.ToString("F0"))));
            Assert.That(lambda, Is.EqualTo(0.30).Within(0.03), "λ of a sward");
            for (int k = 0; k < BitHeights.Length; k++) Assert.That(stand[k], Is.LessThan(open[k]), BitHeights[k] + " m: the stand shelters");
            Assert.That(stand[BitHeights.Length - 1], Is.LessThan(0.01 * open[BitHeights.Length - 1]), "high up the stand lifts almost none");
        }

        // The ground reaches the drawn side whole: none before the first tick, the newest tick at or before the frame's time, two
        // grids over the same poses bit for bit, 0 B a tick and a take, and a helper ticking while this thread takes never hands
        // over a torn snapshot (each snapshot equals the sequential run's for its tick).
        [Test]
        public void TheGroundIsHandedWholeDeterministicallyAndAllocatesNothing()
        {
            PlantWash a = Grid("belt_straw", RainLevel.Sunny, new PlantMechanics[0], out _, 32), b = Grid("belt_straw", RainLevel.Sunny, new PlantMechanics[0], out _, 32);
            Assert.That(a.TakeGround(1e9), Is.Null, "no tick yet");
            const int Count = 120;
            var poses = new PoseFrame[Count];
            for (int t = 0; t < Count; t++)
            {
                poses[t] = Pose(t * TickSteps, 2.5 - 0.018 * t, 1 + 0.003 * t);
                poses[t].Position = new Double3(0.004 * t * TickSteps, poses[t].Position.Y, 0.001 * t * TickSteps);
            }
            var sums = new System.Collections.Generic.Dictionary<long, ulong>();
            for (int t = 0; t < Count; t++)
            {
                a.Advance(poses[t]);
                b.Advance(poses[t]);
                WashGround ga = a.TakeGround(poses[t].Step * FlightCore.Dt), gb = b.TakeGround(poses[t].Step * FlightCore.Dt);
                Assert.That(ga.Tick, Is.EqualTo(poses[t].Step), "the newest tick at the frame's time");
                ulong sa = Sum(ga), sb = Sum(gb);
                Assert.That(sa, Is.EqualTo(sb), $"tick {t}: bit for bit");
                sums[ga.Tick] = sa;
            }
            a.Advance(Pose(Count * TickSteps, 1.0));
            Assert.That(a.TakeGround((Count * TickSteps - 1) * FlightCore.Dt).Tick, Is.EqualTo((Count - 1) * TickSteps), "a tick past the frame waits");
            Assert.That(a.TakeGround(Count * TickSteps * FlightCore.Dt).Tick, Is.EqualTo(Count * TickSteps));
            long before = AllocProbe.Before();
            for (int t = 1; t <= 20; t++)
            {
                a.Advance(Pose((Count + t) * TickSteps, 1.0 - 0.02 * t));
                a.TakeGround(1e9);
            }
            long bytes = AllocProbe.After(before);
            Print($"{bytes} B over 20 ticks and their takes");
            Assert.That(bytes, Is.Zero, AllocProbe.Report());

            PlantWash c = Grid("belt_straw", RainLevel.Sunny, new PlantMechanics[0], out _, 32);
            var helper = new System.Threading.Thread(() =>
            {
                for (int t = 0; t < Count; t++) c.Advance(poses[t]);
            });
            helper.Start();
            int taken = 0;
            long lastTick = -1;
            while (helper.IsAlive || lastTick < poses[Count - 1].Step)
            {
                WashGround g = c.TakeGround(1e9);
                if (g == null || g.Tick == lastTick) continue;
                Assert.That(g.Tick, Is.GreaterThan(lastTick), "ticks in order");
                Assert.That(Sum(g), Is.EqualTo(sums[g.Tick]), $"tick {g.Tick}: whole");
                lastTick = g.Tick;
                taken++;
            }
            helper.Join();
            Print($"a helper ticking {Count} ticks while this thread took {taken} snapshots: each whole");
            Assert.That(lastTick, Is.EqualTo(poses[Count - 1].Step), "the last tick reached the taker");
        }

        // A snapshot's FNV-1a over its window, its tick and every per-node number's bits.
        static ulong Sum(WashGround g)
        {
            ulong h = 14695981039346656037UL;
            void Mix(ulong v) { h = (h ^ v) * 1099511628211UL; }
            Mix((ulong)g.Tick);
            Mix((ulong)g.StartI);
            Mix((ulong)g.StartJ);
            for (int i = 0; i < g.Size * g.Size; i++)
            {
                Mix(g.Surface[i]);
                Mix((ulong)BitConverter.DoubleToInt64Bits(g.DustFriction[i]));
                Mix((ulong)BitConverter.DoubleToInt64Bits(g.DustThreshold[i]));
                Mix((ulong)BitConverter.DoubleToInt64Bits(g.DustFlux[i]));
                Mix((ulong)BitConverter.DoubleToInt64Bits(g.BitsFriction[i]));
                for (int c = 0; c < ChaffLaw.Classes; c++) Mix((ulong)BitConverter.DoubleToInt64Bits(g.BitsRate[i * ChaffLaw.Classes + c]));
            }
            return h;
        }
    }
}
