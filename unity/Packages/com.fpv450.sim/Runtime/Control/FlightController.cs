using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Control
{
    /// <summary>What the mixer did not deliver, per axis: the largest share of a demand lost (0–1) over a step's loops.</summary>
    public struct MixerLoss
    {
        public double Roll, Pitch, Yaw, Collective;

        public readonly double this[int axis] => axis == 0 ? Roll : axis == 1 ? Pitch : axis == 2 ? Yaw : Collective;
    }

    /// <summary>
    /// Betaflight 4.5 in acro (D-023, design 4), its formulas re-implemented from the published 4.5.0 source (GPL,
    /// read and never copied) on the state FcState holds:
    /// - the RC link: a frame every LoopsPerRcFrame loops (the pilot's 250 Hz link) takes the stick in force:
    ///   rcCommand with the deadband, the throttle through min_check and the 11-point curve, the ACTUAL rate curve,
    ///   feedforward (rc.c updateRcCommands, processRcCommand, calculateFeedforward, applyActualRates); Betaflight's
    ///   arming rules on the arm switch and airmode's start (fc/core.c)
    /// - every loop: the gyro's and the D term's filter chain (FilterBank: the lowpasses, the notches, the RPM filter
    ///   and the dynamic notch), the RC smoothing's PT3 filters at the auto cutoffs (rc.c processRcSmoothingFilter),
    ///   the PID with Betaflight's scaling on the filtered gyro, I-term relax, the I-term's windup and limit, D min,
    ///   TPA, anti-gravity, the yaw P low-pass and the PID-sum limits (flight/pid.c pidController, pid_init.c), and
    ///   the mixer: the dynamic LPFs' throttle, throttle boost, the Quad X mix, airmode's desaturation and the DShot
    ///   endpoints with the idle and battery sag compensation (flight/mixer.c mixTable, calculateThrottleAndCurrent-
    ///   MotorEndpoints 245–262, applyMixerAdjustment, applyMixToMotors; mixer_init.c 347–357; drivers/dshot.c
    ///   dshotInitEndpoints)
    /// - the battery task at 200 Hz while sag compensation is on (Battery: sensors/voltage.c 186–190, 228–232,
    ///   battery.c 206–225, 549–553; fc/tasks.c's FAST_VOLTAGE_TASK_FREQ_HZ, voltage.h 25–26)
    /// The loop runs at its real 4 kHz, LoopsPerStep loops a 1 ms step. Departures, each deliberate: double precision
    /// throughout (Betaflight computes in float); the throttle channel is not truncated to whole µs (the curve's
    /// table is, as Betaflight builds it); the gyro is the rigid body's rate, with no sensor (FilterBank lists the
    /// filter chain's own); the pack voltage is read exactly, then truncated to whole cV as Betaflight's integer cell
    /// voltage is, with no ADC counts; the link's frame rate is fixed, so the auto cutoffs are Betaflight's settled
    /// ones. Not modelled, so refused: feedforward_averaging and a lowpass type other than BIQUAD. Modes other than
    /// acro (angle, horizon, launch control, crash recovery, yaw-spin recovery, absolute control, 3D, dynamic idle,
    /// thrust linearisation, turtle) do not exist here.
    /// </summary>
    public sealed class FlightController
    {
        /// <summary>The PID loop, Hz: the builder's 8 kHz gyro with pid_process_denom 2 (pilot, fc-profiles §1).</summary>
        public const int LoopHz = 4000;

        /// <summary>PID loops a 1 ms flight step (FlightCore.Dt).</summary>
        public const int LoopsPerStep = 4;

        /// <summary>The RC link's frame rate, Hz (pilot, fc-profiles §1: the RC link at 250 Hz).</summary>
        public const int RcLinkHz = 250;

        public const int LoopsPerRcFrame = LoopHz / RcLinkHz;

        /// <summary>The battery task's rate with sag compensation on, Hz (published: voltage.h 26, FAST_VOLTAGE_TASK_FREQ_HZ).</summary>
        public const int BatteryHz = 200;

        public const int LoopsPerBatterySample = LoopHz / BatteryHz;

        /// <summary>One loop, s.</summary>
        public const double LoopDt = 1.0 / LoopHz;

        // ---- Betaflight 4.5.0's constants (published; flight/pid.h, flight/pid_init.c, flight/pid.c, common/filter.c,
        // fc/rc.c, drivers/dshot.h, fc/rc_controls.h) ----
        const double PtermScale = 0.032029, ItermScale = 0.244381, DtermScale = 0.000529, FeedforwardScale = 0.013754;
        const double PidMixerScaling = 1000;
        const double ItermRelaxSetpointThreshold = 40;
        const double AntiGravityKi = 0.34, AntiGravityKp = 0.0034;
        const double DMinRangeHz = 85, DMinLowpassHz = 35, DMinGainFactor = 0.00008, DMinSetpointGainFactor = 0.00008;
        const double CutoffCorrectionPt2 = 1.553773974, CutoffCorrectionPt3 = 1.961459177;
        const double RcSmoothingMinHz = 15;
        const double SetpointRateLimit = 1998;
        const double DshotMinThrottle = 48, DshotMaxThrottle = 2047, DshotRange = DshotMaxThrottle - DshotMinThrottle;
        const double PwmRangeMin = 1000, PwmRangeMax = 2000, PwmRange = PwmRangeMax - PwmRangeMin;
        const double StickHalfRange = 500;
        const double TwoPi = 6.283185307179586;
        const double CellVoltageFullCv = 420; // battery.h 31, CELL_VOLTAGE_FULL_CV
        const int MaxAutoDetectCells = 8;     // battery.h 38

        readonly FcSettings _s;
        readonly FilterBank _bank;

        // Battery sag compensation (mixer_init.c 347–357): the factor and the cell range it works over, cV; the sag
        // filter's gain at the battery task's rate.
        readonly double _sagFactor, _sagRangeCv, _kSag;

        // The PID's gains, Betaflight-scaled, per axis.
        readonly double[] _kp = new double[3], _ki = new double[3], _kd = new double[3], _kf = new double[3];
        readonly double[] _dMinPercent = new double[3];
        readonly double _dMinGyroGain, _dMinSetpointGain;
        readonly double _kRelax, _kDMinRange, _kDMinLpf, _kYawP;
        readonly double _iTermLimit, _windupInv;
        readonly double _agGain, _agPGain, _kAg;
        readonly double _boost, _kBoost;
        readonly double _tpaBreakpoint, _tpaMultiplier, _tpaLowBreakpoint, _tpaLowMultiplier;

        // The RC path.
        readonly double[] _divider = new double[3], _deadband = new double[3];
        readonly double[] _centre = new double[3], _movement = new double[3], _expo = new double[3], _maxRate = new double[3];
        readonly int[] _throttleCurve = new int[12];
        readonly double _kSetpoint, _kThrottle, _kFeedforward;
        readonly double _ffSmooth, _ffBoost, _ffJitter, _ffJitterInv, _ffTransition, _ffTransitionInv, _ffMaxRateLimit;

        // The mixer: Betaflight's Quad X columns by corner, the DShot endpoints.
        readonly double[] _rollMix = new double[4], _pitchMix = new double[4], _yawMix = new double[4];
        readonly double _outputLow, _outputHigh;

        /// <summary>The auto-or-set RC smoothing cutoffs, Hz: setpoint, throttle, feedforward.</summary>
        public readonly double SetpointCutoffHz, ThrottleCutoffHz, FeedforwardCutoffHz;

        /// <param name="settings">The flight's settings.</param>
        /// <param name="clockwise">Each corner's spin, Betaflight's Quad X order: the yaw column follows the props
        /// (Betaflight's table is props-in, motor 1 clockwise; yaw_motors_reversed flips it for props-out).</param>
        public FlightController(in FcSettings settings, bool[] clockwise)
        {
            if (clockwise == null || clockwise.Length != 4) throw new ArgumentException("four corners' spins", nameof(clockwise));
            _s = settings;
            if (!(settings.vbat_sag_compensation >= 0 && settings.vbat_sag_compensation <= 150))
                throw new ArgumentException("vbat_sag_compensation is Betaflight's 0–150 %");
            if (!IsWhole(settings.vbat_max_cell_voltage, 100, 500) || !IsWhole(settings.vbat_warning_cell_voltage, 100, 500)
                || !IsWhole(settings.force_battery_cell_count, 0, 24) || !(settings.vbat_sag_lpf_period >= 1))
                throw new ArgumentException("the battery's cell voltages are whole cV 100–500, force_battery_cell_count 0–24, vbat_sag_lpf_period at least 1");
            if (settings.feedforward_averaging != FeedforwardAveraging.OFF)
                throw new ArgumentException("feedforward_averaging is not modelled yet (p6-flight-core 1.4): it must be OFF");
            if (!IsWhole(settings.thr_mid, 0, 100) || !IsWhole(settings.thr_expo, 0, 100))
                throw new ArgumentException("thr_mid and thr_expo are Betaflight's whole numbers 0–100");

            // pid_init.c pidInitConfig: P, I, D and F with Betaflight's scaling.
            double[] p = { settings.p_roll, settings.p_pitch, settings.p_yaw };
            double[] i = { settings.i_roll, settings.i_pitch, settings.i_yaw };
            double[] d = { settings.d_roll, settings.d_pitch, settings.d_yaw };
            double[] f = { settings.f_roll, settings.f_pitch, settings.f_yaw };
            double[] dMin = { settings.d_min_roll, settings.d_min_pitch, settings.d_min_yaw };
            for (int a = 0; a < 3; a++)
            {
                _kp[a] = PtermScale * p[a];
                _ki[a] = ItermScale * i[a];
                _kd[a] = DtermScale * d[a];
                _kf[a] = FeedforwardScale * (f[a] * 0.01);
                _dMinPercent[a] = dMin[a] > 0 && dMin[a] < d[a] ? dMin[a] / d[a] : 0;
            }
            _dMinGyroGain = DMinGainFactor * settings.d_min_gain / DMinLowpassHz;
            _dMinSetpointGain = DMinSetpointGainFactor * settings.d_min_gain * settings.d_min_advance / 100 / DMinLowpassHz;
            _kRelax = settings.iterm_relax != ItermRelax.OFF ? Pt1Gain(settings.iterm_relax_cutoff) : 0;
            _kDMinRange = Pt1Gain(DMinRangeHz * CutoffCorrectionPt2);
            _kDMinLpf = Pt1Gain(DMinLowpassHz * CutoffCorrectionPt2);
            _kYawP = settings.yaw_lowpass_hz > 0 ? Pt1Gain(settings.yaw_lowpass_hz) : 0;
            _iTermLimit = settings.iterm_limit;
            _windupInv = settings.iterm_windup < 100 ? 1 / (1 - settings.iterm_windup / 100) : 1;
            _agGain = settings.anti_gravity ? settings.anti_gravity_gain : 0;
            _agPGain = settings.anti_gravity_p_gain / 100 * AntiGravityKp;
            _kAg = Pt1Gain(settings.anti_gravity_cutoff_hz * CutoffCorrectionPt2);
            _boost = settings.throttle_boost * 0.1;
            _kBoost = Pt1Gain(settings.throttle_boost_cutoff);
            _tpaBreakpoint = Clamp((settings.tpa_breakpoint - PwmRangeMin) / 1000, 0, 0.99);
            _tpaMultiplier = settings.tpa_rate / 100 / (1 - _tpaBreakpoint);
            _tpaLowBreakpoint = DetMath.Min(Clamp((settings.tpa_low_breakpoint - PwmRangeMin) / 1000, 0.01, 1), _tpaBreakpoint);
            _tpaLowMultiplier = settings.tpa_low_rate / (100 * _tpaLowBreakpoint);

            // rc.c initRcProcessing and the ACTUAL rates.
            _deadband[0] = _deadband[1] = settings.deadband;
            _deadband[2] = settings.yaw_deadband;
            double[] rcRate = { settings.roll_rc_rate, settings.pitch_rc_rate, settings.yaw_rc_rate };
            double[] sRate = { settings.roll_srate, settings.pitch_srate, settings.yaw_srate };
            double[] expo = { settings.roll_expo, settings.pitch_expo, settings.yaw_expo };
            for (int a = 0; a < 3; a++)
            {
                _divider[a] = StickHalfRange - _deadband[a];
                _centre[a] = rcRate[a] * 10;
                _movement[a] = DetMath.Max(0, sRate[a] * 10 - _centre[a]);
                _expo[a] = expo[a] / 100;
                _maxRate[a] = ActualRate(a, 1, 1);
            }
            BuildThrottleCurve((int)settings.thr_mid, (int)settings.thr_expo, _throttleCurve);

            // rc.c rcSmoothingSetFilterCutoffs at the link's settled rate; a set cutoff wins over auto.
            double setpointFactor = 1.5 / (1 + settings.rc_smoothing_auto_factor / 10);
            double throttleFactor = 1.5 / (1 + settings.rc_smoothing_auto_factor_throttle / 10);
            SetpointCutoffHz = settings.rc_smoothing_setpoint_cutoff != 0 ? settings.rc_smoothing_setpoint_cutoff : AutoCutoff(setpointFactor);
            ThrottleCutoffHz = settings.rc_smoothing_throttle_cutoff != 0 ? settings.rc_smoothing_throttle_cutoff : AutoCutoff(throttleFactor);
            FeedforwardCutoffHz = settings.rc_smoothing_feedforward_cutoff != 0 ? settings.rc_smoothing_feedforward_cutoff : AutoCutoff(setpointFactor);
            _kSetpoint = Pt1Gain(SetpointCutoffHz * CutoffCorrectionPt3);
            _kThrottle = Pt1Gain(ThrottleCutoffHz * CutoffCorrectionPt3);
            _kFeedforward = Pt1Gain(FeedforwardCutoffHz * CutoffCorrectionPt3);

            // pid_init.c: feedforward's shaping.
            _ffSmooth = 1 - 0.01 * settings.feedforward_smooth_factor;
            _ffBoost = 0.1 * settings.feedforward_boost;
            _ffJitter = settings.feedforward_jitter_factor;
            _ffJitterInv = _ffJitter != 0 ? 1 / (2 * _ffJitter) : 0;
            _ffTransition = settings.feedforward_transition / 100;
            _ffTransitionInv = settings.feedforward_transition == 0 ? 0 : 100 / settings.feedforward_transition;
            _ffMaxRateLimit = settings.feedforward_max_rate_limit;

            // mixer_init.c mixerQuadX: roll −1 on the right, pitch +1 at the rear (+pitch is nose down); the yaw
            // column +1 on the counter-clockwise props, whose speed-up yaws the body right.
            int[] side = { +1, +1, -1, -1 }, end = { -1, +1, -1, +1 };
            for (int c = 0; c < 4; c++)
            {
                _rollMix[c] = -side[c];
                _pitchMix[c] = -end[c];
                _yawMix[c] = clockwise[c] ? -1 : +1;
            }

            // drivers/dshot.c dshotInitEndpoints: the idle above DShot's 48, the output limit below 2047.
            _outputLow = DshotMinThrottle + settings.dshot_idle_value * 0.01 * 0.01 * DshotRange;
            _outputHigh = DshotMaxThrottle - DshotRange * (1 - settings.motor_output_limit / 100);

            // mixer_init.c 347–357: full is 4.20 V a cell; compensated down to the warning voltage. voltage.c 228–232:
            // the sag filter a PT1 at 1 / (period / 10) Hz at the battery task's rate (GET_BATTERY_LPF_FREQUENCY).
            _sagRangeCv = CellVoltageFullCv - settings.vbat_warning_cell_voltage;
            _sagFactor = settings.vbat_sag_compensation > 0 && _sagRangeCv > 0 ? settings.vbat_sag_compensation / 100 : 0;
            double sagHz = 1 / (settings.vbat_sag_lpf_period / 10), dt = 1.0 / BatteryHz;
            _kSag = TwoPi * sagHz * dt / (TwoPi * sagHz * dt + 1);

            _bank = new FilterBank(settings);
        }

        /// <summary>The gyro and D-term filter chain, its configuration and its delay lines.</summary>
        public FilterBank Filters => _bank;

        /// <summary>The sag compensation's factor (vbat_sag_compensation / 100; 0 when off).</summary>
        public double SagFactor => _sagFactor;

        /// <summary>The state of a controller that has been running with its sticks centred, throttle off, disarmed;
        /// the arm switch must be seen off before it arms (Betaflight refuses an arm switch that is on at power-up).
        /// The filter bank is a resting drone's (FilterBank.Reset); the battery is found at the first battery loop.</summary>
        public void Reset(ref FcState s, in Axes3 gyro)
        {
            s = default;
            s.RcThrottle = s.Throttle1 = s.Throttle2 = s.Throttle = PwmRangeMin;
            s.RcDataThrottle = PwmRangeMin;
            s.GyroPrev = gyro;
            s.TpaFactor = 1;
            s.Flags = FcFlags.ArmBlocked;
            _bank.Reset(ref s);
        }

        /// <summary>True when loop (counted from the flight's start) takes a new RC frame.</summary>
        public static bool IsFrameLoop(long loop) => loop % LoopsPerRcFrame == 0;

        /// <summary>True when loop (counted from the flight's start) runs the battery task: every LoopsPerBatterySample
        /// loops, at the second loop of a step (the scheduler's phase, assumed), so that it reads the bus voltage the
        /// step's first sub-step left.</summary>
        public static bool IsBatteryLoop(long loop) => loop % LoopsPerBatterySample == 1;

        /// <summary>
        /// The battery task with sag compensation on (it does not run otherwise, voltage.c 187): the pack's voltage, V,
        /// through the sag filter. The first reading finds the battery (battery.c batteryUpdatePresence, its display
        /// filter long settled at a powered-up drone's): the cells forced, or counted as whole cV over
        /// vbat_max_cell_voltage, plus one, at most 8; the sag filter starts settled there.
        /// </summary>
        public void Battery(ref FcState s, double packVolts)
        {
            if (_sagFactor <= 0) return;
            if (s.Cells == 0)
            {
                int cells = _s.force_battery_cell_count != 0
                    ? (int)_s.force_battery_cell_count
                    : (int)(packVolts * 100) / (int)_s.vbat_max_cell_voltage + 1;
                s.Cells = (byte)(cells > MaxAutoDetectCells && _s.force_battery_cell_count == 0 ? MaxAutoDetectCells : cells);
                s.SagVolts = packVolts;
                return;
            }
            s.SagVolts += _kSag * (packVolts - s.SagVolts);
        }

        /// <summary>
        /// The share of the motors' range (idle to full) sag compensation takes off its top (calculateThrottleAndCurrent-
        /// MotorEndpoints 245–256): the cell voltage, whole cV (getBatterySagCellVoltage), as goodness 1 at full and 0 at
        /// the warning voltage, times the range over full and the factor; 0 when off or before the battery is found.
        /// </summary>
        public double SagAttenuation(in FcState s)
        {
            if (_sagFactor <= 0) return 0;
            int cellCv = s.Cells > 0 ? (int)(s.SagVolts * 100) / s.Cells : 0;
            double goodness = 1 - Clamp((CellVoltageFullCv - cellCv) / _sagRangeCv, 0, 1);
            return _sagRangeCv / CellVoltageFullCv * goodness * _sagFactor;
        }

        /// <summary>
        /// One RC frame: the stick in force read as Betaflight's channels (roll, pitch, yaw 1500 ± 500 µs, throttle
        /// 1000–2000 µs), the arming rules, then the rates and feedforward (rc.c, fc/core.c).
        /// </summary>
        public void Frame(ref FcState s, ref bool armed, in StickSample k)
        {
            // rc.c updateRcCommands: the deadband, and the throttle through min_check and the curve.
            var rc = new Axes3
            {
                Roll = Deadband(k.Roll, _deadband[0]),
                Pitch = Deadband(k.Pitch, _deadband[1]),
                Yaw = Deadband(k.Yaw, _deadband[2]),
            };
            double rcData = PwmRangeMin + PwmRange * k.Throttle;
            double t = Clamp(rcData, _s.min_check, PwmRangeMax);
            t = (t - _s.min_check) * PwmRangeMin / (PwmRangeMax - _s.min_check);
            s.RcThrottle = LookupThrottle(t);
            s.RcDataThrottle = rcData;

            // fc/core.c: arm on the switch with the throttle low; a switch turned on while arming is refused latches
            // the refusal until it is off again; off disarms. Airmode activates once the throttle passes its start.
            bool throttleLow = rcData < _s.min_check;
            if (armed)
            {
                if (!k.Arm) armed = false;
            }
            else if (!k.Arm) s.Flags &= ~FcFlags.ArmBlocked;
            else if ((s.Flags & FcFlags.ArmBlocked) == 0)
            {
                if (throttleLow) armed = true;
                else s.Flags |= FcFlags.ArmBlocked;
            }
            double percent = Clamp((Clamp(rcData, PwmRangeMin, PwmRangeMax) - _s.min_check) * 100 / (PwmRangeMax - _s.min_check), 0, 100);
            if (_s.airmode && armed)
            {
                if (percent >= _s.airmode_start_throttle_percent) s.Flags |= FcFlags.AirmodeActive;
            }
            else s.Flags &= ~FcFlags.AirmodeActive;

            // rc.c processRcCommand: the rate curve and feedforward.
            for (int a = 0; a < 3; a++)
            {
                double command = rc[a];
                double f = command / _divider[a];
                double setpoint = Clamp(ActualRate(a, f, DetMath.Abs(f)), -SetpointRateLimit, SetpointRateLimit);
                Feedforward(ref s, a, command, setpoint, DetMath.Abs(f));
                s.RcCommand[a] = command;
                s.RawSetpoint[a] = setpoint;
            }
        }

        /// <summary>
        /// One 4 kHz loop: the filter chain on gyro (°/s, pilot axes, this loop's reading) and the motors' speeds
        /// (rad/s, bidirectional DShot), the RC smoothing, the PID on the filtered gyro and the mixer. duty is each
        /// ESC's command, 0–1 of its full duty (DShot 48–2047), zero while disarmed (the ESC lets its motor freewheel);
        /// loss gathers what the mixer did not deliver while armed.
        /// </summary>
        public void Loop(ref FcState s, bool armed, in Axes3 gyro, in Corners motorSpeed, ref Corners duty, ref MixerLoss loss)
        {
            // fc/core.c taskFiltering, then pid.c pidController's D-term filters and rpmFilterUpdate (862–883).
            _bank.FilterGyro(ref s, s.GyroPrev, gyro, motorSpeed);
            s.GyroPrev = gyro;
            _bank.FilterDterm(s, out Axes3 dtermGyro);
            _bank.RpmUpdate();

            Smooth(ref s);

            // fc/core.c processRxModes: the I term is held at zero until armed with the throttle up or airmode active.
            bool itermReset = !(armed && ((s.Flags & FcFlags.AirmodeActive) != 0 || s.RcDataThrottle >= _s.min_check));

            // pid.c pidController.
            double antiGravity = s.AntiGravity * _agGain;
            double accelerator = antiGravity * AntiGravityKi;
            double dynCi = _windupInv > 1 ? Clamp((1 - s.MotorMixRange) * _windupInv, 0, 1) : 1;
            double tpaKp = _s.tpa_mode == TpaMode.PD ? s.TpaFactor : 1;
            var sum = new Axes3();
            for (int a = 0; a < 3; a++)
            {
                double setpoint = s.Setpoint[a], g = s.GyroFiltered[a];
                double error = setpoint - g, itermError = error;
                double iterm = s.ITerm[a];

                // applyItermRelax: the setpoint's high-pass relaxes the I term while the stick moves.
                if (_s.iterm_relax != ItermRelax.OFF)
                {
                    double lpf = s.RelaxLpf[a] + _kRelax * (setpoint - s.RelaxLpf[a]);
                    s.RelaxLpf[a] = lpf;
                    double hpf = DetMath.Abs(setpoint - lpf);
                    if (a < 2 || _s.iterm_relax == ItermRelax.RPY || _s.iterm_relax == ItermRelax.RPY_INC)
                    {
                        double factor = DetMath.Max(0, 1 - hpf / ItermRelaxSetpointThreshold);
                        bool decreasing = (iterm > 0 && itermError < 0) || (iterm < 0 && itermError > 0);
                        if (_s.iterm_relax >= ItermRelax.RP_INC && decreasing) { }
                        else if (_s.iterm_relax_type == ItermRelaxType.SETPOINT) itermError *= factor;
                        else itermError = ApplyDeadband(lpf - g, hpf);
                    }
                }

                double pterm = _kp[a] * error * tpaKp;
                if (a == 2 && _kYawP > 0)
                {
                    s.YawP += _kYawP * (pterm - s.YawP);
                    pterm = s.YawP;
                }

                double ki = _ki[a] + (a == 2 ? 0 : accelerator);
                iterm = Clamp(iterm + ki * dynCi * LoopDt * itermError, -_iTermLimit, _iTermLimit);

                double feedforward = s.Ff[a];
                double dterm = 0;
                if (_kd[a] > 0)
                {
                    double delta = -(dtermGyro[a] - s.DtermGyro[a]) * LoopHz;
                    double preTpaD = _kd[a] * delta;
                    if (_dMinPercent[a] > 0)
                    {
                        double range1 = s.DMinRange1[a], range = s.DMinRange[a];
                        Pt2(ref range1, ref range, delta, _kDMinRange);
                        s.DMinRange1[a] = range1;
                        s.DMinRange[a] = range;
                        double factor = DetMath.Max(DetMath.Abs(range) * _dMinGyroGain, DetMath.Abs(feedforward) * _dMinSetpointGain);
                        factor = _dMinPercent[a] + (1 - _dMinPercent[a]) * factor;
                        double lpf1 = s.DMinLpf1[a], lpf = s.DMinLpf[a];
                        Pt2(ref lpf1, ref lpf, factor, _kDMinLpf);
                        s.DMinLpf1[a] = lpf1;
                        s.DMinLpf[a] = lpf;
                        preTpaD *= DetMath.Min(lpf, 1);
                    }
                    dterm = preTpaD * s.TpaFactor;
                }
                s.DtermGyro[a] = dtermGyro[a];

                double fterm = _kf[a] * feedforward;
                if (a != 2)
                {
                    double attenuator = DetMath.Max(DetMath.Abs(setpoint) / 50, 1);
                    pterm *= 1 + antiGravity / attenuator * _agPGain;
                }
                sum[a] = pterm + iterm + dterm + fterm;
                s.ITerm[a] = iterm;
            }
            if (itermReset) s.ITerm = default;

            Mix(ref s, armed, sum, ref duty, ref loss);
        }

        // mixer.c mixTable, with anti-gravity's and TPA's throttle inputs (pid.c pidUpdateAntiGravityThrottleFilter,
        // pidUpdateTpaFactor) for the next loop.
        void Mix(ref FcState s, bool armed, in Axes3 sum, ref Corners duty, ref MixerLoss loss)
        {
            double throttle = Clamp((s.Throttle - PwmRangeMin) / PwmRange, 0, 1);

            double inverse = 1 - throttle;
            double derivative = DetMath.Abs(throttle - s.ThrottlePrev) * LoopHz;
            derivative *= inverse * inverse;
            if (throttle > s.ThrottlePrev) derivative *= inverse * 0.5;
            s.ThrottlePrev = throttle;
            double ag1 = s.AntiGravity1, ag = s.AntiGravity;
            Pt2(ref ag1, ref ag, derivative, _kAg);
            s.AntiGravity1 = ag1;
            s.AntiGravity = ag;

            bool faded = (s.Flags & FcFlags.TpaLowFaded) != 0;
            bool pastLow = !(throttle < _tpaLowBreakpoint && _tpaLowBreakpoint > 0.01);
            double tpaRate;
            if (pastLow || faded)
            {
                tpaRate = _tpaMultiplier * DetMath.Max(throttle - _tpaBreakpoint, 0);
                if (!_s.tpa_low_always && !faded) s.Flags |= FcFlags.TpaLowFaded;
            }
            else tpaRate = _tpaLowMultiplier * (_tpaLowBreakpoint - throttle);
            s.TpaFactor = 1 - tpaRate;

            _bank.UpdateDynLpf(ref s, throttle);

            if (_boost > 0)
            {
                s.BoostLpf += _kBoost * (throttle - s.BoostLpf);
                throttle = Clamp(throttle + _boost * (throttle - s.BoostLpf), 0, 1);
            }
            double commanded = throttle;

            double roll = Clamp(sum.Roll, -_s.pidsum_limit, _s.pidsum_limit) / PidMixerScaling;
            double pitch = Clamp(sum.Pitch, -_s.pidsum_limit, _s.pidsum_limit) / PidMixerScaling;
            double yaw = Clamp(sum.Yaw, -_s.pidsum_limit_yaw, _s.pidsum_limit_yaw) / PidMixerScaling;
            double mixMax = 0, mixMin = 0;
            Corners mix = default;
            for (int c = 0; c < 4; c++)
            {
                double m = roll * _rollMix[c] + pitch * _pitchMix[c] + yaw * _yawMix[c];
                if (m > mixMax) mixMax = m;
                else if (m < mixMin) mixMin = m;
                mix[c] = m;
            }
            double range = mixMax - mixMin;
            s.MotorMixRange = range;

            // applyMixerAdjustment: scale the mix into range, then move the collective so that no motor clips.
            double transition = !_s.airmode && throttle < 0.5 ? 0.5 + throttle : 1;
            double normalisation = range > 1 ? transition / range : transition;
            throttle = Clamp(throttle, -mixMin * normalisation, 1 - mixMax * normalisation);

            // applyMixToMotors: onto the DShot range between the idle and the limit, less sag compensation's share of
            // its top; disarmed, the motors stop.
            double rangeMax = _outputHigh - SagAttenuation(s) * (_outputHigh - _outputLow);
            for (int c = 0; c < 4; c++)
            {
                double motor = _outputLow + (rangeMax - _outputLow) * (mix[c] * normalisation + throttle);
                motor = Clamp(motor, _outputLow, rangeMax);
                duty[c] = armed ? (motor - DshotMinThrottle) / DshotRange : 0;
            }

            if (!armed) return;
            loss.Roll = DetMath.Max(loss.Roll, Lost(sum.Roll, roll * normalisation));
            loss.Pitch = DetMath.Max(loss.Pitch, Lost(sum.Pitch, pitch * normalisation));
            loss.Yaw = DetMath.Max(loss.Yaw, Lost(sum.Yaw, yaw * normalisation));
            loss.Collective = DetMath.Max(loss.Collective, commanded - throttle);
        }

        // The share of a PID sum the mixer did not deliver.
        static double Lost(double pidSum, double delivered)
        {
            double demand = DetMath.Abs(pidSum) / PidMixerScaling;
            return demand > 1e-12 ? 1 - DetMath.Abs(delivered) / demand : 0;
        }

        // rc.c processRcSmoothingFilter, every loop: the setpoint, the throttle and feedforward through their PT3s.
        // With rc_smoothing off Betaflight 4.5 passes the setpoint and throttle through and never initialises the
        // feedforward filter, whose zero gain then holds feedforward at zero; this model does the same.
        void Smooth(ref FcState s)
        {
            if (!_s.rc_smoothing)
            {
                s.Setpoint = s.RawSetpoint;
                s.Throttle = s.RcThrottle;
                return;
            }
            for (int a = 0; a < 3; a++)
            {
                double x1 = s.Setpoint1[a], x2 = s.Setpoint2[a], x = s.Setpoint[a];
                Pt3(ref x1, ref x2, ref x, s.RawSetpoint[a], _kSetpoint);
                s.Setpoint1[a] = x1;
                s.Setpoint2[a] = x2;
                s.Setpoint[a] = x;
                double f1 = s.Ff1[a], f2 = s.Ff2[a], f = s.Ff[a];
                Pt3(ref f1, ref f2, ref f, s.FfRaw[a], _kFeedforward);
                s.Ff1[a] = f1;
                s.Ff2[a] = f2;
                s.Ff[a] = f;
            }
            Pt3(ref s.Throttle1, ref s.Throttle2, ref s.Throttle, s.RcThrottle, _kThrottle);
        }

        // rc.c calculateFeedforward, at a frame: the setpoint's speed per frame, its smoothed acceleration with the
        // boost, the duplicate-packet and jitter rules, the transition and the max-rate limit.
        void Feedforward(ref FcState s, int a, double command, double setpoint, double deflectionAbs)
        {
            const double rxInterval = 1.0 / RcLinkHz;
            double rxRate = RcLinkHz;
            double deltaAbs = DetMath.Abs(command - s.RcCommand[a]);
            double speed = setpoint - s.RawSetpoint[a];
            double prevSpeed = s.FfSpeedPrev[a], prevAccel = s.FfAccelPrev[a];
            FcFlags duplicateFlag = (FcFlags)((int)FcFlags.DuplicateRoll << a);
            bool prevDuplicate = (s.Flags & duplicateFlag) != 0;

            double zeroAcceleration = 1, jitter = 1;
            if (_ffJitter != 0 && deltaAbs < _ffJitter)
            {
                jitter = DetMath.Max(1 - (deltaAbs + s.FfDeltaPrev[a]) * _ffJitterInv, 0);
                jitter = 1 - jitter * jitter;
            }
            s.FfDeltaPrev[a] = deltaAbs;

            if (deltaAbs != 0)
            {
                if (prevDuplicate)
                {
                    rxRate = 1 / (rxInterval + rxInterval);
                    zeroAcceleration = 0;
                }
                speed *= rxRate;
                s.Flags &= ~duplicateFlag;
            }
            else
            {
                if (!prevDuplicate)
                {
                    speed = prevSpeed;
                    if (DetMath.Abs(setpoint) < 0.95 * _maxRate[a]) speed += prevAccel;
                }
                else
                {
                    speed = 0;
                    prevSpeed = 0;
                }
                zeroAcceleration = 0;
                s.Flags |= duplicateFlag;
            }

            speed = prevSpeed + _ffSmooth * (speed - prevSpeed);
            double acceleration = (speed - prevSpeed) * rxRate * 0.01;
            s.FfSpeedPrev[a] = speed;
            acceleration = prevAccel + _ffSmooth * (acceleration - prevAccel);
            s.FfAccelPrev[a] = acceleration * zeroAcceleration;
            acceleration = acceleration * _ffBoost * jitter * zeroAcceleration;

            double feedforward = speed + acceleration;
            if (_ffTransition != 0 && deflectionAbs < _ffTransition) feedforward *= deflectionAbs * _ffTransitionInv;
            feedforward *= jitter;
            if (_ffMaxRateLimit != 0 && a < 2 && feedforward * setpoint > 0)
            {
                double limit = (_maxRate[a] - DetMath.Abs(setpoint)) * _ffMaxRateLimit;
                feedforward = limit > 0 ? Clamp(feedforward, -limit, limit) : 0;
            }
            s.FfRaw[a] = feedforward;
        }

        /// <summary>An axis's PID gains after Betaflight's scaling (pid_init.c pidInitConfig): per °/s, per °/s·s, per °/s², per °/s².</summary>
        public void Gains(int axis, out double kp, out double ki, out double kd, out double kf)
        {
            kp = _kp[axis];
            ki = _ki[axis];
            kd = _kd[axis];
            kf = _kf[axis];
        }

        /// <summary>rc.c applyActualRates: the setpoint, °/s, of a deflection f in −1…1 (fAbs = |f|).</summary>
        public double ActualRate(int axis, double f, double fAbs)
        {
            double expo = _expo[axis];
            double curve = fAbs * (f * f * f * f * f * expo + f * (1 - expo));
            return f * _centre[axis] + _movement[axis] * curve;
        }

        /// <summary>The setpoint, °/s, a stick position in −1…1 gives once the smoothing has settled: the deadband,
        /// then the rate curve (what Frame computes for one axis).</summary>
        public double SetpointOf(int axis, double stick)
        {
            double f = Deadband(stick, _deadband[axis]) / _divider[axis];
            return Clamp(ActualRate(axis, f, DetMath.Abs(f)), -SetpointRateLimit, SetpointRateLimit);
        }

        /// <summary>rcCommand[THROTTLE], µs, for a throttle stick in 0…1: min_check, then the curve.</summary>
        public double ThrottleCommandOf(double stick)
        {
            double t = Clamp(PwmRangeMin + PwmRange * stick, _s.min_check, PwmRangeMax);
            return LookupThrottle((t - _s.min_check) * PwmRangeMin / (PwmRangeMax - _s.min_check));
        }

        /// <summary>The ESC duty, 0–1, of a mixer output (throttle plus mix) in 0–1, as applyMixToMotors maps it with no sag
        /// compensation.</summary>
        public double DutyOf(double mixerOutput) =>
            (Clamp(_outputLow + (_outputHigh - _outputLow) * mixerOutput, _outputLow, _outputHigh) - DshotMinThrottle) / DshotRange;

        // rc.c updateRcCommands for one axis: |rcData − 1500| capped at 500, less the deadband, signed as the stick.
        static double Deadband(double stick, double deadband)
        {
            double tmp = DetMath.Min(DetMath.Abs(StickHalfRange * stick), StickHalfRange);
            tmp = tmp > deadband ? tmp - deadband : 0;
            return stick < 0 ? -tmp : tmp;
        }

        // rc.c initRcProcessing's lookupThrottleRC, in Betaflight's integer arithmetic (C's truncating division).
        static void BuildThrottleCurve(int mid, int expo, int[] table)
        {
            for (int i = 0; i < table.Length; i++)
            {
                int tmp = 10 * i - mid;
                int y = 1;
                if (tmp > 0) y = 100 - mid;
                if (tmp < 0) y = mid;
                int v = 10 * mid + tmp * (100 - expo + expo * (tmp * tmp) / (y * y)) / 10;
                table[i] = 1000 + 1000 * v / 1000;
            }
        }

        // rc.c rcLookupThrottle on 0…1000, interpolated between the table's points without truncating to whole µs.
        double LookupThrottle(double x)
        {
            int k = (int)(x / 100);
            if (k > 10) k = 10;
            if (k < 0) k = 0;
            return _throttleCurve[k] + (x - 100 * k) * (_throttleCurve[k + 1] - _throttleCurve[k]) / 100;
        }

        // rc.c rcSmoothingSetFilterCutoffs: the link's rate times the factor, truncated to whole Hz, at least 15 Hz.
        static double AutoCutoff(double factor) => DetMath.Max(RcSmoothingMinHz, (int)(RcLinkHz * factor));

        // common/filter.c pt1FilterGain at the loop's dT.
        static double Pt1Gain(double cutoffHz)
        {
            double omega = TwoPi * cutoffHz * LoopDt;
            return omega / (omega + 1);
        }

        // common/filter.c: PT2 and PT3 are PT1 stages in series at a corrected cutoff.
        static void Pt2(ref double s1, ref double s, double input, double k)
        {
            s1 += k * (input - s1);
            s += k * (s1 - s);
        }

        static void Pt3(ref double s1, ref double s2, ref double s, double input, double k)
        {
            s1 += k * (input - s1);
            s2 += k * (s1 - s2);
            s += k * (s2 - s);
        }

        // common/maths.h constrainf: low first.
        static double Clamp(double x, double low, double high) => x < low ? low : x > high ? high : x;

        // common/maths.h fapplyDeadband.
        static double ApplyDeadband(double value, double deadband)
        {
            if (DetMath.Abs(value) < deadband) return 0;
            return value >= 0 ? value - deadband : value + deadband;
        }

        static bool IsWhole(double x, double low, double high) => x >= low && x <= high && x == (int)x;
    }
}
