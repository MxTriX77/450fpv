using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

/// The object scenarios of `-- --selftest worldquery`: the wind grid and gaps (task 3.3) and static contacts, raycasts
/// and runtime objects (task 3.4), on game/maps/sample_patch and on flat in-memory worlds. The references are written
/// here from the definitions (rotation matrices from Math.Sin/Cos, textbook signed distances, dense sampling, sphere
/// tracing, the renderer's barycentric triangles), not taken from the code under test.
public static partial class WorldQuerySelfTest
{
    /// Test assets added to the real catalog: one of each primitive (the cylinder overrides its asset's material) and a
    /// carport-like shed whose sloped sheet-metal roof overrides the timber of its walls.
    const string TestAssets = """
    {
      "t_box": { "type": "object", "scene": "res://x", "material": "timber", "snag_hazard": false, "wind_porosity": 0.0, "gaps": [],
        "collision": [ { "shape": "box", "size_m": [0.8, 0.3, 1.2], "position_m": [0.1, 0.2, -0.1], "rotation_deg": [20, 10, -5] } ] },
      "t_sphere": { "type": "object", "scene": "res://x", "material": "steel", "snag_hazard": false, "wind_porosity": 0.0, "gaps": [],
        "collision": [ { "shape": "sphere", "radius_m": 0.4, "position_m": [0, 0.5, 0] } ] },
      "t_capsule": { "type": "object", "scene": "res://x", "material": "masonry", "snag_hazard": false, "wind_porosity": 0.0, "gaps": [],
        "collision": [ { "shape": "capsule", "radius_m": 0.15, "height_m": 1.0, "position_m": [0, 0.6, 0], "rotation_deg": [0, 30, 45] } ] },
      "t_cylinder": { "type": "object", "scene": "res://x", "material": "timber", "snag_hazard": false, "wind_porosity": 0.0, "gaps": [],
        "collision": [ { "shape": "cylinder", "radius_m": 0.25, "height_m": 0.9, "position_m": [0.05, 0.5, 0], "rotation_deg": [10, 60, 0],
          "material": "sheet_metal" } ] },
      "test_tin_shed": { "type": "object", "scene": "res://x", "material": "timber", "snag_hazard": false, "wind_porosity": 0.0, "gaps": [],
        "collision": [ { "shape": "box", "size_m": [4.0, 2.3, 3.0], "position_m": [0, 1.15, 0] },
          { "shape": "box", "size_m": [4.4, 0.04, 3.4], "position_m": [0, 2.8, 0], "rotation_deg": [0, 0, 8], "material": "sheet_metal" } ] }
    }
    """;

    /// sample_patch's objects.json: 0 house_box, 1 shed_box, 2 gate_frame, 3 household_junk, 4–6 tree_proxy, 7–8 pole,
    /// 9 cable. The start used for the launch rails is the README's example start.
    static readonly Double3 RailStart = new(12, 0, -30);
    const double RailYaw = 90;

    static bool ObjectScenarios(WorldQuery sample, string dir, string surfacesPath)
    {
        string catalogPath = TestCatalog();
        SurfaceParams[] table = SurfaceParams.ParseTable(File.ReadAllText(surfacesPath));
        Catalog catalog = Catalog.Parse(File.ReadAllText(catalogPath));
        byte meadow = table.First(s => s.Id == "meadow_sod").Index;
        WorldQuery Flat() => Uniform(table, meadow, sample.Seed, catalog);
        WorldQuery Fresh() => WorldQuery.Load(dir, surfacesPath, catalogPath);

        bool pass = PlacementOrder();
        // 3.3: wind grid and gaps
        pass &= HouseShadow(Flat());
        pass &= TreeCrown(Flat(), catalog);
        pass &= WindGridLayout(sample);
        pass &= DoorGap(sample, dir);
        // 3.4: static contacts, raycasts, runtime objects
        pass &= NoTunnelling(Flat());
        pass &= RandomCrossings(Flat());
        pass &= SweptSemantics(Flat(), catalog);
        WorldQuery shapesWorld = Flat();
        List<TestShape> shapes = RandomShapes(shapesWorld, catalog);
        pass &= ClosestAgrees(shapesWorld, shapes);
        pass &= RaysAgree(shapesWorld, shapes);
        pass &= TerrainRays(sample, dir, table);
        pass &= MaterialsReported(Fresh(), catalog);
        pass &= RoofRay(Fresh(), catalog);
        pass &= RailsAdded(Fresh(), catalog);
        pass &= ResetBetweenFlights(Fresh);
        pass &= NoAllocationSameBits(Fresh());
        pass &= FarFromEverything(Fresh());
        return pass;
    }

    static string TestCatalog()
    {
        JsonNode catalog = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(CatalogPath)));
        foreach (KeyValuePair<string, JsonNode> asset in JsonNode.Parse(TestAssets).AsObject())
            catalog["assets"][asset.Key] = JsonNode.Parse(asset.Value.ToJsonString());
        string path = Path.Combine(OS.GetUserDataDir(), "worldquery_test_catalog.json");
        File.WriteAllText(path, catalog.ToJsonString());
        return path;
    }

    // ---------------------------------------------------------------- independent geometry

    /// Yaw about Y, then pitch about X, then roll about Z (degrees): Ry·Rx·Rz, row-major, from Math.Sin/Cos.
    static double[] Rot(double yaw, double pitch, double roll)
    {
        double a = yaw * Math.PI / 180, b = pitch * Math.PI / 180, c = roll * Math.PI / 180;
        double[] ry = { Math.Cos(a), 0, Math.Sin(a), 0, 1, 0, -Math.Sin(a), 0, Math.Cos(a) };
        double[] rx = { 1, 0, 0, 0, Math.Cos(b), -Math.Sin(b), 0, Math.Sin(b), Math.Cos(b) };
        double[] rz = { Math.Cos(c), -Math.Sin(c), 0, Math.Sin(c), Math.Cos(c), 0, 0, 0, 1 };
        return MatMul(ry, MatMul(rx, rz));
    }

    static double[] MatMul(double[] a, double[] b)
    {
        var m = new double[9];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                m[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
        return m;
    }

    static Double3 Mul(double[] m, Double3 v) =>
        new(m[0] * v.X + m[1] * v.Y + m[2] * v.Z, m[3] * v.X + m[4] * v.Y + m[5] * v.Z, m[6] * v.X + m[7] * v.Y + m[8] * v.Z);

    static Double3 MulT(double[] m, Double3 v) =>
        new(m[0] * v.X + m[3] * v.Y + m[6] * v.Z, m[1] * v.X + m[4] * v.Y + m[7] * v.Z, m[2] * v.X + m[5] * v.Y + m[8] * v.Z);

    static Double3 Unit(Double3 v) => v * (1 / v.Length());

    static Double3 N(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    static Double3 RandomUnit(Random random)
    {
        while (true)
        {
            var v = new Double3(2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1, 2 * random.NextDouble() - 1);
            double length = v.Length();
            if (length > 0.1 && length <= 1)
                return v * (1 / length);
        }
    }

    static double Angle(Double3 a, Double3 b) => Math.Atan2(Double3.Cross(a, b).Length(), Double3.Dot(a, b));

    static double TerrainAt(WorldQuery world, double x, double z)
    {
        var g = new GroundSample[1];
        world.SampleGround(new[] { new XZ(x, z) }, g);
        return g[0].TerrainHeight;
    }

    static double SegmentDistance(Double3 a, Double3 b, Double3 p)
    {
        Double3 d = b - a;
        double dd = Double3.Dot(d, d), t = dd > 0 ? Math.Clamp(Double3.Dot(p - a, d) / dd, 0, 1) : 0;
        return (p - (a + d * t)).Length();
    }

    /// A wire's polyline as the README defines it: each span drops 4·sag·t·(1 − t) at ⌈length / 0.5 m⌉ equal steps of t.
    static Double3[] Sagged(Double3[] points, double sag, double segment = 0.5)
    {
        var poly = new List<Double3> { points[0] };
        for (int i = 0; i + 1 < points.Length; i++)
        {
            int steps = (int)Math.Ceiling((points[i + 1] - points[i]).Length() / segment);
            for (int k = 1; k <= steps; k++)
            {
                double t = (double)k / steps;
                Double3 p = points[i] + (points[i + 1] - points[i]) * t;
                poly.Add(new Double3(p.X, p.Y - 4 * sag * t * (1 - t), p.Z));
            }
        }
        return poly.ToArray();
    }

    /// Distance from p to the polyline, and the length fraction along it of the nearest point.
    static (double distance, double param) Project(Double3[] poly, Double3 p)
    {
        double best = double.PositiveInfinity, bestAlong = 0, along = 0;
        for (int i = 0; i + 1 < poly.Length; i++)
        {
            Double3 d = poly[i + 1] - poly[i];
            double length = d.Length(), t = Math.Clamp(Double3.Dot(p - poly[i], d) / (length * length), 0, 1);
            double distance = (p - (poly[i] + d * t)).Length();
            if (distance < best)
                (best, bestAlong) = (distance, along + t * length);
            along += length;
        }
        return (best, bestAlong / along);
    }

    /// A placed primitive or wire, rebuilt here from the catalog numbers and the placement.
    sealed class TestShape
    {
        public ShapeKind Kind;
        public Double3 C, Half;
        public double[] M; // local to world
        public double Radius, H;
        public int Object, Shape;
        public ushort Material;
        public Double3[] Poly;
        public double Bound; // bounding radius around C (wires: around the polyline's box centre)
    }

    static TestShape Shape(ShapeDef d, Double3 position, double yaw, double pitch, double roll, double scale, int obj, int index)
    {
        double[] m = Rot(yaw, pitch, roll);
        var s = new TestShape
        {
            Kind = d.Kind, C = position + Mul(m, d.Position) * scale, M = MatMul(m, Rot(d.Yaw, d.Pitch, d.Roll)),
            Half = d.Size * (0.5 * scale), Radius = d.Radius * scale, Object = obj, Shape = index, Material = d.Material,
            H = (d.Kind == ShapeKind.Capsule ? Math.Max(d.Height / 2 - d.Radius, 0) : d.Height / 2) * scale,
        };
        s.Bound = s.Kind == ShapeKind.Box ? s.Half.Length() : s.Kind == ShapeKind.Sphere ? s.Radius
            : s.Kind == ShapeKind.Capsule ? s.H + s.Radius : Math.Sqrt(s.H * s.H + s.Radius * s.Radius);
        return s;
    }

    static double Sdf(TestShape s, Double3 p)
    {
        switch (s.Kind)
        {
            case ShapeKind.Wire:
                double best = double.PositiveInfinity;
                for (int i = 0; i + 1 < s.Poly.Length; i++)
                    best = Math.Min(best, SegmentDistance(s.Poly[i], s.Poly[i + 1], p));
                return best - s.Radius;
            case ShapeKind.Sphere:
                return (p - s.C).Length() - s.Radius;
        }
        Double3 l = MulT(s.M, p - s.C);
        if (s.Kind == ShapeKind.Box)
        {
            double qx = Math.Abs(l.X) - s.Half.X, qy = Math.Abs(l.Y) - s.Half.Y, qz = Math.Abs(l.Z) - s.Half.Z;
            return new Double3(Math.Max(qx, 0), Math.Max(qy, 0), Math.Max(qz, 0)).Length() + Math.Min(Math.Max(qx, Math.Max(qy, qz)), 0);
        }
        if (s.Kind == ShapeKind.Capsule)
            return new Double3(l.X, l.Y - Math.Clamp(l.Y, -s.H, s.H), l.Z).Length() - s.Radius;
        double dx = Math.Sqrt(l.X * l.X + l.Z * l.Z) - s.Radius, dy = Math.Abs(l.Y) - s.H;
        return Math.Min(Math.Max(dx, dy), 0) + Math.Sqrt(Math.Max(dx, 0) * Math.Max(dx, 0) + Math.Max(dy, 0) * Math.Max(dy, 0));
    }

    static Double3 Gradient(TestShape s, Double3 p, double h = 1e-6) => new Double3(
        Sdf(s, p + new Double3(h, 0, 0)) - Sdf(s, p - new Double3(h, 0, 0)), Sdf(s, p + new Double3(0, h, 0)) - Sdf(s, p - new Double3(0, h, 0)),
        Sdf(s, p + new Double3(0, 0, h)) - Sdf(s, p - new Double3(0, 0, h))) * (1 / (2 * h));

    /// A lower bound of the distance from the core a–b to the shape (per polyline segment for a wire).
    static double Reach(TestShape s, Double3 a, Double3 b)
    {
        if (s.Kind != ShapeKind.Wire)
            return SegmentDistance(a, b, s.C) - s.Bound;
        double best = double.PositiveInfinity;
        for (int i = 0; i + 1 < s.Poly.Length; i++)
            best = Math.Min(best, SegmentDistance(a, b, (s.Poly[i] + s.Poly[i + 1]) * 0.5) - (s.Poly[i + 1] - s.Poly[i]).Length() / 2);
        return best - s.Radius;
    }

    /// The least signed distance along the core a–b minus r: 2001 samples, then 2001 more around the best one.
    static double BruteDistance(TestShape s, Double3 a, Double3 b, double r)
    {
        if (a.X == b.X && a.Y == b.Y && a.Z == b.Z)
            return Sdf(s, a) - r;
        const int n = 2000;
        double best = double.PositiveInfinity;
        int at = 0;
        for (int i = 0; i <= n; i++)
        {
            double v = Sdf(s, a + (b - a) * ((double)i / n));
            if (v < best)
                (best, at) = (v, i);
        }
        double lo = Math.Max(at - 1, 0) / (double)n, hi = Math.Min(at + 1, n) / (double)n;
        for (int i = 0; i <= n; i++)
            best = Math.Min(best, Sdf(s, a + (b - a) * (lo + (hi - lo) * i / n)));
        return best - r;
    }

    // ---------------------------------------------------------------- 3.3 wind grid and gaps

    /// Axes.FromEuler (DetMath's sine and cosine) against Godot's Basis.FromEuler in YXZ order (float) and the matrices
    /// here (double).
    static bool PlacementOrder()
    {
        var random = new Random(71);
        double worstGodot = 0, worstHere = 0;
        for (int i = 0; i < 1000; i++)
        {
            double yaw = random.NextDouble() * 720 - 360, pitch = random.NextDouble() * 360 - 180, roll = random.NextDouble() * 360 - 180;
            Axes a = Axes.FromEuler(yaw, pitch, roll);
            Basis b = Basis.FromEuler(new Vector3(Mathf.DegToRad((float)pitch), Mathf.DegToRad((float)yaw), Mathf.DegToRad((float)roll)), EulerOrder.Yxz);
            double[] m = Rot(yaw, pitch, roll);
            foreach ((Double3 axis, Vector3 godot, int column) in new[] { (a.X, b.X, 0), (a.Y, b.Y, 1), (a.Z, b.Z, 2) })
            {
                worstGodot = Math.Max(worstGodot, (axis - new Double3(godot.X, godot.Y, godot.Z)).Length());
                worstHere = Math.Max(worstHere, (axis - new Double3(m[column], m[3 + column], m[6 + column])).Length());
            }
        }
        return Check("placement follows Godot's YXZ order", worstGodot <= 1e-5 && worstHere <= 1e-12,
            $"1000 random rotations: largest axis difference {worstGodot:0.0e0} vs Godot's float Basis.FromEuler(YXZ) (limit 1e-5), "
            + $"{worstHere:0.0e0} vs double Ry·Rx·Rz (limit 1e-12)");
    }

    /// house_box at scale 1.2 is a solid box 12 × 6 × 7.2 m. Placed yawed 15° on flat ground: cells a whole cell inside
    /// its footprint report TopM 6, BaseM 0 and porosity ≤ 0.1; cells a whole cell outside are open ground (0, 0, 1);
    /// the band in between (edge blur) is either open or reports the house.
    static bool HouseShadow(WorldQuery world)
    {
        var at = new Double3(10.3, 0, -7.1);
        world.AddObject("house_box", at, 15, 0, 0, 1.2);
        double c = Math.Cos(15 * Math.PI / 180), s = Math.Sin(15 * Math.PI / 180), diagonal = Math.Sqrt(2);
        int n = world.WindCells, interior = 0, open = 0, blur = 0, bad = 0;
        double worstTop = 0, worstInsidePorosity = 0, blurLow = 1, blurHigh = 0;
        string first = "";
        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col < n; col++)
            {
                double x = -world.Half + (col + 0.5) * 2 - at.X, z = -world.Half + (row + 0.5) * 2 - at.Z;
                double lx = x * c - z * s, lz = x * s + z * c; // world to house frame (inverse yaw)
                double inside = Math.Min(6 - Math.Abs(lx), 3.6 - Math.Abs(lz));
                double outside = Math.Sqrt(Math.Pow(Math.Max(Math.Abs(lx) - 6, 0), 2) + Math.Pow(Math.Max(Math.Abs(lz) - 3.6, 0), 2));
                WindCell cell = world.WindGrid[row * n + col];
                bool isOpen = cell.TopM == 0 && cell.BaseM == 0 && cell.Porosity == 1;
                bool isHouse = Math.Abs(cell.TopM - 6) <= 1e-5 && cell.BaseM == 0;
                bool ok;
                if (inside > diagonal)
                {
                    interior++;
                    worstTop = Math.Max(worstTop, Math.Abs(cell.TopM - 6));
                    worstInsidePorosity = Math.Max(worstInsidePorosity, cell.Porosity);
                    ok = isHouse && cell.Porosity <= 0.1;
                }
                else if (outside > diagonal)
                {
                    open++;
                    ok = isOpen;
                }
                else
                {
                    blur++;
                    ok = isOpen || isHouse;
                    if (!isOpen)
                        (blurLow, blurHigh) = (Math.Min(blurLow, cell.Porosity), Math.Max(blurHigh, cell.Porosity));
                }
                if (!ok && bad++ == 0)
                    first = $"; first: row {row}, column {col}: top {cell.TopM}, base {cell.BaseM}, porosity {cell.Porosity}";
            }
        }
        return Check("house shadow", bad == 0 && interior > 0,
            $"6 m solid house yawed 15°: {interior} cells inside the footprint: TopM 6 within {worstTop:0.0e0} m, BaseM 0, porosity "
            + $"at most {worstInsidePorosity} (limit 0.1); {open} open cells report 0 m and porosity 1; {blur} edge cells, the house "
            + $"ones with porosity {blurLow:0.000}–{blurHigh:0.000}; {bad} breaking a rule{first}");
    }

    /// tree_proxy's wind volume is its crown, a 3 m sphere at 7 m with porosity 0.5. On flat ground, with the crown centred
    /// on a cell centre, on a cell corner and off the grid: cells wholly under the crown report BaseM 4 and TopM 10, every
    /// cell it touches reports the same, open ground stays open, and the row and the column of cells through the crown's
    /// centre have path porosity ≈ 0.5 (±0.1: a 6 m crown spans only 3–4 cells of 2 m).
    static bool TreeCrown(WorldQuery world, Catalog catalog)
    {
        AssetDef tree = catalog.Assets["tree_proxy"];
        ShapeDef crown = tree.WindVolume[0];
        double r = crown.Radius, top = crown.Position.Y + r, bottom = crown.Position.Y - r;
        var spots = new (string label, double x, double z)[] { ("on a cell centre", -47, -47), ("on a cell corner", 0, -60), ("off the grid", 47.37, 21.21) };
        foreach (var spot in spots)
            world.AddObject("tree_proxy", new Double3(spot.x, 0, spot.z), 0);
        WindCell[] grid = world.WindGrid.ToArray();
        int n = world.WindCells;
        bool pass = true;
        foreach (var (label, tx, tz) in spots)
        {
            int under = 0, touched = 0, wrong = 0;
            int row0 = (int)Math.Floor((tz + world.Half) / 2), col0 = (int)Math.Floor((tx + world.Half) / 2);
            for (int row = row0 - 5; row <= row0 + 5; row++)
            {
                for (int col = col0 - 5; col <= col0 + 5; col++)
                {
                    double x0 = -world.Half + col * 2, z0 = -world.Half + row * 2;
                    double far = new[] { (x0, z0), (x0 + 2, z0), (x0, z0 + 2), (x0 + 2, z0 + 2) }.Max(p => Math.Sqrt((p.Item1 - tx) * (p.Item1 - tx) + (p.Item2 - tz) * (p.Item2 - tz)));
                    double centre = Math.Sqrt((x0 + 1 - tx) * (x0 + 1 - tx) + (z0 + 1 - tz) * (z0 + 1 - tz));
                    WindCell c = grid[row * n + col];
                    bool crownValues = Math.Abs(c.TopM - top) <= 1e-5 && Math.Abs(c.BaseM - bottom) <= 1e-5;
                    if (c.Porosity < 1)
                    {
                        touched++;
                        wrong += crownValues ? 0 : 1;
                    }
                    if (far <= r)
                    {
                        under++;
                        wrong += crownValues && c.Porosity < 1 ? 0 : 1;
                    }
                    if (centre > r + Math.Sqrt(2) && !(c.TopM == 0 && c.BaseM == 0 && c.Porosity == 1))
                        wrong++;
                }
            }
            double rowPath = 1, colPath = 1;
            for (int i = 0; i < n; i++)
            {
                rowPath *= grid[row0 * n + i].Porosity;
                colPath *= grid[i * n + col0].Porosity;
            }
            pass &= Check($"tree crown ({label})", wrong == 0 && under > 0 && Math.Abs(rowPath - tree.WindPorosity) <= 0.1
                    && Math.Abs(colPath - tree.WindPorosity) <= 0.1,
                $"{under} cells wholly under the crown and {touched} touched, all BaseM {bottom} m and TopM {top} m; path porosity across "
                + $"the crown {rowPath:0.000} west–east and {colPath:0.000} north–south vs wind_porosity {tree.WindPorosity} (limit ±0.1); "
                + $"{wrong} cells breaking a rule");
        }
        return pass;
    }

    /// On sample_patch, row 0 is north: the cell holding the house (object 0, at x 52, z 30, 5 m tall on y 0.684) is solid
    /// with its top 5.684 m minus the terrain there; the same column mirrored north–south is open ground. The first tree's
    /// crown (x −40, z −50, on y 0.214) sits 4.214–10.214 m minus the terrain.
    static bool WindGridLayout(WorldQuery sample)
    {
        int n = sample.WindCells;
        int row = (int)Math.Floor((30 + sample.Half) / 2), col = (int)Math.Floor((52 + sample.Half) / 2);
        WindCell house = sample.WindGrid[row * n + col], mirror = sample.WindGrid[(n - 1 - row) * n + col];
        double terrain = TerrainAt(sample, -sample.Half + (col + 0.5) * 2, -sample.Half + (row + 0.5) * 2);
        int treeRow = (int)Math.Floor((-50 + sample.Half) / 2), treeCol = (int)Math.Floor((-40 + sample.Half) / 2);
        WindCell tree = sample.WindGrid[treeRow * n + treeCol];
        double treeTerrain = TerrainAt(sample, -sample.Half + (treeCol + 0.5) * 2, -sample.Half + (treeRow + 0.5) * 2);
        int obstacles = 0;
        foreach (WindCell c in sample.WindGrid)
            obstacles += c.Porosity < 1 ? 1 : 0;
        bool ok = n == 128 && sample.WindGrid.Length == n * n && Math.Abs(house.TopM - (5.684 - terrain)) <= 1e-4
            && Math.Abs(house.BaseM - Math.Max(0.684 - terrain, 0)) <= 1e-4 && house.Porosity <= 1e-6
            && mirror.TopM == 0 && mirror.Porosity == 1 && Math.Abs(tree.BaseM - (4.214 - treeTerrain)) <= 1e-4
            && Math.Abs(tree.TopM - (10.214 - treeTerrain)) <= 1e-4 && tree.Porosity < 1;
        return Check("wind grid layout on sample_patch", ok,
            $"{n} × {n} cells of 2 m, {obstacles} with an obstacle; house cell (row {row}, column {col}) TopM {house.TopM:0.0000} m "
            + $"(expected {5.684 - terrain:0.0000}), BaseM {house.BaseM:0.0000} (expected {Math.Max(0.684 - terrain, 0):0.0000}), porosity "
            + $"{house.Porosity}; mirrored cell (row {n - 1 - row}) TopM {mirror.TopM}, porosity {mirror.Porosity}; first tree's cell "
            + $"BaseM {tree.BaseM:0.0000} / TopM {tree.TopM:0.0000} (expected {4.214 - treeTerrain:0.0000} / {10.214 - treeTerrain:0.0000}), "
            + $"porosity {tree.Porosity:0.000}");
    }

    /// Query centred 5 m in front of and behind sample_patch's gate (object 2): its gap "gate" comes back with the
    /// world-space centre, facing and size computed here with Godot's Basis; at 4.9 m it does not. 5 m to the side, 0.8 m
    /// of which is inside the 1.6 m wide opening, a radius of 4.25 m reaches it and 4.15 m does not. An empty buffer
    /// still gets the true count.
    static bool DoorGap(WorldQuery sample, string dir)
    {
        using JsonDocument objects = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "objects.json")));
        JsonElement gate = objects.RootElement.GetProperty("objects")[2];
        Double3 position = Catalog.Vec3(gate, "position_m"), rotation = Catalog.Vec3(gate, "rotation_deg");
        Basis basis = Basis.FromEuler(new Vector3(Mathf.DegToRad((float)rotation.Y), Mathf.DegToRad((float)rotation.X), Mathf.DegToRad((float)rotation.Z)), EulerOrder.Yxz);
        Vector3 local = basis * new Vector3(0, 0.99f, 0);
        var centre = new Double3(position.X + local.X, position.Y + local.Y, position.Z + local.Z);
        var normal = new Double3(basis.Z.X, basis.Z.Y, basis.Z.Z);
        var up = new Double3(basis.Y.X, basis.Y.Y, basis.Y.Z);
        Double3 across = Double3.Cross(up, normal);
        var gaps = new Gap[4];
        int front = sample.GapsNear(centre + normal * 5, 6, gaps);
        Gap g = gaps[0];
        bool found = front == 1 && g.Object == 2 && g.Name == "gate" && (g.Center - centre).Length() <= 1e-5
            && Math.Abs(g.Width - 1.6) <= 1e-6 && Math.Abs(g.Height - 1.98) <= 1e-6
            && Angle(N(g.Normal), normal) <= 1e-5 && Angle(N(g.Up), up) <= 1e-5;
        int tooFar = sample.GapsNear(centre + normal * 5, 4.9, gaps), behind = sample.GapsNear(centre - normal * 5, 6, gaps);
        int sideIn = sample.GapsNear(centre + across * 5, 4.25, gaps), sideOut = sample.GapsNear(centre + across * 5, 4.15, gaps);
        int overflow = sample.GapsNear(centre + normal * 5, 6, Span<Gap>.Empty);
        return Check("door gap", found && tooFar == 0 && behind == 1 && sideIn == 1 && sideOut == 0 && overflow == 1,
            $"5 m in front, r 6 m: {front} gap, object {g.Object} '{g.Name}' centre {g.Center} vs {centre} "
            + $"({(g.Center - centre).Length():0.0e0} m), {g.Width} × {g.Height} m, facing {N(g.Normal)}; r 4.9 m: {tooFar}; 5 m behind: "
            + $"{behind}; 5 m to the side, r 4.25 / 4.15 m: {sideIn} / {sideOut}; empty buffer returns {overflow}");
    }

    // ---------------------------------------------------------------- 3.4 static contacts, raycasts, runtime objects

    /// Spheres of 15 mm diameter (and 15 mm radius) swept 60 mm per step across a 5 mm wire (a 30 m span sagging 0.6 m),
    /// at 3000 phases of the step (every 0.02 mm), crossing at a polyline vertex and mid-segment, square across, at 30° and
    /// diving at 45°, with the path passing the wire's axis at offsets from 0 to 0.1 mm short of a graze. Every
    /// configuration must report the wire; every reported point lies on the wire's surface with the right WireParam, and
    /// a contact the sweep ran into during the step (0 < Time < 1) faces the side the sphere came from. A Time 0 contact
    /// was already overlapping at the previous pose, whose centre may lie on the wire's axis, where no side is defined.
    /// Static queries at the same poses (every 10th phase) are counted for comparison.
    static bool NoTunnelling(WorldQuery world)
    {
        const double wireRadius = 0.0025, step = 0.06, sag = 0.6;
        Double3[] ends = { new(-15, 3, 0), new(15, 3, 0) };
        int wire = world.AddWire("cable", ends, sag, 2 * wireRadius);
        Double3[] poly = Sagged(ends, sag);
        var crossings = new (string label, Double3 at, Double3 along)[]
        {
            ("vertex", poly[30], Unit(poly[31] - poly[29])),
            ("mid-segment", (poly[30] + poly[31]) * 0.5, Unit(poly[31] - poly[30])),
        };
        var motions = new[] { new Double3(0, 0, 1), new Double3(Math.Sin(Math.PI / 6), 0, Math.Cos(Math.PI / 6)), Unit(new Double3(0, -1, 1)) };
        const int phases = 3000;
        var buffer = new StaticContact[8];
        long configs = 0, missed = 0, staticSampled = 0, staticMissed = 0, badPoint = 0, badNormal = 0, fromSweep = 0, startedIn = 0;
        double worstSurface = 0, worstParam = 0;
        string firstMiss = "", firstBad = "";
        foreach (var (label, at, along) in crossings)
        {
            foreach (Double3 u in motions)
            {
                Double3 side = Unit(Double3.Cross(u, along));
                foreach (double radius in new[] { 0.0075, 0.015 })
                {
                    foreach (double h in new[] { 0, 0.5 * wireRadius, 0.96 * wireRadius, (wireRadius + radius) / 2, wireRadius + radius - 1e-4 })
                    {
                        for (int j = 0; j < phases; j++)
                        {
                            double phase = -step + j * (step / phases);
                            Double3 Pose(int k) => at + side * h + u * (phase + step * k);
                            bool seen = false, seenStatic = false, sampleStatic = j % 10 == 0;
                            for (int k = -3; k <= 3; k++)
                            {
                                var current = new Capsule(Pose(k), Pose(k), radius);
                                int ns = sampleStatic ? world.StaticContacts(current, 0, buffer) : 0;
                                for (int i = 0; i < Math.Min(ns, buffer.Length); i++)
                                    seenStatic |= buffer[i].Object == wire;
                                if (k == -3)
                                    continue;
                                int n = world.StaticContacts(new Capsule(Pose(k - 1), Pose(k - 1), radius), current, 0, buffer);
                                for (int i = 0; i < Math.Min(n, buffer.Length); i++)
                                {
                                    StaticContact c = buffer[i];
                                    if (c.Object != wire)
                                        continue;
                                    seen = true;
                                    (double distance, double param) = Project(poly, c.Point);
                                    worstSurface = Math.Max(worstSurface, Math.Abs(distance - wireRadius));
                                    worstParam = Math.Max(worstParam, Math.Abs(c.WireParam - param));
                                    badPoint += Math.Abs(distance - wireRadius) > 1e-7 || Math.Abs(c.WireParam - param) > 1e-6 ? 1 : 0;
                                    if (c.Time == 0)
                                        startedIn++;
                                    else if (c.Time < 1)
                                    {
                                        fromSweep++;
                                        if (Double3.Dot(N(c.Normal), u) >= 0 && badNormal++ == 0)
                                            firstBad = $"; first not facing: {label}, motion {u}, radius {radius}, offset {h}, phase {phase}, "
                                                + $"step {k}, Time {c.Time}, normal {N(c.Normal)}, distance {c.Distance}";
                                    }
                                }
                            }
                            configs++;
                            staticSampled += sampleStatic ? 1 : 0;
                            staticMissed += sampleStatic && !seenStatic ? 1 : 0;
                            if (!seen && missed++ == 0)
                                firstMiss = $"; first miss: {label}, motion {u}, radius {radius}, offset {h}, phase {phase}";
                        }
                    }
                }
            }
        }
        return Check("no tunnelling through a wire", missed == 0 && badPoint == 0 && badNormal == 0,
            $"{configs} sweeps across the wire (2 crossings × 3 directions × 2 sphere sizes × 5 offsets × {phases} phases): "
            + $"{missed} missed; {fromSweep} contacts where the sweep touched the wire during the step (0 < Time < 1), {badNormal} of "
            + $"them not facing the approach; {startedIn} with Time 0 (already overlapping at the previous pose); "
            + $"points on the wire surface within {worstSurface:0.0e0} m, WireParam within {worstParam:0.0e0}, {badPoint} bad; "
            + $"static queries alone at the same poses miss {staticMissed} of {staticSampled} sampled configurations "
            + $"({100.0 * staticMissed / staticSampled:0.0} %){firstMiss}{firstBad}");
    }

    /// 5000 random sweeps of 60 mm across a 3-span, 8 mm sagging wire: random crossing point, direction (at least 10° off
    /// the wire), phase, sphere radius (2–20 mm) and offset (up to 0.1 mm short of a graze). All must report the wire.
    static bool RandomCrossings(WorldQuery world)
    {
        const double wireRadius = 0.004, step = 0.06, sag = 0.4;
        Double3[] ends = { new(-12, 5, 3), new(0, 6, -2), new(9, 4.5, 6) };
        int wire = world.AddWire("cable", ends, sag, 2 * wireRadius);
        Double3[] poly = Sagged(ends, sag);
        var random = new Random(79);
        var buffer = new StaticContact[8];
        int missed = 0;
        string firstMiss = "";
        for (int n = 0; n < 5000; n++)
        {
            int segment = 1 + random.Next(poly.Length - 3);
            Double3 along = Unit(poly[segment + 1] - poly[segment]), at = poly[segment] + (poly[segment + 1] - poly[segment]) * random.NextDouble();
            Double3 u;
            do
                u = RandomUnit(random);
            while (Math.Abs(Double3.Dot(u, along)) > Math.Cos(10 * Math.PI / 180));
            double radius = 0.002 + 0.018 * random.NextDouble(), phase = -step * random.NextDouble();
            double h = (2 * random.NextDouble() - 1) * (wireRadius + radius - 1e-4);
            Double3 side = Unit(Double3.Cross(u, along));
            Double3 Pose(int k) => at + side * h + u * (phase + step * k);
            bool seen = false;
            for (int k = -1; k <= 2; k++)
            {
                int count = world.StaticContacts(new Capsule(Pose(k - 1), Pose(k - 1), radius), new Capsule(Pose(k), Pose(k), radius), 0, buffer);
                for (int i = 0; i < Math.Min(count, buffer.Length); i++)
                    seen |= buffer[i].Object == wire;
            }
            if (!seen && missed++ == 0)
                firstMiss = $"; first miss: at {at}, motion {u}, radius {radius}, offset {h}, phase {phase}";
        }
        return Check("no tunnelling, random crossings", missed == 0, $"5000 random 60 mm sweeps across an 8 mm wire: {missed} missed{firstMiss}");
    }

    /// The swept rules on the launch rails and a 5 mm wire: a sphere resting on a bar that slides off its edge and drops
    /// reports nothing (no phantom contact); a sphere resting on the wire and pushed 30 mm through it in one step reports
    /// the wire with Time below 1 and the upward normal it rested on; a sphere dropping 100 mm through a 25 mm bar reports
    /// it at the first touch (Time 0.425, normal up); and one landing 2 mm into a bar reports the current pose.
    static bool SweptSemantics(WorldQuery world, Catalog catalog)
    {
        int rails = world.AddObject("launch_rails", new Double3(0, 0, 20), 0);
        int wire = world.AddWire("cable", new[] { new Double3(-5, 3, 40), new Double3(5, 3, 40) }, 0, 0.005);
        var buffer = new StaticContact[8];
        const double r = 0.0075;
        // Bar 0 spans x −0.1425 to −0.1175 with its top at y 0.25.
        var resting = new Double3(-0.1185, 0.25 + r - 1e-4, 20);
        int before = world.StaticContacts(new Capsule(resting, resting, r), 0.001, buffer);
        double restingDistance = before > 0 ? buffer[0].Distance : double.NaN;
        bool restingOk = before == 1 && buffer[0].Object == rails && buffer[0].Shape == 0 && Math.Abs(restingDistance + 1e-4) <= 1e-9;
        var offEdge = new Double3(-0.1075, 0.25 + r - 1.1e-3, 20);
        int slid = world.StaticContacts(new Capsule(resting, resting, r), new Capsule(offEdge, offEdge, r), 0.001, buffer);

        var onWire = new Double3(0.3, 3 + 0.0025 + r - 1e-4, 40);
        var below = new Double3(0.3, 2.97, 40);
        int pushed = world.StaticContacts(new Capsule(onWire, onWire, r), new Capsule(below, below, r), 0, buffer);
        StaticContact push = buffer[0];
        bool pushOk = pushed == 1 && push.Object == wire && push.Time < 1 && push.Normal.Y > 0.999 && Math.Abs(push.Distance + 1e-4) <= 1e-9;

        var above = new Double3(-0.13, 0.30, 19.9);
        var under = new Double3(-0.13, 0.20, 19.9);
        int through = world.StaticContacts(new Capsule(above, above, r), new Capsule(under, under, r), 0, buffer);
        StaticContact bar = buffer[0];
        bool throughOk = through == 1 && bar.Object == rails && bar.Shape == 0 && Math.Abs(bar.Time - 0.425) <= 1e-5
            && bar.Normal.Y > 0.999999 && bar.Distance <= 1e-6 && bar.Distance >= -1e-6 && bar.Material == catalog.MaterialId("steel");

        var landed = new Double3(-0.13, 0.25 + r - 0.002, 19.9);
        int landing = world.StaticContacts(new Capsule(above, above, r), new Capsule(landed, landed, r), 0, buffer);
        bool landingOk = landing == 1 && buffer[0].Time == 1 && Math.Abs(buffer[0].Distance + 0.002) <= 1e-9;
        return Check("swept contact rules", restingOk && slid == 0 && pushOk && throughOk && landingOk,
            $"resting on a bar: {before} contact, distance {restingDistance:0.0e0}; sliding off its edge: "
            + $"{slid} contacts; pushed through the wire: {pushed}, Time {push.Time}, normal {N(push.Normal)}, distance {push.Distance:0.0e0}; "
            + $"dropping 100 mm through a bar: {through}, Time {bar.Time:0.000000}, normal y {bar.Normal.Y}, distance {bar.Distance:0.0e0}; "
            + $"landing 2 mm into it: {landing} contact, Time {buffer[0].Time}, distance {buffer[0].Distance:0.000000}");
    }

    /// 24 random placements (yaw, pitch, roll, scale 0.7–1.5) of the four primitive test assets plus two sagging wires,
    /// on flat ground, and the same shapes rebuilt here.
    static List<TestShape> RandomShapes(WorldQuery world, Catalog catalog)
    {
        var random = new Random(83);
        var shapes = new List<TestShape>();
        string[] ids = { "t_box", "t_sphere", "t_capsule", "t_cylinder" };
        for (int i = 0; i < 24; i++)
        {
            var position = new Double3(-10 + 20 * random.NextDouble(), 0.3 + 2 * random.NextDouble(), -10 + 20 * random.NextDouble());
            double yaw = random.NextDouble() * 360 - 180, pitch = random.NextDouble() * 360 - 180, roll = random.NextDouble() * 360 - 180;
            double scale = 0.7 + 0.8 * random.NextDouble();
            int index = world.AddObject(ids[i % 4], position, yaw, pitch, roll, scale);
            ShapeDef[] collision = catalog.Assets[ids[i % 4]].Collision;
            for (int k = 0; k < collision.Length; k++)
                shapes.Add(Shape(collision[k], position, yaw, pitch, roll, scale, index, k));
        }
        foreach ((Double3[] points, double sag, double diameter) in new[]
        {
            (new[] { new Double3(-11, 2.5, -4), new Double3(11, 3.5, 5) }, 0.5, 0.012),
            (new[] { new Double3(-6, 1.5, 9), new Double3(0, 2, 2), new Double3(7, 1.2, -9) }, 0.3, 0.005),
        })
        {
            int index = world.AddWire("cable", points, sag, diameter);
            Double3[] poly = Sagged(points, sag);
            Double3 lo = poly.Aggregate((p, q) => new Double3(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Min(p.Z, q.Z)));
            Double3 hi = poly.Aggregate((p, q) => new Double3(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y), Math.Max(p.Z, q.Z)));
            shapes.Add(new TestShape
            {
                Kind = ShapeKind.Wire, Poly = poly, Radius = diameter / 2, Object = index, Material = catalog.Assets["cable"].Material,
                C = (lo + hi) * 0.5, Bound = (hi - lo).Length() / 2 + diameter / 2,
            });
        }
        return shapes;
    }

    /// 3000 random spheres and capsules (up to 0.5 m long, radius 2–52 mm) near the random shapes, margin 0.3 m. Every
    /// shape whose sampled least distance is within the margin is reported, nothing else is, in canonical order; each
    /// reported distance is the sampled least distance (within 1 µm); the point is on the surface and point + normal ×
    /// (distance + radius) is on the core (within 1 µm); the normal points out of the shape and, away from edges, is the
    /// gradient of its signed distance there (within 1e-4 rad).
    static bool ClosestAgrees(WorldQuery world, List<TestShape> shapes)
    {
        var random = new Random(89);
        var buffer = new StaticContact[64];
        const double margin = 0.3;
        int contacts = 0, missing = 0, extra = 0, badDistance = 0, badWitness = 0, inward = 0, badGradient = 0, gradientChecked = 0, unordered = 0;
        double worstDistance = 0, worstWitness = 0, worstGradient = 0;
        string first = "";
        for (int q = 0; q < 3000; q++)
        {
            TestShape target = shapes[random.Next(shapes.Count)];
            Double3 a = target.Kind == ShapeKind.Wire
                ? target.Poly[random.Next(target.Poly.Length)] + RandomUnit(random) * (0.3 * random.NextDouble())
                : target.C + RandomUnit(random) * ((target.Bound + 0.3) * random.NextDouble());
            Double3 b = q % 2 == 0 ? a : a + RandomUnit(random) * (0.02 + 0.48 * random.NextDouble());
            double r = 0.002 + 0.05 * random.NextDouble();
            int n = world.StaticContacts(new Capsule(a, b, r), margin, buffer);
            StaticContact[] reported = buffer[..Math.Min(n, buffer.Length)];
            for (int i = 1; i < reported.Length; i++)
                unordered += reported[i - 1].Object < reported[i].Object || reported[i - 1].Object == reported[i].Object && reported[i - 1].Shape < reported[i].Shape ? 0 : 1;
            foreach (TestShape s in shapes)
            {
                double reach = Reach(s, a, b) - r;
                int at = Array.FindIndex(reported, c => c.Object == s.Object && c.Shape == s.Shape);
                if (reach > margin + 1e-6)
                {
                    extra += at >= 0 ? 1 : 0;
                    continue;
                }
                double brute = BruteDistance(s, a, b, r);
                if (at < 0)
                {
                    if (brute <= margin - 1e-6 && missing++ == 0)
                        first = $"; first missing: object {s.Object} ({s.Kind}), sampled distance {brute}";
                    continue;
                }
                StaticContact c = reported[at];
                contacts++;
                extra += brute > margin + 1e-6 ? 1 : 0;
                worstDistance = Math.Max(worstDistance, Math.Abs(c.Distance - brute));
                badDistance += Math.Abs(c.Distance - brute) <= 1e-6 ? 0 : 1;
                Double3 normal = N(c.Normal), core = c.Point + normal * (c.Distance + r);
                double witness = Math.Max(Math.Abs(Sdf(s, c.Point)), SegmentDistance(a, b, core));
                worstWitness = Math.Max(worstWitness, witness);
                badWitness += witness <= 1e-6 ? 0 : 1;
                inward += Sdf(s, c.Point + normal * 1e-4) > 0 ? 0 : 1;
                Double3 gradient = Gradient(s, core);
                if (Math.Abs(gradient.Length() - 1) <= 1e-3 && Math.Abs(Sdf(s, core)) > 1e-4)
                {
                    gradientChecked++;
                    double angle = Angle(gradient, normal);
                    worstGradient = Math.Max(worstGradient, angle);
                    badGradient += angle <= 1e-4 ? 0 : 1;
                }
            }
        }
        return Check("closest points agree with sampling", missing == 0 && extra == 0 && badDistance == 0 && badWitness == 0 && inward == 0
                && badGradient == 0 && unordered == 0 && contacts > 1000,
            $"3000 queries (half spheres, half capsules) around 24 boxes, spheres, capsules and cylinders at random rotations and 2 "
            + $"wires: {contacts} contacts, {missing} missing, {extra} beyond the margin, {unordered} out of order; distance vs sampled "
            + $"within {worstDistance:0.0e0} m ({badDistance} over 1 µm); witness within {worstWitness:0.0e0} m ({badWitness} over 1 µm); "
            + $"{inward} normals pointing in; normal vs signed-distance gradient within {worstGradient:0.0e0} rad at {gradientChecked} "
            + $"points away from edges ({badGradient} over 1e-4){first}");
    }

    static double Union(List<TestShape> shapes, Double3 p, out int nearest)
    {
        double best = p.Y; // the flat ground, y ≤ 0
        nearest = -1;
        for (int i = 0; i < shapes.Count; i++)
        {
            double d = Sdf(shapes[i], p);
            if (d < best)
                (best, nearest) = (d, i);
        }
        return best;
    }

    /// 5000 rays of up to 3 m from outside every shape, aimed near a random shape, against sphere tracing of the union
    /// of the shapes and the ground: the same first hit (within 1 µm), shape, material and normal (the signed distance
    /// gradient, within 1e-3 rad away from edges). Rays that pass within 1 µm of a surface without entering it (grazing)
    /// are allowed to differ and are counted. 300 rays that start inside a shape never hit that shape.
    static bool RaysAgree(WorldQuery world, List<TestShape> shapes)
    {
        var random = new Random(97);
        const double maxDistance = 3;
        var rays = new Ray[1];
        var hits = new RayHit[1];
        var trace = new List<(double t, double s)>();
        int count = 0, hitCount = 0, disagree = 0, grazing = 0, badShape = 0, badNormal = 0, normalChecked = 0, insideHits = 0;
        double worst = 0, worstNormal = 0;
        string first = "";
        while (count < 5000)
        {
            TestShape target = shapes[random.Next(shapes.Count)];
            Double3 aim = target.Kind == ShapeKind.Wire ? target.Poly[random.Next(target.Poly.Length)] : target.C;
            Double3 o = aim + RandomUnit(random) * (target.Bound + 0.2 + 1.5 * random.NextDouble());
            if (Union(shapes, o, out _) <= 0)
                continue;
            Double3 d = Unit(aim + RandomUnit(random) * (target.Bound * random.NextDouble()) - o);
            rays[0] = new Ray(o, d);
            world.Raycast(rays, maxDistance, hits);
            count++;
            // sphere tracing: never steps past the first surface of an exact signed distance
            double t = 0;
            int shape = -1;
            bool found = false;
            trace.Clear();
            for (int i = 0; i < 100000 && t <= maxDistance; i++)
            {
                double s = Union(shapes, o + d * t, out shape);
                if (s < 1e-10)
                {
                    found = true;
                    break;
                }
                trace.Add((t, s));
                t += s;
            }
            found &= t <= maxDistance;
            // passing within 1 µm of a surface, other than on the final approach to the hit
            double closest = trace.Where(p => !found || p.t < t - 1e-4).Select(p => p.s).DefaultIfEmpty(double.PositiveInfinity).Min();
            RayHit hit = hits[0];
            bool reported = hit.Distance <= maxDistance;
            hitCount += reported ? 1 : 0;
            if (reported != found || found && Math.Abs(hit.Distance - t) > 1e-6)
            {
                if (closest <= 1e-6 || found && Math.Abs(Double3.Dot(N(hit.Normal), d)) < 0.01)
                    grazing++;
                else if (disagree++ == 0)
                    first = $"; first: origin {o}, direction {d}: reported {hit.Distance} (object {hit.Object}), traced {(found ? t : double.NaN)}";
                continue;
            }
            if (!found)
                continue;
            worst = Math.Max(worst, Math.Abs(hit.Distance - t));
            int expectedObject = shape < 0 ? -1 : shapes[shape].Object;
            ushort expectedMaterial = shape < 0 ? Catalog.NoMaterial : shapes[shape].Material;
            badShape += hit.Object == expectedObject && hit.Material == expectedMaterial && (shape >= 0 || hit.Surface != 0) ? 0 : 1;
            Double3 gradient = shape < 0 ? new Double3(0, 1, 0) : Gradient(shapes[shape], hit.Point);
            if (Math.Abs(gradient.Length() - 1) <= 1e-3)
            {
                normalChecked++;
                double angle = Angle(gradient, N(hit.Normal));
                worstNormal = Math.Max(worstNormal, angle);
                badNormal += angle <= 1e-3 && Double3.Dot(N(hit.Normal), d) < 0 ? 0 : 1;
            }
        }
        for (int i = 0; i < 300; i++)
        {
            TestShape s = shapes[i % 24];
            rays[0] = new Ray(s.C, RandomUnit(random));
            world.Raycast(rays, maxDistance, hits);
            insideHits += hits[0].Object == s.Object ? 1 : 0;
        }
        return Check("rays agree with sphere tracing", disagree == 0 && badShape == 0 && badNormal == 0 && insideHits == 0 && hitCount > 2000,
            $"5000 rays of up to {maxDistance} m among the random shapes and wires over flat ground: {hitCount} hits, first hit within "
            + $"{worst:0.0e0} m of sphere tracing, {disagree} disagreeing, {grazing} grazing (passing within 1 µm, allowed); {badShape} with "
            + $"the wrong object, material or surface; normal vs gradient within {worstNormal:0.0e0} rad at {normalChecked} hits away from "
            + $"edges ({badNormal} over 1e-3 rad or not facing the ray); 300 rays from inside a shape: {insideHits} hit it{first}");
    }

    /// Rays on sample_patch's terrain alone, against the renderer's triangles evaluated independently (barycentric,
    /// as in the SampleGround check): 3000 vertical rays land on TerrainHeight within 1 nm, with the surface of SampleGround;
    /// 2000 oblique rays land on a triangle (within 1 nm) and stay above the terrain before it (checked every 5 mm); rays
    /// outside the map land on the edge continued flat, as SampleGround's terrain does.
    static bool TerrainRays(WorldQuery sample, string dir, SurfaceParams[] table)
    {
        var bare = new WorldQuery(table, sample.SizeM, sample.Seed, sample.Samples, HeightsOf(sample), sample.Cells, SurfaceLayer(sample), CoverLayer(dir));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "map.json")));
        JsonElement height = manifest.RootElement.GetProperty("height");
        byte[] r16 = File.ReadAllBytes(Path.Combine(dir, "height.r16"));
        float offset = height.GetProperty("offset_m").GetSingle(), scale = height.GetProperty("scale_m").GetSingle();
        double H(double x, double z) => RendererTriangle(r16, offset, scale, sample.Samples, sample.HeightResolution, sample.Half, x, z);
        var random = new Random(101);
        var rays = new Ray[1];
        var hits = new RayHit[1];
        var ground = new GroundSample[1];
        double worstVertical = 0, worstOblique = 0, worstOutside = 0;
        int badVertical = 0, badOblique = 0, above = 0, badOutside = 0, missed = 0;
        for (int i = 0; i < 3000; i++)
        {
            double x = -sample.Half + random.NextDouble() * sample.SizeM, z = -sample.Half + random.NextDouble() * sample.SizeM;
            bare.SampleGround(new[] { new XZ(x, z) }, ground);
            rays[0] = new Ray(new Double3(x, ground[0].TerrainHeight + 0.01 + 5 * random.NextDouble(), z), new Double3(0, -1, 0));
            bare.Raycast(rays, 10, hits);
            double error = Math.Abs(hits[0].Point.Y - ground[0].TerrainHeight);
            worstVertical = Math.Max(worstVertical, error);
            badVertical += error <= 1e-9 && hits[0].Object == -1 && hits[0].Material == Catalog.NoMaterial && hits[0].Surface == ground[0].Surface
                && hits[0].Normal.Y > 0 && Math.Abs(hits[0].Normal.Length() - 1) <= 1e-6 ? 0 : 1;
        }
        for (int i = 0; i < 2000; i++)
        {
            double x = -sample.Half + 20 + random.NextDouble() * (sample.SizeM - 40), z = -sample.Half + 20 + random.NextDouble() * (sample.SizeM - 40);
            Double3 d;
            do
                d = RandomUnit(random);
            while (d.Y > -0.3);
            var o = new Double3(x, H(x, z) + 0.5 + 4.5 * random.NextDouble(), z);
            rays[0] = new Ray(o, d);
            bare.Raycast(rays, 20, hits);
            if (!(hits[0].Distance <= 20))
            {
                missed++;
                continue;
            }
            Double3 p = hits[0].Point;
            double error = Math.Abs(p.Y - H(p.X, p.Z));
            worstOblique = Math.Max(worstOblique, error);
            badOblique += error <= 1e-9 ? 0 : 1;
            for (double t = 0; t < hits[0].Distance - 1e-6; t += 0.005)
            {
                Double3 q = o + d * t;
                above += q.Y - H(q.X, q.Z) >= -1e-9 ? 0 : 1;
            }
        }
        for (int i = 0; i < 200; i++)
        {
            double along = -sample.Half + random.NextDouble() * sample.SizeM, out_ = sample.Half + 1 + 30 * random.NextDouble();
            (double x, double z) = (i % 4) switch { 0 => (out_, along), 1 => (-out_, along), 2 => (along, out_), _ => (along, -out_) };
            bare.SampleGround(new[] { new XZ(x, z) }, ground);
            rays[0] = new Ray(new Double3(x, ground[0].TerrainHeight + 2, z), new Double3(0, -1, 0));
            bare.Raycast(rays, 10, hits);
            double error = Math.Abs(hits[0].Point.Y - ground[0].TerrainHeight);
            worstOutside = Math.Max(worstOutside, error);
            badOutside += error <= 1e-9 ? 0 : 1;
        }
        return Check("rays on the rendered terrain", badVertical == 0 && badOblique == 0 && above == 0 && badOutside == 0 && missed == 0,
            $"3000 vertical rays: hit y vs TerrainHeight within {worstVertical:0.0e0} m, {badVertical} bad (height, surface, object −1, "
            + $"unit upward normal); 2000 oblique rays: hit on the renderer's triangle within {worstOblique:0.0e0} m, {badOblique} bad, "
            + $"{above} samples below the terrain before the hit, {missed} missed; 200 vertical rays outside the map: within "
            + $"{worstOutside:0.0e0} m of SampleGround's edge terrain, {badOutside} bad (limit 1 nm)");
    }

    /// A 7.5 mm sphere 1 mm off a surface of each sample_patch asset, of the runtime shed (timber walls, sheet-metal roof
    /// by shape override) and of the launch rails: the contact names that object, that shape and its material.
    static bool MaterialsReported(WorldQuery world, Catalog catalog)
    {
        const double r = 0.0075, gap = r + 0.001;
        var shedAt = new Double3(-60, TerrainAt(world, -60, 40), 40);
        int shed = world.AddObject("test_tin_shed", shedAt, 25);
        int rails = world.AddObject("launch_rails", new Double3(RailStart.X, TerrainAt(world, RailStart.X, RailStart.Z), RailStart.Z), RailYaw);
        double[] shedRot = Rot(25, 0, 0), roofRot = MatMul(shedRot, Rot(0, 0, 8));
        Double3 roofTop = shedAt + Mul(shedRot, new Double3(0, 2.8, 0)) + Mul(roofRot, new Double3(0, 0.02, 0));
        Double3 roofUp = Mul(roofRot, new Double3(0, 1, 0)), wallSide = shedAt + Mul(shedRot, new Double3(2 + gap, 1, 0));
        Double3 railsAt = new(RailStart.X, TerrainAt(world, RailStart.X, RailStart.Z), RailStart.Z);
        var probes = new (string what, Double3 centre, int obj, int shape, string material)[]
        {
            ("house roof", new(52, 0.684 + 5 + gap, 30), 0, 0, "masonry"),
            ("shed roof", new(38, -0.017 + 2.3 + gap, 44), 1, 0, "timber"),
            ("gate top bar", new(30.5, 0.714 + 2.1 + gap, 20), 2, 2, "steel"),
            ("tree trunk", new(-40 + 0.1 + gap, 0.214 + 3, -50), 4, 0, "timber"),
            ("pole", new(-30 + 0.11 + gap, -1.053 + 4, 0), 7, 0, "timber"),
            ("cable mid-span", new(-15, (6.747 + 7.8) / 2 - 0.6 - 0.006 - gap, 0), 9, 0, "cable"),
            ("runtime shed roof", roofTop + roofUp * gap, shed, 1, "sheet_metal"),
            ("runtime shed wall", wallSide, shed, 0, "timber"),
            ("launch rail", railsAt + Mul(Rot(RailYaw, 0, 0), new Double3(0.13, 0.25 + gap, 0)), rails, 1, "steel"),
        };
        var buffer = new StaticContact[8];
        int bad = 0;
        string numbers = "";
        foreach (var (what, centre, obj, shape, material) in probes)
        {
            int n = world.StaticContacts(new Capsule(centre, centre, r), 0.005, buffer);
            int at = Array.FindIndex(buffer, 0, Math.Min(n, buffer.Length), c => c.Object == obj && c.Shape == shape);
            string got = at < 0 ? "none" : catalog.MaterialIds[buffer[at].Material];
            bad += got == material && Math.Abs(buffer[at].Distance - 0.001) <= 2e-4 ? 0 : 1;
            numbers += $"{what} → object {obj} shape {shape}: {got}{(at < 0 ? "" : $" at {buffer[at].Distance * 1000:0.000} mm")}; ";
        }
        return Check("material reported", bad == 0, numbers + $"{bad} wrong");
    }

    /// A ray cast straight down from 1 m above 1000 random points of the runtime shed's sloped sheet-metal roof, and above
    /// the middle of the sample_patch house (masonry, object 0): 1 m ± 1 mm, the material, the object index and the
    /// roof's normal.
    static bool RoofRay(WorldQuery world, Catalog catalog)
    {
        var shedAt = new Double3(-60, TerrainAt(world, -60, 40), 40);
        int shed = world.AddObject("test_tin_shed", shedAt, 25);
        double[] shedRot = Rot(25, 0, 0), roofRot = MatMul(shedRot, Rot(0, 0, 8));
        Double3 roofUp = Mul(roofRot, new Double3(0, 1, 0));
        ushort sheet = catalog.MaterialId("sheet_metal");
        var random = new Random(103);
        var rays = new Ray[1000];
        var hits = new RayHit[1000];
        for (int i = 0; i < rays.Length; i++)
        {
            var local = new Double3(-2.1 + 4.2 * random.NextDouble(), 0.02, -1.6 + 3.2 * random.NextDouble());
            Double3 p = shedAt + Mul(shedRot, new Double3(0, 2.8, 0)) + Mul(roofRot, local);
            rays[i] = new Ray(p + new Double3(0, 1, 0), new Double3(0, -1, 0));
        }
        world.Raycast(rays, 2, hits);
        double worst = 0, worstNormal = 0;
        int bad = 0;
        foreach (RayHit h in hits)
        {
            worst = Math.Max(worst, Math.Abs(h.Distance - 1));
            worstNormal = Math.Max(worstNormal, Angle(N(h.Normal), roofUp));
            bad += Math.Abs(h.Distance - 1) <= 1e-3 && h.Material == sheet && h.Object == shed && Angle(N(h.Normal), roofUp) <= 1e-6 ? 0 : 1;
        }
        var houseRay = new[] { new Ray(new Double3(52, 0.684 + 5 + 1, 30), new Double3(0, -1, 0)) };
        var houseHit = new RayHit[1];
        world.Raycast(houseRay, 2, houseHit);
        bool house = Math.Abs(houseHit[0].Distance - 1) <= 1e-3 && houseHit[0].Object == 0 && houseHit[0].Material == catalog.MaterialId("masonry");
        return Check("roof under a rotor", bad == 0 && house,
            $"1000 rays 1 m above the sloped sheet-metal roof of runtime object {shed}: distance 1 m within {worst:0.0e0} m (limit 1 mm), "
            + $"normal within {worstNormal:0.0e0} rad of the roof's, {bad} with the wrong distance, material, object or normal; "
            + $"above the house: {houseHit[0].Distance:0.000000000} m, object {houseHit[0].Object}, {catalog.MaterialIds[houseHit[0].Material]}");
    }

    /// The launch rails added at sample_patch's example start (x 12, z −30, yaw 90°) become object 10. An arm-like capsule
    /// (10 mm radius, 0.4 m) resting 0.1 mm into both bars returns exactly the two bars in order, steel, facing up, at
    /// −0.1 mm, and the same when it lands on them from 20 mm above in one step. A ray from 1 m above a bar hits it at
    /// 1 m, and the wind grid cell under the rails now holds them.
    static bool RailsAdded(WorldQuery world, Catalog catalog)
    {
        var start = new Double3(RailStart.X, TerrainAt(world, RailStart.X, RailStart.Z), RailStart.Z);
        int row = (int)Math.Floor((start.Z + world.Half) / 2), col = (int)Math.Floor((start.X + world.Half) / 2);
        WindCell before = world.WindGrid[row * world.WindCells + col];
        int rails = world.AddObject("launch_rails", start, RailYaw);
        WindCell after = world.WindGrid[row * world.WindCells + col];
        double[] m = Rot(RailYaw, 0, 0);
        const double r = 0.01, rest = 0.25 + r - 1e-4;
        Double3 a = start + Mul(m, new Double3(-0.2, rest, 0.05)), b = start + Mul(m, new Double3(0.2, rest, 0.05));
        var buffer = new StaticContact[8];
        int n = world.StaticContacts(new Capsule(a, b, r), 0.002, buffer);
        ushort steel = catalog.MaterialId("steel");
        bool Resting(int count) => count == 2 && buffer[0].Object == rails && buffer[1].Object == rails && buffer[0].Shape == 0
            && buffer[1].Shape == 1 && buffer.Take(2).All(c => c.Material == steel && Math.Abs(c.Distance + 1e-4) <= 1e-9
                && c.Normal.Y >= 1 - 1e-6 && c.Time == 1);
        bool resting = Resting(n);
        string numbers = $"{n} contacts: " + string.Join(", ", buffer.Take(Math.Min(n, 8)).Select(c =>
            $"object {c.Object} shape {c.Shape} {catalog.MaterialIds[c.Material]} at {c.Distance * 1000:0.0000} mm, normal y {c.Normal.Y:0.0000000}"));
        Double3 lift = new(0, 0.02, 0);
        int landed = world.StaticContacts(new Capsule(a + lift, b + lift, r), new Capsule(a, b, r), 0.002, buffer);
        bool landing = Resting(landed);
        var ray = new[] { new Ray(start + Mul(m, new Double3(-0.13, 1.25, 0.1)), new Double3(0, -1, 0)) };
        var hit = new RayHit[1];
        world.Raycast(ray, 2, hit);
        bool rayOk = Math.Abs(hit[0].Distance - 1) <= 1e-9 && hit[0].Object == rails && hit[0].Material == steel;
        bool wind = before.Porosity == 1 && after.TopM > 0 && after.Porosity < 1;
        return Check("rails added", rails == 10 && world.ObjectCount == 11 && resting && landing && rayOk && wind,
            $"rails are object {rails} of {world.ObjectCount}; resting arm: {numbers}; landing from 20 mm in one step: {landed} contacts "
            + $"({(landing ? "same" : "different")}); ray from 1 m above a bar: {hit[0].Distance:0.000000000} m, object {hit[0].Object}, "
            + $"{(hit[0].Material == Catalog.NoMaterial ? "terrain" : catalog.MaterialIds[hit[0].Material])}; wind cell under the "
            + $"rails before (top {before.TopM}, porosity {before.Porosity}), after (top {after.TopM:0.000}, porosity {after.Porosity:0.0000})");
    }

    /// World-query "Reset between flights" (API review F2). On sample_patch the launch rails, a gate and a 20 m wire are
    /// added, then the world is reset: the object count and the wind grid are the freshly loaded ones, bit for bit, and
    /// the gate's gap, the wire and the rails are gone from GapsNear, contacts and rays. The rails are added again: a
    /// 7.5 mm foot resting 0.1 mm into each bar gets exactly one contact, from that bar, and the wind grid equals, bit for
    /// bit, that of a fresh world with the rails added once. Without the reset a second set of rails gives each foot two
    /// contacts, for comparison.
    static bool ResetBetweenFlights(Func<WorldQuery> fresh)
    {
        WorldQuery world = fresh(), once = fresh(), twice = fresh();
        static byte[] Grid(WorldQuery w) => MemoryMarshal.AsBytes(w.WindGrid).ToArray();
        byte[] loaded = Grid(world);
        int loadedObjects = world.ObjectCount;
        var start = new Double3(RailStart.X, TerrainAt(world, RailStart.X, RailStart.Z), RailStart.Z);
        var gateAt = new Double3(-80, TerrainAt(world, -80, 80), 80);
        var wireMid = new Double3(-80, 8 - 0.5, 60);
        var gaps = new Gap[4];
        var buffer = new StaticContact[8];
        var ray = new[] { new Ray(start + Mul(Rot(RailYaw, 0, 0), new Double3(-0.13, 1.25, 0.1)), new Double3(0, -1, 0)) };
        var hit = new RayHit[1];
        world.AddObject("launch_rails", start, RailYaw);
        world.AddObject("gate_frame", gateAt, 0);
        int wire = world.AddWire("cable", new[] { new Double3(-90, 8, 60), new Double3(-70, 8, 60) }, 0.5, 0.012);
        int gapsAdded = world.GapsNear(gateAt + new Double3(0, 1, 0), 3, gaps);
        int wireAdded = world.StaticContacts(new Capsule(wireMid, wireMid, 0.05), 0.01, buffer);
        bool wireSeen = wireAdded == 1 && buffer[0].Object == wire;

        world.ResetRuntimeObjects();
        int gapsReset = world.GapsNear(gateAt + new Double3(0, 1, 0), 3, gaps);
        int wireReset = world.StaticContacts(new Capsule(wireMid, wireMid, 0.05), 0.01, buffer);
        world.Raycast(ray, 2, hit);
        bool cleared = world.ObjectCount == loadedObjects && Grid(world).AsSpan().SequenceEqual(loaded) && gapsAdded == 1 && gapsReset == 0
            && wireSeen && wireReset == 0 && hit[0].Object == -1;
        string clearedText = $"after the reset {world.ObjectCount} objects (loaded {loadedObjects}), wind grid "
            + $"{(Grid(world).AsSpan().SequenceEqual(loaded) ? "equal to" : "DIFFERENT from")} the loaded one, gate gaps {gapsAdded} → {gapsReset}, "
            + $"wire contacts {wireAdded} → {wireReset}, ray at a bar hits object {hit[0].Object}";

        int rails = world.AddObject("launch_rails", start, RailYaw);
        int onceRails = once.AddObject("launch_rails", start, RailYaw);
        twice.AddObject("launch_rails", start, RailYaw);
        twice.AddObject("launch_rails", start, RailYaw);
        const double r = 0.0075;
        bool feet = true;
        string feetText = "";
        for (int bar = 0; bar < 2; bar++)
        {
            Double3 foot = start + Mul(Rot(RailYaw, 0, 0), new Double3(bar == 0 ? -0.13 : 0.13, 0.25 + r - 1e-4, 0.05));
            int n = world.StaticContacts(new Capsule(foot, foot, r), 0.002, buffer);
            feet &= n == 1 && buffer[0].Object == rails && buffer[0].Shape == bar;
            string mine = string.Join(" and ", buffer.Take(Math.Min(n, buffer.Length)).Select(c => $"({c.Object}, {c.Shape})"));
            int doubled = twice.StaticContacts(new Capsule(foot, foot, r), 0.002, buffer);
            feetText += $"bar {bar}: {n} contact {mine}, without the reset {doubled}; ";
        }
        bool grid = Grid(world).AsSpan().SequenceEqual(Grid(once));
        return Check("reset between flights", cleared && rails == loadedObjects && onceRails == rails && feet && grid,
            $"rails, a gate and a wire added, then reset: {clearedText}. Rails added again as object {rails} (a single addition: "
            + $"{onceRails}); a 7.5 mm foot resting on each {feetText}wind grid {(grid ? "equal to" : "DIFFERENT from")} a single "
            + "addition's, bit for bit");
    }

    // ---------------------------------------------------------------- allocation, determinism, broadphase

    /// Fixed inputs around sample_patch's objects and the rails: swept capsule pairs, rays and gap queries.
    sealed class ObjectInputs
    {
        public Capsule[] Previous, Current;
        public Ray[] Rays;
        public Double3[] GapCentres;
        public StaticContact[] Contacts = new StaticContact[32];
        public RayHit[] Hits;
        public Gap[] Gaps = new Gap[4];
    }

    static ObjectInputs Inputs(WorldQuery world)
    {
        world.AddObject("launch_rails", new Double3(RailStart.X, TerrainAt(world, RailStart.X, RailStart.Z), RailStart.Z), RailYaw);
        Double3[] near = { new(52, 3, 30), new(38, 1.5, 44), new(30.5, 1.5, 20), new(-40, 3, -50), new(-30, 4, 0), new(-15, 6.6, 0), new(12, 0.3, -30) };
        var random = new Random(107);
        var inputs = new ObjectInputs { Previous = new Capsule[2000], Current = new Capsule[2000], Rays = new Ray[2000], Hits = new RayHit[2000], GapCentres = new Double3[50] };
        for (int i = 0; i < 2000; i++)
        {
            Double3 at = near[i % near.Length] + RandomUnit(random) * (3 * random.NextDouble());
            Double3 arm = RandomUnit(random) * (0.3 * random.NextDouble()), move = RandomUnit(random) * (0.05 * random.NextDouble());
            double r = 0.002 + 0.03 * random.NextDouble();
            inputs.Current[i] = new Capsule(at, at + arm, r);
            inputs.Previous[i] = new Capsule(at - move, at + arm - move, r);
            inputs.Rays[i] = new Ray(at, RandomUnit(random));
        }
        for (int i = 0; i < inputs.GapCentres.Length; i++)
            inputs.GapCentres[i] = new Double3(30.5, 1.7, 20) + RandomUnit(random) * (15 * random.NextDouble());
        return inputs;
    }

    static ulong Mix(ulong hash, ulong value)
    {
        for (int i = 0; i < 8; i++)
            hash = (hash ^ (value >> 8 * i & 0xFF)) * 1099511628211UL;
        return hash;
    }

    static ulong Bits(double v) => (ulong)BitConverter.DoubleToInt64Bits(v);

    /// FNV-1a over every contact, hit, gap and wind cell; allocates nothing.
    static ulong ObjectHash(WorldQuery world, ObjectInputs inputs)
    {
        ulong hash = 14695981039346656037UL;
        for (int i = 0; i < inputs.Current.Length; i++)
        {
            int n = world.StaticContacts(inputs.Previous[i], inputs.Current[i], 0.02, inputs.Contacts);
            hash = Mix(hash, (ulong)n);
            foreach (byte b in MemoryMarshal.AsBytes(inputs.Contacts.AsSpan(0, Math.Min(n, inputs.Contacts.Length))))
                hash = (hash ^ b) * 1099511628211UL;
        }
        world.Raycast(inputs.Rays, 2, inputs.Hits);
        foreach (byte b in MemoryMarshal.AsBytes(inputs.Hits.AsSpan()))
            hash = (hash ^ b) * 1099511628211UL;
        foreach (Double3 centre in inputs.GapCentres)
        {
            int n = world.GapsNear(centre, 6, inputs.Gaps);
            hash = Mix(hash, (ulong)n);
            for (int i = 0; i < Math.Min(n, inputs.Gaps.Length); i++)
            {
                Gap g = inputs.Gaps[i];
                hash = Mix(Mix(Mix(hash, Bits(g.Center.X)), Bits(g.Center.Y)), Bits(g.Center.Z));
                hash = Mix(Mix(Mix(hash, Bits(g.Normal.X)), Bits(g.Normal.Y)), Bits(g.Normal.Z));
                hash = Mix(Mix(Mix(hash, Bits(g.Up.X)), Bits(g.Up.Y)), Bits(g.Up.Z));
                hash = Mix(Mix(Mix(hash, Bits(g.Width)), Bits(g.Height)), (ulong)g.Object);
                foreach (char ch in g.Name)
                    hash = Mix(hash, ch);
            }
        }
        foreach (byte b in MemoryMarshal.AsBytes(world.WindGrid))
            hash = (hash ^ b) * 1099511628211UL;
        return hash;
    }

    /// The replay digest's object part: a freshly loaded sample_patch with the rails, in either process.
    static ulong ObjectDigest()
    {
        WorldQuery world = WorldQuery.Load(ProjectSettings.GlobalizePath(Package), ProjectSettings.GlobalizePath(SurfacesPath),
            ProjectSettings.GlobalizePath(CatalogPath));
        return ObjectHash(world, Inputs(world));
    }

    /// 2000 swept capsules, 2000 rays, 50 gap queries and the whole wind grid, three times after a warm-up: the second
    /// run allocates nothing on the managed heap, and all three give the same bits.
    static bool NoAllocationSameBits(WorldQuery world)
    {
        ObjectInputs inputs = Inputs(world);
        ulong first = ObjectHash(world, inputs);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ulong second = ObjectHash(world, inputs);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ulong third = ObjectHash(world, inputs);
        return Check("allocation-free and bit-identical", allocated == 0 && first == second && second == third,
            $"2000 swept StaticContacts, 2000 rays, 50 GapsNear and the wind grid around sample_patch's objects and the rails: "
            + $"{allocated} bytes allocated after warm-up; hashes {first:x16}, {second:x16}, {third:x16}");
    }

    /// Queries over open meadow far from every object return nothing and take the broadphase's fast path; informational
    /// timings of the object queries follow (the formal benchmark is tools/worldbench, in Release).
    static bool FarFromEverything(WorldQuery world)
    {
        ObjectInputs inputs = Inputs(world);
        var random = new Random(109);
        var far = new Capsule[1000];
        for (int i = 0; i < far.Length; i++)
        {
            var at = new Double3(-120 + 40 * random.NextDouble(), 1 + 3 * random.NextDouble(), 70 + 50 * random.NextDouble());
            far[i] = new Capsule(at, at + RandomUnit(random) * 0.3, 0.02);
        }
        var buffer = new StaticContact[32];
        int found = 0;
        foreach (Capsule c in far)
            found += world.StaticContacts(c, c, 0.05, buffer);
        var watch = Stopwatch.StartNew();
        for (int k = 0; k < 100; k++)
        {
            foreach (Capsule c in far)
                world.StaticContacts(c, c, 0.05, buffer);
        }
        double farUs = watch.Elapsed.TotalMilliseconds * 1000 / (100.0 * far.Length);
        watch.Restart();
        for (int k = 0; k < 50; k++)
        {
            for (int i = 0; i < inputs.Current.Length; i++)
                world.StaticContacts(inputs.Current[i], 0.02, buffer);
        }
        double nearUs = watch.Elapsed.TotalMilliseconds * 1000 / (50.0 * inputs.Current.Length);
        watch.Restart();
        for (int k = 0; k < 50; k++)
        {
            for (int i = 0; i < inputs.Current.Length; i++)
                world.StaticContacts(inputs.Previous[i], inputs.Current[i], 0.02, buffer);
        }
        double sweptUs = watch.Elapsed.TotalMilliseconds * 1000 / (50.0 * inputs.Current.Length);
        watch.Restart();
        for (int k = 0; k < 50; k++)
            world.Raycast(inputs.Rays, 2, inputs.Hits);
        double rayUs = watch.Elapsed.TotalMilliseconds * 1000 / (50.0 * inputs.Rays.Length);
#if DEBUG
        const string build = "Debug";
#else
        const string build = "Release";
#endif
        return Check("far from everything", found == 0,
            $"1000 capsules over open meadow: {found} contacts. Timings ({build} build, informational; the benchmark is tools/worldbench): "
            + $"far {farUs:0.000} µs, near objects static {nearUs:0.000} µs and swept {sweptUs:0.000} µs per capsule, 2 m rays "
            + $"{rayUs:0.000} µs each (budget: 100,000 capsules or rays in 100 ms, 1 µs each)");
    }
}
