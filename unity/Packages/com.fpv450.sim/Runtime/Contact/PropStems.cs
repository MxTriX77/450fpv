using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Wind;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The props in standing stems (w9-village-physics 2.2 and 2.12, design.md "## 2.2" and "## 2.12"; PHY-05, PHY-24, PHY-26):
    /// a map's stem field (IWorldQuery.StemsNear) and its drawn straws and stems (MicroDetailNear's S04 elements) about each disc,
    /// every step, after the shapes' passes. Engine-free, allocation-free in a step, DetMath only; nothing about it is drawn.
    /// - The field's stems are placed one by one from the world (2.12): per 0.1 m square, the cell's stems per m² reaching h_ref, each
    ///   at its own place, natural lean and drawn length from its own hash (world-derived, never a flight draw); its diameter from
    ///   its length by the kind's allometry, d = d₁·L^(1/1.53) (Niklas 1995).
    /// - A disc's slipstream (q = T/A) folds the stems under it: a vertical jet loads a stem leaning φ with q·C_d·d·sin²φ, so as a
    ///   rigid link on 4EI/L its moment is m(φ) = (φ − φ₀) − A·sin²φ − G·sin φ, A = q·C_d·d·L³/(8EI), G = μ·g·L³/(8EI); its lean
    ///   follows φ' = −m/τ at its first mode's τ = L²/(1.875²·√(EI/μ)). A folded stem stands as an arch, its crest 0.5·l_b,
    ///   l_b = (EI/(q·C_d·d))^(1/3).
    /// - A blade cuts a stem whose top stands over the disc's plane where its speed across the stem's axis reaches the stem's
    ///   critical speed v_c = k_edge·[F_c·√d / (2√(2π)·(EI·μ³)^¼)]^(2/3), F_c = e_s·A_w/d (stem-properties.md §2.2); slower, it is
    ///   pushed aside. A cut costs e_s·A_w and the swept band's fling μ·h_f·u², a slice of a stub min(1, Δ/d)·e_s·A_w and μ·Δ·u²;
    ///   the rotor pays it, the body takes E/u against the blade's motion and the pitched face's E_cut·(c·cos θ/h_f)/u along the
    ///   axis, each at its own radius and azimuth, by the blade crossing it in the step.
    /// - Each stem keeps its lean and its length (shortened by a cut) in a store shared by every disc, keyed by its hash.
    /// - The wash with distance (2.13, design.md "## 2.13"; UseWash): the stems read the line's law of the rotors' air (RotorAir,
    ///   p7d-tether design 19) through an instance of their own, begun each step with the line's inputs. A stem takes the air
    ///   at ⅔·L·cos φ over its base on its vertical, with its near column's swirl, refreshed every StemAirRefresh steps by its
    ///   slot; its load normal to it in its plane of lean is ½ρ·C_d·d·|u_φ|·u_φ a length, so m(φ) = (φ − φ₀) − K·|u_φ|·u_φ − G·sin φ,
    ///   K = ½ρ·C_d·d·L³/(8EI): 2.12's A·sin²φ under a vertical jet, the whole cross-flow on a standing stem in a wall jet, and
    ///   the fountain standing it up. Off by default (D-111: its near field waits for the near wake's spread and the jets'
    ///   turbulence); without FlightSetup.StemWash, or with no table, the slipstream is 2.12's, above.
    /// The step's cuts go to BladeStrikes (StrikeKind.Stem), a rotor's episode to the log (StemEpisode, start and end), the cut
    /// pieces to the feed (FeedSignals.StemCuts…); a stem's force goes through Breaks as a twig's does.
    /// </summary>
    public sealed partial class PropStrikes
    {
        /// <summary>A stem's drag coefficient across the flow: 1.15 (published: 1.0–1.3 for wires and cables, Engineering ToolBox;
        /// 1.28 a flat plate, NASA Glenn; stem-properties.md §2.4).</summary>
        public const double StemDrag = 1.15;

        /// <summary>The prop's edge factor on a sharp blade's critical speed: 3 (assumed, 2–4: between a sharp mower blade's 25 m/s
        /// and nylon line's 61–86 m/s on grass, the blade's nose being 0.13–0.65 mm round; stem-properties.md §2.2).</summary>
        public const double EdgeFactor = 3;

        /// <summary>A stem's diameter grows as its length to this power: H ∝ D^1.53 over 76 herbaceous species (published: Niklas
        /// 1995, Ann. Bot. 75: 133–142). The cover row's diameter_m is a 1 m stem's.</summary>
        public const double StemAllometry = 1 / 1.53;

        /// <summary>A field stem's natural lean, rad: up to 15° by its hash (assumed, 5–25°: the drawn blades' spread and the
        /// detail's half tilt to the ground). It sets how readily a stem starts to fold in a vertical jet.</summary>
        public const double StemNaturalLean = 0.2617993877991494;

        /// <summary>A folded stalk's arch stands to this share of its bending length (derived: the elastica of a stalk clamped
        /// upright, loaded w·sin²θ normal to it, its end on the ground: 0.43–0.61 for L/l_b 3–6; design.md 2.12).</summary>
        public const double StemFoldedCrest = 0.5;

        /// <summary>The radius at which the wake's swirl is taken, over R: 2/3 (assumed: the disc's mean radius by area weighting
        /// of a uniform load).</summary>
        public const double StemSwirlRadius = 2.0 / 3;

        /// <summary>A cut through a leaning or folded stem leaves the stub up to the plane along a leg no flatter than this cosine
        /// (assumed: the arch's rising leg within about 45° of upright where the plane crosses it).</summary>
        public const double StemStubCos = 0.7;

        /// <summary>The field's squares, m; the height the squares' stems reach, m (the lowest a disc's plane comes over the
        /// ground while the drone stands: 0.288 m less 0.06 m for a rim tilted 30°, less a margin); the layers' reach shares'
        /// height step, m (all numerical).</summary>
        public const double StemSquare = 0.1, StemReference = 0.2, StemReachStep = 0.05;

        /// <summary>No field stem is placed under a disc more than this over the ground, m (numerical: the drawn stems stand to
        /// 1.77 m; a slipstream's grip on a canopy further down is left out).</summary>
        public const double StemWakeTop = 2.5;

        /// <summary>A rotor's cache of stems is rebuilt once its hub has moved this far, m (numerical), over the disc's radius and
        /// this margin again.</summary>
        public const double StemCacheRefresh = 0.1;

        /// <summary>A stem episode ends this long after its last whole stem, s (numerical: the forensics' grain).</summary>
        public const double StemQuietTime = 0.1;

        const int StemCacheSize = 1024, StemPatchesMax = 64, StateSlots = 16384, StateProbe = 16, SquareSlots = 1024, SquareProbe = 8,
            Layers = 16, ReachEntries = 41;
        const double Gravity = 9.80665, PointRadius = 1e-3;

        /// <summary>One stem's tissue: its section's wall area, mass per length, bending stiffness, the slipstream's lean per
        /// pascal per m³ C_d·d/(8EI), the critical speed and a cut's edge work.</summary>
        struct Tissue
        {
            public bool Known;
            public ushort Code;
            public double D, Area, Mu, EI, Kappa0, Vc, Cut;
        }

        /// <summary>One cached stem: its base, its lean's direction, its drawn length and top over its base, its tissue and the
        /// constants of its lean (μg/8EI, its mode's τ per m², cos φ₀), its state's key and slot.</summary>
        struct Stalk
        {
            public Double3 Base, Lean;
            public double Length, Rise, Phi0, CosPhi0, MuG8, Tau0;
            public Tissue T;
            public ulong Id;
            public int Slot;
        }

        /// <summary>The last step's stems per rotor, for a test or a bench: the disc's height over the ground (m), the
        /// slipstream's pressure (Pa: 2.12's T/A; with the wash, 2.13, the mean in-plane pressure ½ρ·(u_lean² + u_y²) on the stems
        /// based under the disc), the step's fresh cuts and stub slices (cut equivalents), all its cuts, its work and the fresh
        /// cuts' share of it (J), the
        /// impulse it gave the body in the disc's plane (world N·s) and along the rotor's axis (N·s), the stems cached about the
        /// disc and those under it standing over its plane; the stems based under the disc (within R of its axis) and those of
        /// them leaning past 45° (2.13, before the step's lean), the same for the tall ones (drawn 1 m or longer: the pilot's
        /// "high grass"), and the fresh cuts outside R/√2 (the disc's outer ring).</summary>
        public struct StemRecord
        {
            public double Height, Load, Fresh, Sliced, Cuts, Work, FreshWork, Axial, FreshRing;
            public Double3 InPlane;
            public int Cached, Over, Under, Folded, UnderTall, FoldedTall;
        }

        /// <summary>The last step's stems per rotor (StemRecord), Corners' order.</summary>
        public readonly StemRecord[] StemLast = new StemRecord[4];

        /// <summary>The known-wrong's switches, for a test's comparison only (a flight never sets them): off, the slipstream folds
        /// no stem, and a blade cuts every stem it meets whatever its speed.</summary>
        public bool StemLean = true, StemCritical = true;

        /// <summary>A sweep's scales on the field's stems per m² and on every stem's diameter (design.md 2.12's sweep only: a flight
        /// never sets them, and a log does not carry them).</summary>
        public double StemDensityScale = 1, StemDiameterScale = 1;

        /// <summary>Every how many steps a stem's air is taken again, by its slot (2.13, numerical: 8 ms, under half the lean's τ
        /// of every stem that can reach a standing disc's plane). A test's comparison sets 1; a flight never sets it.</summary>
        public int StemAirRefresh = 8;

        /// <summary>The stems' rotor air (2.13: the line's law, begun with the line's inputs while stems are near), or null for
        /// 2.12's undiminished column.</summary>
        public RotorAir Wash => _air;

        // 2.13: the stems' air, its inputs and the step's hubs; the air on each stem (world m/s, its swirl in) and its step.
        RotorAir _air;
        WindField _wind;
        double _rho;
        readonly Double3[] _motor = new Double3[4], _washHub = new Double3[4], _washAir = new Double3[4];
        readonly double[] _washThrust = new double[4], _washGround = new double[4];
        readonly bool[] _washDisc = new bool[4];
        readonly XZ[] _washXZ = new XZ[4];
        readonly GroundSample[] _washSample = new GroundSample[4];
        readonly Ray[] _soilRay = new Ray[1];
        readonly RayHit[] _soilHit = new RayHit[1];
        Double3 _washUp;
        double[] _sUx, _sUy, _sUz;
        long[] _sAir;

        /// <summary>Each corner's strikes' impulse along its rotor's axis this step, N·s (every kind: shapes, twigs, stems), which
        /// loads its arm's mode at the tip (AirframeModes.Strike, G8).</summary>
        public ref readonly Corners TipImpulse => ref _tip;

        Corners _tip;
        bool _stemsOn;
        readonly CoverSet[] _coverSets = new CoverSet[7]; // by CoverKind
        readonly StemPatch[] _patches = new StemPatch[StemPatchesMax];
        readonly XZ[] _hubXZ = new XZ[4], _point = new XZ[1];
        readonly GroundSample[] _hubGround = new GroundSample[4], _pointGround = new GroundSample[1];
        readonly double[] _stemQ = new double[4];
        readonly bool[] _stemNear = new bool[4];
        readonly double[] _slotCuts = new double[8];

        // The stems' states, shared by every disc: key (0 empty), the steps they were last loaded, advanced and seen, lean, its sine
        // and cosine kept with it (2.13: taken once a lean), length, the jet on it and its lag, its top over its base, the jet's
        // swirl in its plane, at rest. One record a stem (w9 step budget, second pass): a step's few hundred stems, at slots spread
        // by their hashes, read a cache line or two each, not one in each of thirteen arrays.
        struct StemState
        {
            public ulong Key;
            public long Loaded, Advanced, Seen;
            public double Phi, Sin, Cos, Len, Q, QLag, Top, Swirl;
            public bool Rest;
        }

        readonly StemState[] _st = new StemState[StateSlots];

        // The field's squares: key, the ground's height and normal at the centre, the stems per m² reaching h_ref per layer.
        readonly long[] _qKey = new long[SquareSlots];
        readonly double[] _qGround = new double[SquareSlots], _qNx = new double[SquareSlots], _qNz = new double[SquareSlots], _qNy = new double[SquareSlots];
        readonly double[] _qDensity = new double[SquareSlots * Layers];

        // The layers: kind, tallest top, and the share of stems at h_ref still reaching h_ref + j·StemReachStep.
        readonly StemKind[] _layerKind = new StemKind[Layers];
        readonly double[] _layerTop = new double[Layers];
        readonly bool[] _layerReach = new bool[Layers];
        readonly double[] _reach = new double[Layers * ReachEntries];

        // The stems cached about each rotor: StemCacheSize slots each.
        readonly Stalk[] _stalks = new Stalk[4 * StemCacheSize];
        readonly int[] _stalkCount = new int[4];
        readonly Double3[] _cacheAt = new Double3[4];
        readonly bool[] _cacheValid = new bool[4];
        readonly MicroElement[] _elements = new MicroElement[StemCacheSize];

        // The episodes and the feed's field window, per rotor.
        readonly double[] _episodeFrac = new double[4], _episodeWork = new double[4], _episodeCuts = new double[4], _episodeDiameter = new double[4];
        readonly double[] _episodeKind = new double[4 * 7];
        readonly long[] _episodeLast = new long[4];
        readonly bool[] _episodeOn = new bool[4];
        Corners _sumCuts = default, _sumLength = default, _sumRadius = default, _lastAngle = default, _fieldCuts = default, _fieldLength = default,
            _fieldRadius = default, _fieldAngle = default;

        // The step's per rotor sums for the log and the feed.
        readonly double[] _stepCuts = new double[4], _stepLength = new double[4], _stepRadius = new double[4], _stepWork = new double[4],
            _stepDiameter = new double[4], _stepKind = new double[4 * 7];

        void InitStems()
        {
            _stemsOn = _spec.Stems && _spec.Surfaces != null;
            for (int i = 0; i < SquareSlots; i++) _qKey[i] = long.MinValue;
            if (!_stemsOn) return;
            CoverRow[] rows = _spec.Surfaces.Cover;
            for (int k = 0; k < rows.Length && k < _coverSets.Length; k++) _coverSets[k] = _contacts.Cover((CoverKind)k);
        }

        /// <summary>
        /// The stems read the rotors' wash with distance (w9-village-physics 2.13): the line's law (RotorAir, p7d-tether design 19)
        /// from table's rotor_air rows, with the line's switches (recirculation: the fountain and the ground vortex; body: the
        /// drone's shapes over the fountain; cutOff: the 4 R known-wrong), begun each step at the motors' hubs with the wind.
        /// FlightCore calls it at construction; without it the stems fly 2.12's undiminished column.
        /// </summary>
        public void UseWash(TetherTable table, PropulsionSpec propulsion, WindField wind, bool recirculation, bool body, bool cutOff)
        {
            if (!_stemsOn) return;
            _air = new RotorAir(table, _radius, recirculation, body ? _contacts : null, cutOff);
            _wind = wind ?? throw new ArgumentNullException(nameof(wind));
            _rho = table.Number("fiber.air_density_kg_m3");
            for (int i = 0; i < 4; i++)
            {
                _motor[i] = propulsion.Motors[i].Position;
                // The discs' hubs are the motors' (drone.json): their ground is the stems' own sample; else its own.
                _washDisc[i] = _discOf[i] >= 0 && _spec.Shapes[_discOf[i]].A.X == _motor[i].X && _spec.Shapes[_discOf[i]].A.Y == _motor[i].Y
                    && _spec.Shapes[_discOf[i]].A.Z == _motor[i].Z;
            }
            _sUx = new double[StateSlots];
            _sUy = new double[StateSlots];
            _sUz = new double[StateSlots];
            _sAir = new long[StateSlots];
            for (int i = 0; i < StateSlots; i++) _sAir[i] = -1;
        }

        // A stem's tissue from its cover set at diameter d and modulus E.
        static Tissue TissueOf(in CoverSet set, double d, double modulus, ushort code)
        {
            double di = d * (1 - 2 * set.WallShare), d2 = d * d, di2 = di * di;
            double area = Pi / 4 * (d2 - di2), ei = modulus * Pi / 64 * (d2 * d2 - di2 * di2), mu = set.Density * area, cut = set.CutEnergy * area;
            // v_c,sharp = [F_c·√d / (2√(2π)·(EI·μ³)^¼)]^(2/3), F_c = e_s·A_w/d (stem-properties.md §2.2).
            double inertia = DetMath.Sqrt(DetMath.Sqrt(ei * mu * mu * mu));
            double ratio = cut / d * DetMath.Sqrt(d) / (2 * DetMath.Sqrt(TwoPi) * inertia), cube = DetMath.Cbrt(ratio);
            return new Tissue
            {
                Known = true, Code = code, D = d, Area = area, Mu = mu, EI = ei, Kappa0 = StemDrag * d / (8 * ei), Vc = EdgeFactor * cube * cube, Cut = cut,
            };
        }

        // A hash's i-th uniform in [0, 1).
        static double Uniform(ulong id, ulong i) => (FlightRandom.Mix64(unchecked(id + (i + 1) * 0x9E3779B97F4A7C15UL)) >> 11) * (1.0 / 9007199254740992.0);

        // ⌊v⌋ for a cell index, by a truncation and a compare (basic operations only).
        static int Floor(double v)
        {
            int t = (int)v;
            return v < t ? t - 1 : t;
        }

        // ---- the stems' states ------------------------------------------------------------------------------------------------

        // The state's slot of stem id (inserted at its drawn length and natural lean if new; when its probe is full, the slot seen
        // longest ago is taken over).
        int State(ulong id, double length, double phi0, long step)
        {
            int home = (int)(FlightRandom.Mix64(id) & (StateSlots - 1)), oldest = home;
            for (int p = 0; p < StateProbe; p++)
            {
                int i = (home + p) & (StateSlots - 1);
                if (_st[i].Key == id) return i;
                if (_st[i].Key == 0)
                {
                    oldest = i;
                    break;
                }
                if (_st[i].Seen < _st[oldest].Seen) oldest = i;
            }
            ref StemState t = ref _st[oldest];
            t.Key = id;
            t.Phi = phi0;
            DetMath.SinCos(phi0, out t.Sin, out t.Cos);
            t.Len = length;
            t.Q = t.QLag = t.Swirl = 0;
            t.Rest = false;
            t.Top = double.NaN;
            t.Loaded = t.Advanced = -1;
            t.Seen = step;
            if (_sAir != null) _sAir[oldest] = -1;
            return oldest;
        }

        // A cached stem's slot, found again if another stem took it over.
        int SlotOf(ref Stalk k, long step)
        {
            if (_st[k.Slot].Key != k.Id) k.Slot = State(k.Id, k.Length, k.Phi0, step);
            _st[k.Slot].Seen = step;
            return k.Slot;
        }

        // ---- the field's squares and layers ---------------------------------------------------------------------------------

        // The square (ix, iz)'s slot, asked of the world if not yet known: its ground and the stems per m² reaching h_ref per layer.
        int Square(int ix, int iz)
        {
            long key = ((long)ix << 32) ^ (uint)iz;
            int home = (int)(FlightRandom.Mix64((ulong)key) & (SquareSlots - 1));
            int at = home;
            for (int p = 0; p < SquareProbe; p++)
            {
                int i = (home + p) & (SquareSlots - 1);
                if (_qKey[i] == key) return i;
                if (_qKey[i] == long.MinValue)
                {
                    at = i;
                    break;
                }
            }
            var centre = new XZ((ix + 0.5) * StemSquare, (iz + 0.5) * StemSquare);
            _point[0] = centre;
            _world.SampleGround(_point, _pointGround);
            _qKey[at] = key;
            _qGround[at] = _pointGround[0].Height;
            Double3 n = _pointGround[0].Normal;
            bool flat = !(n.Y > 0.1);
            _qNx[at] = flat ? 0 : n.X;
            _qNz[at] = flat ? 0 : n.Z;
            _qNy[at] = flat ? 1 : n.Y;
            for (int l = 0; l < Layers; l++) _qDensity[at * Layers + l] = 0;
            int count = _world.StemsNear(centre, PointRadius, StemReference, _patches);
            if (count > _patches.Length) count = _patches.Length;
            double inverse = 1 / (Pi * PointRadius * PointRadius);
            bool unseen = false;
            for (int p = 0; p < count; p++)
            {
                StemPatch patch = _patches[p];
                if (patch.Layer >= Layers) continue;
                _qDensity[at * Layers + patch.Layer] += patch.Density * patch.Area * inverse; // the circle's mean on a cell's edge
                _layerKind[patch.Layer] = patch.Kind;
                _layerTop[patch.Layer] = patch.HeightMax;
                unseen |= !_layerReach[patch.Layer];
            }
            if (unseen) Reach(centre);
            return at;
        }

        // The layers seen at centre for the first time: the share of their stems at h_ref still reaching each height, from the
        // field there (the same in every cell: the field is the cell's count × the layer's reach), to 1e-9.
        void Reach(XZ centre)
        {
            Span<double> base0 = stackalloc double[Layers];
            Span<bool> fresh = stackalloc bool[Layers];
            Span<double> here = stackalloc double[Layers];
            base0.Clear();
            fresh.Clear();
            for (int j = 0; j < ReachEntries; j++)
            {
                int count = _world.StemsNear(centre, PointRadius, StemReference + j * StemReachStep, _patches);
                if (count > _patches.Length) count = _patches.Length;
                here.Clear();
                for (int p = 0; p < count; p++)
                    if (_patches[p].Layer < Layers) here[_patches[p].Layer] += _patches[p].Density * _patches[p].Area;
                for (int l = 0; l < Layers; l++)
                {
                    if (j == 0)
                    {
                        fresh[l] = !_layerReach[l] && here[l] > 0;
                        base0[l] = here[l];
                    }
                    if (!fresh[l]) continue;
                    double share = here[l] / base0[l];
                    _reach[l * ReachEntries + j] = (long)(share * 1e9 + 0.5) * 1e-9;
                }
            }
            for (int l = 0; l < Layers; l++)
                if (fresh[l]) _layerReach[l] = true;
        }

        // The drawn length of a stem of layer l at the share s of its layer's stems at h_ref (the shares fall with height).
        double LengthAt(int l, double s)
        {
            int at = l * ReachEntries;
            for (int j = 0; j + 1 < ReachEntries; j++)
            {
                double a = _reach[at + j], b = _reach[at + j + 1];
                if (b > s) continue;
                double u = a > b ? (a - s) / (a - b) : 0;
                return StemReference + (j + u) * StemReachStep;
            }
            return StemReference + (ReachEntries - 1) * StemReachStep;
        }

        // ---- the caches -------------------------------------------------------------------------------------------------------

        // Rotor c's cache about hub: the field's stems based within the disc and the refresh's margin (where the world has a
        // ground height), then S04's elements.
        void Cache(int c, Double3 hub, bool field, long step)
        {
            _cacheAt[c] = hub;
            _cacheValid[c] = true;
            double reach = _radius + StemCacheRefresh + 0.02, reach2 = reach * reach;
            int m = 0, at = c * StemCacheSize;
            int x0 = Floor((hub.X - reach) / StemSquare), x1 = field ? Floor((hub.X + reach) / StemSquare) : x0 - 1;
            int z0 = Floor((hub.Z - reach) / StemSquare), z1 = Floor((hub.Z + reach) / StemSquare);
            for (int iz = z0; iz <= z1; iz++)
                for (int ix = x0; ix <= x1; ix++)
                {
                    int q = Square(ix, iz);
                    for (int l = 0; l < Layers; l++)
                    {
                        double density = _qDensity[q * Layers + l] * StemDensityScale;
                        if (!(density > 0) || !_layerReach[l]) continue;
                        int k = (int)_layerKind[l] + (int)CoverKind.GrassBlade - 1;
                        if (k < (int)CoverKind.GrassBlade || k > (int)CoverKind.BurntCulm) continue;
                        CoverSet set = _coverSets[k];
                        if (!(set.Diameter > 0) || !(set.CutEnergy > 0)) continue;
                        ulong cell = FlightRandom.Mix64(unchecked(((ulong)(uint)ix << 32) ^ (uint)iz ^ ((ulong)l << 59)));
                        double count = density * StemSquare * StemSquare;
                        int K = (int)(count + Uniform(cell, 0));
                        for (int s = 0; s < K && m < StemCacheSize; s++)
                        {
                            ulong id = FlightRandom.Mix64(unchecked(cell + (ulong)(s + 1) * 0xD1B54A32D192ED03UL)) | 1;
                            double x = (ix + Uniform(id, 0)) * StemSquare, z = (iz + Uniform(id, 1)) * StemSquare;
                            double dx = x - hub.X, dz = z - hub.Z;
                            if (dx * dx + dz * dz > reach2) continue;
                            double length = LengthAt(l, (s + Uniform(id, 2)) / K);
                            double phi0 = StemNaturalLean * Uniform(id, 3), azimuth = TwoPi * Uniform(id, 4);
                            double cx = (ix + 0.5) * StemSquare, cz = (iz + 0.5) * StemSquare;
                            double y = _qGround[q] - (_qNx[q] * (x - cx) + _qNz[q] * (z - cz)) / _qNy[q];
                            // d = d₁·L^(1/1.53): the kind's 1 m stem's diameter (the row's diameter_m) at the stem's own length.
                            double d = set.Diameter * StemDiameterScale * DetMath.Exp(StemAllometry * DetMath.Log(length));
                            DetMath.SinCos(azimuth, out double sa, out double ca);
                            Add(ref _stalks[at + m++], id, new Double3(x, y, z), new Double3(ca, 0, sa), length, length, phi0,
                                TissueOf(set, d, 0.5 * (set.ModulusMin + set.ModulusMax), (ushort)(k + 1)), step);
                        }
                    }
                }
            if (_coverSets.Length > (int)CoverKind.Stem && (_coverSets[(int)CoverKind.Straw].CutEnergy > 0 || _coverSets[(int)CoverKind.Stem].CutEnergy > 0))
            {
                int n = _world.MicroDetailNear(hub, reach, MicroKind.Straw | MicroKind.Stem, _elements);
                if (n > _elements.Length) n = _elements.Length;
                for (int e = 0; e < n && m < StemCacheSize; e++)
                {
                    MicroElement el = _elements[e];
                    CoverKind kind = el.Kind == MicroKind.Stem ? CoverKind.Stem : CoverKind.Straw;
                    CoverSet set = _coverSets[(int)kind];
                    if (!(set.CutEnergy > 0) || !(el.Diameter > 0)) continue;
                    // The element's modulus in its row's range by its Id's hash: the fiber's rule (world-derived, never a flight draw).
                    double u = (FlightRandom.Mix64(el.Id) >> 11) * (1.0 / 9007199254740992.0);
                    double modulus = set.ModulusMin + (set.ModulusMax - set.ModulusMin) * u;
                    double top = DetMath.Max(el.P0.Y, DetMath.Max(el.P1.Y, el.P2.Y));
                    double length = (el.P1 - el.P0).Length() + (el.P2 - el.P1).Length();
                    ulong id = FlightRandom.Mix64(el.Id ^ 0x5304_5304_5304_5304UL) | 1;
                    // Its lean's direction: its drawn shape's, or by its hash when drawn upright.
                    Double3 lean = el.P2 - el.P0;
                    lean = new Double3(lean.X, 0, lean.Z);
                    double across = lean.Length();
                    if (across > 1e-9) lean = lean * (1 / across);
                    else
                    {
                        DetMath.SinCos(TwoPi * Uniform(id, 4), out double sa, out double ca);
                        lean = new Double3(ca, 0, sa);
                    }
                    double rise = top - el.P0.Y, phi0 = length > rise && length > 0 ? DetMath.Acos(DetMath.Min(1, rise / length)) : 0;
                    Add(ref _stalks[at + m++], id, el.P0, lean, length, rise, phi0,
                        TissueOf(set, el.Diameter * StemDiameterScale, modulus, (ushort)((int)kind + 1)), step);
                }
            }
            _stalkCount[c] = m;
        }

        void Add(ref Stalk k, ulong id, Double3 basePoint, Double3 lean, double length, double rise, double phi0, in Tissue t, long step)
        {
            k.Base = basePoint;
            k.Lean = lean;
            k.Length = length;
            k.Rise = rise;
            k.Phi0 = phi0;
            DetMath.SinCos(phi0, out _, out k.CosPhi0);
            k.T = t;
            k.MuG8 = t.Mu * Gravity / (8 * t.EI);
            k.Tau0 = 1 / (1.875 * 1.875 * DetMath.Sqrt(t.EI / t.Mu));
            k.Id = id;
            k.Slot = State(id, length, phi0, step);
        }

        // A stem's top over its base: leaning, L·cos φ (its drawn shape's rise at most); folded by a jet, its arch's crest.
        double TopOf(in Stalk k, int s)
        {
            ref readonly StemState t = ref _st[s];
            double len = t.Len, cp = t.Cos;
            double rise = k.Rise * (len / k.Length), top = DetMath.Min(rise, len * cp), q = t.QLag;
            // A stem standing to its natural lean's top or higher stands over any crest (the crest is at most that): no root.
            if (q > 0 && top < DetMath.Min(rise, len * k.CosPhi0))
            {
                double lb = DetMath.Cbrt(k.T.EI / (StemDrag * k.T.D * q));
                top = DetMath.Max(top, DetMath.Min(DetMath.Min(rise, len * k.CosPhi0), StemFoldedCrest * lb));
            }
            return top;
        }

        // A stem under a disc in its record: standing or folded past 45°, and among the tall (drawn 1 m or longer).
        void Count(ref StemRecord rec, in Stalk k, int s)
        {
            bool folded = _st[s].Phi > Pi / 4, tall = !(k.Length < 1);
            rec.Under++;
            if (folded) rec.Folded++;
            if (!tall) return;
            rec.UnderTall++;
            if (folded) rec.FoldedTall++;
        }

        // ---- the wash (2.13) ------------------------------------------------------------------------------------------------

        // The stems' rotor air this step, with the line's inputs (FiberLine's Begin): each motor's hub, its rotor's mean thrust,
        // the wind at it less its velocity (over a hole the step's own frame, NaN), the ground under it (the soil under a hole),
        // and the drone's shapes over the fountain.
        void BeginWash(in FlightState now, in Corners thrust, Double3 up)
        {
            Quat q = now.Attitude;
            bool own = false;
            for (int i = 0; i < 4; i++)
            {
                _washHub[i] = now.Position + q.Rotate(_motor[i]);
                _washXZ[i] = new XZ(_washHub[i].X, _washHub[i].Z);
                own |= !_washDisc[i];
            }
            if (own) _world.SampleGround(_washXZ, _washSample);
            for (int i = 0; i < 4; i++)
            {
                GroundSample g = _washDisc[i] ? _hubGround[i] : _washSample[i];
                bool hole = (g.Flags & GroundFlags.Hole) != 0;
                _washGround[i] = hole ? _world.SoilUnder(_washHub[i], _soilRay, _soilHit, out _) : g.Height;
                Double3 v = now.Velocity + q.Rotate(Double3.Cross(now.BodyRate, _motor[i]));
                _washAir[i] = _wind.At(_washHub[i], hole ? double.NaN : g.Height) - v;
                _washThrust[i] = thrust[i];
            }
            _washUp = up;
            if (_air.BodyBlocks) _air.PlaceBody(now.Position, q);
            _air.Begin(_washHub, up, _washThrust, _washAir, _washGround);
        }

        // Stem k's air (slot s) at ⅔·L·cos φ over its base on its vertical, with its near column's swirl, taken again every
        // StemAirRefresh steps by its slot (at once when new or stale) and held between; returns its in-plane pressure,
        // ½ρ·(u_lean² + u_y²), Pa.
        double StemAir(in Stalk k, int s, long step)
        {
            long last = _sAir[s];
            if (!(last >= 0 && step - last < StemAirRefresh && (step + s) % StemAirRefresh != 0))
            {
                _sAir[s] = step;
                double cp = _st[s].Cos;
                var p = new Double3(k.Base.X, k.Base.Y + 2.0 / 3 * _st[s].Len * cp, k.Base.Z);
                Double3 u = _air.At(p, k.Base.Y, out _);
                // A near column turns with its rotor (2.12): tan β = Q/(2·T·r̄) times the column's speed, along the turning.
                for (int j = 0; j < 4; j++)
                {
                    double w = _air.Column(j, p), t = _propulsion.Thrust(j);
                    if (!(w > 0) || !(t > 0)) continue;
                    Double3 rel = p - _washHub[j], flat = rel - _washUp * Double3.Dot(rel, _washUp);
                    double r2 = Double3.Dot(flat, flat);
                    if (!(r2 > 0)) continue;
                    double tan = DetMath.Abs(_propulsion.Torque(j)) / (2 * t * StemSwirlRadius * _radius);
                    u = u + Double3.Cross(_washUp, flat) * (-_spin[j] * w * tan / DetMath.Sqrt(r2));
                }
                _sUx[s] = u.X;
                _sUy[s] = u.Y;
                _sUz[s] = u.Z;
            }
            double ul = _sUx[s] * k.Lean.X + _sUz[s] * k.Lean.Z, uy = _sUy[s];
            return 0.5 * _rho * (ul * ul + uy * uy);
        }

        // ---- the step --------------------------------------------------------------------------------------------------------

        // Before the discs' passes: the four hubs' ground, each rotor's cache, the jet on each stem, and every cached stem's lean;
        // with the wash (2.13), the stems' rotor air begun first from the step's mean thrust.
        void StemsBegin(in FlightState now, in Corners thrust)
        {
            _tip = default;
            if (!_stemsOn) return;
            Array.Clear(_stepCuts, 0, 4);
            Array.Clear(_stepLength, 0, 4);
            Array.Clear(_stepRadius, 0, 4);
            Array.Clear(_stepWork, 0, 4);
            Array.Clear(_stepDiameter, 0, 4);
            Array.Clear(_stepKind, 0, _stepKind.Length);
            for (int c = 0; c < 4; c++) StemLast[c] = default;
            Span<double> hx = stackalloc double[4], hy = stackalloc double[4], hz = stackalloc double[4];
            for (int c = 0; c < 4; c++)
            {
                Double3 hub = _discOf[c] >= 0 ? now.Position + now.Attitude.Rotate(_spec.Shapes[_discOf[c]].A) : now.Position;
                _hubXZ[c] = new XZ(hub.X, hub.Z);
                hx[c] = hub.X;
                hy[c] = hub.Y;
                hz[c] = hub.Z;
            }
            _world.SampleGround(_hubXZ, _hubGround);
            long step = now.Step;
            Double3 axis = now.Attitude.Rotate(Up);
            double R = _radius, dt = FlightCore.Dt;
            if (_air != null)
            {
                bool any = false;
                for (int c = 0; c < 4; c++)
                {
                    double ground = _hubGround[c].Height;
                    any |= _discOf[c] >= 0 && (!(ground > double.NegativeInfinity) || hy[c] - ground < StemWakeTop);
                }
                if (any) BeginWash(now, thrust, axis);
            }
            for (int c = 0; c < 4; c++)
            {
                var hub = new Double3(hx[c], hy[c], hz[c]);
                double ground = _hubGround[c].Height, h = hub.Y - ground;
                // A world without a ground height (the test range's plane) has no field, only its elements.
                bool field = ground > double.NegativeInfinity;
                _stemNear[c] = _discOf[c] >= 0 && (!field || h < StemWakeTop);
                StemLast[c].Height = h;
                if (!_stemNear[c])
                {
                    _stalkCount[c] = 0;
                    _cacheValid[c] = false;
                    continue;
                }
                if (!_cacheValid[c] || (hub - _cacheAt[c]).Length() > StemCacheRefresh) Cache(c, hub, field, step);
                StemLast[c].Cached = _stalkCount[c];
                if (_air != null)
                {
                    // The wash (2.13): each stem's air once a step, the field's, not a disc's; the stems under the disc counted.
                    double load = 0;
                    for (int e = 0; e < _stalkCount[c]; e++)
                    {
                        ref Stalk k = ref _stalks[c * StemCacheSize + e];
                        int s = SlotOf(ref k, step);
                        ref StemState t = ref _st[s];
                        if (t.Loaded != step)
                        {
                            t.Loaded = step;
                            t.Q = StemLean ? StemAir(k, s, step) : 0; // the known-wrong folds nothing, as 2.12's
                        }
                        Double3 rel = k.Base - hub, flat = rel - axis * Double3.Dot(rel, axis);
                        if (!(Double3.Dot(flat, flat) <= R * R)) continue;
                        Count(ref StemLast[c], k, s);
                        load += t.Q;
                    }
                    StemLast[c].Load = StemLast[c].Under > 0 ? load / StemLast[c].Under : 0;
                    continue;
                }
                // The slipstream's pressure under the disc, q = T/A (momentum theory's contracted wake), on the stems based under it,
                // and its swirl's angle off the axis, tan β = Q/(2·T·r̄) (momentum theory: the wake's angular momentum flux is the
                // torque, w_θ = Q/(ρ·2v_i·A·r̄) over its axial 2v_i; r̄ = 2R/3, assumed), turning with the rotor.
                double thrust1 = _propulsion.Thrust(c), q = StemLean && thrust1 > 0 ? thrust1 / (Pi * R * R) : 0;
                double swirl = q > 0 ? DetMath.Abs(_propulsion.Torque(c)) / (2 * thrust1 * StemSwirlRadius * R) : 0, spin = _spin[c];
                _stemQ[c] = q;
                StemLast[c].Load = q;
                for (int e = 0; e < _stalkCount[c]; e++)
                {
                    ref Stalk k = ref _stalks[c * StemCacheSize + e];
                    int s = SlotOf(ref k, step);
                    ref StemState t = ref _st[s];
                    if (t.Loaded != step)
                    {
                        t.Loaded = step;
                        t.Q = 0;
                    }
                    Double3 rel = k.Base - hub, flat = rel - axis * Double3.Dot(rel, axis);
                    double r2 = Double3.Dot(flat, flat);
                    if (r2 <= R * R) Count(ref StemLast[c], k, s);
                    if (!(q > t.Q)) continue;
                    if (!(r2 <= R * R)) continue;
                    t.Q = q;
                    // The swirl's tilt in the stem's own plane of lean: tan β times the wake's turning direction along its lean.
                    Double3 turning = r2 > 0 ? Double3.Cross(axis, flat) * (-spin / DetMath.Sqrt(r2)) : default;
                    t.Swirl = swirl * Double3.Dot(turning, k.Lean);
                }
            }
            // Each stem's lean once a step: φ' = −m(φ)/τ, semi-implicit in its slope; its jet's lag over τ for the arch.
            for (int c = 0; c < 4; c++)
            {
                if (!_stemNear[c]) continue;
                for (int e = 0; e < _stalkCount[c]; e++)
                {
                    ref Stalk k = ref _stalks[c * StemCacheSize + e];
                    int s = k.Slot;
                    ref StemState t = ref _st[s];
                    if (t.Advanced == step) continue;
                    t.Advanced = step;
                    double q = t.Loaded == step ? t.Q : 0, phi = t.Phi;
                    if (!(q > 0) && !(t.QLag > 0) && t.Rest) continue; // at rest, its top known
                    double len = t.Len, l3 = len * len * len, tau = len * len * k.Tau0;
                    double G = k.MuG8 * l3, m, slope, rate = dt / tau;
                    double sp = t.Sin, cp = t.Cos;
                    if (_air != null)
                    {
                        // 2.13: the held air in the stem's plane of lean, u_φ = u·ê_φ along the lean's growth and u·â along the
                        // stem; m(φ) = (φ − φ₀) − K·|u_φ|·u_φ − G·sin φ, K = ½ρ·C_d·d·L³/(8EI), its slope 1 + 2K·|u_φ|·(u·â) − G·cos φ.
                        double ul = 0, uy = 0;
                        if (q > 0)
                        {
                            ul = _sUx[s] * k.Lean.X + _sUz[s] * k.Lean.Z;
                            uy = _sUy[s];
                        }
                        double K = 0.5 * _rho * k.T.Kappa0 * l3, uphi = ul * cp - uy * sp, ua = ul * sp + uy * cp, au = DetMath.Abs(uphi);
                        m = phi - k.Phi0 - K * au * uphi - G * sp;
                        slope = 1 + 2 * K * au * ua - G * cp;
                    }
                    else
                    {
                        double A = q * k.T.Kappa0 * l3, beta = q > 0 ? t.Swirl : 0;
                        // m(φ) = (φ − φ₀) − A·sin(φ + β)·|sin(φ + β)| − G·sin φ: the jet tilted β in the stem's plane, its normal share.
                        // Without a tilt φ + β is φ (or +0 for −0, whose sine and cosine are +0's), whose sine and cosine are kept
                        // with it: the same bits, one SinCos less for every stem out of the jet (w9 step budget, second pass).
                        double sj = sp, cj = cp;
                        if (beta != 0) DetMath.SinCos(phi + beta, out sj, out cj);
                        double aj = DetMath.Abs(sj);
                        m = phi - k.Phi0 - A * sj * aj - G * sp;
                        slope = 1 - 2 * A * aj * cj - G * cp;
                    }
                    double next = DetMath.Min(Pi / 2, DetMath.Max(-Pi / 2, phi - m * rate / (1 + DetMath.Max(0, slope) * rate)));
                    t.Rest = !(q > 0) && DetMath.Abs(next - phi) < 1e-12;
                    t.Phi = next;
                    DetMath.SinCos(next, out t.Sin, out t.Cos);
                    double lag = t.QLag + (q - t.QLag) * (dt / (tau + dt));
                    t.QLag = lag > 1e-9 ? lag : 0;
                    t.Top = TopOf(k, s);
                }
            }
        }

        // One rotor's stems this step (disc shape i, corner c), its blades sweeping from start over sweep. True when a blade changed.
        bool Stems(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 axis, double start, double sweep,
            StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            ref StemRecord rec = ref StemLast[c];
            if (!_stemsOn || !_stemNear[c]) return false;
            double omega = now.RotorSpeed[c];
            if (!(omega > 0) || !(axis.Y > 0.2)) return false;
            double R = _radius, rtip = 0, rroot = (_b.Stations[0] - 0.5 * _span) * R, s = _spin[c];
            for (int b = 0; b < _blades; b++) rtip = DetMath.Max(rtip, _propulsion.Rotor(c).Blade(b).TipRadius * R);
            Double3 hub = now.Position + now.Attitude.Rotate(d.A);
            double work = 0, axial = 0;
            Double3 inPlane = default;
            bool changed = false;
            for (int e = 0; e < _stalkCount[c]; e++)
            {
                ref Stalk k = ref _stalks[c * StemCacheSize + e];
                Double3 rel = k.Base - hub, flat = rel - axis * Double3.Dot(rel, axis);
                double r2 = Double3.Dot(flat, flat);
                if (r2 > rtip * rtip || r2 < rroot * rroot) continue;
                int slot = k.Slot;
                ref StemState t = ref _st[slot];
                double plane = hub.Y - (axis.X * rel.X + axis.Z * rel.Z) / axis.Y, top = k.Base.Y + t.Top;
                if (!(top > plane) || !(k.Base.Y < plane)) continue;
                rec.Over++;
                double r = DetMath.Sqrt(r2);
                Double3 dir = flat * (1 / r);
                // The blade's speed across the stem's axis: ω·r·√(1 − (t̂·â)²), t̂ the blade's motion there, â the stem's axis.
                double sp = t.Sin, cp = t.Cos;
                Double3 motion = Double3.Cross(axis, dir) * -s, stem = new Double3(k.Lean.X * sp, cp, k.Lean.Z * sp);
                double along = Double3.Dot(motion, stem), across = omega * r * DetMath.Sqrt(DetMath.Max(0, 1 - along * along));
                if (StemCritical && !(across >= k.T.Vc)) continue; // pushed aside
                double psi = Azimuth(now.Attitude, dir, c);
                int by = -1;
                for (int b = 0; b < _blades && by < 0; b++)
                {
                    double ahead = Wrap(psi - (start + TwoPi * b / _blades));
                    if (ahead > 0 && ahead <= sweep) by = b;
                }
                if (by < 0) continue;
                // Fresh if never cut; else a stub sliced by the drop over the plane. The cut leaves it to the plane along its leg.
                double len = t.Len, drop = top - plane, rise = plane - k.Base.Y, leg = DetMath.Max(cp, StemStubCos);
                bool fresh = !(len < k.Length);
                double slice = fresh ? 0 : DetMath.Min(1, drop / k.T.D), piece = fresh ? DetMath.Max(0, len - rise / leg) : drop, was = work;
                changed |= Part(i, d, c, by, ref now, body, hub, axis, psi, r, k.T, fresh ? 1 : 0, slice, fresh ? 0 : drop, 1, piece, events,
                    ref impulse, ref angular, ref work, ref inPlane, ref axial);
                t.Len = DetMath.Min(len, rise / leg);
                t.Top = TopOf(k, slot);
                if (fresh)
                {
                    rec.Fresh += 1;
                    rec.FreshWork += work - was;
                    if (r2 * 2 > R * R) rec.FreshRing += 1; // outside the near column's R/√2 (momentum theory's far wake)
                }
                else rec.Sliced += slice;
                rec.Cuts += 1;
                if (!(now.RotorSpeed[c] > 0)) break;
                omega = now.RotorSpeed[c];
            }
            rec.Work += work;
            rec.InPlane = rec.InPlane + inPlane;
            rec.Axial += axial;
            return changed;
        }

        // After every disc's passes: each rotor's episode (a resting one's too) and the feed's field window.
        void StemsEnd(in FlightState now, StepEvents events)
        {
            if (!_stemsOn) return;
            for (int c = 0; c < 4; c++)
                if (_discOf[c] >= 0) Episode(_spec.Shapes[_discOf[c]], c, now, events);
            if (now.Step % FlightCore.FieldSteps != 0) return;
            // A whole field: its sums published for the feed (steps 20k - 19 to 20k).
            for (int r = 0; r < 4; r++)
            {
                double n = _sumCuts[r];
                _fieldCuts[r] = n;
                _fieldLength[r] = n > 0 ? _sumLength[r] / n : 0;
                _fieldRadius[r] = n > 0 ? _sumRadius[r] / n : 0;
                _fieldAngle[r] = _lastAngle[r];
            }
            _sumCuts = _sumLength = _sumRadius = default;
        }

        // One part of a rotor's stems on blade b at azimuth psi (rotor's), radius r: cuts fresh stems, slices stubs (cut
        // equivalents) with their flung length, their tissue, the step's whole count and the pieces' length; its work off the
        // rotor, its impulses on the body, its force through the blade (Breaks). True when the blade broke.
        bool Part(int i, in ContactShape d, int c, int b, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, double psi, double r,
            in Tissue t, double fresh, double slices, double flung, double cuts, double piece, StepEvents events, ref Double3 impulse, ref Double3 angular,
            ref double work, ref Double3 inPlane, ref double axialSum)
        {
            BladeState blade = _propulsion.Rotor(c).Blade(b);
            double R = _radius, tip = blade.TipRadius * R;
            if (!(r > 0) || !(r <= tip) || !(cuts > 0)) return false;
            double omega = now.RotorSpeed[c];
            if (!(omega > 0)) return false;
            int k = Station(r / R);
            double chord = _b.Chord[k] * blade.ChordShare[k], theta = _b.SectionAngle[k] + blade.FactoryPitch + blade.PitchSet[k];
            DetMath.SinCos(theta, out double st, out double ct);
            double band = chord * st + _b.Thickness[k] * ct, u = omega * r;
            double edge = (fresh + slices) * t.Cut, fling = t.Mu * (fresh * band + flung) * u * u, energy = edge + fling;
            // The stem's force on the blade's edge, through its sections (as a twig's).
            double f = _strength[c * _blades + b];
            int broke = Breaks(blade, f, r, r, t.Cut / t.D, 0, out _, out _);
            if (broke >= 0) energy += _b.FractureEnergy * f * _area[broke] * blade.ChordShare[broke];
            double J = _propulsion.Inertia(c), stored = 0.5 * J * omega * omega, share = 1;
            if (!(energy < stored))
            {
                share = stored / energy; // the rotor stops in the stems: the share it had the energy for is cut
                broke = -1;
            }
            double after = share < 1 ? 0 : DetMath.Sqrt(omega * omega - 2 * energy / J), lost = J * (omega - after);
            now.RotorSpeed[c] = after;
            // The impulses: the in-plane E/u against the blade's motion (the rotor's lost momentum over r, so the body is not paid
            // twice), the pitched face's share of the edge's along the axis, at the cut.
            double s = _spin[c];
            DetMath.SinCos(psi, out double sp, out double cp);
            Double3 dir = now.Attitude.Rotate(new Double3(cp, 0, s * sp)); // blade b along body (cos psi, 0, s sin psi), as Azimuth reads it
            Double3 motion = Double3.Cross(axis, dir) * -s, point = hub + dir * r;
            double push = band > 0 ? edge * share * (chord * ct / band) / u : 0;
            Double3 external = motion * -(lost / r) + axis * push, reaction = new Double3(0, -s * lost, 0);
            impulse = impulse + external;
            angular = angular + reaction + Double3.Cross(now.Attitude.InverseRotate(point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(external));
            inPlane = inPlane + motion * -(lost / r);
            axialSum += push;
            double used = share < 1 ? stored : energy;
            work += used;
            bool damaged = false;
            if (broke >= 0)
            {
                double before = Mass(blade);
                blade.Trim(_b.Stations[broke]);
                damaged = true;
                Damaged(i, d, b, StrikeMode.Break, WorldIds.Ground, 0, t.Code, WorldIds.NoSurface, point, blade.TipRadius, before - Mass(blade), _b.Stations[broke], events);
            }
            if (share < 1)
            {
                FlightEvent e = Of(EventKind.RotorJammed, d, WorldIds.Ground, 0, t.Code, WorldIds.NoSurface, point);
                e.Index = (byte)c;
                e.A = energy;
                e.B = stored;
                e.C = share;
                events.Add(e);
            }
            // The log: the blade's step in BladeStrikes, its whole stems counted from the running count.
            double counted = cuts * share;
            int slot = BladeStrikes.Slot(c, b);
            if (slot < BladeStrikes.Slots)
            {
                _slotCuts[slot] += counted;
                int whole = (int)_slotCuts[slot];
                _slotCuts[slot] -= whole;
                BladeStrike strike = now.BladeStrikes[slot];
                strike.AddStems(t.Code, used, external.Length(), t.D, edge * share, r, whole);
                now.BladeStrikes[slot] = strike;
            }
            _stepCuts[c] += counted;
            _stepLength[c] += counted * piece;
            _stepRadius[c] += counted * r;
            _stepWork[c] += used;
            _stepDiameter[c] += counted * t.D;
            _stepKind[c * 7 + t.Code - 1] += counted;
            _lastAngle[c] = now.RotorAngle[c];
            return damaged;
        }

        // The rotor's episode (StemEpisode) and the feed's field window, after its stems this step.
        void Episode(in ContactShape d, int c, in FlightState now, StepEvents events)
        {
            double cuts = _stepCuts[c];
            _episodeFrac[c] += cuts;
            int whole = (int)_episodeFrac[c];
            _episodeFrac[c] -= whole;
            if (cuts > 0 || _episodeOn[c])
            {
                _episodeWork[c] += _stepWork[c];
                _episodeCuts[c] += cuts;
                _episodeDiameter[c] += _stepDiameter[c];
                for (int k = 0; k < 7; k++) _episodeKind[c * 7 + k] += _stepKind[c * 7 + k];
            }
            if (whole > 0)
            {
                _episodeLast[c] = now.Step;
                if (!_episodeOn[c])
                {
                    _episodeOn[c] = true;
                    events.Add(EpisodeEvent(d, c, now, false));
                }
            }
            else if (_episodeOn[c] && now.Step - _episodeLast[c] >= (long)(StemQuietTime / FlightCore.Dt + 0.5))
            {
                events.Add(EpisodeEvent(d, c, now, true));
                _episodeOn[c] = false;
                _episodeWork[c] = _episodeCuts[c] = _episodeDiameter[c] = 0;
                for (int k = 0; k < 7; k++) _episodeKind[c * 7 + k] = 0;
            }
            _sumCuts[c] = _sumCuts[c] + cuts;
            _sumLength[c] = _sumLength[c] + _stepLength[c];
            _sumRadius[c] = _sumRadius[c] + _stepRadius[c];
        }

        FlightEvent EpisodeEvent(in ContactShape d, int c, in FlightState now, bool end)
        {
            int most = 0;
            for (int k = 1; k < 7; k++)
                if (_episodeKind[c * 7 + k] > _episodeKind[c * 7 + most]) most = k;
            FlightEvent e = Of(EventKind.StemEpisode, d, WorldIds.Ground, 0, (ushort)(most + 1), WorldIds.NoSurface,
                now.Position + now.Attitude.Rotate(d.A));
            e.Index = (byte)(c | (end ? 0x80 : 0));
            e.A = _episodeWork[c];
            e.B = _episodeCuts[c];
            e.C = _episodeCuts[c] > 0 ? _episodeDiameter[c] / _episodeCuts[c] : 0;
            return e;
        }

        /// <summary>The props' cut stems over the latest whole field, for the feed (FeedSignals.StemCuts…).</summary>
        public void StemSignals(ref FeedSignals s)
        {
            s.StemCuts = _fieldCuts;
            s.StemPieceLength = _fieldLength;
            s.StemCutRadius = _fieldRadius;
            s.StemBladeAngle = _fieldAngle;
        }
    }
}
