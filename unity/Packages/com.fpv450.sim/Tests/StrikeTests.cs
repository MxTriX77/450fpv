using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7f-damage 1.3 (design 4–6, prop-motor-damage): blade strikes on shapes and the crash rule. Each scenario names the
    // known-wrong model it fails.
    public sealed class StrikeTests
    {
        const double R = 0.127, H = 0.004;
        static readonly Double3 Up = new Double3(0, 1, 0);
        static readonly Double3 South = new Double3(0, 0, -1);

        static DroneSpec Spec => RepositoryDrone.Spec();

        static StrikeScenarios.Rig Rig(Double3 at, Quat attitude, params ShapeGeometry[] shapes) =>
            new StrikeScenarios.Rig(StrikeScenarios.Setup(Spec, at, attitude, shapes.Length > 0 ? StrikeScenarios.One(shapes[0]) : null),
                StrikeScenarios.HoverDuty(Spec));

        // A face of a material in the sky: a 1 m box whose south face is at p.
        static RuntimeObject[] FaceOf(string material, out Double3 p)
        {
            p = StrikeScenarios.Sky + new Double3(0, 0, 1);
            return StrikeScenarios.One(StrikeScenarios.Box(material, p + new Double3(0, 0, 0.5), new Double3(0.5, 0.5, 0.5)));
        }

        static List<PropStrikes.PassRecord> Passes(StrikeScenarios.Rig rig) => rig.Core.Strikes.Record;

        // A carve's speed through the face over the blade's own and its frontal height there (p7f-damage 1.3's tip slide), from
        // the record's body velocity at the centroid and the blade's motion and the rig's axis: u = (ω·r_c + v·motion, v·axis),
        // α = θ − atan2(u_a, u_t), h = c·|sin α| + t·|cos α|.
        static void Relative(in PropStrikes.PassRecord r, Double3 axis, double rc, double chord, double thick, double theta, out double ratio, out double height)
        {
            double ut = r.SpeedBefore * rc + Double3.Dot(r.Body, r.Motion), ua = Double3.Dot(r.Body, axis);
            ratio = Math.Sqrt(ut * ut + ua * ua) / (r.SpeedBefore * rc);
            double a = theta - Math.Atan2(ua, ut);
            height = chord * Math.Abs(Math.Sin(a)) + thick * Math.Abs(Math.Cos(a));
        }

        // Heading 45° (fl due north) and tilted 30° about the body's horizontal axis across fl's arm, so fl's disc dips lowest.
        static Quat Rolled()
        {
            double s = Math.Sin(15 * StrikeScenarios.DegToRad) * 0.7071067811865476, c = Math.Cos(15 * StrikeScenarios.DegToRad);
            return (StrikeScenarios.Heading(StrikeScenarios.LeadHeading) * new Quat(s, 0, s, c)).Normalized();
        }

        // ---- Where a blade meets a shape ------------------------------------------------------------------------------------

        // "The meeting radius is exact": a level disc at the origin, each shape on a body held still; the closed forms.
        [Test]
        public void TheMeetingRadiusIsExact()
        {
            Double3 hub = default, axis = Up;
            var none = ReadOnlySpan<Double3>.Empty;
            foreach (double d in new[] { 0.001, 0.005, 0.020 })
            {
                ShapeGeometry wall = StrikeScenarios.Box("concrete", new Double3(R - d + 0.5, 0, 0.03), new Double3(0.5, 1, 1));
                Assert.That(BladeCut.Nearest(wall, none, hub, axis, H, R, out BladeCut.Meeting m), Is.True);
                TestContext.Out.WriteLine($"box face {d * 1e3} mm inside the rim: r_m {m.Radius * 1e3:F9} mm (closed form {(R - d) * 1e3:F9})");
                Assert.That(m.Radius, Is.EqualTo(R - d).Within(1e-6));
                Assert.That(m.Direction.X, Is.EqualTo(1).Within(1e-12));
                Assert.That(m.Normal.X, Is.EqualTo(-1).Within(1e-12));
            }
            // A 20 mm rod 60 mm inside the rim (its near side at R − 0.06), standing through the disc.
            ShapeGeometry rod = StrikeScenarios.Rod("steel", new Double3(0.6, 0, 0.8) * (R - 0.06 + 0.01), 0.01, 0.5);
            Assert.That(BladeCut.Nearest(rod, none, hub, axis, H, R, out BladeCut.Meeting rm), Is.True);
            Assert.That(rm.Radius, Is.EqualTo(R - 0.06).Within(1e-6), "the rod");
            // A sphere above the disc plane, off the slab at the hub: the slab's cut, a disc of √(ρ² − (y − h)²) about its foot.
            ShapeGeometry ball = new ShapeGeometry { Kind = ShapeKind.Sphere, Center = new Double3(R, 0.02, 0), Radius = 0.05, Material = 1 };
            Assert.That(BladeCut.Nearest(ball, none, hub, axis, H, R, out BladeCut.Meeting bm), Is.True);
            double cut = Math.Sqrt(0.05 * 0.05 - (0.02 - H) * (0.02 - H));
            TestContext.Out.WriteLine($"sphere: r_m {bm.Radius * 1e3:F9} mm (closed form {(R - cut) * 1e3:F9}), along its closest point");
            Assert.That(bm.Radius, Is.EqualTo(R - cut).Within(1e-6), "the sphere, off the slab at the hub");
            // A vertical cylinder of 30 mm radius whose axis is 0.11 m out.
            ShapeGeometry cyl = new ShapeGeometry
            {
                Kind = ShapeKind.Cylinder, Center = new Double3(0, 0.1, -0.11), AxisX = new Double3(1, 0, 0), AxisY = Up, AxisZ = new Double3(0, 0, 1),
                Radius = 0.03, HalfLength = 0.2, Material = 1,
            };
            Assert.That(BladeCut.Nearest(cyl, none, hub, axis, H, R, out BladeCut.Meeting cm), Is.True);
            Assert.That(cm.Radius, Is.EqualTo(0.08).Within(1e-6), "the cylinder");
            // The plane under a disc tilted 30°, the hub 60 mm over it: (h_hub − h·cos 30°)/sin 30°.
            Double3 tilted = new Double3(Math.Sin(30 * StrikeScenarios.DegToRad), Math.Cos(30 * StrikeScenarios.DegToRad), 0);
            ShapeGeometry ground = new ShapeGeometry { Kind = ShapeKind.Plane, Center = new Double3(0, -0.06, 0), AxisY = Up };
            Assert.That(BladeCut.Nearest(ground, none, hub, tilted, H, R, out BladeCut.Meeting gm), Is.True);
            double plane = (0.06 - H * Math.Cos(30 * StrikeScenarios.DegToRad)) / Math.Sin(30 * StrikeScenarios.DegToRad);
            TestContext.Out.WriteLine($"plane under a disc tilted 30°: r_m {gm.Radius * 1e3:F9} mm (closed form {plane * 1e3:F9})");
            Assert.That(gm.Radius, Is.EqualTo(plane).Within(1e-6));
            // A conductor: L03's third conductor at its mid-span (the chain's lowest point), the hub level with it 0.1 m across
            // the chord: the conductor's near side, 0.1 m less its radius.
            MapWorld w = RangeScenarios.World;
            int line = RangeScenarios.Placed("L03_medium", 1);
            Assert.That(w.Geometry(line, 2, out ShapeGeometry wire), Is.True);
            ReadOnlySpan<Double3> chain = w.WirePoints(line, 2);
            Double3 mid = chain[chain.Length / 2], chord = wire.AxisY, across = Double3.Cross(Up, chord);
            across = across * (1 / across.Length());
            Double3 at = mid + across * 0.1;
            BladeCut.WireWindow(chain, wire.Radius, at, R + H, out int lo, out int hi);
            Assert.That(BladeCut.Nearest(wire, chain.Slice(lo, hi - lo + 2), at, Up, H, R, out BladeCut.Meeting wm), Is.True);
            TestContext.Out.WriteLine($"conductor (r {wire.Radius * 1e3:F2} mm): r_m {wm.Radius * 1e3:F9} mm (closed form {(0.1 - wire.Radius) * 1e3:F9})");
            Assert.That(wm.Radius, Is.EqualTo(0.1 - wire.Radius).Within(1e-6));
        }

        // "Each blade meets the shape once per pass", and the rod once inside the rim is still met on every pass: a straw rod
        // (a thin compliant row: Glance passes, no damage, so the blades stay whole) drifted in through the rim to 60 mm inside,
        // held 2000 steps at 9000 rpm. The known-wrong, the ring's pairs alone, finds nothing there.
        [Test]
        public void EachBladeMeetsAShapeOncePerPassAndARodInsideTheRimStaysMet()
        {
            DroneSpec spec = Spec;
            Double3 hub = spec.Motors[1].Position, origin = StrikeScenarios.Sky;
            Double3 rodAt = origin + hub + new Double3(R + 0.010, 0, 0); // its near side 20 mm out at the start, 60 mm in at the end
            StrikeScenarios.Rig rig = Rig(origin + new Double3(-0.02, 0, 0), Quat.Identity, StrikeScenarios.Rod("straw", rodAt, 0.01, 0.3));
            for (int k = 1; k <= 80; k++) rig.Step(origin + new Double3(-0.02 + 0.08 * k / 80, 0, 0), default);
            rig.Log.Clear();
            int steps = 2000;
            rig.Hold(origin + new Double3(0.06, 0, 0), steps);
            double turns = StrikeScenarios.HoverSpeed * steps * 0.001 / (2 * Math.PI);
            var passes = new int[2];
            foreach (FlightEvent e in rig.Log)
                if (e.Kind == EventKind.BladeStrike && e.Part == spec.PartIndex("prop_fr")) passes[e.Index & 15]++;
            TestContext.Out.WriteLine($"{turns:F2} turns: blade 0 {passes[0]} passes, blade 1 {passes[1]}; rotor at {rig.Now.RotorSpeed[1]:F1} rad/s");
            // Once a turn each, ± the window's ends: never twice in a pass.
            Assert.That(passes[0], Is.InRange((int)turns - 1, (int)turns + 1));
            Assert.That(passes[1], Is.InRange((int)turns - 1, (int)turns + 1));
            // The known-wrong: the rim's ring of capsules no longer touches the rod 60 mm inside.
            var buf = new StaticContact[8];
            ContactShape disc = Array.Find(rig.Core.Contacts.Spec.Shapes, s => s.Name == "disc_fr");
            int found = 0;
            for (int s = 0; s < 12; s++)
            {
                double a0 = 2 * Math.PI * s / 12, a1 = 2 * Math.PI * (s + 1) / 12;
                Double3 p = rig.Now.Position + disc.A + new Double3(Math.Cos(a0), 0, Math.Sin(a0)) * R;
                Double3 q = rig.Now.Position + disc.A + new Double3(Math.Cos(a1), 0, Math.Sin(a1)) * R;
                found += rig.Core.World.StaticContacts(new Capsule(p, q, H), new Capsule(p, q, H), 0, buf);
            }
            Assert.That(found, Is.EqualTo(0), "the ring alone would have lost the rod inside the rim");
        }

        // "A runtime object is met where it is": the launch stand's post where the stand stands, moved 0.37 m and turned 30°.
        [Test]
        public void ARuntimeObjectIsMetWhereItIs()
        {
            DroneSpec spec = Spec;
            ushort steel = RangeScenarios.World.MaterialId(spec.Stand.Material);
            Double3 foot = StrikeScenarios.Sky + new Double3(0.37, -1, 0.1);
            RuntimeObject stand = LaunchStand.Build(spec, foot, 30 * StrikeScenarios.DegToRad, steel);
            ShapeGeometry post = stand.Shapes[1];
            // The front-right disc 2 mm into the post's +x face just under its top (a level disc).
            Double3 face = post.Center + post.AxisX * post.HalfExtents.X + Up * (post.HalfExtents.Y - 0.002);
            Double3 hub = spec.Motors[1].Position;
            Double3 body = face - hub + post.AxisX * (R - 0.002);
            var moved = new StrikeScenarios.Rig(StrikeScenarios.Setup(spec, body + post.AxisX * 0.01, Quat.Identity, new[] { stand }), StrikeScenarios.HoverDuty(spec));
            for (int k = 1; k <= 10; k++) moved.Step(body + post.AxisX * (0.01 - 0.001 * k), default);
            moved.Hold(body, 20);
            FlightEvent first = moved.Log.Find(e => e.Kind == EventKind.BladeStrike);
            Assert.That(first.Kind, Is.EqualTo(EventKind.BladeStrike), "the disc met the post");
            Assert.That(first.Object, Is.EqualTo(moved.Core.RuntimeIds[0]));
            Assert.That(first.Shape, Is.EqualTo(1));
            double off = Double3.Dot(first.Point - face, post.AxisX);
            TestContext.Out.WriteLine($"met the moved post at ({first.Point.X:F6}, {first.Point.Y:F6}, {first.Point.Z:F6}); {off * 1e6:F3} µm off its face");
            Assert.That(Math.Abs(off), Is.LessThan(1e-6), "on the post's face where the stand is");
            // The stand where it was (unmoved): the same drone pose meets nothing.
            RuntimeObject home = LaunchStand.Build(spec, StrikeScenarios.Sky + new Double3(0, -1, 0), 0, steel);
            var still = new StrikeScenarios.Rig(StrikeScenarios.Setup(spec, body + post.AxisX * 0.01, Quat.Identity, new[] { home }), StrikeScenarios.HoverDuty(spec));
            for (int k = 1; k <= 10; k++) still.Step(body + post.AxisX * (0.01 - 0.001 * k), default);
            still.Hold(body, 20);
            Assert.That(still.Count(EventKind.BladeStrike), Is.EqualTo(0));
        }

        // ---- Who yields, and the hard target's modes --------------------------------------------------------------------------

        // "Grazing a wall trims the tip and pushes the drone off": the lead disc drifts at 0.3 m/s into a hard face until its rim
        // is 1 mm in, the disc level, then holds. On steel (μ 0.28) the blades are crushed back to the face pass by pass, each to
        // within 1 µm of it. On H01_D's south wall (masonry, μ 0.55) the friction's edgewise moment, μ·σ_c·A at the tip, passes
        // the tip's edgewise strength 6–10 mm in before the crush is done: each blade chips there on its first touch (design 5's
        // break check; the design's trim did not take the masonry's friction in). The rotor dips by √(ω² − 2E/J) and the face
        // pushes the drone off on both. The known-wrong, model 29's strike that carries no force, pushes nothing.
        [TestCase("steel")]
        [TestCase("H01_D")]
        public void GrazingAWallTrimsTheTipAndPushesTheDroneOff(string wall)
        {
            DroneSpec spec = Spec;
            Double3 face, n = South;
            RuntimeObject[] objects = null;
            double y;
            if (wall == "H01_D")
            {
                StrikeScenarios.SouthWall(out face, out n, out double lo, out double hi);
                y = Math.Min(hi - 0.5, lo + 1.5);
            }
            else
            {
                objects = FaceOf(wall, out face);
                y = face.Y;
            }
            Quat att = StrikeScenarios.Heading(StrikeScenarios.LeadHeading);
            StrikeScenarios.Rig rig = StrikeScenarios.Into(spec, objects, face, n, att, 0.001, y, 0.3, 300);
            double worstDip = 0, worstLength = 0, push = 0;
            int crush = 0, breaks = 0;
            foreach (PropStrikes.PassRecord r in Passes(rig))
            {
                double dip = r.SpeedAfter > 0 ? Math.Abs(r.SpeedAfter - Math.Sqrt(r.SpeedBefore * r.SpeedBefore - 2 * r.Work / r.Inertia)) / r.SpeedAfter : 0;
                worstDip = Math.Max(worstDip, dip);
                if (r.Mode == StrikeMode.Crush && !r.Jammed)
                {
                    crush++;
                    worstLength = Math.Max(worstLength, Math.Abs(r.TipAfter * R - r.Radius));
                }
                if (r.Mode == StrikeMode.Break) breaks++;
                push += Double3.Dot(r.Impulse, n) * rig.Core.Body.InverseMass;
                TestContext.Out.WriteLine(StrikeScenarios.Line(r));
            }
            TestContext.Out.WriteLine($"{wall}: {crush} crushes, {breaks} breaks; the face's push along its normal {push:F5} m/s in all; the dip's worst error "
                + $"{worstDip:E2}, a crush's length {worstLength * 1e6:F4} µm off the face; blades now {rig.Core.Propulsion.Rotor(3).Blade(0).TipRadius * R * 1e3:F3} and "
                + $"{rig.Core.Propulsion.Rotor(3).Blade(1).TipRadius * R * 1e3:F3} mm");
            if (wall == "steel")
            {
                Assert.That(crush, Is.GreaterThan(0), "crushed back to the face");
                Assert.That(breaks, Is.EqualTo(0));
                Assert.That(worstLength, Is.LessThan(1e-6), "a crush leaves the tip at the face");
            }
            else Assert.That(breaks, Is.GreaterThan(0), "masonry's friction chips the tips");
            Assert.That(worstDip, Is.LessThan(1e-9), "ω' = √(ω² − 2E/J)");
            Assert.That(push, Is.GreaterThan(0), "the face's push moves the drone away from the wall");
        }

        // "A tilted disc slaps rather than crushes": the same drift with the drone pitched 20° toward the wall, and a concrete
        // slab under a disc rolled 30°: the blades bend over the face (no crush), every break or set at its station with its peak
        // moment, the friction's work and the worn volume per pass. The known-wrong, the crush whatever the tilt (bend never
        // chosen), crushes on every pass.
        [Test]
        public void ATiltedDiscSlapsRatherThanCrushes()
        {
            DroneSpec spec = Spec;
            StrikeScenarios.SouthWall(out Double3 face, out Double3 n, out double lo, out double hi);
            Quat pitched = StrikeScenarios.Heading(0, 20 * StrikeScenarios.DegToRad);
            StrikeScenarios.Rig wall = StrikeScenarios.Into(spec, null, face, n, pitched, 0.001, Math.Min(hi - 0.5, lo + 1.5), 0.3, 300);
            Double3 top = StrikeScenarios.Sky + new Double3(0, -0.5, 0);
            RuntimeObject[] slab = StrikeScenarios.One(StrikeScenarios.Box("concrete", top + new Double3(0, -0.25, 0), new Double3(1, 0.25, 1)));
            StrikeScenarios.Rig ground = StrikeScenarios.Into(spec, slab, top, Up, Rolled(), 0.001, top.Y, 0.3, 300);
            foreach (var (name, rig) in new[] { ("wall pitched 20°", wall), ("slab under 30°", ground) })
            {
                int crush = 0, bent = 0;
                double friction = 0, worn = 0;
                foreach (PropStrikes.PassRecord r in Passes(rig))
                {
                    if (r.Mode == StrikeMode.Glance) continue;
                    TestContext.Out.WriteLine(name + ": " + StrikeScenarios.Line(r));
                    if (r.Mode == StrikeMode.Crush) crush++;
                    else bent++;
                    friction += r.Friction;
                    worn += r.Wear;
                    Assert.That(r.BendForce, Is.LessThan(r.CrushForce), "bending clears it for less than a crush");
                }
                int sets = 0;
                foreach (FlightEvent e in rig.Log)
                    if (e.Kind == EventKind.BladeDamaged && (StrikeMode)(e.Index >> 4) != StrikeMode.Wear) sets++;
                TestContext.Out.WriteLine($"{name}: {bent} bends, {crush} crushes, {sets} breaks or sets; friction {friction:F4} J, worn {worn * 1e9:F4} mm³");
                Assert.That(bent, Is.GreaterThan(0));
                Assert.That(crush, Is.EqualTo(0), "no crush on a tilted face");
            }
        }

        // w9-village-physics 2.6's addendum: a thin branch met along the disc's axis faster than its own thickness and the slab's
        // (2h = 8 mm) a step is in neither the step's start slab nor its end's. A 10 mm bark branch across the fr disc 80 mm out,
        // the rig driving the drone up through it at 20 m/s, one step a crossing: the slab holds the branch for (10 + 8)/20 = 0.9
        // of the step (0.8 on the slab's samples at most h apart, BladeCut.Crossing), and each blade meets it when its azimuth passes
        // the branch's within that share: by hand 2 · 942 rad/s · 1 ms · 0.9 / 2π = 0.27 of the crossings. The known-wrong (the slab
        // at the step's ends only, model 93's) meets it on none.
        [Test]
        public void ABranchCrossedWithinAStepIsMet()
        {
            DroneSpec spec = Spec;
            const double V = 20, D = 0.010;
            const int Crossings = 120;
            Double3 hub = spec.Motors[1].Position, origin = StrikeScenarios.Sky;
            var branch = new ShapeGeometry
            {
                Kind = ShapeKind.Capsule, Center = origin + hub + new Double3(0.08, 0, 0), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 0, 1),
                AxisZ = new Double3(0, -1, 0), Radius = D / 2, HalfLength = 0.12, Material = RangeScenarios.World.MaterialId("bark"),
            };
            Double3 below = origin + new Double3(0, -V * FlightCore.Dt / 2, 0), above = origin + new Double3(0, V * FlightCore.Dt / 2, 0);
            int met = 0, atEnds = 0, tunnels = 0, lost = 0;
            foreach (Double3 end in new[] { below, above })
                if (BladeCut.Nearest(branch, default, end + hub, new Double3(0, 1, 0), H, R, out _)) atEnds++;
            for (int k = 0; k < Crossings; k++)
            {
                // A fresh rig each crossing (a pass may break a blade), held 1 + k steps first so the rotor's phase at the crossing
                // varies.
                var rig = Rig(below, Quat.Identity, branch);
                rig.Core.Strikes.Record = new List<PropStrikes.PassRecord>();
                rig.Hold(below, 1 + k);
                rig.Core.Strikes.Record.Clear();
                rig.Step(above, new Double3(0, V, 0));
                foreach (PropStrikes.PassRecord r in rig.Core.Strikes.Record)
                    if (r.Corner == 1)
                    {
                        met++;
                        break;
                    }
                tunnels += rig.Count(EventKind.Tunnel);
                lost += rig.Count(EventKind.ContactsLost);
            }
            double share = met / (double)Crossings, hand = 2 * StrikeScenarios.HoverSpeed * FlightCore.Dt * ((D + 2 * H) / (V * FlightCore.Dt)) / (2 * Math.PI);
            TestContext.Out.WriteLine($"{met} of {Crossings} crossings met ({share:F3}; by hand {hand:F3}); the slab at the step's ends holds it {atEnds} times; "
                + $"{tunnels} tunnels");
            Assert.That(atEnds, Is.Zero, "the known-wrong: the branch is in neither end's slab");
            Assert.That(share, Is.EqualTo(hand).Within(0.4 * hand), "the blades meet it on their share of the crossings");
            Assert.That(tunnels + lost, Is.Zero);
        }

        // "A head-on rod breaks the blade": a 12 mm steel rod 40 mm inside the rim of a disc at 9000 rpm. The blade breaks at a
        // station inside the rod's radius, the fragment's mass leaves the rotor (its J falls at the rebuild); the known-wrong that
        // crushes instead of breaking loses the rod's depth only, so it fails the break.
        [Test]
        public void AHeadOnRodBreaksTheBlade()
        {
            DroneSpec spec = Spec;
            StrikeScenarios.HeadOn(spec, 0x450F, out StrikeScenarios.Rig rig, out PropStrikes.PassRecord hit, out double lost, out double jBefore);
            TestContext.Out.WriteLine(StrikeScenarios.Line(hit));
            double rodNear = R - 0.040;
            TestContext.Out.WriteLine($"the break at {hit.TipAfter * R * 1e3:F2} mm (the rod's near side at {rodNear * 1e3:F2}); {lost * 1e3:F4} g lost, its energy "
                + $"½ΔJω² {0.5 * (jBefore - rig.Core.Propulsion.Inertia(1)) * hit.SpeedAfter * hit.SpeedAfter:F3} J; J {jBefore:E4} → {rig.Core.Propulsion.Inertia(1):E4}");
            Assert.That(hit.Mode, Is.EqualTo(StrikeMode.Break));
            Assert.That(hit.TipAfter * R, Is.LessThan(rodNear), "it breaks inside the rod's radius");
            Assert.That(lost, Is.GreaterThan(0));
            Assert.That(rig.Core.Propulsion.Inertia(1), Is.LessThan(jBefore), "the fragment's share of J leaves the rotor");
            Assert.That(R - hit.TipAfter * R, Is.GreaterThan(R - rodNear), "the known-wrong crush loses the rod's depth only");
        }

        // ---- Soft targets and the ground ---------------------------------------------------------------------------------------

        // "A prop in a trunk sticks a bit": the lead disc pushed 1, 5 and 20 mm into V01_L's trunk (bark) and L01_straight's pole
        // (timber) at hover speed, the motor at the hover's duty. At 1 mm each pass costs the carve's energy and the rotor recovers
        // between passes; at 20 mm the rotor stops on the wood (RotorJammed) and turns again as the motor drives it; a break only
        // where the cutting force passes a section's strength. The known-wrong, no work in a carve, never dips or stops.
        [TestCase("V01_L", "bark")]
        [TestCase("L01_straight", "timber")]
        public void APropInATrunkSticksABit(string variant, string material)
        {
            DroneSpec spec = Spec;
            Quat att = StrikeScenarios.Heading(StrikeScenarios.LeadHeading);
            StrikeScenarios.Trunk(variant, material, 1.5, out Double3 p, out Double3 n, out double radius);
            foreach (double depth in new[] { 0.001, 0.005, 0.020 })
            {
                StrikeScenarios.Rig rig = StrikeScenarios.Into(spec, null, p, n, att, depth, p.Y, 0.3, 1500);
                List<PropStrikes.PassRecord> passes = Passes(rig).FindAll(r => r.Mode != StrikeMode.Glance);
                int jams = rig.Count(EventKind.RotorJammed), breaks = passes.FindAll(r => r.Mode == StrikeMode.Break).Count;
                double lowest = double.PositiveInfinity, work = 0;
                foreach (PropStrikes.PassRecord r in passes)
                {
                    lowest = Math.Min(lowest, r.SpeedBefore);
                    work = Math.Max(work, r.Work);
                }
                TestContext.Out.WriteLine($"{variant} ({material}, r {radius * 1e3:F1} mm) {depth * 1e3} mm: {passes.Count} passes, {jams} jams, {breaks} breaks; "
                    + $"the most work {work:F3} J; the lowest speed before a pass {lowest:F1} rad/s; at the end {rig.Now.RotorSpeed[3]:F1} rad/s");
                for (int i = 0; i < passes.Count && i < (depth == 0.001 ? 30 : 6); i++) TestContext.Out.WriteLine("  " + StrikeScenarios.Line(passes[i]));
                Assert.That(passes.Count, Is.GreaterThan(0));
                Assert.That(passes.TrueForAll(r => r.Mode == StrikeMode.Carve || r.Mode == StrikeMode.Break), Is.True, "wood is carved");
                if (depth == 0.001)
                {
                    Assert.That(jams, Is.EqualTo(0));
                    Assert.That(passes.TrueForAll(r => r.Work > 0), Is.True, "each pass costs the carve's energy");
                    // Between passes the motor speeds it up again: each pass starts faster than the last one left it (at the fixed
                    // duty of the rig the speed settles where the motor's torque meets the carve's mean drag).
                    for (int i = 1; i < passes.Count; i++)
                        Assert.That(passes[i].SpeedBefore, Is.GreaterThan(passes[i - 1].SpeedAfter), "the rotor recovers between passes");
                }
                if (depth == 0.020)
                {
                    Assert.That(jams, Is.GreaterThan(0), "the rotor stops on the wood");
                    int after = passes.FindIndex(r => r.Jammed);
                    Assert.That(passes.Count, Is.GreaterThan(after + 1), "and turns again as the motor drives it");
                }
            }
        }

        // "Props that touch the ground bog down", against the materials table's soil-like row (sandbag: dry sand's c·N_c and
        // density): 2 and 10 mm into its top under a disc rolled 30°. Each pass's work is p·V + m_f·v² within 1e-9 of the closed
        // form, the flung mass dominates, the 10 mm dig stops the rotor within its first two passes. The ground's own soil (the
        // dirt track, S04's) and its sand's wear: PropsThatTouchTheGroundBogDown.
        [Test]
        public void PropsThatTouchSoilBogDown()
        {
            DroneSpec spec = Spec;
            Double3 top = StrikeScenarios.Sky + new Double3(0, -0.5, 0);
            RuntimeObject[] soil = StrikeScenarios.One(StrikeScenarios.Box("sandbag", top + new Double3(0, -0.25, 0), new Double3(1, 0.25, 1)));
            MaterialRow row = Array.Find(ContactScenarios.RepositoryMaterials(out _), m => m.Name == "sandbag");
            PropBlade b = spec.Blade;
            Double3 axis = Rolled().Rotate(Up);
            foreach (double depth in new[] { 0.002, 0.010 })
            {
                StrikeScenarios.Rig rig = StrikeScenarios.Into(spec, soil, top, Up, Rolled(), depth, top.Y, 1.0, 200);
                List<PropStrikes.PassRecord> passes = Passes(rig).FindAll(r => r.Mode != StrikeMode.Glance && !r.Stone);
                double worst = 0;
                int closedForms = 0;
                foreach (PropStrikes.PassRecord r in passes)
                {
                    if (r.Mode != StrikeMode.Carve || r.Grind || r.Exit < r.TipBefore * R) continue;
                    double rt = r.TipBefore * R, a = Math.Acos(r.Radius / rt), t = Math.Tan(a);
                    double area = rt * rt * (a - Math.Sin(a) * Math.Cos(a));
                    double second = Math.Pow(rt, 4) * a / 2 - Math.Pow(r.Radius, 4) * (t + t * t * t / 3) / 2;
                    double rc = 0.5 * (r.Radius + rt);
                    int k = (int)((rc / R - (b.Stations[0] - 0.5 * b.Span)) / b.Span);
                    BladeState s = rig.Core.Propulsion.Rotor(3).Blade(r.Blade);
                    double theta = b.SectionAngle[k] + s.FactoryPitch + s.PitchSet[k];
                    Relative(r, axis, rc, b.Chord[k] * s.ChordShare[k], b.Thickness[k], theta, out double q, out double hf);
                    double closed = row.CrushStrength * area * hf * q + row.Density * hf * r.SpeedBefore * r.SpeedBefore * second * q * q * q;
                    worst = Math.Max(worst, Math.Abs(r.Work - closed) / closed);
                    closedForms++;
                    Assert.That(r.Fling, Is.GreaterThan(r.Work - r.Fling), "the flung mass dominates");
                }
                int first = passes.FindIndex(r => r.Jammed);
                TestContext.Out.WriteLine($"sandbag {depth * 1e3} mm: {passes.Count} passes, the first jam at pass {first}; {closedForms} against p·V + m_f·v², the worst {worst:E2}");
                foreach (PropStrikes.PassRecord r in passes.GetRange(0, Math.Min(4, passes.Count))) TestContext.Out.WriteLine("  " + StrikeScenarios.Line(r));
                Assert.That(closedForms, Is.GreaterThan(0));
                Assert.That(worst, Is.LessThan(1e-9));
                if (depth == 0.010) Assert.That(first, Is.InRange(0, 1), "the 10 mm dig stops the rotor within its first two passes");
            }
        }

        // "Props that touch the ground bog down", restated on surfaces.json's soils (p7f-damage 1.1a's soil half, 1.3's soil carve):
        // a disc rolled 30° reaching 2 and 10 mm into the dirt track's ground (a stub floor of S13), S04's (the range's plane at its
        // track start) and a soil shape of S04 (a runtime box with the belt_straw surface, as S04's patches have), dry, in Rain and
        // in Severe rain. On the soils without their stones (the carve's own claims; the stones are ASoilPassMeetsItsStones'),
        // each carve's work is p·V + m_f·v² within 1e-9 of the closed form at the rain's set (p = c·N_c, ρ the bulk density) at the
        // blade's speed through the soil (its own and the body's, p7f-damage 1.3's tip slide), the flung mass dominates, the 10 mm dig stops the rotor within its first two passes, the sand wears the leading
        // edges by Archard (V = k·s·W/H_b on every carve, BladeDamaged Wear), and the wetter soil, denser, takes more from the same
        // first pass. The known-wrong, model 40's Glance (the same rig without surfaces.json), takes no work and never stops the
        // rotor, so it fails.
        [Test]
        public void PropsThatTouchTheGroundBogDown()
        {
            DroneSpec spec = Spec;
            SurfaceTable table = Stoneless(SoilScenarios.Table());
            PropBlade b = spec.Blade;
            double k = new ContactSpec().WearCoefficient, nc = PropStrikes.BearingFactor;
            Assert.That(nc, Is.EqualTo(5.14).Within(0.002), "Prandtl's N_c");
            var range = new FlightWorld(RangeScenarios.World);
            Double3 belt = SoilScenarios.TrackStart(range), sky = StrikeScenarios.Sky + new Double3(0, -0.5, 0);
            byte beltId = MapWorld.SurfaceId("belt_straw");
            var soilBox = new ShapeGeometry
            {
                Kind = ShapeKind.Box, Center = sky + new Double3(0, -0.25, 0), AxisX = new Double3(1, 0, 0), AxisY = Up, AxisZ = new Double3(0, 0, 1),
                HalfExtents = new Double3(1, 0.25, 1), Surface = beltId,
            };
            var faces = new (string Name, string Surface, Func<FlightWorld> World, Double3 Top, RuntimeObject[] Objects)[]
            {
                ("the dirt track", "dirt_track", () => SoilScenarios.Floor("dirt_track"), default, null),
                ("S04's ground", "belt_straw", () => new FlightWorld(RangeScenarios.World), belt, null),
                ("S04's soil box", "belt_straw", () => new FlightWorld(RangeScenarios.World), sky, StrikeScenarios.One(soilBox)),
            };
            foreach (var face in faces)
            {
                double first2 = 0;
                foreach (RainLevel rain in new[] { RainLevel.Sunny, RainLevel.Rain, RainLevel.SevereRain })
                {
                    SoilSet set = Array.Find(table.Surfaces, s => s.Name == face.Surface).Set(SurfaceTable.MoistureOf(rain));
                    double p = set.Cohesion * nc;
                    foreach (double depth in new[] { 0.002, 0.010 })
                    {
                        StrikeScenarios.Rig rig = StrikeScenarios.Into(spec, face.Objects, face.Top, Up, Rolled(), depth, face.Top.Y, 1.0, 200, world: face.World(),
                            table: table, rain: rain);
                        List<PropStrikes.PassRecord> passes = Passes(rig).FindAll(r => r.Mode != StrikeMode.Glance && !r.Stone);
                        int stones = Passes(rig).FindAll(r => r.Stone).Count;
                        // Each blade's first carve meets it unworn (chord share 1), so its whole closed form is checked there; on
                        // every carve the crush and the fling share one swept volume (the frontal height the fling implies), and
                        // the sand's wear is Archard's on the pass's work.
                        double worst = 0, worstSplit = 0, worstWear = 0;
                        int closedForms = 0, splits = 0;
                        var unworn = new bool[spec.Props.Blades];
                        foreach (PropStrikes.PassRecord r in passes)
                        {
                            if (r.Mode != StrikeMode.Carve || r.Grind || r.Exit < r.TipBefore * R) continue;
                            double rt = r.TipBefore * R, a = Math.Acos(r.Radius / rt), t = Math.Tan(a);
                            double area = rt * rt * (a - Math.Sin(a) * Math.Cos(a));
                            double second = Math.Pow(rt, 4) * a / 2 - Math.Pow(r.Radius, 4) * (t + t * t * t / 3) / 2;
                            // The fling's h·q³ and the crush's h·q: one swept volume at the relative speed q (Ratio).
                            double implied = r.Fling / (set.BulkDensity * r.SpeedBefore * r.SpeedBefore * second);
                            worstSplit = Math.Max(worstSplit, Math.Abs(r.Work - r.Fling - p * area * implied / (r.Ratio * r.Ratio)) / (r.Work - r.Fling));
                            worstWear = Math.Max(worstWear, Math.Abs(r.Wear - k * set.SandShare * r.Work / b.Hardness) / r.Wear);
                            splits++;
                            Assert.That(r.Fling, Is.GreaterThan(r.Work - r.Fling), "the flung mass dominates");
                            if (unworn[r.Blade]) continue;
                            unworn[r.Blade] = true;
                            double rc = 0.5 * (r.Radius + rt);
                            int st = (int)((rc / R - (b.Stations[0] - 0.5 * b.Span)) / b.Span);
                            BladeState s = rig.Core.Propulsion.Rotor(3).Blade(r.Blade);
                            double theta = b.SectionAngle[st] + s.FactoryPitch + s.PitchSet[st];
                            Relative(r, Rolled().Rotate(Up), rc, b.Chord[st], b.Thickness[st], theta, out double q, out double hf);
                            double closed = p * area * hf * q + set.BulkDensity * hf * r.SpeedBefore * r.SpeedBefore * second * q * q * q;
                            worst = Math.Max(worst, Math.Abs(r.Work - closed) / closed);
                            closedForms++;
                        }
                        int first = passes.FindIndex(r => r.Jammed), worn = 0;
                        foreach (FlightEvent e in rig.Log)
                            if (e.Kind == EventKind.BladeDamaged && (StrikeMode)(e.Index >> 4) == StrikeMode.Wear) worn++;
                        double chord = 1;
                        for (int bl = 0; bl < spec.Props.Blades; bl++)
                            for (int j = 0; j < b.Stations.Length; j++) chord = Math.Min(chord, rig.Core.Propulsion.Rotor(3).Blade(bl).ChordShare[j]);
                        TestContext.Out.WriteLine($"{face.Name} ({face.Surface}, {rain}: c·N_c {p / 1e3:F2} kPa, ρ {set.BulkDensity:F1} kg/m³, sand {set.SandShare:F2}) "
                            + $"{depth * 1e3} mm: {passes.Count} passes ({stones} stones met), the first jam at pass {first}; {closedForms} unworn against p·V + m_f·v², the worst {worst:E2}, "
                            + $"{splits} split within {worstSplit:E2}; Archard's wear within {worstWear:E2}, {worn} wear events, the leading edge's chord share down to {chord:F6}");
                        foreach (PropStrikes.PassRecord r in passes.GetRange(0, Math.Min(3, passes.Count))) TestContext.Out.WriteLine("  " + StrikeScenarios.Line(r));
                        Assert.That(closedForms, Is.GreaterThan(0), face.Name + " is carved");
                        Assert.That(worst, Is.LessThan(1e-9));
                        Assert.That(worstSplit, Is.LessThan(1e-9));
                        Assert.That(worstWear, Is.LessThan(1e-9));
                        Assert.That(worn, Is.GreaterThan(0), "the sand wears the leading edge");
                        Assert.That(chord, Is.LessThan(1));
                        if (depth == 0.010) Assert.That(first, Is.InRange(0, 1), "the 10 mm dig stops the rotor within its first two passes");
                        if (depth != 0.002) continue;
                        Assert.That(passes[0].Work, Is.GreaterThan(first2), "the wetter soil is denser and takes more from the same pass");
                        first2 = passes[0].Work;
                    }
                }
                // The known-wrong: model 40's Glance, the same 10 mm dig without surfaces.json.
                StrikeScenarios.Rig wrong = StrikeScenarios.Into(spec, face.Objects, face.Top, Up, Rolled(), 0.010, face.Top.Y, 1.0, 200, world: face.World());
                List<PropStrikes.PassRecord> glances = Passes(wrong);
                TestContext.Out.WriteLine($"{face.Name}, the known-wrong Glance: {glances.Count} passes, all glances {glances.TrueForAll(r => r.Mode == StrikeMode.Glance)}, "
                    + $"jams {wrong.Count(EventKind.RotorJammed)}");
                Assert.That(glances.Count, Is.GreaterThan(0), "the disc meets the ground");
                Assert.That(glances.TrueForAll(r => r.Mode == StrikeMode.Glance && r.Work == 0), Is.True);
                Assert.That(wrong.Count(EventKind.RotorJammed), Is.EqualTo(0), "the Glance never bogs the prop down, so it fails");
            }
        }

        // "A soil pass meets its stones" (p7f-damage 1.3's tip slide): a disc rolled 30° 2 mm into the dirt track (medium gravel, 3.5 %)
        // and S04's ground (fine gravel, 1 %) at the hover's duty, on 150 seeds each ("soil.stones"). The stones met are the passes'
        // own odds, Σ(1 − e^(−λ)) over the draws, within 4 σ of a binomial; the known-wrong, the fraction taken as a pass's chance
        // (the stones' share of the soil, not of what the pass sweeps), misses them by far, so it fails. A stone is a hard target:
        // at the blade's speed it breaks or notches the leading edge, so the blades lose more mass than on the same soil without
        // its stones.
        [Test]
        public void ASoilPassMeetsItsStones()
        {
            DroneSpec spec = Spec;
            SurfaceTable table = SoilScenarios.Table(), none = Stoneless(table);
            var range = new FlightWorld(RangeScenarios.World);
            Double3 belt = SoilScenarios.TrackStart(range);
            var faces = new (string Name, string Surface, Func<FlightWorld> World, Double3 Top)[]
            {
                ("the dirt track", "dirt_track", () => SoilScenarios.Floor("dirt_track"), default),
                ("S04's ground", "belt_straw", () => new FlightWorld(RangeScenarios.World), belt),
            };
            foreach (var face in faces)
            {
                SoilSet set = Array.Find(table.Surfaces, s => s.Name == face.Surface).Dry;
                double expected = 0, variance = 0, lost = 0, lostNone = 0;
                int met = 0, draws = 0, damaging = 0;
                for (ulong seed = 1; seed <= 150; seed++)
                {
                    StrikeScenarios.Rig rig = StrikeScenarios.Into(spec, null, face.Top, Up, Rolled(), 0.002, face.Top.Y, 1.0, 60, seed, world: face.World(), table: table);
                    PropStrikes.PassRecord stone = default;
                    int before = draws;
                    foreach (PropStrikes.PassRecord r in Passes(rig))
                    {
                        // A draw's odds: a stone's record, or a carve's that met none; the carve right after a stone, on its blade with
                        // its odds, is that stone's draw carving on (none follows a stone that stopped the rotor or cut the blade short).
                        bool carveOn = stone.Stone && r.Corner == stone.Corner && r.Blade == stone.Blade && r.Stones == stone.Stones;
                        bool draw = r.Stone || (r.Mode != StrikeMode.Glance && !r.Grind && r.Stones > 0 && !carveOn);
                        stone = r;
                        if (!draw) continue;
                        double pr = 1 - Math.Exp(-r.Stones);
                        expected += pr;
                        variance += pr * (1 - pr);
                        draws++;
                        if (!r.Stone) continue;
                        met++;
                        if (r.Mode != StrikeMode.Glance) damaging++;
                    }
                    Assert.That((ulong)(draws - before), Is.LessThanOrEqualTo(rig.Core.Strikes.StoneDraws), "each draw counted once");
                    foreach (FlightEvent e in rig.Log) if (e.Kind == EventKind.BladeDamaged) lost += e.B;
                    StrikeScenarios.Rig plain = StrikeScenarios.Into(spec, null, face.Top, Up, Rolled(), 0.002, face.Top.Y, 1.0, 60, seed, world: face.World(), table: none);
                    foreach (FlightEvent e in plain.Log) if (e.Kind == EventKind.BladeDamaged) lostNone += e.B;
                }
                double sigma = Math.Sqrt(variance), naive = draws * set.StoneFraction, naiveSigma = Math.Sqrt(draws * set.StoneFraction * (1 - set.StoneFraction));
                TestContext.Out.WriteLine($"{face.Name} ({set.StoneFraction * 100:F1} % of {set.StoneDiameter * 1e3:F1} mm stones): {draws} draws, {met} stones met against "
                    + $"{expected:F1} ± {sigma:F1} expected; {damaging} of them broke or notched the blade; the known-wrong (the fraction as a pass's chance) "
                    + $"{naive:F1} ± {naiveSigma:F1}; mass lost {lost * 1e3:F2} g with the stones, {lostNone * 1e3:F3} g without");
                Assert.That(draws, Is.GreaterThan(100), "enough passes drew");
                Assert.That(Math.Abs(met - expected), Is.LessThan(4 * sigma), "the stones met are the passes' odds");
                Assert.That(Math.Abs(met - naive), Is.GreaterThan(4 * Math.Max(naiveSigma, 1)), "the known-wrong misses them, so it fails");
                Assert.That(damaging, Is.GreaterThan(0), "a stone is a hard target");
                Assert.That(lost, Is.GreaterThan(lostNone), "the stones chew the blades");
            }
        }

        // The pilot's landing tipped over onto the dirt track and S04's soil, Sunny and in Rain, the throttle cut at the touch or
        // the drone disarmed (prop-motor-damage's "Props that touch the ground bog down" when the drone tips over on landing):
        // it rolls onto its side, its lower props carve the soil and stop in it; the known-wrong, the rigid ground without
        // surfaces.json, only glances. "An armed tip-over chews its props up and stops" (p7f-damage 1.3's tip slide): with the
        // throttle cut and airmode's authority, the props meet the soil's stones and break, and the drone is at rest 3 s after the
        // touch (StrikeScenarios.TipStops); the known-wrong, model 43's carve (ContactSpec.TipSlide off), slides on at 13 m/s.
        // Since model 48 (the tip slide on the line) the landing flies the legs rigid across (StrikeScenarios.TippedLanding: on
        // p7g-legs' side give it does not tip at this drift), and the disarmed drone's ESCs brake as drone.json's BLHeli_S does
        // (p6-flight-core 1.6c, model 44): the props are stopped before the roll brings them to the soil, so the disarmed tip-over
        // carves next to nothing and breaks no blade (a stopped prop survives the tumble, the research 1.3's tip slide cites); the
        // known-wrong, the ESCs freewheeling (EscDisarm.Freewheel, model 38's coast), still spins them into the soil, its stones
        // and their breaks, and without surfaces.json there it glances.
        [TestCase("track", RainLevel.Sunny, false)]
        [TestCase("track", RainLevel.Rain, false)]
        [TestCase("belt", RainLevel.Sunny, false)]
        [TestCase("belt", RainLevel.Rain, false)]
        [TestCase("track", RainLevel.Sunny, true)]
        [TestCase("track", RainLevel.Rain, true)]
        [TestCase("belt", RainLevel.Sunny, true)]
        [TestCase("belt", RainLevel.Rain, true)]
        public void ATippedLandingDigsItsPropsIn(string site, RainLevel rain, bool disarm)
        {
            DroneSpec spec = Spec;
            StrikeScenarios.TipResult r = StrikeScenarios.TippedLanding(spec, World(site, out Double3 at), at, rain, SoilScenarios.Table(), disarm);
            StrikeScenarios.TipResult wrong = StrikeScenarios.TippedLanding(spec, World(site, out at), at, rain, null, disarm);
            TestContext.Out.WriteLine($"{site} in {rain}, {(disarm ? "disarmed" : "the throttle cut")}: " + r.Describe());
            TestContext.Out.WriteLine("  the known-wrong (no surfaces.json): " + wrong.Describe());
            Assert.That(r.MaxTilt, Is.GreaterThan(45), "the landing tips onto its side");
            Assert.That(r.Tunnels, Is.EqualTo(0));
            Assert.That(wrong.Passes, Is.EqualTo(0), "the known-wrong's props only glance");
            Assert.That(StrikeScenarios.TipStops(r), Is.True, "the landing stops on the ground");
            if (disarm)
            {
                StrikeScenarios.TipResult free = StrikeScenarios.TippedLanding(StrikeScenarios.Freewheeling(DroneSpec.Parse(RepositoryDrone.Text())), World(site, out at),
                    at, rain, SoilScenarios.Table(), true);
                StrikeScenarios.TipResult freeWrong = StrikeScenarios.TippedLanding(StrikeScenarios.Freewheeling(DroneSpec.Parse(RepositoryDrone.Text())), World(site, out at),
                    at, rain, null, true);
                TestContext.Out.WriteLine("  the known-wrong (the ESCs freewheeling): " + free.Describe());
                TestContext.Out.WriteLine("  freewheeling without surfaces.json: " + freeWrong.Describe());
                Assert.That(r.Broken, Is.EqualTo(0), "the braked props survive the tumble");
                Assert.That(r.Lost, Is.LessThan(1e-6), "under a milligram worn");
                Assert.That(free.InSoil, Is.GreaterThan(0), "freewheeling, the spinning props carve the soil");
                Assert.That(free.Stones, Is.GreaterThan(0), "meet its stones");
                Assert.That(free.Broken, Is.GreaterThan(0), "and break: the brake is what saves them, so the known-wrong fails");
                Assert.That(freeWrong.Glances, Is.GreaterThan(0), "without surfaces.json its props meet the ground");
                Assert.That(freeWrong.Passes, Is.EqualTo(0), "and only glance");
                return;
            }
            Assert.That(r.InSoil, Is.GreaterThan(0), "its props carve the soil");
            Assert.That(r.Work, Is.GreaterThan(0));
            // Since model 64 on S04's wet soil the cut tip-over ends on its side at 74° (the canister props it, upside down on the
            // old coil) and its lower props are chewed to stumps on 12 stones (9 breaks, the shortest blade 19.8 mm) before any
            // jams: stopped by breaking, not by the soil's grip.
            Assert.That(r.Jams + r.Broken, Is.GreaterThan(0), "and stop in it: jammed, or chewed to stumps");
            Assert.That(wrong.Glances, Is.GreaterThan(0), "the known-wrong's props meet the ground");
            Assert.That(r.Stones, Is.GreaterThan(0), "its props meet the soil's stones");
            Assert.That(r.Broken, Is.GreaterThan(0), "and the armed props are chewed up");
            StrikeScenarios.TipResult slid = StrikeScenarios.TippedLanding(spec, World(site, out at), at, rain, SoilScenarios.Table(), disarm, tipSlide: false);
            TestContext.Out.WriteLine("  the known-wrong (model 43's carve): " + slid.Describe());
            Assert.That(StrikeScenarios.TipStops(slid), Is.False, "model 43's carve slides on, so it fails");
        }

        static FlightWorld World(string site, out Double3 at) => site == "track" ? StrikeScenarios.TrackWorld(out at) : StrikeScenarios.BeltWorld(out at);

        // surfaces.json's table with every soil's stones taken out (the carve's own claims, and the stones' known-wrong).
        static SurfaceTable Stoneless(SurfaceTable table)
        {
            SurfaceTable t = table.Clone();
            for (int i = 0; i < t.Surfaces.Length; i++)
            {
                t.Surfaces[i].Dry.StoneFraction = 0;
                t.Surfaces[i].Wet.StoneFraction = 0;
                t.Surfaces[i].Saturated.StoneFraction = 0;
            }
            return t;
        }

        // "Who yields is decided by the material": the same 5 mm entry at hover speed into concrete, steel, masonry, timber, bark,
        // rubber and S04's straw (its boxes' row): the hard three damage the blade, the soft three are carved and leave it whole
        // (unless a cut breaks a section), the straw leaves it whole. The known-wrong, damage by the entry speed alone, damages
        // the blade on the straw as on concrete (the same speed), so it fails.
        [Test]
        public void WhoYieldsIsDecidedByTheMaterial()
        {
            DroneSpec spec = Spec;
            Quat att = StrikeScenarios.Heading(StrikeScenarios.LeadHeading);
            var speeds = new Dictionary<string, double>();
            foreach (string material in new[] { "concrete", "steel", "masonry", "timber", "bark", "rubber", "straw" })
            {
                RuntimeObject[] objects = FaceOf(material, out Double3 face);
                StrikeScenarios.Rig rig = StrikeScenarios.Into(spec, objects, face, South, att, 0.005, face.Y, 0.3, 100);
                List<PropStrikes.PassRecord> passes = Passes(rig);
                int damaged = rig.Count(EventKind.BladeDamaged);
                BladeState b0 = rig.Core.Propulsion.Rotor(3).Blade(0), b1 = rig.Core.Propulsion.Rotor(3).Blade(1);
                StrikeMode mode = passes.Count > 0 ? passes[0].Mode : StrikeMode.Glance;
                speeds[material] = passes.Count > 0 ? passes[0].SpeedBefore * passes[0].Radius : 0; // the blade's speed where it meets the face
                TestContext.Out.WriteLine($"{material}: {passes.Count} passes, first {mode}, {damaged} damage events; tips {b0.TipRadius * R * 1e3:F2} / {b1.TipRadius * R * 1e3:F2} mm"
                    + (passes.Count > 0 ? "; " + StrikeScenarios.Line(passes[0]) : ""));
                Assert.That(passes.Count, Is.GreaterThan(0));
                if (material == "concrete" || material == "steel" || material == "masonry") Assert.That(damaged, Is.GreaterThan(0), material + " damages the blade");
                else if (material == "straw")
                {
                    Assert.That(damaged, Is.EqualTo(0), "the straw leaves it whole");
                    Assert.That(b0.Whole && b1.Whole, Is.True);
                }
                else
                {
                    Assert.That(passes.TrueForAll(r => r.Mode == StrikeMode.Carve || r.Mode == StrikeMode.Break), Is.True, material + " is carved");
                    if (!passes.Exists(r => r.Mode == StrikeMode.Break)) Assert.That(b0.Whole && b1.Whole, Is.True, material + " leaves the blade whole");
                }
            }
            // The known-wrong: a blade damaged whenever its speed into the face passes a threshold; the straw's and the concrete's
            // are the same strike (the same blade speed where it meets the face), so the threshold that damages on concrete damages
            // on the straw too.
            TestContext.Out.WriteLine($"the blade's speed where it meets the face: straw {speeds["straw"]:F6} m/s, concrete {speeds["concrete"]:F6} m/s");
            Assert.That(speeds["straw"], Is.EqualTo(speeds["concrete"]).Within(1e-9), "the same strike");
        }

        // ---- The strike in its step, and no crash by a prop ---------------------------------------------------------------------

        // "The strike's impulses balance": on a body held free in space (the rig without its clamp, no gravity, no thrust on the
        // body), a crush on steel and a carve in timber. The body's angular momentum about its centre of mass plus the rotors'
        // spin changes by the face's impulse's moment within 1e-9, its momentum by the face's impulse, the rotor's energy lost is
        // the pass's work; the strike's step flies the coefficients it began with.
        [TestCase("steel")]
        [TestCase("timber")]
        public void TheStrikesImpulsesBalance(string material)
        {
            DroneSpec spec = Spec;
            Quat att = StrikeScenarios.Heading(StrikeScenarios.LeadHeading);
            RuntimeObject[] objects = FaceOf(material, out Double3 face);
            Double3 at = StrikeScenarios.RimInto(spec, att, 3, face, South, 0.002, face.Y), start = at + South * 0.01;
            var rig = new StrikeScenarios.Rig(StrikeScenarios.Setup(spec, start, att, objects), StrikeScenarios.HoverDuty(spec)) { Free = true };
            rig.Core.Strikes.Record = new List<PropStrikes.PassRecord>();
            MassProperties body = rig.Core.Body;
            int checkedPasses = 0;
            double worstP = 0, worstL = 0, worstE = 0;
            for (int k = 1; k <= 120 && checkedPasses < 4; k++)
            {
                Double3 p = k <= 40 ? start + (at - start) * (k / 40.0) : at;
                int before = Passes(rig).Count;
                double eta = rig.Core.Propulsion.Rotor(3).ThrustRatio(0);
                bool sound = rig.Core.Propulsion.Rotor(3).Sound;
                rig.Step(p, default);
                if (Passes(rig).Count != before + 1) continue;
                PropStrikes.PassRecord r = Passes(rig)[before];
                if (r.Mode == StrikeMode.Glance) continue;
                worstP = Math.Max(worstP, (rig.DeltaV * body.Mass - r.Impulse).Length() / r.Impulse.Length());
                Double3 arm = rig.Now.Attitude.InverseRotate(r.Point - rig.Now.Position) - body.CenterOfMass;
                Double3 moment = Double3.Cross(arm, rig.Now.Attitude.InverseRotate(r.Impulse));
                double spin = spec.Motors[3].Clockwise ? -1 : 1;
                Double3 rotor = new Double3(0, spin * r.Inertia * (r.SpeedBefore - r.SpeedAfter), 0); // the spin's change, s·J·(ω − ω')
                Double3 lhs = body.Inertia * rig.DeltaRate + rotor;
                worstL = Math.Max(worstL, (lhs - moment).Length() / moment.Length());
                double lostE = 0.5 * r.Inertia * (r.SpeedBefore * r.SpeedBefore - r.SpeedAfter * r.SpeedAfter);
                worstE = Math.Max(worstE, Math.Abs(lostE - r.Used) / r.Used);
                if (sound) Assert.That(eta, Is.EqualTo(1), "the strike's step flew the coefficients it began with");
                checkedPasses++;
                TestContext.Out.WriteLine(StrikeScenarios.Line(r));
            }
            TestContext.Out.WriteLine($"{material}: {checkedPasses} passes; momentum within {worstP:E2}, angular momentum within {worstL:E2}, the energy within {worstE:E2}; "
                + $"the rotor's thrust ratio after them {rig.Core.Propulsion.Rotor(3).ThrustRatio(0):F6}");
            Assert.That(checkedPasses, Is.GreaterThan(0));
            Assert.That(worstP, Is.LessThan(1e-9));
            Assert.That(worstL, Is.LessThan(1e-9));
            Assert.That(worstE, Is.LessThan(1e-9));
        }

        // The damage acts from the next step: through the strike's step the thrust is the undamaged twin's bit for bit; the step
        // after it differs (the rotor's tables and its dipped speed). On steel, a crush.
        [Test]
        public void TheDamageFirstActsInTheNextStep()
        {
            DroneSpec spec = Spec;
            Quat att = StrikeScenarios.Heading(StrikeScenarios.LeadHeading);
            RuntimeObject[] objects = FaceOf("steel", out Double3 face);
            Double3 at = StrikeScenarios.RimInto(spec, att, 3, face, South, 0.002, face.Y), start = at + South * 0.01;
            var on = new StrikeScenarios.Rig(StrikeScenarios.Setup(spec, start, att, objects), StrikeScenarios.HoverDuty(spec));
            var off = new StrikeScenarios.Rig(StrikeScenarios.Setup(spec, start, att, objects, strikes: false), StrikeScenarios.HoverDuty(spec));
            int struck = -1;
            for (int k = 1; k <= 60 && struck < 0; k++)
            {
                Double3 p = start + (at - start) * Math.Min(1, k / 40.0);
                on.Step(p, default);
                off.Step(p, default);
                Assert.That(on.Core.Propulsion.Thrust(3), Is.EqualTo(off.Core.Propulsion.Thrust(3)), "through the strike's step, the twin's thrust");
                if (on.Count(EventKind.BladeDamaged) > 0) struck = k;
            }
            Assert.That(struck, Is.GreaterThan(0));
            on.Step(at, default);
            off.Step(at, default);
            TestContext.Out.WriteLine($"struck at step {struck}; the next step's thrust {on.Core.Propulsion.Thrust(3):F6} N against the twin's {off.Core.Propulsion.Thrust(3):F6}");
            Assert.That(on.Core.Propulsion.Thrust(3), Is.Not.EqualTo(off.Core.Propulsion.Thrust(3)));
        }

        // "The drone flies on after a wall touch": flown by SimPilot along H01_D's south wall at 2 m/s with the lead prop's rim
        // 3 mm into it for 0.3 s, then away and hovering: no crash, damaged blades, a hover at the end. And at 10 m/s into the
        // wall (1.6b's approach): a crash by acceleration named by a body part, the prop's strike and damage logged before it.
        // The known-wrong, model 29's prop crash, ends a strike faster than 3 m/s along the normal.
        [Test]
        public void TheDroneFliesOnAfterAWallTouch()
        {
            DroneSpec spec = Spec;
            StrikeScenarios.GrazeResult g = StrikeScenarios.Graze(spec, 2, 0.003, 0.3, 3, 0x450F);
            TestContext.Out.WriteLine(g.Describe());
            Assert.That(g.Crashes, Is.EqualTo(0), "a graze ends no flight");
            Assert.That(g.Damaged, Is.GreaterThan(0), "damaged");
            Assert.That(g.EndSpeed, Is.LessThan(0.5), "it hovers after");

            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong h);
            var events = new List<FlightEvent>();
            ContactScenarios.PoleStrike r = RangeScenarios.Into(spec, m, h, RangeScenarios.Find("wall"), events: events);
            int crashAt = events.FindIndex(e => e.Kind == EventKind.Crash);
            int strikeAt = events.FindIndex(e => e.Kind == EventKind.BladeStrike), damageAt = events.FindIndex(e => e.Kind == EventKind.BladeDamaged);
            TestContext.Out.WriteLine($"10 m/s into the wall: crash ({r.Cause}) by {r.Part} on {r.Object} ({r.Material}); first blade strike event {strikeAt}, "
                + $"first damage {damageAt}, the crash {crashAt} of {events.Count}");
            Assert.That(r.Crashes, Is.GreaterThan(0));
            Assert.That(r.Cause, Is.EqualTo(CrashCause.Acceleration));
            Assert.That(r.Part, Does.Not.StartWith("prop_"), "named by a body part");
            Assert.That(strikeAt, Is.InRange(0, crashAt - 1), "the prop's strike before it");
            Assert.That(damageAt, Is.InRange(0, crashAt - 1), "and its damage");
        }

        // ---- Damage's randomness ----------------------------------------------------------------------------------------------

        // "The strength draws cover their bounds and set the chip": 10 000 seeds' draws within ±12 %, both clamps reached; one
        // scripted strike on 1000 seeds breaks the blade at a length that varies across them only through the drawn strength
        // (one factor, one length), from a chip near the tip on the weakest blades to none on the strongest. The scripted strike
        // is the pitched wall's slap near its margin (its force the tip's own dynamics, not its crush, so the drawn strength
        // moves the first station it breaks inward and, past the margin, keeps the blade whole); a crush's break does not move
        // with it (its force σ_c·A and its strength scale alike: the masonry graze chips at 117.0 mm, the head-on rod breaks at
        // 85.57 mm, at both bounds). The known-wrong, a fixed chip per strike, gives one length on every seed.
        [Test]
        public void TheStrengthDrawsCoverTheirBoundsAndSetTheChip()
        {
            DroneSpec spec = Spec;
            double lo = 1, hi = 1;
            int clampedLo = 0, clampedHi = 0;
            for (ulong seed = 1; seed <= 10000; seed++)
            {
                var random = new FlightRandom(seed);
                SeedStream s = random.Open("prop.blade_strength");
                for (int i = 0; i < 8; i++)
                {
                    double d = random.DrawNormal(ref s, "x", 0.05, 0.12);
                    lo = Math.Min(lo, 1 + d);
                    hi = Math.Max(hi, 1 + d);
                    if (d == -0.12) clampedLo++;
                    if (d == 0.12) clampedHi++;
                }
            }
            TestContext.Out.WriteLine($"80 000 factors in {lo:F4}–{hi:F4}; clamped {clampedLo} low, {clampedHi} high");
            Assert.That(lo, Is.EqualTo(0.88).Within(1e-15));
            Assert.That(hi, Is.EqualTo(1.12).Within(1e-15));
            Assert.That(clampedLo, Is.GreaterThan(0));
            Assert.That(clampedHi, Is.GreaterThan(0));

            var left = new SortedDictionary<double, double>();
            double weak = double.NaN, strong = double.NaN;
            for (ulong seed = 1; seed <= 1000 || double.IsNaN(weak) || double.IsNaN(strong); seed++)
            {
                PropStrikes.PassRecord hit = StrikeScenarios.WallChip(spec, seed, out double f);
                double tip = hit.TipAfter * R;
                if (f == 1 - 0.12) weak = tip;
                if (f == 1 + 0.12) strong = tip;
                if (seed > 1000) continue;
                if (left.TryGetValue(f, out double seen)) Assert.That(tip, Is.EqualTo(seen), "one factor, one length");
                left[f] = tip;
            }
            double shortest = double.PositiveInfinity, longest = 0;
            foreach (double t in left.Values)
            {
                shortest = Math.Min(shortest, t);
                longest = Math.Max(longest, t);
            }
            var stations = new SortedSet<double>(left.Values);
            var lines = new List<string>();
            foreach (double t in stations)
            {
                double from = double.PositiveInfinity, to = 0;
                foreach (var kv in left)
                {
                    if (kv.Value != t) continue;
                    from = Math.Min(from, kv.Key);
                    to = Math.Max(to, kv.Key);
                }
                lines.Add($"{t * 1e3:F2} mm for factors {from:F3}–{to:F3}");
            }
            TestContext.Out.WriteLine("lengths left: " + string.Join("; ", lines));
            TestContext.Out.WriteLine($"1000 seeds: the blade left {shortest * 1e3:F2}–{longest * 1e3:F2} mm; at the bounds' factors 0.88 and 1.12 "
                + $"{weak * 1e3:F2} and {strong * 1e3:F2} mm");
            Assert.That(stations.Count, Is.GreaterThan(1), "the drawn strength moves the chip (a fixed chip gives one length)");
            Assert.That(weak, Is.LessThan(R), "the weakest blade chips");
            Assert.That(strong, Is.EqualTo(R).Within(1e-12), "the strongest stays whole past the slap's margin");
            Assert.That(shortest, Is.GreaterThan(0.85 * R), "a chip near the tip, never the blade");
        }
    }
}
