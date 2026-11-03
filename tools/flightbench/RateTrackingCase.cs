using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;

namespace Fpv450.FlightBench
{
    // How the drone tracks the rates its flight controller asks for (reference-notes/rate-tracking.md; the pilot's
    // "yaw and roll are super slow", D-076). `--rate-tracking` flies, headless on the sim core, the session's own drone
    // (the player's SessionSetup.Build: drone.json at the loadout, legs on, the coil full, a full pack, Calm, every
    // airframe effect) held in a hover 100 m up by the closed-loop pilot (SimPilot), then a stick step on one axis at the
    // hover's trimmed throttle; it prints the setpoint against the achieved rate, then the same steps with one element
    // changed at a time (the limits). `--rate-log <log>...` reads the pilot's own flight logs the same way: how far the
    // sticks went, how the achieved rate followed the setpoint, and how often the mixer saturated.
    internal static class RateTrackingCase
    {
        const double HoverSeconds = 6, CruiseSeconds = 10, QuietSeconds = 0.2, AfterSeconds = 0.6;
        static readonly ulong[] Seeds = { 0x450F, 0x4510, 0x4511 };
        static readonly string[] AxisName = { "roll", "pitch", "yaw" };
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        /// <summary>
        /// The player's default (D-076; fc-profiles §2b; FcSettingsStore.PlayerDefault on unity/game/p6-input-app--player)
        /// on the sim core's FcSettings.Default, but for the throttle curve: the player's is thr_mid 100 / thr_expo 36,
        /// which unity/main's FlightController cannot build (its 12th point divides by zero; the player branch guards it).
        /// The curve maps only the throttle stick to the mixer's throttle, and every throttle-dependent part of the loop
        /// (TPA, anti-gravity, the dynamic filters, the mixer) reads the mixer's throttle, which the hover sets; so the
        /// hover is flown on a straight curve, as LiftoffTests does, and the response is the player default's.
        /// </summary>
        public static FcSettings PlayerDefault()
        {
            FcSettings s = FcSettings.Default;
            s.roll_rc_rate = s.pitch_rc_rate = 5; s.roll_srate = s.pitch_srate = 22; s.roll_expo = s.pitch_expo = 45;
            s.yaw_rc_rate = 8; s.yaw_srate = 24; s.yaw_expo = 20;
            s.thr_mid = 50; s.thr_expo = 0;
            s.deadband = 4; s.yaw_deadband = 5;
            s.rc_smoothing_auto_factor = 80;
            s.f_roll = 80; s.f_pitch = 80; s.f_yaw = 0;
            s.feedforward_transition = 40; s.feedforward_jitter_factor = 12; s.feedforward_boost = 0;
            return s;
        }

        /// <summary>Model 26 as the pilot flew it: their fc-settings.json of the sim default column (FcSettings.Default's
        /// rates, deadband, smoothing and feedforward), the throttle curve straight as above.</summary>
        static FcSettings PilotsModel26()
        {
            FcSettings s = FcSettings.Default;
            s.thr_mid = 50; s.thr_expo = 0;
            return s;
        }

        /// <summary>The session's flight (the player branch's SessionSetup.Build) on the floor world, 100 m up.</summary>
        static FlightSetup Setup(DroneSpec spec, double payload, in FcSettings settings, ulong seed)
        {
            var loadout = new Loadout { Payload = payload, Legs = true, CoilPaidOut = 0 };
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            return new FlightSetup
            {
                Seed = seed, Settings = settings, World = FlightWorld.Floor(),
                Mass = spec.BuildMass(loadout), Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(),
                Wind = WindSetup.Of(WindLevel.Calm), CameraPosition = spec.CameraPosition,
                Contacts = spec.BuildContacts(loadout, materials, hash),
                Asymmetry = spec.BuildAsymmetry(), Placement = spec.BuildPlacement(), VortexRing = spec.BuildVortexRing(),
                GroundEffect = spec.BuildGroundEffect(), RotorWake = spec.BuildRotorWake(), Flapping = spec.BuildFlapping(),
                Interference = spec.BuildInterference(), Modes = spec.BuildModes(),
                StartPosition = new Double3(0, 100, 0), StartPackCharge = 1,
            };
        }

        // One element changed at a time.
        sealed class Variant
        {
            public string Name;
            public Action<FlightSetup> Setup;
            public Func<FcSettings, FcSettings> Settings;
            public bool YawOnly, HalfToo;
        }

        static void ScaleInertia(FlightSetup s, double k)
        {
            for (int i = 0; i < s.Propulsion.Motors.Length; i++) s.Propulsion.Motors[i].RotorInertia *= k;
        }

        static readonly Variant[] Variants =
        {
            new Variant { Name = "as built" },
            new Variant { Name = "Betaflight's yaw I: x2.5 (pid_init.c)", YawOnly = true, Settings = s => { s.i_yaw *= 2.5; return s; } },
            new Variant { Name = "no gyro or D-term filters", Settings = s =>
            {
                s.gyro_lpf1_dyn_min_hz = s.gyro_lpf1_static_hz = s.gyro_lpf2_static_hz = 0;
                s.gyro_notch1_hz = s.gyro_notch2_hz = 0;
                s.dyn_notch_count = 0;
                s.dshot_bidir = false;
                s.dterm_notch_hz = s.dterm_lpf1_dyn_min_hz = s.dterm_lpf1_static_hz = s.dterm_lpf2_static_hz = 0;
                return s;
            } },
            new Variant { Name = "feedforward off", HalfToo = true, Settings = s => { s.f_roll = s.f_pitch = 0; return s; } },
            new Variant { Name = "P, I, D, D min x1.5", Settings = s =>
            {
                s.p_roll *= 1.5; s.i_roll *= 1.5; s.d_roll *= 1.5; s.d_min_roll *= 1.5;
                s.p_pitch *= 1.5; s.i_pitch *= 1.5; s.d_pitch *= 1.5; s.d_min_pitch *= 1.5;
                s.p_yaw *= 1.5; s.i_yaw *= 1.5;
                return s;
            } },
            new Variant { Name = "rotor inertia x0.907 (p7f 1.2's blades)", Setup = s => ScaleInertia(s, (2 * 2.12e-5 + 1.2e-5) / 6e-5) },
            new Variant { Name = "rotor inertia x0.5", Setup = s => ScaleInertia(s, 0.5) },
            new Variant { Name = "D-064's spreads off (motors and wakes)", Setup = s => { s.Asymmetry = null; s.RotorWake = null; } },
            new Variant { Name = "rigid airframe (no modes)", Setup = s => s.Modes = null },
            new Variant { Name = "airmode off", Settings = s => { s.airmode = false; return s; } },
            new Variant { Name = "pidsum_limit_yaw 1000", YawOnly = true, Settings = s => { s.pidsum_limit_yaw = 1000; return s; } },
            // The pilot's other builds, as Betaflight flies them (the yaw I x2.5): the OTU10 configs' set (the section-1 set
            // x1.2 on roll and pitch, feedforward 80), and the yaw of the Beshketnyk 10x8 configs (P 50, I 85, F 100).
            new Variant { Name = "OTU10's PIDs, Betaflight's yaw I", Settings = s =>
            {
                s.p_roll = 78; s.i_roll = 24; s.d_roll = 84; s.d_min_roll = 72;
                s.p_pitch = 72; s.i_pitch = 24; s.d_pitch = 78; s.d_min_pitch = 72;
                s.i_yaw *= 2.5;
                return s;
            } },
            new Variant { Name = "Beshketnyk 10x8's yaw, Betaflight's yaw I", YawOnly = true, Settings = s => { s.p_yaw = 50; s.i_yaw = 85 * 2.5; s.f_yaw = 100; return s; } },
            new Variant { Name = "drag torque kappa x1.25", YawOnly = true, Setup = s => s.Propulsion.Props.TorqueRatio *= 1.25 },
        };

        sealed class Result
        {
            public double Target, Hover, MaxRate;
            public double Rise, SetpointRise, Peak, Overshoot, SteadyError, RmsError, Behind;
            public double Turn, TurnIdeal, TurnSetpoint, Stop, Reverse, PeakAccel, Reach30;
            public double SatShare, FullShare, IdleShare;
            public double Mass, Inertia;
        }

        // Flies one step: hover, a quiet moment with the sticks centred, then the stick on axis for hold s, then centred.
        static Result Fly(FlightSetup setup, int axis, double stick, double hold, double turn, double cruise)
        {
            var core = new FlightCore(setup);
            FlightController fc = core.Controller;
            long Us() => core.State.Step * FlightCore.StepUs;
            for (int k = 0; k < 8; k++) core.Advance(new StickSample { HostUs = Us(), Arm = false });
            for (int k = 0; k < 8; k++) core.Advance(new StickSample { HostUs = Us(), Arm = true });
            var pilot = new SimPilot(fc, core.State.Position);
            if (cruise > 0)
            {
                pilot.HoldVelocity = true;
                pilot.TargetVelocity = new Double3(0, 0, cruise);
            }
            int hover = (int)((cruise > 0 ? CruiseSeconds : HoverSeconds) * 1000);
            double trim = 0, mixer = 0;
            int n = 0;
            for (int k = 0; k < hover; k++)
            {
                StickSample st = pilot.Sticks(core, Us());
                core.Advance(st);
                if (k < hover - 1000) continue;
                trim += st.Throttle;
                mixer += (core.State.Fc.Throttle - 1000) / 1000;
                n++;
            }
            trim /= n;
            mixer /= n;
            for (int k = 0; k < QuietSeconds * 1000 || core.State.Step % 4 != 0; k++)
                core.Advance(new StickSample { HostUs = Us(), Arm = true, Throttle = trim });

            int holdSteps = (int)(hold * 1000), total = holdSteps + (int)(AfterSeconds * 1000);
            var raw = new double[total];
            var sp = new double[total];
            var rate = new double[total];
            bool[] saturated = new bool[4];
            int satSteps = 0, full = 0, idle = 0;
            double idleDuty = fc.DutyOf(0) + 1e-9;
            for (int k = 0; k < total; k++)
            {
                var st = new StickSample { HostUs = Us(), Arm = true, Throttle = trim };
                if (k < holdSteps)
                {
                    if (axis == 0) st.Roll = stick;
                    else if (axis == 1) st.Pitch = stick;
                    else st.Yaw = stick;
                }
                core.Advance(st);
                ref readonly FlightState s = ref core.State;
                raw[k] = s.Fc.RawSetpoint[axis];
                sp[k] = s.Fc.Setpoint[axis];
                rate[k] = FlightCore.Gyro(s.BodyRate)[axis];
                for (int i = 0; i < core.Events.Count; i++)
                {
                    ref readonly FlightEvent e = ref core.Events[i];
                    if (e.Kind == EventKind.MixerSaturation && e.Index < 4) saturated[e.Index] = e.A > 0;
                }
                if (k < holdSteps)
                {
                    if (saturated[axis]) satSteps++;
                    bool f = false, z = false;
                    for (int i = 0; i < 4; i++)
                    {
                        if (core.Trace.RotorCommand[i] >= 0.9999) f = true;
                        if (core.Trace.RotorCommand[i] <= idleDuty) z = true;
                    }
                    if (f) full++;
                    if (z) idle++;
                }
            }

            MassProperties body = setup.Mass.Properties;
            var r = new Result
            {
                Target = fc.SetpointOf(axis, stick), Hover = mixer, Mass = body.Mass,
                Inertia = axis == 0 ? body.Inertia.ZZ : axis == 1 ? body.Inertia.XX : body.Inertia.YY,
                SatShare = (double)satSteps / holdSteps, FullShare = (double)full / holdSteps, IdleShare = (double)idle / holdSteps,
                MaxRate = fc.SetpointOf(axis, 1),
            };
            Measure(r, raw, sp, rate, holdSteps, turn);
            return r;
        }

        static void Measure(Result r, double[] raw, double[] sp, double[] rate, int hold, double turn)
        {
            double target = r.Target;
            r.Rise = r.SetpointRise = r.Turn = r.TurnIdeal = r.TurnSetpoint = r.Stop = r.Reach30 = double.NaN;
            double angle = 0, angleSp = 0, peak = 0, sum = 0, err = 0, accel = 0;
            int steady = 0, errN = 0;
            for (int k = 0; k < hold; k++)
            {
                double t = (k + 1) * FlightCore.Dt;
                if (double.IsNaN(r.Rise) && rate[k] >= 0.9 * target) r.Rise = t;
                if (double.IsNaN(r.SetpointRise) && sp[k] >= 0.9 * target) r.SetpointRise = t;
                if (double.IsNaN(r.Reach30) && rate[k] >= MoveLevel * r.MaxRate) r.Reach30 = t;
                peak = Math.Max(peak, rate[k]);
                angle += rate[k] * FlightCore.Dt;
                angleSp += sp[k] * FlightCore.Dt;
                if (turn > 0 && double.IsNaN(r.Turn) && angle >= turn) r.Turn = t;
                if (turn > 0 && double.IsNaN(r.TurnSetpoint) && angleSp >= turn) r.TurnSetpoint = t;
                if (t >= 0.5)
                {
                    sum += rate[k];
                    steady++;
                }
                if (t <= 0.5)
                {
                    err += (sp[k] - rate[k]) * (sp[k] - rate[k]);
                    errN++;
                }
                if (k >= 5) accel = Math.Max(accel, (rate[k] - rate[k - 5]) / (5 * FlightCore.Dt));
            }
            r.Peak = peak;
            r.Overshoot = (peak - target) / target;
            r.SteadyError = steady > 0 ? (sum / steady - target) / target : double.NaN;
            r.RmsError = Math.Sqrt(err / errN);
            r.PeakAccel = accel;
            r.TurnIdeal = turn > 0 ? turn / target : double.NaN;

            // How far the turn is behind the stick's own (raw) setpoint at the hold's end, as time at the target rate: the
            // equivalent delay of the whole response (the rise less what the overshoot gives back).
            double behind = 0;
            for (int k = 0; k < hold; k++) behind += (raw[k] - rate[k]) * FlightCore.Dt;
            r.Behind = behind / target;

            // The stop after the stick is centred: until the rate is within 10 % of the target of zero, and its swing past zero.
            double reverse = 0;
            for (int k = hold; k < rate.Length; k++)
            {
                if (double.IsNaN(r.Stop) && Math.Abs(rate[k]) <= 0.1 * target)
                {
                    bool stays = true;
                    for (int j = k; j < Math.Min(rate.Length, k + 50); j++) stays &= Math.Abs(rate[j]) <= 0.1 * target;
                    if (stays) r.Stop = (k - hold + 1) * FlightCore.Dt;
                }
                reverse = Math.Min(reverse, rate[k]);
            }
            r.Reverse = -reverse / target;
        }

        static Result Mean(Result[] runs)
        {
            var m = new Result();
            foreach (var f in typeof(Result).GetFields())
            {
                double s = 0;
                foreach (Result r in runs) s += (double)f.GetValue(r);
                f.SetValue(m, s / runs.Length);
            }
            return m;
        }

        static string Ms(double s) => double.IsNaN(s) ? "never" : (1000 * s).ToString("0", C);
        static string F(double x, string f) => double.IsNaN(x) ? "-" : x.ToString(f, C);
        static string Pc(double x) => double.IsNaN(x) ? "-" : (100 * x).ToString("0.0", C);

        sealed class Job
        {
            public string Settings;
            public FcSettings Fc;
            public Variant Variant;
            public double Payload, Stick, Cruise;
            public int Axis;
            public Result[] Runs = new Result[Seeds.Length];
        }

        static double Hold(int axis, double stick) => stick >= 1 ? (axis == 2 ? 1.2 : 2.0) : 1.0;

        static double Turn(int axis, double stick) => stick >= 1 ? (axis == 2 ? 180 : 360) : 45;

        public static bool Run()
        {
            DroneSpec spec = PoweredFlightCase.Drone();
            SessionSetupPrepare(spec);
            var jobs = new List<Job>();
            FcSettings player = PlayerDefault(), pilots = PilotsModel26();
            foreach (double payload in new[] { 0.0, 1.0, 2.0 })
                for (int axis = 0; axis < 3; axis++)
                    foreach (double stick in new[] { 1.0, 0.5 })
                    {
                        jobs.Add(new Job { Settings = "player default", Fc = player, Variant = Variants[0], Payload = payload, Axis = axis, Stick = stick });
                        jobs.Add(new Job { Settings = "model 26 as flown", Fc = pilots, Variant = Variants[0], Payload = payload, Axis = axis, Stick = stick });
                        if (axis == 2)
                            jobs.Add(new Job { Settings = "player default, Betaflight's yaw I", Fc = player, Variant = Variants[1], Payload = payload, Axis = axis, Stick = stick });
                        if (payload > 0)
                            jobs.Add(new Job { Settings = "player default, cruise 18 m/s", Fc = player, Variant = Variants[0], Payload = payload, Axis = axis, Stick = stick, Cruise = 18 });
                    }
            foreach (double payload in new[] { 1.0, 2.0 })
                for (int v = 1; v < Variants.Length; v++)
                    for (int axis = 0; axis < 3; axis++)
                    {
                        if (Variants[v].YawOnly && axis != 2) continue;
                        jobs.Add(new Job { Settings = "player default", Fc = player, Variant = Variants[v], Payload = payload, Axis = axis, Stick = 1 });
                        if (Variants[v].HalfToo)
                            jobs.Add(new Job { Settings = "player default", Fc = player, Variant = Variants[v], Payload = payload, Axis = axis, Stick = 0.5 });
                    }
            var flights = new List<(Job job, int seed)>();
            foreach (Job j in jobs)
                for (int s = 0; s < Seeds.Length; s++) flights.Add((j, s));
            Parallel.For(0, flights.Count, i =>
            {
                (Job j, int s) = flights[i];
                FcSettings fc = j.Variant.Settings != null ? j.Variant.Settings(j.Fc) : j.Fc;
                FlightSetup setup = Setup(spec, j.Payload, fc, Seeds[s]);
                j.Variant.Setup?.Invoke(setup);
                j.Runs[s] = Fly(setup, j.Axis, j.Stick, Hold(j.Axis, j.Stick), Turn(j.Axis, j.Stick), j.Cruise);
            });

            Console.WriteLine($"rate tracking: the session's drone (legs on, the coil full, a full pack, Calm), hovered {HoverSeconds:0} s 100 m up, "
                + $"then a stick step at the hover's throttle; each cell the mean of {Seeds.Length} seeds (0x{Seeds[0]:X}..0x{Seeds[Seeds.Length - 1]:X}); rates in deg/s, times in ms");
            foreach (string settings in new[] { "player default", "model 26 as flown", "player default, Betaflight's yaw I", "player default, cruise 18 m/s" })
            {
                Console.WriteLine();
                Console.WriteLine($"## {settings}");
                Console.WriteLine("| Bottle | Axis | Stick | Setpoint | Mixer thr % | t90 setpoint | t to 30 % of full | t90 rate | Peak | Overshoot % | Steady err % | RMS err 0-0.5 s | Behind at hold end | Turn | Ideal | Setpoint's | Stop | Swing back % | Peak accel | Axis sat % | Motor full % | Motor idle % |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (Job j in jobs)
                {
                    bool yawI = settings.EndsWith("yaw I", StringComparison.Ordinal);
                    if (j.Settings != settings || j.Variant != (yawI ? Variants[1] : Variants[0])) continue;
                    Result r = Mean(j.Runs);
                    double turn = Turn(j.Axis, j.Stick);
                    Console.WriteLine($"| {j.Payload:0} kg | {AxisName[j.Axis]} | {j.Stick:0.0} | {F(r.Target, "0.0")} | {Pc(r.Hover)} | {Ms(r.SetpointRise)} | {Ms(r.Reach30)} | {Ms(r.Rise)} | "
                        + $"{F(r.Peak, "0.0")} | {Pc(r.Overshoot)} | {Pc(r.SteadyError)} | {F(r.RmsError, "0.0")} | {Ms(r.Behind)} | {turn:0} deg {Ms(r.Turn)} | {Ms(r.TurnIdeal)} | {Ms(r.TurnSetpoint)} | "
                        + $"{Ms(r.Stop)} | {Pc(r.Reverse)} | {F(r.PeakAccel, "0")} | {Pc(r.SatShare)} | {Pc(r.FullShare)} | {Pc(r.IdleShare)} |");
                }
            }
            Console.WriteLine();
            Console.WriteLine("## the limits: full stick, the player default, one element changed");
            Console.WriteLine("| Bottle | Axis | Variant | t90 rate | Peak | Overshoot % | Steady err % | Behind at hold end | Turn | Stop | Peak accel | Axis sat % | Motor full % | Motor idle % |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (double payload in new[] { 1.0, 2.0 })
                for (int axis = 0; axis < 3; axis++)
                    foreach (Job j in jobs)
                    {
                        if (j.Payload != payload || j.Axis != axis || j.Settings != "player default" || j.Cruise > 0) continue;
                        if (j.Stick != 1 && !(j.Variant.HalfToo || j.Variant == Variants[0])) continue;
                        Result r = Mean(j.Runs);
                        string name = j.Stick != 1 ? j.Variant.Name + " (half stick)" : j.Variant.Name;
                        Console.WriteLine($"| {j.Payload:0} kg | {AxisName[j.Axis]} | {name} | {Ms(r.Rise)} | {F(r.Peak, "0.0")} | {Pc(r.Overshoot)} | {Pc(r.SteadyError)} | {Ms(r.Behind)} | "
                            + $"{Ms(r.Turn)} | {Ms(r.Stop)} | {F(r.PeakAccel, "0")} | {Pc(r.SatShare)} | {Pc(r.FullShare)} | {Pc(r.IdleShare)} |");
                    }
            Console.WriteLine();
            foreach (double payload in new[] { 0.0, 1.0, 2.0 })
            {
                Result r0 = Mean(Find(jobs, payload, 0)), r1 = Mean(Find(jobs, payload, 1)), r2 = Mean(Find(jobs, payload, 2));
                Console.WriteLine($"{payload:0} kg bottle: {F(r0.Mass, "0.000")} kg, inertia roll {F(r0.Inertia, "0.0000")}, pitch {F(r1.Inertia, "0.0000")}, yaw {F(r2.Inertia, "0.0000")} kg.m2");
            }
            return true;
        }

        static Result[] Find(List<Job> jobs, double payload, int axis)
        {
            foreach (Job j in jobs)
                if (j.Payload == payload && j.Axis == axis && j.Variant == Variants[0] && j.Settings == "player default" && j.Stick == 1 && j.Cruise == 0) return j.Runs;
            throw new InvalidOperationException();
        }

        // The rotor interference's table, built once for the process (SessionSetup.Prepare), so the parallel flights share it.
        static void SessionSetupPrepare(DroneSpec spec)
        {
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = spec.Motors[i].Position;
            spec.BuildInterference().Table(hubs);
        }

        // ---- The pilot's own flights ----

        /// <summary>Reads each log: per axis, the sticks' reach, the setpoint against the achieved rate in free flight, the
        /// delay from the stick's setpoint to the drone's rate in each of the pilot's moves, and the mixer's saturation.</summary>
        public static bool RunLogs(string[] paths)
        {
            Console.WriteLine($"rate tracking in flight logs: free flight only (armed, not crashed, no contact within {Clear} ms either side); rates in deg/s. "
                + $"A move: the stick's setpoint rising through {100 * MoveLevel:0} % of the axis's full rate after {QuietBefore} ms below half that; its delay: until the rate reaches the same level (within {MoveWindow} ms)");
            Console.WriteLine("| Log | Bottle | Wind | Free s | Axis | Rates (centre/full/expo) | Stick max | Stick >= 0.95 % | Stick >= 0.5 % | Setpoint max | Rate max | Best lag | Gain at lag | Moves | Delay median | Delay 90 % | Not reached % | Rate peak / setpoint peak (median) | Axis sat % | Any motor full % |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var pooled = new List<double>[3];
            var pooledPeak = new List<double>[3];
            var pooledMissed = new int[3];
            for (int a = 0; a < 3; a++) { pooled[a] = new List<double>(); pooledPeak[a] = new List<double>(); }
            int index = 0;
            foreach (string path in paths)
            {
                index++;
                FlightLogReader reader;
                try { reader = FlightLogReader.Open(path); }
                catch (FlightLogException e) { Console.WriteLine($"| {index} | unreadable: {e.Message} |"); continue; }
                using (reader)
                {
                    LogHeader h = reader.Header;
                    var stick = new List<double>[3];
                    var raw = new List<double>[3];
                    var rate = new List<double>[3];
                    var satAt = new List<bool>[3];
                    for (int a = 0; a < 3; a++) { stick[a] = new List<double>(); raw[a] = new List<double>(); rate[a] = new List<double>(); satAt[a] = new List<bool>(); }
                    var sat = new bool[4];
                    var flying = new List<bool>();
                    var touching = new List<bool>();
                    var fullAt = new List<bool>();
                    while (reader.Next())
                    {
                        if (reader.Kind == LogFormat.StepBlock)
                        {
                            reader.ReadStep(out LoggedStep step);
                            flying.Add(step.State.Armed && !step.State.Crashed);
                            touching.Add(step.Trace.ContactsTouching > 0);
                            Axes3 g = FlightCore.Gyro(step.State.BodyRate);
                            for (int a = 0; a < 3; a++)
                            {
                                stick[a].Add(a == 0 ? step.Stick.Roll : a == 1 ? step.Stick.Pitch : step.Stick.Yaw);
                                raw[a].Add(step.State.Fc.RawSetpoint[a]);
                                rate[a].Add(g[a]);
                                satAt[a].Add(sat[a]);
                            }
                            bool f = false;
                            for (int i = 0; i < 4; i++) f |= step.Trace.RotorCommand[i] >= 0.9999;
                            fullAt.Add(f);
                        }
                        else
                        {
                            reader.ReadEvent(out LoggedEvent e);
                            if (e.Event.Kind == EventKind.MixerSaturation && e.Event.Index < 4) sat[e.Event.Index] = e.Event.A > 0;
                        }
                    }
                    // Free flight: flying, and no contact within Clear steps either side.
                    int n = flying.Count, lastTouch = int.MinValue / 2, next = int.MaxValue / 2;
                    var free = new bool[n];
                    var nextTouch = new int[n];
                    for (int k = n - 1; k >= 0; k--)
                    {
                        if (touching[k]) next = k;
                        nextTouch[k] = next;
                    }
                    int count = 0, full = 0;
                    for (int k = 0; k < n; k++)
                    {
                        if (touching[k]) lastTouch = k;
                        free[k] = flying[k] && k - lastTouch > Clear && nextTouch[k] - k > Clear;
                        if (!free[k]) continue;
                        count++;
                        if (fullAt[k]) full++;
                    }
                    string wind = h.Wind.Level.ToString();
                    for (int a = 0; a < 3; a++)
                        LogAxis(index, h, wind, count, a, stick[a], raw[a], rate[a], satAt[a], free, full, pooled[a], pooledPeak[a], ref pooledMissed[a]);
                }
            }
            Console.WriteLine();
            Console.WriteLine("| All logs | Axis | Moves | Delay median | Delay 90 % | Not reached % | Rate peak / setpoint peak (median) |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            for (int a = 0; a < 3; a++)
            {
                pooled[a].Sort();
                pooledPeak[a].Sort();
                int moves = pooled[a].Count + pooledMissed[a];
                Console.WriteLine($"| pooled | {AxisName[a]} | {moves} | {Ms(Quantile(pooled[a], 0.5))} | {Ms(Quantile(pooled[a], 0.9))} | "
                    + $"{Pc(moves > 0 ? (double)pooledMissed[a] / moves : double.NaN)} | {F(Quantile(pooledPeak[a], 0.5), "0.00")} |");
            }
            return true;
        }

        const int Clear = 1000, QuietBefore = 100, MoveWindow = 500;
        const double MoveLevel = 0.3;

        static double Quantile(List<double> sorted, double q) => sorted.Count == 0 ? double.NaN : sorted[(int)(q * (sorted.Count - 1))];

        static double Setting(LogHeader h, string name)
        {
            foreach (SettingValue v in h.Settings)
                if (v.Name == name) return BitConverter.Int64BitsToDouble(v.Bits);
            return double.NaN;
        }

        static void LogAxis(int index, LogHeader h, string wind, int count, int a, List<double> stick, List<double> raw, List<double> rate,
            List<bool> satAt, bool[] free, int full, List<double> pooled, List<double> pooledPeak, ref int pooledMissed)
        {
            string p = AxisName[a];
            double fullRate = Setting(h, p + "_srate") * 10;
            string rates = $"{Setting(h, p + "_rc_rate") * 10:0}/{fullRate:0}/{Setting(h, p + "_expo") / 100:0.00}";
            double stickMax = 0, spMax = 0, rateMax = 0;
            int over95 = 0, over50 = 0, satSteps = 0, n = free.Length;
            for (int k = 0; k < n; k++)
            {
                if (!free[k]) continue;
                double s = Math.Abs(stick[k]);
                stickMax = Math.Max(stickMax, s);
                if (s >= 0.95) over95++;
                if (s >= 0.5) over50++;
                if (satAt[k]) satSteps++;
                spMax = Math.Max(spMax, Math.Abs(raw[k]));
                rateMax = Math.Max(rateMax, Math.Abs(rate[k]));
            }
            // The lag: the shift (0–200 ms) that best lays the free-flight rate on the stick's setpoint, and the gain there.
            double best = double.MaxValue, bestLag = double.NaN, gain = double.NaN;
            for (int lag = 0; lag <= 200; lag += 2)
            {
                double e = 0, xy = 0, xx = 0;
                int m = 0;
                for (int k = lag; k < n; k++)
                {
                    if (!free[k] || !free[k - lag]) continue;
                    double x = raw[k - lag], y = rate[k];
                    e += (y - x) * (y - x);
                    xy += x * y;
                    xx += x * x;
                    m++;
                }
                if (m == 0) continue;
                e /= m;
                if (e < best)
                {
                    best = e;
                    bestLag = lag * FlightCore.Dt;
                    gain = xx > 0 ? xy / xx : double.NaN;
                }
            }
            // The moves: the stick's setpoint rising through the level after QuietBefore steps below half of it; the delay
            // until the rate reaches the level the same way, and the rate's peak over the setpoint's within the window.
            double level = MoveLevel * fullRate;
            var delays = new List<double>();
            var peaks = new List<double>();
            int missed = 0, below = 0;
            for (int k = 1; k + MoveWindow < n; k++)
            {
                if (!free[k]) { below = 0; continue; }
                double x = Math.Abs(raw[k]);
                if (x < 0.5 * level) { below++; continue; }
                if (x < level) continue;
                bool start = below >= QuietBefore;
                below = 0;
                if (!start) continue;
                int sign = raw[k] > 0 ? 1 : -1, reached = -1;
                double spPeak = 0, ratePeak = 0;
                bool clean = true;
                for (int j = k; j < k + MoveWindow; j++)
                {
                    if (!free[j]) { clean = false; break; }
                    spPeak = Math.Max(spPeak, raw[j] * sign);
                    ratePeak = Math.Max(ratePeak, rate[j] * sign);
                    if (reached < 0 && rate[j] * sign >= level) reached = j;
                }
                if (!clean) continue;
                if (reached < 0) missed++;
                else delays.Add((reached - k) * FlightCore.Dt);
                peaks.Add(ratePeak / spPeak);
            }
            delays.Sort();
            peaks.Sort();
            pooled.AddRange(delays);
            pooledPeak.AddRange(peaks);
            pooledMissed += missed;
            int moves = delays.Count + missed;
            Console.WriteLine($"| {index} | {h.Loadout.Payload:0.0} kg | {wind} | {count / 1000.0:0} | {p} | {rates} | {stickMax:0.00} | {Pc((double)over95 / count)} | {Pc((double)over50 / count)} | "
                + $"{spMax:0} | {rateMax:0} | {Ms(bestLag)} | {F(gain, "0.00")} | {moves} | {Ms(Quantile(delays, 0.5))} | {Ms(Quantile(delays, 0.9))} | "
                + $"{Pc(moves > 0 ? (double)missed / moves : double.NaN)} | {F(Quantile(peaks, 0.5), "0.00")} | {Pc((double)satSteps / count)} | {(a == 0 ? Pc((double)full / count) : "")} |");
        }
    }
}
