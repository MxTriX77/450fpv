using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Fpv450.App
{
    /// <summary>
    /// The farmland's tree wood without the leaves' flutter. The pilot: "why do trees breathe. i mean the leaves, yes, they
    /// should, but not the pillars". The friend's tree wind (Farm_land/Shaders/FarmLandTreeWind.hlsl) bends each tree by its
    /// sway and flutters whatever reaches out from the trunk, so the island tree's thick limbs, metres out, rose and fell
    /// about ±8 cm at about 1 Hz. Whenever the FarmLand scene loads (Explore and the flight alike), every wood material, the
    /// friend's Tree Bark shader but for the island tree's twigs, draws through a runtime copy whose flutter is 0. The leaves
    /// and twigs keep the friend's flutter, and every material keeps its sway.
    /// </summary>
    /// <remarks>
    /// The trees are terrain tree instances, drawn from their prototypes' prefabs, so the copies reach them through prototype
    /// copies on a runtime copy of each tile's terrain data, as the player draws terrain edits (D-095). The friend's
    /// materials and prefabs and the store's terrain data are never written: in the editor a material changed in play mode
    /// is saved to its asset. One copy stands in for each wood material everywhere, so the SRP batcher and the trees'
    /// instancing batch as before; terrain trees take no MaterialPropertyBlock. The tiles' colliders keep the store's data.
    /// </remarks>
    public static class FarmlandTreeWood
    {
        /// <summary>The friend's wood shader (trunks, stumps, the bushes' stems and the island tree's limbs and twigs).</summary>
        public const string BarkShader = "Farm Land/Tree Bark";

        /// <summary>The wind's flutter, m (the friend's 0.08).</summary>
        public const string Flutter = "_TreeFlutter";

        /// <summary>The Tree Bark materials that are no wood: the island tree's twigs flutter with its leaves.</summary>
        public static readonly string[] Twigs = { "IslandTree_Twigs" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == FarmlandExplore.Scene) Calm(scene);
        }

        public static bool IsWood(Material material) =>
            material != null && material.shader != null && material.shader.name == BarkShader && Array.IndexOf(Twigs, material.name) < 0;

        /// <summary>Gives the scene's terrain trees their wood without flutter; returns how many wood materials it copied.</summary>
        public static int Calm(Scene scene)
        {
            var clock = Stopwatch.StartNew();
            var owner = new GameObject("Tree wood without flutter").AddComponent<Owned>();
            SceneManager.MoveGameObjectToScene(owner.gameObject, scene);
            var shelf = new GameObject("Prototypes");
            shelf.SetActive(false);
            shelf.transform.SetParent(owner.transform, false);

            var materials = new Dictionary<Material, Material>();
            var prefabs = new Dictionary<GameObject, GameObject>();
            int tiles = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    TerrainData data = terrain.terrainData;
                    if (data == null) continue;
                    TreePrototype[] prototypes = data.treePrototypes;
                    bool wood = false;
                    foreach (TreePrototype prototype in prototypes)
                    {
                        if (prototype.prefab == null) continue;
                        if (!prefabs.TryGetValue(prototype.prefab, out GameObject copy))
                            prefabs[prototype.prefab] = copy = Copy(prototype.prefab, shelf.transform, materials, owner);
                        wood |= copy != prototype.prefab;
                        prototype.prefab = copy;
                    }
                    if (!wood) continue;
                    TerrainData drawn = Object.Instantiate(data);
                    drawn.name = data.name;
                    drawn.treePrototypes = prototypes;
                    owner.Objects.Add(drawn);
                    terrain.terrainData = drawn;
                    tiles++;
                }
            Debug.Log($"[App] the farmland's tree wood without flutter: {materials.Count} wood materials, {tiles} tiles' trees, " +
                      $"runtime copies in {clock.Elapsed.TotalMilliseconds:0} ms");
            return materials.Count;
        }

        // The prefab itself when none of its renderers draws wood; else a copy on the shelf with the wood's copies.
        static GameObject Copy(GameObject prefab, Transform shelf, Dictionary<Material, Material> materials, Owned owner)
        {
            bool wood = false;
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                wood |= Array.Exists(renderer.sharedMaterials, IsWood);
            if (!wood) return prefab;

            GameObject copy = Object.Instantiate(prefab, shelf, false);
            copy.name = prefab.name;
            foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!IsWood(slots[i])) continue;
                    if (!materials.TryGetValue(slots[i], out Material still))
                    {
                        still = new Material(slots[i]) { name = slots[i].name };
                        still.SetFloat(Flutter, 0f);
                        materials[slots[i]] = still;
                        owner.Objects.Add(still);
                    }
                    slots[i] = still;
                }
                renderer.sharedMaterials = slots;
            }
            return copy;
        }

        /// <summary>Destroys the runtime copies with the scene.</summary>
        sealed class Owned : MonoBehaviour
        {
            public readonly List<Object> Objects = new List<Object>();

            void OnDestroy()
            {
                foreach (Object o in Objects)
                    if (o != null) Destroy(o);
            }
        }
    }
}
