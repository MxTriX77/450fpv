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
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.UI;
using Fpv450.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Fpv450.App
{
    /// <summary>
    /// The app (design 6, APP-02…05): the menu (Train, Calibrate, Exit; Explore in a review build), the setup, the
    /// Calibrate screen, and the flight over the test range (FlightScene). It lives across scenes: Boot holds the menu,
    /// TestRange the world. Train → the setup → Fly saves the setup and loads the range, and the flight starts once the
    /// radio is in, so arming the switch is the only step left. The summary's Fly again and Setup stay in the range.
    /// </summary>
    /// <remarks>
    /// Options: <c>-windowed</c> (a 1920 × 1080 window), <c>-vsync &lt;n&gt;</c> (as the range's boot), <c>-quality &lt;name&gt;</c>
    /// (a preset, saved for the next start: QualityPreset). <c>-smoketest</c>
    /// walks the app (Smoke) and quits, exit code 1 on any failed check or any error logged; <c>-smokeshots &lt;folder&gt;</c>
    /// saves its screens there.
    /// </remarks>
    public sealed class AppFlow : MonoBehaviour
    {
        public const string BootScene = "Boot", RangeScene = "TestRange";

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
            try
            {
                _setup.Starts = FlightWorlds.TestRange(AppData.Root).StartPoints;
            }
            catch (Exception e)
            {
                Debug.LogError("[App] the test range's start points could not be read: " + e.Message);
            }
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

        void Load(bool explore)
        {
            _explore = explore;
            _view = View.Loading;
            SceneManager.LoadSceneAsync(RangeScene, LoadSceneMode.Single);
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

        // After NoclipFeed's own hook (it subscribed before the first scene), so the range's rig is whole when switched off.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == RangeScene)
            {
                // Its Escape quits and its -smoketest quits after a frame: the app has its own.
                var boot = FindFirstObjectByType<TestRangeBoot>();
                if (boot != null) boot.enabled = false;
                if (_explore)
                {
                    _view = View.Explore;
                    Debug.Log("[App] explore: the test range's noclip and feed (V, O; F1-F4; 1-9); Esc for the menu");
                    return;
                }
                _flight = FlightScene.Attach(scene);
                if (_flight.Host.Inputs != null)
                {
                    _flight.Host.Inputs.Choices = _setup.Choices.Clone();
                    if (_smokeLogs != null) _flight.Host.Inputs.LogFolder = _smokeLogs;
                }
                _flight.SetupRequested += () =>
                {
                    if (_flight.Host.Inputs != null) _setup.Choices = _flight.Host.Inputs.Choices.Clone();
                    _setup.Notes.Clear();
                    _flight.Covered = true;
                    _view = View.Setup;
                };
                _flight.MenuRequested += Menu;
                _view = View.Flight;
            }
            else if (scene.name == BootScene)
            {
                _flight = null;
                if (_view == View.Loading) _view = View.Menu;
            }
        }

        void Update()
        {
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
                    GUI.Label(new Rect(AppStyle.S(150), Screen.height * 0.45f, Screen.width, AppStyle.S(40)), _explore ? "Loading the test range…" : "Loading the test range…  arm once the drone is in", AppStyle.Label);
                    break;
            }
        }

        // ---- The smoke test ----

        /// <summary>
        /// The player's smoke test, through the screens' own calls: the menu and the setup drawn; Explore in a review build;
        /// then a flight from the ground (legs on, the first start that is not the stand's) and one from the launch stand
        /// (legs off, the stand's start), the second set from the first's summary through Setup. With the development
        /// keyboard each flight is flown by a script of its keys: armed, climbed, paused (no step while paused),
        /// restarted (the stand's, before arming, from the pause: a new seed and log), descended, disarmed, and the summary
        /// read from the log. The keyboard is barred twice, as in a pilot's build without the radio: the stand's flight,
        /// set from the first's summary, waits at its start; its Restart ends it and waits. A pilot's build has no keyboard:
        /// there each flight is checked waiting for the radio. The flights log to the smoke test's own folder.
        /// V is pressed through the Input System (D-055) for each view's shot: at rest, in the air and after the crash, or
        /// while waiting for the radio; the keyboard's flights, flown with those presses, must replay bit for bit.
        /// </summary>
        IEnumerator Smoke()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-smokeshots");
            string shots = at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
            if (shots != null) Directory.CreateDirectory(shots);
            // Its flights log to a folder of its own (QA's note): beside the shots, or in the app's temporary folder.
            _smokeLogs = shots != null ? Path.Combine(shots, "flights") : Path.Combine(Application.temporaryCachePath, "smoketest-flights");
            Debug.Log("[Smoke] flight logs in " + _smokeLogs);
            // The smoke test's flights save their setup; the pilot's own is put back at the end.
            string setupFile = SetupStore.PathIn(Application.persistentDataPath);
            byte[] pilotSetup = File.Exists(setupFile) ? File.ReadAllBytes(setupFile) : null;
            int failed = 0, shot = 0;
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
                Transform eye = Camera.main != null ? Camera.main.transform : null;
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
            // A flight flown with V presses replays bit for bit on the world it names (D-055: the log never sees V).
            IEnumerator Replays(string log, string where)
            {
                SessionInputs inputs = _flight.Host.Inputs;
                Task<ReplayResult> replay = Task.Run(() =>
                {
                    using (var f = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        return FlightReplay.Run(f, id => id == inputs.WorldId ? inputs.World() : null, inputs.Drone.ContentHash);
                });
                yield return Until(() => replay.IsCompleted, 60);
                ReplayResult r = replay.IsCompleted && !replay.IsFaulted ? replay.Result : null;
                Check(r != null && r.Outcome == ReplayOutcome.Match, "the flight " + where + ", flown with V presses, replays bit for bit: " +
                      (r != null ? r.Message : replay.IsFaulted ? replay.Exception.GetBaseException().Message : "not done in 60 s"));
            }

            yield return Until(() => Time.frameCount > 3, 5);
            yield return new WaitForSecondsRealtime(0.5f);
            Check(_view == View.Menu, "the menu opens first");
            yield return Shot("menu");
            Train();
            yield return null;
            Check(_view == View.Setup && _setup.Starts.Length >= 2, "Train opens the setup, " + _setup.Starts.Length + " start points");
            yield return Shot("setup");

            if (AppData.Review)
            {
                Load(explore: true);
                yield return Until(() => _view == View.Explore, 60);
                Check(_view == View.Explore && Camera.main != null, "Explore loads the test range's noclip");
                yield return new WaitForSecondsRealtime(2);
                yield return Shot("explore");
                Menu();
                yield return Until(() => _view == View.Menu, 30);
                Check(_view == View.Menu, "Esc's route back to the menu");
                Train();
            }

            StartPoint ground = Array.Find(_setup.Starts, s => !s.Stand), stand = Array.Find(_setup.Starts, s => s.Stand);
            Check(ground.Name != null && stand.Name != null, "the range lists a ground start and a stand start");
            var starts = new[] { ground, stand };
            for (int n = 0; n < starts.Length; n++)
            {
                StartPoint start = starts[n];
                FlightChoices c = _setup.Choices;
                c.StartPoint = start.Name;
                c.Legs = !start.Stand;
                c.Payload = 1.0;
                c.Wind = WindLevel.Calm;
                c.Rain = Rain.Sunny;
                c.TimeOfDay = n == 0 ? 11.5 : 17.5;
                // QA's finding 15: the second flight is set from the first's summary. With the keyboard barred, as in a
                // pilot's build without the radio, it waits, and the view is the chosen start's, not the last flight's end.
                bool barred = n == 1 && _flight != null && _flight.Host.Inputs != null && _flight.Host.Inputs.KeyboardAllowed;
                if (barred) _flight.Host.Inputs.KeyboardAllowed = false;
                Fly();
                string where = "'" + start.Name + "' (legs " + (c.Legs ? "on" : "off, the stand") + ")";
                yield return Until(() => _view == View.Flight && _flight != null && _flight.Host.Session != null, 60);
                Check(_flight != null && _flight.Host.Session != null, "Fly loads the flight over the test range at " + where);
                if (_flight == null || _flight.Host.Session == null) break;
                FlightSessionHost host = _flight.Host;
                if (barred)
                {
                    yield return new WaitForSecondsRealtime(1);
                    Vector3 eye = Camera.main != null ? Camera.main.transform.position : Vector3.positiveInfinity;
                    // The lens over the start: its mount turned to the start's heading.
                    Vector3 lens = new Vector3((float)start.X, 0f, (float)start.Z) + Quaternion.Euler(0f, (float)(start.Yaw * 180 / Math.PI), 0f) * _flight.CameraMount;
                    Check(host.WaitingForRadio && Math.Abs(eye.x - lens.x) < 0.01 && Math.Abs(eye.z - lens.z) < 0.01 && eye.y < 0.5,
                          "after a flight, the wait for the radio views the chosen start " + where + " from " + eye.ToString("0.00"));
                    yield return Shot("waiting_after_flight_" + start.Name);
                    host.Inputs.KeyboardAllowed = true;
                }
                if (!host.Inputs.KeyboardAllowed)
                {
                    yield return new WaitForSecondsRealtime(2);
                    Check(host.WaitingForRadio || host.Session.State == SessionState.Flying, "the flight waits for the radio (or flies on it)");
                    yield return Shot("waiting_" + start.Name);
                    if (n == 0)
                    {
                        yield return PressV("while waiting at '" + start.Name + "'");
                        yield return Shot("waiting_chase_" + start.Name);
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
                if (start.Stand)
                {
                    // The pause's Restart, first without the radio (QA's finding 14): the flight ends by the pilot and the
                    // next waits for it; with the keyboard back it starts.
                    ulong seed = session.Seed;
                    string log = session.LogPath;
                    int flights = session.Flights;
                    host.Pause();
                    host.Inputs.KeyboardAllowed = false;
                    host.Restart();
                    yield return new WaitForSecondsRealtime(0.5f);
                    Check(host.WaitingForRadio && session.State == SessionState.Ended && session.End.Kind == FlightEndKind.Pilot,
                          "Restart without the radio: ended by the pilot, and the next flight waits for it");
                    host.Inputs.KeyboardAllowed = true;
                    yield return new WaitForSecondsRealtime(1);
                    Check(session.Flights == flights + 1 && session.Seed != seed && session.LogPath != log, "Restart: a new flight with a new seed and log");
                }
                double rest = _flight.Pose.Position.Y;
                Check(Math.Abs(rest - _flight.RestPose.Position.Y) < 0.01, "at rest at " + rest.ToString("0.000") + " m (start " + _flight.RestPose.Position.Y.ToString("0.000") + " m)");
                yield return Shot("rest_" + start.Name);
                yield return PressV("at rest at " + where);
                yield return Shot("chase_" + start.Name);
                yield return PressV("at rest at " + where);

                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.4f);
                Check(_flight.Pose.Armed, "armed by the keyboard's switch");
                double top = rest;
                Action climb = () => top = Math.Max(top, _flight.Pose.Position.Y);
                yield return Hold(KeyboardKeys.ThrottleUp, 1.6f, climb);
                yield return Hold(KeyboardKeys.None, 1.4f, climb);
                Check(top - rest > 1.0, "climbed " + (top - rest).ToString("0.00") + " m");
                yield return PressV("in the air");
                yield return Shot("air_chase_" + start.Name);
                yield return PressV("in the air");

                host.Pause();
                long steps = session.Core.State.Step;
                yield return new WaitForSecondsRealtime(0.3f);
                Check(session.State == SessionState.Paused && session.Core.State.Step == steps, "paused: no step for 0.3 s, at step " + steps);
                if (n == 0) yield return Shot("pause");
                host.Resume();
                yield return Hold(KeyboardKeys.ThrottleDown, 1.4f);
                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return Until(() => session.State == SessionState.Ended, 5);
                Check(session.State == SessionState.Ended, "the flight ends on the disarm");
                yield return Until(() => _flight.Summary != null, 30);
                PostFlight summary = _flight.Summary;
                Check(summary != null && summary.End.Kind == FlightEndKind.Disarm, "the summary from the log: " + (summary != null ? summary.Cause() : "none"));
                Check(summary != null && summary.Log.Events.Exists(e => e.Event.Kind == EventKind.Hitch && e.Event.Index == (byte)HitchCause.Pause), "the pause is in the log");
                yield return Shot("summary_" + start.Name);
                yield return Replays(session.LogPath, "from " + where);
                if (n == 0)
                {
                    _setup.Choices = host.Inputs.Choices.Clone();
                    _flight.Covered = true;
                    _view = View.Setup; // the summary's Setup
                }
            }

            // The keyboard's flight into the house beside the ground start (QA's finding 3 in a player): the summary names
            // the placed object it hit, from the map. A pilot's build has no keyboard and skips it.
            string lastStart = stand.Name;
            double lastHour = 17.5;
            if (_flight != null && _flight.Host.Inputs != null && _flight.Host.Inputs.KeyboardAllowed)
            {
                FlightChoices c = _setup.Choices;
                c.StartPoint = lastStart = ground.Name;
                c.Legs = true;
                c.TimeOfDay = lastHour = 14.0;
                Fly();
                FlightSessionHost host = _flight.Host;
                yield return Until(() => host.Session.State == SessionState.Flying && host.Session.Flights == _flight.ShownFlight && host.Session.Choices.StartPoint == ground.Name, 10);
                yield return new WaitForSecondsRealtime(1);
                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.4f);
                Check(_flight.Pose.Armed, "armed for the flight into the house");
                yield return IntoTheHouse();
                FlightSession session = host.Session;
                Check(session.State == SessionState.Ended && session.End.Kind == FlightEndKind.Crash, "the flight into the house ends in a crash: " + session.End.Kind);
                yield return PressV("after the crash");
                yield return Shot("crash_chase");
                yield return PressV("after the crash");
                yield return Until(() => _flight.Summary != null, 30);
                PostFlight summary = _flight.Summary;
                Check(summary != null && summary.HasCrash && summary.CrashObject == HouseBesideTrack,
                      "the summary names the house: " + (summary != null ? summary.Cause() : "none"));
                yield return Shot("summary_crash");
            }

            FlightChoices saved = SetupStore.Load(Application.persistentDataPath, out string error);
            Check(error == null && saved.StartPoint == lastStart && saved.Legs == (lastStart == ground.Name) && saved.TimeOfDay == lastHour,
                  "the last setup is saved for the next start");
            if (pilotSetup != null) File.WriteAllBytes(setupFile, pilotSetup);
            else File.Delete(setupFile);
            Menu();
            yield return Until(() => _view == View.Menu, 30);
            Check(_view == View.Menu, "Menu returns to the menu");
            Debug.Log("[Smoke] " + (failed == 0 && _errors == 0 ? "PASS" : "FAIL") + ": " + failed + " checks failed, " + _errors + " errors logged");
            Application.Quit(failed == 0 && _errors == 0 ? 0 : 1);
        }

        /// <summary>The house beside the ground start `track`, 6 m ahead and 14 m to the right, behind a 1.8 m fence; its
        /// north wall lies between headings 143° and 176° from the start.</summary>
        const string HouseBesideTrack = "Z01_house_adobe";

        // The smoke test's keyboard pilot into the house, on the flight's own pose. The throttle (W, S, which move it at
        // KeyboardSticks.ThrottlePerSecond) is steered to a level set by the height's and the climb's errors about the
        // hover (0.48 at 1 kg, assumed; tried on the session at 7 and 16 ms frames), holding 3 m over the start; D turns
        // to 158° once 2 m up, then the pitch key tilts it forward for 0.35 s, and it flies on until it hits.
        IEnumerator IntoTheHouse()
        {
            const double Height = 3, Heading = 158, Hover = 0.48;
            FlightSessionHost host = _flight.Host;
            FlightSession session = host.Session;
            KeyboardKeys keys = KeyboardKeys.None;
            host.Keys = () => keys;
            double rest = _flight.RestPose.Position.Y, throttle = 0; // the keyboard's throttle, as its keys move it
            float end = Time.realtimeSinceStartup + 15, pitchFrom = -1;
            while (session.State == SessionState.Flying && Time.realtimeSinceStartup < end)
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
                double want = Math.Max(0.3, Math.Min(0.7, Hover + (pitchFrom >= 0 ? 0.03 : 0) + 0.05 * (Height - h) - 0.06 * p.Velocity.Y));
                KeyboardKeys k = throttle < want - 0.01 ? KeyboardKeys.ThrottleUp : throttle > want + 0.01 ? KeyboardKeys.ThrottleDown : KeyboardKeys.None;
                if (pitchFrom < 0 && h > 2)
                {
                    if (Math.Abs(turn) > 3) k |= turn > 0 ? KeyboardKeys.YawRight : KeyboardKeys.YawLeft;
                    else pitchFrom = now;
                }
                if (pitchFrom >= 0 && now - pitchFrom < 0.35f) k |= KeyboardKeys.PitchForward;
                keys = k;
            }
            host.Keys = () => KeyboardKeys.None;
        }
    }
}
