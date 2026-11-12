using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7g-legs 1.4, "Replay through a broken and a detached leg" (leg-damage): tools/replay's damage flight (ScriptedFlight.FlyLegs,
    /// recorded here in memory) loses leg_rr up into a bar and bends and then breaks the other three (its rebound lands the
    /// two-legged drone at 2.4 m/s on the straw); a drifting touch (0.5 m/s down rolled 10° with 0.6 m/s across, seed 20 since the
    /// skid's frame, model 54: seed 2's touch leaves leg_rr 0.24° and leg_fr 0.44° on it, model 51's 2.03°) leaves leg_rr bent 1.75°
    /// and on. Each log replays headless bit for bit, every record, event and loose-piece record; the damage flight's events
    /// name each leg's change in step order; and a replay holding drone.json with one strength bound changed is refused
    /// before its first step. On model 49 no flight was found that ends with one leg bent and on beside one broken and one
    /// lost (the damage flight on seeds 1–40, its pad, cushion and speed varied): a landing hard enough to follow a lost leg
    /// parts the rest past their sets.
    /// </summary>
    public sealed class LegReplayTests
    {
        const string World = "floor-pads";

        static FlightWorld Pads() => new FlightWorld(new FloorWorld(0, "belt_bare", "steel", "timber", "fabric", "straw"));

        static FlightWorld Open(string id) => id == World ? Pads() : null;

        static RuntimeObject Slab(FlightWorld world, string name, string material, Double3 centre, Double3 half) => new RuntimeObject(name, new[]
        {
            new ShapeGeometry
            {
                Kind = ShapeKind.Box, Center = centre, AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1),
                HalfExtents = half, Material = world.Query.MaterialId(material),
            },
        });

        // The damage flight (tools/replay --record <log> --legs): the free body on seed 11 up at 6 m/s, leg_rr's skid end under
        // a steel bar over its overhang, a fabric cushion over it, down onto a straw pad; 4000 steps.
        static byte[] Record(FlightSetup setup, string world, DroneSpec spec, int steps, out FlightCore core)
        {
            core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.WorldId = world;
            header.SetDrone(spec, spec.Nominal);
            var stream = new MemoryStream();
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            for (int k = 0; k < steps; k++)
            {
                var stick = new StickSample { HostUs = k * FlightCore.StepUs };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            return stream.ToArray();
        }

        static byte[] Fly(DroneSpec spec, out FlightCore core)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong materialsHash);
            FlightWorld world = Pads();
            const double pad = 0.125, y0 = 0.26 + pad, gap = 0.1;
            Double3 foot = spec.BuildContacts(spec.Nominal, materials, materialsHash).Legs.Foot[0];
            double skidTop = y0 + foot.Y + 0.015;
            var setup = new FlightSetup
            {
                Seed = 11, World = world, Mass = spec.BuildMass(spec.Nominal), Contacts = spec.BuildContacts(spec.Nominal, materials, materialsHash),
                StartPosition = new Double3(0, y0, 0), StartVelocity = new Double3(0, 6, 0),
                RuntimeObjects = new[]
                {
                    Slab(world, "bar", "steel", new Double3(foot.X, skidTop + 0.012, foot.Z - 0.06), new Double3(0.03, 0.01, 0.01)),
                    Slab(world, "cushion", "fabric", new Double3(0, y0 + 0.11 + gap + 0.75, 0), new Double3(1.5, 0.75, 1.5)),
                    Slab(world, "pad", "straw", new Double3(0, pad / 2, 0), new Double3(1.5, pad / 2, 1.5)),
                },
            };
            return Record(setup, World, spec, 4000, out core);
        }

        [Test]
        public void ReplayThroughABrokenAndADetachedLeg()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            byte[] log = Fly(spec, out FlightCore core);
            ContactScenarios.RepositoryMaterials(out ulong materials);

            // The legs' changes, from the log, in step order.
            var changes = new List<string>();
            var states = new LegFlags[4];
            var reader = new FlightLogReader(new MemoryStream(log));
            while (reader.Next())
            {
                if (reader.Kind != LogFormat.EventBlock) continue;
                reader.ReadEvent(out LoggedEvent item);
                FlightEvent e = item.Event;
                if (e.Kind < EventKind.LegSet || e.Kind > EventKind.SkidLoose) continue;
                int leg = System.Array.IndexOf(core.Contacts.Spec.Legs.Parts, e.Part);
                string name = leg >= 0 ? "leg_" + LegDamage.Names[leg] : "part " + e.Part;
                changes.Add($"{item.Step} {e.Kind} {name} ({(LegMode)e.Index}, {e.A:F2} past {e.B:F2})");
            }
            for (int c = 0; c < 4; c++) states[c] = core.Contacts.Legs.Flags(c);
            TestContext.Out.WriteLine(string.Join("\n", changes));
            int bentBroken = 0, off = 0;
            for (int c = 0; c < 4; c++)
            {
                TestContext.Out.WriteLine($"leg_{LegDamage.Names[c]}: {states[c]}, set {LegScenarios.SetDegrees(core.Contacts.Legs.Set(c)):F2}°");
                if ((states[c] & LegFlags.Detached) != 0) off++;
                else if ((states[c] & (LegFlags.Broken | LegFlags.Set)) == (LegFlags.Broken | LegFlags.Set)) bentBroken++;
            }
            Assert.That(off, Is.GreaterThanOrEqualTo(1), "a leg comes off");
            Assert.That(bentBroken, Is.GreaterThanOrEqualTo(1), "a leg bends and then breaks");
            long last = -1;
            foreach (string ch in changes)
            {
                long step = long.Parse(ch.Substring(0, ch.IndexOf(' ')));
                Assert.That(step, Is.GreaterThanOrEqualTo(last), "the changes in step order");
                last = step;
            }
            StringAssert.Contains("LegDetached leg_rr", string.Join("\n", changes), "the event names the leg that came off");

            ReplayResult same = FlightReplay.Run(new MemoryStream(log), Open, spec.ContentHash, materialsHash: materials);
            TestContext.Out.WriteLine(same.Message);
            Assert.That(same.Outcome, Is.EqualTo(ReplayOutcome.Match), same.Message);
            Assert.That(same.Steps, Is.EqualTo(4000));
            Assert.That(same.Events, Is.GreaterThanOrEqualTo(changes.Count));
            Assert.That(same.Loose, Is.GreaterThan(0), "the loose pieces' side record replayed");

            // One strength bound changed (the tube's yield stress's upper bound, 58 → 59 MPa): drone.json's hash moves and the
            // replay is refused before its first step.
            string text = RepositoryDrone.Text(), from = "\"yield_stress_bounds_pa\": {\"value\": [5.0e7, 5.8e7]";
            Assert.That(text.Contains(from), Is.True);
            DroneSpec other = DroneSpec.Parse(text.Replace(from, "\"yield_stress_bounds_pa\": {\"value\": [5.0e7, 5.9e7]"));
            Assert.That(other.ContentHash, Is.Not.EqualTo(spec.ContentHash));
            ReplayResult refused = FlightReplay.Run(new MemoryStream(log), Open, other.ContentHash, materialsHash: materials);
            TestContext.Out.WriteLine(refused.Message);
            Assert.That(refused.Outcome, Is.EqualTo(ReplayOutcome.Refused));
            Assert.That(refused.Steps, Is.EqualTo(0));
            StringAssert.Contains("drone.json differs", refused.Message);

            // The drifting touch: bent and on, by a set a pilot sees (past 1°). Since model 86 (the anchors' reach) at 12° and
            // (1.0, −0.7) m/s: 10° and (0.6, −0.5) bent leg_fr 1.92° only by a support's anchor that slid 0.21 m in one step
            // (past the leg's 0.176 m reach), and now sets leg_rr 0.61°.
            MaterialRow[] rows = ContactScenarios.RepositoryMaterials(out ulong rowsHash);
            var touch = new LegScenarios.Touch { RollDeg = 12, Velocity = new Double3(1.0, -0.7, 0), Seed = 20 };
            byte[] drift = Record(LegScenarios.Setup(spec, rows, rowsHash, touch), "floor", spec, 3000, out FlightCore landed);
            int bentOn = 0;
            for (int c = 0; c < 4; c++)
            {
                LegFlags f = landed.Contacts.Legs.Flags(c);
                TestContext.Out.WriteLine($"the drifting touch, leg_{LegDamage.Names[c]}: {f}, set {LegScenarios.SetDegrees(landed.Contacts.Legs.Set(c)):F4}°");
                if ((f & LegFlags.Set) != 0 && (f & (LegFlags.Broken | LegFlags.Detached)) == 0 && LegScenarios.SetDegrees(landed.Contacts.Legs.Set(c)) > 1) bentOn++;
            }
            Assert.That(bentOn, Is.GreaterThanOrEqualTo(1), "a leg bent past 1° and on");
            ReplayResult bentReplay = FlightReplay.Run(new MemoryStream(drift), id => id == "floor" ? FlightWorld.Floor() : null, spec.ContentHash, materialsHash: materials);
            TestContext.Out.WriteLine(bentReplay.Message);
            Assert.That(bentReplay.Outcome, Is.EqualTo(ReplayOutcome.Match), bentReplay.Message);
        }
    }
}
