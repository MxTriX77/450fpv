using System;
using System.Diagnostics;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
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

        static void Row(string name, bool pass, string detail)
        {
            _ok &= pass;
            Console.WriteLine($"  {name}: {detail}: {(pass ? "PASS" : "FAIL")}");
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

            Console.WriteLine($"{Name}: {(_ok ? "PASS" : "FAIL")}");
            return _ok;
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
                Row(name + ", resting", r.Soil == r.Pairs.Count && r.Soil >= 8 && Math.Abs(r.LoadOverWeight - 1) < 1e-3 && r.LateEvents == 0
                    && r.Tunnels == 0 && r.Crashes == 0 && r.Lost == 0, Rest(r));
                ContactScenarios.Landing held = ContactScenarios.Land(spec, materials, hash, false, site: site);
                Row(name + ", throttle held, as the stub's", Math.Abs(held.SeparationHeight - stubHeld.SeparationHeight) <= 1e-3 && held.Tunnels == 0 && held.Crashes == 0
                    && held.Lost == 0, Land(held) + First(held));
                ContactScenarios.Landing cut = ContactScenarios.Land(spec, materials, hash, true, site: site);
                Row(name + ", throttle cut, as the stub's", Math.Abs(cut.Settled - stubCut.Settled) <= 0.01 && cut.Tunnels == 0 && cut.Crashes == 0 && cut.Lost == 0,
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
