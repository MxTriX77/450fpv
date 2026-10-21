using System;
using System.Collections;
using System.IO;
using Fpv450.Feed;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fpv450.World
{
    /// <summary>
    /// The gallery build's boot (p1-g1-look-lock-gallery design 5 and 6): vsync from <c>-vsync &lt;n&gt;</c> (on by
    /// default, for the pilot; QA measures with <c>-vsync 0</c>), then a log line per station listing its placed
    /// prefabs, each renderer checked for a missing mesh, material or texture and for a shader the GPU cannot run
    /// (the pink error shader). With <c>-smoketest</c> the player quits after one frame, with exit code 1 if any check
    /// failed; with <c>-capture &lt;folder&gt;</c> it first saves every viewpoint under every light preset there (QA's
    /// screenshot review). Escape quits.
    /// </summary>
    public sealed class GalleryBoot : MonoBehaviour
    {
        const float LabelSeconds = 1.2f; // past the preset label's second on screen

        public Transform stations;
        public ViewpointKeys keys;
        public LookSetup look;

        void Awake()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-vsync");
            QualitySettings.vSyncCount = at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out int count) ? count : 1;
        }

        IEnumerator Start()
        {
            Debug.Log($"[Gallery] {SceneManager.GetActiveScene().name} loaded: GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                      $"{Screen.width}x{Screen.height} {Screen.fullScreenMode}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                      $"LOD bias {QualitySettings.lodBias:0.##}, vsync {QualitySettings.vSyncCount}");
            int prefabs = 0, problems = 0;
            foreach (Transform marker in stations)
            {
                var names = new string[marker.childCount];
                for (int i = 0; i < marker.childCount; i++)
                    names[i] = marker.GetChild(i).name;
                prefabs += names.Length;
                Debug.Log($"[Gallery] station {marker.name} at ({marker.position.x:0}, {marker.position.y:0}, {marker.position.z:0}): " +
                          (names.Length > 0 ? string.Join(", ", names) : "empty marker"));
                problems += Check(marker);
            }
            Debug.Log($"[Gallery] {prefabs} prefabs placed, {problems} missing meshes, materials, textures or unsupported shaders");

            string[] args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args, "-capture");
            if (capture >= 0 && capture + 1 < args.Length)
                yield return Capture(args[capture + 1]);
            else if (Array.IndexOf(args, "-smoketest") < 0)
                yield break;
            yield return null;
            Application.Quit(problems == 0 ? 0 : 1);
        }

        IEnumerator Capture(string folder)
        {
            Directory.CreateDirectory(folder);
            for (int preset = 0; preset < look.PresetCount; preset++)
            {
                look.Select(preset);
                for (int i = 0; i < GalleryStations.Viewpoints.Length; i++)
                {
                    keys.Jump(i);
                    yield return i == 0 ? new WaitForSecondsRealtime(LabelSeconds) : null;
                    yield return null;
                    GalleryStations.Viewpoint viewpoint = GalleryStations.Viewpoints[i];
                    string file = Path.Combine(folder, $"{i + 1}_{viewpoint.Name.Replace(' ', '_')}_{look.Active.name}.png");
                    ScreenCapture.CaptureScreenshot(file);
                    yield return null;
                    yield return null;
                    Debug.Log($"[Gallery] captured {file} ({Screen.width}x{Screen.height})");
                }
            }
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                Application.Quit();
        }

        static int Check(Transform marker)
        {
            int problems = 0;
            foreach (MeshRenderer renderer in marker.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                    problems += Problem(marker, renderer, "no mesh");
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        problems += Problem(marker, renderer, "no material");
                    else if (material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")
                        problems += Problem(marker, renderer, $"{material.name} has no supported shader (pink)");
                    else if (material.mainTexture == null)
                        problems += Problem(marker, renderer, $"{material.name} has no texture");
                }
            }
            return problems;
        }

        static int Problem(Transform marker, Renderer renderer, string what)
        {
            Debug.LogError($"[Gallery] station {marker.name}: {renderer.name}: {what}");
            return 1;
        }
    }
}
