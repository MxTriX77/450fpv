using System;

namespace Fpv450.Feed
{
    /// <summary>
    /// Every value the feed pass reads per field, as the Godot feed's <c>VideoFeed._Process</c> handed them to
    /// <c>video_feed.gdshader</c> (<c>docs/legacy/video-feed-godot/src/VideoFeed.cs:141-169</c>): the scheduler's
    /// events and loss state, the seed and field, and the three signals the shader reads. The engine side copies this
    /// into the material (<c>FeedFeature</c>); keeping it here, engine-free, lets the dotnet tests hold it to Godot.
    /// </summary>
    public struct FeedUniforms
    {
        public float Gain, LinkMargin, MotorCurrent, Seed, Field;
        public float N3Amp, N3Roll, N3Spacing, N4Amp, N4Span, Degrade;
        public float LossState, LossSplit, LossTopStrip, BlackA, BlackB, SnowLevel, SnowChroma;
        public float Tell, TearAmp, TearTop, GrainBoost;
        /// 1 draws the black wedge a tear cuts in from the right edge (gd:382-385), the "black sticks"; 0 leaves the
        /// tear its sideways shear alone. 1 only with <see cref="FeedEvents.GodotLinkArtefacts"/> (D-065).
        public float TearWedge;

        /// <summary>
        /// The uniforms for the field <paramref name="events"/> stands at, in VideoFeed.cs:141-169's order.
        /// <paramref name="gain"/> is the camera's AE gain (1 by day, until task 1.7's loop drives it), and
        /// <paramref name="linkMargin"/> and <paramref name="motorCurrent"/> are 0–1, as the shader takes them.
        /// </summary>
        public static FeedUniforms From(FeedEvents events, int seed, float gain, float linkMargin, float motorCurrent)
        {
            FeedLoss loss = events.Loss;
            return new FeedUniforms
            {
                Gain = gain,
                LinkMargin = linkMargin,
                MotorCurrent = motorCurrent,
                Seed = seed,                       // VideoFeed.cs:144, (float)_seed
                Field = events.Field,              // VideoFeed.cs:145, (float)_events.Field
                N3Amp = events.N3Amp,
                N3Roll = events.N3Roll,
                N3Spacing = events.N3Spacing,
                N4Amp = events.N4Amp,
                N4Span = events.N4Span,
                Degrade = events.Degrade,
                LossState = (int)loss.Current,     // VideoFeed.cs:159, the shader's 0 picture … 4 black
                LossSplit = loss.Split,
                LossTopStrip = loss.TopStrip,
                BlackA = loss.BlackA,
                BlackB = loss.BlackB,
                SnowLevel = loss.SnowLevel,
                SnowChroma = loss.SnowChroma,
                Tell = loss.Tell,
                TearAmp = loss.TearAmp,
                TearTop = loss.TearTop,
                GrainBoost = loss.GrainBoost,
                TearWedge = events.GodotLinkArtefacts ? 1f : 0f,
            };
        }

        /// <summary>The uniforms for <paramref name="signals"/>: <see cref="From"/> with the K§11.8 signals mapped
        /// to the shader's 0–1 scales by <see cref="FeedSignalScale"/>.</summary>
        public static FeedUniforms From(FeedEvents events, int seed, float gain, IFeedSignals signals)
            => From(events, seed, gain, FeedSignalScale.LinkMargin(signals.LinkMarginDb),
                FeedSignalScale.MotorCurrent(signals.MotorCurrentTotalA));
    }

    /// <summary>
    /// The two K§11.8 signals the Godot shader read on a 0–1 scale, mapped from their physical units (task 1.1 left
    /// them to 1.2). As in <see cref="FeedEvents"/>, every float operation is cast, so Mono rounds as CoreCLR does.
    /// </summary>
    public static class FeedSignalScale
    {
        /// What the Godot stand-in called full current: "what a hard-working heavy cargo quad pulls at full
        /// current" (FeedSignals.cs:37, <c>FullAmps = 68f</c>, with <c>Amps = FullAmps * MotorCurrent</c>). Its
        /// inverse is the port's scale until phase 5 sizes the real drone's current.
        public const float FullAmps = 68f;

        /// The optical margin, in dB above the receiver threshold, at which the shader's link margin reads 1
        /// ("plenty", gd:46). Assumed, from the receiver's physics until phase 7d measures the link: an analog
        /// intensity-modulated fiber link is thermal-noise limited, so its electrical SNR goes as the optical power
        /// squared, 2 dB per optical dB. A clean composite picture needs about 46 dB weighted video SNR and sync
        /// holds to about 20 dB, so the picture goes from clean to the threshold over 26 dB of SNR, 13 optical dB.
        public const float FullMarginDb = 13f;

        /// Total motor current in A to the shader's 0–1 (gd:47).
        public static float MotorCurrent(float amps) => Clamp01((float)(amps / FullAmps));

        /// The link's optical margin in dB to the shader's 0–1 (gd:46): 0 at the threshold, 1 from
        /// <see cref="FullMarginDb"/> up; an unmodelled fiber (infinite margin) reads 1.
        public static float LinkMargin(float marginDb) => Clamp01((float)(marginDb / FullMarginDb));

        static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Min(1f, Math.Max(0f, v));
    }
}
