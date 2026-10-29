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

        // Model 3's 992 bytes, then 1.6's contacts, then 1.4b's filter block: each parent's layout is the record less the
        // other's block. Then p7b-airframe 1.1's rotor angles: model 7's layout is the record less them.
        const int ContactBytesAt = 992, ContactBytes = 120, FilterBytesAt = ContactBytesAt + ContactBytes;
        const int RotorAngleAt = 1296, RotorAngleBytes = 32;

        // p7b-airframe 1.1's audit, kept: with the motors' asymmetry off, over model 7's 1296-byte layout with its events,
        // each golden is model 7's bit for bit (the rotors' angles turn, but nothing reads them without an unbalance).
        [Test]
        public void WithTheAsymmetryOffEachGoldenIsModel7s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Assert.That(GoldenFlights.FlyPowered(spec, out _, RotorAngleAt, RotorAngleBytes, asymmetry: false), Is.EqualTo(0xA65FBFBCBCFC5B96UL), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, RotorAngleAt, RotorAngleBytes, false), Is.EqualTo(0x3AF33645FF8F1919UL), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, RotorAngleAt, RotorAngleBytes), Is.EqualTo(0xE3178EE7FF322698UL), "tumble");
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
            Assert.That(LogFormat.StepSize - RotorAngleAt, Is.EqualTo(RotorAngleBytes), "to the record's end");
        }
    }
}
