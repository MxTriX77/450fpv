using System;
using System.Globalization;
using System.IO;
using Fpv450.Feed;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using FeedLinkState = Fpv450.Feed.LinkState;
using SimLinkState = Fpv450.Sim.Flight.LinkState;

namespace Fpv450.FeedBench
{
    // `feedbench link --break|--dip [--seed n] [--flight n] [--timer on|off]` (p7d-tether 1.16): the physics' link through the feed's
    // receiver, headless, one row per field: the field's signals as FlightCore publishes them (the state, the margin's
    // least, the dips), the receiver's line, the feed's loss state, and the failsafe's stage.
    // --break: CutByADisc's flight (fiber-tether's "From the break to the disarm"): 20 m up, +z to 15 m/s from 10 s, the
    // full reverse at 16 s, until a disc cuts the line; then 2 s on, past Betaflight's DROP.
    // --dip: no flight dips today (1.8's twig slid round rather than wrapping), so the margin is scripted through the
    // physics' own FiberLink (tether.json's budget, threshold and hysteresis; FlightCore's 20-step field window): steady,
    // then below 0 dB for about 0.5 s, then back, as the link's length would make it.
    internal static class LinkCase
    {
        const int FieldSteps = FlightCore.FieldSteps;

        public static int Run(string[] args)
        {
            bool brk = Array.IndexOf(args, "--break") >= 0, dip = Array.IndexOf(args, "--dip") >= 0;
            int seed = 1, i = Array.IndexOf(args, "--seed");
            if (i >= 0 && (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seed)))
                brk = dip = false;
            int flight = 1; // the flight's seed: 1 is fiber-tether's disc cut (rotor 0, FiberCutTests)
            i = Array.IndexOf(args, "--flight");
            if (i >= 0 && (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out flight)))
                brk = dip = false;
            i = Array.IndexOf(args, "--timer");
            bool timer = !(i >= 0 && i + 1 < args.Length && args[i + 1] == "off");
            if (brk == dip)
            {
                Console.Error.WriteLine("usage: feedbench link --break|--dip [--seed n] [--flight n] [--timer on|off]");
                return 2;
            }
            var o = Console.Out;
            o.NewLine = "\n";
            var events = new FeedEvents(seed) { DropoutTimer = timer };
            var link = new FeedLink();
            var signals = new StubFeedSignals();
            o.WriteLine($"feed seed {seed}{(brk ? $", flight seed {flight}" : "")}, Godot's dropout timer {(timer ? "on (D-085)" : "off")}");
            o.WriteLine("field  step   state   margin_least_db  dips in_dip  feed_state   busy  failsafe");
            return brk ? Break(o, events, link, signals, (ulong)flight) : Dip(o, events, link, signals);
        }

        // One field: the physics' signals into the receiver, then the feed's field; a row when asked.
        static void Field(TextWriter o, in FeedSignals s, FeedEvents events, FeedLink link, StubFeedSignals signals,
            string failsafe, bool print)
        {
            signals.LinkState = (FeedLinkState)(byte)s.LinkState;
            signals.LinkMarginDb = (float)s.LinkMarginDb;
            signals.LinkDips = s.LinkDips;
            signals.LinkInDip = s.LinkInDip;
            signals.LinkLossCause = s.LinkLossCause == 0 ? "" : ((FiberMember)s.LinkLossCause).ToString();
            signals.TetherBreakMechanism = s.TetherBreakMechanism == 0 ? "" : ((TetherMechanism)s.TetherBreakMechanism).ToString();
            int field = events.Field + 1;
            string said = link.Apply(signals, events.Loss, field);
            events.AdvanceTo(field, 0f);
            FeedLoss l = events.Loss;
            string state = l.Current == FeedLoss.State.Picture && (l.Tell > 0f || l.TearAmp > 0f || l.GrainBoost > 0f) ? "Tell" : l.Current.ToString();
            if (print || said != null)
                o.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,5} {1,6}  {2,-7} {3,15}  {4,4} {5,6}  {6,-11} {7,4}  {8}{9}",
                    field, s.Step, s.LinkState, double.IsNegativeInfinity(s.LinkMarginLeastDb) ? "-inf" : s.LinkMarginLeastDb.ToString("0.000", CultureInfo.InvariantCulture),
                    s.LinkDips, s.LinkInDip ? "yes" : "no", state, l.Busy ? 1 : 0, failsafe, said != null ? "   <- " + said : ""));
        }

        static int Break(TextWriter o, FeedEvents events, FeedLink link, StubFeedSignals signals, ulong seed)
        {
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(ContactScenarios.RepositoryFile("assets-src/drone/drone.json")));
            FlightSetup setup = TetherScenarios.Setup(spec, WindLevel.Calm, seed: seed, cuts: true);
            double stand = TetherScenarios.Stand(setup);
            var core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            long breakStep = -1, lost = -1, stage1 = -1, stage2 = -1, disarm = -1, feedLoss = -1, blue, lastNotBlue = -1;
            string failsafe = "-";
            for (int k = 0; k < 26000 && (breakStep < 0 || core.State.Step < breakStep + 2000); k++)
            {
                StickSample stick;
                double t = k * FlightCore.Dt;
                if (k < 20) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 10 };
                else
                {
                    double y = stand + DetMath.Min(20, 2 * t);
                    pilot.HoldVelocity = t >= 10;
                    pilot.MaxTiltSin = t < 16 ? 0.5736 : 0.866;
                    pilot.TargetVelocity = new Double3(0, 0, t < 16 ? 15 : -8);
                    pilot.Target = new Double3(0, y, t < 10 ? 0 : core.State.Position.Z);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                }
                core.Advance(stick);
                long step = core.State.Step;
                for (int e = 0; e < core.Events.Count; e++)
                {
                    ref readonly FlightEvent ev = ref core.Events[e];
                    if (ev.Kind == EventKind.TetherBreak) breakStep = step;
                    if (ev.Kind != EventKind.Failsafe) continue;
                    failsafe = ((FailsafeStage)ev.Index).ToString();
                    if (ev.Index == 0) lost = step;
                    if (ev.Index == 1) stage1 = step;
                    if (ev.Index == 2) stage2 = step;
                    if (ev.Index == 3) disarm = step;
                }
                if (step < FieldSteps || step % FieldSteps != 0) continue;
                core.GetSignals(out FeedSignals s);
                bool near = breakStep >= 0 || s.LinkState == SimLinkState.Broken;
                Field(o, s, events, link, signals, failsafe, near);
                if (s.LinkState == SimLinkState.Broken && feedLoss < 0) feedLoss = step;
                if (feedLoss >= 0 && events.Loss.Current != FeedLoss.State.Blue) lastNotBlue = step;
            }
            blue = lastNotBlue >= 0 ? lastNotBlue + FieldSteps : feedLoss; // blue in every field after it
            if (breakStep < 0)
            {
                o.WriteLine("no break in 26 s");
                return 1;
            }
            o.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "break at step {0} ({1}); the feed's loss read at step {2} (+{3} steps, the end of the break's field); the terminal blue from step {4} (+{5} ms); "
                + "failsafe: signal lost +{6} ms, stage 1 +{7} ms, stage 2 +{8} ms, disarmed +{9} ms",
                breakStep, (TetherMechanism)core.Tether.LastBreak.Mechanism, feedLoss, feedLoss - breakStep, blue, blue - breakStep,
                lost - breakStep, stage1 - breakStep, stage2 - breakStep, disarm - breakStep));
            return 0;
        }

        static int Dip(TextWriter o, FeedEvents events, FeedLink link, StubFeedSignals signals)
        {
            TetherTable table = TetherScenarios.Table();
            var cuts = new FiberCuts(table, new FlightRandom(1)); // no bends: the margin is the budget less the length's
            var fiber = new FiberLink(table);
            double launch = table.Number("link.launch_margin_db"), alpha = table.Number("link.attenuation_db_per_m");
            double sum = 0, least = double.PositiveInfinity;
            long dipStart = -1, dipEnd = -1;
            for (long step = 1; step <= 3000; step++)
            {
                // The scripted margin, dB: 9 steady; from 1.000 s down to −2 over 60 ms; from 1.450 s back up to 9 over 100 ms.
                double t = step * FlightCore.Dt, m = 9;
                if (t >= 1.0 && t < 1.06) m = 9 - 11 * (t - 1.0) / 0.06;
                else if (t >= 1.06 && t < 1.45) m = -2;
                else if (t >= 1.45 && t < 1.55) m = -2 + 11 * (t - 1.45) / 0.10;
                fiber.Update((launch - m) / alpha, cuts, step);
                if (fiber.DipBegan) dipStart = step;
                if (fiber.DipEnded) dipEnd = step;
                sum += fiber.Margin;
                least = Math.Min(least, fiber.Margin);
                if (step % FieldSteps != 0) continue;
                var s = new FeedSignals
                {
                    Step = step, LinkState = fiber.State, LinkMarginDb = sum / FieldSteps, LinkMarginLeastDb = least,
                    LinkDips = fiber.Dips, LinkInDip = fiber.InDip, LinkLossCause = fiber.InDip ? fiber.DipKind : (byte)0,
                };
                Field(o, s, events, link, signals, "-", step >= 900 && step <= 1700);
                sum = 0;
                least = double.PositiveInfinity;
            }
            o.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "dip from step {0} (field {1}) to step {2} (field {3}): {4} ms below 0 dB and back above 1 dB", dipStart,
                (dipStart - 1) / FieldSteps + 1, dipEnd, (dipEnd - 1) / FieldSteps + 1, dipEnd - dipStart));
            return 0;
        }
    }
}
