using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Control
{
    /// <summary>Betaflight's lowpass types (CLI gyro_lpf*_type, dterm_lpf*_type).</summary>
    public enum LowpassType : byte { PT1, BIQUAD, PT2, PT3 }

    /// <summary>
    /// Betaflight 4.5.0's gyro and D-term filter chain (design 4, task 1.4b), its formulas re-implemented from the source
    /// (GPL: read, never copied), so the loop carries the filters' real phase lag. Per 4 kHz loop, in Betaflight's order:
    /// - taskFiltering (fc/core.c 1271–1277): the motors' speeds from bidirectional DShot through a PT1 at
    ///   rpm_filter_lpf_hz (drivers/dshot.c 159–166, 330–335); then filterGyro (sensors/gyro_filter_impl.c 23–91): the
    ///   gyro's two 8 kHz samples since the last loop through LPF2, its downsampling filter (sensors/gyro.c 444–455,
    ///   gyro_init.c 263–268; without LPF2, their average), then the RPM notches (flight/rpm_filter.c 170–186), notch 1
    ///   and 2 (gyro_init.c 111–139), LPF1 (gyro_init.c 141–214, 246–261) and the dynamic notches
    ///   (flight/dyn_notch_filter.c 406–413), the dynamic notch's input pushed before them (208–211); then its analysis
    ///   (dynNotchUpdate 216–251, dynNotchProcess 254–404, on common/sdft.c's sliding DFT 39–200)
    /// - pidController (flight/pid.c 862–883): the D term's notch, LPF1 and LPF2 on the filtered gyro (pid_init.c
    ///   69–193), then rpmFilterUpdate (rpm_filter.c 112–168), a few notches each loop
    /// - mixTable (flight/mixer.c 452–468, 654–657): the dynamic LPF1 cutoffs follow the throttle in hundredths, at most
    ///   every 5 ms (gyro.c 603–644, pid.c 1185–1226)
    /// The 8 kHz samples are the plant's gyro ramp (FlightCore): the previous loop's reading and this loop's, and their
    /// midpoint, which is the ramp's value at the half loop.
    ///
    /// State: the outputs (the filtered gyro, the D term's last input), the dynamic notches' centres and the dynamic
    /// LPF's throttle are FcState's and logged every step; the delay lines, the coefficients that follow the motors and
    /// the notches, the motor-speed filters and the sliding DFT's window (about 700 doubles) are this bank's, outside
    /// FlightState: the 1 kHz record cannot carry them within LOG-01's budget, and every effect they have reaches the
    /// logged filtered gyro, which a replay compares bit for bit. Reset gives the bank of a controller at rest (a zero
    /// gyro, the motors stopped). Preallocated at construction; no allocation per loop.
    ///
    /// Departures: double precision; DetMath's sine and cosine for sin_approx and cos_approx; the motors' speeds are the
    /// rotors' own (no eRPM telemetry quantisation or delay); the gyro has no sensor (its noise, its on-chip anti-alias
    /// filter); rpm_filter_weights are Betaflight's default 100, 100, 100; only the BIQUAD lowpass type (the builder's)
    /// is modelled.
    /// </summary>
    public sealed class FilterBank
    {
        const double TwoPi = 6.283185307179586;

        // The builder's 8 kHz gyro and its 4 kHz PID loop (pilot, fc-profiles §1), µs.
        const int GyroSampleUs = 125, LoopUs = 250;
        const int GyroNyquistHz = 1000000 / 2 / GyroSampleUs, LoopNyquistHz = 1000000 / 2 / LoopUs;

        // Published, Betaflight 4.5.0: mixer.c 67–68, rpm_filter.c 46 and 90, sdft.h 32–33, sdft.c 29,
        // dyn_notch_filter.c 86–87 and dyn_notch_filter.h 29.
        const int DynLpfThrottleSteps = 100;
        const int DynLpfUpdateLoops = 5000 / LoopUs;
        const int RpmFilterDurationUs = 1000;
        const double RpmMaxHz = 0.48 * 1e6 / LoopUs;
        const int SdftSampleSize = 72, SdftBinCount = SdftSampleSize / 2;
        const double SdftR = 0.9999;
        const double DynNotchSmoothHz = 4;
        const int DynNotchStepCount = 4, DynNotchCalcTicks = 3 * DynNotchStepCount;
        public const int DynNotchCountMax = 5;
        public const int RpmHarmonicsMax = 3;

        // ---- the configuration ----
        readonly bool _lpf2, _notch1, _notch2, _lpf1, _dynLpf, _dNotch, _dLpf1, _dDynLpf, _dLpf2, _rpm, _dynNotch;
        readonly Biquad[] _lpf1Table = new Biquad[DynLpfThrottleSteps + 1], _dLpf1Table = new Biquad[DynLpfThrottleSteps + 1];
        readonly Biquad _lpf2Init, _notch1Init, _notch2Init, _lpf1Init, _dNotchInit, _dLpf1Init, _dLpf2Init;
        readonly double _kMotorHz, _rpmMinHz, _rpmFadeHz, _rpmQ;
        readonly int _harmonics, _rpmUpdates;
        readonly int _notchCount, _sampleCount, _startBin, _endBin, _batchSize;
        readonly double _notchQ, _notchMinHz, _notchMaxHz, _sampleCountRcp, _resolutionHz, _pt1LooptimeS, _rPowerN;
        readonly double[] _twRe = new double[SdftBinCount], _twIm = new double[SdftBinCount];
        readonly double[] _notchInitHz = new double[DynNotchCountMax];

        // ---- the state (see the summary) ----
        readonly Biquad[] _lpf2s = new Biquad[3], _notch1s = new Biquad[3], _notch2s = new Biquad[3], _lpf1s = new Biquad[3];
        readonly Biquad[] _dNotches = new Biquad[3], _dLpf1s = new Biquad[3], _dLpf2s = new Biquad[3];
        readonly double[] _motorHz = new double[4];
        readonly Biquad[] _rpmNotches = new Biquad[3 * RpmHarmonicsMax * 4]; // [axis][harmonic][motor]
        int _rpmMotor, _rpmHarmonic;
        readonly Biquad[] _dynNotches = new Biquad[3 * DynNotchCountMax]; // [axis][notch]
        readonly double[] _samples = new double[3 * SdftSampleSize];
        readonly double[] _re = new double[3 * SdftBinCount], _im = new double[3 * SdftBinCount];
        readonly int[] _sdftIdx = new int[3];
        readonly double[] _accumulator = new double[3], _average = new double[3];
        int _sampleIndex, _tick, _step, _axis;
        readonly int[] _peakBin = new int[DynNotchCountMax];
        readonly double[] _peakValue = new double[DynNotchCountMax];
        readonly double[] _power = new double[SdftBinCount];
        double _noiseThreshold;

        public FilterBank(in FcSettings s)
        {
            // gyro_init.c gyroInitFilters (246–283): LPF1 at its dynamic minimum when dynamic, else static; LPF2 at the
            // gyro's rate, which makes it the downsampling filter; the notches at their Nyquist-adjusted centres.
            double lpf1Hz = s.gyro_lpf1_dyn_min_hz > 0 ? s.gyro_lpf1_dyn_min_hz : s.gyro_lpf1_static_hz;
            _lpf1 = Lowpass("gyro_lpf1", s.gyro_lpf1_type, lpf1Hz, lpf1Hz <= LoopNyquistHz);
            _dynLpf = s.gyro_lpf1_dyn_min_hz > 0;
            _lpf2 = Lowpass("gyro_lpf2", s.gyro_lpf2_type, s.gyro_lpf2_static_hz, s.gyro_lpf2_static_hz <= GyroNyquistHz);
            if (_lpf1) _lpf1Init = Biquad.Lowpass(lpf1Hz, LoopUs);
            if (_lpf2) _lpf2Init = Biquad.Lowpass(s.gyro_lpf2_static_hz, GyroSampleUs);
            _notch1 = Notch(s.gyro_notch1_hz, s.gyro_notch1_cutoff, out _notch1Init);
            _notch2 = Notch(s.gyro_notch2_hz, s.gyro_notch2_cutoff, out _notch2Init);

            // pid_init.c pidInitFilters (69–193): the D term's notch, LPF1 (its biquad needs the static cutoff below
            // Nyquist, line 123) and LPF2.
            _dNotch = Notch(s.dterm_notch_hz, s.dterm_notch_cutoff, out _dNotchInit);
            double dLpf1Hz = s.dterm_lpf1_dyn_min_hz > 0 ? s.dterm_lpf1_dyn_min_hz : s.dterm_lpf1_static_hz;
            _dLpf1 = Lowpass("dterm_lpf1", s.dterm_lpf1_type, dLpf1Hz, s.dterm_lpf1_static_hz < LoopNyquistHz);
            _dDynLpf = s.dterm_lpf1_dyn_min_hz > 0;
            _dLpf2 = Lowpass("dterm_lpf2", s.dterm_lpf2_type, s.dterm_lpf2_static_hz, s.dterm_lpf2_static_hz < LoopNyquistHz);
            if (_dLpf1) _dLpf1Init = Biquad.Lowpass(dLpf1Hz, LoopUs);
            if (_dLpf2) _dLpf2Init = Biquad.Lowpass(s.dterm_lpf2_static_hz, LoopUs);

            // The dynamic LPFs' coefficients for every throttle hundredth (dynLpfGyroUpdate, dynLpfDTermUpdate).
            for (int q = 0; q <= DynLpfThrottleSteps; q++)
            {
                double throttle = q / (double)DynLpfThrottleSteps;
                _lpf1Table[q] = Biquad.Lowpass(DynLpfCutoff(throttle, s.gyro_lpf1_dyn_min_hz, s.gyro_lpf1_dyn_max_hz, s.gyro_lpf1_dyn_expo), LoopUs);
                _dLpf1Table[q] = Biquad.Lowpass(DynLpfCutoff(throttle, s.dterm_lpf1_dyn_min_hz, s.dterm_lpf1_dyn_max_hz, s.dterm_lpf1_dyn_expo), LoopUs);
            }

            // rpm_filter.c rpmFilterInit (71–110) and dshot.c initDshotTelemetry.
            if (!IsWhole(s.rpm_filter_harmonics, 0, RpmHarmonicsMax))
                throw new ArgumentException("rpm_filter_harmonics is a whole number 0–3");
            _harmonics = (int)s.rpm_filter_harmonics;
            _rpm = s.dshot_bidir && _harmonics > 0;
            _rpmMinHz = s.rpm_filter_min_hz;
            _rpmFadeHz = s.rpm_filter_fade_range_hz;
            _rpmQ = s.rpm_filter_q / 100;
            _kMotorHz = Pt1Gain(s.rpm_filter_lpf_hz, LoopUs * 1e-6);
            // ceil(notches / (1 ms / loop)), in whole µs so that 4 × 3 notches take 3 a loop.
            _rpmUpdates = (4 * _harmonics * LoopUs + RpmFilterDurationUs - 1) / RpmFilterDurationUs;

            // dyn_notch_filter.c dynNotchInit (157–205) and sdft.c sdftInit (39–67).
            if (!IsWhole(s.dyn_notch_count, 0, DynNotchCountMax))
                throw new ArgumentException("dyn_notch_count is a whole number 0–5");
            _notchCount = (int)s.dyn_notch_count;
            _dynNotch = _notchCount > 0;
            _notchQ = s.dyn_notch_q / 100;
            _notchMinHz = s.dyn_notch_min_hz;
            _notchMaxHz = DetMath.Min(DetMath.Max(_notchMinHz, s.dyn_notch_max_hz), LoopNyquistHz);
            _sampleCount = IntMax(1, (int)(LoopNyquistHz / _notchMaxHz));
            _sampleCountRcp = 1.0 / _sampleCount;
            double sdftRateHz = (1000000 / LoopUs) / (double)_sampleCount;
            _resolutionHz = sdftRateHz / SdftSampleSize;
            _startBin = IntMax(1, Lrint(_notchMinHz / _resolutionHz));
            _endBin = IntMin(SdftBinCount - 1, Lrint(_notchMaxHz / _resolutionHz));
            _startBin = IntMin(_startBin, SdftBinCount - 1);
            _endBin = IntMax(_endBin, _startBin);
            _batchSize = (_endBin - _startBin + 1) / _sampleCount;
            _pt1LooptimeS = DynNotchCalcTicks / (double)(1000000 / LoopUs);
            double r = 1;
            for (int i = 0; i < SdftSampleSize; i++) r *= SdftR;
            _rPowerN = r;
            double c = TwoPi / SdftSampleSize;
            for (int i = 0; i < SdftBinCount; i++)
            {
                DetMath.SinCos(c * i, out double sn, out double cs);
                _twRe[i] = SdftR * cs;
                _twIm[i] = SdftR * sn;
            }
            for (int p = 0; p < _notchCount; p++)
                _notchInitHz[p] = (p + 0.5) * (_notchMaxHz - _notchMinHz) / _notchCount + _notchMinHz;
        }

        /// <summary>The gyro's 8 kHz LPF2 is on (the downsampling filter); else the two samples are averaged.</summary>
        public bool Lpf2On => _lpf2;

        /// <summary>The RPM filter is on (bidirectional DShot and at least one harmonic).</summary>
        public bool RpmOn => _rpm;

        /// <summary>The dynamic notch's count per axis (0 = off), its Q, and its sliding DFT's bins and resolution.</summary>
        public int NotchCount => _notchCount;
        public double NotchQ => _notchQ;
        public int StartBin => _startBin;
        public int EndBin => _endBin;
        public double ResolutionHz => _resolutionHz;

        /// <summary>The RPM notches' Q and the lowest and highest notch frequencies, Hz.</summary>
        public double RpmQ => _rpmQ;
        public double RpmMinHz => _rpmMinHz;

        /// <summary>The motors' filtered speeds the RPM notches follow, Hz.</summary>
        public double MotorHz(int motor) => _motorHz[motor];

        /// <summary>The coefficients of the gyro's LPF1 and the D term's LPF1 at a throttle hundredth (0–100).</summary>
        public Biquad GyroLpf1At(int quantum) => _dynLpf ? _lpf1Table[quantum] : _lpf1Init;
        public Biquad DtermLpf1At(int quantum) => _dDynLpf ? _dLpf1Table[quantum] : _dLpf1Init;

        /// <summary>The static sections as built: gyro LPF2 (at the gyro's rate), notch 1 and 2, the D term's notch and LPF2.</summary>
        public Biquad GyroLpf2 => _lpf2Init;
        public Biquad GyroNotch1 => _notch1Init;
        public Biquad GyroNotch2 => _notch2Init;
        public Biquad DtermNotch => _dNotchInit;
        public Biquad DtermLpf2 => _dLpf2Init;

        /// <summary>An RPM notch's section now (axis, harmonic 0-based, motor), and a dynamic notch's (axis, notch).</summary>
        public Biquad RpmNotch(int axis, int harmonic, int motor) => _rpmNotches[(axis * RpmHarmonicsMax + harmonic) * 4 + motor];
        public Biquad DynNotch(int axis, int notch) => _dynNotches[axis * DynNotchCountMax + notch];

        /// <summary>The bank of a controller at rest; FcState's share is set to match.</summary>
        public void Reset(ref FcState s)
        {
            for (int a = 0; a < 3; a++)
            {
                _lpf2s[a] = _lpf2Init;
                _notch1s[a] = _notch1Init;
                _notch2s[a] = _notch2Init;
                _lpf1s[a] = GyroLpf1At(0);
                _dNotches[a] = _dNotchInit;
                _dLpf1s[a] = DtermLpf1At(0);
                _dLpf2s[a] = _dLpf2Init;
                _sdftIdx[a] = 0;
                _accumulator[a] = _average[a] = 0;
            }
            // rpmFilterInit's notches at minHz·i with no weight, then every update at a stopped motor: minHz, no weight.
            for (int m = 0; m < 4; m++) _motorHz[m] = 0;
            for (int i = 0; i < _rpmNotches.Length; i++) _rpmNotches[i] = Biquad.Notch(_rpmMinHz, LoopUs, _rpmQ, 0);
            _rpmMotor = _rpmHarmonic = 0;
            for (int a = 0; a < 3; a++)
                for (int p = 0; p < DynNotchCountMax; p++)
                    _dynNotches[a * DynNotchCountMax + p] = p < _notchCount ? Biquad.Notch(_notchInitHz[p], LoopUs, _notchQ) : default;
            Array.Clear(_samples, 0, _samples.Length);
            Array.Clear(_re, 0, _re.Length);
            Array.Clear(_im, 0, _im.Length);
            Array.Clear(_power, 0, _power.Length);
            Array.Clear(_peakBin, 0, _peakBin.Length);
            Array.Clear(_peakValue, 0, _peakValue.Length);
            _sampleIndex = _tick = _step = _axis = 0;
            _noiseThreshold = 0;

            s.GyroFiltered = default;
            s.DtermGyro = default;
            for (int p = 0; p < DynNotchCountMax; p++) NotchHz(ref s, p) = default;
            for (int p = 0; p < _notchCount; p++)
            {
                ref Axes3 hz = ref NotchHz(ref s, p);
                hz.Roll = hz.Pitch = hz.Yaw = _notchInitHz[p];
            }
            s.DynLpfThrottle = 0;
            s.DynLpfLoops = DynLpfUpdateLoops;
        }

        /// <summary>
        /// taskFiltering: the motors' speeds (rad/s) into their filters, then the gyro: previous and gyro are the last
        /// loop's and this loop's readings, °/s; s.GyroFiltered gets the result.
        /// </summary>
        public void FilterGyro(ref FcState s, in Axes3 previous, in Axes3 gyro, in Corners motorSpeed)
        {
            if (_rpm)
                for (int m = 0; m < 4; m++)
                    _motorHz[m] += _kMotorHz * (motorSpeed[m] / TwoPi - _motorHz[m]);
            for (int a = 0; a < 3; a++)
            {
                double now = gyro[a], half = 0.5 * (previous[a] + now);
                double x;
                if (_lpf2)
                {
                    _lpf2s[a].ApplyDF1(half);
                    x = _lpf2s[a].ApplyDF1(now);
                }
                else x = (half + now) / 2;
                if (_rpm) x = Rpm(a, x);
                if (_notch1) x = _notch1s[a].ApplyDF2(x);
                if (_notch2) x = _notch2s[a].ApplyDF2(x);
                if (_lpf1) x = _lpf1s[a].ApplyDF1(x);
                if (_dynNotch)
                {
                    _accumulator[a] += x;
                    for (int p = 0; p < _notchCount; p++) x = _dynNotches[a * DynNotchCountMax + p].ApplyDF1(x);
                }
                s.GyroFiltered[a] = x;
            }
            if (_dynNotch) DynNotchUpdate(ref s);
        }

        /// <summary>pidController's D-term filters on the filtered gyro: the gyro the D term differentiates, °/s.</summary>
        public void FilterDterm(in FcState s, out Axes3 dterm)
        {
            dterm = default;
            for (int a = 0; a < 3; a++)
            {
                double x = s.GyroFiltered[a];
                if (_dNotch) x = _dNotches[a].ApplyDF2(x);
                if (_dLpf1) x = _dLpf1s[a].ApplyDF1(x);
                if (_dLpf2) x = _dLpf2s[a].ApplyDF2(x);
                dterm[a] = x;
            }
        }

        /// <summary>rpmFilterUpdate: the next few notches follow their motor's filtered speed (all axes alike).</summary>
        public void RpmUpdate()
        {
            if (!_rpm) return;
            for (int i = 0; i < _rpmUpdates; i++)
            {
                double hz = Clamp((_rpmHarmonic + 1) * _motorHz[_rpmMotor], _rpmMinHz, RpmMaxHz);
                double margin = hz - _rpmMinHz, weight = 1;
                if (margin < _rpmFadeHz) weight *= margin / _rpmFadeHz;
                int k = _rpmHarmonic * 4 + _rpmMotor;
                ref Biquad template = ref _rpmNotches[k];
                template.Update(hz, LoopUs, _rpmQ, true, weight);
                for (int a = 1; a < 3; a++) _rpmNotches[a * RpmHarmonicsMax * 4 + k].CopyCoefficients(template);
                _rpmHarmonic = (_rpmHarmonic + 1) % _harmonics;
                if (_rpmHarmonic == 0) _rpmMotor = (_rpmMotor + 1) % 4;
            }
        }

        /// <summary>
        /// mixer.c updateDynLpfCutoffs, with the throttle mixTable has after TPA (0–1): at most every 5 ms, a new
        /// hundredth of throttle moves both LPF1s to their cutoffs for it.
        /// </summary>
        public void UpdateDynLpf(ref FcState s, double throttle)
        {
            if (s.DynLpfLoops >= DynLpfUpdateLoops)
            {
                int quantum = Lrint(throttle * DynLpfThrottleSteps);
                if (quantum != s.DynLpfThrottle)
                {
                    if (_dynLpf)
                        for (int a = 0; a < 3; a++) _lpf1s[a].CopyCoefficients(_lpf1Table[quantum]);
                    if (_dDynLpf)
                        for (int a = 0; a < 3; a++) _dLpf1s[a].CopyCoefficients(_dLpf1Table[quantum]);
                    s.DynLpfThrottle = (byte)quantum;
                    s.DynLpfLoops = 0;
                }
            }
            if (s.DynLpfLoops < DynLpfUpdateLoops) s.DynLpfLoops++;
        }

        /// <summary>dynLpfCutoffFreq (pid.c 1221–1226), or dynThrottle's curve without expo (gyro.c 605–618).</summary>
        public static double DynLpfCutoff(double throttle, double min, double max, double expo)
        {
            if (expo > 0)
            {
                double curve = throttle * (1 - throttle) * (expo / 10) + throttle;
                return (max - min) * curve + min;
            }
            return DetMath.Max(throttle * (1 - throttle * throttle / 3) * 1.5 * max, min);
        }

        // rpmFilterApply: every harmonic, every motor (their weights all above zero).
        double Rpm(int axis, double x)
        {
            int at = axis * RpmHarmonicsMax * 4;
            for (int h = 0; h < _harmonics; h++)
                for (int m = 0; m < 4; m++)
                    x = _rpmNotches[at + h * 4 + m].ApplyDF1Weighted(x);
            return x;
        }

        // dynNotchUpdate: every sampleCount loops the accumulated samples' mean starts a round of analysis; each loop
        // pushes one batch of bins of every axis's sliding DFT, and an analysis step runs while the round lasts.
        void DynNotchUpdate(ref FcState s)
        {
            if (_sampleIndex == _sampleCount)
            {
                _sampleIndex = 0;
                for (int a = 0; a < 3; a++)
                {
                    _average[a] = _accumulator[a] * _sampleCountRcp;
                    _accumulator[a] = 0;
                }
                _tick = DynNotchCalcTicks;
            }
            for (int a = 0; a < 3; a++) PushBatch(a, _average[a], _sampleIndex);
            _sampleIndex++;
            if (_tick > 0)
            {
                Process(ref s);
                --_tick;
            }
        }

        // sdft.c sdftPushBatch (87–107) and updateEdges (187–200).
        void PushBatch(int axis, double sample, int batch)
        {
            int at = axis * SdftBinCount, sampleAt = axis * SdftSampleSize;
            int start = _batchSize * batch + _startBin, end = start;
            double delta = sample - _rPowerN * _samples[sampleAt + _sdftIdx[axis]];
            bool last = batch == _sampleCount - 1;
            if (last)
            {
                _samples[sampleAt + _sdftIdx[axis]] = sample;
                _sdftIdx[axis] = (_sdftIdx[axis] + 1) % SdftSampleSize;
                end += _endBin - start + 1;
            }
            else end += _batchSize;
            for (int i = start; i < end; i++) Twiddle(at, i, delta);
            if (_startBin > 0 && batch == 0) Twiddle(at, _startBin - 1, delta);
            if (_endBin < SdftBinCount - 1 && last) Twiddle(at, _endBin + 1, delta);
        }

        // data[i] = twiddle[i]·(data[i] + delta).
        void Twiddle(int at, int i, double delta)
        {
            double re = _re[at + i] + delta, im = _im[at + i];
            _re[at + i] = _twRe[i] * re - _twIm[i] * im;
            _im[at + i] = _twRe[i] * im + _twIm[i] * re;
        }

        // dynNotchProcess: one of its four steps on the current axis.
        void Process(ref FcState s)
        {
            int axis = _axis;
            switch (_step)
            {
                case 0:
                {
                    // STEP_WINDOW: the Hann-windowed power (sdftWinSq), and its total over the range.
                    WindowedPower(axis);
                    _noiseThreshold = 0;
                    for (int bin = _startBin; bin <= _endBin; bin++) _noiseThreshold += _power[bin];
                    break;
                }
                case 1:
                {
                    // STEP_DETECT_PEAKS: the count biggest local maxima, then in ascending bin order (empty ones last).
                    for (int p = 0; p < _notchCount; p++)
                    {
                        _peakBin[p] = 0;
                        _peakValue[p] = 0;
                    }
                    for (int bin = _startBin + 1; bin < _endBin; bin++)
                    {
                        if (_power[bin] > _power[bin - 1] && _power[bin] > _power[bin + 1])
                        {
                            for (int p = 0; p < _notchCount; p++)
                            {
                                if (_power[bin] > _peakValue[p])
                                {
                                    for (int k = _notchCount - 1; k > p; k--)
                                    {
                                        _peakBin[k] = _peakBin[k - 1];
                                        _peakValue[k] = _peakValue[k - 1];
                                    }
                                    _peakBin[p] = bin;
                                    _peakValue[p] = _power[bin];
                                    break;
                                }
                            }
                            bin++;
                        }
                    }
                    for (int p = _notchCount - 1; p > 0; p--)
                    {
                        for (int k = 0; k < p; k++)
                        {
                            if (_peakBin[k] > _peakBin[k + 1] && _peakBin[k + 1] != 0)
                            {
                                (_peakBin[k], _peakBin[k + 1]) = (_peakBin[k + 1], _peakBin[k]);
                                (_peakValue[k], _peakValue[k + 1]) = (_peakValue[k + 1], _peakValue[k]);
                            }
                        }
                    }
                    break;
                }
                case 2:
                {
                    // STEP_CALC_FREQUENCIES: the noise floor without the peaks, twice it the threshold; each peak above it
                    // fitted by a parabola and the notch's centre moved toward it, faster for a bigger peak.
                    int peaks = 0;
                    for (int p = 0; p < _notchCount; p++)
                    {
                        if (_peakBin[p] == 0) continue;
                        _noiseThreshold -= 0.75 * _power[_peakBin[p] - 1];
                        _noiseThreshold -= _power[_peakBin[p]];
                        _noiseThreshold -= 0.75 * _power[_peakBin[p] + 1];
                        peaks++;
                    }
                    _noiseThreshold /= _endBin - _startBin - peaks + 1;
                    _noiseThreshold *= 2;
                    for (int p = 0; p < _notchCount; p++)
                    {
                        if (_peakBin[p] == 0 || !(_peakValue[p] > _noiseThreshold)) continue;
                        double meanBin = _peakBin[p];
                        double y0 = _power[_peakBin[p] - 1], y1 = _power[_peakBin[p]], y2 = _power[_peakBin[p] + 1];
                        double denom = 2 * (y0 - 2 * y1 + y2);
                        if (denom != 0) meanBin += (y0 - y2) / denom;
                        double centre = Clamp(meanBin * _resolutionHz, _notchMinHz, _notchMaxHz);
                        double multiplier = Clamp(_peakValue[p] / _noiseThreshold, 1, 10);
                        double gain = Pt1Gain(DynNotchSmoothHz * multiplier, _pt1LooptimeS);
                        ref Axes3 hz = ref NotchHz(ref s, p);
                        hz[axis] = hz[axis] + gain * (centre - hz[axis]);
                    }
                    break;
                }
                default:
                {
                    // STEP_UPDATE_FILTERS: the moved notches' coefficients; then the next axis.
                    for (int p = 0; p < _notchCount; p++)
                        if (_peakBin[p] != 0 && _peakValue[p] > _noiseThreshold)
                            _dynNotches[axis * DynNotchCountMax + p].Update(NotchHz(ref s, p)[axis], LoopUs, _notchQ, true, 1);
                    _axis = (_axis + 1) % 3;
                    break;
                }
            }
            _step = (_step + 1) % DynNotchStepCount;
        }

        // sdft.c sdftWinSq (134–166): X[k] − (X[k−1] + X[k+1])/2, squared, over the range; at the spectrum's edge the
        // missing neighbour is dropped.
        void WindowedPower(int axis)
        {
            int at = axis * SdftBinCount;
            for (int i = _startBin; i <= _endBin; i++)
            {
                double re, im;
                if (i == _startBin && _startBin == 0)
                {
                    re = _re[at + i] - _re[at + i + 1];
                    im = _im[at + i] - _im[at + i + 1];
                }
                else if (i == _endBin && _endBin == SdftBinCount - 1)
                {
                    re = _re[at + i] - _re[at + i - 1];
                    im = _im[at + i] - _im[at + i - 1];
                }
                else
                {
                    re = _re[at + i] - 0.5 * (_re[at + i - 1] + _re[at + i + 1]);
                    im = _im[at + i] - 0.5 * (_im[at + i - 1] + _im[at + i + 1]);
                }
                _power[i] = re * re + im * im;
            }
        }

        /// <summary>A dynamic notch's centres, Hz per axis (FcState.DynNotch1…5).</summary>
        public static ref Axes3 NotchHz(ref FcState s, int notch)
        {
            switch (notch)
            {
                case 0: return ref s.DynNotch1;
                case 1: return ref s.DynNotch2;
                case 2: return ref s.DynNotch3;
                case 3: return ref s.DynNotch4;
                case 4: return ref s.DynNotch5;
                default: throw new ArgumentOutOfRangeException(nameof(notch));
            }
        }

        // A lowpass slot: on when its cutoff is set and allowed; only Betaflight's BIQUAD is modelled.
        static bool Lowpass(string name, LowpassType type, double hz, bool allowed)
        {
            if (!(hz > 0)) return false;
            if (type != LowpassType.BIQUAD)
                throw new ArgumentException(name + "_type " + type + " is not modelled (p6-flight-core 1.4b): only BIQUAD, the builder's");
            return allowed;
        }

        // gyroInitFilterNotch1/2 and pidInitFilters' notch: the centre held at Nyquist when above it (gyro_init.c 97–109,
        // pid_init.c 84–93); on with a centre and a cutoff.
        static bool Notch(double hz, double cutoff, out Biquad init)
        {
            if (hz > LoopNyquistHz) hz = cutoff < LoopNyquistHz ? LoopNyquistHz : 0;
            init = default;
            if (hz == 0 || cutoff == 0) return false;
            init = Biquad.Notch(hz, LoopUs, Biquad.NotchQ(hz, cutoff));
            return true;
        }

        // common/filter.c pt1FilterGain.
        static double Pt1Gain(double cutoffHz, double dt)
        {
            double omega = TwoPi * cutoffHz * dt;
            return omega / (omega + 1);
        }

        // lrintf for x ≥ 0: to the nearest whole number, ties to even (the default rounding mode).
        static int Lrint(double x)
        {
            int n = (int)x;
            double f = x - n;
            if (f > 0.5 || (f == 0.5 && (n & 1) == 1)) n++;
            return n;
        }

        static double Clamp(double x, double low, double high) => x < low ? low : x > high ? high : x;

        static bool IsWhole(double x, double low, double high) => x >= low && x <= high && x == (int)x;

        static int IntMax(int a, int b) => b > a ? b : a;

        static int IntMin(int a, int b) => b < a ? b : a;
    }
}
