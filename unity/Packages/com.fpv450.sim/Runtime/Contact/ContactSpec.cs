using System;

namespace Fpv450.Sim.Contact
{
    /// <summary>What a drone contact shape does (drone.json's contacts.shapes[].role).</summary>
    public enum ContactRole : byte
    {
        /// <summary>A load-bearing sphere on a skid: the ground under it from SampleGround, objects through StaticContacts,
        /// carried by its corner's leg spring (PHB-05: load-bearing parts are spheres).</summary>
        Support = 0,

        /// <summary>A skid segment's capsule: objects only (crossings between the supports), carried by its corner's leg.</summary>
        Skid = 1,

        /// <summary>A frame part (an arm edge, a motor can, the stack, a pack, the payload, the spool, a leg tube): the ground
        /// and objects, with its own compliance in series with the material.</summary>
        Body = 2,

        /// <summary>A prop disc: strikes only (no force), its bounding sphere then a ring of capsules round its rim.</summary>
        Disc = 3,

        /// <summary>
        /// A prop blade's edge at rest (p7f-damage 1.3b, design 16, D-082): a capsule along one edge of one span segment of one
        /// blade, placed each Sense at the rotor's angle from the blade's state, carried like a body part while its rotor turns
        /// slower than ContactSpec.BladeRestSpeed (the strikes take a faster one). Its Stiffness and StiffnessUp are its mount's
        /// (the motor's), in series with the blade's own bending at the contact; DampingRatio is the blade's.
        /// </summary>
        Blade = 4,
    }

    /// <summary>
    /// One contact shape of the drone in body axes (DetMath.cs) at a loadout: a sphere (A = B) or a capsule A–B of
    /// Radius. Mass shapes are not contact shapes (review-physics P5): the arms are 25 × 6 mm plates, the skids carry
    /// spheres. A shape belongs to a mass part, whose name the log's events carry and whose legs-off fate it follows.
    /// </summary>
    public struct ContactShape
    {
        public string Name;

        /// <summary>The mass part it belongs to, an index into DroneSpec.Parts (and the mass model's parts).</summary>
        public int Part;

        public ContactRole Role;

        /// <summary>The leg's corner (Corners order) for supports, skids and discs; −1 otherwise.</summary>
        public int Corner;

        /// <summary>The drone side's contact material (DroneSpec.ContactMaterials), for the log and a later pairing table.</summary>
        public string Material;

        /// <summary>Body m: the core segment; a sphere and a disc's hub have A = B.</summary>
        public Double3 A, B;

        /// <summary>m: the sphere's or capsule's radius, or the disc's radius.</summary>
        public double Radius;

        /// <summary>A disc's half-thickness (the blade's), m; 0 otherwise.</summary>
        public double HalfThickness;

        /// <summary>A body shape's own series stiffness, N/m, and the damping ratio of that compliance; 0 for the others (the
        /// legs' spring carries supports and skids). Stiffness acts across body y (in the arm plane), StiffnessUp along it:
        /// a plate arm bends out of its plane far more easily (equal for a part without that difference). Along a contact
        /// normal n (body) the stiffness is 1/(n_y²/StiffnessUp + (1 − n_y²)/Stiffness).</summary>
        public double Stiffness, StiffnessUp, DampingRatio;
    }

    /// <summary>One row of assets-src/physics_materials.json: a world material as the drone's parts meet it.</summary>
    public struct MaterialRow
    {
        public string Name;
        public double StaticFriction, KineticFriction;

        /// <summary>N/m in series with the touching part's own; 0 for a rigid material (no stiffness_n_per_m in the file).</summary>
        public double Stiffness;

        /// <summary>The damping ratio of that compliance; 0 for a rigid material.</summary>
        public double DampingRatio;

        /// <summary>How a layer's Stiffness scales with a part's footprint (p7a-ground design 6); None for a row read per part as it stands.</summary>
        public AreaLaw AreaLaw;

        /// <summary>The layer's depth at which Stiffness was derived for the reference foot, m; 0 without an area law.</summary>
        public double AreaReferenceDepth;

        /// <summary>A slender member's Young's modulus for its cantilever bending (bark, deadwood), Pa; 0 for none.</summary>
        public double ElasticModulus;

        /// <summary>
        /// The row's wet block (Rain and Severe rain alike): the wet stiffness, its ratio to Stiffness, μs and μk, and the
        /// cohesive pull σ_a (Pa) with its release separation δ_a (m); every field 0 when HasWet is false.
        /// </summary>
        public bool HasWet;
        public double WetStiffnessRatio, WetStiffness, WetStaticFriction, WetKineticFriction, WetAdhesion, WetAdhesionRelease;
        /// <summary>What a blade meets (p7f-damage design 5): the face's indentation hardness, Pa, against the blade's (a harder
        /// face is a hard target and the blade yields, a softer one yields to it); the pressure it resists a blade carving it,
        /// Pa, 0 where the file's is null (a thin compliant row a blade passes without damage from it); and its density,
        /// kg/m³, the mass a carving blade flings.</summary>
        public double Hardness, CrushStrength, Density;

        /// <summary>The radius of the row's arrises a line bends over (its edge_radius_m), m; 0 where the file's is null (a
        /// row whose members are round, or homogenised: the fiber reads their own radius or skips them, p7d-tether 1.7).</summary>
        public double EdgeRadius;

        public readonly bool Rigid => Stiffness == 0;
    }

    /// <summary>
    /// A compliant layer's area law (p7a-ground design 6): mat, a loose layer spreading the load 2:1 through its depth
    /// (k = E_m·A_s/D); punch, an elastic flat punch (k ∝ the footprint's equivalent diameter, Sneddon); winkler, a loose
    /// fill (k ∝ the footprint's area).
    /// </summary>
    public enum AreaLaw : byte { None = 0, Mat = 1, Punch = 2, Winkler = 3 }

    /// <summary>
    /// The flight's contact model inputs (p6-flight-core design 7): drone.json's contact shapes at the pilot's loadout,
    /// its thresholds, the legs' spring, and the physics materials table. Built by DroneSpec.BuildContacts; the flight
    /// keeps its own copy and the log header records every field, so a replay needs no file.
    /// </summary>
    public sealed class ContactSpec
    {
        /// <summary>StaticContacts' margin, m: at least the deepest stop less what a part needs to clear the shape
        /// (review-physics P2).</summary>
        public double Margin;

        /// <summary>A contact acceleration of the body over this ends the flight in a crash, m/s².</summary>
        public double CrashAccel;


        /// <summary>An impact's window from its first touch, s: its peak acceleration and impulse are taken over it.</summary>
        public double ImpactWindow;

        /// <summary>A touch closing faster than this starts an impact, m/s; a slower one is a resting or sliding touch.</summary>
        public double ImpactSpeed;

        /// <summary>The stick anchor's tangential stiffness over the normal stiffness (K§7.1 step 5).</summary>
        public double FrictionStiffnessRatio;

        /// <summary>The share of the drone's mass a damping ratio is taken on: c = 2ζ·√(k·m·share).</summary>
        public double DampingMassShare;

        /// <summary>μs and μk of the ground's surfaces (rigid at G7; 7a reads each surface's own).</summary>
        public double GroundStaticFriction, GroundKineticFriction;

        /// <summary>Each leg's spring k (N/m) and damper c (N·s/m), in series with the ground (drone.json's legs).</summary>
        public double LegStiffness, LegDamping;

        /// <summary>
        /// Each leg's give across its tube at the foot (N/m) and that give's damping ratio (p7g-legs 1.2, design 3; its damper
        /// c = 2ζ·√(k·m·share), as a body shape's): the legs stand along body +y, LegStiffness along them, this across, the
        /// stick anchor in series with it, so a gripping foot loads the leg through the tube's bending and a tilted leg pushes
        /// its foot along its tube (a strut). 0 for the law before it (model 37: one spring along the contact's normal, the
        /// stick anchor alone across it).
        /// </summary>
        public double LegSideStiffness, LegSideDampingRatio;

        /// <summary>
        /// The side give's parts and the leg's own along it (p7g-legs 1.3, design 3; derived from the section, 1.1): the tube
        /// bending from the clamp at the foot, 3EI/L³ (N/m); the clamp's jaws' bed turning, at the foot (N/m); the free length's
        /// sway-buckling load π²EI/(4L²), fixed at the clamp, the foot free to sway (N); and the tube's own EA/L along it (N/m).
        /// The axial load P takes the tube's give down by its geometric stiffness, 12P/(π²L) = LegTubeSideStiffness·P/LegSwayLoad,
        /// to 0 at LegSwayLoad and never below (the buckled branch). The jaws' bed turns only as their friction slips (LegDamage),
        /// so the side give with the jaws held is the tube's alone. A crack scales the tube's two stiffnesses by its factor. 0
        /// for 1.2's chain: LegSideStiffness alone, no axial softening, no crack flown.
        /// </summary>
        public double LegTubeSideStiffness, LegJawSideStiffness, LegSwayLoad, LegTubeAxialStiffness;

        /// <summary>
        /// The leg across its tube as a lever (p7g-legs 1.4, the geometric sway; m): the pseudo-rigid-body model's link from its
        /// characteristic pivot to the foot (Howell and Midha 1995: a cantilever with an end force is a rigid link of γL on a
        /// torsional spring, γ ≈ 0.85, K_Θ ≈ 2.65, published), here γL = LegSwayLoad / LegTubeSideStiffness = π²L/12 (γ 0.822,
        /// K_Θ = 3γ = 2.467, derived: the lever keeps 1.1's 3EI/L³ at the foot and the column's π²EI/(4L²) exactly). The foot
        /// swings on the link's circle, so a leaning leg rises off its rest (L − √(δ² + (L − a)²) of its push a), its axial
        /// force acts along the leaned link and its bending (K = k_t·(γL)², by the crack's factor) across it: the axial load's
        /// softening is the link's statics, past the sway load a negative give (the leg vaults the body over its held foot) and
        /// at large leans its stiffening, never clamped at 0. 0 for 1.3's linear softening (model 46, its audit).
        /// </summary>
        public double LegLeverLength;

        /// <summary>
        /// A hanging skid half's whole give is geometry (p7g-legs, model 51): the massless half stands out of what it touches by
        /// its contact spring's share of the force alone, its cantilever's give by its turn about the holding leg's bore and the
        /// rest of its chain (the holding leg's give across its tube and the stick anchor in series, and the cantilever's share
        /// that no turn about the bore gives) by its move along the deepest point's normal; unloaded as its skid leaves. On for
        /// every flight; off only for the audit and the known-wrong against model 50, whose half turned by its cantilever's
        /// share alone, so the holding leg's give was a penetration (a rigid limb passed under a few newtons, a Tunnel).
        /// </summary>
        public bool HangingGive = true;

        /// <summary>The legs' failure law's data (LegDamage, p7g-legs 1.2); null for none, the elastic legs. Needs the side
        /// give.</summary>
        public LegDamageSpec Legs;

        /// <summary>The capsules round a prop disc's rim (review-physics P4).</summary>
        public int RingSegments;

        /// <summary>
        /// The blades meet what their discs strike (p7f-damage 1.3, PropStrikes): each pass takes its work from the rotor, gives
        /// the body its impulses and leaves its damage. On for every flight; off only for an audit against model 29, whose
        /// strikes did nothing (a strike is then logged and does nothing, and, since model 35, never ends the flight).
        /// </summary>
        public bool BladeStrikes = true;

        /// <summary>
        /// The tip slide's physics (p7f-damage 1.3, model 45, PropStrikes): a carve at the blade's speed through the face (its own
        /// and the body's), its axial push the leading face's, a slow pass held to the step, a jammed carve's rest its crush only,
        /// the held blade's plough, the disc's part in a carved face making no thrust, the soil's stones; a grind logged only on a
        /// step that pays the rotor's work. On for every flight; off only for an audit and the known-wrong against model 43's carve
        /// (the blade's own speed, cot θ of the whole impulse, no stones), which a flight with it off steps as.
        /// </summary>
        public bool TipSlide = true;

        /// <summary>Archard's wear coefficient k of the blade sliding on a harder face, V = k·F_n·s/H_b: 3e-3 (assumed, p7f-damage
        /// design 5: inside Lancaster 1969's published 1e-3–1e-2 for polymers abraded by harder asperities, its middle on a log
        /// scale; set by 1.3, which 1.1a left unset).</summary>
        public double WearCoefficient = 3e-3;

        /// <summary>Each blade's strength factor's spread (prop.blade_strength, design 12): σ 5 %, clamped ±12 % (assumed: moulded
        /// glass-filled nylon varies with its fibre orientation and weld lines; no spread is published for props).</summary>
        public double StrengthSigma = 0.05, StrengthBound = 0.12;

        /// <summary>The section's bending form factor on c·t²/6 and t·c²/6: 0.7 (assumed, design 1: an airfoil's against its
        /// bounding rectangle).</summary>
        public double SectionForm = 0.7;

        /// <summary>A section's plastic hinge stiffness over its elastic one, k_p = share × EI/span: 0.1 (assumed, design 5).</summary>
        public double PlasticShare = 0.1;

        /// <summary>The width of a slap's contact along the span, m: 1 mm (assumed, design 5), which caps its peak force at the
        /// tip's local crush, σ_c·chord·width.</summary>
        public double SlapWidth = 0.001;

        /// <summary>The blade's non-rotating first flap mode, Hz, and its Southwell coefficient (RotorFlappingSpec's: 150 Hz
        /// assumed, 1.193 published), for its rotating flap stiffness K_β = I_b·(ω₀² + (K_s − 1)·Ω²) (DroneSpec.BuildContacts).</summary>
        public double FlapFrequency = 150, FlapSouthwell = 1.193;

        /// <summary>
        /// Props are parts at rest (p7f-damage 1.3b, design 16, D-082): a rotor turning slower than BladeRestSpeed carries its
        /// blades as contact shapes (ContactRole.Blade) at their angle, bearing load through the contact model, the load's
        /// moment about the shaft acting on the rotor and its sections bending or breaking by the blade laws (PropStrikes);
        /// faster, its strikes meet what its disc cuts. On for every flight; off only for an audit and the known-wrong (the
        /// prop passed through at rest: model 50's), which a flight with it off steps as.
        /// </summary>
        public bool BladeRest = true;

        /// <summary>The rotor speed under which its blades are contact shapes, rad/s (DroneSpec.BuildContacts, derived: the
        /// speed at which the tip moves its own capsule's radius in a step, so the penalty contact resolves a resting blade's
        /// motion; above it a blade's pass is a strike).</summary>
        public double BladeRestSpeed;

        /// <summary>A resting blade's span segments' outer ends, r/R, from its root (ContactRole.Blade; assumed: the planform's
        /// chord kink at 0.3 R, then three spans; each segment's greatest half-thickness is within 0.7 mm of its least).</summary>
        public static readonly double[] BladeSegmentEnds = { 0.3, 0.55, 0.8, 1.0 };

        /// <summary>The shapes present at the loadout (a part at 0 kg has none).</summary>
        public ContactShape[] Shapes = new ContactShape[0];

        /// <summary>physics_materials.json's rows, and the file's hash (WorldHash's rule, as drone.json's).</summary>
        public MaterialRow[] Materials = new MaterialRow[0];
        public ulong MaterialsHash;

        /// <summary>surfaces.json's table with its hash (p7a-ground 1.1); null for a flight without it.</summary>
        public SurfaceTable Surfaces;

        public ContactSpec Clone()
        {
            var c = (ContactSpec)MemberwiseClone();
            c.Shapes = (ContactShape[])Shapes.Clone();
            c.Materials = (MaterialRow[])Materials.Clone();
            c.Surfaces = Surfaces?.Clone();
            c.Legs = Legs?.Clone();
            return c;
        }

        /// <summary>The row of a world material name, or false.</summary>
        public bool Row(string name, out MaterialRow row)
        {
            foreach (MaterialRow r in Materials)
            {
                if (string.Equals(r.Name, name, StringComparison.Ordinal))
                {
                    row = r;
                    return true;
                }
            }
            row = default;
            return false;
        }
    }
}
