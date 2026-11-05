using System;
using System.IO;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7f-damage 1.5 (design 8, 10; D-073, D-083's first step): the motor's bell. The scenarios are BellScenarios'; each is
    // asserted against its known-wrong.
    public sealed class BellTests
    {
        static DroneSpec Spec => RepositoryDrone.Spec();

        // prop-motor-damage's "A scraped bell draws more current": the rear-right bell pressed at 5 and 20 N on masonry while the
        // drone holds that rotor's thrust. The rotor feels μ_k·F_n·R (1e-9), the drone is pushed along the wall against the bell's
        // surface (as a spinning wheel drives), and the motor's current and the total rise with the push. The known-wrong, a flat
        // efficiency loss while the motor touches (Kt × 0.95), draws the same at 5 and at 20 N, so it fails.
        [Test]
        public void AScrapedBellDrawsMoreCurrent()
        {
            BellScenarios.Press free = BellScenarios.Pressed(Spec, 0), p5 = BellScenarios.Pressed(Spec, 5), p20 = BellScenarios.Pressed(Spec, 20);
            BellScenarios.Press w5 = BellScenarios.Pressed(Spec, 5, flat: true), w20 = BellScenarios.Pressed(Spec, 20, flat: true);
            foreach (BellScenarios.Press p in new[] { free, p5, p20 }) TestContext.Out.WriteLine("bell:       " + p.Describe());
            foreach (BellScenarios.Press p in new[] { w5, w20 }) TestContext.Out.WriteLine("flat loss:  " + p.Describe());
            double rise5 = p5.Amps - free.Amps, rise20 = p20.Amps - free.Amps, wrong5 = w5.Amps - free.Amps, wrong20 = w20.Amps - free.Amps;
            TestContext.Out.WriteLine($"rr's rise: {rise5:F3} A at 5 N, {rise20:F3} A at 20 N (× {rise20 / rise5:F2}); total {p5.Total - free.Total:F3}, {p20.Total - free.Total:F3} A; "
                + $"the flat loss {wrong5:F3} and {wrong20:F3} A");
            foreach (BellScenarios.Press p in new[] { p5, p20 })
            {
                Assert.That(p.Normal, Is.EqualTo(p.Newtons).Within(1e-6 * p.Newtons), "the press asked (N)");
                Assert.That(Math.Abs(p.Scrape - p.Expected), Is.LessThanOrEqualTo(1e-9 * p.Expected), "the rotor feels μ_k·F_n·R");
                Assert.That(Math.Abs(p.Push - p.Kinetic * p.Normal), Is.LessThanOrEqualTo(1e-9 * p.Kinetic * p.Normal), "pushed along the wall at μ_k·F_n");
                Assert.That(p.Strikes + p.Others, Is.EqualTo(0), "the bell alone touches");
            }
            Assert.That(p5.Push, Is.GreaterThan(0), "against the bell's surface");
            Assert.That(rise5, Is.GreaterThan(0), "the motor draws more pressed");
            Assert.That(rise20, Is.GreaterThan(2 * rise5), "and more with the push");
            Assert.That(p20.Total - free.Total, Is.GreaterThan(p5.Total - free.Total), "so does the total");
            Assert.That(p5.Total, Is.GreaterThan(free.Total));
            Assert.That(Math.Abs(wrong20 - wrong5), Is.LessThan(0.1 * Math.Abs(wrong5)), "the flat loss does not grow with the push: it fails");
        }

        // prop-motor-damage's "A dented bell rubs and can bind", on the bell as built (MotorBell): (a) a side hit at 1, 3 and 6 m/s
        // with the rotor at hover speed. On concrete at the stator's height the ring closes the gap at k_r·g (137 N), far short of
        // its collapse load (524 N): the teeth carry the rest through the magnets, so it never dents there, and the magnets' press
        // rubs while it lasts; the known-wrong, design 8's ring with nothing behind it, dents past the gap and would rub for good.
        // On a 3 mm steel rod across the can's lip below the stator (the ring free to the end turns' 1.8 mm) the sphere meets it on
        // its lower slope, where the arm's upward give takes the load: the hits glance under the collapse load (a dent there would be
        // logged with its depth: the law's own test below). The band above the stator lies within 5 mm of the prop's slab: what
        // reaches it there reaches the blades.
        [Test]
        public void ASideHitDentsTheBellOnlyWhereNothingIsBehindIt()
        {
            DroneSpec spec = Spec;
            foreach (double v in new[] { 1.0, 3.0, 6.0 })
            {
                BellScenarios.Hit wall = BellScenarios.Struck(spec, v, false), rod = BellScenarios.Struck(spec, v, true);
                TestContext.Out.WriteLine(wall.Describe());
                TestContext.Out.WriteLine(rod.Describe());
                Assert.That(wall.Strikes + wall.Tunnels + rod.Strikes + rod.Tunnels, Is.EqualTo(0), "the bell alone meets them");
                Assert.That(wall.DentGap, Is.EqualTo(0), "the stator stops the ring short of its collapse");
                Assert.That(wall.Kt, Is.EqualTo(1), "so the gap and Kt are as new");
                Assert.That(wall.Peak, Is.GreaterThan(137), "the hit closes the gap");
                Assert.That(wall.Press, Is.GreaterThan(0), "and presses the magnets onto the teeth");
                Assert.That(wall.SpeedAfter, Is.LessThan(wall.SpeedBefore), "the scrape and the press slow the rotor");
                if (wall.Peak > 524 + 2e5 * 0.0003) Assert.That(wall.WrongDent, Is.GreaterThan(0.0003), "the known-wrong would dent past the gap");
                if (rod.Peak > 524)
                {
                    Assert.That(rod.Dents, Is.GreaterThan(0), "past its collapse load the free band dents");
                    Assert.That(rod.DentFree, Is.GreaterThan(0).And.LessThanOrEqualTo(0.0018), "toward the end turns, at most to them (m)");
                }
                else Assert.That(rod.Dents, Is.EqualTo(0));
                Assert.That(rod.Kt, Is.EqualTo(1), "a dent off the stator leaves the gap");
            }
        }

        // (a') The ring's collapse where nothing is behind it, on the law itself (MotorBell.Pair, a bell pair's load rising 50 N a
        // step to 1 kN on a 200 kN/m pair, off the stator): each step past the collapse load dents by (F − P_c)/k_n and hardens
        // P_c to F (1e-12 m), the ring's load capped by its room to the end turns, k_r·(c_e − δ), so the dent never passes
        // c_e − P_c/k_r (its elastic give at the collapse and the dent fill the clearance); each step that dents reports it (a
        // MotorDamaged event in a flight). A free hit there does not reach it: the motor's sphere meets a rod at its lower face on
        // a slope, where the arm's 12 kN/m upward give takes the load (the rod rows of the side-hit test).
        [Test]
        public void TheRingDentsPastItsCollapseLoadWithNothingBehindIt()
        {
            BellScenarios.Rig rig = BellScenarios.Dented(Spec, 0);
            MotorBell b = rig.Core.Contacts.Bells;
            int c = BellScenarios.Corner, dents = 0;
            double kr = b.RingStiffness(c), ce = b.Clearance(c), pc = b.CollapseLoad(c), kn = 2e5, dent = 0, hard = pc, last = 0;
            for (double f = 100; f <= 1000; f += 50)
            {
                bool dented = b.Pair(c, f, default, new Double3(0, 0, 1), false, kn, 0.5, out double depth, out double load, out double passed);
                double fr = Math.Min(f, kr * (ce - dent));
                bool expect = fr > hard;
                if (expect)
                {
                    dent = Math.Min(dent + (fr - hard) / kn, ce);
                    hard = fr;
                }
                Assert.That(dented, Is.EqualTo(expect), $"at {f} N");
                Assert.That(Math.Abs(b.DentFree(c) - dent), Is.LessThanOrEqualTo(1e-12), $"the dent at {f} N");
                if (dented)
                {
                    dents++;
                    last = depth;
                    Assert.That(load, Is.EqualTo(fr));
                }
            }
            TestContext.Out.WriteLine($"k_r {kr:E4} N/m, P_c {pc:F1} N, the room {ce * 1e3:F2} mm (k_r·c_e {kr * ce:F1} N): {dents} denting steps to {b.DentFree(c) * 1e3:F4} mm "
                + $"(bound {(ce - pc / kr) * 1e3:F4} mm), Kt × {b.KtRatio(c):F6}");
            Assert.That(dents, Is.GreaterThan(0));
            Assert.That(last, Is.EqualTo(b.DentFree(c)), "the last report's depth");
            Assert.That(b.DentFree(c), Is.LessThanOrEqualTo(ce - pc / kr + 1e-12), "the room bounds it");
            Assert.That(b.DentGap(c), Is.EqualTo(0));
            Assert.That(b.KtRatio(c), Is.EqualTo(1), "off the stator the gap is as new");
        }

        // (b) A dent short of the gap changes Kt by the surface magnet's flux form within 1e-9: preset 0.1 mm in the stator's band,
        // over a quarter of the circumference, B = B_r/(1 + μ_r·g/l_m); a dent to two thirds of the gap about 3 %.
        [Test]
        public void ADentShortOfTheGapChangesKtByTheFlux()
        {
            MotorInternals m = Spec.Internals[BellScenarios.Corner];
            foreach (double dent in new[] { 0.0001, 0.0002 })
            {
                BellScenarios.Rig rig = BellScenarios.Dented(Spec, dent);
                rig.Step(BellScenarios.Sky, default);
                double g = m.Gap, l = m.MagnetThickness, mu = m.MagnetPermeability, a = 0.25;
                double expected = a * (1 + mu * g / l) / (1 + mu * (g - dent) / l) + (1 - a);
                double kt = rig.Core.Contacts.Bells.KtRatio(BellScenarios.Corner);
                TestContext.Out.WriteLine($"a {dent * 1e3:F1} mm dent: Kt × {kt:F9} (the flux form {expected:F9}); the motor's Kt {rig.Core.Propulsion.TorqueConstant(BellScenarios.Corner):E6} N·m/A");
                Assert.That(Math.Abs(kt - expected), Is.LessThanOrEqualTo(1e-9 * expected));
                Assert.That(rig.Trace.BellKtChange[BellScenarios.Corner], Is.EqualTo(kt - 1), "logged");
                Assert.That(rig.Trace.BellRub[BellScenarios.Corner], Is.EqualTo(0), "short of the gap it does not rub");
                // Propulsion flies it, with the motor's heat's factor since model 70 (p7f-damage 1.6b: its windings warm from the first step).
                double heat = rig.Core.Contacts.Health.KtFactor(BellScenarios.Corner);
                Assert.That(rig.Core.Propulsion.TorqueConstant(BellScenarios.Corner), Is.EqualTo(60 / (2 * Math.PI * 900) * kt * heat).Within(1e-15), "Propulsion flies it");
            }
        }

        // (c) A dent past the gap rubs with Roark's force k_r·(δ − g) and its torque μ_s·N·r_stator (1e-9), and wears in by
        // Archard on the magnet and the teeth: each step's wear is the closed form's at that step's force and speed (1e-9), and the
        // time to grind in at hover is printed. The known-wrong, a rub with no wear, never grinds in.
        [Test]
        public void ADentPastTheGapRubsAndWearsIn()
        {
            MotorInternals m = Spec.Internals[BellScenarios.Corner];
            const double past = 0.0001;
            BellScenarios.Rig rig = BellScenarios.Dented(Spec, m.Gap + past);
            MotorBell b = rig.Core.Contacts.Bells;
            int c = BellScenarios.Corner;
            double kr = b.RingStiffness(c), rs = b.StatorRadius(c);
            double first = b.Interference(c);
            rig.Step(BellScenarios.Sky, default);
            double n = kr * first, w = rig.Now.RotorSpeed[c]; // the wear's speed: the rotor's after the step's sub-steps
            Assert.That(Math.Abs(b.Rub(c) - n), Is.LessThanOrEqualTo(1e-9 * n), "Roark's force");
            Assert.That(Math.Abs(rig.Trace.BellRub[c] - 0.3 * n * rs), Is.LessThanOrEqualTo(1e-9 * 0.3 * n * rs), "its torque");
            double area = 2 * Math.PI * rs * (m.StatorTo - m.StatorFrom);
            double wear = 1e-4 * n * w * rs * FlightCore.Dt * (1 / (5.5e9 * 0.25 * area) + 1 / (1.7e9 * area));
            Assert.That(Math.Abs(b.Worn(c) - wear), Is.LessThanOrEqualTo(1e-9 * wear), "Archard's wear this step");
            for (int k = 0; k < 2000; k++) rig.Step(BellScenarios.Sky, default);
            double rate = (first - b.Interference(c)) / 2.001;
            TestContext.Out.WriteLine($"a dent {past * 1e3:F2} mm past the gap: k_r {kr:E4} N/m, N {n:F2} N, rub {0.3 * n * rs:F4} N·m; after 2 s {b.Interference(c) * 1e6:F4} µm past, "
                + $"the magnet {b.MagnetThickness(c) * 1e3:F6} mm, Kt × {b.KtRatio(c):F6}; worn in at this rate in {first / rate:F0} s (an exponential's time constant at hover)");
            Assert.That(b.Interference(c), Is.LessThan(first), "it grinds in");
            Assert.That(b.MagnetThickness(c), Is.LessThan(m.MagnetThickness), "the magnet thins");
        }

        // (d) Binding: with the ESC held at 40 A (the compared variant) a rub past Kt·40 A = 0.42 N·m stops the rotor and its drive
        // at the limit cannot turn it: bound. The class's ESC limits no current (D-069, D-073): the same rub keeps turning on the
        // chain's own drive (about Kt·V/R at full duty), which a rub past that binds. The known-wrong, design 8's 40 A bind for the
        // class's ESC, binds at the first.
        [Test]
        public void ARubPastTheDriveBindsTheMotor()
        {
            MotorInternals m = Spec.Internals[BellScenarios.Corner];
            int c = BellScenarios.Corner;
            string Run(double torque, bool protection, out bool bound, out double speed, out double drive)
            {
                BellScenarios.Rig probe = BellScenarios.Dented(Spec, 0, protection);
                MotorBell b = probe.Core.Contacts.Bells;
                double dent = m.Gap + torque / (0.3 * b.RingStiffness(c) * b.StatorRadius(c));
                BellScenarios.Rig rig = BellScenarios.Dented(Spec, dent, protection);
                bound = false;
                drive = 0;
                for (int k = 0; k < 1500; k++)
                {
                    rig.Step(BellScenarios.Sky, default);
                    if ((rig.Core.Propulsion.Bound & (1 << c)) != 0)
                    {
                        bound = true;
                        drive = rig.Core.Propulsion.HeldDrive(c);
                    }
                }
                speed = rig.Now.RotorSpeed[c];
                return $"rub {torque:F2} N·m, {(protection ? "40 A" : "no limit")}: {(bound ? "bound" : "turning")} at {speed:F1} rad/s, duty {rig.Duty:F3}, the stalled drive {drive:F3} N·m";
            }
            string a = Run(0.6, true, out bool b40, out double s40, out double d40);
            string n = Run(0.6, false, out bool bFree, out double sFree, out _);
            string h = Run(2.5, false, out bool bHigh, out _, out double dHigh);
            TestContext.Out.WriteLine(a);
            TestContext.Out.WriteLine(n);
            TestContext.Out.WriteLine(h);
            Assert.That(b40, Is.True, "past Kt·40 A the protected motor binds");
            Assert.That(s40, Is.EqualTo(0));
            Assert.That(d40, Is.LessThanOrEqualTo(0.6), "its stalled drive within the rub");
            Assert.That(bFree, Is.False, "the class's ESC turns it on (the known-wrong's 40 A bind fails here)");
            Assert.That(sFree, Is.GreaterThan(100));
            Assert.That(bHigh, Is.True, "a rub past the chain's drive binds it");
            Assert.That(dHigh, Is.GreaterThan(0.6).And.LessThanOrEqualTo(2.5), "at full duty the stalled drive is the chain's (N·m)");
        }

        // prop-motor-damage's "A dragging motor holds its limit longer": the full punch from a descent, with every ESC at 40 A (the
        // compared variant), the rear-right motor rubbing 0.15 N·m (a dent past the gap) against the sound drone on the same seed.
        // Full duty from the hover asks far past 40 A, so both meet the limit in the first steps; the rubbing motor's no later, from a
        // higher current before the punch, and holds it longer, and the total current is the motors' sum (1e-9). The known-wrong, a
        // rub only logged (its drag never in the motor's torque balance), is the sound drone's.
        [Test]
        public void ADraggingMotorHoldsItsLimitLonger()
        {
            DroneSpec spec = Spec;
            Punch sound = Punched(spec, 0), rubbing = Punched(spec, 0.15), logged = Punched(spec, 0.15, applied: false);
            TestContext.Out.WriteLine("sound:    " + sound.Describe());
            TestContext.Out.WriteLine("rubbing:  " + rubbing.Describe());
            TestContext.Out.WriteLine("logged:   " + logged.Describe());
            Assert.That(rubbing.FirstLimited, Is.LessThanOrEqualTo(sound.FirstLimited), "on its limit no later");
            Assert.That(rubbing.HoverAmps, Is.GreaterThan(sound.HoverAmps), "from a higher current before the punch");
            Assert.That(rubbing.Limited, Is.GreaterThan(sound.Limited), "and on it longer (s)");
            Assert.That(rubbing.SumError, Is.LessThanOrEqualTo(1e-9), "the total is the motors' sum");
            Assert.That(logged.Limited, Is.EqualTo(sound.Limited), "the known-wrong is the sound drone: it fails");
        }

        public struct Punch
        {
            public double Descent, FirstLimited, Limited, HoverAmps, SumError;

            public string Describe() =>
                $"from {Descent:F2} m/s down: rr's ESC on its 40 A limit {FirstLimited * 1e3:F0} ms after the punch, for {Limited:F3} s in all; rr {HoverAmps:F2} A in the hover before; "
                + $"the total against the motors' sum {SumError:E2}";
        }

        static Punch Punched(DroneSpec spec, double rub, bool applied = true)
        {
            FlightSetup setup = ShakeScenario.Setup(spec, 0, 3, protection: true);
            MaterialRow[] m = ContactScenarios.RepositoryMaterials(out ulong mh);
            setup.Contacts = spec.BuildContacts(new Loadout { Payload = 0, Legs = true }, m, mh);
            // The bell's rub alone: since model 70 without the motors' heat (p7f-damage 1.6b), which the known-wrong's SetMotor would
            // overwrite and whose rub heat a logged rub would still warm the motor by.
            setup.Contacts.MotorHeat = false;
            var core = new FlightCore(setup);
            MotorBell b = core.Contacts.Bells;
            int c = BellScenarios.Corner;
            if (rub > 0) b.PresetDent(c, b.Gap(c) + rub / (0.3 * b.RingStiffness(c) * b.StatorRadius(c)));
            var r = new Punch { FirstLimited = double.NaN };
            int hover = 16 + (int)(ShakeScenario.HoverSeconds * 1000), input = hover + 600, fields = 0;
            ShakeScenario.Fly(core, setup, 0.6, false, 1, (k, stick) =>
            {
                long step = k.State.Step;
                if (!applied) k.Propulsion.SetMotor(c, 1, 0); // its drag computed, never flown (the next step's Propel reads this)
                if (step > hover - 1000 && step <= hover && step % FlightCore.FieldSteps == 0)
                {
                    k.GetSignals(out FeedSignals h);
                    r.HoverAmps += h.MotorCurrentA[c];
                    fields++;
                }
                if (step == hover) r.HoverAmps /= fields;
                if (step == input) r.Descent = -k.State.Velocity.Y;
                if (step < input) return;
                if ((k.State.EscLimited & (1 << c)) != 0)
                {
                    if (double.IsNaN(r.FirstLimited)) r.FirstLimited = (step - input) * FlightCore.Dt;
                    r.Limited += FlightCore.Dt;
                }
                if (step % FlightCore.FieldSteps == 0)
                {
                    k.GetSignals(out FeedSignals s);
                    double sum = 0;
                    for (int i = 0; i < 4; i++) sum += s.MotorCurrentA[i];
                    if (s.MotorCurrentTotalA != 0) r.SumError = Math.Max(r.SumError, Math.Abs(s.MotorCurrentTotalA - sum) / Math.Abs(s.MotorCurrentTotalA));
                }
            });
            return r;
        }

        // prop-motor-damage's "A trim raises the hover's current": the hover with the rear-right prop trimmed by 12.7 mm and sound,
        // on the same seed and pack. Both blades trimmed (a smaller disc; the scenario's case), the rotor needs 6.9 % more power for its
        // thrust (its BEMT tables, η_Q/η_T^1.5) and the total current is clearly higher, which MotorCurrentTotalA carries to the OSD.
        // One blade trimmed (printed) moves that rotor's power by only 0.8 % (the long blade still sweeps the disc), a quarter of
        // the hover's load: the total moves by less than that share (the faster rotor's higher C_T and its lower torque's copper
        // loss take it back), and the printed change is the model's, not a pass. The known-wrong, a trim that leaves the rotor's
        // tables alone (PropulsionSpec.Rotor null), hovers on the sound drone's current, so it fails.
        [Test]
        public void ATrimRaisesTheHoversCurrent()
        {
            DroneSpec spec = Spec;
            DamageScenario.Result sound = DamageScenario.Hover(DamageScenario.Setup(spec, 1), out FlightCore cs);
            cs.GetSignals(out FeedSignals ss);
            DamageScenario.Result both = DamageScenario.Hover(DamageScenario.Setup(spec, 1), out FlightCore cb, DamageScenario.Trim(0, 0.0127, 0.0127));
            cb.GetSignals(out FeedSignals sb);
            DamageScenario.Result one = DamageScenario.Hover(DamageScenario.Setup(spec, 1), out FlightCore co, DamageScenario.Trim(0, 0.0127));
            co.Propulsion.Rotor(0).Ratios(0, 0, out double t1, out double q1);
            cb.Propulsion.Rotor(0).Ratios(0, 0, out double t2, out double q2);
            FlightSetup bare = DamageScenario.Setup(spec, 1, pitch: false);
            bare.Propulsion.Rotor = null;
            DamageScenario.Result wrong = DamageScenario.Hover(bare); // the trim has nothing to act on: the published table flies
            DamageScenario.Result soundBare = DamageScenario.Hover(DamageScenario.Setup(spec, 1, pitch: false));
            double p1 = q1 / Math.Pow(t1, 1.5), p2 = q2 / Math.Pow(t2, 1.5);
            TestContext.Out.WriteLine($"sound {sound.Amps:F3} A (the OSD's last field {ss.MotorCurrentTotalA:F3} A); both blades trimmed {both.Amps:F3} A ({sb.MotorCurrentTotalA:F3} A), "
                + $"+{both.Amps - sound.Amps:F3} A, the rotor's power at its thrust × {p2:F4}; one blade {one.Amps:F3} A, {one.Amps - sound.Amps:+0.000;-0.000} A, × {p1:F4}; "
                + $"the known-wrong {wrong.Amps:F3} A against its sound {soundBare.Amps:F3} A");
            Assert.That(both.Amps, Is.GreaterThan(sound.Amps * 1.005), "a trimmed disc draws more");
            Assert.That(sb.MotorCurrentTotalA, Is.GreaterThan(ss.MotorCurrentTotalA), "the OSD's total shows it");
            Assert.That(Math.Abs(one.Amps - sound.Amps), Is.LessThan((p1 - 1) * 0.25 * sound.Amps), "one blade: within its rotor's share of the change");
            Assert.That(wrong.Amps, Is.LessThanOrEqualTo(soundBare.Amps * 1.001), "the known-wrong does not rise: it fails");
        }

        // The upside-down tip-over (D-083's case, the motors scraping the soil) recorded and replayed headless: the bells load,
        // every step bit for bit.
        [Test]
        public void ATippedLandingScrapesItsBellsAndReplaysBitForBit()
        {
            DroneSpec spec = Spec;
            var stream = new MemoryStream();
            FlightRecorder recorder = null;
            long steps = 0;
            double scraped = 0;
            FlightWorld world = StrikeScenarios.TrackWorld(out Double3 at);
            StrikeScenarios.TippedLanding(spec, world, at, RainLevel.Rain, SoilScenarios.Table(), false, started: (setup, core) =>
            {
                LogHeader header = LogHeader.For(setup, core);
                header.WorldId = "track";
                header.SetDrone(spec, spec.Nominal);
                recorder = new FlightRecorder(stream, header, FlightRecorder.DefaultCapacity, ownsStream: false);
            }, stepped: (core, stick) =>
            {
                recorder.RecordStep(core.State, stick, core.Trace, core.Events);
                for (int c = 0; c < 4; c++) scraped = Math.Max(scraped, core.Trace.BellScrape[c]);
                steps++;
            });
            recorder.Close();
            Assert.That(recorder.Fault, Is.Null);
            TestContext.Out.WriteLine($"  the bells' largest scrape {scraped:F4} N·m over {steps} steps");
            Assert.That(scraped, Is.GreaterThan(0), "the bells touch the soil");
            ContactScenarios.RepositoryMaterials(out ulong materials);
            ReplayResult replay = FlightReplay.Run(new MemoryStream(stream.ToArray()), id => world, spec.ContentHash, null, SoilScenarios.Table().Hash,
                materialsHash: materials);
            TestContext.Out.WriteLine($"  replay: {replay.Outcome}, {replay.Steps} steps; {replay.Message}");
            Assert.That(replay.Outcome, Is.EqualTo(ReplayOutcome.Match), replay.Message);
            Assert.That(replay.Steps, Is.EqualTo(steps));
        }

        // A pressed bell's step allocates nothing.
        [Test]
        public void APressedBellAllocatesNothing()
        {
            BellScenarios.Rig rig = BellScenarios.Dented(Spec, 0.0004);
            for (int k = 0; k < 100; k++) rig.Step(BellScenarios.Sky, default);
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 500; k++) rig.Step(BellScenarios.Sky, default);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            TestContext.Out.WriteLine($"  {allocated} B over 500 rubbing steps");
            Assert.That(allocated, Is.Zero);
        }
    }
}
