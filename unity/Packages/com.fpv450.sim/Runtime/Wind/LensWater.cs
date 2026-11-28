using System;
using Fpv450.Sim.Flight;

namespace Fpv450.Sim.Wind
{
    /// <summary>
    /// One drop on the FPV camera's lens at the end of a PAL field (w21-rain-on-camera 1.1, FEED-13, D-126), as the feed draws it.
    /// - **The lens's coordinates:** X and Y are metres from the lens's optical axis, in the camera's axes (the body's: 0° uptilt),
    ///   +X to the right and +Y up as the pilot sees the picture, measured in the plane across the axis (the front glass's dome is
    ///   below them: LensWater.Sag). A drop at (0, 0) sits on the axis; the glass ends at LensWater.ClearRadius.
    /// - **Its size:** the water's Volume, and the contact patch on the glass: an ellipse of equal-area radius Radius, Elongation
    ///   times as long (along Axis) as it is wide, under a spherical cap of apex Height (the refracting surface).
    /// - **Its motion:** Velocity on the lens; Sliding while the forces on it beat its adhesion this field (it then lays Trail
    ///   points behind it). The long axis is the way it was last pushed.
    /// - **Its identity:** Id is unique over the flight; a merged drop keeps the larger one's.
    /// </summary>
    public struct LensDrop
    {
        /// <summary>Unique over the flight, never reused: the drawn side tracks a drop from field to field by it. A merged drop
        /// keeps the larger one's Id; a new arrival takes the next.</summary>
        public int Id;

        /// <summary>The contact patch's centre, lens m (see the struct's summary).</summary>
        public double X, Y;

        /// <summary>The water in the drop, m³.</summary>
        public double Volume;

        /// <summary>The contact patch's equal-area radius, m: the patch is π·Radius² m² whatever its Elongation.</summary>
        public double Radius;

        /// <summary>The cap's apex over the glass, m (with Radius, the cap's curvature: the drop's refraction).</summary>
        public double Height;

        /// <summary>The contact patch's length over its width, 1 or more (1: round, at rest).</summary>
        public double Elongation;

        /// <summary>The patch's long axis, a unit vector in the lens's X, Y: the way the drop was last pushed.</summary>
        public double AxisX, AxisY;

        /// <summary>The drop's velocity over the glass this field, lens m/s (0 while it is pinned).</summary>
        public double VelocityX, VelocityY;

        /// <summary>The forces on it beat its adhesion this field: it moves, and lays trail points while it does.</summary>
        public bool Sliding;

        /// <summary>The trail's points in LensWater.Trail, from TrailStart, TrailCount of them, oldest first (the newest at the
        /// drop's rear): LensWater.TrailOf(i) reads them.</summary>
        public int TrailStart, TrailCount;
    }

    /// <summary>
    /// One point of a drop's trail (w21-rain-on-camera 1.1): where the drop passed and the water it left there, a film the
    /// drop's receding edge lays when it slides faster than the edge can dewet (LensWater's design §3.6).
    /// </summary>
    public struct LensTrailPoint
    {
        /// <summary>Where the drop's rear passed, lens m.</summary>
        public double X, Y;

        /// <summary>The wetted track's width there, m: the drop's width across its way as it passed.</summary>
        public double Width;

        /// <summary>The film's mean thickness there, m (the water left over the track's area); 0 for a track the edge dewetted.</summary>
        public double Film;

        /// <summary>The flight step it was laid at (the field's last).</summary>
        public long Step;
    }

    /// <summary>
    /// The water on the FPV camera's lens (w21-rain-on-camera 1.1, FEED-13, D-126; design.md): the rain reaching the lens from
    /// the drops' velocity relative to the camera onto the lens's facing at the drone's attitude, less what the airframe shades,
    /// and each drop on the glass held by its adhesion and moved by the air's shear and the specific force in the lens's frame;
    /// merging, trails and shedding. It runs on the poses the flight publishes (PoseBuffer), once a PAL field (20 ms of the
    /// flight's time), on the drawn side or a helper, never in the flight's step, and never feeds back: the flight is the same
    /// with it on or off, and a replay draws the same drops. Deterministic from the flight's seed (its own stream, "lens.rain",
    /// outside the flight's log), allocation-free after its constructor, with at most Capacity drops and TrailPoints a drop.
    /// </summary>
    public sealed class LensWater
    {
        /// <summary>The most drops on the lens at once.</summary>
        public const int Capacity = 96;

        /// <summary>The most trail points a drop keeps (the oldest drop off as it lays more).</summary>
        public const int TrailPoints = 16;

        readonly LensDrop[] _drops = new LensDrop[Capacity];
        readonly LensTrailPoint[] _trail = new LensTrailPoint[Capacity * TrailPoints];
        int _count = 0;
        long _field = -1;

        /// <param name="spec">The drone: its camera's lens (CameraPosition), its motors and props (the props' disks shelter).</param>
        /// <param name="airframe">The airframe as built (DroneSpec.BuildMass at the flight's loadout): its parts shelter the lens;
        /// a part of mass 0 is off.</param>
        /// <param name="rateMmPerHour">The flight's rain rate, mm/h (FeedSignals.RainRateMmPerHour, RainField's draw): 0 when
        /// Sunny, and the lens stays dry.</param>
        /// <param name="seed">The flight's seed (FlightSetup.Seed): the lens's own stream "lens.rain" is drawn from it.</param>
        public LensWater(DroneSpec spec, MassModel airframe, double rateMmPerHour, ulong seed) =>
            throw new NotImplementedException("w21-rain-on-camera 1.1");

        /// <summary>The front glass's clear radius, m: the lens's X, Y run over the disc of this radius.</summary>
        public double ClearRadius { get; }

        /// <summary>The newest field the lens has stepped to (FieldCamera's index: steps 20k … 20k + 19), −1 before the first.</summary>
        public long Field => _field;

        /// <summary>The drops on the lens after the newest field, in no fixed order (track them by Id).</summary>
        public ReadOnlySpan<LensDrop> Drops => new ReadOnlySpan<LensDrop>(_drops, 0, _count);

        /// <summary>Every drop's trail points, each drop's run at its TrailStart.</summary>
        public ReadOnlySpan<LensTrailPoint> Trail => _trail;

        /// <summary>Drop i's trail points, oldest first.</summary>
        public ReadOnlySpan<LensTrailPoint> TrailOf(int i) => new ReadOnlySpan<LensTrailPoint>(_trail, _drops[i].TrailStart, _drops[i].TrailCount);

        /// <summary>The front glass's sag at lens (x, y), m: how far behind the dome's apex the glass lies there.</summary>
        public double Sag(double x, double y) => throw new NotImplementedException("w21-rain-on-camera 1.1");

        /// <summary>The rain reaching the glass over the newest field, as the law gives it before the drops are drawn: drops a
        /// second and their water, m³/s.</summary>
        public double ArrivalRate { get; private set; }
        public double WaterRate { get; private set; }

        /// <summary>The share of the rain heading for the glass over the newest field that the airframe (its parts and the
        /// props' disks) took first, 0–1.</summary>
        public double Sheltered { get; private set; }

        /// <summary>The flight's drops so far: arrived on the glass, merged into another, and shed (off the glass's edge or
        /// pulled off it).</summary>
        public int Arrived { get; private set; }
        public int Merged { get; private set; }
        public int Shed { get; private set; }

        /// <summary>
        /// Steps to the newest whole field the flight has published, each from the poses at its start, middle and end in the
        /// history. A render that fell behind the history (128 steps) resumes at the newest whole field: the fields between are
        /// skipped (the drops keep their state; each field's own draws stay its own).
        /// </summary>
        public void Advance(PoseBuffer poses) => throw new NotImplementedException("w21-rain-on-camera 1.1");

        /// <summary>One field: the poses at its first step, its middle and its last (steps 20k, 20k + 10 and 20k + 20).</summary>
        public void Advance(in PoseFrame start, in PoseFrame middle, in PoseFrame end) =>
            throw new NotImplementedException("w21-rain-on-camera 1.1");
    }
}
