using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Feed;
using Fpv450.Input;
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
    /// Builds <c>Gallery_LookLock.unity</c> (p1-g1-look-lock-gallery design 1-3, 5 and 6) from
    /// <see cref="GalleryStations"/>: the floor, the look setup, the noclip camera with its keys and boot, and each
    /// station's prefabs under its numbered marker, the L03 spans hung from the L01 ports. The scene is never edited
    /// by hand: change the table and run <c>-batchmode -quit -executeMethod Fpv450.GalleryScene.Build</c> (exit code 1
    /// on any error). A rebuilt prefab shows in the scene without a rebuild; a pole whose ports move needs one.
    /// </summary>
    public static class GalleryScene
    {
        public const string ScenePath = "Assets/_Project/Scenes/Gallery_LookLock.unity";
        const string Folder = "Assets/_Project/Scenes/Gallery";
        const string FloorMeshPath = Folder + "/Floor.asset";
        const string FloorMaterialPath = Folder + "/Floor.mat";
        const string MacroPath = Folder + "/FloorMacro.png";
        const string CoverSet = "Assets/_Project/Art/ThirdParty/texturecan/ground_0018/ground_0018";

        // The floor (design 2): the straw cover set over 2.4 km, so the far ground lies in the haze from every
        // viewpoint (about 1 km or more ahead), with a hole where a surface or earthwork patch carries its own ground.
        static readonly Vector2 FloorCentre = new Vector2(135f, 0f);
        const float FloorHalfM = 1200f;
        const float CoverTileM = 2f;                 // the set's tile in belt_straw's recipe
        const float CoverSmoothness = 0.15f;         // matte: at the set's own 0.4 the far floor took a grey sky sheen the patch's straws do not show
        // The set's mean (linear 0.555 / 0.335 / 0.117) gained so the floor renders as the S04 patch does from its
        // viewpoint, the mat with its straws (day: sRGB 113 / 105 / 88, measured in the build), so the patch's edge is
        // no step: linear gains 0.284 / 0.358 / 0.552 (S04's mat albedo #7a7055 alone would be 0.350 / 0.483 / 0.779),
        // written sRGB-encoded as URP's base colour is. No occlusion map: the set's AO imports single-channel, and
        // URP/Lit reads its green.
        static readonly Color CoverGain = new Color(0.5693f, 0.6327f, 0.7686f);
        // The macro variation (design 2, assumed): value noise of 30 m cells and a 15 m octave over a 480 m tile,
        // brightness +-10 % and a yellow-grey shift, through URP/Lit's detail albedo x2 (linear 0.5 is neutral).
        const float MacroTileM = 480f;
        const int MacroCells = 16, MacroPx = 256;
        const float MacroBrightness = 0.10f, MacroRed = 0.04f, MacroBlue = 0.06f;

        // The camera (design 5, task 1.1's As built): the noclip's 60° vertical, skybox, no post, no AA, MSAA as URP-High has it (1x).
        const float FovDeg = 60f, NearClipM = 0.05f, FarClipM = 3000f;

        // A prebuilt span placed as an object sits on its ports within 5 mm, and no chord leaves a port back across
        // its own pole by more than 5° (p1-l01-l03-pole-wires review-physics §1, items 3 and 5).
        const float PortToleranceM = 0.005f;
        static readonly float BackAngleSin = Mathf.Sin(5f * Mathf.Deg2Rad);

        public static void Build()
        {
            try
            {
                BuildScene();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GalleryScene] FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static void BuildScene()
        {
            ShaderUtil.allowAsyncCompilation = false;
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder).Replace('\\', '/'), Path.GetFileName(Folder));

            var stations = new GameObject("Stations").transform;
            var holes = new List<Rect>();
            int prefabs = 0;
            foreach (GalleryStations.Station station in GalleryStations.Stations)
                prefabs += Place(station, stations, holes);
            Floor(holes, FloorCentre, FloorHalfM, FloorMeshPath);

            GameObject go = Rig(out LookSetup look, out ViewpointKeys keys);
            var boot = go.AddComponent<GalleryBoot>();
            boot.stations = stations;
            boot.keys = keys;
            boot.look = look;
            GalleryStations.Viewpoint start = GalleryStations.Viewpoints[0];
            go.transform.SetPositionAndRotation(start.Position, start.Rotation);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"could not save {ScenePath}");
            AssetDatabase.SaveAssets();
            Debug.Log($"[GalleryScene] wrote {ScenePath}: {GalleryStations.Stations.Length} stations, {prefabs} prefabs, {GalleryStations.Viewpoints.Length} viewpoints, " +
                      $"floor {2f * FloorHalfM:0} m with {holes.Count} holes");
        }

        /// <summary>
        /// The look setup with G1's four presets, and the camera (design 5) with the noclip, the number keys and the
        /// preset keys; the test range's scene builder shares it. The caller adds its boot and the start pose.
        /// </summary>
        internal static GameObject Rig(out LookSetup look, out ViewpointKeys keys)
        {
            look = new GameObject("Look").AddComponent<LookSetup>();
            look.skyModel = Load<TextAsset>(LookSetup.SkyModelPath);
            look.presets = LookSetup.PresetPaths.Select(Load<TextAsset>).ToArray();
            look.skyShader = Load<Shader>(LookSetup.SkyShaderPath);

            var go = new GameObject("Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = FovDeg;
            camera.nearClipPlane = NearClipM;
            camera.farClipPlane = FarClipM;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            go.AddComponent<AudioListener>();
            var noclip = go.AddComponent<NoclipCamera>();
            keys = go.AddComponent<ViewpointKeys>();
            keys.noclip = noclip;
            go.AddComponent<LightPresets>().look = look;
            look.viewCamera = camera;
            return go;
        }

        // --- the stations ------------------------------------------------------------------------------------------

        static int Place(GalleryStations.Station station, Transform stations, List<Rect> holes)
        {
            var marker = new GameObject(station.MarkerName).transform;
            marker.SetParent(stations, false);
            marker.localPosition = station.Origin;
            string at = $"({station.Origin.x:0}, {station.Origin.y:0}, {station.Origin.z:0})";
            if (station.Placements.Length == 0)
            {
                Debug.Log($"[GalleryScene] station {station.MarkerName} ({station.Letters}) at {at}: empty marker, its prefabs come with task 1.3");
                return 0;
            }

            var placed = new GameObject[station.Placements.Length];
            for (int i = 0; i < placed.Length; i++)
            {
                GalleryStations.Placement placement = station.Placements[i];
                placed[i] = Instantiate(placement.Prefab, marker);
                placed[i].transform.localPosition = new Vector3(placement.X, 0f, placement.Z);
                placed[i].transform.localRotation = Quaternion.Euler(0f, placement.YawDeg, 0f);
                // A surface or earthwork patch is its own ground: the floor leaves a hole over its bounds.
                if (placement.Prefab.StartsWith("surfaces/", StringComparison.Ordinal) || placement.Prefab.StartsWith("earthworks/", StringComparison.Ordinal))
                    holes.Add(Footprint(placed[i], placement.Prefab, placement.YawDeg));
            }
            foreach (GalleryStations.Hanging hanging in station.Hangings)
                Hang(hanging, marker, placed, station.Placements);

            foreach (Transform child in marker)
                Debug.Log($"[GalleryScene] station {station.MarkerName} ({station.Letters}): {child.name} at ({child.position.x:0.000}, {child.position.y:0.000}, {child.position.z:0.000}), " +
                          $"euler ({Mathf.DeltaAngle(0f, child.eulerAngles.x):0.000}°, {child.eulerAngles.y:0.00}°, {Mathf.DeltaAngle(0f, child.eulerAngles.z):0.000}°)");
            return marker.childCount;
        }

        internal static GameObject Instantiate(string prefab, Transform parent)
        {
            string path = $"{GalleryStations.PrefabsFolder}{prefab}.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                ?? throw new FileNotFoundException($"{path} is missing: build the asset (pwsh tools/build_asset.ps1) and git lfs checkout first");
            Review.RequireRenderable(asset, asset.name);
            foreach (Material material in asset.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials))
                if (material.shader == null || material.shader.name == "Hidden/InternalErrorShader")
                    throw new InvalidOperationException($"{path}: {material.name} has no shader (it would draw pink)");
            return (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        }

        internal static AssetPhysicsMeta Meta(GameObject instance) =>
            instance.GetComponent<AssetPhysicsMeta>() ?? throw new InvalidOperationException($"{instance.name} has no AssetPhysicsMeta");

        /// <summary>A patch's plan footprint in the scene: its metadata bounds under its placement, at a multiple of 90°.</summary>
        internal static Rect Footprint(GameObject instance, string prefab, float yawDeg)
        {
            if (Mathf.Repeat(yawDeg, 90f) != 0f)
                throw new InvalidOperationException($"{prefab}: a patch that cuts the floor must stand at a multiple of 90°, not {yawDeg}°");
            Bounds bounds = Meta(instance).bounds;
            Vector3 a = instance.transform.TransformPoint(bounds.min), b = instance.transform.TransformPoint(bounds.max);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
        }

        /// <summary>
        /// Hangs a span by its ports: the first conductor's start on its port (a conductor's centre is its port plus
        /// one radius along the port's direction), the far pole slid along the line until its port lies one chord
        /// away, the span turned so the chord meets it with the sag kept vertical; a free end leaves along the port's
        /// outward direction. Every conductor's ends are then checked against their ports.
        /// </summary>
        static void Hang(GalleryStations.Hanging hanging, Transform marker, GameObject[] placed, GalleryStations.Placement[] placements)
        {
            GameObject span = Instantiate(hanging.Span, marker);
            AssetPhysicsMeta.Wire[] wires = Meta(span).wires;
            bool free = hanging.To < 0;
            if (wires.Length != hanging.FromPorts.Length || (!free && wires.Length != hanging.ToPorts.Length))
                throw new InvalidOperationException($"{hanging.Name}: {wires.Length} conductors for {hanging.FromPorts.Length} and {hanging.ToPorts.Length} ports");
            GameObject from = placed[hanging.From], to = free ? null : placed[hanging.To];
            AssetPhysicsMeta.Wire first = wires[0];
            Vector3 p0 = first.points[0], chordLocal = first.points[first.points.Length - 1] - p0;
            Vector3 start = Seat(from, hanging.FromPorts[0], first);
            Quaternion turn;
            if (free)
            {
                Vector3 outward = Vector3.ProjectOnPlane(Direction(from, hanging.FromPorts[0]), Vector3.up);
                turn = Quaternion.LookRotation(outward) * Quaternion.Inverse(Quaternion.LookRotation(Vector3.ProjectOnPlane(chordLocal, Vector3.up)));
            }
            else
            {
                Vector3 nominal = to.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 chord = Seat(to, hanging.ToPorts[0], first) - start;
                    to.transform.position -= Vector3.ProjectOnPlane(chord, Vector3.up).normalized * (chord.magnitude - chordLocal.magnitude);
                }
                float slid = Vector3.Distance(nominal, to.transform.position);
                Debug.Log($"[GalleryScene] {placements[hanging.To].Name} slid {1000f * slid:0.0} mm along the line so its {hanging.ToPorts[0]} lies {hanging.Name}'s {chordLocal.magnitude:0.000} m chord from {placements[hanging.From].Name}'s {hanging.FromPorts[0]}");
                turn = Quaternion.LookRotation(Seat(to, hanging.ToPorts[0], first) - start) * Quaternion.Inverse(Quaternion.LookRotation(chordLocal));
            }
            span.transform.SetPositionAndRotation(start - turn * p0, turn);
            CheckEnds("[GalleryScene]", hanging.Name, span, from, placements[hanging.From].Name, hanging.FromPorts,
                      to, free ? null : placements[hanging.To].Name, hanging.ToPorts);
        }

        /// <summary>
        /// Logs where each conductor of a placed span ends against its ports, and throws when one ends more than 5 mm
        /// off its seat or leaves it back across its pole; <paramref name="to"/> null is a free end. The test range's
        /// scene builder checks its spans, placed from their data, with it.
        /// </summary>
        internal static void CheckEnds(string tag, string spanName, GameObject span, GameObject from, string fromName, string[] fromPorts,
                                       GameObject to, string toName, string[] toPorts)
        {
            AssetPhysicsMeta.Wire[] wires = Meta(span).wires;
            for (int i = 0; i < wires.Length; i++)
            {
                AssetPhysicsMeta.Wire wire = wires[i];
                Vector3 a = span.transform.TransformPoint(wire.points[0]), b = span.transform.TransformPoint(wire.points[wire.points.Length - 1]);
                string line = $"{tag} {spanName} conductor {i} ({1000f * wire.diameterM:0} mm): " +
                              End(from, fromName, fromPorts[i], wire, a, b - a);
                if (to != null)
                    line += "; " + End(to, toName, toPorts[i], wire, b, a - b);
                else
                    line += $"; free end at ({b.x:0.000}, {b.y:0.000}, {b.z:0.000}), its house not on this station";
                Debug.Log(line);
            }
        }

        static string End(GameObject pole, string poleName, string portId, AssetPhysicsMeta.Wire wire, Vector3 end, Vector3 chord)
        {
            float off = Vector3.Distance(end, Seat(pole, portId, wire));
            float along = Vector3.Dot(chord.normalized, Direction(pole, portId));
            if (off > PortToleranceM)
                throw new InvalidOperationException($"{wire.slot} conductor ends {1000f * off:0.0} mm off {poleName} {portId}, over {1000f * PortToleranceM:0} mm");
            if (along < -BackAngleSin)
                throw new InvalidOperationException($"{wire.slot} conductor leaves {poleName} {portId} back across the pole (chord · outward {along:0.000})");
            AssetPhysicsMeta.Port port = Port(pole, portId);
            if (wire.diameterM > port.maxDiameterM + 1e-6f)
                Debug.LogWarning($"[GalleryScene] {wire.slot}: the {1000f * wire.diameterM:0} mm conductor is over {poleName} {portId}'s {1000f * port.maxDiameterM:0} mm seat (visual only at G1: a wire has no collider, review-physics §1)");
            return $"{1000f * off:0.00} mm off {poleName} {portId}, chord · outward {along:+0.000;-0.000}";
        }

        static AssetPhysicsMeta.Port Port(GameObject pole, string id) =>
            Meta(pole).ports.FirstOrDefault(p => p.id == id) ?? throw new InvalidOperationException($"{pole.name} has no port {id}");

        static Vector3 Direction(GameObject pole, string id) => pole.transform.TransformDirection(Port(pole, id).direction);

        static Vector3 Seat(GameObject pole, string id, AssetPhysicsMeta.Wire wire) =>
            pole.transform.TransformPoint(Port(pole, id).position) + Direction(pole, id) * (0.5f * wire.diameterM);

        // --- the floor ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The floor (design 2): the straw cover over a square of half side <paramref name="halfM"/> about
        /// <paramref name="floorCentre"/>, less the holes, its mesh written to <paramref name="meshPath"/>; the test range
        /// shares it and its material.
        /// </summary>
        internal static GameObject Floor(List<Rect> holes, Vector2 floorCentre, float halfM, string meshPath)
        {
            var go = new GameObject("Floor");
            go.AddComponent<MeshFilter>().sharedMesh = FloorMesh(holes, floorCentre, halfM, meshPath);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FloorMaterial();
            // As the review's ground: it only receives, so it never sets the sun's shadow depth range.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>The floor's rectangle less the holes, as the cells of the grid their edges cut it into; UVs in metres.</summary>
        static Mesh FloorMesh(List<Rect> holes, Vector2 floorCentre, float halfM, string meshPath)
        {
            var floor = Rect.MinMaxRect(floorCentre.x - halfM, floorCentre.y - halfM, floorCentre.x + halfM, floorCentre.y + halfM);
            float[] xs = holes.SelectMany(h => new[] { h.xMin, h.xMax }).Append(floor.xMin).Append(floor.xMax).Distinct().OrderBy(x => x).ToArray();
            float[] zs = holes.SelectMany(h => new[] { h.yMin, h.yMax }).Append(floor.yMin).Append(floor.yMax).Distinct().OrderBy(z => z).ToArray();
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i + 1 < xs.Length; i++)
                for (int j = 0; j + 1 < zs.Length; j++)
                {
                    var centre = new Vector2(0.5f * (xs[i] + xs[i + 1]), 0.5f * (zs[j] + zs[j + 1]));
                    if (holes.Any(h => h.Contains(centre)))
                        continue;
                    int n = vertices.Count;
                    vertices.Add(new Vector3(xs[i], 0f, zs[j]));
                    vertices.Add(new Vector3(xs[i], 0f, zs[j + 1]));
                    vertices.Add(new Vector3(xs[i + 1], 0f, zs[j + 1]));
                    vertices.Add(new Vector3(xs[i + 1], 0f, zs[j]));
                    triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 }); // clockwise from above: facing up
                }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Floor" };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, vertices.Count).ToList());
            mesh.SetUVs(0, vertices.Select(v => new Vector2(v.x, v.z)).ToList());
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        internal static Material FloorMaterial()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("no URP/Lit shader");
            var material = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (material == null)
            {
                material = new Material(lit) { name = "Floor" };
                AssetDatabase.CreateAsset(material, FloorMaterialPath);
            }
            material.shader = lit;
            material.SetTexture("_BaseMap", Load<Texture2D>($"{CoverSet}_color_2k.jpg"));
            material.SetTextureScale("_BaseMap", Vector2.one / CoverTileM);
            material.SetColor("_BaseColor", CoverGain);
            material.SetTexture("_BumpMap", Load<Texture2D>($"{CoverSet}_normal_opengl_2k.png"));
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_OcclusionMap", null);
            material.DisableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_Smoothness", CoverSmoothness);
            material.SetFloat("_Metallic", 0f);
            // URP/Lit's detail UV is the base UV times the detail tiling: the macro tile repeats every MacroTileM metres.
            material.SetTexture("_DetailAlbedoMap", Macro());
            material.SetTextureScale("_DetailAlbedoMap", Vector2.one * (CoverTileM / MacroTileM));
            material.SetFloat("_DetailAlbedoMapScale", 1f);
            material.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>The macro variation as a linear texture, written only when its pixels change.</summary>
        static Texture2D Macro()
        {
            var texture = new Texture2D(MacroPx, MacroPx, TextureFormat.RGB24, false, true);
            var pixels = new Color32[MacroPx * MacroPx];
            for (int y = 0; y < MacroPx; y++)
                for (int x = 0; x < MacroPx; x++)
                {
                    float u = (float)x / MacroPx, v = (float)y / MacroPx;
                    float tone = (Noise(u, v, MacroCells, 1) + 0.5f * Noise(u, v, 2 * MacroCells, 2)) / 1.5f;
                    float shift = Noise(u, v, MacroCells, 3);
                    float grey = 0.5f * (1f + MacroBrightness * tone);
                    pixels[y * MacroPx + x] = new Color(grey * (1f + MacroRed * shift), grey, grey * (1f - MacroBlue * shift));
                }
            texture.SetPixels32(pixels);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            string file = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, MacroPath);
            if (!File.Exists(file) || !File.ReadAllBytes(file).SequenceEqual(png))
            {
                File.WriteAllBytes(file, png);
                AssetDatabase.ImportAsset(MacroPath, ImportAssetOptions.ForceUpdate);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(MacroPath);
            if (importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Repeat || importer.textureCompression != TextureImporterCompression.Uncompressed || !importer.mipmapEnabled)
            {
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            return Load<Texture2D>(MacroPath);
        }

        /// <summary>Tileable value noise in -1..1 over <paramref name="cells"/> lattice cells a tile, smoothstep between them.</summary>
        static float Noise(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells;
            int i = Mathf.FloorToInt(x), j = Mathf.FloorToInt(y);
            float fx = x - i, fy = y - j;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float Corner(int di, int dj) => Lattice((i + di) % cells, (j + dj) % cells, seed);
            return Mathf.Lerp(Mathf.Lerp(Corner(0, 0), Corner(1, 0), fx), Mathf.Lerp(Corner(0, 1), Corner(1, 1), fx), fy);
        }

        static float Lattice(int i, int j, int seed)
        {
            uint h = (uint)i * 0x8da6b343u ^ (uint)j * 0xd8163841u ^ (uint)seed * 0xcb1ab31fu;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0xffffffu) / 8388607.5f - 1f;
        }

        internal static T Load<T>(string path) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException($"{path} is missing");
    }
}
