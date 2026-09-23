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
}
