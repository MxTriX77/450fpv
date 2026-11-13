using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// p7g-legs 1.3: a damaged leg changes the drone (design 4, 5 and 8). The spec's "The build follows a lost leg" and "The next
    /// landing feels a damaged leg", each with its known-wrong.
    /// </summary>
    public class LegChangeTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        static MaterialRow[] Materials(out ulong hash) => ContactScenarios.RepositoryMaterials(out hash);

        // The angular momentum about the world origin, and the linear momentum, of a rigid body of these mass properties in this
        // state (RigidBody's: the state is its frame's origin).
        static void Momentum(in MassProperties b, in FlightState s, out Double3 angular, out Double3 linear)
        {
            Double3 com = s.Position + s.Attitude.Rotate(b.CenterOfMass);
            Double3 v = s.Velocity + s.Attitude.Rotate(Double3.Cross(s.BodyRate, b.CenterOfMass));
            linear = v * b.Mass;
            angular = Double3.Cross(com, linear) + s.Attitude.Rotate(b.Inertia * s.BodyRate);
        }

        static double Relative(Double3 a, Double3 b) => (a - b).Length() / Math.Max(a.Length(), b.Length());

        static double Relative(Sym3 a, Sym3 b)
        {
            double d = Math.Abs(a.XX - b.XX) + Math.Abs(a.YY - b.YY) + Math.Abs(a.ZZ - b.ZZ) + Math.Abs(a.XY - b.XY) + Math.Abs(a.XZ - b.XZ) + Math.Abs(a.YZ - b.YZ);
            return d / (Math.Abs(a.XX) + Math.Abs(a.YY) + Math.Abs(a.ZZ));
        }

        // ---- The build follows a lost leg --------------------------------------------------------------------------------

        [Test]
        public void TheBuildFollowsALostLeg()
        {
            // 1.2's "A leg comes off": seed 11's leg_rr hooks under a steel bar over its overhang as the drone rises at 1.5 times
            // its slip's speed, and slides out of its clamp. At its PieceDropped the build is the remaining parts' mass build,
            // the mass down by the piece's own (the leg and its T-joint: the skid hangs on leg_fr), and the drone and the piece
            // together keep the momentum the drone had. The known-wrong deletes the piece: the momentum it carried is lost.
            MaterialRow[] m = Materials(out ulong h);
            const ulong seed = 11;
            const int leg = 0;
            LegDamageSpec legs = Spec.BuildContacts(Spec.Nominal, m, h).Legs;
            Double3 foot = legs.Foot[leg];
            FlightWorld world = FlightWorld.Floor();
            var t = new LegScenarios.Touch { Velocity = new Double3(0, 6.0, 0), Seed = seed, Seconds = 0.5, Start = new Double3(0, 1, 0) };
            double skidTop = 1 + foot.Y + 0.01 + 0.005;
            t.Objects = new[] { LegScenarios.Box(world, "bar", new Double3(foot.X, skidTop + 0.002 + 0.01, foot.Z - 0.06), new Double3(0.03, 0.01, 0.01)) };
            FlightSetup setup = LegScenarios.Setup(Spec, m, h, t);
            setup.World = world;
            var core = new FlightCore(setup);
            bool dropped = false;
            for (int k = 0; k < 500 && !dropped; k++)
            {
                MassProperties before = core.Body;
                core.Advance(StickSample.Neutral);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind != EventKind.PieceDropped) continue;
                    dropped = true;
                    int slot = e.Index, bits = (int)e.C;
                    TestContext.Out.WriteLine($"step {core.State.Step}: {Spec.Parts[e.Part].Name}'s piece drops, parts 0x{bits:X4}, {e.A * 1000:F2} g at {e.B:F3} m/s");
                    Assert.That(bits, Is.EqualTo(1 << leg | 1 << (8 + leg)), "the leg and its T-joint; its skid hangs on leg_fr");
                    Assert.That((core.Contacts.Corner(leg) & CornerState.Hanging) != 0, "its skid half hangs from leg_fr's T-joint");

                    // The mass: down by the piece's own, its leg and T-joint.
                    MassProperties after = core.Body, piece = core.Loose.Body(slot);
                    double parts = Spec.Parts[legs.Parts[leg]].Mass + Spec.Parts[legs.TJointParts[leg]].Mass;
                    double fall = before.Mass - after.Mass;
                    TestContext.Out.WriteLine($"  mass {before.Mass:R} → {after.Mass:R} kg: down {fall:R}, the leg and T-joint {parts:R}, the piece {piece.Mass:R}");
                    Assert.That(piece.Mass, Is.EqualTo(parts), "the piece weighs its parts");
                    Assert.That(Math.Abs(fall - parts), Is.LessThanOrEqualTo(2 * Math.Pow(2, -52) * before.Mass), "the mass falls by the piece's, to the sum's rounding");

                    // The remaining parts' own mass build.
                    MassModel fresh = Spec.BuildMass(Spec.Nominal);
                    fresh.SetPartMass(legs.Parts[leg], 0);
                    fresh.SetPartMass(legs.TJointParts[leg], 0);
                    double com = Relative(after.CenterOfMass, fresh.Properties.CenterOfMass), inertia = Relative(after.Inertia, fresh.Properties.Inertia);
                    TestContext.Out.WriteLine($"  against the remaining parts' build: mass {after.Mass - fresh.Properties.Mass:E2} kg, centre {com:E2}, inertia {inertia:E2} relative");
                    Assert.That(after.Mass, Is.EqualTo(fresh.Properties.Mass).Within(1e-12 * after.Mass));
                    Assert.That(com, Is.LessThanOrEqualTo(1e-12));
                    Assert.That(inertia, Is.LessThanOrEqualTo(1e-12));

                    // The momentum through the change: the drone's before against the drone's and the piece's after.
                    ref readonly FlightState s = ref core.State;
                    Momentum(before, s, out Double3 h0, out Double3 p0);
                    Momentum(after, s, out Double3 h1, out Double3 p1);
                    Momentum(piece, core.Loose.State(slot), out Double3 h2, out Double3 p2);
                    double angular = Relative(h0, h1 + h2), linear = Relative(p0, p1 + p2), lost = Relative(h0, h1);
                    TestContext.Out.WriteLine($"  momentum kept: angular {angular:E2}, linear {linear:E2} relative; the known-wrong (the piece deleted) loses {lost:E2} of the angular");
                    Assert.That(angular, Is.LessThanOrEqualTo(1e-12), "angular momentum kept");
                    Assert.That(linear, Is.LessThanOrEqualTo(1e-12), "linear momentum kept");
                    Assert.That(lost, Is.GreaterThan(1e-6), "the known-wrong loses the piece's");
                }
            }
            Assert.That(dropped, "the leg comes off and drops");

            // The pieces fly and rest on the floor (the side record's); their contacts are their own. 15 s (9 s to model 51): on
            // the skid's frame (model 54) the later breaks move the flight, and the left skid's piece (leg_fl's lower tube, its
            // T-joint and the skid) rolls and slides on the floor at about 0.12 m/s until it rests between 9 and 12 s.
            for (int k = 0; k < 15000; k++) core.Advance(StickSample.Neutral);
            for (int i = 0; i < LoosePieces.Slots; i++)
            {
                if (!core.Loose.Active(i)) continue;
                ref readonly FlightState rest = ref core.Loose.State(i);
                Double3 com = rest.Position + rest.Attitude.Rotate(core.Loose.Body(i).CenterOfMass);
                TestContext.Out.WriteLine($"  piece {i} (parts 0x{core.Loose.PartsOf(i):X4}, {core.Loose.Body(i).Mass * 1000:F1} g) {(core.Loose.AtRest(i) ? "rests" : "moves")} "
                    + $"from step {rest.Step}, its centre at {com} m");
                Assert.That(core.Loose.AtRest(i), "it comes to rest on the floor");
                Assert.That(com.Y, Is.LessThan(0.03), "on the floor");
            }
        }

        [Test]
        public void TheAirframesModesAreSolvedAgainForALostLeg()
        {
            // A powered body with the airframe's modes: a leg off, the modes are a fresh solve on the rebuilt build, bit for bit.
            MaterialRow[] m = Materials(out ulong h);
            FlightSetup setup = ContactScenarios.Setup(Spec, m, h, true, new Double3(0, 1, 0));
            setup.Modes = Spec.BuildModes();
            var core = new FlightCore(setup);
            var hubs = new Double3[4];
            for (int i = 0; i < 4; i++) hubs[i] = setup.Propulsion.Motors[i].Position;
            double before = core.Modes.Frequency(0);
            core.RemoveLeg(1);
            var fresh = new AirframeModes(setup.Modes, CopyOf(core), hubs);
            Assert.That(core.Modes.Count, Is.EqualTo(fresh.Count));
            for (int i = 0; i < fresh.Count; i++)
                Assert.That(BitConverter.DoubleToInt64Bits(core.Modes.Frequency(i)), Is.EqualTo(BitConverter.DoubleToInt64Bits(fresh.Frequency(i))), $"mode {i}");
            TestContext.Out.WriteLine($"leg_fr off: the lowest mode {before:F3} → {core.Modes.Frequency(0):F3} Hz, a fresh solve's {fresh.Frequency(0):F3} Hz");
        }

        // ---- A hanging half against a rigid shape (model 51) -------------------------------------------------------------
        //
        // The crown in Windy's mechanism at model 50 (InTheWindNoTunnelThroughARigidShape): the left skid's front half hangs from
        // leg_rl's T-joint once leg_fl is off, and the drone carries it across into a rigid shape at about 2.3 m/s. Its force is
        // its chain's, the contact spring, the skid's cantilever, the holding leg's give across its tube and the stick anchor in
        // series (about 64 N/m across), and at 50 only the cantilever's share moved its shapes: the rest was a penetration, 0.115 m
        // into V01_L's bark under 9 N, past the limb's middle, a Tunnel. Here the free body, leg_fl off, moves at 2.3 m/s to its
        // left into a 10 mm steel rod standing 60 mm inboard of the half's front end, across it; the half's penetration into it
        // is measured from the shapes' own geometry against the rod's box, each step (400 points along each core). With the
        // half's whole give geometric (ContactSpec.HangingGive) it stands out of the rod by its contact spring's share and a
        // step's travel, under its own radius, as leg_rl cracks and sets and the half folds at its bore; the known-wrong, model
        // 50's turn alone, passes the rod's middle and logs its Tunnel.
        sealed class HalfPush
        {
            public int Tunnels, Touches, Steps;
            public double Deepest;
            public long Allocated; // by the steps from the 10th, B
            public readonly List<string> Order = new List<string>();
        }

        static HalfPush PushTheHangingHalf(bool give)
        {
            MaterialRow[] m = Materials(out ulong h);
            const int leg = 3; // leg_fl: its half of the left skid hangs from leg_rl's T-joint
            var start = new Double3(0, 1, 0);
            ContactSpec probe = Spec.BuildContacts(Spec.Nominal, m, h);
            ContactShape end = default;
            foreach (ContactShape s in probe.Shapes)
                if (s.Corner == leg && s.Role == ContactRole.Support && s.A.Z > end.A.Z) end = s;
            const double half = 0.005, gap = 0.004;
            var rod = new Double3(start.X + end.A.X - end.Radius - half - gap, start.Y + end.A.Y, start.Z + end.A.Z - 0.06);
            var extent = new Double3(half, 0.05, half);
            FlightWorld world = FlightWorld.Floor();
            var t = new LegScenarios.Touch { Velocity = new Double3(-2.3, 0, 0), Start = start, Edit = c => { c.HangingGive = give; return c; } };
            t.Objects = new[] { LegScenarios.Box(world, "rod", rod, extent) };
            FlightSetup setup = LegScenarios.Setup(Spec, m, h, t);
            setup.World = world;
            var core = new FlightCore(setup);
            core.RemoveLeg(leg);
            Assert.That((core.Contacts.Corner(leg) & CornerState.Hanging) != 0, "the half hangs from leg_rl's T-joint");
            int id = core.RuntimeIds[0];
            var r = new HalfPush();
            for (int k = 0; k < 150; k++)
            {
                long before = AllocProbe.Before();
                core.Advance(StickSample.Neutral);
                if (k >= 10) r.Allocated += AllocProbe.After(before);
                ref readonly FlightState s = ref core.State;
                for (int i = 0; i < core.Events.Count; i++)
                {
                    FlightEvent e = core.Events[i];
                    if (e.Kind == EventKind.Tunnel) r.Tunnels++;
                    if (e.Kind == EventKind.ContactMade && e.Object == id && core.Contacts.Shape(e.Index).Corner == leg) r.Touches++;
                    if (e.Kind == EventKind.Tunnel || e.Kind >= EventKind.LegSet && e.Kind <= EventKind.SkidLoose)
                        r.Order.Add($"step {s.Step}: {e.Kind} {Spec.Parts[e.Part].Name}" + (e.Kind == EventKind.Tunnel ? $" ({e.A * 1e3:F1} mm deep)" : $" ({(LegMode)e.Index})"));
                }
                if ((core.Contacts.Corner(leg) & CornerState.Hanging) == 0) break; // its skid left with leg_rl
                r.Steps = k + 1;
                for (int i = 0; i < core.Contacts.ShapeCount; i++)
                {
                    ContactShape c = core.Contacts.Shape(i);
                    if (c.Corner != leg || core.Contacts.ShapeOff(i) || c.Role != ContactRole.Support && c.Role != ContactRole.Skid) continue;
                    for (int n = 0; n <= 400; n++)
                    {
                        Double3 p = s.Position + s.Attitude.Rotate(c.A + (c.B - c.A) * (n / 400.0));
                        r.Deepest = Math.Max(r.Deepest, c.Radius - BoxDistance(p - rod, extent));
                    }
                }
            }
            return r;
        }

        // A point's signed distance to an axis-aligned box about the origin (negative inside), m.
        static double BoxDistance(Double3 p, Double3 h)
        {
            double x = Math.Abs(p.X) - h.X, y = Math.Abs(p.Y) - h.Y, z = Math.Abs(p.Z) - h.Z;
            double ox = Math.Max(x, 0), oy = Math.Max(y, 0), oz = Math.Max(z, 0);
            return Math.Sqrt(ox * ox + oy * oy + oz * oz) + Math.Min(Math.Max(x, Math.Max(y, z)), 0);
        }

        [Test]
        public void AHangingHalfStaysOutOfARigidShape()
        {
            HalfPush on = PushTheHangingHalf(true), off = PushTheHangingHalf(false);
            foreach ((string name, HalfPush r) in new[] { ("its whole give geometric", on), ("model 50's turn alone (the known-wrong)", off) })
            {
                TestContext.Out.WriteLine($"{name}: {r.Touches} touches of the rod, deepest {r.Deepest * 1e3:F2} mm into it over {r.Steps} steps hanging, {r.Tunnels} tunnels, "
                    + $"{r.Allocated} B allocated by the steps");
                foreach (string line in r.Order) TestContext.Out.WriteLine("  " + line);
            }
            Assert.That(on.Touches, Is.GreaterThan(0), "the half meets the rod");
            Assert.That(on.Tunnels, Is.Zero, "no tunnel");
            Assert.That(on.Deepest, Is.LessThan(0.005), "it stands out of the rod by its contact spring's share and a step's travel, under its radius");
            Assert.That(on.Allocated, Is.Zero, "0 B a step with a half hanging and giving" + AllocProbe.Report());
            Assert.That(off.Touches, Is.GreaterThan(0));
            Assert.That(off.Tunnels, Is.GreaterThan(0), "the known-wrong passes the rod's middle: a Tunnel");
        }

        // The flight's mass model as it is now, from its parts.
        static MassModel CopyOf(FlightCore core)
        {
            MassModel spec = Spec.BuildMass(Spec.Nominal);
            var parts = new DronePart[spec.PartCount];
            for (int i = 0; i < parts.Length; i++) parts[i] = spec.Part(i);
            var model = new MassModel(parts);
            LegDamageSpec l = core.Contacts.Spec.Legs;
            model.SetPartMass(l.Parts[1], 0);
            model.SetPartMass(l.TJointParts[1], 0);
            Assert.That(model.Properties.Mass, Is.EqualTo(core.Body.Mass), "the copy is the flight's build");
            return model;
        }

        // ---- The sway after a windy landing (design 3: the joints' friction) ----------------------------------------------

        // The decay time (s) of the sway after the cut landing on S04's soil in a steady 5 m/s crosswind: the body's velocity
        // across the wind's way and along it, its peaks from 0.5 s to 3.5 s after the touch, ln(peak) fitted against time by
        // least squares; the sway's frequency (Hz) from the peaks' spacing.
        static double SwayDecay(Func<ContactSpec, ContactSpec> edit, out double hz, out int peaks)
        {
            MaterialRow[] m = Materials(out ulong h);
            ContactScenarios.Site site = RangeScenarios.At(RangeScenarios.Soil, 0);
            site.Airframe = true;
            site.Wind = Wind.WindSetup.SteadyFrom(270, 5);
            var times = new List<double>();
            var speeds = new List<double>();
            ContactScenarios.Land(Spec, m, h, true, site: site, edit: edit, watch: (core, t) =>
            {
                times.Add(t);
                speeds.Add(core.State.Velocity.X);
            });
            var at = new List<double>();
            var size = new List<double>();
            for (int i = 1; i + 1 < speeds.Count; i++)
            {
                if (times[i] < 0.5 || times[i] > 3.5) continue;
                double a = Math.Abs(speeds[i]);
                if (a >= Math.Abs(speeds[i - 1]) && a > Math.Abs(speeds[i + 1]) && a > 1e-5)
                {
                    at.Add(times[i]);
                    size.Add(Math.Log(a));
                }
            }
            peaks = at.Count;
            hz = peaks > 1 ? (peaks - 1) / (at[peaks - 1] - at[0]) / 2 : double.NaN; // |v| peaks twice a cycle
            double mt = 0, ms = 0;
            for (int i = 0; i < peaks; i++)
            {
                mt += at[i] / peaks;
                ms += size[i] / peaks;
            }
            double num = 0, den = 0;
            for (int i = 0; i < peaks; i++)
            {
                num += (at[i] - mt) * (size[i] - ms);
                den += (at[i] - mt) * (at[i] - mt);
            }
            return den > 0 ? -den / num : double.NaN;
        }

        [Test]
        public void TheSwayAfterAWindyLandingDecaysByItsModelledDamping()
        {
            // 1.2's 4.6 s was the tube's loss alone on its 262 N/m chain, 1/(ζω) at ζ = η/2 = 0.015. Model 41's side give holds
            // the jaws by their friction (μ·P·D, 1.1's preload and μ drawn per leg) until the moment's excess over their bed
            // passes it: at the sway's force (about a newton at the foot, 0.2 N·m at the clamp against 1.1–5.5 N·m) they never
            // slip, so they add no damping there, only stiffness (the bed held: the tube's 268 N/m with the anchor). The
            // T-joint is in no sway's path: the feet turn by the skids' rolling (1.1's pinned foot). Printed with the jaws' bed
            // always turning (1.2's chain) and with the axial load's softening off, so the cause of each change shows.
            double model41 = SwayDecay(null, out double hz41, out int p41);
            double chain = SwayDecay(c => { c.LegTubeSideStiffness = c.LegJawSideStiffness = c.LegSwayLoad = c.LegTubeAxialStiffness = 0; return c; },
                out double hzChain, out int pChain);
            TestContext.Out.WriteLine($"the sway after a cut landing on S04's soil in a 5 m/s crosswind: model 41 {model41:F2} s ({hz41:F2} Hz, {p41} peaks); "
                + $"1.2's chain (the tube's loss alone, the jaws' bed always turning, no P·Δ) {chain:F2} s ({hzChain:F2} Hz, {pChain} peaks); 1.2's estimate 4.6 s");
            Assert.That(p41, Is.GreaterThan(4), "it sways");
            Assert.That(model41, Is.GreaterThan(0), "and the sway decays");
        }

        // ---- The next landing feels a damaged leg -------------------------------------------------------------------------

        // A touch at 0.5 m/s down with the throttle cut (the free body) drifting along body x or z, with a damaged leg made
        // before the first step (a preset set, or the leg off with its skid hanging), or the known-wrong (the set kept by the
        // law, the shapes left in place).
        static LandingScenarios.Touch Drift(bool forward, int leg, Double3 set, int build) => new LandingScenarios.Touch
        {
            Legs = true, Descent = 0.5, Forward = forward, Power = LandingScenarios.Power.Free, Seconds = 3,
            Prepare = build == 0 ? (Action<FlightCore>)null
                : build == 1 ? core => core.PresetLeg(leg, set)
                : build == 2 ? core => core.RemoveLeg(leg)
                : core => core.PresetLeg(leg, set, false),
        };

        [Test]
        public void TheNextLandingFeelsADamagedLeg()
        {
            MaterialRow[] m = Materials(out ulong h);
            // The bent leg: the first set a harsh level landing leaves (1.2's scenario, seed 1, 1.04 times its sim's onset). Since w9
            // 2.6 a crack (the edge's local yield) comes before the set and does not count.
            static bool Bent(LegScenarios.Outcome x)
            {
                for (int c = 0; c < 4; c++) if ((x.Flags[c] & (LegFlags.Set | LegFlags.Broken)) != 0) return true;
                return false;
            }
            const ulong seed = 1;
            double lo = 1.5, hi = 2.6;
            while (hi - lo > 0.005)
            {
                double mid = (lo + hi) / 2;
                if (Bent(LegScenarios.Run(Spec, m, h, new LegScenarios.Touch { Velocity = new Double3(0, -mid, 0), Seed = seed }))) hi = mid;
                else lo = mid;
            }
            // At the onset (w9 2.6: past about 1.04 × it the level landing's bowed legs kink and are lost, AHarshLandingBendsALeg).
            LegScenarios.Outcome harsh = LegScenarios.Run(Spec, m, h, new LegScenarios.Touch { Velocity = new Double3(0, -hi, 0), Seed = seed });
            int leg = -1;
            for (int c = 0; c < 4 && leg < 0; c++)
                if ((harsh.Flags[c] & LegFlags.Set) != 0 && (harsh.Flags[c] & LegFlags.Broken) == 0 && LegScenarios.SetDegrees(harsh.Set[c]) > 0) leg = c;
            Assert.That(leg, Is.GreaterThanOrEqualTo(0), "the harsh landing bends a leg");
            Double3 set = harsh.Set[leg];
            TestContext.Out.WriteLine($"the harsh landing at {hi:F3} m/s bends leg_{LegDamage.Names[leg]} {LegScenarios.SetDegrees(set):F2}° (its foot's way {set.X:F4}, {set.Z:F4}; twist {set.Y:F4} rad)");

            // The lowest drift that tips it, toward each side in turn: along x and along z, each way. Toward the damaged side
            // is the leg's own side of each axis.
            LegDamageSpec l = Spec.BuildContacts(Spec.Nominal, m, h).Legs;
            Double3 at = l.Foot[leg];
            string[] builds = { "intact", "bent", "missing", "bent, shapes left (known-wrong)" };
            // Since model 49 (p7g-legs 1.4, the legs' lever) a level drifting landing parts or bends the leading legs before it
            // goes over, so the intact and the bent builds tip at no drift under 4 m/s; the bent leg is felt in the landing at the
            // spec's 0.3 m/s of drift, its largest tilt (printed beside the tip speeds).
            var critical = new double[4, 4];
            var tilt = new double[4, 4];
            for (int b = 0; b < 4; b++)
            {
                string line = builds[b] + ":";
                for (int d = 0; d < 4; d++)
                {
                    bool forward = d >= 2;
                    double sign = d % 2 == 0 ? 1 : -1;
                    LandingScenarios.Touch t = Drift(forward, leg, set, b);
                    t.Sign = sign;
                    critical[b, d] = LandingScenarios.CriticalSpeed(Spec, m, h, t);
                    t.Lateral = 0.3;
                    tilt[b, d] = LandingScenarios.Run(Spec, m, h, t).MaxTilt;
                    line += $"  {(forward ? "z" : "x")}{(sign > 0 ? "+" : "−")} {critical[b, d]:F3} m/s ({tilt[b, d]:F3}° at 0.3)";
                }
                TestContext.Out.WriteLine(line);
            }
            // Toward the damaged side: the leg's own side of each axis. The missing leg tips lower there; the bent one tips
            // otherwise than the intact (by its foot's move: a level landing's set splays the foot out, so it stands wider on
            // that side), and the known-wrong, its shapes left at the build, tips as the intact one exactly.
            int towardX = at.X >= 0 ? 0 : 1, towardZ = at.Z >= 0 ? 2 : 3;
            bool differs = false;
            for (int d = 0; d < 4; d++)
            {
                Assert.That(critical[3, d], Is.EqualTo(critical[0, d]), "the known-wrong (the shapes left) tips as the intact");
                Assert.That(tilt[3, d], Is.EqualTo(tilt[0, d]), "and lands as the intact at 0.3 m/s");
                differs |= critical[1, d] != critical[0, d] || tilt[1, d] != tilt[0, d];
            }
            // Since model 86 (the anchors' reach) no build tips under 4 m/s of drift: the missing leg's tips at 2–4 m/s rode a
            // hanging support's anchor left 2–5.8 m behind it (the moment's lever, w9-village-physics 2.4). It is felt in the
            // landing at 0.3 m/s instead: toward its side it tips at a lower drift, or where neither tips it leans farther.
            foreach (int d in new[] { towardX, towardZ })
                Assert.That(critical[2, d] < critical[0, d] || critical[2, d] == critical[0, d] && tilt[2, d] > tilt[0, d], Is.True,
                    "the missing leg tips at a lower drift toward its side, or leans farther at 0.3 m/s");
            Assert.That(differs, "the next landing feels the bent leg");
            double outward = set.X * Math.Sign(at.X) + set.Z * Math.Sign(at.Z);
            TestContext.Out.WriteLine($"the set moves the foot {(outward > 0 ? "outward" : "inward")}; toward its side the bent build tips at "
                + $"{critical[1, towardX] - critical[0, towardX]:+0.000;-0.000;0} (x) and {critical[1, towardZ] - critical[0, towardZ]:+0.000;-0.000;0} (z) m/s from the intact");
        }
    }
}
