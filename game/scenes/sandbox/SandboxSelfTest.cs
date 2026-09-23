using System;
using System.Collections.Generic;
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
                float expected = NoclipCamera.DefaultSpeed * (fast ? NoclipCamera.FastMultiplier : 1f);
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

    /// Windowed: F3 shows the overlay and its text changes at least 4 times per second, F12 saves a PNG at the
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
            var label = overlay.GetNode<Label>("Panel/Label");

            await Seconds(tree, 2.0);
            await PressKey(tree, Key.F3);
            bool shown = Check("F3 shows the overlay", overlay.Visible);

            int changes = 0;
            string text = label.Text;
            ulong end = Time.GetTicksMsec() + 5000;
            while (Time.GetTicksMsec() < end)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                if (label.Text != text)
                {
                    changes++;
                    text = label.Text;
                }
            }
            bool refreshing = Check($"overlay text changed {changes / 5.0:0.0} times/s (need >= 4)", changes >= 20);

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

    /// Windowed, vsync off: after a 2 s warm-up, flies the camera 12 s along a fixed path round the origin (a quarter
    /// orbit of radius 200 m at 15 m, then a quarter orbit climbing to 150 m, about 52 m/s, looking along the path) and
    /// prints fps and 1 % low over the whole path (the overlay's definitions), draw calls, video memory and the time of
    /// the first frame. A measurement, not a pass/fail check: compare the numbers with the budget. About 15 s.
    public static async void FlyPath(Sandbox sandbox)
    {
        SceneTree tree = sandbox.GetTree();
        try
        {
            if (DisplayServer.GetName() == "headless")
                throw new InvalidOperationException("needs a window, run it without --headless");
            Input.MouseMode = Input.MouseModeEnum.Visible;
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            var camera = sandbox.GetNode<NoclipCamera>("Camera");
            sandbox.GetNode<PerfOverlay>("PerfOverlay").Visible = true;
            FlyPathPose(camera, 0);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            GD.Print($"selftest flypath: first frame {Time.GetTicksMsec()} ms after engine start");
            await Seconds(tree, 2.0);

            var frameMs = new List<double>();
            long drawSum = 0, drawMax = 0, primitivesMax = 0;
            ulong start = Time.GetTicksUsec(), last = start;
            while (true)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                ulong now = Time.GetTicksUsec();
                if (now - start > 12_000_000)
                    break;
                frameMs.Add((now - last) / 1000.0);
                last = now;
                FlyPathPose(camera, (now - start) / 1e6);
                long draws = (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
                drawSum += draws;
                drawMax = Math.Max(drawMax, draws);
                primitivesMax = Math.Max(primitivesMax, (long)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
            }

            double[] sorted = frameMs.ToArray();
            Array.Sort(sorted);
            int slowest = Math.Max(1, sorted.Length / 100);
            double slowestSum = 0, sum = 0;
            for (int i = 0; i < sorted.Length; i++)
            {
                sum += sorted[i];
                if (i >= sorted.Length - slowest)
                    slowestSum += sorted[i];
            }
            double videoMb = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / (1024.0 * 1024.0);
            GD.Print($"selftest flypath: {sorted.Length} frames in 12 s, {DisplayServer.WindowGetSize()} window, vsync off: "
                + $"{1000.0 * sorted.Length / sum:0} fps avg, 1 % low {1000.0 * slowest / slowestSum:0} fps, worst frame {sorted[^1]:0.0} ms");
            GD.Print($"selftest flypath: draw calls {drawSum / sorted.Length} avg, {drawMax} max; primitives {primitivesMax} max; "
                + $"video memory {videoMb:0} MB");
            GD.Print($"selftest flypath: GPU {RenderingServer.GetVideoAdapterName()}, {RenderingServer.GetCurrentRenderingMethod()}, "
                + $"screen {DisplayServer.ScreenGetRefreshRate():0} Hz");
        }
        catch (Exception e)
        {
            GD.PrintErr($"ERROR: selftest flypath: {e.Message}");
            tree.Quit(1);
            return;
        }
        tree.Quit(0);
    }

    static void FlyPathPose(Camera3D camera, double t)
    {
        double angle = Math.PI / 2 * t / 6.0; // counter-clockwise seen from above, starting 200 m east of the origin
        float climb = (float)Math.Clamp((t - 6.0) / 6.0, 0.0, 1.0);
        camera.GlobalPosition = new Vector3((float)(200 * Math.Cos(angle)), 15f + 135f * climb, (float)(-200 * Math.Sin(angle)));
        camera.Rotation = new Vector3(Mathf.DegToRad(-5f - 7f * climb), (float)angle, 0f);
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
