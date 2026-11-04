using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Fpv450.Feed
{
    /// <summary>
    /// The feed on the test range's noclip camera (p4-feed design 8, task 1.8, D-044): when the test range loads, this
    /// goes on its main camera with an <see cref="FpvCamera"/> fed by <see cref="NoclipFeedSignals"/>, so the scene
    /// file stays the world's. The feed is on by default; V toggles it (GAL-02) and O toggles the review overlay,
    /// every signal's value and source beside the feed's state, drawn over the picture, never through it.
    /// </summary>
    /// <remarks>
    /// Options: <c>-feed 0</c> starts with the feed off (the noclip row, clean captures); <c>-feedwidth &lt;n&gt;</c>
    /// sets the render's samples across the picture (<see cref="FeedSettings.sceneWidth"/>, task 1.8's render-size
    /// runs); <c>-feedcost</c> runs the cost scenario (PLT-07: 2 s warm-up, 30 s measured at the start view, the feed
    /// passes' GPU time from their "Feed" profiling sampler, so it needs a development build) and quits, exit code 1
    /// over FEED-10's 1.0 ms. With <c>-feedshutter &lt;s&gt;</c> it reads through the camera's rolling shutter
    /// (p7e-camera-feel 1.7): a track for the pilot's full-stick yaw, 300 °/s (fc-profiles.md), at that exposure, so
    /// daylight's 1/8192 s reads one tap a row and dusk's 1/50 s eight, the flight's worst case.
    /// </remarks>
    [DefaultExecutionOrder(-100)] // samples the camera's pose after every mover and before the rig steps its field
    public sealed class NoclipFeed : MonoBehaviour
    {
        const string SceneName = "TestRange";
        const float WarmUpSeconds = 2f, CostSeconds = 30f; // PLT-07
        const float BudgetMs = 1f;                         // FEED-10
        const double ShutterYawDegPerS = 300;             // fc-profiles.md, the pilot's full stick

        FpvCamera rig;
        NoclipFeedSignals signals;
        RollingShutter shutter;
        float shutterExposureS;
        bool overlay;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName)
                return;
            Camera camera = Camera.main;
            if (camera == null || camera.gameObject.scene != scene)
                Debug.LogError($"[Feed] {SceneName} has no main camera; the feed is off.");
            else if (camera.GetComponent<FpvCamera>() == null)
                camera.gameObject.AddComponent<NoclipFeed>();
        }

        void Awake()
        {
            FeedSettings settings = FeedSettings.Load();
            if (settings == null)
            {
                Debug.LogError("[Feed] no Resources/FeedSettings asset; the feed is off.");
                enabled = false;
                return;
            }
            string[] args = Environment.GetCommandLineArgs();
            int width = IntArg(args, "-feedwidth", settings.sceneWidth);
            if (width != settings.sceneWidth)
            {
                settings = Instantiate(settings);
                settings.sceneWidth = width;
            }
            signals = new NoclipFeedSignals(transform);
            rig = gameObject.AddComponent<FpvCamera>(); // it waits for its settings and begins at Start
            rig.settings = settings;
            rig.Signals = signals;
            int at = Array.IndexOf(args, "-feedshutter");
            if (at >= 0 && at + 1 < args.Length && float.TryParse(args[at + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out shutterExposureS) && shutterExposureS > 0f)
            {
                shutter = new RollingShutter();
                rig.Shutter = shutter;
            }
            rig.enabled = IntArg(args, "-feed", 1) != 0;
            if (!rig.enabled)
                Debug.Log("[Feed] off at start (-feed 0); V turns it on.");
        }

        IEnumerator Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-feedcost") >= 0)
                yield return Cost();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.vKey.wasPressedThisFrame)
            {
                rig.enabled = !rig.enabled;
                if (!rig.enabled)
                    Debug.Log("[Feed] off");
            }
            if (keyboard.oKey.wasPressedThisFrame)
                overlay = !overlay;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            signals.Sample(dt);
            if (!rig.Running)
                return;
            // The stub OSD's altitude, distance and timer, from the free camera as the Godot stand-in took them
            // (FeedSignals.cs:43-58); the OSD content model replaces them (task 1.5).
            StubOsd osd = signals.Stub.Osd;
            Vector3 p = transform.position;
            osd.AltitudeM = p.y;
            osd.DistanceM = new Vector2(p.x, p.z).magnitude;
            osd.ElapsedSeconds += dt;
        }

        /// The cost scenario (analog-feed spec, "The cost on AC"): the GPU time of the feed's three passes per frame.
        IEnumerator Cost()
        {
            yield return null; // the rig has begun (its Start), with the identity track
            if (shutter != null)
            {
                shutter.Build(YawSamples(ShutterYawDegPerS), shutterExposureS, rig.settings.PixelsPerRad);
                rig.UploadShutter();
                Debug.Log($"[Feed] cost: the rolling shutter's read, a {ShutterYawDegPerS:0} °/s yaw at a {shutterExposureS * 1e3:0.###} ms exposure, " +
                          $"{shutter.TapsOf(0)} to {shutter.TapsOf(RollingShutter.Rows - 1)} taps a row, the render covering {rig.RenderCoverage:0.000} half-widths");
            }
            yield return new WaitForSecondsRealtime(WarmUpSeconds);
            Recorder feed = Recorder.Get("Feed");
            if (!feed.isValid || !SystemInfo.supportsGpuRecorder)
            {
                Debug.LogError($"[Feed] cost: no GPU recorder for the feed's sampler (valid {feed.isValid}, GPU recorder {SystemInfo.supportsGpuRecorder}); it needs a development build.");
                Application.Quit(1);
                yield break;
            }
            feed.enabled = true;
            var gpu = new List<float>(8192);
            int frames = 0, blocks = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < CostSeconds)
            {
                yield return null;
                frames++;
                if (feed.gpuSampleBlockCount > 0)
                {
                    gpu.Add(feed.gpuElapsedNanoseconds * 1e-6f);
                    blocks += feed.gpuSampleBlockCount;
                }
            }
            float seconds = Time.realtimeSinceStartup - start;
            if (gpu.Count == 0)
            {
                Debug.LogError("[Feed] cost: the feed's sampler recorded no GPU time.");
                Application.Quit(1);
                yield break;
            }
            gpu.Sort();
            float mean = 0f;
            foreach (float ms in gpu)
                mean += ms;
            mean /= gpu.Count;
            Vector2Int size = rig.settings.SceneSizeFor(rig.RenderCoverage);
            Debug.Log($"[Feed] cost: {gpu.Count} of {frames} frames over {seconds:0.0} s, {(float)blocks / gpu.Count:0.0} passes a frame, the feed's GPU time mean {mean:0.000} ms, " +
                      $"median {gpu[gpu.Count / 2]:0.000}, 99th percentile {gpu[Mathf.Clamp(Mathf.CeilToInt(0.99f * gpu.Count) - 1, 0, gpu.Count - 1)]:0.000}, " +
                      $"max {gpu[gpu.Count - 1]:0.000} ms ({(mean <= BudgetMs ? "within" : "OVER")} FEED-10's {BudgetMs:0.0} ms); " +
                      $"{frames / seconds:0.0} fps; render {size.x}x{size.y}, field {rig.settings.fieldWidth}x{FeedSettings.FieldRows}, " +
                      $"output {Screen.width}x{Screen.height}, vsync {QualitySettings.vSyncCount}, GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})");
            Application.Quit(mean <= BudgetMs ? 0 : 1);
        }

        void OnGUI()
        {
            if (!overlay)
                return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(12, Screen.height / 64), wordWrap = false };
            var all = (FeedSignal[])Enum.GetValues(typeof(FeedSignal));
            float em = style.fontSize, line = Mathf.Round(em * 1.3f), x = 24f, y = Mathf.Round(Screen.height * 0.1f);
            float valueX = x + 12f * em, sourceX = x + 34f * em;
            var box = new Rect(x - 8f, y - 6f, 40f * em, line * (all.Length + 2.5f) + 12f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, box.width, 2f * line), Header(), style);
            y += Mathf.Round(line * 2.5f);
            foreach (FeedSignal s in all)
            {
                GUI.Label(new Rect(x, y, valueX - x, line), s.ToString(), style);
                GUI.Label(new Rect(valueX, y, sourceX - valueX, line), FeedSignalLog.Value(signals, s), style);
                GUI.Label(new Rect(sourceX, y, box.xMax - sourceX, line), signals.SourceOf(s).ToString().ToLowerInvariant(), style);
                y += line;
            }
        }

        string Header()
        {
            if (!rig.Running)
                return "Feed off (V)";
            FeedSettings s = rig.settings;
            Vector2Int size = s.SceneSize;
            return $"Feed on (V)   seed {s.seed}   field {rig.Events.Field}   {rig.Events.Loss.Current}   quality drop {rig.Events.Degrade:0.00}\n" +
                   $"render {size.x}x{size.y}, analog pass {s.fieldWidth}x{FeedSettings.FieldRows}";
        }

        // The camera turning about its own up axis at degPerS, at the track's samples against the field's reference instant.
        static System.Numerics.Quaternion[] YawSamples(double degPerS)
        {
            var q = new System.Numerics.Quaternion[RollingShutter.Samples];
            for (int i = 0; i < q.Length; i++)
            {
                double us = (i - RollingShutter.SampleLead) * RollingShutter.SampleUs - RollingShutter.ReferenceUs;
                double half = 0.5 * degPerS * Math.PI / 180.0 * us * 1e-6;
                q[i] = new System.Numerics.Quaternion(0f, (float)Math.Sin(half), 0f, (float)Math.Cos(half));
            }
            return q;
        }

        static int IntArg(string[] args, string name, int fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int value) ? value : fallback;
        }
    }
}
