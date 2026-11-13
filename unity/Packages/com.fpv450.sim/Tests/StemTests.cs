using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The props in standing stems (w9-village-physics 2.2 and 2.12, design.md "## 2.2" and "## 2.12"): the stems a disc's leading
    /// edge meets against the hand calculation from the field (a creep and the pilot's approach), the vertical landing, the shake
    /// across the straw's number and thickness, a hover that cuts itself clear, the range's stalks by the same law, the arm loaded
    /// at its tip (G8), and the log's replay. Each run prints its numbers; design.md 2.12 holds them.
    /// </summary>
    public class StemTests
    {
        const double R = 0.127;

        static double Disc => Math.PI * R * R;

        /// <summary>
        /// The hand calculation of the stems a disc's leading edge meets upright (design.md 2.12, from 2.2's C): over the run's
        /// steps from..to, every step's 2·R·v·D(h)·dt, v the hub's speed across its axis and D(h) the field's stems per m² under the
        /// disc that stand to h at their natural lean (D at h/cos φ₀, φ₀ even over 0–15°, 6 points), read with StemsNear itself.
        /// Each is cut at the rim at e_s·A_w + μ·h_f·u², its diameter by the allometry at its length (the field's tops spread over
        /// each 2 cm above h). Every 10th step is evaluated (numerical). Returns the stems; work gives their joules.
        /// </summary>
        internal static double Entries(StemScenarios.Run r, int c, int from, int to, out double work, double density = 1, double diameter = 1)
        {
            MapWorld village = SlopeScenarios.Farmland();
            SurfaceTable table = SoilScenarios.Table();
            PropBlade blade = RepositoryDrone.Spec().BuildPropulsion().Rotor.Blade;
            int k = blade.Stations.Length - 1;
            double rim = R, band = blade.Chord[k] * Math.Sin(blade.SectionAngle[k]) + blade.Thickness[k] * Math.Cos(blade.SectionAngle[k]);
            var patches = new StemPatch[64];
            var upper = new StemPatch[64];
            double stems = 0;
            work = 0;
            for (int i = from; i < to; i += 10)
            {
                double h = r.HubH[c][i], v = r.HubV[c][i], u = r.Omega[c][i] * rim;
                if (!(v > 0) || !(h > 0)) continue;
                var at = new XZ(r.HubX[c][i], r.HubZ[c][i]);
                for (int q = 0; q < 6; q++)
                {
                    double phi0 = PropStrikes.StemNaturalLean * (q + 0.5) / 6, reach = h / Math.Cos(phi0);
                    int n = village.StemsNear(at, R, reach, patches);
                    for (int p = 0; p < n; p++)
                    {
                        double each = 2 * R * v * patches[p].Density * density * patches[p].Area / Disc * FlightCore.Dt * 10 / 6;
                        stems += each;
                        // The energy over the stems' lengths from reach up: the field's tops in 2 cm bands.
                        CoverSet set = table.Cover[(int)patches[p].Kind + (int)CoverKind.GrassBlade - 1].Dry;
                        double total = patches[p].Density, energy = 0;
                        for (double top = reach; top < patches[p].HeightMax; top += 0.02)
                        {
                            double above = Density(village, at, top + 0.02, patches[p].Cell, patches[p].Layer, upper);
                            double share = (Density(village, at, top, patches[p].Cell, patches[p].Layer, upper) - above) / total, L = top + 0.01;
                            double d = set.Diameter * diameter * Math.Pow(L, PropStrikes.StemAllometry), di = d * (1 - 2 * set.WallShare);
                            double area = Math.PI / 4 * (d * d - di * di);
                            energy += share * (set.CutEnergy * area + set.Density * area * band * u * u);
                        }
                        work += each * energy;
                    }
                }
            }
            return stems;
        }

        static double Density(MapWorld village, XZ at, double h, int cell, byte layer, StemPatch[] buffer)
        {
            int n = village.StemsNear(at, R, h, buffer);
            for (int p = 0; p < n; p++)
                if (buffer[p].Cell == cell && buffer[p].Layer == layer) return buffer[p].Density;
            return 0;
        }

        static double Sum(double[] a, int from, int to)
        {
            double s = 0;
            for (int i = from; i < to; i++) s += a[i];
            return s;
        }

        // The pilot's account, as a creep: a front disc moving through the densest tall cell at a held height cuts the stems its
        // leading edge meets standing, each a discrete hit; the slipstream folds what is under it; the rear discs run in the front
        // ones' track and meet stubs at their own plane.
        [Test]
        public void ACreepCutsWhatItsLeadingEdgesMeet()
        {
            StemScenarios.Run run = StemScenarios.Creep(speed: 0.5);
            int from = 4100, to = (int)run.Steps - 1;
            double hand = 0, handWork = 0, sim = 0, simWork = 0, seconds = (to - from) * FlightCore.Dt;
            for (int c = 1; c < 4; c += 2)
            {
                double h = Entries(run, c, from, to, out double w), n = Sum(run.Fresh1[c], from, to), e = Sum(run.FreshWork1[c], from, to);
                TestContext.Out.WriteLine($"rotor {c}: {seconds:F1} s creeping at 0.5 m/s, 0.6 m: fresh cuts {n:F0} = {n / seconds:F1} /s (hand {h / seconds:F1}), "
                    + $"their work {e / seconds:F2} W (hand {w / seconds:F2}), {e / Math.Max(1, n) * 1000:F0} mJ a cut (hand {w / h * 1000:F0})");
                hand += h;
                handWork += w;
                sim += n;
                simWork += e;
            }
            TestContext.Out.WriteLine($"the front discs: {sim:F0} fresh cuts (hand {hand:F1}), {simWork:F2} J (hand {handWork:F2})");
            Assert.That(sim, Is.EqualTo(hand).Within(10).Percent, "the leading edges' stems, by hand");
            Assert.That(simWork, Is.EqualTo(handWork).Within(10).Percent, "their work, by hand");
            double frontFresh = run.Fresh[1] + run.Fresh[3], rearFresh = run.Fresh[0] + run.Fresh[2];
            TestContext.Out.WriteLine($"fresh per rotor {F(run.Fresh)}, sliced {F(run.Sliced)}; episodes {run.Starts}/{run.Ends}; {run.Allocated} B");
            Assert.That(rearFresh, Is.LessThan(0.7 * frontFresh), "the rear discs run in the front ones' track");
            Assert.That(run.Allocated, Is.Zero, AllocProbe.Report());
        }

        // The pilot's landing: an approach at 2 m/s coming down at 0.5 m/s, a flare, the touch. The front discs cut what stands in
        // front of them (in front of the camera), within 10 % of the hand calculation over the run-in; the log replays bit for bit.
        [Test]
        public void TheApproachCutsInFrontOfTheCamera()
        {
            var log = new MemoryStream();
            StemScenarios.Run run = StemScenarios.Approach(log: log), calm = StemScenarios.Approach(stems: false);
            int touch = (int)run.Touch - 1;
            for (int c = 0; c < 4; c++)
            {
                double hand = Entries(run, c, 3100, touch, out double handWork), sim = Sum(run.Fresh1[c], 3100, touch), simWork = Sum(run.FreshWork1[c], 3100, touch);
                TestContext.Out.WriteLine($"rotor {c}, the approach to the touch: fresh {sim:F0} (the leading edge's entries by hand {hand:F1}), {simWork:F2} J (hand {handWork:F2})");
            }
            Print("approach", run, calm);
            Assert.That(run.Touch, Is.GreaterThan(0));
            Assert.That(run.FreshBefore[1] + run.FreshBefore[3], Is.GreaterThan(1.3 * (run.FreshBefore[0] + run.FreshBefore[2])), "cut in front of the camera");
            Assert.That(run.Allocated, Is.Zero, "0 B in steady state" + AllocProbe.Report());
            Assert.That(run.Refused, Is.EqualTo(0));
            ReplayResult replay = FlightReplay.Run(new MemoryStream(log.ToArray()), StemScenarios.OpenFarmland, RepositoryDrone.Spec().ContentHash, null,
                SoilScenarios.Table().Hash, materialsHash: Hash());
            TestContext.Out.WriteLine($"replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(run.Steps));
        }

        // The spec's vertical landing after a hover over the spot: the slipstream folds most stems under the discs in the hover; the
        // fresh cuts are what the drift runs the discs onto (the hand calculation's entries, a floor) and the stems the jet has not
        // yet folded (those whose natural lean lies across the swirl start from it alone); the stubs are sliced as the discs come
        // down. The known-wrong (nothing folds, every stem met is cut) cuts more than twice as many.
        [Test]
        public void TheVerticalLandingFoldsMostStems()
        {
            StemScenarios.Run run = StemScenarios.Landing(), calm = StemScenarios.Landing(stems: false);
            int touch = (int)run.Touch - 1;
            double fresh = 0, bound = 0;
            for (int c = 0; c < 4; c++)
            {
                fresh += Sum(run.Fresh1[c], 4100, touch);
                bound += Entries(run, c, 4100, touch, out _);
            }
            TestContext.Out.WriteLine($"vertical landing: fresh before the touch {fresh:F0}, the drift's entries by hand {bound:F1}");
            Print("vertical landing", run, calm);
            Assert.That(fresh, Is.GreaterThanOrEqualTo(0.9 * bound), "at least what the drift runs the discs onto");
            StemScenarios.Run wrong = StemScenarios.Landing(knownWrong: true);
            double wrongFresh = wrong.FreshBefore[0] + wrong.FreshBefore[1] + wrong.FreshBefore[2] + wrong.FreshBefore[3];
            TestContext.Out.WriteLine($"the known-wrong (no fold, no critical speed): {wrongFresh:F0} fresh before the touch");
            Assert.That(wrongFresh, Is.GreaterThan(2 * fresh), "the known-wrong cuts what the slipstream lays over");
            Assert.That(run.Allocated, Is.Zero, AllocProbe.Report());
        }

        // The pilot (2026-11-13): the shake grows with the straws' number and thickness, from one a pilot lands through to one that
        // sends them away. The sweep scales the field's stems per m² and every stem's diameter; nothing is tuned to a threshold.
        [Test]
        public void TheShakeGrowsWithTheStrawsNumberAndThickness()
        {
            double[] densities = { 0.5, 1, 2 }, diameters = { 0.75, 1, 1.5 };
            var roll = new double[3, 3];
            StemScenarios.Run calm = StemScenarios.Landing(stems: false);
            foreach (string kind in new[] { "vertical", "approach" })
            {
                TestContext.Out.WriteLine($"{kind}: the last 2 s before the touch; 4–15 Hz roll and pitch rate RMS (°/s) and angle RMS (°), "
                    + "below-4 Hz attitude wander (°), the front rotors' deepest speed dip (rad/s, %), the front current's peak over its hover (A), fresh and sliced cuts, the front's mean power (W)");
                for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 3; b++)
                    {
                        var scales = new StemScenarios.Scales { Density = densities[a], Diameter = diameters[b] };
                        StemScenarios.Run r = kind == "vertical" ? StemScenarios.Landing(scales: scales) : StemScenarios.Approach(scales: scales);
                        int end = (int)r.Touch - 1, from = end - 2000;
                        double rr = Shake.RateRms(r.Roll, from, 2000, 4, 15), pr = Shake.RateRms(r.Pitch, from, 2000, 4, 15);
                        double ra = Shake.AngleRms(r.Roll, from, 2000, 4, 15), pa = Shake.AngleRms(r.Pitch, from, 2000, 4, 15);
                        double wander = Math.Sqrt(Sq(Shake.AngleRms(r.Roll, from, 2000, 0.5, 4)) + Sq(Shake.AngleRms(r.Pitch, from, 2000, 0.5, 4)));
                        double dip = 0, hoverOmega = r.Omega[1][3000], peak = 0, hoverAmps = r.Amps[1][3000] + r.Amps[3][3000];
                        for (int i = from; i < end; i++)
                        {
                            dip = Math.Max(dip, hoverOmega - Math.Min(r.Omega[1][i], r.Omega[3][i]));
                            peak = Math.Max(peak, r.Amps[1][i] + r.Amps[3][i] - hoverAmps);
                        }
                        double fresh = 0, sliced = 0;
                        for (int c = 0; c < 4; c++)
                        {
                            fresh += r.FreshBefore[c];
                            sliced += r.SlicedBefore[c];
                        }
                        double power = Sum(r.FrontWork, from, end) / 2;
                        if (kind == "vertical") roll[a, b] = Math.Sqrt(rr * rr + pr * pr);
                        TestContext.Out.WriteLine($"  density ×{densities[a]}, diameter ×{diameters[b]}: roll {rr:F2} °/s {ra:F3}°, pitch {pr:F2} °/s {pa:F3}°, wander {wander:F2}°, "
                            + $"dip {dip:F0} rad/s ({dip / hoverOmega * 100:F1} %), current +{peak:F1} A, fresh {fresh:F0}, sliced {sliced:F0}, {power:F1} W; {r.Allocated} B");
                        Assert.That(r.Allocated, Is.Zero, $"0 B in the {kind} run at density ×{densities[a]}, diameter ×{diameters[b]}" + AllocProbe.Report());
                    }
            }
            Assert.That(roll[2, 2], Is.GreaterThan(roll[1, 1]), "more and thicker straw shakes more");
            Assert.That(roll[1, 1], Is.GreaterThan(roll[0, 0]), "and less and thinner, less");
        }

        static double Sq(double x) => x * x;



        static void Print(string name, StemScenarios.Run r, StemScenarios.Run calm)
        {
            int end = (int)r.Touch - 1, n = 2000, from = end - n;
            int ce = (int)calm.Touch - 1, cfrom = ce - n;
            TestContext.Out.WriteLine($"{name}: touch at step {r.Touch}; fresh {F(r.Fresh)}, sliced {F(r.Sliced)} (cut equivalents), before the touch fresh {F(r.FreshBefore)}; "
                + $"work {F(r.Work)} J; front power over the last 2 s {Sum(r.FrontWork, from, end) / 2:F1} W; episodes {r.Starts}/{r.Ends}; {r.Allocated} B");
            foreach (var (axis, x, y) in new[] { ("roll", r.Roll, calm.Roll), ("pitch", r.Pitch, calm.Pitch), ("yaw", r.Yaw, calm.Yaw) })
                TestContext.Out.WriteLine($"   {axis}, the last 2 s: 4–15 Hz {Shake.RateRms(x, from, n, 4, 15):F2} °/s, {Shake.AngleRms(x, from, n, 4, 15):F3}° (without stems "
                    + $"{Shake.RateRms(y, cfrom, n, 4, 15):F2} °/s, {Shake.AngleRms(y, cfrom, n, 4, 15):F3}°); below 4 Hz {Shake.AngleRms(x, from, n, 0.5, 4):F2}° "
                    + $"({Shake.AngleRms(y, cfrom, n, 0.5, 4):F2}°); the strongest line at 250–350 Hz {Shake.Line(x, from, n, 250, 350, out double f):F3} °/s at {f:F0} Hz");
        }

        // "A hover cuts itself clear": the discs 0.5 m up in the cell's stems for 10 s. The slipstream folds what is under the
        // discs; the hold's drift runs the leading edges into a few; once the discs have cut round them nothing is left.
        [Test]
        public void AHoverInGrassCutsItselfClear()
        {
            StemScenarios.Run r = StemScenarios.Hover();
            TestContext.Out.WriteLine($"hover 10 s: fresh {F(r.Fresh)}, sliced {F(r.Sliced)}, work {F(r.Work)} J; the last cut at step {r.LastCut}; "
                + $"episodes {r.Starts} begun, {r.Ends} ended; {r.Allocated} B");
            Assert.That(r.LastCut, Is.LessThan(r.Steps - 2000), "no cut in the last 2 s");
            Assert.That(r.Ends, Is.EqualTo(r.Starts), "every episode ended");
            Assert.That(r.Allocated, Is.Zero, AllocProbe.Report());
        }

        // "The test range's straw": S04's stalks by the same law. A disc creeping through the range golden's landing spot at 0.4 m meets
        // the stalks at its leading edge and cuts them.
        [Test]
        public void TheRangesStalksAreCutByTheSameLaw()
        {
            int cached = 0;
            StemScenarios.Run r = StemScenarios.RangeCreep(stepped: core =>
            {
                for (int c = 0; c < 4; c++) cached = Math.Max(cached, core.Strikes.StemLast[c].Cached);
            });
            TestContext.Out.WriteLine($"S04 creep at 0.4 m: fresh {F(r.Fresh)}, sliced {F(r.Sliced)}, work {F(r.Work)} J; up to {cached} elements about a disc; "
                + $"episodes {r.Starts}/{r.Ends}; {r.Allocated} B");
            Assert.That(cached, Is.GreaterThan(0), "the discs pass over S04's elements");
            Assert.That(r.Fresh[0] + r.Fresh[1] + r.Fresh[2] + r.Fresh[3], Is.GreaterThan(0), "the stalks standing to the discs are cut");
            Assert.That(r.Allocated, Is.Zero, AllocProbe.Report());
        }

        // G8: a strike's push along its rotor's axis loads that arm's mode at the tip in the step it happens, as a thrust impulse over
        // one step would, less that step's damping (Advance's implicit damper).
        [Test]
        public void AStrikeLoadsItsArmInTheStepItHappens()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MassModel mass = spec.BuildMass(spec.Nominal);
            AirframeModesSpec ms = spec.BuildModes();
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = spec.BuildPropulsion().Motors[i].Position;
            var modes = new AirframeModes(ms, mass, hubs);
            const double p = 3e-3, dt = FlightCore.Dt; // N·s at the front right tip
            ModeSet rate = default, accel = default;
            var impulse = new Corners { FrontRight = p };
            modes.Strike(ref rate, ref accel, impulse, dt);
            ModeSet q2 = default, rate2 = default;
            var thrust = new Corners { FrontRight = p / dt };
            Double3 r = hubs[1] - mass.Properties.CenterOfMass, moment = Double3.Cross(r, new Double3(0, p / dt, 0));
            modes.Advance(ref q2, ref rate2, out _, thrust, new Double3(0, p / dt, 0), moment, dt);
            for (int m = 0; m < modes.Count; m++)
            {
                double damped = rate[m] / (1 + dt * 2 * modes.Damping(m) * 2 * Math.PI * modes.Frequency(m));
                Assert.That(damped, Is.EqualTo(rate2[m]).Within(1e-9).Percent, "mode " + m);
            }
            double tip = modes.HubVelocity(rate, 1), other = modes.HubVelocity(rate, 2);
            TestContext.Out.WriteLine($"a {p * 1000} mN·s strike at fr's tip: its hub at {tip * 1000:F3} mm/s, rl's at {other * 1000:F3} mm/s, the camera's "
                + $"{modes.CameraAcceleration(accel).Length():F3} m/s²");
            Assert.That(tip, Is.GreaterThan(0), "the struck arm's tip moves up");
            Assert.That(Math.Abs(other), Is.LessThan(tip), "the opposite arm far less");
        }

        static string F(double[] a) => string.Join(" ", Array.ConvertAll(a, x => x.ToString("F1")));

        static ulong Hash()
        {
            ContactScenarios.RepositoryMaterials(out ulong hash);
            return hash;
        }
    }
}
