using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Step 4: dirt tracks along the field edges, laid over the Step 3 heightmap and painting.
/// </summary>
public static partial class FarmLandBuilder
{
    const float MainTrackWidth = 6f;
    const float SideTrackWidth = 4f;

    const float TrackDipDepth = 0.35f;
    const float TrackDipShoulder = 4f;
    const int TrackSmoothingRadius = 6;
    const int TrackSmoothingPasses = 2;

    const float TrackEdgeSoftness = 0.8f;
    const float TrackEdgeWobbleShare = 0.5f;
    const float TrackEdgeReach = TrackEdgeSoftness + (FieldEdgeWobble + FieldEdgeFineWobble) * TrackEdgeWobbleShare;
    const float SideTrackCrownGrass = 0.85f;
    const float SideTrackCrownInner = 0.3f;
    const float SideTrackCrownOuter = 0.9f;

    const int TrackCurveSegments = 8;
    const float TrackContactDistance = 15f;

    public enum TrackSurface
    {
        Gravel,
        GrassPath,
    }

    /// <summary>
    /// The track network. Paths run through their points as a smooth curve. Side tracks start and end
    /// on the track they join.
    /// </summary>
    public static readonly FarmTrack[] Tracks =
    {
        // Main tracks: the diagonal from the west edge to the junction, then branches north, south and east.
        new FarmTrack("T1 diagonal", TrackSurface.Gravel, MainTrackWidth,
            V(0, 224), V(42, 248), V(122, 296), V(210, 349), V(298, 401), V(350, 447), V(440, 492), V(530, 540), V(575, 570)),
        new FarmTrack("T2 north", TrackSurface.Gravel, MainTrackWidth,
            V(575, 570), V(571, 612), V(543, 655), V(524, 770), V(503, 870), V(482, 965), V(476, 1000)),
        new FarmTrack("T3 south", TrackSurface.Gravel, MainTrackWidth,
            V(575, 570), V(572, 520), V(566, 470), V(571, 350), V(576, 240), V(583, 178), V(591, 28), V(594, 0)),
        new FarmTrack("T4 east", TrackSurface.Gravel, MainTrackWidth,
            V(575, 570), V(640, 566), V(745, 578), V(895, 601), V(940, 612), V(1000, 628)),

        // Side tracks.
        new FarmTrack("T5 north-east", TrackSurface.GrassPath, SideTrackWidth,
            V(770, 582), V(782, 680), V(786, 780), V(787, 878), V(800, 940), V(808, 1000)),
        new FarmTrack("T6 south-east", TrackSurface.GrassPath, SideTrackWidth,
            V(574, 300), V(645, 295), V(760, 295), V(865, 295), V(925, 300), V(1000, 318)),
        new FarmTrack("T7 west loop", TrackSurface.GrassPath, SideTrackWidth,
            V(523, 768), V(480, 766), V(380, 767), V(260, 765), V(150, 765), V(118, 735), V(92, 655), V(87, 555), V(97, 470), V(128, 395), V(150, 314)),
        new FarmTrack("T8 strips", TrackSurface.GrassPath, SideTrackWidth,
            V(312, 413), V(307, 340), V(307, 200), V(307, 60), V(310, 0)),
        new FarmTrack("T9 link", TrackSurface.GrassPath, SideTrackWidth,
            V(576, 231), V(480, 230), V(390, 230), V(345, 238), V(307, 238)),

        // The Step 8 village lane, a dead end off the north track.
        new FarmTrack("T10 village lane", TrackSurface.GrassPath, SideTrackWidth,
            V(572, 601), V(545, 603), V(512, 600), V(480, 602), V(462, 604)),
    };

    public sealed class FarmTrack
    {
        public readonly string Name;
        public readonly TrackSurface Surface;
        public readonly float Width;
        public readonly Vector2[] Path;

        public FarmTrack(string name, TrackSurface surface, float width, params Vector2[] points)
        {
            Name = name;
            Surface = surface;
            Width = width;
            Path = Smooth(points);
        }

        public float HalfWidth => Width * 0.5f;

        public float Length
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < Path.Length; i++)
                    length += Vector2.Distance(Path[i - 1], Path[i]);
                return length;
            }
        }

        static Vector2[] Smooth(Vector2[] points)
        {
            var path = new List<Vector2> { points[0] };
            for (int i = 0; i < points.Length - 1; i++)
            {
                // Past the ends, continue in a straight line so the curve has neighbours to use.
                Vector2 before = i > 0 ? points[i - 1] : 2f * points[0] - points[1];
                Vector2 after = i + 2 < points.Length ? points[i + 2] : 2f * points[i + 1] - points[i];
                for (int segment = 1; segment <= TrackCurveSegments; segment++)
                    path.Add(CurvePoint(before, points[i], points[i + 1], after, (float)segment / TrackCurveSegments));
            }
            return path.ToArray();
        }

        // Centripetal Catmull-Rom, which doesn't overshoot where neighbouring points are unevenly spaced.
        static Vector2 CurvePoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float amount)
        {
            float t1 = Mathf.Sqrt(Vector2.Distance(p0, p1));
            float t2 = t1 + Mathf.Sqrt(Vector2.Distance(p1, p2));
            float t3 = t2 + Mathf.Sqrt(Vector2.Distance(p2, p3));
            float t = Mathf.Lerp(t1, t2, amount);

            Vector2 a1 = Vector2.LerpUnclamped(p0, p1, t / t1);
            Vector2 a2 = Vector2.LerpUnclamped(p1, p2, (t - t1) / (t2 - t1));
            Vector2 a3 = Vector2.LerpUnclamped(p2, p3, (t - t2) / (t3 - t2));
            Vector2 b1 = Vector2.LerpUnclamped(a1, a2, t / t2);
            Vector2 b2 = Vector2.LerpUnclamped(a2, a3, (t - t1) / (t3 - t1));
            return Vector2.LerpUnclamped(b1, b2, (t - t1) / (t2 - t1));
        }
    }

    /// <summary>
    /// Rebuilds the landform and layout, then lays the tracks over them, with level, rubble-strewn plots for
    /// the Step 8 houses. Tracks can't be re-applied on their own.
    /// </summary>
    [MenuItem("Farm Land/Step 4 - Dirt Tracks")]
    public static void BuildDirtTracks()
    {
        BuildLandformAndLayout();

        TerrainData data = LoadRequired<TerrainData>(TerrainDataPath);
        CarveTracks(data);
        LevelVillageGround(data);
        PaintTracks(data);
        PaintVillageGround(data);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log(DescribeTracks());
    }

    /// <summary>Length and field clearance per track, the map share, and how close each field is to a track.</summary>
    public static string DescribeTracks()
    {
        var report = new StringBuilder("Track | m | width | smallest margin to a field (m)\n");
        float area = 0f;
        foreach (FarmTrack track in Tracks)
        {
            area += track.Length * track.Width;
            report.AppendLine($"{track.Name} | {track.Length:F0} | {track.Width:F0} | {SmallestFieldMargin(track):F1}");
        }
        report.AppendLine($"Tracks cover about {area / 10000f:F2} ha, {area / (MapSize * MapSize) * 100f:F1}% of the map");

        foreach (FarmField field in Fields)
        {
            float nearest = NearestTrackDistance(field);
            string state = nearest <= TrackContactDistance ? "ok" : "NOT CONNECTED";
            report.AppendLine($"{field.Name}: track {nearest:F1} m away, {state}");
        }
        return report.ToString().TrimEnd();
    }

    // Smooths bumps along the track, then sinks it slightly, easing back to the ground on each side.
    static void CarveTracks(TerrainData data)
    {
        int size = data.heightmapResolution;
        float spacing = (float)MapSize / (size - 1);

        float[,] heights = data.GetHeights(0, 0, size, size);
        var smooth = (float[,])heights.Clone();
        for (int pass = 0; pass < TrackSmoothingPasses; pass++)
            BlurClamped(smooth, TrackSmoothingRadius);

        float[,] edge = TrackEdgeDistance(null, size, spacing, TrackDipShoulder);
        float dip = TrackDipDepth / MapMaxHeight;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TrackDipShoulder, 0f, edge[z, x]));
                heights[z, x] = Mathf.Lerp(heights[z, x], smooth[z, x], weight) - dip * weight;
            }
        }

        data.SetHeights(0, 0, heights);
    }

    // Side tracks go first, so gravel stays on top where a side track joins a main track.
    static void PaintTracks(TerrainData data)
    {
        int resolution = data.alphamapResolution;
        float[,,] alpha = data.GetAlphamaps(0, 0, resolution, resolution);
        float texelSize = (float)MapSize / resolution;

        PaintTrackSurface(alpha, TrackSurface.GrassPath, SideTrackWidth * 0.5f, resolution, texelSize);
        PaintTrackSurface(alpha, TrackSurface.Gravel, MainTrackWidth * 0.5f, resolution, texelSize);

        data.SetAlphamaps(0, 0, alpha);
    }

    static void PaintTrackSurface(float[,,] alpha, TrackSurface surface, float halfWidth, int resolution, float texelSize)
    {
        float[,] edge = TrackEdgeDistance(surface, resolution, texelSize, TrackEdgeReach);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                if (float.IsPositiveInfinity(edge[z, x]))
                    continue;

                float worldX = x * texelSize;
                float worldZ = z * texelSize;
                float distance = edge[z, x] + FieldEdgeOffset(worldX, worldZ) * TrackEdgeWobbleShare;
                float coverage = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TrackEdgeSoftness, -TrackEdgeSoftness, distance));
                if (coverage > 0f)
                    BlendTrackSurface(alpha, z, x, surface, coverage, worldX, worldZ, edge[z, x] + halfWidth);
            }
        }
    }

    // Gravel with drifts of dry mud, or a grassy path with bare wheel ruts and grass down the middle,
    // which turns dry where the track crosses burnt ground.
    static void BlendTrackSurface(float[,,] alpha, int z, int x, TrackSurface surface, float coverage, float worldX, float worldZ, float centreDistance)
    {
        for (int i = 0; i < GroundLayerCount; i++)
            alpha[z, x, i] *= 1f - coverage;

        if (surface == TrackSurface.Gravel)
        {
            float dryMud = Mathf.Lerp(0.35f, 0.8f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, BlendNoise(worldX, worldZ, 900f))));
            alpha[z, x, (int)GroundLayer.Gravel] += coverage * (1f - dryMud);
            alpha[z, x, (int)GroundLayer.DryMud] += coverage * dryMud;
            return;
        }

        float drift = 0.35f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.7f, BlendNoise(worldX, worldZ, 1100f)));
        float crown = SideTrackCrownGrass * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(SideTrackCrownInner, SideTrackCrownOuter, centreDistance));
        float grass = Mathf.Max(drift, crown);
        float burnt = BurntShare(worldX, worldZ);
        alpha[z, x, (int)GroundLayer.GrassPath] += coverage * (1f - grass);
        alpha[z, x, (int)GroundLayer.GrassA] += coverage * grass * (1f - burnt);
        alpha[z, x, (int)GroundLayer.GrassDry] += coverage * grass * burnt;
    }

    /// <summary>
    /// Distance from each sample to the nearest track edge, negative on the track and infinite past the
    /// reach. A null surface includes every track.
    /// </summary>
    static float[,] TrackEdgeDistance(TrackSurface? surface, int size, float spacing, float reach)
    {
        var edge = new float[size, size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
                edge[z, x] = float.PositiveInfinity;
        }

        foreach (FarmTrack track in Tracks)
        {
            if (surface.HasValue && track.Surface != surface.Value)
                continue;

            float range = track.HalfWidth + reach;
            for (int i = 1; i < track.Path.Length; i++)
            {
                Vector2 from = track.Path[i - 1];
                Vector2 to = track.Path[i];
                int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - range) / spacing));
                int maxX = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + range) / spacing));
                int minZ = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - range) / spacing));
                int maxZ = Mathf.Min(size - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + range) / spacing));

                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float distance = SegmentDistance(new Vector2(x * spacing, z * spacing), from, to) - track.HalfWidth;
                        if (distance <= reach && distance < edge[z, x])
                            edge[z, x] = distance;
                    }
                }
            }
        }
        return edge;
    }

    static float SegmentDistance(Vector2 point, Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float along = Mathf.Clamp01(Vector2.Dot(point - from, segment) / segment.sqrMagnitude);
        return Vector2.Distance(point, from + segment * along);
    }

    static float SmallestFieldMargin(FarmTrack track)
    {
        float smallest = float.MaxValue;
        foreach (FarmField field in Fields)
        {
            foreach (Vector2 point in track.Path)
                smallest = Mathf.Min(smallest, field.SignedDistance(point) - track.HalfWidth);
        }
        return smallest;
    }

    static float NearestTrackDistance(FarmField field)
    {
        float nearest = float.MaxValue;
        foreach (FarmTrack track in Tracks)
        {
            foreach (Vector2 point in track.Path)
                nearest = Mathf.Min(nearest, field.SignedDistance(point) - track.HalfWidth);
        }
        return nearest;
    }

    static Vector2 V(float x, float z)
    {
        return new Vector2(x, z);
    }
}
