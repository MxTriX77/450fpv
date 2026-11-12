using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// The near rule's frame cost (w3-farmland-ground 3.2, the smoke test's <c>-terrainab</c>): the hills flight's cruise
    /// alternates FlightScene's near rule on and off every BlockS, and each frame goes to its block's side: the frame's time,
    /// the GPU's, the CPU's and the wait to present (FrameTimingManager, a development build), triangles and draw calls. The first SettleS of a
    /// block are left out (the timings lag the frame by a few frames), and so is any frame within SettleS after a stall (a
    /// measure, a shot, a measure's comparison still running on the workers; the measures fall on the rule's blocks). Interleaved, a shared machine's load falls on both sides alike; PLT-07's verdict needs a quiet one.
    /// </summary>
    public sealed class TerrainCost : IDisposable
    {
        public const float BlockS = 1f, SettleS = 0.25f;

        sealed class Side
        {
            public readonly List<float> Frame = new List<float>(16384), Gpu = new List<float>(16384), Main = new List<float>(16384),
                Render = new List<float>(16384), Present = new List<float>(16384);
            public double Triangles, Calls;
        }

        readonly Side[] _sides = { new Side(), new Side() }; // 0: the rule on, 1: the scene's errors
        readonly FrameTiming[] _timing = new FrameTiming[1];
        readonly float _begin;
        float _skipUntil;
        ProfilerRecorder _triangles, _calls;

        public TerrainCost(float now)
        {
            _begin = now;
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _calls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        }

        /// <summary>Whether the rule is on in the block holding now.</summary>
        public bool On(float now) => ((int)((now - _begin) / BlockS) & 1) == 0;

        /// <summary>A frame that stalled (a measure, a shot) ends now: the next SettleS are left out.</summary>
        public void Stall(float now) => _skipUntil = now + SettleS;

        /// <summary>Called once a frame: the last frame's figures go to the side of the block holding now.</summary>
        public void Frame(float now, float frameS)
        {
            FrameTimingManager.CaptureFrameTimings();
            uint got = FrameTimingManager.GetLatestTimings(1, _timing);
            float inBlock = (now - _begin) % BlockS;
            if (now < _skipUntil || inBlock < SettleS) return;
            Side s = _sides[On(now) ? 0 : 1];
            s.Frame.Add(frameS * 1e3f);
            if (got > 0)
            {
                s.Gpu.Add((float)_timing[0].gpuFrameTime);
                s.Main.Add((float)_timing[0].cpuMainThreadFrameTime);
                s.Render.Add((float)_timing[0].cpuRenderThreadFrameTime);
                s.Present.Add((float)_timing[0].cpuMainThreadPresentWaitTime);
            }
            s.Triangles += _triangles.Valid ? _triangles.LastValue : 0;
            s.Calls += _calls.Valid ? _calls.LastValue : 0;
        }

        /// <summary>Frames counted on a side: 0 the rule on, 1 the scene's errors.</summary>
        public int Frames(int side) => _sides[side].Frame.Count;

        public string Report()
        {
            var b = new StringBuilder("the near rule's frame cost (" + BlockS + " s blocks, vsync " + QualitySettings.vSyncCount + ", " +
                                      Screen.width + "x" + Screen.height + ", " + SystemInfo.graphicsDeviceName + ", battery " + SystemInfo.batteryStatus +
                                      ", frame timing " + (FrameTimingManager.IsFeatureEnabled() ? "on" : "off") + ")");
            for (int i = 0; i < 2; i++)
            {
                Side s = _sides[i];
                int n = s.Frame.Count;
                b.Append(i == 0 ? "; pixel error " + FlightScene.NearPixelError + " within " + FlightScene.NearM + " m: " : "; the scene's errors: ")
                    .Append(n).Append(" frames, frame ").Append(Stats(s.Frame))
                    .Append(", GPU ").Append(Stats(s.Gpu)).Append(", main thread ").Append(Stats(s.Main)).Append(", render thread ").Append(Stats(s.Render))
                    .Append(", present wait ").Append(Stats(s.Present))
                    .Append(", ").Append(n > 0 ? (s.Triangles / n / 1e6).ToString("0.000") : "-").Append(" M triangles, ")
                    .Append(n > 0 ? (s.Calls / n).ToString("0") : "-").Append(" draw calls");
            }
            return b.ToString();
        }

        // Mean, median and 99th percentile, ms.
        static string Stats(List<float> ms)
        {
            if (ms.Count == 0) return "-";
            var sorted = new List<float>(ms);
            sorted.Sort();
            double sum = 0;
            foreach (float v in sorted) sum += v;
            return "mean " + (sum / sorted.Count).ToString("0.000") + " / median " + sorted[sorted.Count / 2].ToString("0.000") + " / p99 " +
                   sorted[Mathf.Clamp(Mathf.CeilToInt(0.99f * sorted.Count) - 1, 0, sorted.Count - 1)].ToString("0.000") + " ms";
        }

        public void Dispose()
        {
            _triangles.Dispose();
            _calls.Dispose();
        }
    }
}
