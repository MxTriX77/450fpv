using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics: "Mass and inertia from the drone's components" (PHB-02, DRN-03), and the drone spec's "Re-derived
    // for letter T's layout". Roll is about body z (forward), pitch about body x (right), yaw about body y (up), all
    // about the centre of mass. Every figure printed is derived: the parallel-axis sum of drone.json's parts.
    public sealed class MassBuildTests
    {
        // ---- the repository's drone.json against the object sheet (docs/object-sheets/drone.md) ----

        // One of the sheet's seven loadouts, with the sheet's own re-derivation of K§5.2 on letter T's layout. L7 is the
        // heaviest corner, on the sheet's pack option B (6S3P twice), which the test puts on in place of the file's packs.
        public sealed class SheetLoadout
        {
            public string Name;
            public bool SixP;
            public double Payload, Coil;                     // kg; the coil's line left, 0–1
            public double Mass, ComY, Roll, Pitch, Yaw, CoilAbove;          // legs on
            public double OffMass, OffRoll, OffPitch, OffYaw, OffCoilAbove; // legs off (the sheet gives no CoM there)

            public override string ToString() => Name;
        }

        static readonly SheetLoadout[] Sheet =
        {
            new SheetLoadout { Name = "L1", Payload = 0, Coil = 0, Mass = 3.46, ComY = -0.016, Roll = 0.039, Pitch = 0.038, Yaw = 0.055, CoilAbove = 0.104, OffMass = 3.34, OffRoll = 0.030, OffPitch = 0.029, OffYaw = 0.052, OffCoilAbove = -0.030 },
            new SheetLoadout { Name = "L2", Payload = 0, Coil = 1, Mass = 4.16, ComY = 0.001, Roll = 0.046, Pitch = 0.048, Yaw = 0.058, CoilAbove = 0.087, OffMass = 4.04, OffRoll = 0.031, OffPitch = 0.032, OffYaw = 0.055, OffCoilAbove = -0.025 },
            new SheetLoadout { Name = "L3", Payload = 1.0, Coil = 0, Mass = 4.46, ComY = -0.037, Roll = 0.051, Pitch = 0.046, Yaw = 0.060, CoilAbove = 0.125, OffMass = 4.34, OffRoll = 0.046, OffPitch = 0.041, OffYaw = 0.057, OffCoilAbove = -0.057 },
            new SheetLoadout { Name = "L4 nominal in flight", Payload = 1.0, Coil = 0.5, Mass = 4.81, ComY = -0.028, Roll = 0.056, Pitch = 0.053, Yaw = 0.062, CoilAbove = 0.116, OffMass = 4.69, OffRoll = 0.048, OffPitch = 0.043, OffYaw = 0.059, OffCoilAbove = -0.053 },
            new SheetLoadout { Name = "L5 nominal take-off", Payload = 1.0, Coil = 1, Mass = 5.16, ComY = -0.020, Roll = 0.061, Pitch = 0.059, Yaw = 0.064, CoilAbove = 0.108, OffMass = 5.04, OffRoll = 0.049, OffPitch = 0.045, OffYaw = 0.060, OffCoilAbove = -0.049 },
            new SheetLoadout { Name = "L6", Payload = 2.0, Coil = 1, Mass = 6.16, ComY = -0.038, Roll = 0.083, Pitch = 0.072, Yaw = 0.076, CoilAbove = 0.126, OffMass = 6.04, OffRoll = 0.073, OffPitch = 0.061, OffYaw = 0.072, OffCoilAbove = -0.070 },
            new SheetLoadout { Name = "L7 heaviest", SixP = true, Payload = 1.4, Coil = 1, Mass = 6.46, ComY = -0.030, Roll = 0.080, Pitch = 0.068, Yaw = 0.082, CoilAbove = 0.118, OffMass = 6.34, OffRoll = 0.070, OffPitch = 0.057, OffYaw = 0.078, OffCoilAbove = -0.051 },
        };

        static IEnumerable<SheetLoadout> SheetLoadouts() => Sheet;

        // The file's spec, or (L7) a fresh copy with the sheet's pack option B: 18 of the same 71 g cells and 8 % for
        // strip and wrap, 3 across, so the box is 0.068 m wide with its inner face where the file's is (x ±0.050).
        static DroneSpec SpecFor(SheetLoadout l)
        {
            if (!l.SixP) return RepositoryDrone.Spec();
            DroneSpec spec = DroneSpec.Parse(File.ReadAllText(TestFiles.Find(RepositoryDrone.RelativePath)));
            foreach (DronePack pack in spec.Packs)
            {
                ref DronePart p = ref spec.Parts[pack.Part];
                double inner = Math.Abs(p.Position.X) - p.Size.X / 2;
                p.Mass = 18 * 0.071 * 1.08;
                p.Size.X = 0.068;
                p.Position.X = Math.Sign(p.Position.X) * (inner + p.Size.X / 2);
            }
            return spec;
        }

        static Loadout At(DroneSpec spec, SheetLoadout l, bool legs) =>
            new Loadout { Payload = l.Payload, Legs = legs, CoilPaidOut = (1 - l.Coil) * spec.Coil.FiberLength };

        // The drone spec's "Re-derived for letter T's layout": each loadout, legs on and legs off, lands on the sheet's
        // figures. Mass to 1 % (the sheet's 70 g cells and 0.30 kg housing, the file's published 71 g and derived
        // 0.31 kg), the centre of mass and the coil's height above it to 2 mm, the inertia to 3 %.
        [TestCaseSource(nameof(SheetLoadouts))]
        public void TheDroneMatchesTheSheetsLoadouts(SheetLoadout l)
        {
            DroneSpec spec = SpecFor(l);
            foreach (bool legs in new[] { true, false })
            {
                MassProperties b = spec.BuildMass(At(spec, l, legs)).Properties;
                Double3 coil = Position(spec, spec.Coil.Part, legs);
                double above = coil.Y - b.CenterOfMass.Y;
                TestContext.WriteLine($"{l.Name}, legs {(legs ? "on " : "off")}: mass {b.Mass:F3} kg, CoM ({b.CenterOfMass.X:F4}, {b.CenterOfMass.Y:F4}, {b.CenterOfMass.Z:F4}) m, "
                    + $"roll {b.Inertia.ZZ:F4} pitch {b.Inertia.XX:F4} yaw {b.Inertia.YY:F4} kg·m², products xy {b.Inertia.XY:F5} xz {b.Inertia.XZ:F5} yz {b.Inertia.YZ:F5}, "
                    + $"coil {above:+0.000;-0.000} m above the CoM (all derived)");

                Assert.That(b.Mass, Is.EqualTo(legs ? l.Mass : l.OffMass).Within(1).Percent, "mass");
                Assert.That(b.Inertia.ZZ, Is.EqualTo(legs ? l.Roll : l.OffRoll).Within(3).Percent, "roll");
                Assert.That(b.Inertia.XX, Is.EqualTo(legs ? l.Pitch : l.OffPitch).Within(3).Percent, "pitch");
                Assert.That(b.Inertia.YY, Is.EqualTo(legs ? l.Yaw : l.OffYaw).Within(3).Percent, "yaw");
                Assert.That(above, Is.EqualTo(legs ? l.CoilAbove : l.OffCoilAbove).Within(0.002), "the coil's height above the CoM");
                if (legs) Assert.That(b.CenterOfMass.Y, Is.EqualTo(l.ComY).Within(0.002), "the CoM's height");
                Assert.That(b.CenterOfMass.X, Is.EqualTo(0).Within(1e-12), "the layout is symmetric across");
            }
        }

        // PHY-12 (p7b-airframe 1.5) on the seven loadouts, legs on and off: the inertia ratios as the real drone.json builds
        // them, and yaw saturating first. Yaw's authority: the same thrust step ΔT on each rotor, in the mixer's pattern for
        // each axis, gives a roll moment Σ ΔT·|x|, a pitch moment Σ ΔT·|z| (the hubs about the CoM) and a yaw moment Σ ΔT·Q/T
        // (the props' torque over thrust at hover speed, derived from drone.json's k_T, torque ratio and static tables); over
        // each axis's inertia, the angular accelerations. Yaw's is the smallest by far, so yaw meets the motors' range first.
        [TestCaseSource(nameof(SheetLoadouts))]
        public void TheInertiaCharacterOnTheSevenLoadouts(SheetLoadout l)
        {
            DroneSpec spec = SpecFor(l);
            DroneProps p = spec.Props;
            foreach (bool legs in new[] { true, false })
            {
                MassProperties b = spec.BuildMass(At(spec, l, legs)).Properties;
                double roll = b.Inertia.ZZ, pitch = b.Inertia.XX, yaw = b.Inertia.YY;
                double hoverW = Math.Sqrt(b.Mass * 9.80665 / 4 / (p.ThrustCoefficient * Table(p.StaticRpm, p.StaticThrustRatio, p.ReferenceRpm)));
                double rpm = hoverW * 60 / (2 * Math.PI);
                double qOverT = p.TorqueRatio * Table(p.StaticRpm, p.StaticTorqueRatio, rpm) / Table(p.StaticRpm, p.StaticThrustRatio, rpm);
                double armX = 0, armZ = 0;
                for (int i = 0; i < 4; i++)
                {
                    armX += Math.Abs(spec.Motors[i].Position.X - b.CenterOfMass.X);
                    armZ += Math.Abs(spec.Motors[i].Position.Z - b.CenterOfMass.Z);
                }
                double aRoll = armX / roll, aPitch = armZ / pitch, aYaw = 4 * qOverT / yaw; // rad/s² a newton of ΔT
                TestContext.WriteLine($"{l.Name}, legs {(legs ? "on " : "off")}: pitch/roll {pitch / roll:F3}, yaw/roll {yaw / roll:F3} "
                    + $"(roll {roll:F4} pitch {pitch:F4} yaw {yaw:F4} kg·m²); a newton of ΔT a rotor: roll {aRoll:F1}, pitch {aPitch:F1}, "
                    + $"yaw {aYaw:F2} rad/s² (Q/T {qOverT * 1000:F2} mm at {rpm:F0} rpm), yaw/roll {aYaw / aRoll:F3} (all derived)");
                Assert.That(aYaw, Is.LessThan(0.2 * aRoll).And.LessThan(0.2 * aPitch), "yaw saturates first");
                if (!legs) continue;
                // PHY-12's ratios as letter T's layout gives them (1.5's finding, handed to the orchestrator): the packs along
                // the sides and the payload crosswise put roll above pitch on six of the seven; yaw/roll is 1.0–1.3 on five.
                bool pitchAbove = pitch > roll, yawInBand = yaw / roll >= 1.0 && yaw / roll <= 1.3;
                TestContext.WriteLine($"  PHY-12: pitch above roll {pitchAbove}, yaw 1.0–1.3 × roll {yawInBand}");
                Assert.That(pitchAbove, Is.EqualTo(l.Name == "L2"), "pitch above roll, as the layout gives it");
                Assert.That(yawInBand, Is.EqualTo(l.Name != "L1" && l.Name != "L6"), "yaw 1.0–1.3 × roll, as the layout gives it");
            }
        }

        // A static table at a speed (rpm), linear, held at its ends.
        static double Table(double[] x, double[] y, double at)
        {
            if (at <= x[0]) return y[0];
            for (int k = 0; k < x.Length - 1; k++)
                if (at < x[k + 1]) return y[k] + (at - x[k]) * (y[k + 1] - y[k]) / (x[k + 1] - x[k]);
            return y[y.Length - 1];
        }

        // The spool above the centre of mass on the legs-on build and below it on the legs-off build (D-043), at every
        // payload of the setting (APP-03: 0–2.0 kg in 0.1 kg steps) and every state of the coil.
        [Test]
        public void TheSpoolIsAboveTheCentreOfMassLegsOnAndBelowItLegsOff()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double onMin = double.MaxValue, onMax = double.MinValue, offMin = double.MaxValue, offMax = double.MinValue;
            for (int tenths = 0; tenths <= 20; tenths++)
            for (int paid = 0; paid <= 2; paid++)
            foreach (bool legs in new[] { true, false })
            {
                var loadout = new Loadout { Payload = tenths / 10.0, Legs = legs, CoilPaidOut = paid / 2.0 * spec.Coil.FiberLength };
                double above = Position(spec, spec.Coil.Part, legs).Y - spec.BuildMass(loadout).Properties.CenterOfMass.Y;
                if (legs) { onMin = Math.Min(onMin, above); onMax = Math.Max(onMax, above); }
                else { offMin = Math.Min(offMin, above); offMax = Math.Max(offMax, above); }
            }
            TestContext.WriteLine($"the coil above the CoM: legs on {onMin:F3}–{onMax:F3} m, legs off {offMin:F3}–{offMax:F3} m (derived)");
            Assert.That(onMin, Is.GreaterThan(0.05), "legs on, the spool is well above the CoM");
            Assert.That(offMax, Is.LessThan(-0.02), "legs off, the spool is below it");
        }

        // What letter T's layout changed from K§5.2 (the sheet): from the class's typical 1 kg payload up, roll exceeds
        // pitch (the crosswise payload and the side packs), where K§5.2 had pitch above roll; with no payload the
        // fore-aft spool keeps pitch level with roll; yaw is 0.9–1.45 × roll; the spool's forward offset puts the CoM
        // forward and leaves a product of inertia I_yz that the build keeps (nothing is balanced to zero).
        [Test]
        public void LetterTsLayoutPutsRollAbovePitchWithAPayload()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (double left in new[] { 0.0, 0.5, 1.0 })
            {
                int lowest = 21; // tenths of a kg: the lowest payload of the run, from 2.0 kg down, with roll above pitch
                for (int tenths = 20; tenths >= 0; tenths--)
                {
                    var loadout = new Loadout { Payload = tenths / 10.0, Legs = true, CoilPaidOut = (1 - left) * spec.Coil.FiberLength };
                    MassProperties b = spec.BuildMass(loadout).Properties;
                    double roll = b.Inertia.ZZ, pitch = b.Inertia.XX, yaw = b.Inertia.YY;
                    if (roll > pitch && lowest == tenths + 1) lowest = tenths;
                    if (loadout.Payload >= 1.0) Assert.That(roll, Is.GreaterThan(pitch), $"roll above pitch at {loadout.Payload} kg");
                    if (loadout.Payload == 0) Assert.That(pitch / roll, Is.InRange(0.97, 1.05), "no payload: pitch level with roll");
                    Assert.That(yaw / roll, Is.InRange(0.9, 1.45), "yaw / roll");
                    Assert.That(b.CenterOfMass.Z, Is.InRange(0.002, 0.011), "the CoM forward of the centre (the spool)");
                    Assert.That(b.Inertia.YZ, Is.InRange(-0.006, -0.0009), "the spool above and forward leaves I_yz");
                }
                TestContext.WriteLine($"coil {left * 100:F0} % full: roll exceeds pitch from {lowest / 10.0:F1} kg of payload up (derived)");
            }
        }

        // The coupling the product of inertia gives, printed for the flight core (1.4): a pure roll moment from rest
        // spins the body up as I⁻¹·M, so it also yaws, by (I⁻¹)_yz / (I⁻¹)_zz of the roll rate, and the principal
        // axes in the roll–yaw plane are turned off the body axes about x.
        [Test]
        public void TheProductOfInertiaCouplesRollIntoYaw()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (SheetLoadout l in Sheet)
            {
                if (l.SixP) continue;
                MassProperties b = spec.BuildMass(At(spec, l, true)).Properties;
                Double3 spin = b.InverseInertia * new Double3(0, 0, 1);
                double ratio = spin.Y / spin.Z;
                double tilt = 0.5 * Math.Atan2(2 * b.Inertia.YZ, b.Inertia.YY - b.Inertia.ZZ) * 180 / Math.PI;
                TestContext.WriteLine($"{l.Name}: a roll moment yaws the body at {ratio:+0.000;-0.000} of its roll rate (−I_yz/I_yy {-b.Inertia.YZ / b.Inertia.YY:+0.000;-0.000}); "
                    + $"the yaw principal axis {tilt:F1}° off body y about x (derived)");
                Assert.That(ratio, Is.EqualTo(-b.Inertia.YZ / b.Inertia.YY).Within(0.1 * Math.Abs(ratio) + 1e-4));
                Assert.That(ratio, Is.InRange(0.01, 0.1), "a few per cent, the sign of the spool above and forward");
            }
        }

        // The payload setting scales the payload part (DRN-03): T's body at the file's 1.0 kg, uniformly by the cube root
        // of the mass ratio, its face toward the frame where it was (the top legs on, the bottom on the upper plate legs off).
        [Test]
        public void ThePayloadScalesWithItsMass()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            DronePart file = spec.Parts[spec.Payload.Part];
            Loadout nominal = spec.Nominal;

            DronePart same = spec.BuildMass(nominal).Part(spec.Payload.Part);
            Assert.That(Bits(same), Is.EqualTo(Bits(file)), "at the file's own mass the part is the file's, bit for bit");

            foreach (bool legs in new[] { true, false })
            {
                Loadout heavy = nominal;
                heavy.Legs = legs;
                heavy.Payload = 2.0;
                Double3 at = Position(spec, spec.Payload.Part, legs);
                DronePart p = spec.BuildMass(heavy).Part(spec.Payload.Part);
                double s = Math.Pow(2.0, 1.0 / 3);
                double r0 = file.Diameter / 2, r = p.Diameter / 2;
                Assert.That(p.Diameter, Is.EqualTo(file.Diameter * s).Within(1e-15));
                Assert.That(p.Length, Is.EqualTo(file.Length * s).Within(1e-15));
                if (legs) Assert.That(p.Position.Y + r, Is.EqualTo(at.Y + r0).Within(1e-15), "legs on, its top stays under the frame");
                else Assert.That(p.Position.Y - r, Is.EqualTo(at.Y - r0).Within(1e-15), "legs off, its bottom stays on the upper plate");
                TestContext.WriteLine($"2.0 kg, legs {(legs ? "on" : "off")}: Ø{p.Diameter:F4} × {p.Length:F4} m, centre y {p.Position.Y:F4} m (derived)");
            }

            Loadout none = nominal;
            none.Payload = 0;
            MassModel empty = spec.BuildMass(none);
            Assert.That(empty.Properties.Mass, Is.EqualTo(spec.BuildMass(nominal).Properties.Mass - file.Mass).Within(1e-12), "0 kg leaves it off");
        }

        // The coil paying out changes one part's mass, and the build follows: the total by the change, the CoM by the
        // lever rule, and the same bits as a model built afresh at that loadout.
        [Test]
        public void APartsMassChangeRebuildsTheBody()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MassModel model = spec.BuildMass(spec.Nominal);
            MassProperties before = model.Properties;
            int coil = spec.Coil.Part;

            double empty = spec.CoilMass(spec.Coil.FiberLength);
            Assert.That(empty, Is.EqualTo(spec.Coil.SpoolMass).Within(1e-15));
            double change = empty - spec.Parts[coil].Mass;
            model.SetPartMass(coil, empty);
            MassProperties after = model.Properties;
            Double3 lever = (spec.Parts[coil].Position - before.CenterOfMass) * (change / after.Mass);
            Assert.That(after.Mass, Is.EqualTo(before.Mass + change).Within(1e-15));
            Assert.That((after.CenterOfMass - (before.CenterOfMass + lever)).Length(), Is.LessThan(1e-15), "the lever rule");
            Assert.That(after.CenterOfMass.Y, Is.LessThan(before.CenterOfMass.Y), "legs on, the CoM sinks as the line on top pays out");

            Loadout paid = spec.Nominal;
            paid.CoilPaidOut = spec.Coil.FiberLength;
            Assert.That(Bits(after), Is.EqualTo(Bits(spec.BuildMass(paid).Properties)), "a rebuild is a build");
        }

        // Legs off is drone.json's legs_off build: the legs, T-joints and skids off, the spool under the bottom plate and
        // the payload on top.
        [Test]
        public void LegsOffIsTheLegsOffBuild()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Loadout off = spec.Nominal;
            off.Legs = false;
            MassModel model = spec.BuildMass(off);
            double removed = 0;
            foreach (int part in spec.LegsOff.Removed)
            {
                removed += spec.Parts[part].Mass;
                Assert.That(model.Part(part).Mass, Is.EqualTo(0));
            }
            foreach (int leg in spec.Legs.Parts)
                Assert.That(Array.IndexOf(spec.LegsOff.Removed, leg), Is.GreaterThanOrEqualTo(0), "every leg comes off");
            TestContext.WriteLine($"legs off: {removed:F3} kg off (derived)");
            Assert.That(removed, Is.EqualTo(0.116).Within(1e-12), "4 legs, 4 T-joints and 2 skids (the sheet: 0.12 kg)");
            Assert.That(model.Properties.Mass, Is.EqualTo(spec.BuildMass(spec.Nominal).Properties.Mass - removed).Within(1e-12));
            Assert.That(model.Part(spec.Coil.Part).Position.Y, Is.LessThan(0), "the spool hangs underneath");
            Assert.That(model.Part(spec.Payload.Part).Position.Y, Is.GreaterThan(0), "the payload rides on top");
        }

        [Test]
        public void ALoadoutOutsideTheSpecIsRefused()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Loadout l = spec.Nominal;
            l.Payload = 2.5;
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.BuildMass(l));
            l = spec.Nominal;
            l.CoilPaidOut = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => spec.BuildMass(l));
        }

        // ---- the bootstrap fixture against K§5.2, and the build itself ----

        [Test]
        public void TheBootstrapDroneMatchesK52()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            MassProperties b = spec.BuildMass(spec.Nominal).Properties;
            double roll = b.Inertia.ZZ, pitch = b.Inertia.XX, yaw = b.Inertia.YY;
            TestContext.WriteLine($"mass {b.Mass:F4} kg, CoM ({b.CenterOfMass.X:F4}, {b.CenterOfMass.Y:F4}, {b.CenterOfMass.Z:F4}) m, "
                + $"roll {roll:F5} pitch {pitch:F5} yaw {yaw:F5} kg·m², yaw/roll {yaw / roll:F3}, "
                + $"products xy {b.Inertia.XY:F5} xz {b.Inertia.XZ:F5} yz {b.Inertia.YZ:F5}");

            Assert.That(b.Mass, Is.EqualTo(4.8).Within(1).Percent, "the mass");
            Assert.That(roll, Is.EqualTo(0.044).Within(5).Percent, "roll inertia");
            Assert.That(pitch, Is.EqualTo(0.054).Within(5).Percent, "pitch inertia");
            Assert.That(yaw, Is.EqualTo(0.050).Within(5).Percent, "yaw inertia");
            Assert.That(pitch, Is.GreaterThan(roll), "pitch above roll: the payload sits forward");
            Assert.That(yaw / roll, Is.InRange(1.0, 1.3), "yaw only 1.0–1.3 × roll");
        }

        // K§5.2's derived centre of mass with a full coil: 1–2 cm below the arm plane, 1–3 cm forward, and the coil's
        // centre 8–9 cm below it, so a side gust on the coil is also a rolling moment.
        [Test]
        public void AFullCoilPutsTheCentreOfMassWhereK52Says()
        {
            DroneSpec spec = BootstrapDrone.Spec();
            Loadout full = spec.Nominal;
            full.CoilPaidOut = 0;
            MassProperties b = spec.BuildMass(full).Properties;
            double coilBelow = b.CenterOfMass.Y - spec.Parts[spec.Coil.Part].Position.Y;
            TestContext.WriteLine($"full coil: mass {b.Mass:F3} kg, CoM {b.CenterOfMass.Y * 100:F2} cm up, {b.CenterOfMass.Z * 100:F2} cm forward, coil {coilBelow * 100:F2} cm below it");
            Assert.That(-b.CenterOfMass.Y, Is.InRange(0.01, 0.02));
            Assert.That(b.CenterOfMass.Z, Is.InRange(0.01, 0.03));
            Assert.That(b.CenterOfMass.X, Is.EqualTo(0).Within(1e-12));
            Assert.That(coilBelow, Is.InRange(0.08, 0.09));
        }

        // The parallel-axis sum against a hand calculation: two 1 kg cubes of 0.1 m, 0.4 m apart along x, and a 2 kg rod
        // along the xz diagonal at the origin.
        [Test]
        public void TheBuildIsTheParallelAxisSum()
        {
            var parts = new[]
            {
                new DronePart { Name = "a", Kind = PartKind.Box, Mass = 1, Position = new Double3(-0.2, 0, 0), Size = new Double3(0.1, 0.1, 0.1) },
                new DronePart { Name = "b", Kind = PartKind.Box, Mass = 1, Position = new Double3(0.2, 0, 0), Size = new Double3(0.1, 0.1, 0.1) },
                new DronePart
                {
                    Name = "rod", Kind = PartKind.Cylinder, Mass = 2, Position = new Double3(0, 0, 0),
                    Diameter = 0.02, Length = 0.6, Axis = new Double3(Math.Sqrt(0.5), 0, Math.Sqrt(0.5)),
                },
            };
            MassProperties b = new MassModel(parts).Properties;

            double cube = 1 * (0.01 + 0.01) / 12;                  // each cube about its own centre
            double across = 2 * (3 * 0.0001 + 0.36) / 12, about = 2 * 0.0001 / 2; // the rod across and about its axis
            Assert.That(b.Mass, Is.EqualTo(4));
            Assert.That(b.CenterOfMass.Length(), Is.LessThan(1e-15));
            Assert.That(b.Inertia.XX, Is.EqualTo(2 * cube + (across + about) / 2).Within(1e-15), "x: the cubes lie on it");
            Assert.That(b.Inertia.YY, Is.EqualTo(2 * cube + 2 * 0.04 + across).Within(1e-15), "y: 2·m·d² and the rod across");
            Assert.That(b.Inertia.ZZ, Is.EqualTo(2 * cube + 2 * 0.04 + (across + about) / 2).Within(1e-15));
            Assert.That(b.Inertia.XZ, Is.EqualTo((about - across) / 2).Within(1e-15), "the rod's product of inertia");
            Assert.That(b.Inertia.XY, Is.EqualTo(0).Within(1e-18));
            Assert.That(b.Inertia.YZ, Is.EqualTo(0).Within(1e-18));
        }

        // A part's centre on the legs-on or the legs-off build, as the file gives it.
        static Double3 Position(DroneSpec spec, int part, bool legs)
        {
            if (!legs)
                for (int i = 0; i < spec.LegsOff.Moved.Length; i++)
                    if (spec.LegsOff.Moved[i] == part)
                        return spec.LegsOff.MovedTo[i];
            return spec.Parts[part].Position;
        }

        static long[] Bits(in MassProperties b) => new[]
        {
            BitConverter.DoubleToInt64Bits(b.Mass), BitConverter.DoubleToInt64Bits(b.CenterOfMass.X),
            BitConverter.DoubleToInt64Bits(b.CenterOfMass.Y), BitConverter.DoubleToInt64Bits(b.CenterOfMass.Z),
            BitConverter.DoubleToInt64Bits(b.Inertia.XX), BitConverter.DoubleToInt64Bits(b.Inertia.YY),
            BitConverter.DoubleToInt64Bits(b.Inertia.ZZ), BitConverter.DoubleToInt64Bits(b.Inertia.XY),
            BitConverter.DoubleToInt64Bits(b.Inertia.XZ), BitConverter.DoubleToInt64Bits(b.Inertia.YZ),
        };

        static long[] Bits(in DronePart p) => new[]
        {
            BitConverter.DoubleToInt64Bits(p.Mass), BitConverter.DoubleToInt64Bits(p.Position.X),
            BitConverter.DoubleToInt64Bits(p.Position.Y), BitConverter.DoubleToInt64Bits(p.Position.Z),
            BitConverter.DoubleToInt64Bits(p.Diameter), BitConverter.DoubleToInt64Bits(p.Length),
            BitConverter.DoubleToInt64Bits(p.Size.X), BitConverter.DoubleToInt64Bits(p.Size.Y), BitConverter.DoubleToInt64Bits(p.Size.Z),
        };
    }
}
