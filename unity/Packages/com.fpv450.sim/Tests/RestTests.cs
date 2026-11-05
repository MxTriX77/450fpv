using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7f-damage 1.3b (design 16, D-082): props are parts at rest; an armed idle prop strikes what it touches. The scenarios are
    // RestScenarios'; tools/flightbench --damage prints them.
    public sealed class RestTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        // prop-motor-damage's "A resting prop bears load": set down on one prop's blade (a steel block under it), the drone
        // rests on that blade and three legs, the blade's load and deflection logged; the known-wrong (the prop passed through
        // at rest, model 50's) settles level on all four legs, its front-left corner lower, the block inside the blade.
        [Test]
        public void ARestingPropBearsLoad()
        {
            RestScenarios.Result r = RestScenarios.Block(Spec), wrong = RestScenarios.Block(Spec, rest: false);
            TestContext.Out.WriteLine("at rest:          " + r.Describe());
            TestContext.Out.WriteLine("passing through:  " + wrong.Describe());
            Assert.That(r.BladeTouches, Is.True, "the blade rests on the block");
            Assert.That(r.SupportTouches, Is.False, "the front-left skid is off the floor: the blade holds that corner");
            Assert.That(r.Load, Is.GreaterThan(2), "the blade bears part of the weight (N)");
            Assert.That(r.Bend, Is.GreaterThan(0), "and bends under it");
            // The blade's stiffness at its contact is a cantilever's from the hub (the sections', derived) in series with the
            // motor's mount: under the mount's 12 kN/m along the shaft, over a few hundred N/m at the very tip.
            Assert.That(r.Load / r.Bend, Is.InRange(500.0, 12000.0), "its own stiffness there, N/m");
            Assert.That(r.Crashes + r.Tunnels, Is.EqualTo(0));
            Assert.That(r.Damaged, Is.EqualTo(0), "the blade holds the drone's share elastically");
            Assert.That(wrong.Load, Is.EqualTo(0), "the known-wrong's blade bears nothing");
            Assert.That(wrong.SupportTouches, Is.True, "it rests on the front-left skid instead");
            Assert.That(r.HubHeight - wrong.HubHeight, Is.GreaterThan(0.003), "and lower at that corner (m)");
        }

        // The resting blades' block of the record is the trace's: the load and deflection a log carries.
        [Test]
        public void TheRecordCarriesTheRestingBladesLoad()
        {
            double load = 0, bend = 0, logged = 0, loggedBend = 0;
            var record = new byte[LogFormat.StepSize];
            RestScenarios.Block(Spec, seconds: 1, stepped: core =>
            {
                LogFormat.EncodeStep(record, core.State, default, core.Trace);
                LogFormat.DecodeStep(record, out _, out _, out StepTrace t);
                load = core.Trace.BladeLoad1[RestScenarios.FrontLeft];
                bend = core.Trace.BladeBend1[RestScenarios.FrontLeft];
                logged = t.BladeLoad1[RestScenarios.FrontLeft];
                loggedBend = t.BladeBend1[RestScenarios.FrontLeft];
            });
            Assert.That(load, Is.GreaterThan(0));
            Assert.That(logged, Is.EqualTo(load), "blade_load_1.front_left");
            Assert.That(loggedBend, Is.EqualTo(bend), "blade_bend_1.front_left");
        }

        // A resting blade's sections take its load by design 5's laws: dropped onto the block, the corner's share on the blade's
        // span passes a section's proportional limit or its strength and the blade sets or breaks there; passing through (the
        // known-wrong), nothing happens to it.
        [Test]
        public void ADroppedDroneBendsOrBreaksTheBladeItLandsOn()
        {
            RestScenarios.Result r = RestScenarios.Block(Spec, seconds: 1, drop: 0.1), wrong = RestScenarios.Block(Spec, rest: false, seconds: 1, drop: 0.1);
            TestContext.Out.WriteLine("dropped 0.1 m onto the blade: " + r.Describe());
            TestContext.Out.WriteLine("passing through:              " + wrong.Describe());
            Assert.That(r.Broken + r.Bent, Is.GreaterThan(0), "the landing's load bends or breaks the blade");
            Assert.That(r.Tunnels, Is.EqualTo(0));
            Assert.That(wrong.Damaged, Is.EqualTo(0), "passing through, the blade takes nothing");
        }

        // prop-motor-damage's "Arming with a prop on something moves the drone" (D-082, the pilot's case): set down disarmed on
        // R01_A's roof, rolled onto its left skid with the rear-left prop's blade on the rack's side rail (RestScenarios.RackPose),
        // the drone rests on its skid, legs and pack and on the blade; armed with the sticks centred (Betaflight's motor idle), the idle rotor strikes the rail, the strikes'
        // impulses move the drone, and the blade chips by the hardness rule (steel is harder than the blade: the blade yields),
        // each strike logged. The known-wrong (the prop passed through at rest) settles with the prop in the rail.
        [Test]
        public void ArmingWithAPropOnTheRackMovesTheDrone()
        {
            RestScenarios.Result r = RestScenarios.Rack(Spec), wrong = RestScenarios.Rack(Spec, rest: false, arm: false);
            TestContext.Out.WriteLine("on the rack:      " + r.Describe());
            TestContext.Out.WriteLine("passing through:  " + wrong.Describe());
            Assert.That(r.Load, Is.GreaterThan(1), "disarmed, the drone rests on the blade (N)");
            Assert.That(r.Strikes, Is.GreaterThan(0), "armed at idle, the blade strikes");
            Assert.That(r.FirstStrikeOn, Is.EqualTo("R01_A (steel)"), "the rack");
            Assert.That(r.Notches, Is.GreaterThan(0), "the blade chips: its leading edge crushed on the steel, a share of its chord");
            Assert.That(r.Lost, Is.GreaterThan(0), "it loses material (kg)");
            Assert.That(r.Travel, Is.GreaterThan(0.001), "the drone moves (m)");
            Assert.That(r.Crashes + r.Tunnels, Is.EqualTo(0));
            Assert.That(wrong.Load, Is.EqualTo(0), "the known-wrong's blade bears nothing");
            Assert.That(r.HubAtArming - wrong.HubHeight, Is.GreaterThan(0.003), "it rests lower, the prop through the rail (m)");
        }

        // The pilot's case recorded with its header and replayed headless on the test range: every step bit for bit.
        [Test]
        public void ArmingOnTheRackReplaysBitForBit()
        {
            DroneSpec spec = Spec;
            var stream = new System.IO.MemoryStream();
            FlightRecorder recorder = null;
            long steps = 0;
            RestScenarios.Result r = RestScenarios.Rack(spec, settle: 1, armed: 1, started: (setup, core) =>
            {
                LogHeader header = LogHeader.For(setup, core);
                header.WorldId = "test-range";
                header.SetDrone(spec, spec.Nominal);
                recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            }, stepped: (core, stick) =>
            {
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                steps++;
            });
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            Assert.That(r.Strikes, Is.GreaterThan(0), "the replay covers the strikes");
            ContactScenarios.RepositoryMaterials(out ulong materials);
            var world = new FlightWorld(RangeScenarios.World);
            ReplayResult replay = FlightReplay.Run(new System.IO.MemoryStream(stream.ToArray()), id => id == "test-range" ? world : null, spec.ContentHash, null,
                materialsHash: materials);
            TestContext.Out.WriteLine($"  replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(steps));
        }

        // A resting prop's step allocates nothing: the blades placed, loaded and checked, 0 B a step.
        [Test]
        public void ARestingPropAllocatesNothing()
        {
            long start = 0, end = 0;
            int k = 0;
            double load = 0;
            RestScenarios.Block(Spec, seconds: 1, stepped: core =>
            {
                k++;
                if (k == 200) start = GC.GetAllocatedBytesForCurrentThread();
                if (k == 1000) end = GC.GetAllocatedBytesForCurrentThread();
                load = core.Trace.BladeLoad1[RestScenarios.FrontLeft];
            });
            long allocated = end - start;
            TestContext.Out.WriteLine($"  {allocated} B over 800 steps resting on the blade ({load:F2} N on it at the end)");
            Assert.That(load, Is.GreaterThan(0), "resting on it");
            Assert.That(allocated, Is.Zero);
        }
    }
}
