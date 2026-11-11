using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fpv450
{
    /// <summary>
    /// A scene's missing references (w1-farmland-world): its objects, its terrains' tree and detail prototypes and
    /// layers, and its sky. The farmland's references to the vendor store's linked parts are missing in a checkout
    /// without <c>tools/vendor/link.ps1</c>: <c>FarmLandSceneTests</c> checks the scene with this, and
    /// <c>Build.Windows</c> refuses to build a player with a broken farmland (task 2.4).
    /// </summary>
    public static class SceneReferences
    {
        /// <summary>Opens the scene beside the open ones, lists what is missing, one line each, and closes it.</summary>
        public static List<string> Missing(string scenePath)
        {
            var missing = new List<string>();
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
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
            return missing;
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
