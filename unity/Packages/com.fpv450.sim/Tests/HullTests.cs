using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The hull's moment (p7c-wind design 12, Aero.Hull; off by default, D-107): drone.json's measured curves are Rhudy and Baker's tables, the moment's
    /// sign and size at clip P's cruise trim by hand in both planes, and its known-wrong, the hull's potential-flow couple, which
    /// turns the hull the other way.
    /// </summary>
    public sealed class HullTests
    {
        const double DegToRad = 0.017453292519943295, Rho = 1.225;

        static Aero RepositoryAero(bool potential = false)
        {
            AeroSpec spec = RepositoryDrone.Spec().BuildAero();
            spec.HullPotential = potential;
            spec.HullOn = true;
            return new Aero(spec);
        }

        [Test]
        public void TheHullIsOffUnlessAFlightAsksForIt()
        {
            // D-107: built, graded, not flown; drone.json's data is read and logged, the moment off by default.
            AeroSpec spec = RepositoryDrone.Spec().BuildAero();
            Assert.That(spec.HullFrontalArea, Is.GreaterThan(0));
            Assert.That(spec.HullOn, Is.False);
            Assert.That(new Aero(spec).HasHull, Is.False);
            Assert.That(RepositoryAero().HasHull, Is.True);
        }

        [Test]
        public void TheCurvesAreRhudyAndBakersTables()
        {
            AeroSpec a = RepositoryDrone.Spec().BuildAero();
            Assert.That(a.HullRatios, Is.EqualTo(new[] { 0.75, 1.15, 1.528 }));
            // AEDC-TR-72-180's tabulated C_m at M∞ 0.39–0.41, less each table's reading at 0° (+0.0157, −0.0355, −0.0255).
            (int curve, double alpha, double table, double zero)[] points =
            {
                (0, 9.94, -0.0288, 0.0157), (0, 24.89, -0.0721, 0.0157), (1, 9.97, -0.1288, -0.0355), (1, 29.96, -0.0813, -0.0355),
                (2, 10.0, -0.1951, -0.0255), (2, 30.05, -0.0099, -0.0255),
            };
            foreach (var p in points)
            {
                int i = Array.IndexOf(a.HullAngles[p.curve], p.alpha);
                Assert.That(i, Is.GreaterThan(0), $"L/d {a.HullRatios[p.curve]} at {p.alpha}°");
                Assert.That(a.HullMoments[p.curve][i], Is.EqualTo(p.table - p.zero).Within(1e-12));
            }
            // Each curve restores (the nose back into the flow) from 0° to where the report's own data turn: past 73°, 39° and 28°.
            var aero = new Aero(a);
            foreach (var (ratio, upTo) in new[] { (0.75, 73.0), (1.15, 39.0), (1.528, 28.0) })
                for (double deg = 1; deg <= upTo; deg += 1)
                    Assert.That(aero.Coefficient(deg * DegToRad, ratio), Is.LessThan(0), $"L/d {ratio} at {deg}°");
            // Odd in the incidence, and a backward body's mirror.
            Assert.That(aero.Coefficient(-12 * DegToRad, 1.0), Is.EqualTo(-aero.Coefficient(12 * DegToRad, 1.0)));
            Assert.That(aero.Coefficient(168 * DegToRad, 1.0), Is.EqualTo(-aero.Coefficient(12 * DegToRad, 1.0)).Within(1e-15));
        }

        [Test]
        public void AtTheCruiseTheHullTurnsItsNoseIntoTheAirInBothPlanes()
        {
            AeroSpec spec = RepositoryDrone.Spec().BuildAero();
            Aero aero = RepositoryAero();
            // Clip P's trim: 20.5 m/s at 22.4° nose down, so the body moves forward and up through the air on its own axes.
            double t = 22.4 * DegToRad, v = 20.5;
            var cruise = new Double3(0, v * Math.Sin(t), v * Math.Cos(t));
            Double3 m = aero.Hull(cruise);
            // By hand: L/H = 0.177 / 0.295 lies under the measured 0.75, so the 0.75 curve, between 19.91° (−0.0756) and 22.96° (−0.0862).
            double ratio = spec.HullSize.Z / spec.HullSize.Y;
            Assert.That(ratio, Is.LessThan(0.75));
            double cm = -0.0756 + (22.4 - 19.91) / (22.96 - 19.91) * (-0.0862 + 0.0756);
            double hand = cm * 0.5 * Rho * spec.HullFrontalArea * v * v * spec.HullSize.Y; // + nose up away from the flow: here it pulls the nose up
            TestContext.WriteLine($"the hull at the cruise: {m.X:F4} N·m about x (by hand {hand:F4}: the nose up, back into the air), {m.Y:E2} about y");
            Assert.That(m.X, Is.EqualTo(hand).Within(1e-9), "−x is nose up: the nose, down from the flow, turns back into it");
            Assert.That(m.X, Is.InRange(-0.20, -0.14));
            Assert.That(Math.Abs(m.Y) + Math.Abs(m.Z), Is.LessThan(1e-15), "no yaw in a symmetric flow");

            // An updraft (the air from below) eases the incidence: the nose-up moment falls, a nose-down step, into the air.
            Double3 up = aero.Hull(cruise + new Double3(0, -Math.Cos(t), Math.Sin(t)));
            TestContext.WriteLine($"per m/s of updraft: {up.X - m.X:+0.0000;-0.0000} N·m about x");
            Assert.That(up.X - m.X, Is.GreaterThan(0));

            // The air from the right (the body moving right through it): the nose turns right, into the air (+y).
            Double3 side = aero.Hull(new Double3(5, cruise.Y, cruise.Z));
            double yawRatio = spec.HullSize.Z / spec.HullSize.X, beta = Math.Atan2(5, cruise.Z);
            double handYaw = -aero.Coefficient(beta, yawRatio) * 0.5 * Rho * spec.HullFrontalArea * (25 + cruise.Z * cruise.Z) * spec.HullSize.X;
            TestContext.WriteLine($"5 m/s from the right: {side.Y:F4} N·m about y (L/W {yawRatio:F3}, β {beta / DegToRad:F1}°)");
            Assert.That(side.Y, Is.GreaterThan(0));
            Assert.That(side.Y, Is.EqualTo(handYaw).Within(1e-12));
            Assert.That(side.X, Is.EqualTo(m.X), "the planes apart");
        }

        [Test]
        public void ThePotentialCoupleTurnsTheHullTheOtherWay()
        {
            // The known-wrong: ρ(A·v) × v with the hull's added volume, Munk's couple of the same body in attached flow.
            Aero potential = RepositoryAero(potential: true), measured = RepositoryAero();
            AeroSpec spec = potential.Spec;
            double t = 22.4 * DegToRad, v = 20.5;
            var cruise = new Double3(0, v * Math.Sin(t), v * Math.Cos(t));
            Sym3 a = spec.HullAddedVolume;
            double vy = cruise.Y, vz = cruise.Z;
            double hand = Rho * ((a.YY - a.ZZ) * vy * vz + a.YZ * (vz * vz - vy * vy));
            Double3 m = potential.Hull(cruise);
            TestContext.WriteLine($"the potential couple at the cruise: {m.X:F4} N·m about x (by hand {hand:F4}); the measured moment {measured.Hull(cruise).X:F4}");
            Assert.That(m.X, Is.EqualTo(hand).Within(1e-12));
            Assert.That(m.X, Is.GreaterThan(0), "nose down: away from the air");
            Assert.That(measured.Hull(cruise).X, Is.LessThan(0));
            Double3 side = potential.Hull(new Double3(5, cruise.Y, cruise.Z));
            Assert.That(side.Y, Is.LessThan(0), "the nose turns left, away from the air from the right");
        }
    }
}
