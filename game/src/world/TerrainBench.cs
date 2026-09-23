using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Godot;

/// Terrain-only benchmark: `-- --scene res://scenes/world/terrain_bench.tscn --package <absolute dir>` builds the
/// heightfield of that map package with HeightmapTerrain and prints how long it took. Used to measure D-009.
public partial class TerrainBench : Node3D
{
    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        int i = Array.IndexOf(args, "--package");
        if (i < 0 || i + 1 >= args.Length)
        {
            GD.PrintErr("ERROR: terrain bench needs --package <dir>");
            return;
        }
        string dir = args[i + 1];
        var clock = Stopwatch.StartNew();
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "map.json")));
        JsonElement height = manifest.RootElement.GetProperty("height");
        byte[] r16 = File.ReadAllBytes(Path.Combine(dir, "height.r16"));
        long read = clock.ElapsedMilliseconds;
        var terrain = new HeightmapTerrain { Name = "Terrain" };
        AddChild(terrain);
        terrain.Build(r16, height.GetProperty("samples_per_side").GetInt32(), height.GetProperty("resolution_m").GetSingle(),
            height.GetProperty("offset_m").GetSingle(), height.GetProperty("scale_m").GetSingle());
        GD.Print($"TerrainBench: {dir} read in {read} ms, built in {clock.ElapsedMilliseconds} ms total");
    }
}
