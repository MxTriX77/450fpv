using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// airframe-effects' "Heavy on low throttle" (p7b-airframe 1.2): the repository's drone at 6.2 kg (p5-drone's L6, the
    /// loaded drone: the 2 kg payload, legs on, the full coil), its asymmetry and drag, in still air 300 m up. Armed, it hovers under the
    /// closed-loop pilot for HoverSeconds, then the pilot flies it down at 3 m/s (a target sinking at Descent) until the
    /// descent has settled; then the throttle is held, roll, pitch and yaw centred, for HoldSeconds: low (LowThrottle) or
    /// higher (the pilot's own throttle there plus HighThrottleStep, which arrests the descent); then the pilot recovers
    /// to the height the hold ended at. The flight with the ring off (FlightSetup.VortexRing null) is the same flight
    /// until a ring forms, bit for bit: the comparison for the thrust's loss and the wobble.
    /// The suite asserts it (VortexRingTests); tools/flightbench --scenarios prints it.
    /// </summary>
    public static class VortexRingScenario
    {
        /// <summary>The hold, s: the rings' whole passage on the low throttle (derived, QA's finding 4): the rotors enter
        /// within 0.04 s and the falling drone carries all four past η 2.0, the windmill brake state, 0.53–0.57 s into the
        /// hold on seeds 101–120, so the wobble's statistic sees every step the rings wander (0.4 s saw about 80 % of it).</summary>
        public const double HoldSeconds = 0.6;

        public const double Descent = 3, HoverSeconds = 2, DescentSeconds = 3, RecoverSeconds = 3;
        public const double LowThrottle = 0.15, HighThrottleStep = 0.1;
        public const ulong Seed = 0x450F;
        const double G = 9.80665;

        public struct Result
        {
            /// <summary>The held throttle stick, and the descent (m/s, down positive) as the hold began and as it ended.</summary>
            public double Throttle, DescentAtHold, DescentAtEnd;

            /// <summary>Entries and exits of any rotor: in the spin-up (the arming and the hover's first second, the drone sinking
            /// as its idling rotors spin up), after it to the hold (the settled hover and the pilot's 3 m/s descent), in the
            /// hold, and in all.</summary>
            public int SpinUpEntries, EntriesBefore, EntriesInHold, Entries, Exits;

            /// <summary>The hold's first entry: its η, the descent then (m/s), its rotor's v_h (m/s), and when, s into the hold.</summary>
            public double EntryRate, EntryDescent, EntryInduced, EntryAfter;

            /// <summary>The last exit, and the lowest η a rotor kept its ring at while η fell (the hysteresis).</summary>
            public double ExitRate, LowestHeld;

            /// <summary>Over the hold: the rotors' mean thrust with their rings over the same rotors at the same speeds and
            /// inflow without them (Propulsion.Aero), and the deepest η.</summary>
            public double ThrustRatio, DeepestRate;

            /// <summary>Over the hold: the body's roll and pitch rate, RMS rad/s; its change over 10 ms, RMS rad/s (the wobble,
            /// apart from the throttle's slow transient); the largest tilt from level, degrees; and the roll and pitch moment
            /// the rings' thrust fluctuation makes, RMS N·m.</summary>
            public double RateRms, WobbleRms, TiltMax, RingMomentRms;
        }

        /// <summary>The 20 seeds the suite flies the scenario on (QA's 101–120), besides Seed.</summary>
        public static readonly ulong[] Seeds = { 101, 102, 103, 104, 105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120 };

        public static FlightSetup Setup(DroneSpec spec, bool ring = true, bool knownWrong = false, ulong seed = Seed)
        {
            MassModel mass = spec.BuildMass(new Loadout { Payload = 2.0, Legs = true });
            var setup = new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = mass, Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(),
                Wind = WindSetup.Of(WindLevel.Still), Asymmetry = spec.BuildAsymmetry(), StartPosition = new Double3(0, 300, 0), StartPackCharge = 0.9,
            };
            // The ring's wobble is set against the same flight without it: the motors' own spool-down spread (D-064) wobbles
            // both and is graded by GroundMicroMotionProbe, so this flight keeps design 1's tolerances only.
            setup.Asymmetry.ResistanceSigma = setup.Asymmetry.FrictionSigma = setup.Asymmetry.InertiaSigma = 0;
            if (ring) setup.VortexRing = spec.BuildVortexRing();
            if (knownWrong)
            {
                // Descent speed alone: the rates over the hover's v_h at the drone's weight, whatever the thrust.
                double area = 3.141592653589793 * spec.Props.Diameter * spec.Props.Diameter / 4;
                setup.VortexRing.FixedInducedVelocity = DetMath.Sqrt(mass.Properties.Mass * G / 4 / (2 * setup.VortexRing.AirDensity * area));
            }
            return setup;
        }

        public static Result Fly(FlightSetup setup, bool low, double lowThrottle = LowThrottle) => Fly(setup, low, out _, lowThrottle);

        /// <summary>Flies the scenario; low holds lowThrottle (LowThrottle), or below the pilot's own throttle there by its
        /// negative, else the pilot's plus HighThrottleStep. after sees each step (a log's recorder).</summary>
        public static Result Fly(FlightSetup setup, bool low, out FlightCore core, double lowThrottle = LowThrottle,
            System.Action<FlightCore, StickSample> after = null)
            => Fly(new FlightCore(setup), setup, low, out core, lowThrottle, after);

        public static Result Fly(FlightCore flight, FlightSetup setup, bool low, out FlightCore core, double lowThrottle = LowThrottle,
            System.Action<FlightCore, StickSample> after = null)
        {
            core = flight;
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            var r = new Result { EntryRate = double.NaN, ExitRate = double.NaN, LowestHeld = double.NaN };
            const int arm = 16, wander = 10;
            int spinUp = arm + 1000, hover = arm + (int)(HoverSeconds * 1000), down = hover + (int)(DescentSeconds * 1000), hold = down + (int)(HoldSeconds * 1000);
            int end = hold + (int)(RecoverSeconds * 1000);
            StickSample held = default;
            double ratioSum = 0, rate = 0, wobble = 0, moment = 0;
            var past = new Double3[wander];
            int n = 0;
            for (int k = 0; k < end; k++)
            {
                ref readonly FlightState s = ref core.State;
                long us = s.Step * FlightCore.StepUs;
                StickSample sticks;
                if (k < arm) sticks = new StickSample { HostUs = us, Arm = k >= 8 };
                else if (k < down)
                {
                    if (k >= hover) pilot.Target = new Double3(0, setup.StartPosition.Y - Descent * (k - hover) * FlightCore.Dt, 0);
                    sticks = pilot.Sticks(core, us);
                }
                else if (k < hold)
                {
                    if (k == down)
                    {
                        held = pilot.Sticks(core, us);
                        held.Roll = held.Pitch = held.Yaw = 0;
                        held.Throttle = !low ? held.Throttle + HighThrottleStep : lowThrottle >= 0 ? lowThrottle : held.Throttle + lowThrottle;
                        r.Throttle = held.Throttle;
                        r.DescentAtHold = -s.Velocity.Y;
                    }
                    sticks = held;
                    sticks.HostUs = us;
                }
                else
                {
                    if (k == hold) pilot.Target = new Double3(s.Position.X, s.Position.Y, s.Position.Z);
                    sticks = pilot.Sticks(core, us);
                }
                int was = s.RingLatched;
                core.Advance(sticks);
                after?.Invoke(core, sticks);
                ref readonly FlightState a = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    ref readonly FlightEvent e = ref core.Events[i];
                    if (e.Kind == EventKind.VortexRingEntered)
                    {
                        r.Entries++;
                        if (k < spinUp) r.SpinUpEntries++;
                        else if (k < down) r.EntriesBefore++;
                        else if (k >= down && k < hold && r.EntriesInHold++ == 0)
                        {
                            r.EntryRate = e.A;
                            r.EntryDescent = -a.Velocity.Y;
                            r.EntryInduced = e.C;
                            r.EntryAfter = (k - down + 1) * FlightCore.Dt;
                        }
                    }
                    else if (e.Kind == EventKind.VortexRingLeft)
                    {
                        r.Exits++;
                        r.ExitRate = e.A;
                    }
                }
                Propulsion p = core.Propulsion;
                if (p.HasRing)
                    for (int i = 0; i < 4; i++)
                    {
                        double eta = p.RingRate(i);
                        if ((a.RingLatched & (1 << i)) != 0 && (was & (1 << i)) != 0 && eta < setup.VortexRing.EntryRate
                            && !(eta >= r.LowestHeld) && k >= hold)
                            r.LowestHeld = eta;
                        if (k >= down && k < hold) r.DeepestRate = DetMath.Max(r.DeepestRate, eta);
                    }
                if (k >= down && k < hold)
                {
                    double with = 0, without = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        p.Aero(a.RotorSpeed[i], p.Inflow[i], out double t, out _);
                        with += p.MeanThrust(i);
                        without += t;
                    }
                    ratioSum += with / without;
                    rate += a.BodyRate.X * a.BodyRate.X + a.BodyRate.Z * a.BodyRate.Z;
                    Double3 dw = a.BodyRate - past[k % wander];
                    wobble += dw.X * dw.X + dw.Z * dw.Z;
                    double mx = 0, mz = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        double dt = p.MeanThrust(i) * a.RingThrust[i];
                        mx -= p.Hub(i).Z * dt;
                        mz += p.Hub(i).X * dt;
                    }
                    moment += mx * mx + mz * mz;
                    n++;
                    Double3 up = a.Attitude.Rotate(new Double3(0, 1, 0));
                    r.TiltMax = DetMath.Max(r.TiltMax, System.Math.Acos(DetMath.Min(1, up.Y)) * 57.29577951308232);
                }
                if (k == hold - 1) r.DescentAtEnd = -a.Velocity.Y;
                past[k % wander] = a.BodyRate;
            }
            r.ThrustRatio = ratioSum / n;
            r.RateRms = DetMath.Sqrt(rate / n);
            r.WobbleRms = DetMath.Sqrt(wobble / n);
            r.RingMomentRms = DetMath.Sqrt(moment / n);
            return r;
        }

        /// <summary>The scenario's verdict on one flight held low, with the ring-off flight held the same: entered within the
        /// hold's first 0.1 s at the 3 m/s descent (at most 3.5 m/s), from η at or past the entry; lost thrust; wobbled at
        /// least 1.5 times the ringless flight's 10 ms rate change; and stayed in the ring below the entry rate on the way
        /// out, leaving below the exit rate.</summary>
        public static bool HeavyOnLowThrottle(in Result on, in Result off, VortexRingSpec ring) =>
            on.EntriesBefore == 0 && on.EntriesInHold >= 1 && on.EntryAfter <= 0.1 && on.EntryDescent <= 3.5 && on.EntryRate >= ring.EntryRate
            && on.ThrustRatio < 0.97 && on.RingMomentRms > 0 && on.WobbleRms >= 1.5 * off.WobbleRms
            && on.LowestHeld < ring.EntryRate && on.ExitRate < ring.ExitRate && on.Exits >= on.Entries;

        /// <summary>The higher throttle's verdict: no ring from the hover's settled second to the recovery's end.</summary>
        public static bool HigherThrottle(in Result r) => r.Entries == r.SpinUpEntries;

        public static string Describe(in Result r) =>
            $"stick {r.Throttle:F3}, descending {r.DescentAtHold:F2} → {r.DescentAtEnd:F2} m/s over the {HoldSeconds:F1} s hold; "
            + $"{r.Entries} entries ({r.SpinUpEntries} in the spin-up, {r.EntriesBefore} in the hover and the descent, {r.EntriesInHold} in the hold), {r.Exits} exits; the hold's first entry after {r.EntryAfter * 1000:F0} ms at η "
            + $"{r.EntryRate:F3} ({r.EntryDescent:F2} m/s, v_h {r.EntryInduced:F2} m/s), deepest η {r.DeepestRate:F2}; last exit at η {r.ExitRate:F3}, held to η "
            + $"{r.LowestHeld:F3}; thrust {r.ThrustRatio * 100:F1} % of the ringless rotors'; roll and pitch rate {r.RateRms:E2} rad/s RMS, "
            + $"its 10 ms change {r.WobbleRms:E2} rad/s RMS, tilt to {r.TiltMax:F2}°, the rings' moment {r.RingMomentRms:E2} N·m RMS";
    }
}
