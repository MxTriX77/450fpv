# Physics ledger

QA's recall of everything asked, said or decided about the drone's physics, with its status on this branch and the test that proves it. It decides what gets tested and fixed next.

- **Branch:** `unity/physics/physics-integration-2--w9-ledger`, cut from the Village physics line at `1130a5c0` (W9 2.2, the stems on the props, merged).
- **Evidence:** the runs in §0, made on that commit. Nothing is taken from a handoff's word.

## Status words

| Status | Means |
|---|---|
| **proven** | a test or bench row named in the row asserts it, and it passed in §0's runs |
| **built, unproven** | the code exists; no test asserts the behaviour the pilot asked for |
| **partly** | some of it is asserted; the named part is not |
| **missing** | not built |
| **contradicted** | a test, a run or a measurement shows the opposite |

## 0. The runs

(filled from the runs)

## 1. The pilot's words on physics

Quoted only from the repository's files. Sources: **V** `VISION.md` §2 (point number), **B** `docs/pilot-brief-2026-10-17.md` (answer number), **PR** `docs/reference-notes/pilot-requirements.md`, **K** `docs/knowledge.md`, **H12** `docs/handover-2026-11-12.md` §2 (on `unity/main`), **D-nnn** `docs/decisions.md` (on `unity/main`), **PQ** `docs/pilot-questions.md` (answered list). Tests are `tools/simtests` classes (`Class.Method`) or `flightbench` modes (`fb --mode`). Code is under `unity/Packages/com.fpv450.sim/Runtime/`.

### 1.1 Liftoff and the legs

| # | The pilot's words | Source | Req | Where it is built | Proven by (tonight) | Status |
|---|---|---|---|---|---|---|
| P1 | "the moment you touch out of the ground you may shake a little bit … one or many of them may get randomly stuck for a moment which affects the horizon while you liftoff" | V 1 | PHY-03, PHY-24 | `Contact/Soil.cs` (anchor, groove), `ContactModel` (cohesive pull), `Flight/BuildTolerance.cs` (balance) | none: the liftoff harness (p7a 1.17, W9 2.9) was never built. W9 audit §1.3 measured the opposite: no corner ever holds alone (wet holds release all four within 3 ms, dry within 31 ms, body turned ≤ 0.08°) | **contradicted** |
| P2 | "sometimes a little bit. but based on ground type and how soft it is - more often. if it's raining it sticks most of the times" | B 6 | PHY-03, PHY-01 | as P1; wet sets in `assets-src/surfaces.json` | `WetGroundTests.AWetSkidIsHeldAsItLifts`, `RainSoftensTheGround` (a symmetric hold of all four skids); the rate over grounds is measured nowhere | **partly** |
| P3 | "the legs are plastic, around 25-35cm, they are a little bit soft, that also SOMETIMES adds the stumbling effect when you land" | K 4.2 | DRN-01, PHY-02 | `Contact/LegDamage.cs`, PVC-U legs in `drone.json` (p7g) | `LegDamageTests.ANormalLandingLeavesEveryLegIntact`, `LandingTests.OnTheirSideGiveTheLegsTipTheDroneSooner`; "stumbling sometimes" has no rate test | **partly** |
| P4 | Legs "usually plastic, or something … as flexible as a carbon plastic", hand-made, varying per drone | PQ, D-086 | PHY-24 | `leg.strength.` stream (`LegDamage`) | `LegDamageTests.TheStrengthDrawsCoverTheirBounds`. D-086 also asks the **stiffness** per flight: no `leg.stiffness` stream exists (only a log column name) | **partly** |
| P5 | "every build balances differently (a little nose-down, tail-down or to a side) … tangible on a heavy quad at liftoff and landing" | PQ, D-086 | PHY-24 | `BuildTolerance` (`build.placement`), wired in `FlightSession` | `PlacementTests.ThePlacementIsDrawnBoundedAndLogged`, `AFlightCarriesItsPlacementAndReplaysBitForBit`, `OffCentreTheCoilTipsAtOnce` | **proven** |
| P6 | Launch stand "like a chair upside down, so each chair leg is beneath each motor" | B 1, D-022 | DRN-07, PHY-06 | `Contact/LaunchStand.cs` (static posts) | `fb --contacts` stand rows (posts); the free stand (tip, drag, `MoveObject`, p7a 1.4/1.16) is not built | **partly** |

### 1.2 Landing and the ground

| # | The pilot's words | Source | Req | Where it is built | Proven by (tonight) | Status |
|---|---|---|---|---|---|---|
| P7 | "if you leave the sticks in that fixed position, the plastic landing legs will bounce the drone back up into the air. So you have to catch the right moment" | V 6 | PHY-02 | leg spring-dampers + soil (`ContactModel`, `Soil`) | the contact golden's held bounce (`GoldenFlightTests`, `fb --golden`); "Hold or cut on each ground" (p7a 1.18) never built | **partly** |
| P8 | "when you land the ground it may be unexpected (pitfalls, loose or porous soil, subtle grass which causes jitter)" | V 2 | PHY-04, PHY-01, PHY-05 | loose soil: `Soil.cs` per `classes/` cell. Pitfalls: none (`PitfallEntered` does not exist). Grass jitter: none (p7a 1.14, W9 2.10) | `SoilTests` (14), `fb --contacts` soil rows; nothing for pitfalls or jitter | **partly** (soil only) |
| P9 | "in the Train i could already sense that the drone may fall or its legs may get stuck if i land on a slopy ground" | H12 2 | PHY-04 | `ContactModel` on the heightfield (W3 2.2) | `SlopeTests` (9), `fb --slopes`. Contradicted on landing: W9 audit G1 measured a runaway anchor lever (1.0–6.8 kN·m on 41–145 N, levers 19–59 m) that tumbles cut landings on 10–20° slopes; G2: on a 10° side slope the legs part before the drone rolls | **contradicted** (fix 2.4 in work) |
| P10 | "somewhere the ground may be softer, so a legs or more could get thrusted and get stuck a bit INTO that ground when landing" | H12 2 | PHY-01, PHY-03 | `Soil.cs`, `surfaces.json` per Village cell | `SoilTests` ("Loose soil swallows a fast landing"), W9 audit §1.2 (Village sinks within 0.36 mm of the floor). The sink is proven; "stuck" at the next liftoff is P1 | **partly** |
| P11 | "all of these and other nuances we;ve built and discussed million times before must work properly on the new map too" | H12 2 | W9 spec | W9 change | W9 audit §1 table: 12 of 23 interactions work on Village; pitfalls, furrows, mat, stems on legs and fiber are absent | **partly** |
| P12 | "if i land on a high grass with straw - straws are noticeable cut in front of my quads camera and my quad is shaking" | H12 2 | PHY-05, W9 | `Contact/PropStems.cs` (W9 2.2) | `StemTests.TheStrawLandingCutsNoStemTheSlipstreamLaidOver` **asserts the opposite**: a vertical landing cuts nothing, a creep cuts about 33 stems/s for 0.7 W, the shake is nil (W9 2.12 is fixing it) | **contradicted** |
| P13 | "straws and leaves still dont affect flight physics … a straw or two even tho they are tiny may affect the flight - add wobbling, shaking, props getting stuck a bit" | D-059 | PHY-05 | `PropStems` (stems), `PropStrikes.Twigs` (twigs in crowns) | `StemTests.ACreepCutsWhatItsLeadingEdgesMeet`, `ObjectContactTests.ADroneInACrownPaysForItsTwigsAndDoesNotHang`. Leaves: missing (W9 2.3). Shake from a straw: nil (P12) | **partly** |
| P14 | Landing "falls quite more often than i do in this game"; it "tolerates excessive angles"; on the coil it "tilts super slowly" | D-067 | PHY-02 | `ContactModel` stick spring, `LandingTests` mechanics | `LandingTests.TheDroneTipsWhereItsCentreOfMassPassesOverTheSkid`, `TheCoilRocksAboutItsContact`, `fb --landing` | **proven** (mechanics; no fall rate) |
| P15 | "it falls when landing a bit more often" and "the drone crashes a bit more often (more reasons for it)" | D-079 | PHY-02, PHY-04 | the sum of p7a, p7f, p7d, p7g | no test counts falls or crashes over landings; the rate is not measured | **built, unproven** |

### 1.3 Motors, mass, rates and the feel of flight

| # | The pilot's words | Source | Req | Where it is built | Proven by (tonight) | Status |
|---|---|---|---|---|---|---|
| P16 | "motors … may suddenly start working A TAD BIT asynchronously, and even a tiny offset in aerodynamics causes the drone to mildly yank" | V 4 | PHY-07 | `Flight/MotorAsymmetry.cs` (`motor.kv`, `esc.lag`, `prop.unbalance` …) drawn once per flight | `AsymmetryTests.TheYankIsAsPredictedOnThirtySeeds`, `AsymmetryOnAndOff`, `IdenticalRotorsFailTheBand`. "Suddenly" mid-flight comes only from heat, dirt and damage (P26) | **proven** (per flight) |
| P17 | "large sized drones differ a lot from tiny freestyle FPV aircraft because they are heavy" | V 4 | PHY-12, DRN-03 | `Flight/MassModel.cs` from `drone.json` | `MassBuildTests` (12), `DroneSpecTests` | **proven** |
| P18 | "a tiny, cunning, and elegant randomness" | V 4 | PHY-24 | `Flight/FlightRandom.cs` (28 named streams) | `SeedStreamTests`, `StepPurityTests.NothingOutsideTheStateEntersAStep` | **proven** |
| P19 | "motors may be different just by 0.0004%, randomly, but this tiny difference is visible in micromovements … ESPECIALLY when lifting off or landing" | D-064 | PHY-07, PHY-08 | rotor unsteadiness, `ground.recirc`, `GroundEffect` | `AsymmetryTests.NeverPerfectlyStill`, `GroundEffectTests.TheCloseHoverIsNeverStill`. D-068: 1–4 Hz inside the footage, above 4 Hz 1.5–2.1× too lively (spectrum shape borrowed) | **partly** |
| P20 | Cargo motors "are pretty powerful"; "such drones lift off at 30-45" %; loaded about 3.5–4 kg | D-066 | PHB-03, DRN-03 | `drone.json` power and mass (D-069) | `MassBuildTests`, `PropulsionTests`. Hover 37 / 45 / 53 % at 0 / 1 / 2 kg (D-069): the 2 kg case is outside 30–45 % | **partly** |
| P21 | Roll and yaw "super slow", "ultra smooth and slow"; throttle "a bit more powerful" | D-076 | INP-03, PHB-03 | `Control/FcSettings.cs` defaults = the pilot's profile | `FlightControllerTests`, `fb --rate-tracking` (prints, asserts nothing on feel). The feel is the pilot's at a gate | **built, unproven** |
| P22 | "after pitch backward it's kind taking a long time to adjust altitude" | D-087 | PHB-03 | `RotorFlapping` edgewise thrust (model 76) | `EdgewiseThrustTests.TheClimbDampingIsPadfieldsInHoverAndInForwardFlightAndTheAxialRotorFailsIt`, `fb --pitch-back` | **proven** |
| P23 | Throttle-punch shake "the image vibrates for a mere 0.3–0.7 sec"; "doesnt happen much" | PR-1, B 9 | PHY-10 | `VortexRing`, `AirframeModes`, `FeedSignals.CameraVibration` | `ShakeTests.ShakeOnlyWhenItsCausesMeet`, `TheShakeLastsAboutTheArrest`, `fb --shake` (40 seeds a cell) | **proven** |
| P24 | Settling into own wash: "not a question of speed of descent but mostly how much thorttle power … as opposed to drones weight. wobble - yes, of course" | B 8 | PHY-09 | `Flight/VortexRing.cs` | `VortexRingTests.HeavyOnLowThrottleEntersAndTheHigherThrottleDoesNot`, `DescentSpeedAloneFailsIt` | **proven** |
| P25 | "airmode off. on low throttle we have the minimal motors spinning"; brake on stop off; no return-to-home; "with no video you often still have control" | D-086 | INP-03, PHY-21 | `FlightController` (motor idle), ESC brake, `Failsafe` | `LandingTests.ArmedAndCutFromAHoverItDoesNotTipWithoutDrift`, `FiberCutTests.AVideoLossKeepsControlOnlyABreakEndsIt` | **proven** |
| P26 | Upside down with throttle "the motors begin to fail one after another" (heat, dirt) | D-083 | PHY-26 | `Contact/MotorHealth.cs` | `HeatTests.UpsideDownOnTheGroundWithThrottleTheMotorsFailOneAfterAnother`, `AStalledMotorHeatsAndFails`, `DirtInTheGapDragsThenBinds` | **proven** |
| P27 | A wall strike is "too gameish": "the props don't break" | D-079 | PHY-26 | `Flight/BladeStrikes.cs`, `PropStrikes`, `DamagedRotor` | `StrikeTests.GrazingAWallTrimsTheTipAndPushesTheDroneOff`, `AHeadOnRodBreaksTheBlade`, `fb --damage` | **proven** |
| P28 | "the moment i ARM (even without using any sticks), i should move somehow"; "props seem to be noclip" | D-082 | PHY-26 | `PropStrikes` rest loads (p7f 1.3b) | `RestTests.ArmingWithAPropOnTheRackMovesTheDrone`, `ARestingPropBearsLoad` | **proven** |
| P29 | "inside this house even if it doesn't slug i suddenly feel like the drone has RX loss" | D-087 | PLT-04 | step budget work (p7h, W3 2.1) | `fb --bench` rows exist; no quiet-machine run since; in-player step time unmeasured since the cuts | **built, unproven** (owed on a quiet machine) |

### 1.4 Wind

| # | The pilot's words | Source | Req | Where it is built | Proven by (tonight) | Status |
|---|---|---|---|---|---|---|
| P30 | "fly through narrow holes or among dense woods in a severy wind" | V 3 | PHY-13 | `Wind/WindField`, wakes, cavity, `HillFlow` (W7 1.1) | `MapWindTests` (14), `HillFlowTests`, `fb --scenarios`. Village's belts and ruins have no wake: W7 1.2 is built on its own branch, not on this line | **partly** |
| P31 | Wind must not "JUST make the drone drift … but also cause sometimes drone swirling and swerving in yaw axis … nose may lift up a bit suddenly AND turn in yaw" | PR-3 | PHY-13a | per-rotor gust gradients, `RotorFlapping`, `RotorInterference` | `fb --scenarios` gust rows. The levels miss their targets: Severe 9 of 21 (D-058); pitch about 2.4–3× low (handover 11-04 §4); p7c 1.5 open | **partly** |
| P32 | In a belt "either you yaw but lose direction OR you roll and keep direction, but lose horizon" | K 4.3 Q7 | PHY-16 | wind field + exact rate kinematics | `WindHarnessTests.TheSplitReadsClipPsRollShare`; the belt targets are p7c 1.5's (open) | **partly** |
| P33 | "mostly it went from left, but it would randomly blow from other sides too … unexpected and undefined" | K 4.3 Q2 | PHY-15 | `wind.direction`, `wind.meander`, `wind.shift` streams | `WindFieldTests`; D-086's gusts from off the forecast's direction are still in the queue (handover 11-06 §6.4) | **partly** |
| P34 | "i cant say speed is different, mostly i focus more how to compensate shifts in course because of wind" | B 10 | PHY-17a | `drone.json` aero block | `fb --scenarios` cruise row (clip P's 22.7° flies 20.6 m/s, inside 17–22) | **proven** |

### 1.5 The fiber and wires

| # | The pilot's words | Source | Req | Where it is built | Proven by (tonight) | Status |
|---|---|---|---|---|---|---|
| P35 | "you don't want to do crazy turns, but smooth orbits instead, and you can' go backwards sharply because the optic cable may (or may not …) cut off" | V 5 | PHY-19 | `Tether/` chain, `FiberSection` strength draws | `FiberCutTests` (15). PHY-19's accept (a reverse sometimes cuts over 20 seeds, orbits never) has no harness: p7d 1.11 open | **partly** |
| P36 | What cuts it: "overly sharp reverse, snagging on tress, very sharp lift off, or sudden momentum backwards after lift off" | B 11 | PHY-19 | as P35 | snag: `FiberCutTests.AReverseRoundASnaggedBarbCuts`; liftoff: only `ANormalLiftoffDoesNotCut`; a sharp liftoff and a backward move after liftoff are asserted nowhere | **partly** |
| P37 | "it tears upon the canister hole's edge … they must get used to the quad cutting itself too often" | D-087 | PHY-19 | nozzle lip (`tether.json` `lip_radius_m` 0.3 mm) | last measured on model 79 (handover 11-06 §4): reverses cut 26 of 50 at the **rear blades**, the edge peaks 0.69–1.49 GPa against 3–5 GPa strength; "tears at the hole's edge: not met". The edge-load branch (p7d 1.11 tests) never merged | **contradicted** |
| P38 | "They are designed to fly up to 80-100km/h … a sharp turn increases the chance to cut" | D-087 | PHY-19 | Newton chain (models 77–79) | handover 11-06: cruise to 100 km/h keeps the line; Windy 100 km/h tears 1 of 60. No test in the suite asserts the cruise band | **built, unproven** |
| P39 | The spool's exit "about 25–30°"; "the fiber's ground end is patched into a ground station, so tension matters" | PQ, D-086 | PHY-18 | bent nozzle 27.5° (`tether.json` `exit_direction`) | `TetherTests.TheExitsPullAndItsMoment`. The exit is (0, −0.1843, −0.1994) in `tether.json`, `coil_nozzle` and the generator; only `drone.json`'s `spool_mass_kg` note still names model 64's −0.170 (a stale note) | **proven** |
| P40 | "the wind would shake the optic fiber which led to even more noisiness and flashiness. also a lot of trees + wobbling - this stretches opto" | K 4.3 Q8 | PHY-20 | link margin in `Tether/` | `FiberCutTests.ASnagDipsTheLink`, `TheBendLossFollowsTheLaw` | **partly** (wind shaking the span → margin not asserted) |
| P41 | Wires: "a drone getting tangled in thin wires behaves differently than it does with thick cables"; "they are soft and a bit elastic" | PR-6, D-059 | PHY-22 | wires are static capsules; soft wires p7d 1.9–1.12 not built | none | **missing** |
| P42 | "Fiber on water keeps the signal" | D-039 | PHY-20 | the link margin reads tension, bend and abrasion only, so water never drops it; Village has no water body | none | **built, unproven** |

### 1.6 The camera, the log and the bar

| # | The pilot's words | Source | Req | Where it is built | Proven by (tonight) | Status |
|---|---|---|---|---|---|---|
| P43 | "the drone's camera must be sensitive to micro movements of the whole drone" | D-059 | PHY-25 | `Flight/FieldCamera.cs`, `PoseBuffer`, `AirframeModes` (p7e) | `FieldCameraTests` (6). The rolling-shutter read and the in-Unity verify are open (p7e 1.5, 1.7, 1.8) | **partly** |
| P44 | "if for example i test and i fuck up my drone - i must know what actually happened in terms of physics to be sure it's not a bug" | V §4 | LOG-01…03 | `Log/FlightRecorder`, `FlightReplay`, `tools/replay`, `PostFlight` | `FlightLogTests` (26), the goldens' bit-for-bit replays. No tool gives LOG-03's verdict ("physics bug, or pilot error") | **partly** |
| P45 | "no more fix, test, fix, and the sluggishness back again" (a gate before any beta) | D-091 | TST, PLT-04 | Phase A (not started) | none | **missing** |
| P46 | "we must have top-notch realistic physics no one implemented in any FPV sim out there yet" | PR-3 | all | — | this ledger | — |

## 2. Every physics requirement

(to fill)

## 3. Open tasks and debts

(to fill)

## 4. The ranked gap list

(to fill)
