using System;
using System.Globalization;
using System.Text;
using Fpv450.App.Session;
using Fpv450.Input.Radio;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tests;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Tests.Session
{
    // tx-input's throttle default (p6-input-app 1.3, D-076; fc-profiles §2c): where the player's drone lifts off its legs,
    // where it hovers, and how fast it climbs for 1 % of stick there, with no bottle, a 1 kg and a 2 kg one, as a stick
    // through the throttle curves. The physics decides all three, headless on the sim core through the session's own setup
    // (SessionSetup.Build: drone.json at the loadout, the airframe's effects, Calm, a full pack, the coil full): the hover is
    // the mixer throttle the closed-loop pilot (SimPilot) holds 100 m up; the liftoff is the mixer throttle at which the
    // drone standing on its legs on the floor leaves it, the throttle raised slowly from below the hover; the climb is the
    // vertical speed 1 and 2 s after the 2 kg hover's mixer throttle is held 1 point higher, less the same flight held at
    // the hover. Each is a mixer throttle, so one flight serves every curve: all fly a straight curve (50 / 0, so the ramp is
    // even in mixer throttle), and the stick is that throttle read back through each curve (ThrottleCommandOf); a curve's
    // climb for 1 % of stick is its slope at the hover times the climb for a point. The ramp's rate moves the liftoff by up
    // to half a point (0.25 against 0.5 % of stick a second).
    public sealed class LiftoffTests
    {
        const ulong Seed = 0x450F;

        // The liftoff's ramp: the stick brought up over LeadIn s to RampBelowHover of mixer throttle under the hover,
        // then raised RampPerSecond of stick a second; airborne once no contact has touched for Airborne s.
        const double RampPerSecond = 0.005, RampBelowHover = 0.06, LeadIn = 3, Airborne = 0.1;

        static readonly double[] Payloads = { 0, 1, 2 };

        // The pilot's own fc-settings.json as they fly it (model 108): thr_mid 78, theirs since before D-066, and the thr_expo
        // 36 D-076's reading gave it.
        const string PilotsSave = "{\"format\": \"fpv450 fc settings 2\", \"settings\": {\"thr_mid\": 78, \"thr_expo\": 36}}";

        static SessionInputs Inputs(double payload, FcSettings settings, Action<FlightSetup> adjust)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            return new SessionInputs
            {
                Drone = RepositoryDrone.Spec(), Materials = materials, MaterialsHash = hash, Settings = settings,
                Choices = new FlightChoices { Payload = payload, Legs = true, Wind = WindLevel.Calm },
                Adjust = adjust,
            };
        }

        static FlightCore Armed(SessionInputs inputs)
        {
            var core = new FlightCore(SessionSetup.Build(inputs, Seed, out _, out _));
            for (int k = 0; k < 8; k++) core.Advance(new StickSample { Arm = false });
            for (int k = 0; k < 8; k++) core.Advance(new StickSample { Arm = true });
            Assert.That(core.State.Armed, Is.True, "armed");
            return core;
        }

        static double MixerThrottle(FlightCore core) => (core.State.Fc.Throttle - 1000) / 1000;

        static FlightCore Aloft(double payload, FcSettings settings) =>
            Armed(Inputs(payload, settings, s => s.StartPosition += new Double3(0, 100, 0)));

        // The mixer throttle holding a hover 100 m up: the mean of the last two seconds of eight.
        static double HoverThrottle(double payload, FcSettings settings)
        {
            FlightCore core = Aloft(payload, settings);
            var pilot = new SimPilot(core.Controller, core.State.Position);
            double sum = 0;
            int n = 0;
            for (int k = 0; k < 8000; k++)
            {
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                if (k < 6000) continue;
                sum += MixerThrottle(core);
                n++;
            }
            return sum / n;
        }

        // The vertical speed 1 and 2 s after the throttle is held at the hover plus `points` of mixer throttle: the closed
        // loop's eight seconds 100 m up, a second held at the hover to level off, then the step (its pilot holding the
        // position, the throttle held). Relative to the speed at the step.
        static double[] Climb(double payload, FcSettings settings, double hover, double points)
        {
            FlightCore core = Aloft(payload, settings);
            var pilot = new SimPilot(core.Controller, core.State.Position);
            for (int k = 0; k < 8000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            pilot.HeldThrottle = StickFor(core.Controller, hover);
            for (int k = 0; k < 1000; k++) core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
            double v0 = core.State.Velocity.Y;
            pilot.HeldThrottle = StickFor(core.Controller, hover + points / 100);
            var v = new double[2];
            for (int k = 1; k <= 2000; k++)
            {
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                if (k % 1000 == 0) v[k / 1000 - 1] = core.State.Velocity.Y - v0;
            }
            return v;
        }

        // The mixer throttle at which the drone standing on its legs leaves the floor: when the Airborne s without a
        // touching contact began.
        static double LiftoffThrottle(double payload, FcSettings settings, double hover)
        {
            FlightCore core = Armed(Inputs(payload, settings, null));
            double from = StickFor(core.Controller, hover - RampBelowHover), liftoff = double.NaN;
            int lead = (int)(LeadIn / FlightCore.Dt), airborne = (int)(Airborne / FlightCore.Dt), free = 0;
            for (int k = 0; k < lead + (int)(2 * RampBelowHover / RampPerSecond / FlightCore.Dt); k++)
            {
                double stick = k < lead ? from * k / lead : from + RampPerSecond * (k - lead) * FlightCore.Dt;
                core.Advance(new StickSample { Throttle = stick, Arm = true });
                if (k < lead) continue;
                if (core.Trace.ContactsTouching > 0)
                {
                    free = 0;
                    continue;
                }
                if (free++ == 0) liftoff = MixerThrottle(core);
                if (free >= airborne) return liftoff;
            }
            Assert.Fail("no liftoff with a " + payload + " kg bottle");
            return double.NaN;
        }

        static double Mixer(FlightController fc, double stick) => (fc.ThrottleCommandOf(stick) - 1000) / 1000;

        // The stick whose curve gives this mixer throttle.
        static double StickFor(FlightController fc, double throttle)
        {
            double lo = 0, hi = 1;
            for (int it = 0; it < 60; it++)
            {
                double mid = (lo + hi) / 2;
                if (Mixer(fc, mid) < throttle) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        // The curve's slope at this mixer throttle: points of mixer throttle for a point of stick (over ±0.5 % of stick).
        static double Slope(FlightController fc, double throttle)
        {
            double stick = StickFor(fc, throttle);
            return (Mixer(fc, stick + 0.005) - Mixer(fc, stick - 0.005)) / 0.01;
        }

        static FcSettings Curve(double mid, double expo)
        {
            FcSettings s = FcSettingsStore.PlayerDefault;
            s.thr_mid = mid;
            s.thr_expo = expo;
            return s;
        }

        static string P(double x) => (100 * x).ToString("0.0", CultureInfo.InvariantCulture);

        static string F(double x) => x.ToString("0.000", CultureInfo.InvariantCulture);

        [Test]
        public void TheLiftoffHoverAndClimbByStick()
        {
            FcSettings now = FcSettingsStore.PlayerDefault, pilots = FcSettingsStore.FromJson(PilotsSave), linear = Curve(50, 0);
            Assert.AreEqual(new[] { 52.0, 50 }, new[] { now.thr_mid, now.thr_expo }, "the default curve");
            Assert.AreEqual(new[] { 78.0, 36 }, new[] { pilots.thr_mid, pilots.thr_expo }, "the pilot's save as read now: both kept");
            (string name, FcSettings settings)[] curves =
            {
                ("the pilot's save 78/36", pilots),
                ("the old default 100/36", Curve(100, 36)),
                ("the default 52/50", now),
            };
            var fc = new FlightController[curves.Length];
            for (int c = 0; c < curves.Length; c++) fc[c] = new FlightController(curves[c].settings, new[] { true, false, false, true });

            foreach (double payload in Payloads)
            {
                double hover = HoverThrottle(payload, linear), liftoff = LiftoffThrottle(payload, linear, hover);
                var line = new StringBuilder($"a {payload} kg bottle: the mixer throttle at liftoff {P(liftoff)} %, in the hover {P(hover)} %");
                for (int c = 0; c < curves.Length; c++)
                    line.Append($"; {curves[c].name}: liftoff stick {P(StickFor(fc[c], liftoff))} %, hover stick {P(StickFor(fc[c], hover))} %, "
                        + $"slope there {F(Slope(fc[c], hover))}");
                TestContext.WriteLine(line.ToString());
                Assert.That(liftoff, Is.LessThan(hover), payload + " kg: the ground's cushion lifts it off below the hover's throttle");
                if (payload < 2)
                    Assert.That(100 * StickFor(fc[2], liftoff), Is.InRange(30, 45), payload + " kg: the default lifts off inside the pilot's 30–45 % (D-066)");
                if (payload != 2) continue;

                // The pilot's usual bottle: the default's flat spot is at its hover, and finer there than the pilot's save, not flat.
                Assert.That(Math.Abs(now.thr_mid / 100 - hover), Is.LessThanOrEqualTo(0.01), "thr_mid sits at the 2 kg hover");
                double ratio = Slope(fc[2], hover) / Slope(fc[0], hover);
                Assert.That(ratio, Is.InRange(0.5, 0.7), "the default's stick at the 2 kg hover: finer than the pilot's save, not an altitude hold");

                double[] up = Climb(payload, linear, hover, 1), still = Climb(payload, linear, hover, 0);
                double at1 = up[0] - still[0], at2 = up[1] - still[1];
                var climb = new StringBuilder($"a {payload} kg bottle, 1 point of mixer throttle over the hover: {F(at1)} m/s after 1 s, {F(at2)} m/s after 2 s");
                for (int c = 0; c < curves.Length; c++)
                {
                    double slope = Slope(fc[c], hover);
                    climb.Append($"; {curves[c].name}, 1 % of stick: {F(slope * at1)} m/s after 1 s, {F(slope * at2)} m/s after 2 s");
                }
                TestContext.WriteLine(climb.ToString());
                Assert.That(at1, Is.GreaterThan(0), "a point over the hover climbs");
            }
        }
    }
}
