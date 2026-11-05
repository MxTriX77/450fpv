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
        // at rest, model 50's) settles on its front-left skid, that corner lower. Since model 60 over the set-downs
        // (RestScenarios.BlockDrops, the start's lift 0–5 mm): where the drone comes to rest turns on how it lands and slides,
        // the blade near its root holding the corner off the floor or the block far out on the blade, which bends and lets the
        // skid's corner down beside it (model 59's one set-down held the corner 4.8 mm up, the frame's 0.3 mm with the skid
        // down; on 59 two of the six held it over 3 mm, on 60 one, and on 59 one slid off the block altogether).
        [Test]
        public void ARestingPropBearsLoad()
        {
            RestScenarios.Result r = RestScenarios.Block(Spec), wrong = RestScenarios.Block(Spec, rest: false);
            TestContext.Out.WriteLine("at rest:          " + r.Describe());
            TestContext.Out.WriteLine("passing through:  " + wrong.Describe());
            RestScenarios.BlockSweep w = RestScenarios.Blocks(Spec, each: (drop, at, through) => TestContext.Out.WriteLine(
                $"  set down {drop * 1e3:F1} mm higher: {at.Load:F2} N, {at.Bend * 1e3:F2} mm, {(at.Bend > 0 ? at.Load / at.Bend : 0):F0} N/m, the corner "
                + $"{(at.SupportTouches ? "down" : "held")}, {(at.HubHeight - through.HubHeight) * 1e3:F2} mm over the known-wrong"));
            TestContext.Out.WriteLine($"over {w.N} set-downs: bears {w.Bears}, holds the corner off the floor {w.Holds}, over the known-wrong {w.Over} "
                + $"({w.LeastOver * 1e3:F2}–{w.MostOver * 1e3:F2} mm), the known-wrong on its skid {w.WrongOnItsSkid}");
            // The blade's stiffness at its contact is a cantilever's from the hub (the sections', derived) in series with the
            // motor's mount: under the mount's 12 kN/m along the shaft, over a few hundred N/m at the very tip.
            Assert.That(w.Bears, Is.EqualTo(w.N), "on every set-down the drone rests on the blade: over 2 N, bent at its own stiffness, undamaged");
            Assert.That(w.WrongOnItsSkid, Is.EqualTo(w.N), "the known-wrong's blade bears nothing and it rests on the front-left skid");
            // Since model 64 one set-down of six (0.5 mm higher) slides the corner down beside the bent blade, where the known-wrong
            // rests too (0.03 mm apart): wherever the blade holds the corner, the drone rests over the known-wrong.
            Assert.That(w.Over, Is.GreaterThanOrEqualTo(w.Holds), "and lower at that corner wherever the blade holds it");
            Assert.That(w.Holds, Is.GreaterThan(0), "on some the blade alone holds the corner off the floor");
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
        // the drone rests on its skid, legs and pack and on the blade; armed with the sticks centred (Betaflight's motor idle), the idle rotor strikes the car, the strikes'
        // impulses move the drone, and the blade chips by the hardness rule (steel is harder than the blade: the blade yields),
        // each strike logged. The known-wrong (the prop passed through at rest) settles with the prop in the rail. Since model 60
        // over the flights' seeds (RestScenarios.RackSeeds): which surface an idle blade meets first, the rail or the roof's sheet
        // beside it, and whether its first blows crush its edge, turn on the pose within a millimetre (model 59's seed 0x450F
        // struck the rail first and crushed 11 notches, the frame's strikes the roof first and breaks the blade; over seeds 1–20
        // the rail first on 7 on both models, a crushed edge on 9 and 13).
        [Test]
        public void ArmingWithAPropOnTheRackMovesTheDrone()
        {
            RestScenarios.Result r = RestScenarios.Rack(Spec), wrong = RestScenarios.Rack(Spec, rest: false, arm: false);
            TestContext.Out.WriteLine("on the rack:      " + r.Describe());
            TestContext.Out.WriteLine("passing through:  " + wrong.Describe());
            RestScenarios.RackSweep w = RestScenarios.Racks(Spec, each: (seed, at, through) => TestContext.Out.WriteLine(
                $"  seed {seed}: {at.Load:F2} N, {at.Strikes} strikes (first on {at.FirstStrikeOn}), {at.Damaged} damage events ({at.Notches} notches, "
                + $"{at.Broken} breaks), {at.Lost * 1e6:F1} mg lost, moved {at.Travel * 1e3:F1} mm, {(at.HubAtArming - through.HubHeight) * 1e3:F2} mm over the known-wrong; {at.Crashes} crashes, {at.Tunnels} tunnels, the known-wrong {through.Crashes}, {through.Tunnels}"));
            TestContext.Out.WriteLine($"over {w.N} seeds: rests on the blade {w.Rests}, strikes {w.Strikes}, chips {w.Chips}, notches {w.Notches}, first on the rail "
                + $"{w.OnTheRail}, moves {w.Moves}, over the known-wrong {w.Over}, the known-wrong bears nothing {w.WrongBearsNothing}, {w.Crashes} crashes (driven off the roof), {w.Tunnels} tunnels");
            // Since model 64 the canister hangs 0.06 m lower than the old coil, and rolled onto its left skid the drone rests on it
            // as well on most seeds (the canister, leg_fl, pack_left and the payload at 0x450F): the blade bears over 1 N on 8 of 20
            // (most on the old coil). Armed, the idle blade still strikes, chips and moves the drone on most.
            Assert.That(w.Rests, Is.GreaterThan(0), "disarmed, the drone rests on the blade (over 1 N) on some seeds");
            Assert.That(w.Strikes, Is.GreaterThan(w.N / 2), "armed at idle, the blade strikes on most");
            Assert.That(w.Chips, Is.GreaterThan(w.N / 2), "and chips: damaged, its material lost");
            Assert.That(w.Moves, Is.GreaterThan(w.N / 2), "the drone moves (over 1 mm)");
            Assert.That(w.Over, Is.GreaterThan(w.N / 2), "it rests over the known-wrong, the prop in the rail (over 3 mm)");
            Assert.That(w.Notches, Is.GreaterThan(0), "on some its leading edge is crushed on the steel, a share of its chord");
            Assert.That(w.OnTheRail, Is.GreaterThan(0), "on some the rail is struck first");
            Assert.That(w.WrongBearsNothing, Is.EqualTo(w.N), "the known-wrong's blade bears nothing");
            Assert.That(w.Tunnels, Is.EqualTo(0));
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
