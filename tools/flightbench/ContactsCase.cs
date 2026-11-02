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

            Soil(spec);

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
            Row("the prints", !rl.Print[0] && rl.Print[1] && !rl.Print[2] && rl.Yield[1] < rl.Yield[0] && Math.Abs(rl.Yield[2] / rl.Yield[0] - 1) < 0.02,
                $"the soil yields {rl.Yield[0] * 1e3:F3} mm, then {rl.Yield[1] * 1e3:F3} mm from its print's floor {rl.Floor[1] * 1e3:F3} mm down, then {rl.Yield[2] * 1e3:F3} mm aside");

            Console.WriteLine("the glide landing (the pilot's 0.5 m/s down and 0.3 m/s forward, held to the touch, cut):");
            SoilScenarios.Landing gt = SoilScenarios.Land(spec, SoilScenarios.Floor("tilled"), default, 0.5, 0.3);
            SoilScenarios.Landing gd = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 0.5, 0.3);
            SoilScenarios.Landing rt = SoilScenarios.Land(spec, SoilScenarios.Floor("tilled"), default, 0.5, 0.3, rigid: true);
            SoilScenarios.Landing rd = SoilScenarios.Land(spec, SoilScenarios.Floor("dirt_track"), default, 0.5, 0.3, rigid: true);
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
                    int held = 0, soil = 0;
                    double hold = 0, sink = 0;
                    for (ulong seed = 1; seed <= 4; seed++)
                    {
                        SoilScenarios.Liftoff l = SoilScenarios.Lift(spec, place.World, place.At, hover, rain, seed, true);
                        if (l.Held >= 0) held++;
                        hold = Math.Max(hold, l.Hold);
                        for (int c = 0; c < 4; c++)
                        {
                            sink = Math.Max(sink, l.Sink[c]);
                            if (l.Sink[c] > 0) soil++;
                        }
                    }
                    string detail = $"{place.Name}, {rain}: hover {hover:F3}, {soil} of 16 half-skids in soil at rest, the deepest {sink * 1e3:F2} mm; {held} of 4 held, "
                        + $"the last corner at most {hold * 1e3:F0} ms after the others";
                    if (rain == RainLevel.Sunny && place.World != range) Row("no held leg on the dry track", held == 0, detail);
                    else Console.WriteLine("  (information) " + detail);
                }
            }
            Console.WriteLine("  (information) the pilot's \"most of the time in rain\" needs a pull against the lift, the cohesive pull σ_a·S_m over δ_a (design 7, task 1.13):"
                + " the anchor's c_a·S_m acts along the ground and lets go with the normal force. The dry track's \"sometimes\" needs the leg spread (1.17) and hooks (1.14).");
        }

        /// <summary>The step with its contacts busiest: legs on resting on the floor (eight supports and their friction), and
        /// legs off resting on the stand (eight arm edges on four posts), armed at idle; 60 000 steps each.</summary>
        public static bool Bench()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            bool ok = true;
            foreach (bool legs in new[] { true, false })
            {
                Loadout loadout = spec.Nominal;
                loadout.Legs = legs;
                ContactSpec contacts = spec.BuildContacts(loadout, materials, hash);
                FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, legs, legs
                    ? new Double3(0, ContactScenarios.Standing(contacts), 0) : LaunchStand.RestingOrigin(spec, contacts, default));
                if (!legs) setup.RuntimeObjects = new[] { LaunchStand.Build(spec, default, 0, setup.World.Query.MaterialId(spec.Stand.Material)) };
                setup.GroundEffect = spec.BuildGroundEffect(); // p7b-airframe 1.3: the rotors' rays and ground samples on top of the contacts
                setup.Flapping = spec.BuildFlapping(); // and 1.3c's flapping
                var core = new FlightCore(setup);
                const int warmup = 2000, steps = 60000;
                var times = new long[steps];
                long allocated = 0;
                int touching = 0;
                for (int k = -warmup; k < steps; k++)
                {
                    var stick = new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k + warmup >= 8, Throttle = 0 };
                    if (k == 0) allocated = GC.GetAllocatedBytesForCurrentThread();
                    long t0 = Stopwatch.GetTimestamp();
                    core.Advance(stick);
                    if (k >= 0) times[k] = Stopwatch.GetTimestamp() - t0;
                    if (k == steps - 1) touching = core.Trace.ContactsTouching;
                }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Array.Sort(times);
                double us = 1e6 / Stopwatch.Frequency, p50 = times[steps / 2] * us, p99 = times[steps * 99 / 100] * us;
                bool pass = p99 <= 300 && allocated == 0 && core.State.Armed && touching > 0;
                ok &= pass;
                FlightSetup again = ContactScenarios.Setup(spec, materials, hash, legs, setup.StartPosition);
                if (!legs) again.RuntimeObjects = new[] { LaunchStand.Build(spec, default, 0, again.World.Query.MaterialId(spec.Stand.Material)) };
                again.GroundEffect = spec.BuildGroundEffect();
                again.Flapping = spec.BuildFlapping();
                string share = TimedQuery.Share(again, (c, k) => new StickSample { HostUs = c.State.Step * FlightCore.StepUs, Arm = k >= 8, Throttle = 0 }, warmup, steps);
                Console.WriteLine($"{Name}: {(legs ? "legs on, resting on the floor" : "legs off, resting on the stand")}, armed at idle: {steps} steps, "
                    + $"{contacts.Shapes.Length} shapes, {touching} touching pairs: p50 {p50:F2} µs, p99 {p99:F2} µs, steady-state allocation {allocated} B, "
                    + $"{share}: {(pass ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            }
            return ok;
        }
    }
}
