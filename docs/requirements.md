# Requirements

The refined requirement set for the Unity restart.

## How to use this file
- **Wording.** "Shall" means required, and every requirement has an acceptance test.
- **IDs are stable.** OpenSpec changes cite them in their proposals, and QA cites them in reviews.
- **Changing a requirement** means a new decision in [decisions.md](decisions.md), then an edit here. The pilot's words beat any interpretation.
- **Sources:**

  | Tag | Points to |
  |---|---|
  | **V§n** | [VISION.md](../VISION.md) |
  | **B** | the pilot's brief of 2026-10-17 ([pilot-brief-2026-10-17.md](pilot-brief-2026-10-17.md)) |
  | **PR-n** | the pilot requirements ([reference-notes/pilot-requirements.md](reference-notes/pilot-requirements.md), summarised in [knowledge.md](knowledge.md) §4) |
  | **D-n** | [decisions.md](decisions.md) |
  | **K§n** | [knowledge.md](knowledge.md) |

- **Phase:** each requirement is built in the phase that owns it ([plan.md](plan.md)). The traceability table at the end maps every ID to its phase.

---

## 0. What "realism" means (read this first)

The pilot, 2026-10-17: "when i say realism i mean more than just abstract realism. i also mean that the game should be a copy of how it looks on reference (both my flight replay references and those images), it's important. it's also important that we do proper geometry if you remember - not only for looks but also for physics".

**REAL-01 Realism is a copy of the references.**
- Whenever the pilot or these docs say "realistic", they mean **looks like the pilot's references**:
  - the **flight replays** (clips A–H, L, P), seen through the feed
  - the **photos** (I–K, M–O), seen in noclip at full resolution
- They don't mean "realistic in general", "AAA-pretty", or a generic asset-store look.
- Every visual decision is judged against a reference letter first. Taste comes after.
- *Source:* the pilot, D-028.

**REAL-02 Reference views.** Every reference letter gets at least one **recreated view**: a saved camera in the gallery or the world showing the same scene type, viewpoint height, framing, light, time of day and weather.

| Letter | View |
|---|---|
| A | open rolling meadow from height, then the long descent to the grass, under overcast |
| B | dry cratered field in low warm sun, with broken trees and the earth bank |
| C | the village yard: fruit tree, sheds, leaf litter, the approach into the 0.70 m cellar door |
| D | an overgrown yard against the sun: weeds, dead trees, stone rubble, a roof edge, a sagging cable |
| E | the damaged adobe house, roof stripped to the battens: the fly-in through the door to the far window |
| F | inside a dense belt: thin trunks, dusty bare ground, dust haze, the pit with branches |
| G | low and fast along a trench beside a narrow tree strip and a dark field |
| H | slow and low along a belt edge, over lodged straw and dark soil, past a pole, under overcast |
| L | night over rolling dark fields, with silhouettes against the sky |
| P | severe wind and rain over stubble fields, crossing one belt |
| I | the urban edge: the burnt five-storey block, a collapsed roof pile, a damaged two-storey house |
| J | a village street: damaged brick houses, stripped roofs, an overhead wire, brush piles |
| K | the block with its facade torn off, and the rubble mound |
| M | the abandoned classic sedan on asphalt |
| N | the blast-wrecked hatchback in its debris field |
| O | the burnt-out bonneted truck (no snow: season isn't a setting) |

*Accept:* every view exists. The review controls cycle through them (GAL-05, WLD-14).

**REAL-03 Side-by-side comparison.**
- Each reference view is rendered the way the pilot saw it: through the feed for clips, in noclip at full resolution for photos.
- It is placed **next to the reference frame or photo** in a **local-only comparison sheet** under `reference/_compare/`, which is git-ignored and never committed or uploaded.
- It is judged on:
  - shapes and proportions
  - materials and colours (the `feed` and `photo` values)
  - clutter and damage density
  - vegetation density and silhouette
  - light and atmosphere
  - feed artifacts
- Every visible difference becomes a task, fixed or explained.
- *Who judges:* QA and the tech-artist. The pilot judges at the gates.

**REAL-04 Copy the look, not the location** (OPSEC).
- **Copied:** scene types, materials, construction, proportions, damage, density, light and the feed.
- **Never reproduced:** a specific identifiable place. No unique landmarks, signs, text or layouts traceable to a real location.
- The map stays a composite (SEC-03).

**REAL-05 Proper geometry, for the looks and for the physics.** The pilot (PR-5): "every little texture detail will affect the physics".
- **Real 3D shape with real thickness** for everything the drone can see up close, touch or fly through:
  - walls with their real depth and real openings
  - jagged broken brick and missing courses, protruding rebar
  - broken battens with real gaps, shattered tile and slate
  - fence boards with gaps
  - trunks, branches and twigs near flight paths
  - wires and poles
  - debris under the legs
- **No faking** with textures, normal or parallax maps, alpha cards or decals for anything with a physical role:
  - Textures and normals only add surface detail below about 1–2 cm that has no physical role.
  - Leaf cards are fine for foliage mass; the crown's physics is its wind volume plus twig and branch snags.
- **Collision follows the visual** (AST-05 tolerances):
  - every visible obstacle collides
  - every visible opening is a gap the sim knows, measured as the real clear space
  - no invisible walls, and no holes the sim doesn't know
- **The ground is geometry, read by both renderer and physics from the same source:** relief, crater rims, pitfalls, furrows, ruts and trench walls, plus the shared micro-detail generator for stems, straw and twigs.
- **Dimensions match the references exactly** (AST-04).
- *Accept:* a geometry audit per asset:
  - a collision overlay against the visual
  - every opening probed with the drone-sized capsule
  - a list of physical features confirming none is texture-only

---

## 1. Product (PRD)

**PRD-01 Purpose.** A lightweight Windows simulator of **heavy fiber-optic cargo FPV quads** whose product is **physics realism**: undefined and sometimes unexpected dynamics, as in real flight. It is not an AAA game. *Accept:* every phase's spec states how it serves this. *Source:* V§1–2, V§3.

**PRD-02 One drone.** Exactly one aircraft, **"Svinoriz 10 Opto"**, 10-inch with 3-blade props. There is no drone selection and no tuning of the airframe beyond PRD-04. *Accept:* no UI or config exposes another airframe. *Source:* B req 4, V§3, D-011.

**PRD-03 One map.** One composite map of Ukrainian terrain, about 3 adjacent villages with their fields and tree belts plus one urban edge (D-018). *Source:* B plan 2, V§3.

**PRD-04 The only settings.**
- **Payload:** the additional weight, in kg.
- **Legs:** on or off. Off means liftoff from the **four-post launch stand** (D-022), so the coil clears the ground.
- **Time of day**, continuous; night is part of it.
- **Wind:** Calm, Windy or Severe (D-010).
- **Rain:** Sunny, Rain or Severe rain (D-021).

*Accept:* the setup screen offers exactly these five, plus the start point. *Source:* V§3, D-010, D-021.

**PRD-05 Menu.** The app opens to **Train / Calibrate / Exit**, "prettier than uglier". Review builds add **Explore** (noclip). *Source:* V§3.

**PRD-06 Physics logging.** Every flight writes a log that can explain any crash as physics or pilot error. *Accept:* LOG-01 to LOG-03. *Source:* V§4.

**PRD-07 Noclip explorer** for gallery and world review:
- mouse look and **WASD**
- **Shift** for much faster movement
- **E/Space** up, **Q/Ctrl** down
- mouse wheel scales the speed

*Accept:* used at gates G2 and G3. *Source:* V§5.2.

**PRD-08 "Tiny, cunning, elegant randomness."** All randomness is seeded, bounded and physical (PHY-24). The same seed replays the same flight. *Source:* V§2.4, K§6.9.

---

## 2. Platform and performance (PLT)

**PLT-01 Stack.**
- Unity 6 LTS (exact version pinned), URP Forward+, C#.
- Windows 10/11 x64.
- Blender, headless, for asset generation; Python for tools.
- *Source:* B req 1, D-001.

**PLT-02 Frame rate on the dev machine** (Ryzen 7 7735HS, RTX 4050 Laptop 6 GB, 16 GB, 1920 × 1080):

| View | Requirement |
|---|---|
| **FPV view** (the feed on), the busiest place on the world map | **1 % low ≥ 60 fps**, average ≥ 100 fps |
| **Noclip review** at 1080p, the busiest gallery and world views | **1 % low ≥ 60 fps**. Unity's STP upscaler is allowed at render scale ≥ 0.67 |

*Accept:* the PLT-07 protocol. *Source:* B req 2, V§3.

**PLT-03 Mid-range PCs.** On a GTX 1660 Super / RX 6600-class GPU with a 6-core CPU and 16 GB, the FPV view averages ≥ 60 fps at the "Medium" quality preset. This is informational; QA estimates it from GPU timing. *Source:* V§3.

**PLT-04 Physics rate.**
- The flight model runs at **1000 Hz in real time** on its own thread, with the full world loaded.
- **p99 step time ≤ 300 µs**, including world queries, with world queries alone **< 60 µs** worst case.
- **Zero managed allocations** in steady state.
- A late step is caught up and logged as a `hitch`, never stretched.

*Source:* K§6, K§10.4.

**PLT-05 Memory.** VRAM ≤ 4.5 GB (the GPU has 6 GB) and RAM ≤ 8 GB on the world map. *Source:* dev machine.

**PLT-06 Load times.** The gallery takes ≤ 10 s and the world ≤ 20 s from the menu to flyable, on the dev machine.

**PLT-07 Benchmark protocol (QA).**
- On **AC power**, with the power mode and CPU/GPU clocks printed, vsync off.
- A fixed scripted camera or flight path; 2 s warm-up; ≥ 30 s measured.
- Reports average fps, 1 % low, worst frame, CPU and GPU ms, draw calls, triangles and VRAM.
- Battery figures are never budget evidence (the iGPU takes over).

*Source:* K§14.1.

---

## 3. Process and team (PRC)

**PRC-01 Orchestrator and subagents.** The main session orchestrates and delegates implementation to role subagents ([team.md](team.md)), **one fresh subagent per logical task** (D-029: the pilot prefers it). Every handoff is announced (role, change id, goal), and every returning handoff is summarised for the pilot. *Source:* V§4, B req 5.

**PRC-02 Spec-driven, scaled to the task** (D-033). Standard and Complex work is an OpenSpec change before it is built (Complex: proposal, design, specs, tasks; Standard: proposal and tasks, with a spec delta or design only when needed). Quick work (wording, labels, a config value, a test threshold, a one-file fix, bookkeeping) carries no change folder. Code follows specs. When the pilot changes direction, the spec changes first. *Source:* D-015.

**PRC-03 Commits** (the pilot's `AGENTS.md`):
- one meaningful piece per commit
- **subject line only** (`type(scope): summary`, ≤ 72 characters)
- no body, no trailers, **no Claude co-author line**
- the role's own git identity, via `git -c`

*Source:* AGENTS.md.

**PRC-04 PRs.**
- Opened automatically against `unity/main`, from feature branches `unity/<prefix>/<change-id>` (D-025).
- Merged by the orchestrator once QA is green, with `gh pr merge --merge` and never squash.
- Only a pilot-gate task waits for the pilot.

*Source:* D-002, D-014.

**PRC-05 The pilot reviews only gates** (G1–G8 in [plan.md](plan.md)). They never review sandboxes, plans, specs or notes. *Source:* K§2.2.

**PRC-06 Research, don't ask.** Public facts (airframes, components, construction, war damage, vehicles, vegetation) are researched. The pilot is asked only what only a pilot knows, briefly, and only when it matters ([pilot-questions.md](pilot-questions.md)). *Source:* K§2.2.

**PRC-07 Reports.** On request, write `docs/reports/YYYY-MM-DD.md`, covering every role: done, in progress, blocked, decisions needed, risks. *Source:* V§4.

**PRC-08 Decisions** are logged in [decisions.md](decisions.md).

**PRC-09 Token and limit discipline:**
- at most 2 subagents in parallel
- fresh agents with short briefs, rather than resuming huge ones
- commit early
- long jobs run in the background, never polled
- image-heavy work goes to subagents, which write their findings to files
- the orchestrator keeps its own context lean

*Source:* K§14.1.

**PRC-10 Evidence.** Every handoff pastes the verification commands it ran and their output. "Done" without evidence is not done.

**PRC-11 Calculate, don't ask** (D-024).
- **Never asked:** any quantity physics or research can produce (heights, rates, frequencies, sizes, how often).
- **Asked:** qualitative experience and preferences, at gates only, and only if not already answered ([pilot-questions.md](pilot-questions.md)).

**PRC-12 Two codebases, one repo** (D-025, D-027). Godot stays on `main` (tag `godot-final`). Unity lives only under `unity/` (trunk `unity/main`). Switching between them on GitHub and locally follows [repository.md](repository.md).

---

## 4. OPSEC and licensing (SEC)

**SEC-01 Footage never in git.**
- `reference/` is git-ignored before the first commit.
- Agents never copy, upload or commit anything from it, and never write reference file names anywhere.

*Accept:* the `.gitignore` entry, plus a pre-commit scan (SEC-06). *Source:* D-013.

**SEC-02 Notes are class-level.** Clips and photos are cited by letter only. No places, coordinates, dates, OSD values, unit details, people or sortie specifics. *Source:* K§2.4.

**SEC-03 No georeferencing.** Maps hold no lat/lon or EPSG data and no real place names. Village and road signs use **invented** names. *Accept:* the validator rejects geo fields and blocklisted names.

**SEC-04 Licences** follow D-003:
- CC0 preferred, CC-BY with attribution
- no NC, ND, paid, Asset Store or Fab content
- every third-party file pinned (URL, licence, author, SHA-256) in `tools/assets/manifest.json`
- `CREDITS.md` generated, and `--verify` checks that it covers every file

**SEC-05 OSD strings.** The name reads "Svinoriz 10 Opto" and the armament label "! SAFE !". Never revert either. *Source:* D-011.

**SEC-06 Pre-commit scan** (hook plus QA). It blocks:
- media files outside `assets-src/` and `unity/Assets/`
- footage-like names and extensions
- messenger names
- coordinates and dates in notes

---

## 5. Assets (AST): phase 1

**AST-01 Content.** The asset classes, variants and states in [asset-list.md](asset-list.md). Every **P1** item exists before gallery release G2; P2 items follow before the world (G3). *Source:* B plan 1–2.

**AST-02 Reference-driven.** Every asset has an **object sheet** (`docs/object-sheets/<class>.md`) giving:
- the clip and photo letters that show it, plus research sources (URLs) for what the footage can't show
- dimensions with ranges
- construction and materials
- colours (photo albedo, plus the footage `feed` values to check against)
- damage patterns
- its physical role

The footage is PAL and too coarse for photogrammetry, so **assets are rebuilt to match the reference, never extracted from it**. *Source:* B plan 1, PR-7, K§9.

**AST-03 Editable by parameters** (D-004). Each asset is generated from a parametric Blender script plus a params file per variant. **One command rebuilds one asset or all of them**, export and Unity import included. Each class's params are documented in its object sheet. *Accept:* QA changes one parameter (a wall height, damage level or colour), rebuilds, and sees the change in Unity with no hand step. *Source:* B plan 1.

**AST-04 Scale and dimensions** sit inside the reference ranges. Bricks are exact: 250 × 120 × 65 mm. The entrance door of the village houses H01–H04 is exact: 0.8 × 1.8–1.9 m, and their windows 0.8 × 1.0 m for the adobe houses; every other door, doorway and window (interior doors, the H04 veranda doorway, the small hut H05, outbuildings, cellars, shops, the block) takes the labelled size its sheet gives, published where the row cites the standard it comes from, else measured or assumed (the 0.70 m cellar door is the pilot's, PR-9). Wall heights to the plate, from grade outside, per class (D-032): the small clay hut H05 2.0–2.3 m; adobe houses H01–H02 2.3–2.6 m (measured in E); single-storey brick houses H03–H04 2.6–3.1 m including their plinth and cornice (derived: a 2.8 m storey plus plinth or cornice; J shows H03's ridge, not its wall); two-storey houses 2.8 m a storey; the Stalin-era block 3.0–3.4 m a storey. A 1.8 m human scale bar is checked in screenshots.

**AST-05 Physics metadata, authored with the asset** (not in phase 7):
- **collision:** primitives (box, sphere, capsule, cylinder) following the visual surface within **±5 cm** wherever the drone can touch, and **±2 cm** around openings and landing surfaces
- **a material id per shape:** steel, sheet_metal, timber, masonry, concrete, glass, cable, foliage and so on
- `snag_hazard` flags on edges, battens, rebar, wires and branches
- **gaps:** planar polygons in any orientation, measured from the real clear space, with the narrowest width recorded
- **wind volumes:** primitives, each with its own porosity (D-007)
- landable-surface flags (roofs, beds, slabs)
- LOD switch distances

*Accept:* a probe flies a drone-sized capsule (0.574 m nose-first) through every gap and contacts every material. *Source:* D-012, PR-5, PR-9, K§10.6.

**AST-06 Damage is physical and parametric.**
- **Buildings** have the states **intact-weathered, damaged and destroyed**:
  - stripped roofs with broken battens
  - jagged openings with broken brick courses
  - **protruding rebar** in concrete
  - collapsed walls, soot
  - rubble with brick, tile and plaster colours
- **Vehicles** have the states **abandoned, blast-wrecked and burnt-out** (K§9.2, R0).
- **Damage geometry carries collision.** Openings are the real clear space.

*Source:* PR-5, K§9.

**AST-07 Texturing rules.**
- UVs are **in asset space, upright and continuous** across panels; walls use facade maps plus damage masks.
- **No visible tiling or chequerboarding at 10 m.** Bricks are upright.
- Materials use photo-like albedo. Adjacent materials are distinct in hue, or blended deliberately.
- Texel density targets: about 512 px/m on surfaces the pilot passes within 2 m (walls near openings, ground), and about 256 px/m elsewhere.

*Source:* K§14.4.

**AST-08 Budgets per class.** The phase-1 spec sets triangle counts per LOD (3–4 LODs), and far impostors for trees. Textures are **2K at most**, with shared atlases and trim sheets for draw-call economy. Sum check: the world's VRAM stays within PLT-05.

**AST-09 Vegetation.**
- Late-summer to autumn mixed foliage (green, yellow, part-bare).
- Visual wind sway driven by the wind level (later, by the wind field).
- **Tree belts carry bushes among the trees** (PR-10).
- Dead and broken variants.
- Fine twig crowns that read at distance.
- **At 200 m a belt reads as a dark band.**
- Clean skyline silhouettes for night.

*Source:* K§9.2.

**AST-10 Micro-detail.** Grass stems, lying straw, twigs and litter are drawn near the camera from the **same deterministic generator the physics uses** (WLD-11), at 0 mm parity. A near-ring fade plus a far density layer to ≥ 60 m leave no hard edge. Litter has leaf silhouettes, not flat chips. *Source:* V§4.1, K§14.4.

**AST-11 Ground materials** for every surface in the surface table (K§7.2): meadow_sod, dry_crust, crater_spoil, belt_straw, belt_bare, tilled (with directional furrows), burnt_field, weeds, yard_litter, rubble, asphalt, concrete_slab, dirt_track. Each pairs a visual material with physical parameters. Transitions are feathered.

**AST-12 Asset review sheet.** Every asset gets automated screenshots at **1.5 m, 10 m, 50 m and 200 m**, plus a turntable and a collision overlay, at day and overcast. **Every asset that appears in a reference also gets its side-by-side against the reference** (REAL-03, local only). QA judges them against the object sheet and the reference before the asset enters the gallery. *Source:* K§14.1.

---

## 6. Gallery (GAL): phase 2, the first release

**GAL-01 Content.** One map (about 512–768 m) holding:
- a **station per asset**, with all its variants and states side by side
- a patch per **surface**, with feathered edges
- a **relief** patch
- a **tree-belt section** with a trench
- a **village yard set**
- a **crater field**
- a **wire and pole span**
- a **vehicle row**
- the Stalin-era block with its rubble

Unbuilt items keep a reserved, labelled station. *Source:* B plan 2, old D-017.

**GAL-05 Reference views in the gallery.** The gallery holds a recreated reference view (REAL-02) for every letter its contents cover (B, C, D, E, F, G, H, I, J, K, M, N, O). **R** cycles through them, framed as in the reference. The same views produce the REAL-03 comparison sheets.

**GAL-02 Review controls.**
- PRD-07 movement
- **L** toggles labels (outside the feed)
- **T** cycles time-of-day presets (dawn, day sun, overcast, dusk, night)
- **F12** takes a screenshot to a local folder
- **F1** shows help

**GAL-03 Build.** A Windows standalone build that meets PLT-02 (noclip) and PLT-06.

**GAL-04 Gate G2.** The pilot explores the gallery. Every feedback point becomes a task, fixed or deferred with a reason, before G3 work uses the asset.

---

## 7. World (WLD): phase 3

**WLD-01 Composition** (D-018), about 4 × 4 km:
- **three adjacent villages**, linear, along roads, each about 0.6–1.2 km of street
- fields: meadow, tilled, burnt, cratered
- **tree belts** crossing the fields, several with trenches along them
- one **urban edge** with the single destroyed Stalin-era block, its rubble and garages
- vehicles on roads, in yards and at field edges

*Source:* B plan 2–3, K§4.5.

**WLD-02 Relief.** Real rolling ground: 2–5 m over hundreds of metres, crests, dips, a gully, road embankments, spoil banks. Never a flat plane. Crests hide what is behind them. *Source:* PR-8.

**WLD-03 Organic surfaces.** Every surface area has an organic, feathered outline in both the visual and the physical layers. *Source:* PR-11.

**WLD-04 Belts.** 10–30 m wide, hundreds of metres long, trunks 1–3 m apart, with an understory, **bushes among the trees**, clearings and gaps. They are porous windbreaks.

**WLD-05 Villages.** Each yard has:
- a house, 1–3 outbuildings, a cellar
- a garden plot (tilled), fruit trees, a fence and gate
- litter, weeds and brush piles

Along the street: wires and poles. The damage mix is plausible for a frontline village: abandoned, damaged, destroyed, with craters in yards and roads. Vehicles are abandoned or destroyed.

**WLD-06 Roads.** A damaged asphalt village road with potholes and craters, dirt tracks with ruts, and **concrete-slab tracks** across the fields (PR-8).

**WLD-07 Fly-ins.**
- At least **30 authentic fly-in targets**: cellar doors (0.70 m), broken windows, doors, roof breaches, gable breaches, open rooms of the block, gaps between structures, belt corridors.
- Each one is a catalog gap sized to the drone: **passable nose-first with a few centimetres a side, and not with careless yaw**.

*Source:* PR-9, V§2.3.

**WLD-08 Start points.** At least 6, in varied terrain. With legs off, the launch stand (D-022) is placed at the start point as a runtime object.

**WLD-09 Editable map.**
- The map is a `layout.json` (villages, yards, roads, fields, belts, trenches, craters, points of interest, object placements) compiled into the package by `tools/map/compile`.
- **A pilot-requested change** (move a belt, add a house, widen a trench) is a layout edit and a rebuild, and takes **≤ 15 minutes of agent time**.
- The validator guards every rule.

*Source:* K§2.2, D-007.

**WLD-10 Night readability.** Crests, lone trees, poles and belt crowns keep clean skyline silhouettes at distance. Their LODs preserve them.

**WLD-11 World-query core** (engine-free, in `com.fpv450.sim`):
- **the queries:** `SampleGround`, `MicroDetailNear`, `StaticContacts` (swept, with `Time`), `Geometry`, `Raycast`, `WindGrid`, `GapsNear`, runtime objects with reset, content hash, `QueryVersion`
- **determinism:** IEEE basic operations only; a golden file in Debug and Release; batched results identical to single results; a concurrency test
- **performance:** the K§10.4 budgets

*Source:* K§10, D-005, D-006.

**WLD-12 Validation.** The map validator checks:
- layer sizes and indices
- unknown assets, materials or surfaces
- physical ranges
- out-of-bounds objects and starts
- the format version
- georeferencing (SEC-03)

**WLD-14 Reference views in the world.** Every letter (A–H, L, P, I–K, M–O) has at least one place in the world that recreates its scene type (REAL-02, REAL-04), reachable from the review controls. The world is judged against the references through these views before G3.

**WLD-13 Gate G3 (UAT-2). The pilot explores the full world in noclip (PRD-07) and gives final map feedback.

---

## 8. Rendering and look (VIS)

**VIS-01 Pipeline.** URP Forward+, with the GPU Resident Drawer and occlusion culling where they help. One LOD system only: Unity LOD groups driven by catalog distances, with no engine auto-LOD on top. *Source:* D-001, K§14.4.

**VIS-02 Time of day** is continuous, in hours. Sun and moon position, sky, ambient and night light all follow it.

**VIS-03 Sky and rain look** (D-021; tuned in the phase-1 look-lock):

| Rain setting | Look |
|---|---|
| Sunny | dry; cloud cover drawn per flight, from clear to overcast; the wind level adds cloud motion |
| Rain | **overcast**, wet materials (darker, glossier), a grey neutral sky, dark ground that keeps its colour |
| Severe rain | heavier still: darker overcast, standing water sheen on hard surfaces, rain extinction in the distance |

Rain shows no visible falling streaks in the feed (K§8.7). **Lens drops** (FEED-13) are the main rain cue in the goggles.

**VIS-04 Distance.** Aerial perspective is tuned so a belt at 200 m reads as a dark band and the horizon matches the footage's tone (K§9).

**VIS-05 Shadows, AO and vegetation sway.** Sway amplitude follows the wind level, and later the wind field.

**VIS-06 Effects hooks** for phase 7: dust under prop wash on dry surfaces, ash plumes on burnt ground, dust haze in belts. These are visual only.

**VIS-07 Prop blades and airframe parts in view.** The FPV camera sees prop blades crossing the top corners and airframe parts at mid-height, matching the reference set-up (K§11). They are rendered as geometry, not as a feed effect.

---

## 9. Analog feed (FEED): phase 4

The trait definitions and every measured number are in K§11. **The algorithms come from the Godot feed** (FEED-00), so K§11 is the reference for the traits Godot never built.

**FEED-00 Port the Godot feed faithfully** (D-026, the pilot: "very much similar to those under the godot codebase").
- The Unity feed's noise algorithms are a **faithful port** of the Godot feed in [legacy/video-feed-godot/](legacy/video-feed-godot/): `video_feed.gdshader`, `FeedEvents`, `FeedLoss`, `FeedOsd`, `FeedFont`.
- That means the same pipeline, constants, hash, event rates, loss sequences and OSD geometry.
- Additions (O1 lens, AE/AWB, N13, lens drops, physics drivers) layer on top, without changing the ported output while they are off.
- **The only change** inside the port is the name: "Svinoriz 10 Opto".
- *Accept:*
  - a field-by-field diff of the event schedule against Godot for the same seed (15,000 fields)
  - visually matching stills at the same seed and field for every stage (the A/B in that folder's README)
  - then G4

**FEED-01 Simulated, not filtered.** Artifacts come from signals (FEED-03). Free-running random rates are used only where K§11 says no cause is visible. *Source:* D-008.

**FEED-02 Pipeline and resolution.**
- The stage order follows K§11.7.
- The FPV camera renders at reduced resolution with extra FOV (D-017).
- The analog stages run at field resolution (about 720 × 288), **50 fields/s**, then bob-deinterlace and upscale.
- **PAL only** (D-009).

**FEED-03 Signal interface.**
- `IFeedSignals` carries every signal in K§11.8, plus rain rate and air velocity at the camera.
- **In phase 4, labelled stubs** drive it: noclip camera rates, a scripted link margin, and debug triggers.
- **In phases 6–7, physics replaces the stubs.**
- The log records which signals are stubbed.

**FEED-04 Traits.** Each trait listed below is implemented with the parameters measured in K§11:
- N1 grain, N2 sparkles, **N3 and N4 always on** (as in the Godot feed the pilot chose; not per-flight draws), the quality-drop envelope (`degrade`), N8 tear, N11, N12 recovering dropouts, N13 dim-light steps
- the staged loss and the hard cut
- P1–P5, P7, P8
- O1 (k1 ≈ 0.33), O2, O3, O4
- C1 AE, C2 AWB, C3, C4, C5 night

N9, N10 and P10 are **not** simulated.

**FEED-05 Losses, as in the Godot feed.**
- **Terminal loss** fires on impact, power loss or fiber break, never on a random clock. The variant (staged or hard cut) follows how fast the link margin fell (K§11.3).
- **Recovering dropouts (N12)** keep the Godot timer the pilot asked for: **3–6 per minute**, with gaps of 5–32 s drawn as u².
- **Quality drops** keep theirs: about 10 per minute, 0.3–2.2 s long, depth 0.25–1.0.
- **Physics adds, never replaces.** From phase 7, link-margin dips from the fiber add events and modulate N2 on top of those baselines.
- **There is always a tell before the picture goes.**

*Source:* PR-4.

**FEED-06 Clean recovery.** After a recovering dropout, the picture and OSD come back clean. Receiver text appears only on blue screens. *Source:* PR-4 correction.

**FEED-07 Snow texture.** Near-binary white dashes on black, one field line tall, colour-killed after the first 1–2 fields, under the receiver text. **Never grey Gaussian noise.**

**FEED-08 Night** is the same pipeline at its low-light end: monochrome, strongest grain, N13 on. There is no separate night filter.

**FEED-09 Seeded and tunable.**
- Every feed draw comes from a seeded stream that is logged.
- No two losses look alike.
- All rates and ranges live in one config asset.

**FEED-10 Performance.** The feed's GPU cost is **≤ 1.0 ms** at 1080p output on the dev machine.

**FEED-11 Review tools:**
- freeze the field and step through fields
- pin the seed
- trigger any event from a debug panel
- a local-only A/B viewer against reference stills (never committed)

**FEED-12 Acceptance.** Three comparisons:
- against the Godot feed (FEED-00)
- **against the footage itself**, through the reference views (REAL-03), on the K§11 traits
- the measured statistics below

In detail:
- A local comparison sheet per trait, against the reference letters, using the measured statistics where K§11 gives them: grain σ, luma and chroma bandwidth, k1 ± 0.03, AE and AWB τ, loss stage lengths, snow statistics.
- Then **gate G4**: the pilot flies noclip through the feed and judges it.

**FEED-13 Rain drops on the lens** (the pilot overrides the clip-P reading, D-021).
- In Rain and Severe rain, drops land on the lens and **stay, even in fast flight**. **They travel across the lens with the airflow** at times.
- Drop size, adhesion, how far they slide under the air shear at the lens, merging and shedding are **computed**, from rain rate and the air velocity at the camera. They are never asked of the pilot.
- They are rendered as out-of-focus refractive blobs and smears ahead of the analog stages, so they take the feed's softness.
- In phase 4, stubs drive the rain rate and air velocity; physics replaces them in phase 7.

---

## 10. OSD

**OSD-01 Exact grid.**
- A character OSD on a **30 × 16 grid**, cell **67.3 × 71.0 px** at 1920 × 1080, with the origin inset half a cell. The grid overflows the frame (2019 × 1136), and that is correct.
- The glyph is **5 × 8 samples in a 14 × 18 cell**: ink about 23–24 × 29–30 px, white with a black outline.
- The font and crosshair match the footage.

*Source:* PR-2a, K§11.6.

**OSD-02 Strings.** "Svinoriz 10 Opto" and "! SAFE !". Every value is invented. *Source:* D-011.

**OSD-03 Content from sim state:**
- armed state, flight timer
- battery voltage under load, current
- link quality (from the fiber link margin)
- the dotted artificial horizon (from attitude)
- warnings blinking about 7 frames on and ≥ 5 off

Train mode shows what the pilot needs; the layout positions come from the footage.

**OSD-04 Analog path.**
- Inserted **before** the analog stages and **not lens-distorted**.
- Tears with N8 and dims with N13.
- Band-limited into the chroma detector: fringing on OSD rows about 7.9, against about 105 without the limit.
- The scene smears horizontally while the OSD stays crisp (chroma trap).

*Source:* K§11.6.

---

## 11. Drone (DRN): phase 5

**DRN-01 The model.** "Svinoriz 10 Opto", researched from typical Ukrainian fiber-optic heavy quads:
- 10-inch X frame (452 mm wheelbase class), 3115-class motors, **3-blade 10" props**
- **the fiber coil underneath**
- **two battery packs**
- **a payload on top**, generic and without functional detail
- FPV camera at **0° uptilt**
- fiber transceiver, flight-controller stack, wiring
- **plastic legs 25–35 cm** in the legs-on variant, and a legs-off variant

*Source:* B plan 5, B req 4, K§5.

**DRN-02 Exact geometry.** Prop tip to tip is **0.574 m nose-first and 0.706 m on the diagonal**, ±5 mm. Gap sizing depends on it. *Source:* PR-9.

**DRN-03 One component spec.** `drone.json` lists every part's mass, size, position and material.
- The visual model **and** the physics mass and inertia build both read it.
- Payload mass scales the payload part.
- The coil mass falls with payout, and the CoM shift is computed.
- The inertia ranges are checked against K§5.2, after re-deriving for payload on top and two packs.

**DRN-04 In-view parts.** Prop blades and airframe parts visible from the FPV camera match the reference set-up (VIS-07). Props show motion blur and spin with rpm.

**DRN-05 Animation hooks.** Prop spin, leg flex (from physics), coil payout, and the trailing fiber line (a thin rendered line through the tether nodes).

**DRN-06 Gate G5.** A gallery station with both leg variants, a turntable and the launch stand. The pilot approves the look.

**DRN-07 Launch stand** (D-022).
- **Shape:** an X-shaped steel base with **four posts about 0.25 m high, one under each motor**. The posts sit on the motor diagonals (0.226 m from the centre for the 452 mm wheelbase), and the drone rests on its arms just inboard of the motors.
- **Clearance:** the coil hangs clear of the base.
- **Sizing:** every dimension is derived from the Svinoriz geometry in `drone.json`.
- **Physics metadata:** collision, material steel, and its mass, so physics can compute tipping and dragging.

---

## 12. App and UI (APP): phase 6

**APP-01 Wireframes first (gate G6).** Main menu, setup, calibration, in-flight pause, and the post-flight summary. The pilot approves before any UI is built. *Source:* V§4.3.

**APP-02 Main menu.** Train, Calibrate and Exit (plus Explore in review builds). A clean, readable design.

**APP-03 Setup.**
- payload 0–2.0 kg in 0.1 kg steps
- legs on or off
- time of day (continuous, night included)
- wind (Calm / Windy / Severe)
- rain (Sunny / Rain / Severe rain)
- start point
- **Fly**

The last setup is remembered.

**APP-04 In flight.**
- Arm and disarm on a transmitter switch.
- Pause and restart.
- **The flight ends** on a crash (an impact over its threshold, or upside-down on the ground), a fiber break with failsafe, a battery cut-off, or disarm.
- The **post-flight summary** shows the seed, the log path and the key events, with the logged crash cause.

**APP-05 Persistence.** Calibration profiles, rates, setup and quality preset are saved to the user's profile folder.

---

## 13. Controller input (INP): phase 6, before basic physics (D-016)

**INP-01 Devices.** RadioMaster TX12 in USB joystick (HID) mode (EdgeTX or OpenTX), plus generic HID joysticks.

**INP-02 Calibration wizard:**
- centre the sticks, then sweep the extremes
- detect the channel order (AETR/TAER), reversal, endpoints and deadband
- assign the arm switch
- a live bar display of the raw and calibrated axes
- save named profiles
- **a calibration health check:** if an axis spans only a tiny range, it behaves like a switch. Say so plainly and tell the pilot to recalibrate the radio. The pilot hit exactly this on a TX12 MKII (EdgeTX 2.8.0): spans 79 and 85, against healthy 683–903 ([reference-notes/fc-profiles.md](reference-notes/fc-profiles.md))

*Accept:* a TX12 calibrates in under 2 minutes, with no keyboard needed after the start. *Source:* V§3.

**INP-03 Flight-controller settings: Betaflight 4.5 semantics** (D-023).
- **Settings in v1:** **ACTUAL rates** (centre sensitivity, full-stick rate, expo per axis) and the **throttle curve** (`thr_mid`, `thr_expo`, Betaflight's 11-point table), with curve previews.
- **Defaults: the "sim default" column** of [reference-notes/fc-profiles.md](reference-notes/fc-profiles.md), which is the pilot's flown set leaning toward the factory set, mostly on throttle:
  - roll and pitch: 55 / 225 °/s, expo 0.34
  - yaw: 85 / 260 °/s, expo 0.15
  - throttle: `thr_mid` 50, `thr_expo` 10
  - deadband 2 / 2, RC smoothing 50, feedforward 50
- **Profiles** are stored with **Betaflight CLI names**.

**INP-07 Room for the rest** (D-023). The settings layer must be able to expose any other Betaflight flight-feel setting without a redesign: deadband, RC smoothing, feedforward (transition, jitter, boost), PID stiffness steps, battery sag compensation, throttle softer/stiffer, and import of a real `diff all`. They are not in the v1 UI, because the pilot "MAY ask" for them later.

**INP-04 Timing contract** (designed once):
- HID reports are timestamped on arrival with a high-resolution clock and passed lock-free to the physics thread.
- The physics step uses the latest sample (or interpolates, as decided in the spec).
- **Stick reading never depends on render frame rate.**
- The TX12's report interval is measured and recorded.
- End-to-end latency from stick to motor command is measured and reported.

*Source:* D-016.

**INP-05 Disconnect.** USB loss triggers a failsafe disarm, which is logged.

**INP-06 Keyboard fallback** for development only. It is never used for tuning.

---

## 14. Basic flight physics (PHB): phase 6

**PHB-01 The final architecture from day one:**
- an engine-free core (D-006)
- a **1 kHz fixed step on its own thread**
- a double-buffered pose that the renderer interpolates
- seeded named streams
- the binary log and **headless bit-exact replay** (LOG-01, LOG-02)

Phase 7 adds mechanisms and never rewrites this. *Source:* D-012.

**PHB-02 Rigid body.** Mass and inertia come from `drone.json` (DRN-03): a quaternion attitude and semi-implicit Euler integration.

**PHB-03 Propulsion basics:**
- **a Betaflight 4.5 flight-controller model** (D-023): ACTUAL rates, throttle curve, deadband, RC smoothing, feedforward, PID with Betaflight's scaling, D min, I-term relax, throttle boost, acro only, no self-levelling
  - **default PIDs are the real builder's:** roll 65/20/70, pitch 60/20/65, yaw I 30
  - the FC loop may sub-step at the real 4 kHz inside the 1 kHz step, deterministically
- an **X mixer that saturates honestly**
- ESC and motor first-order lags
- thrust T = k_T·n² and torque Q = κ·T from momentum theory
- battery voltage sag; **the pack voltage (6S or 8S Li-ion, two packs) is set in phase 5**
- *Accept:*
  - hover throttle and thrust margin match K§5.3 at nominal mass
  - **a loaded drone lifts off near 50–65 % motor power** (a real heavy quad: about 62 %)
  - **the real builder PIDs give a stable, well-damped loop** on the sim plant: a check that the plant is realistic

**PHB-04 Aero basics.** Body drag with along and across areas on the relative wind, rotor H-force, and coil drag below the CoM.

**PHB-05 Contact basics:**
- legs as spring-dampers on `SampleGround`
- launch-stand and object contacts through `StaticContacts` with the **Time < 1 latch rule** (K§7.5)
- crash detection
- `tunnel` events logged as bugs

**PHB-06 Wind basics.** A mean wind per wind level plus simple turbulence, **clearly labelled as a stand-in** until PHY-13.

**PHB-07 Gate G7 (MVP-1).** The pilot launches, connects the TX12, calibrates, takes off from the ground and from the launch stand, flies, lands, and reads the post-flight summary. The log replays bit-exactly.

---

## 15. Full physics (PHY): phase 7, delivered as MVP-2..n

The design to start from is [legacy/flight-model/](legacy/flight-model/), condensed in K§6–§8. Every mechanism is **physical and seeded, never noise on the pose**. Each subgroup below is its own OpenSpec change and its own MVP.

### 15.1 Ground and landing
**PHY-01 Soil, mat and friction** per K§7.1–§7.2: elasto-plastic Winkler soil, a mat in series, a stick-point friction anchor, ploughing, footprints, all blended per surface.
- **Wet ground** (D-021): in Rain and Severe rain, every surface gets **wet parameters**: lower bearing, more sink, and **adhesion and suction** that hold a foot on unloading (mud). Wet straw and grass are heavier and cling. These are researched soil-mechanics values; the first attempt left water and mud out.

*Source:* V§2.2.

**PHY-02 Landing bounce.** It emerges from leg compliance and residual thrust, with nothing scripted.
- **Hold the sticks and the drone leaves the ground; cut the throttle and it stays.**
- The separation is **computed**, never asked (D-024).

*Source:* V§2.6, K§6.6.

**PHY-03 Liftoff stick and shake.**
- **Hooks** (straws, twigs, weeds; keyed by `Id`; release forces from the surface table) and **stiction** in soil the foot has sunk into.
- One held foot pivots the drone and tips the horizon. The effective weight changes as it unsticks.
- Per-rotor ground effect over uneven ground adds a rolling moment.
- **The pilot's account sets the targets:** "sometimes, a little bit" on firm dry ground; **more often on softer ground**; **most of the time in rain**. The horizon tips a little.
  - The rates come from the physics (hooks, stiction, wet adhesion), and are checked against that account.
  - The first attempt measured about 0.9 hooked legs per liftoff on belt straw, which is too high for "sometimes".

*Source:* V§2.1, K§7.3.

**PHY-04 Unexpected ground.** Pitfalls (logged by `FeatureId`), crater rims, slopes, rubble, furrows (rocking across them, not along), leaf-litter slide, brittle char stubble. *Source:* V§2.2.

**PHY-05 Stems and twigs.** Cantilever forces giving touchdown jitter and drag when skimming weeds; twigs as rollers; prop strikes logged.

**PHY-06 Launch stand** (D-022).
- The drone rests on four post tops: hard steel point contacts under the arms.
- At liftoff an arm can catch or slide on a post, and an uneven push off the posts tips the horizon.
- The unanchored stand can tip or drag if an arm or the fiber catches it.
- The spool clears the base.

### 15.2 Propulsion and airframe
**PHY-07 Motor asynchrony.** Kv spread, ESC timing jitter and prop imbalance, drawn once per flight and bounded.
- With wind off on flat ground, the drone drifts and needs roll and yaw correction **at a rate inside a band**. The band is **computed** from researched component tolerances (motor Kv matching, prop balance, ESC timing) and never asked (D-024). The pilot judges the feel at the MVP.
- With asymmetry off, it is visibly steadier.
- It never buzzes or diverges.

*Source:* V§2.4.

**PHY-08 Ground effect** per rotor (Cheeseman–Bennett, from per-rotor rays), ceiling effect under roofs, and **prop-wash recirculation** in a close hover, which is never still.

**PHY-09 Settling into the drone's own wash (vortex ring state)**, with hysteresis. Entry and exit are logged.
- **The pilot's account:** "not a question of speed of descent but mostly how much throttle power hence pressure you give as opposed to drones weight. wobble - yes, of course". So the onset is governed by **thrust relative to weight while descending**: a heavy drone on low throttle sinks into its own wash and wobbles.
- **The model:** descent rate normalised by the induced velocity at the *current* thrust, with thrust-to-weight driving it, not descent speed alone.

**PHY-10 Throttle-punch shake (PR-1).**
- After a fast descent is arrested by a sharp throttle increase: a damped, high-frequency disturbance of **0.3–0.7 s**, seen mainly as **camera shake**.
- It depends on descent rate, input abruptness and payload, and doesn't fire on every throttle increase. It varies between occurrences.
- **The pilot: it "doesn't happen much".** Its occurrence comes out of the physics (wash re-ingestion, frame resonance near the 50–60 Hz the real filters notch, payload and spool pendulum), and is never tuned to a pilot number.
- It publishes the camera-vibration signal.

**PHY-11 Battery.** State of charge and sag over the flight. The heavy corner has no thrust margin left. Low-voltage warnings go to the OSD.

**PHY-12 Inertia character.** Pitch inertia exceeds roll, and yaw is only 1.0–1.3 × roll. **Yaw saturates first**, so the mixer desaturates honestly. *Source:* V§2.4, K§5.3.

### 15.3 Wind and turbulence
**PHY-13 Wind field.**
- Three layers (K§6.7): log-law mean profile, seeded broadband turbulence reaching 2–4 Hz, and obstacle wakes from the wind grid (belts, buildings, vehicles), plus jets through gaps and terrain shelter.
- **PHY-13a (PR-3):** vorticity in the field and **per-rotor gust gradients**, which produce **wind-alone yaw swerves** and **coupled nose-up plus yaw events**, with awkward intermediate states when corrected.

**PHY-14 Weather levels meet the targets.**
- Severe, Windy and Calm reproduce the K§8.6 target table, flown by the simulated pilot of K§8.5 on the test route, measured automatically.
- Calm is flown with asymmetry off.
- **Calm's floor** ("never perfectly still") is a new target, owned by PHY-07.

**PHY-15 Per-flight wind.**
- Prevailing direction (uniform) and strength (uniform in the level's band) are drawn from the seed and logged.
- Meander and rare shifts follow K§8.3. It is **never a fixed vector** (T0c).

**PHY-16 Horizon–heading trade-off.** It emerges because gusts are real sideways forces, the camera has 0° uptilt, and rate-mode kinematics are exact (T10b, T11b). *Source:* pilot Q7.

**PHY-17 Rain** (D-021):
- wet mass and drag on the fiber span
- wet soil and cover (PHY-01, PHY-03)
- the rain-rate signal and **the air velocity at the camera**, for the lens-drop model (FEED-13)
- rain is independent of the wind level

**PHY-17a Cruise in wind.** The pilot flies strong wind at **normal cruise speed** and works on correcting course drift ("i cant say speed is different"). So the airframe drag is tuned so that clip P's 22.7° pitch at about 4.8 kg gives the published cruise, **17–22 m/s**. That is the low-drag reading of K§8.2, a crosswind of about 6.5–8.5 m/s.

### 15.4 Fiber tether
**PHY-18 The spool** pays out one way (it never retracts). The span uses ≤ 32 lumped nodes with gravity, wind drag and ground and vegetation contact (hooks, twig capstans). Tension acts at the spool exit, below the CoM. Coil mass leaves the airframe.

**PHY-19 Cut risk, never certainty.**
- Tension, bend radius over edges (material `edge_radius_m`) and abrasion are compared against a **per-flight fiber-strength draw**.
- **What cuts it in practice** (pilot): "overly sharp reverse, snagging on trees, very sharp lift off, or sudden momentum backwards after lift off". The model must produce each of these from physics:
  - **sharp reverse:** the one-way spool can't take line back, so it loops, loads or meets the props
  - **snagging on trees:** branch hooks and capstans
  - **a very sharp liftoff:** spool payout inertia and friction limit how fast line can leave, so a hard acceleration spikes the tension
  - **backward momentum right after liftoff:** the drone moves back over the line lying at the launch point, so the line can enter a prop disc (**a line–prop contact is a cut**) or snag on the stand
- **Smooth orbits keep it safe.**

*Source:* V§2.5.

**PHY-20 Link margin** comes from tension, bend and vibration, as **event-like dips** (spike, snag, sharp bend), with wind and trees raising their rate. It never follows attitude rate or rain on the lens. *Source:* K§8.7.

**PHY-21 Fiber break** loses the link: the staged picture loss plays, then the flight controller's failsafe disarms. It is logged with its mechanism.

### 15.5 Wires and structures
**PHY-22 Wire strikes by diameter.**
- **Thin wire** catches, wraps and tangles in the props, then snags and drags the drone, or holds it.
- **Thick cable** deflects, stops or flips it.
- Wire compliance comes from span tension (T = w·L²/(8·sag)).

*Source:* PR-6.

**PHY-23 Openings.** Jagged edges and rebar collide physically. Gaps are the real clear space. Turbulence funnels through narrow openings. *Source:* PR-5, PR-9.

### 15.6 Randomness and interfaces
**PHY-24 The randomness rule.** Randomness enters **only** as bounded perturbations of named physical parameters, drawn from named seeded streams and logged.
- A catalogue lists every stream with its bounds.
- Ground and cover are world-derived and never drawn: "the ground is not random, it is unknown, and the pilot learns it".

*Source:* V§2.4, K§6.9.

**PHY-25 Physics → feed.**
- Every signal in K§11.8 is published as 50 Hz field aggregates (mean and max), replacing the phase-4 stubs.
- Impacts and fiber or power loss trigger loss events.

---

## 16. Logging and replay (LOG)

**LOG-01 The flight log.**
- **Header:** seed, every per-flight draw, settings, mass and inertia, world content hash, `QueryVersion`, build, runtime and ISA flags.
- **Full state at 1 kHz.**
- **Events at step resolution:** contacts with surface, material and feature; hooks; pitfalls; tunnels; strikes; saturation; VRS; payout; tether break and its mechanism; hitches; impacts with peak acceleration and impulse; wind draws; stub-signal flags.
- **Size:** ≤ 50 MB per minute of flight after lossless compression, written from a ring buffer by a worker.

**LOG-02 Headless replay.** `tools/replay` reproduces every logged state **bit-for-bit without Unity**, and refuses a mismatched world or version.

**LOG-03 Forensics.** A crash report, in the UI summary and in a CLI tool, answers without guessing:
- which part touched what, at what speed and with what impulse
- motor saturation
- battery sag
- the hook or pitfall involved, by id
- tether load or break
- the wind at the time
- the pilot's last inputs

It ends in a verdict: **physics bug, or pilot error**. *Source:* V§4.

**LOG-04 Cross-machine determinism.** The golden and replay tests pass on a second x64 machine (QA, before release).

**LOG-05 Privacy.** Logs hold sim data only, stored under the user's profile.

---

## 17. Testing and QA (TST)

**TST-01 Test layers:**
- dotnet tests for the sim core (sim math, world queries, the golden)
- Unity Test Framework EditMode and PlayMode tests
- Python tests for tools and validators
- benchmarks (PLT-07)

**TST-02 Golden audits.** Any golden re-record is audited case by case. A moved case needs a proven cause.

**TST-03 Statistical targets** must fail known-wrong models (plain Gaussian noise; scheduled shifts). Test lengths are sized by their measured false-failure rate. *Source:* K§14.1.

**TST-04 Screenshot reviews** for every asset and view change (AST-12). The pre-pilot QA review comes before every gate.

**TST-05 Determinism tests** cover concurrency, batched against single results, Debug against Release, and the second machine (LOG-04).

---

## Traceability: requirement → phase and gate

| Phase ([plan.md](plan.md)) | Requirements | Gate |
|---|---|---|
| **All phases** | **REAL-01…05: realism is a copy of the references, with proper geometry** | every gate |
| 0 Setup | PLT-01, PRC-01…12, SEC-01…06, TST-01 | none |
| 1 Assets | AST-01…12, VIS-01…05 (look-lock), SEC-04 | G1 look-lock (short) |
| 2 Gallery | GAL-01…04, PRD-07, PLT-02 (noclip), PLT-06 | **G2 gallery** |
| 3 World | WLD-01…13, AST-10/11 at scale, PLT-04 (queries), PLT-05 | **G3 world (UAT-2)** |
| 4 Feed and OSD | FEED-00…13, OSD-01…04, VIS-07 | **G4 feed** |
| 5 Drone | DRN-01…07 | **G5 drone** |
| 6 TX and basic physics | APP-01…05, INP-01…07, PHB-01…07, LOG-01…03 | **G6 wireframes**, **G7 MVP-1** |
| 7 Full physics | PHY-01…25, LOG-03…04, VIS-06 | **MVP-2..n** |
| 8 Release | PLT-02/03, LOG-04, everything green | **G8 release** |
