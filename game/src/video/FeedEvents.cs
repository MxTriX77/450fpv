using System;
using Godot;

/// The feed's own randomness, advanced once per video field (50 Hz) and seeded, so a recorded flight replays to the
/// same feed (D-010). Nothing here is a fixed cycle: N3 and N4 fire stochastically at the rates measured in
/// docs/reference-notes/video-feed.md §1.2, and hold for a drawn number of fields. N1 grain is not scheduled at all —
/// it is always on, and the shaders redraw its pattern from (seed, field), so it is never clean and never loops.
///
/// Every rate below is a tunable with wide error: the notes measure them over 1.2–1.3 min from three airframe
/// groups, so a trait seen once is only 1/min ± a factor of several.
public sealed class FeedEvents
{
    // N3 rolling lines, from clip C: ~50 onsets per minute, runs of 2–24 recorded frames, lines 150–157 px apart
    // rolling 14–60 px per recorded frame. Converted to fields (1 recorded frame ≈ 1.67 fields) and to field lines.
    const double N3OnsetPerField = 50.0 / 60.0 / 50.0;
    const int N3MinFields = 3, N3MaxFields = 40;
    // N4 diagonal interference, from clip B: ~8.5 runs/s, runs of 1–3 recorded frames, covering 20–50 % of the field.
    const double N4OnsetPerField = 8.5 / 50.0;
    const int N4MinFields = 2, N4MaxFields = 5;

    /// Whether this flight's airframe has the trait at all. The footage has each in 1 of its 3 airframe groups, so
    /// the real draw is p ≈ 1/3 per flight; the spike forces both on so the pilot can see them.
    public bool HasN3 = true, HasN4 = true;

    /// The field counter the shaders hash on. Pinned by `--video-field` for a reproducible still.
    public int Field;

    public float N3Amp, N3Roll, N3Spacing = 40f;
    public float N4Amp, N4Span;

    readonly Random _rng;
    int _n3Left, _n4Left;
    float _n3RollRate;

    public FeedEvents(int seed) => _rng = new Random(seed);

    /// Advances to `field`, replaying every field in between so the result depends only on the seed and the field.
    public void AdvanceTo(int field, float motorCurrent)
    {
        while (Field < field)
        {
            Field++;
            Step(motorCurrent);
        }
    }

    void Step(float motorCurrent)
    {
        if (HasN3)
        {
            if (_n3Left > 0)
            {
                _n3Left--;
                N3Roll = (N3Roll + _n3RollRate / N3Spacing) % 1f;
            }
            else if (_rng.NextDouble() < N3OnsetPerField)
            {
                _n3Left = _rng.Next(N3MinFields, N3MaxFields + 1);
                N3Spacing = 39f + 3f * (float)_rng.NextDouble();  // 150–157 px of picture height
                _n3RollRate = 2f + 7f * (float)_rng.NextDouble(); // field lines per field
            }
            // Motor current only modulates depth; the pilot says throttle coupling is rare (U6).
            N3Amp = _n3Left > 0 ? 0.055f + 0.05f * motorCurrent : 0f;
        }

        if (HasN4)
        {
            if (_n4Left > 0)
                _n4Left--;
            else if (_rng.NextDouble() < N4OnsetPerField)
            {
                _n4Left = _rng.Next(N4MinFields, N4MaxFields + 1);
                N4Span = 0.20f + 0.30f * (float)_rng.NextDouble();
            }
            N4Amp = _n4Left > 0 ? 0.030f + 0.025f * motorCurrent : 0f;
        }
    }

    /// Hook for the loss sequences (N5–N7). They are event-driven in the notes — impact, power loss, fiber break —
    /// never a random rate, so nothing fires them until physics exists to say a link was lost.
    public void TriggerLoss() => GD.PrintErr("VideoFeed: loss sequence is not implemented in the spike.");
}
