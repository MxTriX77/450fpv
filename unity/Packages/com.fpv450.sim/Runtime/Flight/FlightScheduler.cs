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
    /// hitch: a thread late by no more than the cap (below) changes when a step runs, never what it computes.
    ///
    /// Timing (p7h-step-budget 1.1): it times each step's Advance on the same clock into Times, which only the log's end
    /// and the player's line read; timing never feeds the state.
    ///
    /// The lag cap (p7h-step-budget 2.1, design 2 and 3): a step about to start more than MaxOwedUs after its host time moves
    /// the time base (StartUs) later, so that it starts exactly MaxOwedUs late. The backlog past the cap is dropped, never
    /// run: no step is skipped and every step is still 1 ms, but the sticks are read at the moved host times, so no step
    /// reads a stick older than the cap. Each move is a Drop hitch; the poses and the sticks' hold follow it.
    ///
    /// Waiting: the thread sleeps 1 ms while more than 2 ms remain and yields otherwise, so it holds a core near 1 kHz.
    /// Tests and drivers without a thread call Begin and then RunDue with their own clock.
    /// </summary>
    public sealed class FlightScheduler
    {
        /// <summary>
        /// B, the most a step may start after its host time, µs: one PAL field (FlightCore.FieldSteps, 20 ms; derived from
        /// the pilot's control loop, which closes through the feed, design 2 of p7h-step-budget). Never tuned.
        /// </summary>
        public const long MaxOwedUs = FlightCore.FieldSteps * FlightCore.StepUs;

        readonly FlightCore _core;
        readonly StickInbox _sticks;
        readonly PoseBuffer _poses;
        readonly IHostClock _clock;
        readonly IFlightRecorder _recorder;
        readonly StepTimes _times;
        Thread _thread;
        volatile bool _stop;
        long _startUs;
        long _hitches;
        bool _begun;

        /// <summary>times: the flight's step timer, kept across the schedulers of one flight (its pauses); a new one when null.</summary>
        public FlightScheduler(FlightCore core, StickInbox sticks, PoseBuffer poses, IHostClock clock, IFlightRecorder recorder = null, StepTimes times = null)
        {
            _core = core ?? throw new ArgumentNullException(nameof(core));
            _sticks = sticks ?? throw new ArgumentNullException(nameof(sticks));
            _poses = poses ?? throw new ArgumentNullException(nameof(poses));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _recorder = recorder;
            _times = times ?? new StepTimes();
        }

        /// <summary>HostClock µs of step 0 on the current time base: Begin's, moved later by every drop.</summary>
        public long StartUs => _startUs;

        /// <summary>Each step's Advance on the host clock; read it once the flight thread has stopped.</summary>
        public StepTimes Times => _times;

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
        /// make a hitch. A step that would start more than MaxOwedUs late first moves the time base (a drop), so a pass
        /// owes at most MaxOwedUs when it begins, and each later step of it is capped again as it starts, on the clock.
        /// </summary>
        public long RunDue(long nowUs)
        {
            long done = _core.State.Step;
            if (nowUs < StepHostUs(done)) return 0;
            Cap(done, nowUs);
            long due = (nowUs - _startUs) / FlightCore.StepUs + 1;
            long pending = due - done;
            long passUs = 0;
            if (pending >= 2)
            {
                passUs = _clock.NowUs();
                var hitch = new Hitch { Step = done, Steps = pending, LateUs = nowUs - StepHostUs(done) };
                Volatile.Write(ref _hitches, _hitches + 1);
                _recorder?.RecordHitch(hitch);
            }
            // Stop ends a pass between steps: a late thread's catch-up never holds up the pilot's end, pause or restart.
            for (long k = done; k < due && !_stop; k++)
            {
                if (k > done) Cap(k, nowUs + _clock.NowUs() - passUs); // the pass's own time so far
                _sticks.Consume(StepHostUs(k), out StickSample stick);
                long t0 = _clock.NowUs();
                _core.Advance(stick);
                _times.Add(_clock.NowUs() - t0);
                _recorder?.RecordStep(_core.State, stick, _core.Trace, _core.Events);
                _core.GetPose(out PoseFrame pose);
                _core.GetSignals(out FeedSignals signals);
                _poses.Publish(pose, signals);
            }
            return pending;
        }

        // Step `step` starts at host time atUs. Past MaxOwedUs late, the time base moves later by the excess so that it
        // starts exactly MaxOwedUs late: the excess is dropped (a Drop hitch), and the poses and the sticks' hold follow.
        void Cap(long step, long atUs)
        {
            long lateUs = atUs - StepHostUs(step);
            if (lateUs <= MaxOwedUs) return;
            long dropUs = lateUs - MaxOwedUs;
            _startUs += dropUs;
            _poses.MoveStart(_startUs);
            _sticks.MoveHold(dropUs);
            var hitch = new Hitch { Step = step, LateUs = lateUs, DroppedUs = dropUs };
            Volatile.Write(ref _hitches, _hitches + 1);
            _recorder?.RecordHitch(hitch);
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

        /// <summary>Stops the flight thread and waits for it: at most the step it is in, never the rest of a catch-up pass.</summary>
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
