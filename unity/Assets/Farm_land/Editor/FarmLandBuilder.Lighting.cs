using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Step 9: sun, sky, fog and post-processing, as one of a few authored moods for a neglected, post-apocalyptic
/// look. Fog takes its colour from the sky just above the horizon, so the far terrain melts into the sky.
/// </summary>
public static partial class FarmLandBuilder
{
    const string LightingFolder = "Assets/Farm_land/Lighting";
    const string SkyMaterialPath = LightingFolder + "/FarmLand_Sky.mat";
    const string PostProcessProfilePath = LightingFolder + "/FarmLand_PostProcess.asset";
    const string SunObjectName = "Sun";
    const string VolumeObjectName = "Global Volume";

    public enum LightingMood
    {
        NaturalDay,
        GreyHaze,
        DustyEvening,
        BleachedNoon,
    }

    /// <summary>The mood Step 9 applies, chosen by the owner. The others stay available for comparison captures.</summary>
    public const LightingMood ChosenMood = LightingMood.NaturalDay;

    sealed class MoodSettings
    {
        // Sun: angle above the horizon, compass direction it shines towards, colour and intensity.
        public float SunPitch;
        public float SunYaw;
        public Color SunColor;
        public float SunIntensity;

        // Procedural sky.
        public Color SkyTint;
        public Color SkyGround;
        public float Atmosphere;
        public float SkyExposure;
        public float SunSize;

        // Exponential squared fog, coloured from the sky. A density of 0.0018 leaves about 70% clear at
        // 330 m and 25% at 650 m.
        public float FogDensity;
        public Color FogTint;
        public float AmbientIntensity;

        // Post-processing.
        public float PostExposure;
        public float Contrast;
        public float Saturation;
        public Color ColorFilter;
        public float Temperature;
        public float Tint;
        public Vector4 Shadows;
        public Vector4 Highlights;
        public float BloomIntensity;
        public float Vignette;
    }

    static MoodSettings Mood(LightingMood mood)
    {
        switch (mood)
        {
            // A clear summer midday as a camera would see it: a high, bright sun with short, hard shadows, a blue
            // sky, thin aerial haze that only shows past a kilometre or two, and neutral grading.
            case LightingMood.NaturalDay:
                return new MoodSettings
                {
                    SunPitch = 60f, SunYaw = -30f, SunColor = new Color(1f, 0.98f, 0.94f), SunIntensity = 2.35f,
                    SkyTint = new Color(0.5f, 0.5f, 0.5f), SkyGround = new Color(0.4f, 0.4f, 0.4f),
                    Atmosphere = 0.95f, SkyExposure = 1.3f, SunSize = 0.035f,
                    FogDensity = 0.0006f, FogTint = Color.white, AmbientIntensity = 1f,
                    PostExposure = 0f, Contrast = 10f, Saturation = -4f, ColorFilter = Color.white,
                    Temperature = 0f, Tint = 0f,
                    Shadows = new Vector4(1f, 1f, 1f, 0f), Highlights = new Vector4(1f, 1f, 1f, 0f),
                    BloomIntensity = 0.12f, Vignette = 0.1f,
                };
            case LightingMood.GreyHaze:
                return new MoodSettings
                {
                    SunPitch = 32f, SunYaw = -35f, SunColor = new Color(0.93f, 0.92f, 0.88f), SunIntensity = 0.85f,
                    SkyTint = new Color(0.52f, 0.53f, 0.55f), SkyGround = new Color(0.42f, 0.41f, 0.39f),
                    Atmosphere = 0.85f, SkyExposure = 1.1f, SunSize = 0.02f,
                    FogDensity = 0.002f, FogTint = new Color(0.96f, 0.98f, 1f), AmbientIntensity = 1.2f,
                    PostExposure = 0.2f, Contrast = 8f, Saturation = -45f, ColorFilter = new Color(0.97f, 0.98f, 1f),
                    Temperature = -6f, Tint = 0f,
                    Shadows = new Vector4(0.96f, 0.98f, 1.04f, 0f), Highlights = new Vector4(1f, 1f, 1f, 0f),
                    BloomIntensity = 0.25f, Vignette = 0.22f,
                };
            case LightingMood.DustyEvening:
                return new MoodSettings
                {
                    SunPitch = 13f, SunYaw = -62f, SunColor = new Color(1f, 0.76f, 0.5f), SunIntensity = 2.1f,
                    SkyTint = new Color(0.62f, 0.52f, 0.42f), SkyGround = new Color(0.45f, 0.38f, 0.31f),
                    Atmosphere = 1.8f, SkyExposure = 1.3f, SunSize = 0.045f,
                    FogDensity = 0.0019f, FogTint = new Color(1f, 0.97f, 0.93f), AmbientIntensity = 1.35f,
                    PostExposure = 0.6f, Contrast = 14f, Saturation = -22f, ColorFilter = new Color(1f, 0.97f, 0.93f),
                    Temperature = 12f, Tint = 2f,
                    Shadows = new Vector4(0.93f, 0.96f, 1.06f, 0f), Highlights = new Vector4(1.04f, 1f, 0.94f, 0f),
                    BloomIntensity = 0.55f, Vignette = 0.3f,
                };
            default:
                return new MoodSettings
                {
                    SunPitch = 62f, SunYaw = -20f, SunColor = new Color(1f, 0.97f, 0.86f), SunIntensity = 1.65f,
                    SkyTint = new Color(0.6f, 0.62f, 0.56f), SkyGround = new Color(0.5f, 0.49f, 0.43f),
                    Atmosphere = 1.4f, SkyExposure = 1.25f, SunSize = 0.03f,
                    FogDensity = 0.0017f, FogTint = new Color(1f, 1f, 0.95f), AmbientIntensity = 1.1f,
                    PostExposure = 0.05f, Contrast = 18f, Saturation = -42f, ColorFilter = new Color(1f, 1f, 0.88f),
                    Temperature = 8f, Tint = -8f,
                    Shadows = new Vector4(1f, 1f, 0.97f, 0.03f), Highlights = new Vector4(1f, 1f, 0.94f, 0f),
                    BloomIntensity = 0.4f, Vignette = 0.2f,
                };
        }
    }

    [MenuItem("Farm Land/Step 9 - Lighting")]
    public static void BuildLighting()
    {
        RequireEditMode();
        Scene scene = OpenFarmLandScene();
        ApplyLightingMood(ChosenMood);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Applies a mood to the open scene: sun, sky, fog, ambient light and the global post-processing volume.</summary>
    public static void ApplyLightingMood(LightingMood mood)
    {
        MoodSettings settings = Mood(mood);
        EnsureFolder(LightingFolder);
        ConfigureColorGrading();

        Light sun = GameObject.Find(SunObjectName).GetComponent<Light>();
        sun.transform.rotation = Quaternion.Euler(settings.SunPitch, settings.SunYaw, 0f);
        sun.color = settings.SunColor;
        sun.intensity = settings.SunIntensity;
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;

        Material sky = LoadOrCreateMaterial(SkyMaterialPath, Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunDisk", 2f);
        sky.SetFloat("_SunSize", settings.SunSize);
        sky.SetFloat("_SunSizeConvergence", 5f);
        sky.SetFloat("_AtmosphereThickness", settings.Atmosphere);
        sky.SetColor("_SkyTint", settings.SkyTint);
        sky.SetColor("_GroundColor", settings.SkyGround);
        sky.SetFloat("_Exposure", settings.SkyExposure);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = settings.AmbientIntensity;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = settings.FogDensity;
        RenderSettings.fogColor = SkyHorizonColor(true) * settings.FogTint;
        MatchSkyGroundToFog(sky);
        DynamicGI.UpdateEnvironment();

        BuildPostProcessProfile(settings);
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            GetOrAdd<UniversalAdditionalCameraData>(camera.gameObject).renderPostProcessing = true;
    }

    // Grading in HDR keeps highlights such as the low sun from clipping before the tonemapper.
    static void ConfigureColorGrading()
    {
        var pipeline = LoadRequired<UniversalRenderPipelineAsset>(RenderPipelinePath);
        if (pipeline.colorGradingMode == ColorGradingMode.HighDynamicRange)
            return;
        pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
        EditorUtility.SetDirty(pipeline);
    }

    static void BuildPostProcessProfile(MoodSettings settings)
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessProfilePath);
        }

        // Rebuilt from scratch each time, so nothing is left over from an earlier mood.
        foreach (VolumeComponent old in profile.components)
            Object.DestroyImmediate(old, true);
        profile.components.Clear();

        var tonemapping = AddVolumeComponent<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        var colour = AddVolumeComponent<ColorAdjustments>(profile);
        colour.postExposure.Override(settings.PostExposure);
        colour.contrast.Override(settings.Contrast);
        colour.saturation.Override(settings.Saturation);
        colour.colorFilter.Override(settings.ColorFilter);

        var balance = AddVolumeComponent<WhiteBalance>(profile);
        balance.temperature.Override(settings.Temperature);
        balance.tint.Override(settings.Tint);

        var split = AddVolumeComponent<ShadowsMidtonesHighlights>(profile);
        split.shadows.Override(settings.Shadows);
        split.highlights.Override(settings.Highlights);

        var bloom = AddVolumeComponent<Bloom>(profile);
        bloom.threshold.Override(1.1f);
        bloom.intensity.Override(settings.BloomIntensity);
        bloom.scatter.Override(0.6f);

        var vignette = AddVolumeComponent<Vignette>(profile);
        vignette.intensity.Override(settings.Vignette);
        vignette.smoothness.Override(0.45f);

        AddCameraMotionBlur(profile);
        EditorUtility.SetDirty(profile);

        GameObject volumeObject = FindOrCreate(VolumeObjectName);
        Volume volume = GetOrAdd<Volume>(volumeObject);
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.sharedProfile = profile;
    }

    static T AddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    // Past the map's surroundings the camera sees the sky's ground half, so it's tinted until the part seen
    // from high views, 10 to 25 degrees below the horizon, matches the fog. A few rounds converge, since the
    // sky's response isn't linear.
    static void MatchSkyGroundToFog(Material sky)
    {
        Color fog = RenderSettings.fogColor;
        for (int round = 0; round < 6; round++)
        {
            Color below = SkyHorizonColor(false);
            Color ground = sky.GetColor("_GroundColor");
            for (int channel = 0; channel < 3; channel++)
                ground[channel] = Mathf.Clamp01(ground[channel] * fog[channel] / Mathf.Max(below[channel], 0.01f));
            sky.SetColor("_GroundColor", ground);
        }
        EditorUtility.SetDirty(sky);
    }

    /// <summary>
    /// Average sky colour rendered without terrain or post-processing, either just above the horizon or
    /// 10 to 25 degrees below it.
    /// </summary>
    static Color SkyHorizonColor(bool above)
    {
        const int width = 256;
        const int height = 256;
        var cameraObject = new GameObject("Sky Sample Camera") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        try
        {
            cameraObject.transform.SetPositionAndRotation(new Vector3(MapSize * 0.5f, 60f, MapSize * 0.5f), Quaternion.Euler(0f, RenderSettings.sun.transform.eulerAngles.y + 90f, 0f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.targetTexture = target;
            GetOrAdd<UniversalAdditionalCameraData>(cameraObject).renderPostProcessing = false;
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();

            // A 60 degree field of view over 256 rows is about 4.3 rows per degree.
            Color total = Color.clear;
            int count = 0;
            int first = above ? height / 2 + 1 : height / 2 - 107;
            int last = above ? height / 2 + 7 : height / 2 - 43;
            for (int y = first; y <= last; y++)
            {
                for (int x = 0; x < width; x += 4)
                {
                    total += image.GetPixel(x, y);
                    count++;
                }
            }
            Object.DestroyImmediate(image);
            Color average = total / count;
            average.a = 1f;
            return average;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
