using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>The flight's motors, props and packs (drone.json's, DroneSpec.BuildPropulsion); the log header records it.</summary>
    public sealed class PropulsionSpec
    {
        /// <summary>Four, in Betaflight's Quad X order.</summary>
        public DroneMotor[] Motors;

        public DroneProps Props;

        /// <summary>Two, in parallel on the ESCs' bus (PHB-03).</summary>
        public DronePack[] Packs;

        /// <summary>The props' blades as elements (DamagedRotorSpec, p7f-damage 1.2): each rotor's thrust, torque, inertia and
        /// 1/rev loads follow its blades' state; null for none (a bench's props, a drone.json without props.blade), whose
        /// rotors are always the published table's.</summary>
        public DamagedRotorSpec Rotor;
    }

    /// <summary>What the rotors did over a step's sub-steps, for the rigid body, the log and the feed.</summary>
    public struct RotorLoads
    {
        /// <summary>Each rotor's thrust along body +y, N, summed over the sub-steps.</summary>
        public Corners Thrust;

        /// <summary>The rotors' reaction on the body about +y, N·m, summed: their drag torque and spin-up.</summary>
        public double YawMoment;

        /// <summary>Each ESC's draw from the packs, A, summed.</summary>
        public Corners SupplyAmps;

        /// <summary>Each rotor's speed after each sub-step, rad/s, summed (the step's mean speed, for the H-force and
        /// the rotors' angular momentum).</summary>
        public Corners Speed;

        /// <summary>The props' unbalance pull on their hubs, body N, and its moment about the body origin, body N·m, summed.</summary>
        public Double3 UnbalanceForce, UnbalanceMoment;

        /// <summary>The ESCs whose current limit held their motor in a sub-step (bit 1 &lt;&lt; corner), and the largest
        /// share of each one's duty the limit took, 0–1.</summary>
        public int Limited;
        public Corners LimitTaken;

        /// <summary>The packs' terminal voltage, V, and each pack's current, A, at the last sub-step.</summary>
        public double Volts, Amps0, Amps1;

        public int SubSteps;
    }

    /// <summary>
    /// The propulsion chain under the flight controller (design 5, PHB-03), per rotor, at the controller's 4 kHz:
    /// - the ESC: a first-order filter on the duty command, d += (u − d)·h/(τ_esc + h) (drone.json's τ_esc: the DShot
    ///   frame, the PWM's update and the winding's L/R as one lag, about 0.4 ms; the duty is not slewed, as BLHeli_32
    ///   at its maximum acceleration and Bluejay do: p7b-airframe 1.0)
    /// - the motor: a DC motor at the duty's share of the bus voltage, i = (d·V − Ke·ω)/R, its torque Kt·i (Kt = Ke
    ///   in SI) against the no-load loss Kt·I0 and the prop's drag torque, J·ω̇ = Kt·i − Kt·I0 − Q. One state, ω; its
    ///   lag τ_m = J / (Kt·Ke/R + ∂Q/∂ω) follows from the load (about 40 ms at hover, Propulsion's tests), and the
    ///   update is the design's n += (n* − n)·h/(τ + h): backward Euler on the linearised motor, no transcendental
    ///   functions. The ESC drives both ways (Betaflight's DShot ESCs brake actively), so the current goes negative
    ///   when the prop is pulled down.
    /// - the disarm (drone.json's esc_disarm, EscDisarm; p6-flight-core 1.6c): Betaflight sends DShot 0 and the class's
    ///   BLHeli_S (or Bluejay) stays in its run loop at zero power, its complementary PWM's damping FET shorting the active
    ///   phases: the same motor at d = 0, i = −Ke·ω/R, a braking torque with no supply current. Below its firmware's
    ///   minimum speed (or past its stop counter) it turns every FET off and the motor coasts (i = 0); with brake on stop it
    ///   then turns on the three low-side FETs, a three-phase short, i = −1.095·Ke·ω/R. Freewheel coasts at once. The ESC
    ///   starts with its motor stopped, every FET off, and brakes on stop only after a run loop.
    /// - the prop: T = k_T(rpm)·f_T(J)·ω² and Q = κ·k_T·q(rpm)·f_Q(J)·ω², drone.json's published tables of the
    ///   static coefficients against rpm (C_T rises with rpm) and against the advance ratio J = V_a/(n·D) of the
    ///   axial inflow through the disc (the forward-flight derate, PHB-03): never a flat factor. A descending rotor
    ///   (J below 0) keeps the static slope to DescentAdvanceRatioLimit, then holds; the vortex ring replaces that
    ///   branch in p7b-airframe.
    /// - the ESC's current limit (drone.json's esc_current_limit_a; the policy of p6-flight-core 1.5): an ESC whose
    ///   motor would draw more than the limit while driving lowers its effective duty to d' = (I_lim·R + Ke·ω)/V, so
    ///   the motor's current is the limit, as BLHeli_32's per-ESC current protection does (its manual: fast enough
    ///   to limit the current during accelerations too). A limited motor is a current source, its supply current
    ///   d'·I_lim = (I_lim·R + Ke·ω)·I_lim / V a constant-power load on the bus. Braking current is not limited.
    /// - the packs: two in parallel at one bus voltage, each V_k = n_s·OCV(SoC_k) less its current through
    ///   n_s·R_cell/n_p, solved with the ESCs' load in closed form (the free ESCs' load is linear in V, the limited
    ///   ones' constant power, so V is a quadratic's root: √ only); each SoC integrates its current. So the motors'
    ///   top speed follows the sagging voltage.
    /// - the inflow: each hub's velocity through the air in body axes (the body's motion, ω × r, less the wind at the
    ///   hub): its part along the disc's normal is the axial inflow, its part in the disc's plane drives the rotor's
    ///   in-plane drag, the H-force, −h·(ω/ω_ref)·v_∥ (drone.json's h_force_n_per_m_s at reference_rpm; design 6:
    ///   it grows with the airspeed and with the rotor, as published rotor-drag models have it, ∝ ω·v_∥).
    /// The body feels each rotor's thrust and H-force at its hub and, about +y, the reaction of its motor: the prop's
    /// drag torque plus the rotor's own spin-up, J·ω̇ (a clockwise rotor yaws the body left). The rotors' spin
    /// momentum h = Σ ±J·ω (SpinMomentum) gives the gyroscopic moment −ω × h (FlightCore).
    /// - the asymmetry (MotorAsymmetry, p7b-airframe 1.1): each motor's Kv (so its Ke and Kt), each ESC's lag and each
    ///   prop's residual unbalance as the flight drew them; the unbalance pulls its hub with U·ω² at the rotor's angle.
    /// - the vortex ring (VortexRingSpec, p7b-airframe 1.2), inside RotorInflow: each rotor's ring adds its lagged inflow
    ///   to the thrust's advance ratio and its fluctuation to the thrust; AdvanceRing moves the rings once a step.
    /// - the ground and the ceiling (GroundEffectSpec, p7b-airframe 1.3), inside RotorInflow: each rotor's thrust at its speed
    ///   gains by its distances along its axis to the surfaces below and above it (SetSurfaces), and its re-ingested wash
    ///   adds its inflow to the thrust's and the torque's advance ratio; AdvanceWash moves the wash once a step.
    /// - the rotor's own wake (RotorWakeSpec, D-064), inside RotorInflow: each rotor's wandering wake inflow joins the wash's
    ///   in the thrust's and the torque's advance ratio; AdvanceWake moves it once a step.
    /// - the rotors in each other's wake (RotorInterferenceSpec, p7b-airframe 1.3d), inside RotorInflow: each rotor's axial inflow takes
    ///   the other rotors' wakes' downwash or upwash at its disc, scaled by its fluctuation; AdvanceInterference moves that once a step.
    /// - the motors' own dynamics (MotorAsymmetry, D-064): each winding's resistance, each motor's friction and each rotor's
    ///   inertia as the flight drew them.
    /// - the blades (DamagedRotor, p7f-damage 1.2), with PropulsionSpec.Rotor: a rotor whose blades are off the sheet (a
    ///   damage, or the factory pitch offsets the asymmetry draws, prop.blade_pitch) takes its thrust and torque times its
    ///   tables' η at their advance ratios, its J less the material lost, and, beside the unbalance at each sub-step, the lost
    ///   material's pull, its blades' unequal lift and drag (a hub moment and an in-plane force) and its flap sets' couple,
    ///   all turning with RotorAngle. A sound rotor with no pitch offset skips all of it: the published table, bit for bit.
    /// - a disc partly inside a carved face (PropStrikes, p7f-damage 1.3's tip slide; SetImmersed): its blades make no thrust
    ///   and no air torque there (the face's carve is their drag), so its thrust and torque lose the immersed part's shares of
    ///   its blades' loads (DamagedRotor.Immersed), from the step after the strikes found it. 0 shares skip it, bit for bit.
    /// - the motor's damage (Contact.MotorBell, p7f-damage 1.5, SetMotor): its Kt (= Ke) ratio from its gap's flux and its added
    ///   drag beside Kt·I0 (the bell's scrape, the magnets' rub), a Coulomb drag turning and a hold at rest; so a dragging motor
    ///   draws more current for the same thrust and reaches a current limit sooner, and one its drag holds at rest under drive is
    ///   bound (Bound). Never set, the motor is the drawn one, bit for bit.
    /// - the shaft (Contact.MotorShaft, p7f-damage 1.6, SetShaft and LoseProp): a bent shaft's 1/rev loads, fixed in the rotating
    ///   frame and turning with RotorAngle at each sub-step beside the unbalance: the masses' pull ω²·U at the hub, their couples
    ///   ω²·K, the thrust tilted, T·u at the hub with its line through the hinge (a couple T·l·(u × ŷ)), and the drag torque's
    ///   tilt, its reaction·Q·u. A broken shaft loses its prop: no thrust or air torque, J the bell's, no factory unbalance and no
    ///   blades. Never set, the rotor is model 63's, bit for bit.
    /// </summary>
    public sealed class Propulsion
    {
        /// <summary>The advance ratio a descending rotor's static slope runs to (assumed: a 1.9 m/s descent at hover
        /// speed, below the vortex ring's onset). It ends near η 0.19 (the static C_T's v_h is 0.263·n·D), by Johnson's VzD
        /// 0.2, where his VRS inflow leaves momentum theory; the vortex ring (VortexRingSpec) adds to it from there.</summary>
        public const double DescentAdvanceRatioLimit = -0.05;

        const double Pi = 3.141592653589793, TwoPi = 6.283185307179586;
        const double SecondsPerHour = 3600;

        readonly double[] _ke = new double[4], _invR = new double[4], _friction = new double[4], _inertia = new double[4];
        readonly double[] _escTau = new double[4], _reaction = new double[4], _polePairs = new double[4];
        readonly double[] _limit = new double[4], _limitVolts = new double[4];
        readonly Double3[] _hub = new Double3[4];
        readonly double[] _unbalance = new double[4], _unbalanceAngle = new double[4];
        readonly bool _unbalanced;
        readonly double _kT, _kQ, _diameter, _hPerSpeed;
        readonly double[] _rpm, _rpmThrust, _rpmTorque, _j, _jThrust, _jTorque;
        readonly double[] _series = new double[2], _conductance = new double[2], _capacity = new double[2];

        // The ESCs on a disarm (EscDisarm): each one's speed below which its firmware leaves the run loop (rad/s; +∞ for
        // Freewheel), the time after the disarm at which its stop counter does (s; +∞ for none), and when its brake on stop
        // turns on the low-side FETs (s; +∞ without it). The state: the sub-steps since the disarm, the ESCs out of their
        // run loop and those whose brake on stop is set, bit 1 << corner; it is a function of the flight so far, like the
        // contact model's tables, and the flight starts with every motor stopped.
        readonly double[] _runExitSpeed = new double[4], _stopCountExit = new double[4], _brakeAt = new double[4];
        int _stopLoops, _escOff = 0xF, _escBrake;

        // The firmwares' published rules (BLHeli_S 16.7, bitdump/BLHeli's BLHeli_S SiLabs/BLHeli_S.asm; Bluejay 0.21,
        // bird-sanctuary/bluejay's src/Bluejay.asm, Modules/Isrs.asm and Settings/BluejaySettings.asm):
        // - the run loop's minimum speed: BLHeli_S's run6_brake_done, "Comm_Period4x more than 32ms (~1220 eRPM)";
        //   Bluejay's run6_check_speed, "Default minimum speed (~1330 erpm)"
        // - the stop counter: Rcp_Stop_Cnt counts the ticks of timer 2 with a zero throttle, BLHeli_S's every 32 ms (on a
        //   48 MHz MCU it skips every other interrupt), Bluejay's every 16 ms after arming on 48 MHz MCUs; BLHeli_S leaves
        //   its run loop past 250 ticks, or 3 with brake on stop ("About 100ms before stopping when brake is set"); Bluejay
        //   only with brake on stop, past 3. The limit passes 2–3 ticks after the zero throttle by the timer's phase,
        //   which nothing sets: the middle, 2.5 (assumed).
        // - brake on stop: BLHeli_S's run_to_wait_for_power_on turns every FET off, waits 100 ms ("Wait for pwm to be
        //   stopped") and turns on AcomFET, BcomFET and CcomFET; Bluejay's exit_run_mode_no_stall_no_beep turns on its
        //   three Com FETs at once. Both default it off (DEFAULT_PGM_BRAKE_ON_STOP 0), and Bluejay's braking strength to
        //   its full 255.
        const double BlheliSExitErpm = 1220, BluejayExitErpm = 1330, BlheliSTick = 0.032, BluejayTick = 0.016;
        const double BlheliSStopTicks = 250, BrakeStopTicks = 3, TickPhase = 0.5, BlheliSBrakeWait = 0.1;

        /// <summary>A three-phase short's braking torque over the run loop's two-phase short at zero duty, which the DC model's
        /// Ke and R describe (derived: sinusoidal back-EMF E a phase, no inductance; all three phases shorted dissipate
        /// 3/2·E²/R_ph, two phases across a commutation's 60° about the line voltage's peak 3E²·(1/2 + 3√3/4π)/(2R_ph) =
        /// 1.370·E²/R_ph).</summary>
        public const double ThreePhaseShort = 1.0947;

        // The vortex ring (null: none): its spec and stream, the rotor's radius, 2ρA, the held descent branch's inflow over
        // v_h, and the advance ratio a m/s of the ring's inflow makes at 1 rad/s.
        readonly VortexRingSpec _ring;
        readonly SeedStream _ringStream;
        readonly double _radius, _twoRhoArea, _heldInflow, _ringAdvance;
        readonly double[] _ringInflow = new double[4], _ringGain = new double[4], _meanThrust = new double[4];
        readonly double[] _ringRate = new double[4], _ringInPlane = new double[4], _ringInduced = new double[4];

        // The shares of each rotor's thrust and torque its disc's part inside a carved face makes no more (SetImmersed).
        readonly double[] _immersedThrust = new double[4], _immersedTorque = new double[4];
        readonly double[] _shaftTorque = new double[4], _shaftStiffness = new double[4], _shaftDamping = new double[4], _shaftReference = new double[4];
        readonly double[] _back = new double[4]; // a resting rotor's backward turn rate, rad/s (its speed is never negative)

        // The ground and the ceiling (null: neither): their spec and the wash's stream; each rotor's distances along its axis
        // to them (m, +∞ for none), its gain from them, and its v_h (m/s) and re-ingested inflow (m/s) at the step's start.
        readonly GroundEffectSpec _ground;
        readonly SeedStream _groundStream;
        Corners _groundAt, _ceilingAt;
        readonly double[] _surfaceGain = new double[4], _washInflow = new double[4], _washInduced = new double[4];
        readonly double[][] _ocv = new double[2][];

        // The rotor's own wake (null: none): its spec and stream, its σ over v_h, each rotor's wake inflow (m/s) at the step's
        // start; and the inflow over v_h of a thrust share through the props' static slope.
        readonly RotorWakeSpec _wake;
        readonly SeedStream _wakeStream;
        readonly double _wakeSigma;
        readonly double[] _wakeInflow = new double[4];
        readonly double _shareToInflow;

        // The rotors in each other's wake (null: none): its spec, table and stream; each rotor's induced velocity (m/s), its wake's
        // tan(χ/2) and downstream direction (body x, z) and its in-plane airspeed (m/s) at the step's start, and the interference
        // it takes (m/s).
        readonly RotorInterferenceSpec _interference;
        readonly RotorInterferenceTable _interferenceTable;
        readonly SeedStream _interferenceStream;
        readonly double[] _interferenceInduced = new double[4], _interferenceSkew = new double[4], _interferenceDx = new double[4], _interferenceDz = new double[4];
        readonly double[] _interferenceSpeed = new double[4];
        Corners _interferenceInflow;

        // The blades (null: none): each rotor's, the rotors off their sound path (bit 1 << corner), each rotor's J with sound
        // blades, and the step's 1/rev aerodynamic loads per rotor in its frame (DamagedRotor.Loads): moment and force, p and q.
        readonly DamagedRotor[] _rotors;
        int _damaged;
        readonly double _propInertia; // the blades' and the hub's J, kg·m² (PropBlade.Inertia; a lost prop's)
        readonly double[] _soundInertia = new double[4];
        readonly double[] _momentP = new double[4], _momentQ = new double[4], _forceP = new double[4], _forceQ = new double[4];

        // The motors' damage (SetMotor, p7f-damage 1.5): each motor's Ke as drawn, its Kt ratio's share of it now in _ke, and its
        // added drag (N·m: a Coulomb drag while it turns, a hold at rest); the rotors its drag held at rest under drive in the last
        // sub-step (bit 1 << corner), the drive's torque Kt·i there (N·m) and the bus voltage.
        readonly double[] _keDrawn = new double[4], _drag = new double[4], _heldDrive = new double[4];
        int _bound;

        // The shafts (SetShaft, LoseProp; p7f-damage 1.6): the bent ones (bit 1 << corner) and each one's unbalance (kg·m), couple per
        // ω² (kg·m²) and tilt u = φ × ŷ (rad), (p, q) in the rotor's frame, and its hub's lever over the hinge (m); the props lost.
        int _bent, _lost;
        readonly double[] _shaftUp = new double[4], _shaftUq = new double[4], _shaftKp = new double[4], _shaftKq = new double[4];
        readonly double[] _shaftTp = new double[4], _shaftTq = new double[4], _shaftLever = new double[4];

        // The arms in their plane (SetArms, ArmFlex, model 72; null: stiff that way, model 68's), and the ESCs stopped with their
        // motors turning (StopEsc: their FETs off, the rotor coasts; bit 1 << corner).
        ArmFlex _arms;
        int _escStopped;

        // Each hub's elastic velocity along +y (SetHubElastic), and whether the modes set it.
        Corners _hubElastic;
        bool _elastic;

        // The step's inflow and in-plane airspeed per rotor, and each rotor's thrust and torque at its present speed.
        Corners _inflow;
        readonly Double3[] _inPlane = new Double3[4];
        readonly double[] _thrust = new double[4], _torque = new double[4];

        public Propulsion(PropulsionSpec spec) : this(spec, null) { }

        public Propulsion(PropulsionSpec spec, MotorAsymmetry.Draws draws) : this(spec, draws, null, default) { }

        /// <summary>The propulsion of spec, its four motors, ESCs and props each as drawn (MotorAsymmetry; null for four
        /// identical ones), with each rotor's vortex ring from ring and its thrust's fluctuation from ringStream (null for
        /// none).</summary>
        public Propulsion(PropulsionSpec spec, MotorAsymmetry.Draws draws, VortexRingSpec ring, SeedStream ringStream)
            : this(spec, draws, ring, ringStream, null, default, null, default, null, default) { }

        /// <summary>The same, with each rotor's ground and ceiling effect from ground and its re-ingested wash from
        /// groundStream (null for neither).</summary>
        public Propulsion(PropulsionSpec spec, MotorAsymmetry.Draws draws, VortexRingSpec ring, SeedStream ringStream,
            GroundEffectSpec ground, SeedStream groundStream)
            : this(spec, draws, ring, ringStream, ground, groundStream, null, default) { }

        /// <summary>The same, with each rotor's own wake unsteadiness from wake and its stream wakeStream (null for none: a
        /// perfectly steady rotor, the known-wrong of D-064).</summary>
        public Propulsion(PropulsionSpec spec, MotorAsymmetry.Draws draws, VortexRingSpec ring, SeedStream ringStream,
            GroundEffectSpec ground, SeedStream groundStream, RotorWakeSpec wake, SeedStream wakeStream)
            : this(spec, draws, ring, ringStream, ground, groundStream, wake, wakeStream, null, default) { }

        /// <summary>The same, with the rotors in each other's wake from interference and its fluctuation from
        /// interferenceStream (null for none).</summary>
        public Propulsion(PropulsionSpec spec, MotorAsymmetry.Draws draws, VortexRingSpec ring, SeedStream ringStream,
            GroundEffectSpec ground, SeedStream groundStream, RotorWakeSpec wake, SeedStream wakeStream,
            RotorInterferenceSpec interference, SeedStream interferenceStream)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Motors == null || spec.Motors.Length != 4) throw new ArgumentException("four motors", nameof(spec));
            if (spec.Packs == null || spec.Packs.Length != 2) throw new ArgumentException("two packs", nameof(spec));
            for (int i = 0; i < 4; i++)
            {
                DroneMotor m = spec.Motors[i];
                _ke[i] = _keDrawn[i] = draws == null ? 60 / (TwoPi * m.Kv) : 60 / (TwoPi * (m.Kv * draws.KvScale[i]));
                double resistance = draws?.ResistanceScale == null ? m.Resistance : m.Resistance * draws.ResistanceScale[i];
                _invR[i] = 1 / resistance;
                _friction[i] = draws?.FrictionScale == null ? _ke[i] * m.NoLoadCurrent : _ke[i] * (m.NoLoadCurrent * draws.FrictionScale[i]);
                _inertia[i] = draws?.InertiaScale == null ? m.RotorInertia : m.RotorInertia * draws.InertiaScale[i];
                _escTau[i] = draws == null ? m.EscTau : m.EscTau + draws.EscLag[i];
                _reaction[i] = m.Clockwise ? -1 : +1;
                _polePairs[i] = m.MagnetPoles / 2;
                _hub[i] = m.Position;
                _limit[i] = m.EscCurrentLimit;
                _limitVolts[i] = m.EscCurrentLimit * resistance;
                EscRules(m.EscDisarm, _polePairs[i], out _runExitSpeed[i], out _stopCountExit[i], out _brakeAt[i]);
                if (draws == null) continue;
                _unbalance[i] = draws.Unbalance[i];
                _unbalanceAngle[i] = draws.UnbalanceAngle[i];
                _unbalanced |= _unbalance[i] > 0;
            }
            DroneProps p = spec.Props;
            _kT = p.ThrustCoefficient;
            _kQ = p.TorqueRatio * p.ThrustCoefficient;
            _diameter = p.Diameter;
            _hPerSpeed = p.HForce / (p.ReferenceRpm * (TwoPi / 60));
            _rpm = p.StaticRpm;
            _rpmThrust = p.StaticThrustRatio;
            _rpmTorque = p.StaticTorqueRatio;
            _j = p.InflowAdvanceRatio;
            _jThrust = p.InflowThrustRatio;
            _jTorque = p.InflowTorqueRatio;
            for (int k = 0; k < 2; k++)
            {
                DronePack pack = spec.Packs[k];
                _series[k] = pack.Series;
                _conductance[k] = pack.Parallel / (pack.Series * pack.CellResistance);
                _capacity[k] = pack.Parallel * pack.CellCapacity * SecondsPerHour;
                _ocv[k] = pack.Ocv;
            }
            for (int i = 0; i < 4; i++)
            {
                _ringGain[i] = _surfaceGain[i] = 1;
                _groundAt[i] = _ceilingAt[i] = double.PositiveInfinity;
            }
            // The blades: each rotor's, with the factory pitch offsets drawn for it (rotor-major), into its tables.
            double[] pitch = draws?.BladePitch;
            if (spec.Rotor != null)
            {
                _rotors = new DamagedRotor[4];
                _propInertia = spec.Rotor.Blade.Inertia(p.Diameter / 2, p.Blades);
                for (int i = 0; i < 4; i++)
                {
                    _soundInertia[i] = _inertia[i];
                    _rotors[i] = new DamagedRotor(spec.Rotor, p.Diameter / 2, p.Blades, _reaction[i], p.InflowAdvanceRatio);
                    for (int b = 0; b < p.Blades; b++)
                        _rotors[i].Blade(b).Reset(pitch == null ? 0 : pitch[i * p.Blades + b]);
                    Rebuild(i);
                }
            }
            else if (pitch != null)
            {
                for (int k = 0; k < pitch.Length; k++)
                    if (pitch[k] != 0) throw new ArgumentException("the blades' factory pitch needs the props' blade (PropulsionSpec.Rotor)", nameof(draws));
            }
            if (interference != null)
            {
                _interference = interference.Clone();
                _interferenceTable = _interference.Table(_hub);
                _interferenceStream = interferenceStream;
            }
            if (ring == null && ground == null && wake == null) return;
            _ring = ring?.Clone();
            _ringStream = ringStream;
            _ground = ground?.Clone();
            _groundStream = groundStream;
            _wake = wake?.Clone();
            _wakeStream = wakeStream;
            double rho = ring != null ? ring.AirDensity : ground != null ? ground.AirDensity : wake.AirDensity;
            _radius = _diameter / 2;
            _twoRhoArea = 2 * rho * (TwoPi / 2) * _radius * _radius;
            // The static C_T at the reference rpm, k_T·(2π)²/(ρD⁴), sets v_h/(n·D) = √(2C_T/π) (momentum theory). The table's
            // static slope s (∂ratio/∂J) with momentum theory at hover gives the blade's thrust against the inflow through the
            // disc: a climb's ΔV moves that inflow by (1 − r)/2·ΔV, r = −s·√(2C_T/π) (derived, linearised at hover), so a
            // ring's added inflow δ is the advance ratio 2δ/((1 − r)·n·D).
            double ct = _kT * TwoPi * TwoPi / (rho * _diameter * _diameter * _diameter * _diameter);
            double vhOverNd = DetMath.Sqrt(2 * ct / (TwoPi / 2));
            double r = -(_jThrust[1] - _jThrust[0]) / (_j[1] - _j[0]) * vhOverNd;
            _ringAdvance = 2 * TwoPi / ((1 - r) * _diameter);
            // The held branch's inflow over v_h: momentum theory at the descent where it holds (η of the limit).
            double held = -DescentAdvanceRatioLimit / vhOverNd;
            _heldInflow = -0.5 * held + DetMath.Sqrt(0.25 * held * held + 1);
            // An added inflow x·v_h at hover moves the thrust by the static slope through that advance ratio, −2r·x/(1 − r) of
            // it, so a thrust share σ_T is the inflow σ_T·(1 − r)/(2r) over v_h (derived; 1.5 at r 0.25).
            _shareToInflow = (1 - r) / (2 * r);
            if (_wake != null) _wakeSigma = _wake.ThrustSigma * _shareToInflow;
        }

        /// <summary>Whether the rotors carry their own wake's unsteadiness (RotorWakeSpec).</summary>
        public bool HasWake => _wake != null;

        /// <summary>The rotors' wake σ in hover, over v_h (the thrust's σ through the props' static slope).</summary>
        public double WakeSigma => _wakeSigma;

        /// <summary>The inflow over v_h of a thrust share at hover, through the props' static slope (derived).</summary>
        public double ShareToInflow => _shareToInflow;

        /// <summary>Whether the rotors have their vortex rings (VortexRingSpec).</summary>
        public bool HasRing => _ring != null;

        /// <summary>Whether the rotors feel the ground and the ceiling (GroundEffectSpec).</summary>
        public bool HasGround => _ground != null;

        /// <summary>Whether the rotors take each other's wake (RotorInterferenceSpec).</summary>
        public bool HasInterference => _interference != null;

        /// <summary>Each rotor's added inflow from the other rotors' wakes this step, m/s, positive down through the disc (0
        /// without the wake): inside Inflow.</summary>
        public ref readonly Corners InterferenceInflow => ref _interferenceInflow;

        /// <summary>A rotor's induced velocity (m/s) and its wake's tan(χ/2) this step, from RotorInflow (0 without the wake).</summary>
        public double InterferenceInduced(int corner) => _interferenceInduced[corner];
        public double InterferenceSkew(int corner) => _interferenceSkew[corner];

        /// <summary>A rotor's gain from the ground and the ceiling this step (1 without them).</summary>
        public double SurfaceGain(int corner) => _surfaceGain[corner];

        /// <summary>A rotor's thrust at its present speed with this step's inflow, N (after BeginStep, then each sub-step).</summary>
        public double Thrust(int corner) => _thrust[corner];

        /// <summary>A rotor's drag torque at its present speed with this step's inflow, N·m (after BeginStep, then each sub-step).</summary>
        public double Torque(int corner) => _torque[corner];

        /// <summary>
        /// The shares of a rotor's thrust and air torque that its disc's part inside a carved face (the soil, a softer shape)
        /// no longer makes, 0–1 (PropStrikes after each Sense, DamagedRotor.Immersed): RotorAero's from the next step on.
        /// </summary>
        public void SetImmersed(int corner, double thrust, double torque)
        {
            _immersedThrust[corner] = thrust;
            _immersedTorque[corner] = torque;
        }

        /// <summary>
        /// A resting prop's blades' moment about its shaft (ContactModel, p7f-damage 1.3b), for the next step's sub-steps: the
        /// moment along the rotor's turn at rest, N·m, at the angle where its blades were placed (reference, rad), and its
        /// stiffness (N·m/rad) and damping (N·m·s/rad) against a turn from there: τ = torque − stiffness·(θ − reference) −
        /// damping·ω. A resting rotor is massless (assumed: its J's energy at those speeds negligible against the contact's):
        /// each sub-step it turns to where the moment, its motor's drive and its own drag balance, implicitly in both the
        /// stiffness and the damping (derived); forward no faster than its J and the net moment allow (its dynamics), backward
        /// (its speed held at 0) at most BackSpeed·h. At rest the motor's own drag, Kt·I0 (its published no-load current), holds
        /// it while the moment and the drive are within it (assumed: a Coulomb hold, the iron's hysteresis drag and the detent
        /// being of that order at rest; past it the drive is less that drag). All 0 for a rotor whose blades do not rest.
        /// </summary>
        public void SetShaftLoad(int corner, double torque, double stiffness, double damping, double reference)
        {
            _shaftTorque[corner] = torque;
            _shaftStiffness[corner] = stiffness;
            _shaftDamping[corner] = damping;
            _shaftReference[corner] = reference;
        }

        /// <summary>
        /// A motor's damage for the next step's sub-steps (Contact.MotorBell, p7f-damage 1.5, design 10): its torque constant over
        /// the drawn one, ktRatio (Kt = Ke, so its back-EMF too), and its added drag torque beside Kt·I0, N·m: the bell's scrape on
        /// what it touches and the magnets' rub on the stator (1.6's bearings and 1.6b's dirt add to it). A Coulomb drag: against
        /// the turn while the rotor turns; at rest a hold the drive must pass before the rotor turns (with Kt·I0 then held too).
        /// The ESC and the bus see it only through the current. A motor never set flies its drawn Ke and no drag, bit for bit.
        /// </summary>
        public void SetMotor(int corner, double ktRatio, double drag)
        {
            _ke[corner] = _keDrawn[corner] * ktRatio;
            _drag[corner] = drag;
        }

        /// <summary>
        /// A rotor's bent shaft for the next step's sub-steps (Contact.MotorShaft, p7f-damage 1.6, design 9), in its frame (p along
        /// blade 0, q = p × ŷ): the turning masses' unbalance, kg·m (its pull ω²·U at the hub), their couple per ω², kg·m², the
        /// shaft's tilt u = φ × ŷ, rad (the thrust T·u at the hub, its line through the hinge, a couple T·lever·(u × ŷ); the drag
        /// torque's reaction·Q·u), and the hub's lever over the hinge, m. All 0: straight, model 63's rotor bit for bit.
        /// </summary>
        public void SetShaft(int corner, double unbalanceP, double unbalanceQ, double coupleP, double coupleQ, double tiltP, double tiltQ, double lever)
        {
            _shaftUp[corner] = unbalanceP;
            _shaftUq[corner] = unbalanceQ;
            _shaftKp[corner] = coupleP;
            _shaftKq[corner] = coupleQ;
            _shaftTp[corner] = tiltP;
            _shaftTq[corner] = tiltQ;
            _shaftLever[corner] = lever;
            bool bent = unbalanceP != 0 || unbalanceQ != 0 || coupleP != 0 || coupleQ != 0 || tiltP != 0 || tiltQ != 0;
            if (bent) _bent |= 1 << corner;
            else _bent &= ~(1 << corner);
        }

        /// <summary>
        /// A rotor's prop lost with its broken shaft (Contact.MotorShaft, PropLost): from the next sub-step no thrust and no air
        /// torque, its J the bell's (the drawn J less the prop's blades and hub, PropBlade.Inertia), no factory unbalance (the
        /// prop's), its blades gone (trimmed to nothing, so nothing strikes or rests), no blade loads.
        /// </summary>
        public void LoseProp(int corner)
        {
            int bit = 1 << corner;
            if ((_lost & bit) != 0) return;
            _lost |= bit;
            _unbalance[corner] = 0;
            if (_rotors != null)
            {
                DamagedRotor r = _rotors[corner];
                for (int b = 0; b < r.Blades; b++) r.Blade(b).Trim(0);
                r.Rebuild();
                _damaged &= ~bit;
                _momentP[corner] = _momentQ[corner] = _forceP[corner] = _forceQ[corner] = 0;
                _inertia[corner] = _soundInertia[corner] - _propInertia;
            }
            _thrust[corner] = _torque[corner] = _meanThrust[corner] = 0;
        }

        /// <summary>The arms in their plane (ArmFlex), from the next sub-step: the rotors' in-plane loads reach the body through
        /// them. The core sets them with the airframe's modes (AirframeModesSpec.ArmInPlane).</summary>
        public void SetArms(ArmFlex arms) => _arms = arms;

        /// <summary>
        /// A rotor's ESC stopped from the next sub-step (a motor that stops in flight: an ESC's fault, or a desync it does not
        /// restart from): its FETs off, no drive and no brake, so the rotor coasts down under its prop's torque while the
        /// controller still commands it. A scenario's fixture (p7f-damage 1.6, the losses), like Rebuild's trims: not in the log.
        /// </summary>
        public void StopEsc(int corner) => _escStopped |= 1 << corner;

        /// <summary>The rotors whose prop is lost (bit 1 &lt;&lt; corner), and those whose shaft is bent.</summary>
        public int Lost => _lost;
        public int Bent => _bent;

        /// <summary>A motor's Kt (= Ke) now, N·m/A, and its added drag torque, N·m (SetMotor).</summary>
        public double TorqueConstant(int corner) => _ke[corner];
        public double MotorDrag(int corner) => _drag[corner];

        /// <summary>The rotors their drag held at rest under drive in the last sub-step (bit 1 &lt;&lt; corner): the drive the ESC put
        /// into the stalled winding at its duty, less the prop's torque, did not pass the motor's drag (a bound motor, design 8,
        /// D-073); and that drive's torque Kt·i, N·m.</summary>
        public int Bound => _bound;
        public double HeldDrive(int corner) => _heldDrive[corner];

        /// <summary>The bus voltage at the last sub-step, V (an output: Contact.MotorShaft's stalled drive Kt·V/R).</summary>
        public double BusVolts { get; private set; }

        /// <summary>A motor's DC resistance as drawn (winding, ESC and leads), Ω.</summary>
        public double Resistance(int corner) => 1 / _invR[corner];

        public double ShaftTorque(int corner) => _shaftTorque[corner];
        public double ShaftStiffness(int corner) => _shaftStiffness[corner];

        /// <summary>The fastest backward turn of a resting rotor, rad/s (ContactSpec.BladeRestSpeed, set by FlightCore).</summary>
        public double BackSpeed;

        public double ImmersedThrust(int corner) => _immersedThrust[corner];
        public double ImmersedTorque(int corner) => _immersedTorque[corner];

        /// <summary>
        /// Each rotor's distance along its axis to the surface below it (the ground or a shape) and above it, m, +∞ for none
        /// within the rays' reach (FlightCore's rays and SampleGround): RotorInflow's ground and ceiling from then on.
        /// </summary>
        public void SetSurfaces(in Corners ground, in Corners ceiling)
        {
            _groundAt = ground;
            _ceilingAt = ceiling;
        }

        /// <summary>
        /// Each hub's elastic velocity along +y, m/s (AirframeModes, p7b-airframe 1.4): RotorInflow adds it to the hub's
        /// air from then on, so a flexing arm's prop damps it. Never set without the modes.
        /// </summary>
        public void SetHubElastic(in Corners velocity)
        {
            _hubElastic = velocity;
            _elastic = true;
        }

        /// <summary>The held descent branch's inflow through the disc over v_h, which the ring adds to (derived).</summary>
        public double HeldInflow => _heldInflow;

        /// <summary>A rotor's ring this step, from AdvanceRing: its η, μ and v_h (m/s) at the step's start.</summary>
        public double RingRate(int corner) => _ringRate[corner];
        public double RingInPlane(int corner) => _ringInPlane[corner];
        public double RingInduced(int corner) => _ringInduced[corner];

        /// <summary>A rotor's thrust at the last sub-step without its ring's fluctuation, N (the ring's v_h is of it).</summary>
        public double MeanThrust(int corner) => _meanThrust[corner];

        /// <summary>Whether a prop carries an unbalance (MotorAsymmetry) or a rotor's blades are off the sheet (DamagedRotor), so
        /// the rotors load their hubs once a revolution.</summary>
        public bool Unbalanced => _unbalanced || _damaged != 0 || _bent != 0;

        /// <summary>A rotor's blades (DamagedRotor), or null without PropulsionSpec.Rotor.</summary>
        public DamagedRotor Rotor(int corner) => _rotors?[corner];

        /// <summary>The rotors whose blades are off the sheet, bit 1 &lt;&lt; corner.</summary>
        public int Damaged => _damaged;

        /// <summary>A rotor's moment of inertia about its shaft now, kg·m²: the drawn J less the material its blades lost.</summary>
        public double Inertia(int corner) => _inertia[corner];

        /// <summary>
        /// A rotor's blades as they now are into its tables, its J and its 1/rev loads (a damaging step, a fixture, the flight's
        /// start): from the next BeginStep on. A rotor back to sound blades without pitch offsets takes the published table
        /// again, bit for bit.
        /// </summary>
        public void Rebuild(int corner)
        {
            if ((_lost & (1 << corner)) != 0) return; // a lost prop has no blades to rebuild
            DamagedRotor r = _rotors[corner];
            r.Rebuild();
            if (r.Sound)
            {
                _damaged &= ~(1 << corner);
                _inertia[corner] = _soundInertia[corner];
                _momentP[corner] = _momentQ[corner] = _forceP[corner] = _forceQ[corner] = 0;
                return;
            }
            _damaged |= 1 << corner;
            _inertia[corner] = _soundInertia[corner] - r.LostInertia;
        }

        /// <summary>
        /// A rotor's once-per-revolution loads at its angle (rad) and speed (rad/s) with this step's thrust and torque (after
        /// BeginStep, then each sub-step), body axes: the props' pull on the hub, its factory unbalance (MotorAsymmetry) and
        /// its blades' lost material, N; its blades' unequal drag, an in-plane force at the hub, N; and the pure couples, their
        /// unequal lift's hub moment and the flap sets' centrifugal moment, N·m. Each sub-step adds them (SubStep).
        /// </summary>
        public void OneRevLoads(int corner, double angle, double speed, out Double3 pull, out Double3 drag, out Double3 couple)
        {
            pull = _unbalanced ? FactoryPull(corner, angle, speed) : default;
            drag = couple = default;
            if ((_bent & (1 << corner)) != 0)
            {
                ShaftLoads(corner, angle, speed, _thrust[corner], _torque[corner], out Double3 bentPull, out Double3 tilt, out Double3 bentCouple);
                pull = pull + bentPull;
                drag = tilt;
                couple = bentCouple;
            }
            if ((_damaged & (1 << corner)) == 0) return;
            BladeLoads(corner, angle, speed, out Double3 lost, out Double3 bladeDrag, out Double3 bladeCouple);
            pull = pull + lost;
            drag = drag + bladeDrag;
            couple = couple + bladeCouple;
        }

        /// <summary>A rotor's hub, body m.</summary>
        public Double3 Hub(int corner) => _hub[corner];

        /// <summary>A motor's pole pairs, for its electrical frequency.</summary>
        public double PolePairs(int corner) => _polePairs[corner];

        /// <summary>The axial inflow through each disc this step, m/s, positive down through it (as in a climb).</summary>
        public ref readonly Corners Inflow => ref _inflow;

        /// <summary>Each hub's velocity through the air in its disc's plane this step, body m/s (y is 0).</summary>
        public Double3 InPlane(int corner) => _inPlane[corner];

        /// <summary>A rotor's in-plane drag at a speed (rad/s) with this step's in-plane airspeed, body N.</summary>
        public Double3 HForce(int corner, double speed) => _inPlane[corner] * (-_hPerSpeed * speed);

        /// <summary>The rotors' flapping (RotorFlappingSpec, p7b-airframe 1.3c): null for none. FlightCore sets its own copy.</summary>
        public RotorFlappingSpec Flapping { get; set; }

        /// <summary>
        /// A rotor's hub moment from its edgewise flow at a speed (rad/s) and thrust (N) with this step's inflow, body N·m
        /// (RotorFlappingSpec): its blades' flapwise spring moments, (N_b/2)·(s·M₁s·X − M₁c·Y), X the in-plane airflow's
        /// direction, Y = +y × X, s +1 clockwise. A pure couple; 0 without flapping, a stopped rotor or no in-plane airspeed.
        /// </summary>
        public Double3 HubMoment(int corner, double speed, double thrust)
        {
            RotorFlappingSpec f = Flapping;
            Double3 p = _inPlane[corner];
            double v = DetMath.Sqrt(p.X * p.X + p.Z * p.Z);
            if (f == null || !(speed > 0) || !(v > 0)) return default;
            f.Solve(speed, v, _inflow[corner], thrust, out double m1c, out double m1s, out _, out _, out _);
            double xx = -p.X / v, xz = -p.Z / v, half = 0.5 * f.Blades, roll = -_reaction[corner] * m1s;
            return new Double3(half * (roll * xx - m1c * xz), 0, half * (roll * xz + m1c * xx));
        }

        /// <summary>A rotor's ESC current limit, A.</summary>
        public double CurrentLimit(int corner) => _limit[corner];

        /// <summary>A rotor's spin angular momentum about body +y at a speed (rad/s), N·m·s: +J·ω clockwise (a positive
        /// rotation about +y, DetMath.cs), −J·ω counter-clockwise.</summary>
        public double SpinMomentum(int corner, double speed) => -_reaction[corner] * _inertia[corner] * speed;

        /// <summary>The cells in series of the first pack (both are alike): the OSD's low-voltage signal is per cell.</summary>
        public double SeriesCells => _series[0];

        /// <summary>A pack's open-circuit voltage at a state of charge, V: n_s × the cell's OCV table, held at full; past
        /// empty (the cell's cut-off, charge 0) it keeps falling at the table's last slope to 0 V (assumed: an
        /// over-discharged cell collapses, it does not hold its cut-off voltage; p7b-airframe 1.5).</summary>
        public double OpenCircuitVolts(int pack, double charge)
        {
            double[] ocv = _ocv[pack];
            double x = charge * (ocv.Length - 1);
            if (!(x > 0)) return _series[pack] * DetMath.Max(0, ocv[0] + x * (ocv[1] - ocv[0]));
            if (x >= ocv.Length - 1) return _series[pack] * ocv[ocv.Length - 1];
            int k = (int)x;
            return _series[pack] * (ocv[k] + (x - k) * (ocv[k + 1] - ocv[k]));
        }

        /// <summary>The step's inflow in still air (BeginStep with no wind).</summary>
        public void BeginStep(in FlightState s) => BeginStep(s, null);

        /// <summary>
        /// The step's inflow (RotorInflow) from the state and the wind at each hub (world m/s, in Corners order; null for
        /// still air), and the rotors' loads at their speeds.
        /// </summary>
        public void BeginStep(in FlightState s, Double3[] windAtHubs)
        {
            RotorInflow(s, windAtHubs);
            for (int i = 0; i < 4; i++)
            {
                RotorAero(i, s.RotorSpeed[i], out double t, out double q);
                _thrust[i] = t;
                _torque[i] = q;
            }
        }

        /// <summary>
        /// The per-rotor inflow seam (p7b-airframe design 2, shared with p7c-wind): the one place each rotor's air through
        /// its disc is made, for the step, into Inflow (axial, m/s, positive down through the disc as in a climb) and
        /// InPlane (body m/s). Its input from outside is the wind at each hub, world m/s in Corners order (null for still
        /// air): p7c-wind's field sampled at the hub. Today the air is each hub's velocity through it in body axes, the
        /// body origin's velocity plus ω × r less that wind, split along the disc's normal (body +y) and its plane.
        /// The rotors' own terms go here and nowhere else: the own wash and the vortex ring (p7b 1.2), the neighbours'
        /// wash and recirculation, and the ground and ceiling (1.3), each from the state and the rotors' own loads; each
        /// hub's elastic velocity along the axis when the airframe flexes (1.4, SetHubElastic); and, after every rotor's own
        /// air, the other rotors' wakes at its disc (1.3d, Interference).
        /// The vortex ring (1.2): each rotor's ring adds its inflow through the disc (FlightState.RingInflow, m/s) to the
        /// thrust's inflow and its fluctuation (RingThrust) to the thrust, as the state holds them at the step's start;
        /// AdvanceRing moves them after the step's loops. The torque keeps the freestream's inflow: Washizu et al. and
        /// Azuma and Obata measured little torque change in the ring (under 2.5 % against 15 % of thrust; Johnson 2005).
        /// </summary>
        public void RotorInflow(in FlightState s, Double3[] windAtHubs)
        {
            Double3 v = s.Attitude.InverseRotate(s.Velocity);
            Double3 w = s.BodyRate;
            for (int i = 0; i < 4; i++)
            {
                Double3 air = v + Double3.Cross(w, _hub[i]);
                if (windAtHubs != null) air = air - s.Attitude.InverseRotate(windAtHubs[i]);
                if (_elastic) air.Y += _hubElastic[i];
                _inflow[i] = air.Y;
                _inPlane[i] = new Double3(air.X, 0, air.Z);
                if (_ring != null)
                {
                    _ringInflow[i] = s.RingInflow[i];
                    _ringGain[i] = 1 + s.RingThrust[i];
                }
                if (_ground == null && _wake == null) continue;
                double t = _meanThrust[i], vh = t > 0 ? DetMath.Sqrt(t / _twoRhoArea) : 0;
                _washInduced[i] = vh;
                // The rotor's own wake (D-064), in v_h.
                if (_wake != null) _wakeInflow[i] = s.WakeInflow[i] * vh;
                if (_ground == null) continue;
                // The ground and the ceiling (1.3), at v_h of the rotor's thrust at the last sub-step, faded by the hub's
                // airspeed in the disc's plane (Cheeseman and Bennett's forward form); the re-ingested wash in v_h.
                double vh2 = vh * vh, v2 = air.X * air.X + air.Z * air.Z;
                double fade = vh2 + v2 > 0 ? vh2 / (vh2 + v2) : 1;
                _surfaceGain[i] = _ground.GroundGain(_groundAt[i] / _radius, fade) * _ground.CeilingGain(_ceilingAt[i] / _radius, fade);
                _washInflow[i] = s.WashInflow[i] * vh;
            }
            if (_interference != null) Interference(s);
        }

        // The rotors in each other's wake (RotorInterferenceSpec, p7b-airframe 1.3d): each source's induced velocity at its thrust at the
        // last sub-step (Glauert's), the share of its wake the flow carries away (Formed), and its wake's skew and downstream
        // direction from its own inflow so far; each target's added
        // inflow Σ k·v_i over the others, scaled by its fluctuation (1 + FlightState.InterferenceShare), into its axial inflow as the
        // wind's axial part enters it, so the thrust, the torque, the flapping and the ring all take it.
        void Interference(in FlightState s)
        {
            for (int i = 0; i < 4; i++)
            {
                Double3 p = _inPlane[i];
                double v = DetMath.Sqrt(p.X * p.X + p.Z * p.Z), t = _meanThrust[i], vi = _interference.InducedVelocity(v, _inflow[i], t);
                double through = _inflow[i] + vi;
                _interferenceInduced[i] = vi * _interference.Formed(t, DetMath.Sqrt(v * v + through * through));
                _interferenceSpeed[i] = v;
                _interferenceSkew[i] = RotorInterferenceSpec.HalfSkew(v, through);
                _interferenceDx[i] = v > 0 ? -p.X / v : 0;
                _interferenceDz[i] = v > 0 ? -p.Z / v : 0;
            }
            for (int j = 0; j < 4; j++)
            {
                double sum = 0;
                for (int i = 0; i < 4; i++)
                    if (i != j && _interferenceInduced[i] > 0)
                        sum += _interferenceTable.Factor(i, j, _interferenceSkew[i], _interferenceDx[i], _interferenceDz[i]) * _interferenceInduced[i];
                double added = sum * (1 + s.InterferenceShare[j]);
                _interferenceInflow[j] = added;
                _inflow[j] += added;
            }
        }

        /// <summary>
        /// The wakes' fluctuation over one step of dt seconds (RotorInterferenceSpec): each rotor's share x, a first-order process,
        /// x ← a·x + √(1 − a²)·σ·n, a = τ/(τ + dt), τ = FluctuationScale·R/V_∥ at the rotor's in-plane airspeed, n from
        /// "rotor.interference" at the step and the corner, bounded. Held while the rotor has no in-plane airspeed.
        /// </summary>
        public void AdvanceInterference(ref FlightState s, double dt)
        {
            double sigma = _interference.FluctuationSigma, bound = _interference.FluctuationBound;
            for (int i = 0; i < 4; i++)
            {
                double v = _interferenceSpeed[i], x = s.InterferenceShare[i];
                if (!(v > 0)) continue;
                double tau = _interference.FluctuationScale * _interference.Radius / v, a = tau / (tau + dt);
                x *= a;
                if (sigma > 0) x += DetMath.Sqrt(1 - a * a) * sigma * VortexRingSpec.Normal(_interferenceStream, s.Step, i);
                s.InterferenceShare[i] = x > bound ? bound : x < -bound ? -bound : x;
            }
        }

        /// <summary>
        /// The rotors' re-ingested wash over one step of dt seconds (GroundEffectSpec): each rotor's share of v_h, x, a
        /// first-order process, x ← a·x + √(1 − a²)·σ·n, a = τ/(τ + dt), τ = RecirculationLoop·(z + R)/v_h with z the rotor's
        /// height held between LowestHeight and RecirculationOnset, σ its height's, n from "ground.recirc" at the step and
        /// the corner, bounded. A stopped rotor has none.
        /// </summary>
        public void AdvanceWash(ref FlightState s, double dt)
        {
            for (int i = 0; i < 4; i++)
            {
                double vh = _washInduced[i], x = s.WashInflow[i];
                if (!(vh > 0))
                {
                    s.WashInflow[i] = 0;
                    continue;
                }
                double z = _groundAt[i] / _radius;
                double sigma = _ground.Sigma(z);
                if (!(sigma > 0) && x == 0) continue;
                z = DetMath.Min(DetMath.Max(z, _ground.LowestHeight), _ground.RecirculationOnset);
                double tau = _ground.RecirculationLoop * (z + 1) * _radius / vh, a = tau / (tau + dt);
                x *= a;
                if (sigma > 0) x += DetMath.Sqrt(1 - a * a) * (sigma * _shareToInflow) * VortexRingSpec.Normal(_groundStream, s.Step, i);
                double bound = _ground.RecirculationBound * _shareToInflow;
                s.WashInflow[i] = x > bound ? bound : x < -bound ? -bound : x;
            }
        }

        /// <summary>
        /// The rotors' own wakes over one step of dt seconds (RotorWakeSpec, D-064): each rotor's wake inflow over v_h, x, a
        /// first-order process, x ← a·x + √(1 − a²)·σ·n, a = τ/(τ + dt), τ = R/(2π·CornerFrequency·v_h), σ the hover's (the
        /// thrust's σ through the static slope) faded by Glauert's v_i/v_h at the rotor's in-plane μ, n from
        /// "rotor.wake" at the step and the corner, bounded at Bound times the hover's σ. A stopped rotor has none.
        /// </summary>
        public void AdvanceWake(ref FlightState s, double dt)
        {
            for (int i = 0; i < 4; i++)
            {
                double vh = _washInduced[i];
                if (!(vh > 0))
                {
                    s.WakeInflow[i] = 0;
                    continue;
                }
                Double3 p = _inPlane[i];
                double sigma = _wakeSigma * RotorWakeSpec.Fade(DetMath.Sqrt(p.X * p.X + p.Z * p.Z) / vh);
                double tau = _radius / (TwoPi * _wake.CornerFrequency * vh), a = tau / (tau + dt);
                double x = a * s.WakeInflow[i] + DetMath.Sqrt(1 - a * a) * sigma * VortexRingSpec.Normal(_wakeStream, s.Step, i);
                double bound = _wake.Bound * _wakeSigma;
                s.WakeInflow[i] = x > bound ? bound : x < -bound ? -bound : x;
            }
        }

        /// <summary>
        /// The rotors' vortex rings over one step of dt seconds (VortexRingSpec), from the step's inflow and each rotor's thrust
        /// at its last sub-step without the fluctuation: η and μ against that thrust's v_h; in or out of the ring by the
        /// hysteresis; the ring's added inflow lagged toward its value there, dδ/dt = (δ* − δ)/τ, τ = InflowLag·R/v_h, by
        /// backward Euler; the fluctuation a first-order process with the corner f = FluctuationFrequency·v_h/R,
        /// x ← a·x + √(1 − a²)·σ·n, a = τ_x/(τ_x + dt), n from "vrs.thrust" at the step and the corner, bounded. A stopped
        /// rotor has no ring.
        /// </summary>
        public void AdvanceRing(ref FlightState s, double dt)
        {
            int latched = s.RingLatched;
            for (int i = 0; i < 4; i++)
            {
                bool was = (latched & (1 << i)) != 0;
                double t = _meanThrust[i];
                _ringRate[i] = _ringInPlane[i] = _ringInduced[i] = 0;
                if (!(t > 0))
                {
                    s.RingInflow[i] = 0;
                    s.RingThrust[i] = 0;
                    latched &= ~(1 << i);
                    continue;
                }
                double vh = _ring.FixedInducedVelocity > 0 ? _ring.FixedInducedVelocity : DetMath.Sqrt(t / _twoRhoArea);
                Double3 p = _inPlane[i];
                double eta = -_inflow[i] / vh, mu = DetMath.Sqrt(p.X * p.X + p.Z * p.Z) / vh;
                bool now = _ring.InRing(was, eta, mu);
                double target = now ? _ring.AddedInflow(eta, mu, _heldInflow) * vh : 0;
                double tau = _ring.InflowLag * _radius / vh;
                double d = s.RingInflow[i];
                s.RingInflow[i] = d + (target - d) * dt / (tau + dt);
                double sigma = now ? _ring.Sigma(eta, mu) : 0, x = s.RingThrust[i];
                if (sigma > 0 || x != 0)
                {
                    double tx = _radius / (TwoPi * _ring.FluctuationFrequency * vh), a = tx / (tx + dt);
                    x *= a;
                    if (sigma > 0) x += DetMath.Sqrt(1 - a * a) * sigma * VortexRingSpec.Normal(_ringStream, s.Step, i);
                    double bound = _ring.FluctuationBound;
                    x = x > bound ? bound : x < -bound ? -bound : x;
                    s.RingThrust[i] = x;
                }
                _ringRate[i] = eta;
                _ringInPlane[i] = mu;
                _ringInduced[i] = vh;
                latched = now ? latched | (1 << i) : latched & ~(1 << i);
            }
            s.RingLatched = (byte)latched;
        }

        // An ESC's disarm rules (the constants above): the run loop's minimum mechanical speed (rad/s), the stop counter's
        // exit and brake on stop's FETs (s after the disarm), +∞ where its firmware has none.
        static void EscRules(EscDisarm rule, double polePairs, out double exitSpeed, out double countExit, out double brakeAt)
        {
            bool blheli = rule == EscDisarm.BlheliS || rule == EscDisarm.BlheliSBrakeOnStop;
            bool brake = rule == EscDisarm.BlheliSBrakeOnStop || rule == EscDisarm.BluejayBrakeOnStop;
            double tick = blheli ? BlheliSTick : BluejayTick;
            exitSpeed = rule == EscDisarm.Freewheel ? double.PositiveInfinity
                : (blheli ? BlheliSExitErpm : BluejayExitErpm) / polePairs * (TwoPi / 60);
            countExit = brake ? (BrakeStopTicks - TickPhase) * tick
                : rule == EscDisarm.BlheliS ? (BlheliSStopTicks - TickPhase) * tick : double.PositiveInfinity;
            brakeAt = !brake ? double.PositiveInfinity : blheli ? countExit + BlheliSBrakeWait : countExit;
        }

        /// <summary>
        /// One sub-step of h seconds with the controller's duty commands (0–1); driving is false while disarmed. The
        /// state's ESC commands, rotor speeds and charges advance; loads gathers the thrust, the reaction and the
        /// currents.
        /// </summary>
        public void SubStep(ref FlightState s, in Corners duty, bool driving, double h, ref RotorLoads loads)
        {
            // The ESCs' command filters.
            for (int i = 0; i < 4; i++)
            {
                double d = s.EscCommand[i];
                s.EscCommand[i] = d + ((driving ? duty[i] : 0) - d) * h / (_escTau[i] + h);
            }

            // The ESCs' modes (EscDisarm): armed, each drives; disarmed, each stays in its run loop until its firmware
            // leaves it (below its minimum speed, or past its stop counter), then lets its motor coast, or with brake on
            // stop turns on its low-side FETs.
            if (driving)
            {
                _stopLoops = 0;
                _escOff = 0;
                _escBrake = 0;
            }
            else
            {
                if (_stopLoops < int.MaxValue) _stopLoops++;
                double since = _stopLoops * h;
                for (int i = 0; i < 4; i++)
                {
                    int bit = 1 << i;
                    if ((_escOff & bit) != 0 || (s.RotorSpeed[i] >= _runExitSpeed[i] && since < _stopCountExit[i])) continue;
                    _escOff |= bit;
                    if (_brakeAt[i] < double.PositiveInfinity) _escBrake |= bit;
                }
            }
            int running = (driving ? 0xF : ~_escOff & 0xF) & ~_escStopped;

            // The bus: the packs' Thevenin source, loaded by the free ESCs, I = Σ d·(d·V − Ke·ω)/R, and by the limited
            // ones, Σ (I_lim·R + Ke·ω)·I_lim / V. An ESC is limited when its motor's current at the bus's voltage would
            // pass its limit; limiting one raises the voltage, so the set only grows and four rounds settle it. A disarmed
            // ESC still in its run loop loads the bus at its decaying duty and is never limited (its current brakes).
            double voc0 = OpenCircuitVolts(0, s.PackCharge0), voc1 = OpenCircuitVolts(1, s.PackCharge1);
            double g = _conductance[0] + _conductance[1];
            double vth = (_conductance[0] * voc0 + _conductance[1] * voc1) / g, rth = 1 / g;
            double volts = Bus(vth, rth, 0, 0, 0);
            int limited = 0;
            if (running != 0)
            {
                for (int round = 0; round < 5; round++)
                {
                    double load = 0, emf = 0, power = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if ((running & (1 << i)) == 0) continue;
                        double d = s.EscCommand[i];
                        if ((limited & (1 << i)) != 0)
                        {
                            power += (_limitVolts[i] + _ke[i] * s.RotorSpeed[i]) * _limit[i];
                            continue;
                        }
                        load += d * d * _invR[i];
                        emf += d * _ke[i] * s.RotorSpeed[i] * _invR[i];
                    }
                    volts = Bus(vth, rth, load, emf, power);
                    int more = 0;
                    for (int i = 0; driving && i < 4; i++)
                        if ((limited & (1 << i)) == 0 && (_escStopped & (1 << i)) == 0 && (s.EscCommand[i] * volts - _ke[i] * s.RotorSpeed[i]) * _invR[i] > _limit[i])
                            more |= 1 << i;
                    if (more == 0) break;
                    limited |= more;
                }
            }

            int bound = 0;
            for (int i = 0; i < 4; i++)
            {
                double w = s.RotorSpeed[i], d = s.EscCommand[i], ke = _ke[i];
                int bit = 1 << i;
                bool run = (running & bit) != 0, held = (limited & bit) != 0;
                bool brake = !run && (_escBrake & bit) != 0 && _stopLoops * h >= _brakeAt[i];
                double current = held ? _limit[i] : run ? (d * volts - ke * w) * _invR[i] : brake ? -ThreePhaseShort * ke * w * _invR[i] : 0;
                double drive = held ? (_limitVolts[i] + ke * w) / volts : run ? d : 0;
                double q = _torque[i], added = _drag[i];
                double loss = w > 0 ? _friction[i] + added : 0;
                double rate = (ke * current - loss - q) / _inertia[i];
                // A free motor's current falls as it speeds up (Ke²/R), a shorted one's too; a limited one's is held, so only
                // the prop damps.
                double slope = -((run && !held ? ke * ke * _invR[i] : brake ? ThreePhaseShort * ke * ke * _invR[i] : 0) + (w > 0 ? 2 * q / w : 0)) / _inertia[i];
                double next, back = 0;
                double ks = _shaftStiffness[i], cs = _shaftDamping[i];
                // A driven rotor its added drag alone holds at rest: bound (design 8, D-073).
                bool binds = added > 0 && run && d > 0 && ke * current - q <= added;
                if (ks == 0 && cs == 0 && _shaftTorque[i] == 0)
                {
                    if (w > 0 || added == 0) next = w + h * rate / (1 - h * slope);
                    else
                    {
                        // At rest with an added drag (SetMotor): held while the drive is within it and Kt·I0, then turned by
                        // the drive past them.
                        double net = ke * current - q, hold = _friction[i] + added;
                        if (net <= hold)
                        {
                            next = 0;
                            if (binds)
                            {
                                bound |= bit;
                                _heldDrive[i] = ke * current;
                            }
                        }
                        else next = h * ((net - hold) / _inertia[i]) / (1 - h * slope);
                    }
                }
                else
                {
                    // A resting prop's blades (SetShaftLoad): their moment at the rotor's angle now, and the motor's drive.
                    double turned = s.RotorAngle[i] - _shaftReference[i];
                    while (turned > Pi) turned -= TwoPi;
                    while (turned <= -Pi) turned += TwoPi;
                    double shaft = _shaftTorque[i] - ks * turned, hold = _friction[i] + added, net = ke * current - q + shaft;
                    double u = w > 0 ? w : -_back[i]; // the rotor's turn rate, signed (backward only at rest)
                    if (u == 0 && net <= hold && net >= -hold)
                    {
                        next = 0; // held at rest by the motor's own drag (and its added drag, SetMotor)
                        if (binds)
                        {
                            bound |= bit;
                            _heldDrive[i] = ke * current;
                        }
                    }
                    else
                    {
                        double drag = u > 0 || u == 0 && net > 0 ? hold : -hold;
                        double turn = (net - drag) / (ks + cs / h) / h; // the massless balance's rate over the sub-step
                        double free = u + h * (net - cs * u - drag) / _inertia[i] / (1 - h * slope);
                        double un = turn >= 0 ? DetMath.Min(turn, free) : DetMath.Max(turn, free); // no faster than its J allows
                        if (u > 0 && un < 0 || u < 0 && un > 0) un = 0; // through rest, at rest for the sub-step
                        if (un < -BackSpeed) un = -BackSpeed;
                        next = un > 0 ? un : 0;
                        back = un < 0 ? un * h : 0;
                    }
                    _back[i] = back < 0 ? -back / h : 0;
                }
                if (next < 0) next = 0;
                RotorAero(i, next, out double thrust, out double torque);
                loads.Thrust[i] += thrust;
                loads.YawMoment += _reaction[i] * (_inertia[i] * (next - w) / h + torque);
                loads.SupplyAmps[i] += drive * current;
                loads.Speed[i] += next;
                Turn(ref s, i, w, next, h, thrust, torque, ref loads);
                if (back != 0) s.RotorAngle[i] = Wrapped(s.RotorAngle[i] + back);
                if (held)
                {
                    loads.Limited |= 1 << i;
                    loads.LimitTaken[i] = DetMath.Max(loads.LimitTaken[i], d > 0 ? (d - drive) / d : 0);
                }
                s.RotorSpeed[i] = next;
                _thrust[i] = thrust;
                _torque[i] = torque;
            }

            _bound = bound;
            double amps0 = (voc0 - volts) * _conductance[0], amps1 = (voc1 - volts) * _conductance[1];
            s.PackCharge0 -= amps0 * h / _capacity[0];
            s.PackCharge1 -= amps1 * h / _capacity[1];
            loads.Volts = volts;
            BusVolts = volts;
            loads.Amps0 = amps0;
            loads.Amps1 = amps1;
            loads.SubSteps++;
        }

        static double Wrapped(double a)
        {
            while (a >= TwoPi) a -= TwoPi;
            while (a < 0) a += TwoPi;
            return a;
        }

        // A rotor turns by the sub-step's mean speed, its angle kept in [0, 2π); an unbalanced prop's residual U, at its
        // angle on the rotor, pulls the hub outward with U·ω² in the disc's plane. A clockwise rotor turns positively
        // about +y (DetMath.cs), so the unbalance at angle θ from body +x points along (cos θ, 0, −sin θ). A rotor whose
        // blades are off the sheet adds its blades' loads (BladeLoads) at the same angle and speed.
        void Turn(ref FlightState s, int i, double w, double next, double h, double thrust, double torque, ref RotorLoads loads)
        {
            double angle = s.RotorAngle[i] + (w + next) * 0.5 * h;
            while (angle >= TwoPi) angle -= TwoPi;
            s.RotorAngle[i] = angle;
            if (_arms != null)
            {
                // Through the arm in its plane (ArmFlex, model 72): the rotor's in-plane force and its couples, summed.
                Double3 force = _unbalanced ? FactoryPull(i, angle, next) : default, moment = default;
                if ((_bent & (1 << i)) != 0)
                {
                    ShaftLoads(i, angle, next, thrust, torque, out Double3 pull, out Double3 tilt, out Double3 bentCouple);
                    force = force + pull + tilt;
                    moment = moment + bentCouple;
                }
                if ((_damaged & (1 << i)) != 0)
                {
                    BladeLoads(i, angle, next, out Double3 lostPull, out Double3 bladeDrag, out Double3 bladeCouple);
                    force = force + lostPull + bladeDrag;
                    moment = moment + bladeCouple;
                }
                _arms.Carry(ref s, i, force, moment, thrust, SpinMomentum(i, next), h, ref loads);
                return;
            }
            if (_unbalanced)
            {
                Double3 f = FactoryPull(i, angle, next);
                loads.UnbalanceForce = loads.UnbalanceForce + f;
                loads.UnbalanceMoment = loads.UnbalanceMoment + Double3.Cross(_hub[i], f);
            }
            if ((_bent & (1 << i)) != 0)
            {
                ShaftLoads(i, angle, next, thrust, torque, out Double3 pull, out Double3 tilt, out Double3 bentCouple);
                Double3 f = pull + tilt;
                loads.UnbalanceForce = loads.UnbalanceForce + f;
                loads.UnbalanceMoment = loads.UnbalanceMoment + Double3.Cross(_hub[i], f) + bentCouple;
            }
            if ((_damaged & (1 << i)) == 0) return;
            BladeLoads(i, angle, next, out Double3 lost, out Double3 drag, out Double3 couple);
            Double3 g = lost + drag;
            loads.UnbalanceForce = loads.UnbalanceForce + g;
            loads.UnbalanceMoment = loads.UnbalanceMoment + Double3.Cross(_hub[i], g) + couple;
        }

        // The factory unbalance's pull at the rotor's angle and speed, body N.
        Double3 FactoryPull(int i, double angle, double speed)
        {
            double theta = _unbalanceAngle[i] - _reaction[i] * angle;
            DetMath.SinCos(theta, out double sin, out double cos);
            double pull = _unbalance[i] * speed * speed;
            return new Double3(pull * cos, 0, -pull * sin);
        }

        // A damaged rotor's loads at its angle ψ and speed ω, body axes (DamagedRotor): its lost material's pull ω²·U, its
        // blades' drag at the hub and its couples (their lift's hub moment and the flap sets' ω²·C). The rotor's frame (p along
        // blade 0, q along blade 0 × +y) turns with ψ: blade 0 lies along (cos ψ, 0, s·sin ψ), s the reaction sign, so a
        // vector (p, q) is (p·cos ψ − s·q·sin ψ, 0, s·p·sin ψ + q·cos ψ) in the body.
        void BladeLoads(int i, double angle, double speed, out Double3 lost, out Double3 drag, out Double3 couple)
        {
            DamagedRotor r = _rotors[i];
            DetMath.SinCos(angle, out double sin, out double cos);
            double s = _reaction[i], w2 = speed * speed;
            lost = InBody(r.UnbalanceP * w2, r.UnbalanceQ * w2, cos, s * sin);
            drag = InBody(_forceP[i], _forceQ[i], cos, s * sin);
            couple = InBody(_momentP[i] + r.CoupleP * w2, _momentQ[i] + r.CoupleQ * w2, cos, s * sin);
        }

        // A bent shaft's loads at the rotor's angle ψ and speed ω with its thrust T and torque Q, body axes (SetShaft): the masses'
        // pull ω²·U at the hub; the thrust's tilt T·u at the hub; the couples, ω²·K, T·lever·(u × ŷ) (u × ŷ = (−u_q, u_p)) and the
        // drag torque's tilt reaction·Q·u.
        void ShaftLoads(int i, double angle, double speed, double thrust, double torque, out Double3 pull, out Double3 tilt, out Double3 couple)
        {
            DetMath.SinCos(angle, out double sin, out double cos);
            double s = _reaction[i], w2 = speed * speed, up = _shaftTp[i], uq = _shaftTq[i], tl = thrust * _shaftLever[i], rq = s * torque;
            pull = InBody(_shaftUp[i] * w2, _shaftUq[i] * w2, cos, s * sin);
            tilt = InBody(thrust * up, thrust * uq, cos, s * sin);
            couple = InBody(_shaftKp[i] * w2 - tl * uq + rq * up, _shaftKq[i] * w2 + tl * up + rq * uq, cos, s * sin);
        }

        static Double3 InBody(double p, double q, double cos, double sin) => new Double3(p * cos - q * sin, 0, p * sin + q * cos);

        // The bus voltage: V = vth − rth·(load·V − emf + power/V), the larger root of
        // (1 + rth·load)·V² − (vth + rth·emf)·V + rth·power = 0; with no limited ESC, the linear solve.
        static double Bus(double vth, double rth, double load, double emf, double power)
        {
            double a = 1 + rth * load, b = vth + rth * emf;
            if (power == 0) return b / a;
            double disc = b * b - 4 * a * (rth * power);
            return (b + DetMath.Sqrt(DetMath.Max(disc, 0))) / (2 * a);
        }

        /// <summary>A rotor's thrust, N, and drag torque, N·m, at speed w (rad/s) with axial inflow va (m/s).</summary>
        public void Aero(double w, double va, out double thrust, out double torque)
        {
            if (!(w > 0))
            {
                thrust = 0;
                torque = 0;
                return;
            }
            double rpm = w * (60 / TwoPi);
            double advance = va / (w * (_diameter / TwoPi));
            double w2 = w * w;
            thrust = _kT * Table(_rpm, _rpmThrust, rpm) * InflowRatio(_jThrust, advance) * w2;
            torque = _kQ * Table(_rpm, _rpmTorque, rpm) * InflowRatio(_jTorque, advance) * w2;
        }

        // A rotor's thrust and torque at speed w with this step's inflow (Aero), and with its vortex ring when the rotors
        // have one: the ring's added inflow moves the thrust's advance ratio past the held descent branch, its fluctuation
        // scales the thrust; _meanThrust keeps the thrust without the fluctuation (in the ring the torque keeps the
        // freestream's, as measured). With the ground and the ceiling, the re-ingested wash's inflow joins the ring's and the
        // surfaces' gain scales the thrust; the rotor's own wake (D-064) joins the wash. The wash and the wake move the
        // torque's advance ratio too: the blades see that inflow, so the props' published torque against J answers it.
        void RotorAero(int i, double w, out double thrust, out double torque)
        {
            if ((_lost & (1 << i)) != 0)
            {
                // A lost prop (LoseProp): the bell turns in the air with nothing to load it but its motor's own drag.
                thrust = torque = 0;
                _meanThrust[i] = 0;
                return;
            }
            if ((_ring == null && _ground == null && _wake == null) || !(w > 0))
            {
                Aero(w, _inflow[i], out thrust, out torque);
                Immerse(i, ref thrust, ref torque);
                if ((_damaged & (1 << i)) != 0)
                {
                    if (w > 0)
                    {
                        double advance0 = _inflow[i] / (w * (_diameter / TwoPi));
                        Blades(i, advance0, advance0, ref thrust, ref torque);
                    }
                    else _momentP[i] = _momentQ[i] = _forceP[i] = _forceQ[i] = 0;
                }
                _meanThrust[i] = thrust;
                return;
            }
            double rpm = w * (60 / TwoPi);
            double advance = _inflow[i] / (w * (_diameter / TwoPi));
            double w2 = w * w;
            double own = (_ground == null ? 0 : _washInflow[i]) + (_wake == null ? 0 : _wakeInflow[i]);
            double held = advance >= 0 ? advance : DetMath.Max(advance, DescentAdvanceRatioLimit);
            double j = held + (_ringInflow[i] + own) * _ringAdvance / w;
            double mean = _kT * Table(_rpm, _rpmThrust, rpm) * InflowRatio(_jThrust, j) * w2;
            if (_ground != null) mean *= _surfaceGain[i];
            double jq = own == 0 ? advance : held + own * _ringAdvance / w;
            torque = _kQ * Table(_rpm, _rpmTorque, rpm) * InflowRatio(_jTorque, jq) * w2;
            bool damaged = (_damaged & (1 << i)) != 0;
            if (damaged)
            {
                _rotors[i].Ratios(j, jq, out double etaT, out double etaQ);
                mean *= etaT;
                torque *= etaQ;
            }
            Immerse(i, ref mean, ref torque);
            _meanThrust[i] = mean;
            thrust = mean * _ringGain[i];
            if (damaged) _rotors[i].Loads(j, jq, thrust, torque, out _momentP[i], out _momentQ[i], out _forceP[i], out _forceQ[i]);
        }

        // A disc partly inside a carved face: only its part in the air makes thrust and air torque (SetImmersed).
        void Immerse(int i, ref double thrust, ref double torque)
        {
            if (_immersedThrust[i] != 0) thrust *= 1 - _immersedThrust[i];
            if (_immersedTorque[i] != 0) torque *= 1 - _immersedTorque[i];
        }

        // A damaged rotor's thrust and torque times its tables' η at their advance ratios (jt the thrust's, jq the torque's),
        // and its 1/rev aerodynamic loads at them (DamagedRotor.Loads), for BladeLoads.
        void Blades(int i, double jt, double jq, ref double thrust, ref double torque)
        {
            DamagedRotor r = _rotors[i];
            r.Ratios(jt, jq, out double etaT, out double etaQ);
            thrust *= etaT;
            torque *= etaQ;
            r.Loads(jt, jq, thrust, torque, out _momentP[i], out _momentQ[i], out _forceP[i], out _forceQ[i]);
        }

        // A ratio against the advance ratio: the table, its first segment continued below 0 to the descent limit.
        double InflowRatio(double[] ratio, double advance)
        {
            if (advance >= 0) return Table(_j, ratio, advance);
            double j = DetMath.Max(advance, DescentAdvanceRatioLimit);
            return ratio[0] + j * (ratio[1] - ratio[0]) / (_j[1] - _j[0]);
        }

        // Linear between the points, held at the ends.
        static double Table(double[] x, double[] y, double at)
        {
            int n = x.Length;
            if (!(at > x[0])) return y[0];
            if (at >= x[n - 1]) return y[n - 1];
            int k = 0;
            while (at >= x[k + 1]) k++;
            return y[k] + (at - x[k]) * (y[k + 1] - y[k]) / (x[k + 1] - x[k]);
        }
    }
}
