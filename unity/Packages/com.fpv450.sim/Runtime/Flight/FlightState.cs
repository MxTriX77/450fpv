using Fpv450.Sim.Control;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// The integrated state of one flight (K§6.2), in the frames of DetMath.cs. A step is a function of this state,
    /// the stick sample in force, the flight's fixed setup and the world queries, and of nothing else. A value type:
    /// copying it is a snapshot.
    ///
    /// Slots the later tasks fill, so the log layout and the pose have one home:
    /// - this change: the swept-contact latches and the friction anchors (1.6) live in ContactModel's fixed tables, a
    ///   function of the flight so far like the rest of this state; the crash is here
    /// - phase 7: per-leg plastic sink and stick anchors (7a), hooks by element Id (7a), the spool's paid-out length
    ///   and the tether's node positions and velocities (7d)
    /// </summary>
    public struct FlightState
    {
        /// <summary>Steps since the flight started; the state's sim time is Step × FlightCore.Dt.</summary>
        public long Step;

        /// <summary>World, m: the body origin, the arm-plane centre (D-019), not the centre of mass.</summary>
        public Double3 Position;

        /// <summary>World, m/s, of the body origin.</summary>
        public Double3 Velocity;

        /// <summary>Body to world, unit, renormalised every step.</summary>
        public Quat Attitude;

        /// <summary>ω in body axes, rad/s.</summary>
        public Double3 BodyRate;

        /// <summary>The flight controller's armed state, by Betaflight's arming rules (FlightController.Frame); a body
        /// without propulsion follows the stick's arm switch.</summary>
        public bool Armed;

        /// <summary>Rotor speed per corner, rad/s.</summary>
        public Corners RotorSpeed;

        /// <summary>The ESC's command filter state per corner: the duty it drives its motor at, 0–1.</summary>
        public Corners EscCommand;

        /// <summary>State of charge of the two packs, 0–1 (0 is the cells' 3.0 V warning, below which they still run).</summary>
        public double PackCharge0, PackCharge1;

        /// <summary>The flight controller's filters, integrators and latches (FlightController).</summary>
        public FcState Fc;

        /// <summary>The ESCs whose current limit held their motor during the last step, bit 1 &lt;&lt; corner (Propulsion).</summary>
        public byte EscLimited;

        /// <summary>The flight has ended in a crash (ContactModel, 1.6): latched; the body still moves, the app ends the flight.</summary>
        public bool Crashed;
    }
}
