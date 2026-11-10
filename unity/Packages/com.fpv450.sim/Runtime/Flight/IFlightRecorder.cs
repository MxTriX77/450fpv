using Fpv450.Sim.Control;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// A late scheduler pass (PLT-04): the first pending step started LateUs after its due time, and Steps steps were
    /// run back to back to catch up. Or a drop (DroppedUs over 0, p7h-step-budget 2.1): step Step was about to start
    /// LateUs late, past FlightScheduler.MaxOwedUs, so every later step's host time moved DroppedUs later. Hitches change
    /// no state; only the log sees them.
    /// </summary>
    public struct Hitch
    {
        /// <summary>The first late step's index.</summary>
        public long Step;

        /// <summary>Steps run in the catch-up pass, 2 or more; 0 for a drop.</summary>
        public long Steps;

        /// <summary>How late the first of them started, µs of host time; a drop's, before it.</summary>
        public long LateUs;

        /// <summary>A drop's move of the time base, µs (LateUs − MaxOwedUs); 0 for a late pass.</summary>
        public long DroppedUs;
    }

    /// <summary>
    /// Where the scheduler hands every step and hitch, on the flight thread. The log (Log.FlightRecorder) implements it
    /// with its ring buffer; a test implements it with a state dump. An implementation must not allocate, and waits
    /// only when its buffer is full, which it logs as a hitch.
    /// </summary>
    public interface IFlightRecorder
    {
        /// <summary>
        /// After each step: the new state, the stick sample that was in force during it, what the step computed beside
        /// the state, and the events it made (FlightCore.Trace and Events, valid until the next step).
        /// </summary>
        void RecordStep(in FlightState state, in StickSample stick, in StepTrace trace, StepEvents events);

        void RecordHitch(in Hitch hitch);
    }
}
