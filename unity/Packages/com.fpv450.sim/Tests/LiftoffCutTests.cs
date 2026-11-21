using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7d-tether design 26 (1.11i): the line's cut at a liftoff, an empirical law of the pilot's account (LiftoffCuts). The law
    /// alone on a drone driven step by step, then the session's drone: the pilot's very sharp liftoffs cut with the law and
    /// never without it (the known-wrong, TetherLiftoffPhysical).
    /// </summary>
    public sealed class LiftoffCutTests
    {
        const double Dt = FlightCore.Dt;

        static void Print(string s) => TestContext.Progress.WriteLine(s);

        // A pitch of the nose by degrees (up +) about the body's x axis: the body's forward (0, 0, 1) rises by sin(degrees).
        static Quat Nose(double degrees)
        {
            double a = -degrees * Math.PI / 180 / 2;
            return new Quat(Math.Sin(a), 0, 0, Math.Cos(a));
        }

        sealed class Lift
        {
            public LiftoffCuts Law;
            public readonly StepEvents Events = new StepEvents();
            public FlightState S;
            public int Begun, Ended;
            public FlightEvent Begin, End;
            public bool Cut;
            public long CutStep = -1;

            public Lift(ulong seed)
            {
                Law = new LiftoffCuts(TetherScenarios.Table(), new FlightRandom(seed));
                S = new FlightState { Attitude = Quat.Identity, Armed = true };
            }

            // steps on the ground, then airborne at thrust over weight tw, the attitude q, velocity v; paid out grows at payout m/s.
            public void Fly(int ground, int air, double tw, Quat q, Double3 v, double payout)
            {
                double paid = 0;
                for (int k = 0; k < ground + air && !Cut; k++)
                {
                    bool touching = k < ground;
                    S.Step = k;
                    S.Attitude = touching ? Quat.Identity : q;
                    S.Velocity = touching ? default : v;
                    if (!touching) paid += payout * Dt;
                    Events.Clear();
                    Cut = Law.Step(S, touching, touching ? 1 : 0, touching ? 0.5 : tw, paid, Dt, Events, -1);
                    if (Cut) CutStep = k;
                    for (int i = 0; i < Events.Count; i++)
                    {
                        if (Events[i].Kind != EventKind.Liftoff) continue;
                        if ((Events[i].Index & LiftoffCuts.Began) != 0)
                        {
                            Begun++;
                            Begin = Events[i];
                        }
                        else
                        {
                            Ended++;
                            End = Events[i];
                        }
                    }
                }
            }
        }

        [Test]
        public void TheChanceIsThePilotsAccount()
        {
            TetherTable t = TetherScenarios.Table();
            double[] thrust = t.Row("launch.liftoff_cut_by_thrust").Values, back = t.Row("launch.liftoff_cut_by_back_speed").Values;
            Assert.That(t.Row("launch.liftoff_cut_by_thrust").Kind, Is.EqualTo(TetherValueKind.Chances));
            Assert.That(LiftoffCuts.Curve(thrust, 1.95), Is.EqualTo(0.87).Within(1e-12), "a very sharp liftoff, the pilot's 87 %");
            Assert.That(LiftoffCuts.Curve(thrust, 2.45), Is.EqualTo(0.95).Within(1e-12), "max throttle, the pilot's 95 %");
            Assert.That(LiftoffCuts.Curve(thrust, 3.5), Is.EqualTo(0.95).Within(1e-12), "held beyond its end");
            Assert.That(LiftoffCuts.Curve(thrust, 1.0), Is.EqualTo(0), "a thrust that only holds the weight never cuts");
            Assert.That(LiftoffCuts.Curve(thrust, 1.3), Is.LessThanOrEqualTo(0.02), "a careful liftoff is rare");
            Assert.That(LiftoffCuts.Curve(back, 3), Is.EqualTo(0.40).Within(1e-12), "backwards too much at the liftoff, the pilot's 40 %");
            Assert.That(LiftoffCuts.Curve(back, 0.2), Is.EqualTo(0), "a drift");
        }

        [Test]
        public void ALiftoffDrawsOnceAndLogsItsInputs()
        {
            var l = new Lift(7);
            l.Fly(200, 3000, 2.45, Quat.Identity, new Double3(0, 6, 0), 5);
            Assert.That(l.Begun, Is.EqualTo(1), "one liftoff, one begin");
            Assert.That(l.Ended, Is.EqualTo(1), "and one end");
            Assert.That(l.Law.Count, Is.EqualTo(1UL), "one draw from fiber.liftoff");
            Assert.That(l.Begin.A, Is.EqualTo(l.Law.Draw), "the begin logs the draw");
            Assert.That(l.Begin.Shape, Is.EqualTo(0), "the draw's index");
            Assert.That(l.End.A, Is.EqualTo(0.95).Within(1e-9), "the end logs the chance");
            Assert.That(l.End.B, Is.EqualTo(2.45).Within(1e-9), "and the pressure");
            Assert.That(l.Cut, Is.EqualTo(l.Law.Draw < 0.95), "the line parts when the chance passes the draw");
            Assert.That((l.End.Index & LiftoffCuts.CutHere) != 0, Is.EqualTo(l.Cut));
            if (l.Cut) Assert.That(l.CutStep, Is.EqualTo(200 + 500), "at the window's end, when the pressure is known");

            // The same seed draws the same: a replay's liftoff cuts the same.
            var again = new Lift(7);
            again.Fly(200, 3000, 2.45, Quat.Identity, new Double3(0, 6, 0), 5);
            Assert.That(again.Law.Draw, Is.EqualTo(l.Law.Draw));
            Assert.That(again.CutStep, Is.EqualTo(l.CutStep));
        }

        [Test]
        public void TheRateOverSeedsIsTheChance()
        {
            int cuts = 0;
            const int Seeds = 400;
            for (ulong seed = 1; seed <= Seeds; seed++)
            {
                var l = new Lift(seed);
                l.Fly(200, 1500, 1.95, Quat.Identity, new Double3(0, 4, 0), 4);
                if (l.Cut) cuts++;
            }
            double rate = (double)cuts / Seeds, spread = 3 * Math.Sqrt(0.87 * 0.13 / Seeds);
            Print($"a very sharp liftoff's pressure over {Seeds} seeds: {cuts} cut ({100 * rate:0.0} %)");
            Assert.That(rate, Is.EqualTo(0.87).Within(spread), "the draws are uniform: the rate is the chance");
        }

        [Test]
        public void TheLeanMovesTheChanceAsThePilotSays()
        {
            var back = new Lift(3);
            back.Fly(200, 1000, 1.95, Nose(20), default, 4);
            Assert.That(back.End.A, Is.EqualTo(0.97).Within(1e-9), "the nose pulled up: 10 points more");
            Assert.That(back.End.Index & LiftoffCuts.Leaned, Is.Not.Zero);

            var capped = new Lift(3);
            capped.Fly(200, 1000, 2.45, Nose(20), default, 4);
            Assert.That(capped.End.A, Is.EqualTo(0.99).Within(1e-9), "below 100 %");

            var safe = new Lift(3);
            safe.Fly(200, 1000, 2.45, Nose(-20), default, 4);
            Assert.That(safe.End.A, Is.EqualTo(0.05).Within(1e-9), "throttle with pitch forward, the safest launch: 5 %");
            Assert.That(safe.End.Index & LiftoffCuts.SafeLaunch, Is.Not.Zero);

            var upright = new Lift(3);
            upright.Fly(200, 1000, 1.95, Nose(4), default, 4);
            Assert.That(upright.End.A, Is.EqualTo(0.87).Within(1e-9), "a trim's few degrees are no stick input");
        }

        [Test]
        public void BackwardsOverItsOwnLineTheLiftoffLasts()
        {
            // Tail first at 3 m/s at a careful pressure, paying nothing out: the liftoff lasts, its chance the pilot's 40 %.
            var l = new Lift(11);
            l.Fly(200, 6000, 1.1, Quat.Identity, new Double3(0, 0, -3), 0);
            Assert.That(l.Law.Chance, Is.EqualTo(0.40).Within(1e-9));
            Assert.That(l.Law.Open || l.Cut, Is.True, "no payout, no end");
            Assert.That(l.Cut, Is.EqualTo(l.Law.Draw < 0.40));
        }

        [Test]
        public void ALandingEndsItAndTheNextLeaveDrawsAgain()
        {
            var l = new Lift(5);
            l.Fly(200, 300, 1.2, Quat.Identity, default, 0);
            Assert.That(l.Law.Open || l.Cut, Is.True);
            if (l.Cut) return; // the 1-in-50 seed: nothing more to see
            // Down again for a second: the liftoff ends at rest; up again: a second liftoff, a second draw.
            for (int k = 0; k < 1000; k++)
            {
                l.S.Step = 500 + k;
                l.Events.Clear();
                l.Law.Step(l.S, true, 1, 0.5, 0, Dt, l.Events, -1);
            }
            Assert.That(l.Law.Open, Is.False, "rested: the liftoff ended");
            l.S.Step = 1500;
            l.Events.Clear();
            l.Law.Step(l.S, false, 0, 1.2, 0, Dt, l.Events, -1);
            Assert.That(l.Law.Open, Is.True, "the next leave");
            Assert.That(l.Law.Count, Is.EqualTo(2UL), "its own draw");
        }

        [Test]
        public void ADroneThatNeverStoodNeverLiftsOff()
        {
            // A careful liftoff that climbs away (its 10 m of line paid out in half a second here), then a second in a crown,
            // touching with its legs carrying nothing, then free: no liftoff; then a second standing on its legs, then free: one.
            var l = new Lift(11);
            l.Fly(200, 1000, 1.2, Quat.Identity, new Double3(0, 4, 0), 20);
            if (l.Cut) return; // its 1.3 %
            Assert.That(l.Law.Open, Is.False, "the climb's liftoff ended at its line");
            Brush(l, 1200, 0);
            Assert.That(l.Law.Count, Is.EqualTo(1UL), "a second of brushing, never standing: no second liftoff");
            Brush(l, 2400, 1);
            Assert.That(l.Law.Count, Is.EqualTo(2UL), "a second standing on its legs: the next leave is a liftoff");
        }

        static void Brush(Lift l, long from, double legs)
        {
            for (int k = 0; k < 1000; k++)
            {
                l.S.Step = from + k;
                l.Events.Clear();
                l.Law.Step(l.S, true, legs, 1.2, 0, Dt, l.Events, -1);
            }
            l.S.Step = from + 1000;
            l.Events.Clear();
            l.Law.Step(l.S, false, 0, 1.2, 0, Dt, l.Events, -1);
        }

        [Test]
        public void TheSessionDronesVerySharpLiftoffsCutAndTheKnownWrongNever()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            const int Seeds = 8;
            int with = 0, without = 0, liftoffCuts = 0;
            FiberCutScenarios.StopAtBreak = true;
            try
            {
                for (ulong seed = 1; seed <= Seeds; seed++)
                {
                    FiberCutScenarios.Outcome o = FiberCutScenarios.Liftoff(spec, true, seed);
                    if (o.Broken) with++;
                    if (o.Broken && o.Mechanism == TetherMechanism.Liftoff) liftoffCuts++;
                    if (FiberCutScenarios.Liftoff(spec, true, seed, s => s.TetherLiftoffPhysical = true).Broken) without++;
                }
            }
            finally
            {
                FiberCutScenarios.StopAtBreak = false;
            }
            Print($"the very sharp liftoff at max throttle, {Seeds} seeds: {with} cut with the law ({liftoffCuts} by it), {without} without");
            Assert.That(with, Is.GreaterThanOrEqualTo(Seeds - 2), "the pilot's 95 %");
            Assert.That(liftoffCuts, Is.EqualTo(with), "each by the liftoff law, named");
            Assert.That(without, Is.EqualTo(0), "the known-wrong: the line's own mechanics never reach a liftoff's cut");
        }
    }
}
