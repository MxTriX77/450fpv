using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // airframe-effects' "Motor asymmetry within component tolerances" (p7b-airframe 1.1, PHY-07).
    public sealed class AsymmetryTests
    {
        [Test]
        public void AsymmetryOnAndOff()
        {
            AsymmetryScenario.Run run = AsymmetryScenario.Flown(RepositoryDrone.Spec());
            AsymmetryScenario.Band b = run.Band;
            TestContext.Out.WriteLine($"band: roll {b.RollRateLo:E2}–{b.RollRateHi:E2} °/s, yaw {b.YawRateLo:E2}–{b.YawRateHi:E2} °/s, "
                + $"vibration {b.VibrationLo:E2}–{b.VibrationHi:E2} rad/s");
            for (int i = 0; i < AsymmetryScenario.Seeds.Length; i++)
            {
                AsymmetryScenario.Result on = run.On[i], off = run.Off[i];
                AsymmetryScenario.Prediction p = run.Predicted[i];
                string seed = "seed " + AsymmetryScenario.Seeds[i];
                TestContext.Out.WriteLine(seed + ": " + AsymmetryScenario.Describe(on, off, p));
                Assert.That(AsymmetryScenario.InBand(on, b), Is.True, seed + ": the correction rates in the band");
                Assert.That(AsymmetryScenario.AsPredicted(on, p), Is.True, seed + ": the yank and the vibration as its draws predict");
                Assert.That(AsymmetryScenario.Steady(on, p), Is.True, seed + ": never buzzes or diverges");
                Assert.That(off.RollRate, Is.LessThanOrEqualTo(0.5 * on.RollRate), seed + ": off, the roll drift at least halved");
                Assert.That(off.YawRate, Is.LessThanOrEqualTo(0.5 * on.YawRate), seed + ": off, the yaw drift at least halved");
                Assert.That(off.SideDrift, Is.LessThanOrEqualTo(0.5 * on.SideDrift), seed + ": off, the sideways drift at least halved");
            }
        }

        // QA's finding 6: the yank as its draws predict, within 30 % and YankFloorDeg (derived, 0.04°), and never buzzing, on
        // seeds 1–30 (the suite's 1–5 and QA's 11–15 among them). On fresh seeds it holds on 215 of 220 (YankFloorDeg).
        [Test]
        public void TheYankIsAsPredictedOnThirtySeeds()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            AsymmetryScenario.Run run = AsymmetryScenario.Flown(spec);
            double beyond = 0, busiest = 0;
            for (ulong seed = 1; seed <= 30; seed++)
            {
                AsymmetryScenario.Result on = AsymmetryScenario.Hover(AsymmetryScenario.Setup(spec, seed, true), out FlightCore core);
                AsymmetryScenario.Prediction p = AsymmetryScenario.Predict(run.Model, core.Draws);
                double roll = System.Math.Abs(System.Math.Abs(on.YankRollDeg) - p.RollDeg) - 0.3 * p.RollDeg;
                double heading = System.Math.Abs(System.Math.Abs(on.YankHeadingDeg) - p.HeadingDeg) - 0.3 * p.HeadingDeg;
                beyond = System.Math.Max(beyond, System.Math.Max(roll, heading));
                busiest = System.Math.Max(busiest, on.VibrationMax / p.VibrationInPhase);
                string at = $"seed {seed}: the yank {on.YankRollDeg:F4}° and {on.YankHeadingDeg:F4}° against {p.RollDeg:F4}° and {p.HeadingDeg:F4}°, "
                    + $"the busiest block {on.VibrationMax / p.VibrationInPhase:F2} of the in-phase bound";
                TestContext.Out.WriteLine(at);
                Assert.That(AsymmetryScenario.AsPredicted(on, p), Is.True, at);
                Assert.That(AsymmetryScenario.Steady(on, p), Is.True, at);
            }
            TestContext.Out.WriteLine($"seeds 1-30: the largest miss beyond 30 % {beyond:F4}° (the floor {AsymmetryScenario.YankFloorDeg}°), the busiest block {busiest:F2} of the in-phase bound");
        }

        // The known-wrong loop for "never buzzes": the builder's D gains (and D min) × 4 put the loop on the edge of a buzz,
        // and it chases the props' vibration: the ESCs' commands jump and the body vibrates far past the props' in-phase pull.
        // × 3 still holds (the D-term's filters), so the bound sits where the loop's own margin ends.
        [Test]
        public void ALoopOnTheEdgeOfABuzzFailsSteady()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            AsymmetryScenario.Run run = AsymmetryScenario.Flown(spec);
            FlightSetup setup = AsymmetryScenario.Setup(spec, 1, true);
            Control.FcSettings fc = setup.Settings;
            fc.d_roll *= 4;
            fc.d_min_roll *= 4;
            fc.d_pitch *= 4;
            fc.d_min_pitch *= 4;
            setup.Settings = fc;
            AsymmetryScenario.Result r = AsymmetryScenario.Hover(setup, out FlightCore core);
            AsymmetryScenario.Prediction p = AsymmetryScenario.Predict(run.Model, core.Draws);
            TestContext.Out.WriteLine($"D x 4: the ESCs' largest step {r.DutyJump:E2}, the busiest block {r.VibrationMax / p.VibrationInPhase:F1} of the in-phase bound");
            Assert.That(AsymmetryScenario.Steady(r, p), Is.False, "it buzzes");
            Assert.That(r.VibrationMax, Is.GreaterThan(p.VibrationInPhase), "past the props' own pull");
        }

        // Calm's floor (PHY-14), "never perfectly still", from the airframe: with the asymmetry on, every 1 s block of the
        // hover's last 20 s vibrates above the band's floor, and with it off every block is far below it.
        [Test]
        public void NeverPerfectlyStill()
        {
            AsymmetryScenario.Run run = AsymmetryScenario.Flown(RepositoryDrone.Spec());
            for (int i = 0; i < AsymmetryScenario.Seeds.Length; i++)
            {
                Assert.That(AsymmetryScenario.NeverStill(run.On[i], run.Band), Is.True, "seed " + AsymmetryScenario.Seeds[i] + ": on, above the floor");
                Assert.That(AsymmetryScenario.NeverStill(run.Off[i], run.Band), Is.False, "seed " + AsymmetryScenario.Seeds[i] + ": off, still");
                Assert.That(run.Off[i].VibrationMax, Is.LessThan(0.01 * run.Band.VibrationLo), "seed " + AsymmetryScenario.Seeds[i] + ": off, far below");
            }
        }

        // The known-wrong model: identical rotors need no roll or yaw correction at all, below the band.
        [Test]
        public void IdenticalRotorsFailTheBand()
        {
            AsymmetryScenario.Run run = AsymmetryScenario.Flown(RepositoryDrone.Spec());
            foreach (AsymmetryScenario.Result off in run.Off) Assert.That(AsymmetryScenario.InBand(off, run.Band), Is.False);
        }

        [Test]
        public void TheDrawsAreNamedBoundedLoggedAndReplayed()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = AsymmetryScenario.Setup(spec, 7, true);
            var core = new FlightCore(setup);
            MotorAsymmetry a = setup.Asymmetry;
            Assert.That(core.Random.Opened, Is.EqualTo(new[] { "motor.kv", "esc.lag", "prop.unbalance" }), "still air draws nothing first");
            Assert.That(core.Random.Draws.Count, Is.EqualTo(16));
            foreach (ParameterDraw d in core.Random.Draws)
            {
                Assert.That(d.Value, Is.InRange(d.Lo, d.Hi), d.Parameter);
                if (d.Stream == "motor.kv") Assert.That((d.Lo, d.Hi), Is.EqualTo((-a.KvBound, a.KvBound)));
                if (d.Stream == "esc.lag") Assert.That((d.Lo, d.Hi), Is.EqualTo((-a.EscLagBound, a.EscLagBound)));
            }
            Assert.That(a.PermissibleUnbalance, Is.EqualTo(1.0695e-7).Within(1e-10), "16 g at G 6.3 and 9000 rpm: 0.107 g·mm");
            for (int i = 0; i < 4; i++)
            {
                Assert.That(core.Draws.KvScale[i], Is.Not.EqualTo(1));
                Assert.That(core.Draws.Unbalance[i], Is.InRange(0, a.PermissibleUnbalance));
            }

            LogHeader h = LogHeader.For(setup, core);
            LogHeader back = LogHeader.Read(new MemoryStream(h.Encode()));
            foreach (string f in LogHeader.AsymmetryFieldsLogged)
            {
                FieldInfo info = typeof(MotorAsymmetry).GetField(f);
                Assert.That(info.GetValue(back.Asymmetry), Is.EqualTo(info.GetValue(a)), "asymmetry." + f);
            }
            Assert.That(back.Draws, Is.EqualTo(h.Draws));
            var problems = new List<string>();
            var replayed = new FlightCore(back.ToSetup(setup.World, problems));
            Assert.That(problems, Is.Empty);
            Assert.That(replayed.Draws.KvScale, Is.EqualTo(core.Draws.KvScale), "the replay draws the same motors");

            FlightSetup offSetup = AsymmetryScenario.Setup(spec, 7, false);
            var off = new FlightCore(offSetup);
            Assert.That(off.Random.Draws.Count, Is.EqualTo(0));
            Assert.That(off.Draws, Is.Null);
            Assert.That(LogHeader.Read(new MemoryStream(LogHeader.For(offSetup, off).Encode())).Asymmetry, Is.Null);
        }
    }
}
