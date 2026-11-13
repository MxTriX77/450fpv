using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>A value of the flight's state that is not finite, thrown by FlightCore's step guard in the step it appears, naming
    /// the quantity and the part of the step that made it (w9-village-physics 2.12's addendum).</summary>
    public sealed class NonFiniteStateException : InvalidOperationException
    {
        public NonFiniteStateException(string what, string part, long step)
            : base($"the {what} is not finite after the {part} in step {step}")
        {
            What = what;
            Part = part;
            Step = step;
        }

        /// <summary>The quantity, the part of the step and the step.</summary>
        public string What { get; }
        public string Part { get; }
        public long Step { get; }
    }
}
