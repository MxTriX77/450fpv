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
    /// Options: <c>-windowed</c> (a 1920 × 1080 window), <c>-vsync &lt;n&gt;</c> (as the range's boot).
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

        /// <summary>Boot's: makes the app once; the Boot scene loads again for the menu and finds it.</summary>
        public static void Create()
        {
            if (_instance != null) return;
            string[] args = Environment.GetCommandLineArgs();
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
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
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
    }
}
