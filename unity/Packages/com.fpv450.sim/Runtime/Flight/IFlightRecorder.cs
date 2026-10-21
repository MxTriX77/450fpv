using Fpv450.Sim.Control;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// A late scheduler pass (PLT-04): the first pending step started LateUs after its due time, and Steps steps were
    /// run back to back to catch up. Hitches change no state; only the log sees them.
    /// </summary>
    public struct Hitch
    {
        /// <summary>The first late step's index.</summary>
        public long Step;

        /// <summary>Steps run in the catch-up pass, 2 or more.</summary>
        public long Steps;

        /// <summary>How late the first of them started, µs of host time.</summary>
        public long LateUs;
    }

    /// <summary>
    /// Where the scheduler hands every step and hitch, on the flight thread. Task 1.2's log implements it with its
    /// ring buffer; a test implements it with a state dump. An implementation must not block or allocate.
    /// </summary>
    public interface IFlightRecorder
    {
        /// <summary>After each step: the new state and the stick sample that was in force during it.</summary>
        void RecordStep(in FlightState state, in StickSample stick);

        void RecordHitch(in Hitch hitch);
    }
}
