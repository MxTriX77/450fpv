using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.UI;
using Fpv450.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Fpv450.App
{
    /// <summary>
    /// The flight over the test range (design 6, APP-04, D-047), put on the loaded TestRange scene by AppFlow. It switches
    /// the range's noclip rig off and takes over its look, then flies a FlightSessionHost:
    /// - the drone at the interpolated PoseFrame (PosePair.Interpolate on the host clock): the stand-in from drone.json's
    ///   parts at the flight's loadout, and the launch stand from the flight's runtime object when the legs are off
    /// - the FPV camera on the drone at 0° uptilt (FEED-02's rig, FpvCamera) through the analog feed and its OSD, fed by
    ///   the physics' signals (FlightFeedSignals)
    /// - the setup's time of day and rain on the look (rain is the overcast preset's sky until the weather's look)
    /// - over the picture: the arming hint, the pause (Esc) and the post-flight summary, read from the log off the frame.
    /// Review builds add C (a chase view that shows the stand-in) and V (the feed off and on).
    /// </summary>
    [DefaultExecutionOrder(100)] // after the host has ticked the session this frame
    public sealed class FlightScene : MonoBehaviour
    {
        /// <summary>The FPV camera's mount, body axes: the front of the stack between the plates (drone.json's electronics
        /// box, "the camera at the front"), looking along +z at 0° uptilt. Assumed until p5-drone's prefab.</summary>
        public static readonly Vector3 CameraMount = new Vector3(0f, 0.025f, 0.06f);

        /// <summary>The chase view's offset behind and above the drone, m, and its pitch down, degrees (review only).</summary>
        static readonly Vector3 ChaseOffset = new Vector3(0f, 0.7f, -2.2f);
        const float ChasePitchDeg = 14f;

        public FlightSessionHost Host { get; private set; }

        /// <summary>The summary's Setup and Menu, for AppFlow.</summary>
        public event Action SetupRequested, MenuRequested;

        /// <summary>AppFlow draws the setup over the flight: draw nothing.</summary>
        public bool Covered;

        /// <summary>The review chase view (C), which shows the stand-in (the FPV view draws only its shadow).</summary>
        public bool Chase
        {
            get => _chase;
            set
            {
                _chase = value;
                if (_model != null) StandIn.Visible(_model, value);
            }
        }
        bool _chase;

        /// <summary>This frame's interpolated pose, and the current flight's first (rest) pose; Flights 0 before any.</summary>
        public PoseFrame Pose;
        public PoseFrame RestPose;
        public int ShownFlight { get; private set; }

        /// <summary>The ended flight's summary once read; null while reading or flying.</summary>
        public PostFlight Summary { get; private set; }

        LookSetup _look;
        Transform _body;
        Camera _camera;
        FpvCamera _rig;
        readonly FlightFeedSignals _signals = new FlightFeedSignals();
        GameObject _model, _stand;
        DroneSpec _spec;
        bool _armedOnce;
        Task<PostFlight> _reading;
        int _readFor;
        List<string> _lines;
        string _title;

        /// <summary>
        /// Takes the loaded test range over for a flight; call it from SceneManager.sceneLoaded, before the scene's Start.
        /// The noclip rig (its camera, keys, boot and NoclipFeed) is switched off whole, and the look's own preset start
        /// with it, so the setup's time of day is the first sky drawn.
        /// </summary>
        public static FlightScene Attach(UnityEngine.SceneManagement.Scene scene)
        {
            var boot = FindFirstObjectByType<TestRangeBoot>();
            if (boot != null) boot.gameObject.SetActive(false);
            var go = new GameObject("Flight");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var flight = go.AddComponent<FlightScene>();
            flight._look = FindFirstObjectByType<LookSetup>();
            if (flight._look != null) flight._look.enabled = false;
            flight.Host = go.AddComponent<FlightSessionHost>();
            flight.Rig();
            return flight;
        }

        void Rig()
        {
            _body = new GameObject("Drone").transform;
            _body.SetParent(transform, false);
            var go = new GameObject("FpvCamera") { tag = "MainCamera" };
            go.transform.SetParent(_body, false);
            go.transform.localPosition = CameraMount;
            _camera = go.AddComponent<Camera>();
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 3000f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.allowHDR = true;
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            go.AddComponent<AudioListener>();
            FeedSettings settings = FeedSettings.Load();
            if (settings == null)
            {
                Debug.LogError("[Flight] no Resources/FeedSettings asset; the feed is off.");
                return;
            }
            _rig = go.AddComponent<FpvCamera>(); // it begins at Start, once it has its settings and signals
            _rig.settings = settings;
            _rig.Signals = _signals;
            if (_look != null) _look.viewCamera = _camera;
        }

        void Start()
        {
            try
            {
                _spec = DroneSpec.Parse(System.IO.File.ReadAllText(AppData.File(AppData.DroneFile)));
            }
            catch (Exception e)
            {
                Debug.LogError("[Flight] " + e.Message);
            }
        }

        void LateUpdate()
        {
            FlightSession session = Host.Session;
            Keys(session);
            if (session == null || session.Flights == 0) return;
            if (session.Flights != ShownFlight) Show(session);

            // The buffer is reused by every flight: a pair from before this flight's step 0 is the last flight's.
            ref readonly PosePair pair = ref session.Poses.Read();
            if (pair.StartUs >= session.StartUs)
            {
                pair.Interpolate(HostClock.Instance.NowUs(), out Pose);
                _body.SetPositionAndRotation(V(Pose.Position), Q(Pose.Attitude));
                _armedOnce |= Pose.Armed;
                Double3 home = Pose.Position - RestPose.Position;
                _signals.Update(pair.Signals, Pose.Position.Y - RestPose.Position.Y, Math.Sqrt(home.X * home.X + home.Z * home.Z), Pose.TimeS);
            }
            if (Chase)
            {
                Vector3 forward = Vector3.ProjectOnPlane(_body.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
                Quaternion heading = Quaternion.LookRotation(forward.normalized, Vector3.up);
                _camera.transform.SetPositionAndRotation(_body.position + heading * ChaseOffset, heading * Quaternion.Euler(ChasePitchDeg, 0f, 0f));
            }
            else
            {
                _camera.transform.localPosition = CameraMount;
                _camera.transform.localRotation = Quaternion.identity;
            }
            if (_look != null) _look.UpdateFog(_camera);
            Cursor.visible = session.State != SessionState.Flying || Covered;
            Summarise(session);
        }

        // A new flight: its stand-in at the loadout, its stand, its rest pose and its look.
        void Show(FlightSession session)
        {
            ShownFlight = session.Flights;
            _armedOnce = false;
            Summary = null;
            _lines = null;
            _reading = null; // a read the pilot flew again before it finished is the last flight's
            if (_model != null) Destroy(_model);
            if (_stand != null) Destroy(_stand);
            if (_spec != null)
            {
                _model = StandIn.Drone(_spec, session.Loadout);
                _model.transform.SetParent(_body, false);
                StandIn.Visible(_model, Chase);
            }
            foreach (RuntimeObject o in session.Setup.RuntimeObjects ?? new RuntimeObject[0])
                if (o.Name == LaunchStand.Name)
                {
                    _stand = StandIn.Boxes(o, _spec != null ? _spec.Stand.Material : "steel");
                    _stand.transform.SetParent(transform, false);
                }
            FlightSetup setup = session.Setup;
            RestPose = new PoseFrame { Position = setup.StartPosition, Attitude = setup.StartAttitude };
            Pose = RestPose;
            _body.SetPositionAndRotation(V(Pose.Position), Q(Pose.Attitude));
            Look(session.Choices);
        }

        void Look(FlightChoices c)
        {
            _signals.Stub.TimeOfDayHours = (float)c.TimeOfDay;
            if (_look == null) return;
            LookPreset sky = Preset(c.Rain == Rain.Sunny ? "day" : "overcast");
            _look.hour = (float)c.TimeOfDay;
            if (sky != null)
            {
                _look.cloudCover = sky.cloud_cover;
                _look.fogExtinctionPerKm = sky.fog_extinction_per_km;
                _look.fogScaleHeightM = sky.fog_scale_height_m;
            }
            _look.Apply();
            Debug.Log($"[Flight] look: {SetupScreen.Clock(c.TimeOfDay)}, {c.Rain} as the {(sky != null ? sky.name : "scene's")} sky, cover {_look.cloudCover:0.##}, " +
                      $"sun {_look.State.SunElevationDeg:0.0}° up");
        }

        LookPreset Preset(string name)
        {
            foreach (TextAsset asset in _look.presets)
                if (asset != null && asset.name == name)
                    return LookPreset.FromJson(asset.text, asset.name);
            return null;
        }

        void Keys(FlightSession session)
        {
            Keyboard k = Keyboard.current;
            if (k == null || Covered) return;
            if (k.escapeKey.wasPressedThisFrame && session != null)
            {
                if (session.State == SessionState.Flying) Host.Pause();
                else if (session.State == SessionState.Paused) Host.Resume();
            }
            if (!AppData.Review) return;
            if (k.cKey.wasPressedThisFrame) Chase = !Chase;
            if (k.vKey.wasPressedThisFrame && _rig != null) _rig.enabled = !_rig.enabled;
        }

        // The ended flight's summary, read from its log on a worker thread (PostFlight reads the whole log).
        void Summarise(FlightSession session)
        {
            if (session.State != SessionState.Ended || _readFor == session.Flights) return;
            if (_reading == null)
            {
                string path = session.LogPath;
                _reading = Task.Run(() => PostFlight.Read(path));
                return;
            }
            if (!_reading.IsCompleted) return;
            _readFor = session.Flights;
            if (_reading.IsFaulted)
            {
                _title = "The log could not be read";
                _lines = new List<string> { _reading.Exception?.GetBaseException().Message ?? "", "log " + session.LogPath };
                Debug.LogError("[Flight] summary: " + _lines[0]);
            }
            else
            {
                Summary = _reading.Result;
                _title = Title(Summary.End.Kind);
                _lines = Summary.Lines();
                Debug.Log("[Flight] summary of flight " + session.Flights + ": " + string.Join(" | ", _lines));
            }
            _reading = null;
        }

        static string Title(FlightEndKind kind)
        {
            switch (kind)
            {
                case FlightEndKind.Crash: return "Crashed";
                case FlightEndKind.UpsideDown: return "Upside down";
                case FlightEndKind.BatteryCutoff: return "Battery cut-off";
                case FlightEndKind.Failsafe: return "Failsafe";
                case FlightEndKind.Disarm: return "Disarmed";
                case FlightEndKind.Pilot: return "Flight ended";
                case FlightEndKind.Fault: return "The flight stopped";
                default: return "The log was cut";
            }
        }

        void OnGUI()
        {
            if (Covered) return;
            FlightSession session = Host.Session;
            if (Chase) FlightScreens.Corner("Chase view (C) · " + StandIn.DroneName);
            if (session == null)
            {
                if (Host.Problems.Count > 0) FlightScreens.Hint(Host.Problems[Host.Problems.Count - 1]);
                return;
            }
            switch (session.State)
            {
                case SessionState.Ready:
                    if (Host.WaitingForRadio) FlightScreens.Hint("Connect the radio by USB, in joystick mode");
                    break;
                case SessionState.Flying:
                    if (!_armedOnce) FlightScreens.Hint("Arm: switch off, then on, with the throttle down  ·  Esc pauses");
                    break;
                case SessionState.Paused:
                    switch (FlightScreens.Pause())
                    {
                        case PauseChoice.Resume: Host.Resume(); break;
                        case PauseChoice.Restart: Host.Restart(); break;
                        case PauseChoice.End: session.EndFlight(); break;
                    }
                    break;
                case SessionState.Ended:
                    switch (FlightScreens.Summary(_lines != null ? _title : "Flight over", _lines))
                    {
                        case SummaryChoice.FlyAgain: Host.Fly(); break;
                        case SummaryChoice.Setup: SetupRequested?.Invoke(); break;
                        case SummaryChoice.Menu: MenuRequested?.Invoke(); break;
                    }
                    break;
            }
        }

        void OnDisable() => Cursor.visible = true;

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(in Quat q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
