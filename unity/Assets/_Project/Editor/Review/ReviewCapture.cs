using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The AST-12 asset review sheet (setup-review-capture). For every variant of an asset id and every light profile
    /// under <c>Settings/Review/</c>: 1024 px tiles at 1.5, 10, 50 and 200 m, an 8-view turntable and the metadata
    /// overlay, on a neutral stage with a 1.8 m scale bar, composed into <c>review-sheets/&lt;ID&gt;/&lt;variant&gt;_&lt;light&gt;.png</c>.
    /// Batchmode, with graphics: <c>-batchmode -quit -executeMethod Fpv450.Review.Capture -asset &lt;ID&gt;</c>;
    /// exit code 1 on any error, and nothing written when the id, a prefab or its metadata is missing, or when a
    /// prefab would render nothing (<see cref="RequireRenderable"/>).
    /// The stage, the camera, the light profiles, the shadow-distance fit and the warm-up render are internal, and the
    /// LOD parity measure (<c>LodParity.cs</c>) shoots on them as they are defined here.
    /// </summary>
    public static class Review
    {
        internal const int Tile = 1024;
        const float VerticalFovDeg = 60f;      // the noclip default (design decision 4, assumed)
        const float LookDownDeg = 20f;         // the far shots and the turntable (assumed typical approach)
        const float ShotAzimuthDeg = 225f;     // distance and overlay shots: camera to the south-west, so the +X scale bar sits right and behind
        const float PilotHeightM = 1.5f;
        const float GroundSizeM = 400f;
        const float GroundAlbedo = 0.18f;
        const float BarHeightM = 1.8f, BarPostM = 0.05f, BarBandM = 0.1f, BarGapM = 1f, BarBearingDeg = 112.5f;
        const float PilotBarGapM = 0.5f;       // the pilot-height view: the bar on the far side of the asset, inside the steep frame (design decision 2)
        const float SurfaceLookDownDeg = 35f;  // a surfaces asset's pilot-height view: down at the mat a few metres ahead (p1-s04-belt-straw design 7, assumed)
        const float SurfaceBarAheadM = 2.5f;   // ... with the bar planted this far ahead on +X, inside the frame
        const float EarthworkLookDownDeg = 20f;    // an earthwork's pilot-height view: along the run from above the lip (p1-e01-trench design 9, assumed: G flies along the trench low)
        const float EarthworkBarAlongM = 3f;       // ... with the bar planted this far along the run
        const float EarthworkBarBesideLipM = 0.2f; // ... and this far out from the lip on the field side: on the berm, past the 0.1 m lip crumble, inside the 0.3 m berm (design 2)
        const float MinShadowDistanceM = 12f;  // the stage's shadow distance: finer shadow texels than the game's 50 m on a small stage
        const float BoundsMarginM = 0.3f;      // a camera closer than this to the bounds is pushed outside them
        internal static readonly float[] DistancesM = { 1.5f, 10f, 50f, 200f };
        internal static readonly string[] Lights = { "day", "overcast" };
        const string CatalogFile = "assets-src/catalog.json";
        const string ProfilesFolder = "Assets/_Project/Settings/Review";
        const string ModelsFolder = "Assets/_Project/Art/Models";
        const string PrefabsFolder = "Assets/_Project/Prefabs";
        const string SheetsFolder = "review-sheets";

        static readonly Color32 White = new Color32(255, 255, 255, 255);
        static readonly Color32 Panel = new Color32(24, 24, 24, 255);
        static readonly Color32 Frame = new Color32(90, 90, 90, 255);

        public static void Capture()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-asset");
                if (at < 0 || at + 1 >= args.Length)
                    throw new ArgumentException("usage: -executeMethod Fpv450.Review.Capture -asset <ID>");
                Run(args[at + 1]);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Review] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static void Run(string id)
        {
            AssetDatabase.Refresh();
            (string assetClass, string[] variants) = CatalogEntry(id);
            var inputs = new List<(string variant, string prefabPath, string metaPath)>();
            foreach (string variant in variants)
            {
                string prefabPath = PrefabPath(assetClass, variant);
                string metaPath = MetaPath(id, assetClass, variant);
                foreach (string path in new[] { prefabPath, metaPath })
                    if (!File.Exists(ProjectPath(path)))
                        throw new FileNotFoundException($"{path} is missing: run the catalog import first");
                inputs.Add((variant, prefabPath, metaPath));
            }
            foreach ((string variant, string prefabPath, _) in inputs)
                RequireRenderable(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? throw new FileNotFoundException($"{prefabPath} did not load as a prefab"), variant);
            LightProfile[] profiles = Lights.Select(LoadProfile).ToArray();

            ShaderUtil.allowAsyncCompilation = false; // or the first tiles show the "compiling" placeholder
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string quality = $"{QualitySettings.names[QualitySettings.GetQualityLevel()]} ({(UniversalRenderPipeline.asset != null ? UniversalRenderPipeline.asset.name : "no URP asset")}), LOD BIAS {QualitySettings.lodBias:0.##}";
            string gpu = $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})";
            Debug.Log($"[Review] {id}: {inputs.Count} variant(s), lights {string.Join("/", Lights)}, GPU {gpu}, quality {quality}, colour space {QualitySettings.activeColorSpace}");

            BuildGround();
            Rig rig = Rig.Create();
            try
            {
                foreach ((string variant, string prefabPath, string metaPath) in inputs)
                {
                    ReviewMeta meta = ReviewMeta.Read(ProjectPath(metaPath));
                    EarthworkRun run = assetClass == "earthworks" ? EarthworkRun.Read(File.ReadAllText(ProjectPath(metaPath)), metaPath) : null;
                    Stage stage = Stage.Build(prefabPath, meta, assetClass, run);
                    try
                    {
                        foreach (LightProfile profile in profiles)
                        {
                            ApplyLight(profile, rig);
                            ReviewCanvas sheet = Sheet(stage, meta, profile, rig, variant, gpu, quality);
                            string folder = Path.Combine(RepoRoot, SheetsFolder, id);
                            Directory.CreateDirectory(folder);
                            string file = Path.Combine(folder, $"{variant}_{profile.name}.png");
                            byte[] png = sheet.ToPng();
                            File.WriteAllBytes(file, png);
                            Debug.Log($"[Review] {variant} {profile.name}: {SheetsFolder}/{id}/{variant}_{profile.name}.png ({sheet.Width}x{sheet.Height}, {png.Length / 1048576.0:0.0} MB)");
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(stage.Root);
                    }
                }
            }
            finally
            {
                rig.Dispose();
            }
        }

        /// <summary>
        /// Refuses a prefab that would render an empty stage: a renderer without a mesh, or a material without a
        /// texture asset, which is what an un-smudged Git LFS pointer leaves behind (the FBX or a PNG fails to import,
        /// the references go missing, and every tile shows bare ground). The importer gives every renderer its mesh
        /// and every material its diffuse, so each missing one is named.
        /// </summary>
        public static void RequireRenderable(GameObject prefab, string variant)
        {
            var missing = new List<string>();
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
                missing.Add("no renderer");
            foreach (MeshRenderer renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                    missing.Add($"no mesh on {renderer.name}");
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                        missing.Add($"no material on {renderer.name}");
                    else if (material.mainTexture == null || !AssetDatabase.GetAssetPath(material.mainTexture).StartsWith("Assets/", StringComparison.Ordinal))
                        missing.Add($"texture missing on {material.name}");
                }
            }
            if (missing.Count > 0)
                throw new InvalidOperationException($"{variant}: {string.Join(", ", missing.Distinct())} (an LFS pointer on disk? run git lfs checkout)");
        }

        // --- the catalog and the repo ---------------------------------------------------------------------------

        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        internal static string ProjectPath(string assetPath) => Path.Combine(RepoRoot, "unity", assetPath);

        internal static string PrefabPath(string assetClass, string variant) => $"{PrefabsFolder}/{assetClass}/{variant}.prefab";

        internal static string MetaPath(string id, string assetClass, string variant) => $"{ModelsFolder}/{assetClass}/{id}/{variant}.assetmeta.json";

        internal static (string assetClass, string[] variants) CatalogEntry(string id)
        {
            string path = Path.Combine(RepoRoot, CatalogFile);
            if (!File.Exists(path))
                throw new FileNotFoundException($"{CatalogFile} is missing");
            var catalog = Json.Obj(Json.Parse(File.ReadAllText(path)), CatalogFile);
            var assets = Json.Obj(catalog, "assets", CatalogFile);
            if (!assets.TryGetValue(id, out object entryValue))
                throw new ArgumentException($"{id} is not an asset of {CatalogFile}");
            var entry = Json.Obj(entryValue, $"{CatalogFile} {id}");
            string[] variants = Json.Arr(entry, "variants", $"{CatalogFile} {id}")
                .Select(v => v as string ?? throw new FormatException($"{CatalogFile} {id}: variants must be strings"))
                .ToArray();
            if (variants.Length == 0)
                throw new FormatException($"{CatalogFile} {id}: no variants");
            return (Json.Str(entry, "class", $"{CatalogFile} {id}"), variants);
        }

        // --- light profiles (Settings/Review/<light>.json, design decision 3) -------------------------------------

        [Serializable]
        internal sealed class LightProfile
        {
            public string name;
            public bool sun;
            public float sun_elevation_deg;
            public float sun_azimuth_deg;
            public float sun_intensity;
            public float[] sun_color_linear;
            public bool shadows;
            public float[] ambient_linear;
            public float[] background_srgb;

            public Color SunColor => Rgb(sun_color_linear);
            public Color Ambient => Rgb(ambient_linear);
            public Color Background => Rgb(background_srgb);

            /// <summary>The illuminance on a horizontal surface (sun × sin(elevation) plus ambient), as luminance.</summary>
            public float HorizontalIlluminance =>
                (sun ? sun_intensity * Mathf.Sin(sun_elevation_deg * Mathf.Deg2Rad) * Luminance(SunColor) : 0f) + Luminance(Ambient);

            public string Summary => sun
                ? $"SUN ELEV {sun_elevation_deg:0}°, AZ {sun_azimuth_deg:0}°, I {sun_intensity:0.00}, {(shadows ? "SHADOWS" : "NO SHADOWS")}"
                : "NO SUN";

            static Color Rgb(float[] c) => new Color(c[0], c[1], c[2], 1f);
        }

        internal static LightProfile LoadProfile(string light)
        {
            string path = ProjectPath($"{ProfilesFolder}/{light}.json");
            if (!File.Exists(path))
                throw new FileNotFoundException($"{ProfilesFolder}/{light}.json is missing");
            var profile = JsonUtility.FromJson<LightProfile>(File.ReadAllText(path));
            if (profile == null || profile.name != light)
                throw new FormatException($"{ProfilesFolder}/{light}.json: \"name\" must be \"{light}\"");
            foreach ((string key, float[] value) in new[] { ("sun_color_linear", profile.sun_color_linear), ("ambient_linear", profile.ambient_linear), ("background_srgb", profile.background_srgb) })
                if (value == null || value.Length != 3)
                    throw new FormatException($"{ProfilesFolder}/{light}.json: \"{key}\" must hold 3 numbers");
            return profile;
        }

        static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Unity's Color fields (lights, ambient, material colours) are sRGB-encoded in a linear project.</summary>
        static Color Encoded(Color linear) => QualitySettings.activeColorSpace == ColorSpace.Linear ? linear.gamma : linear;

        internal static void ApplyLight(LightProfile profile, Rig rig)
        {
            foreach (GameObject stale in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(go => go.name == "ReviewSun"))
                Object.DestroyImmediate(stale);

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Encoded(profile.Ambient);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = UniformCubemap(Encoded(profile.Ambient));
            RenderSettings.reflectionIntensity = 1f;

            Light sun = null;
            if (profile.sun)
            {
                var go = new GameObject("ReviewSun");
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = Encoded(profile.SunColor);
                sun.intensity = profile.sun_intensity;
                sun.shadows = profile.shadows ? LightShadows.Soft : LightShadows.None;
                // Euler x pitches the beam down by the elevation; the beam travels away from the sun's bearing.
                go.transform.rotation = Quaternion.Euler(profile.sun_elevation_deg, profile.sun_azimuth_deg + 180f, 0f);
                sun.GetUniversalAdditionalLightData();
            }
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
            rig.Camera.backgroundColor = profile.Background;
            Debug.Log($"[Review] light {profile.name}: {profile.Summary}, horizontal illuminance {profile.HorizontalIlluminance:0.000}");
        }

        static Cubemap UniformCubemap(Color encoded)
        {
            var cube = new Cubemap(8, TextureFormat.RGBA32, false);
            Color[] face = Enumerable.Repeat(encoded, 64).ToArray();
            for (int i = 0; i < 6; i++)
                cube.SetPixels(face, (CubemapFace)i);
            cube.Apply(false, false);
            return cube;
        }

        // --- the stage (design decision 2) -----------------------------------------------------------------------

        static void BuildGround()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m
            ground.name = "Ground";
            Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.transform.localScale = new Vector3(GroundSizeM / 10f, 1f, GroundSizeM / 10f);
            var renderer = ground.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Lit(new Color(GroundAlbedo, GroundAlbedo, GroundAlbedo), 0.05f);
            // The ground only receives shadows: nothing lies under it, and as a 400 m caster it would set the sun's
            // shadow depth range for the whole stage instead of the asset and the bar, and be pancaked onto the
            // shadow map's near plane right where it receives.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material Lit(Color linearAlbedo, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("no URP Lit shader"));
            material.SetColor("_BaseColor", Encoded(linearAlbedo));
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            return material;
        }

        internal sealed class Stage
        {
            public GameObject Root;
            public Bounds AssetBounds;  // world
            public Bounds FrameBounds;  // asset plus scale bar, world
            public LODGroup LodGroup;
            public Vector3 BarBase;     // the bar's place at the off-grid bearing, world
            public Vector3 Origin;      // the asset's origin, world
            public bool Surface;        // catalog class "surfaces": the pilot-height tile is shot from on the surface
            public EarthworkRun Run;    // catalog class "earthworks" with a holes record: the pilot-height tile is shot along the run
            public bool WarmedUp;       // the discarded render before this stage's first tile is done
            GameObject bar;

            public Vector3 BarTop => bar.transform.position + Vector3.up * BarHeightM;

            /// <summary>Moves the scale bar so that its base stands at <paramref name="basePosition"/>.</summary>
            public void PlaceBar(Vector3 basePosition) => bar.transform.localPosition = basePosition;

            /// <summary>Hides the scale bar, for renders of the asset alone; its place stays in <see cref="FrameBounds"/>.</summary>
            public void HideBar() => bar.SetActive(false);

            public static Stage Build(string prefabPath, ReviewMeta meta, string assetClass, EarthworkRun run = null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    ?? throw new FileNotFoundException($"{prefabPath} did not load as a prefab");
                var stage = new Stage { Root = new GameObject("ReviewStage"), Surface = assetClass == "surfaces", Run = run };
                GameObject instance = Object.Instantiate(prefab, stage.Root.transform);
                instance.name = prefab.name;
                // Onto the ground when its bounds reach below it; an asset above it (a wire span) stays at its own height.
                instance.transform.localPosition = new Vector3(0f, Mathf.Max(0f, -meta.Bounds.min.y), 0f);
                stage.Origin = instance.transform.position;
                // The shots frame the object's own box (bounds.lod) when the metadata gives one, not what lies round it.
                stage.AssetBounds = new Bounds(meta.Frame.center + instance.transform.localPosition, meta.Frame.size);
                stage.LodGroup = instance.GetComponent<LODGroup>();

                // The bar stands 1 m outside the bounds, at a bearing off the 45° turntable grid so no view lines it up with the asset.
                float bearing = BarBearingDeg * Mathf.Deg2Rad;
                var towardsBar = new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing));
                Vector3 centre = stage.AssetBounds.center;
                Vector3 barBase = centre + towardsBar * (ExitDistance(stage.AssetBounds, centre, towardsBar) + BarGapM + BarPostM * 0.5f);
                barBase.y = 0f;
                stage.bar = new GameObject("ScaleBar");
                stage.bar.transform.SetParent(stage.Root.transform, false);
                Material light = Lit(new Color(0.8f, 0.8f, 0.8f), 0.05f), dark = Lit(new Color(0.03f, 0.03f, 0.03f), 0.05f);
                int bands = Mathf.RoundToInt(BarHeightM / BarBandM);
                for (int i = 0; i < bands; i++)
                {
                    GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    band.name = $"ScaleBar{i}";
                    Object.DestroyImmediate(band.GetComponent<Collider>());
                    band.transform.SetParent(stage.bar.transform, false);
                    band.transform.localPosition = Vector3.up * (BarBandM * (i + 0.5f));
                    band.transform.localScale = new Vector3(BarPostM, BarBandM, BarPostM);
                    band.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? light : dark;
                }
                stage.BarBase = barBase;
                stage.PlaceBar(barBase);
                stage.FrameBounds = stage.AssetBounds;
                stage.FrameBounds.Encapsulate(new Bounds(barBase + Vector3.up * (BarHeightM * 0.5f), new Vector3(BarPostM, BarHeightM, BarPostM)));
                return stage;
            }

            public Matrix4x4 LocalToWorld => Root.transform.GetChild(0).localToWorldMatrix;
        }

        /// <summary>
        /// An earthwork's run (p1-e01-trench design 9), in the asset's frame, from the metadata's first <c>holes</c> record
        /// (the niches' records follow it, square to the run, and are ignored): the first point of its
        /// <c>centre_line</c> on the lip at y = 0, the plan direction of the line's first segment, and half its
        /// <c>top_width_m</c>.
        /// </summary>
        internal sealed class EarthworkRun
        {
            public Vector3 Start;
            public Vector3 Along;
            public float HalfTopWidth;

            /// <summary>The run of an <c>.assetmeta.json</c> text; null when it has no <c>holes</c> record.</summary>
            public static EarthworkRun Read(string metaJson, string what)
            {
                var meta = Json.Obj(Json.Parse(metaJson), what);
                if (!meta.TryGetValue("holes", out object value))
                    return null;
                List<object> holes = Json.Arr(value, $"{what} \"holes\"");
                if (holes.Count == 0)
                    return null;
                string record = $"{what} holes[0]";
                var hole = Json.Obj(holes[0], record);
                List<object> line = Json.Arr(hole, "centre_line", record);
                Vector3 a = Json.Vec3(line[0], $"{record} centre_line[0]"), b = Json.Vec3(line[1], $"{record} centre_line[1]");
                return new EarthworkRun
                {
                    Start = new Vector3(a.x, 0f, a.z),
                    Along = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized,
                    HalfTopWidth = Json.Num(hole, "top_width_m", record) * 0.5f,
                };
            }

            /// <summary>The field side: the run's right, as in G, where the field lies right of the trench and the strip left.</summary>
            public Vector3 Field => Vector3.Cross(Vector3.up, Along);
        }

        // --- the camera rig (design decision 4) ------------------------------------------------------------------

        internal sealed class Rig : IDisposable
        {
            public Camera Camera;
            RenderTexture target;
            Texture2D readback;
            bool? requestSupported;

            public static Rig Create()
            {
                var go = new GameObject("ReviewCamera");
                var camera = go.AddComponent<Camera>();
                camera.fieldOfView = VerticalFovDeg;
                camera.aspect = 1f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 1000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.allowHDR = true;
                camera.enabled = false;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
                int msaa = UniversalRenderPipeline.asset != null ? Mathf.Max(1, UniversalRenderPipeline.asset.msaaSampleCount) : 1;
                var rig = new Rig
                {
                    Camera = camera,
                    target = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = msaa },
                    readback = new Texture2D(Tile, Tile, TextureFormat.RGB24, false, false),
                };
                rig.target.Create();
                return rig;
            }

            public void Aim(Vector3 position, Vector3 lookAt)
            {
                Camera.transform.position = position;
                Camera.transform.LookAt(lookAt, Vector3.up);
            }

            /// <summary>Whether a world point lies inside the aimed camera's frame, in front of the near plane.</summary>
            public bool InFrame(Vector3 world)
            {
                Vector3 v = Camera.WorldToViewportPoint(world);
                return v.z > Camera.nearClipPlane && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
            }

            /// <summary>Renders the camera from <paramref name="position"/> looking at <paramref name="lookAt"/> and reads the pixels back.</summary>
            public ReviewCanvas Shoot(Vector3 position, Vector3 lookAt)
            {
                Aim(position, lookAt);
                return ReviewCanvas.FromTexture(Render());
            }

            /// <summary>
            /// Renders the camera as it is aimed into the tile and reads the pixels back into the readback texture it
            /// returns, on the CPU side only: its callers read them with GetPixels32, and an Apply uploading them again
            /// left 4 MB with the graphics driver per render until the batch method returned (p1-v01-locust task 1.2m:
            /// the parity measure past 6 GB within a minute, measured).
            /// </summary>
            public Texture2D Render()
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (requestSupported == null)
                {
                    requestSupported = RenderPipeline.SupportsRenderRequest(Camera, request);
                    Debug.Log($"[Review] rendering through {(requestSupported.Value ? "RenderPipeline.SubmitRenderRequest" : "Camera.Render")}, {Tile}x{Tile}, MSAA {target.antiAliasing}");
                }
                if (requestSupported.Value)
                    RenderPipeline.SubmitRenderRequest(Camera, request);
                else
                {
                    Camera.targetTexture = target;
                    Camera.Render();
                }
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0, false);
                RenderTexture.active = null;
                return readback;
            }

            public void Dispose()
            {
                Camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(Camera.gameObject);
            }
        }

        static Vector3 Orbit(Vector3 target, float distance, float azimuthDeg, float downDeg)
        {
            float az = azimuthDeg * Mathf.Deg2Rad, down = downDeg * Mathf.Deg2Rad;
            return target + distance * new Vector3(Mathf.Sin(az) * Mathf.Cos(down), Mathf.Sin(down), Mathf.Cos(az) * Mathf.Cos(down));
        }

        /// <summary>The distance from <paramref name="from"/> (inside the bounds) along <paramref name="dir"/> to where it leaves them.</summary>
        static float ExitDistance(Bounds b, Vector3 from, Vector3 dir)
        {
            float t = float.PositiveInfinity;
            for (int i = 0; i < 3; i++)
                if (Mathf.Abs(dir[i]) > 1e-6f)
                    t = Mathf.Min(t, (dir[i] > 0f ? b.max[i] - from[i] : from[i] - b.min[i]) / Mathf.Abs(dir[i]));
            return float.IsInfinity(t) ? 0f : t;
        }

        static Bounds Safe(Bounds b)
        {
            b.Expand(BoundsMarginM * 2f);
            return b;
        }

        /// <summary>
        /// The far shots: the slant distance to the bounds centre from a 20° look-down. A camera that lands inside the
        /// (expanded) bounds is pushed out along the same ray to the nominal distance from where the ray leaves them.
        /// </summary>
        internal static Vector3 SlantShot(Bounds bounds, float distance, out bool fromBounds)
        {
            Vector3 c = bounds.center;
            Vector3 position = Orbit(c, distance, ShotAzimuthDeg, LookDownDeg);
            fromBounds = Safe(bounds).Contains(position);
            if (!fromBounds)
                return position;
            Vector3 dir = (position - c).normalized;
            return c + dir * (ExitDistance(Safe(bounds), c, dir) + distance);
        }

        /// <summary>
        /// Pilot height: the camera 1.5 m above the ground, 1.5 m from the bounds centre when that keeps it outside
        /// the bounds; otherwise 1.5 m outside the bounds, horizontally, still looking at the centre.
        /// </summary>
        static Vector3 PilotShot(Bounds bounds, out bool fromBounds)
        {
            Vector3 c = bounds.center;
            float az = ShotAzimuthDeg * Mathf.Deg2Rad, dy = PilotHeightM - c.y;
            var horizontal = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
            fromBounds = true;
            float horizontalSquared = PilotHeightM * PilotHeightM - dy * dy;
            if (horizontalSquared >= 0.25f * 0.25f) // keeps the camera off the vertical over a ground-level centre
            {
                Vector3 position = c + horizontal * Mathf.Sqrt(horizontalSquared) + Vector3.up * dy;
                fromBounds = Safe(bounds).Contains(position);
                if (!fromBounds)
                    return position;
            }
            Vector3 exit = c + horizontal * ExitDistance(Safe(bounds), c, horizontal);
            return new Vector3(exit.x + horizontal.x * PilotHeightM, PilotHeightM, exit.z + horizontal.z * PilotHeightM);
        }

        /// <summary>
        /// The scale bar's base for the pilot-height view: at 1.5 m the camera looks steeply down at a small asset and the
        /// side bar leaves the frame, so the bar stands on the far side of the asset, PilotBarGapM past its bounds, where its
        /// lower bands are what a pilot sees beside the asset (design decision 2). The label says when its top is cut.
        /// </summary>
        static Vector3 PilotBar(Bounds bounds)
        {
            float az = ShotAzimuthDeg * Mathf.Deg2Rad;
            var towardsFar = new Vector3(-Mathf.Sin(az), 0f, -Mathf.Cos(az));
            Vector3 c = bounds.center;
            Vector3 barBase = c + towardsFar * (ExitDistance(bounds, c, towardsFar) + PilotBarGapM + BarPostM * 0.5f);
            barBase.y = 0f;
            return barBase;
        }

        /// <summary>
        /// The pilot-height tile's pose: the camera, its look-at point and the scale bar's base. An asset is shot from
        /// beside its bounds (<see cref="PilotShot"/>, <see cref="PilotBar"/>), but a surfaces asset from on the surface
        /// (p1-s04-belt-straw design 7): 1.5 m above its origin, the patch centre on the mat's mean top, looking 35° down
        /// towards +X, with the bar planted 2.5 m ahead on +X inside the frame; and an earthwork with a <c>holes</c> record
        /// along its run (p1-e01-trench design 9): 1.5 m above the lip at the run's start, half the top width out on the
        /// field side, looking along the first segment 20° down, with the bar planted 3 m along the run, 0.2 m out from
        /// the lip.
        /// </summary>
        internal static (Vector3 position, Vector3 lookAt, Vector3 barBase, bool fromBounds) PilotPose(Stage stage)
        {
            Bounds asset = stage.AssetBounds;
            if (stage.Run != null)
            {
                EarthworkRun run = stage.Run;
                Matrix4x4 local = stage.LocalToWorld;
                float lookDown = EarthworkLookDownDeg * Mathf.Deg2Rad;
                Vector3 camera = local.MultiplyPoint3x4(run.Start + run.Field * run.HalfTopWidth + Vector3.up * PilotHeightM);
                Vector3 forward = local.MultiplyVector(run.Along * Mathf.Cos(lookDown) + Vector3.down * Mathf.Sin(lookDown));
                Vector3 bar = local.MultiplyPoint3x4(run.Start + run.Along * EarthworkBarAlongM + run.Field * (run.HalfTopWidth + EarthworkBarBesideLipM));
                return (camera, camera + forward, bar, false);
            }
            if (!stage.Surface)
            {
                Vector3 beside = PilotShot(asset, out bool fromBounds);
                return (beside, asset.center, PilotBar(asset), fromBounds);
            }
            float down = SurfaceLookDownDeg * Mathf.Deg2Rad;
            Vector3 position = stage.Origin + Vector3.up * PilotHeightM;
            return (position, position + new Vector3(Mathf.Cos(down), -Mathf.Sin(down), 0f), stage.Origin + Vector3.right * SurfaceBarAheadM, false);
        }

        /// <summary>Whether Unity's LOD group draws nothing from this camera: the metadata's cull distance, through lodBias.</summary>
        static bool Culled(LODGroup group, Camera camera)
        {
            if (group == null)
                return false;
            LOD[] lods = group.GetLODs();
            if (lods.Length == 0)
                return false;
            float distance = Vector3.Distance(camera.transform.position, group.transform.TransformPoint(group.localReferencePoint));
            float relativeHeight = group.size * QualitySettings.lodBias / (2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            return relativeHeight < lods[lods.Length - 1].screenRelativeTransitionHeight;
        }

        /// <summary>The turntable's distance: the frame bounds (the asset and the scale bar) fill the 60° frame with a 10 % margin.</summary>
        internal static float TurntableDistance(Stage stage) =>
            Mathf.Max(0.5f, stage.FrameBounds.extents.magnitude / Mathf.Sin(VerticalFovDeg * 0.5f * Mathf.Deg2Rad) * 1.1f);

        /// <summary>
        /// The stage's shadow distance: the game's (50 m at High) gives 2.4 cm shadow texels, too coarse for a small stage,
        /// so four turntable distances, at least 12 m, and never more than <paramref name="projectShadowDistance"/>.
        /// </summary>
        internal static float ShadowDistance(Stage stage, float projectShadowDistance) =>
            Mathf.Min(projectShadowDistance, Mathf.Max(MinShadowDistanceM, 4f * TurntableDistance(stage)));

        /// <summary>
        /// The first render of a session, and the first render of a stage's materials, can come out wrong (a dark ground,
        /// a pale asset, texture detail off): one discarded render of the same shot before a stage's first tile.
        /// </summary>
        internal static void WarmUp(Rig rig, Stage stage, Vector3 position, Vector3 lookAt)
        {
            if (stage.WarmedUp)
                return;
            rig.Shoot(position, lookAt);
            stage.WarmedUp = true;
            Debug.Log($"[Review] warm-up: one discarded render of {stage.Root.transform.GetChild(0).name} before its first tile");
        }

        // --- the sheet (design decisions 5 and 6) ------------------------------------------------------------------

        static ReviewCanvas Sheet(Stage stage, ReviewMeta meta, LightProfile profile, Rig rig, string variant, string gpu, string quality)
        {
            string title = $"{variant}  {profile.name}";
            float turntableDistance = TurntableDistance(stage);
            var tiles = new List<ReviewCanvas>();

            // The stage's shadow distance (ShadowDistance) for the capture, the project's put back afterwards.
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset;
            float projectShadowDistance = pipeline != null ? pipeline.shadowDistance : 0f;
            float shadowDistance = pipeline != null ? ShadowDistance(stage, projectShadowDistance) : 0f;
            string shadows = pipeline != null
                ? $"SHADOWS {pipeline.mainLightShadowmapResolution} PX, {pipeline.shadowCascadeCount} CASCADE(S), {shadowDistance:0.#} M (GAME {projectShadowDistance:0.#} M)"
                : "SHADOWS: NO URP ASSET";
            if (pipeline != null)
                pipeline.shadowDistance = shadowDistance;
            try
            {
                Tiles(stage, meta, rig, title, turntableDistance, tiles);
            }
            finally
            {
                if (pipeline != null)
                    pipeline.shadowDistance = projectShadowDistance;
            }

            var sheet = new ReviewCanvas(4 * Tile, 4 * Tile, Panel);
            for (int i = 0; i < tiles.Count; i++)
                sheet.Blit(tiles[i], i % 4 * Tile, i / 4 * Tile);
            sheet.Blit(Legend(meta, profile, title, gpu, quality, shadows), Tile, 3 * Tile);
            sheet.Text(4 * Tile - ReviewCanvas.TextWidth(gpu, 3) - 16, 4 * Tile - ReviewCanvas.LineHeight(3) - 8, gpu, 3, White);
            return sheet;
        }

        static void Tiles(Stage stage, ReviewMeta meta, Rig rig, string title, float turntableDistance, List<ReviewCanvas> tiles)
        {
            Bounds asset = stage.AssetBounds;
            foreach (float distance in DistancesM)
            {
                bool pilot = distance == DistancesM[0];
                bool fromBounds;
                Vector3 position, lookAt = asset.center, barBase = stage.BarBase;
                if (pilot)
                    (position, lookAt, barBase, fromBounds) = PilotPose(stage);
                else
                    position = SlantShot(asset, distance, out fromBounds);
                string view = !pilot ? $"{distance:0.#} M SLANT {LookDownDeg:0}° DOWN AZ {ShotAzimuthDeg:0}"
                    : stage.Run != null ? $"{distance:0.#} M PILOT HEIGHT ALONG THE RUN {EarthworkLookDownDeg:0}° DOWN"
                    : stage.Surface ? $"{distance:0.#} M PILOT HEIGHT ON SURFACE {SurfaceLookDownDeg:0}° DOWN"
                    : $"{distance:0.#} M PILOT HEIGHT AZ {ShotAzimuthDeg:0}";
                if (fromBounds)
                    view += $" FROM BOUNDS ({Vector3.Distance(position, asset.center):0.0} M TO CENTRE)";
                if (pilot)
                {
                    stage.PlaceBar(barBase);
                    rig.Aim(position, lookAt);
                    if (!rig.InFrame(stage.BarTop))
                        view += "  BAR LOWER BANDS";
                    if (stage.Surface)
                    {
                        Vector3 forward = rig.Camera.transform.forward;
                        Debug.Log($"[Review] surface pilot pose: camera ({position.x:0.000}, {position.y:0.000}, {position.z:0.000}), {position.y - stage.Origin.y:0.000} m above the patch centre " +
                                  $"({stage.Origin.x:0.000}, {stage.Origin.y:0.000}, {stage.Origin.z:0.000}), forward ({forward.x:0.0000}, {forward.y:0.0000}, {forward.z:0.0000}), " +
                                  $"{Mathf.Asin(-forward.y) * Mathf.Rad2Deg:0.00}° down towards +X; bar base ({barBase.x:0.000}, {barBase.y:0.000}, {barBase.z:0.000}), " +
                                  $"{barBase.x - position.x:0.00} m ahead, in frame {rig.InFrame(barBase)}");
                    }
                    if (stage.Run != null)
                    {
                        Vector3 forward = rig.Camera.transform.forward, along = stage.Run.Along;
                        Vector3 lip = stage.LocalToWorld.MultiplyPoint3x4(stage.Run.Start);
                        Debug.Log($"[Review] earthwork pilot pose: camera ({position.x:0.000}, {position.y:0.000}, {position.z:0.000}), {position.y - lip.y:0.000} m above the lip, " +
                                  $"{stage.Run.HalfTopWidth:0.000} m to the field side (right) of the run's start ({lip.x:0.000}, {lip.y:0.000}, {lip.z:0.000}), " +
                                  $"forward ({forward.x:0.0000}, {forward.y:0.0000}, {forward.z:0.0000}), {Mathf.Asin(-forward.y) * Mathf.Rad2Deg:0.00}° down along the first segment " +
                                  $"({along.x:0.0000}, {along.y:0.0000}, {along.z:0.0000}); bar base ({barBase.x:0.000}, {barBase.y:0.000}, {barBase.z:0.000}), " +
                                  $"{EarthworkBarAlongM:0.00} m along the run, in frame {rig.InFrame(barBase)}, top in frame {rig.InFrame(stage.BarTop)}");
                    }
                }
                tiles.Add(Shot(rig, stage, position, lookAt, title, view, null, null));
                stage.PlaceBar(stage.BarBase);
            }
            for (int i = 0; i < 8; i++)
            {
                float azimuth = i * 45f;
                Vector3 position = Orbit(stage.FrameBounds.center, turntableDistance, azimuth, LookDownDeg);
                tiles.Add(Shot(rig, stage, position, stage.FrameBounds.center, title, $"TURNTABLE {i + 1}/8 AZ {azimuth:0} {turntableDistance:0.00} M", null, null));
            }
            {
                float distance = Mathf.Min(DistancesM[1], turntableDistance);
                Vector3 position = SlantShot(asset, distance, out bool fromBounds);
                string view = $"OVERLAY {distance:0.00} M {LookDownDeg:0}° DOWN AZ {ShotAzimuthDeg:0}" + (fromBounds ? " FROM BOUNDS" : "");
                tiles.Add(Shot(rig, stage, position, asset.center, title, view, meta, stage.LocalToWorld));
            }
        }

        static ReviewCanvas Shot(Rig rig, Stage stage, Vector3 position, Vector3 lookAt, string title, string view, ReviewMeta overlay, Matrix4x4? localToWorld)
        {
            WarmUp(rig, stage, position, lookAt);
            ReviewCanvas tile = rig.Shoot(position, lookAt);
            bool culled = Culled(stage.LodGroup, rig.Camera);
            if (overlay != null)
                DrawOverlay(tile, rig.Camera, overlay.Segments(localToWorld.Value));
            tile.Darken(0, 0, Tile, 2 * ReviewCanvas.LineHeight(3) + 16, 150);
            tile.Text(12, 10, title, 3, White);
            tile.Text(12, 10 + ReviewCanvas.LineHeight(3), view + (culled ? "  CULLED" : ""), 3, culled ? new Color32(255, 200, 80, 255) : White);
            tile.Outline(0, 0, Tile, Tile, Frame, 2);
            Color32 ground = tile.Mean(32, Tile - 96, 64, 64);
            Debug.Log($"[Review] tile {view}: camera ({position.x:0.00}, {position.y:0.00}, {position.z:0.00}), " +
                      $"{Vector3.Distance(position, lookAt):0.000} m to the look-at point{(culled ? ", culled" : "")}, lower-left patch {ground.r}/{ground.g}/{ground.b}");
            return tile;
        }

        /// <summary>Projects each world segment through the camera, clips it at the near plane and draws it 2 px wide.</summary>
        static void DrawOverlay(ReviewCanvas tile, Camera camera, List<ReviewMeta.Segment> segments)
        {
            Matrix4x4 view = camera.worldToCameraMatrix, projection = camera.projectionMatrix;
            float near = -camera.nearClipPlane;
            foreach (ReviewMeta.Segment segment in segments)
            {
                Vector3 a = view.MultiplyPoint3x4(segment.A), b = view.MultiplyPoint3x4(segment.B);
                if (a.z > near && b.z > near)
                    continue;
                if (a.z > near)
                    a = Vector3.Lerp(a, b, (near - a.z) / (b.z - a.z));
                else if (b.z > near)
                    b = Vector3.Lerp(b, a, (near - b.z) / (a.z - b.z));
                tile.Line(ToPixels(projection, a), ToPixels(projection, b), segment.Color, 2);
            }
        }

        static Vector2 ToPixels(Matrix4x4 projection, Vector3 viewPoint)
        {
            Vector4 clip = projection * new Vector4(viewPoint.x, viewPoint.y, viewPoint.z, 1f);
            return new Vector2((clip.x / clip.w + 1f) * 0.5f * Tile, (1f - clip.y / clip.w) * 0.5f * Tile);
        }

        static ReviewCanvas Legend(ReviewMeta meta, LightProfile profile, string title, string gpu, string quality, string shadows)
        {
            var panel = new ReviewCanvas(Tile, Tile, Panel);
            int y = 24, step = ReviewCanvas.LineHeight(3) + 6;
            panel.Text(24, y, $"LEGEND  {title}", 3, White);
            y += step + 8;
            foreach ((Color32 color, string text) in LegendKeys(meta))
            {
                panel.Fill(24, y + 2, 18, 18, color);
                panel.Text(56, y, text, 3, White);
                y += step;
            }
            y += 12;
            foreach (string line in new[]
                     {
                         $"BOUNDS {meta.Bounds.size.x:0.000} X {meta.Bounds.size.y:0.000} X {meta.Bounds.size.z:0.000} M" +
                         (meta.Lod is Bounds lod ? $", SHOTS FRAMED ON BOUNDS.LOD {lod.size.x:0.000} X {lod.size.y:0.000} X {lod.size.z:0.000} M" : ""),
                         $"LODS {meta.LodSummary}, CULL {meta.CullDistance:0.#} M",
                         $"MATERIALS {meta.MaterialSummary}",
                         $"LIGHT {profile.name}: {profile.Summary}",
                         $"AMBIENT {Luminance(profile.Ambient):0.00}, ILLUMINANCE {profile.HorizontalIlluminance:0.00}, GROUND ALBEDO {GroundAlbedo:0.00}",
                         $"FOV {VerticalFovDeg:0}° VERT, TILES {Tile} PX, BAR {BarHeightM:0.0} M / {BarBandM * 100:0} CM BANDS",
                         shadows,
                         $"QUALITY {quality}",
                         $"GPU {gpu}",
                     })
                foreach (string row in Wrap(line, (Tile - 48) / ReviewCanvas.TextWidth("X", 3)))
                {
                    panel.Text(24, y, row, 3, White);
                    y += step;
                }
            panel.Outline(0, 0, Tile, Tile, Frame, 2);
            return panel;
        }

        /// <summary>
        /// The legend's colour keys: the rigid collision primitives, a compliant shell's pieces apart from them when
        /// the asset has any (p1-v01-locust QA 2.1 S6), the gaps, the wind volumes and the snag features.
        /// </summary>
        internal static List<(Color32 color, string text)> LegendKeys(ReviewMeta meta)
        {
            List<ReviewMeta.Shape> rigid = meta.Collision.Where(s => !ReviewMeta.IsShell(s)).ToList();
            List<ReviewMeta.Shape> shell = meta.Collision.Where(ReviewMeta.IsShell).ToList();
            var keys = new List<(Color32 color, string text)> { (ReviewMeta.CollisionColor, $"COLLISION ({rigid.Count}): {Kinds(rigid)}") };
            if (shell.Count > 0)
                keys.Add((ReviewMeta.ShellColor, $"SHELL PIECES ({shell.Count}, {string.Join(" / ", shell.Select(s => s.material).Distinct().OrderBy(m => m)).ToUpperInvariant()}): {Kinds(shell)}"));
            keys.Add((ReviewMeta.GapColor, $"GAPS ({meta.Gaps.Count}), WITH THEIR NORMALS"));
            keys.Add((ReviewMeta.WindColor, $"WIND VOLUMES ({meta.WindVolumes.Count}): {Kinds(meta.WindVolumes)}"));
            keys.Add((ReviewMeta.SnagColor, $"SNAG ({meta.Snags.Count}), CROSSES AT THE POINTS"));
            return keys;
        }

        /// <summary>
        /// A legend line broken after its commas into rows of at most <paramref name="columns"/> characters, each
        /// following row indented (an item longer than that stays whole), so a variant's every slot material and LOD
        /// shows: an autumn variant's leaf material fell past the panel's edge (p1-v01-locust task 1.4).
        /// </summary>
        internal static IEnumerable<string> Wrap(string line, int columns)
        {
            string row = "";
            foreach (string item in line.Split(new[] { ", " }, StringSplitOptions.None))
            {
                if (row.Length > 0 && row.Length + 2 + item.Length > columns)
                {
                    yield return row + ",";
                    row = "  " + item;
                }
                else
                    row = row.Length == 0 ? item : $"{row}, {item}";
            }
            yield return row;
        }

        static string Kinds(List<ReviewMeta.Shape> shapes) =>
            shapes.Count == 0 ? "NONE" : string.Join(", ", shapes.GroupBy(s => s.kind).Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}"));
    }
}
