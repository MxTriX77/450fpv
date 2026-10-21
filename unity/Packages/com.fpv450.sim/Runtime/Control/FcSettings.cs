namespace Fpv450.Sim.Control
{
    /// <summary>Betaflight's `iterm_relax` values.</summary>
    public enum ItermRelax : byte { OFF, RP, RPY, RP_INC, RPY_INC }

    /// <summary>Betaflight's `iterm_relax_type` values.</summary>
    public enum ItermRelaxType : byte { GYRO, SETPOINT }

    /// <summary>Betaflight's `feedforward_averaging` values (CLI OFF, 2_POINT, 3_POINT, 4_POINT).</summary>
    public enum FeedforwardAveraging : byte { OFF, TWO_POINT, THREE_POINT, FOUR_POINT }

    /// <summary>
    /// The flight controller's settings, a Betaflight 4.5 model in acro with ACTUAL rates (D-023). Every field is a
    /// Betaflight CLI name in its CLI unit, so a real `diff all` imports field for field; the input layer edits the
    /// rates and the throttle curve in Calibrate (p6-input-app). The flight takes a copy at its start and logs it.
    ///
    /// Defaults (FcSettings.Default): the rates, throttle curve, deadband, RC smoothing and feedforward are the sim
    /// default column of reference-notes/fc-profiles.md (pilot); the PIDs, D min, `iterm_relax_cutoff` and
    /// `throttle_boost` are the builder's (fc-profiles §1, pilot); the rest are Betaflight 4.5's defaults (published,
    /// re-checked against the source in 1.4) or assumed, as each field says. Every value is a named, tunable default.
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

        // Throttle curve, 0–100: Betaflight's 11-point table from the mid point and the expo.
        public double thr_mid, thr_expo;

        // Deadband, in rcCommand units of the ±500 stick range.
        public double deadband, yaw_deadband;

        // RC smoothing. A cutoff of 0 is "auto", from the measured report rate and the auto factor.
        public bool rc_smoothing;
        public double rc_smoothing_auto_factor, rc_smoothing_auto_factor_throttle;
        public double rc_smoothing_setpoint_cutoff, rc_smoothing_throttle_cutoff;

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

        // I-term relax.
        public ItermRelax iterm_relax;
        public ItermRelaxType iterm_relax_type;
        public double iterm_relax_cutoff;

        // Throttle boost.
        public double throttle_boost, throttle_boost_cutoff;

        // Battery sag compensation, 0–100 %.
        public double vbat_sag_compensation;

        // Motors: idle in hundredths of a percent (550 = 5.5 %), and airmode (CLI `feature AIRMODE`).
        public double dshot_idle_value;
        public bool airmode;

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

            // published: Betaflight 4.5 defaults (the builder kept yaw P at its default)
            p_yaw = 45, d_min_yaw = 0,
            d_min_gain = 37, d_min_advance = 20,
            iterm_limit = 400,
            iterm_relax = ItermRelax.RP, iterm_relax_type = ItermRelaxType.SETPOINT,
            throttle_boost_cutoff = 15,
            rc_smoothing = true,
            rc_smoothing_auto_factor_throttle = 30,
            rc_smoothing_setpoint_cutoff = 0, rc_smoothing_throttle_cutoff = 0,
            feedforward_transition = 0, feedforward_smooth_factor = 25, feedforward_jitter_factor = 7,
            feedforward_boost = 15, feedforward_max_rate_limit = 90,
            feedforward_averaging = FeedforwardAveraging.OFF,
            dshot_idle_value = 550,

            // assumed: the profiles do not record it
            airmode = true,
        };
    }
}
