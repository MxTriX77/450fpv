using System;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.World;

namespace Fpv450.Sim.Contact
{
    /// <summary>
    /// What the drone's parts fly through (w16-plants-in-the-air 1.5, its design.md "## 1.5"; w9-village-physics 2.10's legs; PHY-05):
    /// the stems on every part but the turning discs, and a crown's leaves on the same parts. Engine-free, allocation-free in a
    /// step, DetMath only; ContactSpec.PartStems (header entry 34) off flies model 103's contacts, which had neither.
    /// - The stems are the one stem field's (D-102), placed by PropStems' own law: the stems a disc's plane can reach are the props'
    ///   own (the same hash, the same state), and the field's shorter ones, which only the parts reach, are placed by the same law
    ///   from the ground up (the field's stems per m² at 0 m less those at h_ref, their lengths from the field's reach below it).
    ///   A map's drawn standing stems (S04's) are the props' elements. The parts' cache holds those that can reach the drone's
    ///   lowest point, about the drone, refreshed as PropStems' are; it is empty while the drone's lowest point is over StemWakeTop.
    /// - Each stem is one link with one lean law, the props' (PropStems, 2.12–2.13): a rigid link from its base, its lean b (a
    ///   horizontal vector) following b' = −m(b)/τ at its first mode's τ, m its net moment over 4EI/L (its spring about its natural
    ///   lean and set, less its jet's load and its weight along its lean). A stem a part touches leans by that law in two dimensions
    ///   here (PropStems holds its φ meanwhile), and is the props' again once no part touches it and its lean across its way is home.
    /// - The step, after the drone's move: the lean's free step, implicit in its spring (touched at an arm s up the stem the spring is
    ///   the cantilever loaded there, K = 3EI/s: 3EI·δ/s³ at the contact, plants-in-the-air.md §6.2; free, 4EI/L); then each part the
    ///   link meets is a constraint: the stem's point at the contact moves out of the part along the normal's share across the stem
    ///   (a rotation about its base), the side kept from the last step while the pair touches (a skid whose core passed the stem's
    ///   line holds it ahead), the lean never past lying flat (the ground). The parts' force is the generalised force that move took,
    ///   Q = (k·τ/dt + K)·Δb (held, the spring K·(b − rest); moved, its first mode's damping k·τ·ḃ more), shared among them by their
    ///   moves along it, over each one's arm, along its normal; its share along the stem at most Euler's π²EI/(4L²) (published). At a
    ///   stem's first touch the part brings its mass at the contact, min(I/s², μL) with I = 4μL³/1.875⁴, to its own speed across it.
    /// - Its root yields: the spring carries at most ε_f·2EI/d (StemYieldStrain); past it the stem's rest follows its lean (lodged).
    ///   A stem a part meets within the part's own reach of its base is crushed flat under it and carries nothing (the ground does).
    /// - The leaves (a crown's, ContactModel.CrownOf): the leaves a part meets are brought to its normal speed (Newton's impact law,
    ///   published: C_p = 2·sin²θ): −ρ_l·(⅔·2r·ℓ·|v⊥|·v⊥ + ½·πr²·|v|·v) on a capsule of core ℓ and radius r, ρ_l the crown's leaf area
    ///   density times the fresh leaf's mass per area (2.3's numbers). A turning disc's leaves stay PropStrikes.Leaves'.
    /// - Logged: EventKind.PlantPush, a group's mean force over each field it was touched in (the four legs and the body).
    /// </summary>
    public sealed partial class PropStrikes
    {
        /// <summary>1.2's link's inertia about its base over μL³: (4EI/L)/ω₁², ω₁ = 1.875²·√(EI/μL⁴), so 4/1.875⁴ (derived; as
        /// PlantMechanics.Stem): a stem's mass at a contact s up it is I/s², at most its whole μL (the first touch's impulse).</summary>
        public const double StemLinkInertia = 4.0 / (1.875 * 1.875 * 1.875 * 1.875);

        /// <summary>Newton's impact law's shape factors on ρ·v²·A (published: C_p = 2·sin²θ; a cylinder's C_D 4/3 on its diameter,
        /// a sphere's 1 on its disc).</summary>
        public const double NewtonCylinder = 2.0 / 3, NewtonSphere = 0.5;

        /// <summary>A part's push across a stem is taken at least this share of its normal (numerical: a push within 78° of the
        /// stem's axis is Euler's, capped there; the stem's turn away from it is the link's geometry).</summary>
        const double LeastAcross = 0.2;

        /// <summary>A stem no part touches is its jet's (PropStems') again once its lean across its way lies this near its rest, rad
        /// (numerical: 0.1 µm at 1 m).</summary>
        const double HomeLean = 1e-7;

        /// <summary>A stem's root yields at this bending strain (derived: barley straw's bending stress over its bending modulus in
        /// the same three-point tests, 8.41 MPa / 474 MPa, stem-properties.md [6]; assumed for every culm and drawn stem): its yield
        /// moment ε_f·2EI/d at the model's own EI. Past it the bend sets (the stem lodges).</summary>
        public const double StemYieldStrain = 8.41 / 474;

        /// <summary>A stem cut shorter than this pushes nothing (numerical).</summary>
        const double MinLength = 1e-3;

        /// <summary>The projection's passes over the parts a stem meets, and the parts one stem holds at once (numerical).</summary>
        const int ProjectionPasses = 3, MaxHits = 8;

        const int ShortEntries = 5, PartCacheSize = 4096, ActiveMax = 2048, PartGroups = 5, BodyGroup = 4;

        /// <summary>The parts' stems' and leaves' push on a group this step (the four legs in Corners' order: a corner's supports,
        /// skid half and leg tube; then the body, its resting blades among it): the force, world N; the stems touching it; the
        /// leaves' share of the force, world N.</summary>
        public struct PartRecord
        {
            public Double3 Force, Leaves;
            public int Stems;
        }

        /// <summary>The last step's push per group (PartRecord).</summary>
        public readonly PartRecord[] PartLast = new PartRecord[PartGroups];

        /// <summary>The parts' stems: cached about the drone, and in the active list (near a part, or still bent).</summary>
        public int PartCached => _pCount;

        public int PartActive => _aCount;

        /// <summary>Stems near a part left out of the active list because it was full, so far (a world past the list's budget).</summary>
        public long PartOverflow { get; private set; }

        /// <summary>The known-wrong's switch, for a test's comparison only (a flight never sets it): off, the stems push no part
        /// and the leaves none (model 103's contacts; ContactSpec.PartStems off is the same, and what a log without entry 34 flew).</summary>
        public bool PartPush = true;


        bool _partsOn;
        double _droneReach;
        int[] _groupOf;                               // shape → its push's group (a corner's leg, or the body)
        int[] _massOf;                                // shape → its mass part, the broadphase's group
        bool[] _pOn, _leafSkip;
        Double3[] _pA, _pB, _pLo, _pHi;
        double[] _pR;
        Double3[] _mLo, _mHi;
        bool[] _mOn;
        int[] _massStart, _massShapes;
        int _masses;

        // The parts' cache: the stems that can reach the drone's lowest point, their natural links' tips, its centre and that point.
        readonly Stalk[] _pStalk = new Stalk[PartCacheSize];
        readonly Double3[] _pTip = new Double3[PartCacheSize];
        int _pCount;
        Double3 _pAt;
        double _pLow;
        bool _pValid;

        // The active stems: a copy of each one's stalk and its state's slot.
        readonly Stalk[] _aStalk = new Stalk[ActiveMax];
        readonly int[] _aSlot = new int[ActiveMax];
        int _aCount;

        // One stem's moves by part in its step: the part, its summed move (rad, a lean vector), its last normal, contact point, arm,
        // |n·t| and the across share's square.
        readonly int[] _hPart = new int[MaxHits];
        readonly bool[] _hFresh = new bool[MaxHits];
        readonly double[] _hX = new double[MaxHits], _hZ = new double[MaxHits], _hS = new double[MaxHits], _hAlong = new double[MaxHits], _hAcross = new double[MaxHits];
        readonly Double3[] _hN = new Double3[MaxHits], _hPoint = new Double3[MaxHits];

        // The squares' stems per m² from the ground up (asked for only by the parts), and each layer's share of its stems reaching
        // 0, 0.05 … 0.2 m (h_ref).
        readonly double[] _qDensity0 = new double[SquareSlots * Layers];
        readonly bool[] _qHas0 = new bool[SquareSlots];
        readonly double[] _shortReach = new double[Layers * ShortEntries];
        readonly bool[] _layerShort = new bool[Layers];

        // A field's push per group (stems, then leaves): its impulse, the impulse-weighted point, the stem-steps, the kinds met.
        readonly Double3[] _fImpulse = new Double3[2 * PartGroups], _fPoint = new Double3[2 * PartGroups];
        readonly double[] _fWeight = new double[2 * PartGroups];
        readonly int[] _fStems = new int[2 * PartGroups];
        readonly double[] _fKind = new double[PartGroups * 7];
        readonly int[] _fObject = new int[PartGroups];
        readonly ushort[] _fMaterial = new ushort[PartGroups];

        void InitParts()
        {
            int n = _spec.Shapes.Length;
            _groupOf = new int[n];
            _massOf = new int[n];
            _pOn = new bool[n];
            _leafSkip = new bool[n];
            _pA = new Double3[n];
            _pB = new Double3[n];
            _pLo = new Double3[n];
            _pHi = new Double3[n];
            _pR = new double[n];
            for (int i = 0; i < n; i++)
            {
                ContactShape sh = _spec.Shapes[i];
                int g = BodyGroup;
                if (sh.Role == ContactRole.Support || sh.Role == ContactRole.Skid) g = sh.Corner;
                for (int c = 0; _spec.Legs != null && c < 4; c++)
                    if (_spec.Legs.Parts[c] == sh.Part) g = c;
                _groupOf[i] = g < 0 ? BodyGroup : g;
                _massOf[i] = sh.Part;
                _masses = sh.Part + 1 > _masses ? sh.Part + 1 : _masses;
                double a = sh.A.Length(), b = sh.B.Length();
                _droneReach = DetMath.Max(_droneReach, DetMath.Max(a, b) + sh.Radius);
                // A sphere inside a capsule of its own part (a load-bearing end, a skid's support) is the capsule's cap: its leaves are
                // the capsule's.
                if (sh.A.X != sh.B.X || sh.A.Y != sh.B.Y || sh.A.Z != sh.B.Z) continue;
                for (int j = 0; j < n; j++)
                {
                    ContactShape o = _spec.Shapes[j];
                    if (j == i || o.Part != sh.Part || o.Role == ContactRole.Disc || o.A.X == o.B.X && o.A.Y == o.B.Y && o.A.Z == o.B.Z) continue;
                    Double3 ab = o.B - o.A;
                    double u = Double3.Dot(sh.A - o.A, ab) / Double3.Dot(ab, ab);
                    u = u < 0 ? 0 : u > 1 ? 1 : u;
                    if ((sh.A - (o.A + ab * u)).Length() + sh.Radius <= o.Radius + 1e-6) _leafSkip[i] = true;
                }
            }
            _mLo = new Double3[_masses];
            _mHi = new Double3[_masses];
            _mOn = new bool[_masses];
            // The shapes by mass part (the broadphase's groups), in shape order within each.
            _massStart = new int[_masses + 1];
            _massShapes = new int[n];
            for (int i = 0; i < n; i++) _massStart[_massOf[i] + 1]++;
            for (int m = 0; m < _masses; m++) _massStart[m + 1] += _massStart[m];
            var fill = new int[_masses];
            for (int i = 0; i < n; i++) _massShapes[_massStart[_massOf[i]] + fill[_massOf[i]]++] = i;
            _partsOn = _stemsOn && _spec.PartStems;
        }

        // ---- the parts' cache ------------------------------------------------------------------------------------------------

        // The stems per m² of square q from the ground up, per layer (one point StemsNear at 0 m the first time), and the layers'
        // short reach shares the first time a layer is met there.
        void Ground0(int q, XZ centre)
        {
            if (_qHas0[q]) return;
            _qHas0[q] = true;
            for (int l = 0; l < Layers; l++) _qDensity0[q * Layers + l] = 0;
            int count = _world.StemsNear(centre, PointRadius, 0, _patches);
            if (count > _patches.Length) count = _patches.Length;
            double inverse = 1 / (Pi * PointRadius * PointRadius);
            bool unseen = false;
            for (int p = 0; p < count; p++)
            {
                StemPatch patch = _patches[p];
                if (patch.Layer >= Layers) continue;
                _qDensity0[q * Layers + patch.Layer] += patch.Density * patch.Area * inverse;
                _layerKind[patch.Layer] = patch.Kind; // a layer of short stems alone is met only here
                unseen |= !_layerShort[patch.Layer];
            }
            if (!unseen) return;
            // Each new layer's share of all its stems still reaching 0.05 j m (j = 0 … 4, the last h_ref), to 1e-9.
            Span<double> base0 = stackalloc double[Layers];
            Span<bool> fresh = stackalloc bool[Layers];
            Span<double> here = stackalloc double[Layers];
            base0.Clear();
            fresh.Clear();
            for (int j = 0; j < ShortEntries; j++)
            {
                int c = _world.StemsNear(centre, PointRadius, j * StemReachStep, _patches);
                if (c > _patches.Length) c = _patches.Length;
                here.Clear();
                for (int p = 0; p < c; p++)
                    if (_patches[p].Layer < Layers) here[_patches[p].Layer] += _patches[p].Density * _patches[p].Area;
                for (int l = 0; l < Layers; l++)
                {
                    if (j == 0)
                    {
                        fresh[l] = !_layerShort[l] && here[l] > 0;
                        base0[l] = here[l];
                    }
                    if (fresh[l]) _shortReach[l * ShortEntries + j] = (long)(here[l] / base0[l] * 1e9 + 0.5) * 1e-9;
                }
            }
            for (int l = 0; l < Layers; l++)
                if (fresh[l]) _layerShort[l] = true;
        }

        // The drawn length of a short stem of layer l (one not reaching h_ref) at the share s of its layer's short stems, the
        // tallest first.
        double ShortLengthAt(int l, double s)
        {
            int at = l * ShortEntries;
            double top = _shortReach[at + ShortEntries - 1], target = top + s * (1 - top);
            for (int j = ShortEntries - 2; j >= 0; j--)
            {
                double a = _shortReach[at + j], b = _shortReach[at + j + 1];
                if (a < target) continue;
                double u = a > b ? (a - target) / (a - b) : 0;
                return (j + u) * StemReachStep;
            }
            return 0;
        }

        // The cache about centre (the body origin): every stem whose base is within the drone's reach and its own natural lean's,
        // and which reaches the drone's lowest point (world y, low) less the refresh margin over its square's ground.
        void PartCache(Double3 centre, double low, bool field)
        {
            _pAt = centre;
            _pLow = low;
            _pValid = true;
            int m = 0;
            double near = _droneReach + StemCacheRefresh;
            DetMath.SinCos(StemNaturalLean, out double lean, out _);
            double tallest = StemReference + (ReachEntries - 1) * StemReachStep, span = near + tallest * lean;
            if (field)
            {
                int x0 = Floor((centre.X - span) / StemSquare), x1 = Floor((centre.X + span) / StemSquare);
                int z0 = Floor((centre.Z - span) / StemSquare), z1 = Floor((centre.Z + span) / StemSquare);
                for (int iz = z0; iz <= z1; iz++)
                    for (int ix = x0; ix <= x1; ix++)
                    {
                        // A square wholly out of the tallest stem's reach is not asked for.
                        double cx = (ix + 0.5) * StemSquare, cz = (iz + 0.5) * StemSquare;
                        double ox = DetMath.Max(0, DetMath.Abs(cx - centre.X) - 0.5 * StemSquare), oz = DetMath.Max(0, DetMath.Abs(cz - centre.Z) - 0.5 * StemSquare);
                        double off2 = ox * ox + oz * oz;
                        if (off2 > span * span) continue;
                        int q = Square(ix, iz);
                        double slope = DetMath.Sqrt(_qNx[q] * _qNx[q] + _qNz[q] * _qNz[q]) / _qNy[q];
                        // The shortest stem here that can reach the drone's lowest point: its base no higher than the square's ground
                        // plus its half diagonal's rise.
                        double need = low - (_qGround[q] + slope * 0.5 * StemSquare * 1.4142135623730951) - StemCacheRefresh;
                        bool shortToo = need < StemReference;
                        if (shortToo) Ground0(q, new XZ(cx, cz));
                        for (int l = 0; l < Layers; l++)
                        {
                            double density = _qDensity[q * Layers + l] * StemDensityScale;
                            int k = (int)_layerKind[l] + (int)CoverKind.GrassBlade - 1;
                            if (k < (int)CoverKind.GrassBlade || k > (int)CoverKind.BurntCulm) continue;
                            CoverSet set = _coverSets[k];
                            if (!(set.Diameter > 0) || !(set.CutEnergy > 0)) continue;
                            // The layer's tallest's reach from here: a square out of it holds none of the layer's that can come near.
                            double reachLayer = near + DetMath.Max(_layerTop[l], StemReference) * lean;
                            if (off2 > reachLayer * reachLayer) continue;
                            ulong cell = FlightRandom.Mix64(unchecked(((ulong)(uint)ix << 32) ^ (uint)iz ^ ((ulong)l << 59)));
                            // The props' own stems (PropStems' Cache, the same hash and order: the tallest first).
                            if (density > 0 && _layerReach[l])
                            {
                                int K = (int)(density * StemSquare * StemSquare + Uniform(cell, 0));
                                for (int s = 0; s < K && m < PartCacheSize; s++)
                                {
                                    ulong id = FlightRandom.Mix64(unchecked(cell + (ulong)(s + 1) * 0xD1B54A32D192ED03UL)) | 1;
                                    if (!Within(id, ix, iz, centre, reachLayer)) continue;
                                    double length = LengthAt(l, (s + Uniform(id, 2)) / K);
                                    if (length < need) break;
                                    PlaceField(ref m, id, ix, iz, q, length, set, k, centre, near, lean);
                                }
                            }
                            // The shorter ones (the field's at 0 m less at h_ref), by the same law from their own hash.
                            if (!shortToo || !_layerShort[l]) continue;
                            double extra = (_qDensity0[q * Layers + l] - _qDensity[q * Layers + l]) * StemDensityScale;
                            if (!(extra > 0)) continue;
                            ulong low0 = FlightRandom.Mix64(cell ^ 0x5348_4F52_5453_5445UL);
                            int Ks = (int)(extra * StemSquare * StemSquare + Uniform(low0, 0));
                            for (int s = 0; s < Ks && m < PartCacheSize; s++)
                            {
                                ulong id = FlightRandom.Mix64(unchecked(low0 + (ulong)(s + 1) * 0xD1B54A32D192ED03UL)) | 1;
                                if (!Within(id, ix, iz, centre, near + StemReference * lean)) continue;
                                double length = ShortLengthAt(l, (s + Uniform(id, 2)) / Ks);
                                if (length < need || !(length > 0)) break;
                                PlaceField(ref m, id, ix, iz, q, length, set, k, centre, near, lean);
                            }
                        }
                    }
            }
            if (_coverSets.Length > (int)CoverKind.Stem && _coverSets[(int)CoverKind.Stem].CutEnergy > 0)
            {
                // The map's drawn standing stems (S04's), as PropStems takes them.
                int n = _world.MicroDetailNear(centre, span, MicroKind.Stem, _elements);
                if (n > _elements.Length) n = _elements.Length;
                CoverSet set = _coverSets[(int)CoverKind.Stem];
                for (int e = 0; e < n && m < PartCacheSize; e++)
                {
                    MicroElement el = _elements[e];
                    if (el.Kind != MicroKind.Stem || !(el.Diameter > 0)) continue;
                    double u = (FlightRandom.Mix64(el.Id) >> 11) * (1.0 / 9007199254740992.0);
                    double modulus = set.ModulusMin + (set.ModulusMax - set.ModulusMin) * u;
                    double top = DetMath.Max(el.P0.Y, DetMath.Max(el.P1.Y, el.P2.Y));
                    double length = (el.P1 - el.P0).Length() + (el.P2 - el.P1).Length();
                    if (!(top + StemCacheRefresh >= low)) continue;
                    ulong id = FlightRandom.Mix64(el.Id ^ 0x5304_5304_5304_5304UL) | 1;
                    Double3 way = el.P2 - el.P0;
                    way = new Double3(way.X, 0, way.Z);
                    double across = way.Length();
                    if (across > 1e-9) way = way * (1 / across);
                    else
                    {
                        DetMath.SinCos(TwoPi * Uniform(id, 4), out double sa, out double ca);
                        way = new Double3(ca, 0, sa);
                    }
                    double rise = top - el.P0.Y, phi0 = length > rise && length > 0 ? DetMath.Acos(DetMath.Min(1, rise / length)) : 0;
                    Tissue t = TissueOf(set, el.Diameter * StemDiameterScale, modulus, (ushort)((int)CoverKind.Stem + 1));
                    Place(ref m, id, el.P0, way, length, rise, phi0, t);
                }
            }
            _pCount = m;
        }

        // Whether stem id of the square (ix, iz) has its base within reach of centre (in x–z), placed as PropStems places it.
        static bool Within(ulong id, int ix, int iz, Double3 centre, double reach)
        {
            double dx = (ix + Uniform(id, 0)) * StemSquare - centre.X, dz = (iz + Uniform(id, 1)) * StemSquare - centre.Z;
            return !(dx * dx + dz * dz > reach * reach);
        }

        // A field stem of the square (ix, iz) (slot q) of layer kind k, placed as PropStems places it, kept if its base is within
        // the drone's reach (near) and its own lean's.
        void PlaceField(ref int m, ulong id, int ix, int iz, int q, double length, in CoverSet set, int k, Double3 centre, double near, double lean)
        {
            double x = (ix + Uniform(id, 0)) * StemSquare, z = (iz + Uniform(id, 1)) * StemSquare;
            double dx = x - centre.X, dz = z - centre.Z, reach = near + length * lean;
            if (dx * dx + dz * dz > reach * reach) return;
            double phi0 = StemNaturalLean * Uniform(id, 3), azimuth = TwoPi * Uniform(id, 4);
            double cx = (ix + 0.5) * StemSquare, cz = (iz + 0.5) * StemSquare;
            double y = _qGround[q] - (_qNx[q] * (x - cx) + _qNz[q] * (z - cz)) / _qNy[q];
            double d = set.Diameter * StemDiameterScale * DetMath.Exp(StemAllometry * DetMath.Log(length));
            DetMath.SinCos(azimuth, out double sa, out double ca);
            Tissue t = TissueOf(set, d, 0.5 * (set.ModulusMin + set.ModulusMax), (ushort)(k + 1));
            Place(ref m, id, new Double3(x, y, z), new Double3(ca, 0, sa), length, length, phi0, t);
        }

        void Place(ref int m, ulong id, Double3 basePoint, Double3 lean, double length, double rise, double phi0, in Tissue t)
        {
            ref Stalk k = ref _pStalk[m];
            k.Base = basePoint;
            k.Lean = lean;
            k.Length = length;
            k.Rise = rise;
            k.Phi0 = phi0;
            DetMath.SinCos(phi0, out double sp, out k.CosPhi0);
            k.T = t;
            k.MuG8 = t.Mu * Gravity / (8 * t.EI);
            k.Tau0 = 1 / (1.875 * 1.875 * DetMath.Sqrt(t.EI / t.Mu));
            k.Id = id;
            k.Slot = -1;
            _pTip[m] = basePoint + new Double3(lean.X * sp * length, k.CosPhi0 * length, lean.Z * sp * length);
            m++;
        }

        // ---- the step --------------------------------------------------------------------------------------------------------

        // After StemsBegin (the jets' leans of the step), before the discs: the parts at the pose now, the cache, the stems near
        // them made active, and every active stem's bend stepped against the parts; the parts' forces as impulses.
        void PartStems(in FlightState now, in MassProperties body, ref Double3 impulse, ref Double3 angular)
        {
            for (int g = 0; g < PartGroups; g++) PartLast[g] = default;
            if (!_partsOn || !PartPush) return;
            long step = now.Step;
            Double3 lo = new Double3(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity), hi = lo * -1;
            double grow = 0;
            for (int m = 0; m < _masses; m++) _mOn[m] = false;
            int shapes = _contacts.ShapeCount < _pOn.Length ? _contacts.ShapeCount : _pOn.Length;
            for (int i = 0; i < shapes; i++)
            {
                _pOn[i] = false;
                if (_contacts.ShapeOff(i)) continue;
                ContactShape sh = _contacts.Shape(i);
                if (sh.Role == ContactRole.Disc || sh.Role == ContactRole.Blade && !_contacts.Resting(sh.Corner)) continue;
                Double3 a = now.Position + now.Attitude.Rotate(sh.A), b = now.Position + now.Attitude.Rotate(sh.B);
                double r = sh.Radius;
                _pOn[i] = true;
                _pA[i] = a;
                _pB[i] = b;
                _pR[i] = r;
                _pLo[i] = new Double3(DetMath.Min(a.X, b.X) - r, DetMath.Min(a.Y, b.Y) - r, DetMath.Min(a.Z, b.Z) - r);
                _pHi[i] = new Double3(DetMath.Max(a.X, b.X) + r, DetMath.Max(a.Y, b.Y) + r, DetMath.Max(a.Z, b.Z) + r);
                int mp = _massOf[i];
                if (!_mOn[mp])
                {
                    _mOn[mp] = true;
                    _mLo[mp] = _pLo[i];
                    _mHi[mp] = _pHi[i];
                }
                else
                {
                    _mLo[mp] = Min(_mLo[mp], _pLo[i]);
                    _mHi[mp] = Max(_mHi[mp], _pHi[i]);
                }
                lo = Min(lo, _pLo[i]);
                hi = Max(hi, _pHi[i]);
                grow = DetMath.Max(grow, r);
            }
            if (!(lo.Y < double.PositiveInfinity)) return;
            // Near the field: the drone's lowest point under StemWakeTop over the highest ground under its hubs (a world without
            // ground heights, the test range's plane, has no field and its drawn stems only).
            double ground = double.NegativeInfinity;
            for (int c = 0; c < 4; c++) ground = DetMath.Max(ground, _hubGround[c].Height);
            bool field = ground > double.NegativeInfinity, near = !field || lo.Y - ground < StemWakeTop;
            if (!near)
            {
                _pValid = false;
                _pCount = 0;
            }
            else if (!_pValid || (now.Position - _pAt).Length() > StemCacheRefresh || lo.Y < _pLow - StemCacheRefresh)
                PartCache(now.Position, lo.Y, field);

            // The cached stems that meet a part become active: they lean then by the props' law in two dimensions, the parts as
            // constraints. A stem's natural link rules it out cheaply; a stem based under a disc may lie folded any way, so its whole
            // length is its reach.
            double reach = grow + 0.01; // the parts' largest radius and a thick stem's half diameter
            double R2 = _radius * _radius;
            for (int e = 0; e < _pCount; e++)
            {
                ref Stalk k = ref _pStalk[e];
                Double3 tip = _pTip[e];
                if (DetMath.Max(k.Base.Y, tip.Y) < lo.Y - reach || k.Base.Y > hi.Y + reach) continue;
                double around = reach;
                for (int c = 0; c < 4; c++)
                {
                    double hx = k.Base.X - _hubXZ[c].X, hz = k.Base.Z - _hubXZ[c].Z;
                    if (_discOf[c] >= 0 && hx * hx + hz * hz <= R2) around = reach + k.Length;
                }
                if (DetMath.Max(k.Base.X, tip.X) < lo.X - around || DetMath.Min(k.Base.X, tip.X) > hi.X + around) continue;
                if (DetMath.Max(k.Base.Z, tip.Z) < lo.Z - around || DetMath.Min(k.Base.Z, tip.Z) > hi.Z + around) continue;
                bool any = false;
                for (int mp = 0; mp < _masses && !any; mp++) any = _mOn[mp] && Overlaps(k.Base, tip, _mLo[mp], _mHi[mp], around);
                if (!any) continue;
                // Its lean now: its state's if it has one (the jet's, or as the parts left it), else its natural one.
                int s = Find(k.Id);
                double len = k.Length, bx = k.Phi0 * k.Lean.X, bz = k.Phi0 * k.Lean.Z;
                if (s >= 0)
                {
                    ref StemState t = ref _st[s];
                    if (t.Active || !(t.Len > MinLength)) continue;
                    len = t.Len;
                    bx = t.Phi * k.Lean.X + t.CX;
                    bz = t.Phi * k.Lean.Z + t.CZ;
                }
                Link(k, len, bx, bz, out tip, out _);
                if (!Meets(k.Base, tip, 0.5 * k.T.D)) continue;
                if (_aCount == ActiveMax)
                {
                    PartOverflow++;
                    continue;
                }
                s = State(k.Id, k.Length, k.Phi0, step);
                ref StemState a = ref _st[s];
                a.Seen = step;
                a.Active = true;
                _aStalk[_aCount] = k;
                _aStalk[_aCount].Slot = s;
                _aSlot[_aCount++] = s;
            }

            // Every active stem's lean, against the parts; one no part touches whose lean across its way has come home is the
            // jet's (PropStems') again.
            for (int j = 0; j < _aCount; j++)
            {
                ref Stalk k = ref _aStalk[j];
                int s = _aSlot[j];
                if (_st[s].Key != k.Id)
                {
                    Drop(j--); // its slot was taken by another stem: its lean is lost with it
                    continue;
                }
                ref StemState t = ref _st[s];
                t.Seen = step;
                bool touched = Bend(k, ref t, now, body, ref impulse, ref angular, out double away);
                if (!touched && away < HomeLean)
                {
                    // Its lean along its way back into the jet's φ; across it, its set (a lodged stem's, else nothing) is kept.
                    double along = t.CX * k.Lean.X + t.CZ * k.Lean.Z;
                    t.Phi += along;
                    t.CX -= along * k.Lean.X;
                    t.CZ -= along * k.Lean.Z;
                    DetMath.SinCos(t.Phi, out t.Sin, out t.Cos);
                    t.Arm = 0;
                    t.Rest = false;
                    t.Active = false;
                    t.Bent = t.CX != 0 || t.CZ != 0 || t.SX != 0 || t.SZ != 0;
                    t.Top = TopOf(k, s);
                    Drop(j--);
                    continue;
                }
                t.Top = TopOf(k, s);
            }
        }

        // The state's slot of stem id if it has one, else −1 (no insert).
        int Find(ulong id)
        {
            int home = (int)(FlightRandom.Mix64(id) & (StateSlots - 1));
            for (int p = 0; p < StateProbe; p++)
            {
                int i = (home + p) & (StateSlots - 1);
                if (_st[i].Key == id) return i;
                if (_st[i].Key == 0) return -1;
            }
            return -1;
        }

        // Whether a link base–tip of half thickness half meets any part now.
        bool Meets(Double3 basePoint, Double3 tip, double half)
        {
            for (int mp = 0; mp < _masses; mp++)
            {
                if (!_mOn[mp] || !Overlaps(basePoint, tip, _mLo[mp], _mHi[mp], half)) continue;
                for (int at = _massStart[mp]; at < _massStart[mp + 1]; at++)
                {
                    int i = _massShapes[at];
                    if (!_pOn[i] || !Overlaps(basePoint, tip, _pLo[i], _pHi[i], half)) continue;
                    double re = _pR[i] + half;
                    if (FiberContacts.SegmentSegment(basePoint, tip, _pA[i], _pB[i], out _, out _) < re * re) return true;
                }
            }
            return false;
        }

        void Drop(int j)
        {
            _aCount--;
            if (j == _aCount) return;
            _aStalk[j] = _aStalk[_aCount];
            _aSlot[j] = _aSlot[_aCount];
        }

        static Double3 Min(Double3 a, Double3 b) => new Double3(DetMath.Min(a.X, b.X), DetMath.Min(a.Y, b.Y), DetMath.Min(a.Z, b.Z));

        static Double3 Max(Double3 a, Double3 b) => new Double3(DetMath.Max(a.X, b.X), DetMath.Max(a.Y, b.Y), DetMath.Max(a.Z, b.Z));

        // One active stem's step under the props' stems' own lean law (PropStems', w9 2.12–2.13) in two dimensions: its whole lean
        // b = φ·ê + c (φ held while it is active) follows b' = −m(b)/τ at its first mode's τ, m its net moment over 4EI/L (its spring
        // about its natural lean and set, less its jet's load and its weight along its lean now); touched at an arm s its spring is the
        // cantilever's there, 3EI/s. Then each part its link meets moves it out (a constraint) and takes the generalised force that
        // move took, (4EI/L·τ/dt + K)·Δb, no more than its root carries; at a pair's first touch the part also gives the stem's mass
        // there its own speed (an inelastic impulse). True when a part touched it; away, its lean's distance across its way from its
        // rest (its natural lean and set).
        bool Bend(in Stalk k, ref StemState t, in FlightState now, in MassProperties body, ref Double3 impulse, ref Double3 angular, out double away)
        {
            double dt = FlightCore.Dt, len = t.Len, ei = k.T.EI, mu = k.T.Mu, d = k.T.D, half = 0.5 * d;
            double l2 = len * len, l3 = l2 * len, k4 = 4 * ei / len, tau = l2 * k.Tau0, rate = dt / tau;
            double K = t.Arm > 0 ? 3 * ei / t.Arm : k4;
            double bx = t.Phi * k.Lean.X + t.CX, bz = t.Phi * k.Lean.Z + t.CZ;
            // Its root yields past ε_f·2EI/d: the set follows the lean so its spring carries no more (a plastic hinge; the stem stays
            // where it yielded to, lodged).
            double yield = StemYieldStrain * 2 * ei / d;
            double ex = bx - (k.Phi0 * k.Lean.X + t.SX), ez = bz - (k.Phi0 * k.Lean.Z + t.SZ), e = DetMath.Sqrt(ex * ex + ez * ez);
            if (K * e > yield)
            {
                double give = 1 - yield / (K * e);
                t.SX += ex * give;
                t.SZ += ez * give;
                ex -= ex * give;
                ez -= ez * give;
            }
            // Its weight's and its jet's load at the lean now, along its way, over 4EI/L (PropStems' A·sin(φ + β)|sin(φ + β)| + G·sin φ
            // under 2.12's column, K·|u_φ|·u_φ + G·sin φ in the wash).
            double phi = DetMath.Sqrt(bx * bx + bz * bz), wx = k.Lean.X, wz = k.Lean.Z;
            if (phi > 1e-12)
            {
                wx = bx / phi;
                wz = bz / phi;
            }
            DetMath.SinCos(phi, out double sp, out double cp);
            double q = t.Loaded == now.Step ? t.Q : 0, load = k.MuG8 * l3 * sp;
            if (q > 0 && _air == null)
            {
                double sj = sp, beta = t.Swirl;
                if (beta != 0) DetMath.SinCos(phi + beta, out sj, out _);
                load += q * k.T.Kappa0 * l3 * sj * DetMath.Abs(sj);
            }
            else if (q > 0)
            {
                int slot = k.Slot;
                double ul = _sUx[slot] * wx + _sUz[slot] * wz, uy = _sUy[slot], uphi = ul * cp - uy * sp;
                load += 0.5 * _rho * k.T.Kappa0 * l3 * DetMath.Abs(uphi) * uphi;
            }
            // Its free step, implicit in its spring: b − m·r/(1 + (K/k)·r), r = dt/τ.
            double kr = K / k4, den = 1 + kr * rate;
            bx -= (kr * ex - load * wx) * rate / den;
            bz -= (kr * ez - load * wz) * rate / den;
            Clamp(ref bx, ref bz);
            double fx = bx, fz = bz;

            // Each part its link meets moves it out (a few passes, so a stem between two parts settles where it can); the moves
            // summed per part.
            int hits = 0;
            Link(k, len, bx, bz, out Double3 tip, out Double3 axis);
            for (int pass = 0; pass < ProjectionPasses; pass++)
            {
                bool moved = false;
                for (int mp = 0; mp < _masses; mp++)
                {
                    if (!_mOn[mp] || !Overlaps(k.Base, tip, _mLo[mp], _mHi[mp], half)) continue;
                    for (int at = _massStart[mp]; at < _massStart[mp + 1]; at++)
                    {
                        int i = _massShapes[at];
                        if (!_pOn[i] || !Overlaps(k.Base, tip, _pLo[i], _pHi[i], half)) continue;
                        double re = _pR[i] + half;
                        double d2 = FiberContacts.SegmentSegment(k.Base, tip, _pA[i], _pB[i], out double u, out double w);
                        if (!(d2 < re * re)) continue;
                        Double3 ps = k.Base + (tip - k.Base) * u, pc = _pA[i] + (_pB[i] - _pA[i]) * w;
                        double dist = DetMath.Sqrt(d2);
                        Double3 n = dist > 1e-12 ? (ps - pc) * (1 / dist) : default;
                        double push = re - dist;
                        // The side it met the part on, kept while they touch: a part whose core passed the stem's line holds it there.
                        bool kept = t.Part == i && t.Touch == now.Step - 1;
                        if (kept)
                        {
                            var side = new Double3(t.NX, t.NY, t.NZ);
                            if (!(Double3.Dot(n, side) > 0))
                            {
                                n = side;
                                push = re + dist;
                            }
                        }
                        else if (!(dist > 1e-12))
                        {
                            n = Away(ps, now.Position);
                            push = re;
                        }
                        double s = u * len;
                        if (s < MinLength) s = MinLength;
                        double along = Double3.Dot(n, axis);
                        Double3 across = n - axis * along;
                        double w2 = Double3.Dot(across, across);
                        Turn(bx, bz, s, across * (push / DetMath.Max(w2, LeastAcross * LeastAcross)), out double dbx, out double dbz);
                        double nbx = bx + dbx, nbz = bz + dbz;
                        Clamp(ref nbx, ref nbz);
                        dbx = nbx - bx;
                        dbz = nbz - bz;
                        bx = nbx;
                        bz = nbz;
                        Link(k, len, bx, bz, out tip, out axis);
                        moved = true;
                        int h = 0;
                        while (h < hits && _hPart[h] != i) h++;
                        if (h == hits)
                        {
                            if (hits == MaxHits) continue;
                            hits++;
                            _hPart[h] = i;
                            _hX[h] = _hZ[h] = 0;
                            _hFresh[h] = t.Touch != now.Step - 1; // a stem already moving with the drone is not set moving again
                        }
                        _hX[h] += dbx;
                        _hZ[h] += dbz;
                        _hN[h] = n;
                        _hPoint[h] = pc + n * _pR[i];
                        _hS[h] = s;
                        _hAlong[h] = DetMath.Abs(along);
                        _hAcross[h] = w2;
                    }
                }
                if (!moved) break;
            }
            bool touched = hits > 0;
            // A stem a part meets within the part's own reach of its base (the part is on its root) is crushed under it: it lies flat
            // the way it leans, its root yielded (lodged), and carries nothing (the ground carries the part).
            bool crushed = false;
            for (int h = 0; h < hits && !crushed; h++)
                crushed = _hPoint[h].Y - k.Base.Y < _pR[_hPart[h]] + half;
            if (crushed)
            {
                double lean = DetMath.Sqrt(bx * bx + bz * bz);
                double wayX = lean > 1e-12 ? bx / lean : k.Lean.X, wayZ = lean > 1e-12 ? bz / lean : k.Lean.Z;
                bx = Pi / 2 * wayX;
                bz = Pi / 2 * wayZ;
                t.SX = bx - k.Phi0 * k.Lean.X;
                t.SZ = bz - k.Phi0 * k.Lean.Z;
                hits = 0;
            }
            int main = 0;
            for (int h = 1; h < hits; h++)
                if (_hX[h] * _hX[h] + _hZ[h] * _hZ[h] > _hX[main] * _hX[main] + _hZ[main] * _hZ[main]) main = h;
            if (hits > 0)
            {
                // The parts' generalised force on the stem, (k·τ/dt + K)·Δb, Δb its lean's move off its free step, no more than its
                // root carries; shared among the parts by their moves along it (moves that cancel, a stem squeezed between two parts,
                // moved nothing and load nothing), each over its arm across the stem along its normal, its share along the stem at most
                // Euler's π²EI/(4L²). At a pair's first touch, the stem's mass at the contact (its link's inertia there, at most its
                // whole mass) brought to the part's speed across it.
                double s = _hS[main], Kc = 3 * ei / s, gx = bx - fx, gz = bz - fz, g = DetMath.Sqrt(gx * gx + gz * gz);
                double Q = DetMath.Min((k4 * tau / dt + Kc) * g, yield), shares = 0;
                for (int h = 0; h < hits; h++) shares += DetMath.Max(0, _hX[h] * gx + _hZ[h] * gz);
                double pcr = Pi * Pi * ei / (4 * l2), linkInertia = StemLinkInertia * mu * l3;
                for (int h = 0; h < hits; h++)
                {
                    double normal = shares > 0 ? DetMath.Max(0, _hX[h] * gx + _hZ[h] * gz) / shares * Q / _hS[h] / DetMath.Sqrt(DetMath.Max(_hAcross[h], LeastAcross * LeastAcross)) : 0;
                    if (normal * _hAlong[h] > pcr) normal = pcr / _hAlong[h];
                    if (_hFresh[h])
                    {
                        double closing = Double3.Dot(PointVelocity(now, _hPoint[h]), _hN[h]);
                        double mass = DetMath.Min(linkInertia / (_hS[h] * _hS[h]), mu * len);
                        if (closing > 0) normal += mass * closing * _hAcross[h] / dt;
                    }
                    if (normal > 0) Push(_hPart[h], _hN[h] * -normal, _hPoint[h], k.T.Code, now, body, ref impulse, ref angular);
                }
                t.Part = _hPart[main];
                t.NX = _hN[main].X;
                t.NY = _hN[main].Y;
                t.NZ = _hN[main].Z;
                t.Arm = s;
            }
            else t.Arm = 0;
            if (touched) t.Touch = now.Step;
            t.CX = bx - t.Phi * k.Lean.X;
            t.CZ = bz - t.Phi * k.Lean.Z;
            t.Bent = true;
            // How far its lean lies across its way from its rest.
            double rx = bx - (k.Phi0 * k.Lean.X + t.SX), rz = bz - (k.Phi0 * k.Lean.Z + t.SZ);
            away = DetMath.Abs(-rx * k.Lean.Z + rz * k.Lean.X);
            return touched;
        }

        // The lean vector (bx, bz) of a link no flatter than lying on the ground.
        static void Clamp(ref double bx, ref double bz)
        {
            double phi = DetMath.Sqrt(bx * bx + bz * bz);
            if (!(phi > Pi / 2)) return;
            double scale = Pi / 2 / phi;
            bx *= scale;
            bz *= scale;
        }

        // A stem's link at the lean (bx, bz): its tip and axis.
        static void Link(in Stalk k, double len, double bx, double bz, out Double3 tip, out Double3 axis)
        {
            double phi = DetMath.Sqrt(bx * bx + bz * bz), ex = k.Lean.X, ez = k.Lean.Z;
            if (phi > 1e-12)
            {
                ex = bx / phi;
                ez = bz / phi;
            }
            DetMath.SinCos(phi, out double sp, out double cp);
            axis = new Double3(ex * sp, cp, ez * sp);
            tip = k.Base + axis * len;
        }

        // The lean's change that moves a link's point at arm s by move (world, across the link), at the lean (bx, bz): along its
        // way it moves s·(cos φ·ê, −sin φ) a radian, across it s·(sin φ/φ)·ê⊥ (upright, s·Δb).
        static void Turn(double bx, double bz, double s, Double3 move, out double dbx, out double dbz)
        {
            double phi = DetMath.Sqrt(bx * bx + bz * bz);
            if (!(phi > 1e-9))
            {
                dbx = move.X / s;
                dbz = move.Z / s;
                return;
            }
            double ex = bx / phi, ez = bz / phi;
            DetMath.SinCos(phi, out double sp, out double cp);
            double dphi = (move.X * cp * ex - move.Y * sp + move.Z * cp * ez) / s;
            double dpsi = (-move.X * ez + move.Z * ex) / s * (phi / sp);
            dbx = dphi * ex - dpsi * ez;
            dbz = dphi * ez + dpsi * ex;
        }

        // A link's bounds grown by grow against a box.
        static bool Overlaps(Double3 a, Double3 b, Double3 lo, Double3 hi, double grow) =>
            !(DetMath.Max(a.X, b.X) < lo.X - grow || DetMath.Min(a.X, b.X) > hi.X + grow || DetMath.Max(a.Y, b.Y) < lo.Y - grow
              || DetMath.Min(a.Y, b.Y) > hi.Y + grow || DetMath.Max(a.Z, b.Z) < lo.Z - grow || DetMath.Min(a.Z, b.Z) > hi.Z + grow);

        // A stem's point on a part's core with no side to keep: pushed away from the body origin across.
        static Double3 Away(Double3 p, Double3 origin)
        {
            Double3 h = new Double3(p.X - origin.X, 0, p.Z - origin.Z);
            double l = h.Length();
            return l > 1e-12 ? h * (1 / l) : new Double3(1, 0, 0);
        }

        // A force on part shape i at point (world): its impulse and moment on the body this step, and its group's record.
        void Push(int i, Double3 f, Double3 point, ushort code, in FlightState now, in MassProperties body, ref Double3 impulse, ref Double3 angular)
        {
            double dt = FlightCore.Dt;
            Double3 j = f * dt;
            impulse = impulse + j;
            angular = angular + Double3.Cross(now.Attitude.InverseRotate(point - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(j));
            int g = _groupOf[i];
            PartLast[g].Force = PartLast[g].Force + f;
            PartLast[g].Stems++;
            _fImpulse[g] = _fImpulse[g] + j;
            double size = j.Length();
            _fPoint[g] = _fPoint[g] + point * size;
            _fWeight[g] += size;
            _fStems[g]++;
            if (code >= 1 && code <= 7) _fKind[g * 7 + code - 1] += size;
        }

        // ---- the leaves --------------------------------------------------------------------------------------------------------

        // A crown's leaves on every part in a crown's shell (not a turning disc, whose leaves are Leaves'): Newton's impact law.
        void PartLeaves(in FlightState now, in MassProperties body, ref Double3 impulse, ref Double3 angular)
        {
            if (!_spec.PartStems || !PartPush || _lad == null || !_contacts.InCrown) return;
            double dt = FlightCore.Dt;
            int shapes = _contacts.ShapeCount < _leafSkip.Length ? _contacts.ShapeCount : _leafSkip.Length;
            for (int i = 0; i < shapes; i++)
            {
                if (_leafSkip[i] || _contacts.ShapeOff(i)) continue;
                ContactShape sh = _contacts.Shape(i);
                if (sh.Role == ContactRole.Disc || sh.Role == ContactRole.Blade && !_contacts.Resting(sh.Corner)) continue;
                if (!_contacts.CrownOf(i, out int obj, out ushort material)) continue;
                CrownRow k = material < _crown.Length ? _crown[material] : null;
                if (k == null || obj < 0 || obj >= _lad.Length || !(_lad[obj] > 0)) continue;
                double rho = _lad[obj] * k.LeafFreshMass, r = sh.Radius;
                Double3 a = now.Position + now.Attitude.Rotate(sh.A), b = now.Position + now.Attitude.Rotate(sh.B), centre = (a + b) * 0.5;
                Double3 v = PointVelocity(now, centre), core = b - a;
                double l = core.Length(), speed = v.Length();
                Double3 across = l > 0 ? v - core * (Double3.Dot(v, core) / (l * l)) : default;
                Double3 f = across * (-rho * NewtonCylinder * 2 * r * l * across.Length()) + v * (-rho * NewtonSphere * Pi * r * r * speed);
                if (!(f.X != 0 || f.Y != 0 || f.Z != 0)) continue;
                Double3 j = f * dt;
                impulse = impulse + j;
                angular = angular + Double3.Cross(now.Attitude.InverseRotate(centre - now.Position) - body.CenterOfMass, now.Attitude.InverseRotate(j));
                int g = _groupOf[i], at = PartGroups + g;
                PartLast[g].Leaves = PartLast[g].Leaves + f;
                PartLast[g].Force = PartLast[g].Force + f;
                _fImpulse[at] = _fImpulse[at] + j;
                double size = j.Length();
                _fPoint[at] = _fPoint[at] + centre * size;
                _fWeight[at] += size;
                _fStems[at]++;
                _fObject[g] = obj;
                _fMaterial[g] = material;
            }
        }

        // ---- the log ---------------------------------------------------------------------------------------------------------

        // At a field's last step: each group's push over the field (stems, then leaves), one PlantPush each that was pushed.
        void PartsEnd(in FlightState now, StepEvents events)
        {
            if (!_spec.PartStems || now.Step % FlightCore.FieldSteps != 0) return;
            double field = FlightCore.FieldSteps * FlightCore.Dt;
            for (int at = 0; at < 2 * PartGroups; at++)
            {
                if (_fStems[at] == 0) continue;
                int g = at % PartGroups;
                bool leaves = at >= PartGroups;
                FlightEvent e = FlightEvent.Of(EventKind.PlantPush);
                e.Index = (byte)(g | (leaves ? 0x40 : 0));
                e.Part = g < 4 && _spec.Legs != null ? (ushort)_spec.Legs.Parts[g] : FlightEvent.NoPart;
                e.Surface = WorldIds.NoSurface;
                if (leaves)
                {
                    e.Object = _fObject[g];
                    e.Material = _fMaterial[g];
                }
                else
                {
                    e.Object = WorldIds.Ground;
                    int most = 0;
                    for (int c = 1; c < 7; c++)
                        if (_fKind[g * 7 + c] > _fKind[g * 7 + most]) most = c;
                    e.Material = (ushort)(most + 1);
                    for (int c = 0; c < 7; c++) _fKind[g * 7 + c] = 0;
                }
                e.Shape = _fStems[at];
                e.Point = _fWeight[at] > 0 ? _fPoint[at] * (1 / _fWeight[at]) : now.Position;
                e.A = _fImpulse[at].X / field;
                e.B = _fImpulse[at].Y / field;
                e.C = _fImpulse[at].Z / field;
                events.Add(e);
                _fImpulse[at] = _fPoint[at] = default;
                _fWeight[at] = 0;
                _fStems[at] = 0;
            }
        }
    }
}
