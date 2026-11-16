using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// What the drone's parts fly through (w16-plants-in-the-air 1.5, design.md "## 1.5"; w9-village-physics 2.10's legs; PHY-05):
    /// the stems on the legs and the body, each a cantilever at its contact on the props' stems' own lean law, and a crown's leaves on
    /// the parts by Newton's impact law (PropStrikes' PartStems). Against the hand calculations of
    /// tools/flightbench/handcalc/part_stems.py; the known-wrong is ContactSpec.PartStems off, model 103's contacts.
    /// </summary>
    public sealed class PartStemTests
    {
        /// <summary>The flat weeds cell the skims fly over (the scan's densest flat weeds cell for stems reaching 0.3 m).</summary>
        static readonly XZ Weeds = new XZ(823.00, 34.60);

        static MapWorld Village => SlopeScenarios.Farmland();

        // The field's stems per m² reaching each height at a cell, green and dry (burnt with the dry): the hand calculation's input.
        static void Field(XZ at, out double[] heights, out double[] green, out double[] dry)
        {
            var patches = new StemPatch[64];
            heights = new double[37];
            green = new double[37];
            dry = new double[37];
            for (int j = 0; j <= 36; j++)
            {
                heights[j] = j * 0.05;
                int n = Village.StemsNear(at, 0.001, heights[j], patches);
                for (int i = 0; i < Math.Min(n, patches.Length); i++)
                {
                    double d = patches[i].Density * patches[i].Area / (Math.PI * 1e-6);
                    if (patches[i].Kind == StemKind.GreenBlade) green[j] += d;
                    else dry[j] += d;
                }
            }
        }

        // The field at the two cells the hand calculation carries (part_stems.py's WEEDS table and the touchdown's count).
        [Test]
        public void TheFieldAtTheCells()
        {
            var g = new GroundSample[1];
            Village.SampleGround(new[] { Weeds }, g);
            Assert.That(Village.SurfaceName(g[0].Surface), Is.EqualTo("weeds"));
            Assert.That(g[0].Normal.Y, Is.GreaterThan(0.995), "flat");
            foreach (XZ at in new[] { Weeds, StemScenarios.Dense })
            {
                Field(at, out double[] h, out double[] green, out double[] dry);
                TestContext.Out.WriteLine($"({at.X:F2}, {at.Z:F2}):");
                for (int j = 0; j < h.Length; j += 3) TestContext.Out.WriteLine($"  h {h[j]:F2} m: green {green[j]:F0}, dry {dry[j]:F0} a m^2");
            }
            Field(Weeds, out _, out double[] gw, out double[] dw);
            Assert.That(gw[6] + dw[6], Is.EqualTo(1077).Within(2), "the weeds cell at 0.3 m (part_stems.py)");
            Assert.That(gw[15] + dw[15], Is.EqualTo(141).Within(2), "the weeds cell at 0.75 m");
            Field(StemScenarios.Dense, out _, out double[] gd, out double[] dd);
            Assert.That(gd[0] + dd[0], Is.EqualTo(2344).Within(2), "the densest tall cell from the ground (part_stems.py)");
        }

        // ---- One stem and a skid, against the cantilever by hand ---------------------------------------------------------

        const double StemX = 3.0, StemZ = 0.17, StemLength = 0.4, StemDiameter = 0.004;

        // A drawn standing stem (the cover row `stem`), upright, ahead of the right skid's front half between its leg and its end.
        static MicroElement OneStem() => new MicroElement
        {
            Id = (9UL << 32) | 1, Kind = MicroKind.Stem, Anchored = true, PointCount = 2, P0 = new Double3(StemX, 0, StemZ),
            P1 = new Double3(StemX, StemLength, StemZ), P2 = new Double3(StemX, StemLength, StemZ), Diameter = StemDiameter, Parent = -1, ParentShape = -1,
        };

        struct Sweep
        {
            public double Work, Impulse, Peak, Height, Speed, Again;
            public int Steps, Groups;
            public long Allocated;
        }

        // The drone hovering with its skids' cores height m up, flying sideways (+x, the right skid ahead) at speed m/s through the one
        // stem: the stem's work on the drone along its motion (J), its impulse (N·s), its largest force (N), the right skid's core
        // over the stem's base and the speed while they touch, the steps they touch and the groups the stem pushed.
        static Sweep SkidThrough(double speed, double height, bool parts = true)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            var world = new FlightWorld(new ElementWorld(new[] { OneStem() }));
            double stand = SoilScenarios.Standing(spec), skid = stand - 0.005; // the skids' cores over the body origin's stand
            var at = new Double3(0, height + skid, 0);
            FlightSetup setup = SoilScenarios.Setup(spec, world, at);
            setup.Contacts.PartStems = parts;
            setup.StartVelocity = new Double3(speed, 0, 0);
            var core = new FlightCore(setup);
            // The parts' law alone: the right front disc's column passes over the stem before the skid, and its fold (2.12's, tested
            // in StemTests) is held off; the stem stands under the disc's plane, so nothing is cut either way.
            core.Strikes.StemLean = false;
            var pilot = new SimPilot(core.Controller, at) { HoldVelocity = true, TargetVelocity = new Double3(speed, 0, 0), ThrottleGain = 0.5, ThrottleDamping = 0.3 };
            Arm(core, pilot);
            var r = new Sweep();
            int skidShape = Array.FindIndex(core.Contacts.Spec.Shapes, s => s.Name == "skid_fr");
            int groups = 0;
            for (int k = 0; k < (int)((StemX + 1.5) / speed / FlightCore.Dt); k++)
            {
                ref readonly FlightState s = ref core.State;
                pilot.Target = new Double3(pilot.Target.X, pilot.Target.Y + 5 * (height + skid - s.Position.Y) * FlightCore.Dt, 0);
                long before = AllocProbe.Before();
                core.Advance(pilot.Sticks(core, s.Step * FlightCore.StepUs));
                if (k > 1000) r.Allocated += AllocProbe.After(before);
                Double3 f = core.Strikes.PartLast[1].Force, others = default; // the right skid's front half's corner
                for (int g = 0; g < 5; g++)
                {
                    PropStrikes.PartRecord p = core.Strikes.PartLast[g];
                    if (g != 1) others = others + p.Force;
                    if (p.Stems > 0) groups |= 1 << g;
                }
                r.Again -= Double3.Dot(others, core.State.Velocity) * FlightCore.Dt;
                if (f.X == 0 && f.Y == 0 && f.Z == 0) continue;
                Double3 v = core.State.Velocity;
                r.Work -= Double3.Dot(f, v) * FlightCore.Dt;
                r.Impulse += f.Length() * FlightCore.Dt;
                r.Peak = Math.Max(r.Peak, f.Length());
                Double3 c = core.State.Position + core.State.Attitude.Rotate((core.Contacts.Shape(skidShape).A + core.Contacts.Shape(skidShape).B) * 0.5);
                r.Height += c.Y;
                r.Speed += v.X;
                r.Steps++;
            }
            if (r.Steps > 0)
            {
                r.Height /= r.Steps;
                r.Speed /= r.Steps;
            }
            for (int g = 0; g < 5; g++)
                if ((groups & 1 << g) != 0) r.Groups++;
            return r;
        }

        // The hand: the stem (the cover row `stem`, its modulus by its Id's hash as PropStems draws it) met by a horizontal capsule of
        // radius r at y over its base moving across it at v, quasi-statically by the law (part_stems.py's push): the least lean that
        // clears the capsule, the spring 3EI/s at the contact's arm, the first-order law's k·τ·φ', the weight's moment, the root's
        // yield; and at the first touch the stem's mass at the contact (min(I/s², μL)) brought to v. Returns the work along the motion.
        static double HandWork(CoverSet set, ulong id, double y, double r, double v, out double peak)
        {
            double u01 = (FlightRandom.Mix64(id) >> 11) * (1.0 / 9007199254740992.0);
            double e = set.ModulusMin + (set.ModulusMax - set.ModulusMin) * u01, d = StemDiameter, di = d * (1 - 2 * set.WallShare);
            double ei = e * Math.PI / 64 * (d * d * d * d - di * di * di * di), mu = set.Density * Math.PI / 4 * (d * d - di * di), L = StemLength;
            double k4 = 4 * ei / L, tau = L * L / (1.875 * 1.875 * Math.Sqrt(ei / mu)), my = PropStrikes.StemYieldStrain * 2 * ei / d;
            double gm = mu * 9.80665 * L * L / 2, re = r + d / 2, inertia = PropStrikes.StemLinkInertia * mu * L * L * L;
            const int steps = 20000;
            double work = 0, last = 0, dx = (2 * L + re) / steps;
            peak = 0;
            for (int i = 0; i <= steps; i++)
            {
                // The pushed stem stays ahead of the part (the side kept): y·sin φ − x·cos φ = r_e; past its tip it slides under.
                double x = -re + i * dx, rr = Math.Sqrt(x * x + y * y);
                if (rr <= re) break;
                double phi = Math.Max(0, Math.Atan2(x, y) + Math.Asin(re / rr)), arm = x * Math.Sin(phi) + y * Math.Cos(phi);
                if (arm > L || phi >= Math.PI / 2) break;
                arm = Math.Max(1e-3, arm);
                double rate = i > 0 ? Math.Max(0, (phi - last) / dx * v) : 0;
                double moment = Math.Min(3 * ei / arm * phi - gm * Math.Sin(phi) + k4 * tau * rate, my), normal = Math.Max(0, moment) / arm;
                if (i > 0) work += normal * Math.Cos(phi) * dx;
                peak = Math.Max(peak, normal);
                last = phi;
            }
            double mass = Math.Min(inertia / (y * y), mu * L);
            return work + mass * v * v;
        }

        // A skid swept broadside through one standing stem pays the stem's bending, its first mode's damping and the impulse of its
        // mass at the contact, the cantilever at the contact by hand (part_stems.py) within 15 %; the known-wrong pays nothing. The
        // stem springs back behind it, and the left skid meets it again (its work printed: a second passage the hand leaves out).
        [TestCase(2.0)]
        [TestCase(5.0)]
        public void ASkidSweptThroughAStemIsTheCantileverByHand(double speed)
        {
            Sweep run = SkidThrough(speed, 0.2), wrong = SkidThrough(speed, 0.2, parts: false);
            CoverSet set = SoilScenarios.Table().Cover[(int)CoverKind.Stem].Set(Moisture.Dry);
            ulong id = FlightRandom.Mix64(OneStem().Id ^ 0x5304_5304_5304_5304UL) | 1;
            double hand = HandWork(set, OneStem().Id, run.Height, 0.005, run.Speed, out double peak);
            TestContext.Out.WriteLine($"{speed} m/s: the stem's work on the right skid {run.Work:F4} J (hand {hand:F4} J at the run's {run.Height:F3} m and {run.Speed:F2} m/s; then on the rest {run.Again:F4} J), "
                + $"impulse {run.Impulse:F4} N.s, peak {run.Peak:F3} N (hand {peak:F3} N), {run.Steps} steps, {run.Groups} group(s), {run.Allocated} B; "
                + $"known-wrong {wrong.Work:F4} J");
            Assert.That(id, Is.Not.Zero);
            Assert.That(run.Groups, Is.InRange(1, 2), "the right skid's front half, then the left skid's");
            Assert.That(run.Work, Is.EqualTo(hand).Within(15).Percent, "the work against the hand");
            Assert.That(run.Allocated, Is.EqualTo(0), "0 B");
            Assert.That(wrong.Steps, Is.EqualTo(0), "model 103's skid skimmed it with no force");
        }

        // ---- The landing in the densest tall cell ------------------------------------------------------------------------

        struct Leg
        {
            public double Peak, Before, After, Late, Impulse, Up;
        }

        // The spec's straw landing into the densest tall cell with the parts' push (StemScenarios.Landing): each leg's (and the body's)
        // stems' force, its largest, its means over the last 0.5 s before the touch, the first 0.5 s after it and the last 0.5 s, its
        // impulse from 1 s before the touch to the end, and the stems' upward share at the touch; printed for the forensics.
        [Test]
        public void ALandingInTheDensestTallCellPrintsEachLegsStems()
        {
            var force = new Double3[5][];
            for (int g = 0; g < 5; g++) force[g] = new Double3[32000];
            int pushes = 0;
            long overflow = 0;
            StemScenarios.Run r = StemScenarios.Landing(parts: true, stepped: core =>
            {
                long k = core.State.Step;
                for (int g = 0; g < 5 && k < 32000; g++) force[g][k] = core.Strikes.PartLast[g].Force;
                for (int i = 0; i < core.Events.Count; i++)
                    if (core.Events[i].Kind == EventKind.PlantPush) pushes++;
                overflow = core.Strikes.PartOverflow;
            });
            Assert.That(r.Touch, Is.GreaterThan(0), "it touches the ground");
            long end = r.Steps, t = r.Touch;
            var legs = new Leg[5];
            string[] names = { "rear right", "front right", "rear left", "front left", "body" };
            for (int g = 0; g < 5; g++)
            {
                for (long k = t - 1000; k < end; k++)
                {
                    double f = force[g][k].Length();
                    legs[g].Peak = Math.Max(legs[g].Peak, f);
                    legs[g].Impulse += f * FlightCore.Dt;
                    if (k < t && k >= t - 500) legs[g].Before += f / 500;
                    if (k >= t && k < t + 500) legs[g].After += f / 500;
                    if (k >= end - 500) legs[g].Late += f / 500;
                }
                legs[g].Up = force[g][t].Y;
                TestContext.Out.WriteLine($"{names[g],-12} peak {legs[g].Peak,7:F3} N; mean {legs[g].Before:F3} N the 0.5 s before the touch, "
                    + $"{legs[g].After:F3} N the 0.5 s after, {legs[g].Late:F3} N the last 0.5 s; impulse {legs[g].Impulse:F4} N.s; up at the touch {legs[g].Up:F3} N");
            }
            TestContext.Out.WriteLine($"touch at step {t} of {end}; {pushes} PlantPush events; {r.Allocated} B after the first second; {r.Refused} events refused; "
                + $"{overflow} stems past the active list");
            Assert.That(r.Allocated, Is.EqualTo(0), "0 B");
            Assert.That(r.Refused, Is.EqualTo(0), "no event refused");
            Assert.That(pushes, Is.GreaterThan(0), "the stems' push is logged");
            Assert.That(overflow, Is.EqualTo(0));
            double legsBefore = 0, legsLate = 0;
            for (int g = 0; g < 4; g++)
            {
                legsBefore += legs[g].Before + legs[g].After;
                legsLate += legs[g].Late;
                Assert.That(legs[g].Peak, Is.LessThan(80), names[g] + ": no more than the thickest stalk's root yields at a skid's reach (part_stems.py)");
            }
            Assert.That(legsBefore, Is.GreaterThan(0.1), "the legs meet the stems through the touchdown");
            Assert.That(legsLate, Is.LessThan(legsBefore), "the stems under the skids lodge: the legs carry less once down");
        }

        // ---- Skimming the weeds, and a crown at the same speed ------------------------------------------------------------

        struct Skim
        {
            public double Stems, Leaves, Twigs, Hand, Height, Speed;
            public int Steps;
        }

        // The drone level, its skids' bottoms height m over the weeds cell's ground, flying along +z (its nose) at speed m/s from 2 m
        // short of the cell to 2 m past it, its height held over the ground under it: the stems' mean drag on its parts along its
        // motion (N) over the 2 m about the cell.
        static Skim SkimWeeds(double speed, double height, bool parts = true, System.Collections.Generic.List<long> ticks = null)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            double stand = SoilScenarios.Standing(spec);
            var start = new XZ(Weeds.X, Weeds.Z - 2 - 0.5 * speed * speed / 3);
            double y = StemScenarios.Ground(start) + height + stand;
            // Started at speed (FlightSetup.StartVelocity), so it is level over the stretch, its height held firmly.
            FlightCore core = StemScenarios.Start(new Double3(start.X, y, start.Z), false, null, out _, parts: parts, velocity: new Double3(0, 0, speed));
            var pilot = new SimPilot(core.Controller, new Double3(start.X, y, start.Z))
            {
                HoldVelocity = true, TargetVelocity = new Double3(0, 0, speed), ThrottleGain = 0.5, ThrottleDamping = 0.3,
            };
            Arm(core, pilot);
            var r = new Skim();
            for (int k = 0; k < (int)((4 + 0.5 * speed * speed / 3) / speed / FlightCore.Dt); k++)
            {
                ref readonly FlightState s = ref core.State;
                double over = s.Position.Y - stand - StemScenarios.Ground(new XZ(s.Position.X, s.Position.Z));
                pilot.Target = new Double3(start.X, pilot.Target.Y + 5 * (height - over) * FlightCore.Dt, pilot.Target.Z);
                StickSample stick = pilot.Sticks(core, s.Step * FlightCore.StepUs);
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                core.Advance(stick);
                if (ticks != null && Math.Abs(core.State.Position.Z - Weeds.Z) <= 1) ticks.Add(System.Diagnostics.Stopwatch.GetTimestamp() - t0);
                if (Math.Abs(core.State.Position.Z - Weeds.Z) > 1) continue;
                r.Height += core.State.Position.Y - stand - StemScenarios.Ground(new XZ(core.State.Position.X, core.State.Position.Z));
                r.Speed += core.State.Velocity.Length();
                Double3 v = core.State.Velocity, f = default;
                for (int g = 0; g < 5; g++) f = f + core.Strikes.PartLast[g].Force;
                r.Stems -= Double3.Dot(f, v) / v.Length();
                r.Steps++;
            }
            r.Stems /= Math.Max(1, r.Steps);
            r.Height /= Math.Max(1, r.Steps);
            r.Speed /= Math.Max(1, r.Steps);
            return r;
        }

        const double CrownCentre = 10, CrownRadius = 3;

        // The drone at 10 m flying along +z at speed m/s through a 3 m GreenBush crown (CrownTests' world): the twigs' (the contacts')
        // and the leaves' (PartStems') mean drag along its motion while the whole drone is inside, and the leaves' by Newton by hand
        // (part_stems.py's report_crown at the bush's leaf area density).
        static Skim ThroughCrown(double speed, bool parts = true, System.Collections.Generic.List<long> ticks = null)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MaterialRow[] rows = ContactScenarios.RepositoryMaterials(out ulong hash);
            var world = new FlightWorld(new FloorWorld(0, "belt_bare", "steel", "timber", "twig", "foliage"));
            var start = new Double3(0, CrownCentre, -6 - 0.5 * speed * speed / 3);
            FlightSetup setup = ContactScenarios.Setup(spec, rows, hash, true, start);
            setup.World = world;
            setup.StartVelocity = new Double3(0, 0, speed);
            Loadout legs = spec.Nominal;
            legs.Legs = true;
            setup.Contacts = spec.BuildContacts(legs, rows, hash, SoilScenarios.Table());
            setup.Contacts.PartStems = parts;
            setup.RuntimeObjects = new[]
            {
                new RuntimeObject("GreenBush", new[]
                {
                    new ShapeGeometry
                    {
                        Kind = ShapeKind.Sphere, Center = new Double3(0, CrownCentre, 0), AxisX = new Double3(1, 0, 0), AxisY = new Double3(0, 1, 0),
                        AxisZ = new Double3(0, 0, 1), Radius = CrownRadius, Material = world.Query.MaterialId("twig"),
                    },
                }),
            };
            var core = new FlightCore(setup);
            CrownRow k = Array.Find(rows, x => x.Name == "twig").Crown;
            var pilot = new SimPilot(core.Controller, start) { HoldVelocity = true, TargetVelocity = new Double3(0, 0, speed), ThrottleGain = 0.5, ThrottleDamping = 0.3 };
            Arm(core, pilot);
            var r = new Skim();
            for (int s = 0; s < (int)((12 + 0.5 * speed * speed / 3) / speed / FlightCore.Dt); s++)
            {
                StickSample stick = pilot.Sticks(core, core.State.Step * FlightCore.StepUs);
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                core.Advance(stick);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                ref readonly FlightState st = ref core.State;
                if ((st.Position - new Double3(0, CrownCentre, 0)).Length() > CrownRadius - 0.6) continue;
                ticks?.Add(t1 - t0);
                Double3 v = st.Velocity, along = v * (1 / v.Length()), leaves = default;
                for (int g = 0; g < 5; g++) leaves = leaves + core.Strikes.PartLast[g].Leaves;
                r.Leaves -= Double3.Dot(leaves, along);
                r.Twigs -= Double3.Dot(core.Trace.ContactForce, along);
                r.Hand += k.LeafAreaDensityOf("GreenBush") * k.LeafFreshMass * NewtonArea(core, v) * v.Length() * v.Length();
                r.Speed += v.Length();
                r.Steps++;
            }
            r.Leaves /= Math.Max(1, r.Steps);
            r.Twigs /= Math.Max(1, r.Steps);
            r.Hand /= Math.Max(1, r.Steps);
            r.Speed /= Math.Max(1, r.Steps);
            return r;
        }

        // The parts' Newtonian area against a velocity (κ·A: ⅔ of a capsule's projected side, ½ of its caps' disc; a sphere inside a
        // capsule of its own part is the capsule's cap), by hand from the contact spec.
        static double NewtonArea(FlightCore core, Double3 v)
        {
            ContactSpec spec = core.Contacts.Spec;
            Double3 dir = core.State.Attitude.InverseRotate(v * (1 / v.Length()));
            double area = 0;
            for (int i = 0; i < spec.Shapes.Length; i++)
            {
                ContactShape s = spec.Shapes[i];
                if (s.Role == ContactRole.Disc || s.Role == ContactRole.Blade) continue;
                bool sphere = s.A.X == s.B.X && s.A.Y == s.B.Y && s.A.Z == s.B.Z, cap = false;
                for (int j = 0; sphere && j < spec.Shapes.Length; j++)
                {
                    ContactShape o = spec.Shapes[j];
                    if (j == i || o.Part != s.Part || o.Role == ContactRole.Disc || o.A.X == o.B.X && o.A.Y == o.B.Y && o.A.Z == o.B.Z) continue;
                    Double3 ab = o.B - o.A;
                    double u = Math.Max(0, Math.Min(1, Double3.Dot(s.A - o.A, ab) / Double3.Dot(ab, ab)));
                    if ((s.A - (o.A + ab * u)).Length() + s.Radius <= o.Radius + 1e-6) cap = true;
                }
                if (cap) continue;
                Double3 core3 = s.B - s.A;
                double l = core3.Length(), sin = l > 0 ? Double3.Cross(core3 * (1 / l), dir).Length() : 0;
                area += PropStrikes.NewtonCylinder * 2 * s.Radius * l * sin * sin + PropStrikes.NewtonSphere * Math.PI * s.Radius * s.Radius;
            }
            return area;
        }

        // Skimming the weeds with the skids in their tops drags the drone (it was nothing before, model 103); a crown at the same speed
        // drags it more: its twigs and its leaves. Both against the hand calculation (part_stems.py): the skim under its upper bound
        // (every strip of the drone meeting fresh stems), the crown's leaves within 25 % of Newton's.
        [TestCase(2.0)]
        [TestCase(5.0)]
        public void SkimmingWeedsDragsAndACrownDragsMore(double speed)
        {
            Skim weeds = SkimWeeds(speed, 0.75), wrong = SkimWeeds(speed, 0.75, parts: false), crown = ThroughCrown(speed);
            double upper = speed < 3 ? 0.58 : 1.39; // part_stems.py: skids 0.75 m up over the weeds cell
            TestContext.Out.WriteLine($"{speed} m/s: weeds {weeds.Stems:F3} N over {weeds.Steps} steps at {weeds.Speed:F2} m/s, the skids {weeds.Height:F3} m up (hand's upper bound {upper:F2} N; known-wrong {wrong.Stems:F3} N); "
                + $"crown: twigs {crown.Twigs:F3} N, leaves {crown.Leaves:F3} N (Newton by hand {crown.Hand:F3} N), together {crown.Twigs + crown.Leaves:F3} N over {crown.Steps} steps at {crown.Speed:F2} m/s");
            Assert.That(weeds.Stems, Is.GreaterThan(0), "the weeds drag the skimming drone");
            Assert.That(weeds.Stems, Is.LessThan(1.2 * upper), "under the hand's upper bound");
            Assert.That(wrong.Stems, Is.EqualTo(0), "model 103 skimmed them with no force");
            Assert.That(crown.Leaves, Is.EqualTo(crown.Hand).Within(25).Percent, "the leaves by Newton");
            Assert.That(crown.Twigs + crown.Leaves, Is.GreaterThan(weeds.Stems), "a crown drags more than skimming weeds at the same speed");
        }

        // Armed over the first 16 steps, the throttle low as the flight controller asks (already moving: FlightSetup.StartVelocity).
        static void Arm(FlightCore core, SimPilot pilot)
        {
            for (int k = 0; k < 16; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = k >= 8 });
        }

        // ---- The step's cost ----------------------------------------------------------------------------------------------

        // The step's own time (Advance alone) with the parts' push and without it (model 103's contacts): over the weeds high (3 m:
        // nothing near), skimming their tops (the skids 0.75 m up at 5 m/s), ploughing them (the skids 0.3 m up at 2 m/s: the body in
        // the stems), and in a crown at 2 m/s; medians and 99th percentiles of each run's steps over the cell. Indicative only: a shared
        // machine, not a quiet one (PLT-04 is QA's).
        [Test]
        public void TheCostNearAndAwayFromPlants()
        {
            double us = 1e6 / System.Diagnostics.Stopwatch.Frequency;
            foreach ((string name, Func<bool, System.Collections.Generic.List<long>, Skim> run) in new (string, Func<bool, System.Collections.Generic.List<long>, Skim>)[]
            {
                ("over the weeds, 3 m up, 2 m/s", (on, t) => SkimWeeds(2, 3, on, t)),
                ("skimming the weeds' tops, the skids 0.75 m up, 5 m/s", (on, t) => SkimWeeds(5, 0.75, on, t)),
                ("ploughing the weeds, the skids 0.3 m up, 2 m/s", (on, t) => SkimWeeds(2, 0.3, on, t)),
                ("through a crown, 2 m/s", (on, t) => ThroughCrown(2, on, t)),
            })
            {
                var with = new System.Collections.Generic.List<long>(4096);
                var without = new System.Collections.Generic.List<long>(4096);
                run(true, with); // both warmed (the JIT's tiers), then each measured
                run(false, without);
                with.Clear();
                without.Clear();
                run(true, with);
                run(false, without);
                with.Sort();
                without.Sort();
                double p50 = with[with.Count / 2] * us, p99 = with[with.Count * 99 / 100] * us;
                double q50 = without[without.Count / 2] * us, q99 = without[without.Count * 99 / 100] * us;
                TestContext.Out.WriteLine($"{name}: with the push p50 {p50:F1} µs, p99 {p99:F1} µs; without p50 {q50:F1} µs, p99 {q99:F1} µs ({with.Count} steps)");
                Assert.That(with.Count, Is.GreaterThan(100));
            }
        }

        // ---- The switch, the log, the replay -----------------------------------------------------------------------------

        // The header records the switch (entry 34); a log without it flew model 103's contacts.
        [TestCase(true)]
        [TestCase(false)]
        public void TheHeaderKeepsTheSwitch(bool on)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = SoilScenarios.Setup(spec, new FlightWorld(new FloorWorld(0, "belt_bare", "steel", "timber")), new Double3(0, 1, 0));
            setup.Contacts.PartStems = on;
            var core = new FlightCore(setup);
            LogHeader back = LogHeader.Read(new MemoryStream(LogHeader.For(setup, core).Encode()));
            Assert.That(back.Contacts.PartStems, Is.EqualTo(on));
        }

        // The landing's parts' push is the same bit for bit flown twice and replayed from its log.
        [Test]
        public void ThePushIsDeterministicAndReplays()
        {
            using var log = new MemoryStream();
            StemScenarios.Run a = StemScenarios.Landing(parts: true, log: log, from: 1.2), b = StemScenarios.Landing(parts: true, from: 1.2);
            Assert.That(a.Steps, Is.EqualTo(b.Steps));
            for (int k = 0; k < a.Steps; k++)
                Assert.That(BitConverter.DoubleToInt64Bits(a.Pitch[k]), Is.EqualTo(BitConverter.DoubleToInt64Bits(b.Pitch[k])), "step " + (k + 1));
            ContactScenarios.RepositoryMaterials(out ulong materials);
            ReplayResult replay = FlightReplay.Run(new MemoryStream(log.ToArray()), StemScenarios.OpenFarmland, RepositoryDrone.Spec().ContentHash, null,
                SoilScenarios.Table().Hash, materialsHash: materials);
            TestContext.Out.WriteLine($"replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(a.Steps));
        }
    }
}
