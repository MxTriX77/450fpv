using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Step 3: rolling heightmap, field outlines and the ground painting that follows them.
/// </summary>
public static partial class FarmLandBuilder
{
    const int LandformSeed = 4217;
    const float LandformLowest = 12f;
    const float LandformHighest = 42f;

    // Stronger relief in chosen places, mostly in the outer meadows, next to low ground left as it is.
    // Heights are in metres, and noise varies each one so the rises don't look like domes.
    static readonly (float x, float z, float radius, float height)[] LocalRises =
    {
        (80f, 930f, 125f, 15f),
        (880f, 935f, 115f, 14f),
        (930f, 265f, 130f, 16f),
        (610f, 325f, 85f, 8f),
    };

    const float LocalRiseNoiseWavelength = 150f;
    const float LocalRiseMinimumShare = 0.7f;
    const float LocalRiseMaximumShare = 1.3f;

    // Fine bumps for the meadow only, so fields stay smooth.
    const float MeadowBumpHeight = 0.7f;
    const float MeadowFineBumpHeight = 0.12f;

    // Three box passes at this radius average out anything shorter than about 100 m.
    const int FieldSmoothingRadius = 36;
    const int FieldSmoothingPasses = 3;
    const float FieldSmoothingFalloff = 35f;

    const float FieldEdgeSoftness = 1f;
    const float FieldEdgeWobble = 1.2f;
    const float FieldEdgeFineWobble = 0.5f;

    const int ArcSegments = 16;

    // Weeds appear where noise passes a threshold, and most of that noise falls between 0.3 and 0.7.
    const float WeedThresholdBare = 0.66f;
    const float WeedThresholdOvergrown = 0.34f;
    const float WeedBlendWidth = 0.1f;
    const float WeedEdgeShift = 0.1f;
    const float WeedEdgeWidth = 14f;

    /// <summary>
    /// The field layout. Outlines run counter-clockwise seen from above (x east, z north), which leaves
    /// room between fields for the Step 4 tracks: a diagonal from the west edge to a junction near
    /// (560, 560), with branches running south, north and east from there. The number after each name
    /// is how overgrown the abandoned field is, from 0 for bare furrows to 1 for mostly grown over.
    /// </summary>
    public static readonly FarmField[] Fields =
    {
        // Three long strips side by side, with their north ends cut by the diagonal track.
        new FarmField("F1", 0.35f, P(42, 32), P(122, 30), P(122, 285), P(42, 237)),
        new FarmField("F2", 0.6f, P(130, 30), P(210, 28), P(210, 337), P(130, 289)),
        new FarmField("F3", 0.2f, P(218, 28), P(298, 26), P(298, 390), P(218, 342)),

        // Wedge under the diagonal track, with cut corners on the west and at the junction.
        new FarmField("F4", 0.85f, P(384, 240), P(566, 240), P(556, 470), P(520, 520), P(350, 418), P(350, 274)),

        // D-shaped west end, with meadow on its north and west.
        new FarmField("F5", 0.3f, P(410, 28), P(580, 28), P(572, 178), P(410, 178, 55f)),

        // Largest field, with a long curved west side and a slanted south side.
        new FarmField("F6", 0.55f, P(170, 400), P(440, 515), P(470, 710), P(150, 710, 65f)),

        // Slanted east side along the north track, with a cut south-west corner.
        new FarmField("F7", 0.2f, P(130, 775), P(495, 775), P(462, 965), P(260, 965), P(130, 870)),

        // Tall field beside the north track, with cut corners at the junction and to the north-east.
        new FarmField("F8", 0.7f, P(600, 600), P(745, 625), P(745, 925), P(705, 965), P(495, 965), P(556, 650)),

        // Small field with a rounded east end, set apart in the north-east meadow.
        new FarmField("F9", 0.9f, P(795, 690), P(920, 715, 40f), P(920, 870), P(795, 870)),

        // Six sides, with its north edge following the east track.
        new FarmField("F10", 0.35f, P(630, 360), P(860, 360), P(930, 470), P(895, 590), P(650, 552), P(630, 530)),

        // Curved east side and a cut south-west corner.
        new FarmField("F11", 0.6f, P(690, 25), P(860, 25, 30f), P(860, 285), P(650, 285), P(650, 55)),
    };

    /// <summary>A corner of a field outline. Bulge curves the side leaving this corner, in metres, outward when positive.</summary>
    public readonly struct OutlinePoint
    {
        public readonly Vector2 Position;
        public readonly float Bulge;

        public OutlinePoint(Vector2 position, float bulge)
        {
            Position = position;
            Bulge = bulge;
        }
    }

    public sealed class FarmField
    {
        public readonly string Name;
        public readonly float Overgrowth;
        public readonly int SideCount;
        public readonly Vector2[] Outline;
        public readonly Rect Bounds;
        public readonly GroundLayer MudLayer;

        public FarmField(string name, float overgrowth, params OutlinePoint[] corners)
        {
            Name = name;
            Overgrowth = overgrowth;
            SideCount = corners.Length;
            Outline = Tessellate(corners);
            MudLayer = FurrowLayer(corners);

            Vector2 min = Outline[0];
            Vector2 max = Outline[0];
            foreach (Vector2 point in Outline)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public float AreaHectares
        {
            get
            {
                float doubled = 0f;
                for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
                    doubled += Outline[j].x * Outline[i].y - Outline[i].x * Outline[j].y;
                return Mathf.Abs(doubled) * 0.5f / 10000f;
            }
        }

        public Vector2 Centre => Bounds.center;

        /// <summary>
        /// Share of weeds at a point, from 0 to 1. Used for both ground paint and grass, so they agree.
        /// Patches stretch along the furrows and spread in from the field edge.
        /// </summary>
        public float WeedShare(float worldX, float worldZ, float signedDistance)
        {
            const float alongScale = 1f / 35f;
            const float acrossScale = 1f / 9f;
            const float noiseOffset = 1300f;

            bool rowsAlongX = MudLayer == GroundLayer.AerialMud0;
            float along = rowsAlongX ? worldX : worldZ;
            float across = rowsAlongX ? worldZ : worldX;
            float rows = Mathf.PerlinNoise(along * alongScale + noiseOffset, across * acrossScale + noiseOffset);
            float noise = BlendNoise(worldX, worldZ, noiseOffset) * 0.75f + rows * 0.25f;

            float edge = WeedEdgeShift * Mathf.InverseLerp(WeedEdgeWidth, 0f, -signedDistance);
            float threshold = Mathf.Lerp(WeedThresholdBare, WeedThresholdOvergrown, Overgrowth) - edge;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - WeedBlendWidth, threshold + WeedBlendWidth, noise));
        }

        /// <summary>World-axis name for the direction the rows run in.</summary>
        public string FurrowAxis => MudLayer == GroundLayer.AerialMud0 ? "X" : "Z";

        // Negative inside the field. Works on any outline, using the nearest edge and an even-odd crossing test.
        public float SignedDistance(Vector2 point)
        {
            Vector2[] outline = Outline;
            float nearest = (point - outline[0]).sqrMagnitude;
            float sign = 1f;

            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            {
                Vector2 edge = outline[j] - outline[i];
                Vector2 offset = point - outline[i];
                float along = Mathf.Clamp01(Vector2.Dot(offset, edge) / Vector2.Dot(edge, edge));
                nearest = Mathf.Min(nearest, (offset - edge * along).sqrMagnitude);

                bool above = point.y >= outline[i].y;
                bool belowNext = point.y < outline[j].y;
                bool leftOfEdge = edge.x * offset.y > edge.y * offset.x;
                if ((above && belowNext && leftOfEdge) || (!above && !belowNext && !leftOfEdge))
                    sign = -sign;
            }
            return sign * Mathf.Sqrt(nearest);
        }

        // Rows follow the world axis closest to the longest straight side.
        static GroundLayer FurrowLayer(OutlinePoint[] corners)
        {
            Vector2 longest = Vector2.zero;
            for (int i = 0; i < corners.Length; i++)
            {
                if (corners[i].Bulge != 0f)
                    continue;
                Vector2 side = corners[(i + 1) % corners.Length].Position - corners[i].Position;
                if (side.sqrMagnitude > longest.sqrMagnitude)
                    longest = side;
            }
            return Mathf.Abs(longest.x) >= Mathf.Abs(longest.y) ? GroundLayer.AerialMud0 : GroundLayer.AerialMud90;
        }

        static Vector2[] Tessellate(OutlinePoint[] corners)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < corners.Length; i++)
            {
                points.Add(corners[i].Position);
                if (corners[i].Bulge != 0f)
                    AddArc(points, corners[i].Position, corners[(i + 1) % corners.Length].Position, corners[i].Bulge);
            }
            return points.ToArray();
        }

        // Circular arc between two corners, using the chord and how far the middle bulges away from it.
        static void AddArc(List<Vector2> points, Vector2 from, Vector2 to, float bulge)
        {
            Vector2 chord = to - from;
            float halfChord = chord.magnitude * 0.5f;
            Vector2 direction = chord.normalized;
            Vector2 outward = new Vector2(direction.y, -direction.x) * Mathf.Sign(bulge);
            float height = Mathf.Abs(bulge);
            float radius = (halfChord * halfChord + height * height) / (2f * height);

            for (int segment = 1; segment < ArcSegments; segment++)
            {
                float along = Mathf.Lerp(-halfChord, halfChord, (float)segment / ArcSegments);
                float offset = Mathf.Sqrt(radius * radius - along * along) - (radius - height);
                points.Add((from + to) * 0.5f + direction * along + outward * offset);
            }
        }
    }

    [MenuItem("Farm Land/Step 3 - Landform and Layout")]
    public static void BuildLandformAndLayout()
    {
        RequireEditMode();

        BuildTerrain();
        TerrainData data = LoadRequired<TerrainData>(TerrainDataPath);
        BuildLandform(data);
        PaintLayout(data);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log(DescribeLayout());
    }

    /// <summary>Generates the heightmap: rolling hills remapped to the target range, smoothed under fields.</summary>
    public static void BuildLandform(TerrainData data)
    {
        int size = data.heightmapResolution;
        float spacing = (float)MapSize / (size - 1);
        var random = new System.Random(LandformSeed);

        float[,] hills = LayeredNoise(size, spacing, random, (640f, 1f), (290f, 0.42f), (130f, 0.14f));
        RemapRange(hills, LandformLowest, LandformHighest);
        float[,] bumps = LayeredNoise(size, spacing, random, (38f, MeadowBumpHeight), (11f, MeadowFineBumpHeight));
        AddLocalRises(hills, spacing, random);

        var smoothHills = (float[,])hills.Clone();
        for (int pass = 0; pass < FieldSmoothingPasses; pass++)
            BlurClamped(smoothHills, FieldSmoothingRadius);

        float[,] fieldWeight = FieldSmoothingWeights(size, spacing);

        var heights = new float[size, size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                float meadow = hills[z, x] + bumps[z, x];
                heights[z, x] = Mathf.Lerp(meadow, smoothHills[z, x], fieldWeight[z, x]) / MapMaxHeight;
            }
        }

        data.SetHeights(0, 0, heights);
    }

    /// <summary>Paints the meadow everywhere, then each abandoned field with a soft, slightly uneven edge.</summary>
    public static void PaintLayout(TerrainData data)
    {
        int resolution = data.alphamapResolution;
        int layers = data.terrainLayers.Length;
        var alpha = new float[resolution, resolution, layers];
        float texelSize = (float)MapSize / resolution;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
                PaintMeadow(alpha, z, x, x * texelSize, z * texelSize);
        }

        float reach = FieldEdgeSoftness + FieldEdgeWobble + FieldEdgeFineWobble;
        foreach (FarmField field in Fields)
        {
            int minX = WorldToAlpha(field.Bounds.xMin - reach, resolution);
            int maxX = WorldToAlpha(field.Bounds.xMax + reach, resolution);
            int minZ = WorldToAlpha(field.Bounds.yMin - reach, resolution);
            int maxZ = WorldToAlpha(field.Bounds.yMax + reach, resolution);

            for (int z = minZ; z < maxZ; z++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    float worldX = x * texelSize;
                    float worldZ = z * texelSize;
                    float signedDistance = field.SignedDistance(new Vector2(worldX, worldZ));
                    float distance = signedDistance + FieldEdgeOffset(worldX, worldZ);
                    float coverage = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FieldEdgeSoftness, -FieldEdgeSoftness, distance));
                    if (coverage > 0f)
                        BlendFieldSurface(alpha, z, x, field, coverage, worldX, worldZ, signedDistance);
                }
            }
        }

        data.SetAlphamaps(0, 0, alpha);
    }

    /// <summary>Area, shape and furrow direction per field, plus totals and the smallest gap, for review.</summary>
    public static string DescribeLayout()
    {
        var report = new StringBuilder("Field | ha | sides | rows along | overgrowth | centre\n");
        float total = 0f;
        foreach (FarmField field in Fields)
        {
            total += field.AreaHectares;
            report.AppendLine($"{field.Name} | {field.AreaHectares:F1} | {field.SideCount} | {field.FurrowAxis} | {field.Overgrowth:F2} | {field.Centre.x:F0},{field.Centre.y:F0}");
        }

        report.AppendLine($"{Fields.Length} fields, {total:F1} ha of {MapSize * MapSize / 10000f:F0} ha");
        report.Append($"Smallest gap between fields: {SmallestFieldGap():F1} m");
        return report.ToString();
    }

    static void AddLocalRises(float[,] hills, float spacing, System.Random random)
    {
        int size = hills.GetLength(0);
        float offsetX = (float)random.NextDouble() * 1000f;
        float offsetZ = (float)random.NextDouble() * 1000f;
        float noiseScale = spacing / LocalRiseNoiseWavelength;

        foreach ((float riseX, float riseZ, float radius, float height) in LocalRises)
        {
            var centre = new Vector2(riseX, riseZ);
            int minX = Mathf.Max(0, Mathf.FloorToInt((riseX - radius) / spacing));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt((riseX + radius) / spacing));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((riseZ - radius) / spacing));
            int maxZ = Mathf.Min(size - 1, Mathf.CeilToInt((riseZ + radius) / spacing));

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x * spacing, z * spacing), centre);
                    float falloff = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / radius));
                    float share = Mathf.Lerp(LocalRiseMinimumShare, LocalRiseMaximumShare,
                        Mathf.PerlinNoise(x * noiseScale + offsetX, z * noiseScale + offsetZ));
                    hills[z, x] += height * falloff * share;
                }
            }
        }
    }

    static OutlinePoint P(float x, float z, float bulge = 0f)
    {
        return new OutlinePoint(new Vector2(x, z), bulge);
    }

    static float SmallestFieldGap()
    {
        float smallest = float.MaxValue;
        foreach (FarmField field in Fields)
        {
            foreach (FarmField other in Fields)
            {
                if (field == other)
                    continue;
                foreach (Vector2 corner in field.Outline)
                    smallest = Mathf.Min(smallest, other.SignedDistance(corner));
            }
        }
        return smallest;
    }

    // Keeps field edges from looking ruled, and adds a positive or negative offset in metres.
    static float FieldEdgeOffset(float worldX, float worldZ)
    {
        const float broadScale = 1f / 25f;
        const float fineScale = 1f / 6f;
        const float noiseOffset = 700f;
        float broad = Mathf.PerlinNoise(worldX * broadScale + noiseOffset, worldZ * broadScale + noiseOffset) * 2f - 1f;
        float fine = Mathf.PerlinNoise(worldX * fineScale + noiseOffset, worldZ * fineScale + noiseOffset) * 2f - 1f;
        return broad * FieldEdgeWobble + fine * FieldEdgeFineWobble;
    }

    // 1 inside a field, falling to 0 at the falloff distance outside.
    static float[,] FieldSmoothingWeights(int size, float spacing)
    {
        var weights = new float[size, size];
        foreach (FarmField field in Fields)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((field.Bounds.xMin - FieldSmoothingFalloff) / spacing));
            int maxX = Mathf.Min(size - 1, Mathf.CeilToInt((field.Bounds.xMax + FieldSmoothingFalloff) / spacing));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((field.Bounds.yMin - FieldSmoothingFalloff) / spacing));
            int maxZ = Mathf.Min(size - 1, Mathf.CeilToInt((field.Bounds.yMax + FieldSmoothingFalloff) / spacing));

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = field.SignedDistance(new Vector2(x * spacing, z * spacing));
                    float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FieldSmoothingFalloff, 0f, distance));
                    weights[z, x] = Mathf.Max(weights[z, x], weight);
                }
            }
        }
        return weights;
    }

    // Sums Perlin noise centred on zero. Each octave is a wavelength in metres and a peak height.
    static float[,] LayeredNoise(int size, float spacing, System.Random random, params (float wavelength, float amount)[] octaves)
    {
        var noise = new float[size, size];
        foreach ((float wavelength, float amount) in octaves)
        {
            float offsetX = (float)random.NextDouble() * 1000f;
            float offsetZ = (float)random.NextDouble() * 1000f;
            float scale = spacing / wavelength;
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                    noise[z, x] += (Mathf.PerlinNoise(x * scale + offsetX, z * scale + offsetZ) * 2f - 1f) * amount;
            }
        }
        return noise;
    }

    static void RemapRange(float[,] values, float lowest, float highest)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (float value in values)
        {
            min = Mathf.Min(min, value);
            max = Mathf.Max(max, value);
        }

        int size = values.GetLength(0);
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
                values[z, x] = Mathf.Lerp(lowest, highest, Mathf.InverseLerp(min, max, values[z, x]));
        }
    }

    // Box blur in both axes, using a running sum. Samples past the map edge repeat the edge value.
    static void BlurClamped(float[,] values, int radius)
    {
        int size = values.GetLength(0);
        var line = new float[size];
        float weight = 1f / (radius * 2 + 1);

        for (int z = 0; z < size; z++)
        {
            float sum = 0f;
            for (int offset = -radius; offset <= radius; offset++)
                sum += values[z, Mathf.Clamp(offset, 0, size - 1)];
            for (int x = 0; x < size; x++)
            {
                line[x] = sum * weight;
                sum += values[z, Mathf.Min(x + radius + 1, size - 1)] - values[z, Mathf.Max(x - radius, 0)];
            }
            for (int x = 0; x < size; x++)
                values[z, x] = line[x];
        }

        for (int x = 0; x < size; x++)
        {
            float sum = 0f;
            for (int offset = -radius; offset <= radius; offset++)
                sum += values[Mathf.Clamp(offset, 0, size - 1), x];
            for (int z = 0; z < size; z++)
            {
                line[z] = sum * weight;
                sum += values[Mathf.Min(z + radius + 1, size - 1), x] - values[Mathf.Max(z - radius, 0), x];
            }
            for (int z = 0; z < size; z++)
                values[z, x] = line[z];
        }
    }
}
