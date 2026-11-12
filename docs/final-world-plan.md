# The final world: the integration plan

Planned 2026-11-08 by the orchestrator. **Phase L started 2026-11-09** (§3 has where it stands); Phase W started 2026-11-11; B and A wait for the pilot's flight (D-093). The decisions behind it are D-088 to D-091 in [decisions.md](decisions.md). The weekly limit is tight, so **the pilot announces the start of each phase**. Don't start the next phase on your own.

Rules that still hold: the D-033 tiers; every subagent on Opus 5.5, one fresh subagent per unit, at most four in parallel (D-036); image work only in subagents; numbers from the ledger (§6); the orchestrator opens and merges every PR, merges with `-m`, subjects of at most 72 characters; build only when the pilot isn't flying; no dialogs or listening sockets in unattended runs; remove each worktree once its branch lands.

## 1. Inputs

**The friend's world (D-088),** a Unity project the pilot received. It arrived under `reference/` (git-ignored); W0 moves it out, and the orchestrator's local notes hold its path. Beside it are a PDF scene guide and the project's own `docs/` (design, assets, roadmap) and `AGENTS.md`. Read those before touching it.
- Unity 6000.3.0f1 and URP 17.3: the same line and pipeline as ours (6000.3.26f1). About 1.7 GB of assets.
- **The map:** 1 × 1 km of abandoned farmland: a 1025² heightmap with rolling hills (12–42 m, plus four stronger rises) and 8 ground layers (field mud in two furrow directions, dry mud, three grasses, gravel, a grass path).
  - 11 fields, F1–F11: 51 ha, overgrowth 0.2–0.9.
  - Tracks: 4 gravel tracks (T1–T4) and 6 grassy side tracks (T5–T10), with the crossroads at about (575, 570).
  - About 2.85 M grass instances in 14 types, 294 trees and 1,352 bushes as terrain instances.
  - A ruined village: 5 wooden houses and 28 pieces of debris.
  - Around it, eight surround tiles (meadow, grass and tree belts) and eight far tiles carry the hills about 4 km further.
- **Built by code:** a seeded editor generator (`FarmLandBuilder`, the "Farm Land" menu, one step per area) rebuilds the terrain, tracks, grass, trees, bushes and village. Same seed, same result. Its field outlines are in `FarmLandBuilder.Layout.cs`.
- **Visual only today:** the trees' colliders were removed, the houses are plain meshes, and nothing carries physics data.
- **Sources:**
  - the friend's own code and shaders
  - CC0 Poly Haven ground textures and the broadleaf tree
  - Unity's Terrain Sample pack (the grass)
  - an HDRP sample with Quixel Megascans content (the pines and bushes)
  - an Asset Store house model (the ruins)

**Our objects (D-090):** the trench `E01_curving`, the houses `H01_I` and `H01_D`, the cars `R01_A` and `R01_F`. All are generated, with their physics metadata.

**The model-79 lag (D-087, unexplained):** the pilot's three videos A, B and C pair with flights 35, 37 and 38 of the 2026-11-06 evening session. The logs are in the player's flight-log folder (`%LOCALAPPDATA%\450fpv\flights`; `Player.log` lists each flight's file). The bug starts about 158 s, 12 s and 63 s into those flights (±2 s, from file times). All flights were from the track start, in Severe wind.

What the 2026-11-06 review found:
- **The render didn't drop:** 40–48 fps all through each video, with no dip at any bug.
- **The scheduler never drops steps:** a step over 1 ms leaves the sim behind real time, and it stays behind (`FlightScheduler.RunDue`).
- **Every late pass is in the logs** as a `Hitch` event.
- **The suspect:** the fiber lying across the straw patches (S04, x −134…−86, z ±13, about 50–90 m from the track start) keeps querying their strips after the drone has left. Not proven. (The extent was wrong: S04 lines both edges of the posadka along its whole length, x −140…160.)

**Explained by L1 (2026-11-09),** in [reports/lag/model-79-flights.md](reports/lag/model-79-flights.md) on the build line: in all three flights the sim fell behind real time. The fiber's contact queries pushed the player's step past 1 ms, and the scheduler ran every late step, so the delay grew (worst 25.6 s in A). The queries come from any segment of the laid line that rests within 0.5 m of a non-ground shape: A's line lay on the yard's gate and house, while B and C lay on S04's straw. **The cause is general, not the straw.**

## 2. Order

```
L  the lag (closed with what it has)  ──►  W  the world in  ──►  the pilot flies the farmland  ──►  B and A, in an order the pilot sets  ──►  beta
```

The pilot set this order on 2026-11-11 (D-093). It replaces the earlier "B in parallel with W".

## 3. Phase L: the lag's cause

Order (the pilot, 2026-11-09): L1, then L4 and L5, then decide L6. Only L6 can be map-specific, so it waits for the evidence: if the straw is the cause, L6 is dropped (the straw leaves with the test range in W1); if the cause is general, L6 is folded into W3 and designed against the farmland.

Where it stands (2026-11-10):
- **L1 done:** the cause is general (§1), so **L6 is folded into W3**.
- **L4 landed** on the build line (PR #223, change `p7h-step-budget` 1.1, format 72). The player now writes each flight's step times to `Player.log` and the log's end block. Mono's own 60 s test figures: p50 34 µs, p99 112 µs, max 1.2 ms.
- **L5 is in progress** (`p7h-step-budget` 2.1), then the QA review (3.1).

| Unit | Role · tier | Goal | Done when |
|---|---|---|---|
| L1 | QA · Quick, one agent for all three flights (35, 37, 38) | Build `tools/replay` from the build line, then read: the `Hitch` events over the whole flight and ±10 s of the bug (step, steps, how late); the events (prop strikes, straw cuts, motor health, fiber contacts); where the drone and the fiber's nodes were, against the straw patches and the houses. Replay the late stretches with per-phase step timing (.NET). Classify the case: the sim behind real time, a render drop, input, or a physics state. Make contact sheets from the video only if the log can't decide. | `docs/reports/lag/model-79-flights.md` names the cause per flight with the evidence (events, timings) |
| L4 | Game developer, physics review · Standard | Step time inside the player (Mono): per-step wall time, an overrun counter, and p50/p99/max per flight, in the log's end record and the development overlay | The player reports its own step times; format 72 if the end record changes |
| L5 | Physics engineer · Standard | **A lag cap:** once the sim is behind by more than a bound the physics engineer computes and justifies (the pilot's control latency), it moves its time base forward instead of running the backlog. Logged as a `Hitch` with a new cause; replays stay exact, because the sticks are logged per step. | A slow stretch costs one bounded hiccup, never growing control lag; tests show it |
| L6 | Owner of the cause · Standard, only if the cause is general | Fix the root cause L1 found. If it's the fiber's queries among dense shapes, fix the cost path itself, not just the straw: the new world has 1,650 trees and bushes and a village. | The three flights' worst stretches replay within the step budget |

## 4. Phase W: the world in

| Unit | Role · tier | Goal | Done when |
|---|---|---|---|
| W0 | Orchestrator · Quick | **The vendor store (D-089):** move the world out of `reference/` into a git-ignored local store in the main checkout. `tools/vendor/link.ps1` links it into any checkout or worktree (junctions). `.gitignore` and the hygiene scan block the vendor paths. `tools/assets/manifest.json` lists the packs, marked local-only, and `CREDITS.md` credits them. **Push our own text** (the friend's code and shaders, scenes, prefabs, materials, the map package); **keep every third-party binary local**, along with anything that copies its geometry. | A fresh worktree runs `link.ps1` and opens the world; `precommit.py --tree HEAD` is clean |
| W1 | Game developer + world builder · Complex | **The world in our Unity project:** the scene, terrain tiles, vegetation and village become the player's flight world. Keep only the packages it needs (URP and the Input System are ours already). Keep our pipeline asset and renderer (the feed). One start point, in the village on free level ground (D-092). The test range leaves the player and stays only for headless tests and benches, so the straw leaves the game. | The player loads the farmland from the menu (noclip first, flight once W3 lands); smoke PASS |
| W2 | World builder · Standard | **The map package export:** an exporter in our repo (`FarmlandExport`, run in batchmode) writes `maps/farmland/`: the heights of the map, the surround and the far ring (D-091: heights everywhere, objects only on the map); the 8 layers mapped to classes in `surfaces.json`; grass density per cell; and every tree, bush, ruin, debris and object placement with its variant. | `python tools/map/validate.py maps/farmland` passes |
| W3 | World builder, physics sign-off · Complex | **Hilly ground in the sim core** (the world core has no heightfield today): `SampleGround`, `Raycast`, `StaticContacts` and the fiber's ground contact over a heightfield triangulated exactly as Unity's terrain, with a hole mask for the trenches. Only + − × ÷ √, 0 B in steady state, `QueryVersion` 8, worldbench rows and goldens. Check the legs' and crash contact on slopes: model 82 if the contact model changes. The terrain draws close enough to the heightfield near the drone that what's drawn is what's touched (tech artist checks). **And L6 (the fiber's query cost):** a laid segment resting near shapes must not cost a full `StaticContacts` query every step. The cost grows with the shapes in the segment's bounding box, and far segments are 20–200 m long. Fix the cost path for the farmland's trees, bushes and village, not for one asset. | Goldens audited case by case; bench within PLT-04; the three model-79 flights' worst stretches (L1's report) replay within the step budget |
| W4a | Asset artist · Standard | **Trees and bushes get physics:** trunk and main limbs as capsules, crowns as volumes with our crown physics (drag, snag, prop strikes), bushes as soft volumes. Generated from the vendor meshes. Metadata and params are pushed; copied geometry stays local. Grass gets no physics per blade: its density per cell drives the micro-detail on each class. | Review sheet at 1.5 / 10 / 50 / 200 m; flightbench crown rows pass |
| W4b | Asset artist · Standard | **The ruins and debris get physics:** the 5 houses and 28 pieces, with collision following the visual (walls, openings as gaps, roof sheets) and their materials (wood, burnt wood, sheet metal) | Review sheet; a crash into a ruin names its material |
| W5 | World builder · Quick | **Place our objects (D-090):** one `E01_curving` in each field F1–F11 at a seeded random spot and yaw, fully inside the field with a margin. `H01_I`, `H01_D`, `R01_A` and `R01_F` go in the village on free ground, clear of ruins, debris and trees. Houses and cars sit level (the terrain under their footprint flattened by the export). The trench is cut into the terrain (a terrain hole, the sim's hole mask, a flattened rim). Their physics metadata is reused, except that `E01_curving`'s filler band widens to at least 3.6 m (derived in W2's design.md: a terrain hole cell reaches 1.38 m past the opening), by the asset artist before the cut. The cuts and flattening are data in `maps/farmland/` (the hole mask and footprints), applied at load to the sim and to a runtime copy of the drawn terrain; the store's terrain stays untouched (D-095). | Placements validate; review sheet of each spot |
| W6 | Tech artist · Standard | **Look and frame rate with our feed:** the friend's lighting and post-processing in our pipeline, and the feed's 2889×1625 render on the RTX 4050: PLT-02, PLT-05 (VRAM ≤ 4.5 GB), PLT-06 (≤ 20 s to flyable). Tune grass distance and counts. On AC power only. | PLT-07 protocol report |
| W7 | Physics engineer · Standard | **Wind over hills:** check `WindGrid` against the terrain (crest speed-up, shelter in the lee and in the tree belts). Add what's missing from physics, not a tuned value. | Wind rows pass on the farmland |
| W8 | World builder · Standard | **The world reaches the spool (D-091):** the far ring ends about 4.5 km from the village, but the spool holds 10 km. Extend the ground (heights only, no objects) to the spool's reach in the scene and the package. Until then, a point past the far ring is a hole. | A 10 km flight from the start point meets ground everywhere |

Parallel: W0 first. Then W1, W2 and W4a/W4b can run together (at most four). W3 after W2's format is fixed. W5 after W2 and W3. W6 after W1. W7 after W3. W8 any time after W2; it doesn't hold up the pilot's flight.

**Added after the pilot flew the farmland** ([handover-2026-11-12.md](handover-2026-11-12.md) §2 and §3):
- **W9, Village physics** (`w9-village-physics`, Complex): the audit is done; the stem field, stems on the props, the legs and the fiber, leaves in crowns, and two contact bugs (D-102). In work.
- **W10, Play** (`w10-play-maps`, D-100): the carousel of House and Village with their stills. In the pilot's build.
- **W11, the M map** (`w11-world-map`, D-101). In the pilot's build; its frame cost on a quiet machine is owed.
- **W12, furrows** (not started, D-102): a world-and-look unit that draws the fields' furrow relief near the drone and stores it in the package, so the physics can follow it.
- **W13, the props turn in the frame** (`w13-props-in-frame`, Standard): the drawn props follow the sim's rotors over each video field's exposure. Built; its review is next. It ships with W14 (D-103).
- **W14, the camera's place** (`w14-camera-place`, Standard, D-103): the lens moves to where the footage and the published frame put it, with the airframe parts the footage shows at mid-height. Proposed.

## 5. Phase B: the three open branches (after the pilot flies the farmland, D-093)

As in [handover-2026-11-06.md](handover-2026-11-06.md) §2 and §6.2: arm-break (model 80), edge-load (model 81 only if physics changes), app-reds. Fresh agents. Merge onto the line in that order, goldens audited case by case.

## 6. Phase A: the acceptance gate (D-091), before any beta

| Unit | Role · tier | Goal |
|---|---|---|
| A1 | QA, physics and game developer support · Complex | **The scenario suite** on the farmland, run headless (.NET) and in the player (an unattended bench mode: no dialogs, no listening sockets, AC power): low passes over every ground class and over the trenches, tree lines and crowns, the village (ruins, our houses, the cars), long fiber payouts across the map and the surround, Calm, Windy and Severe, crashes, R restarts. At least 500 seeds, and a soak of at least 8 h. |
| A2 | QA · in A1 | **Pass lines:** PLT-04's step budgets met **in the player**, not only in .NET; **0 late steps** over the soak; every log replays bit for bit; every step finite (no NaN or Inf); no tunnelling into the ground or objects beyond the contact tolerance; fiber tension and contact forces within physical bounds; 0 B allocated in steady state. **The pilot's complaints as named tests:** no sluggishness (0 late steps and the stick-to-response time from the log), the climb after a pitch-back, the fiber's cuts (D-087). |
| A3 | Game developer + QA · Standard | **IL2CPP:** build the player on IL2CPP, replay the suite against .NET bit for bit (watch for fused multiply-adds) and measure the step. If it's exact and faster, a new decision switches the player (supersedes "Mono for now"). |
| A4 | QA · Quick | The open debts: the `FlightScheduler` stop-between-steps review and the `RegionCache` sign-off. Also: three EditMode tests already fail on the build line (leg shots, the R01_A fall, the car bumper's material id); the hygiene scan's false positive in the project's 2D physics settings file (it blocks `precommit.py --tree HEAD`); the post-flight summary's missing case for the payout event (kind 14); and flight 36's 2,868 fiber-shape-full events, which need a physics review |

**The gate:** every pass line green on two overnight runs in a row. Then build the beta (`tools/build_latest.ps1`, when the pilot isn't flying) and hand it to the pilot.

## 7. Numbers ledger

- Models: 80 arm-break, 81 edge-load (reserved). **Next free: 82** (W3 if the contact model changes).
- Formats: 70 arm-break, 71 edge-load (reserved), 72 `p7h-step-budget` (used: the step times in the end block; L5 stays inside it). **Next free: 73** (W2's map identity in the header).
- Event kinds: **next free 39.** L5's cap is a new `HitchCause`, not a new kind.
- `QueryVersion`: 7 now, **8** assigned to W3 (`w3-farmland-ground`).
- Decisions: D-088 to D-095 used. **Next free: D-096.**
