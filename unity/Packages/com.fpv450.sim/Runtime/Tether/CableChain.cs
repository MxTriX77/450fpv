using System;

namespace Fpv450.Sim.Tether
{
    /// <summary>
    /// A member one cable node touches (p7d-tether design 2, 6): a plane through Point with the outward unit Normal,
    /// moving at Velocity, set by the caller before each step (the end-of-step place of the member). The normal is a
    /// one-sided penalty spring of Stiffness with a damper of Damping; while it presses, a tangential spring of
    /// HoldStiffness holds the node at the point where it touched (the chain's Hold), slipping by Coulomb's law at
    /// KineticFriction once its force passes StaticFriction times the normal force. With Capstan set, line may slide
    /// through the node by Euler–Eytelwein's capstan (the wrap round an edge, a twig, a rod, a wire, the exit lip).
    /// </summary>
    public struct CableContact
    {
        public bool Active;
        public Double3 Point, Normal, Velocity;
        /// <summary>N/m, N·s/m and N/m: the member's (the caller's labelled values).</summary>
        public double Stiffness, Damping, HoldStiffness;
        /// <summary>μs and μk, the line's on the member.</summary>
        public double StaticFriction, KineticFriction;
        public bool Capstan;

        /// <summary>The chain's: the hold is set (the node pressed on the member in the last step).</summary>
        public bool Engaged;
        /// <summary>The chain's: where the node is held along the member, m (only its tangential part acts).</summary>
        public Double3 Hold;
        /// <summary>The chain's: the normal force at the step's end, N (above 0 pressing).</summary>
        public double NormalForce;

        // This step's normal model: 0 none, 1 pressing (the spring from the surface), 2 speculative (from the gap at
        // the step's start, the node being predicted to reach the surface within the step).
        internal byte Mode;
        internal double Rest;

        /// <summary>Points the contact at a member; the hold starts afresh.</summary>
        public static CableContact On(Double3 point, Double3 normal, Double3 velocity, double stiffness, double damping,
            double holdStiffness, double staticFriction, double kineticFriction, bool capstan) => new CableContact
        {
            Active = true, Point = point, Normal = normal, Velocity = velocity, Stiffness = stiffness, Damping = damping,
            HoldStiffness = holdStiffness, StaticFriction = staticFriction, KineticFriction = kineticFriction, Capstan = capstan,
        };
    }

    /// <summary>A lumped node of a cable chain. The caller sets Force, Drag and Air each step (the loads, design 3–5),
    /// the anchor and the contacts; the chain keeps Material and Mass.</summary>
    public struct CableNode
    {
        public Double3 Position, Velocity;
        /// <summary>The material coordinate s: unstretched metres from the line's fixed end (node 0's side).</summary>
        public double Material;
        /// <summary>kg: the line's mass per metre times half of each neighbouring segment's rest length.</summary>
        public double Mass;
        /// <summary>An external force held over the step, N (not gravity, which the chain adds).</summary>
        public Double3 Force;
        /// <summary>A linearised drag, N·s/m: the force −Drag·(v − Air), taken implicitly.</summary>
        public Sym3 Drag;
        public Double3 Air;
        /// <summary>A bilateral spring and damper to AnchorTarget moving at AnchorVelocity (0 stiffness: none).</summary>
        public double AnchorStiffness, AnchorDamping;
        public Double3 AnchorTarget, AnchorVelocity;
        /// <summary>Two members at most: a node touching two takes both on its diagonal (the wedge, design 6).</summary>
        public CableContact Contact0, Contact1;
    }

    /// <summary>What the energy check made of a step (design 13).</summary>
    public enum CableGuard : byte
    {
        /// <summary>The energy rose no more than the work done on the line, within rounding.</summary>
        Kept = 0,
        /// <summary>The linearisation's own deficit raised it: the rise was taken back from the relative motion
        /// (labelled numerical damping, never a source).</summary>
        Clipped = 1,
        /// <summary>A rise beyond what the linearisation can make, or a non-finite value: a breach (after the safe solve,
        /// a TetherWedge: a physics bug).</summary>
        Wedge = 2,
    }

    /// <summary>A TetherWedge (design 13): a physics bug, never pilot error. The flight logs it once the chain is in the
    /// step (1.6); the step is undone and the line stopped.</summary>
    public struct CableWedge
    {
        public long Step;
        /// <summary>The first node with a non-finite value, or −1 for an energy breach.</summary>
        public int Node;
        public bool NonFinite;
        /// <summary>J: the rise over the step's budget, the linearisation's deficit, and what the relative motion held.</summary>
        public double Excess, Deficit, Removable;
    }

    /// <summary>
    /// One cable (p7d-tether design 2): the fiber's span and every active conductor. Lumped nodes joined by
    /// tension-only segments (EA/ℓ₀ along each, T/ℓ across it), stepped inside the 1 ms step by linearly implicit
    /// (backward) Euler: a block-tridiagonal solve (Thomas without pivoting, 3 × 3 blocks inverted by cofactors), then
    /// positions from the new velocities. No sub-steps; only + − × ÷ √ and DetMath; preallocated, 0 B a step.
    ///
    /// Energy (design 13). Each force is taken through a quadratic model that equals its true energy at the step's
    /// start; backward Euler on quadratic models dissipates (it damps a mode by about ω·dt/2 a radian: the labelled
    /// numerical damping, never a source). Where the true energy is not quadratic, the model is chosen above it: a taut
    /// segment's exact tangent; a slack segment predicted to tighten within the step as a spring along its direction from
    /// its length at the start; a node pressing into a member as the spring from the surface; one predicted to reach it as
    /// a spring from its gap at the start. A touch or a tightening the explicit prediction missed is taken speculative
    /// and the step solved again from its start (at most MaxCorrections times; the same dt, never a sub-step). What the
    /// models still miss (a segment stretched sideways) is their deficit, computed exactly at the step's end: a rise
    /// within it is taken back from the line's relative motion (CableGuard.Clipped); one the motion cannot give back
    /// solves the step once more on majorizing models (SafeSteps), which cannot gain energy; a rise after that, or a
    /// non-finite value, is a TetherWedge.
    /// </summary>
    public sealed class CableChain
    {
        /// <summary>The check's tolerance, relative to the energy's magnitude (the sum of its terms' sizes): rounding.</summary>
        public const double EnergyTolerance = 1e-12;

        // A slide leaves a segment at least this rest length, m (numerical: the merge then takes it).
        const double MinRest = 1e-6;

        /// <summary>The solves a step may add when its prediction missed a touch or a tightening (numerical).</summary>
        public const int MaxCorrections = 2;

        public readonly int Capacity;
        /// <summary>kg/m and N: the line's (the table's or the wire record's).</summary>
        public readonly double MassPerMetre, AxialStiffness;
        /// <summary>m/s², world axes.</summary>
        public Double3 Gravity;

        public int Count { get; private set; }
        public long Steps { get; private set; }

        /// <summary>J: the true energy at the last step's start (with its targets) and end, its budget (the start plus
        /// the work of the loads, the air and the members' motion over the step), the models' deficit at its end.</summary>
        public double EnergyAtStart { get; private set; }
        public double EnergyAtEnd { get; private set; }
        public double Budget { get; private set; }
        public double Deficit { get; private set; }
        public CableGuard LastGuard { get; private set; }

        /// <summary>The steps the check clipped and the energy it took back, J (labelled numerical damping).</summary>
        public long Clips { get; private set; }
        public double ClippedEnergy { get; private set; }
        public long Wedges { get; private set; }
        public CableWedge LastWedge { get; private set; }

        /// <summary>Material slid through capstan contacts, splits and merges, and splits refused for want of room.</summary>
        public long Slides { get; private set; }
        public long Splits { get; private set; }
        public long Merges { get; private set; }
        public long SplitsRefused { get; private set; }
        /// <summary>The extra solves the active-set correction took, and the steps solved again on majorizing models.</summary>
        public long Corrections { get; private set; }
        public long SafeSteps { get; private set; }

        readonly CableNode[] _nodes;

        // Spacing (design 2, 3): the target rest length h0 within NearLength of the focus, growing by Growth a node beyond.
        double _h0, _near, _growth, _focus;

        // This step's segment models: 0 none, 1 taut (the exact tangent), 2 speculative (along d from its start length).
        readonly byte[] _segKind;
        readonly Double3[] _segDir;
        readonly double[] _segLen, _segTension, _segStiff, _segEnergy;

        readonly Sym3[] _diag, _off, _invD;
        readonly Double3[] _rhs, _y, _pred;
        readonly Double3[] _x0, _v0;
        readonly double[] _s0;
        readonly CableContact[] _c0, _c1;
        readonly double[] _capLen, _capT, _capExp, _capFactor, _capS;

        public CableChain(int capacity, double massPerMetre, double axialStiffness)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!(massPerMetre > 0) || !(axialStiffness > 0)) throw new ArgumentOutOfRangeException(nameof(massPerMetre));
            Capacity = capacity;
            MassPerMetre = massPerMetre;
            AxialStiffness = axialStiffness;
            _nodes = new CableNode[capacity];
            _segKind = new byte[capacity];
            _segDir = new Double3[capacity];
            _segLen = new double[capacity];
            _segTension = new double[capacity];
            _segStiff = new double[capacity];
            _segEnergy = new double[capacity];
            _diag = new Sym3[capacity];
            _off = new Sym3[capacity];
            _invD = new Sym3[capacity];
            _rhs = new Double3[capacity];
            _y = new Double3[capacity];
            _pred = new Double3[capacity];
            _x0 = new Double3[capacity];
            _v0 = new Double3[capacity];
            _s0 = new double[capacity];
            _c0 = new CableContact[capacity];
            _c1 = new CableContact[capacity];
            _capLen = new double[capacity];
            _capT = new double[capacity];
            _capExp = new double[capacity];
            _capFactor = new double[capacity];
            _capS = new double[capacity];
        }

        public ref CableNode this[int i]
        {
            get
            {
                if ((uint)i >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(i));
                return ref _nodes[i];
            }
        }

        /// <summary>Lays the line out at rest: count nodes at positions, with material coordinates rising from node 0.</summary>
        public void Reset(Double3[] positions, double[] material, int count)
        {
            if (count < 2 || count > Capacity) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && !(material[i] > material[i - 1])) throw new ArgumentException("material coordinates must rise");
                _nodes[i] = new CableNode { Position = positions[i], Material = material[i] };
            }
            Count = count;
            for (int i = 0; i < count; i++) _nodes[i].Mass = MassOf(i);
            Steps = 0;
            EnergyAtEnd = Energy(out _);
        }

        /// <summary>The spacing law: rest length h0 within nearLength (material metres) of focus, then growing by growth a
        /// node (h = h0 + (growth − 1)·(distance − nearLength)). Segments longer than their target split at their midpoint;
        /// two neighbours whose sum fits one target merge, if that loses energy. h0 ≤ 0 turns it off.</summary>
        public void SetSpacing(double h0, double nearLength, double growth, double focus)
        {
            _h0 = h0;
            _near = nearLength;
            _growth = growth;
            _focus = focus;
        }

        /// <summary>Segment j's tension at the current state, N (0 when slack).</summary>
        public double Tension(int j)
        {
            Double3 d = _nodes[j + 1].Position - _nodes[j].Position;
            double rest = _nodes[j + 1].Material - _nodes[j].Material;
            double stretch = d.Length() - rest;
            return stretch > 0 ? AxialStiffness / rest * stretch : 0;
        }

        /// <summary>The force the chain puts on node i's anchor at the current state, N (the drone's at its exit, applied
        /// to its next step).</summary>
        public Double3 AnchorForce(int i)
        {
            ref CableNode n = ref _nodes[i];
            return (n.Position - n.AnchorTarget) * n.AnchorStiffness + (n.Velocity - n.AnchorVelocity) * n.AnchorDamping;
        }

        /// <summary>The true energy (kinetic, elastic, gravitational, the anchors' and the contacts' springs), J, with the
        /// targets as they stand; magnitude is the sum of its terms' sizes (the scale of the check's tolerance).</summary>
        public double Energy(out double magnitude)
        {
            double kinetic = 0, elastic = 0, gravity = 0, springs = 0;
            int n = Count;
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                kinetic += 0.5 * a.Mass * Double3.Dot(a.Velocity, a.Velocity);
                gravity -= a.Mass * Double3.Dot(Gravity, a.Position);
                if (a.AnchorStiffness > 0)
                {
                    Double3 e = a.Position - a.AnchorTarget;
                    springs += 0.5 * a.AnchorStiffness * Double3.Dot(e, e);
                }
                springs += ContactEnergy(a.Position, ref a.Contact0) + ContactEnergy(a.Position, ref a.Contact1);
                if (i + 1 < n)
                {
                    double rest = _nodes[i + 1].Material - a.Material;
                    double stretch = (_nodes[i + 1].Position - a.Position).Length() - rest;
                    if (stretch > 0) elastic += 0.5 * (AxialStiffness / rest) * stretch * stretch;
                }
            }
            magnitude = kinetic + elastic + springs + (gravity < 0 ? -gravity : gravity);
            return kinetic + elastic + gravity + springs;
        }

        static double ContactEnergy(Double3 x, ref CableContact c)
        {
            if (!c.Active) return 0;
            double e = 0, g = Double3.Dot(c.Normal, x - c.Point);
            if (g < 0) e += 0.5 * c.Stiffness * g * g;
            if (c.Engaged && c.HoldStiffness > 0)
            {
                Double3 t = Tangential(x - c.Hold, c.Normal);
                e += 0.5 * c.HoldStiffness * Double3.Dot(t, t);
            }
            return e;
        }

        static Double3 Tangential(Double3 v, Double3 n) => v - n * Double3.Dot(n, v);

        /// <summary>
        /// The check's verdict on a rise (design 13): within the tolerance it is rounding; within the linearisation's
        /// deficit, and no more than the relative motion holds, it is clipped from that motion; anything else, or a
        /// non-finite value, is a breach: the chain solves the step again on majorizing models, and a breach after that is
        /// a TetherWedge.
        /// </summary>
        public static CableGuard Judge(double excess, double deficit, double removable, double tolerance)
        {
            if (!(excess <= tolerance))
            {
                if (double.IsNaN(excess) || double.IsInfinity(excess)) return CableGuard.Wedge;
                if (excess <= deficit + tolerance && excess <= removable) return CableGuard.Clipped;
                return CableGuard.Wedge;
            }
            return CableGuard.Kept;
        }

        /// <summary>One step of dt seconds (the flight's 1 ms).</summary>
        public void Step(double dt)
        {
            int n = Count;
            double dt2 = dt * dt;
            Steps++;
            for (int i = 0; i < n; i++)
            {
                _x0[i] = _nodes[i].Position;
                _v0[i] = _nodes[i].Velocity;
                _s0[i] = _nodes[i].Material;
            }

            // 1. The explicit forces at the start (the members' pressing springs, not the speculative ones) and the
            //    explicit prediction of each node's end, which only chooses the models.
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                Double3 f = Gravity * a.Mass + a.Force - a.Drag * (a.Velocity - a.Air);
                if (a.AnchorStiffness > 0 || a.AnchorDamping > 0)
                    f = f - (a.Position - a.AnchorTarget) * a.AnchorStiffness - (a.Velocity - a.AnchorVelocity) * a.AnchorDamping;
                f = f + PressingForce(a.Position, a.Velocity, ref a.Contact0) + PressingForce(a.Position, a.Velocity, ref a.Contact1);
                _rhs[i] = f;
            }
            for (int j = 0; j + 1 < n; j++)
            {
                Double3 d = _nodes[j + 1].Position - _nodes[j].Position;
                double len = d.Length(), rest = _nodes[j + 1].Material - _nodes[j].Material;
                if (len > rest)
                {
                    Double3 pull = d * (AxialStiffness / rest * (len - rest) / len);
                    _rhs[j] = _rhs[j] + pull;
                    _rhs[j + 1] = _rhs[j + 1] - pull;
                }
            }
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                _pred[i] = a.Position + a.Velocity * dt + _rhs[i] * (dt2 / a.Mass);
            }

            // 2. The contacts' models and holds (a hold starts where the node is, so it starts with no energy).
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                ChooseContact(a.Position, _pred[i], dt, ref a.Contact0);
                ChooseContact(a.Position, _pred[i], dt, ref a.Contact1);
            }

            // 3. The true energy at the start, with this step's targets.
            double startMagnitude;
            double e0 = Energy(out startMagnitude);
            EnergyAtStart = e0;

            // 4. The segments' models: a taut one's exact tangent; a slack one predicted to tighten, a spring along its
            //    direction from its length at the start (above the true energy, which is 0 until the rest length).
            for (int j = 0; j + 1 < n; j++)
            {
                Double3 d = _nodes[j + 1].Position - _nodes[j].Position;
                double len = d.Length(), rest = _nodes[j + 1].Material - _nodes[j].Material, k = AxialStiffness / rest;
                _segStiff[j] = k;
                _segLen[j] = len;
                if (len >= rest && len > 0)
                {
                    _segKind[j] = 1;
                    _segDir[j] = d * (1 / len);
                    _segTension[j] = k * (len - rest);
                    _segEnergy[j] = 0.5 * k * (len - rest) * (len - rest);
                }
                else if (len > 0 && (_pred[j + 1] - _pred[j]).Length() > rest)
                {
                    _segKind[j] = 2;
                    _segDir[j] = d * (1 / len);
                    _segTension[j] = 0;
                    _segEnergy[j] = 0;
                }
                else
                {
                    _segKind[j] = 0;
                    _segTension[j] = 0;
                    _segEnergy[j] = 0;
                }
            }

            for (int i = 0; i < n; i++)
            {
                _c0[i] = _nodes[i].Contact0;
                _c1[i] = _nodes[i].Contact1;
            }

            // 5–8. The step on these models, and the check. A rise the clip cannot take back is solved again from the
            //      start on majorizing models (every term above its true energy, so none can be gained): over-stiff across
            //      the line for that one step (labelled numerical damping). Only what still rises, or is not finite, is a
            //      TetherWedge.
            CableGuard verdict = Run(dt, e0, startMagnitude, false, out double e1, out double excess, out double deficit, out double removable, out int bad);
            if (verdict == CableGuard.Wedge)
            {
                Restore(n);
                for (int j = 0; j + 1 < n; j++)
                    if (_segLen[j] > 0)
                    {
                        _segKind[j] = 3;
                        _segDir[j] = (_x0[j + 1] - _x0[j]) * (1 / _segLen[j]);
                    }
                for (int i = 0; i < n; i++)
                {
                    ref CableNode a = ref _nodes[i];
                    Speculate(a.Position, ref a.Contact0);
                    Speculate(a.Position, ref a.Contact1);
                }
                SafeSteps++;
                verdict = Run(dt, e0, startMagnitude, true, out e1, out excess, out deficit, out removable, out bad);
            }
            LastGuard = verdict;
            if (verdict == CableGuard.Clipped)
            {
                TakeBack(excess, removable);
                Clips++;
                ClippedEnergy += excess;
                e1 = Energy(out _);
            }
            else if (verdict == CableGuard.Wedge)
            {
                Wedges++;
                LastWedge = new CableWedge
                {
                    Step = Steps, Node = bad, NonFinite = bad >= 0, Excess = excess, Deficit = deficit, Removable = removable,
                };
                Restore(n);
                for (int i = 0; i < n; i++)
                {
                    _nodes[i].Velocity = default;
                    _nodes[i].Contact0.Engaged = false;
                    _nodes[i].Contact1.Engaged = false;
                }
                e1 = Energy(out _);
            }

            // 9. Insertion and merging by state, from the fixed end.
            if (_h0 > 0 && Remesh()) e1 = Energy(out _);
            EnergyAtEnd = e1;
        }

        // The step from its start on the chosen models: solve (with the active-set correction unless safe), positions,
        // the budget's work, the models' deficit, the contacts' forces and holds, the capstan, then the check.
        CableGuard Run(double dt, double e0, double startMagnitude, bool safe, out double e1, out double excess, out double deficit,
            out double removable, out int bad)
        {
            int n = Count;
            for (int pass = 0; ; pass++)
            {
                Assemble(dt);
                Solve();
                for (int i = 0; i < n; i++) _nodes[i].Position = _x0[i] + _nodes[i].Velocity * dt;
                if (safe || pass == MaxCorrections || !Correct()) break;
                Corrections++;
                for (int i = 0; i < n; i++)
                {
                    _nodes[i].Position = _x0[i];
                    _nodes[i].Velocity = _v0[i];
                }
            }

            double work = 0;
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                work += Double3.Dot(a.Force, a.Position - _x0[i]) + dt * Double3.Dot(a.Velocity, a.Drag * a.Air);
                if (a.AnchorDamping > 0) work += dt * a.AnchorDamping * Double3.Dot(a.Velocity, a.AnchorVelocity);
                work += DamperWork(a.Velocity, dt, ref a.Contact0) + DamperWork(a.Velocity, dt, ref a.Contact1);
            }
            deficit = 0;
            for (int j = 0; j + 1 < n; j++) deficit += SegmentDeficit(j);
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                deficit += ContactDeficit(a.Position, ref a.Contact0) + ContactDeficit(a.Position, ref a.Contact1);
            }

            // The contacts' forces and the holds' Coulomb slip; the capstan's slides from the fixed end. Both only lose
            // energy.
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                SettleContact(a.Position, ref a.Contact0);
                SettleContact(a.Position, ref a.Contact1);
            }
            CapstanRuns();

            e1 = Energy(out double endMagnitude);
            double budget = e0 + work;
            excess = e1 - budget;
            double tolerance = EnergyTolerance * (startMagnitude > endMagnitude ? startMagnitude : endMagnitude);
            bad = FirstNonFinite();
            removable = bad < 0 ? RelativeKinetic(out _) : 0;
            Budget = budget;
            Deficit = deficit;
            return bad >= 0 ? CableGuard.Wedge : Judge(excess, deficit, removable, tolerance);
        }

        // The step's start back: positions, velocities, material, masses and the contacts as chosen.
        void Restore(int n)
        {
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                a.Position = _x0[i];
                a.Velocity = _v0[i];
                a.Material = _s0[i];
                a.Contact0 = _c0[i];
                a.Contact1 = _c1[i];
            }
            for (int i = 0; i < n; i++) _nodes[i].Mass = MassOf(i);
        }

        static void Speculate(Double3 x, ref CableContact c)
        {
            if (!c.Active || c.Mode != 0) return;
            c.Mode = 2;
            c.Rest = Double3.Dot(c.Normal, x - c.Point);
            c.Engaged = true;
            c.Hold = x;
        }

        // The system's blocks and right-hand side at the step's start (positions and velocities x₀, v₀).
        void Assemble(double dt)
        {
            int n = Count;
            double dt2 = dt * dt;
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                Sym3 diag = Iso(a.Mass) + a.Drag * dt;
                Double3 f = Gravity * a.Mass + a.Force + a.Drag * a.Air;
                if (a.AnchorStiffness > 0 || a.AnchorDamping > 0)
                {
                    diag = diag + Iso(dt * a.AnchorDamping + dt2 * a.AnchorStiffness);
                    f = f - (a.Position - a.AnchorTarget) * a.AnchorStiffness + a.AnchorVelocity * a.AnchorDamping;
                }
                AddContact(a.Position, dt, ref a.Contact0, ref diag, ref f);
                AddContact(a.Position, dt, ref a.Contact1, ref diag, ref f);
                _diag[i] = diag;
                _rhs[i] = a.Velocity * a.Mass + f * dt;
            }
            for (int j = 0; j + 1 < n; j++)
            {
                byte kind = _segKind[j];
                if (kind == 0)
                {
                    _off[j] = default;
                    continue;
                }
                Double3 d = _segDir[j];
                double along = _segStiff[j], across = 0;
                if (kind == 1) across = _segTension[j] / _segLen[j];
                else if (kind == 3) across = along; // the majorizer ½k|Δ|² − k·ℓ·d·Δ: isotropic, about the same force
                along -= across;
                if (_segTension[j] > 0)
                {
                    Double3 pull = d * (dt * _segTension[j]);
                    _rhs[j] = _rhs[j] + pull;
                    _rhs[j + 1] = _rhs[j + 1] - pull;
                }
                Sym3 k = Outer(d, along * dt2) + Iso(across * dt2);
                _diag[j] = _diag[j] + k;
                _diag[j + 1] = _diag[j + 1] + k;
                _off[j] = k * -1.0;
            }
        }

        // Thomas: D₀ = A₀, Dᵢ = Aᵢ − Bᵢ₋₁·Dᵢ₋₁⁻¹·Bᵢ₋₁, then back; the new velocities into the nodes.
        void Solve()
        {
            int n = Count;
            _invD[0] = _diag[0].Inverse();
            _y[0] = _rhs[0];
            for (int i = 1; i < n; i++)
            {
                Sym3 b = _off[i - 1];
                Sym3 p = _invD[i - 1];
                _y[i] = _rhs[i] - b * (p * _y[i - 1]);
                _invD[i] = Minus(_diag[i], Sandwich(b, p)).Inverse();
            }
            Double3 next = _invD[n - 1] * _y[n - 1];
            _nodes[n - 1].Velocity = next;
            for (int i = n - 2; i >= 0; i--)
            {
                next = _invD[i] * (_y[i] - _off[i] * next);
                _nodes[i].Velocity = next;
            }
        }

        // After a pass: every slack segment the prediction left out that ends taut, and every member it left out that the
        // node ends inside, is taken speculative from the start (above the true energy, 0 there): true if any was.
        bool Correct()
        {
            bool missed = false;
            int n = Count;
            for (int j = 0; j + 1 < n; j++)
            {
                if (_segKind[j] != 0 || !(_segLen[j] > 0)) continue;
                double rest = _nodes[j + 1].Material - _nodes[j].Material;
                if (!((_nodes[j + 1].Position - _nodes[j].Position).Length() > rest)) continue;
                _segKind[j] = 2;
                _segDir[j] = (_x0[j + 1] - _x0[j]) * (1 / _segLen[j]);
                missed = true;
            }
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                missed |= Missed(a.Position, _x0[i], ref a.Contact0);
                missed |= Missed(a.Position, _x0[i], ref a.Contact1);
            }
            return missed;
        }

        static bool Missed(Double3 x, Double3 start, ref CableContact c)
        {
            if (!c.Active || c.Mode != 0 || !(Double3.Dot(c.Normal, x - c.Point) < 0)) return false;
            c.Mode = 2;
            c.Rest = Double3.Dot(c.Normal, start - c.Point);
            c.Engaged = true;
            c.Hold = start;
            return true;
        }

        // ---- the contacts ----

        // The force of a member the node presses into at the start (spring and damper), for the prediction only.
        static Double3 PressingForce(Double3 x, Double3 v, ref CableContact c)
        {
            if (!c.Active) return default;
            double g = Double3.Dot(c.Normal, x - c.Point);
            if (!(g < 0)) return default;
            double closing = Double3.Dot(c.Normal, v - c.Velocity);
            return c.Normal * (-c.Stiffness * g - c.Damping * closing);
        }

        static void ChooseContact(Double3 x, Double3 predicted, double dt, ref CableContact c)
        {
            if (!c.Active)
            {
                c.Mode = 0;
                c.Engaged = false;
                c.NormalForce = 0;
                return;
            }
            double g = Double3.Dot(c.Normal, x - c.Point);
            if (g <= 0)
            {
                c.Mode = 1;
                c.Rest = 0;
            }
            else if (Double3.Dot(c.Normal, predicted - c.Point) < 0)
            {
                c.Mode = 2;
                c.Rest = g;
            }
            else c.Mode = 0;
            if (c.Mode == 0) c.Engaged = false;
            else if (!c.Engaged)
            {
                c.Engaged = true;
                c.Hold = x;
            }
            else c.Hold = c.Hold + c.Velocity * dt;
        }

        // The contact's model on the diagonal and in the force: the normal spring from its rest gap (the surface when
        // pressing, the gap at the start when speculative) and its damper, the hold's tangential spring.
        static void AddContact(Double3 x, double dt, ref CableContact c, ref Sym3 diag, ref Double3 f)
        {
            if (c.Mode == 0) return;
            Double3 nrm = c.Normal;
            double g = Double3.Dot(nrm, x - c.Point);
            diag = diag + Outer(nrm, dt * c.Damping + dt * dt * (c.Stiffness - c.HoldStiffness)) + Iso(dt * dt * c.HoldStiffness);
            f = f + nrm * (-c.Stiffness * (g - c.Rest) + c.Damping * Double3.Dot(nrm, c.Velocity));
            if (c.HoldStiffness > 0) f = f - Tangential(x - c.Hold, nrm) * c.HoldStiffness;
        }

        static double DamperWork(Double3 v, double dt, ref CableContact c)
        {
            if (c.Mode == 0 || !(c.Damping > 0)) return 0;
            return dt * c.Damping * Double3.Dot(c.Normal, v) * Double3.Dot(c.Normal, c.Velocity);
        }

        // The true normal energy at the end above the model's: only a touch the prediction missed (the pressing model
        // is the full spring from the surface, the speculative one from a gap at or above it, both at or above it).
        static double ContactDeficit(Double3 x, ref CableContact c)
        {
            if (!c.Active || c.Mode != 0) return 0;
            double g = Double3.Dot(c.Normal, x - c.Point);
            return g < 0 ? 0.5 * c.Stiffness * g * g : 0;
        }

        static void SettleContact(Double3 x, ref CableContact c)
        {
            if (c.Mode == 0)
            {
                c.NormalForce = 0;
                return;
            }
            double g = Double3.Dot(c.Normal, x - c.Point);
            double normal = -c.Stiffness * (g - c.Rest);
            c.NormalForce = normal > 0 ? normal : 0;
            if (!(c.HoldStiffness > 0) || c.Capstan) return; // a capstan's hold is the member's point; line slides through it
            Double3 t = Tangential(x - c.Hold, c.Normal);
            double held = c.HoldStiffness * t.Length(), limit = c.StaticFriction * c.NormalForce;
            if (held > limit && held > 0)
            {
                // Coulomb: the hold slides toward the node until it pulls with μk·N (its spring only loses energy).
                double keep = c.KineticFriction * c.NormalForce / held;
                c.Hold = c.Hold + t * (1 - keep);
            }
        }

        // ---- the segments ----

        // The true elastic energy at the end above the model's (taut: the tangent's quadratic about the start; speculative:
        // the spring along d from the start length; none: 0).
        double SegmentDeficit(int j)
        {
            Double3 d = _nodes[j + 1].Position - _nodes[j].Position;
            double rest = _nodes[j + 1].Material - _nodes[j].Material, k = _segStiff[j];
            double stretch = d.Length() - rest;
            double truth = stretch > 0 ? 0.5 * k * stretch * stretch : 0;
            if (truth == 0) return 0;
            double model;
            byte kind = _segKind[j];
            if (kind == 1)
            {
                Double3 dir = _segDir[j];
                Double3 delta = d - dir * _segLen[j];
                double along = Double3.Dot(dir, delta), across = _segTension[j] / _segLen[j];
                model = _segEnergy[j] + _segTension[j] * along
                    + 0.5 * (k * along * along + across * (Double3.Dot(delta, delta) - along * along));
            }
            else if (kind == 2)
            {
                double s = Double3.Dot(_segDir[j], d) - _segLen[j];
                model = 0.5 * k * s * s;
            }
            else if (kind == 3)
            {
                Double3 delta = d - _segDir[j] * _segLen[j];
                model = _segEnergy[j] + _segTension[j] * Double3.Dot(_segDir[j], delta) + 0.5 * k * Double3.Dot(delta, delta);
            }
            else model = 0;
            return truth > model ? truth - model : 0;
        }

        // ---- the capstan (design 2: Euler–Eytelwein) ----

        // Each run of neighbouring nodes on capstan members (one node for a twig or an edge, several round a post) is one
        // wrap: it holds while T_hi/T_lo ≤ Π e^(μs·θ) over its nodes, θ each node's turn; once past it, material slides
        // through the whole run until every segment from the low side carries e^(μk·θ) times the one before (the
        // capstan's profile), the run's material kept. One node is the plain capstan; several compose as one wrap.
        void CapstanRuns()
        {
            int n = Count, i = 1;
            while (i + 1 < n)
            {
                if (!Friction(i, out _, out _))
                {
                    i++;
                    continue;
                }
                int first = i;
                while (i + 1 < n && Friction(i, out _, out _)) i++;
                SlideRun(first, i - 1);
            }
        }

        // Node i's capstan friction: the μs and μk of its pressing capstan members, weighted by their normal forces.
        bool Friction(int i, out double muS, out double muK)
        {
            ref CableNode c = ref _nodes[i];
            double weight = 0;
            muS = 0;
            muK = 0;
            Accumulate(ref c.Contact0, ref muS, ref muK, ref weight);
            Accumulate(ref c.Contact1, ref muS, ref muK, ref weight);
            if (!(weight > 0)) return false;
            muS /= weight;
            muK /= weight;
            return true;
        }

        void SlideRun(int first, int last)
        {
            // The run's segments are first − 1 … last: their lengths and tensions now, the kinetic exponent μk·θ
            // accumulated from the in side, and the profile's factor from the low side.
            double holdIn = 0, slideIn = 0; // Σ μ·θ, static and kinetic
            for (int j = first - 1; j <= last; j++)
            {
                double len = (_nodes[j + 1].Position - _nodes[j].Position).Length(), rest = _nodes[j + 1].Material - _nodes[j].Material;
                _capLen[j] = len;
                _capT[j] = len > rest ? AxialStiffness * (len / rest - 1) : 0;
            }
            for (int i = first; i <= last; i++)
            {
                Double3 da = _nodes[i].Position - _nodes[i - 1].Position, db = _nodes[i + 1].Position - _nodes[i].Position;
                double turn = DetMath.Atan2(Double3.Cross(da, db).Length(), Double3.Dot(da, db));
                Friction(i, out double muS, out double muK);
                holdIn += muS * turn;
                slideIn += muK * turn;
                _capExp[i] = slideIn; // the kinetic exponent reached past node i, on the in side's count
            }
            double tIn = _capT[first - 1], tOut = _capT[last];
            bool outward = tOut >= tIn; // material flows toward the high side
            double hi = outward ? tOut : tIn, lo = outward ? tIn : tOut;
            if (!(hi > 0) || hi <= DetMath.Exp(holdIn) * lo) return;

            // The profile from the low side: segment j carries t·f_j, f_j = e^(μk·θ) accumulated from the low end.
            _capExp[first - 1] = 0;
            for (int j = first - 1; j <= last; j++)
            {
                double fromIn = DetMath.Exp(_capExp[j]);
                _capFactor[j] = outward ? fromIn : DetMath.Exp(slideIn) / fromIn;
            }
            double material = _nodes[last + 1].Material - _nodes[first - 1].Material, slack = -material;
            for (int j = first - 1; j <= last; j++) slack += _capLen[j];

            // Σ L_j/(1 + f_j·t/EA) = the run's material: decreasing and convex in t, so Newton from t = 0 rises onto it.
            double t = 0;
            if (slack > 0)
            {
                for (int it = 0; it < 40; it++)
                {
                    double f = -material, df = 0;
                    for (int j = first - 1; j <= last; j++)
                    {
                        double q = 1 / (1 + _capFactor[j] * t / AxialStiffness);
                        f += _capLen[j] * q;
                        df -= _capLen[j] * _capFactor[j] / AxialStiffness * q * q;
                    }
                    double step = f / df;
                    t -= step;
                    if (!(step < -1e-15 * t)) break;
                }
            }

            // The new material coordinates (the low side takes what slack is left), checked and costed before applying.
            int lowSeg = outward ? first - 1 : last;
            double position = _nodes[first - 1].Material;
            for (int j = first - 1; j < last; j++)
            {
                double rest = _capLen[j] / (1 + _capFactor[j] * t / AxialStiffness);
                if (t == 0 && j == lowSeg) rest = _capLen[j] - slack;
                position += rest;
                _capS[j + 1] = position; // the new s of node j + 1
            }
            for (int i = first; i <= last + 1; i++)
            {
                double before = i == first ? _nodes[i - 1].Material : _capS[i - 1];
                double after = i == last + 1 ? _nodes[i].Material : _capS[i];
                if (!(after - before > MinRest)) return;
            }

            double elastic = 0;
            for (int j = first - 1; j <= last; j++)
            {
                double restNow = _nodes[j + 1].Material - _nodes[j].Material;
                double restNew = (j + 1 <= last ? _capS[j + 1] : _nodes[j + 1].Material) - (j >= first ? _capS[j] : _nodes[j].Material);
                elastic += Spring(_capLen[j], restNew) - Spring(_capLen[j], restNow);
            }
            // The masses move with the material: what nodes lose goes to a pool at its mean velocity, from which the
            // gaining nodes take it, merging (momentum kept, kinetic energy only falls).
            double pooled = 0;
            Double3 momentum = default;
            for (int i = first - 1; i <= last + 1; i++)
            {
                double dm = NewMass(i, first, last) - _nodes[i].Mass;
                if (dm < 0)
                {
                    pooled -= dm;
                    momentum = momentum - _nodes[i].Velocity * dm;
                }
            }
            Double3 pool = pooled > 0 ? momentum * (1 / pooled) : default;
            double kinetic = 0, potential = 0;
            for (int i = first - 1; i <= last + 1; i++)
            {
                ref CableNode a = ref _nodes[i];
                double dm = NewMass(i, first, last) - a.Mass;
                potential -= dm * Double3.Dot(Gravity, a.Position);
                if (dm > 0)
                {
                    Double3 v = (a.Velocity * a.Mass + pool * dm) * (1 / (a.Mass + dm));
                    kinetic += 0.5 * (a.Mass + dm) * Double3.Dot(v, v) - 0.5 * a.Mass * Double3.Dot(a.Velocity, a.Velocity)
                        - 0.5 * dm * Double3.Dot(pool, pool);
                }
            }
            if (pooled > 0) kinetic += 0.5 * pooled * Double3.Dot(pool, pool) - PoolKinetic(first, last);
            if (elastic + kinetic + potential > 0) return;

            for (int i = first - 1; i <= last + 1; i++)
            {
                ref CableNode a = ref _nodes[i];
                double dm = NewMass(i, first, last) - a.Mass;
                if (dm > 0) a.Velocity = (a.Velocity * a.Mass + pool * dm) * (1 / (a.Mass + dm));
            }
            for (int i = first; i <= last; i++) _nodes[i].Material = _capS[i];
            for (int i = first - 1; i <= last + 1; i++) _nodes[i].Mass = MassOf(i);
            Slides++;
        }

        // Node i's mass with the run's new material coordinates (in _capS for first … last).
        double NewMass(int i, int first, int last)
        {
            double s = i >= first && i <= last ? _capS[i] : _nodes[i].Material;
            double before = 0, after = 0;
            if (i > 0) before = s - (i - 1 >= first && i - 1 <= last ? _capS[i - 1] : _nodes[i - 1].Material);
            if (i + 1 < Count) after = (i + 1 >= first && i + 1 <= last ? _capS[i + 1] : _nodes[i + 1].Material) - s;
            return 0.5 * MassPerMetre * (before + after);
        }

        // The kinetic energy the losing nodes' material carried away, J.
        double PoolKinetic(int first, int last)
        {
            double e = 0;
            for (int i = first - 1; i <= last + 1; i++)
            {
                double dm = NewMass(i, first, last) - _nodes[i].Mass;
                if (dm < 0) e -= 0.5 * dm * Double3.Dot(_nodes[i].Velocity, _nodes[i].Velocity);
            }
            return e;
        }

        static void Accumulate(ref CableContact c, ref double muS, ref double muK, ref double weight)
        {
            if (c.Mode == 0 || !c.Capstan || !(c.NormalForce > 0)) return;
            muS += c.StaticFriction * c.NormalForce;
            muK += c.KineticFriction * c.NormalForce;
            weight += c.NormalForce;
        }

        double Spring(double length, double rest)
        {
            double s = length - rest;
            return s > 0 ? 0.5 * (AxialStiffness / rest) * s * s : 0;
        }

        // ---- the check's helpers ----

        int FirstNonFinite()
        {
            for (int i = 0; i < Count; i++)
            {
                ref CableNode a = ref _nodes[i];
                if (!Finite(a.Position) || !Finite(a.Velocity) || double.IsNaN(a.Material) || double.IsInfinity(a.Material)) return i;
            }
            return -1;
        }

        static bool Finite(Double3 v) => !double.IsNaN(v.X + v.Y + v.Z) && !double.IsInfinity(v.X + v.Y + v.Z);

        // The kinetic energy about the line's mean velocity, J: what a clip may take without touching its momentum.
        double RelativeKinetic(out Double3 mean)
        {
            double mass = 0;
            Double3 momentum = default;
            for (int i = 0; i < Count; i++)
            {
                mass += _nodes[i].Mass;
                momentum = momentum + _nodes[i].Velocity * _nodes[i].Mass;
            }
            mean = momentum * (1 / mass);
            double e = 0;
            for (int i = 0; i < Count; i++)
            {
                Double3 u = _nodes[i].Velocity - mean;
                e += 0.5 * _nodes[i].Mass * Double3.Dot(u, u);
            }
            return e;
        }

        void TakeBack(double excess, double removable)
        {
            RelativeKinetic(out Double3 mean);
            double scale = DetMath.Sqrt((removable - excess) / removable);
            for (int i = 0; i < Count; i++) _nodes[i].Velocity = mean + (_nodes[i].Velocity - mean) * scale;
        }

        // ---- insertion and merging (design 2) ----

        double Target(double s)
        {
            double away = s - _focus;
            if (away < 0) away = -away;
            return away <= _near ? _h0 : _h0 + (_growth - 1) * (away - _near);
        }

        bool Removable(int i)
        {
            ref CableNode a = ref _nodes[i];
            return !(a.AnchorStiffness > 0) && !(a.AnchorDamping > 0) && !a.Contact0.Active && !a.Contact1.Active;
        }

        bool Remesh()
        {
            bool changed = false;
            int j = 0;
            while (j + 1 < Count)
            {
                double sa = _nodes[j].Material, sb = _nodes[j + 1].Material, rest = sb - sa;
                if (rest > Target(0.5 * (sa + sb)))
                {
                    if (Count == Capacity)
                    {
                        SplitsRefused++;
                        j++;
                        continue;
                    }
                    Split(j);
                    changed = true;
                    j += 2;
                    continue;
                }
                if (j + 2 < Count && Removable(j + 1))
                {
                    double sc = _nodes[j + 2].Material;
                    if (sc - sa <= Target(0.5 * (sa + sc)) && TryMerge(j + 1))
                    {
                        changed = true;
                        j++;
                        continue;
                    }
                }
                j++;
            }
            return changed;
        }

        // A segment split at its midpoint: position, velocity and material halfway, so momentum is kept, the springs'
        // energy is unchanged and the kinetic falls by μℓ₀/16·|v_a − v_b|².
        void Split(int j)
        {
            Array.Copy(_nodes, j + 1, _nodes, j + 2, Count - j - 1);
            ref CableNode a = ref _nodes[j];
            ref CableNode b = ref _nodes[j + 2];
            _nodes[j + 1] = new CableNode
            {
                Position = (a.Position + b.Position) * 0.5,
                Velocity = (a.Velocity + b.Velocity) * 0.5,
                Material = 0.5 * (a.Material + b.Material),
            };
            Count++;
            a.Mass = MassOf(j);
            _nodes[j + 1].Mass = MassOf(j + 1);
            b.Mass = MassOf(j + 2);
            Splits++;
        }

        // Node m removed: its mass goes to its neighbours by the rest lengths (μ·ℓ_b/2 to the one before, μ·ℓ_a/2 to the one
        // after), merging inelastically (momentum kept, kinetic energy can only fall). Done only if the whole energy
        // (springs, kinetic, gravity) does not rise.
        bool TryMerge(int m)
        {
            ref CableNode a = ref _nodes[m - 1];
            ref CableNode c = ref _nodes[m];
            ref CableNode b = ref _nodes[m + 1];
            double ra = c.Material - a.Material, rb = b.Material - c.Material;
            double before = Spring((c.Position - a.Position).Length(), ra) + Spring((b.Position - c.Position).Length(), rb);
            double after = Spring((b.Position - a.Position).Length(), ra + rb);
            double toA = 0.5 * MassPerMetre * rb, toB = 0.5 * MassPerMetre * ra;
            Double3 va = (a.Velocity * a.Mass + c.Velocity * toA) * (1 / (a.Mass + toA));
            Double3 vb = (b.Velocity * b.Mass + c.Velocity * toB) * (1 / (b.Mass + toB));
            double kinetic = 0.5 * (a.Mass + toA) * Double3.Dot(va, va) + 0.5 * (b.Mass + toB) * Double3.Dot(vb, vb)
                - 0.5 * a.Mass * Double3.Dot(a.Velocity, a.Velocity) - 0.5 * b.Mass * Double3.Dot(b.Velocity, b.Velocity)
                - 0.5 * c.Mass * Double3.Dot(c.Velocity, c.Velocity);
            double potential = -Double3.Dot(Gravity, a.Position * toA + b.Position * toB - c.Position * c.Mass);
            if (after - before + kinetic + potential > 0) return false;
            a.Velocity = va;
            b.Velocity = vb;
            Array.Copy(_nodes, m + 1, _nodes, m, Count - m - 1);
            Count--;
            _nodes[Count] = default;
            _nodes[m - 1].Mass = MassOf(m - 1);
            _nodes[m].Mass = MassOf(m);
            Merges++;
            return true;
        }

        double MassOf(int i)
        {
            double before = i > 0 ? _nodes[i].Material - _nodes[i - 1].Material : 0;
            double after = i + 1 < Count ? _nodes[i + 1].Material - _nodes[i].Material : 0;
            return 0.5 * MassPerMetre * (before + after);
        }

        // ---- 3 × 3 helpers (+ − × ÷ only) ----

        static Sym3 Iso(double s) => new Sym3(s, s, s, 0, 0, 0);

        static Sym3 Outer(Double3 d, double s) =>
            new Sym3(s * d.X * d.X, s * d.Y * d.Y, s * d.Z * d.Z, s * d.X * d.Y, s * d.X * d.Z, s * d.Y * d.Z);

        static Sym3 Minus(Sym3 a, Sym3 b) => new Sym3(a.XX - b.XX, a.YY - b.YY, a.ZZ - b.ZZ, a.XY - b.XY, a.XZ - b.XZ, a.YZ - b.YZ);

        // B·P·B for symmetric B and P: T = P·B column by column, then the six entries of B·T.
        static Sym3 Sandwich(Sym3 b, Sym3 p)
        {
            Double3 t0 = p * new Double3(b.XX, b.XY, b.XZ), t1 = p * new Double3(b.XY, b.YY, b.YZ), t2 = p * new Double3(b.XZ, b.YZ, b.ZZ);
            return new Sym3(
                b.XX * t0.X + b.XY * t0.Y + b.XZ * t0.Z,
                b.XY * t1.X + b.YY * t1.Y + b.YZ * t1.Z,
                b.XZ * t2.X + b.YZ * t2.Y + b.ZZ * t2.Z,
                b.XX * t1.X + b.XY * t1.Y + b.XZ * t1.Z,
                b.XX * t2.X + b.XY * t2.Y + b.XZ * t2.Z,
                b.XY * t2.X + b.YY * t2.Y + b.YZ * t2.Z);
        }
    }
}
