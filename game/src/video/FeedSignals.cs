using Godot;

/// The signals the feed shaders read, one value per rendered frame.
///
/// TEMPORARY DRIVER. `docs/reference-notes/video-feed.md` §7 lists the real sources: motor current and electrical
/// frequency, battery voltage, fiber tension, bend radius and link margin, scene light level. None of them exist yet
/// — there is no drone and no flight model — so <see cref="UpdateFromCamera"/> derives all three from the free
/// camera, which is obviously wrong and is meant to be thrown away. The feed only ever reads the three fields
/// below, so replacing this class with the physics↔video interface touches nothing else.
public sealed class FeedSignals
{
    /// Fiber optical margin. 1 = plenty, 0 = at the receiver threshold. Feeds N2 sparkle density and the N12/N5–N7
    /// dropout hazard. Real source: fiber tension, bend radius and link state.
    public float LinkMargin = 1f;

    /// Total motor current, 0–1 of full. Feeds N3 visibility and N4 amplitude (both weak per the pilot, U6).
    public float MotorCurrent;

    /// Camera AE gain. 1 = bright day, 4 ≈ the night level of clip L. Feeds N1 grain σ and C3 saturation.
    public float Gain = 1f;

    Vector3 _lastCameraPos;

    /// Stand-in: link margin falls with distance from a notional pilot at the map origin (as if the fiber were
    /// paying out), and motor current rises with camera speed.
    public void UpdateFromCamera(Vector3 position, double dt)
    {
        float paidOut = new Vector2(position.X, position.Z).Length();
        LinkMargin = Mathf.Clamp(1f - paidOut / 400f, 0f, 1f);
        float speed = dt > 0 ? (position - _lastCameraPos).Length() / (float)dt : 0f;
        MotorCurrent = Mathf.Clamp(speed / 30f, 0f, 1f);
        _lastCameraPos = position;
    }
}
