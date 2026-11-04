using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fpv450.App.Session;
using Fpv450.Feed;
using Fpv450.Fiber;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.UI;
using Fpv450.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using Num = System.Numerics;

namespace Fpv450.App
{
    /// <summary>
    /// The flight over the test range (design 6, APP-04, D-047), put on the loaded TestRange scene by AppFlow. It switches
    /// the range's noclip rig off and takes over its look, then flies a FlightSessionHost:
    /// - the drone at the interpolated PoseFrame (PosePair.Interpolate on the host clock): p5-drone's D01 prefab for the
    ///   setup's legs (DroneModels) with D01_bottle on it at the payload's size, and D02, the launch stand, where the
    ///   flight's runtime object stands when the legs are off
    /// - the legs from the pose (p7g-legs 1.5, LegView): each turned by its set, a stub when broken, gone when off, its
    ///   skid half hanging or folded, a dropped piece where the pose puts it
    /// - the FPV camera at drone.json's camera.lens_m, 0° uptilt (FEED-02's rig, FpvCamera), through the analog feed and its
    ///   OSD, fed by the physics' signals (FlightFeedSignals)
    /// - the fiber (p7d-tether 1.14's FiberView): the line through the pose's nodes and its laid trail, and the coil's pack
    ///   at the line left on it, each flight's own, drawn at the pose the drone is drawn at
    /// - the setup's time of day and rain on the look (rain is the overcast preset's sky until the weather's look)
    /// - over the picture: the arming hint, the pause (Esc) and the post-flight summary, read from the log off the frame,
    ///   which shows only once the pilot has ended the flight (D-063); nothing else is drawn over a flight
    /// V switches between the FPV view and the third-person chase view in every build (D-055); only the camera moves,
    /// still through the feed and its OSD. Review builds add F (the feed off and on). R restarts the flight at once from
    /// the same setup and start point; C turns the feed's noise and X Betaflight's stick overlay on the OSD off and on,
    /// each for the rest of the app's run (D-078).
    /// In the FPV view the camera takes each PAL field at its own sim time (p7e-camera-feel): the scene is drawn once per
    /// whole field, with the camera at the field's reference instant on the flexing frame (FieldCamera), each line read
    /// through the field's rolling-shutter track, and held until the next field; the feed's fields follow the flight's.
    /// </summary>
    [DefaultExecutionOrder(100)] // after the host has ticked the session this frame
    public sealed class FlightScene : MonoBehaviour
    {
        public const string DroneName = "Svinoriz 10 Opto";

        /// <summary>The FPV camera's mount, body axes: drone.json's camera.lens_m (where the model draws the lens), looking
        /// along +z at 0° uptilt.</summary>
        public Vector3 CameraMount { get; private set; }

        /// <summary>The keys over the flight (D-055, D-078): V the view in every build, F the feed in review builds, R the
        /// restart, C the feed's noise, X the OSD's sticks. None is one of KeyboardFallback's, so the flight never sees them.</summary>
        public const Key ViewKey = Key.V, FeedKey = Key.F, RestartKey = Key.R, NoiseKey = Key.C, SticksKey = Key.X;

        /// <summary>The feed's noise (C) and the OSD's stick overlay (X), D-078: on and off at the app's start, then the
        /// pilot's for the rest of the run, across flights, restarts and the menu.</summary>
        public static bool FeedNoise { get; private set; } = true;
        public static bool StickOverlay { get; private set; }

        /// <summary>The chase view's offset behind and above the drone on its heading, m, and its pitch down, degrees.</summary>
        public static readonly Vector3 ChaseOffset = new Vector3(0f, 0.7f, -2.2f);
        const float ChasePitchDeg = 14f;

        /// <summary>The camera's height over the ground while a flight waits for the radio: about where the drone
        /// rests on its legs or on the stand (0.253 m), a preview only.</summary>
        const float PreviewHeightM = 0.25f;
        FlightChoices _previewed;

        public FlightSessionHost Host { get; private set; }

        /// <summary>The flight's camera, enabled or not: the FPV view holds a field by not rendering between fields
        /// (p7e-camera-feel), when Camera.main does not see it.</summary>
        public Transform Eye => _camera != null ? _camera.transform : null;

        /// <summary>The summary's Setup and Menu, for AppFlow.</summary>
        public event Action SetupRequested, MenuRequested;

        /// <summary>AppFlow draws the setup over the flight: draw nothing.</summary>
        public bool Covered;

        /// <summary>The third-person chase view (V), behind and above the drone; false: the FPV camera at the lens.</summary>
        public bool Chase;

        /// <summary>This frame's interpolated pose, and the current flight's first (rest) pose; Flights 0 before any.</summary>
        public PoseFrame Pose;
        public PoseFrame RestPose;
        public int ShownFlight { get; private set; }

        /// <summary>The current flight's drawn bottle (D01_bottle on the drone) and stand (D02); null when it has none.</summary>
        public Transform Bottle { get; private set; }
        public Transform Stand => _stand != null ? _stand.transform : null;

        /// <summary>The ended flight's summary once read; null while reading or flying.</summary>
        public PostFlight Summary { get; private set; }

        /// <summary>The OSD's content (p4-feed 1.5), as the feed draws it.</summary>
        public FlightOsd Osd => _signals.Osd;

        /// <summary>The fiber's drawing (p7d-tether 1.14): the current flight's line, trail and coil.</summary>
        public FiberView Fiber => _fiber;

        LookSetup _look;
        Transform _body;
        Camera _camera;
        FpvCamera _rig;
        FiberView _fiber;
        readonly LegView _legs = new LegView();
        readonly FlightFeedSignals _signals = new FlightFeedSignals();
        GameObject _model, _stand;
        DroneSpec _spec;
        DroneModels _models;
        bool _armedOnce;
        Task<PostFlight> _reading;
        int _readFor;
        List<string> _lines;
        string _title;

        // The field latch (p7e-camera-feel): the camera's rolling-shutter track, the history's samples it is built from,
        // the field last drawn, and the feed's field offset while the flight drives it.
        readonly RollingShutter _shutter = new RollingShutter();
        readonly Num.Quaternion[] _samples = new Num.Quaternion[RollingShutter.Samples];
        long _shownField = -1;
        int _fieldOffset;
        bool _driving;

        /// <summary>False with <c>-camerafeel 0</c>: the camera as before p7e-camera-feel, drawn every frame at the render
        /// clock's pose and read as rendered, the feed on its own clock (the before half of its A/B, task 1.7).</summary>
        public static readonly bool CameraFeel = CameraFeelArg();

        static bool CameraFeelArg()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-camerafeel");
            return !(i >= 0 && i + 1 < args.Length && args[i + 1] == "0");
        }

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
            var fiber = new GameObject("Fiber"); // unscaled, at the origin: its meshes sit in world m
            fiber.transform.SetParent(transform, false);
            _fiber = fiber.AddComponent<FiberView>();
            var go = new GameObject("FpvCamera") { tag = "MainCamera" };
            go.transform.SetParent(_body, false);
            _camera = go.AddComponent<Camera>();
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 3000f;
            _camera.clearFlags = CameraClearFlags.Skybox;
            _camera.allowHDR = true;
            _signals.Osd.StickOverlay = StickOverlay;
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
            _rig.Noise = FeedNoise;
            if (CameraFeel) _rig.Shutter = _shutter;
            else Debug.Log("[Flight] -camerafeel 0: the camera at the render clock's pose, read as rendered (p7e-camera-feel's before)");
            if (_look != null) _look.viewCamera = _camera;
        }

        void Start()
        {
            try
            {
                _spec = DroneSpec.Parse(System.IO.File.ReadAllText(AppData.File(AppData.DroneFile)));
                _signals.Osd.Cells = _spec.Packs[0].Series; // Propulsion.SeriesCells, PackLow's
                if (_spec.CameraLens is Double3 lens) CameraMount = V(lens);
                else Debug.LogError("[Flight] drone.json gives no camera.lens_m: the FPV camera sits at the body origin");
            }
            catch (Exception e)
            {
                Debug.LogError("[Flight] " + e.Message);
            }
            _models = DroneModels.Load();
            if (_models == null || _models.legsOn == null || _models.legsOff == null || _models.bottle == null || _models.stand == null)
                Debug.LogError("[Flight] no Resources/" + DroneModels.ResourceName + " with both D01 prefabs, D01_bottle and D02: run Fpv450.EditorSetup.ProjectSetup.Apply");
            _camera.transform.localPosition = CameraMount;
        }

        void LateUpdate()
        {
            FlightSession session = Host.Session;
            Keys();
            // The thin-wire rule at the feed's sample spacing while the feed reads the render (p7d-tether 1.14).
            FiberView.SetFeedRead(_rig != null && _rig.Running ? FiberCoverage.FeedRead(_rig.settings.K1, _rig.RenderCoverage) : Vector4.zero);
            if (session == null) return;
            if (Host.WaitingForRadio)
            {
                Unlatch();
                Preview(Host.Inputs);
                PlaceCamera(_camera.transform, _body, CameraMount, Chase);
                return;
            }
            if (session.Flights == 0) return;
            if (session.Flights != ShownFlight) Show(session);

            // The buffer is reused by every flight: a pair from before this flight's step 0 is the last flight's.
            ref readonly PosePair pair = ref session.Poses.Read();
            bool thisFlight = pair.StartUs >= session.StartUs;
            if (thisFlight)
            {
                pair.Interpolate(HostClock.Instance.NowUs(), out Pose);
                _body.SetPositionAndRotation(V(Pose.Position), Q(Pose.Attitude));
                _fiber.Draw(Pose);
                _legs.Draw(Pose);
                _armedOnce |= Pose.Armed;
                Double3 home = Pose.Position - RestPose.Position;
                _signals.Update(pair.Signals, Pose.Armed, Pose.Position.Y - RestPose.Position.Y, Math.Sqrt(home.X * home.X + home.Z * home.Z), Pose.TimeS);
            }
            PlaceCamera(_camera.transform, _body, CameraMount, Chase);
            Latch(session, thisFlight);
            if (_look != null) _look.UpdateFog(_camera);
            Cursor.visible = session.State != SessionState.Flying || Covered;
            Summarise(session);
        }

        /// <summary>The view (D-055): the camera, a child of the drone's body, at the lens (the mount, looking along +z),
        /// or behind and above the drone on its heading, pitched down. Only the camera moves: the feed and its OSD ride it.</summary>
        public static void PlaceCamera(Transform camera, Transform body, Vector3 mount, bool chase)
        {
            if (chase)
            {
                Vector3 forward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
                Quaternion heading = Quaternion.LookRotation(forward.normalized, Vector3.up);
                camera.SetPositionAndRotation(body.position + heading * ChaseOffset, heading * Quaternion.Euler(ChasePitchDeg, 0f, 0f));
            }
            else
            {
                camera.localPosition = mount;
                camera.localRotation = Quaternion.identity;
            }
        }

        /// <summary>The field to draw this frame (p7e-camera-feel): the newest whole field when it is not the one shown,
        /// else −1 (the shown field is held).</summary>
        public static long NewField(long latestStep, long shownField)
        {
            long field = FieldCamera.LatestField(latestStep);
            return field >= 0 && field != shownField ? field : -1;
        }

        // The field latch while this flight flies or pauses with the feed running (p7e-camera-feel, design 2): the feed at
        // the flight's field; in the FPV view the scene drawn once per whole field, the body and the camera at the field's
        // reference instant and its rows through the field's track; in the chase view every frame, read as rendered.
        void Latch(FlightSession session, bool thisFlight)
        {
            bool flying = CameraFeel && thisFlight && (session.State == SessionState.Flying || session.State == SessionState.Paused)
                && _rig != null && _rig.Running;
            long latest = flying ? FieldCamera.LatestField(session.Poses.LatestStep) : -1;
            if (latest < 0)
            {
                Unlatch();
                return;
            }
            if (!_driving)
            {
                _fieldOffset = _rig.Events.Field + 1 - (int)latest; // the feed runs on, never back
                _driving = _rig.Driven = true;
            }
            _rig.Drive(_fieldOffset + (int)latest);
            long field = NewField(session.Poses.LatestStep, _shownField);
            if (Chase)
            {
                _camera.enabled = true;
                if (field >= 0 && _shutter.Turning)
                {
                    _shutter.Clear();
                    _rig.UploadShutter();
                }
                _shownField = latest;
                return;
            }
            if (field < 0)
            {
                _camera.enabled = false; // the field shown is held: the feed reads the last render
                return;
            }
            _shownField = field;
            _camera.enabled = true;
            long start = FieldCamera.FieldStartUs(field);
            if (!FieldCamera.At(session.Poses, start + (long)RollingShutter.ReferenceUs, out PoseFrame at))
            {
                // The history has lost the field (a render stalled past it): this frame's pose, read as rendered.
                _shutter.Clear();
                _rig.UploadShutter();
                return;
            }
            // The camera renders at the mount on the rigid body; the frame's elastic turn, the reference row's included,
            // is in the track, so the camera's transform stays the lens's (D-055) and the read turns the picture.
            _body.SetPositionAndRotation(V(at.Position), Q(at.Attitude));
            _fiber.Draw(at); // the line at the field's instant, as the body
            _legs.Draw(at);
            FillSamples(session.Poses, start, at.Attitude);
            _shutter.Build(_samples, RollingShutter.ExposureS(_signals.SceneLuminance), _rig.settings.PixelsPerRad);
            _rig.UploadShutter();
        }

        // The camera on the flexing frame (FieldCamera.Attitude) against the rigid body's attitude at the field's reference
        // instant, at the history's steps from SampleLead before the field to its end; a
        // step the history does not hold (before the flight began) takes the nearest one it does, none the identity.
        void FillSamples(PoseBuffer poses, long fieldStartUs, Quat reference)
        {
            Quat inverse = reference.Conjugate();
            long first = fieldStartUs / FlightCore.StepUs - RollingShutter.SampleLead;
            Num.Quaternion held = Num.Quaternion.Identity;
            bool any = false;
            for (int i = 0; i < RollingShutter.Samples; i++)
            {
                if (poses.TryRead(first + i, out PoseFrame p))
                {
                    Quat q = inverse * FieldCamera.Attitude(p);
                    held = new Num.Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
                    if (!any)
                        for (int b = 0; b < i; b++) _samples[b] = held;
                    any = true;
                }
                _samples[i] = held;
            }
        }

        // Back to the render clock: the camera drawn every frame, read as rendered, the feed on its own clock.
        void Unlatch()
        {
            if (_camera != null) _camera.enabled = true;
            _shownField = -1;
            if (!_driving) return;
            _driving = false;
            if (_rig != null) _rig.Driven = false;
            if (_shutter.Turning)
            {
                _shutter.Clear();
                if (_rig != null) _rig.UploadShutter();
            }
        }

        // A new flight: its drone for the legs, its line and coil, its stand, its rest pose and its look. A restart (R, the
        // pause's) is a new flight: the last flight's line, trail and coil go with its drone.
        void Show(FlightSession session)
        {
            Unlatch();
            ShownFlight = session.Flights;
            _previewed = null; // the next wait for the radio previews again
            _armedOnce = false;
            Summary = null;
            _lines = null;
            _reading = null; // a read the pilot flew again before it finished is the last flight's
            _fiber.End(); // the coil's meshes back to the last model before it goes
            if (_model != null) Destroy(_model);
            if (_stand != null) Destroy(_stand);
            Bottle = null;
            GameObject prefab = _models != null ? _models.For(session.Loadout.Legs) : null;
            // Drawn in both views (the parts in the lens's frustum, and the chase view).
            _model = prefab != null ? Drawn(prefab, _body) : null;
            // Its legs from the pose (p7g-legs 1.5): a new model, so a restart's are drawn intact.
            LegDamageSpec legs = session.Core.Contacts?.Spec.Legs;
            _legs.Bind(_model, legs);
            if (legs != null && _model != null && _legs.Count == 0)
                Debug.LogWarning("[Flight] the drone model has no leg parts of their own (<variant>_LOD<n>_leg_rr and the rest): its legs are drawn as built, never damaged");
            // The flight's line from its start, and the coil's pack full on the model's own meshes, before the bottle goes on.
            _fiber.Begin(session.Core.Tether, session.Setup.Tether, _model, _spec != null ? CoilShape.D01(_spec) : default);
            if (_model != null)
            {
                _model.name = DroneName;
                // The bottle on its seat, sized by the payload's mass as the physics' part (D-050); none at 0 kg.
                float scale = DroneModels.BottleScale(session.Loadout.Payload);
                if (scale > 0 && _models.bottle != null)
                {
                    Bottle = Drawn(_models.bottle, _model.transform).transform;
                    Bottle.localPosition = DroneModels.BottleSeat;
                    Bottle.localScale = new Vector3(scale, scale, scale);
                }
            }
            foreach (RuntimeObject o in session.Setup.RuntimeObjects ?? new RuntimeObject[0])
                if (o.Name == LaunchStand.Name && _models != null && _models.stand != null)
                {
                    DroneModels.StandPose(o, out Vector3 foot, out Quaternion turn);
                    _stand = Drawn(_models.stand, transform);
                    _stand.transform.SetPositionAndRotation(foot, turn);
                }
            FlightSetup setup = session.Setup;
            RestPose = new PoseFrame { Position = setup.StartPosition, Attitude = setup.StartAttitude };
            Pose = RestPose;
            _body.SetPositionAndRotation(V(Pose.Position), Q(Pose.Attitude));
            _signals.Osd.Reset(); // a fresh pack: the filters and the charge used start again
            _signals.Osd.MinCheck = setup.Settings.min_check; // the throttle element's 0 %
            Look(session.Choices);
        }

        // A prefab's instance, drawn only: its colliders go, since PhysX stays out of the flight, whose contacts are
        // drone.json's and the runtime stand's.
        static GameObject Drawn(GameObject prefab, Transform parent)
        {
            GameObject go = Instantiate(prefab, parent, false);
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            return go;
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

        // While the radio is awaited (before the first flight, or after a Fly or a Restart): the view from the chosen
        // start at the chosen time, once for each choice the setup hands over and once after each flight. The last
        // flight's drone, line and stand go, and its OSD values. LateUpdate places the camera (V's view) every frame.
        void Preview(SessionInputs inputs)
        {
            FlightChoices c = inputs.Choices;
            if (ReferenceEquals(c, _previewed)) return;
            _previewed = c;
            _fiber.End();
            _legs.Bind(null, null);
            if (_model != null) Destroy(_model);
            if (_stand != null) Destroy(_stand);
            StartPoint at = Array.Find(inputs.StartPoints, s => s.Name == c.StartPoint);
            if (at.Name == null) at = inputs.StartPoints[0];
            _body.SetPositionAndRotation(new Vector3((float)at.X, PreviewHeightM, (float)at.Z), Quaternion.Euler(0f, (float)(at.Yaw * 180 / Math.PI), 0f));
            _signals.Osd.Reset(); // the OSD as before the first flight, disarmed, not the last flight's end
            _signals.Update(default, false, 0, 0, 0);
            Look(c);
        }

        LookPreset Preset(string name)
        {
            foreach (TextAsset asset in _look.presets)
                if (asset != null && asset.name == name)
                    return LookPreset.FromJson(asset.text, asset.name);
            return null;
        }

        // This frame's keys over the flight (LateUpdate reads them first): V the view in every build (D-055), F the feed
        // in review builds, R the restart, C the noise and X the OSD's sticks (D-078). None reaches the flight, whose keys
        // are KeyboardFallback's.
        void Keys()
        {
            Keyboard k = Keyboard.current;
            if (k == null || Covered) return;
            Pressed(k[ViewKey].wasPressedThisFrame, AppData.Review && k[FeedKey].wasPressedThisFrame, k[NoiseKey].wasPressedThisFrame,
                k[SticksKey].wasPressedThisFrame);
            if (k[RestartKey].wasPressedThisFrame) Restart();
        }

        /// <summary>
        /// R (D-078): the flight again at once, from the same setup and start point, with a new seed and log. A flight
        /// flying or paused ends by the pilot and the next starts, as the pause's Restart does (D-063: nothing else ends
        /// it); after End flight it is the summary's Fly again. A flight already waiting for the radio is left to wait.
        /// </summary>
        public void Restart()
        {
            FlightSession session = Host != null ? Host.Session : null;
            if (session == null || Host.WaitingForRadio) return;
            if (session.State == SessionState.Flying || session.State == SessionState.Paused) Host.Restart();
            else if (session.State == SessionState.Ended) Host.Fly();
        }

        /// <summary>What this frame's presses of the view, feed, noise and sticks keys do: the view flips between the lens
        /// and the chase view, the feed goes off or on, its noise off or on (the picture and the OSD stay), the OSD's stick
        /// overlay on or off. Nothing else.</summary>
        public void Pressed(bool view, bool feed, bool noise = false, bool sticks = false)
        {
            if (view) Chase = !Chase;
            if (feed && _rig != null) _rig.enabled = !_rig.enabled;
            if (noise) FeedNoise = !FeedNoise;
            if (sticks) StickOverlay = !StickOverlay;
            if (_rig != null) _rig.Noise = FeedNoise;
            _signals.Osd.StickOverlay = StickOverlay;
        }

        /// <summary>Esc over the flight (AppFlow routes it, one action a press): pause and resume, and the menu while no
        /// flight can start.</summary>
        public void Escape()
        {
            FlightSession session = Host.Session;
            if (session != null && session.State == SessionState.Flying) Host.Pause();
            else if (session != null && session.State == SessionState.Paused) Host.Resume();
            else if (session == null || Host.WaitingForRadio) MenuRequested?.Invoke();
        }

        // The ended flight's summary, read from its log on a worker thread (PostFlight reads the whole log).
        void Summarise(FlightSession session)
        {
            if (session.State != SessionState.Ended || _readFor == session.Flights) return;
            if (_reading == null)
            {
                string path = session.LogPath;
                SessionInputs inputs = Host.Inputs;
                _reading = Task.Run(() => PostFlight.Read(path, inputs.OpenWorld));
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
                case FlightEndKind.Pilot: return "Flight ended";
                case FlightEndKind.Fault: return "The flight stopped";
                default: return "The log was cut";
            }
        }

        void OnGUI()
        {
            if (Covered) return;
            FlightSession session = Host.Session;
            if (session == null)
            {
                if (Host.Problems.Count > 0) FlightScreens.Hint(Host.Problems[Host.Problems.Count - 1] + "  ·  Esc for the menu");
                return;
            }
            if (Host.WaitingForRadio)
            {
                FlightScreens.Hint("Connect the radio by USB, in joystick mode  ·  Esc for the menu");
                return;
            }
            switch (session.State)
            {
                case SessionState.Flying:
                    if (!_armedOnce) FlightScreens.Hint(ArmHint(session.Input) + "  ·  V: 1st / 3rd person  ·  Esc pauses");
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

        /// <summary>How the flight's input arms: the radio's switch (held off until seen off), or the development
        /// keyboard's Enter, a toggle, with its throttle (W up, S down) at the bottom.</summary>
        public static string ArmHint(InputSource input) => input == InputSource.Keyboard
            ? "Arm: Enter, with the throttle down (S)"
            : "Arm: switch off, then on, with the throttle down";

        void OnDisable() => Cursor.visible = true;

        static Vector3 V(in Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(in Quat q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
