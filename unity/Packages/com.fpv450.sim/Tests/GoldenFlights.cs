using System;
using Fpv450.Sim.Contact;
using Fpv450.Sim.Control;
using Fpv450.Sim.Flight;
using Fpv450.Sim.Log;
using Fpv450.Sim.Wind;

namespace Fpv450.Sim.Tests
{
    /// <summary>
    /// The golden flights. The powered one (p6-flight-core 1.4, 1.5, 1.4b): the repository's drone at its nominal loadout
    /// with its drag and its contacts, in the Windy wind (the field, p7c-wind 1.4), armed by Betaflight's rules and flown 10 s by a scripted
    /// pilot through every part of the controller with the builder's filter chain, propulsion and the air (a spin-up, a
    /// climb, roll, pitch and yaw steps, a throttle punch into the ESCs' current limit and a chop, a weave), 300 m up, so
    /// it touches nothing. The contact one (1.6): the drone takes off from the floor under the closed-loop pilot, comes
    /// down on the slow glide path with the sticks held, bounces, and the throttle is cut. Each hash is every step's log
    /// record and events, so it pins the whole state, the controller's included, bit for bit. The suite checks them on
    /// .NET and in Unity's EditMode (Mono), tools/flightbench --golden on .NET Release. A re-record raises
    /// LogFormat.ModelVersion and is audited case by case (CLAUDE.md).
    /// </summary>
    public static class GoldenFlights
    {
        public const int PoweredSteps = 10000, ContactSteps = 12000;

        // 1.4: first recorded with LogFormat.ModelVersion 2: 0xED68941C901FBA75.
        // 1.5: re-recorded with ModelVersion 3 (the 992-byte record), audited case by case against model 2's flight on
        // the records' shared 912 bytes: with the air off (no drag, still air, no ESC limit) and the gyroscopic term
        // taken out, every record of all 10 000 steps is bit-identical; then each new term moves the flight from its
        // first step of effect: the gyroscopic moment from step 1133 (0.17 m by 10 s), the rotors' in-plane drag from
        // step 22 (model 2's drone fell 315 m with no drag at all; it now ends 196 m up), the 40 A limit from step
        // 2006 (the roll step's kick), the body's and the coil's drag from step 2, the wind from step 1. Pinned again
        // within model 3 when drone.json's h_force_n_per_m_s was tuned from 0.31 to 0.225 (clip P's cruise pitch);
        // the drone's hash refuses the earlier logs.
        // 1.6, on its own branch: re-recorded with ModelVersion 4 (the 1112-byte record), the contacts in its setup, as
        // 0x798AFECB92E12DB7. Audited: hashed over the records' first 992 bytes (model 3's layout) with its events, the
        // flight was model 3's 0x734CE3306086F5D7 bit for bit: at 196–300 m no shape touches, so the contacts add nothing,
        // not even a signed zero; the 120 appended bytes are zero but for the touching count, which is 0 too.
        // 1.4b, on its own branch: re-recorded with ModelVersion 4 (the 1176-byte record: model 3's 992 bytes, then the
        // filter chain's and the battery's) as 0x3695FC49DCA906DC, audited case by case on the shared 992 bytes: with every
        // filter off and the PID reading the gyro at the loop rather than its two 8 kHz samples' mean (a temporary edit),
        // every record and event of all 10 000 steps is bit-identical to model 3's 0x734CE3306086F5D7. Then each part of
        // the chain moves the motion from its first armed step, 21 (the controller's own state from step 2, D min's
        // filters running disarmed): the 8 kHz mean (0.008 m from the gyro-at-the-loop flight by 10 s), gyro LPF2
        // (0.18 m), LPF1 (2.1 m), the gyro notch (4.3 m), the D term's filters (3.7 m, the builder's whole chain, this
        // golden); the RPM filter from step 164, when the motors pass rpm_filter_min_hz (4.2 m); the dynamic notch
        // (3.5 m). The golden flies the default, sag compensation off; with it at 100 the flight moves from step 21 and
        // ends 38.6 m lower (the motors' top cut by up to 25 % on the 90 % pack).
        // 1.6 and 1.4b merged: re-recorded with ModelVersion 5 (the 1296-byte record: model 3's 992 bytes, 1.6's 120
        // contact bytes, 1.4b's 184 filter bytes), audited against each parent with the other's terms off:
        // - against 1.4b: hashed over 1.4b's layout (the record less the contacts' 120 bytes) with its events, the flight is
        //   1.4b's 0x3695FC49DCA906DC bit for bit (PoweredFiltersHash, GoldenFlightTests), the contacts adding nothing 300 m
        //   up, as in 1.6; the contacts' bytes are zero throughout (the touching count 0).
        // - against 1.6: with every filter off and the PID reading the gyro at the loop (1.4b's temporary edit, made again
        //   and reverted), hashed over 1.6's 1112-byte layout with its events, the flight is 1.6's 0x798AFECB92E12DB7 bit
        //   for bit. With the chain on, it moves as 1.4b's did, from step 21.
        // p7c-wind 1.4: re-recorded with ModelVersion 6 (the 1392-byte record: model 5's 1296 bytes, the stand-in's gust
        // slot now the wind at the body's origin, then the four hubs' winds) in the Windy field, audited case by case
        // against model 5 (35c6432) by flying both models in still air and in a steady 5 m/s from 270° and hashing model
        // 5's 1296 bytes (for steady air less the 24-byte wind slot, which model 5 left 0): every record and event of
        // all 10 000 steps is bit-identical (StillAirPoweredHash below; steady 0x00D35F9AE93901C0 on both). In the Windy
        // field the flight moves from step 1, the wind's first: the field's draws are new (the direction, the strength,
        // the meander, the shifts, the modes), so the stand-in's air is gone; it ends 203.1 m up against 199.4 and
        // 16.6 m from model 5's end.
        public const ulong PoweredHash = 0xBC85ED28D55D2986UL;

        // The powered golden in still air over model 5's 1296-byte layout: model 5's flight, bit for bit (the audit).
        // Model 5 in turn held 1.4b's 0x3695FC49DCA906DC over 1.4b's layout in the stand-in's Windy air; that flight
        // cannot be flown on model 6, whose Windy air is the field's.
        public const ulong StillAirPoweredHash = 0x47789216C52FBF65UL;

        // 1.6: first recorded with ModelVersion 4 as 0x8B568804552379E6.
        // 1.6 and 1.4b merged: re-recorded with ModelVersion 5. Audited against 1.6: with every filter off and the PID
        // reading the gyro at the loop (the temporary edit), hashed over 1.6's 1112 bytes with its events, the flight is
        // 1.6's 0x8B568804552379E6 bit for bit. With the builder's chain the controller's state moves from step 2 (D min's
        // filters, disarmed) and the motion from the first armed step, 9; the flight keeps its course: the sticks held at
        // the same step (7219) and the skids down at the same step (7412), off the ground 1 ms later, 140.1 mm up in the
        // held bounce against 142.3, the last hop after the cut ending 8 ms later (step 9313 against 9305), 5 impacts,
        // the same height at rest. It has no audit against 1.4b, whose core has no contacts: the flight starts on the floor.
        // p7c-wind 1.4: re-recorded with ModelVersion 6 in the Calm field. Audited as the powered one: in still air
        // (StillAirContactHash) and in the steady 5 m/s (0x7F53C9713DCE5641) both models fly every record and event of
        // the 12 000 steps bit for bit over model 5's bytes. In Calm the flight moves from its first step (the field's
        // draws are new) and keeps its course: 5 impacts as model 5's, at rest 0.2519 m up on its 8 touching pairs.
        public const ulong ContactHash = 0x3D6092D3121E468AUL;

        /// <summary>The contact golden in still air over model 5's 1296-byte layout: model 5's flight, bit for bit.</summary>
        public const ulong StillAirContactHash = 0x7C3C81C9E2B04309UL;

        /// <summary>Model 5's record length: the record less p7c-wind 1.4's four hub winds.</summary>
        public const int Model5Bytes = 1296;

        public static FlightSetup PoweredSetup(DroneSpec spec)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong hash);
            return new FlightSetup
            {
                Seed = 0x450F,
                World = FlightWorld.Floor(),
                Mass = spec.BuildMass(spec.Nominal),
                Propulsion = spec.BuildPropulsion(),
                Aero = spec.BuildAero(),
                Wind = WindSetup.Of(WindLevel.Windy),
                Contacts = spec.BuildContacts(spec.Nominal, materials, hash),
                StartPosition = new Double3(0, 300, 0),
                StartPackCharge = 0.9,
            };
        }

        /// <summary>The scripted pilot's sticks at step k.</summary>
        public static StickSample PoweredStick(int k)
        {
            double t = k * FlightCore.Dt;
            var s = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 20 };
            if (k < 1000) s.Throttle = 0.47 * DetMath.Max(0, t - 0.1) / 0.9;
            else s.Throttle = 0.47;
            if (k >= 2000 && k < 2500) s.Roll = 0.9575;
            if (k >= 2500 && k < 3000) s.Roll = -0.5;
            if (k >= 3000 && k < 3500) s.Pitch = 0.6;
            if (k >= 3500 && k < 4500) s.Yaw = 1;
            if (k >= 4500 && k < 5500) s.Throttle = 1;
            if (k >= 5500 && k < 6000) s.Throttle = 0;
            if (k >= 6000)
            {
                double phase = (k - 6000) % 1000 / 1000.0;
                s.Throttle = 0.5;
                s.Roll = phase < 0.5 ? 0.4 : -0.4;
                s.Pitch = phase < 0.25 || phase >= 0.75 ? -0.2 : 0.2;
            }
            return s;
        }

        /// <summary>Flies the powered golden flight; returns its hash, with the core's last state for a report. With
        /// skipAt and skipBytes the hash leaves out each record's bytes from skipAt on, skipBytes of them (a parent model's
        /// layout, for an audit).</summary>
        public static ulong FlyPowered(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, WindSetup? wind = null)
        {
            FlightSetup setup = PoweredSetup(spec);
            if (wind != null) setup.Wind = wind.Value;
            core = new FlightCore(setup);
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong hash = 0xCBF29CE484222325UL;
            for (int k = 0; k < PoweredSteps; k++)
            {
                StickSample stick = PoweredStick(k);
                core.Advance(stick);
                LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                hash = Fnv(hash, record, 0, skipAt);
                hash = Fnv(hash, record, skipAt + skipBytes, record.Length);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, core.State.Step, core.Events[i]);
                    hash = Fnv(hash, e, 0, e.Length);
                }
            }
            return hash;
        }

        /// <summary>
        /// Flies the contact golden flight: legs on from the floor, the closed-loop pilot up to a metre for 4 s, then down
        /// at 0.5 m/s; with the skids 0.1 m up the sticks are held (roll, pitch and yaw centred) and the drone bounces; 1.5 s
        /// later the throttle is cut and it comes to rest. Returns its hash, with the core for a report.
        /// </summary>
        public static ulong FlyContact(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, WindSetup? wind = null)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, mh, true, new Double3(0, stand, 0));
            setup.Wind = wind ?? WindSetup.Of(WindLevel.Calm);
            core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, new Double3(0, stand + 1, 0));
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong hash = 0xCBF29CE484222325UL;
            StickSample held = default;
            int holdAt = -1;
            for (int k = 0; k < ContactSteps; k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick;
                if (k < 16) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8 };
                else if (holdAt < 0)
                {
                    if (k >= 4000) pilot.Target = new Double3(0, stand + 1 - 0.5 * (k - 4000) * FlightCore.Dt, 0);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                    if (k >= 4000 && s.Position.Y - stand < 0.1)
                    {
                        holdAt = k;
                        held = stick;
                        held.Roll = held.Pitch = held.Yaw = 0;
                    }
                }
                else
                {
                    stick = held;
                    stick.HostUs = k * FlightCore.StepUs;
                    if (k - holdAt >= 1500) stick.Throttle = 0;
                }
                core.Advance(stick);
                LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                hash = Fnv(hash, record, 0, skipAt);
                hash = Fnv(hash, record, skipAt + skipBytes, record.Length);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, core.State.Step, core.Events[i]);
                    hash = Fnv(hash, e, 0, e.Length);
                }
            }
            return hash;
        }

        static ulong Fnv(ulong h, byte[] bytes, int from, int to)
        {
            for (int i = from; i < to; i++) h = unchecked((h ^ bytes[i]) * 0x100000001B3UL);
            return h;
        }
    }
}
