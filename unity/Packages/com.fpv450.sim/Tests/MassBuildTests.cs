using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics: "Mass and inertia from the drone's components" (PHB-02, DRN-03). Roll is about body z
    // (forward), pitch about body x (right), yaw about body y (up), all about the centre of mass.
    public sealed class MassBuildTests
    {
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
    }
}
