using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;

namespace Fpv450.App.Session
{
    /// <summary>Why a flight ended (APP-04). Only the pilot ends one (D-063): nothing the drone meets does, not even a
    /// crash or the failsafe (FlightIncidentKind).</summary>
    public enum FlightEndKind : byte
    {
        /// <summary>Still flying.</summary>
        None = 0,

        /// <summary>The pilot ended it: End flight from the pause, restart, quit or back to the menu. Never found in a
        /// log, which then simply ends.</summary>
        Pilot = 6,

        /// <summary>The flight thread failed (FlightSession.Fault): a bug.</summary>
        Fault = 7,
    }

    /// <summary>The end and the step it came at: the state's step count after the flight's last step.</summary>
    public struct FlightEnd
    {
        public FlightEndKind Kind;
        public long Step;

        public bool Ended => Kind != FlightEndKind.None;
        public double Seconds => Step * FlightCore.Dt;
    }

    /// <summary>What a flight met (APP-04, D-063), found by PostFlight in its log. None ends the flight: the drone stays
    /// under physics, and the summary lists them when the pilot ends it. The fiber's come with p7d.</summary>
    public enum FlightIncidentKind : byte
    {
        /// <summary>An impact over its threshold (the core's crash: ContactModel's acceleration; a blade's strike is damage).</summary>
        Crash = 1,

        /// <summary>Upside down on the ground: the body's up axis below the horizon while touching, for
        /// PostFlight.UpsideDownSteps.</summary>
        UpsideDown = 2,

        /// <summary>A pack's charge reached the battery cut-off.</summary>
        BatteryCutoff = 3,

        /// <summary>The radio was lost (INP-05): the input layer's failsafe, which disarms an armed drone.</summary>
        Failsafe = 4,
    }

    /// <summary>An incident and the step it came at: the state's step count after the step that met it.</summary>
    public struct FlightIncident
    {
        public FlightIncidentKind Kind;
        public long Step;

        public double Seconds => Step * FlightCore.Dt;
    }

    /// <summary>A prop's damage in the log (D-079), found by PostFlight: one blade's damage events (BladeDamaged), or one
    /// rotor's jams (RotorJammed, Slot JamSlot). None ends the flight; a new flight's props are whole.</summary>
    public struct PropDamage
    {
        public const int JamSlot = 16;

        /// <summary>The blade (0–15, the event's index), or JamSlot for the rotor's jams.</summary>
        public int Slot;

        /// <summary>The last of them, and how many there were.</summary>
        public LoggedEvent Last;
        public int Count;

        /// <summary>The blade's mass lost over its damage events, kg (each event's B); 0 for the jams.</summary>
        public double MassLost;
    }
}
