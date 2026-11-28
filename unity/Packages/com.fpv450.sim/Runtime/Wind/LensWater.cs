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
    /// The lens's numbers (w21-rain-on-camera design.md, docs/reference-notes/lens-drops.md), each labelled; every assumed one is
    /// tunable here. Angles in rad, lengths in m.
    /// </summary>
    public sealed class LensSpec
    {
        /// <summary>The front glass's clear radius: 4.5 mm (assumed: an M12 2.1 mm wide-angle front element, 8–10 mm across).</summary>
        public double ClearRadius = 0.0045;

        /// <summary>The barrel's front face, the bluff disc the air meets: 7 mm (assumed: the M12 thread and its front ring).</summary>
        public double BarrelRadius = 0.007;

        /// <summary>The front glass's radius of curvature: 8 mm (assumed: a convex meniscus, its rim tilted 34°); +∞ for flat glass.</summary>
        public double CurvatureRadius = 0.008;

        /// <summary>Water's advancing and receding angles on the glass: 50° and 20° (assumed: weathered glass, static near 35°;
        /// clean glass wets at under 15°, glass in open air rises to 25–40°, published ranges).</summary>
        public double AdvancingAngle = 50 * Deg, RecedingAngle = 20 * Deg;

        /// <summary>The retention's shape factor k: 1 (published, measured 0.98 ± 0.08 for sessile water drops).</summary>
        public double RetentionFactor = 1;

        /// <summary>Water's surface tension, N/m, and viscosity, Pa·s, at 15 °C (published).</summary>
        public double SurfaceTension = 0.0735, WaterViscosity = 1.138e-3;

        /// <summary>Air's kinematic viscosity, m²/s (published, 15–20 °C; DustLaw's).</summary>
        public double AirViscosity = 1.5e-5;

        /// <summary>A drop's drag coefficient on its side silhouette: 1 (assumed; it gives the published depinning thresholds in a
        /// boundary layer and in a jet, lens-drops.md §5).</summary>
        public double DragCoefficient = 1;

        /// <summary>Le Grand et al.'s slope s in Ca = s·(F − F_ret)/(γ·V^⅓): 0.006 (derived from their 0.0067–0.0113, scaled to
        /// water on glass by the contact line's θ/ln(b/a)).</summary>
        public double SlideSlope = 0.006;

        /// <summary>The contact line's ln(b/a): 15 (derived: 1 mm over a water molecule's 0.3 nm).</summary>
        public double ContactLineLog = 15;

        /// <summary>The sliding drop's elongation at most (assumed: Le Grand et al.'s drops reach about 2 at the corner and grow
        /// longer as their tails pearl off).</summary>
        public double MaxElongation = 3;

        /// <summary>Landau–Levich–Derjaguin's 0.94 in the trail's film 0.94·h·Ca^⅔ (published; the drop's height as the meniscus,
        /// assumed).</summary>
        public double FilmCoefficient = 0.94;

        /// <summary>The splashed share We/(We + this) of an impact (published: Brown, Jepsen and Yoon 2008, water).</summary>
        public double SplashWeber = 1e6;

        /// <summary>Laan et al.'s A in the spread D_max/D = Re^⅕·√P/(A + √P) (published, 1.24 ± 0.01).</summary>
        public double SpreadFit = 1.24;

        /// <summary>The smallest drop kept, as its patch radius: 25 µm (numerical: under it the drop has gone into its trail).</summary>
        public double MinRadius = 25e-6;

        const double Deg = 3.141592653589793 / 180;
    }

    /// <summary>
    /// The water on the FPV camera's lens (w21-rain-on-camera 1.1, FEED-13, D-126; design.md): the rain reaching the lens from
    /// the drops' velocity relative to the camera onto the lens's facing at the drone's attitude, less what the airframe shades,
    /// and each drop on the glass held by its adhesion and moved by the air's shear and the specific force in the lens's frame;
    /// merging, trails and shedding. It runs on the poses the flight publishes (PoseBuffer), once a PAL field (20 ms of the
    /// flight's time), on the drawn side or a helper, never in the flight's step, and never feeds back: the flight is the same
    /// with it on or off, and a replay draws the same drops. Deterministic from the flight's seed (its own stream, "lens.rain",
    /// outside the flight's log), allocation-free after its constructor, with at most Capacity drops and TrailPoints a drop.
    /// - **Arriving** (design §3.1–3.2): Marshall–Palmer's drops in 12 bins, carrying the flight's rate, at the air's velocity
    ///   relative to the lens plus their fall; onto 19 facets of the domed glass; a ray back from each facet past the airframe's
    ///   parts (shade) and the props' disks (their blades' hit share). Poisson arrivals by field.
    /// - **An impact** (§3.3) leaves its water less the splash, spread over Laan et al.'s radius (merging what it covers), then a
    ///   cap at the receding angle.
    /// - **A drop** (§3.4–3.8): the specific force g − a_L − ω̇×ρ − ω×(ω×ρ) − 2ω×v and the air's drag over the glass (potential
    ///   flow onto the barrel's face, Homann's and Blasius's sublayers) against its retention k·γ·w·(cos θ_R − cos θ_A): pinned,
    ///   or sliding at Le Grand et al.'s speed, elongating and laying an LLD film past Cox–Voinov's dewetting speed; merging when
    ///   patches touch; shed past the glass's edge or when pulled off.
    /// </summary>
    public sealed class LensWater
    {
        /// <summary>The most drops on the lens at once.</summary>
        public const int Capacity = 96;

        /// <summary>The most trail points a drop keeps (the oldest drop off as it lays more).</summary>
        public const int TrailPoints = 16;

        /// <summary>The draws a field may read from "lens.rain" (field k reads 64k to 64k + 63) and the arrivals a field takes at
        /// most (numerical: the Severe rain's fast flight expects 0.2 a field).</summary>
        public const int DrawsPerField = 64, MaxArrivals = 8;

        /// <summary>The stream's name.</summary>
        public const string Stream = "lens.rain";

        const double Pi = 3.141592653589793;
        const double Homann = 1.312, Blasius = 0.332; // axisymmetric stagnation's f''(0) and the flat plate's (published)
        const double Hit = 0.75;                       // the radius on the blade where a disk's hit share is taken, R (numerical)
        static readonly double[] Edges = { 0.1, 0.2, 0.3, 0.5, 0.7, 1.0, 1.3, 1.7, 2.2, 2.8, 3.5, 4.5, 6.0 }; // mm
        static readonly int[] Rings = { 1, 6, 12 };

        readonly LensSpec _lens;
        readonly Double3 _apex;
        readonly double _rate, _airDensity, _dcos, _tanHalf, _capShape, _frontal, _caStar, _minVolume;
        readonly SeedStream _stream;

        // The rain's bins: number density (m⁻³), diameter bounds (mm), the geometric mid's fall speed (m/s), the mean drop (m³).
        readonly int _bins;
        readonly double[] _binN, _binD0, _binD1, _binFall, _binVolume;
        readonly double _slope; // Marshall–Palmer's Λ, mm⁻¹

        // The dome's facets: the centre (lens m), its normal, its true area, its ring and sector bounds.
        readonly int _facets;
        readonly double[] _fx, _fy, _fArea, _fr0, _fr1, _fp0, _fp1;
        readonly Double3[] _fn;
        readonly double[] _lambda; // the field's cumulative expected arrivals, bin-major

        // The shelter: boxes, solid cylinders, the props' disks and their blade at Hit·R.
        readonly Double3[] _boxLo, _boxHi, _cylC, _cylA;
        readonly double[] _cylR, _cylH;
        readonly Double3[] _hub;
        readonly double _propRadius, _bladeChord, _bladeDepth, _bladeRadius;
        readonly int _blades;

        readonly LensDrop[] _drops = new LensDrop[Capacity];
        readonly LensTrailPoint[] _trail = new LensTrailPoint[Capacity * TrailPoints];
        int _count, _nextId = 1;
        long _field = -1;

        /// <param name="spec">The drone: its camera's lens (CameraPosition), its motors and props (the props' disks shelter).</param>
        /// <param name="airframe">The airframe as built (DroneSpec.BuildMass at the flight's loadout): its parts shelter the lens;
        /// a part of mass 0 is off. Null: no part shelters it.</param>
        /// <param name="rateMmPerHour">The flight's rain rate, mm/h (FeedSignals.RainRateMmPerHour, RainField's draw): 0 when
        /// Sunny, and the lens stays dry.</param>
        /// <param name="seed">The flight's seed (FlightSetup.Seed): the lens's own stream "lens.rain" is drawn from it.</param>
        /// <param name="lens">The lens's numbers; LensSpec's when null.</param>
        public LensWater(DroneSpec spec, MassModel airframe, double rateMmPerHour, ulong seed, LensSpec lens = null)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (!(rateMmPerHour >= 0)) throw new ArgumentOutOfRangeException(nameof(rateMmPerHour));
            _lens = lens ?? new LensSpec();
            if (!(_lens.ClearRadius > 0 && _lens.BarrelRadius > _lens.ClearRadius && _lens.CurvatureRadius > _lens.ClearRadius))
                throw new ArgumentException("the lens's glass must lie inside its barrel and under its curvature", nameof(lens));
            _apex = spec.CameraPosition;
            _rate = rateMmPerHour;
            _airDensity = spec.Aero != null ? spec.Aero.AirDensity : 1.225;
            _stream = new SeedStream(Stream, FlightRandom.StreamKey(seed, Stream));

            DetMath.SinCos(_lens.RecedingAngle, out double sinR, out double cosR);
            _dcos = cosR - DetMath.Cos(_lens.AdvancingAngle);
            _tanHalf = sinR / (1 + cosR);
            _capShape = Pi / 6 * _tanHalf * (3 + _tanHalf * _tanHalf); // V = shape·r³
            _frontal = (_lens.RecedingAngle - sinR * cosR) / (sinR * sinR); // A_f = frontal·r²
            _caStar = _lens.RecedingAngle * _lens.RecedingAngle * _lens.RecedingAngle / (9 * _lens.ContactLineLog);
            _minVolume = _capShape * _lens.MinRadius * _lens.MinRadius * _lens.MinRadius;

            // The rain's bins: Marshall–Palmer's slope, the intercept set so the bins carry the rate (design §3.1).
            _bins = Edges.Length - 1;
            _binN = new double[_bins];
            _binD0 = new double[_bins];
            _binD1 = new double[_bins];
            _binFall = new double[_bins];
            _binVolume = new double[_bins];
            var preset = RainPreset.Of(RainLevel.Rain);
            _slope = rateMmPerHour > 0 ? 4.1 * DetMath.Exp(-0.21 * DetMath.Log(rateMmPerHour)) : 4.1;
            double carried = 0;
            for (int b = 0; b < _bins; b++)
            {
                double d0 = Edges[b], d1 = Edges[b + 1], l = _slope;
                double n = (DetMath.Exp(-l * d0) - DetMath.Exp(-l * d1)) / l;
                double mean = (Cube(d1, l) - Cube(d0, l)) / n; // mm³
                double mid = DetMath.Sqrt(d0 * d1);
                _binD0[b] = d0;
                _binD1[b] = d1;
                _binN[b] = n;
                _binFall[b] = preset.FallA - preset.FallB * DetMath.Exp(-preset.FallC * mid);
                _binVolume[b] = Pi / 6 * mean * 1e-9;
                carried += n * _binFall[b] * _binVolume[b];
            }
            double intercept = rateMmPerHour * RainPreset.MmPerHour / carried;
            for (int b = 0; b < _bins; b++) _binN[b] *= intercept;
            Intercept = intercept;

            // The dome's facets (design §2).
            _facets = 0;
            foreach (int r in Rings) _facets += r;
            _fx = new double[_facets];
            _fy = new double[_facets];
            _fArea = new double[_facets];
            _fr0 = new double[_facets];
            _fr1 = new double[_facets];
            _fp0 = new double[_facets];
            _fp1 = new double[_facets];
            _fn = new Double3[_facets];
            int f = 0;
            for (int ring = 0; ring < Rings.Length; ring++)
                for (int s = 0; s < Rings[ring]; s++, f++)
                {
                    double r0 = _lens.ClearRadius * ring / Rings.Length, r1 = _lens.ClearRadius * (ring + 1) / Rings.Length;
                    double p0 = 2 * Pi * s / Rings[ring], p1 = 2 * Pi * (s + 1) / Rings[ring];
                    double rc = ring > 0 ? DetMath.Sqrt(0.5 * (r0 * r0 + r1 * r1)) : 0;
                    DetMath.SinCos(0.5 * (p0 + p1), out double sp, out double cp);
                    _fx[f] = rc * cp;
                    _fy[f] = rc * sp;
                    _fn[f] = Normal(_fx[f], _fy[f]);
                    _fArea[f] = 0.5 * (r1 * r1 - r0 * r0) * (p1 - p0) / _fn[f].Z;
                    _fr0[f] = r0;
                    _fr1[f] = r1;
                    _fp0[f] = p0;
                    _fp1[f] = p1;
                }
            _lambda = new double[_bins * _facets];

            // The shelter (design §3.2): the parts as built, less those off and the props' mass shapes; the props' disks.
            int boxes = 0, cylinders = 0;
            int parts = airframe != null ? airframe.PartCount : 0;
            for (int i = 0; i < parts; i++)
            {
                ref readonly DronePart p = ref airframe.Part(i);
                if (!Shelters(p)) continue;
                if (p.Kind == PartKind.Box) boxes++;
                else cylinders++;
            }
            _boxLo = new Double3[boxes];
            _boxHi = new Double3[boxes];
            _cylC = new Double3[cylinders];
            _cylA = new Double3[cylinders];
            _cylR = new double[cylinders];
            _cylH = new double[cylinders];
            boxes = cylinders = 0;
            for (int i = 0; i < parts; i++)
            {
                ref readonly DronePart p = ref airframe.Part(i);
                if (!Shelters(p)) continue;
                if (p.Kind == PartKind.Box)
                {
                    _boxLo[boxes] = p.Position - p.Size * 0.5;
                    _boxHi[boxes++] = p.Position + p.Size * 0.5;
                }
                else
                {
                    _cylC[cylinders] = p.Position;
                    _cylA[cylinders] = p.Axis * (1 / p.Axis.Length());
                    _cylR[cylinders] = 0.5 * p.Diameter;
                    _cylH[cylinders++] = 0.5 * p.Length;
                }
            }
            int motors = spec.Motors != null ? spec.Motors.Length : 0;
            _hub = new Double3[motors];
            for (int i = 0; i < motors; i++) _hub[i] = spec.Motors[i].Position;
            _propRadius = 0.5 * spec.Props.Diameter;
            _blades = spec.Props.Blades;
            PropBlade blade = spec.Blade;
            if (blade != null && _propRadius > 0)
            {
                int at = 0;
                for (int i = 1; i < blade.Stations.Length; i++)
                    if (DetMath.Abs(blade.Stations[i] - Hit) < DetMath.Abs(blade.Stations[at] - Hit)) at = i;
                DetMath.SinCos(blade.SectionAngle[at], out double sinBeta, out _);
                _bladeChord = blade.Chord[at];
                _bladeDepth = blade.Chord[at] * sinBeta + blade.Thickness[at];
                _bladeRadius = blade.Stations[at] * _propRadius;
            }
        }

        /// <summary>The lens's numbers.</summary>
        public LensSpec Spec => _lens;

        /// <summary>The front glass's clear radius, m: the lens's X, Y run over the disc of this radius.</summary>
        public double ClearRadius => _lens.ClearRadius;

        /// <summary>The rain spectrum's intercept, m⁻³ mm⁻¹: Marshall–Palmer's shape carrying the flight's rate exactly (design
        /// §3.1; the published 8000 carries it within 15 %).</summary>
        public double Intercept { get; }

        /// <summary>The newest field the lens has stepped to (FieldCamera's index: steps 20k … 20k + 19), −1 before the first.</summary>
        public long Field => _field;

        /// <summary>The drops on the lens after the newest field, in no fixed order (track them by Id).</summary>
        public ReadOnlySpan<LensDrop> Drops => new ReadOnlySpan<LensDrop>(_drops, 0, _count);

        /// <summary>Every drop's trail points, each drop's run at its TrailStart.</summary>
        public ReadOnlySpan<LensTrailPoint> Trail => _trail;

        /// <summary>Drop i's trail points, oldest first.</summary>
        public ReadOnlySpan<LensTrailPoint> TrailOf(int i) => new ReadOnlySpan<LensTrailPoint>(_trail, _drops[i].TrailStart, _drops[i].TrailCount);

        /// <summary>The front glass's sag at lens (x, y), m: how far behind the dome's apex the glass lies there.</summary>
        public double Sag(double x, double y)
        {
            double rc = _lens.CurvatureRadius;
            if (double.IsPositiveInfinity(rc)) return 0;
            return rc - DetMath.Sqrt(rc * rc - x * x - y * y);
        }

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

        /// <summary>The water's books, m³: what the impacts left on the glass (and Place set), what was shed, what went into
        /// trails; with Water, on the glass now, arrived = on the glass + shed + in trails.</summary>
        public double ArrivedWater { get; private set; }
        public double ShedWater { get; private set; }
        public double TrailWater { get; private set; }

        /// <summary>The water on the glass now, m³.</summary>
        public double Water
        {
            get
            {
                double w = 0;
                for (int i = 0; i < _count; i++) w += _drops[i].Volume;
                return w;
            }
        }

        /// <summary>
        /// A drop set on the glass at rest, at lens (x, y) with volume m³ (a test's or a bench's start; the flight's drops come
        /// from the rain); it merges with any it touches at the next field. Its water joins ArrivedWater; Arrived counts rain only.
        /// </summary>
        public int Place(double x, double y, double volume)
        {
            if (!(volume > 0) || !(x * x + y * y < _lens.ClearRadius * _lens.ClearRadius))
                throw new ArgumentException("a drop needs water, on the glass");
            if (_count == Capacity) throw new InvalidOperationException("the lens holds Capacity drops");
            int i = Add(x, y, volume);
            ArrivedWater += volume;
            return _drops[i].Id;
        }

        /// <summary>
        /// Steps to the newest whole field the flight has published, each from the poses at its start, middle and end in the
        /// history. A render that fell behind the history (128 steps) resumes at the newest whole field: the fields between are
        /// skipped (the drops keep their state; each field's own draws stay its own).
        /// </summary>
        public void Advance(PoseBuffer poses)
        {
            long newest = poses.LatestStep;
            long last = FieldCamera.LatestField(newest);
            if (last < 0) return;
            long next = _field + 1;
            if (next * FlightCore.FieldSteps < newest - PoseBuffer.HistorySteps + 1) next = last;
            for (long k = next; k <= last; k++)
            {
                long s = k * FlightCore.FieldSteps;
                if (poses.TryRead(s, out PoseFrame a) && poses.TryRead(s + FlightCore.FieldSteps / 2, out PoseFrame m)
                    && poses.TryRead(s + FlightCore.FieldSteps, out PoseFrame b))
                    Advance(a, m, b);
                else
                    _field = k; // the writer is refilling a slot it needs: the field is lost
            }
        }

        /// <summary>One field: the poses at its first step, its middle and its last (steps 20k, 20k + 10 and 20k + 20).</summary>
        public void Advance(in PoseFrame start, in PoseFrame middle, in PoseFrame end)
        {
            double dt = (end.Step - start.Step) * FlightCore.Dt;
            if (!(dt > 0)) throw new ArgumentException("a field runs forward in time");
            long field = start.Step / FlightCore.FieldSteps;
            Quat q = middle.Attitude;
            Double3 air = middle.AirVelocityAtCamera;
            if (_rate > 0) Arrive(field, q, air, middle.RotorSpeed, dt);
            else
            {
                ArrivalRate = WaterRate = Sheltered = 0;
            }

            // The specific force in the lens's frame (design §3.4): the lens's own acceleration over the field from its velocity
            // at the field's ends, gravity and the rates at the middle.
            Double3 la = LensVelocity(start), lb = LensVelocity(end);
            Double3 accel = q.InverseRotate((lb - la) * (1 / dt));
            Double3 gravity = q.InverseRotate(FlightCore.Gravity);
            Double3 w = middle.BodyRate, wdot = (end.BodyRate - start.BodyRate) * (1 / dt);
            Double3 lens = gravity - accel;
            double un = -air.Z, utx = air.X, uty = air.Y, ut = DetMath.Sqrt(utx * utx + uty * uty);
            double layer = double.PositiveInfinity;
            if (un > 0) layer = DetMath.Sqrt(_lens.AirViscosity * _lens.BarrelRadius * Pi / (2 * un)) / Homann;
            if (ut > 0) layer = DetMath.Min(layer, DetMath.Sqrt(_lens.AirViscosity * _lens.BarrelRadius / ut) / Blasius);
            for (int i = 0; i < _count; i++)
            {
                if (Move(i, lens, w, wdot, un, utx, uty, layer, dt, end.Step)) continue;
                i--; // drop i is gone: the last moved into its slot
            }
            MergeTouching();
            _field = field;
        }

        // The lens's world velocity in a pose: the body's, plus the rotation's at the lens's arm.
        Double3 LensVelocity(in PoseFrame p) => p.Velocity + p.Attitude.Rotate(Double3.Cross(p.BodyRate, _apex));

        // The rain arriving over a field (design §3.1–3.3): the expected arrivals per bin and facet, then the field's draws.
        void Arrive(long field, in Quat q, in Double3 air, in Corners rotors, double dt)
        {
            Double3 down = q.InverseRotate(new Double3(0, -1, 0));
            double total = 0, free = 0, water = 0;
            for (int b = 0; b < _bins; b++)
            {
                Double3 u = air + down * _binFall[b];
                double speed = u.Length();
                Double3 back = speed > 0 ? u * (-1 / speed) : default;
                for (int f = 0; f < _facets; f++)
                {
                    double onto = -Double3.Dot(u, _fn[f]);
                    if (onto > 0)
                    {
                        double expected = _binN[b] * onto * _fArea[f] * dt;
                        double passed = expected * Transmission(f, back, DetMath.Abs(u.Y), rotors);
                        free += expected;
                        total += passed;
                        water += passed * _binVolume[b];
                    }
                    _lambda[b * _facets + f] = total;
                }
            }
            ArrivalRate = total / dt;
            WaterRate = water / dt;
            Sheltered = free > 0 ? 1 - total / free : 0;
            if (!(total > 0)) return;

            // The field's draws, by index (design §3.1): the count by Poisson inversion, then per arrival its facet and bin, its
            // place over the facet and its diameter in the bin.
            ulong at = (ulong)field * DrawsPerField;
            double u0 = Unit(at), p = DetMath.Exp(-total), sum = p;
            int count = 0;
            while (u0 > sum && count < MaxArrivals)
            {
                count++;
                p *= total / count;
                sum += p;
            }
            for (int j = 0; j < count; j++)
            {
                ulong d = at + 1 + 4 * (ulong)j;
                double pick = Unit(d) * total;
                int cell = 0;
                while (cell < _lambda.Length - 1 && _lambda[cell] <= pick) cell++;
                int b = cell / _facets, f = cell - b * _facets;
                double r0 = _fr0[f], r1 = _fr1[f];
                double r = DetMath.Sqrt(r0 * r0 + Unit(d + 1) * (r1 * r1 - r0 * r0));
                DetMath.SinCos(_fp0[f] + Unit(d + 2) * (_fp1[f] - _fp0[f]), out double sp, out double cp);
                double l = _slope, d0 = _binD0[b], d1 = _binD1[b];
                double diameter = d0 - DetMath.Log(1 - Unit(d + 3) * (1 - DetMath.Exp(-l * (d1 - d0)))) / l;
                Double3 u = air + down * _binFall[b];
                Impact(r * cp, r * sp, diameter * 1e-3, -Double3.Dot(u, _fn[f]));
            }
        }

        // One raindrop of diameter D (m) striking the glass at (x, y) at normal speed un (design §3.3).
        void Impact(double x, double y, double diameter, double un)
        {
            LensSpec s = _lens;
            double we = RainPreset.WaterDensity * diameter * un * un / s.SurfaceTension;
            double re = RainPreset.WaterDensity * un * diameter / s.WaterViscosity;
            double volume = Pi / 6 * diameter * diameter * diameter * (1 - we / (we + s.SplashWeber));
            double root = DetMath.Sqrt(we * DetMath.Exp(-0.4 * DetMath.Log(re)));
            double spread = 0.5 * diameter * DetMath.Exp(0.2 * DetMath.Log(re)) * root / (s.SpreadFit + root);
            Arrived++;
            ArrivedWater += volume;
            int keep;
            if (_count < Capacity) keep = Add(x, y, volume);
            else
            {
                // A full lens: the water joins the nearest drop.
                keep = 0;
                double best = double.PositiveInfinity;
                for (int i = 0; i < _count; i++)
                {
                    double dx = _drops[i].X - x, dy = _drops[i].Y - y, d2 = dx * dx + dy * dy;
                    if (d2 < best)
                    {
                        best = d2;
                        keep = i;
                    }
                }
                Combine(keep, x, y, volume, 0, 0);
                Merged++;
                return;
            }
            // The lamella runs over every drop within its spread: they merge into it.
            for (int i = 0; i < _count; i++)
            {
                if (i == keep) continue;
                double dx = _drops[i].X - x, dy = _drops[i].Y - y, reach = spread + _drops[i].Radius;
                if (dx * dx + dy * dy >= reach * reach) continue;
                keep = MergeInto(keep, i);
                i = -1; // the slots moved: look again from the start
            }
        }

        // Moves drop i over the field (design §3.4–3.6, §3.8); false when it is gone (shed or spent into its trail).
        bool Move(int i, in Double3 lens, in Double3 w, in Double3 wdot, double un, double utx, double uty, double layer, double dt,
            long step)
        {
            ref LensDrop d = ref _drops[i];
            LensSpec s = _lens;
            Double3 n = Normal(d.X, d.Y);
            double sag = Sag(d.X, d.Y);
            Double3 rho = new Double3(d.X, d.Y, -sag);
            // The drop's velocity along the glass: its X, Y and the dome's slope under them (the glass at z = −sag).
            double vz = double.IsPositiveInfinity(s.CurvatureRadius) ? 0 : -(d.X * d.VelocityX + d.Y * d.VelocityY) / Root(d.X, d.Y);
            Double3 v = new Double3(d.VelocityX, d.VelocityY, vz);
            Double3 f = lens - Double3.Cross(wdot, rho) - Double3.Cross(w, Double3.Cross(w, rho)) - Double3.Cross(w, v) * 2;
            double fn = Double3.Dot(f, n);
            Double3 ft = f - n * fn;
            double mass = RainPreset.WaterDensity * d.Volume;

            // Pulled off the glass: past the contact line's whole tension.
            if (mass * fn > 2 * Pi * d.Radius * s.SurfaceTension) return Gone(i, false);

            // The air over the glass at the drop (potential flow onto the barrel's face plus the edge-on stream), its sublayer.
            double radial = d.X * d.X + d.Y * d.Y, barrel = s.BarrelRadius;
            double stagnation = un > 0 ? 2 / Pi * un / DetMath.Sqrt(barrel * barrel - radial) : 0;
            Double3 ue = new Double3(utx + stagnation * d.X, uty + stagnation * d.Y, 0);
            ue = ue - n * Double3.Dot(ue, n);
            double x = d.Height / layer, share = x <= 1 ? x * x / 3 : 1 - 2 / (3 * x);
            double drag = 0.5 * _airDensity * s.DragCoefficient * _frontal * d.Radius * d.Radius * share * ue.Length();
            Double3 push = ft * mass + ue * drag;
            double force = push.Length();
            double width = 2 * d.Radius / DetMath.Sqrt(d.Elongation);
            double hold = s.RetentionFactor * s.SurfaceTension * width * _dcos;
            if (force > 0)
            {
                Double3 way = push * (1 / force);
                double planar = DetMath.Sqrt(way.X * way.X + way.Y * way.Y);
                if (planar > 0)
                {
                    d.AxisX = way.X / planar;
                    d.AxisY = way.Y / planar;
                }
            }
            if (!(force > hold))
            {
                d.VelocityX = d.VelocityY = 0;
                d.Elongation = 1;
                d.Sliding = false;
                return true;
            }

            // Sliding (Le Grand et al.'s quasi-steady speed), its elongation, its trail.
            double speed = s.SlideSlope * (force - hold) / (s.WaterViscosity * DetMath.Cbrt(d.Volume));
            Double3 dir = push * (1 / force);
            double path = speed * dt;
            d.X += dir.X * path;
            d.Y += dir.Y * path;
            d.VelocityX = dir.X * speed;
            d.VelocityY = dir.Y * speed;
            d.Sliding = true;
            double ca = s.WaterViscosity * speed / s.SurfaceTension;
            d.Elongation = DetMath.Min(1 + ca / _caStar, s.MaxElongation);
            if (ca > _caStar)
            {
                double film = s.FilmCoefficient * d.Height * DetMath.Exp(2.0 / 3 * DetMath.Log(ca));
                double track = 2 * d.Radius / DetMath.Sqrt(d.Elongation);
                double laid = DetMath.Min(film * track * path, 0.5 * d.Volume);
                d.Volume -= laid;
                TrailWater += laid;
                Shape(ref d);
                double half = d.Radius * DetMath.Sqrt(d.Elongation);
                Lay(i, new LensTrailPoint
                {
                    X = d.X - d.AxisX * half,
                    Y = d.Y - d.AxisY * half,
                    Width = 2 * d.Radius / DetMath.Sqrt(d.Elongation),
                    Film = laid / (track * path),
                    Step = step,
                });
                if (d.Volume < _minVolume) return Gone(i, true);
            }
            if (d.X * d.X + d.Y * d.Y > s.ClearRadius * s.ClearRadius) return Gone(i, false);
            return true;
        }

        // Merges every pair of drops whose patches touch, until none do (design §3.7).
        void MergeTouching()
        {
            for (int pass = 0; pass < Capacity; pass++)
            {
                bool merged = false;
                for (int i = 0; i < _count; i++)
                    for (int j = i + 1; j < _count; j++)
                    {
                        double dx = _drops[j].X - _drops[i].X, dy = _drops[j].Y - _drops[i].Y, reach = _drops[i].Radius + _drops[j].Radius;
                        if (dx * dx + dy * dy >= reach * reach) continue;
                        i = MergeInto(i, j);
                        j = i; // drop i grew and the slots moved: look again from i
                        merged = true;
                    }
                if (!merged) return;
            }
        }

        // Drop gone into drop keep: returns keep's slot after the move.
        int MergeInto(int keep, int gone)
        {
            ref LensDrop g = ref _drops[gone];
            if (g.Volume > _drops[keep].Volume)
            {
                // The larger's identity, axis and trail.
                ref LensDrop k = ref _drops[keep];
                k.Id = g.Id;
                k.AxisX = g.AxisX;
                k.AxisY = g.AxisY;
                Array.Copy(_trail, gone * TrailPoints, _trail, keep * TrailPoints, g.TrailCount);
                k.TrailCount = g.TrailCount;
            }
            Combine(keep, g.X, g.Y, g.Volume, g.VelocityX, g.VelocityY);
            Merged++;
            int last = _count - 1;
            Remove(gone);
            return keep == last ? gone : keep;
        }

        // Adds water (at x, y, moving at vx, vy) into drop i: the volume-weighted place and velocity.
        void Combine(int i, double x, double y, double volume, double vx, double vy)
        {
            ref LensDrop d = ref _drops[i];
            double total = d.Volume + volume, a = d.Volume / total, b = volume / total;
            d.X = a * d.X + b * x;
            d.Y = a * d.Y + b * y;
            d.VelocityX = a * d.VelocityX + b * vx;
            d.VelocityY = a * d.VelocityY + b * vy;
            d.Volume = total;
            Shape(ref d);
        }

        // A new drop at rest; returns its slot.
        int Add(double x, double y, double volume)
        {
            int i = _count++;
            _drops[i] = new LensDrop
            {
                Id = _nextId++,
                X = x,
                Y = y,
                Volume = volume,
                Elongation = 1,
                AxisX = 0,
                AxisY = -1,
                TrailStart = i * TrailPoints,
            };
            Shape(ref _drops[i]);
            return i;
        }

        // Drop i leaves: shed off the glass, or spent into its trail.
        bool Gone(int i, bool spent)
        {
            if (spent) TrailWater += _drops[i].Volume;
            else
            {
                ShedWater += _drops[i].Volume;
                Shed++;
            }
            Remove(i);
            return false;
        }

        // Drop i's slot takes the last drop.
        void Remove(int i)
        {
            int last = --_count;
            if (i == last) return;
            _drops[i] = _drops[last];
            _drops[i].TrailStart = i * TrailPoints;
            Array.Copy(_trail, last * TrailPoints, _trail, i * TrailPoints, _drops[i].TrailCount);
        }

        // Appends a trail point to drop i's, the oldest dropping off when it holds TrailPoints.
        void Lay(int i, in LensTrailPoint point)
        {
            ref LensDrop d = ref _drops[i];
            int start = i * TrailPoints;
            if (d.TrailCount == TrailPoints)
            {
                Array.Copy(_trail, start + 1, _trail, start, TrailPoints - 1);
                d.TrailCount--;
            }
            _trail[start + d.TrailCount++] = point;
        }

        // The cap at the receding angle for the drop's volume.
        void Shape(ref LensDrop d)
        {
            d.Radius = DetMath.Cbrt(d.Volume / _capShape);
            d.Height = d.Radius * _tanHalf;
        }

        // The glass's outward normal at lens (x, y), camera axes.
        Double3 Normal(double x, double y)
        {
            double rc = _lens.CurvatureRadius;
            if (double.IsPositiveInfinity(rc)) return new Double3(0, 0, 1);
            return new Double3(x / rc, y / rc, Root(x, y) / rc);
        }

        double Root(double x, double y) => DetMath.Sqrt(_lens.CurvatureRadius * _lens.CurvatureRadius - x * x - y * y);

        // The share of a facet's drops arriving from back (unit, toward where they come from) that the airframe lets through:
        // 0 past a solid part; through each prop disk, 1 less its blades' hit share for drops crossing at axial (m/s).
        double Transmission(int f, in Double3 back, double axial, in Corners rotors)
        {
            Double3 o = _apex + new Double3(_fx[f], _fy[f], -Sag(_fx[f], _fy[f]));
            for (int i = 0; i < _boxLo.Length; i++)
                if (RayBox(o, back, _boxLo[i], _boxHi[i])) return 0;
            for (int i = 0; i < _cylC.Length; i++)
                if (RayCylinder(o, back, _cylC[i], _cylA[i], _cylR[i], _cylH[i])) return 0;
            double t = 1;
            if (_bladeRadius > 0 && back.Y != 0)
                for (int c = 0; c < _hub.Length; c++)
                {
                    double along = (_hub[c].Y - o.Y) / back.Y;
                    if (!(along > 0)) continue;
                    double dx = o.X + back.X * along - _hub[c].X, dz = o.Z + back.Z * along - _hub[c].Z;
                    if (dx * dx + dz * dz >= _propRadius * _propRadius) continue;
                    double hit = axial > 0 ? _blades / (2 * Pi) * (_bladeChord / _bladeRadius + rotors[c] * _bladeDepth / axial) : 1;
                    t *= 1 - DetMath.Min(hit, 1);
                }
            return t;
        }

        const double Ahead = 1e-9; // m: a hit must lie past the ray's start (numerical)

        static bool RayBox(in Double3 o, in Double3 d, in Double3 lo, in Double3 hi)
        {
            double near = Ahead, far = double.PositiveInfinity;
            return Slab(o.X, d.X, lo.X, hi.X, ref near, ref far) && Slab(o.Y, d.Y, lo.Y, hi.Y, ref near, ref far)
                && Slab(o.Z, d.Z, lo.Z, hi.Z, ref near, ref far);
        }

        static bool Slab(double o, double d, double lo, double hi, ref double near, ref double far)
        {
            if (d == 0) return o >= lo && o <= hi;
            double a = (lo - o) / d, b = (hi - o) / d;
            if (a > b) (a, b) = (b, a);
            if (a > near) near = a;
            if (b < far) far = b;
            return near <= far;
        }

        static bool RayCylinder(in Double3 o, in Double3 d, in Double3 c, in Double3 axis, double radius, double half)
        {
            Double3 oc = o - c;
            double da = Double3.Dot(d, axis), oa = Double3.Dot(oc, axis);
            Double3 dp = d - axis * da, op = oc - axis * oa;
            double a = Double3.Dot(dp, dp), b = 2 * Double3.Dot(op, dp), e = Double3.Dot(op, op) - radius * radius;
            if (a > 0)
            {
                double disc = b * b - 4 * a * e;
                if (disc >= 0)
                {
                    double root = DetMath.Sqrt(disc);
                    double t1 = (-b - root) / (2 * a), t2 = (-b + root) / (2 * a);
                    if (t1 > Ahead && DetMath.Abs(oa + t1 * da) <= half) return true;
                    if (t2 > Ahead && DetMath.Abs(oa + t2 * da) <= half) return true;
                }
            }
            if (da != 0)
                for (int side = -1; side <= 1; side += 2)
                {
                    double t = (side * half - oa) / da;
                    if (!(t > Ahead)) continue;
                    Double3 q = op + dp * t;
                    if (Double3.Dot(q, q) <= radius * radius) return true;
                }
            return false;
        }

        static bool Shelters(in DronePart p) =>
            p.Mass > 0 && !(p.Name != null && p.Name.StartsWith("prop_", StringComparison.Ordinal));

        // Draw i of the lens's stream, uniform in [0, 1).
        double Unit(ulong i) => (_stream.BitsAt(i) >> 11) * (1.0 / 9007199254740992.0);

        // −e^(−λD)·(D³/λ + 3D²/λ² + 6D/λ³ + 6/λ⁴): ∫D³·e^(−λD) dD.
        static double Cube(double d, double l) =>
            -DetMath.Exp(-l * d) * (d * d * d / l + 3 * d * d / (l * l) + 6 * d / (l * l * l) + 6 / (l * l * l * l));
    }
}
