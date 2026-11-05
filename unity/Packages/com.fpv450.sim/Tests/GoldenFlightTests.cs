using System;
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
            // Since model 39 the legs' state block (a flight without contacts leaves it zero) is left out with them.
            ulong with = GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore core, ContactBytesAt, ContactBytes, layout: LegAt);
            ulong without = GoldenFlights.FlyPowered(RepositoryDrone.Spec(), out FlightCore bare, ContactBytesAt, ContactBytes, false, layout: LegAt);
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
        // moves it (on p7a-ground's model 23 it was model 18's, 0x1152D64875230761). Since model 40 without props.blade.
        [Test]
        public void WithoutTheSoilTheContactGoldenIsModel26s()
        {
            Assert.That(GoldenFlights.FlyContact(GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))), out _, soil: false,
                    layout: GoldenFlights.Model28Layout, yawI: false),
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
            Assert.That(speed, Is.LessThan(5e-3), "at rest (1.15 mm/s at 12 s on D-066's model 23, under 1 mm/s on model 22)");
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))); // since model 46 on model 26's legs
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(GoldenFlights.WithRotorDrag(RepositoryDrone.Text(), GoldenFlights.TunedHForce)));
            WindSetup still = WindSetup.Of(WindLevel.Still);
            const int at = GoldenFlights.Model19Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore core, RotorWindBytesAt, WindBytes, wind: still, interference: false, wake: false, motors: false,
                layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model15StillPoweredHash), "powered");
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
            DroneSpec Model37() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel37Legs(DroneSpec.Parse(RepositoryDrone.Text())));
            const int at = GoldenFlights.Model37Layout;
            Assert.That(GoldenFlights.FlyPowered(Model37(), out _, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model37PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model37(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false),
                Is.EqualTo(GoldenFlights.Model37RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model37(), out _, layout: at, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model37ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model37(), out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model37TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model37(), out _, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false),
                Is.EqualTo(GoldenFlights.Model37RangeHash), "range");
            // With them, the flights that touch nothing are model 37's too.
            DroneSpec now = GoldenFlights.WithoutTheBlade(DroneSpec.Parse(RepositoryDrone.Text()));
            Assert.That(GoldenFlights.FlyPowered(now, out _, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model37PoweredHash), "powered, with the law");
            Assert.That(GoldenFlights.FlyPowered(now, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false),
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
            DroneSpec now = GoldenFlights.WithoutTheBlade(DroneSpec.Parse(RepositoryDrone.Text()));
            const int at = GoldenFlights.Model28Layout, skip = GoldenFlights.Model41Skip, end = GoldenFlights.Model39Layout;
            Assert.That(GoldenFlights.FlyPowered(now, out _, at, skip, layout: end, yawI: false), Is.EqualTo(GoldenFlights.Model39PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(now, out _, at, skip, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: end, yawI: false),
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
            DroneSpec Model26() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text())));
            const int at = GoldenFlights.Model37Layout;
            Assert.That(GoldenFlights.FlyPowered(Model26(), out _, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model26PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(Model26(), out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, yawI: false),
                Is.EqualTo(GoldenFlights.Model26RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(Model26(), out _, layout: at, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model26ContactHash), "contact");
            Assert.That(GoldenFlights.FlyTumble(Model26(), out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model26TumbleHash), "tumble");
            Assert.That(GoldenFlights.FlyRange(Model26(), out _, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false),
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
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()));
            const int at = GoldenFlights.Model44Layout;
            uint states = 0;
            void Legs(byte[] record)
            {
                for (int i = at; i < at + 8; i++) states |= record[i]; // each leg's state byte and each corner's
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: at, onRecord: Legs), Is.EqualTo(GoldenFlights.Model44PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Legs),
                Is.EqualTo(GoldenFlights.Model44RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore contact, layout: at, onRecord: Legs), Is.EqualTo(GoldenFlights.Model44ContactHash), "contact, on the soil");
            Assert.That(contact.Contacts.Legs, Is.Null, "no failure law on model 26's legs");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Legs), Is.EqualTo(GoldenFlights.Model44RangeHash), "range, with the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, layout: at, onRecord: Legs, recirculation: false), Is.EqualTo(GoldenFlights.Model44TetherHash),
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
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()));
            const int at = GoldenFlights.Model44Layout;
            uint states = 0;
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, layout: at, onRecord: r =>
            {
                for (int i = at; i < at + 8; i++) states |= r[i];
            }), Is.EqualTo(GoldenFlights.Model47TetherHash), "tether, with the fountain");
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
            DroneSpec spec = RepositoryDrone.Spec();
            const int at = GoldenFlights.Model46Layout;
            int nonZero = 0;
            void Lean(byte[] record)
            {
                for (int i = at; i < LogFormat.StepSize; i++) if (record[i] != 0) nonZero++;
            }
            Action<FlightSetup> off = GoldenFlights.WithoutTheLever;
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Lean, edit: off), Is.EqualTo(GoldenFlights.Model46ContactHash), "contact, on the soil");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Lean, edit: off), Is.EqualTo(GoldenFlights.Model46RangeHash), "range, with the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, layout: at, onRecord: Lean, edit: off), Is.EqualTo(GoldenFlights.Model48TetherHash), "tether, with the fountain");
            Assert.That(nonZero, Is.EqualTo(0), "the lean off the lever");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, layout: at, onRecord: Lean), Is.EqualTo(GoldenFlights.Model46PoweredHash), "powered, on the lever");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Lean),
                Is.EqualTo(GoldenFlights.Model46RainPoweredHash), "rain, on the lever");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model46TumbleHash), "tumble, on the lever");
            Assert.That(nonZero, Is.EqualTo(0), "the lean of a flight that touches nothing");
        }

        // The hanging half and the stub (model 51), its audit, kept: no golden parts a leg, so none has a hanging half or a stub
        // (every corner state zero on every record), and with the fix off (WithoutTheHangingGive: model 50's turn alone and the
        // free tube's spring on a stub) and on every golden is model 50's pin over the whole record, every record and event: no
        // golden was re-recorded. The flights the fix moves are those whose legs part (the crown in Windy, the damage cell's seed 3,
        // the legs' replay): RangeContactTests.WithoutTheHangingGiveTheWindyCrownIsModel50s pins one of them to model 50's Tunnel.
        [Test]
        public void WithoutTheHangingGiveEachGoldenIsModel50s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            int corners = 0;
            int at = -1;
            foreach (LogField f in LogFormat.StepFields)
                if (f.Name == "corner_state.rear_right") at = f.Offset;
            Assert.That(at, Is.GreaterThan(0));
            void Whole(byte[] record)
            {
                for (int i = at; i < at + 4; i++) corners |= record[i];
            }
            // Since model 54 without the skid's frame (WithoutTheFrame): model 51's pins are model 50's.
            Action<FlightSetup> frameless = GoldenFlights.WithoutTheFrame;
            foreach (Action<FlightSetup> edit in new[] { frameless, frameless + GoldenFlights.WithoutTheHangingGive })
            {
                string how = edit == frameless ? "with the fix" : "without it";
                Assert.That(GoldenFlights.FlyContact(spec, out _, onRecord: Whole, edit: edit), Is.EqualTo(GoldenFlights.Model51ContactHash), "contact, " + how);
                Assert.That(GoldenFlights.FlyRange(spec, out _, onRecord: Whole, edit: edit), Is.EqualTo(GoldenFlights.Model51RangeHash), "range, " + how);
                Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, onRecord: Whole, edit: edit), Is.EqualTo(GoldenFlights.Model51TetherHash), "tether, " + how);
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, onRecord: Whole), Is.EqualTo(GoldenFlights.PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Whole),
                Is.EqualTo(GoldenFlights.RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _), Is.EqualTo(GoldenFlights.TumbleHash), "tumble");
            Assert.That(corners, Is.EqualTo(0), "no golden parts a leg: no hanging half, no stub");
        }

        // The skid's frame fore and aft (model 54), its audit, kept: without the frame (WithoutTheFrame: one value both ways, every
        // leg the cantilever along the skid as across it) the contact, range and tether goldens are model 51's pins, every record
        // and event, and with it the flights that touch nothing (powered, rain, tumble) are their pins unchanged; no golden parts,
        // cracks or sets a leg (every leg state zero on every record). So the frame is the whole of each move.
        [Test]
        public void WithoutTheFrameEachGoldenIsModel51s()
        {
            DroneSpec spec = RepositoryDrone.Spec();
            int states = 0, at = -1;
            foreach (LogField f in LogFormat.StepFields)
                if (f.Name == "leg_state.rear_right") at = f.Offset;
            Assert.That(at, Is.GreaterThan(0));
            void Intact(byte[] record)
            {
                for (int i = at; i < at + 4; i++) states |= record[i];
            }
            Action<FlightSetup> off = GoldenFlights.WithoutTheFrame;
            Assert.That(GoldenFlights.FlyContact(spec, out _, onRecord: Intact, edit: off), Is.EqualTo(GoldenFlights.Model51ContactHash), "contact, without the frame");
            Assert.That(GoldenFlights.FlyRange(spec, out _, onRecord: Intact, edit: off), Is.EqualTo(GoldenFlights.Model51RangeHash), "range, without the frame");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, onRecord: Intact, edit: off), Is.EqualTo(GoldenFlights.Model51TetherHash), "tether, without the frame");
            Assert.That(GoldenFlights.FlyContact(spec, out _, onRecord: Intact), Is.EqualTo(GoldenFlights.ContactHash), "contact, with it");
            Assert.That(GoldenFlights.FlyRange(spec, out _, onRecord: Intact), Is.EqualTo(GoldenFlights.RangeHash), "range, with it");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, onRecord: Intact), Is.EqualTo(GoldenFlights.TetherHash), "tether, with it");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, onRecord: Intact), Is.EqualTo(GoldenFlights.PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Intact),
                Is.EqualTo(GoldenFlights.RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _), Is.EqualTo(GoldenFlights.TumbleHash), "tumble");
            Assert.That(states, Is.EqualTo(0), "every leg intact in every golden");
        }

        // Its audit against p7g-legs (model 41), kept: without the line's mechanisms (no surfaces.json, no area laws, no line, no
        // props.blade, model 34's yaw I; no golden enters a cut), over p7g-legs' record (the record less the fiber's 104 and the
        // strikes' 384 bytes at 1904), every golden is model 41's pin, every record and event, and the two blocks are zero on
        // every record: the line's code changes no flight without its mechanisms, and they are the whole of each move against
        // p7g-legs.
        [Test]
        public void WithoutTheLineEachGoldenIsModel41s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(DroneSpec.Parse(RepositoryDrone.Text()));
            const int at = GoldenFlights.Model28Layout, skip = GoldenFlights.Model41Skip;
            int nonZero = 0;
            void Line(byte[] record)
            {
                for (int i = at; i < at + skip; i++) if (record[i] != 0) nonZero++;
            }
            const int end = GoldenFlights.Model46Layout; // since model 49 the record before the legs' lean, the lever off (1.3's softening)
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, onRecord: Line, yawI: false), Is.EqualTo(GoldenFlights.Model41PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Line, yawI: false),
                Is.EqualTo(GoldenFlights.Model41RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, at, skip, layout: end, onRecord: Line, soil: false, yawI: false, edit: GoldenFlights.WithoutTheLever),
                Is.EqualTo(GoldenFlights.Model41ContactHash), "contact, on the rigid floor");
            Assert.That(GoldenFlights.FlyRange(spec, out _, at, skip, layout: end, onRecord: Line, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false,
                edit: GoldenFlights.WithoutTheLever), Is.EqualTo(GoldenFlights.Model41RangeHash), "range, without the area laws");
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
            DroneSpec Model22() => GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text())));
            const int at = GoldenFlights.Model28Layout; // since model 31 the record's end is the fiber's block, since model 40 then the strikes
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))); // the branch flew them
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
        const int FiberAt = WakeAt + WakeBytes, FiberBytes = 104, StrikesAt = FiberAt + FiberBytes, StrikesBytes = 384;
        const int LegAt = StrikesAt + StrikesBytes, LegBytes = 168, DamageAt = LegAt + LegBytes, DamageBytes = 224, LeanAt = DamageAt + DamageBytes, LeanBytes = 32;

        // The audits of p7b-airframe 1.3d and D-064, kept on the integration (model 24): without the interference, the rotor
        // wakes and the motors' resistance, friction and inertia spreads, over model 19's 1808 bytes, the powered, rain and
        // tumble goldens are model 19's, every record and event (the powered and rain flights find no surface, so the
        // re-ingested wash's new numbers never act there; the contact flight's wash has no switch, GoldenFlights' notes).
        [Test]
        public void WithoutEitherTheGoldenFlightsAreModel19s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text())));
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel22Drone(DroneSpec.Parse(RepositoryDrone.Text())));
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model28Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < GoldenFlights.Model38Layout; i++) if (record[i] != 0) nonZero++;
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model28Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < GoldenFlights.Model38Layout; i++) if (record[i] != 0) nonZero++;
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model38Layout; // since model 40 the record's end is the blade strikes
            Assert.That(GoldenFlights.FlyContact(spec, out _, soil: false, yawI: false, layout: at), Is.EqualTo(GoldenFlights.Model31ContactHash), "contact, on the rigid floor");
            Assert.That(GoldenFlights.FlyRange(spec, out _, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false, layout: at), Is.EqualTo(GoldenFlights.Model31RangeHash),
                "range, without the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, soil: false, tether: true, yawI: false, layout: at, recirculation: false), Is.EqualTo(GoldenFlights.Model31TetherHash), "tether, on the rigid floor");
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
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model38Layout; // since model 40 the record's end is the blade strikes
            Assert.That(GoldenFlights.FlyPowered(spec, out _, yawI: false, layout: at), Is.EqualTo(GoldenFlights.Model34PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, yawI: false, layout: at),
                Is.EqualTo(GoldenFlights.Model34RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, yawI: false, layout: at), Is.EqualTo(GoldenFlights.Model34ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, yawI: false, layout: at), Is.EqualTo(GoldenFlights.Model34RangeOn38Hash), "range, with model 38's buried face");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, yawI: false, layout: at, recirculation: false), Is.EqualTo(GoldenFlights.Model34TetherHash), "tether");
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
            DroneSpec spec = RepositoryDrone.Spec();
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore line, tether: true, recirculation: false, edit: GoldenFlights.WithoutTheFrame),
                Is.EqualTo(GoldenFlights.Model49TetherHash), "tether, on design 4's air (since model 54 without the skid's frame)");
            Assert.That(line.Strikes.Passes, Is.EqualTo(0), "no blade passed the soil or a shape");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, layout: GoldenFlights.Model46Layout, recirculation: false, edit: GoldenFlights.WithoutTheLever),
                Is.EqualTo(GoldenFlights.Model46TetherHash), "tether, on design 4's air and the lever off");
            System.Action<FlightSetup> off = s => s.Contacts.TipSlide = false;
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore contact, edit: off), Is.EqualTo(GoldenFlights.ContactHash), "contact, the tip slide off");
            Assert.That(contact.Strikes.Passes, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyRange(spec, out FlightCore range, edit: off), Is.EqualTo(GoldenFlights.RangeHash), "range, the tip slide off");
            Assert.That(range.Strikes.Passes, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore tether, tether: true, edit: off), Is.EqualTo(GoldenFlights.TetherHash), "tether, the tip slide off");
            Assert.That(tether.Strikes.Passes, Is.EqualTo(0));
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

        // p7f-damage 1.2's audit, kept (prop-motor-damage's "The intact blade is the published table"): with the blades' factory
        // pitch off and the flapping's inertia at model 26's uniform rod, the rotors carry their blades as elements but stay
        // sound, skip their path and fly the published table: every golden is model 26's, every record and event; so is every
        // golden on a drone.json without props.blade (no elements, no pitch draws, the rod). Since model 40 (the physics
        // integration 2) each flies without the line's mechanisms (no soil, no area laws, model 34's yaw I; no line, no cut).
        [Test]
        public void WithThePitchOffAndTheRodsInertiaEachGoldenIsModel26s()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text())); // since model 46
            System.Action<FlightSetup> model26 = s =>
            {
                GoldenFlights.WithoutThePitch(s);
                GoldenFlights.WithTheRodsInertia(spec)(s);
            };
            const int at = GoldenFlights.Model29Layout;
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore core, edit: model26, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model26PoweredHash), "powered");
            Assert.That(core.Propulsion.Rotor(0), Is.Not.Null, "the blades as elements, sound");
            Assert.That(core.Propulsion.Damaged, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, edit: model26, layout: at, yawI: false),
                Is.EqualTo(GoldenFlights.Model26RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, edit: model26, layout: at, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model26ContactHash), "contact");
            Assert.That(GoldenFlights.FlyRange(spec, out _, edit: model26, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false),
                Is.EqualTo(GoldenFlights.Model26RangeHash), "range");
            DroneSpec bladeless = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text())));
            Assert.That(GoldenFlights.FlyPowered(bladeless, out _, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model26PoweredHash), "powered without props.blade");
            Assert.That(GoldenFlights.FlyContact(bladeless, out _, layout: at, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model26ContactHash), "contact without props.blade");
        }

        // p7f-damage 1.3's audit, kept: the goldens' discs cut nothing (no blade strike, no BladeStrike event), so over model 29's
        // 1904 bytes every golden is model 29's pin, every record and event, and the strikes' 384 bytes are zero throughout; the
        // strikes' switch (ContactSpec.BladeStrikes) changes none of them. Since model 40 each flies without the line's
        // mechanisms (no soil, no area laws, model 34's yaw I; no line, no cut), and the strikes' block follows the fiber's.
        [Test]
        public void OverModel29sLayoutEachGoldenIsModel29s()
        {
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text())), now = RepositoryDrone.Spec(); // since model 46
            const int at = GoldenFlights.Model29Layout;
            bool zero = true;
            System.Action<byte[]> strikes = record =>
            {
                for (int i = GoldenFlights.Model38Layout; i < GoldenFlights.Model44Layout; i++) zero &= record[i] == 0;
            };
            System.Action<FlightSetup> off = s => { if (s.Contacts != null) s.Contacts.BladeStrikes = false; };
            Assert.That(GoldenFlights.FlyPowered(spec, out _, onRecord: strikes, layout: at, yawI: false), Is.EqualTo(GoldenFlights.Model29PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: strikes, layout: at, yawI: false),
                Is.EqualTo(GoldenFlights.Model29RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out FlightCore contact, onRecord: strikes, layout: at, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model29ContactHash), "contact");
            Assert.That(contact.Strikes, Is.Not.Null, "the strikes on");
            Assert.That(contact.Strikes.Passes, Is.EqualTo(0), "no blade passed a shape");
            Assert.That(GoldenFlights.FlyRange(spec, out FlightCore range, onRecord: strikes, layout: at, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false),
                Is.EqualTo(GoldenFlights.Model29RangeHash), "range");
            Assert.That(range.Strikes.Passes, Is.EqualTo(0));
            Assert.That(GoldenFlights.FlyTumble(spec, out _, out _, layout: at), Is.EqualTo(GoldenFlights.Model29TumbleHash), "tumble");
            Assert.That(zero, Is.True, "the strikes' bytes are zero");
            Assert.That(GoldenFlights.FlyContact(now, out _, edit: off), Is.EqualTo(GoldenFlights.ContactHash), "contact, the strikes off");
            Assert.That(GoldenFlights.FlyRange(now, out _, edit: off), Is.EqualTo(GoldenFlights.RangeHash), "range, the strikes off");
            Assert.That(GoldenFlights.FlyContact(now, out _, tether: true, edit: off), Is.EqualTo(GoldenFlights.TetherHash), "tether, the strikes off");
        }

        // The physics integration 2 with p7f-damage (model 40), its audit against the line (model 38), kept: on a drone.json
        // without props.blade (no blades as elements, no pitch draws, the flapping's rod, and so no strikes), over the line's
        // 2008 bytes, every golden is model 38's pin, every record and event, and the strikes' block is zero on every record:
        // p7f-damage's code (the rotors' path, the disc's slab pairs, the retired prop crash) changes no flight without the
        // blade's table, and props.blade is the whole of each move against the line.
        [Test]
        public void WithoutTheBladeEachGoldenIsModel38s()
        {
            DroneSpec spec = GoldenFlights.WithoutTheBlade(GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text()))); // since model 46 on model 26's legs
            const int at = GoldenFlights.Model38Layout;
            int nonZero = 0;
            void Tail(byte[] record)
            {
                for (int i = at; i < GoldenFlights.Model44Layout; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out FlightCore p, layout: at, onRecord: Tail), Is.EqualTo(GoldenFlights.Model38PoweredHash), "powered");
            Assert.That(p.Strikes, Is.Null, "no strikes without the blade's table");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, layout: at, onRecord: Tail),
                Is.EqualTo(GoldenFlights.Model38RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, layout: at, onRecord: Tail), Is.EqualTo(GoldenFlights.Model38ContactHash), "contact, on the soil");
            Assert.That(GoldenFlights.FlyRange(spec, out _, layout: at, onRecord: Tail), Is.EqualTo(GoldenFlights.Model38RangeHash), "range, with the area laws");
            Assert.That(GoldenFlights.FlyContact(spec, out _, tether: true, layout: at, onRecord: Tail, recirculation: false), Is.EqualTo(GoldenFlights.Model38TetherHash), "tether");
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
            DroneSpec spec = GoldenFlights.WithModel26Legs(DroneSpec.Parse(RepositoryDrone.Text())); // since model 46
            const int at = GoldenFlights.Model28Layout, skip = GoldenFlights.Model35Skip, end = GoldenFlights.Model44Layout;
            int nonZero = 0;
            void Fiber(byte[] record)
            {
                for (int i = at; i < at + skip; i++) if (record[i] != 0) nonZero++;
            }
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, onRecord: Fiber, yawI: false), Is.EqualTo(GoldenFlights.Model35PoweredHash), "powered");
            Assert.That(GoldenFlights.FlyPowered(spec, out _, at, skip, layout: end, wind: WindSetup.Of(WindLevel.Calm), rain: RainLevel.SevereRain, onRecord: Fiber, yawI: false),
                Is.EqualTo(GoldenFlights.Model35RainPoweredHash), "rain");
            Assert.That(GoldenFlights.FlyContact(spec, out _, at, skip, layout: end, onRecord: Fiber, soil: false, yawI: false), Is.EqualTo(GoldenFlights.Model35ContactHash),
                "contact, on the rigid floor");
            Assert.That(GoldenFlights.FlyRange(spec, out _, at, skip, layout: end, onRecord: Fiber, materials: GoldenFlights.WithoutTheAreaLaws(), yawI: false),
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
            Assert.That(LogFormat.StepSize - LeanAt, Is.EqualTo(LeanBytes), "to the record's end");
        }
    }
}
