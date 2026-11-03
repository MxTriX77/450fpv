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

        /// <summary>What a blade meets (p7f-damage design 5): the face's indentation hardness, Pa, against the blade's (a harder
        /// face is a hard target and the blade yields, a softer one yields to it); the pressure it resists a blade carving it,
        /// Pa, 0 where the file's is null (a thin compliant row a blade passes without damage from it); and its density,
        /// kg/m³, the mass a carving blade flings.</summary>
        public double Hardness, CrushStrength, Density;

        public readonly bool Rigid => Stiffness == 0;
    }

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

        /// <summary>The capsules round a prop disc's rim (review-physics P4).</summary>
        public int RingSegments;

        /// <summary>
        /// The blades meet what their discs strike (p7f-damage 1.3, PropStrikes): each pass takes its work from the rotor, gives
        /// the body its impulses and leaves its damage. On for every flight; off only for an audit against model 29, whose
        /// strikes did nothing (a strike is then logged and does nothing, and, since model 35, never ends the flight).
        /// </summary>
        public bool BladeStrikes = true;

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

        /// <summary>The shapes present at the loadout (a part at 0 kg has none).</summary>
        public ContactShape[] Shapes = new ContactShape[0];

        /// <summary>physics_materials.json's rows, and the file's hash (WorldHash's rule, as drone.json's).</summary>
        public MaterialRow[] Materials = new MaterialRow[0];
        public ulong MaterialsHash;

        public ContactSpec Clone()
        {
            var c = (ContactSpec)MemberwiseClone();
            c.Shapes = (ContactShape[])Shapes.Clone();
            c.Materials = (MaterialRow[])Materials.Clone();
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
