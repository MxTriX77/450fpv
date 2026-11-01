using System;
using System.Threading;
using Fpv450.Sim.Control;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// Runs the flight in real time on its own thread (design 2, PHB-01, PLT-04), and is the only code that reads the
    /// host clock. Step k is due at StepHostUs(k) = StartUs + k ms, the instant its stick input is complete; it then
    /// consumes the sample in force at that instant, advances 1 ms, hands the state to the recorder and publishes the
    /// pose. A pass that finds two or more steps due runs them all, back to back, as real 1 ms steps and logs one
    /// hitch: a late thread changes when a step runs, never what it computes.
    ///
    /// Waiting: the thread sleeps 1 ms while more than 2 ms remain and yields otherwise, so it holds a core near 1 kHz.
    /// Tests and drivers without a thread call Begin and then RunDue with their own clock.
    /// </summary>
    public sealed class FlightScheduler
    {
        readonly FlightCore _core;
        readonly StickInbox _sticks;
        readonly PoseBuffer _poses;
        readonly IHostClock _clock;
        readonly IFlightRecorder _recorder;
        Thread _thread;
        volatile bool _stop;
        long _startUs;
        long _hitches;
        bool _begun;

        public FlightScheduler(FlightCore core, StickInbox sticks, PoseBuffer poses, IHostClock clock, IFlightRecorder recorder = null)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _sticks = sticks ?? throw new ArgumentNullException(nameof(sticks));
            _poses = poses ?? throw new ArgumentNullException(nameof(poses));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _recorder = recorder;
        }

        /// <summary>HostClock µs of step 0.</summary>
        public long StartUs => _startUs;

        /// <summary>Hitches logged so far.</summary>
        public long HitchCount => Volatile.Read(ref _hitches);

        /// <summary>The exception that stopped the flight thread, if one did.</summary>
        public Exception Fault { get; private set; }

        /// <summary>Host time of step k: it may run once the clock reaches this, and consumes the sample in force then.</summary>
        public long StepHostUs(long step) => _startUs + step * FlightCore.StepUs;

        /// <summary>Fixes the start time and publishes the initial pose. Once per flight.</summary>
        public void Begin(long startUs)
        {
            if (_begun) throw new InvalidOperationException("the flight has already begun");
            _begun = true;
            _startUs = startUs;
            _core.GetPose(out PoseFrame pose);
            _core.GetSignals(out FeedSignals signals);
            _poses.Begin(startUs, pose, signals);
        }

        /// <summary>
        /// One scheduler pass at host time nowUs: runs every step due by then and returns how many it ran. Two or more
        /// make a hitch.
        /// </summary>
        public long RunDue(long nowUs)
        {
            long done = _core.State.Step;
            if (nowUs < StepHostUs(done)) return 0;
            long due = (nowUs - _startUs) / FlightCore.StepUs + 1;
            long pending = due - done;
            if (pending >= 2)
            {
                var hitch = new Hitch { Step = done, Steps = pending, LateUs = nowUs - StepHostUs(done) };
                Volatile.Write(ref _hitches, _hitches + 1);
                _recorder?.RecordHitch(hitch);
            }
            for (long k = done; k < due; k++)
            {
                _sticks.Consume(StepHostUs(k), out StickSample stick);
                _core.Advance(stick);
                _recorder?.RecordStep(_core.State, stick, _core.Trace, _core.Events);
                _core.GetPose(out PoseFrame pose);
                _core.GetSignals(out FeedSignals signals);
                _poses.Publish(pose, signals);
            }
            return pending;
        }

        /// <summary>Begins now and runs the flight on its own thread.</summary>
        public void Start() => Start(_clock.NowUs());

        /// <summary>Begins at startUs (now or later) and runs the flight on its own thread.</summary>
        public void Start(long startUs)
        {
            Begin(startUs);
            _thread = new Thread(Run) { Name = "fpv450 flight", IsBackground = true, Priority = ThreadPriority.Highest };
            _thread.Start();
        }

        /// <summary>Stops the flight thread and waits for it.</summary>
        public void Stop()
        {
            _stop = true;
            _thread?.Join();
            _thread = null;
        }

        void Run()
        {
            try
            {
                while (!_stop)
                {
                    RunDue(_clock.NowUs());
                    long wait = StepHostUs(_core.State.Step) - _clock.NowUs();
                    if (wait > 2 * FlightCore.StepUs)
                        Thread.Sleep(1);
                    else if (wait > 0)
                        Thread.Yield();
                }
            }
            catch (Exception e)
            {
                Fault = e;
            }
        }
    }
}
