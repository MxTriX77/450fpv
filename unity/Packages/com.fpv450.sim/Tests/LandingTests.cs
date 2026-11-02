using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// D-067 (p6-flight-core 1.6c): the sim's toppling and the coil's rock against rigid-body mechanics (LandingStability),
    /// on the stub's rigid floor, as free bodies (the mechanics' own case) unless a test says otherwise. Each check fails
    /// a known-wrong model: a frictionless skid, a coil on an axle, and the armed flight controller's hold.
    /// </summary>
    public class LandingTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        static MaterialRow[] Materials(out ulong hash) => ContactScenarios.RepositoryMaterials(out hash);

        static LandingStability LegsOn()
        {
            MaterialRow[] m = Materials(out ulong h);
            Loadout l = Spec.Nominal;
            return LandingStability.Legs(Spec, Spec.BuildContacts(l, m, h), Spec.BuildMass(l).Properties);
        }

        static LandingStability LegsOff()
        {
            MaterialRow[] m = Materials(out ulong h);
            Loadout l = Spec.Nominal;
            l.Legs = false;
            return LandingStability.Coil(Spec, Spec.BuildContacts(l, m, h), Spec.BuildMass(l).Properties);
        }

        static LandingScenarios.Outcome Run(LandingScenarios.Touch t)
        {
            MaterialRow[] m = Materials(out ulong h);
            return LandingScenarios.Run(Spec, m, h, t);
        }

        static double Critical(LandingScenarios.Touch t)
        {
            MaterialRow[] m = Materials(out ulong h);
            return LandingScenarios.CriticalSpeed(Spec, m, h, t);
        }

        static double RollTime(bool legs, double from, double to, Func<ContactSpec, ContactSpec> edit = null)
        {
            MaterialRow[] m = Materials(out ulong h);
            return LandingScenarios.RollTime(Spec, m, h, legs, from, to, edit);
        }

        [Test]
        public void TheDroneTipsWhereItsCentreOfMassPassesOverTheSkid()
        {
            LandingStability a = LegsOn();
            Assert.That(a.StaticRollTipDeg, Is.EqualTo(23.39).Within(0.01), "the roll tip, D-050's nominal build");
            Assert.That(a.StaticPitchTipDeg, Is.EqualTo(42.80).Within(0.01), "the pitch tip over the front ends");
            // Set down at rest just inside and just past each tip.
            Assert.That(RollTime(true, a.StaticRollTipDeg - 0.05, 60), Is.EqualTo(double.PositiveInfinity), "inside the roll tip it rights itself");
            Assert.That(RollTime(true, a.StaticRollTipDeg + 0.05, 60), Is.LessThan(10), "past it, it falls");
            var t = new LandingScenarios.Touch { Legs = true, Seconds = 4 };
            t.PitchDeg = a.StaticPitchTipDeg - 0.05;
            Assert.That(Run(t).Over, Is.False, "inside the pitch tip");
            t.PitchDeg = a.StaticPitchTipDeg + 0.05;
            Assert.That(Run(t).Over, Is.True, "past the pitch tip");
        }

        [Test]
        public void PastTheTipItFallsAsAPendulumAboutTheSkid()
        {
            LandingStability a = LegsOn();
            foreach (double past in new[] { 1.0, 5, 10 })
            {
                double sim = RollTime(true, a.StaticRollTipDeg + past, 74.9), mechanics = a.FallTime(past, 74.9);
                Assert.That(sim / mechanics, Is.EqualTo(1).Within(0.03), past + "° past the tip: " + sim + " s against " + mechanics);
            }
        }

        [Test]
        public void ACaughtSkidTipsItAtTheRigidBodySpeed()
        {
            // On dead legs (overdamped, so they return nothing of the touch) a caught skid is the mechanics' plastic catch, and
            // the sim gives its speed to 1 %, sideways and fore-aft; on the repository's legs (ζ 0.2) the legs' spring gives
            // back part of the touch as rotation, and a caught skid tips the drone up to 12 % sooner.
            LandingStability a = LegsOn();
            Func<ContactSpec, ContactSpec> dead = LandingScenarios.DeadLegsCaught(a.Mass), caught = LandingScenarios.GroundFriction(10);
            foreach (double tilt in new[] { 10.0, 20 })
            {
                var side = new LandingScenarios.Touch { Legs = true, RollDeg = tilt, Descent = 0.5, Edit = dead };
                var ahead = new LandingScenarios.Touch { Legs = true, PitchDeg = tilt, Descent = 0.5, Forward = true, Edit = dead };
                double roll = a.CaughtSkidSpeed(tilt, 0.5), pitch = a.CaughtEndSpeed(tilt, 0.5);
                Assert.That(Critical(side) / roll, Is.EqualTo(1).Within(0.01), $"{tilt}° of roll on dead legs, against {roll:F3}");
                Assert.That(Critical(ahead) / pitch, Is.EqualTo(1).Within(0.01), $"{tilt}° nose-down on dead legs, against {pitch:F3}");
                side.Edit = ahead.Edit = caught;
                Assert.That(Critical(side) / roll, Is.EqualTo(1).Within(0.12), $"{tilt}° of roll on the legs");
                Assert.That(Critical(ahead) / pitch, Is.EqualTo(1).Within(0.12), $"{tilt}° nose-down on the legs");
            }
        }

        [Test]
        public void ASmoothSkidSlipsBeforeItCatches()
        {
            // The ground's μ 0.5/0.4: where the catch needs more friction than that (no descent) the skid slips and the drone
            // tips only faster; where it needs less (a descent) it catches as a caught one does.
            LandingStability a = LegsOn();
            double mu = Spec.Contacts.GroundStaticFriction;
            Assert.That(a.CatchFriction(10, 0, a.CaughtSkidSpeed(10, 0)), Is.GreaterThan(mu));
            Assert.That(a.CatchFriction(10, 0.5, a.CaughtSkidSpeed(10, 0.5)), Is.LessThan(mu));
            double slips = Critical(new LandingScenarios.Touch { Legs = true, RollDeg = 10 });
            Assert.That(slips, Is.GreaterThan(a.CaughtSkidSpeed(10, 0)), "it slips first");
            double catches = Critical(new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5 });
            Assert.That(catches / a.CaughtSkidSpeed(10, 0.5), Is.EqualTo(1).Within(0.10), "it catches");
        }

        [Test]
        public void AFrictionlessSkidNeverCatches()
        {
            // The known-wrong: with no friction nothing stops the skid, so no lateral speed tips the drone below its static tip.
            LandingStability a = LegsOn();
            double sim = Critical(new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5, Edit = LandingScenarios.GroundFriction(0.001) });
            Assert.That(sim, Is.EqualTo(double.PositiveInfinity), "it skates at 4 m/s");
            Assert.That(Math.Abs(sim / a.CaughtSkidSpeed(10, 0.5) - 1), Is.Not.LessThanOrEqualTo(0.10), "it fails the caught skid's check");
        }

        [Test]
        public void SkidsSlidingAlongTheirLengthTripOnlyWhenTheirEndsCatch()
        {
            // Forward, the skids slide as a sled's runners: the friction at the ground pitches the nose down, but it trips the
            // drone over the front ends only past μ_k 0.91, so on the ground's 0.4 nothing trips it; caught at their ends (a
            // groove, a clod) the skids tip it at the mechanics' speed.
            LandingStability a = LegsOn();
            Assert.That(a.SlidingTripFriction, Is.EqualTo(0.907).Within(0.001));
            var t = new LandingScenarios.Touch { Legs = true, PitchDeg = 10, Descent = 0.5, Forward = true };
            Assert.That(Critical(t), Is.EqualTo(double.PositiveInfinity), "the runners slide");
            t.Edit = LandingScenarios.GroundFriction(10);
            double sim = Critical(t), mechanics = a.CaughtEndSpeed(10, 0.5);
            Assert.That(sim / mechanics, Is.EqualTo(1).Within(0.12), $"caught ends: {sim:F3} m/s against {mechanics:F3}");
        }

        [Test]
        public void WithThrustHeldOnlyAPinnedSkidRollsItOver()
        {
            // Dynamic rollover: with the hover's thrust held a pinned skid tips the drone from a degree or two; the ground's
            // Coulomb μ cannot pin a skid the thrust has nearly unloaded, so on the stub's rigid floor it skates instead. Set
            // down tilted at the hover's thrust with no descent of its own: the tilted thrust drifts it onto its low skid.
            LandingStability a = LegsOn();
            Assert.That(a.RolloverDeg(0.95), Is.LessThan(2.0));
            Assert.That(LandingStability.PinFriction(10, 0.95), Is.GreaterThan(2.0));
            var t = new LandingScenarios.Touch { Legs = true, RollDeg = 10, Power = LandingScenarios.Power.Held };
            Assert.That(Run(t).Over, Is.False, "the smooth skid skates");
            t.Edit = LandingScenarios.GroundFriction(10);
            Assert.That(Run(t).Over, Is.True, "the pinned skid rolls over");
        }

        [Test]
        public void TheCoilRocksAboutItsContact()
        {
            LandingStability a = LegsOff();
            Assert.That(a.PackRestDeg, Is.EqualTo(22.59).Within(0.01), "the pack meets the ground");
            Assert.That(a.CoilRollingRate, Is.EqualTo(5.364).Within(0.001));
            foreach (double from in new[] { 0.5, 1, 2 })
            {
                double sim = RollTime(false, from, 20), rolling = a.CoilRockTime(from, 20), axle = a.CoilRockTime(from, 20, false);
                Assert.That(sim / rolling, Is.EqualTo(1).Within(0.03), $"from {from}°: {sim} s against {rolling} rolling");
                Assert.That(Math.Abs(sim / axle - 1), Is.GreaterThan(0.10), $"an axle's {axle} s is a known-wrong");
            }
            // The known-wrong that cannot roll: frictionless, the coil slides from under the centre of mass, as fast as an axle.
            double slides = RollTime(false, 1, 20, LandingScenarios.GroundFriction(0.001));
            Assert.That(Math.Abs(slides / a.CoilRockTime(1, 20) - 1), Is.GreaterThan(0.10), "a frictionless coil fails the rock");
        }

        [Test]
        public void TheRockIsNotSlowedByTheContactsDamping()
        {
            // Rolling compresses nothing and slips nothing, so an overdamped contact (ζ 2 on every body shape) rocks as the
            // repository's ζ 0.2: damping is not what holds the coil up.
            double sim = RollTime(false, 1, 20), overdamped = RollTime(false, 1, 20, LandingScenarios.BodyDamping(2));
            Assert.That(overdamped / sim, Is.EqualTo(1).Within(0.02));
        }

        [Test]
        public void ArmedTheFlightControllerHoldsTheCoilUp()
        {
            // Betaflight's airmode at zero throttle keeps the PID's authority, and the rate loop fights the rock: armed with the
            // throttle cut the coil takes longer to tilt than disarmed (the pilot's "tilts super slowly", D-067).
            var t = new LandingScenarios.Touch { Legs = false, RollDeg = 1, Descent = 0.1, Power = LandingScenarios.Power.Disarmed, ReachDeg = 20, Seconds = 6 };
            double free = Run(t).Reached;
            t.Power = LandingScenarios.Power.Cut;
            double held = Run(t).Reached;
            Assert.That(free, Is.LessThan(1.0));
            Assert.That(held, Is.GreaterThan(1.3 * free), $"armed {held} s against disarmed {free} s");
        }
    }
}
