using System.IO;
using System.Linq;
using System.Reflection;
using Fpv450.App;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Fpv450.Tests.App
{
    /// <summary>
    /// ХАНТЕР-09's grass (w20-hunter-track 3.2a): the scene's ground holds Village's own detail prototypes layer for layer, and
    /// the terrain it builds at load holds every instance of the package's grass/map.raw, the counts the sim's stems are made of,
    /// cell for cell (the file's rows from the north, Unity's from the south). Needs the farmland's terrain data from the store.
    /// </summary>
    public sealed class HunterGrassTests
    {
        const string ScenePath = "Assets/_Project/Scenes/HunterTrack.unity";
        const string VillageTerrain = "Assets/Farm_land/Terrain/FarmLand_TerrainData.asset";

        [Test]
        public void TheGroundGrowsThePackagesGrassOnVillagesPrototypes()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            TerrainData built = null;
            try
            {
                MapGround ground = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapGround>(true)).Single();
                TerrainData village = AssetDatabase.LoadAssetAtPath<TerrainData>(VillageTerrain);
                Assert.That(village, Is.Not.Null, VillageTerrain);
                Assert.That(ground.grassFile, Is.EqualTo("maps/hunter-09/grass/map.raw"));
                Assert.That(ground.grassLayers.Select(g => g.prefab), Is.EqualTo(village.detailPrototypes.Select(p => p.prototype)), "Village's prefabs");
                Assert.That((ground.grassPerPatch, ground.grassScatter), Is.EqualTo((village.detailResolutionPerPatch, village.detailScatterMode)));

                typeof(MapGround).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ground, null);
                built = ground.GetComponent<Terrain>().terrainData;
                byte[] counts = File.ReadAllBytes(AppData.File(ground.grassFile));
                int res = built.detailResolution, plane = res * res;
                Assert.That(built.detailPrototypes.Length * plane, Is.EqualTo(counts.Length));
                for (int l = 0; l < built.detailPrototypes.Length; l++)
                {
                    int[,] layer = built.GetDetailLayer(0, 0, res, res, l);
                    for (int r = 0; r < res; r++)
                        for (int c = 0; c < res; c++)
                            if (layer[res - 1 - r, c] != counts[l * plane + r * res + c])
                                Assert.Fail($"layer {l}, row {r}, column {c}: {layer[res - 1 - r, c]} drawn, {counts[l * plane + r * res + c]} in the package");
                }
            }
            finally
            {
                if (built != null)
                {
                    foreach (TerrainLayer t in built.terrainLayers) Object.DestroyImmediate(t);
                    Object.DestroyImmediate(built);
                }
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
