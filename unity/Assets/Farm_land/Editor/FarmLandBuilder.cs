using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Re-runnable build steps for the farmland scene. See docs/roadmap.md.
/// </summary>
public static partial class FarmLandBuilder
{
    const string RenderPipelinePath = "Assets/Settings/PC_RPAsset.asset";
    const string GlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
    const string ScenePath = "Assets/Farm_land/Scenes/FarmLand.unity";
    const string ScenesFolder = "Assets/Farm_land/Scenes";

    const float ShadowDistance = 400f;

    // Far enough to see the outer terrain ring through the haze, about 4 km past the map edge.
    public const float CameraFarClip = 6000f;
    const int InputSystemPackageOnly = 1;

    [MenuItem("Farm Land/Step 1 - Project Foundation")]
    public static void BuildFoundation()
    {
        ConfigureRenderPipeline();
        ConfigureInputHandling();
        BuildScene();
        AssetDatabase.SaveAssets();
    }

    static void ConfigureRenderPipeline()
    {
        var pipeline = LoadRequired<UniversalRenderPipelineAsset>(RenderPipelinePath);
        var globalSettings = LoadRequired<RenderPipelineGlobalSettings>(GlobalSettingsPath);

        EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(globalSettings);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;

        pipeline.shadowDistance = ShadowDistance;
        EditorUtility.SetDirty(pipeline);
    }

    // The Input System backend only starts after an Editor restart when this value changes.
    static void ConfigureInputHandling()
    {
        var playerSettings = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        SerializedProperty inputHandler = playerSettings.FindProperty("activeInputHandler");
        if (inputHandler.intValue == InputSystemPackageOnly)
            return;

        inputHandler.intValue = InputSystemPackageOnly;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("FarmLandBuilder: input handling set to Input System. Restart the Editor to apply it.");
    }

    static void BuildScene()
    {
        Scene scene = OpenOrCreateScene();

        ConfigureSun(FindOrCreate("Sun"));
        ConfigureCamera(FindOrCreate("Review Camera"));

        // Procedural sky from the default new-scene setup, also used for ambient light.
        RenderSettings.ambientMode = AmbientMode.Skybox;
        DynamicGI.UpdateEnvironment();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static Scene OpenOrCreateScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (!AssetDatabase.IsValidFolder(ScenesFolder))
            AssetDatabase.CreateFolder("Assets/Farm_land", "Scenes");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Replace the default objects with our own named ones.
        foreach (GameObject root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);

        return scene;
    }

    static void ConfigureSun(GameObject sunObject)
    {
        sunObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        Light sun = GetOrAdd<Light>(sunObject);
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.95f, 0.86f);
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
    }

    static void ConfigureCamera(GameObject cameraObject)
    {
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetPositionAndRotation(
            new Vector3(500f, 80f, 100f), Quaternion.Euler(10f, 0f, 0f));

        Camera camera = GetOrAdd<Camera>(cameraObject);
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = CameraFarClip;
        camera.fieldOfView = 60f;
        camera.clearFlags = CameraClearFlags.Skybox;

        GetOrAdd<UniversalAdditionalCameraData>(cameraObject);
        GetOrAdd<AudioListener>(cameraObject);
        GetOrAdd<FreeFlyCamera>(cameraObject);
    }

    static GameObject FindOrCreate(string name)
    {
        GameObject existing = GameObject.Find(name);
        return existing != null ? existing : new GameObject(name);
    }

    static T GetOrAdd<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }

    static T LoadRequired<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new System.IO.FileNotFoundException($"FarmLandBuilder: missing {typeof(T).Name}", path);
        return asset;
    }
}
