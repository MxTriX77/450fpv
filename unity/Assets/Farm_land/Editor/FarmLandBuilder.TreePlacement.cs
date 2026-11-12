using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Step 7: places the Step 6 trees as terrain instances. Groups are authored below so they can be moved at
/// review, and a seed varies the trees inside them. Only tree instances change on the terrain. Step 12 adds
/// the bushes here too, since they're terrain instances on the same list and keep clear of the trees.
/// </summary>
public static partial class FarmLandBuilder
{
    const int TreePlacementSeed = 7411;
    const string TreeLineupObjectName = "Tree Lineup";

    // Distances from a trunk, in metres. Field edges wobble by up to 1.7 m past their outline.
    const float TreeFieldClearance = 4f;
    const float TreeTrackClearance = 3f;
    const float TreeMapMargin = 6f;
    const float TreeSpacing = 8f;

    // How far a tree can move from where a line or lone tree asks for it, if that spot isn't clear.
    const float TreeSearchRadius = 8f;
    const int TreeSearchAttempts = 40;

    const float TreeLineSetback = 5f;
    const float TreeLineSetbackJitter = 2f;
    const float TreeLineSpacingJitter = 0.2f;

    const float MinTreeScale = 0.85f;
    const float MaxTreeScale = 1.2f;
    const float TreeWidthVariation = 0.08f;

    // Share of trees that are dry, ranked by how dry the ground is. The jitter keeps a small area from
    // turning all one colour at the edge of the dry ground.
    const float DryTreeShare = 0.25f;
    const float DryTreeJitter = 0.15f;

    // Bare pines stay at 11k triangles when far away, so there are only a few, kept apart.
    const int DeadPineCount = 5;
    const float DeadPineSpacing = 100f;
    const string DeadPinePrefabName = "PineDead_02";

    // Young trees moving into overgrown fields. Thickets keep their older, taller trees near the middle and
    // younger ones at the fringe, and only start where the field has gone to weeds.
    const float FieldTreeEdgeInset = 3f;
    const float FieldTreeSpacing = 5f;
    const float FieldThicketRadius = 16f;
    const float FieldThicketMaxDepth = 60f;
    const float FieldTreeMinWeeds = 0.5f;
    const float MinFieldTreeScale = 0.5f;
    const float MaxFieldTreeScale = 0.95f;
    const float FieldTreeScaleJitter = 0.1f;

    enum TreeMix
    {
        Island,
        Pine002,
        Pine004,
        Young,
    }

    static readonly Dictionary<TreeMix, string[]> TreeMixPrefabs = new Dictionary<TreeMix, string[]>
    {
        [TreeMix.Island] = new[] { IslandPrefabName },
        [TreeMix.Pine002] = new[] { "Pine002_S2", "Pine002_M", "Pine002_M", "Pine002_L" },
        [TreeMix.Pine004] = new[] { "Pine004_1", "Pine004_3", "Pine006_01", "Pine006_02" },
        [TreeMix.Young] = new[] { IslandPrefabName, IslandPrefabName, IslandPrefabName, "Pine006_01", "Pine006_02" },
    };

    // Each green tree's dry copy, with a scale that keeps it about as tall as the tree it replaces.
    static readonly Dictionary<string, (string prefab, float scale)> DryTreeCopies = new Dictionary<string, (string, float)>
    {
        [IslandPrefabName] = (IslandDryPrefabName, 1f),
        ["Pine002_S2"] = ("Pine002_M_Dry", 0.6f),
        ["Pine002_M"] = ("Pine002_M_Dry", 1f),
        ["Pine002_L"] = ("Pine002_M_Dry", 1.5f),
        ["Pine004_1"] = ("Pine004_1_Dry", 1f),
        ["Pine004_3"] = ("Pine004_1_Dry", 1f),
        ["Pine006_01"] = ("Pine006_01_Dry", 1f),
        ["Pine006_02"] = ("Pine006_01_Dry", 1.2f),
    };

    /// <summary>Small clusters of island trees just outside field corners, and pine groups in the outer meadows and on the rises.</summary>
    static readonly (string name, float x, float z, float radius, int count, TreeMix mix)[] TreeClusters =
    {
        ("F1 south-west", 22f, 24f, 14f, 4, TreeMix.Island),
        ("F5 north-west", 392f, 196f, 12f, 4, TreeMix.Island),
        ("F4 west", 332f, 272f, 10f, 3, TreeMix.Island),
        ("F11 south-west", 632f, 32f, 14f, 5, TreeMix.Island),
        ("F11 north-east", 882f, 268f, 12f, 4, TreeMix.Island),
        ("F10 north-west", 608f, 342f, 12f, 5, TreeMix.Island),
        ("F10 east", 950f, 480f, 14f, 5, TreeMix.Island),
        ("F8 north-east", 748f, 962f, 13f, 4, TreeMix.Island),
        ("F9 north-east", 938f, 888f, 14f, 5, TreeMix.Island),
        ("F7 north-west", 168f, 936f, 16f, 5, TreeMix.Island),
        ("F6 north-east", 496f, 700f, 12f, 4, TreeMix.Island),
        ("F6 south", 215f, 378f, 14f, 4, TreeMix.Island),

        ("North-west rise", 68f, 945f, 22f, 8, TreeMix.Pine002),
        ("East rise", 948f, 235f, 22f, 8, TreeMix.Pine004),
        ("North-east rise", 900f, 952f, 20f, 7, TreeMix.Pine002),
        ("West burnt band", 40f, 335f, 18f, 6, TreeMix.Pine004),
        ("South meadow", 333f, 70f, 16f, 5, TreeMix.Pine002),
        ("South-east edge", 965f, 95f, 15f, 5, TreeMix.Pine004),
    };

    /// <summary>
    /// Short lines along tracks. From and to are shares of the track's length, and side is the side of the
    /// track looking along its path, +1 for left and -1 for right.
    /// </summary>
    static readonly (string track, float from, float to, int count, float side)[] TreeLines =
    {
        ("T1 diagonal", 0.03f, 0.18f, 6, 1f),
        ("T2 north", 0.24f, 0.46f, 5, 1f),
        ("T3 south", 0.18f, 0.38f, 5, 1f),
        ("T4 east", 0.16f, 0.38f, 5, 1f),
        ("T5 north-east", 0.3f, 0.52f, 5, 1f),
        ("T6 south-east", 0.44f, 0.66f, 5, 1f),
        ("T7 west loop", 0.15f, 0.26f, 5, 1f),
        ("T7 west loop", 0.6f, 0.7f, 4, -1f),
        ("T8 strips", 0.55f, 0.8f, 5, 1f),
        ("T9 link", 0.22f, 0.6f, 5, 1f),
    };

    /// <summary>Lone trees in the meadows.</summary>
    static readonly (float x, float z)[] LoneTrees =
    {
        (40f, 700f), (28f, 520f), (45f, 822f), (20f, 150f), (345f, 190f), (625f, 150f),
        (508f, 625f), (605f, 470f), (765f, 655f), (865f, 635f), (965f, 395f), (985f, 165f),
        (780f, 325f), (965f, 690f), (845f, 965f), (610f, 985f), (345f, 985f), (250f, 385f),
    };

    /// <summary>
    /// Trees growing into the most overgrown fields, as thickets spreading in from the edge plus scattered
    /// saplings on the weed patches. Furrowed fields stay clear.
    /// </summary>
    static readonly (string field, int thickets, int perThicket, int scattered)[] FieldTrees =
    {
        ("F9", 2, 8, 5),
        ("F4", 3, 9, 6),
        ("F8", 3, 7, 5),
        ("F11", 2, 8, 4),
        ("F2", 1, 8, 3),
        ("F6", 2, 8, 4),
    };

    /// <summary>A few trees among the Step 8 houses, mostly dying back.</summary>
    static readonly (float x, float z, float radius, int count)[] VillageTrees =
    {
        (512f, 600f, 40f, 6),
    };

    sealed class PlacedTree
    {
        public Vector2 Position;
        public string Prefab;
        public float Scale;
        public float Dryness;
        public bool InField;
        public bool Bush;
    }

    [MenuItem("Farm Land/Step 7 - Place Trees")]
    public static void BuildPlaceTrees()
    {
        RequireEditMode();
        Scene scene = OpenFarmLandScene();

        foreach (string lineupName in new[] { TreeLineupObjectName, BushLineupObjectName })
        {
            GameObject lineup = GameObject.Find(lineupName);
            if (lineup != null)
                Object.DestroyImmediate(lineup);
        }

        var unplaced = new List<Vector2>();
        List<PlacedTree> trees = PlaceTrees(unplaced);
        MakeDryTrees(trees);
        PlaceBushes(trees);

        TerrainData data = LoadRequired<TerrainData>(TerrainDataPath);
        ApplyTreesToTerrain(data, trees);

        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(DescribeTrees(trees.Where(tree => !tree.Bush).ToList(), unplaced));
        Debug.Log(DescribeBushes(trees.Where(tree => tree.Bush).ToList()));
    }

    static List<PlacedTree> PlaceTrees(List<Vector2> unplaced)
    {
        var trees = new List<PlacedTree>();

        // Each group has its own random sequence, so moving one doesn't reshuffle the others.
        int group = 0;
        foreach (var cluster in TreeClusters)
        {
            var random = new System.Random(TreePlacementSeed + group++);
            for (int i = 0; i < cluster.count; i++)
                TryPlaceTree(trees, unplaced, cluster.mix, new Vector2(cluster.x, cluster.z), cluster.radius, random, false);
        }

        foreach (var line in TreeLines)
        {
            var random = new System.Random(TreePlacementSeed + group++);
            FarmTrack track = Tracks.First(candidate => candidate.Name == line.track);
            for (int i = 0; i < line.count; i++)
            {
                float step = (line.to - line.from) / Mathf.Max(1, line.count - 1);
                float along = line.from + step * (i + RandomRange(random, -TreeLineSpacingJitter, TreeLineSpacingJitter));
                float setback = track.HalfWidth + TreeLineSetback + RandomRange(random, -TreeLineSetbackJitter, TreeLineSetbackJitter);
                TryPlaceTree(trees, unplaced, TreeMix.Island, BesideTrack(track, along, line.side * setback), TreeSearchRadius, random, true);
            }
        }

        foreach ((float x, float z) in LoneTrees)
        {
            var random = new System.Random(TreePlacementSeed + group++);
            TryPlaceTree(trees, unplaced, TreeMix.Island, new Vector2(x, z), TreeSearchRadius, random, true);
        }

        foreach (var entry in FieldTrees)
        {
            var random = new System.Random(TreePlacementSeed + group++);
            PlaceFieldTrees(trees, unplaced, Fields.First(field => field.Name == entry.field), entry.thickets, entry.perThicket, entry.scattered, random);
        }

        foreach (var cluster in VillageTrees)
        {
            var random = new System.Random(TreePlacementSeed + group++);
            for (int i = 0; i < cluster.count; i++)
                TryPlaceTree(trees, unplaced, TreeMix.Island, new Vector2(cluster.x, cluster.z), cluster.radius, random, false);
        }

        return trees;
    }

    static void PlaceFieldTrees(List<PlacedTree> trees, List<Vector2> unplaced, FarmField field, int thickets, int perThicket, int scattered, System.Random random)
    {
        for (int t = 0; t < thickets; t++)
        {
            // Thickets start from the edge, where seed blows in from the meadow, on ground that's gone to weeds.
            Vector2? centre = RandomFieldSpot(field, random, spot =>
                -field.SignedDistance(spot) < FieldThicketMaxDepth && FieldTreeSpotIsClear(field, spot, trees, FieldThicketRadius));
            if (centre == null)
            {
                unplaced.Add(field.Centre);
                continue;
            }

            for (int i = 0; i < perThicket; i++)
            {
                Vector2? spot = null;
                float distance = 0f;
                for (int attempt = 0; attempt < TreeSearchAttempts && spot == null; attempt++)
                {
                    float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                    distance = i == 0 ? 0f : FieldThicketRadius * Mathf.Sqrt((float)random.NextDouble());
                    Vector2 candidate = centre.Value + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    if (FieldTreeSpotIsClear(field, candidate, trees, FieldTreeSpacing))
                        spot = candidate;
                }
                if (spot == null)
                {
                    unplaced.Add(centre.Value);
                    continue;
                }
                float age = 1f - distance / FieldThicketRadius;
                AddFieldTree(trees, spot.Value, age, random);
            }
        }

        for (int i = 0; i < scattered; i++)
        {
            Vector2? spot = RandomFieldSpot(field, random, candidate => FieldTreeSpotIsClear(field, candidate, trees, FieldTreeSpacing * 2f));
            if (spot == null)
                unplaced.Add(field.Centre);
            else
                AddFieldTree(trees, spot.Value, RandomRange(random, 0f, 0.4f), random);
        }
    }

    static void AddFieldTree(List<PlacedTree> trees, Vector2 spot, float age, System.Random random)
    {
        string[] prefabs = TreeMixPrefabs[TreeMix.Young];
        float scale = Mathf.Lerp(MinFieldTreeScale, MaxFieldTreeScale, age) + RandomRange(random, -FieldTreeScaleJitter, FieldTreeScaleJitter);
        trees.Add(new PlacedTree
        {
            Position = spot,
            Prefab = prefabs[random.Next(prefabs.Length)],
            Scale = Mathf.Clamp(scale, MinFieldTreeScale, MaxFieldTreeScale),
            InField = true,
        });
    }

    // A random spot inside the field's bounds that passes the test, or null.
    static Vector2? RandomFieldSpot(FarmField field, System.Random random, System.Func<Vector2, bool> test)
    {
        Rect bounds = field.Bounds;
        for (int attempt = 0; attempt < TreeSearchAttempts * 10; attempt++)
        {
            var spot = new Vector2(RandomRange(random, bounds.xMin, bounds.xMax), RandomRange(random, bounds.yMin, bounds.yMax));
            if (test(spot))
                return spot;
        }
        return null;
    }

    // Inside the field on weeds, clear of tracks and of other trees by the given distance.
    static bool FieldTreeSpotIsClear(FarmField field, Vector2 spot, List<PlacedTree> trees, float spacing)
    {
        float signed = field.SignedDistance(spot);
        if (signed > -FieldTreeEdgeInset || NearestTrackEdge(spot) < TreeTrackClearance || NearestHouseEdge(spot) < HouseTreeClearance)
            return false;
        if (field.WeedShare(spot.x, spot.y, signed) < FieldTreeMinWeeds)
            return false;

        foreach (PlacedTree tree in trees)
        {
            if (Vector2.Distance(tree.Position, spot) < spacing)
                return false;
        }
        return true;
    }

    // Tries the exact spot first if asked, then random spots around it.
    static void TryPlaceTree(List<PlacedTree> trees, List<Vector2> unplaced, TreeMix mix, Vector2 centre, float radius, System.Random random, bool tryCentre)
    {
        for (int attempt = 0; attempt < TreeSearchAttempts; attempt++)
        {
            Vector2 spot = centre;
            if (attempt > 0 || !tryCentre)
            {
                float angle = RandomRange(random, 0f, Mathf.PI * 2f);
                float distance = radius * Mathf.Sqrt((float)random.NextDouble());
                spot += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            }

            if (!TreeSpotIsClear(spot, trees))
                continue;

            string[] prefabs = TreeMixPrefabs[mix];
            trees.Add(new PlacedTree
            {
                Position = spot,
                Prefab = prefabs[random.Next(prefabs.Length)],
                Scale = RandomRange(random, MinTreeScale, MaxTreeScale),
            });
            return;
        }
        unplaced.Add(centre);
    }

    static bool TreeSpotIsClear(Vector2 spot, List<PlacedTree> trees)
    {
        if (spot.x < TreeMapMargin || spot.y < TreeMapMargin || spot.x > MapSize - TreeMapMargin || spot.y > MapSize - TreeMapMargin)
            return false;
        if (NearestFieldEdge(spot) < TreeFieldClearance || NearestTrackEdge(spot) < TreeTrackClearance || NearestHouseEdge(spot) < HouseTreeClearance)
            return false;

        foreach (PlacedTree tree in trees)
        {
            if (Vector2.Distance(tree.Position, spot) < TreeSpacing)
                return false;
        }
        return true;
    }

    // Negative inside a field.
    static float NearestFieldEdge(Vector2 spot)
    {
        float nearest = float.MaxValue;
        foreach (FarmField field in Fields)
            nearest = Mathf.Min(nearest, field.SignedDistance(spot));
        return nearest;
    }

    // Negative on a track.
    static float NearestTrackEdge(Vector2 spot)
    {
        float nearest = float.MaxValue;
        foreach (FarmTrack track in Tracks)
        {
            for (int i = 1; i < track.Path.Length; i++)
                nearest = Mathf.Min(nearest, SegmentDistance(spot, track.Path[i - 1], track.Path[i]) - track.HalfWidth);
        }
        return nearest;
    }

    // A point at a share of the track's length, moved sideways, to the left of its path when positive.
    static Vector2 BesideTrack(FarmTrack track, float share, float sideways)
    {
        float remaining = Mathf.Clamp01(share) * track.Length;
        for (int i = 1; i < track.Path.Length; i++)
        {
            Vector2 segment = track.Path[i] - track.Path[i - 1];
            float length = segment.magnitude;
            if (remaining <= length || i == track.Path.Length - 1)
            {
                Vector2 direction = segment / length;
                return track.Path[i - 1] + direction * remaining + new Vector2(-direction.y, direction.x) * sideways;
            }
            remaining -= length;
        }
        return track.Path[0];
    }

    /// <summary>Turns the trees on the driest ground dry, using the same noise as the ground paint, and a few of them into dead pines.</summary>
    static void MakeDryTrees(List<PlacedTree> trees, bool deadPines = true)
    {
        var random = new System.Random(TreePlacementSeed);
        foreach (PlacedTree tree in trees)
        {
            float x = tree.Position.x;
            float z = tree.Position.y;
            tree.Dryness = (MeadowDryness(x, z) + BurntShare(x, z)) * 0.5f + RandomRange(random, 0f, DryTreeJitter);
            if (InVillage(tree.Position))
                tree.Dryness += VillageDryBoost;
        }

        int dryCount = Mathf.RoundToInt(trees.Count * DryTreeShare);
        List<PlacedTree> dry = trees.OrderByDescending(tree => tree.Dryness).Take(dryCount).ToList();

        foreach (PlacedTree tree in dry)
        {
            (string prefab, float scale) = DryTreeCopies[tree.Prefab];
            tree.Prefab = prefab;
            tree.Scale *= scale;
        }

        var dead = new List<PlacedTree>();
        foreach (PlacedTree tree in dry)
        {
            if (!deadPines || dead.Count == DeadPineCount)
                break;
            if (tree.InField)
                continue;
            if (dead.All(other => Vector2.Distance(other.Position, tree.Position) >= DeadPineSpacing))
                dead.Add(tree);
        }
        foreach (PlacedTree tree in dead)
        {
            tree.Prefab = DeadPinePrefabName;
            tree.Scale = RandomRange(random, MinTreeScale, MaxTreeScale);
        }
    }

    static void ApplyTreesToTerrain(TerrainData data, List<PlacedTree> trees)
    {
        List<string> prefabs = trees.Select(tree => tree.Prefab).Distinct().OrderBy(name => name, System.StringComparer.Ordinal).ToList();
        var bushPrefabs = new HashSet<string>(trees.Where(tree => tree.Bush).Select(tree => tree.Prefab));
        data.treePrototypes = prefabs
            .Select(name => new TreePrototype { prefab = LoadRequired<GameObject>($"{(bushPrefabs.Contains(name) ? BushesFolder : TreesFolder)}/{name}.prefab") })
            .ToArray();

        var random = new System.Random(TreePlacementSeed);
        var instances = new TreeInstance[trees.Count];
        for (int i = 0; i < instances.Length; i++)
        {
            PlacedTree tree = trees[i];
            instances[i] = new TreeInstance
            {
                position = new Vector3(tree.Position.x / MapSize, 0f, tree.Position.y / MapSize),
                prototypeIndex = prefabs.IndexOf(tree.Prefab),
                heightScale = tree.Scale,
                widthScale = tree.Scale * RandomRange(random, 1f - TreeWidthVariation, 1f + TreeWidthVariation),
                rotation = RandomRange(random, 0f, Mathf.PI * 2f),
                color = Color.white,
                lightmapColor = Color.white,
            };
        }
        data.SetTreeInstances(instances, true);
    }

    static string DescribeTrees(List<PlacedTree> trees, List<Vector2> unplaced)
    {
        var report = new StringBuilder("Tree | count\n");
        foreach (IGrouping<string, PlacedTree> kind in trees.GroupBy(tree => tree.Prefab).OrderBy(kind => kind.Key, System.StringComparer.Ordinal))
            report.AppendLine($"{kind.Key} | {kind.Count()}");

        int dry = trees.Count(tree => tree.Prefab.EndsWith("_Dry") || tree.Prefab == DeadPinePrefabName);
        int island = trees.Count(tree => tree.Prefab.StartsWith(IslandPrefabName));
        report.AppendLine($"{trees.Count} trees, {island} island ({island * 100 / trees.Count}%), {dry} dry or dead ({dry * 100 / trees.Count}%)");
        report.AppendLine($"Couldn't place {unplaced.Count}: {DescribeSpots(unplaced)}");
        foreach (var entry in FieldTrees)
        {
            FarmField field = Fields.First(candidate => candidate.Name == entry.field);
            int count = trees.Count(tree => tree.InField && field.SignedDistance(tree.Position) < 0f);
            report.AppendLine($"{entry.field}: {count} trees in field ({count / field.AreaHectares:F1} per ha)");
        }
        report.AppendLine($"Smallest clearance outside fields: {trees.Where(tree => !tree.InField).Min(tree => NearestFieldEdge(tree.Position)):F1} m to a field, {trees.Min(tree => NearestTrackEdge(tree.Position)):F1} m to a track");
        report.Append($"Dead pines at: {DescribeSpots(trees.Where(tree => tree.Prefab == DeadPinePrefabName).Select(tree => tree.Position))}");
        return report.ToString();
    }

    static string DescribeSpots(IEnumerable<Vector2> spots)
    {
        return string.Join(", ", spots.Select(spot => $"({spot.x:F0}, {spot.y:F0})"));
    }

    static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
