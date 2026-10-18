using System;
using System.Linq;
using Fpv450.App;
using Fpv450.Input;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Fpv450.EditorSetup
{
    /// <summary>
    /// Re-derives the project's settings (setup-unity-skeleton, design decision 1): the player
    /// settings, one URP asset with a Forward+ renderer per quality level, the quality levels,
    /// and the Boot scene as the only scene in the build. Idempotent: a second run changes nothing.
    /// Run with <c>-executeMethod Fpv450.EditorSetup.ProjectSetup.Apply</c>.
    /// </summary>
    public static class ProjectSetup
    {
        const string SettingsFolder = "Assets/_Project/Settings";
        const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        static readonly string[] QualityLevels = { "Low", "Medium", "High" };

        public static void Apply()
        {
            ApplyPlayerSettings();
            UniversalRenderPipelineAsset[] pipelines = QualityLevels.Select(EnsurePipelineAsset).ToArray();
            ApplyQualityLevels(pipelines);
            ApplyGraphicsSettings(pipelines[pipelines.Length - 1]);

            // URP creates these at the root of Assets when it is first imported. Moving them keeps
            // their GUIDs, so the graphics settings still point at them.
            MoveIntoSettings("UniversalRenderPipelineGlobalSettings");
            MoveIntoSettings("DefaultVolumeProfile");

            EnsureBootScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Applied");
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "450fpv";
            PlayerSettings.productName = "450fpv";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";

            // Active input handling has no public API: 1 means the Input System package only.
            SerializedObject player = ProjectSettingsFile("ProjectSettings.asset");
            player.FindProperty("activeInputHandler").intValue = 1;
            player.ApplyModifiedPropertiesWithoutUndo();
        }

        static UniversalRenderPipelineAsset EnsurePipelineAsset(string level)
        {
            string assetPath = $"{SettingsFolder}/URP-{level}.asset";
            string rendererPath = $"{SettingsFolder}/URP-{level}-Renderer.asset";

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                // The same steps as URP's own "URP Asset (with Universal Renderer)" menu item.
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    $"{UniversalRenderPipelineAsset.packagePath}/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, rendererPath);
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
            }

            if (renderer.renderingMode != RenderingMode.ForwardPlus)
            {
                renderer.renderingMode = RenderingMode.ForwardPlus;
                EditorUtility.SetDirty(renderer);
            }

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, assetPath);
            }

            return asset;
        }

        static void ApplyQualityLevels(UniversalRenderPipelineAsset[] pipelines)
        {
            SerializedObject quality = ProjectSettingsFile("QualitySettings.asset");
            SerializedProperty levels = quality.FindProperty("m_QualitySettings");
            for (int i = levels.arraySize - 1; i >= 0; i--)
            {
                string name = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (Array.IndexOf(QualityLevels, name) < 0)
                    levels.DeleteArrayElementAtIndex(i);
            }

            for (int i = 0; i < QualityLevels.Length; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                if (level.FindPropertyRelative("name").stringValue != QualityLevels[i])
                    throw new InvalidOperationException($"Quality level {i} is not {QualityLevels[i]}");
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = pipelines[i];
                // The catalog importer turns each asset's metadata LOD and cull distances (derived from pixel
                // sizes) into screen-relative heights on the assumption that lodBias is 1; Unity's template Low
                // and Medium levels ship with 0.4 and 0.7, which would switch and cull at a fraction of those
                // distances (p1-pipeline-hardening).
                level.FindPropertyRelative("lodBias").floatValue = 1f;
            }

            int high = QualityLevels.Length - 1;
            quality.FindProperty("m_CurrentQuality").intValue = high;
            SerializedProperty platformDefaults = quality.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < platformDefaults.arraySize; i++)
                platformDefaults.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = high;
            quality.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ApplyGraphicsSettings(RenderPipelineAsset defaultPipeline)
        {
            GraphicsSettings.defaultRenderPipeline = defaultPipeline;

            // URP sets both on its first rendered frame; setting them here keeps that from dirtying the project.
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
        }

        static void MoveIntoSettings(string name)
        {
            string from = $"Assets/{name}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(from) == null)
                return;

            string error = AssetDatabase.MoveAsset(from, $"{SettingsFolder}/{name}.asset");
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException(error);
        }

        static void EnsureBootScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath) != null)
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData),
                typeof(AudioListener), typeof(NoclipCamera), typeof(Boot));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 2f, 0f);

            var sun = new GameObject("Sun", typeof(Light), typeof(UniversalAdditionalLightData));
            sun.GetComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            if (!EditorSceneManager.SaveScene(scene, BootScenePath))
                throw new InvalidOperationException($"Could not save {BootScenePath}");
        }

        static SerializedObject ProjectSettingsFile(string file) =>
            new SerializedObject(AssetDatabase.LoadAllAssetsAtPath($"ProjectSettings/{file}")[0]);
    }
}
