using Fpv450.Sim.Flight;
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
            ulong with = GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore core, ContactBytesAt, ContactBytes);
            ulong without = GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore bare, ContactBytesAt, ContactBytes, false);
            Assert.That(core.Contacts, Is.Not.Null, "the flight carries its contacts");
            Assert.That(bare.Contacts, Is.Null);
            Assert.That(core.Contacts.ImpactCount, Is.EqualTo(0));
            Assert.That(with, Is.EqualTo(without), "1.4b's layout, every bit, with the contacts and without them");
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
            Assert.That(sink, Is.InRange(0.010, 0.026), "sunk into the mat as at rest there (RangeContactTests)");
            Assert.That(speed, Is.LessThan(2e-3), "at rest (1.15 mm/s at 12 s on D-066's model 23, under 1 mm/s on model 22)");
            Assert.That(GoldenFlights.FlyRange(spec, out _), Is.EqualTo(hash), "a second flight gives the same bits");
            Assert.That(hash, Is.EqualTo(GoldenFlights.RangeHash), "the pinned golden (a re-record is audited and raises the model version)");
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
        // a kept test until model 22) is a temporary edit since model 24 (GoldenFlights.RangeHash's notes).
        [Test]
        public void InStillAirTheGoldenFlightsAreModel15s()
        {
            DroneSpec spec = GoldenFlights.WithModel22Drone(GoldenFlights.WithRotorDrag(RepositoryDrone.Text(), GoldenFlights.TunedHForce));
            WindSetup still = WindSetup.Of(WindLevel.Still);
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore core, RotorWindBytesAt, WindBytes, wind: still, interference: false, wake: false, motors: false,
                layout: at), Is.EqualTo(GoldenFlights.Model15StillPoweredHash), "powered");
            Assert.That(core.Contacts.ImpactCount, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, RotorWindBytesAt, WindBytes, at), Is.EqualTo(GoldenFlights.Model15TumbleHash), "tumble");
        }

        // p7g-legs 1.1 (model 37), its audit, kept: on model 26's legs (WithModel26Legs: the CFRP legs' and skids' 11 and 20 g,
        // the assumed 10 kN/m spring and its 45 N·s/m damper, the leg shapes' 6600 N/m) every golden is model 26's, every
        // record and event, so the legs' data is the whole change.
        [Test]
        public void OnModel26sLegsEachGoldenIsModel26s()
        {
            DroneSpec Model26() => GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()));
            Assert.That(GoldenFlights.FlyPowered(Model26(), out _), Is.EqualTo(GoldenFlights.Model26PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model26(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain),
                Is.EqualTo(GoldenFlights.Model26RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model26(), out _), Is.EqualTo(GoldenFlights.Model26ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model26(), out _, out _), Is.EqualTo(GoldenFlights.Model26TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model26(), out _), Is.EqualTo(GoldenFlights.Model26RangeHash), "range");
        }

        // D-066 (model 23 on p7b-airframe--power), its audit, kept: on model 22's drone (the 40 A limit, the 1.0 A no-load
        // current, the 0.92 kg packs and the 0.25 kg electronics put back) every golden is model 22's there and, since the
        // merge at model 26, the landing's merge's model 25 here, every record and event (the goldens fly the exact build, no
        // placement). Each older kept audit flies model 22's drone first.
        [Test]
        public void OnModel22sDroneEachGoldenIsModel25s()
        {
            DroneSpec Model22() => GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text()));
            Assert.That(GoldenFlights.FlyPowered(Model22(), out _), Is.EqualTo(GoldenFlights.Model25PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain),
                Is.EqualTo(GoldenFlights.Model25RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model22(), out _), Is.EqualTo(GoldenFlights.Model25ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model22(), out _, out _), Is.EqualTo(GoldenFlights.Model25TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model22(), out _), Is.EqualTo(GoldenFlights.Model25RangeHash), "range");
        }

        // The merge's audit against p7b-airframe--power (model 24 there), kept: on drone.json without the interference and
        // D-064's sources, over model 19's 1808 bytes, the powered, rain and tumble goldens are that branch's pins, every record
        // and event (they touch nothing; the contact and range flights need D-064's wash and 1.6c's spring off too, which have
        // no switch: GoldenFlights' notes).
        [Test]
        public void WithoutTheIntegrationTheGoldenFlightsAreThePowerBranchs()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text())); // the branch flew them
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, interference: false, wake: false, motors: false, layout: at),
                Is.EqualTo(GoldenFlights.PowerPoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: at), Is.EqualTo(GoldenFlights.PowerRainHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.PowerTumbleHash), "tumble");
        }

        // The tumble's chain, kept over main's layout: on model 14's fit and on model 13's file it is their pins.
        [Test]
        public void OverMainsLayoutTheTumbleIsModel13sAnd14s()
        {
            DroneSpec model14 = GoldenFlights.WithModel14Fit(GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text())));
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

        // The audits of p7b-airframe 1.3d and D-064, kept on the integration (model 24): without the interference, the rotor
        // wakes and the motors' resistance, friction and inertia spreads, over model 19's 1808 bytes, the powered, rain and
        // tumble goldens are model 19's, every record and event (the powered and rain flights find no surface, so the
        // re-ingested wash's new numbers never act there; the contact flight's wash has no switch, GoldenFlights' notes).
        [Test]
        public void WithoutEitherTheGoldenFlightsAreModel19s()
        {
            DroneSpec spec = GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text()));
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, interference: false, wake: false, motors: false, layout: at),
                Is.EqualTo(GoldenFlights.Model19PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: at), Is.EqualTo(GoldenFlights.Model19RainHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model19TumbleHash), "tumble");
        }

        // The integration's audit against p7b-airframe--micro (model 23), kept: without the interference, over that branch's
        // 1840-byte record (the record less the interference's 64 bytes at 1808), each golden is its pin bit for bit, record
        // and events: the interference's code, its stream and the record's fields change no flight without it. Since model 25
        // the contact flight's pin is the branch's with 1.6c's stick spring (GoldenFlights.MicroContactHash's notes).
        [Test]
        public void WithoutTheInterferenceTheGoldenFlightsAreTheMicroBranchs()
        {
            DroneSpec spec = GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text()));
            Assert.That(GoldenFlights.FlyPowered(spec, out _, InterferenceAt, InterferenceBytes, interference: false), Is.EqualTo(GoldenFlights.MicroPoweredHash), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, InterferenceAt, InterferenceBytes, interference: false), Is.EqualTo(GoldenFlights.MicroContactHash), "contact");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, InterferenceAt, InterferenceBytes, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain,
                interference: false), Is.EqualTo(GoldenFlights.MicroRainHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, InterferenceAt, InterferenceBytes), Is.EqualTo(GoldenFlights.MicroTumbleHash), "tumble");
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
            Assert.That(LogFormat.StepSize - WakeAt, Is.EqualTo(WakeBytes), "to the record's end");
        }
    }
}
