using System;
using System.Collections.Generic;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.World;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // p7g-legs 1.2: the legs' failure law (LegDamage) on the contact model's loads with the legs' side give, its strength
    // draws and its log. The touches are free bodies (LegScenarios: the throttle cut, the rigid-body landing of 1.1's onsets),
    // and the known-wrong of each is named. A damaged leg's consequences for the drone are 1.3's: here the state, the set,
    // the events and the leg's own give.
    public sealed class LegDamageTests
    {
        const double G = 9.80665;

        static DroneSpec Spec => RepositoryDrone.Spec();

        static MaterialRow[] Materials(out ulong hash) => ContactScenarios.RepositoryMaterials(out hash);

        static LegScenarios.Outcome Run(LegScenarios.Touch t)
        {
            MaterialRow[] m = Materials(out ulong h);
            return LegScenarios.Run(Spec, m, h, t);
        }

        static LegScenarios.Touch Down(double speed, ulong seed, double rollDeg = 0, double pitchDeg = 0) =>
            new LegScenarios.Touch { Velocity = new Double3(0, -speed, 0), Seed = seed, RollDeg = rollDeg, PitchDeg = pitchDeg };

        static string Describe(LegScenarios.Outcome o)
        {
            var parts = new List<string>();
            for (int c = 0; c < 4; c++)
                parts.Add($"{LegDamage.Names[c]} {o.Flags[c]} {LegScenarios.SetDegrees(o.Set[c]):F2}° (M {o.PeakMoment[c]:F2} N·m, P {o.PeakAxial[c]:F0} N)");
            return string.Join("; ", parts) + $"; tilt {o.MaxTilt:F1}°, {o.Events.Count} leg events, {o.Tunnels} tunnels";
        }

        // The legs' law of a flight drawn on a seed, for its capacities.
        static LegDamage Drawn(ulong seed)
        {
            MaterialRow[] m = Materials(out ulong h);
            return new LegDamage(Spec.BuildContacts(Spec.Nominal, m, h).Legs, new FlightRandom(seed));
        }

        // ---- A normal landing leaves every leg intact ---------------------------------------------------------------------

        [Test]
        public void ANormalLandingLeavesEveryLegIntact()
        {
            // Level within 2° at 0.5 m/s down, the throttle cut, on the rigid floor and on the range's straw and bare soil,
            // seeds 1–40: every leg intact, and the flight is the elastic legs' bit for bit (the record and the events, the law's
            // own judged moment aside). The known-wrong: the strength floor at its weakest bounds with no side give, the rigid
            // stick anchor of model 37 (26 kN/m across the foot), cracks a leg on the 2° touch.
            var sites = new List<(string, ContactScenarios.Site?, double, double)>
            {
                ("floor level", null, 0, 0), ("floor rolled 2°", null, 2, 0), ("floor pitched 2°", null, 0, 2), ("floor rolled and pitched 2°", null, 2, 2),
                ("S04's straw", RangeScenarios.At(RangeScenarios.Straw, 0), 2, 0), ("S04's bare soil", RangeScenarios.At(RangeScenarios.Soil, 0), 2, 0),
            };
            foreach ((string name, ContactScenarios.Site? site, double roll, double pitch) in sites)
            {
                double worst = 0;
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    LegScenarios.Touch t = Down(0.5, seed, roll, pitch);
                    t.Site = site;
                    t.Seconds = site == null ? 2 : 1.5;
                    LegScenarios.Outcome o = Run(t);
                    for (int c = 0; c < 4; c++) Assert.That(o.Flags[c], Is.EqualTo(LegFlags.None), $"{name}, seed {seed}: {Describe(o)}");
                    t.Elastic = true;
                    Assert.That(Run(t).Hash, Is.EqualTo(o.Hash), $"{name}, seed {seed}: the elastic legs' flight");
                    LegDamage d = Drawn(seed);
                    for (int c = 0; c < 4; c++) worst = Math.Max(worst, o.PeakMoment[c] / d.CrackMoment(c));
                }
                TestContext.Out.WriteLine($"{name}: every leg intact on seeds 1–40, the clamp's moment at most {worst:P0} of its leg's crack moment");
            }
        }

        // ---- A harsh landing bends a leg --------------------------------------------------------------------------------

        // The level touchdown speed at which a leg's bow first reaches its clamp's edge's first yield (the set's onset), by
        // energy on a quarter of the drone (undamped, so a lower bound; ζ 0.2 lowers the dynamic peak to about 0.82): the
        // spring's energy at the buckling load, P²/(2k), and the bow's shortening at that moment, P·Δ, Δ = 2.5816·w²/L with
        // the bow's w = M_c/(0.7151·P), less the weight's work over the compression.
        static double SetSpeed(LegDamage d, int leg, double mass)
        {
            LegDamageSpec s = d.Spec;
            double k = Spec.Legs.Stiffness, p = d.BucklingLoad;
            double mc = d.CrackMoment(leg, p);
            double w = mc / (LegDamage.BowMoment * p), bow = LegDamage.BowShortening * w * w / s.FreeLength;
            double quarter = mass / 4, energy = p * p / (2 * k) + p * bow - quarter * G * (p / k + bow);
            return Math.Sqrt(2 * energy / quarter);
        }

        [Test]
        public void AHarshLandingBendsALeg()
        {
            MaterialRow[] m = Materials(out ulong h);
            double mass = Spec.BuildMass(Spec.Nominal).Properties.Mass;
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                LegDamage d = Drawn(seed);
                int weakest = 0;
                double derived = double.PositiveInfinity;
                for (int c = 0; c < 4; c++)
                {
                    double v = SetSpeed(d, c, mass);
                    if (v < derived)
                    {
                        derived = v;
                        weakest = c;
                    }
                }
                // The sim's own onset: the lowest level touchdown speed (to 5 mm/s) that leaves a leg set.
                double lo = derived, hi = derived / 0.6;
                Assert.That(Run(Down(hi, seed)).Damaged(), Is.GreaterThan(0), "a leg sets at " + hi.ToString("F2") + " m/s");
                while (hi - lo > 0.005)
                {
                    double mid = (lo + hi) / 2;
                    if (Run(Down(mid, seed)).Damaged() > 0) hi = mid;
                    else lo = mid;
                }
                TestContext.Out.WriteLine($"seed {seed}: leg_{LegDamage.Names[weakest]} the weakest, its set onset {derived:F3} m/s undamped (derived), "
                    + $"{derived / 0.82:F3} m/s at ζ 0.2's 0.82; the sim's onset {hi:F3} m/s");
                Assert.That(hi / (derived / 0.82), Is.EqualTo(1).Within(0.1), "the sim's onset is the derivation's, damped");
                // The set's window: from the onset a set grows to a degree or two, and past about 1.06 times it a leg parts.
                foreach (double over in new[] { 1.0, 1.08, 1.15 })
                    TestContext.Out.WriteLine($"  at {over:F2}×, {hi * over:F3} m/s: {Describe(Run(Down(hi * over, seed)))}");
                double speed = hi * 1.04;
                LegScenarios.Outcome o = Run(Down(speed, seed)), elastic = Run(new LegScenarios.Touch { Velocity = new Double3(0, -speed, 0), Seed = seed, Elastic = true });
                TestContext.Out.WriteLine($"  at 1.04×, {speed:F3} m/s: {Describe(o)}");
                int set = 0;
                for (int c = 0; c < 4; c++)
                {
                    if ((o.Flags[c] & LegFlags.Set) == 0 || (o.Flags[c] & LegFlags.Broken) != 0) continue;
                    set++;
                    Assert.That(LegScenarios.SetDegrees(o.Set[c]), Is.GreaterThan(0), "its set angle is logged");
                    Assert.That(o.Events.Exists(e => e.Value.Kind == EventKind.LegSet && e.Value.Part == d.Spec.Parts[c]), "its LegSet event names it");
                }
                Assert.That(set, Is.GreaterThan(0), "a leg keeps a set after the drone rests");
                Assert.That(o.Core.State.Velocity.Length(), Is.LessThan(0.05), "the drone rests");
                for (int c = 0; c < 4; c++) Assert.That(elastic.Flags[c], Is.EqualTo(LegFlags.None), "the known-wrong elastic leg keeps none");
            }
        }

        // ---- Not parallel enough to the ground --------------------------------------------------------------------------

        [Test]
        public void NotParallelEnoughToTheGround()
        {
            // At 1.1 m/s down: level, then rolled right side down 10°, 20° and 30°, seeds 1–40. The rolled touch loads the first
            // skid's legs alone: the body turns about its centre of mass on their struts, and they bend across under it and bow.
            // The known-wrong splits the legs' load evenly over the four, along them. At the pilot's normal 0.5 m/s the shares are
            // a step (level and 10° intact on every seed, 20° and 30° damaged on all, most legs broken); at 1.1 m/s the
            // strengths' spread shows at 10° (33 of 40 seeds, none broken), and level stays intact.
            // Model 41 (p7g-legs 1.3): past its 47 N sway load a leg's side give is gone and the axial load on its deflection
            // bends it at the clamp (P·δ), so the second skid's legs, landing loaded while the body still rolls about their stuck
            // feet, crack too (80 of 80 at every roll: about 240 N on 8 mm); the first-touching legs' share still rises with the
            // roll (41, 66, 80 of 80; broken 0, 28, 80), and level stays intact.
            const double speed = 1.1;
            double[] rolls = { 0, 10, 20, 30 }, shares = new double[4];
            int[] firsts = new int[4], broken = new int[4];
            int levelEven = -1;
            for (int r = 0; r < 4; r++)
            {
                double roll = rolls[r];
                int damaged = 0, first = 0, other = 0, even = 0, parted = 0;
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    LegScenarios.Outcome o = Run(Down(speed, seed, roll));
                    if (o.Damaged() > 0) damaged++;
                    first += o.Damaged(0, 2);
                    other += o.Damaged(2, 4);
                    for (int c = 0; c < 2; c++)
                        if ((o.Flags[c] & LegFlags.Broken) != 0) parted++;
                    // The even split: a quarter of the contacts' force along each leg, which buckles it past its load.
                    LegDamage d = Drawn(seed);
                    for (int c = 0; c < 4; c++)
                        if (o.EvenAxial > d.BucklingLoad) even++;
                }
                shares[r] = damaged / 40.0;
                firsts[r] = first;
                broken[r] = parted;
                TestContext.Out.WriteLine($"{roll}°: {shares[r]:P0} of seeds damaged; {first} of the 80 first-touching legs damaged, {parted} of them broken; "
                    + $"{other} of the 80 others damaged; the even split {even} legs");
                if (roll == 0)
                {
                    levelEven = even;
                    Assert.That(damaged, Is.EqualTo(0), "level, every leg intact");
                    continue;
                }
                Assert.That(even, Is.LessThanOrEqualTo(levelEven), $"{roll}°: the known-wrong even split damages no more legs rolled than level");
                Assert.That(shares[r], Is.GreaterThanOrEqualTo(shares[r - 1]), $"{roll}°: a share at least the last roll's");
                Assert.That(firsts[r] + broken[r], Is.GreaterThanOrEqualTo(firsts[r - 1] + broken[r - 1]), $"{roll}°: the first-touching legs' damage rises");
            }
            Assert.That(shares[1], Is.GreaterThan(shares[0]), "rolled 10° damages more than level");
        }

        // ---- A hard strike breaks a leg ----------------------------------------------------------------------------------

        // The energy that parts a leg by a force across its foot (s = 0): the elastic work to the parting moment, F²/(2k_l), and
        // the plastic offset's δ_p from the edge's first yield to it, ∫ F dδ_p, by the law's own closed forms.
        static double PartingEnergy(LegDamage d, int leg, double kl)
        {
            LegDamageSpec s = d.Spec;
            double lever = d.Lever(leg), mc = d.CrackMoment(leg), f = s.ShapeFactor, kc = mc / (s.Modulus * s.SecondMoment);
            double top = d.PartingMoment(leg) / mc, scale = kc * lever * lever, energy = 0, last = 0;
            const int n = 4000;
            for (int i = 1; i <= n; i++)
            {
                double mm = 1 + (top - 1) * i / n, dp = LegDamage.OffsetRatio(mm, f) * scale;
                energy += (mm - (top - 1) / (2.0 * n)) * mc / lever * (dp - last);
                last = dp;
            }
            double force = top * mc / lever;
            return force * force / (2 * kl) + energy;
        }

        [Test]
        public void AHardStrikeBreaksALeg()
        {
            // The right rear foot meets a kerb's rigid steel edge sideways (the drone flying +x just over the floor): the leg
            // breaks at its clamp's edge, its weakest section (the edge factor's), and the event names the leg, the load and the
            // object hit. The speed: the parting energy over the drone's effective mass at the foot across it. The stub and the
            // parted piece as a loose body are 1.3's. The known-wrong: the elastic leg never breaks.
            MaterialRow[] m = Materials(out ulong h);
            MassProperties body = Spec.BuildMass(Spec.Nominal).Properties;
            ContactSpec contacts = Spec.BuildContacts(Spec.Nominal, m, h);
            const ulong seed = 7;
            LegDamage d = Drawn(seed);
            const int leg = 0; // rear right
            Double3 foot = d.Spec.Foot[leg], r = foot - body.CenterOfMass;
            double kl = contacts.LegSideStiffness * (contacts.FrictionStiffnessRatio * contacts.LegStiffness) / (contacts.LegSideStiffness + contacts.FrictionStiffnessRatio * contacts.LegStiffness);
            double effective = 1 / (1 / body.Mass + r.Z * r.Z / body.Inertia.YY + r.Y * r.Y / body.Inertia.ZZ);
            double energy = PartingEnergy(d, leg, kl), derived = Math.Sqrt(2 * energy / effective);
            TestContext.Out.WriteLine($"leg_rr: parts at {d.PartingMoment(leg):F2} N·m ({d.PartingMoment(leg) / d.Lever(leg):F1} N across the foot), "
                + $"{energy:F3} J; the drone's {effective:F3} kg across its foot: {derived:F3} m/s (derived)");
            FlightWorld world = FlightWorld.Floor();
            double speed = 2 * derived;
            var t = new LegScenarios.Touch
            {
                Velocity = new Double3(speed, 0, 0), Seed = seed, Seconds = 1,
                Start = new Double3(0, -foot.Y + 0.005 + 0.002, 0), // the skids 2 mm over the floor
            };
            // 20 mm tall: the skid and its supports meet it, the leg's tube (from 27 mm up) passes over.
            t.Objects = new[] { LegScenarios.Box(world, "kerb", new Double3(foot.X + 0.005 + 0.004 + 0.05, 0.01, foot.Z), new Double3(0.05, 0.01, 0.015)) };
            LegScenarios.Outcome o = Run(t);
            TestContext.Out.WriteLine($"into the kerb at {speed:F2} m/s: {Describe(o)}");
            foreach (var e in o.Events) TestContext.Out.WriteLine($"  step {e.Key}: {e.Value.Kind} ({(LegMode)e.Value.Index}) part {Spec.Parts[e.Value.Part].Name}, {e.Value.A:F3} past {e.Value.B:F3}, object {e.Value.Object}");
            Assert.That((o.Flags[leg] & LegFlags.Broken) != 0, "leg_rr breaks");
            KeyValuePair<long, FlightEvent> broke = o.Events.Find(e => e.Value.Kind == EventKind.LegBroken);
            Assert.That(broke.Value.Part, Is.EqualTo(d.Spec.Parts[leg]), "the event names the leg");
            Assert.That(broke.Value.A, Is.GreaterThanOrEqualTo(broke.Value.B), "its load passed its capacity");
            Assert.That(o.Core.World.Query.ObjectName(broke.Value.Object), Is.EqualTo("kerb"), "and the object hit");
            t.Elastic = true;
            LegScenarios.Outcome elastic = Run(t);
            Assert.That(elastic.Damaged(), Is.EqualTo(0), "the known-wrong elastic leg never breaks");
        }

        // ---- A leg comes off -------------------------------------------------------------------------------------------

        [Test]
        public void ALegComesOff()
        {
            // The right rear skid's overhang hooks under a rigid steel bar as the drone rises: the pull along the leg passes its
            // clamp's friction, 2μP with P = T/(K·d), the tube slides out past the grip's length, and the event names the leg and
            // the cause. The speed: the elastic energy to the slip and the slip's work over the grip, over the drone's effective
            // mass along the leg at the hook. Whether its skid hangs on the other leg or drops with it is 1.3's, with the T-joints'
            // capacities in place here. A twist alone on the jaws passes the bending first (μPD over the skid's lever needs more
            // across the end than the edge's first yield takes). The known-wrong: the elastic leg stays on.
            MaterialRow[] m = Materials(out ulong h);
            MassProperties body = Spec.BuildMass(Spec.Nominal).Properties;
            const ulong seed = 11;
            LegDamage d = Drawn(seed);
            const int leg = 0;
            Double3 foot = d.Spec.Foot[leg];
            var hook = new Double3(foot.X, foot.Y + 0.01, foot.Z - 0.06); // on the overhang, between the joint and the end
            Double3 r = hook - body.CenterOfMass;
            double effective = 1 / (1 / body.Mass + r.Z * r.Z / body.Inertia.XX + r.X * r.X / body.Inertia.ZZ);
            double k = Spec.Legs.Stiffness, slip = d.ClampSlip(leg), energy = slip * slip / (2 * k) + slip * d.Spec.GripLength;
            double derived = Math.Sqrt(2 * energy / effective);
            TestContext.Out.WriteLine($"leg_rr: its clamp holds {slip:F0} N of pull (2μP) and {d.ClampTwist(leg):F2} N·m of twist (μPD, {d.ClampTwist(leg) / 0.1175:F1} N "
                + $"across the skid's end, where the edge's first yield takes {d.CrackMoment(leg) / d.Lever(leg):F1} N); off after {energy:F2} J: {derived:F2} m/s up at "
                + $"{effective:F2} kg (derived)");
            FlightWorld world = FlightWorld.Floor();
            double speed = 1.5 * derived;
            var t = new LegScenarios.Touch { Velocity = new Double3(0, speed, 0), Seed = seed, Seconds = 0.5, Start = new Double3(0, 1, 0) };
            // The bar's underside 2 mm over the skid's top at the hook, across the right skid only.
            double skidTop = 1 + foot.Y + 0.01 + 0.005;
            t.Objects = new[] { LegScenarios.Box(world, "bar", new Double3(foot.X, skidTop + 0.002 + 0.01, hook.Z), new Double3(0.03, 0.01, 0.01)) };
            LegScenarios.Outcome o = Run(t);
            TestContext.Out.WriteLine($"up into the bar at {speed:F2} m/s: {Describe(o)}");
            foreach (var e in o.Events) TestContext.Out.WriteLine($"  step {e.Key}: {e.Value.Kind} ({(LegMode)e.Value.Index}) part {Spec.Parts[e.Value.Part].Name}, {e.Value.A:F1} past {e.Value.B:F1}");
            Assert.That((o.Flags[leg] & LegFlags.Detached) != 0, "leg_rr comes off");
            KeyValuePair<long, FlightEvent> off = o.Events.Find(e => e.Value.Kind == EventKind.LegDetached);
            Assert.That(off.Value.Part, Is.EqualTo(d.Spec.Parts[leg]), "the event names the leg");
            Assert.That((LegMode)off.Value.Index, Is.EqualTo(LegMode.ClampSlip), "and the cause");
            Assert.That(off.Value.A, Is.GreaterThan(off.Value.B), "its pull passed the jaws");
            t.Elastic = true;
            Assert.That(Run(t).Damaged(), Is.EqualTo(0), "the known-wrong elastic leg stays on");
            Assert.That(d.ClampTwist(leg) / 0.1175, Is.GreaterThan(d.CrackMoment(leg) / d.Lever(leg)), "a twist alone bends the leg first");
        }

        // ---- The strength draws cover their bounds -------------------------------------------------------------------------

        [Test]
        public void TheStrengthDrawsCoverTheirBounds()
        {
            // 80 000 flights' draws: each of the 8 strengths of each leg reaches within 0.5 % of its span of both its bounds, and
            // stays inside them; each flight's header lists its 32 draws with their bounds. The known-wrong: a draw over the
            // bounds' middle half misses both by 25 % of the span.
            MaterialRow[] m = Materials(out ulong h);
            LegDamageSpec spec = Spec.BuildContacts(Spec.Nominal, m, h).Legs;
            var lo = new Dictionary<string, double>();
            var hi = new Dictionary<string, double>();
            var bounds = new Dictionary<string, (double, double)>();
            for (ulong seed = 1; seed <= 80000; seed++)
            {
                var random = new FlightRandom(seed);
                new LegDamage(spec, random);
                foreach (ParameterDraw draw in random.Draws)
                {
                    string key = draw.Stream + "/" + draw.Parameter;
                    bounds[key] = (draw.Lo, draw.Hi);
                    lo[key] = lo.TryGetValue(key, out double a) ? Math.Min(a, draw.Value) : draw.Value;
                    hi[key] = hi.TryGetValue(key, out double b) ? Math.Max(b, draw.Value) : draw.Value;
                    Assert.That(draw.Value, Is.InRange(draw.Lo, draw.Hi), key);
                }
            }
            Assert.That(bounds.Count, Is.EqualTo(32), "8 strengths of 4 legs");
            foreach (KeyValuePair<string, (double Lo, double Hi)> b in bounds)
            {
                double span = b.Value.Hi - b.Value.Lo;
                Assert.That(span, Is.GreaterThan(0), b.Key);
                Assert.That((lo[b.Key] - b.Value.Lo) / span, Is.LessThan(0.005), b.Key + " reaches its low bound");
                Assert.That((b.Value.Hi - hi[b.Key]) / span, Is.LessThan(0.005), b.Key + " reaches its high bound");
                double narrowLo = b.Value.Lo + span / 4, narrowHi = b.Value.Hi - span / 4;
                Assert.That((narrowLo - b.Value.Lo) / span, Is.Not.LessThan(0.005), "the middle half misses the low bound");
                Assert.That((b.Value.Hi - narrowHi) / span, Is.Not.LessThan(0.005), "and the high");
            }
            // Each bound is the file's published or assumed one.
            Assert.That(bounds["leg.strength.rr/tube.yield_stress_pa"], Is.EqualTo((spec.YieldStress.Min, spec.YieldStress.Max)));
            Assert.That(bounds["leg.strength.fl/clamp.edge_stress_factor"], Is.EqualTo((spec.EdgeFactor.Min, spec.EdgeFactor.Max)));

            // A flight's header lists them, and they survive the log.
            FlightSetup setup = LegScenarios.Setup(Spec, m, h, Down(0.5, 0x450F));
            var core = new FlightCore(setup);
            LogHeader header = LogHeader.For(setup, core);
            LogHeader read = LogHeader.Read(new System.IO.MemoryStream(header.Encode()));
            int legs = 0;
            foreach (ParameterDraw draw in read.Draws)
            {
                if (!draw.Stream.StartsWith("leg.strength.", StringComparison.Ordinal)) continue;
                legs++;
                Assert.That(bounds[draw.Stream + "/" + draw.Parameter], Is.EqualTo((draw.Lo, draw.Hi)), "logged with its bounds");
            }
            Assert.That(legs, Is.EqualTo(32), "the header lists each flight's 32 leg draws");
            Assert.That(read.Contacts.Legs, Is.Not.Null, "and the law's data");
            Assert.That(read.Contacts.LegSideStiffness, Is.EqualTo(setup.Contacts.LegSideStiffness));
        }

        // The goldens' audit against model 37 (an explicit printer for the pins' notes): each flown on drone.json and on model
        // 37's leg law, compared record by record over model 37's 1904 bytes.
        [Test, Explicit]
        public void AuditTheGoldens()
        {
            foreach (string which in new[] { "powered", "rain", "contact", "range", "tumble" })
            {
                var now = new List<byte[]>();
                var was = new List<byte[]>();
                FlightCore a = null, b = null;
                ulong ha = 0, hb = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    DroneSpec spec = pass == 0 ? DroneSpec.Parse(RepositoryDrone.Text()) : GoldenFlights.WithModel37Legs(DroneSpec.Parse(RepositoryDrone.Text()));
                    List<byte[]> into = pass == 0 ? now : was;
                    Action<byte[]> keep = r => into.Add((byte[])r.Clone());
                    FlightCore core;
                    ulong hash = which == "powered" ? GoldenFlights.FlyPowered(spec, out core, onRecord: keep)
                        : which == "rain" ? GoldenFlights.FlyPowered(spec, out core, wind: Fpv450.Sim.Wind.WindSetup.Of(Fpv450.Sim.Wind.WindLevel.Calm), rain: Fpv450.Sim.Wind.RainLevel.SevereRain, onRecord: keep)
                        : which == "contact" ? GoldenFlights.FlyContact(spec, out core, onRecord: keep)
                        : which == "range" ? GoldenFlights.FlyRange(spec, out core, onRecord: keep)
                        : GoldenFlights.FlyTumble(spec, out core, out _);
                    if (which == "tumble")
                    {
                        // FlyTumble has no onRecord: hash over 1904 bytes only.
                        hash = GoldenFlights.FlyTumble(spec, out core, out _, layout: GoldenFlights.Model37Layout);
                    }
                    if (pass == 0) { a = core; ha = hash; } else { b = core; hb = hash; }
                }
                long first = -1;
                double far = 0, turn = 0;
                for (int k = 0; k < now.Count && k < was.Count; k++)
                {
                    bool same = true;
                    for (int i = 0; i < GoldenFlights.Model37Layout; i++) if (now[k][i] != was[k][i]) { same = false; break; }
                    if (!same && first < 0) first = k + 1;
                    LogFormat.DecodeStep(now[k], out FlightState sa, out _, out _);
                    LogFormat.DecodeStep(was[k], out FlightState sb, out _, out _);
                    far = Math.Max(far, (sa.Position - sb.Position).Length());
                    double dot = Math.Abs(sa.Attitude.X * sb.Attitude.X + sa.Attitude.Y * sb.Attitude.Y + sa.Attitude.Z * sb.Attitude.Z + sa.Attitude.W * sb.Attitude.W);
                    turn = Math.Max(turn, 2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI);
                }
                string legs = "";
                for (int c = 0; c < 4; c++) legs += " " + (LegFlags)(byte)(a.Trace.LegState >> (8 * c));
                TestContext.Out.WriteLine($"{which}: model 39 0x{ha:X16}; on model 37's legs 0x{hb:X16}; first differing step {first}; at most {far * 1e3:F1} mm and {turn:F2}° apart; "
                    + $"ends {(a.State.Position - b.State.Position).Length() * 1e3:F1} mm apart, at {a.State.Position.Y:F4} m against {b.State.Position.Y:F4}; "
                    + $"impacts {a.Contacts?.ImpactCount} against {b.Contacts?.ImpactCount}, crashed {a.State.Crashed}; legs{legs}; touching {a.Trace.ContactsTouching} against {b.Trace.ContactsTouching}");
            }
        }

        [Test]
        public void TheLawAllocatesNothingInAStep()
        {
            // Through a level landing at 2.2 m/s that bows, cracks, sets and breaks legs (seed 1), each step allocates 0 B.
            MaterialRow[] m = Materials(out ulong h);
            var core = new FlightCore(LegScenarios.Setup(Spec, m, h, Down(2.2, 1)));
            core.Advance(StickSample.Neutral);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 2000; k++) core.Advance(StickSample.Neutral);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            int broken = 0;
            for (int c = 0; c < 4; c++)
                if ((core.Contacts.Legs.Flags(c) & LegFlags.Broken) != 0) broken++;
            Assert.That(broken, Is.GreaterThan(0), "the landing breaks a leg");
            Assert.That(core.Loose.Count, Is.GreaterThan(0), "and its pieces fly loose (p7g-legs 1.3: the structure's change, the pieces' steps)");
            Assert.That(allocated, Is.EqualTo(0), "2000 steps through the law's every branch");
        }

        // ---- The law's closed forms ------------------------------------------------------------------------------------------

        [Test]
        public void TheBowsConstantsAreTheFixedPinnedModes()
        {
            // β the root of tan β = β; the mode w = C[sin kx − β cos kx − kx + β]: its peak, the clamp's moment over P·w_max, and
            // its shortening over w_max²/L, by quadrature (System.Math, a test's).
            double beta = 4.493409457909064;
            Assert.That(Math.Tan(beta) - beta, Is.EqualTo(0).Within(1e-9));
            Assert.That(beta * beta, Is.EqualTo(LegDamageSpec.FixedPinned).Within(1e-12));
            double peak = 0, sum = 0;
            const int n = 200000;
            for (int i = 0; i <= n; i++)
            {
                double th = beta * i / n;
                peak = Math.Max(peak, Math.Sin(th) - beta * Math.Cos(th) - th + beta);
                if (i == n) break;
                double mid = beta * (i + 0.5) / n, slope = Math.Cos(mid) + beta * Math.Sin(mid) - 1;
                sum += slope * slope * beta / n;
            }
            Assert.That(beta / peak, Is.EqualTo(LegDamage.BowMoment).Within(1e-9), "the clamp's moment over P·w_max");
            Assert.That(beta * sum / 2 / (peak * peak), Is.EqualTo(LegDamage.BowShortening).Within(1e-6), "the shortening over w²/L");
        }

        [Test]
        public void TheSetsClosedFormsIntegrateTheSpreadZone()
        {
            // θ_p and δ_p against the plastic curvature κ_p = κ_c·(g(m) − m) summed along the cantilever whose moment falls from m
            // at the clamp to 0 at the foot (System.Math's quadrature), at the tube's shape factor.
            double f = new LegDamageSpec { OuterDiameter = 0.01, InnerDiameter = 0.008 }.ShapeFactor;
            Assert.That(f, Is.EqualTo(1.403).Within(0.001), "the 10/8 mm tube's shape factor");
            foreach (double m in new[] { 1.05, 1.2, 1.35, 1.4 })
            {
                double theta = 0, delta = 0;
                const int n = 200000;
                double reach = 1 - 1 / m; // the zone's length over L
                for (int i = 0; i < n; i++)
                {
                    double x = reach * (i + 0.5) / n, local = m * (1 - x), kp = Math.Sqrt((f - 1) / (f - local)) - local;
                    theta += kp * reach / n;
                    delta += kp * (1 - x) * reach / n;
                }
                Assert.That(LegDamage.RotationRatio(m, f), Is.EqualTo(theta).Within(1e-4 * theta + 1e-9), $"θ_p at m {m}");
                Assert.That(LegDamage.OffsetRatio(m, f), Is.EqualTo(delta).Within(1e-4 * delta + 1e-9), $"δ_p at m {m}");
            }
            Assert.That(LegDamage.RotationRatio(1, f), Is.EqualTo(0), "none at the edge's first yield");
        }
    }
}
