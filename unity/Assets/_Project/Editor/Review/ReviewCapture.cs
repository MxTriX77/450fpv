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
    /// exit code 1 on any error, and nothing written when the id, a prefab or its metadata is missing.
    /// </summary>
    public static class Review
    {
        const int Tile = 1024;
        const float VerticalFovDeg = 60f;      // the noclip default (design decision 4, assumed)
        const float LookDownDeg = 20f;         // the far shots and the turntable (assumed typical approach)
        const float ShotAzimuthDeg = 225f;     // distance and overlay shots: camera to the south-west, so the +X scale bar sits right and behind
        const float PilotHeightM = 1.5f;
        const float GroundSizeM = 400f;
        const float GroundAlbedo = 0.18f;
        const float BarHeightM = 1.8f, BarPostM = 0.05f, BarBandM = 0.1f, BarGapM = 1f, BarBearingDeg = 112.5f;
        const float MinShadowDistanceM = 12f;  // the stage's shadow distance: finer shadow texels than the game's 50 m on a small stage
        const float BoundsMarginM = 0.3f;      // a camera closer than this to the bounds is pushed outside them
        static readonly float[] DistancesM = { 1.5f, 10f, 50f, 200f };
        static readonly string[] Lights = { "day", "overcast" };
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
                string prefabPath = $"{PrefabsFolder}/{assetClass}/{variant}.prefab";
                string metaPath = $"{ModelsFolder}/{assetClass}/{id}/{variant}.assetmeta.json";
                foreach (string path in new[] { prefabPath, metaPath })
                    if (!File.Exists(ProjectPath(path)))
                        throw new FileNotFoundException($"{path} is missing: run the catalog import first");
                inputs.Add((variant, prefabPath, metaPath));
            }
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
                    Stage stage = Stage.Build(prefabPath, meta);
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

        // --- the catalog and the repo ---------------------------------------------------------------------------

        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        static string ProjectPath(string assetPath) => Path.Combine(RepoRoot, "unity", assetPath);

        static (string assetClass, string[] variants) CatalogEntry(string id)
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
        sealed class LightProfile
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

        static LightProfile LoadProfile(string light)
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

        static void ApplyLight(LightProfile profile, Rig rig)
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
            ground.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(GroundAlbedo, GroundAlbedo, GroundAlbedo), 0.05f);
        }

        static Material Lit(Color linearAlbedo, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("no URP Lit shader"));
            material.SetColor("_BaseColor", Encoded(linearAlbedo));
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            return material;
        }

        sealed class Stage
        {
            public GameObject Root;
            public Bounds AssetBounds;  // world
            public Bounds FrameBounds;  // asset plus scale bar, world
            public LODGroup LodGroup;

            public static Stage Build(string prefabPath, ReviewMeta meta)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                    ?? throw new FileNotFoundException($"{prefabPath} did not load as a prefab");
                var stage = new Stage { Root = new GameObject("ReviewStage") };
                GameObject instance = Object.Instantiate(prefab, stage.Root.transform);
                instance.name = prefab.name;
                instance.transform.localPosition = new Vector3(0f, -meta.Bounds.min.y, 0f); // on the ground
                stage.AssetBounds = new Bounds(meta.Bounds.center + instance.transform.localPosition, meta.Bounds.size);
                stage.LodGroup = instance.GetComponent<LODGroup>();

                // The bar stands 1 m outside the bounds, at a bearing off the 45° turntable grid so no view lines it up with the asset.
                float bearing = BarBearingDeg * Mathf.Deg2Rad;
                var towardsBar = new Vector3(Mathf.Sin(bearing), 0f, Mathf.Cos(bearing));
                Vector3 centre = stage.AssetBounds.center;
                Vector3 barBase = centre + towardsBar * (ExitDistance(stage.AssetBounds, centre, towardsBar) + BarGapM + BarPostM * 0.5f);
                barBase.y = 0f;
                Material light = Lit(new Color(0.8f, 0.8f, 0.8f), 0.05f), dark = Lit(new Color(0.03f, 0.03f, 0.03f), 0.05f);
                int bands = Mathf.RoundToInt(BarHeightM / BarBandM);
                for (int i = 0; i < bands; i++)
                {
                    GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    band.name = $"ScaleBar{i}";
                    Object.DestroyImmediate(band.GetComponent<Collider>());
                    band.transform.SetParent(stage.Root.transform, false);
                    band.transform.localPosition = barBase + Vector3.up * (BarBandM * (i + 0.5f));
                    band.transform.localScale = new Vector3(BarPostM, BarBandM, BarPostM);
                    band.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? light : dark;
                }
                stage.FrameBounds = stage.AssetBounds;
                stage.FrameBounds.Encapsulate(new Bounds(barBase + Vector3.up * (BarHeightM * 0.5f), new Vector3(BarPostM, BarHeightM, BarPostM)));
                return stage;
            }

            public Matrix4x4 LocalToWorld => Root.transform.GetChild(0).localToWorldMatrix;
        }

        // --- the camera rig (design decision 4) ------------------------------------------------------------------

        sealed class Rig : IDisposable
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

            /// <summary>Renders the camera from <paramref name="position"/> looking at <paramref name="lookAt"/> and reads the pixels back.</summary>
            public ReviewCanvas Shoot(Vector3 position, Vector3 lookAt)
            {
                Camera.transform.position = position;
                Camera.transform.LookAt(lookAt, Vector3.up);
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
                readback.Apply(false);
                RenderTexture.active = null;
                return ReviewCanvas.FromTexture(readback);
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
        static Vector3 SlantShot(Bounds bounds, float distance, out bool fromBounds)
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

        // --- the sheet (design decisions 5 and 6) ------------------------------------------------------------------

        static ReviewCanvas Sheet(Stage stage, ReviewMeta meta, LightProfile profile, Rig rig, string variant, string gpu, string quality)
        {
            string title = $"{variant}  {profile.name}";
            float turntableDistance = Mathf.Max(0.5f, stage.FrameBounds.extents.magnitude / Mathf.Sin(VerticalFovDeg * 0.5f * Mathf.Deg2Rad) * 1.1f);
            var tiles = new List<ReviewCanvas>();

            // The game's shadow distance (50 m at High) gives 2.4 cm shadow texels, too coarse for a small stage; fit it to
            // the stage for the capture and put it back afterwards. The value never exceeds the project's.
            UniversalRenderPipelineAsset pipeline = UniversalRenderPipeline.asset;
            float projectShadowDistance = pipeline != null ? pipeline.shadowDistance : 0f;
            float shadowDistance = pipeline != null ? Mathf.Min(projectShadowDistance, Mathf.Max(MinShadowDistanceM, 4f * turntableDistance)) : 0f;
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
                Vector3 position = pilot ? PilotShot(asset, out fromBounds) : SlantShot(asset, distance, out fromBounds);
                string view = pilot ? $"{distance:0.#} M PILOT HEIGHT AZ {ShotAzimuthDeg:0}" : $"{distance:0.#} M SLANT {LookDownDeg:0}° DOWN AZ {ShotAzimuthDeg:0}";
                if (fromBounds)
                    view += $" FROM BOUNDS ({Vector3.Distance(position, asset.center):0.0} M TO CENTRE)";
                tiles.Add(Shot(rig, stage, position, asset.center, title, view, null, null));
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
            foreach ((Color32 color, string text) in new[]
                     {
                         (ReviewMeta.CollisionColor, $"COLLISION ({meta.Collision.Count}): {Kinds(meta.Collision)}"),
                         (ReviewMeta.GapColor, $"GAPS ({meta.Gaps.Count}), WITH THEIR NORMALS"),
                         (ReviewMeta.WindColor, $"WIND VOLUMES ({meta.WindVolumes.Count}): {Kinds(meta.WindVolumes)}"),
                         (ReviewMeta.SnagColor, $"SNAG ({meta.Snags.Count}), CROSSES AT THE POINTS"),
                     })
            {
                panel.Fill(24, y + 2, 18, 18, color);
                panel.Text(56, y, text, 3, White);
                y += step;
            }
            y += 12;
            foreach (string line in new[]
                     {
                         $"BOUNDS {meta.Bounds.size.x:0.000} X {meta.Bounds.size.y:0.000} X {meta.Bounds.size.z:0.000} M",
                         $"LODS {meta.LodSummary}, CULL {meta.CullDistance:0.#} M",
                         $"MATERIALS {meta.MaterialSummary}",
                         $"LIGHT {profile.name}: {profile.Summary}",
                         $"AMBIENT {Luminance(profile.Ambient):0.00}, ILLUMINANCE {profile.HorizontalIlluminance:0.00}, GROUND ALBEDO {GroundAlbedo:0.00}",
                         $"FOV {VerticalFovDeg:0}° VERT, TILES {Tile} PX, BAR {BarHeightM:0.0} M / {BarBandM * 100:0} CM BANDS",
                         shadows,
                         $"QUALITY {quality}",
                         $"GPU {gpu}",
                     })
            {
                panel.Text(24, y, line, 3, White);
                y += step;
            }
            panel.Outline(0, 0, Tile, Tile, Frame, 2);
            return panel;
        }

        static string Kinds(List<ReviewMeta.Shape> shapes) =>
            shapes.Count == 0 ? "NONE" : string.Join(", ", shapes.GroupBy(s => s.kind).Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} {g.Key}"));
    }
}
