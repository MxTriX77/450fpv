using System;
using System.Linq;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7f-damage 1.6b (design 17, D-083): the motor's heat and dirt (Contact.MotorHealth), prop-motor-damage's "Each motor has a health
    /// from heat, dirt and wear" scenarios on HeatScenarios' rigs and the pilot's case.
    /// </summary>
    public class HeatTests
    {
        // "A stalled motor heats and fails": rr jammed at full throttle. Under a 40 A ESC limit (the compared variant, a current source)
        // the windings follow the two-node model's closed form within 1 % of their rise up to the short; under the class's ESC (no
        // limit, about 200 A stalled) its magnets pass their maximum, a coil shorts at the insulation's rating and the motor fails at
        // its breakdown, at printed times; Kt falls with the magnets' heat as 1 + α_B·θ_S. The known-wrong, no heat, never fails.
        [Test]
        public void AStalledMotorHeatsAndFails()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MotorInternals m = spec.Internals[HeatScenarios.Corner];
            HeatScenarios.Stall limited = HeatScenarios.Jammed(spec, 40, seconds: 300);
            HeatScenarios.Stall free = HeatScenarios.Jammed(spec, double.PositiveInfinity, seconds: 5);
            HeatScenarios.Stall wrong = HeatScenarios.Jammed(spec, double.PositiveInfinity, heat: false, seconds: 5);
            foreach (HeatScenarios.Stall s in new[] { limited, free, wrong }) TestContext.Out.WriteLine(s.Describe());
            Assert.That(limited.Amps, Is.EqualTo(40).Within(1e-9), "the limited stall is a current source");
            Assert.That(limited.ClosedFormError, Is.LessThan(0.01), "the windings follow the closed form within 1 % of their rise");
            foreach (HeatScenarios.Stall s in new[] { limited, free })
            {
                Assert.That(s.ShortAt, Is.Not.NaN, "a coil shorts");
                Assert.That(s.FailAt, Is.GreaterThan(s.ShortAt), "then the motor fails");
                Assert.That(s.PeakWinding, Is.GreaterThanOrEqualTo(m.InsulationBreakdown), "at its breakdown");
                Assert.That(s.KtBeforeShort, Is.EqualTo(s.KtExpected).Within(1e-12), "Kt follows the magnets' reversible coefficient");
                Assert.That(s.KtBeforeShort, Is.LessThan(1), "Kt falls with heat");
            }
            Assert.That(free.FailAt, Is.LessThan(limited.FailAt), "the unlimited stall's larger current fails it sooner");
            Assert.That(wrong.FailAt, Is.NaN, "the known-wrong (no heat) never fails");
            Assert.That(wrong.Events, Is.Zero);
            // The class's published peak (Hobbywing XRotor 3115 900 KV: 81 A for 9 s, its 220 °C wire), from a bench's 25 °C at the drone's
            // full-throttle speed, through the model's laws: drone.json's winding conductance is derived from it, so it reaches the
            // rating; and, information, the drone's own full throttle (47.7 A) held from the air's 15 °C for 30, 60 and 120 s.
            var rig = new HeatScenarios.Rig(HeatScenarios.Setup(spec), default, false);
            MotorHealth h = rig.Health;
            double alpha = rig.Core.Contacts.Spec.CopperTemperatureCoefficient, w = 1500, loss = rig.Core.Propulsion.NoLoadTorque(0) * w;
            foreach ((double amps, double s, double ta) in new[] { (81.0, 9.0, 25.0), (47.7, 30.0, 15.0), (47.7, 60.0, 15.0), (47.7, 120.0, 15.0) })
            {
                HeatScenarios.Burst(h, alpha, m.WindingResistance, amps, w, loss, ta, s, out double tw, out double tsr);
                TestContext.Out.WriteLine($"{amps:F1} A for {s:F0} s turning at {w:F0} rad/s from {ta:F0} °C: the windings {tw:F1} °C, the rest {tsr:F1} °C "
                    + $"(the rating {m.InsulationRating:F0} °C)");
                if (amps > 80) Assert.That(tw, Is.EqualTo(m.InsulationRating).Within(2), "the published peak reaches the rating, no more");
            }
        }

        // "A spinning motor cools by its own air": the same current through rr turning at hover and stalled (its ESC limited to that
        // current), 60 s of each flown and printed: turning, its own air (the bell's spin over its side and top, the end turns in the
        // bell's air) takes the heat away, so it settles far cooler, though turning adds its no-load loss (14 W at hover: in the
        // first minute the turning motor runs warmer, the stalled one's rest taking minutes to heat; printed).
        [Test]
        public void ASpinningMotorCoolsByItsOwnAir()
        {
            HeatScenarios.Cooling c = HeatScenarios.Cools(RepositoryDrone.Spec());
            TestContext.Out.WriteLine(c.Describe());
            Assert.That(c.StallAmps, Is.EqualTo(c.Amps).Within(1e-9), "the same current stalled");
            Assert.That(c.SteadySpinWinding, Is.LessThan(c.SteadyStallWinding), "settles cooler turning");
            Assert.That(c.SteadySpinStator, Is.LessThan(c.SteadyStallStator), "its stator and bell too");
            Assert.That(c.SpinAir, Is.GreaterThan(2 * c.StallAir), "the spin's air is most of its cooling");
        }

        // "Dirt in the gap drags, then binds": rr's turning bell pressed 0.05 mm into the dirt track (10 N) at the hover's duty: the dirt
        // in its gap and its drag rise, and past the gap's capacity it binds by the bind law (Propulsion.Bound, MotorBound), then its
        // stalled current heats it (printed). At a low duty, 0.15, the dirt's drag slows the rotor so it scrapes in little more
        // (printed). The known-wrong, no dirt, never binds.
        [Test]
        public void DirtInTheGapDragsThenBinds()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double hover = StrikeScenarios.HoverDuty(spec);
            HeatScenarios.Fouled f = HeatScenarios.Scraped(spec, hover, seconds: 5), wrong = HeatScenarios.Scraped(spec, hover, heat: false, seconds: 5);
            HeatScenarios.Fouled slow = HeatScenarios.Scraped(spec, 0.15);
            TestContext.Out.WriteLine(f.Describe());
            TestContext.Out.WriteLine(slow.Describe());
            TestContext.Out.WriteLine(wrong.Describe());
            Assert.That(f.Normal, Is.GreaterThan(0), "the bell is loaded on the soil");
            for (int i = 1; i < f.Times.Length; i++)
            {
                Assert.That(f.Mass[i], Is.GreaterThanOrEqualTo(f.Mass[i - 1]), "the dirt only grows");
                Assert.That(f.Drag[i], Is.GreaterThanOrEqualTo(f.Drag[i - 1]), "and its drag");
            }
            Assert.That(f.Mass[0], Is.GreaterThan(0), "dirt enters at once");
            Assert.That(f.PackedAt, Is.Not.NaN, "the gap packs");
            Assert.That(f.BoundAt, Is.GreaterThanOrEqualTo(f.PackedAt), "and then the motor binds");
            Assert.That(wrong.Bound, Is.Zero, "without dirt it never binds");
        }

        // The pilot's case (D-083): upside down on the dirt track with its props in the soil, the throttle held at three levels for
        // 30 s. Each motor's failure time is printed with its load: the motors fail at different times set by their own loads, a
        // higher throttle fails them sooner (the first failure no later), and the known-wrong (a fixed timer: every motor gone a set
        // time after the throttle) fails them together.
        [Test]
        public void UpsideDownOnTheGroundWithThrottleTheMotorsFailOneAfterAnother()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double[] throttles = { 0.3, 0.45, 0.6 };
            var runs = throttles.Select(t => HeatScenarios.Upside(spec, t)).ToArray();
            foreach (HeatScenarios.Upturned u in runs) TestContext.Out.WriteLine(u.Describe());
            // Since model 73 without airmode (D-086), rolled onto the left side (HeatScenarios.TipDegrees). Printed beside, not asserted:
            // the same start with airmode on (model 70's controller), and model 70's own start (pitched), with airmode (model 70's case,
            // its times to the ms) and without it (flat on its payload, the props clear of the soil: none fails).
            foreach (double t in throttles) TestContext.Out.WriteLine("  airmode on: " + HeatScenarios.Upside(spec, t, airmode: true).Describe());
            foreach (bool air in new[] { true, false })
                foreach (double t in throttles)
                    TestContext.Out.WriteLine("  model 70's start: " + HeatScenarios.Upside(spec, t, airmode: air, pitchTip: true).Describe());
            double First(HeatScenarios.Upturned u) => u.FailAt.Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.PositiveInfinity).Min();
            for (int i = 1; i < runs.Length; i++)
                Assert.That(First(runs[i]), Is.LessThanOrEqualTo(First(runs[i - 1])), "a higher throttle fails them sooner");
            HeatScenarios.Upturned top = runs[runs.Length - 1];
            double[] failed = top.FailAt.Where(x => !double.IsNaN(x)).ToArray();
            Assert.That(failed.Length, Is.GreaterThanOrEqualTo(2), "at the highest throttle the motors fail");
            Assert.That(failed.Max() - failed.Min(), Is.GreaterThan(0.1), "one after another, by their own loads");
            foreach (HeatScenarios.Upturned u in runs) Assert.That(u.Tunnels, Is.Zero);
            // The known-wrong: a fixed timer, every motor gone the same set time after the throttle (here the physics' median).
            double timer = failed.OrderBy(x => x).ElementAt(failed.Length / 2);
            double[] wrong = { timer, timer, timer, timer };
            TestContext.Out.WriteLine($"the known-wrong fixed timer fails all four at {timer:F3} s at every throttle");
            Assert.That(wrong.Max() - wrong.Min(), Is.Not.GreaterThan(0.1), "the known-wrong fails them together");
        }

        // The pilot's case at its top throttle recorded and replayed headless: heating, shorting, failing and fouling motors, every step
        // bit for bit; and its steps through them allocate nothing.
        [Test]
        public void TheUpturnedMotorsReplayBitForBitAndAllocateNothing()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = HeatScenarios.UpsideSetup(spec);
            var core = new FlightCore(setup);
            var stream = new System.IO.MemoryStream();
            LogHeader header = LogHeader.For(setup, core);
            header.WorldId = "track";
            header.SetDrone(spec, spec.Nominal);
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            long steps = 0, allocated = 0;
            int health = 0;
            for (int k = 0; k < 9000; k++)
            {
                var stick = new Control.StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8, Throttle = k < 2000 ? 0 : 0.6 };
                long before = AllocProbe.Before();
                core.Advance(stick);
                if (k >= 2100) allocated += AllocProbe.After(before);
                for (int i = 0; i < core.Events.Count; i++)
                    if (core.Events[i].Kind == EventKind.MotorDamaged && core.Events[i].Index >> 4 >= 5) health++;
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                steps++;
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            TestContext.Out.WriteLine($"  {health} health events over {steps} steps; {allocated} B allocated in the 6.9 s of throttle");
            Assert.That(health, Is.GreaterThan(3), "the motors short and fail");
            Assert.That(allocated, Is.Zero, AllocProbe.Report());
            ContactScenarios.RepositoryMaterials(out ulong materials);
            ReplayResult replay = FlightReplay.Run(new System.IO.MemoryStream(stream.ToArray()), id => SoilScenarios.Floor("dirt_track"), spec.ContentHash, null,
                SoilScenarios.Table().Hash, materialsHash: materials);
            TestContext.Out.WriteLine($"  replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(steps));
        }
    }
}
