using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;

/// The LOD-switching scenarios (build-uat1-parts 2.6), windowed: `-- --map <id> --selftest lod`.
///
/// For every asset the map places that has `lod_switch_m`, it leaves one instance of that asset alone in the frame,
/// with the terrain, the micro-detail, the far cover, every other object and the sun's shadow switched off, and
/// counts the triangles the renderer actually drew at a series of camera distances around each switch. The count is
/// the whole check: it names the level that drew, and it proves no second level drew with it. It also reads each
/// asset's `.glb.import` to confirm Godot's own automatic mesh LOD is off, so nothing is hiding under ours.
public static class LodSelfTest
{
    /// Where a level is sampled around its switch, in metres either side.
    const double Margin = 1.0;

    public static async void Run(Sandbox sandbox)
    {
        SceneTree tree = sandbox.GetTree();
        bool pass = false;
        try
        {
            if (DisplayServer.GetName() == "headless")
                throw new InvalidOperationException("needs a window, run it without --headless");
            MapScene map = sandbox.Map ?? throw new InvalidOperationException("no map loaded");
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            sandbox.GetNode<DirectionalLight3D>("Sun").ShadowEnabled = false;
            foreach (string node in new[] { "Terrain", "MicroDetail", "FarCover" })
                map.GetNode<Node3D>(node).Visible = false;
            var objects = map.GetNode<Node3D>("Objects");
            var visuals = objects.GetChildren().OfType<Node3D>().Where(n => n is not StaticBody3D).ToList();
            foreach (Node3D visual in visuals)
                visual.Visible = false;

            pass = true;
            foreach ((string asset, Node3D visual) in OnePer(map, visuals))
            {
                pass &= NoGodotLod(map, asset);
                visual.Visible = true;
                pass &= await Switches(sandbox, map, asset, visual);
                visual.Visible = false;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"ERROR: selftest lod: {e.Message}");
            pass = false;
        }
        GD.Print($"selftest lod: {(pass ? "ALL PASS" : "FAILED")}");
        tree.Quit(pass ? 0 : 1);
    }

    /// One placed instance of each asset that has switch distances, by rising asset id.
    static IEnumerable<(string, Node3D)> OnePer(MapScene map, List<Node3D> visuals)
    {
        var found = new SortedDictionary<string, Node3D>(StringComparer.Ordinal);
        foreach (Node3D visual in visuals)
        {
            string asset = map.World.PlacementOf((int)visual.GetMeta("object")).Asset.Id;
            if (map.World.Catalog.Assets[asset].LodSwitch.Length > 0 && !found.ContainsKey(asset))
                found[asset] = visual;
        }
        return found.Select(p => (p.Key, p.Value));
    }

    /// Godot's automatic mesh LOD must be off for our assets, or a second, hidden LOD would sit under the one the
    /// catalog switches and would decimate a crown's loose alpha cards away.
    static bool NoGodotLod(MapScene map, string asset)
    {
        string scene = map.World.Catalog.Assets[asset].Scene;
        var settings = new ConfigFile();
        Error error = settings.Load(scene + ".import");
        bool generates = error != Error.Ok || (bool)settings.GetValue("params", "meshes/generate_lods", true);
        return Check(asset, "Godot's own mesh LOD is off", !generates,
            $"{scene}.import meshes/generate_lods={(error == Error.Ok ? generates.ToString().ToLowerInvariant() : $"unreadable ({error})")}");
    }

    /// Walks the camera out past every switch and counts the triangles drawn at each stop.
    static async Task<bool> Switches(Sandbox sandbox, MapScene map, string asset, Node3D visual)
    {
        SceneTree tree = sandbox.GetTree();
        var camera = sandbox.GetNode<Camera3D>("Camera");
        double[] switches = map.World.Catalog.Assets[asset].LodSwitch;
        MeshInstance3D[] levels = Levels(visual, switches.Length + 1);
        int[] triangles = levels.Select(m => m.Mesh.GetFaces().Length / 3).ToArray();
        Vector3 middle = visual.GlobalPosition + Vector3.Up * (float)(levels[0].GetAabb().GetCenter().Y * visual.Scale.Y);

        var stops = new List<(double Distance, int Level)>();
        for (int level = 0; level < levels.Length; level++)
        {
            double begin = level > 0 ? switches[level - 1] : 0;
            double end = level < switches.Length ? switches[level] : switches[^1] * 1.35;
            stops.Add((begin + Margin, level));         // just inside this level's near edge
            stops.Add(((begin + end) / 2, level));      // the middle of its band
            stops.Add((end - Margin, level));           // just inside its far edge
        }

        bool pass = true;
        var drew = new List<string>();
        foreach ((double distance, int level) in stops)
        {
            camera.GlobalPosition = middle + new Vector3(0, 0, (float)distance);
            camera.LookAt(middle);
            int drawn = await Triangles(tree, visual);
            int at = Array.IndexOf(triangles, drawn);
            drew.Add($"{distance:0.0} m {drawn}");
            pass &= Check(asset, $"{distance:0.0} m draws LOD{level} alone", drawn == triangles[level],
                $"{drawn} triangles drawn, LOD{level} has {triangles[level]}"
                + (at >= 0 && at != level ? $" (LOD{at} drew instead)" : drawn > triangles[level] ? " (more than one level drew)" : ""));
        }
        GD.Print($"selftest lod: {asset}: LODs {string.Join("/", triangles)} triangles, switch at "
            + $"{string.Join(", ", switches.Select(d => $"{d:0} m"))}; drawn {string.Join(", ", drew)}");
        return pass;
    }

    /// The asset's LOD meshes in level order.
    static MeshInstance3D[] Levels(Node3D visual, int count)
    {
        var levels = new MeshInstance3D[count];
        foreach (MeshInstance3D mesh in visual.GetChildren().OfType<MeshInstance3D>())
            levels[int.Parse(mesh.Name.ToString().Split("_LOD")[^1])] = mesh;
        return levels;
    }

    /// The triangles this object added to the frame: the renderer's count with it drawn, less the count without it.
    static async Task<int> Triangles(SceneTree tree, Node3D visual)
    {
        long with = await Count(tree);
        visual.Visible = false;
        long without = await Count(tree);
        visual.Visible = true;
        return (int)(with - without);
    }

    static async Task<long> Count(SceneTree tree)
    {
        for (int i = 0; i < 4; i++)  // the monitor reports the frame that has been drawn, so let it catch up
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        return (long)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
    }

    static bool Check(string asset, string scenario, bool ok, string numbers)
    {
        if (!ok)
            GD.Print($"selftest lod: {asset}: {scenario}: {numbers} FAIL");
        return ok;
    }
}
