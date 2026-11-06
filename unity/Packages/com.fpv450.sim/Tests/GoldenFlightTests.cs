using System;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Tether;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;
using NUnit.Framework;

namespace Fpv450.Sim.Tests
{
    // The golden flights (GoldenFlights) on this runtime: .NET in tools/simtests, Mono in Unity's EditMode run.
    // The same bits on both is LOG-04's runtime half for the flight controller, propulsion and the contacts.
    public sealed class GoldenFlightTests
    {
        [Test]
        public void ThePoweredGoldenFlightIsPinned()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            ulong hash = GoldenFlights.FlyPowered(spec, out FlightCore core);
            TestContext.Out.WriteLine("powered golden: 0x" + hash.ToString("X16") + " after " + core.State.Step + " steps, armed "
                + core.State.Armed + ", height " + core.State.Position.Y.ToString("0.00") + " m");
            Assert.That(core.State.Armed, Is.True, "the scripted pilot armed and flew");
            Assert.That(core.State.RotorSpeed.FrontLeft, Is.GreaterThan(500), "the motors turn");
            Assert.That(GoldenFlights.FlyPowered(spec, out _), Is.EqualTo(hash), "a second flight gives the same bits");
            Assert.That(hash, Is.EqualTo(GoldenFlights.PoweredHash), "the pinned golden (a re-record is audited and raises the model version)");
        }

        // 1.6's audit, kept across the merge with 1.4b and model 6 (1.7): the contacts change nothing for a flight that
        // touches nothing. Over the record less 1.6's 120 contact bytes (1.4b's 1176-byte layout) the powered flight with
        // its contacts is the same flight without them, bit for bit, record and events. At model 5 both were 1.4b's
        // pinned 0x3695FC49DCA906DC.
        [Test]
        public void ThePoweredFlightTouchesNothing()
        {
            // Since model 39 the legs' state block (a flight without contacts leaves it zero) is left out with them.
            ulong with = GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore core, ContactBytesAt, ContactBytes, layout: LegAt);
            ulong without = GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore bare, ContactBytesAt, ContactBytes, false, layout: LegAt);
            Assert.That(core.Contacts, Is.Not.Null, "the flight carries its contacts");
            Assert.That(bare.Contacts, Is.Null);
            Assert.That(core.Contacts.ImpactCount, Is.EqualTo(0));
            Assert.That(with, Is.EqualTo(without), "1.4b's layout, every bit, with the contacts and without them");
        }

        // Model 73 (the physics integration 2 with D-086's controller: 69 merged onto 70), its audit against model 70, kept: with
        // Betaflight's airmode on, as every model before 69 flew it, each golden is its model 70 pin over the whole record, every
        // record and event; without it each is its model 73 pin. So the controller is the whole of 73's move against 70. The tumble
        // has no controller and keeps its pin. Since model 75 (72 merged onto 73) over model 73's 3360 bytes, the clean sensors.
        // Since model 77 (74's bent nozzle on the line, and 77's chain) the tether golden with airmode on, which flies the linear
        // step (GoldenFlights.FlyContact), is model 74's pin, and without it (the linear step over 73's layout) the merge's: so it is
        // also 77's audit against 74, the controller the whole of the merge's move (WithTheLinearStepTheTetherGoldenIsTheMerges
        // splits off the chain).
        [Test]
        public void WithAirmodeEachGoldenIsModel70s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            WindSetup calm = WindSetup.Of(WindLevel.Calm);
            Assert.That(FcSettings.Default.airmode, Is.False);
            const int m73 = GoldenFlights.Model73Layout; // since model 75 over model 73's 3360 bytes, the clean sensors
            Audit("powered", "model 70", (air, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, onRecord: keep, airmode: air, layout: m73), c),
                GoldenFlights.Model70PoweredHash, GoldenFlights.Model73PoweredHash);
            Audit("rain", "model 70", (air, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, wind: calm, rain: RainLevel.SevereRain, onRecord: keep, airmode: air, layout: m73), c),
                GoldenFlights.Model70RainPoweredHash, GoldenFlights.Model73RainPoweredHash);
            Audit("contact", "model 70", (air, keep) => (GoldenFlights.FlyContact(spec, out FlightCore c, onRecord: keep, airmode: air, layout: m73), c),
                GoldenFlights.Model70ContactHash, GoldenFlights.Model73ContactHash);
            Audit("range", "model 70", (air, keep) => (GoldenFlights.FlyRange(spec, out FlightCore c, onRecord: keep, airmode: air, layout: m73), c),
                GoldenFlights.Model70RangeHash, GoldenFlights.Model73RangeHash);
            Audit("tether", "model 74", (air, keep) => (GoldenFlights.FlyContact(spec, out FlightCore c, tether: true, onRecord: keep, airmode: air, layout: m73), c),
                GoldenFlights.Model74TetherHash, GoldenFlights.Model77MergeTetherHash);
        }

        // Model 69 (D-086: the pilot flies without airmode, FcSettings.Default's since 69), its audit against model 65, kept: with
        // Betaflight's airmode on, as every model before 69 flew it, each golden is its model 65 pin, every record and event; without
        // it each is its model 69 pin. So airmode is the whole move. The tumble has no controller and keeps its pin. Since model 73
        // (69 merged onto 70) over model 63's 3040 bytes, the motors' heat off (GoldenFlights' heat over a parent's layout) and the
        // shafts on (no golden's shaft passes its yield, with airmode or without): so it is also 73's audit against 69, the heat
        // and the shafts' zero block the whole of 73's move against it.
        [Test]
        public void WithAirmodeEachGoldenIsModel65s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            WindSetup calm = WindSetup.Of(WindLevel.Calm);
            const int m63 = GoldenFlights.Model63Layout;
            Assert.That(FcSettings.Default.airmode, Is.False);
            Audit("powered", "model 65", (air, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, onRecord: keep, airmode: air, layout: m63), c),
                GoldenFlights.Model65PoweredHash, GoldenFlights.Model69PoweredHash);
            Audit("rain", "model 65", (air, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, wind: calm, rain: RainLevel.SevereRain, onRecord: keep, airmode: air, layout: m63), c),
                GoldenFlights.Model65RainPoweredHash, GoldenFlights.Model69RainPoweredHash);
            Audit("contact", "model 65", (air, keep) => (GoldenFlights.FlyContact(spec, out FlightCore c, onRecord: keep, airmode: air, layout: m63), c),
                GoldenFlights.Model65ContactHash, GoldenFlights.Model69ContactHash);
            Audit("range", "model 65", (air, keep) => (GoldenFlights.FlyRange(spec, out FlightCore c, onRecord: keep, airmode: air, layout: m63), c),
                GoldenFlights.Model65RangeHash, GoldenFlights.Model69RangeHash);
            // Since model 77 (74's bent nozzle on the line) the tether flies model 70's drone.json, its nozzle straight (Model70Drone).
            DroneSpec straight = Model70Drone.Spec();
            Audit("tether", "model 65", (air, keep) => (GoldenFlights.FlyContact(straight, out FlightCore c, tether: true, onRecord: keep, airmode: air, layout: m63), c),
                GoldenFlights.Model65TetherHash, GoldenFlights.Model69TetherHash);
        }

        // Model 75 (the physics integration 2 with the losses: 72 merged onto 73), its audit against model 73, kept: with the clean
        // sensors (GoldenFlights.CleanSensors: the arms stiff in their plane, no gyro range; every flight over a layout short of the
        // record) each golden over model 73's 3360 bytes is its model 73 pin, every record and event, the arms' block zero on every
        // record; with them each is its pin. So the arms and the gyro's range are the whole of 75's move against 73.
        // Since model 78 (77 merged onto 75) it is 78's audit against model 77 too: 77's record is 73's 3360 bytes and 77 kept 73's
        // pins but the tether's, so with the clean sensors the tether golden on the repository's drone.json (the bent nozzle, the
        // Newton step) is model 77's pin, every record and event; on model 70's drone.json (Model70Drone, its nozzle straight) with
        // the known-wrong linear step it is model 73's. So 75's arms and gyro range are the whole of 78's move against 77.
        [Test]
        public void WithTheCleanSensorsEachGoldenIsModel77s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            WindSetup calm = WindSetup.Of(WindLevel.Calm);
            const int m73 = GoldenFlights.Model73Layout;
            int block = 0;
            Action<byte[]> Zero(bool clean, Action<byte[]> keep) => r =>
            {
                keep(r);
                if (clean) for (int i = m73; i < LogFormat.StepSize; i++) if (r[i] != 0) block++;
            };
            int Layout(bool clean) => clean ? m73 : LogFormat.StepSize;
            const string sw = "the clean sensors";
            Audit("powered", "model 77", (clean, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, onRecord: Zero(clean, keep), layout: Layout(clean)), c),
                GoldenFlights.Model73PoweredHash, GoldenFlights.PoweredHash, sw);
            Audit("rain", "model 77", (clean, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, wind: calm, rain: RainLevel.SevereRain, onRecord: Zero(clean, keep), layout: Layout(clean)), c),
                GoldenFlights.Model73RainPoweredHash, GoldenFlights.RainPoweredHash, sw);
            Audit("contact", "model 77", (clean, keep) => (GoldenFlights.FlyContact(spec, out FlightCore c, onRecord: Zero(clean, keep), layout: Layout(clean)), c),
                GoldenFlights.Model73ContactHash, GoldenFlights.ContactHash, sw);
            Audit("range", "model 77", (clean, keep) => (GoldenFlights.FlyRange(spec, out FlightCore c, onRecord: Zero(clean, keep), layout: Layout(clean)), c),
                GoldenFlights.Model73RangeHash, GoldenFlights.RangeHash, sw);
            Audit("tether", "model 77", (clean, keep) => (GoldenFlights.FlyContact(spec, out FlightCore c, tether: true, onRecord: Zero(clean, keep), layout: Layout(clean),
                linearStep: false), c), GoldenFlights.Model77TetherHash, GoldenFlights.TetherHash, sw);
            Assert.That(GoldenFlights.FlyContact(Model70Drone.Spec(), out FlightCore old, tether: true, onRecord: Zero(true, _ => { }), layout: m73),
                Is.EqualTo(GoldenFlights.Model73TetherHash), "tether on model 70's drone.json with the linear step: model 73's");
            Assert.That(old.Tether.Chain.NewtonSolves, Is.Zero, "the linear step");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m73), Is.EqualTo(GoldenFlights.Model73TumbleHash), "tumble");
            Assert.That(block, Is.EqualTo(0), "the arms' block is zero with the clean sensors");
        }

        // Model 75, its audit against model 72 (p7f-damage 1.6's losses, on 68): with Betaflight's airmode on (as 72 flew) and the
        // motors' heat off (ContactSpec.MotorHeat, as 72 flew), the arms and the gyro's range on, each golden over model 72's bytes
        // (the record's first 3168 and its last 176: the heat's 192 at Model68Layout skipped, GoldenFlights.HeatSkip) is its model
        // 72 pin, every record and event, the heat's block zero on every record; without them each is its pin. So D-086's controller
        // and the heat are the whole of 75's move against 72. Since model 78 (77's bent nozzle and chain on the line) the tether
        // flies model 70's drone.json (Model70Drone, its nozzle straight) with airmode on, so with the linear step, as 72 flew.
        [Test]
        public void WithAirmodeAndWithoutTheHeatEachGoldenIsModel72s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            WindSetup calm = WindSetup.Of(WindLevel.Calm);
            const int at = GoldenFlights.Model68Layout, skip = GoldenFlights.HeatSkip;
            int block = 0;
            Action<byte[]> Zero(bool old, Action<byte[]> keep) => r =>
            {
                keep(r);
                if (old) for (int i = at; i < at + skip; i++) if (r[i] != 0) block++;
            };
            int At(bool old) => old ? at : LogFormat.StepSize;
            int Skip(bool old) => old ? skip : 0;
            const string sw = "airmode on and the heat off";
            Audit("powered", "model 72", (old, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, At(old), Skip(old), onRecord: Zero(old, keep), heat: !old, airmode: old), c),
                GoldenFlights.Model72PoweredHash, GoldenFlights.PoweredHash, sw);
            Audit("rain", "model 72", (old, keep) => (GoldenFlights.FlyPowered(spec, out FlightCore c, At(old), Skip(old), wind: calm, rain: RainLevel.SevereRain, onRecord: Zero(old, keep),
                heat: !old, airmode: old), c), GoldenFlights.Model72RainPoweredHash, GoldenFlights.RainPoweredHash, sw);
            Audit("contact", "model 72", (old, keep) => (GoldenFlights.FlyContact(spec, out FlightCore c, At(old), Skip(old), onRecord: Zero(old, keep), heat: !old, airmode: old), c),
                GoldenFlights.Model72ContactHash, GoldenFlights.ContactHash, sw);
            Audit("range", "model 72", (old, keep) => (GoldenFlights.FlyRange(spec, out FlightCore c, At(old), Skip(old), onRecord: Zero(old, keep), heat: !old, airmode: old), c),
                GoldenFlights.Model72RangeHash, GoldenFlights.RangeHash, sw);
            DroneSpec straight = Model70Drone.Spec();
            Audit("tether", "model 72", (old, keep) => (GoldenFlights.FlyContact(old ? straight : spec, out FlightCore c, At(old), Skip(old), tether: true, onRecord: Zero(old, keep),
                heat: !old, airmode: old), c), GoldenFlights.Model72TetherHash, GoldenFlights.TetherHash, sw);
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, at, skip), Is.EqualTo(GoldenFlights.Model72TumbleHash), "tumble (no rotor: the heat's zero block alone)");
            Assert.That(block, Is.EqualTo(0), "the heat's block is zero without the heat");
        }

        // Model 78 (77 merged onto 75), its audit against model 75: with the known-wrong linear step (FlightSetup.TetherLinearStep,
        // the chain before 77) on model 70's drone.json (Model70Drone: its nozzle straight on the 15° canister) with model 70's exit,
        // 4 mm lip and peel referred to 1 m/s (GoldenFlights.Model70Tether) the tether golden is model 75's pin over the whole record,
        // every record and event; every golden without the line is 75's own pin (the line is 77's alone, and the bent nozzle meets
        // nothing in them). So 77's exit, edge, peel and Newton step are the whole of 78's move against 75.
        [Test]
        public void WithTheLinearStepAndTheOldExitTheTetherGoldenIsModel75s()
        {
            DroneSpec spec = RepositoryDrone.Spec(), straight = Model70Drone.Spec();
            Audit("tether", "model 75", (old, keep) => (GoldenFlights.FlyContact(old ? straight : spec, out FlightCore c, tether: true, onRecord: keep, linearStep: old), c),
                GoldenFlights.Model75TetherHash, GoldenFlights.TetherHash, "the linear step and model 70's exit, lip and peel");
        }

        static void Audit(string name, string model, Func<bool, Action<byte[]>, (ulong hash, FlightCore core)> fly, ulong older, ulong pin, string sw = "airmode")
        {
            var on = new System.Collections.Generic.List<FlightState>();
            var off = new System.Collections.Generic.List<FlightState>();
            var onBits = new System.Collections.Generic.List<ulong>();
            var offBits = new System.Collections.Generic.List<ulong>();
            Action<byte[]> Keep(System.Collections.Generic.List<FlightState> into, System.Collections.Generic.List<ulong> bits) => r =>
            {
                LogFormat.DecodeStep(r, out FlightState st, out _, out _);
                into.Add(st);
                bits.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length));
            };
            (ulong a, FlightCore ca) = fly(true, Keep(on, onBits));
            (ulong b, FlightCore cb) = fly(false, Keep(off, offBits));
            int first = 0;
            while (first < onBits.Count && first < offBits.Count && onBits[first] == offBits[first]) first++;
            double far = 0, turn = 0, rateOn = 0, rateOff = 0;
            string marks = "";
            for (int k = 0; k < on.Count && k < off.Count; k++)
            {
                far = Math.Max(far, (on[k].Position - off[k].Position).Length());
                Quat qa = on[k].Attitude, qb = off[k].Attitude;
                double dot = Math.Abs(qa.X * qb.X + qa.Y * qb.Y + qa.Z * qb.Z + qa.W * qb.W), angle = 2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI;
                turn = Math.Max(turn, angle);
                rateOn = Math.Max(rateOn, on[k].BodyRate.Length() * 180 / Math.PI);
                rateOff = Math.Max(rateOff, off[k].BodyRate.Length() * 180 / Math.PI);
                if ((k + 1) % 2000 == 0 || k + 1 == 5500) marks += $" {(k + 1) / 1000.0:0.#} s {angle:F1}°, {(on[k].Position - off[k].Position).Length():F2} m;";
            }
            FlightState ea = ca.State, eb = cb.State;
            string Contacts(FlightCore c) => c.Contacts == null ? "" : $", {c.Contacts.ImpactCount} impacts, {c.Trace.ContactsTouching} touching";
            string Line(FlightCore c) => c.Tether == null ? "" : c.Tether.Broken ? $", the line broken at step {c.Tether.BrokenAt} by {c.Tether.LastBreak.Mechanism}" : $", the line whole, {c.Tether.PaidOut:F3} m paid out";
            TestContext.Progress.WriteLine($"{name}: {sw} on 0x{a:X16} ({model} 0x{older:X16}), off 0x{b:X16}; the records the same for {first} of {offBits.Count} steps; "
                + $"at most {far * 1e3:F1} mm and {turn:F2}° apart (at{marks}), the largest body rate {rateOn:F0} °/s on and {rateOff:F0} off, the ends {(ea.Position - eb.Position).Length() * 1e3:F1} mm apart; on: {ea.Position.Y:F4} m up, armed {ea.Armed}, crashed {ea.Crashed}{Contacts(ca)}{Line(ca)}; "
                + $"off: {eb.Position.Y:F4} m up, armed {eb.Armed}, crashed {eb.Crashed}{Contacts(cb)}{Line(cb)}");
            Assert.That(a, Is.EqualTo(older), name + " with " + sw + ": " + model + "'s");
            Assert.That(b, Is.EqualTo(pin), name + " without: the pin");
        }

        // QA's review-base finding 3: a second guard on the gyroscopic term, on the drone's own body.
        [Test]
        public void TheTumbleGoldenIsPinned()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            ulong hash = GoldenFlights.FlyTumble(spec, out FlightCore core, out GoldenFlights.TumbleChecks c);
            TestContext.Out.WriteLine("tumble golden: 0x" + hash.ToString("X16") + " after " + core.State.Step + " steps: momentum within "
                + c.Momentum.ToString("E2") + ", energy within " + c.Energy.ToString("E2") + ", the CoM " + c.Parabola.ToString("E2")
                + " m from its parabola, the body rate wandering " + c.Wander.ToString("F3") + " rad/s");
            Assert.That(c.Momentum, Is.LessThan(1e-10), "the angular momentum about the CoM, in the world");
            Assert.That(c.Energy, Is.LessThan(1e-10), "the rotational energy (the midpoint rate)");
            Assert.That(c.Parabola, Is.LessThan(1e-9), "the CoM on semi-implicit Euler's exact parabola");
            Assert.That(c.Wander, Is.GreaterThan(0.5), "the body rate moves in body axes: ω × I·ω");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _), Is.EqualTo(hash), "a second flight gives the same bits");
            Assert.That(hash, Is.EqualTo(GoldenFlights.TumbleHash), "the pinned golden (a re-record is audited and raises the model version)");
        }

        [Test]
        public void TheContactGoldenFlightIsPinned()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            ulong hash = GoldenFlights.FlyContact(spec, out FlightCore core);
            TestContext.Out.WriteLine("contact golden: 0x" + hash.ToString("X16") + " after " + core.State.Step + " steps, height "
                + core.State.Position.Y.ToString("0.0000") + " m, " + core.Contacts.ImpactCount + " impacts, legs " + core.Trace.LegLoad.RearRight.ToString("0.0")
                + " N rear right");
            Assert.That(core.Contacts.ImpactCount, Is.GreaterThanOrEqualTo(2), "it touched down, bounced and touched down again");
            Assert.That(core.Trace.ContactsTouching, Is.GreaterThanOrEqualTo(4), "it ends at rest on its skids");
            Assert.That(core.State.Crashed, Is.False);
            Assert.That(GoldenFlights.FlyContact(spec, out _), Is.EqualTo(hash), "a second flight gives the same bits");
            Assert.That(hash, Is.EqualTo(GoldenFlights.ContactHash), "the pinned golden (a re-record is audited and raises the model version)");
        }

        // p7a-ground 1.10's audit (model 23), restated at the ground merge (model 27), kept: without surfaces.json the contact
        // golden is the physics line's model 26 flight on the rigid floor, every record and event bit for bit; only the soil
        // moves it (on p7a-ground's model 23 it was model 18's, 0x1152D64875230761). Since model 40 without props.blade.
        [Test]
        public void WithoutTheSoilTheContactGoldenIsModel26s()
        {
            Assert.That(GoldenFlights.FlyContact(GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))), out _, soil: false,
                    layout: GoldenFlights.Model28Layout, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model26ContactHash), "over model 28's 1904 bytes since model 34 (the line's record)");
        }

        // 1.6b: the test range's contact golden, from S04's soil onto its straw (GoldenFlights.FlyRange).
        [Test]
        public void TheRangeGoldenFlightIsPinned()
        {
            Assert.That(RangeScenarios.World.ContentHash, Is.EqualTo(GoldenFlights.RangeContent), "the test range as recorded (else the map moved, not the physics)");
            DroneSpec spec = RepositoryDrone.Spec();
            ulong hash = GoldenFlights.FlyRange(spec, out FlightCore core);
            ref readonly FlightState s = ref core.State;
            double stand = ContactScenarios.Standing(core.Contacts.Spec), sink = RangeScenarios.Straw.Y + stand - s.Position.Y;
            double speed = DetMath.Sqrt(Double3.Dot(s.Velocity, s.Velocity));
            TestContext.Out.WriteLine("range golden: 0x" + hash.ToString("X16") + " after " + s.Step + " steps, sunk " + (sink * 1e3).ToString("0.00")
                + " mm into the straw, " + core.Contacts.ImpactCount + " impacts, " + core.Trace.ContactsTouching + " touching pairs, " + (speed * 1e3).ToString("0.000") + " mm/s");
            Assert.That(core.Contacts.ImpactCount, Is.GreaterThanOrEqualTo(1), "it touched down on the mat");
            Assert.That(s.Crashed, Is.False);
            Assert.That(core.Trace.ContactsTouching, Is.GreaterThanOrEqualTo(8), "it ends at rest in the straw");
            Assert.That(sink, Is.EqualTo(0.009).Within(15).Percent, "sunk into the mat as at rest there (RangeContactTests: 9.0 mm by the half-skid's area)");
            // Since model 46 (p7g-legs on the line) the legs' side sway on the straw decays at the tube's own ζ (about 4 s) under
            // the idle rotors: 3.87 mm/s at 12 s (without props.blade 3.67; on p7g's legs without their side give 0.16; on model
            // 26's legs 0.70), under the landings' 1 cm/s rest.
            // Since model 64 (the canister's mass lower) that sway is 5.15 mm/s at 12 s (3.87 on the old coil), under the landings'
            // 1 cm/s rest.
            Assert.That(speed, Is.LessThan(1e-2), "at rest: under the landings' 1 cm/s (1.15 mm/s at 12 s on D-066's model 23, under 1 mm/s on model 22)");
            Assert.That(GoldenFlights.FlyRange(spec, out _), Is.EqualTo(hash), "a second flight gives the same bits");
            Assert.That(hash, Is.EqualTo(GoldenFlights.RangeHash), "the pinned golden (a re-record is audited and raises the model version)");
        }

        // p7a-ground 1.12's audit (model 33), kept: without the layers' area laws the range golden is model 32's flight, every
        // record and event, dry and in Rain with surfaces.json (1.13's flight that ended rolled 17.6° on wet straw): neither
        // reaches the stroke, and the seam's facing test changes nothing there, so the area law is the whole move. Since model 40
        // without props.blade.
        [Test]
        public void WithoutTheAreaLawsTheRangeGoldenIsModel32s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model28Layout; // since model 34 the record's end is the line's block
            Assert.That(GoldenFlights.FlyRange(spec, out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model32RangeHash), "dry");
            Assert.That(GoldenFlights.FlyRange(spec, out _, rain: RainLevel.Rain, table: SoilScenarios.Table(), materials: GoldenFlights.WithoutTheAreaLaws(), layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model32RainRangeHash), "in Rain with surfaces.json");
        }

        // The merge of p7c-wind and unity/main (model 17), its audit against unity/main, kept: on drone.json with main's tuned
        // rotor drag (0.25) in still air, where the field and the stand-in both blow nothing, each golden over main's
        // 1656-byte layout (the record less p7c-wind's 152 bytes at 1296) is main's model 15 flight in still air, bit for bit
        // (pinned from main's code with the goldens' air set still, a temporary edit), and the tumble, which has no air,
        // main's model 15 pin. Main's kept audits of models 6–14 fly the stand-in's Windy and Calm air, which is gone. D-064
        // (model 23): over the record's first 1808 bytes, without the rotor wakes and the motors' new spreads; the contact
        // flight's re-ingested wash moved to published numbers and answers in the torque, so its audit against model 19 (and
        // through it model 15's still-air pin, 0x557DEBE410FAEEA1) was a temporary edit (GoldenFlights' notes). For the same
        // reason the range golden's audit against model 16 in still air (0xDC85BA160EB0415C over the range branch's 1656 bytes,
        // a kept test until model 22) is a temporary edit since model 24 (GoldenFlights.Model63RangeHash's notes).
        [Test]
        public void InStillAirTheGoldenFlightsAreModel15s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(GoldenFlights.WithRotorDrag(Model63Drone.Text(), GoldenFlights.TunedHForce)));
            WindSetup still = WindSetup.Of(WindLevel.Still);
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore core, RotorWindBytesAt, WindBytes, wind: still, interference: false, wake: false, motors: false,
                layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model15StillPoweredHash), "powered");
            Assert.That(core.Contacts.ImpactCount, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, RotorWindBytesAt, WindBytes, at), Is.EqualTo(GoldenFlights.Model15TumbleHash), "tumble");
        }

        // p7g-legs 1.2 (model 39), its audit, kept: without the legs' side give and their failure law (WithModel37Legs), over
        // model 37's 1904 bytes (the record less the legs' state), every golden is model 37's, every record and event, so the
        // leg law is the whole change. Since model 46 (the physics integration 2) each flies without the line's mechanisms (no
        // soil, no area laws, no props.blade, model 34's yaw I; no line, no cut), as WithoutTheLineEachGoldenIsModel41s.
        [Test]
        public void WithoutTheFootsGiveEachGoldenIsModel37s()
        {
            DroneSpec Model37() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel37Legs(DroneSpec.Parse(Model63Drone.Text())));
            const int at = GoldenFlights.Model37Layout;
            Assert.That(GoldenFlights.FlyPowered(Model37(), out _, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model37PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model37(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model37RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model37(), out _, layout: at, soil: false, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model37ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model37(), out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model37TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model37(), out _, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model37RangeHash), "range");
            // With them, the flights that touch nothing are model 37's too.
            DroneSpec now = GoldenFlights.WithoutTheBlade(DroneSpec.Parse(Model63Drone.Text()));
            Assert.That(GoldenFlights.FlyPowered(now, out _, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model37PoweredHash), "powered, with the law");
            Assert.That(GoldenFlights.FlyPowered(now, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model37RainPoweredHash), "rain, with the law");
            Assert.That(GoldenFlights.FlyTumble(now, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model37TumbleHash), "tumble, with the law");
        }

        // p7g-legs 1.3 (model 41), its audit, kept: the flights that touch nothing (powered, rain, tumble) are model 39's over its
        // 2072 bytes, every record and event, with the side give's parts and a damaged leg's change in place; the contact and
        // range flights' moves are their pins' audits (GoldenFlights). Since model 46 without the line's mechanisms, model 39's
        // record the first 1904 bytes and then the legs' state (the fiber's and the strikes' blocks skipped).
        [Test]
        public void OverModel39sRecordTheFlightsThatTouchNothingAreModel39s()
        {
            DroneSpec now = GoldenFlights.WithoutTheBlade(DroneSpec.Parse(Model63Drone.Text()));
            const int at = GoldenFlights.Model28Layout, skip = GoldenFlights.Model41Skip, end = GoldenFlights.Model39Layout;
            Assert.That(GoldenFlights.FlyPowered(now, out _, at, skip, layout: end, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model39PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(now, out _, at, skip, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: end, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model39RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(now, out _, out _, at, skip, end), Is.EqualTo(GoldenFlights.Model39TumbleHash), "tumble");
        }

        // p7g-legs 1.1 (model 37), its audit, kept: on model 26's legs (WithModel26Legs: the CFRP legs' and skids' 11 and 20 g,
        // the assumed 10 kN/m spring and its 45 N·s/m damper, the leg shapes' 6600 N/m, no side give) every golden is model
        // 26's over its 1904 bytes, every record and event, so the legs' data is the whole change. Since model 46 without the
        // line's mechanisms.
        [Test]
        public void OnModel26sLegsEachGoldenIsModel26s()
        {
            DroneSpec Model26() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text())));
            const int at = GoldenFlights.Model37Layout;
            Assert.That(GoldenFlights.FlyPowered(Model26(), out _, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model26PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model26(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model26RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model26(), out _, layout: at, soil: false, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model26ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model26(), out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model26TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model26(), out _, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model26RangeHash), "range");
        }

        // The physics integration 2 with p7g-legs (model 46), its audit against the line (model 44), kept: on model 26's legs
        // (WithModel26Legs: no side give, no failure law, the CFRP masses and the 10 kN/m spring), over the line's 2392 bytes,
        // every golden is model 44's pin, every record and event: p7g-legs' code (the side give's path, the law, the damaged
        // leg's geometry, the loose pieces, the soil's leg coupling) changes no flight without the legs' build, and drone.json's
        // legs are the whole of each move against the line. No leg on model 26's legs ever changes state. Since model 48 (the tip
        // slide and the fountain) the tether flies design 4's air (TetherDesign4Air), and so this is also the audit against the
        // tip slide's model 45, which kept model 40's pins as models 42 and 44 did: its carve moves no golden.
        [Test]
        public void OnModel26sLegsEachGoldenIsModel44s()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()));
            const int at = GoldenFlights.Model44Layout;
            uint states = 0;
            void Legs(byte[] record)
            {
                for (int i = at; i < at + 8; i++) states |= record[i]; // each leg's state byte and each corner's
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: at, onRecord: Legs, airmode: true), Is.EqualTo(GoldenFlights.Model44PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Legs, airmode: true),
                Is.EqualTo(GoldenFlights.Model44RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore contact, layout: at, onRecord: Legs, airmode: true), Is.EqualTo(GoldenFlights.Model44ContactHash), "contact, on the soil");
            Assert.That(contact.Contacts.Legs, Is.Null, "no failure law on model 26's legs");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Legs, airmode: true), Is.EqualTo(GoldenFlights.Model44RangeHash), "range, with the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, fiberContacts: false, coulomb: false, layout: at, onRecord: Legs, recirculation: false, airmode: true), Is.EqualTo(GoldenFlights.Model44TetherHash),
                "tether, on design 4's air");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model44TumbleHash), "tumble");
            Assert.That(states, Is.EqualTo(0u), "every leg intact, every corner whole");
        }

        // The physics integration 2 with the tip slide and the fountain (model 48), its audit against p7d-tether 1.6b (model 47,
        // the fountain on 44), kept: on model 26's legs, over the line's 2392 bytes, the tether golden with the fountain and the
        // ground vortex is model 47's pin, every record and event, every leg intact; the other goldens fly no line and are model
        // 47's as they are model 44's (above: 1.6b kept them). So the legs' build is the whole of the tether golden's move against
        // 1.6b, and the fountain the whole against the line (WithoutTheFountainEachGoldenIsModel49s).
        [Test]
        public void OnModel26sLegsEachGoldenIsModel47s()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()));
            const int at = GoldenFlights.Model44Layout;
            uint states = 0;
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, fiberContacts: false, coulomb: false, layout: at, onRecord: r =>
            {
                for (int i = at; i < at + 8; i++) states |= r[i];
            }, airmode: true), Is.EqualTo(GoldenFlights.Model47TetherHash), "tether, with the fountain");
            Assert.That(states, Is.EqualTo(0u), "every leg intact, every corner whole");
        }

        // p7g-legs 1.4 (model 49), its audit, kept: with the lever off (WithoutTheLever: 1.3's linear softening) every golden is
        // model 46's pin over its 2784 bytes, every record and event, and the lean zero on every record; with it on, the flights
        // that touch nothing (powered, rain, tumble) are model 46's too. So the lever is the whole move. Since model 50 (the
        // lever on the line) the pins are model 48's, the line's before the merge: the tether flight with the lever off is model
        // 48's (the fountain's, Model48TetherHash), every other golden model 46's as model 48 kept them.
        [Test]
        public void WithoutTheLeverEachGoldenIsModel48s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int at = GoldenFlights.Model46Layout;
            int nonZero = 0;
            void Lean(byte[] record)
            {
                for (int i = at; i < LogFormat.StepSize; i++) if (record[i] != 0) nonZero++;
            }
            Action<FlightSetup> off = GoldenFlights.WithoutTheLever;
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Lean, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model46ContactHash), "contact, on the soil");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Lean, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model46RangeHash), "range, with the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, fiberContacts: false, coulomb: false, layout: at, onRecord: Lean, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model48TetherHash), "tether, with the fountain");
            Assert.That(nonZero, Is.EqualTo(0), "the lean off the lever");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: at, onRecord: Lean, airmode: true), Is.EqualTo(GoldenFlights.Model46PoweredHash), "powered, on the lever");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Lean, airmode: true),
                Is.EqualTo(GoldenFlights.Model46RainPoweredHash), "rain, on the lever");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model46TumbleHash), "tumble, on the lever");
            Assert.That(nonZero, Is.EqualTo(0), "the lean of a flight that touches nothing");
        }

        // The hanging half and the stub (model 51), its audit, kept: no golden parts a leg, so none has a hanging half or a stub
        // (every corner state zero on every record), and with the fix off (WithoutTheHangingGive: model 50's turn alone and the
        // free tube's spring on a stub) and on every golden is model 50's pin over the whole record, every record and event: no
        // golden was re-recorded. The flights the fix moves are those whose legs part (the crown in Windy, the damage cell's seed 3,
        // the legs' replay): RangeContactTests.WithoutTheHangingGiveTheWindyCrownIsModel50s pins one of them to model 50's Tunnel.
        // Since model 55 (the fiber's contacts on the line) the tether golden flies here without them (FlightSetup.
        // TetherWithoutContacts), model 50's line: so with the fix on this is the audit "without the fiber's contacts each golden
        // is model 51's" (51 kept every pin of 50's), and with it off too model 50's. Since model 57 (the props at rest on the
        // line, a 2944-byte record) over model 50's 2816 bytes against model 50's pins (the new block is the rest's, audited below).
        [Test]
        public void WithoutTheHangingGiveEachGoldenIsModel50s()
        {
            DroneSpec spec = Model63Drone.Spec();
            int corners = 0;
            int at = CornerStates();
            const int m50 = GoldenFlights.Model50Layout;
            void Whole(byte[] record)
            {
                for (int i = at; i < at + 4; i++) corners |= record[i];
            }
            // Since model 54 without the skid's frame (WithoutTheFrame): model 51's pins are model 50's.
            Action<FlightSetup> frameless = GoldenFlights.WithoutTheFrame;
            foreach (Action<FlightSetup> edit in new[] { frameless, frameless + GoldenFlights.WithoutTheHangingGive })
            {
                string how = edit == frameless ? "with the fix" : "without it";
                Assert.That(GoldenFlights.FlyContact(spec, out _, layout: m50, onRecord: Whole, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model50ContactHash), "contact, " + how);
                Assert.That(GoldenFlights.FlyRange(spec, out _, layout: m50, onRecord: Whole, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model50RangeHash), "range, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, fiberContacts: false, coulomb: false, layout: m50, onRecord: Whole, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model50TetherHash), "tether without the fiber's contacts, " + how);
                Assert.That(line.Tether.Contacts, Is.Null, "no fiber contacts");
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: m50, onRecord: Whole, airmode: true), Is.EqualTo(GoldenFlights.Model50PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m50, onRecord: Whole, airmode: true),
                Is.EqualTo(GoldenFlights.Model50RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m50), Is.EqualTo(GoldenFlights.Model50TumbleHash), "tumble");
            Assert.That(corners, Is.EqualTo(0), "no golden parts a leg: no hanging half, no stub");
        }

        // The physics integration 2 with the fiber's contacts (model 55), its audit against p7d-tether 1.7 (model 53), kept: no
        // golden parts a leg, so with the hanging half's give and the stub's spring off (WithoutTheHangingGive, model 53's legs)
        // and on, the tether golden with the fiber's contacts is 1.7's pin (TetherHash), every record and event, and every
        // golden without the line keeps its pin (above): the hanging give is the whole of 55's move against 53 (none in the
        // goldens), the fiber's contacts the whole against 51 (WithoutTheHangingGiveEachGoldenIsModel50s' tether). Since model
        // 57 over model 50's 2816 bytes against 1.7's pin (Model53TetherHash); since model 59 without the cuts (1.7's line).

        // p7d-tether 1.8's audit (model 56), kept: without the cuts and the link (FlightSetup.TetherWithoutCuts) the tether golden
        // is model 55's pin, every record and event; no other golden flies the line, so every other pin is model 55's as it
        // stands. Since model 59 (the cuts on the props at rest) without a resting prop (ContactSpec.BladeRest off) over model
        // 50's 2816 bytes: 1.8's own flight, its pin Model56TetherHash.
        // Since model 60 (the skid's frame on the line) without the frame (GoldenFlights.WithoutTheFrame).
        [Test]
        public void WithoutTheCutsEachGoldenIsModel55s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int m50 = GoldenFlights.Model50Layout;
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, cuts: false, layout: m50, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest, airmode: true), Is.EqualTo(GoldenFlights.Model55TetherHash),
                "tether without the cuts: model 55's");
            Assert.That(line.Tether.Cuts, Is.Null);
            // With them every record is model 55's up to the line's break (the disc at the landing, a known risk: 1.8's note), and
            // the events before it differ only by FiberSection's first reads at the drone's parts.
            var rec55 = new System.Collections.Generic.List<ulong>();
            var rec56 = new System.Collections.Generic.List<ulong>();
            var ev55 = new System.Collections.Generic.List<string>();
            var ev56 = new System.Collections.Generic.List<string>();
            void Events(FlightCore c, System.Collections.Generic.List<string> into)
            {
                for (int i = 0; i < c.Events.Count; i++)
                    if (c.Events[i].Kind != EventKind.FiberSection) into.Add($"{c.State.Step} {c.Events[i].Kind} {c.Events[i].Index} {c.Events[i].A:R}");
            }
            GoldenFlights.FlyContact(spec, out _, tether: true, coulomb: false, cuts: false, layout: m50, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest, onRecord: r => rec55.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, m50)), after: c => Events(c, ev55), airmode: true);
            ulong hash = GoldenFlights.FlyContact(spec, out FlightCore cut, tether: true, coulomb: false, layout: m50, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest, body: false, onRecord: r => rec56.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, m50)), after: c => Events(c, ev56), airmode: true);
            long at = cut.Tether.Broken ? cut.Tether.BrokenAt : rec56.Count;
            int same = 0;
            while (same < rec55.Count && same < rec56.Count && rec55[same] == rec56[same]) same++;
            int evSame = 0;
            while (evSame < ev55.Count && evSame < ev56.Count && ev55[evSame] == ev56[evSame]) evSame++;
            FiberBreak b = cut.Tether.LastBreak;
            TestContext.Progress.WriteLine($"model 56: the records model 55's for {same} steps, the line {(cut.Tether.Broken ? $"broken at step {at} by {b.Mechanism} at {(FiberMember)b.Kind} {b.Member}, crossing at {b.Turn:F4} m (body {cut.State.Attitude.InverseRotate(b.Point - cut.State.Position).X:F3}, {cut.State.Attitude.InverseRotate(b.Point - cut.State.Position).Y:F3}, {cut.State.Attitude.InverseRotate(b.Point - cut.State.Position).Z:F3} m), T {b.Tension:F3} N, the near piece {(cut.Tether.Attached ? cut.Tether.Chain.Count + " nodes" : "none")}" : "whole")}; "
                + $"the events other than FiberSection model 55's for {evSame} of {ev55.Count} ({ev56.Count} with the cuts); {cut.Tether.Cuts.Sections} sections read; 0x{hash:X16}");
            Assert.That(same, Is.GreaterThanOrEqualTo(at - 1), "every record as model 55's up to the break");
            for (int i = 0; i < evSame; i++) Assert.That(ev56[i], Is.EqualTo(ev55[i]));
            Assert.That(evSame == ev55.Count || ev56.Count > evSame && long.Parse(ev56[evSame].Split(' ')[0]) >= at, Is.True, "the events model 55's up to the break, but FiberSection");
            Assert.That(hash, Is.EqualTo(GoldenFlights.Model56TetherHash), "tether with them: model 56's");
        }

        // Since model 60 (the skid's frame on the line) without the frame (GoldenFlights.WithoutTheFrame).
        [Test]
        public void WithoutTheHangingGiveEachGoldenIsModel53s()
        {
            DroneSpec spec = Model63Drone.Spec();
            int corners = 0;
            int at = CornerStates();
            foreach (Action<FlightSetup> edit in new[] { GoldenFlights.WithoutTheFrame, GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheHangingGive })
            {
                string how = edit == GoldenFlights.WithoutTheFrame ? "with the hanging give" : "without it";
                Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, cuts: false, edit: edit, layout: GoldenFlights.Model50Layout, onRecord: r =>
                {
                    for (int i = at; i < at + 4; i++) corners |= r[i];
                }, airmode: true), Is.EqualTo(GoldenFlights.Model53TetherHash), "tether with the fiber's contacts, " + how);
                Assert.That(line.Tether.Contacts, Is.Not.Null, "the fiber's contacts");
                Assert.That(line.Tether.Contacts.Inserted, Is.GreaterThan(0), "the line meets the drone's parts");
            }
            Assert.That(corners, Is.EqualTo(0), "no leg parts on the line's golden: no hanging half, no stub");
        }

        // p7f-damage 1.3b's audit (model 52), kept: no golden's prop rests on anything (a rotor's blades are shapes only while it
        // turns slower than ContactSpec.BladeRestSpeed: on the floor before the arming and after the disarm, their discs 0.29 m
        // over the skids), so over model 50's 2816 bytes each golden is model 50's pin, every record and event (a touching blade
        // shape would add its ContactMade), with the props at rest on and off, and the resting blades' block is zero on every
        // record: the rest changes no flight whose props meet nothing at rest, and the record's new block is the whole move.
        // Since model 57 (on the line with the fiber's contacts) the tether golden flies here without them (model 50's line).
        // Since model 60 (the skid's frame on the line) without the frame (GoldenFlights.WithoutTheFrame).
        [Test]
        public void WithoutARestingPropEachGoldenIsModel50s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int at = GoldenFlights.Model50Layout;
            int nonZero = 0;
            void Rest(byte[] record)
            {
                for (int i = at; i < LogFormat.StepSize; i++) if (record[i] != 0) nonZero++;
            }
            foreach (bool rest in new[] { true, false })
            {
                Action<FlightSetup> edit = rest ? GoldenFlights.WithoutTheFrame : GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest;
                string on = rest ? " (at rest)" : " (passing through)";
                Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: at, onRecord: Rest, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model50PoweredHash), "powered" + on);
                Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Rest, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model50RainPoweredHash), "rain" + on);
                Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Rest, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model50ContactHash), "contact" + on);
                Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Rest, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model50RangeHash), "range" + on);
                Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, fiberContacts: false, coulomb: false, layout: at, onRecord: Rest, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model50TetherHash),
                    "tether" + on);
            }
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model50TumbleHash), "tumble (no rotors)");
            Assert.That(nonZero, Is.EqualTo(0), "no resting blade's load on any record");
        }

        // The physics integration 2 with the props at rest (model 57), its audit against model 55, kept: no golden's prop rests on
        // anything (WithoutARestingPropEachGoldenIsModel50s), so over model 55's 2816 bytes the tether golden with the fiber's
        // contacts is 55's pin (1.7's, Model53TetherHash), every record and event, with the props at rest on and off, the new
        // block zero; every golden without the line is 55's pin there too (55 kept model 50's: the test above). The rest is the
        // whole of 57's move against 55: the record's block. Since model 59 without the cuts (model 55's line).
        // Since model 60 (the skid's frame on the line) without the frame (GoldenFlights.WithoutTheFrame).
        [Test]
        public void WithoutARestingPropEachGoldenIsModel55s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int at = GoldenFlights.Model50Layout;
            int nonZero = 0;
            foreach (bool rest in new[] { true, false })
            {
                Action<FlightSetup> edit = rest ? GoldenFlights.WithoutTheFrame : GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest;
                Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, cuts: false, layout: at, edit: edit, onRecord: r =>
                {
                    for (int i = at; i < LogFormat.StepSize; i++) if (r[i] != 0) nonZero++;
                }, airmode: true), Is.EqualTo(GoldenFlights.Model53TetherHash), "tether with the fiber's contacts" + (rest ? " (at rest)" : " (passing through)"));
                Assert.That(line.Tether.Contacts.Inserted, Is.GreaterThan(0), "the line meets the drone's parts");
            }
            Assert.That(nonZero, Is.EqualTo(0), "no resting blade's load on any record");
        }

        // Its audit against p7f-damage 1.3b (model 52), kept: without the fiber's contacts (FlightSetup.TetherWithoutContacts)
        // and with the hanging half's give and the stub's spring on and off (no golden parts a leg) every golden is model 52's
        // pin over the whole 2944-byte record, every record and event: the fiber's contacts and the hanging give are the whole
        // of 57's move against 52 (the tether golden's, TetherHash's note).
        // Since model 60 (the skid's frame on the line) without the frame (GoldenFlights.WithoutTheFrame).
        [Test]
        public void WithoutTheFiberContactsAndTheHangingGiveEachGoldenIsModel52s()
        {
            DroneSpec spec = Model63Drone.Spec();
            foreach (Action<FlightSetup> edit in new[] { GoldenFlights.WithoutTheFrame, GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheHangingGive })
            {
                string how = edit == GoldenFlights.WithoutTheFrame ? "with the hanging give" : "without it";
                Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: GoldenFlights.Model63Layout, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model63PoweredHash), "powered, " + how);
                Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: GoldenFlights.Model63Layout, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model63RainPoweredHash), "rain, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out _, edit: edit, layout: GoldenFlights.Model57Layout, airmode: true), Is.EqualTo(GoldenFlights.Model59ContactHash), "contact, " + how);
                Assert.That(GoldenFlights.FlyRange(spec, out _, edit: edit, layout: GoldenFlights.Model57Layout, airmode: true), Is.EqualTo(GoldenFlights.Model59RangeHash), "range, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, fiberContacts: false, edit: edit, layout: GoldenFlights.Model57Layout, airmode: true),
                    Is.EqualTo(GoldenFlights.Model52TetherHash), "tether without the fiber's contacts, " + how);
                Assert.That(line.Tether.Contacts, Is.Null, "no fiber contacts");
            }
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: GoldenFlights.Model63Layout), Is.EqualTo(GoldenFlights.Model63TumbleHash), "tumble");
        }

        // The physics integration 2 with the cuts (model 59), its audit against the props at rest (model 57), kept: without the
        // cuts and the link (FlightSetup.TetherWithoutCuts) the tether golden is model 57's pin over the whole 2944-byte record,
        // every record and event; with them every record is 57's up to the line's break and the events but FiberSection's reads
        // too. Every golden without the line is 57's pin as it stands (no cut, no link, no failsafe without the line).
        // Since model 60 (the skid's frame on the line) without the frame (GoldenFlights.WithoutTheFrame); since model 63 (the
        // bells on the line) over model 57's 2944 bytes (GoldenFlights.Model57Layout).
        [Test]
        public void WithoutTheCutsEachGoldenIsModel57s()
        {
            DroneSpec spec = Model63Drone.Spec();
            var rec57 = new System.Collections.Generic.List<ulong>();
            var rec59 = new System.Collections.Generic.List<ulong>();
            var ev57 = new System.Collections.Generic.List<string>();
            var ev59 = new System.Collections.Generic.List<string>();
            void Events(FlightCore c, System.Collections.Generic.List<string> into)
            {
                for (int i = 0; i < c.Events.Count; i++)
                    if (c.Events[i].Kind != EventKind.FiberSection) into.Add($"{c.State.Step} {c.Events[i].Kind} {c.Events[i].Index} {c.Events[i].A:R}");
            }
            ulong uncut = GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, cuts: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout,
                onRecord: r => rec57.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), after: c => Events(c, ev57), airmode: true);
            Assert.That(uncut, Is.EqualTo(GoldenFlights.Model57TetherHash), "tether without the cuts: model 57's");
            Assert.That(line.Tether.Cuts, Is.Null);
            ulong hash = GoldenFlights.FlyContact(spec, out FlightCore cut, tether: true, coulomb: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout, body: false,
                onRecord: r => rec59.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), after: c => Events(c, ev59), airmode: true);
            long at = cut.Tether.Broken ? cut.Tether.BrokenAt : rec59.Count;
            int same = 0;
            while (same < rec57.Count && same < rec59.Count && rec57[same] == rec59[same]) same++;
            int evSame = 0;
            while (evSame < ev57.Count && evSame < ev59.Count && ev57[evSame] == ev59[evSame]) evSame++;
            FiberBreak b = cut.Tether.LastBreak;
            TestContext.Progress.WriteLine($"model 59: the records model 57's for {same} steps, the line {(cut.Tether.Broken ? $"broken at step {at} by {b.Mechanism} at {(FiberMember)b.Kind} {b.Member}, crossing at {b.Turn:F4} m, the blade at {b.Radius:F2} m/s, T {b.Tension:F3} N" : "whole")}; "
                + $"the events other than FiberSection model 57's for {evSame} of {ev57.Count} ({ev59.Count} with the cuts); 0x{hash:X16}");
            Assert.That(same, Is.GreaterThanOrEqualTo(at - 1), "every record as model 57's up to the break");
            Assert.That(evSame == ev57.Count || ev59.Count > evSame && long.Parse(ev59[evSame].Split(' ')[0]) >= at, Is.True, "the events model 57's up to the break, but FiberSection");
            Assert.That(hash, Is.EqualTo(GoldenFlights.Model59TetherHash), "tether with them: model 59's (the fountain through the body)");
        }

        // Model 64's audit against model 63 (D01's canister), kept (since model 65 with the 0-force hold, FlightSetup.TetherZeroForceHold: 63's and 64's chain; since model 68 over model 63's 3040 bytes, the shafts on): on model 63's drone.json (the old coil beneath the frame, a
        // fixture: Model63Drone; FlyContact lets its line out at the old rear cap, GoldenFlights.Model63Tether) every golden is
        // model 63's pin, every record and event, with the fountain through the body (61's blocking off), so the canister and its
        // exit are the whole move of the powered, rain, contact, range and tumble goldens; with 61's blocking on, the tether
        // golden on the old coil is the merge's (Model63BodyTetherHash), so the canister and its exit are the whole move of 64's
        // tether golden against it. The new coil's records part from the old's at the first step (the mass build).
        [Test]
        public void OnTheOldCoilEachGoldenIsModel63s()
        {
            DroneSpec spec = Model63Drone.Spec();
            Assert.That(spec.Coil.Housing, Is.EqualTo(-1), "model 63's coil has no housing");
            const int m63 = GoldenFlights.Model63Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: m63, airmode: true), Is.EqualTo(GoldenFlights.Model63PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m63, airmode: true),
                Is.EqualTo(GoldenFlights.Model63RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: m63, airmode: true), Is.EqualTo(GoldenFlights.Model63ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: m63, airmode: true), Is.EqualTo(GoldenFlights.Model63RangeHash), "range");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m63), Is.EqualTo(GoldenFlights.Model63TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore old, tether: true, coulomb: false, body: false, layout: m63, airmode: true), Is.EqualTo(GoldenFlights.Model63TetherHash),
                "tether, the fountain through the body");
            ulong blocked = GoldenFlights.FlyContact(spec, out FlightCore merged, tether: true, coulomb: false, layout: m63, airmode: true);
            ulong canister = GoldenFlights.FlyContact(Model70Drone.Spec(), out FlightCore now, tether: true, coulomb: false, layout: m63, airmode: true);
            string Outcome(FlightCore c) => c.Tether.Broken
                ? $"broken at step {c.Tether.BrokenAt} by {c.Tether.LastBreak.Mechanism} at {(FiberMember)c.Tether.LastBreak.Kind} {c.Tether.LastBreak.Member}"
                : $"whole, {c.Tether.PaidOut:F3} m paid out";
            TestContext.Progress.WriteLine($"model 64 against 63: on the old coil through the body the line {Outcome(old)}; with 61's blocking {Outcome(merged)} "
                + $"(0x{blocked:X16}); on the canister {Outcome(now)} (0x{canister:X16})");
            Assert.That(blocked, Is.EqualTo(GoldenFlights.Model63BodyTetherHash), "tether on the old coil with 61's blocking: the merge's");
            Assert.That(canister, Is.EqualTo(GoldenFlights.Model64TetherHash), "tether on the canister: model 64's");
        }

        // Model 61's audit against model 59, kept (since model 64 without the skid's frame, over model 57's 2944 bytes; since model 65 with the 0-force hold): with the fountain through the drone's body (FlightSetup.
        // TetherFountainThroughBody) the tether golden is model 59's pin, every record and event; with the body's blocking the
        // records are 59's until the line first lies where the body stands over or under the fountain, and the outcome prints.
        [Test]
        public void WithoutTheBodysBlockingEachGoldenIsModel59s()
        {
            DroneSpec spec = Model63Drone.Spec();
            var rec59 = new System.Collections.Generic.List<ulong>();
            var rec61 = new System.Collections.Generic.List<ulong>();
            ulong through = GoldenFlights.FlyContact(spec, out FlightCore old, tether: true, coulomb: false, body: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout,
                onRecord: r => rec59.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), airmode: true);
            Assert.That(through, Is.EqualTo(GoldenFlights.Model59TetherHash), "the fountain through the body: model 59's");
            Assert.That(old.Tether.Rotors.BodyBlocks, Is.False);
            ulong hash = GoldenFlights.FlyContact(spec, out FlightCore now, tether: true, coulomb: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout,
                onRecord: r => rec61.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), airmode: true);
            Assert.That(now.Tether.Rotors.BodyBlocks, Is.True);
            int same = 0;
            while (same < rec59.Count && same < rec61.Count && rec59[same] == rec61[same]) same++;
            string Outcome(FlightCore c) => c.Tether.Broken
                ? $"broken at step {c.Tether.BrokenAt} by {c.Tether.LastBreak.Mechanism} at {(FiberMember)c.Tether.LastBreak.Kind} {c.Tether.LastBreak.Member} (the blade at {c.Tether.LastBreak.Radius:F2} m/s, T {c.Tether.LastBreak.Tension:F3} N)"
                : "whole";
            TestContext.Progress.WriteLine($"model 61: the records model 59's for {same} of {rec61.Count} steps; through the body the line {Outcome(old)}, with it {Outcome(now)}; 0x{hash:X16}");
            Assert.That(hash, Is.EqualTo(GoldenFlights.Model61TetherHash), "tether with the body's blocking: model 61's");
        }

        // Its audit against p7d-tether 1.8 (model 56), kept: without a resting prop (ContactSpec.BladeRest off) the tether golden
        // with the cuts is model 56's pin over model 50's 2816 bytes, every record and event, the resting blades' block zero; and
        // with the rest on too: no blade rests on anything and the line never meets a resting blade, before the break or after
        // it (the near piece hangs from the exit through the disarm and the spin-down), so the rest's whole move is the block.
        // Since model 60 (the skid's frame on the line) without the frame (GoldenFlights.WithoutTheFrame).
        [Test]
        public void WithoutARestingPropEachGoldenIsModel56s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int m50 = GoldenFlights.Model50Layout;
            var off = new System.Collections.Generic.List<ulong>();
            var on = new System.Collections.Generic.List<ulong>();
            int nonZero = 0, onZero = 0;
            ulong passing = GoldenFlights.FlyContact(spec, out FlightCore through, tether: true, coulomb: false, layout: m50, edit: GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheRest, body: false, onRecord: r =>
            {
                off.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, m50));
                for (int i = m50; i < LogFormat.StepSize; i++) if (r[i] != 0) nonZero++;
            }, airmode: true);
            Assert.That(passing, Is.EqualTo(GoldenFlights.Model56TetherHash), "tether without a resting prop: model 56's");
            Assert.That(nonZero, Is.EqualTo(0), "no resting blade's load without the rest");
            ulong resting = GoldenFlights.FlyContact(spec, out FlightCore rest, tether: true, coulomb: false, layout: m50, edit: GoldenFlights.WithoutTheFrame, body: false, onRecord: r =>
            {
                on.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, m50));
                for (int i = m50; i < LogFormat.StepSize; i++) if (r[i] != 0) onZero++;
            }, airmode: true);
            int same = 0;
            while (same < off.Count && same < on.Count && off[same] == on[same]) same++;
            TestContext.Progress.WriteLine($"model 59 over 2816 bytes: without the rest 0x{passing:X16}, with it 0x{resting:X16}, the records 56's for {same} of {on.Count} steps, "
                + $"{onZero} non-zero bytes in the resting blades' block; the break at step {(through.Tether.Broken ? through.Tether.BrokenAt : -1)} and {(rest.Tether.Broken ? rest.Tether.BrokenAt : -1)}");
            Assert.That(resting, Is.EqualTo(GoldenFlights.Model56TetherHash), "tether with the rest: model 56's over its bytes");
            Assert.That(same, Is.EqualTo(on.Count), "every record");
            Assert.That(onZero, Is.EqualTo(0), "no resting blade's load on any record");
        }

        // p7f-damage 1.5's audit (model 58, the motors' bells, the 3040-byte record): with the bells on and off (ContactSpec.MotorBell),
        // every golden over model 57's 2944 bytes is model 57's pin, every record and event, and the bells' block is zero on every
        // record: no golden's motor band touches anything, so the bells are the whole of 58's move against 57. Since model 63 (the
        // bells on the line) without the skid's frame (GoldenFlights.WithoutTheFrame) and the cuts (FlightSetup.TetherWithoutCuts).
        [Test]
        public void WithoutTheBellsEachGoldenIsModel57s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int m57 = GoldenFlights.Model57Layout;
            int block = 0;
            void Zero(byte[] record)
            {
                for (int i = m57; i < LogFormat.StepSize; i++) if (record[i] != 0) block++;
            }
            foreach (Action<FlightSetup> edit in new Action<FlightSetup>[] { GoldenFlights.WithoutTheFrame, GoldenFlights.WithoutTheFrame + GoldenFlights.WithoutTheBells })
            {
                string how = edit == GoldenFlights.WithoutTheFrame ? "with the bells" : "without them";
                Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: m57, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model57PoweredHash), "powered, " + how);
                Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m57, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model57RainPoweredHash), "rain, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out _, layout: m57, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model57ContactHash), "contact, " + how);
                Assert.That(GoldenFlights.FlyRange(spec, out _, layout: m57, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model57RangeHash), "range, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, coulomb: false, cuts: false, layout: m57, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model57TetherHash),
                    "tether, " + how);
            }
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m57), Is.EqualTo(GoldenFlights.Model57TumbleHash), "tumble");
            Assert.That(block, Is.EqualTo(0), "the bells' block is zero");
        }

        // The physics integration 2 with the bells (model 63), its audit against the skid's frame on the cuts (model 60), kept:
        // with the bells on and off (ContactSpec.MotorBell) every golden over model 57's 2944 bytes is model 60's pin, every record
        // and event (the tether's with the fiber's contacts, the cuts, the link and the failsafe), and the bells' block is zero on
        // every record: no golden's motor band touches anything while its rotor turns or rests, so the bells are the whole of 63's
        // move against 60.
        [Test]
        public void WithoutTheBellsEachGoldenIsModel60s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int m57 = GoldenFlights.Model57Layout;
            int block = 0;
            void Zero(byte[] record)
            {
                for (int i = m57; i < LogFormat.StepSize; i++) if (record[i] != 0) block++;
            }
            foreach (Action<FlightSetup> edit in new Action<FlightSetup>[] { null, GoldenFlights.WithoutTheBells })
            {
                string how = edit == null ? "with the bells" : "without them";
                Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: m57, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model60PoweredHash), "powered, " + how);
                Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m57, onRecord: Zero, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model60RainPoweredHash), "rain, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out _, layout: m57, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model60ContactHash), "contact, " + how);
                Assert.That(GoldenFlights.FlyRange(spec, out _, layout: m57, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model60RangeHash), "range, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, body: false, layout: m57, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model60TetherHash),
                    "tether, " + how);
                Assert.That(line.Tether.Cuts, Is.Not.Null, "with the cuts");
            }
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m57), Is.EqualTo(GoldenFlights.Model60TumbleHash), "tumble");
            Assert.That(block, Is.EqualTo(0), "the bells' block is zero");
        }

        // p7f-damage 1.6's audit (model 66, the shafts and the bearings, off 63), kept: with the shafts on and off (ContactSpec.MotorShaft)
        // every golden over model 63's 3040 bytes was model 63's pin, every record and event, and the shafts' block zero on every record.
        // Since model 68 (66 merged onto 65, the canister and the fiber's chain fixes) the same against model 65: every golden over
        // the first 3040 bytes is model 65's pin, every record and event (the tether's with the fiber's contacts, the cuts, the link
        // and the failsafe), with the shafts on and off, and the shafts' block is zero on every record: no golden's shaft passes its
        // yield and no bearing its static rating on the canister either, so the record's 128 zero bytes are the whole of 68's move
        // against 65.
        [Test]
        public void WithoutTheShaftsEachGoldenIsModel65s()
        {
            DroneSpec spec = Model70Drone.Spec(); // since model 74 its drone.json is a fixture (the nozzle straight on the 15° canister)
            const int m63 = GoldenFlights.Model63Layout;
            int block = 0;
            void Zero(byte[] record)
            {
                for (int i = m63; i < LogFormat.StepSize; i++) if (record[i] != 0) block++;
            }
            foreach (Action<FlightSetup> edit in new Action<FlightSetup>[] { null, GoldenFlights.WithoutTheShafts })
            {
                string how = edit == null ? "with the shafts" : "without them";
                Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: m63, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model65PoweredHash), "powered, " + how);
                Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m63, onRecord: Zero, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model65RainPoweredHash), "rain, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out _, layout: m63, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model65ContactHash), "contact, " + how);
                Assert.That(GoldenFlights.FlyRange(spec, out _, layout: m63, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model65RangeHash), "range, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, layout: m63, onRecord: Zero, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model65TetherHash),
                    "tether, " + how);
                Assert.That(line.Tether.Cuts, Is.Not.Null, "with the cuts");
                Assert.That(line.Tether.Broken, Is.False, "the line whole on the canister");
            }
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m63), Is.EqualTo(GoldenFlights.Model65TumbleHash), "tumble");
            Assert.That(block, Is.EqualTo(0), "the shafts' block is zero");
        }

        // The physics integration 2 with the shafts (model 68), its audit against model 66, kept: on model 63's drone.json (the old
        // coil, Model63Drone, its line out of the old rear cap), the fountain through the body (61's blocking off), the known-wrong
        // 0-force hold (63's chain, so 62's and 64's chain fixes off) and no blade root (64's fix off), every golden is model 66's
        // pin over the whole 3168-byte record, every record and event, the shafts on as 66 flew them. So 65's canister and chain
        // fixes are the whole of 68's move against 66, as the audit above makes the shafts the whole of its move against 65.
        [Test]
        public void OnModel63sDroneEachGoldenIsModel66s()
        {
            DroneSpec spec = Model63Drone.Spec();
            Assert.That(spec.Coil.Housing, Is.EqualTo(-1), "model 63's coil has no housing");
            Action<FlightSetup> edit = GoldenFlights.WithoutTheBladeRoot;
            const int m68 = GoldenFlights.Model68Layout; // since model 70 over model 68's 3168 bytes, the motors' heat off
            Assert.That(GoldenFlights.FlyPowered(spec, out _, edit: edit, layout: m68, airmode: true), Is.EqualTo(GoldenFlights.Model66PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: edit, layout: m68, airmode: true),
                Is.EqualTo(GoldenFlights.Model66RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, edit: edit, layout: m68, airmode: true), Is.EqualTo(GoldenFlights.Model66ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, edit: edit, layout: m68, airmode: true), Is.EqualTo(GoldenFlights.Model66RangeHash), "range");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, body: false, edit: edit, layout: m68, airmode: true),
                Is.EqualTo(GoldenFlights.Model66TetherHash), "tether, the fountain through the body, the 0-force hold");
            Assert.That(line.Tether.Chain.ZeroForceHold, Is.True);
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m68), Is.EqualTo(GoldenFlights.Model66TumbleHash), "tumble");
        }

        // p7d-tether 1.8's audit (model 74, the fiber cuts where the pilot says, off 70), kept: on model 70's drone.json (Model70Drone,
        // its nozzle straight on the canister's 15°) with model 70's exit, 4 mm lip and peel referred to 1 m/s (GoldenFlights.
        // Model70Tether, the hole's edge wearing nothing) every golden is model 70's pin, every record and event; on the repository's
        // drone.json (the nozzle bent to 27.5°) every golden without the line is model 70's pin too, and the tether golden with model
        // 70's lip, peel and no edge wear is model 74's. So the bent nozzle's exit is the whole of the tether golden's move, and the
        // hole's edge, its wear and the peel's reference leave every golden. Since model 77 (73's controller on the line) flown with
        // Betaflight's airmode on, as models 70 and 74 flew (and so with the linear step, GoldenFlights.FlyContact). Since model 78
        // (75's arms on the line) over model 70's 3360 bytes, the clean sensors.
        [Test]
        public void WithTheOldExitAndPeelEachGoldenIsModel70s()
        {
            DroneSpec spec = Model70Drone.Spec();
            const int m73 = GoldenFlights.Model73Layout; // since model 78 over model 70's 3360 bytes, the clean sensors
            Assert.That(GoldenFlights.FlyPowered(spec, out _, airmode: true, layout: m73), Is.EqualTo(GoldenFlights.Model70PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, airmode: true, layout: m73),
                Is.EqualTo(GoldenFlights.Model70RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, airmode: true, layout: m73), Is.EqualTo(GoldenFlights.Model70ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, airmode: true, layout: m73), Is.EqualTo(GoldenFlights.Model70RangeHash), "range");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, airmode: true, layout: m73), Is.EqualTo(GoldenFlights.Model70TetherHash), "tether");
            Assert.That(line.Tether.Cuts, Is.Not.Null, "with the cuts");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m73), Is.EqualTo(GoldenFlights.Model70TumbleHash), "tumble");
            DroneSpec now = RepositoryDrone.Spec();
            Assert.That(GoldenFlights.FlyContact(now, out _, airmode: true, layout: m73), Is.EqualTo(GoldenFlights.Model70ContactHash), "the bent nozzle meets nothing in the contact golden");
            Assert.That(GoldenFlights.Model74TetherHash, Is.Not.EqualTo(GoldenFlights.Model70TetherHash), "the exit moves the tether golden");
            Assert.That(GoldenFlights.FlyContact(now, out _, tether: true, airmode: true, layout: m73, edit: x =>
            {
                x.Tether = FiberCutScenarios.With(x.Tether, "spool.lip_radius_m", 0.004);
                x.Tether = FiberCutScenarios.With(x.Tether, "spool.peel_reference_rate_m_s", 1);
                x.Tether = FiberCutScenarios.With(x.Tether, "spool.lip_archard_wear_coefficient", 0);
                x.Tether = FiberCutScenarios.With(x.Tether, "spool.mouth_radius_m", 0);
            }), Is.EqualTo(GoldenFlights.Model74TetherHash), "the tether golden on the 27.5° exit with 70's lip and peel");
        }

        // Model 77 (1.8's finding 2: the segments' Newton solves), its audit, kept: with the known-wrong linear
        // step (FlightSetup.TetherLinearStep, the line's step on its start's linearisation alone) the tether golden is the merge's
        // (74 onto 73, Model77MergeTetherHash), every record and event, and with airmode on too model 74's pin; every golden without
        // the line keeps its pin (the chain is the line's alone). So the chain's Newton solves are the whole of 77's move against
        // its merge. Since model 78 (75's arms on the line) over model 77's 3360 bytes, the clean sensors, against 77's own pin.
        [Test]
        public void WithTheLinearStepTheTetherGoldenIsTheMerges()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            const int m73 = GoldenFlights.Model73Layout; // since model 78 over model 77's 3360 bytes, the clean sensors
            Audit("tether", "the merge", (one, keep) => (GoldenFlights.FlyContact(spec, out FlightCore c, tether: true, onRecord: keep, linearStep: one, layout: m73), c),
                GoldenFlights.Model77MergeTetherHash, GoldenFlights.Model77TetherHash, "the linear step");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore old, tether: true, airmode: true, linearStep: true, layout: m73), Is.EqualTo(GoldenFlights.Model74TetherHash),
                "with airmode too: model 74's");
            Assert.That(old.Tether.Chain.NewtonSolves, Is.Zero);
            GoldenFlights.FlyContact(spec, out FlightCore now, tether: true);
            TestContext.Progress.WriteLine($"the tether golden: {now.Tether.Chain.NewtonSolves} Newton solves, {now.Tether.Chain.NewtonUnconverged} steps past their budget, "
                + $"{now.Tether.Chain.Corrections} corrections, {now.Tether.Chain.SafeSteps} safe steps, {now.Tether.Chain.Clips} clips; model 74's (the linear step, airmode on) "
                + $"{old.Tether.Chain.Corrections}, {old.Tether.Chain.SafeSteps}, {old.Tether.Chain.Clips}");
        }

        // p7f-damage 1.6b's audit (model 70, the motors' heat and dirt, off 68), kept: with the heat off (ContactSpec.MotorHeat) every
        // golden over model 68's 3168 bytes is model 68's pin, every record and event (the tether's with the fiber's contacts, the
        // cuts, the link and the failsafe), and the heat's block is zero on every record. So the heat (which every flight with the
        // bells has from its first step: the windings take their current's I²R at once) and its 192 bytes are the whole of 70's move
        // against 68.
        // Since model 75 (72 merged onto 73) it is p7f-damage 1.6's losses' audit (model 72, off 68) too, folded in (72's
        // WithTheCleanSensorsEachGoldenIsModel68s flew the same flights): over model 68's bytes every flight flies the clean sensors
        // (GoldenFlights.CleanSensors: the arms stiff in their plane, no gyro range), and the heat's and the arms' blocks are zero on
        // every record. So the arms in their plane and the gyro's range are the whole of 72's move against 68.
        [Test]
        public void WithoutTheHeatEachGoldenIsModel68s()
        {
            DroneSpec spec = Model70Drone.Spec(); // since model 74 its drone.json is a fixture (the nozzle straight on the 15° canister)
            const int m68 = GoldenFlights.Model68Layout;
            int block = 0;
            void Zero(byte[] record)
            {
                for (int i = m68; i < LogFormat.StepSize; i++) if (record[i] != 0) block++;
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: m68, onRecord: Zero, airmode: true), Is.EqualTo(GoldenFlights.Model68PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: m68, onRecord: Zero, airmode: true),
                Is.EqualTo(GoldenFlights.Model68RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: m68, onRecord: Zero, airmode: true), Is.EqualTo(GoldenFlights.Model68ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: m68, onRecord: Zero, airmode: true), Is.EqualTo(GoldenFlights.Model68RangeHash), "range");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, layout: m68, onRecord: Zero, airmode: true), Is.EqualTo(GoldenFlights.Model68TetherHash), "tether");
            Assert.That(line.Tether.Cuts, Is.Not.Null, "with the cuts");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: m68), Is.EqualTo(GoldenFlights.Model68TumbleHash), "tumble");
            Assert.That(block, Is.EqualTo(0), "the heat's and the arms' blocks are zero");
            // And the heat on: every golden moves (each heats from its first powered step), its block not zero.
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: m68, heat: true, airmode: true), Is.Not.EqualTo(GoldenFlights.Model68PoweredHash), "powered, heated");
        }

        // Its audit against the bells (p7f-damage 1.5, model 58, off 57), kept: without the skid's frame (GoldenFlights.
        // WithoutTheFrame) and the cuts (FlightSetup.TetherWithoutCuts, so no link and no failsafe) every golden is model 58's
        // pin over the whole 3040-byte record, every record and event, the bells on as 58 flew them (the fiber's contacts, the
        // hanging give and the props at rest are 57's, so 58's too). So 59's and 60's mechanisms are the whole of 63's move
        // against 58. Since model 66 (the shafts' block after it) over model 63's 3040 bytes (GoldenFlights.Model63Layout).
        [Test]
        public void WithoutTheFrameAndTheCutsEachGoldenIsModel58s()
        {
            DroneSpec spec = Model63Drone.Spec();
            Action<FlightSetup> edit = GoldenFlights.WithoutTheFrame;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, edit: edit, layout: GoldenFlights.Model63Layout, airmode: true), Is.EqualTo(GoldenFlights.Model58PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: edit, layout: GoldenFlights.Model63Layout, airmode: true),
                Is.EqualTo(GoldenFlights.Model58RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, edit: edit, layout: GoldenFlights.Model63Layout, airmode: true), Is.EqualTo(GoldenFlights.Model58ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, edit: edit, layout: GoldenFlights.Model63Layout, airmode: true), Is.EqualTo(GoldenFlights.Model58RangeHash), "range");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, cuts: false, edit: edit, layout: GoldenFlights.Model63Layout, airmode: true), Is.EqualTo(GoldenFlights.Model58TetherHash), "tether");
            Assert.That(line.Tether.Cuts, Is.Null, "no cuts");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: GoldenFlights.Model63Layout), Is.EqualTo(GoldenFlights.Model58TumbleHash), "tumble");
        }

        // The line held between the drone's parts (model 62), its audit against model 59, kept: with the known-wrong 0-force hold
        // (FlightSetup.TetherZeroForceHold) the tether golden is model 59's pin, every record and event; with the in-solve Coulomb
        // bound the records part where a part's slot first breaks it, and the step, the line's state then and the break are
        // printed. Every golden without the line is 59's pin as it stands (no part slot without the line). Since model 65 (62
        // merged onto 64) on model 63's drone.json (the old coil, Model63Drone), the fountain through the body, without the skid's
        // frame, over model 57's 2944 bytes, where the line's flight is model 59's; with the rules there it is model 62's pin,
        // every record and event: 62's chain is the same code on 64's line with 64's switches off.
        [Test]
        public void WithoutTheCoulombHoldEachGoldenIsModel59s()
        {
            DroneSpec spec = Model63Drone.Spec();
            var rec59 = new System.Collections.Generic.List<ulong>();
            var rec62 = new System.Collections.Generic.List<ulong>();
            var ev59 = new System.Collections.Generic.List<string>();
            var ev62 = new System.Collections.Generic.List<string>();
            void Events(FlightCore c, System.Collections.Generic.List<string> into)
            {
                for (int i = 0; i < c.Events.Count; i++) into.Add($"{c.State.Step} {c.Events[i].Kind} {c.Events[i].Index} {c.Events[i].A:R}");
            }
            ulong old = GoldenFlights.FlyContact(spec, out FlightCore held, tether: true, coulomb: false, body: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout,
                onRecord: r => rec59.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), after: c => Events(c, ev59), airmode: true);
            Assert.That(old, Is.EqualTo(GoldenFlights.Model59TetherHash), "tether with the 0-force hold: model 59's");
            Assert.That(held.Tether.Chain.ZeroForceHold, Is.True);
            ulong hash = GoldenFlights.FlyContact(spec, out FlightCore now, tether: true, body: false, edit: GoldenFlights.WithoutTheFrame, layout: GoldenFlights.Model57Layout,
                onRecord: r => rec62.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), after: c => Events(c, ev62), airmode: true);
            int same = 0;
            while (same < rec59.Count && same < rec62.Count && rec59[same] == rec62[same]) same++;
            int evSame = 0;
            while (evSame < ev59.Count && evSame < ev62.Count && ev59[evSame] == ev62[evSame]) evSame++;
            FiberLine a = held.Tether, b = now.Tether;
            string Line(FiberLine f) => f.Broken
                ? $"broken at step {f.BrokenAt} by {f.LastBreak.Mechanism} at {(FiberMember)f.LastBreak.Kind} {f.LastBreak.Member} (T {f.LastBreak.Tension:F3} N)"
                : $"whole, {f.PaidOut:F4} m paid out";
            TestContext.Progress.WriteLine($"model 62: the records model 59's for {same} of {rec62.Count} steps, the events for {evSame} of {ev59.Count} ({ev62.Count} now); "
                + $"model 59's line {Line(a)}, wedges {a.Chain.Wedges}, safe steps {a.Chain.SafeSteps}; model 62's {Line(b)}, wedges {b.Chain.Wedges}, safe steps {b.Chain.SafeSteps}; 0x{hash:X16}");
            Assert.That(hash, Is.EqualTo(GoldenFlights.Model62TetherHash), "tether with the Coulomb hold: model 62's");
        }

        // The physics integration 2 with the fiber's chain fixes (model 65, 62 merged onto 64), its audit against model 64, kept:
        // with the known-wrong 0-force hold (FlightSetup.TetherZeroForceHold: model 64's chain, its hold and its safe pass) the
        // tether golden is model 64's pin, every record and event; every golden without the line is 64's pin as it stands (no
        // part slot and no safe pass without the line). With the rules the records part where the chain first differs, and the
        // step, the line's state and its outcome print. Since model 68 over model 63's 3040 bytes (the shafts' block after it).
        [Test]
        public void WithoutTheCoulombHoldEachGoldenIsModel64s()
        {
            DroneSpec spec = Model70Drone.Spec(); // since model 74 its drone.json is a fixture (the nozzle straight on the 15° canister)
            var rec64 = new System.Collections.Generic.List<ulong>();
            var rec65 = new System.Collections.Generic.List<ulong>();
            var ev64 = new System.Collections.Generic.List<string>();
            var ev65 = new System.Collections.Generic.List<string>();
            void Events(FlightCore c, System.Collections.Generic.List<string> into)
            {
                for (int i = 0; i < c.Events.Count; i++) into.Add($"{c.State.Step} {c.Events[i].Kind} {c.Events[i].Index} {c.Events[i].A:R}");
            }
            ulong old = GoldenFlights.FlyContact(spec, out FlightCore held, tether: true, coulomb: false, layout: GoldenFlights.Model63Layout,
                onRecord: r => rec64.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), after: c => Events(c, ev64), airmode: true);
            Assert.That(old, Is.EqualTo(GoldenFlights.Model64TetherHash), "tether with the 0-force hold: model 64's");
            Assert.That(held.Tether.Chain.ZeroForceHold, Is.True);
            ulong hash = GoldenFlights.FlyContact(spec, out FlightCore now, tether: true, layout: GoldenFlights.Model63Layout,
                onRecord: r => rec65.Add(GoldenFlights.Fnv(0xCBF29CE484222325UL, r, 0, r.Length)), after: c => Events(c, ev65), airmode: true);
            Assert.That(now.Tether.Chain.ZeroForceHold, Is.False);
            int same = 0;
            while (same < rec64.Count && same < rec65.Count && rec64[same] == rec65[same]) same++;
            int evSame = 0;
            while (evSame < ev64.Count && evSame < ev65.Count && ev64[evSame] == ev65[evSame]) evSame++;
            FiberLine a = held.Tether, b = now.Tether;
            string Line(FiberLine f) => f.Broken
                ? $"broken at step {f.BrokenAt} by {f.LastBreak.Mechanism} at {(FiberMember)f.LastBreak.Kind} {f.LastBreak.Member} (T {f.LastBreak.Tension:F3} N)"
                : $"whole, {f.PaidOut:F4} m paid out, {f.Chain.Count} nodes, trail {f.TrailCount}";
            TestContext.Progress.WriteLine($"model 65: the records model 64's for {same} of {rec65.Count} steps, the events for {evSame} of {ev64.Count} ({ev65.Count} now); "
                + $"model 64's line {Line(a)}, wedges {a.Chain.Wedges}, safe steps {a.Chain.SafeSteps}; model 65's {Line(b)}, wedges {b.Chain.Wedges}, safe steps {b.Chain.SafeSteps}; "
                + $"the drone at rest {now.State.Position.Y:F4} m up (64: {held.State.Position.Y:F4}); 0x{hash:X16}");
            Assert.That(hash, Is.EqualTo(GoldenFlights.Model65TetherHash), "tether with the rules: model 65's");
        }

        static int CornerStates()
        {
            int at = -1;
            foreach (LogField f in LogFormat.StepFields)
                if (f.Name == "corner_state.rear_right") at = f.Offset;
            Assert.That(at, Is.GreaterThan(0));
            return at;
        }

        // The physics integration 2 with the skid's frame (model 60), its audit against the cuts on the props at rest (model 59),
        // kept: without the frame (WithoutTheFrame: one value both ways, every leg the cantilever along the skid as across it)
        // the contact, range and tether goldens are model 59's pins over the whole record, every record and event (the tether's
        // with the fiber's contacts, the cuts, the link and the failsafe); with it the flights that touch nothing (powered, rain,
        // tumble) keep their pins; no golden cracks, sets or parts a leg (every leg state zero on every record). So the frame is
        // the whole of 60's move against 59. p7g-legs' model 54 audit (WithoutTheFrameEachGoldenIsModel51s) is this test's
        // frame-off half on the older line; its pins are kept over 2816 bytes (below). Since model 63 (the bells on the line) over
        // model 57's 2944 bytes (GoldenFlights.Model57Layout), the bells on (WithoutTheBellsEachGoldenIsModel60s).
        [Test]
        public void WithoutTheFrameEachGoldenIsModel59s()
        {
            DroneSpec spec = Model63Drone.Spec();
            int states = 0, at = -1;
            foreach (LogField f in LogFormat.StepFields)
                if (f.Name == "leg_state.rear_right") at = f.Offset;
            Assert.That(at, Is.GreaterThan(0));
            void Intact(byte[] record)
            {
                for (int i = at; i < at + 4; i++) states |= record[i];
            }
            Action<FlightSetup> off = GoldenFlights.WithoutTheFrame;
            Assert.That(GoldenFlights.FlyContact(spec, out _, onRecord: Intact, edit: off, layout: GoldenFlights.Model57Layout, airmode: true), Is.EqualTo(GoldenFlights.Model59ContactHash), "contact, without the frame");
            Assert.That(GoldenFlights.FlyRange(spec, out _, onRecord: Intact, edit: off, layout: GoldenFlights.Model57Layout, airmode: true), Is.EqualTo(GoldenFlights.Model59RangeHash), "range, without the frame");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, coulomb: false, body: false, onRecord: Intact, edit: off, layout: GoldenFlights.Model57Layout, airmode: true), Is.EqualTo(GoldenFlights.Model59TetherHash), "tether, without the frame");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: GoldenFlights.Model63Layout, onRecord: Intact, airmode: true), Is.EqualTo(GoldenFlights.Model63ContactHash), "contact, with it");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: GoldenFlights.Model63Layout, onRecord: Intact, airmode: true), Is.EqualTo(GoldenFlights.Model63RangeHash), "range, with it");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: GoldenFlights.Model63Layout, tether: true, coulomb: false, body: false, onRecord: Intact, airmode: true), Is.EqualTo(GoldenFlights.Model63TetherHash), "tether, with it");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: GoldenFlights.Model63Layout, onRecord: Intact, airmode: true), Is.EqualTo(GoldenFlights.Model63PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: GoldenFlights.Model63Layout, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Intact, airmode: true),
                Is.EqualTo(GoldenFlights.Model63RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: GoldenFlights.Model63Layout), Is.EqualTo(GoldenFlights.Model63TumbleHash), "tumble");
            Assert.That(states, Is.EqualTo(0), "every leg intact in every golden");
        }

        // Its audit against the skid's frame (p7g-legs on the line, model 54, off 51), kept: without the cuts and the link
        // (FlightSetup.TetherWithoutCuts), the fiber's contacts (FlightSetup.TetherWithoutContacts, which has no cuts) and the
        // props at rest (ContactSpec.BladeRest off), over model 50's 2816 bytes, the contact, range and tether goldens are model
        // 54's pins, every record and event, and with the rest on too (no golden's prop rests on anything), the resting blades'
        // block zero on every record. So 55's, 57's and 59's mechanisms are the whole of 60's move against 54.
        [Test]
        public void WithoutTheCutsTheRestAndTheFiberContactsEachGoldenIsModel54s()
        {
            DroneSpec spec = Model63Drone.Spec();
            const int m50 = GoldenFlights.Model50Layout;
            int nonZero = 0;
            void Rest(byte[] record)
            {
                for (int i = m50; i < LogFormat.StepSize; i++) if (record[i] != 0) nonZero++;
            }
            foreach (bool rest in new[] { false, true })
            {
                Action<FlightSetup> edit = rest ? null : (Action<FlightSetup>)GoldenFlights.WithoutTheRest;
                string on = rest ? " (at rest)" : " (passing through)";
                Assert.That(GoldenFlights.FlyContact(spec, out _, layout: m50, onRecord: Rest, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model54ContactHash), "contact" + on);
                Assert.That(GoldenFlights.FlyRange(spec, out _, layout: m50, onRecord: Rest, edit: edit, airmode: true), Is.EqualTo(GoldenFlights.Model54RangeHash), "range" + on);
                Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, fiberContacts: false, layout: m50, onRecord: Rest, edit: edit, airmode: true),
                    Is.EqualTo(GoldenFlights.Model54TetherHash), "tether without the fiber's contacts and the cuts" + on);
                Assert.That(line.Tether.Contacts, Is.Null, "no fiber contacts");
                Assert.That(line.Tether.Cuts, Is.Null, "no cuts");
            }
            Assert.That(nonZero, Is.EqualTo(0), "no resting blade's load on any record");
        }

        // Its audit against p7g-legs (model 41), kept: without the line's mechanisms (no surfaces.json, no area laws, no line, no
        // props.blade, model 34's yaw I; no golden enters a cut), over p7g-legs' record (the record less the fiber's 104 and the
        // strikes' 384 bytes at 1904), every golden is model 41's pin, every record and event, and the two blocks are zero on
        // every record: the line's code changes no flight without its mechanisms, and they are the whole of each move against
        // p7g-legs.
        [Test]
        public void WithoutTheLineEachGoldenIsModel41s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(DroneSpec.Parse(Model63Drone.Text()));
            const int at = GoldenFlights.Model28Layout, skip = GoldenFlights.Model41Skip;
            int nonZero = 0;
            void Line(byte[] record)
            {
                for (int i = at; i < at + skip; i++) if (record[i] != 0) nonZero++;
            }
            const int end = GoldenFlights.Model46Layout; // since model 49 the record before the legs' lean, the lever off (1.3's softening)
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, onRecord: Line, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model41PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Line, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model41RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, at, skip, layout: end, onRecord: Line, soil: false, yawI: false, edit: GoldenFlights.WithoutTheLever, airmode: true),
                Is.EqualTo(GoldenFlights.Model41ContactHash), "contact, on the rigid floor");
            Assert.That(GoldenFlights.FlyRange(spec, out _, at, skip, layout: end, onRecord: Line, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false,
                edit: GoldenFlights.WithoutTheLever, airmode: true), Is.EqualTo(GoldenFlights.Model41RangeHash), "range, without the area laws");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, at, skip, end), Is.EqualTo(GoldenFlights.Model41TumbleHash), "tumble");
            Assert.That(nonZero, Is.EqualTo(0), "the fiber's and the strikes' blocks of a flight without the line and props.blade");
        }

        // D-066 (model 23 on p7b-airframe--power), its audit, kept: on model 22's drone (the 40 A limit, the 1.0 A no-load
        // current, the 0.92 kg packs and the 0.25 kg electronics put back) every golden is model 22's there and, since the
        // merge at model 26, the landing's merge's model 25 here, every record and event (the goldens fly the exact build, no
        // placement). Each older kept audit flies model 22's drone first. Since model 29 every older kept audit flies without
        // props.blade (GoldenFlights.WithoutTheBlade, p7f-damage 1.2), and since model 35 over model 29's 1904 bytes (the record
        // less 1.3's blade strikes, which are zero without props.blade: no strikes).
        [Test]
        public void OnModel22sDroneEachGoldenIsModel25s()
        {
            DroneSpec Model22() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(DroneSpec.Parse(Model63Drone.Text())));
            const int at = GoldenFlights.Model28Layout; // since model 31 the record's end is the fiber's block, since model 40 then the strikes
            Assert.That(GoldenFlights.FlyPowered(Model22(), out _, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model25PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model25RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model22(), out _, soil: false, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model25ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model22(), out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model25TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model22(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model25RangeHash),
                "range, without 1.12's area laws (model 33)");
        }

        // The merge's audit against p7b-airframe--power (model 24 there), kept: on drone.json without the interference and
        // D-064's sources, over model 19's 1808 bytes, the powered, rain and tumble goldens are that branch's pins, every record
        // and event (they touch nothing; the contact and range flights need D-064's wash and 1.6c's spring off too, which have
        // no switch: GoldenFlights' notes).
        [Test]
        public void WithoutTheIntegrationTheGoldenFlightsAreThePowerBranchs()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))); // the branch flew them
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, interference: false, wake: false, motors: false, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.PowerPoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.PowerRainHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.PowerTumbleHash), "tumble");
        }

        // The tumble's chain, kept over main's layout: on model 14's fit and on model 13's file it is their pins.
        [Test]
        public void OverMainsLayoutTheTumbleIsModel13sAnd14s()
        {
            DroneSpec model14 = GoldenFlights.WithModel14Fit(GoldenFlights.WithModel22Drone(DroneSpec.Parse(Model63Drone.Text())));
            Assert.That(GoldenFlights.FlyTumble(model14, out _, out _, RotorWindBytesAt, WindBytes, GoldenFlights.Model19Layout), Is.EqualTo(0x26CDDB5EDB155896UL), "model 14's");
            Assert.That(GoldenFlights.FlyTumble(DroneSpec.Parse(Model13Drone.Text()), out _, out _, RotorWindBytesAt, WindBytes, GoldenFlights.Model19Layout),
                Is.EqualTo(0xB371E06FF28075D8UL), "model 13's");
        }

        // Model 3's 992 bytes, then 1.6's contacts, then 1.4b's filter block: each parent's layout is the record less the
        // other's block. Then p7c-wind's 152 bytes (1.4's rotor winds, 1.7's rain, film and camera air), then p7b-airframe's:
        // 1.1's rotor angles, 1.2's rings, 1.3's surfaces and wash, 1.4's modes. Main's layout is the record less p7c-wind's
        // bytes; p7c-wind's is the record's first 1448.
        const int ContactBytesAt = 992, ContactBytes = 120, FilterBytesAt = ContactBytesAt + ContactBytes, RotorWindBytesAt = 1296, WindBytes = 152;
        const int RotorAngleAt = RotorWindBytesAt + WindBytes, RotorAngleBytes = 32, RingAt = RotorAngleAt + RotorAngleBytes, RingBytes = 72;
        const int GroundAt = RingAt + RingBytes, GroundBytes = 96, ModeAt = GroundAt + GroundBytes, ModeBytes = 160;
        const int InterferenceAt = ModeAt + ModeBytes, InterferenceBytes = 64, WakeAt = InterferenceAt + InterferenceBytes, WakeBytes = 32;
        const int FiberAt = WakeAt + WakeBytes, FiberBytes = 104, StrikesAt = FiberAt + FiberBytes, StrikesBytes = 384;
        const int LegAt = StrikesAt + StrikesBytes, LegBytes = 168, DamageAt = LegAt + LegBytes, DamageBytes = 224, LeanAt = DamageAt + DamageBytes, LeanBytes = 32;

        // The audits of p7b-airframe 1.3d and D-064, kept on the integration (model 24): without the interference, the rotor
        // wakes and the motors' resistance, friction and inertia spreads, over model 19's 1808 bytes, the powered, rain and
        // tumble goldens are model 19's, every record and event (the powered and rain flights find no surface, so the
        // re-ingested wash's new numbers never act there; the contact flight's wash has no switch, GoldenFlights' notes).
        [Test]
        public void WithoutEitherTheGoldenFlightsAreModel19s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(DroneSpec.Parse(Model63Drone.Text())));
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, interference: false, wake: false, motors: false, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model19PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model19RainHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model19TumbleHash), "tumble");
        }

        // The integration's audit against p7b-airframe--micro (model 23), kept: without the interference, over that branch's
        // 1840-byte record (the record less the interference's 64 bytes at 1808), each golden is its pin bit for bit, record
        // and events: the interference's code, its stream and the record's fields change no flight without it. Since model 25
        // the contact flight's pin is the branch's with 1.6c's stick spring (GoldenFlights.MicroContactHash's notes).
        [Test]
        public void WithoutTheInterferenceTheGoldenFlightsAreTheMicroBranchs()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(DroneSpec.Parse(Model63Drone.Text())));
            const int at = GoldenFlights.Model28Layout; // since model 31 the record's end is the fiber's block
            Assert.That(GoldenFlights.FlyPowered(spec, out _, InterferenceAt, InterferenceBytes, interference: false, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.MicroPoweredHash), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, InterferenceAt, InterferenceBytes, interference: false, soil: false, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.MicroContactHash), "contact");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, InterferenceAt, InterferenceBytes, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain,
                interference: false, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.MicroRainHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, InterferenceAt, InterferenceBytes, at), Is.EqualTo(GoldenFlights.MicroTumbleHash), "tumble");
        }

        // p7d-tether 1.6's audit (model 31): every golden flown without the line is model 28's over its 1904-byte record, every
        // record and event, and the fiber's block after it is zero on every record: the line's code (the modes' rebuild, the
        // chain's spool, the flight's line) changes no flight without it. Restated at the physics integration 2 (model 34): the
        // contact and range goldens fly p7a-ground's soil and area laws since then, so here they fly without them.
        [Test]
        public void OverModel28sRecordEachGoldenIsModel28s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model28Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < GoldenFlights.Model38Layout; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: at, onRecord: Tail, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model28PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Tail, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model28RainHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Tail, soil: false, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model28ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model28TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Tail, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model28RangeHash), "range");
            Assert.That(nonZero, Is.EqualTo(0), "the fiber's block of a flight without the line");
        }

        // The physics integration 2's audit against p7a-ground (model 34 against its 33), kept: over model 28's 1904 bytes, the
        // record less the line's block, the contact golden on the floor's soil and the range golden with the layers' area laws
        // are p7a-ground's model 33 pins, every record and event, and the line's block is zero on every record: the cavity
        // (neither flight enters a cut's opening), the line's code and its fdlibm Atan2 change neither flight. Since model 38 the
        // range flight meets the buried face (ContactModel.Buried) too, as GoldenFlights.Model33RangeOn38Hash: model 33's pin
        // with it off.
        [Test]
        public void OverModel28sRecordEachGoldenIsModel33s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model28Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < GoldenFlights.Model38Layout; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Tail, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model33ContactHash), "contact, on the soil");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Tail, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model33RangeOn38Hash),
                "range, with the area laws and model 38's buried face");
            Assert.That(nonZero, Is.EqualTo(0), "the fiber's block of a flight without the line");
        }

        // The physics integration 2's audit against p7d-tether (model 34 against its 31), kept: without surfaces.json and the
        // layers' area laws, over the whole 2008-byte record, the contact, range and tether goldens are p7d-tether's model 31
        // pins, every record and event: the soil and the area laws are the whole of each move (the cavity: no cut entered).
        [Test]
        public void WithoutTheGroundTheGoldenFlightsAreModel31s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model38Layout; // since model 40 the record's end is the blade strikes
            Assert.That(GoldenFlights.FlyContact(spec, out _, soil: false, yawI: false, layout: at, airmode: true), Is.EqualTo(GoldenFlights.Model31ContactHash), "contact, on the rigid floor");
            Assert.That(GoldenFlights.FlyRange(spec, out _, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, layout: at, airmode: true), Is.EqualTo(GoldenFlights.Model31RangeHash),
                "range, without the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, soil: false, tether: true, fiberContacts: false, coulomb: false, yawI: false, layout: at, recirculation: false, airmode: true), Is.EqualTo(GoldenFlights.Model31TetherHash), "tether, on the rigid floor");
        }

        // The physics integration 2's audit against p7c-wind's cavity (model 34 against its 30), kept: the range golden flies
        // WindSetup.ForMap's Calm, which carries the test range's cuts (E01's run and niches, Z04's shaft); with them dropped (the
        // cavity off) it is the same bits, since it never enters a cut's opening. Every other golden flies the stub's floor,
        // which has no cut.
        [Test]
        public void WithoutTheCutsTheRangeGoldenIsTheSame()
        {
            DroneSpec spec = Model63Drone.Spec();
            WindSetup air = WindSetup.ForMap(RangeScenarios.World, WindLevel.Calm);
            Assert.That(air.Cuts, Is.Not.Null.And.Not.Empty, "the map's wind carries its cuts");
            air.Cuts = null;
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: GoldenFlights.Model63Layout, wind: air, airmode: true), Is.EqualTo(GoldenFlights.Model63RangeHash));
        }

        // Betaflight's yaw I's audit (model 36 against 34), kept: on model 34's yaw I (GoldenFlights.Model34YawI, i_yaw just
        // under 12, whose yaw Ki is model 34's ITERM_SCALE × 30 bit for bit) every golden is model 34's pin, every record and
        // event: pid_init.c's × 2.5 on yaw's Ki is the whole move. Each kept audit above against an earlier model flies it too;
        // the tumble has no rotors and keeps its hash (TheTumbleGoldenIsPinned). Since model 38 the range flight meets the
        // buried face too, as GoldenFlights.Model34RangeOn38Hash: model 34's pin with it off.
        [Test]
        public void OnModel34sYawIEachGoldenIsModel34s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model38Layout; // since model 40 the record's end is the blade strikes
            Assert.That(GoldenFlights.FlyPowered(spec, out _, yawI: false, layout: at, airmode: true), Is.EqualTo(GoldenFlights.Model34PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, yawI: false, layout: at, airmode: true),
                Is.EqualTo(GoldenFlights.Model34RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, yawI: false, layout: at, airmode: true), Is.EqualTo(GoldenFlights.Model34ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, yawI: false, layout: at, airmode: true), Is.EqualTo(GoldenFlights.Model34RangeOn38Hash), "range, with model 38's buried face");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, fiberContacts: false, coulomb: false, yawI: false, layout: at, recirculation: false, airmode: true), Is.EqualTo(GoldenFlights.Model34TetherHash), "tether");
        }

        // The physics integration 2 with the tip slide and the fountain (model 48), its audit against the line (model 46), kept:
        // without the fountain (design 4's field, FlightSetup.TetherDesign4Air: p7d-tether 1.6b's own known-wrong, its audit
        // against 44 when it was alone) every golden is model 46's pin over the whole 2784 bytes, every record and event: the
        // tether golden on design 4's air is model 46's, and the goldens without the line fly as at 46 (their pins are unchanged).
        // The tip slide's side: no golden's disc cuts the ground or a soil shape (0 blade passes in the contact, range and tether
        // flights, on the floor's soil and S04's straw), so the carve, the stones and the immersed disc are no move in any golden,
        // and with the tip slide off (ContactSpec.TipSlide, the known-wrong: model 43's carve) each is the same bits. Since model
        // 50 (the lever on the line) the audit is against p7g-legs 1.4's model 49 (the lever on 46): on design 4's air the tether
        // golden is model 49's pin over the whole 2816 bytes, and with the lever off too model 46's over 2784; the goldens without
        // the line are model 49's pins (unchanged by the merge).
        [Test]
        public void WithoutTheFountainEachGoldenIsModel49s()
        {
            DroneSpec spec = Model63Drone.Spec();
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, coulomb: false, fiberContacts: false, recirculation: false, layout: GoldenFlights.Model50Layout,
                edit: GoldenFlights.WithoutTheFrame, airmode: true),
                Is.EqualTo(GoldenFlights.Model49TetherHash),
                "tether, on design 4's air (since model 60 without the skid's frame)");
            Assert.That(line.Strikes.Passes, Is.EqualTo(0), "no blade passed the soil or a shape");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, fiberContacts: false, coulomb: false, layout: GoldenFlights.Model46Layout, recirculation: false, edit: GoldenFlights.WithoutTheLever, airmode: true),
                Is.EqualTo(GoldenFlights.Model46TetherHash), "tether, on design 4's air and the lever off");
            System.Action<FlightSetup> off = s => s.Contacts.TipSlide = false;
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore contact, layout: GoldenFlights.Model63Layout, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model63ContactHash), "contact, the tip slide off");
            Assert.That(contact.Strikes.Passes, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyRange(spec, out FlightCore range, layout: GoldenFlights.Model63Layout, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model63RangeHash), "range, the tip slide off");
            Assert.That(range.Strikes.Passes, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore tether, layout: GoldenFlights.Model63Layout, tether: true, coulomb: false, body: false, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model63TetherHash), "tether, the tip slide off");
            Assert.That(tether.Strikes.Passes, Is.EqualTo(0));
        }

        // p7d-tether 1.6: the contact flight with the line, pinned (GoldenFlights.Model63TetherHash), the same twice, the line paying out
        // and its pull reaching the drone.
        [Test]
        public void TheTetherGoldenFlightIsPinned()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            ulong hash = GoldenFlights.FlyContact(spec, out FlightCore core, tether: true);
            Assert.That(hash, Is.EqualTo(GoldenFlights.TetherHash), "the pinned golden (a re-record is audited and raises the model version)");
            Assert.That(core.Tether.PaidOut, Is.GreaterThan(0), "the line paid out");
            Assert.That(core.Tether.Chain.Wedges, Is.EqualTo(0));
            Assert.That(core.State.Crashed, Is.False);
            Assert.That(core.Tether.Broken, Is.False, "the pilot's account: a normal liftoff and landing do not cut it (since model 64, D01's canister)");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true), Is.EqualTo(hash), "a second flight gives the same bits");
        }

        // p7f-damage 1.2's audit, kept (prop-motor-damage's "The intact blade is the published table"): with the blades' factory
        // pitch off and the flapping's inertia at model 26's uniform rod, the rotors carry their blades as elements but stay
        // sound, skip their path and fly the published table: every golden is model 26's, every record and event; so is every
        // golden on a drone.json without props.blade (no elements, no pitch draws, the rod). Since model 40 (the physics
        // integration 2) each flies without the line's mechanisms (no soil, no area laws, model 34's yaw I; no line, no cut).
        [Test]
        public void WithThePitchOffAndTheRodsInertiaEachGoldenIsModel26s()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text())); // since model 46
            System.Action<FlightSetup> model26 = s =>
            {
                GoldenFlights.WithoutThePitch(s);
                GoldenFlights.WithTheRodsInertia(spec)(s);
            };
            const int at = GoldenFlights.Model29Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore core, edit: model26, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model26PoweredHash), "powered");
            Assert.That(core.Propulsion.Rotor(0), Is.Not.Null, "the blades as elements, sound");
            Assert.That(core.Propulsion.Damaged, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: model26, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model26RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, edit: model26, layout: at, soil: false, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model26ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, edit: model26, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model26RangeHash), "range");
            DroneSpec bladeless = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text())));
            Assert.That(GoldenFlights.FlyPowered(bladeless, out _, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model26PoweredHash), "powered without props.blade");
            Assert.That(GoldenFlights.FlyContact(bladeless, out _, layout: at, soil: false, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model26ContactHash), "contact without props.blade");
        }

        // p7f-damage 1.3's audit, kept: the goldens' discs cut nothing (no blade strike, no BladeStrike event), so over model 29's
        // 1904 bytes every golden is model 29's pin, every record and event, and the strikes' 384 bytes are zero throughout; the
        // strikes' switch (ContactSpec.BladeStrikes) changes none of them. Since model 40 each flies without the line's
        // mechanisms (no soil, no area laws, model 34's yaw I; no line, no cut), and the strikes' block follows the fiber's.
        [Test]
        public void OverModel29sLayoutEachGoldenIsModel29s()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text())), now = Model63Drone.Spec(); // since model 46
            const int at = GoldenFlights.Model29Layout;
            bool zero = true;
            System.Action<byte[]> strikes = record =>
            {
                for (int i = GoldenFlights.Model38Layout; i < GoldenFlights.Model44Layout; i++) zero &= record[i] == 0;
            };
            System.Action<FlightSetup> off = s => { if (s.Contacts != null) s.Contacts.BladeStrikes = false; };
            Assert.That(GoldenFlights.FlyPowered(spec, out _, onRecord: strikes, layout: at, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model29PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: strikes, layout: at, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model29RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore contact, onRecord: strikes, layout: at, soil: false, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model29ContactHash), "contact");
            Assert.That(contact.Strikes, Is.Not.Null, "the strikes on");
            Assert.That(contact.Strikes.Passes, Is.EqualTo(0), "no blade passed a shape");
            Assert.That(GoldenFlights.FlyRange(spec, out FlightCore range, onRecord: strikes, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model29RangeHash), "range");
            Assert.That(range.Strikes.Passes, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model29TumbleHash), "tumble");
            Assert.That(zero, Is.True, "the strikes' bytes are zero");
            Assert.That(GoldenFlights.FlyContact(now, out _, layout: GoldenFlights.Model63Layout, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model63ContactHash), "contact, the strikes off");
            Assert.That(GoldenFlights.FlyRange(now, out _, layout: GoldenFlights.Model63Layout, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model63RangeHash), "range, the strikes off");
            Assert.That(GoldenFlights.FlyContact(now, out _, layout: GoldenFlights.Model63Layout, tether: true, coulomb: false, body: false, edit: off, airmode: true), Is.EqualTo(GoldenFlights.Model63TetherHash), "tether, the strikes off");
        }

        // The physics integration 2 with p7f-damage (model 40), its audit against the line (model 38), kept: on a drone.json
        // without props.blade (no blades as elements, no pitch draws, the flapping's rod, and so no strikes), over the line's
        // 2008 bytes, every golden is model 38's pin, every record and event, and the strikes' block is zero on every record:
        // p7f-damage's code (the rotors' path, the disc's slab pairs, the retired prop crash) changes no flight without the
        // blade's table, and props.blade is the whole of each move against the line.
        [Test]
        public void WithoutTheBladeEachGoldenIsModel38s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model38Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < GoldenFlights.Model44Layout; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore p, layout: at, onRecord: Tail, airmode: true), Is.EqualTo(GoldenFlights.Model38PoweredHash), "powered");
            Assert.That(p.Strikes, Is.Null, "no strikes without the blade's table");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Tail, airmode: true),
                Is.EqualTo(GoldenFlights.Model38RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Tail, airmode: true), Is.EqualTo(GoldenFlights.Model38ContactHash), "contact, on the soil");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Tail, airmode: true), Is.EqualTo(GoldenFlights.Model38RangeHash), "range, with the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, fiberContacts: false, coulomb: false, layout: at, onRecord: Tail, recirculation: false, airmode: true), Is.EqualTo(GoldenFlights.Model38TetherHash), "tether");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model38TumbleHash), "tumble");
            Assert.That(nonZero, Is.EqualTo(0), "the strikes' block of a flight without props.blade");
        }

        // Its audit against p7f-damage (model 35), kept: without the line's mechanisms (no surfaces.json, no area laws, no line,
        // model 34's yaw I; no golden enters a cut), over p7f-damage's record (the record less the fiber's 104 bytes at 1904),
        // every golden is model 35's pin, every record and event, and the fiber's block is zero on every record: the line's code
        // changes no flight without its mechanisms, and they are the whole of each move against p7f-damage.
        [Test]
        public void WithoutTheLineEachGoldenIsModel35s()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(Model63Drone.Text())); // since model 46
            const int at = GoldenFlights.Model28Layout, skip = GoldenFlights.Model35Skip, end = GoldenFlights.Model44Layout;
            int nonZero = 0;
            void Fiber(byte[] record)
            {
                for (int i = at; i < at + skip; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, onRecord: Fiber, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model35PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Fiber, yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model35RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, at, skip, layout: end, onRecord: Fiber, soil: false, yawI: false, airmode: true), Is.EqualTo(GoldenFlights.Model35ContactHash),
                "contact, on the rigid floor");
            Assert.That(GoldenFlights.FlyRange(spec, out _, at, skip, layout: end, onRecord: Fiber, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, airmode: true),
                Is.EqualTo(GoldenFlights.Model35RangeHash), "range, without the area laws");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, at, skip, end), Is.EqualTo(GoldenFlights.Model35TumbleHash), "tumble");
            Assert.That(nonZero, Is.EqualTo(0), "the fiber's block of a flight without the line");
        }

        // p7b-airframe 1.3's check, kept without model 9's pin (it flew the stand-in's air): 193–300 m up the powered golden's
        // rays find no surface within their reach.
        [Test]
        public void ThePoweredGoldenFindsNoSurface()
        {
            GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore core);
            Assert.That(core.GroundEffect, Is.Not.Null);
            Assert.That(core.Trace.GroundDistance.FrontLeft, Is.GreaterThan(100), "the floor sampled far below");
            Assert.That(core.Trace.CeilingDistance.FrontLeft, Is.EqualTo(double.PositiveInfinity), "nothing above");
        }

        [Test]
        public void TheRecordKeepsEachParentsBytesInPlace()
        {
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, ContactBytesAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "contact_force.x")),
                "1.6 appends its fields after model 3's 992 bytes");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, FilterBytesAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "fc.gyro_filtered.roll")),
                "1.4b's filter block follows the contacts' 120 bytes");
            Assert.That(RotorWindBytesAt - FilterBytesAt, Is.EqualTo(1176 - 992), "1.4b's whole block, to model 5's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, RotorWindBytesAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "rotor_wind.rear_right.x")),
                "p7c-wind 1.4's rotor winds follow it");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, RotorWindBytesAt + 96), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "rain_force.x")),
                "four vectors, then p7c-wind 1.7's rain");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, RotorAngleAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "rotor_angle.rear_right")),
                "p7b-airframe 1.1's rotor angles follow p7c-wind's 152 bytes");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, RingAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "ring_inflow.rear_right")),
                "1.2's rings follow the rotor angles");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GroundAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "ground_distance.rear_right")),
                "1.3's surfaces and wash follow the rings");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, ModeAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "mode_shape0")),
                "1.4's modes follow the surfaces");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, InterferenceAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "interference_inflow.rear_right")),
                "1.3d's interference follows the modes");
            Assert.That(InterferenceAt, Is.EqualTo(GoldenFlights.Model19Layout), "model 19's record is the record before it");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, WakeAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "wake_inflow.rear_right")),
                "D-064's rotor wakes follow the interference");
            Assert.That(GoldenFlights.Model28Layout - WakeAt, Is.EqualTo(WakeBytes), "to model 28's record's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GoldenFlights.Model28Layout), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "tether.paid_out")),
                "p7d-tether 1.6's fiber block follows model 28's record");
            Assert.That(GoldenFlights.Model38Layout - GoldenFlights.Model28Layout, Is.EqualTo(FiberBytes), "to the line's record's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, StrikesAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "blade_strike.rear_right.0.kind")),
                "p7f-damage 1.3's blade strikes follow the fiber's block (model 40)");
            Assert.That(StrikesAt, Is.EqualTo(GoldenFlights.Model38Layout), "model 38's record is the record before them");
            Assert.That(FiberAt, Is.EqualTo(GoldenFlights.Model29Layout), "and model 29's the record before the fiber's block");
            Assert.That(GoldenFlights.Model44Layout - StrikesAt, Is.EqualTo(StrikesBytes), "to model 44's record's end");
            Assert.That(LegAt, Is.EqualTo(GoldenFlights.Model44Layout), "model 44's record is the record before the legs'");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, LegAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "leg_state.rear_right")),
                "p7g-legs 1.2's legs follow the blade strikes (model 46)");
            Assert.That(DamageAt, Is.EqualTo(GoldenFlights.Model39Layout), "p7g-legs' model 39 record ends before a damaged leg's (its first 1904 bytes, then the legs' state)");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, DamageAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "leg_side_give.rear_right")),
                "p7g-legs 1.3's side give, jaws and folds follow the legs' state");
            Assert.That(LeanAt, Is.EqualTo(GoldenFlights.Model46Layout), "model 46's record ends before the legs' lean");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, LeanAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "leg_lean.rear_right")),
                "p7g-legs 1.4's lean follows the side give, jaws and folds (model 49)");
            Assert.That(GoldenFlights.Model50Layout - LeanAt, Is.EqualTo(LeanBytes), "to model 50's record's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GoldenFlights.Model50Layout), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "blade_load_0.rear_right")),
                "p7f-damage 1.3b's resting blades follow the lean (model 52)");
            Assert.That(GoldenFlights.Model57Layout - GoldenFlights.Model50Layout, Is.EqualTo(128), "four corners' two blades' load and deflection, to model 57's record's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GoldenFlights.Model57Layout), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "bell_scrape.rear_right")),
                "p7f-damage 1.5's bells follow the resting blades (model 58)");
            Assert.That(GoldenFlights.Model63Layout - GoldenFlights.Model57Layout, Is.EqualTo(96), "four motors' scrape, rub and Kt change, to model 63's record's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GoldenFlights.Model63Layout), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "shaft_bend_p.rear_right")),
                "p7f-damage 1.6's shafts follow the bells (model 66)");
            Assert.That(GoldenFlights.Model68Layout - GoldenFlights.Model63Layout, Is.EqualTo(128), "four motors' bend, bearings' drag and ball-pass frequency, to model 68's record's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GoldenFlights.Model68Layout), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "winding_temperature.rear_right")),
                "p7f-damage 1.6b's heat follows the shafts (model 70)");
            Assert.That(GoldenFlights.Model73Layout - GoldenFlights.Model68Layout, Is.EqualTo(192), "four motors' temperatures, Kt change, dirt, its drag and state, to model 73's record's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GoldenFlights.Model73Layout), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "arm_bend.rear_right")),
                "p7f-damage 1.6's losses' arms follow the heat (model 75; after the shafts in model 72's record)");
            Assert.That(LogFormat.StepSize - GoldenFlights.Model73Layout, Is.EqualTo(176), "four arms' bend, twist, their rates and load, the gyro's peak and the arms broken, to the record's end");
        }
    }
}
