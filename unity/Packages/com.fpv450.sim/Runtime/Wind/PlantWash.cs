using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Wind
{
    /// <summary>The dust the wash lifts at one place (DustLaw): the wall jet's friction velocity u* there and the surface's threshold
    /// u*_t (m/s; +∞ where none lifts: a covered surface, saturated ground, no ground), whether it lifts, the saltation flux that
    /// knocks the dust loose (kg/m/s, White's) and the surface (IWorldQuery's id). Film: the surface's loose layer is a thin film
    /// (a track, a slab, rubble): short whiffs, then clean (its supply is not modelled).</summary>
    public struct DustSample
    {
        public double Friction, Threshold, Flux;
        public bool Lifts, Film;
        public byte Surface;
    }

    /// <summary>
    /// The dust the props' wash lifts (w16-plants-in-the-air design.md §5; plants-in-the-air.md §5): a threshold on the wall jet's
    /// friction velocity per surface. Dust is knocked loose by saltating sand-sized grains, so its threshold is the saltation
    /// threshold (Kok et al. 2012, published); the surfaces' dry thresholds and their labels are the note's §5.3.
    /// - **u\*** from the wall jet's peak speed u_m along the ground and its height y_m: the larger of the smooth wall's
    ///   u_m·√(c_f/2), c_f = 0.06·(u_m·δ/ν)^−0.3·(r/H)^−0.16, δ = y_m/0.165 (Poreh et al. 1967 and Verhoff's profile, published)
    ///   and the fully rough wall's u_m/(5.75·log₁₀(y_m/k_s) + 8.5) (Nikuradse, published; k_s per surface, assumed): the
    ///   transitional wall lies between and tends to the larger.
    /// - **Wet ground** raises the threshold by Fécan et al.'s √(1 + 1.21·(w − w′)^0.68), w′ = 0.0014c² + 0.17c (published; w the
    ///   gravimetric water %, the wet set's water content over the surface's dry bulk density; c the clay %, assumed per surface);
    ///   saturated ground lifts none (assumed: no loose grains). Covered surfaces (sod, weeds, straw, litter) shelter the soil and
    ///   lift none (published in kind, Kok et al. 2012): their chaff is the cut pieces' (1.4).
    /// - **The flux** past the threshold, Q = 2.61·(ρ/g)·u*³·(1 − r²)·(1 + r), r = u*_t/u* (White 1979, recalled).
    /// </summary>
    public static class DustLaw
    {
        /// <summary>Air's kinematic viscosity, m²/s (published: 15–20 °C).</summary>
        public const double Viscosity = 1.5e-5;

        /// <summary>The bulk density of the loose film on a paved surface, kg/m³: 1300 (assumed, a loose silt's), for its wetting.</summary>
        public const double FilmBulkDensity = 1300;

        /// <summary>White's constant (published, recalled).</summary>
        public const double White = 2.61;

        /// <summary>One surface's dust: its dry threshold u*_t (m/s), its wall roughness k_s (m), its clay share (%), whether its
        /// loose layer is a film.</summary>
        public struct Row
        {
            public string Name;
            public double Threshold, Roughness, Clay;
            public bool Film;
        }

        /// <summary>The surface table's bare surfaces (plants-in-the-air.md §5.3; the thresholds the bands' middles): crater spoil
        /// 0.14–0.25 and the track's loose film 0.19–0.25 (published, Sharratt and Vaddella 2012's loose loams), the belt's firm
        /// bare loam 0.20–0.35 (assumed between them and a fallow field), ash on burnt ground 0.13–0.38 (published for volcanic
        /// ash), tilled loam 0.35–0.40 (published, a fallow silt loam in place), a crusted field 0.5–1.0 (assumed between
        /// disturbed crusts and loose loam, Belnap and Gillette 1997), rubble's fines 0.25–0.5 and the films on asphalt and slabs
        /// 0.2–0.3 (assumed). k_s 2 mm on soils, 0.5 mm on paving (assumed: rolled soil, the note's §5.2); clay 20 % on loams, 10 %
        /// on spoil-free fines (assumed, Fécan's 10–30 %).</summary>
        public static bool Find(string surface, out Row row)
        {
            switch (surface)
            {
                case "crater_spoil": row = new Row { Name = surface, Threshold = 0.19, Roughness = 0.002, Clay = 20 }; return true;
                case "dirt_track": row = new Row { Name = surface, Threshold = 0.22, Roughness = 0.002, Clay = 20, Film = true }; return true;
                case "belt_bare": row = new Row { Name = surface, Threshold = 0.27, Roughness = 0.002, Clay = 20 }; return true;
                case "burnt_field": row = new Row { Name = surface, Threshold = 0.25, Roughness = 0.002, Clay = 20 }; return true;
                case "tilled": row = new Row { Name = surface, Threshold = 0.375, Roughness = 0.002, Clay = 20 }; return true;
                case "dry_crust": row = new Row { Name = surface, Threshold = 0.75, Roughness = 0.002, Clay = 20 }; return true;
                case "rubble": row = new Row { Name = surface, Threshold = 0.37, Roughness = 0.002, Clay = 10, Film = true }; return true;
                case "asphalt": row = new Row { Name = surface, Threshold = 0.25, Roughness = 0.0005, Clay = 10, Film = true }; return true;
                case "concrete_slab": row = new Row { Name = surface, Threshold = 0.25, Roughness = 0.0005, Clay = 10, Film = true }; return true;
                default: row = default; return false; // covered, or not a surface of the table
            }
        }

        /// <summary>A surface's threshold u*_t (m/s) at the flight's moisture, from its row and the surface table (the wet set's
        /// water content over the surface's dry bulk density); +∞ for a surface with no row (covered), or saturated.</summary>
        public static double Threshold(string surface, Moisture moisture, SurfaceTable table, out Row row)
        {
            if (!Find(surface, out row) || moisture == Moisture.Saturated) return double.PositiveInfinity;
            if (moisture == Moisture.Dry) return row.Threshold;
            double bulk = 0;
            foreach (SurfaceRow s in table.Surfaces)
                if (s.Name == surface) bulk = s.Dry.BulkDensity;
            if (!(bulk > 0)) bulk = FilmBulkDensity; // a paved surface: its loose film's
            double water = 100 * table.Wetting.WaterContentWet * table.WaterDensity / bulk;
            double wp = 0.0014 * row.Clay * row.Clay + 0.17 * row.Clay;
            if (!(water > wp)) return row.Threshold;
            return row.Threshold * DetMath.Sqrt(1 + 1.21 * DetMath.Exp(0.68 * DetMath.Log(water - wp)));
        }

        /// <summary>u* (m/s) of a wall jet whose peak speed along the ground is peak (m/s) at height (m), radius r from where it lands
        /// and H its source's distance (m), over a wall of roughness k_s (m).</summary>
        public static double Friction(double peak, double height, double radius, double source, double roughness)
        {
            if (!(peak > 0) || !(height > 0)) return 0;
            double delta = height / 0.165;
            double ratio = radius > 1e-3 && source > 0 ? radius / source : 1;
            double cf = 0.06 * DetMath.Exp(-0.3 * DetMath.Log(peak * delta / Viscosity)) * DetMath.Exp(-0.16 * DetMath.Log(ratio));
            double smooth = peak * DetMath.Sqrt(0.5 * cf);
            if (!(height > roughness)) return smooth;
            double rough = peak / (5.75 * DetMath.Log(height / roughness) / 2.302585092994046 + 8.5);
            return rough > smooth ? rough : smooth;
        }

        /// <summary>White's saltation flux, kg/m/s, at u* over a threshold (0 at or under it).</summary>
        public static double Flux(double friction, double threshold, double airDensity)
        {
            if (!(friction > threshold)) return 0;
            double r = threshold / friction;
            return White * airDensity / PlantLaw.Gravity * friction * friction * friction * (1 - r * r) * (1 + r);
        }
    }

    /// <summary>
    /// The props' wash on the ground's plants and dust (w16-plants-in-the-air design.md §4–§5): a world-anchored grid of nodes
    /// around the drone, each a stand of stems of every band the drawn side asks for, integrated in the rotors' air at a fixed
    /// tick of the flight's time. It runs on the drawn side's frame or a helper, never in the flight's step; it reads the poses
    /// the flight publishes (PoseBuffer), so a replay drives it the same way.
    /// - **The air:** the line's law (RotorAir, p7d-tether design 19: the near columns, the merged jet, the wall jets, the fountain
    ///   and the ground vortex) in an instance of its own, begun each tick with the line's inputs from the pose: the hubs, the
    ///   rotors' mean thrust, the field's air at each hub less its velocity, the ground under each. The drone's body over the
    ///   fountain is left out (it shapes only the air within a body's depth under it, where stems meet the legs: 1.5's).
    /// - **Each node** samples the air on its vertical at Heights over its ground (the near jets' top-hat edge straddled), and each
    ///   band's stand steps by PlantLaw (Profile: each element at its own height, the drag on the air relative to it), from its
    ///   natural lean (PropStems' mean, its way by the node's world hash). The wash's bend is the stand's lean less its rest.
    /// - **The window** follows the drone by whole cells; a node entering it starts at rest, one leaving it is dropped (what it
    ///   held is lost: the window's size bounds the trail a fast low pass leaves; design.md §4.4).
    /// - **Dust** per node from its lowest samples (DustLaw), for the surface under it at the flight's moisture.
    /// Deterministic (DetMath only, a fixed tick, the world's own hash), allocation-free after the constructor.
    /// </summary>
    public sealed class PlantWash
    {

        /// <summary>A wash weaker than this where it lands (m/s: the merged jet's landing speed or a near column's) moves no plant
        /// visibly (published: no visible change in a crop under 3 m/s at the canopy, the note's [21]): the tick skips the air (the
        /// stands relax in still air), from about 12 m up for this drone's hover.</summary>
        public const double MinWash = 3;

        /// <summary>The stands' steps a tick (numerical: 20 ms each at the 40 ms tick, the air held over the tick; a culm's fast
        /// swing back after a gust stays within 0.02 rad of the full law at 20 ms, 0.08 at 40, PlantAirTests).</summary>
        public const int SubSteps = 2;

        /// <summary>The highest sample read for dust, m (numerical: above the wall jet's peak at any hover of interest).</summary>
        const double DustTop = 0.1;

        readonly RotorAir _air;
        readonly PlantAir _wind;
        readonly IWorldQuery _world;
        readonly Double3[] _motors;
        readonly PlantMechanics[] _bands;
        // The heights the air is sampled at over a node's ground, m: fine at the wall (the wall jet's peak lies at 0.165·δ,
        // 1–6 cm here), the near jets' top-hat edge h_j straddled by ±2 %, then to the tallest drawn stems (numerical); and each
        // band's rest.
        readonly double[] _heights, _rest;
        readonly int[] _bandHeights;
        readonly int _size, _tickSteps, _heightCount;
        readonly double _cell, _dt, _density, _jetOrigin, _wallRatio, _wallExponent;

        // Per slot (size² of them): the world cell held, its ground and surface, its stand's natural way, its air (Heights) this
        // tick, whether its air is nil, its dust; per slot and band the stand's state now and at the tick before.
        readonly int[] _ci, _cj;
        readonly bool[] _held, _still;
        readonly double[] _ground, _wayX, _wayZ, _friction, _flux, _threshold;
        readonly byte[] _surface;
        readonly Double3[] _samples;
        readonly PlantState[] _now, _was;

        // The surfaces' dust (by IWorldQuery's id).
        readonly double[] _surfaceThreshold = new double[256], _surfaceRoughness = new double[256];
        readonly bool[] _surfaceFilm = new bool[256];

        // The tick's inputs.
        readonly Double3[] _hub = new Double3[4], _relative = new Double3[4];
        readonly double[] _thrust = new double[4], _hubGround = new double[4];
        readonly XZ[] _xz = new XZ[4], _one = new XZ[1];
        readonly GroundSample[] _hubSample = new GroundSample[4], _oneSample = new GroundSample[1];
        long _tick = -1;
        int _startI, _startJ;
        bool _on;
        double _sourceDistance;
        Double3 _centre;

        /// <param name="table">The line's table (its rotor_air rows and air density).</param>
        /// <param name="radius">The props' radius, m.</param>
        /// <param name="motors">The hubs in the body frame (drone.json's motors), Corners' order.</param>
        /// <param name="bands">The stems the drawn side draws (PlantMechanics.Stem), integrated at every node.</param>
        /// <param name="moisture">The flight's ground moisture (SurfaceTable.MoistureOf its rain).</param>
        /// <param name="size">Nodes a side (even).</param>
        /// <param name="cell">The nodes' spacing, m.</param>
        /// <param name="tickSteps">The tick, flight steps (40: 25 Hz; the drawn side reads between the last two ticks).</param>
        public PlantWash(TetherTable table, double radius, Double3[] motors, IWorldQuery world, PlantAir wind, PlantMechanics[] bands,
            SurfaceTable surfaces, Moisture moisture, int size = 32, double cell = 0.15, int tickSteps = 40)
        {
            if (motors == null || motors.Length != 4) throw new ArgumentException("four motors", nameof(motors));
            if (size < 4 || size % 2 != 0 || !(cell > 0) || tickSteps < 1) throw new ArgumentException("the grid is out of its bounds");
            _air = new RotorAir(table, radius);
            _wind = wind ?? throw new ArgumentNullException(nameof(wind));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _motors = (Double3[])motors.Clone();
            _bands = (PlantMechanics[])bands.Clone();
            _size = size;
            _cell = cell;
            _tickSteps = tickSteps;
            _dt = tickSteps * FlightCore.Dt;
            _density = table.Number("fiber.air_density_kg_m3");
            _jetOrigin = table.Number("rotor_air.far_jet_virtual_origin_over_motor_distance");
            _wallRatio = table.Number("rotor_air.wall_jet_half_height_ratio");
            _wallExponent = table.Number("rotor_air.wall_jet_half_height_exponent");
            double hj = _air.JetThickness, top = DustTop;
            foreach (PlantMechanics b in _bands) top = DetMath.Max(top, b.DragTo);
            _heights = new[] { 0.008, 0.02, 0.98 * hj, 1.02 * hj, 0.08, 0.15, 0.27, 0.45, 0.7, 1.0, 1.4, 1.9 };
            Array.Sort(_heights);
            int count = 0;
            while (count < _heights.Length && (count == 0 || _heights[count - 1] < top)) count++;
            _heightCount = count;
            _bandHeights = new int[_bands.Length];
            _rest = new double[_bands.Length];
            for (int b = 0; b < _bands.Length; b++)
            {
                int k = 0;
                while (k < _heightCount && (k == 0 || _heights[k - 1] < _bands[b].DragTo)) k++;
                _bandHeights[b] = k;
                _rest[b] = PlantLaw.Rest(_bands[b], _bands[b].NaturalLean);
            }
            int n = size * size;
            _ci = new int[n];
            _cj = new int[n];
            _held = new bool[n];
            _still = new bool[n];
            _ground = new double[n];
            _wayX = new double[n];
            _wayZ = new double[n];
            _friction = new double[n];
            _flux = new double[n];
            _threshold = new double[n];
            _surface = new byte[n];
            _samples = new Double3[n * _heightCount];
            _now = new PlantState[n * _bands.Length];
            _was = new PlantState[n * _bands.Length];
            for (int s = 0; s < 256; s++) _surfaceThreshold[s] = double.PositiveInfinity;
            for (int s = 1; s < 256; s++)
            {
                string name = world.SurfaceName((byte)s);
                if (name == null) continue;
                _surfaceThreshold[s] = DustLaw.Threshold(name, moisture, surfaces, out DustLaw.Row row);
                _surfaceRoughness[s] = row.Roughness;
                _surfaceFilm[s] = row.Film;
            }
        }

        /// <summary>The bands' count, and the flight step of the last tick (−1 before the first).</summary>
        public int Bands => _bands.Length;
        public long LastTick => _tick;

        /// <summary>The tick's length, s.</summary>
        public double Tick => _dt;

        /// <summary>The heights the air is sampled at, m over a node's ground (the first count of them in use).</summary>
        public ReadOnlySpan<double> Heights => new ReadOnlySpan<double>(_heights, 0, _heightCount);

        /// <summary>The wash's own instance of the line's law, as the last tick began it.</summary>
        public RotorAir Rotors => _air;

        /// <summary>
        /// Ticks to the newest step the flight has published (every tickSteps steps of its time), each from the pose of its own
        /// step in the history. A render that fell behind the history (128 steps) resumes at the newest tick: the steps between
        /// are skipped (the stands keep their state).
        /// </summary>
        public void Advance(PoseBuffer poses)
        {
            long newest = poses.LatestStep;
            if (newest < 0) return;
            long next = _tick < 0 ? newest - newest % _tickSteps : _tick + _tickSteps;
            if (newest - next >= PoseBuffer.HistorySteps) next = newest - newest % _tickSteps;
            for (; next <= newest; next += _tickSteps)
            {
                if (!poses.TryRead(next, out PoseFrame pose)) continue;
                Advance(pose);
            }
        }

        /// <summary>One tick at the pose's step (the caller keeps the ticks tickSteps apart).</summary>
        public void Advance(in PoseFrame pose)
        {
            double t = pose.Step * FlightCore.Dt;
            Quat q = pose.Attitude;
            Double3 up = q.Rotate(new Double3(0, 1, 0));
            for (int i = 0; i < 4; i++)
            {
                _hub[i] = pose.Position + q.Rotate(_motors[i]);
                _xz[i] = new XZ(_hub[i].X, _hub[i].Z);
            }
            _world.SampleGround(_xz, _hubSample);
            for (int i = 0; i < 4; i++)
            {
                double g = _hubSample[i].Height;
                _hubGround[i] = g;
                Double3 v = pose.Velocity + q.Rotate(Double3.Cross(pose.BodyRate, _motors[i]));
                _relative[i] = (g > double.NegativeInfinity ? _wind.AirAt(_hub[i], g, t) : default) - v;
                _thrust[i] = pose.RotorThrust[i];
            }
            _air.Begin(_hub, up, _thrust, _relative, _hubGround);
            double strength = _air.FarLands ? _air.FarGroundSpeed * _air.FarLandingShare : 0, columns = 0;
            for (int i = 0; i < 4; i++) columns = DetMath.Max(columns, 2 * _air.Induced(i) * _air.NearShare);
            _on = strength > MinWash || columns > MinWash;
            double mean = 0;
            for (int i = 0; i < 4; i++) mean += 0.25 * (_hub[i].Y - _hubGround[i]);
            _sourceDistance = _air.FarLands ? mean - _jetOrigin * _air.MotorDistance : mean;
            _centre = _air.FarLands ? _air.FarLanding : pose.Position;
            Recentre(pose.Position);
            for (int s = 0; s < _size * _size; s++) Node(s);
            _tick = pose.Step;
        }

        // The window by whole cells about the drone: a cell newly in it takes a free slot, its ground and its stands at rest.
        void Recentre(in Double3 position)
        {
            int ci = Floor(position.X / _cell), cj = Floor(position.Z / _cell), half = _size / 2;
            _startI = ci - half;
            _startJ = cj - half;
            for (int a = 0; a < _size; a++)
                for (int b = 0; b < _size; b++)
                {
                    int wi = _startI + a, wj = _startJ + b, s = Slot(wi, wj);
                    if (_held[s] && _ci[s] == wi && _cj[s] == wj) continue;
                    Enter(s, wi, wj);
                }
        }

        void Enter(int s, int wi, int wj)
        {
            _held[s] = true;
            _ci[s] = wi;
            _cj[s] = wj;
            _one[0] = new XZ((wi + 0.5) * _cell, (wj + 0.5) * _cell);
            _world.SampleGround(_one, _oneSample);
            _ground[s] = _oneSample[0].Height;
            _surface[s] = _oneSample[0].Surface;
            _threshold[s] = _ground[s] > double.NegativeInfinity ? _surfaceThreshold[_surface[s]] : double.PositiveInfinity;
            // The stand's natural way by the cell's world hash (world-derived, as PropStems' stems).
            ulong h = FlightRandom.Mix64(unchecked(((ulong)(uint)wi << 32) ^ (uint)wj ^ 0x9E3779B97F4A7C15UL));
            DetMath.SinCos(PlantLaw.TwoPi * ((h >> 11) * (1.0 / 9007199254740992.0)), out double sin, out double cos);
            _wayX[s] = cos;
            _wayZ[s] = sin;
            _still[s] = false;
            for (int k = 0; k < _heightCount; k++) _samples[s * _heightCount + k] = default;
            for (int b = 0; b < _bands.Length; b++)
            {
                var rest = new PlantState { X = _rest[b] * cos, Z = _rest[b] * sin };
                _now[s * _bands.Length + b] = rest;
                _was[s * _bands.Length + b] = rest;
            }
            _friction[s] = 0;
            _flux[s] = 0;
        }

        // One node's tick: its air, each band's stand, its dust.
        void Node(int s)
        {
            double ground = _ground[s];
            bool hasGround = ground > double.NegativeInfinity;
            int k0 = s * _heightCount;
            if (_on && hasGround)
            {
                double x = (_ci[s] + 0.5) * _cell, z = (_cj[s] + 0.5) * _cell;
                for (int k = 0; k < _heightCount; k++) _samples[k0 + k] = _air.At(new Double3(x, ground + _heights[k], z), ground, out _);
                _still[s] = false;
            }
            else if (!_still[s])
            {
                for (int k = 0; k < _heightCount; k++) _samples[k0 + k] = default;
                _still[s] = true;
            }
            if (!_still[s]) Array.Copy(_samples, k0, _scratch, 0, _heightCount); // the step reads the node's block from 0
            for (int b = 0; b < _bands.Length; b++)
            {
                int i = s * _bands.Length + b;
                PlantState st = _now[i];
                _was[i] = st;
                if (!hasGround) continue;
                double nx = _bands[b].NaturalLean * _wayX[s], nz = _bands[b].NaturalLean * _wayZ[s];
                if (_still[s])
                {
                    double rx = _rest[b] * _wayX[s], rz = _rest[b] * _wayZ[s];
                    double dx = st.X - rx, dz = st.Z - rz;
                    if (dx * dx + dz * dz < 1e-14 && st.RateX * st.RateX + st.RateZ * st.RateZ < 1e-12) continue; // at rest
                }
                for (int sub = 0; sub < SubSteps; sub++)
                {
                    if (_still[s]) PlantLaw.Step(_bands[b], ref st, nx, nz, default(Double3), _dt / SubSteps);
                    else PlantLaw.Step(_bands[b], ref st, nx, nz, _heights, _scratch, _bandHeights[b], _dt / SubSteps);
                }
                _now[i] = st;
            }
            Dust(s);
        }

        readonly Double3[] _scratch = new Double3[16];

        // The node's u* from its lowest samples' peak speed along the ground, at the wall jet's own peak height 0.165·δ: the merged
        // jet's δ = c·R·(R/H)^e, R = max(r, r_i) (RotorAir's law, the table's rows) where it carries the landing (its share ≥ ½),
        // else the near jets' top-hat, its thickness taken as its δ (assumed).
        void Dust(int s)
        {
            _friction[s] = 0;
            _flux[s] = 0;
            if (_still[s] || !(_threshold[s] < double.PositiveInfinity)) return;
            double peak = 0;
            int k0 = s * _heightCount;
            for (int k = 0; k < _heightCount && _heights[k] <= DustTop; k++)
            {
                Double3 u = _samples[k0 + k];
                double h = DetMath.Sqrt(u.X * u.X + u.Z * u.Z);
                if (h > peak) peak = h;
            }
            double x = (_ci[s] + 0.5) * _cell - _centre.X, z = (_cj[s] + 0.5) * _cell - _centre.Z, r = DetMath.Sqrt(x * x + z * z);
            double height = 0.165 * _air.JetThickness;
            if (_air.FarLands && _air.FarLandingShare >= 0.5)
            {
                double big = r > _air.FarStart ? r : _air.FarStart;
                height = 0.165 * _wallRatio * big * DetMath.Exp(_wallExponent * DetMath.Log(big / _sourceDistance));
            }
            double f = DustLaw.Friction(peak, height, r, _sourceDistance, _surfaceRoughness[_surface[s]]);
            _friction[s] = f;
            _flux[s] = DustLaw.Flux(f, _threshold[s], _density);
        }

        static int Floor(double v)
        {
            int t = (int)v;
            return t > v ? t - 1 : t;
        }

        int Slot(int wi, int wj)
        {
            int a = wi % _size, b = wj % _size;
            if (a < 0) a += _size;
            if (b < 0) b += _size;
            return a + _size * b;
        }

        // The node at world cell (wi, wj) when the window holds it, else −1.
        int Held(int wi, int wj)
        {
            if (wi < _startI || wi >= _startI + _size || wj < _startJ || wj >= _startJ + _size) return -1;
            int s = Slot(wi, wj);
            return _held[s] && _ci[s] == wi && _cj[s] == wj ? s : -1;
        }

        /// <summary>
        /// A band's wash bend at world (x, z): its stand's lean less its rest, between the last two ticks at fraction (0 the tick
        /// before, 1 the last), bilinear between the four nodes about it, and the wash's air at the band's load point (world m/s,
        /// for the wind's linearisation, PlantAir.Sample). False outside the window (no wash part).
        /// </summary>
        public bool Bend(int band, double x, double z, double fraction, out PlantBend bend, out Double3 washAtLoad)
        {
            bend = default;
            washAtLoad = default;
            if (_tick < 0 || band < 0 || band >= _bands.Length) return false;
            double u = x / _cell - 0.5, v = z / _cell - 0.5;
            int i0 = Floor(u), j0 = Floor(v);
            double fu = u - i0, fv = v - j0;
            double load = _bands[band].LoadPoint;
            for (int c = 0; c < 4; c++)
            {
                int s = Held(i0 + (c & 1), j0 + (c >> 1));
                if (s < 0) { bend = default; washAtLoad = default; return false; }
                double w = ((c & 1) == 0 ? 1 - fu : fu) * ((c >> 1) == 0 ? 1 - fv : fv);
                PlantState a = _was[s * _bands.Length + band], b = _now[s * _bands.Length + band];
                double rx = _rest[band] * _wayX[s], rz = _rest[band] * _wayZ[s];
                bend.X += w * (a.X + (b.X - a.X) * fraction - rx);
                bend.Z += w * (a.Z + (b.Z - a.Z) * fraction - rz);
                bend.RateX += w * (a.RateX + (b.RateX - a.RateX) * fraction);
                bend.RateZ += w * (a.RateZ + (b.RateZ - a.RateZ) * fraction);
                washAtLoad = washAtLoad + AirAt(s, load) * w;
            }
            return true;
        }

        // A node's air at a height over its ground (linear between the samples, the nearest outside them).
        Double3 AirAt(int s, double height)
        {
            int k0 = s * _heightCount;
            if (!(height > _heights[0])) return _samples[k0];
            for (int k = 1; k < _heightCount; k++)
                if (height <= _heights[k])
                {
                    double t = (height - _heights[k - 1]) / (_heights[k] - _heights[k - 1]);
                    return _samples[k0 + k - 1] + (_samples[k0 + k] - _samples[k0 + k - 1]) * t;
                }
            return _samples[k0 + _heightCount - 1];
        }

        /// <summary>The dust at world (x, z): the node's under it (nearest, the last tick's). False outside the window.</summary>
        public bool DustAt(double x, double z, out DustSample dust)
        {
            dust = default;
            int s = _tick < 0 ? -1 : Held(Floor(x / _cell), Floor(z / _cell));
            if (s < 0) return false;
            dust.Friction = _friction[s];
            dust.Threshold = _threshold[s];
            dust.Flux = _flux[s];
            dust.Lifts = _flux[s] > 0;
            dust.Film = _surfaceFilm[_surface[s]];
            dust.Surface = _surface[s];
            return true;
        }
    }
}
