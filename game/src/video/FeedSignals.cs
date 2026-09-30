using Godot;

/// The signals the feed shaders and the OSD read, one value per rendered frame.
///
/// TEMPORARY DRIVER. `docs/reference-notes/video-feed.md` §7 lists the real sources: motor current and electrical
/// frequency, battery voltage, fiber tension, bend radius and link margin, scene light level. None of them exist yet
/// — there is no drone and no flight model — so <see cref="UpdateFromCamera"/> derives everything from the free
/// camera, which is obviously wrong and is meant to be thrown away. The feed and the OSD only ever read the fields
/// below, so replacing this class with the physics↔video interface touches nothing else.
///
/// Attitude, altitude, distance and the flight timer are real values of the camera. Battery voltage, current and link
/// margin are stand-ins, and are marked as such below.
public sealed class FeedSignals
{
    /// Fiber optical margin. 1 = plenty, 0 = at the receiver threshold. Feeds N2 sparkle density, the N12/N5–N7
    /// dropout hazard and the OSD's link readout. Real source: fiber tension, bend radius and link state.
    public float LinkMargin = 1f;

    /// Total motor current, 0–1 of full. Feeds N3 visibility and N4 amplitude (both weak per the pilot, U6).
    public float MotorCurrent;

    /// Camera AE gain. 1 = bright day, 4 ≈ the night level of clip L. Feeds N1 grain σ and C3 saturation.
    public float Gain = 1f;

    /// Camera attitude and position, for the OSD. These are real.
    public float HeadingDeg, PitchDeg, RollDeg, Altitude, Distance;

    /// Seconds since the feed started, the OSD's flight timer.
    public double ElapsedSeconds;

    /// STAND-IN: pack voltage under load, 6S. A full pack sagging with current and slowly draining.
    public float PackVolts = 25.2f;
    /// STAND-IN: total current in amps, from <see cref="MotorCurrent"/>.
    public float Amps;

    /// 6S full, and what a hard-working heavy cargo quad pulls at full current.
    const float PackFullVolts = 25.2f, SagVolts = 3.4f, DrainVoltsPerSecond = 0.004f, FullAmps = 68f;

    Vector3 _lastCameraPos;

    /// Stand-in: link margin falls with distance from a notional pilot at the map origin (as if the fiber were
    /// paying out), and motor current rises with camera speed.
    public void UpdateFromCamera(Vector3 position, Vector3 rotation, double dt)
    {
        Distance = new Vector2(position.X, position.Z).Length();
        LinkMargin = Mathf.Clamp(1f - Distance / 400f, 0f, 1f);
        float speed = dt > 0 ? (position - _lastCameraPos).Length() / (float)dt : 0f;
        MotorCurrent = Mathf.Clamp(speed / 30f, 0f, 1f);
        _lastCameraPos = position;

        Altitude = position.Y;
        HeadingDeg = -Mathf.RadToDeg(rotation.Y);
        PitchDeg = Mathf.RadToDeg(rotation.X);
        RollDeg = Mathf.RadToDeg(rotation.Z);

        ElapsedSeconds += dt;
        Amps = FullAmps * MotorCurrent;
        PackVolts = Mathf.Clamp(PackFullVolts - DrainVoltsPerSecond * (float)ElapsedSeconds
            - SagVolts * MotorCurrent, 19.8f, PackFullVolts);
    }
}
