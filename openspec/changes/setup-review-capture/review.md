# Review: setup-review-capture (task 2.1)

**Reviewer:** qa-engineer · **Branch:** `unity/techart/setup-review-capture` at `c34d7c7` (five commits on `0e2e6a3` of the asset-pipeline branch) · **Requirements cited:** AST-12, AST-04, AST-05, REAL-03 groundwork

Everything below was run by QA in its own worktree, Unity 6000.3.26f1 with graphics (Unity Hub running, no editor holding the project), Blender 5.2.2, pwsh 7, on AC power (not a benchmark). The Library was cold for the first run.

## Scenarios

| Scenario | Result | Evidence |
|---|---|---|
| K01 sheet is complete | **PASS** | exit 0; `review-sheets/K01/K01_whole_day.png` and `K01_whole_overcast.png`, 4096 × 4096; 13 rendered tiles plus the legend per light, 26 rendered tiles in all; every tile labelled with variant, light, view and distance |
| Distances are measured from the asset | **PASS** | from the logged camera positions, straight-line distance to the bounds centre (0, 0.0325, 0): 1.5001 / 9.9929 / 49.9946 / 199.9977 m, errors 0.008 / 0.071 / 0.011 / 0.001 % (the 0.07 % is the two-decimal rounding of the logged position) |
| Overlay matches the metadata | **PASS** | one green box outline (12 edges) hugging the brick to about a pixel at 2× zoom (about 3 mm at 2.46 m); 990 pure-green pixels, 0 magenta / cyan / red, matching one box and no gaps, wind volumes or snags; legend names COLLISION (1): BOX, GAPS (0), WIND VOLUMES (0): NONE, SNAG (0) with colour swatches |
| Unknown id fails | **PASS** | `-asset NOPE` → exit 1 after 19.6 s, `[Review] FAILED: NOPE is not an asset of assets-src/catalog.json`; `review-sheets/` unchanged (the two K01 files kept their earlier timestamps, no `NOPE/` folder) |
| End to end with sheet | **PASS** | `pwsh tools/build_asset.ps1 K01` → exit 0 through [1/3] Blender, [2/3] import, [3/3] sheet, 47 s warm; prefab mtime 02:18:58, sheets 02:19:22 and 02:19:23 |

## Requirements

| ID | Status | Notes |
|---|---|---|
| AST-12 | met | 1.5 / 10 / 50 / 200 m, 8-view turntable, collision overlay, day and overcast, per variant. The side-by-side against the reference is out of scope here (REAL-03, phase 1) |
| Spec "Sheet content": *every view SHALL include a 1.8 m human scale bar* | **partly met** | the bar is in the 10 m, 50 m, turntable and overlay tiles. **It is not in the 1.5 m pilot-height tile**: with a ground-level bounds centre the 1.5 m straight-line camera sits at (−0.22, 1.50, −0.22), looking 78° down, and the bar 1 m outside the bounds at bearing 112.5° is outside the 60° frame (day shows only its 1.8 m shadow across the top of the tile; overcast shows nothing). At 200 m the bar is rendered but sub-pixel (0.2 px wide, 8 px tall), which is physically right and informative, like the culled brick |
| AST-04 | met for what the sheet can show | the brick matches its metadata box (0.250 × 0.065 × 0.120 m) within about 3 mm in the overlay; depth-corrected against the bar in turntable 1/8 it reads 2.6–2.8 bands long (2.5 expected, within the measurement) |
| AST-05 | met for the overlay | collision read from `.assetmeta.json`, not from Unity colliders (design decision 5). The capsule probe is phase-1 work |
| Spec "Location and failure" | met | PNGs under the git-ignored `review-sheets/<ID>/`; non-zero exit on an unknown id before anything is written |
| Spec "Part of the one command" | met | step [3/3] in `build_asset.ps1`, same fail-fast style, own log `unity/Logs/review_K01.log` |

## Commands and output

```
& $UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Review.Capture -asset K01 -logFile - | Out-Host
[Review] K01: 1 variant(s), lights day/overcast, GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12), quality High (URP-High), LOD BIAS 1, colour space Linear
[Review] light day: SUN ELEV 45°, AZ 135°, I 1.00, SHADOWS, horizontal illuminance 0.976
[Review] rendering through RenderPipeline.SubmitRenderRequest, 1024x1024, MSAA 1
[Review] tile 1.5 M PILOT HEIGHT AZ 225: camera (-0.22, 1.50, -0.22), 1.500 m to the look-at point, lower-left patch 120/120/119
[Review] tile 10 M SLANT 20° DOWN AZ 225: camera (-6.64, 3.45, -6.64), 10.000 m to the look-at point, lower-left patch 120/120/119
[Review] tile 50 M SLANT 20° DOWN AZ 225: camera (-33.22, 17.13, -33.22), 50.000 m to the look-at point, lower-left patch 120/120/119
[Review] tile 200 M SLANT 20° DOWN AZ 225: camera (-132.89, 68.44, -132.89), 200.000 m to the look-at point, culled, lower-left patch 120/120/119
[Review] tile TURNTABLE 1/8 AZ 0 2.46 M … TURNTABLE 8/8 AZ 315 2.46 M: 2.463 m to the look-at point
[Review] tile OVERLAY 2.46 M 20° DOWN AZ 225: camera (-1.64, 0.87, -1.64), 2.463 m to the look-at point, lower-left patch 120/120/119
[Review] K01_whole day: review-sheets/K01/K01_whole_day.png (4096x4096, 0.6 MB)
[Review] light overcast: NO SUN, horizontal illuminance 0.977
… the same 13 tiles …
[Review] K01_whole overcast: review-sheets/K01/K01_whole_overcast.png (4096x4096, 0.4 MB)
Exiting batchmode successfully now!
EXIT CODE: 0   ELAPSED: 00:03:13 (cold Library)

python check_distances.py capture_K01.log 0 0.0325 0
PASS 1.5 M PILOT HEIGHT AZ 225   cam=(-0.220, 1.500, -0.220)    d_centre=1.5001   nominal=1.5   err=0.008%
PASS 10 M SLANT 20° DOWN AZ 225  cam=(-6.640, 3.450, -6.640)    d_centre=9.9929   nominal=10.0  err=0.071%
PASS 50 M SLANT 20° DOWN AZ 225  cam=(-33.220, 17.130, -33.220) d_centre=49.9946  nominal=50.0  err=0.011%
PASS 200 M SLANT 20° DOWN AZ 225 cam=(-132.890, 68.440,-132.890) d_centre=199.9977 nominal=200.0 err=0.001%
26 tile lines; distance scenario PASS

python crop_sheet.py review-sheets/K01/K01_whole_day.png crops      (and _overcast)
  tiles 0–12 rendered and labelled (bright label pixels 4095–5922 each), tile 13 legend, tiles 14–15 empty panel
  labelled rendered tiles: 14 (13 + legend)   overlay pixels: green=990 magenta=0 cyan=0 red=0   (both sheets)

bar band count along the bar in TURNTABLE 3/8 (full res): 18 DLDLDLDLDLDLDLDLDL

& $UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Review.Capture -asset NOPE -logFile -
[Review] FAILED: NOPE is not an asset of assets-src/catalog.json
EXIT CODE: 1   ELAPSED: 00:00:19
review-sheets after: K01_whole_day.png 02:14:59, K01_whole_overcast.png 02:15:01 (unchanged)

pwsh tools/build_asset.ps1 K01
build_asset: [1/3] Blender build of K01
K01_whole: unity\Assets\_Project\Art\Models\debris\K01\K01_whole.fbx  size [0.25, 0.065, 0.12] m  tris 44 / 12  metadata written
build_asset: [2/3] Unity import of K01
[CatalogImporter] K01_whole: Assets/_Project/Prefabs/debris/K01_whole.prefab, 2 LODs, 1 colliders, materials brick -> brick_red, LOD heights 0.0722 / 0.0018 (size 0.25 m, fov 60 deg)
build_asset: [3/3] Review sheet of K01
[Review] … 26 tiles as above …
build_asset: OK (K01)
EXIT CODE: 0   ELAPSED: 00:00:47
prefab: 02:18:58   fbx: 02:18:44   sheets: 02:19:22 (day), 02:19:23 (overcast)

git status --porcelain --untracked-files=all      → empty
SHA-256 before and after the end-to-end run, identical: URP-High/Medium/Low(.asset, -Renderer.asset), K01_whole.fbx, K01_whole.assetmeta.json, K01_whole.prefab
m_ShadowDistance: 50 in all three URP assets (the capture sets 12 m for the stage and restores it; the asset is never dirtied)

openspec validate setup-review-capture --strict   → Change 'setup-review-capture' is valid
python tools/hygiene/precommit.py --tree HEAD     → 322 files scanned, 0 findings
```

## Visual notes (knowledge §14.1: look at it)

- **Layout** is as designed: row 1 the four distances, rows 2–3 the turntable, row 4 the overlay, the legend and two empty panels, the GPU name bottom right. Labels are 15 × 21 px glyphs, crisp and legible at full size; 200 m carries **CULLED** in amber on both lights.
- **The brick** lies flat on its bed face, long axis along X, as a loose brick should. Proportions match the metadata box (overlay) and the 10 cm bands (turntable). Bevel reads at 1.5 m. Day shows a short sun shadow consistent with a 45° sun on a 6.5 cm brick.
- **The scale bar**: 18 bands, readable at 10 m, a striped line about 32 px tall at 50 m, invisible at 200 m. **Absent from the 1.5 m tile** (see Requirements).
- **The overlay** sits exactly on the brick, both lights.
- **Mortar joints: confirmed.** In both 1.5 m tiles the brick's top face carries two light mortar lines, one across the face near one end and one meeting it mid-face in a T: the `brick_red` wall texture (tiled at 1.4 m) mapped onto a lone 0.25 m brick. A pilot would call it wrong: a loose brick has no joints on its bed face. This is the asset's material, not the capture; the sheet did its job by showing it. Belongs to the asset-artist (setup-asset-pipeline / phase-1 materials).
- **Turntable on a tiny asset**: the frame is sized to the 1.8 m bar, so the brick is about 100 px wide in each view. By design (bounds plus bar) and fine for a brick, but for anything much smaller than the bar the turntable shows little detail. Suggestion, not a defect.
- Day and overcast read the same brightness on the ground (lower-left patch 120 vs 122), so the equal-illuminance profiles work.

## Hygiene

- Files: only `Editor/Review/*` (+ `.meta`), `Settings/Review/*` (+ `.meta`), `tools/build_asset.ps1`, `tasks.md`. Nothing from `reference/`, `Library/`, `Temp/`, `Logs/`; no media (sheets are ignored under `review-sheets/`).
- Commits: five, all `Tech Artist <techart@450fpv.local>`, subject only, no body, no co-author line. **One subject is 74 characters**, over the 72 of PRC-03 / workflow.md: `fix(review): fit shadows to the stage, move the bar off the turntable grid`.
- OPSEC grep of the diff (footage names, extensions, messengers, dates, `reference/`): nothing. Hygiene tree scan: 0 findings.
- `openspec validate --strict`: valid. Tasks: 1.1 ticked by the tech-artist; 2.1 left unticked by QA (see verdict).

## Changes requested

1. **The 1.5 m pilot-height view must include the scale bar** (spec "Sheet content": *every view SHALL include a 1.8 m human scale bar*). For a ground-level asset the 1.5 m straight-line camera looks 78° down and the bar 1 m beside the bounds is out of frame. Either the capture puts the bar inside the pilot shot (for example the bar on the camera's side of the asset, off the view axis, or at a bearing chosen per shot), or the orchestrator amends the spec and design to exempt the pilot-height view when the bar cannot share its frame, and says so in the tile label. The Distance scenario as written (straight-line 1.5 m to the centre) is what forces the top-down view on small assets; that conflict is a spec decision, not QA's.
2. **Reword the 74-character subject** to 72 or fewer before merge (the branch is unmerged, so a rewrite is fine).

Everything else passes. Re-review after the fix needs only the K01 capture (both 1.5 m tiles show the bar, or the amended label), the commit list, and `git status`.

**Verdict: CHANGES REQUESTED**

## Re-review (task 2.1, after task 1.2)

**Reviewer:** qa-engineer, a fresh agent reviewing independently of the first pass · **Branch:** `unity/techart/setup-review-capture` at `0462dac` (`9bca297`, the fix commits, plus a merge of `unity/main` `bd9e95e`) · Unity 6000.3.26f1 with graphics, Unity Hub running, no editor on the project, Blender 5.2.2, pwsh 7.6, AC power ("Balanced"; captures are not benchmarks). A fresh worktree with a cold Library.

### The two requested changes

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | The 1.5 m pilot-height view includes the scale bar | **PASS** | Both 1.5 m tiles show the bar's lower bands at the top centre of the frame, above the brick and clear of it, and the label reads `1.5 M PILOT HEIGHT AZ 225  BAR LOWER BANDS` (design decision 2 as amended). Measured on the tile's centre column: light/dark/light/dark bands at rows 105–201 (day) and 103–207 (overcast) below the 70 px label strip, with a fifth band greyed under the strip; the brick at rows 435–589; a 230 px gap between the bar's base and the brick. Predicted from the code's constants before the run: bar base at (0.431, 0, 0.431), 0.61 m past the bounds centre on the far side, base at row 196, brick's far top edge at row 458, 4.8 bands in frame, bar top behind the camera plane (so the label must appear): every number within a few pixels of what rendered |
| 2 | The 74-character subject | **PASS** | Rewritten to `fix(review): fit stage shadows, move the bar off the turntable grid` (67 characters), author and committer still Tech Artist. The change's nine commits are 43–67 characters, subject only, no body, no co-author line |

### Scenarios, re-run

| Scenario | Result | Evidence |
|---|---|---|
| K01 sheet is complete | **PASS** | exit 0 (2:59 cold Library, 0:27 warm); both sheets 4096 × 4096; 13 rendered tiles plus the legend per light, every tile labelled (label-pixel count 1548–6786 on tiles 0–13, 0 on the two empty panels); 26 rendered tiles in all |
| Distances are measured from the asset | **PASS** | from the logged camera positions to the bounds centre (0, 0.0325, 0): 1.5001 / 9.9929 / 49.9946 / 199.9977 m, errors 0.008 / 0.071 / 0.011 / 0.001 %. **All 26 camera positions are identical to the first review's** (moved 0.000 m at 1.5, 10, 50, 200 m, the turntable and the overlay): the fix changes nothing but the bar's place in the pilot tile |
| Overlay matches the metadata | **PASS** | 990 pure-green pixels, 0 magenta / cyan / red in both sheets, as in the first review; looked at: one box hugging the textured brick, the legend as before |
| Unknown id fails | not re-run | task 1.2 touches neither the catalog lookup nor the exit path; the first review's PASS stands |
| End to end with sheet | **PASS** | `pwsh tools/build_asset.ps1 K01` exit 0 in 41 s through [1/3] Blender, [2/3] import, [3/3] sheet; prefab 02:46:26.683, sheets 02:46:46.111 and 02:46:47.672; SHA-256 of the FBX, the metadata, the prefab and the three URP assets identical before and after; `m_ShadowDistance: 50` in all three; `git status --porcelain --untracked-files=all` empty |

### What the pilot tiles show

- **Day:** the bar's base stands at the top centre, 0.6 m beyond the brick; two light and two dark bands read cleanly, the next dark band is dimmed under the label strip. The brick lies at the frame centre with its own short shadow. The bar's 1.8 m shadow runs sideways from its base along its own row, nowhere near the brick. Nothing covers the brick.
- **Overcast:** the same geometry without shadows; the bands read as white / dark grey on the 0.18 ground.
- The bar's lower bands are what a pilot at 1.5 m would see beside a brick: the amended decision 2 is met as written, and the label says the top is cut.
- Still there from the first review, for the asset-artist, not this change: the mortar joints on the brick's bed face (the wall texture tiled onto a lone brick).

### An environment finding, proved, not a defect of the change

The first capture in this worktree rendered **every tile without the brick** (bar, shadow and overlay box on bare ground), and `build_asset.ps1` failed at step 2 with `red_brick_diff_1k.png did not import as a texture`. Cause: the harness-made worktree held **Git LFS pointer files** (130–132 bytes) for the four red_brick PNGs and both FBX files, although all six objects were in the shared `.git/lfs/objects` store and the LFS filter is configured globally and in the repo. Unity's log says `Couldn't read file …/K01_whole.fbx` and `File could not be read` for the textures, so the prefab had no mesh and the material no texture. `git lfs checkout` (local store, no network) replaced the pointers; `git lfs ls-files` then marks all six `*`, and both runs above are on the real content. Blender's rebuilt FBX hashes to exactly the LFS object (`aa63fda7…`), so the export is byte-deterministic and the "modified" FBX git reported after the failed run was only the un-smudged index.

Two suggestions from it (handoff, not this branch): a line in `docs/environment.md` that a new worktree may hold LFS pointers, checked with `git lfs ls-files` and fixed with `git lfs checkout` before any Unity work; and a loud failure in the capture when the asset's renderers have no mesh at the 1.5 m shot (it is not culled there), instead of a blank stage that exits 0.

### Hygiene

- `git log --format='%h %an | %s' 0e2e6a3..HEAD --no-merges`: 18 commits, identities Tech Artist, QA Engineer, Orchestrator, Asset Artist, Physics Engineer; no bodies, no trailers. The change's own nine (`cfcba96`, `152c086`, `d56b65f`, `a6294a9`, `2cfbbc7`, `b6c0f4b`, `3dc48f9`, `3cdb167`, `9bca297`) are all ≤ 72 characters. One subject in the range is 84 characters: `e082dbf feat(blender): enforce the metadata conventions and the closed physics material list` (Asset Artist). It comes from `unity/main` through the merge (setup-asset-pipeline, merged in #44 and archived), is not this branch's commit and is outside its rewrite; `enforce-commit-format` is where that gets caught from now on.
- Files on the branch over `unity/main`: `Editor/Review/*` (+ `.meta`), `Settings/Review/*` (+ `.meta`), `tools/build_asset.ps1`, the change folder. Nothing from `reference/`, `Library/`, `Temp/`, `Logs/`; no PNG; `review-sheets/` and `unity/Logs/` stay ignored (status empty after every run).
- `python tools/hygiene/precommit.py --tree HEAD`: 338 files scanned, 0 findings. `openspec validate setup-review-capture --strict`: valid. OPSEC grep of the branch diff (footage names, extensions, messengers, dates, `reference/`): only the first review's own hygiene sentence.

### Commands and output

```
git switch unity/techart/setup-review-capture            → 0462dac, up to date with origin
Get-Process "Unity Hub"                                   → running (6 processes); no Unity editor, no unity/Temp/UnityLockfile
(Get-CimInstance Win32_Battery).BatteryStatus             → 2 (AC); powercfg: Balanced

& $UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Review.Capture -asset K01 -logFile - | Out-Host
  run 1 (cold Library, LFS pointers on disk): EXIT CODE 0, 00:02:59, 26 tiles, brick absent everywhere
  git lfs ls-files → 6 files, 5 pointers ("-") on disk; git lfs checkout → "Checking out LFS objects: 100% (6/6), 11 MB"
  run 2 (real content):                       EXIT CODE 0, 00:00:27
[Review] K01: 1 variant(s), lights day/overcast, GPU NVIDIA GeForce RTX 4050 Laptop GPU (Direct3D12), quality High (URP-High), LOD BIAS 1, colour space Linear
[Review] light day: SUN ELEV 45°, AZ 135°, I 1.00, SHADOWS, horizontal illuminance 0.976
[Review] rendering through RenderPipeline.SubmitRenderRequest, 1024x1024, MSAA 1
[Review] tile 1.5 M PILOT HEIGHT AZ 225  BAR LOWER BANDS: camera (-0.22, 1.50, -0.22), 1.500 m to the look-at point, lower-left patch 120/120/119
[Review] tile 10 M SLANT 20° DOWN AZ 225: camera (-6.64, 3.45, -6.64), 10.000 m …
[Review] tile 50 M SLANT 20° DOWN AZ 225: camera (-33.22, 17.13, -33.22), 50.000 m …
[Review] tile 200 M SLANT 20° DOWN AZ 225: camera (-132.89, 68.44, -132.89), 200.000 m …, culled
[Review] tile TURNTABLE 1/8 … 8/8: 2.463 m to the look-at point (same positions as the first review)
[Review] tile OVERLAY 2.46 M 20° DOWN AZ 225: camera (-1.64, 0.87, -1.64), 2.463 m …
[Review] K01_whole day: review-sheets/K01/K01_whole_day.png (4096x4096, 0.6 MB)
[Review] light overcast: NO SUN, horizontal illuminance 0.977
[Review] tile 1.5 M PILOT HEIGHT AZ 225  BAR LOWER BANDS: camera (-0.22, 1.50, -0.22), 1.500 m …, lower-left patch 122/121/120
… the same 12 tiles …
[Review] K01_whole overcast: review-sheets/K01/K01_whole_overcast.png (4096x4096, 0.4 MB)
Exiting batchmode successfully now!

python check_distances.py capture_K01_run2.log 0 0.0325 0
PASS 1.5 M PILOT HEIGHT AZ 225  BAR LOWER BANDS  cam=(-0.220, 1.500, -0.220)    d_centre=1.5001   err=0.008% | vs first review: moved 0.000 m SAME
PASS 10 M SLANT 20° DOWN AZ 225                  cam=(-6.640, 3.450, -6.640)    d_centre=9.9929   err=0.071% | SAME
PASS 50 M SLANT 20° DOWN AZ 225                  cam=(-33.220, 17.130, -33.220) d_centre=49.9946  err=0.011% | SAME
PASS 200 M SLANT 20° DOWN AZ 225                 cam=(-132.890, 68.440, -132.890) d_centre=199.9977 err=0.001% | SAME
OVERLAY 2.46 M 20° DOWN AZ 225                   cam=(-1.640, 0.870, -1.640) | SAME
26 tile lines; PASS (0 failures)

python crop_sheet.py sheets_run2/K01_whole_day.png crops_run2      (and _overcast)
  labelled tiles: 14 (13 + legend)   overlay pixels: green=990 magenta=0 cyan=0 red=0   (both sheets)
  pilot tile, centre column (day):      D 58-104 (strip to 70)  L 105-140  D 141-178  L 179-201  …  brick 460-558
  pilot tile, centre column (overcast): D 58-102                L 103-142  D 143-176  L 177-207  …  brick 460-559
  brick rows over the whole tile: 435..589 (both lights)

python predict_pilot_bar.py   (from the code's constants, before the run)
  camera (-0.220, 1.500, -0.220); bar base (0.431, 0, 0.431), 0.610 m past the centre
  bar base -> row 196.5; brick far top edge -> row 458.4; bar top 1.8 m -> depth -0.103 m (behind the camera)
  bar leaves the frame top at 0.476 m: 4.8 bands in frame

pwsh tools/build_asset.ps1 K01     ($env:UNITY / $env:BLENDER set to the pinned paths)
build_asset: [1/3] Blender build of K01
K01_whole: unity\Assets\_Project\Art\Models\debris\K01\K01_whole.fbx  size [0.25, 0.065, 0.12] m  tris 44 / 12  metadata written
build_asset: [2/3] Unity import of K01
[CatalogImporter] K01_whole: Assets/_Project/Prefabs/debris/K01_whole.prefab, 2 LODs, 1 colliders, materials brick -> brick_red, LOD heights 0.0722 / 0.0018 (size 0.25 m, fov 60 deg)
build_asset: [3/3] Review sheet of K01
[Review] … the 26 tiles as above, both 1.5 m tiles labelled BAR LOWER BANDS …
build_asset: OK (K01)
EXIT CODE: 0   ELAPSED: 00:00:41
prefab 02:46:26.683; sheets 02:46:46.111 (day), 02:46:47.672 (overcast)
SHA-256 before/after: SAME K01_whole.fbx, K01_whole.assetmeta.json, K01_whole.prefab, URP-High/Medium/Low.asset
m_ShadowDistance: 50 in URP-High, URP-Medium, URP-Low
git status --porcelain --untracked-files=all      → empty

git log --format='%h %an | %s' 0e2e6a3..HEAD --no-merges | subject lengths
 53 9bca297 Tech Artist | docs(openspec): tick task 1.2 of setup-review-capture
 60 3cdb167 Tech Artist | fix(review): keep the scale bar inside the pilot-height view
 58 3dc48f9 Orchestrator | docs(openspec): put the scale bar in the pilot-height view
 43 b6c0f4b QA Engineer | docs(openspec): review setup-review-capture
 53 2cfbbc7 Tech Artist | docs(openspec): tick task 1.1 of setup-review-capture
 58 a6294a9 Tech Artist | chore(tools): add the review sheet step to build_asset.ps1
 67 d56b65f Tech Artist | fix(review): fit stage shadows, move the bar off the turntable grid
 58 152c086 Tech Artist | feat(review): add the batchmode asset review sheet capture
 53 cfcba96 Tech Artist | feat(review): add the day and overcast light profiles
 (the other nine are unity/main's; the one over 72 is e082dbf, 84, from setup-asset-pipeline)
 BODY=[] for all 18

python tools/hygiene/precommit.py --tree HEAD     → 338 files scanned, 0 findings
openspec validate setup-review-capture --strict   → Change 'setup-review-capture' is valid
```

### Requirements after the fix

| ID | Status |
|---|---|
| AST-12 | met |
| Spec "Sheet content", *every view SHALL include a 1.8 m human scale bar* | **met**: the bar is now in all thirteen tiles of each light, the pilot tile by its lower bands with the label saying so |
| AST-04 | met for what the sheet can show (unchanged) |
| AST-05 | met for the overlay (unchanged) |
| Spec "Location and failure", "Part of the one command" | met (unchanged) |

**Verdict: APPROVED**
