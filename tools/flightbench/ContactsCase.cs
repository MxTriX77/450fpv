using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.FlightBench
{
    // The ground-contact scenarios (p6-flight-core 1.6) on the world-query stub, from the file the suite compiles
    // (ContactScenarios): the landing with the sticks held and with the throttle cut, the liftoff from the stand, the
    // pole at 10 m/s, and a part driven through a rod faster than its compliance can stop it, which must log a tunnel.
    // Then the same on the test range (1.6b, RangeScenarios, which RangeContactTests asserts): resting and landing on the
    // plane, S04's soil and straw and E01's run, the field start's stand and a belly landing, and flights into its objects.
    // The bench times the step with the contacts busiest: resting on the floor, and on the stand.
    internal static class ContactsCase
    {
        public const string Name = "contacts";
        static bool _ok = true;

        static void Row(string name, bool pass, string detail, bool knownWrong = false)
        {
            bool good = knownWrong ? !pass : pass;
            _ok &= good;
            string verdict = knownWrong ? (pass ? "PASSES: the target cannot tell it from the model (FAIL)" : "fails, as it must") : pass ? "PASS" : "FAIL";
            Console.WriteLine($"  {name}: {detail}: {verdict}");
        }

        static string Land(ContactScenarios.Landing r) =>
            $"touch at {r.TouchSpeed:F3} m/s with the throttle held at {r.HeldThrottle:F3}; legs compressed {r.PeakCompression * 1e3:F2} mm at most, "
            + $"{r.PeakLegLoad:F1} N on a leg ({r.PeakLegOverWeight:F2} of the weight); "
            + (r.Separated ? $"left the ground at {r.LeaveSpeed:F3} m/s (restitution {r.Restitution:F2}) and rose {r.SeparationHeight * 1e3:F1} mm"
                : "never left the ground")
            + $"; last unloaded leg {r.LastUnloaded:F3} s after the touch, at rest {(double.IsNaN(r.Settled) ? "never" : $"{r.Settled:F3} s")} after it; "
            + $"{r.ContactEvents} contact events, {r.Tunnels} tunnels, {r.Crashes} crashes, {r.Lost} lost";

        public static bool Run()
        {
            _ok = true;
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            Console.WriteLine($"{Name}: drone.json 0x{spec.ContentHash:X16}, physics_materials.json 0x{hash:X16}; the stub's rigid floor, still air");

            Console.WriteLine("the landing (manifesto §2.6, ground-contact 'Hold the sticks and it leaves the ground'): down at 0.5 m/s, the sticks held from 0.1 m");
            ContactScenarios.Landing held = ContactScenarios.Land(spec, materials, hash, false);
            Row("throttle held", held.Separated && held.SeparationHeight > 0 && held.Tunnels == 0 && held.Crashes == 0 && held.Lost == 0, Land(held));
            ContactScenarios.Landing cut = ContactScenarios.Land(spec, materials, hash, true);
            Row("throttle cut at the touch", !double.IsNaN(cut.Settled) && cut.Settled <= 2 && cut.Tunnels == 0 && cut.Crashes == 0 && cut.Lost == 0, Land(cut));
            ContactScenarios.Landing glide = ContactScenarios.Land(spec, materials, hash, false, 0.5, 0.5);
            Console.WriteLine("  (information) the glide path, 0.5 m/s down and 0.5 m/s forward, held: " + Land(glide));
            ContactScenarios.Landing hard = ContactScenarios.Land(spec, materials, hash, true, 1.5);
            Console.WriteLine("  (information) a hard landing, 1.5 m/s down, cut: " + Land(hard));

            Console.WriteLine("liftoff from the stand (ground-contact 'Liftoff from the stand'), legs off, a collective ramp 0 to 0.75 over 3 s:");
            ContactScenarios.Liftoff lift = ContactScenarios.FromStand(spec, materials, hash);
            Row("off the four posts", lift.MadeSteel >= 4 && lift.Broken >= 4 && lift.BrokenSteel == lift.Broken && !double.IsNaN(lift.Time) && lift.ClimbAfter > 0.05
                && lift.Slide < 0.005 && lift.Tunnels == 0 && lift.Crashes == 0 && lift.Lost == 0,
                $"{lift.Made} post contacts made ({lift.MadeSteel} steel) and resting {lift.RestDepth * 1e6:F1} µm deep; {lift.Broken} broken in the ramp, "
                + $"{lift.BrokenSteel} steel; off the last post {lift.Time:F3} s into the ramp at throttle {lift.Throttle:F3}, ESC duty {lift.Duty * 100:F1} %, "
                + $"after sliding {lift.Slide * 1e3:F2} mm; {lift.ClimbAfter:F3} m over the posts a second later; {lift.Tunnels} tunnels, {lift.Crashes} crashes, "
                + "the stand static (W8)");

            Console.WriteLine("into a pole (ground-contact 'Into a pole'): 10 m/s at 1.5 m into the stub's copy of L01_straight:");
            ContactScenarios.PoleStrike pole = ContactScenarios.IntoPole(spec, materials, hash);
            Row("a crash, no tunnel", pole.Crashes >= 1 && pole.Object == "L01_straight" && pole.Material == "timber" && pole.Tunnels == 0 && pole.Lost == 0
                && pole.PeakAccel > 0 && pole.Impulse > 0,
                $"crash ({pole.Cause}) by '{pole.Part}' on '{pole.Object}' ({pole.Material}) at {pole.Speed:F2} m/s, flying {pole.FlightSpeed:F2} m/s at "
                + $"{pole.Height:F2} m; its impact (from '{pole.ImpactPart}'): peak {pole.PeakAccel:F0} m/s² ({pole.PeakAccel / 9.80665:F0} g), impulse "
                + $"{pole.Impulse:F2} N·s; touched by {string.Join(", ", pole.Touched)}; {pole.Tunnels} tunnels");

            Console.WriteLine("a tunnel is caught (K§7.5: logged as a bug), a free body with its contacts swept sideways into a 9 mm steel rod at its leg tubes:");
            int slow = ContactScenarios.Overdriven(spec, materials, hash, 2, out string slowDetail);
            Row("at 2 m/s the leg tube's 6.6 kN/m stops it", slow == 0, slowDetail);
            int fast = ContactScenarios.Overdriven(spec, materials, hash, 40, out string fastDetail);
            Row("at 40 m/s it goes through, and the log says so", fast >= 1, fastDetail);

            Range(spec, materials, hash);
            Soil(spec);
            WetGround(spec, materials);
            Layers(spec, materials);

            Console.WriteLine($"{Name}: {(_ok ? "PASS" : "FAIL")}");
            return _ok;
        }

        // p7a-ground 1.10: the soil under the half-skids with surfaces.json, its scenarios with their known-wrong models, and
        // the rest, liftoff and glide on the stub's floors (until the range's zones land, 1.3) and on the test range.
        static void Soil(DroneSpec spec)
        {
            SurfaceTable table = SoilScenarios.Table();
            Console.WriteLine($"the soil (p7a-ground 1.10): surfaces.json 0x{table.Hash:X16}; Bekker n = 1 at b = the tube's 10 mm, Wong's rule per column, the leg in series");

            Console.WriteLine("a resting drone sinks as the rows say (3 s disarmed; the sink at each half-skid's load centroid against the row's derived sink for its load):");
            foreach (SurfaceRow row in table.Surfaces)
            {
                if (row.Dry.Rigid) continue;
                string line = "";
                bool pass = true;
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
                {
                    SoilScenarios.Rest r = SoilScenarios.Resting(spec, SoilScenarios.Floor(row.Name), default, rain);
                    SoilSet set = row.Set(SurfaceTable.MoistureOf(rain));
                    double worst = 0, sink = 0, resid = 0;
                    for (int c = 0; c < 4; c++)
                    {
                        ContactModel.SkidSoil k = r.Skids[c];
                        double t = k.Centroid, j = 0.1025, L = 0.22;
                        double z = t <= j ? k.Sink0 + (k.Sink1 - k.Sink0) * t / j : k.Sink1 + (k.Sink2 - k.Sink1) * (t - j) / (L - j);
                        double m = t <= j ? k.Max0 + (k.Max1 - k.Max0) * t / j : k.Max1 + (k.Max2 - k.Max1) * (t - j) / (L - j);
                        double v = SoilScenarios.LevelSink(set, 0.005, L, k.Static, table.FirmLayer);
                        double derived = m > v ? m - (m - v) / set.UnloadRatio : v;
                        worst = Math.Max(worst, Math.Abs(z / derived - 1));
                        sink = Math.Max(sink, z);
                        resid = Math.Max(resid, Math.Abs(k.Residual));
                        pass &= k.Engaged;
                    }
                    pass &= worst < 0.02 && resid < 1e-9 && r.LateEvents == 0;
                    line += $"{rain} {sink * 1e3:F2} mm (within {worst * 100:F3} %, residual {resid:E0} N) ";
                }
                Row(row.Name, pass, line.TrimEnd());
            }
            SoilScenarios.Rest rigidRest = SoilScenarios.Resting(spec, SoilScenarios.Floor("belt_straw"), default, rigid: true);
            Row("known-wrong: the rigid ground with plain friction", rigidRest.Skids[0].Engaged, "no soil under the skids, no sink", knownWrong: true);

            Console.WriteLine("a sunk skid resists sideways more than along (belt_straw, the skids held 3 mm below the surface, pushed slowly):");
            var across = new Double3(1, 0, 0);
            var along = new Double3(0, 0, 1);
            SoilScenarios.Push pa = SoilScenarios.Drag(spec, SoilScenarios.Floor("belt_straw"), 0.003, across);
            SoilScenarios.Push pb = SoilScenarios.Drag(spec, SoilScenarios.Floor("belt_straw"), 0.003, along);
            double ratio = pa.Breakaway / pb.Breakaway, derivedRatio = pa.DerivedHold / pb.DerivedHold;
            Row("dry", Math.Abs(ratio / derivedRatio - 1) < 0.02 && Math.Abs(pa.Breakaway / pa.DerivedHold - 1) < 0.02 && Math.Abs(pb.Breakaway / pb.DerivedHold - 1) < 0.02,
                $"{pa.Sink * 1e3:F2} mm in, {pa.Normal:F2} N on the ground; across breaks at {pa.Breakaway:F3} N (μs·Fn + P, {pa.DerivedHold:F3}), slides at {pa.Sliding:F3} "
                + $"({pa.DerivedSlide:F3}); along {pb.Breakaway:F3} N ({pb.DerivedHold:F3}); across/along {ratio:F4} against {derivedRatio:F4}; "
                + $"the groove alone {(pa.DerivedHold - 0.5 * pa.Normal) / (pb.DerivedHold - 0.5 * pb.Normal):F2}x (L/2r = 22)");
            SoilScenarios.Push pw = SoilScenarios.Drag(spec, SoilScenarios.Floor("belt_straw"), 0.008, across, RainLevel.Rain);
            Row("in Rain, with the interface adhesion", Math.Abs(pw.Breakaway / pw.DerivedHold - 1) < 0.02,
                $"{pw.Sink * 1e3:F2} mm in, {pw.Normal:F2} N; across breaks at {pw.Breakaway:F3} N (μs·Fn + c_a·S_m + P, {pw.DerivedHold:F3}), slides at {pw.Sliding:F3} ({pw.DerivedSlide:F3})");
            double ra = SoilScenarios.Breakaway(spec, SoilScenarios.Floor("belt_straw"), 0.003, across, out double na, rigid: true);
            double rb = SoilScenarios.Breakaway(spec, SoilScenarios.Floor("belt_straw"), 0.003, along, out double nb, rigid: true);
            Row("known-wrong: the rigid ground with plain friction", Math.Abs(ra / rb / derivedRatio - 1) < 0.02,
                $"across {ra:F9} N, along {rb:F9} N, both μs·Fn ({0.5 * na:F9}): no groove, the ratio 1", knownWrong: true);

            Console.WriteLine("loose soil swallows a fast landing (1.5 m/s down, the throttle cut at the touch):");
            SoilScenarios.Landing spoil = SoilScenarios.Land(spec, SoilScenarios.Floor("crater_spoil"), default, 1.5);
            SoilScenarios.Landing track = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 1.5);
            Row("crater_spoil against dirt_track", spoil.PeakSink > track.PeakSink && spoil.Stop > track.Stop && spoil.PeakAccel < track.PeakAccel && spoil.Set > 0,
                $"sank {spoil.PeakSink * 1e3:F2} against {track.PeakSink * 1e3:F2} mm, stopped in {spoil.Stop * 1e3:F0} against {track.Stop * 1e3:F0} ms at {spoil.PeakAccel:F1} "
                + $"against {track.PeakAccel:F1} m/s², set {spoil.Set * 1e3:F2} and {track.Set * 1e3:F2} mm, rose {spoil.Rebound * 1e3:F1} and {track.Rebound * 1e3:F1} mm");
            SoilScenarios.Landing es = SoilScenarios.Land(spec, SoilScenarios.Floor("crater_spoil"), default, 1.5, table: SoilScenarios.With("crater_spoil", x => { x.UnloadRatio = 1; return x; }));
            SoilScenarios.Landing et = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 1.5, table: SoilScenarios.With("dirt_track", x => { x.UnloadRatio = 1; return x; }));
            Row("known-wrong: an elastic soil (unload ratio 1)", es.Set > 0 && et.Set > 0,
                $"set {es.Set * 1e3:F2} and {et.Set * 1e3:F2} mm, rose {es.Rebound * 1e3:F1} and {et.Rebound * 1e3:F1} mm: no set, more returned", knownWrong: true);

            Console.WriteLine("a re-landing finds its prints (crater_spoil, the pilot's 0.3 m/s landing, cut; again on the spot; then 0.3 m aside):");
            SoilScenarios.Relanding rl = SoilScenarios.Relandings(spec, SoilScenarios.Floor("crater_spoil"), default);
            Row("the prints", !rl.Print[0] && rl.Print[1] && !rl.Print[2] && rl.Yield[1] < rl.Yield[0] && Math.Abs(rl.Yield[2] / rl.Yield[0] - 1) < SoilScenarios.AsideShare,
                $"the soil yields {rl.Yield[0] * 1e3:F3} mm, then {rl.Yield[1] * 1e3:F3} mm from its print's floor {rl.Floor[1] * 1e3:F3} mm down, then {rl.Yield[2] * 1e3:F3} mm aside");

            Console.WriteLine("the glide landing (the pilot's 0.5 m/s down and 0.3 m/s forward, held to the touch, cut):");
            // Since model 46 the soil's catch is judged with the legs rigid across (SoilTests.TheGlideLandingPitchesFurtherOnSoftGround).
            Func<ContactSpec, ContactSpec> rigidAcross = LandingScenarios.RigidAcross();
            SoilScenarios.Landing gt = SoilScenarios.Land(spec, SoilScenarios.Floor("tilled"), default, 0.5, 0.3, legs: rigidAcross);
            SoilScenarios.Landing gd = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 0.5, 0.3, legs: rigidAcross);
            SoilScenarios.Landing rt = SoilScenarios.Land(spec, SoilScenarios.Floor("tilled"), default, 0.5, 0.3, rigid: true, legs: rigidAcross);
            SoilScenarios.Landing rd = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 0.5, 0.3, rigid: true, legs: rigidAcross);
            SoilScenarios.Landing st = SoilScenarios.Land(spec, SoilScenarios.Floor("tilled"), default, 0.5, 0.3);
            SoilScenarios.Landing sd = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 0.5, 0.3);
            Console.WriteLine($"  (information) on the legs' side give: tilled {st.NoseDown:F3}° ({st.PeakSink * 1e3:F2} mm in), dirt track {sd.NoseDown:F3}° "
                + $"({sd.PeakSink * 1e3:F2} mm): {(st.NoseDown > sd.NoseDown ? "tilled stumbles more on their lever too (model 46 reversed it, 0.173° against 0.407°)" : "the bending legs absorb the catch")}");
            Row("tilled stumbles more than the dirt track", gt.NoseDown > gd.NoseDown && gt.Crashes + gd.Crashes == 0,
                $"nose down {gt.NoseDown:F3}° ({gt.PeakSink * 1e3:F2} mm in) against {gd.NoseDown:F3}° ({gd.PeakSink * 1e3:F2} mm)");
            Row("known-wrong: the rigid ground with plain friction", Math.Abs(rt.NoseDown - rd.NoseDown) >= 0.5 * (gt.NoseDown - gd.NoseDown),
                $"{rt.NoseDown:F3}° and {rd.NoseDown:F3}°", knownWrong: true);

            // The range flights: rest and liftoff (design 13's ramp and held-leg rule) dry and in rain, four seeds with the
            // motors' asymmetry drawn.
            Console.WriteLine("rest and liftoff, design 13's held leg (a corner over 1 N 20 ms after the others, the drone turning 0.5° toward it), seeds 1-4:");
            var range = new FlightWorld(SoilScenarios.Range());
            var places = new (string Name, FlightWorld World, Double3 At)[]
            {
                ("the stub's dirt_track (the track's zone until 1.3)", SoilScenarios.Floor("dirt_track"), default),
                ("the range's track start (the plane's belt_straw)", range, SoilScenarios.TrackStart(range)),
                ("S04's patch (its straw mat over its soil box)", range, SoilScenarios.PatchCentre(range)),
            };
            foreach (var place in places)
            {
                double hover = SoilScenarios.HoverThrottle(spec, place.World, place.At);
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
                {
                    int held = 0, soil = 0, stuck = 0;
                    double hold = 0, sink = 0, pull = 0, down = 0;
                    for (ulong seed = 1; seed <= 4; seed++)
                    {
                        SoilScenarios.Liftoff l = SoilScenarios.Lift(spec, place.World, place.At, hover, rain, seed, true);
                        if (l.Held >= 0) held++;
                        if (l.Stuck > 0)
                        {
                            stuck++;
                            down = Math.Max(down, -l.EndLoad);
                        }
                        hold = Math.Max(hold, l.Hold);
                        pull = Math.Max(pull, l.Pull);
                        for (int c = 0; c < 4; c++)
                        {
                            sink = Math.Max(sink, l.Sink[c]);
                            if (l.Sink[c] > 0) soil++;
                        }
                    }
                    string detail = $"{place.Name}, {rain}: hover {hover:F3}, {soil} of 16 half-skids in soil at rest, the deepest {sink * 1e3:F2} mm; {held} of 4 held, "
                        + $"the last corner at most {hold * 1e3:F0} ms after the others; a corner pulled down by at most {pull:F2} N; {stuck} of 4 still down at the ramp's end"
                        + (stuck > 0 ? $", the ground pulling the drone down by up to {down:F2} N (all the thrust over the weight)" : "");
                    if (rain == RainLevel.Sunny && place.World != range) Row("no held leg on the dry track", held == 0, detail);
                    else Console.WriteLine("  (information) " + detail);
                }
            }
            Console.WriteLine("  (information) in rain the wet groove's cohesive pull (1.13) holds each half-skid by up to σ_a·S_m against the lift: 6.6 N wet and 11 N saturated"
                + " on a soft soil sunk past the tube's radius, four of them 26 and 44 N; where the four exceed the ramp's thrust over the weight the drone stays down"
                + " (design 13's protocol and σ_a are both assumed: 1.17 grades them). On S04 the wet straw clings as on one mat: the faces between its overlapping"
                + " tiles are buried in the mat and carry nothing (model 38). The dry track's \"sometimes\" needs the leg spread (1.17) and hooks (1.14).");
        }

        // p7a-ground 1.13: wet ground (design 7), its scenarios with their known-wrong models (WetGroundTests asserts them).
        static void WetGround(DroneSpec spec, MaterialRow[] materials)
        {
            const double R = 0.005, L = 0.22;
            SurfaceTable table = SoilScenarios.Table();
            Console.WriteLine("wet ground (p7a-ground 1.13): a wet groove's cohesive pull as its half-skid lifts, wet straw, the cover's wet sets");

            Console.WriteLine("a wet skid is held as it lifts (belt_straw, pressed to the weight, lifted at 10 mm/s; each corner's pull against σ_a·S_m, its work against ½·σ_a·δ_a·S_m):");
            foreach (RainLevel rain in new[] { RainLevel.Rain, RainLevel.SevereRain })
            {
                SoilSet set = Array.Find(table.Surfaces, x => x.Name == "belt_straw").Set(SurfaceTable.MoistureOf(rain));
                SoilScenarios.Unstick u = SoilScenarios.Unsticking(spec, SoilScenarios.Floor("belt_straw"), default, rain);
                Row(rain.ToString(), Held(u, set.Adhesion, set.AdhesionRelease, 2 * R * L, out string detail), detail);
            }
            SoilScenarios.Unstick dry = SoilScenarios.Unsticking(spec, SoilScenarios.Floor("belt_straw"), default, RainLevel.Sunny);
            Row("dry: no pull", Math.Max(Math.Max(dry.Peak[0], dry.Peak[1]), Math.Max(dry.Peak[2], dry.Peak[3])) == 0, "the pull at most 0 N");
            SoilScenarios.Unstick wrong = SoilScenarios.Unsticking(spec, SoilScenarios.Floor("belt_straw"), default, RainLevel.Rain,
                SoilScenarios.With("belt_straw", x => { x.Adhesion = 0; return x; }));
            double target = Array.Find(table.Surfaces, x => x.Name == "belt_straw").Wet.Adhesion * 2 * R * L;
            Row("known-wrong: 1.10's wet soil, no cohesive pull (c_a along the ground only)", Math.Abs(wrong.Peak[0] / target - 1) < 0.01,
                $"the pull peaks at {wrong.Peak[0]:F3} N against σ_a·S_m {target:F3}", knownWrong: true);

            Console.WriteLine("wet straw (a level straw mat, S04's row and depth; pressed to the weight, lifted at 10 mm/s):");
            MaterialRow straw = Array.Find(materials, m => m.Name == "straw");
            Double3 top = new Double3(0, SoilScenarios.MatDepth, 0);
            SoilScenarios.Unstick dryMat = SoilScenarios.Unsticking(spec, SoilScenarios.MatFloor(), top, RainLevel.Sunny, place: SoilScenarios.PlaceMat);
            foreach (RainLevel rain in new[] { RainLevel.Rain, RainLevel.SevereRain })
            {
                SoilScenarios.Unstick u = SoilScenarios.Unsticking(spec, SoilScenarios.MatFloor(), top, rain, place: SoilScenarios.PlaceMat);
                bool pass = Held(u, straw.WetAdhesion, straw.WetAdhesionRelease, 2 * R * L, out string detail);
                double kw = u.PressLoad[0] / u.Press[0], kd = dryMat.PressLoad[0] / dryMat.Press[0];
                pass &= u.Press[0] > dryMat.Press[0] && kw < kd;
                Row(rain.ToString(), pass, $"{u.Press[0] * 1e3:F2} mm in at the weight against dry {dryMat.Press[0] * 1e3:F2}, a corner's {kw:F1} N/m against {kd:F1}; " + detail);
            }
            Row("dry straw: no cling", Math.Max(Math.Max(dryMat.Peak[0], dryMat.Peak[1]), Math.Max(dryMat.Peak[2], dryMat.Peak[3])) == 0, "the pull at most 0 N");
            // The same lift on S04's own tiles on the range, where the skids cross overlapping tiles (model 38's buried face).
            var range = new FlightWorld(SoilScenarios.Range());
            foreach (RainLevel rain in new[] { RainLevel.Rain, RainLevel.SevereRain })
            {
                SoilScenarios.Unstick u = SoilScenarios.Unsticking(spec, range, RangeScenarios.Straw, rain);
                Row("S04's overlapping tiles, " + rain, Held(u, straw.WetAdhesion, straw.WetAdhesionRelease, 2 * R * L, out string detail) && u.EndForce.Length() == 0,
                    detail + $"; {u.EndForce.Length():F3} N on the drone once it is off");
            }

            Console.WriteLine("rain softens the ground (3 s disarmed on each soil; the T-joints' sinks dry / Rain / Severe rain):");
            foreach (string name in new[] { "dirt_track", "belt_straw", "tilled", "crater_spoil" })
            {
                double last = 0;
                bool pass = true;
                string line = "";
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
                {
                    SoilScenarios.Rest r = SoilScenarios.Resting(spec, SoilScenarios.Floor(name), default, rain);
                    double sink = Math.Max(Math.Max(r.Skids[0].Sink1, r.Skids[1].Sink1), Math.Max(r.Skids[2].Sink1, r.Skids[3].Sink1));
                    pass &= sink > last * 1.01;
                    line += $"{sink * 1e3:F3} ";
                    last = sink;
                }
                Row(name, pass, line.TrimEnd() + " mm");
            }

            Console.WriteLine("the cover's wet sets (the elements read them, 1.14 and 1.15):");
            CoverSet cd = table.Cover[(int)CoverKind.Straw].Dry, cw = table.Cover[(int)CoverKind.Straw].Wet;
            Row("straw", cw.HookReleaseMax > cd.HookReleaseMax && cw.CutEnergy > cd.CutEnergy,
                $"hook release {cd.HookReleaseMin:F2}–{cd.HookReleaseMax:F2} N dry, {cw.HookReleaseMin:F2}–{cw.HookReleaseMax:F2} wet; cut energy {cd.CutEnergy:F0} J/m² dry, {cw.CutEnergy:F0} wet");
        }

        // p7a-ground 1.12: compliant layers (design 6), the area laws and the stroke with their known-wrong models (LayerTests
        // asserts them).
        static void Layers(DroneSpec spec, MaterialRow[] materials)
        {
            MaterialRow straw = Array.Find(materials, m => m.Name == "straw"), fabric = Array.Find(materials, m => m.Name == "fabric");
            Console.WriteLine("compliant layers (p7a-ground 1.12): a layer row meets each part by its footprint, to 80 % of its thickness, then carries it onto what is beneath");

            Console.WriteLine("a skid meets the straw by its area (pressed level into S04's 0.125 m straw tile on the range to the weight; a corner's load over the straw's compression):");
            var range = new FlightWorld(RangeScenarios.World);
            LayerScenarios.Press skid = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range), LayerScenarios.Tile, 1);
            LayerScenarios.Press foot = LayerScenarios.Pressing(LayerScenarios.ReferenceFeet(LayerScenarios.Setup(spec, range)), LayerScenarios.Tile, 1);
            LayerScenarios.Press bare = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range, without: "straw"), LayerScenarios.Tile, 1);
            double ks = skid.Load[0] / skid.Compression[0], kf = foot.Load[0] / foot.Compression[0], kw = bare.Load[0] / bare.Compression[0];
            Row("the half-skid against the 15 mm reference foot", Math.Abs(ks / kf / 3.03 - 1) < 0.01 && Math.Abs(kf / straw.Stiffness - 1) < 1e-9,
                $"{ks:F2} N/m against {kf:F2} ({ks / kf:F4} ×, the table's 3.03), pressed {skid.Overlap[0] * 1e3:F2} mm at {skid.Load[0]:F2} N");
            Row("known-wrong: the straw row without its area law", Math.Abs(kw / kf / 3.03 - 1) < 0.01,
                $"{kw:F2} N/m ({kw / kf:F4} ×), pressed {bare.Overlap[0] * 1e3:F2} mm, {bare.Overlap[0] / skid.Overlap[0]:F3} × deeper", knownWrong: true);

            Console.WriteLine("a hard landing on a mattress stops at its stroke (pressed level past each layer's stroke load):");
            var top = new Double3(0, LayerScenarios.FabricDepth, 0);
            LayerScenarios.Press mattress = LayerScenarios.Pressing(LayerScenarios.Setup(spec, LayerScenarios.MattressFloor()), top, 10, place: LayerScenarios.PlaceMattress);
            LayerScenarios.Press loose = LayerScenarios.Pressing(LayerScenarios.Setup(spec, LayerScenarios.MattressFloor()), top, 10, double.PositiveInfinity,
                LayerScenarios.PlaceMattress);
            double fs = Layer.StrokeShare * LayerScenarios.FabricDepth, kfab = LayerScenarios.HalfSkid(fabric, LayerScenarios.FabricDepth);
            Row("0.15 m of fabric over the rigid floor, 10 g", Math.Abs(mattress.Compression[0] - fs) < 1e-9 && mattress.Load[0] > kfab * fs && mattress.Allocated == 0,
                $"a corner's {mattress.Load[0]:F1} N (the stroke at {kfab * fs:F1}): the fabric {mattress.Compression[0] * 1e3:F3} mm, the leg {mattress.Deflection[0] * 1e3:F2} mm");
            // Since model 64 the canister's belly takes a share of the press, and the known-wrong's fabric stops short of the
            // 150 mm box (143 mm): it is judged against the target, the 120 mm stroke (LayerTests).
            Row("known-wrong: no stroke limit", loose.Compression[0] <= fs + 0.01,
                $"the fabric {loose.Compression[0] * 1e3:F1} mm, past its {fs * 1e3:F0} mm stroke (the box {LayerScenarios.FabricDepth * 1e3:F0} mm): the skid "
                + $"{(loose.Overlap[0] - LayerScenarios.FabricDepth) * 1e3:F1} mm into the floor",
                knownWrong: true);
            SurfaceTable table = SoilScenarios.Table();
            LayerScenarios.Press mat = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range, table), LayerScenarios.Tile, 20);
            LayerScenarios.Press through = LayerScenarios.Pressing(LayerScenarios.Setup(spec, range, table), LayerScenarios.Tile, 20, double.PositiveInfinity);
            double ss = Layer.StrokeShare * SoilScenarios.MatDepth, kst = LayerScenarios.HalfSkid(straw, SoilScenarios.MatDepth);
            ContactModel.SkidSoil sk = mat.Soil[0];
            Row("S04's 0.125 m straw over its soil box, 20 g", sk.Engaged && Math.Abs(mat.Compression[0] - sk.Sink1 - ss) < 1e-9 && Math.Abs((mat.Load[0] - sk.Static) / (kst * ss) - 1) < 1e-8
                && Math.Abs(sk.Residual) < 1e-9 && mat.Allocated == 0,
                $"a corner's {mat.Load[0]:F1} N (the stroke at {kst * ss:F1}): the straw {(mat.Compression[0] - sk.Sink1) * 1e3:F3} mm carrying {mat.Load[0] - sk.Static:F2} N, the soil "
                + $"under it {sk.Sink1 * 1e3:F3} mm carrying {sk.Static:F2} N by its law (residual {sk.Residual:E1} N)");
            Row("known-wrong: no stroke limit", through.Compression[0] <= SoilScenarios.MatDepth,
                $"the straw {through.Compression[0] * 1e3:F1} mm, past its {SoilScenarios.MatDepth * 1e3:F0} mm", knownWrong: true);

            Console.WriteLine("a rest on wet straw stays level (the range golden's flight on S04's straw with surfaces.json; its roll at the end):");
            MaterialRow[] without = (MaterialRow[])materials.Clone();
            for (int i = 0; i < without.Length; i++)
                if (without[i].Name == "straw") without[i].AreaLaw = AreaLaw.None;
            foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain })
            {
                ulong h = GoldenFlights.FlyRange(spec, out FlightCore core, rain: rain, table: table);
                double roll = Roll(core.State);
                Row(rain.ToString(), Math.Abs(roll) < 2 && !core.State.Crashed, $"0x{h:X16}, rests rolled {roll:F3}°, {core.Contacts.ImpactCount} impacts");
            }
            ulong hw = GoldenFlights.FlyRange(spec, out FlightCore wrong, rain: RainLevel.Rain, table: table, materials: without);
            Row("known-wrong: Rain, the straw row without its area law", Math.Abs(Roll(wrong.State)) < 2, $"0x{hw:X16}, rests rolled {Roll(wrong.State):F3}°",
                knownWrong: true);
        }

        static double Roll(in FlightState s) => Math.Asin(s.Attitude.Rotate(new Double3(1, 0, 0)).Y) * 57.29577951308232;

        // A lift's pull against σ_a·S, its release by δ_a and its work against ½·σ_a·δ_a·S, every corner.
        static bool Held(SoilScenarios.Unstick u, double sigma, double release, double footprint, out string detail)
        {
            bool pass = u.Allocated == 0;
            double worstPeak = 0, worstWork = 0, released = double.PositiveInfinity, peak = 0;
            for (int c = 0; c < 4; c++)
            {
                double p = u.Peak[c] / (sigma * footprint) - 1, w = u.Work[c] / (0.5 * sigma * release * footprint) - 1;
                if (Math.Abs(p) > Math.Abs(worstPeak)) worstPeak = p;
                if (Math.Abs(w) > Math.Abs(worstWork)) worstWork = w;
                released = Math.Min(released, u.Released[c]);
                peak = Math.Max(peak, u.Peak[c]);
                pass &= Math.Abs(p) < 0.01 && Math.Abs(w) < 0.01 && u.Released[c] < release && u.Released[c] > release - 5e-5 && !u.Holding[c];
            }
            detail = $"the pull peaks at {peak:F4} N (σ_a·S_m {sigma * footprint:F4}, worst {worstPeak * 100:F3} %), last acts {released * 1e3:F4} mm past the set "
                + $"(δ_a {release * 1e3:F1}), work within {worstWork * 100:F3} % of ½·σ_a·δ_a·S_m {0.5 * sigma * release * footprint * 1e3:F3} mJ; {u.Allocated} B allocated";
            return pass;
        }

        static string Rest(RangeScenarios.Resting r) =>
            $"sank {r.Sink * 1e3:F2} mm, legs {r.LegLoad[0]:F1}/{r.LegLoad[1]:F1}/{r.LegLoad[2]:F1}/{r.LegLoad[3]:F1} N ({r.LoadOverWeight:F3} of the weight), "
            + $"{r.Speed * 1e3:F3} mm/s; {r.Pairs.Count} open pairs: {r.Plane} plane, {r.Soil} soil, {r.Material} material, {r.PlaneInHole} plane over a hole; "
            + $"{r.HoleSupports} supports over Hole; {r.LateEvents} contact events in the last second; {r.Tunnels} tunnels, {r.Crashes} crashes, {r.Lost} lost";

        static string First(ContactScenarios.Landing l)
        {
            IWorldQuery q = l.Core.World.Query;
            FlightEvent e = l.First;
            return $"; first '{l.Core.Contacts.Spec.Shapes[e.Index].Name}' on {(e.Object == WorldIds.Ground ? "the plane" : q.ObjectName(e.Object) + "#" + e.Object + "/" + e.Shape)} "
                + $"({(e.Surface != 0 ? "surface " + q.SurfaceName(e.Surface) : q.MaterialName(e.Material))}) at y {e.Point.Y:F4}";
        }

        static string Strike(ContactScenarios.PoleStrike p) =>
            (p.Touch ? $"first touch '{p.FirstPart}' on '{p.FirstObject}' ({p.FirstMaterial}) by {p.First.Kind} at {p.First.A:F2} m/s, flying {p.FirstFlightSpeed:F2} m/s; " : "touched nothing; ")
            + (p.Crashes > 0 ? $"crash ({p.Cause}) by '{p.Part}' on '{p.Object}' ({p.Material}) at {p.Speed:F2} m/s, flying {p.FlightSpeed:F2} m/s at {p.Height:F2} m; "
                + $"impact (from '{p.ImpactPart}') peak {p.PeakAccel:F0} m/s² ({p.PeakAccel / 9.80665:F0} g), impulse {p.Impulse:F2} N·s; " : "no crash; ")
            + $"touched by {string.Join(", ", p.Touched)}; {p.Tunnels} tunnels{(p.Tunnels > 0 ? " (" + string.Join(", ", p.TunnelThrough) + ")" : "")}, {p.Lost} lost";

        /// <summary>The ground-contact scenarios on the test range (1.6b, D-044): MapWorld with its start points, patches and objects.</summary>
        static void Range(DroneSpec spec, MaterialRow[] materials, ulong hash)
        {
            var load = Stopwatch.StartNew();
            MapWorld world = RangeScenarios.World;
            Console.WriteLine($"the test range (D-047): MapWorld 0x{world.ContentHash:X16}, query version {world.QueryVersion}, {world.PlacedObjects} objects, "
                + $"loaded in {load.Elapsed.TotalSeconds:F2} s; still air");
            StartPoint track = RangeScenarios.Start("track"), field = RangeScenarios.Start("field");

            Console.WriteLine("resting (legs on, disarmed, 3 s on the skids):");
            foreach (var (name, site) in new[] { ("the track start", RangeScenarios.At(track)), ("S04's straw", RangeScenarios.At(RangeScenarios.Straw, 0)),
                ("S04's bare soil", RangeScenarios.At(RangeScenarios.Soil, 0)), ("E01's run", RangeScenarios.At(RangeScenarios.Run, RangeScenarios.RunHeading)) })
            {
                RangeScenarios.Resting r = RangeScenarios.Rest(spec, materials, hash, site);
                Row(name, r.Tunnels == 0 && r.Crashes == 0 && r.Lost == 0, Rest(r) + "; " + string.Join("; ", r.Pairs));
            }

            Console.WriteLine("the landing (down at 0.5 m/s, the sticks held from 0.1 m):");
            foreach (var (name, site) in new[] { ("the track start", RangeScenarios.At(track)), ("S04's straw", RangeScenarios.At(RangeScenarios.Straw, 0)),
                ("S04's bare soil", RangeScenarios.At(RangeScenarios.Soil, 0)), ("E01's run", RangeScenarios.At(RangeScenarios.Run, RangeScenarios.RunHeading)) })
            {
                ContactScenarios.Landing held = ContactScenarios.Land(spec, materials, hash, false, site: site);
                Row(name + ", throttle held", held.Tunnels == 0 && held.Crashes == 0 && held.Lost == 0, Land(held) + First(held));
                ContactScenarios.Landing cut = ContactScenarios.Land(spec, materials, hash, true, site: site);
                Row(name + ", throttle cut", cut.Tunnels == 0 && cut.Crashes == 0 && cut.Lost == 0, Land(cut) + First(cut));
            }

            Console.WriteLine("the field start (legs off, the stand):");
            ContactScenarios.Liftoff lift = ContactScenarios.FromStand(spec, materials, hash, site: RangeScenarios.At(field));
            Row("off the four posts", lift.Tunnels == 0 && lift.Crashes == 0 && lift.Lost == 0,
                $"{lift.Made} post contacts made ({lift.MadeSteel} steel) and resting {lift.RestDepth * 1e6:F1} µm deep; {lift.Broken} broken in the ramp, "
                + $"{lift.BrokenSteel} steel; off the last post {lift.Time:F3} s into the ramp at throttle {lift.Throttle:F3}, ESC duty {lift.Duty * 100:F1} %, "
                + $"after sliding {lift.Slide * 1e3:F2} mm; {lift.ClimbAfter:F3} m over the posts a second later; {lift.Tunnels} tunnels, {lift.Crashes} crashes");
            RangeScenarios.Belly belly = RangeScenarios.BellyLanding(spec, materials, hash, RangeScenarios.At(field));
            Row("a belly landing 3 m off the stand, cut at the touch", belly.Tunnels == 0 && belly.Crashes == 0 && belly.Lost == 0,
                $"first '{belly.FirstPart}' at {belly.TouchSpeed:F3} m/s, peak {belly.PeakAccel / 9.80665:F1} g, at rest {(double.IsNaN(belly.Settled) ? "never" : $"{belly.Settled:F3} s")} after it "
                + $"on {string.Join(", ", belly.Resting)}; {belly.Tunnels} tunnels, {belly.Crashes} crashes");

            Console.WriteLine("flights into the range's objects:");
            foreach (RangeScenarios.Approach a in RangeScenarios.Approaches)
            {
                ContactScenarios.PoleStrike p = RangeScenarios.Into(spec, materials, hash, a);
Row($"{a.Name} ({a.Variant}, {(a.ClimbRate > 0 ? $"climbing at {a.ClimbRate} m/s" : $"{a.Speed} m/s at {a.Height} m")})", p.Tunnels == 0 && p.Lost == 0, Strike(p));
            }
            RangeScenarios.OnWire wire = RangeScenarios.OntoWire(spec, materials, hash);
            Row("a skid across a conductor at 2 m/s", wire.Tunnels == 0 && wire.Lost == 0,
                $"first '{wire.FirstShape}' at {wire.TouchSpeed:F3} m/s; touched by {string.Join(", ", wire.Shapes)}; peak leg {wire.PeakLegLoad:F1} N; "
                + $"{wire.After:F3} m over the conductor 1.5 s later; {wire.Strikes} prop strikes, {wire.Crashes} crashes, {wire.Tunnels} tunnels");

            // The two laws with the airframe's effects on, as RangeContactTests' WithTheAirframesEffects… assert them.
            Console.WriteLine("with the airframe's effects on (p7b-airframe: the motors' asymmetry, the vortex rings, the ground effect, the flapping, the modes):");
            ContactScenarios.Site stub = default;
            stub.Airframe = true;
            ContactScenarios.Landing stubHeld = ContactScenarios.Land(spec, materials, hash, false, site: stub);
            ContactScenarios.Landing stubCut = ContactScenarios.Land(spec, materials, hash, true, site: stub);
            Row("the stub's floor, throttle held", stubHeld.Separated && stubHeld.Tunnels == 0 && stubHeld.Crashes == 0 && stubHeld.Lost == 0, Land(stubHeld));
            Row("the stub's floor, throttle cut", !double.IsNaN(stubCut.Settled) && stubCut.Tunnels == 0 && stubCut.Crashes == 0 && stubCut.Lost == 0, Land(stubCut));
            foreach (var (name, at) in new[] { ("S04's bare soil", RangeScenarios.At(RangeScenarios.Soil, 0)), ("E01's run", RangeScenarios.At(RangeScenarios.Run, RangeScenarios.RunHeading)) })
            {
                ContactScenarios.Site site = at;
                site.Airframe = true;
                RangeScenarios.Resting r = RangeScenarios.Rest(spec, materials, hash, site);
                Row(name + ", resting, each support once", r.Soil == r.Pairs.Count && r.Pairs.Count == 8 && Math.Abs(r.LoadOverWeight - 1) < 1e-3 && r.LateEvents == 0
                    && r.Tunnels == 0 && r.Crashes == 0 && r.Lost == 0, Rest(r));
                ContactScenarios.Landing held = ContactScenarios.Land(spec, materials, hash, false, site: site);
                Row(name + ", throttle held, as the stub's", Math.Abs(held.SeparationHeight - stubHeld.SeparationHeight) <= 1e-3 && held.Tunnels == 0 && held.Crashes == 0
                    && held.Lost == 0, Land(held) + First(held));
                ContactScenarios.Landing cut = ContactScenarios.Land(spec, materials, hash, true, site: site);
                // Since model 46 the rest's time is the idle rotors' forcing on the soft legs (RangeContactTests'
                // WithTheAirframesEffectsSoilIsStillThePlane): both rest within 2.5 s at the stub's height.
                Row(name + ", throttle cut, as the stub's", !double.IsNaN(cut.Settled) && cut.Settled <= 2.5 && !double.IsNaN(stubCut.Settled)
                    && Math.Abs(cut.Core.State.Position.Y - at.Foot.Y - stubCut.Core.State.Position.Y) <= 1e-3 && cut.Tunnels == 0 && cut.Crashes == 0 && cut.Lost == 0,
                    Land(cut) + First(cut));
            }
            ContactScenarios.Site straw = RangeScenarios.At(RangeScenarios.Straw, 0);
            straw.Airframe = true;
            foreach (bool c in new[] { false, true })
            {
                ContactScenarios.Landing l = ContactScenarios.Land(spec, materials, hash, c, site: straw);
                Row($"S04's straw, throttle {(c ? "cut" : "held")}, no tunnel through the mat", l.Tunnels == 0 && l.Crashes == 0 && l.Lost == 0, Land(l) + First(l));
            }
            ContactScenarios.PoleStrike crown = RangeScenarios.Into(spec, materials, hash, RangeScenarios.Find("crown"), true);
            Row("the crown (V01_L, 10 m/s at 10 m), no tunnel through its twigs", crown.Tunnels == 0 && crown.Lost == 0, Strike(crown));
            FarmlandCrowns(spec, materials, hash);
        }

        /// <summary>The farmland's tree families (w4a-farmland-vegetation 1.1), one variant each, with the material its crown is
        /// felt by first: the twig shell over the drawn foliage, or the dead pine's bare deadwood (it draws no foliage).</summary>
        static readonly (string Asset, string Variant, string Felt)[] FarmlandTrees =
        {
            ("VF01", "IslandTree", "twig"), ("VF02", "Pine002_M", "twig"), ("VF03", "Pine004_1", "twig"), ("VF04", "Pine006_01", "twig"),
            ("VF05", "PineDead_02", "deadwood"),
        };

        /// <summary>
        /// w4a-farmland-vegetation's crown rows: each farmland tree family's metadata alone on a meadow plane at the origin (a world
        /// of one placement, as the map export will place it), the legs-on drone 35 m south of it with the airframe's effects on, held
        /// at the middle height of the crown's wind box and flown north at 10 m/s into the crown, as the test range's crown row flies
        /// V01_L. A row passes when the drone's first touch is that tree in its crown's material (the crown is felt: its twig shell,
        /// with twig contacts after it, or a dead pine's bare deadwood; a first touch of the trunk is not the crown, w4a review E4b)
        /// with no tunnel and no contact pair lost (ContactsLost: more than PairsPerShape at once); it prints the crown's work: its
        /// twig contacts and the impulse they took, and the prop strikes with what struck them.
        /// </summary>
        static void FarmlandCrowns(DroneSpec spec, MaterialRow[] materials, ulong hash)
        {
            string root = RangeScenarios.Root;
            string[] names = MapWorld.MaterialNames(System.IO.File.ReadAllText(System.IO.Path.Combine(root, MapWorld.MaterialsFile)));
            foreach ((string asset, string variant, string felt) in FarmlandTrees)
            {
                string model = "vegetation/" + asset + "/" + variant + ".assetmeta.json";
                string text = System.IO.File.ReadAllText(System.IO.Path.Combine(root, MapWorld.ModelsRoot, model));
                double height;
                using (var doc = System.Text.Json.JsonDocument.Parse(text))
                    height = doc.RootElement.GetProperty("wind_volumes")[0].GetProperty("center")[1].GetDouble();
                string placements = "{\"format\": \"fpv450-placements\", \"format_version\": 1, \"map\": \"w4a-crown\", \"frame\": \"unity-lh-yup\", "
                    + "\"extent_m\": [-64, -64, 64, 64], \"ground\": {\"plane\": {\"y_m\": 0, \"surface\": \"meadow_sod\"}, \"patches\": [], \"holes\": []}, "
                    + "\"instances\": [{\"id\": 0, \"asset\": \"" + asset + "\", \"variant\": \"" + variant
                    + "\", \"class\": \"vegetation\", \"position_m\": [0, 0, 0], \"yaw_deg\": 0}]}";
                var world = new MapWorld(placements, m => System.IO.File.ReadAllText(System.IO.Path.Combine(root, MapWorld.ModelsRoot, m)), names);
                var site = new ContactScenarios.Site(new FlightWorld(world), new Double3(0, 0, -35), 0) { Airframe = true };
                var events = new System.Collections.Generic.List<FlightEvent>();
                ContactScenarios.PoleStrike p = ContactScenarios.Into(spec, materials, hash, site, height, 10, _ => 0, null, 6000, 0, events);
                ushort twig = world.MaterialId("twig");
                int twigContacts = 0, strikes = 0;
                double impulse = 0;
                var struck = new System.Collections.Generic.SortedSet<string>(StringComparer.Ordinal);
                foreach (FlightEvent e in events)
                {
                    if (e.Kind == EventKind.ContactMade && e.Material == twig) twigContacts++;
                    if (e.Kind == EventKind.ContactBroken && e.Material == twig) impulse += e.B;
                    if (e.Kind == EventKind.PropStrike) { strikes++; struck.Add(e.Material > 0 ? names[e.Material - 1] : "ground"); }
                }
                Row($"the farmland crown ({variant}, 10 m/s at {height:F2} m), felt first by its {felt}, no tunnel, no pair lost",
                    p.Touch && p.FirstObject == variant && p.FirstMaterial == felt && (felt != "twig" || twigContacts > 0) && p.Tunnels == 0 && p.Lost == 0,
                    $"{twigContacts} twig contacts taking {impulse:F2} N·s, {strikes} prop strikes ({(strikes > 0 ? string.Join(", ", struck) : "none")}); " + Strike(p));
            }
        }

        /// <summary>The step with its contacts busiest: legs on resting on the floor (eight supports and their friction), and
        /// legs off resting on the stand (eight arm edges on four posts), armed at idle; 60 000 steps each. Then the same on
        /// the test range (MapWorld) with the airframe's effects on, as a pilot's flight has them: legs on resting in S04's
        /// straw, and the field start's stand through its launch (FromStand's flight: armed on the posts, the collective ramp,
        /// the climb), 12 flights of 5016 steps from the arming.</summary>
        public static bool Bench()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            bool ok = true;
            Func<FlightCore, int, StickSample> idle = (c, k) => new StickSample { HostUs = c.State.Step * FlightCore.StepUs, Arm = k >= 8, Throttle = 0 };
            foreach (bool legs in new[] { true, false })
            {
                Loadout loadout = spec.Nominal;
                loadout.Legs = legs;
                ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
                Double3 start = legs ? new Double3(0, ContactScenarios.Standing(contacts), 0) : LaunchStand.RestingOrigin(spec, contacts, default);
                FlightSetup Make()
                {
                    FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, legs, start);
                    if (!legs) setup.RuntimeObjects = new[] { LaunchStand.Build(spec, default, 0, setup.World.Query.MaterialId(spec.Stand.Material)) };
                    setup.GroundEffect = spec.BuildGroundEffect(); // p7b-airframe 1.3: the rotors' rays and ground samples on top of the contacts
                    setup.Flapping = spec.BuildFlapping(); // and 1.3c's flapping
                    return setup;
                }
                ok &= Timed(legs ? "legs on, resting on the floor, armed at idle" : "legs off, resting on the stand, armed at idle", contacts.Shapes.Length, Make, idle, 2000, 60000, 1);
                if (!legs) continue;
                // p7g-legs 1.4 (design 7): the same row with the legs' law off (the elastic legs), beside it.
                FlightSetup Elastic()
                {
                    FlightSetup setup = Make();
                    setup.Contacts = setup.Contacts.Clone();
                    setup.Contacts.Legs = null;
                    return setup;
                }
                ok &= Timed("legs on, resting on the floor, armed at idle, the legs' law off", contacts.Shapes.Length, Elastic, idle, 2000, 60000, 1);
            }

            // p7g-legs 1.4: the law through its failures, armed at idle: down at 1.5 m/s rolled 20° with 0.6 m/s of drift onto the
            // floor (LegScenarios' touch, seed 1; --legs' cell parts a leg on every seed), so the legs crack, set and part and their
            // pieces fly loose; and the same touch with the law off (the elastic legs) and with the lever off (1.3's softening).
            {
                var t = new LegScenarios.Touch { RollDeg = 20, Velocity = new Double3(0.6, -1.5, 0), Seed = 1 };
                FlightSetup Touch(Func<ContactSpec, ContactSpec> edit)
                {
                    LegScenarios.Touch e = t;
                    e.Edit = edit;
                    FlightSetup free = LegScenarios.Setup(spec, materials, hash, e);
                    FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, true, free.StartPosition, free.Seed);
                    setup.Contacts = free.Contacts;
                    setup.StartAttitude = free.StartAttitude;
                    setup.StartVelocity = free.StartVelocity;
                    return setup;
                }
                foreach ((string label, Func<ContactSpec, ContactSpec> edit) in new (string, Func<ContactSpec, ContactSpec>)[]
                {
                    ("legs on, a rolled drifting touch that bends and breaks legs, armed at idle, the legs' law on", null),
                    ("the same touch, the legs' law off", c => { c = c.Clone(); c.Legs = null; return c; }),
                    ("the same touch, the law on, the lever off (1.3's softening)", c => { c = c.Clone(); c.LegLeverLength = 0; return c; }),
                })
                {
                    FlightSetup Make() => Touch(edit);
                    var probe = new FlightCore(Make());
                    for (int k = 0; k < 3000; k++) probe.Advance(idle(probe, k));
                    int damaged = 0;
                    for (int c = 0; c < 4; c++) if ((byte)(probe.Trace.LegState >> (8 * c)) != 0) damaged++;
                    Console.WriteLine($"{Name}: {label}: after 3 s {damaged} legs damaged, {probe.Loose?.Count ?? 0} pieces loose");
                    ok &= Timed(label, Make().Contacts.Shapes.Length, Make, idle, 5, 3000, 8);
                }
            }

            MapWorld world = RangeScenarios.World;
            Console.WriteLine($"{Name}: the test range, MapWorld 0x{world.ContentHash:X16} ({world.PlacedObjects} objects), the airframe's effects on:");
            {
                ContactScenarios.Site straw = RangeScenarios.At(RangeScenarios.Straw, 0);
                straw.Airframe = true;
                ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, hash);
                Double3 start = straw.Foot + new Double3(0, ContactScenarios.Standing(contacts), 0);
                FlightSetup Make()
                {
                    FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, true, start);
                    setup.World = new FlightWorld(world);
                    return straw.Effects(setup, spec);
                }
                ok &= Timed("legs on, resting in S04's straw, armed at idle", contacts.Shapes.Length, Make, idle, 2000, 60000, 1);
            }
            {
                ContactScenarios.Site field = RangeScenarios.At(RangeScenarios.Start("field"));
                field.Airframe = true;
                Loadout off = spec.Nominal;
                off.Legs = false;
                ContactSpec contacts = spec.BuildContacts(off, materials, hash);
                FlightSetup Make()
                {
                    FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, false, LaunchStand.RestingOrigin(spec, contacts, field.Foot));
                    setup.World = new FlightWorld(world);
                    setup.StartAttitude = field.Attitude();
                    setup.RuntimeObjects = new[] { LaunchStand.Build(spec, field.Foot, field.Heading, world.MaterialId(spec.Stand.Material)) };
                    return field.Effects(setup, spec);
                }
                // FromStand's sticks: armed at 1008, the collective from 0 at 1016 to 0.75 over 3 s, held 2 s.
                Func<FlightCore, int, StickSample> launch = (c, k) => new StickSample
                {
                    HostUs = c.State.Step * FlightCore.StepUs, Arm = k >= 1008, Throttle = k < 1016 ? 0 : DetMath.Min(0.75, 0.75 * (k - 1016) / 3000.0),
                };
                ok &= Timed("legs off, the field start's stand: armed on the posts, the launch and the climb", contacts.Shapes.Length, Make, launch, 1000, 5016, 12);
            }
            return ok;
        }

        /// <summary>
        /// Flies repeats fresh flights of make() for warmup + steps each and times the last steps of each: the step's p50 and
        /// p99 over them all and its allocation in them. Then the same flights again with the world's queries timed
        /// (TimedQuery): their share of the step, their calls a step, and their time in a step at p99 and at most (PLT-04's
        /// 60 µs line). Passes on p99 ≤ 300 µs, 0 B, armed at the end, and touching at the first timed step.
        /// </summary>
        static bool Timed(string label, int shapes, Func<FlightSetup> make, Func<FlightCore, int, StickSample> stick, int warmup, int steps, int repeats)
        {
            var times = new long[steps * repeats];
            long allocated = 0;
            int touching = 0;
            bool armed = true;
            for (int r = 0; r < repeats; r++)
            {
                var core = new FlightCore(make());
                long before = 0;
                for (int k = 0; k < warmup + steps; k++)
                {
                    StickSample s = stick(core, k);
                    if (k == warmup) before = GC.GetAllocatedBytesForCurrentThread();
                    long t0 = Stopwatch.GetTimestamp();
                    core.Advance(s);
                    if (k >= warmup) times[r * steps + k - warmup] = Stopwatch.GetTimestamp() - t0;
                    if (r == 0 && k == warmup) touching = core.Trace.ContactsTouching;
                }
                allocated += GC.GetAllocatedBytesForCurrentThread() - before;
                armed &= core.State.Armed;
            }
            Array.Sort(times);
            int n = times.Length;
            double us = 1e6 / Stopwatch.Frequency, p50 = times[n / 2] * us, p99 = times[n * 99 / 100] * us;
            bool pass = p99 <= 300 && allocated == 0 && armed && touching > 0;
            string share = TimedQuery.Share(make, stick, warmup, steps, repeats);
            Console.WriteLine($"{Name}: {label}: {repeats} × {steps} steps, {shapes} shapes, {touching} touching pairs at the first timed step: p50 {p50:F2} µs, "
                + $"p99 {p99:F2} µs, steady-state allocation {allocated} B, {share}: {(pass ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            return pass;
        }
    }
}
