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
    // disarmed, armed with the throttle cut, and with the hover's throttle held; and the coil's rock, legs off. Since
    // p7g-legs 1.2 the legs give across their tubes, which the rigid-body answer has not: its checks fly them rigid across
    // (LandingScenarios.RigidAcross, model 37's leg law), and the soft legs' own numbers are printed beside.
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
            var rigid = LandingScenarios.RigidAcross();
            ContactSpec legs = spec.BuildContacts(on, m, h);
            double side = legs.LegSideStiffness * (legs.FrictionStiffnessRatio * legs.LegStiffness) / (legs.LegSideStiffness + legs.FrictionStiffnessRatio * legs.LegStiffness);
            Console.WriteLine($"  on 1.2's side give ({side:F0} N/m across each foot) the quasi-static roll tip is {a.SoftRollTipDeg(side):F2}° (LandingStability.SoftRollTipDeg)");
            double tip13 = a.SoftRollTipDeg(legs.LegTubeSideStiffness, legs.FrictionStiffnessRatio * legs.LegStiffness, legs.LegSwayLoad, out double feet);
            Console.WriteLine($"  on 1.3's (the tube's {legs.LegTubeSideStiffness:F0} N/m less P·Δ against its {legs.LegSwayLoad:F1} N sway load, the jaws held) it is {tip13:F2}°, the feet {feet * 1e3:F1} mm in");
            double tipped = LandingScenarios.RollTime(spec, m, h, true, a.StaticRollTipDeg + 0.05, 60, rigid);
            double held = LandingScenarios.RollTime(spec, m, h, true, a.StaticRollTipDeg - 0.05, 60, rigid);
            Row("the roll tip", !double.IsPositiveInfinity(tipped) && double.IsPositiveInfinity(held),
                $"0.05° inside it rights itself ({S(held)}), 0.05° past it falls to 60° in {S(tipped)} s");
            foreach (double past in new[] { 1.0, 5, 10 })
            {
                double sim = LandingScenarios.RollTime(spec, m, h, true, a.StaticRollTipDeg + past, 74.9, rigid), mech = a.FallTime(past, 74.9);
                Row($"the fall from {past}° past the tip to the motor cans (74.9°)", Math.Abs(sim / mech - 1) < 0.03, $"{sim:F3} s, mechanics {mech:F3} s");
            }

            Console.WriteLine("  touchdowns: the lowest lateral speed (m/s) that tips the drone over; the caught skid's mechanics and its friction ratio J_t/J_n");
            Console.WriteLine("    the mechanics' columns rigid across (dead legs, caught; free, caught), the rest on the soft legs");
            Console.WriteLine("    the powered columns without airmode (FcSettings.Default since model 69, D-086); \"on\" the compared variant, model 65's");
            Console.WriteLine("    roll  descent | mechanics (J_t/J_n) | dead legs, caught | free, caught | soft, caught | free, μ 0.5/0.4 | disarmed | armed, cut | held, μ 0.5 | held, caught | on: cut | on: held, caught");
            foreach (double descent in new[] { 0.0, 0.5 })
                foreach (double roll in new[] { 0.0, 5, 10, 15, 20 })
                {
                    var t = new LandingScenarios.Touch { Legs = true, RollDeg = roll, Descent = descent };
                    double mech = a.CaughtSkidSpeed(roll, descent);
                    var caught = t;
                    caught.Edit = LandingScenarios.GroundFriction(10);
                    double soft = LandingScenarios.CriticalSpeed(spec, m, h, caught);
                    caught.Edit = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(10));
                    double fc = LandingScenarios.CriticalSpeed(spec, m, h, caught);
                    caught.Edit = LandingScenarios.RigidAcross(LandingScenarios.DeadLegsCaught(a.Mass));
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
                    t.Airmode = true;
                    double hcOn = LandingScenarios.CriticalSpeed(spec, m, h, t);
                    t.Edit = null;
                    t.Power = LandingScenarios.Power.Cut;
                    double cutOn = LandingScenarios.CriticalSpeed(spec, m, h, t);
                    Console.WriteLine($"    {roll,4:F0}° {descent,5:F1}    | {mech,9:F3} ({a.CatchFriction(roll, descent, mech),4:F2})   | {S(fd),17} | {S(fc),12} | {S(soft),12} | {S(fs),15} | {S(dis),8} | {S(cut),10} | {S(hs),11} | {S(hc),12} | {S(cutOn),7} | {S(hcOn),15}");
                    if (roll > 0 && descent > 0)
                        Row($"    the caught skid at {roll}°, {descent} m/s down", Math.Abs(fd / mech - 1) < 0.01 && Math.Abs(fc / mech - 1) < 0.15,
                            $"on dead legs {fd:F3}, on the legs {fc:F3}, against {mech:F3}");
                }
            // The disarmed column by the ESC's rule on Betaflight's DShot 0 (p6-flight-core 1.6c, EscDisarm): drone.json's
            // BLHeli_S with brake on stop off (D-086: the firmware's damped light brakes in its run loop, then every FET off),
            // model 38's freewheel (the known-wrong) and the compared variants (brake on stop on; Bluejay).
            var rules = new[] { EscDisarm.Freewheel, EscDisarm.BlheliS, EscDisarm.BlheliSBrakeOnStop, EscDisarm.Bluejay, EscDisarm.BluejayBrakeOnStop };
            Console.WriteLine("  disarmed at the touch, by the ESC's rule: the lowest lateral speed (m/s) that tips the drone over");
            Console.WriteLine("    roll  descent | freewheel (38) | blheli_s | blheli_s, brake on stop | bluejay | bluejay, brake on stop");
            var tipsAtRest = new int[rules.Length];
            foreach (double descent in new[] { 0.0, 0.5 })
                foreach (double roll in new[] { 0.0, 5, 10, 15, 20 })
                {
                    var line = new System.Text.StringBuilder($"    {roll,4:F0}° {descent,5:F1}    |");
                    for (int r = 0; r < rules.Length; r++)
                    {
                        var t = new LandingScenarios.Touch { Legs = true, RollDeg = roll, Descent = descent, Power = LandingScenarios.Power.Disarmed, Esc = rules[r] };
                        double v = LandingScenarios.CriticalSpeed(spec, m, h, t);
                        if (v == 0) tipsAtRest[r]++;
                        line.Append(r == 0 ? $" {S(v),14} |" : r == 2 ? $" {S(v),23} |" : r == 4 ? $" {S(v),22}" : $" {S(v),8} |");
                    }
                    Console.WriteLine(line);
                }
            Console.WriteLine($"    tipping with no drift, of 10: freewheel {tipsAtRest[0]}, blheli_s {tipsAtRest[1]}, brake on stop {tipsAtRest[2]}, "
                + $"bluejay {tipsAtRest[3]}, brake on stop {tipsAtRest[4]}");

            // The powered columns' steady approach (N4): its touch's own state, with no drift.
            Console.WriteLine($"  the powered columns fly a steady approach (N4): the touch {LandingScenarios.ApproachSeconds:F1} s after the throttle's step, "
                + $"its lateral speed the touch's own; powered, \"no descent\" is {LandingScenarios.PoweredMinDescent:F1} m/s");
            Console.WriteLine("    roll  descent | touch after (s) | tilt (deg) | lateral, descent (m/s) | thrust / weight | anti-gravity | throttle");
            foreach (double descent in new[] { 0.0, 0.5 })
                foreach (double roll in new[] { 0.0, 5, 10, 15, 20 })
                {
                    var t = new LandingScenarios.Touch { Legs = true, RollDeg = roll, Descent = descent, Power = LandingScenarios.Power.Held };
                    LandingScenarios.Outcome o = LandingScenarios.Run(spec, m, h, t);
                    bool steady = o.Touched && o.TouchAt >= 0.4 && Math.Abs(o.TouchTilt - roll) < 0.05 && Math.Abs(o.TouchLateral) < 0.01
                        && Math.Abs(o.TouchDescent - Math.Max(descent, LandingScenarios.PoweredMinDescent)) < 0.005 && o.TouchAntiGravity < 0.01;
                    Row($"    {roll,4:F0}° {descent,5:F1}    | {o.TouchAt,15:F3} | {o.TouchTilt,10:F3} | {o.TouchLateral,10:F4}, {o.TouchDescent,6:F4} | {o.TouchThrust / (a.Mass * 9.80665),15:F3} | {o.TouchAntiGravity,12:F4} | {o.Throttle:F4}",
                        steady, "steady");
                }
            Console.WriteLine($"  dynamic rollover on a pinned skid: from {a.RolloverDeg(0.95):F2}° with 0.95 of the weight in thrust, {a.RolloverDeg(0.5):F2}° with 0.5; "
                + $"pinning it at 10° takes μ {LandingStability.PinFriction(10, 0.95):F2} and {LandingStability.PinFriction(10, 0.5):F2}");
            Console.WriteLine($"  forward along the skids (free, 0.5 m/s down): sliding runners trip only past μ_k {a.SlidingTripFriction:F2}");
            foreach (double pitch in new[] { 0.0, 10, 20 })
            {
                var t = new LandingScenarios.Touch { Legs = true, PitchDeg = pitch, Descent = 0.5, Forward = true, Edit = rigid };
                double smooth = LandingScenarios.CriticalSpeed(spec, m, h, t);
                t.Edit = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(10));
                double caught = LandingScenarios.CriticalSpeed(spec, m, h, t), mech = a.CaughtEndSpeed(pitch, 0.5);
                t.Edit = LandingScenarios.RigidAcross(LandingScenarios.DeadLegsCaught(a.Mass));
                double dead = LandingScenarios.CriticalSpeed(spec, m, h, t);
                Row($"    {pitch}° nose-down", double.IsPositiveInfinity(smooth) && Math.Abs(caught / mech - 1) < 0.15 && (pitch == 0 || Math.Abs(dead / mech - 1) < 0.01),
                    $"μ 0.5/0.4 {S(smooth)} m/s; the ends caught {S(caught)} m/s, on dead legs {S(dead)}, mechanics {mech:F3}");
            }
            var frictionless = new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5, Edit = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(0.001)) };
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

            double first = c.FirstRestDeg(out string firstPart);
            Console.WriteLine($"legs off, the coil (the canister, at rest pitched {c.CoilRestPitchDeg:F2}° nose-down): {c.Mass:F3} kg, centre of mass {c.CoilAbove * 1e3:F2} mm over its axis (R {c.CoilRadius * 1e3:F1} mm); "
                + $"the pack would meet the ground at {c.PackRestDeg:F2}°, the {firstPart} meets it first at {first:F2}°; rate rolling {c.CoilRollingRate:F3} /s, on an axle {c.CoilAxleRate:F3} /s");
            foreach (double from in new[] { 0.1, 0.5, 1, 2, 5 })
            {
                double sim = LandingScenarios.RollTime(spec, m, h, false, from, 20, null, c.CoilRestPitchDeg), roll = c.CoilRockTime(from, 20), axle = c.CoilRockTime(from, 20, false);
                Row($"  the rock from {from}° to 20°", Math.Abs(sim / roll - 1) < 0.03, $"{sim:F3} s; rolling {roll:F3} s, an axle {axle:F3} s");
            }
            foreach (double e in new[] { 0.001, 0.002, 0.005 })
                Console.WriteLine($"  from level with the centre of mass {e * 1e3:F0} mm off the axis (mechanics): {c.CoilRockTime(0, 20, true, e):F3} s to 20°");
            double slides = LandingScenarios.RollTime(spec, m, h, false, 1, 20, LandingScenarios.GroundFriction(0.001), c.CoilRestPitchDeg);
            Row("  known-wrong, a frictionless coil", Math.Abs(slides / c.CoilRockTime(1, 20) - 1) > 0.10, $"{slides:F3} s from 1° (it slides out from under, as an axle)");
            double damped = LandingScenarios.RollTime(spec, m, h, false, 1, 20, LandingScenarios.BodyDamping(2), c.CoilRestPitchDeg);
            Row("  an overdamped contact (ζ 2)", Math.Abs(damped / LandingScenarios.RollTime(spec, m, h, false, 1, 20, null, c.CoilRestPitchDeg) - 1) < 0.02, $"{damped:F3} s from 1°: rolling compresses and slips nothing");
            foreach (bool? air in new bool?[] { null, true })
                foreach (var p in new[] { LandingScenarios.Power.Disarmed, LandingScenarios.Power.Cut, LandingScenarios.Power.Held })
                {
                    var t = new LandingScenarios.Touch { Legs = false, RollDeg = 1, Descent = 0.1, Power = p, ReachDeg = 20, Seconds = 6, Airmode = air };
                    var o = LandingScenarios.Run(spec, m, h, t);
                    Console.WriteLine($"  powered onto the coil at 1°, {p}{(air == true ? ", airmode on (65)" : "")}: 20° {S(o.Reached)} s after the touch, at {o.EndTilt:F1}° after 6 s "
                        + $"(the touch at {o.TouchDescent:F3} m/s down, {o.TouchAt:F3} s after the throttle's step)");
                }
            foreach (EscDisarm rule in rules)
            {
                var t = new LandingScenarios.Touch { Legs = false, RollDeg = 1, Descent = 0.1, Power = LandingScenarios.Power.Disarmed, ReachDeg = 20, Seconds = 6, Esc = rule };
                var o = LandingScenarios.Run(spec, m, h, t);
                Console.WriteLine($"  the coil disarmed, {rule}: 20° {S(o.Reached)} s after the touch, at {o.EndTilt:F1}° after 6 s");
            }
            return _ok;
        }
    }
}
