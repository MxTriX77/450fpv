using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// w9-village-physics 2.9 (with p7a-ground 1.17): the legs' stiffness drawn per flight (leg.stiffness, design 12, D-086),
    /// the anchors' reach in the header (2.4's debt), and the liftoff harness's flight (LiftoffScenarios) recorded and replayed.
    /// </summary>
    public sealed class LiftoffTests
    {
        static DroneSpec _drone;
        static DroneSpec Drone() => _drone ??= DroneSpec.Parse(File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));

        static LegDamageSpec Legs()
        {
            ContactScenarios.RepositoryMaterials(out ulong hash);
            return Drone().BuildContacts(Drone().Nominal, ContactScenarios.RepositoryMaterials(out _), hash).Legs;
        }

        [Test]
        public void TheLegDrawsCoverTheirBounds()
        {
            // "The leg draws cover their bounds" (p7a-ground 1.17): 4000 flights' four draws each, nearly normal with σ 3 %,
            // clamped to ±10 %, one stream of its own, recorded with their bounds.
            LegDamageSpec spec = Legs();
            Assert.That(spec.StiffnessSigma, Is.EqualTo(0), "drone.json's build flies the nominal legs, as the goldens do");
            spec.DrawStiffness();
            Assert.That(spec.StiffnessSigma, Is.EqualTo(0.03));
            Assert.That(spec.StiffnessBound, Is.EqualTo(0.10));
            double lo = 1, hi = 1, sum = 0, sum2 = 0;
            int n = 0;
            for (ulong seed = 1; seed <= 4000; seed++)
            {
                var random = new FlightRandom(seed);
                var legs = new LegDamage(spec, random);
                int recorded = 0;
                foreach (ParameterDraw d in random.Draws)
                {
                    if (d.Stream != "leg.stiffness") continue;
                    Assert.That((d.Lo, d.Hi), Is.EqualTo((-0.10, 0.10)), d.Parameter);
                    Assert.That(d.Parameter, Is.EqualTo("leg.stiffness." + LegDamage.Names[recorded]));
                    Assert.That(legs.Modulus(recorded), Is.EqualTo(1 + d.Value), "the leg flies its own draw");
                    Assert.That(legs.StiffnessFactor(recorded), Is.EqualTo(1 + d.Value), "an intact leg's stiffness is its draw's");
                    recorded++;
                }
                Assert.That(recorded, Is.EqualTo(4), "four draws a flight");
                Assert.That(random.Opened, Does.Contain("leg.stiffness"));
                for (int k = 0; k < 4; k++)
                {
                    double m = legs.Modulus(k) - 1;
                    lo = Math.Min(lo, legs.Modulus(k));
                    hi = Math.Max(hi, legs.Modulus(k));
                    sum += m;
                    sum2 += m * m;
                    n++;
                }
            }
            double mean = sum / n, sigma = Math.Sqrt(sum2 / n - mean * mean);
            TestContext.WriteLine($"  {n} draws: {lo:F4}–{hi:F4}, mean {mean:+0.00000;-0.00000}, σ {sigma:F5}");
            Assert.That(lo, Is.GreaterThanOrEqualTo(0.90));
            Assert.That(hi, Is.LessThanOrEqualTo(1.10));
            Assert.That(Math.Abs(mean), Is.LessThan(0.002), "centred on the nominal leg");
            Assert.That(sigma, Is.EqualTo(0.03).Within(0.002), "σ 3 %");
            Assert.That(lo, Is.LessThan(0.92), "the draws reach toward the lower bound");
            Assert.That(hi, Is.GreaterThan(1.08), "and the upper");

            // Without a spread (the goldens' build, a log before 2.9) no stream is opened and every leg is nominal.
            LegDamageSpec none = Legs();
            var r0 = new FlightRandom(7);
            var nominal = new LegDamage(none, r0);
            Assert.That(r0.Opened, Does.Not.Contain("leg.stiffness"));
            for (int k = 0; k < 4; k++) Assert.That(nominal.StiffnessFactor(k), Is.EqualTo(1));
        }

        [Test]
        public void TheDrawsMoveNoOtherDraw()
        {
            // A stream of its own after the strengths: every other draw of the flight is the same value with or without it.
            LegDamageSpec spec = Legs(), none = spec.Clone();
            spec.DrawStiffness();
            var a = new FlightRandom(11);
            var b = new FlightRandom(11);
            new LegDamage(spec, a);
            new LegDamage(none, b);
            var without = new List<ParameterDraw>();
            foreach (ParameterDraw d in a.Draws)
                if (d.Stream != "leg.stiffness") without.Add(d);
            Assert.That(without.Count, Is.EqualTo(b.Draws.Count));
            for (int i = 0; i < without.Count; i++)
            {
                Assert.That(without[i].Parameter, Is.EqualTo(b.Draws[i].Parameter));
                Assert.That(without[i].Value, Is.EqualTo(b.Draws[i].Value), without[i].Parameter);
            }
        }

        // One recorded liftoff on Village's tilled spot in Severe rain, the session's airframe, the reach as given.
        static MemoryStream Record(bool reach)
        {
            DroneSpec spec = Drone();
            MapWorld farm = SlopeScenarios.Farmland();
            var world = new FlightWorld(farm);
            GroundSample g = SlopeScenarios.Ground(farm, 715.970, 105.470);
            var foot = new Double3(715.970, g.Height, 105.470);
            FlightSetup setup = SoilScenarios.Setup(spec, world, foot + new Double3(0, SoilScenarios.Standing(spec), 0), RainLevel.SevereRain, 3);
            FiberCutScenarios.SessionAirframe(spec, setup);
            setup.Contacts.AnchorReach = reach;
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.WorldId = "farmland";
            header.SetDrone(spec, spec.Nominal);
            var stream = new MemoryStream();
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            for (int k = 0; k < 4000; k++)
            {
                var stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8, Throttle = k < 2000 ? 0 : Math.Min(1, (k - 2000) / 500.0) * 0.8 };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            return stream;
        }

        static ReplayResult Replay(MemoryStream log)
        {
            ContactScenarios.RepositoryMaterials(out ulong materials);
            return FlightReplay.Run(new MemoryStream(log.ToArray()), StemScenarios.OpenFarmland, Drone().ContentHash, null, SoilScenarios.Table().Hash,
                materialsHash: materials);
        }

        [Test]
        public void TheHeaderLogsTheReachAndTheSpreadAndAFlightWithoutTheReachReplays()
        {
            // 2.4's debt: the reach is in ContactSpec and its own header entry, so a flight flown with it off replays bit for bit;
            // the legs' spread and their four draws ride along.
            foreach (bool reach in new[] { true, false })
            {
                MemoryStream log = Record(reach);
                LogHeader h = new FlightLogReader(new MemoryStream(log.ToArray())).Header;
                Assert.That(h.Contacts.AnchorReach, Is.EqualTo(reach), "the reach's switch");
                Assert.That(h.Contacts.Legs.StiffnessSigma, Is.EqualTo(0.03));
                Assert.That(h.Contacts.Legs.StiffnessBound, Is.EqualTo(0.10));
                int draws = 0;
                foreach (ParameterDraw d in h.Draws)
                    if (d.Stream == "leg.stiffness") draws++;
                Assert.That(draws, Is.EqualTo(4), "the four leg draws are in the header");
                Assert.That(h.Unknown, Is.Empty, "the reader knows the entry");
                ReplayResult r = Replay(log);
                TestContext.WriteLine($"  reach {(reach ? "on" : "off")}: replay {r.Outcome}, {r.Steps} steps; {r.Message}");
                Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
                Assert.That(r.Steps, Is.EqualTo(4000));
            }
        }

        [Test]
        public void ALogWithoutTheEntryReadsAsItsModelFlew()
        {
            // A log before 2.9 has no ground entry: the reach as its model flew it (on from 86) and no spread.
            DroneSpec spec = Drone();
            FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor("tilled"), new Double3(0, SoilScenarios.Standing(spec), 0));
            setup.Contacts.Legs.DrawStiffness();
            var core = new FlightCore(setup);
            Assert.That(LogHeader.Read(new MemoryStream(LogHeader.For(setup, core).Encode())).Contacts.Legs.StiffnessSigma, Is.EqualTo(0.03), "with the entry");
            foreach (int model in new[] { 85, 86 })
            {
                LogHeader h = LogHeader.For(setup, core);
                h.ModelVersion = model;
                LogHeader back = LogHeader.Read(new MemoryStream(Without(h.Encode(), 28)));
                Assert.That(back.Contacts.AnchorReach, Is.EqualTo(model >= 86), "model " + model);
                Assert.That(back.Contacts.Legs.StiffnessSigma, Is.EqualTo(0));
                Assert.That(back.Contacts.Legs.StiffnessBound, Is.EqualTo(0));
            }
        }

        // The header's bytes without one tagged entry (the prologue's length mended): an older build's header.
        static byte[] Without(byte[] header, ushort tag)
        {
            // The prologue (the magic, the version, the header's length at byte 12), then entries of u16 tag, u32 size, the bytes.
            const int Prologue = LogHeader.PrologueSize;
            var output = new MemoryStream();
            output.Write(header, 0, Prologue);
            int at = Prologue;
            while (at < header.Length)
            {
                ushort t = BitConverter.ToUInt16(header, at);
                int size = (int)BitConverter.ToUInt32(header, at + 2);
                if (t != tag) output.Write(header, at, 6 + size);
                at += 6 + size;
            }
            byte[] b = output.ToArray();
            BitConverter.GetBytes((uint)(b.Length - Prologue)).CopyTo(b, 12);
            return b;
        }

        [Test]
        public void AHeldLegTipsTheHorizonAndLetsGo()
        {
            // p7a-ground 1.17's "A held leg tips the horizon and lets go": the harness's flight from House's track start in Severe
            // rain (the plane's saturated belt_straw), seeds 1–10: a corner holds alone at least 20 ms after the other three,
            // the body turns at least 0.5° toward it, and the drone then leaves; no flight tunnels or crashes.
            DroneSpec spec = Drone();
            var world = new FlightWorld(SoilScenarios.Range());
            Double3 foot = SoilScenarios.TrackStart(world);
            double hover = LiftoffScenarios.Hover(spec, world, foot);
            int lone = 0;
            for (ulong seed = 1; seed <= 10; seed++)
            {
                LiftoffScenarios.Liftoff r = LiftoffScenarios.Fly(spec, new FlightWorld(SoilScenarios.Range()), foot, hover, RainLevel.SevereRain, seed, seed % 2 == 0);
                TestContext.WriteLine($"  seed {seed}: off {r.Off:F3} s at {r.OffThrottle:F3}; lone {r.Lone} {r.LoneHold * 1000:F0} ms {r.LoneTurn:F2}° {r.LonePeak:F1} N; " +
                    $"group {r.Group} {r.GroupHold * 1000:F0} ms {r.GroupTurn:F2}°");
                Assert.That(r.Off, Is.Not.NaN, "the drone leaves the ground");
                Assert.That(r.Tunnels + r.Crashes, Is.EqualTo(0));
                if (r.Lone < 0) continue;
                lone++;
                Assert.That(r.LoneHold, Is.GreaterThanOrEqualTo(LiftoffScenarios.HeldTime));
                Assert.That(r.LoneTurn, Is.GreaterThanOrEqualTo(LiftoffScenarios.HeldTurn));
            }
            Assert.That(lone, Is.GreaterThan(0), "a corner holds alone on some liftoff (3 of 10 measured, design 2.9)");
        }

        [Test]
        public void AVillageLiftoffAllocatesNothing()
        {
            // 0 B in steady state on the wet ground's liftoff with the legs drawn: the rest and the pull, steps alone.
            DroneSpec spec = Drone();
            MapWorld farm = SlopeScenarios.Farmland();
            GroundSample g = SlopeScenarios.Ground(farm, 715.970, 105.470);
            FlightSetup setup = SoilScenarios.Setup(spec, new FlightWorld(farm), new Double3(715.970, g.Height + SoilScenarios.Standing(spec), 105.470),
                RainLevel.SevereRain, 3);
            FiberCutScenarios.SessionAirframe(spec, setup);
            var core = new FlightCore(setup);
            for (int k = 0; k < 1000; k++) core.Advance(new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8 });
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 1000; k < 4000; k++)
                core.Advance(new StickSample { HostUs = k * FlightCore.StepUs, Arm = true, Throttle = k < 2000 ? 0 : Math.Min(1, (k - 2000) / 500.0) * 0.8 });
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine($"  {allocated} B over 3000 steps; the drone {core.State.Position.Y - g.Height:F3} m up");
            Assert.That(allocated, Is.EqualTo(0));
        }
    }
}
