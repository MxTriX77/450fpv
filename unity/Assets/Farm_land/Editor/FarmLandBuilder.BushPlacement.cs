using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Step 12: places the Step 11 bushes as terrain instances alongside the trees, as part of Step 7. Bushes
/// gather in broken patches along field edges and tracks, at the foot of trees, in the overgrown fields and
/// among the ruins, with a few out in the meadows. The driest ground gets dry and dead bushes, using the same
/// noise as the ground paint.
/// </summary>
public static partial class FarmLandBuilder
{
    const int BushPlacementSeed = 1212;

    // Distances in metres. Bushes may touch each other, which makes clumps, but stay off trunks and tracks.
    const float BushSpacing = 2.4f;
    const float BushTreeClearance = 3.5f;
    const float BushTrackClearance = 1.5f;
    const float BushHouseClearance = 1.5f;
    const float BushMapMargin = 3f;

    // Field edges: patches of bushes just outside the outline, where the noise along the edge is high. Fields
    // with more overgrowth have longer patches, and bushes there can step a little into the field.
    const float BushEdgeStep = 3.2f;
    const float BushEdgeNoiseScale = 1f / 45f;
    const float BushEdgeThresholdBare = 0.62f;
    const float BushEdgeThresholdOvergrown = 0.45f;
    const float BushEdgeOffsetMin = 1.2f;
    const float BushEdgeOffsetMax = 4.5f;
    const float BushEdgeInsideMax = 3f;

    // Tracks: occasional patches beside them.
    const float BushTrackStep = 4f;
    const float BushTrackNoiseScale = 1f / 30f;
    const float BushTrackThreshold = 0.62f;
    const float BushTrackOffsetMin = 1.5f;
    const float BushTrackOffsetMax = 3.5f;

    // Trees: a share of the trees outside fields get a few bushes around their foot.
    const float BushTreeShare = 0.55f;
    const int BushesPerTreeMax = 3;
    const float BushTreeRingMax = 7f;

    // Overgrown fields: bushes on the weed patches, per hectare at full overgrowth.
    const float BushFieldDensity = 6f;
    const float BushFieldMinWeeds = 0.6f;

    // Village: bushes close to the ruined walls.
    const int VillageBushCount = 18;
    const float VillageBushRingMax = 6f;

    // Meadows: small clumps scattered across open ground.
    const int MeadowBushClumps = 45;
    const int MeadowClumpSizeMax = 3;
    const float MeadowClumpRadius = 4f;

    // Green bushes stand well clear of the grass, which reaches about a metre.
    const float MinBushScale = 0.9f;
    const float MaxBushScale = 1.5f;

    // The large leafy bush is 5 m across at full size, so it's kept smaller.
    const float LeafyBushScale = 0.8f;

    // Dead bushes are dark and bare, so they're kept smaller than the rest and stand out less.
    const float MinDeadBushScale = 0.55f;
    const float MaxDeadBushScale = 0.85f;

    // Shares of bushes, ranked by how dry the ground is: the driest are dead, the next are dry.
    const float DeadBushShare = 0.05f;
    const float DryBushShare = 0.35f;
    const float DryBushJitter = 0.15f;

    // Of the dry bushes, this share become a dry-only kind rather than a dry copy of their green bush.
    const float DryOnlyBushShare = 0.35f;

    static readonly string[] GreenBushes = { "GreenBush", "GreenBush", "LeafyBush_P2" };
    static readonly Dictionary<string, string> DryBushCopies = new Dictionary<string, string>
    {
        ["GreenBush"] = "GreenBush_Dry",
        ["LeafyBush_P2"] = "LeafyBush_P2_Dry",
    };
    static readonly string[] DryOnlyBushes = { "RedBush_Dry", "SmallBushDry_A", "SmallBushDry_B" };
    static readonly string[] DeadBushes = { "DeadBush_4", "DeadBush_4", "DeadBush_4", "DeadBush_6" };

    /// <summary>Adds bushes to the placed trees, keeping clear of them. Part of Step 7.</summary>
    static void PlaceBushes(List<PlacedTree> trees)
    {
        var bushes = new List<PlacedTree>();
        int group = 0;

        foreach (FarmField field in Fields)
            PlaceFieldEdgeBushes(bushes, trees, field, new System.Random(BushPlacementSeed + group++));

        foreach (FarmTrack track in Tracks)
            PlaceTrackBushes(bushes, trees, track, new System.Random(BushPlacementSeed + group++));

        PlaceTreeFootBushes(bushes, trees, new System.Random(BushPlacementSeed + group++));

        foreach (FarmField field in Fields)
            PlaceOvergrownFieldBushes(bushes, trees, field, new System.Random(BushPlacementSeed + group++));

        PlaceVillageBushes(bushes, trees, new System.Random(BushPlacementSeed + group++));
        PlaceMeadowBushes(bushes, trees, new System.Random(BushPlacementSeed + group++));

        MakeDryBushes(bushes);
        trees.AddRange(bushes);
    }

    static void PlaceFieldEdgeBushes(List<PlacedTree> bushes, List<PlacedTree> trees, FarmField field, System.Random random)
    {
        float threshold = Mathf.Lerp(BushEdgeThresholdBare, BushEdgeThresholdOvergrown, field.Overgrowth);
        foreach (Vector2 point in AlongPath(field.Outline, BushEdgeStep, true))
        {
            float noise = Mathf.PerlinNoise(point.x * BushEdgeNoiseScale + 210f, point.y * BushEdgeNoiseScale + 210f);
            if (noise < threshold)
                continue;

            // Outward from the field, or a little inside it on weeds when it's overgrown.
            float inside = BushEdgeInsideMax * field.Overgrowth;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 spot = point + RandomOffset(random, BushEdgeOffsetMax);
                float signed = field.SignedDistance(spot);
                bool outside = signed >= BushEdgeOffsetMin && signed <= BushEdgeOffsetMax && NearestFieldEdge(spot) >= BushEdgeOffsetMin;
                bool onWeeds = signed < 0f && -signed <= inside && field.WeedShare(spot.x, spot.y, signed) >= BushFieldMinWeeds;
                if ((outside || onWeeds) && TryAddBush(bushes, trees, spot, random, onWeeds))
                    break;
            }
        }
    }

    static void PlaceTrackBushes(List<PlacedTree> bushes, List<PlacedTree> trees, FarmTrack track, System.Random random)
    {
        foreach (Vector2 point in AlongPath(track.Path, BushTrackStep, false))
        {
            float noise = Mathf.PerlinNoise(point.x * BushTrackNoiseScale + 530f, point.y * BushTrackNoiseScale + 530f);
            if (noise < BushTrackThreshold)
                continue;

            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            Vector2 direction = AlongDirection(track.Path, point);
            Vector2 sideways = new Vector2(-direction.y, direction.x) * side;
            float offset = track.HalfWidth + RandomRange(random, BushTrackOffsetMin, BushTrackOffsetMax);
            Vector2 spot = point + sideways * offset + direction * RandomRange(random, -1f, 1f);
            if (NearestFieldEdge(spot) >= BushEdgeOffsetMin)
                TryAddBush(bushes, trees, spot, random, false);
        }
    }

    static void PlaceTreeFootBushes(List<PlacedTree> bushes, List<PlacedTree> trees, System.Random random)
    {
        foreach (PlacedTree tree in trees.ToList())
        {
            if (tree.InField || random.NextDouble() > BushTreeShare)
                continue;
            int count = random.Next(1, BushesPerTreeMax + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 spot = tree.Position + RandomOffset(random, BushTreeRingMax);
                if (NearestFieldEdge(spot) >= BushEdgeOffsetMin)
                    TryAddBush(bushes, trees, spot, random, false);
            }
        }
    }

    static void PlaceOvergrownFieldBushes(List<PlacedTree> bushes, List<PlacedTree> trees, FarmField field, System.Random random)
    {
        int count = Mathf.RoundToInt(BushFieldDensity * field.Overgrowth * field.AreaHectares);
        for (int i = 0, attempts = 0; i < count && attempts < count * 20; attempts++)
        {
            var spot = new Vector2(RandomRange(random, field.Bounds.xMin, field.Bounds.xMax), RandomRange(random, field.Bounds.yMin, field.Bounds.yMax));
            float signed = field.SignedDistance(spot);
            if (signed >= 0f || field.WeedShare(spot.x, spot.y, signed) < BushFieldMinWeeds)
                continue;
            if (TryAddBush(bushes, trees, spot, random, true))
                i++;
        }
    }

    static void PlaceVillageBushes(List<PlacedTree> bushes, List<PlacedTree> trees, System.Random random)
    {
        for (int i = 0, attempts = 0; i < VillageBushCount && attempts < VillageBushCount * 30; attempts++)
        {
            var house = VillageHouses[random.Next(VillageHouses.Length)];
            Vector2 spot = new Vector2(house.x, house.z) + RandomOffset(random, HouseFootprint.size.magnitude * 0.5f + VillageBushRingMax);
            if (NearestHouseEdge(spot) > VillageBushRingMax || NearestFieldEdge(spot) < BushEdgeOffsetMin)
                continue;
            if (TryAddBush(bushes, trees, spot, random, false))
                i++;
        }
    }

    static void PlaceMeadowBushes(List<PlacedTree> bushes, List<PlacedTree> trees, System.Random random)
    {
        for (int i = 0, attempts = 0; i < MeadowBushClumps && attempts < MeadowBushClumps * 30; attempts++)
        {
            var centre = new Vector2(RandomRange(random, 0f, MapSize), RandomRange(random, 0f, MapSize));
            if (NearestFieldEdge(centre) < 8f || NearestTrackEdge(centre) < 6f || InVillage(centre))
                continue;
            if (!TryAddBush(bushes, trees, centre, random, false))
                continue;
            i++;
            int extra = random.Next(0, MeadowClumpSizeMax);
            for (int k = 0; k < extra; k++)
                TryAddBush(bushes, trees, centre + RandomOffset(random, MeadowClumpRadius), random, false);
        }
    }

    // Adds a green bush if the spot is clear. Field bushes skip the field clearance, since they were tested
    // against the weeds already.
    static bool TryAddBush(List<PlacedTree> bushes, List<PlacedTree> trees, Vector2 spot, System.Random random, bool inField)
    {
        if (!BushSpotIsClear(spot, bushes, trees, inField))
            return false;

        string prefab = GreenBushes[random.Next(GreenBushes.Length)];
        float scale = RandomRange(random, MinBushScale, MaxBushScale) * (prefab == "LeafyBush_P2" ? LeafyBushScale : 1f);
        bushes.Add(new PlacedTree { Position = spot, Prefab = prefab, Scale = scale, InField = inField, Bush = true });
        return true;
    }

    static bool BushSpotIsClear(Vector2 spot, List<PlacedTree> bushes, List<PlacedTree> trees, bool inField)
    {
        if (spot.x < BushMapMargin || spot.y < BushMapMargin || spot.x > MapSize - BushMapMargin || spot.y > MapSize - BushMapMargin)
            return false;
        if (NearestTrackEdge(spot) < BushTrackClearance || NearestHouseEdge(spot) < BushHouseClearance)
            return false;
        if (!inField && NearestFieldEdge(spot) < 0f)
            return false;
        foreach (PlacedTree tree in trees)
        {
            if (!tree.Bush && Vector2.Distance(tree.Position, spot) < BushTreeClearance)
                return false;
        }
        foreach (PlacedTree bush in bushes)
        {
            if (Vector2.Distance(bush.Position, spot) < BushSpacing)
                return false;
        }
        return true;
    }

    /// <summary>Ranks bushes by how dry the ground is, like the trees. The driest die, and the next dry out.</summary>
    static void MakeDryBushes(List<PlacedTree> bushes)
    {
        var random = new System.Random(BushPlacementSeed);
        foreach (PlacedTree bush in bushes)
        {
            float x = bush.Position.x;
            float z = bush.Position.y;
            bush.Dryness = (MeadowDryness(x, z) + BurntShare(x, z)) * 0.5f + RandomRange(random, 0f, DryBushJitter);
            if (InVillage(bush.Position))
                bush.Dryness += VillageDryBoost;
        }

        List<PlacedTree> ranked = bushes.OrderByDescending(bush => bush.Dryness).ToList();
        int deadCount = Mathf.RoundToInt(bushes.Count * DeadBushShare);
        int dryCount = Mathf.RoundToInt(bushes.Count * DryBushShare);
        for (int i = 0; i < dryCount && i < ranked.Count; i++)
        {
            PlacedTree bush = ranked[i];
            if (i < deadCount)
            {
                bush.Prefab = DeadBushes[random.Next(DeadBushes.Length)];
                bush.Scale = RandomRange(random, MinDeadBushScale, MaxDeadBushScale);
                continue;
            }
            if (random.NextDouble() < DryOnlyBushShare)
                bush.Prefab = DryOnlyBushes[random.Next(DryOnlyBushes.Length)];
            else
                bush.Prefab = DryBushCopies[bush.Prefab];

            // Dry-only kinds aren't copies of the green bush they replace, so they get their own size.
            if (!DryBushCopies.ContainsValue(bush.Prefab))
                bush.Scale = RandomRange(random, MinBushScale, MaxBushScale);
        }
    }

    // Points every step metres along a path, closed back to its start when asked.
    static IEnumerable<Vector2> AlongPath(Vector2[] path, float step, bool closed)
    {
        int segments = closed ? path.Length : path.Length - 1;
        float carry = 0f;
        for (int i = 0; i < segments; i++)
        {
            Vector2 from = path[i];
            Vector2 to = path[(i + 1) % path.Length];
            float length = Vector2.Distance(from, to);
            for (float d = carry; d < length; d += step)
                yield return Vector2.Lerp(from, to, d / length);
            carry = Mathf.Repeat(carry - length, step);
        }
    }

    // Direction of the path's segment nearest the point.
    static Vector2 AlongDirection(Vector2[] path, Vector2 point)
    {
        float nearest = float.MaxValue;
        Vector2 direction = Vector2.right;
        for (int i = 1; i < path.Length; i++)
        {
            float distance = SegmentDistance(point, path[i - 1], path[i]);
            if (distance < nearest && path[i] != path[i - 1])
            {
                nearest = distance;
                direction = (path[i] - path[i - 1]).normalized;
            }
        }
        return direction;
    }

    static Vector2 RandomOffset(System.Random random, float radius)
    {
        float angle = RandomRange(random, 0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * Mathf.Sqrt((float)random.NextDouble());
    }

    static string DescribeBushes(List<PlacedTree> bushes)
    {
        var report = new StringBuilder("Bush | count\n");
        foreach (IGrouping<string, PlacedTree> kind in bushes.GroupBy(bush => bush.Prefab).OrderBy(kind => kind.Key, System.StringComparer.Ordinal))
            report.AppendLine($"{kind.Key} | {kind.Count()}");
        int dry = bushes.Count(bush => !GreenBushes.Contains(bush.Prefab));
        report.Append($"{bushes.Count} bushes, {bushes.Count(bush => bush.InField)} in fields, {dry} dry or dead ({dry * 100 / Mathf.Max(1, bushes.Count)}%)");
        return report.ToString();
    }
}
