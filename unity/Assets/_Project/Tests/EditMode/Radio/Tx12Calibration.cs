using System;
using Fpv450.Input.Radio;
using Fpv450.Sim.Control;

namespace Fpv450.Tests.Radio
{
    /// <summary>
    /// A TX12 on EdgeTX 2.8 in USB joystick mode, as the calibration sees it: each stick's gimbal reading (an 11-bit
    /// ADC, 1024 at rest) goes through the radio's own calibration, EdgeTX's `(v − mid) · 1024 / max(100, span)`
    /// clamped to ±1024 (mixer.cpp evalInputs), then the input's expo and weight, the channel's reversal, and out as
    /// the channel + 1024 clipped to 0–2047 (Tx12Stream). A healthy radio's calibration spans equal its gimbals'
    /// travel; fc-profiles' healthy spans are 683–903 a side. Synthetic, not a recording.
    /// </summary>
    sealed class Tx12Radio
    {
        public const int Roll = 0, Pitch = 1, Throttle = 2, Yaw = 3;

        public static readonly RadioAxis[] Axes = Tx12Stream.Axes;

        /// <summary>Each stick's channel (0-based): AETR.</summary>
        public int[] Channel = { 0, 1, 2, 3 };

        /// <summary>Each gimbal's ADC travel from rest to each end, inside fc-profiles' healthy 683–903.</summary>
        public int[] TravelNeg = { 811, 742, 683, 868 }, TravelPos = { 794, 903, 720, 851 };

        /// <summary>The radio's own calibration spans; null: the gimbals' travel (a healthy calibration).</summary>
        public int[] SpanNeg, SpanPos;

        /// <summary>EdgeTX expo (%) and weight (%) on each stick's input, and the channel's reversal.</summary>
        public int[] Expo = { 0, 0, 0, 0 }, Weight = { 100, 100, 100, 100 };

        public bool[] Reverse = new bool[4];

        /// <summary>A stick whose output spans only this many HID counts about the centre (0: the radio's pipeline).</summary>
        public int[] HidSpan = { 0, 0, 0, 0 };

        /// <summary>Each report's ADC jitter, uniform from NoiseLo to NoiseHi counts (0 and 0: an ADC filtered still).</summary>
        public int NoiseLo = -1, NoiseHi = 1;

        /// <summary>The arm switch's channel (0-based, an axis), or −1 with ArmButton set.</summary>
        public int ArmChannel = 4, ArmButton = -1;

        /// <summary>The pilot's hands: roll, pitch and yaw −1 to +1 (right, forward), the throttle 0 to 1 (up).</summary>
        public readonly double[] Stick = new double[4];

        public bool Arm;

        public void Fill(long t, RadioReport r)
        {
            var ch = new int[8];
            ch[5] = -1024;
            ch[6] = 0;
            ch[7] = 1024;
            ch[4] = -1024;
            for (int f = 0; f < 4; f++)
            {
                double p = f == Throttle ? 2 * Stick[f] - 1 : Stick[f];
                int noise = NoiseLo + (int)(Hash((ulong)t * 8 + (ulong)f) % (ulong)(NoiseHi - NoiseLo + 1)); // ADC jitter
                int adc = 1024 + (int)Math.Round(p >= 0 ? p * TravelPos[f] : p * TravelNeg[f]) + noise;
                int v = adc - 1024;
                int span = v > 0 ? (SpanPos ?? TravelPos)[f] : (SpanNeg ?? TravelNeg)[f];
                v = (int)((long)v * 1024 / Math.Max(100, span));
                v = Math.Max(-1024, Math.Min(1024, v));
                if (Expo[f] != 0)
                {
                    double x = v / 1024.0;
                    v = (int)Math.Round((Expo[f] * x * x * x + (100 - Expo[f]) * x) / 100 * 1024);
                }
                v = v * Weight[f] / 100;
                if (Reverse[f]) v = -v;
                if (HidSpan[f] > 0) v = (int)Math.Round(p * (p >= 0 ? HidSpan[f] - HidSpan[f] / 2 : HidSpan[f] / 2)) + noise;
                ch[Channel[f]] = v;
            }
            r.Buttons = 0;
            if (ArmChannel >= 0) ch[ArmChannel] = Arm ? 1024 : -1024;
            else if (Arm) r.Buttons = 1UL << ArmButton;
            r.HostUs = t;
            for (int i = 0; i < 8; i++) r.Axes[i] = Math.Max(0, Math.Min(2047, ch[i] + 1024));
        }

        internal static ulong Hash(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }

    /// <summary>
    /// A pilot following the wizard's prompts with a radio alone: 0.8 s to react to each prompt, a stick to its start
    /// end in 0.3 s and held 0.5 s, then swept steadily to the far end over SweepUs and held; the first sweep of a
    /// stick takes FirstSweepUs (a hasty pilot), and each repeat after the second is 2.5 times slower than the last
    /// (QA's pilot). A stick springs back to centre when its step ends. With Wobble, a thumb
    /// left on roll, pitch and yaw at the centre step moves them by up to that fraction of their half travel.
    /// </summary>
    sealed class CalibratingPilot
    {
        public const long ReactUs = 800000, ToStartUs = 300000, HoldStartUs = 500000;

        public long SweepUs = 2000000, FirstSweepUs = 2000000;

        public double Wobble;

        /// <summary>The prompt said "Too fast" at least once.</summary>
        public bool SawTooFast;

        readonly Tx12Radio _radio;
        CalibrationStep _step = CalibrationStep.Centre;
        int _repeats;
        long _since;
        double _from;
        int _attempt;

        public CalibratingPilot(Tx12Radio radio) => _radio = radio;

        public void Act(long t, CalibrationWizard wizard)
        {
            if (wizard.Step != _step || wizard.Repeats > _repeats)
            {
                if (wizard.Step != _step)
                {
                    int previous = Function(_step);
                    if (previous >= 0 && previous != Tx12Radio.Throttle) _radio.Stick[previous] = 0; // let go
                    if (_step == CalibrationStep.Centre) _radio.Stick[Tx12Radio.Roll] = _radio.Stick[Tx12Radio.Pitch] = _radio.Stick[Tx12Radio.Yaw] = 0;
                    _attempt = 0;
                }
                _step = wizard.Step;
                _since = t;
                int f = Function(_step);
                _from = f >= 0 ? _radio.Stick[f] : 0;
                _attempt++;
            }
            _repeats = wizard.Repeats;
            SawTooFast |= wizard.TooFast;

            long tau = t - _since - ReactUs;
            switch (_step)
            {
                case CalibrationStep.ArmOn:
                    if (tau >= 0) _radio.Stick[Tx12Radio.Throttle] = 0;
                    if (tau >= 500000) _radio.Arm = true;
                    return;
                case CalibrationStep.ArmOff:
                    if (tau >= 0) _radio.Arm = false;
                    return;
                case CalibrationStep.Centre:
                    if (Wobble != 0)
                        foreach (int f in new[] { Tx12Radio.Roll, Tx12Radio.Pitch, Tx12Radio.Yaw })
                            _radio.Stick[f] = Wobble * Math.Sin(2 * Math.PI * (0.7 + 0.13 * f) * t / 1e6);
                    return;
                case CalibrationStep.Done:
                    return;
            }
            int stick = Function(_step);
            double start = stick == Tx12Radio.Throttle ? 0 : -1, sweep = _attempt == 1 ? FirstSweepUs : SweepUs * Math.Pow(2.5, _attempt - 2);
            double p;
            if (tau < 0) p = _from;
            else if (tau < ToStartUs) p = _from + (start - _from) * tau / ToStartUs;
            else if (tau < ToStartUs + HoldStartUs) p = start;
            else if (tau < ToStartUs + HoldStartUs + sweep) p = start + (1 - start) * (tau - ToStartUs - HoldStartUs) / sweep;
            else p = 1;
            _radio.Stick[stick] = p;
        }

        static int Function(CalibrationStep step)
        {
            switch (step)
            {
                case CalibrationStep.Throttle: return Tx12Radio.Throttle;
                case CalibrationStep.Yaw: return Tx12Radio.Yaw;
                case CalibrationStep.Pitch: return Tx12Radio.Pitch;
                case CalibrationStep.Roll: return Tx12Radio.Roll;
                default: return -1;
            }
        }
    }

    static class Calibrations
    {
        /// <summary>
        /// The radio's reports every reportUs (2 ms by default; 20–200 µs of wake jitter) into the wizard, the pilot
        /// acting before each, until the wizard is done or limitUs has passed. The radio's output updates every
        /// updateUs (0: every report); a report between two updates repeats the last output, as a 1 ms USB stream over a
        /// 4 ms radio. Every report is also kept, as a recording.
        /// </summary>
        public static CalibrationWizard Run(Tx12Radio radio, CalibratingPilot pilot, out RecordedRadio recording, long limitUs = 300000000,
            long reportUs = 2000, long updateUs = 0)
        {
            var wizard = new CalibrationWizard("RadioMaster TX12 (synthetic)", InputSource.Radio, Tx12Radio.Axes);
            recording = new RecordedRadio("RadioMaster TX12 (synthetic)", InputSource.Radio, Tx12Radio.Axes, 24);
            var report = new RadioReport(Tx12Radio.Axes.Length);
            long nextUpdate = 0;
            for (long k = 0; wizard.Step != CalibrationStep.Done; k++)
            {
                long tick = k * reportUs, t = 1000000 + tick + 20 + (long)(Tx12Radio.Hash((ulong)k) % 181);
                if (t > limitUs) break;
                pilot.Act(t, wizard);
                if (tick >= nextUpdate)
                {
                    radio.Fill(t, report);
                    nextUpdate += updateUs > 0 ? updateUs : reportUs;
                }
                else report.HostUs = t;
                recording.Add(t, report.Axes, report.Buttons);
                wizard.Feed(report);
            }
            return wizard;
        }
    }
}
