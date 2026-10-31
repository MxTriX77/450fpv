using System;
using System.Collections.Generic;
using Fpv450.Sim.Control;

namespace Fpv450.Input.Radio
{
    public enum CalibrationStep { Centre, Throttle, Yaw, Pitch, Roll, ArmOn, ArmOff, Done }

    public enum FindingKind
    {
        /// <summary>The axis spans a tiny range, or jumps from end to end: it behaves like a switch. Blocks saving.</summary>
        TooNarrow,

        /// <summary>The stick-to-channel curve is not a line: expo or a curve left on the radio.</summary>
        RadioCurve,

        /// <summary>An endpoint short of the radio's full travel: dual rates or a weight under 100 %.</summary>
        ShortEndpoint,
    }

    /// <summary>One thing the health check found, said plainly for the pilot.</summary>
    public sealed class CalibrationFinding
    {
        public FindingKind Kind;
        public string Stick, Message;

        public bool Blocking => Kind == FindingKind.TooNarrow;
    }

    /// <summary>
    /// The calibration wizard (INP-02, p6-input-app design 4), engine-free: fed every raw report with its arrival
    /// stamp, so its result never depends on the frame rate, and it advances on the radio alone, never on a key.
    /// - **Centre:** every axis still for 1 s, the sticks let go and the throttle down: each axis's rest and its noise
    ///   (the deadband), and the buttons' rest.
    /// - **The sweep,** one stick at a time (throttle, yaw, pitch, roll): from one end slowly to the other, then held.
    ///   The axis that travels most is the stick's, which gives the channel order; the direction of the sweep gives
    ///   the reversal; its extremes the endpoints. The hold ends the step: 0.3 s at 80 % or more of the radio's travel
    ///   from the rest (from the start for the throttle), 2 s short of it, so a pause mid-sweep rarely ends it.
    /// - **The health check:** a span under 200 of a 2048 range, or a sweep that jumps from end to end twice running
    ///   (under 0.7 s from 10 % to 90 % of its travel; the first time the pilot is asked to go slower), is too
    ///   narrow: it behaves like a switch (the TX12 pitfall in fc-profiles), recalibrate the radio. The jump rule
    ///   times the sweep, so a slow pilot passes it; a radio's axis whose slower sweep never changes by less than
    ///   PitfallStep between two reports, on either side of its centre, is too narrow at any speed, jitter or filter.
    ///   A side whose part of the band held too few of the radio's updates (UpdateUs) to show a healthy step is not
    ///   judged: the pilot is asked to go slower, as many times as it needs; a repeat no slower (its side held under
    ///   Slower times the updates of the last) names the axis: by its speed if its step is under EdgeTX's floor gain, else
    ///   as a switch. An axis that keeps the centre step from settling for 5 s
    ///   is named in the prompt.
    /// - **Radio-side curves:** the sweep from 10 % to 90 % of its travel off a fitted line by more than 3 % of the
    ///   travel is a curve on the radio; an endpoint more than 5 % short of a radio's full range is dual rates or a
    ///   weight under 100 %. Both are named, with what to switch off; neither blocks saving.
    /// - **The arm switch:** the axis or button that changes when it is flipped on, then back off.
    /// The thresholds are assumed (design 4) until the pilot calibrates a TX12 by hand at G7.
    /// </summary>
    public sealed class CalibrationWizard
    {
        public const long StillUs = 1000000, HoldUs = 300000, LongHoldUs = 2000000, ArmHoldUs = 200000;
        public const long MinTransitUs = 700000, RestlessUs = 5000000;
        public const double StillBand = 0.02, MinTravel = 0.02, EndBand = 0.10, Dominance = 2, FullReach = 0.8;
        public const double CentreSide = 0.35, NarrowSpan = 200.0 / 2048, LineTolerance = 0.03, ShortEnd = 0.05;
        public const double ArmChange = 0.25;

        /// <summary>
        /// The TX12 pitfall's smallest step, a fraction of the HID range: half EdgeTX's floor gain. EdgeTX scales a stick
        /// as `(v − mid) · 1024 / max(100, span)` (mixer.cpp evalInputs) onto the 2048-count range, so an axis whose radio
        /// calibration saved a span under 100 (fc-profiles' 79 / 1 and 85 / 0) moves 1024 / 100 = 10.24 counts for each
        /// ADC count: however slowly it is swept, jittery or filtered still, its value never changes by less than 10
        /// counts between two reports (EdgeTX's integer division). A healthy span (683–903) moves it 1.1–1.5 counts for
        /// each, so a sweep's smallest change is 1 or 2. Half the floor gain, 5 counts, sits a factor 2 under the pitfall
        /// and over the 4.7 counts a still ADC moves per report at the jump rule's fastest accepted sweep (80 % of the
        /// 2048 in 0.7 s, a report every 2 ms). It catches a radio span up to about 205 (1024 / 5); 250–300 pass.
        /// A radio that updates less often moves more per update (QA's finding 10: 5–9 counts every 3–4 ms on a quick
        /// sweep), so a side of the sweep is judged only if its part of the band held band / (PitfallStep − 1) updates
        /// or more: then a healthy radio's mean step is under PitfallStep − 1 and some step is smaller (pigeonhole, with
        /// a count's margin; about 205 a side, 410 over the band). Fewer, and the sweep was too fast to tell: the step is
        /// repeated, as many times slower as the side needs, until a repeat is no slower (QA's finding 11): the pitfall's
        /// coarse side passes about 82 ADC counts, so it needs about 8 s a sweep at 2 ms and 16 s at 4 ms. At 2 ms a
        /// healthy radio is never coarse on a sweep the jump rule accepts.
        /// </summary>
        public const double PitfallStep = 1024.0 / 100 / 2 / 2048;

        /// <summary>The changes of one axis that UpdateUs is the mean interval of.</summary>
        public const int UpdateWindow = 16;

        /// <summary>
        /// The slowest radio's update interval, µs, and UpdateUs's bound before 16 changes: EdgeTX's MAX_REFRESH_RATE
        /// (mixer_scheduler.h). EdgeTX runs its mixer, and so its USB output, at the RF module's period (ELRS at 150 Hz:
        /// 6.7 ms; at 50 Hz: 20 ms), so a TX12 with its module on updates that slowly (QA's finding 12). A still ADC's slow
        /// sweep reads slower than its radio (QA's finding 11); the Slower rule bounds the repeats that costs.
        /// </summary>
        public const long SlowestUpdateUs = 50000;

        /// <summary>
        /// A repeat is slower only if its coarse side held this many times the updates of the last; else the axis is
        /// named. A factor, not "more", so the repeats are bounded: a side that held a quarter of its need ends within 7.
        /// </summary>
        public const double Slower = 1.25;

        /// <summary>The most times slower a prompt asks for; the least is 2.</summary>
        public const int MostTimesSlower = 10;

        /// <summary>
        /// How many times slower a side that held this share of the updates it needs must be swept: 2 to MostTimesSlower,
        /// capped before the cast, so a share of 0 (in-band reports on one stamp) asks MostTimesSlower.
        /// </summary>
        public static int TimesSlower(double held) => Math.Max(2, (int)Math.Min(MostTimesSlower, Math.Ceiling(1 / held)));

        static readonly string[] StickNames = { "Throttle", "Yaw", "Pitch", "Roll" };

        readonly RadioAxis[] _axes;
        readonly InputSource _source;
        readonly int _n;
        readonly int[] _rest, _noise, _spread, _lo, _hi, _holdMin, _holdMax, _winMin, _winMax;
        readonly long[] _holdStart, _changeUs; // _changeUs: each axis's last UpdateWindow + 1 change stamps, a ring
        readonly int[] _changes, _last;
        readonly bool[] _assigned;
        readonly List<long> _t = new List<long>();
        readonly List<int> _v = new List<int>(); // _n values per report of the step
        ulong _restButtons, _winButtons;
        long _winStart = -1;
        int _fastAxis = -1;
        bool _fastHigh; // the end the too-fast sweep finished at, which the repeat must finish at too
        bool _jumped; // the too-fast sweep jumped from end to end (the jump rule), not one too quick to judge its step
        double _held; // the share of the updates it needs that the too-quick sweep's coarse side held
        int _times; // how many times slower than the last sweep the prompt asks for; 0: about 2 seconds
        bool _radioBlamed; // a blocking finding names the radio first, not only a sweep's speed
        int _armCandidate = -1; // an axis index, or 1000 + a button index
        long _armSince;
        int _mover = -1; // the axis that last broke the centre step's stillness

        public CalibrationWizard(string device, InputSource source, RadioAxis[] axes)
        {
            _axes = axes ?? throw new ArgumentNullException(nameof(axes));
            _source = source;
            _n = axes.Length;
            _rest = new int[_n];
            _noise = new int[_n];
            _spread = new int[_n];
            _lo = new int[_n];
            _hi = new int[_n];
            _holdMin = new int[_n];
            _holdMax = new int[_n];
            _winMin = new int[_n];
            _winMax = new int[_n];
            _holdStart = new long[_n];
            _changeUs = new long[_n * (UpdateWindow + 1)];
            _changes = new int[_n];
            _last = new int[_n];
            _assigned = new bool[_n];
            Profile.Device = device ?? "";
            Profile.Name = Profile.Device.Length > 0 ? Profile.Device : "Radio"; // the default name, renamed at will
        }

        public CalibrationStep Step { get; private set; } = CalibrationStep.Centre;

        /// <summary>The last sweep went from end to end too fast, or too fast to judge its step: the step is repeated, slower.</summary>
        public bool TooFast => Repeats > 0;

        /// <summary>The times this step has been asked again, slower, so far.</summary>
        public int Repeats { get; private set; }

        /// <summary>
        /// The radio's update interval, µs: the shortest mean interval between UpdateWindow consecutive changes of any one
        /// axis so far, and never more than SlowestUpdateUs (it before). A report that repeats the last (a 1 ms USB stream
        /// over a 4 ms radio) is not an update; the mean rides out the stamps' wake jitter and reports read in a bunch.
        /// </summary>
        public long UpdateUs { get; private set; } = SlowestUpdateUs;

        /// <summary>The profile so far; complete at Done.</summary>
        public CalibrationProfile Profile { get; } = new CalibrationProfile();

        public List<CalibrationFinding> Findings { get; } = new List<CalibrationFinding>();

        /// <summary>Done, and the health check found an axis too narrow: the profile must not be saved.</summary>
        public bool Failed => Step == CalibrationStep.Done && Findings.Exists(f => f.Blocking);

        /// <summary>Stamp µs of the first report and of the last.</summary>
        public long FirstUs { get; private set; } = -1;

        public long LastUs { get; private set; } = -1;

        /// <summary>What the pilot does now, in plain words.</summary>
        public string Prompt
        {
            get
            {
                string slower = Repeats == 0 ? "" : Repeats == 1 ? "Too fast. " : "Still too fast. ";
                string pace = _times > 0 ? "about " + _times + " times slower than that" : "about 2 seconds";
                switch (Step)
                {
                    case CalibrationStep.Centre:
                        return Restless < 0 ? "Let go of the sticks. Throttle fully down, arm switch off."
                            : "Let go of the sticks. Channel " + (Restless + 1) + " keeps moving: if it does with your hands off, recalibrate the radio's sticks.";
                    case CalibrationStep.Throttle: return slower + "Throttle: slowly all the way up, " + pace + ", and hold.";
                    case CalibrationStep.Yaw: return slower + "Yaw: full left, then slowly full right, " + pace + ", and hold.";
                    case CalibrationStep.Pitch: return slower + "Pitch: full back, then slowly full forward, " + pace + ", and hold.";
                    case CalibrationStep.Roll: return slower + "Roll: full left, then slowly full right, " + pace + ", and hold.";
                    case CalibrationStep.ArmOn: return "Throttle down, then flip the arm switch on.";
                    case CalibrationStep.ArmOff: return "Flip the arm switch off.";
                    default:
                        if (!Failed) return "Calibrated.";
                        return _radioBlamed ? "Recalibrate the radio's sticks, then calibrate here again." : "Calibrate here again, sweeping slower.";
                }
            }
        }

        /// <summary>
        /// The axis (0-based) that has kept the centre step from settling for RestlessUs, or −1: a thumb on a stick, or a
        /// radio's axis that jitters over the still band at rest (the TX12 pitfall with 3 ADC counts of jitter or more).
        /// </summary>
        public int Restless => Step == CalibrationStep.Centre && LastUs - FirstUs >= RestlessUs ? _mover : -1;

        /// <summary>Takes one report, in arrival order.</summary>
        public void Feed(RadioReport report)
        {
            if (Step == CalibrationStep.Done) return;
            if (FirstUs < 0) FirstUs = report.HostUs;
            LastUs = report.HostUs;
            Measure(report);
            switch (Step)
            {
                case CalibrationStep.Centre:
                    FeedCentre(report);
                    break;
                case CalibrationStep.ArmOn:
                    FeedArmOn(report);
                    break;
                case CalibrationStep.ArmOff:
                    if (CalibratedStickMap.Armed(report, Profile.Arm)) _armSince = report.HostUs;
                    else if (report.HostUs - _armSince >= ArmHoldUs) Step = CalibrationStep.Done;
                    break;
                default:
                    FeedStick(report);
                    break;
            }
        }

        int Range(int i) => _axes[i].Max - _axes[i].Min;

        int Band(int i) => Math.Max(2, (int)(StillBand * Range(i)));

        void Measure(RadioReport r)
        {
            int m = UpdateWindow + 1;
            for (int i = 0; i < _n; i++)
            {
                int v = r.Axes[i], c = _changes[i];
                if (c > 0 && v == _last[i]) continue;
                _last[i] = v;
                _changeUs[i * m + c % m] = r.HostUs;
                if (c >= UpdateWindow) UpdateUs = Math.Min(UpdateUs, (r.HostUs - _changeUs[i * m + (c - UpdateWindow) % m]) / UpdateWindow);
                _changes[i] = c + 1;
            }
        }

        // ---- Centre ----

        void FeedCentre(RadioReport r)
        {
            bool reset = _winStart < 0 || r.Buttons != _winButtons;
            for (int i = 0; i < _n && !reset; i++)
            {
                int v = r.Axes[i];
                if (Math.Max(_winMax[i], v) - Math.Min(_winMin[i], v) > Band(i))
                {
                    reset = true;
                    _mover = i;
                }
            }
            if (reset)
            {
                _winStart = r.HostUs;
                _winButtons = r.Buttons;
                for (int i = 0; i < _n; i++) _winMin[i] = _winMax[i] = r.Axes[i];
                return;
            }
            for (int i = 0; i < _n; i++)
            {
                _winMin[i] = Math.Min(_winMin[i], r.Axes[i]);
                _winMax[i] = Math.Max(_winMax[i], r.Axes[i]);
            }
            if (r.HostUs - _winStart < StillUs) return;
            for (int i = 0; i < _n; i++)
            {
                _rest[i] = (_winMin[i] + _winMax[i]) / 2;
                _spread[i] = _winMax[i] - _winMin[i];
                _noise[i] = (_spread[i] + 1) / 2;
            }
            _restButtons = r.Buttons;
            Begin(CalibrationStep.Throttle, r);
        }

        // ---- The sweep ----

        void Begin(CalibrationStep step, RadioReport r)
        {
            Step = step;
            _t.Clear();
            _v.Clear();
            for (int i = 0; i < _n; i++)
            {
                _lo[i] = _hi[i] = _holdMin[i] = _holdMax[i] = r.Axes[i];
                _holdStart[i] = r.HostUs;
            }
            _armCandidate = -1;
        }

        void FeedStick(RadioReport r)
        {
            _t.Add(r.HostUs);
            for (int i = 0; i < _n; i++) _v.Add(r.Axes[i]);

            int leader = -1;
            int lead = -1, second = 0;
            for (int i = 0; i < _n; i++)
            {
                if (_assigned[i]) continue;
                int v = r.Axes[i];
                _lo[i] = Math.Min(_lo[i], v);
                _hi[i] = Math.Max(_hi[i], v);
                if (Math.Max(_holdMax[i], v) - Math.Min(_holdMin[i], v) > Band(i))
                {
                    _holdStart[i] = r.HostUs;
                    _holdMin[i] = _holdMax[i] = v;
                }
                else
                {
                    _holdMin[i] = Math.Min(_holdMin[i], v);
                    _holdMax[i] = Math.Max(_holdMax[i], v);
                }
                int travel = _hi[i] - _lo[i];
                if (travel > lead)
                {
                    second = Math.Max(second, lead);
                    lead = travel;
                    leader = i;
                }
                else second = Math.Max(second, travel);
            }
            if (leader < 0 || lead < MinTravel * Range(leader) || lead < Dominance * second) return;

            int L = leader, value = r.Axes[L], lo = _lo[L], hi = _hi[L];
            bool atHi = hi - value <= EndBand * lead, atLo = value - lo <= EndBand * lead;
            if (atHi == atLo) return;
            if (L == _fastAxis && atHi != _fastHigh) return; // the way back to the start, before the repeat
            int finish = atHi ? hi : lo, start = atHi ? lo : hi;
            bool throttle = Step == CalibrationStep.Throttle;
            double reach;
            if (throttle)
            {
                reach = Math.Abs(finish - start) / (double)Range(L);
            }
            else
            {
                int rest = _rest[L];
                if (rest - lo < CentreSide * lead || hi - rest < CentreSide * lead) return; // one side only so far
                int end = atHi ? _axes[L].Max : _axes[L].Min;
                reach = Math.Abs(finish - rest) / (double)Math.Max(1, Math.Abs(end - rest));
            }
            if (r.HostUs - _holdStart[L] < (reach >= FullReach ? HoldUs : LongHoldUs)) return;

            // The sweep: from the last report near the start end to the first near the finish after it.
            int count = _t.Count, iS = -1, iF = -1;
            for (int k = count - 1; k >= 0; k--)
                if (Math.Abs(_v[k * _n + L] - start) <= EndBand * lead)
                {
                    iS = k;
                    break;
                }
            for (int k = Math.Max(0, iS); k < count; k++)
                if (Math.Abs(_v[k * _n + L] - finish) <= EndBand * lead)
                {
                    iF = k;
                    break;
                }
            long transit = iS >= 0 && iF > iS ? _t[iF] - _t[iS] : 0;
            string name = StickNames[(int)Step - (int)CalibrationStep.Throttle];
            bool narrow = lead < NarrowSpan * Range(L), jumps = false, coarse = false, again = false;
            int step = 0, slower = 0; // slower: how many times slower a repeat named for its speed needed
            if (!narrow && transit < MinTransitUs)
            {
                if (_fastAxis != L || !_jumped)
                {
                    Repeat(L, atHi, true, 0, r); // once: the pilot may have been quick
                    return;
                }
                jumps = true;
            }
            else if (!narrow && _source == InputSource.Radio)
            {
                // The pitfall at any slower speed: EdgeTX's floor gain never moves the axis by less than 10 counts. The
                // throttle too, split at the range's middle, where EdgeTX's output is 0.
                int pitfall = Math.Max(2, (int)(PitfallStep * Range(L)));
                step = SmallestStep(L, iS, iF, lo, hi, throttle ? (_axes[L].Min + _axes[L].Max) / 2 : _rest[L], pitfall, out double held, out int deferred);
                coarse = step >= pitfall;
                if (!coarse && held < 1)
                {
                    if (_fastAxis != L || _jumped || held >= Slower * _held)
                    {
                        // A slow radio's quick sweep is not the pitfall: again, as many times slower as the coarse side
                        // needs. A factor of the pilot's own sweep, not seconds: a pitfall's flat ends hide the stick, so
                        // the wizard cannot time the whole sweep.
                        _held = held;
                        Repeat(L, atHi, false, TimesSlower(held), r);
                        return;
                    }
                    step = deferred; // no slower: its steps are the radio's, or the pilot cannot sweep slower
                    coarse = again = true;
                    // Under EdgeTX's floor gain (10 of the 2048) the pitfall cannot be: the sweep's speed is named first.
                    if (step < (int)(2 * PitfallStep * Range(L))) slower = TimesSlower(held);
                }
            }
            Repeats = 0;
            _fastAxis = -1;
            _times = 0;

            StickCalibration c = Stick(Step);
            c.Axis = L;
            c.Min = lo;
            c.Max = hi;
            c.Reversed = finish < start; // the sweep ends right, forward or up
            c.Centre = throttle ? (lo + hi) / 2 : _rest[L];
            c.Deadband = throttle ? 0 : _noise[L];
            _assigned[L] = true;

            int span = Range(L) + 1;
            _radioBlamed |= (narrow || coarse || jumps) && slower == 0;
            if (narrow)
                Add(FindingKind.TooNarrow, name, name + " spans only " + lead + " of " + span + ": it behaves like a switch. Recalibrate the radio's sticks, then calibrate here again.");
            else if (slower > 0)
                Add(FindingKind.TooNarrow, name, name + " was not swept slower, so it cannot be told from a switch. Calibrate here again, sweeping it about " + slower + " times slower. If it is refused again, recalibrate the radio's sticks.");
            else if (again)
                Add(FindingKind.TooNarrow, name, name + " moves in jumps of " + step + " of " + span + " or more and was not swept slower: it behaves like a switch. Recalibrate the radio's sticks, then calibrate here again, sweeping it slower.");
            else if (coarse)
                Add(FindingKind.TooNarrow, name, name + " moves in jumps of " + step + " of " + span + " or more, however slowly it is swept: it behaves like a switch. Recalibrate the radio's sticks, then calibrate here again.");
            else if (jumps)
                Add(FindingKind.TooNarrow, name, name + " jumps from end to end even when moved slowly: it behaves like a switch. Recalibrate the radio's sticks, then calibrate here again.");
            else
            {
                if (OffLine(L, iS, iF) > LineTolerance * lead)
                    Add(FindingKind.RadioCurve, name, name + " does not follow the stick in a straight line: switch off expo and curves for it on the radio. The sim's rates do that.");
                if (_source == InputSource.Radio && (lo > _axes[L].Min + ShortEnd * Range(L) || hi < _axes[L].Max - ShortEnd * Range(L)))
                    Add(FindingKind.ShortEndpoint, name, name + " reaches only " + Math.Round(100.0 * lead / Range(L)) + " % of the radio's full travel: switch off dual rates and set its weight to 100 % on the radio, and move the stick fully to each end.");
            }

            if (Step == CalibrationStep.Roll)
            {
                Step = CalibrationStep.ArmOn;
                _armCandidate = -1;
            }
            else Begin(Step + 1, r);
        }

        // The step again, slower; the way back to the start of the sweep is not a sweep.
        void Repeat(int axis, bool high, bool jumped, int times, RadioReport r)
        {
            _fastAxis = axis;
            _fastHigh = high;
            _jumped = jumped;
            _times = times;
            Repeats++;
            Begin(Step, r);
        }

        StickCalibration Stick(CalibrationStep step)
        {
            switch (step)
            {
                case CalibrationStep.Throttle: return Profile.Throttle;
                case CalibrationStep.Yaw: return Profile.Yaw;
                case CalibrationStep.Pitch: return Profile.Pitch;
                default: return Profile.Roll;
            }
        }

        // The radio's gain per ADC count, at any sweep speed: the smallest non-zero change between two consecutive reports
        // of the sweep inside 10–90 % of its travel, on each side of the centre (EdgeTX scales each side by its own span),
        // and the larger of the two. 0 if neither side has one. A side as coarse as pitfall counts only if its part of
        // the band held enough of the radio's updates to judge it (PitfallStep); else it is unjudged: held is the least
        // share of the updates it needs that an unjudged side held (1 if none), deferred its larger smallest step.
        int SmallestStep(int axis, int from, int to, int lo, int hi, int centre, int pitfall, out double held, out int deferred)
        {
            double inLo = lo + EndBand * (hi - lo), inHi = hi - EndBand * (hi - lo);
            int below = int.MaxValue, above = int.MaxValue;
            long belowFrom = -1, belowTo = -1, aboveFrom = -1, aboveTo = -1;
            for (int k = Math.Max(0, from); k < to; k++)
            {
                int a = _v[k * _n + axis], b = _v[(k + 1) * _n + axis], d = Math.Abs(b - a);
                if (Math.Min(a, b) < inLo || Math.Max(a, b) > inHi) continue;
                if (a > centre && b > centre)
                {
                    if (d != 0) above = Math.Min(above, d);
                    if (aboveFrom < 0) aboveFrom = _t[k];
                    aboveTo = _t[k + 1];
                }
                else if (a < centre && b < centre)
                {
                    if (d != 0) below = Math.Min(below, d);
                    if (belowFrom < 0) belowFrom = _t[k];
                    belowTo = _t[k + 1];
                }
            }
            held = 1;
            deferred = 0;
            below = Side(below, belowTo - belowFrom, centre - inLo, pitfall, ref held, ref deferred);
            above = Side(above, aboveTo - aboveFrom, inHi - centre, pitfall, ref held, ref deferred);
            return Math.Max(below, above);
        }

        // One side's smallest step: 0 without one, or if it is as coarse as pitfall over fewer than band / (pitfall − 1)
        // of the radio's updates in us (unjudged).
        int Side(int smallest, long us, double band, int pitfall, ref double held, ref int deferred)
        {
            if (smallest == int.MaxValue) return 0;
            double share = us * (double)(pitfall - 1) / (band * UpdateUs);
            if (smallest < pitfall || share >= 1) return smallest;
            held = Math.Min(held, share);
            deferred = Math.Max(deferred, smallest);
            return 0;
        }

        // The largest distance of the sweep's reports from their least-squares line in time.
        double OffLine(int axis, int from, int to)
        {
            if (from < 0 || to - from < 2) return 0;
            double t0 = _t[from], st = 0, sv = 0, stt = 0, stv = 0;
            int m = to - from + 1;
            for (int k = from; k <= to; k++)
            {
                double t = (_t[k] - t0) * 1e-6, v = _v[k * _n + axis];
                st += t;
                sv += v;
                stt += t * t;
                stv += t * v;
            }
            double det = m * stt - st * st;
            if (det <= 0) return 0;
            double b = (m * stv - st * sv) / det, a = (sv - b * st) / m, worst = 0;
            for (int k = from; k <= to; k++)
                worst = Math.Max(worst, Math.Abs(_v[k * _n + axis] - (a + b * (_t[k] - t0) * 1e-6)));
            return worst;
        }

        void Add(FindingKind kind, string stick, string message) =>
            Findings.Add(new CalibrationFinding { Kind = kind, Stick = stick, Message = message });

        // ---- The arm switch ----

        void FeedArmOn(RadioReport r)
        {
            int candidate = -1, best = 0;
            for (int i = 0; i < _n; i++)
            {
                if (_assigned[i]) continue;
                int d = Math.Abs(r.Axes[i] - _rest[i]);
                if (d >= ArmChange * Range(i) && d > best)
                {
                    best = d;
                    candidate = i;
                }
            }
            ulong changed = r.Buttons ^ _restButtons;
            if (candidate < 0 && changed != 0)
                for (int b = 0; b < 64; b++)
                    if (((changed >> b) & 1) != 0)
                    {
                        candidate = 1000 + b;
                        break;
                    }
            if (candidate != _armCandidate)
            {
                _armCandidate = candidate;
                _armSince = r.HostUs;
                return;
            }
            if (candidate < 0 || r.HostUs - _armSince < ArmHoldUs) return;

            ArmCalibration a = Profile.Arm;
            if (candidate < 1000)
            {
                a.Control = ArmControl.Axis;
                a.Index = candidate;
                a.Threshold = (_rest[candidate] + r.Axes[candidate]) / 2;
                a.OnHigh = r.Axes[candidate] > _rest[candidate];
            }
            else
            {
                a.Control = ArmControl.Button;
                a.Index = candidate - 1000;
                a.OnHigh = ((r.Buttons >> a.Index) & 1) != 0;
            }
            Step = CalibrationStep.ArmOff;
            _armSince = r.HostUs;
        }
    }
}
