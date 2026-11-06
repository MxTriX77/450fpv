using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7f-damage 1.6b's scenarios (prop-motor-damage's "Each motor has a health from heat, dirt and wear"): the repository's drone, its
    /// rear-right motor's heat on a rig at Sky (StrikeScenarios.Sky: nothing to touch), its dirt on the dirt track, and the pilot's
    /// case, D-083: upside down on the dirt track with the throttle held. HeatTests asserts them; tools/flightbench --damage prints
    /// them.
    /// </summary>
    public static class HeatScenarios
    {
        /// <summary>The motor the rigs load: rear right.</summary>
        public const int Corner = 0;

        /// <summary>A jammed rotor's stop, N·m/rad: a rigid hold (the stalled drive's 2.4 N·m turns it 2.4 mrad; assumed).</summary>
        public const double JamStiffness = 1000;

        static readonly string[] Ids = { "rr", "fr", "rl", "fl" };

        /// <summary>
        /// The rig: FlightCore's propulsion at each corner's duty, then its contacts' forces (the bells and the motors' health into the
        /// rotors for the next step), the body held at its start pose, then the sense and the strikes. Jammed holds rr's rotor at its
        /// angle by a stiff stop (Propulsion.SetShaftLoad after each step's forces, as a resting blade's load is put).
        /// </summary>
        public sealed class Rig
        {
            public readonly FlightCore Core;
            public FlightState Now;
            public Corners Duty;
            public readonly bool Jammed;
            public readonly StepEvents Events = new StepEvents(256);
            public readonly List<FlightEvent> Log = new List<FlightEvent>();
            public readonly List<long> LogSteps = new List<long>();
            public StepTrace Trace;
            readonly double _jamAngle;
            readonly Double3 _at;
            readonly Quat _attitude;

            public Rig(FlightSetup setup, Corners duty, bool jammed)
            {
                Core = new FlightCore(setup);
                Now = Core.State;
                Duty = duty;
                Jammed = jammed;
                _at = setup.StartPosition;
                _attitude = setup.StartAttitude;
                for (int i = 0; i < 4; i++)
                {
                    Now.EscCommand[i] = duty[i];
                    Now.RotorSpeed[i] = jammed && i == Corner ? 0 : StrikeScenarios.HoverSpeed;
                }
                Now.Armed = true;
                _jamAngle = Now.RotorAngle[Corner];
                if (jammed) Core.Propulsion.SetShaftLoad(Corner, 0, JamStiffness, 0, _jamAngle);
            }

            /// <summary>rr's winding current over the last step, its root mean square, A.</summary>
            public double Amps => Math.Sqrt(Core.Propulsion.CurrentSquared(Corner) / FlightCore.Dt);

            public MotorHealth Health => Core.Contacts.Health;

            public void Step()
            {
                FlightState s = Now;
                s.BladeStrikes = default;
                Corners angles = s.RotorAngle;
                Propulsion p = Core.Propulsion;
                p.BeginStep(s);
                var loads = new RotorLoads();
                for (int j = 0; j < FlightController.LoopsPerStep; j++) p.SubStep(ref s, Duty, true, FlightController.LoopDt, ref loads);
                Events.Clear();
                Double3 force = default, moment = default;
                StepTrace t = default;
                Core.Contacts.Forces(ref s, Core.Body, ref force, ref moment, ref t, Events);
                Trace = t;
                FlightState was = s;
                s.Position = _at;
                s.Velocity = default;
                s.Attitude = _attitude;
                s.BodyRate = default;
                s.Step++;
                Core.Contacts.Sense(was, ref s, Events);
                Core.Strikes?.Apply(angles, ref s, Core.Body, Events);
                for (int i = 0; i < Events.Count; i++)
                {
                    Log.Add(Events[i]);
                    LogSteps.Add(s.Step);
                }
                Now = s;
                if (Jammed) p.SetShaftLoad(Corner, 0, JamStiffness, 0, _jamAngle);
            }

            /// <summary>The step of the first MotorDamaged of a band on a corner, −1 for none.</summary>
            public long First(int corner, int band)
            {
                for (int i = 0; i < Log.Count; i++)
                {
                    FlightEvent e = Log[i];
                    if (e.Kind == EventKind.MotorDamaged && (e.Index & 0xF) == corner && e.Index >> 4 == band) return LogSteps[i];
                }
                return -1;
            }

            public int Count(EventKind kind)
            {
                int n = 0;
                foreach (FlightEvent e in Log)
                    if (e.Kind == kind) n++;
                return n;
            }
        }
        /// <summary>The rig's setup at Sky, level, no objects; heat false flies the motors without heat and dirt (ContactSpec.MotorHeat
        /// off: model 68's, the known-wrong); limit sets rr's ESC current limit (A; +∞ for the class's none).</summary>
        public static FlightSetup Setup(DroneSpec spec, bool heat = true, double limit = double.PositiveInfinity)
        {
            FlightSetup setup = StrikeScenarios.Setup(spec, StrikeScenarios.Sky, Quat.Identity, null);
            setup.Contacts.MotorHeat = heat;
            setup.Propulsion.Motors[Corner].EscCurrentLimit = limit;
            return setup;
        }

        static Corners Hover(DroneSpec spec, double rr)
        {
            double d = StrikeScenarios.HoverDuty(spec);
            return new Corners { RearRight = rr, FrontRight = d, RearLeft = d, FrontLeft = d };
        }

        // ---- A stalled motor heats and fails ------------------------------------------------------------------------------------

        /// <summary>A jammed rotor's run (prop-motor-damage's "A stalled motor heats and fails").</summary>
        public struct Stall
        {
            public double Limit, Seconds, Amps, HotAt, ShortAt, FailAt, PeakWinding, KtBeforeShort, KtExpected, ClosedFormError, ClosedFormAt, AmbientUsed;
            public int Events;
            public bool Heat;

            public string Describe() =>
                $"rr jammed at full throttle, {(double.IsInfinity(Limit) ? "the class's ESC (no current limit)" : $"a {Limit:F0} A ESC limit")}{(Heat ? "" : ", no heat (the known-wrong)")}: "
                + $"{Amps:F1} A; the magnets past their maximum at {At(HotAt)}, a coil shorted at {At(ShortAt)}, failed at {At(FailAt)} (of {Seconds:F1} s); "
                + $"the windings' peak {PeakWinding:F1} °C; {Events} health events"
                + (double.IsNaN(KtBeforeShort) ? "" : $"; Kt × {KtBeforeShort:F6} before the short (1 + α_B·θ_S + α_B·(T_peak − T_max)⁺: {KtExpected:F6})")
                + (double.IsNaN(ClosedFormError) ? "" : $"; the windings against the two-node closed form: worst {ClosedFormError * 100:F3} % of the rise to {ClosedFormAt:F1} s (ambient conductance {AmbientUsed:F4} W/K)");
        }

        static string At(double t) => double.IsNaN(t) ? "never" : $"{t:F3} s";

        /// <summary>
        /// rr's rotor jammed at Sky (a stiff stop), its duty full, the others at hover, for seconds (or to the motor's failure and
        /// 0.2 s past it); limit rr's ESC limit (A). With a finite limit the stalled winding takes exactly the limit (a current
        /// source), so the windings follow the two-node model's closed form for a constant current (ClosedForm): its worst error
        /// up to the short, over the rise, is printed.
        /// </summary>
        public static Stall Jammed(DroneSpec spec, double limit, bool heat = true, double seconds = 120)
        {
            var rig = new Rig(Setup(spec, heat, limit), Hover(spec, 1), true);
            var r = new Stall { Limit = limit, Heat = heat, HotAt = double.NaN, ShortAt = double.NaN, FailAt = double.NaN, KtBeforeShort = double.NaN,
                KtExpected = double.NaN, ClosedFormError = double.NaN };
            int steps = (int)(seconds * 1000), stop = steps;
            var tw = new List<double>();
            var ts = new List<double>();
            double amps = 0;
            int measured = 0;
            for (int k = 0; k < stop; k++)
            {
                rig.Step();
                if (rig.Health != null)
                {
                    tw.Add(rig.Health.WindingTemperature(Corner));
                    ts.Add(rig.Health.StatorTemperature(Corner));
                    r.PeakWinding = Math.Max(r.PeakWinding, rig.Health.WindingTemperature(Corner));
                    if (rig.Health.State(Corner) < MotorHealth.Shorted)
                    {
                        r.KtBeforeShort = rig.Health.KtFactor(Corner);
                        MotorInternals mi = spec.Internals[Corner];
                        double th = rig.Health.StatorTemperature(Corner) - rig.Core.Contacts.Spec.AirTemperature;
                        double past = Math.Max(0, rig.Health.PeakTemperature(Corner) - mi.MagnetMaxTemperature);
                        r.KtExpected = 1 + mi.RemanenceCoefficient * th + mi.RemanenceCoefficient * past;
                    }
                    if (rig.Health.State(Corner) == MotorHealth.Failed && stop == steps) stop = Math.Min(steps, k + 200);
                }
                if (k < 1000) { amps += rig.Amps; measured++; }
            }
            r.Seconds = stop / 1000.0;
            r.Amps = amps / measured;
            long hot = rig.First(Corner, 5), shorted = rig.First(Corner, 6), failed = rig.First(Corner, 7);
            r.HotAt = hot < 0 ? double.NaN : hot / 1000.0;
            r.ShortAt = shorted < 0 ? double.NaN : shorted / 1000.0;
            r.FailAt = failed < 0 ? double.NaN : failed / 1000.0;
            foreach (FlightEvent e in rig.Log)
                if (e.Kind == EventKind.MotorDamaged && (e.Index & 0xF) == Corner && e.Index >> 4 >= 5) r.Events++;
            if (heat && !double.IsInfinity(limit))
            {
                // The closed form up to the short (or the run's end), with the ambient conductance at the stator's mean rise there.
                int end = shorted < 0 ? tw.Count : (int)shorted - 1;
                double ta = rig.Core.Contacts.Spec.AirTemperature, mean = 0;
                for (int k = 0; k < end; k++) mean += (ts[k] - ta) / end;
                r.AmbientUsed = rig.Health.Ambient(Corner, mean, 0, 0);
                double worst = 0, rise = tw[end - 1] - ta;
                for (int k = 0; k < end; k++)
                {
                    double form = ClosedForm(rig, limit, r.AmbientUsed, (k + 1) * FlightCore.Dt, out _);
                    worst = Math.Max(worst, Math.Abs(tw[k] - ta - form) / rise);
                }
                r.ClosedFormError = worst;
                r.ClosedFormAt = end / 1000.0;
            }
            return r;
        }

        /// <summary>
        /// The two-node model's closed form for rr at a constant current (A), stalled (no air of its own, no mechanical loss), from the
        /// air's temperature: θ' = A·θ + b, A = [[(I²R₂₀α − G − G_w)/C_W, G/C_W], [G/C_S, −(G + G_a)/C_S]], b = [I²R_a/C_W, 0], so
        /// θ(t) = e^(At)·(θ₀ + A⁻¹b) − A⁻¹b with e^(At) by Sylvester's formula over A's two real eigenvalues; the windings' rise at t
        /// (s), K, and the rest's.
        /// </summary>
        public static double ClosedForm(Rig rig, double amps, double ambient, double t, out double stator)
        {
            MotorHealth h = rig.Health;
            ContactSpec cs = rig.Core.Contacts.Spec;
            MotorInternals m = rig.Core.Contacts.Spec.Motors[Corner];
            double i2 = amps * amps, alpha = cs.CopperTemperatureCoefficient, r20 = m.WindingResistance;
            double ra = h.WindingResistanceAt(Corner, cs.AirTemperature);
            double cw = h.WindingCapacity(Corner), cst = h.StatorCapacity(Corner), g = h.WindingConductance(Corner), gw = h.EndTurns(Corner, 0);
            double a11 = (i2 * r20 * alpha - g - gw) / cw, a12 = g / cw, a21 = g / cst, a22 = -(g + ambient) / cst;
            double b1 = i2 * ra / cw;
            double det = a11 * a22 - a12 * a21;
            // The particular solution −A⁻¹b.
            double p1 = -(a22 * b1) / det, p2 = (a21 * b1) / det;
            double tr = a11 + a22, disc = Math.Sqrt(tr * tr / 4 - det), l1 = tr / 2 + disc, l2 = tr / 2 - disc;
            double e1 = Math.Exp(l1 * t), e2 = Math.Exp(l2 * t);
            // e^(At) = (e1·(A − l2·I) − e2·(A − l1·I))/(l1 − l2), on x₀ − p = −p.
            double x1 = -p1, x2 = -p2;
            double m11 = (e1 * (a11 - l2) - e2 * (a11 - l1)) / (l1 - l2), m12 = (e1 - e2) * a12 / (l1 - l2);
            double m21 = (e1 - e2) * a21 / (l1 - l2), m22 = (e1 * (a22 - l2) - e2 * (a22 - l1)) / (l1 - l2);
            stator = m21 * x1 + m22 * x2 + p2;
            return m11 * x1 + m12 * x2 + p1;
        }

        // ---- A spinning motor cools by its own air ------------------------------------------------------------------------------

        /// <summary>The same current turning and stalled (prop-motor-damage's "A spinning motor cools by its own air").</summary>
        public struct Cooling
        {
            public double Seconds, Amps, SpinSpeed, SpinWinding, SpinStator, StallWinding, StallStator, StallAmps;
            public double SpinAir, StallAir, SpinEndTurns, StallEndTurns, SteadySpinWinding, SteadyStallWinding, SteadySpinStator, SteadyStallStator;

            public string Describe() =>
                $"rr at {Amps:F2} A: turning at {SpinSpeed:F0} rad/s the windings {SpinWinding:F1} °C and the stator {SpinStator:F1} °C after {Seconds:F0} s, "
                + $"stalled ({StallAmps:F2} A, its ESC limited to it) {StallWinding:F1} and {StallStator:F1} °C; the rest's conductance to the air "
                + $"{SpinAir:F4} W/K turning against {StallAir:F4} stalled ({(SpinAir - StallAir) / SpinAir * 100:F1} % of it the spin's air), the end turns' "
                + $"{SpinEndTurns:F4} against {StallEndTurns:F4} W/K; settled, {SteadySpinWinding:F1} / {SteadySpinStator:F1} °C turning against "
                + $"{SteadyStallWinding:F1} / {SteadyStallStator:F1} °C stalled";
        }

        /// <summary>
        /// rr turning at hover (the others too) for seconds, its winding's current measured over its first second; and rr jammed with its
        /// ESC limited to that current (so the same current flows stalled), for the same time. Each one's settled temperatures from
        /// the model's own laws (Settle).
        /// </summary>
        public static Cooling Cools(DroneSpec spec, double seconds = 60)
        {
            var spin = new Rig(Setup(spec), Hover(spec, StrikeScenarios.HoverDuty(spec)), false);
            var r = new Cooling { Seconds = seconds };
            double amps = 0, speed = 0;
            int steps = (int)(seconds * 1000);
            for (int k = 0; k < steps; k++)
            {
                spin.Step();
                if (k >= 1000) continue;
                amps += spin.Amps * spin.Amps / 1000;
                speed += spin.Now.RotorSpeed[Corner] / 1000;
            }
            r.Amps = Math.Sqrt(amps);
            r.SpinSpeed = speed;
            r.SpinWinding = spin.Health.WindingTemperature(Corner);
            r.SpinStator = spin.Health.StatorTemperature(Corner);
            var stall = new Rig(Setup(spec, limit: r.Amps), Hover(spec, 1), true);
            double stallAmps = 0;
            for (int k = 0; k < steps; k++)
            {
                stall.Step();
                if (k < 1000) stallAmps += stall.Amps / 1000;
            }
            r.StallAmps = stallAmps;
            r.StallWinding = stall.Health.WindingTemperature(Corner);
            r.StallStator = stall.Health.StatorTemperature(Corner);
            MotorHealth h = spin.Health;
            double ta = spin.Core.Contacts.Spec.AirTemperature;
            r.SpinAir = h.Ambient(Corner, r.SpinStator - ta, speed, 0);
            r.StallAir = h.Ambient(Corner, r.SpinStator - ta, 0, 0);
            r.SpinEndTurns = h.EndTurns(Corner, speed);
            r.StallEndTurns = h.EndTurns(Corner, 0);
            double loss = spin.Core.Propulsion.NoLoadTorque(Corner) * speed;
            Settle(h, r.Amps, speed, loss, ta, out r.SteadySpinWinding, out r.SteadySpinStator);
            Settle(h, r.Amps, 0, 0, ta, out r.SteadyStallWinding, out r.SteadyStallStator);
            return r;
        }

        /// <summary>The model's settled temperatures (°C) for rr at a constant current (A), speed (rad/s) and mechanical loss heat (W):
        /// the two nodes' balance with the copper's resistance and the air's conductances at the temperatures, by fixed point.</summary>
        public static void Settle(MotorHealth h, double amps, double speed, double loss, double ta, out double winding, out double stator)
        {
            double tw = 0, ts = 0, g = h.WindingConductance(Corner), gw = h.EndTurns(Corner, speed);
            for (int k = 0; k < 500; k++)
            {
                double ga = h.Ambient(Corner, ts, speed, 0), p = amps * amps * h.WindingResistanceAt(Corner, ta + tw);
                // tw·(g + gw) − g·ts = p; −g·tw + (g + ga)·ts = loss.
                double a11 = g + gw, a22 = g + ga, det = a11 * a22 - g * g;
                tw = (p * a22 + g * loss) / det;
                ts = (a11 * loss + g * p) / det;
            }
            winding = ta + tw;
            stator = ta + ts;
        }

        /// <summary>The model's laws stepped alone for rr at a constant current (A) and speed (rad/s) with its mechanical loss (W) for
        /// seconds from the air's temperature ta (°C), backward Euler at the flight's step as MotorHealth.Step: the windings' and the
        /// rest's temperatures (°C) at the end. For a published rating's check (information).</summary>
        public static void Burst(MotorHealth h, double alpha, double r20, double amps, double speed, double loss, double ta, double seconds, out double winding, out double stator)
        {
            double tw = 0, ts = 0, dt = FlightCore.Dt, q = amps * amps * dt, ra = h.WindingResistanceAt(Corner, ta);
            double cw = h.WindingCapacity(Corner), cs = h.StatorCapacity(Corner), g = h.WindingConductance(Corner), gw = h.EndTurns(Corner, speed);
            for (int k = 0; k < (int)(seconds / dt); k++)
            {
                double ga = h.Ambient(Corner, ts, speed, 0);
                double a11 = cw + dt * (g + gw) - q * r20 * alpha, a12 = -dt * g, b1 = cw * tw + q * ra;
                double a21 = -dt * g, a22 = cs + dt * (g + ga), b2 = cs * ts + loss * dt;
                double det = a11 * a22 - a12 * a21;
                tw = (b1 * a22 - a12 * b2) / det;
                ts = (a11 * b2 - a21 * b1) / det;
            }
            winding = ta + tw;
            stator = ta + ts;
        }

        // ---- Dirt in the gap drags, then binds ------------------------------------------------------------------------------------

        /// <summary>A motor in the soil (prop-motor-damage's "Dirt in the gap drags, then binds").</summary>
        public struct Fouled
        {
            public string How;
            public double Seconds, Normal, PackedAt, BoundAt, Capacity, Drive, EndSpeed, EndNormal, Duty, FailAt;
            public double[] Times, Mass, Drag;
            public int Bound;
            public bool Heat;

            public string Describe()
            {
                var s = new System.Text.StringBuilder($"{How}{(Heat ? "" : " (no heat nor dirt, the known-wrong)")}, its duty {Duty:F3}: the bell's load {Normal:F2} N; dirt (mg) and its drag (mN·m):");
                for (int i = 0; i < Times.Length; i++) s.Append($" {Times[i] * 1e3:F0} ms {Mass[i] * 1e6:F1} / {Drag[i] * 1e3:F3};");
                s.Append($" the gap's capacity {Capacity * 1e6:F1} mg of solids; packed at {At(PackedAt)}, bound (the bind law, MotorBound) at {At(BoundAt)}, the hover drive {Drive:F3} N·m; bound {Bound} steps; failed by heat at {At(FailAt)}; at the end rr turns at {EndSpeed:F1} rad/s, its bell loaded {EndNormal:F2} N");
                return s.ToString();
            }
        }

        /// <summary>
        /// rr's turning bell pressed into the dirt track: the drone on its right side (rolled 90°, body +x down) held depth into the
        /// soil at its motors' lowest point, its rotors at hover duty, the blades neither striking nor resting (ContactSpec
        /// BladeStrikes and BladeRest off, so only the bells meet the soil), for seconds.
        /// </summary>
        public static Fouled Scraped(DroneSpec spec, double duty, double depth = 0.00005, bool heat = true, double seconds = 3)
        {
            Quat q = new Quat(0, 0, -0.7071067811865476, 0.7071067811865476);
            DronePart motor = spec.Parts[spec.PartIndex("motor_rr")];
            Double3 c = q.Rotate(motor.Position);
            double R = motor.Diameter / 2;
            var at = new Double3(0, -depth + R - c.Y, 0);
            FlightSetup setup = StrikeScenarios.Setup(spec, at, q, null, strikes: false, world: SoilScenarios.Floor("dirt_track"), table: SoilScenarios.Table());
            setup.Contacts.BladeRest = false;
            setup.Contacts.MotorHeat = heat;
            var rig = new Rig(setup, new Corners { RearRight = duty, FrontRight = duty, RearLeft = duty, FrontLeft = duty }, false);
            return Run(rig, "rr's bell scraping the dirt track " + depth * 1e3 + " mm in", seconds, heat);
        }

        static Fouled Run(Rig rig, string how, double seconds, bool heat)
        {
            int steps = (int)(seconds * 1000);
            double[] marks = { 0.001, 0.005, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, seconds };
            var r = new Fouled { How = how, Heat = heat, PackedAt = double.NaN, BoundAt = double.NaN, Times = marks, Mass = new double[marks.Length],
                Drag = new double[marks.Length] };
            int next = 0;
            for (int k = 1; k <= steps; k++)
            {
                rig.Step();
                r.Normal = Math.Max(r.Normal, rig.Core.Contacts.Bells.Normal(Corner));
                // The bind law's flag (Propulsion.Bound: the drive into the stalled winding within the drag; FlightCore logs it as
                // MotorBound, which the rig does not run).
                if ((rig.Core.Propulsion.Bound & 1 << Corner) != 0)
                {
                    if (double.IsNaN(r.BoundAt)) r.BoundAt = k / 1000.0;
                    r.Bound++;
                }
                for (int i = 0; i < rig.Events.Count; i++)
                    if (rig.Events[i].Kind == EventKind.MotorDamaged && rig.Events[i].Index == (Corner | 8 << 4) && double.IsNaN(r.PackedAt)) r.PackedAt = k / 1000.0;
                while (next < marks.Length && k >= (int)Math.Round(marks[next] * 1000))
                {
                    r.Mass[next] = rig.Health != null ? rig.Health.DirtMass(Corner) : 0;
                    r.Drag[next] = rig.Health != null ? rig.Health.DirtDrag(Corner) : 0;
                    next++;
                }
            }
            r.Seconds = seconds;
            r.EndSpeed = rig.Now.RotorSpeed[Corner];
            r.EndNormal = rig.Core.Contacts.Bells.Normal(Corner);
            r.Duty = rig.Duty[Corner];
            long failed = rig.First(Corner, 7);
            r.FailAt = failed < 0 ? double.NaN : failed / 1000.0;
            r.Capacity = rig.Health != null ? rig.Health.DirtCapacity(Corner) : 0;
            Propulsion p = rig.Core.Propulsion;
            r.Drive = p.TorqueConstant(Corner) * rig.Duty[Corner] * p.BusVolts / p.Resistance(Corner);
            return r;
        }

        // ---- Upside down on the ground with throttle, the motors fail one after another --------------------------------------------

        /// <summary>The pilot's case at one throttle (D-083).</summary>
        public struct Upturned
        {
            public double Throttle, Seconds;
            public double[] FailAt, ShortAt, PackedAt, Amps, PeakWinding, Dirt, ShortestTip;
            public int[] Passes, InSoil, Bound;
            public string Resting;
            public int Tunnels;
            public bool Airmode, PitchTip;

            public string Describe()
            {
                var s = new System.Text.StringBuilder($"throttle {Throttle:F2} for {Seconds:F0} s, airmode {(Airmode ? "on" : "off")}, {(PitchTip ? "pitched (model 70's start)" : "rolled")} {TipDegrees}°, resting on{Resting}:");
                for (int c = 0; c < 4; c++)
                    s.Append($"\n      {Ids[c]}: fails at {At(FailAt[c])} (shorted {At(ShortAt[c])}), {Amps[c]:F1} A mean, the windings' peak {PeakWinding[c]:F0} °C, "
                        + $"{Passes[c]} blade passes ({InSoil[c]} in the soil), the shortest blade {ShortestTip[c] * 127:F1} mm, dirt {Dirt[c] * 1e6:F0} mg (packed {At(PackedAt[c])}), {Bound[c]} binds");
                s.Append($"\n      {Tunnels} tunnels");
                return s.ToString();
            }
        }

        /// <summary>The drone's tip: inverted (rolled 180°) and then rolled TipDegrees about world z onto its left side, so it rests on its
        /// payload and its left motors and props, as a drone upside down on the ground lies (its payload holds the flat inversion's props
        /// off the soil). A static rest by the geometry: armed at zero throttle it stays there with airmode and without (probed, model
        /// 73). Until model 73 the tip was about world x (pitch, pitchTip): the drop lands it flat on its payload alone, and only
        /// airmode's zero-throttle mix (its desaturation lifting the collective) rolled it onto its left motors in the settle's
        /// 1.0–1.4 s; without airmode (D-086) it lies flat, its props clear of the soil.</summary>
        public const double TipDegrees = 24;

        /// <summary>The pilot's case's setup: the drone upside down and tipped TipDegrees, 3 mm over the dirt track (Sunny, surfaces.json's
        /// soil); heat false without the motors' heat and dirt; airmode Betaflight's airmode (on: model 70's controller; null:
        /// FcSettings.Default's, off since model 69, D-086); pitchTip the tip about world x, model 70's start.</summary>
        public static FlightSetup UpsideSetup(DroneSpec spec, bool heat = true, ulong seed = 0x450F, bool? airmode = null, bool pitchTip = false)
        {
            FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor("dirt_track"), default, seed: seed);
            setup.Contacts.MotorHeat = heat;
            if (airmode != null) setup.Settings.airmode = airmode.Value;
            double h = TipDegrees * Math.PI / 360;
            Quat q = (pitchTip ? new Quat(Math.Sin(h), 0, 0, Math.Cos(h)) : new Quat(0, 0, -Math.Sin(h), Math.Cos(h))) * new Quat(0, 0, 1, 0);
            double low = 0;
            foreach (ContactShape s in setup.Contacts.Shapes)
            {
                if (s.Role == ContactRole.Disc) continue;
                low = Math.Min(low, Math.Min(q.Rotate(s.A).Y, q.Rotate(s.B).Y) - s.Radius);
            }
            setup.StartPosition = new Double3(0, -low + 0.003, 0);
            setup.StartAttitude = q;
            return setup;
        }

        /// <summary>
        /// D-083's case: the drone dropped upside down onto the dirt track (Sunny, surfaces.json's soil; tipped TipDegrees), armed with the
        /// throttle down for 2 s to settle, then the throttle held at throttle for seconds, the sticks centred (acro, the controller's
        /// own mixing). Each motor's failure (MotorDamaged band 7) and short (6) time from the throttle's start, its mean current, its
        /// windings' peak, its blades' passes and the soil's share, its dirt and binds. heat false: no heat nor dirt (the known-wrong
        /// below is in HeatTests: a fixed timer).
        /// </summary>
        public static Upturned Upside(DroneSpec spec, double throttle, bool heat = true, double seconds = 30, ulong seed = 0x450F, bool? airmode = null,
            bool pitchTip = false)
        {
            var core = new FlightCore(UpsideSetup(spec, heat, seed, airmode, pitchTip));
            var r = new Upturned
            {
                Airmode = core.Settings.airmode, PitchTip = pitchTip,
                Throttle = throttle, Seconds = seconds, FailAt = Nan(), ShortAt = Nan(), PackedAt = Nan(), Amps = new double[4], PeakWinding = new double[4],
                Dirt = new double[4], ShortestTip = new double[4], Passes = new int[4], InSoil = new int[4], Bound = new int[4],
            };
            const int settle = 2000;
            int steps = settle + (int)(seconds * 1000);
            var rest = new bool[256];
            for (int k = 0; k < steps; k++)
            {
                var stick = new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8, Throttle = k < settle ? 0 : throttle };
                core.Advance(stick);
                double t = (k + 1 - settle) / 1000.0;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    int c = e.Index & 0xF, band = e.Index >> 4;
                    if (e.Kind == EventKind.MotorDamaged && c < 4 && band == 7 && double.IsNaN(r.FailAt[c])) r.FailAt[c] = t;
                    if (e.Kind == EventKind.MotorDamaged && c < 4 && band == 6 && double.IsNaN(r.ShortAt[c])) r.ShortAt[c] = t;
                    if (e.Kind == EventKind.MotorDamaged && c < 4 && band == 8 && double.IsNaN(r.PackedAt[c])) r.PackedAt[c] = t;
                    if (e.Kind == EventKind.MotorBound && e.Index < 4 && e.A > 0) r.Bound[e.Index]++;
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.BladeStrike && k >= settle)
                    {
                        int corner = Array.IndexOf(Ids, spec.Parts[e.Part].Name.Substring(5));
                        if (corner < 0) continue;
                        r.Passes[corner]++;
                        if (e.Object == WorldIds.Ground || e.Surface != WorldIds.NoSurface) r.InSoil[corner]++;
                    }
                }
                if (k < settle) continue;
                for (int c = 0; c < 4; c++)
                {
                    r.Amps[c] += Math.Sqrt(core.Propulsion.CurrentSquared(c) / FlightCore.Dt) / (steps - settle);
                    if (core.Contacts.Health != null) r.PeakWinding[c] = Math.Max(r.PeakWinding[c], core.Contacts.Health.WindingTemperature(c));
                }
                if (k == settle)
                    for (int i = 0; i < core.Contacts.ShapeCount; i++)
                        if (core.Contacts.Touching(i) && core.Contacts.Shape(i).Role == ContactRole.Body) rest[i] = true;
            }
            var names = new System.Text.StringBuilder();
            for (int i = 0; i < core.Contacts.ShapeCount; i++)
                if (rest[i]) names.Append(" " + core.Contacts.Shape(i).Name);
            r.Resting = names.ToString();
            for (int c = 0; c < 4; c++)
            {
                r.Dirt[c] = core.Contacts.Health != null ? core.Contacts.Health.DirtMass(c) : 0;
                r.ShortestTip[c] = Math.Min(core.Propulsion.Rotor(c).Blade(0).TipRadius, core.Propulsion.Rotor(c).Blade(1).TipRadius);
            }
            return r;
        }

        static double[] Nan() => new[] { double.NaN, double.NaN, double.NaN, double.NaN };
    }
}
