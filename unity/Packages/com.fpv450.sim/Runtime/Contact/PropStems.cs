using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// The props in standing stems (w9-village-physics 2.2, its design.md "## 2.2"; PHY-05, PHY-24, PHY-26): a map's stem field
    /// (IWorldQuery.StemsNear) and its drawn straws and stems (MicroDetailNear's S04 elements) under each turning disc, every step,
    /// after the shapes' passes. Engine-free, allocation-free in a step, DetMath only; nothing about it is drawn.
    /// - The slipstream lays them over: the load across a stem is q·C_d·d·cos²φ with q = T/A (momentum theory's contracted wake),
    ///   and it leans as a rigid link on a base spring 4EI/L, φ/cos²φ = q·C_d·d·L³/(8EI), its top at L·cos φ. The load follows the
    ///   rotor's q with PressTime pressing them and RiseTime letting them up.
    /// - A blade cuts a stem only where its speed across it, ω·r·cos φ, reaches the stem's critical speed v_c = k_edge·[F_c·√d /
    ///   (2√(2π)·(EI·μ³)^¼)]^(2/3), F_c = e_s·A_w/d (stem-properties.md §2.2); slower, the stem is pushed aside and nothing is taken.
    /// - A cut costs e_s·A_w and the swept band's fling μ·h_f·u² (h_f = c·sin θ + t·cos θ), a slice of a stub min(1, Δ/d)·e_s·A_w
    ///   and μ·Δ·u²; the rotor pays it, the body takes E/u against the blade's motion and the pitched face's E_cut·(c·cos θ/h_f)/u
    ///   along the axis.
    /// - The field's stems lie evenly over the disc: its blades share them, their in-plane impulses cancel. A disc moving into
    ///   stems meets them upright across its leading arc, ∝ cos(ψ − ψ_lead), each blade cutting what entered since the last.
    ///   An element is met by the blade crossing its azimuth, once a pass.
    /// - A stem cut stands lower: each rotor's field height cut to and stubs, every disc's trail over the field (where its stems
    ///   were cut, and to what), and for the elements a grid of the planes each stalk's ground was cut to.
    /// The step's cuts go to BladeStrikes (StrikeKind.Stem), a rotor's episode to the log (StemEpisode, start and end), the
    /// field's cut pieces to the feed (FeedSignals.StemCuts…); a stem's force goes through Breaks as a twig's does.
    /// </summary>
    public sealed partial class PropStrikes
    {
        /// <summary>A stem's drag coefficient across the flow: 1.15 (published: 1.0–1.3 for wires and cables, Engineering ToolBox;
        /// 1.28 a flat plate, NASA Glenn; stem-properties.md §2.4).</summary>
        public const double StemDrag = 1.15;

        /// <summary>The prop's edge factor on a sharp blade's critical speed: 3 (assumed, 2–4: between a sharp mower blade's 25 m/s
        /// and nylon line's 61–86 m/s on grass, the blade's nose being 0.13–0.65 mm round; stem-properties.md §2.2).</summary>
        public const double EdgeFactor = 3;

        /// <summary>The stems' load follows the rotor's q pressing them down in 0.04 s (derived: a 0.5 m culm's top driven over by
        /// w/μ ≈ 500 m/s² gets there in about 44 ms) and rising in 0.1 s (assumed: inside the 0.02–0.35 s quarter periods of 0.2 m
        /// stubble and a 0.6 m culm's first bending mode).</summary>
        public const double StemPressTime = 0.04, StemRiseTime = 0.1;

        /// <summary>The elements' cut grid's cell, m, and its slots (numerical: a stalk's ground; 4,096 cells of cut ground, the home
        /// slot overwritten when its probe finds none free).</summary>
        public const double StemGridCell = 0.05;
        const int StemGridSlots = 4096, StemGridProbe = 8;

        /// <summary>Each disc's trail over the stem field: an entry every 1 cm of its hub's travel, the last 64 kept (numerical: 0.64 m,
        /// twice the 0.32 m a rear disc runs behind a front one).</summary>
        public const double StemTrailStep = 0.01;
        const int StemTrailSize = 64;

        /// <summary>A rotor's element cache is asked again once its hub has moved this far, m (numerical), over the disc's radius and
        /// this margin again.</summary>
        public const double StemCacheRefresh = 0.1;
        const int StemCacheSize = 1024, StemPatchesMax = 64;

        /// <summary>A stem episode ends this long after its last whole stem, s (numerical: the forensics' grain).</summary>
        public const double StemQuietTime = 0.1;

        const int FieldKinds = 3, TableSize = 256;

        /// <summary>One kind's tissue at the flight's moisture: its section's wall area, mass per length, bending stiffness, the
        /// slipstream's lean per pascal C_d·d/(8EI), the critical speed and a cut's edge work.</summary>
        struct Tissue
        {
            public bool Known;
            public ushort Code;
            public double D, Area, Mu, Kappa0, Vc, Cut;
        }

        /// <summary>One cached element: its base, its length along its points and its top over its base, its tissue.</summary>
        struct Stalk
        {
            public Double3 Base;
            public double Length, Rise;
            public Tissue T;
            public bool Inside;
        }

        /// <summary>The last step's stems per rotor, for a test or a bench: the disc's height over the ground (m) and the height its
        /// field stems are cut to (m, +∞ for none), the stems' load (Pa), the step's fresh cuts, stub slices (cut equivalents) and
        /// entries cut (stems), all its cuts (stems, fractional), its work (J), the impulse it gave the body in the disc's plane
        /// (world N·s) and along the rotor's axis (N·s), and the elements met.</summary>
        public struct StemRecord
        {
            public double Height, Cut, Load, Fresh, Sliced, Entered, Cuts, Work, Axial;
            public Double3 InPlane;
            public int Elements;
        }

        /// <summary>The last step's stems per rotor (StemRecord), Corners' order.</summary>
        public readonly StemRecord[] StemLast = new StemRecord[4];

        /// <summary>The known-wrong's switches, for a test's comparison only (a flight never sets them): off, the slipstream lays
        /// no stem over, and a blade cuts every stem it meets whatever its speed (the audit's §5.2 law).</summary>
        public bool StemLean = true, StemCritical = true;

        /// <summary>Each corner's strikes' impulse along its rotor's axis this step, N·s (every kind: shapes, twigs, stems), which
        /// loads its arm's mode at the tip (AirframeModes.Strike, G8).</summary>
        public ref readonly Corners TipImpulse => ref _tip;

        Corners _tip;
        bool _stemsOn;
        readonly Tissue[] _tissue = new Tissue[7];                  // by CoverKind
        readonly double[] _leanTable = new double[TableSize + 1];   // φ at s = x/(1 + x), x = φ/cos²φ
        readonly double[] _root1 = new double[TableSize + 1], _root2 = new double[TableSize + 1]; // φ·cos φ = c, both branches
        double _rootMax, _rootPhi;
        readonly StemPatch[] _patches = new StemPatch[StemPatchesMax];
        readonly XZ[] _hubXZ = new XZ[4];
        readonly GroundSample[] _hubGround = new GroundSample[4];
        readonly double[] _stemQ = new double[4], _stemCut = { double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity };
        readonly double[] _stemDue = new double[4 * FieldKinds], _stemStubs = new double[4 * FieldKinds];
        readonly double[] _slotCuts = new double[8];
        readonly double[] _trailX = new double[4 * StemTrailSize], _trailZ = new double[4 * StemTrailSize], _trailY = new double[4 * StemTrailSize];
        readonly int[] _trailHead = new int[4], _trailCount = new int[4];
        readonly long[] _gridKey = new long[StemGridSlots];
        readonly double[] _gridY = new double[StemGridSlots];

        // The elements' caches, per rotor: StemCacheSize slots each.
        readonly Stalk[] _stalks = new Stalk[4 * StemCacheSize];
        readonly int[] _stalkCount = new int[4];
        readonly Double3[] _cacheAt = new Double3[4];
        readonly bool[] _cacheValid = new bool[4];
        readonly MicroElement[] _elements = new MicroElement[StemCacheSize];
        CoverSet _strawSet, _stemSet;

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
        readonly Double3[] _lastHub = new Double3[4];

        void InitStems()
        {
            _stemsOn = _spec.Stems && _spec.Surfaces != null;
            for (int i = 0; i < StemGridSlots; i++) _gridKey[i] = long.MinValue;
            if (!_stemsOn) return;
            CoverRow[] rows = _spec.Surfaces.Cover;
            for (int k = (int)CoverKind.GrassBlade; k <= (int)CoverKind.BurntCulm && k < rows.Length; k++)
            {
                CoverSet set = _contacts.Cover((CoverKind)k);
                if (set.Diameter > 0 && set.CutEnergy > 0) _tissue[k] = TissueOf(set, set.Diameter, 0.5 * (set.ModulusMin + set.ModulusMax), (ushort)(k + 1));
            }
            if (rows.Length > (int)CoverKind.Stem)
            {
                _strawSet = _contacts.Cover(CoverKind.Straw);
                _stemSet = _contacts.Cover(CoverKind.Stem);
            }

            // φ/cos²φ = x over s = x/(1 + x) ∈ [0, 1], and the two roots of φ·cos φ = c over c ∈ [0, max], by bisection at load.
            for (int j = 0; j <= TableSize; j++)
            {
                double s = (double)j / TableSize;
                if (j == TableSize)
                {
                    _leanTable[j] = Pi / 2;
                    continue;
                }
                double x = s / (1 - s), lo = 0, hi = Pi / 2;
                for (int n = 0; n < 60; n++)
                {
                    double m = 0.5 * (lo + hi);
                    DetMath.SinCos(m, out _, out double cm);
                    if (m < x * cm * cm) lo = m;
                    else hi = m;
                }
                _leanTable[j] = 0.5 * (lo + hi);
            }
            double a = 0, b = Pi / 2; // cos φ = φ·sin φ at the most φ·cos φ
            for (int n = 0; n < 60; n++)
            {
                double m = 0.5 * (a + b);
                DetMath.SinCos(m, out double sm, out double cm);
                if (cm > m * sm) a = m;
                else b = m;
            }
            _rootPhi = 0.5 * (a + b);
            DetMath.SinCos(_rootPhi, out _, out double cr);
            _rootMax = _rootPhi * cr;
            for (int j = 0; j <= TableSize; j++)
            {
                double c = _rootMax * j / TableSize;
                _root1[j] = Bisect(c, 0, _rootPhi, true);
                _root2[j] = Bisect(c, _rootPhi, Pi / 2, false);
            }
        }

        // The root of φ·cos φ = c between lo and hi, rising (the short branch) or falling.
        static double Bisect(double c, double lo, double hi, bool rising)
        {
            for (int n = 0; n < 60; n++)
            {
                double m = 0.5 * (lo + hi);
                DetMath.SinCos(m, out _, out double cm);
                if (m * cm < c == rising) lo = m;
                else hi = m;
            }
            return 0.5 * (lo + hi);
        }

        // A kind's tissue from its cover set at diameter d and modulus E.
        static Tissue TissueOf(in CoverSet set, double d, double modulus, ushort code)
        {
            double di = d * (1 - 2 * set.WallShare), d2 = d * d, di2 = di * di;
            double area = Pi / 4 * (d2 - di2), ei = modulus * Pi / 64 * (d2 * d2 - di2 * di2), mu = set.Density * area, cut = set.CutEnergy * area;
            // v_c,sharp = [F_c·√d / (2√(2π)·(EI·μ³)^¼)]^(2/3), F_c = e_s·A_w/d (stem-properties.md §2.2).
            double inertia = DetMath.Sqrt(DetMath.Sqrt(ei * mu * mu * mu));
            double ratio = cut / d * DetMath.Sqrt(d) / (2 * DetMath.Sqrt(TwoPi) * inertia), cube = DetMath.Cbrt(ratio);
            return new Tissue
            {
                Known = true, Code = code, D = d, Area = area, Mu = mu, Kappa0 = StemDrag * d / (8 * ei), Vc = EdgeFactor * cube * cube, Cut = cut,
            };
        }

        // The lean of a stem whose κ·L³ is x: φ/cos²φ = x, read from the table.
        double Lean(double x)
        {
            if (!(x > 0)) return 0;
            double s = x / (1 + x) * TableSize;
            int j = (int)s;
            if (j >= TableSize) return _leanTable[TableSize];
            return _leanTable[j] + (_leanTable[j + 1] - _leanTable[j]) * (s - j);
        }

        // The two roots of φ·cos φ = c (0 ≤ c ≤ the most), read from the tables.
        void Roots(double c, out double phi1, out double phi2)
        {
            double s = c / _rootMax * TableSize;
            int j = (int)s;
            if (j >= TableSize)
            {
                phi1 = phi2 = _rootPhi;
                return;
            }
            double u = s - j;
            phi1 = _root1[j] + (_root1[j + 1] - _root1[j]) * u;
            phi2 = _root2[j] + (_root2[j + 1] - _root2[j]) * u;
        }

        // ---- the cut grid ----------------------------------------------------------------------------------------------------

        long GridKey(double x, double z) => ((long)Floor(x / StemGridCell) << 32) ^ (uint)Floor(z / StemGridCell);

        // ⌊v⌋ for a cell index, by a truncation and a compare (basic operations only).
        static int Floor(double v)
        {
            int t = (int)v;
            return v < t ? t - 1 : t;
        }

        int GridHome(long key) => (int)(FlightRandom.Mix64((ulong)key) & (StemGridSlots - 1));

        // The plane the ground at (x, z) was cut to, world y; +∞ for none.
        double GridCut(double x, double z)
        {
            long key = GridKey(x, z);
            int home = GridHome(key);
            for (int p = 0; p < StemGridProbe; p++)
            {
                int i = (home + p) & (StemGridSlots - 1);
                if (_gridKey[i] == key) return _gridY[i];
                if (_gridKey[i] == long.MinValue) return double.PositiveInfinity;
            }
            return double.PositiveInfinity;
        }

        // The ground at (x, z) is cut down to y (its plane lowered, never raised).
        void GridLower(double x, double z, double y)
        {
            long key = GridKey(x, z);
            int home = GridHome(key);
            for (int p = 0; p < StemGridProbe; p++)
            {
                int i = (home + p) & (StemGridSlots - 1);
                if (_gridKey[i] == key)
                {
                    if (y < _gridY[i]) _gridY[i] = y;
                    return;
                }
                if (_gridKey[i] != long.MinValue) continue;
                _gridKey[i] = key;
                _gridY[i] = y;
                return;
            }
            _gridKey[home] = key;
            _gridY[home] = y;
        }

        // ---- the discs' trails over the stem field ------------------------------------------------------------------------------

        // A rotor's disc over stems now stands at hub with its stems cut down to the plane y: a new trail entry once the hub has
        // moved StemTrailStep from the last, else the last one's plane lowered to it.
        void Trail(int c, Double3 hub, double y)
        {
            int at = c * StemTrailSize;
            if (_trailCount[c] > 0)
            {
                int last = at + (_trailHead[c] + StemTrailSize - 1) % StemTrailSize;
                double dx = hub.X - _trailX[last], dz = hub.Z - _trailZ[last];
                if (dx * dx + dz * dz < StemTrailStep * StemTrailStep)
                {
                    if (y < _trailY[last]) _trailY[last] = y;
                    return;
                }
            }
            int i = at + _trailHead[c];
            _trailX[i] = hub.X;
            _trailZ[i] = hub.Z;
            _trailY[i] = y;
            _trailHead[c] = (_trailHead[c] + 1) % StemTrailSize;
            if (_trailCount[c] < StemTrailSize) _trailCount[c]++;
        }

        // The plane the field's stems at (x, z) were cut down to: the lowest of every trail entry whose disc held the point strictly
        // inside (any rotor's: a rear disc in the front one's track, a disc back over its own); +∞ for ground no disc has cut.
        double TrailCut(double x, double z, double rtip)
        {
            double y = double.PositiveInfinity, inside = rtip * (1 - 1e-6);
            for (int c = 0; c < 4; c++)
                for (int k = 0; k < _trailCount[c]; k++)
                {
                    int i = c * StemTrailSize + k;
                    double dx = x - _trailX[i], dz = z - _trailZ[i];
                    if (dx * dx + dz * dz < inside * inside && _trailY[i] < y) y = _trailY[i];
                }
            return y;
        }

        // ---- the step --------------------------------------------------------------------------------------------------------

        // The four hubs' ground, once a step before the discs' passes (the field's heights).
        void StemsBegin(in FlightState now)
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
            for (int c = 0; c < 4; c++)
            {
                Double3 hub = _discOf[c] >= 0 ? now.Position + now.Attitude.Rotate(_spec.Shapes[_discOf[c]].A) : now.Position;
                _hubXZ[c] = new XZ(hub.X, hub.Z);
            }
            _world.SampleGround(_hubXZ, _hubGround);
        }

        // One rotor's stems this step (disc shape i, corner c), its blades sweeping from start over sweep. True when a blade changed.
        bool Stems(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 axis, double start, double sweep,
            StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            ref StemRecord rec = ref StemLast[c];
            if (!_stemsOn) return false;
            double R = _radius, dt = FlightCore.Dt, omega = now.RotorSpeed[c];
            // The stems' load follows the rotor's q = T/A: quicker pressed than let up.
            double thrust = _propulsion.Thrust(c), q = thrust > 0 ? thrust / (Pi * R * R) : 0, was = _stemQ[c];
            double tau = q > was ? StemPressTime : StemRiseTime;
            _stemQ[c] = was + (q - was) * (dt / (dt + tau));
            q = StemLean ? _stemQ[c] : 0;
            rec.Load = q;
            rec.Cut = _stemCut[c];
            bool changed = false;
            if (omega > 0)
            {
                double rtip = 0;
                for (int b = 0; b < _blades; b++) rtip = DetMath.Max(rtip, _propulsion.Rotor(c).Blade(b).TipRadius * R);
                Double3 hub = now.Position + now.Attitude.Rotate(d.A);
                changed |= Field(i, d, c, ref now, body, hub, axis, start, sweep, q, rtip, ref rec, events, ref impulse, ref angular);
                changed |= Elements(i, d, c, ref now, body, hub, axis, start, sweep, q, rtip, ref rec, events, ref impulse, ref angular);
            }
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

        // The stem field's stems under the disc (design.md 2.2, the law's steps 1–6).
        bool Field(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, double start, double sweep,
            double q, double rtip, ref StemRecord rec, StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            double ground = _hubGround[c].Height;
            if (!(ground > double.NegativeInfinity)) return false;
            double R = _radius, dt = FlightCore.Dt, omega = now.RotorSpeed[c], h = hub.Y - ground, cut = _stemCut[c];
            rec.Height = h;
            int n = _world.StemsNear(_hubXZ[c], R, h, _patches);
            if (n > _patches.Length) n = _patches.Length;
            double passes = _blades * omega * dt / TwoPi, disc = Pi * R * R, rroot = (_b.Stations[0] - 0.5 * _span) * R;
            // The hub's motion across the stems in the disc's plane: what it runs into is met upright at its leading edge.
            Double3 v = PointVelocity(now, hub), across = v - axis * Double3.Dot(v, axis);
            double speed = across.Length();
            Double3 lead = speed > 0 ? across * (1 / speed) : default;
            double atLead = speed > 0 ? TrailCut(hub.X + lead.X * rtip, hub.Z + lead.Z * rtip, rtip) - ground : double.PositiveInfinity;
            double fresh = 0, sliced = 0, entered = 0;
            Span<double> nH = stackalloc double[FieldKinds], rH = stackalloc double[FieldKinds], lH = stackalloc double[FieldKinds];
            Span<double> nE = stackalloc double[FieldKinds], lE = stackalloc double[FieldKinds];
            nH.Clear(); rH.Clear(); lH.Clear(); nE.Clear(); lE.Clear();
            bool deeper = cut > h && cut < double.PositiveInfinity;
            for (int p = 0; p < n; p++)
            {
                StemPatch patch = _patches[p];
                int k = (int)patch.Kind + (int)CoverKind.GrassBlade - 1;
                if (k < (int)CoverKind.GrassBlade || k > (int)CoverKind.BurntCulm || !_tissue[k].Known) continue;
                Tissue t = _tissue[k];
                int f = k - (int)CoverKind.GrassBlade;
                double kappa = q * t.Kappa0;
                // The stems reaching the disc as they lean, where the blade is fast enough across them.
                double count = Reaching(patch, kappa, h, h, out double phi, out double mid), share = Annulus(t.Vc, omega, phi, rtip, rroot, out double rms);
                nH[f] += count * share;
                rH[f] += count * share * rms;
                DetMath.SinCos(phi, out _, out double cphi);
                lH[f] += count * share * DetMath.Max(0, mid - h / cphi); // the piece above the cut
                // What the leading edge runs into: upright, its tops up to the tallest or to the ground's cut plane there.
                if (speed > 0 && omega * rtip >= (StemCritical ? t.Vc : 0))
                {
                    double top = DetMath.Min(atLead, patch.HeightMax), span = patch.HeightMax - h;
                    if (top > h && span > 0)
                    {
                        double stand = patch.Density * patch.Area / disc * (top - h) / span;
                        nE[f] += stand;
                        lE[f] += stand * 0.5 * (top - h);
                    }
                }
            }

            // The parts: the stems under the disc (fresh tops, sliced stubs), and those the leading edge meets.
            double work = 0;
            bool changed = false;
            Double3 inPlane = default;
            double axial = 0;
            for (int f = 0; f < FieldKinds; f++)
            {
                int k = f + (int)CoverKind.GrassBlade;
                Tissue t = _tissue[k];
                if (!t.Known) continue;
                // The stubs cut before stand at the height cut to: coming down, the disc slices them; the stems it now meets
                // beyond them (come within its reach as it came down, or risen as the slipstream eased) are fresh, due until a
                // blade sweeps them.
                ref double due = ref _stemDue[c * FieldKinds + f], stubs = ref _stemStubs[c * FieldKinds + f];
                double drop = deeper ? cut - h : 0, slices = deeper ? stubs * DetMath.Min(passes, drop / t.D) : 0;
                double flungStubs = deeper ? stubs * drop : 0;
                if (nH[f] > stubs)
                {
                    due += nH[f] - stubs;
                    stubs = nH[f];
                }
                double take = due * DetMath.Min(1, passes);
                due -= take;
                double r = nH[f] > 0 ? rH[f] / nH[f] : DetMath.Sqrt(0.5 * (rtip * rtip + rroot * rroot)); // stubs alone: over the blade
                double length = nH[f] > 0 ? lH[f] / nH[f] : 0;
                if (take > 0 || slices > 0)
                {
                    // Shared evenly by the blades, at their own azimuths this step.
                    double cuts = take + slices;
                    for (int b = 0; b < _blades; b++)
                        changed |= Part(i, d, c, b, ref now, body, hub, axis, start + 0.5 * sweep + TwoPi * b / _blades, r, t,
                            take / _blades, slices / _blades, flungStubs / _blades, cuts / _blades, (take * length + slices * drop) / cuts, events,
                            ref impulse, ref angular, ref work, ref inPlane, ref axial);
                    fresh += take;
                    sliced += slices;
                }
                double enter = 2 * rtip * speed * dt * nE[f];
                if (enter > 0)
                {
                    double piece = lE[f] / nE[f], psiLead = Azimuth(now.Attitude, lead, c);
                    for (int b = 0; b < _blades; b++)
                    {
                        double psi = start + 0.5 * sweep + TwoPi * b / _blades;
                        double weight = 0.5 * Pi * DetMath.Max(0, Cos(psi - psiLead));
                        if (!(weight > 0)) continue;
                        double rb = _propulsion.Rotor(c).Blade(b).TipRadius * R;
                        changed |= Part(i, d, c, b, ref now, body, hub, axis, psi, rb, t, enter * weight, 0, 0, enter * weight, piece, events,
                            ref impulse, ref angular, ref work, ref inPlane, ref axial);
                    }
                    entered += enter;
                }
            }

            // The height cut to: down to the disc where it came down; towards the ground it moves onto in the share it sweeps, and
            // its stubs towards the stems reaching the disc there.
            if (h < cut) cut = h;
            if (speed > 0)
            {
                double s = DetMath.Min(1, 2 * rtip * speed * dt / (Pi * rtip * rtip));
                cut += s * (DetMath.Min(atLead, h) - cut);
                for (int f = 0; f < FieldKinds; f++) _stemStubs[c * FieldKinds + f] += s * (nH[f] - _stemStubs[c * FieldKinds + f]);
            }
            _stemCut[c] = cut;
            if (n > 0) Trail(c, hub, ground + cut); // the ground under the disc stands no higher than that
            rec.Cut = cut;
            rec.Fresh += fresh;
            rec.Sliced += sliced;
            rec.Entered += entered;
            rec.Cuts += fresh + sliced + entered;
            rec.Work += work;
            rec.InPlane = rec.InPlane + inPlane;
            rec.Axial += axial;
            return changed;
        }

        // The stems of a patch in the disc whose tops, leaning by κ, reach x (≥ h): their tops above h spread evenly up to the
        // patch's tallest (assumed), so the lengths [L1, L2] that stand to x are the share (L2 − L1)/(top − h) of those reaching h.
        // phi: their lean at the middle length.
        double Reaching(in StemPatch p, double kappa, double h, double x, out double phi, out double mid)
        {
            phi = 0;
            mid = h;
            double span = p.HeightMax - h;
            if (!(span > 0)) return 0;
            double lo, hi;
            if (!(kappa > 0))
            {
                lo = x;
                hi = p.HeightMax;
            }
            else
            {
                double c = kappa * x * x * x;
                if (!(c < _rootMax)) return 0;
                Roots(c, out double phi1, out double phi2);
                DetMath.SinCos(phi1, out _, out double c1);
                DetMath.SinCos(phi2, out _, out double c2);
                lo = x / c1;
                hi = c2 * c2 > 0 ? DetMath.Min(DetMath.Cbrt(phi2 / (kappa * c2 * c2)), p.HeightMax) : p.HeightMax;
            }
            lo = DetMath.Max(lo, h);
            if (!(hi > lo)) return 0;
            mid = 0.5 * (lo + hi);
            phi = Lean(kappa * mid * mid * mid);
            return p.Density * p.Area * (hi - lo) / span;
        }

        // The share of the disc where a blade at ω cuts a stem leaning φ, ω·r·cos φ ≥ v_c, outside the root, and its cuts' rms
        // radius (the stems even over the disc).
        double Annulus(double vc, double omega, double phi, double rtip, double rroot, out double rms)
        {
            DetMath.SinCos(phi, out _, out double cphi);
            double across = omega * cphi, rc = StemCritical ? (across > 0 ? vc / across : double.PositiveInfinity) : 0;
            rc = DetMath.Max(rc, rroot);
            rms = 0;
            if (!(rc < rtip)) return 0;
            rms = DetMath.Sqrt(0.5 * (rtip * rtip + rc * rc));
            return (rtip * rtip - rc * rc) / (_radius * _radius);
        }

        static double Cos(double a)
        {
            DetMath.SinCos(a, out _, out double c);
            return c;
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
            // The log: the blade's step in BladeStrikes, its whole stems counted from the running mean.
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

        // S04's drawn straws and stems under the disc (the range): discrete, each met by the blade crossing its azimuth.
        bool Elements(int i, in ContactShape d, int c, ref FlightState now, in MassProperties body, Double3 hub, Double3 axis, double start, double sweep,
            double q, double rtip, ref StemRecord rec, StepEvents events, ref Double3 impulse, ref Double3 angular)
        {
            if (!(_strawSet.CutEnergy > 0) && !(_stemSet.CutEnergy > 0)) return false;
            if (!_cacheValid[c] || (hub - _cacheAt[c]).Length() > StemCacheRefresh) Cache(c, _cacheValid[c] ? _lastHub[c] : hub, hub, rtip, axis);
            _lastHub[c] = hub;
            int count = _stalkCount[c];
            if (count == 0 || !(axis.Y > 0.2)) return false;
            double omega = now.RotorSpeed[c], rroot = (_b.Stations[0] - 0.5 * _span) * _radius;
            double work = 0, axial = 0;
            Double3 inPlane = default;
            bool changed = false;
            for (int e = 0; e < count; e++)
            {
                ref Stalk k = ref _stalks[c * StemCacheSize + e];
                Double3 rel = k.Base - hub, flat = rel - axis * Double3.Dot(rel, axis);
                double r = flat.Length();
                bool inside = r <= rtip && r >= rroot;
                bool entering = inside && !k.Inside;
                k.Inside = inside;
                if (!inside || !(omega > 0)) continue;
                rec.Elements++;
                // Its top: leaning in the slipstream (upright as it enters), or the plane its ground was cut to.
                double phi = entering ? 0 : Lean(q * k.T.Kappa0 * k.Length * k.Length * k.Length);
                DetMath.SinCos(phi, out _, out double cphi);
                double own = k.Base.Y + DetMath.Min(k.Rise, k.Length * cphi), cutTo = GridCut(k.Base.X, k.Base.Z), top = DetMath.Min(own, cutTo);
                double plane = hub.Y - (axis.X * rel.X + axis.Z * rel.Z) / axis.Y;
                if (!(top > plane) || !(k.Base.Y < plane)) continue;
                if (StemCritical && !(omega * r * cphi >= k.T.Vc)) continue; // pushed aside
                double psi = Azimuth(now.Attitude, flat * (1 / r), c);
                int by = -1;
                for (int b = 0; b < _blades && by < 0; b++)
                {
                    double ahead = Wrap(psi - (start + TwoPi * b / _blades));
                    if (ahead > 0 && ahead <= sweep) by = b;
                }
                if (by < 0) continue;
                double drop = top - plane;
                bool fresh = !(cutTo < own);
                double slice = fresh ? 0 : DetMath.Min(1, drop / k.T.D), piece = fresh ? DetMath.Max(0, k.Length - (plane - k.Base.Y) / cphi) : drop;
                GridLower(k.Base.X, k.Base.Z, plane);
                changed |= Part(i, d, c, by, ref now, body, hub, axis, psi, r, k.T, fresh ? 1 : 0, slice, fresh ? 0 : drop, 1, piece, events,
                    ref impulse, ref angular, ref work, ref inPlane, ref axial);
                if (fresh) rec.Fresh += 1;
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

        // The rotor's element cache about its hub: the straws and stems whose bases lie within the disc and the refresh's margin.
        void Cache(int c, Double3 last, Double3 hub, double rtip, Double3 axis)
        {
            _cacheAt[c] = hub;
            _cacheValid[c] = true;
            int n = _world.MicroDetailNear(hub, _radius + StemCacheRefresh + 0.02, MicroKind.Straw | MicroKind.Stem, _elements);
            if (n > _elements.Length) n = _elements.Length;
            int m = 0;
            for (int e = 0; e < n; e++)
            {
                MicroElement el = _elements[e];
                CoverSet set = el.Kind == MicroKind.Stem ? _stemSet : _strawSet;
                if (!(set.CutEnergy > 0) || !(el.Diameter > 0)) continue;
                // The element's modulus in its row's range by its Id's hash: the fiber's rule (world-derived, never a flight draw).
                double u = (FlightRandom.Mix64(el.Id) >> 11) * (1.0 / 9007199254740992.0);
                double modulus = set.ModulusMin + (set.ModulusMax - set.ModulusMin) * u;
                double top = DetMath.Max(el.P0.Y, DetMath.Max(el.P1.Y, el.P2.Y));
                ref Stalk k = ref _stalks[c * StemCacheSize + m++];
                k.Base = el.P0;
                k.Length = (el.P1 - el.P0).Length() + (el.P2 - el.P1).Length();
                k.Rise = top - el.P0.Y;
                k.T = TissueOf(set, el.Diameter, modulus, (ushort)((el.Kind == MicroKind.Stem ? (int)CoverKind.Stem : (int)CoverKind.Straw) + 1));
                Double3 rel = k.Base - last, flat = rel - axis * Double3.Dot(rel, axis);
                k.Inside = flat.Length() <= rtip; // inside at the last step: not met as an entry
            }
            _stalkCount[c] = m;
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
