using System;
using Godot;

/// Review sandbox: sky, sun and ground placeholder.
/// `-- --scene res://…` instances that scene at the origin in place of the ground placeholder.
/// `-- --map <id>` loads the map package game/maps/<id>/ in place of the ground placeholder, with the noclip camera
/// MapStartHeight above the map centre.
/// F12 saves a screenshot to user://screenshots/.
public partial class Sandbox : Node3D
{
    /// Metres above the terrain at the map centre where the camera starts.
    public const float MapStartHeight = 15f;

    public string LastScreenshot { get; private set; }
    /// The map `--map` loaded, or null.
    public MapScene Map { get; private set; }

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        string scenePath = ArgValue(args, "--scene");
        if (scenePath != null)
            LoadScene(scenePath);
        string mapId = ArgValue(args, "--map");
        if (mapId != null)
            LoadMap(mapId);

        string selftest = ArgValue(args, "--selftest");
        if (selftest == "noclip")
        {
            GetTree().Quit(SandboxSelfTest.Noclip(GetNode<NoclipCamera>("Camera")) ? 0 : 1);
        }
        else if (selftest == "overlay")
        {
            SandboxSelfTest.Overlay(this);
        }
        else if (selftest == "flypath")
        {
            SandboxSelfTest.FlyPath(this);
        }
        else if (selftest == "worldquery")
        {
            WorldQuerySelfTest.Run(this);
        }
        else if (selftest == "worldquery-digest")
        {
            WorldQuerySelfTest.Digest(this, ArgValue(args, "--digest-out"));
        }
        else if (selftest != null)
        {
            GD.PrintErr($"ERROR: unknown selftest '{selftest}'.");
            GetTree().Quit(2);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("screenshot"))
            SaveScreenshot();
    }

    void SaveScreenshot()
    {
        string dir = ProjectSettings.GlobalizePath("user://screenshots");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        string path = $"{dir}/{DateTime.Now:yyyyMMdd-HHmmss-fff}.png";
        Error error = GetViewport().GetTexture().GetImage().SavePng(path);
        if (error != Error.Ok)
        {
            GD.PrintErr($"ERROR: screenshot not saved to {path}: {error}");
            return;
        }
        LastScreenshot = path;
        GD.Print($"Screenshot saved: {path}");
    }

    void LoadScene(string path)
    {
        var scene = ResourceLoader.Exists(path) ? ResourceLoader.Load(path) as PackedScene : null;
        if (scene == null)
        {
            GD.PrintErr($"ERROR: Sandbox cannot load scene '{path}' (missing or not a scene). Starting with the ground placeholder.");
            return;
        }
        GetNode<Node3D>("Ground").Visible = false;
        AddChild(scene.Instantiate());
        GD.Print($"Sandbox: loaded {path} at the origin");
    }

    void LoadMap(string id)
    {
        MapScene map = MapScene.Load(id, out string error);
        if (map == null)
        {
            GD.PrintErr($"ERROR: Sandbox cannot load map '{id}': {error}. Starting with the ground placeholder.");
            return;
        }
        GetNode<Node3D>("Ground").Visible = false;
        AddChild(map);
        Map = map;
        Span<XZ> centre = stackalloc XZ[] { new XZ(0, 0) };
        Span<GroundSample> ground = stackalloc GroundSample[1];
        map.World.SampleGround(centre, ground);
        GetNode<NoclipCamera>("Camera").ResetPose(new Vector3(0, (float)ground[0].TerrainHeight + MapStartHeight, 0));
        GD.Print($"Sandbox: loaded map {id}");
    }

    static string ArgValue(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
