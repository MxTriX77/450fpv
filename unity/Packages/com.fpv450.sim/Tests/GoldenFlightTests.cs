using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
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

        // The kept audits of models 7–11 fly drone.json on model 11's pack table (p7b-airframe 1.5 re-mapped it).
        static DroneSpec Model11() => GoldenFlights.WithModel11Packs(DroneSpec.Parse(RepositoryDrone.Text()));

        // p7b-airframe 1.5's audit: on model 11's pack table, the record's layout unchanged, all three are model 11's bit for
        // bit, so the new pins move by the table alone.
        [Test]
        public void OnModel11sPackTableEachGoldenIsModel11s()
        {
            Assert.That(GoldenFlights.FlyPowered(Model11(), out _, ModeAt, ModeBytes, modes: false), Is.EqualTo(0x9057D18CF741A255UL), "powered");
            Assert.That(GoldenFlights.FlyContact(Model11(), out _, ModeAt, ModeBytes, modes: false), Is.EqualTo(0xF6E32F2FDC831268UL), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model11(), out _, out _, ModeAt, ModeBytes), Is.EqualTo(0x58EF4F6AE8D111D8UL), "tumble");
        }

        // Model 3's 992 bytes, then 1.6's contacts, then 1.4b's filter block: each parent's layout is the record less the
        // other's block. Then p7b-airframe 1.1's rotor angles: model 7's layout is the record less them.
        const int ContactBytesAt = 992, ContactBytes = 120, FilterBytesAt = ContactBytesAt + ContactBytes;
        const int RotorAngleAt = 1296, RotorAngleBytes = 32, RingAt = RotorAngleAt + RotorAngleBytes, RingBytes = 72;
        const int GroundAt = RingAt + RingBytes, GroundBytes = 96, ModeAt = GroundAt + GroundBytes, ModeBytes = 160;

        // p7b-airframe 1.1's audit, kept: with the motors' asymmetry off (and 1.2's rings), over model 7's 1296-byte layout
        // with its events, each golden is model 7's bit for bit (the rotors' angles turn, but nothing reads them without an
        // unbalance).
        [Test]
        public void WithTheAsymmetryOffEachGoldenIsModel7s()
        {
            DroneSpec spec = Model11();
            const int skip = RotorAngleBytes + RingBytes + GroundBytes + ModeBytes;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, RotorAngleAt, skip, asymmetry: false, ring: false, ground: false, flapping: false, modes: false), Is.EqualTo(0xA65FBFBCBCFC5B96UL), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, RotorAngleAt, skip, false, false, ground: false, flapping: false, modes: false), Is.EqualTo(0x3AF33645FF8F1919UL), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, RotorAngleAt, skip), Is.EqualTo(0xE3178EE7FF322698UL), "tumble");
        }

        // p7b-airframe 1.2's audit: with the vortex rings off (and 1.3's ground effect), over model 8's 1328-byte layout with
        // its events, each golden is model 8's bit for bit (the rings' 72 bytes are zero without them).
        [Test]
        public void WithTheRingsOffEachGoldenIsModel8s()
        {
            DroneSpec spec = Model11();
            const int skip = RingBytes + GroundBytes + ModeBytes;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, RingAt, skip, ring: false, ground: false, flapping: false, modes: false), Is.EqualTo(0x08FBAF9331110BE9UL), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, RingAt, skip, ring: false, ground: false, flapping: false, modes: false), Is.EqualTo(0x5D45B356ECD6B940UL), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, RingAt, skip), Is.EqualTo(0xF7E0F587B45FA098UL), "tumble");
        }

        // p7b-airframe 1.3's audit: with the ground effect off, over model 9's 1400-byte layout with its events, each golden
        // is model 9's bit for bit (the surfaces' and the wash's 96 bytes are zero without it).
        [Test]
        public void WithTheGroundEffectOffEachGoldenIsModel9s()
        {
            DroneSpec spec = Model11();
            Assert.That(GoldenFlights.FlyPowered(spec, out _, GroundAt, GroundBytes + ModeBytes, ground: false, flapping: false, modes: false), Is.EqualTo(0x5AF86BCB6FD0B3A2UL), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, GroundAt, GroundBytes + ModeBytes, ground: false, flapping: false, modes: false), Is.EqualTo(0x8D965FFD8708C6C3UL), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, GroundAt, GroundBytes + ModeBytes), Is.EqualTo(0x50541A5726F695D8UL), "tumble");
        }

        // 1.3c's audit: with the flapping off, the record's layout unchanged, all three are model 10's bit for bit.
        [Test]
        public void WithTheFlappingOffEachGoldenIsModel10s()
        {
            DroneSpec spec = Model11();
            Assert.That(GoldenFlights.FlyPowered(spec, out _, ModeAt, ModeBytes, flapping: false, modes: false), Is.EqualTo(0x0B91602E467653F4UL), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, ModeAt, ModeBytes, flapping: false, modes: false), Is.EqualTo(0x05EDD1881425872EUL), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, ModeAt, ModeBytes), Is.EqualTo(0x58EF4F6AE8D111D8UL), "tumble");
        }

        // 1.4's audit: with the airframe's modes off, over model 12's 1496-byte layout, all three are model 12's bit for bit.
        [Test]
        public void WithTheModesOffEachGoldenIsModel12s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Assert.That(GoldenFlights.FlyPowered(spec, out _, ModeAt, ModeBytes, modes: false), Is.EqualTo(0x63D668DD01A1E587UL), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, ModeAt, ModeBytes, modes: false), Is.EqualTo(0x8B50CC1BD8EBC8FFUL), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, ModeAt, ModeBytes), Is.EqualTo(0x58EF4F6AE8D111D8UL), "tumble");
        }

        // 1.3's audit of the powered golden with the ground effect on: 196-300 m up, no rotor's ray finds a surface within its
        // reach, so the effect adds nothing, not a bit, over model 9's layout.
        [Test]
        public void ThePoweredGoldenFindsNoSurface()
        {
            Assert.That(GoldenFlights.FlyPowered(Model11(), out FlightCore core, GroundAt, GroundBytes + ModeBytes, flapping: false, modes: false), Is.EqualTo(0x5AF86BCB6FD0B3A2UL));
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
            Assert.That(RotorAngleAt - FilterBytesAt, Is.EqualTo(1176 - 992), "1.4b's whole block, to model 7's end");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, RotorAngleAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "rotor_angle.rear_right")),
                "1.1's rotor angles follow model 7's 1296 bytes");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, RingAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "ring_inflow.rear_right")),
                "1.2's rings follow model 8's 1328 bytes");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, GroundAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "ground_distance.rear_right")),
                "1.3's surfaces and wash follow model 9's 1400 bytes");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, ModeAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "mode_shape0")),
                "1.4's modes follow model 12's 1496 bytes");
            Assert.That(LogFormat.StepSize - ModeAt, Is.EqualTo(ModeBytes), "to the record's end");
        }
    }
}
