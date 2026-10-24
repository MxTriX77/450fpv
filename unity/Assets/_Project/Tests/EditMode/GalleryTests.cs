using System.Linq;
using System.Reflection;
using Fpv450.Feed;
using Fpv450.Input;
using Fpv450.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The G1 gallery (p1-g1-look-lock-gallery, look-lock-gallery "Saved viewpoints at the letters' framing" and
    /// "Six stations from the look-lock assets"): every number key lands the noclip camera on its viewpoint and
    /// noclip control keeps that pose, and the committed scene holds what <see cref="GalleryStations"/> lists, wired
    /// as task 1.1's look setup asks. Needs a prior <c>-executeMethod Fpv450.GalleryScene.Build</c>.
    /// </summary>
    public sealed class GalleryTests
    {
        static void AssertPose(Transform camera, GalleryStations.Viewpoint viewpoint, string what)
        {
            Assert.That(Vector3.Distance(camera.position, viewpoint.Position), Is.LessThan(1e-4f), $"{what}: position");
            Assert.That(Quaternion.Angle(camera.rotation, viewpoint.Rotation), Is.LessThan(0.01f), $"{what}: heading");
        }

        [Test]
        public void EveryNumberKey_LandsOnItsViewpointAndNoclipKeepsIt()
        {
            GalleryStations.Viewpoint[] viewpoints = GalleryStations.Viewpoints;
            Assert.That(viewpoints.Length, Is.InRange(1, 9), "keys 1-9");
            var go = new GameObject("GalleryTestCamera");
            try
            {
                var noclip = go.AddComponent<NoclipCamera>();
                var keys = go.AddComponent<ViewpointKeys>();
                keys.noclip = noclip;
                MethodInfo update = typeof(NoclipCamera).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(update, Is.Not.Null);
                // Backwards through the table, so every jump leaves a different pose.
                for (Key key = Key.Digit9; key >= Key.Digit1; key--)
                {
                    int index = ViewpointKeys.IndexOf(key);
                    int expected = key - Key.Digit1 < viewpoints.Length ? key - Key.Digit1 : -1;
                    Assert.That(index, Is.EqualTo(expected), $"{key}");
                    if (index < 0)
                        continue;
                    GalleryStations.Viewpoint viewpoint = viewpoints[index];
                    keys.Jump(index);
                    AssertPose(go.transform, viewpoint, $"{key} ({viewpoint.Name})");
                    update.Invoke(noclip, null); // a noclip frame without input
                    AssertPose(go.transform, viewpoint, $"{key} ({viewpoint.Name}) after a noclip frame");
                    TestContext.Out.WriteLine($"{key}: {viewpoint.Name} ({viewpoint.Letters}) at {viewpoint.Position}, yaw {viewpoint.YawDeg}°, pitch {viewpoint.PitchDeg}° down");
                }
                Assert.That(ViewpointKeys.IndexOf(Key.Digit0), Is.EqualTo(-1));
                Assert.That(ViewpointKeys.IndexOf(Key.F1), Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Scene_HoldsTheTablesStationsWiredForTheLookSetup()
        {
            Scene scene = EditorSceneManager.OpenScene(GalleryScene.ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Camera camera = roots.Select(r => r.GetComponent<Camera>()).Single(c => c != null);
                LookSetup look = roots.Select(r => r.GetComponent<LookSetup>()).Single(l => l != null);
                Assert.That(roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)), Is.Empty, "no light: LookSetup makes its own sun");
                Assert.That(look.viewCamera, Is.SameAs(camera));
                Assert.That(look.skyModel, Is.Not.Null);
                Assert.That(look.skyShader, Is.Not.Null);
                Assert.That(look.presets.Length, Is.EqualTo(4));
                Assert.That(look.presets, Has.None.Null);

                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                Assert.That(camera.farClipPlane, Is.GreaterThanOrEqualTo(2000f));
                UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
                Assert.That(data.renderPostProcessing, Is.False);
                Assert.That(data.antialiasing, Is.EqualTo(AntialiasingMode.None));
                Assert.That(camera.GetComponent<ViewpointKeys>().noclip, Is.SameAs(camera.GetComponent<NoclipCamera>()));
                Assert.That(camera.GetComponent<LightPresets>().look, Is.SameAs(look));
                AssertPose(camera.transform, GalleryStations.Viewpoints[0], "the camera starts at the S04 viewpoint");

                GalleryBoot boot = camera.GetComponent<GalleryBoot>();
                Assert.That(boot.keys, Is.SameAs(camera.GetComponent<ViewpointKeys>()));
                Assert.That(boot.look, Is.SameAs(look));
                Transform stations = boot.stations;
                Assert.That(stations, Is.Not.Null);
                Assert.That(stations.childCount, Is.EqualTo(GalleryStations.Stations.Length));
                foreach (GalleryStations.Station station in GalleryStations.Stations)
                {
                    Transform marker = stations.Find(station.MarkerName);
                    Assert.That(marker, Is.Not.Null, station.MarkerName);
                    Assert.That(Vector3.Distance(marker.position, station.Origin), Is.LessThan(1e-4f), station.MarkerName);
                    string[] expected = station.Placements.Select(p => p.Prefab).Concat(station.Hangings.Select(h => h.Span)).ToArray();
                    string[] placed = marker.Cast<Transform>()
                        .Select(child => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)))
                        .ToArray();
                    Assert.That(placed, Is.EqualTo(expected.Select(p => $"{GalleryStations.PrefabsFolder}{p}.prefab").ToArray()), $"{station.MarkerName}: rebuild the scene after a table change");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
