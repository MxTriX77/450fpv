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
    ///   when the prop is pulled down; disarmed, the ESC lets the motor freewheel (i = 0).
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
    ///   adds its inflow to the thrust's advance ratio; AdvanceWash moves the wash once a step.
    /// </summary>
    public sealed class Propulsion
    {
        /// <summary>The advance ratio a descending rotor's static slope runs to (assumed: a 1.9 m/s descent at hover
        /// speed, below the vortex ring's onset). It ends near η 0.19 (the static C_T's v_h is 0.263·n·D), by Johnson's VzD
        /// 0.2, where his VRS inflow leaves momentum theory; the vortex ring (VortexRingSpec) adds to it from there.</summary>
        public const double DescentAdvanceRatioLimit = -0.05;

        const double TwoPi = 6.283185307179586;
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

        // The vortex ring (null: none): its spec and stream, the rotor's radius, 2ρA, the held descent branch's inflow over
        // v_h, and the advance ratio a m/s of the ring's inflow makes at 1 rad/s.
        readonly VortexRingSpec _ring;
        readonly SeedStream _ringStream;
        readonly double _radius, _twoRhoArea, _heldInflow, _ringAdvance;
        readonly double[] _ringInflow = new double[4], _ringGain = new double[4], _meanThrust = new double[4];
        readonly double[] _ringRate = new double[4], _ringInPlane = new double[4], _ringInduced = new double[4];

        // The ground and the ceiling (null: neither): their spec and the wash's stream; each rotor's distances along its axis
        // to them (m, +∞ for none), its gain from them, and its v_h (m/s) and re-ingested inflow (m/s) at the step's start.
        readonly GroundEffectSpec _ground;
        readonly SeedStream _groundStream;
        Corners _groundAt, _ceilingAt;
        readonly double[] _surfaceGain = new double[4], _washInflow = new double[4], _washInduced = new double[4];
        readonly double[][] _ocv = new double[2][];

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
            : this(spec, draws, ring, ringStream, null, default) { }

        /// <summary>The same, with each rotor's ground and ceiling effect from ground and its re-ingested wash from
        /// groundStream (null for neither).</summary>
        public Propulsion(PropulsionSpec spec, MotorAsymmetry.Draws draws, VortexRingSpec ring, SeedStream ringStream,
            GroundEffectSpec ground, SeedStream groundStream)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Motors == null || spec.Motors.Length != 4) throw new ArgumentException("four motors", nameof(spec));
            if (spec.Packs == null || spec.Packs.Length != 2) throw new ArgumentException("two packs", nameof(spec));
            for (int i = 0; i < 4; i++)
            {
                DroneMotor m = spec.Motors[i];
                _ke[i] = draws == null ? 60 / (TwoPi * m.Kv) : 60 / (TwoPi * (m.Kv * draws.KvScale[i]));
                _invR[i] = 1 / m.Resistance;
                _friction[i] = _ke[i] * m.NoLoadCurrent;
                _inertia[i] = m.RotorInertia;
                _escTau[i] = draws == null ? m.EscTau : m.EscTau + draws.EscLag[i];
                _reaction[i] = m.Clockwise ? -1 : +1;
                _polePairs[i] = m.MagnetPoles / 2;
                _hub[i] = m.Position;
                _limit[i] = m.EscCurrentLimit;
                _limitVolts[i] = m.EscCurrentLimit * m.Resistance;
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
            if (ring == null && ground == null) return;
            _ring = ring?.Clone();
            _ringStream = ringStream;
            _ground = ground?.Clone();
            _groundStream = groundStream;
            double rho = ring != null ? ring.AirDensity : ground.AirDensity;
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
        }

        /// <summary>Whether the rotors have their vortex rings (VortexRingSpec).</summary>
        public bool HasRing => _ring != null;

        /// <summary>Whether the rotors feel the ground and the ceiling (GroundEffectSpec).</summary>
        public bool HasGround => _ground != null;

        /// <summary>A rotor's gain from the ground and the ceiling this step (1 without them).</summary>
        public double SurfaceGain(int corner) => _surfaceGain[corner];

        /// <summary>A rotor's thrust at its present speed with this step's inflow, N (after BeginStep, then each sub-step).</summary>
        public double Thrust(int corner) => _thrust[corner];

        /// <summary>
        /// Each rotor's distance along its axis to the surface below it (the ground or a shape) and above it, m, +∞ for none
        /// within the rays' reach (FlightCore's rays and SampleGround): RotorInflow's ground and ceiling from then on.
        /// </summary>
        public void SetSurfaces(in Corners ground, in Corners ceiling)
        {
            _groundAt = ground;
            _ceilingAt = ceiling;
        }

        /// <summary>The held descent branch's inflow through the disc over v_h, which the ring adds to (derived).</summary>
        public double HeldInflow => _heldInflow;

        /// <summary>A rotor's ring this step, from AdvanceRing: its η, μ and v_h (m/s) at the step's start.</summary>
        public double RingRate(int corner) => _ringRate[corner];
        public double RingInPlane(int corner) => _ringInPlane[corner];
        public double RingInduced(int corner) => _ringInduced[corner];

        /// <summary>A rotor's thrust at the last sub-step without its ring's fluctuation, N (the ring's v_h is of it).</summary>
        public double MeanThrust(int corner) => _meanThrust[corner];

        /// <summary>Whether a prop carries an unbalance (MotorAsymmetry), so the rotors pull their hubs.</summary>
        public bool Unbalanced => _unbalanced;

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
        /// wash and recirculation, and the ground and ceiling (1.3), each from the state and the rotors' own loads.
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
                _inflow[i] = air.Y;
                _inPlane[i] = new Double3(air.X, 0, air.Z);
                if (_ring != null)
                {
                    _ringInflow[i] = s.RingInflow[i];
                    _ringGain[i] = 1 + s.RingThrust[i];
                }
                if (_ground == null) continue;
                // The ground and the ceiling (1.3), at v_h of the rotor's thrust at the last sub-step, faded by the hub's
                // airspeed in the disc's plane (Cheeseman and Bennett's forward form); the re-ingested wash in v_h.
                double t = _meanThrust[i], vh = t > 0 ? DetMath.Sqrt(t / _twoRhoArea) : 0;
                double vh2 = vh * vh, v2 = air.X * air.X + air.Z * air.Z;
                double fade = vh2 + v2 > 0 ? vh2 / (vh2 + v2) : 1;
                _surfaceGain[i] = _ground.GroundGain(_groundAt[i] / _radius, fade) * _ground.CeilingGain(_ceilingAt[i] / _radius, fade);
                _washInflow[i] = s.WashInflow[i] * vh;
                _washInduced[i] = vh;
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
                if (sigma > 0) x += DetMath.Sqrt(1 - a * a) * sigma * VortexRingSpec.Normal(_groundStream, s.Step, i);
                double bound = _ground.RecirculationBound;
                s.WashInflow[i] = x > bound ? bound : x < -bound ? -bound : x;
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

            // The bus: the packs' Thevenin source, loaded by the free ESCs, I = Σ d·(d·V − Ke·ω)/R, and by the limited
            // ones, Σ (I_lim·R + Ke·ω)·I_lim / V. An ESC is limited when its motor's current at the bus's voltage would
            // pass its limit; limiting one raises the voltage, so the set only grows and four rounds settle it.
            double voc0 = OpenCircuitVolts(0, s.PackCharge0), voc1 = OpenCircuitVolts(1, s.PackCharge1);
            double g = _conductance[0] + _conductance[1];
            double vth = (_conductance[0] * voc0 + _conductance[1] * voc1) / g, rth = 1 / g;
            double volts = Bus(vth, rth, 0, 0, 0);
            int limited = 0;
            if (driving)
            {
                for (int round = 0; round < 5; round++)
                {
                    double load = 0, emf = 0, power = 0;
                    for (int i = 0; i < 4; i++)
                    {
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
                    for (int i = 0; i < 4; i++)
                        if ((limited & (1 << i)) == 0 && (s.EscCommand[i] * volts - _ke[i] * s.RotorSpeed[i]) * _invR[i] > _limit[i])
                            more |= 1 << i;
                    if (more == 0) break;
                    limited |= more;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                double w = s.RotorSpeed[i], d = s.EscCommand[i], ke = _ke[i];
                bool held = (limited & (1 << i)) != 0;
                double current = !driving ? 0 : held ? _limit[i] : (d * volts - ke * w) * _invR[i];
                double drive = held ? (_limitVolts[i] + ke * w) / volts : d;
                double q = _torque[i];
                double loss = w > 0 ? _friction[i] : 0;
                double rate = (ke * current - loss - q) / _inertia[i];
                // A free motor's current falls as it speeds up (Ke²/R); a limited one's is held, so only the prop damps.
                double slope = -((driving && !held ? ke * ke * _invR[i] : 0) + (w > 0 ? 2 * q / w : 0)) / _inertia[i];
                double next = w + h * rate / (1 - h * slope);
                if (next < 0) next = 0;
                RotorAero(i, next, out double thrust, out double torque);
                loads.Thrust[i] += thrust;
                loads.YawMoment += _reaction[i] * (_inertia[i] * (next - w) / h + torque);
                loads.SupplyAmps[i] += drive * current;
                loads.Speed[i] += next;
                Turn(ref s, i, w, next, h, ref loads);
                if (held)
                {
                    loads.Limited |= 1 << i;
                    loads.LimitTaken[i] = DetMath.Max(loads.LimitTaken[i], d > 0 ? (d - drive) / d : 0);
                }
                s.RotorSpeed[i] = next;
                _thrust[i] = thrust;
                _torque[i] = torque;
            }

            double amps0 = (voc0 - volts) * _conductance[0], amps1 = (voc1 - volts) * _conductance[1];
            s.PackCharge0 -= amps0 * h / _capacity[0];
            s.PackCharge1 -= amps1 * h / _capacity[1];
            loads.Volts = volts;
            loads.Amps0 = amps0;
            loads.Amps1 = amps1;
            loads.SubSteps++;
        }

        // A rotor turns by the sub-step's mean speed, its angle kept in [0, 2π); an unbalanced prop's residual U, at its
        // angle on the rotor, pulls the hub outward with U·ω² in the disc's plane. A clockwise rotor turns positively
        // about +y (DetMath.cs), so the unbalance at angle θ from body +x points along (cos θ, 0, −sin θ).
        void Turn(ref FlightState s, int i, double w, double next, double h, ref RotorLoads loads)
        {
            double angle = s.RotorAngle[i] + (w + next) * 0.5 * h;
            while (angle >= TwoPi) angle -= TwoPi;
            s.RotorAngle[i] = angle;
            if (!_unbalanced) return;
            double theta = _unbalanceAngle[i] - _reaction[i] * angle;
            DetMath.SinCos(theta, out double sin, out double cos);
            double pull = _unbalance[i] * next * next;
            var f = new Double3(pull * cos, 0, -pull * sin);
            loads.UnbalanceForce = loads.UnbalanceForce + f;
            loads.UnbalanceMoment = loads.UnbalanceMoment + Double3.Cross(_hub[i], f);
        }

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
        // scales the thrust; _meanThrust keeps the thrust without the fluctuation. With the ground and the ceiling, the
        // re-ingested wash's inflow joins the ring's and the surfaces' gain scales the thrust (the torque keeps its own).
        void RotorAero(int i, double w, out double thrust, out double torque)
        {
            if ((_ring == null && _ground == null) || !(w > 0))
            {
                Aero(w, _inflow[i], out thrust, out torque);
                _meanThrust[i] = thrust;
                return;
            }
            double rpm = w * (60 / TwoPi);
            double advance = _inflow[i] / (w * (_diameter / TwoPi));
            double w2 = w * w;
            double added = _ground == null ? _ringInflow[i] : _ringInflow[i] + _washInflow[i];
            double j = (advance >= 0 ? advance : DetMath.Max(advance, DescentAdvanceRatioLimit)) + added * _ringAdvance / w;
            double mean = _kT * Table(_rpm, _rpmThrust, rpm) * InflowRatio(_jThrust, j) * w2;
            if (_ground != null) mean *= _surfaceGain[i];
            _meanThrust[i] = mean;
            thrust = mean * _ringGain[i];
            torque = _kQ * Table(_rpm, _rpmTorque, rpm) * InflowRatio(_jTorque, advance) * w2;
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
