using System;
using System.Diagnostics;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // fiber-tether (p7d-tether, model 61): the rotors' fountain against the drone's body. The plane jet impinging on a wide
    // underside (Khayrullina et al. 2017: free to 70-75 % of its length), on a narrow one (the potential flow's stagnation),
    // the lee over a part, and the repository drone at rest with and without its body; the figures print for the as-built note.
    public sealed class RotorAirBodyTests
    {
        static void Print(string line) => TestContext.Progress.WriteLine(line);

        static TetherTable Table => TetherScenarios.Table();

        // A body of the given contact shapes (body axes = world: the field's tests place it at the origin, level).
        static ContactModel Body(params ContactShape[] shapes) =>
            new ContactModel(new ContactSpec { Shapes = shapes, Materials = ContactScenarios.RepositoryMaterials(out _) }, FlightWorld.Floor(), 1);

        static ContactShape Capsule(Double3 a, Double3 b, double r) => new ContactShape { Name = "test", Role = ContactRole.Body, A = a, B = b, Radius = r };

        // Two or four rotors over a floor at y = 0, their discs level, with or without a body placed at the origin.
        static RotorAir Field(double radius, Double3[] hubs, double thrust, ContactModel body)
        {
            var field = new RotorAir(Table, radius, true, body);
            var h = new Double3[4];
            var t = new double[4];
            for (int i = 0; i < 4; i++)
            {
                h[i] = i < hubs.Length ? hubs[i] : new Double3(1e3 * (i + 1), 1, 0);
                t[i] = i < hubs.Length ? thrust : 0;
            }
            if (body != null) field.PlaceBody(default, Quat.Identity);
            field.Begin(h, new Double3(0, 1, 0), t, new Double3[4], new double[4]);
            return field;
        }

        static Double3 Air(RotorAir f, double x, double y, double z = 0) => f.At(new Double3(x, y, z), 0, out _);

        // Dekker et al.'s rig (RotorAirTests): R 76.2 mm, 2.14 N each, one diameter apart tip to tip, at H/R 4.
        const double R = 0.0762, Thrust = 2.14;
        static double Spread => Table.Number("rotor_air.plane_jet_spreading_rate");
        static readonly Double3[] Dekker = { new Double3(-2 * R, 3.999 * R, 0), new Double3(2 * R, 3.999 * R, 0) };

        // The underside of a body wide to the jet (a capsule of 0.2 m along the sheet, its lowest line 2R over the floor on the
        // midline): below it the fountain is the free one down to 72.5 % of the plane jet's length to the underside (the
        // published 70-75 %, the band's middle), then falls linearly to nothing at the underside (the stagnation flow); the air
        // turned across the sheet keeps the flow's continuity (∂u/∂x + ∂w/∂y = 0) and never passes the jet's centreline speed.
        [Test]
        public void TheFountainStagnatesUnderAWideUndersideAsMeasured()
        {
            const double a = 0.3, under = 2 * R;
            ContactModel body = Body(Capsule(new Double3(0, under + a, -0.3), new Double3(0, under + a, 0.3), a));
            RotorAir free = Field(R, Dekker, Thrust, null), blocked = Field(R, Dekker, Thrust, body);
            // The last height at which the blocked field is the free one, bit for bit (bisection on the midline).
            double lo = 0.2 * under, hi = under;
            Assert.That(Air(blocked, 0, lo).Y, Is.EqualTo(Air(free, 0, lo).Y), "free well below the body");
            for (int k = 0; k < 60; k++)
            {
                double mid = 0.5 * (lo + hi);
                if (Air(blocked, 0, mid).Y == Air(free, 0, mid).Y) lo = mid;
                else hi = mid;
            }
            // The jet's length from its virtual origin: the free field's centreline speed w_c ∝ 1/√y gives y from two heights.
            double w1 = Air(free, 0, 0.5 * under).Y, w2 = Air(free, 0, 0.9 * under).Y;
            double origin = (w2 * w2 * 0.9 * under - w1 * w1 * 0.5 * under) / (w1 * w1 - w2 * w2); // y + y0 ∝ 1/w²
            double length = under + origin, freeShare = (lo + origin) / length, depth = under - lo;
            double top = Air(free, 0, lo).Y, worst = 0, mostTurned = 0, divergence = 0, farther = 0;
            for (int i = 1; i < 10; i++)
            {
                double y = lo + depth * i / 10, expected = top * (under - y) / depth;
                worst = Math.Max(worst, Math.Abs(Air(blocked, 0, y).Y - expected) / top);
            }
            // Continuity across the sheet inside the layer, and the turned air's speed against the centreline's.
            double b = 0, e = 1e-6;
            for (int i = 1; i < 16; i++)
            {
                double x = 0.002 * i, y = lo + 0.5 * depth;
                if (Air(blocked, x, y).X == 0) break; // past the layer, where the underside curves up
                double dudx = (Air(blocked, x + e, y).X - Air(blocked, x - e, y).X) / (2 * e);
                double dwdy = (Air(blocked, x, y + e).Y - Air(blocked, x, y - e).Y) / (2 * e);
                double off = Math.Abs(dudx + dwdy) / (top / depth);
                if (x <= 0.2 * Spread * length) divergence = Math.Max(divergence, off);
                else farther = Math.Max(farther, off);
                mostTurned = Math.Max(mostTurned, Math.Abs(Air(blocked, x, y).X) / top);
                b = x;
            }
            double atUnderside = Air(blocked, 0, under * (1 - 1e-9)).Y, inside = Air(blocked, 0, under + 0.01).Y;
            Print($"wide underside 2R up: the fountain free to {freeShare * 100:F2} % of the plane jet's length ({length:F4} m from its virtual origin; "
                + $"measured 70-75 %), the layer {depth * 1e3:F2} mm (the body's own a/2 {a / 2 * 1e3:F0} mm), the linear fall within {worst:E1} of the free top's "
                + $"{top:F3} m/s, {atUnderside:E1} m/s at the underside, {inside} inside; continuity within {divergence:E1} of W_c/δ within a fifth of the jet's half-width, {farther:E2} on to {b * 1e3:F0} mm across (the free sheet's origin and momentum vary across it), "
                + $"the turned air at most {mostTurned:F3} W_c");
            Assert.That(freeShare, Is.EqualTo(0.725).Within(1e-6), "free to 72.5 % of the jet's length (the published 70-75 %)");
            Assert.That(depth, Is.LessThan(a / 2), "the jet's depth governs under a body wide to it");
            Assert.That(worst, Is.LessThan(1e-9), "the stagnation's linear fall");
            Assert.That(Math.Abs(atUnderside), Is.LessThan(1e-6 * top), "nothing rises at the underside");
            Assert.That(inside, Is.EqualTo(0), "no air inside the body");
            Assert.That(divergence, Is.LessThan(2e-2), "the turned air keeps the flow's continuity at the sheet's core");
            Assert.That(mostTurned, Is.LessThanOrEqualTo(1 + 1e-12), "never faster than the jet's centreline");
        }

        // A body narrower than the jet (a 10 mm rod along the sheet, 2R up): its own stagnation depth a/2, the potential flow's
        // slope at a cylinder's stagnation point (U·(1 − a²/r²) ≈ U·2d/a), governs; beside it the fountain is free; over it its
        // lee takes the rise to one diameter over its top (assumed), free beyond.
        [Test]
        public void ANarrowPartStagnatesAsItsPotentialFlowAndShadowsItsLee()
        {
            const double a = 0.005, under = 2 * R;
            ContactModel body = Body(Capsule(new Double3(0, under + a, -0.3), new Double3(0, under + a, 0.3), a));
            RotorAir free = Field(R, Dekker, Thrust, null), blocked = Field(R, Dekker, Thrust, body);
            double top = Air(free, 0, under - a / 2).Y;
            double d = 0.02e-3, potential = top * (1 - a * a / ((a + d) * (a + d)));
            double near = Air(blocked, 0, under - d).Y, beside = Air(blocked, 1.5 * a, under).Y, besideFree = Air(free, 1.5 * a, under).Y;
            double crown = under + 2 * a;
            double inLee = Air(blocked, 0, crown + 0.5 * 2 * a).Y, pastLee = Air(blocked, 0, crown + 1.5 * 2 * a).Y, pastFree = Air(free, 0, crown + 1.5 * 2 * a).Y;
            double halfway = Air(blocked, 0, under - a / 4).Y;
            Print($"narrow rod (a {a * 1e3:F0} mm) 2R up: {d * 1e3:F1} mm under it {near:F4} m/s against the potential flow's {potential:F4} ({near / potential:F4}); "
                + $"a/4 under it {halfway / top:F4} of the free top's {top:F3}; beside it (1.5a) {beside:F4} (free {besideFree:F4}); in its lee (0.5 D over its top) {inLee}, "
                + $"past it (1.5 D) {pastLee:F4} (free {pastFree:F4})");
            Assert.That(near / potential, Is.EqualTo(1).Within(0.01), "the potential flow's stagnation slope under a narrow part");
            Assert.That(halfway / top, Is.EqualTo(0.5).Within(1e-9), "linear over a/2");
            Assert.That(beside, Is.EqualTo(besideFree), "beside the part the fountain is free");
            Assert.That(inLee, Is.EqualTo(0), "nothing rises in its lee");
            Assert.That(pastLee, Is.EqualTo(pastFree), "free past its lee");
        }

        // The repository drone resting on its legs over the floor, its four rotors at a liftoff's thrust (v_h 10.1 m/s, as
        // RotorAirTests' cost case): the fountain on the body's centreline (the rear pair's and the front pair's sheet, x = 0)
        // with the body and through it (model 59's field), under the coil, at the old exit's column (just aft of the old coil's
        // cap), and between the rear discs where 1.8's disc cuts sat (0.15-0.18 m aft): blocked under and over the body, free where
        // nothing covers. Since model 64 the coil is D01's canister (its body's capsule at the centre, read from the shapes), and
        // its nozzle runs aft under the rear discs' gap to the exit at 0.205 m: there the fountain stagnates under it and is blocked
        // over it, up to its lee's top (body y −0.11 m), and from there up to the discs it is the field through the body.
        [Test]
        public void TheDronesBodyOverItsFountain()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            var model = new ContactModel(contacts, FlightWorld.Floor(), spec.BuildMass(spec.Nominal).Properties.Mass, random: new FlightRandom(1));
            const double r = 0.127, vh = 10.1;
            double thrust = 2 * Table.Number("fiber.air_density_kg_m3") * Math.PI * r * r * vh * vh;
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = spec.Motors[i].Position + new Double3(0, stand, 0);
            RotorAir Make(ContactModel body)
            {
                var f = new RotorAir(Table, r, true, body);
                if (body != null) f.PlaceBody(new Double3(0, stand, 0), Quat.Identity);
                f.Begin(hubs, new Double3(0, 1, 0), new[] { thrust, thrust, thrust, thrust }, new Double3[4], new double[4]);
                return f;
            }
            RotorAir with = Make(model), through = Make(null);
            double disc = hubs[0].Y;
            int same = 0, cells = 0, blocked = 0;
            foreach ((string name, double z) in new[] { ("under the coil", 0.0), ("the exit's column", -0.101), ("the rear discs' gap", -0.16), ("aft", -0.18) })
            {
                var line = new System.Text.StringBuilder($"{name} (z {z * 1e3:F0} mm):");
                for (double y = 0.02; y < disc; y += 0.02)
                {
                    double w = Air(with, 0.004, y, z).Y, t = Air(through, 0.004, y, z).Y;
                    line.Append($" {y - stand:+0.00;-0.00} m {w:F2}/{t:F2}");
                    if (z <= -0.16 && y - stand > -0.11) { cells++; if (w == t) same++; }
                    if (z <= -0.16 && y - stand <= -0.11 && w < t) blocked++;
                }
                Print(line + " (body y, with the body / through it, m/s)");
            }
            // The coil's capsule ("coil": the canister's body since model 64) at z 0: its axis's height there and its half height on
            // the vertical, r over the axis's cosine to the horizontal.
            ContactShape coil = Array.Find(contacts.Shapes, x => x.Name == "coil");
            Double3 ax = coil.B - coil.A;
            double axisY = coil.A.Y + ax.Y * (0 - coil.A.Z) / ax.Z, half = coil.Radius * ax.Length() / Math.Sqrt(ax.X * ax.X + ax.Z * ax.Z);
            double belly = stand + axisY - half; // the coil's lowest line under the centre, world
            double underCoil = Air(with, 0.004, belly - 0.002).Y, underFree = Air(through, 0.004, belly - 0.002).Y;
            double overCoil = Air(with, 0.004, stand + axisY + half + 0.004).Y, overFree = Air(through, 0.004, stand + axisY + half + 0.004).Y;
            Print($"2 mm under the coil's belly {underCoil:F3} m/s (through it {underFree:F3}); 4 mm over its top {overCoil} (through it {overFree:F3}); "
                + $"between the rear discs, 0.16-0.18 m aft, over the nozzle's lee {same} of {cells} heights the field through the body, bit for bit, under it {blocked} blocked");
            Assert.That(underCoil, Is.LessThan(0.2 * underFree), "the coil stagnates the rise under it");
            Assert.That(overCoil, Is.EqualTo(0), "nothing rises over it (its lee)");
            Assert.That(same, Is.EqualTo(cells), "nothing of the body stands over the nozzle's lee in the rear discs' gap: the fountain there is model 59's");
            Assert.That(blocked, Is.GreaterThan(0), "the canister's nozzle under the rear discs' gap blocks the fountain in its column (model 64)");
        }

        // The body's cost on the field over a line's 32 nodes laid under the resting drone and its allocation (Release; the
        // machine shared, so under load).
        [Test]
        public void TheBodyCostsLittleAndAllocatesNothing()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            var model = new ContactModel(contacts, FlightWorld.Floor(), spec.BuildMass(spec.Nominal).Properties.Mass, random: new FlightRandom(1));
            const double r = 0.127, vh = 10.1;
            double thrust = 2 * Table.Number("fiber.air_density_kg_m3") * Math.PI * r * r * vh * vh;
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = spec.Motors[i].Position + new Double3(0, stand, 0);
            var thrusts = new[] { thrust, thrust, thrust, thrust };
            var air = new Double3[4];
            var ground = new double[4];
            var nodes = new Double3[32];
            for (int i = 0; i < 32; i++) nodes[i] = new Double3(0.002 * i, i < 8 ? stand - 0.053 - 0.03 * i : 0.0002, 0.05 - 0.02 * i);
            RotorAir with = new RotorAir(Table, r, true, model), through = new RotorAir(Table, r, true, null);
            var up = new Double3(0, 1, 0);
            var at = new Double3(0, stand, 0);
            double sink = 0;
            void Step(RotorAir f)
            {
                if (f.BodyBlocks) f.PlaceBody(at, Quat.Identity);
                f.Begin(hubs, up, thrusts, air, ground);
                for (int i = 0; i < 32; i++) sink += f.At(nodes[i], 0, out _).Y;
            }
            for (int k = 0; k < 2000; k++) { Step(with); Step(through); } // the JIT's warm-up
            double[] ns = { double.PositiveInfinity, double.PositiveInfinity };
            long before = AllocProbe.Before();
            for (int round = 0; round < 40; round++)
            {
                RotorAir f = round % 2 == 0 ? with : through;
                long t0 = Stopwatch.GetTimestamp();
                for (int k = 0; k < 1000; k++) Step(f);
                double t = (Stopwatch.GetTimestamp() - t0) * 1e9 / Stopwatch.Frequency / 1000.0;
                if (t < ns[round % 2]) ns[round % 2] = t;
            }
            long allocated = AllocProbe.After(before);
            Print($"a step of the field over 32 nodes under the resting drone: {ns[0] / 1000:F2} µs with the body ({model.ShapeCount} shapes placed), "
                + $"{ns[1] / 1000:F2} µs through it (+{(ns[0] - ns[1]) / 1000:F2} µs; the least of 20 rounds, under load); {allocated} B ({sink:E1})");
            Assert.That(allocated, Is.Zero, "0 B in the steps" + AllocProbe.Report());
        }
    }
}
