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
