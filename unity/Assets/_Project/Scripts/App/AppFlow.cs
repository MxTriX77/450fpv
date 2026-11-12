using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Input;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using Fpv450.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using StartPoint = Fpv450.App.Session.StartPoint;

namespace Fpv450.App
{
    /// <summary>
    /// The app (design 6, APP-02…05): the menu (Train, Calibrate, Exit; Explore in a review build), the setup, the
    /// Calibrate screen, and the flight over the farmland (FlightScene; w3-farmland-ground 3.1). It lives across scenes: Boot
    /// holds the menu, FarmLand the world. Train → the setup → Fly saves the setup and loads the farmland, and the flight
    /// starts at the village's start once the radio is in, so arming the switch is the only step left. The summary's Fly
    /// again and Setup stay on the farmland. Explore opens the farmland in noclip (FarmlandExplore). The test range left the
    /// player; the headless tests and the benches keep it.
    /// </summary>
    /// <remarks>
    /// Options: <c>-windowed</c> (a 1920 × 1080 window), <c>-vsync &lt;n&gt;</c> (as the range's boot), <c>-quality &lt;name&gt;</c>
    /// (a preset, saved for the next start: QualityPreset). <c>-smoketest</c>
    /// walks the app (Smoke) and quits, exit code 1 on any failed check or any error logged (on D3D12 it reads its own log
    /// file for the pipeline library's line, so not with <c>-logFile -</c>); <c>-smokeshots &lt;folder&gt;</c>
    /// saves its screens there, and <c>-smokeseq &lt;n&gt;</c> beside them n consecutive frames at the punch-outs and the
    /// hover, FPV and chase (Sequence). <c>-camerafeel 0</c> flies the camera as before p7e-camera-feel (FlightScene.CameraFeel).
    /// <c>-smokewind &lt;Calm|Windy|Severe&gt;</c> flies the smoke test's flights in that wind (Calm by default), a look at
    /// the wind field in the player: its checks are Calm's. <c>-smokelegs</c> flies LegShots' damaged-leg flights in place of
    /// the smoke test's flights, each shot in both views, then R's restart (p7g-legs 1.5). <c>-smokehills</c> flies the
    /// two-minute flight over the farmland's hills in their place (HillsFlight, w3-farmland-ground 3.1).
    /// </remarks>
    public sealed class AppFlow : MonoBehaviour
    {
        public const string BootScene = "Boot";

        enum View { Menu, Setup, Calibrate, Loading, Flight, Explore }

        static AppFlow _instance;
        View _view;
        bool _explore;
        readonly SetupScreen _setup = new SetupScreen();
        CalibrateScreen _calibrate;
        FlightScene _flight;
        string _profile;
        int _errors;
        string _smokeLogs; // the smoke test's flight logs, never in the pilot's folder
        string _sequences; // -smokeseq's folder, and its frames a sequence
        int _sequenceFrames;
        LegShots.Case _legCase; // -smokelegs: the next flight's start, or null for a flight that stands

        /// <summary>Boot's: makes the app once; the Boot scene loads again for the menu and finds it.</summary>
        public static void Create()
        {
            if (_instance != null) return;
            string[] args = Environment.GetCommandLineArgs();
            if (!Application.isEditor) QualityPreset.Apply(args, Application.persistentDataPath); // before the vsync: a preset sets its own
            int at = Array.IndexOf(args, "-vsync");
            QualitySettings.vSyncCount = at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int vsync) ? vsync : 1;
            if (Array.IndexOf(args, "-windowed") >= 0) Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            else if (!Application.isEditor) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            var go = new GameObject("App");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AppFlow>();
        }

        void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.logMessageReceivedThreaded += OnLog;
            _setup.Starts = FlightWorlds.FarmlandStarts;
            ReadProfile();
            Debug.Log("[App] menu: Train, Calibrate, " + (AppData.Review ? "Explore, " : "") + "Exit (" + (AppData.Review ? "a review build" : "the pilot's build")
                      + "); data under " + AppData.Root + "; profile folder " + Application.persistentDataPath);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-smoketest") >= 0) StartCoroutine(Smoke());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceivedThreaded -= OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) System.Threading.Interlocked.Increment(ref _errors);
        }

        // ---- The flow ----

        void Train()
        {
            _setup.Choices = SetupStore.Load(Application.persistentDataPath, out string error);
            _setup.Notes.Clear();
            if (error != null) _setup.Notes.Add(error);
            _view = View.Setup;
        }

        void Fly()
        {
            FlightChoices choices = _setup.Choices.Clone();
            try
            {
                SetupStore.Save(Application.persistentDataPath, choices);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[App] the setup could not be saved: " + e.Message);
            }
            Debug.Log("[App] fly: " + SetupStore.ToJson(choices).Replace("\n", " "));
            if (_flight != null && _flight.Host.Inputs != null)
            {
                _flight.Host.Inputs.Choices = choices;
                _flight.Covered = false;
                _view = View.Flight;
                _flight.Host.Fly();
                return;
            }
            Load(explore: false);
        }

        // The farmland, for a flight (w3-farmland-ground 3.1) or in noclip (Explore, w1-farmland-world 2.1).
        void Load(bool explore)
        {
            _explore = explore;
            _view = View.Loading;
            _exploreAsked = Time.realtimeSinceStartup;
            _exploreFirstFrame = -1f;
            SceneManager.LoadSceneAsync(FarmlandExplore.Scene, LoadSceneMode.Single);
        }

        float _exploreAsked, _exploreFirstFrame = -1f; // realtime s: Explore's press, and its first noclip frame after it
        Transform _exploreStand;

        // PLT-06: the seconds from the menu's Explore to the farmland's first noclip frame on screen.
        IEnumerator FirstExploreFrame()
        {
            yield return new WaitForEndOfFrame();
            _exploreFirstFrame = Time.realtimeSinceStartup - _exploreAsked;
            Debug.Log("[App] explore: the farmland's first noclip frame " + _exploreFirstFrame.ToString("0.00") + " s after the menu's Explore (PLT-06: 20 s)");
        }

        void Menu()
        {
            _flight = null;
            _view = View.Loading;
            SceneManager.LoadSceneAsync(BootScene, LoadSceneMode.Single);
        }

        void Calibrate()
        {
            _calibrate = gameObject.AddComponent<CalibrateScreen>();
            _calibrate.Reader = AppRadio.Reader;
            _calibrate.Closed += () =>
            {
                Destroy(_calibrate);
                _calibrate = null;
                ReadProfile();
                _view = View.Menu;
            };
            _view = View.Calibrate;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == FarmlandExplore.Scene && _explore)
            {
                _exploreStand = FarmlandExplore.Open(scene);
                _view = View.Explore;
                Debug.Log("[App] explore: the farmland in noclip with the feed (V, O), from the village's start '" + FlightWorlds.FarmlandStart.Name +
                          "' with the launch stand" + (_exploreStand != null ? " at " + _exploreStand.position.ToString("0.00") : " MISSING") + "; Esc for the menu");
                StartCoroutine(FirstExploreFrame());
            }
            else if (scene.name == FarmlandExplore.Scene)
            {
                _flight = FlightScene.Attach(scene);
                if (_flight.Host.Inputs != null)
                {
                    _flight.Host.Inputs.Choices = _setup.Choices.Clone();
                    if (_smokeLogs != null)
                    {
                        _flight.Host.Inputs.LogFolder = _smokeLogs;
                        LegCase(_flight.Host.Inputs);
                        // The player's default rates and throttle (D-066), never the pilot's saved ones: its checks and
                        // its keyboard pilot assume a curve.
                        _flight.Host.Inputs.Settings = FcSettingsStore.PlayerDefault;
                    }
                }
                _flight.SetupRequested += () =>
                {
                    if (_flight.Host.Inputs != null) _setup.Choices = _flight.Host.Inputs.Choices.Clone();
                    _setup.Notes.Clear();
                    _flight.Covered = true;
                    _view = View.Setup;
                };
                _flight.MenuRequested += Menu;
                // The loading screen stays up while the host prepares the flight off the frame (Update ends it).
                _flight.Covered = _flight.Host.Preparing;
                _view = _flight.Host.Preparing ? View.Loading : View.Flight;
            }
            else if (scene.name == BootScene)
            {
                _flight = null;
                if (_view == View.Loading) _view = View.Menu;
            }
        }

        void Update()
        {
            if (_view == View.Loading && _flight != null && !_flight.Host.Preparing)
            {
                _flight.Covered = false;
                _view = View.Flight;
            }
            Keyboard k = Keyboard.current;
            if (k == null || !k.escapeKey.wasPressedThisFrame) return;
            if (_view == View.Explore) Menu();
            else if (_view == View.Setup) Back();
            else if (_view == View.Flight && _flight != null) _flight.Escape();
        }

        void Back()
        {
            if (_flight != null)
            {
                _flight.Covered = false;
                _view = View.Flight;
            }
            else _view = View.Menu;
        }

        void ReadProfile()
        {
            try
            {
                _profile = CalibrationStore.Load(Application.persistentDataPath).ActiveProfile?.Name;
            }
            catch (Exception)
            {
                _profile = null;
            }
        }

        string RadioLine()
        {
            RadioReader r = AppRadio.Reader;
            if (!r.Connected) return "No radio: plug the TX12 in by USB, in joystick mode";
            return r.DeviceName + (_profile != null ? "  ·  profile " + _profile : "  ·  not calibrated yet");
        }

        void OnGUI()
        {
            switch (_view)
            {
                case View.Menu:
                    switch (MenuScreen.Draw(AppData.Review, RadioLine(), "build " + Application.version + (AppData.Review ? "  ·  review" : "")))
                    {
                        case MenuChoice.Train: Train(); break;
                        case MenuChoice.Calibrate: Calibrate(); break;
                        case MenuChoice.Explore: Load(explore: true); break;
                        case MenuChoice.Exit: Application.Quit(); break;
                    }
                    break;
                case View.Setup:
                    switch (_setup.Draw(_flight != null))
                    {
                        case SetupChoice.Fly: Fly(); break;
                        case SetupChoice.Back: Back(); break;
                    }
                    break;
                case View.Loading:
                    AppStyle.Begin();
                    AppStyle.DrawGround();
                    GUI.Label(new Rect(AppStyle.S(150), Screen.height * 0.45f, Screen.width, AppStyle.S(40)), _explore ? "Loading the farmland…  noclip" : "Loading the farmland…  arm once the drone is in", AppStyle.Label);
                    break;
            }
        }

        // ---- The smoke test ----

        /// <summary>
        /// The player's smoke test, through the screens' own calls: the menu and the setup drawn; Explore in a review build;
        /// then two flights over the farmland from the village's start, the one the setup offers (D-092, w3-farmland-ground
        /// 3.1; on the test range until then, from its houses' start): on the legs, then from the launch
        /// stand (legs off) in Rain, the second set from the first's summary through Setup. Each flight carries
        /// surfaces.json's ground (the rain's at its wet set), the map's cuts and the fiber line, which draws in the air
        /// and is laid again by a restart, and the props' blade tables and strikes, whole at each start (D-079). With the development
        /// keyboard each flight is flown by a script of its keys: armed, climbed, paused (no step while paused),
        /// restarted (the stand's, before arming, from the pause: a new seed and log), the throttle down, disarmed in the
        /// air with no end and no summary (D-062), re-armed, flown on, ended from the pause's End flight, and the summary
        /// read from the log. The keyboard is barred twice, as in a pilot's build without the radio: the stand's flight,
        /// set from the first's summary, waits at its start; its Restart ends it and waits. A pilot's build has no keyboard:
        /// there each flight is checked waiting for the radio. The flights log to the smoke test's own folder.
        /// V is pressed through the Input System (D-055) for each view's shot: at rest, in the air and after the crash, or
        /// while waiting for the radio; the keyboard's flights, flown with those presses, must replay bit for bit. The OSD
        /// (p4-feed 1.5, D-057) reads DISARMED while waiting and at rest, and AIR in the air with the current, the charge
        /// used and the pack, shot in the FPV view. Then the keyboard's flight into the ground (into a house on the test range,
        /// which the farmland's sim does not have until its objects, w3-farmland-ground 1.2): the crash ends nothing, and R
        /// restarts it. <c>-smokehills</c> flies HillsFlight in place of the flights.
        /// </summary>
        IEnumerator Smoke()
        {
            string[] args = Environment.GetCommandLineArgs();
            // A radio left plugged in would fly the scripted flights: the smoke test reads none.
            AppRadio.Unplug();
            Debug.Log("[Smoke] the radio is not read: the flights fly on the development keyboard, or wait for the radio");
            int at = Array.IndexOf(args, "-smokeshots");
            string shots = at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
            if (shots != null) Directory.CreateDirectory(shots);
            // Its flights log to a folder of its own (QA's note): beside the shots, or in the app's temporary folder.
            _smokeLogs = shots != null ? Path.Combine(shots, "flights") : Path.Combine(Application.temporaryCachePath, "smoketest-flights");
            Debug.Log("[Smoke] flight logs in " + _smokeLogs);
            // -smokeseq <n>: n consecutive frames at the punch and the hover, FPV and chase, and in the crash flight's yaw
            // and pitch (p7e-camera-feel 1.7's before and after, with -camerafeel 0 for the before), beside the shots.
            int seq = Array.IndexOf(args, "-smokeseq");
            if (shots != null && seq >= 0 && seq + 1 < args.Length && int.TryParse(args[seq + 1], out _sequenceFrames) && _sequenceFrames > 0)
                _sequences = Path.Combine(shots, "seq");
            int windAt = Array.IndexOf(args, "-smokewind");
            WindLevel wind = WindLevel.Calm;
            if (windAt >= 0 && (windAt + 1 >= args.Length || !Enum.TryParse(args[windAt + 1], true, out wind) ||
                                (wind != WindLevel.Calm && wind != WindLevel.Windy && wind != WindLevel.Severe)))
            {
                Debug.LogError("[Smoke] -smokewind is Calm, Windy or Severe");
                wind = WindLevel.Calm;
            }
            Debug.Log("[Smoke] the flights' wind: " + wind);
            // The smoke test's flights save their setup; the pilot's own is put back at the end.
            string setupFile = SetupStore.PathIn(Application.persistentDataPath);
            byte[] pilotSetup = File.Exists(setupFile) ? File.ReadAllBytes(setupFile) : null;
            int failed = 0, shot = 0;
            string Osd() => "'" + _flight.Osd.Row(0).TrimEnd() + "' / '" + _flight.Osd.Row(11).TrimEnd() + "' / '" + _flight.Osd.Row(12).TrimEnd() +
                            "' / '" + _flight.Osd.Row(13).TrimEnd() + "'";
            void Check(bool ok, string what)
            {
                Debug.Log("[Smoke] " + (ok ? "ok   " : "FAIL ") + what);
                if (!ok) failed++;
            }
            IEnumerator Shot(string name)
            {
                if (shots == null) yield break;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(shots, (++shot).ToString("00") + "_" + name + ".png"));
                yield return null;
                yield return null;
            }
            IEnumerator Until(Func<bool> done, float seconds)
            {
                float end = Time.realtimeSinceStartup + seconds;
                while (!done() && Time.realtimeSinceStartup < end) yield return null;
            }
            IEnumerator Hold(KeyboardKeys keys, float seconds, Action each = null)
            {
                FlightSessionHost host = _flight.Host;
                host.Keys = () => keys;
                float end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    each?.Invoke();
                }
                host.Keys = () => KeyboardKeys.None;
                yield return null;
            }
            // D-055: V pressed through the Input System, as the pilot's key: while it is held the flight's keys
            // (KeyboardFallback's) stay none, and once the scene has read it the camera has switched views, the feed on it.
            IEnumerator PressV(string where)
            {
                bool chase = !_flight.Chase;
                Keyboard k = Keyboard.current ?? InputSystem.AddDevice<Keyboard>("Smoke keyboard");
                InputSystem.QueueStateEvent(k, new KeyboardState(FlightScene.ViewKey));
                yield return null; // the frame's input update has it; its LateUpdate reads it
                Check(k[FlightScene.ViewKey].isPressed && KeyboardFallback.ReadKeys() == KeyboardKeys.None, "V held " + where + ": the flight's keys are none");
                InputSystem.QueueStateEvent(k, new KeyboardState());
                yield return null;
                Transform eye = _flight.Eye; // not Camera.main: the FPV view's camera is off between fields (p7e-camera-feel)
                FpvCamera feed = eye != null ? eye.GetComponent<FpvCamera>() : null;
                bool feedOn = feed != null && feed.Running;
                if (chase)
                {
                    // From the drone: up, and back along its heading, whatever its attitude.
                    Vector3 d = eye != null ? eye.position - eye.parent.position : Vector3.zero;
                    Vector3 back = eye != null ? Vector3.ProjectOnPlane(d, Vector3.up) : Vector3.zero;
                    Check(_flight.Chase && Math.Abs(d.y - FlightScene.ChaseOffset.y) < 0.01 && Math.Abs(back.magnitude + FlightScene.ChaseOffset.z) < 0.01 &&
                          Vector3.Dot(back, Vector3.ProjectOnPlane(eye.parent.forward, Vector3.up)) < 0 && feedOn,
                          "V " + where + ": the 3rd-person view, " + back.magnitude.ToString("0.00") + " m behind and " + d.y.ToString("0.00") +
                          " m above the drone, the feed " + (feedOn ? "on" : "OFF"));
                }
                else
                    Check(!_flight.Chase && eye != null && (eye.localPosition - _flight.CameraMount).magnitude < 1e-4f && Quaternion.Angle(eye.localRotation, Quaternion.identity) < 0.01f && feedOn,
                          "V again " + where + ": the FPV camera at the lens, the feed " + (feedOn ? "on" : "OFF"));
            }
            // R pressed through the Input System, as the pilot's key (D-078, D-087): the flight restarts in the frame that
            // reads the press, in whatever state it was, as a new flight from the same setup and start: step 0 on a whole
            // line, the link intact, the failsafe cleared, its props whole and its legs intact, the feed's picture back. Its pair
            // is this flight's by FlightScene's test: a drop in its first frames (p7h-step-budget 2.1) moves its start later.
            IEnumerator PressR(string where)
            {
                FlightSession session = _flight.Host.Session;
                int flights = session.Flights;
                FlightChoices before = session.Choices;
                Double3 from = session.Setup.StartPosition;
                Keyboard k = Keyboard.current ?? InputSystem.AddDevice<Keyboard>("Smoke keyboard");
                InputSystem.QueueStateEvent(k, new KeyboardState(FlightScene.RestartKey));
                yield return null; // the frame that reads the press; its LateUpdate restarts
                int read = Time.frameCount;
                yield return new WaitForEndOfFrame();
                bool atOnce = session.Flights == flights + 1 && _flight.ShownFlight == session.Flights;
                InputSystem.QueueStateEvent(k, new KeyboardState());
                yield return Until(() => session.Flights == flights + 1 && _flight.ShownFlight == session.Flights, 10);
                int frames = Time.frameCount - read;
                FlightCore core = session.Core;
                PosePair pair = session.Poses.Read(); // a copy: an iterator holds no ref
                bool whole = core.Propulsion.Rotor(0) != null;
                for (int rotor = 0; whole && rotor < 4; rotor++)
                    for (int blade = 0; blade < core.Propulsion.Rotor(rotor).Blades; blade++)
                        whole &= core.Propulsion.Rotor(rotor).Blade(blade).Whole;
                FpvCamera feed = _flight.Eye != null ? _flight.Eye.GetComponent<FpvCamera>() : null;
                FeedLoss.State shown = feed != null && feed.Running ? feed.Events.Loss.Current : FeedLoss.State.Black;
                FlightChoices c = session.Choices;
                Check(atOnce && session.State == SessionState.Flying && pair.StartUs >= session.StartUs && pair.Signals.LinkState == Fpv450.Sim.Flight.LinkState.Intact &&
                      core.Failsafe != null && !core.Failsafe.Active && core.Tether != null && !core.Tether.Broken && whole &&
                      pair.Current.LegState == 0 && pair.Current.CornerState == 0 && shown == FeedLoss.State.Picture &&
                      c.StartPoint == before.StartPoint && c.Legs == before.Legs && c.Payload == before.Payload && c.Wind == before.Wind && c.Rain == before.Rain &&
                      c.TimeOfDay == before.TimeOfDay && session.Setup.StartPosition.Equals(from),
                      "R " + where + ": flight " + session.Flights + (atOnce ? " in the frame that read the press" : " " + frames + " frames after it") +
                      ", from '" + c.StartPoint + "' at step " + pair.Current.Step + ": the link " + pair.Signals.LinkState + ", the failsafe " +
                      (core.Failsafe != null ? (core.Failsafe.Active ? "ACTIVE" : "clear") : "none") + ", the line " + (core.Tether != null && !core.Tether.Broken ? "whole" : "BROKEN") +
                      ", props " + (whole ? "whole" : "DAMAGED") + ", legs " + (pair.Current.LegState == 0 && pair.Current.CornerState == 0 ? "intact" : "DAMAGED") + ", the feed " + shown);
                yield return Shot("restart_" + (where.StartsWith("after") ? "after_break" : "from_summary"));
            }
            // The fiber's tube by name (FiberView's FiberSpan and FiberTrail0), drawn or not.
            MeshRenderer Tube(string name)
            {
                Transform t = _flight.Fiber != null ? _flight.Fiber.transform.Find(name) : null;
                return t != null ? t.GetComponent<MeshRenderer>() : null;
            }
            // A flight flown with V presses replays bit for bit on the world it names (D-055: the log never sees V).
            IEnumerator Replays(string log, string where, float seconds = 60)
            {
                SessionInputs inputs = _flight.Host.Inputs;
                Task<ReplayResult> replay = Task.Run(() =>
                {
                    using (var f = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        return FlightReplay.Run(f, id => id == inputs.WorldId ? inputs.World() : null, inputs.Drone.ContentHash,
                            surfacesHash: inputs.Surfaces?.Hash ?? 0, tetherHash: inputs.Tether?.ContentHash ?? 0, materialsHash: inputs.MaterialsHash);
                });
                yield return Until(() => replay.IsCompleted, seconds);
                ReplayResult r = replay.IsCompleted && !replay.IsFaulted ? replay.Result : null;
                Check(r != null && r.Outcome == ReplayOutcome.Match, "the flight " + where + ", flown with V presses, replays bit for bit: " +
                      (r != null ? r.Message : replay.IsFaulted ? replay.Exception.GetBaseException().Message : "not done in " + seconds + " s"));
            }
            // -smokelegs (p7g-legs 1.5): LegShots' flights in place of the flights below, each shot in the FPV and chase views
            // once its leg shows its state, and its summary; then R's restart from the last, standing on four intact legs.
            // The view as rendered, the feed off for the frame (F's toggle), to see the legs at the screen's resolution.
            IEnumerator CleanShot(string name)
            {
                _flight.Pressed(false, true);
                yield return null;
                yield return Shot(name);
                _flight.Pressed(false, true);
                yield return null;
            }
            IEnumerator LegFlights(StartPoint start)
            {
                for (int i = 0; i < LegShots.Cases.Length; i++)
                {
                    LegShots.Case leg = LegShots.Cases[i];
                    FlightChoices c = _setup.Choices;
                    c.StartPoint = start.Name;
                    c.Legs = true;
                    c.Payload = 1.0;
                    c.Wind = WindLevel.Calm;
                    c.Rain = Rain.Sunny;
                    c.TimeOfDay = 12.0;
                    _legCase = leg;
                    if (_flight != null && _flight.Host.Inputs != null) LegCase(_flight.Host.Inputs);
                    Fly();
                    yield return Until(() => _view == View.Flight && _flight != null && _flight.Host.Session != null &&
                                             _flight.Host.Session.State == SessionState.Flying && _flight.ShownFlight == _flight.Host.Session.Flights &&
                                             _flight.Host.Session.Seed == leg.Seed, 60);
                    FlightSessionHost host = _flight.Host;
                    FlightSession session = host.Session;
                    yield return Until(() => _flight.Pose.TimeS >= leg.ShotS, 30);
                    PoseFrame pose = _flight.Pose;
                    Check(LegShots.Shows(leg, pose), "the " + leg.Name + " flight (seed " + session.Seed + ") at " + pose.TimeS.ToString("0.00") + " s: " + LegShots.Describe(pose));
                    yield return Shot("legs_" + leg.Name + "_fpv");
                    yield return PressV("with the " + leg.Name + " leg");
                    yield return Shot("legs_" + leg.Name + "_chase");
                    yield return CleanShot("legs_" + leg.Name + "_chase_clean");
                    if (i == LegShots.Cases.Length - 1)
                    {
                        // R's restart, from the pause: the next flight stands at the start, its legs intact.
                        _legCase = null;
                        int flights = session.Flights;
                        host.Pause();
                        host.Restart();
                        yield return Until(() => session.Flights == flights + 1 && session.State == SessionState.Flying && _flight.ShownFlight == session.Flights, 30);
                        yield return new WaitForSecondsRealtime(1);
                        PoseFrame again = _flight.Pose;
                        bool unbent = true;
                        for (int k = 0; k < 4; k++) unbent &= again.LegSet[k].Equals(default(Double3));
                        Check(session.Flights == flights + 1 && again.LegState == 0 && again.CornerState == 0 && unbent && again.Loose[0].Parts == 0,
                              "R's restart stands on four intact legs: " + LegShots.Describe(again));
                        yield return Shot("legs_restart_chase");
                        yield return CleanShot("legs_restart_chase_clean");
                    }
                    yield return PressV("with the " + leg.Name + " leg");
                    host.Pause();
                    session.EndFlight(); // the pause's End flight
                    yield return Until(() => _flight.Summary != null, 30);
                    PostFlight summary = _flight.Summary;
                    Check(summary != null && (i == LegShots.Cases.Length - 1 ? summary.Legs.Count == 0 : summary.Legs.Count > 0),
                          "the summary's legs: " + (summary != null ? string.Join(" | ", summary.Legs.ConvertAll(l => summary.Describe(l))) : "none"));
                    if (i < LegShots.Cases.Length - 1) yield return Shot("legs_" + leg.Name + "_summary");
                    _setup.Choices = host.Inputs.Choices.Clone();
                    _flight.Covered = true;
                    _view = View.Setup; // the summary's Setup
                }
                _legCase = null;
            }
            // -smokehills (w3-farmland-ground 3.1, the spec's "a flight on the hills replays", the pilot not being there to fly
            // it): two minutes over the farmland's hills from the village's start, legs on, 1 kg, Windy, the line paying out,
            // flown by HillsPilot on the development keyboard with the sim's ground. The drawn terrain is measured against the
            // sim's near the drone (TerrainParity) at rest, every 20 s of the cruise and after the landing, at the scene's pixel
            // error and at the flight's near the drone (FlightScene.NearTerrain). The flight is ended from the pause, its
            // summary read, its log replayed bit for bit and read for the ground (GroundCheck: no tunnel, how deep any shape
            // went); it stays in the smoke test's folder for tools/replay, headless.
            IEnumerator HillsFlight(StartPoint start)
            {
                FlightChoices c = _setup.Choices;
                c.StartPoint = start.Name;
                c.Legs = true;
                c.Payload = 1.0;
                c.Wind = WindLevel.Windy;
                c.Rain = Rain.Sunny;
                c.TimeOfDay = 12.0;
                Fly();
                yield return Until(() => _view == View.Flight && _flight != null && _flight.Host.Session != null &&
                                         _flight.Host.Session.State == SessionState.Flying && _flight.ShownFlight == _flight.Host.Session.Flights, 60);
                FlightSessionHost host = _flight != null ? _flight.Host : null;
                FlightSession session = host != null ? host.Session : null;
                Check(session != null && session.State == SessionState.Flying, "the hills flight starts at '" + start.Name + "'");
                if (session == null || !host.Inputs.KeyboardAllowed) yield break;
                yield return new WaitForSecondsRealtime(1);
                IWorldQuery world = session.Setup.World.Query;
                var views = new List<Task<List<TerrainParity.View>>>();
                void Parity(string where)
                {
                    Camera lens = _flight.Eye != null ? _flight.Eye.GetComponent<Camera>() : null;
                    if (lens != null)
                        views.Add(TerrainParity.Measure(lens, world, where, ("the scene's", t => _flight.SceneError(t)),
                            (FlightScene.NearPixelError.ToString("0.##") + " near the drone", t => FlightScene.NearPixelError)));
                }
                Parity("at rest");
                yield return Shot("hills_rest");
                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.4f);
                Check(_flight.Pose.Armed, "armed for the hills");
                var point = new XZ[1];
                var under = new GroundSample[1];
                var pilot = new HillsPilot(host.Inputs.Settings, (x, z) =>
                {
                    point[0] = new XZ(x, z);
                    world.SampleGround(point, under);
                    return under[0].Height;
                });
                KeyboardKeys keys = KeyboardKeys.None;
                host.Keys = () => keys;
                float begin = Time.realtimeSinceStartup, measure = begin + 20, called = begin;
                int cruiseShots = 0;
                while (pilot.Now != HillsPilot.Phase.Done && session.State == SessionState.Flying && !Crashed(session) && Time.realtimeSinceStartup < begin + 300)
                {
                    yield return null;
                    // The time the last keys were held: every frame since the last call, a shot's frames included.
                    float now = Time.realtimeSinceStartup;
                    keys = pilot.Next(_flight.Pose, now - called);
                    called = now;
                    if (pilot.Now == HillsPilot.Phase.Cruise && Time.realtimeSinceStartup >= measure)
                    {
                        measure += 20;
                        PoseFrame p = _flight.Pose;
                        Parity("cruising at t = " + p.TimeS.ToString("0") + " s, (" + p.Position.X.ToString("0") + ", " + p.Position.Z.ToString("0") + ")");
                        if (cruiseShots++ < 3) yield return Shot("hills_cruise_" + cruiseShots);
                    }
                }
                keys = KeyboardKeys.None;
                host.Keys = () => KeyboardKeys.None;
                PoseFrame last = _flight.Pose;
                Check(pilot.Now == HillsPilot.Phase.Done && session.State == SessionState.Flying && !Crashed(session) && last.TimeS >= 120,
                      "HillsPilot flew the route and landed: " + pilot.Now + " at t = " + last.TimeS.ToString("0.0") + " s, (" + last.Position.X.ToString("0.0") + ", " +
                      last.Position.Y.ToString("0.0") + ", " + last.Position.Z.ToString("0.0") + "), over ground from " + pilot.LowestGround.ToString("0.0") + " to " +
                      pilot.HighestGround.ToString("0.0") + " m, " + (Crashed(session) ? "CRASHED" : "no crash"));
                yield return new WaitForSecondsRealtime(1);
                Parity("landed");
                yield return Shot("hills_landed");
                yield return PressV("landed on the hills");
                yield return Shot("hills_landed_chase");
                yield return PressV("landed on the hills");
                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                host.Pause();
                session.EndFlight();
                yield return Until(() => _flight.Summary != null, 30);
                PostFlight summary = _flight.Summary;
                Check(summary != null && summary.End.Kind == FlightEndKind.Pilot && !summary.HasCrash && summary.Log.Seconds >= 120,
                      "the hills flight's summary: " + (summary != null ? summary.Log.Seconds.ToString("0.0") + " s, " + string.Join(" | ", summary.Lines()) : "none"));
                yield return Shot("hills_summary");
                string log = session.LogPath;
                Debug.Log("[Smoke] the hills flight's log: " + log);
                yield return Replays(log, "over the hills", 600);
                Task<GroundCheck.Result> check = Task.Run(() => GroundCheck.Read(log, world));
                yield return Until(() => check.IsCompleted, 120);
                GroundCheck.Result g = check.IsCompleted && !check.IsFaulted ? check.Result : default;
                Check(check.IsCompleted && !check.IsFaulted && g.Steps > 0 && g.Tunnels == 0 && g.BodyDepth <= 0,
                      "the hills flight's log against the ground: " + (check.IsFaulted ? check.Exception.GetBaseException().Message : g.ToString()));
                yield return Until(() => views.TrueForAll(t => t.IsCompleted), 120);
                foreach (Task<List<TerrainParity.View>> t in views)
                {
                    if (!t.IsCompleted || t.IsFaulted)
                    {
                        Check(false, "the drawn terrain's measure: " + (t.IsFaulted ? t.Exception.GetBaseException().Message : "not done"));
                        continue;
                    }
                    foreach (TerrainParity.View v in t.Result)
                        Debug.Log("[Smoke] the drawn terrain against the sim's: " + v + (v.Flipped ? " (rows flipped)" : ""));
                    // The flight's own setting (the last two views): the full heightmap within 30 m of the lens, WLD-11's 1 mm
                    // (3.2), and on the ground under the drone 0.01 mm within 2 m.
                    bool grounded = t.Result[0].Name.StartsWith("at rest", StringComparison.Ordinal) || t.Result[0].Name.StartsWith("landed", StringComparison.Ordinal);
                    for (int k = 2; k < t.Result.Count; k++)
                    {
                        TerrainParity.View v = t.Result[k];
                        Check(v.Pixels[2] > 0 && v.Max[2] <= 1e-3 && (!grounded || v.Pixels[0] > 0 && v.Max[0] <= 1e-5),
                              "the drawn terrain near the drone " + v.Name + ": within 10 m " + (v.Max[1] * 1e3).ToString("0.000") + " mm, within 30 m " +
                              (v.Max[2] * 1e3).ToString("0.000") + " mm (WLD-11: 1 mm)" + (grounded ? ", within 2 m " + (v.Max[0] * 1e3).ToString("0.000") + " mm" : ""));
                    }
                }
            }

            yield return Until(() => Time.frameCount > 3, 5);
            yield return new WaitForSecondsRealtime(0.5f);
            Check(_view == View.Menu, "the menu opens first");
            // w1-farmland-world 2.4 (QA's F6): on D3D12 Unity's own line proves it read the pipeline library PsoLibrary keeps
            // empty, so a Unity that renames the file fails here instead of quietly ending the crash fix on quit.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12)
            {
                if (PsoLibrary.FoundEmpty)
                    Check(PlayerLogHas(PsoLibrary.InitFailure, out string log), "the player log has Unity's '" + PsoLibrary.InitFailure + "': the D3D12 pipeline library " +
                          "starts empty (" + log + ")");
                else
                    Debug.Log("[Smoke] the D3D12 pipeline library was dropped this run, not read empty: its log line is the next run's check");
            }
            yield return Shot("menu");
            Train();
            yield return null;
            Check(_view == View.Setup && _setup.Starts.Length == 1 && _setup.Starts[0].Name == FlightWorlds.FarmlandStart.Name,
                  "Train opens the setup, from the village's start only (D-092): " + _setup.Starts.Length + " start point(s)");
            yield return Shot("setup");

            if (AppData.Review)
            {
                // w1-farmland-world 2.1: Explore opens the farmland in noclip with the feed, the stand at the village's start
                // on the terrain, its first frame within PLT-06's 20 s of the press.
                Load(explore: true);
                yield return Until(() => _view == View.Explore && _exploreFirstFrame >= 0, 60);
                Camera eye = Camera.main;
                FpvCamera feed = eye != null ? eye.GetComponent<FpvCamera>() : null;
                NoclipCamera noclip = eye != null ? eye.GetComponent<NoclipCamera>() : null;
                StartPoint start = FlightWorlds.FarmlandStart;
                bool onGround = _exploreStand != null && FarmlandExplore.Ground(_exploreStand.gameObject.scene, (float)start.X, (float)start.Z, out float terrain) &&
                                Mathf.Abs(_exploreStand.position.x - (float)start.X) < 1e-3f && Mathf.Abs(_exploreStand.position.z - (float)start.Z) < 1e-3f &&
                                Mathf.Abs(_exploreStand.position.y - terrain) < 1e-3f;
                Check(_view == View.Explore && SceneManager.GetActiveScene().name == FarmlandExplore.Scene && noclip != null && noclip.enabled && feed != null && feed.Running && onGround &&
                      _exploreFirstFrame <= 20f,
                      "Explore opens the farmland in noclip, the feed " + (feed != null && feed.Running ? "on" : "OFF") + ", the stand " +
                      (_exploreStand != null ? "at " + _exploreStand.position.ToString("0.000") + (onGround ? " on the terrain" : " OFF the start or the ground") : "MISSING") +
                      ", its first frame " + _exploreFirstFrame.ToString("0.00") + " s after the press (PLT-06: 20 s)");
                yield return new WaitForSecondsRealtime(2);
                yield return Shot("explore_farmland_stand");
                // w3-farmland-ground 3.2: the drawn terrain against the sim's from noclip's start, the stand 3 m ahead, through
                // the feed and without it (V), at the scene's errors and at the flight's near the drone: what noclip shows
                // of the ground the drone stands on. Noclip keeps the scene's errors; within 10 m they must hold WLD-11's 1 mm.
                if (eye != null && feed != null)
                {
                    IWorldQuery world = FlightWorlds.Farmland(AppData.Root).Open().Query;
                    var views = new List<Task<List<TerrainParity.View>>>();
                    for (int v = 0; v < 2; v++)
                    {
                        feed.enabled = v == 0;
                        yield return null;
                        yield return null;
                        views.Add(TerrainParity.Measure(eye, world, "noclip's start, the feed " + (v == 0 ? "on" : "off"), ("the scene's", t => t.heightmapPixelError),
                            (FlightScene.NearPixelError.ToString("0.##") + " near the drone", t => FlightScene.NearPixelError)));
                    }
                    feed.enabled = true;
                    yield return Until(() => views.TrueForAll(t => t.IsCompleted), 60);
                    foreach (Task<List<TerrainParity.View>> t in views)
                    {
                        if (!t.IsCompleted || t.IsFaulted) Check(false, "the drawn terrain's measure in noclip: " + (t.IsFaulted ? t.Exception.GetBaseException().Message : "not done"));
                        else
                        {
                            foreach (TerrainParity.View v in t.Result) Debug.Log("[Smoke] the drawn terrain against the sim's: " + v + (v.Flipped ? " (rows flipped)" : ""));
                            for (int k = 0; k < 2; k++)
                                Check(t.Result[k].Pixels[1] > 0 && t.Result[k].Max[1] <= 1e-3, "the drawn terrain round the stand in " + t.Result[k].Name + ": within 10 m " +
                                      (t.Result[k].Max[1] * 1e3).ToString("0.000") + " mm (WLD-11: 1 mm)");
                        }
                    }
                }
                Menu();
                yield return Until(() => _view == View.Menu, 30);
                Check(_view == View.Menu, "Esc's route back to the menu");
                Train();
            }

            StartPoint ground = _setup.Starts.Length > 0 ? _setup.Starts[0] : default;
            Check(ground.Name != null, "the farmland lists its village's start");
            bool hills = Array.IndexOf(args, "-smokehills") >= 0;
            if (hills || Array.IndexOf(args, "-smokelegs") >= 0)
            {
                yield return hills ? HillsFlight(ground) : LegFlights(ground);
                if (pilotSetup != null) File.WriteAllBytes(setupFile, pilotSetup);
                else File.Delete(setupFile);
                Debug.Log("[Smoke] " + (failed == 0 && _errors == 0 ? "PASS" : "FAIL") + ": " + failed + " checks failed, " + _errors + " errors logged");
                Application.Quit(failed == 0 && _errors == 0 ? 0 : 1);
                yield break;
            }
            for (int n = 0; n < 2; n++)
            {
                StartPoint start = ground;
                bool stand = n == 1; // the second from the launch stand, at the same start
                string tag = start.Name + (stand ? "_stand" : "");
                FlightChoices c = _setup.Choices;
                c.StartPoint = start.Name;
                c.Legs = !stand;
                c.Payload = 1.0;
                c.Wind = wind;
                c.Rain = stand ? Rain.Rain : Rain.Sunny; // the stand's in rain, for the wet ground
                c.TimeOfDay = n == 0 ? 11.5 : 17.5;
                // QA's finding 15: the second flight is set from the first's summary. With the keyboard barred, as in a
                // pilot's build without the radio, it waits, and the view is the chosen start's, not the last flight's end.
                bool barred = n == 1 && _flight != null && _flight.Host.Inputs != null && _flight.Host.Inputs.KeyboardAllowed;
                if (barred) _flight.Host.Inputs.KeyboardAllowed = false;
                Fly();
                string where = "'" + start.Name + "' (legs " + (c.Legs ? "on" : "off, the stand") + ")";
                yield return Until(() => _view == View.Flight && _flight != null && _flight.Host.Session != null, 60);
                Check(_flight != null && _flight.Host.Session != null, "Fly loads the flight over the farmland at " + where);
                if (_flight == null || _flight.Host.Session == null) break;
                FlightSessionHost host = _flight.Host;
                if (barred)
                {
                    yield return new WaitForSecondsRealtime(1);
                    Vector3 eye = Camera.main != null ? Camera.main.transform.position : Vector3.positiveInfinity;
                    // The lens over the start: its mount turned to the start's heading, over the terrain there.
                    FarmlandExplore.Ground(SceneManager.GetActiveScene(), (float)start.X, (float)start.Z, out float terrain);
                    Vector3 lens = new Vector3((float)start.X, terrain, (float)start.Z) + Quaternion.Euler(0f, (float)(start.Yaw * 180 / Math.PI), 0f) * _flight.CameraMount;
                    Check(host.WaitingForRadio && Math.Abs(eye.x - lens.x) < 0.01 && Math.Abs(eye.z - lens.z) < 0.01 && eye.y - terrain < 0.5,
                          "after a flight, the wait for the radio views the chosen start " + where + " from " + eye.ToString("0.00"));
                    yield return Shot("waiting_after_flight_" + tag);
                    host.Inputs.KeyboardAllowed = true;
                }
                if (!host.Inputs.KeyboardAllowed)
                {
                    yield return new WaitForSecondsRealtime(2);
                    Check(host.WaitingForRadio || host.Session.State == SessionState.Flying, "the flight waits for the radio (or flies on it)");
                    Check(!host.WaitingForRadio || _flight.Osd.Row(0).StartsWith(FlightOsd.DisarmedMode, StringComparison.Ordinal),
                          "the OSD reads DISARMED while waiting: " + Osd());
                    yield return Shot("waiting_" + tag);
                    if (n == 0)
                    {
                        yield return PressV("while waiting at '" + start.Name + "'");
                        yield return Shot("waiting_chase_" + tag);
                        yield return PressV("while waiting at '" + start.Name + "'");
                        _flight.Covered = true;
                        _setup.Choices = host.Inputs.Choices.Clone();
                        _view = View.Setup;
                    }
                    continue;
                }

                yield return Until(() => host.Session.State == SessionState.Flying && _flight.ShownFlight == host.Session.Flights, 10);
                yield return new WaitForSecondsRealtime(1);
                FlightSession session = host.Session;
                Check(session.State == SessionState.Flying, "flight " + session.Flights + " started at " + where + " on " + session.Input);
                if (stand)
                {
                    // The pause's Restart, first without the radio (QA's finding 14): the flight ends by the pilot and the
                    // next waits for it; with the keyboard back it starts.
                    ulong seed = session.Seed;
                    string log = session.LogPath;
                    int flights = session.Flights;
                    object line = session.Core.Tether;
                    host.Pause();
                    host.Inputs.KeyboardAllowed = false;
                    host.Restart();
                    yield return new WaitForSecondsRealtime(0.5f);
                    Check(host.WaitingForRadio && session.State == SessionState.Ended && session.End.Kind == FlightEndKind.Pilot,
                          "Restart without the radio: ended by the pilot, and the next flight waits for it");
                    host.Inputs.KeyboardAllowed = true;
                    // The next flight starts StartWaitSeconds on, after its preparation; the scene draws it the frame after.
                    yield return Until(() => session.Flights == flights + 1 && session.State == SessionState.Flying && _flight.ShownFlight == session.Flights, 10);
                    yield return new WaitForSecondsRealtime(1);
                    Check(session.Flights == flights + 1 && session.Seed != seed && session.LogPath != log, "Restart: a new flight with a new seed and log");
                    PoseFrame again = _flight.Pose;
                    MeshRenderer laid = Tube("FiberTrail0");
                    Check(session.Core.Tether != null && !ReferenceEquals(session.Core.Tether, line) && again.FiberPaidOut < 0.5 &&
                          (again.FiberTrailCount < 2 || laid != null && laid.enabled),
                          "Restart lays the line again: a new line, " + again.FiberPaidOut.ToString("0.000") + " m paid out, its trail " + again.FiberTrailCount +
                          " points drawn from the launch, the coil " + (_flight.Fiber.Coil != null ? "at " + (_flight.Fiber.Coil.ShownRadius * 1000).ToString("0.00") + " mm" : "as the model draws it"));
                }
                double rest = _flight.Pose.Position.Y;
                Check(Math.Abs(rest - _flight.RestPose.Position.Y) < 0.01, "at rest at " + rest.ToString("0.000") + " m (start " + _flight.RestPose.Position.Y.ToString("0.000") + " m)");
                Check(_flight.Osd.Row(0).StartsWith(FlightOsd.DisarmedMode, StringComparison.Ordinal), "the OSD reads DISARMED at rest: " + Osd());
                // The physics integration 2: surfaces.json's ground, the map's cuts and the fiber line from the table's launch.
                FlightSetup built = session.Setup;
                // The map's cuts are its objects' (MapWind.Cuts: none on the farmland until its objects, w3-farmland-ground 1.2).
                int mapCuts = built.World.Query is MapWorld map ? MapWind.Cuts(map)?.Length ?? 0 : -1;
                Check(built.Contacts.Surfaces != null && (built.Wind.Cuts?.Length ?? 0) == mapCuts && built.Tether != null && session.Core.Tether != null &&
                      built.TetherCoilPart == host.Inputs.Drone.Coil.Part && !built.TetherTwoWay && built.TetherLayout == null,
                      "the flight carries surfaces.json's ground, the map's " + (built.Wind.Cuts != null ? built.Wind.Cuts.Length : 0) + " cuts and the line from the launch, " +
                      _flight.Pose.FiberNodeCount + " nodes, the trail " + _flight.Pose.FiberTrailCount + " points");
                // D-079, the props break (p7f-damage): props.blade's tables, each blade's factory pitch draw, the merged
                // physics_materials.json with what a blade meets and the strikes; whole props (the stand's flight is a restart's).
                bool whole = built.Propulsion.Rotor != null && session.Core.Propulsion.Rotor(0) != null;
                for (int rotor = 0; whole && rotor < 4; rotor++)
                    for (int blade = 0; blade < session.Core.Propulsion.Rotor(rotor).Blades; blade++)
                        whole &= session.Core.Propulsion.Rotor(rotor).Blade(blade).Whole;
                Check(whole && built.Asymmetry != null && built.Contacts.BladeStrikes && session.Core.Strikes != null &&
                      built.Contacts.MaterialsHash == host.Inputs.MaterialsHash && built.Contacts.Materials.Length == host.Inputs.Materials.Length,
                      "the flight carries the blade tables, the strikes and physics_materials.json 0x" + built.Contacts.MaterialsHash.ToString("X16") +
                      " (" + built.Contacts.Materials.Length + " rows), its props whole");
                if (session.Choices.Rain != Rain.Sunny)
                {
                    CoverRow straw = built.Contacts.Surfaces.Cover[(int)CoverKind.Straw];
                    Check(SurfaceTable.MoistureOf(built.Rain) == Moisture.Wet && session.Core.Contacts.Cover(CoverKind.Straw).Equals(straw.Wet) && !straw.Wet.Equals(straw.Dry),
                          "the rain flight loads wet ground: surfaces.json's " + SurfaceTable.MoistureOf(built.Rain) + " sets, the straw at its wet modulus " +
                          session.Core.Contacts.Cover(CoverKind.Straw).ModulusMin.ToString("0.###E0") + " Pa against " + straw.Dry.ModulusMin.ToString("0.###E0") + " dry");
                }
                // p5-drone's bottle on its seat at the payload's size, and D02 at the stand's foot and heading.
                Transform bottle = _flight.Bottle, drawn = _flight.Stand;
                Check(bottle != null && (bottle.localPosition - DroneModels.BottleSeat).magnitude < 1e-6f
                      && Mathf.Abs(bottle.localScale.x - DroneModels.BottleScale(session.Loadout.Payload)) < 1e-6f,
                      "D01_bottle on its seat at " + session.Loadout.Payload.ToString("0.0") + " kg's size" + (bottle != null ? ", scale " + bottle.localScale.x.ToString("0.000") : ""));
                if (stand)
                    Check(drawn != null && Mathf.Abs(drawn.position.x - (float)start.X) < 1e-3f && Mathf.Abs(drawn.position.z - (float)start.Z) < 1e-3f
                          && Quaternion.Angle(drawn.rotation, Quaternion.Euler(0f, (float)(start.Yaw * 180 / Math.PI), 0f)) < 0.1f,
                          "D02 at the stand's foot and heading" + (drawn != null ? ", " + drawn.position.ToString("0.000") + " at " + drawn.eulerAngles.y.ToString("0.0") + "°, the drone " + (rest - drawn.position.y).ToString("0.000") + " m above its foot" : ""));
                else Check(drawn == null, "no stand with the legs on");
                yield return Shot("rest_" + tag);
                yield return PressV("at rest at " + where);
                yield return Shot("chase_" + tag);
                bool chaseClimb = _sequences != null && stand; // -smokeseq: the stand's punch-out in the chase view
                if (!chaseClimb) yield return PressV("at rest at " + where);

                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.4f);
                Check(_flight.Pose.Armed, "armed by the keyboard's switch");
                double top = rest;
                Action climb = () => top = Math.Max(top, _flight.Pose.Position.Y);
                StartCoroutine(Sequence("punch_" + (chaseClimb ? "chase_" : "fpv_") + tag, 1.4f)); // the throttle near 0.7
                yield return Hold(KeyboardKeys.ThrottleUp, 1.6f, climb);
                yield return Hold(KeyboardKeys.None, 1.4f, climb);
                Check(top - rest > 1.0, "climbed " + (top - rest).ToString("0.00") + " m");
                if (chaseClimb) yield return PressV("in the air");
                FlightOsd.Readout shown = _flight.Osd.Shown;
                Check(_flight.Osd.Row(0).StartsWith(FlightOsd.ArmedMode + " ", StringComparison.Ordinal) && shown.Centiamps > 0 && shown.MahDrawn > 0 &&
                      shown.PackCentivolts > 0 && shown.CellCentivolts == shown.PackCentivolts / _flight.Osd.Cells,
                      "the OSD reads AIR armed, with the current, the charge used and the pack: " + Osd());
                PoseFrame high = _flight.Pose;
                MeshRenderer span = Tube("FiberSpan"), trail = Tube("FiberTrail0");
                Double3 last = high.FiberNodeCount > 0 ? high.FiberNodes[high.FiberNodeCount - 1] : default;
                Vector3 exit = high.FiberNodeCount > 0 ? new Vector3((float)last.X, (float)last.Y, (float)last.Z) : Vector3.positiveInfinity;
                Check(high.FiberNodeCount >= 2 && high.FiberPaidOut > 0 && span != null && span.enabled && span.bounds.SqrDistance(exit) < 1f &&
                      (high.FiberTrailCount < 2 || trail != null && trail.enabled),
                      "the line draws in the air: " + high.FiberNodeCount + " nodes through the exit at " + exit.ToString("0.00") + ", " + high.FiberPaidOut.ToString("0.00") +
                      " m paid out, the trail " + high.FiberTrailCount + " points (" + _flight.Fiber.ChunkBuilds + " chunk builds), the coil " +
                      (_flight.Fiber.Coil != null ? "at " + (_flight.Fiber.Coil.ShownRadius * 1000).ToString("0.00") + " mm" : "as the model draws it"));
                yield return Shot("air_" + tag);
                yield return PressV("in the air");
                yield return Shot("air_chase_" + tag);
                yield return PressV("in the air");

                host.Pause();
                long steps = session.Core.State.Step;
                yield return new WaitForSecondsRealtime(0.3f);
                Check(session.State == SessionState.Paused && session.Core.State.Step == steps, "paused: no step for 0.3 s, at step " + steps);
                Double3 air = session.Core.Trace.Wind; // the field at the body in the last step; the core is not stepping
                Debug.Log("[Smoke] " + session.Setup.Wind.Level + " wind at the drone, " + (session.Core.State.Position.Y - rest).ToString("0.0") + " m up: (" +
                          air.X.ToString("0.00") + ", " + air.Y.ToString("0.00") + ", " + air.Z.ToString("0.00") + ") m/s, " +
                          Math.Sqrt(air.X * air.X + air.Z * air.Z).ToString("0.00") + " m/s across");
                if (n == 0) yield return Shot("pause");
                host.Resume();

                // D-062: a disarm in the air ends nothing. The throttle down (re-arming wants it there), disarmed at the
                // top of the climb: still flying, no summary, DISARMED. Re-armed while it falls: AIR, and it flies on. The
                // flight ends from the pause's End flight, and only then is there a summary.
                yield return Hold(KeyboardKeys.ThrottleDown, 1.8f);
                yield return Until(() => _flight.Pose.Velocity.Y <= 0, 5);
                double up = _flight.Pose.Position.Y - rest;
                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.2f);
                Check(!_flight.Pose.Armed && session.State == SessionState.Flying && _flight.Summary == null && up > 3 &&
                      _flight.Osd.Row(0).StartsWith(FlightOsd.DisarmedMode, StringComparison.Ordinal),
                      "disarmed in the air at " + up.ToString("0.0") + " m: still flying, no summary, the OSD reads DISARMED: " + Osd());
                yield return Shot("disarmed_air_" + tag);
                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.2f);
                up = _flight.Pose.Position.Y - rest;
                Check(_flight.Pose.Armed && session.State == SessionState.Flying && up > 1 && _flight.Osd.Row(0).StartsWith(FlightOsd.ArmedMode + " ", StringComparison.Ordinal),
                      "re-armed in the air at " + up.ToString("0.0") + " m: the OSD reads AIR: " + Osd());
                double sink = _flight.Pose.Velocity.Y;
                yield return Hold(KeyboardKeys.ThrottleUp, 0.6f);
                Check(session.State == SessionState.Flying && _flight.Pose.Armed && _flight.Osd.Shown.Centiamps > 0 && _flight.Summary == null,
                      "flies on after the re-arm, at " + (_flight.Pose.Position.Y - rest).ToString("0.0") + " m, " + sink.ToString("0.0") + " then " +
                      _flight.Pose.Velocity.Y.ToString("0.0") + " m/s up: " + Osd());
                yield return Shot("rearmed_" + tag);
                host.Pause();
                session.EndFlight(); // the pause's End flight
                yield return Until(() => _flight.Summary != null, 30);
                PostFlight summary = _flight.Summary;
                Check(summary != null && summary.End.Kind == FlightEndKind.Pilot, "End flight, and the summary from the log: " + (summary != null ? summary.Cause() : "none"));
                Check(summary != null && summary.Log.Events.Exists(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Pause), "the pause is in the log");
                yield return Shot("summary_" + tag);
                yield return Replays(session.LogPath, "from " + where);
                if (n == 0)
                {
                    _setup.Choices = host.Inputs.Choices.Clone();
                    _flight.Covered = true;
                    _view = View.Setup; // the summary's Setup
                }
            }

            // The keyboard's flight into the ground by the village's start (QA's finding 3 in a player, on the test range
            // a house; the farmland's sim has no objects until w3-farmland-ground 1.2): the summary names what it hit. A
            // pilot's build has no keyboard and skips it.
            // D-063: the crash ends nothing. The drone stays under physics with nothing over the picture: the throttle
            // down, it lies where it lands, stepping on, until the pause's End flight, whose summary lists the crash, and R
            // from that summary restarts in the frame that reads it. D-087: when the crash broke the line, R is pressed there
            // instead, after the break with the failsafe at its stage, and must restart in the frame that reads it (PressR);
            // the crash flight's summary, read from its log, lists the crash, the break and the failsafe's stages. The
            // restarted flight is then ended and R pressed from its summary.
            string lastStart = ground.Name;
            bool lastLegs = false; // the stand's flight
            double lastHour = 17.5;
            if (_flight != null && _flight.Host.Inputs != null && _flight.Host.Inputs.KeyboardAllowed)
            {
                FlightChoices c = _setup.Choices;
                c.StartPoint = lastStart = ground.Name;
                c.Legs = lastLegs = true;
                c.Rain = Rain.Sunny;
                c.TimeOfDay = lastHour = 14.0;
                Fly();
                FlightSessionHost host = _flight.Host;
                yield return Until(() => host.Session.State == SessionState.Flying && host.Session.Flights == _flight.ShownFlight && host.Session.Choices.StartPoint == ground.Name, 10);
                yield return new WaitForSecondsRealtime(1);
                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.4f);
                Check(_flight.Pose.Armed, "armed for the flight into the ground");
                yield return IntoTheGround();
                FlightSession session = host.Session;
                PoseFrame atCrash = _flight.Pose;
                double rest = _flight.RestPose.Position.Y, hit = atCrash.Position.Y - rest;
                long crashStep = session.Poses.LatestStep;
                Check(Crashed(session) && session.State == SessionState.Flying && _flight.Summary == null,
                      "the flight into the ground crashes at " + hit.ToString("0.0") + " m and flies on, no summary: " + session.State);
                yield return Shot("crash_fpv");
                yield return Hold(KeyboardKeys.ThrottleDown, 1.5f);
                yield return Hold(KeyboardKeys.None, 1.0f);
                // A line the tumble broke late is still in Betaflight's failsafe here (1.5 s from the last good frame): the
                // pose and the OSD are read once its stage has settled and a frame has drawn it, so they agree with it.
                if (session.Poses.Read().Signals.LinkState == Fpv450.Sim.Flight.LinkState.Broken)
                {
                    yield return Until(() => session.Core.Failsafe == null || session.Core.Failsafe.Stage == Fpv450.Sim.Control.FailsafeStage.Disarmed, 5);
                    yield return null;
                    yield return null;
                }
                PoseFrame lying = _flight.Pose;
                long stepped = session.Poses.LatestStep - crashStep;
                // Under physics: it moved off the crash pose, wherever it lands (the ground below the wall, or the eave and
                // the roof, where the airframe's effects and the throttle's ramp down can carry it). It need not rest:
                // armed at zero throttle the motors idle and the mixer still works them against what it lies on (no
                // airmode since D-086: half the PID mix below half throttle, the I term held under min_check). Unless the
                // crash broke the line (p7d-tether 1.8, from model 56): the receiver has lost the link, and Betaflight's
                // failsafe drops the drone at stage 2 and disarms it, 1.5 s after the last good frame; the OSD then reads DISARMED.
                Quat a = atCrash.Attitude, b = lying.Attitude;
                double moved = (lying.Position - atCrash.Position).Length(), turned = 2 * Math.Acos(Math.Min(1, Math.Abs(a.W * b.W + a.X * b.X + a.Y * b.Y + a.Z * b.Z))) * 180 / Math.PI;
                bool broke = session.Poses.Read().Signals.LinkState == Fpv450.Sim.Flight.LinkState.Broken;
                Fpv450.Sim.Control.Failsafe failsafe = session.Core.Failsafe;
                bool dropped = broke && failsafe != null && failsafe.Stage == Fpv450.Sim.Control.FailsafeStage.Disarmed;
                Check(session.State == SessionState.Flying && _flight.Summary == null && stepped > 2000 && (moved > 0.1 || turned > 30) && lying.Armed == !dropped &&
                      _flight.Osd.Row(0).StartsWith(dropped ? FlightOsd.DisarmedMode : FlightOsd.ArmedMode + " ", StringComparison.Ordinal),
                      "after the crash the drone stays under physics, nothing over the picture: from " + hit.ToString("0.0") + " m it moved " + moved.ToString("0.00") + " m and turned " +
                      turned.ToString("0") + "°, now at " + (lying.Position.Y - rest).ToString("0.0") + " m, " + lying.Velocity.Length().ToString("0.0") + " m/s, " + stepped + " steps on; " +
                      (broke ? "the line broke, the failsafe " + (dropped ? "disarmed it" : "at " + (failsafe != null ? failsafe.Stage.ToString() : "none")) : "the line whole") + "; " + Osd());
                yield return Shot("crash_lying_fpv");
                yield return PressV("after the crash");
                yield return Shot("crash_chase");
                yield return PressV("after the crash");
                string crashLog = session.LogPath;
                PostFlight summary;
                if (broke)
                {
                    // D-087: R after the break, the failsafe at its stage, restarts in the frame that reads the press; the
                    // crash flight's summary is then read from its log.
                    yield return PressR("after the break, the failsafe " + (failsafe != null ? failsafe.Stage.ToString() : "none"));
                    SessionInputs inputs = host.Inputs;
                    Task<PostFlight> read = Task.Run(() => PostFlight.Read(crashLog, inputs.OpenWorld));
                    yield return Until(() => read.IsCompleted, 30);
                    summary = read.IsCompleted && !read.IsFaulted ? read.Result : null;
                }
                else
                {
                    host.Pause();
                    session.EndFlight(); // the pause's End flight
                    yield return Until(() => _flight.Summary != null, 30);
                    summary = _flight.Summary;
                }
                FlightIncident crash = summary != null ? summary.Incidents.Find(i => i.Kind == FlightIncidentKind.Crash) : default;
                Check(summary != null && summary.End.Kind == FlightEndKind.Pilot && summary.HasCrash && crash.Kind == FlightIncidentKind.Crash &&
                      summary.CrashObject == CrashGround && summary.Lines().Contains(summary.Describe(crash)),
                      (broke ? "R" : "End flight") + ", and the summary lists the crash into the ground: " + (summary != null ? string.Join(" | ", summary.Lines()) : "none"));
                if (broke)
                {
                    // D-087: the summary lists the break, where and how, and each failsafe stage the log has.
                    List<FlightIncident> fiber = summary != null
                        ? summary.Incidents.FindAll(i => i.Kind == FlightIncidentKind.FiberBreak || i.Kind == FlightIncidentKind.FailsafeStage)
                        : new List<FlightIncident>();
                    int stages = summary != null ? summary.Log.Count(EventKind.Failsafe) : -1;
                    Check(fiber.Count > 0 && fiber.Count == stages + 1 && fiber[0].Kind == FlightIncidentKind.FiberBreak && summary.BreakPlace.Length > 0 &&
                          fiber.TrueForAll(i => summary.Lines().Contains(summary.Describe(i))),
                          "the summary lists the fiber's break and the failsafe's " + stages + " stages: " + string.Join(" | ", fiber.ConvertAll(i => summary.Describe(i))));
                    yield return Replays(crashLog, "into the ground");
                    // The restarted flight's End flight, and R from its summary: the next starts in the frame that reads it.
                    FlightSession again = host.Session;
                    host.Pause();
                    again.EndFlight();
                    yield return Until(() => _flight.Summary != null, 30);
                    yield return Shot("summary_after_restart");
                    yield return PressR("from the summary");
                }
                else
                {
                    yield return Shot("summary_crash");
                    yield return Replays(crashLog, "into the ground");
                    yield return PressR("from the summary");
                }
            }

            FlightChoices saved = SetupStore.Load(Application.persistentDataPath, out string error);
            Check(error == null && saved.StartPoint == lastStart && saved.Legs == lastLegs && saved.TimeOfDay == lastHour,
                  "the last setup is saved for the next start");
            if (pilotSetup != null) File.WriteAllBytes(setupFile, pilotSetup);
            else File.Delete(setupFile);
            Menu();
            yield return Until(() => _view == View.Menu, 30);
            Check(_view == View.Menu, "Menu returns to the menu");
            if (_sequences != null)
            {
                UnityEngine.Rendering.AsyncGPUReadback.WaitAllRequests();
                yield return Until(() => _sequenceWrites.TrueForAll(t => t.IsCompleted), 60);
                Check(_sequenceWrites.TrueForAll(t => t.IsCompleted && !t.IsFaulted), "the sequences' " + _sequenceWrites.Count + " frames are written in " + _sequences);
            }
            Debug.Log("[Smoke] " + (failed == 0 && _errors == 0 ? "PASS" : "FAIL") + ": " + failed + " checks failed, " + _errors + " errors logged");
            Application.Quit(failed == 0 && _errors == 0 ? 0 : 1);
        }

        // The smoke test's: whether the player's log file has the line. A log to stdout (-logFile -) is no file, so the
        // check fails there: give -logFile a path.
        static bool PlayerLogHas(string line, out string log)
        {
            log = Application.consoleLogPath;
            if (string.IsNullOrEmpty(log) || log == "-" || !File.Exists(log))
            {
                log = (log == "-" ? "the log goes to stdout" : string.IsNullOrEmpty(log) ? "no log file" : log + " not found") + ": give -logFile a path";
                return false;
            }
            using (var reader = new StreamReader(new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
                for (string text = reader.ReadLine(); text != null; text = reader.ReadLine())
                    if (text.Contains(line))
                        return true;
            return false;
        }

        // -smokelegs: the next flight's seed and start from its case; none (a restart) stands as a flight does.
        void LegCase(SessionInputs inputs)
        {
            if (_legCase == null && inputs.Adjust == null) return;
            inputs.NewSeed = () => _legCase != null ? _legCase.Seed : 1000;
            inputs.Adjust = s =>
            {
                if (_legCase != null) LegShots.Apply(_legCase, s);
            };
        }

        /// <summary>What the crash flight hits: the summary's name for the terrain (PostFlight).</summary>
        const string CrashGround = "the ground";

        /// <summary>The crash flight's dive: the nose this far down, in degrees.</summary>
        const double DiveDeg = 60;

        // The smoke test's keyboard pilot into the ground, on the flight's own pose. The throttle (W, S, which move it at
        // KeyboardSticks.ThrottlePerSecond) is steered to the stick whose command, on the flight's own curve, is a level
        // set by the height's and the climb's errors about the hover (a command of 0.46 at 1 kg, assumed: 0.48 of the
        // stick on the 50 / 10 curve it was tried on, at 7 and 16 ms frames), holding 3 m over the start; D turns to 158°
        // once 2 m up (the test range's house lay that way), then the pitch key tilts it forward until the nose is DiveDeg
        // down, and on the hover's command alone it dives into the ground.
        // The flight's crash latch, as the physics last published it (the core is stepping on its thread).
        static bool Crashed(FlightSession session) => session.Poses.Read().Signals.Crashed;

        IEnumerator IntoTheGround()
        {
            const double Height = 3, Heading = 158, Hover = 0.46;
            FlightSessionHost host = _flight.Host;
            FlightSession session = host.Session;
            var curve = new RatePreview();
            curve.From(host.Inputs.Settings);
            KeyboardKeys keys = KeyboardKeys.None;
            host.Keys = () => keys;
            double rest = _flight.RestPose.Position.Y, throttle = 0; // the keyboard's throttle, as its keys move it
            float end = Time.realtimeSinceStartup + 15, pitchFrom = -1;
            float hoverUntil = _sequences != null ? -1 : 0; // -smokeseq: a hover once 2 m up, shot FPV then chase
            bool turning = false;
            while (session.State == SessionState.Flying && !Crashed(session) && Time.realtimeSinceStartup < end)
            {
                yield return null;
                float now = Time.realtimeSinceStartup;
                throttle += KeyboardSticks.ThrottlePerSecond * Time.unscaledDeltaTime
                            * (((keys & KeyboardKeys.ThrottleUp) != 0 ? 1 : 0) - ((keys & KeyboardKeys.ThrottleDown) != 0 ? 1 : 0));
                throttle = Math.Max(0, Math.Min(1, throttle));
                PoseFrame p = _flight.Pose;
                double h = p.Position.Y - rest;
                Quat q = p.Attitude;
                double heading = Math.Atan2(2 * (q.X * q.Z + q.W * q.Y), 1 - 2 * (q.X * q.X + q.Y * q.Y)) * 180 / Math.PI;
                double turn = ((Heading - heading) % 360 + 540) % 360 - 180;
                double want = HillsPilot.StickFor(curve, pitchFrom >= 0 ? Hover : Math.Max(0.3, Math.Min(0.7, Hover + 0.05 * (Height - h) - 0.06 * p.Velocity.Y)));
                KeyboardKeys k = throttle < want - 0.01 ? KeyboardKeys.ThrottleUp : throttle > want + 0.01 ? KeyboardKeys.ThrottleDown : KeyboardKeys.None;
                if (pitchFrom < 0 && h > 2)
                {
                    if (hoverUntil < 0)
                    {
                        hoverUntil = now + 2.5f;
                        end += 2.5f; // the hover is not the flight's time to the ground
                        StartCoroutine(HoverSequences());
                    }
                    if (now < hoverUntil) { }
                    else if (Math.Abs(turn) > 3)
                    {
                        k |= turn > 0 ? KeyboardKeys.YawRight : KeyboardKeys.YawLeft;
                        if (!turning) StartCoroutine(Sequence("yaw_fpv", 0.15f));
                        turning = true;
                    }
                    else
                    {
                        pitchFrom = now;
                        StartCoroutine(Sequence("punch_forward_fpv", 0.1f));
                    }
                }
                double nose = Math.Asin(Math.Max(-1, Math.Min(1, -q.Rotate(new Double3(0, 0, 1)).Y))) * 180 / Math.PI; // down, °
                if (pitchFrom >= 0 && nose < DiveDeg) k |= KeyboardKeys.PitchForward;
                keys = k;
            }
            host.Keys = () => KeyboardKeys.None;
        }

        // The crash flight's hover (-smokeseq): a sequence in the FPV view, then one in the chase view and back. V's own
        // toggle, which the flight never sees.
        IEnumerator HoverSequences()
        {
            yield return Sequence("hover_fpv", 0.6f);
            _flight.Pressed(true, false);
            yield return Sequence("hover_chase", 0.3f);
            _flight.Pressed(true, false);
        }

        readonly List<Task> _sequenceWrites = new List<Task>();

        // -smokeseq's frames (p7e-camera-feel 1.7): _sequenceFrames consecutive frames from `delay` s on, each as the screen
        // showed it: copied on the GPU at the frame's end, read back without a stall and written off the main thread, so
        // the capture leaves the frames' timing as it was. frames.txt has each frame's number, its time, the feed's field,
        // the view and whether the FPV camera drew that frame or held its field.
        IEnumerator Sequence(string name, float delay)
        {
            if (_sequences == null) yield break;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            string dir = Path.Combine(_sequences, name);
            Directory.CreateDirectory(dir);
            var lines = new string[_sequenceFrames];
            bool flip = SystemInfo.graphicsUVStartsAtTop;
            float t0 = -1f;
            for (int i = 0; i < _sequenceFrames; i++)
            {
                yield return new WaitForEndOfFrame();
                RenderTexture rt = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
                string file = Path.Combine(dir, i.ToString("00") + ".png");
                int w = rt.width, h = rt.height;
                UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, r =>
                {
                    RenderTexture.ReleaseTemporary(rt);
                    if (r.hasError)
                    {
                        Debug.LogError("[Smoke] sequence " + name + ": a frame's readback failed");
                        return;
                    }
                    byte[] px = r.GetData<byte>().ToArray();
                    _sequenceWrites.Add(Task.Run(() => File.WriteAllBytes(file,
                        ImageConversion.EncodeArrayToPNG(Opaque(px, w, h, flip), UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB, (uint)w, (uint)h))));
                });
                if (t0 < 0f) t0 = Time.realtimeSinceStartup;
                Transform eye = _flight != null ? _flight.Eye : null;
                FpvCamera rig = eye != null ? eye.GetComponent<FpvCamera>() : null;
                lines[i] = i.ToString("00") + "  frame " + Time.frameCount + "  " + ((Time.realtimeSinceStartup - t0) * 1e3).ToString("0.0") + " ms  field " +
                           (rig != null && rig.Running ? rig.Events.Field.ToString() : "-") + "  " + (_flight != null && _flight.Chase ? "chase" : "fpv") +
                           (eye == null ? "" : eye.GetComponent<Camera>().enabled ? "  drawn" : "  held");
            }
            File.WriteAllLines(Path.Combine(dir, "frames.txt"), lines);
            Debug.Log("[Smoke] sequence " + name + ": " + _sequenceFrames + " frames, " + (FlightScene.CameraFeel ? "the field latch" : "-camerafeel 0") + ", in " + dir);
        }

        // The readback's rows top first and every pixel opaque (the back buffer's alpha is not the picture's).
        static byte[] Opaque(byte[] px, int w, int h, bool flip)
        {
            var o = new byte[px.Length];
            int row = w * 4;
            for (int y = 0; y < h; y++) Buffer.BlockCopy(px, (flip ? h - 1 - y : y) * row, o, y * row, row);
            for (int i = 3; i < o.Length; i += 4) o[i] = 255;
            return o;
        }
    }
}
