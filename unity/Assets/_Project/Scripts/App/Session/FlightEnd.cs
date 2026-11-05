using Fpv450.Sim;
using Fpv450.Sim.Contact;
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

        /// <summary>The fiber broke (p7d-tether 1.8): the log's TetherBreak, its mechanism and its place. The link is lost
        /// with it, so Betaflight's failsafe follows (FailsafeStage).</summary>
        FiberBreak = 5,

        /// <summary>A stage of the flight controller's failsafe on the lost link (the log's Failsafe events, Betaflight 4.5):
        /// the signal lost, stage 1, stage 2, the disarm.</summary>
        FailsafeStage = 6,
    }

    /// <summary>An incident and the step it came at: the state's step count after the step that met it.</summary>
    public struct FlightIncident
    {
        public FlightIncidentKind Kind;
        public long Step;

        /// <summary>The log's event, for a fiber break and a failsafe stage.</summary>
        public LoggedEvent Event;

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

    /// <summary>A leg's damage in the log (p7g-legs 1.5), found by PostFlight: its state at the flight's last step record and
    /// the event of its worst state, with the landing or the object and the speed. None ends the flight; a new flight's legs
    /// are intact.</summary>
    public struct DamagedLeg
    {
        /// <summary>The leg's corner (Corners order: rear right, front right, rear left, front left).</summary>
        public int Corner;

        /// <summary>Its state at the end (the step record's leg_state, its elastic bow left out), and its set there (body
        /// rad: x and z its bend, y its twist).</summary>
        public LegFlags State;
        public Double3 Set;

        /// <summary>The first event of its worst state: LegDetached, LegBroken, SkidLoose, LegCracked, then LegSet; and the
        /// names of what it met there, the object ("the ground" for a landing) and its material or surface ("" for none).</summary>
        public LoggedEvent Cause;
        public string Object, Material;
    }
}
