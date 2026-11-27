using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The pilot's "fly underneath a house and go full throttle" (w20-hunter-track design 11) on FloorScenarios' timber floor: what
    /// meets the floor is the drone's own top, so with the bottle strapped on top (D-050) the props stay off it and full throttle
    /// only pins the drone and heats its motors until one fails, in about a minute and a half; without the
    /// bottle, or leaned about it, the props reach the floor and the blades, the rotors and the motors pay within seconds. The
    /// pilot's 2 kg bottle throughout.
    /// </summary>
    public class FloorTests
    {
        // Flight 35's crawl space at full throttle: the bottle pinned to the floor, the props 9 cm under it, no blade pass, the
        // motors at full duty warming.
        [Test]
        public void UnderAFloorAtFullThrottleTheBottleHoldsThePropsOff()
        {
            FloorScenarios.Pinned r = FloorScenarios.UnderTheFloor(RepositoryDrone.Spec(), FloorScenarios.PilotPayload, 8);
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

        // Held there: the first motor fails by heat (MotorHealth: its windings past the insulation's 220 °C rating short a coil, past
        // the enamel's 300 °C breakdown it fails) a minute or two after full throttle, with no blade having met the floor before.
        [Test]
        public void PinnedAtFullThrottleAMotorFailsByHeat()
        {
            FloorScenarios.Pinned r = FloorScenarios.UnderTheFloor(RepositoryDrone.Spec(), FloorScenarios.PilotPayload, 180);
            TestContext.Out.WriteLine(r.Describe());
            double failed = r.FirstFailure() - 1; // from full throttle
            Assert.That(failed, Is.InRange(45.0, 179.0), "a motor fails by heat, in a minute or two");
            Assert.That(r.FirstShort() - 1, Is.LessThan(failed), "a coil shorts first");
            Assert.That(r.PassesBeforeFailure, Is.Zero, "no blade met the floor while the bottle held the drone");
            Assert.That(r.Tunnels, Is.Zero);
        }

        // The known-wrong of the first case, a drone with nothing above its props: the same press puts the blades into the floor;
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

        // Design 11's addendum: without the bottle the props press flat into the floor (the motor bells stop the drone with the
        // blades' slab 1.4 mm into the timber, drone.json's), level or with the floor tilted up to 15°. At every angle they meet it as
        // the bare drone's hover climbs into it, about 0.6 s in, jam within 0.1 s, and within half a second the jammed blades,
        // dragged as the drone moves, break or chip: by 6 s every angle has chipped or lost a blade, the motors still turning.
        [TestCase(0.0)]
        [TestCase(1.0)]
        [TestCase(2.0)]
        [TestCase(5.0)]
        [TestCase(15.0)]
        public void FlatUnderTheFloorThePropsMeetItAtEveryAngle(double tilt)
        {
            FloorScenarios.Pinned r = FloorScenarios.UnderTheFloor(RepositoryDrone.Spec(), 0, FloorScenarios.TiltSeconds, tilt: tilt);
            TestContext.Out.WriteLine(r.Describe());
            Assert.That(r.FirstPassAt, Is.LessThan(1.0), "the props meet the floor in the first second");
            Assert.That(r.Passes, Is.GreaterThan(100), "and keep meeting it");
            Assert.That(r.FirstJamAt - r.FirstPassAt, Is.InRange(0.0, 0.1), "a carve the rotor cannot pay stops it at once");
            Assert.That(r.FirstDamageAt - r.FirstPassAt, Is.InRange(0.0, 0.5), "the blades are damaged within half a second");
            Assert.That(r.ShortestTip, Is.LessThan(0.9), "a blade chipped or lost");
            Assert.That(r.Tunnels, Is.Zero);
        }

        // The gap it closes: the lead disc clamped at full duty under a timber face, its slab's deepest point 1 mm in, from flat to
        // leaned. Model 108 made no pass under 1° (the face met at radius 0, inside the blade's root); now every angle carves, the
        // first pass's work falls as the face leans away (693 J flat, about 1 J at 20°) and the rotor's speed after 1 s rises with
        // it, stalled flat and near free at 20°.
        [Test]
        public void ClampedFlatTheBladesMeetTheFaceAtEveryAngle()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double[] angles = { 0, 0.5, 1, 2, 3, 5, 10, 15, 20 };
            double work = double.PositiveInfinity, speed = 0;
            foreach (double a in angles)
            {
                FloorScenarios.Press r = FloorScenarios.Pressed(spec, a, 0.001, 1);
                TestContext.Out.WriteLine($"{a:F1}°: " + r.Describe());
                Assert.That(r.Passes, Is.GreaterThan(100), a + "°: the blades meet the face");
                Assert.That(r.First.Mode, Is.EqualTo(StrikeMode.Carve), a + "°: timber is carved");
                Assert.That(r.First.Work, Is.LessThanOrEqualTo(work), a + "°: less of the face past the slab as it leans away");
                Assert.That(r.MeanSpeed, Is.GreaterThanOrEqualTo(speed - 20), a + "°: the rotor turns faster as it leans away");
                work = r.First.Work;
                speed = r.MeanSpeed;
            }
            FloorScenarios.Press flat = FloorScenarios.Pressed(spec, 0, 0.001, 1), steep = FloorScenarios.Pressed(spec, 20, 0.001, 1);
            Assert.That(flat.MeanSpeed, Is.LessThan(50), "flat, the carve stalls the rotor");
            Assert.That(steep.MeanSpeed, Is.GreaterThan(1200), "at 20° it barely slows it");
            // The known-wrong: model 108's law (no blade inside its root) leaves a flat face untouched.
            foreach (double a in new[] { 0.0, 0.5 })
                Assert.That(FloorScenarios.Pressed(spec, a, 0.001, 1, faces: false).Passes, Is.Zero, a + "°: model 108 made no pass");
        }

        // No jump: at 2°, the face pressed from where its chord on the disc lies outside the blade's root (r_m 18 mm) to past the
        // hub. The first pass's work grows smoothly with the depth through the root (r_m 12.7 mm, about 3.99 mm) and the hub (4.43
        // mm), where model 108's dropped from 2.3 kJ to nothing.
        [Test]
        public void PressedThroughTheBladesRootTheCarveGrowsWithoutAJump()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double last = 0;
            for (int k = 0; k <= 8; k++)
            {
                double depth = 0.0038 + 0.0001 * k;
                FloorScenarios.Press r = FloorScenarios.Pressed(spec, 2, depth, 0.05);
                TestContext.Out.WriteLine($"{depth * 1000:F2} mm: r_m {r.First.Radius * 1000:F2} mm, work {r.First.Work:F1} J");
                Assert.That(r.First.Radius, Is.GreaterThanOrEqualTo(0.0127 - 1e-9), "met at the blade's root at the nearest");
                if (k > 0) Assert.That(r.First.Work / last, Is.InRange(1.0, 1.1), depth * 1000 + " mm: no jump");
                last = r.First.Work;
            }
            Assert.That(FloorScenarios.Pressed(spec, 2, 0.0041, 0.05, faces: false).Passes, Is.Zero, "model 108's gap past the root");
        }

        // A hard face flat on the disc: the blades bend over it by the slab's depth (k_f·p/sin β, 1.75 N a millimetre) and slide and
        // wear, at 0° as at 15° (model 108's k_f·p·cot β was nothing flat); concrete never jams the rotor, it only slows it.
        [Test]
        public void FlatOnConcreteTheBladesSlideAndWear()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (double a in new[] { 0.0, 1, 2, 5, 15 })
            {
                FloorScenarios.Press r = FloorScenarios.Pressed(spec, a, 0.001, 1, "concrete");
                TestContext.Out.WriteLine($"{a:F0}°: " + r.Describe());
                Assert.That(r.First.Mode, Is.EqualTo(StrikeMode.Wear), a + "°: it bends over the face and slides");
                Assert.That(r.First.BendForce, Is.InRange(1.5, 2.1), a + "°: the tip bent by its millimetre against k_f");
                Assert.That(r.Jams, Is.Zero, a + "°");
                Assert.That(r.Damaged, Is.GreaterThan(100), a + "°: worn every pass");
                Assert.That(r.MeanSpeed, Is.InRange(1250.0, 1520.0), a + "°: slowed, not stopped");
            }
            Assert.That(FloorScenarios.Pressed(spec, 0, 0.001, 1, "concrete", faces: false).Passes, Is.Zero, "model 108 made no pass flat");
        }

        // Pinned, then leaned about the bottle (a roll stick held from 3 s): the props on the low side reach the floor and chip.
        [Test]
        public void LeanedAboutItsBottleThePropsReachTheFloorAndChip()
        {
            FloorScenarios.Pinned r = FloorScenarios.UnderTheFloor(RepositoryDrone.Spec(), FloorScenarios.PilotPayload, 7, roll: 0.2);
            TestContext.Out.WriteLine(r.Describe());
            Assert.That(r.Passes, Is.GreaterThan(0), "the leaned props reach the floor");
            Assert.That(r.Damaged, Is.GreaterThan(0), "and chip");
            Assert.That(r.ShortestTip, Is.LessThan(1));
            Assert.That(r.Tunnels, Is.Zero);
        }
    }
}
