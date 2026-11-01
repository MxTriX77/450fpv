namespace Fpv450.Sim.Control
{
    /// <summary>Betaflight's `iterm_relax` values.</summary>
    public enum ItermRelax : byte { OFF, RP, RPY, RP_INC, RPY_INC }

    /// <summary>Betaflight's `iterm_relax_type` values.</summary>
    public enum ItermRelaxType : byte { GYRO, SETPOINT }

    /// <summary>Betaflight's `feedforward_averaging` values (CLI OFF, 2_POINT, 3_POINT, 4_POINT).</summary>
    public enum FeedforwardAveraging : byte { OFF, TWO_POINT, THREE_POINT, FOUR_POINT }

    /// <summary>Betaflight's `tpa_mode` values: TPA on P and D, or on D only.</summary>
    public enum TpaMode : byte { PD, D }

    /// <summary>
    /// The flight controller's settings, a Betaflight 4.5 model in acro with ACTUAL rates (D-023). Every field is a
    /// Betaflight CLI name in its CLI unit, so a real `diff all` imports field for field; the input layer edits the
    /// rates and the throttle curve in Calibrate (p6-input-app). The flight takes a copy at its start and logs it.
    ///
    /// Defaults (FcSettings.Default): the rates, throttle curve, deadband, RC smoothing and feedforward are the sim
    /// default column of reference-notes/fc-profiles.md (pilot); the PIDs, D min, `iterm_relax_cutoff`,
    /// `throttle_boost`, the gyro and D-term filters, the RPM filter and the cell warning are the builder's (fc-profiles §1,
    /// pilot); the rest are Betaflight 4.5.0's defaults (published, checked in its source in 1.4 and 1.4b:
    /// src/main/flight/pid.c resetPidProfile, src/main/pg/rx.c, src/main/pg/motor.c,
    /// src/main/config/feature.c). Every value is a named, tunable default. FlightController says which settings it
    /// models and refuses the values it does not (feedforward_averaging, a lowpass type other than BIQUAD).
    ///
    /// ACTUAL rates store the centre sensitivity and the full-stick rate in tens of °/s. Betaflight keeps them as
    /// integers; the sim default column asks for halves (55 and 225 °/s), which this record holds exactly as 5.5 and
    /// 22.5. An imported `diff all` gives integers, which it holds as well.
    /// </summary>
    public struct FcSettings
    {
        // ACTUAL rates. Centre sensitivity = *_rc_rate × 10 °/s, full-stick rate = *_srate × 10 °/s, expo = *_expo / 100.
        public double roll_rc_rate, pitch_rc_rate, yaw_rc_rate;
        public double roll_srate, pitch_srate, yaw_srate;
        public double roll_expo, pitch_expo, yaw_expo;

        // Throttle curve, 0–100: Betaflight's 11-point table from the mid point and the expo (whole numbers, as
        // Betaflight's uint8 settings).
        public double thr_mid, thr_expo;

        // The throttle channel's low end, µs: below it the throttle reads zero and is "low" for arming.
        public double min_check;

        // Deadband, in rcCommand units of the ±500 stick range.
        public double deadband, yaw_deadband;

        // RC smoothing. A cutoff of 0 is "auto", from the link's frame rate and the auto factor.
        public bool rc_smoothing;
        public double rc_smoothing_auto_factor, rc_smoothing_auto_factor_throttle;
        public double rc_smoothing_setpoint_cutoff, rc_smoothing_throttle_cutoff, rc_smoothing_feedforward_cutoff;

        // Feedforward.
        public double f_roll, f_pitch, f_yaw;
        public double feedforward_transition, feedforward_smooth_factor, feedforward_jitter_factor;
        public double feedforward_boost, feedforward_max_rate_limit;
        public FeedforwardAveraging feedforward_averaging;

        // PID gains in Betaflight's units; the controller applies Betaflight's internal scaling (design 4).
        public double p_roll, i_roll, d_roll, d_min_roll;
        public double p_pitch, i_pitch, d_pitch, d_min_pitch;
        public double p_yaw, i_yaw, d_yaw, d_min_yaw;
        public double d_min_gain, d_min_advance;
        public double iterm_limit;

        // The PID sum's limits (roll and pitch, yaw), and the yaw P term's low-pass, Hz (0 = off).
        public double pidsum_limit, pidsum_limit_yaw;
        public double yaw_lowpass_hz;

        // I-term relax.
        public ItermRelax iterm_relax;
        public ItermRelaxType iterm_relax_type;
        public double iterm_relax_cutoff;

        // I-term windup: the I term grows more slowly once the mixer's range passes this share, %.
        public double iterm_windup;

        // Anti-gravity (CLI `feature ANTI_GRAVITY` and its gains).
        public bool anti_gravity;
        public double anti_gravity_gain, anti_gravity_cutoff_hz, anti_gravity_p_gain;

        // TPA: D (or P and D) attenuated above a throttle breakpoint, µs, and a low-throttle rate below another.
        public TpaMode tpa_mode;
        public double tpa_rate, tpa_breakpoint, tpa_low_rate, tpa_low_breakpoint;
        public bool tpa_low_always;

        // Throttle boost.
        public double throttle_boost, throttle_boost_cutoff;

        // The gyro's filters (sensors/gyro.c, gyro_init.c): LPF1, dynamic from its min to its max cutoff with the
        // throttle (a min of 0: static at gyro_lpf1_static_hz) and its curve's expo, tenths; LPF2 at the gyro's 8 kHz;
        // two notches, centre and lower cutoff. Hz; 0 = off.
        public LowpassType gyro_lpf1_type, gyro_lpf2_type;
        public double gyro_lpf1_static_hz, gyro_lpf1_dyn_min_hz, gyro_lpf1_dyn_max_hz, gyro_lpf1_dyn_expo;
        public double gyro_lpf2_static_hz;
        public double gyro_notch1_hz, gyro_notch1_cutoff, gyro_notch2_hz, gyro_notch2_cutoff;

        // The dynamic notch (flight/dyn_notch_filter.c): notches per axis (0–5, 0 = off), Q × 100, its range, Hz.
        public double dyn_notch_count, dyn_notch_q, dyn_notch_min_hz, dyn_notch_max_hz;

        // The RPM filter (flight/rpm_filter.c) on bidirectional DShot's motor speeds: harmonics (0–3, 0 = off),
        // Q × 100, the lowest notch and the fade above it, Hz, and the motor speeds' low-pass, Hz.
        public bool dshot_bidir;
        public double rpm_filter_harmonics, rpm_filter_q, rpm_filter_min_hz, rpm_filter_fade_range_hz, rpm_filter_lpf_hz;

        // The D term's filters (flight/pid_init.c): a notch, centre and lower cutoff; LPF1, dynamic as the gyro's;
        // LPF2. Hz; 0 = off.
        public double dterm_notch_hz, dterm_notch_cutoff;
        public LowpassType dterm_lpf1_type, dterm_lpf2_type;
        public double dterm_lpf1_static_hz, dterm_lpf1_dyn_min_hz, dterm_lpf1_dyn_max_hz, dterm_lpf1_dyn_expo;
        public double dterm_lpf2_static_hz;

        // Battery sag compensation (flight/mixer.c, sensors/battery.c, sensors/voltage.c), 0–150 %: that share of the
        // warning-to-full cell range, over full, taken off the motors' top on a full pack and none at the warning
        // voltage. The cell thresholds, cV; the sag filter's period, tenths of a second; the cell count (0 = counted
        // from vbat_max_cell_voltage when the pack is found).
        public double vbat_sag_compensation;
        public double vbat_warning_cell_voltage, vbat_max_cell_voltage, vbat_sag_lpf_period, force_battery_cell_count;

        // Motors: idle in hundredths of a percent (550 = 5.5 %), the output limit, % of full, airmode (CLI `feature
        // AIRMODE`) and the throttle that activates it, %.
        public double dshot_idle_value;
        public double motor_output_limit;
        public bool airmode;
        public double airmode_start_throttle_percent;

        /// <summary>The defaults, labelled per field.</summary>
        public static FcSettings Default => new FcSettings
        {
            // pilot: fc-profiles §2, sim default column
            roll_rc_rate = 5.5, pitch_rc_rate = 5.5, yaw_rc_rate = 8.5,
            roll_srate = 22.5, pitch_srate = 22.5, yaw_srate = 26,
            roll_expo = 34, pitch_expo = 34, yaw_expo = 15,
            thr_mid = 50, thr_expo = 10,
            deadband = 2, yaw_deadband = 2,
            rc_smoothing_auto_factor = 50,
            f_roll = 50, f_pitch = 50, f_yaw = 0,
            vbat_sag_compensation = 0,

            // pilot: fc-profiles §1, the builder's set
            p_roll = 65, i_roll = 20, d_roll = 70, d_min_roll = 60,
            p_pitch = 60, i_pitch = 20, d_pitch = 65, d_min_pitch = 60,
            i_yaw = 30, d_yaw = 0,
            iterm_relax_cutoff = 8,
            throttle_boost = 5,

            // pilot: fc-profiles §1, the builder's filters, RPM filter and cell warning (hand-set, so simplified filter
            // tuning off)
            gyro_lpf1_type = LowpassType.BIQUAD, gyro_lpf1_dyn_min_hz = 35, gyro_lpf1_dyn_max_hz = 90,
            gyro_lpf2_type = LowpassType.BIQUAD, gyro_lpf2_static_hz = 90,
            gyro_notch1_hz = 50, gyro_notch1_cutoff = 35,
            dyn_notch_q = 500, dyn_notch_min_hz = 60, dyn_notch_max_hz = 200,
            dterm_lpf1_type = LowpassType.BIQUAD, dterm_lpf1_static_hz = 63, dterm_lpf1_dyn_min_hz = 35, dterm_lpf1_dyn_max_hz = 80,
            dterm_lpf2_type = LowpassType.BIQUAD, dterm_lpf2_static_hz = 80,
            dterm_notch_hz = 60, dterm_notch_cutoff = 45,
            dshot_bidir = true, rpm_filter_min_hz = 50,
            vbat_warning_cell_voltage = 300,

            // published: Betaflight 4.5.0's defaults (the builder kept yaw P at its default). pid.c resetPidProfile:
            // the yaw P, D min, I-term, PID-sum, anti-gravity, TPA and throttle-boost values; pid.h PID_YAW_DEFAULT,
            // D_MIN_DEFAULT, PIDSUM_LIMIT(_YAW); pg/rx.c rxConfig: min_check, the RC smoothing and airmode's start;
            // pg/motor.c: dshot_idle_value; config/feature.c: AIRMODE and ANTI_GRAVITY among the default features
            p_yaw = 45, d_min_yaw = 0,
            d_min_gain = 37, d_min_advance = 20,
            iterm_limit = 400,
            pidsum_limit = 500, pidsum_limit_yaw = 400,
            yaw_lowpass_hz = 100,
            iterm_relax = ItermRelax.RP, iterm_relax_type = ItermRelaxType.SETPOINT,
            iterm_windup = 85,
            anti_gravity = true, anti_gravity_gain = 80, anti_gravity_cutoff_hz = 5, anti_gravity_p_gain = 100,
            tpa_mode = TpaMode.D, tpa_rate = 65, tpa_breakpoint = 1350, tpa_low_rate = 20, tpa_low_breakpoint = 1050,
            tpa_low_always = false,
            throttle_boost_cutoff = 15,
            min_check = 1050,
            rc_smoothing = true,
            rc_smoothing_auto_factor_throttle = 30,
            rc_smoothing_setpoint_cutoff = 0, rc_smoothing_throttle_cutoff = 0, rc_smoothing_feedforward_cutoff = 0,
            feedforward_transition = 0, feedforward_smooth_factor = 25, feedforward_jitter_factor = 7,
            feedforward_boost = 15, feedforward_max_rate_limit = 90,
            feedforward_averaging = FeedforwardAveraging.OFF,
            dshot_idle_value = 550,
            motor_output_limit = 100,
            airmode = true,
            airmode_start_throttle_percent = 25,

            // published: Betaflight 4.5.0's defaults (1.4b). sensors/gyro.c pgResetFn_gyroConfig 105–134 (LPF1's
            // static 250 Hz, unused while it is dynamic; the curve's expo 5; notch 2 off), pid.c resetPidProfile 169–205
            // (the D term's expo 5), pg/dyn_notch.c 32–37 (3 notches), pg/rpm_filter.c 32–39 (3 harmonics, Q 500, the
            // fade 50 Hz, the speeds' low-pass 150 Hz), sensors/battery.c 110–132 (vbat_max_cell_voltage 430 as
            // VBAT_CELL_VOLTAGE_DEFAULT_MAX, the sag filter's period 2, the cells counted)
            gyro_lpf1_static_hz = 250, gyro_lpf1_dyn_expo = 5,
            gyro_notch2_hz = 0, gyro_notch2_cutoff = 0,
            dyn_notch_count = 3,
            dterm_lpf1_dyn_expo = 5,
            rpm_filter_harmonics = 3, rpm_filter_q = 500, rpm_filter_fade_range_hz = 50, rpm_filter_lpf_hz = 150,
            vbat_max_cell_voltage = 430, vbat_sag_lpf_period = 2, force_battery_cell_count = 0,
        };
    }
}
