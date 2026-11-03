using System;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;

namespace Fpv450.FlightBench
{
    // D-067's landing stability (p6-flight-core 1.6c): LandingStability's rigid-body answer from the mass build and the
    // contact shapes, against the sim's scripted touchdowns (LandingScenarios, which LandingTests asserts) over a grid of
    // roll errors, lateral speeds and descents, on the stub's floor and at the test range's soil, run and straw; free,
    // disarmed, armed with the throttle cut, and with the hover's throttle held; and the coil's rock, legs off.
    internal static class LandingCase
    {
        static bool _ok = true;

        static void Row(string name, bool pass, string detail)
        {
            _ok &= pass;
            Console.WriteLine($"  {name}: {detail}: {(pass ? "PASS" : "FAIL")}");
        }

        static string S(double v) => double.IsPositiveInfinity(v) ? "never" : v.ToString("F3");

        public static bool Run()
        {
            _ok = true;
            DroneSpec spec = PoweredFlightCase.Drone();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            Loadout on = spec.Nominal, off = spec.Nominal;
            off.Legs = false;
            LandingStability a = LandingStability.Legs(spec, spec.BuildContacts(on, m, h), spec.BuildMass(on).Properties);
            LandingStability c = LandingStability.Coil(spec, spec.BuildContacts(off, m, h), spec.BuildMass(off).Properties);

            Console.WriteLine($"legs on: {a.Mass:F3} kg, centre of mass {a.SkidHeight * 1e3:F2} mm over the skids' axes, {a.Track * 1e3:F1} mm inboard; "
                + $"I_roll {a.RollInertia:F5}, about the skid {a.PivotInertia:F5} kg·m², the fall's rate {a.FallRate:F3} /s");
            Console.WriteLine($"  static tip: roll {a.StaticRollTipDeg:F2}°, pitch over the front ends {a.StaticPitchTipDeg:F2}°");
            double tipped = LandingScenarios.RollTime(spec, m, h, true, a.StaticRollTipDeg + 0.05, 60);
            double held = LandingScenarios.RollTime(spec, m, h, true, a.StaticRollTipDeg - 0.05, 60);
            Row("the roll tip", !double.IsPositiveInfinity(tipped) && double.IsPositiveInfinity(held),
                $"0.05° inside it rights itself ({S(held)}), 0.05° past it falls to 60° in {S(tipped)} s");
            foreach (double past in new[] { 1.0, 5, 10 })
            {
                double sim = LandingScenarios.RollTime(spec, m, h, true, a.StaticRollTipDeg + past, 74.9), mech = a.FallTime(past, 74.9);
                Row($"the fall from {past}° past the tip to the motor cans (74.9°)", Math.Abs(sim / mech - 1) < 0.03, $"{sim:F3} s, mechanics {mech:F3} s");
            }

            Console.WriteLine("  touchdowns: the lowest lateral speed (m/s) that tips the drone over; the caught skid's mechanics and its friction ratio J_t/J_n");
            Console.WriteLine("    roll  descent | mechanics (J_t/J_n) | dead legs, caught | free, caught | free, μ 0.5/0.4 | disarmed | armed, cut | held, μ 0.5 | held, caught");
            foreach (double descent in new[] { 0.0, 0.5 })
                foreach (double roll in new[] { 0.0, 5, 10, 15, 20 })
                {
                    var t = new LandingScenarios.Touch { Legs = true, RollDeg = roll, Descent = descent };
                    double mech = a.CaughtSkidSpeed(roll, descent);
                    var caught = t;
                    caught.Edit = LandingScenarios.GroundFriction(10);
                    double fc = LandingScenarios.CriticalSpeed(spec, m, h, caught);
                    caught.Edit = LandingScenarios.DeadLegsCaught(a.Mass);
                    double fd = LandingScenarios.CriticalSpeed(spec, m, h, caught);
                    double fs = LandingScenarios.CriticalSpeed(spec, m, h, t);
                    t.Power = LandingScenarios.Power.Disarmed;
                    double dis = LandingScenarios.CriticalSpeed(spec, m, h, t);
                    t.Power = LandingScenarios.Power.Cut;
                    double cut = LandingScenarios.CriticalSpeed(spec, m, h, t);
                    t.Power = LandingScenarios.Power.Held;
                    double hs = LandingScenarios.CriticalSpeed(spec, m, h, t);
                    t.Edit = LandingScenarios.GroundFriction(10);
                    double hc = LandingScenarios.CriticalSpeed(spec, m, h, t);
                    Console.WriteLine($"    {roll,4:F0}° {descent,5:F1}    | {mech,9:F3} ({a.CatchFriction(roll, descent, mech),4:F2})   | {S(fd),17} | {S(fc),12} | {S(fs),15} | {S(dis),8} | {S(cut),10} | {S(hs),11} | {S(hc),12}");
                    if (roll > 0 && descent > 0)
                        Row($"    the caught skid at {roll}°, {descent} m/s down", Math.Abs(fd / mech - 1) < 0.01 && Math.Abs(fc / mech - 1) < 0.12,
                            $"on dead legs {fd:F3}, on the legs {fc:F3}, against {mech:F3}");
                }
            Console.WriteLine($"  dynamic rollover on a pinned skid: from {a.RolloverDeg(0.95):F2}° with 0.95 of the weight in thrust, {a.RolloverDeg(0.5):F2}° with 0.5; "
                + $"pinning it at 10° takes μ {LandingStability.PinFriction(10, 0.95):F2} and {LandingStability.PinFriction(10, 0.5):F2}");
            Console.WriteLine($"  forward along the skids (free, 0.5 m/s down): sliding runners trip only past μ_k {a.SlidingTripFriction:F2}");
            foreach (double pitch in new[] { 0.0, 10, 20 })
            {
                var t = new LandingScenarios.Touch { Legs = true, PitchDeg = pitch, Descent = 0.5, Forward = true };
                double smooth = LandingScenarios.CriticalSpeed(spec, m, h, t);
                t.Edit = LandingScenarios.GroundFriction(10);
                double caught = LandingScenarios.CriticalSpeed(spec, m, h, t), mech = a.CaughtEndSpeed(pitch, 0.5);
                t.Edit = LandingScenarios.DeadLegsCaught(a.Mass);
                double dead = LandingScenarios.CriticalSpeed(spec, m, h, t);
                Row($"    {pitch}° nose-down", double.IsPositiveInfinity(smooth) && Math.Abs(caught / mech - 1) < 0.12 && (pitch == 0 || Math.Abs(dead / mech - 1) < 0.01),
                    $"μ 0.5/0.4 {S(smooth)} m/s; the ends caught {S(caught)} m/s, on dead legs {S(dead)}, mechanics {mech:F3}");
            }
            var frictionless = new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5, Edit = LandingScenarios.GroundFriction(0.001) };
            Row("known-wrong, a frictionless skid", double.IsPositiveInfinity(LandingScenarios.CriticalSpeed(spec, m, h, frictionless)),
                $"never catches (mechanics: a caught skid tips at {a.CaughtSkidSpeed(10, 0.5):F3} m/s)");

            // The range's grounds: S04's bare soil and E01's run are rigid soil shapes on the ground's pair, the plane's law;
            // S04's straw is a compliant mat over its soil.
            Console.WriteLine("  the range (free body, 10° of roll, 0.5 m/s down; caught and with the ground's μ):");
            foreach (var (name, foot) in new[] { ("the stub's floor", (Double3?)null), ("S04's soil", RangeScenarios.Soil), ("E01's run", RangeScenarios.Run), ("S04's straw", RangeScenarios.Straw) })
            {
                var t = new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5 };
                if (foot != null) t.Site = RangeScenarios.At(foot.Value, 0);
                double smooth = LandingScenarios.CriticalSpeed(spec, m, h, t);
                t.Edit = LandingScenarios.GroundFriction(10);
                double caught = LandingScenarios.CriticalSpeed(spec, m, h, t);
                Console.WriteLine($"    {name}: μ 0.5/0.4 {S(smooth)} m/s, caught {S(caught)} m/s");
            }

            Console.WriteLine($"legs off, the coil: {c.Mass:F3} kg, centre of mass {c.CoilAbove * 1e3:F2} mm over its axis (R {c.CoilRadius * 1e3:F1} mm); "
                + $"the pack meets the ground at {c.PackRestDeg:F2}°; rate rolling {c.CoilRollingRate:F3} /s, on an axle {c.CoilAxleRate:F3} /s");
            foreach (double from in new[] { 0.1, 0.5, 1, 2, 5 })
            {
                double sim = LandingScenarios.RollTime(spec, m, h, false, from, 20), roll = c.CoilRockTime(from, 20), axle = c.CoilRockTime(from, 20, false);
                Row($"  the rock from {from}° to 20°", Math.Abs(sim / roll - 1) < 0.03, $"{sim:F3} s; rolling {roll:F3} s, an axle {axle:F3} s");
            }
            foreach (double e in new[] { 0.001, 0.002, 0.005 })
                Console.WriteLine($"  from level with the centre of mass {e * 1e3:F0} mm off the axis (mechanics): {c.CoilRockTime(0, 20, true, e):F3} s to 20°");
            double slides = LandingScenarios.RollTime(spec, m, h, false, 1, 20, LandingScenarios.GroundFriction(0.001));
            Row("  known-wrong, a frictionless coil", Math.Abs(slides / c.CoilRockTime(1, 20) - 1) > 0.10, $"{slides:F3} s from 1° (it slides out from under, as an axle)");
            double damped = LandingScenarios.RollTime(spec, m, h, false, 1, 20, LandingScenarios.BodyDamping(2));
            Row("  an overdamped contact (ζ 2)", Math.Abs(damped / LandingScenarios.RollTime(spec, m, h, false, 1, 20) - 1) < 0.02, $"{damped:F3} s from 1°: rolling compresses and slips nothing");
            foreach (var p in new[] { LandingScenarios.Power.Disarmed, LandingScenarios.Power.Cut, LandingScenarios.Power.Held })
            {
                var t = new LandingScenarios.Touch { Legs = false, RollDeg = 1, Descent = 0.1, Power = p, ReachDeg = 20, Seconds = 6 };
                var o = LandingScenarios.Run(spec, m, h, t);
                Console.WriteLine($"  powered onto the coil at 1°, {p}: 20° {S(o.Reached)} s after the touch, at {o.EndTilt:F1}° after 6 s");
            }
            return _ok;
        }
    }
}
