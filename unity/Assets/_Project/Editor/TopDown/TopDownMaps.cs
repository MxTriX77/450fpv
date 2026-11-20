using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Fpv450.App.Session;
using Fpv450.Assets;
using Fpv450.Feed;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Fpv450
{
    /// <summary>
    /// The top-down image of each map (w11-world-map 1.1, D-101), for the M map panel: each map's scene drawn
    /// orthographically from straight above, north up and east right, under the map's own light (the scene's as saved:
    /// the friend's sun and sky on the farmland; the range's LookSetup at its start preset, as its Start applies it),
    /// from the params per map in <see cref="ParamsPath"/> (the extent in the sim's frame and the image's size). As a plan,
    /// it leaves out fog and post-processing, draws every tree and LOD group at full detail (impostors are upright cards,
    /// edge-on from above) and no terrain grass (it draws only near the camera, so it would show as a disc).
    /// Rendered at twice the size and averaged down 2 x 2.
    /// <para>
    /// <see cref="Build.Windows"/> renders every map before it builds and carries <see cref="Build.GeneratedFolder"/> into
    /// the player's <c>StreamingAssets/generated/</c>, so the images live in <c>unity/Generated/topdown/</c> in the editor
    /// and in <c>StreamingAssets/generated/topdown/</c> in the player, git-ignored and never pushed (D-089). Per map:
    /// <c>&lt;id&gt;.png</c> and <c>&lt;id&gt;.json</c>:
    /// <code>
    /// {"format": "fpv450-topdown", "map": "farmland", "frame": "unity-lh-yup",
    ///  "x_m": [x0, x1], "z_m": [z0, z1], "width_px": W, "height_px": H}
    /// </code>
    /// The sim's (and Unity's, D-019) point (x, z) lies on the image at u = (x − x0) / (x1 − x0) · W pixels from its left
    /// edge and v = (z1 − z) / (z1 − z0) · H pixels from its top edge (the PNG's first row is the north edge); pixel
    /// (i, j) covers [i, i + 1) × [j, j + 1). A heading ψ (positive from north towards east, the drone's yaw) points along
    /// (sin ψ, −cos ψ) in (u, v). Texture2D.LoadImage puts the PNG's top row at the texture's top, so the texture's
    /// v coordinate is 1 − v / H.
    /// </para>
    /// <c>-batchmode -quit -executeMethod Fpv450.TopDownMaps.Run</c> renders them alone; <see cref="Check"/> tests the
    /// transform against known points. Both need graphics (never <c>-nographics</c>); exit code 1 on any error.
    /// </summary>
    public static class TopDownMaps
    {
        public const string ParamsPath = "Assets/_Project/Editor/TopDown/TopDownMaps.json";
        public const string Kind = "topdown";
        const string Tag = "[TopDown]";
        const int Supersample = 2;
        // The camera this high over the tallest terrain or renderer in the extent: over the trees on the highest hill,
        // and well inside the shadow distance, so the sun's shadows still draw.
        const float ClearanceM = 50f;
        const float TreeLodBias = 100f; // the friend's own plan capture's (FarmLandCapture)

        sealed class MapParams
        {
            public string Id, Scene;
            public float X0, X1, Z0, Z1;
            public int Width, Height;
        }

        /// <summary>The folder of the images and their transforms (unity/Generated/topdown).</summary>
        public static string Folder => Path.Combine(Build.GeneratedFolder, Kind);

        public static void Run()
        {
            if (!RenderAll())
                EditorApplication.Exit(1);
        }

        /// <summary>Renders every map's image and transform into <see cref="Folder"/>; false, with the error logged, on any
        /// failure. Leaves an empty scene open.</summary>
        public static bool RenderAll()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                foreach (MapParams map in Load())
                {
                    Open(map);
                    Texture2D image = Render(map, Array.Empty<Vector2>());
                    string png = Path.Combine(Folder, map.Id + ".png");
                    File.WriteAllBytes(png, image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                    File.WriteAllText(Path.Combine(Folder, map.Id + ".json"), TransformJson(map));
                    Debug.Log($"{Tag} {map.Id}: {png} ({new FileInfo(png).Length} bytes), {map.Width}x{map.Height} over x [{map.X0}, {map.X1}] z [{map.Z0}, {map.Z1}] m, " +
                              $"{(map.X1 - map.X0) / map.Width:0.###} m a pixel");
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
        /// The transform's check (w11-world-map 1.1): renders each map with a magenta marker 4 pixels wide at two known
        /// points, writes the image and its transform to the folder after <c>-out</c>, reads both back and finds each
        /// marker's centre on the PNG; every one must lie within one pixel of where the transform puts the point. Village:
        /// its start (FlightWorlds.FarmlandStart) and a ruin, the marker at the scene's object, the transform fed the
        /// package's position; House: its 'track' start and the placement file's first instance, likewise.
        /// <c>-batchmode -quit -executeMethod Fpv450.TopDownMaps.Check -out &lt;folder&gt;</c>.
        /// </summary>
        public static void Check()
        {
            int failed = 0;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int at = Array.IndexOf(args, "-out");
                if (at < 0 || at + 1 >= args.Length)
                    throw new ArgumentException("usage: -executeMethod Fpv450.TopDownMaps.Check -out <folder>");
                string folder = Path.GetFullPath(args[at + 1]);
                Directory.CreateDirectory(folder);
                foreach (MapParams map in Load())
                {
                    Open(map);
                    var points = KnownPoints(map.Id);
                    Texture2D image = Render(map, points.Select(p => p.scene).ToArray());
                    string png = Path.Combine(folder, map.Id + "_check.png"), json = Path.Combine(folder, map.Id + "_check.json");
                    File.WriteAllBytes(png, image.EncodeToPNG());
                    File.WriteAllText(json, TransformJson(map));
                    Object.DestroyImmediate(image);

                    var read = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    read.LoadImage(File.ReadAllBytes(png));
                    Dictionary<string, object> t = Json.Obj(Json.Parse(File.ReadAllText(json)), json);
                    Vector2 xs = Json.Vec2(t, "x_m", json), zs = Json.Vec2(t, "z_m", json);
                    float w = Json.Num(t, "width_px", json), h = Json.Num(t, "height_px", json);
                    foreach ((string name, Vector2 sim, Vector2 scene) in points)
                    {
                        var expected = new Vector2((sim.x - xs.x) / (xs.y - xs.x) * w, (zs.y - sim.y) / (zs.y - zs.x) * h);
                        bool found = Marker(read, expected, out Vector2 measured, out float area);
                        float error = (measured - expected).magnitude;
                        bool ok = found && error <= 1f;
                        if (!ok) failed++;
                        Debug.Log($"{Tag} check {map.Id} {name}: sim ({sim.x:0.###}, {sim.y:0.###}), scene ({scene.x:0.###}, {scene.y:0.###}) m; " +
                                  $"transform u {expected.x:0.000}, v {expected.y:0.000} px; marker on the PNG " +
                                  (found ? $"u {measured.x:0.000}, v {measured.y:0.000} px ({area:0.0} px²), off by {error:0.000} px" : "not found") +
                                  $": {(ok ? "PASS" : "FAIL")}");
                    }
                    Object.DestroyImmediate(read);
                }
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
        }

        static List<MapParams> Load()
        {
            Dictionary<string, object> root = Json.Obj(Json.Parse(File.ReadAllText(ParamsPath)), ParamsPath);
            var maps = new List<MapParams>();
            foreach (object entry in Json.Arr(root, "maps", ParamsPath))
            {
                Dictionary<string, object> m = Json.Obj(entry, ParamsPath + " maps[]");
                string what = $"{ParamsPath} {Json.Str(m, "id", ParamsPath)}";
                Vector2 x = Json.Vec2(m, "x_m", what), z = Json.Vec2(m, "z_m", what);
                var map = new MapParams
                {
                    Id = Json.Str(m, "id", what), Scene = Json.Str(m, "scene", what),
                    X0 = x.x, X1 = x.y, Z0 = z.x, Z1 = z.y,
                    Width = (int)Json.Num(m, "width_px", what), Height = (int)Json.Num(m, "height_px", what),
                };
                if (map.X1 <= map.X0 || map.Z1 <= map.Z0 || map.Width <= 0 || map.Height <= 0)
                    throw new FormatException($"{what}: an empty extent or image");
                if (Mathf.Abs((map.X1 - map.X0) / map.Width - (map.Z1 - map.Z0) / map.Height) > 1e-5f * (map.X1 - map.X0) / map.Width)
                    throw new FormatException($"{what}: the pixels aren't square (the extent's sides and the image's differ in aspect)");
                maps.Add(map);
            }
            return maps;
        }

        static string TransformJson(MapParams map)
        {
            static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
            return "{\n" +
                   "  \"format\": \"fpv450-topdown\",\n" +
                   $"  \"map\": \"{map.Id}\",\n" +
                   "  \"frame\": \"unity-lh-yup\",\n" +
                   $"  \"x_m\": [{F(map.X0)}, {F(map.X1)}],\n" +
                   $"  \"z_m\": [{F(map.Z0)}, {F(map.Z1)}],\n" +
                   $"  \"width_px\": {map.Width},\n" +
                   $"  \"height_px\": {map.Height}\n" +
                   "}\n";
        }

        /// <summary>Opens the map's scene alone and sets it up as a plan under its own light.</summary>
        static void Open(MapParams map)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("no graphics device: the top-down images render, so run Unity without -nographics");
            EditorSceneManager.OpenScene(map.Scene, OpenSceneMode.Single);
            LookSetup look = Object.FindFirstObjectByType<LookSetup>();
            if (look != null)
                look.Select(look.startPreset); // the range's light as its Start applies it
            RenderSettings.fog = false;
            foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                terrain.treeLODBiasMultiplier = TreeLodBias;
                terrain.detailObjectDistance = 0f;
                terrain.basemapDistance = 100000f;
                terrain.heightmapPixelError = 1f;
            }
            foreach (LODGroup group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
                group.ForceLOD(0);
        }

        /// <summary>The open scene drawn from above over the map's extent, with a magenta square 4 pixels wide over each of
        /// <paramref name="markers"/> (x, z).</summary>
        static Texture2D Render(MapParams map, IReadOnlyList<Vector2> markers)
        {
            var area = new Rect(map.X0, map.Z0, map.X1 - map.X0, map.Z1 - map.Z0);
            float top = Top(area, out string topBy);
            float eye = top + ClearanceM;
            Debug.Log($"{Tag} {map.Id}: {map.Scene}, the tallest in the extent {top:0.0} m ({topBy}), the camera at {eye:0.0} m, sun {(RenderSettings.sun != null ? RenderSettings.sun.transform.forward.ToString("0.000") : "none")}");

            bool async = ShaderUtil.allowAsyncCompilation;
            var objects = new List<Object>();
            int w = map.Width * Supersample, h = map.Height * Supersample;
            var big = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Bilinear };
            var small = new RenderTexture(map.Width, map.Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var go = new GameObject("TopDown camera") { hideFlags = HideFlags.HideAndDontSave };
                objects.Add(go);
                go.transform.SetPositionAndRotation(new Vector3(area.center.x, eye, area.center.y), Quaternion.Euler(90f, 0f, 0f)); // up north, right east
                Camera camera = go.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = area.height / 2f;
                camera.aspect = (float)map.Width / map.Height;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = eye + 100f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.allowHDR = true;
                camera.enabled = false;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;

                if (markers.Count > 0)
                {
                    var magenta = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
                    magenta.SetColor("_BaseColor", Color.magenta);
                    objects.Add(magenta);
                    float side = 4f * area.width / map.Width;
                    foreach (Vector2 m in markers)
                    {
                        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        objects.Add(cube);
                        Object.DestroyImmediate(cube.GetComponent<Collider>());
                        cube.transform.position = new Vector3(m.x, eye - 1f, m.y);
                        cube.transform.localScale = new Vector3(side, 0.1f, side);
                        var renderer = cube.GetComponent<MeshRenderer>();
                        renderer.sharedMaterial = magenta;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }

                var request = new RenderPipeline.StandardRequest { destination = big };
                for (int i = 0; i < 2; i++) // the first warms the terrain's trees and the shaders up
                {
                    if (RenderPipeline.SupportsRenderRequest(camera, request))
                        RenderPipeline.SubmitRenderRequest(camera, request);
                    else
                    {
                        camera.targetTexture = big;
                        camera.Render();
                        camera.targetTexture = null;
                    }
                }
                // Each small pixel's centre falls on the corner of its 2 x 2 block, where bilinear filtering averages the four.
                Graphics.Blit(big, small);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = small;
                var image = new Texture2D(map.Width, map.Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, map.Width, map.Height), 0, 0, false);
                image.Apply();
                RenderTexture.active = previous;
                return image;
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                foreach (Object o in objects)
                    Object.DestroyImmediate(o);
                big.Release();
                small.Release();
                Object.DestroyImmediate(big);
                Object.DestroyImmediate(small);
            }
        }

        /// <summary>The highest terrain or renderer over the area, m, and what it is.</summary>
        static float Top(Rect area, out string by)
        {
            float top = float.NegativeInfinity;
            by = "nothing";
            foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                Vector3 p = t.GetPosition(), s = t.terrainData.size;
                float y = p.y + t.terrainData.bounds.max.y;
                if (area.Overlaps(new Rect(p.x, p.z, s.x, s.z)) && y > top)
                {
                    top = y;
                    by = "terrain " + t.name;
                }
            }
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Bounds b = r.bounds;
                if (r.enabled && area.Overlaps(new Rect(b.min.x, b.min.z, b.size.x, b.size.z)) && b.max.y > top)
                {
                    top = b.max.y;
                    by = r.name;
                }
            }
            if (float.IsNegativeInfinity(top))
                throw new InvalidOperationException("nothing draws over the map's extent");
            return top;
        }

        /// <summary>The check's known points: a name, the point in the sim's frame (code or package) and where the open
        /// scene has it (x, z).</summary>
        static List<(string name, Vector2 sim, Vector2 scene)> KnownPoints(string map)
        {
            var points = new List<(string, Vector2, Vector2)>();
            if (map == FlightWorlds.FarmlandMap)
            {
                StartPoint start = FlightWorlds.FarmlandStart; // in the scene's frame, the sim's (D-019)
                var at = new Vector2((float)start.X, (float)start.Z);
                points.Add(($"start '{start.Name}'", at, at));
                string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "maps", FlightWorlds.FarmlandMap, "placements.json"));
                Dictionary<string, object> package = Json.Obj(Json.Parse(File.ReadAllText(file)), file);
                Dictionary<string, object> ruin = Json.Arr(package, "placements", file).Select(o => Json.Obj(o, file))
                    .First(p => Json.Str(p, "kind", file) == "ruin");
                string variant = Json.Str(ruin, "variant", file);
                Vector3 sim = Json.Vec3(ruin, "position_m", file);
                GameObject village = GameObject.Find("Village") ?? throw new InvalidOperationException("the farmland has no Village object");
                Transform scene = village.transform.Cast<Transform>()
                    .Where(c => PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject)?.name == variant)
                    .OrderBy(c => (new Vector2(c.position.x, c.position.z) - new Vector2(sim.x, sim.z)).sqrMagnitude).First();
                points.Add(($"ruin {Json.Num(ruin, "id", file)} {variant}", new Vector2(sim.x, sim.z), new Vector2(scene.position.x, scene.position.z)));
            }
            else if (map == FlightWorlds.TestRangeMap)
            {
                string file = TestRangeScene.PlacementsFile;
                Dictionary<string, object> package = TestRangeScene.LoadPlacements();
                Dictionary<string, object> start = Json.Arr(package, "start_points", file).Select(o => Json.Obj(o, file))
                    .First(p => Json.Str(p, "id", file) == FlightWorlds.HousesStart);
                Vector3 s = Json.Vec3(start, "position_m", file);
                points.Add(($"start '{FlightWorlds.HousesStart}'", new Vector2(s.x, s.z), new Vector2(s.x, s.z)));
                Dictionary<string, object> first = Json.Obj(Json.Arr(package, "instances", file)[0], file);
                string name = TestRangeScene.InstanceName((int)Json.Num(first, "id", file), Json.Str(first, "variant", file));
                Vector3 sim = Json.Vec3(first, "position_m", file);
                GameObject scene = GameObject.Find(name) ?? throw new InvalidOperationException($"the test range has no {name}");
                points.Add(($"instance {name}", new Vector2(sim.x, sim.z), new Vector2(scene.transform.position.x, scene.transform.position.z)));
            }
            else
                throw new InvalidOperationException($"no known points for map {map}");
            return points;
        }

        /// <summary>The magenta marker's centre near <paramref name="expected"/> (u, v from the top-left, px), weighted by how
        /// magenta each pixel is, and its area; false when no marker is within 12 pixels.</summary>
        static bool Marker(Texture2D image, Vector2 expected, out Vector2 centre, out float area)
        {
            const int Reach = 12;
            int w = image.width, h = image.height;
            Color32[] pixels = image.GetPixels32(); // row 0 is the bottom, the PNG's last
            double sum = 0, su = 0, sv = 0;
            int u0 = Mathf.Max(0, (int)expected.x - Reach), u1 = Mathf.Min(w - 1, (int)expected.x + Reach);
            int v0 = Mathf.Max(0, (int)expected.y - Reach), v1 = Mathf.Min(h - 1, (int)expected.y + Reach);
            for (int v = v0; v <= v1; v++)
                for (int u = u0; u <= u1; u++)
                {
                    Color32 p = pixels[(h - 1 - v) * w + u];
                    float weight = Mathf.Max(0, Mathf.Min(p.r, p.b) - p.g) / 255f;
                    sum += weight;
                    su += weight * (u + 0.5);
                    sv += weight * (v + 0.5);
                }
            area = (float)sum;
            centre = sum > 1 ? new Vector2((float)(su / sum), (float)(sv / sum)) : new Vector2(float.NaN, float.NaN);
            return sum > 1;
        }
    }
}
