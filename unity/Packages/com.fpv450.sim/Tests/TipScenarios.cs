using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// w17-four-legs 1.3's flights for the four legs' tips in the soil (design §4's targets): the start set down and at rest, a level
    /// drop with the throttle cut (a touchdown at a speed, along or not), and a careful liftoff whose thrust over weight rises at the
    /// pilot's measured rate. Each on the stub floor of one surface (SoilScenarios.Floor), still air, surfaces.json at the rain's set.
    /// Without NUnit, so flightbench links it too.
    /// </summary>
    public static class TipScenarios
    {
        const double G = 9.80665;

        /// <summary>The drone's weight in a flight, N.</summary>
        public static double Weight(FlightCore core) => core.Body.Mass * G;

        /// <summary>The rotors' thrust over the weight now.</summary>
        public static double ThrustOverWeight(FlightCore core)
        {
            double t = 0;
            for (int c = 0; c < 4; c++) t += core.Propulsion.Thrust(c);
            return t / Weight(core);
        }

        // ---- The start ----------------------------------------------------------------------------------------------------

        public struct Start
        {
            /// <summary>Each leg's draw (leg.setdown), its start's rest sink and deepest (m), and after `seconds` at rest disarmed its
            /// sink and its load (N); the body's height change over the rest (m) and its largest speed (m/s).</summary>
            public double[] SetDown, StartSink, StartMax, Sink, Load;
            public double Settle, Speed;
            public ulong Hash;
        }

        /// <summary>The legs-on drone set on surface's stub floor with seed's draws, disarmed for `seconds`.</summary>
        public static Start Rest(DroneSpec spec, string surface, RainLevel rain, ulong seed, double seconds = 1)
        {
            var core = new FlightCore(SoilScenarios.Setup(spec, SoilScenarios.Floor(surface), new Double3(0, SoilScenarios.Standing(spec), 0), rain, seed));
            var r = new Start
            {
                SetDown = new double[4], StartSink = new double[4], StartMax = new double[4], Sink = new double[4], Load = new double[4],
            };
            for (int c = 0; c < 4; c++)
            {
                r.SetDown[c] = core.Contacts.SetDownOf(c);
                r.StartSink[c] = core.Contacts.StartSink(c);
                r.StartMax[c] = core.Contacts.StartDeepest(c);
            }
            double y0 = core.State.Position.Y;
            ulong hash = 0xCBF29CE484222325UL;
            var record = new byte[Log.LogFormat.StepSize];
            int steps = (int)(seconds / FlightCore.Dt + 0.5);
            for (int k = 0; k < steps; k++)
            {
                var stick = new StickSample { HostUs = core.State.Step * FlightCore.StepUs };
                core.Advance(stick);
                r.Speed = DetMath.Max(r.Speed, core.State.Velocity.Length());
                Log.LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                for (int i = 0; i < record.Length; i++) hash = (hash ^ record[i]) * 0x100000001B3UL;
            }
            for (int c = 0; c < 4; c++)
            {
                r.Sink[c] = core.Contacts.TipOf(c).Sink;
                r.Load[c] = core.Trace.LegLoad[c];
            }
            r.Settle = core.State.Position.Y - y0;
            r.Hash = hash;
            return r;
        }

        // ---- The touchdown ------------------------------------------------------------------------------------------------

        public struct Touchdown
        {
            /// <summary>The descent at the first touch (m/s); each leg's deepest sink and its sink at the end (m), the greatest of
            /// them; the most a leg carried (N) and the body's peak upward acceleration over g (g's); the most the body rose after
            /// its first lowest point (m); the supports' ground contacts broken after the first touch; the slide along the ground
            /// from the touch to the end (m); the time from the touch until the body stays slower than 1 mm/s... 10 mm/s (s; NaN
            /// never); crashes and tunnels.</summary>
            public double TouchSpeed, Deepest, EndSink, PeakLoad, PeakG, Rebound, Slide, Rest;
            public double[] Max, End;
            public int Lost, Crashes, Tunnels, Ploughed;
        }

        /// <summary>
        /// The legs-on drone dropped level, disarmed (the throttle cut, the props still), from the height that gives `down` m/s at
        /// the touch (still air), moving `along` m/s forward (body +z); flown `seconds` past the start. rest is the speed under which
        /// the body is at rest (m/s). tips false flies the known-wrong: the new drone's tips on the ground's Coulomb alone.
        /// </summary>
        public static Touchdown Drop(DroneSpec spec, string surface, RainLevel rain, double down, double along = 0, double seconds = 2.5,
            ulong seed = 0x450F, bool tips = true, double rest = 0.01)
        {
            double h = down * down / (2 * G);
            FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor(surface), new Double3(0, SoilScenarios.Standing(spec) + h, 0), rain, seed);
            if (!tips) setup.Contacts.LegTips = false;
            setup.StartVelocity = new Double3(0, 0, along);
            var core = new FlightCore(setup);
            var r = new Touchdown { Max = new double[4], End = new double[4], TouchSpeed = double.NaN, Rest = double.NaN };
            long touch = -1;
            double lastVy = 0;
            Double3 at = default;
            int steps = (int)(seconds / FlightCore.Dt + 0.5);
            for (int k = 0; k < steps; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                ref readonly FlightState s = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.TipPloughed) r.Ploughed++;
                    bool support = IsSupport(core, e);
                    if (touch < 0 && e.Kind == EventKind.ContactMade && e.Object == WorldIds.Ground)
                    {
                        touch = s.Step;
                        r.TouchSpeed = -lastVy;
                        at = s.Position;
                    }
                    else if (touch >= 0 && e.Kind == EventKind.ContactBroken && e.Object == WorldIds.Ground && support) r.Lost++;
                }
                lastVy = s.Velocity.Y;
                if (touch < 0) continue;
                for (int c = 0; c < 4; c++)
                {
                    TipSoil t = core.Contacts.TipOf(c);
                    r.Max[c] = DetMath.Max(r.Max[c], t.Max);
                    r.PeakLoad = DetMath.Max(r.PeakLoad, core.Trace.LegLoad[c]);
                }
                r.PeakG = DetMath.Max(r.PeakG, core.Trace.ContactForce.Y / Weight(core));
                // The rebound: the lowest support's clearance over the floor while every one is off it (the drone in the air again).
                double clear = double.PositiveInfinity;
                foreach (ContactShape sh in core.Contacts.Spec.Shapes)
                    if (sh.Role == ContactRole.Support) clear = DetMath.Min(clear, (s.Position + s.Attitude.Rotate(sh.A)).Y - sh.Radius);
                r.Rebound = DetMath.Max(r.Rebound, clear);
                double speed = s.Velocity.Length();
                if (speed < rest) { if (double.IsNaN(r.Rest)) r.Rest = (s.Step - touch) * FlightCore.Dt; }
                else r.Rest = double.NaN;
            }
            for (int c = 0; c < 4; c++)
            {
                r.End[c] = core.Contacts.TipOf(c).Sink;
                r.Deepest = DetMath.Max(r.Deepest, r.Max[c]);
                r.EndSink = DetMath.Max(r.EndSink, r.End[c]);
            }
            Double3 d = core.State.Position - at;
            r.Slide = Math.Sqrt(d.X * d.X + d.Z * d.Z);
            return r;
        }

        // A contact event on a support's part (a leg's tip on four legs, a skid on model 105's).
        static bool IsSupport(FlightCore core, in FlightEvent e)
        {
            foreach (ContactShape sh in core.Contacts.Spec.Shapes)
                if (sh.Role == ContactRole.Support && sh.Part == e.Part) return true;
            return false;
        }

        // ---- The push across ---------------------------------------------------------------------------------------------

        public struct Push
        {
            /// <summary>The tips' depth when the push came (m, the deepest of the four); the thrust over weight held; the mean push the
            /// ground held over the last second (N, the contacts' horizontal force); the body's move along the ground from the push's
            /// start to the end (m); the slides' onsets (TipPloughed); crashes and tunnels.</summary>
            public double Depth, Tw, Held, Moved, Onset;
            public int Ploughed, Crashes, Tunnels;
        }

        /// <summary>
        /// Design §4's target 4: the drone touches down level at `down` m/s on surface (disarmed), rests 1 s, is armed and its pilot holds
        /// the thrust at `tw` of the weight (below hover), and a steady wind of `wind` m/s blows across it from the side (body −x to +x)
        /// for 3 s from 3 s on. tips false flies the new drone's tips on the ground's Coulomb (known-wrong).
        /// </summary>
        public static Push Pushed(DroneSpec spec, string surface, double down, double tw, double wind, bool tips = true, double payload = double.NaN)
        {
            double h = down * down / (2 * G);
            FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor(surface), new Double3(0, SoilScenarios.Standing(spec) + h, 0));
            if (!double.IsNaN(payload)) Load(spec, setup, payload);
            if (!tips) setup.Contacts.LegTips = false;
            setup.Wind = WindSetup.SteadyFrom(270, wind);
            setup.Wind.SteadyOnsetSeconds = 3;
            var core = new FlightCore(setup);
            var r = new Push();
            for (int k = 0; k < 1000; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
            CrosswindScenario.Arm(core);
            double throttle = 0;
            Double3 at = default;
            double held = 0;
            int n = 0;
            while (core.State.Step < 6000)
            {
                throttle = DetMath.Min(1, DetMath.Max(0, throttle + Gain * (tw - ThrustOverWeight(core))));
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true, Throttle = throttle });
                for (int i = 0; i < core.Events.Count; i++)
                {
                    EventKind e = core.Events[i].Kind;
                    if (e == EventKind.TipPloughed && core.State.Step > 3000) r.Ploughed++;
                    if (e == EventKind.Crash) r.Crashes++;
                    if (e == EventKind.Tunnel) r.Tunnels++;
                }
                if (core.State.Step == 3000)
                {
                    at = core.State.Position;
                    r.Tw = ThrustOverWeight(core);
                    for (int c = 0; c < 4; c++) r.Depth = DetMath.Max(r.Depth, core.Contacts.TipOf(c).Sink);
                }
                if (core.State.Step > 3000 && core.State.Step <= 3050)
                {
                    Double3 f = core.Trace.ContactForce;
                    r.Onset = Math.Max(r.Onset, Math.Sqrt(f.X * f.X + f.Z * f.Z));
                }
                if (core.State.Step > 5000)
                {
                    Double3 f = core.Trace.ContactForce;
                    held += Math.Sqrt(f.X * f.X + f.Z * f.Z);
                    n++;
                }
            }
            Double3 d = core.State.Position - at;
            r.Moved = Math.Sqrt(d.X * d.X + d.Z * d.Z);
            r.Held = n > 0 ? held / n : 0;
            return r;
        }

        /// <summary>A setup's drone at a payload (kg, the pilot's bottle: 1 Sunny, 2 in Severe rain, design §1), legs on.</summary>
        public static void Load(DroneSpec spec, FlightSetup setup, double payload)
        {
            var l = new Loadout { Payload = payload, Legs = true };
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            bool tips = setup.Contacts.LegTips;
            setup.Mass = spec.BuildMass(l);
            setup.Contacts = spec.BuildContacts(l, materials, hash, setup.Contacts.Surfaces);
            setup.Contacts.LegTips = tips;
        }

        // ---- The hand's touchdown (four_legs.py's, design §3) ---------------------------------------------------------------

        /// <summary>
        /// The hand calculation of a level touchdown with the throttle cut (four_legs.py's touchdown, design §3), from the spec's data:
        /// a quarter of the drone on one leg, its axial spring and damper (legs.stiffness and damping) in series with the tip's soil, a
        /// massless node: the face's loading line K·z, Wong's unloading line at max(K, K·z_m/δ_e), the firm layer, the face's damping
        /// c_s·A_t, the shaft's grip r_s·k_t·πD·z²/2 (and wet c_a·πD·z) against the node's motion, rigid–plastic; the leg touches
        /// while it is compressed. Explicit Euler at 10 µs for 2 s. Returns the node's deepest and final sink (m) and the rebound's
        /// speed (m/s, 0 if the leg never leaves).
        /// </summary>
        public static void HandTouchdown(ContactSpec spec, in SoilSet set, double firmModulus, double mass, double speed, out double deepest, out double end,
            out double rebound)
        {
            double area = Math.PI * spec.TipRadius * spec.TipRadius, back = (1 - spec.TipPoisson * spec.TipPoisson) * area / (2 * spec.TipRadius * spec.TipModulusRatio);
            double kt = set.TipResistance / spec.TipReferenceDepth, ka = kt * area, firm = firmModulus * area;
            double grip = spec.ShaftFriction * kt * Math.PI * spec.ShaftDiameter / 2, wet = set.InterfaceAdhesion * Math.PI * spec.ShaftDiameter;
            double m4 = mass / 4, k = spec.LegStiffness, c = spec.LegDamping, cs = set.Damping * area, dt = 1e-5;
            double delta = 0, vel = -speed, z = 0, zm = 0;
            rebound = 0;
            bool was = true;
            for (double t = 0; t < 2; t += dt)
            {
                double ddelta = -vel, zdot = 0, x = delta - z, fleg = 0;
                bool contact = x > 0;
                if (contact)
                {
                    double fs = 0;
                    if (z > 0)
                    {
                        fs = z >= zm ? ka * z : Math.Max(0, ka * zm - Math.Max(ka, ka * zm / back) * (zm - z));
                        if (z > set.MaxSink) fs += firm * (z - set.MaxSink);
                    }
                    double hold = z > 0 ? grip * z * z + wet * z : 0, drive = k * x + c * ddelta - fs;
                    if (drive > hold) zdot = (drive - hold) / (c + cs);
                    else if (drive < -hold && fs > 0) zdot = (drive + hold) / (c + cs);
                    fleg = Math.Max(0, k * x + c * (ddelta - zdot));
                }
                else if (was && vel > 0 && rebound == 0) rebound = vel;
                was = contact;
                vel += (fleg - m4 * G) / m4 * dt;
                delta -= vel * dt;
                z += zdot * dt;
                if (z > zm) zm = z;
            }
            deepest = zm;
            end = z;
        }

        // ---- The careful liftoff ------------------------------------------------------------------------------------------

        public struct Liftoff
        {
            /// <summary>Each leg's release (s from the ramp's start; NaN held to the end) and its peak hold (N); the thrust over weight
            /// as the last leg let go, the body's upward speed then (m/s) and 0.3 s after (m above the start); the most the ground
            /// pulled the body down (N, the contacts' force below 0); the first to the last release (s); the body's turn toward the
            /// last held leg between the third and the last release (°); the TipFreed events.</summary>
            public double[] Left, Hold;
            public double LastTw, LastUp, Height, Pull, Spread, Turn;

            /// <summary>Each leg's break: when its hold peaked (s; NaN never held), the thrust over weight and the body's upward speed at
            /// the last break (m/s); the work of the thrust's excess over the weight on the body's climb to the last release and the
            /// body's kinetic energy there (J): no stored energy when the second is no more than the first.</summary>
            public double[] Break;
            public double BreakTw, BreakUp, Excess, Kinetic;
            public int Freed, Crashes, Tunnels;
        }

        /// <summary>
        /// A careful liftoff from the start set down on surface (design §4, target 2): armed on the ground, the throttle at rest, then
        /// a pilot who raises the thrust over weight from 0.85 at `rate` per second (the pilot's measured climb: 0.72 /s Sunny, 0.41 /s
        /// in Severe rain) by the throttle, roll, pitch and yaw centred, until the last leg lets go and 0.3 s more. A leg has let go when
        /// its tip leaves the soil (its load at nothing). tips false flies the new drone's tips on the ground's Coulomb (known-wrong);
        /// a drone on skids (SkidsDrone) flies model 105's half-skids.
        /// </summary>
        public static Liftoff Lift(DroneSpec spec, string surface, RainLevel rain, double rate, ulong seed = 0x450F, bool tips = true)
        {
            FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor(surface), new Double3(0, SoilScenarios.Standing(spec), 0), rain, seed);
            if (!tips) setup.Contacts.LegTips = false;
            var core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            for (int k = 0; k < 500; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true });
            var r = new Liftoff
            {
                Left = new double[4], Hold = new double[4], Break = new double[4], LastTw = double.NaN, LastUp = double.NaN, BreakTw = double.NaN,
                BreakUp = double.NaN,
            };
            var breakTw = new double[4];
            var breakUp = new double[4];
            for (int c = 0; c < 4; c++) r.Left[c] = r.Break[c] = double.NaN;
            // The pilot's hand first brings the thrust to 0.85 of the weight (1.5 s), then raises it at the rate.
            double throttle = 0;
            for (int k = 0; k < 1500; k++)
            {
                throttle = DetMath.Min(1, DetMath.Max(0, throttle + Gain * (0.85 - ThrustOverWeight(core))));
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true, Throttle = throttle });
            }
            double y0 = core.State.Position.Y, release = double.NaN;
            bool[] on = new bool[4];
            for (int c = 0; c < 4; c++) on[c] = true;
            int last = -1;
            Double3[] corner = new Double3[4];
            foreach (ContactShape sh in core.Contacts.Spec.Shapes)
                if (sh.Role == ContactRole.Support && sh.Corner >= 0) corner[sh.Corner] = sh.A;
            double leanAtThird = double.NaN;
            const int Steps = 8000;
            for (int k = 0; k < Steps; k++)
            {
                double t = k * FlightCore.Dt, target = 0.85 + rate * t, tw = ThrustOverWeight(core);
                throttle = DetMath.Min(1, DetMath.Max(0, throttle + Gain * (target - tw)));
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true, Throttle = throttle });
                ref readonly FlightState s = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.TipFreed) r.Freed++;
                    if (e.Kind == EventKind.Crash) r.Crashes++;
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                }
                r.Pull = DetMath.Max(r.Pull, -core.Trace.ContactForce.Y);
                int still = 0;
                for (int c = 0; c < 4; c++)
                {
                    double load = core.Trace.LegLoad[c];
                    if (-load > r.Hold[c])
                    {
                        r.Hold[c] = -load;
                        r.Break[c] = t;
                        breakTw[c] = tw;
                        breakUp[c] = s.Velocity.Y;
                    }
                    bool held = load != 0;
                    if (on[c] && !held)
                    {
                        on[c] = false;
                        r.Left[c] = t;
                    }
                    else if (!on[c] && held)
                    {
                        on[c] = true; // it touched again: its release is the next
                        r.Left[c] = double.NaN;
                    }
                    if (on[c]) still++;
                }
                if (still == 1 && double.IsNaN(leanAtThird))
                    for (int c = 0; c < 4; c++)
                        if (on[c])
                        {
                            last = c;
                            leanAtThird = Lean(s, corner[c]);
                        }
                if (still == 1 && last >= 0) r.Turn = DetMath.Max(r.Turn, Lean(s, corner[last]) - leanAtThird);
                if (double.IsNaN(release)) r.Excess += (tw - 1) * Weight(core) * s.Velocity.Y * FlightCore.Dt;
                if (still == 0 && double.IsNaN(release))
                {
                    release = t;
                    r.LastTw = tw;
                    r.LastUp = s.Velocity.Y;
                    r.Kinetic = 0.5 * core.Body.Mass * s.Velocity.Y * s.Velocity.Y;
                }
                if (!double.IsNaN(release) && t >= release + 0.3)
                {
                    r.Height = s.Position.Y - y0;
                    break;
                }
            }
            double first = double.PositiveInfinity, lastLeft = double.NegativeInfinity;
            for (int c = 0; c < 4; c++)
            {
                if (double.IsNaN(r.Left[c])) continue;
                first = DetMath.Min(first, r.Left[c]);
                lastLeft = DetMath.Max(lastLeft, r.Left[c]);
            }
            r.Spread = lastLeft - first;
            double lastBreak = double.NegativeInfinity;
            for (int c = 0; c < 4; c++)
                if (r.Break[c] > lastBreak)
                {
                    lastBreak = r.Break[c];
                    r.BreakTw = breakTw[c];
                    r.BreakUp = breakUp[c];
                }
            return r;
        }

        // The pilot's throttle hand on the thrust over weight's error, per step (assumed: 2 /s, slow against the motors' lag).
        const double Gain = 0.002;

        // How far the body's up axis leans toward a corner's side, degrees.
        static double Lean(in FlightState s, Double3 corner)
        {
            Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0)), w = s.Attitude.Rotate(new Double3(corner.X, 0, corner.Z));
            w = new Double3(w.X, 0, w.Z);
            return Math.Asin(Double3.Dot(up, w) / w.Length()) * 180 / Math.PI;
        }

        /// <summary>One line for a print.</summary>
        public static string Describe(in Touchdown d) =>
            $"touch {d.TouchSpeed:F3} m/s: deepest {d.Deepest * 1e3:F2} mm, end {d.EndSink * 1e3:F2} mm (legs {d.End[0] * 1e3:F2}/{d.End[1] * 1e3:F2}/"
            + $"{d.End[2] * 1e3:F2}/{d.End[3] * 1e3:F2}), peak {d.PeakLoad:F1} N a leg, {d.PeakG:F2} W, rebound {d.Rebound * 1e3:F2} mm, lost {d.Lost}, "
            + $"slide {d.Slide * 1e3:F1} mm, rest {d.Rest:F3} s, ploughed {d.Ploughed}, crashes {d.Crashes}, tunnels {d.Tunnels}";

        public static string Describe(in Liftoff l) =>
            $"released at {l.Left[0]:F3}/{l.Left[1]:F3}/{l.Left[2]:F3}/{l.Left[3]:F3} s (spread {l.Spread * 1e3:F0} ms), holds {l.Hold[0]:F2}/{l.Hold[1]:F2}/"
            + $"{l.Hold[2]:F2}/{l.Hold[3]:F2} N, the ground's pull at most {l.Pull:F2} N; last at T/W {l.LastTw:F3}, up {l.LastUp:F3} m/s, "
            + $"{l.Height * 1e3:F0} mm up 0.3 s after; turned {l.Turn:F2}° toward the last; {l.Freed} TipFreed, crashes {l.Crashes}, tunnels {l.Tunnels}; "
            + $"breaks {l.Break[0]:F3}/{l.Break[1]:F3}/{l.Break[2]:F3}/{l.Break[3]:F3} s, the last at T/W {l.BreakTw:F3} up {l.BreakUp:F3} m/s; "
            + $"the excess's work {l.Excess:F3} J, the kinetic energy at the release {l.Kinetic:F3} J";
    }
}
