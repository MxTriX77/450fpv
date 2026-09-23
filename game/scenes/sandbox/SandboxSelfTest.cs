using System;
using System.Threading.Tasks;
using Godot;

/// Self-checks run with `-- --selftest <name>`. Each prints PASS/FAIL lines; the caller exits non-zero on failure.
public static class SandboxSelfTest
{
    /// Holds W (and Shift+W) for 1.0 s of simulated time at fixed 30 and 144 fps; distance must be within ±5 %.
    public static bool Noclip(NoclipCamera camera)
    {
        bool pass = true;
        foreach (int fps in new[] { 30, 144 })
        {
            foreach (bool fast in new[] { false, true })
            {
                float expected = fast ? 48f : 6f; // the spec's values, not the constants under test
                camera.ResetPose(Vector3.Zero);
                Input.ActionPress("move_forward");
                if (fast)
                    Input.ActionPress("move_fast");
                for (int frame = 0; frame < fps; frame++)
                    camera._Process(1.0 / fps);
                Input.ActionRelease("move_forward");
                Input.ActionRelease("move_fast");

                Vector3 moved = camera.GlobalPosition;
                bool ok = moved.DistanceTo(Vector3.Forward * expected) <= 0.05f * expected;
                GD.Print($"selftest noclip: {fps} fps, {(fast ? "Shift+W" : "W")} 1.0 s -> {moved.Length():F3} m forward "
                    + $"(expected {expected} m ±5 %) {(ok ? "PASS" : "FAIL")}");
                pass &= ok;
            }
        }
        return pass;
    }

    /// Windowed: F3 shows the overlay and it refreshes at least 4 times per second, F12 saves a PNG at the
    /// window's resolution, F3 again hides the overlay. Also prints the frame budget measured with vsync off over the
    /// overlay's 5 s window, after a 2 s warm-up. Quits by itself (about 10 s).
    public static async void Overlay(Sandbox sandbox)
    {
        SceneTree tree = sandbox.GetTree();
        bool pass = false;
        try
        {
            if (DisplayServer.GetName() == "headless")
                throw new InvalidOperationException("needs a window, run it without --headless");
            Input.MouseMode = Input.MouseModeEnum.Visible;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            var overlay = sandbox.GetNode<PerfOverlay>("PerfOverlay");

            await Seconds(tree, 2.0);
            await PressKey(tree, Key.F3);
            bool shown = Check("F3 shows the overlay", overlay.Visible);

            int refreshesBefore = overlay.Refreshes;
            ulong end = Time.GetTicksMsec() + 5000;
            while (Time.GetTicksMsec() < end)
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            int refreshes = overlay.Refreshes - refreshesBefore;
            bool refreshing = Check($"overlay refreshed {refreshes / 5.0:0.0} times/s (need >= 4)", refreshes >= 20);

            GD.Print($"selftest overlay: measured {DisplayServer.WindowGetSize()} window, vsync off: "
                + $"{overlay.Fps:0} fps, {overlay.AvgFrameMs:0.00} ms avg, 1 % low {overlay.OnePercentLowFps:0} fps, "
                + $"{overlay.DrawCalls} draw calls (empty-sandbox budget: >= 144 fps, 1 % low >= 120)");
            GD.Print($"selftest overlay: GPU {RenderingServer.GetVideoAdapterName()}, "
                + $"{RenderingServer.GetCurrentRenderingMethod()}, screen {DisplayServer.ScreenGetRefreshRate():0} Hz");

            string before = sandbox.LastScreenshot;
            await PressKey(tree, Key.F12);
            string shot = sandbox.LastScreenshot;
            Image image = shot != null && shot != before ? Image.LoadFromFile(shot) : null;
            Vector2I window = DisplayServer.WindowGetSize();
            bool captured = Check($"F12 saved {shot} at {image?.GetSize()} (window {window})",
                image != null && image.GetSize() == window);

            await PressKey(tree, Key.F3);
            bool hidden = Check("F3 again hides the overlay", !overlay.Visible);
            pass = shown && refreshing && captured && hidden;
        }
        catch (Exception e)
        {
            GD.PrintErr($"ERROR: selftest overlay: {e.Message}");
        }
        tree.Quit(pass ? 0 : 1);
    }

    static bool Check(string what, bool ok)
    {
        GD.Print($"selftest overlay: {what} {(ok ? "PASS" : "FAIL")}");
        return ok;
    }

    static async Task PressKey(SceneTree tree, Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        for (int i = 0; i < 2; i++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    static async Task Seconds(SceneTree tree, double seconds)
    {
        await tree.ToSignal(tree.CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
