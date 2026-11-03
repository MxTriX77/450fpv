using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The build's placement (BuildTolerance, D-066, PHY-24): the bottle, the packs, the coil and the leads where the pilot's
    // hands set them each flight, drawn from "build.placement", bounded and logged, never on the pose, so the centre of
    // mass sits a few millimetres off the exact build's; and what that does on the coil (D-067, the orchestrator's landing
    // analysis: the exact build, centred side to side, tips only from the touchdown's own tilt).
    public sealed class PlacementTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        // The centre of mass across (x) and along (z) the exact build's, m, for one seed's draws at a loadout.
        static Double3 Offset(BuildTolerance t, ulong seed, Loadout loadout, out IReadOnlyList<ParameterDraw> draws)
        {
            var random = new FlightRandom(seed);
            MassModel exact = Spec.BuildMass(loadout), moved = exact.Clone();
            t.Apply(moved, t.DrawFor(random));
            draws = random.Draws;
            Assert.That(moved.Properties.Mass, Is.EqualTo(exact.Properties.Mass).Within(1e-12), "placement moves, never adds");
            return moved.Properties.CenterOfMass - exact.Properties.CenterOfMass;
        }

        // The largest offset across the bounds allow, m: Σ m·δ over the mass (each bound at its end, the packs' out-moves on
        // opposite sides, the leads at their share).
        static double MostAcross(BuildTolerance t, MassModel m) =>
            (m.Part(t.BottlePart).Mass * t.BottleAcross + m.Part(t.CoilPart).Mass * t.CoilAcross
             + m.Part(t.PackParts[0]).Mass * t.PackOut + t.WiringMass * t.WiringAcross) / m.Properties.Mass;

        [Test]
        public void ThePlacementIsDrawnBoundedAndLogged()
        {
            BuildTolerance t = Spec.BuildPlacement();
            foreach (var (name, payload) in new[] { ("no bottle", 0.0), ("the nominal 1 kg", 1.0), ("2 kg", 2.0) })
            {
                var loadout = new Loadout { Payload = payload, Legs = true };
                MassModel m = Spec.BuildMass(loadout);
                Assert.That(m.Properties.CenterOfMass.X, Is.EqualTo(0).Within(1e-12), "the exact build is centred side to side");
                double most = MostAcross(t, m), sum = 0, largest = 0;
                const int seeds = 2000;
                for (ulong seed = 1; seed <= seeds; seed++)
                {
                    Double3 o = Offset(t, seed, loadout, out IReadOnlyList<ParameterDraw> draws);
                    Assert.That(Math.Abs(o.X), Is.LessThanOrEqualTo(most + 1e-12), "inside the bounds");
                    sum += o.X * o.X;
                    largest = Math.Max(largest, Math.Abs(o.X));
                    if (seed > 1) continue;
                    Assert.That(draws.Count, Is.EqualTo(2 + 2 * t.PackParts.Length + 3), "the bottle's two, each pack's two, the coil's two, the leads'");
                    foreach (ParameterDraw d in draws)
                    {
                        Assert.That(d.Stream, Is.EqualTo("build.placement"));
                        Assert.That(d.Parameter, Does.StartWith("build."));
                        Assert.That(d.Value, Is.InRange(d.Lo, d.Hi), d.Parameter);
                    }
                }
                TestContext.Out.WriteLine($"{name}, {m.Properties.Mass:F3} kg: the centre of mass across the exact build's, over {seeds} seeds: "
                    + $"RMS {1000 * Math.Sqrt(sum / seeds):F2} mm, largest {1000 * largest:F2} mm, the bounds' {1000 * most:F2} mm (derived)");
                Assert.That(largest, Is.GreaterThan(0.5 * most), "the draws reach towards the bounds");
            }
        }

        // A flight with the placement on draws it at its start (before every other stream), carries it in the header and
        // replays bit for bit; the pose is never touched (the flight starts where the setup says, level).
        [Test]
        public void AFlightCarriesItsPlacementAndReplaysBitForBit()
        {
            DroneSpec spec = Spec;
            Loadout loadout = spec.Nominal;
            var setup = new FlightSetup
            {
                Seed = 7, World = FlightWorld.Floor(), Mass = spec.BuildMass(loadout), Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(),
                Placement = spec.BuildPlacement(), StartPosition = new Double3(0, 50, 0), StartAttitude = Quat.Identity, StartPackCharge = 0.9,
            };
            var core = new FlightCore(setup);
            Assert.That(core.Random.Opened[0], Is.EqualTo("build.placement"));
            Assert.That(core.State.Position.Y, Is.EqualTo(50), "the pose is the setup's");
            Assert.That(core.Body.CenterOfMass.X, Is.Not.EqualTo(0), "the flight's own centre of mass");
            LogHeader h = LogHeader.For(setup, core);
            h.Build = "simtests";
            h.Isa = "test";
            h.WorldId = "floor";
            h.SetDrone(spec, loadout);
            var log = new MemoryStream();
            var recorder = new FlightRecorder(log, h, FlightRecorder.DefaultCapacity, ownsStream: false);
            for (int k = 0; k < 3000; k++)
            {
                var stick = new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8, Throttle = k < 16 ? 0 : 0.5 };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            log.Position = 0;
            LogHeader read = LogHeader.Read(new MemoryStream(log.ToArray()));
            Assert.That(read.Placement, Is.Not.Null);
            Assert.That(read.Placement.BottleAcross, Is.EqualTo(setup.Placement.BottleAcross));
            Assert.That(read.Placement.PackParts, Is.EqualTo(setup.Placement.PackParts));
            ReplayResult replay = FlightReplay.Run(log, id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash);
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
        }

        // On the coil, legs off, disarmed on the floor (D-067): the time from release until a pack meets the floor, s, or NaN
        // within the window. Started level, or at a roll.
        static double TipTime(FlightSetup setup, double seconds, out double rollDeg)
        {
            var core = new FlightCore(setup);
            rollDeg = 0;
            for (int k = 0; k < seconds * 1000; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                Double3 right = core.State.Attitude.Rotate(new Double3(1, 0, 0));
                rollDeg = Math.Asin(Math.Max(-1, Math.Min(1, right.Y))) * 57.29577951308232;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.ContactMade && Spec.Parts[e.Part].Name.StartsWith("pack_")) return (k + 1) * FlightCore.Dt;
                }
            }
            return double.NaN;
        }

        static FlightSetup OnTheCoil(ulong seed, bool placement, double startRollDeg)
        {
            DroneSpec spec = Spec;
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong hash);
            Loadout loadout = spec.Nominal;
            loadout.Legs = false;
            ContactSpec contacts = spec.BuildContacts(loadout, m, hash);
            double low = 0;
            foreach (ContactShape s in contacts.Shapes)
                if (s.Role == ContactRole.Body) low = Math.Min(low, Math.Min(s.A.Y, s.B.Y) - s.Radius);
            double half = startRollDeg / 2 / 57.29577951308232;
            return new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(loadout), Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(),
                Contacts = contacts, StartPosition = new Double3(0, -low, 0), StartPackCharge = 0.9,
                StartAttitude = new Quat(0, 0, Math.Sin(half), Math.Cos(half)), Placement = placement ? spec.BuildPlacement() : null,
            };
        }

        [Test]
        public void OffCentreTheCoilTipsAtOnce()
        {
            double exactLevel = TipTime(OnTheCoil(1, false, 0), 3, out double levelRoll);
            double exactTilted = TipTime(OnTheCoil(1, false, 0.1), 3, out _);
            TestContext.Out.WriteLine($"the exact build on its coil, legs off, disarmed: level {(double.IsNaN(exactLevel) ? "never in 3 s" : exactLevel.ToString("F3") + " s")} "
                + $"(rolled {levelRoll:F3}°), from 0.1° of roll {exactTilted:F3} s to a pack on the floor");
            var times = new List<double>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                FlightSetup s = OnTheCoil(seed, true, 0);
                double t = TipTime(s, 3, out _);
                Double3 o = new FlightCore(s).Body.CenterOfMass - Spec.BuildMass(new Loadout { Payload = Spec.Nominal.Payload, Legs = false, CoilPaidOut = Spec.Nominal.CoilPaidOut }).Properties.CenterOfMass;
                TestContext.Out.WriteLine($"seed {seed}: the centre of mass {1000 * o.X:+0.00;-0.00} mm across, a pack on the floor at {t:F3} s");
                times.Add(t);
            }
            times.Sort();
            TestContext.Out.WriteLine($"placed: tipped on {times.FindAll(x => !double.IsNaN(x)).Count} of 20 within 3 s, {times[0]:F3}–{times[times.Count - 1]:F3} s, median {(times[9] + times[10]) / 2:F3} s");
            Assert.That(double.IsNaN(exactLevel), Is.True, "the exact build, centred, balances on its coil from level");
            Assert.That(times.TrueForAll(x => !double.IsNaN(x)), Is.True, "every placed build tips off its coil with nothing to start it");
            Assert.That((times[9] + times[10]) / 2, Is.LessThan(exactTilted), "most tip sooner than the exact build from 0.1° (a few tenths of a millimetre is less than that tilt)");
        }
    }
}
