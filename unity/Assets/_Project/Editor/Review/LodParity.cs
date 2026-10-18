using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using Fpv450.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The LOD parity measure (p1-v01-locust task 1.2a, design decision 3 as amended; asset-pipeline "Baked impostor
    /// LOD", scenario "Every LOD matches LOD0"). For every variant of an asset id, or the one <c>-variant</c> names, by
    /// the review's day and overcast light (<c>Settings/Review/</c>): the review sheet's 50 m tile and a tile at each
    /// LOD's switch distance, from the review's far-shot camera, with each LOD forced in turn. Each LOD's crown comes
    /// from two renders, over black and over white, with no ground and no scale bar: a pixel's coverage is one less the
    /// mean over the linear channels of white minus black, and the render over black is its colour times that coverage.
    /// Each value is the mean over a 2 × 2 grid of camera offsets a quarter pixel either way of the review's framing (a
    /// crown of about 100 px moves 5–6 % under a half-pixel shift; task 1.2c), LOD0's too, and each row also prints the
    /// spread of its luminance and coverage over the four (max − min, as a percentage of the mean); the offsets are made
    /// on the projection, see <see cref="Rig.Offset"/>.
    /// Per LOD it prints the coverage-weighted mean linear colour and its Rec. 709 luminance, the crown's coverage in
    /// pixels, both against LOD0's at the same distance and light, the margin to the spec's limits (6 % in luminance,
    /// 15 % in coverage) in percentage points, negative outside them, and PASS or FAIL. The shadow distance is the
    /// review's, fitted to its stage, so a small asset's 50 m tile has no shadows; the review's 225° camera looks
    /// along one of the impostor's eight baked views, its best case.
    /// <para>Batchmode, with graphics:
    /// <c>-batchmode -quit -executeMethod Fpv450.LodParity.Measure -asset &lt;ID&gt; [-variant &lt;variant&gt;]</c>.
    /// Exit code 0 when every row passes, 2 when one fails, 1 on an error.</para>
    /// </summary>
    public static class LodParity
    {
        // The spec's limits ("Every LOD matches LOD0"): a LOD's crown against LOD0's.
        internal const double LuminanceLimit = 0.06, CoverageLimit = 0.15;

        // The review sheet's stage, light and far shots as ReviewCapture.cs sets them; kept in step by hand, since that
        // file keeps them private. The scale bar is never built here, but its place enters the stage's shadow distance.
        const int Tile = 1024;
        const float VerticalFovDeg = 60f, LookDownDeg = 20f, ShotAzimuthDeg = 225f, ReviewTileM = 50f, FarClipM = 1000f;
        const float BarHeightM = 1.8f, BarPostM = 0.05f, BarGapM = 1f, BarBearingDeg = 112.5f;
        const float MinShadowDistanceM = 12f, BoundsMarginM = 0.3f;
        static readonly string[] Lights = { "day", "overcast" };
        const string CatalogFile = "assets-src/catalog.json";
        const string ProfilesFolder = "unity/Assets/_Project/Settings/Review";
        const string PrefabsFolder = "Assets/_Project/Prefabs";

        // sRGB byte to linear (IEC 61966-2-1), as the readback holds the target's encoded bytes.
        static readonly double[] Linear = Enumerable.Range(0, 256).Select(v =>
        {
            double c = v / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }).ToArray();

        /// <summary>A crown in one tile: its coverage in pixels and the coverage-weighted mean of its linear colour.</summary>
        internal readonly struct Crown
        {
            public readonly double Coverage, R, G, B;

            public Crown(double coverage, double r, double g, double b)
            {
                Coverage = coverage;
                R = r;
                G = g;
                B = b;
            }

            /// <summary>Rec. 709 luminance of the mean linear colour; NaN when nothing is covered.</summary>
            public double Luminance => 0.2126 * R + 0.7152 * G + 0.0722 * B;
        }

        /// <summary>
        /// The crown from the same tile rendered over black and over white. A pixel of coverage a and linear colour c
        /// reads a·c over black and a·c + 1 − a over white, so a = 1 − mean(white − black) over the linear channels, and
        /// the crown's mean colour is the sum over black divided by the summed coverage.
        /// </summary>
        internal static Crown Find(Color32[] overBlack, Color32[] overWhite)
        {
            if (overBlack.Length != overWhite.Length)
                throw new ArgumentException($"the renders differ in size: {overBlack.Length} and {overWhite.Length} px");
            double a = 0, r = 0, g = 0, b = 0;
            for (int i = 0; i < overBlack.Length; i++)
            {
                double r0 = Linear[overBlack[i].r], g0 = Linear[overBlack[i].g], b0 = Linear[overBlack[i].b];
                a += 1.0 - (Linear[overWhite[i].r] - r0 + Linear[overWhite[i].g] - g0 + Linear[overWhite[i].b] - b0) / 3.0;
                r += r0;
                g += g0;
                b += b0;
            }
            return a > 0 ? new Crown(a, r / a, g / a, b / a) : new Crown(0, double.NaN, double.NaN, double.NaN);
        }

        /// <summary>The camera offsets each value is averaged over, in pixels: a 2 × 2 grid half a pixel apart, centred on the review's framing.</summary>
        internal static readonly Vector2[] Offsets = { new Vector2(-0.25f, -0.25f), new Vector2(0.25f, -0.25f), new Vector2(-0.25f, 0.25f), new Vector2(0.25f, 0.25f) };

        /// <summary>
        /// A LOD's crown over the offsets: the mean of the samples' coverages and colours, so its luminance is the mean of
        /// theirs (luminance is linear in the colour), and the spread, max − min over the samples, of the luminance and of
        /// the coverage, each as a fraction of its mean.
        /// </summary>
        internal static (Crown mean, double luminanceSpread, double coverageSpread) Average(Crown[] samples)
        {
            var mean = new Crown(samples.Average(s => s.Coverage), samples.Average(s => s.R), samples.Average(s => s.G), samples.Average(s => s.B));
            double luminanceSpread = samples.Max(s => s.Luminance) - samples.Min(s => s.Luminance);
            double coverageSpread = samples.Max(s => s.Coverage) - samples.Min(s => s.Coverage);
            return (mean, luminanceSpread / mean.Luminance, coverageSpread / mean.Coverage);
        }

        /// <summary>A LOD's value against LOD0's, as a fraction: 0.05 is 5 % over.</summary>
        internal static double Relative(double value, double lod0) => value / lod0 - 1.0;

        /// <summary>Whether a LOD's crown is within the spec's limits of LOD0's (a NaN, a LOD with no crown, fails).</summary>
        internal static bool Passes(double relativeLuminance, double relativeCoverage) =>
            Math.Abs(relativeLuminance) < LuminanceLimit && Math.Abs(relativeCoverage) < CoverageLimit;

        public static void Measure()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string id = Arg(args, "-asset") ?? throw new ArgumentException("usage: -executeMethod Fpv450.LodParity.Measure -asset <ID> [-variant <variant>]");
                if (!Run(id, Arg(args, "-variant")))
                    EditorApplication.Exit(2);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LodParity] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string[] args, string name)
        {
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }

        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        static bool Run(string id, string onlyVariant)
        {
            AssetDatabase.Refresh();
            (string assetClass, string[] variants) = CatalogEntry(id);
            if (onlyVariant != null)
            {
                if (!variants.Contains(onlyVariant))
                    throw new ArgumentException($"{onlyVariant} is not a variant of {id} ({string.Join(", ", variants)})");
                variants = new[] { onlyVariant };
            }
            var prefabs = variants.Select(variant =>
            {
                string path = $"{PrefabsFolder}/{assetClass}/{variant}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException($"{path} is missing: run the catalog import first");
                Review.RequireRenderable(prefab, variant);
                return prefab;
            }).ToArray();
            Profile[] profiles = Lights.Select(LoadProfile).ToArray();
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset ?? throw new InvalidOperationException("no URP asset is active");

            ShaderUtil.allowAsyncCompilation = false; // or the first renders show the "compiling" placeholder
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log($"[LodParity] {id}: {variants.Length} variant(s), lights {string.Join("/", Lights)}, GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), " +
                      $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} ({pipeline.name}), LOD bias {QualitySettings.lodBias:0.##}, MSAA {Mathf.Max(1, pipeline.msaaSampleCount)}, " +
                      $"colour space {QualitySettings.activeColorSpace}; limits luminance {LuminanceLimit * 100:0} %, coverage {CoverageLimit * 100:0} %; " +
                      $"each value the mean over {Offsets.Length} projection offsets ({string.Join(", ", Offsets.Select(o => $"{o.x:+0.##;-0.##} {o.y:+0.##;-0.##}"))} px), spread max - min of the {Offsets.Length}");

            var rig = new Rig(Mathf.Max(1, pipeline.msaaSampleCount));
            float projectShadowDistance = pipeline.shadowDistance;
            int rows = 0, failed = 0;
            try
            {
                for (int v = 0; v < variants.Length; v++)
                {
                    GameObject instance = Object.Instantiate(prefabs[v]);
                    instance.name = variants[v];
                    try
                    {
                        (int variantRows, int variantFailed) = MeasureVariant(instance, profiles, rig, pipeline, projectShadowDistance);
                        rows += variantRows;
                        failed += variantFailed;
                    }
                    finally
                    {
                        pipeline.shadowDistance = projectShadowDistance;
                        Object.DestroyImmediate(instance);
                    }
                }
            }
            finally
            {
                rig.Dispose();
                foreach (GameObject sun in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.name == "ReviewSun"))
                    Object.DestroyImmediate(sun);
            }
            Debug.Log($"[LodParity] {id}: {(failed == 0 ? "PASS" : "FAIL")}, {failed} of {rows} LOD rows outside the limits");
            return failed == 0;
        }

        static (int rows, int failed) MeasureVariant(GameObject instance, Profile[] profiles, Rig rig, UniversalRenderPipelineAsset pipeline, float projectShadowDistance)
        {
            string variant = instance.name;
            AssetPhysicsMeta meta = instance.GetComponent<AssetPhysicsMeta>() ?? throw new InvalidOperationException($"{variant}: the prefab has no AssetPhysicsMeta");
            LODGroup group = instance.GetComponent<LODGroup>() ?? throw new InvalidOperationException($"{variant}: the prefab has no LODGroup");
            int lodCount = group.lodCount;
            if (lodCount != meta.lods.Length)
                throw new InvalidOperationException($"{variant}: the LOD group has {lodCount} LODs, the metadata {meta.lods.Length}");
            string[] names = meta.lods.Select(l => l.mesh.StartsWith(variant + "_", StringComparison.Ordinal) ? l.mesh.Substring(variant.Length + 1) : l.mesh).ToArray();

            // On the ground, as the review stage stands it, and the shadow distance the review fits to its stage.
            instance.transform.position = new Vector3(0f, -meta.bounds.min.y, 0f);
            var asset = new Bounds(meta.bounds.center + instance.transform.position, meta.bounds.size);
            pipeline.shadowDistance = ReviewShadowDistance(asset, projectShadowDistance);

            // Each tile names the LODs Unity shows there: the one in range at 50 m, the two that meet at a switch.
            int shownAtReview = Enumerable.Range(0, lodCount).Last(i => meta.lods[i].switchDistanceM <= ReviewTileM);
            string shown = ReviewTileM < meta.cullDistanceM ? $"{names[shownAtReview]} shown" : "culled";
            var tiles = new List<(string label, float distance)> { ($"review tile {ReviewTileM:0.#} m, {shown}", ReviewTileM) };
            for (int i = 1; i < lodCount; i++)
                tiles.Add(($"{names[i]} switch {meta.lods[i].switchDistanceM:0.#} m, {names[i - 1]} to {names[i]}", meta.lods[i].switchDistanceM));
            Debug.Log($"[LodParity] {variant}: LODs {string.Join(", ", names.Select((n, i) => $"{n} {meta.lods[i].tris} tris" + (i == 0 ? "" : $" @ {meta.lods[i].switchDistanceM:0.#} m")))}, " +
                      $"cull {meta.cullDistanceM:0.#} m; bounds centre {asset.center}, size {asset.size}; shadow distance {pipeline.shadowDistance:0.#} m (game {projectShadowDistance:0.#} m)");

            int rows = 0, failed = 0;
            try
            {
                foreach (Profile profile in profiles)
                {
                    ApplyLight(profile);
                    rig.Shoot(Color.black); // a warm-up: the first render after a light change can read the previous ambient
                    foreach ((string label, float distance) in tiles)
                    {
                        Vector3 position = SlantShot(asset, distance, out bool fromBounds);
                        float toCentre = Vector3.Distance(position, asset.center);
                        if (toCentre + asset.extents.magnitude > FarClipM)
                            throw new InvalidOperationException($"{variant} {label}: the crown lies past the {FarClipM:0} m far plane");
                        rig.Aim(position, asset.center);
                        var crowns = new Crown[lodCount];
                        var spreads = new (double luminance, double coverage)[lodCount];
                        for (int i = 0; i < lodCount; i++)
                        {
                            group.ForceLOD(i);
                            var samples = new Crown[Offsets.Length];
                            for (int k = 0; k < Offsets.Length; k++)
                            {
                                rig.Offset(Offsets[k]);
                                samples[k] = Find(rig.Shoot(Color.black), rig.Shoot(Color.white));
                            }
                            (crowns[i], spreads[i].luminance, spreads[i].coverage) = Average(samples);
                        }
                        group.ForceLOD(-1);
                        if (crowns[0].Coverage <= 0)
                            throw new InvalidOperationException($"{variant} {profile.name} {label}: LOD0 covers no pixel");
                        Debug.Log($"[LodParity] {variant} {profile.name}, {label}: camera {toCentre:0.00} m from the bounds centre{(fromBounds ? " (pushed out of the bounds)" : "")}");
                        for (int i = 0; i < lodCount; i++)
                        {
                            Crown c = crowns[i];
                            string row = $"[LodParity]   {names[i],-9} Y {c.Luminance:0.0000}  coverage {c.Coverage,9:0.0} px  linear rgb {c.R:0.0000} {c.G:0.0000} {c.B:0.0000}" +
                                         $"  spread Y {spreads[i].luminance * 100:0.00} %, coverage {spreads[i].coverage * 100:0.00} %";
                            if (i > 0)
                            {
                                double y = Relative(c.Luminance, crowns[0].Luminance), cover = Relative(c.Coverage, crowns[0].Coverage);
                                bool pass = Passes(y, cover);
                                row += $"  against LOD0: Y {y * 100:+0.00;-0.00} % (margin {(LuminanceLimit - Math.Abs(y)) * 100:0.00}), " +
                                       $"coverage {cover * 100:+0.00;-0.00} % (margin {(CoverageLimit - Math.Abs(cover)) * 100:0.00})  {(pass ? "PASS" : "FAIL")}";
                                rows++;
                                if (!pass)
                                    failed++;
                            }
                            Debug.Log(row);
                        }
                    }
                }
            }
            finally
            {
                group.ForceLOD(-1);
            }
            Debug.Log($"[LodParity] {variant}: {(failed == 0 ? "PASS" : "FAIL")}, {failed} of {rows} LOD rows outside the limits");
            return (rows, failed);
        }

        // --- the review stage, as ReviewCapture.cs builds it ------------------------------------------------------

        static (string assetClass, string[] variants) CatalogEntry(string id)
        {
            string path = Path.Combine(RepoRoot, CatalogFile);
            var assets = Json.Obj(Json.Obj(Json.Parse(File.ReadAllText(path)), CatalogFile), "assets", CatalogFile);
            if (!assets.TryGetValue(id, out object entryValue))
                throw new ArgumentException($"{id} is not an asset of {CatalogFile}");
            var entry = Json.Obj(entryValue, $"{CatalogFile} {id}");
            string[] variants = Json.Arr(entry, "variants", $"{CatalogFile} {id}").Select(v => (string)v).ToArray();
            return (Json.Str(entry, "class", $"{CatalogFile} {id}"), variants);
        }

        /// <summary>
        /// The review sheet's shadow distance: four turntable distances, at least 12 m, never over the game's. The
        /// turntable frames the asset and the scale bar, which stands 1 m outside the bounds at 112.5°.
        /// </summary>
        static float ReviewShadowDistance(Bounds asset, float projectShadowDistance)
        {
            float bearing = BarBearingDeg * Mathf.Deg2Rad;
            var towardsBar = new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing));
            Vector3 barBase = asset.center + towardsBar * (ExitDistance(asset, asset.center, towardsBar) + BarGapM + BarPostM * 0.5f);
            barBase.y = 0f;
            Bounds frame = asset;
            frame.Encapsulate(new Bounds(barBase + Vector3.up * (BarHeightM * 0.5f), new Vector3(BarPostM, BarHeightM, BarPostM)));
            float turntable = Mathf.Max(0.5f, frame.extents.magnitude / Mathf.Sin(VerticalFovDeg * 0.5f * Mathf.Deg2Rad) * 1.1f);
            return Mathf.Min(projectShadowDistance, Mathf.Max(MinShadowDistanceM, 4f * turntable));
        }

        static float ExitDistance(Bounds b, Vector3 from, Vector3 dir)
        {
            float t = float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
                if (Mathf.Abs(dir[i]) > 1e-6f)
                    t = Mathf.Min(t, (dir[i] > 0f ? b.max[i] - from[i] : from[i] - b.min[i]) / Mathf.Abs(dir[i]));
            return float.IsInfinity(t) ? 0f : t;
        }

        /// <summary>The review's far shot: the distance to the bounds centre from a 20° look-down at 225°, pushed out along the ray when it lands within 0.3 m of the bounds.</summary>
        static Vector3 SlantShot(Bounds bounds, float distance, out bool fromBounds)
        {
            Vector3 c = bounds.center;
            float az = ShotAzimuthDeg * Mathf.Deg2Rad, down = LookDownDeg * Mathf.Deg2Rad;
            Vector3 position = c + distance * new Vector3(Mathf.Sin(az) * Mathf.Cos(down), Mathf.Sin(down), Mathf.Cos(az) * Mathf.Cos(down));
            Bounds safe = bounds;
            safe.Expand(BoundsMarginM * 2f);
            fromBounds = safe.Contains(position);
            if (!fromBounds)
                return position;
            Vector3 dir = (position - c).normalized;
            return c + dir * (ExitDistance(safe, c, dir) + distance);
        }

        [Serializable]
        sealed class Profile
        {
            public string name;
            public bool sun;
            public float sun_elevation_deg, sun_azimuth_deg, sun_intensity;
            public float[] sun_color_linear, ambient_linear;
            public bool shadows;
        }

        static Profile LoadProfile(string light)
        {
            string path = Path.Combine(RepoRoot, ProfilesFolder, light + ".json");
            var profile = JsonUtility.FromJson<Profile>(File.ReadAllText(path));
            if (profile == null || profile.name != light || profile.ambient_linear?.Length != 3 || profile.sun_color_linear?.Length != 3)
                throw new FormatException($"{ProfilesFolder}/{light}.json: \"name\" must be \"{light}\", the colours 3 numbers each");
            return profile;
        }

        /// <summary>Unity's Color fields (lights, ambient) are sRGB-encoded in a linear project.</summary>
        static Color Encoded(Color linear) => QualitySettings.activeColorSpace == ColorSpace.Linear ? linear.gamma : linear;

        static void ApplyLight(Profile profile)
        {
            foreach (GameObject stale in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.name == "ReviewSun"))
                Object.DestroyImmediate(stale);
            var ambient = new Color(profile.ambient_linear[0], profile.ambient_linear[1], profile.ambient_linear[2]);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Encoded(ambient);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            var cube = new Cubemap(8, TextureFormat.RGBA32, false);
            Color[] face = Enumerable.Repeat(Encoded(ambient), 64).ToArray();
            for (int i = 0; i < 6; i++)
                cube.SetPixels(face, (CubemapFace)i);
            cube.Apply(false, false);
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = 1f;
            Light sun = null;
            if (profile.sun)
            {
                var go = new GameObject("ReviewSun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = Encoded(new Color(profile.sun_color_linear[0], profile.sun_color_linear[1], profile.sun_color_linear[2]));
                sun.intensity = profile.sun_intensity;
                sun.shadows = profile.shadows ? LightShadows.Soft : LightShadows.None;
                go.transform.rotation = Quaternion.Euler(profile.sun_elevation_deg, profile.sun_azimuth_deg + 180f, 0f);
                sun.GetUniversalAdditionalLightData();
            }
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>The review's camera: 60° vertical, square 1024 px tiles, no post-processing, no anti-aliasing but the pipeline's MSAA.</summary>
        sealed class Rig : IDisposable
        {
            readonly Camera camera;
            readonly RenderTexture target;
            readonly Texture2D readback;

            public Rig(int msaa)
            {
                camera = new GameObject("LodParityCamera").AddComponent<Camera>();
                camera.fieldOfView = VerticalFovDeg;
                camera.aspect = 1f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = FarClipM;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = true;
                camera.enabled = false;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
                target = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa };
                target.Create();
                readback = new Texture2D(Tile, Tile, TextureFormat.RGB24, false, false);
            }

            public void Aim(Vector3 position, Vector3 lookAt)
            {
                camera.transform.position = position;
                camera.transform.LookAt(lookAt, Vector3.up);
            }

            /// <summary>
            /// Shifts the image by a fraction of a pixel from the review's framing: a translation in clip space on the
            /// projection, as URP's TAA jitters it. The eye point stays, so every depth moves by the same fraction of a
            /// pixel and the light and shadows are the same view's; moving the camera in its own plane instead is exact
            /// at one depth only, and at a 7.5 m switch a crown 2 m deep would see its near and far leaves move apart.
            /// </summary>
            public void Offset(Vector2 pixels)
            {
                camera.ResetProjectionMatrix();
                camera.projectionMatrix = Matrix4x4.Translate(new Vector3(pixels.x, pixels.y, 0f) * (2f / Tile)) * camera.projectionMatrix;
            }

            public Color32[] Shoot(Color background)
            {
                camera.backgroundColor = background;
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                }
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0, false);
                readback.Apply(false);
                RenderTexture.active = null;
                return readback.GetPixels32();
            }

            public void Dispose()
            {
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
