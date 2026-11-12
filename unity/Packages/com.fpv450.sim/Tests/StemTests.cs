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
    /// The props in standing stems (w9-village-physics 2.2, design.md "## 2.2"): the spec's straw landing against hand calculation A
    /// and its known-wrong, the pilot's creep against hand calculation C, a hover that cuts itself clear, the range's stalks by the
    /// same law, the arm loaded at its tip (G8), and the log's replay. The hand calculations' numbers are design.md's; each run
    /// prints its own.
    /// </summary>
    public class StemTests
    {
        const double R = 0.127;

        static double Disc => Math.PI * R * R;

        // Hand calculation A: coming down at hover thrust, no Village culm under a disc stands to it, so no fresh cut and no slice
        // until the touch; the drift's few entries are the only cuts. The known-wrong (the audit's law: no lean, every stem met is
        // cut) takes 8.40 W a front rotor at the plane's 1.0 m (8.05 W a rear one), and cuts every stem reaching the standing plane.
        [Test]
        public void TheStrawLandingCutsNoStemTheSlipstreamLaidOver()
        {
            var log = new MemoryStream();
            StemScenarios.Run law = StemScenarios.Landing(log: log);
            double inDisc = 0, entered = 0, work = 0;
            for (int c = 0; c < 4; c++)
            {
                inDisc += law.FreshBefore[c] + law.SlicedBefore[c];
                entered += law.EnteredBefore[c];
                work += law.WorkBefore[c];
            }
            TestContext.Out.WriteLine($"the law: touch at step {law.Touch}, lowest disc {law.Lowest:F4} m; before the touch {inDisc} fresh and sliced, {entered:F3} stems "
                + $"entered by the drift for {work * 1000:F2} mJ; after it {Sum(law.Entered) - entered:F3} entered; episodes {law.Starts}/{law.Ends}; {law.Allocated} B, "
                + $"{law.Refused} events refused");
            Assert.That(law.Touch, Is.GreaterThan(0));
            Assert.That(inDisc, Is.EqualTo(0), "hand calculation A: no stem under a disc reaches it");
            Assert.That(entered, Is.LessThan(0.5), "the hold's drift runs a disc into a stem or two at most");
            Assert.That(law.Allocated, Is.EqualTo(0), "0 B in steady state");
            Assert.That(law.Refused, Is.EqualTo(0));

            ReplayResult replay = FlightReplay.Run(new MemoryStream(log.ToArray()), StemScenarios.OpenFarmland, RepositoryDrone.Spec().ContentHash, null,
                SoilScenarios.Table().Hash, materialsHash: Hash());
            TestContext.Out.WriteLine($"replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(law.Steps));
            LogHeader header = new FlightLogReader(new MemoryStream(log.ToArray())).Header;
            Assert.That(header.Contacts.Stems && header.Modes.StrikeLoad, Is.True, "the header logs the stems' switches (its own entry)");
            Assert.That(header.Contacts.Surfaces.Cover[(int)CoverKind.DryCulm].Dry.Diameter, Is.EqualTo(0.0015), "and the cover rows' diameters");

            // The known-wrong at the plane's 1.0 m, front and rear (fr 1, rr 0).
            double[] power = new double[4];
            int[] steps = new int[4];
            StemScenarios.Run wrong = StemScenarios.Landing(knownWrong: true, stepped: core =>
            {
                for (int c = 0; c < 4; c++)
                {
                    PropStrikes.StemRecord s = core.Strikes.StemLast[c];
                    if (!(s.Height > 0.99 && s.Height < 1.01)) continue;
                    power[c] += s.Work / FlightCore.Dt;
                    steps[c]++;
                }
            });
            double front = power[1] / steps[1], rear = power[0] / steps[0];
            TestContext.Out.WriteLine($"the known-wrong: {Sum(wrong.FreshBefore) / 4:F1} fresh stems a rotor before the touch (the cell's 69 a disc reaching 0.288 m), "
                + $"{Sum(wrong.WorkBefore):F1} J; at 1.0 m a front rotor {front:F3} W (hand 8.40), a rear {rear:F3} W (hand 8.05)");
            Assert.That(front, Is.EqualTo(8.40).Within(10).Percent, "hand calculation A's known-wrong, a front rotor");
            Assert.That(rear, Is.EqualTo(8.05).Within(10).Percent, "a rear rotor");
            Assert.That(Sum(wrong.FreshBefore) / 4, Is.GreaterThan(60), "the known-wrong cuts what the law lets lie: it fails the law's target");
        }

        // Hand calculation C: creeping forward through the cell, a front disc cuts the upright stems its leading edge meets at
        // n = 2·R·v·D(h), each at the rim, and their in-plane impulses rotate at the blade pass with π·n·J_c/4; the rear discs run in
        // the front ones' track and cut nearly none.
        [Test]
        public void ACreepCutsWhatItsLeadingEdgesMeet()
        {
            SurfaceTable table = SoilScenarios.Table();
            PropBlade blade = RepositoryDrone.Spec().BuildPropulsion().Rotor.Blade;
            MapWorld village = SlopeScenarios.Farmland();
            var patches = new StemPatch[64];
            var a = new Window[4];
            for (int c = 0; c < 4; c++) a[c] = new Window();
            FeedSignals seen = default;
            StemScenarios.Run run = StemScenarios.Creep(stepped: core =>
            {
                if (core.State.Step < 4016) return;
                for (int c = 1; c < 4; c += 2) // the front discs, fr and fl
                {
                    ContactShape d = Array.Find(core.Contacts.Spec.Shapes, s => s.Role == ContactRole.Disc && s.Corner == c);
                    Double3 hub = core.State.Position + core.State.Attitude.Rotate(d.A);
                    PropStrikes.StemRecord s = core.Strikes.StemLast[c];
                    int n = village.StemsNear(new XZ(hub.X, hub.Z), R, s.Height, patches);
                    bool inside = n > 0;
                    for (int p = 0; p < n; p++) inside &= patches[p].Cell == 86802 && patches[p].Area > Disc * 0.999;
                    if (!inside) continue;
                    a[c].Add(core, s, c);
                }
                core.GetSignals(out FeedSignals f);
                if (f.StemCuts.FrontRight > seen.StemCuts.FrontRight) seen = f;
            });
            TestContext.Out.WriteLine($"creep: entered {F(run.Entered)}, work {F(run.Work)} J; episodes {run.Starts}/{run.Ends}; {run.Allocated} B");
            for (int c = 1; c < 4; c += 2)
            {
                Window w = a[c];
                double h = w.H / w.N, v = w.V / w.N, omega = w.W / w.N, u = omega * R, seconds = w.N * FlightCore.Dt;
                // The hand calculation at the window's own height, speed and ω (design.md C's formula; its nominal 0.6 m, 0.3 m/s gives 26.36 /s).
                int n = village.StemsNear(new XZ(StemScenarios.Dense.X, StemScenarios.Dense.Z), R, h, patches);
                double rate = 0, power = 0;
                int k = blade.Stations.Length - 1;
                double band = blade.Chord[k] * Math.Sin(blade.SectionAngle[k]) + blade.Thickness[k] * Math.Cos(blade.SectionAngle[k]);
                for (int p = 0; p < n; p++)
                {
                    CoverSet set = table.Cover[(int)patches[p].Kind + (int)CoverKind.GrassBlade - 1].Dry;
                    double d = set.Diameter, di = d * (1 - 2 * set.WallShare), area = Math.PI / 4 * (d * d - di * di);
                    double each = 2 * R * v * patches[p].Density * patches[p].Area / Disc;
                    rate += each;
                    power += each * (set.CutEnergy * area + set.Density * area * band * u * u);
                }
                double jc = power / rate / u, line = Math.PI * rate * jc / 4;
                double simRate = w.Entered / seconds, simPower = w.Work / seconds;
                double ax = Order(w, 0), az = Order(w, 2);
                TestContext.Out.WriteLine($"rotor {c}: {seconds:F2} s in the cell at h {h:F3} m, v {v:F3} m/s, ω {omega:F2}: rate {simRate:F3} /s (hand {rate:F3}), "
                    + $"power {simPower:F4} W (hand {power:F4}), the blade-pass line {ax:E3} / {az:E3} N (hand π·n·J_c/4 = {line:E3})");
                Assert.That(seconds, Is.GreaterThan(1), "the window: the disc wholly in the dense cell");
                Assert.That(simRate, Is.EqualTo(rate).Within(10).Percent, "hand calculation C's rate");
                Assert.That(simPower, Is.EqualTo(power).Within(10).Percent, "its power");
                Assert.That(ax, Is.EqualTo(line).Within(10).Percent, "its blade-pass line, across");
                Assert.That(az, Is.EqualTo(line).Within(10).Percent, "and along");
            }
            Assert.That(run.Entered[0] + run.Entered[2], Is.LessThan(0.05 * (run.Entered[1] + run.Entered[3])), "the rear discs run in the front ones' track");
            Assert.That(seen.StemCuts.FrontRight, Is.GreaterThan(0), "the feed gets the field's cuts");
            Assert.That(seen.StemPieceLength.FrontRight > 0 && seen.StemCutRadius.FrontRight > 0.12, Is.True, "their pieces and radius, at the rim");
            Assert.That(run.Allocated, Is.EqualTo(0));
        }

        sealed class Window
        {
            public int N;
            public double H, V, W, Entered, Work;
            public double[] Order = new double[4]; // Σ F·cos 2ψ, Σ F·sin 2ψ for x, then z

            public void Add(FlightCore core, in PropStrikes.StemRecord s, int c)
            {
                N++;
                H += s.Height;
                Double3 v = core.State.Velocity;
                V += Math.Sqrt(v.X * v.X + v.Z * v.Z);
                W += core.State.RotorSpeed[c];
                Entered += s.Entered;
                Work += s.Work;
                double psi = 2 * core.State.RotorAngle[c], fx = s.InPlane.X / FlightCore.Dt, fz = s.InPlane.Z / FlightCore.Dt;
                Order[0] += fx * Math.Cos(psi);
                Order[1] += fx * Math.Sin(psi);
                Order[2] += fz * Math.Cos(psi);
                Order[3] += fz * Math.Sin(psi);
            }
        }

        static double Order(Window w, int at) => 2.0 / w.N * Math.Sqrt(w.Order[at] * w.Order[at] + w.Order[at + 1] * w.Order[at + 1]);

        // "A hover cuts itself clear": the discs 0.5 m up in the cell's 1.8 m stems for 10 s. The spin-up lays them over before the
        // blades are fast; the hold's drift runs the leading edges into a few, and once the discs have cut round them nothing is left.
        [Test]
        public void AHoverInGrassCutsItselfClear()
        {
            StemScenarios.Run r = StemScenarios.Hover();
            TestContext.Out.WriteLine($"hover 10 s: fresh {F(r.Fresh)}, entered {F(r.Entered)}, work {F(r.Work)} J; the last cut at step {r.LastCut}; "
                + $"episodes {r.Starts} begun, {r.Ends} ended; {r.Allocated} B");
            Assert.That(r.LastCut, Is.LessThan(r.Steps - 2000), "no cut in the last 2 s");
            Assert.That(r.Ends, Is.EqualTo(r.Starts), "every episode ended");
            Assert.That(r.Allocated, Is.EqualTo(0));
        }

        // "The test range's straw": S04's stalks by the same law. A disc creeping through the range golden's landing spot at 0.4 m meets
        // the stalks upright at its rim and cuts them; once cut they stand at its plane and are not met again at the same height.
        [Test]
        public void TheRangesStalksAreCutByTheSameLaw()
        {
            int elements = 0;
            StemScenarios.Run r = StemScenarios.RangeCreep(stepped: core =>
            {
                for (int c = 0; c < 4; c++) elements = Math.Max(elements, core.Strikes.StemLast[c].Elements);
            });
            TestContext.Out.WriteLine($"S04 creep at 0.4 m: fresh {F(r.Fresh)}, sliced {F(r.Sliced)}, work {F(r.Work)} J; up to {elements} elements under a disc; "
                + $"episodes {r.Starts}/{r.Ends}; {r.Allocated} B");
            Assert.That(elements, Is.GreaterThan(0), "the discs pass over S04's elements");
            Assert.That(Sum(r.Fresh), Is.GreaterThan(0), "the stalks standing to the discs are cut");
            Assert.That(r.Allocated, Is.EqualTo(0));
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

        static double Sum(double[] a) => a[0] + a[1] + a[2] + a[3];

        static string F(double[] a) => string.Join(" ", Array.ConvertAll(a, x => x.ToString("F3")));

        static ulong Hash()
        {
            ContactScenarios.RepositoryMaterials(out ulong hash);
            return hash;
        }
    }
}
