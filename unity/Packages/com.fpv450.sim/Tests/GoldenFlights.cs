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
        // Model 15 on unity/main: 0xCCECAFB7466D6ADE. On p7c-wind's branch, from model 5 (0x734B61C0C15797BC):
        // p7c-wind 1.4: re-recorded with ModelVersion 6 (the 1392-byte record: model 5's 1296 bytes, the stand-in's gust
        // slot now the wind at the body's origin, then the four hubs' winds) in the Windy field, audited case by case
        // against model 5 (35c6432) by flying both models in still air and in a steady 5 m/s from 270° and hashing model
        // 5's 1296 bytes (for steady air less the 24-byte wind slot, which model 5 left 0): every record and event of
        // all 10 000 steps is bit-identical (StillAirPoweredHash below; steady 0x00D35F9AE93901C0 on both). In the Windy
        // field the flight moves from step 1, the wind's first: the field's draws are new (the direction, the strength,
        // the meander, the shifts, the modes), so the stand-in's air is gone; it ends 203.1 m up against 199.4 and
        // 16.6 m from model 5's end.
        // p7c-wind 1.6: re-recorded with ModelVersion 7 (drone.json's rotor in-plane drag 0.225 → 0.044 N per m/s, the
        // only change), audited case by case: on model 6's drone (Model6Drone) both goldens are model 6's pins and the
        // still-air ones model 5's, every record and event (WithModel6sRotorDragTheGoldenFlightsAreModel6s). With the new
        // drag every flight moves from step 21, the first with the hubs moving in their discs' planes through the air
        // (position.x), in the Windy field, in still air and in a steady 5 m/s alike: the open-loop sticks leave the drone
        // tilted and sinking, its discs edgewise to the descent meet 5× less in-plane drag, and it ends 131.2 m up against
        // 203.1 (still air 128.8 against 200.0), 71.9 m from model 6's end.
        // p7c-wind 1.5: re-recorded with ModelVersion 8 (the levels' centres from the cruise, Severe 7.2, Windy 3.6, Calm
        // 1.08 m/s; the wakes, which no golden flies). Audited case by case: with the centres set back to model 7's
        // (5.0 / 2.5 / 0.8, the temporary edit) both goldens are model 7's pins (0x376ECD53376BBC68, 0xCD821FAEE49DF277)
        // and on model 6's drone model 6's (0xBC85ED28D55D2986, 0x3D6092D3121E468A), every record and event, so the wake
        // code and the header's new fields change no flight without obstacles; the still-air pins hold as they are. At
        // model 8's centres the Windy flight moves from step 1, the wind's first: the strength draw is the same uniform
        // draw in a band 1.44 times model 7's. The model-6-drone pins of 1.6's audit fly model 7's centres only, so their
        // standing test went with them (flightbench --golden no longer flies them).
        // p7c-wind 1.7: re-recorded with ModelVersion 9 (rain; the 1448-byte record: model 8's 1392 bytes, then the rain's
        // force, the film and the air at the camera). The golden flies Sunny, and audited case by case: over model 8's
        // 1392 bytes with its events it is model 8's pin (Model8PoweredHash) bit for bit, every record of the 10 000 steps
        // (RainTests.ASunnyFlightIsModel8sBitForBit): a Sunny flight opens no rain stream and adds no film part. Only
        // the new bytes move: the rain's two slots stay 0, and the camera's air is the field's at drone.json's new lens
        // position. RainPoweredHash below pins the same flight in Severe rain.
        // Model 10 on p7c-wind's branch: 0xFF9521B7653C909C.
        // p7c-wind merged with unity/main: re-recorded with ModelVersion 17 (the 1808-byte record: model 5's 1296 bytes with
        // the wind at the body's origin where the stand-in's gust was, p7c-wind's 152, then p7b-airframe's 360), in the Windy
        // field on D-050's drone with p7c-wind 1.6's derived rotor drag (0.044). Audited case by case against each parent with
        // the other's terms off, every record and event of each flight bit for bit:
        // - against p7c-wind's model 10: with the rigid body's midpoint passes at 0 (main's model 6; a temporary edit,
        //   reverted), every airframe mechanism off, the trunk's 5 ms ESC lag and model 11's pack table on model 13's file
        //   (main's models 7 and 12 undone, D-043's layout), its rotor drag 0.044 and p7c-wind's lens (0, −0.005, 0.115),
        //   hashed over p7c-wind's 1448 bytes (the record less p7b-airframe's 360), the powered, contact and rain goldens are
        //   model 10's 0xFF9521B7653C909C, 0xBC4E9FA68E254625 and 0x6E28860F47C45BC1.
        // - against unity/main's model 15: with main's tuned rotor drag (0.25) on the repository's drone, hashed over main's
        //   1656 bytes (the record less p7c-wind's 152), in still air (the kept test, InStillAirTheGoldenFlightsAreModel15s),
        //   in a steady 5 m/s from 270° and in a 4 m/s head gust stepping in at 3 s (the stand-in's gust slot written as it
        //   carried it, −Mean before the onset and 0 after), the powered and contact flights are model 15's, as flown on main's
        //   code with the goldens' air so set (a temporary edit there): still 0xABF47E6E55E48EB8 and 0x557DEBE410FAEEA1, steady
        //   0xC64EBF46E14DCB08 and 0x77E7E0AB78B82E81, stepped 0xFEA7140235016C01 and 0xF5C392DF1CB6E9B8; the tumble is model
        //   15's 0xFB841727FADDC7A7. The field's steady onset and the rotors' inflow seam (RotorInflow) carry main's air.
        // In the Windy field the flight moves from step 1 against either parent (the field's draws against main's stand-in;
        // the midpoint passes against p7c-wind's) and ends 148.18 m up (main's 220.65 m); 106 events; armed, no crash.
        // Model 17: 0x14FA2EF5AE127F25. p7c-wind 1.5's re-grade: re-recorded with ModelVersion 18 (Severe's centre 7.2 → 9.5
        // m/s, Windy's 3.6 → 4.75, Calm's 1.08 → 1.425). Audited: with the centre set back to 7.2 (a temporary edit, reverted)
        // the powered, contact, rain and tumble goldens and the still-air audit are model 17's, every record and event. At
        // 11.5 the Windy flight moves from step 1 (the strength draw, 4.83 m/s in Windy's band) and ends 148.12 m up,
        // 102 events, armed, no crash.
        // Model 18: 0xFE81E38EFCBD2F4E. D-060: re-recorded with ModelVersion 19 (the open field's z0 0.03 → 0.10 m, σ_u/U
        // 0.135 → 0.164). Audited: with z0 0.03 m and σ_u/U 0.135 (a temporary edit, reverted) the powered, contact, rain and
        // tumble goldens are model 18's, every record and event. With class 4's roughness the Windy flight moves from step 1
        // (the turbulence's amplitude and the profile) and keeps its course: armed, no crash, 148.28 m up against 148.12,
        // 2.5 m from model 18's end and within 8 mrad of its attitude. Then the turbulence carried from the 1 m cut to the
        // discs' 0.5 m (ModelVersion 19 still; the branch had not landed): with the cut back at 1 m and 128 and 64 modes (a
        // temporary edit, reverted) every golden is the class-4 step's (powered 0xD28B561A9782C097, contact
        // 0xA05AEB00620769BF, rain 0xCCAE77686A84020E); to 0.5 m the Windy flight moves from step 1 and keeps its course:
        // armed, no crash, 147.90 m up, 0.59 m from the class-4 step's end and within 8.3 mrad.
        // Model 19: 0xA1FF085091A019CD. D-064: re-recorded with ModelVersion 23 (the 1840-byte record: model 19's 1808 bytes,
        // then the rotors' wakes, 32) with the rotors' own wakes in its setup and the motors' resistance, friction and inertia
        // drawn. Audited: without them, over model 19's 1808 bytes, every record and event is model 19's (the kept test,
        // WithoutD064TheGoldenFlightsAreModel19s; 193–300 m up no surface is in reach, so the re-ingested wash's new numbers
        // never act). With them it moves from step 21, the first armed step (the wakes wander from the rotors' first turn;
        // the motors' spreads alone move it from the same step), and keeps its course: armed, no crash, 149.00 m up against
        // 147.90 at 10 s, 1.16 m from model 19's end and within 1.9° of its attitude (the motors' spreads alone: 0.83 m, 0.24°).
        public const ulong PoweredHash = 0x5073BC74B404B3AAUL;

        // The powered golden in Calm air and Severe rain (p7c-wind 1.7, first recorded with ModelVersion 9): the drops'
        // drag and film on top of the Calm flight, which it leaves from step 1 (the drops' force at rest).
        // Model 10 on p7c-wind's branch: 0x6E28860F47C45BC1. p7c-wind merged with unity/main (ModelVersion 17), audited as the
        // powered flight: with main's terms off it is model 10's bit for bit; on the merge the film holds 9.50 g at 10 s.
        // Model 17: 0x5FA1F49E82CBC0D5. p7c-wind 1.5's re-grade (ModelVersion 18), audited as the powered flight: in Calm's
        // stronger band it moves from step 1; the film holds 9.57 g at 10 s.
        // Model 18: 0xE5D4C472F6CA12D2. D-060 (ModelVersion 19), audited as the powered flight: it moves from step 1 and keeps
        // its course, 148.90 m up against 148.97, 0.58 m from model 18's end; the film holds 9.59 g at 10 s. To 0.5 m it
        // moves from step 1: 148.69 m up, 0.24 m from the class-4 step's end; the film holds 9.60 g.
        // Model 19: 0xB10D9A860D4431C0. D-064 (ModelVersion 23), audited as the powered flight (the kept test): it moves from
        // step 21 and keeps its course, 149.95 m up against 148.69, 1.82 m from model 19's end and within 3.3°; the film holds
        // 9.57 g at 10 s against 9.60.
        public const ulong RainPoweredHash = 0x789866FBE53F0C70UL;

        /// <summary>The merge's audit against unity/main (GoldenFlightTests.InStillAirTheGoldenFlightsAreModel15s): main's
        /// model 15 in still air over its 1656-byte layout, the powered and contact goldens (pinned from main's code with
        /// their air set still, a temporary edit), and its tumble's pin.</summary>
        public const ulong Model15StillPoweredHash = 0xABF47E6E55E48EB8UL, Model15StillContactHash = 0x557DEBE410FAEEA1UL, Model15TumbleHash = 0xFB841727FADDC7A7UL;

        /// <summary>Model 19's pins (D-060, before D-064), which WithoutD064TheGoldenFlightsAreModel19s holds over 1808 bytes.</summary>
        public const ulong Model19PoweredHash = 0xA1FF085091A019CDUL, Model19RainPoweredHash = 0xB10D9A860D4431C0UL,
            Model19ContactHash = 0x8C1E6B066F143A67UL, Model19TumbleHash = 0x2A0283BBFE59FD84UL;

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
        // Model 15 on unity/main: 0xC378E1EFFB9AEC93. On p7c-wind's branch, from model 5 (0xF1D0832353311083):
        // p7c-wind 1.4: re-recorded with ModelVersion 6 in the Calm field. Audited as the powered one: in still air
        // (StillAirContactHash) and in the steady 5 m/s (0x7F53C9713DCE5641) both models fly every record and event of
        // the 12 000 steps bit for bit over model 5's bytes. In Calm the flight moves from its first step (the field's
        // draws are new) and keeps its course: 5 impacts as model 5's, at rest 0.2519 m up on its 8 touching pairs.
        // p7c-wind 1.6: re-recorded with ModelVersion 7, audited as the powered one (model 6's drone flies model 6's pin).
        // With the new drag the flight moves from step 9, the first armed step's motion (position.x), and keeps its
        // course: 5 impacts as model 6's, at rest 0.2518 m up, 9.5 cm from model 6's resting place in Calm (1.8 mm in
        // still air); in a steady 5 m/s it drifts 0.8 m against 1.7 and touches down with 5 impacts against 7.
        // p7c-wind 1.5: re-recorded with ModelVersion 8, audited as the powered one (above); in Calm's band, 1.35 times
        // model 7's, the flight moves from step 1 and keeps its course: 5 impacts, at rest 0.2518 m up on 8 touching pairs.
        // p7c-wind 1.7: re-recorded with ModelVersion 9, audited as the powered one: over model 8's 1392 bytes it is
        // model 8's pin (Model8ContactHash) bit for bit; only the camera's air is new.
        // Model 10 on p7c-wind's branch: 0xBC4E9FA68E254625.
        // p7c-wind merged with unity/main: re-recorded with ModelVersion 17, audited as the powered flight (above), against
        // each parent bit for bit. In the Calm field on D-050's drone it moves from step 1 and keeps its course: 5 impacts,
        // no crash, at rest on 8 touching pairs at 0.2518 m, 50 mm from where it rose; 125 events.
        // Model 17: 0x4D5A4F57872D01F7. p7c-wind 1.5's re-grade (ModelVersion 18), audited as the powered flight. In Calm's
        // band, 1.32 times model 17's, it moves from step 1 and keeps its course: 5 impacts, no crash, at rest on 8 touching
        // pairs at 0.2518 m, 51 mm from where it rose; 125 events.
        // Model 18: 0x1152D64875230761. D-060 (ModelVersion 19), audited as the powered flight. It moves from step 1 (on its
        // legs the hubs, 0.288 m up, are now under d + z0 = 0.30 m, in the cover's still air) and keeps its course: within
        // 20 mm of model 18's path and 1.2 mrad, 5 impacts, no crash, at rest at the same 0.2518 m, 20 mm aside. To 0.5 m it
        // is the class-4 step's to step 290 (still air at the hubs on the floor) and moves from step 291, as they rise into
        // the field: within 2.7 mm and 1.3 mrad, 5 impacts, no crash, at rest at the same 0.2518 m, 0.8 mm aside.
        // Model 19: 0x8C1E6B066F143A67. D-064: re-recorded with ModelVersion 23 with the rotors' own wakes and the motors' new
        // spreads, and the re-ingested wash on published numbers (Dekker et al. 2023's loop, Healy et al. 2021's level) moving
        // the torque too. Audited: with the wash's model 19 numbers and its torque at the freestream's inflow (a temporary edit
        // in Propulsion and GroundEffectSpec, reverted) and without the wakes and the spreads, over the first 1808 bytes, every
        // record and event is model 19's. Against that same flight on the new wash (D-064's sources off) it moves from step 9,
        // the first armed step, and keeps its course: 5 impacts, no crash, at rest at the same 0.2518 m, 44 mm aside, the
        // heading 0.15° apart.
        public const ulong ContactHash = 0xFF836F92A12B4AB0UL;

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
        // Model 15 on unity/main: 0xFB841727FADDC7A7. p7c-wind merged with unity/main (ModelVersion 17): the 1808-byte record,
        // p7c-wind's bytes zero but the camera's air (the free body's own motion at the lens); over main's 1656 bytes it is
        // model 15's (the kept test), and models 13's and 14's on their files. It has no air: model 18 keeps it.
        // Model 19: 0x2A0283BBFE59FD84. D-064 (ModelVersion 23): the record's 32 wake bytes, zero (no rotors); over model 19's
        // 1808 bytes it is model 19's (the kept test).
        public const ulong TumbleHash = 0xDB5DEEA6285B6184UL;
        public const int TumbleSteps = 10000;

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

        /// <summary>unity/main's rotor in-plane drag, N per m/s at the reference speed (p7b-airframe 1.7: tuned so the model
        /// flies clip P's pitch at 13 m/s on D-050's silhouette), which p7c-wind 1.6's derived 0.044 replaces at the merge.</summary>
        public const string TunedHForce = "0.25";

        /// <summary>
        /// The drone with another rotor in-plane drag (drone.json's props.h_force_n_per_m_s, N per m/s at the reference speed):
        /// drone.json's text with the value replaced, for the merge's audit against unity/main's tuned 0.25.
        /// </summary>
        public static DroneSpec WithRotorDrag(string text, string hForce)
        {
            const string key = "\"h_force_n_per_m_s\": {\"value\": ";
            int at = text.IndexOf(key, System.StringComparison.Ordinal);
            if (at < 0) throw new System.InvalidOperationException("drone.json has no h_force_n_per_m_s");
            int from = at + key.Length, to = text.IndexOf(',', from);
            return DroneSpec.Parse(text.Substring(0, from) + hForce + text.Substring(to));
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
                CameraPosition = spec.CameraPosition,
                Contacts = spec.BuildContacts(spec.Nominal, materials, hash),
                Asymmetry = spec.BuildAsymmetry(),
                VortexRing = spec.BuildVortexRing(),
                GroundEffect = spec.BuildGroundEffect(),
                RotorWake = spec.BuildRotorWake(),
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
        /// layout, for an audit); onRecord sees each step's record before it is hashed (an audit's step-by-step comparison).</summary>
        public static ulong FlyPowered(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, bool contacts = true,
            bool asymmetry = true, bool ring = true, bool ground = true, bool flapping = true, bool modes = true, WindSetup? wind = null,
            Action<byte[]> onRecord = null, RainLevel rain = RainLevel.Sunny, bool wake = true, bool motors = true, int end = LogFormat.StepSize)
        {
            FlightSetup setup = PoweredSetup(spec);
            if (wind != null) setup.Wind = wind.Value;
            setup.Rain = rain;
            if (!modes) setup.Modes = null;
            if (!flapping) setup.Flapping = null;
            if (!contacts) setup.Contacts = null;
            if (!asymmetry) setup.Asymmetry = null;
            if (!ring) setup.VortexRing = null;
            if (!ground) setup.GroundEffect = null;
            WithoutD064(setup, wake, motors);
            core = new FlightCore(setup);
            var record = new byte[LogFormat.StepSize];
            var e = new byte[LogFormat.EventSize];
            ulong hash = 0xCBF29CE484222325UL;
            for (int k = 0; k < PoweredSteps; k++)
            {
                StickSample stick = PoweredStick(k);
                core.Advance(stick);
                LogFormat.EncodeStep(record, core.State, stick, core.Trace);
                onRecord?.Invoke(record);
                hash = Fnv(hash, record, 0, skipAt < end ? skipAt : end);
                hash = Fnv(hash, record, skipAt + skipBytes, end);
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
            bool ring = true, Action<FlightCore> after = null, bool ground = true, bool flapping = true, bool modes = true, WindSetup? wind = null,
            Action<byte[]> onRecord = null, bool wake = true, bool motors = true, int end = LogFormat.StepSize)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, mh, true, new Double3(0, stand, 0));
            setup.Wind = wind ?? WindSetup.Of(WindLevel.Calm);
            if (asymmetry) setup.Asymmetry = spec.BuildAsymmetry();
            if (ring) setup.VortexRing = spec.BuildVortexRing();
            if (ground) setup.GroundEffect = spec.BuildGroundEffect();
            if (flapping) setup.Flapping = spec.BuildFlapping();
            if (modes) setup.Modes = spec.BuildModes();
            setup.RotorWake = spec.BuildRotorWake();
            WithoutD064(setup, wake, motors);
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
                onRecord?.Invoke(record);
                hash = Fnv(hash, record, 0, skipAt < end ? skipAt : end);
                hash = Fnv(hash, record, skipAt + skipBytes, end);
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
        public static ulong FlyTumble(DroneSpec spec, out FlightCore core, out TumbleChecks checks, int skipAt = LogFormat.StepSize, int skipBytes = 0,
            int end = LogFormat.StepSize)
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
                hash = Fnv(hash, record, 0, skipAt < end ? skipAt : end);
                hash = Fnv(hash, record, skipAt + skipBytes, end);
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

        /// <summary>The record's length before D-064's rotor wakes (model 19's 1808 bytes).</summary>
        public const int Model19Size = 1808;

        // D-064's sources off for an audit against model 19: no rotor wake (wake), the motors' resistance, friction and inertia
        // undrawn (motors). The re-ingested wash's published numbers and its torque have no switch (their audit was a
        // temporary edit, GoldenFlights' notes).
        static void WithoutD064(FlightSetup setup, bool wake, bool motors)
        {
            if (!wake) setup.RotorWake = null;
            if (!motors && setup.Asymmetry != null) setup.Asymmetry.ResistanceSigma = setup.Asymmetry.FrictionSigma = setup.Asymmetry.InertiaSigma = 0;
        }

        static ulong Fnv(ulong h, byte[] bytes, int from, int to)
        {
            for (int i = from; i < to; i++) h = unchecked((h ^ bytes[i]) * 0x100000001B3UL);
            return h;
        }
    }
}
