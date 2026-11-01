using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;

namespace Fpv450.Input.Radio
{
    /// <summary>
    /// The latency test mode (design 3, INP-04): a known stick step through the whole HID path, timed in the physics.
    /// - **The path:** the device's reports (a recording, or a live radio's) cross the device boundary through an
    ///   injector that keeps each report's arrival stamp and replaces its values with the pattern; then the input
    ///   thread (RadioReader, the raw map), the flight's StickInbox and the flight thread (FlightScheduler, with the
    ///   app's 1 ms timer period) on the bench: the repository's drone at its nominal loadout, 300 m up in still air,
    ///   through the controller's RC link, RC smoothing and mixer and the ESCs.
    /// - **The pattern,** from the flight's start: disarmed, throttle down; the arm switch on at ArmUs; the throttle stick
    ///   at Low from HoldFromUs, so the rotors and the throttle boost settle; then from FirstChangeUs a change every
    ///   HoldUs between High and Low, Changes of them, about hover and small enough that the ESCs stay off their
    ///   current limit. Roll, pitch and yaw centred.
    /// - **Two figures** from each change's first report's arrival stamp (InputLatency): to the host time of the first
    ///   step that uses it, and to the motor command's response, the ESCs' mean duty halfway through the change. The
    ///   breakdown splits the second: the wait for the RC link's next frame (every 4 steps at 250 Hz), the RC smoothing
    ///   and the mixer to the controller's DShot command halfway, and the ESC's lag from there.
    /// - **The log** is the test flight's own. Its header is written before the first step, so the figures are spliced
    ///   into it after the flight, with the flight's step-0 host time.
    /// </summary>
    public sealed class LatencyTest
    {
        public const long ArmUs = 100000, HoldFromUs = 300000, FirstChangeUs = 800000, HoldUs = 82000;
        public const double Low = 0.42, High = 0.48;

        /// <summary>The input thread reads this long before the flight: the report-interval histogram's window.</summary>
        public const long PreRollUs = 1000000;

        const int StepsPerFrame = FlightController.LoopsPerRcFrame / FlightController.LoopsPerStep;
        const long CommandAtUs = (FlightController.LoopsPerStep - 1) * FlightCore.StepUs / FlightController.LoopsPerStep;

        /// <summary>The flight's step-0 host µs and the changes made.</summary>
        public readonly long StartUs;
        public readonly int Changes;

        /// <summary>The figures, as the header holds them.</summary>
        public InputLatency Figures;

        /// <summary>
        /// The second figure's parts over the same changes, µs: the wait from the first step that uses the change to the
        /// RC link's next frame; from that frame to the controller's DShot command halfway (the RC smoothing, the
        /// throttle boost, the mixer); from there to the ESCs' duty halfway (the ESC's lag).
        /// </summary>
        public LatencySpread FrameWait, Smoothing, Esc;

        /// <summary>Each timed change's parts, µs, in order: arrival to the first step, the frame wait, the smoothing, the ESC.</summary>
        public long[] FirstStepUs, FrameWaitUs, SmoothingUs, EscUs, MotorUs;

        /// <summary>Changes timed (some may be lost to a stall), and those with an ESC at its current limit.</summary>
        public int Timed, Limited;

        /// <summary>The flight's steps, hitches and dropped reports, and the report intervals before the flight.</summary>
        public long Steps, Hitches, Dropped;
        public ReportIntervals Intervals;
        public string DeviceName = "";
        public InputSource Source;

        LatencyTest(long startUs, int changes)
        {
            StartUs = startUs;
            Changes = changes;
        }

        /// <summary>The flight's length, µs: the settling, the changes' holds and one more, which ends the last.</summary>
        public static long DurationUs(int changes) => FirstChangeUs + (changes + 1) * HoldUs;

        /// <summary>The bench: the drone at its nominal loadout 300 m up in still air, with settings (no contacts).</summary>
        public static FlightSetup Bench(DroneSpec spec, in FcSettings settings) => new FlightSetup
        {
            Seed = 0x1A7E,
            Settings = settings,
            World = FlightWorld.Floor(),
            Mass = spec.BuildMass(spec.Nominal),
            Propulsion = spec.BuildPropulsion(),
            Aero = spec.BuildAero(),
            Wind = WindSetup.Of(WindLevel.Still),
            StartPosition = new Double3(0, 300, 0),
        };

        /// <summary>
        /// Runs the test on device in real time on clock (its stamps' clock), the flight's step 0 at startUs (at least
        /// PreRollUs from now, the reader starting at once), and writes the flight's log to log. Blocks for the flight.
        /// </summary>
        public static LatencyTest Run(IRadioDevice device, DroneSpec spec, in FcSettings settings, IHostClock clock, long startUs, int changes,
            Stream log, string build)
        {
            if (changes < 1) throw new ArgumentException("at least one change", nameof(changes));
            var test = new LatencyTest(startUs, changes);
            var injector = new Injector(device, test);
            bool opened = false;
            var reader = new RadioReader(() =>
            {
                if (opened) return null;
                opened = true;
                return injector;
            });
            long steps = DurationUs(changes) / FlightCore.StepUs;
            var probe = new Probe((int)steps + 64);
            using (FineTimer())
            {
                reader.Start();
                try
                {
                    Sleep(clock, startUs - 50000);
                    FlightSetup setup = Bench(spec, settings);
                    var core = new FlightCore(setup);
                    LogHeader header = LogHeader.For(setup, core);
                    header.SetDrone(spec, spec.Nominal);
                    header.Build = build ?? "";
                    header.WorldId = "floor";
                    header.Input = test.Source = reader.Source;
                    header.InputDevice = test.DeviceName = reader.DeviceName;
                    header.InputIntervals = test.Intervals = reader.Intervals(10000000);
                    header.StartHostUs = startUs;
                    var body = new MemoryStream();
                    var recorder = new FlightRecorder(body, header, FlightRecorder.DefaultCapacity, false);
                    long headerLength = header.Length;
                    probe.Inner = recorder;

                    var inbox = new StickInbox();
                    reader.Inbox = inbox;
                    var scheduler = new FlightScheduler(core, inbox, new PoseBuffer(), clock, probe);
                    scheduler.Start(startUs);
                    Sleep(clock, startUs + steps * FlightCore.StepUs + 5000);
                    scheduler.Stop();
                    reader.Inbox = null;
                    recorder.Close(inbox.Dropped);
                    if (scheduler.Fault != null) throw new InvalidOperationException("the flight thread failed", scheduler.Fault);
                    if (recorder.Fault != null) throw new IOException("the log failed", recorder.Fault);
                    test.Hitches = scheduler.HitchCount;
                    test.Dropped = inbox.Dropped;
                    test.Steps = core.State.Step;

                    test.Analyse(probe, injector.ChangeUs, injector.Stamped);
                    header.InputLatency = test.Figures;
                    byte[] head = header.Encode();
                    log.Write(head, 0, head.Length);
                    log.Write(body.GetBuffer(), (int)headerLength, (int)(body.Length - headerLength));
                    log.Flush();
                }
                finally
                {
                    reader.Stop();
                }
            }
            return test;
        }

        // The pattern at t µs from the flight's start: the throttle stick, the arm switch, and the change it belongs
        // to (−1 before the first). The last, change Changes, ends the timed ones' last hold and is held to the end.
        static double Pattern(long t, int changes, out bool arm, out int change)
        {
            arm = t >= ArmUs;
            change = -1;
            if (t < HoldFromUs) return 0;
            if (t < FirstChangeUs) return Low;
            change = (int)Math.Min((t - FirstChangeUs) / HoldUs, changes);
            return change % 2 == 0 ? High : Low;
        }

        // changeUs holds Changes + 1 stamps: the last one only ends the last change's hold.
        void Analyse(Probe p, long[] changeUs, int stamped)
        {
            int count = Math.Min(stamped - 1, Changes);
            if (count < 0) count = 0;
            FirstStepUs = new long[count];
            FrameWaitUs = new long[count];
            SmoothingUs = new long[count];
            EscUs = new long[count];
            MotorUs = new long[count];
            int n = 0, k = 0;
            for (int i = 0; i < count; i++)
            {
                long t = changeUs[i];
                while (k < p.Count && p.StickUs[k] < t) k++;
                if (k == 0 || k >= p.Count) break;
                int k1 = k, kn = k1;
                while (kn < p.Count && p.StickUs[kn] < changeUs[i + 1]) kn++;
                if (kn == p.Count) break; // the flight ended inside this change's hold

                int kf = (k1 + StepsPerFrame - 1) / StepsPerFrame * StepsPerFrame;
                long frameUs = StartUs + kf * FlightCore.StepUs;
                long command = Crossing(p.Command, k1, kn, CommandAtUs);
                long esc = Crossing(p.Esc, k1, kn, FlightCore.StepUs);
                if (command == long.MinValue || esc == long.MinValue) continue;
                for (int j = k1; j < kn; j++)
                    if (p.Limited[j])
                    {
                        Limited++;
                        break;
                    }
                FirstStepUs[n] = StartUs + k1 * FlightCore.StepUs - t;
                FrameWaitUs[n] = frameUs - (StartUs + k1 * FlightCore.StepUs);
                SmoothingUs[n] = command - frameUs;
                EscUs[n] = esc - command;
                MotorUs[n] = esc - t;
                n++;
            }
            Timed = n;
            Array.Resize(ref FirstStepUs, n);
            Array.Resize(ref FrameWaitUs, n);
            Array.Resize(ref SmoothingUs, n);
            Array.Resize(ref EscUs, n);
            Array.Resize(ref MotorUs, n);
            Figures = new InputLatency { Steps = n, FirstStep = LatencySpread.Of(FirstStepUs, n), Motor = LatencySpread.Of(MotorUs, n) };
            FrameWait = LatencySpread.Of(FrameWaitUs, n);
            Smoothing = LatencySpread.Of(SmoothingUs, n);
            Esc = LatencySpread.Of(EscUs, n);
        }

        // The host µs at which values first passes halfway from the step before k1 to the step before kn, linear between
        // two steps' samples; step k's sample stands at its host time plus atUs. MinValue when it never does.
        long Crossing(double[] values, int k1, int kn, long atUs)
        {
            double from = values[k1 - 1], to = values[kn - 1], half = (from + to) / 2;
            if (to == from) return long.MinValue;
            bool rising = to > from;
            for (int k = k1; k < kn; k++)
            {
                if (rising ? values[k] < half : values[k] > half) continue;
                double f = (half - values[k - 1]) / (values[k] - values[k - 1]);
                return StartUs + (k - 1) * FlightCore.StepUs + atUs + (long)Math.Round(f * FlightCore.StepUs);
            }
            return long.MinValue;
        }

        static void Sleep(IHostClock clock, long untilUs)
        {
            while (clock.NowUs() < untilUs) Thread.Sleep(1);
        }

        // The device boundary: the device's report, arrival stamp and all, with the pattern's values in it.
        sealed class Injector : IRadioDevice
        {
            readonly IRadioDevice _device;
            readonly LatencyTest _test;
            readonly int _throttle, _arm;
            public readonly long[] ChangeUs;
            public int Stamped;

            public Injector(IRadioDevice device, LatencyTest test)
            {
                _device = device ?? throw new ArgumentNullException(nameof(device));
                _test = test;
                var map = new RawStickMap();
                _throttle = map.Throttle;
                _arm = map.Arm;
                ChangeUs = new long[test.Changes + 1];
            }

            public string Name => _device.Name;
            public InputSource Source => _device.Source;
            public RadioAxis[] Axes => _device.Axes;
            public int ButtonCount => _device.ButtonCount;

            public RadioRead Read(RadioReport report, int timeoutMs)
            {
                RadioRead r = _device.Read(report, timeoutMs);
                if (r != RadioRead.Report) return r;
                double throttle = Pattern(report.HostUs - _test.StartUs, _test.Changes, out bool arm, out int change);
                RadioAxis[] axes = _device.Axes;
                for (int i = 0; i < report.Axes.Length && i < axes.Length; i++)
                {
                    RadioAxis a = axes[i];
                    double v = i == _throttle ? throttle : i == _arm ? (arm ? 1 : 0) : 0.5;
                    report.Axes[i] = a.Min + (int)Math.Round(v * ((double)a.Max - a.Min));
                }
                report.Buttons = arm ? 1UL : 0UL;
                while (Stamped <= change && Stamped < ChangeUs.Length) ChangeUs[Stamped++] = report.HostUs; // a skipped change is timed as empty
                return r;
            }

            public void Dispose() => _device.Dispose();
        }

        // On the flight thread: each step's stick stamp, the four corners' mean DShot command and ESC duty, and whether
        // an ESC held its current limit; every step goes on to the log. Preallocated.
        sealed class Probe : IFlightRecorder
        {
            public IFlightRecorder Inner;
            public readonly long[] StickUs;
            public readonly double[] Command, Esc;
            public readonly bool[] Limited;
            public int Count;

            public Probe(int capacity)
            {
                StickUs = new long[capacity];
                Command = new double[capacity];
                Esc = new double[capacity];
                Limited = new bool[capacity];
            }

            public void RecordStep(in FlightState state, in StickSample stick, in StepTrace trace, StepEvents events)
            {
                Inner?.RecordStep(state, stick, trace, events);
                if (Count == StickUs.Length) return;
                StickUs[Count] = stick.HostUs;
                Command[Count] = (trace.RotorCommand[0] + trace.RotorCommand[1] + trace.RotorCommand[2] + trace.RotorCommand[3]) / 4;
                Esc[Count] = (state.EscCommand[0] + state.EscCommand[1] + state.EscCommand[2] + state.EscCommand[3]) / 4;
                Limited[Count] = state.EscLimited != 0;
                Count++;
            }

            public void RecordHitch(in Hitch hitch) => Inner?.RecordHitch(hitch);
        }

        // The app's 1 ms timer period (p6-flight-core 1.2's note), so the flight thread's 1 ms sleeps last about 1 ms.
        static IDisposable FineTimer() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new Period() : null;

        sealed class Period : IDisposable
        {
            [DllImport("winmm.dll")]
            static extern uint timeBeginPeriod(uint period);

            [DllImport("winmm.dll")]
            static extern uint timeEndPeriod(uint period);

            public Period() => timeBeginPeriod(1);

            public void Dispose() => timeEndPeriod(1);
        }
    }
}
