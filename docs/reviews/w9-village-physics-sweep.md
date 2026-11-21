# W9 3.0: the map-wide landing sweep

QA's first run of `flightbench --sweep` (OpenSpec `w9-village-physics`, task 3.0). It ran on Village (`farmland`) and on House (`test-range`) at seed 1, in Calm air. Nothing is fixed here: each finding in §4 is a task for the physics engineer.

- **Branch:** `unity/physics/physics-integration-2--w9-sweep`, from `6062b9cc` (the physics line with tonight's fixes: the anchors' reach (model 86), the stems on the props, and the wind's wakes behind the ruins and hedges (model 88)).
- **The sweep's code:** `tools/flightbench/SweepCase.cs` and `unity/Packages/com.fpv450.sim/Tests/SweepScenarios.cs`, with its tests in `SweepTests.cs`. It reads the core and never changes it: **no hook was added to the sim core.**
- **Row files:** every failing row is in `w9-village-physics-sweep/farmland-400-1-failing.tsv` (502 rows) and `w9-village-physics-sweep/test-range-400-1-failing.tsv` (483 rows). Each row gives the point, its map position, class, slope, heading and flight seed, and each check as `ok`, `F@<first failing step>` or `-` (not judged), with that check's worst value.
- **Timing:** this machine was shared and not on AC power, so no time here counts as evidence (PLT-07).

## 1. What the sweep does

**The points.** Each map's own rectangle is sampled uniformly from the seed: Village's `map_rect_m` is 1 × 1 km, and the range's `extent_m` is 512 × 512 m.
- A point counts as a landing spot if a ray from above meets the ground first. On the range that may also be a ground patch: S04's straw or E01's cut.
- A point is dropped if anything else lies in a 0.5 m column above it: a crown, a roof, a wall, or the side of a cut.
- Each point is classed by its ground class (the range's patches by their object), its slope band (0–5, 5–10, 10–15, 15–20, 20–30 and 30°+) and, on Village, its stem stratum: bare, the short layers, or a tall layer (`map.json`'s Tall* groups).
- The first 400 points are in proportion to area. More points from the same stream then bring every class, band and stratum to at least 20. Village needed 88 more, mostly on the 15°+ slopes beside the village. The range needed 48 more, on the patches.

**The manoeuvres.** Each manoeuvre at a point is its own flight with its own drawn heading and flight seed:
- **Land:** hover at 1 m, descend at 0.3 m/s, and ramp the throttle to 0 over 0.5 s after the touch. **Liftoff** follows in the same flight once the drone is at rest (or 10 s after the touch): a fresh pilot climbs 1.5 m.
- **Cut:** the 0.5 m/s landing with the throttle cut at the touch (`SlopeScenarios.Land`'s).
- **Drop:** throttle 0 from a 1 m hover, still armed.
- **Slide:** from 6 m back, accelerate to 2 m/s along the heading, descend at 0.5 m/s, and cut at the touch.

Each flight uses the pilot's loadout (L1: 2 kg, legs on) with every model of the player's flight except the fiber line (`FarmlandLineCase`'s setup without its tether). The air is Calm with the map's own obstacles (`WindSetup.ForMap`).

**The invariants.** Every step is judged, and each check gives its own verdict:

| Check | Fails when | Limit and where it comes from |
|---|---|---|
| Finite | a NaN or ∞ appears in the state or the trace, or the drone passes \|v\| 50 m/s, \|ω\| 100 rad/s or 1 km from its start | assumed |
| Ground | a frame part, support or skid lies further past the ground's (or a soil shape's) surface than its own allowance | allowance = 20 mm (assumed) + its soil's sink this step + its leg's compression and lean (the trace's); frame parts get the 20 mm only |
| Lever | \|M_contact\| > 0.5 m · \|ΣF_contact\| | the spec's rule for G1, read as `AnchorReachTests` reads it (with the vector sum, which is stricter than the sum of the sizes) |
| Energy | the body's mechanical energy since the start passes the rotors', the air's and gravity's work by more than 0.05 J | the residual is ΔE − W(all forces but the contacts), summed. `RigidBody`'s kick makes it exact to rounding, so what is left is the contacts' net return. Starting in the air, a passive contact can never make it positive |
| EventsOnce | an event (kind, part, index, object, shape) is logged twice in one step | G5's kind |
| EventsKept | the step's 64-event buffer refuses any event | |
| Tunnel | a `Tunnel` or `WorldBug` event | always a bug |
| Peak | a Land or Cut reaches 2 × 490.3 m/s² after the touch | `--slopes`' land rows |
| Rest | the drone does not hold under 0.05 m/s and 0.1 rad/s for 0.5 s within 10 s of the touch, or never touches | the spec's 10 s |
| Lifts | the liftoff does not leave the ground within 5 s | the held-leg harness's 5 s; judged only where the landing ended under 45° of tilt |
| Replay | each flight's log, replayed headless through `FlightReplay`, does not match bit for bit | |
| Alloc | a step after the first second allocates anything | 0 B |
| Throws | the core throws | |

**Determinism.** The rows' SHA-256 leaves out only the allocation column (see finding 9).
- `--sweep test-range 12 5` gives `9c0342d3…` on 1 thread and on 4.
- The 400-point range sweep gives `d9a4e472…` on 8 threads and on 6.
- The same seed gives the same points and rows (`SweepTests.TheSameSeedGivesTheSameRows`).

**The known-wrong** (`SweepTests.WithoutTheReach_TheSweepFailsTheLever`): the sweep's own Cut, flown nose uphill at four of the audit's Village slope spots.
- With `ContactModel.AnchorReach` off (model 85's slide), it fails the Lever check at all four: levers of 41.3, 5.6, 17.0 and 15.8 m.
- With the reach on, it passes at all four: levers of 0.28, 0.24, 0.34 and 0.38 m.
- The Energy check also catches model 85 at three of the four (+0.97 to +2.68 J). The two checks are independent.

## 2. How to run it

```
dotnet run -c Release --project tools/flightbench -- --sweep farmland 400 1          # Village, seed 1
dotnet run -c Release --project tools/flightbench -- --sweep test-range 400 1        # House
  [--threads N] [--point I | --from I] [--out rows.tsv] [--wind windy] [--no-replay]
```

`--point I` flies one point's four flights and prints each row in full, with the flight's events by kind and first step. It is the replay command for every row below.

| Run | Points | Flights | Threads | Wall time | Rows SHA-256 (without the allocation column) |
|---|---|---|---|---|---|
| farmland 400 1 | 488 | 1,952 (2,440 rows) | 8 | 44 min 22 s | `257ed6aa…` |
| test-range 400 1 | 448 | 1,792 (2,240 rows) | 6 | 17 min 29 s | `46b0758c…` |

The farmland rows were flown on `ac51342c`'s build. The two later commits change only how the range's patches are drawn: at the head the farmland draw is the same (472,915 candidates, 488 points), and the flights are unchanged. The range rows are the head's.

On Village a flight takes about 11 s of one thread, its replay included. On the range it takes about 3.5 s. The farmland is slower because of its stem field, its wind obstacles and its terrain queries. **A 400-point sweep takes about 45 minutes on Village and about 20 on the range, on 8 threads of this shared machine.**

## 3. The counts

Each cell is failed/judged. The full tables, by class, band and stem stratum, are what the bench prints.

**Village (farmland), 2,440 rows:**

| | Finite | Ground | Lever | Energy | Events once | Events kept | Tunnel | Peak | Rest | Lifts | Replay | Alloc | Throws |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Land | 0/488 | 27/488 | 0/488 | 0/488 | 0 | 0 | 0 | 0/488 | 0/488 | | 0/488 | 6/488 | 0 |
| Liftoff | 0/488 | 0/488 | 0/488 | 0/488 | 0 | 0 | 0 | | | 0/406 | 0/488 | 1/488 | 0 |
| Cut | 0/488 | 29/488 | 0/488 | 0/488 | 0 | 0 | 0 | 0/488 | 1/488 | | 0/488 | 3/488 | 0 |
| Drop | 0/488 | 39/488 | 0/488 | 0/488 | 0 | 0 | 0 | | 4/488 | | 0/488 | 1/488 | 0 |
| Slide | 0/488 | 377/488 | 1/483 | 0/488 | 0 | 0 | 0 | | 14/488 | | 0/488 | 47/488 | 0 |

By class, Ground fails: meadow_sod 214/955, weeds 150/735, tilled 96/550, dry_crust 7/100, dirt_track 5/100. By band: 0–5° 279/1695, 5–10° 50/345, 10–15° 17/100, 15–20° 30/100, 20–30° 42/100, 30°+ 54/100. By stems: bare 64/475, short 322/1465, tall 86/500. No other check depends on class or stratum.

**House (test-range), 2,240 rows:**

| | Finite | Ground | Lever | Energy | Events once | Events kept | Tunnel | Peak | Rest | Lifts | Replay | Alloc | Throws |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Land | 0/448 | 13/448 | 1/448 | 0/448 | 0 | 0 | 0 | 0/448 | 0/448 | | 0/448 | 3/448 | 0 |
| Liftoff | 0/448 | 0/448 | 0/448 | 0/448 | 0 | 0 | 0 | | | 2/429 | 0/448 | 5/448 | 0 |
| Cut | 0/448 | 13/448 | 2/448 | 1/448 | 0 | 0 | 1/448 | 0/448 | 0/448 | | 0/448 | 6/448 | 0 |
| Drop | 0/448 | 19/448 | 1/448 | 0/448 | 0 | 0 | 0 | | 4/448 | | 0/448 | 2/448 | 0 |
| Slide | 0/448 | 403/448 | 2/448 | 7/448 | 0 | 0 | 1/448 | | 3/448 | | 0/448 | 67/448 | 0 |

By class: belt_straw (the plane) 1,960 rows; E01's cut 180 rows (all six of its Lever failures but one, both Tunnels, 70 Ground); S04's straw 100 rows. The range has no slope but E01's 22° ends (20–30° band, 100 rows).

**How the landings end** (rows that end more than 45° from upright / more than 120°, i.e. on the back, per band):

| | Village 0–5° | 5–10° | 10–15° | 15–20° | 20–30° | 30°+ | Range level |
|---|---|---|---|---|---|---|---|
| Land | 0 / 0 of 339 | 8 / 0 of 69 | 15 / 1 of 20 | 19 / 3 of 20 | 20 / 4 of 20 | 20 / 4 of 20 | 2 / 0 of 428 |
| Cut | 0 / 0 | 29 / 0 | 19 / 0 | 20 / 2 | 19 / 5 | 18 / 4 | 3 / 0 |
| Drop | 339 / 128 | 67 / 36 | 17 / 8 | 18 / 5 | 15 / 6 | 14 / 7 | 369 / 96 |
| Slide | 330 / 249 | 64 / 36 | 19 / 8 | 14 / 8 | 14 / 7 | 15 / 6 | 424 / 370 |

**What held everywhere** (4,680 rows across both maps):
- No NaN.
- No event logged twice in a step, and no event refused.
- Every log replayed bit for bit.
- Every Land and Cut stayed under the 980 m/s² peak.
- On Village, no Energy failure and no Tunnel.

## 4. The failures, ranked, each sized as a task

Each finding names its replay command. Run every command from the worktree's root at this branch's head, unless the finding names another commit.

**1. The core throws on a rotor hub below the ground** (Standard; physics-engineer). In `PropStrikes.Roots` (`PropStems.cs:237`), `Field` reads `x = κ·h³` with `h = hub.Y − ground` under the hub, and only guards `x < _rootMax`. A negative `h` makes `s` negative, `j` −1, and the table's index throws `IndexOutOfRangeException`. The whole flight thread dies, so in the player the flight ends.
- **Seen:** twice, at commit `ac51342c`: `dotnet run -c Release --project tools/flightbench -- --sweep test-range 400 1 --point 495` (point 495, Land, step 2638; and Cut, step 4136). E01's cut floor is at (53.13, −13.63).
- **Why the start doesn't excuse it:** at that commit the sweep still started this flight with an arm inside the earth beside E01's cut (fixed in the sweep by `02231218`). The core must still not throw.
- **Also reachable in fair flight:** any rotor hub under the ground sampled beneath it reaches the same line. This run measured frame parts up to 35 mm past the ground (finding 6).
- **Fix:** guard `h ≤ 0` (no stem stands to a disc under the ground), plus a test that drives a disc's hub under the ground over the stem field.

**2. On E01's soil shapes the anchors still run away** (Standard; physics-engineer, 2.4's follow-up). Model 86's reach applies to the ground's pairs only (design.md 2.4, "Not on an object's shapes"). A range patch's floor and ends are soil shapes, which are objects.
- **The worst row:** test-range #440 Slide at (59.38, −16.69), 22°, heading 43.7°. 214.2 N·m on 43.4 N, a 4.93 m lever. `--sweep test-range 400 1 --point 440`.
- **Others:** #440 Cut 1.10 m, #437 Land 0.85 m, #437 Slide 0.57 m, #435 Cut 1.00 m (on 0.13 N).
- **Task:** apply `ContactModel.Reached` to soil-shape pairs as to the ground's, and add E01's 22° end to `AnchorReachTests`.

**3. Two `Tunnel` events on E01** (Standard; physics-engineer). Each is the left skid (part 32, `skid_left`), and a tunnel is always a bug.
- #430 Cut at (58.31, −13.86): 58.0 mm deep at step 5640.
- #445 Slide at (63.18, −10.83), 22° end: 7.1 mm at step 6392.
- Replay with `--sweep test-range 400 1 --point 430` and `--point 445`.
- Probably the same soil-shape handling as finding 2: both are on E01, and both drones end tilted.

**4. A 1 m throttle-cut drop breaks every leg; a 2 m/s slide turns the drone over** (Complex; physics-engineer, against p7g's failure law, D-104).
- **Drop:** touching at 2.9–3.4 m/s, all four legs crack and break within 3 ms. I checked 10 drops' events by `--point` (Village #0, #197, #251, #340; range #3, #10, #20, #30, #40, #50): 10 of 10.
- **Then:** the drone falls onto its props and ends past 45° in 339/339 level Village drops and 369/428 on the range, on its back in 128 and 96 of them.
- **Slide:** a 2 m/s slide touching at 0.3–0.5 m/s ends past 45° in 330/339 level Village slides (249 on the back) and 424/428 on the range (370). Its legs crack and mostly break at the touch. Village #0 cracks all four and breaks three.
- **What I can't tell:** whether a 4 kg drone breaks all four legs at 3 m/s is the legs' law to judge, not taste, so I report it as unproven, not as wrong. It is the outcome a pilot would meet at every hard landing.
- **Task:** hold one drop and one slide against `--legs`' grid and p7g's sources. Print the clamp moments at the touch against the tube's strength.
- **Replay:** `--sweep farmland 400 1 --point 0` (Drop, Slide) or `--sweep test-range 400 1 --point 10`.

**5. Across a 5–15° slope a landing goes onto its side (G2, measured map-wide)** (Standard; physics-engineer, 2.6). Cut landings end past 45° at 29 of 69 Village points of 5–10° and 19 of 20 points of 10–15°. Land ends past 45° at 8 of 69 and 15 of 20. At 0–5° none do, of 339.
- These are random headings. `--slopes`' along-the-fall-line rows rest at 10°, so this is the across case, G2 at every heading.
- 2.6's rerun of this sweep is its acceptance count.
- **Replay:** any 5–15° Cut in the farmland file, for example `--sweep farmland 400 1 --point 135` (6.42°, still moving after 10 s).

**6. A broken leg's hanging skid half goes into the ground** (Standard; physics-engineer, p7g-legs). Of 920 Ground failures, 885 are a support or skid on a corner whose leg broke: `Hanging`, `Stub` and often `Folded`. The leg's give in the trace is 0 there, and the soil reports less than 1.3 mm of sink.
- **Depth:** median 48 mm on Village slides, up to 139 mm (Village #455 Slide, meadow_sod 24°).
- **Why it is a bug:** the hanging half's elastic turn (`HangTurn`) is in the shapes' placement, so the overlap is not that give. The half's end supports seem not to be held by the ground once the leg is gone.
- **Task:** trace one such support's ground pair on `--sweep test-range 400 1 --point 5` (Slide, `support_fl_end` 91 mm past the ground, corner `Hanging, Stub, Folded`).
- **Four Ground failures on an intact corner** (range, `None`), all on E01: skids 20–24 mm into its soil shapes at 1.3–1.7 m/s (#402, #431, #434 Slide; #441 Cut). The other 31 are frame parts (finding 11).

**7. A drone at rest on idle props keeps moving** (Standard; physics-engineer).
- **The worst rows:**
  - Range S04 Drop #400 and #405: on the side (52°), 0.49–0.51 m/s and 1.6–1.9 rad/s, 10 s after the touch.
  - Range #412 Slide: 0.54 m/s.
  - Village #340 Slide: upright on dirt_track at 5.4°, still 1.40 m/s and 1.09 rad/s after sliding 14.8 m, with 635 contacts made and broken. It is hopping.
- **Why it matters:** the throttle is 0, armed, so the motors idle. The energy check passes on these, so whatever drives them is the rotors' work: idle props on straw or a hopping skid. Neither is a still drone.
- **Replay:** `--sweep test-range 400 1 --point 405` and `--sweep farmland 400 1 --point 340`.
- **The rest of the Rest failures (13)** move at 0.002–0.04 m/s and 0.01–0.2 rad/s, tipped. Those may be the limit's strictness on a tipped drone rocking on idle props (see §5).

**8. The contacts return energy in eight range flights** (Standard; physics-engineer, with a read-only hook).
- **Rows:** seven slides and one cut, up to +1.33 J: #405 Slide on S04, #423 Slide on E01 0.83 J, #86 0.71 J, #321 0.68 J, #154 0.30 J, #437 Cut 0.15 J, #61 and #407 0.05 J.
- **What is common:** all eight had blades on the ground. So do 439 of the 441 slides that pass.
- **What the check can't tell:** a turning blade pushing on the ground is the motor's work, which the spec allows. A resting blade's pair counted as a contact here cannot be told apart from a spring that gives back more than it took.
- **Task:** a read-only accessor for the blade pairs' share of the contact force and moment, so the sweep can move it into the rotors' work. Then a second sweep says which of the eight are real.
- **Replay:** `--sweep test-range 400 1 --point 405`.

**9. A step allocates mid-flight, by thread** (Quick; physics-engineer or QA).
- **Rows:** 58 Village rows and 83 range rows allocated 0.4–8 KB in one step after the first second, mostly in slides at the tumble.
- **It depends on the thread:** the physics rows are bit for bit across 8 and 6 threads, but this column moved from 84 to 103 failing rows. A failing row passes when flown alone (Village #197 Slide: 424 B in the sweep, 0 B by `--point`).
- **Most likely cause:** a lazily made per-thread cache (`[ThreadStatic]`, like `MapWorld.RegionCache`) or a first-time path inside the step on a thread-pool thread.
- **What it means in the player:** one flight thread allocates it once per process, mid-flight.
- **Task:** find the allocation with an allocation trace on a fresh thread, then pre-make it at the core's construction.

**10. The lever passes 0.5 m by a hair with light contacts** (Quick; QA). Village #283 Slide: 0.514 m (1.95 N·m on 3.80 N). Range: #245 Drop 0.520 m on S04, #483 Drop 0.514 m, #474 Cut 0.500 m.
- **Why it may be the reading:** the sweep reads \|ΣF\|, as `AnchorReachTests` does. That is stricter than the spec's Σ\|F\| when two contacts pull against each other.
- **Settling it** needs the sum of the sizes, a read-only hook in `ContactModel.Forces`. If the sum clears them, the rule holds there.

**11. Frame parts 20–35 mm into soft ground** (Quick; QA). Motor cans and the coil on steep Village drops (15–37°, at 2.7–4.7 m/s) lie 20–35 mm past the surface. The worst is #478 Land, `motor_rl` 34.9 mm at 32.8°.
- **Probably the limit's fault:** the 20 mm limit leaves out a layer's compression. Meadow sod's layer is softer than the frame parts and gives several centimetres.
- **The next sweep should** allow each body pair its row's compression up to its stroke, and judge what's left.

**12. The slide never touched on five Village spots, and two range liftoffs stayed down** (Quick; QA and physics-engineer).
- **Never touched:** Village #251, #332, #430, #433 and #435 Slide, on 9–17° slopes. The sweep's pilot holds a height over the ground under its body, so in ground effect over a falling slope it never got lower than 0.1 m. That is the script's fault: next time, cut the throttle 4 s into the descent.
- **Liftoff stayed down:** range #419 and #440, on E01's 22° end, upright, never left the ground in 5 s. That may be finding 2's anchors on soil shapes holding the legs. Replay with `--sweep test-range 400 1 --point 419`.

### Reported though the limit may be too strict, and why
- **Ground's 20 mm** on a frame part in a soft layer (finding 11): the layer's compression is the ground's own give.
- **Ground's 20 mm past a support's soil sink and its leg's give:** the leg's own give is read from the trace and counted. A broken leg's hanging half has none in the trace, so finding 6 may count part of a hanging half's give. I report it as a bug because the hanging turn is in the shapes' placement already.
- **Rest's 0.05 m/s and 0.1 rad/s** on a tipped drone with idle props: rocking at 0.01–0.2 rad/s may be the idle rotors' torque against a drone on its side. It still is not a drone at rest, and the pilot would see it rock.
- **Lever's \|ΣF\|** (finding 10).
- **Alloc** (finding 9): this one is real (0 B is the rule), but how many rows fail depends on the thread.

## 5. What to add to the sweep next
1. **A Windy pass** (`--wind windy`). It isn't flown yet: the Calm passes and their harness fixes took the night.
2. **Rain** (Rain, Severe rain) on the soil classes: the wet hold is the held-leg harness's ground.
3. **The fiber line on:** a landing's line over stems and the ground (spec, "A line laid across a grass field").
4. **Read-only hooks** for Σ\|F\| and for the blade pairs' share, so the Lever and Energy checks read the spec's own terms (findings 8 and 10).
5. **Soft layers' compression** in the Ground allowance for body parts (finding 11).
6. **A throttle cut 4 s into a slide's descent**, so steep slides touch (finding 12).
7. **A rerun after 2.4's soil-shape follow-up and 2.6**, as `tasks.md` 3.0 asks. Findings 2, 3 and 5 are their acceptance counts.
8. **A thread-fresh allocation probe** in `SweepTests`: one flight on a new thread, with 0 B after its first second.

## 6. The checks, as run (this branch)
- `dotnet test tools/simtests -c Release --filter FullyQualifiedName~SweepTests`: both pass (`WithoutTheReach_TheSweepFailsTheLever` 18 s, `TheSameSeedGivesTheSameRows` 33 s).
- `dotnet test tools/simtests -c Release`: Passed 849, Failed 0, in 26 min 29 s. This ran on `6dc231ae`'s tests; the later commits change only the sweep's own files, and its two tests were rerun at the head.
- `dotnet run -c Release --project tools/flightbench -- --golden`: 62 PASS, 0 FAIL, exit 0 (nothing moved).
- `python tools/hygiene/precommit.py --tree HEAD`: 0 findings.
