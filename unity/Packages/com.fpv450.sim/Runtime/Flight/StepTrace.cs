using System;

namespace Fpv450.Sim.Flight
{
    /// <summary>
    /// What a step computed on its way to the new state that is not itself integrated state, for the log (design 10,
    /// LOG-01): the quantities a crash is explained by. FlightCore fills it every step. A slot stays zero until the
    /// task that computes it fills it: the rotor commands and the packs' voltage and current (1.4). The net force and
    /// moment are the sums the rigid body integrated.
    /// </summary>
    public struct StepTrace
    {
        /// <summary>The mixer's command per corner after saturation, 0–1 (1.4).</summary>
        public Corners RotorCommand;

        /// <summary>Each pack's terminal voltage, V (1.4).</summary>
        public double PackVolts0, PackVolts1;

        /// <summary>The current drawn from each pack, A (1.4).</summary>
        public double PackAmps0, PackAmps1;

        /// <summary>The net force on the body, its weight included, world, N.</summary>
        public Double3 NetForce;

        /// <summary>The net moment about the centre of mass, body axes, N·m.</summary>
        public Double3 NetMoment;
    }

    /// <summary>
    /// The kinds of a step's events (design 10, LOG-01). Fixed: a kind is never renumbered, and a new mechanism appends
    /// its kinds (phase 7: hooks, pitfalls, VRS, payout, the tether's break). The producers arrive with their mechanisms:
    /// mixer saturation (1.4); contacts, prop strikes, crashes and tunnels (1.6); the scheduler and the log write the
    /// hitches. FlightEvent says what each field holds per kind; a field a kind does not use keeps its "none" value.
    /// </summary>
    public enum EventKind : ushort
    {
        None = 0,

        /// <summary>Part touches (Object, Shape) of Material or Surface at Point. A: the closing speed along the normal, m/s.</summary>
        ContactMade = 1,

        /// <summary>Part leaves (Object, Shape). A: how long the contact lasted, s; B: its impulse, N·s.</summary>
        ContactBroken = 2,

        /// <summary>Index's prop disc is entered by (Object, Shape) at Point. A: the speed of the entry, m/s.</summary>
        PropStrike = 3,

        /// <summary>The mixer clipped and Index's axis (MixerAxis) lost authority. A: the share of its demand lost, 0–1.</summary>
        MixerSaturation = 4,

        /// <summary>A crash of Part on (Object, Shape) at Point. A: the speed, m/s; B: the impulse, N·s; C: the peak acceleration, m/s².</summary>
        Crash = 5,

        /// <summary>Part ended the step through (Object, Shape) at Point: a physics bug, never pilot error. A: the depth, m.</summary>
        Tunnel = 6,

        /// <summary>A late step (Index: HitchCause). A: the steps run to catch up, or the waits for the log; B: how late, µs.</summary>
        Hitch = 7,

        /// <summary>A step made more events than StepEvents holds: a bug in a producer. A: how many were lost.</summary>
        EventsLost = 8,
    }

    /// <summary>MixerSaturation's Index.</summary>
    public enum MixerAxis : byte { Roll = 0, Pitch = 1, Yaw = 2, Collective = 3 }

    /// <summary>Hitch's Index.</summary>
    public enum HitchCause : byte
    {
        /// <summary>The scheduler found two or more steps due (FlightScheduler, PLT-04).</summary>
        Late = 1,

        /// <summary>The log's ring was full, so the flight thread waited for the flush worker (FlightRecorder).</summary>
        LogFull = 2,
    }

    /// <summary>
    /// One event of a step (design 10): fixed fields, whose meaning per kind EventKind gives. The log stamps it with the
    /// step that made it, so a producer sets everything but the step.
    /// </summary>
    public struct FlightEvent
    {
        public const ushort NoPart = 0xFFFF;
        public const byte NoIndex = 0xFF;

        public EventKind Kind;

        /// <summary>The drone part, an index into the mass model's parts (the log header lists them); NoPart when none.</summary>
        public ushort Part;

        /// <summary>A corner, an axis or a cause, by kind; NoIndex when none.</summary>
        public byte Index;

        /// <summary>The world object (WorldIds.Ground for the ground) and its shape, as StaticContacts names them.</summary>
        public int Object, Shape;

        /// <summary>The world's material and surface ids (the log header names them).</summary>
        public ushort Material;
        public byte Surface;

        /// <summary>World, m.</summary>
        public Double3 Point;

        /// <summary>The kind's numbers, in the units EventKind gives.</summary>
        public double A, B, C;

        /// <summary>An event of this kind with every other field at its "none" value.</summary>
        public static FlightEvent Of(EventKind kind) => new FlightEvent { Kind = kind, Part = NoPart, Index = NoIndex };
    }

    /// <summary>
    /// The events one step made, in the order made: preallocated, cleared at each step's start. A step that makes more
    /// than the capacity keeps the first ones and counts the rest in Lost, which the log records as an event.
    /// </summary>
    public sealed class StepEvents
    {
        readonly FlightEvent[] _items;

        public StepEvents(int capacity = 64)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new FlightEvent[capacity];
        }

        public int Count { get; private set; }

        /// <summary>Events refused this step because the buffer was full.</summary>
        public int Lost { get; private set; }

        public ref readonly FlightEvent this[int i]
        {
            get
            {
                if ((uint)i >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(i));
                return ref _items[i];
            }
        }

        public void Add(in FlightEvent e)
        {
            if (Count == _items.Length)
            {
                Lost++;
                return;
            }
            _items[Count++] = e;
        }

        public void Clear()
        {
            Count = 0;
            Lost = 0;
        }
    }
}
