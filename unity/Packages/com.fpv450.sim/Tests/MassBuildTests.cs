using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics: "Mass and inertia from the drone's components" (PHB-02, DRN-03). Roll is about body z
    // (forward), pitch about body x (right), yaw about body y (up), all about the centre of mass.
    public sealed class MassBuildTests
    {
        static MassModel Nominal()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            return spec.BuildMass(spec.Nominal);
        }

        [Test]
        public void TheBootstrapDroneMatchesK52()
        {
            MassProperties b = Nominal().Properties;
            double roll = b.Inertia.ZZ, pitch = b.Inertia.XX, yaw = b.Inertia.YY;
            TestContext.WriteLine($"mass {b.Mass:F4} kg, CoM ({b.CenterOfMass.X:F4}, {b.CenterOfMass.Y:F4}, {b.CenterOfMass.Z:F4}) m, "
                + $"roll {roll:F5} pitch {pitch:F5} yaw {yaw:F5} kg·m², yaw/roll {yaw / roll:F3}, "
                + $"products xy {b.Inertia.XY:F5} xz {b.Inertia.XZ:F5} yz {b.Inertia.YZ:F5}");

            Assert.That(b.Mass, Is.EqualTo(4.8).Within(1).Percent, "the mass");
            Assert.That(roll, Is.EqualTo(0.044).Within(5).Percent, "roll inertia");
            Assert.That(pitch, Is.EqualTo(0.054).Within(5).Percent, "pitch inertia");
            Assert.That(yaw, Is.EqualTo(0.050).Within(5).Percent, "yaw inertia");
            Assert.That(pitch, Is.GreaterThan(roll), "pitch above roll: the payload sits forward");
            Assert.That(yaw / roll, Is.InRange(1.0, 1.3), "yaw only 1.0–1.3 × roll");
        }

        // K§5.2's derived centre of mass with a full coil: 1–2 cm below the arm plane, 1–3 cm forward, and the coil's
        // centre 8–9 cm below it, so a side gust on the coil is also a rolling moment.
        [Test]
        public void AFullCoilPutsTheCentreOfMassWhereK52Says()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            Loadout full = spec.Nominal;
            full.CoilPaidOut = 0;
            MassProperties b = spec.BuildMass(full).Properties;
            double coilBelow = b.CenterOfMass.Y - spec.Parts[spec.Coil.Part].Position.Y;
            TestContext.WriteLine($"full coil: mass {b.Mass:F3} kg, CoM {b.CenterOfMass.Y * 100:F2} cm up, {b.CenterOfMass.Z * 100:F2} cm forward, coil {coilBelow * 100:F2} cm below it");
            Assert.That(-b.CenterOfMass.Y, Is.InRange(0.01, 0.02));
            Assert.That(b.CenterOfMass.Z, Is.InRange(0.01, 0.03));
            Assert.That(b.CenterOfMass.X, Is.EqualTo(0).Within(1e-12));
            Assert.That(coilBelow, Is.InRange(0.08, 0.09));
        }

        // The parallel-axis sum against a hand calculation: two 1 kg cubes of 0.1 m, 0.4 m apart along x, and a 2 kg rod
        // along the xz diagonal at the origin.
        [Test]
        public void TheBuildIsTheParallelAxisSum()
        {
            var parts = new[]
            {
                new DronePart { Name = "a", Kind = PartKind.Box, Mass = 1, Position = new Double3(-0.2, 0, 0), Size = new Double3(0.1, 0.1, 0.1) },
                new DronePart { Name = "b", Kind = PartKind.Box, Mass = 1, Position = new Double3(0.2, 0, 0), Size = new Double3(0.1, 0.1, 0.1) },
                new DronePart
                {
                    Name = "rod", Kind = PartKind.Cylinder, Mass = 2, Position = new Double3(0, 0, 0),
                    Diameter = 0.02, Length = 0.6, Axis = new Double3(Math.Sqrt(0.5), 0, Math.Sqrt(0.5)),
                },
            };
            MassProperties b = new MassModel(parts).Properties;

            double cube = 1 * (0.01 + 0.01) / 12;                  // each cube about its own centre
            double across = 2 * (3 * 0.0001 + 0.36) / 12, about = 2 * 0.0001 / 2; // the rod across and about its axis
            Assert.That(b.Mass, Is.EqualTo(4));
            Assert.That(b.CenterOfMass.Length(), Is.LessThan(1e-15));
            Assert.That(b.Inertia.XX, Is.EqualTo(2 * cube + (across + about) / 2).Within(1e-15), "x: the cubes lie on it");
            Assert.That(b.Inertia.YY, Is.EqualTo(2 * cube + 2 * 0.04 + across).Within(1e-15), "y: 2·m·d² and the rod across");
            Assert.That(b.Inertia.ZZ, Is.EqualTo(2 * cube + 2 * 0.04 + (across + about) / 2).Within(1e-15));
            Assert.That(b.Inertia.XZ, Is.EqualTo((about - across) / 2).Within(1e-15), "the rod's product of inertia");
            Assert.That(b.Inertia.XY, Is.EqualTo(0).Within(1e-18));
            Assert.That(b.Inertia.YZ, Is.EqualTo(0).Within(1e-18));
        }

        // The payload setting and the coil paying out change one part's mass, and the build follows: the total by the
        // change, the CoM by the lever rule, and the same bits as a model built afresh at those masses.
        [Test]
        public void APartsMassChangeRebuildsTheBody()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            MassModel model = Nominal();
            MassProperties before = model.Properties;
            int payload = spec.Payload.Part, coil = spec.Coil.Part;

            model.SetPartMass(payload, 2.0);
            MassProperties heavy = model.Properties;
            Double3 lever = (spec.Parts[payload].Position - before.CenterOfMass) * (1.0 / heavy.Mass);
            Assert.That(heavy.Mass, Is.EqualTo(before.Mass + 1.0).Within(1e-15));
            Assert.That((heavy.CenterOfMass - (before.CenterOfMass + lever)).Length(), Is.LessThan(1e-15), "the lever rule");
            Assert.That(heavy.Inertia.XX, Is.GreaterThan(before.Inertia.XX), "more pitch inertia with the payload forward and below");

            double empty = spec.CoilMass(spec.Coil.FiberLength);
            Assert.That(empty, Is.EqualTo(spec.Coil.SpoolMass).Within(1e-15));
            model.SetPartMass(coil, empty);
            Assert.That(model.Properties.CenterOfMass.Y, Is.GreaterThan(heavy.CenterOfMass.Y), "the CoM rises as the line pays out");

            var loadout = new Loadout { Payload = 2.0, Legs = true, CoilPaidOut = spec.Coil.FiberLength };
            MassProperties fresh = spec.BuildMass(loadout).Properties;
            Assert.That(Bits(model.Properties), Is.EqualTo(Bits(fresh)), "a rebuild is a build");
        }

        [Test]
        public void LegsOffTakeTheLegsOut()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            Loadout off = spec.Nominal;
            off.Legs = false;
            double legs = 0;
            foreach (int leg in spec.Legs.Parts) legs += spec.Parts[leg].Mass;
            Assert.That(spec.BuildMass(off).Properties.Mass, Is.EqualTo(Nominal().Properties.Mass - legs).Within(1e-12));
        }

        [Test]
        public void ALoadoutOutsideTheSpecIsRefused()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            Loadout l = spec.Nominal;
            l.Payload = 2.5;
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.BuildMass(l));
            l = spec.Nominal;
            l.CoilPaidOut = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.BuildMass(l));
        }

        static long[] Bits(in MassProperties b) => new[]
        {
            BitConverter.DoubleToInt64Bits(b.Mass), BitConverter.DoubleToInt64Bits(b.CenterOfMass.X),
            BitConverter.DoubleToInt64Bits(b.CenterOfMass.Y), BitConverter.DoubleToInt64Bits(b.CenterOfMass.Z),
            BitConverter.DoubleToInt64Bits(b.Inertia.XX), BitConverter.DoubleToInt64Bits(b.Inertia.YY),
            BitConverter.DoubleToInt64Bits(b.Inertia.ZZ), BitConverter.DoubleToInt64Bits(b.Inertia.XY),
            BitConverter.DoubleToInt64Bits(b.Inertia.XZ), BitConverter.DoubleToInt64Bits(b.Inertia.YZ),
        };
    }
}
