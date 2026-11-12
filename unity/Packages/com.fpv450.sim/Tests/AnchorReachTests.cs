using System;
using System.Collections.Generic;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // "A contact moment has a force behind it" (w9-village-physics 2.4, G1; PHY-01, PHY-04): in no step does the contacts' moment
    // about the centre of mass pass the size of their force times the drone's reach, over the pilot's cut landing (0.5 m/s, the
    // throttle cut at the touch) along and across every slope row of flightbench --slopes, the audit's package rows and its
    // farmland spots. Before 2.4 a sliding soil anchor's stretch ran to tens of metres and the foot, the moment's lever, with it
    // (1,455 N·m on 41.5 N at 9.98° weeds, audit §1.6).
    public sealed class AnchorReachTests
    {
        static DroneSpec _drone;
        static DroneSpec Drone() => _drone ??= DroneSpec.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));

        static void Print(string s) => TestContext.Progress.WriteLine(s);

        /// <summary>The drone's reach about its centre of mass (m, derived: the skids' and props' farthest points lie within 0.6 m
        /// across, so no contact point lies more than about 0.5 m from the centre of mass).</summary>
        public const double Reach = 0.5;

        // flightbench --slopes' landing rows (meadow_sod) and the audit's package rows (1.5, 1.6): (deg, surface).
        static readonly (double Deg, string Surface)[] Packages =
        {
            (0, "meadow_sod"), (10, "meadow_sod"), (20, "meadow_sod"), (30, "meadow_sod"), (38, "meadow_sod"),
            (10, "weeds"), (9.43, "meadow_sod"), (14.47, "meadow_sod"), (14.36, "weeds"), (19.70, "meadow_sod"),
        };

        // The audit's farmland spots (1.1's slope spots, without the 35.65° one inside a bush's crown).
        static readonly (double X, double Z)[] Spots =
        {
            (313.04, 20.00), (311.19, 50.71), (884.69, 291.58), (586.47, 20.37), (507.29, 572.78), (522.83, 587.58), (523.94, 596.09),
        };

        static string Row(string name, in SlopeScenarios.Touchdown t) =>
            $"{name}: facet {t.Facet:F2}°, lever {t.Lever:F3} m ({t.LeverMoment:F2} N·m) at {t.LeverStep} ms, unbacked {t.Unbacked:F2} N·m, "
            + $"peak {t.PeakAccel:F0} m/s², tilt max/end {t.MaxTilt:F1}/{t.EndTilt:F1}°, crashes {t.Crashes}, tunnels {t.Tunnels}";

        [Test]
        public void OnEverySlopeRow_TheContactMomentHasAForceBehindIt()
        {
            var over = new List<string>();
            foreach (var (deg, surface) in Packages)
                foreach (double yaw in new[] { 0.0, 90 })
                {
                    SlopeScenarios.Touchdown t = SlopeScenarios.Land(Drone(), deg, RainLevel.Sunny, yaw, surface: surface);
                    string row = Row($"package {deg}° {surface} yaw {yaw}", t);
                    Print(row);
                    if (t.Lever > Reach || t.Unbacked > 0) over.Add(row);
                }
            foreach (var (x, z) in Spots)
                foreach (double yaw in new[] { 0.0, 90 })
                {
                    SlopeScenarios.Touchdown t = SlopeScenarios.LandOnFarmland(Drone(), x, z, yaw);
                    string row = Row($"farmland ({x}, {z}) yaw {yaw}", t);
                    Print(row);
                    if (t.Lever > Reach || t.Unbacked > 0) over.Add(row);
                }
            Assert.That(over, Is.Empty, "a contact moment past the force's size times the reach:\n" + string.Join("\n", over));
        }

        // The known-wrong: model 85's rule fails the same bound on the audit's row (1,455 N·m on 41.5 N at 9.98° weeds, along).
        [Test]
        public void WithoutTheReach_TheWeedsLandingHasAMomentWithNoForceBehindIt()
        {
            SlopeScenarios.Touchdown wrong = SlopeScenarios.Land(Drone(), 10, RainLevel.Sunny, 0, surface: "weeds", prepare: GoldenFlights.WithoutTheReach);
            SlopeScenarios.Touchdown right = SlopeScenarios.Land(Drone(), 10, RainLevel.Sunny, 0, surface: "weeds");
            Print(Row("model 85's slide", wrong));
            Print(Row("the reach", right));
            Assert.That(wrong.Lever, Is.GreaterThan(10 * Reach), "the known-wrong fails the bound");
            Assert.That(right.Lever, Is.LessThanOrEqualTo(Reach));
        }
    }
}
