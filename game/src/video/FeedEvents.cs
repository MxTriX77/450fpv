using System;
using Godot;

/// The feed's own randomness, advanced once per video field (50 Hz) and seeded, so a recorded flight replays to the
/// same feed (D-010). Nothing here is a fixed cycle: every event draws its own gap, depth and duration. N1 grain is
/// not scheduled at all — it is always on, and the shader redraws its pattern from (seed, field) every field, so the
/// picture is never clean and never loops.
public sealed class FeedEvents
{
    // ---------------------------------------------------------------------------------------------------------
    // TUNING. Retuning after the pilot watches it should be changing a number here, nothing else.
    // Gaps are drawn as Min + (Max - Min) * u², which puts the mean near the short end: short gaps are common and
    // long ones happen, so the viewer never learns a rhythm.
    // ---------------------------------------------------------------------------------------------------------

    /// Recovering dropouts (N12): a tell, a short outage, then the picture back — the pilot's "occasional cut", whose
    /// staging lives in <see cref="FeedLoss"/>. The pilot asked for these as a random event at 3–6 per minute.
    /// `docs/reference-notes/video-feed.md` measures ≈5/min in severe wind and rain and a free-running floor of
    /// 0.2/min otherwise, so this rate is the pilot's deliberate demo choice and the real driver is the fiber link
    /// margin. 5–32 s apart, mean ≈ 14 s.
    /// Terminal losses are never on this timer: <see cref="TriggerCut"/> is what fires them.
    const double DropoutGapMinSeconds = 5.0, DropoutGapMaxSeconds = 32.0;

    /// Quality drops: the feed slides into a heavier degraded look and back out. Noticeably more frequent than the
    /// cuts. 1–15 s apart, mean ≈ 5.7 s ⇒ about 10 per minute. Nearest thing measured is N12 (≈5/min in severe wind
    /// and rain, none in 1.3 min of calmer flight), so this is above the footage on purpose, and tunable.
    const double DropGapMinSeconds = 1.0, DropGapMaxSeconds = 15.0;
    /// 0.3–2.2 s, shaped by a sine envelope, so some are a blink and some ride for a couple of seconds.
    const double DropMinSeconds = 0.3, DropMaxSeconds = 2.2;
    /// How deep a drop can go. 0 is the baseline picture, 1 is the heaviest degraded look.
    const float DropMinDepth = 0.25f, DropMaxDepth = 1.0f;

    // N3 rolling lines, from clip C: ~50 onsets per minute, runs of 2–24 recorded frames, lines 150–157 px apart
    // rolling 14–60 px per recorded frame. Converted to fields (1 recorded frame ≈ 1.67 fields) and to field lines.
    const double N3OnsetPerField = 50.0 / 60.0 / 50.0;
    const int N3MinFields = 3, N3MaxFields = 40;
    // N4 diagonal interference, from clip B: ~8.5 runs/s, runs of 1–3 recorded frames, covering 20–50 % of the field.
    const double N4OnsetPerField = 8.5 / 50.0;
    const int N4MinFields = 2, N4MaxFields = 5;

    const double Fields = 50.0;

    /// Whether this flight's airframe has the trait at all. The footage has each in 1 of its 3 airframe groups, so
    /// the real draw is p ≈ 1/3 per flight; the feed forces both on so they are part of the look.
    public bool HasN3 = true, HasN4 = true;

    /// The field counter the shader hashes on. Pinned by `--video-field` for a reproducible still.
    public int Field;

    public float N3Amp, N3Roll, N3Spacing = 40f;
    public float N4Amp, N4Span;
    /// 0 = the baseline picture, 1 = the heaviest quality drop.
    public float Degrade;

    /// The receiver's loss of picture: the staged sequences of N5–N8 and N12.
    public readonly FeedLoss Loss;

    readonly Random _rng;
    int _n3Left, _n4Left;
    float _n3RollRate;
    int _dropLeft, _dropTotal, _dropWait;
    float _dropDepth;
    int _dropoutWait;

    public FeedEvents(int seed)
    {
        _rng = new Random(seed);
        Loss = new FeedLoss(_rng);
        _dropWait = DrawGap(DropGapMinSeconds, DropGapMaxSeconds);
        _dropoutWait = DrawGap(DropoutGapMinSeconds, DropoutGapMaxSeconds);
    }

    /// Advances to `field`, replaying every field in between, so the result depends only on the seed and the field.
    public void AdvanceTo(int field, float motorCurrent)
    {
        while (Field < field)
        {
            Field++;
            Step(motorCurrent);
        }
    }

    /// Fires a terminal loss of picture now: the staged sequence of N5–N7, or the hard cut. The hook the flight model
    /// calls on an impact, a battery collapse or a fiber break. `abrupt` is true when the optical margin vanished
    /// within one field (a clean break, or power cut) and false when it faded first (the fiber stretching or bending),
    /// which is what picks the variant. Nothing on a timer calls this.
    public void TriggerCut(bool abrupt = false) => Loss.StartTerminal(abrupt);

    void Step(float motorCurrent)
    {
        StepDrop();
        StepLoss();

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
            // Motor current only modulates depth; the pilot says throttle coupling is rare (U6). A quality drop
            // makes the lines heavier, which is how the footage's worst stretches read.
            N3Amp = _n3Left > 0 ? (0.045f + 0.05f * motorCurrent) * (1f + Degrade) : 0f;
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
            N4Amp = _n4Left > 0 ? (0.022f + 0.02f * motorCurrent) * (1f + 1.5f * Degrade) : 0f;
        }
    }

    void StepDrop()
    {
        if (_dropLeft > 0)
        {
            _dropLeft--;
            // One sine arch over the whole event: it slides in and back out, and no two are the same shape because
            // the depth and the length are both drawn.
            float t = 1f - (float)_dropLeft / _dropTotal;
            Degrade = _dropDepth * Mathf.Sin(Mathf.Pi * t);
            return;
        }
        Degrade = 0f;
        if (--_dropWait > 0)
            return;
        _dropTotal = _dropLeft = DrawFields(DropMinSeconds, DropMaxSeconds);
        _dropDepth = DropMinDepth + (DropMaxDepth - DropMinDepth) * (float)_rng.NextDouble();
        _dropWait = DrawGap(DropGapMinSeconds, DropGapMaxSeconds);
    }

    /// Advances the loss sequence, and starts a recovering dropout when the timer comes up and nothing is running.
    /// A terminal blue screen never clears, so once one is up the timer does nothing.
    void StepLoss()
    {
        if (!Loss.Busy && --_dropoutWait <= 0)
        {
            Loss.StartDropout();
            _dropoutWait = DrawGap(DropoutGapMinSeconds, DropoutGapMaxSeconds);
        }
        Loss.Step();
    }

    /// A gap in fields, squared so short gaps are common and long ones still happen.
    int DrawGap(double minSeconds, double maxSeconds)
    {
        double u = _rng.NextDouble();
        return (int)((minSeconds + (maxSeconds - minSeconds) * u * u) * Fields);
    }

    int DrawFields(double minSeconds, double maxSeconds)
        => Math.Max(1, (int)((minSeconds + (maxSeconds - minSeconds) * _rng.NextDouble()) * Fields));
}
