using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7f-damage 1.6's losses (prop-motor-damage's "Breakage"; LossScenarios): a whole prop lost, a stopped motor and one blade lost,
    // each on model 72 and on model 68's clean sensors (the known-wrong). tools/flightbench --damage prints them over the loads.
    public sealed class LossTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        // "A lost prop or a stopped motor drops its corner": lift and yaw torque go together at one corner, and three rotors cannot
        // balance roll, pitch and yaw under Betaflight's mixer (Mueller and D'Andrea 2014: a quadrotor holds a point on three only by
        // giving up yaw and spinning). From the hover and from the cruise, the sticks centred and the simulated pilot flying, the
        // drone rolls toward the dead corner past 45° within the second, that corner lowest; the clean sensors do the same.
        [Test]
        public void ALostPropOrAStoppedMotorDropsItsCorner()
        {
            DroneSpec spec = Spec;
            foreach (LossScenarios.Loss loss in new[] { LossScenarios.Loss.Prop, LossScenarios.Loss.Stopped })
                foreach (bool cruise in new[] { false, true })
                    foreach (bool centred in new[] { true, false })
                    {
                        LossScenarios.Result r = LossScenarios.Fly(spec, loss, cruise, 1.0, centred, 1);
                        LossScenarios.Result clean = LossScenarios.Fly(spec, loss, cruise, 1.0, centred, 1, clean: true);
                        TestContext.Out.WriteLine(r.Describe());
                        TestContext.Out.WriteLine("  " + clean.Describe());
                        Assert.That(LossScenarios.DropsItsCorner(r), Is.True, r.Describe());
                        Assert.That(LossScenarios.DropsItsCorner(clean), Is.True, clean.Describe());
                    }
        }

        // "A lost blade shakes the drone and pulls at its hub", at steady throttle: the remaining blade lifts the corner (about 0.6
        // of the rotor's thrust; its motor's command rises and stays short of full at the class's loads, hover and cruise), the 1/rev
        // reaches the gyro at a few hundred °/s at most, far inside its ±2000, the ESC's commutation moves far less than its window,
        // and the arm in its plane carries it, twisting several degrees at 1/rev short of its strength: the drone holds its attitude.
        // The clean sensors (model 68's rigid arm) hold too, with no twist: the arm's twist is what they leave out.
        [Test]
        public void ALostBladeHoldsAtSteadyThrottle()
        {
            DroneSpec spec = Spec;
            foreach (bool cruise in new[] { false, true })
                foreach (double payload in new[] { 0.0, 2.0 })
                    foreach (bool centred in new[] { true, false })
                    {
                        LossScenarios.Result r = LossScenarios.Fly(spec, LossScenarios.Loss.Blade, cruise, payload, centred, 1);
                        LossScenarios.Result clean = LossScenarios.Fly(spec, LossScenarios.Loss.Blade, cruise, payload, centred, 1, clean: true);
                        TestContext.Out.WriteLine(r.Describe());
                        TestContext.Out.WriteLine("  " + clean.Describe());
                        Assert.That(LossScenarios.BladeHolds(r), Is.True, r.Describe());
                        Assert.That(r.ThrustShare, Is.InRange(0.5, 0.65), "about 0.6 of the rotor's thrust left");
                        Assert.That(r.Pull, Is.GreaterThan(200), "the lost blade's pull, hundreds of newtons");
                        Assert.That(r.GyroPeak, Is.LessThan(0.25 * 2000), "the gyro far inside its range");
                        Assert.That(r.CommutationChange, Is.LessThan(0.1 * LossScenarios.CommutationWindow), "no desync: the ESC tracks its rotor");
                        Assert.That(r.Twist, Is.GreaterThan(5), "the arm twists at 1/rev");
                        Assert.That(r.ArmLoad, Is.InRange(0.2, 0.8), "short of its strength");
                        Assert.That(clean.Twist, Is.Zero, "the rigid arm does not");
                    }
        }

        // "A lost blade's chop breaks its arm": with a lost blade, the throttle stick taken to 0.15 for half a second (a pilot easing
        // off a shaking drone, or a descent) takes the rotor down through its arm's twist mode near 88 Hz; there the 1/rev twists the
        // arm past its strength, it breaks at its clamp (ArmBroken), its prop is lost and, the throttle back, the drone rolls toward
        // that corner. The clean sensors' rigid arm never breaks and the drone holds: the known-wrong fails.
        [Test]
        public void ALostBladesChopBreaksItsArm()
        {
            DroneSpec spec = Spec;
            foreach (double payload in new[] { 0.0, 1.0, 2.0 })
            {
                LossScenarios.Result r = LossScenarios.Fly(spec, LossScenarios.Loss.Blade, false, payload, true, 1, chop: true);
                LossScenarios.Result clean = LossScenarios.Fly(spec, LossScenarios.Loss.Blade, false, payload, true, 1, clean: true, chop: true);
                TestContext.Out.WriteLine(r.Describe());
                TestContext.Out.WriteLine("  " + clean.Describe());
                Assert.That(LossScenarios.ChopBreaksTheArm(r), Is.True, r.Describe());
                Assert.That(double.IsNaN(clean.BrokeAt) && double.IsNaN(clean.TiltAt), Is.True, "the known-wrong holds: " + clean.Describe());
            }
        }

        // The arms in their plane carrying a lost blade's 1/rev, through an arm's break (ArmBroken, LoseProp) and the gyro's range,
        // allocate nothing in a step.
        [Test]
        public void TheArmsAndTheGyroRangeAllocateNothing()
        {
            DroneSpec spec = Spec;
            var core = new FlightCore(LossScenarios.Setup(spec, 1, 1.0, false));
            CrosswindScenario.Arm(core);
            var stick = new StickSample { Arm = true, Throttle = 0.5 };
            for (int k = 0; k < 100; k++)
            {
                stick.HostUs = core.State.Step * FlightCore.StepUs;
                core.Advance(stick);
            }
            core.Propulsion.Rotor(LossScenarios.Corner).Blade(0).Trim(spec.Blade.Stations[0]);
            core.Propulsion.Rebuild(LossScenarios.Corner);
            long start = AllocProbe.Before();
            for (int k = 0; k < 1500; k++)
            {
                stick.HostUs = core.State.Step * FlightCore.StepUs;
                stick.Throttle = k > 300 && k < 800 ? 0.15 : 0.5;
                core.Advance(stick);
                core.CameraVibration();
            }
            long allocated = AllocProbe.After(start);
            TestContext.Out.WriteLine($"  {allocated} B over 1500 steps; rr's arm broken: {(core.State.ArmBroken & 1) != 0}");
            Assert.That(allocated, Is.Zero, AllocProbe.Report());
        }
    }
}
