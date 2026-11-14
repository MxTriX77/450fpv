using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// airframe-effects' "Shake only when its causes meet" (p7b-airframe 1.4, PHY-10, PR-1): arrested descents of the
    /// repository's drone, legs on, at a payload, with its asymmetry, rings, flapping, the rotors' own wakes (design 3g, as
    /// every pilot's flight flies them) and the airframe's modes, in still air 300 m up (the ground's wash moot there, and a
    /// vertical punch forms no interference). The closed-loop pilot hovers it for HoverSeconds; then the throttle is chopped (ChopStick) for a drop time,
    /// so it falls; then the pilot's input to arrest it, roll, pitch and yaw centred: a sharp step to a stick, or a gentle
    /// ramp to the hover's stick plus GentleStep over GentleRamp; held until the descent is arrested (or for HoldLimit), then
    /// the hover's stick, sticks centred, to the window's end. A drop of 0 is the same input from the hover.
    /// The image's shake is the camera's turn above LowCorner (the flight's own motion is below it): its rate (the body's
    /// and the frame's elastic turn, the camera's angular velocity), high-passed, and its angle, the rate through the same
    /// first-order high-pass; it shows in a field whose RMS angle reaches Margin times the same flight's hover's RMS, and at
    /// least Visible (design 3i: an excess over the drone's own floor), and its fields form episodes (JoinFields).
    /// </summary>
    public static class ShakeScenario
    {
        public const double HoverSeconds = 3, ChopStick = 0.05, GentleStep = 0.15, GentleRamp = 0.6, HoldLimit = 1.5;

        /// <summary>The window after the input, s: it sees the end of the longest shake (QA's finding 1: the heavy punch from
        /// 9 m/s is arrested at up to 1.41 s, so 1.5 s cut it), HoldLimit plus Settle plus a margin (derived).</summary>
        public const double WindowSeconds = 3;

        /// <summary>The jitter's corner, Hz: the camera vibration's X band's (VibrationBands.LowCorner, assumed).</summary>
        public const double LowCorner = VibrationBands.LowCorner;

        /// <summary>
        /// The visible shake, the field's RMS turn, degrees: 0.088 (derived: one effective luma sample, K§11.5's 450 across
        /// p4-feed's 106° at its f 1.005, 0.25° at the centre, peak to peak of a sine, 0.25/(2√2)).
        /// </summary>
        public const double Visible = 0.25 / 2.8284271247461903;

        /// <summary>A field, s (FlightCore.FieldSteps steps: one PAL field).</summary>
        public const double FieldSeconds = FlightCore.FieldSteps * FlightCore.Dt;

        /// <summary>The hover the window is measured against, s: the hover's last 2 s before the chop, 100 fields (design 3i;
        /// assumed: the hover's first second is the pilot's settling and the filter's).</summary>
        public const double ReferenceSeconds = 2;

        /// <summary>
        /// The margin over the hover's own RMS a field must reach to show as the shake (design 3i, derived): the largest
        /// window peak over its own hover's RMS of 10 000 null flights a payload (ShakeAttributionProbe.TheHoldsMargin: 3.657
        /// empty, 3.761 at 1 kg, 3.501 at 2 kg, the 99th percentiles 2.77–2.91; seeds 1–10 000), rounded up, so a fresh null
        /// flight reads as a shake with probability about 1/10 001. At model 96 the hover's RMS is 0.027–0.073° (median 0.045°
        /// empty), so the threshold is 0.10–0.27°, about twice the visible line.
        /// </summary>
        public const double Margin = 3.77;

        /// <summary>Fields over the threshold are one episode while the next starts within this many fields of the last (design
        /// 3i, derived): the band's lowest frequency, LowCorner, peaks every half period, 1/(2·5 Hz) = 0.1 s = 5 fields, and a
        /// field near the zero crossing between two peaks can dip under the threshold.</summary>
        public const int JoinFields = 5;

        /// <summary>
        /// The settling after a full punch's arrest, s: the shake ends within it once the descent is arrested, the ESCs off
        /// their limit and the hover's stick back, as the loop levels the tilt (derived, QA's finding 8: the builder's loop
        /// settles a 200 °/s step within 5 % in 0.21 s on pitch and 0.31 s on roll, TheBuildersPidsHoldAStableLoop with the
        /// modes on; on 40 seeds a cell every full punch's visible shake ends 0.08–0.26 s after its arrest or before it).
        /// </summary>
        public const double Settle = 0.31;

        const double RadToDeg = 57.29577951308232, TwoPi = 6.283185307179586;

        /// <summary>
        /// The pilot's 0.3–0.7 s for the empty drone's moderate descents (PR-1, K§4.1, D-049), read to the text's tenth (a
        /// field is 20 ms, and the first and last fields over the visible line move by one with the seed).
        /// </summary>
        public static bool LastsInTenths(double seconds, double low, double high)
        {
            double tenths = System.Math.Round(seconds * 10, System.MidpointRounding.AwayFromZero) / 10;
            return tenths >= low && tenths <= high;
        }

        /// <summary>
        /// A full punch's shake ends with its arrest (D-066): the descent arrested, and the shake, where it shows, never
        /// outlasting the arrest by more than Settle. Its end is bounded above only: the visible shake can fade below the line
        /// before the arrest ends (2 kg), and a weak one from 2.5 m/s shows only in the settling just after it.
        /// </summary>
        public static bool EndsWithTheArrest(in Result r) =>
            !double.IsNaN(r.ArrestedAfter) && (r.Fields == 0 || r.Last + FieldSeconds <= r.ArrestedAfter + Settle);

        /// <summary>A full punch's shake lasts the arrest: it ends with it and shows.</summary>
        public static bool LastsTheArrest(in Result r) => EndsWithTheArrest(r) && r.Fields > 0;

        /// <summary>
        /// With current protection (Setup's protection, D-051's alternative): an ESC on its limit for the whole arrest
        /// (within 0.03 s) and all four for part of it, the shake ending with the arrest (model 22's drone, D-053).
        /// </summary>
        public static bool HoldsTheLimit(in Result r) =>
            EndsWithTheArrest(r) && DetMath.Abs(r.Limited - r.ArrestedAfter) <= 0.03 && r.AllLimited > 0;

        /// <summary>
        /// PHY-09's wobble, not the shake (D-049): a gentle ramp does not arrest a 5–9 m/s descent within HoldLimit, so the
        /// rotors sit in their ring for over 1 s of the window and the wandering thrust shows over the visible line.
        /// </summary>
        public static bool Wobbles(in Result r) => r.Fields > 0 && r.InRing > 1.0;

        /// <summary>The frame's band rings on a sharp step: the camera vibration's Y band (50–200 Hz) over this, m/s² (derived:
        /// between the gentle input from the hover's 0.11 at most and every sharp step's 0.42 at least, on 40 seeds a cell).</summary>
        public const double Rings = 0.3;

        /// <summary>From the hover a sharp step shows only the motors' spin-up kick: shorter than the pilot's shortest shake,
        /// 0.3 s (PR-1). On the class's ESC the kick is D-064's motor spreads, which no current limit trims. At model 96, on
        /// the excess over the hover (design 3i), at most 0.18 s on seeds 1–200 (shown on 150 of 200 empty, 11 at 2 kg).</summary>
        public const double KickSeconds = 0.3;

        /// <summary>The shake lasts past the kick (KickSeconds, the pilot's shortest shake). Below it a full punch shows the
        /// motors' spin-up kick, ringed or ringless. Past it (model 96, design 3i, the six full punches on seeds 1–200) the
        /// re-ingested wash carries most of it (258 of 1200 with the rings, 107 without), D-064's motor spreads a second share
        /// (217 with them off); so the count past it is a rate of the wash's share, not the wash alone.</summary>
        public static bool PastTheKick(in Result r) => r.Fields > 0 && r.Duration >= KickSeconds;

        /// <summary>The ring carries the shake past the kick: without it the shake lasts past the kick on at most two thirds
        /// as many full punches (design 3i; 0.41 on seeds 1–200, a fresh 40 seeds' ratio at most 0.58 at its 99th percentile).</summary>
        public static bool RingCarriesIt(int ringedPast, int ringlessPast) => 3 * ringlessPast <= 2 * ringedPast;

        /// <summary>"Nearly every": the share of a cell's flights a rate PHY-10 states holds on, where a bound is not true on
        /// every seed (QA's re-review, B1; D-053). Its claims hold on 99 % or more of 440 seeds a cell (1–40, 301–500 and
        /// 1001–1200), so a fresh 40 seeds misses one about 0.5 % of the time at most (the rigid airframe's "within three
        /// fields", 436 of 440, on its one cell of 40), far less for the others (240 or 160 flights each).</summary>
        public const double NearlyEvery = 0.95;

        /// <summary>The pilot's "most" for the 0.3–0.7 s after a sudden drop (PR-1): this share of the seeds and the median
        /// inside. At model 96, on the excess over the hover (design 3i), the empty drone's full punch from 9 m/s holds it (160
        /// of 200, the median 0.42 s on seeds 1–200); from 5.3 and 2.5 m/s it does not (52 and 77 of 200, medians 0.14 and
        /// 0.20 s, past KickSeconds on 45 and 26).</summary>
        public const double Most = 0.7;

        /// <summary>The pilot's band on a group of seeds: at least Most of them inside 0.3–0.7 s, and the median.</summary>
        public static bool PilotBand(int inside, int of, double median) => inside >= Most * of && LastsInTenths(median, 0.3, 0.7);

        public struct Result
        {
            /// <summary>The descent as the input began, m/s down; and when it was arrested, s after (NaN: not in HoldLimit).</summary>
            public double Descent, ArrestedAfter;

            /// <summary>The hover's own RMS turn over its last ReferenceSeconds, and the window's threshold (the larger of
            /// Visible and Margin times it), degrees.</summary>
            public double Hover, Threshold;

            /// <summary>The fields over the threshold in the window and the episodes they form (JoinFields); the longest
            /// episode's start after the input and its length (the shake's, 0 for none), s; the last field over the threshold's
            /// start, s; and the window's largest field RMS turn, degrees.</summary>
            public int Fields, Episodes;
            public double First, Last, Duration, Peak;

            /// <summary>The camera vibration's largest field RMS over the window, per band (X, Y, Z), m/s², and whether
            /// the feed's CameraVibration was live.</summary>
            public Double3 VibrationPeak;
            public bool Published;

            /// <summary>Time in the window with a rotor in its ring, with any ESC on its current limit and with all four on
            /// it, s; and when the last ESC left its limit, s after the input (0 for never on it).</summary>
            public double InRing, Limited, AllLimited, LimitEnds;

            /// <summary>The largest height lost or gained against a steady arrest is not asked; the trajectory's error: the
            /// largest tilt from level in the window, degrees.</summary>
            public double TiltMax;
        }

        /// <summary>The ESC with current protection (D-051's alternative, model 22's drone: BLHeli_32's protection at the motor's
        /// 40 A, p6-flight-core 1.5's policy), A. The drone's own ESC, the class's published one, senses no current (D-066).</summary>
        public const double Protection = 40;

        public static FlightSetup Setup(DroneSpec spec, double payload, ulong seed, bool modes = true, bool protection = false)
        {
            var setup = new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(new Loadout { Payload = payload, Legs = true }),
                Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(), Wind = WindSetup.Of(WindLevel.Still),
                Asymmetry = spec.BuildAsymmetry(), VortexRing = spec.BuildVortexRing(), Flapping = spec.BuildFlapping(),
                RotorWake = spec.BuildRotorWake(), // design 3g: before it the scenario flew no own wake
                StartPosition = new Double3(0, 300, 0), StartPackCharge = 0.9,
            };
            if (modes) setup.Modes = spec.BuildModes();
            if (protection)
                for (int i = 0; i < setup.Propulsion.Motors.Length; i++) setup.Propulsion.Motors[i].EscCurrentLimit = Protection;
            return setup;
        }

        /// <summary>Flies one arrested descent: drop s of chop, then a sharp step to stick (gentle false) or the gentle ramp.</summary>
        public static Result Fly(FlightSetup setup, double drop, bool gentle, double stick = 1,
            System.Action<FlightCore, StickSample> after = null)
            => Fly(new FlightCore(setup), setup, drop, gentle, stick, after);

        public static Result Fly(FlightCore core, FlightSetup setup, double drop, bool gentle, double stick = 1,
            System.Action<FlightCore, StickSample> after = null)
            => Fly(core, setup, drop, gentle, stick, GentleStep, after);

        /// <summary>The null flight (design 3i): the hover, then the hover's stick held, sticks centred, for the window, no
        /// chop and no input. What the window shows then is the flight's own floor, and Margin is read off it.</summary>
        public static Result Hold(FlightSetup setup) => Fly(new FlightCore(setup), setup, 0, true, 1, 0, null);

        static Result Fly(FlightCore core, FlightSetup setup, double drop, bool gentle, double stick, double gentleStep,
            System.Action<FlightCore, StickSample> after)
        {
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            const int arm = 16;
            int hover = arm + (int)(HoverSeconds * 1000), input = hover + (int)(drop * 1000), end = input + (int)(WindowSeconds * 1000);
            int reference = hover - (int)(ReferenceSeconds * 1000);
            var r = new Result { ArrestedAfter = double.NaN };
            double hoverStick = 0, cut = 1 - 6.283185307179586 * LowCorner * FlightCore.Dt;
            Double3 rateLow = default, angle = default;
            double fieldSum = 0, referenceSum = 0, threshold = 0;
            int fieldCount = 0, referenceFields = 0, arrested = -1;
            double open = double.NaN; // the open episode's first field's start, s after the input (NaN: none open)
            for (int k = 0; k < end; k++)
            {
                ref readonly FlightState s = ref core.State;
                long us = s.Step * FlightCore.StepUs;
                StickSample sticks;
                if (k < arm) sticks = new StickSample { HostUs = us, Arm = k >= 8 };
                else if (k < hover)
                {
                    sticks = pilot.Sticks(core, us);
                    hoverStick = sticks.Throttle;
                }
                else if (k < input) sticks = new StickSample { HostUs = us, Arm = true, Throttle = ChopStick };
                else
                {
                    if (k == input) r.Descent = -s.Velocity.Y;
                    double from = drop > 0 ? ChopStick : hoverStick, t = (k - input) * FlightCore.Dt;
                    double throttle = !gentle ? stick : from + (hoverStick + gentleStep - from) * DetMath.Min(1, t / GentleRamp);
                    if (arrested < 0 && k > input + 50 && (s.Velocity.Y >= 0 || t >= HoldLimit))
                    {
                        arrested = k;
                        if (s.Velocity.Y >= 0) r.ArrestedAfter = t;
                    }
                    sticks = new StickSample { HostUs = us, Arm = true, Throttle = arrested >= 0 ? hoverStick : throttle };
                }
                core.Advance(sticks);
                after?.Invoke(core, sticks);
                if (k < arm) continue;

                // The camera's turn above the corner: its angular velocity less a first-order low-pass of it, and the angle
                // that high-passed rate turns, leaking at the same corner. Run from the pilot's first step, so the hover's
                // fields are measured as the window's are.
                core.GetSignals(out FeedSignals signals);
                Double3 w = signals.CameraAngularVelocity;
                rateLow = rateLow * cut + w * (1 - cut);
                angle = angle * cut + (w - rateLow) * FlightCore.Dt;
                fieldSum += angle.X * angle.X + angle.Y * angle.Y + angle.Z * angle.Z;
                bool window = k >= input;
                if (window)
                {
                    ref readonly FlightState a = ref core.State;
                    if (a.RingLatched != 0) r.InRing += FlightCore.Dt;
                    if (a.EscLimited != 0)
                    {
                        r.Limited += FlightCore.Dt;
                        r.LimitEnds = (k + 1 - input) * FlightCore.Dt;
                    }
                    if (a.EscLimited == 0xF) r.AllLimited += FlightCore.Dt;
                    Double3 up = a.Attitude.Rotate(new Double3(0, 1, 0));
                    r.TiltMax = DetMath.Max(r.TiltMax, System.Math.Acos(DetMath.Min(1, up.Y)) * RadToDeg);
                }
                if (++fieldCount < FlightCore.FieldSteps) continue;
                double rms = DetMath.Sqrt(fieldSum / fieldCount) * RadToDeg;
                fieldSum = 0;
                fieldCount = 0;
                if (!window)
                {
                    // The hover's own fields, its last ReferenceSeconds before the chop.
                    if (k + 1 - FlightCore.FieldSteps >= reference && k + 1 <= hover)
                    {
                        referenceSum += rms * rms;
                        referenceFields++;
                    }
                    continue;
                }
                if (threshold == 0)
                {
                    r.Hover = DetMath.Sqrt(referenceSum / referenceFields);
                    threshold = r.Threshold = DetMath.Max(Visible, Margin * r.Hover);
                }
                int field = (k + 1 - input) / FlightCore.FieldSteps - 1;
                double at = field * FieldSeconds;
                r.Peak = DetMath.Max(r.Peak, rms);
                r.Published |= (signals.Live & FeedSignalSet.CameraVibration) != 0;
                Double3 v = signals.CameraVibration;
                r.VibrationPeak = new Double3(DetMath.Max(r.VibrationPeak.X, v.X), DetMath.Max(r.VibrationPeak.Y, v.Y), DetMath.Max(r.VibrationPeak.Z, v.Z));
                if (!(rms >= threshold)) continue;
                if (r.Fields++ > 0 && field - (int)System.Math.Round(r.Last / FieldSeconds) > JoinFields)
                {
                    Close(ref r, open, r.Last);
                    open = double.NaN;
                }
                if (double.IsNaN(open)) open = at;
                r.Last = at;
            }
            if (!double.IsNaN(open)) Close(ref r, open, r.Last);
            return r;
        }

        // An episode from its first field's start to its last field's end; the longest is the shake's.
        static void Close(ref Result r, double first, double last)
        {
            r.Episodes++;
            double length = last + FieldSeconds - first;
            if (length > r.Duration)
            {
                r.Duration = length;
                r.First = first;
            }
        }

        public static string Describe(in Result r) =>
            $"{r.Descent,5:F2} m/s down, arrested {(double.IsNaN(r.ArrestedAfter) ? "  —  " : r.ArrestedAfter.ToString("F2") + " s")}; shake in {r.Fields,2} fields, {r.Episodes} episodes, "
            + $"the longest {r.First:F2} s on ({r.Duration:F2} s), the last field to {r.Last + FieldSeconds:F2} s; peak {r.Peak:F3}° over the hover's "
            + $"{r.Hover:F3}° (the threshold {r.Threshold:F3}°); vibration X {r.VibrationPeak.X:F2} Y {r.VibrationPeak.Y:F2} "
            + $"Z {r.VibrationPeak.Z:F2} m/s²; ring {r.InRing:F2} s, ESC limit {r.Limited:F2} s (all four {r.AllLimited:F2} s, the last off at {r.LimitEnds:F2} s); tilt to {r.TiltMax:F2}°";
    }
}
