using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.UI;
using Fpv450.World;
using UnityEngine;
using UnityEngine.InputSystem;
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
                if (_flight.Host.Inputs != null) _flight.Host.Inputs.Choices = _setup.Choices.Clone();
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
        /// restarted (the stand's, before arming: a new seed and log), descended, disarmed, and the summary read from the
        /// log. A pilot's build has no keyboard: there each flight is checked waiting for the radio.
        /// </summary>
        IEnumerator Smoke()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-smokeshots");
            string shots = at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
            if (shots != null) Directory.CreateDirectory(shots);
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
                Fly();
                string where = "'" + start.Name + "' (legs " + (c.Legs ? "on" : "off, the stand") + ")";
                yield return Until(() => _view == View.Flight && _flight != null && _flight.Host.Session != null, 60);
                Check(_flight != null && _flight.Host.Session != null, "Fly loads the flight over the test range at " + where);
                if (_flight == null || _flight.Host.Session == null) break;
                FlightSessionHost host = _flight.Host;
                if (!host.Inputs.KeyboardAllowed)
                {
                    yield return new WaitForSecondsRealtime(2);
                    Check(host.WaitingForRadio || host.Session.State == SessionState.Flying, "the flight waits for the radio (or flies on it)");
                    yield return Shot("waiting_" + start.Name);
                    if (n == 0)
                    {
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
                    ulong seed = session.Seed;
                    string log = session.LogPath;
                    int flights = session.Flights;
                    host.Restart();
                    yield return new WaitForSecondsRealtime(1);
                    Check(session.Flights == flights + 1 && session.Seed != seed && session.LogPath != log, "Restart: a new flight with a new seed and log");
                }
                double rest = _flight.Pose.Position.Y;
                Check(Math.Abs(rest - _flight.RestPose.Position.Y) < 0.01, "at rest at " + rest.ToString("0.000") + " m (start " + _flight.RestPose.Position.Y.ToString("0.000") + " m)");
                yield return Shot("rest_" + start.Name);
                _flight.Chase = true;
                yield return null;
                yield return Shot("chase_" + start.Name);
                _flight.Chase = false;

                yield return Hold(KeyboardKeys.ArmToggle, 0.1f);
                yield return new WaitForSecondsRealtime(0.4f);
                Check(_flight.Pose.Armed, "armed by the keyboard's switch");
                double top = rest;
                Action climb = () => top = Math.Max(top, _flight.Pose.Position.Y);
                yield return Hold(KeyboardKeys.ThrottleUp, 1.6f, climb);
                yield return Hold(KeyboardKeys.None, 1.4f, climb);
                Check(top - rest > 1.0, "climbed " + (top - rest).ToString("0.00") + " m");
                _flight.Chase = true;
                yield return null;
                yield return Shot("air_chase_" + start.Name);
                _flight.Chase = false;

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
                if (n == 0)
                {
                    _setup.Choices = host.Inputs.Choices.Clone();
                    _flight.Covered = true;
                    _view = View.Setup; // the summary's Setup
                }
            }

            FlightChoices saved = SetupStore.Load(Application.persistentDataPath, out string error);
            Check(error == null && saved.StartPoint == stand.Name && !saved.Legs, "the last setup is saved for the next start");
            if (pilotSetup != null) File.WriteAllBytes(setupFile, pilotSetup);
            else File.Delete(setupFile);
            Menu();
            yield return Until(() => _view == View.Menu, 30);
            Check(_view == View.Menu, "Menu returns to the menu");
            Debug.Log("[Smoke] " + (failed == 0 && _errors == 0 ? "PASS" : "FAIL") + ": " + failed + " checks failed, " + _errors + " errors logged");
            Application.Quit(failed == 0 && _errors == 0 ? 0 : 1);
        }
    }
}
