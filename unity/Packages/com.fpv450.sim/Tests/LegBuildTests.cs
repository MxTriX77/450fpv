using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7g-legs 1.1: the class's legs as data. drone.json's legs block says what the legs are built of (the pilot's
    // "plastic, 25–35 cm, a little soft" in letter T's shape): every value labelled and sourced, the legs' stiffness from
    // their section, the mass build re-asserted, and the landing speeds and roll angles at which each failure mode would
    // start, from the closed forms of design 3.
    public sealed class LegBuildTests
    {
        const double G = 9.80665;

        // ---- every value labelled and loaded ---------------------------------------------------------------------------

        // Each labelled value of the legs block, as its schema path and its text in the file.
        static List<KeyValuePair<string, string>> LegValues(string text)
        {
            int legs = text.IndexOf("\n  \"legs\": {", StringComparison.Ordinal), end = text.IndexOf("\n  \"legs_off\": {", StringComparison.Ordinal);
            Assert.That(legs, Is.GreaterThan(0));
            Assert.That(end, Is.GreaterThan(legs));
            string block = text.Substring(legs, end - legs);
            int tube = block.IndexOf("\"tube\": {", StringComparison.Ordinal), clamp = block.IndexOf("\"clamp\": {", StringComparison.Ordinal),
                tjoint = block.IndexOf("\"tjoint\": {", StringComparison.Ordinal);
            var values = new List<KeyValuePair<string, string>>();
            foreach (Match m in Regex.Matches(block, "\"([a-z_0-9]+)\": (\\{\"value\": [^{}]*\\})"))
            {
                int at = m.Index;
                string owner = at > tjoint ? "legs.tjoint" : at > clamp ? "legs.clamp" : at > tube ? "legs.tube" : "legs";
                values.Add(new KeyValuePair<string, string>(owner + "." + m.Groups[1].Value, m.Groups[2].Value));
            }
            return values;
        }

        [Test]
        public void EveryLegValueIsLabelledAndLoaded()
        {
            string text = RepositoryDrone.Text();
            DroneSpec spec = RepositoryDrone.Spec();
            DroneLegs legs = spec.Legs;
            Assert.That(legs.Tube, Is.Not.Null, "the repository's legs say what they are built of");
            Assert.That(legs.Clamp, Is.Not.Null);
            Assert.That(legs.TJoint, Is.Not.Null);
            Assert.That(legs.Tube.Material, Is.EqualTo("pvc_u"), "the pilot's plastic (D-079), researched: rigid PVC-U");
            Assert.That(legs.Clamp.Material, Is.EqualTo("petg_printed"));
            Assert.That(legs.TJoint.Material, Is.EqualTo("petg_printed"));

            List<KeyValuePair<string, string>> values = LegValues(text);
            Assert.That(values.Count, Is.EqualTo(3 + 14 + 12 + 7), "the legs' 3, the tube's 14, the clamp's 12 and the T-joint's 7");
            foreach (KeyValuePair<string, string> v in values)
            {
                StringAssert.IsMatch("\"label\": \"(pilot|measured|published|derived|assumed)\"", v.Value, v.Key + " is labelled");
                StringAssert.IsMatch("\"note\": \"[^\"]{8,}\"", v.Value, v.Key + " carries its source");
            }

            // A copy with one label, or one note, taken away is refused, naming the field.
            int refused = 0;
            foreach (KeyValuePair<string, string> v in values)
            {
                string unlabelled = Regex.Replace(v.Value, ", \"label\": \"[a-z]+\"", "");
                string unnoted = Regex.Replace(v.Value, ", \"note\": \"[^\"]*\"", "");
                foreach (var cut in new[] { new { Field = unlabelled, Expect = v.Key + ".label: missing" }, new { Field = unnoted, Expect = v.Key + ".note: missing" } })
                {
                    Assert.That(cut.Field, Is.Not.EqualTo(v.Value));
                    int at = IndexIn(text, v.Key, v.Value);
                    string broken = text.Substring(0, at) + cut.Field + text.Substring(at + v.Value.Length);
                    var e = Assert.Throws<DroneSpecException>(() => DroneSpec.Parse(broken), v.Key);
                    Assert.That(string.Join("\n", e.Problems), Does.Contain(cut.Expect));
                    refused++;
                }
            }
            TestContext.Out.WriteLine($"{values.Count} leg values, each labelled and sourced; {refused} copies with a label or a note taken away, each refused naming its field");

            // The tube is the legs' and the skids' stock: their parts are its mass, its diameter, its material.
            foreach (string name in new[] { "leg_rr", "leg_fr", "leg_rl", "leg_fl", "skid_right", "skid_left" })
            {
                DronePart p = spec.Parts[spec.PartIndex(name)];
                Assert.That(p.Material, Is.EqualTo("polymer"), name);
                Assert.That(p.Diameter, Is.EqualTo(legs.Tube.OuterDiameter), name);
                Assert.That(p.Mass, Is.EqualTo(legs.Tube.Mass(p.Length)).Within(0.00005), name + ": ρ·A·ℓ to the tenth of a gram");
            }
            foreach (ContactShape s in spec.Contacts.Shapes)
                if (s.Role == ContactRole.Support || s.Role == ContactRole.Skid || s.Name.StartsWith("leg_", StringComparison.Ordinal))
                    Assert.That(s.Material, Is.EqualTo("polymer"), s.Name);

            // The mass build with the new legs matches the class (D-066, D-069: 4.09 kg ready to fly without a bottle, the
            // coil full; 3.74 kg with it half paid out; the published builds within 2–4 %).
            MassProperties full = spec.BuildMass(new Loadout { Payload = 0, Legs = true, CoilPaidOut = 0 }).Properties;
            MassProperties half = spec.BuildMass(new Loadout { Payload = 0, Legs = true, CoilPaidOut = spec.Coil.FiberLength / 2 }).Properties;
            TestContext.Out.WriteLine($"ready to fly, no bottle: {full.Mass:F4} kg with the coil full (D-069: 4.09), {half.Mass:F4} kg half paid out (3.74)");
            Assert.That(full.Mass, Is.EqualTo(4.09).Within(2).Percent);
            Assert.That(half.Mass, Is.EqualTo(3.74).Within(2).Percent);
        }

        // The field's text, found after its block's start, so a key two blocks share (modulus_pa, torque_n_m) is its own.
        static int IndexIn(string text, string path, string field)
        {
            string[] parts = path.Split('.');
            int at = text.IndexOf("\n  \"legs\": {", StringComparison.Ordinal);
            if (parts.Length == 3) at = text.IndexOf("\"" + parts[1] + "\": {", at, StringComparison.Ordinal);
            at = text.IndexOf("\"" + parts[parts.Length - 1] + "\": " + field, at, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), path);
            return at + parts[parts.Length - 1].Length + 4;
        }

        [Test]
        public void ABuildWithoutBoundsAroundItsValueIsRefused()
        {
            string text = RepositoryDrone.Text();
            string find = "\"yield_stress_bounds_pa\": {\"value\": [5.0e7, 5.8e7]";
            Assert.That(text.Contains(find), Is.True);
            var e = Assert.Throws<DroneSpecException>(() => DroneSpec.Parse(text.Replace(find, "\"yield_stress_bounds_pa\": {\"value\": [5.6e7, 5.8e7]")));
            Assert.That(string.Join("\n", e.Problems), Does.Contain("legs.tube.yield_stress_bounds_pa: must hold yield_stress_pa"));
            find = "\"tjoint\": {";
            int at = text.IndexOf(find, StringComparison.Ordinal);
            e = Assert.Throws<DroneSpecException>(() => DroneSpec.Parse(text.Substring(0, at) + "\"tjoint_\": {" + text.Substring(at + find.Length)));
            Assert.That(string.Join("\n", e.Problems), Does.Contain("legs.tjoint: missing"));
        }

        // ---- the legs' stiffness from their section ---------------------------------------------------------------------

        [Test]
        public void TheLegsStiffnessComesFromTheirSection()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            DroneLegs legs = spec.Legs;
            LegTube t = legs.Tube;
            LegClamp c = legs.Clamp;

            // The free length is the parts' gap: the clamp's bottom to the T-joint's top.
            DronePart clamp = spec.Parts[spec.PartIndex("clamp_rr")], tjoint = spec.Parts[spec.PartIndex("tjoint_rr")];
            double gap = (clamp.Position.Y - clamp.Size.Y / 2) - (tjoint.Position.Y + tjoint.Size.Y / 2);
            Assert.That(t.FreeLength, Is.EqualTo(gap).Within(1e-12), "the clamp's bottom to the T-joint's top");

            // The clamp's mount is the arm the contacts already bend: arm_rr_a's 3EI/L³ out of its plane at r 0.200, over
            // 0.155 m from the body's edge, moved in to the clamp's r.
            ContactShape arm = Array.Find(spec.Contacts.Shapes, s => s.Name == "arm_rr_a");
            DronePart leg = spec.Parts[legs.Parts[0]];
            double r = Math.Sqrt(leg.Position.X * leg.Position.X + leg.Position.Z * leg.Position.Z), a = r - (0.200 - 0.155);
            double armThere = 3 * 50e9 * (0.025 * 0.006 * 0.006 * 0.006 / 12) / (a * a * a);
            Assert.That(c.MountStiffness, Is.EqualTo(armThere).Within(0.1).Percent, "the arm's give at the clamp");
            Assert.That(arm.StiffnessUp * Math.Pow(0.155 / a, 3), Is.EqualTo(armThere).Within(1).Percent, "the same arm as the arm shapes'");

            double ring = t.RingStiffness(legs.TJoint.SkidGrip), jaws = c.JawStiffness, vertical = legs.VerticalStiffness, side = legs.SideStiffness;
            TestContext.Out.WriteLine($"vertical: the tube's EA/L {t.AxialStiffness:F0} N/m, the arm under the clamp {c.MountStiffness:F0} N/m, the skid's ring in the T-joint {ring:F0} N/m, in series {vertical:F0} N/m (derived)");
            TestContext.Out.WriteLine($"side: the tube's 3EI/L³ {t.BendingStiffness:F1} N/m, the jaws' turn {jaws:F1} N·m/rad ({jaws / (t.FreeLength * t.FreeLength):F0} N/m at the foot), in series {side:F1} N/m (derived)");
            Assert.That(legs.Stiffness, Is.EqualTo(vertical).Within(0.1).Percent, "legs.stiffness_n_per_m is the section's");
            // The leg shapes keep model 26's 6600 N/m penetration spring (assumed): the leg's own 262 N/m across, as a
            // penetration, lets a wire past the tube's core under a few newtons, a Tunnel; p7g-legs 1.3 bends the shapes instead.
            foreach (ContactShape s in spec.Contacts.Shapes)
            {
                if (s.Role != ContactRole.Body || !s.Name.StartsWith("leg_", StringComparison.Ordinal)) continue;
                Assert.That(s.Stiffness, Is.EqualTo(6600), s.Name + ": model 26's, until 1.3");
                Assert.That(s.StiffnessUp, Is.EqualTo(6600), s.Name);
            }

            // The damper keeps its ζ 0.2 on the new spring: c = 2ζ·√(k·m/4) on the take-off mass (L5).
            double takeOff = spec.BuildMass(new Loadout { Payload = 1.0, Legs = true, CoilPaidOut = 0 }).Properties.Mass;
            double damping = 2 * 0.2 * Math.Sqrt(legs.Stiffness * takeOff / 4);
            Assert.That(legs.Damping, Is.EqualTo(damping).Within(0.5), "ζ 0.2 on a quarter of " + takeOff.ToString("F3") + " kg");

            // What the section gives beside model 26's assumed numbers, and what the contact model does not carry yet.
            double overhang = (0.225 - 0.005) - (Math.Abs(tjoint.Position.Z) + tjoint.Size.Z / 2);
            double sway = Math.PI * Math.PI * t.Modulus.Value * t.SecondMoment / (4 * t.FreeLength * t.FreeLength);
            TestContext.Out.WriteLine($"model 26: k 10000 N/m (assumed), c 45 N·s/m, the leg shapes 6600 N/m (10/8 mm CFRP); model 37: k {legs.Stiffness:F0} N/m and c {legs.Damping:F0} N·s/m (derived), "
                + $"the leg across its tube {side:F0} N/m (derived, printed), the leg shapes 6600 N/m (assumed, kept until 1.3 bends them)");
            TestContext.Out.WriteLine($"the skid's overhang under its end: 3EI/L³ over {overhang:F4} m = {t.BendingStiffness * Math.Pow(t.FreeLength / overhang, 3):F0} N/m (one spring a corner carries the leg's {legs.Stiffness:F0} there)");
            TestContext.Out.WriteLine($"standing: the four legs sway-buckle (fixed at the clamps, the feet pinned, K = 2) at {sway:F1} N a leg, {sway / (takeOff * G / 4):F2} times its share of the take-off weight");
        }

        // ---- where each failure mode would start --------------------------------------------------------------------------

        // One touching leg in a landing at roll φ: its spring along the vertical, its share of the drone's effective mass
        // and weight at the touching skid, and per newton of ground force its axial load and its moment at the clamp.
        struct Touch
        {
            public bool Slides;
            public double Need, Stiffness, Mass, Weight, Axial, Moment;
        }

        // The leg is a strut with the tube's axial give ka along it and its bending give kb across it, fixed at the clamp. The
        // clamp comes down by δ; the foot sticks while the ground's reaction stays in its friction cone (tan of its tilt
        // from the vertical at most μs), else it slides inboard under μk. Closed forms in + − × ÷ √ and the roll's sine and
        // cosine; the impact's peak by energy, the throttle cut: F = W + √(W² + k·m·v²) (undamped, so the force's upper bound
        // and the speed's lower bound: ζ 0.2 lowers the dynamic peak to 0.82 of it).
        static Touch Land(double rollDegrees, double ka, double kb, double mass, double comY, double roll, double us, double uk)
        {
            double phi = rollDegrees * Math.PI / 180, s = Math.Sin(phi), c = Math.Cos(phi);
            var t = new Touch();
            if (rollDegrees == 0)
            {
                t.Mass = mass / 4;
            }
            else
            {
                double d = 0.1025 * c + (-0.253 - comY) * s; // the touching skid's line from the CoM, across, m
                t.Mass = 1 / (1 / mass + d * d / roll) / 2;
            }
            t.Weight = t.Mass * G;
            t.Need = (ka - kb) * s * c / (ka * c * c + kb * s * s);
            double slide = 0; // the foot's slide over the clamp's drop
            if (t.Need > us)
            {
                t.Slides = true;
                slide = (uk * (ka * c * c + kb * s * s) - (ka - kb) * s * c) / (ka * s * s + kb * c * c - uk * (ka - kb) * s * c);
            }
            double vertical = ka * (slide * s * c + c * c) + kb * (-slide * s * c + s * s);
            t.Stiffness = vertical;
            t.Axial = ka * (slide * s + c) / vertical;
            t.Moment = Math.Abs(kb * (-slide * c + s)) / vertical;
            return t;
        }

        // The touchdown speed whose peak ground force on the leg reaches F; 0 when the weight dropped on it does already.
        static double Speed(double force, in Touch t)
        {
            double q = (force - t.Weight) * (force - t.Weight) - t.Weight * t.Weight;
            return q > 0 ? Math.Sqrt(q / (t.Stiffness * t.Mass)) : 0;
        }

        [Test]
        public void TheFailureOnsetsArePrinted()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            DroneLegs legs = spec.Legs;
            LegTube t = legs.Tube;
            LegClamp c = legs.Clamp;
            MassProperties b = spec.BuildMass(new Loadout { Payload = 1.0, Legs = true, CoilPaidOut = 0 }).Properties;
            double ka = legs.VerticalStiffness, kb = legs.SideStiffness, kt = c.EdgeFactor.Value, ci = t.OuterDiameter / 2;

            // The capacities (design 3), nominal.
            var modes = new[]
            {
                new { Name = "crack (the edge's first yield, σ_y·I/(K_t·c))", Moment = t.YieldMoment / kt, Axial = 0.0 },
                new { Name = "set (σ_y·I/c)", Moment = t.YieldMoment, Axial = 0.0 },
                new { Name = "hinge (σ_y·Z_p, the section fully plastic)", Moment = t.PlasticMoment, Axial = 0.0 },
                new { Name = "fracture (design 3: K_t·M·c/I ≥ σ_u)", Moment = t.UltimateStress.Value * t.SecondMoment / (kt * ci), Axial = 0.0 },
                new { Name = "buckling (20.19·EI/L², fixed and pinned)", Moment = 0.0, Axial = t.BucklingLoad },
            };
            TestContext.Out.WriteLine($"the leg: PVC-U 10/8 mm, L {t.FreeLength} m, k {ka:F0} N/m along and {kb:F0} N/m across; the clamp's preload {c.Preload:F0} N "
                + $"({c.Torque.Value} N·m over K {c.NutFactor.Value} and M{c.BoltDiameter * 1e3:F0}); take-off {b.Mass:F3} kg, CoM y {b.CenterOfMass.Y:F4} m, roll inertia {b.Inertia.ZZ:F4} kg·m²");
            foreach (var m in modes)
                TestContext.Out.WriteLine($"  {m.Name}: " + (m.Axial > 0 ? $"{m.Axial:F0} N along the leg" : $"{m.Moment:F2} N·m at the clamp ({m.Moment / t.FreeLength:F1} N across the foot)"));
            double socket = legs.TJoint.InterlayerStrength.Value * Math.PI * (Math.Pow(legs.TJoint.SocketDiameter, 4) - Math.Pow(t.OuterDiameter, 4)) / (32 * legs.TJoint.SocketDiameter);
            TestContext.Out.WriteLine($"  the clamp lets the tube slide out at a pull of {c.SlipForce:F0} N (a push bears on the arm) and turn about its axis at {c.TwistMoment(t.OuterDiameter):F2} N·m "
                + $"({c.TwistMoment(t.OuterDiameter) / 0.1175:F1} N sideways at a skid's end); the T-joint's socket breaks across its layers at {socket:F2} N·m; no level or rolled landing loads these");

            // The speeds, on the floor's friction (the contacts' ground pair) and frictionless (a slick surface's bound).
            double floorS = spec.Contacts.GroundStaticFriction, floorK = spec.Contacts.GroundKineticFriction;
            foreach (double[] mu in new[] { new[] { floorS, floorK }, new[] { 0.0, 0.0 } })
            {
                TestContext.Out.WriteLine($"touchdown speed (m/s, the throttle cut) at which each mode starts, μs {mu[0]} μk {mu[1]}:");
                foreach (double roll in new[] { 0.0, 10, 20, 30 })
                {
                    Touch touch = Land(roll, ka, kb, b.Mass, b.CenterOfMass.Y, b.Inertia.ZZ, mu[0], mu[1]);
                    string line = $"  roll {roll,2:F0}° ({(touch.Slides ? "slides" : "sticks")}, needs μ {touch.Need:F2}, k {touch.Stiffness:F0} N/m, {touch.Mass:F2} kg a leg):";
                    foreach (var m in modes)
                    {
                        double per = m.Axial > 0 ? touch.Axial : touch.Moment * t.FreeLength;
                        line += per > 1e-9 ? $" {m.Name.Substring(0, m.Name.IndexOf(' '))} {Speed((m.Axial > 0 ? m.Axial : m.Moment) / per, touch):F2}" : $" {m.Name.Substring(0, m.Name.IndexOf(' '))} -";
                    }
                    TestContext.Out.WriteLine(line);
                }
            }
            double tip = Math.Atan(0.1025 / (0.253 + b.CenterOfMass.Y)) * 180 / Math.PI;
            TestContext.Out.WriteLine($"past {tip:F1}° of roll the centre of mass is outside the touching skid: the drone tips over it whatever the legs do");

            // The pilot's normal landing (level, 0.5 m/s, VISION §2.6) starts no mode, even at the strengths' weakest
            // bounds: the leg's softest modulus buckles first.
            var weak = new LegTube
            {
                OuterDiameter = t.OuterDiameter, InnerDiameter = t.InnerDiameter, FreeLength = t.FreeLength,
                Modulus = new LegBound { Value = t.Modulus.Min }, YieldStress = new LegBound { Value = t.YieldStress.Min },
            };
            Touch level = Land(0, ka, kb, b.Mass, b.CenterOfMass.Y, b.Inertia.ZZ, floorS, floorK);
            Assert.That(Speed(weak.BucklingLoad, level), Is.GreaterThan(0.5 * 2), "a level landing at 0.5 m/s buckles no leg, by a factor of 2 in speed");
            Assert.That(level.Moment, Is.EqualTo(0).Within(1e-12), "level, the legs carry it along their tubes");
        }
    }
}
