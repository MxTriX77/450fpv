using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // fiber-tether (p7d-tether 1.6): the spool, the span and the trail, each scenario of the task flown by TetherScenarios on
    // the stub's floor; the figures print for the as-built note.
    public sealed class TetherTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        static void Print(string line) => TestContext.Progress.WriteLine(line);

        // "Line pays out as the drone flies away": monotonic, never shorter than the straight line from the anchor, and at steady
        // speed the logged exit tension is the pack's law over the lip (the moving belt's capstan, CableSpool) within 2 %.
        [TestCase(5.0)]
        [TestCase(10.0)]
        [TestCase(20.0)]
        public void LinePaysOutAsTheDroneFliesAway(double speed)
        {
            TetherScenarios.PayOut r = TetherScenarios.FlyAway(Spec, speed);
            Print($"{speed} m/s: paid {r.Paid:F2} m, fell {r.Fell} steps, least margin over the anchor's distance {r.Margin:F4} m; last 5 s: "
                + $"payout {r.Speed:F3} m/s at {r.Ground:F3} m/s over the ground, exit tension {r.Exit:F5} N against the law's {r.Law:F5} N at that speed "
                + $"({(r.Exit / r.Law - 1) * 100:F2} %); G₀ {r.PeelEnergy:F4} J/m; {r.Clips} clips, {r.SafeSteps} safe steps, {r.Wedges} wedges");
            Assert.That(r.Fell, Is.EqualTo(0), "the paid-out length never falls");
            Assert.That(r.Margin, Is.GreaterThan(-1e-3), "the line from the anchor is never shorter than the straight line to the exit (its stretch aside)");
            Assert.That(r.Paid, Is.GreaterThan(speed * 18), "it paid out the flight");
            Assert.That(r.Exit / r.Law, Is.EqualTo(1).Within(0.02), "the exit tension is the law at steady speed");
            Assert.That(r.Wedges, Is.EqualTo(0));
        }

        // "Line never comes back": out 100 m and straight back, then a hover; the paid-out length never falls, the exit's tension
        // drops to the slack span's own load, and the slack shows in the span's line over its chord and, once it rests, in the trail.
        [Test]
        public void LineNeverComesBack()
        {
            TetherScenarios.OutBack r = TetherScenarios.OutThenBack(Spec, false);
            Print($"one-way: most {r.Most:F2} m, at the turn {r.AtTurn:F2}, least after it {r.LeastAfterTurn:F2}, at the end {r.AtEnd:F2}, fell {r.Fell} steps; "
                + $"hold {r.Hold:F4} N, exit tension 0.5–2.5 s after the turn {r.SlackLeast:F5}–{r.SlackTension:F5} N; line over chord 3 s after {r.Slack:F3}; "
                + $"trail {r.TrailAtTurn} → {r.TrailAtEnd}");
            Assert.That(r.Fell, Is.EqualTo(0), "never falls");
            Assert.That(r.LeastAfterTurn, Is.GreaterThanOrEqualTo(r.AtTurn), "never below the turn's");
            Assert.That(r.SlackLeast, Is.LessThan(0.1 * r.Hold), "the exit falls to the slack span's own load");
            Assert.That(r.Slack, Is.GreaterThan(1.05), "the slack lies in the span");
            Assert.That(r.TrailAtEnd, Is.GreaterThan(r.TrailAtTurn), "and in the trail");
            Assert.That(r.Wedges, Is.EqualTo(0));
        }

        // "A known-wrong two-way spool": the same flight with a spool that takes line back at the static hold; its paid-out length
        // falls on the return, so "Line never comes back" fails for it (the sharp reverse's "may", 1.11, fails with it). Since
        // model 60 the fall is from its most to its end: the line pays out past the turn's length as the drone slows (to 107.9 m
        // on model 59, 109.5 on 60, from 95.9), and the spool takes it back to 91.2 m on 59, to 100.0 on 60, where the slack's
        // first points have frozen on the ground (the frame's stance at the takeoff moves where it lands).
        [Test]
        public void AKnownWrongTwoWaySpoolFails()
        {
            TetherScenarios.OutBack r = TetherScenarios.OutThenBack(Spec, true);
            Print($"two-way: most {r.Most:F2} m, at the turn {r.AtTurn:F2}, least after it {r.LeastAfterTurn:F2}, at the end {r.AtEnd:F2}, fell {r.Fell} steps, {r.TakenBack:F2} m taken back; "
                + $"exit tension 0.5–2.5 s after the turn {r.SlackLeast:F5}–{r.SlackTension:F5} N against a hold of {r.Hold:F4}; line over chord {r.Slack:F3}");
            Assert.That(r.Fell, Is.GreaterThan(0), "the line comes back");
            // From its most (after the turn: since model 62's safe pass leaves free slots free, the slack first draws line out
            // past the turn's length, 112.9 m against the turn's 95.9, then the reel takes 12.1 m of it back). Since model 65 (62's
            // chain on the canister) by what the reel takes back in all: it takes 5.2 m back over 18 245 steps, but the hover's
            // slack draws it out again (106.56 m at most, 106.40 at the end; with the 0-force hold, 64's chain, 17.1 m taken back,
            // 107.34 → 94.00), where the one-way spool takes back nothing (LineNeverComesBack: it never falls).
            Assert.That(r.TakenBack, Is.GreaterThan(1), "the reel takes line back on the return");
            bool target = r.Fell == 0 && r.LeastAfterTurn >= r.AtTurn;
            Assert.That(target, Is.False, "the known-wrong model fails \"Line never comes back\"");
        }

        // "A falling line settles at its terminal speed": 10 m released 50 m up in still air falls at 1.4 m/s within 5 % (White's
        // fit at Re 24) and lands after 35 s within 5 %.
        [Test]
        public void AFallingLineSettlesAtItsTerminalSpeed()
        {
            TetherScenarios.Fall r = TetherScenarios.Falling();
            Print($"falls at {r.Speed:F4} m/s (the law's terminal {r.Terminal:F4}), lands at {r.Landed:F2} s");
            Assert.That(r.Speed, Is.EqualTo(1.4).Within(5).Percent);
            Assert.That(r.Speed, Is.EqualTo(r.Terminal).Within(0.5).Percent, "the chain's drag is the law's");
            Assert.That(r.Landed, Is.EqualTo(35).Within(5).Percent);
        }

        // "A pulled trail lifts": a trail held by a snag 50 m behind lifts in order as the drone climbs and flies on, the chain
        // within its 32 nodes, and line pays out only once the span from the exit to the snag is pulled taut at the payout tension
        // (since 1.6b the pull's payout: the rear pair's fountain under the drone sitting on the floor may draw a little first,
        // its drag on the piece rising to the exit at the hold, printed).
        [Test]
        public void APulledTrailLifts()
        {
            TetherScenarios.PulledTrail r = TetherScenarios.PullTrail(Spec);
            Print($"frozen {r.Frozen} points, down to {r.Fewest}, rose while lifting {r.RoseWhileLifting}; most nodes {r.MostNodes}; first payout with "
                + $"{r.TrailAtPayout} trail points, exit tension {r.TensionOverHold:F3} of the hold, line over chord from the snag {r.LineOverChord:F4}; paid {r.Paid:F2} m, "
                + $"{r.PaidBeforePull * 1000:F3} mm of it before the pull");
            Assert.That(r.Frozen, Is.GreaterThan(3), "the laid line froze into the trail");
            Assert.That(r.Fewest, Is.EqualTo(1), "every point lifted back to the snag");
            Assert.That(r.RoseWhileLifting, Is.False, "last frozen first, in order");
            Assert.That(r.MostNodes, Is.LessThanOrEqualTo(32));
            // Since model 64 the canister's nozzle points aft and down, along the pull to the trail, so the lip's capstan is smaller
            // and the payout begins as the last point before the snag lifts (2 points left; on the old coil's level exit, 1). Since model
            // 70 judged past 1 mm (TetherScenarios.PayoutThreshold): the heated motors' climb draws a fraction of a millimetre at the
            // hold with 10 points still frozen (traced, heat on and off: off, the pull's payout at 1 point; on, at 2).
            Assert.That(r.TrailAtPayout, Is.LessThanOrEqualTo(2), "line pays out only once the trail has lifted to the snag, or the last point before it");
            Assert.That(r.TensionOverHold, Is.GreaterThanOrEqualTo(0.99), "and the exit is pulled to the payout tension");
            Assert.That(r.Paid, Is.GreaterThan(1));
            Assert.That(r.PaidBeforePull, Is.LessThan(1e-3), "the rotors' air at the sit draws less than a millimetre");
            Assert.That(r.Wedges, Is.EqualTo(0));
        }

        // "Rain makes it heavier and draggier": the line's mass per metre and diameter follow the table's films, and it settles
        // faster and trails lower in rain than dry.
        [Test]
        public void RainMakesItHeavierAndDraggier()
        {
            TetherTable t = TetherScenarios.Table();
            double d = t.Number("fiber.coating_diameter_m"), mu = t.Number("fiber.mass_per_m_kg"), rho = t.Number("fiber.water_density_kg_m3");
            TetherScenarios.Wet dry = TetherScenarios.Cruise(Spec, RainLevel.Sunny);
            TetherScenarios.Wet rain = TetherScenarios.Cruise(Spec, RainLevel.Rain);
            TetherScenarios.Wet severe = TetherScenarios.Cruise(Spec, RainLevel.SevereRain);
            foreach ((string name, TetherScenarios.Wet w, double film) in new[]
            {
                ("dry", dry, 0.0), ("rain", rain, t.Number("fiber.wet_film_rain_m")), ("severe", severe, t.Number("fiber.wet_film_severe_m")),
            })
            {
                double dw = d + 2 * film, mw = mu + rho * Math.PI * (dw * dw - d * d) / 4;
                Print($"{name}: {w.MassPerMetre * 1e6:F2} g/km ({mw * 1e6:F2}), Ø {w.Diameter * 1e3:F3} mm ({dw * 1e3:F3}), the airborne line's mean height "
                    + $"{w.MeanHeight:F3} m, its nodes sinking at {w.Sink:F3} m/s, the law's terminal speed {w.Terminal:F3} m/s; {w.Wedges} wedges");
                Assert.That(w.MassPerMetre, Is.EqualTo(mw).Within(1e-12), name);
                Assert.That(w.Diameter, Is.EqualTo(dw).Within(1e-15), name);
                Assert.That(w.Wedges, Is.EqualTo(0), name);
            }
            Assert.That(rain.MeanHeight, Is.LessThan(dry.MeanHeight), "rain trails lower");
            Assert.That(severe.MeanHeight, Is.LessThan(rain.MeanHeight), "severe rain lower still");
            Assert.That(rain.Terminal, Is.GreaterThan(dry.Terminal), "the wet line settles faster");
            Assert.That(severe.Terminal, Is.GreaterThan(rain.Terminal));
            Assert.That(rain.Sink, Is.GreaterThan(dry.Sink), "and its airborne nodes sink faster");
        }

        // "A line under a punch is lifted and drawn": legs-on with the line laid under the rear, full throttle against hover thrust;
        // the line beside the skids is lifted and moves faster at full throttle, each node's air is the field's law at its position
        // and its drag the law's on that air within 1 %; a node crossing a turning disc is counted (1.8 cuts the line there).
        // Since 1.6b (D-075) the line reaches a disc by the modelled flow: the rear pair's wall jets meet under the line and rise
        // as a fountain, which carries it up between the rear discs to their inboard rims, faster at full throttle than at hover;
        // design 4's field without it (FlightSetup.TetherDesign4Air) is the known-wrong, the line held 0.24–0.29 m off the rims.
        // Since model 48 (the fountain on p7g-legs' PVC-U legs) the approach is judged over the flight's draws: the line lies on the
        // rear pair's stagnation line, where their sinks balance, so how fast it is drawn to a rim turns on what tips it off that
        // line (the spool's peel and irregularity draws), and on one seed the two can come either way (0x7D16: 2.44 m/s at full
        // against 2.63 at hover, the discs climbing away at 1.4 m/s counted; on model 26's legs 2.40 against 2.20). Over seeds 1–30
        // full throttle draws it faster on the mean and on most seeds; design 4's field never does, so it fails. Since model 64 the
        // line is laid under the body's rear explicitly (the launch lays it along the nozzle, 0.3 m behind), and the canister over
        // it keeps it off the rims (below).
        [Test]
        public void ALineUnderAPunchIsLiftedAndDrawn()
        {
            TetherScenarios.Punch full = TetherScenarios.Liftoff(Spec, true);
            TetherScenarios.Punch hover = TetherScenarios.Liftoff(Spec, false);
            TetherScenarios.Punch wrongFull = TetherScenarios.Liftoff(Spec, true, design4: true);
            TetherScenarios.Punch wrongHover = TetherScenarios.Liftoff(Spec, false, design4: true);
            foreach ((string name, TetherScenarios.Punch p) in new[] { ("full", full), ("hover", hover), ("design 4 full", wrongFull), ("design 4 hover", wrongHover) })
                Print($"{name}: the line lying beside the skids rose {p.Lift * 1000:F2} mm off the floor, the wall jet's lift up to {p.LiftOverWeight:F2} times "
                    + $"its weight, moved at up to {p.Speed:F3} m/s, came within {p.Rim * 1000:F1} mm of a rim at up to {p.PeakApproach:F3} m/s; "
                    + $"the whole line within {p.LineRim * 1000:F1} mm of a rim; a segment crossed a turning disc {p.Crossed} times; "
                    + $"air within {p.AirError:E2}, drag within {p.DragError:E2} of the law over {p.Checked} node-steps");
            Assert.That(full.LiftOverWeight, Is.GreaterThan(1), "the wall jet lifts the line lying in it off the floor");
            Assert.That(full.Lift, Is.GreaterThan(hover.Lift), "it rises higher");
            Assert.That(full.Speed, Is.GreaterThan(hover.Speed), "and moves faster");
            Approach(false, out double fullMean, out double hoverMean, out int faster);
            Approach(true, out double wrongFullMean, out double wrongHoverMean, out int wrongFaster);
            Print($"over seeds 1–30 the fastest approach to a rim at full throttle {fullMean:F3} m/s on the mean, at hover {hoverMean:F3}, full faster on {faster} of 30; "
                + $"design 4's field {wrongFullMean:F3} against {wrongHoverMean:F3}, full faster on {wrongFaster}");
            Assert.That(fullMean, Is.GreaterThan(hoverMean), "toward the nearest disc's edge faster than at hover, over the flight's draws");
            Assert.That(faster, Is.GreaterThan(15), "on most seeds");
            // Since model 64 (D01's canister over the line laid under the body's rear, 61's body over the fountain): the fountain
            // under the canister stagnates, so it lifts the line beside the skids (10.4 × its weight at full throttle, 3.0 at hover)
            // but no longer draws it nearer a rim than it lay (290.6 mm at full, 240.8 at hover; on the old coil 0.24–0.29 m nearer
            // than design 4's field). Design 4's field, the known-wrong, now draws it at full throttle too (the climbing exit pulls
            // the piece rising to it), but at hover it lifts nothing (−0.01 mm), where the fountain lifts it 167 mm.
            // Since model 74 (the line leaving 27.5° down from a tip 14 mm lower, D-086) the line rises to the exit more steeply and
            // the piece beside the skids lies in the canister's lee at hover: the fountain lifts it nothing there either (0.00 mm),
            // and at full throttle 431 mm against design 4's 223 mm, drawing it to 196 mm of a rim at 2.26 m/s where design 4's
            // field leaves it at 297 mm unmoved. So the known-wrong is judged at full throttle, the rims at 0.15 m, and the approach
            // at full; and the fountain's law (ExpectedAir writes it without the body's blocking, model 61) is checked on the punch
            // with the fountain through the body, where the blocked flight's nodes in the lee differ from it by design.
            Assert.That(wrongFull.Lift, Is.LessThan(full.Lift - 0.05), "design 4's field lifts the line far less at full throttle: the known-wrong fails");
            foreach ((TetherScenarios.Punch p, TetherScenarios.Punch wrong) in new[] { (full, wrongFull), (hover, wrongHover) })
            {
                Assert.That(wrong.Rim, Is.GreaterThan(0.2), "design 4's field alone holds the line beside the skids off the rims: the known-wrong");
                Assert.That(p.Rim, Is.GreaterThan(0.15), "the canister over it: the line beside the skids is not drawn into a disc");
                Assert.That(p.Crossed, Is.EqualTo(0), "no segment crosses a turning disc");
                Assert.That(p.LineRim, Is.LessThan(wrong.LineRim), "and carries the line rising to the exit nearer the rear discs' inboard rims");
            }
            Assert.That(full.PeakApproach, Is.GreaterThan(wrongFull.PeakApproach), "faster than design 4's field at full throttle");
            TetherScenarios.Punch lawFull = TetherScenarios.Liftoff(Spec, true, throughBody: true), lawHover = TetherScenarios.Liftoff(Spec, false, throughBody: true);
            Print($"the fountain through the body: air within {lawFull.AirError:E2} at full, {lawHover.AirError:E2} at hover; with the body's blocking "
                + $"{full.AirError:E2} and {hover.AirError:E2} (its lee)");
            foreach (TetherScenarios.Punch p in new[] { lawFull, lawHover, wrongFull, wrongHover })
                Assert.That(p.AirError, Is.LessThan(0.01), "each node's air is the field's law at it");
            foreach (TetherScenarios.Punch p in new[] { full, hover, wrongFull, wrongHover, lawFull, lawHover })
                Assert.That(p.DragError, Is.LessThan(0.01), "its drag the law's");
        }

        // The punch's fastest approach to a rim over seeds 1–30 (the spool's draws), at full throttle and at hover thrust.
        static void Approach(bool design4, out double fullMean, out double hoverMean, out int faster)
        {
            fullMean = hoverMean = 0;
            faster = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                double f = TetherScenarios.Liftoff(Spec, true, design4, seed).PeakApproach, h = TetherScenarios.Liftoff(Spec, false, design4, seed).PeakApproach;
                fullMean += f / 30;
                hoverMean += h / 30;
                if (f > h) faster++;
            }
        }

        // "The exit's pull and its moment": the force on the drone is the logged exit tension (less the leaving line's momentum
        // flux) along the first segment and its moment that force at the exit, within 1e-9 relative; once the coil runs out the
        // line holds the drone: the exit's tension rises and the drone is pulled back.
        [Test]
        public void TheExitsPullAndItsMoment()
        {
            TetherScenarios.Pull r = TetherScenarios.ExitPull(Spec);
            Print($"over {r.Steps} steps: force within {r.ForceError:E2}, moment within {r.MomentError:E2} (the parts' reactions up to {r.PartReaction:F4} N); hovering taut, the exit at most {r.HoverTension:F4} N; "
                + $"the coil empty at {r.EmptyAt:F2} s, then the exit up to {r.HeldTension:F3} N, the drone {r.OnPast:F2} m on past it ({r.FreeOnPast:F2} m with line left), "
                + $"its nose up to {r.Pitch:F3} (sin) toward the hold, the pull's moment about body x {r.PitchMoment:F4} N·m at the most tension");
            Assert.That(r.ForceError, Is.LessThan(1e-9));
            Assert.That(r.MomentError, Is.LessThan(1e-9));
            Assert.That(r.Steps, Is.GreaterThan(10000));
            Assert.That(r.EmptyAt, Is.Not.NaN, "the coil ran out");
            Assert.That(r.HeldTension, Is.GreaterThan(10 * r.HoverTension), "held, the exit's tension rises");
            Assert.That(r.OnPast, Is.LessThan(0.5 * r.FreeOnPast), "and the drone is pulled back");
        }

        // "The drone gets lighter": 2 km paid out: the mass falls by 0.138 kg within 1 g, the centre of mass is the mass build's at
        // that coil within 1e-9 m, and no rebuild changes the angular momentum of the drone and the line that left.
        [Test]
        public void TheDroneGetsLighter()
        {
            TetherScenarios.Lighter r = TetherScenarios.TwoKilometres(Spec);
            Print($"paid {r.Paid:F2} m in {r.Rebuilds} rebuilds: the mass fell {r.MassFall * 1000:F4} g, the CoM {r.ComError:E2} m from the build's, "
                + $"the angular momentum within {r.MomentumError:E2}; {r.Wedges} wedges");
            Assert.That(r.Paid, Is.GreaterThanOrEqualTo(2000));
            Assert.That(r.Rebuilds, Is.EqualTo(200));
            Assert.That(r.MassFall, Is.EqualTo(0.138).Within(0.001));
            Assert.That(r.ComError, Is.LessThan(1e-9));
            Assert.That(r.MomentumError, Is.LessThan(1e-12));
            Assert.That(r.Wedges, Is.EqualTo(0));
        }

        // "The drone gets lighter", AirframeModes' coil: the modes re-solved for a lighter coil without allocating, the frame's
        // deflections carried across (the same mass gives the same coordinates back), the coil's mounts' modes a little stiffer.
        [Test]
        public void TheModesFollowTheCoil()
        {
            MassModel mass = Spec.BuildMass(Spec.Nominal);
            var hubs = new Double3[4];
            PropulsionSpec p = Spec.BuildPropulsion();
            for (int i = 0; i < 4; i++) hubs[i] = p.Motors[i].Position;
            var modes = new AirframeModes(Spec.BuildModes(), mass, hubs);
            ModeSet q = default, rate = default;
            for (int m = 0; m < modes.Count; m++)
            {
                q[m] = 1e-4 * (m + 1);
                rate[m] = -2e-3 * (m + 1);
            }
            ModeSet q0 = q, rate0 = rate;
            var before = new double[modes.Count];
            for (int m = 0; m < modes.Count; m++) before[m] = modes.Frequency(m);
            modes.Rebuild(mass, ref q, ref rate);
            for (int m = 0; m < modes.Count; m++)
            {
                Assert.That(q[m], Is.EqualTo(q0[m]).Within(1e-12 * DetMath.Abs(q0[m])), "the same mass maps each mode onto itself");
                Assert.That(rate[m], Is.EqualTo(rate0[m]).Within(1e-12 * DetMath.Abs(rate0[m])));
            }
            int coil = Spec.Coil.Part;
            double lighter = mass.Part(coil).Mass - 6.9e-4;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            mass.SetPartMass(coil, lighter);
            modes.Rebuild(mass, ref q, ref rate);
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.That(allocated, Is.EqualTo(0), "the rebuild allocates nothing");
            double highest = 0;
            for (int m = 0; m < modes.Count; m++) highest = DetMath.Max(highest, (modes.Frequency(m) - before[m]) / before[m]);
            Print($"the coil 0.69 g lighter: the modes' frequencies rise by up to {highest:E2} of themselves");
            Assert.That(highest, Is.GreaterThan(0), "a lighter coil on the same mount rings higher");
            Assert.That(highest, Is.LessThan(1e-3));
        }

        // DRN-05: the pose the renderer reads carries the line: its active nodes, the trail's published count and the payout, and
        // an interpolated pose takes the latest step's nodes (a split or merge between two steps changes which material each is).
        [Test]
        public void ThePoseCarriesTheLine()
        {
            FlightSetup setup = TetherScenarios.Setup(Spec);
            FlightCore core = TetherScenarios.Fly(setup, 8, TetherScenarios.OutAndBack(setup.StartPosition.Y, 5, 3, 6, double.PositiveInfinity, 0), null);
            core.GetPose(out PoseFrame a);
            CableChain line = core.Tether.Chain;
            Assert.That(a.FiberNodeCount, Is.EqualTo(line.Count));
            for (int i = 0; i < line.Count; i++) Assert.That(a.FiberNodes[i], Is.EqualTo(line[i].Position), "node " + i);
            Assert.That(a.FiberTrailCount, Is.EqualTo(core.Tether.TrailCount));
            Assert.That(a.FiberPaidOut, Is.EqualTo(core.Tether.PaidOut));
            Assert.That((a.FiberNodes[0] - core.Tether.Trail[core.Tether.TrailCount - 1]).Length(), Is.LessThan(1e-4),
                "node 0 sits on the trail's last point (on its anchor spring)");
            core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true, Throttle = 0.5 });
            core.GetPose(out PoseFrame b);
            PoseFrame.Lerp(a, b, 0.25, out PoseFrame mid);
            Assert.That(mid.FiberNodeCount, Is.EqualTo(b.FiberNodeCount));
            Assert.That(mid.FiberNodes[b.FiberNodeCount - 1], Is.EqualTo(b.FiberNodes[b.FiberNodeCount - 1]));
            core.GetPose(out PoseFrame none);
            FlightSetup bare = TetherScenarios.Setup(Spec);
            bare.Tether = null;
            new FlightCore(bare).GetPose(out none);
            Assert.That(none.FiberNodeCount, Is.EqualTo(0), "no line, no nodes");
        }

        // The line's step allocates nothing, and a flight with it replays bit for bit from its log (the header's table, layout and
        // coil; the record's fiber block).
        [Test]
        public void AFlightWithTheLineAllocatesNothingAndReplays()
        {
            FlightSetup setup = TetherScenarios.Setup(Spec, WindLevel.Windy);
            setup.Modes = Spec.BuildModes(); // the coil's rebuilds re-solve the airframe's modes in the step
            var core = new FlightCore(setup);
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(Spec, Spec.Nominal);
            var log = new System.IO.MemoryStream();
            var recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            TetherScenarios.Plan plan = TetherScenarios.OutAndBack(setup.StartPosition.Y, 8, 4, 12, 9, 12);
            long allocated = 0;
            for (int k = 0; k < 12000; k++)
            {
                StickSample stick;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    plan(k * FlightCore.Dt, core, pilot);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                long before = GC.GetAllocatedBytesForCurrentThread();
                core.Advance(stick);
                if (k >= 2000) allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Print($"paid {core.Tether.PaidOut:F2} m, {core.Tether.Chain.Count} nodes, trail {core.Tether.TrailCount}, the modes rebuilt with the coil; "
                + $"{allocated} B allocated in the steps");
            Assert.That(core.Tether.PaidOut, Is.GreaterThan(20), "rebuilds happened");
            Assert.That(allocated, Is.EqualTo(0), "0 B in steady state");
            ContactScenarios.RepositoryMaterials(out ulong materials); // the replay holds the flight's physics_materials.json (p7f-damage 1.1a)
            ReplayResult r = FlightReplay.Run(new System.IO.MemoryStream(log.ToArray()), id => id == "floor" ? FlightWorld.Floor() : null, Spec.ContentHash,
                tetherHash: TetherScenarios.Table().ContentHash, materialsHash: materials);
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
            Assert.That(r.Steps, Is.EqualTo(12000));
        }
    }
}
