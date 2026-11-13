using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // w3-farmland-ground 2.7: contact with the farmland's objects. A wall stops the drone the same however thin its collision
    // (W4b's ruins are 2 cm skins), a prop's tip meets a thin skin as a face, a drone in a crown pays for its twigs, and without
    // surfaces.json a blade at rest passes the floor as its passes do. The repository's drone on its legs, still air; the walls
    // timber runtime objects on the stub's floor, the crown the farmland's.
    public sealed class ObjectContactTests
    {
        const double Face = 30;

        // A timber wall 8 m wide and 4 m tall on the stub's floor, its face at z = Face, made of skins (each from, to: m behind the
        // face): one slab, one thin skin, or W4b's two skins with the cavity between.
        static RuntimeObject Wall(FlightWorld world, double x, params (double From, double To)[] skins)
        {
            ushort timber = world.Query.MaterialId("timber");
            var shapes = new ShapeGeometry[skins.Length];
            for (int i = 0; i < skins.Length; i++)
                shapes[i] = new ShapeGeometry
                {
                    Kind = ShapeKind.Box, Center = new Double3(x, 2, Face + 0.5 * (skins[i].From + skins[i].To)), AxisX = new Double3(1, 0, 0),
                    AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1), HalfExtents = new Double3(4, 2, 0.5 * (skins[i].To - skins[i].From)),
                    Material = timber,
                };
            return new RuntimeObject("wall", shapes);
        }

        static readonly (string Name, (double, double)[] Skins)[] Walls =
        {
            ("a 1 m slab", new[] { (0.0, 1.0) }),
            ("a 2 cm skin", new[] { (0.0, 0.02) }),
            ("a 1 cm skin", new[] { (0.0, 0.01) }),
            ("two 2 cm skins 16 cm apart (W4b's)", new[] { (0.0, 0.02), (0.18, 0.2) }),
        };

        struct Throw
        {
            public double Peak, Impulse, EndSpeed;
            public int Tunnels, PastFace;
        }

        // The drone disarmed, level, thrown at 8.7 m/s (A4's crash speed into R01_A's wall) north at 1.5 m from 0.5 m short of the
        // face, for 0.4 s: the contacts' peak acceleration, their impulse along z, its speed along z at the end, the tunnels and the
        // touches made past the face.
        static Throw Thrown((double, double)[] skins)
        {
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong hash);
            DroneSpec spec = RepositoryDrone.Spec();
            FlightWorld world = FlightWorld.Floor();
            FlightSetup setup = ContactScenarios.Setup(spec, m, hash, true, new Double3(0, 1.5, Face - 0.5));
            setup.World = world;
            setup.RuntimeObjects = new[] { Wall(world, 0, skins) };
            setup.StartVelocity = new Double3(0, 0, 8.7);
            var core = new FlightCore(setup);
            var r = new Throw();
            for (int k = 0; k < 400; k++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                r.Peak = System.Math.Max(r.Peak, core.Trace.ContactForce.Length() * core.Body.InverseMass);
                r.Impulse += core.Trace.ContactForce.Z * FlightCore.Dt;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.ContactMade && e.Object >= 0 && e.Surface == WorldIds.NoSurface && e.Point.Z > Face + 1e-4) r.PastFace++;
                }
            }
            r.EndSpeed = core.State.Velocity.Z;
            return r;
        }

        // Model 85 (the known-wrong): a part found inside a thin skin past its middle (a blade back at rest, a piece a leg's break
        // moved) took the skin's far face, which pushed it on through: thrown into a 2 cm skin the drone took 59.483 N·s and left at
        // 3.554 m/s against the slab's 59.406 and 3.541, with 5 touches made on the far face (8 on a 1 cm skin; 15 on a 5 cm one,
        // 61.25 N·s and 3.871 m/s).
        [Test]
        public void AWallStopsADroneTheSameHoweverThinItsCollision()
        {
            Throw slab = Thrown(Walls[0].Skins);
            TestContext.Out.WriteLine($"{Walls[0].Name}: peak {slab.Peak:F1} m/s², impulse {slab.Impulse:F3} N·s, leaving at {slab.EndSpeed:F3} m/s");
            Assert.That(slab.Peak, Is.GreaterThan(1000), "the premise: a crash");
            for (int w = 1; w < Walls.Length; w++)
            {
                Throw t = Thrown(Walls[w].Skins);
                TestContext.Out.WriteLine($"{Walls[w].Name}: peak {t.Peak:F1} m/s², impulse {t.Impulse:F3} N·s, leaving at {t.EndSpeed:F3} m/s, "
                    + $"{t.PastFace} touches past the face, {t.Tunnels} tunnels");
                Assert.That(t.PastFace, Is.EqualTo(0), Walls[w].Name + ": nothing touches the skin past its face");
                Assert.That(t.Tunnels, Is.EqualTo(0), Walls[w].Name);
                Assert.That(t.Peak, Is.EqualTo(slab.Peak).Within(0.5).Percent, Walls[w].Name + ": the peak");
                Assert.That(t.Impulse, Is.EqualTo(slab.Impulse).Within(0.5).Percent, Walls[w].Name + ": the impulse");
                Assert.That(t.EndSpeed, Is.EqualTo(slab.EndSpeed).Within(0.01), Walls[w].Name + ": the speed it leaves at");
            }
        }

        // The armed drone flown at 8 m/s into each wall where A4's crash met R01_A's (x −3, from z −30): the strikes' passes.
        static List<PropStrikes.PassRecord> Flown((double, double)[] skins, out ContactScenarios.PoleStrike r)
        {
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong hash);
            DroneSpec spec = RepositoryDrone.Spec();
            FlightWorld world = FlightWorld.Floor();
            var site = new ContactScenarios.Site(world, new Double3(-3, 0, Face - 25.448), 0);
            var passes = new List<PropStrikes.PassRecord>();
            r = ContactScenarios.Into(spec, m, hash, site, 1.5, 8, core => { core.Strikes.Record = passes; return core.RuntimeIds[0]; },
                new[] { Wall(world, -3, skins) }, 3000, 0, null);
            return passes;
        }

        // Model 85 (the known-wrong): a skin whose far face lay inside the blade's tip was a rod inside the rim: once the disc was
        // through its 2 cm the carve swept a pole's disc across them (10.19 J where the slab's segment took 67.70 J), and at 8.7 m/s
        // from 30 m (WallTrace's flight) the leading edge's crush broke the blade in the first pass where the slab's carve jammed
        // the rotor. The peak: 2,704 m/s² against the slab's 5,069 (now 3,637: the stopped blades press 2 cm of timber, not 1 m).
        [Test]
        public void APropsTipMeetsAThinSkinAsAFace()
        {
            List<PropStrikes.PassRecord> slab = Flown(Walls[0].Skins, out ContactScenarios.PoleStrike rs);
            List<PropStrikes.PassRecord> skin = Flown(Walls[1].Skins, out ContactScenarios.PoleStrike rk);
            for (int k = 0; k < skin.Count && k < 6; k++)
                TestContext.Out.WriteLine($"pass {k}: slab {slab[k].Mode} at {slab[k].Radius:F4} m, {slab[k].Work:F2} J; skin {skin[k].Mode} at {skin[k].Radius:F4} m "
                    + $"(out at {skin[k].Exit:F4}), {skin[k].Work:F2} J");
            TestContext.Out.WriteLine($"slab: peak {rs.PeakAccel:F0} m/s²; skin: peak {rk.PeakAccel:F0} m/s² (the stopped blades pressed into 1 m of timber, or 2 cm)");
            Assert.That(skin.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(skin[0].Jammed, Is.True, "the first pass's carve stops the rotor at the skin's face, as at the slab's");
            for (int k = 0; k < 6; k++)
            {
                Assert.That(skin[k].Mode, Is.EqualTo(slab[k].Mode), $"pass {k}: the slab's mode");
                Assert.That(skin[k].Radius, Is.EqualTo(slab[k].Radius).Within(1e-6), $"pass {k}: at the slab's radius");
                Assert.That(skin[k].Work, Is.LessThanOrEqualTo(slab[k].Work * (1 + 1e-9)), $"pass {k}: no more work than the slab's (less timber behind)");
                Assert.That(skin[k].Work, Is.GreaterThan(0.5 * slab[k].Work), $"pass {k}");
            }
            Assert.That(rk.Tunnels + rs.Tunnels, Is.EqualTo(0));
        }

        struct CrownRun
        {
            public double FirstTouch, Work, Height, Away;
            public int Cuts, Glances;
            public long Allocated, Steps;
            public bool Crashed;
        }

        // The hills route's crown (3.1's route before e860cc0e, where AFlightOverTheHillsReplays stuck): IslandTree object 9 on the
        // farmland, the drone on its legs with the airframe's effects, from 30 m short of where it hung (341.9, 278.5) along the old
        // route's bearing, climbed to 5 m and flown at 6.85 m/s into the crown, the pilot holding that velocity for 25 s; with
        // surfaces.json's ground and twigs or without (the known-wrong: the twig shells glance). The first touch of the tree (s),
        // the twigs cut and their work (J), the glances on its shells, and at the end the height over the ground (m), the distance
        // from where it hung (m) and whether it crashed.
        static CrownRun Crown(bool table, System.IO.Stream log = null)
        {
            MapWorld w = SlopeScenarios.Farmland();
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong hash);
            DroneSpec spec = RepositoryDrone.Spec();
            var hung = new XZ(341.9, 278.5);
            var foot = new XZ(hung.X + 0.596 * 30, hung.Z + 0.803 * 30);
            var gs = new GroundSample[1];
            w.SampleGround(new[] { foot }, gs);
            var site = new ContactScenarios.Site(new FlightWorld(w), new Double3(foot.X, gs[0].Height, foot.Z), System.Math.Atan2(-0.596, -0.803)) { Airframe = true };
            Loadout legs = spec.Nominal;
            legs.Legs = true;
            FlightSetup setup = ContactScenarios.Setup(spec, m, hash, true, site.Foot + new Double3(0, ContactScenarios.Standing(spec.BuildContacts(legs, m, hash)), 0));
            if (table) setup.Contacts = spec.BuildContacts(legs, m, hash, SoilScenarios.Table());
            setup.World = site.World;
            setup.StartAttitude = site.Attitude();
            setup = site.Effects(setup, spec);
            // The crown alone: the field's stems off (w9-village-physics 2.12). With them on, the liftoff from the foot's grass cuts
            // its stalks, the flight into the crown differs, and the drone hangs in IslandTree's shells again (4.17 m up after 25 s,
            // 6 twigs cut): a finding for 2.3, handed back with 2.12, not hidden here.
            setup.Contacts.Stems = false;
            var core = new FlightCore(setup);
            var r = new CrownRun { FirstTouch = double.NaN };
            FlightRecorder recorder = null;
            if (log != null)
            {
                LogHeader header = LogHeader.For(setup, core);
                header.WorldId = "farmland";
                header.SetDrone(spec, legs);
                recorder = new FlightRecorder(log, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            }
            void Advance(StickSample stick, bool timed)
            {
                long before = AllocProbe.Before();
                core.Advance(stick);
                if (timed) r.Allocated += AllocProbe.After(before);
                recorder?.RecordStep(core.State, stick, core.Trace, core.Events);
            }
            for (int k = 0; k < 16; k++) Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 }, false); // CrosswindScenario.Arm's
            var pilot = new SimPilot(core.Controller, site.Foot + new Double3(0, 5, 0)) { Heading = site.Nose() };
            ushort twig = w.MaterialId("twig");
            for (int k = 0; k < 25000; k++)
            {
                if (k == 6000)
                {
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = site.Nose() * 6.85;
                }
                // The steps in the crown, from its first touch on: 0 B (the pilot's sticks outside them).
                Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs), !double.IsNaN(r.FirstTouch));
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (double.IsNaN(r.FirstTouch) && (e.Kind == EventKind.ContactMade || e.Kind == EventKind.PropStrike) && e.Material == twig) r.FirstTouch = k * FlightCore.Dt;
                    if (e.Kind == EventKind.Crash) r.Crashed = true;
                    if (e.Kind != EventKind.BladeStrike || e.Material != twig) continue;
                    if ((StrikeMode)(e.Index >> 4) == StrikeMode.Glance) r.Glances++;
                    else
                    {
                        r.Cuts++;
                        r.Work += e.A;
                    }
                }
            }
            ref readonly FlightState end = ref core.State;
            w.SampleGround(new[] { new XZ(end.Position.X, end.Position.Z) }, gs);
            r.Height = end.Position.Y - gs[0].Height;
            r.Away = System.Math.Sqrt((end.Position.X - hung.X) * (end.Position.X - hung.X) + (end.Position.Z - hung.Z) * (end.Position.Z - hung.Z));
            r.Steps = core.State.Step;
            recorder?.Close();
            return r;
        }

        // A drone in a crown (the hills runs: pushed into IslandTree's at 6.85 m/s it hung 241 s at 5 m over the ground with no fall
        // and no crash, 88,699 blade strikes taking 0.00 J each). With its twigs cut at their cost (PropStrikes.Twigs) the props pay
        // for each and the drone does not stay: it leaves the crown, crashed or fallen; the known-wrong, the shells glancing, hangs.
        [Test]
        public void ADroneInACrownPaysForItsTwigsAndDoesNotHang()
        {
            var log = new System.IO.MemoryStream();
            CrownRun cut = Crown(true, log), wrong = Crown(false);
            foreach ((string name, CrownRun r) in new[] { ("twigs cut", cut), ("the known-wrong, the shells glancing", wrong) })
                TestContext.Out.WriteLine($"{name}: first touch at {r.FirstTouch:F2} s; {r.Cuts} twigs cut for {r.Work:F1} J, {r.Glances} glances; at 25 s "
                    + $"{r.Height:F2} m up, {r.Away:F2} m from where it hung, {(r.Crashed ? "crashed" : "no crash")}");
            Assert.That(cut.FirstTouch, Is.LessThan(15), "the premise: the crown is met");
            Assert.That(cut.Cuts, Is.GreaterThan(0), "the blades cut twigs");
            Assert.That(cut.Work / cut.Cuts, Is.GreaterThan(1), "each twig costs its rotor joules, not nothing");
            Assert.That(cut.Glances, Is.EqualTo(0), "a twig shell never glances");
            Assert.That(cut.Crashed || cut.Height < 1 || cut.Away > 5, Is.True, "the drone does not hang in the crown");
            Assert.That(wrong.Glances, Is.GreaterThan(1000), "the known-wrong's shells glance");
            Assert.That(!wrong.Crashed && wrong.Height > 3 && wrong.Away < 3, Is.True, "the known-wrong hangs in the crown");
            // The twigs' count is the flight's own state (no draw): the log replays headless bit for bit, and a step allocates nothing.
            Assert.That(cut.Allocated, Is.EqualTo(0), "0 B in the crown's steps" + AllocProbe.Report());
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong hash);
            var world = new FlightWorld(SlopeScenarios.Farmland());
            ReplayResult replay = FlightReplay.Run(new System.IO.MemoryStream(log.ToArray()), id => id == "farmland" ? world : null, RepositoryDrone.Spec().ContentHash,
                null, SoilScenarios.Table().Hash, materialsHash: hash);
            TestContext.Out.WriteLine($"replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(cut.Steps));
        }

        struct HoverRun
        {
            public int Breaks, ZeroMass, Repeats, Cuts, MostInStep, Opening, Lost;
            public double Work;
            public long Allocated, Steps;
        }

        // w9-village-physics 2.7 (audit §1.15, G5): the audit's 35° spot on the farmland, (525.79, 612.74), lies inside a
        // GreenBush_Dry crown. The drone starts there 1.24 m over the ground and the pilot holds that point for seconds, with
        // surfaces.json's ground and twigs: the breaks (BladeDamaged, mode Break), those that took no mass, those that repeat a
        // station a blade already broke at, the twigs cut and their work, the most BladeDamaged in one step, and the events the
        // step's buffer refused: in the first step (every pair in the crown made at once) and after it.
        static HoverRun CrownHover(int seconds, System.IO.Stream log = null)
        {
            MapWorld w = SlopeScenarios.Farmland();
            DroneSpec spec = RepositoryDrone.Spec();
            var spot = new XZ(525.79, 612.74);
            var gs = new GroundSample[1];
            w.SampleGround(new[] { spot }, gs);
            var at = new Double3(spot.X, gs[0].Height + 1.24, spot.Z);
            var world = new FlightWorld(w);
            FlightSetup setup = SoilScenarios.Setup(spec, world, at);
            var core = new FlightCore(setup);
            FlightRecorder recorder = null;
            if (log != null)
            {
                LogHeader header = LogHeader.For(setup, core);
                header.WorldId = "farmland";
                header.SetDrone(spec, spec.Nominal);
                recorder = new FlightRecorder(log, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            }
            ushort twig = w.MaterialId("twig");
            var r = new HoverRun();
            var broken = new HashSet<(int, int, double)>();
            var pilot = new SimPilot(core.Controller, at);
            for (int k = 0; k < 16 + 1000 * seconds; k++)
            {
                StickSample stick = k < 16 ? new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 } : pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                long before = AllocProbe.Before();
                core.Advance(stick);
                if (k >= 1016) r.Allocated += AllocProbe.After(before);
                recorder?.RecordStep(core.State, stick, core.Trace, core.Events);
                if (k == 0) r.Opening = core.Events.Lost;
                else r.Lost += core.Events.Lost;
                int damaged = 0;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.BladeStrike && e.Material == twig && (StrikeMode)(e.Index >> 4) != StrikeMode.Glance)
                    {
                        r.Cuts++;
                        r.Work += e.A;
                    }
                    if (e.Kind != EventKind.BladeDamaged) continue;
                    damaged++;
                    if ((StrikeMode)(e.Index >> 4) != StrikeMode.Break) continue;
                    r.Breaks++;
                    if (!(e.B > 0)) r.ZeroMass++;
                    if (!broken.Add((e.Part, e.Index & 15, e.C))) r.Repeats++;
                }
                r.MostInStep = System.Math.Max(r.MostInStep, damaged);
            }
            r.Steps = core.State.Step;
            recorder?.Close();
            return r;
        }

        // A hover inside a bush crown (G5): at model 85 every blade of all four props logged a Break at 0.9888 R taking 0 kg on
        // every step, 200,000 events in 25 s: its props stand still in the shells, and each resting blade's load radius was its
        // pairs' points on the shells' planes, 4.5–5.5 R, so the station at its tip "broke" again (2.7). A break takes mass and
        // happens at most once at any station of a blade.
        [Test]
        public void AHoverInACrownBreaksNoBladeTwice()
        {
            var log = new System.IO.MemoryStream();
            HoverRun r = CrownHover(25, log);
            TestContext.Out.WriteLine($"25 s in GreenBush_Dry's crown: {r.Cuts} twigs cut for {r.Work:F1} J; {r.Breaks} breaks, {r.ZeroMass} with 0 kg lost, "
                + $"{r.Repeats} at a station already broken; at most {r.MostInStep} BladeDamaged in a step; events refused {r.Opening} in the first step, "
                + $"{r.Lost} after; {r.Allocated} B after the first second");
            Assert.That(r.ZeroMass, Is.EqualTo(0), "no break takes 0 kg");
            Assert.That(r.Repeats, Is.EqualTo(0), "a blade breaks at most once at any station");
            Assert.That(r.Lost, Is.EqualTo(0), "the step's buffer refuses no event after the first step");
            Assert.That(r.Allocated, Is.EqualTo(0), "0 B in steady state" + AllocProbe.Report());
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong hash);
            var world = new FlightWorld(SlopeScenarios.Farmland());
            ReplayResult replay = FlightReplay.Run(new System.IO.MemoryStream(log.ToArray()), id => id == "farmland" ? world : null, RepositoryDrone.Spec().ContentHash,
                null, SoilScenarios.Table().Hash, materialsHash: hash);
            TestContext.Out.WriteLine($"replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(r.Steps));
        }

        // Model 85 (the known-wrong): without surfaces.json a blade's passes glanced through the floor (no face to them) while a
        // blade at rest bore on it as rigid: A4's crash into R01_A had a rotor's disc 50 mm into the floor before its bell stopped it,
        // and its blades came back to rest up to 85 mm inside (2.6). The tipped landing on the stub's floor: 16 touches of the floor
        // by resting blades, which broke them (the shortest 0 mm). Now a resting blade passes it as its passes do, and the floor
        // stays the strikes' glance (the known-wrong of StrikeTests' soil, which surfaces.json's ground makes physical).
        [Test]
        public void WithoutSurfacesABladeAtRestPassesTheFloorAsItsPassesDo()
        {
            int touches = 0;
            StrikeScenarios.TipResult r = StrikeScenarios.TippedLanding(RepositoryDrone.Spec(), FlightWorld.Floor(), default, RainLevel.Sunny, null, false,
                stepped: (core, stick) =>
                {
                    for (int i = 0; i < core.Events.Count; i++)
                    {
                        FlightEvent e = core.Events[i];
                        if (e.Kind == EventKind.ContactMade && e.Object == WorldIds.Ground && core.Contacts.Spec.Shapes[e.Index].Role == ContactRole.Blade) touches++;
                    }
                });
            TestContext.Out.WriteLine(r.Describe() + $"; {touches} touches of the floor by resting blades");
            Assert.That(r.Glances, Is.GreaterThan(0), "the premise: the discs meet the floor, and their passes glance");
            Assert.That(r.Passes, Is.EqualTo(0));
            Assert.That(touches, Is.EqualTo(0), "no resting blade bears on the floor its passes glance through");
            Assert.That(r.Tunnels, Is.EqualTo(0));
        }
    }
}
