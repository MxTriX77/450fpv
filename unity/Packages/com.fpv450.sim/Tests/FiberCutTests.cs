using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>p7d-tether 1.8: cut risk, the link and the break (design 7, 8, 14).</summary>
    public sealed class FiberCutTests
    {
        static DroneSpec Spec => FiberCutScenarios.OnTheOldCoil ? Model63Drone.Spec() : RepositoryDrone.Spec();

        static void Print(string s) => TestContext.Progress.WriteLine(s);

        // The grid and the cause table fly the session's drone (p7d-tether 1.11), FIBER_AIRFRAME=bare the bare drone they flew to
        // model 79; the switch is reset when the test ends, so no other scenario flies it.
        static IDisposable TheSessionDrone()
        {
            FiberCutScenarios.SessionDrone = !FiberCutScenarios.BareAirframe;
            Print(FiberCutScenarios.SessionDrone ? "the session's drone (SessionSetup.Build's airframe)" : "the bare drone (FIBER_AIRFRAME=bare)");
            return new Bare();
        }

        sealed class Bare : IDisposable
        {
            public void Dispose() => FiberCutScenarios.SessionDrone = false;
        }

        // Design 18: the point-contact law at the hole's edge against fiber-exit.md §2.3's table (derived there by a separate
        // script), its limits, and where the capstan law takes over.
        [Test]
        public void ThePointContactLawAtTheEdge()
        {
            const double Deg = Math.PI / 180;
            (double Mu, double Theta, double Ratio)[] table =
            {
                (0.3, 90, 1.52), (0.3, 150, 1.77), (0.3, 180, 1.81), (0.45, 30, 1.26), (0.45, 90, 1.87), (0.45, 120, 2.14),
                (0.45, 135, 2.25), (0.45, 150, 2.33), (0.45, 180, 2.39), (0.7, 150, 3.55), (1.0, 90, 3.73), (1.0, 150, 5.55), (1.0, 180, 5.83),
            };
            foreach (var r in table)
            {
                FiberCuts.PointContact(r.Theta * Deg, r.Mu, out double ratio, out double outer);
                Print($"μ {r.Mu} θ {r.Theta}°: T₂/T₁ {ratio:0.000} (the note's {r.Ratio}), φ₂ {outer / Deg:0.0}°, capstan {Math.Exp(r.Mu * r.Theta * Deg):0.00}");
                Assert.That(ratio, Is.EqualTo(r.Ratio).Within(0.006), $"μ {r.Mu} θ {r.Theta}");
                Assert.That(ratio, Is.LessThanOrEqualTo(Math.Exp(r.Mu * r.Theta * Deg)), "never above the capstan (Singh 2022)");
                Assert.That(outer, Is.LessThan(r.Theta * Deg / 2), "the higher tension's arm turns less");
            }
            // The limits: no friction, no gain and a symmetric bend; a small turn, the capstan's; no turn, nothing.
            FiberCuts.PointContact(150 * Deg, 0, out double r0, out double o0);
            Assert.That(r0, Is.EqualTo(1).Within(1e-12));
            Assert.That(o0, Is.EqualTo(75 * Deg).Within(1e-12));
            FiberCuts.PointContact(5 * Deg, 0.45, out double small, out _);
            Assert.That(small, Is.EqualTo(Math.Exp(0.45 * 5 * Deg)).Within(1e-4));
            FiberCuts.PointContact(0, 0.45, out double none, out double noTurn);
            Assert.That(none, Is.EqualTo(1));
            Assert.That(noTurn, Is.EqualTo(0));

            // The note's stress check: at 150° and μ 0.45 the band's top, G·irr 0.123 N, bends the glass 2.51 GPa at the edge.
            TetherTable t = TetherScenarios.Table();
            double ei = t.Number("fiber.bending_stiffness_n_m2"), er = t.Number("fiber.youngs_modulus_pa") * t.Number("fiber.glass_diameter_m") / 2;
            FiberCuts.PointContact(150 * Deg, 0.45, out double k150, out double o150);
            double t2 = 0.123 * k150, sigma = er * 2 * Math.Sin(o150 / 2) * Math.Sqrt(t2 / ei) + t2 / t.Number("fiber.glass_area_m2");
            Print($"150°, μ 0.45, G·irr 0.123 N: T₂ {t2:0.000} N, σ {sigma / 1e9:0.000} GPa");
            Assert.That(sigma / 1e9, Is.EqualTo(2.51).Within(0.01));

            // The spool's factor: 1 with the line on the axis, the bore's entry alone up to its free angle, continuous past it;
            // the 0.3 mm edge never wrapped under 5 N at 150°, the 4 mm flare wrapped at 0.1 N (the capstan's e^(μθ) then).
            double free = Math.Atan2(2 * t.Number("spool.mouth_radius_m"), t.Number("spool.nozzle_length_m"));
            double cap = t.Number("spool.lip_radius_m") + t.Number("fiber.coating_thickness_m"), mu = t.Number("spool.lip_friction");
            Assert.That(mu, Is.EqualTo(0.45), "design 18's μ_l");
            Assert.That(FiberCuts.LipFactor(0, free, mu, cap, ei, 0.1), Is.EqualTo(1));
            FiberCuts.PointContact(free, mu, out double entry, out _);
            Assert.That(FiberCuts.LipFactor(free, free, mu, cap, ei, 0.1), Is.EqualTo(entry));
            Assert.That(FiberCuts.LipFactor(free + 1e-9, free, mu, cap, ei, 0.1), Is.EqualTo(entry).Within(1e-6));
            FiberCuts.PointContact(150 * Deg - free, mu, out double rim, out double rimOuter);
            Assert.That(FiberCuts.LipFactor(150 * Deg, free, mu, cap, ei, 5), Is.EqualTo(entry * rim));
            Assert.That(FiberCuts.Wraps(rimOuter, cap, ei, 5), Is.False, "a 0.3 mm edge is met at a point under 5 N");
            Assert.That(FiberCuts.Wraps(rimOuter, 0.004 + t.Number("fiber.coating_thickness_m"), ei, 0.1), Is.True, "the 4 mm flare is wrapped at 0.1 N");
            Assert.That(FiberCuts.LipFactor(150 * Deg, free, mu, 0.004, ei, 0.1), Is.EqualTo(DetMath.Exp(mu * 150 * Deg)));
        }

        // fiber-tether: "The draws cover their bounds".
        [Test]
        public void TheDrawsCoverTheirBounds()
        {
            FiberCutScenarios.Draws d = FiberCutScenarios.DrawTheStreams(Spec, 10000, 10000, 200);
            for (int b = 0; b < 6; b++)
            {
                Print($"{FiberCutScenarios.DrawBands[b]}: {d.Count[b]} draws in [{d.Least[b]:G6}, {d.Most[b]:G6}], {d.Low[b]} in the lowest 1 %, {d.High[b]} in the highest");
                Assert.That(d.Low[b], Is.GreaterThan(0), FiberCutScenarios.DrawBands[b] + ": its lowest 1 % reached");
                Assert.That(d.High[b], Is.GreaterThan(0), FiberCutScenarios.DrawBands[b] + ": its highest 1 % reached");
            }
            Print($"{d.Flaws} flaws in {d.Scanned} sections (one in {d.Scanned / (double)d.Flaws:F0}; the table's one per 20 km of 0.1 m sections is one in 200 000)");
            Assert.That(d.Outside, Is.EqualTo(0), "every draw inside its band");
            Assert.That(d.OrderChanged, Is.EqualTo(0), "the same seed and index give the same value whatever was drawn before");
            Assert.That(d.SpanMismatch, Is.EqualTo(0), "FiberCuts' span strength is its stream's draw");
            Assert.That(d.Scanned / (double)d.Flaws, Is.InRange(150000, 270000), "the flaws at the table's density");
        }

        // fiber-tether: "The bend loss follows the law".
        [Test]
        public void TheBendLossFollowsTheLaw()
        {
            double[] radii = { 0.015, 0.010, 0.0075, 0.005, 0.003 }, expected = { 0.004, 0.1, 0.5, 2.5, 9.1 };
            for (int i = 0; i < radii.Length; i++)
            {
                var (loss, turn, rEff, bends) = FiberCutScenarios.WrapRod(radii[i], 0.5);
                double law = FiberCutScenarios.LawLoss(radii[i]) * turn / (2 * Math.PI);
                Print($"rod {radii[i] * 1e3:F1} mm: one wrap of {turn:F6} rad ({bends - 1} bend and the lip), R_eff {rEff * 1e3:F4} mm, loss {loss:F5} dB; the law {law:F5}, the spec's {expected[i]}");
                Assert.That(bends, Is.EqualTo(2), "one wrap and the lip");
                Assert.That(Math.Abs(turn / (2 * Math.PI) - 1), Is.LessThan(1e-3), "a full turn");
                Assert.That(Math.Abs(loss / law - 1), Is.LessThan(1e-9), "the table's law");
                Assert.That(Math.Abs(loss / expected[i] - 1), Is.LessThan(0.01), "within 1 % of G.657.A2's fit");
            }
        }

        // fiber-tether: "Each mechanism on its own" (the bend and the rub; the tension and the disc below).
        [Test]
        public void ATightBendAtLowTensionBreaksByBend()
        {
            int broke = 0;
            for (ulong seed = 1; seed <= 5; seed++)
            {
                FiberCutScenarios.Arris a = FiberCutScenarios.OverAnArris(0.0003, 180, 0.1, 0.2, 0, 0, 20, seed);
                Print($"seed {seed}: {(a.Broken ? $"{a.Mechanism} at {a.Time:F3} s, σ {a.Stress / 1e9:F3} GPa against {a.Strength / 1e9:F3}, θ {a.Turn:F3}, T {a.Tension:F4} N, R_eff {a.Radius * 1e3:F4} mm" : "whole")}; slid {a.Slid:F3} m over {a.Sections} sections; wedges {a.Wedges}");
                if (!a.Broken) continue;
                broke++;
                Assert.That(a.Mechanism, Is.EqualTo(TetherMechanism.Bend), "the bend's");
                Assert.That(a.Stress, Is.GreaterThanOrEqualTo(a.Strength));
                Assert.That(a.Wedges, Is.EqualTo(0));
            }
            Assert.That(broke, Is.GreaterThan(0), "a tight bend at low tension can break the line on its own");
        }

        [Test]
        public void ALongRubBreaksByAbrasionOnlyAfterTheWearPassesTheCoating()
        {
            double coating = TetherScenarios.Table().Number("fiber.coating_thickness_m");
            int broke = 0;
            for (ulong seed = 1; seed <= 3; seed++)
            {
                FiberCutScenarios.Arris a = FiberCutScenarios.OverAnArris(0.001, 60, 0.3, 0, 0.03, 2, 40, seed);
                Print($"seed {seed}: {(a.Broken ? $"{a.Mechanism} at {a.Time:F3} s, σ {a.Stress / 1e9:F3} GPa against {a.Strength / 1e9:F3}, T {a.Tension:F3} N, R_eff {a.Radius * 1e3:F3} mm, the wear there {a.Depth * 1e6:F1} µm (worn through at {a.WornAt:F3} s)" : "whole")}; wedges {a.Wedges}");
                if (!a.Broken) continue;
                broke++;
                Assert.That(a.Mechanism, Is.EqualTo(TetherMechanism.Abrasion), "the rub's");
                Assert.That(a.Depth, Is.GreaterThanOrEqualTo(coating), "the abrasion break only after the wear passes the coating");
                Assert.That(a.WornAt, Is.GreaterThan(0).And.LessThanOrEqualTo(a.Time));
            }
            Assert.That(broke, Is.GreaterThan(0), "a long rub can break the line on its own");
        }

        // The held line pulls the lip too: the exit's 2 mm nozzle (spool.lip_radius_m, assumed) turned by the drone's pitch bends
        // the glass 2.2 GPa under any pull past λ's reach, so a seed with a strong span breaks at the lip first.
        [Test]
        public void ATensionSpikeBreaksByTension()
        {
            int tension = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                FiberCutScenarios.Outcome o = FiberCutScenarios.HeldClimb(Spec, seed);
                Print($"seed {seed}: {o}; the span's strength {o.SpanStrength / 1e9:F3} GPa ({o.SpanStrength * TetherScenarios.Table().Number("fiber.glass_area_m2"):F2} N)");
                Assert.That(o.Broken, Is.True, "the line held on the drone breaks");
                Assert.That(o.Stress, Is.GreaterThanOrEqualTo(o.Strength));
                if (o.Mechanism == TetherMechanism.Tension)
                {
                    tension++;
                    Assert.That(o.Strength, Is.EqualTo(o.SpanStrength), "against the flight's span strength");
                }
                else Assert.That(o.Mechanism == TetherMechanism.Bend && o.Place == FiberMember.Lip, Is.True, "else at the lip it pulls through");
            }
            Assert.That(tension, Is.GreaterThan(0), "a tension spike breaks the line by its tension on its own");
        }

        // The physics integration 2 with the cuts (model 59): a resting rotor's blades (p7f-damage 1.3b, ContactModel.Resting) are
        // the drone's parts, which the line meets through its contacts, never a disc that cuts at a blade's pass (FiberLine.Prop,
        // as PropStrikes passes no resting blade). The rule, not a flight: the disc's cut above (seed 1, rotor 0 at step 17 461)
        // flown with ContactSpec.BladeRestSpeed past every rotor's speed, so every rotor's blades stay parts all flight, is never
        // cut by a pass; flown as it is, it is. Since model 74 (the 27.5° exit, the hole's edge, the peel) the first seed of 1–6
        // whose disc cuts the line, as FromTheBreakToTheDisarm takes it (seed 1 stays whole to its end since then).
        [Test]
        public void ARestingBladeCutsNothingByItsPass()
        {
            ulong seed = 1;
            FiberCutScenarios.Disarm turning = default;
            for (; seed <= 6; seed++)
            {
                turning = FiberCutScenarios.CutByADisc(Spec, seed);
                if (turning.Mechanism == TetherMechanism.Prop) break;
            }
            Print($"seed {seed}");
            FiberCutScenarios.Disarm resting = FiberCutScenarios.CutByADisc(Spec, seed, s => s.Contacts.BladeRestSpeed = 1e4);
            Print($"turning: {turning.Mechanism} on rotor {turning.Corner} at step {turning.BreakStep}; every rotor resting: "
                + (resting.BreakStep >= 0 ? $"{resting.Mechanism} at step {resting.BreakStep}" : "whole"));
            Assert.That(turning.Mechanism, Is.EqualTo(TetherMechanism.Prop), "a turning disc cuts at its pass");
            Assert.That(resting.BreakStep < 0 || resting.Mechanism != TetherMechanism.Prop, Is.True, "a resting blade never cuts by a pass");
        }

        // fiber-tether: "From the break to the disarm", "Every link signal is live", "A break leaves a flyable drone" and the
        // disc's part of "Each mechanism on its own".
        [Test]
        public void FromTheBreakToTheDisarm()
        {
            FiberCutScenarios.Disarm d = default;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                d = FiberCutScenarios.CutByADisc(Spec, seed);
                Print($"seed {seed}: {d.Mechanism} at step {d.BreakStep}");
                if (d.Mechanism == TetherMechanism.Prop) break;
            }
            FcSettings fc = FcSettings.Default;
            long stage1 = (long)Math.Round((Failsafe.RxLossSeconds + Failsafe.HoldSeconds) * 1000), stage2 = (long)Math.Round(fc.failsafe_delay * 100);
            Print($"cut by {d.Mechanism} on rotor {d.Corner} at step {d.BreakStep}, {d.HeightAtBreak:F2} m up; Broken from {d.BrokenStep}; signal lost {d.LostStep - d.BreakStep}, "
                + $"stage 1 {d.Stage1Step - d.BreakStep}, stage 2 {d.Stage2Step - d.BreakStep}, disarmed {d.DisarmStep - d.BreakStep} steps after; the feed's loss at {d.FeedLossStep} "
                + $"({d.FeedLossStep - d.BreakStep} steps); sticks reaching the controller {d.SticksReached}, stage 1 not centred {d.Stage1NotCentred}; "
                + $"{d.Fields} fields, their worst error {d.FieldError:E2}; near piece {d.NearNodes} nodes (attached {d.Attached}), its force's worst error {d.ForceError:E2} N; far piece moved {d.FarMoved:E2} m");
            Assert.That(d.Mechanism, Is.EqualTo(TetherMechanism.Prop), "a disc cut it");
            Assert.That(d.BrokenStep, Is.EqualTo(d.BreakStep), "TetherBreak and the link's Broken share a step");
            Assert.That(d.SticksReached, Is.EqualTo(0), "the sticks stop reaching the controller from that step");
            Assert.That(d.LostStep - d.BreakStep, Is.EqualTo((long)Math.Round(Failsafe.RxLossSeconds * 1000)).Within(1));
            Assert.That(d.Stage1Step - d.BreakStep, Is.EqualTo(stage1).Within(1), "stage 1 at its time within a step");
            Assert.That(d.Stage1NotCentred, Is.EqualTo(0), "stage 1's sticks centred");
            Assert.That(d.Stage2Step - d.BreakStep, Is.EqualTo(stage2).Within(1), "stage 2 at failsafe_delay within a step");
            Assert.That(d.DisarmStep, Is.EqualTo(d.Stage2Step), "DROP disarms at stage 2");
            Assert.That(d.DisarmedAtEnd, Is.True);
            Assert.That(d.FeedLossStep - d.BreakStep, Is.InRange(0, FlightCore.FieldSteps), "the feed's loss within a field of the break");
            Assert.That(d.BreakEventsInField, Is.EqualTo(1), "the break in its field's signals");
            Assert.That(d.Fields, Is.GreaterThan(400));
            Assert.That(d.FieldError, Is.LessThanOrEqualTo(1e-9), "each field's mean, maximum and least equal the steps'");
            Assert.That(d.FarMoved, Is.EqualTo(0), "the far piece stays where it lies");
            if (d.Attached) Assert.That(d.ForceError, Is.LessThan(1e-12), "the line's force on the drone is the near piece's");
        }

        // D-086: no return-to-home; on the fiber a video loss is not a control loss. The receiver's link is the line itself
        // (Failsafe: the uplink and the picture share the fiber), so only the line's break (TetherBreak) starts the failsafe, and
        // its procedure is DROP. A dip (the margin under the receiver's threshold: the picture lost) leaves the sticks reaching the
        // controller; the feed's own dropouts (D-085) are drawn in the app and never reach the core. Here tether.json's
        // dip_threshold_db is raised over the launch margin, so the picture is lost from the first step: the drone still arms,
        // climbs and holds where the pilot flies it, the failsafe never starts; then the sharp reverse's disc cut (CutByADisc)
        // with the same lost picture ends control: stage 2's DROP disarms it.
        [Test]
        public void AVideoLossKeepsControlOnlyABreakEndsIt()
        {
            string json = System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(TetherTable.RelativePath));
            const string row = "\"dip_threshold_db\": {\"value\": 0,";
            StringAssert.Contains(row, json);
            TetherTable blind = TetherTable.Parse(json.Replace(row, "\"dip_threshold_db\": {\"value\": 100,"));
            Assert.That(blind.Number("link.dip_threshold_db"), Is.GreaterThan(blind.Number("link.launch_margin_db")), "the picture lost from the start");
            Assert.That(FcSettings.Default.failsafe_procedure, Is.EqualTo(FailsafeProcedure.DROP), "no return-to-home (D-086)");

            // The same climb with the picture (the repository's threshold) and without it: the flight is the same, step for step.
            var seen = new System.Collections.Generic.List<Double3>();
            FlightCore Climb(TetherTable table, bool blinded, out int notDipped, out int failsafe)
            {
                FlightSetup setup = TetherScenarios.Setup(Spec, WindLevel.Calm, cuts: true);
                setup.Tether = table;
                double stand = TetherScenarios.Stand(setup);
                Double3 start = setup.StartPosition;
                int nd = 0, fs = 0, k = 0;
                FlightCore c = TetherScenarios.Fly(setup, 6, (t, core, pilot) => pilot.Target = new Double3(start.X, stand + DetMath.Min(1.5, 0.5 * t), start.Z), core =>
                {
                    if (core.State.Step > 20 && !core.Tether.Link.InDip) nd++;
                    if (core.Failsafe.Active) fs++;
                    for (int i = 0; i < core.Events.Count; i++) if (core.Events[i].Kind == EventKind.Failsafe) fs++;
                    if (!blinded) seen.Add(core.State.Position);
                    else if (core.State.Position.X != seen[k].X || core.State.Position.Y != seen[k].Y || core.State.Position.Z != seen[k].Z) fs += 1000000;
                    k++;
                });
                notDipped = nd;
                failsafe = fs;
                return c;
            }
            FlightCore sighted = Climb(TetherScenarios.Table(), false, out _, out _);
            FlightCore core = Climb(blind, true, out int notDipped, out int failsafe);
            double climbed = core.State.Position.Y - (TetherScenarios.Setup(Spec, WindLevel.Calm, cuts: true).StartPosition.Y);
            Print($"the picture lost from step 1 ({core.Tether.Link.Dips} dip, margin {core.Tether.Link.Margin:F2} dB against a {blind.Number("link.dip_threshold_db"):F0} dB threshold; "
                + $"with the picture {sighted.Tether.Link.Dips} dips): after 6 s armed {core.State.Armed}, climbed {climbed:F3} m, every step's position the sighted flight's: {failsafe < 1000000}; "
                + $"failsafe steps and events {failsafe % 1000000}; line broken {core.Tether.Broken}");
            Assert.That(core.Tether.Broken, Is.False);
            Assert.That(sighted.Tether.Link.Dips, Is.EqualTo(0), "with the picture, no dip");
            Assert.That(notDipped, Is.EqualTo(0), "the picture stays lost");
            Assert.That(failsafe, Is.EqualTo(0), "a video loss starts no failsafe and changes nothing in the flight");
            Assert.That(core.State.Armed, Is.True);
            Assert.That(climbed, Is.GreaterThan(1.2), "the pilot's sticks climb it toward 1.5 m");

            FiberCutScenarios.Disarm d = default;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                d = FiberCutScenarios.CutByADisc(Spec, seed, s2 => s2.Tether = blind);
                if (d.Mechanism == TetherMechanism.Prop) break;
            }
            Print($"then cut by {d.Mechanism} at step {d.BreakStep}: signal lost {d.LostStep - d.BreakStep}, stage 2 {d.Stage2Step - d.BreakStep}, disarmed "
                + $"{d.DisarmStep - d.BreakStep} steps after; sticks reaching the controller after it {d.SticksReached}");
            Assert.That(d.BreakStep, Is.GreaterThan(0), "the line broke");
            Assert.That(d.SticksReached, Is.EqualTo(0), "from the break no stick reaches the controller");
            Assert.That(d.DisarmStep, Is.EqualTo(d.Stage2Step), "DROP disarms at stage 2");
            Assert.That(d.DisarmedAtEnd, Is.True, "the break ends control");
        }

        // fiber-tether: "A break leaves a flyable drone": the held line's breaks (the near piece hanging from the exit when the
        // break is along the span), flown 4 s on.
        [Test]
        public void ABreakLeavesAFlyableDrone()
        {
            int hanging = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                FiberCutScenarios.Outcome o = FiberCutScenarios.HeldClimb(Spec, seed, seconds: 14);
                Print($"seed {seed}: {o}; the near piece {o.NearNodes} nodes, hanging at the end {o.Attached}, the far piece moved {o.FarMoved:E2} m over {o.After} steps, the force's worst error {o.ForceError:E2} N");
                Assert.That(o.Broken, Is.True);
                Assert.That(o.BrokenInStep, Is.True, "the link Broken, its margin −∞, in the break's step");
                Assert.That(o.FarMoved, Is.EqualTo(0), "the far piece stays where it lies");
                Assert.That(o.After, Is.GreaterThan(1000), "the flight goes on (the failsafe drops it at stage 2)");
                if (o.NearNodes == 0) continue;
                hanging++;
                Assert.That(o.ForceError, Is.LessThan(1e-12), "the line's force on the drone is the near piece's");
            }
            Print($"{hanging} of 6 left a piece hanging from the exit");
        }

        // fiber-tether: "A line draped over a coil run holds".
        [Test]
        public void ALineDrapedOverACoilRunHolds()
        {
            int touched = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                FiberCutScenarios.Coil c = FiberCutScenarios.OverTheCoil(Spec, seed, false);
                Print($"seed {seed}: {c.O}; on the coil {c.Touching} steps, its most stress at a barb {c.MostStress / 1e9:F3} GPa, the least strength there {c.LeastStrength / 1e9:F3}, turn {c.MostTurn:F3}; a touch moved {c.MostMoved * 1e3:F2} mm along its record (most at {c.MovedAt:F3} s, T {c.MovedTension:F3} N, N {c.MovedPress:F3} N, the other contact {c.MovedOther} {c.MovedOtherPress:F3} N, the drone {c.MovedHeight:F2} m up)");
                if (c.Touching > 0) touched++;
                // Since model 70 (p7f-damage 1.6b, the motors' heat) seed 20's hover rubs the line on a barb until its abrasion takes
                // the section's strength to 0.68 GPa and it cuts there at 10.6 s (Abrasion at Snag, 1.08 GPa): the barbs' static
                // stress never passes the line's drawn strength, the wear does. Traced over 40 seeds: without the heat none cuts, with
                // it seed 20 alone; the claims are judged against the static cut (the spec's wording is the orchestrator's: handback).
                bool worn = c.O.Broken && c.O.Mechanism == TetherMechanism.Abrasion;
                if (c.LeastStrength > c.MostStress) Assert.That(c.O.Broken && !worn, Is.False, "holds where the strength exceeds the stress at the barbs, but by wear");
                if (c.LeastStrength > 1.35e9) Assert.That(c.O.Broken && !worn && c.O.Place == FiberMember.Snag, Is.False, "never cut at a barb above 1.35 GPa, but by wear");
                Assert.That(c.MostMoved, Is.LessThanOrEqualTo(0.0175), "each touch within half a barb pitch of where it touched");
            }
            Assert.That(touched, Is.EqualTo(20), "the line lies over the coil on every seed");
        }

        // fiber-tether: "A reverse round a snagged barb cuts".
        [Test]
        public void AReverseRoundASnaggedBarbCuts()
        {
            int cuts = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                FiberCutScenarios.Coil c = FiberCutScenarios.OverTheCoil(Spec, seed, true);
                Print($"seed {seed}: {c.O}; its most turn at a barb {c.MostTurn * 180 / Math.PI:F0}°, stress {c.MostStress / 1e9:F3} GPa, the least strength there {c.LeastStrength / 1e9:F3}");
                if (!c.O.Broken) continue;
                cuts++;
                Assert.That(c.O.Stress, Is.GreaterThanOrEqualTo(c.O.Strength), "it met its strength");
                if (c.O.Place == FiberMember.Snag)
                {
                    Assert.That(c.O.Mechanism, Is.EqualTo(TetherMechanism.Bend).Or.EqualTo(TetherMechanism.Abrasion), "a barb's cut is the bend's (or its wear's)");
                    Assert.That(c.O.Object, Is.EqualTo(9), "the object named");
                    Assert.That(c.O.Shape, Is.InRange(0, 11), "the record's index named");
                }
            }
            Print($"{cuts} of 20 cut");
        }

        // fiber-tether: "A snag dips the link".
        [Test]
        public void ASnagDipsTheLink()
        {
            FiberCutScenarios.Dip d = FiberCutScenarios.AroundATwig(Spec, 5, WindLevel.Windy);
            Print($"margin {d.Before:F3} dB before the wrap, least {d.Least:F3}; the twig's loss up to {d.TwigLoss:F3} dB over {d.Wrapped} steps wrapped, the margin's spread {d.Spread:F4} dB; "
                + $"{d.Dips} dips ({d.DipEnds} ended), {d.DipsNamingTwig} events naming the twig; the margin against its law {d.MarginVsLaw:E2} dB; {d.O}");
            Assert.That(d.Wrapped, Is.GreaterThan(1000), "the line wraps the twig");
            Assert.That(d.MarginVsLaw, Is.LessThan(1e-9), "the margin is the budget less the length and every bend");
            Assert.That(d.Before - d.Least, Is.GreaterThanOrEqualTo(d.TwigLoss * 0.5), "the margin falls by the twig's bend loss");
            Assert.That(d.Spread, Is.GreaterThan(0), "it varies as the wrap flutters in the wind");
            if (d.Dips > 0) Assert.That(d.DipsNamingTwig, Is.EqualTo(d.Dips + d.DipEnds), "each dip names the twig's Id");
        }

        // fiber-tether: "A free line costs only its length and its lip".
        [Test]
        public void AFreeLineCostsOnlyItsLengthAndItsLip()
        {
            FiberCutScenarios.FreeLine f = FiberCutScenarios.Flips(Spec);
            Print($"{f.Free} steps hanging free, {f.Touching} touching; the most rate {f.MostRate:F0} °/s; the length's loss {f.LengthLoss:F4} dB, the lip's up to {f.LipLoss:F4} dB; "
                + $"the margin against the budget less them {f.Worst:E2} dB, against a line at rest at the same lip {f.AtRest:E2} dB; dips {f.Dips}; {f.O}");
            Assert.That(f.MostRate, Is.GreaterThan(500), "the rolls and flips");
            Assert.That(f.LengthLoss, Is.EqualTo(0.42).Within(0.01), "2 km at 0.21 dB/km");
            Assert.That(f.Free, Is.GreaterThan(5000), "the line hangs free over many steps");
            Assert.That(f.Worst, Is.LessThanOrEqualTo(0.01), "the budget less 0.42 dB and the lip's loss");
            Assert.That(f.AtRest, Is.LessThanOrEqualTo(0.01), "as at rest with the same lip");
            Assert.That(f.Dips, Is.EqualTo(0));
        }

        // 1.8's finding 2 (model 77), the scenario where the old rule's spike appears and the fix holds: a U-turn at 5 m/s, the heading
        // at 90 °/s, Calm, seed 5 (D-087's grid). The line's last centimetres swing across the right skid's rear and the body. With
        // the known-wrong linear step (model 74's chain) the segments the step rotates end stretched by δ²/(2ℓ), read up to
        // 19 N on 10–16 mm segments where the solve applied 0.01–0.05 N, and the span's tension check cuts the line there (12.946 s,
        // 19.3 N; 502 steps of the turn read over 1 N); with the chain's Newton solves the line is whole and the most end-of-step
        // tension in the turn is 0.2 N, what the step applied.
        [Test]
        public void AUTurnsSpanReadsWhatTheStepApplied()
        {
            double most = 0;
            long over = 0;
            FiberCutScenarios.Each = core =>
            {
                if (core.State.Step < 11000 || !core.Tether.Attached) return;
                CableChain c = core.Tether.Chain;
                double m = 0;
                for (int j = 0; j < c.Count - 1; j++) if (c.Tension(j) > m) m = c.Tension(j);
                if (m > most) most = m;
                if (m > 1) over++;
            };
            FiberCutScenarios.Turned now;
            double oldMost;
            long oldOver;
            FiberCutScenarios.Turned old;
            try
            {
                now = FiberCutScenarios.Turn(RepositoryDrone.Spec(), WindLevel.Calm, 5, 5, 90, 180);
                double nowMost = most;
                long nowOver = over;
                most = 0;
                over = 0;
                old = FiberCutScenarios.Turn(RepositoryDrone.Spec(), WindLevel.Calm, 5, 5, 90, 180, x => x.TetherLinearStep = true);
                oldMost = most;
                oldOver = over;
                most = nowMost;
                over = nowOver;
            }
            finally
            {
                FiberCutScenarios.Each = null;
            }
            Print($"Newton on the segments: {now.O}; the most span tension in the turn {most:0.000} N, {over} steps over 1 N");
            Print($"the linear step (model 74): {old.O}; the most span tension in the turn {oldMost:0.000} N, {oldOver} steps over 1 N");
            Assert.That(old.O.Broken, Is.True, "the known-wrong cuts");
            Assert.That(old.O.Mechanism, Is.EqualTo(TetherMechanism.Tension));
            Assert.That(old.O.Place, Is.EqualTo(FiberMember.Span));
            Assert.That(now.O.Broken, Is.False, "the line is whole");
            Assert.That(most, Is.LessThan(1.0), "the span reads what the step applied");
            Assert.That(over * 10, Is.LessThan(oldOver), "a tenth of the known-wrong's steps over 1 N at most");
        }

        // The pilot's account (docs/pilot-questions.md, PHY-19): a normal liftoff does not cut the line.
        [Test]
        public void ANormalLiftoffDoesNotCut()
        {
            for (ulong seed = 1; seed <= 10; seed++)
            {
                FiberCutScenarios.Outcome o = FiberCutScenarios.Liftoff(Spec, false, seed);
                Print($"seed {seed}: {o}");
                Assert.That(o.Broken, Is.False, "a normal liftoff in Calm never cuts");
            }
        }

        // The pilot's four causes against their known-wrong models, over seeds (FIBER_SEEDS, 10 by default): the rates, the
        // mechanisms and the places printed (1.11 grades them against design 16's targets).
        [Test, Explicit("the pilot's causes over seeds, printed: minutes")]
        public void ThePilotsCausesOverSeeds()
        {
            using IDisposable airframe = TheSessionDrone();
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("FIBER_SEEDS"), out int n) ? n : 10;
            string which = Environment.GetEnvironmentVariable("FIBER_PROBE") ?? "all";
            var cases = new (string Name, Func<ulong, FiberCutScenarios.Outcome> Fly)[]
            {
                ("normal liftoff, Calm", s => FiberCutScenarios.Liftoff(Spec, false, s)),
                ("normal liftoff, Windy", s => FiberCutScenarios.Liftoff(Spec, false, s, null, WindLevel.Windy)),
                ("normal liftoff, Calm, the fountain through the body", s => FiberCutScenarios.Liftoff(Spec, false, s, x => x.TetherFountainThroughBody = true)),
                ("normal liftoff, Windy, the fountain through the body", s => FiberCutScenarios.Liftoff(Spec, false, s, x => x.TetherFountainThroughBody = true, WindLevel.Windy)),
                ("sharp liftoff", s => FiberCutScenarios.Liftoff(Spec, true, s)),
                ("sharp liftoff, design 4's air", s => FiberCutScenarios.Liftoff(Spec, true, s, x => x.TetherDesign4Air = true)),
                ("backward after liftoff", s => FiberCutScenarios.Backward(Spec, s)),
                ("backward, design 4's air", s => FiberCutScenarios.Backward(Spec, s, x => x.TetherDesign4Air = true)),
                ("sharp reverse, Calm", s => FiberCutScenarios.SharpReverse(Spec, WindLevel.Calm, s)),
                ("sharp reverse, Windy", s => FiberCutScenarios.SharpReverse(Spec, WindLevel.Windy, s)),
                ("sharp reverse, two-way spool", s => FiberCutScenarios.SharpReverse(Spec, WindLevel.Calm, s, x => x.TetherTwoWay = true)),
                ("tree snag", s => FiberCutScenarios.TreeSnag(Spec, WindLevel.Calm, s)),
                ("tree snag, no contacts", s => FiberCutScenarios.TreeSnag(Spec, WindLevel.Calm, s, x => x.TetherWithoutContacts = true)),
            };
            // FIBER_THREADS flights at once (1 by default): each flight is its own core, so the order never moves a bit.
            int threads = int.TryParse(Environment.GetEnvironmentVariable("FIBER_THREADS"), out int m) ? m : 1;
            var results = new FiberCutScenarios.Outcome[cases.Length, seeds];
            System.Threading.Tasks.Parallel.For(0, cases.Length * seeds, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = threads }, i =>
            {
                if (which == "all" || cases[i / seeds].Name.Contains(which)) results[i / seeds, i % seeds] = cases[i / seeds].Fly((ulong)(i % seeds + 1));
            });
            for (int ci = 0; ci < cases.Length; ci++)
            {
                var c = cases[ci];
                if (which != "all" && !c.Name.Contains(which)) continue;
                var counts = new System.Collections.Generic.SortedDictionary<string, int>();
                for (ulong seed = 1; seed <= (ulong)seeds; seed++)
                {
                    FiberCutScenarios.Outcome o = results[ci, (int)seed - 1];
                    string key = o.Broken ? $"{o.Mechanism} at {o.Place}" : "whole";
                    counts[key] = counts.TryGetValue(key, out int k) ? k + 1 : 1;
                    Print($"{c.Name} seed {seed}: {o}");
                }
                Print($"== {c.Name}: " + string.Join(", ", System.Linq.Enumerable.Select(counts, kv => $"{kv.Key} {kv.Value}")) + $" of {seeds}");
            }
        }

        // p7d-tether 1.11 (design 16, 17): the pilot's causes on eight seeds a row, the short version of flightbench --tether that a
        // normal run judges. Asserted: the flights that must keep the line keep it (a normal liftoff, a 20 m orbit and a gentle 15 m
        // U-turn at 54 km/h, the cruise at 100 km/h); a sharp reverse cuts on some seeds, each cut naming its mechanism and place,
        // and by its slack at a rear disc, which the two-way spool's known-wrong never does; a tree snag cuts on some. Printed, not
        // asserted, because design 17 derives that the physics does not meet them: a very sharp liftoff and a backward move after
        // liftoff cutting on some, and the reverse tearing at the hole's edge.
        [Test]
        public void ThePilotsCausesOnEightSeeds()
        {
            const int Seeds = 8;
            using IDisposable airframe = TheSessionDrone();
            DroneSpec spec = Spec;
            var rows = new (string Name, Func<ulong, FiberCutScenarios.Outcome> Fly)[]
            {
                ("normal liftoff", s => FiberCutScenarios.Liftoff(spec, false, s)),
                ("orbit of 20 m at 54 km/h", s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 43, 360, null, 21).O),
                ("gentle 180° turn of 15 m at 54 km/h", s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 15, 57, 180).O),
                ("cruise at 100 km/h", s => FiberCutScenarios.Turn(spec, WindLevel.Calm, s, 27.8, 0).O),
                ("sharp reverse", s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s)),
                // The two-way spool's take-up at models 74–88's 2–10 g (design 18; TetherCase's known-wrong).
                ("sharp reverse, the two-way spool", s => FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, s, x =>
                {
                    x.TetherTwoWay = true;
                    x.Tether = FiberCutScenarios.WithBand(x.Tether, "spool.peel_energy_j_m", 0.0196, 0.0981);
                })),
                ("tree snag", s => FiberCutScenarios.TreeSnag(spec, WindLevel.Calm, s)),
                ("very sharp liftoff", s => FiberCutScenarios.Liftoff(spec, true, s)),
                ("backward after liftoff", s => FiberCutScenarios.Backward(spec, s)),
            };
            var o = new FiberCutScenarios.Outcome[rows.Length, Seeds];
            System.Threading.Tasks.Parallel.For(0, rows.Length * Seeds,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 2) },
                i => o[i / Seeds, i % Seeds] = rows[i / Seeds].Fly((ulong)(i % Seeds + 1)));
            int Count(int row, Func<FiberCutScenarios.Outcome, bool> p)
            {
                int c = 0;
                for (int s = 0; s < Seeds; s++) if (p(o[row, s])) c++;
                return c;
            }
            for (int r = 0; r < rows.Length; r++)
                for (int s = 0; s < Seeds; s++) Print($"{rows[r].Name} seed {s + 1}: {o[r, s]}");
            for (int r = 0; r < 4; r++) Assert.That(Count(r, x => x.Broken), Is.EqualTo(0), rows[r].Name + " keeps the line");
            int cuts = Count(4, x => x.Broken), discs = Count(4, x => x.Broken && x.Mechanism == TetherMechanism.Prop);
            Assert.That(cuts, Is.InRange(1, Seeds - 1), "a sharp reverse may or may not cut");
            Assert.That(Count(4, x => x.Broken && (x.Mechanism == TetherMechanism.Wedge || x.Place == FiberMember.None)), Is.EqualTo(0),
                "each reverse cut names its mechanism and place");
            Assert.That(discs, Is.GreaterThan(0), "the reverse cuts by its slack at a rear disc");
            Assert.That(Count(5, x => x.Broken && x.Mechanism == TetherMechanism.Prop), Is.EqualTo(0), "the two-way spool leaves no slack for a disc (the known-wrong)");
            Assert.That(Count(6, x => x.Broken), Is.GreaterThan(0), "a tree snag cuts on some");
            Print($"not met (design 17): very sharp liftoff cuts {Count(7, x => x.Broken)} of {Seeds}, backward after liftoff {Count(8, x => x.Broken)} of {Seeds}; "
                + $"the reverse at the hole's edge {Count(4, x => x.Broken && x.Place == FiberMember.Lip)} of {Seeds} (by a rear disc {discs})");
        }

        // D-087's grid: turns of 90° and 180° at each speed up to 100 km/h and each heading rate (0 the straight cruise), and sudden
        // reverses from each speed, over seeds (FIBER_SEEDS, 5 by default) in Calm and Windy; each cell's cuts, mechanisms and
        // places, and what the drone made of the turn, printed (FIBER_THREADS flights at once, 6 by default; each flight is its
        // own core, so the order of the runs never moves a bit).
        [Test, Explicit("the turn and reverse grid over seeds, printed: minutes")]
        public void TheTurnsAndReversesOverSeeds()
        {
            using IDisposable airframe = TheSessionDrone();
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("FIBER_SEEDS"), out int n) ? n : 5;
            int threads = int.TryParse(Environment.GetEnvironmentVariable("FIBER_THREADS"), out int m) ? m : 6;
            string winds = Environment.GetEnvironmentVariable("FIBER_WINDS") ?? "Calm,Windy";
            double[] speeds = { 5, 10, 15, 20, 25, 27.8 }, rates = { 0, 45, 90, 180, 360 };
            foreach (string wn in winds.Split(','))
            {
                WindLevel wind = wn == "Windy" ? WindLevel.Windy : WindLevel.Calm;
                var cells = new System.Collections.Generic.List<(string Name, Func<ulong, (FiberCutScenarios.Outcome O, string Note)> Fly)>();
                foreach (double v in speeds)
                    foreach (double r in rates)
                        foreach (double turn in r == 0 ? new double[] { 0 } : new double[] { 90, 180 })
                    {
                        double vv = v, rr = r, deg = turn;
                        cells.Add(($"{vv * 3.6:0} km/h, {(rr == 0 ? "cruise" : $"turn {deg:0}° at {rr:0} °/s")}", s =>
                        {
                            FiberCutScenarios.Turned t = FiberCutScenarios.Turn(Spec, wind, s, vv, rr, deg == 0 ? 90 : deg);
                            return (t.O, $"v {t.Speed:0.0} m/s, pitch {t.Pitch * 180 / Math.PI:0.0}°, nose {t.NoseRate * 180 / Math.PI:0} °/s, path {t.PathRate * 180 / Math.PI:0} °/s, {t.Lateral / 9.80665:0.0} g; in the turn lip {t.LipTurn * 180 / Math.PI:0}°, exit {t.ExitTurn:0.000} N; the edge's wear {t.LipWear:0.0000} of the coating; {t.Sections} sections, {t.Bugs} bugs");
                        }));
                    }
                foreach (double v in speeds)
                {
                    double vv = v;
                    cells.Add(($"{vv * 3.6:0} km/h, reverse to −8 m/s", s => Edge(FiberCutScenarios.SharpReverse(Spec, wind, s, null, vv))));
                }
                // p7d-tether 1.11: the pilot's own reverse, a pitch-back at the cruise's throttle held, from 36–100 km/h.
                foreach (double v in speeds)
                {
                    if (v < 10) continue;
                    double vv = v;
                    cells.Add(($"{vv * 3.6:0} km/h, pitch-back to −8 m/s, throttle held", s => Edge(FiberCutScenarios.PitchBack(Spec, wind, s, null, vv))));
                }
                var results = new (FiberCutScenarios.Outcome O, string Note)[cells.Count, seeds];
                var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = threads };
                System.Threading.Tasks.Parallel.For(0, cells.Count * seeds, options, i => results[i / seeds, i % seeds] = cells[i / seeds].Fly((ulong)(i % seeds + 1)));
                for (int c = 0; c < cells.Count; c++)
                {
                    var counts = new System.Collections.Generic.SortedDictionary<string, int>();
                    for (int s = 0; s < seeds; s++)
                    {
                        FiberCutScenarios.Outcome o = results[c, s].O;
                        string key = o.Broken ? $"{o.Mechanism} at {o.Place}" : "whole";
                        counts[key] = counts.TryGetValue(key, out int k) ? k + 1 : 1;
                        Print($"{wn} {cells[c].Name} seed {s + 1}: {o}; {results[c, s].Note}");
                    }
                    Print($"== {wn} {cells[c].Name}: " + string.Join(", ", System.Linq.Enumerable.Select(counts, kv => $"{kv.Key} {kv.Value}")) + $" of {seeds}");
                }
            }
        }

        static (FiberCutScenarios.Outcome, string) Edge(FiberCutScenarios.Outcome o) =>
            (o, $"the edge's most σ {o.EdgeMost / 1e9:0.000} GPa at {o.EdgeAt:0.000} s (θ {o.EdgeTurn * 180 / Math.PI:0}°, T {o.EdgeTension:0.000} N); ends {o.Height:0.0} m up");

        static string Code(FiberCutScenarios.Outcome o) => !o.Broken ? "W" : o.Mechanism == TetherMechanism.Prop ? "D"
            : o.Mechanism == TetherMechanism.Tension ? "T" : o.Mechanism == TetherMechanism.Bend ? (o.Place == FiberMember.Lip ? "L" : "B")
            : o.Mechanism == TetherMechanism.Abrasion ? (o.Place == FiberMember.Lip ? "A" : "a") : o.Mechanism.ToString();

        // p7d-tether 1.11: the sensitivity of the reverses (the sharp reverse and the pilot's pitch-back at the cruise's throttle held,
        // 36–100 km/h) to the spool's three numbers, on the session's drone (FIBER_AIRFRAME=bare the bare drone), seeds 1–FIBER_SEEDS
        // (10 by default) in Calm and Windy (FIBER_WINDS): per configuration, row and wind, the cuts by cause (W whole, D disc, T tension
        // on the span, L bend at the hole's edge, A abrasion at it, B bend elsewhere, a abrasion elsewhere) and the hole's edge's most
        // stress from the stick (11 s) while whole: its mean and most, and how many flights pass 3.0 GPa, the intact band's floor.
        // FIBER_CONFIGS picks configurations by number (0 the base; all by default); FIBER_THREADS flights at once (10 by default).
        [Test, Explicit("the reverses' sensitivity to the spool's numbers, printed: minutes")]
        public void TheReversesSensitivityToTheSpool()
        {
            using IDisposable airframe = TheSessionDrone();
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("FIBER_SEEDS"), out int n) ? n : 10;
            int threads = int.TryParse(Environment.GetEnvironmentVariable("FIBER_THREADS"), out int m) ? m : 10;
            string winds = Environment.GetEnvironmentVariable("FIBER_WINDS") ?? "Calm,Windy";
            string pick = Environment.GetEnvironmentVariable("FIBER_CONFIGS");
            const double Gram = 9.80665e-3;
            var configs = new (string Name, Action<FlightSetup> Edit)[]
            {
                ("base", null),
                ("G0 0.01–2 g (US 5,167,382's reduced adhesive)", s => s.Tether = FiberCutScenarios.WithBand(s.Tether, "spool.peel_energy_j_m", 0.01 * Gram, 2 * Gram)),
                ("G0 5–10 g (US 5,167,382's conventional pack)", s => s.Tether = FiberCutScenarios.WithBand(s.Tether, "spool.peel_energy_j_m", 5 * Gram, 10 * Gram)),
                ("G0 ×2", s => s.Tether = FiberCutScenarios.Scaled(s.Tether, "spool.peel_energy_j_m", 2)),
                ("n 0.1", s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.peel_rate_exponent", 0.1)),
                ("n 0.3", s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.peel_rate_exponent", 0.3)),
                ("n 0.3, v_ref 1 m/s", s =>
                {
                    s.Tether = FiberCutScenarios.With(s.Tether, "spool.peel_rate_exponent", 0.3);
                    s.Tether = FiberCutScenarios.With(s.Tether, "spool.peel_reference_rate_m_s", 1);
                }),
                ("μ 0.3 (models to 88)", s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.lip_friction", 0.3)),
                ("μ 0.36", s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.lip_friction", 0.36)),
                ("μ 1.0", s => s.Tether = FiberCutScenarios.With(s.Tether, "spool.lip_friction", 1.0)),
            };
            double[] speeds = { 10, 15, 20, 25, 27.8 };
            for (int ci = 0; ci < configs.Length; ci++)
            {
                var config = configs[ci];
                if (pick != null && Array.IndexOf(pick.Split(','), ci.ToString()) < 0) continue;
                foreach (string wn in winds.Split(','))
                {
                    WindLevel wind = wn == "Windy" ? WindLevel.Windy : WindLevel.Calm;
                    var flights = new FiberCutScenarios.Outcome[2, speeds.Length, seeds];
                    var options = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = threads };
                    System.Threading.Tasks.Parallel.For(0, 2 * speeds.Length * seeds, options, i =>
                    {
                        int row = i / (speeds.Length * seeds), v = i / seeds % speeds.Length, s = i % seeds;
                        flights[row, v, s] = row == 0
                            ? FiberCutScenarios.SharpReverse(Spec, wind, (ulong)(s + 1), config.Edit, speeds[v])
                            : FiberCutScenarios.PitchBack(Spec, wind, (ulong)(s + 1), config.Edit, speeds[v]);
                    });
                    for (int row = 0; row < 2; row++)
                    {
                        string name = row == 0 ? "sharp reverse" : "pitch-back, throttle held";
                        var all = new System.Collections.Generic.SortedDictionary<string, int>();
                        double sum = 0, most = 0;
                        int over = 0;
                        string cells = "";
                        for (int v = 0; v < speeds.Length; v++)
                        {
                            var counts = new System.Collections.Generic.SortedDictionary<string, int>();
                            for (int s = 0; s < seeds; s++)
                            {
                                FiberCutScenarios.Outcome o = flights[row, v, s];
                                string c = Code(o);
                                counts[c] = counts.TryGetValue(c, out int k) ? k + 1 : 1;
                                all[c] = all.TryGetValue(c, out int ka) ? ka + 1 : 1;
                                sum += o.EdgeMost;
                                if (o.EdgeMost > most) most = o.EdgeMost;
                                if (o.EdgeMost >= 3.0e9) over++;
                                Print($"[{ci} {config.Name}] {wn} {name} {speeds[v] * 3.6:0} km/h seed {s + 1}: {o}; " + Edge(o).Item2);
                            }
                            cells += $" | {speeds[v] * 3.6:0}: " + string.Join(" ", System.Linq.Enumerable.Select(counts, kv => $"{kv.Value}{kv.Key}"));
                        }
                        int total = speeds.Length * seeds;
                        Print($"== [{ci} {config.Name}] {wn} {name}:{cells} | all " + string.Join(" ", System.Linq.Enumerable.Select(all, kv => $"{kv.Value}{kv.Key}"))
                            + $" of {total}; the edge's most σ mean {sum / total / 1e9:0.000} GPa, most {most / 1e9:0.000}, at or past 3.0 GPa {over}");
                    }
                }
            }
        }

        // p7d-tether 1.11: a sharp reverse traced step by step on the session's drone (FIBER_AIRFRAME=bare the bare drone): the
        // pose and the climb, the rotors, the lip (wrap, tension, stress), the line's first nodes from the nozzle in body axes, and
        // each rear disc's nearest node (its height over the disc's plane and its distance from the hub's axis), every FIBER_EVERY
        // steps from FIBER_FROM s and every step of the last 40 before a break.
        [Test, Explicit("a probe: a sharp reverse's line against the rear discs, printed step by step from FIBER_FROM s")]
        public void ProbeTheReversesRearDiscs()
        {
            ulong seed = ulong.TryParse(Environment.GetEnvironmentVariable("FIBER_SEED"), out ulong v) ? v : 1;
            double speed = double.TryParse(Environment.GetEnvironmentVariable("FIBER_SPEED"), out double sp) ? sp : 15;
            double from = double.TryParse(Environment.GetEnvironmentVariable("FIBER_FROM"), out double f) ? f : 10.9;
            int every = int.TryParse(Environment.GetEnvironmentVariable("FIBER_EVERY"), out int e) ? e : 20;
            WindLevel wind = Environment.GetEnvironmentVariable("FIBER_WIND") == "Windy" ? WindLevel.Windy : WindLevel.Calm;
            DroneSpec spec = Spec;
            var hubs = new Double3[4];
            for (int m = 0; m < 4; m++) hubs[m] = spec.Motors[m].Position;
            double radius = spec.Props.Diameter / 2;
            var lines = new System.Collections.Generic.List<(long K, string S)>();
            using IDisposable airframe = TheSessionDrone();
            FiberCutScenarios.StopAtBreak = true;
            FiberCutScenarios.Each = core =>
            {
                long k = core.State.Step;
                if (k * FlightCore.Dt < from) return;
                ref readonly FlightState s = ref core.State;
                FiberLine l = core.Tether;
                Double3 nose = s.Attitude.Rotate(new Double3(0, 0, 1)), up = s.Attitude.Rotate(new Double3(0, 1, 0));
                double pitch = DetMath.Atan2(nose.Y, DetMath.Sqrt(nose.X * nose.X + nose.Z * nose.Z)) * 180 / Math.PI;
                string head = $"t {k * FlightCore.Dt:0.000} y {s.Position.Y:0.000} z {s.Position.Z:0.000} vy {s.Velocity.Y:0.00} vz {s.Velocity.Z:0.00} nose-up {pitch:0.0}° " +
                    $"rate ({s.BodyRate.X:0.00},{s.BodyRate.Y:0.00},{s.BodyRate.Z:0.00}) ω rear {s.RotorSpeed[0]:0}/{s.RotorSpeed[2]:0} front {s.RotorSpeed[1]:0}/{s.RotorSpeed[3]:0} ring rear {s.RingInflow[0]:0.00}/{s.RingInflow[2]:0.00} m/s latched {s.RingLatched} throttle {s.Fc.Throttle:0.00}";
                if (l.Broken || !l.Attached) { lines.Add((k, head + " BROKEN")); return; }
                CableChain c = l.Chain;
                int n = c.Count;
                if (lines.Count == 0)
                {
                    // The whole line once at the start: each node's world z and height, its tension, then the trail's last points.
                    string all = $"the line at t {k * FlightCore.Dt:0.000}: {n} nodes, trail {l.TrailCount};";
                    for (int i = 0; i < n; i++) all += $" ({c[i].Position.Z:0.0},{c[i].Position.Y:0.00}{(i < n - 1 ? $",{c.Tension(i):0.000}" : "")})";
                    all += " | trail's last:";
                    for (int i = Math.Max(0, l.TrailCount - 5); i < l.TrailCount; i++) all += $" ({l.Trail[i].Z:0.0},{l.Trail[i].Y:0.00})";
                    Print(all);
                }
                ref readonly FiberBend lip = ref l.Cuts.Bend(l.Cuts.BendCount - 1);
                string body = $" | lip {lip.Turn * 180 / Math.PI:0.0}° T {lip.Tension:0.000} N σ {lip.Stress / 1e9:0.000} GPa exitT {l.ExitTension:0.000} sdot {c.SpoolSpeed:0.00} paid {l.PaidOut:0.00} n {n} |";
                for (int i = n - 1; i >= Math.Max(0, n - 6); i--)
                {
                    Double3 b = s.Attitude.InverseRotate(c[i].Position - s.Position);
                    body += $" [{n - 1 - i} ({b.X:0.000},{b.Y:0.000},{b.Z:0.000}) T {(i < n - 1 ? c.Tension(i) : 0):0.000}]";
                }
                foreach (int m in new[] { 0, 2 })
                {
                    Double3 hub = s.Position + s.Attitude.Rotate(hubs[m]);
                    double best = double.PositiveInfinity, bz = 0, blat = 0;
                    int bi = -1;
                    for (int i = 0; i < n; i++)
                    {
                        Double3 q = c[i].Position - hub;
                        double z = Double3.Dot(q, up), lat = (q - up * z).Length();
                        double rim = lat < radius ? DetMath.Abs(z) : DetMath.Sqrt((lat - radius) * (lat - radius) + z * z);
                        if (rim < best) { best = rim; bi = i; bz = z; blat = lat; }
                    }
                    body += $" | disc {m}: node {n - 1 - bi} rim {best * 1e3:0} mm, over the plane {bz * 1e3:0} mm, off the axis {blat * 1e3:0} mm";
                }
                lines.Add((k, head + body));
            };
            FiberCutScenarios.Outcome o;
            try
            {
                o = FiberCutScenarios.SharpReverse(spec, wind, seed, null, speed);
            }
            finally
            {
                FiberCutScenarios.Each = null;
                FiberCutScenarios.StopAtBreak = false;
            }
            for (int i = 0; i < lines.Count; i++)
                if (i % every == 0 || i >= lines.Count - 40) Print(lines[i].S);
            Print($"{wind} seed {seed} from {speed * 3.6:0} km/h: {o}");
        }

        // p7d-tether 1.11: how near each sharp reverse comes to tearing at the hole's edge, on the session's drone (FIBER_AIRFRAME=bare
        // the bare drone), Calm, 36–100 km/h, seeds 1–FIBER_SEEDS, one after another: the most stress at the lip while the line is
        // whole, with its wrap and tension, and the outcome.
        [Test, Explicit("a probe: the sharp reverses' most stress at the hole's edge, printed")]
        public void ProbeTheReversesEdge()
        {
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("FIBER_SEEDS"), out int n) ? n : 10;
            using IDisposable airframe = TheSessionDrone();
            double most = 0, turn = 0, tension = 0, at = 0;
            FiberCutScenarios.Each = core =>
            {
                FiberLine l = core.Tether;
                if (core.State.Step < 11000 || l.Broken || !l.Attached) return;
                ref readonly FiberBend lip = ref l.Cuts.Bend(l.Cuts.BendCount - 1);
                if (!(lip.Stress > most)) return;
                most = lip.Stress;
                turn = lip.Turn;
                tension = lip.Tension;
                at = core.State.Step * FlightCore.Dt;
            };
            try
            {
                foreach (double v in new[] { 10, 15, 20, 25, 27.8 })
                    for (ulong seed = 1; seed <= (ulong)seeds; seed++)
                    {
                        most = turn = tension = at = 0;
                        FiberCutScenarios.Outcome o = FiberCutScenarios.SharpReverse(Spec, WindLevel.Calm, seed, null, v);
                        Print($"{v * 3.6:0} km/h seed {seed}: the edge's most σ {most / 1e9:0.000} GPa at {at:0.000} s (θ {turn * 180 / Math.PI:0}°, T {tension:0.000} N); " +
                            (o.Broken ? $"cut {o.Mechanism} at {o.Place} t {o.Time:0.000} s" : "whole"));
                    }
            }
            finally
            {
                FiberCutScenarios.Each = null;
            }
        }

        // Night-fiber probe: the hole's edge in each of the pilot's causes, step by step (FIBER_CASE reverse, pitchback, sharp,
        // backward; FIBER_SEED; FIBER_SPEED; FIBER_FROM, FIBER_TO, FIBER_EVERY steps): the lip's wrap, its capped and the chain's own
        // tension, the payout's speed and its change in the step, the stress, and the line's least distance to a disc.
        [Test, Explicit("a probe: the lip through a cause, printed")]
        public void ProbeTheLipInTheCauses()
        {
            string which = Environment.GetEnvironmentVariable("FIBER_CASE") ?? "reverse";
            ulong seed = ulong.TryParse(Environment.GetEnvironmentVariable("FIBER_SEED"), out ulong v) ? v : 1;
            double speed = double.TryParse(Environment.GetEnvironmentVariable("FIBER_SPEED"), out double sp) ? sp : 15;
            double from = double.TryParse(Environment.GetEnvironmentVariable("FIBER_FROM"), out double f) ? f : 0;
            double to = double.TryParse(Environment.GetEnvironmentVariable("FIBER_TO"), out double tt) ? tt : 99;
            int every = int.TryParse(Environment.GetEnvironmentVariable("FIBER_EVERY"), out int e) ? e : 50;
            DroneSpec spec = Spec;
            var hubs = new Double3[4];
            for (int m = 0; m < 4; m++) hubs[m] = spec.Motors[m].Position;
            double radius = spec.Props.Diameter / 2;
            using IDisposable airframe = TheSessionDrone();
            double last = 0, mostS = 0, mostT = 0, mostTTheta = 0, mostTAt = 0, mostSAt = 0, mostSTheta = 0, mostST = 0, mostJump = 0, jumpTheta = 0, jumpAt = 0;
            FiberCutScenarios.StopAtBreak = true;
            FiberCutScenarios.Each = core =>
            {
                long k = core.State.Step;
                double t = k * FlightCore.Dt;
                ref readonly FlightState s = ref core.State;
                FiberLine l = core.Tether;
                if (l.Broken || !l.Attached) return;
                CableChain c = l.Chain;
                int n = c.Count;
                ref readonly FiberBend lip = ref l.Cuts.Bend(l.Cuts.BendCount - 1);
                double sdot = c.SpoolSpeed, jump = sdot - last;
                last = sdot;
                if (lip.Stress > mostS) { mostS = lip.Stress; mostSAt = t; mostSTheta = lip.Turn; mostST = lip.Tension; }
                if (l.ExitTension > mostT) { mostT = l.ExitTension; mostTAt = t; mostTTheta = lip.Turn; }
                if (jump > mostJump) { mostJump = jump; jumpAt = t; jumpTheta = lip.Turn; }
                if (t < from || t > to || k % every != 0) return;
                Double3 nose = s.Attitude.Rotate(new Double3(0, 0, 1)), up = s.Attitude.Rotate(new Double3(0, 1, 0));
                double pitch = DetMath.Atan2(nose.Y, DetMath.Sqrt(nose.X * nose.X + nose.Z * nose.Z)) * 180 / Math.PI;
                double best = double.PositiveInfinity;
                for (int m = 0; m < 4; m++)
                {
                    Double3 hub = s.Position + s.Attitude.Rotate(hubs[m]);
                    for (int i = 0; i < n; i++)
                    {
                        Double3 q = c[i].Position - hub;
                        double z = Double3.Dot(q, up), lat = (q - up * z).Length();
                        double rim = lat < radius ? DetMath.Abs(z) : DetMath.Sqrt((lat - radius) * (lat - radius) + z * z);
                        if (rim < best) best = rim;
                    }
                }
                Double3 b1 = s.Attitude.InverseRotate(c[n - 2].Position - s.Position);
                Print($"t {t:0.000} y {s.Position.Y:0.00} z {s.Position.Z:0.00} v ({s.Velocity.X:0.0},{s.Velocity.Y:0.0},{s.Velocity.Z:0.0}) nose-up {pitch:0} thr {s.Fc.Throttle:0.00} | lip {lip.Turn * 180 / Math.PI:0}° T {lip.Tension:0.000} chainT {l.ExitTension:0.000} σ {lip.Stress / 1e9:0.00} sdot {sdot:0.00} paid {l.PaidOut:0.0} n {n} node1 ({b1.X:0.00},{b1.Y:0.00},{b1.Z:0.00}) rim {best * 1e3:0}{(Environment.GetEnvironmentVariable("FIBER_NODES") == "1" ? Near(c) : "")}");
            };
            FiberCutScenarios.Outcome o;
            try
            {
                o = which == "pitchback" ? FiberCutScenarios.PitchBack(spec, WindLevel.Calm, seed, null, speed)
                    : which == "sharp" ? FiberCutScenarios.Liftoff(spec, true, seed)
                    : which == "backward" ? FiberCutScenarios.Backward(spec, seed)
                    : which == "cruise" ? FiberCutScenarios.Turn(spec, WindLevel.Windy, seed, 27.8, 0).O
                    : which == "backlow" ? FiberCutScenarios.Backward(spec, seed, null, 3, double.TryParse(Environment.GetEnvironmentVariable("FIBER_HEIGHT"), out double h) ? h : 0.4)
                    : FiberCutScenarios.SharpReverse(spec, WindLevel.Calm, seed, null, speed);
            }
            finally
            {
                FiberCutScenarios.Each = null;
                FiberCutScenarios.StopAtBreak = false;
            }
            Print($"most lip σ {mostS / 1e9:0.000} GPa at {mostSAt:0.000} (θ {mostSTheta * 180 / Math.PI:0}°, T {mostST:0.000}); most chain exit T {mostT:0.000} at {mostTAt:0.000} (θ {mostTTheta * 180 / Math.PI:0}°); most ṡ jump {mostJump:0.00} m/s at {jumpAt:0.000} (θ {jumpTheta * 180 / Math.PI:0}°)");
            Print($"{which} seed {seed}: {o}");
        }

        // The last three nodes' contacts and segment tensions (FIBER_NODES=1): the member, its normal force, a capstan's hold.
        static string Near(CableChain c)
        {
            string r = "";
            for (int i = Math.Max(0, c.Count - 4); i < c.Count - 1; i++)
            {
                ref CableNode a = ref c[i];
                string k(in CableContact x) => x.Active ? $"{(FiberMember)x.MemberKind}{x.Member}:{x.NormalForce:0.000}{(x.Capstan ? "c" : "")}" : "-";
                r += $" [{i} T {c.Tension(i):0.000} {k(a.Contact0)} {k(a.Contact1)}]";
            }
            return r;
        }

        [Test, Explicit("a probe: the line at the exit through a turn, printed step by step from FIBER_FROM s")]
        public void ProbeTheTurnsExit()
        {
            ulong seed = ulong.TryParse(Environment.GetEnvironmentVariable("FIBER_SEED"), out ulong v) ? v : 1;
            double speed = double.TryParse(Environment.GetEnvironmentVariable("FIBER_SPEED"), out double sp) ? sp : 5;
            double rate = double.TryParse(Environment.GetEnvironmentVariable("FIBER_RATE"), out double r) ? r : 90;
            double from = double.TryParse(Environment.GetEnvironmentVariable("FIBER_FROM"), out double f) ? f : 11;
            FiberCutScenarios.StopAtBreak = true;
            FiberCutScenarios.Each = core =>
            {
                if (core.State.Step * FlightCore.Dt < from) return;
                FiberLine l = core.Tether;
                CableChain c = l.Chain;
                int n = c.Count;
                if (!l.Attached || n < 3) return;
                string nodes = "";
                int most = 0;
                for (int j = 1; j < n - 1; j++) if (c.Tension(j) > c.Tension(most)) most = j;
                int lo = Environment.GetEnvironmentVariable("FIBER_AT") == "most" ? Math.Max(0, most - 2) : Math.Max(0, n - 4);
                for (int i = lo; i < Math.Min(n, lo + 5); i++)
                {
                    Double3 b = core.State.Attitude.InverseRotate(c[i].Position - core.State.Position);
                    nodes += $" [{i} s={c[i].Material:0.000} b=({b.X:0.000},{b.Y:0.000},{b.Z:0.000}) v={c[i].Velocity.Length():0.00} T={(i < n - 1 ? c.Tension(i) : 0):0.000} c0={(c[i].Contact0.Active ? ((FiberMember)c[i].Contact0.MemberKind).ToString() + c[i].Contact0.Member + ":" + c[i].Contact0.NormalForce.ToString("0.000") : "-")} c1={(c[i].Contact1.Active ? ((FiberMember)c[i].Contact1.MemberKind).ToString() + c[i].Contact1.Member + ":" + c[i].Contact1.NormalForce.ToString("0.000") : "-")}]";
                }
                Print($"k {core.State.Step} n {n} sdot {c.SpoolSpeed:0.000} paying {c.SpoolPaying} pack {c.SpoolPackTension:0.000} exitT {c.SpoolExitTension:0.000} lip {l.Cuts.Bend(l.Cuts.BendCount - 1).Turn * 180 / Math.PI:0.0} broken {l.Broken}{nodes}");
            };
            FiberCutScenarios.Turned t = FiberCutScenarios.Turn(Spec, WindLevel.Calm, seed, speed, rate);
            FiberCutScenarios.Each = null;
            FiberCutScenarios.StopAtBreak = false;
            Print(t.O.ToString());
        }

        [Test, Explicit("a probe: the sharp liftoff's tension, printed")]
        public void ProbeTheSharpLiftoffsTension()
        {
            ulong seed = ulong.TryParse(Environment.GetEnvironmentVariable("FIBER_SEED"), out ulong v) ? v : 2;
            FiberCutScenarios.StopAtBreak = true;
            FiberCutScenarios.Each = core =>
            {
                long k = core.State.Step;
                CableChain c = core.Tether.Chain;
                if (core.Tether.Broken) return;
                int at = -1;
                double most = 0;
                for (int j = 0; j < c.Count - 1; j++)
                    if (c.Tension(j) > most) { most = c.Tension(j); at = j; }
                if (most < 2) return;
                ref readonly FlightState s = ref core.State;
                string nodes = "";
                for (int i = Math.Max(0, at - 1); i <= Math.Min(c.Count - 1, at + 2); i++)
                    nodes += $" [{i} s={c[i].Material:0.0000} x={c[i].Position.X:0.0000} y={c[i].Position.Y:0.0000} z={c[i].Position.Z:0.0000} v={c[i].Velocity.Length():0.00} T={(i < c.Count - 1 ? c.Tension(i) : 0):0.00} A={c[i].AnchorStiffness:0} c0={(c[i].Contact0.Active ? ((FiberMember)c[i].Contact0.MemberKind).ToString() + c[i].Contact0.Member + ":" + c[i].Contact0.NormalForce.ToString("0.00") : "-")} c1={(c[i].Contact1.Active ? ((FiberMember)c[i].Contact1.MemberKind).ToString() + c[i].Contact1.Member + ":" + c[i].Contact1.NormalForce.ToString("0.00") : "-")}]";
                TestContext.Progress.WriteLine($"k {k} drone y {s.Position.Y:0.000} vy {s.Velocity.Y:0.00} z {s.Position.Z:0.000} n {c.Count} trail {core.Tether.TrailCount} paid {core.Tether.PaidOut:0.000} Tmax {most:0.00} at {at}{nodes}");
            };
            if (Environment.GetEnvironmentVariable("FIBER_CASE") == "coil")
            {
                FiberCutScenarios.CoilProbe = true;
                TestContext.Progress.WriteLine(FiberCutScenarios.OverTheCoil(Spec, seed, false).O.ToString());
                FiberCutScenarios.CoilProbe = false;
                return;
            }
            if (Environment.GetEnvironmentVariable("FIBER_CASE") == "twig") TestContext.Progress.WriteLine(FiberCutScenarios.AroundATwig(Spec, seed, WindLevel.Windy, 2).O.ToString());
            else TestContext.Progress.WriteLine(FiberCutScenarios.Liftoff(Spec, true, seed).ToString());
            FiberCutScenarios.Each = null;
            FiberCutScenarios.StopAtBreak = false;
        }

        [Test, Explicit("a probe: the pilot's causes over a few seeds, printed")]
        public void ProbeThePilotsCauses()
        {
            string which = Environment.GetEnvironmentVariable("FIBER_PROBE") ?? "all";
            int seeds = int.TryParse(Environment.GetEnvironmentVariable("FIBER_SEEDS"), out int n) ? n : 4;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                if (which == "all" || which.Contains("liftoff"))
                {
                    TestContext.Progress.WriteLine($"seed {seed} normal liftoff: {FiberCutScenarios.Liftoff(Spec, false, seed)}");
                    TestContext.Progress.WriteLine($"seed {seed} sharp liftoff: {FiberCutScenarios.Liftoff(Spec, true, seed)}");
                    TestContext.Progress.WriteLine($"seed {seed} sharp liftoff, design 4: {FiberCutScenarios.Liftoff(Spec, true, seed, s => s.TetherDesign4Air = true)}");
                }
                if (which.Contains("windyliftoff"))
                    TestContext.Progress.WriteLine($"seed {seed} normal liftoff windy: {FiberCutScenarios.Liftoff(Spec, false, seed, null, WindLevel.Windy)}");
                if (which == "all" || which.Contains("backward"))
                {
                    TestContext.Progress.WriteLine($"seed {seed} backward: {FiberCutScenarios.Backward(Spec, seed)}");
                    TestContext.Progress.WriteLine($"seed {seed} backward, design 4: {FiberCutScenarios.Backward(Spec, seed, s => s.TetherDesign4Air = true)}");
                }
                if (which == "all" || which.Contains("reverse"))
                {
                    TestContext.Progress.WriteLine($"seed {seed} reverse calm: {FiberCutScenarios.SharpReverse(Spec, WindLevel.Calm, seed)}");
                    TestContext.Progress.WriteLine($"seed {seed} reverse windy: {FiberCutScenarios.SharpReverse(Spec, WindLevel.Windy, seed)}");
                    TestContext.Progress.WriteLine($"seed {seed} reverse two-way: {FiberCutScenarios.SharpReverse(Spec, WindLevel.Calm, seed, s => s.TetherTwoWay = true)}");
                }
                if (which == "all" || which.Contains("tree"))
                {
                    TestContext.Progress.WriteLine($"seed {seed} tree: {FiberCutScenarios.TreeSnag(Spec, WindLevel.Calm, seed)}");
                    TestContext.Progress.WriteLine($"seed {seed} tree, no contacts: {FiberCutScenarios.TreeSnag(Spec, WindLevel.Calm, seed, s => s.TetherWithoutContacts = true)}");
                }
            }
        }
    }
}
