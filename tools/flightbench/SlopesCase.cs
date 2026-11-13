using System;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // The flight on hills (w3-farmland-ground 2.2): the legs-on drone with surfaces.json on slopes of 0–38° (SlopeScenarios: a
    // map package's plane tile of meadow_sod; 0° the stub floor of meadow_sod, a plane) and at the farmland's steps. `--slopes`
    // prints every row and judges the ones with a reference: a level terrain against the plane, a crash into a slope against the
    // same dive onto the plane turned, the resting drone's slip against atan μs and its roll against the rigid-body tip, the steps
    // against their faces; the line over the ledge and over a ridge (2.5). `--slopes <part>` prints one part: rest, land, crash,
    // strike, ledge or line.
    internal static class SlopesCase
    {
        static readonly double[] Angles = { 0, 10, 20, 30, 38 };
        static bool _ok;

        static void Row(string name, bool pass, string detail)
        {
            _ok &= pass;
            Console.WriteLine($"    {name}: {detail}: {(pass ? "PASS" : "FAIL")}");
        }

        public static bool Run(string only = null)
        {
            _ok = true;
            bool On(string part) => only == null || only == part;
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            Console.WriteLine("  the slopes (deg, a facet's): " + string.Join(", ", Array.ConvertAll(Angles, a => SlopeScenarios.FacetDeg(
                SlopeScenarios.Ground(a == 0 ? (IWorldQuery)new FloorWorld(0, "meadow_sod") : SlopeScenarios.Slope(a), 0, 0)).ToString("F2"))));

            if (On("rest"))
            {
                Console.WriteLine("  at rest, disarmed, 3 s on meadow_sod: the move down the fall line (m), in the last second (m), the end speed (m/s), the largest tilt from the normal (deg)");
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
                    foreach (double yaw in new[] { 0.0, 90 })
                    {
                        var line = new System.Text.StringBuilder($"    {rain,-10} {(yaw == 0 ? "along" : "across"),6}:");
                        foreach (double deg in Angles)
                        {
                            SlopeScenarios.Rest r = SlopeScenarios.Settle(spec, deg, rain, yaw);
                            line.Append($" | {r.Facet:F0}° {r.Slide:F3} {r.Late:F3} m {r.Speed:F2} m/s {r.Tilt:F0}°{(r.Tunnels > 0 ? " TUNNEL" : "")}");
                            _ok &= r.Tunnels == 0;
                        }
                        Console.WriteLine(line);
                    }
                Console.WriteLine("  the steepest slope it holds on with its skids along the fall line (bisection over the tile's units), at least the friction's atan μk:");
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
                {
                    double holds = SlopeScenarios.Holds(spec, rain, 0), bound = SlopeScenarios.FrictionBound(rain), stops = SlopeScenarios.FrictionBound(rain, kinetic: true);
                    Row($"{rain}", holds >= stops - 0.5, $"holds to {holds:F2}° (atan μk {stops:F2}°, atan μs {bound:F2}°)");
                }
                var legs = LandingStability.Legs(spec, spec.BuildContacts(spec.Nominal, m, h), spec.BuildMass(spec.Nominal).Properties);
                double tip = legs.StaticRollTipDeg;
                SlopeScenarios.Rest held = SlopeScenarios.Settle(spec, 10, RainLevel.Sunny, 90), over = SlopeScenarios.Settle(spec, 30, RainLevel.Sunny, 90);
                Row("across the slope", held.Tilt < 5 && over.Tilt > 45,
                    $"at 10° it stands ({held.Tilt:F1}° from the normal), at 30° it rolls over ({over.Tilt:F0}°); the rigid body tips over its downhill skid at {tip:F2}° (LandingStability)");
            }

            if (On("land"))
            {
                Console.WriteLine("  the pilot's landing at 0.5 m/s, the throttle cut at the touch, dry: touch (m/s), peak (m/s²), sink (mm), slide (m), end speed (m/s), tilt from the normal max/end (deg)");
                foreach (double yaw in new[] { 0.0, 90 })
                    foreach (double deg in Angles)
                    {
                        SlopeScenarios.Touchdown t = SlopeScenarios.Land(spec, deg, RainLevel.Sunny, yaw);
                        Console.WriteLine($"    {(yaw == 0 ? "along" : "across"),6} {t.Facet,5:F2}°: {t.TouchSpeed:F3}, {t.PeakAccel,6:F0}, {t.PeakSink * 1e3,5:F1}, {t.Slide,6:F2}, {t.EndSpeed,5:F2}, "
                            + $"{t.MaxTilt,5:F1}/{t.EndTilt,5:F1}{(t.Over ? ", over" : "")}; crashes {t.Crashes}, tunnels {t.Tunnels}");
                        _ok &= t.Tunnels == 0 && t.PeakAccel < 2 * 490.3;
                    }
                SlopeScenarios.Touchdown plane = SlopeScenarios.Land(spec, 0, RainLevel.Sunny, 0), terrain = SlopeScenarios.Land(spec, 0, RainLevel.Sunny, 0, true);
                Row("a level terrain lands as the plane", Math.Abs(terrain.PeakSink - plane.PeakSink) < 1e-6 && Math.Abs(terrain.PeakAccel / plane.PeakAccel - 1) < 0.01,
                    $"sink {terrain.PeakSink * 1e3:F4} against {plane.PeakSink * 1e3:F4} mm, peak {terrain.PeakAccel:F2} against {plane.PeakAccel:F2} m/s²");
            }

            if (On("crash"))
            {
                Console.WriteLine("  a crash into the slope at 10 m/s uphill, disarmed and level, against the same dive onto the plane, turned");
                foreach (double deg in new[] { 20.0, 30, 38 })
                {
                    SlopeScenarios.Impact c = SlopeScenarios.Crash(spec, deg, 10), p = SlopeScenarios.Crash(spec, deg, 10, true);
                    // The ledger's gap 14 (D-091, w9 2.6): no support or skid past the ground by more than the sweep's 20 mm beyond its
                    // corner's own give; the parts as the contact model has them, the dropped pieces gone. The frame's deepest is printed
                    // beside (a soft layer's compression, the sweep's finding 11, is not judged here).
                    double tol = SweepScenarios.GroundTolerance;
                    Row($"{c.Facet:F2}°", c.CrashStep > 0 && p.CrashStep > 0 && c.LegExcess <= tol && p.LegExcess <= tol && c.Tunnels + p.Tunnels == 0,
                        $"the crash by the {c.CrashBy} at {c.Speed:F2} m/s, {c.PeakAccel:F0} m/s² (the plane: {p.CrashBy}, {p.Speed:F2} m/s, {p.PeakAccel:F0} m/s²); "
                        + $"the deepest past the ground {c.Depth * 1e3:F2} mm by the {c.DepthBy} (the plane {p.Depth * 1e3:F2} mm); a leg's part past its give "
                        + $"{c.LegExcess * 1e3:F2} mm by the {c.LegExcessBy ?? "-"} (the plane {p.LegExcess * 1e3:F2} mm, {p.LegExcessBy ?? "-"}; at most {tol * 1e3:F0}); tunnels {c.Tunnels + p.Tunnels}");
                }
            }

            if (On("strike"))
            {
                Console.WriteLine("  a prop's strike on the ground: the tipped landing (2.5 m/s of drift, the skid caught, the throttle cut)");
                StrikeScenarios.TipResult plane = SlopeScenarios.Strike(spec, 0), terrain = SlopeScenarios.Strike(spec, 0, true), slope = SlopeScenarios.Strike(spec, 20);
                Console.WriteLine("    the plane:      " + plane.Describe());
                Console.WriteLine("    the 0° terrain: " + terrain.Describe());
                Console.WriteLine("    20°:            " + slope.Describe());
                Row("the blades carve a terrain as the plane", terrain.InSoil == plane.InSoil && Math.Abs(terrain.Work / plane.Work - 1) < 0.01 && slope.InSoil > 10,
                    $"{terrain.InSoil} passes in the soil and {terrain.Work:F2} J against {plane.InSoil} and {plane.Work:F2} J; on 20° {slope.InSoil}");
            }

            if (On("ledge"))
            {
                Console.WriteLine("  a leg at the farmland's steps: the most on a leg (N), the peak (m/s²), the most the body rose (m)");
                SlopeScenarios.Ledge lip = SlopeScenarios.AtLedge(spec, false), face = SlopeScenarios.AtLedge(spec, true);
                Row("over the ledge's lip", lip.Tunnels == 0,
                    $"the step {lip.Step:F3} m: it tips over and falls it, {lip.PeakLoad:F0} N, {lip.PeakAccel:F0} m/s², and ends {lip.EndX - SlopeScenarios.LedgeX:F2} m past the lip; crashes {lip.Crashes}, tunnels {lip.Tunnels}");
                Row("into a far seam's face", face.PeakLoad < 150 && face.Rise < 0.01 && face.Tunnels == 0,
                    $"the step {face.Step * 1e3:F1} mm, nose first at 1 m/s: {face.PeakLoad:F0} N, {face.PeakAccel:F0} m/s², rose {face.Rise * 1e3:F1} mm; crashes {face.Crashes}, tunnels {face.Tunnels}");
            }

            if (On("line"))
            {
                Console.WriteLine("  the line laid on the far ring and dragged up over the ledge's lip as the drone flies west over the top");
                SlopeScenarios.LineLedge l = SlopeScenarios.LineOverLedge(spec);
                Row("the line over the ledge", !l.Broken && l.FastestNode < 20 && l.OnLip > 1000 && l.Deepest <= FiberLine.CrestDepth,
                    $"the fastest node {l.FastestNode:F2} m/s, the deepest behind the face {l.Behind * 1e3:F1} mm, {l.Paid:F1} m paid out, " + (l.Broken ? "broken: " + l.Break : "whole")
                    + $"; the deepest segment {l.Deepest:F4} m in the ground (the law's chords {l.LawDeepest:F4} m), a crest's node on the lip {l.OnLip} steps, {l.Crests} crests' nodes put in");
                Console.WriteLine("  a line laid over a 3 m ridge (16.7° a side), the drone flying away from it (w3-farmland-ground 2.5)");
                SlopeScenarios.LineRidge r = SlopeScenarios.LineOverRidge(spec);
                Row("the line over the ridge", !r.Broken && r.Deepest <= FiberLine.CrestDepth && r.OnCrest && r.CrestZ < 1 && r.Wedges == 0 && r.Allocated == 0,
                    $"the spacing law's chords {r.Launched:F3} m under the crest at the launch; the deepest segment {r.LaunchedDeepest:F4} m in the ground then, "
                    + $"{r.Deepest:F4} m over the flight; a crest's node {r.CrestZ:F3} m off the crest line, {r.CrestHeight * 1e3:F2} mm over the ground; "
                    + $"{r.Inserted} crests' nodes put in, {r.Refused} refused; {r.Paid:F1} m paid out, " + (r.Broken ? "broken" : "whole") + $", {r.Wedges} wedges, {r.Allocated} B");
            }
            Console.WriteLine(_ok ? "flightbench: slopes PASS" : "flightbench: slopes FAIL");
            return _ok;
        }
    }
}
