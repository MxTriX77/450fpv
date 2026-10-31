using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Fpv450.Feed;
using Fpv450.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fpv450.World
{
    /// <summary>
    /// The test range build's boot (p6-test-range design 8 and 9): the gallery's screen and vsync options, then the
    /// seconds from start to the first frame (PLT-06), every placed prefab by asset and variant with its count, and
    /// each renderer checked as the gallery checks its stations. <c>-smoketest</c> quits after that first frame,
    /// exit code 1 on any problem; <c>-capture &lt;folder&gt;</c> first saves every number-key view under every light
    /// preset; <c>-bench</c> flies <see cref="legs"/> (PLT-07: a fixed path, 2 s warm-up, the rest measured; pass
    /// <c>-vsync 0</c>) and logs each leg's frame rate. Escape quits.
    /// </summary>
    public sealed class TestRangeBoot : MonoBehaviour
    {
        /// <summary>A straight scripted flight from <see cref="from"/> to <see cref="to"/> over <see cref="seconds"/>, facing along it.</summary>
        [Serializable]
        public struct Leg
        {
            public string name;
            public Vector3 from;
            public Vector3 to;
            public float pitchDeg;
            public float seconds;
        }

        const float WarmUpSeconds = 2f;  // PLT-07
        const float LabelSeconds = 1.2f; // past the preset label's second on screen

        public Transform placed;
        public ViewpointKeys keys;
        public NoclipCamera noclip;
        public LookSetup look;
        public Leg[] legs = new Leg[0];

        void Awake() => GalleryBoot.ApplyScreen(Environment.GetCommandLineArgs());

        IEnumerator Start()
        {
            yield return null; // the screen mode applies at the end of the first frame
            Debug.Log($"[TestRange] {SceneManager.GetActiveScene().name} loaded, first frame {Time.realtimeSinceStartup:0.00} s after start: " +
                      $"GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {Screen.width}x{Screen.height} {Screen.fullScreenMode}, " +
                      $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, LOD bias {QualitySettings.lodBias:0.##}, vsync {QualitySettings.vSyncCount}");
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int prefabs = 0, problems = 0;
            foreach (Transform group in placed)
                foreach (Transform instance in group)
                {
                    var meta = instance.GetComponent<AssetPhysicsMeta>();
                    if (meta == null)
                    {
                        Debug.LogError($"[TestRange] {instance.name}: no AssetPhysicsMeta");
                        problems++;
                        continue;
                    }
                    string key = $"{meta.id} {meta.variant}";
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                    prefabs++;
                }
            foreach (KeyValuePair<string, int> count in counts)
                Debug.Log($"[TestRange] {count.Key} x {count.Value}");
            problems += GalleryBoot.Check(placed, "[TestRange] placed");
            Debug.Log($"[TestRange] {prefabs} prefabs placed in {placed.childCount} groups, {counts.Count} variants, " +
                      $"{problems} missing meshes, materials, textures or unsupported shaders");

            string[] args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args, "-capture");
            if (capture >= 0 && capture + 1 < args.Length)
                yield return Capture(args[capture + 1]);
            else if (Array.IndexOf(args, "-bench") >= 0)
                yield return Bench();
            else if (Array.IndexOf(args, "-smoketest") < 0)
                yield break;
            yield return null;
            Application.Quit(problems == 0 ? 0 : 1);
        }

        IEnumerator Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            noclip.enabled = false;
            for (int preset = 0; preset < look.PresetCount; preset++)
            {
                look.Select(preset);
                for (int i = 0; i < keys.Count; i++)
                {
                    keys.Jump(i);
                    yield return i == 0 ? new WaitForSecondsRealtime(LabelSeconds) : null;
                    yield return null;
                    string file = Path.Combine(folder, $"{i + 1}_{keys.views[i].name.Replace(' ', '_')}_{look.Active.name}.png");
                    ScreenCapture.CaptureScreenshot(file);
                    yield return null;
                    yield return null;
                    Debug.Log($"[TestRange] captured {file} ({Screen.width}x{Screen.height})");
                }
            }
        }

        IEnumerator Bench()
        {
            noclip.enabled = false;
            keys.enabled = false;
            var frames = new List<float>(8192);
            foreach (Leg leg in legs)
            {
                Vector3 along = leg.to - leg.from;
                Quaternion view = Quaternion.Euler(leg.pitchDeg, Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg, 0f);
                frames.Clear();
                float start = Time.realtimeSinceStartup;
                for (float t = 0f; t < leg.seconds; t = Time.realtimeSinceStartup - start)
                {
                    transform.SetPositionAndRotation(Vector3.Lerp(leg.from, leg.to, t / leg.seconds), view);
                    yield return null;
                    if (t >= WarmUpSeconds)
                        frames.Add(Time.unscaledDeltaTime);
                }
                frames.Sort();
                float total = 0f;
                foreach (float frame in frames)
                    total += frame;
                float p99 = frames[Mathf.Clamp(Mathf.CeilToInt(0.99f * frames.Count) - 1, 0, frames.Count - 1)];
                Debug.Log($"[TestRange] bench {leg.name}: {frames.Count} frames over {total:0.0} s, average {frames.Count / total:0.0} fps, " +
                          $"1 % low {1f / p99:0.0} fps (99th-percentile frame {1000f * p99:0.00} ms), worst frame {1000f * frames[frames.Count - 1]:0.00} ms, " +
                          $"vsync {QualitySettings.vSyncCount}, {Screen.width}x{Screen.height}");
            }
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Application.Quit();
        }
    }
}
