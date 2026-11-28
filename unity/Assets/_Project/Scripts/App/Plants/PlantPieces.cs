using System;
using System.Threading;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.App
{
    /// <summary>
    /// The pieces in the air round the drone (w16-plants-in-the-air 1.4, design.md "## 1.4"): the props' cut stems flung off the
    /// discs, the dust the wash lifts from dry bare ground, and the loose plant bits it lifts from covered ground, each moved by the
    /// sim's air (the wash, RotorAir at the coarse wash grid's tick, and the wind, PlantAir) and settling at its own speed. The CPU
    /// side: the pieces' state, their births and their steps, on the coarse wash grid's helper right after each of its ticks; a
    /// snapshot of each tick's pieces for the render thread (PlantPiecesView draws it a tick behind the field, as the wash is).
    /// - **Births, each a function of the flight alone** (its published poses, the props' cuts per field, the wash's ground per
    ///   tick, the world's hash; never a flight stream): a replay draws the same pieces. The cuts come from the render thread's
    ///   signals once a field (Field); a tick waits for the fields it spawns (FieldWait at most), and counts any it never got.
    /// - **The step:** a piece's air u = wind + wash + the turbulence's eddy (a hashed draw a tick, σ from the local friction
    ///   velocity); its velocity relative to the air w relaxes by quadratic drag to its settling speed v_t:
    ///   dw/dt = −g ŷ − (g/v_t²)|w|w, stepped linearly implicit (its terminal speed exact at any step); dust has no inertia
    ///   (its response time v_t/g is microseconds) and moves with the air less its settling speed.
    /// - **Bounded:** DustSlots puffs, CutSlots cut pieces and BitSlots bits; a new one takes a free slot of its kind or the one
    ///   after the last taken (the oldest).
    /// Drawing only: nothing here reaches the flight; it reads the flight's wind field, world and poses, as the wash grids do.
    /// </summary>
    public sealed class PlantPieces
    {
        public enum Kind : byte { None = 0, Dust = 1, Cut = 2, Chaff = 3, Straw = 4, Leaf = 5 }

        /// <summary>The slots (numerical bounds: the dust puffs' draw is a quarter-resolution pass, the others thin quads).</summary>
        public const int DustSlots = 512, CutSlots = 256, BitSlots = 1024;
        public const int DustFirst = 0, CutFirst = DustSlots, BitFirst = DustSlots + CutSlots, Capacity = DustSlots + CutSlots + BitSlots;

        /// <summary>The sub-steps a tick (numerical: 10 ms at the 40 ms tick; a piece's air from the wash is sampled at each).</summary>
        public const int SubSteps = 4;

        /// <summary>The floats a piece takes in a snapshot: nine float4s, PlantPieces.shader's Piece.</summary>
        public const int Stride = 36;

        /// <summary>Dust puffs born a tick at most (numerical: 50 a second, ten seconds of them in DustSlots), and the least mass a
        /// puff is born with, kg: the mass whose peak optical depth at its first size is 0.05 (under it a puff draws under a grey
        /// level of the feed; numerical); a tick's lesser emission carries to the next.</summary>
        public const int DustPerTick = 2;
        public const double DustVisible = 0.05;

        /// <summary>A puff is gone when its peak optical depth falls under this (numerical: about a hundredth of a grey level) or
        /// it is wider than DustWidest m.</summary>
        public const double DustGone = 0.002, DustWidest = 12;

        /// <summary>Cut pieces a rotor flings a field at most (numerical: the physics' count past it is drawn as this many).</summary>
        public const int CutsPerRotorField = 8;

        /// <summary>A piece lives at most Life s; one lying still fades for Lie s (numerical).</summary>
        public const double Life = 30, Lie = 0.5;

        /// <summary>How long a tick waits for the render thread's cuts of its fields, ms (numerical: two frames at 50 fps).</summary>
        public const int FieldWait = 40;

        /// <summary>Beyond this from the drone the wash is taken as none, m (numerical: RotorAir's merged jet is under 0.1 m/s past it
        /// at any hover the wash moves plants from, PlantWash.MinWash).</summary>
        public const double WashReach = 30;

        // ---- the laws' constants (labelled) ----

        /// <summary>g, m/s².</summary>
        const double G = 9.80665;

        /// <summary>von Kármán's constant (published).</summary>
        const double Kappa = 0.41;

        /// <summary>The ground's roughness length for the turbulence's friction velocity, m: 0.01 (assumed: bare soil and short
        /// grass, published 0.001–0.03, Stull 1988 Fig. 9.6).</summary>
        const double RoughnessLength = 0.01;

        /// <summary>The turbulence's velocity scales over the friction velocity, σ_u/u*, σ_v/u*, σ_w/u*: 2.3, 1.9, 1.25 (published:
        /// the neutral surface layer, Panofsky and Dutton 1984). Each piece draws its eddy once a tick (assumed: the Lagrangian
        /// time scale near the ground, about 0.5 z/σ_w, is 20–100 ms within a metre of it).</summary>
        const double EddyU = 2.3, EddyV = 1.9, EddyW = 1.25;

        /// <summary>Dust's vertical flux over the saltation flux, α = 10^(0.134·clay − 6) per cm (published: Marticorena and
        /// Bergametti 1995, clay ≤ 20 %; the surface's clay share is DustLaw's row).</summary>
        static double Sandblasting(double clay) => 100 * Math.Pow(10, 0.134 * Math.Min(clay, 20) - 6);

        /// <summary>The emitted dust's effective radius, m: 2 µm (assumed: a fresh source's fine mode, PM10 by mass; plants-in-the-
        /// air.md §5.3's fines under 20 µm). Its mass extinction 3·Q_ext/(4·ρ_p·r_eff), Q_ext 2 (published: the large-particle limit),
        /// ρ_p the surface table's grain density; its settling speed Stokes's, ρ_p·g·(2r)²/(18μ) (published), μ 1.8e-5 Pa·s.</summary>
        const double DustRadius = 2e-6, Viscosity = 1.8e-5;

        /// <summary>A cut piece's drag across it: C_d 1.2 (published: a circular cylinder across the flow at Re 10²–10⁴), falling
        /// broadside (published in kind: a slender body settles broadside at these Reynolds numbers).</summary>
        const double CylinderDrag = 1.2;

        /// <summary>A piece landing where the wash's friction velocity passes its Keep threshold bounces with this share of its
        /// speed into the ground, upward (assumed).</summary>
        const double Bounce = 0.3;

        /// <summary>A lifted bit's tumble, Hz: 7.5–15 (measured: clip H's thrown leaves turn from face-on to edge-on between frames
        /// at 30 fps, a quarter turn in under 33 ms, plants-in-the-air.md §1.2). A cut piece's: its speed off the blade over its
        /// length, times a hashed share in 0.1–0.5 (assumed: a stem struck off-centre).</summary>
        const double TumbleMin = 7.5, TumbleMax = 15;

        // ---- inputs ----

        readonly IWorldQuery _world;
        readonly PlantAir _wind;
        readonly Moisture _moisture;
        readonly Double3[] _motors;
        readonly double[] _spin = new double[4];
        readonly double _airDensity, _extinction, _dustSettling;
        readonly double[] _clay = new double[256];
        readonly bool[] _dusty = new bool[256];
        readonly int[] _dustColour = new int[256];
        readonly BitPiece[] _bits = new BitPiece[ChaffLaw.Classes];
        readonly CoverSet[] _stems = new CoverSet[4]; // by StemKind
        readonly double[] _stemSettling = new double[4];

        // ---- the pieces (structure of arrays, by slot) ----

        readonly Kind[] _kind = new Kind[Capacity];
        readonly byte[] _colour = new byte[Capacity];
        readonly ulong[] _seed = new ulong[Capacity];
        readonly double[] _born = new double[Capacity], _lay = new double[Capacity];
        readonly Double3[] _x = new Double3[Capacity], _v = new Double3[Capacity], _eddy = new Double3[Capacity];
        readonly double[] _settle = new double[Capacity], _length = new double[Capacity], _width = new double[Capacity], _keep = new double[Capacity];
        readonly double[] _mass = new double[Capacity], _sh = new double[Capacity], _sv = new double[Capacity], _sh0 = new double[Capacity], _sv0 = new double[Capacity];
        readonly Double3[] _axis = new Double3[Capacity], _spinAxis = new Double3[Capacity];
        readonly double[] _spinRate = new double[Capacity];
        readonly Double3[] _path = new Double3[Capacity * (SubSteps + 1)];
        readonly double[] _ground = new double[Capacity];
        readonly int[] _cursor = new int[3];

        // ---- the tick's scratch ----

        readonly XZ[] _xz = new XZ[Capacity];
        readonly GroundSample[] _under = new GroundSample[Capacity];
        readonly Double3[] _wash = new Double3[Capacity];
        double _carry;
        long _tick = -1;
        ulong _digest = 14695981039346656037UL;

        // The wind's lattice round the drone at the tick (numerical: 3 × 3 columns 8 m apart, six heights over each column's
        // ground; a piece takes it trilinearly in x, z and the log of its height, the nearest column's edge beyond).
        const int LatticeSide = 3, LatticeHeights = 6;
        const double LatticeSpacing = 8;
        static readonly double[] LatticeZ = { 0.05, 0.15, 0.4, 1.0, 2.5, 6.0 };
        readonly Double3[] _lattice = new Double3[LatticeSide * LatticeSide * LatticeHeights];
        readonly XZ[] _latticeXz = new XZ[LatticeSide * LatticeSide];
        readonly GroundSample[] _latticeGround = new GroundSample[LatticeSide * LatticeSide];
        double _latticeX0, _latticeZ0;

        // ---- the cuts from the render thread ----

        struct FieldCuts
        {
            public long Field;
            public Corners Cuts, Length, Radius, Angle;
            public PoseFrame Pose;
        }

        readonly FieldCuts[] _fields = new FieldCuts[64];
        int _fieldHead, _fieldCount;
        readonly object _fieldLock = new object();
        long _delivered = -1, _lastField = -1;
        readonly StemPatch[] _patches = new StemPatch[32];

        /// <summary>The fields the render thread never saw (its frame skipped past them), and the ticks that went on without a
        /// field's cuts (the render thread was later than FieldWait): a replay draws the same pieces only while both stay 0.</summary>
        public long MissedFields { get; private set; }
        public long LateTicks { get; private set; }

        /// <param name="world">The flight's world.</param>
        /// <param name="wind">The flight's wind field (read as the plants read it).</param>
        /// <param name="surfaces">The surfaces' table (DustLaw's rows by name; the grains' density).</param>
        /// <param name="moisture">The flight's ground moisture.</param>
        /// <param name="cover">The flight's cover sets (the stems a prop cuts: green, dry and burnt culms).</param>
        /// <param name="motors">The hubs, body frame, Corners' order, and which spin clockwise.</param>
        /// <param name="airDensity">The air's density, kg/m³ (the line's table).</param>
        public PlantPieces(IWorldQuery world, WindField wind, SurfaceTable surfaces, Moisture moisture, Func<CoverKind, CoverSet> cover,
            Double3[] motors, bool[] clockwise, double airDensity)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _wind = new PlantAir(wind);
            _moisture = moisture;
            _motors = (Double3[])motors.Clone();
            for (int c = 0; c < 4; c++) _spin[c] = clockwise[c] ? -1 : 1;
            _airDensity = airDensity;
            double grain = surfaces.ParticleDensity > 0 ? surfaces.ParticleDensity : 2650;
            _extinction = 3 * 2 / (4 * grain * DustRadius);
            _dustSettling = grain * G * 4 * DustRadius * DustRadius / (18 * Viscosity);
            for (int s = 1; s < 256; s++)
            {
                string name = world.SurfaceName((byte)s);
                if (name == null || !DustLaw.Find(name, out DustLaw.Row row)) continue;
                _dusty[s] = true;
                _clay[s] = row.Clay;
                _dustColour[s] = DustColour(name);
            }
            for (int c = 0; c < ChaffLaw.Classes; c++) _bits[c] = ChaffLaw.Piece((BitClass)c, moisture);
            _stems[(int)StemKind.GreenBlade] = cover(CoverKind.GrassBlade);
            _stems[(int)StemKind.DryCulm] = cover(CoverKind.DryCulm);
            _stems[(int)StemKind.BurntCulm] = cover(CoverKind.BurntCulm);
            _stems[(int)StemKind.None] = cover(CoverKind.DryCulm);
            for (int k = 0; k < 4; k++) _stemSettling[k] = StemSettling(_stems[k], airDensity);
            for (int i = 0; i < _born.Length; i++) _lay[i] = double.NaN;
        }

        /// <summary>The colours the drawing knows (PlantPieces.shader's palette, sRGB): 0 loam dust, 1 ash, 2 brick dust, 3 paving's
        /// grey film, 4 a green culm, 5 a dry culm, 6 a burnt culm, 7 chaff, 8 straw, 9 a dead leaf.</summary>
        public const int Colours = 10;

        static int DustColour(string surface) => surface == "burnt_field" ? 1 : surface == "rubble" ? 2 : surface == "asphalt" || surface == "concrete_slab" ? 3 : 0;

        /// <summary>A culm's settling speed, m/s: broadside, v_t = √(2·ρ·a·d·g/(ρ_a·C_d)), a = (π/4)(1 − (1 − 2w)²) its wall's share of
        /// its disc (derived: weight over drag per length), on its cover set's diameter, wall share and tissue density.</summary>
        public static double StemSettling(in CoverSet set, double airDensity)
        {
            double w = set.WallShare > 0 ? set.WallShare : 0.5, a = Math.PI / 4 * (1 - (1 - 2 * w) * (1 - 2 * w));
            double d = set.Diameter > 0 ? set.Diameter : 0.003, rho = set.Density > 0 ? set.Density : 400;
            return Math.Sqrt(2 * rho * a * d * G / (airDensity * CylinderDrag));
        }

        /// <summary>The mass extinction of the emitted dust (m²/kg) and its settling speed (m/s): the laws' numbers.</summary>
        public double DustExtinction => _extinction;
        public double DustSettling => _dustSettling;

        /// <summary>A size class's piece as the drawing takes it (ChaffLaw.Piece at the flight's moisture).</summary>
        public BitPiece Bit(BitClass c) => _bits[(int)c];

        /// <summary>The last tick's flight step (−1 before the first) and a hash of every piece's state after it (the replay's check).</summary>
        public long LastTick => _tick;
        public ulong Digest => _digest;

        /// <summary>The dust emitted since the flight began, kg (α·Q over the lifting nodes, every tick), and the pieces born of
        /// each kind.</summary>
        public double DustEmitted { get; private set; }
        public long CutsBorn { get; private set; }
        public long BitsBorn { get; private set; }

        /// <summary>A slot's kind, place (world m), velocity (world m/s), and a puff's σ_h and σ_v (m), after the last tick.</summary>
        public Kind KindOf(int slot) => _kind[slot];
        public Double3 Position(int slot) => _x[slot];
        public Double3 Velocity(int slot) => _v[slot];
        public double SigmaH(int slot) => _sh[slot];
        public double SigmaV(int slot) => _sv[slot];
        public bool Lying(int slot) => !double.IsNaN(_lay[slot]);

        /// <summary>The live puffs' median centre height over the ground and their thickest σ_v, m (a still's summary).</summary>
        public void DustShape(out double medianHeight, out double thickest, out double widest)
        {
            var heights = new System.Collections.Generic.List<double>();
            thickest = widest = 0;
            for (int i = DustFirst; i < DustFirst + DustSlots; i++)
            {
                if (_kind[i] != Kind.Dust) continue;
                heights.Add(_x[i].Y - _ground[i]);
                thickest = Math.Max(thickest, _sv[i]);
                widest = Math.Max(widest, _sh[i]);
            }
            heights.Sort();
            medianHeight = heights.Count > 0 ? heights[heights.Count / 2] : double.NaN;
        }

        /// <summary>The live pieces of a kind after the last tick, and the dust's mass in the air, kg.</summary>
        public int Live(Kind kind)
        {
            int n = 0;
            for (int i = 0; i < Capacity; i++)
                if (_kind[i] == kind) n++;
            return n;
        }

        public double DustMass
        {
            get
            {
                double m = 0;
                for (int i = DustFirst; i < DustFirst + DustSlots; i++)
                    if (_kind[i] == Kind.Dust) m += _mass[i];
                return m;
            }
        }

        // ---- the render thread: the cuts once a field ----

        /// <summary>
        /// The render thread, once a frame (before the frame's wash tick in a field without threads): the newest signals. A new
        /// field's cuts per rotor (FeedSignals.StemCuts…, over the field that ended at its step) go to the ticks with the pose at
        /// the field's end; the step seen is the ticks' watermark.
        /// </summary>
        public void Field(in FeedSignals signals, PoseBuffer poses)
        {
            long step = signals.Step, field = step / FlightCore.FieldSteps;
            if (step < 0) return;
            if (field > _lastField)
            {
                if (_lastField >= 0 && field > _lastField + 1) MissedFields += field - _lastField - 1;
                _lastField = field;
                Corners cuts = signals.StemCuts;
                if (cuts.FrontLeft > 0 || cuts.FrontRight > 0 || cuts.RearLeft > 0 || cuts.RearRight > 0)
                {
                    var record = new FieldCuts
                    {
                        Field = field, Cuts = cuts, Length = signals.StemPieceLength, Radius = signals.StemCutRadius,
                        Angle = signals.StemBladeAngle,
                    };
                    if (!poses.TryRead(field * FlightCore.FieldSteps, out record.Pose)) record.Pose = poses.Read().Current;
                    lock (_fieldLock)
                    {
                        if (_fieldCount == _fields.Length) { _fieldHead = (_fieldHead + 1) % _fields.Length; _fieldCount--; }
                        _fields[(_fieldHead + _fieldCount) % _fields.Length] = record;
                        _fieldCount++;
                    }
                }
            }
            if (step > Volatile.Read(ref _delivered)) Volatile.Write(ref _delivered, step);
        }

        // ---- the helper: the ticks ----

        /// <summary>
        /// The coarse wash grid's helper (PlantField's, or a test's): PlantWash.Advance(PoseBuffer)'s ticks one by one, each from the
        /// pose of its own step in the history, the pieces stepped right after each in its air and on its ground, and published.
        /// LastAdvanceMs is the pieces' share of the call.
        /// </summary>
        public void Advance(PlantWash grid, PoseBuffer poses)
        {
            long newest = poses.LatestStep, spent = 0;
            LastAdvanceMs = 0;
            if (newest < 0) return;
            long steps = (long)Math.Round(grid.Tick / FlightCore.Dt);
            long next = grid.LastTick < 0 ? newest - newest % steps : grid.LastTick + steps;
            if (newest - next >= PoseBuffer.HistorySteps) next = newest - newest % steps;
            for (; next <= newest; next += steps)
            {
                if (!poses.TryRead(next, out PoseFrame pose)) continue;
                grid.Advance(pose);
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                Tick(grid, grid.TakeGround(double.MaxValue), pose);
                Publish(grid.Tick);
                spent += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            }
            LastAdvanceMs = spent * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        /// <summary>The pieces' time in the last Advance, ms.</summary>
        public double LastAdvanceMs { get; private set; }

        /// <summary>
        /// The coarse wash grid's helper, right after the grid's tick at the pose's step (its RotorAir begun there, ground its
        /// snapshot of that tick): the pieces born over the tick, then every piece stepped over it in the tick's air.
        /// </summary>
        public void Tick(PlantWash grid, WashGround ground, in PoseFrame pose)
        {
            double tick = grid.Tick, t1 = pose.Step * FlightCore.Dt, t0 = t1 - tick, dt = tick / SubSteps;
            _wind.Begin(t1);
            Lattice(pose.Position, t1);
            WaitForFields(pose.Step);
            SpawnCuts(pose.Step, t0, tick, grid);
            if (ground != null && ground.Tick == pose.Step)
            {
                SpawnDust(ground, t0, tick, grid);
                SpawnBits(ground, t0, tick);
            }
            Step(grid.Rotors, pose, ground, t0, dt);
            _tick = pose.Step;
            Hash();
        }

        void WaitForFields(long step)
        {
            if (Volatile.Read(ref _delivered) < 0) return; // no render thread has given a field yet: none to wait for
            int waited = 0;
            while (Volatile.Read(ref _delivered) < step && waited < FieldWait)
            {
                Thread.Sleep(1);
                waited++;
            }
            if (Volatile.Read(ref _delivered) < step) LateTicks++;
        }

        // The wind at the lattice's nodes at the tick.
        void Lattice(in Double3 drone, double t)
        {
            _latticeX0 = drone.X - LatticeSpacing * (LatticeSide - 1) / 2.0;
            _latticeZ0 = drone.Z - LatticeSpacing * (LatticeSide - 1) / 2.0;
            for (int j = 0; j < LatticeSide; j++)
                for (int i = 0; i < LatticeSide; i++)
                    _latticeXz[i + LatticeSide * j] = new XZ(_latticeX0 + i * LatticeSpacing, _latticeZ0 + j * LatticeSpacing);
            _world.SampleGround(_latticeXz, _latticeGround);
            for (int c = 0; c < _latticeXz.Length; c++)
            {
                double g = _latticeGround[c].Height;
                for (int k = 0; k < LatticeHeights; k++)
                    _lattice[c * LatticeHeights + k] = g > double.NegativeInfinity
                        ? _wind.Air(new Double3(_latticeXz[c].X, g + LatticeZ[k], _latticeXz[c].Z), g) : default;
            }
        }

        Double3 WindAt(in Double3 p, double ground)
        {
            double fx = (p.X - _latticeX0) / LatticeSpacing, fz = (p.Z - _latticeZ0) / LatticeSpacing;
            fx = Math.Max(0, Math.Min(LatticeSide - 1.000001, fx));
            fz = Math.Max(0, Math.Min(LatticeSide - 1.000001, fz));
            int i = (int)fx, j = (int)fz;
            double u = fx - i, w = fz - j;
            double z = ground > double.NegativeInfinity ? Math.Max(LatticeZ[0], p.Y - ground) : LatticeZ[LatticeHeights - 1];
            double lz = Math.Log(Math.Min(z, LatticeZ[LatticeHeights - 1]));
            int k = 0;
            while (k < LatticeHeights - 2 && lz > Math.Log(LatticeZ[k + 1])) k++;
            double h = (lz - Math.Log(LatticeZ[k])) / (Math.Log(LatticeZ[k + 1]) - Math.Log(LatticeZ[k]));
            h = Math.Max(0, Math.Min(1, h));
            Double3 sum = default;
            for (int c = 0; c < 4; c++)
            {
                int ci = i + (c & 1), cj = j + (c >> 1);
                double weight = ((c & 1) == 0 ? 1 - u : u) * ((c >> 1) == 0 ? 1 - w : w);
                int at = (ci + LatticeSide * cj) * LatticeHeights + k;
                sum = sum + (_lattice[at] * (1 - h) + _lattice[at + 1] * h) * weight;
            }
            return sum;
        }

        // ---- births ----

        int Take(Kind kind)
        {
            int group = kind == Kind.Dust ? 0 : kind == Kind.Cut ? 1 : 2;
            int first = group == 0 ? DustFirst : group == 1 ? CutFirst : BitFirst, count = group == 0 ? DustSlots : group == 1 ? CutSlots : BitSlots;
            int start = _cursor[group], slot = -1;
            for (int n = 0; n < count && slot < 0; n++)
            {
                int s = first + (start + n) % count;
                if (_kind[s] == Kind.None) slot = s;
            }
            if (slot < 0) slot = first + start % count; // full: the oldest
            _cursor[group] = (slot - first + 1) % count;
            return slot;
        }

        void Born(int s, Kind kind, ulong seed, double born, in Double3 x, in Double3 v)
        {
            _kind[s] = kind;
            _seed[s] = seed;
            _born[s] = born;
            _lay[s] = double.NaN;
            _x[s] = x;
            _v[s] = v;
            _eddy[s] = default;
            for (int k = 0; k <= SubSteps; k++) _path[s * (SubSteps + 1) + k] = x;
            // The tumble's axis and the piece's own axis across it (end over end), from its seed (the flight's data hashed).
            _spinAxis[s] = Unit(seed, 2);
            Double3 a = Double3.Cross(_spinAxis[s], Unit(seed, 1));
            double l = a.Length();
            _axis[s] = l > 1e-6 ? a * (1 / l) : Double3.Cross(_spinAxis[s], new Double3(0, 1, 0)) * (1 / Math.Max(1e-6, Math.Sqrt(1 - _spinAxis[s].Y * _spinAxis[s].Y)));
        }

        // The cuts of the fields that ended in the tick (t0, t0 + tick], each piece off its rotor's blade at the cut's mean radius
        // where the blade stood at the field's last cut (one of the two blades, by the field's hash), at the blade's speed there.
        void SpawnCuts(long step, double t0, double tick, PlantWash grid)
        {
            long from = step - (long)Math.Round(tick / FlightCore.Dt);
            while (true)
            {
                FieldCuts f;
                lock (_fieldLock)
                {
                    if (_fieldCount == 0) return;
                    f = _fields[_fieldHead];
                    long end = f.Field * FlightCore.FieldSteps;
                    if (end > step) return; // a later tick's
                    _fieldHead = (_fieldHead + 1) % _fields.Length;
                    _fieldCount--;
                    if (end <= from) continue; // an earlier tick's that came late: counted in LateTicks
                }
                for (int c = 0; c < 4; c++) SpawnRotor(f, c, t0, tick);
            }
        }

        void SpawnRotor(in FieldCuts f, int c, double t0, double tick)
        {
            double cuts = f.Cuts[c], length = f.Length[c], radius = f.Radius[c];
            if (!(cuts > 0) || !(length > 0) || !(radius > 0)) return;
            ulong h = Hash(0xC07, (ulong)f.Field, (ulong)c);
            int n = (int)Math.Floor(cuts + Fraction(h));
            if (n > CutsPerRotorField) n = CutsPerRotorField;
            PoseFrame p = f.Pose;
            Quat q = p.Attitude;
            Double3 up = q.Rotate(new Double3(0, 1, 0)), hub = p.Position + q.Rotate(_motors[c]);
            double s = _spin[c], omega = p.RotorSpeed[c];
            double fieldEnd = f.Field * FlightCore.FieldSteps * FlightCore.Dt, fieldLength = FlightCore.FieldSteps * FlightCore.Dt;
            // The stems the disc met there (the map's stem field reaching the disc's plane): the piece's kind, so its section.
            StemKind kind = StemKind.DryCulm;
            for (int j = 0; j < n; j++)
            {
                ulong hj = Hash(h, (ulong)j, 0x5EED);
                double psi = f.Angle[c] + ((hj & 1) == 0 ? 0 : Math.PI);
                Double3 dirBody = new Double3(Math.Cos(psi), 0, s * Math.Sin(psi));
                Double3 dir = q.Rotate(dirBody), motion = Double3.Cross(up, dir) * -s;
                Double3 at = hub + dir * radius;
                if (j == 0) kind = StemAt(at);
                double born = fieldEnd - fieldLength * Fraction(Hash(hj, 1, 0));
                if (born <= t0) born = t0 + 1e-6;
                if (born > t0 + tick) born = t0 + tick;
                Double3 v = p.Velocity + q.Rotate(Double3.Cross(p.BodyRate, _motors[c] + dirBody * radius)) + motion * (omega * radius);
                Double3 x = at - p.Velocity * (fieldEnd - born);
                int slot = Take(Kind.Cut);
                Born(slot, Kind.Cut, hj, born, x, v);
                CutsBorn++;
                CoverSet set = _stems[(int)kind];
                _settle[slot] = _stemSettling[(int)kind];
                _length[slot] = length;
                _width[slot] = set.Diameter > 0 ? set.Diameter : 0.003;
                _keep[slot] = _bits[(int)BitClass.Straw].Keep > 0 ? _bits[(int)BitClass.Straw].Keep : double.PositiveInfinity;
                _colour[slot] = (byte)(kind == StemKind.GreenBlade ? 4 : kind == StemKind.BurntCulm ? 6 : 5);
                _spinRate[slot] = (omega * radius / Math.Max(length, 0.01)) * (0.1 + 0.4 * Fraction(Hash(hj, 2, 0)));
            }
        }

        // The stem kind with the most stems reaching the disc's plane at a point (the map's stem field), dry where none.
        readonly XZ[] _oneXz = new XZ[1];
        readonly GroundSample[] _oneGround = new GroundSample[1];

        StemKind StemAt(in Double3 at)
        {
            _oneXz[0] = new XZ(at.X, at.Z);
            _world.SampleGround(_oneXz, _oneGround);
            double height = _oneGround[0].Height > double.NegativeInfinity ? at.Y - _oneGround[0].Height : 0;
            int count = _world.StemsNear(new XZ(at.X, at.Z), 0.05, height, _patches);
            StemKind best = StemKind.DryCulm;
            double most = 0;
            for (int i = 0; i < Math.Min(count, _patches.Length); i++)
                if (_patches[i].Density * _patches[i].Area > most)
                {
                    most = _patches[i].Density * _patches[i].Area;
                    best = _patches[i].Kind;
                }
            return best;
        }

        // The dust over the tick: each lifting node's vertical flux α·Q (MB95 on White's saltation flux) over its cell; a tick's
        // emission (with what earlier ticks carried) in up to DustPerTick puffs, each placed by the emission's own distribution
        // over the nodes (stratified by the tick's hash), as wide as the share of the lifting area it stands for and as thick as
        // the near jets (RotorAir.JetThickness: the wall jets' layer).
        void SpawnDust(WashGround ground, double t0, double tick, PlantWash grid)
        {
            int n = ground.Size * ground.Size;
            double cell = ground.Cell, area = cell * cell, total = 0, lifting = 0;
            for (int i = 0; i < n; i++)
            {
                double q = ground.DustFlux[i];
                if (!(q > 0) || !_dusty[ground.Surface[i]]) continue;
                total += Sandblasting(_clay[ground.Surface[i]]) * q * area * tick;
                lifting += area;
            }
            if (!(total > 0))
            {
                _carry = 0;
                return;
            }
            _carry += total;
            DustEmitted += total;
            double sv = Math.Max(0.01, 0.5 * grid.Rotors.JetThickness);
            int puffs = DustPerTick;
            double sh = Math.Max(cell, Math.Sqrt(lifting / (puffs * 2 * Math.PI)));
            double least = DustVisible * 2 * Math.PI * sh * sv / _extinction; // τ_peak = k·m/(2π·σ_h·σ_v)
            while (puffs > 1 && _carry / puffs < least) puffs--;
            sh = Math.Max(cell, Math.Sqrt(lifting / (puffs * 2 * Math.PI)));
            if (_carry / puffs < DustVisible * 2 * Math.PI * sh * sv / _extinction) return; // carried on
            double mass = _carry / puffs;
            _carry = 0;
            ulong h = Hash(0xD057, (ulong)ground.Tick, 0);
            for (int p = 0; p < puffs; p++)
            {
                double target = (p + Fraction(Hash(h, (ulong)p, 1))) / puffs * total, run = 0;
                int node = -1;
                for (int i = 0; i < n && node < 0; i++)
                {
                    double q = ground.DustFlux[i];
                    if (!(q > 0) || !_dusty[ground.Surface[i]]) continue;
                    run += Sandblasting(_clay[ground.Surface[i]]) * q * area * tick;
                    if (run >= target) node = i;
                }
                if (node < 0) continue;
                long a = node % ground.Size, b = node / ground.Size;
                ulong hp = Hash(h, (ulong)p, 2);
                double x = (ground.StartI + a + Fraction(Hash(hp, 1, 0))) * cell, z = (ground.StartJ + b + Fraction(Hash(hp, 2, 0))) * cell;
                double born = t0 + tick * Fraction(Hash(hp, 3, 0));
                if (born <= t0) born = t0 + 1e-6;
                int slot = Take(Kind.Dust);
                Born(slot, Kind.Dust, hp, born, new Double3(x, double.NaN, z), default);
                _mass[slot] = mass;
                _sh[slot] = _sh0[slot] = sh;
                _sv[slot] = _sv0[slot] = sv;
                _settle[slot] = _dustSettling;
                _colour[slot] = (byte)_dustColour[ground.Surface[node]];
            }
        }

        // The bits over the tick: each node's pieces of each class, rate × cell² × tick rounded by the node's hash (their mean the
        // rate's), each at a hashed point of its cell, lying in the cover (BitStart over the ground), at rest.
        void SpawnBits(WashGround ground, double t0, double tick)
        {
            int n = ground.Size * ground.Size;
            double cell = ground.Cell, area = cell * cell;
            for (int i = 0; i < n; i++)
                for (int c = 0; c < ChaffLaw.Classes; c++)
                {
                    double rate = ground.BitsRate[i * ChaffLaw.Classes + c];
                    if (!(rate > 0)) continue;
                    long a = i % ground.Size, b = i / ground.Size;
                    ulong h = Hash(0xB175, (ulong)ground.Tick, Hash((ulong)(ground.StartI + a), (ulong)(ground.StartJ + b), (ulong)c));
                    int count = (int)Math.Floor(rate * area * tick + Fraction(h));
                    BitPiece piece = _bits[c];
                    for (int j = 0; j < count; j++)
                    {
                        ulong hj = Hash(h, (ulong)j, 7);
                        double x = (ground.StartI + a + Fraction(Hash(hj, 1, 0))) * cell, z = (ground.StartJ + b + Fraction(Hash(hj, 2, 0))) * cell;
                        double born = t0 + tick * Fraction(Hash(hj, 3, 0));
                        if (born <= t0) born = t0 + 1e-6;
                        int slot = Take(c == (int)BitClass.Chaff ? Kind.Chaff : c == (int)BitClass.Straw ? Kind.Straw : Kind.Leaf);
                        Born(slot, (Kind)((int)Kind.Chaff + c), hj, born, new Double3(x, double.NaN, z), default);
                        BitsBorn++;
                        _settle[slot] = piece.Settling > 0 ? piece.Settling : 1;
                        _length[slot] = piece.Length > 0 ? piece.Length : 0.01;
                        _width[slot] = piece.Width > 0 ? piece.Width : 0.003;
                        _keep[slot] = piece.Keep > 0 ? piece.Keep : double.PositiveInfinity;
                        _colour[slot] = (byte)(7 + c);
                        _spinRate[slot] = 2 * Math.PI * (TumbleMin + (TumbleMax - TumbleMin) * Fraction(Hash(hj, 4, 0)));
                    }
                }
        }

        /// <summary>A bit's start over the ground, m: 0.03 (assumed: lying in the cover's lodged layer).</summary>
        const double BitStart = 0.03;

        // ---- the step ----

        void Step(RotorAir rotors, in PoseFrame pose, WashGround ground, double t0, double dt)
        {
            // Each live piece's ground (once a tick) and the tick's eddy.
            int live = 0;
            for (int i = 0; i < Capacity; i++)
                if (_kind[i] != Kind.None) _xz[live++] = new XZ(_x[i].X, _x[i].Z);
            _world.SampleGround(new ReadOnlySpan<XZ>(_xz, 0, live), new Span<GroundSample>(_under, 0, live));
            int at = 0;
            Double3 drone = pose.Position;
            for (int i = 0; i < Capacity; i++)
            {
                if (_kind[i] == Kind.None) continue;
                double g = _under[at++].Height;
                _ground[i] = g;
                if (!(g > double.NegativeInfinity))
                {
                    _kind[i] = Kind.None;
                    continue;
                }
                if (double.IsNaN(_x[i].Y)) // born this tick: its height over the ground it lies on
                {
                    double start = _kind[i] == Kind.Dust ? 0.8 * _sv[i] : BitStart;
                    _x[i] = new Double3(_x[i].X, g + start, _x[i].Z);
                    for (int k = 0; k <= SubSteps; k++) _path[i * (SubSteps + 1) + k] = _x[i];
                }
            }
            for (int i = 0; i < Capacity; i++)
            {
                if (_kind[i] == Kind.None) continue;
                int p0 = i * (SubSteps + 1);
                _path[p0] = _x[i];
                double g = _ground[i];
                bool near = (_x[i] - drone).Length() < WashReach;
                Double3 wind = WindAt(_x[i], g);
                Double3 washed = near ? rotors.At(_x[i], g, out _) : default;
                // The tick's eddy: σ_u, σ_v, σ_w times the local friction velocity, from the air's speed at the piece (the log law
                // u* = κ·|u|/ln(z/z₀)), along the air and across it, by the piece's hash and the tick.
                Double3 mean = wind + washed;
                double z = Math.Max(2 * RoughnessLength, _x[i].Y - g), speed = Math.Sqrt(mean.X * mean.X + mean.Z * mean.Z);
                double ustar = Kappa * Math.Sqrt(mean.X * mean.X + mean.Y * mean.Y + mean.Z * mean.Z) / Math.Log(z / RoughnessLength);
                _eddy[i] = Eddy(_seed[i], (ulong)pose.Step, mean, speed, ustar);
                for (int sub = 0; sub < SubSteps; sub++)
                {
                    double s0 = t0 + sub * dt, s1 = s0 + dt;
                    if (_born[i] >= s1)
                    {
                        _path[p0 + sub + 1] = _x[i];
                        continue;
                    }
                    double h = s1 - Math.Max(s0, _born[i]);
                    if (sub > 0 && near) washed = rotors.At(_x[i], g, out _);
                    Double3 u = wind + washed + (_kind[i] == Kind.Dust ? default : _eddy[i]);
                    if (_kind[i] == Kind.Dust) StepDust(i, u, h, ustar, g);
                    else StepPiece(i, u, h, g, ground, s1);
                    _path[p0 + sub + 1] = _x[i];
                    if (_kind[i] == Kind.None) break;
                }
                if (_kind[i] == Kind.None) continue;
                Retire(i, t0 + SubSteps * dt);
            }
        }

        static Double3 Eddy(ulong seed, ulong tick, in Double3 mean, double speed, double ustar)
        {
            ulong h = Hash(seed, tick, 0xEDD1);
            double a = Normal(h, 0), b = Normal(h, 1), c = Normal(h, 2);
            Double3 along = speed > 1e-6 ? new Double3(mean.X / speed, 0, mean.Z / speed) : new Double3(1, 0, 0);
            Double3 across = new Double3(-along.Z, 0, along.X);
            return along * (EddyU * ustar * a) + across * (EddyV * ustar * b) + new Double3(0, EddyW * ustar * c, 0);
        }

        // Dust: with the air, less its settling speed; its puff spreads by the surface layer's eddy diffusivity K = κ·u*·z
        // (published: neutral, Monin–Obukhov) on both axes, z the larger of the centre's height and the puff's thickness, and its
        // centre stays at 0.8 σ_v over the ground at least (the centroid of a Gaussian reflected at the ground).
        void StepDust(int i, in Double3 u, double h, double ustar, double ground)
        {
            Double3 x = _x[i] + (u - new Double3(0, _settle[i], 0)) * h;
            double zc = Math.Max(_sv[i], x.Y - ground), k = Kappa * ustar * zc;
            _sh[i] = Math.Sqrt(_sh[i] * _sh[i] + 2 * k * h);
            _sv[i] = Math.Sqrt(_sv[i] * _sv[i] + 2 * k * h);
            if (x.Y < ground + 0.8 * _sv[i]) x = new Double3(x.X, ground + 0.8 * _sv[i], x.Z);
            _x[i] = x;
            _v[i] = u;
        }

        // A piece: dw/dt = −g ŷ − (g/v_t²)|w|w relative to the air, stepped linearly implicit, its inner steps no longer than
        // 0.5/(k|w|) (numerical: the fast flight off a blade); the ground under it at the tick's start (a tick moves it under 0.5 m
        // but off a blade). At the ground: on where the wash's u* over the cover passes its Keep, bounced; else it lies.
        void StepPiece(int i, in Double3 u, double h, double ground, WashGround snapshot, double at)
        {
            if (!double.IsNaN(_lay[i]))
            {
                _v[i] = default;
                return;
            }
            double vt = _settle[i], k = G / (vt * vt);
            Double3 x = _x[i], w = _v[i] - u;
            double left = h;
            for (int n = 0; n < 64 && left > 1e-9; n++)
            {
                double speed = w.Length(), step = Math.Min(left, 0.5 / (k * speed + 1e-9));
                Double3 next = (w - new Double3(0, G * step, 0)) * (1 / (1 + k * speed * step));
                x = x + (u + (w + next) * 0.5) * step;
                w = next;
                left -= step;
            }
            _v[i] = u + w;
            if (x.Y < ground)
            {
                double keep = _keep[i], ustar = 0;
                if (snapshot != null && snapshot.Node(x.X, x.Z, out int node)) ustar = snapshot.BitsFriction[node];
                if (ustar > keep && _v[i].Y < 0) _v[i] = new Double3(_v[i].X, -Bounce * _v[i].Y, _v[i].Z);
                else
                {
                    _lay[i] = at;
                    _v[i] = default;
                }
                x = new Double3(x.X, ground, x.Z);
            }
            _x[i] = x;
        }

        void Retire(int i, double t)
        {
            bool gone = t - _born[i] > Life;
            if (_kind[i] == Kind.Dust)
            {
                double peak = _extinction * _mass[i] / (2 * Math.PI * _sh[i] * _sv[i]);
                gone |= peak < DustGone || _sh[i] > DustWidest;
            }
            else gone |= !double.IsNaN(_lay[i]) && t - _lay[i] > Lie;
            if (gone) _kind[i] = Kind.None;
        }

        // ---- the snapshot for the render thread ----

        /// <summary>A tick's pieces as PlantPieces.shader reads them: per slot nine float4s (Stride floats): the path's five points at
        /// the tick's sub-steps (xyz) with, in w, the kind, the birth and the time it lay down (s from T0; 1e9 for none), the seed's
        /// fraction and the tumble's rate (rad/s); then (length, width, σ_h at T0, σ_h at T1), (σ_v at T0, σ_v at T1, the dust's k·m
        /// (m²) or 1, the colour), (the piece's axis at its birth, 0) and (the tumble's axis, 0).</summary>
        public sealed class Snapshot
        {
            public long Tick = -1;
            public double T0, TickSeconds;
            public readonly float[] Data = new float[Capacity * Stride];
            public int Dust, Pieces;
        }

        Snapshot _back = new Snapshot(), _ready = new Snapshot(), _front = new Snapshot();
        bool _fresh, _shown;
        readonly object _lock = new object();

        /// <summary>The helper, after Tick: the tick's pieces to the back snapshot, swapped in as the ready one.</summary>
        public void Publish(double tickSeconds)
        {
            Snapshot s = _back;
            s.Tick = _tick;
            s.TickSeconds = tickSeconds;
            s.T0 = _tick * FlightCore.Dt - tickSeconds;
            s.Dust = s.Pieces = 0;
            float[] d = s.Data;
            for (int i = 0; i < Capacity; i++)
            {
                int o = i * Stride;
                if (_kind[i] == Kind.None)
                {
                    d[o + 3] = 0;
                    continue;
                }
                if (_kind[i] == Kind.Dust) s.Dust++;
                else s.Pieces++;
                for (int k = 0; k <= SubSteps; k++)
                {
                    Double3 p = _path[i * (SubSteps + 1) + k];
                    d[o + 4 * k] = (float)p.X;
                    d[o + 4 * k + 1] = (float)p.Y;
                    d[o + 4 * k + 2] = (float)p.Z;
                }
                d[o + 3] = (float)_kind[i];
                d[o + 7] = (float)(_born[i] - s.T0);
                d[o + 11] = double.IsNaN(_lay[i]) ? 1e9f : (float)(_lay[i] - s.T0);
                d[o + 15] = (float)Fraction(_seed[i]);
                d[o + 19] = (float)_spinRate[i];
                bool dust = _kind[i] == Kind.Dust;
                d[o + 20] = (float)(dust ? 0 : _length[i]);
                d[o + 21] = (float)(dust ? 0 : _width[i]);
                d[o + 22] = (float)(dust ? _sh0[i] : 0);
                d[o + 23] = (float)(dust ? _sh[i] : 0);
                d[o + 24] = (float)(dust ? _sv0[i] : 0);
                d[o + 25] = (float)(dust ? _sv[i] : 0);
                d[o + 26] = (float)(dust ? _extinction * _mass[i] : 1);
                d[o + 27] = _colour[i];
                d[o + 28] = (float)_axis[i].X;
                d[o + 29] = (float)_axis[i].Y;
                d[o + 30] = (float)_axis[i].Z;
                d[o + 31] = 0;
                d[o + 32] = (float)_spinAxis[i].X;
                d[o + 33] = (float)_spinAxis[i].Y;
                d[o + 34] = (float)_spinAxis[i].Z;
                d[o + 35] = 0;
                if (dust)
                {
                    _sh0[i] = _sh[i];
                    _sv0[i] = _sv[i];
                }
            }
            lock (_lock)
            {
                (_back, _ready) = (_ready, _back);
                _fresh = true;
            }
        }

        /// <summary>The render thread: the newest tick's pieces at or before the frame's time t (s), null before the first.</summary>
        public Snapshot Take(double t, out bool changed)
        {
            changed = false;
            lock (_lock)
            {
                if (_fresh && _ready.Tick * FlightCore.Dt <= t + 1e-9)
                {
                    (_ready, _front) = (_front, _ready);
                    _fresh = false;
                    changed = true;
                    _shown = true;
                }
            }
            return _shown ? _front : null;
        }

        // ---- hashes ----

        void Hash()
        {
            ulong d = _digest;
            for (int i = 0; i < Capacity; i++)
            {
                if (_kind[i] == Kind.None) continue;
                d = Mix(d, (ulong)i);
                d = Mix(d, (ulong)BitConverter.DoubleToInt64Bits(_x[i].X));
                d = Mix(d, (ulong)BitConverter.DoubleToInt64Bits(_x[i].Y));
                d = Mix(d, (ulong)BitConverter.DoubleToInt64Bits(_x[i].Z));
                d = Mix(d, (ulong)BitConverter.DoubleToInt64Bits(_v[i].Y));
                if (_kind[i] == Kind.Dust) d = Mix(d, (ulong)BitConverter.DoubleToInt64Bits(_sh[i]));
            }
            _digest = d;
        }

        static ulong Mix(ulong d, ulong v) => (d ^ v) * 1099511628211UL;

        static ulong Hash(ulong a, ulong b, ulong c) =>
            FlightRandom.Mix64(unchecked(a * 0x9E3779B97F4A7C15UL ^ FlightRandom.Mix64(b + 0x632BE59BD9B4E019UL) ^ (c << 17) ^ (c >> 3)));

        static double Fraction(ulong h) => (h >> 11) * (1.0 / 9007199254740992.0);

        // A standard normal from the hash (Box–Muller on two of its draws).
        static double Normal(ulong h, ulong k)
        {
            double a = Fraction(Hash(h, k, 1)), b = Fraction(Hash(h, k, 2));
            return Math.Sqrt(-2 * Math.Log(Math.Max(a, 1e-300))) * Math.Cos(2 * Math.PI * b);
        }

        static Double3 Unit(ulong seed, ulong k)
        {
            double z = 2 * Fraction(Hash(seed, k, 3)) - 1, a = 2 * Math.PI * Fraction(Hash(seed, k, 4)), r = Math.Sqrt(Math.Max(0, 1 - z * z));
            return new Double3(r * Math.Cos(a), z, r * Math.Sin(a));
        }
    }
}
