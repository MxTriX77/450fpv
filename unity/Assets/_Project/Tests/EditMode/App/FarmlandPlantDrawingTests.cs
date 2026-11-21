using System.Collections.Generic;
using System.Linq;
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
    /// The farmland's plants draw in the flight's air (w16-plants-in-the-air 1.3): after FarmlandPlantDrawing's hook every grass,
    /// foliage and wood material the terrain draws is a copy on the project's plant shader for it, with the friend's textures,
    /// colours and settings, told what it draws (a clump's culms and mesh top; a tree's or bush's sway band, height, twigs and
    /// branches; wood and cards by the sway alone), nothing draws the friend's wind any more, the grass keeps every instance the
    /// friend painted, and the friend's materials, prefabs and shaders and the store's terrain data are left as they were. Opens
    /// the friend's scene, so it needs <c>pwsh tools/vendor/link.ps1</c>.
    /// </summary>
    public sealed class FarmlandPlantDrawingTests
    {
        const string ScenePath = "Assets/Farm_land/Scenes/FarmLand.unity";

        static readonly Dictionary<string, string> Ours = new Dictionary<string, string>
        {
            [FarmlandPlantDrawing.GrassShader] = "Fpv450/Plant Grass",
            [FarmlandPlantDrawing.LeavesShader] = "Fpv450/Plant Leaves",
            [FarmlandPlantDrawing.BarkShader] = "Fpv450/Plant Bark",
        };

        [Test]
        public void TheFarmlandPlantsDrawInTheFlightsAir()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Terrain[] tiles = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
            TerrainData[] stored = tiles.Select(t => t.terrainData).ToArray();
            try
            {
                // The friend's plant materials as he left them, by name, and the grass he painted.
                var friend = new Dictionary<string, Material>();
                foreach (Material m in Materials(stored)) friend[m.name] = m;
                Assert.That(friend.Values.Count(m => m.shader.name == FarmlandPlantDrawing.GrassShader), Is.GreaterThan(0), "the friend's grass");
                Assert.That(friend.Values.Count(m => m.shader.name == FarmlandPlantDrawing.LeavesShader), Is.GreaterThan(0), "the friend's foliage");
                Assert.That(friend.Values.Count(m => m.shader.name == FarmlandPlantDrawing.BarkShader), Is.GreaterThan(0), "the friend's wood");
                long[] painted = stored.Select(Painted).ToArray();

                int copies = FarmlandPlantDrawing.Draw(scene);

                int wood = 0, foliage = 0, grass = 0;
                foreach (TerrainData data in tiles.Select(t => t.terrainData))
                {
                    foreach (TreePrototype p in data.treePrototypes.Where(p => p.prefab != null))
                        foreach (Material m in p.prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null))
                        {
                            Assert.That(Ours.ContainsKey(m.shader.name), Is.False, p.prefab.name + "'s " + m.name + " draws no friend's wind");
                            if (!friend.TryGetValue(m.name, out Material his) || !Ours.ContainsValue(m.shader.name)) continue;
                            Assert.That(m.shader.name, Is.EqualTo(Ours[his.shader.name]), m.name + "'s shader");
                            Same(m, his);
                            Assert.That(m.GetFloat("_PlantSway"), Is.EqualTo(PlantBands.Sway(p.prefab.name)), p.prefab.name + "'s sway band");
                            Assert.That(m.GetFloat("_PlantHeight"), Is.GreaterThan(1f), p.prefab.name + "'s drawn height");
                            bool still = FarmlandPlantDrawing.IsWood(his) || FarmlandPlantDrawing.IsCard(his);
                            Assert.That(m.GetFloat("_PlantFoliage"), Is.EqualTo(still ? 0f : 1f), m.name + (still ? " moves by the sway alone" : " rides its twigs"));
                            if (still) wood++;
                            else foliage++;
                        }
                    foreach (DetailPrototype p in data.detailPrototypes.Where(p => p.usePrototypeMesh && p.prototype != null))
                        foreach (Material m in p.prototype.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null))
                        {
                            Assert.That(m.shader.name, Is.EqualTo("Fpv450/Plant Grass"), p.prototype.name + "'s grass");
                            Same(m, friend[m.name]);
                            Assert.That(m.GetFloat("_PlantClass"), Is.EqualTo(FarmlandPlantDrawing.Culms(friend[m.name])), p.prototype.name + "'s culms");
                            Assert.That(m.GetFloat("_PlantMeshTop"), Is.InRange(0.2f, 1f), p.prototype.name + "'s mesh top");
                            grass++;
                        }
                }
                Assert.That(wood > 0 && foliage > 0 && grass > 0, $"wood {wood}, foliage {foliage}, grass {grass}");
                for (int i = 0; i < tiles.Length; i++)
                    Assert.That(Painted(tiles[i].terrainData), Is.EqualTo(painted[i]), tiles[i].name + " keeps the grass the friend painted");

                // The friend's assets and the store's terrain data as they were.
                foreach (Material m in friend.Values)
                {
                    Assert.That(Ours.ContainsKey(m.shader.name), m.name + ": the friend's asset keeps his shader");
                    Assert.That(EditorUtility.IsDirty(m), Is.False, m.name + ": the friend's asset is not to be saved");
                }
                for (int i = 0; i < tiles.Length; i++)
                {
                    TerrainCollider collider = tiles[i].GetComponent<TerrainCollider>();
                    if (collider != null)
                        Assert.That(collider.terrainData, Is.SameAs(stored[i]), tiles[i].name + "'s collider keeps the store's data");
                    Assert.That(EditorUtility.IsDirty(stored[i]), Is.False, stored[i].name + " is not to be saved");
                    Assert.That(stored[i].treePrototypes.All(p => p.prefab == null || EditorUtility.IsPersistent(p.prefab)), stored[i].name + " keeps the friend's trees");
                    Assert.That(stored[i].detailPrototypes.All(p => p.prototype == null || EditorUtility.IsPersistent(p.prototype)), stored[i].name + " keeps the friend's grass");
                }
                TestContext.WriteLine($"{copies} material copies on the project's plant shaders: {grass} grass, {foliage} foliage and twig, {wood} wood and card " +
                                      $"materials drawn; {tiles.Count(t => t.terrainData != stored[tiles.ToList().IndexOf(t)])} of {tiles.Length} tiles from a runtime copy");
            }
            finally
            {
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i] != null && tiles[i].terrainData != stored[i])
                    {
                        Object.DestroyImmediate(tiles[i].terrainData);
                        tiles[i].terrainData = stored[i];
                    }
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // The copy keeps the friend's textures, colours and numbers wherever both shaders have them.
        static void Same(Material ours, Material his)
        {
            foreach (string name in his.GetTexturePropertyNames().Where(ours.HasTexture))
                Assert.That(ours.GetTexture(name), Is.SameAs(his.GetTexture(name)), ours.name + "'s " + name);
            foreach (string name in new[] { "_BaseColor", "_Cutoff", "_Smoothness", "_Wrap", "_Translucency", "_VertexColorShare", "_NormalUp", "_Desaturate", "_EdgeFade", "_FadeEnd" })
            {
                if (!ours.HasProperty(name) || !his.HasProperty(name)) continue;
                if (name == "_BaseColor") Assert.That(ours.GetColor(name), Is.EqualTo(his.GetColor(name)), ours.name + "'s " + name);
                else Assert.That(ours.GetFloat(name), Is.EqualTo(his.GetFloat(name)), ours.name + "'s " + name);
            }
        }

        // The friend's plant materials the tiles draw: their trees' and their grass's, every LOD.
        static IEnumerable<Material> Materials(IEnumerable<TerrainData> tiles) =>
            tiles.Where(d => d != null).SelectMany(d =>
                    d.treePrototypes.Where(p => p.prefab != null).Select(p => p.prefab)
                        .Concat(d.detailPrototypes.Where(p => p.usePrototypeMesh && p.prototype != null).Select(p => p.prototype)))
                .SelectMany(p => p.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials)
                .Where(FarmlandPlantDrawing.IsPlant).Distinct();

        // The grass a tile has painted: its detail layers' instance counts summed over its middle 256 × 256 cells.
        static long Painted(TerrainData data)
        {
            if (data == null) return 0;
            long sum = 0;
            int w = Mathf.Min(256, data.detailWidth), h = Mathf.Min(256, data.detailHeight);
            int x = (data.detailWidth - w) / 2, y = (data.detailHeight - h) / 2;
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                foreach (int n in data.GetDetailLayer(x, y, w, h, layer))
                    sum += n;
            return sum;
        }
    }
}
