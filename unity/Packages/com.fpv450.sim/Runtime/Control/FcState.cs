using System;

namespace Fpv450.Sim.Control
{
    /// <summary>
    /// One value per flight-controller axis, in the pilot's directions (DetMath.cs, "Pilot axes"; Betaflight's for
    /// roll and pitch): roll +right, pitch +nose down (stick forward), yaw +right. The controller works in °/s.
    /// </summary>
    public struct Axes3
    {
        public double Roll, Pitch, Yaw;

        public double this[int axis]
        {
            readonly get
            {
                switch (axis)
                {
                    case 0: return Roll;
                    case 1: return Pitch;
                    case 2: return Yaw;
                    default: throw new ArgumentOutOfRangeException(nameof(axis));
                }
            }
            set
            {
                switch (axis)
                {
                    case 0: Roll = value; break;
                    case 1: Pitch = value; break;
                    case 2: Yaw = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(axis));
                }
            }
        }
    }

    /// <summary>The flight controller's latched conditions and per-axis bits (FcState.Flags).</summary>
    [Flags]
    public enum FcFlags : ushort
    {
        None = 0,

        /// <summary>The arm switch went on while arming was refused (throttle not low, or on at the start): it must go
        /// off before the drone arms (Betaflight's ARMING_DISABLED_ARM_SWITCH).</summary>
        ArmBlocked = 1 << 0,

        /// <summary>Airmode has activated since arming: the throttle passed airmode_start_throttle_percent.</summary>
        AirmodeActive = 1 << 1,

        /// <summary>The low-throttle TPA has faded out for good (tpa_low_always off, once past its breakpoint).</summary>
        TpaLowFaded = 1 << 2,

        /// <summary>The axis's last RC frame repeated the one before (feedforward's duplicate-packet rule).</summary>
        DuplicateRoll = 1 << 3, DuplicatePitch = 1 << 4, DuplicateYaw = 1 << 5,

        /// <summary>The mixer did not deliver the axis's demand in the last step (MixerSaturation's state).</summary>
        SaturatedRoll = 1 << 6, SaturatedPitch = 1 << 7, SaturatedYaw = 1 << 8, SaturatedCollective = 1 << 9,
    }

    /// <summary>
    /// The flight controller's state: everything Betaflight 4.5 carries from one 4 kHz loop to the next in the model
    /// FlightController runs, so FlightState holds the whole flight (K§6.2) and the log every bit of it, but for the
    /// filter chain's delay lines and its sliding DFT (FilterBank), of which it holds the outputs. A value type.
    /// Names follow Betaflight's where it has them (rc.c, pid.c, mixer.c, gyro_filter_impl.c, dyn_notch_filter.c).
    /// </summary>
    public struct FcState
    {
        // ---- the RC link's last frame (rc.c updateRcCommands, processRcCommand) ----

        /// <summary>rcCommand for roll, pitch and yaw: µs from the centre after the deadband, the pilot's directions.</summary>
        public Axes3 RcCommand;

        /// <summary>rcCommand[THROTTLE] from the throttle curve, 1000–2000 µs, before the RC smoothing.</summary>
        public double RcThrottle;

        /// <summary>rcData[THROTTLE], µs: the throttle channel itself, for the throttle's status and airmode.</summary>
        public double RcDataThrottle;

        /// <summary>rawSetpoint: the rate curve's setpoint, °/s, before the RC smoothing.</summary>
        public Axes3 RawSetpoint;

        // ---- feedforward (rc.c calculateFeedforward) ----

        /// <summary>prevRcCommandDeltaAbs, µs; prevSetpointSpeed, °/s²; prevAcceleration; feedforwardRaw, °/s².</summary>
        public Axes3 FfDeltaPrev, FfSpeedPrev, FfAccelPrev, FfRaw;

        // ---- the RC smoothing's PT3 filters (rc.c processRcSmoothingFilter): two stages, then the output ----

        /// <summary>setpointRate, °/s: the setpoint the PID follows.</summary>
        public Axes3 Setpoint1, Setpoint2, Setpoint;

        /// <summary>rcCommand[THROTTLE] smoothed, µs.</summary>
        public double Throttle1, Throttle2, Throttle;

        /// <summary>feedforwardSmoothed, °/s²: the PID's feedforward input (pidSetpointDelta).</summary>
        public Axes3 Ff1, Ff2, Ff;

        // ---- the PID (pid.c pidController) ----

        /// <summary>The I term, in Betaflight's PID units.</summary>
        public Axes3 ITerm;

        /// <summary>The gyro the last loop read, °/s, before its filters: the start of the plant's ramp to this loop and
        /// the other end of its 8 kHz midpoint.</summary>
        public Axes3 GyroPrev;

        /// <summary>I-term relax's setpoint low-pass (windupLpf), °/s.</summary>
        public Axes3 RelaxLpf;

        /// <summary>D min's PT2 filters: on the D term's gyro delta (dMinRange) and on the gain factor (dMinLowpass).</summary>
        public Axes3 DMinRange1, DMinRange, DMinLpf1, DMinLpf;

        /// <summary>The yaw P term's low-pass, PID units.</summary>
        public double YawP;

        // ---- the mixer (mixer.c mixTable) ----

        /// <summary>Throttle boost's PT1 of the throttle, 0–1.</summary>
        public double BoostLpf;

        /// <summary>Anti-gravity's PT2 of the throttle's derivative (antiGravityThrottleD), and the throttle it last saw.</summary>
        public double AntiGravity1, AntiGravity, ThrottlePrev;

        /// <summary>TPA's factor on D, set by the last mix for the next loop.</summary>
        public double TpaFactor;

        /// <summary>The last mix's range (motorMixRange), for the I term's windup.</summary>
        public double MotorMixRange;

        public FcFlags Flags;

        // ---- the filter chain's outputs and slow parameters (FilterBank; its delay lines are the bank's) ----

        /// <summary>gyroADCf: the gyro after its filters, °/s, which the P and I terms read.</summary>
        public Axes3 GyroFiltered;

        /// <summary>previousGyroRateDterm: the filtered gyro after the D term's filters at the last loop, °/s.</summary>
        public Axes3 DtermGyro;

        /// <summary>The dynamic notches' centres (dynNotch.centerFreq), Hz per axis; zero past dyn_notch_count.</summary>
        public Axes3 DynNotch1, DynNotch2, DynNotch3, DynNotch4, DynNotch5;

        /// <summary>The dynamic LPFs' throttle in hundredths (0–100), and the loops since it last moved them (to 20, 5 ms).</summary>
        public byte DynLpfThrottle, DynLpfLoops;

        // ---- the battery (sensors/voltage.c, battery.c), run only with sag compensation on ----

        /// <summary>The pack voltage through the sag filter, V.</summary>
        public double SagVolts;

        /// <summary>The cell count, found at the battery's first reading; 0 before it.</summary>
        public byte Cells;
    }
}
