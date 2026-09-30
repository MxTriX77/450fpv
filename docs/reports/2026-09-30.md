# 2026-09-30 — status and resume point

## Late session: the gallery, the art pass, the feed correction (supersedes the resume point further down)

The pilot opened a new local session: "review everything… move on to the gallery". They are near their usage limit, so the session was wrapped up with everything committed and pushed. **Nothing below is merged:** three PRs are open and each is waiting on the checks it lists.

**Review of `main` at the start:** fully green, on AC power:
- `dotnet build`: 0 warnings;
- map, worldquery and LOD selftests: ALL PASS;
- golden, Debug and Release: ALL PASS;
- `export.py`: 0 problems, and it rebuilt nothing;
- validator: OK.

The one hygiene gap was that the feed's 10 Godot `.uid` sidecars had never been committed. They now are (8178809).

**Pilot answers and corrections, all recorded in `pilot-requirements.md`:**
- **PR-9:** narrow fly-ins are sized to the drone. The cellar doorway goes to 0.70 m.
- **PR-10:** belts carry occasional bushes among the trees.
- **PR-11:** the "plain-coloured tiles" were the hard-edged surface rectangles of `sample_patch`. Maps meant for the pilot get organic, feathered outlines.
- **PR-4 correction:** the picture comes back from a dropout **clean**. Receiver text appears only on the blue screen.
- **Standing instruction:** building construction and damage are researched, never asked. The plaster question should not have been put to the pilot.

**Decisions:** D-017, the gallery built first with reserved stations, and `labels.json` kept out of the content hash.

### Open PRs, and what each needs before it merges

| PR | Branch | State | Before merge |
|---|---|---|---|
| [#33](https://github.com/MxTriX77/450fpv/pull/33) gallery (4.1) | `world/uat1-gallery` | 5 commits. Selftests, golden (unchanged), validator (65/65) and the label scenario all pass. Loads in 3.8 s, 347 fps at the start view. | The screenshot pass: every patch and built station at 10/50/200 m, the relief at low level, the belt at 1.5 m and 20 m, all looked at and fixed. Then tick 4.1, then a QA review. |
| [#31](https://github.com/MxTriX77/450fpv/pull/31) cellar doorway 0.70 m | `world/house-art-pass` | 2 commits. The golden re-record moved exactly the content hash and 3 wind cells at the cellar, traced to the doorway by isolation runs. | `--selftest lod`, and `--selftest worldquery` after the re-record. Then a QA review. |
| [#32](https://github.com/MxTriX77/450fpv/pull/32) feed: clean picture after a dropout | `techart/recovery-clean-picture` | 2 commits, build clean. | A `--video-field` sweep across a recovering blue-out and across a terminal loss, looked at frame by frame, plus `--selftest map`. Recipe: run once with `-- --video-seed 1 --video-field 1`, whose start-up line lists the dropout fields; then sweep from 5 fields before to 40 after. |

Merge order: #31 before #33. The gallery copies the house yard layout from `make_sample_patch.py`, so re-run the gallery generator after any house footprint change.

### Parked, not in a PR: the facade system (3.2 art pass)

`world/house-art-pass-wip` holds one commit (9975a2b) that **does not build**. It is branched from #31's tip.

**Root cause, found and proved:** `assetkit.Mesh.box` runs each box's longest side along V. Walls therefore turn their texture 90° depending on panel proportions, neighbours don't share texture, opposite faces are mirrored, and bricks and tiles lie on end. That is the chequerboard and the "upside-down" bricks, together with two other causes: `render_clay` and `adobe_clay` are close in hue, and the patch scale is close to the panel size.

**The fix, half-written:**
- UVs in asset space, upright and continuous (`box(..., upright=, uv2=)`);
- a `Facade` class that unrolls every wall onto one facade map;
- a numpy-painted RGBA damage mask: R damage depth, G soot, B dirt and streaks, A the red-clay base band;
- `facade.gdshader`: whitewash over render over the adobe core;
- the house rewiring script, parked at `tools/blender/wip/edit_house1.py`.

**Plaster, settled by research:** weathered lime whitewash (#c4bfb1) over clay render (#97754f) over adobe (#7d6146), with a red-clay base band 0.42 m high. About 30 % of the whitewash is gone, round the openings, corners, foot and wall head, plus blast patches. Soot sits above the burnt west-room windows.

**Resume steps:**
1. Wire `house_adobe`, `cellar_head` and `shed_plank` to the new `wall()`.
2. Add `house_adobe_facade.tres` and `cellar_head_facade.tres`. The measured linear texture means are:
   - `rough_plaster_broken`: (0.3247, 0.2142, 0.1035)
   - `dirt`: (0.1253, 0.0853, 0.0487)
   - `red_brick_plaster_patch_02`: (0.2366, 0.126, 0.0627)
   
   The cellar facade is lime render over a brick core.
3. Give the mask PNG an import sidecar with `compress/mode=2`, `mipmaps/generate=true`, `process/fix_alpha_border=false` (essential, since A carries data) and `compress/normal_map=2`. List the masks in `team_files` in `tools/assets/cc0_manifest.json`, update `CREDITS.md`, and delete `render_clay.tres` if nothing uses it any more.
4. Rebuild and export, and confirm the catalog is unchanged, so the golden must not move.
5. Give the rubble look brick, tile and plaster colours, and drape the tarp. The tarp is what read as the "solid blue plane".
6. As a separate physical commit with its own golden audit: an irregular batten grid, broken tile courses, and window and door frames with collision.

### Resume here, in order

1. **Close the three PRs:** run the checks each one lists, look at the screenshots and frames, run a QA review, then merge (#31 → #32 → #33).
2. **Finish the 3.2 art pass** from `world/house-art-pass-wip` (steps above), then tick 3.2.
3. **Propose a gap-model change before 3.4.** A catalog gap is an upright rectangle, so broken windows (PR-5), the house's roof bay and gable breach, and every opening in the destroyed five-storey block are invisible to the sim. Physics-engineer and world-artist. Jagged openings and rebar wait for it.
4. **3.3 vehicles → 3.4 block → 3.5 the rest.** Each asset goes into its reserved gallery station by re-running `make_uat1_gallery.py`. Then 4.2 (busiest-view fps), then 5.x reviews, then UAT-1.
5. **In parallel with the objects:**
   - the TX12 input layer (D-016, game-developer; it needs an OpenSpec change first);
   - updating the `build-flight-model` proposal with PR-1, PR-3, PR-6, PR-9 and the input-timing contract. The proposal is now pushed as `physics/build-flight-model`.

### Small findings, for later

- **The F3 perf overlay probably goes through the analog feed:** it shares the feed's canvas layer 1 and is drawn before it. Tech-artist.
- **The gallery labels sit bottom centre, between the OSD's bottom rows.** Move them up if they cover OSD text.
- **Filler-contract lines print as "selftest worldquery:" inside `--selftest map`.** Cosmetic.
- **`backup/study-before-rewrite`** is a stale local branch: docs only, and superseded by the archived study. Safe to delete. **`chore/report-resume-point`** is fully merged.

---

**Evening update.** The morning pause did not last: the pilot came back the same day, locally rather than in the cloud, and asked for an analog video feed to demo with. That became PRs #27–#29 (D-013, D-014), plus pilot requirements PR-1 to PR-8 in `docs/reference-notes/pilot-requirements.md`. The pilot then said "continue the work now, in the necessary order", and the session hit its usage limit just as the world-artist started the 3.2 art pass. That agent had done nothing yet: `world/house-art-pass` has no commits and nothing was lost.

**Resume here:** re-dispatch world-artist on the 3.2 art pass (see below). The order the orchestrator set:

1. `build-uat1-parts` 3.2 art pass: fix the defects first (chequerboard walls, upside-down bricks), then look (plaster and whitewash, shelled roof, empty window frame, a yard with feathered edges, rubble coloured like brick). **Jagged openings and rebar (PR-5) are held back.** They need the gap model changed first, because a catalog gap is only an upright rectangle. That change also covers the roof bay and the gable breach.
2. Then 3.3 → 3.5, the gallery (4.x) and the reviews (5.x), up to UAT-1.
3. **TX12 (the pilot's question):** build the input layer first and physics second, so flight feel is tuned with the transmitter in hand. It's game-developer work, independently checkable with an axis display. The input spec must fix how HID samples are timestamped and fed into the 1 kHz step. It runs alongside the flight-model spec, not ahead of the object work.

Fly what exists:

```
& $env:GODOT --path game -- --map sample_patch
```

## Where each role stands

| Role | State |
|---|---|
| World Artist | Mid-`build-uat1-parts`. Sections 1 and 2 are complete, and so is 3.1 (the tree belt). 3.2 (house and yard) is **built and green but unticked**, because the art pass is missing. That pass is next. |
| Physics Engineer | Signed off `burnt_field` and the terrain-holes design (2.2). The `build-flight-model` proposal is **drafted, local-only and unpushed**: 10 commits on `physics/build-flight-model`, in worktree `.claude/worktrees/agent-a39874c0e3c75c642`. It was written at 12:20, before PR-1 to PR-8, so it still lacks the throttle-punch shake (PR-1), wind moments and yaw swerve (PR-3), wire strikes by diameter (PR-6), the camera-shake signal to the feed, and the input-timing contract. Update it before review. Task 5.1 is still ahead. |
| Tech Artist | Shipped the analog feed spike (#27), staged loss sequences with the in-signal OSD (#28), and the footage-exact OSD (#29). This work carries **D-013 debt**: no OpenSpec change, no scenarios, no tests, no QA, and no trustworthy performance figure. The last clean measurement was about 1.0 ms at 1920×1055. The sandbox fog and environment work is still waiting. |
| Game Developer | Not yet engaged. Next: the TX12 input layer, and the wireframes gate. |
| QA Engineer | Task 1.4 is open (the corner-lookup check in `--selftest worldquery`). Owed: a performance measurement of the feed on a quiet machine on AC. Task 5.2 is the pre-pilot review. |

## `build-uat1-parts`: 9 of 14 ticked

Done: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 3.1.
Open: **1.4** (QA), **3.2** (built, unticked — art pass), **3.3** vehicles, **3.4** the five-storey block, **3.5** the rest, **4.1/4.2** the review map, **5.1–5.4** reviews and UAT-1.

## Resume here: 3.2's art pass

The structures, collision, gaps, cellar hole and golden are all finished and verified. What is missing is surface work. In the order to fix:

1. **The walls read as a chequerboard** — a defect, not a style choice. Three known causes: the patch field's scale is too close to the panel size; `render_clay` and `adobe_clay` are too close in hue; each panel takes its own UV offset so neighbouring panels do not share continuous texture. Fixing this changes how the house reads more than anything else on this list.
2. **The yard is a hard-edged axis-aligned rectangle** of orange litter cut out of green meadow. From the air it reads as a sticker. Needs feathering.
3. **The rubble ring is near-white**, reading as gravel or snow rather than brick and tile.
4. **The batten grid is too regular** for a shelled roof.
5. From the pilot's own look: the walls are **bare adobe where these houses are lime-plastered and whitewashed**, the **roof tiles are clean and undamaged**, and the **window is a solid blue plane** rather than an empty frame.

## Known rough, outside 3.2

- **Distance washes out.** At 200 m a real посадка is a dark band; ours dissolves into pale fog. Cause is the sandbox fog plus the `belt_bare` albedo — tech-artist's environment, not the assets. The far tree impostor was deliberately *not* darkened to compensate, because matching the near LOD's tone is what keeps the 200 m switch invisible.
- **Micro-detail litter reads as flat chips** under about 4 m on `yard_litter`. It is the physics' own litter drawn honestly; cheapest fix is a leaf silhouette for litter instances.
- **Surfaces are darker near than far** (normal and AO resolving close up, mipping flat at distance). Physically defensible; the pilot will see ground darken as they descend.

## Two map-format findings for the physics engineer

- **A catalog gap is an upright rectangle (yaw only)**, so two real openings on the house carry no gap entry: the stripped roof bay (sloping plane) and the breach over the broken east gable (a triangle bounded by the roof frame). A pilot can fly through both; **the sim does not know they exist.** This matters for a simulator whose whole point is threading gaps.
- **`wind_porosity` is one scalar per asset**, so the house's solid walls and its open roof share one figure (measured 0.11 over the long face). "Solid below, open above" cannot be expressed.

## Open questions only the pilot can answer

1. **Belt density.** V1 gives trunks 1–3 m apart; the belt is built at 3 m, the open end of that range, partly for frame budget. Are real посадки denser — closer to a wall you cannot see through at low level? Our skyline is also more even than a real belt's. One-line change (`BELT_STEP_M`, `BELT_ROWS`, species mix).
2. **Plaster.** In the villages flown over, how many houses were whitewashed lime plaster versus bare adobe or brick? And how did the damaged ones fail — plaster shot off in patches, or whole walls collapsed? This repeats across every house in a 4 km map.
3. **Cellar doorway width.** Built at 0.78 m; W4 says "about 0.7 × 1.6 m". At 0.78 m a 0.706 m prop diagonal fits with 37 mm each side. At a true 0.70 m it would not fit diagonally at all — which may be exactly the point.

## Housekeeping

- Measured performance is only meaningful on AC power. On battery, Vulkan exposes only the integrated Radeon 680M and even the empty sandbox drops to 177 fps. Figures quoted in PRs #24 and #25 are indicative, not budget numbers.
- Dead code noticed and deliberately not touched: an unused `var samples = new List<XZ>();` in `HoleSelfTest.FillerContract`'s F-4 block.
- `.claude/worktrees/` appears untracked in the working tree; it comes from cloud-session isolation and is not project content.

## Work moved to cloud sessions (2026-09-30)

*Superseded the same evening: work continued locally, and PRs #27–#29 were verified on the pilot's machine. The contract below still applies to anything that is ever done in the cloud.*

From this point the pilot has moved all work, the orchestrator included, to Claude Code cloud sessions, to spend cloud credits rather than local session tokens. They will move back to a local session later and ask for a review of everything done in between.

**The cloud environment almost certainly cannot run this project's verification.** It needs Godot 4.7.2 .NET, Blender 5.2, git-lfs and a GPU that can open a window. Any cloud session should establish what it actually has before promising a check it cannot run, and should say plainly in every handoff which gates it could not run, rather than reporting an unverified result as done.

### Re-verification contract for the return to a local session

Nothing done in the cloud counts as verified until these pass on the pilot's machine. Run every one, do not take a handoff's word for any of them:

```
$env:GODOT = "C:\Users\maksy\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
dotnet build game/Fpv450.sln                                   # clean, 0 warnings
& $env:GODOT --headless --path game --import                   # after any asset change
& $env:GODOT --headless --path game -- --map sample_patch --selftest map      # ALL PASS, stem parity 0.0000 mm
& $env:GODOT --headless --path game -- --selftest worldquery   # ALL PASS
& $env:GODOT --path game --resolution 1280x720 -- --map sample_patch --selftest lod   # ALL PASS
dotnet run -c Debug --project tools/worldbench -- --golden     # ALL PASS
dotnet run -c Release --project tools/worldbench -- --golden   # ALL PASS
python tools/blender/export.py                                 # 0 problems
python tools/map/validate_map.py game/maps/sample_patch
python tools/map/test_validate_map.py
```

Then, beyond the commands:

- **Audit every golden re-record case by case.** No case's stored inputs may change; no terrain, `SampleGround`, `MicroDetailNear` or `flat:<surface>` case may move; every case that does move needs a cause proved by experiment, not asserted. A re-record that quietly blesses drift is the worst failure available here, and it is invisible if nobody checks.
- **Look at every new asset.** Screenshots at 10 m, 50 m and 200 m, read and judged against the reference notes. The house's chequerboard walls passed every automated check; only looking caught it.
- **Re-measure performance on AC power.** Battery figures are meaningless: Vulkan exposes only the integrated GPU and the empty sandbox alone drops to 177 fps.
- **Check the commits themselves:** subject line only, no body, no trailers, no Claude co-author line, and the correct role identity from `docs/team.md`. Nothing from `reference/`, `.godot/`, logs or builds.
