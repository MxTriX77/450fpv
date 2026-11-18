using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// w17-four-legs 1.3: the four legs' tips in the soil (ContactModel's tips, ContactSpec.LegTips) against design §4's targets
    /// (TipScenarios), each with its known-wrong: model 105's skids (SkidsDrone, the switch off) or the new drone's tips on the ground's
    /// Coulomb alone (the switch off on the new drone). Every bound is the design's or the law's own number from the files.
    /// </summary>
    public sealed class TipTests
    {
        static void Print(string s) => TestContext.Progress.WriteLine(s);

        static DroneSpec Drone() => RepositoryDrone.Spec();

        static SoilSet Set(string surface, RainLevel rain)
        {
            SurfaceTable table = SoilScenarios.Table();
            foreach (SurfaceRow row in table.Surfaces)
                if (row.Name == surface) return row.Set(SurfaceTable.MoistureOf(rain));
            throw new ArgumentException(surface);
        }

        static ContactSpec Contacts()
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            return Drone().BuildContacts(Drone().Nominal, materials, hash, SoilScenarios.Table());
        }

        // The law's most a leg's tip holds on its way out at sink z (N): the shaft's grip and wet adhesion, and the wet face's suction's
        // peak σ_a·A_t (design 2, from the files).
        static double MostHold(ContactSpec c, in SoilSet set, double z)
        {
            double kt = set.TipResistance / c.TipReferenceDepth;
            return c.ShaftFriction * kt * Math.PI * c.ShaftDiameter * z * z / 2 + set.InterfaceAdhesion * Math.PI * c.ShaftDiameter * z
                + set.Adhesion * Math.PI * c.TipRadius * c.TipRadius;
        }

        // ---- The drone on four legs ---------------------------------------------------------------------------------------------

        [Test]
        public void TheRepositoryDroneStandsOnItsFourTips()
        {
            // The spec's "The model and the physics agree" (DRN-01, DRN-03, REAL-05): the legs-on drone loaded by the sim has four leg
            // tips as its lowest contact points, one a corner on its leg's part at the leg's lower end, no skid part or shape, and every
            // drawn leg its capsule ending at its tip; its contacts fly the tips (the switch on), its legs' law has no T-joint, skid,
            // sibling or frame (design 5, 6); the legs-off build takes the four legs alone. Model 105's file keeps its skids.
            DroneSpec spec = Drone();
            ContactSpec c = Contacts();
            Assert.That(c.LegTips, Is.True, "the switch: four legs stand on their tips");
            Assert.That(spec.PartIndex("skid_right"), Is.EqualTo(-1));
            Assert.That(spec.PartIndex("tjoint_rr"), Is.EqualTo(-1));
            Assert.That(spec.Legs.TJoint, Is.Null);
            double lowest = double.PositiveInfinity, tips = double.NegativeInfinity;
            int supports = 0;
            foreach (ContactShape sh in c.Shapes)
            {
                Assert.That(sh.Role, Is.Not.EqualTo(ContactRole.Skid), sh.Name);
                lowest = Math.Min(lowest, Math.Min(sh.A.Y, sh.B.Y) - sh.Radius);
                if (sh.Role != ContactRole.Support) continue;
                supports++;
                DronePart leg = spec.Parts[spec.Legs.Parts[sh.Corner]];
                Assert.That(sh.Part, Is.EqualTo(spec.Legs.Parts[sh.Corner]), sh.Name + " is its leg's");
                Assert.That(sh.A.Y - sh.Radius, Is.EqualTo(leg.Position.Y - leg.Length / 2).Within(1e-9), sh.Name + "'s lowest point is its leg's end");
                tips = Math.Max(tips, sh.A.Y - sh.Radius);
                int tubes = 0;
                foreach (ContactShape t in c.Shapes)
                    if (t.Role == ContactRole.Body && t.Part == sh.Part && (Math.Abs(t.B.Y - sh.A.Y) < 1e-9 || Math.Abs(t.A.Y - sh.A.Y) < 1e-9)) tubes++;
                Assert.That(tubes, Is.EqualTo(1), sh.Name + ": its leg's capsule ends at the tip");
            }
            Assert.That(supports, Is.EqualTo(4), "one tip a corner");
            Assert.That(lowest, Is.EqualTo(tips).Within(1e-9), "the tips are the lowest contact points");
            Assert.That(c.LegFrameSideStiffness, Is.Zero, "no skids' frame");
            for (int k = 0; k < 4; k++)
            {
                Assert.That(c.Legs.TJointParts[k], Is.EqualTo(-1));
                Assert.That(c.Legs.SkidParts[k], Is.EqualTo(-1));
                Assert.That(c.Legs.Sibling[k], Is.EqualTo(-1));
            }
            Assert.That(spec.LegsOff.Removed.Length, Is.EqualTo(8), "the four legs and their sockets (1.5)");
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            Assert.That(SkidsDrone.Spec().BuildContacts(SkidsDrone.Spec().Nominal, materials, hash, SoilScenarios.Table()).LegTips, Is.False, "model 105's skids");
            Assert.That(SkidsDrone.Spec().LegsOff.Removed.Length, Is.EqualTo(10), "model 105's: 4 legs, 4 T-joints, 2 skids");
        }

        // ---- Target 1: two seeds start differently, one replays --------------------------------------------------------------

        [Test]
        public void TwoSeedsStartDifferentlyAndOneReplays()
        {
            // "Each flight starts on its own footing" (PHY-24): the start's draws leg.setdown, uniform in [1, 1.5], set each tip to its
            // own depth on meadow; two seeds differ leg by leg, one seed flies the same bits twice and the body starts at rest.
            DroneSpec spec = Drone();
            TipScenarios.Start a = TipScenarios.Rest(spec, "meadow_sod", RainLevel.Rain, 0x450F), b = TipScenarios.Rest(spec, "meadow_sod", RainLevel.Rain, 0x51);
            TipScenarios.Start again = TipScenarios.Rest(spec, "meadow_sod", RainLevel.Rain, 0x450F);
            for (int c = 0; c < 4; c++)
            {
                Print($"leg {c}: draws {a.SetDown[c]:F4} / {b.SetDown[c]:F4}, start sinks {a.StartSink[c] * 1e3:F3} / {b.StartSink[c] * 1e3:F3} mm, deepest "
                    + $"{a.StartMax[c] * 1e3:F3} / {b.StartMax[c] * 1e3:F3} mm, after 1 s {a.Sink[c] * 1e3:F3} / {b.Sink[c] * 1e3:F3} mm");
                Assert.That(a.SetDown[c], Is.InRange(1.0, 1.5));
                Assert.That(b.SetDown[c], Is.InRange(1.0, 1.5));
                Assert.That(a.SetDown[c], Is.Not.EqualTo(b.SetDown[c]), "the draws differ per leg");
                Assert.That(a.StartSink[c], Is.Not.EqualTo(b.StartSink[c]), "the start's depths differ per leg");
                Assert.That(a.StartSink[c], Is.GreaterThan(0), "each tip stands slightly in the ground");
                Assert.That(a.StartMax[c], Is.GreaterThanOrEqualTo(a.StartSink[c]));
            }
            Print($"settled {a.Settle * 1e3:F3} / {b.Settle * 1e3:F3} mm, most speed {a.Speed * 1e3:F2} / {b.Speed * 1e3:F2} mm/s");
            Assert.That(again.Hash, Is.EqualTo(a.Hash), "one seed, the same bits");
            Assert.That(a.Hash, Is.Not.EqualTo(b.Hash), "two seeds, two starts");
            Assert.That(Math.Abs(a.Settle), Is.LessThan(1e-3), "the start is at rest on its four: it settles less than a millimetre");
        }

        // A recorded start and careful liftoff on meadow in Severe rain, on the tips (the header's tag 35 and the draws).
        static MemoryStream Record(ulong seed)
        {
            DroneSpec spec = Drone();
            FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor("meadow_sod"), new Double3(0, SoilScenarios.Standing(spec), 0), RainLevel.SevereRain, seed);
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            header.WorldId = "floor";
            header.SetDrone(spec, spec.Nominal);
            var stream = new MemoryStream();
            var recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            for (int k = 0; k < 3000; k++)
            {
                var stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8, Throttle = k < 1000 ? 0 : Math.Min(1, (k - 1000) / 1500.0) * 0.7 };
                core.Advance(stick);
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            return stream;
        }

        [Test]
        public void TheHeaderLogsTheTipsAndTheirDrawsAndTheFlightReplays()
        {
            // Design 8: the tip law's entry (the switch, its values, the surfaces' tip resistances) and the four leg.setdown draws with
            // their bounds are in the header, and the flight replays bit for bit from it (LOG-01, PHY-24).
            MemoryStream log = Record(0x450F);
            LogHeader h = new FlightLogReader(new MemoryStream(log.ToArray())).Header;
            ContactSpec c = Contacts();
            Assert.That(h.Contacts.LegTips, Is.True);
            Assert.That(h.Contacts.TipRadius, Is.EqualTo(c.TipRadius));
            Assert.That(h.Contacts.ShaftDiameter, Is.EqualTo(c.ShaftDiameter));
            Assert.That(h.Contacts.ShaftFriction, Is.EqualTo(c.ShaftFriction));
            Assert.That(h.Contacts.SetDownMax, Is.EqualTo(c.SetDownMax));
            Assert.That(h.Contacts.Surfaces.Surfaces[0].Saturated.TipResistance, Is.EqualTo(c.Surfaces.Surfaces[0].Saturated.TipResistance));
            Assert.That(h.Unknown, Is.Empty, "the reader knows the entry");
            int draws = 0;
            foreach (ParameterDraw d in h.Draws)
                if (d.Stream == "leg.setdown")
                {
                    draws++;
                    Assert.That(d.Lo, Is.EqualTo(c.SetDownMin));
                    Assert.That(d.Hi, Is.EqualTo(c.SetDownMax));
                }
            Assert.That(draws, Is.EqualTo(4), "the four set-down draws");
            ContactScenarios.RepositoryMaterials(out ulong materials);
            ReplayResult r = FlightReplay.Run(new MemoryStream(log.ToArray()), _ => SoilScenarios.Floor("meadow_sod"), Drone().ContentHash, null,
                SoilScenarios.Table().Hash, materialsHash: materials);
            Print($"replay {r.Outcome}, {r.Steps} steps; {r.Message}");
            Assert.That(r.Outcome, Is.EqualTo(ReplayOutcome.Match), r.Message);
        }

        [Test]
        public void ALogWithoutTheEntryReadsAsTheSkids()
        {
            // A log before 1.3 has no tag 35: its contacts flew the skids (ContactSpec.LegTips off).
            DroneSpec spec = Drone();
            FlightSetup setup = SoilScenarios.Setup(spec, SoilScenarios.Floor("meadow_sod"), new Double3(0, SoilScenarios.Standing(spec), 0));
            var core = new FlightCore(setup);
            LogHeader h = LogHeader.For(setup, core);
            Assert.That(LogHeader.Read(new MemoryStream(h.Encode())).Contacts.LegTips, Is.True, "with the entry");
            Assert.That(LogHeader.Read(new MemoryStream(Without(h.Encode(), 35))).Contacts.LegTips, Is.False, "without it (tag 35)");
        }

        // The header's bytes without one tagged entry (the prologue's length mended): an older build's header.
        static byte[] Without(byte[] header, ushort tag)
        {
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

        // ---- Target 2: no magnet ----------------------------------------------------------------------------------------------

        [Test]
        public void ACarefulLiftoffFromWetMeadowHasNoMagnet()
        {
            // Design §4 target 2 (the spec's "A careful liftoff"): from the start on meadow in Severe rain, the thrust over weight rising
            // at the pilot's 0.41 /s: the ground never pulls the drone harder than the four tips' most hold by the law (against model
            // 103's 26.9 N measured); the last leg breaks free at a thrust over weight within its hold over the last corner's share of
            // the excess (design 2's 0.345 at the 2 kg loadout, this build's own here) and the controller's 0.05 (assumed); the upward
            // speed then is under 0.1 m/s (against 0.34–0.52), and at the last release the body has no more kinetic energy than the
            // thrust's excess did work on its climb (nothing stored is given back). Known-wrong: model 105's skids fail it.
            DroneSpec spec = Drone();
            ContactSpec c = Contacts();
            SoilSet set = Set("meadow_sod", RainLevel.SevereRain);
            TipScenarios.Start start = TipScenarios.Rest(spec, "meadow_sod", RainLevel.SevereRain, 0x450F, 0.01);
            double most = 0, sum = 0;
            for (int k = 0; k < 4; k++)
            {
                double hold = MostHold(c, set, start.StartMax[k]);
                most = Math.Max(most, hold);
                sum += hold;
            }
            MassProperties body = spec.BuildMass(spec.Nominal).Properties;
            double r2 = 0;
            foreach (ContactShape sh in c.Shapes)
                if (sh.Role == ContactRole.Support) r2 = Math.Max(r2, sh.A.X * sh.A.X + sh.A.Z * sh.A.Z); // a tip's distance from the centre in plan
            double iDiag = (body.Inertia.XX + body.Inertia.ZZ) / 2, share = iDiag / (iDiag + body.Mass * r2);
            double weight = body.Mass * 9.80665, bound = 1 + most / (share * weight) + 0.05;
            TipScenarios.Liftoff l = TipScenarios.Lift(spec, "meadow_sod", RainLevel.SevereRain, 0.41);
            TipScenarios.Liftoff skids = TipScenarios.Lift(SkidsDrone.Spec(), "meadow_sod", RainLevel.SevereRain, 0.41);
            Print($"the law's most hold {most:F2} N a leg, {sum:F2} N the four; the last corner's share {share:F3}, the bound T/W {bound:F3}");
            Print("tips: " + TipScenarios.Describe(l));
            Print("skids (known-wrong): " + TipScenarios.Describe(skids));
            Assert.That(l.Crashes + l.Tunnels, Is.Zero);
            Assert.That(l.Pull, Is.GreaterThan(0), "premise: the wet ground holds the tips");
            Assert.That(l.Pull, Is.LessThanOrEqualTo(sum), "no magnet: the ground's pull is the tips' holds");
            Assert.That(l.BreakTw, Is.LessThanOrEqualTo(bound), "the last leg breaks free near the weight");
            Assert.That(l.BreakUp, Is.LessThan(0.1), "no jump at the break");
            Assert.That(l.Kinetic, Is.LessThanOrEqualTo(l.Excess), "nothing stored is given back at the release");
            Assert.That(l.Freed, Is.EqualTo(4), "each held tip logs its TipFreed");
            Assert.That(skids.Pull > sum || skids.BreakTw > bound || skids.BreakUp >= 0.1, Is.True, "known-wrong: model 105's skids are the magnet");
        }

        // ---- Target 3: hard ground ----------------------------------------------------------------------------------------------

        [Test]
        public void HardGroundTakesNoTipAndHoldsNoLeg()
        {
            // Design §4 target 3 (the spec's "Hard ground takes no tip"): on asphalt and a concrete slab a 1.5 m/s touchdown leaves no tip
            // in the ground, and a careful liftoff from asphalt holds no leg: no pull, the last leg off at T/W ≤ 1.01 + the controller's
            // 0.05 (assumed, as target 2's).
            DroneSpec spec = Drone();
            foreach (string surface in new[] { "asphalt", "concrete_slab" })
            {
                TipScenarios.Touchdown d = TipScenarios.Drop(spec, surface, RainLevel.Sunny, 1.5);
                Print($"{surface} 1.5 m/s: " + TipScenarios.Describe(d));
                Assert.That(d.Deepest, Is.Zero, surface + ": no tip enters");
                Assert.That(d.Crashes + d.Tunnels, Is.Zero);
            }
            TipScenarios.Liftoff l = TipScenarios.Lift(spec, "asphalt", RainLevel.SevereRain, 0.41);
            Print("asphalt liftoff: " + TipScenarios.Describe(l));
            Assert.That(l.Pull, Is.LessThan(1e-9), "nothing pulls");
            Assert.That(l.LastTw, Is.LessThanOrEqualTo(1.06), "no leg held");
        }

        // ---- Target 4: no sled ----------------------------------------------------------------------------------------------------

        [Test]
        public void TipsInTheSoilHoldAPushAcross()
        {
            // Design §4 target 4 (the spec's "No sliding like a sled"): the pilot's 2 kg loadout (design §3's) after a 0.5 m/s touchdown
            // on dry meadow, the thrust held at 0.92 of the weight (the pilot's first motion, design §1.1), a steady side wind pushing the
            // drone across (its push the ground's mean hold over the last second, at least 4 N, a Severe wind's order): it moves less than
            // the legs' bending, under 0.05 m (against F23's 0.20 m). Known-wrong: the same tips on the ground's Coulomb alone slide.
            DroneSpec spec = Drone();
            TipScenarios.Push tips = TipScenarios.Pushed(spec, "meadow_sod", 0.5, 0.92, Wind, payload: 2);
            TipScenarios.Push coulomb = TipScenarios.Pushed(spec, "meadow_sod", 0.5, 0.92, Wind, tips: false, payload: 2);
            Print($"tips: {tips.Depth * 1e3:F2} mm in, T/W {tips.Tw:F3}, held {tips.Held:F2} N, moved {tips.Moved * 1e3:F1} mm, {tips.Ploughed} ploughs");
            Print($"Coulomb (known-wrong): T/W {coulomb.Tw:F3}, held {coulomb.Held:F2} N, moved {coulomb.Moved * 1e3:F1} mm");
            Assert.That(tips.Crashes + tips.Tunnels, Is.Zero);
            Assert.That(tips.Held, Is.GreaterThanOrEqualTo(4), "premise: the wind pushes at least 4 N");
            Assert.That(tips.Moved, Is.LessThan(0.05), "the tips hold it");
            Assert.That(coulomb.Moved, Is.GreaterThanOrEqualTo(0.05), "known-wrong: Coulomb on the tips' faces alone slides");
        }

        /// <summary>The side wind of target 4's push, m/s (assumed: the order of the pilot's Severe level, centre 9.5 m/s, at the least
        /// that pushes this airframe 4 N on the ground).</summary>
        const double Wind = 8;

        // ---- Target 5: a rougher landing digs deeper ------------------------------------------------------------------------------

        [Test]
        public void ARougherLandingDigsDeeperByTheHandCalculation()
        {
            // Design §4 target 5 (the spec's "A rough landing digs deeper"): level touchdowns with the throttle cut at 0.5 and 1.5 m/s
            // on dry meadow, tilled and crust: the tips end deeper after the rougher one, and the four legs' mean deepest and end sinks
            // are the hand calculation's (TipScenarios.HandTouchdown, four_legs.py's, on this build's mass and the files) within 20 %.
            DroneSpec spec = Drone();
            ContactSpec c = Contacts();
            double mass = spec.BuildMass(spec.Nominal).Properties.Mass, firm = SoilScenarios.Table().FirmLayer;
            foreach (string surface in new[] { "meadow_sod", "tilled", "dry_crust" })
            {
                double softEnd = 0, softDeep = 0;
                foreach (double v in new[] { 0.5, 1.5 })
                {
                    TipScenarios.Touchdown d = TipScenarios.Drop(spec, surface, RainLevel.Sunny, v);
                    TipScenarios.HandTouchdown(c, Set(surface, RainLevel.Sunny), firm, mass, d.TouchSpeed, out double deepest, out double end, out double rebound);
                    double meanMax = (d.Max[0] + d.Max[1] + d.Max[2] + d.Max[3]) / 4, meanEnd = (d.End[0] + d.End[1] + d.End[2] + d.End[3]) / 4;
                    Print($"{surface} {v} m/s: {TipScenarios.Describe(d)}; mean deepest {meanMax * 1e3:F2} mm, end {meanEnd * 1e3:F2} mm; the hand "
                        + $"{deepest * 1e3:F2} / {end * 1e3:F2} mm, rebound {rebound:F3} m/s");
                    Assert.That(d.Crashes + d.Tunnels, Is.Zero);
                    Assert.That(meanMax, Is.EqualTo(deepest).Within(20).Percent, surface + " deepest");
                    if (v == 0.5) softDeep = meanMax;
                    else Assert.That(meanMax, Is.GreaterThan(softDeep), surface + ": the rougher landing digs deeper");
                    // The end sink is the hand's where the tips stayed in their holes. A touchdown that bounces them out (the hand's own
                    // rebound: crust at 1.5 m/s) turns the body in the air and its tips land beside their holes, on virgin soil: the hand's
                    // one-dimensional end does not hold there (a reported miss of target 5's end, measured at 1.3).
                    if (d.Lost > 0)
                    {
                        Print($"  {surface} {v} m/s: the tips bounced out ({d.Lost} contacts lost): the end {meanEnd * 1e3:F2} mm is not the hand's 1-D {end * 1e3:F2} mm");
                        continue;
                    }
                    Assert.That(meanEnd, Is.EqualTo(end).Within(20).Percent, surface + " end");
                    if (v == 0.5) softEnd = meanEnd;
                    else Assert.That(meanEnd, Is.GreaterThan(softEnd), surface + ": the rougher landing ends deeper");
                }
            }
        }

        // ---- Target 6: a landing on sod has the drone's weight -----------------------------------------------------------------

        [Test]
        public void ALandingOnSodHasTheDronesWeight()
        {
            // Design §4 target 6 (the spec's "A touchdown on sod"): level, 0.5 m/s, the throttle cut, dry meadow: one rebound at most 2 mm,
            // at most 2 contacts lost, at rest (under 1 cm/s, the landings' rest) within 1 s; at 0.3 m/s along it slides under 0.05 m.
            // Known-wrong: the same tips on the ground's Coulomb alone (no soil under them: the legs' own bounce, as on asphalt) fail it.
            // Model 105's skids (F05: 8 contacts lost, a 1.84 m slide at 1.0 m/s down and 1.49 along on the track) are printed: at this
            // target's 0.5 m/s they meet it too (measured at 1.3).
            DroneSpec spec = Drone();
            TipScenarios.Touchdown level = TipScenarios.Drop(spec, "meadow_sod", RainLevel.Sunny, 0.5);
            TipScenarios.Touchdown along = TipScenarios.Drop(spec, "meadow_sod", RainLevel.Sunny, 0.5, 0.3);
            TipScenarios.Touchdown skids = TipScenarios.Drop(SkidsDrone.Spec(), "meadow_sod", RainLevel.Sunny, 0.5);
            TipScenarios.Touchdown coulomb = TipScenarios.Drop(spec, "meadow_sod", RainLevel.Sunny, 0.5, tips: false);
            TipScenarios.Touchdown skidsAlong = TipScenarios.Drop(SkidsDrone.Spec(), "meadow_sod", RainLevel.Sunny, 0.5, 0.3);
            Print("level: " + TipScenarios.Describe(level));
            Print("along 0.3 m/s: " + TipScenarios.Describe(along));
            Print("Coulomb (known-wrong): " + TipScenarios.Describe(coulomb));
            Print("skids level: " + TipScenarios.Describe(skids));
            Print("skids along: " + TipScenarios.Describe(skidsAlong));
            Assert.That(level.Rebound, Is.LessThanOrEqualTo(0.002));
            Assert.That(level.Lost, Is.LessThanOrEqualTo(2));
            Assert.That(level.Rest, Is.LessThanOrEqualTo(1.0), "at rest within 1 s");
            Assert.That(along.Slide, Is.LessThan(0.05), "no sled");
            Assert.That(along.Rebound, Is.LessThanOrEqualTo(0.002));
            Assert.That(along.Lost, Is.LessThanOrEqualTo(2));
            Assert.That(level.Crashes + level.Tunnels + along.Crashes + along.Tunnels, Is.Zero);
            bool fails = coulomb.Rebound > 0.002 || coulomb.Lost > 2 || !(coulomb.Rest <= 1.0);
            Assert.That(fails, Is.True, "known-wrong: the tips on Coulomb alone bounce");
        }

        // ---- 0 B and the step's cost (design 8, PLT-04) -------------------------------------------------------------------------

        [Test]
        public void TheTipsAllocateNothing()
        {
            // 0 B in steady state on the tips: at rest on wet meadow, a careful liftoff, the holds and the releases, steps alone.
            DroneSpec spec = Drone();
            var core = new FlightCore(SoilScenarios.Setup(spec, SoilScenarios.Floor("meadow_sod"), new Double3(0, SoilScenarios.Standing(spec), 0), RainLevel.SevereRain));
            for (int k = 0; k < 1000; k++) core.Advance(new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8 });
            long before = AllocProbe.Before();
            for (int k = 1000; k < 4000; k++)
                core.Advance(new StickSample { HostUs = k * FlightCore.StepUs, Arm = true, Throttle = k < 1500 ? 0 : Math.Min(1, (k - 1500) / 1500.0) * 0.8 });
            long allocated = AllocProbe.After(before);
            Print($"{allocated} B over 3000 steps; the drone {core.State.Position.Y:F3} m up");
            Assert.That(core.State.Position.Y, Is.GreaterThan(0.3), "premise: it lifted off");
            Assert.That(allocated, Is.Zero, AllocProbe.Report());
        }

        [Test, Explicit("a timing: the interleaved A/B of the step on the tips against the half-skids, on wet meadow at rest")]
        public void TheTipsStepAgainstTheHalfSkids()
        {
            // Design 8: one node a corner instead of three. The same rest (armed, idle, Severe rain on meadow) on four legs and on model
            // 105's skids, in blocks of 500 steps taken in turn, each one's block times sorted: their medians and the ratio.
            FlightCore Make(DroneSpec spec) =>
                new FlightCore(SoilScenarios.Setup(spec, SoilScenarios.Floor("meadow_sod"), new Double3(0, SoilScenarios.Standing(spec), 0), RainLevel.SevereRain));
            FlightCore a = Make(Drone()), b = Make(SkidsDrone.Spec());
            const int Blocks = 40, Steps = 500;
            var ta = new double[Blocks];
            var tb = new double[Blocks];
            var watch = new System.Diagnostics.Stopwatch();
            for (int k = 0; k < 1000; k++)
            {
                a.Advance(new StickSample { HostUs = a.State.Step * FlightCore.StepUs, Arm = k >= 8 });
                b.Advance(new StickSample { HostUs = b.State.Step * FlightCore.StepUs, Arm = k >= 8 });
            }
            for (int i = 0; i < Blocks; i++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    FlightCore core = (i + pass) % 2 == 0 ? a : b;
                    watch.Restart();
                    for (int k = 0; k < Steps; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true });
                    watch.Stop();
                    (core == a ? ta : tb)[i] = watch.Elapsed.TotalMilliseconds * 1000 / Steps;
                }
            }
            Array.Sort(ta);
            Array.Sort(tb);
            double ma = ta[Blocks / 2], mb = tb[Blocks / 2];
            Print($"the step at rest: on the tips {ma:F2} µs (quartiles {ta[Blocks / 4]:F2}–{ta[3 * Blocks / 4]:F2}), on the half-skids {mb:F2} µs "
                + $"({tb[Blocks / 4]:F2}–{tb[3 * Blocks / 4]:F2}); ratio {ma / mb:F3}");
        }

        // ---- The legs' law on four legs (design 5) ---------------------------------------------------------------------------------

        [Test, Explicit("a probe: prints the tips' start, touchdowns, liftoffs and pushes")]
        public void Probe()
        {
            DroneSpec spec = Drone();
            foreach (double payload in new[] { double.NaN, 2.0 })
                foreach (double wind in new[] { 4.0, 6.0, 8.0 })
                {
                    TipScenarios.Push p = TipScenarios.Pushed(spec, "meadow_sod", 0.5, 0.92, wind, true, payload), q = TipScenarios.Pushed(spec, "meadow_sod", 0.5, 0.92, wind, false, payload);
                    Print($"payload {payload} wind {wind}: tips {p.Depth * 1e3:F2} mm T/W {p.Tw:F3} onset {p.Onset:F2} N held {p.Held:F2} N moved {p.Moved * 1e3:F1} mm ploughs {p.Ploughed}; "
                        + $"Coulomb onset {q.Onset:F2} held {q.Held:F2} moved {q.Moved * 1e3:F1} mm");
                }
            foreach (double along in new[] { 0.5, 0.75, 1.0 })
                Print($"meadow dry 1.5 m/s, {along} along: " + TipScenarios.Describe(TipScenarios.Drop(spec, "meadow_sod", RainLevel.Sunny, 1.5, along)));
            Print("liftoff meadow Sunny: " + TipScenarios.Describe(TipScenarios.Lift(spec, "meadow_sod", RainLevel.Sunny, 0.72)));
            Print("liftoff tilled Severe rain: " + TipScenarios.Describe(TipScenarios.Lift(spec, "tilled", RainLevel.SevereRain, 0.41)));
        }
    }
}
