using UnityEngine;
using UnityEngine.Rendering;

namespace Fpv450.Feed
{
    /// <summary>
    /// The look setup (p1-g1-look-lock-gallery design 4, VIS-02, VIS-04): drives the directional light, a gradient
    /// sky, the ambient probe and reflection, and exponential height fog from <see cref="hour"/> and
    /// <see cref="cloudCover"/> through <see cref="LookModel"/>. <see cref="Select"/> applies one of the presets under
    /// <c>Settings/Light/</c> (day, low sun, overcast, night: keys F1-F4 in the gallery) and shows its label for one
    /// second. It runs in play mode only; the review capture keeps its own rig.
    /// </summary>
    public sealed class LookSetup : MonoBehaviour
    {
        const float LabelSeconds = 1f;
        const int ReflectionSize = 32;

        public TextAsset skyModel;
        public TextAsset[] presets = new TextAsset[0];
        public Shader skyShader;
        [Tooltip("The sun; one is made under this object when empty.")]
        public Light sun;
        [Tooltip("The camera whose height thins the fog; Camera.main when empty.")]
        public Camera viewCamera;
        public int startPreset;

        [Header("Applied by Select, editable in play mode")]
        public float hour = 12f;
        [Range(0f, 1f)] public float cloudCover = 0.2f;
        public float fogExtinctionPerKm = 0.2f;
        public float fogScaleHeightM = 1200f;

        SkyModel model;
        Material skyMaterial;
        Cubemap reflection;
        string label;
        float labelUntil = -1f;
        GUIStyle labelStyle;

        public LookPreset Active { get; private set; }
        public LookState State { get; private set; }
        public SkyModel Model => model ??= SkyModel.FromJson(skyModel.text, skyModel.name);
        public int PresetCount => presets.Length;

        void Start() => Select(startPreset);

        /// <summary>Applies preset <paramref name="index"/> (0 day, 1 low sun, 2 overcast, 3 night) and shows its label.</summary>
        public void Select(int index)
        {
            TextAsset asset = presets[index];
            Active = LookPreset.FromJson(asset.text, asset.name);
            hour = Active.hour;
            cloudCover = Active.cloud_cover;
            fogExtinctionPerKm = Active.fog_extinction_per_km;
            fogScaleHeightM = Active.fog_scale_height_m;
            Apply();
            label = Active.label;
            labelUntil = Time.unscaledTime + LabelSeconds;
            Debug.Log($"[Look] {Active.name}: hour {hour:0.##}, cover {cloudCover:0.##}, sun {State.SunElevationDeg:0.0}° up at {State.SunAzimuthDeg:0}°, " +
                      $"horizontal illuminance {State.HorizontalIlluminance:0.000} (sun {State.SunHorizontal:0.000}), fog {fogExtinctionPerKm:0.###}/km over {fogScaleHeightM:0} m");
        }

        /// <summary>Applies the current hour, cloud cover and haze.</summary>
        public void Apply()
        {
            State = LookModel.Evaluate(Model, hour, cloudCover, fogExtinctionPerKm, fogScaleHeightM);
            LookState s = State;

            if (sun == null)
            {
                var go = new GameObject("Sun");
                go.transform.SetParent(transform, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            float intensity = Mathf.Max(s.Sun.r, Mathf.Max(s.Sun.g, s.Sun.b));
            sun.enabled = intensity > 0f;
            if (intensity > 0f)
            {
                // URP lights use linear intensity: the colour is sRGB-encoded, so its linear value times the intensity is s.Sun.
                sun.useColorTemperature = false;
                sun.color = (s.Sun * (1f / intensity)).gamma;
                sun.intensity = intensity;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.LookRotation(-s.TowardsSun, Vector3.up);
            }
            RenderSettings.sun = sun;

            if (skyMaterial == null)
                skyMaterial = new Material(skyShader) { name = "LookSky", hideFlags = HideFlags.DontSave };
            skyMaterial.SetVector("_SkyZenith", s.Zenith);
            skyMaterial.SetVector("_SkyHorizon", s.Horizon);
            skyMaterial.SetVector("_SkyGround", s.Ground);
            skyMaterial.SetFloat("_SkyZenithExponent", Model.zenith_exponent);
            skyMaterial.SetFloat("_SkyGroundFade", Model.ground_fade_sin);
            skyMaterial.SetVector("_SunDirection", s.TowardsSun);
            skyMaterial.SetVector("_SunDisc", s.Sun * (Model.sun_disc_gain * s.SunVisibility));
            skyMaterial.SetVector("_SunHalo", s.Sun * (Model.sun_halo_gain * s.SunVisibility));
            skyMaterial.SetFloat("_SunCosRadius", Mathf.Cos(Model.sun_disc_radius_deg * Mathf.Deg2Rad));
            RenderSettings.skybox = skyMaterial;

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientProbe = s.Ambient;
            RenderSettings.ambientIntensity = 1f;
            FillReflection(s);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = s.FogColor.gamma;
            UpdateFog(viewCamera != null ? viewCamera : Camera.main);
        }

        /// <summary>Thins the fog with the camera's height (exponential height fog along near-level rays).</summary>
        public void UpdateFog(Camera camera) =>
            RenderSettings.fogDensity = LookModel.FogDensity(State, camera != null ? camera.transform.position.y : 0f);

        void LateUpdate()
        {
            if (Active != null)
                UpdateFog(viewCamera != null ? viewCamera : Camera.main);
        }

        void OnValidate()
        {
            if (Application.isPlaying && Active != null)
                Apply();
        }

        /// <summary>The environment reflection: the sky gradient without the sun, mip-mapped for rough surfaces.</summary>
        void FillReflection(LookState s)
        {
            if (reflection == null)
                reflection = new Cubemap(ReflectionSize, TextureFormat.RGBAHalf, true) { name = "LookReflection", hideFlags = HideFlags.DontSave };
            var pixels = new Color[ReflectionSize * ReflectionSize];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < ReflectionSize; y++)
                for (int x = 0; x < ReflectionSize; x++)
                {
                    float u = 2f * (x + 0.5f) / ReflectionSize - 1f, v = 2f * (y + 0.5f) / ReflectionSize - 1f;
                    pixels[y * ReflectionSize + x] = LookModel.Sky(s, FaceDirection((CubemapFace)face, u, v), Model);
                }
                reflection.SetPixels(pixels, (CubemapFace)face);
            }
            reflection.Apply(true, false);
        }

        /// <summary>A cubemap texel's direction, rows from the top of each face (the D3D cube layout).</summary>
        static Vector3 FaceDirection(CubemapFace face, float u, float v) => face switch
        {
            CubemapFace.PositiveX => new Vector3(1f, -v, -u),
            CubemapFace.NegativeX => new Vector3(-1f, -v, u),
            CubemapFace.PositiveY => new Vector3(u, 1f, v),
            CubemapFace.NegativeY => new Vector3(u, -1f, -v),
            CubemapFace.PositiveZ => new Vector3(u, -v, 1f),
            _ => new Vector3(-u, -v, -1f),
        };

        void OnGUI()
        {
            if (Time.unscaledTime > labelUntil || string.IsNullOrEmpty(label))
                return;
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(16, Screen.height / 40), fontStyle = FontStyle.Bold };
            var rect = new Rect(24f, 20f, Screen.width - 48f, labelStyle.fontSize * 2f);
            labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), label, labelStyle);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(rect, label, labelStyle);
        }

        void OnDestroy()
        {
            Release(skyMaterial);
            Release(reflection);
        }

        static void Release(Object made)
        {
            if (made == null)
                return;
            if (Application.isPlaying)
                Destroy(made);
            else
                DestroyImmediate(made);
        }

#if UNITY_EDITOR
        /// <summary>The project's sky model, presets and sky shader, when the component is added in the editor.</summary>
        void Reset()
        {
            skyModel = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(SkyModelPath);
            presets = System.Array.ConvertAll(PresetPaths, path => UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            skyShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
        }
#endif

        public const string SkyModelPath = "Assets/_Project/Settings/Sky/sky_model.json";
        public const string SkyShaderPath = "Assets/_Project/Shaders/GradientSky.shader";
        public static readonly string[] PresetPaths =
        {
            "Assets/_Project/Settings/Light/day.json",
            "Assets/_Project/Settings/Light/low_sun.json",
            "Assets/_Project/Settings/Light/overcast.json",
            "Assets/_Project/Settings/Light/night.json",
        };
    }
}
