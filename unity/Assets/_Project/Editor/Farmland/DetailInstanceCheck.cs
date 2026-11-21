using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Fpv450
{
    /// <summary>
    /// w9-village-physics 2.1's editor check: does <c>TerrainData.ComputeDetailInstanceTransforms</c> give exactly the grass
    /// instances the map's terrain draws, and at which density? It opens the farmland scene read-only (it never saves; the
    /// store's linked assets are only read) and writes a report and pictures to <c>-checkOut</c>:
    /// - per patch and layer: the cells' counts (GetDetailLayer) against the transforms at density 1 and at the terrain's
    ///   detailObjectDensity, and whether each cell holds exactly its count of transforms;
    /// - per window: the terrain's own drawing of its details from straight above (orthographic, no post, no MSAA), and the
    ///   transforms drawn with each prototype's mesh and materials (Graphics.RenderMesh), both against a magenta clear; the
    ///   two coverage masks are compared pixel by pixel.
    ///
    ///   Unity -batchmode -quit -projectPath unity -executeMethod Fpv450.DetailInstanceCheck.Run -checkOut &lt;dir&gt; -logFile -
    /// </summary>
    public static class DetailInstanceCheck
    {
        const string Tag = "[DetailInstanceCheck]";
        const string ScenePath = "Assets/Farm_land/Scenes/FarmLand.unity";
        const int Pixels = 1024;
        const float WindowM = 8f;

        // The windows' centres (m): the audit's densest tall cell (14 layers, 13 of them drawn), Grass_A's densest cell
        // (5 instances) and a dry-and-burnt cell on a patch corner.
        static readonly (string name, Vector2 xz)[] Windows =
        {
            ("tall_768_917", new Vector2(768.07f, 917.48f)),
            ("green_284_969", new Vector2(283.69f, 969.24f)),
            ("dense_870_152", new Vector2(869.63f, 151.86f)),
        };

        public static void Run()
        {
            try
            {
                string output = Argument("-checkOut") ?? throw new InvalidOperationException("pass -checkOut <dir>");
                Directory.CreateDirectory(output);
                var report = new StringBuilder();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Terrain map = Terrain.activeTerrains.FirstOrDefault(t => t.terrainData != null && t.terrainData.name == "FarmLand_TerrainData")
                    ?? throw new InvalidOperationException("no terrain with FarmLand_TerrainData");
                TerrainData data = map.terrainData;
                report.AppendLine($"unity {Application.unityVersion}, GPU {SystemInfo.graphicsDeviceName}, {SystemInfo.graphicsDeviceType}");
                report.AppendLine($"map terrain '{map.name}' at {V(map.transform.position)}, size {V(data.size)}");
                report.AppendLine($"detailObjectDensity {map.detailObjectDensity}, detailObjectDistance {map.detailObjectDistance}, scatter {data.detailScatterMode}, " +
                                  $"detailResolution {data.detailResolution}, perPatch {data.detailResolutionPerPatch}, patches {data.detailPatchCount}");
                DetailPrototype[] prototypes = data.detailPrototypes;
                for (int l = 0; l < prototypes.Length; l++)
                {
                    DetailPrototype p = prototypes[l];
                    report.AppendLine($"layer {l} {(p.prototype ? p.prototype.name : "-")}: renderMode {p.renderMode}, usePrototypeMesh {p.usePrototypeMesh}, useInstancing {p.useInstancing}, " +
                                      $"useDensityScaling {p.useDensityScaling}, density {p.density}, alignToGround {p.alignToGround}, positionJitter {p.positionJitter}, " +
                                      $"width {p.minWidth}-{p.maxWidth}, height {p.minHeight}-{p.maxHeight}, noiseSeed {p.noiseSeed}, noiseSpread {p.noiseSpread}, holeEdgePadding {p.holeEdgePadding}");
                }

                float cell = data.size.x / data.detailResolution;
                int per = data.detailResolutionPerPatch;
                var patches = new SortedSet<(int, int)>();
                foreach ((string _, Vector2 xz) in Windows)
                    foreach (float dx in new[] { -WindowM / 2, WindowM / 2 })
                        foreach (float dz in new[] { -WindowM / 2, WindowM / 2 })
                            patches.Add(((int)((xz.x + dx - map.transform.position.x) / cell) / per, (int)((xz.y + dz - map.transform.position.z) / cell) / per));
                foreach ((int px, int pz) in patches)
                    CountPatch(data, map, px, pz, cell, per, report);

                foreach ((string name, Vector2 xz) in Windows)
                    Picture(map, name, xz, output, report);

                File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString());
                Debug.Log($"{Tag}\n{report}");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} {e}");
                EditorApplication.Exit(1);
            }
        }

        // The patch's cells (Unity's indices, z from the south) against the transforms, per layer, at density 1 and the terrain's.
        static void CountPatch(TerrainData data, Terrain map, int px, int pz, float cell, int per, StringBuilder report)
        {
            for (int l = 0; l < data.detailPrototypes.Length; l++)
            {
                int[,] counts = data.GetDetailLayer(px * per, pz * per, per, per, l);
                int sum = 0;
                foreach (int c in counts)
                    sum += c;
                var line = new StringBuilder($"patch ({px}, {pz}) layer {l}: cells sum {sum}");
                foreach (float density in new[] { 1f, 0.5f, map.detailObjectDensity }.Distinct())
                {
                    DetailInstanceTransform[] t = data.ComputeDetailInstanceTransforms(px, pz, l, density, out Bounds _);
                    var inCell = new int[per, per];
                    int outside = 0;
                    foreach (DetailInstanceTransform d in t)
                    {
                        int cx = Mathf.FloorToInt(d.posX / cell) - px * per, cz = Mathf.FloorToInt(d.posZ / cell) - pz * per;
                        if (cx < 0 || cz < 0 || cx >= per || cz >= per)
                            outside++;
                        else
                            inCell[cz, cx]++;
                    }
                    int cellsDiffer = 0;
                    for (int z = 0; z < per; z++)
                        for (int x = 0; x < per; x++)
                            if (inCell[z, x] != counts[z, x])
                                cellsDiffer++;
                    line.Append($"; density {density}: {t.Length} transforms, cells differing {cellsDiffer}, outside the patch {outside}");
                }
                report.AppendLine(line.ToString());
            }
        }

        // The terrain's drawing against the transforms' drawing of one window, from straight above.
        static void Picture(Terrain map, string name, Vector2 xz, string output, StringBuilder report)
        {
            foreach (Terrain t in Terrain.activeTerrains)
                if (t != map)
                    t.enabled = false;
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                r.enabled = false;
            map.drawHeightmap = false;
            map.treeDistance = 0f;
            map.detailObjectDistance = 250f;

            float ground = map.SampleHeight(new Vector3(xz.x, 0f, xz.y)) + map.transform.position.y;
            var camera = new GameObject("DetailInstanceCheck").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = WindowM / 2;
            camera.aspect = 1f;
            camera.transform.SetPositionAndRotation(new Vector3(xz.x, ground + 20f, xz.y), Quaternion.Euler(90f, 0f, 0f));
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(1f, 0f, 1f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.enabled = false;
            UniversalAdditionalCameraData extra = camera.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = false;
            extra.antialiasing = AntialiasingMode.None;
            extra.renderShadows = false;
            var target = new RenderTexture(Pixels, Pixels, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            camera.targetTexture = target;
            var readback = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            try
            {
                map.drawTreesAndFoliage = true;
                Render(camera, readback); // warm-up
                Color32[] drawn = Render(camera, readback);
                Save(readback, Path.Combine(output, name + "_terrain.png"));
                (int sa, int sb, int sboth, int sdiffer) = Compare(drawn, Render(camera, readback), Path.Combine(output, $"{name}_diff_terrain_again.png"));
                report.AppendLine($"window {name}: the terrain drawn twice: {sa} and {sb} px covered, {sdiffer} differing");
                float density0 = map.detailObjectDensity;
                map.detailObjectDensity = 0.5f;
                Render(camera, readback);
                (int ha, int hb, int hboth, int hdiffer) = Compare(drawn, Render(camera, readback), Path.Combine(output, $"{name}_diff_terrain_density0.5.png"));
                map.detailObjectDensity = density0;
                report.AppendLine($"window {name}: the terrain at detailObjectDensity 0.5 against {density0}: {hb} and {ha} px covered, {hdiffer} differing");

                map.drawTreesAndFoliage = false;
                TerrainData data = map.terrainData;
                float cell = data.size.x / data.detailResolution;
                int per = data.detailResolutionPerPatch;
                int x0 = (int)((xz.x - WindowM / 2 - map.transform.position.x) / cell) / per, x1 = (int)((xz.x + WindowM / 2 - map.transform.position.x) / cell) / per;
                int z0 = (int)((xz.y - WindowM / 2 - map.transform.position.z) / cell) / per, z1 = (int)((xz.y + WindowM / 2 - map.transform.position.z) / cell) / per;
                var results = new List<string>();
                foreach (float density in new[] { map.detailObjectDensity, 1f }.Distinct())
                {
                    foreach ((bool degrees, bool tilt) in new[] { (false, false), (true, false), (false, true) })
                    {
                        int instances = 0;
                        for (int px = x0; px <= x1; px++)
                            for (int pz = z0; pz <= z1; pz++)
                                for (int l = 0; l < data.detailPrototypes.Length; l++)
                                {
                                    DetailPrototype p = data.detailPrototypes[l];
                                    foreach (DetailInstanceTransform d in data.ComputeDetailInstanceTransforms(px, pz, l, density, out Bounds _))
                                    {
                                        instances++;
                                        float yaw = degrees ? d.rotationY : d.rotationY * Mathf.Rad2Deg;
                                        // The prototype's alignToGround, read as the up axis turned that share of the way to the ground's normal.
                                        Quaternion turn = Quaternion.identity;
                                        if (tilt)
                                        {
                                            Vector3 normal = data.GetInterpolatedNormal(d.posX / data.size.x, d.posZ / data.size.z);
                                            turn = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, p.alignToGround));
                                        }
                                        Matrix4x4 at = Matrix4x4.TRS(map.transform.position + new Vector3(d.posX, d.posY, d.posZ), turn * Quaternion.Euler(0f, yaw, 0f),
                                            new Vector3(d.scaleXZ, d.scaleY, d.scaleXZ));
                                        foreach (MeshRenderer r in p.prototype.GetComponentsInChildren<MeshRenderer>())
                                        {
                                            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                                            Matrix4x4 m = at * p.prototype.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                                            Material[] materials = r.sharedMaterials;
                                            for (int s = 0; s < mesh.subMeshCount; s++)
                                                Graphics.RenderMesh(new RenderParams(materials[Mathf.Min(s, materials.Length - 1)]) { camera = camera, shadowCastingMode = ShadowCastingMode.Off },
                                                    mesh, s, m);
                                        }
                                    }
                                }
                        Color32[] mine = Render(camera, readback);
                        string label = $"density{density:0.###}_{(degrees ? "deg" : "rad")}{(tilt ? "_tilt" : "")}";
                        Save(readback, Path.Combine(output, $"{name}_transforms_{label}.png"));
                        (int a, int b, int both, int differ) = Compare(drawn, mine, Path.Combine(output, $"{name}_diff_{label}.png"));
                        results.Add($"{label}: {instances} transforms; covered terrain {a} px, transforms {b} px, both {both}, differing {differ} " +
                                    $"({(a + b - both > 0 ? 100.0 * differ / (a + b - both) : 0):0.000} % of the union)");
                    }
                }
                report.AppendLine($"window {name} at ({xz.x}, {xz.y}), ground {ground:0.000} m, {WindowM} m square, {Pixels} px: " + string.Join("; ", results));
            }
            finally
            {
                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        static Color32[] Render(Camera camera, Texture2D readback)
        {
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            readback.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0, false);
            readback.Apply(false);
            RenderTexture.active = null;
            return readback.GetPixels32();
        }

        static bool Covered(Color32 c) => !(c.r == 255 && c.g == 0 && c.b == 255);

        static (int, int, int, int) Compare(Color32[] a, Color32[] b, string path)
        {
            int na = 0, nb = 0, both = 0, differ = 0;
            var diff = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                bool ca = Covered(a[i]), cb = Covered(b[i]);
                na += ca ? 1 : 0;
                nb += cb ? 1 : 0;
                both += ca && cb ? 1 : 0;
                differ += ca != cb ? 1 : 0;
                diff[i] = ca == cb ? (ca ? new Color32(128, 128, 128, 255) : new Color32(0, 0, 0, 255)) : ca ? new Color32(255, 0, 0, 255) : new Color32(0, 255, 0, 255);
            }
            var t = new Texture2D(Pixels, Pixels, TextureFormat.RGB24, false);
            t.SetPixels32(diff);
            t.Apply(false);
            Save(t, path);
            UnityEngine.Object.DestroyImmediate(t);
            return (na, nb, both, differ);
        }

        static void Save(Texture2D t, string path) => File.WriteAllBytes(path, t.EncodeToPNG());

        static string V(Vector3 v) => $"({v.x}, {v.y}, {v.z})";

        static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }
}
