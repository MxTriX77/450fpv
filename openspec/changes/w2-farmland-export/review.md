# QA review: w2-farmland-export (task 2.1)

**Verdict: merge.** The map package is deterministic, self-consistent and Unity's:
- **Two fresh exports are byte-identical** to each other and to the committed package, all 39 files (`map.json` included).
- **The heights are Unity's.** The 1,000 probes match within 0.0112 mm against a 1 mm tolerance. I re-derived the probe points from the seed, 1,000 of 1,000. Both known-wrong triangulations fail the same test: the other diagonal on 256 probes, bilinear on 161.
- **Every object is placed, once, on the map:** 294 trees, 1,352 bushes, 5 ruins and 28 debris. The exporter's own scene checks passed in both of my runs.
- **The class rule is design §5's.** The main tracks read 100 % `dirt_track` along their centre lines, and the ruin yards read 55 % `dirt_track` and 37 % `dry_crust`, as §5 predicted.
- **Nothing third-party is pushed.**
- **The exporter compiles in our project.**

Nothing blocks. The findings below are should-fix items for later units, and one QA incident in the local store that is mine, not the branch's (see "QA incident").

**Scope:** `unity/physics/physics-integration-2--w2-export` at `44db6a69`, tasks 1.1–1.3. Its merge base is today's `origin/unity/physics/physics-integration-2` (`97686f5e`), so it merges as a fast-forward. The diff is 50 files: the package, `tools/map/validate.py` and its tests, the exporter and the change folder.

**How I checked:**
- Every command below was run by me. No author's output was used, except the full EditMode run (see Tests).
- Scratch scripts and exports stayed in my session's scratchpad, outside the repo. This worktree was not edited, apart from this file and the tick.

## Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| **The package validates** | **Passed** | `validate.py` OK; two exports from the same scene byte-identical to each other and to the committed package (E1) |
| **The heights are Unity's** | **Passed** | 1,000 seeded probes, worst 0.0112 mm against 1 mm; probe points re-derived 1,000 of 1,000; both known-wrong models fail (E2) |
| **Every object is placed** | **Passed** | 1,679 rows = 294 + 1,352 + 5 + 28, all on the map. The exporter checks the counts against `treeInstanceCount` and the `Village` children, and that no other renderer draws on the map; both of my runs passed it (E1, E3) |

## Requirements

| ID | Status | Why |
|---|---|---|
| AST-11 | **Partly met** (the share this change owns: met) | Every class id is a `surfaces.json` row, and the gravel rule is the physics engineer's (design §5). The visual materials and feathering belong to other units |
| WLD-11 | **Partly met** (the share this change owns: met) | The package carries what the heightfield needs: Unity's samples, its triangulation and a bit-checkable reference (`Tile.height`). The 10,000-point rendered-surface check and the queries are W3's |
| WLD-12 | **Partly met** | Checked: format version, georeferencing keys, file hashes, 2^n + 1 resolutions, seams, sizes, unknown surfaces, out-of-map objects, the row-0-north convention (a flipped heightmap fails the probes). Not yet: physical ranges, the `holes` layer (none yet, W5), pose parity at load (W3), the rules' surfaces (S1) |
| WLD-09 | **Not met, by decision** | D-088 adopts the farmland as it is: no `layout.json` and no compiler. The exporter's one command rebuilds the package |
| WLD-16 | **Not testable yet** | No holes. Their place and encoding are signed off in design §4, for W5 |
| AST-10 | **The input is met** | The grass instance counts per cell and layer are exported, and their totals checked |
| AST-05 | **Not this change** | The physics metadata is W4a's and W4b's |

## Decisions

- **D-088 (as it is):** the exporter only reads the scene and the generator, through reflection. My before-and-after hash of the store's source files shows no scene, terrain, prefab or generator file changed by an export.
- **D-089 (nothing non-redistributable pushed):**
  - `precommit.py --tree HEAD`: 0 findings. `fetch.py --verify`: OK.
  - Nothing under `vendor/`, `reference/`, `Library/`, `Temp/` or `Logs/` is in the diff.
  - The package holds the generator's own data: heights, class ids and grass counts. The landform is procedural, from `LandformSeed` (no vendor heightmap stamp in `FarmLandBuilder`).
  - From the packs it holds only prefab names and two mesh y-extents per grass prefab: numbers, not geometry.
- **D-094 (the scene's frame):** `map_rect_m` is [0, 0, 1000, 1000], and the README states the frame. This branch's `docs/decisions.md` doesn't hold D-088–D-095 yet: the integration branch was cut before them. That's for the integration's merge with `unity/main`, not for this branch.
- **D-095 (terrain edits are data):** the package carries no edits. The holes' place is reserved in the README and design §4.

## Evidence

**E1. Two exports, byte for byte.** I copied the exporter at `44db6a69` (SHA-256 `889C4E5F…8137`, identical to the worktree's) into a temporary folder of the store's farmland project. I ran it twice into scratch folders, then removed it.
```
& $env:UNITY -batchmode -nographics -quit -projectPath <store farmland project> -executeMethod Fpv450.FarmlandExport.Run -exportOut <scratch>/exportA -logFile <scratch>/exportA.log
exportA exit=0 elapsed=00:01:05
[FarmlandExport] wrote …\exportA: 17 tiles, 294 tree, 1352 bush, 5 ruin, 28 debris, 499 ring_trees_left_out; terrain colliders vs SampleHeight at 1000 probes: max 0.0210 mm
exportB exit=0 elapsed=00:00:21   (the same log line)

file lists A=B
file lists A=package (less README)
byte differences: 0          (cmp of all 39 files: A against B, and A against maps/farmland)
d926b392…a087  exportA/map.json = exportB/map.json = maps/farmland/map.json
```

**E2. The heights.**
```
python tools/map/validate.py maps/farmland
OK maps/farmland: 17 tiles (12 seams), 294 tree, 1352 bush, 5 ruin, 28 debris, 2841176 grass instances, 11 fields, 1000 height probes within 0.0112 mm of Unity
```
An independent script (scratch, read-only) re-derived the probe points from SplitMix64 seed 450, and ran the package's triangulation and two known-wrong ones against Unity's heights:
```
[probes] 1000 in the file, by ring {'map': 500, 'surround': 300, 'far': 200}; 1000 of 1000 re-derived (ring, x, z) equal
[model] package SW-NE: worst miss map 0.0065 mm, surround 0.0046 mm, far 0.0112 mm; probes over 1 mm: 0
[model] known-wrong NW-SE: worst miss map 5.0325 mm, surround 1.6886 mm, far 154.5901 mm; probes over 1 mm: 256
[model] known-wrong bilinear: worst miss map 3.8650 mm, surround 0.8360 mm, far 88.8774 mm; probes over 1 mm: 161
```
- The test is deterministic, with no draw at run time, so it has no false-failure rate.
- The right model clears the tolerance 89×, and both wrong ones fail it. Design §1's table reproduces to the digit.
- The probes' Unity heights are reproduced too: `height_probes.json` is among E1's identical files.

**E3. Placements, for W3, W4a and W4b** (scratch script):
```
variants: 34 Counter({'tree': 13, 'bush': 9, 'debris': 8, 'ruin': 4})
rows: 1679 Counter({'bush': 1352, 'tree': 294, 'debris': 28, 'ruin': 5}); unused variants: []
tree:   max|q|-1=9.17e-08, pure yaw, scale (w, h, w), x==z True
bush:   max|q|-1=9.22e-08, pure yaw, scale (w, h, w), x==z True
ruin:   pure yaw; negative scale: [(1662, 'Ruin_Walls', [-1, 1, 1])]
debris: all 28 carry x/z quaternion terms (full rotation), tilt 1.61–24.88°, scale 1
W4a variants: 22, package tree+bush variants: 22; in package, not W4a: []; in W4a, not package: []
W4b ruin/debris metadata: 13, package ruin+debris variants: 12; in W4b, not package: ['Ruin_Walls_mirrored']
[ground] tree: pivot y - ground, -0.05 to 0.05 mm   bush: -0.06 to 0.05 mm
[ground] ruin: -80.02 to -79.95 mm                  debris: -20.05 to -19.95 mm
```
- Every row has a full quaternion (4 numbers) and a scale per axis.
- `Ruin_Walls` id 1662 carries x = −1.
- W4a's 22 `variant` values are exactly the package's tree and bush variants.
- The README's ground claims hold: trees and bushes within 0.06 mm, houses 80 mm in, debris 20 mm in.

**E4. Hashes:**
```
[hash] 38 listed, 0 differ from a fresh SHA-256; 35 in LFS, 0 differ from git-lfs oids
```
`git lfs ls-files -l` shows all 35 binaries as LFS objects, checked out (`*`). Git LFS's own oids equal `map.json`'s SHA-256s, so a second hasher agrees.

**E5. Classes** (scratch script; the main tracks re-traced from `FarmLandBuilder.Tracks.cs`'s control points with its centripetal Catmull-Rom, 8 segments):
```
[classes] map: weeds 34.2 %, meadow_sod 33.4 %, tilled 28.6 %, dirt_track 2.1 %, dry_crust 1.6 %
[classes] surround: meadow_sod 71.0 %, weeds 29.0 %
[classes] far: meadow_sod 69.2 %, weeds 30.8 %
[classes] within 10 m of a ruin's pivot (6569 samples): dirt_track 55.0 %, dry_crust 37.1 %, meadow_sod 4.4 %, weeds 3.5 %
[tracks] main-track centre lines (8391 points every 0.25 m): dirt_track 100.0 %
[tracks] main-track cores, within 2 m of the centre (41943 points): dirt_track 33547 of 33552 off-centre points, meadow_sod 5
```
- The exporter's rule (`FarmlandExport.cs:229–231`) is design §5's exactly: if Gravel > 0, DryMud's weight goes to `dirt_track`; then the argmax, ties to the lower id.
- `map.json` states the rule (`ground_classes.rules`). The README's shares match to the printed digit.
- The ruin yards read 55 / 37 as §5 predicted. That is accepted (the spec says nothing about it).
- The 5 `meadow_sod` samples at 1–2 m off a centre line (0.012 %) are the track's edge feathering where my re-trace sits a hair off; the centre lines are 100 %.

**E6. Compile and tests.**
```
& $env:UNITY -batchmode -nographics -projectPath <w2-export>/unity -runTests -testPlatform EditMode -testFilter "Fpv450.Tests.EditMode.TestRangeTests|Fpv450.Tests.EditMode.GroundPatchPrefabTests" -testResults Logs/qa-editmode-w2.xml -logFile <scratch>/qa-editmode-w2.log
exit=0 elapsed=00:00:41
error CS: 0
<test-run … total="11" passed="11" failed="0" …>
```
- The run compiled the editor assembly (`CompileScripts`, 0 errors).
- `Library/ScriptAssemblies/Fpv450.Editor.dll` (13:34:47) is newer than `FarmlandExport.cs` (13:28:38) and holds the gravel rule's string.
- `Fpv450.Tests.EditMode` references `Fpv450.Editor`, so its 11 tests ran against it.

The developer's full run (`unity/Logs/editmode-w2-13.xml`, started 11:36Z, after `ce666ba0` was committed at 11:35Z): 1,579 tests, 1,557 passed, 16 skipped, 6 failed. The 6 are exactly the known ones:
- the 3 red tests: `LegShotsTests.EachFlightShowsItsLeg` and two `SessionTests` crash checks
- 3 timeouts, `FiberContact` (straw), `FiberCut` and `LegDamage`. All 5 re-run cases pass in `editmode-w2-13-rerun.xml`

None of the 6 touches the map, the exporter or the validator.

```
python -m pytest tools/map -q            48 passed in 17.13s
python -m pytest tools -q                1742 passed in 1811.32s (0:30:11)
python tools/hygiene/precommit.py --tree HEAD    3002 files scanned, 0 findings
python tools/assets/fetch.py --verify            OK
openspec validate w2-farmland-export --strict    Change 'w2-farmland-export' is valid
```

**E7. Hygiene.**
- The 10 commits are subject-only, with no body and no trailer: `git log --format=%B` greps 0 for co-author, Claude or Anthropic.
- The commits use the role identities: World Builder, Physics Engineer, Orchestrator.
- The binaries are in LFS (`*.r16`, `*.raw`).
- No place, date or OSD value appears in the package: the precommit scan is clean.

## Findings

### Blocking
None.

### Should fix (not blocking; owners named)

**S1. `validate.py` doesn't check `ground_classes.rules`.** Owner: the world builder, at the next touch of the validator (W3 or W5).
- `_check_classes` checks `layers` against `surfaces.json`, but a rule's `surface`/`id` and its `layer`/`when.layer` names are never checked.
- A rule's id never joins the allowed set. It passes today only because `dirt_track` (13) is also a layer's class.
- **Fix:**
  - check each rule's (`surface`, `id`) against `surfaces.json` and add the id to the allowed set
  - require `layer` and `when.layer` to be listed layers
  - add one break case to `test_each_rule_catches_its_break`

**S2. Two package facts are checked by the exporter only, not by the validator:**
- **The probe counts per ring.** `map.json`'s `height_probes` says 500 / 300 / 200, but nothing compares it with the file. I did, and it holds (E2).
- **The grass resolution against the map's detail grid.** Only the byte count is checked.

Cheap to add with S1. Owner: the world builder.

**S3. W4b's design and this package disagree on the mirrored house.** Owners: the orchestrator (the call), then W3's loader or W4b.
- `w4b-farmland-ruins` design §2 says "a placement row has a yaw only", and that W2 exports the mirrored instance under `Ruin_Walls_mirrored`.
- This package does what the brief and W3 need: `Ruin_Walls` with scale (−1, 1, 1) and a full quaternion. So `Ruin_Walls_mirrored` never appears as a variant (E3).
- One side must map it. Either W3's loader resolves (`Ruin_Walls`, scale x < 0) to `Ruin_Walls_mirrored`'s metadata, or W4b drops that variant and the loader mirrors `Ruin_Walls`' shapes.
- The full quaternions also settle W4b's "up to 0.24 m off" for the debris' tilt. The tilt measured here is up to 24.9° with the slope.

**S4. The exporter's "nothing else draws on the map" check uses each renderer's bounds centre** (`FarmlandExport.cs:340–346`). A renderer whose centre lies outside the map but which reaches over it would pass unseen. None does today, since the counts match. Owner: the world builder, if the scene ever gains edge objects.

### Notes
- The README rounds the yards' weeds to 3 % (measured 3.50 %). Cosmetic.
- The validator's seam check skips tiles of unequal size, so the far ring's 8 seams (steps up to 99 mm) are documented in design §2 but not validated. W3 derives them at load, so this is fine.

## QA incident (mine, in the local store, not the branch's)

Opening the store's farmland project in batchmode for E1 made Unity 6000.3.26f1 upgrade it from the project's own 6000.3.0f1. It rewrote 4 files and added 4 IDE files. The developer had restored the same 4 files from a backup after their runs, then deleted the backup.

**Restored:**
- `Packages/manifest.json` and `ProjectSettings/ProjectVersion.txt`, byte-exact, rebuilt and checked against my pre-run SHA-256s, with their original timestamps.
- The 4 generated files (`.vsconfig`, two `.csproj`, one `.slnx`) are removed.

**Not restored:** no copy of the originals exists outside `reference/`, which I won't touch.
- **`Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`.** URP's migration added settings to it, 22,478 → 26,647 bytes. Its original SHA-256 is `353764f613f2c5dd2fe1e21edb1b798a00b615a85a17a040537c7e4f0ec45664`.
- **`Packages/packages-lock.json`,** with the 7 updated packages' versions. Reverting those versions gives the original's size, 13,240 bytes, but not its hash. A 15-minute search over earlier same-length versions of the other packages found no match. Its original SHA-256 is `af49a91944e75e63fb3be468b9c57a4d8a35f8d393fb8c0382863e8413b85c58`.
- Both hashes allow a verified restore from the friend's original delivery.
- **Impact:** none on any checkout.
  - W1 links only `ALP_Assets`, `Art`, `Farm_land/Materials`, `Farm_land/Terrain/Generated` and `TerrainSampleAssets` into our project, so neither file is in it.
  - The scene, terrain data, prefabs and generator are unchanged (hash snapshot).
  - Any open of the store in the only installed editor, 6000.3.26f1, rewrites both files the same way again.

The store's source files (everything but `Library/`), against my pre-run snapshot, after the restore:
```
1079 files; added []; removed []
content changed ['Assets/Settings/UniversalRenderPipelineGlobalSettings.asset', 'Packages/packages-lock.json']
```

**Recommended for future runs:** keep a permanent pristine copy of these 4 settings files next to the store, outside every scratchpad, and restore from it after each batchmode open. The next export (W5's re-export) needs one.
