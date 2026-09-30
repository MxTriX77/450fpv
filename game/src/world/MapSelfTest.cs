using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

/// The map-loading scenarios (define-map-format 4.1 and 4.2):
/// - `-- --map sample_patch --selftest map`: the loaded map against objects.json and the world query (poses, wires,
///   colliders and their material tags, the wind grid, one layer per surface, stem parity), then broken packages. Headless.
/// - `-- --map <bad id> --selftest map-fallback`: the sandbox kept its ground placeholder. Headless.
/// - `-- --map <id> --selftest map-view --view x,h,z,yaw,pitch`: windowed. Puts the camera h m above the terrain at (x, z)
///   with yaw and pitch in degrees, waits for the micro-detail, measures fps for 3 s with vsync off and saves a
///   screenshot (F12). About 10 s.
/// Each prints PASS/FAIL lines with numbers and exits non-zero on failure.
public static class MapSelfTest
{
    const string Package = "res://maps/sample_patch";

    public static async void Run(Sandbox sandbox)
    {
        SceneTree tree = sandbox.GetTree();
        bool pass = false;
        try
        {
            MapScene map = sandbox.Map ?? throw new InvalidOperationException("no map loaded: run it with -- --map sample_patch");
            string dir = ProjectSettings.GlobalizePath(Package);
            for (int i = 0; i < 3; i++)
                await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
            pass = StartsAboveCentre(sandbox, map);
            pass &= ObjectsAtPoses(map, dir);
            pass &= WiresOnPolyline(map, dir);
            pass &= CollidersTagged(map, dir, sandbox.GetWorld3D().DirectSpaceState);
            pass &= WindFed(map);
            pass &= SurfacesBound(map);
            pass &= FillersFollowTheVisual(map);
            pass &= CollisionFollowsTheVisual(map);
            pass &= GapsArePassable(map, sandbox.GetWorld3D().DirectSpaceState);
            pass &= await StemParity(sandbox, map);
            pass &= BadPackages(dir);
        }
        catch (Exception e)
        {
            GD.PrintErr($"ERROR: selftest map: {e}");
            pass = false;
        }
        GD.Print($"selftest map: {(pass ? "ALL PASS" : "FAILED")}");
        tree.Quit(pass ? 0 : 1);
    }

    public static bool Fallback(Sandbox sandbox)
    {
        bool placeholder = sandbox.Map == null && sandbox.GetNode<Node3D>("Ground").Visible
            && !sandbox.GetChildren().OfType<MapScene>().Any();
        return Check("fallback", placeholder, $"no map in the scene, ground placeholder visible: {placeholder}");
    }

    public static async void View(Sandbox sandbox, string view)
    {
        SceneTree tree = sandbox.GetTree();
        bool pass = false;
        try
        {
            if (DisplayServer.GetName() == "headless")
                throw new InvalidOperationException("needs a window, run it without --headless");
            MapScene map = sandbox.Map ?? throw new InvalidOperationException("no map loaded");
            double[] v = (view ?? throw new InvalidOperationException("needs --view x,h,z,yaw,pitch")).Split(',').Select(double.Parse).ToArray();
            Input.MouseMode = Input.MouseModeEnum.Visible;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            var camera = sandbox.GetNode<NoclipCamera>("Camera");
            camera.GlobalPosition = new Vector3((float)v[0], (float)(Terrain(map.World, v[0], v[2]) + v[1]), (float)v[2]);
            camera.Rotation = new Vector3(Mathf.DegToRad((float)v[4]), Mathf.DegToRad((float)v[3]), 0);
            var detail = map.GetNode<MicroDetailView>("MicroDetail");
            ulong settleStart = Time.GetTicksMsec();
            await Settle(tree, detail, settleStart + 10000);
            ulong settled = Time.GetTicksMsec() - settleStart;
            await tree.ToSignal(tree.CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);

            var frames = new List<double>();
            ulong start = Time.GetTicksUsec(), last = start;
            while (Time.GetTicksUsec() - start < 3_000_000)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                ulong now = Time.GetTicksUsec();
                frames.Add((now - last) / 1000.0);
                last = now;
            }
            double[] sorted = frames.OrderBy(f => f).ToArray();
            int slowest = Math.Max(1, sorted.Length / 100);
            int instances = detail.GetChildren().OfType<MultiMeshInstance3D>().Sum(t => t.Multimesh.InstanceCount);
            GD.Print($"selftest map-view: {view}: micro-detail settled in {settled} ms, {detail.GetChildCount()} tiles, {instances} elements; "
                + $"{DisplayServer.WindowGetSize()} window, vsync off, 3 s: {1000.0 * sorted.Length / sorted.Sum():0} fps avg, "
                + $"1 % low {1000.0 * slowest / sorted[^slowest..].Sum():0} fps, "
                + $"{Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} draw calls, "
                + $"{Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)} primitives");
            GD.Print($"selftest map-view: GPU {RenderingServer.GetVideoAdapterName()}, {RenderingServer.GetCurrentRenderingMethod()}");

            string before = sandbox.LastScreenshot;
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.F12, PhysicalKeycode = Key.F12, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.F12, PhysicalKeycode = Key.F12, Pressed = false });
            for (int i = 0; i < 3; i++)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            pass = Check("view screenshot", sandbox.LastScreenshot != before, sandbox.LastScreenshot);
        }
        catch (Exception e)
        {
            GD.PrintErr($"ERROR: selftest map-view: {e.Message}");
        }
        tree.Quit(pass ? 0 : 1);
    }

    /// Waits until the micro-detail around the camera's new position is drawn, or the deadline (Time.GetTicksMsec) passes.
    /// The first two frames let the view see the new position; Settled can be left over from the old one.
    static async Task Settle(SceneTree tree, MicroDetailView detail, ulong deadline)
    {
        for (int i = 0; i < 2; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        while (!detail.Settled && Time.GetTicksMsec() < deadline)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    static bool Check(string scenario, bool ok, string numbers)
    {
        GD.Print($"selftest map: {scenario}: {numbers} {(ok ? "PASS" : "FAIL")}");
        return ok;
    }

    static double Terrain(WorldQuery world, double x, double z)
    {
        var samples = new GroundSample[1];
        world.SampleGround(new[] { new XZ(x, z) }, samples);
        return samples[0].TerrainHeight;
    }

    static Vector3 V(JsonNode a) => new((float)a[0].GetValue<double>(), (float)a[1].GetValue<double>(), (float)a[2].GetValue<double>());

    static JsonArray Objects(string dir) => JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "objects.json")))["objects"].AsArray();

    /// The node under Objects that draws object `index` (not its collision body).
    static Node3D Visual(MapScene map, int index) =>
        map.GetNode("Objects").GetChildren().OfType<Node3D>().Single(n => n is not StaticBody3D && (int)n.GetMeta("object") == index);

    // ---------------------------------------------------------------- scenarios

    /// Fly the sample: the noclip camera starts above the map centre.
    static bool StartsAboveCentre(Sandbox sandbox, MapScene map)
    {
        Vector3 p = sandbox.GetNode<Camera3D>("Camera").GlobalPosition;
        double above = p.Y - Terrain(map.World, 0, 0);
        return Check("fly the sample", Math.Abs(p.X) < 0.01 && Math.Abs(p.Z) < 0.01 && Math.Abs(above - Sandbox.MapStartHeight) < 0.01
            && !sandbox.GetNode<Node3D>("Ground").Visible, $"camera at {p}, {above:0.000} m above the terrain at the centre, ground placeholder hidden");
    }

    /// Sample patch: every object of objects.json at its pose within 1 cm, from Godot's own Euler math. The pose error is
    /// the largest displacement of the corners of a 10 m cube round the object's origin.
    static bool ObjectsAtPoses(MapScene map, string dir)
    {
        JsonArray objects = Objects(dir);
        double worst = 0;
        int checkedCount = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            JsonNode o = objects[i];
            if (o["points_m"] != null)
                continue; // a wire: WiresOnPolyline
            Vector3 r = V(o["rotation_deg"]) * (Mathf.Pi / 180f);
            Basis expected = Basis.FromEuler(new Vector3(r.Y, r.X, r.Z), EulerOrder.Yxz).Scaled(Vector3.One * (float)o["scale"].GetValue<double>());
            Transform3D drawn = Visual(map, i).GlobalTransform;
            for (int c = 0; c < 8; c++)
            {
                var corner = new Vector3(c & 1, c >> 1 & 1, c >> 2 & 1) * 10f - Vector3.One * 5f;
                worst = Math.Max(worst, (drawn * corner).DistanceTo(V(o["position_m"]) + expected * corner));
            }
            checkedCount++;
        }
        return Check("sample patch: objects at their poses", worst <= 0.01, $"{checkedCount} objects, largest corner error {worst * 1000:0.000} mm (limit 10 mm)");
    }

    /// Each wire drawn along the README's sag polyline, computed here from objects.json and the catalog.
    static bool WiresOnPolyline(MapScene map, string dir)
    {
        JsonArray objects = Objects(dir);
        JsonNode catalog = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/catalog.json")));
        double worst = 0;
        int wires = 0, pieces = 0;
        bool counts = true;
        for (int i = 0; i < objects.Count; i++)
        {
            JsonNode o = objects[i];
            if (o["points_m"] == null)
                continue;
            double segment = catalog["assets"][o["asset"].GetValue<string>()]["collision"][0]["segment_m"].GetValue<double>();
            double sag = o["sag_m"].GetValue<double>();
            var expected = new List<Vector3> { V(o["points_m"][0]) };
            JsonArray points = o["points_m"].AsArray();
            for (int s = 0; s + 1 < points.Count; s++)
            {
                Vector3 a = V(points[s]), b = V(points[s + 1]);
                int steps = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / segment));
                for (int k = 1; k <= steps; k++)
                {
                    float t = (float)k / steps;
                    expected.Add(a.Lerp(b, t) - Vector3.Up * (float)(4 * sag * t * (1 - t)));
                }
            }
            Node3D[] drawn = Visual(map, i).GetChildren().OfType<Node3D>().ToArray();
            counts &= drawn.Length == expected.Count - 1;
            for (int k = 0; k < Math.Min(drawn.Length, expected.Count - 1); k++)
            {
                Transform3D t = drawn[k].GlobalTransform;
                worst = Math.Max(worst, Math.Max(t.Origin.DistanceTo(expected[k]), (t.Origin + t.Basis.Z).DistanceTo(expected[k + 1])));
            }
            wires++;
            pieces += drawn.Length;
        }
        return Check("sample patch: wires on the sag polyline", counts && wires > 0 && worst <= 0.01,
            $"{wires} wire(s), {pieces} pieces, piece counts as expected: {counts}, largest end error {worst * 1000:0.000} mm (limit 10 mm)");
    }

    /// Every Jolt collision shape where the world query has it, tagged with the catalog material of its asset or shape; a
    /// visual-only object has none. A wire is a capsule per piece of World.WirePoints, centred on the piece, along it,
    /// with the wire's radius. Then Jolt rays from above land on the house roof and the cable and report their tags.
    static bool CollidersTagged(MapScene map, string dir, PhysicsDirectSpaceState3D space)
    {
        JsonArray objects = Objects(dir);
        JsonNode assets = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://assets/catalog.json")))["assets"];
        WorldQuery world = map.World;
        double worst = 0;
        int shapes = 0, wrongTags = 0, bodies = 0, wrongPieces = 0;
        foreach (StaticBody3D body in map.GetNode("Objects").GetChildren().OfType<StaticBody3D>())
        {
            int obj = (int)body.GetMeta("object"), piece = 0;
            JsonNode asset = assets[objects[obj]["asset"].GetValue<string>()];
            Vector3[] polyline = world.WirePoints(obj).ToArray().Select(p => new Vector3((float)p.X, (float)p.Y, (float)p.Z)).ToArray();
            bodies++;
            foreach (CollisionShape3D node in body.GetChildren().OfType<CollisionShape3D>())
            {
                int shape = (int)node.GetMeta("shape");
                world.Geometry(obj, shape, 0, out ShapeGeometry g);
                JsonNode def = asset["collision"][shape];
                string material = def["material"]?.GetValue<string>() ?? asset["material"].GetValue<string>();
                wrongTags += (string)node.GetMeta("material") == material && (int)node.GetMeta("material_id") == g.Material ? 0 : 1;
                if (g.Kind == ShapeKind.Wire)
                {
                    Vector3 p0 = polyline[piece], p1 = polyline[piece + 1];
                    var capsule = (CapsuleShape3D)node.Shape;
                    worst = Math.Max(worst, Math.Max(node.GlobalPosition.DistanceTo((p0 + p1) / 2),
                        (node.GlobalBasis.Y * (p0.DistanceTo(p1) / 2)).DistanceTo((p1 - p0) / 2)));
                    wrongPieces += Math.Abs(capsule.Radius - g.Diameter / 2) < 1e-6 && Math.Abs(capsule.Height - p0.DistanceTo(p1) - g.Diameter) < 1e-5 ? 0 : 1;
                    piece++;
                }
                else
                {
                    worst = Math.Max(worst, node.GlobalPosition.DistanceTo(new Vector3((float)g.Center.X, (float)g.Center.Y, (float)g.Center.Z)));
                }
                shapes++;
            }
            wrongPieces += piece == Math.Max(polyline.Length - 1, 0) ? 0 : 1;
        }
        int expectedBodies = objects.Count(o => assets[o["asset"].GetValue<string>()]["visual_only"]?.GetValue<bool>() != true);
        bool placed = Check("colliders at the world query's shapes, tagged",
            bodies == expectedBodies && wrongTags == 0 && wrongPieces == 0 && worst <= 0.01,
            $"{bodies} bodies (expected {expectedBodies}), {shapes} shapes, {wrongTags} wrong material tags, {wrongPieces} wrong wire capsules, "
            + $"largest centre or axis error {worst * 1000:0.000} mm (limit 10 mm)");

        int house = objects.IndexOf(objects.First(o => o["asset"].GetValue<string>() == "house_box"));
        int cable = objects.IndexOf(objects.First(o => o["asset"].GetValue<string>() == "cable"));
        Vector3 housePos = V(objects[house]["position_m"]);
        Vector3 a = V(objects[cable]["points_m"][0]), b = V(objects[cable]["points_m"][1]);
        Vector3 mid = (a + b) / 2 - Vector3.Up * (float)objects[cable]["sag_m"].GetValue<double>();
        // Jolt's ray casts on millimetre capsules land up to a radius off (measured: a 6 mm capsule lying across a
        // downward ray is hit at its axis), so the heights are checked to 1 cm; the capsules themselves are checked above.
        bool rays = true;
        foreach ((string name, Vector3 at, float top, int obj, string material) in new[]
            { ("house roof", housePos, housePos.Y + 5f, house, "masonry"), ("cable mid-span", mid, mid.Y + 0.006f, cable, "cable") })
        {
            Godot.Collections.Dictionary hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(new Vector3(at.X, 60, at.Z), new Vector3(at.X, -60, at.Z)));
            bool ok = hit.Count > 0;
            string numbers = "no hit";
            if (ok)
            {
                var collider = (CollisionObject3D)hit["collider"];
                var owner = (CollisionShape3D)collider.ShapeOwnerGetOwner(collider.ShapeFindOwner((int)hit["shape"]));
                float y = ((Vector3)hit["position"]).Y;
                ok = (int)collider.GetMeta("object") == obj && (string)owner.GetMeta("material") == material && Math.Abs(y - top) <= 0.01f;
                numbers = $"hit object {collider.GetMeta("object")} ({owner.GetMeta("material")}) at y {y:0.000}, expected object {obj} ({material}) at {top:0.000} ± 0.010";
            }
            rays &= Check($"Jolt ray onto the {name}", ok, numbers);
        }
        return placed & rays;
    }

    /// Wind volumes fed into the wind grid: the cell under the house is a solid 5 m block and the one under a tree's
    /// crown centre has the crown's base and top (4 and 10 m above the ground).
    static bool WindFed(MapScene map)
    {
        WorldQuery w = map.World;
        WindCell Cell(double x, double z) =>
            w.WindGrid[(int)Math.Floor((z + w.Half) / WorldQuery.WindCellSize) * w.WindCells + (int)Math.Floor((x + w.Half) / WorldQuery.WindCellSize)];
        // The belt cell holds tree_acacia's crown (its top, 11.6 m) over the shrub understory (its base, near the
        // ground), and both are porous: the wind goes through a belt, it does not go round it.
        WindCell house = Cell(52, 30), tree = Cell(-40, -50), open = Cell(-100, 100);
        return Check("wind volumes in the wind grid", Math.Abs(house.TopM - 5) < 0.2 && house.Porosity < 0.1 && tree.BaseM < 1.5
            && Math.Abs(tree.TopM - 11.6) < 0.4 && 0 < tree.Porosity && tree.Porosity < 0.9 && open.TopM == 0 && open.Porosity == 1,
            $"house cell top {house.TopM:0.00} m porosity {house.Porosity:0.000}; tree cell base {tree.BaseM:0.00} top {tree.TopM:0.00} m porosity "
            + $"{tree.Porosity:0.000}; open cell top {open.TopM} porosity {open.Porosity}");
    }

    /// The terrain draws with the surface looks: every surface in the patch has a texture set, and the albedo, normal,
    /// height and AO arrays have one layer per set (the eye judges "visibly distinct" on a windowed screenshot, map-view).
    static bool SurfacesBound(MapScene map)
    {
        ShaderMaterial material = map.GetNode<HeightmapTerrain>("Terrain").Material;
        byte[] used = map.World.SurfaceIds.ToArray().Distinct().OrderBy(i => i).ToArray();
        int[] layers = new[] { "set_albedo", "set_normal", "set_height", "set_ao" }
            .Select(name => ((Texture2DArray)material.GetShaderParameter(name)).GetLayers()).ToArray();
        SurfaceLook look = map.Look;
        return Check("sample patch: a texture set per surface", (bool)material.GetShaderParameter("use_surfaces")
            && layers.All(n => n == look.Sets.Length) && used.All(i => look.Layer[i] >= 0 && look.Layer[i] < look.Sets.Length),
            $"{used.Length} surfaces in the patch ({string.Join(", ", used.Select(i => $"{map.World.Surface(i).Id} {look.Sets[look.Layer[i]]}"))}), "
            + $"albedo, normal, height and AO arrays of {string.Join(", ", layers)} layers for {look.Sets.Length} sets");
    }

    /// Visual and physical stems agree: with the camera over belt_straw, the drawn element bases within 3 m of it are the
    /// MicroDetailNear bases within 3 m, each within 1 mm.
    static async Task<bool> StemParity(Sandbox sandbox, MapScene map)
    {
        const double Range = 3, Tolerance = 0.001;
        SceneTree tree = sandbox.GetTree();
        WorldQuery world = map.World;
        var eye = new Double3(0, Terrain(world, 0, -47) + 1.0, -47);
        var probe = new GroundSample[1];
        world.SampleGround(new[] { new XZ(eye.X, eye.Z) }, probe);
        var camera = sandbox.GetNode<Camera3D>("Camera");
        camera.GlobalPosition = new Vector3((float)eye.X, (float)eye.Y, (float)eye.Z);
        var detail = map.GetNode<MicroDetailView>("MicroDetail");
        ulong deadline = Time.GetTicksMsec() + 20000;
        await Settle(tree, detail, deadline);

        var buffer = new MicroElement[1 << 16];
        int n = world.MicroDetailNear(eye, Range, KindMask.All, buffer);
        var reference = new List<(Double3 Base, CoverKind Kind)>();
        for (int i = 0; i < Math.Min(n, buffer.Length); i++)
        {
            if ((buffer[i].Base - eye).Length() <= Range)
                reference.Add((buffer[i].Base, buffer[i].Kind));
        }

        var drawn = new List<Double3>();
        int tiles = 0, total = 0;
        foreach (MultiMeshInstance3D tile in detail.GetChildren().OfType<MultiMeshInstance3D>())
        {
            float[] data = tile.Multimesh.Buffer;
            Vector3 origin = tile.Position;
            tiles++;
            total += tile.Multimesh.InstanceCount;
            for (int k = 0; k + 15 < data.Length; k += 16)
            {
                var b = new Double3((double)origin.X + data[k + 3], (double)origin.Y + data[k + 7], (double)origin.Z + data[k + 11]);
                if ((b - eye).Length() <= Range + Tolerance)
                    drawn.Add(b);
            }
        }
        // Match on a 1 cm grid: each reference base to a drawn one within the tolerance, and back.
        var grid = drawn.GroupBy(Cell).ToDictionary(g => g.Key, g => g.ToList());
        double worst = 0;
        int unmatched = 0;
        foreach ((Double3 b, _) in reference)
        {
            double best = double.MaxValue;
            (long x, long y, long z) = Cell(b);
            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                    for (long dz = -1; dz <= 1; dz++)
                        if (grid.TryGetValue((x + dx, y + dy, z + dz), out List<Double3> near))
                            best = Math.Min(best, near.Min(d => (d - b).Length()));
            unmatched += best <= Tolerance ? 0 : 1;
            worst = Math.Max(worst, best);
        }
        int extra = drawn.Count(d => (d - eye).Length() <= Range - Tolerance
            && !reference.Any(r => (r.Base - d).Length() <= Tolerance));
        string kinds = string.Join(", ", reference.GroupBy(r => r.Kind).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}"));
        return Check("visual and physical stems agree", probe[0].Surface == 4 && detail.Settled && reference.Count > 0 && unmatched == 0 && extra == 0,
            $"camera 1 m over {world.Surface(probe[0].Surface).Id} at ({eye.X}, {eye.Z}); {tiles} tiles drawn, {total} elements; "
            + $"MicroDetailNear r {Range} m: {reference.Count} bases within {Range} m ({kinds}), {unmatched} not drawn, largest error "
            + $"{worst * 1000:0.0000} mm (limit 1 mm); {extra} drawn bases within {Range} m that physics does not have");
    }

    static (long, long, long) Cell(Double3 p) => ((long)Math.Floor(p.X * 100), (long)Math.Floor(p.Y * 100), (long)Math.Floor(p.Z * 100));

    /// Hole-filler rule F-6: every corner a filler draws lies on the surface of one of its own collision shapes, within
    /// 0.05 m, so the cavity's open faces are the drawn ones. Stronger than the rule's three ray heights, because it
    /// covers every face. Both the trench's placeholder boxes and the cellar shaft's mesh are built as the boxes they
    /// collide as, so the answer is 0 mm.
    static bool FillersFollowTheVisual(MapScene map)
    {
        WorldQuery world = map.World;
        int objects = 0, corners = 0, wrong = 0;
        double worst = 0;
        string first = "";
        for (int i = 0; i < world.ObjectCount; i++)
        {
            if (!IsFiller(world, i))
                continue;
            objects++;
            foreach ((Vector3 a, Vector3 b, Vector3 c) in DrawnTriangles(map, i))
            {
                foreach (Vector3 corner in new[] { a, b, c })
                {
                    corners++;
                    double off = ToShapes(world, i, corner);
                    worst = Math.Max(worst, off);
                    if (off > 0.05 && wrong++ == 0)
                        first = $"; first corner {corner} off by {off:0.000} m";
                }
            }
        }
        return Check("hole fillers: F-6 open faces follow the visual", objects > 0 && corners > 0 && wrong == 0,
            $"{objects} filler objects, {corners} drawn corners: each within {worst * 1000:0.0} mm of the surface of one of "
            + $"its own collision shapes (limit 50 mm), {wrong} further off{first}");
    }

    /// A hole filler, from the data: every shape is soil, meaning a surface and no catalog material (F-7).
    static bool IsFiller(WorldQuery world, int obj)
    {
        bool soil = false, other = false;
        for (int shape = 0; world.Geometry(obj, shape, 0, out ShapeGeometry g); shape++)
        {
            if (g.Surface != 0 && g.Material == Catalog.NoMaterial)
                soil = true;
            else
                other = true;
        }
        return soil && !other;
    }

    // ---------------------------------------------------------------- the built assets (task 3.2)

    /// Every asset under this folder is a solid structure built from boxes, so collision can be checked against the
    /// drawn mesh directly. Vegetation is left out on purpose: a crown is loose alpha cards with collision only on the
    /// trunk and the primary limbs, a simplification its builders list.
    const string Structures = "res://assets/models/structures/";
    /// Material names that are drawn but deliberately not collided, each listed in its builder's docstring: soft sheet
    /// a drone tears through or snags on rather than stops against.
    static readonly string[] NotCollided = { "tarp_blue" };

    /// The world-space triangles an object draws at LOD0, less the surfaces of NotCollided.
    static List<(Vector3 A, Vector3 B, Vector3 C)> DrawnTriangles(MapScene map, int index)
    {
        var tris = new List<(Vector3, Vector3, Vector3)>();
        foreach (MeshInstance3D drawn in Visual(map, index).GetChildren().OfType<MeshInstance3D>())
        {
            string name = drawn.Name.ToString();
            if (name.Contains("_LOD") && !name.EndsWith("_LOD0", StringComparison.Ordinal))
                continue;
            Transform3D to = drawn.GlobalTransform;
            for (int s = 0; s < drawn.Mesh.GetSurfaceCount(); s++)
            {
                if (Array.IndexOf(NotCollided, drawn.Mesh.SurfaceGetMaterial(s)?.ResourceName) >= 0)
                    continue;
                Godot.Collections.Array arrays = drawn.Mesh.SurfaceGetArrays(s);
                var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var index3 = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                for (int i = 0; i + 2 < index3.Length; i += 3)
                    tris.Add((to * verts[index3[i]], to * verts[index3[i + 1]], to * verts[index3[i + 2]]));
            }
        }
        return tris;
    }

    /// The distance from a point to the nearest surface of the object's own collision boxes, 0 on a face.
    static double ToShapes(WorldQuery world, int obj, Vector3 p)
    {
        double best = double.PositiveInfinity;
        var point = new Double3(p.X, p.Y, p.Z);
        for (int shape = 0; world.Geometry(obj, shape, 0, out ShapeGeometry g); shape++)
        {
            if (g.Kind != ShapeKind.Box)
                continue;
            Double3 local = g.Axes.ToLocal(point - g.Center), half = g.HalfExtents;
            double dx = Math.Abs(local.X) - half.X, dy = Math.Abs(local.Y) - half.Y, dz = Math.Abs(local.Z) - half.Z;
            double off = dx <= 0 && dy <= 0 && dz <= 0
                ? Math.Min(-dx, Math.Min(-dy, -dz))
                : Math.Sqrt(Math.Max(dx, 0) * Math.Max(dx, 0) + Math.Max(dy, 0) * Math.Max(dy, 0) + Math.Max(dz, 0) * Math.Max(dz, 0));
            best = Math.Min(best, off);
        }
        return best;
    }

    /// The distance along `dir` (unit) to the nearest of the object's own collision boxes, +∞ for a miss. `boxesOnly`
    /// is false when the object has a shape this cannot handle, which for a structure would be a mistake.
    static double RayShapes(WorldQuery world, int obj, Double3 from, Double3 dir, out bool boxesOnly)
    {
        boxesOnly = true;
        double best = double.PositiveInfinity;
        for (int shape = 0; world.Geometry(obj, shape, 0, out ShapeGeometry g); shape++)
        {
            if (g.Kind != ShapeKind.Box)
            {
                boxesOnly = false;
                continue;
            }
            Double3 o = g.Axes.ToLocal(from - g.Center), d = g.Axes.ToLocal(dir), half = g.HalfExtents;
            double near = 0, far = double.PositiveInfinity;
            for (int a = 0; a < 3; a++)
            {
                double oa = a == 0 ? o.X : a == 1 ? o.Y : o.Z;
                double da = a == 0 ? d.X : a == 1 ? d.Y : d.Z;
                // A micron of slack, so a ray that grazes the very edge of a box cannot be counted by the triangle test
                // and missed by this one.
                double h = (a == 0 ? half.X : a == 1 ? half.Y : half.Z) + 1e-6;
                if (Math.Abs(da) < 1e-12)
                {
                    if (Math.Abs(oa) > h)
                        far = -1;
                    continue;
                }
                double lo = (-h - oa) / da, hi = (h - oa) / da;
                if (lo > hi)
                    (lo, hi) = (hi, lo);
                near = Math.Max(near, lo);
                far = Math.Min(far, hi);
            }
            if (near <= far && far >= 0)
                best = Math.Min(best, Math.Max(near, 0));
        }
        return best;
    }

    /// The distance along `dir` (unit) to the nearest triangle of `tris`, +∞ for a miss (Möller-Trumbore).
    static double RayTriangles(List<(Vector3 A, Vector3 B, Vector3 C)> tris, Double3 from, Double3 dir)
    {
        double best = double.PositiveInfinity;
        foreach ((Vector3 a, Vector3 b, Vector3 c) in tris)
        {
            Double3 e1 = new(b.X - a.X, b.Y - a.Y, b.Z - a.Z), e2 = new(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
            Double3 h = Double3.Cross(dir, e2);
            double det = Double3.Dot(e1, h);
            if (Math.Abs(det) < 1e-12)
                continue;
            Double3 s = from - new Double3(a.X, a.Y, a.Z);
            double u = Double3.Dot(s, h) / det;
            if (u < 0 || u > 1)
                continue;
            Double3 q = Double3.Cross(s, e1);
            double v = Double3.Dot(dir, q) / det;
            if (v < 0 || u + v > 1)
                continue;
            double t = Double3.Dot(e2, q) / det;
            if (t > 1e-9)
                best = Math.Min(best, t);
        }
        return best;
    }

    /// Asset-pipeline "Collision follows the visual": 1,000 downward rays over each structure asset's footprint, one
    /// instance of each. A ray that hits the drawn mesh must hit one of that object's own collision boxes within
    /// 0.15 m. The collision is checked against the object's own shapes, not the world, so a neighbour standing over it
    /// (the cellar head over its shaft) cannot stand in for it.
    static bool CollisionFollowsTheVisual(MapScene map)
    {
        WorldQuery world = map.World;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var lines = new List<string>();
        bool pass = true;
        for (int i = 0; i < world.ObjectCount; i++)
        {
            AssetDef asset = world.PlacementOf(i).Asset;
            if (!asset.Scene.StartsWith(Structures, StringComparison.Ordinal) || !seen.Add(asset.Id))
                continue;
            List<(Vector3 A, Vector3 B, Vector3 C)> tris = DrawnTriangles(map, i);
            Vector3 min = tris[0].A, max = tris[0].A;
            foreach ((Vector3 a, Vector3 b, Vector3 c) in tris)
            {
                foreach (Vector3 p in new[] { a, b, c })
                {
                    min = new Vector3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Vector3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
            }
            var rng = new Random(1337);
            int hits = 0, missed = 0;
            bool boxesOnly = true;
            double worst = 0;
            string first = "";
            for (int k = 0; k < 1000; k++)
            {
                var from = new Double3(min.X + rng.NextDouble() * (max.X - min.X), max.Y + 1.0,
                    min.Z + rng.NextDouble() * (max.Z - min.Z));
                var dir = new Double3(0, -1, 0);
                double visual = RayTriangles(tris, from, dir);
                if (double.IsPositiveInfinity(visual))
                    continue;
                hits++;
                double collision = RayShapes(world, i, from, dir, out bool boxes);
                boxesOnly &= boxes;
                // How far the collision surface is from the point the mesh was hit at: along the ray, or, when the ray
                // clipped the very corner of a box and missed it, straight to the nearest box surface.
                var at = new Vector3((float)from.X, (float)(from.Y - visual), (float)from.Z);
                double off = Math.Min(Math.Abs(collision - visual), ToShapes(world, i, at));
                if (off > 0.15)
                {
                    if (missed++ == 0)
                        first = $" (first at ({from.X:0.000}, {from.Z:0.000}), mesh at y {at.Y:0.000}, collision "
                            + $"{(double.IsPositiveInfinity(collision) ? "none" : $"y {from.Y - collision:0.000}")}, "
                            + $"nearest shape surface {ToShapes(world, i, at) * 1000:0.000} mm from that point)";
                    continue;
                }
                worst = Math.Max(worst, off);
            }
            pass &= hits > 0 && missed == 0 && boxesOnly;
            Double3 lo = new(double.MaxValue, double.MaxValue, double.MaxValue), hi = -lo;
            for (int shape = 0; world.Geometry(i, shape, 0, out ShapeGeometry g); shape++)
            {
                Double3 e = new(Math.Abs(g.Axes.X.X) * g.HalfExtents.X + Math.Abs(g.Axes.Y.X) * g.HalfExtents.Y + Math.Abs(g.Axes.Z.X) * g.HalfExtents.Z,
                    Math.Abs(g.Axes.X.Y) * g.HalfExtents.X + Math.Abs(g.Axes.Y.Y) * g.HalfExtents.Y + Math.Abs(g.Axes.Z.Y) * g.HalfExtents.Z,
                    Math.Abs(g.Axes.X.Z) * g.HalfExtents.X + Math.Abs(g.Axes.Y.Z) * g.HalfExtents.Y + Math.Abs(g.Axes.Z.Z) * g.HalfExtents.Z);
                lo = new Double3(Math.Min(lo.X, g.Center.X - e.X), Math.Min(lo.Y, g.Center.Y - e.Y), Math.Min(lo.Z, g.Center.Z - e.Z));
                hi = new Double3(Math.Max(hi.X, g.Center.X + e.X), Math.Max(hi.Y, g.Center.Y + e.Y), Math.Max(hi.Z, g.Center.Z + e.Z));
            }
            lines.Add($"{asset.Id} {hits} of 1000 rays on the mesh, worst gap {worst * 1000:0.0} mm, {missed} over 150 mm"
                + $"{(boxesOnly ? "" : ", NOT all boxes")}{first}; drawn ({min.X:0.000} {min.Y:0.000} {min.Z:0.000})-"
                + $"({max.X:0.000} {max.Y:0.000} {max.Z:0.000}), collided ({lo.X:0.000} {lo.Y:0.000} {lo.Z:0.000})-"
                + $"({hi.X:0.000} {hi.Y:0.000} {hi.Z:0.000})");
        }
        return Check("collision follows the visual", pass && seen.Count > 0,
            $"{seen.Count} structure assets: {string.Join("; ", lines)}");
    }

    /// A drone-sized capsule for the fly-through sweeps: 0.6 m across and 0.62 m tall, the least a gap must be clear
    /// (game/maps/README.md; the reference airframe is 0.57 m across its motors and 0.71 m across its prop diagonal).
    const float DroneRadius = 0.3f, DroneHeight = 0.62f, DroneDiagonal = 0.706f;

    /// Asset-pipeline "House door is a gap": for every gap of a structure asset, GapsNear in front of the opening
    /// returns it, the drawn opening measured by rays through the mesh is the declared width and height within 5 cm,
    /// and a drone-sized capsule swept along the gap's normal passes without touching anything. The house is also
    /// swept in one go from outside its door, through the room and out of the window on the far side, which is the line
    /// clip E flies.
    static bool GapsArePassable(MapScene map, PhysicsDirectSpaceState3D space)
    {
        WorldQuery world = map.World;
        var gaps = new Gap[16];
        var lines = new List<string>();
        bool pass = true;
        for (int i = 0; i < world.ObjectCount; i++)
        {
            Placement p = world.PlacementOf(i);
            if (!p.Asset.Scene.StartsWith(Structures, StringComparison.Ordinal) || p.Asset.Gaps.Length == 0)
                continue;
            List<(Vector3 A, Vector3 B, Vector3 C)> tris = DrawnTriangles(map, i);
            foreach (GapDef def in p.Asset.Gaps)
            {
                Axes facing = p.Rotation.Compose(Axes.FromEuler(def.Yaw, 0, 0));
                Double3 centre = p.Position + p.Rotation.ToWorld(def.Center) * p.Scale;
                Double3 normal = facing.Z, up = facing.Y, across = Double3.Cross(up, normal);
                double width = def.Width * p.Scale, height = def.Height * p.Scale;

                // GapsNear finds it from 2 m in front.
                int found = world.GapsNear(centre + normal * 2, 3, gaps);
                bool named = false;
                for (int k = 0; k < Math.Min(found, gaps.Length); k++)
                    named |= gaps[k].Object == i && gaps[k].Name == def.Name;

                // The declared rectangle is open in the drawn mesh, and 5 cm wider or 5 cm taller is not: so it is the
                // mesh opening within 5 cm. A gap whose sill is the ground (a doorway) is bounded below by the terrain
                // and by whatever lies on it, not by the asset, so its lowest 0.15 m is left out and its sill is not
                // measured; a window's sill is a wall face and is.
                bool onGround = def.Center.Y - def.Height / 2 <= 0.05;
                double floor = onGround ? 0.15 : 0.002;
                int blocked = Blocked(tris, centre, across, up, normal, width, height, floor);
                int wider = Blocked(tris, centre, across, up, normal, width + 0.05, height, floor);
                int taller = Blocked(tris, centre + up * 0.025, across, up, normal, width, height + 0.05, floor + 0.025);
                int lower = onGround ? 1 : Blocked(tris, centre - up * 0.025, across, up, normal, width, height + 0.05, floor);
                bool sized = blocked == 0 && wider > 0 && taller > 0 && lower > 0;

                // The sweep: 0.8 m each side of the opening plane along its normal.
                (bool clear, double travel, string touched) = Sweep(space, centre - normal * 0.8, normal * 1.6, DroneRadius);
                pass &= named && sized && clear;
                lines.Add($"{p.Asset.Id}/{def.Name} {width:0.00} × {height:0.00} m: {blocked} of {Grid * Grid} rays over it "
                    + $"blocked (limit 0), and {wider}/{taller}/{lower} over it 5 cm wider/taller/lower (each over 0), so it is "
                    + $"the mesh opening within 5 cm; GapsNear: {named}; swept 1.6 m through it: "
                    + $"{(clear ? "clear" : $"blocked after {travel:0.00} m by {touched}")}, clearance "
                    + $"{(width - 2 * DroneRadius) / 2 * 1000:0} mm each side and {(height - 2 * DroneRadius) / 2 * 1000:0} mm over "
                    + $"and under a 0.60 m drone, {(width - DroneDiagonal) / 2 * 1000:0} mm each side across its prop diagonal");
            }
        }

        // Clip E's line: in through the door, across the room, out of the window opposite it.
        int house = -1;
        for (int i = 0; i < world.ObjectCount; i++)
            house = world.PlacementOf(i).Asset.Id == "house_adobe" ? i : house;
        string through = "no adobe house placed";
        bool flown = house >= 0;
        if (house >= 0)
        {
            Placement p = world.PlacementOf(house);
            GapDef door = p.Asset.Gaps.First(g => g.Name == "door");
            GapDef far = p.Asset.Gaps.First(g => g.Name == "window_far");
            // One height that clears the door's head and the far window's broken sill, with the capsule's half height.
            double low = Math.Max(door.Center.Y - door.Height / 2, far.Center.Y - far.Height / 2) + DroneHeight / 2;
            double high = Math.Min(door.Center.Y + door.Height / 2, far.Center.Y + far.Height / 2) - DroneHeight / 2;
            double y = (low + high) / 2;
            Double3 from = p.Position + p.Rotation.ToWorld(new Double3(door.Center.X, y, door.Center.Z + 0.8));
            Double3 to = p.Position + p.Rotation.ToWorld(new Double3(far.Center.X, y, far.Center.Z - 0.8));
            (bool clear, double travel, string touched) = Sweep(space, from, to - from, DroneRadius);
            flown = clear && high > low;
            through = $"the door at {door.Center.X:0.00} m and the window opposite it, at {y - door.Center.Y + door.Height / 2:0.00} m "
                + $"over the threshold (the band that clears both is {high - low:0.00} m tall), swept "
                + $"{(to - from).Length():0.00} m: {(clear ? "clear" : $"blocked after {travel:0.00} m by {touched}")}";
        }
        pass &= flown;
        return Check("fly-through gaps", pass && lines.Count > 0,
            $"{lines.Count} gaps on the structure assets: {string.Join("; ", lines)}; and clip E's line through {through}");
    }

    /// Rays per side of the grid a gap's opening is measured with.
    const int Grid = 9;

    /// How many of a `Grid` × `Grid` set of rays through an opening hit the drawn mesh. The rays run along `through`
    /// from 0.9 m in front to 0.9 m behind the opening's plane, over the rectangle `width` × `height` about `centre`,
    /// inset 2 mm at the sides and top and `floor` at the bottom.
    static int Blocked(List<(Vector3 A, Vector3 B, Vector3 C)> tris, Double3 centre, Double3 across, Double3 up,
        Double3 through, double width, double height, double floor)
    {
        const double Depth = 0.9;
        int blocked = 0;
        for (int i = 0; i < Grid; i++)
        {
            double u = (-width / 2 + 0.002) + (width - 0.004) * i / (Grid - 1);
            for (int j = 0; j < Grid; j++)
            {
                double v = (-height / 2 + floor) + (height - floor - 0.002) * j / (Grid - 1);
                Double3 from = centre + across * u + up * v - through * Depth;
                blocked += RayTriangles(tris, from, through) <= 2 * Depth ? 1 : 0;
            }
        }
        return blocked;
    }

    /// Sweeps a drone-sized capsule along `motion` from `from`: whether it got the whole way untouched, how far it got,
    /// and what stopped it.
    static (bool Clear, double Travel, string Touched) Sweep(PhysicsDirectSpaceState3D space, Double3 from, Double3 motion,
        float radius)
    {
        var parameters = new PhysicsShapeQueryParameters3D
        {
            Shape = new CapsuleShape3D { Radius = radius, Height = DroneHeight },
            Transform = new Transform3D(Basis.Identity, new Vector3((float)from.X, (float)from.Y, (float)from.Z)),
            Motion = new Vector3((float)motion.X, (float)motion.Y, (float)motion.Z),
        };
        float[] result = space.CastMotion(parameters);
        double length = motion.Length();
        if (result[0] >= 0.999f)
            return (true, length, "");
        parameters.Transform = new Transform3D(Basis.Identity, new Vector3((float)(from.X + motion.X * result[1]),
            (float)(from.Y + motion.Y * result[1]), (float)(from.Z + motion.Z * result[1])));
        parameters.Motion = Vector3.Zero;
        var names = new List<string>();
        foreach (Godot.Collections.Dictionary touch in space.IntersectShape(parameters, 4))
            names.Add(((CollisionObject3D)touch["collider"]).Name);
        return (false, result[0] * length, names.Count == 0 ? "nothing it reports" : string.Join(", ", names));
    }

    /// A bad or unknown package gives one clear error and no map: broken copies of the sample in a temp folder.
    static bool BadPackages(string dir)
    {
        string root = Path.Combine(OS.GetUserDataDir(), "map_selftest");
        string surfaces = ProjectSettings.GlobalizePath("res://maps/surfaces.json"), catalog = ProjectSettings.GlobalizePath("res://assets/catalog.json");
        var cases = new (string Name, Action<string> Break, string Expect, bool CustomTable)[]
        {
            ("future major", d => Edit(d, "map.json", "\"1.1\"", "\"2.0\""), "format_version is 2.0, and this build reads 1.x", false),
            ("missing layer", d => File.Delete(Path.Combine(d, "cover.png")), "no cover.png", false),
            ("bad map size", d => Edit(d, "map.json", "\"size_m\": 256", "\"size_m\": 300"), "multiple of 256", false),
            ("height size", d => File.WriteAllBytes(Path.Combine(d, "height.r16"), File.ReadAllBytes(Path.Combine(d, "height.r16"))[..^2]),
                "expected 132098", false),
            ("unknown asset", d => Edit(d, "objects.json", "\"house_box\"", "\"no_such_asset\""), "object 0 names asset 'no_such_asset'", false),
            ("unknown surface", d => File.WriteAllText(Path.Combine(d, "surfaces.json"), WithoutSurface(surfaces, 3)), "surface index 3", true),
        };
        bool pass = true;
        foreach ((string name, Action<string> breakIt, string expect, bool customTable) in cases)
        {
            string copy = Path.Combine(root, name.Replace(' ', '_'));
            if (Directory.Exists(copy))
                Directory.Delete(copy, true);
            Directory.CreateDirectory(copy);
            foreach (string file in Directory.GetFiles(dir))
                File.Copy(file, Path.Combine(copy, Path.GetFileName(file)));
            breakIt(copy);
            MapScene map = MapScene.LoadPackage(name, copy, customTable ? Path.Combine(copy, "surfaces.json") : surfaces, catalog, out string error);
            pass &= Check($"bad package: {name}", map == null && error != null && error.Contains(expect) && !error.Contains('\n'), $"error: {error}");
            map?.Free();
        }
        foreach ((string id, string expect) in new[] { ("no_such_map", "no map package game/maps/no_such_map/"), ("../maps", "is not a map id") })
        {
            MapScene map = MapScene.Load(id, out string error);
            pass &= Check($"bad id '{id}'", map == null && error != null && error.Contains(expect), $"error: {error}");
        }
        return pass;
    }

    static void Edit(string dir, string file, string from, string to)
    {
        string path = Path.Combine(dir, file), text = File.ReadAllText(path);
        if (!text.Contains(from))
            throw new InvalidOperationException($"{file} has no '{from}' to break");
        File.WriteAllText(path, text.Replace(from, to));
    }

    static string WithoutSurface(string surfacesPath, int index)
    {
        JsonNode table = JsonNode.Parse(File.ReadAllText(surfacesPath));
        JsonArray list = table["surfaces"].AsArray();
        list.Remove(list.First(s => s["index"].GetValue<int>() == index));
        return table.ToJsonString();
    }
}
