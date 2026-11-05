using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The line's nodes' wind on the field's batch worker (WindBatch, p7d-tether 1.13): a flight with it and one with every
    // node evaluated on the flight thread give the same records and events, bit for bit, and the worker does take points.
    public sealed class WindBatchTests
    {
        // The test range's houses' start (D-077), legs on, the map's own Severe wind and the line: 4 s off the skids and up.
        [Test]
        public void TheLinesWindOnTheWorkerIsTheSameBitsAsOnTheFlightThread()
        {
            ulong worker = Fly(true, out long byWorker, out long byCaller, out int nodes);
            ulong serial = Fly(false, out _, out _, out _);
            TestContext.Progress.WriteLine($"0x{worker:X16} with the worker ({byWorker} points on it, {byCaller} on the flight thread, "
                + $"{nodes} nodes at the end), 0x{serial:X16} without");
            Assert.That(worker, Is.EqualTo(serial));
            Assert.That(byWorker + byCaller, Is.GreaterThan(4000 * 3), "the nodes' wind went through the batch");
        }

        static ulong Fly(bool batch, out long byWorker, out long byCaller, out int nodes)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            StartPoint start = RangeScenarios.Start("track");
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, hash);
            double stand = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, hash, true, start.Position + new Double3(0, stand, 0));
            setup.World = new FlightWorld(RangeScenarios.World);
            setup.Wind = WindSetup.ForMap(RangeScenarios.World, WindLevel.Severe);
            setup.Asymmetry = spec.BuildAsymmetry();
            setup.Tether = TetherScenarios.Table();
            setup.TetherCoilPart = spec.Coil.Part;
            setup.TetherSpoolMass = spec.Coil.SpoolMass;
            var core = new FlightCore(setup);
            WindBatch kept = core.Tether.WindBatch;
            Assert.That(kept, Is.Not.Null, "a field with a profile gives the line its batch");
            if (!batch) core.Tether.WindBatch = null;
            var pilot = new SimPilot(core.Controller, start.Position + new Double3(0, stand, 0));
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong h = 0xCBF29CE484222325UL;
            for (int k = 0; k < 4000; k++)
            {
                StickSample stick;
                if (k < 16) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8 };
                else
                {
                    pilot.Target = start.Position + new Double3(0, stand + Math.Min(3, k * 0.001), 0);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                core.Advance(stick);
                LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                h = GoldenFlights.Fnv(h, record, 0, record.Length);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, core.State.Step, core.Events[i]);
                    h = GoldenFlights.Fnv(h, e, 0, e.Length);
                }
            }
            byWorker = kept.ByWorker;
            byCaller = kept.ByCaller;
            nodes = core.Tether.Chain.Count;
            core.Wind.Dispose();
            return h;
        }
    }
}
