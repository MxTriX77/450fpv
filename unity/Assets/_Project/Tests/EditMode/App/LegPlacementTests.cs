using System.IO;
using Fpv450.App;
using Fpv450.App.Session;
using Fpv450.Sim;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.World;
using NUnit.Framework;
using UnityEngine;
using Part = Fpv450.App.LegPlacement.Part;

namespace Fpv450.Tests.App
{
    // p7g-legs 1.5: the legs as the app draws them (LegPlacement, LegView), against the contact model's own shapes and the
    // loose pieces' builds, on the repository's drone over the floor.
    public sealed class LegPlacementTests
    {
        const int rr = 0, fr = 1, rl = 2, fl = 3; // Corners order
        const double Tolerance = 1e-12; // m and rad: the drawing's sin and cos against DetMath's

        static DroneSpec _spec;
        static MaterialRow[] _materials;
        static ulong _hash;

        [OneTimeSetUp]
        public void LoadDrone()
        {
            _spec = DroneSpec.Parse(File.ReadAllText(AppData.File(AppData.DroneFile)));
            _materials = PhysicsMaterials.Parse(File.ReadAllText(AppData.File(PhysicsMaterials.RelativePath)), out _hash);
        }

        // A flight's core on the floor, its setup changed by adjust; disarmed, the throttle cut.
        static FlightCore Core(ulong seed, System.Action<FlightSetup> adjust = null)
        {
            var inputs = new SessionInputs { Drone = _spec, Materials = _materials, MaterialsHash = _hash, Choices = new FlightChoices { Legs = true }, Adjust = adjust };
            return new FlightCore(SessionSetup.Build(inputs, seed, out _, out _));
        }

        static PoseFrame Pose(FlightCore core)
        {
            core.GetPose(out PoseFrame pose);
            return pose;
        }

        static void Near(Double3 a, Double3 b, string what) => Assert.That((a - b).Length(), Is.LessThan(Tolerance), what + ": " + a + " against " + b);

        // Each shape of corner c's leg tube and skid half as the contact model has placed it, against the drawing's move of its
        // build: the tube by the leg's Leg part, the skid's supports and skid capsule by its Skid part.
        static void ShapesAsDrawn(FlightCore core, in PoseFrame pose, int c)
        {
            ContactModel m = core.Contacts;
            LegDamageSpec legs = m.Spec.Legs;
            int tube = 0, skid = 0;
            for (int i = 0; i < m.ShapeCount; i++)
            {
                ContactShape b = m.Base(i);
                Part? part = b.Role == ContactRole.Body && b.Part == legs.Parts[c] ? Part.Leg
                    : b.Corner == c && (b.Role == ContactRole.Support || b.Role == ContactRole.Skid) ? Part.Skid : (Part?)null;
                if (part == null || m.ShapeOff(i)) continue;
                LegPlacement.Place place = LegPlacement.Of(pose, legs, part.Value, c);
                Assert.That((place.Drawn, place.Slot), Is.EqualTo((true, -1)), b.Name + " drawn on the drone");
                Near(place.Body.Apply(b.A), m.Shape(i).A, b.Name + "'s A");
                Near(place.Body.Apply(b.B), m.Shape(i).B, b.Name + "'s B");
                if (part == Part.Leg) tube++;
                else skid++;
            }
            Assert.That(skid, Is.GreaterThan(0), "corner " + c + "'s skid shapes checked");
            Assert.That(tube, Is.EqualTo((LegPlacement.Corner(pose, c) & (CornerState.Stub | CornerState.TubeGone)) == 0 ? 1 : 0), "and its tube while it is on");
        }

        [Test]
        public void ASetLegIsDrawnWhereItsShapesAre()
        {
            // The front right leg bent across and along and twisted in its clamp (FlightCore's test hook), in the air: its tube
            // and its skid half turn about its clamp's edge as the contact model turns its shapes, by LegDamage.SetRotation.
            FlightCore core = Core(5, s => s.StartPosition = s.StartPosition + new Double3(0, 1, 0));
            var set = new Double3(0.04, -0.03, -0.06);
            core.PresetLeg(fr, set);
            core.Advance(StickSample.Neutral);
            PoseFrame pose = Pose(core);
            LegDamage law = core.Contacts.Legs;
            Near(LegPlacement.SetRotation(pose.LegSet[fr]), law.SetRotation(fr), "the set's rotation is LegDamage.SetRotation");
            Assert.That(pose.LegSet[fr], Is.EqualTo(law.Set(fr)), "the pose carries the set");
            ShapesAsDrawn(core, pose, fr);
            Assert.That(LegPlacement.Of(pose, core.Contacts.Spec.Legs, Part.Stub, fr).Body.Apply(new Double3(1, 2, 3)), Is.EqualTo(new Double3(1, 2, 3)),
                "the stub stays straight in the clamp");
            for (int c = rr; c <= fl; c++)
                if (c != fr)
                    Assert.That(LegPlacement.Of(pose, core.Contacts.Spec.Legs, Part.Leg, c).Body.Apply(new Double3(1, 2, 3)), Is.EqualTo(new Double3(1, 2, 3)),
                        "an unbent leg exactly as built");
        }

        [Test]
        public void AMissingLegsSkidHalfHangsFromTheOtherLeg()
        {
            // The rear left leg off (the test hook) and the front left one bent, in the air: rear left's tube and stub are gone,
            // its skid half hangs from front left's T-joint and is drawn with front left's set, where the contact model has it.
            FlightCore core = Core(5, s => s.StartPosition = s.StartPosition + new Double3(0, 1, 0));
            LegDamageSpec legs = core.Contacts.Spec.Legs;
            Assert.That(legs.Sibling[rl], Is.EqualTo(fl), "rear left's skid half hangs from front left");
            core.PresetLeg(fl, new Double3(-0.05, 0.02, 0.03));
            core.RemoveLeg(rl);
            core.Advance(StickSample.Neutral);
            PoseFrame pose = Pose(core);
            Assert.That(LegPlacement.Corner(pose, rl), Is.EqualTo(CornerState.TubeGone | CornerState.Hanging));
            Assert.That(LegPlacement.Of(pose, legs, Part.Leg, rl).Drawn, Is.False, "its tube gone");
            Assert.That(LegPlacement.Of(pose, legs, Part.Stub, rl).Drawn, Is.False, "with its stub: it slid out of the clamp");
            ShapesAsDrawn(core, pose, rl);
            ShapesAsDrawn(core, pose, fl);

            // A fold turns the half about front left's bore, wherever front left's set has put it.
            pose.SkidFold[rl] = new Double3(0.2, 0, -0.1);
            Rigid held = LegPlacement.Set(pose, legs, fl), half = LegPlacement.SkidHalf(pose, legs, rl);
            Near(half.Apply(legs.Bore[fl]), held.Apply(legs.Bore[fl]), "the bore stays where its leg holds it");
            Near(half.Apply(legs.Foot[rl]) - held.Apply(legs.Bore[fl]), LegPlacement.Turn(pose.SkidFold[rl]).Rotate(held.Apply(legs.Foot[rl]) - held.Apply(legs.Bore[fl])),
                "the rest turned about it by the fold");
        }

        // The summary's flight (SessionTests.TheSummaryNamesTheDamage): dropped level at 1.5 m/s onto a steel kerb 8 mm tall
        // under the rear left foot, seed 14 (seed 3 until model 50, where its rear left holds): rear left parts at its clamp's
        // edge and drops its piece, front right bends.
        static FlightCore Kerb(int steps)
        {
            const double kerb = 0.008;
            FlightCore core = Core(14, s =>
            {
                Double3 foot = s.Contacts.Legs.Foot[rl];
                s.StartPosition = s.StartPosition + new Double3(0, kerb + 0.002, 0);
                s.StartVelocity = new Double3(0, -1.5, 0);
                s.RuntimeObjects = new[]
                {
                    new RuntimeObject("kerb", new[]
                    {
                        new ShapeGeometry
                        {
                            Kind = ShapeKind.Box, Center = new Double3(s.StartPosition.X + foot.X, kerb / 2, s.StartPosition.Z + foot.Z),
                            AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0), AxisZ = new Double3(0, 0, 1),
                            HalfExtents = new Double3(0.05, kerb / 2, 0.015), Material = s.World.Query.MaterialId("steel"),
                        },
                    }),
                };
            });
            for (int k = 0; k < steps; k++) core.Advance(StickSample.Neutral);
            return core;
        }

        [Test]
        public void ABrokenLegKeepsItsStubAndItsPieceIsDrawnWhereItFlies()
        {
            FlightCore core = Kerb(1500);
            PoseFrame pose = Pose(core);
            LegDamageSpec legs = core.Contacts.Spec.Legs;
            Assert.That(LegPlacement.Corner(pose, rl) & CornerState.Stub, Is.EqualTo(CornerState.Stub), "rear left broken at its clamp's edge");
            LegPlacement.Place stub = LegPlacement.Of(pose, legs, Part.Stub, rl);
            Assert.That((stub.Drawn, stub.Slot), Is.EqualTo((true, -1)), "its stub stays in the clamp");
            Assert.That(pose.LegSway[fr], Is.EqualTo(core.Contacts.Legs.Sway(fr)).And.Not.EqualTo(0.0), "the pose carries front right's frame sway");
            ShapesAsDrawn(core, pose, fr); // the bent front right, its skid half turned by its set less its sway

            LegPlacement.Place leg = LegPlacement.Of(pose, legs, Part.Leg, rl), joint = LegPlacement.Of(pose, legs, Part.TJoint, rl);
            Assert.That(leg.Slot, Is.GreaterThanOrEqualTo(0), "its tube below the edge flies in a piece");
            Assert.That(joint.Slot, Is.EqualTo(leg.Slot), "with its T-joint");
            int slot = leg.Slot;
            Assert.That(core.Loose.PartsOf(slot), Is.EqualTo(1 << (4 + rl) | 1 << (8 + rl)), "the piece: the tube below the break and its T-joint");
            Assert.That(pose.Loose[slot].Position, Is.EqualTo(core.Loose.State(slot).Position));

            // The piece's centre of mass in its frame is its parts' as built, turned by the leg's set as it left: the drawing's
            // move of them.
            MassModel built = _spec.BuildMass(new Loadout { Payload = 1.0, Legs = true });
            LoosePieces.Cut(built.Part(legs.Parts[rl]), legs.Clamp[rl], out DronePart lower, out _);
            DronePart tj = built.Part(legs.TJointParts[rl]);
            Double3 centre = (lower.Position * lower.Mass + tj.Position * tj.Mass) * (1 / (lower.Mass + tj.Mass));
            Assert.That((leg.Body.Apply(centre) - core.Loose.Body(slot).CenterOfMass).Length(), Is.LessThan(1e-9), "drawn as it flies");

            // In the drone's frame the piece is where the pose puts it in the world.
            Rigid inBody = LegPlacement.Piece(pose, pose.Loose[slot]).After(leg.Body);
            Double3 world = pose.Position + pose.Attitude.Rotate(inBody.Apply(centre));
            Near(world, pose.Loose[slot].Position + pose.Loose[slot].Attitude.Rotate(core.Loose.Body(slot).CenterOfMass), "the piece's centre in the world");
        }

        [Test]
        public void TheViewMovesTheModelsLegParts()
        {
            // A model with D01's names for the parts (no meshes needed): bound by name, each part moved and shown or hidden by the
            // pose; a part that is not a leg's is left alone; the next flight's model binds afresh, its legs intact.
            FlightCore core = Kerb(1500);
            PoseFrame pose = Pose(core);
            LegDamageSpec legs = core.Contacts.Spec.Legs;
            var model = new GameObject("D01_legs_on");
            try
            {
                Transform Child(string name)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(model.transform, false);
                    return t;
                }
                Transform bentLeg = Child("D01_legs_on_LOD0_leg_fr"), stub = Child("D01_legs_on_LOD1_stub_rl"), lostLeg = Child("D01_legs_on_LOD0_leg_rl"),
                    skid = Child("D01_legs_on_LOD2_skid_rl"), body = Child("D01_legs_on_LOD0"), pack = Child("D01_legs_on_LOD0_coil_pack");
                var view = new LegView();
                view.Bind(model, legs);
                Assert.That(view.Count, Is.EqualTo(4), "the four leg parts, not the body or the coil's pack");
                view.Draw(pose);

                Rigid set = LegPlacement.Set(pose, legs, fr);
                Assert.That((bentLeg.localPosition - V(set.T)).magnitude, Is.LessThan(1e-6f), "the bent leg moved by its set");
                Assert.That(Quaternion.Angle(bentLeg.localRotation, Q(set.Q)), Is.LessThan(1e-3f));
                Assert.That(bentLeg.localRotation, Is.Not.EqualTo(Quaternion.identity), "turned");
                Assert.That(stub.gameObject.activeSelf && stub.localPosition == Vector3.zero, "the broken leg's stub, where it was built");
                LegPlacement.Place lost = LegPlacement.Of(pose, legs, Part.Leg, rl);
                Rigid flies = LegPlacement.Piece(pose, pose.Loose[lost.Slot]).After(lost.Body);
                Assert.That(lostLeg.gameObject.activeSelf && (lostLeg.localPosition - V(flies.T)).magnitude < 1e-5f, "its piece where it flies");
                Assert.That(skid.gameObject.activeSelf, Is.EqualTo(LegPlacement.Of(pose, legs, Part.Skid, rl).Drawn));
                Assert.That((body.localPosition, body.localRotation, pack.localPosition), Is.EqualTo((Vector3.zero, Quaternion.identity, Vector3.zero)), "the rest untouched");

                // R: a new flight's pose, its legs intact; the view bound to it draws every part as built.
                FlightCore again = Core(4);
                again.Advance(StickSample.Neutral);
                foreach (Transform t in new[] { bentLeg, stub, lostLeg, skid })
                {
                    t.localPosition = Vector3.zero;
                    t.localRotation = Quaternion.identity;
                    t.gameObject.SetActive(true);
                }
                view.Bind(model, again.Contacts.Spec.Legs);
                view.Draw(Pose(again));
                foreach (Transform t in new[] { bentLeg, stub, lostLeg, skid })
                    Assert.That((t.gameObject.activeSelf, t.localPosition, t.localRotation), Is.EqualTo((true, Vector3.zero, Quaternion.identity)), t.name + " intact");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        [Test]
        public void TheViewBindsD01sPartsAtEveryLod()
        {
            // The flight's legs-on D01 as FlightScene draws it (an instance under the body): its root in the body's frame, every
            // LOD's leg parts bound (four kinds a corner, no T-joint at LOD3: 60), and each LOD's parts drawn alike: rear left's
            // tube and stub gone and its skid half hanging from front left's set, front left and front right turned by theirs,
            // rear right as built.
            DroneModels models = DroneModels.Load();
            Assert.That(models != null && models.legsOn != null, "ProjectSetup.Apply's D01 prefabs");
            FlightCore core = Core(5, s => s.StartPosition = s.StartPosition + new Double3(0, 1, 0));
            LegDamageSpec legs = core.Contacts.Spec.Legs;
            core.PresetLeg(fl, new Double3(-0.05, 0.02, 0.03));
            core.PresetLeg(fr, new Double3(0.04, -0.03, -0.06));
            core.RemoveLeg(rl);
            core.Advance(StickSample.Neutral);
            PoseFrame pose = Pose(core);
            GameObject model = Object.Instantiate(models.legsOn);
            try
            {
                Assert.That((model.transform.localPosition, model.transform.localRotation, model.transform.localScale), Is.EqualTo((Vector3.zero, Quaternion.identity, Vector3.one)),
                    "the model's root is the body's frame");
                var view = new LegView();
                view.Bind(model, legs);
                Assert.That(view.Count, Is.EqualTo(4 * 4 * 4 - 4), "every LOD's leg parts");
                view.Draw(pose);
                Transform Part(int q, string kind, int c) => model.transform.Find($"{models.legsOn.name}_LOD{q}_{kind}_{LegDamage.Names[c]}");
                void At(Transform t, Rigid move, string what)
                {
                    Assert.That(t.gameObject.activeSelf, what + " drawn");
                    Assert.That((t.localPosition - V(move.T)).magnitude, Is.LessThan(1e-6f), what + " moved");
                    Assert.That(Quaternion.Angle(t.localRotation, Q(move.Q)), Is.LessThan(1e-3f), what + " turned");
                }
                for (int q = 0; q < 4; q++)
                {
                    string lod = "LOD" + q + " ";
                    Assert.That(Part(q, "leg", rl).gameObject.activeSelf || Part(q, "stub", rl).gameObject.activeSelf, Is.False, lod + "rear left's tube and stub gone");
                    At(Part(q, "skid", rl), LegPlacement.SkidHalf(pose, legs, rl), lod + "rear left's skid half, hanging");
                    foreach (int c in new[] { fl, fr })
                    {
                        At(Part(q, "leg", c), LegPlacement.Set(pose, legs, c), lod + LegDamage.Names[c] + "'s leg");
                        At(Part(q, "skid", c), LegPlacement.Set(pose, legs, c), lod + LegDamage.Names[c] + "'s skid half");
                        At(Part(q, "stub", c), Rigid.Identity, lod + LegDamage.Names[c] + "'s stub, straight");
                        if (q < 3) At(Part(q, "tjoint", c), LegPlacement.Set(pose, legs, c), lod + LegDamage.Names[c] + "'s T-joint");
                    }
                    foreach (string kind in LegPlacement.Kinds)
                        if (q < 3 || kind != "tjoint")
                            At(Part(q, kind, rr), Rigid.Identity, lod + "rear right's " + kind + ", as built");
                }
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }

        static Vector3 V(Double3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static Quaternion Q(Quat q) => new Quaternion((float)q.X, (float)q.Y, (float)q.Z, (float)q.W);
    }
}
