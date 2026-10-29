using System;
using System.Globalization;
using System.Numerics;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // propulsion, "A Betaflight 4.5 flight controller in acro", task 1.4b: the builder's gyro and D-term filter chain
    // and battery sag compensation as Betaflight 4.5.0 computes them. Each filter's response is measured on a sine and
    // checked against the transfer function of Betaflight's coefficients (common/filter.c biquadFilterUpdate), written
    // out here in System.Math independently of Biquad's code; the chain's against the product of its sections.
    public sealed class FilterChainTests
    {
        static readonly bool[] PropsIn = { true, false, false, true };
        const double LoopUs = 250, GyroUs = 125;

        static string F(double x, string format = "0.000") => x.ToString(format, CultureInfo.InvariantCulture);

        // Betaflight's biquad coefficients (filter.c 157–202) in System.Math: b0, b1, b2, a1, a2 over a0.
        static double[] Coefficients(double hz, double refreshUs, double q, bool notch)
        {
            double omega = 2 * Math.PI * hz * refreshUs * 1e-6, sn = Math.Sin(omega), cs = Math.Cos(omega), alpha = sn / (2 * q);
            double b0 = notch ? 1 : (1 - cs) / 2, b1 = notch ? -2 * cs : 1 - cs, b2 = b0, a0 = 1 + alpha;
            return new[] { b0 / a0, b1 / a0, b2 / a0, -2 * cs / a0, (1 - alpha) / a0 };
        }

        static double[] Lpf(double hz, double refreshUs) => Coefficients(hz, refreshUs, 1 / Math.Sqrt(2), false);

        static double[] Notch(double hz, double cutoff, double refreshUs) => Coefficients(hz, refreshUs, hz * cutoff / (hz * hz - cutoff * cutoff), true);

        // H(e^{jωT}) of a section, cross-faded by a weight as biquadFilterApplyDF1Weighted does.
        static Complex Response(double[] c, double hz, double refreshUs, double weight = 1)
        {
            Complex z1 = Complex.Exp(new Complex(0, -2 * Math.PI * hz * refreshUs * 1e-6)), z2 = z1 * z1;
            Complex h = (c[0] + c[1] * z1 + c[2] * z2) / (1 + c[3] * z1 + c[4] * z2);
            return weight * h + (1 - weight);
        }

        // The steady response to a sine of the given frequency: the least-squares fit of the output on sin and cos over
        // the last second of three, as gain and phase.
        static Complex Measure(Func<double, double> filter, double hz, double refreshUs)
        {
            int perSecond = (int)(1e6 / refreshUs), n = 3 * perSecond;
            double ss = 0, cc = 0, sc = 0, ys = 0, yc = 0;
            for (int i = 0; i < n; i++)
            {
                double phase = 2 * Math.PI * hz * i * refreshUs * 1e-6, s = Math.Sin(phase), c = Math.Cos(phase);
                double y = filter(s);
                if (i < n - perSecond) continue;
                ss += s * s;
                cc += c * c;
                sc += s * c;
                ys += y * s;
                yc += y * c;
            }
            double det = ss * cc - sc * sc, a = (ys * cc - yc * sc) / det, b = (yc * ss - ys * sc) / det;
            // y = a·sin + b·cos = Im[(a + j·b)·e^{jφ}]: the response to sin is a + j·b.
            return new Complex(a, b);
        }

        static string Db(Complex h) => F(20 * Math.Log10(h.Magnitude), "0.00") + " dB, " + F(h.Phase * 180 / Math.PI, "0.0") + "°";

        static void Check(string name, Func<double, double> filter, double[] reference, double refreshUs, double hz, double weight = 1)
        {
            Complex measured = Measure(filter, hz, refreshUs), expected = Response(reference, hz, refreshUs, weight);
            TestContext.Out.WriteLine($"{name} at {F(hz, "0.0")} Hz: {Db(measured)} (Betaflight's formula {Db(expected)})");
            Assert.That((measured - expected).Magnitude, Is.LessThan(1e-6), name + " at " + hz + " Hz");
        }

        static void SameCoefficients(in Biquad f, double[] c, string name)
        {
            double[] mine = { f.B0, f.B1, f.B2, f.A1, f.A2 };
            for (int i = 0; i < 5; i++) Assert.That(mine[i], Is.EqualTo(c[i]).Within(1e-12), name + " coefficient " + i);
        }

        // ---- each filter of the builder's chain against Betaflight's formula, at three frequencies ----

        [Test]
        public void EachFilterMatchesBetaflightsFormula()
        {
            FcSettings s = FcSettings.Default;
            var fc = new FlightController(s, PropsIn);
            FilterBank bank = fc.Filters;
            var state = new FcState();
            fc.Reset(ref state, default);

            // The gyro's LPF2: a static biquad at the gyro's 8 kHz, in DF1 (gyro_init.c 186–192 with USE_DYN_LPF).
            double[] lpf2 = Lpf(s.gyro_lpf2_static_hz, GyroUs);
            SameCoefficients(bank.GyroLpf2, lpf2, "gyro LPF2");
            foreach (double hz in new[] { 10, s.gyro_lpf2_static_hz, 2 * s.gyro_lpf2_static_hz })
            {
                Biquad f = bank.GyroLpf2;
                Check("gyro LPF2 (biquad 90 Hz, 8 kHz)", x => f.ApplyDF1(x), lpf2, GyroUs, hz);
            }
            Assert.That(Response(lpf2, s.gyro_lpf2_static_hz, GyroUs).Magnitude, Is.EqualTo(1 / Math.Sqrt(2)).Within(1e-9), "−3 dB at the cutoff");

            // Notch 1: static, DF2 (gyro_init.c 111–124), Q from its centre and lower cutoff.
            double[] n1 = Notch(s.gyro_notch1_hz, s.gyro_notch1_cutoff, LoopUs);
            SameCoefficients(bank.GyroNotch1, n1, "gyro notch 1");
            foreach (double hz in new[] { 10, s.gyro_notch1_cutoff, s.gyro_notch1_hz })
            {
                Biquad f = bank.GyroNotch1;
                Check("gyro notch (50 Hz, cutoff 35)", x => f.ApplyDF2(x), n1, LoopUs, hz);
            }
            Assert.That(Response(n1, s.gyro_notch1_hz, LoopUs).Magnitude, Is.LessThan(1e-9), "the notch's zero at its centre");

            // LPF1: dynamic, DF1, at the throttle's hundredth: idle, hover (0.45) and full; at hover, three frequencies.
            foreach (int q in new[] { 0, 45, 100 })
            {
                double cutoff = FilterBank.DynLpfCutoff(q / 100.0, s.gyro_lpf1_dyn_min_hz, s.gyro_lpf1_dyn_max_hz, s.gyro_lpf1_dyn_expo);
                double[] c = Lpf(cutoff, LoopUs);
                SameCoefficients(bank.GyroLpf1At(q), c, "gyro LPF1 at " + q);
                if (q != 45) continue;
                foreach (double hz in new[] { 10, cutoff, 2 * cutoff })
                {
                    Biquad f = bank.GyroLpf1At(q);
                    Check($"gyro LPF1 at throttle {q} % ({F(cutoff, "0.0")} Hz)", x => f.ApplyDF1(x), c, LoopUs, hz);
                }
            }
            Assert.That(FilterBank.DynLpfCutoff(0, 35, 90, 5), Is.EqualTo(35));
            Assert.That(FilterBank.DynLpfCutoff(1, 35, 90, 5), Is.EqualTo(90));
            Assert.That(FilterBank.DynLpfCutoff(0.5, 35, 90, 5), Is.EqualTo(55 * (0.25 * 0.5 + 0.5) + 35).Within(1e-12), "dynLpfCutoffFreq's expo curve");

            // The dynamic notches at their starting centres, DF1, Q 5.
            double[] centres = { 60 + 0.5 * 140 / 3, 60 + 1.5 * 140 / 3, 60 + 2.5 * 140 / 3 };
            Assert.That(bank.NotchCount, Is.EqualTo(3));
            for (int p = 0; p < 3; p++)
            {
                Assert.That(FilterBank.NotchHz(ref state, p).Roll, Is.EqualTo(centres[p]).Within(1e-9), "dynNotchInit's even spread");
                double[] c = Coefficients(centres[p], LoopUs, s.dyn_notch_q / 100, true);
                SameCoefficients(bank.DynNotch(0, p), c, "dynamic notch " + p);
                foreach (double hz in new[] { 10, centres[p] * 0.9, centres[p] })
                {
                    Biquad f = bank.DynNotch(0, p);
                    Check($"dynamic notch {p + 1} ({F(centres[p], "0.0")} Hz, Q 5)", x => f.ApplyDF1(x), c, LoopUs, hz);
                }
            }

            // The D term's notch (DF2), LPF1 (dynamic, DF1) and LPF2 (static, DF2).
            double[] dn = Notch(s.dterm_notch_hz, s.dterm_notch_cutoff, LoopUs);
            SameCoefficients(bank.DtermNotch, dn, "D-term notch");
            foreach (double hz in new[] { 10, s.dterm_notch_cutoff, s.dterm_notch_hz })
            {
                Biquad f = bank.DtermNotch;
                Check("D-term notch (60 Hz, cutoff 45)", x => f.ApplyDF2(x), dn, LoopUs, hz);
            }
            double dCutoff = FilterBank.DynLpfCutoff(0.45, s.dterm_lpf1_dyn_min_hz, s.dterm_lpf1_dyn_max_hz, s.dterm_lpf1_dyn_expo);
            double[] dl1 = Lpf(dCutoff, LoopUs);
            SameCoefficients(bank.DtermLpf1At(45), dl1, "D-term LPF1");
            foreach (double hz in new[] { 10, dCutoff, 2 * dCutoff })
            {
                Biquad f = bank.DtermLpf1At(45);
                Check($"D-term LPF1 at throttle 45 % ({F(dCutoff, "0.0")} Hz)", x => f.ApplyDF1(x), dl1, LoopUs, hz);
            }
            double[] dl2 = Lpf(s.dterm_lpf2_static_hz, LoopUs);
            SameCoefficients(bank.DtermLpf2, dl2, "D-term LPF2");
            foreach (double hz in new[] { 10, s.dterm_lpf2_static_hz, 2 * s.dterm_lpf2_static_hz })
            {
                Biquad f = bank.DtermLpf2;
                Check("D-term LPF2 (biquad 80 Hz)", x => f.ApplyDF2(x), dl2, LoopUs, hz);
            }

            // An RPM notch on a motor at 6000 rpm (100 Hz): its harmonics at 100, 200, 300 Hz, Q 5, the fundamental
            // faded by its margin over rpm_filter_min_hz (rpm_filter.c 135–146), weighted DF1.
            Corners spin = default;
            for (int m = 0; m < 4; m++) spin[m] = 100 * 2 * Math.PI;
            for (int k = 0; k < 400; k++)
            {
                bank.FilterGyro(ref state, default, default, spin);
                bank.RpmUpdate();
            }
            Assert.That(bank.MotorHz(0), Is.EqualTo(100).Within(1e-9), "the motor speed's PT1 settled");
            for (int h = 0; h < 3; h++)
            {
                double hz = 100 * (h + 1), weight = Math.Min(1, (hz - s.rpm_filter_min_hz) / s.rpm_filter_fade_range_hz);
                double[] c = Coefficients(hz, LoopUs, s.rpm_filter_q / 100, true);
                Biquad notch = bank.RpmNotch(1, h, 2);
                SameCoefficients(notch, c, "RPM notch " + h);
                Assert.That(notch.Weight, Is.EqualTo(weight).Within(1e-12));
                foreach (double f in new[] { 10, hz * 0.9, hz })
                {
                    Biquad n = notch;
                    Check($"RPM notch, harmonic {h + 1} ({F(hz, "0")} Hz, weight {F(weight, "0.00")})", x => n.ApplyDF1Weighted(x), c, LoopUs, f, weight);
                }
            }
        }

        // ---- the chain in Betaflight's order, against the product of its sections ----

        // The chain's 8 kHz input from the plant's 4 kHz ramp: the midpoint (x[n−1] + x[n])/2, then x[n]. For
        // x = e^{jωnT} that is e^{jωmT/2}·((1 + c)/2 + (1 − c)/2·(−1)^m), c = cos(ωT/2), so LPF2's output at the loop
        // is H(ω)·(1 + c)/2 + H(ω + 8 kHz·π)·(1 − c)/2 with H at the gyro's rate.
        static Complex Lpf2AtTheLoop(double[] lpf2, double hz)
        {
            double c = Math.Cos(Math.PI * hz * LoopUs * 1e-6);
            return Response(lpf2, hz, GyroUs) * ((1 + c) / 2) + Response(lpf2, hz + 4000, GyroUs) * ((1 - c) / 2);
        }

        [Test]
        public void TheChainIsTheBuildersInBetaflightsOrder()
        {
            FcSettings s = FcSettings.Default;
            const int quantum = 45;
            const double motorHz = 100;
            double gyroCutoff = FilterBank.DynLpfCutoff(quantum / 100.0, s.gyro_lpf1_dyn_min_hz, s.gyro_lpf1_dyn_max_hz, s.gyro_lpf1_dyn_expo);
            double dCutoff = FilterBank.DynLpfCutoff(quantum / 100.0, s.dterm_lpf1_dyn_min_hz, s.dterm_lpf1_dyn_max_hz, s.dterm_lpf1_dyn_expo);
            Complex Gyro(double hz, double[] centres)
            {
                Complex h = Lpf2AtTheLoop(Lpf(s.gyro_lpf2_static_hz, GyroUs), hz);
                for (int k = 1; k <= 3; k++)
                {
                    double at = k * motorHz, weight = Math.Min(1, (at - s.rpm_filter_min_hz) / s.rpm_filter_fade_range_hz);
                    Complex one = Response(Coefficients(at, LoopUs, s.rpm_filter_q / 100, true), hz, LoopUs, weight);
                    h *= one * one * one * one; // four motors alike
                }
                h *= Response(Notch(s.gyro_notch1_hz, s.gyro_notch1_cutoff, LoopUs), hz, LoopUs);
                h *= Response(Lpf(gyroCutoff, LoopUs), hz, LoopUs);
                foreach (double c in centres) h *= Response(Coefficients(c, LoopUs, s.dyn_notch_q / 100, true), hz, LoopUs);
                return h;
            }
            Complex Dterm(double hz) => Response(Notch(s.dterm_notch_hz, s.dterm_notch_cutoff, LoopUs), hz, LoopUs)
                * Response(Lpf(dCutoff, LoopUs), hz, LoopUs) * Response(Lpf(s.dterm_lpf2_static_hz, LoopUs), hz, LoopUs);

            // Three seconds of the sine on every axis in the loop's order (the gyro's chain, the D term's, the RPM
            // notches' update), the steady response fitted over the last second; the dynamic notches' centres at the end.
            void Run(FcSettings settings, double hz, out Complex g, out Complex dt, double[] centres)
            {
                var fc = new FlightController(settings, PropsIn);
                FilterBank bank = fc.Filters;
                var state = new FcState();
                fc.Reset(ref state, default);
                Corners spin = default;
                for (int m = 0; m < 4; m++) spin[m] = motorHz * 2 * Math.PI;
                for (int k = 0; k < 40; k++) bank.UpdateDynLpf(ref state, quantum / 100.0);
                Assert.That(state.DynLpfThrottle, Is.EqualTo(quantum));
                int n = 12000, last = 4000;
                var gyro = new double[n];
                var dterm = new double[n];
                var previous = new Axes3();
                for (int i = 0; i < n; i++)
                {
                    double x = Math.Sin(2 * Math.PI * hz * i * LoopUs * 1e-6);
                    var now = new Axes3 { Roll = x, Pitch = x, Yaw = x };
                    bank.FilterGyro(ref state, previous, now, spin);
                    previous = now;
                    bank.FilterDterm(state, out Axes3 d);
                    bank.RpmUpdate();
                    gyro[i] = state.GyroFiltered.Roll;
                    dterm[i] = d.Roll;
                }
                for (int p = 0; p < centres.Length; p++) centres[p] = FilterBank.NotchHz(ref state, p).Roll;
                g = Fit(gyro, hz, n - last);
                dt = Fit(dterm, hz, n - last);
            }

            // Exactly the sections' product with the dynamic notch off; with it on, its notches move: Betaflight's
            // sliding DFT is pushed in batches of bins over sampleCount loops (sdftPushBatch) and windowed between them,
            // so a strong clean signal shows ripples between the batches as peaks (on a real gyro, noise and vibration
            // set the floor). That run is printed, its notches where they ended.
            FcSettings noDyn = s;
            noDyn.dyn_notch_count = 0;
            foreach (double hz in new[] { 10.0, 25.0, 45.0 })
            {
                Run(noDyn, hz, out Complex g, out Complex dt, new double[0]);
                Complex expected = Gyro(hz, new double[0]), expectedD = expected * Dterm(hz);
                Assert.That((g - expected).Magnitude, Is.LessThan(1e-6), "the gyro chain at " + hz + " Hz");
                Assert.That((dt - expectedD).Magnitude, Is.LessThan(1e-6), "the D term's chain at " + hz + " Hz");
                var centres = new double[3];
                Run(s, hz, out Complex gAll, out Complex dAll, centres);
                // The phase as a delay, read where it has not wrapped yet (10 Hz); above, the phase says it.
                string Delay(Complex h) => hz <= 10 ? F(-h.Phase / (2 * Math.PI * hz) * 1000, "0.00") : "(phase " + F(h.Phase * 180 / Math.PI, "0.0") + "°, wrapped)";
                TestContext.Out.WriteLine($"the builder's gyro chain at {F(hz, "0")} Hz (throttle {quantum} %, motors at {F(motorHz, "0")} Hz): without the dynamic "
                    + $"notch {Db(g)}, a {Delay(g)} ms delay (the sections' product {Db(expected)}), the D term's {Db(dt)}, {Delay(dt)} ms; "
                    + $"with it (its notches at {F(centres[0], "0.0")}, {F(centres[1], "0.0")}, {F(centres[2], "0.0")} Hz) {Db(gAll)}, {Delay(gAll)} ms, "
                    + $"the D term's {Delay(dAll)} ms");
            }
        }

        // The response to sin(2π·hz·i·T) from sample `from` on: the least-squares fit on sin and cos, as a + j·b.
        static Complex Fit(double[] y, double hz, int from)
        {
            double ss = 0, cc = 0, sc = 0, ys = 0, yc = 0;
            for (int i = from; i < y.Length; i++)
            {
                double phase = 2 * Math.PI * hz * i * LoopUs * 1e-6, s = Math.Sin(phase), c = Math.Cos(phase);
                ss += s * s;
                cc += c * c;
                sc += s * c;
                ys += y[i] * s;
                yc += y[i] * c;
            }
            double det = ss * cc - sc * sc;
            return new Complex((ys * cc - yc * sc) / det, (yc * ss - ys * sc) / det);
        }

        // ---- the dynamic notch tracks a peak (dyn_notch_filter.c) ----

        [Test]
        public void TheDynamicNotchFindsAPeak()
        {
            FcSettings s = FcSettings.Default;
            s.dshot_bidir = false; // no RPM notches in the way
            var fc = new FlightController(s, PropsIn);
            FilterBank bank = fc.Filters;
            var state = new FcState();
            fc.Reset(ref state, default);
            Assert.That((bank.StartBin, bank.EndBin), Is.EqualTo((11, 35)), "60–200 Hz on a 400 Hz sliding DFT of 72 samples: 5.56 Hz a bin");
            var previous = new Axes3();
            int loops = 4000;
            for (int k = 0; k < loops; k++)
            {
                double t = k * LoopUs * 1e-6;
                // A 10 Hz manoeuvre and, after half a second, a 140 Hz vibration on roll; pitch and yaw still.
                double roll = 50 * Math.Sin(2 * Math.PI * 10 * t) + (t >= 0.5 ? 5 * Math.Sin(2 * Math.PI * 140 * t) : 0);
                var now = new Axes3 { Roll = roll };
                bank.FilterGyro(ref state, previous, now, default);
                previous = now;
            }
            double nearest = double.MaxValue;
            for (int p = 0; p < 3; p++) nearest = Math.Min(nearest, Math.Abs(FilterBank.NotchHz(ref state, p).Roll - 140));
            TestContext.Out.WriteLine($"after 0.5 s of 140 Hz on roll: roll's notches at {F(state.DynNotch1.Roll, "0.0")}, {F(state.DynNotch2.Roll, "0.0")}, "
                + $"{F(state.DynNotch3.Roll, "0.0")} Hz; pitch's at {F(state.DynNotch1.Pitch, "0.0")}, {F(state.DynNotch2.Pitch, "0.0")}, {F(state.DynNotch3.Pitch, "0.0")} Hz");
            Assert.That(nearest, Is.LessThan(3), "a notch sits on the vibration");
            Assert.That((state.DynNotch1.Pitch, state.DynNotch2.Pitch, state.DynNotch3.Yaw), Is.EqualTo((60 + 0.5 * 140 / 3, 130.0, 60 + 2.5 * 140 / 3)),
                "an axis with nothing in its spectrum keeps its notches");
        }

        // ---- battery sag compensation (mixer.c 245–262, battery.c, voltage.c) ----

        [Test]
        public void SagCompensationIsBetaflightsFormula()
        {
            FcSettings s = FcSettings.Default;
            s.vbat_sag_compensation = 100;
            var fc = new FlightController(s, PropsIn);
            var state = new FcState();
            fc.Reset(ref state, default);
            Assert.That(fc.SagAttenuation(state), Is.EqualTo(0), "before the battery is found");
            fc.Battery(ref state, 25.2);
            Assert.That(state.Cells, Is.EqualTo(6), "2520 cV / 430 + 1");
            // (420 − 300)/420 × goodness × 1: goodness 1 at 4.20 V, 0.5 at 3.60 V, 0 at the 3.00 V warning.
            Assert.That(fc.SagAttenuation(state), Is.EqualTo(120.0 / 420).Within(1e-12));
            state.SagVolts = 6 * 3.6;
            Assert.That(fc.SagAttenuation(state), Is.EqualTo(120.0 / 420 * 0.5).Within(1e-12));
            state.SagVolts = 6 * 3.0;
            Assert.That(fc.SagAttenuation(state), Is.EqualTo(0).Within(1e-12));
            state.SagVolts = 6 * 2.9;
            Assert.That(fc.SagAttenuation(state), Is.EqualTo(0));
            state.SagVolts = 6 * 3.6049;
            Assert.That(fc.SagAttenuation(state), Is.EqualTo(120.0 / 420 * 60 / 120).Within(1e-12), "the cell voltage in whole cV");

            // The sag filter: a PT1 at 5 Hz (vbat_sag_lpf_period 2) at the 200 Hz battery task.
            state.SagVolts = 25.2;
            for (int k = 0; k < 40; k++) fc.Battery(ref state, 23.2);
            double k5 = 2 * Math.PI * 5 * 0.005 / (2 * Math.PI * 5 * 0.005 + 1);
            Assert.That(state.SagVolts, Is.EqualTo(23.2 + 2 * Math.Pow(1 - k5, 40)).Within(1e-9));

            // Betaflight's count from the first reading: a Li-ion 6S at 3.5 V a cell counts 5 (2100 / 430 + 1), which
            // is why a Li-ion pilot sets force_battery_cell_count; forced, 6.
            fc.Reset(ref state, default);
            fc.Battery(ref state, 21.0);
            Assert.That(state.Cells, Is.EqualTo(5));
            s.force_battery_cell_count = 6;
            fc = new FlightController(s, PropsIn);
            fc.Reset(ref state, default);
            fc.Battery(ref state, 21.0);
            Assert.That(state.Cells, Is.EqualTo(6));

            // Off, the battery task does not run and nothing is taken.
            fc = new FlightController(FcSettings.Default, PropsIn);
            fc.Reset(ref state, default);
            fc.Battery(ref state, 25.2);
            Assert.That((state.Cells, fc.SagAttenuation(state)), Is.EqualTo(((byte)0, 0.0)));
            Assert.That(FlightController.IsBatteryLoop(1) && FlightController.IsBatteryLoop(21) && !FlightController.IsBatteryLoop(20), Is.True,
                "200 Hz: every 20th loop, at a step's second");
        }
    }
}
