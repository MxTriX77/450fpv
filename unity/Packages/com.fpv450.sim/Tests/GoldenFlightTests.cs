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

        // p7a-ground 1.10's audit (model 23), restated at the ground merge (model 27), kept: without surfaces.json the contact
        // golden is the physics line's model 26 flight on the rigid floor, every record and event bit for bit; only the soil
        // moves it (on p7a-ground's model 23 it was model 18's, 0x1152D64875230761).
        [Test]
        public void WithoutTheSoilTheContactGoldenIsModel26s()
        {
            Assert.That(GoldenFlights.FlyContact(RepositoryDrone.Spec(), out _, soil: false, layout: GoldenFlights.Model28Layout, yawI: false),
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
            Assert.That(speed, Is.LessThan(2e-3), "at rest (1.15 mm/s at 12 s on D-066's model 23, under 1 mm/s on model 22)");
            Assert.That(GoldenFlights.FlyRange(spec, out _), Is.EqualTo(hash), "a second flight gives the same bits");
            Assert.That(hash, Is.EqualTo(GoldenFlights.RangeHash), "the pinned golden (a re-record is audited and raises the model version)");
        }

        // p7a-ground 1.12's audit (model 33), kept: without the layers' area laws the range golden is model 32's flight, every
        // record and event, dry and in Rain with surfaces.json (1.13's flight that ended rolled 17.6° on wet straw): neither
        // reaches the stroke, and the seam's facing test changes nothing there, so the area law is the whole move.
        [Test]
        public void WithoutTheAreaLawsTheRangeGoldenIsModel32s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            const int at = GoldenFlights.Model28Layout; // since model 34 the record's end is the line's block
            Assert.That(GoldenFlights.FlyRange(spec, out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model32RangeHash), "dry");
            Assert.That(GoldenFlights.FlyRange(spec, out _, rain: RainLevel.Rain, table: SoilScenarios.Table(), materials: GoldenFlights.WithoutTheAreaLaws(), layout: at, yawI: false),
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
        // a kept test until model 22) is a temporary edit since model 24 (GoldenFlights.RangeHash's notes).
        [Test]
        public void InStillAirTheGoldenFlightsAreModel15s()
        {
            DroneSpec spec = GoldenFlights.WithModel22Drone(GoldenFlights.WithRotorDrag(RepositoryDrone.Text(), GoldenFlights.TunedHForce));
            WindSetup still = WindSetup.Of(WindLevel.Still);
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore core, RotorWindBytesAt, WindBytes, wind: still, interference: false, wake: false, motors: false,
                layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model15StillPoweredHash), "powered");
            Assert.That(core.Contacts.ImpactCount, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, RotorWindBytesAt, WindBytes, at), Is.EqualTo(GoldenFlights.Model15TumbleHash), "tumble");
        }

        // D-066 (model 23 on p7b-airframe--power), its audit, kept: on model 22's drone (the 40 A limit, the 1.0 A no-load
        // current, the 0.92 kg packs and the 0.25 kg electronics put back) every golden is model 22's there and, since the
        // merge at model 26, the landing's merge's model 25 here, every record and event (the goldens fly the exact build, no
        // placement). Each older kept audit flies model 22's drone first.
        [Test]
        public void OnModel22sDroneEachGoldenIsModel25s()
        {
            DroneSpec Model22() => GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text()));
            const int at = GoldenFlights.Model28Layout; // since model 31 the record's end is the fiber's block
            Assert.That(GoldenFlights.FlyPowered(Model22(), out _, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model25PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model22(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false),
                Is.EqualTo(GoldenFlights.Model25RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model22(), out _, soil: false, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model25ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model22(), out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model25TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model22(), out _, materials: GoldenFlights.WithoutTheAreaLaws(), layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model25RangeHash),
                "range, without 1.12's area laws (model 33)");
        }

        // The merge's audit against p7b-airframe--power (model 24 there), kept: on drone.json without the interference and
        // D-064's sources, over model 19's 1808 bytes, the powered, rain and tumble goldens are that branch's pins, every record
        // and event (they touch nothing; the contact and range flights need D-064's wash and 1.6c's spring off too, which have
        // no switch: GoldenFlights' notes).
        [Test]
        public void WithoutTheIntegrationTheGoldenFlightsAreThePowerBranchs()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out _, interference: false, wake: false, motors: false, layout: at, yawI: false),
                Is.EqualTo(GoldenFlights.PowerPoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: at, yawI: false), Is.EqualTo(GoldenFlights.PowerRainHash), "rain");
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
            Assert.That(GoldenFlights.FlyPowered(spec, out _, interference: false, wake: false, motors: false, layout: at, yawI: false),
                Is.EqualTo(GoldenFlights.Model19PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, interference: false, wake: false,
                motors: false, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model19RainHash), "rain");
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
            const int at = GoldenFlights.Model28Layout; // since model 31 the record's end is the fiber's block
            Assert.That(GoldenFlights.FlyPowered(spec, out _, InterferenceAt, InterferenceBytes, interference: false, layout: at, yawI: false), Is.EqualTo(GoldenFlights.MicroPoweredHash), "powered");
            Assert.That(GoldenFlights.FlyContact(spec, out _, InterferenceAt, InterferenceBytes, interference: false, soil: false, layout: at, yawI: false), Is.EqualTo(GoldenFlights.MicroContactHash), "contact");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, InterferenceAt, InterferenceBytes, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain,
                interference: false, layout: at, yawI: false), Is.EqualTo(GoldenFlights.MicroRainHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, InterferenceAt, InterferenceBytes, at), Is.EqualTo(GoldenFlights.MicroTumbleHash), "tumble");
        }

        // p7d-tether 1.6's audit (model 31): every golden flown without the line is model 28's over its 1904-byte record, every
        // record and event, and the fiber's block after it is zero on every record: the line's code (the modes' rebuild, the
        // chain's spool, the flight's line) changes no flight without it. Restated at the physics integration 2 (model 34): the
        // contact and range goldens fly p7a-ground's soil and area laws since then, so here they fly without them.
        [Test]
        public void OverModel28sRecordEachGoldenIsModel28s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            const int at = GoldenFlights.Model28Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < LogFormat.StepSize; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: at, onRecord: Tail, yawI: false), Is.EqualTo(GoldenFlights.Model28PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Tail, yawI: false),
                Is.EqualTo(GoldenFlights.Model28RainHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Tail, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model28ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model28TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Tail, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false),
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
            DroneSpec spec = RepositoryDrone.Spec();
            const int at = GoldenFlights.Model28Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < LogFormat.StepSize; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Tail, yawI: false), Is.EqualTo(GoldenFlights.Model33ContactHash), "contact, on the soil");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Tail, yawI: false), Is.EqualTo(GoldenFlights.Model33RangeOn38Hash),
                "range, with the area laws and model 38's buried face");
            Assert.That(nonZero, Is.EqualTo(0), "the fiber's block of a flight without the line");
        }

        // The physics integration 2's audit against p7d-tether (model 34 against its 31), kept: without surfaces.json and the
        // layers' area laws, over the whole 2008-byte record, the contact, range and tether goldens are p7d-tether's model 31
        // pins, every record and event: the soil and the area laws are the whole of each move (the cavity: no cut entered).
        [Test]
        public void WithoutTheGroundTheGoldenFlightsAreModel31s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Assert.That(GoldenFlights.FlyContact(spec, out _, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model31ContactHash), "contact, on the rigid floor");
            Assert.That(GoldenFlights.FlyRange(spec, out _, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false), Is.EqualTo(GoldenFlights.Model31RangeHash),
                "range, without the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, soil: false, tether: true, yawI: false), Is.EqualTo(GoldenFlights.Model31TetherHash), "tether, on the rigid floor");
        }

        // The physics integration 2's audit against p7c-wind's cavity (model 34 against its 30), kept: the range golden flies
        // WindSetup.ForMap's Calm, which carries the test range's cuts (E01's run and niches, Z04's shaft); with them dropped (the
        // cavity off) it is the same bits, since it never enters a cut's opening. Every other golden flies the stub's floor,
        // which has no cut.
        [Test]
        public void WithoutTheCutsTheRangeGoldenIsTheSame()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            WindSetup air = WindSetup.ForMap(RangeScenarios.World, WindLevel.Calm);
            Assert.That(air.Cuts, Is.Not.Null.And.Not.Empty, "the map's wind carries its cuts");
            air.Cuts = null;
            Assert.That(GoldenFlights.FlyRange(spec, out _, wind: air), Is.EqualTo(GoldenFlights.RangeHash));
        }

        // Betaflight's yaw I's audit (model 36 against 34), kept: on model 34's yaw I (GoldenFlights.Model34YawI, i_yaw just
        // under 12, whose yaw Ki is model 34's ITERM_SCALE × 30 bit for bit) every golden is model 34's pin, every record and
        // event: pid_init.c's × 2.5 on yaw's Ki is the whole move. Each kept audit above against an earlier model flies it too;
        // the tumble has no rotors and keeps its hash (TheTumbleGoldenIsPinned). Since model 38 the range flight meets the
        // buried face too, as GoldenFlights.Model34RangeOn38Hash: model 34's pin with it off.
        [Test]
        public void OnModel34sYawIEachGoldenIsModel34s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            Assert.That(GoldenFlights.FlyPowered(spec, out _, yawI: false), Is.EqualTo(GoldenFlights.Model34PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, yawI: false),
                Is.EqualTo(GoldenFlights.Model34RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, yawI: false), Is.EqualTo(GoldenFlights.Model34ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, yawI: false), Is.EqualTo(GoldenFlights.Model34RangeOn38Hash), "range, with model 38's buried face");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, yawI: false), Is.EqualTo(GoldenFlights.Model34TetherHash), "tether");
        }

        // p7d-tether 1.6: the contact flight with the line, pinned (GoldenFlights.TetherHash), the same twice, the line paying out
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
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true), Is.EqualTo(hash), "a second flight gives the same bits");
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
            Assert.That(LogFormat.StepSize - GoldenFlights.Model28Layout, Is.EqualTo(104), "to the record's end");
        }
    }
}
