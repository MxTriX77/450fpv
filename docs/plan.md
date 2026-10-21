# Plan

This is the pilot's seven-step plan (2026-10-17), "made right". Their order stays; the changes and the reasons for each are listed first. Each phase states its goal, owner roles, work, deliverables, done-means and gate. Requirement IDs refer to [requirements.md](requirements.md).

## What changed from the pilot's plan, and why

| # | The pilot's plan | Refinement | Why |
|---|---|---|---|
| 1 | — | **Phase 0, setup** (one session): branch, Unity skeleton, Blender → Unity pipeline, CC0 fetcher, catalog schema, review-screenshot tool, verification commands | Without it, every agent invents its own pipeline in phase 1, and editability (AST-03) breaks. |
| 2 | "Analyze all videos and images, and create textures and models out of them" | The analysis is **asset-focused** and **reuses the existing measured notes** (terrain, feed, wind). Models and textures are **rebuilt to match** the footage, not extracted from it. | The footage is PAL (about 450 × 286 luma), compressed and motion-blurred, so it can't yield photogrammetry or usable textures. Its value is dimensions, materials, colours, damage patterns and density. The feed and wind numbers are already measured, and re-deriving them would burn tokens for nothing. |
| 3 | — | **Physics metadata authored with every asset in phase 1** (collision, materials, gaps as real openings, wind volumes, snag flags) | Otherwise phase 7 reopens all 100 assets. |
| 4 | Gallery after all assets | **Look-lock (G1)** after the first asset of each class, then mass production. Assets land in the gallery as they finish. | The first attempt's chequerboard walls passed every check and were caught only by eye. Locking the look on 6 assets before building 100 is cheap insurance. It is a 10-minute look at tangible things, not a plan review. |
| 5 | World after gallery approval | The world includes the **map format, the editable layout and the deterministic world-query core** | Physics stands on the world queries. Building them with the world means phase 7 consumes a tested API. |
| 6 | Feed and OSD after the world | Built against **`IFeedSignals` with labelled stubs**; physics replaces the stubs in phases 6–7 | The feed is simulated from flight state that doesn't exist yet. The interface keeps the feed honest, and its rewiring trivial. |
| 7 | Drone design | **One component spec** (`drone.json`) shared by the visual model and the physics mass build. Exact prop-tip geometry. Payload on top, two packs. | Gap sizing and inertia both depend on the real geometry. |
| 8 | "TX controllers and basic flight physics" | **Wireframes gate first** (manifesto); **input before flight tuning** (D-016); "basic" physics is built **on the final architecture** (1 kHz thread, determinism, log, replay) | The manifesto requires wireframe approval. Flight feel can't be tuned without the stick. Rewriting the core in phase 7 would waste the most expensive work. |
| 9 | "Ultra realistic physics" | Split into **5 changes, each its own MVP the pilot flies** (ground, propulsion, wind, tether and wires, physics → feed) | The pilot's flights tune each mechanism while the next is built, and every MVP is tangible. |

## Token and context strategy (the pilot's point 1, "before session gets compacted")

- **Everything lands on disk:** object sheets, decisions, specs, handoffs, reports. Compaction or a new session loses nothing, and the next session reads files, not memory.
- **Image-heavy work goes to subagents with narrow briefs** (one asset class each). They write their findings to `docs/object-sheets/`. The orchestrator never loads footage or stills itself.
- **Contact sheets instead of frames.** `tools/reference/contact_sheet.py` tiles 12–24 stills into one image per clip segment, so one image read covers a whole sequence.
- **Reuse before re-deriving:**
  - [knowledge.md](knowledge.md) and [reference-notes/](reference-notes/) already hold the measured terrain classes, sizes and colours, the full feed analysis and the wind study
  - phase 1 only adds what assets need: construction details, damage, close-up materials, density
- **The most expensive phase comes first, while the pilot's plan wants it:** phase 1 front-loads all footage and photo reading. After it, no later phase needs to open the footage again, except feed phase 4, and only for local A/B stills.
- **One subagent per logical task** (D-029, the pilot's preference): one asset, one object sheet group, one shader stage, one query, one mechanism, one screen, one review. A fresh agent with a short brief each time.
- **Agent hygiene** (PRC-09):
  - at most 4 agents in parallel (D-036)
  - fresh agents with short briefs
  - commit early
  - long jobs in the background
  - no polling

---

## Phase 0: Setup (one session)

**Goal:** a working repo and pipelines, so phase 1 agents only make assets.
**Owner:** orchestrator, with game-developer (Unity skeleton), asset-artist (Blender pipeline) and QA (verification). The brief is [briefs/phase-0-setup.md](briefs/phase-0-setup.md).

**Work:**
1. **Branch.** In `C:\.dev\450fpv`:
   - `git init`, then add the remote `MxTriX77/450fpv` and fetch
   - tag `godot-final` at `b684cce` (pushing the tag needs the pilot's OK)
   - `git switch --orphan unity/main`
   - commit this doc pack and the `.gitignore` (with `reference/`) **before anything else**
   - push `-u origin unity/main`
   - ask the pilot whether to make `unity/main` the default branch, and whether to add a ruleset protecting it
2. **Tools:**
   - install and pin Unity 6 LTS through Unity Hub (record the version), Blender (latest stable, pinned), Python 3.12+, .NET SDK, git-lfs, ffmpeg and `gh`
   - record them all in `docs/environment.md`
3. **Unity project** at `unity/`:
   - URP Forward+ with the quality presets (Low, Medium, High)
   - folders `Assets/_Project/{Art,Prefabs,Scenes,Scripts,Shaders,Settings}`
   - the local package `unity/Packages/com.fpv450.sim` with `noEngineReferences: true` (D-006)
   - `tools/worldbench` and `tools/flightbench` .NET projects compiling the package sources
4. **OpenSpec and skills** ([tooling.md](tooling.md)): verify the CLI; keep the prepared `openspec/config.yaml`; check that the copied `karpathy-guidelines` and `openspec-*` skills and the `/opsx:*` commands load.
5. **Pipelines:**
   - `tools/assets/fetch.py`: pinned CC0 fetch (Poly Haven, ambientCG) with SHA-256, and `CREDITS.md` generation
   - `tools/blender/build.py <asset|all>`: a headless generator run → FBX plus a metadata JSON
   - `unity/Assets/_Project/Editor/CatalogImporter`: applies materials, LODs, colliders, gaps and wind volumes from the catalog
   - one command builds one asset end to end: `tools/build_asset.ps1 <ID>`
6. **Review tooling:**
   - `ReviewCapture`, a Unity batchmode method rendering the AST-12 screenshot sheet (1.5 / 10 / 50 / 200 m, turntable, collision overlay)
   - `tools/reference/extract_frames.py` and `contact_sheet.py` (ffmpeg; outputs to the ignored `reference/_frames/`)
7. **Hygiene:**
   - the pre-commit hook (SEC-06)
   - `.gitattributes` with the LFS patterns
   - check the GitHub LFS quota on the account
   - the `CLAUDE.md` verification commands made real and tested once

**Done means:**
- One sample prop (a brick) goes params → Blender → FBX → Unity prefab with collider and material in one command, and its screenshot sheet renders.
- `openspec validate` passes.
- The pre-commit hook blocks a planted `.mp4`.
- Everything is pushed to `unity/main`.

**Gate:** none. The orchestrator reports to the pilot in three lines.

---

## Phase 1: Reference analysis and asset production

**Goal:** every asset in [asset-list.md](asset-list.md) (P1, then P2), built from editable sources, with physics metadata, judged against the reference.
**Owners:** reference-analyst (object sheets), asset-artist (models, textures), tech-artist (look: URP lighting, sky, time of day, post), QA (review sheets). The brief is [briefs/phase-1-assets.md](briefs/phase-1-assets.md).

**Work, in order:**
1. **Object sheets** (reference-analyst; one subagent per class group, at most 4 in parallel, D-036):
   - re-read the relevant clips and photos via contact sheets, the existing notes, and online research for what the footage lacks
   - write `docs/object-sheets/<group>.md` with dimensions, construction, materials, colours (photo albedo and `feed` check values), damage patterns, density and placement rules, physics role and the params list
   - OPSEC-clean
2. **Material library** (asset-artist + tech-artist):
   - fetch the CC0 sets
   - build URP materials and derived textures (tints, facade maps, damage masks) by script
   - build the 13 ground surfaces with feathered blending (S01–S13)
3. **Look setup** (tech-artist): URP lighting, sky per time of day and rain setting (VIS-02/03), wet-material variants, aerial perspective (VIS-04), the vegetation wind shader.
4. **Look-lock assets:** S04, V01 + V13, H01 in I/D/X, R01 in A/F, E01, L01 + L03.
5. **G1 look-lock:** a quick gallery build with those six stations. The pilot looks for about 10 minutes. Their feedback becomes style rules in `docs/style-rules.md`, and the URP-vs-HDRP check of D-001 is settled here.
6. **Mass production** in the asset-list order, P1 first, after G1's deferrals (D-040): V01's crown net and the H belt edge (`p1-v01-locust` QA 2.1's R1 and R2, with the physics rulings of its task 1.8), H01's X state and letter S's items (its task 1.4b). The crown work judges the 200 m band by its linear-light mean at the feed's resolution, never by the gallery's 1× render, and bakes a crown visibility into the asset (`p1-v01-locust` review-techart.md, findings 6–7). First from letter S (D-038, the S read-in's `implications.md` (archive/2026-10-19-p1-letter-s-village-street)): H02 variant 3, the typical village's street house, as params on the shared `house` generator that H01 writes; L02 with the shared whitewashed butt band (L01's too); K01's standing stove block and silicate bricks. From E01's review (archive/2026-10-20-p1-e01-trench/review.md): each trench asset takes its own grassed cover recipe, since `grassed_spoil` carries E01's relief (S14); a build-time triangle budget for earthworks (AST-08, S9); the joint check by identity, not count (S17). Each asset needs:
   - its generator and params
   - LODs and impostors
   - collision, materials, gaps, wind volumes and snag flags
   - a catalog entry
   - a review sheet (AST-12)
   - QA approval

   Each one also goes into its gallery station as it lands.
7. **Micro-detail generator and renderer:** deterministic, shared with physics (AST-10). The materials sign-off it needs is done (`p1-materials-per-part`, archived: every row per touching part, `razor_wire` and `sandbag` added).
8. **Gap probe:** a drone-sized capsule through every gap, and contact with every material (AST-05).

**Deliverables:** `assets-src/` (params), `tools/blender/` generators, Unity prefabs, the catalog, object sheets, review sheets, `CREDITS.md`.

**Done means:**
- Every P1 asset is approved by QA against its object sheet **and against its reference** (side-by-side, REAL-03), with review sheets attached.
- **The geometry audit passes** for every asset (REAL-05): collision overlay, every opening probed, no physical feature texture-only.
- The editability test passes on 3 random assets (AST-03).
- Budgets are met (AST-08).
- No tiling at 10 m (AST-07).
- The gap probe is green.

**Gates:** G1 look-lock (short). G2 follows in phase 2.

**Risks:**
- **Realism without paid assets.** Realism here means *a copy of the references* (D-028). Mitigations: CC0 photoscans, parametric detail with a lot of damage and clutter, the look-lock early, and judging every asset side by side with its reference letter, through the feed (D-020, D-028).
- **Tree realism.** Mitigations: a solid in-house tree generator (Blender geometry nodes or a Sapling-style Python generator) with photoscanned bark and leaf cards, plus impostors.
- **The LFS quota.** Check it in phase 0.

---

## Phase 2: Gallery, the first release (G2)

**Goal:** a Windows build of the gallery map with every asset, variant and state, ready for the pilot's review.
**Owners:** world-builder (gallery generator), game-developer (noclip, review controls, build), QA.

**Work:**
- **The gallery generator:** the station list from asset-list.md, with stations side by side, labels and a reserved slot for anything unbuilt (GAL-01). It also needs:
  - a surface patch per surface
  - a relief patch
  - a belt with a trench
  - the yard set, the crater field, the wire span, the vehicle row and the block
- **Noclip and review controls** (GAL-02).
- **Performance pass** (PLT-02 noclip, PLT-06).
- **QA pre-review** at every station, at 1.5 / 10 / 50 / 200 m.

**Done means:** every P1 station is filled; **every gallery reference view (GAL-05) matches its reference side by side** (REAL-03); performance and load meet budget; QA is green.

**Gate G2:** the pilot explores and gives feedback. Every point becomes a task, fixed or deferred with a reason. P2 assets continue in parallel.

---

## Phase 3: World (G3, UAT-2)

**Goal:** the full composite map, explorable in noclip, built from an editable layout, with the deterministic world-query core underneath.
**Owners:** world-builder (layout, compiler, terrain, placement), physics-engineer (world-query core review and the contact probe), tech-artist (performance, distance look), QA.

**Work:**
**Before the belts are placed** (`p1-v01-locust` review-techart.md, findings 1–4): the Leaf shader sets up its instance first (latent under the SRP batcher, wrong under instanced or BRG draws); the impostor reads its own SH with the spread, and its reflection widens by it; `LodParity` runs again under G1's day and overcast from 2 m and 30 m; the vegetation shaders' cost is measured per PLT-07 on a synthetic belt at 1080p and at the feed's size.

1. **The map-format spec** for Unity: carry the old format over (K§10), plus the D-007 fixes (polygon gaps, per-part porosity) and the D-019 frames. It and the world-query spec take the gap review's G1–G4, G6 and G8–G11 in full (D-037): terrain holes (WLD-16), the frame conventions and corner mapping, the Unity Terrain tile rule, the rendered-surface parity and the load-time pose parity. Hole ids are local to an asset (`run`, `niche_0`): the compiler namespaces them per placed instance (E01's review (archive/2026-10-20-p1-e01-trench/review.md), S10).
2. **The layout and the compiler** (`layout.json` → package) with its validator (WLD-09, WLD-12).
3. **World-query core** in `com.fpv450.sim`:
   - port and adapt the first attempt's C# (`WorldQuery`, `Contacts`, `MicroDetail`, `WindGrid`, `DetMath`, `ContentHash`)
   - the golden file, the concurrency tests and the benchmarks (WLD-11)
   - the physics engineer runs a contact probe (a fake drone over the API) before sign-off
   - from the fortifications and V01 readings (archive/2026-10-18-p1-fortifications-sheet/review-physics.md §6–§7, p1-v01-locust review-physics §A.4): a `convex` shape (a face and edge list per hull, a swept separating-axis contact test, ray clipping, geometry lookup, golden cases, a `teeth_field` benchmark); a spatial index over snag records and gaps with a record query, a cap of 2,048 record segments per 2 m cube per asset and per map, a `wire_runs` benchmark; a two-level shape index for belts of trees; density counted as narrow tests
4. **Unity world builder:** terrain tiles from the heightfield, surface splatting with feathered edges, vegetation and object instancing from `objects.json`, micro-detail near the camera, impostors, streaming if needed. The compiler writes the gaps between separately placed objects (between dragon's teeth, between road blocks) as the layout places them, sizes long thin wind volumes (wire runs, hedges, porous fences) by their thickness across the wind, not by footprint perimeter / π, and re-derives gap verdicts per instance scale (a 0.706 m gap at scale 0.9 is nose-first only).
5. **The map itself:** the D-018 layout as refined by D-035 (WLD-01). A village copying letter S's street with cars on it (D-038), a smaller village and a somewhat larger town divided by belts and fields in the pattern of the layout reference (letter Q); fields, belts with trenches, trenches in the fields, cratered, burnt and tilled fields, roads and slab tracks, a small river and ponds, the fortifications (F01–F03), the urban edge, most houses damaged or destroyed, at least 30 fly-ins, at least 6 start points.
   - The typical village's placement from letter S (the S read-in's `implications.md` (archive/2026-10-19-p1-letter-s-village-street)): the street line in the house-side verge 3–5 m off the road with lamps on some poles, drops of 8–12 m and 21–27 m; a lone shrub (V13) every 15–25 m on greens; single trees in yards and along the street; cars on the verges by the gates, in yards, on the green and at the road edge.
6. **Performance** (PLT-02, PLT-05, PLT-06) and the night silhouette check (WLD-10).

**Done means:**
- The world validates; the golden passes in Debug and Release; query budgets are met.
- PLT-02 noclip is met at the busiest view.
- **Every letter's reference view exists in the world** and passes its side-by-side (WLD-14, REAL-03).
- The layout-edit test passes: move a belt and add a house in ≤ 15 minutes.

**Gate G3 (UAT-2):** the pilot explores every nook in noclip and gives final map feedback.

---

## Phase 4: Analog feed and OSD (G4)

**Goal:** the goggles view, analog and dynamic, exactly the footage's OSD.
**Owners:** tech-artist (feed, OSD rendering), game-developer (OSD content model, debug panel), QA.

**Work:**
1. The FPV camera rig: a low-resolution render with extra FOV (D-017) and the pinned intrinsics (FEED-02: about 106° horizontal, 16:9, f ≈ 1.005 before k1), and in-view prop and airframe stand-ins until phase 5.
2. **Port the Godot feed faithfully** (FEED-00, D-026, [legacy/video-feed-godot/](legacy/video-feed-godot/)): the shader pipeline, `FeedEvents`, `FeedLoss`, `FeedOsd` and `FeedFont`, with the A/B against the Godot original (same seed: identical schedule, matching stills). Then the `IFeedSignals` interface and its labelled stubs (FEED-03).
3. The pipeline stages in K§11.7 order, at field resolution, 50 fields/s (FEED-02, FEED-04).
4. The receiver state machines: staged loss, hard cut, N12 dropouts with clean recovery (FEED-05/06/07).
5. AE and AWB loops, and night (C1–C5, FEED-08). Letter S is the overcast-daylight check of the grade: its colour table (terrain.md §12.5, the whole-patch means) from neutral albedos.
6. The exact OSD: grid, font, crosshair, strings and the analog path (OSD-01…04).
7. Review tools and the local A/B comparison sheet (FEED-11/12). Before it, a Standard change to the reference tools: the frame-metrics tool run alike on footage and captured feed output, burst extraction and the stable letter index (FEED-12, D-037).
8. The performance budget (FEED-10).

**Done means:** every trait is implemented with its K§11 parameters; the comparison sheet is within tolerance; the feed costs ≤ 1.0 ms; PLT-02 FPV is met.

**Gate G4:** the pilot flies noclip through the world, looking through the feed, and judges noise, losses, colour and OSD.

---

## Phase 5: The drone (G5)

**Goal:** "Svinoriz 10 Opto", researched, modelled and specified once for both visuals and physics.
**Owners:** reference-analyst (research: typical Ukrainian fiber-optic heavy quads, coil, battery arrangement, payload mounting, legs, launch stand), asset-artist (model), physics-engineer (`drone.json`, mass and inertia check), QA.

**Work:**
- An object sheet.
- `drone.json` with the components (DRN-03), re-deriving K§5.2 for the payload on top and two packs.
- The model in both leg variants, plus the four-post launch stand sized from it (D02, D-022).
- The pack voltage and arrangement (6S or 8S Li-ion, two packs), from research.
- Exact prop-tip geometry (DRN-02).
- In-view parts for the FPV camera (DRN-04).
- Animation hooks (DRN-05).
- A gallery station.

**Done means:** the geometry tolerances are met, the inertia is within the re-derived ranges, and QA is green.

**Gate G5:** the pilot approves the look.

---

## Phase 6: Wireframes, TX input, app and basic flight (G6, G7 = MVP-1)

**Goal:** the pilot launches, connects the TX12, calibrates and flies, on the final physics architecture.
**Owners:** game-developer (wireframes, UI, input, app flow), physics-engineer (core, basic model, log, replay), QA.

**Work, in order:**
1. **Wireframes** for the main menu, setup, calibration, pause and the post-flight summary. **Gate G6: the pilot approves.**
2. **Input layer** (INP-01…06): HID, the calibration wizard, rates, and the **timing contract**. It is verified with the live axis display before any flight code.
3. **App flow** (APP-01…05): menu, setup, flight session, persistence.
4. **Physics core** (PHB-01): the 1 kHz thread, named seed streams, the binary log, headless replay, render interpolation.
5. **Basic model** (PHB-02…06): rigid body from `drone.json`, the propulsion chain, aero basics, legs and launch-stand contacts with the Time < 1 latch, the stand-in wind.
6. **Feed wiring:** replace the motor, battery and camera-rate stubs with physics signals.

**Done means:** latency is measured; replay is bit-exact; PLT-04 is met; the basic acceptance tests pass: hover throttle and margin match K§5.3; liftoff near 50–65 % motor power when loaded; the real builder PIDs give a stable loop; stand and ground liftoff work.

**Gate G7 (MVP-1):** the pilot flies with the FpvPrep-based default rates (D-023), and adjusts them in Calibrate if they wish.

---

## Phase 7: Ultra-realistic physics (MVP-2…n)

**Goal:** everything in PHY-01…25, each mechanism physical and seeded, tuned with the pilot's flights.
**Owners:** physics-engineer (lead), tech-artist (feed coupling), world-builder (wind grid and wake data), QA (acceptance harness, forensics).

Five changes, each ending in an MVP the pilot flies:

| Change | Covers | Pilot account it must reproduce (computed, never asked: D-024) |
|---|---|---|
| 7a Ground and landing | PHY-01…06: soil, mat, hooks, stiction, pitfalls, furrows, stems, launch stand, landing bounce, wet ground (from `p1-materials-per-part`: area-type rows (`straw`, `fabric`, `sandbag`) scale by the touching part's contact size, about 3× on straw for an arm against a foot; a linear compliant row gets a stroke limit, since at 160 N/m a 3–6 g landing would send a foot 0.22–0.44 m down; `bark` and `deadwood` gain an elastic modulus for slender members; a shape's own `stiffness_n_per_m` overrides its row, validated in `meta.validate` when the first asset needs it, F01 or a car's bumper cover); from E01's review (archive/2026-10-20-p1-e01-trench/review.md) S16: the merge among one asset's own overlapping shapes (in force: normals within 45° merge, the deepest wins) and how a buried shape's side faces behave, or F-3 restated to read a landable ground top so E01's 25 mm ring boxes can go | legs stick "sometimes, a little", more on soft ground, most of the time in rain |
| 7b Propulsion and airframe | PHY-07…12: asymmetry, ground effect, VRS, PR-1 shake, battery, inertia character | settling into own wash depends on throttle against weight, with wobble; the PR-1 shake is rare |
| 7c Wind and turbulence | PHY-13…17 (the wind and rain statistics tools ported to plain Python first, D-037): field, PR-3 vorticity and gradients, wakes, levels against the K§8.6 targets via the simulated-pilot harness, per-flight draws, trade-off, rain, cruise in wind | wind flown at normal cruise speed; the effort goes into correcting course |
| 7d Tether and wires | PHY-18…23: spool, span, cut risk, link margin, fiber break, wire strikes, openings (with the fortifications reading's §9: the exact bend radius over an edge, `payout_tension_n` named and logged, abrasion per fiber section, barb pitch as a lock point, the fiber skipping compliant stand-in shapes; QA scenarios: a line draped over a coil run, a reverse round a snagged barb); a wire's contact takes the span's compliance per direction: 4T/L pushed aside, Irvine's in-plane value with the tension rise pressed down, about 5× stiffer (`p1-materials-per-part`, the `cable` row) | cuts from sharp reverse, tree snags, very sharp liftoff, backward momentum after liftoff |
| 7e Physics → feed, and forensics | PHY-24/25, LOG-03/04: all signals live, the crash report with a verdict, the second-machine determinism check, lens drops driven by physics | drops stay on the lens even when fast, and travel with the airflow |

**Done means, per change:** its PHY requirements are met with scenarios; statistical targets are proven to fail wrong models; replay is bit-exact; performance is held; the pilot has flown it.

---

## Phase 8: Release (G8)

All requirements are green, including PLT-03 on a mid-range estimate and LOG-04 on a second machine. Release notes go out, and the pilot signs off.

---

**The order after G1 (D-042):** G4 (phase 4), G5 (phase 5) and G7 (phase 6, with the core of phase 7's flight physics: wind, turbulence, weather and the airframe effects) come next, built in parallel lanes, with the test range (D-044: a small map of the Godot yard and belt and Unity's assets) as G4's and G7's map; G6 folds into G7; the fiber (7d) follows G7. Mass production, G2 and G3 are postponed.

## Gates at a glance

| Gate | When | What the pilot gets | Pilot time |
|---|---|---|---|
| G1 look-lock | mid phase 1 | 6 stations in a quick gallery build | about 10 min |
| **G2 gallery** | end of phase 2 | the full gallery build in noclip | as they like |
| **G3 world (UAT-2)** | end of phase 3 | the full world in noclip | as they like |
| **G4 feed** | end of phase 4 | the world seen through the goggles feed | about 30 min |
| G5 drone | end of phase 5 | the drone station and turntable | about 5 min |
| **G6 wireframes** | start of phase 6 | screen wireframes | about 10 min |
| **G7 MVP-1** | end of phase 6 | first flight with the TX12 | flying |
| MVP-2…n | phase 7 | each physics change | flying |
| **G8 release** | phase 8 | the release build | — |

## Dependencies

```
0 setup ─► 1 assets ─► 2 gallery ─► 3 world ─► 4 feed+OSD ─► 5 drone ─► 6 TX + basic flight ─► 7 physics ─► 8 release
                │                       │            │              │              │
                │                       │            └─ IFeedSignals stubs ────────┴─► replaced in 6/7
                │                       └─ world-query core ─────────────────────────► consumed in 6/7
                └─ physics metadata (collision, gaps, wind, snag) ───────────────────► consumed in 3/6/7
```

Work that may overlap without breaking the pilot's order, at most 4 agents at a time (D-036):
- P2 assets during phase 3
- research for the drone during phase 4
- the input layer spec during phase 5
