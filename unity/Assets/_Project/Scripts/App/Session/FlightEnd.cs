using Fpv450.Sim;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;

namespace Fpv450.App.Session
{
    /// <summary>Why a flight ended (APP-04). The fiber's ends come with p7d. A disarm on the switch ends nothing (D-062).</summary>
    public enum FlightEndKind : byte
    {
        /// <summary>Still flying.</summary>
        None = 0,

        /// <summary>An impact over its threshold (the core's crash: ContactModel's acceleration or prop strike).</summary>
        Crash = 1,

        /// <summary>Upside down on the ground: the body's up axis below the horizon while touching, for UpsideDownSteps.</summary>
        UpsideDown = 2,

        /// <summary>A pack's charge reached the battery cut-off.</summary>
        BatteryCutoff = 3,

        /// <summary>The controller disarmed on the radio's loss (INP-05).</summary>
        Failsafe = 4,

        /// <summary>The pilot ended it: End flight from the pause, restart, quit or back to the menu. Never found in a
        /// log, which then simply ends.</summary>
        Pilot = 6,

        /// <summary>The flight thread failed (FlightSession.Fault): a bug.</summary>
        Fault = 7,
    }

    /// <summary>The end and the step it came at: the state's step count after the step that ended the flight.</summary>
    public struct FlightEnd
    {
        public FlightEndKind Kind;
        public long Step;

        public bool Ended => Kind != FlightEndKind.None;
        public double Seconds => Step * FlightCore.Dt;
    }

    /// <summary>
    /// The end conditions (APP-04), one rule for the live flight and for its log: the session runs it on every step as
    /// the flight thread hands it to the log, and PostFlight runs it over the log's records, so the summary names the
    /// end the flight stopped on. A function of the step records only (the state, the stick in force, the trace);
    /// allocation-free. In order, the first that holds:
    /// - a crash: FlightState.Crashed, the core's latch
    /// - upside down on the ground: the body's up axis below the horizon while any contact touches, held UpsideDownSteps
    ///   (assumed: a tumble that rights itself does not end the flight)
    /// - the failsafe: armed in the step before and not now, the stick in force the input layer's failsafe sample. A
    ///   disarm on the switch ends nothing (D-062): the drone stays under physics, and the switch re-arms it
    /// - the battery cut-off: either pack's charge at or under BatteryCutoffCharge. Charge 0 is the end of drone.json's
    ///   OCV table, 3.0 V a cell open-circuit (the flight controller's warning); the model holds the voltage there, so
    ///   past it the pack would never empty (assumed; the cells' published 2.5 V cut-off lies below the table)
    /// </summary>
    public sealed class FlightEndRule
    {
        public const long UpsideDownSteps = 1000;
        public const double BatteryCutoffCharge = 0;

        bool _wasArmed;
        long _upsideFrom = -1;

        public FlightEnd End { get; private set; }

        /// <summary>One step's record; true once the flight has ended (End then holds the end, which never changes).</summary>
        public bool Step(in FlightState s, in StickSample stick, in StepTrace trace)
        {
            if (End.Ended) return true;
            FlightEndKind kind = FlightEndKind.None;
            bool upsideDown = s.Attitude.Rotate(new Double3(0, 1, 0)).Y < 0 && trace.ContactsTouching > 0;
            if (!upsideDown) _upsideFrom = -1;
            else if (_upsideFrom < 0) _upsideFrom = s.Step;

            if (s.Crashed) kind = FlightEndKind.Crash;
            else if (upsideDown && s.Step - _upsideFrom >= UpsideDownSteps) kind = FlightEndKind.UpsideDown;
            else if (_wasArmed && !s.Armed && stick.Failsafe) kind = FlightEndKind.Failsafe;
            else if (s.PackCharge0 <= BatteryCutoffCharge || s.PackCharge1 <= BatteryCutoffCharge) kind = FlightEndKind.BatteryCutoff;
            _wasArmed = s.Armed;
            if (kind == FlightEndKind.None) return false;
            End = new FlightEnd { Kind = kind, Step = s.Step };
            return true;
        }
    }
}
