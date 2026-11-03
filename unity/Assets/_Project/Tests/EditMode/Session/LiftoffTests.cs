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
    // tx-input's throttle default (p6-input-app 1.3, D-076): where the player's drone lifts off its legs and where it
    // hovers, with no bottle, a 1 kg and a 2 kg one, as a stick through the throttle curves before and after. The
    // physics decides both, headless on the sim core through the session's own setup (SessionSetup.Build: drone.json at
    // the loadout, the airframe's effects, Calm, a full pack, the coil full): the hover is the mixer throttle the
    // closed-loop pilot (SimPilot) holds 100 m up; the liftoff is the mixer throttle at which the drone standing on its
    // legs on the floor leaves it, the throttle raised slowly from below the hover. Either is a mixer throttle, so one
    // flight serves every curve: both fly a straight curve (50 / 0, so the ramp is even in mixer throttle), and the
    // stick is that throttle read back through each curve (ThrottleCommandOf). The ramp's rate moves the liftoff by up
    // to half a point (0.25 against 0.5 % of stick a second).
    public sealed class LiftoffTests
    {
        const ulong Seed = 0x450F;

        // The liftoff's ramp: the stick brought up over LeadIn s to RampBelowHover of mixer throttle under the hover,
        // then raised RampPerSecond of stick a second; airborne once no contact has touched for Airborne s.
        const double RampPerSecond = 0.005, RampBelowHover = 0.06, LeadIn = 3, Airborne = 0.1;

        static readonly double[] Payloads = { 0, 1, 2 };

        // The pilot's own fc-settings.json as model 26 flew it: the rows the build before D-066 wrote, thr_mid moved to 78.
        const string PilotsSave = "{\"format\": \"fpv450 fc settings 1\", \"settings\": {\"roll_rc_rate\": 5.5, \"roll_srate\": 22.5, "
            + "\"roll_expo\": 34, \"pitch_rc_rate\": 5.5, \"pitch_srate\": 22.5, \"pitch_expo\": 34, \"yaw_rc_rate\": 8.5, \"yaw_srate\": 26, "
            + "\"yaw_expo\": 15, \"thr_mid\": 78, \"thr_expo\": 10}}";

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

        // The mixer throttle holding a hover 100 m up: the mean of the last two seconds of eight.
        static double HoverThrottle(double payload, FcSettings settings)
        {
            FlightCore core = Armed(Inputs(payload, settings, s => s.StartPosition += new Double3(0, 100, 0)));
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

        // The stick whose curve gives this mixer throttle.
        static double StickFor(FlightController fc, double throttle)
        {
            double lo = 0, hi = 1;
            for (int it = 0; it < 60; it++)
            {
                double mid = (lo + hi) / 2;
                if ((fc.ThrottleCommandOf(mid) - 1000) / 1000 < throttle) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }

        static FcSettings Curve(double mid, double expo)
        {
            FcSettings s = FcSettingsStore.PlayerDefault;
            s.thr_mid = mid;
            s.thr_expo = expo;
            return s;
        }

        static string P(double x) => (100 * x).ToString("0.0", CultureInfo.InvariantCulture);

        [Test]
        public void TheLiftoffAndHoverStickByLoad()
        {
            FcSettings before = Curve(78, 10), after = FcSettingsStore.PlayerDefault, pilotsAfter = FcSettingsStore.FromJson(PilotsSave), linear = Curve(50, 0);
            Assert.AreEqual(new[] { 100.0, 36 }, new[] { after.thr_mid, after.thr_expo }, "the default curve");
            Assert.AreEqual(new[] { 78.0, 36 }, new[] { pilotsAfter.thr_mid, pilotsAfter.thr_expo }, "the pilot's save as read now: its thr_mid kept");
            (string name, FcSettings settings)[] curves =
            {
                ("before, the pilot's save 78/10", before),
                ("model 26's default 100/100", Curve(100, 100)),
                ("after, the default 100/36", after),
                ("after, the pilot's save 78/36", pilotsAfter),
            };
            var fc = new FlightController[curves.Length];
            for (int c = 0; c < curves.Length; c++) fc[c] = new FlightController(curves[c].settings, new[] { true, false, false, true });

            foreach (double payload in Payloads)
            {
                double hover = HoverThrottle(payload, linear), liftoff = LiftoffThrottle(payload, linear, hover);
                var line = new StringBuilder($"a {payload} kg bottle: the mixer throttle at liftoff {P(liftoff)} %, in the hover {P(hover)} %");
                for (int c = 0; c < curves.Length; c++)
                    line.Append($"; {curves[c].name}: liftoff stick {P(StickFor(fc[c], liftoff))} %, hover stick {P(StickFor(fc[c], hover))} %");
                TestContext.WriteLine(line.ToString());

                double was = StickFor(fc[0], liftoff), now = StickFor(fc[2], liftoff), pilots = StickFor(fc[3], liftoff);
                Assert.That(liftoff, Is.LessThan(hover), payload + " kg: the ground's cushion lifts it off below the hover's throttle");
                Assert.That(now, Is.LessThan(was), payload + " kg: the default lifts off earlier than the pilot's save did");
                Assert.That(pilots, Is.LessThan(was), payload + " kg: the pilot's save, read now, lifts off earlier than it did");
                Assert.That(100 * now, Is.InRange(30, 45), payload + " kg: the default lifts off inside the pilot's 30–45 % (D-066)");
            }
        }
    }
}
