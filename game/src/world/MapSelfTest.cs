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
        WindCell house = Cell(52, 30), tree = Cell(-40, -50), open = Cell(-100, 100);
        return Check("wind volumes in the wind grid", Math.Abs(house.TopM - 5) < 0.2 && house.Porosity < 0.1 && Math.Abs(tree.BaseM - 4) < 0.3
            && Math.Abs(tree.TopM - 10) < 0.3 && tree.Porosity < 1 && open.TopM == 0 && open.Porosity == 1,
            $"house cell top {house.TopM:0.00} m porosity {house.Porosity:0.000}; tree cell base {tree.BaseM:0.00} top {tree.TopM:0.00} m porosity "
            + $"{tree.Porosity:0.000}; open cell top {open.TopM} porosity {open.Porosity}");
    }

    /// The terrain draws with the surface materials, and every surface in the patch has its own layer in each array (the
    /// eye judges "visibly distinct" on a windowed screenshot, map-view).
    static bool SurfacesBound(MapScene map)
    {
        ShaderMaterial material = map.GetNode<HeightmapTerrain>("Terrain").Material;
        byte[] used = map.World.SurfaceIds.ToArray().Distinct().OrderBy(i => i).ToArray();
        int[] layers = new[] { "surface_albedo", "surface_normal", "surface_roughness" }
            .Select(name => ((Texture2DArray)material.GetShaderParameter(name)).GetLayers()).ToArray();
        return Check("sample patch: a material layer per surface", (bool)material.GetShaderParameter("use_surfaces") && layers.All(n => n > used.Max()),
            $"{used.Length} surfaces in the patch ({string.Join(", ", used.Select(i => map.World.Surface(i).Id))}), "
            + $"albedo, normal and roughness arrays of {string.Join(", ", layers)} layers");
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

    /// A bad or unknown package gives one clear error and no map: broken copies of the sample in a temp folder.
    static bool BadPackages(string dir)
    {
        string root = Path.Combine(OS.GetUserDataDir(), "map_selftest");
        string surfaces = ProjectSettings.GlobalizePath("res://maps/surfaces.json"), catalog = ProjectSettings.GlobalizePath("res://assets/catalog.json");
        var cases = new (string Name, Action<string> Break, string Expect, bool CustomTable)[]
        {
            ("future major", d => Edit(d, "map.json", "\"1.0\"", "\"2.0\""), "format_version is 2.0, and this build reads 1.x", false),
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
