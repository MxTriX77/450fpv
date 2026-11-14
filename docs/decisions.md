# Decision log

- Status is Proposed, Accepted or Superseded. To change a decision, add a new entry that supersedes the old one, and never edit an old entry.
- "Old D-0xx" points to the Godot log, [legacy/godot-decisions.md](legacy/godot-decisions.md).
- Decisions marked **(verify at phase 0)** depend on facts the next session must check before building on them.

## D-001 Engine: Unity 6 LTS, URP, C# · Accepted (verify at phase 0)
**Closed at G1 (D-042):** URP stays. The pilot flew the G1 preview and rated its stations well (`docs/style-rules.md`).

The pilot chose Unity ("backend - whatever the best").

**What we use:**
- **Unity 6 LTS, the newest 6000.x LTS at phase 0.** Pin the exact version in `ProjectSettings/ProjectVersion.txt` and in `docs/environment.md`.
- **The Universal Render Pipeline (URP), Forward+.**

**Why URP rather than HDRP:**
1. **The pilot sees the world through a PAL feed.** The feed carries about 450 × 286 luma samples and 90 chroma samples per line (D-017). Realism there comes from shape, scale, colour, light and clutter density, not from HDRP-grade shading.
2. **It runs on the dev machine.** That is an RTX 4050 Laptop with 6 GB of VRAM, and it must hold 60 fps with dense vegetation. URP's GPU Resident Drawer and GPU occlusion culling suit instanced vegetation.
3. **It is where Unity is investing.** URP is the actively developed pipeline Unity is consolidating on (verify the current status at phase 0).

HDRP stays the fallback only if the gallery's look-lock (plan phase 1) shows URP can't reach the realism bar. Switching later is expensive, so the look-lock settles it early.

**Language:** C# throughout, plus HLSL and Shader Graph for shaders. Python runs the asset and map tools; Blender runs headless.

## D-002 Repository: `MxTriX77/450fpv`, orphan branch `unity` · Accepted; the branch naming is superseded by D-025 (trunk `unity/main`, features `unity/<prefix>/<change-id>`)
The pilot's choice (2026-10-17): "existing repo, but a new branch or something separate".
- **The Unity work** lives on an **orphan branch `unity`**, with no shared history and no Godot files. It is the trunk for the restart.
- **Feature branches** are `<prefix>/<change-id>` and open PRs with `--base unity`.
- **The Godot work** stays on `main`, untouched, tagged `godot-final` at `b684cce`.
- **The default branch:** setting `unity` as the GitHub default is a pilot-approved step in phase 0 (it changes a repo setting).
- **The repo stays public.** OPSEC rules apply in full, and so does D-003.

## D-003 Assets: free only, CC0 preferred · Accepted
The pilot's choice (2026-10-17).

**Allowed:**
- CC0: Poly Haven, ambientCG, CC0 scans
- CC-BY, with attribution in `CREDITS.md`
- the team's own work

**Not allowed:**
- NC or ND licences
- paid assets
- **any Unity Asset Store or Fab item, including free ones.** Their EULAs forbid redistributing source assets in a public repo.

**Rules:**
- Every third-party file is pinned in `tools/assets/manifest.json`, with source URL, licence, author, resolution and SHA-256, and fetched by script.
- `CREDITS.md` is generated from the manifest.

**Consequence:** realism comes from the team's own **parametric models** (D-004) dressed in **photoscanned CC0 materials** and calibrated against the footage. Hand-modelled hero detail goes only where the reference demands it (damage, openings).

## D-004 Every asset can be rebuilt from an editable source · Accepted
The pilot: "we make sure that we can easily do changes down the line on the models and textures".
- **Models** come from **parametric Blender Python generators** (`tools/blender/`) driven by a **params file per variant** (`assets-src/<class>/<variant>.json`): dimensions, materials, damage seed and level, openings, colour.
  - Hand-sculpted parts are saved as `.blend` sources and referenced by the generator.
  - **Exports (FBX) are never edited by hand.**
- **Textures** are the CC0 sources plus **derived textures made by script**: tints, damage masks, facade maps. Each derived texture records its recipe.
- **Unity import is automatic:** an `AssetPostprocessor` plus a catalog importer set materials, LODs, colliders and physics metadata from the catalog. A rebuild is one command per asset or for all assets.
- **A pilot's change** is a params edit plus a rebuild, never a remodel. The same rule covers the map (D-007).

## D-005 Custom flight physics; PhysX stays out of the flight loop · Accepted
Carries over old D-002/D-010.
- **Our own model.** The drone's flight comes from our own fixed-step model (≥ 1 kHz, seeded, logged, deterministic): rotors and motors, aerodynamics, wind, contacts, tether.
- **Our own world queries.** Contacts and rays for the flight model are our own code, over the map data and the catalog primitives.
- **Where PhysX stays:** Unity PhysX is used only for noclip, UI and render-side needs.
- **Why:**
  - Unity's integration gives the "smooth and rigid" feel the manifesto rejects.
  - PhysX scene queries are tied to the scene and the main thread.
  - The headless replay must run without Unity.
  - We need to control cross-machine determinism.

## D-006 The sim core is engine-free · Accepted
- **One local UPM package,** `com.fpv450.sim`, holds the flight model, world queries, map loading, logging and `DetMath`. Its assembly definition sets **`noEngineReferences: true`**.
- **Plain .NET tools** (`tools/flightbench`, `tools/worldbench`, `tools/replay`) compile the **same source files**, so tests, benchmarks and replay run without Unity.
- **Unity-side code** (rendering, input, UI, feed) talks to the core through a thin adapter, and never the other way round.

## D-007 Map format: engine-agnostic package, compiled from an editable layout · Accepted
- **The package** carries over the first attempt's format (see [knowledge.md](knowledge.md) §10): heightfield `.r16`, surface/cover/holes PNG, `objects.json`, a shared surface table and catalog, a content hash and `QueryVersion`.
- **The authoring layout** sits on top: a `layout.json` describing villages, roads, fields, belts, trenches and points of interest. A map compiler (`tools/map/`) turns it into the package.
- **Unity builds from the package:** an editor tool creates terrain tiles, vegetation and prefab instances from it. The package is the source of truth for physics and rendering alike.
- **Fixes over the first attempt:**
  - **gaps are planar polygons in any orientation**, or clear-space volumes
  - **wind porosity is per wind-volume part**, not per asset

## D-008 The video feed is simulated, not filtered · Accepted (old D-008)
- **Where artifacts come from:** sim state (motor current and frequency, pack voltage, vibration, impacts, fiber tension and bend, link margin, light level, frame content). Measured random rates are used only where no cause is visible.
- **Built in phase 4,** before any physics exists, against a **signal interface** (`IFeedSignals`).
  - **Phase 4 uses labelled stand-ins:** noclip camera motion, a scripted link-margin generator, and a debug panel to trigger events.
  - **The real drivers are wired in phases 6–7.**
  - Stand-ins are visibly named `Stub*`, and the log says which signals are stubbed.

## D-009 PAL only · Accepted (old D-015)
- **In scope:** about 286 lines per field, 50 fields/s, the subcarrier's cross-colour, the line-alternating chroma phase and the delay line.
- **Out of scope:** NTSC, and the broadcast plumbing (blanking, VBI, burst amplitude).

## D-010 Weather is a fixed pilot setting: Calm / Windy / Severe · Accepted (old D-011)
- **Never random.** The pilot always chooses the level.
- **The wind stays alive inside a level:** each flight draws its prevailing direction and strength from its seed, then meanders and shifts at random. Gusts, turbulence and wakes come on top.
- **Rain goes with the level.**
- **Severe is held to the clip-P targets;** Windy and Calm are derived from them (see [knowledge.md](knowledge.md) §8).

## D-011 The drone is "Svinoriz 10 Opto"; the OSD armament label reads "! SAFE !" · Accepted
- **The name:** the pilot, 2026-10-17. It supersedes old D-014's spelling "Svinorez".
- **The label:** old D-014. It keeps a real manufacturer's brand out of the product, and the sim never presents itself as armed.
- **Nobody reverts either one.**
- **Everything else on the OSD** copies the footage layout exactly (PR-2, PR-2a).

## D-012 Order of work: the pilot's seven phases, refined · Accepted
The pilot's order (2026-10-17) is assets → gallery → world → feed and OSD → drone → TX and basic physics → full physics. [plan.md](plan.md) keeps that order and adds the refinements needed to make it work:
1. **Phase 0, setup:** repo, Unity skeleton, pipelines.
2. **Physics metadata authored with each asset in phase 1:** collision, materials, gaps, wind volumes, snag flags. Otherwise phase 7 would redo every asset.
3. **Look-lock:** a short look by the pilot at the first asset of each class in the gallery, before mass production.
4. **The deterministic world-query core in phase 3,** with the world. Physics stands on it.
5. **The feed against a signal interface with stand-ins** (D-008).
6. **One component spec for the drone,** shared by the visual model and the physics mass build.
7. **The wireframes gate before UI work** (manifesto §4.3).
8. **"Basic" physics built on the final architecture:** the 1 kHz deterministic thread, logging and replay. Phase 7 then adds mechanisms instead of rewriting.

## D-013 Reference footage never enters git or any upload · Accepted (old D-004)
- **Where the footage lives:** in `reference/` (git-ignored), which the pilot fills themselves.
- **Extracted frames** live in `reference/_frames/`, also ignored.
- **Agents never copy anything out of `reference/`,** and never write reference file names anywhere.
- **What may go in the repo:** only derived, OPSEC-clean notes, cited by clip letter.

## D-014 Merge commits, never squash · Accepted (old D-006)
- **How:** `gh pr merge --merge`.
- **Why:** squash rewrites authorship to whoever merges, while merge commits keep each role's history visible, as the manifesto asks.

## D-015 Spec-driven workflow with OpenSpec · Accepted (old D-007)
- **No change folder, no work.** Every piece of work has a change folder under `openspec/changes/` before anyone builds it.
- **Phase briefs** ([briefs/](briefs/)) seed the changes.

## D-016 Controller input before flight tuning; the HID timing contract is designed once · Accepted (old D-016)
- **Order inside phase 6:** the TX12 input layer and calibration are built and verified first, then basic flight physics is tuned with the transmitter in hand.
- **The contract, fixed in the input spec:** how stick samples are timestamped and fed into the 1 kHz step (latest-sample hold or interpolation, measured latency).

## D-017 The FPV camera renders at reduced resolution · Accepted
- **Why:** the feed's effective detail is about 450 × 286 luma per field.
- **How:** the FPV camera renders the scene into a low-resolution target. That is about 960 × 720 with extra field of view for the barrel distortion; tune it in phase 4. The feed's analog stages run at field resolution (about 720 × 288), then upscale to the screen.
- **Where full resolution is used:** only in noclip review (gallery and world).
- **Consequence:** this is the main performance lever. Scene detail can be richer than a full-resolution 60 fps budget would allow in the goggle view.

## D-018 Map scale: about 4 × 4 km, three adjacent villages · Proposed (settled in the phase-3 spec)
- **The pilot's words:** "quite big (like 3 adjacent typical ukrainian villages) and rich", "big one, not humongous".
- **Starting proposal:** 4 × 4 km (16 km², up to a 6 × 6 km cap).
  - **Villages:** three linear villages along roads, each about 0.6–1.2 km of street with yards and gardens.
  - **Between them:** fields, tree belts with trenches, a cratered field, burnt and tilled fields.
  - **One urban edge** with the single destroyed Stalin-era block.
- **Sizes are confirmed at UAT-2.** The format allows up to 8 km.

## D-019 Frames and units · Accepted (the physics engineer may refine it in the phase-6 spec, before code)
- **Units:** SI throughout (metres, kilograms, seconds, newtons).
- **World frame, shared by map, sim and Unity:** Unity's own, so there is no conversion at the boundary.
  - **Axes:** +X east, +Y up, +Z north. It is left-handed, as Unity is.
  - **The map:** its centre is the origin; grid row 0 is the northmost row and column 0 the westmost.
  - **Change from the old format:** it used +Z south. The map compiler and the format spec say so.
- **The sim's math library documents handedness and its cross-product convention once.** Body axes: x right, y up, z forward (Unity's), with the origin at the arm-plane centre.
- **Euler angles are for logging and the simulated pilot only:** ZYX yaw-pitch-roll, defined so they match the wind study's tracker conventions.

## D-020 Realism is judged through the eyes the pilot will use · Accepted
- **What realism means:** correct silhouettes, scale, colours (photo albedo, checked through the feed against the footage's `feed` values), lighting, wear and damage, and clutter density. Never texel density for its own sake.
- **Budgets:**
  - textures 2K at most, 4K only for terrain detail atlases if VRAM allows
  - mid-poly meshes with 3–4 LODs
  - far impostors for trees
- **How assets are judged:** screenshots at 1.5 m, 10 m, 50 m and 200 m in noclip, plus a look through the feed once phase 4 exists.

## D-021 Rain is its own setting: Sunny / Rain / Severe rain · Accepted
The pilot, 2026-10-17: "we will have rain on/off setting also in the sim. so rain/sunny/severe rain/night".
- **What it supersedes:** D-010's line "rain goes with the level". The setup now has **wind** (Calm / Windy / Severe) and **rain** (Sunny / Rain / Severe rain) as two independent pilot choices. **Night comes from time of day**, which is continuous.
- **Sunny** means dry. Cloud cover is drawn per flight, from clear to overcast, so a dry overcast day (clips A, H) still occurs.
- **Rain and Severe rain** mean an overcast sky, wet materials and the rain-rate signal.
- **Physical consequences, all computed:**
  - **lens drops that stay on even in fast flight and travel with the airflow**
  - **wet soil:** softer, with adhesion and suction, so **legs stick most of the time in rain**
  - wet straw and grass
  - a heavier, draggier fiber span
- **The pilot's words override the clip-P reading** (`reference-notes/wind.md` §7, R1/R8: no drops seen). The pilot's words win.

## D-022 The legs-off launch stand: a four-post stand, about 25 cm high · Accepted
The pilot, 2026-10-17: "like a chair upside down, so each chair leg is beneath each motor… based this off of the expected size of our future svinoriz", with a photo of a typical stand.
- **The stand:** an **X-shaped base** on the ground with **four vertical steel posts about 0.25 m high**, one **under each motor**.
- **Post positions** come from the Svinoriz geometry: on the motor diagonals, 0.226 m from the centre for a 452 mm wheelbase. The drone rests on its arms just inboard of the motors, and the coil hangs clear between the posts.
- **What it supersedes:** the first attempt's "two parallel bars 0.26 m apart" derivation, and the manifesto's literal "2 parallel iron bars". The pilot's latest description wins.
- **The stand is a runtime object** (material steel), placed at the start point when legs are off. It is not anchored, so physics computes whether it tips or drags.

## D-023 The flight-controller model mirrors Betaflight 4.5; the defaults come from the pilot's profiles · Accepted
- **The source:** the pilot's FpvPrep profiles ([reference-notes/fc-profiles.md](reference-notes/fc-profiles.md)).
- **The model:**
  - ACTUAL rates
  - the throttle curve (`thr_mid`, `thr_expo`)
  - deadband and RC smoothing
  - feedforward
  - PID with Betaflight's scaling, plus D min and I-term relax
  - throttle boost
  - sag compensation
  - optionally the filter chain
- **The config uses Betaflight CLI names,** so a real `diff all` can be imported later.
- **The defaults** are the pilot's flown set, leaning 25 % toward the sharper factory set, and 60 % on throttle ("especially throttle").
- **What the pilot sets:** at first, only rates and the throttle curve in Calibrate. The rest stays configurable, because the pilot "MAY ask… to introduce betaflight rates, throttle curves, and other fly control related settings".
- **The FC loop rate:** the real FC runs its PID loop at 4 kHz. The physics engineer decides whether the FC model sub-steps inside the 1 kHz physics step. Either way it stays deterministic.

## D-024 Never ask the pilot what physics can calculate · Accepted
The pilot, 2026-10-17, on questions about bounce height, drop behaviour, yank frequency and shake occurrence: "why tf are you asking me these questions if it's you whos going to write ultra realistic physics? youll need to fucking calculate it."
- **Never asked:** quantities (heights, rates, frequencies, speeds, sizes, how often something happens). The team computes them from physics and research.
- **Asked, only at gates:** qualitative experience ("does X happen at all", "what causes Y in practice", "what makes it worse") and preferences (feel, rates).
- **Already answered or reference-derivable:** never asked again. "You will have many references."

## D-025 Every Unity branch lives under the `unity/` namespace · Accepted
The pilot, 2026-10-17: "so separate branch ok, but what about feature branches? maybe use some git feature for that or we could use master prefix unity".
- **The trunk** is the orphan branch **`unity/main`**. It supersedes D-002's branch name `unity`.
- **Feature branches** are **`unity/<prefix>/<change-id>`**, with PRs opened against `unity/main`.
- **Why a namespace and not a plain `unity` trunk:** git stores branch names as paths, so a branch `unity` and branches `unity/...` can't both exist. The namespace also keeps the new work apart from the Godot branches, some of which share names (`physics/build-flight-model`, `world/...`).
- **No special git feature is needed.** GitHub has no per-branch-group repositories, and server-side git namespaces aren't exposed. What helps instead:
  - prefix filters (`git branch --list 'unity/*'`, `gh pr list --base unity/main`)
  - an optional fetch refspec limited to `unity/*`
  - worktrees for parallel agents
  - an optional, pilot-approved ruleset protecting `unity/main`
- **The Godot branches and `main` are never renamed or deleted.** `main` gets the tag `godot-final`.

## D-026 The Unity feed is a faithful port of the Godot feed · Accepted
The pilot, 2026-10-17: "the current noisiness algorithms should be very much similar to those lie under the godot codebase on git".
- **What is ported:** `video_feed.gdshader`, `FeedEvents`, `FeedLoss`, `FeedOsd` and `FeedFont` (copied in [legacy/video-feed-godot/](legacy/video-feed-godot/)). The algorithms, constants, hash, event rates, loss sequences and OSD geometry are kept.
- **This supersedes three readings in the requirements, in the pilot's favour:**
  - Recovering dropouts stay on the pilot's **3–6/min** timer, and quality drops on about 10/min. They are no longer "event-driven only, with a 0.2/min floor".
  - N3 and N4 are **always on**, not per-flight draws.
  - Physics-driven link events are **added** on top in phase 7, never replacing the pilot's baseline.
- **Unchanged:** terminal losses stay event-driven, exactly as in Godot.
- **New traits layer on top** without altering the ported output while off: lens distortion O1 (scene only, never the OSD), AE/AWB, N13, rain drops on the lens.
- **Acceptance** is an A/B against the Godot original: an identical event schedule per seed, and visually matching stills per seed and field.

## D-027 Switching between the Godot and Unity codebases · Accepted
The pilot, 2026-10-17: "we're creating something separate INSIDE the current gh repo. it should be easy to switch between GODOT codebase and UNITY codebase on github".
- **On GitHub:** the branch dropdown (`main` for Godot, `unity/main` for Unity), two bookmarkable `tree/` links, and per-codebase PR filters (`base:main`, `base:unity/main`).
- **Cross-links on both front pages:**
  - the Unity README links to Godot
  - one banner commit on Godot's `main` README links to Unity, the only post-freeze commit on `main`, made in phase 0 with the pilot's OK
- **The default branch** becomes `unity/main` (with the pilot's OK). Godot stays reachable through the dropdown and the tag `godot-final`.
- **Locally:** two folders, one per codebase (`C:\.dev\450fpv` for Unity, the old checkout for Godot), never switching one folder between the unrelated trees. Godot files are read from the `godot-final` tag when needed.
- **Gate builds** are tagged `unity/g<N>-<name>` (e.g. `unity/g2-gallery`), so every reviewed build can be reopened from the tags page.
- **Details:** [repository.md](repository.md).

## D-028 Realism means a copy of the references, with proper geometry · Accepted
The pilot, 2026-10-17: "when i say realism i mean more than just abstract realism. i also mean that the game should be a copy of how it looks on reference (both my flight replay references and those images), it's important. it's also important that we do proper geometry if you remember - not only for looks but also for physics".
- **"Realistic" means "looks like the pilot's references":**
  - the flight replays through the feed
  - the photos in noclip
  - not abstract or generic realism
- **Every reference letter gets a recreated view.** It is compared side by side with the reference in local-only sheets (REAL-02, REAL-03). That comparison, not taste, decides whether an asset, a scene or the feed is done.
- **Copy the look, not the location:** scene types, materials, damage and density, never an identifiable place (OPSEC, REAL-04).
- **Proper geometry is required, for the looks and for the physics** (REAL-05, PR-5):
  - real shapes with real thickness for anything the drone can see up close, touch or fly through
  - no texture, normal-map, alpha-card or decal fakes for anything with a physical role
  - collision that follows the visual
  - every visible opening known to the sim as its real clear space
  - ground features as geometry that renderer and physics share
- **What it extends:** D-020. D-020's budget and judging points still hold, under this definition.

## D-029 One subagent per logical task · Accepted
The pilot, 2026-10-17: "also, specify that it's preferred to have each subagent for each logical task".
- **What a logical task is:** one reviewable unit of work, usually one OpenSpec task or a tight group of them. Examples:
  - one object sheet group
  - one asset (generator, params, metadata, review sheet)
  - one shader stage
  - one world query
  - one physics mechanism
  - one UI screen
  - one QA review
- **Each logical task gets its own fresh subagent** of the owning role, with a short brief: the change id, the task ids, the files to read, the checks to run.
  - The agent does that task, commits, and ends with the handoff block.
  - **The next task gets a new agent.** Roles are agent *types*, not long-lived workers.
- **Why:**
  - Small contexts stay sharp and cheap.
  - A usage limit or a stop loses at most one task.
  - Every task leaves a visible handoff for the pilot.
  - Nothing depends on an agent remembering an earlier task: it is all in the specs, sheets and commits.
- **Branches:** tasks of one change run **sequentially** on the change branch, `unity/<prefix>/<change-id>`, each agent picking up where the last one's commits left off.
  - Parallel work (at most 2 agents) is either two different changes, or two tasks of one change on task sub-branches `unity/<prefix>/<change-id>--<task>`, merged into the change branch.
  - Never two agents on one branch at once.
  - Each agent works in its own git worktree.
- **Resuming:** an agent is resumed only to finish *its own* task after a usage limit stopped it. It is never resumed or reused for a new task.

## D-030 The team's tooling carries over: OpenSpec, karpathy-guidelines, agents · Accepted
The pilot, 2026-10-17: "specify that we're using openspec and other things we added here (remember i mean those prompt optimization thingies)".
- **OpenSpec** (spec-driven, CLI 1.13.1+), with a project config already written for Unity ([../openspec/config.yaml](../openspec/config.yaml)). Its six skills and the `/opsx:*` commands are copied into `.claude/skills/` and `.claude/commands/opsx/`.
- **karpathy-guidelines,** the prompt and behaviour skill the pilot chose over ponytail and caveman. It is loaded by the orchestrator and every subagent. The project's amendment: simplicity applies to code, never to physical fidelity.
- **The orchestrator rules** (`CLAUDE.md`), **the agent rules** (`AGENTS.md`), **seven role subagents** (`.claude/agents/`), one subagent per logical task, the handoff block and the token discipline.
- **Details:** [tooling.md](tooling.md).

## D-031 D-001 checked at phase 0: Unity 6000.3.26f1, URP · Accepted
Checked on 2026-10-17, as D-001 asked.
- **The version:** **6000.3.26f1** (Unity 6.3 LTS, changeset `de8167e7440c`) is the newest Unity 6 LTS on Unity's release API that day. The 6000.0 line is older. It is pinned in `unity/ProjectSettings/ProjectVersion.txt` and [environment.md](environment.md).
- **URP still holds:** Unity's render-pipeline strategy for 2026 prioritises URP and moves HDRP to maintenance (no new features). That strengthens D-001's third reason. The HDRP fallback stays formally open until the G1 look-lock, but is now unlikely.
- **Later LTS releases** (6.7 LTS is announced) are evaluated at a phase boundary, never adopted mid-phase. An upgrade gets its own decision entry, and every golden and review sheet is re-checked.
- **Player backend:** Mono for now. IL2CPP needs the C++ toolchain and is considered only if profiling shows the need (PLT-02, PLT-04).

## D-032 Measured sizes beat AST-04's assumed ones; reference views are composites · Accepted
Settled while reviewing the phase-1 object sheets (2026-10-18).
- **Wall and door sizes.** AST-04's exact door (0.8 × 1.8–1.9 m) and storey (2.8 m) figures were assumed for village houses; the sheets measured the adobe walls and the entrance door in E and derived the rest, and the measured, published or derived figure wins over the assumed one, per class. AST-04 now lists them. **Wall height to the plate, from grade outside:** the small clay hut H05 2.0–2.3 m; adobe houses H01–H02 2.3–2.6 m (E); single-storey brick houses H03–H04 2.6–3.1 m including their plinth and cornice (derived: a 2.8 m storey plus plinth or cornice; J shows H03's ridge, not its wall); two-storey houses 2.8 m a storey; the Stalin-era block 3.0–3.4 m a storey. **Doors:** the entrance door of the village houses H01–H04 is exact at 0.8 × 1.8–1.9 m, and the adobe houses' windows at 0.8 × 1.0 m; every other door, window and doorway (interior doors, H04's veranda doorway, the hut, outbuildings, cellars, shops, the block's section entrances and balcony doors) takes the size its sheet gives, labelled published where the row cites the standard it comes from, else measured or assumed (the 0.70 m cellar door is the pilot's, PR-9). Wording settled at the QA re-review of the sheets (R7).
- **Reference views (REAL-02) against OPSEC (REAL-04).** A recreated view reproduces the *look* of a letter's scene: the objects in frame, their classes, states, rough distances and light. The world places them as a composite among invented surroundings; it never reproduces a real site's layout beyond what the frame shows, and no view is tied to a place. The I/K urban-edge views and the J street are allowed on this reading.
- **Research sources.** A sheet cites public, non-identifying sources only: construction manuals, standards, encyclopedias, manufacturer pages. News reports, surveys or photo sets about a named place, oblast, front or site are not allowed as sources, even for a generic fact (SEC-02); the fact is cited from a generic source or labelled assumed.


## D-033 Plans are cut for parallel Opus 5.5 subagents; process scales with the task · Accepted
The pilot, 2026-10-18: "Consider plan implementation in a way that it can be processed by multiple opus 5.5 sub agents. This way we cut off the unnecessary bureaucracy and complex workflows where we don't need that at all (for example, for base tasks, or simple tasks). For those more complex - we use a more complex approach. In this regard, you should in general follow the existing tech approach that was specificed in .mds and docs prior starting this project".
- **Plans are cut into units for parallel subagents.** Every plan, of any size, is a set of self-contained units, each one subagent's work: its files, its check, and what it waits on. Units that share no files and wait on nothing run at the same time, each on its own branch and worktree (D-029); the plan says which run together. The parallel cap of PRC-09 applies (raised to 4 by D-036).
- **Every subagent runs on Opus 5.5** (`claude-opus-5-5`), whatever the orchestrator's own model. This replaces the narrower practice of spawning only war-terrain briefs on Opus.
- **Three tiers of process.** The orchestrator states the tier when it plans the work; when unsure, Standard.

  | Tier | What it is | Process |
  |---|---|---|
  | **Quick** | wording, labels or a stale line in a doc, spec or design; a config value; a test threshold; a one-file bug fix with its test; bookkeeping (archive, handover, report) | no OpenSpec change; one subagent with its narrowest check, or the orchestrator for the docs it owns; the orchestrator reads the diff, opens and merges the PR; QA only if behaviour the sim or the pilot sees changes, as a short check |
  | **Standard** | a contained feature or fix in one area: a tool, an importer or pipeline feature, a re-measured sheet row, a mass-production asset on an established generator and contract | an OpenSpec change with proposal and tasks (a spec delta only when a requirement changes, a design only when a choice needs recording); one subagent per unit; one QA pass, any re-review limited to what it asked for |
  | **Complex** | a new generator and its first asset, the look-lock assets and every gate deliverable, a physics mechanism, the feed, a cross-role contract (the metadata schema, the world query) | the full loop of [workflow.md](workflow.md): proposal, specs, design, tasks, the owner readings [team.md](team.md) lists, QA and re-review, the pilot at gates |
- **Owner readings** (the physics engineer on catalog metadata, and the rest of team.md's "who reviews what") are kept, and run when a change brings a new kind of contract: a new metadata key, contact rule, material or shape convention. An asset that reuses an established contract does not get its own reading.
- **The tech approach does not change:** CLAUDE.md, VISION.md, the requirements and the earlier decisions still hold (the engine-free deterministic sim, OPSEC, free assets, the review sheet and the side-by-side against the letters, commits, PRs, one fresh subagent per unit). Only how much process a piece of work carries scales with it.
- **Amends** PRC-02 (Quick work carries no OpenSpec change) and the opening rule of [workflow.md](workflow.md).

## D-034 Fresh-context review subagents; subagents only where expertise says so · Accepted
The pilot, 2026-10-18: "we need subagents to: review code; check the plans and accordance to them and find gaps; with a fresh context, as if some 3rd party is reviewing it with a fresh sight. Each session is the main agent (orchestrator), and you orchestrate and create subagents only for what is needed as per your expertise."
- **Two review kinds, always by a fresh agent that did not do the work:** a **code review** (correctness, simplicity, the engineering rules of CLAUDE.md, tests that fail the known-wrong model) and a **plan-conformance review** (the work against its proposal, design and tasks, and the plan against the requirements, naming the gaps). QA's spec-scenario review stays; these two are added where the tier (D-033) calls for them: Complex changes get both before merge, Standard changes a code review, Quick changes none unless behaviour changes.
- **A periodic gap review of the plans themselves** (requirements, plan, briefs, decisions against VISION.md, the pilot's words and the Godot attempt's OpenSpec documents) runs by a fresh agent at every phase boundary and whenever the pilot adds scope. The first one is the Godot OpenSpec gap review of D-035.
- **The orchestrator decides which subagents a task needs** from its expertise, within D-029 (one fresh subagent per logical task) and D-033 (tiers), rather than running every role on every change.

## D-035 Scope catch-up from the pilot: the world's settlements, fortifications, water, and the reviews · Accepted
The pilot, 2026-10-18, restating the project's demanding work and adding scope. Recorded so the next sessions build to it.
- **The demanding tasks, in the pilot's order:** (1) decrypting the references so the feed feels like the real analog videos (the colour that changes as the grass comes closer, the noise, glitches and flashes, the night view): REAL-01…05, FEED, VIS; (2) models copied from the references and research (Ukrainian villages and small towns), never mimicked: AST, the object sheets; (3) the gallery of every object, explorable in noclip: GAL; (4) the world after the gallery is confirmed: WLD; (5) realistic weather (rain, sun, night) copied from the references: VIS-02/03, D-021; (6) ultra-realistic physics (prop wash, turbulence, bounded randomness, gusts, wobble, and whatever a physicist adds): PHY. The easier ones: the noise from the pilot's Godot feed (D-026, feedback welcome) and controller binding and calibration (INP). All of them stay as the requirements already state them; this entry adds what was missing.
- **The world's settlements (WLD-01 refined):** a typical village, a smaller village nearby and a somewhat larger town, divided by tree belts and fields in the pattern of the pilot's satellite layout reference (stored locally as **letter Q**, git-ignored; a pattern reference, never a place: SEC-02/03). The Stalin-era block, the farm complex and the larger public buildings sit in the town.
- **Frontline today:** most houses damaged or destroyed; trenches and firing positions in the fields; **fortifications** as real objects: concertina wire (єгоза) runs, dragon's-teeth rows, a couple of anti-tank hedgehogs, a checkpoint kit (asset-list §12, F01–F04, P1 for the world except F04), each with an object sheet before it is built.
- **Water:** a small river with tree-lined banks and a pond or two, as letter Q shows. The references so far show no water (terrain.md); this is the one addition the pilot may veto at G1 ([pilot-questions.md](pilot-questions.md)).
- **Reviews the pilot asked for:** a fresh-context gap review of the Godot attempt's OpenSpec documents (`git show godot-final:openspec/...`, 50 files: map format, map loading, world query, reference frames and notes, reference wind, noclip explorer, review sandbox, asset pipeline, terrain holes, the UAT-1 gallery) against the Unity requirements and plan, adding whatever is missing; and D-034's reviews from now on.
- **Conflicts to raise with the pilot:** none open that research or calculation cannot settle, apart from the water question above. The parallel-subagent cap (PRC-09: 2) was offered for raising twice and not answered; it stays at 2 (superseded the same day by D-036: 4).

## D-036 Water in the world; four parallel subagents · Accepted
The pilot, 2026-10-18: "water - i mean you can create a pound or a small river. the requirment here would obviously be - if you lay opto on water signal gets cut (also part of realism). graphics - mildly realistic. effect of win - yeah, there should be waves. parallel agents - raise to 4."
- **Water stays in the world** (WLD-01, new WLD-15): a pond or a small river where the layout reference (letter Q) puts them. The fiber laid on water cuts the signal (superseded by D-039: it does not); the look is mildly realistic; wind raises waves. The physics treats the surface as its own row (`water`): a touch is a crash, nothing lands on it. Phase 3 builds it with the layout; the wave look is phase 4 and the look setup (the feed's cut and its phase-7 rule were dropped by D-039).
- **PRC-09's cap is 4 parallel subagents** (was 2). CLAUDE.md and D-033 follow. The handoff discipline is unchanged: one fresh agent per unit, each in its own worktree, two writers never on the same files.

## D-037 The Godot OpenSpec gap review, folded in · Accepted
The fresh-context review of D-034 and D-035 ([reviews/godot-openspec-gaps.md](reviews/godot-openspec-gaps.md), by QA, who did none of the work) read the first attempt's 50 OpenSpec files: 62 requirements and 108 scenarios, of which 7 requirements and 16 scenarios were missing from the Unity docs and 19 and 23 only partly carried. It found 27 gaps (G1–G27) and 13 lessons (L1–L13). Folded in as follows; the review file stays as the record of where each came from.
- **P1, in the requirements now:** the FPV camera's intrinsics pinned (FEED-02, L1); the FEED-00 A/B on one identical input frame, with the Godot build kept until G4 (L2); the measured colour-with-distance shifts as scripted approaches and a frame-metrics tool run alike on footage and Unity output (FEED-12, G13, G14, L3); colour continuity from near grass to far ground (AST-10, L4); the hole-filler contract for every asset that cuts the ground (AST-05, G5); the noclip controls (PRD-07, G16, G17); the gallery's relief, patch sizes and station list (GAL-01, G21); surfaces distinct at 60 m, furrows that never alias, every surface row signed off before use and a `water` row (AST-11, G22, G26).
- **P2 and P3, given their home:** terrain holes as a new **WLD-16** (G1–G3, G6) and their physics in PHY-04, PHY-08, PHY-13, PHY-19 and LOG-01 (G7); world-query items in WLD-11 (G4, G10, G27, L9) and validation in WLD-12 (G2, G8, G9, G11); the golden in the Unity runtimes (LOG-04, TST-05, G12); the wind-statistics tools ported to plain Python (PHY-14, G15); the overlay, labels and feed toggle (GAL-02, G18, G23); the scripted start (GAL-03, G19); the smoke run's zero-error rule (TST-01, G20); the georeference scan's list (SEC-03, G24); the per-class budget table that fails the build (AST-08, G25); the class-level wording rule and a local hand-term list (SEC-02, SEC-06, L6); **TST-06** (the tests are tested, L5); the contact lessons (PHB-05, PHY-08, L7); the deferred X-items (PHY-04, PHY-05, PHY-13, AST-06, L8); the legacy flight-model scenarios (PHB-01, PHY-18, PHY-19, PHY-24, LOG-02, L10); the inflow derate and the T9b watch item (PHB-03, PHY-14, L11); the receiver's no-signal style, asked at G4 (FEED-07, L12); MODIFIED deltas when a change extends another capability (`workflow.md`, L13).
- **From the review's notes:** INP-02 detects expo or dual rates left on the radio; INP-03 names airmode and motor idle from the pilot's profile.
- **Open changes touched:** `p1-e01-trench` checks F-1 and F-2 as well. The C01 cellar's `holes` record and filler contract go into its sheet as a Quick unit.
- **Phase 3's map-format and world-query specs** take G1–G4, G6 and G8–G11 in full when they are written (plan phase 3 step 1).

## D-038 The typical village copies letter S; references are read for their look and feel · Accepted
The pilot, 2026-10-19, adding a clip of a village street (letter S, local only): "let's say one village looks something like this (but add cars somewhere)" and "when you do - don't forget to thoroughly review references to copy their looks and feels". On a second thought about more Stalin-era blocks in the other settlement: "nvm let it be one stalinka. i dont want to make the work even longer much".
- **The typical village copies letter S** (WLD-01): its street, houses, yards, fences, verges, poles and wires, road and vegetation, read from S's stills: its look, never its sequence or layout (REAL-04); **cars** on the street and in yards (the vehicles sheet, R01). The smaller village is of the same kind. A pattern to copy, never a place (REAL-04).
- **The Stalin-era block stays one** (U01 "exactly one", D-018, D-035): the pilot weighed more of them for training through holes and basements and kept the scope as it is.
- **Every reference is read for its look and feel** (REAL-01–REAL-03), the pilot's words: not only which objects it shows but its composition, light, colour, wear and the density of its clutter and vegetation, so the recreated view feels like the reference, not only contains its parts. Letter S is read into the reference notes and the object sheets by `p1-letter-s-village-street` (Standard).
- **Letter R is skipped:** the object sheets' "Seen in: **R**" means research only, so a letter R would read as that.

## D-039 Fiber on water keeps the signal · Accepted
The pilot, 2026-10-19: "that req about opto being cut if laid on water - disregard. it's simply false, water doesn't hinder opto from transferring signal."
- **Supersedes D-036's signal rule.** Fiber lying on or in water carries the feed as anywhere else: water never cuts the signal and never ends a sortie. WLD-01 and WLD-15 drop the cut; the feed has no water event (phase 4); the physics has no cut rule to set (phase 7).
- **What stays from D-036:** the river and the ponds, their mildly realistic look, the waves from the wind, and the `water` surface row (a touch is a crash, nothing lands on it).
- **The line on the water stays a physical line:** where it lies on or in the water, and the drag a current puts on it, are the tether physics' to model with the rest of the line (phase 7d); none of it touches the feed.

## D-040 Gallery first: G1 on the original timeline · Accepted
The pilot, 2026-10-21, after three days with three of G1's six stations merged: "we were supposed to have a gallery in 2-3 days", then, to the orchestrator's gallery-first proposal: "sure we should be on track with the original plan. im waiting for gallery".
- **The gallery is built now and fills its stations as each lands.** G1's look setup starts at once; the scene takes every merged station and a numbered empty marker for each one still on its branch; the build goes to the pilot as soon as R01 and H01's intact house are in, and the damaged house follows.
- **Before G1, each station keeps to one fresh QA pass (D-034).** A look miss QA finds becomes a named stand-in read out to the pilot at G1, not a rework round. A QA item on physics, tests, metadata or spec text still blocks the merge.
- **Moved to after the pilot's G1 look, which steers them:** V01's crown net and the H belt edge (QA 2.1's R1 and R2 in `p1-v01-locust`'s `review.md`), H01's X state and letter S's items (task 1.4b), and any polishing of a number already inside its spec limit.
- **Unchanged:** the physics rules (real geometry with thickness, collision following the visual, every audit), D-028's definition of realism, and the side-by-side reading of every station against its letters.
- **Mass production:** a variant that is only a params file on an existing generator takes the Quick or Standard tier (D-033): one build, one look, one QA pass.

## D-041 Efficiency: look first, shared pieces first, lean notes · Accepted
The pilot, 2026-10-21: "the fact that it's slow - separate topic, dont care. what i care about is efficiency. if you tell me that all the work behind the scenes to create world and objects preparations will help achieve tremendous good quality down the line - works". Spend goes to what compounds (generators, the metadata contract, audits, shared shaders, physics rulings), not to what does not.
- **Look first inside every asset task.** A rough build, then the side-by-side against its letters (REAL-03, D-038), then the numbers. A wrong shape is found before parity, budgets or tuning are spent on it (E01's and V01's looks failed QA after every number passed).
- **Shared pieces are planned up front.** A proposal names every library, shader, importer role or metadata key it shares with another change in flight, and the change that builds it first; nothing waits mid-change on a piece nobody owns (R01's layered paint and H01's `textures` key were found mid-change).
- **Lean notes and briefs.** An As built note gives the numbers and the decisions, not the story of the tries; a brief names the exact sections to read. Every fresh agent re-reads the change, so its length is paid on every task.
- With D-040: no polishing of a number already inside its limit, and a variant that is only a params file takes the Quick or Standard tier (D-033).

## D-042 First flights before the world: G4, G5 and G7 next; G2 and G3 postponed · Accepted
The pilot, 2026-10-21, after flying the G1 preview (five of six stations): "VAZ - not bad, trench - very good, actually, grass - solid, tree - it's a hq stuff btw im not gonna lie. what's still missing - поля, посадки, houses and many other things. I was thinking of postponing G2 and G3 now, and creating flying algorithms based on the current raw version." Their proposition: "Do G4 - create feed; G5 - create the drone already; G7 - first flights, this would include sub tasks: a) flying controller support b) basic FPV controlling logic (which means it should support yaw pitch roll throttle etc.) c) important there also - physics. Wind, weather, complex flying logic, etc." Asked, they chose to fly free first, with the fiber after the first flights, and a minimal menu judged at G7 instead of a wireframe gate.
- **Next, in this order of gates:**
  - G4: the feed (phase 4)
  - G5: the drone (phase 5)
  - G7: first flights (phase 6, with the core of phase 7's flight physics: wind and turbulence, then weather and the airframe effects)
  - The lanes run in parallel; the gates reach the pilot in that order.
- **The test range is the G1 gallery.** The world queries run over its stations, so the first flights meet the trees, wires, car, trench and house.
- **G6 folds into G7.** Train / Calibrate / Exit and the setup screen are built plainly and judged at G7.
- **The fiber (phase 7d)** follows the first flights, as the next flight build.
- **Postponed:** mass production, G2's gallery of every asset, and G3's world (fields, belts, villages). Phase 1's deferrals wait with them.
- **G1:**
  - The pilot's words are in `docs/style-rules.md`; D-001 closes with URP kept.
  - V01's crown-net rework (`p1-v01-locust` QA 2.1's R1) is dropped: the pilot rated the tree high quality, and the pilot's words beat QA's reading. It returns only if the pilot asks.
  - The H belt edge (R2) waits with G3.
  - H01 finishes (its look pass, its merge, its QA), joins the gallery, and closes G1.

## D-043 The drone copies letter T; legs-off flights launch from the upside-down chair · Accepted
The pilot, 2026-10-21, sharing a photo of a typical heavy fiber-optic quad, filed as letter T: "this is what our drone, a typical optical fiber, should look like. as you can see, it has legs, but for some missions there are no legs, this is exactly when you lift off from that "upside-down" chair - so it stays on something, coz you can't balance on the spool".
- **The Svinoriz 10 Opto copies letter T's look** (D-028):
  - an X frame of carbon plates and tube arms, with four motors
  - the fiber spool in its housing on top
  - the payload slung below, between the legs
  - the two packs strapped along the frame's sides
  - four carbon tube legs with feet
  - The object sheet (`p5-drone` task 1.1) reads every detail T shows.
- **Copy the look, never the markings:** no logo, text or QR code on any part (SEC rules). The payload stays a generic body of T's size and the class's mass, with no functional detail (DRN-01).
- **What it supersedes:** the 2026-10-17 note "the payload sits on top" and DRN-01's "the fiber coil underneath", for the legs-on build. `p5-drone` re-derives K§5.2 on T's layout.
- **Legs-off:** the drone rests by its arms on the upside-down chair, the four-post stand of D-022, because it cannot stand on the spool. The orchestrator reads this, with D-022's "the coil hangs clear", as the spool hanging underneath on the legs-off build. That reading is confirmed at G5.
- **Corrected by T's reading** (`p5-drone` task 1.1, `docs/object-sheets/drone.md`): the arms are flat carbon plates, and the tubes are the legs, standing on fore-aft skids. The props are 2-blade (PRD-02 and DRN-01 said 3-blade). The payload hangs crosswise below the frame. The legs keep the pilot's 25–35 cm, although T's measure about 0.18 m, since the pilot's number beats a photo's scale. The coil keeps the pilot's 1 kg (about 10 km), on T's drum shape scaled up.


## D-044 The test range: a simple map from what exists, for G4 and G7 · Accepted
The pilot, 2026-10-22: "there's a house on Godot - for now you can use that house and posadka's from there to create in unity. just pick whatever we have at the moment that we can use, create a simple ahh map, so i test the flights and complex physics on something tangible, and not a couple of trees standing alone in a sandbox world. make it simple, don't use too much resources for this." Their list: the grass from Unity; the house and the yard from Godot; the posadka from Godot, with Unity's trees and bushes too; the cars from Unity; "and whatever else".
- **Supersedes D-042's "the test range is the G1 gallery".** G4's noclip through the feed and G7's first flights run over the test range (`p6-test-range`). The gallery stays the look-lock reference, and G5's drone station stays in it.
- **The map:** about 512 m square on flat ground. It holds the Godot yard (house, shed, cellar, gate, fence, fruit trees) at its Godot layout; a posadka of the Godot belt trees and shrub mixed with V01 and V13; S04 grass; R01's two cars; an L01/L03 power line; E01's trench; H01's two houses as neighbours once H01 merges; and start points from the ground and from the stand.
- **The Godot assets are stand-ins `Z01`–`Z13`** (D-040), ported through the Unity pipeline from `refs/godot/main` with their Godot physics metadata (collision, gaps, wind volumes, porosity, snag), never merged (D-027). They get no look pass and no rework, and each is swapped for its Unity asset when that lands.
- **Kept cheap:** Standard tier (D-033), three build tasks and one QA pass; no relief, terrain layers or roads (G3's).

## D-045 The builder's filter chain stays; the step-response check follows it · Accepted
`p6-flight-core` 1.4b put the builder's full Betaflight 4.5 gyro and D-term filter chain into the controller (`fc-profiles.md` §1). Its phase lag raises the builder PIDs' 200 °/s roll step overshoot from about 16 % to about 41 %, against the propulsion spec's "less than 25 %", which was written for a loop without filters.
- **The chain stays,** faithful to the pilot's flight controller (D-023). Simplicity never trims physical fidelity.
- **The scenario is re-derived** for the real chain: stable, well inside the 5.5 °/s peak-to-peak settling, with the overshoot held as a regression band rather than the 25 %.
- **The plant's lag is re-checked** in task 1.7 against published data: the motor's time constant (43 ms at hover, derived) and the rotor inertia (±30 %). A slower plant than the real one would explain the overshoot.
- **The pilot's feel decides** at G7 (a qualitative question in `docs/pilot-questions.md`).
- **The log carries the filters' outputs, not their delay lines:** about 700 doubles of filter state stay in the controller and are rebuilt by re-running the flight from its start, so replay stays bit for bit. This departs from K§6.10's "full state at 1 kHz"; a keyframe block comes only if forensics needs a mid-flight restart.

## D-046 The gallery gate closes as seen; the map waits; physics and the radio first · Accepted
The pilot, 2026-10-29: "gallery - not needed anymore, coz ive seen some objects already. map - let's do later on, for this night let's only those tasks that dont require GPU. physics, radiomaster, there's plenty to do and test for you. WILL work on the world map the other time".
- **G1's gate is closed as seen.** `p1-g1-look-lock-gallery`'s remaining tasks (1.3 H01's station, the QA pass for the gate, the pilot's look) are dropped. Its code (`LookSetup`, the noclip camera, the gallery scene and builder) is still needed by the test range and the feed, so it lands with them after one QA pass when the map work resumes.
- **The map waits:** `p6-test-range` (its QA) and `p6-world-query-gallery` 1.3–1.5 pause until the pilot turns to the world map. The test range builds stay the pilot's preview (`TestRange-preview-3`).
- **Now:** work that needs no GPU: the flight core (1.7, D-045's plant check), the engine-free tasks of `p7c-wind` and `p7b-airframe`, and the RadioMaster input (`p6-input-app`). Unity runs only `-nographics` EditMode tests.
- **H01:** QA's B1 fix lands as code and tests; its rebuild and the QA re-check wait for the map work.

## D-047 The test range is the final map; finish the game on it · Accepted
The pilot, 2026-10-31: "let's assume the current demo map is the final map. let's finish the game."
- **The test range (D-044) is the release's one map.** G7, the MVPs after it and G8 fly it. VISION stays as written; the pilot's words here decide the release's map.
- **Dropped:** phase 2 (the gallery, G2), phase 3 (the world, G3, UAT-2) and mass production; `p6-world-query-gallery` 1.3 (the gallery's station data); `p5-drone` 1.6 (the gallery station). G1's code still lands with the test range.
- **G4 and G5 fold into G7:** one flying build where the pilot judges the feed, the OSD and the drone as they fly, so the pilot reviews one build, not three.
- **The map's finishing:** H01's rebuild and QA re-check, the test range's QA, the world queries over it (`p6-world-query-gallery` 1.4, 1.5) and the contacts on it (`p6-flight-core` 1.6b).
- **The order:** the map stack lands on `unity/main` (G1's code, the test range, H01), then the feed; the world queries and contacts; the airframe and the wind; then the app's screens and the built player (`p6-input-app` 1.4b) make the **G7 build**. After G7, one MVP per physics change: 7d (the fiber), 7a (the ground), 7e (forensics, the feed's physics signals), with the feed's remaining tasks, then G8.
- **The GPU is back:** D-046's GPU-free rule was for its night.

## D-048 The inertia character and the low-voltage signal follow the computed build · Accepted
From `p7b-airframe` 1.5, computed, never asked (D-024):
- **PHY-12:** "pitch exceeds roll" was derived in K§5.3 before D-043 copied letter T's layout (the packs along the sides, the payload crosswise). On that layout roll exceeds pitch on six of seven loadouts (pitch/roll 0.855–1.048, yaw/roll 0.91–1.41). The reference decides (D-028): PHY-12 asks for the ratios computed from `drone.json`'s layout, asserted exactly, with yaw saturating first.
- **PHY-11:** the low-voltage signal is the pilot's FC profile's 3.00 V a cell on the loaded voltage. At the heavy corner under full throttle it sets at about 13 % charge, not 20 %. The scenario follows the physics: the margin falls with the sag (19.6 % from full to 20 %), the signal sets at the knee, and no margin is left at the cut-off.
- **The OSD** applies Betaflight's voltage filter and warning hysteresis to the signal (`p4-feed` 1.5).

## D-049 The throttle-punch shake follows the computed airframe · Accepted
From `p7b-airframe` 1.4, computed, never asked (D-024):
- **The visible shake comes from the rigid body:** through a sharp arrest of a descent the four ESCs sit at their 40 A limit while the rotors re-ingest their wash, so the drone jitters and tilts 5–16° uncommanded. The frame's modes (arms at 48–63 Hz, computed from the plate, on the builder's notches) add the 50–200 Hz band, far below one luma sample at the camera.
- **PHY-10's scenario follows the physics:** a gentle throttle increase never shows it from hover; in a 5–9 m/s descent a gentle ramp shows PHY-09's wobble instead; the shake lasts as long as the arrest (0.3–0.7 s for the empty drone's moderate descents, 0.8–1.4 s heavy or from 9 m/s).
- **The pilot's feel decides at G7** (a qualitative question in `docs/pilot-questions.md`).
- **For the feed:** `CameraVibration`'s 50–200 Hz band is for shimmer; the visible shake comes from the pose and `CameraAngularVelocity`.

## D-050 A simpler drone: the coil beneath, a bottle on top · Accepted
The pilot, 2026-11-01: "let's make the model simpler - it's just a drone with a coil beneath, on top we have only a bottle (because our bots are safe and are only for pilot training purposes".
- **Supersedes D-043's layout** (the spool on top, the payload slung below). The Svinoriz 10 Opto is now:
  - the X frame, its arms, four motors and the props, as before
  - **the fiber coil beneath** the frame, as VISION §3 describes; legs-off flights still lift off the upside-down chair, so the coil never touches the ground
  - **a plain plastic bottle strapped on top**, the only load. The setup's additional weight is the bottle's filled mass. It is strapped rigidly, so it doesn't swing like a sling.
  - the two packs and the legs (TRUE/FALSE) stay
- **Simpler model:** no other loads or fittings; the look of the frame, arms and legs still follows letter T.
- **The mass build follows it** (`drone.json`): the inertia ratios are re-computed from the new layout (D-048 still holds: computed, asserted exactly), and the goldens are re-recorded and audited.

## D-051 The throttle-punch shake's cause and bands, corrected · Accepted
QA's review of `p7b-airframe` 1.4 corrects D-049:
- **The cause is the assumed 40 A ESC current limit,** not the rotors re-ingesting their wash: without the limit the full-punch tilt falls from 11–16° to 1.5–3.4° and the arrest is about twice as fast. The four ESCs are limited together for only part of the arrest.
- **The firmware decides it:** with Bluejay, which senses no current, the modelled shake mostly vanishes. G7's ESC firmware question settles the tilt question.
- **The bands kept:** the shake lasts the arrest (60 of 60); the empty drone's 0.3–0.7 s (40 of 40); the 6–15° tilt applies to a full punch only. The heavy or fast band is re-measured with a window that sees its end, on enough seeds, before it is stated.

## D-052 The quality preset is the build's, with a command-line override · Accepted
From QA's review of `p6-input-app` 1.4b (finding 17): PRD-04 and PRD-05 keep the menu and the setup to their few choices, so the quality preset gets no screen.
- **At G7 the player runs the build's preset (High).**
- **A `-quality <name>` option** on the command line picks another preset and is saved to the profile for the next start.
- **APP-05 stands as written:** the preset chosen on the command line is the one saved to the profile.
- **The pilot is asked only if their frame rate needs it** (a preference, at G7).

## D-053 The throttle-punch shake's numbers, from 40 seeds a case · Accepted
`p7b-airframe` 1.8 measured the shake on D-050's final layout (40 seeds a case, a 3 s window, `flightbench --shake`). Its numbers supersede D-049's ("0.8–1.4 s heavy", "5–16°") and D-051's (the tilt figures, "40 of 40"). D-051's cause stands: the assumed 40 A ESC current limit.
- **The shake lasts the arrest:** an ESC holds its limit through every arrest, and the shake ends at most about 0.3 s after it. The medians are 0.44 / 0.64 / 0.82 s empty and 0.66 / 0.96 / 1.32 s at 2 kg, from about 2.5 / 5.3 / 9 m/s.
- **The empty drone's full punch** lasts 0.3–0.7 s on about 96 % of flights, matching the pilot's account.
- **The tilt** on a full punch is 2.7–10° and at least about twice the no-current-limit model's (0.1–3.2°), so the ESC firmware question decides it.
- **The bands with no physical floor are dropped.** The spec states rates where a bound is not true on every seed (QA's re-review, B1).

## D-054 How the world queries' budget is judged until the fiber · Accepted
From QA's review of `p6-world-query-gallery` (B1, S2):
- **At G7 the queries are judged on physics' pattern,** the calls `ContactModel` makes each step, inside PLT-04's whole-step budget (p99 at most 300 µs, 0 B). The 100 000-capsule and 100 000-ray budgets are read the same way.
- **K§10.5's batch with the fiber's segments** is judged when the fiber is built (phase 7d), which decides how often the fiber is queried.
- **PLT-04's 60 µs line stays the target** for the queries' speed work. The straw patch (194 µs at p99 a step) and the launch stand get a broad phase inside each object.

## D-055 V switches between the first- and third-person view · Accepted
The pilot, 2026-11-02: "let's add V - it shifts between 1st person and 3rd person view. it doesn't change anything else, only the camera view".
- **V toggles the camera** between the FPV camera at the lens and the third-person chase view, in every build, the pilot's included.
- **Nothing else changes:** the feed, its noise and the OSD apply in both views; the physics, the log and the input don't see the toggle.
- **The keys move:** the review builds' feed toggle leaves V for a free key; C (the old chase key) is retired. The flight's key list on screen and in the spec follows.

## D-056 Physics next: wind, ground, damage and the fiber · Accepted
The pilot, 2026-11-02, after flying the map build: "i'm very much pleased with the results. let's enhance the physics further, should be mega realistic (turbulence, wind, stucking, wobbling, motors worsening when you scratch the motors and/or propellers on a wall etc.) and opto cutting physics too."
- **Wind and turbulence** (`p7c-wind`): it lands in the player as soon as its merge with the airframe and its re-grade pass QA.
- **The ground** (`p7a-ground`, PHY-01…06): legs that stick for a moment at liftoff, pitfalls, loose and porous soil, grass and stem jitter, and the landing's wobble.
- **Damage from strikes** (`p7f-damage`, the new PHY-26): a prop or motor that scrapes a wall or an obstacle is damaged by the strike's energy. A chipped or bent blade loses thrust and adds a once-per-revolution vibration; a struck motor loses torque or efficiency. The drone flies on worse, logged and replayable.
- **The fiber** (`p7d-tether`, PHY-18…23): the spool, the span, cuts from sharp reverses, snags and wires, and the link's loss.
- **Each is a Complex change** (D-033): its proposal is researched and drafted by the physics engineer from published sources, never asked of the pilot (D-024), then reviewed by the orchestrator. They run in parallel as slots allow, and each ships into the player behind the build command.

## D-057 The OSD shows the arm state, the amps and the volts · Accepted
The pilot, 2026-11-02: "when armed it says AIR, when not it says DISARMED" and "let's simulate ampers and voltage".
- **The flight-mode element** reads **AIR** while armed and **DISARMED** while disarmed, as Betaflight's OSD does. The armament label "! SAFE !" (D-011) and the name "Svinoriz 10 Opto" are unchanged.
- **Amps and volts from the sim:** the pack's loaded voltage (total and per cell), the current drawn (A) and the charge used (mAh) come from the battery and ESC model (PHY-11), through the feed signals, at the OSD's refresh, with Betaflight's voltage filter and warning hysteresis (D-048). The low-voltage warning follows `PackLow`.
- **This is `p4-feed` 1.5** (the OSD content model), built on the player's branch.

## D-058 The wind's two numbers, and landing it before every target passes · Accepted
From `p7c-wind`'s re-grade on the merged airframe (model 18):
- **The rotor's in-plane drag is the derived 0.044 N per m/s,** not the 0.25 tuned to the old 13 m/s calibration. PHY-17a's cruise replaced that calibration: clip P's 22.7° now flies 20.6 m/s, inside 17–22.
- **Severe's centre is 9.5 m/s,** tuned until T0 passes, as design 6 says. Windy and Calm follow from it.
- **The wind lands in the player with task 1.5 open:** Severe meets 9 of 21 targets, Windy 10, Calm 9 of 9. The field is far closer than the stand-in it replaces. The misses are the roll and pitch spreads (2–3× low), with yaw answering 3× more than roll and pitch against P: the next flight-model investigation.

## D-059 Vegetation, soft wires and a camera that feels every movement · Accepted
The pilot, 2026-11-02, after flying the map build:
- "straws and leaves still dont affect flight physics. in real life a straw or two even tho they are tiny may affect the flight - add wobbling, shaking, props getting stuck a bit etc."
- the electrical cables: the drone should get tangled, twisted and flipped, "just like in real life coz they are smooth, they are not solid ... they are soft and a bit elastic"
- "there's still this smoothness to how the camera view looks. the drone's camera must be sensitive to micro movements of the whole drone"

What follows:
- **Straw, leaves, grass and twigs act on the drone** (`p7a-ground`, PHY-05, with `p7f-damage`): a prop that cuts a straw or a leaf takes a torque spike and a short rpm dip, and can stall a moment; the body and legs feel drag and jitter in weeds and crowns. Each is a physical force from the stem's or leaf's own mechanics, never noise on the pose.
- **Wires are soft and elastic** (`p7d-tether`, PHY-22): a wire is a tensioned cable that deflects, stretches and slides. It can wrap a prop, an arm or a leg, entangle the drone, twist it and flip it, as a real strike does.
- **The camera shows the body's every movement** (a new `p7e-camera-feel` change, PHY-25):
  - the rendered camera takes the sim's pose at each video field, never smoothed
  - the camera's soft mount and the frame's vibration reach the picture
  - each prop's small factory imbalance (bounded, seeded, logged) and the frame's modes give the high-frequency shake the pilot knows
- **Order:** these join the ground, damage and fiber changes (D-056). The camera change starts first, because it needs no new world data.

## D-060 The picture's fast motion comes from the turbulence, not the props · Accepted
From the micro-motion investigation (`docs/reference-notes/hover-micro-motion.md`, `unity/physics/p7-micro-motion`). It corrects D-059's camera bullet:
- **Like for like** (Severe cruise, the footage's frame rate, 4–15 Hz), the sim's camera moves 3–4× less than clip P in roll, 2× in pitch and about 5× in heading. The earlier 40–1000× compared the stand-in wind's hover with P's cruise and P's tracker noise.
- **The gust difference between rotors makes 87–97 % of it.** The factory imbalance, the blade pass, ripple and cogging are vibration (`CameraVibration`, the gyro's once-per-rev peak), at most about 1e-4° in the picture; `p7f-damage` grades them as vibration only.
- **`p7c-wind` takes:**
  - z0 from the landscape's class (Davenport–Wieringa class 4, z0 0.10 m, σ_u/U 0.164)
  - the turbulence carried down to the discs' 0.5 m
  - the harness pilot's split set to match P's measured roll share
  - the grade above 4 Hz against P's real motion, with the recorder's field timing
  - the pilot's stick noise from a published human-operator model (the noiseless pilot as the known-wrong, never tuned to pass)
- **Roll and heading then match P;** pitch stays 3× low. A new flight-model item, the rear rotors in the front rotors' wake in forward flight, is named for it.

## D-061 Which objects get a tree inside them · Accepted
Supersedes D-054's last sentence. The world builder measured it, and QA accepted it:
- **S04's straw patch stays in the grid:** a tree over its 195 long, crossing strips measured about 20 % slower than the grid's walk.
- **A tree inside an object goes only to objects with more than 256 shapes.** The straw's cost was the exact distance tests, not the search for candidates.
- **Physics' pattern meets the 60 µs line** at p99 on every row (crown, H01_D, E01, posadka, straw, stand), with 0 B.

## D-062 Disarming doesn't end the flight · Accepted
The pilot, 2026-11-02: "arm disarm change AIR DISARM on OSD, dont make disarm trigger that stupid dialog."
- **Disarming no longer ends the flight.** The OSD shows DISARMED (D-057); the drone stays in the world under physics, and the pilot can re-arm with the switch and fly on.
- **The flight ends on:** a crash, the failsafe, the battery cut-off, or End flight from the pause menu. The post-flight summary shows only then.
- The arm and disarm events stay in the log, and replay is unchanged.

## D-063 Nothing interrupts the flight; the pilot watches the drone · Accepted
The pilot, 2026-11-02: "i dont like that whenever something is off this dialog pops out. just let me see the drone behavior".
- **No event ends the flight on its own.** After a crash, an upside-down rest, the failsafe or the battery cut-off, the drone stays under physics (tumbling, falling, lying where it lands) and the pilot keeps watching through the feed or the chase view.
- **Each event is logged** as before (the crash names its object, LOG-03), and the flight's events are listed when the pilot ends the flight.
- **The summary shows only on End flight or Restart from the pause menu.** No dialog or overlay appears during the flight.
- **Supersedes the end rule** of D-062 and APP-04: the flight now ends only by the pilot.

## D-064 The drone is never perfectly still: low-throttle and near-ground micro-motion · Accepted
The pilot, 2026-11-02: "in real life motors may be different just by 0.0004%, randomly, but this tiny difference is visible in micromovements and microshakings of the camera when on a slower speed, lower throttle etc. ... make it act realistically unexpected (but not like unexpectedly crashing) ... one can sense these things ESPECIALLY when lifting off or landing."
- **The target is the pilot's footage:** the reference analyst measures the camera's micro-motion (rates and spectra by axis) in the clips that show a liftoff, a hover near the ground and a landing, as clip P was measured for the wind (D-028).
- **The sources are physical and published,** each a bounded perturbation of a named parameter, seeded and logged (PHY-24), never noise on the pose:
  - each rotor's own thrust and torque unsteadiness in hover, from published rotor measurements
  - its rise near the ground as the wash recirculates unevenly (ground effect's unsteady part)
  - each motor's and ESC's own dynamics (time constant, friction, low-throttle and idle behaviour), drawn per motor per flight
  - the legs' stick-slip at liftoff (`p7a-ground`)
- **Graded like the wind:** each target is met against the footage and fails a known-wrong model (a perfectly steady rotor), and nothing is tuned to pass.

## D-065 The feed's noise is the camera's, not a radio link's · Accepted (its dropout removal superseded by D-085)
The pilot, 2026-11-02: "the noise is ok, although those black sticks among other noise effect type - that's unnecessary. remember it's not radio, it's optic fiber, so most of noise comes from the analog camera's encoding, not from link quality".
- **Removed:** the link-type artefacts. That means the black streaks or "sticks", and the free-running dropouts to the blue screen (they were D-026's port of a radio link's behaviour).
- **Kept:** the analog camera's own encoding noise (luma and chroma noise, the PAL encoding's artefacts), which the pilot finds right.
- **A picture loss now comes only from the fiber** (a break or a margin dip, `p7d-tether`). This supersedes my answer 1 in `p7d-tether`'s design.

## D-066 The drone's power, mass and rates follow published builds and the pilot's profiles · Accepted
The pilot, 2026-11-02: the rates feel "overly smooth"; cargo drones' "motors are pretty powerful because drones like this are designed to lift some unusually high weight"; "such drones lift off at 30-45 [% throttle] (based on the exact model)", not at 80 %; a loaded drone is about 3.5–4 kg in total. The setup's weight is the bottle only.
- **The mass build** is re-derived from published specifications of comparable heavy 10-inch fiber drones (VISION names Viriy 10 Opto, BOMBUS Opto, OTU, Beshketnyk), with their takeoff mass and payload and each component's published mass. It is checked against the pilot's 3.5–4 kg loaded total.
- **The power:** the motors' thrust comes from published test data for the class (10-inch props, 6S), and the ESC rating and current limit from the class's published ESCs. This revisits D-051's assumed 40 A. The pilot's 30–45 % liftoff throttle is the check, never a tuning target alone.
- **The rates and throttle defaults** come from the pilot's FpvPrep profiles (D-023), without the extra throttle softening.

## D-067 Landing stability is computed: toppling and the coil's tilt · Accepted
The pilot, 2026-11-02: in real life they fall on landing "quite more often than i do in this game"; the sim "tolerates excessive angles of how i position the legs corresponding to the ground"; landing on the coil "does tilt, but it tilts super slowly, in real life it happens a bit faster". There are no figures; "calculate thoroughly".
- **Computed, not tuned:** the physics engineer derives the toppling condition and time from rigid-body mechanics (the support polygon, the centre of mass height and the inertia, the skids catching on the ground, the leg and contact stiffness and damping, friction), then compares the sim with the analytic answer and fixes the cause.
- **The coil (legs off):** its tilt rate is checked against an inverted-pendulum rock about its contact.

## D-068 The micro-motion's published level is kept before its spectrum is measured · Accepted
From `p7b-airframe` 1.10 (D-064). The three sources, each from published measurements, take the camera's micro-motion from about 1000× too quiet to inside the footage at 1–4 Hz, and lower hovers are livelier, as in E, C, D, F and H. Above 4 Hz it reads 1.5–2.1× over the footage, and `p7c-wind`'s T6d pitch now fails (Severe 0.071°, Windy 0.063°, ceiling 0.044°).
- **The published level is kept.** The cause is named: the shape of a 10-inch two-blade prop's thrust spectrum below the rev (its corner is borrowed and its roll-off first order), not the level. A too-lively picture at 4–15 Hz is closer to the pilot's account than the old 1000× too-quiet one.
- **Next:** the spectrum's shape from a published thrust history (Shetty & Selig 2011's static APC 10×5) or a thrust-stand recording, and a floor on the harness pilot's stick noise, so that the hover's yaw is not too still.

## D-069 The class's ESCs limit no current; the punch-out shake is the wash's · Accepted
From `p7b-airframe` 1.10 (D-066), on published specs: the class's ESC (SpeedyBee BLS 60A, BLHeli_S J-H-50; 60 A continuous) senses no current, nor does Bluejay, and BLHeli_32's protection is opt-in. Supersedes D-051's cause and D-053's tilt numbers.
- **`drone.json`'s ESC limit is "none".** Full thrust is 14.17 kgf (3.55 kgf a motor at 47.8 A, inside the ESC's 60 A); the 40 A protection is kept only as a compared variant.
- **The throttle-punch shake's cause is the rotors re-ingesting their wash.** With the rings off it shows on 84 of 240 punches against 186. Its tilt is 0.13–3.35° (was 1.3–10.7° with 40 A), and the arrest takes about half as long. The pilot's 0.3–0.7 s holds from 9 m/s (median 0.60 s); from 2.5 m/s it barely shows, which fits "doesn't happen much".
- **The mass build stands** (published within 2–4 %): 4.09 kg ready to fly without a bottle (coil full), 3.74 kg with the coil half paid out. On the pilot's flown curve a hover takes 37 % of the stick empty, 45 % with 1 kg and 53 % with 2 kg on a full pack. The pilot's 30–45 % holds up to a 1 kg bottle; at 2 kg the class's single 6S3P pack (about 1 kg lighter) would be needed.
- **G7's ESC-firmware question** now decides only whether the protection variant is flown.

## D-070 The punch-out shake has two causes at model 26: the wash first, the motors' spreads second · Accepted
From QA's review of the physics line (`p7b-airframe/review-integration.md`, B2). Restates D-069's evidence at model 26; D-069's ESC finding stands.
- **The wash stays the main cause.** With the rings off, the shake shows on 149 of 240 punches against 206 (seeds 1–40; 148–173 against 206–220 on four other sets). D-069's 84 of 240 was the power branch's number.
- **D-064's motor spreads are a second cause.** At full duty every motor saturates, so the loop can't trim their spread. In the pilot's own case (the empty drone's full punch from 9 m/s) without the ring, 16–24 of 40 flights still shake 0.3–0.7 s; with the spreads off as well, 4 of 40.
- **No current limit trims either cause**, so G7's ESC-firmware question still decides only whether the protection variant is flown.
- The tests judge the wash by the shake past the motors' spin-up kick, on seed sets sized to a false-failure rate under 1 % (D-053's rule).

## D-071 The trench's cavity is built in `p7c-wind`, from published cavity flows · Accepted
From QA's wind review of the physics line (`p7c-wind/review-integration.md`, S5). PHY-13 and `p7c-wind`'s proposal promise the cavity under a trench lip, but nothing decided it. At model 26, model 20's hole rule restarts the open profile at a cut's floor, so in the map's Severe wind E01's run reads 0.47–1.20 of the open field at the same height over its floor.
- **It is built, not deferred.** A launch or landing in a cut is a flight the pilot makes, and a cut's shelter is physical (fidelity is never simplified).
- **The physics engineer builds it in `p7c-wind` (a new 1.10)** after the line lands. The flow inside a cut comes from its depth below the lip (WLD-16: `SampleGround` keeps `TerrainHeight` at the lip over a hole) and its width along the wind, by published open and closed cavity-flow measurements (the shear layer over the cut, the recirculating cell, reattachment on the floor once the cut is long enough). Every number is cited or derived, never asked (D-024).
- **The world builder** adds whatever per-hole data that model needs and the map doesn't already give.
- **Until it lands,** the build's description lists the trench's shelter as not yet done.

## D-072 Model and log-format numbers are assigned once, across every branch · Accepted
The physics branches kept colliding: models 21–26 and formats 14–15 were each taken on more than one branch, so every merge had to re-number and re-audit.
- **The orchestrator assigns the numbers** when a physics branch merges another line or starts a change that moves the model. A number is used once on any pushed `unity/physics/*` branch, never twice.
- **Assigned on 2026-11-03:** `p7a-ground` model 27, format 17; `p7d-tether` 28, 18; `p7f-damage` 29, 19 (taken when its first code lands). A later step in a line asks for its next pair.
- **A landing on `unity/main`** that combines two numbered states takes a new number above both, and audits the goldens case by case.

## D-073 A bound motor takes the ESC's drive without a current limit · Accepted
From `p7f-damage` 1.1b. The damage design put a bind at Kt·I_lim = 0.42 N·m (40 A, D-051), but D-069 found that the class's ESC limits no current. Supersedes `p7f-damage` design 8's and its open question 3's 40 A policy.
- **The drive into a slow or stalled rotor is the electrical chain's own:** the duty the ESC allows at that speed (BLHeli_S's and Bluejay's published low-speed power rules, derived in `p7f` 1.5), the pack's sagged voltage, and the winding, ESC and wiring resistance. On a full pack without such a rule, the stall torque is about Kt·V/R ≈ 2.4 N·m.
- **The 40 A protection** stays only as the compared variant; G7's ESC-firmware question decides only whether it is flown (D-069).

## D-074 Log event kinds are assigned once, like the model numbers · Accepted
`p7d-tether` 1.6 appended event kinds 14–16 (`Payout`, `TetherWedge`, `WorldBug`) above `unity/main`'s 13; `p7a` and `p7f` would have appended the same numbers. Extends D-072 to `StepTrace`'s event kinds.
- **Taken:** 14–16 by `p7d-tether`. The next free kind is 17; the orchestrator assigns each new one when a task asks.

## D-075 The fiber reaches a disc only by a modelled flow: the fountain and the ground vortex · Accepted
From `p7d-tether` 1.6. Design 4's rotor air has inflow only on the intake side, so a line lying under the rear never moves toward a rim at a punch (0.24–0.29 m away, no disc crossed). The scenario's "toward the nearest disc's edge" is right about the risk but needs a physical path, never a pull toward the rim.
- **The path is built from published measurements:** in ground effect the wall jets of neighbouring rotors meet under the body and rise as a fountain that the discs re-ingest on their inboard side; in forward flight near the ground a ground vortex rolls up ahead of the wake. Both are measured for multirotors, tandem rotors and jet lift in ground effect, and the brownout studies track particles along them.
- **A new task, `p7d` 1.6b,** builds both into `RotorAir` after 1.6; until it lands, the punch scenario asserts what the field gives and prints the rim distance.

## D-076 The rates and throttle default to the pilot's FpvPrep profiles · Accepted
The pilot's feedback on the model-26 build: roll and yaw are "super slow", it is "ultra smooth and slow", and the throttle should be "a bit more powerful": lift off "a little bit earlier". Supersedes D-023's blend (the flown set moved 25 % toward the factory set, throttle 60 %).
- **The rates are the pilot's own:** the rate profile they fly, read from FpvPrep's presets and the saved configs of their quads (`C:\.dev\FpvPrepConfigurator`: `presets/`, `quads/*_after.txt`; their diffs carry several rate profiles, so the flown one is found, not guessed). The roll, pitch and yaw rates, expo and feedforward use Betaflight's CLI names, as before.
- **The defaults win over a stale save:** `fc-settings.json` written by an older default set is migrated to the new defaults; a value the pilot changed in the app is kept.
- **The throttle curve** comes from the pilot's own `thr_mid` and `thr_expo` values, chosen so liftoff comes a little earlier than in the model-26 build. The build prints liftoff and hover stick at 0, 1 and 2 kg before and after. The physics (D-066's mass and power) does not change.

## D-077 The setup loads only the test range · Accepted
The pilot: "remove the field option, we only load the one with many houses". The setup offers no map choice; the floor map stays a test fixture only.

## D-078 R restarts the flight, C toggles the camera's noise, X the OSD's sticks · Accepted
The pilot asked for all three. R restarts the flight at once from the same setup and start point (it doesn't interrupt anything else, D-063). C toggles the analog feed's noise for the rest of the session; the OSD and the picture stay. X toggles Betaflight's stick overlay on the OSD (both sticks' positions).

## D-080 The OSD shows the throttle on the right · Accepted
The pilot sent Betaflight's throttle element as they fly it: the throttle glyph, then the value in percent. It goes on the OSD's right side, in the vertical middle (the pilot, after the first placement opposite "! SAFE !"), in the footage-exact font (D-057's OSD), from the sim's own throttle stick as Betaflight 4.5 computes the element (the stick's channel scaled from `min_check`, before the curve; `osd_elements.c`), corrected from "after the curve" when it was built; the sample's value is never copied.

## D-079 Crashes damage the props and legs; crashes and landing falls come a bit more often · Accepted
The pilot on the model-26 build: the camera is "mega good", the flying "almost top notch", but a prop strike on a wall is "too gameish". The drone bounces off realistically, but the props don't break. "In real life the drone crashes a bit more often (more reasons for it), and it falls when landing a bit more often."
- **Each prop has a health:** `p7f-damage`'s blade state (tip radius, chord left, pitch and flap sets) is that health, and `DamagedRotor` (1.2) already flies it. `p7f` 1.3, blade strikes on shapes, is now the priority physics task.
- **The legs take damage too (a new change, `p7g-legs`):** a leg bends, breaks or comes off the drone when a landing is too hard or not parallel enough to the ground. Its loads, strengths and mounts are researched for the class's legs, never asked (D-024). A bent or missing leg changes the next landing.
- **"More reasons to crash and to fall"** comes from the physics still being built, never from tuning toward a rate: `p7a`'s grooves, held legs and vegetation, `p7f`'s strikes, `p7d`'s line and wires, and the legs. `pilot-questions.md`'s G7 airmode question is answered: landings still fall a bit too rarely.

## D-081 Straw is drawn at a distance, not only up close · Accepted
The pilot on the latest build: "straws render only when you fly close to them". S04's micro mesh is drawn only inside its near ring, with nothing standing in for it beyond, so the straw pops in.
- **The near ring keeps the real strands** (the physics elements, AST-10). Beyond it, the straw is drawn by the asset's lighter LODs and the far layer (WLD micro-detail rendering), blended so it never pops.
- **Only the look changes:** physics reads the elements, never the stand-ins (D-028: a stand-in for the look is allowed where it plays no physical role).
- **Judged** on S04's review sheet at 1.5 / 10 / 50 / 200 m against letter H, with the frame rate on AC (PLT-07).

## D-082 Props are real parts in every state; an armed prop on something strikes it · Accepted
The pilot on the model-44 build, landed and disarmed tilted on the car's roof rack with a prop touching it: "the moment i ARM (even without using any sticks), i should move somehow as per physics", and "props seem to be noclip, they shouldn't be. each drone's component must affect its reaction with the world".
- **A prop at rest is a rigid part of the drone:** its blades have contact shapes at their current angle, bear load like any part, and bend or break by `p7f`'s blade laws when the load passes them.
- **An armed prop at idle** (Betaflight's motor idle, no sticks) that touches something strikes it by `p7f` 1.3's laws. The pass's impulse moves the drone; the blade chips, and a rotor that can't pay the pass jams.
- **No part is ever passed through:** every visible part of the drone, the frame, motors, props, legs, coil and bottle, has a contact shape that matches it (D-028's "collision follows the visual").

## D-083 Each motor has a health: heat, dirt and wear make it fail · Accepted
The pilot: "each motor also should have their health var… the drone is upside down on the ground, so motors end up scraping directly against the ground. Not only do the propellers get damaged, but the motors themselves suffer too—they overheat due to the propellers making direct contact with the ground, and dirt or other debris can also get inside. In such a scenario, the motors begin to fail one after another (how quickly… depending on the physics, as well as the duration and intensity of the throttle while scraping against the ground)." Supersedes `p7f-damage` design's "heat and desync stay out of scope".
- **Heat:** each motor's windings heat by I²R and cool by their published thermal capacity and resistance; a stalled or ground-loaded rotor draws more current and heats faster. Past the insulation's rating the windings short and the motor loses torque, then fails.
- **Dirt:** soil and debris in the bell's gap (from the motor scraping or its prop carving soil) add rubbing drag and raise the gap's friction; enough of it binds the motor (`p7f`'s bind law).
- **Wear and shocks:** a strike loads the shaft and bearings (`p7f`'s internals); a bent shaft or a damaged bearing adds drag and vibration.
- **How fast a motor dies follows the physics:** the throttle, the time in contact and the load. Never a timer or a tuned rate. Every number is published or derived (D-024).
- **The pilot sees it:** a hot or dragging motor sags in the flight, and the summary lists each motor's state.

## D-084 The stick overlay is small, under the craft's name, and follows the sticks exactly · Accepted
The pilot on the X stick overlay (D-078): "they shouldn't be that huge, they should be quite tiny, place them somewhere beneath the svinoriz osd label centered. also, they are jittery… like 5fps, while they should reflect precise stick positions". Supersedes D-078's "as Betaflight 4.5 draws it" for the overlay's size, place and rate.
- **Small:** two small stick boxes, drawn in pixels, not as Betaflight's 7×5 character crosses.
- **Placed** centred beneath the "SVINORIZ 10 OPTO" label.
- **Smooth:** the cursors move at the feed's field rate from the sim's own stick samples, never at the OSD's character refresh.

## D-085 The no-signal dropouts come back; only the black sticks stay removed · Accepted
The pilot on D-065, 2026-11-04: "ive mentioned the black sticks (so there's different type of noise, one of them is black sticks - they look too artificial so i asked to remove them). i never mentioned the no signal. … i didn't ask to remove anything among analog noise". D-065 read more into the request than was said: it also took out the free-running dropouts to the no-signal screen. Supersedes D-065's "Removed" bullet for the dropouts and its "A picture loss now comes only from the fiber".
- **Back, as the faithful port had them (D-026):** the free-running recovering dropouts to the no-signal screen, on their ported schedule, A/B-checked against the Godot feed.
- **Still removed:** the tear's black wedge, the "black sticks" (D-065 stands for that one).
- **Unchanged:** the camera's analog noise, and the fiber's own picture loss still to come (`p7d-tether` 1.8 and 1.16), on top of the dropouts.
- The two artefacts sit behind one switch (`FeedEvents.GodotLinkArtefacts`); it is split so each follows its own decision.

## D-086 The pilot's answers on legs, the spool, balance and the controller · Accepted
The pilot, 2026-11-05, on the G7 questions (`pilot-questions.md`, answered).
- **Legs:** usually plastic, hand-made, flexible; they vary per drone. The PVC-U legs stand; the leg's stiffness and strength are a per-flight bounded perturbation (PHY-24), not one fixed value.
- **The roof rack:** the drone rested its weight on the prop. Physics says a weight-bearing prop holds the motor at idle torque; what moves the drone on arming is the ESC's sensorless start (its startup pulses against a held rotor), which `p7f` 1.3 left open. It is modelled, not tuned.
- **The spool's exit:** the canister is mounted at an angle so the fiber leaves its hole at about 25–30°, so a climb or a throttle-up shares the pull with the angle instead of dragging the fiber across the hole's edge. Supersedes the canister's 15° (from photos). The fiber's ground end is patched into a ground station.
- **The canister is not opaque:** the coil and its shrinking pack show through it.
- **Balance and loads vary per flight (VISION §2.4's randomness):** the nominal drone is balanced on the hubs' centre (the class's practice, `reference-notes/fore-aft-balance.md`), and each flight draws a bounded offset of the centre of mass in any direction from the hand-strapped load and the build, so the pilot has to feel the drone at liftoff. Also kept by PHY-24: the fiber's rare cuts with no sharp move come from its strength draws, and the wind's gusts may come from off the forecast's direction.
- **The controller:** no airmode (confirmed by the pilot, 2026-11-05: "airmode off. on low throttle we have the minimal motors spinning": the motors keep Betaflight's motor idle; the saved FpvPrep diffs leave Betaflight's default, but the pilot's word stands); brake on stop off, which is BLHeli_S's and AM32's default and was already model 42's: the ESC's run loop still brakes a disarmed motor (damped light) to about 1220 eRPM, about 0.2 s, then it freewheels (`p6-flight-core`'s model-69 note); no return-to-home. A video loss on the fiber is not a control loss: only a fiber break ends control, and the failsafe follows it (`p7d-tether` 1.8).

## D-087 The pilot on the model-68 beta: the house's lag, the fiber's cuts, R after a loss · Accepted
The pilot, 2026-11-06, on the model-68 build.
- **The lag is still there,** most often "when i fly through the first house's window, inside this house even if it doesn't slug i suddenly feel like the drone has RX loss". The step's cost near and inside the house (contacts and the fiber among many shapes) is profiled and cut there; a step that overruns makes the drone answer late, which reads as a lost link.
- **The fiber cuts too easily:** "it cuts, but when turns are OVERLY sharp, or you suddenly go backward - it tears upon the canister hole's edge. but still this behavior should present - this is part of opto pilot's training, they must get used to the quad cutting itself too often." Ordinary turns and cruise don't cut; an overly sharp turn or a sudden backward move tears the fiber at the exit's edge (with D-086's 25–30° exit). The cut stays common enough that the pilot learns to fly for it. From the physics (the exit's geometry, the glass's strength, the peel), never a tuned rate.
- **R after a signal loss:** "sometimes R doesn't restart the race RIGHT AWAY". R restarts at once in every state, a lost link and a broken fiber included.- **Speed alone doesn't cut the fiber:** "in real life when flying very fast you'd think it'd cut easily - it wouldn't. They are designed to fly up to 80-100km/h. However, obviously when on a higher speed, a sharp turn increases the chance to cut." Cruise up to 80–100 km/h keeps the line whole; the risk rises with a sharp turn's rate at speed. (The spool's peel band that cut cruising lines at 15 m/s is wrong against this.)
- **The climb after a pitch-back settles too slowly:** at a fixed throttle, pitching back from cruise lifts the drone (the thrust turns upward), as in real life, "but after pitch backward it's kind taking a long time to adjust altitude". A climbing rotor's thrust falls with its climb rate (its inflow), so the climb rate settles fast; the vertical damping and the motors' response are checked against rotor theory and the class's data.


## D-088 The final world is the friend's farmland, adopted as it is · Accepted
The pilot, 2026-11-08: "i love object trees and stuff and the map in general in the new world". A friend built a seeded 1 × 1 km farmland in Unity (11 fields, tracks, tree belts, a ruined village, hills of 12–42 m, surround tiles to about 4 km); the pilot wants it implemented as it is.
- **It is the map.** Supersedes D-047's "the test range is the map" for the player; the test range stays only for headless tests and benches, so its straw leaves the game.
- **Its layout, look and objects stay.** WLD-01 to WLD-09 hold only where the farmland meets them; its size and relief replace WLD-01's 4 × 4 km and WLD-02's numbers.
- **The hills are wanted:** the sim gets a heightfield ground (the world core has none today).
- The plan is [final-world-plan.md](final-world-plan.md).

## D-089 Third-party packs are allowed; what can't be redistributed stays local · Accepted
The pilot, 2026-11-08: relax the asset rule, and don't push what can't be pushed.
- **Supersedes D-003's free-assets-only rule:** Asset Store, Fab and Quixel, and Unity sample packs are allowed (the farmland's grass, pines, bushes and house model come from them). CC0 is still preferred for new work.
- **Never pushed:** a file whose licence forbids redistribution, or anything that copies its geometry. It lives in a git-ignored local store, linked into each checkout and worktree; the manifest marks it local-only, `CREDITS.md` credits it, and the hygiene scan blocks its paths. The player build includes it.
- **The world leaves `reference/`:** it is source, not reference material. `reference/` stays the pilot's footage and photos only, with its rules unchanged.

## D-090 Our objects on the farmland · Accepted
The pilot, 2026-11-08.
- **The trench:** one `E01_curving` in each field F1–F11, at a seeded random spot and yaw, fully inside the field; cut into the terrain.
- **The houses and the cars:** `H01_I`, `H01_D`, `R01_A` and `R01_F` in the village, on free ground, level on the terrain.
- The friend's five ruins and their debris stay, and get physics data like every other object.

## D-091 A physics acceptance gate before any beta · Accepted
The pilot, 2026-11-08: the simulator must be ready for a Steam release; no more fix, test, fix, and the sluggishness back again.
- **No beta goes to the pilot until the scenario suite passes** on two overnight runs in a row: PLT-04's budgets in the player, 0 late steps, exact replays, finite steps, no tunnelling, forces within physical bounds, 0 B steady state, and the pilot's complaints as named tests ([final-world-plan.md](final-world-plan.md) §6).
- **The lag first:** the model-79 logs are read before the world comes in, so the cause isn't carried onto the new map.
- **The map's edge:** the sim knows the ground heights of the surround and far tiles too (no objects there), so a long fiber flight meets no invisible wall.

## D-092 The farmland's start point is in the village · Accepted
The pilot, 2026-11-11: "our spawn point will be somewhere in the village".
- **One start point,** on free level ground in the village, clear of the ruins, debris, trees and our objects (D-090). With legs off, the launch stand (D-022) goes there.
- Supersedes WLD-08's "at least 6, in varied terrain" for the farmland. More start points only if the pilot asks.

## D-093 Phase order: W, then the pilot flies the farmland, then B and A · Accepted
The pilot, 2026-11-11.
- **Phase L closes with what it has:** the step timer (L4) and the lag cap (L5) merge on the evidence in hand. The fix of the cause (L6) is already folded into W3.
- **W next,** to a build the pilot flies around the farmland.
- **B and A after the pilot's flight,** in an order the pilot sets then. This supersedes the plan's "B in parallel with W".

## D-094 The farmland keeps its scene's frame · Accepted
The orchestrator, 2026-11-11, on the physics engineer's sign-off of W2's map package (its design.md).
- **The farmland spans [0, 1000]² m with its SW corner at the origin,** as the friend's scene places it. Moving it would break the generator's absolute placements.
- A map package states its rectangle in `map_rect_m`, and the sim never assumes a centred map. Nothing in the sim did (checked: object and wake grids, logs, region caches, wind phases).
- Replaces only D-019's centred-map line. D-019's units, axes and row order (north first) stand.

## D-095 Terrain edits are data, applied at load · Accepted
The orchestrator, 2026-11-11, after W1: the farmland's terrain data is linked from the shared local store (D-089), so editing it in place would change every checkout and could never be pushed.
- **W5's trench holes and flattened footprints live in `maps/farmland/`** (the hole mask and the footprints), generated from the placements.
- **The sim and the player apply the same data at load:** the sim to its heightfield, the player to a runtime copy of the drawn terrain. The store's terrain data stays as the friend's generator wrote it.

## D-096 Scanned vendor wood misses AST-05, measured, until phase A · Accepted
The orchestrator, 2026-11-11, on W4a's tree and bush physics (`w4a-farmland-vegetation`, design.md).
- **The friend's scanned trunks are lobed, and their bases flare into roots, stumps and buried wood.** Capsule fits miss the drawn wood by more than AST-05's 5 cm in places, worst at the ground: the island tree up to 493 mm (capsule off the wood) and 511 mm (wood off any capsule), Pine002_L 526 mm, PineDead_02 169 mm. Richer capsule fits were measured and still failed.
- **Accepted for W, so the pilot can fly the farmland:** the build prints the AST-05 audit for every variant and never refuses on it.
- **Phase A fixes it,** with a primitive for flares or a finer fit. The same debt covers the coarse crown shells (felt 0.3–0.7 m before the drawn leaves) and the branches inferred under leaf cards.
- AST-05 stands as written for everything else, including every asset we generate.

## D-097 The world-query API gains a one-shape contact and a no-ground filter · Accepted
The orchestrator, 2026-11-12, on W3's task 2.1 (`w3-farmland-ground`, design.md), the fix for the fiber's query cost (L6). The interface's own comment makes any change to it a superseding decision (it cites D-041, which is about efficiency, so that citation is stale); this entry is that decision, for these two additions only.
- **`IWorldQuery.ShapeContact`** returns `StaticContacts`' own contact for one shape, to the bit. A laid fiber segment keeps a list of the shapes near it and re-asks only those, instead of running a whole query every step.
- **`QueryFilter.NoGround`** leaves the ground out of `StaticContacts`. The fiber takes the ground from `SampleGround`, so its queries no longer pay for the terrain.
- No answer changes: physics model 79 and `QueryVersion` 8 stand, and every replay is bit for bit. Every implementer of the interface carries both.

## D-098 The world-query API gains a ground-only filter · Accepted
The orchestrator, 2026-11-12, on W3's task 2.5 (`w3-farmland-ground`, design.md §2.5), a laid fiber that follows the hills. Like D-097, this entry is the superseding decision that the interface's own comment asks for, for this one addition.
- **`QueryFilter.GroundOnly`** makes `StaticContacts` and `Raycast` answer the ground alone, step faces included. The fiber's crest test casts each segment's chord against the ground with it, without paying for the objects.
- Its ground contact equals the unfiltered query's ground contact to the bit, and existing calls are unchanged, so `QueryVersion` 8 stands. Every implementer of the interface carries it.

## D-099 The terrain near the drone draws at pixel error 0.05 · Accepted
The orchestrator, 2026-11-12, on W3's task 3.2 (`w3-farmland-ground`, design.md "# Task 3.2"), the tech artist's review of the drawn terrain near the drone (WLD-11, VIS-01).
- **In flight, terrain tiles within 30 m of the drone draw at pixel error 0.05,** set from a script. Unity accepts values below the Inspector's minimum of 1 and draws by them. Every other tile, and noclip, keep the friend's errors.
- **Why 0.05:** the smallest error a simplification can leave on these tiles is half a height unit, 1.83 mm. At the FPV render's 965 px/rad that is 0.05 px at 35 m. So no patch within 35 m of the lens is simplified: the full heightmap is drawn in the physics radius.
- **Measured** from the rendered depth (`TerrainParity`): the drawn terrain within 30 m is the sim's to at most 0.075 mm, the measure's own floor. WLD-11's 1 mm is met as written, with no rewording.
- **Pixel error 1 was rejected:** it is invisible in the feed, but it leaves the drawn ground 1.8–16.2 mm off in flight, so the laid fiber (0.125 mm radius) would be drawn under it.
- **No full-resolution patch:** it gives the same 0 mm, but the friend's terrain would have to be hidden under it, against the untouched store.
- **The cost** is the FPV camera's GPU time: about 1.1–1.4 ms a frame at the 20 W power cap, and 0.5–0.7 M more triangles. W6's quiet PLT-07 run gives the verdict (`-terrainab`).

## D-100 Play replaces Train, with two maps: House and Village · Accepted
The pilot, 2026-11-12, after flying the farmland preview (model 85; their words in [handover-2026-11-12.md](handover-2026-11-12.md) §2).
- **The menu's Train becomes Play.** Play opens a carousel of the maps, and then the setup and the flight as now.
- **House** is the test range (map id `test-range`), which comes back into the player. **Village** is the farmland (D-088).
- **Each map card has a still** seen through the analog feed, with the OSD.
  - House's frame shows the car, the fence and the house.
  - Village's is exactly Explore's spawn frame.
  - The stills are generated, by a capture method plus a pose per map, never by hand.
  - A still of the friend's world follows D-089: it isn't pushed unless it is shown to be redistributable.

## D-101 M shows the world's map without pausing · Accepted
The pilot, 2026-11-12, in the same feedback.
- **M toggles a map of the whole world** in flight and in Explore. The sim keeps running while it shows.
- **It shows the drone's position, live.** The panel is centred and a little smaller than the game window, so it never covers all of it.
- **The image of each map is generated** from above, and it follows D-089 like D-100's stills.

## D-102 One stem field feeds the props, the legs and the fiber; furrows wait · Accepted
The orchestrator, 2026-11-12, on the audit of `w9-village-physics` (its `audit.md`, §7 item 9), after the pilot asked for straw, grass and leaves on the props and for every ground nuance of the test range on Village.
- **One stem field** per cell of Village feeds the props, the legs and the laid fiber. It is derived from the export's grass counts and each grass prefab's stems per instance, never hand-painted.
- **The test range's straw joins the same law.** House flies in the player again (D-100), so the props cut S04's straws as they cut Village's stems. The range golden moves there and is audited.
- **Furrows are not in `w9-village-physics`.** Neither map has them in the physics, and Village only paints them (a texture and a normal map), so there is nothing drawn for their collision to follow (REAL-05). They need a world-and-look unit (W12) that draws the relief near the drone and stores it in the package. PHY-04's rocking across furrows stays unmet until then; `tilled` already gives the loose loam's bearing and friction.
- **The wind's cover per cell** is a uniform 0.3 m today. It follows the stem field in a W7 follow-up, not in W9.

## D-103 The first-person camera's place is fitted to the footage, and the turning props ship with it · Accepted
The orchestrator, 2026-11-12, on the tech artist's finding in `w13-props-in-frame` (its design.md §1 and §6).
- **The drawn props now turn with the sim's rotors.** From the lens's assumed place they cross the picture at both side edges in every field of a hover. The footage of the drone's own airframe group shows a blade almost never, and two pale airframe rods at mid-height instead (`reference-notes/vegetation-on-the-props.md` §3).
- **So the camera's place is derived, no longer assumed** (`w14-camera-place`). It is fitted to where those parts sit in the group's clips and checked against the published frame's camera mount. The hubs stay where the pilot's tip-to-tip numbers pin them (DRN-02). The lens's intrinsics and the 0° uptilt stay too (FEED-02).
- **The turning props reach the pilot's build together with the moved camera,** not before. Blades in every frame would be a new miss, not a fix.
- **The blade's smear follows the exposure the feed already uses.** It is the same in bright and dull light until the feed's auto-exposure measures the scene (`p4-feed` 1.7). Nothing is tuned to fake the wider wedge of dull light.

## D-104 The physics is proven against everything the pilot has said · Accepted
The pilot, 2026-11-13: "We run only physics tests and fixes just to be clear. We must recall all of what was discussed before so the physics of drone is top notch this time". The day before: "i must outline again that our main focus is physics".
- **One ledger recalls it all:** `docs/reviews/physics-ledger.md` lists every statement of the pilot's about the drone's physics, every physics requirement and every open task, each with its status (proven by a test, built but unproven, partly, missing, contradicted) and the test that proves it. Its ranked gap list decides what is tested and fixed next. It is kept current, not rebuilt.
- **The pilot's account outranks a calculation's result.** A run that contradicts what they describe is a gap, however sound its law. The first case: the stems-on-props law cut nothing and shook nothing in a landing on Village's tall grass, where the pilot says straws are cut in front of the camera and the quad shakes (`w9-village-physics` 2.12).
- **While the pilot is away overnight, the team runs physics tests and physics fixes only** (said of that night; kept as the rule for such nights until the pilot says otherwise). Rendering, asset, feed, menu and frame-rate work waits, and a running task of that kind is paused with its state handed back.
- **How the world behaves physically is never asked of the pilot** (D-024, sharpened by "Again physics, why are you asking me this"). Only what physics cannot derive is asked: their own kit, and their preferences.

## D-105 The field's pitch is the own wake's floor, and the bodies' couple waits for its hull · Accepted
From `p7c-wind` 1.5's grade on the physics line (model 88; design 11; the number is the orchestrator's to confirm at the merge, the trunk's last being D-104):
- **The field's pitch passes, the wind's own is still 2.1× low.** Severe's field pitch reads 0.23° (P 0.28°), but 0.19° of it is the rotors' own-wake unsteadiness (D-064), which the fixed-vector wind flies as well: T2's known-wrongs pass, and its spectrum sits at 1–4 Hz and above 4 Hz (T6c, T6d fail). Without the own wake the wind's pitch is 0.13°, and it lacks its low band.
- **The own wake's fade with forward flight is the flight model's next item** (`p7b-airframe` design 3e): at the cruise its wake is swept off the disc within a few revolutions, so the hover's wander cannot hold half its level there. It is not changed under the wind's task.
- **The airframe's closed bodies' Munk couple is the low band's missing physics, and it is not landed.** Built from published potential-flow theory on the canister, the bottle and the packs as isolated spheroids, it doubles the low band and lifts T2 to 0.37°, but its yaw-plane twin swings the heading 4.5× clip P's measured residual. Measured P wins: the couple waits for the airframe's hull as one body (the bodies touch under the plates, in the rotors' slipstream), from a flow solver or published short-body data, never a factor fitted to the targets.
- **Gusts from off the forecast's direction (D-086) are already the field's:** over the open field the wind blows from more than 45° off the drawn direction 38 % of the time and from the other side 9.5 %, in the strip 49 % and 19 %, from K§8.3's meander and shifts, the turbulence and the canopy's eddies. Nothing new is built for them.
- `p7c-wind` 1.5 stays open.

## D-106 The punch-out shake is measured over the drone's own floor · Accepted
From `p7b-airframe` 1.10d's grade on the physics line (model 94; design 3g). It restates how PHY-10 is measured; D-069's ESC finding stands, and D-070's order of causes is not shown at this model.
- **The rotors' own wake flies at its measured level.** Stack et al.'s hover record, read point by point, has a thrust σ of 1.7 % of the mean; model 92 read its peak to peak as 4.2σ and flew 2.9 %. The hover's motion above 4 Hz now sits at the footage's top (0.042° roll, 0.037° pitch, against 0.042° and 0.032°), so D-068's 1.6–1.9× excess is gone. Its rates and its 1–4 Hz band are now under the footage's: not met, and not tuned.
- **The shake scenario flies that wake, as every flight does,** and PHY-10's claims no longer hold against a fixed line. The floor's single fields reach the visible line (one luma sample, 0.088°): a gentle step from the hover "shows the shake" on 20 of 40 seeds, and the known-wrong without the ring passes (past the kick on 198 of 240 punches against 217).
- **The shares at this model, past the kick, out of 240 punches:** the own wake 86 (217 against 131 without it), the motors' spreads 20 (198 against 178, ringless), the ring 19 (217 against 198), the forward centre of mass 10 (198 against 188, ringless). Two things stand between these numbers and a cause:
  - in a descent past J −0.05 the props' held branch passes the wake's, the wash's and the ring's inflow on one side only (`p7b-airframe` 1.10e), and an arrest starts in exactly that state;
  - the measure counts the floor's own fields as the shake.
- **The shake is an excess over the same flight's hover** (`p7b-airframe` 1.10f, after 1.10e). What the pilot calls the image vibrating is motion the hover before it does not show. The margin comes from a false-alarm rate on the hover's own distribution, never from the pilot's 0.3–0.7 s, and the visible line stays the floor of what shows at all. PHY-10's cause sentence is restated from that grade.
- **Until then** `ShakeTests.ShakeOnlyWhenItsCausesMeet`, `flightbench --shake` and `--scenarios` fail on the physics line, and the physics ledger's row P23 stays "partly".

## D-107 The hull's moment is measured, it restores, and it is not flown yet · Accepted
From `p7c-wind` 1.5's second grade on the physics line (model 94; design 12). It settles D-105's third point.
- **As one body the hull's potential couple is as large as its parts'.** The canister, the bottle, the packs and the plates as one hull (gaps under 10 mm closed) carry 0.30 N·m nose down at the cruise, against the separate bodies' 0.32. Treating the parts apart never overstated it.
- **Measured short blunt bodies turn the other way.** Flat-faced cylinders of the hull's proportions restore, the nose turning back into the flow, up to 28–73° of incidence (AEDC-TR-72-180); at L/d 1.53 near 0° the measured moment is −1.7 times Munk's. No share of the potential couple exists for this body: the potential couple is the known-wrong, and it fails as one (Severe T2 2.92° against P's 0.28°).
- **The measured moment is built and not flown.** In pitch it lowers the wind's own motion a little further (Severe T2 0.136° → 0.125°, P 0.28°). In yaw its weathercock lifts the field's swing toward the footage (T5 0.345° → 0.389°, P 0.44°) but breaks the footage's roll-with-yaw-rate correlation, which passed (T10b 0.70 → 0.21 in Severe and −0.14 in Windy, P +0.57). Measured P wins: the law, its data and the hull's added-mass tool (`tools/hullmass`) are on the line, switched off in every flight.
- **What decides it:**
  - first the harness pilot's pitch and yaw holds checked against P, since a weaker yaw hold than a real pilot's gives the same miss; then the hull is graded again;
  - failing that, a measurement or a flow solution of the airframe's own yaw moment in sideslip, with its open sides, its arms and the rotors' wash.
- **The low band's missing motion is still open** (T2, T4, T9b), and the hull is not it. The candidates left: the harness pilot's holds, the vertical gust gradient across each disc, slow vertical gusts at the drone's height.
- `p7c-wind` 1.5 stays open.

## D-108 The punch-out shake's causes on the new measure: the wash first, the spreads second · Accepted
From `p7b-airframe` 1.10f on the physics line (model 96; design 3i). It carries out D-106 and restates D-070's numbers; D-069's ESC finding stands.
- **The measure, as built** (D-106): a field shows the shake when its motion reaches 3.77 times the same flight's hover (the last 2 s before the chop) and at least the visible line. The margin is the largest of 10,000 held hovers a payload, so a held hover reads as a shake about once in 10,000 flights. Fields within 0.1 s of each other are one episode. On the old fixed line half of all held hovers "shook".
- **The causes, on 200 seeds of the six full punches** (lasting past the motors' kick, out of 1,200): the drone 258, without the ring 107, without the motors' spreads 217, with the centre of mass centred 261, with a rigid airframe 251. So the re-ingested wash carries about three fifths of it (151) and the spreads about a sixth (41); the forward centre of mass and the frame's flexing carry none. D-070's order holds on the new measure.
- **The rotors' own wake is not a cause.** It is the floor the shake is told from: without it more punches read as a shake (468), and at model 92's higher level fewer (133).
- **Against the pilot's account:** the empty drone's punch from 9 m/s shakes 0.3–0.7 s on 80 % (median 0.42 s). Never from a held hover or a gentle step (0 of 400). Past the kick on 21.5 % of all full punches out of a descent: "doesn't happen much" is met.
- **Open:** at 2 kg the shake is short (medians 0.06–0.14 s) while the arrest is long (0.35–0.94 s), so "longer when heavy" is not what the physics shows. Whether the loaded drone's short shake is right is not settled: its turn per thrust step against its own floor is probed before PHY-10's payload clause is fixed.
- **The margin is pinned at model 96.** A change that moves the hover's floor re-runs its calibration (`ShakeAttributionProbe.TheHoldsMargin`).

## D-109 The wind harness flies a human pilot; the hull still waits · Accepted
From `p7c-wind` 1.5b on the physics line (model 96; design 13). It answers D-107's first decider.
- **The harness pilot is a human's.** McRuer's crossover pilot at the published 4.3 rad/s on every axis with an effective delay of 0.10 s, and Levison's remnant: observation noise at −20 dB on the roll, pitch and heading errors through a 0.1 s neuromotor lag, beside D-060's −25 dB motor noise, each a seeded bounded stream on the sticks. Nothing is fitted to the wind's targets.
- **It reads P's own decays back better than K§8.5's pilot did** (roll / pitch / yaw to 1/e: 0.29 / 0.20 / 0.28 s, against P's 0.30 / 0.17 / 0.33 s and the old pilot's 0.36 / 0.17 / 0.36 s), so it becomes the wind harness's default pilot. K§8.5's pilot stays as a variant and the noiseless human is the known-wrong. With it Windy meets 13 of 21 targets (9 before) and Severe 11 (12 before): Severe gains T5 and T6b–c and loses T3's ratio, T7's belt share and T11b's tilt. The pilot is chosen for reading the measurement back, not for the count.
- **The hull's yaw moment still breaks T10b with the human pilot** (Severe 0.16 and Windy −0.09, against P's +0.57), and without the hull the human passes it at both levels. So the pilot's yaw hold was not the reason. What is left is D-107's second decider: a measurement or a flow solution of the airframe's own yaw moment in sideslip. The hull stays off.
- **The low band is still missing** (Severe T2 0.144° against P's 0.28°; pitch's 0.23–0.5 Hz band three times under). The slower hold fixes the spectrum's shape, not its size. The candidates left are the air's: the vertical gust gradient across each disc, and slow vertical gusts at the drone's height (`p7c-wind` 1.5c).
- **The hover is still too still** in its 1–4 Hz band, and its heading most of all: 20–45 times under the footage with either pilot. A published remnant scales with the errors a pilot sees, and a near-still hover's are small. What moves a hovering drone's heading is open (`p7c-wind` 1.5d).
- The golden flights keep the pilot they had.

## D-110 A hovering drone's heading is moved by the pilot's hand, not by the airframe · Accepted
From `p7c-wind` 1.5d's ranking on the physics line (model 97; design 14). Nothing is built.
- **The gap:** in a hover the sim's heading is 20–45 times stiller than the footage's (yaw in the 1–4 Hz band 0.005° against 0.11–0.15°).
- **No airframe source is near it.** Each was ranked by hand on the airframe's own numbers and checked against the flown sim:
  - each rotor's torque unsteadiness: 0.001° as flown, at most 0.015° if it moved in step with the thrust, 0.041° with its in-plane twin;
  - the motors' spreads under the roll and pitch corrections: 0.001–0.002°;
  - the products of inertia: under 0.003°; the fiber's pull at the nozzle: 60 times short;
  - the air's swirl: it would take 0.8–1.1 m/s of uncorrelated in-plane air at each hub, where a calm hover's eddies give about 0.1 m/s.
  Together they stay under 0.004°, which the flown floor confirms (0.0024°). The physics lacks nothing that matters here.
- **The yaw loop is not the reason either.** It barely rejects anything at 1–4 Hz (its crossover is 1.4 Hz against roll's 6.2 Hz), and a footage-sized heading uses under 1 % of the yaw authority.
- **The hand is the only candidate of the right size.** The footage's heading needs 11 µs of yaw stick past the deadband (2.3 % of half stick), and it holds 0.07–0.21° across every phase and every air, as a hand's would and an airframe's would not. A published remnant scales with the errors a pilot sees, so the harness's human is rightly quiet in a still hover; the hand's tremor is inside every deadband; crosstalk from the throttle reaches it only from a busy throttle, which no published model gives.
- **Part of the rate gap is the footage's measurement.** The rotation-only fit reads the camera's own sideways travel as yaw and its heave as pitch: below 1 Hz a drift of 2–5 cm/s at 1–2 m reads 0.4–1.9 °/s. The footage's hover rate and its bands below 1 Hz are upper bounds on rotation.
- **So:** in the game the pilot's own hand supplies the heading's motion. The harness cannot grade a hover's heading against the footage until a human's stick in a hover is sized from data: a public dataset of people hovering small rotorcraft by radio is the next source, and failing that one blackbox log of a hover from the pilot's own quad (a question about their own kit, logged). `p7c-wind` 1.5d stays open.

## D-111 The stems keep the undiminished column until the near wake is whole · Accepted
From `w9-village-physics` 2.13's grade on the physics line (model 97; design 2.13).
- **The stems can read the fiber's wash** (design 19's law: the near wake, one merged jet with its measured decay and spread, its landing). It is built, checked against the hand calculation at 16 points, and switched off in every flight.
- **Why it is not flown:** with it most straw under the discs stands. In a vertical landing on Village's densest tall cell 7 / 14 / 20 % of the stems under the discs are folded with the hubs 1.85 / 1.0 / 0.5 m up, against the column's 72 / 71 / 57 %; from a 3 m hover 1–4 %. The pilot: "Usually it lies flat because of prop wash power, but not always." The pilot's account outranks the calculation (D-104).
- **What the law lacks, traced:**
  - the short dry culms under the discs sit 1.3–1.7 m below the hubs, in the merged jet's 10–35 Pa, and need 100–400 Pa to fold;
  - the tall stems stand in each disc's outer ring, where design 19's near wake has no flow: its column does not spread;
  - only the jet's mean is built, where its turbulence would fold a near-upright dry stem from about 0.6 m.
  So the far field is right and the near field is incomplete.
- **What it moved the right way,** for when it flies: twice the fresh cuts at the front discs (137 against 69), and a shake that still grows with the straws' number and thickness (2.4–15.7 °/s across the sweep, against 0.03–9.7).
- **What decides it** (`w9-village-physics` 2.14): the near wake's contraction and spread and the jets' turbulence on near-upright stems, for the stems and the line together; then the stand's mutual contact. The wash flies for the stems when the folded share meets the pilot's "usually", and when the step's cost in the densest cell (100–150 µs more, unmeasured on a quiet machine) fits the budget.

## D-112 The wind's slow pitch is the pilot's, not the air's · Accepted
From `p7c-wind` 1.5c's ranking on the physics line (model 97; design 15). Nothing is built in the wind.
- **The gap:** with the human harness pilot the drone's slow pitch in Severe wind is about half clip P's (T2 0.144° against 0.28°; the 0.23–0.5 Hz band 0.085° against 0.27°).
- **The field already flies the published vertical gust** at the harness's heights: in 0.23–0.5 Hz it carries 1.03 of Kaimal's spectrum at 20 and 35 m, and more than the published below 10 m.
- **The air cannot give P's band.** Per m/s of updraft the closed loop pitches 0.08–0.25° over 0.23–0.5 Hz, so a gust of the published size gives 0.06°. P's 0.27° would need 1.63 m/s of updraft in that band alone, 4.6 times the published band, and scaled that far P's other measured targets fail (T1, T5, T6d). The gradients between the front and rear rotors and across each disc, the mean shear, the belt's eddies and the terrain's slow updrafts are each under a tenth of what it needs.
- **The footage itself says so:** its calm phases show more slow pitch (0.18–0.62° at 0.4–1 Hz in the hover and the low cruise) than P's storm does (0.155°).
- **So the low band is the pilot's own slow pitch:** the speed and course corrections the harness's pilot does not make, since it holds one pitch. In the game the pilot's hand supplies it. The harness can grade it once a pilot's slow corrections are modelled from the calm footage (`p7c-wind` 1.5f), which is a pilot-model task and not physics.
- **What the field does lack, for its own task** (`p7c-wind` 1.5e): its turbulence's scale with height. It holds one length scale at every height, so below 10 m its vertical gusts are too slow: 1.2–2.4 times the published below 0.5 Hz and 0.53–0.81 of it at 1–4 Hz. That moves the hover and the low flight in wind.
- `p7c-wind` 1.5c is closed as ranked; 1.5 stays open on its targets.

## D-113 The build goes first, with everything recently built merged in, tested fully or not · Accepted
The pilot, 2026-11-14: "first prepare the build, i would like to test everything, THEN you will do the additional tests you wanted PLUS maybe my feedback", and then: "let's make sure all of the recently done changes are merged in (even those which haven't been tested fully at the moment) and prepare the build ASAP".
- **The build** is the player on the physics line at model 100 (`w15-player-physics`): a flight draws its legs' stiffness from its seed, the summary names the stems the props cut, the feed is handed the stems' signals, and the same flight gives the same bits in Unity and headless (two seeds, on .NET and on Unity's Mono).
- **Merged before its tests were all judged:** the wind field's turbulence by height (`p7c-wind` 1.5e, model 100). Its goldens hold and the world benchmark passes; the full suite and the line's benches were not run on the merged tree before the build. Two of its scenario tests stay red, on faults the new air shows elsewhere: at 6 to 10 m/s a broken leg's hanging half (model 99) or an intact arm passes through a wire's cable, and the fiber passes through a 2 mm twig instead of wrapping it. Unity's tests that pin a windy flight's hash are not re-pinned for model 100 yet.
- **The build's review was the gate's:** the diff against the change's non-goals, a flight bit for bit on QA's own seed, hygiene and licences, the smoke test. The longer checks follow the build.
- **Not in the build, and why:**
  - the turning props (`w13-props-in-frame`, built): D-103 stands, they ship with the camera's move, which is planned and not built. The pilot decides at the build's handover whether they go in before it;
  - our trenches, houses and cars in Village (`w5-farmland-objects`): half built. They are placed in the map's package, but the ground is not yet cut and levelled for them at load (its task 1.3), so the trenches would lie under uncut ground and the drawn world would not match the physics (REAL-05). Its merge also needs the world's goldens re-recorded.
- **Judged after the build, with the pilot's feedback:** the two faults above; in wind the laid line wearing through on a small pine's twigs in 78 s; a leg cracking from 0.30 m/s of sideways drift, and the kerb's jump from no damage at 1.25 m/s to three or four legs at 1.5 m/s; on Unity's runtime the allocation probe cannot see one small object, and one sweep test runs past Unity's default time limit.
- **The physics work that was waiting** (the trench's soil, the near wake, dry ground's hold) starts once the build is in the pilot's hands.

## D-114 The pilot's flights on model 100: the line's breaks were a contact fault, the sluggishness the step budget; the plants move in the drone's own air · Accepted
The pilot, 2026-11-14, after 14 flights on the model 100 build (Village, Severe, a 2 kg payload, legs on; ledger P47 to P53). Their account outranks any calculation (D-104).
- **The fiber broke in 10 of 11 flights, none by rough flying** (QA's forensics; every log replays bit for bit):
  - 8 by one contact fault: a node split inside a thin board or slab was thrown out at full penalty stiffness. That gave both "pulled apart" breaks (17–26 N over a 2 cm segment) and 5 of the 6 "worn through" ones (50–175 µm of wear in 1–5 ms);
  - 1 by the wear law: wood, bark and straw wear the coating on the severe abrasive band, so a line creeping at 3 mN on a batten wore through in 7 s;
  - 2 by the front-left prop after full-stick turn-arounds, possible only because the spool's peel energy was drawn from its heavy band.
  The fixes: `p7d-tether` 1.11d (the contact fault, the wear classes, the twig) and 1.11e (the peel band). The ruins' 2 cm masonry boxes are audited (`w9-village-physics` 2.15).
- **The sluggishness is the step budget, the sim's own work:** the laid line's queries over a 7,016-box ruin halved the flight's speed for 9 s; the wake grid's one-thread rebuild froze the flight for 76 ms to 1.4 s six times; and the player's Mono runs the core 3–5× slower than .NET. Two fixes with every result unchanged are on the line (7667e6e0). PLT-04 is still missed.
- **IL2CPP gets a trial.** The backend decision ("Mono for now … only if profiling shows the need") is met: the pilot approved the Windows IL2CPP module (installed 2026-11-14). The player stays on Mono until a trial build shows the same flights replay bit for bit on .NET and measures the step.
- **The plants move in the drone's own air** (`w16-plants-in-the-air`): the wind field and the props' wash, flexible plants with published properties that spring back after a short gust, the cut pieces seen, dust lifted from dry ground, and the drone dragged by grass and more by twigs and leaves. Calculated, not tuned; clip H's ending is the check.
- **The references:** the frames and comparison sheets extracted from the pilot's clips are deleted (regenerable); the pilot keeps the 23 original clips and photos and the measurements taken from them for now.
- **D-113's open checks are run:** model 100's full suite and benches showed nothing beyond the known (919 of 921; the two reds are 1.11d's twig and 2.6d's wire band).
