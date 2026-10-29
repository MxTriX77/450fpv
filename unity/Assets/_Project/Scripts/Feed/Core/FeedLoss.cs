using System;

namespace Fpv450.Feed
{
    /// <summary>
    /// The receiver's loss of picture, staged, at field rate (50 Hz).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Ported from the Godot feed</b> (D-026, D-027: ported, never merged): <c>game/src/video/FeedLoss.cs</c> at the
    /// tag <c>godot-final</c>, copied verbatim in <c>docs/legacy/video-feed-godot/src/FeedLoss.cs</c>. The state
    /// machine, constants, draw order and structure are kept as they are; <c>FeedLoss.cs:N</c> after a literal names
    /// its line there. It draws from the scheduler's own <see cref="Random"/>, as in Godot, so the two share one
    /// seeded stream. As in <see cref="FeedEvents"/>, every float operation is cast to <c>float</c>, so that Unity's
    /// Mono, which evaluates float expressions in double precision, rounds as CoreCLR (Godot's runtime) does.
    /// </para>
    /// <para>
    /// The first Godot cut snapped to flat snow and snapped back; the pilot flew it and said the cut is "too sharp
    /// now, like it just appears and just disappears". `docs/reference-notes/video-feed.md` N5–N8 and N12 measure
    /// what the footage actually does, and this is that: a sequence of stages, each with its own drawn length, run
    /// one after the other. Three sequences exist.
    /// </para>
    /// <code>
    ///   Recovering dropout (N12, the pilot's "occasional cut"): StartDropout
    ///       tell (2–5 fields) → blue 18–35 fields, or black 3–4 fields → picture, clean
    ///   Staged terminal loss (N5–N7): StartTerminal with a margin that faded
    ///       partial snow 1–5 → blue 5–8 → full snow 10–15 → blue until reset
    ///   Hard cut (N5–N7 variant 2): StartTerminal with a margin that vanished within one field
    ///       glitch 2–5 fields (a tear, or a grain rise) → blue until reset
    /// </code>
    /// <para>
    /// <b>The recovered picture and OSD come back clean</b> (PR-4 correction): the receiver's text lines belong to
    /// the blue and snow screens only, never over the picture. N12 measures clip P holding them ≈7.3 s over the
    /// recovered picture; the pilot overruled that, so do not restore it from the notes.
    /// </para>
    /// <para>
    /// <b>There is always a tell before the picture goes</b> (PR-4). The footage shows one in 2 of 4 dropouts and 2
    /// of 4 hard cuts; the pilot's requirement raises both to every time, because a cut with no warning is the thing
    /// they rejected.
    /// </para>
    /// <para>
    /// Every duration below is a named constant in fields, converted from the recorded frames the notes measure
    /// (≈29.9 fps, so one recorded frame ≈ 1.67 fields). Nothing is a fixed cycle: each stage draws its own length,
    /// and the snow level, the split line and the tell type are drawn per event.
    /// </para>
    /// <para>
    /// <b>One fix to the Godot code</b> (p4-feed task 1.3, the recovering-dropout scenario): Godot cleared the
    /// picture in the same step as a dropout's last outage field (FeedLoss.cs:206-207), so that field showed the
    /// picture. A blue-out drawn at 18–35 fields showed 17–34, a black dropout drawn at 3–4 showed 2–3, and the black
    /// never left partway down a field. Here the last field shows its stage and the next field clears. No draw moves
    /// and <see cref="Busy"/> falls on the same field, so every later event is Godot's; only that one field per
    /// dropout differs. <c>godotLastField</c> restores Godot's step, for the A/B against the original.
    /// </para>
    /// </remarks>
    public sealed class FeedLoss
    {
        /// What the receiver is putting on screen. The shader switches on this.
        public enum State { Picture, PartialSnow, Blue, Snow, Black }

        // -------------------------------------------------------------------------------------------------------
        // MEASURED RANGES. video-feed.md N5–N8, N12. Retuning is changing a number here, nothing else.
        // -------------------------------------------------------------------------------------------------------

        /// N12 precursor: 1–3 recorded frames of brightening (+25 % to +120 % mean luma) under dense bands of
        /// coloured impulse dashes, or one N8-style tear. Of P's three tells, two were brightening and one a tear.
        const int TellMinFields = 2, TellMaxFields = 5; // FeedLoss.cs:38
        const float TellLumaMin = 0.25f, TellLumaMax = 1.20f; // FeedLoss.cs:39
        const float TellTearChance = 1f / 3f; // FeedLoss.cs:40

        /// N12 outage. The short one is black including the OSD (1 of 4 events, ≈2 recorded frames); the rest are
        /// a hard cut to the blue screen for 0.37–0.70 s (measured 12–20 recorded frames).
        /// The black enters and leaves partway down a field: measured at row ≈585 of 1080 going in and coming out.
        const float BlackChance = 0.25f; // FeedLoss.cs:45
        const int BlackMinFields = 3, BlackMaxFields = 4; // FeedLoss.cs:46
        const float BlackEdgeMin = 0.40f, BlackEdgeMax = 0.70f; // FeedLoss.cs:47
        const int BlueOutMinFields = 18, BlueOutMaxFields = 35; // FeedLoss.cs:48

        /// N5 partial snow: snow down to a split line with the previous picture surviving below it, 1–2 recorded
        /// frames. The split sits at 82–88 % of the height and can move down between fields; in 1 loss of 3 a strip
        /// ≈11 % tall also survives at the top.
        const int PartialMinFields = 1, PartialMaxFields = 5; // FeedLoss.cs:53
        const float SplitMin = 0.80f, SplitMax = 0.90f, SplitDriftPerField = 0.015f; // FeedLoss.cs:54
        const float TopStripChance = 1f / 3f, TopStripHeight = 0.11f; // FeedLoss.cs:55

        /// N6. All three staged losses show the blue interlude for exactly 4 recorded frames, which points at a
        /// receiver timer, so the spread stays narrow. Then full snow for 7–8 recorded frames.
        const int InterludeMinFields = 5, InterludeMaxFields = 8; // FeedLoss.cs:59
        const int SnowMinFields = 10, SnowMaxFields = 15; // FeedLoss.cs:60

        /// The snow's mean level flickers between 52 and 65 levels, redrawn every 1–2 fields.
        const float SnowLevelMin = 52f / 255f, SnowLevelMax = 65f / 255f; // FeedLoss.cs:63
        /// The colour killer never quite takes the snow to grey: a killed snow field still measures a chroma median
        /// of 1.4–2.9 levels, against 7–10 on the first field of a partial snow.
        const float SnowChromaFloor = 0.08f; // FeedLoss.cs:66

        /// N5–N7 variant draw (hypothesis, U2): a margin that faded over a field or longer (the fiber stretching or
        /// bending before it breaks) mostly gives the staged sequence; one that vanished within a field mostly gives
        /// a hard cut. Measured 3 staged of 7 losses, all on this receiver type.
        const float StagedChanceFaded = 0.8f, StagedChanceAbrupt = 0.2f; // FeedLoss.cs:71

        /// N8 tear amplitude, in units of the measured 20–60 px displacement at 1920 wide.
        const float TearMin = 0.45f, TearMax = 1.0f; // FeedLoss.cs:74
        /// The hard cut's tell is one of the two the footage shows on it, 2 of 4 each: N8 tearing, or a ≈10 % rise
        /// in grain over the last 1–2 picture fields.
        const float HardCutTearChance = 0.5f; // FeedLoss.cs:77
        const float GrainRise = 0.10f; // FeedLoss.cs:78

        /// One stage of a sequence. `Fields` is how long it holds; Forever is the terminal blue.
        struct Stage
        {
            public State S;
            public int Fields;
            public float Tell;      // brightening, 0 = none
            public float Tear;      // N8 amplitude, 0 = none
            public float TearTop;   // 1 = the shear is at the top of the field (N12 f1144), 0 = the bottom (N8, H f628)
            public float Grain;     // extra grain, as a fraction
        }

        const int Forever = int.MaxValue; // FeedLoss.cs:91

        readonly Random _rng;
        readonly bool _godotLastField;
        readonly Stage[] _stages = new Stage[4]; // FeedLoss.cs:94
        int _count, _index, _left;
        int _snowLevelLeft;
        int _fieldsInStage;

        public State Current { get; private set; } = State.Picture;
        /// Fraction of the picture height where partial snow ends and the surviving picture begins.
        public float Split { get; private set; }
        /// Fraction of the height of a picture strip surviving at the top of partial snow, 0 when there is none.
        public float TopStrip { get; private set; }
        /// The rows the black dropout covers, as fractions of the height. It enters and leaves partway down a field,
        /// so the first and last fields of the outage are only partly black.
        public float BlackA { get; private set; }
        public float BlackB { get; private set; } = 1f; // FeedLoss.cs:107
        /// The snow field's mean level, 0–1.
        public float SnowLevel { get; private set; }
        /// 1 while the snow still carries colour, fading to 0 as the receiver's colour killer engages.
        public float SnowChroma { get; private set; }
        /// Extra luma as a fraction, and the flag that the impulse dashes come in dense bands with it (N12 precursor).
        public float Tell { get; private set; }
        public float TearAmp { get; private set; }
        public float TearTop { get; private set; }
        public float GrainBoost { get; private set; }
        /// True while a sequence is running, so nothing schedules another on top of it.
        public bool Busy => _count > 0;

        /// `godotLastField` steps a sequence's end as the Godot code did, the last outage field already showing the
        /// picture (see the remarks); only the A/B against the original sets it.
        public FeedLoss(Random rng, bool godotLastField = false)
        {
            _rng = rng;
            _godotLastField = godotLastField;
        }

        /// The recovering dropout of N12: a tell, a short outage, then the picture back. This is what the pilot's
        /// "occasional cut" is, and it is the one sequence a timer may fire.
        public void StartDropout()
        {
            _count = 0;
            Push(DrawTell(false));
            if (_rng.NextDouble() < BlackChance)
                Push(new Stage { S = State.Black, Fields = Draw(BlackMinFields, BlackMaxFields) });
            else
                Push(new Stage { S = State.Blue, Fields = Draw(BlueOutMinFields, BlueOutMaxFields) });
            Begin();
        }

        /// The terminal loss of N5–N7, for the flight model to call on impact, power loss or a fiber break. `abrupt`
        /// is true when the optical margin vanished within one field (a clean break, or power cut) and false when it
        /// faded over a field or longer (the fiber stretching or bending first); that is what picks the variant.
        public void StartTerminal(bool abrupt = false)
        {
            _count = 0;
            if (_rng.NextDouble() < (abrupt ? StagedChanceAbrupt : StagedChanceFaded))
            {
                Split = (float)(SplitMin + (float)((SplitMax - SplitMin) * (float)_rng.NextDouble()));
                TopStrip = _rng.NextDouble() < TopStripChance ? TopStripHeight : 0f;
                Push(new Stage { S = State.PartialSnow, Fields = Draw(PartialMinFields, PartialMaxFields) });
                Push(new Stage { S = State.Blue, Fields = Draw(InterludeMinFields, InterludeMaxFields) });
                Push(new Stage { S = State.Snow, Fields = Draw(SnowMinFields, SnowMaxFields) });
            }
            else
            {
                Push(DrawTell(true));
            }
            Push(new Stage { S = State.Blue, Fields = Forever });
            Begin();
        }

        /// Advances one video field.
        public void Step()
        {
            if (_count == 0)
            {
                Clear();
                return;
            }

            Stage s = _stages[_index];
            Current = s.S;
            Tell = s.Tell;
            TearAmp = s.Tear;
            TearTop = s.TearTop;
            GrainBoost = s.Grain;

            // Snow: a new mean level every 1–2 fields, and the colour killer engaging over the first two fields.
            if (s.S == State.Snow || s.S == State.PartialSnow)
            {
                if (--_snowLevelLeft <= 0)
                {
                    SnowLevel = (float)(SnowLevelMin
                        + (float)((SnowLevelMax - SnowLevelMin) * (float)_rng.NextDouble()));
                    _snowLevelLeft = 1 + _rng.Next(2); // FeedLoss.cs:179
                }
                // The first snow field is coloured (chroma 7–10 levels), the second paler (4–7), then killed. The
                // full snow of stage 3 is already colour-killed when it starts.
                SnowChroma = s.S == State.PartialSnow
                    ? Math.Max(SnowChromaFloor, (float)(1f - (float)(0.5f * _fieldsInStage))) // FeedLoss.cs:184
                    : SnowChromaFloor;
                if (s.S == State.PartialSnow && _fieldsInStage > 0)
                    Split = Math.Min(0.97f, (float)(Split + SplitDriftPerField)); // FeedLoss.cs:187
            }
            else if (s.S == State.Black)
            {
                bool first = _fieldsInStage == 0, last = _left == 1;
                BlackA = first ? BlackEdge() : 0f;
                BlackB = last && !first ? BlackEdge() : 1f;
            }

            _fieldsInStage++;
            if (s.Fields == Forever)
                return;
            if (--_left > 0)
                return;
            _index++;
            _fieldsInStage = 0;
            if (_index < _count)
            {
                _left = _stages[_index].Fields;
                return;
            }
            // The sequence is over, so nothing holds off the next one, but this field still shows its last stage;
            // the next Step clears it (task 1.3's fix). Godot cleared it here.
            _count = 0;
            if (_godotLastField)
                Clear();
        }

        /// The tell that always comes before the picture goes. The footage gives a different pair for each sequence,
        /// so the draw differs too: a dropout brightens under dense bands of impulse dashes, or tears at the top of
        /// the field (P f1144); a hard cut tears at the bottom (N8, H f628), or raises its grain by ≈10 % (A f424).
        Stage DrawTell(bool terminal)
        {
            var s = new Stage { S = State.Picture, Fields = Draw(TellMinFields, TellMaxFields) };
            bool tear = _rng.NextDouble() < (terminal ? HardCutTearChance : TellTearChance);
            if (tear)
            {
                s.Tear = (float)(TearMin + (float)((TearMax - TearMin) * (float)_rng.NextDouble()));
                s.TearTop = terminal ? 0f : 1f; // FeedLoss.cs:222
            }
            else if (terminal)
            {
                s.Grain = GrainRise;
            }
            else
            {
                s.Tell = (float)(TellLumaMin + (float)((TellLumaMax - TellLumaMin) * (float)_rng.NextDouble()));
            }
            return s;
        }

        float BlackEdge()
            => (float)(BlackEdgeMin + (float)((BlackEdgeMax - BlackEdgeMin) * (float)_rng.NextDouble()));

        void Push(Stage s)
        {
            if (_count < _stages.Length)
                _stages[_count++] = s;
        }

        void Begin()
        {
            _index = 0;
            _left = _stages[0].Fields;
            _fieldsInStage = 0;
            _snowLevelLeft = 0;
        }

        void Clear()
        {
            Current = State.Picture;
            Tell = TearAmp = TearTop = GrainBoost = SnowChroma = 0f;
            BlackA = 0f;
            BlackB = 1f; // FeedLoss.cs:256
        }

        int Draw(int min, int max) => _rng.Next(min, max + 1);
    }
}
