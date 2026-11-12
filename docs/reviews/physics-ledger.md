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

From `docs/requirements.md` (PHB, PHY, LOG, TST, PLT-04, DRN-02/03/07, and WLD-16's physics half). "P" numbers point at §1.

| Req | What it asks, in short | Where it is built | Proven by (tonight) | Status |
|---|---|---|---|---|
| PHB-01 | engine-free core, 1 kHz on its own thread, seeded streams, bit-exact replay; render rate changes nothing | `FlightScheduler`, `FlightCore`, `FlightRandom` | `StepPurityTests` (3, incl. the known-wrong scan), `RenderRateTests`, `StaticInitTests`, `GoldenFlightTests` | **proven** |
| PHB-02 | rigid body from `drone.json`, quaternion, semi-implicit Euler | `RigidBody`, `MassModel` | `RigidBodyTests`, `MassBuildTests` | **proven** |
| PHB-03 | Betaflight 4.5 FC, honest mixer, ESC and motor lags, battery sag, axial-inflow derate; "a loaded drone lifts off near 50–65 %" | `Control/FlightController`, `Propulsion`, `RotorFlapping` | `FlightControllerTests`, `FilterChainTests`, `PropulsionTests`, `EdgewiseThrustTests`. Its 50–65 % liftoff line is stale against D-066 / D-069 (37–53 % hover, the pilot's 30–45 %) | **partly** (the requirement text is stale) |
| PHB-04 | body drag, rotor H-force, coil drag at the coil | `Aero` | `AeroTests` | **proven** |
| PHB-05 | legs as spring-dampers, the Time < 1 latch, crash detection, `tunnel` events as bugs | `ContactModel` | `ContactTests` (18), `RangeContactTests` (37), `RuinContactTests`; tunnel counts in §0 | see §0 |
| PHB-06 | wind stand-in | superseded by PHY-13 | — | superseded |
| PHB-07 | G7 MVP-1: launch, calibrate, fly from ground and stand, replay | the player | the pilot flew models 26–85; the orchestrator's soak replays player logs bit for bit (not re-run here) | **built, unproven here** |
| PHY-01 | Winkler soil, mat, stick anchor, ploughing, prints, wet adhesion and suction | `Soil`, `Layer`, `ContactModel` | `SoilTests` (14), `LayerTests`, `WetGroundTests` (7), `SurfaceTableTests` | **proven** (wet straw and grass on Village: no mat, G3) |
| PHY-02 | landing bounce from compliance and residual thrust; hold leaves, cut stays | as PHY-01 | contact golden's held bounce; p7a 1.18 "Hold or cut on each ground" unbuilt | **partly** (P7) |
| PHY-03 | hooks and stiction at liftoff, one held foot tips the horizon, rates against the pilot's account | anchors, cohesive pull; no leg hooks | nothing; W9 audit G7 measured no lone held corner | **contradicted** (P1) |
| PHY-04 | pitfalls, crater rims, slopes, rubble, furrows (rocking across), leaf-litter slide, char stubble; `terrain_deep`, `bottomless` | slopes only (`ContactModel` on the heightfield) | `SlopeTests`; G1's runaway lever on slopes; no `PitfallEntered`, no `WorldBug terrain_deep` / `bottomless`, furrows deferred (D-102) | **contradicted** (slopes) and **missing** (the rest) |
| PHY-05 | stems and twigs as cantilevers on legs and body; prop strikes with cut energy | props on stems (`PropStems`), twigs in crowns (`PropStrikes.Twigs`); no stems on legs or body | `StemTests` (5), `ObjectContactTests` | **partly** (P12, P13) |
| PHY-06 | launch stand: steel posts, an arm catches or slides, the free stand tips or drags | `LaunchStand` (static) | `fb --contacts` stand rows; no free body (p7a 1.16) | **partly** |
| PHY-07 | motor asymmetry drawn per flight, band computed, steadier when off, never buzzes | `MotorAsymmetry` | `AsymmetryTests` (6) | **proven** |
| PHY-08 | per-rotor ground effect, ceiling, recirculation never still | `GroundEffect`, `RotorSurfaces` | `GroundEffectTests` (6) | **proven** (grass is never the ground for it, W9 audit 1.18) |
| PHY-09 | vortex ring by thrust over weight, hysteresis, logged | `VortexRing` | `VortexRingTests` (5) | **proven** |
| PHY-10 | throttle-punch shake 0.3–0.7 s, depends on descent, abruptness, payload; not every time | `VortexRing`, `AirframeModes`, `FeedSignals` | `ShakeTests` (4), `fb --shake` | **proven** |
| PHY-11 | battery charge and sag; margin gone at the cut-off; low-voltage to the OSD | `Propulsion` pack | `BatteryTests` (3) | **proven** |
| PHY-12 | inertia ratios from the layout; yaw saturates first | `MassModel` | `MassBuildTests`, `DroneSpecTests` | **proven** |
| PHY-13 | log-law, turbulence to 2–4 Hz, obstacle wakes, gap jets, shelter, cavity | `Wind/*`, `HillFlow` | `WindFieldTests`, `MapWindTests`, `CavityTests`, `HillFlowTests`, `CanopyFieldTests`. On Village: hills yes, belts and ruins no (W7 1.2 off the line); the cover is a uniform 0.3 m | **partly** |
| PHY-13a | vorticity, per-rotor gust gradients: yaw swerves, nose-up plus yaw | `RotorInterference`, `RotorFlapping`, `RotorWake` | `fb --scenarios`; targets open (p7c 1.5) | **partly** (P31) |
| PHY-14 | Severe / Windy / Calm meet K§8.6's targets; tools fail known-wrongs | `tools/wind/`, `fb --wind-harness` | Severe 9 of 21, Windy 10, Calm 9 of 9 at model 18 (D-058); not re-graded since; p7c 1.5 and 2.1 open | **partly** |
| PHY-15 | per-flight direction and strength, meander, shifts | `wind.*` streams | `WindFieldTests` | **proven** (P33's off-axis gusts queued) |
| PHY-16 | horizon–heading trade-off emerges | wind + rate kinematics | `WindHarnessTests.TheSplitReadsClipPsRollShare` | **partly** |
| PHY-17 | rain: wet fiber, wet soil and cover, rain rate, air at the camera | `Rain`, `FiberLine` wet film, `SurfaceTable` | `RainTests` (5), `TetherTests.RainMakesItHeavierAndDraggier`, `WetGroundTests` | **proven** |
| PHY-17a | clip P's 22.7° flies 17–22 m/s | `drone.json` aero | `fb --scenarios` cruise row | **proven** |
| PHY-18 | one-way spool, ≤ 32 nodes, ground and vegetation contact, wedge stays finite | `Tether/CableChain`, `FiberLine`, `FiberContacts` | `TetherTests` (12), `CableChainTests` (18), `FiberContactTests` (13). Vegetation on Village: none (G3) | **partly** |
| PHY-19 | cut risk from tension, bend, abrasion against a per-flight strength; the pilot's four causes; reverse sometimes cuts, orbits never (20 seeds) | `FiberSection` draws, `Tether/*` | `FiberCutTests` (15); no 20-seed reverse/orbit harness (p7d 1.11); the edge tear not met (P37) | **partly**, edge **contradicted** |
| PHY-20 | link margin from tension, bend, vibration as dips | `Tether/` link | `FiberCutTests.ASnagDipsTheLink`, `AFreeLineCostsOnlyItsLengthAndItsLip`, `TheBendLossFollowsTheLaw` | **partly** (vibration) |
| PHY-21 | a break loses the link, staged loss, failsafe disarm, logged | `Failsafe`, `FeedSignals` | `FiberCutTests.FromTheBreakToTheDisarm`, `ABreakLeavesAFlyableDrone` | **proven** |
| PHY-22 | thin wire wraps and holds, thick deflects; compliance from span tension | static capsules | none (p7d 1.9–1.12) | **missing** |
| PHY-23 | jagged edges and rebar collide; gaps are the real clear space; turbulence funnels through openings | ruins' and houses' shapes; `GapsNear` never ported | `RuinContactTests.ThroughItsDoorNothingTouchesTheDoorway`; no gap-jet test | **partly** |
| PHY-24 | randomness only as bounded named draws; ground and cover never drawn; master switch logged | `FlightRandom`, 28 streams | `SeedStreamTests`, `AsymmetryTests.TheDrawsAreNamedBoundedLoggedAndReplayed`, each change's draw tests. Missing: the leg stiffness draw (D-086). `soil.stones` draws a stone per soil pass (argued in p7f's design as the soil's fraction) | **partly** |
| PHY-25 | every K§11.8 signal at 50 Hz to the feed; impacts and losses trigger events | `FeedSignals`, `FieldCamera` | `FieldCameraTests`, `ShakeTests`; p7e 1.5, 1.7, 1.8 open | **partly** |
| PHY-26 | prop and motor damage from strikes, persistent, logged, replayed | `BladeStrikes`, `DamagedRotor`, `MotorBell`, `MotorShaft`, `MotorHealth`, `ArmFlex` | `StrikeTests` (26), `DamageTests`, `BellTests`, `ShaftTests`, `HeatTests`, `LossTests`, `fb --damage`; the full harness (p7f 1.7) and the arm on sourced data (model 80, unmerged) are open | **partly** |
| LOG-01 | header, full 1 kHz state, events, ≤ 50 MB a minute | `FlightRecorder`, `LogHeader`, `StepTrace` | `FlightLogTests` (26); no test asserts the size per minute | **partly** |
| LOG-02 | headless bit-for-bit replay; refuses a mismatched world; names the first diverging step | `FlightReplay`, `tools/replay` | `FlightLogTests`, every `…ReplaysBitForBit` test, `GoldenFlightTests` | **proven** |
| LOG-03 | a crash report that ends in a verdict: physics bug or pilot error | `PostFlight` lists events; no verdict | none | **partly** |
| LOG-04 | goldens pass on a second x64 machine; Mono equals .NET | — | Mono vs .NET by the orchestrator's soak (not re-run here); a second machine never | **partly** |
| LOG-05 | logs hold sim data only, under the profile | `FlightSession` | the summary shows the log's full path with the Windows user folder (orchestrator's debt) | **partly** |
| TST-01 | dotnet, EditMode, PlayMode, Python, benches, smoke with zero errors | — | dotnet in §0; EditMode and PlayMode not run tonight (no Unity) | **partly** |
| TST-02 | golden re-records audited case by case | `fb --golden` kept audits | `fb --golden` prints each kept audit (§0) | **proven** |
| TST-03 | statistical targets fail known-wrong models; lengths by false-failure rate | per harness | `AsymmetryTests.IdenticalRotorsFailTheBand`, `VortexRingTests.DescentSpeedAloneFailsIt`, `fb --shake`, `--legs`, `--wind-harness` known-wrongs | **partly** (the liftoff, fiber and damage harnesses that would carry it are unbuilt) |
| TST-05 | determinism: concurrency, batched vs single, Debug vs Release, Unity vs .NET, a second machine | — | tonight Release only; the second machine never | **partly** |
| TST-06 | tests assert literal numbers; QA mutates code to prove each check fails | — | not done tonight; `StemTests.TheStrawLandingCutsNoStemTheSlipstreamLaidOver` is a test that pins behaviour the pilot contradicts (P12) | **partly** |
| PLT-04 | 1 kHz real time, p99 ≤ 300 µs, queries < 60 µs, 0 B, late steps logged | `FlightScheduler`, `StepTimes` | 0 B asserted in the suite; timings owed on a quiet machine (no timing counts tonight) | **built, unproven** (timing owed) |
| DRN-02 | tip to tip 0.574 m nose-first, 0.706 m diagonal, ±5 mm | `drone.json` | `DroneSpecTests` | **proven** |
| DRN-03 | one component spec; the mass and inertia read it; coil mass falls with payout | `drone.json`, `MassModel` | `DroneSpecTests` (57), `MassBuildTests`, `TetherTests.TheDroneGetsLighter` | **proven** (one stale note: P39) |
| DRN-07 | the stand: four steel posts, collision and mass so physics tips and drags it | `D02_launch_stand.json`, `LaunchStand` | posts collide; tipping and dragging unbuilt (p7a 1.16) | **partly** |
| WLD-16 (physics) | per foot merge within 45° over holes; `terrain_deep`, `bottomless` as bugs | buried-face rule only | none for the two world bugs | **partly** |

## 3. Open tasks and debts

**Blocks?** says whether the pilot meets it in a flight on Village or House: **yes** (felt in an ordinary flight), **some** (in a particular manoeuvre or setup), **no** (process, look or a test only).

### 3.1 Unticked OpenSpec tasks of the physics changes

Each open task is classed: **done elsewhere** (the work landed under another task; tick it with a pointer), **superseded** (a decision made it moot), **deferred** (waiting on a decision or a map that isn't flown), or **open** (still to do).

| Change · task | What | Class | Where it went, or why | Blocks? |
|---|---|---|---|---|
| p7a 1.2 | broad phase inside each object | superseded | D-061: a tree inside objects over 256 shapes; S04 stays in the grid | no |
| p7a 1.3 | the range's ground: ruts, furrows, pitfalls, craters | deferred | D-102: furrows wait for a world-and-look unit (W12); House only | some |
| p7a 1.4 | `MoveObject` for runtime objects | open | needed by the free stand (1.16); nothing else | some |
| p7a 1.6 | S04's hidden pits | deferred | House only; with 1.3 | some |
| p7a 1.7, 1.8 | V01 and V13 crowns as elements; limb cantilevers | superseded on Village | D-088: Village's crowns are vendor twig shells (W4a) and W9 2.3's leaves; still open for House's own trees | some |
| p7a 1.8b | the Godot stand-in crowns | superseded | D-088 | no |
| p7a 1.11 | pitfalls, `terrain_deep`, `bottomless`, the 45° merge | partly done | the buried-face rule landed (model 38); `PitfallEntered` and both world bugs do not exist | some |
| p7a 1.12 | compliant layers | partly done | part 1 (area laws, stroke) landed (model 33); the `bark` and `deadwood` limb cantilevers are open ("A limb bends from its root") | some |
| p7a 1.14 | vegetation on the body and legs | moved | W9 2.10 (open) | **yes** |
| p7a 1.15 | prop strikes on vegetation | done elsewhere | W9 2.2 (stems), W3 2.7 (twigs); leaves are W9 2.3 | — |
| p7a 1.16 | the launch stand as a free body | open | legs-off flights lift off fixed posts | some (legs off) |
| p7a 1.17 | the leg spread and the liftoff harness | moved | W9 2.9 (open); the `leg.stiffness` stream is still missing | **yes** |
| p7a 1.18 | landings on real ground: hold or cut, wobble, the restitution known-wrong | open | nothing else covers PHY-02 per ground | **yes** |
| p7a 1.19 | budget, log, Severe-rain replay, Mono goldens | partly done | p7h's step budget; the ground's rows owed | no |
| p7a 2.1, 2.2 | QA; the pilot's MVP | open | 2.2 effectively given by the pilot's flights (D-079, D-082) | no |
| p7b 1.3b | ground effect over the range's queries | superseded | the heightfield (W3) and `RotorSurfaces` now carry it; re-measure in W9 3.1 | no |
| p7c 1.5 | the levels against K§8.6's targets | open | landed with it open (D-058): Severe 9 of 21; pitch about 2.4–3× low | **yes** (in wind) |
| p7c 2.1 | QA | open | owed since the line's merge | no |
| p7d 1.9, 1.10 | soft wires; breaking, dropping, winding | open | wires are static capsules (PHY-22) | some (House's wires) |
| p7d 1.11 | the fiber's acceptance harness | open | the edge-load branch's scaffolding (`3cea05af`) is unmerged and unchecked | **yes** (the fiber's cuts) |
| p7d 1.12 | the wires' harness | open | after 1.9 | no |
| p7d 1.13 | the line's budget and log | partly done | W3 2.1 (the query cost), `farmland-line-step` | no |
| p7d 1.14 | the fiber's look | done elsewhere | drawn in the player since model 44 (handover 11-04) | — |
| p7d 1.15 | the wires' live look | deferred | after 1.9 | no |
| p7d 2.1, 2.2 | QA; the pilot's MVP | open | 2.2 given by D-087's feedback | no |
| p7e 1.5, 1.7, 1.8 | the rolling-shutter read; the Unity verify; QA | open | feed-side; needs the GPU | some |
| p7f 1.4 | the vegetation seam | superseded | W9 2.2's `StrikeKind.Stem` and W3 2.7's twigs | — |
| p7f 1.7 | the damage harness on 40 seeds a cell | partly done | `fb --damage` flies the first cells, 8 seeds | no |
| p7f 1.8 | persistence, log, budget | partly done | damage replays bit for bit (`StrikeTests`, `HeatTests`); the budget rows and LOG-01's size owed | no |
| p7f 1.9 | the summary lists the damage | partly done | p7g 1.5 lists legs and motors; `PostFlight` reads blade and motor state | no |
| p7f 1.10 | draw the damage | open | look | no |
| p7f 2.1, 2.2 | QA; the pilot's MVP | open | 2.2 given by D-079, D-082, D-083 | no |
| p7g 2.1 | QA | open | plus two debts: the anchor's slide stiffness past the sway load; a skid driven 17–19 cm past the ground in a 10 m/s crash | some |
| W3 2.3 | flight 37's straw stretches within PLT-04 | open | owed with a quiet-machine run | some (House's straw) |
| W3 3.3, 4.1 | the scripted flights at W3's model; QA | open | quiet PLT-04 runs owed | no |
| W7 1.2 | wind behind belts and the village | open (built off the line) | on `--w7-wind` at `873854e5`, never merged back; needs a model number | **yes** (in wind) |
| W7 1.3, 2.1 | the laid fiber rests in wind; QA | open | after 1.2 | some |
| W9 2.3 | leaves in crowns | open | crown hovers: the props stand still in the twig shells | some |
| W9 2.4 | G1, the anchors' runaway lever | open (in work) | model 86 | **yes** |
| W9 2.5 | cut bits on camera | open | after 2.12 | **yes** |
| W9 2.6 | G2, legs on a side slope | open | after 2.4 | **yes** |
| W9 2.9 | Village's ground rows and the liftoff harness | open | G7 | **yes** |
| W9 2.10 | stems on the legs and the fiber | open | G3 | **yes** |
| W9 2.12 | the landing in high grass with straw | open (in work) | contradicts the pilot today (P12) | **yes** |
| W9 3.0, 3.1 | the map-wide sweep; QA | open | — | no |

### 3.2 Debts outside the task lists

| Debt | Where it lives | Blocks? |
|---|---|---|
| A contact node inserted inside a member is thrown out | `Tether/FiberContacts` (orchestrator's notes, W3 2.4) | some |
| The near piece after a break never freezes | `Tether/` (W3 2.4 notes) | some |
| Laid turns can still merge on plane worlds | `Tether/` laid trail (W3 2.4 notes) | no (Village is a heightfield) |
| The north prop's cut in the climb (the rotor-air review) | `Tether/`, `RotorAir` | some |
| `farmland-line-step`'s harness takes corners at up to 21 m/s | `tools/flightbench/FarmlandLineCase.cs` | no |
| A crown hover: the props stand still, held by the twig shells (25–39 N a blade) | `PropStrikes` twig shells (W9 2.7 notes) | some |
| The first step in a crown overflows the 64-event buffer: 24 events refused, one a blade break | `Flight/StepTrace` (W9 2.7 notes); a lost event is a hole in LOG-03's forensics | some |
| The pine's arms (needle cards) still flutter in the drawn wind; the physics' shells don't move | `FarmlandTreeWood` (look); a drawn branch where the sim has none | some |
| `GapsNear` was never ported: a wall's opening is the clear space between boxes, no gap record | `World/IWorldQuery` (named, not implemented) | some |
| Crown twig shells felt 0.3–0.7 m before the drawn leaves; twig spheres up to 141 mm off the drawn crown; scanned trunks off their capsules by up to 526 mm | D-096 (Phase A) | **yes** (crowns are hit where nothing is drawn) |
| Four post-crash tunnels red at model 44 (wire 5.475 and 5.52 m Severe, crown 10 m Windy, `InTheWindACompliantShapeIsNoTunnel`) | handover 11-04 §2; tonight's count is in §0 | see §0 |
| The arm on sourced data (shear-strain break, ζ, the gyro's 258 Hz anti-alias filter), model 80 | `--arm-break` `f1071c90`, `d84b7995`, unmerged and unchecked | some |
| The edge load in a reverse (static peel, lip friction), model 81 if physics changes | `--edge-load` `3cea05af`, tests only, unchecked | **yes** (P37) |
| The ESC's sensorless start against a held rotor | handover 11-06 §6.4 (D-086's roof rack) | some |
| Body lift, the pitch gap in Severe, the micro-motion spectrum's shape, a stick-noise floor | handover 11-04 §6.9, D-068 | some |
| Wind gusts from off the forecast's direction | handover 11-06 §6.4 (D-086) | some |
| The canister's mass at PETG or PC (about 20 % over the ABS in the note) | handover 11-06 §4 | no |
| `CableChain`'s hold chatter on the wreck; the part slots' speculative wall and Coulomb's residual | handover 11-06 §6.4 | some |
| The `FlightScheduler` stop-between-steps review; the per-thread `RegionCache` sign-off | final-world-plan A4 | no |
| Flight 36's 2,868 fiber-shape-full events want a physics review | final-world-plan A4 | some |
| The post-flight summary lacks lines for the payout event (14) and the stem episode (39) | `PostFlight.cs` | no |
| The summary shows the log's full path with the Windows user folder | `PostFlight` (LOG-05) | no |
| Tests leak `%TEMP%\fpv450-session-*` folders with flight logs | `SessionTests.cs:49`, `FarmlandFlightTests` | no |
| `pytest tools/map` fails `test_probe_golden`: the header holds map.json's old SHA | `tools/map/golden/farmland-probes.txt` | no |
| `drone.json`'s `spool_mass_kg` note names model 64's exit (−0.170); every value the sim reads is the bent nozzle's (0, −0.1843, −0.1994) | `assets-src/drone/drone.json` line 583 | no (a stale note) |
| `PHB-03`'s "lifts off near 50–65 %" contradicts D-066's 30–45 % | `docs/requirements.md` | no (text) |

### 3.3 The pilot's open physics questions (`pilot-questions.md`)

The yaw kick in the first 50 ms; the roll overshoot (about 29 %); the punch-out tilt; clip P's strip height; the laid fiber at a sharp low turn; legs that part on a 10° side slope (G2); where cut straw shows in the goggles. None blocks a fix: each is asked at a gate (D-024).

## 4. The ranked gap list

(to fill)
