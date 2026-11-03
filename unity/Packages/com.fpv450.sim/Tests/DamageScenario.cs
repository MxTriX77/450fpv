using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7f-damage 1.2's flown scenarios: the repository's drone at its nominal loadout in still air 300 m up, as the player
    /// flies it (the motors' asymmetry with D-064's spreads and the factory draws, the vortex rings, each rotor's own wake, the
    /// flapping and the airframe's modes; the rotors in each other's wake left out: in a still-air hover their wakes go
    /// down, and its table's 1.3 s build would cost the 20 000 seeds' hovers hours), armed and held on station by SimPilot
    /// for Seconds; the last Seconds − MeasuredFrom are measured. A damage is set on the blades before the flight arms (a
    /// fixture before strikes exist), and the rotors fly it from their first turn.
    /// </summary>
    public static class DamageScenario
    {
        public const double Seconds = 10, MeasuredFrom = 5;

        /// <summary>The factory band's 99.5th percentile, m/s²: the camera's 50–200 Hz RMS of the hover on seeds 1–20 000's
        /// factory draws (tools/flightbench --factory, model 29: 0.538 / 0.605 / 0.680 at 0.5 / 50 / 99.5 %), pinned here for the
        /// scenarios that compare with it. Most of it is each rotor's own wake (D-064) at the arms' mode near 60 Hz: the same
        /// hovers without the factory draws read 0.532 / 0.599 / 0.675. Model 40 (the physics integration 2, Betaflight's yaw I
        /// × 2.5 with them): the same to three digits, the 99.5th percentile 0.6796763534824888 against model 35's
        /// 0.6796746668543654.</summary>
        public const double FactoryBandHigh = 0.6796763534824888;
        const double TwoPi = 6.283185307179586, RadToDeg = 57.29577951308232;

        public struct Result
        {
            /// <summary>The camera's 50–200 Hz band (VibrationBands' Y), its RMS over the window, m/s².</summary>
            public double Band;

            /// <summary>Each rotor's mean speed, rad/s, and its mean ESC command (the hover's), over the window.</summary>
            public Corners Speed, Command;

            /// <summary>The roll's and the pitch's peak to peak over the window, degrees (the tracker's angles).</summary>
            public double RollPeakToPeak, PitchPeakToPeak;

            /// <summary>The analysed rotor's 1/rev in the gyro, °/s (the vector of the three axes' amplitudes): the body rate the
            /// gyro reads (before the chain) and the controller's filtered gyro (after it, the RPM notches included).</summary>
            public double GyroOneRev, FilteredOneRev;

            /// <summary>The band's spectrum's peak over 50–200 Hz, Hz, and the analysed rotor's mean rotation rate, Hz.</summary>
            public double PeakHz, RotorHz;

            /// <summary>The packs' total current over the window, A.</summary>
            public double Amps;
        }

        /// <summary>The scenario's setup on a seed; unbalance and pitch false switch the factory draws off (the balance grade
        /// and the pitch bound at 0).</summary>
        public static FlightSetup Setup(DroneSpec spec, ulong seed, bool unbalance = true, bool pitch = true)
        {
            FlightSetup s = CrosswindScenario.Setup(spec, WindSetup.Of(WindLevel.Still), seed);
            s.Asymmetry = spec.BuildAsymmetry();
            if (!unbalance) s.Asymmetry.BalanceGrade = 0;
            if (!pitch) s.Asymmetry.BladePitchBound = 0;
            s.VortexRing = spec.BuildVortexRing();
            s.RotorWake = spec.BuildRotorWake();
            s.Flapping = spec.BuildFlapping();
            s.Modes = spec.BuildModes();
            s.CameraPosition = spec.CameraPosition;
            return s;
        }

        /// <summary>
        /// "The baseline shake is bounded away from imperceptible", paired by seed: on[n] and off[n] are seed n's band with the
        /// factory draws and the same seed's without them (the same rotor-wake stream). The draws' own 1/rev band on a seed is
        /// √(on² − off²), 0 where they lower the band; own returns them sorted. Holds when the draws raise the band on at least
        /// 99.5 % of the seeds and their own band's 0.5th percentile is above zero. The absolute band is not compared across
        /// seeds: each rotor's own wake (D-064) varies between seeds more than the draws add.
        /// </summary>
        public static bool BoundedAway(double[] on, double[] off, out int raised, out double[] own)
        {
            own = new double[on.Length];
            raised = 0;
            for (int n = 0; n < on.Length; n++)
            {
                own[n] = Math.Sqrt(Math.Max(on[n] * on[n] - off[n] * off[n], 0));
                if (on[n] > off[n]) raised++;
            }
            Array.Sort(own);
            return raised >= 0.995 * on.Length && own[(int)(0.005 * on.Length)] > 0;
        }

        /// <summary>One blade of a rotor trimmed by a length (m), or both, through the propulsion's fixture path.</summary>
        public static Action<Propulsion> Trim(int corner, double oneBlade, double otherBlade = 0) => p =>
        {
            DamagedRotor r = p.Rotor(corner);
            if (oneBlade > 0) r.Blade(0).Trim(1 - oneBlade / r.Radius);
            if (otherBlade > 0) r.Blade(1).Trim(1 - otherBlade / r.Radius);
            p.Rebuild(corner);
        };

        /// <summary>Flies the hover with a damage set before arming (null for none), analysing rotor `analyse`'s 1/rev and, with
        /// spectrum, the band's spectrum.</summary>
        public static Result Hover(FlightSetup setup, Action<Propulsion> damage = null, int analyse = 0, bool spectrum = false) =>
            Hover(setup, out _, damage, analyse, spectrum);

        public static Result Hover(FlightSetup setup, out FlightCore core, Action<Propulsion> damage = null, int analyse = 0, bool spectrum = false)
        {
            core = new FlightCore(setup);
            damage?.Invoke(core.Propulsion);
            CrosswindScenario.Arm(core);
            var pilot = new SimPilot(core.Controller, setup.StartPosition);
            int steps = (int)(Seconds * 1000), from = (int)(MeasuredFrom * 1000), n = steps - from;
            var r = new Result();
            double band = 0, rollLo = double.MaxValue, rollHi = double.MinValue, pitchLo = double.MaxValue, pitchHi = double.MinValue, amps = 0, turn = 0;
            var gyroRe = new double[3];
            var gyroIm = new double[3];
            var filtRe = new double[3];
            var filtIm = new double[3];
            var mid = spectrum ? new Double3[n] : null;
            double lastAngle = 0;
            for (int k = 0; k < steps; k++)
            {
                core.Advance(pilot.Sticks(core, core.State.Step * FlightCore.StepUs));
                ref readonly FlightState s = ref core.State;
                if (k < from)
                {
                    lastAngle = s.RotorAngle[analyse];
                    continue;
                }
                double y = core.Vibration.Last.Y;
                band += y * y;
                for (int i = 0; i < 4; i++)
                {
                    r.Speed[i] += s.RotorSpeed[i] / n;
                    r.Command[i] += s.EscCommand[i] / n;
                }
                TrackerAngles.Of(s.Attitude, out double roll, out double pitch, out _);
                rollLo = Math.Min(rollLo, roll);
                rollHi = Math.Max(rollHi, roll);
                pitchLo = Math.Min(pitchLo, pitch);
                pitchHi = Math.Max(pitchHi, pitch);
                amps += core.Trace.PackAmps0 + core.Trace.PackAmps1;

                // The analysed rotor's 1/rev: each axis correlated with its own angle (e^{−iψ}).
                double psi = s.RotorAngle[analyse], step = psi - lastAngle;
                if (step < 0) step += TwoPi;
                turn += step;
                lastAngle = psi;
                double c = Math.Cos(psi), sn = Math.Sin(psi);
                Axes3 raw = FlightCore.Gyro(s.BodyRate), filtered = s.Fc.GyroFiltered;
                for (int a = 0; a < 3; a++)
                {
                    gyroRe[a] += raw[a] * c;
                    gyroIm[a] -= raw[a] * sn;
                    filtRe[a] += filtered[a] * c;
                    filtIm[a] -= filtered[a] * sn;
                }
                if (spectrum) mid[k - from] = core.Vibration.LastMid;
            }
            r.Band = Math.Sqrt(band / n);
            r.RollPeakToPeak = (rollHi - rollLo) * RadToDeg;
            r.PitchPeakToPeak = (pitchHi - pitchLo) * RadToDeg;
            r.Amps = amps / n;
            r.RotorHz = turn / TwoPi / (n * FlightCore.Dt);
            double g2 = 0, f2 = 0;
            for (int a = 0; a < 3; a++)
            {
                g2 += (gyroRe[a] * gyroRe[a] + gyroIm[a] * gyroIm[a]) * 4 / ((double)n * n);
                f2 += (filtRe[a] * filtRe[a] + filtIm[a] * filtIm[a]) * 4 / ((double)n * n);
            }
            r.GyroOneRev = Math.Sqrt(g2);
            r.FilteredOneRev = Math.Sqrt(f2);
            if (spectrum) r.PeakHz = Peak(mid, 50, 200, 0.1);
            return r;
        }

        // The frequency (Hz) of the largest power of a 1 kHz signal's three axes summed, on a grid from lo to hi.
        static double Peak(Double3[] x, double lo, double hi, double step)
        {
            double best = 0, at = lo;
            for (double f = lo; f <= hi; f += step)
            {
                double w = TwoPi * f * FlightCore.Dt, power = 0;
                for (int a = 0; a < 3; a++)
                {
                    double re = 0, im = 0;
                    for (int k = 0; k < x.Length; k++)
                    {
                        double v = a == 0 ? x[k].X : a == 1 ? x[k].Y : x[k].Z;
                        re += v * Math.Cos(w * k);
                        im -= v * Math.Sin(w * k);
                    }
                    power += re * re + im * im;
                }
                if (power > best)
                {
                    best = power;
                    at = f;
                }
            }
            return at;
        }
    }
}
