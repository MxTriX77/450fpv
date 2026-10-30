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

        // p7c-wind 1.4's audit: the field changes nothing but the wind. In still air both golden flights over model 5's
        // 1296-byte layout (the record less the hubs' winds) are model 5's, bit for bit; the powered one touches nothing.
        // On model 6's drone: 1.6's re-derived rotor drag moves every flight through the air, still air too.
        [Test]
        public void InStillAirTheGoldenFlightsAreModel5s()
        {
            DroneSpec spec = GoldenFlights.Model6Drone(RepositoryDrone.Text());
            WindSetup still = WindSetup.Of(WindLevel.Still);
            int skip = LogFormat.StepSize - GoldenFlights.Model5Bytes;
            ulong powered = GoldenFlights.FlyPowered(spec, out FlightCore core, GoldenFlights.Model5Bytes, skip, still);
            Assert.That(core.Contacts, Is.Not.Null, "the flight carries its contacts");
            Assert.That(core.Contacts.ImpactCount, Is.EqualTo(0));
            Assert.That(powered, Is.EqualTo(GoldenFlights.StillAirPoweredHash), "model 5's records and events, every bit");
            ulong contact = GoldenFlights.FlyContact(spec, out _, GoldenFlights.Model5Bytes, skip, still);
            Assert.That(contact, Is.EqualTo(GoldenFlights.StillAirContactHash), "model 5's records and events, every bit");
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
        // other's block. Then p7c-wind 1.4's rotor winds (model 6), to the record's end: model 5's layout is the record's
        // first 1296 bytes.
        const int ContactBytesAt = 992, ContactBytes = 120, FilterBytesAt = ContactBytesAt + ContactBytes, RotorWindBytesAt = 1296;

        [Test]
        public void TheRecordKeepsEachParentsBytesInPlace()
        {
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, ContactBytesAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "contact_force.x")),
                "1.6 appends its fields after model 3's 992 bytes");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, FilterBytesAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "fc.gyro_filtered.roll")),
                "1.4b's filter block follows the contacts' 120 bytes");
            Assert.That(RotorWindBytesAt - FilterBytesAt, Is.EqualTo(1176 - 992), "1.4b's whole block");
            Assert.That(LogFormat.FieldAt(LogFormat.StepFields, RotorWindBytesAt), Is.EqualTo(System.Array.FindIndex(LogFormat.StepFields, f => f.Name == "rotor_wind.rear_right.x")),
                "p7c-wind 1.4's rotor winds follow it");
            Assert.That(LogFormat.StepSize - RotorWindBytesAt, Is.EqualTo(96), "four vectors, to the record's end");
        }
    }
}
