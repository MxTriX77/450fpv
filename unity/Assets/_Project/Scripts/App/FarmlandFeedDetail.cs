using System.Collections.Generic;
using System.Reflection;
using Fpv450.Feed;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fpv450.App
{
    /// <summary>
    /// The farmland's detail through the feed (w6-farmland-look 1.2): while the camera's feed runs, detail the PAL picture
    /// can't show goes; with the feed off (noclip's clean view) the friend's is back. Through the feed the terrain's grass
    /// is drawn to <see cref="FeedGrassDistance"/> instead of the friend's 200 m. Runtime only: the friend's scene and the
    /// store keep their values.
    /// </summary>
    /// <remarks>
    /// What it costs and buys, measured through the feed at the friend's 10 views with the field pinned, is in
    /// w6-farmland-look's design.md §7: the grass beyond 100 m is the far band's texture, under a field line deep. The
    /// sun's soft shadows stay High: Low was measured too, and the village lane's broad shadow came back as a hard dapple
    /// that the feed shows.
    /// </remarks>
    public sealed class FarmlandFeedDetail : MonoBehaviour
    {
        /// <summary>The terrain grass's distance through the feed, m (the friend's is 200).</summary>
        public const float FeedGrassDistance = 100f;

        FpvCamera rig;
        bool feed;
        readonly List<(Terrain tile, float distance)> tiles = new List<(Terrain, float)>();
        readonly List<(MonoBehaviour culler, float distance)> cullers = new List<(MonoBehaviour, float)>();
        FieldInfo cullerDistance;

        void Start()
        {
            Scene scene = gameObject.scene;
            // The friend's SurroundGrassCulling (Assembly-CSharp, so by name) sets its tiles' grass distance from its own
            // field each frame; the tiles outside one take theirs directly.
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour b in root.GetComponentsInChildren<MonoBehaviour>())
                    if (b != null && b.GetType().Name == "SurroundGrassCulling")
                    {
                        cullerDistance ??= b.GetType().GetField("grassDistance", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (cullerDistance == null)
                            Debug.LogError("[App] the farmland's SurroundGrassCulling has no grassDistance: its tiles keep the friend's grass through the feed");
                        else
                            cullers.Add((b, (float)cullerDistance.GetValue(b)));
                    }
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Terrain t in root.GetComponentsInChildren<Terrain>())
                    if (!cullers.Exists(c => t.transform.IsChildOf(c.culler.transform)))
                        tiles.Add((t, t.detailObjectDistance));
        }

        void LateUpdate()
        {
            rig ??= GetComponent<FpvCamera>();
            bool on = rig != null && rig.Running;
            if (on != feed)
                Apply(on);
        }

        void OnDisable()
        {
            if (feed)
                Apply(false);
        }

        void Apply(bool on)
        {
            feed = on;
            foreach ((Terrain tile, float distance) in tiles)
                if (tile != null)
                    tile.detailObjectDistance = on ? Mathf.Min(distance, FeedGrassDistance) : distance;
            foreach ((MonoBehaviour culler, float distance) in cullers)
                if (culler != null)
                    cullerDistance.SetValue(culler, on ? Mathf.Min(distance, FeedGrassDistance) : distance);
            Debug.Log(on
                ? $"[App] the farmland through the feed: its grass to {FeedGrassDistance:0} m ({tiles.Count} tiles, {cullers.Count} culler)"
                : "[App] the farmland in noclip: the friend's grass distance");
        }
    }
}
