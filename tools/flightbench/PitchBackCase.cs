using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.FlightBench
{
    // The climb after a pitch-back at a fixed throttle (D-087: "after pitch backward it's kind taking a long time to adjust
    // altitude"). `--pitch-back` flies, headless on the sim core, the session's drone (RateTrackingCase.Setup: drone.json at
    // the loadout, legs on, the coil full, a full pack, Calm, every airframe effect) in a cruise at 30° of nose-down pitch
    // with its height held on the throttle, then holds that cruise's throttle and pitches back to 20°, 10° and 0°, the
    // attitude held as a pilot's hands hold it. It prints the vertical speed and the height against time, the climb rate's
    // rise and settling times, the vertical force's terms, and the closed-form time constant τ = m / D from the rotors' and
    // the body's vertical damping at the state (D = −∂F_y/∂v_y), split by term.
    internal static class PitchBackCase
    {
        const double G = 9.80665, Deg = Math.PI / 180;
        const double CruiseSeconds = 24, TrimSeconds = 4, PreSeconds = 1, RecordSeconds = 12, StartHeight = 400;
        const double AttitudeGain = 10, MaxRate = 400; // rad/s per rad of error, °/s: a pilot's quick, clean pitch-back
        static readonly double[] Payloads = { 0, 1, 2 };
        static readonly double[] Targets = { 20, 10, 0 };
        static readonly double[] Marks = { 0.1, 0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4, 6, 8, 10, 12 };
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        internal sealed class Sample
        {
            public double T, Vz, Rise, Vh, Pitch, Rpm, Inflow, InPlane, Rotor, HForce, Body, Weight, Throttle;
        }

        internal sealed class Flight
        {
            public double Payload, From, To, Mass, Trim, CruiseSpeed, CruiseRpm, CruiseInflow, PreDrift;
            public bool Edgewise;
            public List<Sample> Samples = new List<Sample>();
            public double Peak, PeakAt, Rise63, Rise90, Settle10, Final, FinalRise;
            public Damping AtStart, AtHalf, AtEnd;
        }

        /// <summary>The vertical damping D = −∂F_y/∂v_y at a state, N per m/s, by term (closed form at the state).</summary>
        internal sealed class Damping
        {
            public double RotorHeld, MotorBack, Rotor, HForce, Body, Total, Tau, Mass;
            public double Closed; // the rotors' held-speed slope from the blade elements in combined flow alone (published BET)
        }

        // A pilot's hands in acro: the body's up turned toward the wanted tilt at a rate proportional to its error (capped),
        // the heading held, the sticks the controller's own curve maps to those rates, at the TX12's 250 Hz.
        sealed class Hands
        {
            readonly FlightController _fc;
            public double Pitch; // nose-down, rad
            public double Throttle;
            int _count;
            StickSample _last;

            public Hands(FlightController fc) { _fc = fc; }

            public StickSample Sticks(in FlightState s, long hostUs)
            {
                if (_count++ % 4 != 0)
                {
                    _last.HostUs = hostUs;
                    _last.Throttle = Throttle;
                    return _last;
                }
                var want = new Double3(0, Math.Cos(Pitch), Math.Sin(Pitch));
                Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
                Double3 turn = s.Attitude.InverseRotate(Double3.Cross(up, want)) * AttitudeGain;
                double cap = MaxRate * Deg, n = turn.Length();
                if (n > cap) turn = turn * (cap / n);
                Double3 nose = s.Attitude.Rotate(new Double3(0, 0, 1));
                double h = Math.Sqrt(nose.X * nose.X + nose.Z * nose.Z);
                double heading = h > 1e-6 ? -nose.X / h : 0;
                Double3 rate = turn + s.Attitude.InverseRotate(new Double3(0, 2 * heading, 0));
                const double R2D = 180 / Math.PI;
                return _last = new StickSample
                {
                    HostUs = hostUs, Arm = true, Throttle = Throttle,
                    Roll = Stick(0, -rate.Z * R2D), Pitch = Stick(1, rate.X * R2D), Yaw = Stick(2, rate.Y * R2D),
                };
            }

            double Stick(int axis, double rate)
            {
                double lo = -1, hi = 1;
                if (!(rate < _fc.SetpointOf(axis, 1))) return 1;
                if (!(rate > _fc.SetpointOf(axis, -1))) return -1;
                for (int i = 0; i < 40; i++)
                {
                    double mid = (lo + hi) / 2;
                    if (_fc.SetpointOf(axis, mid) < rate) lo = mid; else hi = mid;
                }
                return (lo + hi) / 2;
            }
        }

        static double PitchOf(in FlightState s)
        {
            Double3 up = s.Attitude.Rotate(new Double3(0, 1, 0));
            return Math.Atan2(up.Z, up.Y) / Deg;
        }

        static Sample Take(FlightCore core, FlightSetup setup, double t, double y0, double throttle)
        {
            ref readonly FlightState s = ref core.State;
            ref readonly StepTrace tr = ref core.Trace;
            Propulsion p = core.Propulsion;
            double mass = setup.Mass.Properties.Mass, rpm = 0, inflow = 0, inPlane = 0, hy = 0;
            for (int i = 0; i < 4; i++)
            {
                double w = s.RotorSpeed[i];
                rpm += w * 60 / (2 * Math.PI) / 4;
                inflow += p.Inflow[i] / 4;
                Double3 ip = p.InPlane(i);
                inPlane += Math.Sqrt(ip.X * ip.X + ip.Z * ip.Z) / 4;
                hy += s.Attitude.Rotate(p.HForce(i, w)).Y;
            }
            return new Sample
            {
                T = t, Vz = s.Velocity.Y, Rise = s.Position.Y - y0, Vh = Math.Sqrt(s.Velocity.X * s.Velocity.X + s.Velocity.Z * s.Velocity.Z),
                Pitch = PitchOf(s), Rpm = rpm, Inflow = inflow, InPlane = inPlane, Throttle = throttle,
                Rotor = tr.NetForce.Y - tr.AeroForce.Y + mass * G, HForce = hy, Body = tr.AeroForce.Y - hy, Weight = -mass * G,
            };
        }

        /// <summary>The vertical damping at the core's present state, closed form: each rotor's thrust slope against its axial
        /// inflow at its speed (the props' table, Propulsion.Aero), the motor's speed-up as the inflow unloads it at a held
        /// duty (dω/dv_a from the motor's −k_e²/R against the air torque's slope), the H-force's and the body's and coil's
        /// drag slopes along world y. A vertical velocity dv_y puts dv_y·cos θ through each disc and dv_y·sin θ in its plane.</summary>
        internal static Damping DampingAt(FlightCore core, FlightSetup setup)
        {
            ref readonly FlightState s = ref core.State;
            Propulsion p = core.Propulsion;
            Double3 upBody = s.Attitude.InverseRotate(new Double3(0, 1, 0)); // world up in body axes
            double c = upBody.Y, sn = Math.Sqrt(Math.Max(0, 1 - c * c));
            var d = new Damping { Mass = setup.Mass.Properties.Mass };
            for (int i = 0; i < 4; i++)
            {
                double w = s.RotorSpeed[i], va = p.Inflow[i], dv = 0.05, dw = 1;
                Double3 ipv = p.InPlane(i);
                double vp = Math.Sqrt(ipv.X * ipv.X + ipv.Z * ipv.Z);
                p.Aero(w, va + dv, vp, out double t1, out double q1);
                p.Aero(w, va - dv, vp, out double t0, out double q0);
                p.Aero(w + dw, va, vp, out double t3, out double q3);
                p.Aero(w - dw, va, vp, out double t2, out double q2);
                double dTdv = (t1 - t0) / (2 * dv), dQdv = (q1 - q0) / (2 * dv), dTdw = (t3 - t2) / (2 * dw), dQdw = (q3 - q2) / (2 * dw);
                double ke = p.TorqueConstant(i), r = p.Resistance(i);
                double dwdv = dQdv / (-ke * ke / r - dQdw); // the held duty's speed-up (> 0: the unloaded prop spins up)
                d.RotorHeld += -dTdv * c * c;
                if (setup.Flapping != null && w > 0)
                {
                    // Glauert's momentum on the blade elements (RotorFlappingSpec.CombinedFlow): dT/dV_a = ρ·A·ΩR·dC_T/dλ_c.
                    // The closed form: the props' measured axial slope at the rotor's J times the blade elements' own factor for
                    // the edgewise flow on it, dC_T/dλ_c at μ over dC_T/dλ_c at 0 (Glauert's inflow; Padfield's Z_w in combined
                    // flow), so the check is of the edgewise physics and its tables, not of the blade elements' axial fit.
                    double vt = w * setup.Flapping.Radius, dl = 1e-4;
                    setup.Flapping.CombinedFlow(va / vt + dl, vp / vt, out double c1, out _, out _);
                    setup.Flapping.CombinedFlow(va / vt - dl, vp / vt, out double c0, out _, out _);
                    setup.Flapping.CombinedFlow(va / vt + dl, 0, out double a1, out _, out _);
                    setup.Flapping.CombinedFlow(va / vt - dl, 0, out double a0, out _, out _);
                    p.Aero(w, va + dv, out double ta1, out _);
                    p.Aero(w, va - dv, out double ta0, out _);
                    d.Closed += -(ta1 - ta0) / (2 * dv) * (c1 - c0) / (a1 - a0) * c * c;
                }
                d.MotorBack += -dTdw * dwdv * c * c;
                Double3 ip = p.InPlane(i), hf = p.HForce(i, w);
                double ipn = Math.Sqrt(ip.X * ip.X + ip.Z * ip.Z), hfn = Math.Sqrt(hf.X * hf.X + hf.Z * hf.Z);
                if (ipn > 1e-9) d.HForce += hfn / ipn * sn * sn;
            }
            d.Rotor = d.RotorHeld + d.MotorBack;
            // The body's and the coil's drag along world y, at the body's velocity (the held attitude turns slowly).
            var aero = new Aero(setup.Aero);
            Quat att = s.Attitude;
            Double3 v = att.InverseRotate(s.Velocity);
            const double e = 0.01;
            double Fy(Double3 vb)
            {
                Double3 f = aero.Body(vb);
                if (setup.Aero.CoilPart >= 0) f = f + aero.Coil(vb, setup.Mass.Part(setup.Aero.CoilPart));
                return att.Rotate(f).Y;
            }
            d.Body = -(Fy(v + upBody * e) - Fy(v - upBody * e)) / (2 * e);
            d.Total = d.Rotor + d.HForce + d.Body;
            d.Tau = d.Mass / d.Total;
            return d;
        }

        internal static Flight Fly(FlightSetup setup, double from, double to, Action<FlightCore> afterCruise = null)
        {
            var core = new FlightCore(setup);
            FlightController fc = core.Controller;
            long Us() => core.State.Step * FlightCore.StepUs;
            for (int k = 0; k < 8; k++) core.Advance(new StickSample { HostUs = Us(), Arm = false });
            for (int k = 0; k < 8; k++) core.Advance(new StickSample { HostUs = Us(), Arm = true });
            var hands = new Hands(fc) { Pitch = from * Deg, Throttle = 0.5 };
            double y0 = core.State.Position.Y, integral = 0, trim = 0;
            int cruise = (int)(CruiseSeconds * 1000), trimFrom = cruise - (int)(TrimSeconds * 1000), n = 0;
            for (int k = 0; k < cruise; k++)
            {
                ref readonly FlightState s = ref core.State;
                if (k % 4 == 0)
                {
                    // The height on the throttle: a PI on the height with the climb rate's damping (the cruise only).
                    double err = y0 - s.Position.Y;
                    integral += err * 0.004;
                    hands.Throttle = Math.Min(1, Math.Max(0, 0.5 + 0.08 * err - 0.12 * s.Velocity.Y + 0.03 * integral));
                }
                core.Advance(hands.Sticks(core.State, Us()));
                if (k >= trimFrom)
                {
                    trim += hands.Throttle;
                    n++;
                }
            }
            trim /= n;
            hands.Throttle = trim;
            var f = new Flight { From = from, To = to, Mass = setup.Mass.Properties.Mass, Trim = trim };
            afterCruise?.Invoke(core);
            double yPre = core.State.Position.Y;
            for (int k = 0; k < PreSeconds * 1000; k++) core.Advance(hands.Sticks(core.State, Us()));
            Sample pre = Take(core, setup, 0, y0, trim);
            f.CruiseSpeed = pre.Vh;
            f.CruiseRpm = pre.Rpm;
            f.CruiseInflow = pre.Inflow;
            f.PreDrift = (core.State.Position.Y - yPre) / PreSeconds;
            f.AtStart = DampingAt(core, setup);
            double yStart = core.State.Position.Y;
            hands.Pitch = to * Deg;
            int record = (int)(RecordSeconds * 1000);
            var vz = new double[record];
            for (int k = 0; k < record; k++)
            {
                core.Advance(hands.Sticks(core.State, Us()));
                double t = (k + 1) * FlightCore.Dt;
                vz[k] = core.State.Velocity.Y;
                if (k == 499) f.AtHalf = DampingAt(core, setup);
                foreach (double m in Marks)
                    if (Math.Abs(t - m) < 1e-9) f.Samples.Add(Take(core, setup, t, yStart, trim));
            }
            f.AtEnd = DampingAt(core, setup);
            double v0 = pre.Vz;
            f.Final = vz[record - 1];
            f.FinalRise = core.State.Position.Y - yStart;
            f.Peak = v0;
            for (int k = 0; k < record; k++)
                if (vz[k] > f.Peak) { f.Peak = vz[k]; f.PeakAt = (k + 1) * FlightCore.Dt; }
            f.Rise63 = f.Rise90 = f.Settle10 = double.NaN;
            double span = f.Peak - v0;
            for (int k = 0; k < record; k++)
            {
                double t = (k + 1) * FlightCore.Dt;
                if (double.IsNaN(f.Rise63) && vz[k] - v0 >= 0.632 * span) f.Rise63 = t;
                if (double.IsNaN(f.Rise90) && vz[k] - v0 >= 0.9 * span) f.Rise90 = t;
            }
            // Settled: the last time the climb rate is more than 10 % of its peak's rise away from its value at the end.
            for (int k = record - 1; k >= 0; k--)
                if (Math.Abs(vz[k] - f.Final) > 0.1 * Math.Max(span, 1e-3)) { f.Settle10 = (k + 2) * FlightCore.Dt; break; }
            return f;
        }

        static string F(double x, string fmt) => double.IsNaN(x) ? "-" : x.ToString(fmt, C);

        public static bool Run()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            RateTrackingCase.SessionSetupPrepare(spec);
            FcSettings fc = RateTrackingCase.PlayerDefault();
            var jobs = new List<(double payload, double to, bool edgewise)>();
            foreach (bool e in new[] { true, false })
                foreach (double p in Payloads)
                    foreach (double t in Targets) jobs.Add((p, t, e));
            var flights = new Flight[jobs.Count];
            Parallel.For(0, jobs.Count, i =>
            {
                FlightSetup setup = RateTrackingCase.Setup(spec, jobs[i].payload, fc, 0x450F);
                setup.StartPosition = new Double3(0, StartHeight, 0);
                setup.Flapping.EdgewiseThrust = jobs[i].edgewise;
                Flight f = Fly(setup, 30, jobs[i].to);
                f.Payload = jobs[i].payload;
                f.Edgewise = jobs[i].edgewise;
                flights[i] = f;
            });
            Console.WriteLine($"pitch-back: the session's drone (legs on, the coil full, a full pack, Calm, the player default), {StartHeight:0} m up, "
                + $"a cruise at 30° nose-down with the height held on the throttle for {CruiseSeconds:0} s (the trim the last {TrimSeconds:0} s' mean), "
                + $"{PreSeconds:0} s at that throttle, then the pitch-back at t = 0 with the throttle held; model 76 (the edgewise flow) and the known-wrong, model 75's axial-only rotor");
            bool ok = true;
            foreach (Flight f in flights)
            {
                Console.WriteLine();
                Console.WriteLine($"## {(f.Edgewise ? "model 76" : "known-wrong (axial only)")}, {f.Payload:0} kg, 30° → {f.To:0}°: {f.Mass:0.000} kg, throttle {100 * f.Trim:0.0} %, cruise {f.CruiseSpeed:0.00} m/s at {f.CruiseRpm:0} rpm, axial inflow {f.CruiseInflow:0.00} m/s, drift before {f.PreDrift:0.000} m/s");
                Console.WriteLine($"climb rate: peak {F(f.Peak, "0.00")} m/s at {F(f.PeakAt, "0.00")} s, 63 % of it at {F(f.Rise63, "0.00")} s, 90 % at {F(f.Rise90, "0.00")} s, within 10 % of the end's for good from {F(f.Settle10, "0.00")} s; at {RecordSeconds:0} s {F(f.Final, "0.00")} m/s, risen {F(f.FinalRise, "0.0")} m");
                foreach (Damping d in new[] { f.AtStart, f.AtHalf, f.AtEnd })
                {
                    Console.WriteLine($"  D {(d == f.AtStart ? "at the pitch-back" : d == f.AtHalf ? "at 0.5 s" : $"at {RecordSeconds:0} s")}: rotors at held speed {d.RotorHeld:0.000} (blade elements in combined flow {d.Closed:0.000}), motors' speed-up {d.MotorBack:0.000}, H-force {d.HForce:0.000}, body and coil {d.Body:0.000}; total {d.Total:0.000} N per m/s, τ = m/D {d.Tau:0.00} s");
                    // The closed form: the sim's rotor slope against the blade elements' own in combined flow, within 10 %.
                    bool near = Math.Abs(d.RotorHeld / d.Closed - 1) <= 0.10;
                    if (f.Edgewise && !near) { Console.WriteLine("  FAIL: the rotors' climb damping is not the blade elements' in combined flow (10 %)"); ok = false; }
                    if (!f.Edgewise) Console.WriteLine($"  the known-wrong's rotor slope over the closed form: {d.RotorHeld / d.Closed:0.000}");
                }
                Console.WriteLine("| t s | vz m/s | rise m | v_h m/s | pitch ° | rpm | axial inflow m/s | in-plane m/s | rotors' F_y N | H-force F_y | body F_y | weight |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (Sample s in f.Samples)
                    Console.WriteLine($"| {s.T:0.00} | {s.Vz:0.00} | {s.Rise:0.00} | {s.Vh:0.00} | {s.Pitch:0.0} | {s.Rpm:0} | {s.Inflow:0.00} | {s.InPlane:0.00} | {s.Rotor:0.00} | {s.HForce:0.00} | {s.Body:0.00} | {s.Weight:0.00} |");
            }
            // The known-wrong must fail the closed form where the edgewise flow is strongest: level discs at speed (30° → 0°, at
            // the pitch-back's end of the attitude move the in-plane flow is the cruise's).
            Console.WriteLine();
            Console.WriteLine("| Load | Pitch-back | t63 m76 | t63 m75 | τ m76 (0.5 s / 12 s) | τ m75 | peak m76 at | peak m75 at | 12 s m76 | 12 s m75 | trim m76 | trim m75 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            int half = flights.Length / 2;
            for (int i = 0; i < half; i++)
            {
                Flight a = flights[i], b = flights[i + half];
                Console.WriteLine($"| {a.Payload:0} kg | 30° → {a.To:0}° | {F(a.Rise63, "0.00")} s | {F(b.Rise63, "0.00")} s | {a.AtHalf.Tau:0.00} / {a.AtEnd.Tau:0.00} s | {b.AtHalf.Tau:0.00} / {b.AtEnd.Tau:0.00} s | {F(a.Peak, "0.00")} m/s, {F(a.PeakAt, "0.00")} s | {F(b.Peak, "0.00")} m/s, {F(b.PeakAt, "0.00")} s | {F(a.Final, "0.00")} | {F(b.Final, "0.00")} | {100 * a.Trim:0.0} % | {100 * b.Trim:0.0} % |");
                if (a.To == 0)
                {
                    // Level discs: the translational lift fades as the speed bleeds, so the climb rate peaks and falls (model 76);
                    // the axial-only rotor's thrust ignores the speed, so its climb rate rises for the whole record.
                    bool peaks = a.PeakAt < 8 && a.Final < 0.97 * a.Peak, wrongPeaks = b.PeakAt < 8 && b.Final < 0.97 * b.Peak;
                    bool wrongMisses = Math.Abs(b.AtHalf.RotorHeld / b.AtHalf.Closed - 1) > 0.10;
                    if (!peaks) { Console.WriteLine($"FAIL: {a.Payload:0} kg 30° → 0°: the climb rate does not peak and fall"); ok = false; }
                    if (wrongPeaks || !wrongMisses) { Console.WriteLine($"FAIL: {a.Payload:0} kg 30° → 0°: the known-wrong passes"); ok = false; }
                }
            }
            Console.WriteLine(ok ? "pitch-back: PASS" : "pitch-back: FAIL");
            return ok;
        }
    }
}
