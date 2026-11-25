using System;
using System.IO;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// A map package's one terrain tile, drawn from the height file the flight reads (w20-hunter-track 1.5), so what is drawn
    /// is what is touched. Built in Awake, before SceneManager.sceneLoaded, so FlightScene.Attach finds it among the active
    /// terrains. One plain layer until portion 3 dresses the field. No collider: PhysX stays out of the flight loop.
    /// The package's grass (task 3.2a), when it has one, is the terrain's details: Village's own detail prototypes (HunterScene
    /// copies them from the farmland's terrain) with the package's scales and counts, the counts the sim's stems are made of.
    /// </summary>
    public sealed class MapGround : MonoBehaviour
    {
        const float MaxSample = 32766f; // the package's samples are Unity's own height units (MapHeightProbes)
        const int AlphamapSize = 16;    // Unity's least: the one layer is everywhere

        /// <summary>One grass layer: Village's detail prototype for it, its scales the package's.</summary>
        [Serializable]
        public struct GrassLayer
        {
            public GameObject prefab;
            public float minWidth, maxWidth, minHeight, maxHeight, noiseSpread, alignToGround, positionJitter;
            public Color healthyColor, dryColor;
            public DetailRenderMode renderMode;
            public bool useInstancing;
        }

        public string heightsFile; // from the data root
        public Vector3 sizeM;
        public Material material;
        public Texture2D texture;
        public float tileM;

        public string grassFile; // from the data root: the package's grass/map.raw, or empty for none
        public GrassLayer[] grassLayers;
        public int grassPerPatch;
        public DetailScatterMode grassScatter;
        public float grassDistanceM;

        TerrainData _data;
        TerrainLayer _layer;

        void Awake() => Draw();

        /// <summary>Builds the tile: Awake in play, and MapStills for the still of a scene opened in edit mode, which
        /// calls no Awake.</summary>
        public void Draw()
        {
            byte[] bytes = File.ReadAllBytes(AppData.File(heightsFile));
            int res = Mathf.RoundToInt(Mathf.Sqrt(bytes.Length / 2));
            var heights = new float[res, res]; // Unity's order: [z from the south, x]; the file's rows run from the north
            for (int r = 0; r < res; r++)
                for (int c = 0; c < res; c++)
                    heights[res - 1 - r, c] = (bytes[2 * (r * res + c)] | bytes[2 * (r * res + c) + 1] << 8) / MaxSample;

            _layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(tileM, tileM) };
            _data = new TerrainData { heightmapResolution = res, alphamapResolution = AlphamapSize, size = sizeM };
            _data.SetHeights(0, 0, heights);
            _data.terrainLayers = new[] { _layer };
            var full = new float[AlphamapSize, AlphamapSize, 1];
            for (int z = 0; z < AlphamapSize; z++)
                for (int x = 0; x < AlphamapSize; x++)
                    full[z, x, 0] = 1f;
            _data.SetAlphamaps(0, 0, full);
            if (!string.IsNullOrEmpty(grassFile))
                Grass();

            var terrain = gameObject.AddComponent<Terrain>();
            terrain.drawInstanced = false;
            terrain.materialTemplate = material;
            terrain.basemapDistance = 20000f; // the one layer at every distance: no base map to bake
            terrain.detailObjectDistance = grassDistanceM;
            terrain.terrainData = _data;
        }

        // The package's grass as Village's terrain holds its own: a prototype per layer, instance counts per cell.
        void Grass()
        {
            byte[] counts = File.ReadAllBytes(AppData.File(grassFile));
            int layers = grassLayers.Length, res = Mathf.RoundToInt(Mathf.Sqrt(counts.Length / Mathf.Max(layers, 1))), plane = res * res;
            if (layers == 0 || layers * plane != counts.Length)
                throw new InvalidDataException($"{grassFile}: {counts.Length} bytes are not {layers} square layers");
            var prototypes = new DetailPrototype[layers];
            for (int l = 0; l < layers; l++)
            {
                GrassLayer g = grassLayers[l];
                prototypes[l] = new DetailPrototype
                {
                    prototype = g.prefab, usePrototypeMesh = true, useInstancing = g.useInstancing, renderMode = g.renderMode,
                    minWidth = g.minWidth, maxWidth = g.maxWidth, minHeight = g.minHeight, maxHeight = g.maxHeight,
                    noiseSpread = g.noiseSpread, healthyColor = g.healthyColor, dryColor = g.dryColor,
                    alignToGround = g.alignToGround, positionJitter = g.positionJitter,
                };
            }
            _data.detailPrototypes = prototypes;
            _data.SetDetailScatterMode(grassScatter);
            _data.SetDetailResolution(res, grassPerPatch);
            var layer = new int[res, res]; // Unity's order again: [z from the south, x]
            for (int l = 0; l < layers; l++)
            {
                for (int r = 0; r < res; r++)
                    for (int c = 0; c < res; c++)
                        layer[res - 1 - r, c] = counts[l * plane + r * res + c];
                _data.SetDetailLayer(0, 0, l, layer);
            }
        }

        void OnDestroy()
        {
            Destroy(_data);
            Destroy(_layer);
        }
    }
}
