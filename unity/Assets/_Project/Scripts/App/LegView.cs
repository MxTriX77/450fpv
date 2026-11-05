using System;
using System.Collections.Generic;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using UnityEngine;

namespace Fpv450.App
{
    /// <summary>A rigid move of body points, p → Q·p + T.</summary>
    public readonly struct Rigid
    {
        public readonly Quat Q;
        public readonly Double3 T;

        public Rigid(Quat q, Double3 t)
        {
            Q = q;
            T = t;
        }

        public static Rigid Identity => new Rigid(Quat.Identity, default);

        /// <summary>The turn q about a pivot.</summary>
        public static Rigid About(Double3 pivot, Quat q) => new Rigid(q, pivot - q.Rotate(pivot));

        public Double3 Apply(Double3 p) => Q.Rotate(p) + T;

        /// <summary>This move after first: p → this(first(p)).</summary>
        public Rigid After(in Rigid first) => new Rigid(Q * first.Q, Q.Rotate(first.T) + T);
    }

    /// <summary>
    /// Where each leg's drawn parts are (p7g-legs 1.5, design 4 and 8), from the pose alone: per corner the tube in its clamp
    /// above the clamp's edge (Stub), the tube below it (Leg), the T-joint and the skid's half under the leg, as D01 draws
    /// them, each its own mesh per LOD, &lt;variant&gt;_LOD&lt;n&gt;_&lt;kind&gt;_&lt;corner&gt; (Kinds, LegDamage.Names).
    /// As the contact model places the shapes: the leg's set (PoseFrame.LegSet, as LegDamage.Set) turns the tube below the edge,
    /// its T-joint and its skid half about the clamp's edge (LegDamageSpec.Clamp) by LegDamage.SetRotation; a half hanging
    /// from the other T-joint on its skid goes with that leg's set and then turns about that leg's bore (Bore) by its fold
    /// (PoseFrame.SkidFold; the hanging cantilever's elastic give is the contact model's, not drawn). The stub stays straight
    /// in the clamp. A broken leg (CornerState.Stub) keeps its stub; a tube that slid out (TubeGone) takes it; a skid gone
    /// (SkidGone) is gone. A part that left in a piece is drawn in that piece's frame (PoseFrame.Loose: the drone's body frame
    /// as it left), turned by its leg's set as it left (the pose keeps it); a skid in a piece unfolded.
    /// </summary>
    public static class LegPlacement
    {
        public enum Part { Stub, Leg, TJoint, Skid }

        /// <summary>The parts' kinds in their mesh names, in Part order.</summary>
        public static readonly string[] Kinds = { "stub", "leg", "tjoint", "skid" };

        /// <summary>Where a part is: drawn or not; on the drone (Slot −1, Body in the body frame) or in a loose piece's slot
        /// (Body in that piece's frame).</summary>
        public struct Place
        {
            public bool Drawn;
            public int Slot;
            public Rigid Body;
        }

        /// <summary>A leg's set (LegDamage.Set: x and z its bend, y its twist) as the turn of its shapes about the clamp's edge,
        /// a body rotation vector: LegDamage.SetRotation's.</summary>
        public static Double3 SetRotation(Double3 set) => new Double3(-set.Z, set.Y, set.X);

        /// <summary>A rotation vector's turn (its axis, by its length in rad).</summary>
        public static Quat Turn(Double3 rho)
        {
            double t = rho.Length();
            if (!(t > 0)) return Quat.Identity;
            double k = Math.Sin(t / 2) / t;
            return new Quat(rho.X * k, rho.Y * k, rho.Z * k, Math.Cos(t / 2));
        }

        /// <summary>Leg c's set: its tube below the edge, its T-joint and its skid half, about its clamp's edge.</summary>
        public static Rigid Set(in PoseFrame pose, LegDamageSpec legs, int c) => Rigid.About(legs.Clamp[c], Turn(SetRotation(pose.LegSet[c])));

        /// <summary>Corner c's skid half on the drone: with its leg's set, or hanging, with the other leg's set and its fold
        /// about that leg's bore.</summary>
        public static Rigid SkidHalf(in PoseFrame pose, LegDamageSpec legs, int c)
        {
            if ((Corner(pose, c) & CornerState.Hanging) == 0) return Set(pose, legs, c);
            int h = legs.Sibling[c];
            Rigid held = Set(pose, legs, h);
            return Rigid.About(held.Apply(legs.Bore[h]), Turn(pose.SkidFold[c])).After(held);
        }

        /// <summary>A loose piece's frame in the drone's body frame now.</summary>
        public static Rigid Piece(in PoseFrame pose, in LoosePose piece)
        {
            Quat inverse = pose.Attitude.Conjugate();
            return new Rigid(inverse * piece.Attitude, inverse.Rotate(piece.Position - pose.Position));
        }

        public static CornerState Corner(in PoseFrame pose, int c) => (CornerState)(byte)(pose.CornerState >> (8 * c));

        /// <summary>Where corner c's part is in this pose.</summary>
        public static Place Of(in PoseFrame pose, LegDamageSpec legs, Part part, int c)
        {
            int slot = Slot(pose, Bits(legs, part, c));
            if (slot >= 0) return new Place { Drawn = true, Slot = slot, Body = Set(pose, legs, c) };
            CornerState corner = Corner(pose, c);
            bool tube = (corner & (CornerState.Stub | CornerState.TubeGone)) == 0, skid = (corner & CornerState.SkidGone) == 0;
            switch (part)
            {
                case Part.Stub: return new Place { Drawn = (corner & CornerState.TubeGone) == 0, Slot = -1, Body = Rigid.Identity };
                case Part.Leg: return new Place { Drawn = tube, Slot = -1, Body = Set(pose, legs, c) };
                case Part.TJoint:
                    // With its leg; a leg gone without it (its socket broke onto the skid) leaves it on the skid half.
                    return tube
                        ? new Place { Drawn = true, Slot = -1, Body = Set(pose, legs, c) }
                        : new Place { Drawn = skid, Slot = -1, Body = SkidHalf(pose, legs, c) };
                default: return new Place { Drawn = skid, Slot = -1, Body = SkidHalf(pose, legs, c) };
            }
        }

        // The loose pieces' parts mask bits for a part (Contact.LegChange: bit c a leg's whole tube, 4 + c its part below the
        // break, 8 + c a T-joint, 12 the right skid, 13 the left).
        static int Bits(LegDamageSpec legs, Part part, int c)
        {
            switch (part)
            {
                case Part.Stub: return 1 << c;
                case Part.Leg: return 1 << c | 1 << (4 + c);
                case Part.TJoint: return 1 << (8 + c);
                default: return 1 << (legs.Bore[c].X >= 0 ? 12 : 13);
            }
        }

        static int Slot(in PoseFrame pose, int bits)
        {
            for (int i = 0; i < LoosePoses.Slots; i++)
                if ((pose.Loose[i].Parts & bits) != 0)
                    return i;
            return -1;
        }
    }

    /// <summary>
    /// The drawn model's leg parts, moved from each pose (LegPlacement): FlightScene binds a new model's parts by their names
    /// at each flight (so a restart's legs are drawn intact) and draws them at the pose the body is drawn at. A model without
    /// the parts (a D01 built before they were split out) binds none and is drawn as built.
    /// </summary>
    public sealed class LegView
    {
        struct Bound
        {
            public Transform Transform;
            public LegPlacement.Part Part;
            public int Corner;
            public Double3 Position; // its local pose as the model has it (the parts sit at the model's origin)
            public Quat Rotation;
        }

        readonly List<Bound> _parts = new List<Bound>();
        LegDamageSpec _legs;

        /// <summary>The parts bound to the model.</summary>
        public int Count => _parts.Count;

        /// <summary>The model's leg parts for the flight's legs (its contacts' LegDamageSpec); null for none.</summary>
        public void Bind(GameObject model, LegDamageSpec legs)
        {
            _parts.Clear();
            _legs = legs;
            if (model == null || legs == null) return;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                for (int p = 0; p < LegPlacement.Kinds.Length; p++)
                    for (int c = 0; c < LegDamage.Legs; c++)
                        if (t.name.EndsWith("_" + LegPlacement.Kinds[p] + "_" + LegDamage.Names[c], StringComparison.Ordinal))
                        {
                            Vector3 at = t.localPosition;
                            Quaternion turn = t.localRotation;
                            _parts.Add(new Bound
                            {
                                Transform = t, Part = (LegPlacement.Part)p, Corner = c, Position = new Double3(at.x, at.y, at.z),
                                Rotation = new Quat(turn.x, turn.y, turn.z, turn.w),
                            });
                        }
        }

        public void Draw(in PoseFrame pose)
        {
            foreach (Bound b in _parts)
            {
                LegPlacement.Place place = LegPlacement.Of(pose, _legs, b.Part, b.Corner);
                GameObject go = b.Transform.gameObject;
                if (go.activeSelf != place.Drawn) go.SetActive(place.Drawn);
                if (!place.Drawn) continue;
                Rigid body = place.Slot < 0 ? place.Body : LegPlacement.Piece(pose, pose.Loose[place.Slot]).After(place.Body);
                Double3 p = body.Apply(b.Position);
                Quat q = body.Q * b.Rotation;
                b.Transform.localPosition = new Vector3((float)p.X, (float)p.Y, (float)p.Z);
                b.Transform.localRotation = new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
            }
        }
    }
}
