using System;
using Fpv450.Sim.Flight;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7f-damage 1.2: the damaged rotor and the factory imbalance (prop-motor-damage's "A damaged rotor's thrust and torque",
    // "The once-per-revolution loads", "A new prop's factory imbalance"), with the blades trimmed or set by the test before
    // strikes exist. The flown scenarios are DamageScenario's.
    public sealed class DamageTests
    {
        const double TwoPi = 6.283185307179586, Rpm9000 = 9000 * TwoPi / 60;

        // A propulsion of the repository's drone with no draws, its rotors sound; corner 0 is the one a test damages.
        static Propulsion Sound(out PropulsionSpec spec)
        {
            spec = RepositoryDrone.Spec().BuildPropulsion();
            return new Propulsion(spec);
        }

        // Each rotor's thrust and torque at a speed in still air with no inflow (BeginStep on a state at rest).
        static void At(Propulsion p, double speed, out Corners thrust, out Corners torque)
        {
            var s = new FlightState { Attitude = Quat.Identity };
            for (int i = 0; i < 4; i++) s.RotorSpeed[i] = speed;
            p.BeginStep(s);
            thrust = default;
            torque = default;
            for (int i = 0; i < 4; i++)
            {
                thrust[i] = p.Thrust(i);
                torque[i] = p.Torque(i);
            }
        }

        // An independent blade-element momentum solution (the model DamagedRotor flies, written again station by station):
        // blades with their tips (r/R), chord shares and pitch offsets on stations x with chord c and angle over the zero-lift
        // line th (rad), spans h; λ_c the climb inflow. Returns C_T, with C_Q and each blade's C_T and Σ dC_T·x.
        static double Bemt(double[] tips, double[][] shares, double[] pitches, double[] x, double[] c, double[] th, double h, double radius,
            double lambdaC, out double cq, double[] bladeCt, double[] bladeCtx)
        {
            const double a = 5.7, cd0 = 0.049;
            int nb = tips.Length;
            double ct = 0;
            cq = 0;
            for (int pass = 0; pass < DamagedRotor.TipLossPasses; pass++)
            {
                double loss = 1 - Math.Sqrt(2 * Math.Max(ct, 0)) / nb, sumT = 0, sumQ = 0;
                Array.Clear(bladeCt, 0, nb);
                Array.Clear(bladeCtx, 0, nb);
                for (int k = 0; k < x.Length; k++)
                {
                    double sig = 0, sigTh = 0;
                    var lift = new double[nb];
                    var drag = new double[nb];
                    var chord = new double[nb];
                    for (int b = 0; b < nb; b++)
                    {
                        lift[b] = Math.Min(1, Math.Max(0, (loss * tips[b] - (x[k] - h / 2)) / h));
                        drag[b] = Math.Min(1, Math.Max(0, (tips[b] - (x[k] - h / 2)) / h));
                        chord[b] = c[k] * (shares == null ? 1 : shares[b][k]);
                        sig += chord[b] * lift[b] / (Math.PI * radius);
                        sigTh += chord[b] * lift[b] * (th[k] + pitches[b]) / (Math.PI * radius);
                    }
                    double s = sig * a / 16 - lambdaC / 2;
                    double lambda = sig > 0 ? Math.Sqrt(Math.Max(s * s + a * sigTh * x[k] / 8, 0)) - s : lambdaC;
                    for (int b = 0; b < nb; b++)
                    {
                        double dct = chord[b] * lift[b] / (Math.PI * radius) * (a / 2) * ((th[k] + pitches[b]) * x[k] * x[k] - lambda * x[k]) * h;
                        bladeCt[b] += dct;
                        bladeCtx[b] += dct * x[k];
                        sumT += dct;
                        sumQ += lambda * dct + chord[b] * drag[b] / (Math.PI * radius) * (cd0 / 2) * x[k] * x[k] * x[k] * h;
                    }
                }
                ct = sumT;
                cq = sumQ;
            }
            return ct;
        }

        // The sheet's blade at n stations from 0.1 R to the tip (the middles of equal spans): the chord law (18 mm at 0.1 R,
        // 26 mm at 0.3 R, 8 mm at the tip) and the 4.5-inch helix over the zero-lift line, from the laws themselves.
        static void Sheet(int n, out double[] x, out double[] c, out double[] th, out double h)
        {
            DroneSpec spec = RepositoryDrone.Spec();
            RotorFlappingSpec f = spec.BuildFlapping();
            h = 0.9 / n;
            x = new double[n];
            c = new double[n];
            th = new double[n];
            for (int k = 0; k < n; k++)
            {
                double r = 0.1 + (k + 0.5) * h;
                x[k] = r;
                c[k] = r < 0.3 ? 0.018 + (r - 0.1) / 0.2 * 0.008 : 0.026 - (r - 0.3) / 0.7 * 0.018;
                th[k] = f.HelixAngle(spec.Props.Pitch, r);
            }
        }

        // The file's 40 stations, their chord and their angle over the zero-lift line.
        static void Table(out double[] x, out double[] c, out double[] th, out double h)
        {
            PropBlade b = RepositoryDrone.Spec().Blade;
            x = b.Stations;
            c = b.Chord;
            h = b.Span;
            th = new double[x.Length];
            for (int k = 0; k < x.Length; k++) th[k] = b.SectionAngle[k] - RepositoryDrone.Spec().BuildRotor().ZeroLiftAngle;
        }

        // "A trimmed blade loses thrust from its outer span": one blade trimmed by 5, 12.7 and 25 mm and both by 5 mm, at 9000
        // rpm in still air: the rotor's thrust ratio is the BEMT's integral within 1e-9 relative, the loss passes the span's
        // removed share, a 5 mm one-blade trim costs 2–5 %, and 160 stations agree with the 40-station tables within 0.5 % of
        // the thrust at 6000, 9000 and 13 000 rpm (the BEMT's ratios have no Reynolds number, so the design's 9000 rpm reference
        // holds at every speed; the 160 stations are the sheet's laws themselves, not the table). The known-wrong, a loss in
        // proportion to the span removed, fails "more than the span's share" by construction.
        [Test]
        public void ATrimmedBladeLosesThrustFromItsOuterSpan()
        {
            Propulsion p = Sound(out PropulsionSpec spec);
            double radius = spec.Props.Diameter / 2;
            At(p, Rpm9000, out Corners sound, out _);
            Table(out double[] x40, out double[] c40, out double[] th40, out double h40);
            Sheet(160, out double[] x160, out double[] c160, out double[] th160, out double h160);
            var ct2 = new double[2];
            var ctx2 = new double[2];
            double intact40 = Bemt(new[] { 1.0, 1.0 }, null, new double[2], x40, c40, th40, h40, radius, 0, out double cqIntact, ct2, ctx2);
            double intact160 = Bemt(new[] { 1.0, 1.0 }, null, new double[2], x160, c160, th160, h160, radius, 0, out _, ct2, ctx2);
            double area = Math.PI * radius * radius, tip = Rpm9000 * radius;
            TestContext.Out.WriteLine($"intact sheet blade at 9000 rpm: {intact40 * 1.225 * area * tip * tip:F3} N (APC 12.43), κ {cqIntact * radius / intact40:F5} m (0.0144); 160 stations {intact160 * 1.225 * area * tip * tip:F3} N");
            foreach ((double[] cut, string name) in new[]
                     {
                         (new[] { 0.005, 0 }, "5 mm off one blade"), (new[] { 0.0127, 0 }, "12.7 mm off one blade"),
                         (new[] { 0.025, 0 }, "25 mm off one blade"), (new[] { 0.005, 0.005 }, "5 mm off both"),
                     })
            {
                p = Sound(out _);
                DamagedRotor r = p.Rotor(0);
                double[] tips = { 1 - cut[0] / radius, 1 - cut[1] / radius };
                for (int b = 0; b < 2; b++) r.Blade(b).Trim(tips[b]);
                p.Rebuild(0);
                At(p, Rpm9000, out Corners trimmed, out _);
                double ratio = trimmed[0] / sound[0];
                double bemt = Bemt(tips, null, new double[2], x40, c40, th40, h40, radius, 0, out _, ct2, ctx2) / intact40;
                double share = (cut[0] + cut[1]) / (2 * 0.9 * radius);
                TestContext.Out.WriteLine($"{name}: thrust ratio {ratio:F5} (BEMT {bemt:F5}), loss {1 - ratio:P2} against the span's {share:P2}");
                Assert.That(ratio, Is.EqualTo(bemt).Within(1e-9 * bemt), name + ": the BEMT's integral");
                Assert.That(1 - ratio, Is.GreaterThan(share), name + ": more than the span's share");
                if (name == "5 mm off one blade") Assert.That(1 - ratio, Is.InRange(0.02, 0.05), name);
                double fine = Bemt(tips, null, new double[2], x160, c160, th160, h160, radius, 0, out _, ct2, ctx2) / intact160;
                var whole = Sound(out _);
                foreach (double rpm in new[] { 6000.0, 9000, 13000 })
                {
                    // The trimmed rotor's thrust at this speed (the published table times its 40-station η) against the sound
                    // rotor's times the 160 stations' ratio.
                    At(p, rpm * TwoPi / 60, out Corners t, out _);
                    At(whole, rpm * TwoPi / 60, out Corners s, out _);
                    Assert.That(t[0], Is.EqualTo(s[0] * fine).Within(0.005 * t[0]), name + " at " + rpm + " rpm: 160 stations");
                }
                TestContext.Out.WriteLine($"  160 stations: {fine:F5} ({(ratio - fine) / fine:P3})");
            }
        }

        // "The intact blade is the published table": a rotor with its blades as elements, sound and without pitch offsets, is
        // the published table bit for bit at every speed and inflow (it skips the blades' path); the powered golden flown so keeps
        // model 26's bits (GoldenFlightTests.WithThePitchOffAndTheRodsInertiaEachGoldenIsModel26s). The known-wrong, the blade
        // elements' own thrust in place of the table, misses APC's published 12.43 N at 9000 rpm by about 14 % (the BEMT's tip
        // loss and annular inflow on p7c-wind 1.6's sections, which were fitted with a uniform inflow).
        [Test]
        public void TheIntactBladeIsThePublishedTable()
        {
            Propulsion p = Sound(out PropulsionSpec spec);
            Assert.That(p.Rotor(0), Is.Not.Null);
            foreach (double rpm in new[] { 1500.0, 6000, 9000, 15000 })
                foreach (double climb in new[] { -1.0, 0, 3, 12 })
                {
                    double w = rpm * TwoPi / 60;
                    var s = new FlightState { Attitude = Quat.Identity, Velocity = new Double3(0, climb, 0) };
                    for (int i = 0; i < 4; i++) s.RotorSpeed[i] = w;
                    p.BeginStep(s);
                    p.Aero(w, climb, out double t, out double q);
                    for (int i = 0; i < 4; i++)
                    {
                        Assert.That(BitConverter.DoubleToInt64Bits(p.Thrust(i)), Is.EqualTo(BitConverter.DoubleToInt64Bits(t)), rpm + " rpm, " + climb + " m/s: thrust");
                        Assert.That(BitConverter.DoubleToInt64Bits(p.Torque(i)), Is.EqualTo(BitConverter.DoubleToInt64Bits(q)), rpm + " rpm, " + climb + " m/s: torque");
                    }
                }
            double radius = spec.Props.Diameter / 2, ct = p.Rotor(0).IntactThrust(0), tip = Rpm9000 * radius;
            double bemt = ct * 1.225 * Math.PI * radius * radius * tip * tip;
            At(p, Rpm9000, out Corners table, out _);
            TestContext.Out.WriteLine($"9000 rpm: the table {table[0]:F3} N, the known-wrong blade elements' own {bemt:F3} N ({bemt / table[0] - 1:P1})");
            Assert.That(Math.Abs(bemt / table[0] - 1), Is.GreaterThan(0.05), "the known-wrong is not the published table");
        }

        // "The trimmed prop is bracketed by the published diameters": both blades trimmed to 0.9 R at 6000, 9000 and 12 000 rpm in
        // still air thrust between APC's 9x4.5MR (the nearest smaller MR of the same pitch, PER3_9x45MR.dat's static rows,
        // published) and the 10x4.5MR's table (drone.json's): a 9-inch cut keeps the 10-inch blade's wider tip. The known-wrong, the
        // trimmed prop as a 10x4.5MR scaled to 9 inches by the affinity law (thrust ∝ D⁴ at the same C_T), falls below the 9x4.5MR.
        [Test]
        public void TheTrimmedPropIsBracketedByThePublishedDiameters()
        {
            Propulsion p = Sound(out _);
            for (int b = 0; b < 2; b++) p.Rotor(0).Blade(b).Trim(0.9);
            p.Rebuild(0);
            foreach ((double rpm, double nine) in new[] { (6000.0, 3.859), (9000.0, 8.746), (12000.0, 15.706) })
            {
                At(p, rpm * TwoPi / 60, out Corners t, out _);
                double ten = t[1], trimmed = t[0], affinity = 0.9 * 0.9 * 0.9 * 0.9 * ten;
                TestContext.Out.WriteLine($"{rpm:F0} rpm: 0.9 R trim {trimmed:F3} N between the 9x4.5MR's {nine:F3} and the 10x4.5MR's {ten:F3} (η {trimmed / ten:F4}); the known-wrong D⁴ scaling {affinity:F3} N");
                Assert.That(trimmed, Is.InRange(nine, ten), rpm + " rpm");
                Assert.That(affinity, Is.LessThan(nine), "the known-wrong falls out of the bracket at " + rpm + " rpm");
            }
        }

        // "The loads follow their forms": a rotor with 12.7 mm off one blade, on the seed's factory draws (its unbalance and its
        // blades' pitch offsets), at 3000, 9000 and 15 000 rpm on a body held still: the hub's pull over a turn is U·ω² within
        // 1e-9 relative, U the lost material's first moment (from the blade's table, here) plus the factory draw; the hub moment
        // is the blades' thrust times their centroids, T·R·(Σ dC_T,0·x − Σ dC_T,1·x)/C_T, within 1e-9 relative; every load turns
        // with the rotor; on a sound prop with the factory draws off all vanish. The known-wrong, the factory unbalance alone (the
        // pull before 1.2), misses U·ω² by the lost mass's share.
        [Test]
        public void TheLoadsFollowTheirForms()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            PropulsionSpec ps = spec.BuildPropulsion();
            MotorAsymmetry.Draws draws = spec.BuildAsymmetry().DrawFor(new FlightRandom(0x450F));
            var p = new Propulsion(ps, draws);
            double radius = ps.Props.Diameter / 2, cut = 0.0127;
            DamagedRotor r = p.Rotor(0);
            r.Blade(0).Trim(1 - cut / radius);
            p.Rebuild(0);

            // The lost material's first moment along blade 0, from the table: each station's mass beyond the tip.
            PropBlade blade = spec.Blade;
            double tipX = 1 - cut / radius, h = blade.Span, first = 0;
            for (int k = 0; k < blade.Stations.Length; k++)
            {
                double x = blade.Stations[k], from = x - h / 2, mass = blade.Density * blade.Fill[k] * blade.Chord[k] * blade.Thickness[k] * radius * h;
                double inside = Math.Min(1, Math.Max(0, (tipX - from) / h));
                first += mass * (1 - inside) * (from + h * inside + (1 - inside) * h / 2) * radius;
            }
            double up = Math.Cos(draws.UnbalanceAngle[0]) * draws.Unbalance[0] - first, uq = -Math.Sin(draws.UnbalanceAngle[0]) * draws.Unbalance[0];
            double u = Math.Sqrt(up * up + uq * uq);
            TestContext.Out.WriteLine($"lost {r.LostMass * 1e3:F3} g, its first moment {first * 1e6:F3} g·mm against the factory {draws.Unbalance[0] * 1e6:F4} g·mm; J {p.Inertia(0):E4} of {spec.Motors[0].RotorInertia * draws.InertiaScale[0]:E4} kg·m²");
            Assert.That(-r.UnbalanceP, Is.EqualTo(first).Within(1e-12 * first), "the table's lost first moment");

            Table(out double[] x40, out double[] c40, out double[] th40, out _);
            var ct2 = new double[2];
            var ctx2 = new double[2];
            double[] pitch = { draws.BladePitch[0], draws.BladePitch[1] };
            double ct = Bemt(new[] { tipX, 1.0 }, null, pitch, x40, c40, th40, h, radius, 0, out _, ct2, ctx2);
            foreach (double rpm in new[] { 3000.0, 9000, 15000 })
            {
                double w = rpm * TwoPi / 60;
                At(p, w, out Corners thrust, out _);
                double moment = Math.Abs(thrust[0] * radius * (ctx2[0] - ctx2[1]) / ct);
                p.OneRevLoads(0, 0, w, out Double3 pull0, out Double3 drag0, out Double3 couple0);
                double lostOnly = Math.Abs(first) * w * w;
                for (int k = 0; k < 360; k++)
                {
                    double psi = k * TwoPi / 360;
                    p.OneRevLoads(0, psi, w, out Double3 pull, out Double3 drag, out Double3 couple);
                    Assert.That(pull.Length(), Is.EqualTo(u * w * w).Within(1e-9 * u * w * w), rpm + " rpm: U·ω² at " + k + "°");
                    Assert.That(couple.Length(), Is.EqualTo(moment).Within(1e-9 * moment), rpm + " rpm: the blades' thrust moment at " + k + "°");
                    foreach ((Double3 at0, Double3 now, string what) in new[] { (pull0, pull, "pull"), (drag0, drag, "drag"), (couple0, couple, "couple") })
                    {
                        Double3 turned = Turned(at0, psi, -1); // corner 0, rear right, turns clockwise
                        Assert.That((now - turned).Length(), Is.LessThan(1e-9 * at0.Length() + 1e-15), what + " turns with the rotor at " + k + "°");
                    }
                }
                double known = draws.Unbalance[0] * w * w;
                TestContext.Out.WriteLine($"{rpm:F0} rpm: pull {u * w * w:F3} N (the lost mass's {lostOnly:F3}), the known-wrong factory pull {known:F4} N; hub moment {moment:F4} N·m on {thrust[0]:F2} N; drag {drag0.Length():F4} N");
                Assert.That(Math.Abs(known - u * w * w), Is.GreaterThan(1e-3 * u * w * w), "the known-wrong misses U·ω²");
            }

            // A sound prop with the factory draws off: nothing.
            var sound = new Propulsion(ps);
            At(sound, Rpm9000, out _, out _);
            sound.OneRevLoads(0, 1, Rpm9000, out Double3 p1, out Double3 d1, out Double3 c1);
            Assert.That((p1, d1, c1), Is.EqualTo((default(Double3), default(Double3), default(Double3))), "a sound prop without draws");
            Assert.That(sound.Damaged, Is.EqualTo(0));
        }

        // A body vector at a rotor's angle 0 turned with the rotor to angle ψ (s the reaction sign, −1 clockwise).
        static Double3 Turned(Double3 v, double psi, double s) =>
            new Double3(v.X * Math.Cos(psi) - s * v.Z * Math.Sin(psi), 0, s * v.X * Math.Sin(psi) + v.Z * Math.Cos(psi));

        // "The pitch draws cover their bounds": 10 000 seeds' eight draws lie within ±0.25° and reach both ends within 0.5 % of
        // the bound; a flight's header lists its eight with the bound. The known-wrong, props without offsets (before 1.2),
        // reaches neither end.
        [Test]
        public void ThePitchDrawsCoverTheirBounds()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            MotorAsymmetry a = spec.BuildAsymmetry();
            double bound = a.BladePitchBound, lo = 0, hi = 0;
            Assert.That(bound, Is.EqualTo(0.25 * Math.PI / 180).Within(1e-15));
            for (ulong seed = 1; seed <= 10000; seed++)
            {
                double[] d = a.DrawFor(new FlightRandom(seed)).BladePitch;
                Assert.That(d, Has.Length.EqualTo(8));
                foreach (double x in d)
                {
                    Assert.That(x, Is.InRange(-bound, bound));
                    lo = Math.Min(lo, x);
                    hi = Math.Max(hi, x);
                }
            }
            TestContext.Out.WriteLine($"80 000 draws from {lo * 180 / Math.PI:F5}° to {hi * 180 / Math.PI:F5}° of ±0.25°");
            Assert.That(lo, Is.LessThan(-0.995 * bound));
            Assert.That(hi, Is.GreaterThan(0.995 * bound));
            Assert.That(0.0, Is.LessThan(0.995 * bound), "the known-wrong's offsets (all 0) reach no end");

            FlightSetup setup = GoldenFlights.PoweredSetup(spec);
            var core = new FlightCore(setup);
            Log.LogHeader header = Log.LogHeader.For(setup, core);
            int listed = 0;
            foreach (ParameterDraw d in header.Draws)
            {
                if (d.Stream != "prop.blade_pitch") continue;
                Assert.That((d.Lo, d.Hi), Is.EqualTo((-bound, bound)), d.Parameter);
                Assert.That(d.Value, Is.EqualTo(core.Draws.BladePitch[listed]), d.Parameter);
                listed++;
            }
            Assert.That(listed, Is.EqualTo(8), "the header's eight pitch draws");
        }

        // "A new blade is whole", the state's half (1.1b checked the tables): when a flight arms every blade's tip is at 1,
        // every chord share 1, every set 0 and its factory pitch the flight's draw. (No damage kind exists before 1.3's first
        // strike, so the flight's events can hold none.)
        [Test]
        public void ANewBladeIsWholeWhenTheFlightArms()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            FlightSetup setup = GoldenFlights.PoweredSetup(spec);
            var core = new FlightCore(setup);
            for (int k = 0; k < 40; k++) core.Advance(GoldenFlights.PoweredStick(k));
            Assert.That(core.State.Armed, Is.True);
            for (int i = 0; i < 4; i++)
                for (int b = 0; b < 2; b++)
                {
                    BladeState s = core.Propulsion.Rotor(i).Blade(b);
                    Assert.That(s.Whole, Is.True, "rotor " + i + " blade " + b);
                    Assert.That(s.TipRadius, Is.EqualTo(1));
                    Assert.That(s.ChordShare, Is.All.EqualTo(1.0));
                    Assert.That(s.PitchSet, Is.All.EqualTo(0.0));
                    Assert.That(s.FlapSet, Is.All.EqualTo(0.0));
                    Assert.That(s.FactoryPitch, Is.EqualTo(core.Draws.BladePitch[2 * i + b]), "the factory pitch is the draw");
                    Assert.That(core.Propulsion.Rotor(i).LostMass, Is.EqualTo(0));
                }
        }

        // "The baseline shake is bounded away from imperceptible", the suite's look (tools/flightbench --factory flies the 20 000
        // seeds, its band, its shares and FactoryBandHigh): on seeds 1–3 the hover with the factory draws shakes the camera's
        // 50–200 Hz band more than the same seed's hover without them (perfect props, the known-wrong, are that floor), its own
        // 1/rev share √(band² − band_off²) printed; the floor is each rotor's own wake (D-064) at the arms' mode near 60 Hz.
        [Test]
        public void TheFactoryImbalanceShakesAboveTheSameHoverWithout()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            for (ulong seed = 1; seed <= 3; seed++)
            {
                DamageScenario.Result on = DamageScenario.Hover(DamageScenario.Setup(spec, seed));
                DamageScenario.Result off = DamageScenario.Hover(DamageScenario.Setup(spec, seed, false, false));
                double line = Math.Sqrt(on.Band * on.Band - off.Band * off.Band);
                TestContext.Out.WriteLine($"seed {seed}: band {on.Band:E3} m/s² with the factory draws, {off.Band:E3} without; their own 1/rev share {line:E3}");
                Assert.That(on.Band, Is.GreaterThan(off.Band), "seed " + seed);
            }
        }

        // "A symmetric trim speeds its rotor up" (Pose et al. 2024's symmetric and one-tip findings): the drone hovers 10 s in
        // still air with both blades of the rear right rotor trimmed by 10 mm, and with one: the symmetric trim's rotor spins at
        // least 2 % faster than the others and the camera's 50–200 Hz band stays within twice the sound drone's; the one-blade
        // trim lifts the band above the factory band's 99.5th percentile, its spectrum peaking within 2 % of that rotor's rate.
        // The known-wrong, the one-blade trim's thrust loss with no 1/rev (both blades' pitch set down until the rotor loses as
        // much, a rotor that is weaker but even), stays inside the factory band.
        [Test]
        public void ASymmetricTrimSpeedsItsRotorUp()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            DamageScenario.Result sound = DamageScenario.Hover(DamageScenario.Setup(spec, 1));
            DamageScenario.Result both = DamageScenario.Hover(DamageScenario.Setup(spec, 1), DamageScenario.Trim(0, 0.010, 0.010));
            DamageScenario.Result one = DamageScenario.Hover(DamageScenario.Setup(spec, 1), out FlightCore core, DamageScenario.Trim(0, 0.010), 0, true);
            double others = (both.Speed[1] + both.Speed[2] + both.Speed[3]) / 3;
            double eta = core.Propulsion.Rotor(0).ThrustRatio(0);
            DamageScenario.Result even = DamageScenario.Hover(DamageScenario.Setup(spec, 1), Even(eta));
            TestContext.Out.WriteLine($"sound: band {sound.Band:E3} m/s², rotors {sound.Speed[0]:F1}/{sound.Speed[1]:F1}/{sound.Speed[2]:F1}/{sound.Speed[3]:F1} rad/s");
            TestContext.Out.WriteLine($"both blades 10 mm: the rotor {both.Speed[0]:F1} rad/s against the others' {others:F1} (+{both.Speed[0] / others - 1:P2}), band {both.Band:E3} ({both.Band / sound.Band:F2} × sound)");
            TestContext.Out.WriteLine($"one blade 10 mm (η {eta:F4}): band {one.Band:E3} (the factory's 99.5 % {DamageScenario.FactoryBandHigh:E3}), peak {one.PeakHz:F1} Hz at the rotor's {one.RotorHz:F2} Hz");
            TestContext.Out.WriteLine($"known-wrong, the same loss even (no 1/rev): band {even.Band:E3}");
            Assert.That(both.Speed[0], Is.GreaterThanOrEqualTo(1.02 * others), "the symmetric trim's rotor spins faster");
            Assert.That(both.Band, Is.LessThanOrEqualTo(2 * sound.Band), "the symmetric trim adds little 1/rev");
            Assert.That(one.Band, Is.GreaterThan(DamageScenario.FactoryBandHigh), "the one-blade trim's shake");
            Assert.That(one.PeakHz, Is.EqualTo(one.RotorHz).Within(0.02 * one.RotorHz), "at the damaged rotor's rate");
            Assert.That(even.Band, Is.LessThanOrEqualTo(DamageScenario.FactoryBandHigh), "the known-wrong stays inside the factory band");
        }

        // The rear right rotor's two blades pitched down alike until its static thrust ratio is eta: a loss with no 1/rev.
        static Action<Propulsion> Even(double eta) => p =>
        {
            DamagedRotor r = p.Rotor(0);
            double lo = -0.2, hi = 0;
            for (int n = 0; n < 60; n++)
            {
                double set = (lo + hi) / 2;
                for (int b = 0; b < 2; b++)
                    for (int k = 0; k < r.Blade(b).PitchSet.Length; k++) r.Blade(b).PitchSet[k] = set;
                p.Rebuild(0);
                if (r.ThrustRatio(0) > eta) hi = set; else lo = set;
            }
        };

        // "The loop is not fooled, the camera is": the drone hovers 10 s with one blade of the rear right rotor trimmed by
        // 12.7 mm, on the builder's filter chain: the attitude's peak to peak over the second 5 s is under 2°, the gyro's 1/rev
        // after the chain (RPM notches, dynamic notches, lowpasses) is at least 10 times weaker than the body rate's before it,
        // and the camera's 50–200 Hz band is above the factory band's 99.5th percentile. The known-wrong, the same thrust loss
        // with no 1/rev (both blades pitched down alike, a scripted loss), leaves the camera inside the factory band. The chain
        // without its notches is printed beside: its lowpasses alone take the 1/rev down about 13 times.
        [Test]
        public void TheLoopIsNotFooledTheCameraIs()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            DamageScenario.Result r = DamageScenario.Hover(DamageScenario.Setup(spec, 1), out FlightCore core, DamageScenario.Trim(0, 0.0127));
            DamageScenario.Result even = DamageScenario.Hover(DamageScenario.Setup(spec, 1), Even(core.Propulsion.Rotor(0).ThrustRatio(0)));
            FlightSetup bare = DamageScenario.Setup(spec, 1);
            Control.FcSettings fc = bare.Settings;
            fc.rpm_filter_harmonics = 0;
            fc.dyn_notch_count = 0;
            bare.Settings = fc;
            DamageScenario.Result w = DamageScenario.Hover(bare, DamageScenario.Trim(0, 0.0127));
            TestContext.Out.WriteLine($"12.7 mm off one blade: roll {r.RollPeakToPeak:F3}° and pitch {r.PitchPeakToPeak:F3}° peak to peak, the gyro's 1/rev {r.GyroOneRev:F3} °/s "
                + $"before the chain and {r.FilteredOneRev:F4} after ({r.GyroOneRev / r.FilteredOneRev:F0} ×), band {r.Band:E3} m/s² (the factory's 99.5 % {DamageScenario.FactoryBandHigh:E3})");
            TestContext.Out.WriteLine($"the chain without its RPM and dynamic notches: {w.GyroOneRev:F3} → {w.FilteredOneRev:F4} °/s ({w.GyroOneRev / w.FilteredOneRev:F0} ×), roll {w.RollPeakToPeak:F3}°, pitch {w.PitchPeakToPeak:F3}°");
            TestContext.Out.WriteLine($"known-wrong, the same loss with no 1/rev: band {even.Band:E3} m/s²");
            Assert.That(Math.Max(r.RollPeakToPeak, r.PitchPeakToPeak), Is.LessThan(2), "the loop holds the attitude");
            Assert.That(r.GyroOneRev, Is.GreaterThanOrEqualTo(10 * r.FilteredOneRev), "the chain notches the 1/rev");
            Assert.That(r.Band, Is.GreaterThan(DamageScenario.FactoryBandHigh), "the camera shakes");
            Assert.That(even.Band, Is.LessThanOrEqualTo(DamageScenario.FactoryBandHigh), "the known-wrong's camera does not");
        }
    }
}
