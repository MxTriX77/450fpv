using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Assets;
using Fpv450.Feed;
using Fpv450.Input;
using Fpv450.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fpv450
{
    /// <summary>
    /// Builds <c>HunterTrack.unity</c> (w20-hunter-track 1.5, the raw map) from <c>maps/hunter-09</c>: every placement as its
    /// prefab at the file's exact pose and scale, the package's terrain tile (MapGround, drawn from its height file at load)
    /// with the package's grass on Village's own detail prototypes (task 3.2a), and the test range's rig, which
    /// FlightScene.Attach takes over. The scene is never edited by hand:
    /// <c>-batchmode -nographics -quit -executeMethod Fpv450.HunterScene.Build</c> (exit code 1 on any error).
    /// </summary>
    public static class HunterScene
    {
        public const string ScenePath = "Assets/_Project/Scenes/HunterTrack.unity";
        const string Tag = "[HunterScene]";
        const string TerrainMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat";
        const float ViewHeightM = 2f; // the noclip rig's start above the spawn, for the scene opened alone
        const string VillageTerrainData = "Assets/Farm_land/Terrain/FarmLand_TerrainData.asset";
        const float VillageGrassDistanceM = 200f; // the farmland map tile's detail distance (FarmLand.unity; FarmLandBuilder.Grass)
        const float ScaleTolerance = 1e-4f;

        public static void Build()
        {
            try
            {
                BuildScene();
            }
            catch (Exception e)
            {
                Debug.LogError($"{Tag} FAILED: {e.Message}\n{e}");
                EditorApplication.Exit(1);
            }
        }

        static void BuildScene()
        {
            ShaderUtil.allowAsyncCompilation = false;
            string relative = "maps/" + FlightWorlds.HunterMap + "/";
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string manifestFile = relative + "map.json", placementsFile = relative + "placements.json";
            Dictionary<string, object> manifest = Json.Obj(Json.Parse(File.ReadAllText(Path.Combine(root, manifestFile))), manifestFile);
            Dictionary<string, object> data = Json.Obj(Json.Parse(File.ReadAllText(Path.Combine(root, placementsFile))), placementsFile);
            List<object> tiles = Json.Arr(manifest, "tiles", manifestFile);
            if (tiles.Count != 1) throw new InvalidOperationException($"{manifestFile}: {tiles.Count} tiles, this scene draws one");
            Dictionary<string, object> tile = Json.Obj(tiles[0], "tile");

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- the ground
            Material floor = GalleryScene.FloorMaterial();
            var groundObject = new GameObject("Ground");
            groundObject.transform.position = Json.Vec3(Json.Get(tile, "position_m", "tile"), "position_m");
            var ground = groundObject.AddComponent<MapGround>();
            ground.heightsFile = relative + Json.Str(Json.Obj(tile, "heights", "tile"), "file", "heights");
            ground.sizeM = Json.Vec3(Json.Get(tile, "size_m", "tile"), "size_m");
            ground.material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath)
                              ?? throw new FileNotFoundException($"{TerrainMaterialPath} is missing");
            ground.texture = floor.GetTexture("_BaseMap") as Texture2D
                             ?? throw new InvalidOperationException("the floor material has no base texture");
            ground.tileM = 1f / floor.GetTextureScale("_BaseMap").x;
            if (manifest.ContainsKey("grass"))
                Grass(ground, Json.Obj(manifest, "grass", manifestFile), relative);

            // --- the placements
            var placed = new GameObject("Placed").transform;
            var track = new GameObject("track").transform;
            track.SetParent(placed, false);
            int rows = 0;
            foreach (object item in Json.Arr(data, "placements", placementsFile))
            {
                Dictionary<string, object> row = Json.Obj(item, $"{placementsFile} placement");
                int id = (int)Json.Num(row, "id", placementsFile);
                string what = $"{placementsFile} placement {id}", variant = Json.Str(row, "variant", what);
                GameObject instance = GalleryScene.Instantiate("track/" + variant, track);
                instance.name = TestRangeScene.InstanceName(id, variant);
                List<object> q = Json.Arr(row, "rotation_xyzw", what);
                if (q.Count != 4) throw new FormatException($"{what}: rotation_xyzw holds {q.Count} numbers");
                instance.transform.SetLocalPositionAndRotation(Json.Vec3(Json.Get(row, "position_m", what), what),
                    new Quaternion((float)(double)q[0], (float)(double)q[1], (float)(double)q[2], (float)(double)q[3]));
                instance.transform.localScale = Json.Vec3(Json.Get(row, "scale", what), what);
                rows++;
            }

            // --- the rig
            Dictionary<string, object> start = Json.Obj(Json.Arr(data, "start_points", placementsFile).Single(), "start point");
            var view = new ViewpointKeys.View
            {
                name = Json.Str(start, "id", "start point"),
                position = Json.Vec3(Json.Get(start, "position_m", "start point"), "start point") + Vector3.up * ViewHeightM,
                yawDeg = Json.Num(start, "heading_deg", "start point"),
            };
            GameObject go = GalleryScene.Rig(out LookSetup look, out ViewpointKeys keys);
            keys.views = new[] { view };
            go.transform.SetPositionAndRotation(view.position, Quaternion.Euler(0f, view.yawDeg, 0f));
            var boot = go.AddComponent<TestRangeBoot>();
            boot.placed = placed;
            boot.keys = keys;
            boot.noclip = go.GetComponent<NoclipCamera>();
            boot.look = look;

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException($"could not save {ScenePath}");
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"{Tag} wrote {ScenePath}: {rows} placements, the tile {ground.heightsFile}, " +
                      $"{(ground.grassLayers?.Length ?? 0)} grass layers {ground.grassFile}, start '{view.name}'");
        }

        // The package's grass layers on Village's own detail prototypes (the farmland map tile's): each layer's prefab and
        // settings are Village's, its scales the package's, which must be Village's too.
        static void Grass(MapGround ground, Dictionary<string, object> grass, string relative)
        {
            TerrainData village = AssetDatabase.LoadAssetAtPath<TerrainData>(VillageTerrainData)
                                  ?? throw new FileNotFoundException($"{VillageTerrainData} is missing");
            DetailPrototype[] theirs = village.detailPrototypes;
            List<object> layers = Json.Arr(grass, "layers", "grass");
            if (layers.Count != theirs.Length)
                throw new InvalidOperationException($"the package has {layers.Count} grass layers, Village {theirs.Length}");
            ground.grassFile = relative + Json.Str(grass, "file", "grass");
            ground.grassLayers = new MapGround.GrassLayer[layers.Count];
            for (int l = 0; l < layers.Count; l++)
            {
                string what = $"grass layer {l}";
                Dictionary<string, object> layer = Json.Obj(layers[l], what);
                DetailPrototype v = theirs[l];
                Vector2 width = Json.Vec2(layer, "width_scale", what), height = Json.Vec2(layer, "height_scale", what);
                if (v.prototype == null || !v.usePrototypeMesh || v.prototype.name != Json.Str(layer, "prefab", what)
                    || Mathf.Abs(v.minWidth - width.x) > ScaleTolerance || Mathf.Abs(v.maxWidth - width.y) > ScaleTolerance
                    || Mathf.Abs(v.minHeight - height.x) > ScaleTolerance || Mathf.Abs(v.maxHeight - height.y) > ScaleTolerance)
                    throw new InvalidOperationException($"{what}: not Village's {v.prototype?.name} at its scales");
                ground.grassLayers[l] = new MapGround.GrassLayer
                {
                    prefab = v.prototype, minWidth = width.x, maxWidth = width.y, minHeight = height.x, maxHeight = height.y,
                    noiseSpread = v.noiseSpread, alignToGround = v.alignToGround, positionJitter = v.positionJitter,
                    healthyColor = v.healthyColor, dryColor = v.dryColor, renderMode = v.renderMode, useInstancing = v.useInstancing,
                };
            }
            ground.grassPerPatch = village.detailResolutionPerPatch;
            ground.grassScatter = village.detailScatterMode;
            ground.grassDistanceM = VillageGrassDistanceM;
        }
    }
}
