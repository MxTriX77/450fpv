# Decision log

- Status is Proposed, Accepted or Superseded. To change a decision, add a new entry that supersedes the old one, and never edit an old entry.
- "Old D-0xx" points to the Godot log, [legacy/godot-decisions.md](legacy/godot-decisions.md).
- Decisions marked **(verify at phase 0)** depend on facts the next session must check before building on them.

## D-001 Engine: Unity 6 LTS, URP, C# · Accepted (verify at phase 0)
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

