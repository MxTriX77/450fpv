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
    /// with its drag and its contacts, in the Windy stand-in wind, armed by Betaflight's rules and flown 10 s by a scripted
    /// pilot through every part of the controller with the builder's filter chain, propulsion, the motors' asymmetry
    /// (p7b-airframe 1.1, on the seed's draws; the contact flight too), the rotors' vortex rings (1.2; the contact flight
    /// too), their ground effect (1.3; the contact flight too, where it acts), the airframe's modes (1.4; the contact flight
    /// too) and the air (a spin-up, a
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
        //   1.4b's 0x3695FC49DCA906DC bit for bit (pinned as a test until model 6), the contacts adding nothing 300 m
        //   up, as in 1.6; the contacts' bytes are zero throughout (the touching count 0).
        // - against 1.6: with every filter off and the PID reading the gyro at the loop (1.4b's temporary edit, made again
        //   and reverted), hashed over 1.6's 1112-byte layout with its events, the flight is 1.6's 0x798AFECB92E12DB7 bit
        //   for bit. With the chain on, it moves as 1.4b's did, from step 21.
        //   Model 5: 0x734B61C0C15797BC.
        // 1.7: re-recorded with ModelVersion 6 (the rigid body's midpoint rate, RigidBody.MidpointPasses), audited: with
        // the passes at 0 (a temporary edit, reverted) the flight is model 5's 0x734B61C0C15797BC bit for bit, and over
        // 1.4b's layout 1.4b's 0x3695FC49DCA906DC. With the three passes it moves from step 1 (the first kick off the
        // principal axes); the closed loop holds it to 0.34 mrad of attitude and 32 mm of position by 10 s, 199.4 m up.
        // Model 6: 0x3F13A5CF5DB7DAFD.
        // p7b-airframe 1.0: re-recorded with ModelVersion 7 (drone.json's τ_esc 5 → 0.4 ms), audited: with τ_esc at 5 ms
        // (a temporary test parsing drone.json so, removed) the flight is model 6's 0x3F13A5CF5DB7DAFD bit for bit. At
        // 0.4 ms the records move from the first armed step, 21; the open-loop script drifts 55 mm and 0.16 mrad by the
        // roll step (step 2000), 0.18 m and 7.9 mrad by the chop's end (5500), then the weave carries it to 4.7 m and
        // 0.16 rad by 10 s, 195.1 m up against 199.4; 88 events against 98 (74 CurrentLimit against 84).
        // Model 7: 0xA65FBFBCBCFC5B96.
        // p7b-airframe 1.1: re-recorded with ModelVersion 8 (the 1328-byte record: model 7's 1296 bytes, then the rotors'
        // angles) with the motors' asymmetry in its setup (seed 0x450F: Kv −1.41, −0.80, −2.24, −3.16 %; ESC lags −42, +69,
        // −11, −28 µs; unbalances 0.023–0.077 g·mm). Audited: with the asymmetry off, hashed over model 7's layout with its
        // events, the flight is model 7's bit for bit (a kept test, WithTheAsymmetryOffEachGoldenIsModel7s). With it the
        // records move from the first armed step, 21 (the motors spin up apart): 0.18 m and 7.7 mrad by the roll step
        // (2000), 0.90 m and 39 mrad by the chop's end (5500), 5.3 m and 0.11 rad by 10 s, 194.1 m up against 195.1;
        // 92 events against 88 (78 CurrentLimit against 74: the cold motors reach the limit at other steps).
        // Model 8: 0x08FBAF9331110BE9.
        // p7b-airframe 1.2: re-recorded with ModelVersion 9 (the 1400-byte record: model 8's 1328 bytes, then the rings' 72)
        // with the vortex rings in its setup. Audited: with them off, hashed over model 8's layout with its events, the flight
        // is model 8's bit for bit (a kept test, WithTheRingsOffEachGoldenIsModel8s). With them the records move from step 393:
        // the drone armed 300 m up sinks as the script's throttle ramps (0.47 by 1 s, below its hover), and at step 392 the
        // rear left rotor's descent along its axis reaches 1.04 v_h (3.7 m/s at v_h 3.2 m/s, the Windy air's 0.95 v_h in its
        // plane), the others within 12 steps; the four leave the ring at η 0.2 at steps 2291–2338, as the roll step tilts
        // their axes. 0.24 m and 2.1 mrad apart by the roll step (2000), 1.0 m and 5.1 mrad by the chop's end (5500), 0.94 m
        // and 8.1 mrad by 10 s, 193.2 m up against 194.1 (the ring's lost thrust); 100 events against 92 (the 4 entries and
        // 4 exits).
        // Model 9: 0x5AF86BCB6FD0B3A2.
        // p7b-airframe 1.3: re-recorded with ModelVersion 10 (the 1496-byte record: model 9's 1400 bytes, then the rotors'
        // surfaces and re-ingested wash, 96) with the ground effect in its setup. Audited: with it off, over model 9's layout,
        // the flight is model 9's bit for bit (a kept test, WithTheGroundEffectOffEachGoldenIsModel9s); with it on too (a kept
        // test, ThePoweredGoldenFindsNoSurface): 193-300 m up no ray finds a surface within its 1.02 m, so every gain is 1
        // and the wash 0, not a bit moves; the new bytes hold the sampled ground's distance along each axis and +∞ above.
        // Model 10: 0x0B91602E467653F4.
        // p7b-airframe 1.3c: re-recorded with ModelVersion 11 with the rotors' flapping in its setup (the record's layout
        // unchanged). Audited: with it off, every record and event is model 10's bit for bit (a kept test,
        // WithTheFlappingOffEachGoldenIsModel10s). With it the flight moves from step 21, the first armed step with the rotors
        // turning in Windy's air: by the roll step's end (2500) 191 mm and 34 mrad apart, by 10 s 40.6 m on the open-loop script
        // (the discs tilt away from each gust), 220.3 m up against 193.2; 102 events against 100; armed, no crash.
        // Model 11: 0x9057D18CF741A255.
        // p7b-airframe 1.5: re-recorded with ModelVersion 12, drone.json's packs on the P50B's table to its 2.5 V cut-off.
        // Audited: on model 11's table, every record and event is model 11's bit for bit (the kept test,
        // OnModel11sPackTableEachGoldenIsModel11s). At charge 0.9 both tables give 4.07 V a cell; the first armed draw (step
        // 20) takes the charge into the new table's 0.85–0.90 segment (1.0 V a unit against 0.9), so the packs differ by at
        // most 3.2 mV and the flight by at most 13.9 mm and 0.15 mrad (0.19 mm and 0.004 mrad by the roll step), 220.30 m up
        // against 220.32; the same 102 events, armed, no crash.
        // Model 12: 0x63D668DD01A1E587. p7b-airframe 1.4: re-recorded with ModelVersion 13 (the 1656-byte record), the
        // airframe's modes in its setup. Audited: with the modes off, over model 12's 1496 bytes, every record and event is
        // model 12's bit for bit (the kept test, WithTheModesOffEachGoldenIsModel12s). With them, the flight moves from step 21,
        // its first armed one: the Windy air's drag on the body, relieved through the masses, has set the payload's sling and
        // the coil's mount swinging, and the gyro reads the frame's turn; 73 mm by 2.5 s, 31 m by 10 s on the open-loop script,
        // 227.76 m up against 220.30, armed, no crash.
        // Model 13: 0x0F843363CA8DBFBA. D-050: re-recorded with ModelVersion 14 on the simpler drone (drone.json: the coil
        // beneath, a bottle strapped on top, fitted to the drone model, the rotors' in-plane drag re-tuned to 0.25 for clip P's
        // pitch on its drag), both loads on strapped mounts. Audited against model 13 (temporary edits, removed): with the
        // sling put back, model 13's drone.json (the fixture) flies model 13's three hashes bit for bit, and with the modes off
        // models 7–12's (the kept tests). On model 13's file the strapped payload alone moves the flight from step 1 (the
        // sling's mode, mode_shape0): 74 mm by 2.5 s, 31 m by 10 s on the open-loop script. On D-050's file it moves from
        // step 1 (the Windy air on the new silhouette, position.x): 147 mm by 2.5 s, 1.3 m by 5 s, 31.4 m by 10 s, 220.36 m
        // up against 227.76; 102 events against 134; armed, no crash.
        // Model 14: 0x381ABAE6909C139F. The electronics fit: re-recorded with ModelVersion 15 (drone.json: the stack's box
        // between the plates, the bottle on the upper plate at 0.068 m, the centre of pressure at −0.018 m). Audited against
        // model 14: on model 14's fit (WithModel14Fit, the kept test) all three are model 14's bit for bit. On the fit the
        // flight moves from step 1 (position.x: the Windy air's moment about the lower centre of mass) and keeps its course
        // on the open-loop script: 12 mm by 2.5 s, 0.16 m by 5 s, 1.93 m and 53 mrad by 10 s, 220.65 m up against 220.36;
        // 112 events against 102; armed, no crash.
        public const ulong PoweredHash = 0xCCECAFB7466D6ADEUL;

        // 1.6: first recorded with ModelVersion 4 as 0x8B568804552379E6.
        // 1.6 and 1.4b merged: re-recorded with ModelVersion 5. Audited against 1.6: with every filter off and the PID
        // reading the gyro at the loop (the temporary edit), hashed over 1.6's 1112 bytes with its events, the flight is
        // 1.6's 0x8B568804552379E6 bit for bit. With the builder's chain the controller's state moves from step 2 (D min's
        // filters, disarmed) and the motion from the first armed step, 9; the flight keeps its course: the sticks held at
        // the same step (7219) and the skids down at the same step (7412), off the ground 1 ms later, 140.1 mm up in the
        // held bounce against 142.3, the last hop after the cut ending 8 ms later (step 9313 against 9305), 5 impacts,
        // the same height at rest. It has no audit against 1.4b, whose core has no contacts: the flight starts on the floor.
        // Model 5: 0xF1D0832353311083.
        // 1.7: re-recorded with ModelVersion 6, audited: with the midpoint passes at 0 (the temporary edit) the flight is
        // model 5's 0xF1D0832353311083 bit for bit. With them it moves from step 1, by at most 1.0 µm and 0.62 µrad, and
        // keeps its course: the sticks held at step 7219, 5 impacts, at rest at the same height to 10 µm on 8 pairs.
        // Model 6: 0x3EE12C8D692BADF3.
        // p7b-airframe 1.0: re-recorded with ModelVersion 7 (τ_esc 0.4 ms), audited: with τ_esc at 5 ms the flight is model
        // 6's 0x3EE12C8D692BADF3 bit for bit. At 0.4 ms it keeps its course: the sticks held at step 7220 (7219), the skids
        // down at the same step (7412), the held bounce 135.7 mm up (140.1), the last touchdown after the cut 16 ms
        // earlier (step 9297 against 9313), 5 impacts, no crash, at rest at the same height to 1.5 µm.
        // Model 7: 0x3AF33645FF8F1919.
        // p7b-airframe 1.1: re-recorded with ModelVersion 8 with the motors' asymmetry on (seed 0x450F's draws, as the
        // powered flight's), audited: with it off, over model 7's layout, model 7's flight bit for bit (the kept test). With
        // it the flight moves from the first armed step, 9, and keeps its course: 5 impacts, no crash, at rest on 8 pairs
        // 4 µm from the identical-rotor flight's height, the idling props' unbalance shaking it at 0.11 mrad/s.
        // Model 8: 0x5D45B356ECD6B940.
        // p7b-airframe 1.2: re-recorded with ModelVersion 9 with the rings on, audited: with them off, over model 8's layout,
        // model 8's flight bit for bit (the kept test). With them it is model 8's to step 8709: in the held bounce's last
        // drop (1.0 m/s down, 72 mm above its standing height, on the held throttle) the front right rotor reaches η 0.45
        // (v_h 1.9 m/s), the others within 8 steps; all four leave below η 0.2 as the skids land (steps 8776–8778), the front
        // right once more on the last hop (8952–8973). It keeps its course: 5 impacts, no crash, at rest 0.2 mm and 0.19 mrad
        // from model 8's; 133 events against 123.
        // Model 9: 0x8D965FFD8708C6C3.
        // p7b-airframe 1.3: re-recorded with ModelVersion 10 with the ground effect on, audited: with it off, over model 9's
        // layout, model 9's flight bit for bit (the kept test). With it the flight moves from step 10, the first with the
        // rotors turning: on its legs the hubs stand 0.288 m (2.27 R) above the floor, inside the cushion and the wash's 4 R.
        // It keeps its course: the climb 8 mm higher at 1 s on the cushion (0.740 m against 0.732), the same at 4 s; the
        // sticks held 7 ms later (the skids below 0.1 m at step 7079 against 7072), the held bounce 163.3 mm up against
        // 130.1 (the held throttle gains the cushion near the floor), at most 54 mm apart (step 8766) and 19 mrad, 5 impacts,
        // no crash, at rest at the same height to 10 µm and 15 mm aside; 135 events against 133.
        // Model 10: 0x05EDD1881425872E.
        // p7b-airframe 1.3c: re-recorded with ModelVersion 11 with the flapping on, audited: with it off, model 10's flight bit
        // for bit (the kept test). With it the flight moves from step 9, the first with the rotors turning in Calm's air, and
        // keeps its course: at most 15 mm from model 10's path, 5 impacts, no crash, at rest at the same 0.2519 m, 11 mm aside.
        // The tumble has no rotors and keeps model 10's hash.
        // Model 11: 0xF6E32F2FDC831268.
        // p7b-airframe 1.5: re-recorded with ModelVersion 12 on the P50B's table, audited as the powered flight: on model 11's
        // table it is model 11's bit for bit. It keeps its course: 5 impacts, no crash, at rest on 8 contacts at the same
        // 0.2519 m, 0.53 mm aside, the packs 2 mV apart. The tumble has no rotors and keeps model 10's hash.
        // Model 12: 0x8B50CC1BD8EBC8FF. p7b-airframe 1.4: re-recorded with ModelVersion 13, the modes in its setup, audited as the
        // powered flight: with them off, over model 12's layout, it is model 12's bit for bit. With them it moves from step 9
        // and keeps its course: within 32 mm of model 12's path, 5 impacts, no crash, at rest at the same 0.2519 m, 26 mm aside.
        // Model 13: 0xD1EA300928A02EEE. D-050: re-recorded with ModelVersion 14, audited as the powered flight: with the sling
        // put back, model 13's file flies model 13's bit for bit; the strapped payload alone moves it from step 1 and it keeps
        // its course (within 35 mm, 5 impacts, at rest at the same 0.2519 m, 24 mm aside). On D-050's file (the legs 3.5 mm
        // further out, the T-joints on the ground) it moves from step 1 and keeps its course: within 40 mm of model 13's path
        // (step 2268) and 26 mrad, 5 impacts, no crash, at rest on 8 contacts at the same 0.2519 m, 35 mm aside; 129 events
        // against 153.
        // Model 14: 0xB57132DE1812820D. The electronics fit (ModelVersion 15), audited as the powered flight: on model 14's
        // fit it is model 14's bit for bit. On the fit it moves from step 1 and keeps its course: within 7.2 mm of model 14's
        // path (step 8816, the held bounce) and 4.3 mrad, 5 impacts, no crash, at rest on 8 contacts at the same 0.2519 m,
        // 5.7 mm aside; 131 events against 129.
        public const ulong ContactHash = 0xC378E1EFFB9AEC93UL;

        // 1.7 (QA's review-base finding 3): first recorded with ModelVersion 6. Its checks fail the known-wrong steps (each a
        // temporary edit, reverted): turning at the start's rate (no midpoint passes) gains 9.0e-4 of the energy in 10 s;
        // leaving out the momentum's transport into the new axes (QA's experiment) loses 16 % of the momentum, and the
        // body rate never wanders.
        // Model 7: 0xE3178EE7FF322698. p7b-airframe 1.1 (ModelVersion 8): the 1328-byte record, the rotors' angles zero (no
        // propulsion); over model 7's layout the hash is model 7's (the kept test). Model 8: 0xF7E0F587B45FA098.
        // p7b-airframe 1.2 (ModelVersion 9): the 1400-byte record, the rings' bytes zero (no propulsion); over model 8's
        // layout the hash is model 8's (the kept test). Model 9: 0x50541A5726F695D8.
        // p7b-airframe 1.3 (ModelVersion 10): the 1496-byte record, the surfaces' and the wash's bytes zero (no propulsion);
        // over model 9's layout the hash is model 9's (the kept test). Model 10–12: 0x58EF4F6AE8D111D8. p7b-airframe 1.4
        // (ModelVersion 13): the 1656-byte record, the modes' bytes zero (no propulsion, no modes); over model 12's layout the
        // hash is model 12's (the kept test). Model 13: 0xB371E06FF28075D8. D-050 (ModelVersion 14): D-050's L4 body (4.832 kg;
        // roll 0.0479, pitch 0.0508, yaw 0.0599 kg·m²; I_yz −7.9e-5) moves it from step 1; model 13's file gives model 13's
        // hash (the audit). Its checks: the momentum within 1.4e-12, the energy 2.9e-12, the parabola 5.1e-11 m, the rate
        // wandering 5.18 rad/s.
        // Model 14: 0x26CDDB5EDB155896. The electronics fit (ModelVersion 15): the L4 body with the bottle on the upper plate
        // (4.832 kg; roll 0.0445, pitch 0.0474, yaw 0.0599 kg·m²; I_yz −6.5e-5) moves it from step 1; model 14's fit gives
        // model 14's hash (the kept test). Its checks: the momentum within 7.1e-13, the energy 1.4e-12, the parabola
        // 5.1e-11 m, the rate wandering 5.55 rad/s.
        public const ulong TumbleHash = 0xFB841727FADDC7A7UL;
        public const int TumbleSteps = 10000;

        // 1.6b: first recorded with ModelVersion 7, on the test range's content RangeContent (MapWorld.ContentHash, query
        // version 2): a change of the map or its assets moves the content hash first, so an audit tells the world's change
        // from the physics'. Model 7: 0x778DD7ED57A37F62 on content 0xD6DE238216CEB4B9. The merge with p7b-airframe
        // (ModelVersion 16): the content moves by physics_materials.json's new carbon_composite row alone (every worldbench
        // query hash unchanged); the flight by D-050's drone, the airframe's effects (FlyContact's five) and the seam rule.
        // Audited (the kept test): every mechanism off on 1.6b's drone.json over 1.6b's layout, without the seam rule (a
        // temporary edit) it is model 7's bit for bit, with it 0x613987892649445F.
        public const ulong RangeHash = 0x292C0858705902B6UL;
        public const ulong RangeContent = 0x6114242E06475FB5UL;
        public const int RangeSteps = 12000;

        /// <summary>Model 13's drone.json (D-043's layout, before D-050), a fixture: the kept audits of models 7–12 fly on it,
        /// those of models 7–11 with model 11's pack table on it.</summary>
        public const string Model13DronePath = "unity/Packages/com.fpv450.sim/Tests/Fixtures/drone-model13.json";

        /// <summary>Model 11's pack table (drone.json's before p7b-airframe 1.5: one cell at charge 0, 0.1, …, 1, from 3.0 V),
        /// which the kept audits of models 7–11 fly on.</summary>
        public static readonly double[] Model11Ocv = { 3.00, 3.45, 3.55, 3.62, 3.67, 3.73, 3.81, 3.90, 3.98, 4.07, 4.20 };

        /// <summary>The spec with model 11's pack table on both packs. Pass a freshly parsed spec: it is changed in place.</summary>
        public static DroneSpec WithModel11Packs(DroneSpec spec)
        {
            for (int k = 0; k < spec.Packs.Length; k++) spec.Packs[k].Ocv = (double[])Model11Ocv.Clone();
            return spec;
        }

        /// <summary>The spec with model 14's fit (drone.json before the electronics were fitted between the plates): the
        /// electronics' box 0.005–0.045 m up, 17 mm over the upper plate, its capsule with it; the bottle on it at 0.087 m;
        /// the centre of pressure at −0.01 m. Pass a freshly parsed spec: it is changed in place.</summary>
        public static DroneSpec WithModel14Fit(DroneSpec spec)
        {
            int electronics = spec.PartIndex("electronics"), bottle = spec.Payload.Part;
            spec.Parts[electronics].Position.Y = 0.025;
            spec.Parts[electronics].Size.Y = 0.04;
            spec.Parts[bottle].Position.Y = 0.087;
            for (int i = 0; i < spec.Contacts.Shapes.Length; i++)
            {
                ref ContactShape s = ref spec.Contacts.Shapes[i];
                if (s.Part == electronics)
                {
                    s.A = new Double3(0, 0.025, -0.02);
                    s.B = new Double3(0, 0.025, 0.04);
                    s.Radius = 0.02;
                }
                else if (s.Part == bottle)
                {
                    s.A.Y = 0.087;
                    s.B.Y = 0.087;
                }
            }
            spec.Aero.BodyCenterOfPressure.Y = -0.01;
            return spec;
        }

        /// <summary>The spec with the trunk's ESC lag (drone.json's 5 ms before p7b-airframe 1.0) on every motor. Pass a
        /// freshly parsed spec: it is changed in place.</summary>
        public static DroneSpec WithTheTrunksEscLag(DroneSpec spec)
        {
            for (int i = 0; i < spec.Motors.Length; i++) spec.Motors[i].EscTau = 0.005;
            return spec;
        }

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
                Asymmetry = spec.BuildAsymmetry(),
                VortexRing = spec.BuildVortexRing(),
                GroundEffect = spec.BuildGroundEffect(),
                Flapping = spec.BuildFlapping(),
                Modes = spec.BuildModes(),
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
        public static ulong FlyPowered(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, bool contacts = true,
            bool asymmetry = true, bool ring = true, bool ground = true, bool flapping = true, bool modes = true)
        {
            FlightSetup setup = PoweredSetup(spec);
            if (!modes) setup.Modes = null;
            if (!flapping) setup.Flapping = null;
            if (!contacts) setup.Contacts = null;
            if (!asymmetry) setup.Asymmetry = null;
            if (!ring) setup.VortexRing = null;
            if (!ground) setup.GroundEffect = null;
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
        public static ulong FlyContact(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, bool asymmetry = true,
            bool ring = true, Action<FlightCore> after = null, bool ground = true, bool flapping = true, bool modes = true)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, mh, true, new Double3(0, stand, 0));
            setup.Wind = WindSetup.Of(WindLevel.Calm);
            if (asymmetry) setup.Asymmetry = spec.BuildAsymmetry();
            if (ring) setup.VortexRing = spec.BuildVortexRing();
            if (ground) setup.GroundEffect = spec.BuildGroundEffect();
            if (flapping) setup.Flapping = spec.BuildFlapping();
            if (modes) setup.Modes = spec.BuildModes();
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
                after?.Invoke(core);
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
        /// Flies the test range's contact golden (1.6b, D-044): FlyContact's flight on S04 #707's straw mat, where
        /// SampleGround reports Hole and the mat's straw boxes (490 N/m, over the patch's soil) are all the ground: legs on,
        /// settling into the straw, the closed-loop pilot up to a metre for 4 s, then down at 0.5 m/s; with the skids 0.1 m
        /// over the straw the sticks are held (roll, pitch and yaw centred) and the mat takes the bounce; 1.5 s later the
        /// throttle is cut and it settles into the straw again. In the Calm stand-in wind, as the stub's, with the airframe's
        /// effects on as FlyContact's (its toggles and its parent layout's skip, for an audit). Returns its hash, with the core
        /// for a report.
        /// </summary>
        public static ulong FlyRange(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, bool asymmetry = true,
            bool ring = true, bool ground = true, bool flapping = true, bool modes = true)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            Double3 foot = RangeScenarios.Straw;
            FlightSetup setup = ContactScenarios.Setup(spec, materials, mh, true, foot + new Double3(0, stand, 0));
            setup.World = new FlightWorld(RangeScenarios.World);
            setup.Wind = WindSetup.Of(WindLevel.Calm);
            if (asymmetry) setup.Asymmetry = spec.BuildAsymmetry();
            if (ring) setup.VortexRing = spec.BuildVortexRing();
            if (ground) setup.GroundEffect = spec.BuildGroundEffect();
            if (flapping) setup.Flapping = spec.BuildFlapping();
            if (modes) setup.Modes = spec.BuildModes();
            core = new FlightCore(setup);
            var pilot = new SimPilot(core.Controller, foot + new Double3(0, stand + 1, 0));
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong hash = 0xCBF29CE484222325UL;
            StickSample held = default;
            int holdAt = -1;
            for (int k = 0; k < RangeSteps; k++)
            {
                ref readonly FlightState s = ref core.State;
                StickSample stick;
                if (k < 16) stick = new StickSample { HostUs = k * FlightCore.StepUs, Arm = k >= 8 };
                else if (holdAt < 0)
                {
                    if (k >= 4000) pilot.Target = foot + new Double3(0, stand + 1 - 0.5 * (k - 4000) * FlightCore.Dt, 0);
                    stick = pilot.Sticks(core, k * FlightCore.StepUs);
                    if (k >= 4000 && s.Position.Y - (foot.Y + stand) < 0.1)
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

        /// <summary>The tumble's setup: the repository drone's nominal body (L4, legs on) alone, no propulsion, air or
        /// contacts, launched 600 m up with a velocity and a spin off its principal axes (its I_yz and the payload's
        /// offset), falling under gravity.</summary>
        public static FlightSetup TumbleSetup(DroneSpec spec) => new FlightSetup
        {
            Seed = 0x450F,
            World = FlightWorld.Floor(),
            Mass = spec.BuildMass(spec.Nominal),
            StartPosition = new Double3(0, 600, 0),
            StartVelocity = new Double3(3, 2, -1),
            StartBodyRate = new Double3(2, -1, 3),
        };

        /// <summary>What the tumble keeps, each relative to its start: the angular momentum about the CoM in the world,
        /// the rotational energy, and the CoM's distance from semi-implicit Euler's exact discrete parabola (m); and how far
        /// the body rate wanders in body axes (rad/s), which a body without ω × I·ω never does.</summary>
        public struct TumbleChecks
        {
            public double Momentum, Energy, Parabola, Wander;
        }

        /// <summary>
        /// Flies the tumble golden (1.7, QA's review-base finding 3): TumbleSetup for 10 s, its hash every step's record.
        /// It guards the gyroscopic term on a body that has one, where the free-body golden's cube has none: a step that
        /// leaves out the momentum's transport into the new axes, or turns at the start's rate, fails its checks.
        /// </summary>
        public static ulong FlyTumble(DroneSpec spec, out FlightCore core, out TumbleChecks checks, int skipAt = LogFormat.StepSize, int skipBytes = 0)
        {
            FlightSetup setup = TumbleSetup(spec);
            core = new FlightCore(setup);
            MassProperties b = setup.Mass.Properties;
            Double3 c = b.CenterOfMass;
            Double3 com0 = setup.StartPosition + Quat.Identity.Rotate(c);
            Double3 v0 = setup.StartVelocity + Double3.Cross(setup.StartBodyRate, c);
            Double3 l0 = b.Inertia * setup.StartBodyRate;
            double e0 = Double3.Dot(setup.StartBodyRate, l0);
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong hash = 0xCBF29CE484222325UL;
            checks = default;
            for (int k = 0; k < TumbleSteps; k++)
            {
                core.Advance(StickSample.Neutral);
                ref readonly FlightState s = ref core.State;
                LogFormat.EncodeStep(record, s, StickSample.Neutral, core.Trace);
                hash = Fnv(hash, record, 0, skipAt);
                hash = Fnv(hash, record, skipAt + skipBytes, record.Length);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, s.Step, core.Events[i]);
                    hash = Fnv(hash, e, 0, e.Length);
                }
                Double3 h = b.Inertia * s.BodyRate;
                double n = k + 1, dt = FlightCore.Dt;
                Double3 parabola = com0 + v0 * (n * dt) + FlightCore.Gravity * (dt * dt * n * (n + 1) / 2);
                checks.Momentum = DetMath.Max(checks.Momentum, (s.Attitude.Rotate(h) - l0).Length() / l0.Length());
                checks.Energy = DetMath.Max(checks.Energy, DetMath.Abs(Double3.Dot(s.BodyRate, h) - e0) / e0);
                checks.Parabola = DetMath.Max(checks.Parabola, (s.Position + s.Attitude.Rotate(c) - parabola).Length());
                checks.Wander = DetMath.Max(checks.Wander, (s.BodyRate - setup.StartBodyRate).Length());
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
