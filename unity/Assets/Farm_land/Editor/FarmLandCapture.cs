using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renders review screenshots from a temporary camera, leaving the scene and its camera untouched.
/// </summary>
public static class FarmLandCapture
{
    const int SheetCellWidth = 800;
    const int SheetCellHeight = 450;
    const int SheetColumns = 4;

    const int MapImageSize = 1600;
    const float MapCameraHeight = 400f;
    const float MapHalfSize = 500f;
    const float MapTreeLodBias = 100f;
    const float EyeHeight = 2f;

    // Terrain details build a few patches per frame, so one frame from a new camera misses most of them.
    const int DetailWarmupFrames = 30;

    // Saved ground-level viewpoints, so every step's review compares the same views.
    static readonly (string name, float x, float z, float yaw, float pitch)[] GroundViews =
    {
        ("view_strips", 170f, 12f, 0f, 2f),
        ("view_junction", 572f, 570f, 347f, 2f),
        ("view_east", 945f, 470f, 270f, 2f),
        ("view_hills", 70f, 440f, 0f, 2f),
        ("view_track_diagonal", 440f, 492f, 243f, 2f),
        ("view_track_north", 513f, 820f, 348f, 2f),
        ("view_track_side", 400f, 767f, 270f, 2f),
        ("view_track_curve", 89f, 610f, 178f, 2f),
        ("view_field_furrows", 122f, 820f, 90f, 2f),
        ("view_field_overgrown", 787f, 780f, 90f, 2f),
        ("view_burnt", 30f, 300f, 60f, 2f),
        ("view_tall", 615f, 125f, 0f, 2f),
        ("view_pines_rise", 68f, 885f, 0f, 6f),
        ("view_dry_line", 345f, 700f, 0f, 2f),
        ("view_field_trees", 385f, 365f, 34f, 2f),
        ("view_village_lane", 466f, 601f, 92f, 3f),
        ("view_village_ruins", 523f, 598f, 300f, 4f),
    };

    // Saved views from above the ground, for the grass draw-distance edge and how the map reads overall.
    static readonly (string name, float x, float z, float yaw, float pitch, float height)[] ElevatedViews =
    {
        ("view_elevated_west", 40f, 330f, 25f, 12f, 40f),
        ("view_elevated_centre", 600f, 430f, 20f, 12f, 35f),
        ("view_high", 500f, 100f, 0f, 25f, 150f),
        ("view_high_steep", 500f, 300f, 0f, 60f, 300f),
        ("view_elevated_fields", 330f, 200f, 40f, 14f, 45f),
        ("view_village_above", 455f, 545f, 45f, 22f, 32f),

        // Close-ups of a 15 m island tree and a 21 m pine, for tree wind.
        ("view_tree_close", 383.7f, 164f, 0f, -14f, 2f),
        ("view_pine_close", 339.9f, 25f, 0f, -18f, 2f),
    };

    // The Step 9 beauty shots: the village, the wide view, the meadow, the village from above, and the map
    // edge. They use saved views, so later changes can be compared.
    public static readonly string[] BeautyViews =
    {
        "view_village_ruins", "view_elevated_centre", "view_hills", "view_village_above", "view_east",
    };

    /// <summary>Saves the beauty shots at 1440p, plus a sheet of all five.</summary>
    public static void CaptureBeautyShots(string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        var sheet = new Texture2D(SheetCellWidth * 2, SheetCellHeight * 3, TextureFormat.RGB24, false);
        try
        {
            for (int i = 0; i < BeautyViews.Length; i++)
            {
                (Vector3 position, Vector3 angles) = SavedView(BeautyViews[i]);
                Texture2D image = Render(position, angles, 2560, 1440);
                File.WriteAllBytes(Path.Combine(outputFolder, $"beauty_{i + 1}_{BeautyViews[i]}.png"), image.EncodeToPNG());
                Texture2D cell = Resized(image, SheetCellWidth, SheetCellHeight);
                sheet.SetPixels((i % 2) * SheetCellWidth, (2 - i / 2) * SheetCellHeight, SheetCellWidth, SheetCellHeight, cell.GetPixels());
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(cell);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outputFolder, "beauty_sheet.jpg"), sheet.EncodeToJPG(90));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }

    /// <summary>Renders one view and saves it as a PNG. A positive orthographic size gives a map-style view.</summary>
    public static void Capture(string path, Vector3 position, Vector3 eulerAngles, int width = 1600, int height = 900, float orthographicSize = 0f)
    {
        Texture2D image = Render(position, eulerAngles, width, height, orthographicSize);
        File.WriteAllBytes(path, image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    /// <summary>Saves a true top-down map, with north at the top, and the saved ground-level views.</summary>
    public static void CaptureLayoutReview(string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);

        // Tree impostors are upright cards, which are hidden edge-on, so the map draws full trees instead.
        // The map is a plan, so it leaves out fog and post-processing.
        Terrain terrain = TerrainAt(MapHalfSize, MapHalfSize);
        float lodBias = terrain.treeLODBiasMultiplier;
        bool fog = RenderSettings.fog;
        terrain.treeLODBiasMultiplier = MapTreeLodBias;
        RenderSettings.fog = false;
        try
        {
            Texture2D map = Render(new Vector3(MapHalfSize, MapCameraHeight, MapHalfSize), new Vector3(90f, 0f, 0f),
                MapImageSize, MapImageSize, MapHalfSize, false);
            File.WriteAllBytes(Path.Combine(outputFolder, "map_top_down.png"), map.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(map);
        }
        finally
        {
            terrain.treeLODBiasMultiplier = lodBias;
            RenderSettings.fog = fog;
        }

        foreach ((string name, float x, float z, float yaw, float pitch) in GroundViews)
        {
            var position = new Vector3(x, GroundHeight(x, z) + EyeHeight, z);
            Capture(Path.Combine(outputFolder, name + ".png"), position, new Vector3(pitch, yaw, 0f));
        }

        foreach ((string name, float x, float z, float yaw, float pitch, float height) in ElevatedViews)
        {
            var position = new Vector3(x, GroundHeight(x, z) + height, z);
            Capture(Path.Combine(outputFolder, name + ".png"), position, new Vector3(pitch, yaw, 0f));
        }
    }

    /// <summary>Ground height at a point, from whichever terrain tile is under it.</summary>
    public static float GroundHeight(float x, float z)
    {
        Terrain terrain = TerrainAt(x, z);
        return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    }

    // The map is surrounded by other terrain tiles since Step 9, so pick the one under the point.
    static Terrain TerrainAt(float x, float z)
    {
        foreach (Terrain terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (x >= origin.x && x <= origin.x + size.x && z >= origin.z && z <= origin.z + size.z)
                return terrain;
        }
        throw new ArgumentException($"FarmLandCapture: no terrain under ({x}, {z})");
    }

    /// <summary>Saves close, mid-distance and overview shots of the Step 2 test patches.</summary>
    public static void CaptureGroundReview(string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        var layers = (FarmLandBuilder.GroundLayer[])Enum.GetValues(typeof(FarmLandBuilder.GroundLayer));

        SaveSheet(Path.Combine(outputFolder, "ground_close.png"), layers, 2.5f, 12f, 30f);
        SaveSheet(Path.Combine(outputFolder, "ground_mid.png"), layers, 30f, 60f, 22f);

        // Oblique view across all patches, for tiling and how the surfaces compare side by side.
        Capture(Path.Combine(outputFolder, "ground_overview.png"), new Vector3(500f, 170f, 110f), new Vector3(35f, 0f, 0f));
    }

    static void SaveSheet(string path, FarmLandBuilder.GroundLayer[] layers, float height, float backOffset, float pitch)
    {
        int rows = Mathf.CeilToInt(layers.Length / (float)SheetColumns);
        var sheet = new Texture2D(SheetCellWidth * SheetColumns, SheetCellHeight * rows, TextureFormat.RGB24, false);

        for (int i = 0; i < layers.Length; i++)
        {
            Rect patch = FarmLandBuilder.TestPatchRect(layers[i]);
            float groundZ = patch.center.y - backOffset;
            var position = new Vector3(patch.center.x, GroundHeight(patch.center.x, groundZ) + height, groundZ);
            Texture2D cell = Render(position, new Vector3(pitch, 0f, 0f), SheetCellWidth, SheetCellHeight);

            // Sheet order matches reading order: first layer at the top left.
            int column = i % SheetColumns;
            int row = rows - 1 - i / SheetColumns;
            sheet.SetPixels(column * SheetCellWidth, row * SheetCellHeight, SheetCellWidth, SheetCellHeight, cell.GetPixels());
            UnityEngine.Object.DestroyImmediate(cell);
        }

        sheet.Apply();
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(sheet);
    }

    /// <summary>
    /// Renders the same views under each lighting mood, as one sheet with a row per mood, then puts the chosen
    /// mood back.
    /// </summary>
    public static void CaptureMoodComparison(string outputFolder, string[] viewNames)
    {
        Directory.CreateDirectory(outputFolder);
        var moods = (FarmLandBuilder.LightingMood[])Enum.GetValues(typeof(FarmLandBuilder.LightingMood));
        var sheet = new Texture2D(SheetCellWidth * viewNames.Length, SheetCellHeight * moods.Length, TextureFormat.RGB24, false);
        try
        {
            for (int row = 0; row < moods.Length; row++)
            {
                FarmLandBuilder.ApplyLightingMood(moods[row]);
                for (int column = 0; column < viewNames.Length; column++)
                {
                    (Vector3 position, Vector3 angles) = SavedView(viewNames[column]);
                    Texture2D image = Render(position, angles, 1600, 900);
                    File.WriteAllBytes(Path.Combine(outputFolder, $"{moods[row]}_{viewNames[column]}.png"), image.EncodeToPNG());
                    Texture2D cell = Resized(image, SheetCellWidth, SheetCellHeight);
                    sheet.SetPixels(column * SheetCellWidth, (moods.Length - 1 - row) * SheetCellHeight, SheetCellWidth, SheetCellHeight, cell.GetPixels());
                    UnityEngine.Object.DestroyImmediate(image);
                    UnityEngine.Object.DestroyImmediate(cell);
                }
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outputFolder, "moods_sheet.jpg"), sheet.EncodeToJPG(88));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sheet);
            FarmLandBuilder.ApplyLightingMood(FarmLandBuilder.ChosenMood);
        }
    }

    /// <summary>Camera position and angles of a saved ground or elevated view.</summary>
    public static (Vector3 position, Vector3 angles) SavedView(string name)
    {
        foreach ((string view, float x, float z, float yaw, float pitch) in GroundViews)
        {
            if (view == name)
                return (new Vector3(x, GroundHeight(x, z) + EyeHeight, z), new Vector3(pitch, yaw, 0f));
        }
        foreach ((string view, float x, float z, float yaw, float pitch, float height) in ElevatedViews)
        {
            if (view == name)
                return (new Vector3(x, GroundHeight(x, z) + height, z), new Vector3(pitch, yaw, 0f));
        }
        throw new ArgumentException($"FarmLandCapture: no saved view called {name}");
    }

    /// <summary>
    /// Saves a run of frames from one view, spaced in real time, to show motion such as wind. In Edit mode
    /// shaders take their time from the real clock, so the frames follow each other as they would in play.
    /// </summary>
    public static void CaptureMotionFrames(string outputFolder, string viewName, float seconds, int framesPerSecond, int width = 960, int height = 540)
    {
        Directory.CreateDirectory(outputFolder);
        (Vector3 position, Vector3 angles) = SavedView(viewName);
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        GameObject cameraObject = CreateCaptureCamera(position, angles, target, 0f, true);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);

        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            for (int frame = 0; frame < DetailWarmupFrames; frame++)
                camera.Render();

            int count = Mathf.RoundToInt(seconds * framesPerSecond);
            float start = Time.realtimeSinceStartup;
            for (int frame = 0; frame < count; frame++)
            {
                float due = start + frame / (float)framesPerSecond;
                while (Time.realtimeSinceStartup < due)
                    System.Threading.Thread.Sleep(1);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(outputFolder, $"{viewName}_{frame:D3}.png"), image.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static GameObject CreateCaptureCamera(Vector3 position, Vector3 eulerAngles, RenderTexture target, float orthographicSize, bool postProcessing)
    {
        var cameraObject = new GameObject("Capture Camera") { hideFlags = HideFlags.HideAndDontSave };
        cameraObject.transform.SetPositionAndRotation(position, Quaternion.Euler(eulerAngles));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = FarmLandBuilder.CameraFarClip;
        camera.fieldOfView = 60f;
        camera.orthographic = orthographicSize > 0f;
        camera.orthographicSize = orthographicSize;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.targetTexture = target;
        cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = postProcessing;
        return cameraObject;
    }

    static Texture2D Resized(Texture2D source, int width, int height)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;
        var result = new Texture2D(width, height, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return result;
    }

    static Texture2D Render(Vector3 position, Vector3 eulerAngles, int width, int height, float orthographicSize = 0f, bool postProcessing = true)
    {
        RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        GameObject cameraObject = CreateCaptureCamera(position, eulerAngles, target, orthographicSize, postProcessing);

        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            for (int frame = 0; frame < DetailWarmupFrames; frame++)
                camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            return image;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}
