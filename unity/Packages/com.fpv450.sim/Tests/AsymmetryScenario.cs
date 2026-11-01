using System.Collections.Generic;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// airframe-effects' "Asymmetry on and off" (p7b-airframe 1.1): the repository's drone at its nominal loadout in still
    /// air over the flat floor, armed in the air 300 m up and hovered hands-off for Seconds: the throttle holds the height
    /// (SimPilot's throttle law), roll, pitch and yaw stay centred, so the rate controller alone holds the attitude, and
    /// whatever roll and heading the motors' asymmetry turns it through is the correction a pilot has to make: the
    /// correction rate is that roll and heading over the hover's 30 s. The drone is symmetric left to right, so with
    /// identical rotors it needs none (only the pitch trim of its forward CoM, the same every flight).
    ///
    /// The band (design 1) is computed from the tolerances, not flown. Predict linearises each motor's steady state at the
    /// hover's duty and bus voltage (the DC motor against the prop's torque, Propulsion.Aero): the thrust's and torque's
    /// slopes against Kv and duty give the roll and yaw moments a draw's Kv spread makes at equal duties, and the I terms
    /// that hold them (the mixer's duty per PID unit); the yank each costs is the rate error the I term integrated to
    /// get there, I / ki (Betaflight's ki). BandOf runs it over 20 000 seeds' draws. The ESC lag spread and the props'
    /// unbalance add no steady moment (the unbalance's mean over a turn is zero); the unbalance keeps the body vibrating
    /// at the rotors' frequencies, never still, its rate computed per rotor. The hands-off drone drifts off sideways, and
    /// the drift's own air (the rotors' in-plane drag at their hubs above the CoM) takes a growing share of the roll
    /// moment, about 14 % at 4 s and a third by 30 s, so each seed's prediction is checked at YankAt, before it does.
    /// The suite asserts it (AsymmetryTests); tools/flightbench --scenarios prints it.
    /// </summary>
    public static class AsymmetryScenario
    {
        public const double Seconds = 30, MeasuredFrom = 10, YankAt = 4;
        const double RadToDeg = 57.29577951308232, TwoPi = 6.283185307179586;

        public struct Result
        {
            /// <summary>At the end: the bank (roll) and pitch angles and the nose's turn from its start, degrees, signed.</summary>
            public double RollDeg, PitchDeg, HeadingDeg;

            /// <summary>The roll and yaw correction rates the hover needs, °/s: the roll and heading over the hover.</summary>
            public double RollRate, YawRate;

            /// <summary>At YankAt, after the spin-up's yank and before the drift's air: the roll angle and the heading,
            /// degrees, and the I terms.</summary>
            public double YankRollDeg, YankHeadingDeg;
            public Axes3 YankITerm;

            /// <summary>The horizontal drift at the end from the start point, m: sideways (body x at the start) and in all.</summary>
            public double SideDrift, Drift;

            /// <summary>The controller's I terms at the end, PID units: the moments it carries.</summary>
            public Axes3 ITerm;

            /// <summary>Over the last Seconds − MeasuredFrom: the body rate's step-to-step change, rad/s, its RMS in the
            /// quietest and the busiest 1 s block and its largest (a buzz is a large one); the largest body rate, rad/s (a
            /// divergence is a large one); the largest step-to-step change of any duty (the ESCs' command).</summary>
            public double VibrationMin, VibrationMax, RateJump, RateMax, DutyJump;

            /// <summary>The hover's operating point at the end: the mean duty, the bus voltage, the mean rotor speed.</summary>
            public double Duty, Volts, Speed;
        }

        public static FlightSetup Setup(DroneSpec spec, ulong seed, bool asymmetry)
        {
            FlightSetup setup = CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Still), seed);
            if (asymmetry) setup.Asymmetry = spec.BuildAsymmetry();
            return setup;
        }

        public static Result Hover(FlightSetup setup) => Hover(setup, out _);

        public static Result Hover(FlightSetup setup, out FlightCore core)
        {
            core = new FlightCore(setup);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            var r = new Result { VibrationMin = double.MaxValue };
            Double3 last = core.State.BodyRate;
            Corners duty = core.State.EscCommand;
            int steps = (int)(Seconds * 1000), from = (int)(MeasuredFrom * 1000), yank = (int)(YankAt * 1000);
            double block = 0;
            for (int k = 0; k < steps; k++)
            {
                StickSample sticks = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                sticks.Roll = sticks.Pitch = sticks.Yaw = 0;
                core.Advance(sticks);
                ref readonly FlightState s = ref core.State;
                Double3 w = s.BodyRate;
                if (k == yank - 1)
                {
                    Angles(s, out r.YankRollDeg, out _, out r.YankHeadingDeg);
                    r.YankITerm = s.Fc.ITerm;
                }
                if (k >= from)
                {
                    double jump = (w - last).Length();
                    r.RateJump = DetMath.Max(r.RateJump, jump);
                    r.RateMax = DetMath.Max(r.RateMax, w.Length());
                    for (int i = 0; i < 4; i++) r.DutyJump = DetMath.Max(r.DutyJump, DetMath.Abs(s.EscCommand[i] - duty[i]));
                    block += jump * jump;
                    if ((k - from + 1) % 1000 == 0)
                    {
                        double rms = DetMath.Sqrt(block / 1000);
                        r.VibrationMin = DetMath.Min(r.VibrationMin, rms);
                        r.VibrationMax = DetMath.Max(r.VibrationMax, rms);
                        block = 0;
                    }
                }
                last = w;
                duty = s.EscCommand;
            }
            ref readonly FlightState e = ref core.State;
            Angles(e, out r.RollDeg, out r.PitchDeg, out r.HeadingDeg);
            r.RollRate = System.Math.Abs(r.RollDeg) / Seconds;
            r.YawRate = System.Math.Abs(r.HeadingDeg) / Seconds;
            double ex = e.Position.X - setup.StartPosition.X, ez = e.Position.Z - setup.StartPosition.Z;
            r.SideDrift = System.Math.Abs(ex);
            r.Drift = DetMath.Sqrt(ex * ex + ez * ez);
            r.ITerm = e.Fc.ITerm;
            for (int i = 0; i < 4; i++)
            {
                r.Duty += e.EscCommand[i] / 4;
                r.Speed += e.RotorSpeed[i] / 4;
            }
            r.Volts = core.Trace.PackVolts0;
            return r;
        }

        // The bank (the right side up), the pitch (the nose up) and the heading (the nose's turn right from north), degrees.
        static void Angles(in FlightState s, out double roll, out double pitch, out double heading)
        {
            Double3 right = s.Attitude.Rotate(new Double3(1, 0, 0)), nose = s.Attitude.Rotate(new Double3(0, 0, 1));
            roll = System.Math.Asin(right.Y) * RadToDeg;
            pitch = System.Math.Asin(nose.Y) * RadToDeg;
            heading = System.Math.Atan2(nose.X, nose.Z) * RadToDeg;
        }

        /// <summary>What the band's linear model predicts for one flight's draws, magnitudes.</summary>
        public struct Prediction
        {
            /// <summary>The roll and yaw moments the Kv spread makes at equal duties, N·m.</summary>
            public double RollMoment, YawMoment;

            /// <summary>The I terms that hold them, PID units; the roll angle and the heading they cost, degrees; and their
            /// correction rates over the hover, °/s.</summary>
            public double RollI, YawI, RollDeg, HeadingDeg, RollRate, YawRate;

            /// <summary>Betaflight's roll and yaw ki the prediction used, PID units per degree (Model's).</summary>
            public double RollKi, YawKi;

            /// <summary>The props' unbalance: the body rate's step-to-step change it makes, RMS, rad/s (the rotors at
            /// their own speeds, so their vibrations add in quadrature); and its most, the four in phase (their amplitudes
            /// summed on each axis), which no block of the unbalance's own vibration can pass.</summary>
            public double Vibration, VibrationInPhase;
        }

        /// <summary>The hover's linearisation (Predict): the operating point and the slopes there.</summary>
        public sealed class Model
        {
            public double Duty, Volts, Speed, Thrust, Torque;

            /// <summary>The thrust's and the torque's slopes against the Kv's scale and against the duty, N and N·m per unit.</summary>
            public double ThrustPerKv, TorquePerKv, ThrustPerDuty, TorquePerDuty;

            /// <summary>The duty a PID unit moves a motor by (the mixer's DShot span over 1000).</summary>
            public double DutyPerUnit;

            /// <summary>Betaflight's roll and yaw ki, PID units per degree of integrated rate error.</summary>
            public double RollKi, YawKi;

            /// <summary>The hubs from the CoM, body m; the spins (+1 clockwise); the inertia's diagonal, kg·m².</summary>
            public Double3[] Arm = new Double3[4];
            public double[] Spin = new double[4];
            public Double3 Inertia;
        }

        /// <summary>The hover's linear model at the identical-rotor hover's operating point (its duty and bus voltage).</summary>
        public static Model Linearise(DroneSpec spec, in Result identical)
        {
            PropulsionSpec ps = spec.BuildPropulsion();
            var prop = new Propulsion(ps);
            DroneMotor m = ps.Motors[0];
            var mo = new Model { Duty = identical.Duty, Volts = identical.Volts };
            mo.Speed = SteadySpeed(prop, m, 1, mo.Duty, mo.Volts);
            prop.Aero(mo.Speed, 0, out mo.Thrust, out mo.Torque);
            const double ek = 1e-4, ed = 1e-5;
            Steady(prop, m, 1 + ek, mo.Duty, mo.Volts, out double tk1, out double qk1);
            Steady(prop, m, 1 - ek, mo.Duty, mo.Volts, out double tk0, out double qk0);
            Steady(prop, m, 1, mo.Duty + ed, mo.Volts, out double td1, out double qd1);
            Steady(prop, m, 1, mo.Duty - ed, mo.Volts, out double td0, out double qd0);
            mo.ThrustPerKv = (tk1 - tk0) / (2 * ek);
            mo.TorquePerKv = (qk1 - qk0) / (2 * ek);
            mo.ThrustPerDuty = (td1 - td0) / (2 * ed);
            mo.TorquePerDuty = (qd1 - qd0) / (2 * ed);
            FcSettings fc = FcSettings.Default;
            const double dshotMin = 48, dshotMax = 2047, range = dshotMax - dshotMin;
            double low = dshotMin + fc.dshot_idle_value * 0.0001 * range, high = dshotMax - range * (1 - fc.motor_output_limit / 100);
            mo.DutyPerUnit = (high - low) / range / 1000;
            mo.RollKi = 0.244381 * fc.i_roll; // pid.c's ITERM_SCALE (published, Betaflight 4.5)
            mo.YawKi = 0.244381 * fc.i_yaw;
            MassProperties body = spec.BuildMass(spec.Nominal).Properties;
            for (int i = 0; i < 4; i++)
            {
                mo.Arm[i] = ps.Motors[i].Position - body.CenterOfMass;
                mo.Spin[i] = ps.Motors[i].Clockwise ? 1 : -1;
            }
            mo.Inertia = new Double3(body.Inertia.XX, body.Inertia.YY, body.Inertia.ZZ);
            return mo;
        }

        /// <summary>The band's prediction for one flight's draws (FlightCore.Draws).</summary>
        public static Prediction Predict(Model mo, MotorAsymmetry.Draws d)
        {
            var p = new Prediction();
            double roll = 0, yaw = 0, rollAuthority = 0, vib2 = 0, sumX = 0, sumY = 0, sumZ = 0;
            for (int i = 0; i < 4; i++)
            {
                double dk = d.KvScale[i] - 1;
                roll += mo.Arm[i].X * mo.ThrustPerKv * dk;           // about body z (FlightCore: m.Z += r.X·T)
                yaw += -mo.Spin[i] * mo.TorquePerKv * dk;            // a clockwise rotor's drag yaws the body left
                rollAuthority += System.Math.Abs(mo.Arm[i].X) * mo.ThrustPerDuty * mo.DutyPerUnit;

                // The unbalance at the hover's speed: its pull F = U·ω² turning in the disc's plane, whose moment about the
                // CoM has amplitude |r_y|·F about x and z and |r_xz|·F about y; each axis's rate a sinusoid at ω, whose
                // step-to-step change has RMS α/ω · 2 sin(ω·dt/2) / √2.
                double f = d.Unbalance[i] * mo.Speed * mo.Speed;
                Double3 r = mo.Arm[i];
                double ax = System.Math.Abs(r.Y) * f / mo.Inertia.X, az = System.Math.Abs(r.Y) * f / mo.Inertia.Z;
                double ay = DetMath.Sqrt(r.X * r.X + r.Z * r.Z) * f / mo.Inertia.Y;
                double step = 2 * System.Math.Sin(mo.Speed * FlightCore.Dt / 2) / mo.Speed;
                vib2 += (ax * ax + ay * ay + az * az) * step * step / 2;
                sumX += ax * step;
                sumY += ay * step;
                sumZ += az * step;
            }
            p.RollMoment = System.Math.Abs(roll);
            p.YawMoment = System.Math.Abs(yaw);
            p.RollI = p.RollMoment / rollAuthority;
            p.YawI = p.YawMoment / (4 * mo.TorquePerDuty * mo.DutyPerUnit);
            p.RollKi = mo.RollKi;
            p.YawKi = mo.YawKi;
            p.RollDeg = p.RollI / mo.RollKi;
            p.HeadingDeg = p.YawI / mo.YawKi;
            p.RollRate = p.RollDeg / Seconds;
            p.YawRate = p.HeadingDeg / Seconds;
            p.Vibration = DetMath.Sqrt(vib2);
            p.VibrationInPhase = DetMath.Sqrt((sumX * sumX + sumY * sumY + sumZ * sumZ) / 2);
            return p;
        }

        /// <summary>The band: the predictions over Draws flights' draws (seeds 1…Draws), their low and high quantiles.</summary>
        public struct Band
        {
            public double RollRateLo, RollRateHi, YawRateLo, YawRateHi, VibrationLo, VibrationHi;
        }

        public const int BandDraws = 20000;
        public const double BandQuantile = 0.005;

        public static Band BandOf(DroneSpec spec, Model mo)
        {
            MotorAsymmetry a = spec.BuildAsymmetry();
            var roll = new List<double>(BandDraws);
            var yaw = new List<double>(BandDraws);
            var vib = new List<double>(BandDraws);
            for (int n = 1; n <= BandDraws; n++)
            {
                Prediction p = Predict(mo, DrawsOf(a, (ulong)n));
                roll.Add(p.RollRate);
                yaw.Add(p.YawRate);
                vib.Add(p.Vibration);
            }
            roll.Sort();
            yaw.Sort();
            vib.Sort();
            int lo = (int)(BandQuantile * BandDraws), hi = BandDraws - 1 - lo;
            return new Band
            {
                RollRateLo = roll[lo], RollRateHi = roll[hi], YawRateLo = yaw[lo], YawRateHi = yaw[hi], VibrationLo = vib[lo], VibrationHi = vib[hi],
            };
        }

        public static readonly ulong[] Seeds = { 1, 2, 3, 4, 5 };

        /// <summary>The scenario on Seeds, asymmetry on and off, with the band and each seed's prediction.</summary>
        public sealed class Run
        {
            public Model Model;
            public Band Band;
            public Result[] On = new Result[5], Off = new Result[5];
            public Prediction[] Predicted = new Prediction[5];
        }

        static Run _run;

        /// <summary>The scenario's flights and band, flown once per process.</summary>
        public static Run Flown(DroneSpec spec)
        {
            if (_run != null) return _run;
            var run = new Run();
            for (int i = 0; i < Seeds.Length; i++)
            {
                run.Off[i] = Hover(Setup(spec, Seeds[i], false));
                run.On[i] = Hover(Setup(spec, Seeds[i], true), out FlightCore core);
                if (i == 0) run.Model = Linearise(spec, run.Off[0]);
                run.Predicted[i] = Predict(run.Model, core.Draws);
            }
            run.Band = BandOf(spec, run.Model);
            return _run = run;
        }

        /// <summary>The correction rates inside the band (the known-wrong identical rotors fall below it).</summary>
        public static bool InBand(in Result r, in Band b) =>
            r.RollRate >= b.RollRateLo && r.RollRate <= b.RollRateHi && r.YawRate >= b.YawRateLo && r.YawRate <= b.YawRateHi;

        /// <summary>
        /// The yank's absolute tolerance, degrees (assumed; QA's finding 6, seed 13): the steady model leaves out the spin-up's
        /// transients, the rotors' reaction torques as they spin up to their Kv-spread speeds and Betaflight's anti-gravity
        /// boosting the I term while the throttle moves, which turn the drone by a few hundredths of a degree whatever the
        /// steady moment. Their size: J·Δω over the yaw inertia, 6e-5 kg·m² × 2 × 16 rad/s (the 2 % Kv spread at the hover's
        /// 920 rad/s, the four signed) / 0.060 kg·m², 1.8 °/s, for the loop's ~20 ms, about 0.04° (derived, its order).
        /// A small yank's relative error grows past 30 % by this much: on seeds 1–30 the largest miss beyond 30 % is 0.026°
        /// (seed 27's roll, 0.018° against 0.063°), so with the floor none falls outside.
        /// </summary>
        public const double YankFloorDeg = 0.03;

        /// <summary>The yank (I terms, roll and heading at YankAt) within 30 % of the seed's own prediction and YankFloorDeg
        /// (its I terms' ki times it), and its vibration's blocks around the prediction: the linear model leaves out the
        /// drift's air (about −14 % on the roll by 4 s) and the spin-up's transients (±12 % on the yaw, and the floor).</summary>
        public static bool AsPredicted(in Result r, in Prediction p) =>
            Near(DetMath.Abs(r.YankITerm.Roll), p.RollI, YankFloorDeg * p.RollKi) && Near(DetMath.Abs(r.YankITerm.Yaw), p.YawI, YankFloorDeg * p.YawKi)
            && Near(DetMath.Abs(r.YankRollDeg), p.RollDeg, YankFloorDeg) && Near(DetMath.Abs(r.YankHeadingDeg), p.HeadingDeg, YankFloorDeg)
            && r.VibrationMin <= 1.3 * p.Vibration && r.VibrationMax >= 0.7 * p.Vibration;

        /// <summary>Never buzzes: the ESCs' commands move by under 1e-4 a step (the loop does not chase the vibration) and the
        /// vibration stays within the props' own pull with all four beating in phase (D-050's layout, its centre of mass
        /// nearer the hubs' plane, puts more of the pull on yaw, where the four beat on one axis, so a block passes model
        /// 13's √2 of their quadrature sum); never diverges: the body rate under 0.01 rad/s and the attitude within 5° of
        /// level.</summary>
        public static bool Steady(in Result r, in Prediction p) =>
            r.DutyJump < 1e-4 && r.VibrationMax <= p.VibrationInPhase && r.RateMax < 0.01
            && DetMath.Abs(r.RollDeg) < 5 && DetMath.Abs(r.PitchDeg) < 5;

        /// <summary>Calm's floor (PHY-14): every 1 s block above the band's lowest vibration.</summary>
        public static bool NeverStill(in Result r, in Band b) => r.VibrationMin >= b.VibrationLo;

        static bool Near(double measured, double predicted, double floor) =>
            measured >= 0.7 * predicted - floor && measured <= 1.3 * predicted + floor;

        public static string Describe(in Result on, in Result off, in Prediction p) =>
            $"on: roll {on.RollRate:E2} and yaw {on.YawRate:E2} °/s, the yank {on.YankRollDeg:F3}° and {on.YankHeadingDeg:F3}° at {YankAt:F0} s "
            + $"(predicted {p.RollDeg:F3}° and {p.HeadingDeg:F3}°, I {DetMath.Abs(on.YankITerm.Roll):F2}/{DetMath.Abs(on.YankITerm.Yaw):F2} against "
            + $"{p.RollI:F2}/{p.YawI:F2}), drifting {on.SideDrift:F1} m sideways, vibration {on.VibrationMin:E2}–{on.VibrationMax:E2} rad/s "
            + $"(predicted {p.Vibration:E2}, in phase at most {p.VibrationInPhase:E2}), the ESCs' largest step {on.DutyJump:E1}; off: {off.RollRate:E1} and {off.YawRate:E1} °/s, "
            + $"{off.SideDrift:F2} m, vibration {off.VibrationMax:E1}";

        /// <summary>A seed's per-flight motor draws, as a flight on that seed makes them (the wind is still, so it draws none first).</summary>
        public static MotorAsymmetry.Draws DrawsOf(MotorAsymmetry a, ulong seed) => a.DrawFor(new FlightRandom(seed));

        // A motor's steady speed at a duty and bus voltage with its Kv scaled: Kt·i = Kt·I0 + Q(ω), i = (d·V − Ke·ω)/R,
        // by bisection, with its thrust and torque there.
        static void Steady(Propulsion prop, DroneMotor m, double kv, double duty, double volts, out double thrust, out double torque)
        {
            double w = SteadySpeed(prop, m, kv, duty, volts);
            prop.Aero(w, 0, out thrust, out torque);
        }

        static double SteadySpeed(Propulsion prop, DroneMotor m, double kv, double duty, double volts)
        {
            double ke = 60 / (TwoPi * (m.Kv * kv));
            double lo = 0, hi = duty * volts / ke;
            for (int i = 0; i < 200; i++)
            {
                double w = (lo + hi) / 2;
                prop.Aero(w, 0, out _, out double q);
                double net = ke * (duty * volts - ke * w) / m.Resistance - ke * m.NoLoadCurrent - q;
                if (net > 0) lo = w; else hi = w;
            }
            return (lo + hi) / 2;
        }
    }
}
