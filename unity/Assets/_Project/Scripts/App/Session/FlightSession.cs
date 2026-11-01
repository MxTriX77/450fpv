using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.App.Session
{
    /// <summary>A start point (APP-03): where the drone stands, or its launch stand does, and its heading.</summary>
    public struct StartPoint
    {
        public string Name;

        /// <summary>World, m: the drone's (or the stand's) centre on the ground.</summary>
        public double X, Z;

        /// <summary>The heading, rad: positive turns north towards east, as the drone's yaw.</summary>
        public double Yaw;

        /// <summary>The world lists it as a start from the launch stand (legs off); otherwise from the ground. A hint for
        /// the screens and the smoke test: the setup's legs decide what the flight stands on.</summary>
        public bool Stand;

        /// <summary>The floor's one start point until the test range lists its own.</summary>
        public static StartPoint Origin => new StartPoint { Name = "origin" };
    }

    /// <summary>What a flight session is built from: the drone, the pilot's persisted calibration, settings and setup,
    /// the app's radio, and the host's clock and folders.</summary>
    public sealed class SessionInputs
    {
        public DroneSpec Drone;
        public MaterialRow[] Materials;
        public ulong MaterialsHash;

        public FlightChoices Choices = new FlightChoices();
        public FcSettings Settings = FcSettings.Default;

        /// <summary>The active calibration profile, or null for RadioReader's raw map.</summary>
        public CalibrationProfile Calibration;

        /// <summary>The app's radio (its input thread running), or null for none.</summary>
        public RadioReader Reader;

        /// <summary>The keyboard may fly when no radio is connected: development builds only (INP-06).</summary>
        public bool KeyboardAllowed;

        public IHostClock Clock = HostClock.Instance;

        /// <summary>The flight thread runs the steps (the app). False: Tick runs the steps due, on the caller's clock (tests).</summary>
        public bool Threaded = true;

        /// <summary>The logs' folder; null for LogStore.Folder.</summary>
        public string LogFolder;

        public string Build = "";

        /// <summary>A new world per flight: the world-query stub's floor until the test range's queries land.</summary>
        public Func<FlightWorld> World = FlightWorld.Floor;

        public string WorldId = "floor";
        public StartPoint[] StartPoints = { StartPoint.Origin };

        /// <summary>Each flight's seed; null for a random one.</summary>
        public Func<ulong> NewSeed;

        /// <summary>A test's or a bench's change to the setup before the flight is built (a start state, an obstacle);
        /// the log header records the result.</summary>
        public Action<FlightSetup> Adjust;

        /// <summary>
        /// The inputs from the user's profile folder (APP-05): the last setup, the settings and the active calibration.
        /// A file that cannot be read gives its defaults, and its reason goes into problems.
        /// </summary>
        public static SessionInputs FromProfile(string directory, DroneSpec drone, MaterialRow[] materials, ulong materialsHash, List<string> problems)
        {
            var inputs = new SessionInputs { Drone = drone, Materials = materials, MaterialsHash = materialsHash };
            inputs.Choices = SetupStore.Load(directory, out string setupError);
            inputs.Settings = FcSettingsStore.Load(directory, out string settingsError);
            if (setupError != null) problems?.Add(setupError);
            if (settingsError != null) problems?.Add(settingsError);
            try
            {
                inputs.Calibration = CalibrationStore.Load(directory).ActiveProfile;
            }
            catch (Exception e)
            {
                problems?.Add(CalibrationStore.FileName + " could not be read (" + e.Message + "): the radio's raw map is in use.");
            }
            return inputs;
        }
    }

    public enum SessionState { Ready, Flying, Paused, Ended }

    /// <summary>
    /// One pilot's flights over a world (design 6, APP-04), engine-free: a scene drives it once a frame with Tick and
    /// shows Poses. Each flight:
    /// - **is built** from the setup's choices (SessionSetup.Build): the loadout, the wind level, the start point, the
    ///   launch stand as a runtime object with the legs off; the persisted settings; a new seed and a new log
    ///   (LogStore), whose header holds the input's source and the step-0 host time (StartHostUs)
    /// - **flies** on the app's radio, its map the active calibration and its arm switch held off until it is seen off
    ///   (RadioReader.HoldArm); the keyboard only when no radio is connected, and only where it is allowed
    /// - **arms and disarms** by the flight controller's own rules (the switch on with the throttle low)
    /// - **runs** on the flight thread (FlightScheduler) under the 1 ms timer period, which the session holds while it
    ///   lives; Pause stops the stepping and Resume goes on with every later step's host time moved by the pause, logged
    ///   as a Pause hitch. A radio lost during the pause is the failsafe in force for the first RC frame after it, even
    ///   when plugged back in before Resume, so the log has it with its stamp and the end rule sees it
    /// - **ends** on the first of FlightEndRule's conditions, found on the flight thread as each step goes to the log; the
    ///   log stops at that step, so PostFlight finds the same end. Restart, EndFlight and Dispose end it by the pilot.
    /// The session never starts or stops the app's radio (the app does, on quit and on a domain reload); it sets the
    /// reader's inbox for each flight and clears it at the end.
    /// </summary>
    public sealed class FlightSession : IDisposable
    {
        /// <summary>Step 0 comes this long after Start, so the log's file and header are written before it is due.</summary>
        public const long StartLeadUs = 20000;

        /// <summary>The window of report intervals the header records (design 2).</summary>
        public const long IntervalWindowUs = 10000000;

        /// <summary>Steps an RC frame: a loss during a pause is in force this long after Resume, so one frame reads it.</summary>
        public const long RcFrameSteps = FlightController.LoopsPerRcFrame / FlightController.LoopsPerStep;

        readonly SessionInputs _in;
        IDisposable _period;
        FlightCore _core;
        FlightScheduler _scheduler;
        FlightRecorder _log;
        EndProbe _probe;
        StickInbox _inbox;
        KeyboardSticks _keyboard;
        long _startUs, _shiftUs, _pausedAtUs, _hitches;
        bool _stepping, _lostWhilePaused;
        StickSample _pausedLoss;
        long _heldToStep; // the kept loss is held in force at the steps before this one

        public FlightSession(SessionInputs inputs)
        {
            _in = inputs ?? throw new ArgumentNullException(nameof(inputs));
            if (inputs.Drone == null) throw new ArgumentException("the session needs the drone", nameof(inputs));
            if (inputs.Materials == null) throw new ArgumentException("the session needs the physics materials", nameof(inputs));
            if (inputs.Clock == null) throw new ArgumentException("the session needs a clock", nameof(inputs));
            if (inputs.Threaded) _period = TimerPeriod.Begin();
        }

        public SessionState State { get; private set; }

        /// <summary>The drone's pose for the scene to interpolate (PosePair.Interpolate); reused by every flight.</summary>
        public PoseBuffer Poses { get; } = new PoseBuffer();

        /// <summary>The current (or last) flight's seed, log, input source, loadout and start.</summary>
        public ulong Seed { get; private set; }
        public string LogPath { get; private set; }
        public InputSource Input { get; private set; }
        public Loadout Loadout { get; private set; }
        public StartPoint StartPoint { get; private set; }
        public FlightChoices Choices { get; private set; }

        /// <summary>The current (or last) flight's setup and core: read them only while the flight is not stepping.</summary>
        public FlightSetup Setup { get; private set; }
        public FlightCore Core => _core;

        /// <summary>HostClock µs of the flight's step 0, and how far its pauses have moved the later steps.</summary>
        public long StartUs => _startUs;
        public long PausedUs => _shiftUs;

        /// <summary>The flight's late scheduler passes so far (the pauses are not among them).</summary>
        public long Hitches => _hitches + (_stepping ? _scheduler.HitchCount : 0);

        /// <summary>Flights started in this session.</summary>
        public int Flights { get; private set; }

        /// <summary>How the last flight ended (None while one flies).</summary>
        public FlightEnd End { get; private set; }

        /// <summary>The exception that stopped the flight thread, for an end of Fault; the log's failed write, if one did.</summary>
        public Exception Fault { get; private set; }
        public Exception LogFault { get; private set; }

        /// <summary>
        /// Starts a flight from the choices' start point (after a flight has ended, or before the first). Throws when no
        /// radio is connected and the keyboard may not fly.
        /// </summary>
        public void Start()
        {
            if (State == SessionState.Flying || State == SessionState.Paused) throw new InvalidOperationException("a flight is under way");
            RadioReader reader = _in.Reader;
            bool radio = reader != null && reader.Connected;
            if (!radio && !_in.KeyboardAllowed) throw new InvalidOperationException("no radio is connected");

            ulong seed = _in.NewSeed != null ? _in.NewSeed() : RandomSeed();
            FlightSetup setup = SessionSetup.Build(_in, seed, out Loadout loadout, out StartPoint start);
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.SetDrone(_in.Drone, loadout);
            header.Build = _in.Build ?? "";
            header.WorldId = _in.WorldId ?? "";
            header.Input = radio ? reader.Source : InputSource.Keyboard;
            header.InputDevice = radio ? reader.DeviceName : "keyboard";
            header.InputIntervals = radio ? reader.Intervals(IntervalWindowUs) : null;
            long startUs = _in.Clock.NowUs() + StartLeadUs;
            header.StartHostUs = startUs;
            string path = LogStore.NewPath(seed, _in.LogFolder);
            FlightRecorder log = FlightRecorder.Create(path, header);

            _core = core;
            _log = log;
            _probe = new EndProbe(log);
            _inbox = new StickInbox();
            _keyboard = null;
            if (radio)
            {
                reader.Map = _in.Calibration != null ? new CalibratedStickMap(_in.Calibration) : null;
                reader.HoldArm();
                reader.Inbox = _inbox;
            }
            else
            {
                _keyboard = new KeyboardSticks();
            }
            Setup = setup;
            Seed = seed;
            LogPath = path;
            Input = header.Input;
            Loadout = loadout;
            StartPoint = start;
            Choices = _in.Choices.Clone();
            _startUs = startUs;
            _shiftUs = 0;
            _hitches = 0;
            _lostWhilePaused = false;
            _heldToStep = 0;
            End = default;
            Fault = LogFault = null;
            Flights++;
            Step(startUs);
            State = SessionState.Flying;
        }

        /// <summary>
        /// Once a frame on the scene's thread: the keyboard's sample when it flies (keys held now), the steps due when
        /// not threaded, the radio's samples drained while paused, and the end once the flight thread has found it.
        /// </summary>
        public void Tick(KeyboardKeys keys = KeyboardKeys.None)
        {
            if (State != SessionState.Flying && State != SessionState.Paused) return;
            long now = _in.Clock.NowUs();
            if (State == SessionState.Flying)
            {
                if (_keyboard != null)
                {
                    _keyboard.Update(keys, now, out StickSample s);
                    _inbox.TryWrite(s);
                }
                if (!_in.Threaded) _scheduler.RunDue(now);
            }
            else if (_keyboard != null)
            {
                _keyboard.Update(KeyboardKeys.None, now, out _); // the pause's time does not move the throttle
            }
            else if (_inbox.Drain(now, out StickSample lost) && !_lostWhilePaused)
            {
                // The flight thread is stopped: this thread is the inbox's consumer. The first loss is kept for Resume.
                _pausedLoss = lost;
                _lostWhilePaused = true;
            }

            if (_scheduler.Fault != null)
            {
                Fault = _scheduler.Fault;
                Finish(new FlightEnd { Kind = FlightEndKind.Fault, Step = _core.State.Step });
            }
            else if (_probe.Ended)
            {
                Finish(_probe.End);
            }
        }

        /// <summary>Stops the stepping (APP-04); the flight's state stays as it was.</summary>
        public void Pause()
        {
            if (State != SessionState.Flying) return;
            StopStepping();
            _pausedAtUs = _in.Clock.NowUs();
            _lostWhilePaused &= _core.State.Step < _heldToStep; // a loss whose held steps have not all run stays kept
            State = SessionState.Paused;
        }

        /// <summary>
        /// Steps again: every later step's host time moves by the pause, which the log records before the next step as a
        /// Pause hitch. A step that was already due when the pause began is due at once. A loss the paused frames
        /// drained is the sample in force for the first RC frame's steps, whatever reports came after it. Those steps
        /// are counted by index, so a pause among them holds it again for the rest.
        /// </summary>
        public void Resume()
        {
            if (State != SessionState.Paused) return;
            if (_scheduler.Fault != null || _probe.Ended)
            {
                Tick(); // it ended before the pause
                return;
            }
            long now = _in.Clock.NowUs();
            long k = _core.State.Step;
            long start = _startUs + _shiftUs + (now - _pausedAtUs);
            if (start + k * FlightCore.StepUs < now) start = now - k * FlightCore.StepUs;
            long moved = start - _startUs - _shiftUs;
            _shiftUs += moved;
            _log.RecordPause(k + 1, moved);
            if (_lostWhilePaused)
            {
                if (_heldToStep <= k) _heldToStep = k + RcFrameSteps; // a new loss; a kept one keeps its steps
                _inbox.Hold(_pausedLoss, start + _heldToStep * FlightCore.StepUs);
            }
            Step(start);
            State = SessionState.Flying;
        }

        /// <summary>Ends the flight by the pilot and starts a new one from the start point, with a new seed and log.</summary>
        public void Restart()
        {
            EndFlight();
            Start();
        }

        /// <summary>Ends a flight under way by the pilot (quit, back to the menu).</summary>
        public void EndFlight()
        {
            if (State == SessionState.Flying || State == SessionState.Paused)
                Finish(new FlightEnd { Kind = FlightEndKind.Pilot, Step = _core.State.Step });
        }

        /// <summary>The ended flight's summary, from its log (design 7). It reads the whole log, so call it off the frame.</summary>
        public PostFlight ReadSummary()
        {
            if (State != SessionState.Ended) throw new InvalidOperationException("the flight has not ended");
            return PostFlight.Read(LogPath);
        }

        public void Dispose()
        {
            EndFlight();
            _period?.Dispose();
            _period = null;
        }

        // A scheduler for the steps from the core's state on, step 0 at startUs (a resumed flight's moved start).
        void Step(long startUs)
        {
            _scheduler = new FlightScheduler(_core, _inbox, Poses, _in.Clock, _probe);
            if (_in.Threaded) _scheduler.Start(startUs);
            else _scheduler.Begin(startUs);
            _stepping = true;
        }

        void StopStepping()
        {
            _scheduler.Stop();
            _hitches += _scheduler.HitchCount;
            _stepping = false;
        }

        void Finish(FlightEnd end)
        {
            if (_stepping) StopStepping();
            if (_keyboard == null && _in.Reader != null && _in.Reader.Inbox == _inbox) _in.Reader.Inbox = null;
            _log.Close(_inbox.Dropped);
            LogFault = _log.Fault;
            End = end;
            State = SessionState.Ended;
        }

        static ulong RandomSeed()
        {
            var bytes = new byte[8];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }

        // On the flight thread, between the scheduler and the log: each step to the log, then the end rule; once the
        // flight has ended nothing more is logged, so the log stops at the end's step.
        sealed class EndProbe : IFlightRecorder
        {
            readonly FlightRecorder _log;
            readonly FlightEndRule _rule = new FlightEndRule();
            volatile bool _ended;

            public EndProbe(FlightRecorder log) => _log = log;

            public bool Ended => _ended;
            public FlightEnd End => _rule.End;

            public void RecordStep(in FlightState state, in StickSample stick, in StepTrace trace, StepEvents events)
            {
                if (_ended) return;
                _log.RecordStep(state, stick, trace, events);
                if (_rule.Step(state, stick, trace)) _ended = true;
            }

            public void RecordHitch(in Hitch hitch)
            {
                if (!_ended) _log.RecordHitch(hitch);
            }
        }
    }

    /// <summary>The setup's choices as a flight (APP-03, design 6).</summary>
    public static class SessionSetup
    {
        /// <summary>
        /// The FlightSetup of inputs' choices: drone.json's mass, propulsion, aero and contacts at the loadout (the
        /// payload, the legs, the coil full at take-off), the wind level, the settings, a new world, and the start point:
        /// legs on, the drone standing on the ground there, its skids unloaded; legs off, the launch stand placed there
        /// as a runtime object and the drone resting on its posts; both at the start point's heading.
        /// </summary>
        public static FlightSetup Build(SessionInputs inputs, ulong seed, out Loadout loadout, out StartPoint start)
        {
            FlightChoices c = inputs.Choices ?? throw new ArgumentException("the session needs the setup's choices", nameof(inputs));
            string problem = c.Check();
            if (problem != null) throw new ArgumentException(problem, nameof(inputs));
            DroneSpec spec = inputs.Drone;
            loadout = new Loadout { Payload = c.Payload, Legs = c.Legs, CoilPaidOut = 0 };
            start = Find(inputs.StartPoints, c.StartPoint);
            FlightWorld world = inputs.World();
            ContactSpec contacts = spec.BuildContacts(loadout, inputs.Materials, inputs.MaterialsHash);
            var foot = new Double3(start.X, GroundAt(world, start), start.Z);
            DetMath.SinCos(start.Yaw / 2, out double sin, out double cos);
            var setup = new FlightSetup
            {
                Seed = seed,
                Settings = inputs.Settings,
                World = world,
                Mass = spec.BuildMass(loadout),
                Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(),
                Wind = WindSetup.Of(c.Wind),
                Contacts = contacts,
                StartAttitude = new Quat(0, sin, 0, cos),
            };
            if (c.Legs)
            {
                setup.StartPosition = foot + new Double3(0, Standing(contacts), 0);
            }
            else
            {
                setup.StartPosition = LaunchStand.RestingOrigin(spec, contacts, foot);
                setup.RuntimeObjects = new[] { LaunchStand.Build(spec, foot, start.Yaw, world.Query.MaterialId(spec.Stand.Material)) };
            }
            inputs.Adjust?.Invoke(setup);
            return setup;
        }

        /// <summary>The body origin's height over the ground of the legs-on drone standing on its skids, unloaded.</summary>
        public static double Standing(ContactSpec contacts)
        {
            double low = 0;
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Support)
                    low = Math.Min(low, Math.Min(s.A.Y, s.B.Y) - s.Radius);
            return -low;
        }

        static StartPoint Find(StartPoint[] points, string name)
        {
            if (points == null || points.Length == 0) throw new ArgumentException("the world lists no start point");
            if (string.IsNullOrEmpty(name)) return points[0];
            foreach (StartPoint p in points)
                if (p.Name == name)
                    return p;
            return points[0]; // a start point the world no longer has: its first
        }

        static double GroundAt(FlightWorld world, StartPoint start)
        {
            var results = new GroundSample[1];
            world.SampleGround(new[] { new XZ(start.X, start.Z) }, results);
            double h = results[0].Height;
            if (double.IsInfinity(h) || double.IsNaN(h)) throw new ArgumentException("the start point '" + start.Name + "' has no ground");
            return h;
        }
    }

    /// <summary>The host's 1 ms timer period while a flight may run (p6-flight-core 1.2's note), so the flight thread's
    /// 1 ms sleeps last about 1 ms; nothing elsewhere.</summary>
    static class TimerPeriod
    {
        public static IDisposable Begin() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new Period() : null;

        sealed class Period : IDisposable
        {
            [DllImport("winmm.dll")]
            static extern uint timeBeginPeriod(uint period);

            [DllImport("winmm.dll")]
            static extern uint timeEndPeriod(uint period);

            int _held = 1;

            public Period() => timeBeginPeriod(1);

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _held, 0) == 1) timeEndPeriod(1);
            }
        }
    }
}
