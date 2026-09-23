using Godot;

/// Free-flying review camera: mouse look, WASD along the view, E/Q (Space/Ctrl) along world up,
/// Shift ×8, scroll changes base speed in ×1.25 steps within 1–60 m/s. No collision.
public partial class NoclipCamera : Camera3D
{
    public const float DefaultSpeed = 6f;
    public const float MinSpeed = 1f;
    public const float MaxSpeed = 60f;
    public const float SpeedStep = 1.25f;
    public const float FastMultiplier = 8f;
    const float MaxPitch = 89f * Mathf.Pi / 180f;

    /// Degrees of turn per pixel of mouse movement.
    [Export] public float MouseSensitivity = 0.1f;

    public float BaseSpeed => DefaultSpeed * Mathf.Pow(SpeedStep, _speedStep);

    int _speedStep;
    float _yaw;
    float _pitch;

    public override void _Ready()
    {
        _yaw = Rotation.Y;
        _pitch = Rotation.X;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("mouse_release"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
        else if (e is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            float radPerPixel = Mathf.DegToRad(MouseSensitivity);
            _yaw -= motion.ScreenRelative.X * radPerPixel;
            _pitch = Mathf.Clamp(_pitch - motion.ScreenRelative.Y * radPerPixel, -MaxPitch, MaxPitch);
            Rotation = new Vector3(_pitch, _yaw, 0f);
        }
        else if (e.IsActionPressed("speed_up"))
        {
            ChangeSpeedStep(1);
        }
        else if (e.IsActionPressed("speed_down"))
        {
            ChangeSpeedStep(-1);
        }
    }

    public override void _Process(double delta)
    {
        Vector2 planar = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        float vertical = Input.GetAxis("move_down", "move_up");
        Basis view = GlobalBasis;
        Vector3 direction = (view.X * planar.X + view.Z * planar.Y + Vector3.Up * vertical).LimitLength(1f);
        float speed = BaseSpeed * (Input.IsActionPressed("move_fast") ? FastMultiplier : 1f);
        GlobalPosition += direction * speed * (float)delta;
    }

    /// Puts the camera at a level pose with the default speed. Used by the selftest.
    public void ResetPose(Vector3 position)
    {
        _speedStep = 0;
        _yaw = 0f;
        _pitch = 0f;
        Rotation = Vector3.Zero;
        GlobalPosition = position;
    }

    void ChangeSpeedStep(int direction)
    {
        float next = DefaultSpeed * Mathf.Pow(SpeedStep, _speedStep + direction);
        if (next < MinSpeed || next > MaxSpeed)
            return;
        _speedStep += direction;
        GD.Print($"Noclip base speed: {BaseSpeed:0.##} m/s");
    }
}
