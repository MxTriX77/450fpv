using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fpv450.Tests.EditMode
{
    /// <summary>
    /// The farmland (w1-farmland-world, farmland-world spec, "A fresh worktree gets the world"): the friend's scene opens
    /// with every reference found, its pushed text and the local store's linked parts together. Walks the scene's
    /// objects, the terrains' tree and detail prototypes and layers, and the sky. Needs <c>pwsh tools/vendor/link.ps1</c>.
    /// </summary>
    public sealed class FarmLandSceneTests
    {
        const string ScenePath = "Assets/Farm_land/Scenes/FarmLand.unity";

        [Test]
        public void Scene_OpensWithEveryReferenceFound()
        {
            var missing = new List<string>();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                CheckMaterial(RenderSettings.skybox, "RenderSettings.skybox", missing);
                foreach (GameObject root in scene.GetRootGameObjects())
                    CheckObject(root, root.name, missing);
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                        CheckTerrain(terrain, missing);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            Assert.That(missing, Is.Empty, "missing in the farmland; in a fresh checkout run pwsh tools/vendor/link.ps1 first");
        }

        static void CheckTerrain(Terrain terrain, List<string> missing)
        {
            string where = terrain.name;
            CheckMaterial(terrain.materialTemplate, $"{where}: material", missing);
            TerrainData data = terrain.terrainData;
            if (data == null)
            {
                missing.Add($"{where}: terrain data");
                return;
            }
            foreach (TreePrototype tree in data.treePrototypes)
            {
                if (tree.prefab == null) missing.Add($"{where}: tree prototype");
                else CheckObject(tree.prefab, $"{where}: tree {tree.prefab.name}", missing);
            }
            foreach (DetailPrototype detail in data.detailPrototypes)
            {
                if (!detail.usePrototypeMesh) { if (detail.prototypeTexture == null) missing.Add($"{where}: detail texture"); }
                else if (detail.prototype == null) missing.Add($"{where}: detail prototype");
                else CheckObject(detail.prototype, $"{where}: detail {detail.prototype.name}", missing);
            }
            foreach (TerrainLayer layer in data.terrainLayers)
            {
                if (layer == null) missing.Add($"{where}: terrain layer");
                else CheckReferences(layer, $"{where}: layer {layer.name}", missing);
            }
        }

        // Every component of the object and its children: no missing script or prefab, no reference to a missing
        // asset, every renderer's mesh and materials present, and no material on the error shader.
        static void CheckObject(GameObject root, string where, List<string> missing)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                GameObject go = transform.gameObject;
                string path = go == root ? where : $"{where}/{go.name}";
                if (PrefabUtility.IsPrefabAssetMissing(go)) missing.Add($"{path}: prefab");
                foreach (Component component in go.GetComponents<Component>())
                {
                    if (component == null) { missing.Add($"{path}: script"); continue; }
                    CheckReferences(component, $"{path}: {component.GetType().Name}", missing);
                }
                if (go.TryGetComponent(out MeshFilter filter) && filter.sharedMesh == null) missing.Add($"{path}: mesh");
                if (go.TryGetComponent(out Renderer renderer))
                    foreach (Material material in renderer.sharedMaterials)
                        CheckMaterial(material, $"{path}: material", missing);
            }
        }

        static void CheckReferences(Object target, string where, List<string> missing)
        {
            SerializedProperty property = new SerializedObject(target).GetIterator();
            while (property.Next(true))
            {
                // A reference to an asset that isn't there keeps its id but loads as null.
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null &&
                    property.objectReferenceInstanceIDValue != 0)
                    missing.Add($"{where}.{property.propertyPath}");
            }
        }

        static void CheckMaterial(Material material, string where, List<string> missing)
        {
            if (material == null) { missing.Add(where); return; }
            Shader shader = material.shader;
            if (shader == null || shader.name == "Hidden/InternalErrorShader" || ShaderUtil.ShaderHasError(shader))
                missing.Add($"{where} {material.name}: shader");
            CheckReferences(material, $"{where} {material.name}", missing);
        }
    }
}
