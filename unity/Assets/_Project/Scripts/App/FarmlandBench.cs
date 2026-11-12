using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Fpv450.Feed;
using Fpv450.Input;
using Unity.Profiling;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The farmland's frame-rate benchmark (w6-farmland-look 1.2, PLT-07). A review build started with
    /// <c>-farmbench &lt;file&gt;</c> opens Explore as the menu does, then flies the camera along <see cref="Path"/>: west
    /// through the village's lane at 4 m, up and across the village at 30 m, along the tree belt on field F8's west edge
    /// at 5 m and over the open field F7 at 15 m, at <see cref="Speed"/>. It flies the path once per run of
    /// <c>-benchruns</c> (default <c>feed,noclip</c>: through the feed, then in noclip with the feed off), each holding the
    /// path's first pose for the 2 s warm-up and measuring the whole path. It logs a line per run, writes the runs to the
    /// file as JSON and quits. <c>tools/bench_farmland.ps1</c> runs it with vsync off and adds what only the OS sees: the
    /// power mode, the clocks, the process's VRAM and RAM.
    /// </summary>
    /// <remarks>
    /// The 1 % low is the 99th-percentile frame's rate, as the test range's bench (<c>TestRangeBoot</c>). The GPU time is
    /// <see cref="FrameTimingManager"/>'s, which needs the player's frame timing stats. With the feed on it covers only
    /// the feed's output camera, not the FPV camera's render into its own target (w6-farmland-look 1.1, measured), so a
    /// GPU-bound run's GPU time is its frame time: the CPU's main and render thread times say whether it is GPU-bound.
    /// </remarks>
    public static class FarmlandBench
    {
        /// <summary>A point of the path: the scene's x and z (m), the camera's height over the terrain (m), and its pitch
        /// down (degrees), each interpolated along the segment to the next.</summary>
        public readonly struct Waypoint
        {
            public readonly float X, Z, Height, Pitch;
            public Waypoint(float x, float z, float height, float pitch) { X = x; Z = z; Height = height; Pitch = pitch; }
        }

        /// <summary>The path, in the scene's frame (x east, z north, m; <c>maps/farmland/outlines.json</c>), 964 m.</summary>
        public static readonly Waypoint[] Path =
        {
            new Waypoint(610f, 601f, 4f, 5f),   // east of the village, on the lane's line
            new Waypoint(440f, 601f, 4f, 5f),   // through the lane between the five ruins
            new Waypoint(420f, 545f, 30f, 15f), // climbing past the village's west edge
            new Waypoint(560f, 650f, 30f, 25f), // across the village, looking down on the ruins
            new Waypoint(540f, 685f, 5f, 5f),   // down to the tree belt's south end
            new Waypoint(487f, 950f, 5f, 5f),   // along the belt on F8's west edge, 12 m off it
            new Waypoint(250f, 870f, 15f, 8f),  // west over the open field F7
        };

        /// <summary>The camera's speed along the path, m/s: a cargo quad's cruise.</summary>
        public const float Speed = 15f;
        const float WarmUpSeconds = 2f;   // PLT-07
        const float TurnSeconds = 1f;     // the heading eases to a new segment's over this
        const float GroundStep = 1f;      // the terrain's height is sampled along the path every this many metres

        /// <summary>One flight of the path, measured.</summary>
        public sealed class Run
        {
            public string Name;
            public bool Feed;
            public int Frames;
            public float Seconds, AverageFps, LowFps, P99Ms, WorstMs, MeanMs;
            public float CpuMainMs, CpuRenderMs, GpuMs;
            public int GpuSamples;
            public float DrawCalls, Batches, SetPass, TrianglesM;
            public long DrawCallsMax, TrianglesMax;
            public float SystemUsedMB;
            public string Render;
        }

        /// <summary>Explore is open: flies the path once per run, writes the file and quits (exit 1 when Explore never
        /// came or the camera is missing).</summary>
        public static IEnumerator Main(float loadSeconds)
        {
            string[] args = Environment.GetCommandLineArgs();
            string file = Arg(args, "-farmbench");
            string[] runs = (Arg(args, "-benchruns") ?? "feed,noclip").Split(',');
            Camera camera = Camera.main;
            FpvCamera rig = camera != null ? camera.GetComponent<FpvCamera>() : null;
            if (loadSeconds < 0f || rig == null || file == null)
            {
                Debug.LogError($"[Bench] the farmland's Explore never came (load {loadSeconds:0.00} s, FPV rig {(rig != null ? "found" : "missing")}, file {file ?? "none"})");
                Application.Quit(1);
                yield break;
            }
            Debug.Log($"[Bench] the farmland's first frame {loadSeconds:0.00} s after Explore (PLT-06: 20 s); {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                      $"{SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB, battery {SystemInfo.batteryStatus}, {Screen.width}x{Screen.height} {Screen.fullScreenMode}, " +
                      $"vsync {QualitySettings.vSyncCount}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, frame timing stats {FrameTimingManager.IsFeatureEnabled()}");
            var results = new List<Run>();
            foreach (string run in runs)
                yield return Fly(camera, rig, run.Trim(), run.Trim() != "noclip", results);
            File.WriteAllText(file, ToJson(loadSeconds, results));
            Debug.Log("[Bench] written to " + file);
            Application.Quit(0);
        }

        /// <summary>Flies the path once with the feed on or off, appends its measurement to <paramref name="results"/>,
        /// and gives the camera back to the noclip as it found it.</summary>
        public static IEnumerator Fly(Camera camera, FpvCamera rig, string name, bool feed, List<Run> results)
        {
            var noclip = camera.GetComponent<NoclipCamera>();
            bool noclipWas = noclip != null && noclip.enabled;
            if (noclip != null) noclip.enabled = false;
            rig.enabled = feed;

            Track track = new Track(camera.gameObject.scene);
            var frames = new List<float>(16384);
            var timings = new FrameTiming[1];
            double cpuMain = 0, cpuRender = 0, gpu = 0, drawCalls = 0, batches = 0, setPass = 0, triangles = 0;
            long drawCallsMax = 0, trianglesMax = 0, systemMax = 0;
            int timed = 0, counted = 0;
            using (var rDraw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
            using (var rBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
            using (var rSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
            using (var rTriangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
            using (var rSystem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory"))
            {
                track.Pose(0f, out Vector3 p0, out Quaternion q0);
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < WarmUpSeconds)
                {
                    camera.transform.SetPositionAndRotation(p0, q0);
                    yield return null;
                }
                start = Time.realtimeSinceStartup;
                for (float t = 0f; t <= track.Seconds; t = Time.realtimeSinceStartup - start)
                {
                    track.Pose(t, out Vector3 p, out Quaternion q);
                    camera.transform.SetPositionAndRotation(p, q);
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime);
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, timings) == 1 && timings[0].cpuFrameTime > 0)
                    {
                        cpuMain += timings[0].cpuMainThreadFrameTime;
                        cpuRender += timings[0].cpuRenderThreadFrameTime;
                        gpu += timings[0].gpuFrameTime;
                        timed++;
                    }
                    if (rDraw.Valid && rDraw.LastValue > 0)
                    {
                        drawCalls += rDraw.LastValue;
                        batches += rBatches.LastValue;
                        setPass += rSetPass.LastValue;
                        triangles += rTriangles.LastValue;
                        drawCallsMax = Math.Max(drawCallsMax, rDraw.LastValue);
                        trianglesMax = Math.Max(trianglesMax, rTriangles.LastValue);
                        counted++;
                    }
                    systemMax = Math.Max(systemMax, rSystem.Valid ? rSystem.LastValue : 0);
                }
            }

            var r = new Run { Name = name, Feed = feed, Frames = frames.Count, GpuSamples = timed };
            frames.Sort();
            foreach (float f in frames) r.Seconds += f;
            r.MeanMs = 1000f * r.Seconds / Math.Max(1, r.Frames);
            r.AverageFps = r.Frames / Math.Max(1e-6f, r.Seconds);
            float p99 = frames.Count > 0 ? frames[Mathf.Clamp(Mathf.CeilToInt(0.99f * frames.Count) - 1, 0, frames.Count - 1)] : 0f;
            r.P99Ms = 1000f * p99;
            r.LowFps = p99 > 0f ? 1f / p99 : 0f;
            r.WorstMs = frames.Count > 0 ? 1000f * frames[frames.Count - 1] : 0f;
            r.CpuMainMs = timed > 0 ? (float)(cpuMain / timed) : -1f;
            r.CpuRenderMs = timed > 0 ? (float)(cpuRender / timed) : -1f;
            r.GpuMs = timed > 0 ? (float)(gpu / timed) : -1f;
            r.DrawCalls = counted > 0 ? (float)(drawCalls / counted) : -1f;
            r.Batches = counted > 0 ? (float)(batches / counted) : -1f;
            r.SetPass = counted > 0 ? (float)(setPass / counted) : -1f;
            r.TrianglesM = counted > 0 ? (float)(triangles / counted / 1e6) : -1f;
            r.DrawCallsMax = drawCallsMax;
            r.TrianglesMax = trianglesMax;
            r.SystemUsedMB = systemMax / (1024f * 1024f);
            Vector2Int size = rig.Running ? rig.settings.SceneSizeFor(rig.RenderCoverage) : new Vector2Int(Screen.width, Screen.height);
            r.Render = $"{size.x}x{size.y}";
            results.Add(r);
            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[Bench] {0} ({1}): {2} frames over {3:0.0} s, average {4:0.0} fps ({5:0.00} ms), 1 % low {6:0.0} fps (99th-percentile frame {7:0.00} ms), " +
                "worst frame {8:0.00} ms; CPU main {9:0.00} ms, render thread {10:0.00} ms, GPU (FrameTimingManager{11}) {12:0.00} ms over {13} frames; " +
                "{14:0} draw calls (max {15}), {16:0} batches, {17:0} set-pass calls, {18:0.00} M triangles (max {19:0.00} M); " +
                "Unity's system memory {20:0} MB; render {21}, screen {22}x{23}",
                name, feed ? "the feed" : "noclip, the feed off", r.Frames, r.Seconds, r.AverageFps, r.MeanMs, r.LowFps, r.P99Ms, r.WorstMs,
                r.CpuMainMs, r.CpuRenderMs, feed ? ", the output camera only" : "", r.GpuMs, timed, r.DrawCalls, r.DrawCallsMax, r.Batches, r.SetPass,
                r.TrianglesM, r.TrianglesMax / 1e6, r.SystemUsedMB, r.Render, Screen.width, Screen.height));

            rig.enabled = false;
            if (noclip != null) noclip.enabled = noclipWas;
        }

        /// <summary>The path as a function of time: the terrain's heights sampled along it once, so a frame allocates nothing.</summary>
        sealed class Track
        {
            readonly float[] start;   // each segment's start time, s
            readonly float[] yaw;     // each segment's heading, degrees
            readonly float[] ground;  // the terrain's height along the path, every GroundStep m
            public readonly float Seconds;

            public Track(UnityEngine.SceneManagement.Scene scene)
            {
                int n = Path.Length - 1;
                start = new float[n + 1];
                yaw = new float[n];
                float length = 0f;
                for (int i = 0; i < n; i++)
                {
                    float dx = Path[i + 1].X - Path[i].X, dz = Path[i + 1].Z - Path[i].Z;
                    yaw[i] = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                    start[i] = length / Speed;
                    length += Mathf.Sqrt(dx * dx + dz * dz);
                }
                start[n] = Seconds = length / Speed;
                ground = new float[Mathf.CeilToInt(length / GroundStep) + 2];
                for (int k = 0; k < ground.Length; k++)
                {
                    Locate(Mathf.Min(k * GroundStep / Speed, Seconds), out int i, out float u);
                    float x = Mathf.Lerp(Path[i].X, Path[i + 1].X, u), z = Mathf.Lerp(Path[i].Z, Path[i + 1].Z, u);
                    ground[k] = FarmlandExplore.Ground(scene, x, z, out float g) ? g : 0f;
                }
            }

            void Locate(float t, out int segment, out float u)
            {
                segment = 0;
                while (segment < yaw.Length - 1 && t >= start[segment + 1]) segment++;
                u = Mathf.Clamp01((t - start[segment]) / (start[segment + 1] - start[segment]));
            }

            public void Pose(float t, out Vector3 position, out Quaternion rotation)
            {
                Locate(Mathf.Clamp(t, 0f, Seconds), out int i, out float u);
                Waypoint a = Path[i], b = Path[i + 1];
                float s = Mathf.Clamp(t, 0f, Seconds) * Speed / GroundStep;
                int k = Mathf.Min((int)s, ground.Length - 2);
                float g = Mathf.Lerp(ground[k], ground[k + 1], s - k);
                position = new Vector3(Mathf.Lerp(a.X, b.X, u), g + Mathf.Lerp(a.Height, b.Height, u), Mathf.Lerp(a.Z, b.Z, u));
                float into = Mathf.Clamp(t, 0f, Seconds) - start[i];
                float heading = i > 0 && into < TurnSeconds ? Mathf.LerpAngle(yaw[i - 1], yaw[i], Mathf.SmoothStep(0f, 1f, into / TurnSeconds)) : yaw[i];
                rotation = Quaternion.Euler(Mathf.Lerp(a.Pitch, b.Pitch, u), heading, 0f);
            }
        }

        static string ToJson(float loadSeconds, List<Run> runs)
        {
            var s = new StringBuilder();
            s.Append("{\n  \"load_s\": ").Append(F(loadSeconds))
             .Append(",\n  \"gpu\": \"").Append(SystemInfo.graphicsDeviceName).Append(" (").Append(SystemInfo.graphicsDeviceType).Append(")\"")
             .Append(",\n  \"cpu\": \"").Append(SystemInfo.processorType).Append("\"")
             .Append(",\n  \"battery\": \"").Append(SystemInfo.batteryStatus).Append("\"")
             .Append(",\n  \"screen\": \"").Append(Screen.width).Append('x').Append(Screen.height).Append(' ').Append(Screen.fullScreenMode).Append("\"")
             .Append(",\n  \"vsync\": ").Append(QualitySettings.vSyncCount)
             .Append(",\n  \"quality\": \"").Append(QualitySettings.names[QualitySettings.GetQualityLevel()]).Append("\"")
             .Append(",\n  \"path_m\": ").Append(F(PathLength()))
             .Append(",\n  \"runs\": [");
            for (int i = 0; i < runs.Count; i++)
            {
                Run r = runs[i];
                s.Append(i == 0 ? "\n    {" : ",\n    {")
                 .Append("\"name\": \"").Append(r.Name).Append("\", \"feed\": ").Append(r.Feed ? "true" : "false")
                 .Append(", \"frames\": ").Append(r.Frames).Append(", \"seconds\": ").Append(F(r.Seconds))
                 .Append(", \"average_fps\": ").Append(F(r.AverageFps)).Append(", \"mean_ms\": ").Append(F(r.MeanMs))
                 .Append(", \"low1_fps\": ").Append(F(r.LowFps)).Append(", \"p99_ms\": ").Append(F(r.P99Ms)).Append(", \"worst_ms\": ").Append(F(r.WorstMs))
                 .Append(", \"cpu_main_ms\": ").Append(F(r.CpuMainMs)).Append(", \"cpu_render_ms\": ").Append(F(r.CpuRenderMs))
                 .Append(", \"gpu_ftm_ms\": ").Append(F(r.GpuMs)).Append(", \"gpu_samples\": ").Append(r.GpuSamples)
                 .Append(", \"draw_calls\": ").Append(F(r.DrawCalls)).Append(", \"draw_calls_max\": ").Append(r.DrawCallsMax)
                 .Append(", \"batches\": ").Append(F(r.Batches)).Append(", \"setpass\": ").Append(F(r.SetPass))
                 .Append(", \"triangles_m\": ").Append(F(r.TrianglesM)).Append(", \"triangles_max\": ").Append(r.TrianglesMax)
                 .Append(", \"unity_system_mb\": ").Append(F(r.SystemUsedMB))
                 .Append(", \"render\": \"").Append(r.Render).Append("\"}");
            }
            return s.Append("\n  ]\n}\n").ToString();
        }

        static float PathLength()
        {
            float length = 0f;
            for (int i = 0; i + 1 < Path.Length; i++)
                length += new Vector2(Path[i + 1].X - Path[i].X, Path[i + 1].Z - Path[i].Z).magnitude;
            return length;
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static string Arg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
