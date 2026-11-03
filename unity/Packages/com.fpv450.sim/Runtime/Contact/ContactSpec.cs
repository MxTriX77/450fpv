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

        /// <summary>A prop disc entering a rigid shape (or the ground) faster than this along the normal is a crash, m/s.</summary>
        public double CrashPropSpeed;

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

        /// <summary>The legs' failure law's data (LegDamage, p7g-legs 1.2); null for none, the elastic legs. Needs the side
        /// give.</summary>
        public LegDamageSpec Legs;

        /// <summary>The capsules round a prop disc's rim (review-physics P4).</summary>
        public int RingSegments;

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
