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
