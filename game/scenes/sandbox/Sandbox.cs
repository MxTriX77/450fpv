using Godot;

/// Review sandbox: sky, sun and ground placeholder.
/// `-- --scene res://…` instances that scene at the origin in place of the ground placeholder.
public partial class Sandbox : Node3D
{
    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        string scenePath = ArgValue(args, "--scene");
        if (scenePath != null)
            LoadScene(scenePath);

        string selftest = ArgValue(args, "--selftest");
        if (selftest == "noclip")
        {
            GetTree().Quit(SandboxSelfTest.Noclip(GetNode<NoclipCamera>("Camera")) ? 0 : 1);
        }
        else if (selftest != null)
        {
            GD.PrintErr($"ERROR: unknown selftest '{selftest}'.");
            GetTree().Quit(2);
        }
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

    static string ArgValue(string[] args, string name)
    {
        int i = System.Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
