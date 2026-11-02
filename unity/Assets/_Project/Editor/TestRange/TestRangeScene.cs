using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.Assets;
using Fpv450.Feed;
using Fpv450.Input;
using Fpv450.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450
{
    /// <summary>
    /// Builds <c>TestRange.unity</c> (p6-test-range design 5, 8) from <c>maps/test-range/placements.json</c>, which
    /// <c>tools/map/make_test_range.py</c> writes: every instance row as its prefab at its position and yaw under its
    /// group, each span's conductors checked on their ports (never slid: the file holds the final poses, so the sim
    /// meets each object where it is drawn), the gallery's floor cut by the file's floor cuts, the gallery's rig with
    /// the file's number-key views and the noclip start, and the boot with the PLT-07 legs. The scene is never edited
    /// by hand: change the generator's table, re-run it and <c>-batchmode -quit -executeMethod Fpv450.TestRangeScene.Build</c>
    /// (exit code 1 on any error).
    /// </summary>
    public static class TestRangeScene
    {
        public const string ScenePath = "Assets/_Project/Scenes/TestRange.unity";
        const string Folder = "Assets/_Project/Scenes/TestRange";
        const string FloorMeshPath = Folder + "/Floor.asset";
        const string UnderMeshPath = Folder + "/FloorUnder.asset";
        const string Tag = "[TestRangeScene]";

        // The floor: the gallery's straw cover over 2.4 km about the map's centre, so the ground past the 512 m map
        // reaches the haze from anywhere on it (gallery design 2).
        const float FloorHalfM = 1200f;
        // One quad under it, deeper than every cut (Z04's shaft 2.35 m, E01 1.85 m, S04 0.43 m), in the floor's
        // material: where a patch or a shaft is culled at its LOD distance its cut shows ground, never the sky.
        const float UnderDepthM = 3f;
        // The file and the scene agree within these (the spec's pose parity; footprints and bands as the file rounds them).
        const float ToleranceM = 0.001f;
        const float ToleranceDeg = 0.01f;

        /// <summary>The placement file, from the Unity project's folder.</summary>
        public static string PlacementsFile =>
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "..", "maps", "test-range", "placements.json"));

        public static void Build()
        {
            try
            {
                BuildScene();
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        public static Dictionary<string, object> LoadPlacements()
        {
            string file = PlacementsFile;
            Dictionary<string, object> data = Json.Obj(Json.Parse(File.ReadAllText(file)), file);
            if (Json.Str(data, "format", file) != "fpv450-placements" || Json.Num(data, "format_version", file) != 1f || Json.Str(data, "frame", file) != "unity-lh-yup")
                throw new FormatException($"{file}: not a version 1 placement file in Unity's frame");
            return data;
        }

        static void BuildScene()
        {
            ShaderUtil.allowAsyncCompilation = false;
            Dictionary<string, object> data = LoadPlacements();
            string file = PlacementsFile;
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder)!.Replace('\\', '/'), Path.GetFileName(Folder));

            // --- the instances
            var placed = new GameObject("Placed").transform;
            var groups = new Dictionary<string, Transform>();
            var byId = new Dictionary<int, GameObject>();
            var rows = new Dictionary<int, Dictionary<string, object>>();
            foreach (object item in Json.Arr(data, "instances", file))
            {
                Dictionary<string, object> row = Json.Obj(item, $"{file} instance");
                int id = (int)Json.Num(row, "id", file);
                string what = $"{file} instance {id}";
                string group = Json.Str(row, "group", what), asset = Json.Str(row, "asset", what), variant = Json.Str(row, "variant", what);
                if (!groups.TryGetValue(group, out Transform parent))
                {
                    parent = new GameObject(group).transform;
                    parent.SetParent(placed, false);
                    groups.Add(group, parent);
                }
                GameObject instance = GalleryScene.Instantiate($"{Json.Str(row, "class", what)}/{variant}", parent);
                instance.name = InstanceName(id, variant);
                instance.transform.SetLocalPositionAndRotation(Json.Vec3(row, "position_m", what), Quaternion.Euler(0f, Json.Num(row, "yaw_deg", what), 0f));
                AssetPhysicsMeta meta = GalleryScene.Meta(instance);
                if (meta.id != asset || meta.variant != variant)
                    throw new InvalidOperationException($"{what}: the prefab carries {meta.id} {meta.variant}, not {asset} {variant}");
                byId.Add(id, instance);
                rows.Add(id, row);
            }

            // --- the spans, on their ports as placed
            int spans = 0;
            foreach (KeyValuePair<int, Dictionary<string, object>> entry in rows.Where(e => e.Value.ContainsKey("hang")))
            {
                Dictionary<string, object> hang = Json.Obj(entry.Value, "hang", $"{file} instance {entry.Key}");
                int from = (int)Json.Num(hang, "from", file), to = (int)Json.Num(hang, "to", file);
                GalleryScene.CheckEnds(Tag, byId[entry.Key].name, byId[entry.Key], byId[from], byId[from].name, Strings(hang, "from_ports", file),
                                       byId[to], byId[to].name, Strings(hang, "to_ports", file));
                spans++;
            }

            // --- the ground: the file's patches and bands checked against the placed metadata, the floor cut by its cuts
            Dictionary<string, object> ground = Json.Obj(data, "ground", file);
            var cuts = Json.Arr(ground, "floor_cuts_m", file).Select(c => Rect4(c, $"{file} floor cut")).ToList();
            foreach (object item in Json.Arr(ground, "patches", file))
            {
                Dictionary<string, object> patch = Json.Obj(item, $"{file} patch");
                int id = (int)Json.Num(patch, "instance", file);
                GameObject instance = byId[id];
                Rect listed = Rect4(Json.Get(patch, "footprint_m", file), $"{file} patch {id}");
                Rect placedRect = GalleryScene.Footprint(instance, instance.name, Json.Num(rows[id], "yaw_deg", file));
                if (!Near(listed, placedRect) || !cuts.Any(c => Near(c, listed)))
                    throw new InvalidOperationException($"{instance.name}: the file's footprint {listed} is not its placed bounds {placedRect} or not a floor cut");
            }
            int holes = 0;
            foreach (object item in Json.Arr(ground, "holes", file))
            {
                Dictionary<string, object> hole = Json.Obj(item, $"{file} hole");
                GameObject instance = byId[(int)Json.Num(hole, "instance", file)];
                string record = Json.Str(hole, "record", file);
                AssetPhysicsMeta.Hole source = GalleryScene.Meta(instance).holes.FirstOrDefault(h => h.id == record)
                    ?? throw new InvalidOperationException($"{instance.name} has no hole {record}");
                List<object> line = Json.Arr(hole, "centre_line_m", file);
                if (line.Count != source.centreLine.Length)
                    throw new InvalidOperationException($"{instance.name} {record}: {line.Count} centre-line points for {source.centreLine.Length}");
                for (int i = 0; i < line.Count; i++)
                {
                    float off = Vector3.Distance(Json.Vec3(line[i], $"{file} hole {record}"), instance.transform.TransformPoint(source.centreLine[i]));
                    if (off > ToleranceM)
                        throw new InvalidOperationException($"{instance.name} {record}: centre-line point {i} lies {1000f * off:0.0} mm off the placed record");
                }
                holes++;
            }
            GameObject floor = GalleryScene.Floor(cuts, Vector2.zero, FloorHalfM, FloorMeshPath);
            Under(floor.transform);

            // --- the rig
            GameObject go = GalleryScene.Rig(out LookSetup look, out ViewpointKeys keys);
            Dictionary<string, object> review = Json.Obj(data, "review", file);
            keys.views = Json.Arr(review, "views", file).Select(v => View(v, file)).ToArray();
            if (keys.views.Length is 0 or > 9)
                throw new InvalidOperationException($"{file}: {keys.views.Length} views for the keys 1-9");
            ViewpointKeys.View start = View(Json.Get(data, "noclip_start", file), file);
            if (!keys.views[0].Equals(start))
                throw new InvalidOperationException($"{file}: view 1 must be the noclip start, so key 1 returns there");
            go.transform.SetPositionAndRotation(start.position, Quaternion.Euler(start.pitchDeg, start.yawDeg, 0f));
            var boot = go.AddComponent<TestRangeBoot>();
            boot.placed = placed;
            boot.keys = keys;
            boot.noclip = go.GetComponent<NoclipCamera>();
            boot.look = look;
            boot.legs = Json.Arr(review, "bench_legs", file).Select(l => Leg(l, file)).ToArray();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"could not save {ScenePath}");
            AssetDatabase.SaveAssets();
            foreach (IGrouping<string, Dictionary<string, object>> variant in rows.Values.GroupBy(r => $"{r["asset"]} {r["variant"]}").OrderBy(g => g.Key, StringComparer.Ordinal))
                Debug.Log($"{Tag} {variant.Key} x {variant.Count()}");
            Debug.Log($"{Tag} wrote {ScenePath}: {byId.Count} prefabs in {groups.Count} groups ({string.Join(", ", groups.Keys)}), {spans} spans on their ports, " +
                      $"floor {2f * FloorHalfM:0} m with {cuts.Count} cuts, {holes} hole bands checked, {keys.views.Length} views, {boot.legs.Length} bench legs");
        }

        /// <summary>An instance's scene name: its row id (four digits) and its variant.</summary>
        public static string InstanceName(int id, string variant) => $"{id:0000} {variant}";

        static string[] Strings(Dictionary<string, object> obj, string key, string what) =>
            Json.Arr(obj, key, what).Select(s => s as string ?? throw new FormatException($"{what}: \"{key}\" holds a non-string")).ToArray();

        static Rect Rect4(object value, string what)
        {
            List<object> c = Json.Arr(value, what);
            if (c.Count != 4 || c.Any(x => x is not double))
                throw new FormatException($"{what}: expected [xmin, zmin, xmax, zmax]");
            return Rect.MinMaxRect((float)(double)c[0], (float)(double)c[1], (float)(double)c[2], (float)(double)c[3]);
        }

        static bool Near(Rect a, Rect b) =>
            Mathf.Abs(a.xMin - b.xMin) <= ToleranceM && Mathf.Abs(a.yMin - b.yMin) <= ToleranceM &&
            Mathf.Abs(a.xMax - b.xMax) <= ToleranceM && Mathf.Abs(a.yMax - b.yMax) <= ToleranceM;

        static ViewpointKeys.View View(object value, string file)
        {
            Dictionary<string, object> v = Json.Obj(value, $"{file} view");
            string name = Json.Str(v, "name", file);
            return new ViewpointKeys.View
            {
                name = name,
                position = Json.Vec3(v, "position_m", $"{file} view {name}"),
                yawDeg = Json.Num(v, "yaw_deg", name),
                pitchDeg = Json.Num(v, "pitch_deg", name),
            };
        }

        static TestRangeBoot.Leg Leg(object value, string file)
        {
            Dictionary<string, object> l = Json.Obj(value, $"{file} bench leg");
            string name = Json.Str(l, "name", file);
            return new TestRangeBoot.Leg
            {
                name = name,
                from = Json.Vec3(l, "from_m", name),
                to = Json.Vec3(l, "to_m", name),
                pitchDeg = Json.Num(l, "pitch_deg", name),
                seconds = Json.Num(l, "seconds", name),
            };
        }

        /// <summary>The quad under the floor (see <see cref="UnderDepthM"/>), facing up, in the floor's material, casting nothing.</summary>
        static void Under(Transform floor)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(UnderMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "FloorUnder" };
                AssetDatabase.CreateAsset(mesh, UnderMeshPath);
            }
            float h = FloorHalfM;
            mesh.Clear();
            mesh.SetVertices(new List<Vector3> { new(-h, -UnderDepthM, -h), new(-h, -UnderDepthM, h), new(h, -UnderDepthM, h), new(h, -UnderDepthM, -h) });
            mesh.SetNormals(Enumerable.Repeat(Vector3.up, 4).ToList());
            mesh.SetUVs(0, new List<Vector2> { new(-h, -h), new(-h, h), new(h, h), new(h, -h) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); // clockwise from above: facing up
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var go = new GameObject("Under");
            go.transform.SetParent(floor, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GalleryScene.FloorMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
