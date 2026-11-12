using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.App;
using Fpv450.Assets;
using Fpv450.Feed;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// Each map's still for Play's carousel (w10-play-maps 1.2, D-100): the map's scene seen through the analog feed with
    /// the OSD as Explore shows them, from a pose per map in <see cref="ParamsPath"/>. The rig is the noclip view's
    /// (FpvCamera with NoclipFeedSignals, the stand-in OSD with its throttle element at 0 %, NoclipFeed's altitude and
    /// distance off the camera, the timer at the field's time), at the params' pinned feed seed and field, so the still is
    /// the same on every build: seed 1 and field 0 are Explore's first frame's (the settings' seed, and the field the rig
    /// begins on). The feed's look is the rig's own (D-026). House (<c>test-range</c>) takes its pose from the
    /// params, under the range's start light (LookSetup's start preset, as its Start applies it). Village (<c>farmland</c>,
    /// pose <c>explore</c>) is Explore's spawn frame: <see cref="FarmlandExplore.Open"/> itself sets the scene and the
    /// camera up, and the grass through the feed is FarmlandFeedDetail's. Each is drawn twice (the first warms the trees
    /// and the shaders up) and read after the second.
    /// <para>
    /// <see cref="Build.Windows"/> renders them before it builds, into <c>unity/Generated/stills/&lt;id&gt;.png</c>,
    /// git-ignored and never pushed (D-089), and carries them into the player's <c>StreamingAssets/generated/stills/</c>,
    /// where PlayMaps.StillPath finds them. <c>-batchmode -quit -executeMethod Fpv450.MapStills.Run</c> renders them alone;
    /// <see cref="Check"/> holds Village's still against Explore's first frame. Both need graphics (never
    /// <c>-nographics</c>); exit code 1 on any error.
    /// </para>
    /// </summary>
    public static class MapStills
    {
        public const string ParamsPath = "Assets/_Project/Editor/Stills/MapStills.json";
        public const string Kind = "stills";
        const string Tag = "[Stills]";
        const string ExplorePose = "explore", ParamsPose = "params";
        // The check's allowance for the wind's sway: the mean difference over the picture, in code values (the largest
        // channel's). Two renders of the same pose measured 4.8; the same still shifted by one pixel, 13.0.
        const double SwayMean = 8.0;
        const int SwayCodeValues = 30; // reported only: the share of pixels off by more than this

        sealed class Params
        {
            public int Width, Height, Seed, Field;
            public readonly List<MapParams> Maps = new List<MapParams>();
        }

        sealed class MapParams
        {
            public string Id, Scene, Pose;
            public Vector3 Position;
            public float YawDeg, PitchDeg;
        }

        /// <summary>What a still was drawn from, and the image.</summary>
        sealed class Shot
        {
            public Texture2D Image;
            public Vector3 Position;
            public Quaternion Rotation;
            public float FieldOfView;
            public string Feed;
        }

        /// <summary>The folder of the stills (unity/Generated/stills).</summary>
        public static string Folder => Path.Combine(Build.GeneratedFolder, Kind);

        /// <summary>Renders them, into the folder after <c>-out</c> when there is one (to look at a pose), else into
        /// <see cref="Folder"/>.</summary>
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-out");
            if (!RenderAll(at >= 0 && at + 1 < args.Length ? Path.GetFullPath(args[at + 1]) : Folder))
                EditorApplication.Exit(1);
        }

        /// <summary>Renders every map's still into <see cref="Folder"/>; false, with the error logged, on any failure.
        /// Leaves an empty scene open.</summary>
        public static bool RenderAll() => RenderAll(Folder);

        static bool RenderAll(string folder)
        {
            try
            {
                Params p = Load();
                Directory.CreateDirectory(folder);
                foreach (MapParams map in p.Maps)
                {
                    Shot shot = Render(p, map);
                    string png = Path.Combine(folder, map.Id + ".png");
                    File.WriteAllBytes(png, shot.Image.EncodeToPNG());
                    Object.DestroyImmediate(shot.Image);
                    Debug.Log($"{Tag} {map.Id}: {png} ({new FileInfo(png).Length} bytes), {p.Width}x{p.Height}, {Describe(shot)}");
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAILED: {e.Message}\n{e}");
                return false;
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        /// <summary>
        /// The spawn-frame match (w10-play-maps spec, "Village's still is Explore's first frame"): renders Village's still
        /// as <see cref="RenderAll"/> does, then Explore's first frame as the player sets it up (the scene opened afresh,
        /// FarmlandExplore.Open, the noclip rig from FeedSettings as NoclipFeed builds it), both at the still's size, field
        /// and OSD. The camera's position, rotation and field of view must be equal to the float, the field of view must be
        /// NoclipFeed's rig's, and the two images the same but for the wind's sway (<see cref="SwayMean"/>). Writes both PNGs to the folder after <c>-out</c>.
        /// <c>-batchmode -quit -executeMethod Fpv450.MapStills.Check -out &lt;folder&gt;</c>.
        /// </summary>
        public static void Check()
        {
            int failed = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-out");
                if (at < 0 || at + 1 >= args.Length)
                    throw new ArgumentException("usage: -executeMethod Fpv450.MapStills.Check -out <folder>");
                string folder = Path.GetFullPath(args[at + 1]);
                Directory.CreateDirectory(folder);
                Params p = Load();
                MapParams village = p.Maps.Find(m => m.Id == PlayMaps.Village.Id)
                                    ?? throw new InvalidOperationException($"{ParamsPath} has no {PlayMaps.Village.Id}");
                if (village.Pose != ExplorePose)
                    throw new InvalidOperationException($"{ParamsPath}: {village.Id}'s pose is '{village.Pose}', not Explore's spawn ('{ExplorePose}')");

                Shot still = Render(p, village);
                Shot explore = Shoot(p, OpenScene(village.Scene), scene =>
                {
                    if (FarmlandExplore.Open(scene) == null)
                        throw new InvalidOperationException("FarmlandExplore.Open failed");
                    return Camera.main;
                });
                File.WriteAllBytes(Path.Combine(folder, "farmland_still.png"), still.Image.EncodeToPNG());
                File.WriteAllBytes(Path.Combine(folder, "farmland_explore.png"), explore.Image.EncodeToPNG());

                FeedSettings asset = FeedSettings.Load();
                float rigFov = asset.VerticalFovDeg(asset.Coverage); // NoclipFeed's rig: the asset's settings, no shutter
                Report("position", Same(still.Position, explore.Position),
                    $"{Exact(still.Position)} vs {Exact(explore.Position)}");
                Report("rotation", Same(still.Rotation, explore.Rotation), $"{Exact(still.Rotation)} vs {Exact(explore.Rotation)}");
                Report("field of view", still.FieldOfView.Equals(explore.FieldOfView) && still.FieldOfView.Equals(rigFov),
                    $"{Exact(still.FieldOfView)} vs {Exact(explore.FieldOfView)} (NoclipFeed's rig {Exact(rigFov)})");
                // The same scene: the vegetation's wind runs on the editor's real time (URP's shader time outside play
                // mode, the terrain grass's waving), so grass and leaves sway between two renders and never match to the
                // byte; everything else does (the feed's field and the OSD are exact). A pose a pixel off, a missing object
                // or another feed field changes far more.
                Color32[] a = still.Image.GetPixels32(), b = explore.Image.GetPixels32();
                int differ = 0, far = 0;
                double sum = 0;
                for (int i = 0; i < a.Length && a.Length == b.Length; i++)
                {
                    int d = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b));
                    if (d > 0) differ++;
                    if (d > SwayCodeValues) far++;
                    sum += d;
                }
                double mean = a.Length > 0 ? sum / a.Length : 0, farShare = a.Length > 0 ? (double)far / a.Length : 1;
                Report("image", a.Length == b.Length && mean <= SwayMean,
                    $"{still.Image.width}x{still.Image.height} vs {explore.Image.width}x{explore.Image.height}, {differ} pixels differ at all, " +
                    $"{far} ({farShare:P2}) by more than {SwayCodeValues} code values, the mean difference {mean:0.00} code values (at most {SwayMean:0.0}, the wind's sway)");
                Object.DestroyImmediate(still.Image);
                Object.DestroyImmediate(explore.Image);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} check FAILED: {e.Message}\n{e}");
                failed++;
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            Debug.Log($"{Tag} check: {(failed == 0 ? "PASS" : $"FAIL, {failed} failed")}");
            if (failed > 0)
                EditorApplication.Exit(1);

            void Report(string what, bool ok, string detail)
            {
                if (!ok) failed++;
                Debug.Log($"{Tag} check farmland {what}: still vs Explore's first frame {detail}: {(ok ? "PASS" : "FAIL")}");
            }
        }

        static Params Load()
        {
            Dictionary<string, object> root = Json.Obj(Json.Parse(File.ReadAllText(ParamsPath)), ParamsPath);
            var p = new Params
            {
                Width = (int)Json.Num(root, "width_px", ParamsPath), Height = (int)Json.Num(root, "height_px", ParamsPath),
                Seed = (int)Json.Num(root, "feed_seed", ParamsPath), Field = (int)Json.Num(root, "feed_field", ParamsPath),
            };
            if (p.Width <= 0 || p.Height <= 0 || p.Field < 0)
                throw new FormatException($"{ParamsPath}: an empty image or a negative field");
            foreach (object entry in Json.Arr(root, "maps", ParamsPath))
            {
                Dictionary<string, object> m = Json.Obj(entry, ParamsPath + " maps[]");
                string what = $"{ParamsPath} {Json.Str(m, "id", ParamsPath)}";
                var map = new MapParams { Id = Json.Str(m, "id", what), Scene = Json.Str(m, "scene", what), Pose = Json.Str(m, "pose", what) };
                if (map.Pose == ParamsPose)
                {
                    map.Position = Json.Vec3(m, "position_m", what);
                    map.YawDeg = Json.Num(m, "yaw_deg", what);
                    map.PitchDeg = Json.Num(m, "pitch_deg", what);
                }
                else if (map.Pose != ExplorePose)
                    throw new FormatException($"{what}: pose '{map.Pose}' is neither '{ParamsPose}' nor '{ExplorePose}'");
                Json.Str(m, "why", what); // each pose says why
                p.Maps.Add(map);
            }
            return p;
        }

        /// <summary>The map's still: its scene opened alone, the camera at the map's pose, through the noclip rig.</summary>
        static Shot Render(Params p, MapParams map) => Shoot(p, OpenScene(map.Scene), scene =>
        {
            if (map.Pose == ExplorePose)
            {
                // Explore's own set-up of the scene and its camera (the stand on the terrain, the eye behind it), so the
                // still can't drift from Explore's spawn frame.
                if (FarmlandExplore.Open(scene) == null)
                    throw new InvalidOperationException("FarmlandExplore.Open failed");
                return Camera.main;
            }
            LookSetup look = Object.FindFirstObjectByType<LookSetup>();
            if (look != null)
                look.Select(look.startPreset); // the range's light as its Start applies it
            Camera camera = Camera.main;
            if (camera == null || camera.gameObject.scene != scene)
                throw new InvalidOperationException($"{map.Scene} has no main camera");
            // NoclipCamera's pose: yaw about up, pitch down, no roll.
            camera.transform.SetPositionAndRotation(map.Position, Quaternion.Euler(map.PitchDeg, map.YawDeg, 0f));
            return camera;
        });

        static Scene OpenScene(string path)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("no graphics device: the stills render, so run Unity without -nographics");
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        /// <summary>The open scene through the noclip rig on the camera <paramref name="pose"/> sets up, at the params'
        /// size, seed and field.</summary>
        static Shot Shoot(Params p, Scene scene, Func<Scene, Camera> pose)
        {
            bool async = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(p.Width, p.Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            FpvCamera rig = null;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Camera camera = pose(scene) ?? throw new InvalidOperationException($"{scene.name} has no main camera");
                // FarmlandFeedDetail's grass through the feed (Explore puts it on the camera; its LateUpdate doesn't run in
                // edit mode). The friend's surround culling, which doesn't run either, only turns far tiles' grass off.
                if (camera.GetComponent<FarmlandFeedDetail>() != null)
                    foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
                        t.detailObjectDistance = Mathf.Min(t.detailObjectDistance, FarmlandFeedDetail.FeedGrassDistance);

                // The noclip view's rig, as NoclipFeed builds it, at the pinned seed and field.
                FeedSettings settings = Object.Instantiate(FeedSettings.Load()
                    ?? throw new InvalidOperationException("no Resources/FeedSettings asset"));
                settings.hideFlags = HideFlags.DontSave;
                settings.seed = p.Seed;
                var signals = new NoclipFeedSignals(camera.transform);
                NoclipFeed.Place(signals.Stub.Osd, camera.transform.position);
                signals.Stub.Osd.ElapsedSeconds = p.Field / FpvCamera.FieldsPerSecond;
                rig = camera.gameObject.AddComponent<FpvCamera>();
                rig.settings = settings;
                rig.Signals = signals;
                rig.pinnedField = p.Field;
                rig.Begin();
                if (!rig.Running)
                    throw new InvalidOperationException("the feed's rig did not begin");
                rig.Output.targetTexture = target;
                rig.Step(p.Field);
                for (int i = 0; i < 2; i++)
                {
                    camera.Render();
                    rig.Output.Render();
                }
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(p.Width, p.Height, TextureFormat.RGB24, false) { hideFlags = HideFlags.DontSave }; // kept over the next scene's opening
                image.ReadPixels(new Rect(0, 0, p.Width, p.Height), 0, 0, false);
                image.Apply();
                RenderTexture.active = previous;
                FeedLoss loss = rig.Events.Loss;
                return new Shot
                {
                    Image = image,
                    Position = camera.transform.position,
                    Rotation = camera.transform.rotation,
                    FieldOfView = camera.fieldOfView,
                    Feed = $"seed {settings.seed} field {rig.Events.Field}: {loss.Current}, degrade {rig.Events.Degrade:0.00}, " +
                           $"N3 {rig.Events.N3Amp:0.000}, N4 {rig.Events.N4Amp:0.000}; OSD {FeedOsd.Describe(signals)}",
                };
            }
            finally
            {
                if (rig != null)
                {
                    rig.End();
                    Object.DestroyImmediate(rig.settings);
                    Object.DestroyImmediate(rig);
                }
                ShaderUtil.allowAsyncCompilation = async;
                target.Release();
                Object.DestroyImmediate(target);
            }
        }

        static string Describe(Shot s) =>
            $"camera at {Exact(s.Position)}, rotation {Exact(s.Rotation)} (euler {s.Rotation.eulerAngles:0.000}), vertical field of view {Exact(s.FieldOfView)}°; {s.Feed}; " +
            $"quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, GPU {SystemInfo.graphicsDeviceName}";

        static bool Same(Vector3 a, Vector3 b) => a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z);
        static bool Same(Quaternion a, Quaternion b) => a.x.Equals(b.x) && a.y.Equals(b.y) && a.z.Equals(b.z) && a.w.Equals(b.w);
        static string Exact(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        static string Exact(Vector3 v) => $"({Exact(v.x)}, {Exact(v.y)}, {Exact(v.z)})";
        static string Exact(Quaternion q) => $"({Exact(q.x)}, {Exact(q.y)}, {Exact(q.z)}, {Exact(q.w)})";
    }
}
