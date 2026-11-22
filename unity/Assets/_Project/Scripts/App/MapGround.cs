using System.IO;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>
    /// A map package's one terrain tile, drawn from the height file the flight reads (w20-hunter-track 1.5), so what is drawn
    /// is what is touched. Built in Awake, before SceneManager.sceneLoaded, so FlightScene.Attach finds it among the active
    /// terrains. One plain layer until portion 3 dresses the field. No collider: PhysX stays out of the flight loop.
    /// </summary>
    public sealed class MapGround : MonoBehaviour
    {
        const float MaxSample = 32766f; // the package's samples are Unity's own height units (MapHeightProbes)
        const int AlphamapSize = 16;    // Unity's least: the one layer is everywhere

        public string heightsFile; // from the data root
        public Vector3 sizeM;
        public Material material;
        public Texture2D texture;
        public float tileM;

        TerrainData _data;
        TerrainLayer _layer;

        void Awake()
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

            var terrain = gameObject.AddComponent<Terrain>();
            terrain.drawInstanced = false;
            terrain.materialTemplate = material;
            terrain.basemapDistance = 20000f; // the one layer at every distance: no base map to bake
            terrain.terrainData = _data;
        }

        void OnDestroy()
        {
            Destroy(_data);
            Destroy(_layer);
        }
    }
}
