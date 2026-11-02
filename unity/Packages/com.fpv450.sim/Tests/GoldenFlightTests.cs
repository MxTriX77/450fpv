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

        // p7a-ground 1.10's audit (model 23), kept: without surfaces.json the contact golden is model 18's flight on the rigid
        // floor, every record and event bit for bit; only the soil moves it.
        [Test]
        public void WithoutTheSoilTheContactGoldenIsModel18s()
        {
            Assert.That(GoldenFlights.FlyContact(RepositoryDrone.Spec(), out _, soil: false), Is.EqualTo(GoldenFlights.Model18ContactHash));
        }

        // The merge of p7c-wind and unity/main (model 17), its audit against unity/main, kept: on drone.json with main's tuned
        // rotor drag (0.25) in still air, where the field and the stand-in both blow nothing, each golden over main's
        // 1656-byte layout (the record less p7c-wind's 152 bytes at 1296) is main's model 15 flight in still air, bit for bit
        // (pinned from main's code with the goldens' air set still, a temporary edit), and the tumble, which has no air,
        // main's model 15 pin. Main's kept audits of models 6–14 fly the stand-in's Windy and Calm air, which is gone.
        [Test]
        public void InStillAirTheGoldenFlightsAreModel15s()
        {
            DroneSpec spec = GoldenFlights.WithRotorDrag(RepositoryDrone.Text(), GoldenFlights.TunedHForce);
            WindSetup still = WindSetup.Of(WindLevel.Still);
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore core, RotorWindBytesAt, WindBytes, wind: still), Is.EqualTo(GoldenFlights.Model15StillPoweredHash), "powered");
            Assert.That(core.Contacts.ImpactCount, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyContact(spec, out _, RotorWindBytesAt, WindBytes, wind: still, soil: false), Is.EqualTo(GoldenFlights.Model15StillContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, RotorWindBytesAt, WindBytes), Is.EqualTo(GoldenFlights.Model15TumbleHash), "tumble");
        }

        // The tumble's chain, kept over main's layout: on model 14's fit and on model 13's file it is their pins.
        [Test]
        public void OverMainsLayoutTheTumbleIsModel13sAnd14s()
        {
            DroneSpec model14 = GoldenFlights.WithModel14Fit(DroneSpec.Parse(RepositoryDrone.Text()));
            Assert.That(GoldenFlights.FlyTumble(model14, out _, out _, RotorWindBytesAt, WindBytes), Is.EqualTo(0x26CDDB5EDB155896UL), "model 14's");
            Assert.That(GoldenFlights.FlyTumble(DroneSpec.Parse(Model13Drone.Text()), out _, out _, RotorWindBytesAt, WindBytes), Is.EqualTo(0xB371E06FF28075D8UL), "model 13's");
        }

        // Model 3's 992 bytes, then 1.6's contacts, then 1.4b's filter block: each parent's layout is the record less the
        // other's block. Then p7c-wind's 152 bytes (1.4's rotor winds, 1.7's rain, film and camera air), then p7b-airframe's:
        // 1.1's rotor angles, 1.2's rings, 1.3's surfaces and wash, 1.4's modes. Main's layout is the record less p7c-wind's
        // bytes; p7c-wind's is the record's first 1448.
        const int ContactBytesAt = 992, ContactBytes = 120, FilterBytesAt = ContactBytesAt + ContactBytes, RotorWindBytesAt = 1296, WindBytes = 152;
        const int RotorAngleAt = RotorWindBytesAt + WindBytes, RotorAngleBytes = 32, RingAt = RotorAngleAt + RotorAngleBytes, RingBytes = 72;
        const int GroundAt = RingAt + RingBytes, GroundBytes = 96, ModeAt = GroundAt + GroundBytes, ModeBytes = 160;

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
            Assert.That(LogFormat.StepSize - ModeAt, Is.EqualTo(ModeBytes), "to the record's end");
        }
    }
}
