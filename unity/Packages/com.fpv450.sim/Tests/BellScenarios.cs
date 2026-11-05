using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7f-damage 1.5's scenarios (prop-motor-damage's "The motor's bell, its gap and binding" and "The ESC and the current show
    /// the damage"): the repository's drone on the test range, its rear-right motor's bell against a runtime face 60 m up (Sky),
    /// its rotors at hover speed and its contacts' forces on a rig whose body is scripted or flies free (no gravity, no thrust on
    /// it). BellTests asserts them; tools/flightbench --damage prints them.
    /// </summary>
    public static class BellScenarios
    {
        /// <summary>The motor the scenarios load: rear right, clockwise.</summary>
        public const int Corner = 0;

        public static Double3 Sky => StrikeScenarios.Sky;

        /// <summary>The rear-right motor's sphere centre on the drone at Sky, level, world.</summary>
        public static Double3 MotorCentre(DroneSpec spec) => Sky + spec.Parts[spec.PartIndex("motor_rr")].Position;

        /// <summary>
        /// The rig: FlightCore's propulsion at a duty, then its contacts' forces (the bells into the rotors for the next step), then
        /// the body moved by the script or, Free, by the contacts' force alone; then the sense and the strikes. With TargetThrust,
        /// the duty holds the loaded rotor's thrust there (the drone holding its height), at most 1.
        /// </summary>
        public sealed class Rig
        {
            public readonly FlightCore Core;
            public FlightState Now;
            public double Duty, TargetThrust = double.NaN;
            public bool Free;
            public Quat Attitude;
            public readonly StepEvents Events = new StepEvents(256);
            public readonly List<FlightEvent> Log = new List<FlightEvent>();
            public StepTrace Trace;
            public Double3 Force;
            public Corners Amps;
            public double TotalAmps;
            public System.Action<Rig> Each;

            public Rig(FlightSetup setup, double duty)
            {
                Core = new FlightCore(setup);
                Now = Core.State;
                Attitude = setup.StartAttitude;
                Duty = duty;
                for (int i = 0; i < 4; i++)
                {
                    Now.EscCommand[i] = duty;
                    Now.RotorSpeed[i] = StrikeScenarios.HoverSpeed;
                }
                Now.Armed = true;
                Now.Velocity = setup.StartVelocity;
            }

            public void Step(Double3 position, Double3 velocity)
            {
                FlightState s = Now;
                s.BladeStrikes = default;
                Corners angles = s.RotorAngle;
                Propulsion p = Core.Propulsion;
                p.BeginStep(s);
                var loads = new RotorLoads();
                var duty = new Corners { RearRight = Duty, FrontRight = Duty, RearLeft = Duty, FrontLeft = Duty };
                for (int j = 0; j < FlightController.LoopsPerStep; j++) p.SubStep(ref s, duty, true, FlightController.LoopDt, ref loads);
                TotalAmps = 0;
                for (int i = 0; i < 4; i++)
                {
                    Amps[i] = loads.SupplyAmps[i] / loads.SubSteps;
                    TotalAmps += Amps[i];
                }
                Events.Clear();
                Double3 force = default, moment = default;
                StepTrace t = default;
                Core.Contacts.Forces(ref s, Core.Body, ref force, ref moment, ref t, Events);
                Trace = t;
                Force = force;
                FlightState was = s;
                if (Free) RigidBody.Step(ref s, Core.Body, force * Core.Body.InverseMass, moment, FlightCore.Dt);
                else
                {
                    s.Position = position;
                    s.Velocity = velocity;
                    s.Attitude = Attitude;
                    s.BodyRate = default;
                }
                s.Step++;
                Core.Contacts.Sense(was, ref s, Events);
                Core.Strikes?.Apply(angles, ref s, Core.Body, Events);
                for (int i = 0; i < Events.Count; i++) Log.Add(Events[i]);
                Now = s;
                Each?.Invoke(this);
                if (!double.IsNaN(TargetThrust))
                    Duty = DetMath.Min(1, Duty * (1 + ThrustGain * (TargetThrust - p.Thrust(Corner)) / TargetThrust));
            }

            public int Count(EventKind kind)
            {
                int n = 0;
                foreach (FlightEvent e in Log)
                    if (e.Kind == kind) n++;
                return n;
            }
        }

        // The thrust hold's gain a step (assumed: about 0.2 s against the motor's 40 ms lag, so it settles without ringing).
        const double ThrustGain = 0.005;

        /// <summary>The rig's setup at Sky, level, with the runtime objects; bells false flies the can rigid (ContactSpec.MotorBell
        /// off, model 57's); protection sets every ESC's limit to ShakeScenario.Protection (40 A, the compared variant).</summary>
        public static FlightSetup Setup(DroneSpec spec, RuntimeObject[] objects, bool bells = true, bool protection = false, Double3 velocity = default)
        {
            FlightSetup setup = StrikeScenarios.Setup(spec, Sky, Quat.Identity, objects);
            setup.Contacts.MotorBell = bells;
            setup.StartVelocity = velocity;
            if (protection)
                for (int i = 0; i < 4; i++) setup.Propulsion.Motors[i].EscCurrentLimit = ShakeScenario.Protection;
            return setup;
        }

        /// <summary>The rear-right motor's contact shape.</summary>
        public static int MotorShape(FlightCore core)
        {
            for (int i = 0; i < core.Contacts.ShapeCount; i++)
                if (core.Contacts.Shape(i).Name == "motor_rr") return i;
            return -1;
        }

        /// <summary>A pressed bell's outcome (prop-motor-damage's "A scraped bell draws more current").</summary>
        public struct Press
        {
            public double Newtons, Normal, Scrape, Expected, Kinetic, Push, Amps, Total, Speed, Duty;
            public int Strikes, Others;

            public string Describe() =>
                $"{Newtons:F0} N asked: the bell's load {Normal:F4} N, its scrape {Scrape:F6} N·m (μ_k·F_n·R {Expected:F6}), the drone pushed {Push:F3} N "
                + $"along the wall against the bell's surface; rr {Amps:F3} A of {Total:F3} A in all at duty {Duty:F4}, its rotor {Speed:F1} rad/s; "
                + $"{Strikes} prop strikes, {Others} other parts touching";
        }

        /// <summary>
        /// "A scraped bell draws more current": the drone held level at Sky with its rear-right motor's bell pressed at newtons
        /// (0 for none) on a masonry block (H01_D's wall's material) whose top stands under the prop's disc, its rotors at hover
        /// speed, the duty holding that rotor's thrust for 1.5 s (the drone holding its height); the last 0.5 s measured. The block
        /// is the wall's face at the motor's height: a full wall would meet the prop's disc first. flat (the known-wrong, with the
        /// bells off) is a flat efficiency loss while the motor touches: its Kt × 0.95, whatever the push.
        /// </summary>
        public static Press Pressed(DroneSpec spec, double newtons, bool bells = true, bool flat = false)
        {
            const double R = 0.0186;
            Double3 c = MotorCentre(spec);
            double depth = newtons > 0 ? newtons / 2e5 : -0.002, normal = 0;
            Press r = default;
            for (int pass = 0; pass < 2; pass++)
            {
                double face = c.X + R - depth;
                var box = StrikeScenarios.Box("masonry", new Double3(face + 0.1, c.Y - 0.0025, c.Z), new Double3(0.1, 0.0095, 0.05));
                var rig = new Rig(Setup(spec, StrikeScenarios.One(box), bells && !flat), StrikeScenarios.HoverDuty(spec));
                int motor = MotorShape(rig.Core);
                double target = 0;
                rig.Each = g =>
                {
                    if (flat) g.Core.Propulsion.SetMotor(Corner, g.Core.Contacts.Touching(motor) ? 0.95 : 1, 0);
                };
                rig.Step(Sky, default);
                target = rig.Core.Propulsion.Thrust(Corner);
                rig.TargetThrust = target;
                r = new Press { Newtons = newtons };
                const int steps = 1500, from = 1000;
                for (int k = 0; k < steps; k++)
                {
                    rig.Step(Sky, default);
                    if (k < from) continue;
                    double n = steps - from;
                    r.Amps += rig.Amps[Corner] / n;
                    r.Total += rig.TotalAmps / n;
                    r.Speed += rig.Now.RotorSpeed[Corner] / n;
                    r.Duty += rig.Duty / n;
                }
                MotorBell b = rig.Core.Contacts.Bells;
                normal = b != null ? b.Normal(Corner) : 0;
                r.Normal = normal;
                r.Scrape = rig.Trace.BellScrape[Corner];
                ContactSpec cs = rig.Core.Contacts.Spec;
                cs.Row("masonry", out MaterialRow row);
                r.Kinetic = row.KineticFriction;
                r.Expected = row.KineticFriction * normal * R;
                r.Push = rig.Trace.ContactForce.Z; // the bell's surface moves −z there (a clockwise rotor), so the drone goes +z
                r.Strikes = rig.Count(EventKind.PropStrike) + rig.Count(EventKind.BladeStrike);
                for (int i = 0; i < rig.Core.Contacts.ShapeCount; i++)
                    if (i != motor && rig.Core.Contacts.Touching(i)) r.Others++;
                if (!(newtons > 0) || !(normal > 0) || flat || !bells) break;
                depth *= newtons / normal;
            }
            return r;
        }

        /// <summary>A side hit's outcome (prop-motor-damage's "A dented bell rubs and can bind").</summary>
        public struct Hit
        {
            public double Speed, Peak, Press, Seize, SpeedBefore, SpeedAfter, Kt, DentGap, DentFree, WrongDent;
            public int Dents, Strikes, Tunnels;
            public bool Rod;

            public string Describe() =>
                $"{(Rod ? "a 3 mm steel rod on the can's lip below the stator" : "a concrete wall at the stator's height")} at {Speed} m/s: peak {Peak:F1} N, "
                + $"the magnets pressed onto the teeth {Press:F1} N (their rub {Seize:F3} N·m at its peak), rr {SpeedBefore:F1} → {SpeedAfter:F1} rad/s; "
                + $"{Dents} dents, the gap's {DentGap * 1e3:F4} mm, off the stator {DentFree * 1e3:F4} mm, Kt × {Kt:F6}; {Strikes} prop strikes, {Tunnels} tunnels"
                + (Rod ? "" : $"; the known-wrong ring with no stator behind it would dent {WrongDent * 1e3:F3} mm, past the {0.3:F1} mm gap: {(WrongDent > 0.0003 ? "rubs" : "no rub")}");
        }

        /// <summary>
        /// A side hit on the rear-right bell at speed (m/s, along +x), the drone free in space, its rotors at hover speed at a
        /// fixed duty: on a concrete wall at the stator's height (the block of Pressed, its face 2 mm off), or, rod, on a 3 mm
        /// steel rod across the can's lip below the stator. 60 ms flown.
        /// </summary>
        public static Hit Struck(DroneSpec spec, double speed, bool rod)
        {
            const double R = 0.0186;
            Double3 c = MotorCentre(spec);
            ShapeGeometry g = rod
                ? StrikeScenarios.Rod("steel", default, 0.0015, 0.05)
                : StrikeScenarios.Box("concrete", new Double3(c.X + R + 0.002 + 0.1, c.Y - 0.0025, c.Z), new Double3(0.1, 0.0095, 0.05));
            if (rod)
            {
                // Across z, 9.7 mm under the motor's centre: it meets the can's lip 7 mm above the base, below the stator, 5 mm over
                // the arm's top and far under the prop's disc (the band above the stator lies within 5 mm of the disc's slab, so
                // anything that reaches it there reaches the blades too).
                double up = -0.0097, dx = System.Math.Sqrt((R + 0.0015) * (R + 0.0015) - up * up);
                g.Center = new Double3(c.X + dx + 0.002, c.Y + up, c.Z);
                g.AxisX = new Double3(0, 1, 0);
                g.AxisY = new Double3(0, 0, 1);
                g.AxisZ = new Double3(1, 0, 0);
            }
            var rig = new Rig(Setup(spec, StrikeScenarios.One(g), velocity: new Double3(speed, 0, 0)), StrikeScenarios.HoverDuty(spec)) { Free = true };
            var h = new Hit { Speed = speed, Rod = rod, SpeedBefore = rig.Now.RotorSpeed[Corner] };
            MotorBell b = rig.Core.Contacts.Bells;
            for (int k = 0; k < 60; k++)
            {
                rig.Step(default, default);
                h.Peak = System.Math.Max(h.Peak, b.Normal(Corner));
                h.Press = System.Math.Max(h.Press, b.Press(Corner));
                h.Seize = System.Math.Max(h.Seize, rig.Trace.BellRub[Corner]);
            }
            h.SpeedAfter = rig.Now.RotorSpeed[Corner];
            h.Kt = b.KtRatio(Corner);
            h.DentGap = b.DentGap(Corner);
            h.DentFree = b.DentFree(Corner);
            h.Dents = rig.Count(EventKind.MotorDamaged);
            h.Strikes = rig.Count(EventKind.PropStrike) + rig.Count(EventKind.BladeStrike);
            h.Tunnels = rig.Count(EventKind.Tunnel);
            // The known-wrong: design 8's ring with nothing behind it, its dent (F − P_c)/k_n from the same peak.
            double kn = rig.Core.Contacts.Shape(MotorShape(rig.Core)).Stiffness;
            h.WrongDent = h.Peak > b.CollapseLoad(Corner) ? (h.Peak - b.CollapseLoad(Corner)) / kn : 0;
            return h;
        }

        /// <summary>A preset dent's rig (no contact): the drone held at Sky, its rotors at hover speed, the duty holding the dented
        /// rotor's thrust; protection the 40 A variant.</summary>
        public static Rig Dented(DroneSpec spec, double dent, bool protection = false)
        {
            var rig = new Rig(Setup(spec, null, protection: protection), StrikeScenarios.HoverDuty(spec));
            rig.Step(Sky, default);
            rig.TargetThrust = rig.Core.Propulsion.Thrust(Corner);
            rig.Core.Contacts.Bells.PresetDent(Corner, dent);
            return rig;
        }
    }
}
