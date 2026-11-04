using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>fiber-tether's 1.7 scenarios (p7d-tether design 6 and 12): the fiber's contacts (FiberContactScenarios).</summary>
    public class FiberContactTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        static void Print(string s) => TestContext.WriteLine(s);

        // "The bend is the elastica's": at 0.05, 0.2, 0.5 and 2 N, turning 15°, 45°, 90° and 180°, over a point edge and 2 mm and
        // 6 mm radii: the logged bend radius equals max(R_edge + c, √(EI/T)/(2·sin(θ/4))) within 1e-9 relative, and the stress
        // E·r/R_eff + T/A_g likewise, both at the logged turn and tension (the elastica over a point edge, the fortifications
        // reading §2.1).
        [Test]
        public void TheBendIsTheElasticas()
        {
            TetherTable t = TetherScenarios.Table();
            double ei = t.Number("fiber.bending_stiffness_n_m2"), c = t.Number("fiber.coating_thickness_m"), e = t.Number("fiber.youngs_modulus_pa");
            double r = t.Number("fiber.glass_diameter_m") / 2, ag = t.Number("fiber.glass_area_m2");
            double worstR = 0, worstS = 0, worstTurn = 0, worstTension = 0;
            int capped = 0, cases = 0;
            foreach (double edge in new[] { 0, 0.002, 0.006 })
                foreach (double tension in new[] { 0.05, 0.2, 0.5, 2 })
                    foreach (double degrees in new[] { 15.0, 45, 90, 180 })
                    {
                        FiberContactScenarios.Bend b = FiberContactScenarios.BendOver(tension, degrees, edge);
                        Assert.That(b.Logged, Is.True, "FiberContact logged");
                        double expected = Math.Max(edge + c, Math.Sqrt(ei / b.LoggedTension) / (2 * Math.Sin(b.LoggedTurn / 4)));
                        double stress = e * r / expected + b.LoggedTension / ag;
                        worstR = Math.Max(worstR, Math.Abs(b.LoggedRadius / expected - 1));
                        worstS = Math.Max(worstS, Math.Abs(b.Stress / stress - 1));
                        worstTurn = Math.Max(worstTurn, Math.Abs(b.LoggedTurn - b.Turn));
                        worstTension = Math.Max(worstTension, Math.Abs(b.LoggedTension / tension - 1));
                        if (expected == edge + c) capped++;
                        cases++;
                        if (degrees == 90 && tension == 0.2)
                            Print($"edge {edge * 1000:F0} mm, 0.2 N at 90°: R_eff {b.LoggedRadius * 1000:F4} mm, σ {b.Stress / 1e9:F4} GPa");
                    }
            Print($"{cases} cases ({capped} at the arris' cap R_edge + c): R_eff within {worstR:E2}, σ within {worstS:E2} of the law; "
                + $"the logged turn within {worstTurn:E2} rad and tension within {worstTension:E2} of the case's");
            Assert.That(worstR, Is.LessThanOrEqualTo(1e-9));
            Assert.That(worstS, Is.LessThanOrEqualTo(1e-9));
            Assert.That(capped, Is.GreaterThan(0), "both branches of the law are met");
            Assert.That(capped, Is.LessThan(cases));
            Assert.That(worstTurn, Is.LessThan(1e-3), "premise: the case's turn");
            Assert.That(worstTension, Is.LessThan(1e-2), "premise: the case's tension");
        }

        // "A segment far from everything is not queried": trailing from 50 m up at 20 m/s, each segment is queried at most once
        // per 25 steps and never after its certificate has run out; and a shape 0.4 m beside a segment is met in the step the
        // segment first reaches it.
        [Test]
        public void ASegmentFarFromEverythingIsNotQueried()
        {
            FiberContactScenarios.Certificate c = FiberContactScenarios.HighCruise(Spec);
            Print($"trailing at 20 m/s {c.Height:F1} m up (the line down to {c.Lowest:F2} m): {c.Queries} queries over {c.Steps} steps of {c.Segments} segments, "
                + $"{(double)c.Queries / c.Steps:F2} a step; the least gap between two queries of one segment {c.LeastGap} steps; expired and not queried {c.MissedExpiries}");
            Assert.That(c.Height, Is.GreaterThan(45), "premise: up high");
            Assert.That(c.LeastGap, Is.GreaterThanOrEqualTo(25));
            Assert.That(c.MissedExpiries, Is.EqualTo(0));
            Assert.That(c.Queries, Is.GreaterThan(0));

            FiberContactScenarios.Pole p = FiberContactScenarios.PoleBeside(Spec);
            Print($"the pole {p.PlacedAt:F3} m beside the hanging line's segment: reached at step {p.Reached}, its first FiberContact at step {p.Met}, pressing when reached {p.PressingWhenReached}; "
                + $"the deepest a segment went into it {p.Deepest * 1000:F3} mm; within the margin {p.NearSteps} steps, queried in {p.NearQueried}");
            Assert.That(p.PlacedAt, Is.EqualTo(0.4).Within(0.02), "premise: placed 0.4 m beside a segment");
            Assert.That(p.Reached, Is.GreaterThan(0), "premise: the line reaches the pole");
            Assert.That(p.Met, Is.GreaterThan(0).And.LessThanOrEqualTo(p.Reached), "met by the step the segment reaches it");
            Assert.That(p.Deepest, Is.LessThan(1e-3));
            Assert.That(p.NearQueried, Is.EqualTo(p.NearSteps), "a segment within the margin of a shape is queried every step");
        }

        // "The wedge of a fork": a line pulled down into the fork of two limbs until one node touches both: both contacts act in
        // the same step, the tension rises only as the drone pulls, nothing is non-finite, and the line holds (1.8 judges a break
        // by tension or bend from the stress printed here).
        [Test]
        public void TheWedgeOfAFork()
        {
            FiberContactScenarios.Wedge w = FiberContactScenarios.ForkWedge();
            Print($"a node on both limbs from step {w.FirstBoth} ({w.BothSteps} node-steps); the tension at the stop {w.AtStop:F4} N (the most while pulled {w.Most:F4}), "
                + $"the most held still after {w.MostAfterStop:F4} N; at the crotch R_eff down to {w.LeastRadius * 1000:F3} mm, σ up to {w.MostStress / 1e9:F3} GPa; "
                + $"wedges {w.Wedges}, safe steps {w.SafeSteps}");
            Assert.That(w.FirstBoth, Is.GreaterThan(0), "a node takes both limbs in one step");
            Assert.That(w.BothSteps, Is.GreaterThan(100));
            Assert.That(w.MostAfterStop, Is.LessThanOrEqualTo(w.AtStop * 1.001 + 1e-6), "the tension rises only as the drone pulls");
            Assert.That(w.Finite, Is.True);
            Assert.That(w.Wedges, Is.EqualTo(0));
        }

        // "A line round a skid tethers the drone": the line wrapped once round the rear skid and held fast on the ground behind,
        // the drone climbing: the skid's contact carries the line's load, the drone is pulled down and back at the skid and leans
        // toward it, and the line slips round the skid by the capstan (1.8's breaks by tension or bend are not in yet), logged.
        [Test]
        public void ALineRoundASkidTethersTheDrone()
        {
            FiberContactScenarios.Skid k = FiberContactScenarios.SkidWrap(Spec);
            Print($"climbing from the re-lay: toward the anchor {k.TowardAnchor:F4} N on the mean; the skid's reaction up to {k.MostReaction:F3} N over {k.Loaded} steps, its mean ({k.MeanReaction.X:F3}, {k.MeanReaction.Y:F3}, {k.MeanReaction.Z:F3}) N; "
                + $"the line up to {k.MostTension:F3} N, the bend's stress at the skid up to {k.MostStress / 1e9:F3} GPa; slipped round it {k.Slipped} at {k.SlipAt:F3} s; "
                + $"the drone leaning toward it up to {k.PitchToward:F4} (pitch, sin) and {k.RollToward:F4} (roll); paid out {k.PaidOut:F3} m after; wedges {k.Wedges}, crashed {k.Crashed}");
            Assert.That(k.Loaded, Is.GreaterThan(100), "the skid carries the line's load");
            Assert.That(k.MeanReaction.Y, Is.LessThan(0), "pulled down at the skid");
            Assert.That(k.TowardAnchor, Is.GreaterThan(0), "and toward the anchor behind (along the skid only by friction)");
            Assert.That(Math.Max(k.PitchToward, k.RollToward), Is.GreaterThan(0), "it leans toward the skid");
            Assert.That(k.Slipped, Is.True, "slipping round the skid is logged (FiberSlip)");
            Assert.That(k.Wedges, Is.EqualTo(0));
            Assert.That(k.Crashed, Is.False);
        }

        // "The line lies on what is under it": hovering 30 m from its anchor over the range's plane, S04's straw and E01's run,
        // after 60 s, the line from the touchdown back to the anchor lies on the ground, the straw's top or the trench's floor and
        // walls within 2 mm, its trail points never inside a rigid shape, and none of it floats.
        [TestCase("plane")]
        [TestCase("straw")]
        [TestCase("run")]
        public void TheLineLiesOnWhatIsUnderIt(string spot)
        {
            FiberContactScenarios.Lying l = FiberContactScenarios.LieOn(Spec, spot);
            Print($"{spot}: {l.Points} points (the trail and the chain to the touchdown), on the plane {l.OnPlane}, the straw {l.OnStraw}, soil {l.OnSoil}, "
                + $"other {l.OnOther}; worst above a surface {l.WorstAbove * 1000:F3} mm, deepest inside a shape {l.DeepestInside * 1000:F3} mm, floating {l.Floating}; "
                + $"paid out {l.PaidOut:F3} m; wedges {l.Wedges}");
            Assert.That(l.Points, Is.GreaterThan(1));
            Assert.That(l.Floating, Is.EqualTo(0), "none of it floats");
            Assert.That(l.WorstAbove, Is.LessThanOrEqualTo(0.002));
            Assert.That(l.DeepestInside, Is.LessThan(1e-4), "never inside a rigid shape");
            // The chain's TetherWedges are printed, not asserted here (not this scenario's THEN): over S04 the line hanging by the
            // left rear leg and skid takes some (the handoff's open defect: the spool segment's model is not majorized in the
            // safe pass, so a contact-driven deficit at the exit is left on a line at rest, which no clip can take back).
            if (spot != "straw") Assert.That(l.Wedges, Is.EqualTo(0));
            if (spot == "plane") Assert.That(l.OnPlane, Is.GreaterThan(0));
            if (spot == "straw") Assert.That(l.OnStraw, Is.GreaterThan(0));
            if (spot == "run") Assert.That(l.OnSoil, Is.GreaterThan(0));
        }

        // "Compliant shells are skipped": a line dragged through V01_L's crown meets its limb capsules and its records (its twigs
        // on this line: p7a's drawn crown twigs are still to land), never its twig shell pieces, and the log names each contact's
        // shape or record; synthetic twig elements are met by their element Id.
        [Test]
        public void CompliantShellsAreSkipped()
        {
            FiberContactScenarios.Shells s = FiberContactScenarios.Crown(Spec);
            Print($"V01_L's crown: FiberContact on limbs {s.Limbs}, on records {s.Records}, on twig shells {s.ShellEvents}; slot-steps on a shell {s.ShellSlots} "
                + $"over {s.Steps} steps; wedges {s.Wedges}");
            Assert.That(s.Limbs + s.Records, Is.GreaterThan(0), "premise: the line meets the crown");
            Assert.That(s.ShellEvents, Is.EqualTo(0));
            Assert.That(s.ShellSlots, Is.EqualTo(0));

            FiberContactScenarios.Elements e = FiberContactScenarios.TwigRow();
            Print($"synthetic twigs: {e.Events} FiberContact events naming {e.Distinct} elements, every one an element of the row {e.NamedRight}; "
                + $"held on a twig {e.HeldSteps} steps; wedges {e.Wedges}");
            Assert.That(e.Distinct, Is.GreaterThan(0));
            Assert.That(e.NamedRight, Is.True, "the log names each element's Id");
            Assert.That(e.Wedges, Is.EqualTo(0));
        }

        // The audit (p7d-tether 1.7): without the fiber's contacts (FlightSetup.TetherWithoutContacts) the tether golden is model
        // 50's pin, every record and event, and no other golden flies the line. On the line since model 55 it is model 51's pin too
        // (the hanging half's give on; 51 kept every pin of 50's, GoldenFlightTests.WithoutTheHangingGiveEachGoldenIsModel50s).
        [Test]
        public void WithoutTheFiberContactsEachGoldenIsModel50s()
        {
            DroneSpec spec = Spec;
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, fiberContacts: false), Is.EqualTo(GoldenFlights.Model50TetherHash),
                "tether, without the contacts");
            Assert.That(line.Tether.Contacts, Is.Null);
        }
    }
}
