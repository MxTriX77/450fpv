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
    /// Since p7g-legs 1.2 the legs give across their tubes (262 N/m at the foot), which the rigid-body mechanics does not
    /// have: its checks fly the legs rigid across (Rigid, model 37's leg law, the mechanics' own assumption), and the legs'
    /// own tip is held against its closed form (OnTheirSideGiveTheLegsTipTheDroneSooner).
    /// </summary>
    public class LandingTests
    {
        static readonly Func<ContactSpec, ContactSpec> Rigid = LandingScenarios.RigidAcross();

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
            Assert.That(a.StaticRollTipDeg, Is.EqualTo(23.33).Within(0.01), "the roll tip, p7g-legs 1.1's plastic legs (23.35° on model 26's, 23.39° on model 22's)");
            Assert.That(a.StaticPitchTipDeg, Is.EqualTo(42.72).Within(0.01), "the pitch tip over the front ends (42.75° on model 26's, 42.80° on model 22's)");
            // Set down at rest just inside and just past each tip (rigid across: the soft legs' own tip is below).
            Assert.That(RollTime(true, a.StaticRollTipDeg - 0.05, 60, Rigid), Is.EqualTo(double.PositiveInfinity), "inside the roll tip it rights itself");
            Assert.That(RollTime(true, a.StaticRollTipDeg + 0.05, 60, Rigid), Is.LessThan(10), "past it, it falls");
            var t = new LandingScenarios.Touch { Legs = true, Seconds = 4, Edit = Rigid };
            t.PitchDeg = a.StaticPitchTipDeg - 0.05;
            Assert.That(Run(t).Over, Is.False, "inside the pitch tip");
            t.PitchDeg = a.StaticPitchTipDeg + 0.05;
            Assert.That(Run(t).Over, Is.True, "past the pitch tip");
        }

        [Test]
        public void OnTheirSideGiveTheLegsTipTheDroneSooner()
        {
            // Quasi-static: on its low skid each leg carries half the weight up its foot; the share across the tilted leg bends the
            // foot in under the body (the leg's 262 N/m in series with the stick anchor), so the centre of mass stands over the
            // feet at a smaller roll than over the skid (LandingStability.SoftRollTipDeg; rigid across, the known-wrong, 23.3°).
            // Set down at rest with the legs unbent, the side mode races the fall: the struts push along the legs through the
            // feet, under the centre of mass, before the bend that would make their force vertical builds, and rock the body back
            // onto both skids from past the quasi-static tip, so the set-down's tip lies between the two (D-067's set-down at
            // rest is the rigid legs' protocol; the landings' tips are 1.4's grid, printed here beside the rigid ones).
            // p7g-legs 1.3: the side give's parts, the tube's 268 N/m less the axial load's softening (each low leg's (W/2)·cos φ
            // against its 47 N sway load) in series with the anchor, the jaws held. The set-downs fly the elastic legs (the law
            // off): past about 30° the low skid's legs part at once on the weight's share across their tilted struts, which 1.3
            // takes off the drone, and it settles on its frame instead of tipping; the side give's own tip is this test's.
            LandingStability a = LegsOn();
            MaterialRow[] m = Materials(out ulong h);
            ContactSpec c = Spec.BuildContacts(Spec.Nominal, m, h);
            double anchor = c.FrictionStiffnessRatio * c.LegStiffness;
            double soft = a.SoftRollTipDeg(c.LegTubeSideStiffness, anchor, c.LegSwayLoad, out double feet);
            double side = c.LegSideStiffness * anchor / (c.LegSideStiffness + anchor);
            double unsoftened = a.SoftRollTipDeg(side);
            Assert.That(Math.Tan(soft * Math.PI / 180), Is.EqualTo((a.Track - feet) / a.SkidHeight).Within(1e-9), "the centre of mass over the bent feet");
            Assert.That(soft, Is.LessThan(a.StaticRollTipDeg - 3), "statically the soft legs tip sooner than the rigid ones");
            Func<ContactSpec, ContactSpec> elastic = e => { e.Legs = null; return e; };
            double lo = 10, hi = 40;
            Assert.That(RollTime(true, lo, 60, elastic), Is.EqualTo(double.PositiveInfinity), $"set down at {lo}° it rights itself");
            Assert.That(RollTime(true, hi, 60, elastic), Is.LessThan(10), $"set down at {hi}° it falls");
            // The lowest set-down that falls, scanned by 0.1° (model 54): the falls are not monotone in the angle (the rebound
            // rights it again in windows past the first fall, about 18.4–19.9° and 21.3–24.9° on both the frame and model 51's legs;
            // from 25.0° every set-down falls), so a bisection lands on whichever window edge it meets (model 51's 22.66°; the
            // frame's 24.94°, past the rigid tip, when 23.125° rights itself on the frame).
            for (hi = lo; hi < 40 && !(RollTime(true, hi, 60, elastic) < 10); hi += 0.1) { }
            TestContext.Out.WriteLine($"the roll tip: {a.StaticRollTipDeg:F2}° rigid across; {soft:F2}° quasi-static on the tube's {c.LegTubeSideStiffness:F0} N/m less P·Δ "
                + $"(the feet {feet * 1e3:F1} mm in; 1.2's {side:F0} N/m chain without it, {unsoftened:F2}°); set down at rest with unbent legs, the sim falls from {hi:F2}°");
            Assert.That(hi, Is.GreaterThan(soft).And.LessThan(a.StaticRollTipDeg), "between the quasi-static tip and the rigid legs'");
            // D-067's grid on the soft legs, beside the rigid one (for the as-built note).
            foreach (double tilt in new[] { 10.0, 20 })
            {
                var t = new LandingScenarios.Touch { Legs = true, RollDeg = tilt, Descent = 0.5, Edit = LandingScenarios.GroundFriction(10) };
                double softCaught = Critical(t);
                t.Edit = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(10));
                TestContext.Out.WriteLine($"a caught skid at {tilt}° of roll, 0.5 m/s down: tips at {softCaught:F3} m/s on the soft legs, {Critical(t):F3} rigid across, the mechanics {a.CaughtSkidSpeed(tilt, 0.5):F3}");
            }
            var smooth = new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5 };
            double softSmooth = Critical(smooth);
            smooth.Edit = Rigid;
            TestContext.Out.WriteLine($"the floor's μ at 10°, 0.5 m/s down: tips at {softSmooth:F3} m/s on the soft legs, {Critical(smooth):F3} rigid across");
        }

        [Test]
        public void PastTheTipItFallsAsAPendulumAboutTheSkid()
        {
            LandingStability a = LegsOn();
            foreach (double past in new[] { 1.0, 5, 10 })
            {
                double sim = RollTime(true, a.StaticRollTipDeg + past, 74.9, Rigid), mechanics = a.FallTime(past, 74.9);
                Assert.That(sim / mechanics, Is.EqualTo(1).Within(0.03), past + "° past the tip: " + sim + " s against " + mechanics);
            }
        }

        [Test]
        public void ACaughtSkidTipsItAtTheRigidBodySpeed()
        {
            // On dead legs (overdamped, so they return nothing of the touch) a caught skid is the mechanics' plastic catch, and
            // the sim gives its speed to 1 %, sideways and fore-aft; on the repository's legs (ζ 0.2) the legs' spring gives
            // back part of the touch as rotation, and a caught skid tips the drone up to 14 % sooner on p7g-legs 1.1's derived
            // 52.3 kN/m (10° and 20°: 0.918 and 0.936 of the mechanics in roll, 0.863 and 0.901 nose-down), up to 10 % on model
            // 26's assumed 10 kN/m (0.960, 0.975, 0.896, 0.934).
            LandingStability a = LegsOn();
            Func<ContactSpec, ContactSpec> dead = LandingScenarios.RigidAcross(LandingScenarios.DeadLegsCaught(a.Mass)), caught = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(10));
            foreach (double tilt in new[] { 10.0, 20 })
            {
                var side = new LandingScenarios.Touch { Legs = true, RollDeg = tilt, Descent = 0.5, Edit = dead };
                var ahead = new LandingScenarios.Touch { Legs = true, PitchDeg = tilt, Descent = 0.5, Forward = true, Edit = dead };
                double roll = a.CaughtSkidSpeed(tilt, 0.5), pitch = a.CaughtEndSpeed(tilt, 0.5);
                Assert.That(Critical(side) / roll, Is.EqualTo(1).Within(0.01), $"{tilt}° of roll on dead legs, against {roll:F3}");
                Assert.That(Critical(ahead) / pitch, Is.EqualTo(1).Within(0.01), $"{tilt}° nose-down on dead legs, against {pitch:F3}");
                side.Edit = ahead.Edit = caught;
                Assert.That(Critical(side) / roll, Is.EqualTo(1).Within(0.15), $"{tilt}° of roll on the legs");
                Assert.That(Critical(ahead) / pitch, Is.EqualTo(1).Within(0.15), $"{tilt}° nose-down on the legs");
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
            double slips = Critical(new LandingScenarios.Touch { Legs = true, RollDeg = 10, Edit = Rigid });
            Assert.That(slips, Is.GreaterThan(a.CaughtSkidSpeed(10, 0)), "it slips first");
            double catches = Critical(new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5, Edit = Rigid });
            Assert.That(catches / a.CaughtSkidSpeed(10, 0.5), Is.EqualTo(1).Within(0.10), "it catches");
        }

        [Test]
        public void AFrictionlessSkidNeverCatches()
        {
            // The known-wrong: with no friction nothing stops the skid, so no lateral speed tips the drone below its static tip.
            LandingStability a = LegsOn();
            double sim = Critical(new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5, Edit = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(0.001)) });
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
            Assert.That(a.SlidingTripFriction, Is.EqualTo(0.905).Within(0.001), "D-066's build (0.907 on model 22's)");
            var t = new LandingScenarios.Touch { Legs = true, PitchDeg = 10, Descent = 0.5, Forward = true, Edit = Rigid };
            Assert.That(Critical(t), Is.EqualTo(double.PositiveInfinity), "the runners slide");
            t.Edit = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(10));
            double sim = Critical(t), mechanics = a.CaughtEndSpeed(10, 0.5);
            Assert.That(sim / mechanics, Is.EqualTo(1).Within(0.15), $"caught ends: {sim:F3} m/s against {mechanics:F3} (the legs' give-back, ACaughtSkidTipsItAtTheRigidBodySpeed)");
        }

        [Test]
        public void WithThrustHeldOnlyAPinnedSkidRollsItOver()
        {
            // Dynamic rollover: with the hover's thrust held a pinned skid tips the drone from a degree or two; the ground's
            // Coulomb μ cannot pin a skid the thrust has nearly unloaded, so on the stub's rigid floor it skates instead. Set
            // down tilted from a steady approach with the thrust held: at the hover's thrust it hops off its first touch, and
            // the tilted thrust drifts it onto its low skid at its next landing (N4), which the pinned skid rolls over.
            LandingStability a = LegsOn();
            Assert.That(a.RolloverDeg(0.95), Is.LessThan(2.0));
            Assert.That(LandingStability.PinFriction(10, 0.95), Is.GreaterThan(2.0));
            var t = new LandingScenarios.Touch { Legs = true, RollDeg = 10, Power = LandingScenarios.Power.Held, Edit = Rigid };
            LandingScenarios.Outcome smooth = Run(t);
            Assert.That(smooth.Over || smooth.LaterOver, Is.False, $"the smooth skid skates (its next landing at {smooth.LaterSpeed:F2} m/s)");
            t.Edit = LandingScenarios.RigidAcross(LandingScenarios.GroundFriction(10));
            LandingScenarios.Outcome pinned = Run(t);
            Assert.That(pinned.Over || pinned.LaterOver, Is.True, $"the pinned skid rolls over (its next landing at {pinned.LaterSpeed:F2} m/s)");
        }

        [Test]
        public void TheCoilRocksAboutItsContact()
        {
            LandingStability a = LegsOff();
            Assert.That(a.PackRestDeg, Is.EqualTo(22.59).Within(0.01), "the pack meets the ground");
            Assert.That(a.CoilRollingRate, Is.EqualTo(5.374).Within(0.001), "D-066's build (5.364 /s on model 22's)");
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

        [Test]
        public void ArmedAndCutFromAHoverItDoesNotTipWithoutDrift()
        {
            // N4 (p6-flight-core 1.6c): on its low skid the drone rights itself under gravity's moment about the skid. The cut
            // excites Betaflight 4.5's anti-gravity, whose I-term accelerator integrates that righting roll at up to 36 times
            // the builder's Ki, and airmode spends the wound I term lifting the high side. From the steady approach the rotors
            // still carry the weight while anti-gravity peaks, so the I term stays short of what holds the high side up, and
            // the drone stays up. The known-wrong was model 38's start: the touch 4 ms after the throttle's step to the hover,
            // the rotors near idle, so the whole weight landed on the skid inside the window and it tipped at all three.
            foreach (double roll in new[] { 5.0, 10, 15 })
            {
                var t = new LandingScenarios.Touch { Legs = true, RollDeg = roll, Power = LandingScenarios.Power.Cut };
                LandingScenarios.Outcome o = Run(t);
                Assert.That(o.TouchAt, Is.GreaterThanOrEqualTo(0.4), "a steady approach: the touch 0.4 s or more after the throttle's step");
                Assert.That(o.TouchAntiGravity, Is.LessThan(0.01), "anti-gravity settled at the touch");
                Assert.That(o.Over, Is.False, $"armed and cut at {roll}° with no drift (it reached {o.MaxTilt:F1}°)");
            }
        }

        [Test]
        public void HeldAtTheHoverATouchIsJudgedOnItsOwn()
        {
            // N4: at the hover's thrust the smooth floor (μ 0.5) cannot pin the low skid (it takes μ 2.56 at 10°), so the touch
            // skates and the drone, its roll held by the rate loop, is flying again some 50 ms after it. Acro does not level it:
            // it drifts at g·tan φ and lands again later at the drift's speed. The known-wrong is that later landing, which
            // model 38's run counted as this touch's.
            Assert.That(LandingStability.PinFriction(10, 0.95), Is.GreaterThan(0.5), "a smooth skid cannot be pinned");
            var t = new LandingScenarios.Touch { Legs = true, RollDeg = 10, Descent = 0.5, Power = LandingScenarios.Power.Held };
            LandingScenarios.Outcome o = Run(t);
            Assert.That(o.TouchEnded, Is.LessThan(0.2), "it flies off its touch");
            Assert.That(o.Over, Is.False, $"the touch at 10° and 0.5 m/s down skates (it reached {o.MaxTilt:F1}°); the drift's next landing came at {o.LaterSpeed:F2} m/s");
        }
    }
}
