using System;
using Fpv450.Sim.Control;

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

        /// <summary>The initial state. A flight starts at rest; a bench may launch a free body.</summary>
        public Double3 StartPosition;
        public Quat StartAttitude = Quat.Identity;
        public Double3 StartVelocity;
        public Double3 StartBodyRate;
    }

    /// <summary>
    /// The flight model (PHB-01): engine-free, a fixed 1 ms step, semi-implicit Euler (K§6.2). A step is a function of
    /// the state, the stick sample in force, the flight's setup and the world queries only (no clock, thread, hash
    /// code or collection order), so a flight replays bit for bit from its log. The FlightScheduler runs it on its
    /// own thread; a replay or a bench calls Advance directly.
    ///
    /// The body is RigidBody's, with the mass, centre of mass and inertia of the setup's MassModel (1.3). The forces
    /// and moments arrive with the later tasks: the controller and propulsion (1.4), aero and the stand-in wind (1.5),
    /// contacts (1.6). Until then a body is a free one: gravity, and the torque-free motion of its inertia.
    /// </summary>
    public sealed class FlightCore
    {
        /// <summary>The fixed step, s (PHB-01). Never varied.</summary>
        public const double Dt = 0.001;

        /// <summary>The fixed step, µs of host time.</summary>
        public const long StepUs = 1000;

        /// <summary>World, m/s²: standard gravity, 9.806 65 (published, CGPM 1901), straight down.</summary>
        public static readonly Double3 Gravity = new Double3(0, -9.80665, 0);

        FlightState _state;
        FcSettings _settings;
        readonly MassModel _mass;

        public FlightCore(FlightSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            World = setup.World ?? throw new ArgumentException("the setup needs a world", nameof(setup));
            _settings = setup.Settings;
            _mass = (setup.Mass ?? throw new ArgumentException("the setup needs a mass model", nameof(setup))).Clone();
            Random = new FlightRandom(setup.Seed);
            _state = new FlightState
            {
                Position = setup.StartPosition,
                Velocity = setup.StartVelocity,
                Attitude = setup.StartAttitude.Normalized(),
                BodyRate = setup.StartBodyRate,
            };
        }

        public ref readonly FlightState State => ref _state;
        public ref readonly FcSettings Settings => ref _settings;
        public FlightWorld World { get; }

        /// <summary>The body's mass, centre of mass and inertia now (design 3).</summary>
        public ref readonly MassProperties Body => ref _mass.Properties;

        /// <summary>The flight's seed, streams and per-flight draws (design 9).</summary>
        public FlightRandom Random { get; }

        /// <summary>One 1 ms step with the stick sample in force (StickSample, "the timing contract").</summary>
        public void Advance(in StickSample stick)
        {
            ref FlightState s = ref _state;
            s.Armed = stick.Arm;

            // The net force (world, N) and the net moment about the CoM (body, N·m): none until 1.4–1.6.
            ref readonly MassProperties body = ref _mass.Properties;
            Double3 force = default;
            Double3 moment = default;

            // Semi-implicit Euler (RigidBody): the velocities first, then the positions with the new velocities.
            RigidBody.Step(ref s, in body, Gravity + force * body.InverseMass, moment, Dt);
            s.Step++;
        }

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
            };
        }

        /// <summary>The feed signals of the current state (FeedSignals says which are live).</summary>
        public void GetSignals(out FeedSignals signals)
        {
            signals = new FeedSignals
            {
                Live = FeedSignalSet.CameraAngularVelocity,
                Step = _state.Step,
                CameraAngularVelocity = _state.BodyRate,
            };
        }
    }
}
