# The final world: the integration plan

Planned 2026-11-08 by the orchestrator; **nothing started**. The decisions behind it are D-088 to D-091 in [decisions.md](decisions.md). The weekly limit is tight, so **the pilot announces the start of each phase**. Don't start the next phase on your own.

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
- **The suspect:** the fiber lying across the straw patches (S04, x −134…−86, z ±13, about 50–90 m from the track start) keeps querying their strips after the drone has left. Not proven.

## 2. Order

```
L  the lag's cause (logs first)  ──►  W  the world in  ──►  A  the acceptance gate  ──►  beta, the pilot's gate
                                       B  the three open branches (in parallel with W, other files)
```

## 3. Phase L: the lag's cause

Order (the pilot, 2026-11-09): L1, then L4 and L5, then decide L6. Only L6 can be map-specific, so it waits for the evidence: if the straw is the cause, L6 is dropped (the straw leaves with the test range in W1); if the cause is general, L6 is folded into W3 and designed against the farmland.

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
| W1 | Game developer + world builder · Complex | **The world in our Unity project:** the scene, terrain tiles, vegetation and village become the player's flight world. Keep only the packages it needs (URP and the Input System are ours already). Keep our pipeline asset and renderer (the feed). Set at least 6 start points in varied terrain (WLD-08). The test range leaves the player and stays only for headless tests and benches, so the straw leaves the game. | The player loads the farmland from the menu (noclip first, flight once W3 lands); smoke PASS |
| W2 | World builder · Standard | **The map package export:** a new "Export map package" step writes `maps/farmland/`: the heights of the map, the surround and the far ring (D-091: heights everywhere, objects only on the map); the 8 layers mapped to classes in `surfaces.json`; grass density per cell; and every tree, bush, ruin, debris and object placement with its variant. | `python tools/map/validate.py maps/farmland` passes |
| W3 | World builder, physics sign-off · Complex | **Hilly ground in the sim core** (the world core has no heightfield today): `SampleGround`, `Raycast`, `StaticContacts` and the fiber's ground contact over a heightfield triangulated exactly as Unity's terrain, with a hole mask for the trenches. Only + − × ÷ √, 0 B in steady state, `QueryVersion` 8, worldbench rows and goldens. Check the legs' and crash contact on slopes: model 82 if the contact model changes. The terrain draws close enough to the heightfield near the drone that what's drawn is what's touched (tech artist checks). | Goldens audited case by case; bench within PLT-04 |
| W4a | Asset artist · Standard | **Trees and bushes get physics:** trunk and main limbs as capsules, crowns as volumes with our crown physics (drag, snag, prop strikes), bushes as soft volumes. Generated from the vendor meshes. Metadata and params are pushed; copied geometry stays local. Grass gets no physics per blade: its density per cell drives the micro-detail on each class. | Review sheet at 1.5 / 10 / 50 / 200 m; flightbench crown rows pass |
| W4b | Asset artist · Standard | **The ruins and debris get physics:** the 5 houses and 28 pieces, with collision following the visual (walls, openings as gaps, roof sheets) and their materials (wood, burnt wood, sheet metal) | Review sheet; a crash into a ruin names its material |
| W5 | World builder · Quick | **Place our objects (D-090):** one `E01_curving` in each field F1–F11 at a seeded random spot and yaw, fully inside the field with a margin. `H01_I`, `H01_D`, `R01_A` and `R01_F` go in the village on free ground, clear of ruins, debris and trees. Houses and cars sit level (the terrain under their footprint flattened by the export). The trench is cut into the terrain (a terrain hole, the sim's hole mask, a flattened rim). Their existing physics metadata is reused unchanged. | Placements validate; review sheet of each spot |
| W6 | Tech artist · Standard | **Look and frame rate with our feed:** the friend's lighting and post-processing in our pipeline, and the feed's 2889×1625 render on the RTX 4050: PLT-02, PLT-05 (VRAM ≤ 4.5 GB), PLT-06 (≤ 20 s to flyable). Tune grass distance and counts. On AC power only. | PLT-07 protocol report |
| W7 | Physics engineer · Standard | **Wind over hills:** check `WindGrid` against the terrain (crest speed-up, shelter in the lee and in the tree belts). Add what's missing from physics, not a tuned value. | Wind rows pass on the farmland |

Parallel: W0 first. Then W1, W2 and W4a/W4b can run together (at most four). W3 after W2's format is fixed. W5 after W2 and W3. W6 after W1. W7 after W3.

## 5. Phase B: the three open branches (in parallel with W)

As in [handover-2026-11-06.md](handover-2026-11-06.md) §2 and §6.2: arm-break (model 80), edge-load (model 81 only if physics changes), app-reds. Fresh agents. Merge onto the line in that order, goldens audited case by case.

## 6. Phase A: the acceptance gate (D-091), before any beta

| Unit | Role · tier | Goal |
|---|---|---|
| A1 | QA, physics and game developer support · Complex | **The scenario suite** on the farmland, run headless (.NET) and in the player (an unattended bench mode: no dialogs, no listening sockets, AC power): low passes over every ground class and over the trenches, tree lines and crowns, the village (ruins, our houses, the cars), long fiber payouts across the map and the surround, Calm, Windy and Severe, crashes, R restarts. At least 500 seeds, and a soak of at least 8 h. |
| A2 | QA · in A1 | **Pass lines:** PLT-04's step budgets met **in the player**, not only in .NET; **0 late steps** over the soak; every log replays bit for bit; every step finite (no NaN or Inf); no tunnelling into the ground or objects beyond the contact tolerance; fiber tension and contact forces within physical bounds; 0 B allocated in steady state. **The pilot's complaints as named tests:** no sluggishness (0 late steps and the stick-to-response time from the log), the climb after a pitch-back, the fiber's cuts (D-087). |
| A3 | Game developer + QA · Standard | **IL2CPP:** build the player on IL2CPP, replay the suite against .NET bit for bit (watch for fused multiply-adds) and measure the step. If it's exact and faster, a new decision switches the player (supersedes "Mono for now"). |
| A4 | QA · Quick | The open debts: the `FlightScheduler` stop-between-steps review and the `RegionCache` sign-off |

**The gate:** every pass line green on two overnight runs in a row. Then build the beta (`tools/build_latest.ps1`, when the pilot isn't flying) and hand it to the pilot.

## 7. Numbers ledger

- Models: 80 arm-break, 81 edge-load (reserved). **Next free: 82** (W3 if the contact model changes).
- Formats: 70 arm-break, 71 edge-load (reserved). **Next free: 72** (L4 or W2's map identity in the header).
- Event kinds: **next free 39.** L5's cap is a new `HitchCause`, not a new kind.
- `QueryVersion`: 7 now, **8** for W3.
- Decisions: D-088 to D-091 used. **Next free: D-092.**
