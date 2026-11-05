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
    /// LogFormat.ModelVersion and is audited case by case (CLAUDE.md). Since model 36 (Betaflight's yaw I) every kept audit
    /// against an earlier model flies model 34's yaw I (yawI false, Model34YawI). Since model 40 (the physics integration 2
    /// with p7f-damage) every kept audit of the line's flies without props.blade (WithoutTheBlade, a freshly parsed spec), and
    /// every kept audit of p7f-damage's without the line's mechanisms (no soil, no area laws, model 34's yaw I).
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
        // Model 19: 0xA1FF085091A019CD. p7b-airframe 1.3d: re-recorded with ModelVersion 21 (the 1872-byte record, the rotors'
        // wakes in its setup), audited case by case: without the wake, over model 19's 1808 bytes, it is model 19's pin bit
        // for bit, record and events (a kept test, WithoutTheWakeEachGoldenIsModel19s). With it the flight moves from step 24,
        // the first with thrust in Windy's air, and keeps its course: armed, no crash, 156.18 m up against 147.90 and 8.31 m from
        // model 19's end on the open-loop script. The largest interference, 6.8 m/s, falls where its physics says: the first
        // second's fall at low thrust across the crosswind (the discs' net flow near 0, the wakes swept flat across the downwind
        // rotors, out of their rings) and the dive after 6.5 s (28 m/s nearly edgewise).
        // Model 19 on p7b-airframe--micro: 0xA1FF085091A019CD. D-064: re-recorded with ModelVersion 23 (the 1840-byte record:
        // model 19's 1808 bytes, then the rotors' wakes, 32) with the rotors' own wakes in its setup and the motors' resistance,
        // friction and inertia drawn, 0x5073BC74B404B3AA. Audited: without them, over model 19's 1808 bytes, every record and
        // event is model 19's (the kept test, WithoutD064TheGoldenFlightsAreModel19s; 193–300 m up no surface is in reach, so the
        // re-ingested wash's new numbers never act). With them it moves from step 21, the first armed step (the wakes wander from
        // the rotors' first turn; the motors' spreads alone move it from the same step), and keeps its course: armed, no crash,
        // 149.00 m up against 147.90 at 10 s, 1.16 m from model 19's end and within 1.9° of its attitude (the motors' spreads
        // alone: 0.83 m, 0.24°).
        // Model 24 (the physics integration): re-recorded on the 1904-byte record (model 19's 1808 bytes, the interference's 64,
        // the own wake's 32) with both in its setup. Audited against each parent with the other switched off: without the
        // interference, over the micro branch's 1840 bytes, it is that branch's 0x5073BC74B404B3AA bit for bit, record and events
        // (the kept test, WithoutTheInterferenceTheGoldenFlightsAreTheMicroBranchs); without D-064's sources (no own wake, the
        // motors' spreads at 0, the wash on model 19's numbers and its torque at the freestream's inflow) and with the
        // interference's stream back at wake.rotor (temporary switches, reverted), over the first 1872 bytes, the rotor-wake
        // branch's 0x2C064B70711ED0ED; without either, model 19's (the kept test). Against the micro branch's flight it moves
        // from step 25, against the rotor-wake branch's from step 21, the first armed step; it keeps its course: armed, no
        // crash, 157.26 m up against 149.00 and 156.18, 8.26 m and 1.17 m from their ends, within 7.8° and 1.8°. The largest
        // interference 7.04 m/s (6.8 on its branch); each rotor's own wake within 0.129 v_h.
        // Model 22 on p7b-airframe--power: 0xA1FF085091A019CD. D-066: re-recorded with ModelVersion 23 (the class's ESC, no current limit; the
        // published 1.4 A no-load current; the packs 0.88 kg and the electronics 0.24 kg). Audited: on model 22's drone
        // (WithModel22Drone) every golden is model 22's, every record and event (OnModel22sDroneEachGoldenIsModel22s, kept). On
        // model 23's the Windy flight moves from step 1 (the lighter body) and keeps its course on the open-loop script, which
        // the 90 g lighter drone climbs harder: armed, no crash, 153.53 m up against 147.90 at 10 s, 16.7 m from model 22's end,
        // the packs at 0.8941 of 0.9 against 0.8949.
        // Model 25: 0x67788361104F58BB. The power merge (ModelVersion 26): re-recorded with D-066's drone under the
        // interference, D-064 and 1.6c's spring (the exact build, no placement). Audited against each parent with the other
        // side off: on model 22's drone (WithModel22Drone) it is model 25's pin bit for bit, every record and event (the kept
        // test, OnModel22sDroneEachGoldenIsModel25s); without the interference and D-064's sources, over model 19's 1808
        // bytes, the power branch's 0xCC20DE164C858267 (the kept test, WithoutTheIntegrationTheGoldenFlightsAreThePowerBranchs).
        // It moves from step 1 against model 25 (the lighter body) and from step 21, the first armed step, against the power
        // branch's, and keeps its course: armed, no crash, 162.72 m up against 157.26 and 153.53 at 10 s on the open-loop
        // script (the lighter drone climbs harder, as on its branch), 20.1 m and 9.9 m from their ends, within 21° and 7.8°;
        // the packs at 0.8941 of 0.9 (the power branch's 0.8941).
        // Model 31 (p7d-tether 1.6): re-recorded on the 2008-byte record, the fiber's block zero; over model 28's 1904 bytes
        // it is model 28's pin, Model28PoweredHash, bit for bit (the kept test).
        // Model 34: 0x68F588AF6D73D471. Betaflight's yaw I (ModelVersion 36, reference-notes/rate-tracking.md §6): re-recorded
        // with yaw's Ki × 2.5 (pid_init.c pidInitConfig, integrated yaw off). Audited: on model 34's yaw I (Model34YawI) it is
        // model 34's pin, every record and event (the kept test OnModel34sYawIEachGoldenIsModel34s), and every older kept audit
        // holds on it. With the factor the record moves from step 1 (fc.motor_mix_range: the disarmed loop's yaw sum, the
        // motors stopped) and the pose from step 21, the first armed step. On the open-loop script the yaw step (3.5–4.5 s)
        // turns it further, so it ends 5.68 m from model 34's end and within 23.0° of its attitude, 160.72 m up against
        // 162.72; armed, no crash.
        // On p7f-damage (off model 26), model 26: 0x53F1D10489702031. p7f-damage 1.2 (ModelVersion 29): re-recorded with the blades as elements, each blade's
        // factory pitch offset (prop.blade_pitch, ±0.25°) and the flapping's blade inertia from props.blade (2.12e-5 kg·m², the
        // rod's 4.30e-5). Audited (the kept test, WithThePitchOffAndTheRodsInertiaEachGoldenIsModel26s): with the pitch draws
        // off and the rod's inertia, or without props.blade, it is model 26's pin bit for bit, every record and event. Each share
        // alone moves it from step 21, the first armed step: the inertia alone (0x460DEB6D2296C4A2; the flapping's softer
        // blade in Windy's air) by 5.51 m and 4.75° at most by 10 s on the open-loop script, 167.91 m up against 162.72; the
        // pitch alone (0x3351ADE2171E74D3; each rotor's thrust offset and its 1/rev) by 2.29 m and 2.06°, 162.36 m up; both by
        // 5.93 m and 4.69°, 167.60 m up, the pitch moving the inertia's flight by 1.79 m and 1.40°. Armed, no crash, no contact.
        // Model 29: 0xB999E5FDE8815DAC. p7f-damage 1.3 (ModelVersion 35): re-recorded on the 2288-byte record, its blade strikes' 384
        // bytes zero (no disc cuts anything 160–300 m up); over model 29's 1904 bytes model 29's pin, every record and event (the
        // kept test, OverModel29sLayoutEachGoldenIsModel29s).
        // Model 38: 0xF0A7027AE1CD5556 (the line), model 35: 0xC1053CA7562E61AC (p7f-damage). The physics integration 2 with
        // p7f-damage (ModelVersion 40; the code is the parents' together): re-recorded with p7f's blades as elements, their
        // pitch draws and the flapping's blade inertia from props.blade on the line's yaw I. Audited against each parent with
        // the other side off (the kept tests WithoutTheBladeEachGoldenIsModel38s and WithoutTheLineEachGoldenIsModel35s):
        // without props.blade, over the line's 2008 bytes, it is model 38's pin, every record and event, the strikes' block
        // zero; on model 34's yaw I, over p7f's record (the fiber's 104 bytes skipped), model 35's, the fiber's block zero (160–
        // 300 m up no soil, layer, line or cut acts, so the yaw I is the whole of the line's share). With the strikes off it is
        // the same bits (no disc cuts anything). It moves from step 21, the first armed step, against the line's (the blades),
        // and from step 2 against p7f's (the yaw I's disarmed loop; the pose from step 21), and keeps its course: armed, no
        // crash, no contact, 167.03 m up at 10 s against 160.72 and 167.60, 7.88 m and 4.34 m from their ends, within 8.7° and
        // 22.4° of their attitudes; the inertia alone (the pitch off) leaves it 1.95 m apart, the pitch alone (the rod's
        // inertia) 6.78 m.
        // On p7g-legs (off model 26), model 26: 0x53F1D10489702031. p7g-legs 1.1 (ModelVersion 37): re-recorded with the class's plastic legs (drone.json's
        // legs block: the PVC-U legs and skids 6.6 g lighter, each leg's spring derived from its section, 52.3 kN/m). Audited:
        // on model 26's legs (WithModel26Legs) it is model 26's pin bit for bit, every record and event (the kept test,
        // OnModel26sLegsEachGoldenIsModel26s). It moves from step 1 (the lighter body; 300 m up the spring never acts) and
        // keeps its course: armed, no crash, 162.65 m up against 162.72 at 10 s, 273 mm from model 26's end and within 0.33°
        // of its attitude; the packs at 0.8941 of 0.9 (the same).
        // Model 37: 0xCD0A523E1C9B94F8. p7g-legs 1.2 (ModelVersion 39): re-recorded on the 2072-byte record (the legs' state after
        // model 37's 1904 bytes). Audited: over model 37's 1904 bytes it is model 37's pin bit for bit, every record and event,
        // with the legs' side give and their law (it touches nothing 300 m up) and without them (the kept test,
        // WithoutTheFootsGiveEachGoldenIsModel37s); the legs' bytes hold four intact legs at full stiffness, their moment 0.
        // Model 39: 0x4907E4D57EB9EB98. p7g-legs 1.3 (ModelVersion 41): re-recorded on the 2296-byte record (each leg's side give,
        // jaws and hanging fold after model 39's 2072 bytes). Audited: over model 39's 2072 bytes it is model 39's pin bit for bit,
        // every record and event (the kept test, OverModel39sRecordTheFlightsThatTouchNothingAreModel39s); the new bytes hold each
        // leg's side give untouched (the tube's 268 N/m with the anchor in series), the jaws and folds 0.
        // Model 44: 0xDF1D314EF8AD6012 (the line), model 41: 0xCA9A9F3EF97C2118 (p7g-legs). The physics integration 2 with
        // p7g-legs (ModelVersion 46; the code is the parents' together): re-recorded on the 2784-byte record with p7g's PVC-U
        // legs. Audited against each parent with the other side off (the kept tests OnModel26sLegsEachGoldenIsModel44s and
        // WithoutTheLineEachGoldenIsModel41s): on model 26's legs, over the line's 2392 bytes, it is model 44's pin, every record
        // and event; without the line's mechanisms (no props.blade, model 34's yaw I), over p7g's record (the fiber's and the
        // strikes' 488 bytes skipped), model 41's, both blocks zero. It moves from step 1 against the line's (the lighter body:
        // the legs and skids 6.6 g lighter; 300 m up nothing touches) and keeps its course: armed, no crash, no contact, 167.13 m
        // up at 10 s against 167.03, 318 mm from its end and within 0.41° of its attitude; against p7g's from step 3 (the
        // yaw I's disarmed loop, the blades from step 21), 4.67 m and 0.80° from its end (162.65 m up). Every leg intact.
        // Model 46: 0xAE194731E398F0A4. p7g-legs 1.4 (ModelVersion 49): re-recorded on the 2816-byte record (the legs' lean after model 46's 2784
        // bytes). Audited: over model 46's 2784 bytes it is model 46's pin bit for bit, every record and event, the lean zero on every
        // record (it touches nothing; the kept test WithoutTheLeverEachGoldenIsModel48s, flightbench --golden).
        // Model 50 (the legs' lever on the line) keeps it: no line, no carve (WithoutTheFountainEachGoldenIsModel49s).
        // Model 50: 0x32CFEE62D75ECC24. p7f-damage 1.3b (ModelVersion 52): re-recorded on the 2944-byte record (the resting blades' block
        // after model 50's 2816 bytes). Audited: over its first 2816 bytes model 50's pin, every record and event, with the props at
        // rest on and off, the block zero on every record (GoldenFlightTests.WithoutARestingPropEachGoldenIsModel50s): no prop rests
        // on anything in it.
        // Model 58 (p7f-damage 1.5, the motors' bells): re-recorded on the 3040-byte record (the bells' block after 2944). Audited
        // case by case: over its first 2944 bytes model 57's pin (Model57PoweredHash), every record and event, with the bells on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel57s): no motor's band touches anything in it.
        // Model 63 (the physics integration 2 with the bells, 58 merged onto 60): model 58's pin, kept. Audited case by case: over
        // model 57's 2944 bytes model 60's pin (its 57's), every record and event, with the bells on and off, the block zero
        // (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel60s); without the skid's frame and the cuts model 58's pin over the
        // whole record (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).
        // Model 64 (the body over the fountain and D01's canister): re-recorded. Audited case by case: on model 63's drone.json (the
        // old coil, Model63Drone; the line out of the old rear cap, Model63Tether) with the fountain through the body it is model
        // 63's pin (Model63PoweredHash), every record and event (GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s, flightbench
        // --golden), so the canister is the whole move. It leaves 63's path at the first step (the canister's mass build: the CoM 7 mm lower and 4.6 mm forward,
        // I_yz +1.3 g·m², roll and pitch up 11–12 %) and keeps its climb: 161.54 m up at 10 s against 167.13 (its larger vertical
        // silhouette), 11.1 m from 63's end and 7.25° from its attitude; armed, no contact, no crash.
        // Model 66 (p7f-damage 1.6, the shafts and the bearings): re-recorded on the 3168-byte record (the shafts' block after 3040).
        // Audited case by case: over its first 3040 bytes model 63's pin (Model63PoweredHash), every record and event, with the shafts on and
        // off, the block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel63s): no shaft passes its yield and
        // no bearing its static rating in it.
        // Model 68 (the physics integration 2 with the shafts, 66 merged onto 65): re-recorded on the 3168-byte record. Audited case
        // by case: over its first 3040 bytes model 65's pin (Model65PoweredHash), every record and event, with the shafts on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel65s): no shaft passes its yield and no bearing
        // its static rating on the canister either; on model 63's drone through the body, with the 0-force hold and no blade root, model
        // 66's pin over the whole record (OnModel63sDroneEachGoldenIsModel66s).
        // Model 72 (p7f-damage 1.6's losses: the arms in their plane, the gyro's range): re-recorded on the 3344-byte record. Audited
        // case by case (GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s): with the clean sensors model 68's pin over its 3168
        // bytes, every record and event, the arms' block zero on every record; with them the flight moves from step 21 (the factory unbalance's 1/rev through the arms: twist at most 0.112°, arm load 0.0032, the gyro's peak 305.1 °/s, inside its 2000), ends 161.5429 m up.
        public const ulong PoweredHash = 0x9F6716D436D70F1CUL;

        // The powered golden in Calm air and Severe rain (p7c-wind 1.7, first recorded with ModelVersion 9): the drops'
        // drag and film on top of the Calm flight, which it leaves from step 1 (the drops' force at rest).
        // Model 10 on p7c-wind's branch: 0x6E28860F47C45BC1. p7c-wind merged with unity/main (ModelVersion 17), audited as the
        // powered flight: with main's terms off it is model 10's bit for bit; on the merge the film holds 9.50 g at 10 s.
        // Model 17: 0x5FA1F49E82CBC0D5. p7c-wind 1.5's re-grade (ModelVersion 18), audited as the powered flight: in Calm's
        // stronger band it moves from step 1; the film holds 9.57 g at 10 s.
        // Model 18: 0xE5D4C472F6CA12D2. D-060 (ModelVersion 19), audited as the powered flight: it moves from step 1 and keeps
        // its course, 148.90 m up against 148.97, 0.58 m from model 18's end; the film holds 9.59 g at 10 s. To 0.5 m it
        // moves from step 1: 148.69 m up, 0.24 m from the class-4 step's end; the film holds 9.60 g.
        // Model 19: 0xB10D9A860D4431C0. p7b-airframe 1.3d (ModelVersion 21), audited as the powered flight: without the wake it
        // is model 19's pin; with it it moves from step 21 and keeps its course, 157.38 m up against 148.69, 8.80 m from model
        // 19's end; the film holds 9.06 g (9.60), its flux on a path a few metres apart.
        // Model 19 on p7b-airframe--micro: D-064 (ModelVersion 23), 0x789866FBE53F0C70, audited as the powered flight (the kept
        // test): it moves from step 21 and keeps its course, 149.95 m up against 148.69, 1.82 m from model 19's end and within
        // 3.3°; the film holds 9.57 g at 10 s against 9.60.
        // Model 24: re-recorded with both, audited as the powered flight (the micro branch's 0x789866FBE53F0C70 without the
        // interference, the kept test; the rotor-wake branch's 0x113E39E1C2C3C447 without D-064 under the temporary switches;
        // model 19's without either). It moves from step 22 against the first and step 21 against the second and keeps its
        // course: 158.09 m up against 149.95 and 157.38, 8.16 m and 0.95 m from their ends, within 7.3° and 1.9°; the film holds
        // 9.11 g at 10 s (9.57 and 9.06).
        // Model 22 on p7b-airframe--power: 0xB10D9A860D4431C0. D-066 (ModelVersion 23), audited as the powered flight: it moves from step 1 and keeps
        // its course, 155.36 m up against 148.69; the film holds 10.28 g at 10 s against 9.60.
        // Model 25: 0x4D876EF3F328F9AD. The power merge (ModelVersion 26), audited as the powered flight (model 25's pin on
        // model 22's drone, the kept test; the power branch's 0xACF1742E51117659 without the integration's sources, the kept
        // test). It moves from step 1 and step 21 and keeps its course: 164.73 m up against 158.09 and 155.36, 19.8 m and 9.8
        // m from their ends, within 19° and 8.0°; the film holds 9.92 g at 10 s (9.11 and 10.28).
        // Model 31 (p7d-tether 1.6): re-recorded on the 2008-byte record, the fiber's block zero; over model 28's 1904 bytes
        // it is model 28's pin, Model28RainHash, bit for bit (the kept test).
        // Model 34: 0x947B8DD37255DDB4. Betaflight's yaw I (ModelVersion 36), audited as the powered flight: on model 34's yaw I
        // it is model 34's pin (the kept test). It moves from step 1 (the record) and step 21 (the pose), and ends 5.26 m from
        // model 34's end and within 23.0°, 162.62 m up against 164.73.
        // On p7f-damage (off model 26), model 26: 0x53B90D4307BF1534. p7f-damage 1.2 (ModelVersion 29), audited as the powered flight (model 26's pin under the
        // kept test's switches): from step 21 the inertia alone (0xD29E350E00C73BA6) moves it by 6.05 m and 5.37° by 10 s, 170.41 m
        // up against 164.73; the pitch alone (0xE9D26813D342FD33) by 1.67 m and 1.16°; both by 6.67 m and 5.71°, 170.63 m up;
        // the film holds 9.67 g at 10 s (9.92), its flux on a path a few metres apart.
        // Model 29: 0x8ABC28AF4D780FF7. p7f-damage 1.3 (ModelVersion 35): the 2288-byte record, the strikes' bytes zero; over model
        // 29's 1904 bytes model 29's pin (the kept test); the film 9.67 g as model 29.
        // Model 38: 0x470AA9C751367BC9 (the line), model 35: 0xA6DB29F570B76DF7 (p7f-damage). The physics integration 2 with
        // p7f-damage (ModelVersion 40), audited as the powered flight (both kept tests): without props.blade model 38's pin
        // over 2008 bytes, on model 34's yaw I model 35's over p7f's record, the strikes off the same bits. It moves from step
        // 21 and step 2 and keeps its course: 169.23 m up against 162.62 and 170.63, 8.09 m and 3.72 m from their ends, within
        // 9.2° and 22.2°; the film holds 9.79 g at 10 s (10.08 and 9.67).
        // On p7g-legs (off model 26), model 26: 0x53B90D4307BF1534. p7g-legs 1.1 (ModelVersion 37), audited as the powered flight (model 26's pin on model
        // 26's legs, the kept test): it moves from step 1 and keeps its course, 164.76 m up against 164.73, 204 mm from model
        // 26's end and within 0.21°; the film holds 9.93 g at 10 s (9.92).
        // Model 37: 0x15C2A0EF0C503D48. p7g-legs 1.2 (ModelVersion 39), audited as the powered flight: over 1904 bytes model 37's
        // pin with the law and without it; the legs' bytes intact.
        // Model 39: 0x5AE2CBA32972DD28. p7g-legs 1.3 (ModelVersion 41), audited as the powered flight: over model 39's 2072 bytes
        // model 39's pin (the kept test, OverModel39sRecordTheFlightsThatTouchNothingAreModel39s); it touches nothing in its 10 s.
        // Model 44: 0x48979FA015B4A693 (the line), model 41: 0x724D775C852F6B48 (p7g-legs). The physics integration 2 with
        // p7g-legs (ModelVersion 46), audited as the powered flight (both kept tests). It moves from step 1 against the line's
        // and keeps its course: 169.24 m up against 169.23, 148 mm from its end and within 0.29°; against p7g's 4.56 m apart
        // (164.76 m up); the film holds 9.80 g at 10 s (9.79 and 9.93).
        // Model 46: 0xED60FDA3FF1868A9. p7g-legs 1.4 (ModelVersion 49): re-recorded on the 2816-byte record (the legs' lean after model 46's 2784
        // bytes). Audited: over model 46's 2784 bytes it is model 46's pin bit for bit, every record and event, the lean zero on every
        // record (it touches nothing; the kept test WithoutTheLeverEachGoldenIsModel48s, flightbench --golden).
        // Model 50 (the legs' lever on the line) keeps it: no line, no carve (WithoutTheFountainEachGoldenIsModel49s).
        // Model 50: 0x19A2E68386BF1DA9. p7f-damage 1.3b (ModelVersion 52): re-recorded on the 2944-byte record (the resting blades' block
        // after model 50's 2816 bytes). Audited: over its first 2816 bytes model 50's pin, every record and event, with the props at
        // rest on and off, the block zero on every record (GoldenFlightTests.WithoutARestingPropEachGoldenIsModel50s): no prop rests
        // on anything in it.
        // Model 58 (p7f-damage 1.5, the motors' bells): re-recorded on the 3040-byte record (the bells' block after 2944). Audited
        // case by case: over its first 2944 bytes model 57's pin (Model57RainPoweredHash), every record and event, with the bells on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel57s): no motor's band touches anything in it.
        // Model 63 (the physics integration 2 with the bells, 58 merged onto 60): model 58's pin, kept. Audited case by case: over
        // model 57's 2944 bytes model 60's pin (its 57's), every record and event, with the bells on and off, the block zero
        // (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel60s); without the skid's frame and the cuts model 58's pin over the
        // whole record (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).
        // Model 64 (the body over the fountain and D01's canister): re-recorded. Audited case by case: on model 63's drone.json (the
        // old coil, Model63Drone; the line out of the old rear cap, Model63Tether) with the fountain through the body it is model
        // 63's pin (Model63RainPoweredHash), every record and event (GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s, flightbench
        // --golden), so the canister is the whole move. The same flight in Calm and Severe rain: from the first step, 161.79 m up at 10 s against 169.24, 12.2 m and 10.3°
        // from 63's end; the film 11.48 g against 9.80 (the canister's wetted surface, π·D·(L + D/2) of its body against the old coil's).
        // Model 66 (p7f-damage 1.6, the shafts and the bearings): re-recorded on the 3168-byte record (the shafts' block after 3040).
        // Audited case by case: over its first 3040 bytes model 63's pin (Model63RainPoweredHash), every record and event, with the shafts on and
        // off, the block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel63s): no shaft passes its yield and
        // no bearing its static rating in it.
        // Model 68 (the physics integration 2 with the shafts, 66 merged onto 65): re-recorded on the 3168-byte record. Audited case
        // by case: over its first 3040 bytes model 65's pin (Model65RainPoweredHash), every record and event, with the shafts on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel65s): no shaft passes its yield and no bearing
        // its static rating on the canister either; on model 63's drone through the body, with the 0-force hold and no blade root, model
        // 66's pin over the whole record (OnModel63sDroneEachGoldenIsModel66s).
        // Model 72 (p7f-damage 1.6's losses: the arms in their plane, the gyro's range): re-recorded on the 3344-byte record. Audited
        // case by case (GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s): with the clean sensors model 68's pin over its 3168
        // bytes, every record and event, the arms' block zero on every record; with them the flight moves from step 21 (twist at most 0.107°, arm load 0.0031, the gyro's peak 304.5 °/s), ends 161.7859 m up.
        public const ulong RainPoweredHash = 0x58C73FF5572B0E66UL;

        /// <summary>The merge's audit against unity/main (GoldenFlightTests.InStillAirTheGoldenFlightsAreModel15s): main's
        /// model 15 in still air over its 1656-byte layout, the powered golden (pinned from main's code with its air set still,
        /// a temporary edit), and its tumble's pin. The contact flight's model 15 pin, 0x557DEBE410FAEEA1, holds with the stick
        /// anchor at the contact point; with 1.6c's stick spring on the slip (p6-flight-core--landing's model 23, its kept
        /// test there) it is 0x78A919AAD7858813: 5 impacts, no crash, at rest at the same 0.2518 m, 0.38 mm aside. Since model
        /// 24 both need D-064's wash off, which has no switch, so both are temporary-edit audits (ContactHash's notes).</summary>
        public const ulong Model15StillPoweredHash = 0xABF47E6E55E48EB8UL, Model15TumbleHash = 0xFB841727FADDC7A7UL;

        /// <summary>The audit against model 28 (GoldenFlightTests.OverModel28sRecordEachGoldenIsModel28s, p7d-tether 1.6): model
        /// 28's 1904-byte record (the record less the fiber's 104-byte block at its end) and its pins, which every golden flown
        /// without the line keeps bit for bit, its fiber block zero on every record (the line's code changes no flight
        /// without it: the airframe modes' rebuild, the cable chain's spool and the flight's line are all off). Since model 34
        /// the contact and range goldens fly p7a-ground's soil and area laws, so they keep these pins without them.</summary>
        public const int Model28Layout = LogFormat.Model28StepSize;
        public const ulong Model28PoweredHash = 0x53F1D10489702031UL, Model28ContactHash = 0xAAEA31DBFBB13710UL, Model28RangeHash = 0xC79B60F487AEB8B6UL,
            Model28RainHash = 0x53B90D4307BF1534UL, Model28TumbleHash = 0xBCBD33E6B6A94844UL;

        /// <summary>The audits against model 19 (GoldenFlightTests.WithoutTheWakeEachGoldenIsModel19s, p7b-airframe 1.3d, and
        /// WithoutD064TheGoldenFlightsAreModel19s, D-064): model 19's 1808-byte record (the record less the interference's 64
        /// bytes and the own wake's 32 at its end) and its pins, which each golden flown without the interference, or without
        /// D-064's sources, keeps bit for bit.</summary>
        public const int Model19Layout = 1808;
        public const ulong Model19PoweredHash = 0xA1FF085091A019CDUL, Model19ContactHash = 0x8C1E6B066F143A67UL,
            Model19RainHash = 0xB10D9A860D4431C0UL, Model19TumbleHash = 0x2A0283BBFE59FD84UL;

        /// <summary>p7f-damage 1.3's audit against model 29 (GoldenFlightTests.OverModel29sLayoutEachGoldenIsModel29s): model 29's
        /// 1904-byte record (the record less the blade strikes' 384 bytes at its end) and its pins, which each golden whose discs
        /// cut nothing keeps bit for bit; every older kept audit hashes over it too.</summary>
        public const int Model29Layout = 1904;
        public const ulong Model29PoweredHash = 0xB999E5FDE8815DACUL, Model29RainPoweredHash = 0x8ABC28AF4D780FF7UL,
            Model29ContactHash = 0xFC78C0BDDEFA2725UL, Model29RangeHash = 0x8F038B8AAEC425E7UL, Model29TumbleHash = 0xBCBD33E6B6A94844UL;

        /// <summary>The physics integration 2 with p7f-damage (model 40), its audit against the line (GoldenFlightTests.
        /// WithoutTheBladeEachGoldenIsModel38s): the line's model 38 record, its first 2008 bytes (the record less the blade
        /// strikes' 384 at its end), and its pins, which every golden flown on a drone.json without props.blade keeps bit for
        /// bit, every record and event, the strikes' block zero on every record.</summary>
        public const int Model38Layout = LogFormat.Model38StepSize;
        public const ulong Model38PoweredHash = 0xF0A7027AE1CD5556UL, Model38RainPoweredHash = 0x470AA9C751367BC9UL,
            Model38ContactHash = 0xEA8C1FA9BBE6A19EUL, Model38RangeHash = 0x5133425038BAA44EUL, Model38TetherHash = 0x318B90FF21403E3BUL,
            Model38TumbleHash = 0x3FD62A50B3AF6C84UL;

        /// <summary>The physics integration 2 with p7g-legs (model 46), its audit against the line (GoldenFlightTests.
        /// OnModel26sLegsEachGoldenIsModel44s): the line's model 44 record, its first 2392 bytes (the record less the legs' 392 at
        /// its end), and its pins, which every golden flown on model 26's legs (WithModel26Legs: no side give, no failure law,
        /// the 10 kN/m spring) keeps bit for bit, every record and event.</summary>
        public const int Model44Layout = LogFormat.Model44StepSize;
        public const ulong Model44PoweredHash = 0xDF1D314EF8AD6012UL, Model44RainPoweredHash = 0x48979FA015B4A693UL,
            Model44ContactHash = 0xC3FBF4393BE70692UL, Model44RangeHash = 0xF6BBC45EC751E45FUL, Model44TetherHash = 0x40D840364F986978UL,
            Model44TumbleHash = 0x4C96AAB4C5453484UL;

        /// <summary>p7g-legs 1.4 (model 49), its audit (GoldenFlightTests.WithoutTheLeverEachGoldenIsModel48s): model 46's record,
        /// its first 2784 bytes (the record less the legs' lean at its end), and its pins, which every golden flown with the lever
        /// off (WithoutTheLever: ContactSpec.LegLeverLength 0, 1.3's linear softening) keeps bit for bit, every record and event,
        /// the lean zero.</summary>
        public const int Model46Layout = LogFormat.Model46StepSize;
        public const ulong Model46PoweredHash = 0xAE194731E398F0A4UL, Model46RainPoweredHash = 0xED60FDA3FF1868A9UL,
            Model46ContactHash = 0x1AAB934D74816482UL, Model46RangeHash = 0x5CB64C5900F44C2BUL, Model46TetherHash = 0xFE34B0491C406118UL,
            Model46TumbleHash = 0x24B0961BC802C1D3UL;

        /// <summary>p7f-damage 1.3b (model 52), its audit (GoldenFlightTests.WithoutARestingPropEachGoldenIsModel50s): model 50's
        /// record, its first 2816 bytes (the record less the resting blades' block at its end), and its pins, which every golden
        /// keeps bit for bit, every record and event, with the props at rest on (no golden's prop rests on anything) and off, the
        /// block zero.</summary>
        public const int Model50Layout = LogFormat.Model50StepSize;

        /// <summary>p7f-damage 1.5 (model 58), its audit (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel57s): model 57's record, its
        /// first 2944 bytes (the record less the bells' block at its end), and its pins, which every golden keeps bit for bit, every
        /// record and event, with the bells on (no golden's motor band touches anything) and off (ContactSpec.MotorBell), the block
        /// zero.</summary>
        public const int Model57Layout = LogFormat.Model57StepSize;
        public const ulong Model57PoweredHash = 0x559EE43EC0D39E24UL, Model57RainPoweredHash = 0x94E95E58184745A9UL,
            Model57ContactHash = 0x23F1CAAA06836829UL, Model57RangeHash = 0x05FA9D844690C592UL, Model57TetherHash = 0x194E6D87B0555391UL,
            Model57TumbleHash = 0x5BAE982A96C6A953UL;

        /// <summary>The physics integration 2 with the bells (model 63), its audits: model 60's pins (the skid's frame on the cuts)
        /// over model 57's 2944 bytes, which every golden keeps with the bells on and off (GoldenFlightTests.WithoutTheBellsEach
        /// GoldenIsModel60s; powered, rain and tumble are 57's, unmoved since), and p7f-damage 1.5's model 58 pins on the 3040-byte
        /// record, which every golden flown without the skid's frame and the cuts keeps (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).</summary>
        public const ulong Model60PoweredHash = Model57PoweredHash, Model60RainPoweredHash = Model57RainPoweredHash,
            Model60ContactHash = 0x443F8EB0FF6F0258UL, Model60RangeHash = 0x5AB70E46CC936BE0UL, Model60TetherHash = 0xA1C51B2FC6318AC2UL,
            Model60TumbleHash = Model57TumbleHash;
        public const ulong Model58PoweredHash = 0xFCDD4AE45B2094A4UL, Model58RainPoweredHash = 0x4A34B87D8D3CB8A9UL,
            Model58ContactHash = 0x35986B0BD8AEDDA9UL, Model58RangeHash = 0x1F8DB2FB55133D92UL, Model58TetherHash = 0x87866B2D06B0F191UL,
            Model58TumbleHash = 0xA07B024ABF6C7DD3UL;

        /// <summary>p7f-damage 1.6 (model 66), its audit: model 63's record, its first 3040 bytes (the record less the shafts' block at
        /// its end; model 63's pins are with the old coil below). Since model 68 (66 merged onto 65) the record the audits of 63, 64
        /// and 65 fly over.</summary>
        public const int Model63Layout = LogFormat.Model63StepSize;

        /// <summary>The goldens at model 65 (the physics integration 2 with the fiber's chain fixes, on the canister), over model 63's
        /// 3040 bytes, which every golden keeps bit for bit, every record and event, with the shafts on (no golden's shaft passes its
        /// yield, no bearing its static rating) and off (ContactSpec.MotorShaft), the shafts' block zero:
        /// GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel65s.</summary>
        public const ulong Model65PoweredHash = 0xB066BF9F2ADF9A09UL, Model65RainPoweredHash = 0x5557901256899D95UL,
            Model65ContactHash = 0x1CA80F585605010CUL, Model65RangeHash = 0xC58CD318318B665FUL, Model65TetherHash = 0xEC47127E19B6CF53UL,
            Model65TumbleHash = 0xA96AB25DE2391ED7UL;

        /// <summary>The goldens at model 66 (p7f-damage 1.6, the shafts and the bearings, on 63), over the whole 3168-byte record, which
        /// every golden keeps bit for bit, every record and event, on model 63's drone.json (the old coil, Model63Drone, its line out of
        /// the old rear cap: Model63Tether), the fountain through the body (FlightSetup.TetherFountainThroughBody), the known-wrong
        /// 0-force hold (FlightSetup.TetherZeroForceHold: 63's chain) and no blade root (ContactSpec.BladeRoot off):
        /// GoldenFlightTests.OnModel63sDroneEachGoldenIsModel66s.</summary>
        public const ulong Model66PoweredHash = 0xC4D5C624C30E66A4UL, Model66RainPoweredHash = 0x588D9ABDBC72E0A9UL,
            Model66ContactHash = 0xFFFBEA9CDA89C058UL, Model66RangeHash = 0x6814962E6FCE6760UL, Model66TetherHash = 0xDD00954720F14CC2UL,
            Model66TumbleHash = 0xDDA88F905E375BD3UL;

        /// <summary>p7f-damage 1.6's losses (model 72), its audit: model 68's record, its first 3168 bytes (the record less the arms'
        /// block at its end). Every golden flown over it flies the clean sensors (CleanSensors).</summary>
        public const int Model68Layout = LogFormat.Model68StepSize;

        /// <summary>The goldens at model 68 (the physics integration 2 with the shafts), which every golden keeps bit for bit over
        /// model 68's 3168 bytes, every record and event, with the clean sensors (the arms stiff in their plane, no gyro range):
        /// GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s.</summary>
        public const ulong Model68PoweredHash = 0x9A30E334BF1CE809UL, Model68RainPoweredHash = 0xF6F5495C2A88B995UL,
            Model68ContactHash = 0xDB9CD4249127070CUL, Model68RangeHash = 0xA5D9361E8D1C945FUL, Model68TetherHash = 0xA77A4DEF62161B53UL,
            Model68TumbleHash = 0x3280F96176B5ACD7UL;

        /// <summary>A flight's contacts without the blade root rule (model 64's fix, ContactSpec.BladeRoot): a blade passes at any
        /// radius, as at model 63.</summary>
        public static void WithoutTheBladeRoot(FlightSetup s)
        {
            if (s.Contacts != null) s.Contacts.BladeRoot = false;
        }

        /// <summary>A flight's contacts without the shafts and the bearings (p7f-damage 1.6's audit and known-wrong): a rigid shaft on
        /// rigid bearings, as at model 63.</summary>
        public static void WithoutTheShafts(FlightSetup s)
        {
            if (s.Contacts != null) s.Contacts.MotorShaft = false;
        }

        /// <summary>A flight's contacts without the motors' bells (p7f-damage 1.5's audit and known-wrong): the can a rigid part, as
        /// at model 57.</summary>
        public static void WithoutTheBells(FlightSetup s)
        {
            if (s.Contacts != null) s.Contacts.MotorBell = false;
        }
        public const ulong Model50PoweredHash = 0x32CFEE62D75ECC24UL, Model50RainPoweredHash = 0x19A2E68386BF1DA9UL,
            Model50ContactHash = 0xE4B8CB8346751E29UL, Model50RangeHash = 0xBADF8E0897A61D92UL, Model50TetherHash = 0x316A98D76AD4F03FUL,
            Model50TumbleHash = 0xE798498661DECB53UL;

        /// <summary>A flight's contacts without the props at rest (p7f-damage 1.3b's audit and known-wrong): a resting prop passes
        /// through what it meets, as at model 50.</summary>
        public static void WithoutTheRest(FlightSetup s) => s.Contacts.BladeRest = false;

        /// <summary>A flight's contacts without the lever (p7g-legs 1.4's audit and known-wrong): 1.3's linear softening, k_t·(1 −
        /// P/P_sway) on the step before's axial load, never below 0. Since model 54 without the skid's frame too (WithoutTheFrame):
        /// the legs before the lever had none, so the audits that fly it are the older models' legs.</summary>
        public static readonly Action<FlightSetup> WithoutTheLever = s =>
        {
            if (s.Contacts == null) return;
            s.Contacts = WithoutTheFrameContacts(s.Contacts);
            s.Contacts.LegLeverLength = 0;
        };

        /// <summary>A flight's contacts as model 50's legs (the hanging half and the stub's audit and known-wrong, model 51): a
        /// hanging half turned by its cantilever's share alone (ContactSpec.HangingGive off), and a broken leg's stub on the free
        /// tube's spring (LegDamageSpec.StubStiffness 0). Every golden flies with it as with the fix: none parts a leg.</summary>
        public static readonly Action<FlightSetup> WithoutTheHangingGive = s =>
        {
            if (s.Contacts == null) return;
            s.Contacts = s.Contacts.Clone();
            s.Contacts.HangingGive = false;
            if (s.Contacts.Legs != null) s.Contacts.Legs.StubStiffness = s.Contacts.Legs.StubStiffnessUp = 0;
        };

        /// <summary>A flight's contacts without the skid's frame (p7g-legs, model 54's audit and known-wrong): one value both ways,
        /// every leg the cantilever along the skid as across it (ContactSpec's frame give, sway load and lever 0, LegDamageSpec's
        /// frame shares 0), model 51's legs.</summary>
        public static readonly Action<FlightSetup> WithoutTheFrame = s =>
        {
            if (s.Contacts == null) return;
            s.Contacts = WithoutTheFrameContacts(s.Contacts);
        };

        public static ContactSpec WithoutTheFrameContacts(ContactSpec c)
        {
            c = c.Clone();
            c.LegFrameSideStiffness = c.LegFrameSwayLoad = c.LegFrameLeverLength = 0;
            if (c.Legs != null) c.Legs.FrameTopShare = c.Legs.FrameJointShare = c.Legs.FrameSelfStiffness = 0;
            return c;
        }

        /// <summary>Its audit against p7g-legs (GoldenFlightTests.WithoutTheLineEachGoldenIsModel41s): p7g-legs' model 41 record is
        /// the record less the fiber's 104 and the blade strikes' 384 bytes at Model28Layout (Model41Skip), and its pins, which
        /// every golden flown without the line's mechanisms (no surfaces.json, no area laws, no line, no props.blade, model 34's
        /// yaw I) keeps bit for bit, every record and event.</summary>
        public const int Model41Skip = LogFormat.Model44StepSize - LogFormat.Model28StepSize;
        public const ulong Model41PoweredHash = 0xCA9A9F3EF97C2118UL, Model41RainPoweredHash = 0x724D775C852F6B48UL,
            Model41ContactHash = 0xB7391628EDE527ADUL, Model41TumbleHash = 0xA6914F6702FE98F3UL, Model41RangeHash = 0x1FB746560EDFD45AUL;

        /// <summary>Its audit against p7f-damage (GoldenFlightTests.WithoutTheLineEachGoldenIsModel35s): p7f-damage's model 35
        /// record is the record less the fiber's 104 bytes at Model28Layout (Model35Skip), and its pins, which every golden flown
        /// without the line's mechanisms (no surfaces.json, no area laws, no line, model 34's yaw I) keeps bit for bit, every
        /// record and event, the fiber's block zero on every record.</summary>
        public const int Model35Skip = LogFormat.Model38StepSize - LogFormat.Model28StepSize;
        public const ulong Model35PoweredHash = 0xC1053CA7562E61ACUL, Model35RainPoweredHash = 0xA6DB29F570B76DF7UL,
            Model35ContactHash = 0x6A44DA1874A49725UL, Model35RangeHash = 0x00CBE79581238DE7UL, Model35TumbleHash = 0x9931AD0B7F279044UL;

        /// <summary>The integration's audit against p7b-airframe--micro (GoldenFlightTests.WithoutTheInterferenceTheGoldenFlightsAreTheMicroBranchs):
        /// that branch's model 23 pins over its 1840-byte record (model 19's 1808 bytes, then the own wake's 32), which each golden
        /// flown without the interference keeps bit for bit. Since model 25 the contact flight's is with 1.6c's stick spring
        /// (0xDFC291AE4872A3B7); the branch's own 0xFF836F92A12B4AB0 holds with the stick anchor at the contact point (a
        /// temporary edit, reverted; ContactHash's notes).</summary>
        public const ulong MicroPoweredHash = 0x5073BC74B404B3AAUL, MicroContactHash = 0xDFC291AE4872A3B7UL, MicroRainHash = 0x789866FBE53F0C70UL,
            MicroTumbleHash = 0xDB5DEEA6285B6184UL;

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
        // Model 19: 0x8C1E6B066F143A67. p7b-airframe 1.3d (ModelVersion 21), audited as the powered flight: without the wake it
        // is model 19's pin; with it it moves from step 9, the first armed step, and keeps its course: Calm's hover skews the
        // wakes little (the largest interference 0.12 m/s), 5 impacts, no crash, at rest at the same 0.2518 m, 3 mm from model
        // 19's resting place.
        // Model 19 on p7b-airframe--micro: D-064, re-recorded with ModelVersion 23 with the rotors' own wakes and the motors'
        // new spreads, and the re-ingested wash on published numbers (Dekker et al. 2023's loop, Healy et al. 2021's level)
        // moving the torque too, 0xFF836F92A12B4AB0. Audited: with the wash's model 19 numbers and its torque at the freestream's
        // inflow (a temporary edit in Propulsion and GroundEffectSpec, reverted) and without the wakes and the spreads, over the
        // first 1808 bytes, every record and event is model 19's. Against that same flight on the new wash (D-064's sources off)
        // it moves from step 9, the first armed step, and keeps its course: 5 impacts, no crash, at rest at the same 0.2518 m,
        // 44 mm aside, the heading 0.15° apart.
        // Model 24: re-recorded with both, audited as the powered flight: without the interference the micro branch's
        // 0xFF836F92A12B4AB0 (the kept test); without D-064's sources under the temporary switches the rotor-wake branch's
        // 0x67D93616B88664A4, and with neither model 19's 0x8C1E6B066F143A67 (and, on main's drag in still air over main's
        // layout, model 15's 0x557DEBE410FAEEA1). It moves from step 10 against the first and step 9, the first armed step,
        // against the second, and keeps its course: 5 impacts, no crash, at rest at the same 0.2518 m on 8 pairs, 3.3 mm and
        // 25 mm from their resting places, within 0.16° and 1.5° (the held bounce); the largest interference 0.125 m/s.
        // Model 22 on p6-flight-core--landing: 0x8C1E6B066F143A67. 1.6c's stick spring on the slip (ModelVersion 23, D-067):
        // with the old anchor at the contact point (a temporary edit, reverted) it is model 22's pin bit for bit; with the new,
        // 0x5B8655B7F91E4D75: 5 impacts, no crash, at rest at the same 0.2518 m, 0.24 mm aside: on its skids nothing rolls,
        // so the bits move only by the rule's arithmetic.
        // Model 24: 0xA93D2534E875790F. The landing's merge (ModelVersion 25): re-recorded with 1.6c's stick spring under the
        // interference and D-064. Audited against each parent with the other side off (temporary edits, reverted): with the
        // stick anchor back at the contact point it is model 24's pin bit for bit; without the interference and D-064's
        // sources (the wash on model 19's numbers and its torque at the freestream's inflow), over the first 1808 bytes, the
        // landing branch's 0x5B8655B7F91E4D75; with neither model 22's 0x8C1E6B066F143A67, and on main's drag in still air
        // over main's layout the landing's 0x78A919AAD7858813 and, with the old anchor, model 15's 0x557DEBE410FAEEA1. It
        // moves from step 3 against model 24 (the skids stand on the floor from the first step: the rule's arithmetic) and
        // from step 9, the first armed step, against the landing's, and keeps its course: 5 impacts, no crash, at rest at the
        // same 0.2518 m on 8 pairs, 0.64 mm and 26.9 mm aside, within 0.66 mm and 0.013° of model 24's path and 31 mm and
        // 1.4° of the landing's (the held bounce).
        // Model 22 on p7b-airframe--power: 0x8C1E6B066F143A67. D-066 (ModelVersion 23), audited as the powered flight: it keeps its course, 5 impacts,
        // no crash, at rest on 8 contacts at 0.2519 m against 0.2518, 18 mm aside.
        // Model 25: 0x0F21FF2A28BA0E16. The power merge (ModelVersion 26): re-recorded with D-066's drone. Audited: on model
        // 22's drone it is model 25's pin bit for bit (the kept test); without the interference and D-064's sources, with the
        // wash on model 19's numbers, its torque at the freestream's inflow and the stick anchor at the contact point
        // (temporary edits, reverted), over 1808 bytes, the power branch's 0xAF0C71C9193B4326. It moves from step 1 against
        // model 25 (the lighter body) and from step 3 against the power branch's (1.6c's spring on the standing skids), and
        // keeps its course: 5 impacts, no crash, at rest at 0.2519 m on 8 pairs (0.2518 and 0.2519), 59 mm and 42 mm aside,
        // within 117 mm and 1.5° of model 25's path and 49 mm and 1.5° of the power branch's (the held bounce).
        // Model 18 on p7a-ground: 0x1152D64875230761. p7a-ground 1.10: re-recorded with ModelVersion 23, the floor's belt_bare now
        // surfaces.json's soil (the setup carries the table; the record's layout is model 18's). Audited: without the table
        // the flight is model 18's, every record and event (WithoutTheSoilTheContactGoldenIsModel18s, restated at model 27). With it the
        // flight moves from step 2, the first whose forces take the soil: the skids sink into belt_bare from the start.
        // It keeps its course: the same climb (0.7424 m at 1 s against 0.7420), the held bounce lower on the soil (its top
        // 0.4094 m against 0.4511), 3 impacts against 5, at most 53.8 mm from model 18's path, no crash, at rest on its
        // skids 3.9 mm deeper (0.2479 m against 0.2518; the half-skids 3.5–4.3 mm in, 5.3–5.6 mm at the deepest); 91
        // events against 125.
        // Model 26: 0xAAEA31DBFBB13710. The ground merge (ModelVersion 27): re-recorded with p7a-ground 1.10's soil under the
        // physics line's model 26 (the floor's belt_bare as surfaces.json's soil; D-066's drone, the interference, D-064 and
        // 1.6c's stick spring). Audited against each parent with the other side off: without surfaces.json it is model 26's
        // pin, every record and event (WithoutTheSoilTheContactGoldenIsModel26s, kept); on model 22's drone without the
        // interference and D-064's sources, over 1808 bytes, with the wash on model 19's numbers and its torque at the
        // freestream's inflow, the stick anchor at the contact point and D-060's field back at z0 0.03 m, σ_u/U 0.135 and the
        // 1 m cut (temporary edits, reverted), p7a-ground's model 23 pin 0xA248F9B82CF85639, and without the table model 18's
        // 0x1152D64875230761. It moves from step 2 against model 26 (the first whose forces take the soil) and from step 1
        // against p7a-ground's (the lighter body), and keeps its course: the climb as model 26's (0.8202 m at 1 s against
        // 0.8238), no crash; the first touchdown, at 1.55 m/s, sinks the deepest node 5.52 mm (p7a-ground's 5.69 at 1.71 m/s);
        // D-064's own wake gives the held bounce a fourth impact (4 against p7a-ground's 3; 5 on model 26's rigid floor), which
        // sets the skids down 24.6 mm from that touchdown, past half a groove's width, on fresh soil (p7a-ground's 5.2 mm,
        // inside its grooves; without the own wake 3 impacts and 7.4 mm), so it rests on its skids 2.0–2.3 mm in (the grooves
        // 2.8–3.3 mm at the deepest) at 0.2497 m against p7a-ground's 0.2479 and model 26's 0.2519, 28 mm from both; within
        // 47 mm and 1.5° of model 26's path and 105 mm and 1.2° of p7a-ground's (the held bounce); 92 events against 125 and 91.
        // Model 31 on p7d-tether (1.6; 0x89EEA4CA37C2ACD0): re-recorded on the 2008-byte record, the fiber's block zero; over model 28's 1904 bytes
        // it is model 28's pin, Model28ContactHash, bit for bit (the kept test).
        // Model 33 on p7a-ground: 0x37C4BFF09B9C22E2 (its model 27's, carried to 33: 1.13's pull acts only in rain and 1.12's
        // layers touch nothing on the floor).
        // The physics integration 2 (ModelVersion 34, p7c-wind 30, p7a-ground 33 and p7d-tether 31 merged onto 26): the ground's
        // soil on the line's 2008-byte record. Audited against each parent with the other side off (GoldenFlightTests' kept
        // audits): over model 28's 1904 bytes it is p7a-ground's model 33 pin, every record and event, the fiber's block zero on
        // every record (OverModel28sRecordEachGoldenIsModel33s); without surfaces.json it is p7d-tether's model 31 pin over the
        // whole record (WithoutTheGroundTheGoldenFlightsAreModel31s) and model 26's over 1904 bytes
        // (WithoutTheSoilTheContactGoldenIsModel26s). The floor has no cut, and its Calm field calls no Atan2. So it is model
        // 33's flight step for step; against p7d-tether's it moves from step 2 (the soil), 4 impacts against 5, at rest at
        // 0.2497 m against 0.2519, 28 mm aside, within 47 mm and 1.5 deg of its path; 92 events against 125.
        // Model 34: 0xE4C64497E36A0322. Betaflight's yaw I (ModelVersion 36), audited as the powered flight: on model 34's yaw I
        // it is model 34's pin, every record and event (the kept test), and every older kept audit holds on it. It moves from
        // step 10, the first with the rotors turning, and keeps its course: within 0.5 mm and 0.07° of model 34's path, 4
        // impacts as model 34's, at rest at the same 0.2497 m on 4 pairs, 0.2 mm aside.
        // On p7f-damage (off model 26), model 26: 0xAAEA31DBFBB13710. p7f-damage 1.2 (ModelVersion 29), audited as the powered flight (model 26's pin under the
        // kept test's switches). It moves from step 9, the first armed step, under either share: the inertia alone
        // (0x448A9D8E4A535411; Calm's hover has little edgewise flow) by at most 1.0 mm and 0.06°; the pitch alone
        // (0xD7986813EEECBEA6) by 17.2 mm and 0.86° (step 9467, the held bounce); both by 18.0 mm and 0.86°. It keeps its course: 5
        // impacts, no crash, at rest at the same 0.2519 m on 8 pairs, 17.6 mm from model 26's resting place.
        // Model 29: 0xFC78C0BDDEFA2725. p7f-damage 1.3 (ModelVersion 35): re-recorded on the 2288-byte record, its blade strikes' 384
        // bytes zero: no disc cuts anything on the floor (0 passes), so over model 29's 1904 bytes it is model 29's pin, every record
        // and event (the kept test, OverModel29sLayoutEachGoldenIsModel29s), the strikes on or off; 5 impacts, no crash, at rest
        // 0.2519 m up on 8 pairs, as model 29.
        // Model 38: 0xEA8C1FA9BBE6A19E (the line), model 35: 0x6A44DA1874A49725 (p7f-damage). The physics integration 2 with
        // p7f-damage (ModelVersion 40): p7f's blades on the floor's belt_bare soil. Audited against each parent with the other
        // side off (both kept tests): without props.blade, over 2008 bytes, model 38's pin, every record and event, the
        // strikes' block zero; without surfaces.json on model 34's yaw I, over p7f's record, model 35's, the fiber's block
        // zero. No disc cuts the floor or its soil (0 passes; the strikes off give the same bits). It moves from step 9, the
        // first armed step, against the line's (the blades' pitch draws: the inertia alone leaves it 1.6 mm from it) and keeps
        // its course: within 12.4 mm and 0.77° of the line's path, 4 impacts as the line's, at rest at the same 0.2497 m on 4
        // pairs, 9.6 mm aside. Against p7f's it moves from step 2 (the soil; the yaw I alone leaves it 0.1 mm apart): within
        // 47.1 mm and 1.9° of its path, 4 impacts against 5, at rest at 0.2497 m against 0.2519, 36.8 mm aside.
        // On p7g-legs (off model 26), model 26: 0xAAEA31DBFBB13710. p7g-legs 1.1 (ModelVersion 37): re-recorded with the plastic legs. Audited: on model
        // 26's legs it is model 26's pin bit for bit (the kept test). It moves from step 1 (the lighter body on the derived
        // 52.3 kN/m spring and its 103 N·s/m damper, the skids standing on the floor from the first step) and keeps its
        // course: 6 impacts against 5, no crash, at rest at 0.2528 m against 0.2519 (the stiffer
        // spring sinks 0.24 mm under a leg's share, the assumed 10 kN/m 1.24 mm), 74 mm aside, within 80 mm and 1.8° of model
        // 26's path (the held bounce).
        // Model 37: 0xA3B92A5D52CFE546. p7g-legs 1.2 (ModelVersion 39): re-recorded with the legs' side give and their failure
        // law. Audited: without them (WithModel37Legs), over 1904 bytes, it is model 37's pin bit for bit (the kept test). With
        // them it moves from step 3 (the skids standing on the floor: their springs across the legs, 262 N/m with the anchor in
        // series, from the first steps) and keeps its course: 8 impacts (6), no crash, at rest at the same 0.2528 m on 8 pairs,
        // 163.6 mm from model 37's resting place, within 165 mm and 6.5° of its path (the held bounce, where the soft legs sway
        // at the tube's own ζ 0.015). The held bounce comes down spinning at 4.3 rad/s about z, so leg_fr's corner touches at 2.6
        // m/s and its tube bows past the buckling load: it cracks and keeps a set of 0.065° (1.90 N·m past 1.69, step 8916); the
        // other three stay intact. On the legs' assumed ζ 0.2 carried across (a temporary edit) the side damper alone cracked all
        // four at that touch.
        // Model 39: 0xFCA3F1141457625C. p7g-legs 1.3 (ModelVersion 41): re-recorded with a damaged leg changing the drone and the
        // side give's parts (the tube's give less P·Δ, the jaws held by their friction, the crack flown). Audited: model 37's pin
        // without the legs' law over 1904 bytes (the kept test, as before). It moves from step 3 (the skids standing: the side
        // give 265 N/m with the jaws held against 1.2's 260 with their bed) and keeps its course: 8 impacts (8), no crash, at
        // rest at the same 0.2528 m on 8 pairs, 169.8 mm from model 37's resting place (39: 163.6) and 1.06° from its attitude.
        // The held bounce's spinning 2.6 m/s corner touch now loads every leg past its sway load, and P·δ at the clamps cracks
        // all four with sets of 0.039°, 0.121°, 0.003° and 0.002° (39: leg_fr alone, 0.065°); none parts, so no piece drops.
        // Model 44: 0xC3FBF4393BE70692 (the line), model 41: 0xB7391628EDE527AD (p7g-legs). The physics integration 2 with
        // p7g-legs (ModelVersion 46): p7g's PVC-U legs, their side give and their law on the floor's belt_bare soil (the soil's
        // leg coupling, ContactModel.SoilTrial). Audited against each parent with the other side off (both kept tests): on
        // model 26's legs, over 2392 bytes, model 44's pin, every record and event; without surfaces.json, props.blade and the
        // line's yaw I, over p7g's record, model 41's. Its shares (the switches): on p7g's legs without their side give and law
        // (WithModel37Legs) it leaves the line's flight from step 1 (the lighter body on the stiffer spring); the side give moves
        // it from step 3 (the skids standing in the soil: the groove's anchor in series with the tube's give), by at most 98.5 mm
        // and 1.82°; with the law off (Legs null) every byte of the line's 2392 is the same (no leg passes a capacity), and so
        // with the strikes off (no disc cuts the floor). It keeps its course: 3 impacts against the line's 4, no crash, at rest at
        // 0.2494 m against 0.2497 on 4 pairs (the soil's half-skids), 79.7 mm from the line's resting place and within 97.1 mm
        // and 1.66° of its path, the deepest nodes 2.9–5.7 mm into the soil, the side give at rest 201 N/m; against p7g's (8
        // impacts on the rigid floor, its spinning corner touch cracking all four) 11.8 mm from its rest. Every leg intact.
        // Model 46: 0x1AAB934D74816482. p7g-legs 1.4 (ModelVersion 49): re-recorded with the leg across its tube as a lever on the 2816-byte
        // record. Audited: with the lever off (WithoutTheLever, 1.3's softening) model 46's pin over its 2784 bytes, every record and event,
        // the lean zero (the kept test WithoutTheLeverEachGoldenIsModel48s). With it, it moves from step 3 (the standing skids' points on
        // their levers) and keeps its course: within 1.9 mm and 0.85° of model 46's path, 3 impacts as 46's, at rest 0.2495 m up against
        // 0.2494 on 4 pairs, 1.2 mm from 46's rest; the largest lean 9.6 mm; every leg intact.
        // Model 50 (the legs' lever on the line) keeps it: no line and no disc in the soil (0 blade passes; the tip slide off, the
        // same bits), and with the lever off it is model 48's, which kept model 46's (WithoutTheLeverEachGoldenIsModel48s).
        // Model 50: 0xE4B8CB8346751E29. p7f-damage 1.3b (ModelVersion 52): re-recorded on the 2944-byte record (the resting blades' block
        // after model 50's 2816 bytes). Audited: over its first 2816 bytes model 50's pin, every record and event, with the props at
        // rest on and off, the block zero on every record (GoldenFlightTests.WithoutARestingPropEachGoldenIsModel50s): no prop rests
        // on anything in it.
        // The skid's frame fore and aft (ModelVersion 54, off model 51's 0xE4B8CB8346751E29, on the 2816-byte record): re-recorded as
        // 0xB40293EBA12A4258 (Model54ContactHash) with each leg the skid's frame
        // along the skid (784 N/m at the foot against the cantilever's 268). Audited: without the frame (WithoutTheFrame, one value
        // both ways) model 51's pin, every record and event (model 54's WithoutTheFrameEachGoldenIsModel51s; since 60 WithoutTheHangingGiveEachGoldenIsModel50s). With it, it moves
        // from step 3 (the standing skids' points along the skid on the frame's give) and keeps its course: within 17.7 mm and
        // 0.57° of model 51's path, 3 impacts as 51's, at rest 0.2494 m up against 0.2495 on 4 pairs, 13.9 mm from 51's rest; the
        // largest lean 8.3 mm; every leg intact.
        // Model 59: 0x23F1CAAA06836829 (the props at rest's 52, kept through 55, 57 and 59: no line, no prop at rest). The physics
        // integration 2 with the skid's frame (ModelVersion 60, 54 merged onto 59): re-recorded. Audited case by case: without the frame
        // (WithoutTheFrame) it is model 59's pin over the whole record, every record and event (GoldenFlightTests.
        // WithoutTheFrameEachGoldenIsModel59s, kept; flightbench's line), and over model 50's 2816 bytes with the props at rest on
        // and off it is the frame's model 54 pin, every record and event, the resting blades' block zero (WithoutTheCutsTheRestAnd
        // TheFiberContactsEachGoldenIsModel54s, kept). With the frame it is 54's flight (no line, no prop at rest): from step 3,
        // within 17.70 mm and 0.567° of model 59's path, 3 impacts as 59's, 65 events against 71, at rest 0.2494 m up against
        // 0.2495 on 4 pairs, 13.85 mm from 59's rest; the largest lean 8.26 mm (9.56); every leg intact.
        // Model 58 (p7f-damage 1.5, the motors' bells): re-recorded on the 3040-byte record (the bells' block after 2944). Audited
        // case by case: over its first 2944 bytes model 57's pin (Model57ContactHash), every record and event, with the bells on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel57s): no motor's band touches anything in it.
        // Model 63 (the physics integration 2 with the bells, 58 merged onto 60): re-recorded on the 3040-byte record, 60's flight
        // and the bells' zero block. Audited case by case: over model 57's 2944 bytes model 60's pin (Model60ContactHash), every record
        // and event, with the bells on and off, the block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel60s):
        // no motor's band touches anything in it; without the skid's frame model 58's pin (Model58ContactHash) over the whole record
        // (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).
        // Model 64 (the body over the fountain and D01's canister): re-recorded. Audited case by case: on model 63's drone.json (the
        // old coil, Model63Drone; the line out of the old rear cap, Model63Tether) with the fountain through the body it is model
        // 63's pin (Model63ContactHash), every record and event (GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s, flightbench
        // --golden), so the canister is the whole move. It leaves 63's path at step 2 and keeps its course: 3 impacts as 63's, at rest 0.2495 m up against 0.2494 on 4 pairs,
        // 10.7 mm and 0.29° from 63's rest.
        // Model 66 (p7f-damage 1.6, the shafts and the bearings): re-recorded on the 3168-byte record (the shafts' block after 3040).
        // Audited case by case: over its first 3040 bytes model 63's pin (Model63ContactHash), every record and event, with the shafts on and
        // off, the block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel63s): no shaft passes its yield and
        // no bearing its static rating in it.
        // Model 68 (the physics integration 2 with the shafts, 66 merged onto 65): re-recorded on the 3168-byte record. Audited case
        // by case: over its first 3040 bytes model 65's pin (Model65ContactHash), every record and event, with the shafts on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel65s): no shaft passes its yield and no bearing
        // its static rating on the canister either; on model 63's drone through the body, with the 0-force hold and no blade root, model
        // 66's pin over the whole record (OnModel63sDroneEachGoldenIsModel66s).
        // Model 72 (p7f-damage 1.6's losses: the arms in their plane, the gyro's range): re-recorded on the 3344-byte record. Audited
        // case by case (GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s): with the clean sensors model 68's pin over its 3168
        // bytes, every record and event, the arms' block zero on every record; with them the flight moves from step 9 (twist at most 0.048°, arm load 0.0014, the gyro's peak 161.7 °/s), the same 3 impacts, at rest 0.2495 m up on 4 pairs.
        public const ulong ContactHash = 0x63DB5A624E7CD561UL;

        /// <summary>The pins at model 59 (the cuts on the props at rest), which every golden flown without the skid's frame keeps
        /// bit for bit over the whole record, every record and event (GoldenFlightTests.WithoutTheFrameEachGoldenIsModel59s).</summary>
        public const ulong Model59ContactHash = 0x23F1CAAA06836829UL, Model59RangeHash = 0x05FA9D844690C592UL,
            Model59TetherHash = 0x1553E92C7C7425BCUL;

        /// <summary>The skid's frame's pins (p7g-legs on the line, model 54, off 51) over model 50's 2816 bytes, which every golden
        /// flown without the cuts (FlightSetup.TetherWithoutCuts), the props at rest (ContactSpec.BladeRest off) and the fiber's
        /// contacts (FlightSetup.TetherWithoutContacts) keeps bit for bit, every record and event (GoldenFlightTests.
        /// WithoutTheCutsTheRestAndTheFiberContactsEachGoldenIsModel54s). Model 51's (the hanging half and the stub) are model
        /// 50's (Model50*Hash), which the flights without the frame too keep.</summary>
        public const ulong Model54ContactHash = 0xB40293EBA12A4258UL, Model54RangeHash = 0x0C2F2D66883FFDE0UL,
            Model54TetherHash = 0xE2CE97AFDB428E8AUL;

        /// <summary>The parents' pins at the physics integration 2 (model 34), which its kept audits fly: p7d-tether's model 31
        /// contact, range and tether goldens over the whole 2008-byte record (WithoutTheGroundTheGoldenFlightsAreModel31s:
        /// without surfaces.json and the layers' area laws), and p7a-ground's model 33 contact and range goldens over model 28's
        /// 1904 bytes (OverModel28sRecordEachGoldenIsModel33s).</summary>
        public const ulong Model31ContactHash = 0x89EEA4CA37C2ACD0UL, Model31RangeHash = 0xE29C5993A913A2B6UL, Model31TetherHash = 0xA8BD64033E8EBFBDUL,
            Model33ContactHash = 0x37C4BFF09B9C22E2UL, Model33RangeHash = 0xCA3CC415DC86A197UL;

        /// <summary>The physics integration 2's pins (model 34), which every golden flown on model 34's yaw I (Model34YawI) keeps
        /// bit for bit, every record and event (GoldenFlightTests.OnModel34sYawIEachGoldenIsModel34s, the kept audit of
        /// Betaflight's yaw I at model 36). The tumble has no rotors and keeps model 34's TumbleHash.</summary>
        public const ulong Model34PoweredHash = 0x68F588AF6D73D471UL, Model34RainPoweredHash = 0x947B8DD37255DDB4UL,
            Model34ContactHash = 0xE4C64497E36A0322UL, Model34RangeHash = 0x1547C72C382DC357UL, Model34TetherHash = 0x0B0C0D39CDE821F4UL;

        /// <summary>The two kept audits' range flights with the layers' area laws as model 38 flies them, with the buried face
        /// (ContactModel.Buried): p7a-ground's model 33 over model 28's 1904 bytes (OverModel28sRecordEachGoldenIsModel33s) and
        /// the flight on model 34's yaw I (OnModel34sYawIEachGoldenIsModel34s). With the buried face off (a temporary edit,
        /// reverted, at physics-integration-2--latch) each is Model33RangeHash and Model34RangeHash bit for bit, every record and
        /// event, so the buried face is the whole of each move; the audits flown without the area laws keep their pins (no
        /// layer, no buried face).</summary>
        public const ulong Model33RangeOn38Hash = 0x83945BE7D69BFA11UL, Model34RangeOn38Hash = 0xC961D6B865107711UL;

        /// <summary>
        /// The contact flight with the line (p7d-tether 1.6, first recorded with ModelVersion 31): FlyContact's flight on the
        /// stub's floor with tether.json's line laid from 3 m behind (the table's launch) and drone.json's coil, half paid out at
        /// the nominal loadout: it takes off, hovers 1 m up, comes down and lands with the line paying out, its span and trail
        /// in the step and its pull on the drone. Hashed over the whole record with its events. Against FlyContact without the line
        /// it moves from step 3 (the laid line's weight and its pull at the exit, at most 0.031 N) and keeps its course: at most
        /// 6.7 mm and 0.10° from it, 5 impacts against 5, at rest at the same 0.2519 m, 2.4 mm aside; 0.073 m paid out, the chain
        /// at 15 nodes, the trail at 8 points, no clip, safe step or wedge.
        /// Model 31: 0xA8BD64033E8EBFBD. The physics integration 2 (ModelVersion 34): re-recorded on the floor's belt_bare as
        /// surfaces.json's soil (FlyContact's default since model 27). Audited: without surfaces.json it is model 31's pin, every
        /// record and event (WithoutTheGroundTheGoldenFlightsAreModel31s). It moves from step 2 (the soil) and keeps its course:
        /// within 46 mm and 1.5 deg of model 31's path, 4 impacts against 5, at rest at 0.2497 m against 0.2519, 29.5 mm aside,
        /// its skids 2.1–2.2 mm into the soil; the line as on model 31 (0.073 m paid out, 15 nodes, the trail at 8 points, no
        /// clip or wedge), the chain's energy check taking 2 safe steps (none on model 31). Against the contact golden without
        /// the line it moves from step 3, as on model 31, and stays within 6.7 mm and 0.04 deg of it, at rest 0.4 mm aside.
        /// Model 34: 0x0B0C0D39CDE821F4. Betaflight's yaw I (ModelVersion 36), audited as the contact flight: on model 34's yaw I
        /// it is model 34's pin (the kept test). It moves from step 10 and keeps its course: within 0.5 mm and 0.08° of model
        /// 34's path, 4 impacts, at rest at the same 0.2497 m, 0.3 mm aside; the line as on model 34 (0.073 m paid out, no
        /// wedge), the chain's energy check taking 1 clip and 1 safe step (model 34: none and 2).
        /// Model 38: 0x318B90FF21403E3B. The physics integration 2 with p7f-damage (ModelVersion 40), audited as the contact
        /// flight: without props.blade, over 2008 bytes, model 38's pin, every record and event (WithoutTheBladeEachGoldenIsModel38s);
        /// the strikes off the same bits; p7f-damage has no line, so it has no parent pin there. It moves from step 9 against
        /// model 38's and keeps its course: within 12.5 mm and 0.80° of its path, 4 impacts, at rest at the same 0.2497 m, 9.3 mm
        /// aside; against the contact golden without the line it moves from step 3, as since model 31, and stays within 6.8 mm
        /// and 0.16° of it, at rest 1.2 mm aside; the line as on model 38 (0.073 m paid out, 15 nodes, the trail at 8 points, no
        /// wedge), the chain's energy check taking no clip or safe step (model 38: 1 and 1).
        /// Model 44: 0x40D840364F986978. The physics integration 2 with p7g-legs (ModelVersion 46), audited as the contact
        /// flight: on model 26's legs, over 2392 bytes, model 44's pin, every record and event (OnModel26sLegsEachGoldenIsModel44s);
        /// the law off the same 2392 bytes, the strikes off the same bits; p7g-legs has no line, so it has no parent pin there.
        /// It moves from step 1 against model 44's and keeps its course: within 96.4 mm and 1.67° of its path, 3 impacts against
        /// 4, at rest at 0.2494 m against 0.2497, 77.7 mm aside; against the contact golden without the line it moves from step
        /// 3, as since model 31, and stays within 6.8 mm and 0.06° of it, at rest 0.9 mm aside; the line paying out 0.075 m, 15
        /// nodes, the trail at 7 points, no clip or wedge, the chain's energy check taking 1 safe step. Every leg intact.
        /// On p7d-tether 1.6b (unity/physics/physics-integration-2--fountain, its ModelVersion 47 on model 44), the fountain and the
        /// ground vortex in the line's air: 0x58966ACA34ED8ED2; on design 4's field (FlightSetup.TetherDesign4Air) model 44's pin,
        /// every record and event. It moved from step 14, the motors' first thrust after the arming (their wakes land, the rear
        /// pair's wall jets meet under the line and their fountain moves it, so the exit's pull), and kept its course: within 1.8
        /// mm and 0.50° of model 44's path, the same 94 events, at rest at the same 0.2497 m, 0.6 mm aside; the line as on model
        /// 44 (0.073 m paid out, 15 nodes, the trail at 8 points, no wedge), the chain's energy check taking 1 safe step (none)
        /// and 8537 corrections (8179).
        /// Model 46: 0xFE34B0491C406118. The physics integration 2 with the tip slide and the fountain (ModelVersion 48), audited
        /// as the contact flight: on design 4's field, over 2784 bytes, model 46's pin, every record and event, and on model 26's
        /// legs, over 2392 bytes, p7d-tether 1.6b's model 47 pin (WithoutTheFountainEachGoldenIsModel49s,
        /// OnModel26sLegsEachGoldenIsModel47s); the tip slide off the same bits (no disc cuts the soil: 0 blade passes). It moves
        /// from step 13, the motors' first thrust after the arming, and keeps its course: within 0.76 mm and 0.10° of model 46's
        /// path, the same 65 events, 3 impacts, at rest at 0.2495 m against 0.2494, 0.6 mm aside; against the contact golden
        /// without the line it moves from step 3, as since model 31, and stays within 6.8 mm and 0.05° of it, at rest 1.4 mm aside;
        /// the line as on model 46 (0.075 m paid out, 15 nodes, the trail at 7 points, no clip or wedge), the chain's energy check
        /// taking no safe step (1) and 7280 corrections (7617). Every leg intact.
        /// p7g-legs 1.4 (ModelVersion 49), audited as the contact flight: with the lever off model 46's
        /// pin over its 2784 bytes, every record and event, the lean zero (WithoutTheLeverEachGoldenIsModel48s); with it, from step 3,
        /// within 1.8 mm and 0.84° of model 46's path, at rest 0.2494 m up as 46's, 1.4 mm from its rest; the line paying out 0.075 m,
        /// 15 nodes, the trail at 7, 1 safe step, no clip or wedge, as 46's; every leg intact.
        /// Model 48: 0x6A5EB8044CD5F1E2. The physics integration 2 with the legs' lever (ModelVersion 50), audited as the contact
        /// flight: with the lever off model 48's pin over 2784 bytes and on design 4's air model 49's over the whole 2816, every
        /// record and event (WithoutTheLeverEachGoldenIsModel48s, WithoutTheFountainEachGoldenIsModel49s); so the lever and the
        /// fountain compose and each parent's mechanism is the whole of the move against the other. Against model 49's path it
        /// moves as model 48 moved from 46 (the attitude by an ulp from step 4, the position from step 14, the motors' first
        /// thrust): within 1.3 mm and 0.15°, the same 71 events, 3 impacts, at rest 0.2495 m against 0.2494, 1.0 mm aside; against
        /// model 48's from step 3 (the standing skids on their levers): within 2.0 mm and 0.82°, 71 events against 65, at rest
        /// 0.2495 m as 48's, 1.2 mm aside; the largest lean 9.5 mm; the line as on both (0.075 m paid out, 15 nodes, the trail at 7
        /// points, no clip or wedge), the chain's energy check taking 1 safe step (48: none, 49: 1). Every leg intact.
        /// Model 50: 0x316A98D76AD4F03F (Model50TetherHash). p7d-tether 1.7 (ModelVersion 53): re-recorded with the fiber's contacts.
        /// Audited: without them (FlightSetup.TetherWithoutContacts) it is model 50's pin, every record and event
        /// (FiberContactTests.WithoutTheFiberContactsEachGoldenIsModel50s, flightbench --golden), and no other golden flies the
        /// line. With them the line's first contact is at step 309, the motors' spool-up: the rear pair's fountain lifts the piece
        /// rising to the exit into the stack's plates, the coil's belly and the payload's aft (θ up to 1.42 rad, T up to 0.61 N),
        /// where it is held; the drone leaves model 50's path at step 660 (the parts' reactions, up to 0.145 N over 132 steps) and
        /// stays within 0.59 mm and 0.025° of it, 3 impacts as 50's, at rest 0.2495 m up as 50's, 0.29 mm aside; 161 events
        /// against 71 (the FiberContact and FiberSlip events); the line 0.0757 m paid out against 0.0753, 20 nodes against 15 (7
        /// contact nodes put in), the trail at 7 points as 50's, no wedge; the energy check took 51 safe steps (50's: 1) and 16
        /// clips (0), the parts' slots near the exit (labelled numerical damping); 80 shape queries over the flight.
        /// Model 55 (the physics integration 2 with the fiber's contacts, 53 merged onto 51): kept, not re-recorded. No leg parts
        /// in it, so with the hanging half's give and the stub's spring on or off it is 1.7's pin (GoldenFlightTests.
        /// WithoutTheHangingGiveEachGoldenIsModel53s), and without the fiber's contacts model 51's, which is model 50's
        /// (WithoutTheHangingGiveEachGoldenIsModel50s).
        /// p7f-damage 1.3b (model 52, off 50): re-recorded on the 2944-byte record as 0x053EF845C2E62E3F (Model52TetherHash), audited
        /// as the others: over its first 2816 bytes model 50's pin, every record and event, with the props at rest on and off,
        /// the block zero (WithoutARestingPropEachGoldenIsModel50s); on design 4's air model 49's pin over those 2816 bytes.
        /// Model 57 (the physics integration 2 with the props at rest, 52 merged onto 55): re-recorded on the 2944-byte record, the
        /// zero block alone. Audited case by case: over its first 2816 bytes it is 55's pin (1.7's, Model53TetherHash), every
        /// record and event, with the props at rest on and off and the block zero (GoldenFlightTests.
        /// WithoutARestingPropEachGoldenIsModel55s): the line never meets a resting blade here (its blades are shapes only before
        /// the arming and after the disarm, and the line's first contact is at step 309, the spool-up); without the fiber's
        /// contacts, with the hanging give on and off, it is model 52's pin over the whole record (WithoutTheFiberContactsAndThe
        /// HangingGiveEachGoldenIsModel52s). No other golden moved: each is 52's pin.
        /// p7d-tether 1.8 (model 56, off 55, the cuts and the link): re-recorded. Audited (GoldenFlightTests.WithoutTheCutsEachGoldenIsModel55s):
        /// without the cuts (FlightSetup.TetherWithoutCuts) it is model 55's pin (Model55TetherHash), every record and event; with
        /// them every record is model 55's up to step 7269, the events but FiberSection's 17 first reads at the drone's parts too,
        /// where rotor 2's disc cuts the line at its tip (the crossing 0.127 m from the hub, T 0.031 N) as the drone settles on
        /// its legs: the piece the rear pair's fountain lifts into the stack's plates crosses the disc's tip circle. The link is
        /// Broken from there, the failsafe disarms 1.5 s later (DROP), and a near piece of 10 nodes hangs from the exit, its free
        /// end at the drone's parts (1661 FiberContact and 2410 FiberSlip events in the 2.7 s after: its slots set afresh as the
        /// free end flutters, against 47 and 43 on model 55's whole flight). A known risk, not tuned:
        /// the fuselage's blocking of the rear fountain is not modelled (1.8's note).
        /// Model 59 (the physics integration 2 with the cuts, 56 merged onto 57): re-recorded on the 2944-byte record, 56's flight
        /// and the zero block. Audited case by case: without the cuts it is model 57's pin (Model57TetherHash), every record and
        /// event, and with them every record is 57's up to step 7269, rotor 2's disc again (its blade at 114.56 m/s: a turning
        /// rotor, far above ContactSpec.BladeRestSpeed; GoldenFlightTests.WithoutTheCutsEachGoldenIsModel57s); over model 50's
        /// 2816 bytes it is model 56's pin (Model56TetherHash), every record and event, with the props at rest on and off, the
        /// block zero on every record: no blade rests on anything and the line never meets a resting one, before the break or
        /// through the near piece's hang, the disarm and the spin-down (WithoutARestingPropEachGoldenIsModel56s). No other golden
        /// moved: each is 57's pin.
        /// The skid's frame fore and aft (ModelVersion 54, off model 51's 0x316A98D76AD4F03F, model 50's: no golden parts a leg):
        /// re-recorded as 0xE2CE97AFDB428E8A (Model54TetherHash) on the 2816-byte record, without the fiber's contacts, audited as the contact flight: without the frame model 51's pin, every record and event
        /// (model 54's WithoutTheFrameEachGoldenIsModel51s; since 60 WithoutTheHangingGiveEachGoldenIsModel50s). With it, it moves from step 3 and keeps its course: within 17.6 mm and 0.49° of
        /// model 51's path, 3 impacts, at rest 0.2494 m against 0.2495, 13.8 mm aside; the largest lean 8.0 mm; every leg intact.
        /// The physics integration 2 with the skid's frame (ModelVersion 60, 54 merged onto 59): re-recorded. Audited case by
        /// case: without the frame it is model 59's pin, every record and event (GoldenFlightTests.WithoutTheFrameEachGoldenIs
        /// Model59s); without the cuts, the fiber's contacts and the props at rest, over model 50's 2816 bytes, the frame's model
        /// 54 pin (WithoutTheCutsTheRestAndTheFiberContactsEachGoldenIsModel54s); every older audit flies WithoutTheFrame beside its
        /// own switch and keeps its pin. With the frame the drone leaves 59's path at step 3 (the standing skids along the skid on
        /// the frame's give) and keeps its course: within 22.59 mm and 0.724° of it, 3 impacts, at rest 0.2500 m up against
        /// 0.2501 on 4 pairs, 0.32 mm aside; the largest lean 3.26 mm (3.55); every leg intact. The line breaks as at 59, by the
        /// same mechanism at the same place, 35 steps later: rotor 2's disc at its tip at step 7304 (59: 7269), the crossing
        /// 0.1260 m from the hub (0.1265), T 0.012 N (0.031), the blade at 113.70 m/s (114.56: a turning rotor, never a resting
        /// one), as the drone settles on its legs: the frame's stiffer fore-aft give changes how the drone settles, and the piece
        /// the rear fountain lifts crosses the tip circle a little later. Then a near piece of 11 nodes (10) hangs from the exit
        /// and the failsafe disarms (DROP); 0.0756 m paid out (0.0757), 11 nodes (10), the trail at 20 points as 59's, 2 clips
        /// (0) and 9 safe steps (3) in the near piece's flutter, 0 wedges, 0 dips, 18 sections read (17); 2095 events against
        /// 4150 (fewer FiberContact and FiberSlip churn at the near piece's free end). 1.8's known risk (the fuselage's blocking
        /// of the rear fountain is not modelled), not tuned.
        /// Model 58 (p7f-damage 1.5, the motors' bells): re-recorded on the 3040-byte record. Audited case by case: over its first
        /// 2944 bytes model 57's pin (Model57TetherHash), every record and event, with the bells on and off, the block zero
        /// (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel57s); without the fiber's contacts model 52's pin over those bytes.
        /// Model 63 (the physics integration 2 with the bells, 58 merged onto 60): re-recorded on the 3040-byte record, 60's flight
        /// and the bells' zero block. Audited case by case: over model 57's 2944 bytes model 60's pin (Model60TetherHash), every record
        /// and event, with the bells on and off, the block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel60s):
        /// no motor's band touches anything in it; without the skid's frame and the cuts model 58's pin (Model58TetherHash) over the whole record
        /// (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).
        /// So the tether golden still breaks as at 60: rotor 2's disc at its tip at step 7304, the failsafe to Disarmed, a near
        /// piece of 11 nodes, 2 clips and 9 safe steps, 0 wedges: no bell touches the line's pieces or the ground through it.
        /// Model 61 (the drone's body over the fountain, RotorAir's blocking by the drone's contact shapes): re-recorded as
        /// 0xA4500FA43C5CE196. Audited case by case: with the fountain through the body (FlightSetup.TetherFountainThroughBody)
        /// it is model 59's pin (Model59TetherHash), every record and event (GoldenFlightTests.
        /// WithoutTheBodysBlockingEachGoldenIsModel59s); with the body the records part from 59's where the line first lies
        /// in the coil's lee (the piece over its aft cap, under the stack), and rotor 2's disc still cuts the line at its tip
        /// as the drone settles, now at step 7265: the piece cut rises behind the coil's aft pole, between the rear discs, where
        /// no part of the body stands over the fountain. Every older kept audit flies through the body (FlyContact's body:
        /// false, and a line without its cuts or contacts is older still). No other golden moved.
        /// Model 64 (the body over the fountain and D01's canister): re-recorded. Audited case by case: on model 63's drone.json (the
        /// old coil, Model63Drone, its line out of the old rear cap: Model63Tether) with the fountain through the body it is model
        /// 63's pin (Model63TetherHash), every record and event; with 61's blocking on the old coil it is the merge's pin
        /// (Model63BodyTetherHash, 0xEF11D87EFFD6402B): records 63's until the piece on the coil's cap enters its lee, and rotor 2's
        /// disc cuts the line at its tip at step 7349 (63: 7304), the blade at 115.29 m/s (GoldenFlightTests.
        /// OnTheOldCoilEachGoldenIsModel63s). On the canister it leaves the old coil's path at the first step (the mass build), keeps
        /// its course (3 impacts, at rest 0.2495 m up on 4 pairs, 24.8 mm and 0.52° from the merge's rest) and **the line stays
        /// whole**: launched along the nozzle's lead to the ground 0.31 m behind the exit, it leaves the tip aft and down, below
        /// and behind the rear discs, and no segment crosses a disc through the liftoff, the hover, the landing and the spin-down;
        /// 0.2300 m paid out (0.0757), 15 nodes (9), the trail at 7 points (19), 4 clips (57) and 23 safe steps (11), 0 wedges, 0
        /// dips, 16 sections read (18). Every older kept audit flies the old coil.
        /// Model 62 (the line held between the drone's parts, p7d-tether 1.7's part slots, on 59): re-recorded. Audited case by
        /// case (GoldenFlightTests.WithoutTheCoulombHoldEachGoldenIsModel59s, flightbench --golden): with the known-wrong 0-force
        /// hold (FlightSetup.TetherZeroForceHold: model 59's chain) it is model 59's pin (Model59TetherHash), every record and
        /// event; with a part holding the line only by what presses it, every record is 59's for 7204 steps (the line's slots on
        /// the stack, the coil and the payload differ from the spool-up, their events from the 55th, but the parts' reactions are
        /// 59's to the bit until then), and rotor 2's disc cuts the line at step 7258 as the drone settles (59's: 7269), T 0.024 N
        /// (0.031): 1.8's known risk, the same mechanism and place. 248 events against 4150 (the near piece's slots no longer
        /// churn at the parts: 1.8's finding 7); no wedge; 2 safe steps against 3. No other golden flies the line: each keeps its
        /// pin.
        /// Model 62 again (unlanded; a shape met by its own face at each node, and the safe pass leaving free slots free unless
        /// the solve ends their node inside): re-recorded from 0x61BFE96EC1C84A80. Audited: with model 62's first chain and the
        /// faces it is that pin (the faces alone move nothing here); with the safe pass's rule every record is that pin's for
        /// 277 steps, to the spool-up's first safe step, where the old pass held every free slot; before the break its fiber
        /// events match (133 against 136 in steps 7000–7999); rotor 2's disc cuts the line at step 7338 as the drone settles
        /// (the pin's 7258), the same mechanism and place, T 0.014 N (0.024); no wedge, 6 safe steps against 2; 1051 events against
        /// 248, the near piece's slips on the parts after the break (1.8's finding 7, its volume 1.13's). With the 0-force hold
        /// (model 59's chain, its safe pass included) it is still Model59TetherHash.
        /// Model 65 (the physics integration 2 with the fiber's chain fixes, 62 merged onto 64): re-recorded. Audited case by case:
        /// with the known-wrong 0-force hold (FlightSetup.TetherZeroForceHold: 64's chain, its hold and its safe pass) it is model
        /// 64's pin (Model64TetherHash), every record and event (GoldenFlightTests.WithoutTheCoulombHoldEachGoldenIsModel64s); on
        /// model 63's drone.json through the body, without the skid's frame, over model 57's 2944 bytes it is model 62's pin
        /// (Model62TetherHash) with the rules and model 59's with the hold (WithoutTheCoulombHoldEachGoldenIsModel59s). With the
        /// rules on the canister every record is 64's for 31 steps, to the spool-up's first safe step (both chains take it at step
        /// 31; 64's safe pass held every free slot speculative, 65's leaves them free): the drone stays within 0.039 mm and 0.0012°
        /// of 64's path and comes to rest where 64's does (within 0.01 mm, 0.2495 m up), 3 impacts as 64's; the line stays whole,
        /// 0.2300 m paid out, 15 nodes, the trail at 7 points, 16 sections read, 0 dips, 0 wedges, as 64's; 3 safe steps (23) and 3
        /// clips (4); 81 events as 64's, every kind's count 64's (30 ContactMade, 22 ContactBroken, 3 Impact, 16 FiberSection, 5 and
        /// 5 of the vortex ring), the first 28 the same, then 64's three FiberSection reads at step 32 come later. No other golden
        /// flies the line: each keeps its pin.
        /// Model 66 (p7f-damage 1.6, the shafts and the bearings): re-recorded on the 3168-byte record. Audited case by case: over its
        /// first 3040 bytes model 63's pin (Model63TetherHash), every record and event, with the shafts on and off, the block zero
        /// (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel63s): rotor 2's disc cuts the line, nothing harder, and its legs land.
        /// Model 68 (the physics integration 2 with the shafts, 66 merged onto 65): re-recorded on the 3168-byte record. Audited case
        /// by case: over its first 3040 bytes model 65's pin (Model65TetherHash), every record and event, with the shafts on and off,
        /// the block zero (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel65s): the line stays whole on the canister, no disc meets
        /// anything hard and the legs land, so no shaft passes its yield and no bearing its static rating; on model 63's drone through
        /// the body, with the 0-force hold and no blade root, model 66's pin over the whole record (rotor 2's disc cuts the line,
        /// OnModel63sDroneEachGoldenIsModel66s).
        /// </summary>
        // Model 72 (p7f-damage 1.6's losses: the arms in their plane, the gyro's range): re-recorded on the 3344-byte record. Audited
        // case by case (GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s): with the clean sensors model 68's pin over its 3168
        // bytes, every record and event, the arms' block zero on every record; with them the flight moves from step 9 (twist at most 0.048°, arm load 0.0014, the gyro's peak 158.2 °/s), the line whole, at rest 0.2495 m up.
        public const ulong TetherHash = 0xC2BD29DB9E191B37UL;

        /// <summary>The tether golden at model 64 (the body over the fountain and D01's canister), which the flight with the
        /// known-wrong 0-force hold keeps (FlightSetup.TetherZeroForceHold: GoldenFlightTests.WithoutTheCoulombHoldEachGoldenIsModel64s).</summary>
        public const ulong Model64TetherHash = 0xDA2557EE178F70F6UL;

        /// <summary>The tether golden at model 62 (the line held between the drone's parts, on 59), which the flight on model 63's
        /// drone.json through the body without the skid's frame keeps over model 57's 2944 bytes (GoldenFlightTests.
        /// WithoutTheCoulombHoldEachGoldenIsModel59s).</summary>
        public const ulong Model62TetherHash = 0xF29F8C067CE666F1UL;

        /// <summary>The tether golden at model 61 (the drone's body over the fountain, on 59), which the flight without the skid's
        /// frame over model 57's 2944 bytes keeps (GoldenFlightTests.WithoutTheBodysBlockingEachGoldenIsModel59s).</summary>
        public const ulong Model61TetherHash = 0xA4500FA43C5CE196UL;

        /// <summary>The tether golden of p7d-tether 1.8 (model 56) on model 50's 2816 bytes, which the flight without the props
        /// at rest keeps (ContactSpec.BladeRest off: GoldenFlightTests.WithoutARestingPropEachGoldenIsModel56s).</summary>
        public const ulong Model56TetherHash = 0x12767492AE1705BCUL;

        /// <summary>The tether golden at model 55 (1.7's pin, kept by the integration), which the flight without 1.8's cuts keeps
        /// (p7d-tether 1.8, FlightSetup.TetherWithoutCuts: GoldenFlightTests.WithoutTheCutsEachGoldenIsModel55s).</summary>
        public const ulong Model55TetherHash = 0xCD7C8022C0A85391UL;

        /// <summary>The tether golden of p7d-tether 1.7 (model 53), which model 55 kept: the flight with the fiber's contacts over
        /// model 50's 2816 bytes with the props at rest on or off (GoldenFlightTests.WithoutARestingPropEachGoldenIsModel55s) and
        /// with the hanging give on or off (WithoutTheHangingGiveEachGoldenIsModel53s). Model 50's (Model50TetherHash, with the
        /// other model 50 pins) is the flight without the fiber's contacts over those bytes (FiberContactTests.
        /// WithoutTheFiberContactsEachGoldenIsModel50s).</summary>
        public const ulong Model53TetherHash = 0xCD7C8022C0A85391UL;

        /// <summary>The tether golden of p7f-damage 1.3b (model 52) on the 2944-byte record, which the flight without the fiber's
        /// contacts keeps over those bytes (Model57Layout, since model 58's bells block; GoldenFlightTests.
        /// WithoutTheFiberContactsAndTheHangingGiveEachGoldenIsModel52s).</summary>
        public const ulong Model52TetherHash = 0x053EF845C2E62E3FUL;

        /// <summary>The physics integration 2 with the tip slide and the fountain (model 48), its audits against the parents'
        /// tether pins: the line's model 46 (TetherHash at model 46, design 4's field), which the contact flight with the line on
        /// design 4's air keeps over 2784 bytes, and p7d-tether 1.6b's model 47 (its fountain on model 44), which the same flight
        /// with the fountain on model 26's legs keeps over 2392 bytes (OnModel26sLegsEachGoldenIsModel47s). The tip slide's model
        /// 45 kept model 40's pins, as 42 and 44 did: on model 26's legs and design 4's air every golden is Model44's
        /// (OnModel26sLegsEachGoldenIsModel44s). Since model 50 (the legs' lever on the line) the parents' own tether pins: model
        /// 48's (TetherHash at model 48, the fountain on 46), which the flight with the lever off keeps over 2784 bytes
        /// (GoldenFlightTests.WithoutTheLeverEachGoldenIsModel48s), and p7g-legs 1.4's model 49 (the lever on 46), which the flight
        /// on design 4's air keeps over the whole 2816 bytes (WithoutTheFountainEachGoldenIsModel49s); on design 4's air with the
        /// lever off it is model 46's (Model46TetherHash) over 2784.</summary>
        public const ulong Model47TetherHash = 0x58966ACA34ED8ED2UL, Model48TetherHash = 0x6A5EB8044CD5F1E2UL,
            Model49TetherHash = 0x080BCB2E19873507UL;

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
        // Model 17–19: 0x2A0283BBFE59FD84. p7b-airframe 1.3d (ModelVersion 21): the 1872-byte record, the wake's bytes zero (no
        // propulsion); over model 19's 1808 bytes it is model 19's pin (the kept test), and models 13's to 15's on their files.
        // On p7b-airframe--micro, D-064 (ModelVersion 23): the record's 32 wake bytes, zero (no rotors), 0xDB5DEEA6285B6184; over
        // model 19's 1808 bytes it is model 19's (the kept test).
        // Model 24: the 1904-byte record, the interference's and the own wake's bytes zero (no propulsion): over the first 1872
        // bytes the rotor-wake branch's pin, over the micro branch's 1840 its pin (the kept test), over 1808 model 19's (the kept
        // test).
        // Model 22 on p7b-airframe--power: 0x2A0283BBFE59FD84. D-066 (ModelVersion 23): the L4 body is the lighter packs' and electronics' (4.742 kg);
        // momentum within 1.7e-12, energy 3.5e-12, the CoM 5.1e-11 m from its parabola, the rate wandering 5.46 rad/s.
        // Model 25: 0x6712A54D0FDA7F84. The power merge (ModelVersion 26): D-066's lighter L4 body (4.742 kg) on the 1904-byte
        // record, the interference's and the own wake's bytes zero: on model 22's drone model 25's pin (the kept test), over
        // 1808 bytes the power branch's 0x95E56C6BB49BB744 (the kept test); momentum within 1.7e-12, energy 3.5e-12, the CoM
        // 5.1e-11 m from its parabola, the rate wandering 5.46 rad/s, the power branch's numbers.
        // Model 31 (p7d-tether 1.6): re-recorded on the 2008-byte record, the fiber's block zero; over model 28's 1904 bytes
        // it is model 28's pin, Model28TumbleHash, bit for bit (the kept test). Betaflight's yaw I (ModelVersion 36): it has no
        // rotors and keeps its hash.
        // On p7f-damage (off model 26), model 29: 0xBCBD33E6B6A94844. p7f-damage 1.3 (ModelVersion 35): the 2288-byte record, the strikes' bytes zero (no contacts);
        // over model 29's 1904 bytes model 29's pin (the kept test), its checks unchanged.
        // Model 38: 0x3FD62A50B3AF6C84 (the line), model 35: 0x9931AD0B7F279044 (p7f-damage). The physics integration 2 with
        // p7f-damage (ModelVersion 40): the 2392-byte record, the fiber's and the strikes' blocks zero (no rotors, no contacts);
        // over the line's 2008 bytes model 38's pin and over p7f's record model 35's (both kept tests), its checks unchanged.
        // On p7g-legs (off model 26), model 26: 0xBCBD33E6B6A94844. p7g-legs 1.1 (ModelVersion 37): the L4 body with the plastic legs (4.735 kg; roll
        // 0.0436, pitch 0.0466, yaw 0.0590 kg·m²): on model 26's legs model 26's pin (the kept test); 8.9 mm and 0.20° from
        // model 26's end at 10 s; momentum within 1.4e-12, energy 2.7e-12, the CoM 5.1e-11 m from its parabola, the rate
        // wandering 5.49 rad/s (5.46).
        // Model 37: 0x5FBFA89AF2217693. p7g-legs 1.2 (ModelVersion 39): the 2072-byte record, the legs' bytes zero (no contacts):
        // over 1904 bytes model 37's pin (the kept test); its checks as before.
        // Model 39: 0x7F9A35ED00445E73. p7g-legs 1.3 (ModelVersion 41): the 2296-byte record; over model 39's 2072 bytes model
        // 39's pin (the kept test); its checks as before.
        // Model 44: 0x4C96AAB4C5453484 (the line), model 41: 0xA6914F6702FE98F3 (p7g-legs). The physics integration 2 with
        // p7g-legs (ModelVersion 46): the 2784-byte record on p7g's L4 body (4.7354 kg), the fiber's, strikes' and legs' blocks
        // zero (no rotors, no contacts); on model 26's legs over 2392 bytes model 44's pin, over p7g's record model 41's (both
        // kept tests); its checks as p7g's (momentum 1.4e-12, energy 2.7e-12, the CoM 5.1e-11 m from its parabola, wandering
        // 5.493 rad/s).
        // Model 46: 0x24B0961BC802C1D3. p7g-legs 1.4 (ModelVersion 49): re-recorded on the 2816-byte record (the legs' lean after model 46's 2784
        // bytes). Audited: over model 46's 2784 bytes it is model 46's pin bit for bit, every record and event, the lean zero on every
        // record (it touches nothing; the kept test WithoutTheLeverEachGoldenIsModel48s, flightbench --golden).
        // Model 50 (the legs' lever on the line) keeps it: no line, no carve (WithoutTheFountainEachGoldenIsModel49s).
        // Model 50: 0xE798498661DECB53. p7f-damage 1.3b (ModelVersion 52): re-recorded on the 2944-byte record (the resting blades' block
        // after model 50's 2816 bytes). Audited: over its first 2816 bytes model 50's pin, every record and event, with the props at
        // rest on and off, the block zero on every record (GoldenFlightTests.WithoutARestingPropEachGoldenIsModel50s): no prop rests
        // on anything in it.
        // Model 58 (p7f-damage 1.5, the motors' bells): re-recorded on the 3040-byte record (the bells' block after 2944). Audited
        // case by case: over its first 2944 bytes model 57's pin (Model57TumbleHash), every record and event, with the bells on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel57s): no motor's band touches anything in it.
        // Model 63 (the physics integration 2 with the bells, 58 merged onto 60): model 58's pin, kept. Audited case by case: over
        // model 57's 2944 bytes model 60's pin (its 57's), every record and event, with the bells on and off, the block zero
        // (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel60s); without the skid's frame and the cuts model 58's pin over the
        // whole record (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).
        // Model 64 (the body over the fountain and D01's canister): re-recorded. Audited case by case: on model 63's drone.json (the
        // old coil, Model63Drone; the line out of the old rear cap, Model63Tether) with the fountain through the body it is model
        // 63's pin (Model63TumbleHash), every record and event (GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s, flightbench
        // --golden), so the canister is the whole move. The free body's new inertia and product of inertia: its checks hold (momentum within 1.6e-13, energy 3.4e-13, the
        // parabola 5.1e-11 m, the rate wandering 4.41 rad/s), 0.24 m and 14.9° from 63's end.
        // Model 66 (p7f-damage 1.6, the shafts and the bearings): re-recorded on the 3168-byte record (the shafts' block after 3040).
        // Audited case by case: over its first 3040 bytes model 63's pin (Model63TumbleHash), every record and event, with the shafts on and
        // off, the block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel63s): no shaft passes its yield and
        // no bearing its static rating in it.
        // Model 68 (the physics integration 2 with the shafts, 66 merged onto 65): re-recorded on the 3168-byte record. Audited case
        // by case: over its first 3040 bytes model 65's pin (Model65TumbleHash), every record and event, with the shafts on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel65s): no shaft passes its yield and no bearing
        // its static rating on the canister either; on model 63's drone through the body, with the 0-force hold and no blade root, model
        // 66's pin over the whole record (OnModel63sDroneEachGoldenIsModel66s).
        // Model 72 (p7f-damage 1.6's losses: the arms in their plane, the gyro's range): re-recorded on the 3344-byte record. Audited
        // case by case (GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s): with the clean sensors model 68's pin over its 3168
        // bytes, every record and event, the arms' block zero on every record; with them the flight moves not at all (no rotors): model 68's records with the zero block appended.
        public const ulong TumbleHash = 0xB4B9AFE7B9B45D17UL;
        public const int TumbleSteps = 10000;

        // 1.6b: first recorded with ModelVersion 7, on the test range's content RangeContent (MapWorld.ContentHash, query
        // version 2): a change of the map or its assets moves the content hash first, so an audit tells the world's change
        // from the physics'. Model 7: 0x778DD7ED57A37F62 on content 0xD6DE238216CEB4B9. The merge with p7b-airframe
        // (ModelVersion 16): the content moves by physics_materials.json's new carbon_composite row alone (every worldbench
        // query hash unchanged); the flight by D-050's drone, the airframe's effects (FlyContact's five) and the seam rule.
        // Audited (the kept test): every mechanism off on 1.6b's drone.json over 1.6b's layout, without the seam rule (a
        // temporary edit) it is model 7's bit for bit, with it 0x613987892649445F.
        // Model 16: 0x292C0858705902B6. The merge with unity/main (ModelVersion 19, p7c-wind's field at model 18): re-recorded
        // on the 1808-byte record (p7c-wind's 152 bytes at 1296) in the Calm field with p7c-wind 1.6's derived rotor drag
        // (0.044), on the same content. Audited against the range branch: with main's tuned drag (0.25), over the range
        // branch's 1656 bytes, in still air it is model 16's still-air flight bit for bit, every record and event
        // (0xDC85BA160EB0415C, a kept test until model 22), and in a steady 5 m/s from 270° model 16's 0x156C9858A7790315 with the
        // record's wind slot written as model 16's stand-in carried it, 0 (a temporary check). Against unity/main the stub's
        // goldens and audits keep main's model 18 pins: 1.6b's laws change no flight on the floor. At model 19 the Calm field
        // blew nothing here: SampleGround reports Hole over S04 and the field took a hole as sheltered (WindField.Begin), so the
        // flight was the same in still air and in Windy's field, bit for bit (a check). It moved from model 16's by the
        // stand-in's Calm air, which blew over the hole, the derived drag and the record: 5 impacts, no crash, at rest
        // 19.15 mm into the straw on 18 pairs (model 16: 21.98 mm on 17). Model 19: 0xE4B13EDD65CFF0B2.
        // Model 20 (the wind over a hole, FlightWorld.SoilUnder): re-recorded on the same content and query version 4, where
        // the Calm field now blows over S04 from the straw mat's top: 5 impacts, no crash, at rest 21.19 mm into the straw on
        // 19 pairs. Audited: with the old rule (the hole's −∞ taken as sheltered, a temporary switch) it is model 19's pin bit
        // for bit, so the wind over the hole is the whole change; in still air it is model 16's (0xDC85BA160EB0415C, the
        // kept test), and Calm and still air now differ. Read from the first soil shape alone (the mat's soil box, 0.125 m
        // under the plane's height, a known-wrong) it was 0xCE9BEF5615932EFB, 7 impacts, 0.233 m up on 21 pairs.
        // Model 20: 0x7A21A5FA04DD30E0. The merge with p7c-wind--d060 (ModelVersion 21: D-060's class-4 z0 and σ_u/U, the
        // turbulence to 0.5 m): re-recorded on the same content and query version 4, where the Calm field over S04 now
        // carries D-060's profile and turbulence: 5 impacts, no crash, at rest 19.10 mm into the straw on 18 pairs. Audited
        // against both parents (temporary switches, reverted): with z0 0.03 m, σ_u/U 0.135 and the 1 m cut every golden is
        // the range branch's model 20 (range 0x7A21A5FA04DD30E0, powered 0xFE81E38EFCBD2F4E, contact 0x1152D64875230761,
        // rain 0xE5D4C472F6CA12D2); with 19's rule over a hole the stub's goldens are p7c-wind--d060's pins and the range
        // flight is model 19's 0xE4B13EDD65CFF0B2, so the change is D-060's field blowing over the hole, nothing else.
        // Model 21: 0x10A035F9F29042CC. The merge with p7c-wind--map-wakes (ModelVersion 22): re-recorded in the map's own
        // Calm wind (WindSetup.ForMap, the player's: the obstacles' wakes and the belt's canopy) on the same content: 4
        // impacts, no crash, at rest 19.09 mm into the straw on 18 pairs. Audited: in WindSetup.Of(Calm), the open field
        // the golden flew before, it is model 21's pin bit for bit (a check before the switch), and in still air model 16's
        // (the kept test); flown twice, the same bits with the wake grid's worker.
        // Model 22: 0x80C73DBA130C6903. The physics integration (ModelVersion 24): re-recorded in the map's own Calm wind
        // (WindSetup.ForMap, kept) on the same content with the interference and D-064 in its setup: 3 impacts (4), no crash,
        // at rest on 19 pairs (18), 0.6 mm deeper in the straw. Audited (temporary switches, reverted): with the interference
        // alone (D-064's sources off, the wash on model 19's numbers, the stream at wake.rotor) it is the rotor-wake merge's
        // 0x818AFC9F9B03EFA3, with neither model 22's pin bit for bit, and on main's drag in still air over the range branch's
        // 1656 bytes model 16's 0xDC85BA160EB0415C (a kept test until model 22: D-064's wash has no switch). Without the
        // interference it is 0x64DBB622DE602158 (no parent flew D-064 on the range). It moves from step 10 against that flight
        // and from step 9 against model 22's, and keeps its course: within 1.0 mm and 0.10° of the first, 34 mm and 3.3° of
        // the second (step 8624, the held bounce over the straw).
        // Model 22 on p6-flight-core--landing: 0x80C73DBA130C6903. 1.6c's stick spring on the slip (ModelVersion 23, D-067):
        // with the old anchor at the contact point (a temporary edit, reverted) it is model 22's pin bit for bit; with the new,
        // 0x7BD729C0B5F2DFD4 on the same content: 5 impacts (4), no crash, at rest 21.24 mm into the straw on 19 pairs (19.09
        // mm on 18), 1.0 mm aside: the straw's boxes are compliant, so its pairs press and slip more than the plane's. There
        // the still-air audit against model 16 (main's drag over the range branch's 1656 bytes, a kept test on that branch)
        // read 0xF19A1EDCA011C4D2 with the new spring: 5 impacts, no crash, at rest 19.11 mm into the straw on 18 pairs
        // (19.17 mm), 2.7 mm aside; model 16's 0xDC85BA160EB0415C with the old anchor.
        // Model 24: 0xAF27BC78A59ECD98. The landing's merge (ModelVersion 25): re-recorded in the map's own Calm wind
        // (WindSetup.ForMap, kept) on the same content with 1.6c's stick spring under the interference and D-064: 3 impacts
        // (3), no crash, at rest 19.53 mm into the straw on 20 pairs (19.66 mm on 19). Audited (temporary edits, reverted):
        // with the stick anchor at the contact point it is model 24's pin bit for bit; without the interference and D-064's
        // sources, over the first 1808 bytes, the landing branch's 0x7BD729C0B5F2DFD4; with neither model 22's pin, and in
        // still air on main's drag over the range branch's 1656 bytes the landing's 0xF19A1EDCA011C4D2 and, with the old
        // anchor, model 16's 0xDC85BA160EB0415C. Without the interference it reads 0x1E7B91861E59753F (0x64DBB622DE602158
        // with the old anchor; no parent flew D-064 on the range). It moves from step 3 against model 24 and from step 9
        // against the landing's, and keeps its course: at rest 1.6 mm and 17.6 mm aside, within 1.8 mm and 0.30° of model
        // 24's path, 35 mm and 4.2° of the landing's (step 8625, the held bounce over the straw).
        // Model 22 on p7b-airframe--power: 0x80C73DBA130C6903. D-066 (ModelVersion 23), audited as the powered flight: it keeps its course, 4 impacts,
        // no crash, at rest on 18 pairs at 0.2342 m against 0.2339, within 1 mm.
        // Model 25: 0x0D5350CB2E68C6B8. The power merge (ModelVersion 26), in WindSetup.ForMap's Calm (kept): re-recorded with
        // D-066's drone: 4 impacts, no crash, at rest 20.69 mm into the straw on 21 pairs (model 25: 19.53 on 20; the power
        // branch: 18.82 on 18). Audited as the contact flight: model 25's pin on model 22's drone (the kept test), the power
        // branch's 0xFBAC3E4D67B45ACD under the temporary edits over 1808 bytes. It moves from step 1 and step 3 and keeps its
        // course: at rest 6.9 mm and 24 mm from their resting places, within 114 mm and 3.1° of model 25's path and 34 mm and
        // 3.5° of the power branch's (the held bounce over the straw).
        // Model 26: 0xC79B60F487AEB8B6, carried to model 32 (1.10's soil and 1.13's rain act on no layer, and the golden flies
        // without surfaces.json). p7a-ground 1.12 (ModelVersion 33): re-recorded with the layers' area laws on the same
        // content: the mat meets each half-skid at 3.03 × the reference foot's 490 N/m, so the drone settles 9 mm into the
        // straw at the start and moves from step 2; 4 impacts (4), no crash, at rest 8.74 mm into the straw on 8 pairs
        // (20.69 mm on 21: the leg tubes' ends no longer reach the mat), rolled 0.10° (−1.34°), 52 mm from model 32's resting
        // place and within 65 mm of its path. Audited: without the area laws (WithoutTheAreaLaws) it is model 32's pin bit
        // for bit, every record and event, and in Rain with surfaces.json 1.13's 0x20B0DB29D33AD18E (the kept test
        // WithoutTheAreaLawsTheRangeGoldenIsModel32s): neither reaches a layer's stroke and the seam's facing test changes
        // nothing there, so the area law is the whole move. That Rain flight now rests rolled −0.15° (17.6°) as
        // 0x4AB00DBB3EA1721E.
        // Model 31 on p7d-tether (1.6; 0xE29C5993A913A2B6): re-recorded on the 2008-byte record, the fiber's block zero; over model 28's 1904 bytes
        // it is model 28's pin, Model28RangeHash, bit for bit (the kept test).
        // Model 33 on p7a-ground: 0xCA3CC415DC86A197 (above).
        // The physics integration 2 (ModelVersion 34): the ground's layers on the line's 2008-byte record. Audited against each
        // parent with the other side off: over model 28's 1904 bytes it is p7a-ground's model 33 pin, every record and event,
        // the fiber's block zero (OverModel28sRecordEachGoldenIsModel33s); without the layers' area laws p7d-tether's model 31
        // pin over the whole record (WithoutTheGroundTheGoldenFlightsAreModel31s) and model 32's over 1904 bytes, dry and in
        // Rain with surfaces.json (WithoutTheAreaLawsTheRangeGoldenIsModel32s); with the map's cuts dropped from
        // WindSetup.ForMap (the cavity off) the same bits, as it never enters a cut's opening (WithoutTheCutsTheRangeGoldenIsTheSame);
        // and with the physics line's series DetMath.Atan2 back (a temporary edit, reverted) the same bits. So it is model 33's
        // flight step for step, and against p7d-tether's the area laws' move above.
        // Model 34: 0x1547C72C382DC357. Betaflight's yaw I (ModelVersion 36), audited as the contact flight: on model 34's yaw I
        // it is model 34's pin (the kept test), and without the map's cuts the same bits as with them. It moves from step 10
        // and keeps its course: within 1.3 mm and 0.19° of model 34's path, 4 impacts, at rest at the same 0.2443 m on 8 pairs,
        // 0.4 mm aside.
        // Model 36: 0x81A48D5C4B5E54DA. The buried face (ModelVersion 38, ContactModel.Buried): re-recorded on the same content.
        // On S04's overlapping tiles a skid or support sunk into one tile lay inside a neighbour's side face, and model 36 took
        // that face for a contact. It moves from step 41 (the settle into the straw) and keeps its course: 3 impacts (4), no
        // crash, at rest 8.74 mm into the straw on 8 pairs (8.68 mm on 8), rolled 0.08° (0.09°), 1.0 mm from model 36's
        // resting place, within 24.8 mm and 1.44° of its path (the drift of its liftoff off the mat at step 168, where model
        // 36's buried side faces pushed the skids sideways as they lifted; largest at step 1974, 1.27 m up in the climb).
        // Audited: with the buried face off (a temporary edit, reverted) it is
        // model 36's pin bit for bit, every record and event, and so is every kept audit; without the area laws it is
        // unchanged (WithoutTheAreaLawsTheRangeGoldenIsModel32s: no layer, no buried face); the two kept audits flown with the
        // area laws move to Model33RangeOn38Hash and Model34RangeOn38Hash.
        // On p7f-damage (off model 26), model 26: 0xC79B60F487AEB8B6. p7f-damage 1.2 (ModelVersion 29), on the same content, audited as the contact flight (model
        // 26's pin under the kept test's switches). From step 9 the inertia alone (0x22B183936CE51C3E) moves it by at most 0.2 mm
        // and 0.04°; the pitch alone (0xA86D6A2518B1F903) by 22.0 mm and 0.43° (step 8915, the held bounce over the straw); both by
        // 22.2 mm and 0.45°. It keeps its course: 3 impacts (4), no crash, at
        // rest 0.2326 m up on 21 pairs (0.2323), 0.6 mm from model 26's resting place.
        // Model 29: 0x8F038B8AAEC425E7. p7f-damage 1.3 (ModelVersion 35), on the same content: re-recorded on the 2288-byte record,
        // the strikes' bytes zero: no disc cuts the straw or the soil (0 passes; the props stand 0.25 m over the skids), so over model
        // 29's 1904 bytes it is model 29's pin, every record and event, the strikes on or off (the kept test); 3 impacts, no crash,
        // at rest 0.2326 m up on 21 pairs, as model 29.
        // Model 38: 0x5133425038BAA44E (the line), model 35: 0x00CBE79581238DE7 (p7f-damage). The physics integration 2 with
        // p7f-damage (ModelVersion 40), on the same content: p7f's blades over the straw's area laws and buried face. Audited
        // against each parent with the other side off (both kept tests): without props.blade, over 2008 bytes, model 38's pin,
        // every record and event, the strikes' block zero; without the layers' area laws on model 34's yaw I, over p7f's
        // record, model 35's, the fiber's block zero; without the map's cuts the same bits (WithoutTheCutsTheRangeGoldenIsTheSame),
        // and with the strikes off the same bits (no disc cuts the straw or the soil, 0 passes: the props stand 0.25 m over the
        // skids). It moves from step 9 against the line's (the blades' pitch draws; the inertia alone leaves it 0.2 mm from it)
        // and keeps its course: 3 impacts as the line's, no crash, at rest 8.83 mm into the straw on 8 pairs (8.74 on 8),
        // rolled 0.03° (0.08°), 4.2 mm from the line's resting place, within 23.2 mm and 0.51° of its path (the held bounce
        // over the straw). Against p7f's it moves from step 2 (the area laws; the yaw I alone leaves it 0.4 mm apart): within
        // 58.4 mm and 2.8° of its path, at rest on 8 pairs against 21, 49.5 mm from its resting place.
        // On p7g-legs (off model 26), model 26: 0xC79B60F487AEB8B6. p7g-legs 1.1 (ModelVersion 37), on the same content: re-recorded with the plastic legs.
        // Audited as the contact flight (model 26's pin on model 26's legs, the kept test): it moves from step 1 and keeps its
        // course: 5 impacts against 4, no crash, at rest 0.2319 m up on S04's straw against 0.2323 (0.4 mm deeper), 1.1 mm
        // from model 26's resting place and within 0.50° of its attitude.
        // Model 37: 0x89E6F89885BD7282. p7g-legs 1.2 (ModelVersion 39), on the same content: re-recorded with the legs' side give
        // and their law. Audited as the contact flight (model 37's pin without them over 1904 bytes, the kept test): it moves from
        // step 3 and keeps its course: 4 impacts against 5, no crash, at rest 0.2338 m up on S04's straw against 0.2319 (19.2 mm
        // into the straw) on 18 pairs (21), 18.2 mm from model 37's resting place, within 21 mm and 4.6° of its path; every leg
        // intact.
        // Model 39: 0xDCC264386AF9748F. p7g-legs 1.3 (ModelVersion 41), on the same content: re-recorded as the contact flight.
        // Audited as it (model 37's pin without the legs' law over 1904 bytes, the kept test): it moves from step 3 and keeps its
        // course: 4 impacts (4), no crash, at rest 0.2338 m up on 18 pairs (the same), 18.4 mm from model 37's resting place
        // (39: 18.2) and 2.55° from its attitude; every leg intact.
        // Model 44: 0xF6BBC45EC751E45F (the line), model 41: 0x1FB746560EDFD45A (p7g-legs). The physics integration 2 with
        // p7g-legs (ModelVersion 46), on the same content: p7g's legs on S04's straw's area laws and buried face. Audited
        // against each parent with the other side off (both kept tests), and without the map's cuts the same bits
        // (WithoutTheCutsTheRangeGoldenIsTheSame); with the strikes off the same bits (0 passes), and with the law off the
        // line's 2392 bytes the same. On p7g's legs without their side give (WithModel37Legs) it leaves the line's from step 1,
        // and the side give moves it from step 3, by at most 13.2 mm and 0.67°. It keeps its course: 3 impacts as the line's, no
        // crash, at rest 7.89 mm into the straw on 8 pairs (the line's 8.83 on 8), 16.4 mm from the line's resting place, within
        // 23.6 mm and 0.75° of its path; against p7g's (the straw without its area law, 4 impacts on 18 pairs) 21.7 mm from its
        // rest. Every leg intact.
        // Model 46: 0x5CB64C5900F44C2B. p7g-legs 1.4 (ModelVersion 49): re-recorded with the lever on the 2816-byte record. Audited: with
        // the lever off model 46's pin over its 2784 bytes, every record and event, the lean zero (WithoutTheLeverEachGoldenIsModel48s).
        // With it, it moves from step 3 and keeps its course: within 1.3 mm and 0.10° of model 46's path, 3 impacts, at rest 0.2451 m up as
        // 46's on 8 pairs in S04's straw, 0.6 mm from 46's rest; the largest lean 4.7 mm; every leg intact.
        // Model 50 (the legs' lever on the line) keeps it: no line and no disc in the soil (0 blade passes; the tip slide off, the
        // same bits), and with the lever off it is model 48's, which kept model 46's (WithoutTheLeverEachGoldenIsModel48s).
        // Model 50: 0xBADF8E0897A61D92. p7f-damage 1.3b (ModelVersion 52): re-recorded on the 2944-byte record (the resting blades' block
        // after model 50's 2816 bytes). Audited: over its first 2816 bytes model 50's pin, every record and event, with the props at
        // rest on and off, the block zero on every record (GoldenFlightTests.WithoutARestingPropEachGoldenIsModel50s): no prop rests
        // on anything in it.
        // The skid's frame fore and aft (ModelVersion 54, off model 51's 0xBADF8E0897A61D92, on the 2816-byte record): re-recorded
        // as 0x0C2F2D66883FFDE0 (Model54RangeHash). Audited: without
        // it model 51's pin, every record and event (model 54's WithoutTheFrameEachGoldenIsModel51s; since 60 WithoutTheHangingGiveEachGoldenIsModel50s). With it, it moves from step 3 and keeps
        // its course: within 7.5 mm and 0.13° of model 51's path, 3 impacts, at rest 0.2451 m up as 51's on 8 pairs in S04's
        // straw, 6.3 mm from 51's rest; the largest lean 4.7 mm; every leg intact.
        // Model 59: 0x05FA9D844690C592 (52's, kept through 55, 57 and 59). The physics integration 2 with the skid's frame
        // (ModelVersion 60, 54 merged onto 59): re-recorded, audited as the contact
        // flight (WithoutTheFrameEachGoldenIsModel59s, WithoutTheCutsTheRestAndTheFiberContactsEachGoldenIsModel54s): 54's flight,
        // from step 3, within 7.52 mm and 0.130° of model 59's path, 3 impacts, at rest 0.2451 m up as 59's on 8 pairs in S04's
        // straw, 6.30 mm from 59's rest; the largest lean 4.72 mm (4.66); every leg intact.
        // Model 58 (p7f-damage 1.5, the motors' bells): re-recorded on the 3040-byte record (the bells' block after 2944). Audited
        // case by case: over its first 2944 bytes model 57's pin (Model57RangeHash), every record and event, with the bells on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel57s): no motor's band touches anything in it.
        // Model 63 (the physics integration 2 with the bells, 58 merged onto 60): re-recorded on the 3040-byte record, 60's flight
        // and the bells' zero block. Audited case by case: over model 57's 2944 bytes model 60's pin (Model60RangeHash), every record
        // and event, with the bells on and off, the block zero on every record (GoldenFlightTests.WithoutTheBellsEachGoldenIsModel60s):
        // no motor's band touches anything in it; without the skid's frame model 58's pin (Model58RangeHash) over the whole record
        // (WithoutTheFrameAndTheCutsEachGoldenIsModel58s).
        // Model 64 (the body over the fountain and D01's canister): re-recorded. Audited case by case: on model 63's drone.json (the
        // old coil, Model63Drone; the line out of the old rear cap, Model63Tether) with the fountain through the body it is model
        // 63's pin (Model63RangeHash), every record and event (GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s, flightbench
        // --golden), so the canister is the whole move. It leaves 63's path at step 2 and keeps its course: 4 impacts against 3, at rest in S04's straw on 8 pairs, 8.0 mm and
        // 0.12° from 63's rest, sunk 7.82 mm; the legs' side sway 5.15 mm/s at 12 s (3.87 on the old coil: TheRangeGoldenFlightIsPinned).
        // Model 66 (p7f-damage 1.6, the shafts and the bearings): re-recorded on the 3168-byte record (the shafts' block after 3040).
        // Audited case by case: over its first 3040 bytes model 63's pin (Model63RangeHash), every record and event, with the shafts on and
        // off, the block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel63s): no shaft passes its yield and
        // no bearing its static rating in it.
        // Model 68 (the physics integration 2 with the shafts, 66 merged onto 65): re-recorded on the 3168-byte record. Audited case
        // by case: over its first 3040 bytes model 65's pin (Model65RangeHash), every record and event, with the shafts on and off, the
        // block zero on every record (GoldenFlightTests.WithoutTheShaftsEachGoldenIsModel65s): no shaft passes its yield and no bearing
        // its static rating on the canister either; on model 63's drone through the body, with the 0-force hold and no blade root, model
        // 66's pin over the whole record (OnModel63sDroneEachGoldenIsModel66s).
        // Model 72 (p7f-damage 1.6's losses: the arms in their plane, the gyro's range): re-recorded on the 3344-byte record. Audited
        // case by case (GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s): with the clean sensors model 68's pin over its 3168
        // bytes, every record and event, the arms' block zero on every record; with them the flight moves from step 9 (twist at most 0.047°, arm load 0.0014, the gyro's peak 58.5 °/s), the same 4 impacts, at rest 0.2452 m up on 8 pairs.
        public const ulong RangeHash = 0xC6B3FFB9461F5871UL;

        /// <summary>Model 32's range golden (the pin before p7a-ground 1.12) and its flight in Rain with surfaces.json (1.13's,
        /// rolled 17.6° on wet straw), which the flight without the layers' area laws keeps (WithoutTheAreaLaws).</summary>
        public const ulong Model32RangeHash = 0xC79B60F487AEB8B6UL, Model32RainRangeHash = 0x20B0DB29D33AD18EUL;

        // D-081: S04's far layer (its base maps, mask maps, far slot and cross-fade in its metadata) moves the content from
        // 0x6114242E06475FB5 alone: every worldbench query hash is unchanged and physics reads none of it. With p7a-ground
        // 1.5's micro_elements beside it (S04's metadata names its sidecar; the strands bent once) it is 0xFBE06530579BCBC7:
        // every worldbench query hash, MicroDetailNear's included, and every flight golden unchanged; no flight reads them yet.
        // With p7d-tether 1.4's bending_stiffness_n_m2 in L03_medium's four wire records it is 0xF07BA3CC79D02169: every
        // worldbench query hash and the range flight unchanged; the loader reads none of it yet.
        public const ulong RangeContent = 0xF07BA3CC79D02169UL;
        public const int RangeSteps = 12000;

        /// <summary>Model 13's drone.json (D-043's layout, before D-050), a fixture: the kept audits of models 7–12 fly on it,
        /// those of models 7–11 with model 11's pack table on it.</summary>
        public const string Model13DronePath = "unity/Packages/com.fpv450.sim/Tests/Fixtures/drone-model13.json";

        /// <summary>The goldens at model 63 (the physics integration 2 with the bells), on the old coil, which model 63's drone.json
        /// keeps with its line leaving the rear cap (Model63Drone, Model63Tether) and the fountain through the body
        /// (FlightSetup.TetherFountainThroughBody): GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s. Every kept audit of an
        /// older model flies that drone since model 64, and its "the pins" checks are these.</summary>
        public const ulong Model63PoweredHash = 0xFCDD4AE45B2094A4UL, Model63RainPoweredHash = 0x4A34B87D8D3CB8A9UL,
            Model63ContactHash = 0x4D6E89806C9F0058UL, Model63RangeHash = 0xEFF887E0EA8DF960UL, Model63TetherHash = 0xCBB115FDD3A3A0C2UL,
            Model63TumbleHash = 0xA07B024ABF6C7DD3UL;

        /// <summary>The tether golden on model 63's drone with 61's blocking of the fountain by the drone's body: the merge of 61
        /// onto 63 before the canister (GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s).</summary>
        public const ulong Model63BodyTetherHash = 0xEF11D87EFFD6402BUL;

        /// <summary>Model 63's drone.json (the old coil beneath the frame), a fixture since model 64's canister (Model63Drone). Since
        /// model 68 with model 66's three internals per motor (shaft_modulus_pa, bell_mass_kg, bell_centre_height_m, which the loader
        /// requires): model 66's drone.json, the layout 63's.</summary>
        public const string Model63DronePath = "unity/Packages/com.fpv450.sim/Tests/Fixtures/drone-model63.json";

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

        /// <summary>D-066's kept audit (GoldenFlightTests.OnModel22sDroneEachGoldenIsModel25s): on WithModel22Drone each golden
        /// flies the landing's merge's model 25 pins bit for bit, every record and event (on p7b-airframe--power, model 22's:
        /// 0xA1FF085091A019CD, 0xB10D9A860D4431C0, 0x8C1E6B066F143A67, 0x2A0283BBFE59FD84, 0x80C73DBA130C6903).</summary>
        public const ulong Model25PoweredHash = 0x67788361104F58BBUL, Model25RainPoweredHash = 0x4D876EF3F328F9ADUL,
            Model25ContactHash = 0x0F21FF2A28BA0E16UL, Model25TumbleHash = 0x6712A54D0FDA7F84UL, Model25RangeHash = 0x0D5350CB2E68C6B8UL;

        /// <summary>p7f-damage 1.2's kept audit (GoldenFlightTests.WithThePitchOffAndTheRodsInertiaEachGoldenIsModel26s): with the
        /// blades' pitch draws off and the flapping's inertia at the rod, or without props.blade, each golden flies model 26's
        /// pins bit for bit, every record and event (the tumble has no rotors and keeps TumbleHash); and p7g-legs 1.1's
        /// (GoldenFlightTests.OnModel26sLegsEachGoldenIsModel26s): on WithModel26Legs each golden flies model 26's pins.</summary>
        public const ulong Model26PoweredHash = 0x53F1D10489702031UL, Model26RainPoweredHash = 0x53B90D4307BF1534UL,
            Model26ContactHash = 0xAAEA31DBFBB13710UL, Model26TumbleHash = 0xBCBD33E6B6A94844UL, Model26RangeHash = 0xC79B60F487AEB8B6UL;

        /// <summary>The audit against p7b-airframe--power (GoldenFlightTests.WithoutTheIntegrationTheGoldenFlightsAreThePowerBranchs):
        /// that branch's model 24 pins over model 19's 1808-byte record, which the powered, rain and tumble goldens flown on
        /// drone.json without the interference and D-064's sources keep bit for bit (they touch nothing, so 1.6c's spring and
        /// D-064's wash never act). Its contact and range pins, 0xAF0C71C9193B4326 and 0xFBAC3E4D67B45ACD, need D-064's wash and
        /// 1.6c's spring off too, temporary edits (ContactHash's and RangeHash's notes).</summary>
        public const ulong PowerPoweredHash = 0xCC20DE164C858267UL, PowerRainHash = 0xACF1742E51117659UL, PowerTumbleHash = 0x95E56C6BB49BB744UL;

        /// <summary>physics_materials.json's rows with every layer's area law dropped (p7a-ground 1.12's audit, model 33): each
        /// layer row read per part as its reference foot, as before 1.12. A flight that never reaches a layer's stroke steps on
        /// them as model 32's.</summary>
        public static MaterialRow[] WithoutTheAreaLaws()
        {
            MaterialRow[] rows = (MaterialRow[])ContactScenarios.RepositoryMaterials(out _).Clone();
            for (int i = 0; i < rows.Length; i++) rows[i].AreaLaw = AreaLaw.None;
            return rows;
        }

        /// <summary>The spec with model 26's legs (drone.json before p7g-legs 1.1): 10/8 mm CFRP legs and skids of 11 and 20 g,
        /// each leg's assumed 10 kN/m spring and its 45 N·s/m damper, and the leg shapes' 6600 N/m every way. Pass a freshly
        /// parsed spec: it is changed in place.</summary>
        public static DroneSpec WithModel26Legs(DroneSpec spec)
        {
            foreach (string name in new[] { "leg_rr", "leg_fr", "leg_rl", "leg_fl", "skid_right", "skid_left" })
            {
                ref DronePart p = ref spec.Parts[spec.PartIndex(name)];
                p.Mass = name.StartsWith("leg_", StringComparison.Ordinal) ? 0.011 : 0.020;
                p.Material = "carbon";
            }
            spec.Legs.Stiffness = 10000;
            spec.Legs.Damping = 45;
            WithModel37Legs(spec); // model 26 had no section, so no side give and no failure law
            for (int i = 0; i < spec.Contacts.Shapes.Length; i++)
            {
                ref ContactShape s = ref spec.Contacts.Shapes[i];
                bool leg = s.Role == ContactRole.Body && s.Name.StartsWith("leg_", StringComparison.Ordinal);
                if (leg) s.Stiffness = s.StiffnessUp = 6600;
                if (leg || s.Role == ContactRole.Support || s.Role == ContactRole.Skid) s.Material = "carbon_composite";
            }
            return spec;
        }

        /// <summary>p7g-legs 1.2's kept audit (GoldenFlightTests.WithoutTheFootsGiveEachGoldenIsModel37s): model 37's record, the
        /// record before the legs' state (2072 − 168 bytes), and its pins, which each golden flown without the legs' side give
        /// and their failure law (WithModel37Legs) keeps bit for bit.</summary>
        public const int Model37Layout = 1904;

        /// <summary>p7g-legs 1.3's kept audit (GoldenFlightTests.OverModel39sRecordTheFlightsThatTouchNothingAreModel39s): model
        /// 39's record, the record before the side give, jaws and folds (2296 − 224 bytes), and its pins. Since model 46 the legs'
        /// block follows the line's 2392 bytes, so its 2072 bytes are the first 1904 and then the legs' state: hashed with
        /// Model41Skip at Model28Layout, to this end.</summary>
        public const int Model39Layout = LogFormat.Model44StepSize + 168;
        public const ulong Model39PoweredHash = 0x4907E4D57EB9EB98UL, Model39RainPoweredHash = 0x5AE2CBA32972DD28UL,
            Model39ContactHash = 0xFCA3F1141457625CUL, Model39TumbleHash = 0x7F9A35ED00445E73UL, Model39RangeHash = 0xDCC264386AF9748FUL;
        public const ulong Model37PoweredHash = 0xCD0A523E1C9B94F8UL, Model37RainPoweredHash = 0x15C2A0EF0C503D48UL,
            Model37ContactHash = 0xA3B92A5D52CFE546UL, Model37TumbleHash = 0x5FBFA89AF2217693UL, Model37RangeHash = 0x89E6F89885BD7282UL;

        /// <summary>The spec without the legs' side give and their failure law (model 37's leg law: one spring along the
        /// contact's normal, the stick anchor alone across it): the legs block without its build, so BuildContacts sets neither.
        /// Pass a freshly parsed spec: it is changed in place.</summary>
        public static DroneSpec WithModel37Legs(DroneSpec spec)
        {
            spec.Legs.Tube = null;
            spec.Legs.Clamp = null;
            spec.Legs.TJoint = null;
            return spec;
        }

        /// <summary>The spec with model 22's drone (drone.json before D-066): the ESCs' assumed 40 A current limit, the
        /// motors' assumed 1.0 A no-load current, the packs at 0.92 kg and the electronics at 0.25 kg, on model 26's legs
        /// (WithModel26Legs: model 22's drone had them). Pass a freshly parsed spec: it is changed in place.</summary>
        public static DroneSpec WithModel22Drone(DroneSpec spec)
        {
            WithModel26Legs(spec);
            for (int i = 0; i < spec.Motors.Length; i++)
            {
                spec.Motors[i].EscCurrentLimit = 40;
                spec.Motors[i].NoLoadCurrent = 1.0;
            }
            foreach (DronePack pack in spec.Packs) spec.Parts[pack.Part].Mass = 0.92;
            spec.Parts[spec.PartIndex("electronics")].Mass = 0.25;
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
                Interference = spec.BuildInterference(),
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
        /// layout, for an audit), and with layout it covers each record's first layout bytes only (a parent's shorter record);
        /// wake false flies it without the rotors' wakes (p7b-airframe 1.3d); onRecord sees each step's record before it is
        /// hashed (an audit's step-by-step comparison).</summary>
        public static ulong FlyPowered(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, bool contacts = true,
            bool asymmetry = true, bool ring = true, bool ground = true, bool flapping = true, bool modes = true, WindSetup? wind = null,
            Action<byte[]> onRecord = null, RainLevel rain = RainLevel.Sunny, bool interference = true, bool wake = true, bool motors = true,
            int layout = LogFormat.StepSize, bool yawI = true, Action<FlightSetup> edit = null)
        {
            FlightSetup setup = PoweredSetup(spec);
            if (!interference) setup.Interference = null;
            if (wind != null) setup.Wind = wind.Value;
            setup.Rain = rain;
            if (!modes) setup.Modes = null;
            if (!flapping) setup.Flapping = null;
            if (!contacts) setup.Contacts = null;
            if (!asymmetry) setup.Asymmetry = null;
            if (!ring) setup.VortexRing = null;
            if (!ground) setup.GroundEffect = null;
            WithoutD064(setup, wake, motors, yawI);
            if (layout < LogFormat.StepSize) CleanSensors(setup);
            edit?.Invoke(setup);
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
                hash = Fnv(hash, record, 0, skipAt < layout ? skipAt : layout);
                hash = Fnv(hash, record, skipAt + skipBytes, layout);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, core.State.Step, core.Events[i]);
                    hash = Fnv(hash, e, 0, e.Length);
                }
            }
            return hash;
        }

        /// <summary>
        /// tether.json as model 63 flew it, for a drone without the canister (Model63Drone): the line leaving the old coil's
        /// rear cap centre, (0, −0.053, −0.1), straight aft, (0, 0, −1), over the old 2 mm lip; every other row the file's
        /// (model 64 changed only these three: GoldenFlightTests.OnTheOldCoilEachGoldenIsModel63s).
        /// </summary>
        public static Tether.TetherTable Model63Tether(Tether.TetherTable table)
        {
            var rows = (Tether.TetherRow[])table.Rows.Clone();
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Name == "spool.exit_position_m") rows[i].Values = new[] { 0, -0.053, -0.1 };
                else if (rows[i].Name == "spool.exit_direction") rows[i].Values = new double[] { 0, 0, -1 };
                else if (rows[i].Name == "spool.lip_radius_m") rows[i].Values = new[] { 0.002 };
            }
            return new Tether.TetherTable(rows, table.ContentHash);
        }

        /// <summary>
        /// Flies the contact golden flight: legs on from the floor, the closed-loop pilot up to a metre for 4 s, then down
        /// at 0.5 m/s; with the skids 0.1 m up the sticks are held (roll, pitch and yaw centred) and the drone bounces; 1.5 s
        /// later the throttle is cut and it comes to rest. The floor's belt_bare is surfaces.json's soil (p7a-ground 1.10);
        /// soil false flies the rigid floor (model 26's flight); rain flies it in that rain (1.13's audit: the wet set, the
        /// rain's drops and film), on table if given. Returns its hash, with the core for a report.
        /// </summary>
        public static ulong FlyContact(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, bool asymmetry = true,
            bool ring = true, Action<FlightCore> after = null, bool ground = true, bool flapping = true, bool modes = true, WindSetup? wind = null,
            Action<byte[]> onRecord = null, bool interference = true, bool wake = true, bool motors = true, int layout = LogFormat.StepSize,
            bool soil = true, RainLevel rain = RainLevel.Sunny, SurfaceTable table = null, bool tether = false, bool yawI = true, Action<FlightSetup> edit = null,
            bool recirculation = true, bool fiberContacts = true, bool cuts = true, bool body = true, bool coulomb = true)
        {
            MaterialRow[] materials = ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            FlightSetup setup = ContactScenarios.Setup(spec, materials, mh, true, new Double3(0, stand, 0));
            if (soil) setup.Contacts = spec.BuildContacts(spec.Nominal, materials, mh, table ?? SoilScenarios.Table()); // p7a-ground 1.10
            setup.Rain = rain;
            setup.Wind = wind ?? WindSetup.Of(WindLevel.Calm);
            if (asymmetry) setup.Asymmetry = spec.BuildAsymmetry();
            if (ring) setup.VortexRing = spec.BuildVortexRing();
            if (ground) setup.GroundEffect = spec.BuildGroundEffect();
            if (flapping) setup.Flapping = spec.BuildFlapping();
            if (interference) setup.Interference = spec.BuildInterference();
            if (modes) setup.Modes = spec.BuildModes();
            setup.RotorWake = spec.BuildRotorWake();
            WithoutD064(setup, wake, motors, yawI);
            if (tether)
            {
                setup.Tether = Tether.TetherTable.Parse(System.IO.File.ReadAllText(ContactScenarios.RepositoryFile(Tether.TetherTable.RelativePath)));
                if (spec.Coil.Housing < 0) setup.Tether = Model63Tether(setup.Tether); // model 63's drone (Model63Drone): its rear cap's exit
                setup.TetherCoilPart = spec.Coil.Part;
                setup.TetherSpoolMass = spec.Coil.SpoolMass;
                setup.TetherDesign4Air = !recirculation; // p7d-tether 1.6b: false flies design 4's field, models 31–44's line
                setup.TetherWithoutContacts = !fiberContacts; // p7d-tether 1.7: false flies the plane alone, models 31–50's line
                setup.TetherWithoutCuts = !cuts || !fiberContacts; // p7d-tether 1.8: false flies model 55's line (no cut, no link); a line without its contacts is older still
                setup.TetherFountainThroughBody = !body || !cuts || !fiberContacts; // model 61: false flies model 59's fountain; an older line is older still
                setup.TetherZeroForceHold = !coulomb; // p7d-tether 1.7's part slots (model 62): false flies model 59's chain (its 0-force hold, its safe pass)
            }
            if (layout < LogFormat.StepSize) CleanSensors(setup);
            edit?.Invoke(setup);
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
                hash = Fnv(hash, record, 0, skipAt < layout ? skipAt : layout);
                hash = Fnv(hash, record, skipAt + skipBytes, layout);
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
        /// SampleGround reports Hole and the mat's straw boxes (490 N/m at the reference foot, 3.03 × that under a half-skid by
        /// its area since 1.12, over the patch's soil) are all the ground: legs on,
        /// settling into the straw, the closed-loop pilot up to a metre for 4 s, then down at 0.5 m/s; with the skids 0.1 m
        /// over the straw the sticks are held (roll, pitch and yaw centred) and the mat takes the bounce; 1.5 s later the
        /// throttle is cut and it settles into the straw again. In the map's own Calm wind (WindSetup.ForMap since model 22,
        /// as the player flies it: the obstacles' wakes and the belt's canopy; over the hole from the mat's top since model
        /// 20), the field disposed at the end (its wake grid's worker), with the airframe's effects on as FlyContact's (its toggles, its parent layout's skip and its air, for an audit; onRecord sees each
        /// step's record before it is hashed). rain and table fly it in that rain with surfaces.json (1.13's audit of wet
        /// straw); materials, physics_materials.json's rows edited (1.12's known-wrong straw without its area law). Returns its
        /// hash, with the core for a report.
        /// </summary>
        public static ulong FlyRange(DroneSpec spec, out FlightCore core, int skipAt = LogFormat.StepSize, int skipBytes = 0, bool asymmetry = true,
            bool ring = true, bool ground = true, bool flapping = true, bool modes = true, WindSetup? wind = null, Action<byte[]> onRecord = null,
            bool interference = true, bool wake = true, bool motors = true, int layout = LogFormat.StepSize, RainLevel rain = RainLevel.Sunny,
            SurfaceTable table = null, MaterialRow[] materials = null, bool yawI = true, Action<FlightSetup> edit = null)
        {
            materials ??= ContactScenarios.RepositoryMaterials(out _);
            ContactScenarios.RepositoryMaterials(out ulong mh);
            ContactSpec contacts = spec.BuildContacts(spec.Nominal, materials, mh);
            double stand = ContactScenarios.Standing(contacts);
            Double3 foot = RangeScenarios.Straw;
            FlightSetup setup = ContactScenarios.Setup(spec, materials, mh, true, foot + new Double3(0, stand, 0));
            if (table != null) setup.Contacts = spec.BuildContacts(spec.Nominal, materials, mh, table); // 1.13's audit: wet straw needs the table
            setup.Rain = rain;
            setup.World = new FlightWorld(RangeScenarios.World);
            setup.Wind = wind ?? WindSetup.ForMap(RangeScenarios.World, WindLevel.Calm);
            if (asymmetry) setup.Asymmetry = spec.BuildAsymmetry();
            if (ring) setup.VortexRing = spec.BuildVortexRing();
            if (ground) setup.GroundEffect = spec.BuildGroundEffect();
            if (flapping) setup.Flapping = spec.BuildFlapping();
            if (interference) setup.Interference = spec.BuildInterference();
            if (modes) setup.Modes = spec.BuildModes();
            setup.RotorWake = spec.BuildRotorWake();
            WithoutD064(setup, wake, motors, yawI);
            if (layout < LogFormat.StepSize) CleanSensors(setup);
            edit?.Invoke(setup);
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
                onRecord?.Invoke(record);
                hash = Fnv(hash, record, 0, skipAt < layout ? skipAt : layout);
                hash = Fnv(hash, record, skipAt + skipBytes, layout);
                for (int i = 0; i < core.Events.Count; i++)
                {
                    LogFormat.EncodeEvent(e, core.State.Step, core.Events[i]);
                    hash = Fnv(hash, e, 0, e.Length);
                }
            }
            core.Wind.Dispose();
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
            int layout = LogFormat.StepSize)
        {
            FlightSetup setup = TumbleSetup(spec);
            if (layout < LogFormat.StepSize) CleanSensors(setup);
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
                hash = Fnv(hash, record, 0, skipAt < layout ? skipAt : layout);
                hash = Fnv(hash, record, skipAt + skipBytes, layout);
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

        /// <summary>The clean sensors (model 68's airframe and gyro, p7f-damage 1.6, the losses): the arms stiff in their plane and no gyro range.
        /// Every golden flown over an older model's record (a layout short of LogFormat.StepSize) flies them, so each kept audit of
        /// models up to 68 stays bit for bit; the audit of model 72 against 68 is GoldenFlightTests.WithTheCleanSensorsEachGoldenIsModel68s.</summary>
        public static void CleanSensors(FlightSetup setup)
        {
            if (setup.Modes != null) setup.Modes.ArmInPlane = false;
            setup.GyroRange = 0;
        }

        // D-064's sources off for an audit against model 19: no rotor wake (wake), the motors' resistance, friction and inertia
        // undrawn (motors). The re-ingested wash's published numbers and its torque have no switch (their audit was a
        // temporary edit, GoldenFlights' notes). yawI false: model 34's yaw I (Model34YawI).
        static void WithoutD064(FlightSetup setup, bool wake, bool motors, bool yawI)
        {
            if (!wake) setup.RotorWake = null;
            if (!motors && setup.Asymmetry != null) setup.Asymmetry.ResistanceSigma = setup.Asymmetry.FrictionSigma = setup.Asymmetry.InertiaSigma = 0;
            if (!yawI) setup.Settings.i_yaw = Model34YawI;
        }

        /// <summary>Model 34's yaw I (the kept audits since model 36): FcSettings.Default's i_yaw 30 with Betaflight's × 2.5
        /// undone. It is the double just under 12, whose yaw Ki, (ITERM_SCALE × i) × 2.5, is model 34's ITERM_SCALE × 30 bit
        /// for bit; 12 itself rounds one ulp above it (FlightControllerTests.TheGainsCarryBetaflightsScaling). Every golden
        /// flown on it is model 34's, and every kept audit of an earlier model flies it.</summary>
        public const double Model34YawI = 11.999999999999998;

        /// <summary>The spec without props.blade (p7f-damage 1.2's audit): no blades as elements, no pitch draws and the
        /// flapping's uniform-rod inertia, so every golden flies model 26's bits. Pass a freshly parsed spec: it is changed in
        /// place.</summary>
        public static DroneSpec WithoutTheBlade(DroneSpec spec)
        {
            spec.Blade = null;
            return spec;
        }

        /// <summary>p7f-damage 1.2's own share (the audit's switch): the blades' factory pitch draws off, so every rotor stays
        /// sound and skips its blades' path.</summary>
        public static void WithoutThePitch(FlightSetup setup)
        {
            if (setup.Asymmetry != null) setup.Asymmetry.BladePitchBound = 0;
        }

        /// <summary>The flapping's blade inertia back at model 26's uniform rod, m/N_b·R²/3 of the prop part (the audit's switch
        /// for the inertia's share), in BuildFlapping's arithmetic.</summary>
        public static Action<FlightSetup> WithTheRodsInertia(DroneSpec spec) => setup =>
        {
            if (setup.Flapping == null) return;
            double radius = spec.Props.Diameter / 2, mass = spec.Parts[spec.PartIndex("prop_rr")].Mass;
            setup.Flapping.BladeInertia = mass / spec.Props.Blades * radius * radius / 3;
        };

        public static ulong Fnv(ulong h, byte[] bytes, int from, int to)
        {
            for (int i = from; i < to; i++) h = unchecked((h ^ bytes[i]) * 0x100000001B3UL);
            return h;
        }
    }
}
