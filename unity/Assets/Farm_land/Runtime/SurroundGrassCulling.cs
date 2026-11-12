using UnityEngine;

/// <summary>
/// Draws grass on the terrain tiles around the map only while the camera is within grass range of them.
/// Terrain grass costs about 2.5 ms a frame across the surround tiles even when none of it is in view, so
/// tiles further away than the draw distance have their grass switched off.
/// </summary>
public class SurroundGrassCulling : MonoBehaviour
{
    [SerializeField] float grassDistance = 200f;

    // Switches on a little before the grass would come into range, so it never pops in.
    [SerializeField] float margin = 20f;

    Terrain[] tiles;

    void OnEnable()
    {
        tiles = GetComponentsInChildren<Terrain>();
    }

    void OnDisable()
    {
        // Leave the tiles as they were authored, for the Editor and for captures.
        if (tiles == null)
            return;
        foreach (Terrain tile in tiles)
            tile.detailObjectDistance = grassDistance;
    }

    void LateUpdate()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 position = camera.transform.position;
        foreach (Terrain tile in tiles)
        {
            Vector3 origin = tile.transform.position;
            Vector3 size = tile.terrainData.size;
            float dx = Mathf.Max(0f, origin.x - position.x, position.x - (origin.x + size.x));
            float dz = Mathf.Max(0f, origin.z - position.z, position.z - (origin.z + size.z));
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            float wanted = distance < grassDistance + margin ? grassDistance : 0f;
            if (tile.detailObjectDistance != wanted)
                tile.detailObjectDistance = wanted;
        }
    }
}
