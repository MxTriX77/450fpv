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
    /// The farmland's tree wood holds still in the flutter (the pilot: the leaves should breathe, "but not the pillars"):
    /// after FarmlandTreeWood's hook every wood material the scene draws, terrain trees and GameObjects alike, has no
    /// flutter; the leaves and twigs keep the friend's 0.08 (his impostors their 0), every material keeps its sway and wind,
    /// and the friend's materials, prefabs and the store's terrain data are left as they were. Opens the friend's scene, so
    /// it needs <c>pwsh tools/vendor/link.ps1</c>.
    /// </summary>
    public sealed class FarmlandTreeWoodTests
    {
        const string ScenePath = "Assets/Farm_land/Scenes/FarmLand.unity";

        /// <summary>The friend's flutter and sway, m (Farm_land/Editor/FarmLandBuilder.Trees.cs).</summary>
        const float FriendFlutter = 0.08f, FriendSway = 0.15f;

        /// <summary>The wood: every Tree Bark material but the island tree's twigs.</summary>
        static readonly string[] Wood =
        {
            "Bush_Bush_qilgY2_Trunk", "Bush_Juniper_Bush_01_Trunk", "Bush_Scots_Pine_a_bark", "IslandTree_Bark", "Pine002_Stump",
            "Pine002_Trunk", "Pine004_Stump", "Pine004_Trunk", "Pine006_Trunk", "PineDead_Stump", "PineDead_Trunk",
        };

        static readonly string[] Wind = { "_TreeSway", "_WindSpeed", "_WindGustSize" };

        [Test]
        public void TheFarmlandWoodStopsFlutteringAndItsLeavesKeepIt()
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Terrain[] tiles = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
            TerrainData[] stored = tiles.Select(t => t.terrainData).ToArray();
            try
            {
                // The rule names the wood: all twelve of the friend's Tree Bark materials but the twigs.
                foreach (string name in Wood.Append("IslandTree_Twigs"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Material>("Assets/Farm_land/URP_Materials/" + name + ".mat");
                    Assert.That(asset, Is.Not.Null, name);
                    Assert.That(asset.shader.name, Is.EqualTo(FarmlandTreeWood.BarkShader), name + "'s shader");
                    Assert.That(FarmlandTreeWood.IsWood(asset), Is.EqualTo(name != "IslandTree_Twigs"), name + " is wood");
                }

                // The friend's materials the trees draw, as he left them, by name.
                var friend = new Dictionary<string, Material>();
                foreach (Material m in TreeMaterials(stored)) friend[m.name] = m;
                string[] drawnWood = friend.Values.Where(FarmlandTreeWood.IsWood).Select(m => m.name).ToArray();
                Assert.That(drawnWood, Is.Not.Empty.And.SubsetOf(Wood), "the wood the trees draw");
                var before = friend.ToDictionary(kv => kv.Key, kv => kv.Value.GetFloat(FarmlandTreeWood.Flutter));

                int copied = FarmlandTreeWood.Calm(scene);

                var calmed = new HashSet<string>();
                int leaves = 0;
                foreach (Material m in TreeMaterials(tiles.Select(t => t.terrainData)).Concat(SceneMaterials(scene)))
                {
                    Assert.That(friend.ContainsKey(m.name), m.name + " is one of the friend's tree materials");
                    Material his = friend[m.name];
                    foreach (string p in Wind)
                        Assert.That(m.GetFloat(p), Is.EqualTo(his.GetFloat(p)), m.name + " keeps its " + p);
                    Assert.That(m.GetFloat("_TreeSway"), Is.EqualTo(FriendSway), m.name + " sways as the friend's");
                    Assert.That(m.GetVector("_WindDirection"), Is.EqualTo(his.GetVector("_WindDirection")), m.name + " keeps its wind's direction");
                    if (Wood.Contains(m.name))
                    {
                        Assert.That(m.GetFloat(FarmlandTreeWood.Flutter), Is.EqualTo(0f), m.name + " is wood: no flutter");
                        calmed.Add(m.name);
                    }
                    else
                    {
                        Assert.That(m, Is.SameAs(his), m.name + " is the friend's own material");
                        bool card = m.name.EndsWith("Impostor") || m.name.EndsWith("Billboards");
                        Assert.That(m.GetFloat(FarmlandTreeWood.Flutter), Is.EqualTo(card ? 0f : FriendFlutter), m.name + " keeps the friend's flutter");
                        if (!card) leaves++;
                    }
                }
                CollectionAssert.AreEquivalent(drawnWood, calmed, "every wood material draws without flutter");
                Material[] detailWood = stored.SelectMany(d => d.detailPrototypes).Where(p => p.usePrototypeMesh && p.prototype != null)
                    .SelectMany(p => p.prototype.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials)
                    .Where(FarmlandTreeWood.IsWood).ToArray();
                Assert.That(detailWood, Is.Empty, "the terrain's detail meshes (the grass) draw no wood");
                Assert.That(copied, Is.EqualTo(drawnWood.Length), "one copy per wood material, so the batches stay as they were");
                Assert.That(leaves, Is.GreaterThan(0), "the leaves and twigs are drawn");

                // The friend's assets and the store's terrain data as they were.
                foreach (Material m in friend.Values)
                {
                    Assert.That(m.GetFloat(FarmlandTreeWood.Flutter), Is.EqualTo(before[m.name]), m.name + ": the friend's asset keeps its flutter");
                    Assert.That(EditorUtility.IsDirty(m), Is.False, m.name + ": the friend's asset is not to be saved");
                }
                int drawn = 0;
                for (int i = 0; i < tiles.Length; i++)
                {
                    if (tiles[i].terrainData != stored[i]) drawn++;
                    TerrainCollider collider = tiles[i].GetComponent<TerrainCollider>();
                    if (collider != null)
                        Assert.That(collider.terrainData, Is.SameAs(stored[i]), tiles[i].name + "'s collider keeps the store's data");
                    Assert.That(EditorUtility.IsDirty(stored[i]), Is.False, stored[i].name + " is not to be saved");
                    Assert.That(stored[i].treePrototypes.All(p => p.prefab == null || EditorUtility.IsPersistent(p.prefab)), stored[i].name + " keeps the friend's prefabs");
                }
                TestContext.WriteLine($"{calmed.Count} wood materials without flutter ({string.Join(", ", calmed.OrderBy(n => n))}; no tree draws " +
                                      $"{string.Join(", ", Wood.Except(calmed))}), {leaves} leaf and twig materials at {FriendFlutter} m, " +
                                      $"{drawn} of {tiles.Length} tiles drawn from a runtime copy");
            }
            finally
            {
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i] != null && tiles[i].terrainData != stored[i])
                    {
                        Object.DestroyImmediate(tiles[i].terrainData);
                        tiles[i].terrainData = stored[i];
                    }
                foreach (Material m in Resources.FindObjectsOfTypeAll<Material>().Where(m => Wood.Contains(m.name) && !EditorUtility.IsPersistent(m)).ToArray())
                    Object.DestroyImmediate(m);
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // The tree materials the tiles draw: their prototypes' renderers', every LOD.
        static IEnumerable<Material> TreeMaterials(IEnumerable<TerrainData> tiles) =>
            tiles.Where(d => d != null).SelectMany(d => d.treePrototypes).Where(p => p.prefab != null)
                .SelectMany(p => p.prefab.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.HasProperty(FarmlandTreeWood.Flutter)).Distinct();

        // The tree materials the scene's own renderers draw (none today; a tree placed as a GameObject would show here).
        static IEnumerable<Material> SceneMaterials(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Renderer>()).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.HasProperty(FarmlandTreeWood.Flutter)).Distinct();
    }
}
