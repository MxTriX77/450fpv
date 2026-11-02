using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fpv450.Assets;
using Fpv450.Feed;
using Fpv450.Input;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The test range (p6-test-range, test-range spec): every row of <c>maps/test-range/placements.json</c> stands in
    /// the committed scene within 1 mm and 0.01° ("Pose parity", the scene's side), the floor is cut where the file
    /// says, the camera starts at the file's noclip start with its views on the number keys, and F1-F4 select G1's
    /// presets, each applied before its frame ends ("Presets switch"). Needs a prior
    /// <c>-executeMethod Fpv450.TestRangeScene.Build</c>; the file's own scenarios are tools/map/tests.
    /// </summary>
    public sealed class TestRangeTests
    {
        const float ToleranceM = 0.001f, ToleranceDeg = 0.01f;

        [Test]
        public void Scene_PlacesEveryRowOfThePlacementFileWithinAMillimetreAndAHundredthOfADegree()
        {
            Dictionary<string, object> data = TestRangeScene.LoadPlacements();
            Scene scene = EditorSceneManager.OpenScene(TestRangeScene.ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform placed = scene.GetRootGameObjects().Single(r => r.name == "Placed").transform;
                var instances = new Dictionary<string, Transform>();
                foreach (Transform group in placed)
                    foreach (Transform instance in group)
                        instances.Add($"{group.name}/{instance.name}", instance);
                List<object> rows = Json.Arr(data, "instances", "placements");
                Assert.That(instances.Count, Is.EqualTo(rows.Count), "one scene instance per row: rebuild the scene after a re-run");
                float worstM = 0f, worstDeg = 0f;
                foreach (object item in rows)
                {
                    var row = (Dictionary<string, object>)item;
                    int id = (int)Json.Num(row, "id", "row");
                    string variant = Json.Str(row, "variant", "row");
                    string key = $"{Json.Str(row, "group", "row")}/{TestRangeScene.InstanceName(id, variant)}";
                    Assert.That(instances.TryGetValue(key, out Transform t), Is.True, key);
                    string source = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject));
                    Assert.That(source, Is.EqualTo($"{GalleryStations.PrefabsFolder}{Json.Str(row, "class", "row")}/{variant}.prefab"), key);
                    float off = Vector3.Distance(t.position, Json.Vec3(row, "position_m", key));
                    float turn = Quaternion.Angle(t.rotation, Quaternion.Euler(0f, Json.Num(row, "yaw_deg", key), 0f));
                    Assert.That(off, Is.LessThanOrEqualTo(ToleranceM), $"{key}: position");
                    Assert.That(turn, Is.LessThanOrEqualTo(ToleranceDeg), $"{key}: yaw (and no pitch or roll)");
                    worstM = Mathf.Max(worstM, off);
                    worstDeg = Mathf.Max(worstDeg, turn);
                }
                TestContext.Out.WriteLine($"{rows.Count} instances: worst position {1000f * worstM:0.000} mm, worst turn {worstDeg:0.0000}°");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Floor_IsCutWhereTheFileSaysAndNowhereElse()
        {
            Dictionary<string, object> data = TestRangeScene.LoadPlacements();
            List<Rect> cuts = Json.Arr(Json.Obj(data, "ground", "placements"), "floor_cuts_m", "placements")
                .Select(c => ((List<object>)c).Select(x => (float)(double)x).ToArray())
                .Select(c => Rect.MinMaxRect(c[0], c[1], c[2], c[3])).ToList();
            Scene scene = EditorSceneManager.OpenScene(TestRangeScene.ScenePath, OpenSceneMode.Additive);
            try
            {
                Mesh mesh = scene.GetRootGameObjects().Single(r => r.name == "Floor").GetComponent<MeshFilter>().sharedMesh;
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                double area = 0;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 c = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3f;
                    Assert.That(cuts.Any(r => r.Contains(new Vector2(c.x, c.z))), Is.False, $"a floor triangle at ({c.x}, {c.z}) lies in a cut");
                    double ax = (double)v[t[i + 1]].x - v[t[i]].x, az = (double)v[t[i + 1]].z - v[t[i]].z;
                    double bx = (double)v[t[i + 2]].x - v[t[i]].x, bz = (double)v[t[i + 2]].z - v[t[i]].z;
                    area += 0.5 * System.Math.Abs(ax * bz - bx * az);
                }
                double cutArea = cuts.Sum(r => ((double)r.xMax - r.xMin) * ((double)r.yMax - r.yMin));
                TestContext.Out.WriteLine($"{cuts.Count} cuts, {cutArea:0.0} m² cut, floor {area:0.0} m²");
                Assert.That(area + cutArea, Is.EqualTo(2400.0 * 2400.0).Within(1.0), "the cuts are all that is missing (they never overlap)");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Scene_StartsAtTheNoclipStartWithTheViewsOnTheKeysAndTheFourPresets()
        {
            Dictionary<string, object> data = TestRangeScene.LoadPlacements();
            Scene scene = EditorSceneManager.OpenScene(TestRangeScene.ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Camera camera = roots.Select(r => r.GetComponent<Camera>()).Single(c => c != null);
                LookSetup look = roots.Select(r => r.GetComponent<LookSetup>()).Single(l => l != null);
                Assert.That(look.viewCamera, Is.SameAs(camera));
                Assert.That(look.presets.Select(AssetDatabase.GetAssetPath), Is.EqualTo(LookSetup.PresetPaths), "G1's four presets, F1-F4 in order");
                Assert.That(camera.GetComponent<LightPresets>().look, Is.SameAs(look));

                Dictionary<string, object> start = Json.Obj(data, "noclip_start", "placements");
                Assert.That(Vector3.Distance(camera.transform.position, Json.Vec3(start, "position_m", "noclip_start")), Is.LessThan(ToleranceM));
                Quaternion expected = Quaternion.Euler(Json.Num(start, "pitch_deg", "noclip_start"), Json.Num(start, "yaw_deg", "noclip_start"), 0f);
                Assert.That(Quaternion.Angle(camera.transform.rotation, expected), Is.LessThan(ToleranceDeg));

                var keys = camera.GetComponent<ViewpointKeys>();
                List<object> views = Json.Arr(Json.Obj(data, "review", "placements"), "views", "placements");
                Assert.That(keys.noclip, Is.SameAs(camera.GetComponent<NoclipCamera>()));
                Assert.That(keys.Count, Is.EqualTo(views.Count));
                Assert.That(keys.views[0].position, Is.EqualTo(camera.transform.position), "key 1 returns to the noclip start");

                var boot = camera.GetComponent<TestRangeBoot>();
                Assert.That(boot.placed, Is.SameAs(roots.Single(r => r.name == "Placed").transform));
                Assert.That(boot.keys, Is.SameAs(keys));
                Assert.That(boot.look, Is.SameAs(look));
                Assert.That(boot.legs.Select(l => l.name), Is.EqualTo(new[] { "yard", "posadka 2 m", "fields 50 m" }), "the spec's three legs");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// The gallery's keys F1-F4 select the presets 0-3, and selecting one applies it before the call returns, so
        /// <see cref="LightPresets"/>, which selects in the Update of the frame the key is pressed, switches the light
        /// in that frame. A key press itself is not simulated: in edit mode the Input System answers
        /// wasPressedThisFrame from its editor updates, never true for a player script (probed: the key reads pressed,
        /// the frame flag stays false), and the project has no PlayMode tests yet.
        /// </summary>
        [Test]
        public void PresetKeys_SelectTheirPresetAndTheLightSwitchesBeforeTheFrameEnds()
        {
            var keys = (Key[])typeof(LightPresets).GetField("Keys", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null);
            Assert.That(keys, Is.EqualTo(new[] { Key.F1, Key.F2, Key.F3, Key.F4 }), "F1 day, F2 low sun, F3 overcast, F4 night");
            var go = new GameObject("TestRangePresetProbe");
            try
            {
                var look = go.AddComponent<LookSetup>();
                look.skyModel = AssetDatabase.LoadAssetAtPath<TextAsset>(LookSetup.SkyModelPath);
                look.presets = LookSetup.PresetPaths.Select(AssetDatabase.LoadAssetAtPath<TextAsset>).ToArray();
                look.skyShader = AssetDatabase.LoadAssetAtPath<Shader>(LookSetup.SkyShaderPath);
                // Backwards, so every key changes the preset.
                for (int i = keys.Length - 1; i >= 0; i--)
                {
                    look.Select(i);
                    string expected = System.IO.Path.GetFileNameWithoutExtension(LookSetup.PresetPaths[i]);
                    Assert.That(look.Active.name, Is.EqualTo(expected), $"{keys[i]} selects {expected}");
                    Assert.That(look.hour, Is.EqualTo(look.Active.hour), $"{keys[i]}: the preset's hour applied");
                    Assert.That(look.sun.enabled, Is.EqualTo(look.State.Sun.maxColorComponent > 0f), $"{keys[i]}: the sun follows the preset");
                    Assert.That(RenderSettings.ambientProbe, Is.EqualTo(look.State.Ambient), $"{keys[i]}: the ambient follows the preset");
                    Assert.That(RenderSettings.fogColor == look.State.FogColor.gamma, Is.True, $"{keys[i]}: the haze follows the preset");
                    TestContext.Out.WriteLine($"{keys[i]}: {look.Active.name}, hour {look.hour:0.##}, sun {look.State.SunElevationDeg:0.0}° up, sun on {look.sun.enabled}");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
