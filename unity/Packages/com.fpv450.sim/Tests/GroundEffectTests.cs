using System.IO;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // airframe-effects' "Ground effect, ceiling effect and recirculation per rotor" (p7b-airframe 1.3) on the stub.
    public sealed class GroundEffectTests
    {
        [Test]
        public void TheFormsAreThePublishedOnesAndFadeOutAtTheReach()
        {
            GroundEffectSpec g = RepositoryDrone.Spec().BuildGroundEffect();
            Assert.That(g.GroundGain(0.6, 1), Is.EqualTo(1 / (1 - 1 / (16 * 0.36) + 1 / (16 * 64.0))).Within(1e-12), "Cheeseman and Bennett less their value at the reach");
            Assert.That(g.GroundGain(0.6, 1) / (1 / (1 - 1 / (16 * 0.36))), Is.EqualTo(1).Within(0.002), "within 0.2 % of theirs");
            Assert.That(g.GroundGain(0.6, 0.5), Is.LessThan(g.GroundGain(0.6, 1)), "forward speed fades it");
            Assert.That(g.GroundGain(g.Reach, 1), Is.EqualTo(1));
            Assert.That(g.GroundGain(g.Reach * (1 - 1e-9), 1), Is.EqualTo(1).Within(1e-9), "no step at the reach");
            Assert.That(g.GroundGain(0.1, 1), Is.EqualTo(g.GroundGain(g.LowestHeight, 1)), "held below the lowest height");
            Assert.That(g.GroundGain(double.PositiveInfinity, 1), Is.EqualTo(1), "no ground");
            Assert.That(g.CeilingGain(g.Reach * (1 - 1e-9), 1), Is.EqualTo(1).Within(1e-9));
            Assert.That(g.CeilingGain(1, 1), Is.EqualTo(System.Math.Pow(1 + g.Gamma(1) - g.Gamma(1 / g.Reach), 2.0 / 3)).Within(1e-12));
            Assert.That(g.Sigma(g.RecirculationOnset), Is.EqualTo(0));
            Assert.That(g.Sigma(g.LowestHeight), Is.EqualTo(g.RecirculationSigma));
        }

        [Test]
        public void TheCushionAndTheHole()
        {
            GroundEffectScenario.Surfaces c = GroundEffectScenario.Cushion(RepositoryDrone.Spec());
            TestContext.Out.WriteLine($"cushion: rotors over the floor 0x{c.Over:X} at {c.DistanceOver:F4} D gain {c.LeastOver:F5}-{c.MostOver:F5} against "
                + $"Cheeseman-Bennett {c.Expected:F5}; over the hole 0x{c.Away:X}, distance {c.DistanceAway} D, |gain - 1| {c.Away1:E2}");
            Assert.That(c.DistanceOver, Is.EqualTo(GroundEffectScenario.Height).Within(1e-12), "the floor found 0.3 D down the axis");
            Assert.That(c.DistanceAway, Is.EqualTo(double.PositiveInfinity), "no ground over the hole");
            Assert.That(GroundEffectScenario.Within(c), Is.True);
        }

        [Test]
        public void TheCeilingUnderARuntimeRoof()
        {
            GroundEffectScenario.Surfaces c = GroundEffectScenario.Ceiling(RepositoryDrone.Spec());
            TestContext.Out.WriteLine($"ceiling: rotors under the roof 0x{c.Over:X} at {c.DistanceOver:F4} D gain {c.LeastOver:F5}-{c.MostOver:F5} against "
                + $"Hsiao-Chirarattananon {c.Expected:F5}; in the open 0x{c.Away:X}, |gain - 1| {c.Away1:E2}");
            Assert.That(c.DistanceOver, Is.EqualTo(GroundEffectScenario.Height).Within(1e-9), "the roof found 0.3 D up the axis");
            Assert.That(GroundEffectScenario.Within(c), Is.True);
        }

        [Test]
        public void TheCloseHoverIsNeverStill()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            GroundEffectScenario.Hover close = GroundEffectScenario.FlyHover(spec, true, GroundEffectScenario.HoverLift);
            GroundEffectScenario.Hover wrong = GroundEffectScenario.FlyHover(spec, false, GroundEffectScenario.HoverLift);
            GroundEffectScenario.Hover high = GroundEffectScenario.FlyHover(spec, true, GroundEffectScenario.HighLift);
            foreach ((string name, GroundEffectScenario.Hover h) in new[] { ("close", close), ("known-wrong, no recirculation", wrong), ("high", high) })
                TestContext.Out.WriteLine($"{name}: skids {h.Clearance:F3} m up, rotors at {h.LeastHeight:F2}-{h.MostHeight:F2} R; body rate RMS per "
                    + $"{GroundEffectScenario.Block} s block {h.LeastRate:E2}-{h.MostRate:E2} rad/s over {h.Blocks} blocks; wash up to {h.MostWash:E2} v_h");
            Assert.That(GroundEffectScenario.NeverStill(close, wrong, high), Is.True);
            Assert.That(GroundEffectScenario.NeverStill(wrong, wrong, high), Is.False, "the model without recirculation hovers still");
        }

        [Test]
        public void ARecordedLowHoverReplaysBitForBit()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = GroundEffectScenario.HoverSetup(spec, true, out double stand);
            var core = new FlightCore(setup);
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(spec, spec.Nominal);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
            GroundEffectScenario.Hover r = GroundEffectScenario.FlyHover(core, spec, stand, GroundEffectScenario.HoverLift,
                (c, stick) => recorder.RecordStep(c.State, stick, c.Trace, c.Events));
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            log.Position = 0;
            ReplayResult replay = FlightReplay.Run(log, id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash);
            TestContext.Out.WriteLine(replay.Message + ": " + replay.Steps + " steps, " + replay.Events + " events; wash up to " + r.MostWash.ToString("E2") + " v_h");
            Assert.That(r.MostWash, Is.GreaterThan(0), "the recorded hover re-ingests its wash");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }
    }
}
