using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The flight on hills (w3-farmland-ground 2.2, model 82): the legs, a crash and a prop's strike on a map package's terrain
    // (SlopeScenarios: a plane tile of meadow_sod, 0° the stub floor of meadow_sod) and at the farmland's steps.
    public sealed class SlopeTests
    {
        static DroneSpec _drone;
        static DroneSpec Drone() => _drone ??= DroneSpec.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));

        static void Print(string s) => TestContext.Progress.WriteLine(s);

        // "A level terrain is the plane": the pilot's landing on a 0° tile of the package lands as on the stub floor of the same
        // surface: its supports take the ground from their own sweeps, the floor's from the samples, and both meet one plane.
        [Test]
        public void OnALevelTerrain_TheLandingIsThePlanes()
        {
            SlopeScenarios.Touchdown plane = SlopeScenarios.Land(Drone(), 0, RainLevel.Sunny, 0);
            SlopeScenarios.Touchdown terrain = SlopeScenarios.Land(Drone(), 0, RainLevel.Sunny, 0, terrainAtZero: true);
            Print($"plane: touch {plane.TouchSpeed:F4} m/s, sink {plane.PeakSink * 1e3:F3} mm, peak {plane.PeakAccel:F2} m/s²; "
                + $"terrain: {terrain.TouchSpeed:F4}, {terrain.PeakSink * 1e3:F3} mm, {terrain.PeakAccel:F2}");
            Assert.That(terrain.TouchSpeed, Is.EqualTo(plane.TouchSpeed).Within(1e-6));
            Assert.That(terrain.PeakSink, Is.EqualTo(plane.PeakSink).Within(1e-6));
            Assert.That(terrain.PeakAccel, Is.EqualTo(plane.PeakAccel).Within(0.01 * plane.PeakAccel));
            Assert.That(terrain.Tunnels + plane.Tunnels, Is.EqualTo(0));
        }

        // "The blades meet a terrain": the tipped landing rolls the drone onto its lower props on the 0° tile and the blades carve
        // its soil as on the plane (before 2.2 a terrain's ground had no geometry for them: no pass at all), and on a 20° slope.
        [Test]
        public void OnATerrain_ThePropsStrikeTheGroundAsOnThePlane()
        {
            StrikeScenarios.TipResult plane = SlopeScenarios.Strike(Drone(), 0), terrain = SlopeScenarios.Strike(Drone(), 0, true);
            StrikeScenarios.TipResult slope = SlopeScenarios.Strike(Drone(), 20);
            Print("plane: " + plane.Describe());
            Print("0° terrain: " + terrain.Describe());
            Print("20°: " + slope.Describe());
            Assert.That(plane.InSoil, Is.GreaterThan(10), "premise: the blades carve the plane's soil");
            Assert.That(terrain.InSoil, Is.EqualTo(plane.InSoil));
            Assert.That(terrain.Work, Is.EqualTo(plane.Work).Within(0.01 * plane.Work));
            Assert.That(slope.InSoil, Is.GreaterThan(10), "on the slope too");
            Assert.That(terrain.Tunnels + slope.Tunnels, Is.EqualTo(0));
        }

        // "The resting drone holds by its soil and slides past it": disarmed on dry meadow_sod, its skids along the fall line, it
        // holds at 20° and slides at 30.03°, past atan μs (28.8°): the groove's end resistance does not hold a sunk skid there.
        // Its compliant legs split the slope's pull by their side stiffness, so it holds at least to atan μk (24.2°), where a slide
        // that its light uphill corners start stops again.
        [Test]
        public void AtRest_ItHoldsOnTwentyDegreesAndSlidesPastTheFrictionBound()
        {
            SlopeScenarios.Rest held = SlopeScenarios.Settle(Drone(), 20, RainLevel.Sunny, 0);
            SlopeScenarios.Rest slid = SlopeScenarios.Settle(Drone(), 30, RainLevel.Sunny, 0);
            double bound = SlopeScenarios.FrictionBound(RainLevel.Sunny), stops = SlopeScenarios.FrictionBound(RainLevel.Sunny, kinetic: true);
            Print($"{held.Facet:F2}°: moved {held.Slide * 1e3:F1} mm, {held.Late * 1e3:F1} mm in the last second; {slid.Facet:F2}°: {slid.Slide:F3} m, {slid.Late:F3} m; "
                + $"atan μk {stops:F2}°, atan μs {bound:F2}°");
            Assert.That(held.Facet, Is.LessThan(stops), "premise: under atan μk");
            Assert.That(SlopeScenarios.Slipped(held), Is.False);
            Assert.That(slid.Facet, Is.GreaterThan(bound), "premise: past atan μs");
            Assert.That(SlopeScenarios.Slipped(slid), Is.True);
            Assert.That(held.Tunnels + slid.Tunnels, Is.EqualTo(0));
        }

        // "A landing on the steepest plot does not blow up": the pilot's landing on 38° (the village's steepest plot is 37.6°)
        // slides and tumbles down it with nothing past the crash's acceleration. Before 2.2 a leg's anchor ran away there once its
        // side give was spent (709 km of stretch in one step: 207,766 m/s²; ContactModel.NilStiffness).
        [Test]
        public void OnThirtyEightDegrees_TheLandingSlidesWithoutABlowUp()
        {
            SlopeScenarios.Touchdown t = SlopeScenarios.Land(Drone(), 38, RainLevel.Sunny, 0);
            Print($"{t.Facet:F2}°: touch {t.TouchSpeed:F3} m/s, peak {t.PeakAccel:F0} m/s², slid {t.Slide:F2} m, {t.EndSpeed:F2} m/s at the end, "
                + $"crashes {t.Crashes}, tunnels {t.Tunnels}");
            Assert.That(t.PeakAccel, Is.LessThan(490.3), "under the crash's acceleration");
            Assert.That(t.EndSpeed, Is.LessThan(15), "a slide's speed, not a blow-up's");
            Assert.That(t.Tunnels, Is.EqualTo(0));
        }

        // "A crash into a slope is the crash into the plane turned": 10 m/s into 30° uphill against the same dive at 30° onto the
        // floor, its frame turned; the deepest part past the ground within 2 mm, no tunnel (gravity's turn differs over 50 ms).
        [Test]
        public void ACrashIntoASlope_IsTheDiveOntoThePlane()
        {
            SlopeScenarios.Impact slope = SlopeScenarios.Crash(Drone(), 30, 10), plane = SlopeScenarios.Crash(Drone(), 30, 10, true);
            Print($"slope: crash by {slope.CrashBy} at {slope.Speed:F2} m/s, deepest {slope.Depth * 1e3:F2} mm; plane: {plane.CrashBy} at {plane.Speed:F2}, {plane.Depth * 1e3:F2} mm");
            Assert.That(slope.CrashStep, Is.GreaterThan(0));
            Assert.That(plane.CrashStep, Is.GreaterThan(0));
            Assert.That(slope.Depth, Is.EqualTo(plane.Depth).Within(0.002));
            Assert.That(slope.Tunnels + plane.Tunnels, Is.EqualTo(0));
        }

        // "A leg meets a step's face, not its top": sliding at 1 m/s into the farmland's far seam (7.4 cm) nose first, the skids'
        // front supports meet the face; before 2.2 a support past the face read the step's top as its overlap and its leg took
        // 385 N, lifting the drone onto the step.
        [Test]
        public void AtAStep_TheLegMeetsTheFace()
        {
            SlopeScenarios.Ledge l = SlopeScenarios.AtLedge(Drone(), true);
            Print($"the seam's step {l.Step * 1e3:F1} mm: the most on a leg {l.PeakLoad:F1} N, peak {l.PeakAccel:F0} m/s², rise {l.Rise * 1e3:F1} mm, tunnels {l.Tunnels}");
            Assert.That(l.Step, Is.GreaterThan(0.05), "premise: a step the skids meet");
            Assert.That(l.PeakLoad, Is.LessThan(150));
            Assert.That(l.Rise, Is.LessThan(0.01));
            Assert.That(l.Tunnels, Is.EqualTo(0));
        }

        // "A node behind a face takes the face": a node 0.3 m under the ledge's top, 2 cm behind its east face, samples the top
        // (its plane 0.3 m above it); its ground is the face, out east.
        [Test]
        public void ANodeBehindTheLedgesFace_TakesTheFace()
        {
            MapWorld world = SlopeScenarios.Farmland();
            GroundSample top = SlopeScenarios.Ground(world, SlopeScenarios.LedgeX - 0.02, SlopeScenarios.LedgeZ);
            var node = new Double3(SlopeScenarios.LedgeX - 0.02, top.Height - 0.3, SlopeScenarios.LedgeZ);
            Double3 at = new Double3(node.X, top.Height, node.Z), normal = top.Normal;
            Assert.That(Double3.Dot(normal, node - at), Is.LessThan(-FiberLine.WallDepth), "premise: behind the face by the sample");
            FiberLine.NearestGround(world, node, 0.0002, new StaticContact[8], ref at, ref normal);
            Print($"the face's point ({at.X:F4}, {at.Y:F3}, {at.Z:F3}), normal ({normal.X:F3}, {normal.Y:F3}, {normal.Z:F3})");
            Assert.That(normal.X, Is.EqualTo(1).Within(1e-9));
            Assert.That(at.X, Is.EqualTo(SlopeScenarios.LedgeX).Within(1e-6));
            Assert.That(Double3.Dot(normal, node - at), Is.EqualTo(-0.02).Within(1e-6), "2 cm behind it");
        }

        // "A laid line lies over a crest" (w3-farmland-ground 2.5, model 82): a line laid over a 3 m ridge, the spacing law's
        // nodes 10–25 m apart there, whose chords between them pass more than a metre under the crest (the line before 2.5). The
        // crests' nodes hold it over the crest from the launch on: no segment lies deeper than InsertGap + KeepGap in the ground
        // at any step's end (FiberLine's guarantee between tests), a crest's node lies on the ground at the crest line, the line
        // stays whole, the steps allocate nothing, and the flight replays bit for bit from its log.
        [Test]
        public void ALineLaidOverARidge_LiesOverItsCrest_AndReplays()
        {
            SlopeScenarios.LineRidge r = SlopeScenarios.LineOverRidge(Drone());
            Print($"the launch's law chords {r.Launched:F3} m under the crest, its deepest segment {r.LaunchedDeepest:F4} m in the ground with the crests' nodes; "
                + $"over the flight the deepest {r.Deepest:F4} m; the nearest node to the crest line {r.CrestZ:F3} m off it, {r.CrestHeight * 1e3:F2} mm over the ground, "
                + $"a crest's: {r.OnCrest}; {r.Inserted} crests' nodes put in, {r.Refused} refused; {r.Paid:F1} m paid out, broken {r.Broken} {r.Break}, wedges {r.Wedges}, {r.Allocated} B");
            Assert.That(r.Launched, Is.GreaterThan(1), "premise: the spacing law's chords pass under the crest");
            Assert.That(r.LaunchedDeepest, Is.LessThanOrEqualTo(FiberLine.CrestDepth), "the launch's line lies over it");
            Assert.That(r.Deepest, Is.LessThanOrEqualTo(FiberLine.CrestDepth), "and so does the flown one");
            Assert.That(r.CrestZ, Is.LessThan(1), "a node at the crest line");
            Assert.That(r.CrestHeight, Is.LessThan(FiberContacts.InsertGap), "on the ground");
            Assert.That(r.OnCrest, Is.True, "a crest's node");
            Assert.That(r.Refused, Is.EqualTo(0));
            Assert.That(r.Broken, Is.False);
            Assert.That(r.Wedges, Is.EqualTo(0));
            Assert.That(r.Allocated, Is.EqualTo(0), "0 B in steady state" + AllocProbe.Report());
            ContactScenarios.RepositoryMaterials(out ulong materials);
            ReplayResult same = FlightReplay.Run(new System.IO.MemoryStream(r.Log), id => id == "ridge" ? new FlightWorld(SlopeScenarios.Ridge()) : null,
                Drone().ContentHash, tetherHash: TetherScenarios.Table().ContentHash, materialsHash: materials);
            Print(same.Message);
            Assert.That(same.Outcome, Is.EqualTo(ReplayOutcome.Match), same.Message);
            Assert.That(same.Steps, Is.EqualTo(10000));
        }

        // "The line wraps the ledge's lip" (2.5): laid on the far ring and dragged up the ledge's 1.5 m face and over its lip as the
        // drone flies west over the top (2.2's row). The chords between the spacing law's nodes would cut the step's corner (the
        // line before 2.5); a crest's node lies on the lip, within CrestDepth of its edge, no segment lies deeper than CrestDepth
        // in the ground, and the line stays whole, its nodes as slow as before 2.5 (3.5 m/s).
        [Test]
        public void ALineDraggedOverTheLedge_WrapsItsLip()
        {
            SlopeScenarios.LineLedge l = SlopeScenarios.LineOverLedge(Drone());
            Print($"the law's chords at most {l.LawDeepest:F4} m in the ground, the line {l.Deepest:F4} m; a crest's node on the lip {l.OnLip} steps; "
                + $"{l.Crests} crests' nodes put in; the fastest node {l.FastestNode:F2} m/s, {l.Paid:F1} m paid out, " + (l.Broken ? "broken: " + l.Break : "whole"));
            Assert.That(l.LawDeepest, Is.GreaterThan(2 * FiberLine.CrestDepth), "premise: the spacing law's chords cut the step's corner");
            Assert.That(l.Deepest, Is.LessThanOrEqualTo(FiberLine.CrestDepth));
            Assert.That(l.OnLip, Is.GreaterThan(1000), "a crest's node on the lip");
            Assert.That(l.FastestNode, Is.LessThan(5));
            Assert.That(l.Broken, Is.False);
        }
    }
}
