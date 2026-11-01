using System;
using System.Collections.Generic;
using System.IO;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // flight-dynamics: "Mass and inertia from the drone's components" (PHB-02, DRN-03), on D-050's layout: letter T's frame,
    // arms, packs and legs, the coil beneath and a bottle strapped on top. Roll is about body z (forward), pitch about body
    // x (right), yaw about body y (up), all about the centre of mass. Every figure printed is derived: the parallel-axis
    // sum of drone.json's parts.
    public sealed class MassBuildTests
    {
        // ---- the repository's drone.json at the seven loadouts ----

        // One of the object sheet's seven loadouts (docs/object-sheets/drone.md), with D-050's build of it from drone.json,
        // derived and pinned (D-048, D-050: the figures as the file's layout gives them). L7 is the heaviest corner, on the
        // sheet's pack option B (6S3P twice), which the test puts on in place of the file's packs.
        public sealed class BuildLoadout
        {
            public string Name;
            public bool SixP;
            public double Payload, Coil;                                              // kg; the coil's line left, 0–1
            public double Mass, ComY, Roll, Pitch, Yaw, CoilAbove, PitchRoll, YawRoll; // legs on
            public double OffMass, OffComY, OffRoll, OffPitch, OffYaw, OffCoilAbove, OffPitchRoll, OffYawRoll; // legs off

            public override string ToString() => Name;
        }

        static readonly BuildLoadout[] Builds =
        {
            new BuildLoadout { Name = "L1", Payload = 0, Coil = 0,
                Mass = 3.487, ComY = -0.0288, Roll = 0.0356, Pitch = 0.0346, Yaw = 0.0549, CoilAbove = -0.0242, PitchRoll = 0.971, YawRoll = 1.541,
                OffMass = 3.371, OffComY = -0.0229, OffRoll = 0.0302, OffPitch = 0.0289, OffYaw = 0.0522, OffCoilAbove = -0.0301, OffPitchRoll = 0.958, OffYawRoll = 1.727 },
            new BuildLoadout { Name = "L2", Payload = 0, Coil = 1,
                Mass = 4.177, ComY = -0.0328, Roll = 0.0365, Pitch = 0.0375, Yaw = 0.0575, CoilAbove = -0.0202, PitchRoll = 1.027, YawRoll = 1.573,
                OffMass = 4.061, OffComY = -0.0280, OffRoll = 0.0313, OffPitch = 0.0321, OffYaw = 0.0548, OffCoilAbove = -0.0250, OffPitchRoll = 1.023, OffYawRoll = 1.749 },
            new BuildLoadout { Name = "L3", Payload = 1.0, Coil = 0,
                Mass = 4.487, ComY = -0.0030, Roll = 0.0468, Pitch = 0.0487, Yaw = 0.0586, CoilAbove = -0.0500, PitchRoll = 1.041, YawRoll = 1.252,
                OffMass = 4.371, OffComY = 0.0023, OffRoll = 0.0403, OffPitch = 0.0420, OffYaw = 0.0559, OffCoilAbove = -0.0553, OffPitchRoll = 1.041, OffYawRoll = 1.387 },
            new BuildLoadout { Name = "L4 nominal in flight", Payload = 1.0, Coil = 0.5,
                Mass = 4.832, ComY = -0.0066, Roll = 0.0479, Pitch = 0.0508, Yaw = 0.0599, CoilAbove = -0.0464, PitchRoll = 1.061, YawRoll = 1.250,
                OffMass = 4.716, OffComY = -0.0018, OffRoll = 0.0416, OffPitch = 0.0443, OffYaw = 0.0572, OffCoilAbove = -0.0512, OffPitchRoll = 1.064, OffYawRoll = 1.376 },
            new BuildLoadout { Name = "L5 nominal take-off", Payload = 1.0, Coil = 1,
                Mass = 5.177, ComY = -0.0097, Roll = 0.0489, Pitch = 0.0528, Yaw = 0.0612, CoilAbove = -0.0433, PitchRoll = 1.080, YawRoll = 1.251,
                OffMass = 5.061, OffComY = -0.0053, OffRoll = 0.0427, OffPitch = 0.0464, OffYaw = 0.0585, OffCoilAbove = -0.0477, OffPitchRoll = 1.085, OffYawRoll = 1.369 },
            new BuildLoadout { Name = "L6", Payload = 2.0, Coil = 1,
                Mass = 6.177, ComY = 0.0094, Roll = 0.0620, Pitch = 0.0723, Yaw = 0.0693, CoilAbove = -0.0624, PitchRoll = 1.166, YawRoll = 1.118,
                OffMass = 6.061, OffComY = 0.0134, OffRoll = 0.0549, OffPitch = 0.0650, OffYaw = 0.0666, OffCoilAbove = -0.0664, OffPitchRoll = 1.183, OffYawRoll = 1.213 },
            new BuildLoadout { Name = "L7 heaviest", SixP = true, Payload = 1.4, Coil = 1,
                Mass = 6.497, ComY = -0.0074, Roll = 0.0660, Pitch = 0.0656, Yaw = 0.0780, CoilAbove = -0.0456, PitchRoll = 0.993, YawRoll = 1.182,
                OffMass = 6.381, OffComY = -0.0039, OffRoll = 0.0597, OffPitch = 0.0590, OffYaw = 0.0753, OffCoilAbove = -0.0491, OffPitchRoll = 0.988, OffYawRoll = 1.261 },
        };

        static IEnumerable<BuildLoadout> BuildLoadouts() => Builds;

        // The file's spec, or (L7) a fresh copy with the sheet's pack option B: 18 of the same 71 g cells and 8 % for
        // strip and wrap, 3 across, so the box is 0.068 m wide with its inner face where the file's is (x ±0.050).
        static DroneSpec SpecFor(BuildLoadout l)
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

        static Loadout At(DroneSpec spec, BuildLoadout l, bool legs) =>
            new Loadout { Payload = l.Payload, Legs = legs, CoilPaidOut = (1 - l.Coil) * spec.Coil.FiberLength };

        // Each loadout, legs on and legs off, as D-050's drone.json builds it: the figures pinned as printed (mass to the
        // gram, the CoM and the coil's height above it to 0.1 mm, the inertia to 1e-4 kg·m²), so a change of the file is
        // seen and re-derived.
        [TestCaseSource(nameof(BuildLoadouts))]
        public void TheDroneBuildsItsLoadouts(BuildLoadout l)
        {
            DroneSpec spec = SpecFor(l);
            foreach (bool legs in new[] { true, false })
            {
                MassProperties b = spec.BuildMass(At(spec, l, legs)).Properties;
                Double3 coil = Position(spec, spec.Coil.Part, legs);
                double above = coil.Y - b.CenterOfMass.Y;
                TestContext.WriteLine($"{l.Name}, legs {(legs ? "on " : "off")}: mass {b.Mass:F3} kg, CoM ({b.CenterOfMass.X:F4}, {b.CenterOfMass.Y:F4}, {b.CenterOfMass.Z:F4}) m, "
                    + $"roll {b.Inertia.ZZ:F4} pitch {b.Inertia.XX:F4} yaw {b.Inertia.YY:F4} kg·m², products xy {b.Inertia.XY:F5} xz {b.Inertia.XZ:F5} yz {b.Inertia.YZ:F5}, "
                    + $"coil {above:+0.0000;-0.0000} m above the CoM (all derived)");

                Assert.That(b.Mass, Is.EqualTo(legs ? l.Mass : l.OffMass).Within(0.0005), "mass");
                Assert.That(b.CenterOfMass.Y, Is.EqualTo(legs ? l.ComY : l.OffComY).Within(0.00005), "the CoM's height");
                Assert.That(b.Inertia.ZZ, Is.EqualTo(legs ? l.Roll : l.OffRoll).Within(0.00005), "roll");
                Assert.That(b.Inertia.XX, Is.EqualTo(legs ? l.Pitch : l.OffPitch).Within(0.00005), "pitch");
                Assert.That(b.Inertia.YY, Is.EqualTo(legs ? l.Yaw : l.OffYaw).Within(0.00005), "yaw");
                Assert.That(above, Is.EqualTo(legs ? l.CoilAbove : l.OffCoilAbove).Within(0.00005), "the coil's height above the CoM");
                Assert.That(b.CenterOfMass.X, Is.EqualTo(0).Within(1e-12), "the layout is symmetric across");
            }
        }

        // PHY-12 (p7b-airframe 1.5, D-048, D-050) on the seven loadouts, legs on and off: the inertia ratios as the real
        // drone.json builds them, asserted to the third decimal, and yaw saturating first. Yaw's authority: the same thrust
        // step ΔT on each rotor, in the mixer's pattern for each axis, gives a roll moment Σ ΔT·|x|, a pitch moment Σ ΔT·|z|
        // (the hubs about the CoM) and a yaw moment Σ ΔT·Q/T (the props' torque over thrust at hover speed, derived from
        // drone.json's k_T, torque ratio and static tables); over each axis's inertia, the angular accelerations. Yaw's is
        // the smallest by far, so yaw meets the motors' range first.
        [TestCaseSource(nameof(BuildLoadouts))]
        public void TheInertiaCharacterOnTheSevenLoadouts(BuildLoadout l)
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
                Assert.That(pitch / roll, Is.EqualTo(legs ? l.PitchRoll : l.OffPitchRoll).Within(0.0005), "pitch / roll, as the layout gives it");
                Assert.That(yaw / roll, Is.EqualTo(legs ? l.YawRoll : l.OffYawRoll).Within(0.0005), "yaw / roll, as the layout gives it");
                if (!legs) continue;
                // D-050's layout: the bottle fore-aft on top and the coil fore-aft beneath put pitch above roll on five of the
                // seven (L2–L6); the empty drone on an empty coil (L1) and the wide 6S3P packs (L7) keep roll at or above
                // pitch. Yaw/roll is 1.0–1.3 with a bottle aboard (L3–L7) and 1.54–1.57 without one (L1, L2).
                bool pitchAbove = pitch > roll, yawInBand = yaw / roll >= 1.0 && yaw / roll <= 1.3;
                TestContext.WriteLine($"  PHY-12: pitch above roll {pitchAbove}, yaw 1.0–1.3 × roll {yawInBand}");
                Assert.That(pitchAbove, Is.EqualTo(l.Name != "L1" && !l.SixP), "pitch above roll, as the layout gives it");
                Assert.That(yawInBand, Is.EqualTo(l.Payload > 0), "yaw 1.0–1.3 × roll, as the layout gives it");
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

        // The coil below the centre of mass on both builds (D-050), at every payload of the setting (APP-03: 0–2.0 kg in
        // 0.1 kg steps) and every state of the coil.
        [Test]
        public void TheCoilIsBelowTheCentreOfMassOnBothBuilds()
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
            Assert.That(onMax, Is.LessThan(-0.015), "legs on, the coil is below the CoM");
            Assert.That(offMax, Is.LessThan(-0.02), "legs off, the coil is below it");
        }

        // What D-050's layout gives across the setting (APP-03): from 1.0 kg of bottle up pitch exceeds roll at every state
        // of the coil (the bottle and the coil lie fore-aft); with no bottle pitch is level with roll; yaw is 1.1–1.6 × roll;
        // the stack's electronics forward of the centre put the CoM under 1 mm forward and leave a small product of inertia
        // I_yz, which the build keeps (nothing is balanced to zero).
        [Test]
        public void D050sLayoutPutsPitchAboveRollWithABottle()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (double left in new[] { 0.0, 0.5, 1.0 })
            {
                int lowest = 21; // tenths of a kg: the lowest payload of the run, from 2.0 kg down, with pitch above roll
                for (int tenths = 20; tenths >= 0; tenths--)
                {
                    var loadout = new Loadout { Payload = tenths / 10.0, Legs = true, CoilPaidOut = (1 - left) * spec.Coil.FiberLength };
                    MassProperties b = spec.BuildMass(loadout).Properties;
                    double roll = b.Inertia.ZZ, pitch = b.Inertia.XX, yaw = b.Inertia.YY;
                    if (pitch > roll && lowest == tenths + 1) lowest = tenths;
                    if (loadout.Payload >= 1.0) Assert.That(pitch, Is.GreaterThan(roll), $"pitch above roll at {loadout.Payload} kg");
                    if (loadout.Payload == 0) Assert.That(pitch / roll, Is.InRange(0.97, 1.03), "no bottle: pitch level with roll");
                    Assert.That(yaw / roll, Is.InRange(1.1, 1.6), "yaw / roll");
                    Assert.That(b.CenterOfMass.Z, Is.InRange(0.0003, 0.001), "the CoM forward of the centre (the stack)");
                    Assert.That(b.Inertia.YZ, Is.InRange(-0.0002, -0.00001), "the stack above and forward leaves I_yz");
                }
                TestContext.WriteLine($"coil {left * 100:F0} % full: pitch exceeds roll from {lowest / 10.0:F1} kg of bottle up (derived)");
            }
        }

        // The coupling the product of inertia gives, printed for the flight core (1.4): a pure roll moment from rest
        // spins the body up as I⁻¹·M, so it also yaws, by (I⁻¹)_yz / (I⁻¹)_zz of the roll rate, and the principal
        // axes in the roll–yaw plane are turned off the body axes about x.
        [Test]
        public void TheProductOfInertiaCouplesRollIntoYaw()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            foreach (BuildLoadout l in Builds)
            {
                if (l.SixP) continue;
                MassProperties b = spec.BuildMass(At(spec, l, true)).Properties;
                Double3 spin = b.InverseInertia * new Double3(0, 0, 1);
                double ratio = spin.Y / spin.Z;
                double tilt = 0.5 * Math.Atan2(2 * b.Inertia.YZ, b.Inertia.YY - b.Inertia.ZZ) * 180 / Math.PI;
                TestContext.WriteLine($"{l.Name}: a roll moment yaws the body at {ratio:+0.0000;-0.0000} of its roll rate (−I_yz/I_yy {-b.Inertia.YZ / b.Inertia.YY:+0.0000;-0.0000}); "
                    + $"the yaw principal axis {tilt:F2}° off body y about x (derived)");
                Assert.That(ratio, Is.EqualTo(-b.Inertia.YZ / b.Inertia.YY).Within(0.1 * Math.Abs(ratio) + 1e-4));
                Assert.That(ratio, Is.InRange(0.0003, 0.003), "a few tenths of a per cent, the sign of the stack above and forward (D-050)");
            }
        }

        // The payload setting scales the payload part (DRN-03): D-050's bottle at the file's 1.0 kg, uniformly by the cube
        // root of the mass ratio, its face toward the frame where it was: on both builds its underside stays on the stack.
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
                Assert.That(p.Position.Y - r, Is.EqualTo(at.Y - r0).Within(1e-15), "its underside stays on the stack");
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
            Assert.That(after.CenterOfMass.Y, Is.GreaterThan(before.CenterOfMass.Y), "the CoM rises as the line beneath pays out (D-050)");

            Loadout paid = spec.Nominal;
            paid.CoilPaidOut = spec.Coil.FiberLength;
            Assert.That(Bits(after), Is.EqualTo(Bits(spec.BuildMass(paid).Properties)), "a rebuild is a build");
        }

        // Legs off is drone.json's legs_off build: the legs, T-joints and skids off and nothing moved (D-050): the coil
        // stays beneath and the bottle on top.
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
            Assert.That(spec.LegsOff.Moved, Is.Empty, "nothing moves");
            Assert.That(model.Part(spec.Coil.Part).Position.Y, Is.LessThan(0), "the coil hangs underneath");
            Assert.That(model.Part(spec.Payload.Part).Position.Y, Is.GreaterThan(0), "the bottle rides on top");
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
