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

            Console.WriteLine($"{Name}: {(_ok ? "PASS" : "FAIL")}");
            return _ok;
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
                string share = TimedQuery.Share(again, (c, k) => new StickSample { HostUs = c.State.Step * FlightCore.StepUs, Arm = k >= 8, Throttle = 0 }, warmup, steps);
                Console.WriteLine($"{Name}: {(legs ? "legs on, resting on the floor" : "legs off, resting on the stand")}, armed at idle: {steps} steps, "
                    + $"{contacts.Shapes.Length} shapes, {touching} touching pairs: p50 {p50:F2} µs, p99 {p99:F2} µs, steady-state allocation {allocated} B, "
                    + $"{share}: {(pass ? "PASS" : "FAIL")} (p99 ≤ 300 µs, 0 B)");
            }
            return ok;
        }
    }
}
