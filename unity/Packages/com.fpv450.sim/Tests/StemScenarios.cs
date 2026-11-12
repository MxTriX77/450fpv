using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The props in standing stems (w9-village-physics 2.2, design.md "## 2.2"): the spec's straw landing into Village's densest tall
    /// cell, the pilot's approach as a creep through it, a hover in it, each flown by SimPilot with surfaces.json on the farmland,
    /// optionally logged. Shared by StemTests and flightbench.
    /// </summary>
    public static class StemScenarios
    {
        /// <summary>The densest tall cell (2.1's golden cell; audit §1.12), and the hubs' height over the body origin, m.</summary>
        public static readonly XZ Dense = new XZ(768.07, 917.48);
        public const double HubOverOrigin = 0.035;

        /// <summary>What a stem flight did, per rotor (Corners' order) and in all.</summary>
        public sealed class Run
        {
            /// <summary>Per rotor: the stems cut (fresh, slices as cut equivalents, entries) and their work, J.</summary>
            public readonly double[] Fresh = new double[4], Sliced = new double[4], Entered = new double[4], Work = new double[4];

            /// <summary>The same up to the touch (the landing).</summary>
            public readonly double[] FreshBefore = new double[4], SlicedBefore = new double[4], EnteredBefore = new double[4], WorkBefore = new double[4];

            /// <summary>StemEpisode starts and ends, whole stems counted in BladeStrikes, and the last step with any cut.</summary>
            public int Starts, Ends, Whole;
            public long LastCut = -1, Touch = -1, Steps;

            /// <summary>Bytes allocated after the first second, events refused, and the lowest disc height reached, m.</summary>
            public long Allocated, Refused;
            public double Lowest = double.PositiveInfinity;
        }

        static MapWorld Village => SlopeScenarios.Farmland();

        /// <summary>The ground's height at a point of Village, m.</summary>
        public static double Ground(XZ at)
        {
            var g = new GroundSample[1];
            Village.SampleGround(new[] { at }, g);
            return g[0].Height;
        }

        /// <summary>A flight from at (the body origin) on Village with surfaces.json, armed in its first 16 steps; the known-wrong
        /// lays no stem over and cuts every stem met (the audit's law); log records it when given; stems false flies without them.</summary>
        public static FlightCore Start(Double3 at, bool knownWrong, Stream log, out FlightRecorder recorder, bool stems = true)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var world = new FlightWorld(Village);
            FlightSetup setup = SoilScenarios.Setup(spec, world, at);
            setup.Contacts.Stems = stems;
            var core = new FlightCore(setup);
            if (knownWrong)
            {
                core.Strikes.StemLean = false;
                core.Strikes.StemCritical = false;
            }
            recorder = null;
            if (log != null)
            {
                LogHeader header = LogHeader.For(setup, core);
                header.WorldId = "farmland";
                header.SetDrone(spec, spec.Nominal);
                recorder = new FlightRecorder(log, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            }
            return core;
        }

        /// <summary>A step flown, recorded and counted: the stems' records, the episodes, the bytes after the first second.</summary>
        public static void Step(FlightCore core, StickSample stick, Run r, FlightRecorder recorder, bool beforeTouch, Action<FlightCore> stepped)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            core.Advance(stick);
            long k = core.State.Step;
            if (k > 1000) r.Allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            recorder?.RecordStep(core.State, stick, core.Trace, core.Events);
            r.Refused += core.Events.Lost;
            bool any = false;
            for (int c = 0; c < 4; c++)
            {
                PropStrikes.StemRecord s = core.Strikes.StemLast[c];
                r.Fresh[c] += s.Fresh;
                r.Sliced[c] += s.Sliced;
                r.Entered[c] += s.Entered;
                r.Work[c] += s.Work;
                if (beforeTouch)
                {
                    r.FreshBefore[c] += s.Fresh;
                    r.SlicedBefore[c] += s.Sliced;
                    r.EnteredBefore[c] += s.Entered;
                    r.WorkBefore[c] += s.Work;
                }
                if (s.Height > 0) r.Lowest = Math.Min(r.Lowest, s.Height);
                any |= s.Cuts > 0;
            }
            if (any) r.LastCut = k;
            for (int i = 0; i < BladeStrikes.Slots; i++)
                if (core.State.BladeStrikes[i].Kind == StrikeKind.Stem) r.Whole += core.State.BladeStrikes[i].Count;
            for (int i = 0; i < core.Events.Count; i++)
            {
                FlightEvent e = core.Events[i];
                if (e.Kind != EventKind.StemEpisode) continue;
                if ((e.Index & 0x80) == 0) r.Starts++;
                else r.Ends++;
            }
            r.Steps = k;
            stepped?.Invoke(core);
        }

        /// <summary>
        /// The spec's straw landing: from a hover with the hubs 1.9 m over the densest tall cell (over its tallest tops, 1.77 m), the
        /// target comes down at descent m/s (and forward at forward m/s, heading +z), the sticks held when the skids are 0.1 m up, the
        /// throttle cut at the first touch, flown 2 s past it (SoilScenarios.Land's pilot).
        /// </summary>
        public static Run Landing(double descent = 0.5, double forward = 0, bool knownWrong = false, Stream log = null, Action<FlightCore> stepped = null,
            bool stems = true)
        {
            double stand = SoilScenarios.Standing(RepositoryDrone.Spec()), ground = Ground(new XZ(Dense.X, Dense.Z - forward * 2.6));
            var foot = new Double3(Dense.X, ground, Dense.Z - forward * 2.6);
            double y0 = ground + 1.9 - HubOverOrigin;
            FlightCore core = Start(new Double3(foot.X, y0, foot.Z), knownWrong, log, out FlightRecorder recorder, stems);
            var r = new Run();
            var pilot = new SimPilot(core.Controller, new Double3(foot.X, y0, foot.Z));
            for (int k = 0; k < 16; k++)
                Step(core, new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }, r, recorder, true, stepped);
            for (int k = 0; k < 4000; k++) Step(core, pilot.Sticks(core, core.State.Step * FlightCore.StepUs), r, recorder, true, stepped);
            StickSample held = default;
            bool holding = false;
            for (int k = 0; k < 20000; k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick = held;
                if (!holding)
                {
                    double t = k * FlightCore.Dt;
                    pilot.Target = new Double3(foot.X, y0 - descent * t, foot.Z + forward * t);
                    if (forward > 0)
                    {
                        pilot.HoldVelocity = true;
                        pilot.TargetVelocity = new Double3(0, -descent, forward);
                    }
                    stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                    if (s.Position.Y - Ground(new XZ(s.Position.X, s.Position.Z)) - stand < 0.1 && s.Velocity.Y < 0)
                    {
                        holding = true;
                        held = stick;
                        held.Roll = held.Pitch = held.Yaw = 0;
                        stick = held;
                    }
                }
                if (r.Touch >= 0) stick.Throttle = 0;
                stick.HostUs = s.Step * FlightCore.StepUs;
                Step(core, stick, r, recorder, r.Touch < 0, stepped);
                for (int i = 0; i < core.Events.Count && r.Touch < 0; i++)
                    if (core.Events[i].Kind == EventKind.ContactMade && core.Events[i].Object == WorldIds.Ground) r.Touch = core.State.Step;
                if (r.Touch >= 0 && core.State.Step - r.Touch >= 2000) break;
            }
            recorder?.Close();
            return r;
        }

        /// <summary>
        /// The pilot's approach as a creep: 4 s of hover with the hubs height m over the ground 1.3 m south of the densest tall cell's
        /// centre, then forward (+z, the nose) at speed m/s for seconds, the hubs' height over the ground held throughout.
        /// </summary>
        public static Run Creep(double height = 0.6, double speed = 0.3, double seconds = 6, Stream log = null, Action<FlightCore> stepped = null, bool stems = true)
        {
            var start = new XZ(Dense.X, Dense.Z - 1.3);
            double y = Ground(start) + height - HubOverOrigin;
            FlightCore core = Start(new Double3(start.X, y, start.Z), false, log, out FlightRecorder recorder, stems);
            var r = new Run();
            var pilot = new SimPilot(core.Controller, new Double3(start.X, y, start.Z));
            for (int k = 0; k < 16; k++)
                Step(core, new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }, r, recorder, true, stepped);
            int steps = (int)(seconds / FlightCore.Dt);
            for (int k = 0; k < 4000 + steps; k++)
            {
                if (k == 4000)
                {
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = new Double3(0, 0, speed);
                }
                // The height held over the ground under the hubs: the pilot's target trimmed by the error (an integral, 2 /s).
                ref readonly FlightState s = ref core.State;
                double over = s.Position.Y + HubOverOrigin - Ground(new XZ(s.Position.X, s.Position.Z));
                pilot.Target = new Double3(start.X, pilot.Target.Y + 2 * (height - over) * FlightCore.Dt, start.Z);
                Step(core, pilot.Sticks(core, s.Step * FlightCore.StepUs), r, recorder, true, stepped);
            }
            recorder?.Close();
            return r;
        }

        /// <summary>A hover with the hubs height m over the densest tall cell for seconds.</summary>
        public static Run Hover(double height = 0.5, double seconds = 10, Stream log = null, Action<FlightCore> stepped = null)
        {
            double y = Ground(Dense) + height - HubOverOrigin;
            var at = new Double3(Dense.X, y, Dense.Z);
            FlightCore core = Start(at, false, log, out FlightRecorder recorder);
            var r = new Run();
            var pilot = new SimPilot(core.Controller, at);
            for (int k = 0; k < 16; k++)
                Step(core, new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }, r, recorder, true, stepped);
            int steps = (int)(seconds / FlightCore.Dt);
            for (int k = 0; k < steps; k++) Step(core, pilot.Sticks(core, core.State.Step * FlightCore.StepUs), r, recorder, true, stepped);
            recorder?.Close();
            return r;
        }

        /// <summary>
        /// The range's straw (S04, the range golden's landing spot, RangeScenarios.Straw): 3 s of hover with the hubs at height m
        /// (world y, the straw's ground at 0) 1 m south of it, then forward (+z) at speed m/s for seconds through its stalks.
        /// </summary>
        public static Run RangeCreep(double height = 0.4, double speed = 0.3, double seconds = 4, Action<FlightCore> stepped = null)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Double3 foot = RangeScenarios.Straw;
            var at = new Double3(foot.X, height - HubOverOrigin, foot.Z - 1.0);
            var core = new FlightCore(SoilScenarios.Setup(spec, new FlightWorld(RangeScenarios.World), at));
            var r = new Run();
            var pilot = new SimPilot(core.Controller, at);
            for (int k = 0; k < 16; k++)
                Step(core, new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }, r, null, true, stepped);
            int steps = (int)(seconds / FlightCore.Dt);
            for (int k = 0; k < 3000 + steps; k++)
            {
                if (k == 3000)
                {
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = new Double3(0, 0, speed);
                }
                ref readonly FlightState s = ref core.State;
                pilot.Target = new Double3(at.X, pilot.Target.Y + 2 * (height - s.Position.Y - HubOverOrigin) * FlightCore.Dt, at.Z);
                Step(core, pilot.Sticks(core, s.Step * FlightCore.StepUs), r, null, true, stepped);
            }
            return r;
        }

        /// <summary>The farmland's world for a replay of a stem flight's log.</summary>
        public static FlightWorld OpenFarmland(string id) => id == "farmland" ? new FlightWorld(Village) : null;
    }
}
