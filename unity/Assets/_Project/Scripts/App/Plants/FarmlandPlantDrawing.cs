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
    /// The farmland's plants drawn in the flight's air (w16-plants-in-the-air 1.3). Whenever the FarmLand scene loads (Explore and
    /// the flight alike), every material of the friend's three plant shaders, his grass, foliage and wood, draws through a runtime
    /// copy on the project's own shader for it (Fpv450/Plant Grass, Plant Leaves, Plant Bark: his look, the sim's air in place of
    /// his wind, PlantBend.hlsl), each copy told what it draws: a clump's culms (green, dry or burnt) and its mesh's top, or a tree's
    /// or bush's sway band, drawn height, twigs and branches. The plants then bend only by the field PlantField writes in a flight;
    /// with none (noclip, the stills) they stand as drawn. Supersedes the wood's calm copies (the pilot: the leaves breathe, "but not
    /// the pillars"): the wood now moves by the whole plant's sway alone.
    /// </summary>
    /// <remarks>
    /// The grass is the terrain's details and the trees and bushes its tree instances, drawn from their prototypes' prefabs, so the
    /// copies reach them through prototype copies on a runtime copy of each tile's terrain data, as the player draws terrain edits
    /// (D-095). The friend's materials, prefabs and shaders and the store's terrain data are never written (D-089): in the editor a
    /// material changed in play mode is saved to its asset. The tiles' colliders keep the store's data. One copy per material and
    /// prototype: the SRP batcher batches by shader, and the trees and details instance per prototype as before.
    /// </remarks>
    public static class FarmlandPlantDrawing
    {
        /// <summary>The friend's plant shaders (Farm_land/Shaders).</summary>
        public const string GrassShader = "Farm Land/Grass", LeavesShader = "Farm Land/Tree Leaves", BarkShader = "Farm Land/Tree Bark";

        /// <summary>The project's, as template materials in Settings/Plants/Resources (so a build carries the shaders).</summary>
        public const string GrassTemplate = "PlantGrass", LeavesTemplate = "PlantLeaves", BarkTemplate = "PlantBark";

        /// <summary>The Tree Bark materials that are no wood: the island tree's twigs ride its branches and twigs with its leaves.</summary>
        public static readonly string[] Twigs = { "IslandTree_Twigs" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // ХАНТЕР-09's terrain draws Village's grass prototypes too (w20-hunter-track 3.2a).
            if (scene.name != FarmlandExplore.Scene && scene.name != PlayMaps.HunterScene) return;
            if (FarmlandPlants.On) Draw(scene);
            else Debug.Log("[Plants] -plants 0: the farmland's plants draw through the friend's own shaders and wind (the A/B's before)");
        }

        public static bool IsPlant(Material m) =>
            m != null && m.shader != null && (m.shader.name == GrassShader || m.shader.name == LeavesShader || m.shader.name == BarkShader);

        /// <summary>A wood material: the friend's Tree Bark but the twigs.</summary>
        public static bool IsWood(Material m) =>
            m != null && m.shader != null && m.shader.name == BarkShader && Array.IndexOf(Twigs, m.name) < 0;

        /// <summary>Impostor and billboard cards: their plant's sway only.</summary>
        public static bool IsCard(Material m) => m.name.EndsWith("Impostor") || m.name.EndsWith("Billboards");

        /// <summary>A grass material's culms: 0 green, 1 dry, 2 burnt (FarmLandBuilder.Grass's materials).</summary>
        public static int Culms(Material m) => m.name.StartsWith("GrassBurnt") ? 2 : m.name.StartsWith("GrassDry") ? 1 : 0;

        /// <summary>Gives the scene's terrain plants the project's shaders; returns the material copies made.</summary>
        public static int Draw(Scene scene)
        {
            var clock = Stopwatch.StartNew();
            Material grass = Resources.Load<Material>(GrassTemplate), leaves = Resources.Load<Material>(LeavesTemplate), bark = Resources.Load<Material>(BarkTemplate);
            if (grass == null || leaves == null || bark == null)
            {
                Debug.LogError("[Plants] no Resources/" + GrassTemplate + ", " + LeavesTemplate + " or " + BarkTemplate + ": the farmland's plants keep the friend's wind");
                return 0;
            }
            PlantField.Unbind();
            var owner = new GameObject("Plants in the flight's air").AddComponent<Owned>();
            SceneManager.MoveGameObjectToScene(owner.gameObject, scene);
            var shelf = new GameObject("Prototypes");
            shelf.SetActive(false);
            shelf.transform.SetParent(owner.transform, false);
            var copies = new Copies(owner, shelf.transform, grass.shader, leaves.shader, bark.shader);

            int tiles = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    TerrainData data = terrain.terrainData;
                    if (data == null) continue;
                    bool changed = false;
                    TreePrototype[] trees = data.treePrototypes;
                    foreach (TreePrototype prototype in trees)
                    {
                        if (prototype.prefab == null) continue;
                        GameObject copy = copies.Tree(prototype.prefab);
                        changed |= copy != prototype.prefab;
                        prototype.prefab = copy;
                    }
                    DetailPrototype[] details = data.detailPrototypes;
                    foreach (DetailPrototype prototype in details)
                    {
                        if (!prototype.usePrototypeMesh || prototype.prototype == null) continue;
                        GameObject copy = copies.Grass(prototype.prototype);
                        changed |= copy != prototype.prototype;
                        prototype.prototype = copy;
                    }
                    if (!changed) continue;
                    TerrainData drawn = Object.Instantiate(data);
                    drawn.name = data.name;
                    drawn.treePrototypes = trees;
                    drawn.detailPrototypes = details;
                    owner.Objects.Add(drawn);
                    terrain.terrainData = drawn;
                    tiles++;
                }
            Debug.Log($"[Plants] the farmland's plants in the flight's air: {copies.Materials} material copies on the project's plant shaders, " +
                      $"{tiles} tiles, runtime copies in {clock.Elapsed.TotalMilliseconds:0} ms");
            return copies.Materials;
        }

        // The prototype copies and their materials' copies.
        sealed class Copies
        {
            readonly Owned _owner;
            readonly Transform _shelf;
            readonly Shader _grass, _leaves, _bark;
            readonly Dictionary<GameObject, GameObject> _prefabs = new Dictionary<GameObject, GameObject>();
            public int Materials;

            public Copies(Owned owner, Transform shelf, Shader grass, Shader leaves, Shader bark)
            {
                _owner = owner;
                _shelf = shelf;
                _grass = grass;
                _leaves = leaves;
                _bark = bark;
            }

            // A tree or bush: its sway band, its drawn height at scale 1, its twigs and branches.
            public GameObject Tree(GameObject prefab)
            {
                if (_prefabs.TryGetValue(prefab, out GameObject done)) return done;
                GameObject copy = Copy(prefab);
                if (copy != prefab)
                {
                    int sway = PlantBands.Sway(prefab.name);
                    if (sway < 0)
                    {
                        Debug.LogWarning("[Plants] " + prefab.name + " is no plant the bands know: it sways as a leafy bush");
                        sway = PlantBands.BushLeafy;
                    }
                    float height = Height(copy);
                    bool tree = PlantBands.IsTree(sway);
                    Swap(copy, m =>
                    {
                        if (m.shader.name == GrassShader) return null;
                        Material c = Make(m, m.shader.name == LeavesShader ? _leaves : _bark);
                        c.SetFloat("_PlantSway", sway);
                        c.SetFloat("_PlantHeight", height);
                        c.SetFloat("_PlantTwig", tree ? PlantBands.TreeTwig : PlantBands.BushTwig);
                        c.SetFloat("_PlantBranch", tree ? 1 : 0);
                        c.SetFloat("_PlantFoliage", IsWood(m) || IsCard(m) ? 0 : 1);
                        return c;
                    });
                }
                return _prefabs[prefab] = copy;
            }

            // A grass clump: its culms and its mesh's top.
            public GameObject Grass(GameObject prefab)
            {
                if (_prefabs.TryGetValue(prefab, out GameObject done)) return done;
                GameObject copy = Copy(prefab);
                if (copy != prefab)
                {
                    MeshFilter filter = copy.GetComponentInChildren<MeshFilter>(true);
                    float top = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.max.y : 0.5f;
                    Swap(copy, m =>
                    {
                        if (m.shader.name != GrassShader) return null;
                        Material c = Make(m, _grass);
                        c.SetFloat("_PlantClass", Culms(m));
                        c.SetFloat("_PlantMeshTop", top);
                        return c;
                    });
                }
                return _prefabs[prefab] = copy;
            }

            // The prefab itself when none of its renderers draws a plant; else a copy on the shelf.
            GameObject Copy(GameObject prefab)
            {
                bool plant = false;
                foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
                    plant |= Array.Exists(r.sharedMaterials, IsPlant);
                if (!plant) return prefab;
                GameObject copy = Object.Instantiate(prefab, _shelf, false);
                copy.name = prefab.name;
                return copy;
            }

            void Swap(GameObject copy, Func<Material, Material> make)
            {
                var made = new Dictionary<Material, Material>();
                foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (!IsPlant(slots[i])) continue;
                        if (!made.TryGetValue(slots[i], out Material c))
                            made[slots[i]] = c = make(slots[i]);
                        if (c != null) slots[i] = c;
                    }
                    r.sharedMaterials = slots;
                }
            }

            Material Make(Material his, Shader ours)
            {
                var c = new Material(his) { name = his.name, shader = ours, enableInstancing = true };
                _owner.Objects.Add(c);
                Materials++;
                return c;
            }

            // The drawn height at the prefab's own scale: its renderers' top over its origin.
            static float Height(GameObject copy)
            {
                float top = 0;
                foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is not MeshRenderer) continue;
                    MeshFilter f = r.GetComponent<MeshFilter>();
                    if (f == null || f.sharedMesh == null) continue;
                    Bounds b = f.sharedMesh.bounds;
                    Matrix4x4 m = copy.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    for (int c = 0; c < 8; c++)
                    {
                        var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                        top = Mathf.Max(top, m.MultiplyPoint3x4(corner).y);
                    }
                }
                return top > 0 ? top : 1;
            }
        }

        /// <summary>Destroys the runtime copies with the scene.</summary>
        sealed class Owned : MonoBehaviour
        {
            public readonly List<Object> Objects = new List<Object>();

            void OnDestroy()
            {
                PlantField.Unbind();
                foreach (Object o in Objects)
                    if (o != null) Destroy(o);
            }
        }
    }
}
