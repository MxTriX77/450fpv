using System.IO;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // airframe-effects' "Heavy on low throttle" (VortexRingScenario, p7b-airframe 1.2), and the known-wrong model it fails.
    public sealed class VortexRingTests
    {
        [Test]
        public void HeavyOnLowThrottleEntersAndTheHigherThrottleDoesNot()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            VortexRingSpec ring = spec.BuildVortexRing();
            VortexRingScenario.Result low = VortexRingScenario.Fly(VortexRingScenario.Setup(spec), true);
            VortexRingScenario.Result lowOff = VortexRingScenario.Fly(VortexRingScenario.Setup(spec, false), true);
            VortexRingScenario.Result high = VortexRingScenario.Fly(VortexRingScenario.Setup(spec), false);
            TestContext.Out.WriteLine("low: " + VortexRingScenario.Describe(low));
            TestContext.Out.WriteLine("low, ring off: " + VortexRingScenario.Describe(lowOff));
            TestContext.Out.WriteLine("higher: " + VortexRingScenario.Describe(high));
            Assert.That(low.DescentAtHold, Is.InRange(2.9, 3.1), "the pilot's 3 m/s descent");
            Assert.That(low.EntriesBefore, Is.EqualTo(0), "no ring in the settled hover or the pilot's descent at 3 m/s");
            Assert.That(low.EntriesInHold, Is.GreaterThanOrEqualTo(1), "on low throttle a rotor enters its ring");
            Assert.That(low.EntryAfter, Is.LessThanOrEqualTo(0.1), "at once");
            Assert.That(low.EntryDescent, Is.LessThanOrEqualTo(3.5), "still at about 3 m/s");
            Assert.That(low.EntryRate, Is.GreaterThanOrEqualTo(ring.EntryRate), "at the entry rate");
            Assert.That(low.ThrustRatio, Is.LessThan(0.97), "it loses thrust");
            Assert.That(low.WobbleRms, Is.GreaterThanOrEqualTo(1.5 * lowOff.WobbleRms), "it wobbles");
            Assert.That(low.LowestHeld, Is.LessThan(ring.EntryRate), "out of the ring needs a lower rate than into it");
            Assert.That(low.ExitRate, Is.LessThan(ring.ExitRate), "and it leaves below the exit rate");
            Assert.That(VortexRingScenario.HeavyOnLowThrottle(low, lowOff, ring), Is.True);
            Assert.That(VortexRingScenario.HigherThrottle(high), Is.True, "the same descent at higher throttle does not enter");
            Assert.That(high.DescentAtHold, Is.EqualTo(low.DescentAtHold), "the same descent, bit for bit, until the hold");
        }

        // The ring's curves at Johnson's points, the hysteresis, the forward speed's escape, and what the propulsion derives.
        [Test]
        public void TheRingFollowsJohnsonsPoints()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            VortexRingSpec v = spec.BuildVortexRing();
            var p = new Propulsion(spec.BuildPropulsion(), null, v, default);
            double held = p.HeldInflow;
            TestContext.Out.WriteLine($"held branch's inflow {held:F4} v_h; the ring's added inflow 0 to η "
                + $"{v.EntryRate + (held - v.EntryInflow) / (v.PeakInflow - v.EntryInflow) * (v.PeakRate - v.EntryRate):F3}, "
                + $"{v.AddedInflow(v.PeakRate, 0, held):F4} v_h at η {v.PeakRate}");
            Assert.That(held, Is.EqualTo(0.9095).Within(0.001), "momentum theory at the held branch's η 0.19");
            Assert.That(v.RingInflow(v.ExitRate), Is.EqualTo(-0.1 + System.Math.Sqrt(1.01)).Within(1e-12), "momentum theory at VzD");
            Assert.That(v.RingInflow(v.EntryRate), Is.EqualTo(0.85).Within(1e-12));
            Assert.That(v.RingInflow(v.PeakRate), Is.EqualTo(1.25).Within(1e-12));
            Assert.That(v.AddedInflow(v.EntryRate, 0, held), Is.EqualTo(0), "no mean loss yet at the entry");
            Assert.That(v.AddedInflow(v.PeakRate, 0, held), Is.EqualTo(1.25 - held).Within(1e-12));
            Assert.That(v.AddedInflow(v.EndRate, 0, held), Is.EqualTo(0));
            Assert.That(v.AddedInflow(v.PeakRate, v.EscapeInPlane, held), Is.EqualTo(0), "swept away");
            Assert.That(v.Sigma(v.PeakRate, 0), Is.EqualTo(v.FluctuationSigma).Within(1e-12));
            Assert.That(v.Sigma(v.EntryRate, 0), Is.EqualTo(0.1 * 0.25 / 1.3).Within(1e-12), "1.9 % at the entry (the V-22's 2.5 % indication)");
            Assert.That(v.InRing(false, 0.44, 0), Is.False);
            Assert.That(v.InRing(false, 0.45, 0), Is.True);
            Assert.That(v.InRing(true, 0.21, 0), Is.True, "held below the entry rate");
            Assert.That(v.InRing(true, 0.19, 0), Is.False);
            Assert.That(v.InRing(true, 2.01, 0), Is.False, "the windmill brake state");
            Assert.That(v.InRing(false, 1.0, 0.95), Is.False);
            Assert.That(v.InRing(true, 1.0, 0.95), Is.False);
            double sum = 0, sq = 0;
            const int n = 40000;
            for (int k = 0; k < n; k++)
            {
                double x = VortexRingSpec.Normal(new SeedStream("vrs.thrust", 42), k / 4, k % 4);
                Assert.That(System.Math.Abs(x), Is.LessThanOrEqualTo(2 * 1.7320508075688772));
                sum += x;
                sq += x * x;
            }
            Assert.That(sum / n, Is.EqualTo(0).Within(0.02));
            Assert.That(sq / n, Is.EqualTo(1).Within(0.03), "unit variance");
        }

        // The known-wrong model: the onset from the descent speed alone (scaled by the weight's hover v_h, whatever the thrust).
        [Test]
        public void DescentSpeedAloneFailsIt()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            VortexRingScenario.Result wrong = VortexRingScenario.Fly(VortexRingScenario.Setup(spec, true, true), true);
            VortexRingScenario.Result off = VortexRingScenario.Fly(VortexRingScenario.Setup(spec, false), true);
            TestContext.Out.WriteLine("known-wrong, low: " + VortexRingScenario.Describe(wrong));
            Assert.That(VortexRingScenario.HeavyOnLowThrottle(wrong, off, spec.BuildVortexRing()), Is.False, "the descent speed alone cannot tell low throttle from high");
            Assert.That(wrong.EntryDescent, Is.GreaterThan(3.5), "it enters only once the drone falls past the hover's v_h × 0.45");
        }

        // A recorded descent through the ring replays bit for bit (FlightReplay, the headless replay), its entries and exits
        // among the events.
        [Test]
        public void ARecordedDescentReplaysBitForBit()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = VortexRingScenario.Setup(spec);
            var core = new FlightCore(setup);
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(spec, new Loadout { Payload = 2.0, Legs = true });
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
            VortexRingScenario.Result r = VortexRingScenario.Fly(core, setup, true, out _, VortexRingScenario.LowThrottle,
                (c, stick) => recorder.RecordStep(c.State, stick, c.Trace, c.Events));
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            log.Position = 0;
            FlightSummary summary = FlightSummary.Read(log);
            Assert.That(summary.Count(EventKind.VortexRingEntered), Is.EqualTo(r.Entries), "every entry logged");
            Assert.That(summary.Count(EventKind.VortexRingLeft), Is.EqualTo(r.Exits), "every exit logged");
            log.Position = 0;
            ReplayResult replay = FlightReplay.Run(log, id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash);
            TestContext.Out.WriteLine(replay.Message + ": " + replay.Steps + " steps, " + replay.Events + " events, " + r.Entries + " entries and "
                + r.Exits + " exits of the ring");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }
    }
}
