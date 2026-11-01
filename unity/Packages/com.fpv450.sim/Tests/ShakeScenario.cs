using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// airframe-effects' "Shake only when its causes meet" (p7b-airframe 1.4, PHY-10, PR-1): arrested descents of the
    /// repository's drone, legs on, at a payload, with its asymmetry, rings, flapping and the airframe's modes, in still air
    /// 300 m up. The closed-loop pilot hovers it for HoverSeconds; then the throttle is chopped (ChopStick) for a drop time,
    /// so it falls; then the pilot's input to arrest it, roll, pitch and yaw centred: a sharp step to a stick, or a gentle
    /// ramp to the hover's stick plus GentleStep over GentleRamp; held until the descent is arrested (or for HoldLimit), then
    /// the hover's stick, sticks centred, to the window's end. A drop of 0 is the same input from the hover.
    /// The image's shake is the camera's turn above LowCorner (the flight's own motion is below it): its rate (the body's
    /// and the frame's elastic turn, the camera's angular velocity), high-passed, and its angle, the rate through the same
    /// first-order high-pass; it shows in a field whose RMS angle is at least Visible.
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
        /// A full punch's shake lasts the arrest (D-049, D-051): an ESC on its current limit for the whole arrest (within
        /// 0.03 s) and all four for part of it, and the shake showing and never outlasting the arrest by more than Settle.
        /// On 40 seeds a cell it holds on every full punch. Its end is bounded above only: on 2 of 240 the visible shake fades
        /// below the line before the arrest ends (2 kg from 5.4 m/s), and on 4 weak ones from 2.5 m/s it shows only in the
        /// settling just after the arrest (0.10–0.22 s long).
        /// </summary>
        public static bool LastsTheArrest(in Result r) =>
            !double.IsNaN(r.ArrestedAfter) && DetMath.Abs(r.Limited - r.ArrestedAfter) <= 0.03 && r.AllLimited > 0
            && r.Fields > 0 && r.First + r.Duration <= r.ArrestedAfter + Settle;

        /// <summary>
        /// PHY-09's wobble, not the shake (D-049): a gentle ramp does not arrest a 5–9 m/s descent within HoldLimit, so the
        /// rotors sit in their ring for over 1 s of the window and the wandering thrust shows over the visible line, while an
        /// ESC meets its limit for at most half the full punch's time at the same descent (0.43 at most on 40 seeds a cell).
        /// </summary>
        public static bool Wobbles(in Result r, in Result punch) => r.Fields > 0 && r.InRing > 1.0 && r.Limited <= 0.5 * punch.Limited;

        /// <summary>The frame's band rings on a sharp step: the camera vibration's Y band (50–200 Hz) over this, m/s² (derived:
        /// between the gentle input from the hover's 0.11 at most and every sharp step's 0.42 at least, on 40 seeds a cell).</summary>
        public const double Rings = 0.3;

        public struct Result
        {
            /// <summary>The descent as the input began, m/s down; and when it was arrested, s after (NaN: not in HoldLimit).</summary>
            public double Descent, ArrestedAfter;

            /// <summary>The fields the shake showed in, the first's and the last's start after the input, s; their span (the
            /// shake's length, 0 for none), and the largest field RMS turn, degrees.</summary>
            public int Fields;
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

        /// <summary>The known-wrong ESC without a current limit (D-051: an ESC that senses no current, as Bluejay's), A.</summary>
        public const double NoCurrentLimit = 1e4;

        public static FlightSetup Setup(DroneSpec spec, double payload, ulong seed, bool modes = true, bool currentLimit = true)
        {
            var setup = new FlightSetup
            {
                Seed = seed, World = FlightWorld.Floor(), Mass = spec.BuildMass(new Loadout { Payload = payload, Legs = true }),
                Propulsion = spec.BuildPropulsion(), Aero = spec.BuildAero(), Wind = WindSetup.Of(WindLevel.Still),
                Asymmetry = spec.BuildAsymmetry(), VortexRing = spec.BuildVortexRing(), Flapping = spec.BuildFlapping(),
                StartPosition = new Double3(0, 300, 0), StartPackCharge = 0.9,
            };
            if (modes) setup.Modes = spec.BuildModes();
            if (!currentLimit)
                for (int i = 0; i < setup.Propulsion.Motors.Length; i++) setup.Propulsion.Motors[i].EscCurrentLimit = NoCurrentLimit;
            return setup;
        }

        /// <summary>Flies one arrested descent: drop s of chop, then a sharp step to stick (gentle false) or the gentle ramp.</summary>
        public static Result Fly(FlightSetup setup, double drop, bool gentle, double stick = 1,
            System.Action<FlightCore, StickSample> after = null)
            => Fly(new FlightCore(setup), setup, drop, gentle, stick, after);

        public static Result Fly(FlightCore core, FlightSetup setup, double drop, bool gentle, double stick = 1,
            System.Action<FlightCore, StickSample> after = null)
        {
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            const int arm = 16;
            int hover = arm + (int)(HoverSeconds * 1000), input = hover + (int)(drop * 1000), end = input + (int)(WindowSeconds * 1000);
            var r = new Result { ArrestedAfter = double.NaN };
            double hoverStick = 0, cut = 1 - 6.283185307179586 * LowCorner * FlightCore.Dt;
            Double3 rateLow = default, angle = default;
            double fieldSum = 0;
            int fieldCount = 0, arrested = -1;
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
                    double throttle = !gentle ? stick : from + (hoverStick + GentleStep - from) * DetMath.Min(1, t / GentleRamp);
                    if (arrested < 0 && k > input + 50 && (s.Velocity.Y >= 0 || t >= HoldLimit))
                    {
                        arrested = k;
                        if (s.Velocity.Y >= 0) r.ArrestedAfter = t;
                    }
                    sticks = new StickSample { HostUs = us, Arm = true, Throttle = arrested >= 0 ? hoverStick : throttle };
                }
                core.Advance(sticks);
                after?.Invoke(core, sticks);
                if (k < input) continue;

                // The camera's turn above the corner: its angular velocity less a first-order low-pass of it, and the angle
                // that high-passed rate turns, leaking at the same corner.
                core.GetSignals(out FeedSignals signals);
                Double3 w = signals.CameraAngularVelocity;
                rateLow = rateLow * cut + w * (1 - cut);
                angle = angle * cut + (w - rateLow) * FlightCore.Dt;
                fieldSum += angle.X * angle.X + angle.Y * angle.Y + angle.Z * angle.Z;
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
                if (++fieldCount < FlightCore.FieldSteps) continue;
                double rms = DetMath.Sqrt(fieldSum / fieldCount) * RadToDeg, at = (k + 1 - input - fieldCount) * FlightCore.Dt;
                fieldSum = 0;
                fieldCount = 0;
                r.Peak = DetMath.Max(r.Peak, rms);
                r.Published |= (signals.Live & FeedSignalSet.CameraVibration) != 0;
                Double3 v = signals.CameraVibration;
                r.VibrationPeak = new Double3(DetMath.Max(r.VibrationPeak.X, v.X), DetMath.Max(r.VibrationPeak.Y, v.Y), DetMath.Max(r.VibrationPeak.Z, v.Z));
                if (!(rms >= Visible)) continue;
                if (r.Fields++ == 0) r.First = at;
                r.Last = at;
            }
            r.Duration = r.Fields == 0 ? 0 : r.Last + FlightCore.FieldSteps * FlightCore.Dt - r.First;
            return r;
        }

        public static string Describe(in Result r) =>
            $"{r.Descent,5:F2} m/s down, arrested {(double.IsNaN(r.ArrestedAfter) ? "  —  " : r.ArrestedAfter.ToString("F2") + " s")}; shake in {r.Fields,2} fields, "
            + $"{r.First:F2}–{r.Last + 0.02:F2} s ({r.Duration:F2} s), peak {r.Peak:F3}°; vibration X {r.VibrationPeak.X:F2} Y {r.VibrationPeak.Y:F2} "
            + $"Z {r.VibrationPeak.Z:F2} m/s²; ring {r.InRing:F2} s, ESC limit {r.Limited:F2} s (all four {r.AllLimited:F2} s, the last off at {r.LimitEnds:F2} s); tilt to {r.TiltMax:F2}°";
    }
}
