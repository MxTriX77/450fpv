using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The pilot's "fly underneath a house and go full throttle" (w20-hunter-track, D-122) on FloorScenarios' timber floor: what
    /// meets the floor is the drone's own top, so with the bottle strapped on top (D-050) the props stay off it and full throttle
    /// only pins the drone and heats its motors (a motor's failure by heat is `flightbench --floor`'s, about 1.5 min); without the
    /// bottle, or leaned about it, the props reach the floor and the blades, the rotors and the motors pay within seconds.
    /// </summary>
    public class FloorTests
    {
        // Flight 35's crawl space at full throttle: the bottle pinned to the floor, the props 9 cm under it, no blade pass, the
        // motors at full duty warming.
        [Test]
        public void UnderAFloorAtFullThrottleTheBottleHoldsThePropsOff()
        {
            FloorScenarios.Pinned r = FloorScenarios.UnderTheFloor(RepositoryDrone.Spec(), RepositoryDrone.Spec().Nominal.Payload, 8);
            TestContext.Out.WriteLine(r.Describe());
            Assert.That(r.PressedAt, Is.LessThan(3), "full throttle presses the drone to the floor");
            Assert.That(r.Push, Is.GreaterThan(40), "with its thrust less its weight");
            Assert.That(r.Passes, Is.Zero, "the bottle on top meets the floor; no blade does");
            Assert.That(r.Clearance, Is.GreaterThan(0.05), "the props stay under the floor by about the bottle's height over them");
            Assert.That(r.Amps, Is.GreaterThan(120), "the motors run at full duty");
            Assert.That(System.Math.Max(System.Math.Max(r.PeakWinding[0], r.PeakWinding[1]), System.Math.Max(r.PeakWinding[2], r.PeakWinding[3])),
                Is.GreaterThan(40), "and warm");
            Assert.That(r.FirstFailure(), Is.EqualTo(double.PositiveInfinity), "not yet failing in 7 s");
            Assert.That(r.Tunnels, Is.Zero);
        }

        // The known-wrong of the case above, a drone with nothing above its props: the same press puts the blades into the floor;
        // they carve it, jam and break down to stubs within seconds (a rotor that stays jammed instead heats its motor to a short:
        // HeatTests.AStalledMotorHeatsAndFails).
        [Test]
        public void WithoutTheBottleThePropsMeetTheFloorAndBreak()
        {
            FloorScenarios.Pinned r = FloorScenarios.UnderTheFloor(RepositoryDrone.Spec(), 0, 5);
            TestContext.Out.WriteLine(r.Describe());
            Assert.That(r.Passes, Is.GreaterThan(0), "the blades meet the floor");
            Assert.That(r.Jams, Is.GreaterThan(0), "a carve the rotor cannot pay stops it");
            Assert.That(r.Damaged, Is.GreaterThan(0), "and the blades break");
            Assert.That(r.ShortestTip, Is.LessThan(0.5), "a blade broken to its root");
            Assert.That(r.Tunnels, Is.Zero);
        }

        // Leaned about the bottle (a roll stick held under the floor): the props on the low side reach the floor and chip.
        [Test]
        public void LeanedAboutItsBottleThePropsReachTheFloorAndChip()
        {
            FloorScenarios.Pinned r = FloorScenarios.UnderTheFloor(RepositoryDrone.Spec(), RepositoryDrone.Spec().Nominal.Payload, 5, roll: 0.2);
            TestContext.Out.WriteLine(r.Describe());
            Assert.That(r.Passes, Is.GreaterThan(0), "the leaned props reach the floor");
            Assert.That(r.Damaged, Is.GreaterThan(0), "and chip");
            Assert.That(r.ShortestTip, Is.LessThan(1));
            Assert.That(r.Tunnels, Is.Zero);
        }
    }
}
