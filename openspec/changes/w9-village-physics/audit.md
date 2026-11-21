# Audit: w9-village-physics, task 1.1 (every ground and vegetation interaction on Village)

**Owner:** physics-engineer · **For:** the orchestrator (rewrites tasks.md 2.x from §7), world-builder (2.1), tech-artist (2.5), qa-engineer (3.1) · **Requirements:** PHY-01, PHY-03, PHY-04, PHY-05, PHY-18, PHY-24, PHY-25, PHY-26, WLD-11, AST-10, PLT-04 · **Base:** this branch at 56eba1cd (the pilot's preview 241145ea plus the proposal): model 85, `LogFormat.Version` 72, `QueryVersion` 8 · **No code changed.**

Numbers are labelled: **measured** (run here; the command is in the appendix), **published** (a source read in this task, cited), **carried** (a published value already in the repository's tables, not re-read here), **derived** (computed from those), **assumed** (a named, tunable constant). "Village" is the farmland map (`maps/farmland`, D-100); "the range" is the test range (`maps/test-range`).

Timings: none are judged here. Every run was on a shared machine with other sessions running Unity (CPU load 54 % sampled mid-audit, `Win32_Processor.LoadPercentage`).

The headless runs use a scratch harness (never committed) that compiles the suite's own scenarios (`SoilScenarios`, `SlopeScenarios`, `SimPilot`) against this worktree's sim core, as `tools/flightbench` does. Its modes and outputs are in the appendix.

## 0. What the audit found

**Works on Village, from Village's own data:**
- **The soil under the skids, per cell.** Each 0.49 m sample of `classes/` picks its `surfaces.json` row: bearing, unloading, friction, adhesion, carve, stones and dirt.
  - A 1 m/s landing on Village's softest class (`tilled`) sinks within 0.08–0.36 mm of the same landing on a stub floor of that class (the range's own code path), in dry, Rain and Severe rain (§1.2).
- **Wet ground holds the legs** the same on Village as on a floor of the same class (§1.3).
- **A drone at rest on a slope** gives the same answer on a Village facet as on W3's slope package of the same angle and class, at 10, 14, 20 and 25° (§1.5).
- **Props carving the ground** work on the terrain, and so do the crowns' twig strikes (W3 2.7).

**Gaps:**
- **G1. A runaway lever in the legs' soil anchors (a physics bug).** It makes some cut landings on 10–20° slopes tumble and break legs. In one step the contact moment's size reaches 1.0–6.8 kN·m while the contacts carry only 41–145 N: levers of 19–59 m on a drone 0.6 m across (measured moments and forces, derived levers). The rigid package and Village both show it, so this is not Village's data. It is the class of bug W3 2.2 §3 named ("a slide can run far past the stretch that loaded it") (§1.6).
- **G2. On a 10° side slope the downhill PVC legs crack and part after a 0.5 m/s cut landing,** under 13–22 N, and only then does the drone roll onto its side. So W3 2.2's "the cut landing rolls from 10°" is a leg failure, not the rigid body's tip. Whether that is right is p7g-legs' question (§1.7).
- **G3. Nothing in the sim knows Village's grass.** `grass/map.raw` (2.84 M drawn instances) is read by nothing. `MicroDetailNear` returns 0 elements over the whole map, against 482,356 on the range. No mat or stem acts on the legs, the fiber or the props (§1.12–1.14).
- **G4. Props have never cut grass or straw on either map.** On the range, S04's straw boxes glance (no crush strength) and its elements reach only the fiber; p7a-ground 1.15 was never built. This is the new physics (§5).
- **G5. A crown's twig strike repeats a zero-mass "break" every step.** All 8 blades log `BladeDamaged` (Break, tip 0.9888 R, 0 kg lost) on every step: 200,000 events in 25 s in a hover inside a GreenBush_Dry crown, with the drone held there (§1.15).
- **G6. Furrows are in neither map's physics, nor in Village's drawn geometry.** They are a texture and a normal map. The range never got its tilled zone either (p7a 1.3, never built) (§2.1).
- **G7. The pilot's "a leg sticks" is measured nowhere.** The liftoff harness (p7a 1.17) was never built.
  - On level ground no corner ever holds alone (design 13's 20 ms and 0.5°): wet holds release all four corners within 3 ms of each other, dry ones within 31 ms, never turning the body more than 0.08°.
  - A gentle climb (throttle ≤ 0.61) does not leave wet tilled or meadow soil in 5 s; throttle 0.7 (Rain) and 0.85 (Severe rain) does.
  - The same happens on Village and on a floor (§1.3).
- **G8. A strike's impulse never loads an arm's flap mode at its tip:** the modes advance before the strikes. The camera sees the rigid body's jolt and the rotor's speed dip, not the arm's ring from the hit (§5.3).

**The known gaps from the brief:**
- **Furrows aren't in the physics:** confirmed.
- **S04-type straw counts only as wind roughness:** corrected. Grass and straw are in Village's physics only as the wind preset's uniform 0.3 m cover, the same on every cell and never read from the grass.
- **A cut landing across a side slope rolls from 10°:** confirmed, with the cause corrected (G2).

## 1. The interactions, one by one

| # | Interaction | What it reads | Range | Village | Shown by |
|---|---|---|---|---|---|
| 1.1 | Soil under the skids (Winkler, Wong's unloading, in series with the legs) | `SampleGround`'s surface id → `surfaces.json` row at the rain's moisture (`ContactModel.cs:476–484`, `3248–3262`) | the plane's `belt_straw`; S04's soil boxes; E01's earthworks | **works**: `classes/` per 0.49 m sample (`Terrain.cs:242–246`); the player loads `surfaces.json` (`FlightSessionHost.cs:66`) | §1.2 runs |
| 1.2 | Soft soil the legs sink into | the row's bearing, unload ratio, max sink | `belt_straw` 4.7e6 N/m³ everywhere (the plane); `crater_spoil` mounds in S04 | **works**: `tilled` 4.0e6 dry (softer than the range's plane) on 28.8 % of the map | §1.2 |
| 1.3 | Wet ground holds the legs (cohesive pull, interface adhesion) | the row's wet / saturated sets | as Village | **works**, the same as a floor; the asymmetric "held leg" is unmeasured (G7) | §1.3 |
| 1.4 | Re-landing on its own prints | the footprint ring keyed by surface | as Village | works by the same code path (`ContactModel` footprints, keyed by surface id) | code path; not run on Village |
| 1.5 | Resting on a slope (slides, holds, rolls) | the facet's normal; soil friction and ploughing | no slopes on the range; W3's slope packages | **works**: Village facets match the packages | §1.5 |
| 1.6 | Landing on a slope, along the fall line | the same, plus the legs' anchors sliding | packages only | **gap G1** (on both) | §1.6 |
| 1.7 | Landing across a slope | the same, plus the legs' failure law (p7g) | packages only | rolls from 10° on both: **G2** | §1.7 |
| 1.8 | Leg damage on impact (set, crack, part, detach) | the contact's load at the clamp | as Village | works: terrain-agnostic; events in every tipping landing | §1.6, §1.7 |
| 1.9 | Steps and the ledge | the terrain's faces through `StaticContacts` | none | works (W3 2.2: `flightbench --slopes ledge`) | W3 design §2.2 "The steps" |
| 1.10 | Pitfalls, ruts, micro-relief, furrows | world features (`Feature`, `PitfallEntered`), zones | **none built** (p7a 1.3, 1.6, 1.11 open; the range's ground is one plane, 46 S04 patches and the hole bands) | none: the 0.977 m heightfield is the only relief | §2.1 |
| 1.11 | Compliant straw mat (area law `mat`) | shapes of material `straw` | 195 boxes in each S04 patch, 0.125 m deep | none: 0 `straw` shapes among Village's 1,679 objects | §1.12 |
| 1.12 | Standing stems, straws and twigs as elements | `MicroDetailNear` | 482,356 elements (S04 only) | **0 elements (G3)** | §1.12 |
| 1.13 | The fiber on stems and straws (contacts, slips, hooks) | `MicroDetailNear` (`FiberContacts.cs:1650`) | flight 37: `FiberSlip` on stems ×446, straw ×106; `FiberContact` on stems ×242, straw ×111 (L1 report) | none (G3) | code path, §1.12 |
| 1.14 | Props in grass and straw | nothing reads them | S04's boxes glance, 0 J (`PropStrikes.cs:20–22`; `physics_materials.json` straw: no crush strength) | nothing (G3, G4) | §1.14 |
| 1.15 | Props in crowns (twigs) | material `twig` shells (`PropStrikes.cs:1063–1200`) | V01/V13 crowns | **works** on 51,014 twig shells; bug G5; no leaves | §1.15 |
| 1.16 | Props carving the ground (carve, stones, sand wear, dirt in the bell) | the surface's row: bearing, bulk density, sand share, stone fraction | as Village | **works** on the terrain (W3 2.2 decision 2; `StoneStrike` and `BladeDamaged` in Village landings) | §1.6 runs |
| 1.17 | Body and legs pushing through bushes and crowns | the `twig` row's 30 N/m shells | crowns | works (W3 2.7: GreenBush at 10 m/s, `flightbench --contacts`) | W3 design §2.7 |
| 1.18 | Ground and ceiling effect, recirculation | `RotorSurfaces`: a ray along each axis and `SampleGround`'s plane (`RotorSurfaces.cs:5–10`) | the plane; S04's box tops (the ray meets them) | works on facets; grass is not seen (the soil is the ground) | code path |
| 1.19 | Wind near the ground (the cover's still layer) | `WindPreset.CoverHeight` 0.3 m, d 0.67·h, z0 0.10 m (`WindField.cs:168–170`) | uniform | **uniform, not from the grass**; each point's own ground (W7 1.1) | code path |
| 1.20 | The fiber on the ground | `SampleGround`; a hole's soil cast | the plane, S04, E01 | works over the terrain (W3 2.1, 2.2 §5, 2.4, 2.5; `SlopeTests`) | W3 design; suite |
| 1.21 | Loose pieces (broken legs) on the ground | each tube's own ground (`LoosePieces.cs:356–360`) | as Village | works | code path; `PieceDropped` in the runs |
| 1.22 | The launch stand on soil | a runtime object | the field stand | **absent for a reason:** Village's start (`FlightWorlds.cs:31`) places no stand | code path |
| 1.23 | Holes, trench walls, an asset's own soil shapes | hole bands, soil shapes | E01, Z04, S04 | none yet (W5 adds trenches) | `maps/farmland/README.md` "Holes" |

### 1.2 Soft soil: a landing at 1 m/s, Village against a floor of the same class

The pilot's landing: an air start 1 m up, a 5 s hover, a 1 m/s descent with the sticks held 0.1 m over the ground, and the throttle cut at the touch. This is `SlopeScenarios.Land`'s protocol at 1 m/s, flown 3 s past the touch. The spot is level (0.48° facet; a 7 × 7 grid at 0.1 m of one class and within 1° of one plane). The floor is `FloorWorld` of the same surface, the range plane's own code path. The range has no `tilled` or `meadow_sod` cell, so the floor stands in for "a range cell of that class". Measured:

| Class (bearing dry, N/m³) | Moisture | Peak sink, Village / floor (mm) | At rest after 3 s (mm) | Peak (m/s²) |
|---|---|---|---|---|
| `tilled` (4.0e6) | dry | 6.825 / 6.749 | 6.194 / 6.199 | 63 / 68 |
| | Rain | 8.902 / 9.058 | 8.650 / 8.815 | 57 / 63 |
| | Severe rain | 11.667 / 11.305 | 11.564 / 11.288 | 57 / 63 |
| `meadow_sod` (7.0e6) | dry | 5.714 / 5.646 | 4.207 / 4.349 | 70 / 73 |
| | Rain | 7.960 / 7.931 | 6.968 / 7.080 | 61 / 64 |
| | Severe rain | 9.365 / 9.636 | 8.593 / 9.097 | 59 / 65 |

- **Every pair is within 0.36 mm at the peak and 0.51 mm at rest.** The spec's 1 mm holds. The difference is the 0.48° facet: the drone lands level on a tilted plane, so one skid end touches first (measured: corner loads at rest 11.0–11.9 N on Village against 10.9/12.0 on the floor).
- **Resting disarmed on `tilled`, dry:** each corner carries 10.1–12.7 N and sinks 2.2–3.4 mm (measured). The law alone gives 2.375 mm at the mean 11.61 N (derived, `SoilScenarios.LevelSink`, a 5 mm tube over 0.22 m).
- **Village's classes** (measured, `SampleGround` every 0.5 m, 3,972,049 points): `weeds` 34.34 %, `meadow_sod` 33.04 %, `tilled` 28.82 %, `dirt_track` 2.15 %, `dry_crust` 1.66 %.
  - **Softest:** `tilled`, the fields. Its bearing is 4.0e6 dry, 1.2e6 in Rain and 6.0e5 in Severe rain (surfaces.json, assumed rows). That is softer than the range's whole plane (`belt_straw` 4.7e6).
  - So "somewhere the ground is softer" holds on Village: the fields, about 29 % of the map.
- **No run tunnels, crashes or damages a leg.**

### 1.3 Wet ground and the liftoff

`SoilScenarios.Lift` (p7a design 13's liftoff): armed at rest for 2 s, then the throttle ramps over 0.5 s to a target and holds it for 1 s. Same spots (measured):

| Ramp to | Moisture | Village `tilled`: corners leave (s) / largest pull (N) | Floor `tilled` |
|---|---|---|---|
| hover + 0.1 (0.578) | dry | 0.409–0.465 / 0 | 0.431–0.449 / 0 |
| | Rain | never in 1.5 s, 4 stuck / 5.36 | never, 4 stuck / 4.32 |
| | Severe rain | never, 4 stuck / 5.43 | never, 4 stuck / 4.52 |
| 0.7 | Rain | 0.550–0.574 / 6.57 | 0.552–0.571 / 6.58 |
| | Severe rain | never, 4 stuck / 9.76 | never, 4 stuck / 9.42 |
| 0.85 | Severe rain | 0.523–0.550 / 10.81 | 0.523–0.548 / 10.87 |

- **Village holds the legs as a floor of its class does.** `meadow_sod` and `weeds` were run too, and match the same way (appendix).
- **SimPilot's own climb from wet ground** (throttle at most 0.61) leaves neither wet `tilled` nor wet `meadow_sod` in 5 s (measured). The body even sinks further meanwhile (measured: its height over the unloaded stand goes from −2…−5 mm at 0.5 s to −5…−14 mm at 5 s, the most in Severe rain on `tilled`), which a net upward load should not do: worth a look in task 7.
- **In no run did a single corner hold** (design 13's held leg: ≥ 20 ms after the other three, ≥ 0.5° toward it). The last corner leaves 0–31 ms after the third (dry, hover + 0.1) and 1–3 ms after it once a wet hold breaks, turning the body at most 0.08°, on Village and on the floor alike.
- **So G7:** the pilot's account ("legs stick sometimes, more on soft ground, most of the time in rain"; "a leg or more could get … stuck a bit into that ground") has no measured target anywhere. The design-13 harness (p7a 1.17), with its four known-wrong models, was never built.
  - On level ground the sim gives a whole-drone wet hold that a hard enough throttle pulls free all at once.
  - What makes one corner hold is unevenness (a slope, micro-relief, a glide's drift). Village has slopes; neither map has micro-relief (1.10).

### 1.5 At rest on a slope: Village against W3's slope package

`SlopeScenarios.Settle`: the drone is set down disarmed, its up on the facet's normal, for 3 s. On Village the spot is a facet whose 7 × 7 grid at 0.1 m is one class within 1° of one normal; the package is `SlopeScenarios.Slope` at the same angle and class. "Along" is the nose uphill, "across" the skids level. Measured: moved / moved in the last second / largest tilt / leg loads at the end:

| Facet | Class | Along, Village | Along, package | Across, Village | Across, package |
|---|---|---|---|---|---|
| 10.00 / 9.98° | weeds | 3 mm / 1 mm / 0.1° | 3 / 1 / 0.1° | 19 mm / 5 mm / 0.7° | 20 / 4 / 0.8° |
| 9.39 / 9.43° | meadow_sod | 5 / 2 / 0.2° | 5 / 2 / 0.2° | 19 / 4 / 0.7° | 19 / 4 / 0.7° |
| 14.43 / 14.47° | meadow_sod | 9 / 2 / 0.4° | 9 / 2 / 0.4° | rolls over, 2 legs part | rolls over, 2 legs part |
| 19.68 / 19.70° | meadow_sod | 23 / 2 / 1.4° | 16 / 1 / 1.2° | slides 1.35 m, rolls | 0.86 m, rolls |
| 24.77 / 24.80° | meadow_sod | 192 / 2 / 1.7° | 159 / 2 / 1.8° | slides 2.38 m, rolls | 3.81 m, rolls |

- **Within 1° of tilt and a few millimetres wherever the drone holds.** The 20–25° "across" rows slide and roll on both, by different distances. They are past the legs' failure (G2), so the comparison stops there.
- **Village's slopes are mostly gentle** (measured, the same sampling): 0–2° 20.89 %, 2–5° 59.60 %, 5–10° 17.29 %, 10–15° 2.07 %, 15–20° 0.113 %, 20–25° 0.020 %, 25–30° 0.008 %, 30–38° 0.004 %; steepest 37.56°. Every facet past 15° found lies in the village's levelled plots (x 487–526, z 573–613), among ruins and bushes. The spot finder's 35.65° facet sat inside a GreenBush_Dry crown, so it was dropped.

### 1.6 Landing along a slope: G1, a runaway lever in the soil anchors

The pilot's landing (0.5 m/s, cut at the touch), nose uphill. Measured, peak acceleration / end:

| Facet, class | Village | Package |
|---|---|---|
| 10.00 / 9.98°, weeds | 994 m/s², tumbles to 140°, 3 legs part | 514 m/s², rolls to its side, 2 legs part |
| 9.39 / 9.43°, meadow_sod | 1,898 m/s², crash, tumbles 9.1 m | 24 m/s², rests |
| 14.43 / 14.47°, meadow_sod | 20 m/s², rests | 20 m/s², rests |
| 14.36 / 14.36°, weeds | 27, rests | 27, rests |
| 19.68 / 19.70°, meadow_sod | 212 m/s², stops tilted 34°, 3 legs part | 313 m/s², tumbles, 4 legs part |

(`flightbench --slopes land`'s own rows on this head, meadow_sod: 9.98° along 22 m/s² and rests, 20° along 31 and rests, the same as W3 2.2's table.)

**Traced** (a scratch per-step print of the contact force, the contact moment, each corner's anchor force and soil force, after the touch). On the 9.98° weeds package:
- At 140–150 ms the drone sits nearly level on its downhill legs (tilt 0.6–0.8° from the normal). All four half-skids are sliding (`SkidSoil.Sliding`), at 0–3 N each.
- The contact moment then grows over four steps with almost no force behind it:

| Step after the touch | Contact force (N) | Contact moment about x (N·m) | Anchor force per corner (N) | Body rate x (rad/s) |
|---|---|---|---|---|
| 150 ms | 1.1 | −14.27 | 0.2 / 0 / 0.2 / 0 | −0.81 |
| 153 ms | 3.9 | −76.27 | 0.9 / 0 / 0.9 / 0 | −3.44 |
| 154 ms | 6.6 | −223.68 | 1.5 / 0 / 1.5 / 0 | −7.71 |
| 155 ms | 41.5 | **+1,454.59** | 9.3 / 0 / 9.3 / 0 | **+20.16** |

- **The lever is 35 m:** 1,455 N·m over 41.5 N (derived). The drone is 0.6 m across. Its rotational energy jumps by about 9 J in one step with the motors cut (derived, at a body inertia of order 0.05 kg·m², assumed).
- The next step puts 264 N on two legs, which crack.
- **On Village** the same pattern appears at 152–155 ms after the touch. At 9.39° meadow: (−929, −345, 143) N·m at 54 N (19 m), then (−1,156, 2,939, −6,038) N·m at 116 N (a 59 m lever, derived); three legs part and it crashes. At 10.00° weeds: (−531, −1,549, 3,044) N·m at 145 N (24 m); three legs part and it tumbles.
- **On the 19.70° package** at 871 ms: (−567, −12, −260) N·m at 114 N (5.4 m).
- **No spike is found** (|moment| over 100 N·m in the first 1.5 s) in the 10° and 20° cross-slope landings, nor on the 14.4° Village spots.
- **The candidate** is the soil anchor's slide (`ContactModel.cs:3168–3179`). Past its cone the anchor's stretch grows by (size − kinetic)/k_tt, and the foot, the contact point less that stretch, is where the force acts. `NilStiffness` (1e-6 of the axial share, `ContactModel.cs:2104–2114`) stops only a division by nothing. A k_tt of 1e-5 to 1e-6 of the leg's axial share still runs the stretch to tens of metres.
  - This is W3 2.2 §3's suggested p7g-legs review. Two wider bounds tried there moved p7g's own scenarios.
  - Not traced to the line here: the fix's first step is a print of each corner's stretch.
- **What it means for the pilot's slopes:** some of the falls the sim shows on 10–20° are this bug, not physics. The slope rows can't be compared Village against package (the spec's "within 1°") until it is fixed. Within a degree of 10° the outcome flips (meadow 9.43° rests, weeds 9.98° tips).

### 1.7 Landing across a slope: G2, the downhill legs part

On the 9.98° meadow package across (traced, no moment spike):
- The cut landing first settles on all four legs: tilt 0.3–2.7° from the normal from 160 to 330 ms, legs 13–22 N downhill and 0–4 N uphill.
- The downhill legs then crack at the clamp's edge: `LegCracked` at 238 and 248 ms, moment 2.02 and 2.21 N·m against a first-yield 2.01 and 2.21 N·m.
- They take sets at 311–328 ms and part at 330 and 331 ms: `LegBroken`, 3.04 and 3.67 N·m against 2.85 and 3.12 N·m.
- Only then does the body roll: tilt 3.7° at 360 ms, 18.9° at 434 ms, 52° at 600 ms. It ends on its side (51.5°).
- **On Village** at 9.39° and 10.00° across, the landing logs the same events (`LegSet`, three `LegCracked`, two `LegBroken`, two `PieceDropped`) and ends at 51.1–51.5° against the package's 51.5°: the same outcome within 1° (not traced step by step).
- **The legs are PVC-U tubes,** 10 × 8 mm (drone.json, measured and published), yield 55 MPa. M_y = σ_y·I/c = 3.2 N·m (derived), so the clamp's edge first yields near 2 N·m with its K_t.
- A 2 N·m moment at the clamp is a side force of 9.4 N at the 0.2135 m free length (derived).
- **Gently set down,** the same drone stands across 10° (1.5) with 4.5–17 N on its legs and no event.
- **So W3 2.2's reading is corrected:** the cut landing across a 10° slope does not tip the rigid body (its static tip is 23.99°). The landing sets the downhill PVC legs, and they part under the static load about 0.3 s later.
- **Physical or not is p7g-legs' to judge** (`flightbench --legs`), after G1's fix. The pilot never said legs break on a side slope; "the drone may fall" fits either reading.

### 1.12 Grass, straw and stems: what each map gives the sim

- **The range:** S04's 46 patches.
  - The mat: 195 compliant `straw` boxes a patch, 0.125 m deep, the area law `mat`, 490 N/m on the reference foot (physics_materials.json). It carries a skid until 80 % of its depth, a corner's share reaching that at 12.7 g (p7a design 6, carried).
  - The elements: 482,356 drawn straws, stems and twigs (measured, `MicroDetailNear` r 2,000 m). At one patch, within 2 m: straw 3,634, stem 786, twig 30, leaf 0.
  - Model-79 flight 37 sank into the south verge's straw and crashed in it. Its line lay on S04's stems and straws: 28 of 32 nodes over the patch, held by about 25 standing stems (`docs/reports/lag/model-79-flights.md:16, 117–137`).
- **Village:**
  - `MicroDetailNear` about the map's centre, r 1,000 m, every kind: **0** (measured).
  - Village's 1,679 objects hold 51,014 shapes of material `twig` and none of `straw` or `foliage` (measured).
  - Only S04's metadata has a `micro_elements` sidecar in the repository (`grep`, measured). None of Village's 34 placed variants has one; its plants carry twig shells (W4a).
  - `grass/map.raw` is read by nothing: no runtime file names it (`grep -i grass` over `Runtime/`: no match, measured).
- **What Village draws** (measured from `grass/map.raw` and `map.json`):

| Group (layers) | Drawn height (mesh top × height scale) | Map cells with any | Instances a cell where any (max) |
|---|---|---|---|
| Green (Grass_A) | 0.46–0.67 m | 55.8 % | 2.36 (5) |
| GreenClump (Grass_B, C, D) | 0.45–0.97 m | 2.2–2.3 % each | 1.00 (1) |
| Dry (GrassDry_A, B) | 0.59–0.90 m | 34.0 % each | 1.11 (2) |
| DryClump (GrassDry_C) | 0.68–0.95 m | 9.9 % | 1.00 (2) |
| Burnt (GrassBurnt_A, B, C) | 0.44–1.15 m | 3.2–8.5 % | 1.0–1.61 (3) |
| TallGreen (Grass_A) | 0.93–1.34 m | 7.7 % | 1 |
| TallDry (GrassDry_C, A) | 1.17–1.77 m | 3.8–4.6 % | 1 |
| TallBurnt (GrassBurnt_C) | 1.22–1.77 m | 0.7 % | 1 |

- **By the sim's class at each grass cell's centre** (instances per m², all / tall / dry and burnt; cells with none):
  - `weeds`: 4.05 / 0.31 / 3.18; 0.7 %
  - `meadow_sod`: 3.89 / 0.17 / 0.31; 1.9 %
  - `tilled`: 0.47 / 0.04 / 0.37; 73.2 %
  - `dirt_track`: 0.08 / 0 / 0.03; 97.6 %
  - `dry_crust`: 1.11 / 0.12 / 0.82; 44.3 %
- **The densest tall cell** is at (768.07, 917.48), `weeds`: 4 tall instances (4.2 /m²) and 11 dry or burnt, up to 1.77 m tall.
  - A landing there meets only soil: `MicroDetailNear` gives 0, and the events are `MixerSaturation` ×12 and `Impact` ×2. There is no strike, no layer and no stem (measured).

### 1.14 Props in grass and straw

Nothing on either map makes a turning prop pay for grass or straw:
- **On the range,** a disc in S04's straw boxes meets a "thin compliant row" (no `crush_strength_pa`, physics_materials.json). That is a `Glance`, logged and doing no work (`PropStrikes.cs:20–22, 459`).
- S04's elements are read only by the fiber (`FiberContacts.cs:1650`). p7a-ground 1.14 (stems on the body and legs) and 1.15 (prop strikes on vegetation) are unchecked in its tasks.md.
- `StrikeKind.Element` is written only by the crown twigs (`PropStrikes.cs:1197`).

### 1.15 Props in crowns: works, with G5

- **Works:** W3 2.7's crown twigs. A `twig` shell stands for 7.5 mm twigs at 2.0 m/m³ (assumed), met at the disc's sweep with no draw.
- **G5, measured:** the drone was spawned 1.24 m over a 35.65° facet at (525.79, 612.74), which lies inside GreenBush_Dry's crown (the spot finder did not check objects). From the first step:
  - Every blade of all four props logs `BladeDamaged`, mode Break (index 32 and 33), with A = C = 0.9888 (the tip r/R after the break and the new tip) and B = 0 kg lost. This repeats every step.
  - 200,000 events in 25 s, 150,000 in the "across" twin. The hover stays inside the crown 1.24 m up, and the drone never reaches the ground.
- **The candidate:** `PropStrikes.Twig` (`PropStrikes.cs:1135–1173`) re-breaks a blade at a station that is already its tip, adding the fracture's work each time and logging a zero-mass break.
  - `Breaks` only looks inward of r_c, so the cause is not yet clear.
  - A hover inside a crown at rest is how to reproduce it.
- It also floods the event stream: LOG-01's budget and the step's 64-event buffer.

## 2. The known gaps, confirmed or corrected

### 2.1 Furrows aren't in the physics (W3 2.2): confirmed, and the reason is the look
- No runtime file names a furrow (`grep -i furrow Runtime/`: no match, measured).
- The package has each field's `furrow_axis` and `overgrowth` (`outlines.json`) and the `tilled` class, but no height, pitch or phase (W2 design §5).
- The drawn fields are the AerialMud texture and its normal map on 0.977 m heights. So there is nothing drawn for a furrow's collision to follow (REAL-05, WLD-11; W3 design §2.2 decision 6).
- **The range never had furrows either.** Its ground is one `belt_straw` plane, the 46 S04 patches and the hole bands (`maps/test-range/placements.json` `ground`). p7a 1.3, with the tilled zone, its furrows, ruts and pitfalls, was never built.
- **What `tilled` already gives:** the loose loam's bearing (the softest class, 1.2), its unload ratio 8 and its friction 0.75/0.6.
- **What adding furrows would take:** a world and look unit that draws the relief near the drone and puts it in the package and the world query, not a physics task. At K§7.2's 0.05 m ridges every 0.5 m a skid pair could sit one skid on a crest and one in a trough, 13.7° (W3 2.2 §6, derived). Leaving them out is a direction decision (§7, task 9).

### 2.2 "S04-type straw counts only as wind roughness on the farmland" (W7 1.1): corrected
- W7 1.1's note was about the range: S04's boxes lie under the cover's still layer (d + z0 = 0.301 m), so the drone's points keep the step's ground over a patch's hole (W7 design decision 1).
- **On Village there is no S04-type straw at all,** neither boxes nor elements (§1.12).
- **Grass enters the physics only through the wind preset's cover:** `CoverHeight` 0.3 m, `DisplacementRatio` 0.67 and `Roughness` 0.10 m. These are set once in `WindPreset` (`WindField.cs:168–170`) and are the same over every cell, whatever `grass/map.raw` holds. No map or flight sets them otherwise (`grep CoverHeight =`, measured).
- So Village's 1.2–1.8 m tall dry grass and its bare tilled fields have the same still layer. The wind's cover per cell is W7's, outside this change (§7, suggestion).

### 2.3 "A cut landing across a side slope rolls from 10°" (W3 2.2): confirmed on Village, cause corrected
Confirmed at 9.39° and 10.00° on Village and on the packages (1.7), ending on the side within 1° of each other. The roll follows the downhill PVC legs' crack and parting, not the rigid body's tip (G2). The along-slope landings near 10° are G1's.

## 3. What W2's export gives a stem field, and what it lacks

**It gives:**
- `grass/map.raw`: 14 layers × 1,024² uint8 instance counts over 0.977 m cells. The map only: the surround's grass within 170 m of the edge is not exported (README).
- `map.json` `grass.layers`, per layer:
  - the prefab and the group (Green, GreenClump, Dry, DryClump, Burnt, TallGreen, TallDry, TallBurnt);
  - the width and height scale ranges and the mesh's vertical extent, so the drawn height range (1.12);
  - the total count.
- `classes/` per 0.49 m, and `outlines.json`'s fields with `overgrowth` 0–1.
- The map's identity already covers the grass: the content hash takes `map.json`, which pins every file's SHA-256 (`WorldHash.cs:12–13`, `MapWorldLoad.cs:441–448`).

**It lacks:**
1. **Stems per instance.** Each instance is a tuft mesh; how many blades or culms one draws is in the friend's prefab (the store, local-only, D-089). Village draws at most 5.2 instances/m² of one layer (measured), so stems per m² hang on this number.
   - K§7.2's assumed covers are of the order of 400 stems/m² for meadow grass and 5–15 mm stalks for weeds (carried, assumed).
   - **Needed:** a count per prefab, read locally from the mesh. The count is a derived number that can be pushed.
2. **Each instance's place, scale and tilt.** Unity scatters a cell's instances itself; the export holds counts and scale ranges only. Without the instances, "what is drawn is what is touched" holds per cell and on average, not stem by stem.
   - Unity's `TerrainData.ComputeDetailInstanceTransforms(patchX, patchY, layer, density, out bounds)` "computes and returns an array of detail object transforms for the specified patch and the specified prototype" (Unity 2022.3 Scripting Reference, read). Whether those are exactly the instances the renderer draws, and at which density setting, is to be checked in the editor. If they are, W2's exporter could write them.
3. **Stem diameter, wall and state** (green, dry, burnt). Only the group names imply them: Green the leaf blades of live grass, Dry and Burnt dead culms. Diameters come from published data per kind (§5.1), not from the export.
4. **Leaves in crowns.** W4a's metadata gives each crown a twig shell (`twig`, 51,014 shapes) and no leaf area. A leaf area density per variant (m² of leaf a m³ of crown) would come from the drawn foliage cards' area per crown volume. That is asset-artist's, measured from the vendor meshes locally.

**The rule for 2.1, suggested:**
- Per cell and layer: stems = instances × stems per instance (1).
- The height range is the layer's drawn range; the kind comes from the group: Green and GreenClump → `grass_blade`, Dry, DryClump and Tall* → `dry_culm`, Burnt → `burnt_culm` (new cover rows, §5.1). The class gates nothing: a cell with no drawn grass has no stems.
- Written to the package by the exporter or derived at load by the world, never hand-painted. `validate.py` checks it against the counts.
- With (2), the field could instead hold the drawn instances. That is a cost and size choice for the world-builder (2.84 M instances).

## 4. The model-79 flights' ground interactions (for the scenario list)

From L1's report on the three model-79 logs. Measured there, read here, not re-run: model 85 refuses model-79 logs, and W3 2.2 replayed them on a temporary build.
- **Flight 35:** the line on the yard's gate and house; no straw.
- **Flight 37:** the drone sank into the south straw verge (S04's mat under the legs, its soil box below) and crashed in it. Its line lay on S04's stems and straws: `FiberSlip` on stems ×446 and straw ×106, `FiberContact` on stems ×242 and straw ×111. The hubs over the patch cast `SoilUnder` rays through the boxes.
- **Flight 38:** a 76–199 m laid segment across the north verge's corner, 61 shapes within the margin.

None of the three had a prop strike on straw, which did not exist (1.14). Flight 37's straw is the range's only lived experience of the pilot's "straw" before this change.

## 5. The new physics: straw, grass and leaves on the props

### 5.1 Published stem and leaf properties

Read in this task (the abstract or page read is named; nothing behind a paywall was read):

| Property | Value | Label and source |
|---|---|---|
| Wheat straw, shear strength | 6.81–7.12 MPa (10.2 % w.b.) | published: Tavakoli, Mohtasebi & Jafari (2008), *Comparison of mechanical properties of wheat and barley straw*, CIGR Ejournal X (abstract) |
| Wheat straw, specific shearing energy (per section area) | 21.85–25.74 mJ/mm² = kJ/m² | published: the same |
| Barley straw, shear strength / specific shearing energy | 3.90–4.49 MPa / 18.79–19.85 mJ/mm² | published: the same |
| Wheat stems, 12 varieties at 14–22 % w.b. | shearing strength 4.9–23.0 MPa; cutting energy 27.0–133.3 mJ a stem; modulus 0.1–3.5 GPa; bending strength 4.2–43.9 MPa | published: Edet (2015), *Mechanical properties of hollow and solid wheat stems*, MSc thesis, University of Saskatchewan (abstract) |
| Barley culm, outer / inner diameter | 5.0 ± 0.1 / 3.6 ± 0.2 mm (brittle), 5.2 ± 0.2 / 3.9 ± 0.2 mm (not brittle); wall 0.65–0.7 mm (derived) | published: Kokubo, Kuraishi & Sakurai (1989), *Plant Physiol.* 91: 876–882 (abstract) |
| Barley culm, maximum bending stress | 192 ± 34 and 490 ± 38 g/mm² (1.9 and 4.8 MPa, derived) | published: the same |
| Leaf, work to shear (95 % range) | woody species 0.022–0.539 J/m; herbs 0.033–0.34 J/m; grasses 0.016–1.12 J/m (N < 10) | published: Onoda, the PROMETHEUS protocols site's "Fracture toughness" page (prometheusprotocols.net), from Onoda et al.'s unpublished dataset (page read; a weak source) |
| Leaf mass per area (10th–90th percentile) | deciduous woody 40–120 g/m²; deciduous shrubs 35–110; grasses 35–225; forbs 25–130 | published: Poorter et al. (2009), *New Phytol.* 182: 565–588, as tabulated in Wikipedia's "Specific leaf area" (read there, not in the paper) |
| A rotary mower's blade tip speed, the legal maximum | 19,000 ft/min = 96.5 m/s (derived) | published: 29 CFR 1910.243(e)(2)(ix) (read) |

**Carried** (in `surfaces.json` and `physics_materials.json`, not re-read here):
- wheat straw's E 4.8–6.6 GPa and tensile strength 21–31 MPa (O'Dogherty et al. 1995);
- the 10–60 kJ/m² order of stalk shearing (Persson 1987);
- green hardwood moduli and densities (Wood Handbook 2021);
- leaf mass per area (Wright et al. 2004).

**Not found readable here (G-research for 2.2):**
- green grass blade and culm diameters and their shear;
- weed stalk diameters (K§7.2's 5–15 mm, assumed) and shear;
- dry grass culm diameters;
- the critical impact-cutting speed of unsupported grass stems. Secondary summaries put it between about 25–30 m/s and over 80 m/s by inclination and rake (O'Dogherty and Gale 1986, McRandal and McNulty 1978–80); the publishers refused automated reads, and nothing from them is used here.

**What the table changes now** (for 2.2, not changed here): `surfaces.json`'s straw `cut_energy_j_per_m2` (15 kJ/m², assumed "toward the low end") lies under the published wheat straw values, 21.85–25.74 kJ/m². Barley's are 18.79–19.85. A per-kind range from these replaces the assumption.

### 5.2 One straw on one blade: the hand calculation

Measured, a still-air hover 1 m over a floor: ω = 910.9 rad/s (8,699 rpm). The tip at R = 0.127 m moves at 115.7 m/s, the blade pass is 290.0 Hz, and the rotor holds ½Jω² = 24.89 J at J = 6.0e-5 kg·m² (drone.json, derived).

A straw met at r/R = 0.741: chord 0.01465 m and section angle 10.94° (drone.json's blade table, assumed). There v = ω·r = 85.7 m/s (derived).

| | (a) `surfaces.json`'s straw: d 3.5 mm, wall 0.12 d, 500 kg/m³ (assumed) | (b) barley culm 5.0 / 3.6 mm (published), 500 kg/m³ (assumed) |
|---|---|---|
| Wall section | 4.064 mm² (derived) | 9.456 mm² (derived) |
| Cut, at 21.85–25.74 kJ/m² (published, quasi-static) | 0.089–0.105 J | 0.207–0.243 J |
| Mass within one chord, m_f | 0.030 g | 0.069 g |
| Fling, m_f·v² (p7a design 9's law) | 0.218 J | 0.508 J |
| Work E | 0.31–0.32 J | 0.72–0.75 J |
| Rotor after, ω' = √(ω² − 2E/J) | 905.1 rad/s (−0.6 %) | 897.2 rad/s (−1.5 %) |
| Angular impulse on the body about the rotor's axis, J·Δω | 3.5e-4 N·m·s | 8.2e-4 N·m·s |
| In-plane impulse at the strike, J·Δω/r | 3.7e-3 N·s | 8.7e-3 N·s |
| The body's Δv (4.8 kg) | 0.77 mm/s | 1.8 mm/s |

All rows except the inputs are derived. The edge's cut is the smaller share; the flung mass costs two to five times more.

**A pass through standing straw is where the design must think hardest:**
- **Under one disc** (0.0507 m²) a cover of 400 stems/m² (K§7.2's meadow, assumed) stands 20 stems.
- **Descending at 0.5 m/s,** the disc moves 1.72 mm a blade pass. If every stem under it were cut at every pass, each pass would cost about 20 × (0.09 + 0.026) ≈ 2.4 J: about 700 W a rotor at 290 Hz.
  - The hover's ideal induced power is 115 W a rotor (derived: momentum theory, T = 11.77 N, ρ 1.225).
  - At that rate the rotor's 24.9 J would be gone in about 10 passes, 35 ms. The prop would stall in the grass, not shake.
- **The pilot's account (the quad shakes, straws are cut in front of the camera) means most stems escape the blade.** The published mechanisms that make them escape must therefore be modelled:
  - **The downwash bends them.** v_i = √(T/2ρA) = 9.74 m/s (derived), q = 58 Pa at the disc and about 4× that in the far wake.
    - A 3.5 mm straw 0.5 m tall (EI 0.024–0.032 N·m², O'Dogherty's E, carried) leans 6–8 cm at the disc's q and 24–32 cm at the far wake's (derived: a uniform load on a cantilever, C_d 1.2 assumed).
    - A 1.5 mm grass culm (EI ≤ 1e-3) lies flat (derived).
    - So fine grass is pressed down before the props reach it, and straw stands in their way.
  - **A stem is only cut if the blade is fast enough at its radius.** Rotary mowers cut grass unsupported at tip speeds up to 96.5 m/s (the legal maximum). The props run 86–116 m/s past r/R 0.74 and under 46 m/s inboard of 0.4. An unsupported stem below the critical speed is pushed aside, not cut. The critical speed itself is still owed (5.1).
  - **A stem already cut stands lower** and is met again only as the disc descends to it: twigs' "a slice thinner than the twig costs its share" (`PropStrikes.Twigs`). In a hover over grass the disc cuts its way clear and the cuts stop. That is the transient the pilot sees.
- **The direction of the impulse:**
  - The leading edge cutting takes its force along the blade's motion: the in-plane impulse above.
  - The flung piece leaves along the face it is struck by. A stem rising into the disc from below meets the lower (pressure) face. The face's normal is mostly axial at a 10.9° section angle, so the reaction on the blade has an axial share as large as the in-plane one or larger.
  - The carve's rule (an axial push of cot θ × the tangential impulse, `PropStrikes.cs:62–64`) is a soil face pressing a pitched blade; it is not this. design.md derives the stem's own.

### 5.3 How the impulse reaches the airframe and the camera

**Today a strike** (`PropStrikes.Apply`):
1. **Takes its work off the rotor:** ω′ above. The motor and ESC bring the rotor back over τ_m 43 ms (p6-flight-core 1.7, carried), so the rotor's thrust and torque dip.
2. **Changes the rigid body's velocity and rate directly** (`PropStrikes.cs:353–354`): the reaction about the rotor's axis, the in-plane impulse at the strike point, and the axial push.

**The airframe's modes** (`AirframeModes`: each arm a cantilever, k = 3EI/L³ = 12.73 kN/m derived from its 25 × 6 mm 55 GPa plate over 0.18 m; one arm's tip mass 0.140 kg, so about 48 Hz as a clamped cantilever, derived) are advanced earlier in the step, under the rotors' thrust and the body's relief (`FlightCore.cs:513–514`).
- So **a strike never loads an arm at its tip (G8).** The arm feels the rotor's thrust dip a step later, not the hit.
- The camera's acceleration is taken after the strikes (`FlightCore.cs:536`, `VibrationBands`), so it sees the rigid jolt.

**For the pilot's "shaking":**
- The impulse train arrives at the blade pass (290 Hz at hover), far above one arm's 48 Hz. The arm's direct response is about (48/290)² ≈ 2.7 % of its static one (derived).
- The visible shake will come mostly from the rotors' speed dips through the flight controller's loop and the body's yaw and roll impulses. The cuts per pass, and how unevenly they fall on the four discs, set its size.
- Still, the tip load belongs in the modes for completeness: the hit's axial share at the hub, in the step it happens.

**What the spec's 10 % check needs:** design.md's hand calculation of the body's vibration at the blade pass from the impulses (the cut count per pass times 5.2's per-cut numbers, the four discs' phases), against the run's spectrum.

### 5.4 What the log needs

**The step record already carries each blade's strikes in a step,** with `StrikeKind.Element`, the count (a byte), energy, impulse, diameter, cut energy and radius (`BladeStrikes`, 8 slots).

**The header already logs `surfaces.json`'s cover rows and hash** and `physics_materials.json`'s hash (`LogHeader.cs:164–198`).

**Per-cut events would break LOG-01.** At 20 cuts a pass, 290 passes a second and 4 rotors, that is 23,200 events/s: about 90 MB a minute at about 64 B an event (derived), against LOG-01's 50 MB. So:
- **Aggregate per blade per step** in `BladeStrikes` (no format change if the existing fields suffice).
- **One new event kind, 39,** a rotor's stem episode (Part the rotor; A its energy, B the cuts, C the mean diameter; Point the centroid), at its start and end. This is the forensic record ("the props were in straw from t1 to t2, X J"). The next number after it stays free.
- **The cut bits for the feed come from the sim's step output** (`FeedSignals`: per rotor the cuts this step, their pieces' lengths, radii and the blades' angles), live and in a replay alike. The replay is the same bits, so "a replay of the same log draws the same pieces" follows from determinism. No per-piece log is needed.
- **New cover rows** (`grass_blade`, `dry_culm`, `burnt_culm`, and the leaf's new fields) change `surfaces.json`'s header serialization only if the row layout changes. If it does: `LogFormat.Version` 73.
- **The stem field's rule** is world math: `QueryVersion` 9. Its data is already under the content hash (3).
- **Model:** 86 for the first physics change to merge (G1's fix most likely), and the next free number after it for the stems. The orchestrator assigns them (D-072).

## 6. Bugs found on the way (not this task's to fix)

- **G1** and **G5** above, each with its reproduction.
- **A harness note, not a sim bug:** `SoilScenarios.Land` starts on the ground. In Rain the wet hold keeps the drone down through its 5 s climb (1.3), so its touch detection misreads (a "touch" at 0.005 m/s, or none in Severe rain).
  - It is used only dry in the suite (`SoilTests.cs:302–381`).
  - This audit's wet landings used the air start (`SlopeScenarios.Land`'s protocol) instead.

## 7. The work, ordered

Each task is one agent (D-029). "Checks" are the narrowest set; every physics task also runs `dotnet test tools/simtests`, `dotnet run -c Release --project tools/flightbench -- --golden`, the replay of a recorded flight bit for bit, 0 B, and `python tools/hygiene/precommit.py --tree HEAD`.

1. **[physics-engineer] G1: the soil anchors' runaway lever.**
   - **What:** reproduce it with the two rows in 1.6 (the 9.98° weeds package along; Village (311.19, 50.71) along). Print each corner's stretch, find the line, and bound the anchor's stretch by what the leg and skid can physically reach.
   - **The design question W3 2.2 §3 left:** the trial's force and the cone's slide use different stiffnesses.
   - **Known-wrong:** today's rule.
   - **Checks:** a new test that in no step does the contacts' moment exceed the sum of their forces' sizes times the drone's reach (0.5 m, assumed), over every slope row and this audit's Village spots; `flightbench --slopes`, `--legs`, `--contacts`.
   - **Goldens it could move:** the range golden and every kept audit whose anchors slide; p7g's `LegChangeTests`, `LegReplayTests` and `RangeContactTests` (which slid anchors past both of W3's trial bounds); `SlopeTests`' landing table. Model 86 (suggested).
2. **[physics-engineer, with QA] G2: the legs on a side slope**, after 1.
   - **What:** rerun the cross-slope landings and trace the clamp moments. Decide with p7g-legs' failure law and its sources whether 10 × 8 mm PVC-U legs part about 0.3 s after a 0.5 m/s cut landing across 10°, and record the reading.
   - **Checks:** `flightbench --legs`, `--slopes land`.
   - **Goldens:** none unless the law changes (then p7g's, audited).
3. **[physics-engineer] G5: the crown twig's repeated zero-mass break.**
   - **What:** reproduce it with a hover inside GreenBush_Dry's crown at (525.79, 612.74), 1.24 m up. Fix `PropStrikes.Twig` (a break at the blade's own tip is no break). Bound the events.
   - **Checks:** `ObjectContactTests`, the crown flight of W3 2.7, a new test that a hover in a crown logs no `BladeDamaged` with 0 kg lost.
   - **Goldens:** none expected (the crown flight is not a golden); the 2.7 numbers re-measured. In parallel with 1.
4. **[world-builder] 2.1: Village's stem field** (§3), in parallel with 1–3.
   - **What:** from `grass/map.raw`, `map.json`'s layers and the stems per instance counted locally from each prefab's mesh (asset-artist reads them; only the counts are pushed, D-089). Per cell and layer: stems/m², the height range and the kind. A world query for physics (by point and radius, or per cell), allocation-free.
   - First check in the editor whether `ComputeDetailInstanceTransforms` gives exactly the drawn instances (§3, item 2), and choose per cell or per instance with its cost.
   - **Checks:** `validate.py maps/farmland`, `worldbench --golden` and `--bench`, simtests.
   - **Goldens:** worldbench's farmland cases are unchanged if the field is a new query; `QueryVersion` 9.
5. **[physics-engineer] 2.2: stems on the props,** after 4 (design.md first).
   - **What:**
     - per-kind rows in `surfaces.json` from 5.1, with the research the table still owes: green blades, dry culms, weed stalks, the critical cutting speed;
     - the downwash's bending of stems, and the critical speed against v = ω·r;
     - the cut count per pass as `Twigs`' mean count, with no draw;
     - each cut's work and impulse (5.2), with the direction of the flung piece from the face it meets;
     - the arm modes' tip load (G8), and the rotor's dip and stall;
     - the aggregated log and event 39 (5.4);
     - the hand calculation the spec's 10 % check compares against.
   - **Decide** whether S04's elements on the range feed the same law (they must, or the range's straw stays inert: its golden then moves where a disc meets S04).
   - **Checks:** the spec's straw landing (0.5 m/s into the tallest straw cell, the densest tall cell at (768.07, 917.48) or a TallDry cell), with its spectrum against the hand calculation; a hover over grass that cuts itself clear; bit-exact replay, 0 B, PLT-04 on a quiet machine.
   - **Goldens:** none on the floor; the range golden if S04 joins; model and format as 5.4.
6. **[physics-engineer, with asset-artist] 2.3: leaves in crowns,** after 3 and 5.
   - **What:** each vendor variant's leaf area density from its drawn foliage (asset-artist, measured locally). The leaf's fling (its fresh mass over the chord; LMA 40–120 g/m² for deciduous woody species, published) and cut (work to shear 0.022–0.539 J/m, published). Move `CrownTwigLength`, `CrownTwigDiameter` and the leaf's constants into `physics_materials.json`'s `twig` and a new `leaf` block, each labelled.
   - **Checks:** the spec's crown flight (2 m/s into a crown), `ObjectContactTests`.
   - **Goldens:** the crown flight's numbers, not a golden.
7. **[physics-engineer] The Village ground rows** (the spec's slope and soft-soil scenarios as tests and `flightbench` rows), after 1 and 2.
   - **What:**
     - the slope landing and rest on this audit's Village spots against the slope packages of the same angle and class. The range has no slopes, so the spec's "test-range slope" reads "slope package";
     - the soft-soil landing on `tilled` against a floor of `tilled` (the range has no `tilled` cell);
     - design 13's liftoff harness (p7a 1.17) on Village's classes and slopes, with its four known-wrong models (G7).
   - **Checks:** the rows themselves.
   - **Goldens:** new rows only.
8. **[tech-artist] 2.5: cut bits on camera,** after 5. From `FeedSignals`' per-rotor cuts, drawn as pieces leaving the front discs at the blade's speed. A replay draws the same pieces by determinism. **Checks:** the feed's A/B against a replay; no sim golden.
9. **[orchestrator] Decisions, not tasks:**
   - **(a) Furrows** (2.1): keep them a texture and record why (REAL-05: nothing drawn to follow), or open a world and look unit that draws and stores them.
   - **(b) The wind's cover per cell from the stem field** (2.2): W7's, a suggestion.
   - **(c) The fiber over grass** (1.13: the laid line lies on the soil under 1.2–1.8 m of dry grass). It needs the stem field's stems to act on the line: a later physics task after 5.
   - **(d) Stems and hooks on the legs** (p7a 1.14). The touchdown jitter in standing stems is part of the pilot's "shaking"; a later physics task after 5.
10. **[qa-engineer] 3.1** as written, plus G1's no-runaway test over every slope row, and the liftoff harness's rates against the pilot's account.

## Appendix: commands run and their key output

Worktree `C:\.dev\450fpv\.claude\worktrees\w3-ground`, branch `unity/physics/physics-integration-2--w9-village` at 56eba1cd. The scratch harness lives in the session's scratchpad (`d-village-audit\harness`, never committed): a console project compiling the suite's `SoilScenarios`, `SlopeScenarios`, `SimPilot`, `CrosswindScenario` and their dependencies against `tools/simcore`, run as `dotnet h2/harness.dll <mode>`.

```
dotnet build -c Release tools/flightbench            → 0 Error(s)

harness micro
  farmland: MicroDetailNear about the map's centre, r 1000 m, every kind: 0 elements (true count)
  test range: MicroDetailNear at S04's first patch, r 2 m, Straw: 3634 / Stem: 786 / Twig: 30 / Leaf: 0
  test range: every element, r 2000 m: 482356
  farmland: 1679 objects; shapes of material twig 51014, foliage 0, straw 0

harness survey  (SampleGround every 0.5 m, 3,972,049 points; 29.8 s, loaded)
  weeds 34.34 %, meadow_sod 33.04 %, tilled 28.82 %, dirt_track 2.15 %, dry_crust 1.66 %; facets max 37.56°
  0–2° 20.892 %, 2–5° 59.603 %, 5–10° 17.289 %, 10–15° 2.071 %, 15–20° 0.113 %, 20–25° 0.020 %, 25–30° 0.008 %, 30–38° 0.004 %
  level spots: tilled (715.970, 105.470) 0.48°; meadow_sod (322.290, 32.580) 0.48°; weeds (26.660, 20.000) 0.30°;
               dry_crust (714.120, 106.580) 0.43°; dirt_track (590.170, 108.800) 0.43°
  slope spots: 10° weeds (313.040, 20.000) 10.00°; 10° meadow (311.190, 50.710) 9.39°; 15° meadow (884.690, 291.580) 14.43°;
               15° weeds (586.470, 20.370) 14.36°; 20° meadow (507.290, 572.780) 19.67°; 25° meadow (522.830, 587.580) 24.77°;
               30° meadow (523.940, 596.090) 29.35°; 35° meadow (525.790, 612.740) 35.65° (inside GreenBush_Dry: dropped)

harness grass   (the per-layer and per-class tables of 1.12)
  densest tall cell: x 768.066 z 917.480, 4 tall instances (4.2 /m2), tallest layer top 1.77 m, class weeds
  MicroDetailNear there (r 2 m, every kind): 0 elements
  landing there (facet 4.27°): touch 0.495 m/s, peak 22 m/s2, sink 3.333 mm; events MixerSaturation:12 Impact:2

harness soft2 <x> <z> 1.0   (the table of 1.2; e.g. tilled dry:)
  Sunny farmland: touch 0.929 m/s, peak 63 m/s2, sink 6.825 mm (at rest 6.194) ... events Impact:1
  Sunny floor   : touch 0.929 m/s, peak 68 m/s2, sink 6.749 mm (at rest 6.199) ... events Impact:1

harness soft <x> <z>   (the ground-started SoilScenarios.Land: dry rows within 0.11–0.91 mm of the floor; the wet rows
                        misread their touch, §6)

harness lift <x> <z> [ramp]   (the table of 1.3; e.g.)
  Rain farmland: sinks 6.32/6.32/6.26/6.65 mm; left 1.500 ×4 s; held corner -1 ... pull 5.36 N stuck 4   (ramp 0.578)
  Rain farmland: left 0.552/0.574/0.550/0.571 s; held corner -1 hold 0.003 s turn 0.08 deg ... pull 6.57 N stuck 0   (ramp 0.7)
  SevereRain farmland: left 0.524/0.550/0.523/0.549 s; held corner -1 ... pull 10.81 N stuck 0   (ramp 0.85)
  (meadow_sod at (322.29, 32.58) and weeds at (26.66, 20.00) the same way: Rain and Severe rain stuck 4 at hover + 0.1,
   pulls 4.8–5.1 N on Village, 4.4–4.7 N on the floor)

harness wetlift tilled | meadow_sod   (SimPilot's climb from the ground, 5 s)
  tilled Sunny: 1.0s 0.591 ... 5.0s 1.220 m; Rain: 5.0s -0.010 m, largest pull 5.8 N; SevereRain: 5.0s -0.014 m, 6.1 N; largest throttle 0.61
  meadow_sod Rain: 5.0s -0.005 m, 5.3 N; SevereRain: 5.0s -0.007 m, 5.5 N

harness slope <x> <z>   (the tables of 1.5 and 1.6; e.g.)
  farmland (311.19, 50.71): meadow_sod, facet 9.391°; no object within 2 m; within 0.5 m: normals up to 1.28° off,
    ground -6 to 0 mm off the facet's plane
  landing along farmland: peak 1898 m/s2 ... tilt max/end 176.8/110.4 deg, over; events ... Crash:1 ... LegBroken:3 LegDetached:1
  landing along package : peak 24 m/s2, sink 5.713 mm ... tilt max/end 9.4/0.3 deg, rests; events MixerSaturation:2 Impact:4

harness trace pkg 9.98 weeds 0   (1.6's table)
  t 154 ms CF (-0.0, 5.3, -3.9) CM (-223.68, -0.00, 0.00) anchorF 1.5/0.0/1.5/0.0 soilF 2.9/0.0/2.9/0.0 ... w (-7.71, 0, 0) soil SSSS
  t 155 ms CF (-0.0, 39.8, 11.9) CM (1454.59, -0.00, 0.00) anchorF 9.3/0.0/9.3/0.0 soilF 18.6/0.0/18.6/0.0 ... w (20.16, 0, 0) acc 514
  t 162 ms ... legs 0/226/0/226 LegCracked(p20 ... A2.08 B1.92) LegCracked(p22 ... A2.08 B1.99)
harness trace farm 311.190 50.710 0
  t 152 ms CF (-10.9, 50.9, 12.5) CM (-928.66, -344.86, 142.94) ... soil SSSe LegCracked(p21) LegBroken(p21) PieceDropped(p21)
  t 153 ms CF (8.6, 115.7, -6.4) CM (-1155.56, 2938.54, -6038.31) ... acc 1898
harness trace pkg 9.98 meadow_sod 90   (1.7: no |CM| over 100 N·m; LegCracked at 238 and 248 ms, A 2.02 B 2.01 and A 2.21 B 2.21;
                                        LegBroken at 330 and 331 ms, A 3.04 B 2.85 and A 3.67 B 3.12; tilt 3.7° at 360 ms, 52.2° at 600 ms)

harness probe 525.790 612.740
  (525.79, 612.74): meadow_sod, facet 35.65; objects within 2 m: GreenBush_Dry/twig -0.93 m
  step 17 BladeDamaged part 4 index 32 obj GreenBush_Dry shape 0 mat twig ... A 0.9888 B 0 C 0.9888; body y 1.245 m over the foot
  (all 8 blades every step; the slope run: BladeDamaged:200000 in the along landing, 150000 across)

harness hover
  hover 1 m over the floor, still air: mean rotor speed 910.9 rad/s (8699 rpm), the tip at R 0.127 m 115.7 m/s,
  rotor energy at J 6.0e-5 24.89 J, blade pass 290.0 Hz

dotnet run -c Release --no-build --project tools/flightbench -- --slopes land
  along 9.98°: 0.495, 22, 6.0, 0.12, 0.01, 10.0/0.3 ... across 9.98°: 0.495, 93, 13.8, 0.66, 0.00, 56.0/51.5
  a level terrain lands as the plane: sink 3.2657 against 3.2657 mm, peak 35.77 against 35.77 m/s²: PASS
  flightbench: slopes PASS

grep -rln -i grass unity/Packages/com.fpv450.sim/Runtime     → (no match)
grep -rn -i furrow unity/Packages/com.fpv450.sim/Runtime     → (no match)
grep -rl micro_elements --include=*.json .                    → unity/Assets/_Project/Art/Models/surfaces/S04/S04_patch.assetmeta.json only
grep -rn "CoverHeight\s*=" Runtime, Scripts                   → WindField.cs:168 only
```
