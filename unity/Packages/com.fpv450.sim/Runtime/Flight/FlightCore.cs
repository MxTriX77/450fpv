using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Flight
{
    /// <summary>What fixes a flight before its first step; the log header records all of it.</summary>
    public sealed class FlightSetup
    {
        /// <summary>The flight seed, shown to the pilot (K§6.9).</summary>
        public ulong Seed;

        /// <summary>The flight controller's settings; the flight keeps its own copy.</summary>
        public FcSettings Settings = FcSettings.Default;

        public FlightWorld World;

        /// <summary>
        /// The body's mass model: drone.json's parts at the pilot's loadout (DroneSpec.BuildMass), or a bench's own
        /// body. The flight keeps its own copy and rebuilds it as parts' masses change.
        /// </summary>
        public MassModel Mass;

        /// <summary>
        /// The motors, props and packs (DroneSpec.BuildPropulsion), under the flight controller. Null for a bench's
        /// free body, which has no controller either and follows the stick's arm switch.
        /// </summary>
        public PropulsionSpec Propulsion;

        /// <summary>The motors' asymmetry (MotorAsymmetry, p7b-airframe 1.1): the tolerances each motor, ESC and prop is
        /// drawn from at the start. Null for four identical rotors (Calm's wind grading, a bench).</summary>
        public MotorAsymmetry Asymmetry;

        /// <summary>The rotors' vortex rings (VortexRingSpec, p7b-airframe 1.2): null for none, as a bench's rotors and the
        /// goldens' audits fly.</summary>
        public VortexRingSpec VortexRing;

        /// <summary>The airframe's drag (DroneSpec.BuildAero): the body's and the coil's. Null for none (a bench's body);
        /// the rotors' in-plane drag comes with the propulsion.</summary>
        public AeroSpec Aero;

        /// <summary>The wind (StandInWind): a pilot's level, or still or steady air for a bench. Still by default.</summary>
        public WindSetup Wind;

        /// <summary>The contacts (DroneSpec.BuildContacts, 1.6): null for none (a bench's free body, a flight in open air).</summary>
        public Contact.ContactSpec Contacts;

        /// <summary>The runtime objects the flight places in its world, in order (the launch stand, a test's pole); the
        /// flight resets the world's own first.</summary>
        public RuntimeObject[] RuntimeObjects = new RuntimeObject[0];

        /// <summary>The initial state. A flight starts at rest; a bench may launch a free body.</summary>
        public Double3 StartPosition;
        public Quat StartAttitude = Quat.Identity;
        public Double3 StartVelocity;
        public Double3 StartBodyRate;

        /// <summary>Both packs' state of charge at the start, 0–1.</summary>
        public double StartPackCharge = 1;
    }

    /// <summary>
    /// The flight model (PHB-01): engine-free, a fixed 1 ms step, semi-implicit Euler (K§6.2). A step is a function of
    /// the state, the stick sample in force, the flight's setup and the world queries only (no clock, thread, hash
    /// code or collection order), so a flight replays bit for bit from its log. The FlightScheduler runs it on its
    /// own thread; a replay or a bench calls Advance directly.
    ///
    /// The body is RigidBody's, with the mass, centre of mass and inertia of the setup's MassModel (1.3). Its forces:
    /// the flight controller and propulsion (1.4) when the setup has propulsion; the air (1.5): the stand-in wind
    /// (StandInWind), sampled at each hub, at the body's centre of pressure and at the coil, the rotors' axial
    /// inflow and in-plane drag (Propulsion), the body's and the coil's drag (Aero), and the rotors' gyroscopic
    /// moment; the contacts (ContactModel, 1.6): the legs, the frame's parts and their friction on the ground and the
    /// world's objects. A bench's body without propulsion is a free one, gravity and the torque-free motion of its
    /// inertia (and the drag, when its setup has an AeroSpec).
    ///
    /// Each step first advances the wind's gust from its stream (the gust in the record is the one that acted), then
    /// takes every force from the step's starting state (the contacts' from the planes and ground the last step's sweep
    /// left at that pose), then moves the body, then sweeps the contact shapes from the old pose to the new one.
    ///
    /// The controller runs at its real 4 kHz: each step is FlightController.LoopsPerStep loops, each followed by a
    /// sub-step of the ESCs, motors and packs (Propulsion) at the loop's 250 µs, while the body moves once a step
    /// with the rotors' mean thrust and reaction. The gyro each loop reads ramps from the last loop's reading to the
    /// state's body rate (the last loop reads it exactly): the plant moves at 1 kHz, and the ramp keeps the D term's
    /// derivative of it smooth rather than one spike a step.
    /// </summary>
    public sealed class FlightCore
    {
        /// <summary>The fixed step, s (PHB-01). Never varied.</summary>
        public const double Dt = 0.001;

        /// <summary>The fixed step, µs of host time.</summary>
        public const long StepUs = 1000;

        /// <summary>World, m/s²: standard gravity, 9.806 65 (published, CGPM 1901), straight down.</summary>
        public static readonly Double3 Gravity = new Double3(0, -9.80665, 0);

        /// <summary>The feed's field: 20 steps, the PAL field rate's 20 ms (published), aligned to the flight.</summary>
        public const int FieldSteps = 20;

        const double RadToDeg = 57.29577951308232;
        const double TwoPi = 6.283185307179586;

        static readonly FcFlags[] Saturated =
            { FcFlags.SaturatedRoll, FcFlags.SaturatedPitch, FcFlags.SaturatedYaw, FcFlags.SaturatedCollective };

        FlightState _state;
        FcSettings _settings;
        readonly MassModel _mass;
        readonly FlightController _fc;
        readonly Propulsion _propulsion;
        readonly Aero _aero;
        readonly StandInWind _wind;
        readonly Contact.ContactModel _contacts;
        readonly Double3[] _hubWind = new Double3[4];
        StepTrace _trace;
        readonly StepEvents _events = new StepEvents();

        // The feed's field window: sums over the steps so far, and the last whole field's values.
        Corners _sumHz, _sumAmps, _fieldHz, _fieldAmps;
        double _sumTotal, _maxTotal, _maxStep, _sumVolts, _lastTotal;
        double _fieldTotal, _fieldTotalMax, _fieldStepMax, _fieldVolts;
        int _fieldCount;

        public FlightCore(FlightSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            World = setup.World ?? throw new ArgumentException("the setup needs a world", nameof(setup));
            _settings = setup.Settings;
            _mass = (setup.Mass ?? throw new ArgumentException("the setup needs a mass model", nameof(setup))).Clone();
            Random = new FlightRandom(setup.Seed);
            if (setup.Aero != null)
            {
                if (setup.Aero.CoilPart >= _mass.PartCount || (setup.Aero.CoilPart >= 0 && _mass.Part(setup.Aero.CoilPart).Kind != PartKind.Cylinder))
                    throw new ArgumentException("the aero spec's coil is not a cylinder of the mass model", nameof(setup));
                _aero = new Aero(setup.Aero);
            }
            _wind = new StandInWind(setup.Wind, Random);
            _state = new FlightState
            {
                Position = setup.StartPosition,
                Velocity = setup.StartVelocity,
                Attitude = setup.StartAttitude.Normalized(),
                BodyRate = setup.StartBodyRate,
                Gust = _wind.StartGust,
            };
            if (setup.Propulsion != null)
            {
                Asymmetry = setup.Asymmetry?.Clone();
                Draws = Asymmetry?.DrawFor(Random);
                VortexRing = setup.VortexRing?.Clone();
                SeedStream ring = VortexRing != null ? Random.Open("vrs.thrust") : default;
                _propulsion = new Propulsion(setup.Propulsion, Draws, VortexRing, ring);
                var clockwise = new bool[4];
                for (int i = 0; i < 4; i++) clockwise[i] = setup.Propulsion.Motors[i].Clockwise;
                _fc = new FlightController(_settings, clockwise);
                _state.PackCharge0 = _state.PackCharge1 = setup.StartPackCharge;
                _fc.Reset(ref _state.Fc, Gyro(_state.BodyRate));
            }

            // The world as this flight finds it: its runtime objects in order, then the contacts at the start pose.
            World.Query.ResetRuntimeObjects();
            RuntimeObject[] objects = setup.RuntimeObjects ?? new RuntimeObject[0];
            RuntimeIds = new int[objects.Length];
            for (int i = 0; i < objects.Length; i++)
                RuntimeIds[i] = World.Query.AddObject(objects[i].Name, objects[i].Shapes);
            if (setup.Contacts != null)
            {
                _contacts = new Contact.ContactModel(setup.Contacts, World, _mass.Properties.Mass);
                _contacts.Sense(_state, ref _state, _events);
                _events.Clear();
            }
        }

        public ref readonly FlightState State => ref _state;
        public ref readonly FcSettings Settings => ref _settings;
        public FlightWorld World { get; }

        /// <summary>The body's mass, centre of mass and inertia now (design 3).</summary>
        public ref readonly MassProperties Body => ref _mass.Properties;

        /// <summary>The flight's seed, streams and per-flight draws (design 9).</summary>
        public FlightRandom Random { get; }

        /// <summary>What the last step computed beside the state (StepTrace), for the log.</summary>
        public ref readonly StepTrace Trace => ref _trace;

        /// <summary>The events the last step made, for the log; a producer adds to them inside Advance.</summary>
        public StepEvents Events => _events;

        /// <summary>The motors' asymmetry, or null for identical rotors; and its per-flight draws.</summary>
        public MotorAsymmetry Asymmetry { get; }
        public MotorAsymmetry.Draws Draws { get; }

        /// <summary>The rotors' vortex rings, or null for none.</summary>
        public VortexRingSpec VortexRing { get; }

        /// <summary>The flight's wind, with its per-flight draws.</summary>
        public StandInWind Wind => _wind;

        /// <summary>The airframe's drag, or null for none.</summary>
        public Aero Aero => _aero;

        /// <summary>The flight controller, or null for a body without propulsion.</summary>
        public FlightController Controller => _fc;

        /// <summary>The propulsion model, or null for a body without it.</summary>
        public Propulsion Propulsion => _propulsion;

        /// <summary>The world's object id of each of the setup's runtime objects, in order.</summary>
        public int[] RuntimeIds { get; }

        /// <summary>The contact model, or null for a flight without contacts.</summary>
        public Contact.ContactModel Contacts => _contacts;

        /// <summary>One 1 ms step with the stick sample in force (StickSample, "the timing contract").</summary>
        public void Advance(in StickSample stick)
        {
            ref FlightState s = ref _state;
            _events.Clear();
            _wind.Advance(ref s.Gust, s.Step);

            // The net force (world, N) and the net moment about the CoM (body, N·m), weight aside; the air's share
            // of them in body axes.
            ref readonly MassProperties body = ref _mass.Properties;
            Double3 force = default;
            Double3 moment = default;
            Double3 airForce = default, airMoment = default;
            if (_fc == null) s.Armed = stick.Arm;
            else Propel(ref s, stick, body, ref force, ref moment, ref airForce, ref airMoment);
            if (_aero != null) Drag(s, body, ref airForce, ref airMoment);
            Double3 airWorld = s.Attitude.Rotate(airForce);
            force = force + airWorld;
            moment = moment + airMoment;
            if (_contacts != null) _contacts.Forces(ref s, body, ref force, ref moment, ref _trace, _events);

            // Semi-implicit Euler (RigidBody): the velocities first, then the positions with the new velocities.
            FlightState was = s;
            RigidBody.Step(ref s, in body, Gravity + force * body.InverseMass, moment, Dt);
            _trace.NetForce = force + Gravity * body.Mass;
            _trace.NetMoment = moment;
            _trace.AeroForce = airWorld;
            _trace.AeroMoment = airMoment;
            s.Step++;
            if (_contacts != null) _contacts.Sense(was, ref s, _events);
        }

        // The body's and the coil's drag (Aero) on the air's velocity at their centres, into the air's body-axis sums.
        void Drag(in FlightState s, in MassProperties body, ref Double3 force, ref Double3 moment)
        {
            Double3 v = s.Attitude.InverseRotate(s.Velocity), com = body.CenterOfMass;
            Double3 cp = _aero.Spec.BodyCenterOfPressure;
            Double3 f = _aero.Body(Air(s, v, cp));
            force = force + f;
            moment = moment + Double3.Cross(cp - com, f);
            int coil = _aero.Spec.CoilPart;
            if (coil < 0) return;
            ref readonly DronePart part = ref _mass.Part(coil);
            f = _aero.Coil(Air(s, v, part.Position), part);
            force = force + f;
            moment = moment + Double3.Cross(part.Position - com, f);
        }

        // A body point's velocity through the air, body m/s: the body origin's velocity v (body) plus ω × r, less the
        // wind there.
        Double3 Air(in FlightState s, in Double3 v, in Double3 point) =>
            v + Double3.Cross(s.BodyRate, point) - s.Attitude.InverseRotate(_wind.At(s.Gust, s.Position + s.Attitude.Rotate(point)));

        // The controller's loops and propulsion's sub-steps for one step; the rotors' mean loads on the body: their
        // thrust and reaction into force (world) and moment, their in-plane drag into the air's sums (body).
        void Propel(ref FlightState s, in StickSample stick, in MassProperties body, ref Double3 force, ref Double3 moment,
            ref Double3 airForce, ref Double3 airMoment)
        {
            for (int i = 0; i < 4; i++)
                _hubWind[i] = _wind.At(s.Gust, s.Position + s.Attitude.Rotate(_propulsion.Hub(i)));
            _propulsion.BeginStep(s, _hubWind);
            Axes3 from = s.Fc.GyroPrev, to = Gyro(s.BodyRate);
            var loads = new RotorLoads();
            var loss = new MixerLoss();
            Corners duty = default;
            long loop = s.Step * FlightController.LoopsPerStep;
            for (int j = 0; j < FlightController.LoopsPerStep; j++)
            {
                // The battery task reads the bus voltage the last sub-step left; the loop, the rotors' speeds (DShot).
                if (FlightController.IsBatteryLoop(loop + j)) _fc.Battery(ref s.Fc, loads.Volts);
                if (FlightController.IsFrameLoop(loop + j)) _fc.Frame(ref s.Fc, ref s.Armed, stick);
                Axes3 gyro = to;
                if (j < FlightController.LoopsPerStep - 1)
                {
                    double t = (j + 1) / (double)FlightController.LoopsPerStep;
                    gyro.Roll = from.Roll + (to.Roll - from.Roll) * t;
                    gyro.Pitch = from.Pitch + (to.Pitch - from.Pitch) * t;
                    gyro.Yaw = from.Yaw + (to.Yaw - from.Yaw) * t;
                }
                _fc.Loop(ref s.Fc, s.Armed, gyro, s.RotorSpeed, ref duty, ref loss);
                _propulsion.SubStep(ref s, duty, s.Armed, FlightController.LoopDt, ref loads);
            }

            // Each rotor's mean thrust at its hub along body +y, and the motors' mean reaction about +y; each rotor's
            // in-plane drag at its hub at its mean speed; the rotors' spin momentum h about +y, whose gyroscopic
            // moment on the body is −ω × h (Euler's equation for the body and its spinning rotors; the spin-up part,
            // −ḣ, is the reaction above).
            double share = 1.0 / loads.SubSteps, lift = 0, spin = 0;
            Double3 com = body.CenterOfMass, m = default;
            Corners amps = default;
            for (int i = 0; i < 4; i++)
            {
                double thrust = loads.Thrust[i] * share, speed = loads.Speed[i] * share;
                Double3 r = _propulsion.Hub(i) - com;
                lift += thrust;
                m.X -= r.Z * thrust;
                m.Z += r.X * thrust;
                amps[i] = loads.SupplyAmps[i] * share;
                Double3 h = _propulsion.HForce(i, speed);
                airForce = airForce + h;
                airMoment = airMoment + Double3.Cross(r, h);
                spin += _propulsion.SpinMomentum(i, speed);
            }
            m.Y = loads.YawMoment * share;
            force = s.Attitude.Rotate(new Double3(0, lift, 0));
            moment = m + GyroscopicMoment(s.BodyRate, spin);
            // The props' unbalance: their mean pull on the hubs over the sub-steps, and its moment about the CoM.
            if (_propulsion.Unbalanced)
            {
                Double3 pull = loads.UnbalanceForce * share;
                force = force + s.Attitude.Rotate(pull);
                moment = moment + loads.UnbalanceMoment * share - Double3.Cross(com, pull);
            }

            _trace.RotorCommand = duty;
            _trace.PackVolts0 = _trace.PackVolts1 = loads.Volts;
            _trace.PackAmps0 = loads.Amps0;
            _trace.PackAmps1 = loads.Amps1;

            // MixerSaturation as each axis's saturation begins (A: the share lost) and ends (A: 0).
            for (int a = 0; a < 4; a++)
            {
                double lost = loss[a];
                bool now = lost > 0, was = (s.Fc.Flags & Saturated[a]) != 0;
                if (now == was) continue;
                FlightEvent e = FlightEvent.Of(EventKind.MixerSaturation);
                e.Index = (byte)a;
                e.A = lost;
                _events.Add(e);
                s.Fc.Flags ^= Saturated[a];
            }

            // CurrentLimit as each ESC's limit begins to hold its motor (A: the share of the duty taken) and lets go (A: 0).
            for (int i = 0; i < 4; i++)
            {
                bool now = (loads.Limited & (1 << i)) != 0, was = (s.EscLimited & (1 << i)) != 0;
                if (now == was) continue;
                FlightEvent e = FlightEvent.Of(EventKind.CurrentLimit);
                e.Index = (byte)i;
                e.A = now ? loads.LimitTaken[i] : 0;
                e.B = _propulsion.CurrentLimit(i);
                _events.Add(e);
            }
            s.EscLimited = (byte)loads.Limited;

            // The vortex rings move once a step, after its loops, from its inflow and the rotors' last thrust
            // (Propulsion.AdvanceRing); each rotor's entry and exit is an event with its η, μ and v_h.
            if (_propulsion.HasRing)
            {
                int was = s.RingLatched;
                _propulsion.AdvanceRing(ref s, Dt);
                for (int i = 0; i < 4; i++)
                {
                    bool now = (s.RingLatched & (1 << i)) != 0;
                    if (now == ((was & (1 << i)) != 0)) continue;
                    FlightEvent e = FlightEvent.Of(now ? EventKind.VortexRingEntered : EventKind.VortexRingLeft);
                    e.Index = (byte)i;
                    e.A = _propulsion.RingRate(i);
                    e.B = _propulsion.RingInPlane(i);
                    e.C = _propulsion.RingInduced(i);
                    _events.Add(e);
                }
            }

            Field(s, amps, loads.Volts);
        }

        /// <summary>
        /// The gyroscopic moment of rotors with spin momentum h about body +y on a body turning at ω (body rad/s), body
        /// N·m: −ω × (0, h, 0) = (h·ω_z, 0, −h·ω_x).
        /// </summary>
        public static Double3 GyroscopicMoment(in Double3 bodyRate, double spin) => Double3.Cross(new Double3(0, spin, 0), bodyRate);

        // The feed's field window (FeedSignals): this step's values into the sums; a whole field publishes them.
        void Field(in FlightState s, in Corners amps, double volts)
        {
            double total = 0;
            for (int i = 0; i < 4; i++)
            {
                _sumHz[i] += s.RotorSpeed[i] / TwoPi * _propulsion.PolePairs(i);
                _sumAmps[i] += amps[i];
                total += amps[i];
            }
            _sumTotal += total;
            _maxTotal = DetMath.Max(_maxTotal, total);
            _maxStep = DetMath.Max(_maxStep, DetMath.Abs(total - _lastTotal) / Dt);
            _lastTotal = total;
            _sumVolts += volts;
            if (++_fieldCount < FieldSteps) return;
            double n = _fieldCount;
            Corners hz = default, perMotor = default;
            for (int i = 0; i < 4; i++)
            {
                hz[i] = _sumHz[i] / n;
                perMotor[i] = _sumAmps[i] / n;
            }
            _fieldHz = hz;
            _fieldAmps = perMotor;
            _fieldTotal = _sumTotal / n;
            _fieldTotalMax = _maxTotal;
            _fieldStepMax = _maxStep;
            _fieldVolts = _sumVolts / n;
            _sumHz = _sumAmps = default;
            _sumTotal = _maxTotal = _maxStep = _sumVolts = 0;
            _fieldCount = 0;
        }

        /// <summary>The gyro of a body rate: the pilot's axes in °/s (roll −ω_z, pitch +ω_x nose down, yaw +ω_y).</summary>
        public static Axes3 Gyro(Double3 bodyRate) => new Axes3
        {
            Roll = -bodyRate.Z * RadToDeg,
            Pitch = bodyRate.X * RadToDeg,
            Yaw = bodyRate.Y * RadToDeg,
        };

        /// <summary>The pose of the current state.</summary>
        public void GetPose(out PoseFrame pose)
        {
            ref readonly FlightState s = ref _state;
            pose = new PoseFrame
            {
                Step = s.Step,
                Position = s.Position,
                Attitude = s.Attitude,
                Velocity = s.Velocity,
                BodyRate = s.BodyRate,
                RotorSpeed = s.RotorSpeed,
                Armed = s.Armed,
                LegCompression = _trace.LegCompression, // the step's start: one step behind the pose
            };
        }

        /// <summary>The feed signals of the current state (FeedSignals says which are live).</summary>
        public void GetSignals(out FeedSignals signals)
        {
            // The air at the camera, in its axes (the body's, 0° uptilt): the wind less the camera's motion, taken at
            // the body origin (drone.json has no camera position yet; within 0.1 m, ω × r is below 0.1·|ω| m/s).
            ref readonly FlightState s = ref _state;
            Double3 air = s.Attitude.InverseRotate(_wind.At(s.Gust, s.Position) - s.Velocity);
            signals = new FeedSignals
            {
                Live = FeedSignalSet.CameraAngularVelocity | FeedSignalSet.AirVelocityAtCamera,
                Step = _state.Step,
                CameraAngularVelocity = _state.BodyRate,
                AirVelocityAtCamera = air,
                Crashed = _state.Crashed,
            };
            if (_contacts != null)
            {
                signals.Live |= FeedSignalSet.Impact;
                signals.ImpactCount = _contacts.ImpactCount;
                signals.ImpactPeakAccel = _contacts.ImpactPeakAccel;
                signals.ImpactImpulse = _contacts.ImpactImpulse;
            }
            if (_propulsion == null) return;
            signals.Live |= FeedSignalSet.MotorElectricalHz | FeedSignalSet.MotorCurrent | FeedSignalSet.MotorCurrentStep
                | FeedSignalSet.PackVolts;
            signals.MotorElectricalHz = _fieldHz;
            signals.MotorCurrentA = _fieldAmps;
            signals.MotorCurrentTotalA = _fieldTotal;
            signals.MotorCurrentTotalMaxA = _fieldTotalMax;
            signals.MotorCurrentStepAPerS = _fieldStepMax;
            signals.PackVolts = _fieldVolts;
        }
    }
}
