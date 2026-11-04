using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Fpv450.Feed
{
    /// <summary>
    /// Re-derives the feed's assets (p4-feed task 1.2): the settings asset, <see cref="FeedFeature"/> on every URP
    /// renderer under <c>Settings/</c>, and the A/B test scene. Idempotent, and the scene is rebuilt from scratch every
    /// time, so nothing here is hand-edited. Run with <c>-executeMethod Fpv450.Feed.FeedSetup.Apply</c>.
    /// </summary>
    public static class FeedSetup
    {
        public const string SettingsPath = "Assets/_Project/Settings/Feed/Resources/FeedSettings.asset";
        public const string ShaderPath = "Assets/_Project/Shaders/Feed.shader";
        public const string TestScenePath = "Assets/_Project/Scenes/FeedTest.unity";
        const string TestSceneFolder = "Assets/_Project/Scenes/FeedTest";
        static readonly string[] Renderers =
        {
            "Assets/_Project/Settings/URP-Low-Renderer.asset",
            "Assets/_Project/Settings/URP-Medium-Renderer.asset",
            "Assets/_Project/Settings/URP-High-Renderer.asset",
        };

        public static void Apply()
        {
            try
            {
                FeedSettings settings = ApplyFeature();
                BuildTestScene(settings);
                AssetDatabase.SaveAssets();
                Debug.Log("[FeedSetup] Applied");
            }
            catch (Exception e)
            {
                Debug.LogError($"[FeedSetup] FAILED: {e.Message}\n{e}");
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
            }
        }

        /// The settings asset and the feature on every renderer, without the test scene, whose rebuild draws new file
        /// ids: what <c>ProjectSetup.Apply</c> re-derives, so a second run of it still changes nothing.
        public static FeedSettings ApplyFeature()
        {
            FeedSettings settings = EnsureSettings();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath)
                ?? throw new FileNotFoundException($"{ShaderPath} did not load as a shader");
            foreach (string path in Renderers)
                EnsureFeature(path, shader);
            return settings;
        }

        static FeedSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<FeedSettings>(SettingsPath);
            if (settings != null)
                return settings;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<FeedSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        /// Adds the feature to a renderer as URP's own "Add Renderer Feature" does: a sub-asset, listed, and mapped by
        /// its local file id.
        static void EnsureFeature(string rendererPath, Shader shader)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath)
                ?? throw new FileNotFoundException($"{rendererPath} did not load as a URP renderer");
            FeedFeature feature = renderer.rendererFeatures.Find(f => f is FeedFeature) as FeedFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FeedFeature>();
                feature.name = "Feed";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            if (feature.Shader != shader)
            {
                feature.Shader = shader;
                EditorUtility.SetDirty(feature);
            }

            var so = new SerializedObject(renderer);
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out _, out long id))
                    map.GetArrayElementAtIndex(i).longValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }

        /// The still A/B's scene (task 1.2): a fixed FPV camera over a simple lit field, laid out after the Godot feed's
        /// `--map sample_patch` view as far as its README and the original allow (a level camera 15 m up at the map
        /// centre, a tree belt across the view with a gap, Godot's sun direction and fog). It is the rig's scene;
        /// the A/B itself feeds one identical frame to both feeds (FEED-00), since two scenes prove nothing.
        static void BuildTestScene(FeedSettings settings)
        {
            Directory.CreateDirectory(TestSceneFolder);
            Material ground = EnsureLit($"{TestSceneFolder}/Ground.mat", Srgb(146, 152, 121));
            Material canopy = EnsureLit($"{TestSceneFolder}/Canopy.mat", Srgb(70, 88, 52));
            Material trunk = EnsureLit($"{TestSceneFolder}/Trunk.mat", Srgb(78, 66, 56));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Object.DestroyImmediate(GameObject.Find("Main Camera"));

            // The sandbox sun (sandbox.tscn's DirectionalLight3D basis): Godot's -Z axis, with Godot's z flipped.
            var sun = GameObject.Find("Directional Light");
            sun.name = "Sun";
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.321394f, -0.766044f, 0.55667f), Vector3.up);
            var light = sun.GetComponent<Light>();
            light.intensity = 1f;
            light.color = Color.white;
            light.shadows = LightShadows.Soft;

            // sandbox.tscn's fog: exponential, 0.0015 /m, its light colour.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0015f;
            RenderSettings.fogColor = new Color(0.7f, 0.74f, 0.78f);

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            plane.transform.localScale = new Vector3(400f, 1f, 400f); // 4 km, sandbox.tscn's PlaneMesh
            plane.GetComponent<MeshRenderer>().sharedMaterial = ground;

            // A tree belt across the view about 50 m out, with a gap at the centre, drawn from a fixed seed.
            var belt = new GameObject("Belt").transform;
            var rng = new System.Random(1);
            for (float x = -160f; x <= 160f; x += 5f + 3f * (float)rng.NextDouble())
            {
                if (Mathf.Abs(x) < 9f)
                    continue;
                float z = 48f + 10f * (float)rng.NextDouble();
                float height = 9f + 6f * (float)rng.NextDouble();
                float radius = 2.5f + 1.5f * (float)rng.NextDouble();
                var tree = new GameObject("Tree").transform;
                tree.SetParent(belt, false);
                tree.localPosition = new Vector3(x, 0f, z);
                Primitive(PrimitiveType.Cylinder, tree, new Vector3(0f, height * 0.35f, 0f),
                    new Vector3(0.35f, height * 0.35f, 0.35f), trunk);
                Primitive(PrimitiveType.Sphere, tree, new Vector3(0f, height - radius, 0f),
                    new Vector3(radius * 2f, radius * 2.2f, radius * 2f), canopy);
            }

            var cameraGo = new GameObject("FpvCamera") { tag = "MainCamera" };
            cameraGo.transform.position = new Vector3(0f, 15f, 0f); // Sandbox.MapStartHeight above the map centre
            var camera = cameraGo.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 2000f;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.None;
            data.renderShadows = true;
            var rig = cameraGo.AddComponent<FpvCamera>();
            rig.settings = settings;

            EditorSceneManager.SaveScene(scene, TestScenePath);
        }

        static Color Srgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

        static Material EnsureLit(string path, Color colour)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Primitive(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
