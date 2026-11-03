using NUnit.Framework;

namespace Fpv450.Feed.Tests
{
    /// <summary>
    /// The pass's per-field uniforms against what the Godot feed handed its shader (VideoFeed.cs:141-169), and the
    /// signal scales task 1.1 left to 1.2 (p4-feed task 1.2). Run by Unity's EditMode and by
    /// <c>dotnet test tools/feedtests</c>.
    /// </summary>
    public sealed class FeedUniformsTests
    {
        /// The loss states keep the shader's numbering (gd:58: 0 picture, 1 partial snow, 2 blue, 3 snow, 4 black).
        [Test]
        public void TheLossStatesKeepTheShadersNumbers()
        {
            Assert.That((int)FeedLoss.State.Picture, Is.EqualTo(0));
            Assert.That((int)FeedLoss.State.PartialSnow, Is.EqualTo(1));
            Assert.That((int)FeedLoss.State.Blue, Is.EqualTo(2));
            Assert.That((int)FeedLoss.State.Snow, Is.EqualTo(3));
            Assert.That((int)FeedLoss.State.Black, Is.EqualTo(4));
        }

        /// Every field the pass reads is the scheduler's own value, field by field, through a staged loss.
        [Test]
        public void TheUniformsAreTheSchedulersValues()
        {
            FeedSchedule.Run(1, 640, 0f, 603, false, e =>
            {
                FeedUniforms u = FeedUniforms.From(e, 1, 1f, 1f, 0f);
                FeedLoss l = e.Loss;
                Assert.That(u.Field, Is.EqualTo((float)e.Field));
                Assert.That(u.Seed, Is.EqualTo(1f));
                Assert.That(new[] { u.N3Amp, u.N3Roll, u.N3Spacing, u.N4Amp, u.N4Span, u.Degrade },
                    Is.EqualTo(new[] { e.N3Amp, e.N3Roll, e.N3Spacing, e.N4Amp, e.N4Span, e.Degrade }));
                Assert.That(new[] { u.LossState, u.LossSplit, u.LossTopStrip, u.BlackA, u.BlackB, u.SnowLevel,
                        u.SnowChroma, u.Tell, u.TearAmp, u.TearTop, u.GrainBoost },
                    Is.EqualTo(new[] { (float)(int)l.Current, l.Split, l.TopStrip, l.BlackA, l.BlackB, l.SnowLevel,
                        l.SnowChroma, l.Tell, l.TearAmp, l.TearTop, l.GrainBoost }));
            });
        }

        /// The stubs are the parked drone with no fiber, which is what the Godot captures pin with
        /// `--video-signals 1,1,0`: gain 1, link margin 1, motor current 0.
        [Test]
        public void TheStubsReadAsGodotsPinnedSignals()
        {
            var events = new FeedEvents(1);
            events.AdvanceTo(600, 0f);
            FeedUniforms u = FeedUniforms.From(events, 1, 1f, new StubFeedSignals());
            Assert.That(new[] { u.Gain, u.LinkMargin, u.MotorCurrent }, Is.EqualTo(new[] { 1f, 1f, 0f }));
        }

        /// The tear's black wedge, the pilot's "black sticks", is Godot's link artefact: drawn only in the A/B
        /// against Godot (D-065).
        [Test]
        public void OnlyGodotsFeedDrawsTheTearsWedge()
        {
            var events = new FeedEvents(1);
            Assert.That(FeedUniforms.From(events, 1, 1f, 1f, 0f).TearWedge, Is.Zero);
            events.GodotLinkArtefacts = true;
            Assert.That(FeedUniforms.From(events, 1, 1f, 1f, 0f).TearWedge, Is.EqualTo(1f));
        }

        /// D-078, the pilot's C: the noise off zeroes N3, N4, the tear and the degrade and marks N1 and N2 off for the
        /// pass, field by field through quality drops, N3, N4 and a hard cut's bottom tear (seed 5, abrupt); every other
        /// value is the noise-on value, bit for bit. The noise on is <c>From</c> as it was, with no flag set.
        [Test]
        public void TheNoiseOffZeroesEachNoiseTermAndNothingElse()
        {
            bool n3 = false, n4 = false, tear = false, degrade = false;
            FeedSchedule.Run(5, 7600, 0.6f, 7500, true, e =>
            {
                FeedUniforms on = FeedUniforms.From(e, 5, 1.3f, 0.7f, 0.6f);
                FeedUniforms off = on.WithoutNoise();
                Assert.That(on.NoiseOff, Is.False, "on: the ported feed");
                Assert.That(new[] { on.N3Amp, on.N4Amp, on.TearAmp, on.Degrade },
                    Is.EqualTo(new[] { e.N3Amp, e.N4Amp, e.Loss.TearAmp, e.Degrade }));
                Assert.That(off.NoiseOff, Is.True, "off: N1 and N2 out of the pass");
                Assert.That(new[] { off.N3Amp, off.N4Amp, off.TearAmp, off.Degrade }, Is.EqualTo(new[] { 0f, 0f, 0f, 0f }));
                Assert.That(new[] { off.Gain, off.LinkMargin, off.MotorCurrent, off.Seed, off.Field, off.N3Roll,
                        off.N3Spacing, off.N4Span, off.LossState, off.LossSplit, off.LossTopStrip, off.BlackA, off.BlackB,
                        off.SnowLevel, off.SnowChroma, off.Tell, off.TearTop, off.GrainBoost, off.TearWedge },
                    Is.EqualTo(new[] { on.Gain, on.LinkMargin, on.MotorCurrent, on.Seed, on.Field, on.N3Roll,
                        on.N3Spacing, on.N4Span, on.LossState, on.LossSplit, on.LossTopStrip, on.BlackA, on.BlackB,
                        on.SnowLevel, on.SnowChroma, on.Tell, on.TearTop, on.GrainBoost, on.TearWedge }));
                n3 |= on.N3Amp > 0f;
                n4 |= on.N4Amp > 0f;
                tear |= on.TearAmp > 0f;
                degrade |= on.Degrade > 0f;
            });
            Assert.That(new[] { n3, n4, tear, degrade }, Is.EqualTo(new[] { true, true, true, true }),
                "the run reaches N3, N4, the tear and a quality drop");
        }

        /// Motor current: the Godot stand-in's 68 A full current (FeedSignals.cs:37), clamped to 0–1.
        [TestCase(0f, 0f)]
        [TestCase(34f, 0.5f)]
        [TestCase(68f, 1f)]
        [TestCase(120f, 1f)]
        [TestCase(-5f, 0f)]
        public void MotorCurrentIsAFractionOf68A(float amps, float fraction)
        {
            Assert.That(FeedSignalScale.MotorCurrent(amps), Is.EqualTo(fraction));
        }

        /// Link margin: 0 at the receiver threshold, 1 from 13 dB up, and an unmodelled fiber reads 1.
        [TestCase(0f, 0f)]
        [TestCase(6.5f, 0.5f)]
        [TestCase(13f, 1f)]
        [TestCase(30f, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(-3f, 0f)]
        [TestCase(float.NaN, 0f)]
        public void LinkMarginIsAFractionOf13Db(float db, float fraction)
        {
            Assert.That(FeedSignalScale.LinkMargin(db), Is.EqualTo(fraction));
        }
    }
}
