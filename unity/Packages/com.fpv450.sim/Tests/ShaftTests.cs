using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7f-damage 1.6 (design 9, 11; D-083's second step): the shaft and the bearings. The scenarios are ShaftScenarios'; each is
    // asserted against its known-wrong.
    public sealed class ShaftTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        // The numbers the scenarios turn on, printed: the shaft's yield, ultimate and hinge, the bell ring's cap on what it passes
        // the shaft, a blade's root cap, the ball-pass ratios.
        static void Numbers(FlightCore core, DroneSpec spec)
        {
            MotorShaft s = core.Contacts.Shafts;
            MotorBell b = core.Contacts.Bells;
            MotorInternals m = spec.Internals[0];
            TestContext.Out.WriteLine($"M_y {s.YieldMoment(0):F4} N·m, M_u {s.UltimateMoment(0):F4} N·m ({s.UltimateMoment(0) / s.YieldMoment(0):F4} M_y), k_φ {s.HingeStiffness(0):F4} N·m/rad, "
                + $"the hub {s.Lever(0) * 1e3} mm over the upper bearing, the bearings {s.Spacing(0) * 1e3} mm apart; a blade's root cap {s.RootCap:F4} N·m; "
                + $"BPFO/f_r {s.OuterRatio(0):F4}, BPFI/f_r {s.InnerRatio(0):F4}, the turning mass {s.RotatingMass(0) * 1e3:F3} g; the bell's ring passes the shaft at most "
                + $"k_r·c_e {b.RingStiffness(0) * b.Clearance(0):F1} N at the can's top, {b.RingStiffness(0) * b.Clearance(0) * (m.BellTo - m.UpperBearingHeight):F4} N·m");
        }

        // prop-motor-damage's "A bent shaft shakes": rr's hub takes side hits whose moment at the upper bearing is 0.8, 1.2 and 2 M_y
        // (through the shaft's load law: the bell's own ring cannot pass the shaft that much, TheBellCannotBendTheShaft), then the
        // drone hovers 10 s. 0.8 leaves it straight (its bearings brinelled: R₁ = 2.33·F passes C0r, so the hover differs from the
        // straight one by their drag, 4e-6 m/s² in the band); 1.2 and 2 log a bend (M − M_y)/k_φ along the hit's moment, its run-out
        // at the stator φ·8 mm and its tilt φ. At 1.2 the hover's 50–200 Hz band rises with them (the 1/rev pull and couples, the
        // run-out's rub); at 2 the run-out, 2 mm, puts the magnets 1.75 mm into the stator: the rub (3.7 N·m) passes the stalled
        // drive, the motor binds (MotorBound) and the drone falls on three rotors, so there is no hover to shake: the band rises
        // with the bend up to the bind, past it the run-out binds the motor (the scenario's words since model 68; since model 70 the bound
        // motor's stalled current then burns it out, p7f-damage 1.6b, so the bind is judged as it happens). The known-wrong, a straight shaft and the motor a flat efficiency loss
        // (Kt × 0.95), keeps the straight hover's band: it fails.
        [Test]
        public void ABentShaftShakes()
        {
            DroneSpec spec = Spec;
            Numbers(new FlightCore(ShaftScenarios.Setup(spec, 1)), spec);
            ShaftScenarios.Bent straight = ShaftScenarios.Hover(spec, 1, 0), wrong = ShaftScenarios.Hover(spec, 1, 0, flat: true);
            TestContext.Out.WriteLine("straight:    " + straight.Describe());
            TestContext.Out.WriteLine("flat loss:   " + wrong.Describe());
            var bent = new Dictionary<double, ShaftScenarios.Bent>();
            foreach (double times in new[] { 0.8, 1.2, 2.0 })
            {
                bent[times] = ShaftScenarios.Hover(spec, 1, times);
                TestContext.Out.WriteLine("hit:         " + bent[times].Describe());
            }
            var core = new FlightCore(ShaftScenarios.Setup(spec, 1));
            MotorShaft s = core.Contacts.Shafts;
            double my = s.YieldMoment(0), k = s.HingeStiffness(0), reach = 0.008;
            Assert.That(bent[0.8].Bends + bent[0.8].Bend, Is.EqualTo(0), "short of M_y the shaft stays straight");
            Assert.That(bent[0.8].Band, Is.EqualTo(straight.Band).Within(1e-3 * straight.Band), "and the hover is the straight one's but the bearings' drag");
            foreach (double times in new[] { 1.2, 2.0 })
            {
                ShaftScenarios.Bent b = bent[times];
                double expected = (times - 1) * my / k;
                Assert.That(b.Bends, Is.EqualTo(1), "the bend is logged");
                Assert.That(Math.Abs(b.Bend - expected), Is.LessThanOrEqualTo(1e-9 * expected), "(M − M_y)/k_φ");
                Assert.That(Math.Abs(b.RunOut - b.Bend * reach), Is.LessThanOrEqualTo(1e-9 * b.RunOut), "its run-out at the stator's far end");
            }
            ShaftScenarios.Bent b12 = bent[1.2];
            Assert.That(b12.Band, Is.GreaterThan(2 * straight.Band), "the bent shaft shakes the camera");
            Assert.That(b12.Band, Is.GreaterThan(DamageScenario.FactoryBandHigh), "past the factory band");
            Assert.That(b12.Rub, Is.GreaterThan(0), "its run-out past the gap rubs");
            Assert.That(b12.Bound, Is.False, "1.2 M_y's rub turns");
            Assert.That(bent[2.0].Bound, Is.True, "2 M_y's run-out binds the motor");
            Assert.That(bent[2.0].Speed, Is.Zero, "its rotor stopped");
            Assert.That(Math.Abs(wrong.Band - straight.Band), Is.LessThan(0.1 * (b12.Band - straight.Band)), "the flat loss does not shake: it fails");
        }

        // "A bent shaft shakes", its forms: the bent rotor's 1/rev loads (Propulsion.OneRevLoads; no factory draws, so they are the
        // shaft's alone) against an independent body-axes computation at 3000, 9000 and 15 000 rpm and eight angles, within 1e-9 of
        // each load's size: the masses' pull ω²·(m_p·l + m_b·l_b)·u, the thrust's tilt T·u, and the couples −ω²·I_rod·(φ·q̂)·q̂ −
        // ω²·(I_p − I_d)_bell·φ − (l − l_b)·ŷ × (ω²·m_b·l_b·u) + T·l·(u × ŷ) + s·Q·u (u = φ × ŷ); and they grow with the bend (×2).
        [Test]
        public void ABentShaftsLoadsFollowTheirForms()
        {
            DroneSpec spec = Spec;
            var core = new FlightCore(ShaftScenarios.Setup(spec, 1, draws: false));
            MotorShaft shafts = core.Contacts.Shafts;
            Propulsion p = core.Propulsion;
            MotorInternals m = spec.Internals[0];
            PropBlade blade = spec.Blade;
            double R = spec.Props.Diameter / 2, mp = 2 * blade.BladeMass(R) + blade.HubMass, l = m.HubHeight - m.UpperBearingHeight;
            double mb = m.BellMass, lb = m.BellCentre - m.UpperBearingHeight, rod = 2 * blade.BladeInertia(R);
            double rb = core.Contacts.Bells.Radius(0) - m.CanThickness - m.BackIronThickness, band = m.BellTo - m.BellFrom;
            double dib = mb * (rb * rb / 2 - band * band / 12), s = spec.Motors[0].Clockwise ? -1 : 1;
            var up = new Double3(0, 1, 0);
            double worst = 0;
            foreach (double rpm in new[] { 3000.0, 9000, 15000 })
            {
                FlightState st = core.State;
                for (int i = 0; i < 4; i++) st.RotorSpeed[i] = rpm * Math.PI / 30;
                double w = st.RotorSpeed[0];
                foreach (double scale in new[] { 1.0, 2.0 })
                {
                    double fp = 0.03 * scale, fq = -0.02 * scale;
                    shafts.PresetBend(0, fp, fq);
                    p.BeginStep(st);
                    double T = p.Thrust(0), Q = p.Torque(0);
                    for (int a = 0; a < 8; a++)
                    {
                        double psi = 0.7 + a * Math.PI / 4;
                        p.OneRevLoads(0, psi, w, out Double3 pull, out Double3 drag, out Double3 couple);
                        double c = Math.Cos(psi), sn = Math.Sin(psi);
                        Double3 pv = new Double3(c, 0, s * sn), qv = new Double3(-s * sn, 0, c), phi = pv * fp + qv * fq, u = Double3.Cross(phi, up);
                        Double3 ePull = u * (w * w * (mp * l + mb * lb)), eTilt = u * T;
                        Double3 eCouple = qv * (-w * w * rod * Double3.Dot(phi, qv)) + phi * (-w * w * dib) - Double3.Cross(up, u * (w * w * mb * lb)) * (l - lb)
                            + Double3.Cross(u, up) * (T * l) + u * (s * Q);
                        worst = Math.Max(worst, Math.Max((pull - ePull).Length() / ePull.Length(),
                            Math.Max((drag - eTilt).Length() / eTilt.Length(), (couple - eCouple).Length() / eCouple.Length())));
                        if (a == 0)
                            TestContext.Out.WriteLine($"{rpm} rpm, φ ({fp}, {fq}) rad: pull {pull.Length():F4} N, the thrust's tilt {drag.Length():F4} N (T {T:F3} N), "
                                + $"couple {couple.Length():F5} N·m");
                    }
                }
            }
            TestContext.Out.WriteLine($"the worst relative difference from the forms: {worst:E2}");
            Assert.That(worst, Is.LessThanOrEqualTo(1e-9));
        }

        // prop-motor-damage's "A brinelled bearing", the law: a side hit on the hub whose upper bearing's radial load is 2·C0r. Its
        // statics R₁ = F·(1 + l/s), R₂ = F·l/s within 1e-9; each dent 1e-4·D_w·(R/C0r)² within 1e-9, logged (MotorDamaged bands 3
        // and 4: the dent, the load, the rating it passed); a second hit below the first's load adds none. The known-wrong, a dent
        // in proportion to the load (1e-4·D_w·R/C0r), is half of it at 2·C0r: it fails the form.
        [Test]
        public void ABrinelledBearingsDentFollowsItsLoad()
        {
            DroneSpec spec = Spec;
            var core = new FlightCore(ShaftScenarios.Setup(spec, 1));
            MotorShaft s = core.Contacts.Shafts;
            MotorInternals m = spec.Internals[0];
            double l = s.Lever(0), span = s.Spacing(0), c0 = m.BearingStaticRating, dw = m.BallDiameter;
            double force = 2 * c0 / (1 + l / span), times = force * l / s.YieldMoment(0);
            var events = new StepEvents(16);
            ShaftScenarios.HubHit(core, 0, times, events);
            int dents = 0;
            for (int i = 0; i < events.Count; i++)
            {
                FlightEvent e = events[i];
                if (e.Kind != EventKind.MotorDamaged) continue;
                int bandCode = e.Index >> 4;
                Assert.That(bandCode, Is.EqualTo(3).Or.EqualTo(4), "a bearing, not the shaft (short of M_y)");
                double load = bandCode == 3 ? force * (1 + l / span) : force * l / span, dent = 1e-4 * dw * (load / c0) * (load / c0);
                TestContext.Out.WriteLine($"{(bandCode == 3 ? "upper" : "lower")} bearing: {e.B:F3} N ({e.B / c0:F4} C0r), dent {e.A * 1e9:F3} nm (the form {dent * 1e9:F3}; "
                    + $"the known-wrong in proportion {1e-4 * dw * load / c0 * 1e9:F3}), passed {e.C:F1} N");
                Assert.That(Math.Abs(e.B - load), Is.LessThanOrEqualTo(1e-9 * load), "the statics on two bearings");
                Assert.That(Math.Abs(e.A - dent), Is.LessThanOrEqualTo(1e-9 * dent), "1e-4·D_w·(R/C0r)²");
                Assert.That(Math.Abs(1e-4 * dw * load / c0 - dent), Is.GreaterThan(1e-9 * dent), "the known-wrong fails the form");
                Assert.That(e.C, Is.EqualTo(c0), "past C0r");
                dents++;
            }
            Assert.That(dents, Is.EqualTo(2), "both bearings pass C0r (the lower at 2·l/(s + l) of it)");
            Assert.That(s.Bend(0), Is.Zero);
            events.Clear();
            ShaftScenarios.HubHit(core, 0, 0.9 * times, events);
            Assert.That(events.Count, Is.Zero, "a lighter hit adds no dent");
        }

        // "A brinelled bearing", flown: the lead prop grazes a steel face 1 mm deep (StrikeTests' graze): each crush's force at the hub
        // (its impulse over α/ω, at most σ_c·A(r_m), and along the span at most the blade's column, its centrifugal tension at the
        // root and its Euler load, MotorShaft.ColumnLoad) loads the upper bearing with 2.3 times it, past C0r (613 N, 2.22 C0r; a
        // 0.78 µm dent). Every dent logged is 1e-4·D_w·(R/C0r)² of its load
        // within 1e-9; with the shafts off (the known-wrong: rigid bearings, model 63) none is.
        [Test]
        public void AGrazeBrinellsTheBearings()
        {
            DroneSpec spec = Spec;
            MotorInternals m = spec.Internals[0];
            foreach (bool on in new[] { true, false })
            {
                Double3 face = StrikeScenarios.Sky + new Double3(0, 0, 1);
                var objects = StrikeScenarios.One(StrikeScenarios.Box("steel", face + new Double3(0, 0, 0.5), new Double3(0.5, 0.5, 0.5)));
                StrikeScenarios.Rig rig = Graze(spec, objects, face, on);
                int dents = 0, bends = 0;
                double worst = 0, largest = 0;
                foreach (FlightEvent e in rig.Log)
                {
                    if (e.Kind != EventKind.MotorDamaged) continue;
                    int bandCode = e.Index >> 4;
                    if (bandCode == 2) bends++;
                    if (bandCode < 3) continue;
                    dents++;
                    double r = e.B / m.BearingStaticRating, dent = 1e-4 * m.BallDiameter * r * r;
                    worst = Math.Max(worst, Math.Abs(e.A - dent) / dent);
                    largest = Math.Max(largest, e.B);
                }
                MotorShaft s = rig.Core.Contacts.Shafts;
                TestContext.Out.WriteLine($"{(on ? "shafts" : "rigid (known-wrong)")}: {rig.Core.Strikes.Passes} passes, {dents} dents logged (the largest load {largest:F1} N, "
                    + $"{largest / m.BearingStaticRating:F3} C0r), {bends} bends; fl's dents {(s != null ? s.DentUpper(3) * 1e9 : 0):F2} / {(s != null ? s.DentLower(3) * 1e9 : 0):F2} nm; "
                    + $"worst against the form {worst:E2}");
                if (on)
                {
                    Assert.That(dents, Is.GreaterThan(0), "the graze brinells");
                    Assert.That(worst, Is.LessThanOrEqualTo(1e-9));
                }
                else Assert.That(dents + bends, Is.Zero, "rigid bearings log nothing: it fails");
            }
        }

        static StrikeScenarios.Rig Graze(DroneSpec spec, RuntimeObject[] objects, Double3 face, bool shafts)
        {
            Quat att = StrikeScenarios.Heading(StrikeScenarios.LeadHeading);
            var south = new Double3(0, 0, -1);
            Double3 at = StrikeScenarios.RimInto(spec, att, 3, face, south, 0.001, face.Y), start = at + south * 0.01;
            FlightSetup setup = StrikeScenarios.Setup(spec, start, att, objects);
            setup.Contacts.MotorShaft = shafts;
            var rig = new StrikeScenarios.Rig(setup, StrikeScenarios.HoverDuty(spec));
            rig.Core.Strikes.Record = new List<PropStrikes.PassRecord>();
            int steps = (int)Math.Ceiling(0.01 / 0.3 * 1000);
            Double3 v = (at - start) * (1000.0 / steps);
            for (int k = 1; k <= steps; k++) rig.Step(start + (at - start) * (k / (double)steps), v);
            rig.Hold(at, 300);
            return rig;
        }

        // "A brinelled bearing", in the hover: rr's bearings brinelled as a 2·C0r hit leaves them. (a) Its drag and its ball-pass
        // frequency are in the step's record: ½·μ·(T/2)·d·Σδ/(1e-4·D_w) and BPFO = (n/2)·(1 − D_w/d_p)·f_r within 1e-9. (b) The rigid
        // body is unchanged by the lines bit for bit: the same flight read through CameraVibration every step and never steps the
        // same states. (c) CameraVibration gains their RMS in the bands they fall in: at hover speed both lines lie above 200 Hz, so
        // X and Y are the stepped band's bit for bit and Z² gains Σ (m_rot·(2πf)²·δ)²/(2M²) within 1e-9; at 15 Hz the outer line
        // (48 Hz) falls in X and the inner (72 Hz) in Y. The known-wrong, rigid bearings, gains nothing.
        [Test]
        public void ABrinelledBearingDragsAndHums()
        {
            DroneSpec spec = Spec;
            FlightSetup setup = ShaftScenarios.Setup(spec, 1);
            var read = new FlightCore(setup);
            var blind = new FlightCore(ShaftScenarios.Setup(spec, 1));
            MotorInternals m = spec.Internals[0];
            double l = read.Contacts.Shafts.Lever(0), span = read.Contacts.Shafts.Spacing(0);
            double times = 2 * m.BearingStaticRating / (1 + l / span) * l / read.Contacts.Shafts.YieldMoment(0);
            var events = new StepEvents(16);
            ShaftScenarios.HubHit(read, 0, times, events);
            ShaftScenarios.HubHit(blind, 0, times, events);
            CrosswindScenario.Arm(read);
            CrosswindScenario.Arm(blind);
            var pa = new SimPilot(read.Controller, setup.StartPosition);
            var pb = new SimPilot(blind.Controller, setup.StartPosition);
            MotorShaft s = read.Contacts.Shafts;
            double du = s.DentUpper(0), dl = s.DentLower(0), worstDrag = 0, worstLine = 0, worstZ = 0;
            int differ = 0, steady = 0;
            Double3 cv = default;
            for (int k = 0; k < 3000; k++)
            {
                read.Advance(pa.Sticks(read, read.State.Step * FlightCore.StepUs));
                cv = read.CameraVibration();
                blind.Advance(pb.Sticks(blind, blind.State.Step * FlightCore.StepUs));
                if (!Same(read.State, blind.State)) differ++;
                if (k < 1000) continue;
                ref readonly StepTrace t = ref read.Trace;
                double T = read.Propulsion.Thrust(0), w = read.State.RotorSpeed[0];
                double drag = 0.5 * 0.0015 * (0.5 * Math.Abs(T)) * m.ShaftDiameter * (du + dl) / (1e-4 * m.BallDiameter);
                double bpfo = 0.5 * m.BearingBalls * (1 - m.BallDiameter / m.PitchDiameter) * w / (2 * Math.PI);
                worstDrag = Math.Max(worstDrag, Math.Abs(t.BearingDrag[0] - drag) / drag);
                worstLine = Math.Max(worstLine, Math.Abs(t.BallPassOuter[0] - bpfo) / bpfo);
                Double3 field = read.Vibration.Field;
                double mass = read.Body.Mass, mr = s.RotatingMass(0), z2 = 0;
                foreach (double ratio in new[] { s.OuterRatio(0), s.InnerRatio(0) })
                {
                    double f = ratio * w / (2 * Math.PI), a2 = Math.Pow(mr * Math.Pow(2 * Math.PI * f, 2) / mass, 2);
                    Assert.That(f, Is.GreaterThan(VibrationBands.HighCorner), "above 200 Hz at hover speed");
                    z2 += 0.5 * a2 * (du * du + dl * dl);
                }
                Assert.That(cv.X, Is.EqualTo(field.X), "X unchanged bit for bit");
                Assert.That(cv.Y, Is.EqualTo(field.Y), "Y unchanged bit for bit");
                worstZ = Math.Max(worstZ, Math.Abs(cv.Z * cv.Z - field.Z * field.Z - z2) / z2);
                steady++;
            }
            Double3 last = read.Vibration.Field;
            TestContext.Out.WriteLine($"rr's dents {du * 1e9:F3} / {dl * 1e9:F3} nm; at the end its drag {read.Trace.BearingDrag[0] * 1e6:F4} µN·m, BPFO {read.Trace.BallPassOuter[0]:F2} Hz, "
                + $"BPFI {read.Trace.BallPassOuter[0] * s.InnerRatio(0) / s.OuterRatio(0):F2} Hz; the camera's bands {last.X:F4} / {last.Y:F4} / {last.Z:F4} m/s² stepped, "
                + $"{cv.X:F4} / {cv.Y:F4} / {cv.Z:F4} with the lines; worst against the forms: drag {worstDrag:E2}, BPFO {worstLine:E2}, Z² {worstZ:E2}; {differ} steps differ");
            Assert.That(differ, Is.Zero, "the body never sees the lines");
            Assert.That(steady, Is.EqualTo(2000));
            Assert.That(worstDrag, Is.LessThanOrEqualTo(1e-9));
            Assert.That(worstLine, Is.LessThanOrEqualTo(1e-9));
            Assert.That(worstZ, Is.LessThanOrEqualTo(1e-9));
            Assert.That(cv.Z, Is.GreaterThan(last.Z), "the lines add to Z");

            // At 15 Hz: the outer line at 48 Hz in X, the inner at 72 Hz in Y.
            var slow = new Corners();
            slow[0] = 2 * Math.PI * 15;
            Double3 lines = s.BallPass(slow, read.Body.Mass);
            TestContext.Out.WriteLine($"at 15 Hz: X {lines.X:E3}, Y {lines.Y:E3}, Z {lines.Z:E3} (m/s²)²");
            Assert.That(lines.X, Is.GreaterThan(0));
            Assert.That(lines.Y, Is.GreaterThan(0));
            Assert.That(lines.Z, Is.Zero);

            // The known-wrong: rigid bearings.
            var rigid = new FlightCore(ShaftScenarios.Setup(spec, 1, shafts: false));
            Assert.That(rigid.Contacts.Shafts, Is.Null, "no shafts, no lines: it fails");
        }

        static bool Same(in FlightState a, in FlightState b)
        {
            if (!Bits(a.Position, b.Position) || !Bits(a.Velocity, b.Velocity) || !Bits(a.BodyRate, b.BodyRate)) return false;
            if (BitConverter.DoubleToInt64Bits(a.Attitude.W) != BitConverter.DoubleToInt64Bits(b.Attitude.W)
                || BitConverter.DoubleToInt64Bits(a.Attitude.X) != BitConverter.DoubleToInt64Bits(b.Attitude.X)) return false;
            for (int i = 0; i < 4; i++)
                if (BitConverter.DoubleToInt64Bits(a.RotorSpeed[i]) != BitConverter.DoubleToInt64Bits(b.RotorSpeed[i])) return false;
            return true;
        }

        static bool Bits(Double3 a, Double3 b) =>
            BitConverter.DoubleToInt64Bits(a.X) == BitConverter.DoubleToInt64Bits(b.X) && BitConverter.DoubleToInt64Bits(a.Y) == BitConverter.DoubleToInt64Bits(b.Y)
            && BitConverter.DoubleToInt64Bits(a.Z) == BitConverter.DoubleToInt64Bits(b.Z);

        // prop-motor-damage's "A lost blade shakes the drone and pulls at its hub": the hovering drone loses rr's blade 0 at its root (seeds 1–3) and
        // flies 1 s with the sticks centred (acro, the throttle held), and with SimPilot holding the point. The rotor flies the
        // remaining blade: about 0.6 of its thrust (one blade's lower inflow, BEMT) and 0.47 of its torque, the lost blade's pull of
        // about 265 N at the loss and the remaining blade's 1/rev moments, so the camera's 50–200 Hz band is many times the
        // known-wrong's (the same mean thrust and torque loss scripted, no 1/rev), which keeps the factory band: it fails "follow the
        // remaining blade". The drone holds its attitude: rr's command rises from the hover's 0.48 to about 0.6 (its remaining blade
        // still lifts the corner's share with the class's power to spare) and the 1/rev, near 150–190 Hz, is far above the attitude
        // loop; the tilt at 1 s is printed (about 7° with the sticks centred, 1° held) and never passes 45° either way. (Until model
        // 68 the scenario read "throws the drone over", 45° within 1 s, design 11's hand estimate; reworded to what this computes.)
        [Test]
        public void ALostBladeShakesTheDroneAndPullsAtItsHub()
        {
            DroneSpec spec = Spec;
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                ShaftScenarios.Lost lost = ShaftScenarios.LoseBlade(spec, seed), held = ShaftScenarios.LoseBlade(spec, seed, centred: false);
                ShaftScenarios.Lost wrong = ShaftScenarios.LoseBlade(spec, seed, wrong: true);
                foreach (ShaftScenarios.Lost r in new[] { lost, held, wrong }) TestContext.Out.WriteLine($"seed {seed}: " + r.Describe());
                Assert.That(lost.Pull, Is.GreaterThan(200), "the lost blade's pull, hundreds of newtons");
                Assert.That(lost.ThrustShare, Is.InRange(0.5, 0.65), "about 0.6 of the rotor's thrust left");
                Assert.That(lost.Band, Is.GreaterThan(5 * wrong.Band), "the rotor follows the remaining blade: it shakes");
                Assert.That(wrong.Band, Is.LessThan(2 * DamageScenario.FactoryBandHigh), "the scripted loss does not: it fails");
                Assert.That(lost.Command, Is.GreaterThan(wrong.Command - 0.05), "rr's command rises to carry its corner");
                Assert.That(lost.TiltAt, Is.NaN, "the drone holds its attitude for the second, the sticks centred");
                Assert.That(held.TiltAt, Is.NaN, "and the simulated pilot holding");
            }
        }

        // "A lost blade shakes the drone and pulls at its hub", its event: a 16 mm steel rod 15 mm from the front-right hub (its near side) breaks
        // both blades at their root station as they pass it, each a BladeDamaged (Break) then a BladeLost with the tip at the first
        // station or less; logged once a blade.
        [Test]
        public void ARootBreakLogsBladeLost()
        {
            DroneSpec spec = Spec;
            StrikeScenarios.Rig rig = ShaftScenarios.Rod(spec, 0.112, 0.008);
            int lost = 0;
            FlightEvent before = default;
            foreach (FlightEvent e in rig.Log)
            {
                if (e.Kind == EventKind.BladeLost)
                {
                    lost++;
                    TestContext.Out.WriteLine($"BladeLost: blade {e.Index}, tip {e.A * 127:F3} mm, {e.B * 1e3:F3} g, broken at {e.C * 127:F3} mm");
                    Assert.That(before.Kind, Is.EqualTo(EventKind.BladeDamaged), "after its break");
                    Assert.That((StrikeMode)(before.Index >> 4), Is.EqualTo(StrikeMode.Break));
                    Assert.That(e.A, Is.LessThanOrEqualTo(spec.Blade.Stations[0]), "at its root");
                }
                before = e;
            }
            Assert.That(lost, Is.EqualTo(2), "both blades, once each");
        }

        // A prop lost with its shaft (design 9, 11): a hub hit past the ultimate moment breaks the shaft. PropLost is logged; from
        // the next step the rotor makes no thrust and no air torque and turns the bell alone (J the drawn less the prop's), its
        // blades gone; the broken shaft takes no more load.
        [Test]
        public void PastItsUltimateTheShaftBreaksAndThePropIsLost()
        {
            DroneSpec spec = Spec;
            var core = new FlightCore(ShaftScenarios.Setup(spec, 1));
            MotorShaft s = core.Contacts.Shafts;
            double j = core.Propulsion.Inertia(0), times = 1.05 * s.UltimateMoment(0) / s.YieldMoment(0);
            var events = new StepEvents(16);
            ShaftScenarios.HubHit(core, 0, times, events);
            int props = 0;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == EventKind.PropLost)
                {
                    props++;
                    Assert.That(events[i].A, Is.GreaterThan(events[i].B), "past M_u");
                }
            Assert.That(props, Is.EqualTo(1));
            Assert.That(s.Broken(0), Is.True);
            double propJ = spec.Blade.Inertia(spec.Props.Diameter / 2, spec.Props.Blades);
            Assert.That(core.Propulsion.Inertia(0), Is.EqualTo(j - propJ).Within(1e-18), "the bell's J");
            CrosswindScenario.Arm(core);
            for (int k = 0; k < 200; k++) core.Advance(new StickSample { HostUs = core.State.Step * FlightCore.StepUs, Arm = true, Throttle = 0.5 });
            TestContext.Out.WriteLine($"rr after: {core.State.RotorSpeed[0]:F1} rad/s, thrust {core.Propulsion.Thrust(0)}, J {core.Propulsion.Inertia(0):E4} (was {j:E4})");
            Assert.That(core.Propulsion.Thrust(0), Is.Zero);
            Assert.That(core.Propulsion.Torque(0), Is.Zero);
            Assert.That(core.State.RotorSpeed[0], Is.GreaterThan(0), "the bell turns");
            events.Clear();
            ShaftScenarios.HubHit(core, 0, 3, events);
            Assert.That(events.Count, Is.Zero, "a broken shaft takes no load");
        }

        // The finding behind "A bent shaft shakes"'s hits on the hub: the bell's ring passes the shaft at most k_r·g at the stator's
        // height (the teeth carry the rest) and k_r·c_e above it, whose moment at the upper bearing from the can's top, 13.1 N·m, is
        // short of M_y (14.7 N·m). A side hit at 6 m/s on concrete at the stator's height logs no bend and no dent (BellScenarios).
        [Test]
        public void TheBellCannotBendTheShaft()
        {
            DroneSpec spec = Spec;
            var core = new FlightCore(ShaftScenarios.Setup(spec, 1));
            Numbers(core, spec);
            MotorBell b = core.Contacts.Bells;
            MotorInternals m = spec.Internals[0];
            double cap = b.RingStiffness(0) * b.Clearance(0) * (m.BellTo - m.UpperBearingHeight);
            Assert.That(cap, Is.LessThan(core.Contacts.Shafts.YieldMoment(0)));
            BellScenarios.Hit hit = BellScenarios.Struck(spec, 6, false);
            TestContext.Out.WriteLine(hit.Describe());
            Assert.That(hit.Dents, Is.Zero, "no bend, no bearing's dent and no ring's dent");
        }

        // A bent shaft on brinelled bearings in the hover, read through CameraVibration each step, allocates nothing.
        [Test]
        public void ABentShaftOnBrinelledBearingsAllocatesNothing()
        {
            DroneSpec spec = Spec;
            var core = new FlightCore(ShaftScenarios.Setup(spec, 1));
            core.Contacts.Shafts.PresetBend(0, 0.01, 0.02);
            core.Contacts.Shafts.PresetDent(0, true, 1e-6);
            CrosswindScenario.Arm(core);
            var stick = new StickSample { Arm = true, Throttle = 0.5 };
            for (int k = 0; k < 100; k++)
            {
                stick.HostUs = core.State.Step * FlightCore.StepUs;
                core.Advance(stick);
            }
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 500; k++)
            {
                stick.HostUs = core.State.Step * FlightCore.StepUs;
                core.Advance(stick);
                core.CameraVibration();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            TestContext.Out.WriteLine($"  {allocated} B over 500 steps");
            Assert.That(allocated, Is.Zero);
        }
    }
}
