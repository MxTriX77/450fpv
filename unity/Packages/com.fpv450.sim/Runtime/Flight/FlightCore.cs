using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Tether;
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

        /// <summary>The build's placement (BuildTolerance, D-066, PHY-24): where the bottle, the packs, the coil and the
        /// leads sit this flight, drawn at the start, so the centre of mass is a few millimetres off the exact build's.
        /// Null for the exact build (the goldens, a bench).</summary>
        public BuildTolerance Placement;

        /// <summary>The rotors' vortex rings (VortexRingSpec, p7b-airframe 1.2): null for none, as a bench's rotors and the
        /// goldens' audits fly.</summary>
        public VortexRingSpec VortexRing;

        /// <summary>The rotors' ground and ceiling effect and recirculation (GroundEffectSpec, p7b-airframe 1.3): null for
        /// none, as a bench's rotors and the goldens' audits fly.</summary>
        public GroundEffectSpec GroundEffect;

        /// <summary>Each rotor's own wake unsteadiness in free air (RotorWakeSpec, D-064): null for a perfectly steady rotor, as a
        /// bench's rotors and the goldens' audits fly.</summary>
        public RotorWakeSpec RotorWake;

        /// <summary>The rotors' edgewise-flow moments, blade flapping and the dissymmetry of lift (RotorFlappingSpec,
        /// p7b-airframe 1.3c): null for none, as a bench's rotors and the goldens' audits fly.</summary>
        public RotorFlappingSpec Flapping;

        /// <summary>The rotors in each other's wake, the rear rotors in the front rotors' in forward flight (RotorInterferenceSpec,
        /// p7b-airframe 1.3d): null for none, as a bench's rotors and the goldens' audits fly.</summary>
        public RotorInterferenceSpec Interference;

        /// <summary>The airframe's elastic modes, the arms' bending and the payload's and the coil's mounts (AirframeModesSpec,
        /// p7b-airframe 1.4): null for a rigid airframe, as a bench and the goldens' audits fly.</summary>
        public AirframeModesSpec Modes;

        /// <summary>The airframe's drag (DroneSpec.BuildAero): the body's and the coil's. Null for none (a bench's body);
        /// the rotors' in-plane drag comes with the propulsion.</summary>
        public AeroSpec Aero;

        /// <summary>The wind (WindField): a pilot's level, or still or steady air for a bench. Still by default.</summary>
        public WindSetup Wind;

        /// <summary>The rain (RainField, p7c-wind 1.7): the pilot's own setting, independent of the wind. Sunny by default.
        /// Rain needs the airframe's drag (Aero), whose silhouettes it falls on.</summary>
        public RainLevel Rain;

        /// <summary>The camera's lens, body m (drone.json's camera block, DroneSpec.CameraPosition): where the feed's air
        /// velocity at the camera is taken. The body's origin by default.</summary>
        public Double3 CameraPosition;

        /// <summary>The contacts (DroneSpec.BuildContacts, 1.6): null for none (a bench's free body, a flight in open air).</summary>
        public Contact.ContactSpec Contacts;

        /// <summary>The fiber's table (tether.json, p7d-tether 1.1): null for a flight without the line. With it the flight
        /// flies the line (FiberLine, 1.6): its coil beneath paying out through the rear cap, its span and its laid trail. The
        /// header records its rows and hash.</summary>
        public Tether.TetherTable Tether;

        /// <summary>The line as the pilot laid it before the start, world m: from its anchor (the first point) along the
        /// ground to the point beneath the exit, which the line then rises from (each point set on the ground). Null: the
        /// table's launch, straight from launch.anchor_behind_start_m behind the start.</summary>
        public Double3[] TetherLayout;

        /// <summary>The coil's part in the mass model and its empty spool's mass, kg (DroneSpec's coil): the line on the coil
        /// is (the part's mass − the spool's)/μ, and the part loses μ per metre paid out. −1: no coil part (the line's mass
        /// leaves no part, and the coil never runs out).</summary>
        public int TetherCoilPart = -1;
        public double TetherSpoolMass;

        /// <summary>The known-wrong two-way spool (p7d-tether design 16), which takes line back at the static hold: a bench's
        /// model, never a pilot's flight.</summary>
        public bool TetherTwoWay;

        /// <summary>The known-wrong rotors' air on the line (p7d-tether 1.6b, D-075): design 4's field alone, without the
        /// fountain and the ground vortex (RotorAir), so a line under the drone never reaches a disc by a modelled flow; a
        /// bench's model and the audit against model 44's line, never a pilot's flight.</summary>
        public bool TetherDesign4Air;

        /// <summary>The line without 1.7's contacts (p7d-tether design 6): it meets the plane alone, with the span's polymer pair,
        /// as model 50's line did; the audit against model 50 and a bench's known-wrong, never a pilot's flight.</summary>
        public bool TetherWithoutContacts;

        /// <summary>The line without 1.8's cut risk and link (p7d-tether design 7, 8): it never breaks and carries no margin, as
        /// model 55's line did; the audit against model 55, never a pilot's flight.</summary>
        public bool TetherWithoutCuts;

        /// <summary>The known-wrong one strength (p7d-tether design 16): every check, the bends' and the peel's, meets the span's
        /// per-flight strength; a bench's model, never a pilot's flight.</summary>
        public bool TetherOneStrength;

        /// <summary>The known-wrong fountain through the body (p7d-tether, model 61): the rotors' fountain rises through the
        /// drone's own stack, coil, packs and payload as model 59's field did (RotorAir without the body); the audit against
        /// model 59 and a bench's known-wrong, never a pilot's flight.</summary>
        public bool TetherFountainThroughBody;
        /// <summary>The known-wrong 0-force hold (p7d-tether 1.7's part slots as model 59 had them): a drone part's hold on the
        /// line acts in the chain's solve whatever its normal force, and its speculative spring pulls (CableChain.ZeroForceHold),
        /// so a line in a crotch of two parts is carried with nothing pressing it, and the energy check's safe pass keeps the spool's
        /// paying model on a segment that pays nothing (model 59's chain); the audit against model 59 and a bench's known-wrong, never a pilot's flight.</summary>
        public bool TetherZeroForceHold;
        /// <summary>The known-wrong linear step (p7d-tether 1.8, model 74's chain): the line's step solved on its start's
        /// linearisation alone (CableChain.LinearStep: each segment's tangent at the step's start), whose residue the cuts read as
        /// tension (1.8's finding 2); the audit against model 74 and a bench's known-wrong, never a pilot's flight.</summary>
        public bool TetherLinearStep;
        /// <summary>The known-wrong sliding trail (the laid trail before w3-farmland-ground 2.4): a laid node rests only while it
        /// presses its support slower than trail.freeze_speed_m_s (a node the ground's stick spring holds rings about its hold
        /// faster, so the laid line never froze), freezes only in air its ground's friction holds, and the trail's last point
        /// lifts once the pull's size passes the tension it held and the friction of trail.hold_length_m of line, whichever way
        /// it pulls; a laid node the ground holds round a turn of the line is merged into its chord (only a crest is held). The
        /// audit against model 84 before 2.4 and every older line, never a pilot's flight.</summary>
        public bool TetherSlidingTrail;

        /// <summary>The gyro's full scale, °/s (p7f-damage 1.6, the losses, model 72): each axis the flight controller reads is clipped to ±it,
        /// as its sensor's range does: 2000 (published: Betaflight 4.5's ICM-426xx driver sets INV_FSR_2000DPS; the class's SpeedyBee
        /// F405 V4 carries an ICM-42688-P; its BMI270 and MPU-6000 drivers set ±2000 too). 0: no range, model 68's clean gyro.</summary>
        public double GyroRange = 2000;

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
    /// the flight controller and propulsion (1.4) when the setup has propulsion; the air (1.5): the wind
    /// field (WindField, p7c-wind), sampled at each hub, at the body's centre of pressure and at the coil, the rotors' axial
    /// inflow and in-plane drag (Propulsion), the body's and the coil's drag (Aero), and the rotors' gyroscopic
    /// moment; the contacts (ContactModel, 1.6): the legs, the frame's parts and their friction on the ground and the
    /// world's objects. A bench's body without propulsion is a free one, gravity and the torque-free motion of its
    /// inertia (and the drag, when its setup has an AeroSpec).
    ///
    /// Each step first fixes the wind field's frame (its time and the ground under the body; the record carries the wind
    /// that acted at the body's origin and at each hub), then takes every force from the step's starting state (the contacts' from the planes and ground the last step's sweep
    /// left at that pose), then moves the body, then sweeps the contact shapes from the old pose to the new one, and then the
    /// blades' passes across the shapes their discs cut take their work off the rotors and give the body their impulses
    /// (PropStrikes, p7f-damage 1.3; the damage they leave flies from the next step).
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

        FlightState _state;
        FcSettings _settings;
        readonly MassModel _mass;
        readonly FlightController _fc;
        readonly Propulsion _propulsion;
        readonly Aero _aero;
        readonly WindField _wind;
        readonly RainField _rain;
        readonly int _filmPart = -1;
        readonly Double3 _camera;
        double _rainMassRate;
        readonly Contact.ContactModel _contacts;
        readonly Contact.PropStrikes _strikes; // the blades against what the discs cut (p7f-damage 1.3), or null
        int _bound; // the motors bound at the last step's end (MotorBound)

        /// <summary>
        /// BLHeli_S 16.7's low-rpm power protection's floor, the duty share its ESC allows a stalled or slow motor
        /// (bitdump/BLHeli, BLHeli_S SiLabs/BLHeli_S.asm, derived, p7f-damage 1.5): Pwm_Limit_Beg is the high byte of
        /// 50 × Pgm_Startup_Pwr_Decoded doubled, 0x40 at DEFAULT_PGM_STARTUP_PWR 9, so 25 of 255; set_pwm_limit_low_rpm holds the
        /// duty to Low_Rpm_Pwr_Slope (the startup power, 9) × (255 div Comm_Period4x_H), no lower than that floor, full above about
        /// 5000 rpm on these 7-pole-pair motors. Not flown (drone.json's esc_current_limit_a note): MotorBound logs the drive it
        /// would allow.
        /// </summary>
        public const double BlheliSLowRpmFloor = 25.0 / 255;
        readonly Contact.LoosePieces _loose; // the pieces a damaged leg drops (p7g-legs 1.3), with the legs' law
        readonly Double3[] _hubWind = new Double3[4];
        readonly Sim.World.XZ[] _windAt = new Sim.World.XZ[1];
        readonly Sim.World.GroundSample[] _windGround = new Sim.World.GroundSample[1];
        readonly Sim.World.Ray[] _soilRay = new Sim.World.Ray[1]; // FlightWorld.SoilUnder's buffers
        readonly Sim.World.RayHit[] _soilHit = new Sim.World.RayHit[1];
        readonly RotorSurfaces _surfaces; // the rotors' surfaces (GroundEffectSpec), or null
        readonly AirframeModes _modes; // the airframe's modes (AirframeModesSpec), or null
        readonly VibrationBands _vibration; // the camera mount's vibration, with the modes
        readonly ArmFlex _arms; // the arms in their plane (AirframeModesSpec.ArmInPlane), or null
        readonly double _gyroRange; // the gyro's full scale, °/s (FlightSetup.GyroRange; 0 none)
        Corners _rotorThrust = default; // each rotor's mean thrust over the step's sub-steps, for the modes
        ModeSet _modeAccel; // the modes' accelerations over the last step, for the camera
        StepTrace _trace;
        readonly StepEvents _events; // sized from the contact table (StepEvents.ForContacts, w9-village-physics 2.3)
        readonly Tether.FiberLine _tether; // the fiber (FlightSetup.Tether), or null
        readonly Control.Failsafe _failsafe; // the controller's failsafe on the line's break (p7d-tether 1.8), or null
        // The link's field window (FeedSignals): the exit's tension (sum, max), the least bend radius, the margin (sum, least).
        int _linkCount;
        double _sumTension, _maxTension, _leastBend = double.PositiveInfinity, _sumMargin, _leastMargin = double.PositiveInfinity;
        double _fieldTension, _fieldTensionMax, _fieldBend = double.PositiveInfinity, _fieldMargin, _fieldMarginLeast;
        LinkState _fieldLink;
        bool _linkField; // a whole field has been published
        readonly Double3[] _tetherHubs;
        StickSample _stick; // the last step's sample in force, published with the signals (FeedSignals.Sticks)

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
            MassModel mass = (setup.Mass ?? throw new ArgumentException("the setup needs a mass model", nameof(setup))).Clone();
            Random = new FlightRandom(setup.Seed);
            // The build's placement first, so the airframe's modes and the body see the flight's own centre of mass.
            Placement = setup.Placement?.Clone();
            if (Placement != null)
            {
                PlacementOffsets = Placement.DrawFor(Random);
                Placement.Apply(mass, PlacementOffsets);
            }
            if (setup.Aero != null)
            {
                if (setup.Aero.CoilPart >= mass.PartCount || (setup.Aero.CoilPart >= 0 && mass.Part(setup.Aero.CoilPart).Kind != PartKind.Cylinder))
                    throw new ArgumentException("the aero spec's coil is not a cylinder of the mass model", nameof(setup));
                _aero = new Aero(setup.Aero);
            }
            // A pilot's level over a terrain carries its hills (HillFlow, w7-farmland-wind 1.1), derived from the world.
            bool level = setup.Wind.Level == WindLevel.Calm || setup.Wind.Level == WindLevel.Windy || setup.Wind.Level == WindLevel.Severe;
            _wind = new WindField(setup.Wind, Random, level ? World.Hills : null);
            _camera = setup.CameraPosition;

            // The rain (a Sunny flight builds none and opens no stream): its draws, then its film as the mass model's
            // last part, at 0 kg until the drops fill it (0 kg leaves the model's mass, centre and inertia bit for bit).
            if (setup.Rain != RainLevel.Sunny)
            {
                if (_aero == null) throw new ArgumentException("rain needs the airframe's drag (an AeroSpec)", nameof(setup));
                int coil = setup.Aero.CoilPart;
                _rain = new RainField(setup.Rain, Random, setup.Aero, coil >= 0 ? Aero.CoilSurface(mass.Part(coil)) : 0);
                var parts = new DronePart[mass.PartCount + 1];
                for (int i = 0; i < mass.PartCount; i++) parts[i] = mass.Part(i);
                parts[mass.PartCount] = RainField.FilmPartOf(setup.Aero);
                mass = new MassModel(parts);
                _filmPart = mass.PartCount - 1;
                if (_rain.FilmMass > 0) mass.SetPartMass(_filmPart, _rain.FilmMass);
            }
            _mass = mass;
            _state = new FlightState
            {
                Position = setup.StartPosition,
                Velocity = setup.StartVelocity,
                Attitude = setup.StartAttitude.Normalized(),
                BodyRate = setup.StartBodyRate,
            };
            if (setup.Propulsion != null)
            {
                Asymmetry = setup.Asymmetry?.Clone();
                Draws = Asymmetry?.DrawFor(Random);
                VortexRing = setup.VortexRing?.Clone();
                SeedStream ring = VortexRing != null ? Random.Open("vrs.thrust") : default;
                GroundEffect = setup.GroundEffect?.Clone();
                SeedStream wash = GroundEffect != null ? Random.Open("ground.recirc") : default;
                RotorWake = setup.RotorWake?.Clone();
                SeedStream wake = RotorWake != null ? Random.Open("rotor.wake") : default;
                Flapping = setup.Flapping?.Clone();
                Interference = setup.Interference?.Clone();
                SeedStream interference = Interference != null ? Random.Open("rotor.interference") : default;
                _propulsion = new Propulsion(setup.Propulsion, Draws, VortexRing, ring, GroundEffect, wash, RotorWake, wake, Interference, interference)
                {
                    Flapping = Flapping,
                };
                ModesSpec = setup.Modes?.Clone();
                if (ModesSpec != null)
                {
                    var hubs = new Double3[4];
                    for (int i = 0; i < 4; i++) hubs[i] = setup.Propulsion.Motors[i].Position;
                    _modes = new AirframeModes(ModesSpec, _mass, hubs);
                    _vibration = new VibrationBands(Dt);
                    if (ModesSpec.ArmInPlane)
                    {
                        _arms = new ArmFlex(ModesSpec, hubs);
                        _propulsion.SetArms(_arms);
                    }
                }
                _gyroRange = setup.GyroRange;
                if (GroundEffect != null) _surfaces = new RotorSurfaces(_propulsion, GroundEffect.Reach * setup.Propulsion.Props.Diameter / 2);
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
            _events = new StepEvents(StepEvents.ForContacts(setup.Contacts?.Shapes?.Length ?? 0));
            if (setup.Contacts != null)
            {
                Contact.Moisture moisture = Contact.SurfaceTable.MoistureOf(setup.Rain);
                _contacts = new Contact.ContactModel(setup.Contacts, World, _mass.Properties.Mass, moisture, random: Random);
                // Props are parts at rest (p7f-damage 1.3b): a resting rotor's blades are contact shapes from the first Sense.
                if (_propulsion != null && setup.Propulsion.Rotor != null && setup.Contacts.BladeStrikes && setup.Contacts.BladeRest)
                {
                    _contacts.AttachRotors(_propulsion, setup.Propulsion);
                    _propulsion.BackSpeed = setup.Contacts.BladeRestSpeed;
                }
                // The motors' bells (p7f-damage 1.5): a contact on a turning bell drags its rotor, dents its ring and rubs its gap.
                if (_propulsion != null) _contacts.AttachBells(_propulsion, setup.Propulsion);
                _contacts.Sense(_state, ref _state, _events);
                _events.Clear();
                if (_contacts.Legs != null)
                {
                    _loose = new Contact.LoosePieces(_contacts.Spec, World, _mass, moisture);
                    _trace.Loose = _loose;
                }
                if (_propulsion != null && setup.Propulsion.Rotor != null && setup.Contacts.BladeStrikes)
                    _strikes = new Contact.PropStrikes(_contacts, World, _propulsion, setup.Propulsion, Random);
            }

            // The fiber (p7d-tether 1.6), laid at the start pose; its draws after every other model's.
            if (setup.Tether != null)
            {
                if (setup.TetherCoilPart >= _mass.PartCount) throw new ArgumentException("the line's coil part is not a part of the mass model", nameof(setup));
                _tether = new Tether.FiberLine(setup.Tether, setup, _mass, Random, _state, Dt, _contacts, _propulsion);
                // The step-start points' wind and the line's nodes' on the field's batch worker, alongside the drone's step:
                // the same bits (WindBatch, 1.13).
                if (_wind.HasProfile) WindBatch = _wind.Batch(_tether.Chain.Capacity + 8);
                if (_fc != null && !setup.TetherWithoutCuts) _failsafe = new Control.Failsafe(_settings, Dt);
                _tetherHubs = new Double3[4];
                if (setup.Propulsion != null)
                    for (int i = 0; i < 4; i++) _tetherHubs[i] = setup.Propulsion.Motors[i].Position;
            }
        }

        /// <summary>The pieces the legs dropped (p7g-legs 1.3), or null without the legs' law.</summary>
        public Contact.LoosePieces Loose => _loose;

        /// <summary>
        /// A leg bent before the first step (the scenarios' damaged builds; a flight never calls it, and its log would not
        /// replay): the leg's set, its shapes turned by it.
        /// </summary>
        public void PresetLeg(int leg, Double3 set, bool move = true) => _contacts.PresetLeg(leg, set, _state, move);

        /// <summary>
        /// A leg off before the first step (the scenarios' damaged builds; a flight never calls it, and its log would not
        /// replay): slid out of its clamp, its piece dropped (no loose body: it is not there), its skid hanging from the other
        /// leg on it, the build rebuilt.
        /// </summary>
        public void RemoveLeg(int leg)
        {
            _contacts.Legs.Remove(leg);
            Contact.LegChange ch = _contacts.Restructure(_state, _events);
            Rebuild(ch, ref _state, false);
            _events.Clear();
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

        /// <summary>The build's placement and this flight's offsets (null for the exact build).</summary>
        public BuildTolerance Placement { get; }
        public BuildTolerance.Offsets PlacementOffsets { get; }
        public MotorAsymmetry.Draws Draws { get; }

        /// <summary>The rotors' vortex rings, or null for none.</summary>
        public VortexRingSpec VortexRing { get; }

        /// <summary>The rotors' ground and ceiling effect, or null for none.</summary>
        public GroundEffectSpec GroundEffect { get; }

        /// <summary>The rotors' own wake unsteadiness, or null for perfectly steady rotors.</summary>
        public RotorWakeSpec RotorWake { get; }

        /// <summary>The rotors' flapping, or null for none.</summary>
        public RotorFlappingSpec Flapping { get; }

        /// <summary>The rotors in each other's wake, or null for none.</summary>
        public RotorInterferenceSpec Interference { get; }

        /// <summary>The airframe's modes' spec and the modes this body has, or null for a rigid airframe.</summary>
        public AirframeModesSpec ModesSpec { get; }
        public AirframeModes Modes => _modes;

        /// <summary>The camera mount's vibration bands (FeedSignals.CameraVibration), or null without the modes.</summary>
        public VibrationBands Vibration => _vibration;

        /// <summary>The flight's wind, with its per-flight draws.</summary>
        public WindField Wind => _wind;

        /// <summary>The flight's rain, or null for a Sunny flight.</summary>
        public RainField Rain => _rain;

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

        /// <summary>The fiber (p7d-tether 1.6), or null for a flight without the line.</summary>
        public Tether.FiberLine Tether => _tether;
        /// <summary>The blades' strikes on what their discs cut (p7f-damage 1.3), or null: no contacts, no propulsion, no props.blade,
        /// or ContactSpec.BladeStrikes off.</summary>
        public Contact.PropStrikes Strikes => _strikes;

        /// <summary>One 1 ms step with the stick sample in force (StickSample, "the timing contract").</summary>
        public void Advance(in StickSample stick)
        {
            ref FlightState s = ref _state;
            _events.Clear();
            // After the line's break no frame reaches the controller: it flies its failsafe's sample (p7d-tether 1.8).
            StickSample flown = _failsafe != null && _failsafe.Active ? _failsafe.Filter(s.Step, stick, _events) : stick;
            _stick = flown;
            s.BladeStrikes = default;
            Corners angles = s.RotorAngle; // where the blades start the step, for their passes (PropStrikes)
            _windBatch?.Join(); // nothing evaluates the wind while its frame changes
            BeginWind(s);
            PostWind(s); // the step's points on the worker, from the frame just begun

            // The net force (world, N) and the net moment about the CoM (body, N·m), weight aside; the air's share
            // of them in body axes.
            ref readonly MassProperties body = ref _mass.Properties;
            Double3 force = default;
            Double3 moment = default;
            Double3 airForce = default, airMoment = default;
            if (_fc == null) s.Armed = flown.Arm;
            else Propel(ref s, flown, body, ref force, ref moment, ref airForce, ref airMoment);
            if (_aero != null) Drag(s, body, ref airForce, ref airMoment);
            Double3 airWorld = s.Attitude.Rotate(airForce);
            force = force + airWorld;
            moment = moment + airMoment;
            if (_contacts != null) _contacts.Forces(ref s, body, ref force, ref moment, ref _trace, _events);
            if (_tether != null) TetherForce(s, body, ref force, ref moment);

            // The airframe's modes under the rotors' thrust and the relief of the body's whole load (AirframeModes).
            if (_modes != null)
                _modes.Advance(ref s.ModeShape, ref s.ModeRate, out _modeAccel, _rotorThrust, s.Attitude.InverseRotate(force), moment, Dt);

            // Semi-implicit Euler (RigidBody): the velocities first, then the positions with the new velocities.
            FlightState was = s;
            RigidBody.Step(ref s, in body, Gravity + force * body.InverseMass, moment, Dt);
            _trace.NetForce = force + Gravity * body.Mass;
            _trace.NetMoment = moment;
            _trace.AeroForce = airWorld;
            _trace.AeroMoment = airMoment;
            s.Step++;
            // The pieces already loose step to this step's end; a leg or a T-joint that let go in this step's forces takes
            // its pieces off now, at the moved pose, before the sweep (p7g-legs 1.3).
            if (_loose != null)
            {
                _loose.Step(_wind);
                if (_contacts.RestructurePending) Rebuild(_contacts.Restructure(s, _events), ref s, true);
            }
            if (_contacts != null) _contacts.Sense(was, ref s, _events);
            // The blades' passes across what the discs cut: their work off the rotors, their impulses on the body, the damage
            // they leave (p7f-damage design 6), after the move as the strikes are sensed.
            if (_strikes != null)
            {
                _strikes.Apply(angles, ref s, body, _events);
                // Each strike's push along its rotor's axis loads its arm at the tip in this step (w9-village-physics 2.2, G8).
                if (_modes != null && ModesSpec.StrikeLoad) _modes.Strike(ref s.ModeRate, ref _modeAccel, _strikes.TipImpulse, Dt);
            }
            _contacts?.Shafts?.Trace(s, ref _trace); // the shafts after the strikes that bend them (p7f-damage 1.6)
            if (_vibration != null) _vibration.Add(CameraAcceleration(was, s), FieldSteps);

            // The line steps with the drone where it now is (its force acts in the next step); at a rebuild mark the coil's
            // mass and the airframe's modes follow, the body's motion kept (RigidBody: the state is the body origin's).
            if (_tether != null)
            {
                _tether.Step(s, _tetherHubs, _rotorThrust, _wind, _events, ref _trace.Tether, angles);
                if (_tether.MassChanged)
                {
                    _mass.SetPartMass(_tether.CoilPart, _tether.CoilMass);
                    _modes?.Rebuild(_mass, ref s.ModeShape, ref s.ModeRate);
                }
                if (_tether.BrokeThisStep) _failsafe?.Lose(s.Step, flown);
                if (_tether.Link != null) LinkField();
            }

            if (_windBatch != null) TraceWind();

            // The film takes the water the drops brought this step; the next step flies its mass.
            if (_rain == null) return;
            _rain.Fill(_rainMassRate, Dt);
            _mass.SetPartMass(_filmPart, _rain.FilmMass);
            _trace.FilmMass = _rain.FilmMass;
        }

        // The line's pull at the exit (FiberLine.Force, from the last step's end) and its moment about the centre of mass, and
        // the reactions of the drone's parts the line presses on (FiberContacts, 1.7), each at its contact point.
        void TetherForce(in FlightState s, in MassProperties body, ref Double3 force, ref Double3 moment)
        {
            Double3 f = _tether.Force;
            Double3 m = Double3.Cross(_tether.ExitBody - body.CenterOfMass, s.Attitude.InverseRotate(f));
            Tether.FiberContacts parts = _tether.Contacts;
            for (int i = 0; parts != null && i < parts.ReactionCount; i++)
            {
                Double3 r = parts.ReactionForce(i);
                f = f + r;
                m = m + Double3.Cross(parts.ReactionPoint(i) - body.CenterOfMass, s.Attitude.InverseRotate(r));
            }
            force = force + f;
            moment = moment + m;
            _trace.Tether.Force = f;
            _trace.Tether.Moment = m;
        }

        // The build after a structure change (p7g-legs 1.3, design 5, the coil's rule): each piece flies loose from where it is
        // with the drone's motion at its parts (fly), then the build without it, every leaving part at 0 kg and a broken leg
        // cut to its stub (LoosePieces.Cut), rebuilt exactly once (MassModel), and the airframe's modes solved again for it.
        // The state is the body origin's (RigidBody), so its velocity and turn stay: the body keeps its linear and angular
        // momentum less what the pieces take, which is theirs at the same motion.
        void Rebuild(Contact.LegChange ch, ref FlightState s, bool fly)
        {
            if (fly)
                for (int p = 0; p < ch.Pieces; p++) _loose.Spawn(ch, p, s, _mass, _contacts, _events);
            Contact.LegDamageSpec l = _contacts.Spec.Legs;
            for (int p = 0; p < ch.Pieces; p++)
            {
                int bits = ch.Parts[p];
                for (int c = 0; c < 4; c++)
                {
                    if ((bits & 1 << c) != 0) Off(l.Parts[c]);
                    if ((bits & 1 << (8 + c)) != 0) Off(l.TJointParts[c]);
                }
                for (int side = 0; side < 2; side++)
                    if ((bits & 1 << (12 + side)) != 0)
                        for (int c = 0; c < 4; c++)
                            if ((l.Bore[c].X >= 0) == (side == 0)) Off(l.SkidParts[c]);
            }
            for (int c = 0; c < 4; c++)
            {
                if ((ch.Stubs & 1 << c) == 0) continue;
                Contact.LoosePieces.Cut(_mass.Part(l.Parts[c]), l.Clamp[c], out _, out DronePart stub);
                _mass.SetPart(l.Parts[c], stub, false);
            }
            _mass.Rebuild();
            _modes?.Rebuild(_mass, ref s.ModeShape, ref s.ModeRate);
        }

        void Off(int part)
        {
            DronePart p = _mass.Part(part);
            p.Mass = 0;
            _mass.SetPart(part, p, false);
        }

        // The camera mount's acceleration over the step, body m/s²: the body origin's (from its velocities), the turn's at the
        // camera, α × r + ω × (ω × r), and the frame's elastic share (AirframeModes).
        /// <summary>
        /// The camera mount's vibration bands for the feed (FeedSignals.CameraVibration), m/s²: the stepped body's and the frame's
        /// (VibrationBands.Field) with the brinelled bearings' ball-pass lines added in quadrature in the bands they fall in
        /// (Contact.MotorShaft.BallPass, p7f-damage 1.6: above the step's Nyquist, so never stepped; an output only).
        /// </summary>
        public Double3 CameraVibration()
        {
            Double3 field = _vibration != null ? _vibration.Field : default;
            Contact.MotorShaft shafts = _contacts?.Shafts;
            if (shafts == null) return field;
            Double3 lines = shafts.BallPass(_state.RotorSpeed, _mass.Properties.Mass);
            if (lines.X == 0 && lines.Y == 0 && lines.Z == 0) return field;
            return new Double3(DetMath.Sqrt(field.X * field.X + lines.X), DetMath.Sqrt(field.Y * field.Y + lines.Y),
                DetMath.Sqrt(field.Z * field.Z + lines.Z));
        }

        Double3 CameraAcceleration(in FlightState was, in FlightState now)
        {
            Double3 r = ModesSpec.Camera, w = now.BodyRate;
            Double3 a = now.Attitude.InverseRotate((now.Velocity - was.Velocity) * (1 / Dt));
            Double3 alpha = (now.BodyRate - was.BodyRate) * (1 / Dt);
            return a + Double3.Cross(alpha, r) + Double3.Cross(w, Double3.Cross(w, r)) + _modes.CameraAcceleration(_modeAccel);
        }

        // The wind field's frame for the step that starts at s (WindField.Begin): the ground under the body's origin from
        // the world when the field has a profile, and the wind there for the record.
        void BeginWind(in FlightState s)
        {
            double ground = 0;
            if (_wind.HasProfile)
            {
                _windAt[0] = new Sim.World.XZ(s.Position.X, s.Position.Z);
                World.SampleGround(_windAt, _windGround);
                ground = (_windGround[0].Flags & Sim.World.GroundFlags.Hole) != 0
                    ? World.SoilUnder(s.Position, _soilRay, _soilHit, out _) // model 20: the wind over a hole
                    : _windGround[0].Height;
            }
            _windOrigin = ground;
            _wind.Begin(s.Step, s.Position, ground);
            if (_windBatch != null) return; // the record's two from the batch, at the step's end (TraceWind)
            _trace.Wind = _wind.At(s.Position, ground);
            _trace.CameraAir = AirAtCamera(s);
        }

        // The ground under one of the drone's points for the field's profile there (w7-farmland-wind 1.1: every point its own,
        // SampleGround's height, a terrain's hills included); over a hole NaN, the step's ground and cut frame (WindField.At:
        // model 20's soil under the origin, whose patches' boxes and gaps, ±0.13 m on S04, lie under the cover's still layer);
        // 0 for a field without a profile, which never reads it.
        double WindGround(in Double3 point)
        {
            if (!_wind.HasProfile) return 0;
            _windAt[0] = new Sim.World.XZ(point.X, point.Z);
            World.SampleGround(_windAt, _windGround);
            return (_windGround[0].Flags & Sim.World.GroundFlags.Hole) != 0 ? double.NaN : _windGround[0].Height;
        }

        double _windOrigin; // the ground under the step's origin (BeginWind)

        // The batch worker (WindBatch) with the line, else null; the step's points' indices in it (−1: not posted).
        Wind.WindBatch _windBatch;
        int _windHub = -1, _windCp = -1, _windCoil = -1, _windBody = -1, _windCamera = -1;
        Double3 _windPosition, _windVelocity, _windRate;
        Quat _windAttitude;

        /// <summary>
        /// The batch worker the step's wind is evaluated on (set where the field has a profile and the flight has the line:
        /// the hubs', the drag's and the record's points at the step's start, and the line's nodes), or null to evaluate them
        /// all on the flight thread. Either gives the same bits (WindBatch).
        /// </summary>
        public Wind.WindBatch WindBatch
        {
            get => _windBatch;
            set
            {
                _windBatch?.Join();
                _windBatch = value;
                _windHub = _windCp = _windCoil = _windBody = _windCamera = -1;
                if (_tether != null) _tether.WindBatch = value;
            }
        }

        // The points the step reads the wind at from its start, as Propel, Drag and BeginWind name them, then the line's
        // nodes, posted to the worker.
        void PostWind(in FlightState s)
        {
            if (_windBatch == null) return;
            _windBatch.Clear();
            _windHub = -1;
            if (_fc != null)
                for (int i = 0; i < 4; i++)
                {
                    int at = Post(s.Position + s.Attitude.Rotate(_propulsion.Hub(i)));
                    if (i == 0) _windHub = at;
                }
            _windCp = _windCoil = -1;
            if (_aero != null)
            {
                _windCp = Post(s.Position + s.Attitude.Rotate(_aero.Spec.BodyCenterOfPressure));
                if (_aero.Spec.CoilPart >= 0) _windCoil = Post(s.Position + s.Attitude.Rotate(_mass.Part(_aero.Spec.CoilPart).Position));
            }
            _windBody = _windBatch.Add(s.Position, _windOrigin);
            _windCamera = Post(s.Position + s.Attitude.Rotate(_camera));
            _windPosition = s.Position;
            _windAttitude = s.Attitude;
            _windVelocity = s.Velocity;
            _windRate = s.BodyRate;
            _tether?.AddWind();
            _windBatch.Post();
        }

        // A point posted over its own ground: its index in the batch (−1: full).
        int Post(in Double3 point) => _windBatch.Add(point, WindGround(point));

        // The record's wind at the body and air at the camera from the step's start (BeginWind's, from the batch).
        void TraceWind()
        {
            _trace.Wind = _windBatch.At(_windBody, _windPosition, _windOrigin);
            _trace.CameraAir = _windAttitude.InverseRotate(WindAt(_windCamera, _windPosition + _windAttitude.Rotate(_camera)) - _windVelocity)
                - Double3.Cross(_windRate, _camera);
        }

        // The wind at a point of the step's start, posted at `at` (−1: not posted), over its own ground.
        Double3 WindAt(int at, in Double3 point) =>
            _windBatch != null && at >= 0 && _windBatch.TryAt(at, point, out Double3 wind) ? wind : _wind.At(point, WindGround(point));

        // The air's velocity at the camera, in its axes (the body's, 0° uptilt): the wind there less the camera's motion,
        // v + ω × r.
        Double3 AirAtCamera(in FlightState s)
        {
            Double3 camera = s.Position + s.Attitude.Rotate(_camera);
            return s.Attitude.InverseRotate(_wind.At(camera, WindGround(camera)) - s.Velocity) - Double3.Cross(s.BodyRate, _camera);
        }

        // The body's and the coil's drag (Aero) on the air's velocity at their centres, into the air's body-axis sums;
        // in rain, the drops' momentum flux on the same silhouettes (RainField), and the water they bring.
        void Drag(in FlightState s, in MassProperties body, ref Double3 force, ref Double3 moment)
        {
            Double3 v = s.Attitude.InverseRotate(s.Velocity), com = body.CenterOfMass;
            Double3 cp = _aero.Spec.BodyCenterOfPressure;
            Double3 a = Air(s, v, cp, _windCp);
            Double3 f = _aero.Body(a);
            force = force + f;
            moment = moment + Double3.Cross(cp - com, f);
            Double3 fall = default, rain = default;
            double flux = 0;
            if (_rain != null)
            {
                // The airframe's velocity through the drops: through the air, plus the drops' fall in body axes.
                fall = s.Attitude.InverseRotate(new Double3(0, _rain.FallSpeed, 0));
                Double3 u = a + fall;
                flux = _aero.BodyFlux(u);
                rain = _rain.Force(u, flux);
                moment = moment + Double3.Cross(cp - com, rain);
            }
            int coil = _aero.Spec.CoilPart;
            if (coil >= 0)
            {
                ref readonly DronePart part = ref _mass.Part(coil);
                a = Air(s, v, part.Position, _windCoil);
                f = _aero.Coil(a, part);
                force = force + f;
                moment = moment + Double3.Cross(part.Position - com, f);
                if (_rain != null)
                {
                    Double3 u = a + fall;
                    double c = Aero.CoilFlux(u, part);
                    f = _rain.Force(u, c);
                    flux += c;
                    rain = rain + f;
                    moment = moment + Double3.Cross(part.Position - com, f);
                }
            }
            if (_rain == null) return;
            force = force + rain;
            _rainMassRate = _rain.WaterContent * flux;
            _trace.RainForce = s.Attitude.Rotate(rain);
        }

        // A body point's velocity through the air, body m/s: the body origin's velocity v (body) plus ω × r, less the
        // wind there.
        Double3 Air(in FlightState s, in Double3 v, in Double3 point, int at) =>
            v + Double3.Cross(s.BodyRate, point) - s.Attitude.InverseRotate(WindAt(at, s.Position + s.Attitude.Rotate(point)));

        // The controller's loops and propulsion's sub-steps for one step; the rotors' mean loads on the body: their
        // thrust and reaction into force (world) and moment, their in-plane drag into the air's sums (body).
        void Propel(ref FlightState s, in StickSample stick, in MassProperties body, ref Double3 force, ref Double3 moment,
            ref Double3 airForce, ref Double3 airMoment)
        {
            for (int i = 0; i < 4; i++)
                _trace.RotorWind[i] = _hubWind[i] = WindAt(_windHub < 0 ? -1 : _windHub + i, s.Position + s.Attitude.Rotate(_propulsion.Hub(i)));
            if (_surfaces != null)
            {
                _surfaces.Find(World, s, out Corners below, out Corners above);
                _propulsion.SetSurfaces(below, above);
                _trace.GroundDistance = below;
                _trace.CeilingDistance = above;
            }
            if (_modes != null)
            {
                Corners elastic = default;
                for (int i = 0; i < 4; i++) elastic[i] = _modes.HubVelocity(s.ModeRate, i);
                _propulsion.SetHubElastic(elastic);
            }
            _propulsion.BeginStep(s, _hubWind);
            _arms?.BeginStep();
            // The gyro on the frame: the rigid body's rate and, with the modes, the frame's elastic turn; its sensor's range
            // clips each axis (FlightSetup.GyroRange, model 72), and the step's largest axis before the clip is traced.
            Axes3 from = s.Fc.GyroPrev, to = Gyro(_modes == null ? s.BodyRate : s.BodyRate + _modes.FrameRate(s.ModeRate));
            if (_gyroRange > 0)
            {
                _trace.GyroPeak = DetMath.Max(DetMath.Abs(to.Roll), DetMath.Max(DetMath.Abs(to.Pitch), DetMath.Abs(to.Yaw)));
                to.Roll = DetMath.Max(-_gyroRange, DetMath.Min(_gyroRange, to.Roll));
                to.Pitch = DetMath.Max(-_gyroRange, DetMath.Min(_gyroRange, to.Pitch));
                to.Yaw = DetMath.Max(-_gyroRange, DetMath.Min(_gyroRange, to.Yaw));
            }
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
                _rotorThrust[i] = thrust;
                Double3 r = _propulsion.Hub(i) - com;
                lift += thrust;
                m.X -= r.Z * thrust;
                m.Z += r.X * thrust;
                amps[i] = loads.SupplyAmps[i] * share;
                Double3 h = _propulsion.HForce(i, speed);
                airForce = airForce + h;
                airMoment = airMoment + Double3.Cross(r, h);
                if (Flapping != null) airMoment = airMoment + _propulsion.HubMoment(i, speed, thrust);
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
                var saturated = (FcFlags)((int)FcFlags.SaturatedRoll << a); // roll, pitch, yaw, collective: bits 6 to 9
                bool now = lost > 0, was = (s.Fc.Flags & saturated) != 0;
                if (now == was) continue;
                FlightEvent e = FlightEvent.Of(EventKind.MixerSaturation);
                e.Index = (byte)a;
                e.A = lost;
                _events.Add(e);
                s.Fc.Flags ^= saturated;
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

            // MotorBound as each motor's drag begins to hold it at rest under drive (p7f-damage 1.5, design 8, D-073) and lets go
            // (A: 0). C: the drive BLHeli_S's low-rpm protection would allow there, its floor of 25/255 (derived, not flown).
            int bound = _propulsion.Bound;
            for (int i = 0; i < 4; i++)
            {
                bool now = (bound & (1 << i)) != 0, was = (_bound & (1 << i)) != 0;
                if (now == was) continue;
                FlightEvent e = FlightEvent.Of(EventKind.MotorBound);
                e.Index = (byte)i;
                e.A = now ? _propulsion.MotorDrag(i) : 0;
                e.B = now ? _propulsion.HeldDrive(i) : 0;
                e.C = now ? _propulsion.TorqueConstant(i) * BlheliSLowRpmFloor * loads.Volts / _propulsion.Resistance(i) : 0;
                _events.Add(e);
            }
            _bound = bound;

            // The arms in their plane (ArmFlex, model 72): each one's largest load this step, and ArmBroken as one breaks at its
            // clamp (A: its load, B: the bend's root stress, Pa, C: the twist's, Pa); its prop is lost from the next step.
            if (_arms != null)
            {
                _trace.ArmLoad = _arms.Peak;
                for (int i = 0; i < 4; i++)
                {
                    if ((_arms.Breaking & (1 << i)) == 0) continue;
                    FlightEvent e = FlightEvent.Of(EventKind.ArmBroken);
                    e.Index = (byte)i;
                    e.A = _arms.Peak[i];
                    e.B = DetMath.Abs(s.ArmBend[i]) * _arms.BendStress;
                    e.C = DetMath.Abs(s.ArmTwist[i]) * _arms.TwistStress;
                    _events.Add(e);
                    _propulsion.LoseProp(i);
                }
            }

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

            // The re-ingested wash moves once a step too (Propulsion.AdvanceWash), from the step's heights and v_h.
            if (_propulsion.HasGround) _propulsion.AdvanceWash(ref s, Dt);

            // And each rotor's own wake (Propulsion.AdvanceWake, D-064), from the step's v_h and in-plane airspeed.
            if (_propulsion.HasWake) _propulsion.AdvanceWake(ref s, Dt);

            // The wakes' interference this step for the record, and their fluctuation moves once a step (Propulsion.AdvanceInterference).
            if (_propulsion.HasInterference)
            {
                _trace.InterferenceInflow = _propulsion.InterferenceInflow;
                _propulsion.AdvanceInterference(ref s, Dt);
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

        // The link's field window (FeedSignals, p7d-tether 1.8): this step's exit tension, least bend radius and margin into the
        // window; a whole field publishes their means, maxima and least, and the state at its last step.
        void LinkField()
        {
            double tension = _tether.ExitTension, bend = _tether.MinBendRadius, margin = _tether.Link.Margin;
            _sumTension += tension;
            _maxTension = DetMath.Max(_maxTension, tension);
            if (bend < _leastBend) _leastBend = bend;
            _sumMargin += margin;
            if (margin < _leastMargin) _leastMargin = margin;
            if (++_linkCount < FieldSteps) return;
            double n = _linkCount;
            _fieldTension = _sumTension / n;
            _fieldTensionMax = _maxTension;
            _fieldBend = _leastBend;
            _fieldMargin = _sumMargin / n;
            _fieldMarginLeast = _leastMargin;
            _fieldLink = _tether.Link.State;
            _linkField = true;
            _sumTension = _maxTension = _sumMargin = 0;
            _leastBend = _leastMargin = double.PositiveInfinity;
            _linkCount = 0;
        }

        /// <summary>The flight controller's failsafe on the line's break (p7d-tether 1.8), or null without the line or its cuts.</summary>
        public Control.Failsafe Failsafe => _failsafe;

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
                CameraTurn = _modes != null ? _modes.FrameTurn(s.ModeShape) : default,
                LegState = _trace.LegState,
                CornerState = _trace.CornerState,
                LegSet = _trace.LegSet,
                SkidFold = _trace.SkidFold,
            };
            Contact.LegDamage legs = _contacts?.Legs;
            if (legs != null)
                for (int c = 0; c < 4; c++) pose.LegSway[c] = legs.Sway(c);
            if (_loose != null)
                for (int i = 0; i < Contact.LoosePieces.Slots; i++)
                {
                    if (!_loose.Active(i)) continue;
                    ref readonly FlightState p = ref _loose.State(i);
                    pose.Loose[i] = new LoosePose { Parts = _loose.PartsOf(i), Position = p.Position, Attitude = p.Attitude, AtRest = _loose.AtRest(i) };
                }
            if (_tether == null) return;
            CableChain line = _tether.Chain;
            int n = line.Count < FiberNodes.Capacity ? line.Count : FiberNodes.Capacity;
            for (int i = 0; i < n; i++) pose.FiberNodes[i] = line[i].Position;
            pose.FiberNodeCount = _tether.Attached ? n : 0;
            pose.FiberTrailCount = _tether.TrailCount;
            pose.FiberPaidOut = _tether.PaidOut;
            pose.FiberBreakTrail = _tether.TrailBeforeBreak;
        }

        /// <summary>The feed signals of the current state (FeedSignals says which are live).</summary>
        public void GetSignals(out FeedSignals signals)
        {
            // The air at the camera's lens (the setup's CameraPosition) in its axes, and the rain's rate (0 when Sunny).
            ref readonly FlightState s = ref _state;
            signals = new FeedSignals
            {
                Live = FeedSignalSet.CameraAngularVelocity | FeedSignalSet.AirVelocityAtCamera | FeedSignalSet.RainRate,
                Step = _state.Step,
                CameraAngularVelocity = _state.BodyRate,
                AirVelocityAtCamera = AirAtCamera(s),
                RainRateMmPerHour = _rain != null ? _rain.RateMmPerHour : 0,
                Crashed = _state.Crashed,
                Sticks = _stick,
            };
            if (_contacts != null)
            {
                signals.Live |= FeedSignalSet.Impact;
                signals.ImpactCount = _contacts.ImpactCount;
                signals.ImpactPeakAccel = _contacts.ImpactPeakAccel;
                signals.ImpactImpulse = _contacts.ImpactImpulse;
            }
            if (_tether != null && _tether.Link != null)
            {
                // The link (p7d-tether 1.8): the field's exit tension (mean and max), least bend radius, margin (mean and least)
                // and state; before the first whole field the current step's. The break and the dips as their steps and counts.
                signals.Live |= FeedSignalSet.FiberTension | FeedSignalSet.FiberMinBendRadius | FeedSignalSet.LinkState | FeedSignalSet.LinkMargin;
                Tether.FiberLink link = _tether.Link;
                signals.FiberTensionN = _linkField ? _fieldTension : _tether.ExitTension;
                signals.FiberTensionMaxN = _linkField ? _fieldTensionMax : _tether.ExitTension;
                signals.FiberMinBendRadiusM = _linkField ? _fieldBend : _tether.MinBendRadius;
                signals.LinkMarginDb = _linkField ? _fieldMargin : link.Margin;
                signals.LinkMarginLeastDb = _linkField ? _fieldMarginLeast : link.Margin;
                signals.LinkState = _linkField ? _fieldLink : link.State;
                signals.LinkDips = link.Dips;
                signals.LinkInDip = link.InDip;
                signals.LinkLossCause = link.InDip ? link.DipKind : link.Largest >= 0 ? _tether.Cuts.Bend(link.Largest).Kind : (byte)0;
                signals.TetherBreakStep = _tether.BrokenAt;
                signals.TetherBreakMechanism = _tether.Broken ? (byte)_tether.LastBreak.Mechanism : (byte)0;
            }
            if (_propulsion == null) return;
            if (_modes != null)
            {
                // The camera on the flexing frame: its elastic turn, and the mount's vibration bands.
                signals.Live |= FeedSignalSet.CameraVibration;
                signals.CameraAngularVelocity = _state.BodyRate + _modes.FrameRate(_state.ModeRate);
                signals.CameraVibration = CameraVibration();
            }
            signals.Live |= FeedSignalSet.MotorElectricalHz | FeedSignalSet.MotorCurrent | FeedSignalSet.MotorCurrentStep
                | FeedSignalSet.PackVolts;
            signals.MotorElectricalHz = _fieldHz;
            signals.MotorCurrentA = _fieldAmps;
            signals.MotorCurrentTotalA = _fieldTotal;
            signals.MotorCurrentTotalMaxA = _fieldTotalMax;
            signals.MotorCurrentStepAPerS = _fieldStepMax;
            signals.PackVolts = _fieldVolts;
            signals.PackLow = _fieldVolts < FeedSignals.LowCellVolts * _propulsion.SeriesCells;
            _strikes?.StemSignals(ref signals); // the props' cut stems (w9-village-physics 2.2)
        }
    }
}
