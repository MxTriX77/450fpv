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
        /// <summary>The caller's: a member re-found every step (the ground's plane under the node), so the node may be merged
        /// away with it on (its hold is lost with it, which only lowers the energy).</summary>
        public bool Remeshable;
        /// <summary>The caller's, a capstan's: its point is held along the member as well (a barb's notch), so the hold never
        /// slips; without it a capstan's hold slips across the line by Coulomb's law (the line sliding along a twig, a wire,
        /// a skid), while along the line material slides through the node by the capstan.</summary>
        public bool Notch;
        /// <summary>The caller's, a capstan's on a moving member (a part of the drone): its hold along the line slips too, once
        /// the pull along the line passes what the wrap there holds, T_lo·(e^(μs·θ) − 1), down to T_lo·(e^(μk·θ) − 1) (θ the
        /// line's turn at the node, T_lo the lower tension beside it), so a member sliding along a straight line glides by it
        /// rather than dragging the node's line at its own speed, while a wrap holds as the capstan does.</summary>
        public bool Glide;
        /// <summary>The caller's, a gliding member's, for this step: the line slips by it, so its hold does not act along the
        /// line in the solve (only across it and along the normal): set when the wrap could not bring the node to the member's
        /// speed along the line within the step (FiberContacts' predictive Coulomb rule).</summary>
        public bool Free;
        /// <summary>The caller's: whether the node is predicted to reach this member within the step is judged by its own motion
        /// (x + v·dt against the member's end-of-step place), not the explicit prediction with the line's forces (which over a
        /// stiff taut segment reaches centimetres, and would hold a node off a member it never reaches), or by its gap being
        /// within KinematicBand (a node the line's own pull draws in against a member it rests by); a touch it still misses is
        /// taken by the correction pass, as any other.</summary>
        public bool Kinematic;
        /// <summary>The caller's: which member this is (its class and identity, opaque to the chain), and where the node first
        /// touched it, m (a barb's lock, a hook's attach point). They move with the node.</summary>
        public ulong Member;
        public byte MemberKind;
        public Double3 Origin;
        /// <summary>The caller's, opaque to the chain: the arris's radius the line bends over there, m; the force and the press
        /// at which the member lets go, N and m (0: never); the node's material coordinate when it was set this step (a capstan's
        /// slide shows as a change); whether it was set afresh this step, and whether line was sliding through it.</summary>
        public double Edge, Cap, Slide, Through;
        public bool Fresh, Sliding;
        /// <summary>The caller's, opaque to the chain: the member's surface class for the line's microbend loss (p7d-tether 1.8):
        /// rough (soil, bark, concrete, straw) or smooth.</summary>
        public bool Rough;
        /// <summary>The caller's, opaque to the chain: the member abrades the line's coating (p7d-tether design 21: a rough row or
        /// an arris or a record harder than the coating, Archard's severe band), else it wears it mildly.</summary>
        public bool Abrasive;
        /// <summary>The caller's, opaque to the chain: how far the member's touch has moved along the line from the node's
        /// material point since the slot was set, m (the line gliding past a member along its length: 1.8's wear).</summary>
        public double Rubbed;

        /// <summary>The chain's: the hold is set (the node pressed on the member in the last step).</summary>
        public bool Engaged;
        /// <summary>The chain's: where the node is held along the member, m (only its tangential part acts).</summary>
        public Double3 Hold;
        /// <summary>The chain's: the normal force at the step's end, N (above 0 pressing).</summary>
        public double NormalForce;
        /// <summary>The chain's (forensics, no force): where the node was as this touch began, the hold's start, m; a node that
        /// leaves the member and touches it again begins a new touch.</summary>
        public Double3 Touched;
        /// <summary>The chain's: the hold moved over the member in the last step (Coulomb's slip, a glide or a free slip, or
        /// the hold let go): the node slid on it rather than sticking (w3-farmland-ground 2.4: the trail's rest test).</summary>
        public bool Slid;

        // This step's normal model: 0 none, 1 pressing (the spring from the surface), 2 speculative (from the gap at
        // the step's start, the node being predicted to reach the surface within the step).
        internal byte Mode;
        internal double Rest;
        /// <summary>The caller's: the depth (m, at most 0) the node was found inside the member's surface when its slot took this
        /// plane, a fresh slot or a kept one whose plane moved under the node (a remesh's node on a chord, a record's closest
        /// segment switching): an overlap the node's own motion did not make, so the normal spring holds the node there rather
        /// than throwing it out (p7d-tether design 18's contacts: a 2.4 mm overlap thrown a resting node out at 6.9 m/s with
        /// 24 N, more than the line's tension and weight can press). It follows the node out as the line's own loads lift it.</summary>
        public double Sunk;
        // This step's, a gliding slot's: an earlier pass ended with its hold pulling while nothing pressed the node on the part
        // (N = 0, so Coulomb's bound μs·N is 0): the hold is out of the solve for the rest of the step (CableChain.Unstuck).
        internal bool Slips;

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
        /// <summary>The anchor's target follows a body (the drone's exit): AnchorTarget is where it is at the step's end, it
        /// moved there at AnchorVelocity over the step, so the energy check takes the spring at the step's start from where
        /// the target was and the target's motion as the work done on the line, F·V·dt (design 13).</summary>
        public bool AnchorFollows;
        /// <summary>Two members at most: a node touching two takes both on its diagonal (the wedge, design 6).</summary>
        public CableContact Contact0, Contact1;
        /// <summary>The caller's: how long the node has rested, s (the trail's freeze, design 3). It moves with the node; a
        /// node a split makes starts at 0.</summary>
        public double RestTime;
        /// <summary>The caller's: how long the node has stayed near StillAt, where its stillness began, on a support or not, s
        /// (w3-farmland-ground 2.4: a span the line holds still between two supports freezes with them).</summary>
        public double StillTime;
        public Double3 StillAt;
        /// <summary>The caller's clearance certificate of the segment from this node to the next (design 12): Cleared, and the
        /// core A–B of the segment a query found clear. It moves with the node, and a split's new node takes its segment's:
        /// both pieces lie on the certified core (the caller judges a segment by its ends' distance from that core, and the
        /// distance from a segment is convex, so a segment whose two ends lie near the core lies near it everywhere).</summary>
        public bool Cleared;
        public Double3 ClearA, ClearB;
        /// <summary>The caller's near list of the segment from this node (w3-farmland-ground 2.1): its slot in the caller's
        /// pool plus one, 0 for none. It moves with the node and a split's new node takes its segment's, as Cleared does; the
        /// caller judges a list by its segment's ends' distance from the list's own core.</summary>
        public int Near;
        /// <summary>The caller's ground certificate of the segment from this node to the next (w3-farmland-ground 2.5): set, the
        /// core A–B of the segment a test found lying no deeper than it allows under the ground, and how far either end may move
        /// from that core before the next test, m. It moves with the node and a split's new node takes its segment's, as Cleared
        /// does (the depth of a point under the ground moves no more than the point, so a segment whose two ends lie near the
        /// core lies near the core's depth everywhere).</summary>
        public bool GroundClear;
        public Double3 GroundA, GroundB;
        public double GroundSlack;
        /// <summary>The caller's: a node the ground holds the line over where the chord between its neighbours would pass under it
        /// (a crest, the ledge's lip; w3-farmland-ground 2.5). It is never merged away, and it counts against CrestCapacity, not
        /// the spacing law's room. A split's new node is not one unless SplitCrest puts it there.</summary>
        public bool Crest;
        /// <summary>The caller's: a point of the laid trail lifted back into the chain this step (w3-farmland-ground 2.4). It lies
        /// where it rested, on its support, though its slots come only with the next step's contacts.</summary>
        public bool Lifted;
        /// <summary>The chain's, the caller clears it: the node was put into a segment (a split: the spacing law's, a member's
        /// contact node, a crest) since, so where it lies is the remesh's choice on the segment's chord, not its own motion's
        /// (p7d-tether design 21: an overlap a member's fresh slot finds it in is kept, not thrown out).</summary>
        public bool Placed;
    }

    /// <summary>
    /// The spool at the chain's last node (p7d-tether design 3), set by the caller before each step: the last node is the
    /// exit, anchored to the drone, and line flows out through it one way. While it pays out, the last segment is the pack's
    /// resistance, not a spring: its tension is the moving line's capstan over the lip on the pack's resistance,
    /// T = μ·ṡ² + e^(μθ)·(P(ṡ) + m_b·s̈) (the belt's capstan with its centrifugal tension, published for flat belts; the peel
    /// P = G₀·irregularity·max(1, (ṡ/v_ref)^n), the momentum flux μ·ṡ², the balloon's inertia m_b·s̈), taken inside the
    /// implicit step: a constant pull e^(μθ)·P(0), a damper along the segment (the rate law's and the flux's secant at the last
    /// step's ṡ), and the balloon's mass e^(μθ)·m_b coupling the two nodes along it, with the geometric stiffness across. It
    /// holds while its tension stays under the static hold e^(μθ)·P(0).
    /// </summary>
    public struct CableSpool
    {
        public bool Active;
        /// <summary>No line flows: the coil is empty (its end is fixed to the core).</summary>
        public bool Locked;
        /// <summary>The known-wrong two-way spool (design 16): it takes line back in at the static hold tension.</summary>
        public bool TwoWay;
        /// <summary>The nozzle's factor this step, T outside over the pack's pull (FiberLine: FiberCuts.LipFactor, design 18; at
        /// least 1).</summary>
        public double Lip;
        /// <summary>N: the pack's static peel resistance at the section at the peel, G₀·irregularity.</summary>
        public double Peel;
        /// <summary>The peel's rate law, max(1, (ṡ/v_ref)^n): n, and v_ref in m/s.</summary>
        public double RateExponent, ReferenceRate;
        /// <summary>kg/m: the line's mass per metre as it leaves the pack (the momentum flux μ·ṡ²).</summary>
        public double FluxMass;
        /// <summary>kg: the balloon's line between the peel and the exit, μ·L_b.</summary>
        public double BalloonMass;
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

        /// <summary>A Kinematic contact this close to its node at the step's start is taken speculative whatever the node's
        /// motion, m (numerical: the line's pull draws a resting node a few millimetres in a step at most).</summary>
        public const double KinematicBand = 0.002;

        /// <summary>The segments' Newton solves a step may add, and the tension error that takes one, N (numerical; 1 mN is 2 % of
        /// the pack's least peel, 0.05 N, and 0.01 % of the proof load). A taut segment's model is its exact tangent at the
        /// linearisation's point (the step's start, then each solve's end), so a segment the solve rotates by δ reads at its end
        /// the stretch δ²/(2ℓ) the tangent never saw: EA·δ²/(2ℓ²), 4.7 N for 1 mm on 10 mm, against 0.03 N the solve applied
        /// (1.8's finding 2); and one modelled slack that ends taut (the correction passes spent) carries a tension no force in
        /// the solve made. While a segment's end tension and its model's differ by more than NewtonTolerance, the step is solved
        /// again from its start with the true energy's tangent at the last solve's end (Newton on the segments, the same dt:
        /// backward Euler's own solution, never a sub-step).</summary>
        public const int MaxNewton = 8; // numerical: a reverse's line whipped round the body's parts at 10 m/s takes 5–8
        public const double NewtonTolerance = 1e-3;

        /// <summary>The nodes the chain holds at most: the spacing law's room and the crests' (w3-farmland-ground 2.5).</summary>
        public readonly int Capacity;
        /// <summary>The spacing law's room: its splits, a member's contact node and a trail point lifted back stop at
        /// SpacingCapacity nodes besides the crests; and the crests' own.</summary>
        public readonly int SpacingCapacity, CrestCapacity;
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

        /// <summary>Merges whose coarser lumping raised the energy (a sagging line's mass lumped higher), the rise taken back
        /// from the line's relative motion, and that energy, J (labelled numerical damping; only a spool's chain).</summary>
        public long MergeClips { get; private set; }
        public double MergeClipped { get; private set; }

        /// <summary>Material slid through capstan contacts, splits and merges, and splits refused for want of room.</summary>
        public long Slides { get; private set; }
        public long Splits { get; private set; }
        public long Merges { get; private set; }
        public long SplitsRefused { get; private set; }
        /// <summary>The extra solves the active-set correction took, and the steps solved again on majorizing models.</summary>
        public long Corrections { get; private set; }
        public long SafeSteps { get; private set; }

        /// <summary>The known-wrong 0-force hold (model 59's, FlightSetup.TetherZeroForceHold): a gliding slot holds the line in the
        /// solve whatever its normal force: its hold at full stiffness, Coulomb's bound applied only after the solve, and its
        /// speculative spring pulling the node toward the part; and the safe pass keeps the spool's paying model on a segment that
        /// pays nothing and takes every free slot speculative from the start (SafeMissed). Model 59's chain bit for bit: a
        /// bench's model and the audit, never a pilot's flight.</summary>
        public bool ZeroForceHold;

        /// <summary>The known-wrong linear step (model 74's chain, FlightSetup.TetherLinearStep): the step solved on its start's
        /// linearisation alone, every segment's model its tangent at the step's start; so a segment the solve rotates ends stretched
        /// by δ²/(2ℓ) and one modelled slack ends taut once the corrections are spent, each read as tension by the cuts (1.8's
        /// finding 2). Model 74's chain bit for bit: a bench's model and the audit, never a pilot's flight. ZeroForceHold (model
        /// 59's chain) steps linearly too.</summary>
        public bool LinearStep;

        /// <summary>The known-wrong of FlightSetup.TetherBeforeDesign18: the capstans' slides may leave the exit's segment past
        /// the spool's law (no SpoolAfterSlide), as to model 88.</summary>
        public bool KeepSlideStretch;

        /// <summary>The known-wrong of FlightSetup.TetherBeforeDesign21: a capstan run slides only when its two outer tensions pass
        /// the wrap's bound, so a segment inside the run may stay stretched while both its ends are slack (its nodes' holds along
        /// the line carry it, unbounded), as to model 100.</summary>
        public bool RunEndsOnly;

        // The rounds of the pack's draw into a run at the exit and the run's slide after it in a step (numerical).
        const int SlideRounds = 8;

        /// <summary>The Newton solves taken (Relinearise, at most MaxNewton a step), and the steps that ended with
        /// a segment's tension still past NewtonTolerance from its model (the budget spent): the linearisation's residue left in the
        /// line.</summary>
        public long NewtonSolves { get; private set; }
        public long NewtonUnconverged { get; private set; }

        /// <summary>The spool at the last node (the caller's, before each step); inactive by default.</summary>
        public CableSpool Spool;
        /// <summary>The balloon's speed ṡ, m/s: line leaving the exit (state; negative only while a two-way spool takes line
        /// back).</summary>
        public double SpoolSpeed;
        /// <summary>The last step's: the line paid out, m (negative: taken back by a two-way spool), the pack's resistance
        /// T_r = μ·ṡ² + P(ṡ) + m_b·s̈ (or its static P(0) while it holds), N, the exit segment's tension at the step's end,
        /// N, and whether line flowed.</summary>
        public double SpoolPaid { get; private set; }
        public double SpoolPackTension { get; private set; }
        public double SpoolExitTension { get; private set; }
        public bool SpoolPaying { get; private set; }

        readonly CableNode[] _nodes;

        // Spacing (design 2, 3): the target rest length h0 within NearLength of the focus, growing by Growth a node beyond.
        double _h0, _near, _growth, _focus;

        // This step's segment models: 0 none, 1 taut (the exact tangent), 2 speculative (along d from its start length).
        readonly byte[] _segKind;
        readonly Double3[] _segDir;
        readonly double[] _segLen, _segTension, _segStiff, _segEnergy;
        // A segment's linearisation moved off the step's start by a Newton solve: the point's offset from the start's segment
        // vector, and the start's model kept for the safe solve.
        readonly bool[] _segMoved;
        readonly Double3[] _segOffset, _segDir0;
        readonly byte[] _segKind0;
        readonly double[] _segLen0, _segTension0, _segEnergy0;

        readonly Sym3[] _diag, _off, _invD;
        readonly Double3[] _rhs, _y, _pred;
        readonly Double3[] _x0, _v0;
        readonly double[] _s0;
        readonly CableContact[] _c0, _c1;
        readonly double[] _capLen, _capT, _capExp, _capFactor, _capS, _capHold;

        // This step's spool model (kind 4 on the last segment): the constant pull, the damper, the balloon's mass, the spool's
        // speed at the start, the static peel's work and the added line's energy.
        double _spoolForce, _spoolDamp, _spoolMass, _spoolSpeed0, _spoolPeelWork;

        // The lowest node whose blocks the last correction changed (Run's next pass assembles and sweeps from it).
        int _dirty;

        /// <summary>A chain of at most capacity nodes by the spacing law and crests more over the ground's crests (0: none).</summary>
        public CableChain(int capacity, double massPerMetre, double axialStiffness, int crests = 0)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (crests < 0) throw new ArgumentOutOfRangeException(nameof(crests));
            if (!(massPerMetre > 0) || !(axialStiffness > 0)) throw new ArgumentOutOfRangeException(nameof(massPerMetre));
            SpacingCapacity = capacity;
            CrestCapacity = crests;
            capacity += crests;
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
            _segMoved = new bool[capacity];
            _segOffset = new Double3[capacity];
            _segDir0 = new Double3[capacity];
            _segKind0 = new byte[capacity];
            _segLen0 = new double[capacity];
            _segTension0 = new double[capacity];
            _segEnergy0 = new double[capacity];
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
            _capHold = new double[capacity];
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
        public double Energy(out double magnitude) => Energy(out magnitude, 0);

        // At a step's start (dt > 0) a following anchor's spring is taken from where its target was, AnchorTarget − V·dt.
        double Energy(out double magnitude, double startDt)
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
                    if (a.AnchorFollows) e = e + a.AnchorVelocity * startDt;
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
            // The spool's balloon moves with the line it pays out (CableSpool): its mass seen through the lip.
            if (Spool.Active && n >= 2) kinetic += 0.5 * (Spool.Lip * Spool.BalloonMass) * SpoolSpeed * SpoolSpeed;
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
            _spoolSpeed0 = SpoolSpeed;
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
                Double3 along = a.Contact0.Free || a.Contact1.Free ? LineTangent(i) : default;
                Double3 drift = a.Position + a.Velocity * dt;
                ChooseContact(a.Position, a.Contact0.Kinematic ? Banded(a.Position, drift, ref a.Contact0) : _pred[i], dt, along, ref a.Contact0);
                ChooseContact(a.Position, a.Contact1.Kinematic ? Banded(a.Position, drift, ref a.Contact1) : _pred[i], dt, along, ref a.Contact1);
            }

            // 3. The true energy at the start, with this step's targets.
            double startMagnitude;
            double e0 = Energy(out startMagnitude, dt);
            EnergyAtStart = e0;

            // 4. The segments' models: a taut one's exact tangent; a slack one predicted to tighten, a spring along its
            //    direction from its length at the start (above the true energy, which is 0 until the rest length).
            for (int j = 0; j + 1 < n; j++)
            {
                Double3 d = _nodes[j + 1].Position - _nodes[j].Position;
                double len = d.Length(), rest = _nodes[j + 1].Material - _nodes[j].Material, k = AxialStiffness / rest;
                _segStiff[j] = k;
                _segLen[j] = len;
                _segMoved[j] = false;
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
            // The spool's segment pays out (kind 4) when it did last step, or when the explicit prediction takes the
            // elastic segment past the static hold.
            if (Spool.Active && n >= 2) ChooseSpool();

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
                // The holds the corrections let go stay out of the safe solve (nothing pressed them).
                for (int i = 0; i < n; i++)
                {
                    _c0[i].Slips = _nodes[i].Contact0.Slips;
                    _c1[i].Slips = _nodes[i].Contact1.Slips;
                }
                // The spool's paying model is above its true energy only while line flows: its constant pull on a segment that
                // ends paying nothing (the exit's nodes closing, the line slack at the exit) is a gain the truth never had. Such a
                // segment is solved safe as the elastic one it is (majorized below, as every other); one paying keeps its model.
                bool held = !ZeroForceHold && Spool.Active && n >= 2 && _segKind[n - 2] == 4 && !(SpoolPaid > 0);
                Restore(n);
                Unmove(n);
                if (held) ElasticModel(n - 2);
                for (int j = 0; j + 1 < n; j++)
                    if (_segLen[j] > 0 && _segKind[j] != 4) // a paying spool's model is linear and dissipative while line flows
                    {
                        _segKind[j] = 3;
                        _segDir[j] = (_x0[j + 1] - _x0[j]) * (1 / _segLen[j]);
                    }
                // The free slots stay free unless the safe solve ends their node inside (SafeMissed); model 59's took every
                // one speculative from the start.
                if (ZeroForceHold)
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
                SpoolSpeed = 0;
                SpoolPaid = 0;
                SpoolPaying = false;
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
            for (int pass = 0, corrections = 0, newton = 0; ; pass++)
            {
                // A pass after the first solves again from the lowest node whose model the correction changed: below it the
                // blocks and the forward sweep are the last pass's, the same values, so the same bits.
                int from = pass == 0 ? 0 : _dirty;
                Assemble(dt, from);
                Solve(from);
                for (int i = 0; i < n; i++) _nodes[i].Position = _x0[i] + _nodes[i].Velocity * dt;
                // The active set first (a touch or a tightening missed, a hold let go), then the segments' Newton solves.
                bool newtonPass = false;
                if (safe ? SafeMissed(pass) : corrections < MaxCorrections && Correct(pass == 0)) corrections++;
                else if (safe || LinearStep || ZeroForceHold) break; // the known-wrongs: their models' linear steps
                else if (newton < MaxNewton && Newton())
                {
                    newton++;
                    newtonPass = true;
                }
                else
                {
                    if (newton == MaxNewton && Relinearise(false)) NewtonUnconverged++;
                    break;
                }
                if (newtonPass) NewtonSolves++;
                else Corrections++;
                for (int i = 0; i < n; i++)
                {
                    _nodes[i].Position = _x0[i];
                    _nodes[i].Velocity = _v0[i];
                }
            }

            // The spool's line out (or in), and the energy it brings: the added line's at its nodes, less the static peel's
            // work (what the pack takes for certain), plus a two-way reel's pull.
            double work = Spool.Active && n >= 2 ? SpoolStep(dt) : 0;
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                work += Double3.Dot(a.Force, a.Position - _x0[i]) + dt * Double3.Dot(a.Velocity, a.Drag * a.Air);
                if (a.AnchorDamping > 0) work += dt * a.AnchorDamping * Double3.Dot(a.Velocity, a.AnchorVelocity);
                if (a.AnchorFollows && a.AnchorStiffness > 0) // the target's motion: the spring's pull on the node along it
                    work -= dt * a.AnchorStiffness * Double3.Dot(a.Position - a.AnchorTarget, a.AnchorVelocity);
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
                Double3 along = LineTangent(i);
                double wrap = 0, low = 0;
                if (a.Contact0.Glide || a.Contact1.Glide) Wrap(i, out wrap, out low);
                SettleContact(a.Position, along, wrap, low, ref a.Contact0);
                SettleContact(a.Position, along, wrap, low, ref a.Contact1);
            }
            CapstanRuns();
            // The pack's line drawn into a run that reaches the exit, and the run's profile again from it (a few rounds, numerical:
            // each round the run's high side settles toward the law's tension times e^(μk·θ)).
            if (Spool.Active && n >= 2 && !KeepSlideStretch)
                for (int round = 0; round < SlideRounds; round++)
                {
                    double paid = SpoolPaid;
                    work += SpoolAfterSlide();
                    if (!(SpoolPaid > paid)) break;
                    CapstanRuns();
                }

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
            SpoolSpeed = _spoolSpeed0;
        }

        // The safe pass's free slots (node, member not in the step's model): a free slot's true energy is 0 while its node
        // ends outside, so the model leaving it out is a majorizer there; one whose node the solve ends inside is taken
        // speculative from the start (Missed) and the step solved again, and past MaxCorrections every free slot is (the
        // complete majorizer), for one last solve. Not every free slot from the start: a speculative slot's spring and damper
        // from its start gap hold the node's normal motion and its hold the node where it stood, so a line whose safe pass
        // ran step after step (a slack segment by the drone stretched past its model each step) had its nodes far from it
        // hover over a surface they never reached (E01's run, model 62: two nodes 13 and 16 mm over the trench's spoil,
        // sinking at the damper's 1.5 mm/s, froze into the trail there) and a node riding near a moving part held in the
        // world. True if the step is solved again.
        bool SafeMissed(int pass)
        {
            if (pass > MaxCorrections) return false;
            bool missed = false;
            int n = Count;
            _dirty = n;
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                if (Missed(a.Position, _x0[i], ref a.Contact0) | Missed(a.Position, _x0[i], ref a.Contact1))
                {
                    missed = true;
                    Dirty(i);
                }
            }
            if (missed && pass == MaxCorrections)
                for (int i = 0; i < n; i++)
                {
                    ref CableNode a = ref _nodes[i];
                    Speculate(_x0[i], ref a.Contact0);
                    Speculate(_x0[i], ref a.Contact1);
                    _dirty = 0;
                }
            return missed;
        }

        // Node or segment i's blocks changed in this correction.
        void Dirty(int i)
        {
            if (i < _dirty) _dirty = i;
        }

        // After a pass on settled contacts: every segment (not the spool's) that ends taut with its true tension more than
        // NewtonTolerance from its model's there takes its exact tangent at that end (its point, direction, length, tension and
        // energy), the step to be solved again from its start (apply); true if any did (or, not applying, would). So a segment
        // modelled slack that ends taut (a tightening the correction passes left, or met after them) is taken as one modelled taut
        // that ends rotated. One way only: a segment that ends slack keeps its model (let go, the parts' holds on its nodes stretch
        // it in the next solve, 0.9 mm past its rest on 10.5 mm, 75 N: Newton cycling across the slack's kink). The tangent's
        // quadratic about that point is the model the deficit is taken against (SegmentDeficit), as the start's was.
        bool Relinearise(bool apply)
        {
            bool any = false;
            int n = Count, last = Spool.Active ? n - 2 : n - 1; // the spool's segment is its own model's (ChooseSpool, SpoolMissed)
            for (int j = 0; j < last; j++)
            {
                byte kind = _segKind[j];
                if (kind > 2) continue;
                Double3 d = _nodes[j + 1].Position - _nodes[j].Position;
                double len = d.Length(), rest = _nodes[j + 1].Material - _nodes[j].Material, k = _segStiff[j];
                if (!(len > rest)) continue;
                double truth = k * (len - rest);
                double model = kind == 0 ? 0 : (kind == 1 ? _segTension[j] : 0) + k * (Double3.Dot(_segDir[j], d) - _segLen[j]);
                double error = truth - model;
                if (!(error > NewtonTolerance || error < -NewtonTolerance)) continue;
                any = true;
                if (!apply) return true;
                if (!_segMoved[j])
                {
                    _segMoved[j] = true;
                    _segKind0[j] = kind;
                    _segDir0[j] = _segDir[j];
                    _segLen0[j] = _segLen[j];
                    _segTension0[j] = _segTension[j];
                    _segEnergy0[j] = _segEnergy[j];
                }
                _segKind[j] = 1;
                _segDir[j] = d * (1 / len);
                _segLen[j] = len;
                _segTension[j] = truth;
                _segEnergy[j] = 0.5 * k * (len - rest) * (len - rest);
                _segOffset[j] = d - (_x0[j + 1] - _x0[j]);
                Dirty(j);
            }
            return any;
        }

        // A Newton pass's changes, the segments' tangents: true if any.
        bool Newton()
        {
            _dirty = Count;
            return Relinearise(true);
        }

        // The segments' models at the step's start again (the safe solve's majorizers are built from them).
        void Unmove(int n)
        {
            for (int j = 0; j + 1 < n; j++)
            {
                if (!_segMoved[j]) continue;
                _segMoved[j] = false;
                _segKind[j] = _segKind0[j];
                _segDir[j] = _segDir0[j];
                _segLen[j] = _segLen0[j];
                _segTension[j] = _segTension0[j];
                _segEnergy[j] = _segEnergy0[j];
            }
        }

        static void Speculate(Double3 x, ref CableContact c)
        {
            if (!c.Active || c.Mode != 0) return;
            c.Mode = 2;
            c.Rest = Double3.Dot(c.Normal, x - c.Point);
            c.Engaged = true;
            c.Hold = x;
        }

        // The system's blocks and right-hand side at the step's start (positions and velocities x₀, v₀), from node from on (the
        // nodes below it keep theirs; segment from − 1 adds its part to node from only).
        void Assemble(double dt, int from)
        {
            int n = Count;
            double dt2 = dt * dt;
            for (int i = from; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                Sym3 diag = Iso(a.Mass) + a.Drag * dt;
                Double3 f = Gravity * a.Mass + a.Force + a.Drag * a.Air;
                if (a.AnchorStiffness > 0 || a.AnchorDamping > 0)
                {
                    diag = diag + Iso(dt * a.AnchorDamping + dt2 * a.AnchorStiffness);
                    f = f - (a.Position - a.AnchorTarget) * a.AnchorStiffness + a.AnchorVelocity * a.AnchorDamping;
                }
                Double3 along = a.Contact0.Free || a.Contact1.Free ? LineTangentAt(i) : default;
                AddContact(a.Position, dt, along, ref a.Contact0, ref diag, ref f);
                AddContact(a.Position, dt, along, ref a.Contact1, ref diag, ref f);
                _diag[i] = diag;
                _rhs[i] = a.Velocity * a.Mass + f * dt;
            }
            for (int j = from > 0 ? from - 1 : 0; j + 1 < n; j++)
            {
                byte kind = _segKind[j];
                if (kind == 0)
                {
                    _off[j] = default;
                    continue;
                }
                bool own = j >= from; // node j's blocks are this assembly's
                Double3 d = _segDir[j];
                if (kind == 4)
                {
                    // The spool paying out (CableSpool): the static pull, the damper and the balloon's mass along d, the
                    // geometric stiffness across; the balloon's momentum at the start along d.
                    double across4 = _segTension[j] / _segLen[j];
                    Sym3 k4 = Outer(d, _spoolDamp * dt + _spoolMass - across4 * dt2) + Iso(across4 * dt2);
                    if (own) _diag[j] = _diag[j] + k4;
                    _diag[j + 1] = _diag[j + 1] + k4;
                    _off[j] = k4 * -1.0;
                    Double3 pull4 = d * (dt * _spoolForce - _spoolMass * _spoolSpeed0);
                    if (own) _rhs[j] = _rhs[j] + pull4;
                    _rhs[j + 1] = _rhs[j + 1] - pull4;
                    continue;
                }
                double along = _segStiff[j], across = 0;
                if (kind == 1) across = _segTension[j] / _segLen[j];
                else if (kind == 3) across = along; // the majorizer ½k|Δ|² − k·ℓ·d·Δ: isotropic, about the same force
                along -= across;
                if (_segTension[j] > 0)
                {
                    Double3 pull = d * (dt * _segTension[j]);
                    if (own) _rhs[j] = _rhs[j] + pull;
                    _rhs[j + 1] = _rhs[j + 1] - pull;
                }
                if (_segMoved[j])
                {
                    // The tangent at a Newton point x̂ (Relinearise): its force at the start's x₀ is T̂·d + K·(x₀ − x̂) along the segment
                    // vector, so the stiffness's pull back from the point's offset joins the right-hand side.
                    Double3 w = _segOffset[j];
                    Double3 back = (d * (along * Double3.Dot(d, w)) + w * across) * -dt;
                    if (own) _rhs[j] = _rhs[j] + back;
                    _rhs[j + 1] = _rhs[j + 1] - back;
                }
                Sym3 k = Outer(d, along * dt2) + Iso(across * dt2);
                if (own) _diag[j] = _diag[j] + k;
                _diag[j + 1] = _diag[j + 1] + k;
                _off[j] = k * -1.0;
            }
        }

        // Thomas: D₀ = A₀, Dᵢ = Aᵢ − Bᵢ₋₁·Dᵢ₋₁⁻¹·Bᵢ₋₁, then back; the new velocities into the nodes. The forward sweep from node
        // from (below it the last solve's, on the same blocks).
        void Solve(int from)
        {
            int n = Count;
            if (from == 0)
            {
                _invD[0] = _diag[0].Inverse();
                _y[0] = _rhs[0];
                from = 1;
            }
            for (int i = from; i < n; i++)
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
        bool Correct(bool first)
        {
            bool missed = false;
            int n = Count;
            _dirty = n;
            for (int j = 0; j + 1 < n; j++)
            {
                if (_segKind[j] != 0 || !(_segLen[j] > 0)) continue;
                double rest = _nodes[j + 1].Material - _nodes[j].Material;
                if (!((_nodes[j + 1].Position - _nodes[j].Position).Length() > rest)) continue;
                _segKind[j] = 2;
                _segDir[j] = (_x0[j + 1] - _x0[j]) * (1 / _segLen[j]);
                missed = true;
                Dirty(j);
            }
            for (int i = 0; i < n; i++)
            {
                ref CableNode a = ref _nodes[i];
                bool changed = Missed(a.Position, _x0[i], ref a.Contact0) | Missed(a.Position, _x0[i], ref a.Contact1);
                if (a.Contact0.Glide || a.Contact1.Glide)
                {
                    if (first) changed |= Released(a.Position, ref a.Contact0) | Released(a.Position, ref a.Contact1);
                    Double3 along = a.Contact0.Free || a.Contact1.Free ? LineTangentAt(i) : default;
                    changed |= Unstuck(a.Position, along, ref a.Contact0) | Unstuck(a.Position, along, ref a.Contact1);
                }
                if (!changed) continue;
                missed = true;
                Dirty(i);
            }
            if (Spool.Active && n >= 2 && !Spool.Locked && SpoolMissed())
            {
                missed = true;
                Dirty(n - 2);
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

        // A gliding slot taken speculative (the node predicted to reach the part, or within KinematicBand of its end-of-step
        // place) whose spring ends the pass pulling the node toward the part (the gap past the one it started from) is let go
        // for the rest of the step: a part only pushes. Its spring from the start gap, measured to the part's end-of-step place,
        // pins the node's motion along the normal in the world, so a node riding a few millimetres off a part at the drone's
        // speed is pulled back by the part's travel in the step (7 mm at 7.4 m/s: 70 N on a node of 3 mg). Let go, it stays
        // out of the solve unless the node ends inside the part, where the next pass takes it again (Missed). Only after the
        // first pass, so a later one never lets go of a part the node then ends inside with no pass left to take it.
        bool Released(Double3 x, ref CableContact c)
        {
            if (ZeroForceHold || !c.Active || c.Mode != 2 || !c.Glide) return false;
            if (!(Double3.Dot(c.Normal, x - c.Point) > c.Rest)) return false;
            c.Mode = 0;
            c.Engaged = false;
            return true;
        }

        // A gliding slot whose hold ends the pass pulling while nothing presses the node on the part (N, the pass's normal force,
        // 0: a node in a crotch of two parts, or within KinematicBand of one, may press neither) holds nothing for the rest of the
        // step: Coulomb's bound μs·N is 0, and a hold at its full stiffness would carry the line's pull at the part's speed (model
        // 59's crotch: the span stretched at 60 N/s to its break). A pressed slot keeps 1.7's hold and its slips after the solve.
        // A free slot's along-line part is not held (AddContact) and not judged.
        bool Unstuck(Double3 x, Double3 along, ref CableContact c)
        {
            if (ZeroForceHold || !c.Active || c.Mode == 0 || !c.Glide || c.Slips || !(c.HoldStiffness > 0)) return false;
            if (-c.Stiffness * (Double3.Dot(c.Normal, x - c.Point) - c.Rest) > 0) return false;
            Double3 off = Tangential(x - c.Hold, c.Normal);
            if (c.Free)
            {
                Double3 axis = FreeAxis(along, c.Normal);
                off = off - axis * Double3.Dot(axis, off);
            }
            if (!(off.Length() > 0)) return false;
            c.Slips = true;
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

        // A Kinematic contact's predicted end: the node's drift, or (within KinematicBand) the point on the member's plane past it.
        static Double3 Banded(Double3 x, Double3 drift, ref CableContact c)
        {
            if (!c.Active) return drift;
            double g = Double3.Dot(c.Normal, x - c.Point);
            return g > 0 && g <= KinematicBand ? x - c.Normal * (2 * g) : drift;
        }

        static void ChooseContact(Double3 x, Double3 predicted, double dt, Double3 along, ref CableContact c)
        {
            c.Slips = false;
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
                c.Rest = c.Sunk < 0 ? c.Sunk : 0;
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
                c.Touched = x;
            }
            else c.Hold = c.Hold + c.Velocity * dt;
            // A free slot's hold holds nothing along the line: it starts the step at the node along it (no energy there).
            if (c.Mode != 0 && c.Free) c.Hold = c.Hold + FreeAxis(along, c.Normal) * Double3.Dot(FreeAxis(along, c.Normal), x - c.Hold);
        }

        // The line's direction in a contact's tangent plane, unit (0 for a line along the normal).
        static Double3 FreeAxis(Double3 along, Double3 normal)
        {
            Double3 t = along - normal * Double3.Dot(normal, along);
            double l = t.Length();
            return l > 0 ? t * (1 / l) : default;
        }

        // The contact's model on the diagonal and in the force: the normal spring from its rest gap (the surface when
        // pressing, the gap at the start when speculative) and its damper, the hold's tangential spring.
        static void AddContact(Double3 x, double dt, Double3 along, ref CableContact c, ref Sym3 diag, ref Double3 f)
        {
            if (c.Mode == 0) return;
            Double3 nrm = c.Normal;
            double g = Double3.Dot(nrm, x - c.Point), hold = c.Slips ? 0 : c.HoldStiffness;
            diag = diag + Outer(nrm, dt * c.Damping + dt * dt * (c.Stiffness - hold)) + Iso(dt * dt * hold);
            f = f + nrm * (-c.Stiffness * (g - c.Rest) + c.Damping * Double3.Dot(nrm, c.Velocity));
            if (c.Slips || !(c.HoldStiffness > 0)) return;
            Double3 off = Tangential(x - c.Hold, nrm);
            if (c.Free)
            {
                // Free along the line: the hold's spring acts across it only.
                Double3 axis = FreeAxis(along, nrm);
                diag = diag + Outer(axis, -dt * dt * c.HoldStiffness);
                off = off - axis * Double3.Dot(axis, off);
            }
            f = f - off * c.HoldStiffness;
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

        // The line's direction at node i at the step's start, as the assembly takes its models there.
        Double3 LineTangentAt(int i)
        {
            Double3 t = _x0[i + 1 < Count ? i + 1 : i] - _x0[i > 0 ? i - 1 : i];
            double l = t.Length();
            return l > 0 ? t * (1 / l) : default;
        }

        // The line's unit direction at node i (its neighbours' chord; one segment's at an end), 0 for a point.
        Double3 LineTangent(int i)
        {
            Double3 t = _nodes[i + 1 < Count ? i + 1 : i].Position - _nodes[i > 0 ? i - 1 : i].Position;
            double l = t.Length();
            return l > 0 ? t * (1 / l) : default;
        }

        // The line's turn at node i and the lower of its two segments' tensions (0 at an end).
        void Wrap(int i, out double turn, out double low)
        {
            turn = 0;
            low = 0;
            if (i == 0 || i + 1 >= Count) return;
            Double3 da = _nodes[i].Position - _nodes[i - 1].Position, db = _nodes[i + 1].Position - _nodes[i].Position;
            turn = DetMath.Atan2(Double3.Cross(da, db).Length(), Double3.Dot(da, db));
            double t0 = Tension(i - 1), t1 = Tension(i);
            low = t0 < t1 ? t0 : t1;
        }

        static void SettleContact(Double3 x, Double3 along, double wrap, double low, ref CableContact c)
        {
            c.Slid = false;
            if (c.Mode == 0)
            {
                c.NormalForce = 0;
                return;
            }
            double g = Double3.Dot(c.Normal, x - c.Point);
            double normal = -c.Stiffness * (g - c.Rest);
            c.NormalForce = normal > 0 ? normal : 0;
            if (!(c.HoldStiffness > 0) || c.Capstan && c.Notch) return; // a notch holds the member's point; line slides through it
            Double3 t = Tangential(x - c.Hold, c.Normal);
            if (c.Slips)
            {
                // Nothing pressed: the hold comes to the node (its spring was out of the solve), so it stores nothing.
                c.Hold = c.Hold + t;
                c.Slid = true;
                return;
            }
            // A capstan's hold along the line is the member's point (line slides through it by the capstan); across the line
            // it slips by Coulomb, as a plain hold does (the line sliding along a twig, a wire or a skid).
            if (c.Capstan)
            {
                Double3 axis = FreeAxis(along, c.Normal);
                Double3 on = axis * Double3.Dot(axis, t);
                t = t - on;
                double pull = c.HoldStiffness * on.Length();
                if (c.Free)
                {
                    c.Hold = c.Hold + on; // slipping by: the hold follows the node along the line
                    c.Slid = true;
                }
                else if (c.Glide && pull > 0 && pull > low * (DetMath.Exp(c.StaticFriction * wrap) - 1))
                {
                    c.Hold = c.Hold + on * (1 - low * (DetMath.Exp(c.KineticFriction * wrap) - 1) / pull);
                    c.Slid = true;
                }
            }
            double held = c.HoldStiffness * t.Length(), limit = c.StaticFriction * c.NormalForce;
            if (held > limit && held > 0)
            {
                // Coulomb: the hold slides toward the node until it pulls with μk·N (its spring only loses energy).
                double keep = c.KineticFriction * c.NormalForce / held;
                c.Hold = c.Hold + t * (1 - keep);
                c.Slid = true;
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
            byte kind = _segKind[j];
            if (kind == 4)
            {
                // The spool's: what the segment holds at its new rest length plus the static peel's work the budget took
                // out, against the model's static pull and its geometric stiffness about the start.
                Double3 dir4 = _segDir[j];
                Double3 delta4 = d - dir4 * _segLen[j];
                double along4 = Double3.Dot(dir4, delta4), across4 = _segTension[j] / _segLen[j];
                double model4 = _segEnergy[j] + _spoolForce * along4 + 0.5 * across4 * (Double3.Dot(delta4, delta4) - along4 * along4);
                truth += _spoolPeelWork;
                return truth > model4 ? truth - model4 : 0;
            }
            if (truth == 0) return 0;
            double model;
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
                _capHold[i] = holdIn; // and the static one
            }
            double tIn = _capT[first - 1], tOut = _capT[last];
            bool outward = tOut >= tIn; // material flows toward the high side
            double hi = outward ? tOut : tIn, lo = outward ? tIn : tOut;
            if (!(hi > 0 && hi > DetMath.Exp(holdIn) * lo) && (RunEndsOnly || !Overheld(first, last, tIn, tOut, holdIn))) return;

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

        // p7d-tether design 21: a segment inside the run (between two of its nodes) carries more than friction from either end can
        // hold it to, T_j > min(T_in·e^(μs·θ in to j), T_out·e^(μs·θ j to out)), by more than the solve's own tension error
        // (NewtonTolerance): no wrap holds that, so material slides into it (the run's profile). Without it a piece between two
        // nodes on one member stayed stretched while both ends were slack, its nodes' holds along the line carrying the pull.
        bool Overheld(int first, int last, double tIn, double tOut, double hold)
        {
            for (int j = first; j < last; j++)
            {
                double fromIn = tIn * DetMath.Exp(_capHold[j]), fromOut = tOut * DetMath.Exp(hold - _capHold[j]);
                double bound = fromIn < fromOut ? fromIn : fromOut;
                if (_capT[j] > bound + NewtonTolerance) return true;
            }
            return false;
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
            return !(a.AnchorStiffness > 0) && !(a.AnchorDamping > 0) && (!a.Contact0.Active || a.Contact0.Remeshable)
                && (!a.Contact1.Active || a.Contact1.Remeshable) && !a.Crest;
        }

        /// <summary>The nodes marked Crest (none without CrestCapacity).</summary>
        public int Crests
        {
            get
            {
                if (CrestCapacity == 0) return 0;
                int crests = 0;
                for (int i = 0; i < Count; i++)
                    if (_nodes[i].Crest) crests++;
                return crests;
            }
        }

        /// <summary>The spacing law's room is spent: SpacingCapacity nodes besides the crests (the chain full without crests).</summary>
        public bool SpacingFull => Count - Crests >= SpacingCapacity;

        bool Remesh()
        {
            bool changed = false;
            int j = 0;
            // The law's room (w3-farmland-ground 2.5): a crest takes none of it, and neither its splits nor its merges make or
            // take one.
            int crests = Crests;
            while (j + 1 < Count)
            {
                double sa = _nodes[j].Material, sb = _nodes[j + 1].Material, rest = sb - sa;
                if (Spool.Active && j + 2 == Count)
                {
                    // The spool's segment grows as line leaves: past twice the exit's target it leaves a segment of the
                    // target behind it, so the line trails at the near spacing (a midpoint split would halve it); the piece a
                    // hair under the target (2^−20 of it, numerical) so rounding never reads it past and splits it again.
                    double h = Target(sb);
                    // A full chain first merges the pair of segments that overshoots its target least (the far line coarser than
                    // its law rather than the line at the exit).
                    if (rest > 2 * h && Count - crests >= SpacingCapacity && MakeRoom())
                    {
                        changed = true;
                        j = Count - 2; // the spool's segment, one node nearer the fixed end
                    }
                    if (rest > 2 * h && Count - crests < SpacingCapacity)
                    {
                        Split(j, h * (1 - 1.0 / 1048576) / rest);
                        changed = true;
                    }
                    else if (rest > 2 * h) SplitsRefused++;
                    // Line taken back (a two-way spool) shrinks it: under half the target the node behind it merges away, so
                    // the reel goes on taking line in.
                    else if (rest < 0.5 * h && Count > 2 && Removable(j)) changed |= TryMerge(j);
                    break;
                }
                if (rest > Target(0.5 * (sa + sb)))
                {
                    if (Count - crests >= SpacingCapacity)
                    {
                        SplitsRefused++;
                        j++;
                        continue;
                    }
                    Split(j, 0.5);
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

        // The removable node whose two segments' sum is the least over the target at their middle, merged away (TryMerge's
        // energy rule); ties to the first from the fixed end. True if one was.
        bool MakeRoom()
        {
            int best = -1;
            double least = double.PositiveInfinity;
            for (int m = 1; m + 1 < Count - 1; m++)
            {
                if (!Removable(m)) continue;
                double sa = _nodes[m - 1].Material, sc = _nodes[m + 1].Material, ratio = (sc - sa) / Target(0.5 * (sa + sc));
                if (ratio < least)
                {
                    least = ratio;
                    best = m;
                }
            }
            return best > 0 && TryMerge(best);
        }

        // A segment split at its midpoint: position, velocity and material halfway, so momentum is kept, the springs'
        // energy is unchanged and the kinetic falls by μℓ₀/16·|v_a − v_b|².
        // On the spool's segment the line at the exit moves with the exit across it and with the balloon along it, v − ṡ·d (the
        // exit node's own velocity is the drone's), so the new node takes the line's. At a fraction f from node j: position,
        // velocity and material at f (a midpoint at ½), so momentum is kept, the straight segment's strain and so its springs'
        // energy are unchanged and the kinetic falls.
        void Split(int j, double f)
        {
            Double3 exitLine = default;
            bool exit = Spool.Active && j + 2 == Count;
            if (exit)
            {
                Double3 r = _nodes[j + 1].Position - _nodes[j].Position;
                double len = r.Length();
                exitLine = _nodes[j + 1].Velocity - (len > 0 ? r * (SpoolSpeed / len) : default);
            }
            Array.Copy(_nodes, j + 1, _nodes, j + 2, Count - j - 1);
            ref CableNode a = ref _nodes[j];
            ref CableNode b = ref _nodes[j + 2];
            _nodes[j + 1] = f == 0.5
                ? new CableNode
                {
                    Position = (a.Position + b.Position) * 0.5,
                    Velocity = (a.Velocity + (exit ? exitLine : b.Velocity)) * 0.5,
                    Material = 0.5 * (a.Material + b.Material),
                }
                : new CableNode
                {
                    Position = a.Position + (b.Position - a.Position) * f,
                    Velocity = a.Velocity + ((exit ? exitLine : b.Velocity) - a.Velocity) * f,
                    Material = a.Material + (b.Material - a.Material) * f,
                };
            Count++;
            _nodes[j + 1].Placed = true;
            // The new node lies on segment j: both pieces lie on its certified core (CableNode.Cleared).
            _nodes[j + 1].Cleared = a.Cleared;
            _nodes[j + 1].ClearA = a.ClearA;
            _nodes[j + 1].ClearB = a.ClearB;
            _nodes[j + 1].Near = a.Near;
            _nodes[j + 1].GroundClear = a.GroundClear; // and on its ground certificate's core (CableNode.GroundClear)
            _nodes[j + 1].GroundA = a.GroundA;
            _nodes[j + 1].GroundB = a.GroundB;
            _nodes[j + 1].GroundSlack = a.GroundSlack;
            a.Mass = MassOf(j);
            _nodes[j + 1].Mass = MassOf(j + 1);
            b.Mass = MassOf(j + 2);
            Splits++;
        }

        // Node m removed: its mass goes to its neighbours by the rest lengths (μ·ℓ_b/2 to the one before, μ·ℓ_a/2 to the one
        // after), merging inelastically (momentum kept, kinetic energy can only fall). Done only if the whole energy
        // (springs, kinetic, gravity) does not rise; on a spool's chain, whose payout splits a node off every near spacing of
        // line, a rise (a sagging line lumped coarser holds its mass higher) the line's relative motion can give back is taken
        // from it about the mean velocity, momentum kept (labelled numerical damping, MergeClips), so the far line coarsens.
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
            double rise = after - before + kinetic + potential, relative = 0;
            if (rise > 0)
            {
                if (!Spool.Active) return false;
                relative = RelativeKinetic(out _) + kinetic; // the merge keeps the mean velocity, so it lowers this by its own loss
                if (!(rise < relative)) return false;
            }
            a.Velocity = va;
            b.Velocity = vb;
            Array.Copy(_nodes, m + 1, _nodes, m, Count - m - 1);
            Count--;
            _nodes[Count] = default;
            _nodes[m - 1].Mass = MassOf(m - 1);
            _nodes[m].Mass = MassOf(m);
            Merges++;
            if (rise > 0)
            {
                TakeBack(rise, relative);
                MergeClips++;
                MergeClipped += rise;
            }
            return true;
        }

        double MassOf(int i)
        {
            double before = i > 0 ? _nodes[i].Material - _nodes[i - 1].Material : 0;
            double after = i + 1 < Count ? _nodes[i + 1].Material - _nodes[i].Material : 0;
            return 0.5 * MassPerMetre * (before + after);
        }

        // ---- the spool (design 3: CableSpool) ----

        void ChooseSpool()
        {
            int j = Count - 2;
            if (Spool.Locked) return;
            bool pay = Spool.TwoWay || SpoolSpeed > 0;
            if (!pay)
            {
                double rest = _s0[j + 1] - _s0[j];
                pay = AxialStiffness * ((_pred[j + 1] - _pred[j]).Length() / rest - 1) > Spool.Lip * Spool.Peel;
            }
            if (pay) PayModel(j);
        }

        // The paying segment's model from the step's start: the static pull e^(μθ)·P(0); the damper, the rate law's and the
        // flux's secant at the last step's ṡ, (e^(μθ)·P(0)·(rate − 1) + μ·ṡ²)/ṡ; the balloon's mass e^(μθ)·m_b; the expected
        // tension for the geometric stiffness across.
        void PayModel(int j)
        {
            Double3 r = _x0[j + 1] - _x0[j];
            double len = r.Length();
            if (!(len > 0)) return;
            double rest = _s0[j + 1] - _s0[j], k = AxialStiffness / rest, sigma = _spoolSpeed0 > 0 ? _spoolSpeed0 : 0;
            _segKind[j] = 4;
            _segDir[j] = r * (1 / len);
            _segLen[j] = len;
            _segStiff[j] = k;
            _segEnergy[j] = len > rest ? 0.5 * k * (len - rest) * (len - rest) : 0;
            _spoolForce = Spool.Lip * Spool.Peel;
            _spoolDamp = sigma > 0 ? (_spoolForce * (Rate(sigma) - 1) + Spool.FluxMass * sigma * sigma) / sigma : 0;
            _spoolMass = Spool.Lip * Spool.BalloonMass;
            _segTension[j] = _spoolForce + _spoolDamp * sigma;
        }

        // The elastic segment's model from the step's start (step 4's rule; a segment the spool gives back is taken
        // speculative when slack, above its true energy).
        void ElasticModel(int j)
        {
            Double3 d = _x0[j + 1] - _x0[j];
            double len = d.Length(), rest = _s0[j + 1] - _s0[j], k = AxialStiffness / rest;
            _segStiff[j] = k;
            _segLen[j] = len;
            _segTension[j] = 0;
            _segEnergy[j] = 0;
            if (!(len > 0))
            {
                _segKind[j] = 0;
                return;
            }
            _segDir[j] = d * (1 / len);
            if (len >= rest)
            {
                _segKind[j] = 1;
                _segTension[j] = k * (len - rest);
                _segEnergy[j] = 0.5 * k * (len - rest) * (len - rest);
            }
            else _segKind[j] = 2;
        }

        // After a pass: a paying segment whose line came back (one way) holds; a holding one past the static hold pays.
        bool SpoolMissed()
        {
            int j = Count - 2;
            if (_segKind[j] == 4)
            {
                if (Spool.TwoWay || !(Double3.Dot(_segDir[j], _nodes[j + 1].Velocity - _nodes[j].Velocity) < 0)) return false;
                ElasticModel(j);
                return true;
            }
            double rest = _s0[j + 1] - _s0[j];
            if (!(AxialStiffness * ((_nodes[j + 1].Position - _nodes[j].Position).Length() / rest - 1) > Spool.Lip * Spool.Peel)) return false;
            PayModel(j);
            return _segKind[j] == 4;
        }

        // The line out through the exit after the solve: while paying, ṡ is the separation rate along the segment and the
        // rest length grows until the segment's tension is the law's at ṡ; while holding, a tension past the static hold the
        // last pass left slides out (closed form, the balloon at rest). The added line takes its node's velocity (its mass
        // moves with the material); the energy it brings, less the static peel's work.
        double SpoolStep(double dt)
        {
            int j = Count - 2;
            ref CableNode a = ref _nodes[j];
            ref CableNode b = ref _nodes[j + 1];
            double rest = b.Material - a.Material, len = (b.Position - a.Position).Length();
            double sdot = 0, ds = 0;
            bool paying = _segKind[j] == 4;
            _spoolPeelWork = 0;
            if (paying)
            {
                sdot = Double3.Dot(_segDir[j], b.Velocity - a.Velocity);
                if (!Spool.TwoWay && sdot < 0) sdot = 0; // the line went slack at the exit: the balloon stops in the pack
                if (sdot != 0 || Spool.TwoWay)
                {
                    double t = sdot >= 0 ? ExitLaw(sdot, dt) : Spool.Lip * Spool.Peel;
                    ds = len / (1 + t / AxialStiffness) - rest;
                    if (!Spool.TwoWay && ds < 0) ds = 0;
                }
                else if (!Spool.Locked && len > rest && !KeepSlideStretch)
                {
                    // Its ends not parting this step, yet stretched past the static hold (a node a part's or a member's slot
                    // moved at the step's end, p7d-tether design 18: 1–3 N held on the exit's segment against the law's 0.26 N,
                    // and on the hole's edge): the pack lets the excess out, as a holding segment's does.
                    double hold = Spool.Lip * Spool.Peel;
                    if (AxialStiffness * (len / rest - 1) > hold) ds = len / (1 + hold / AxialStiffness) - rest;
                }
            }
            else if (!Spool.Locked && len > rest)
            {
                double hold = Spool.Lip * Spool.Peel;
                if (AxialStiffness * (len / rest - 1) > hold) ds = len / (1 + hold / AxialStiffness) - rest;
            }
            if (ds < MinRest - rest) ds = MinRest - rest;
            double energy = 0;
            if (ds != 0)
            {
                double ma = a.Mass, mb = b.Mass;
                b.Material += ds;
                a.Mass = MassOf(j);
                b.Mass = MassOf(j + 1);
                double dma = a.Mass - ma, dmb = b.Mass - mb;
                energy = 0.5 * dma * Double3.Dot(a.Velocity, a.Velocity) + 0.5 * dmb * Double3.Dot(b.Velocity, b.Velocity)
                    - dma * Double3.Dot(Gravity, a.Position) - dmb * Double3.Dot(Gravity, b.Position);
                if (ds > 0 && paying) _spoolPeelWork = Spool.Lip * Spool.Peel * ds;
                if (ds < 0) energy += Spool.Lip * Spool.Peel * -ds; // a two-way reel's motor pulls the line in
            }
            SpoolSpeed = paying ? sdot : 0;
            SpoolPaid = ds;
            SpoolPaying = ds > 0;
            SpoolPackTension = paying && sdot > 0 ? Spool.FluxMass * sdot * sdot + Inner(sdot, dt) : Spool.Peel;
            double restEnd = b.Material - a.Material;
            SpoolExitTension = len > restEnd ? AxialStiffness * (len / restEnd - 1) : 0;
            return energy - _spoolPeelWork;
        }

        // After the capstans' slides (p7d-tether design 18): a run that reaches the exit's node slides material out of the exit's
        // segment toward its high side, which left that segment stretched past the spool's law (1–3 N against 0.2 N where the
        // drone's leg had the line wrapped and the trail held it behind). The line round the leg is drawn from the pack, so the
        // pack lets out what brings the segment back to the step's law (paying) or its static hold; the energy it brings, less
        // the static peel's work, as SpoolStep's.
        double SpoolAfterSlide()
        {
            if (Spool.Locked || Spool.TwoWay) return 0;
            int j = Count - 2;
            ref CableNode a = ref _nodes[j];
            ref CableNode b = ref _nodes[j + 1];
            double rest = b.Material - a.Material, len = (b.Position - a.Position).Length();
            double allowed = SpoolPaying ? SpoolExitTension : Spool.Lip * Spool.Peel;
            if (!(rest > 0) || !(AxialStiffness * (len / rest - 1) > allowed)) return 0;
            double ds = len / (1 + allowed / AxialStiffness) - rest;
            if (!(ds > 0)) return 0;
            double ma = a.Mass, mb = b.Mass;
            b.Material += ds;
            a.Mass = MassOf(j);
            b.Mass = MassOf(j + 1);
            double dma = a.Mass - ma, dmb = b.Mass - mb;
            double energy = 0.5 * dma * Double3.Dot(a.Velocity, a.Velocity) + 0.5 * dmb * Double3.Dot(b.Velocity, b.Velocity)
                - dma * Double3.Dot(Gravity, a.Position) - dmb * Double3.Dot(Gravity, b.Position);
            double peel = Spool.Lip * Spool.Peel * ds;
            _spoolPeelWork += peel;
            SpoolPaid += ds;
            SpoolPaying = true;
            SpoolExitTension = allowed;
            return energy - peel;
        }

        // The pack's resistance inside the lip less its momentum flux, P(ṡ) + m_b·s̈ (never below zero: a line the balloon
        // would push out goes slack), and the exit's tension over the moving lip.
        double Inner(double sdot, double dt)
        {
            double v = Spool.Peel * Rate(sdot) + Spool.BalloonMass * (sdot - _spoolSpeed0) / dt;
            return v > 0 ? v : 0;
        }

        double ExitLaw(double sdot, double dt) => Spool.FluxMass * sdot * sdot + Spool.Lip * Inner(sdot, dt);

        double Rate(double sdot) =>
            sdot > Spool.ReferenceRate ? DetMath.Exp(Spool.RateExponent * DetMath.Log(sdot / Spool.ReferenceRate)) : 1;

        /// <summary>A node put into segment j at the fraction f of it from node j (a contact node where the segment meets a
        /// member, design 6): position, velocity and material at f, so momentum is kept, the straight segment's springs
        /// are unchanged and the kinetic energy only falls (Split). False when the spacing law's room is spent (SpacingFull) or
        /// the piece would be shorter than the chain's least rest length.</summary>
        public bool SplitAt(int j, double f)
        {
            if (SpacingFull || j < 0 || j + 1 >= Count || !(f > 0 && f < 1)) return false;
            double rest = _nodes[j + 1].Material - _nodes[j].Material;
            if (!(f * rest > MinRest) || !((1 - f) * rest > MinRest)) return false;
            Split(j, f);
            return true;
        }

        /// <summary>A crest's node put into segment j at at, on the ground where the chord passes under it (w3-farmland-ground
        /// 2.5): the share of the segment's material the caller gives the piece from node j (the line's length over the ground
        /// there), its velocity at that share (Split's: momentum kept); marked Crest. False when the crests' room is spent, the
        /// chain is full or a piece would be shorter than the least rest length.</summary>
        public bool SplitCrest(int j, double share, Double3 at)
        {
            if (Count == Capacity || Crests >= CrestCapacity || j < 0 || j + 1 >= Count || !(share > 0 && share < 1)) return false;
            double rest = _nodes[j + 1].Material - _nodes[j].Material;
            if (!(share * rest > MinRest) || !((1 - share) * rest > MinRest)) return false;
            Split(j, share);
            _nodes[j + 1].Position = at;
            _nodes[j + 1].Crest = true;
            return true;
        }

        // ---- the laid trail's end (design 3) ----

        /// <summary>A node at rest put before node 0 (the trail's last point lifted back into the chain): material below node
        /// 0's. The caller moves node 0's anchor to it.</summary>
        public void InsertFirst(Double3 position, double material)
        {
            if (Count == Capacity) throw new InvalidOperationException("the chain is full");
            if (!(material < _nodes[0].Material - MinRest)) throw new ArgumentException("material coordinates must rise");
            Array.Copy(_nodes, 0, _nodes, 1, Count);
            _nodes[0] = new CableNode { Position = position, Material = material };
            Count++;
            _nodes[0].Mass = MassOf(0);
            _nodes[1].Mass = MassOf(1);
        }

        /// <summary>Node 0 taken off (frozen into the trail with node 1 now the chain's end), its mass leaving with it.</summary>
        public void RemoveFirst()
        {
            if (Count <= 2) throw new InvalidOperationException("a chain keeps two nodes");
            Array.Copy(_nodes, 1, _nodes, 0, Count - 1);
            Count--;
            _nodes[Count] = default;
            _nodes[0].Mass = MassOf(0);
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
