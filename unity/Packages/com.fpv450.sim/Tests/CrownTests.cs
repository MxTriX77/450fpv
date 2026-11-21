using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // w9-village-physics 2.3 (design.md "## 2.3"): what a crown's twigs hold, and its leaves on the props. A crown here is a twig
    // sphere on the stub's floor (a runtime object named after a vendor variant, so its leaves are that variant's), its law the
    // repository's physics_materials.json twig.crown; the hand numbers are computed here from the drone's own shapes.
    public sealed class CrownTests
    {
        const double Centre = 10, Big = 4;

        static FlightWorld Floor() => new FlightWorld(new FloorWorld(0, "belt_bare", "steel", "timber", "twig", "foliage"));

        static RuntimeObject Sphere(FlightWorld world, string variant, double radius) =>
            new RuntimeObject(variant, new[]
            {
                new ShapeGeometry
                {
                    Kind = ShapeKind.Sphere, Center = new Double3(0, Centre, 0), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
                    AxisZ = new Double3(0, 0, 1), Radius = radius, Material = world.Query.MaterialId("twig"),
                },
            });

        static CrownRow Crown(MaterialRow[] rows)
        {
            foreach (MaterialRow r in rows)
                if (r.Name == "twig") return r.Crown;
            return null;
        }

        // The twigs' hold on the drone pushed straight down, by hand: each part's silhouette along the vertical, p (a capsule's
        // 2·l⊥ + π·w, a sphere's π·w), each leg one part (its half-skid: the leg is one spring), L_V·p/4 twigs a metre at W̄.
        static double HandHold(ContactSpec spec, CrownRow k)
        {
            double perimeter = 0;
            foreach (ContactShape s in spec.Shapes)
            {
                if (s.Role == ContactRole.Disc || s.Role == ContactRole.Support) continue;
                Double3 ab = s.B - s.A;
                double across = Math.Sqrt(ab.X * ab.X + ab.Z * ab.Z);
                perimeter += 2 * across + Math.PI * 2 * s.Radius;
            }
            return k.TwigLength * perimeter / 4 * k.TwigWork;
        }

        struct Fall
        {
            public double Force, Hand, Weight, Depth, Speed;
        }

        // The drone disarmed and level, let go 1 cm over the top of a 4 m twig sphere: its contacts' mean upward force while the
        // whole drone is 0.3–1.5 m inside (every part past the twigs' reach δ̄), how deep it is after 1.5 s and how fast.
        static Fall Drop(MaterialRow[] rows, ulong hash)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightWorld world = Floor();
            Loadout legs = spec.Nominal;
            legs.Legs = true;
            ContactSpec contacts = spec.BuildContacts(legs, rows, hash, SoilScenarios.Table());
            double top = Centre + Big, standing = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, rows, hash, true, new Double3(0, top + standing + 0.01, 0));
            setup.World = world;
            setup.Contacts = contacts;
            setup.RuntimeObjects = new[] { Sphere(world, "DeadBush_4", Big) };
            var core = new FlightCore(setup);
            var r = new Fall { Weight = core.Body.Mass * 9.80665 };
            CrownRow k = Crown(rows);
            r.Hand = k != null ? HandHold(contacts, k) : double.NaN;
            int n = 0;
            for (int s = 0; s < 1500; s++)
            {
                core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs });
                double inside = top - (core.State.Position.Y - standing);
                if (inside < 0.3 || inside > 1.5) continue;
                r.Force += core.Trace.ContactForce.Y;
                n++;
            }
            r.Force = n > 0 ? r.Force / n : 0;
            r.Depth = top - (core.State.Position.Y - standing);
            r.Speed = -core.State.Velocity.Y;
            return r;
        }

        // A 4.7 kg drone falls through a crown's twigs (design 2.3 §2): they break at 0.115 J each and hold 0.75 N of its 46 N,
        // the hand calculation's within 10 %. Model 89's shell held it on the crown's top like a soft solid.
        [Test]
        public void ADroneFallsThroughACrownsTwigsOnTheirStrength()
        {
            MaterialRow[] rows = ContactScenarios.RepositoryMaterials(out ulong hash);
            Fall twigs = Drop(rows, hash), wrong = Drop(GoldenFlights.WithoutTheCrowns(rows), hash);
            TestContext.Out.WriteLine($"the twigs: {twigs.Force:F3} N up while 0.3–1.5 m in (hand {twigs.Hand:F3} N, weight {twigs.Weight:F1} N); "
                + $"after 1.5 s {twigs.Depth:F2} m in at {twigs.Speed:F2} m/s");
            TestContext.Out.WriteLine($"model 89's shell: {wrong.Force:F1} N up; after 1.5 s {wrong.Depth:F3} m in at {wrong.Speed:F3} m/s");
            Assert.That(twigs.Force, Is.EqualTo(twigs.Hand).Within(10).Percent, "the twigs' hold is the hand calculation's");
            Assert.That(twigs.Force, Is.LessThan(0.05 * twigs.Weight), "the twigs hold a few per cent of the drone");
            Assert.That(twigs.Depth, Is.GreaterThan(5), "it falls through: 1.5 s of an almost free fall");
            Assert.That(wrong.Depth, Is.LessThan(0.3), "the known-wrong holds it on the crown");
        }

        struct Pass
        {
            public double Measured, Hand, Rear, ShakeIn, ShakeOut, Line, LineAt, Dip;
            public long Cells;
            public int Lost;
        }

        // The armed drone hovering at the sphere's centre height 6 m short of a 3 m leafy sphere (GreenBush's LAD, 0.976 m²/m³),
        // then flown through it at 2 m/s (the pilot holding that velocity). Per step while a front disc is wholly inside, by
        // hand: it meets fresh leaves at its rim only, 2R·v_⊥ of plane a second, and flings their fresh mass in the slab its tip
        // sweeps to the tip's speed, m_A·LAD·t·(ωR)² a square metre (t = h_f + (s − h_f)·|v̂·â|); its tip under the leaves'
        // critical speed cuts none. Measured: the front rotors' leaf work over the same steps; the rear rotors' (in the front
        // ones' track); the body's 4–15 Hz rate in the leaves and before; the strongest line at 200–400 Hz in them; the front
        // rotors' deepest dip under their speed before the crown.
        static Pass Through(double speed)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] rows = ContactScenarios.RepositoryMaterials(out ulong hash);
            FlightWorld world = Floor();
            const double radius = 3;
            var start = new Double3(0, Centre, -6);
            FlightSetup setup = ContactScenarios.Setup(spec, rows, hash, true, start);
            setup.World = world;
            Loadout legs = spec.Nominal;
            legs.Legs = true;
            setup.Contacts = spec.BuildContacts(legs, rows, hash, SoilScenarios.Table());
            setup.RuntimeObjects = new[] { Sphere(world, "GreenBush", radius) };
            var core = new FlightCore(setup);
            CrownRow k = Crown(rows);
            double lad = k.LeafAreaDensityOf("GreenBush"), mass = k.LeafFreshMass, cell = k.LeafCell, R = setup.Propulsion.Props.Diameter / 2;
            PropBlade b = setup.Propulsion.Rotor.Blade;
            int tip = b.Stations.Length - 1;
            for (int s = 0; s < 16; s++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = s >= 8 });
            var pilot = new SimPilot(core.Controller, start);
            var r = new Pass();
            double[] roll = new double[20000], pitch = new double[20000];
            int inFrom = -1, inTo = -1, n = 0;
            double hover = 0;
            for (int s = 0; s < 9000; s++)
            {
                if (s == 3000)
                {
                    pilot.HoldVelocity = true;
                    pilot.TargetVelocity = new Double3(0, 0, speed);
                    hover = 0.5 * (core.State.RotorSpeed[1] + core.State.RotorSpeed[3]);
                }
                double was1 = core.Strikes.LeafWorkOn(1) + core.Strikes.LeafWorkOn(3);
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                r.Lost += core.Events.Lost;
                ref readonly FlightState st = ref core.State;
                roll[n] = st.BodyRate.Z;
                pitch[n++] = st.BodyRate.X;
                Double3 axis = st.Attitude.Rotate(new Double3(0, 1, 0)), v = st.Velocity;
                double va = Double3.Dot(v, axis), vp = (v - axis * va).Length(), a = v.Length() > 0 ? Math.Abs(va) / v.Length() : 0;
                bool inside = true;
                foreach (int c in new[] { 1, 3 })
                {
                    Double3 hub = st.Position + st.Attitude.Rotate(setup.Contacts.Shapes[DiscOf(setup.Contacts, c)].A) - new Double3(0, Centre, 0);
                    if (!(hub.Length() < radius - R - 0.05)) inside = false;
                }
                if (s > 3000 && inside)
                {
                    if (inFrom < 0) inFrom = n - 1;
                    inTo = n;
                    r.Measured += core.Strikes.LeafWorkOn(1) + core.Strikes.LeafWorkOn(3) - was1;
                    foreach (int c in new[] { 1, 3 })
                    {
                        BladeState blade = core.Propulsion.Rotor(c).Blade(0);
                        double theta = b.SectionAngle[tip] + blade.FactoryPitch + blade.PitchSet[tip];
                        double hf = b.Chord[tip] * blade.ChordShare[tip] * Math.Sin(theta) + b.Thickness[tip] * Math.Cos(theta);
                        double t = hf + (cell - hf) * a, omega = st.RotorSpeed[c];
                        r.Hand += 2 * R * vp * FlightCore.Dt * mass * lad * t * omega * omega * R * R;
                        r.Dip = Math.Max(r.Dip, (hover - omega) / hover);
                    }
                }
            }
            r.Rear = core.Strikes.LeafWorkOn(0) + core.Strikes.LeafWorkOn(2);
            r.Cells = core.Strikes.LeafCells;
            int len = Math.Min(inTo - inFrom, 1000);
            if (len > 100)
            {
                r.ShakeIn = Math.Sqrt(Sq(Shake.RateRms(roll, inFrom, len, 4, 15)) + Sq(Shake.RateRms(pitch, inFrom, len, 4, 15)));
                r.ShakeOut = Math.Sqrt(Sq(Shake.RateRms(roll, inFrom - len - 200, len, 4, 15)) + Sq(Shake.RateRms(pitch, inFrom - len - 200, len, 4, 15)));
                r.Line = Shake.Line(roll, inFrom, len, 200, 400, out r.LineAt);
            }
            return r;
        }

        static int DiscOf(ContactSpec spec, int corner)
        {
            for (int i = 0; i < spec.Shapes.Length; i++)
                if (spec.Shapes[i].Role == ContactRole.Disc && spec.Shapes[i].Corner == corner) return i;
            return -1;
        }

        static double Sq(double x) => x * x;

        // The spec's "Leaves in a crown": a disc driven through a crown's leaves at 2 m/s pays their fling at its rim, within 10 %
        // of the hand calculation; the rear discs in the front ones' track meet less; nothing refused in the steps' buffers.
        [Test]
        public void ADiscThroughLeavesPaysTheirFlingAtItsRim()
        {
            Pass r = Through(2);
            TestContext.Out.WriteLine($"2 m/s through GreenBush's leaves: front rotors {r.Measured:F2} J while wholly inside (hand {r.Hand:F2} J); "
                + $"rear rotors {r.Rear:F2} J over the flight; {r.Cells} cells; the front rotors' deepest dip {100 * r.Dip:F1} %; "
                + $"4–15 Hz body rate {r.ShakeIn:F3} °/s in the leaves against {r.ShakeOut:F3} °/s before; the strongest line at 200–400 Hz "
                + $"{r.Line:F4} °/s at {r.LineAt:F0} Hz; {r.Lost} events refused");
            Assert.That(r.Hand, Is.GreaterThan(1), "the premise: the front discs pass through leaves");
            Assert.That(r.Measured, Is.EqualTo(r.Hand).Within(10).Percent, "the leaves' work is the hand calculation's");
            Assert.That(r.Rear, Is.LessThan(r.Measured), "the rear discs fly in the front ones' cleared track");
            Assert.That(r.Lost, Is.EqualTo(0), "no event refused");
        }
    }
}
