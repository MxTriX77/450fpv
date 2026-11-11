# QA review: w4b-farmland-ruins (task 2.1)

**Verdict: fix first.** The physics side is sound and the build reproduces itself. Three things block the merge:
1. **D-089: the debris' snag records copy the vendor mesh.** Each debris piece's open-edge snag polyline is the mesh's boundary vertices, to the micrometre: 18 of 18 vertices of the wall panel, 25 of 26 and 19 of 20 of the two roof sheets, 49 of 84 of the window. With the plane the boxes give, those three sheet pieces can be rebuilt from the pushed file. The copies are in two commits already on the public remote.
2. **37 visible openings have no gap** (REAL-05, the spec's "every opening's gap"). They are enclosed panes, clear through the wall band in both the visual and the collision, each wider than `gaps.min_m` (0.25 m) both ways: 27 of them 0.26–0.28 m, 10 of them 0.32–0.44 m, among them two full 0.40 × 0.9 m window panes of the burnt ruin. The developer noted 3 of them (the mirror pair's 0.28 m panes).
3. **Three roof holes are not the clear space.** Their polygons run over roof-frame members that are still there, drawn and collided: up to 245 mm into the hole in the visual.

What passes:
- **Every piece rebuilds byte for byte**, and the audit's counts match design.md §8.1.
- **The walls are two skins, open between them,** wherever the visual is hollow.
- **The crash and door tests pass.** The crash names `Ruin_RoofCollapsed`, `wall_back`, `timber`, at the wall's face.
- **The gaps that exist are clear space** in the collision and in the visual, except three roof holes. A drone-sized capsule clears every drone-sized gap where the visual does.
- **Burnt wood is `timber` and the roof is `asbestos_cement`.**
- **The mirrored house's metadata is the house's, mirrored,** to 0.1 mm on every common gap.

**Scope:** `unity/physics/physics-integration-2--w4b-ruins` at `2fc531af`, tasks 1.1 and 1.2. Its merge base with `origin/unity/physics/physics-integration-2` is `97686f5e`. The line has moved on since (W2's merge), and the branch still merges onto its tip without conflict (`git merge-tree`). The diff is 64 files: the generator and its libraries, the params, the metadata, the sim test, the manifest entry and the change folder.

**How I checked:**
- Every command below was run by me. No author's output was used.
- My scratch scripts ran from my session's scratchpad. They import the branch's own readers (`generators.vendor_ruin`'s FBX read and placement, `lib.audit`'s internals), never its slab, air or gap code. Nothing outside this file was edited in the worktree.
- No GPU work, Blender or Unity: the review sheets are the developer's PNGs, read as they are.
- The pilot's pause killed my first runs of three ruin audits and of pytest. I re-ran all of them from the start; the numbers below are from the complete runs.

## Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| **A crash names the ruin's material** | **Passed** | 8 m/s into `wall_back`: first touch at 8.85 m/s, the crash names `Ruin_RoofCollapsed`, `wall_back`, `timber`; the contact point at z −4.5520 against the face's −4.55; 0 tunnels, 0 events lost (E1) |
| **An opening is a gap** | **Partly failed** | The flight through `partition_door1` touches nothing at or before the doorway (E1). A drone-sized capsule clears 39 of the 40 gaps over 0.574 m; the 40th meets the visual too (E5). But the sheets show 37 enclosed openings over 0.25 m with no gap (E6), and 3 roof holes overstate the clear space (E5) |

## Requirements

| ID | Status | Why |
|---|---|---|
| REAL-05 | **Partly met** | Met: real thickness (two 2 cm skins where the mesh is hollow, E7); every visible face within 43 mm of a box; nothing texture-only that the mesh models. Missed: 37 visible openings are not gaps (E6), 3 holes are wider than their clear space (E5). The 241 boxes over 50 mm are the accepted debt below |
| AST-05 | **Partly met** | Present: boxes with a material and a part each, gaps with `narrowest_m`, snag edges, wind volumes, the floor as landable, LOD and cull. The audit's counts are honest (E3). My capsule probe clears 39 of the 40 drone-sized gaps; the 40th meets the visual as well (E5). The branch has no test of its own for it. Gaps are not complete (E6) |
| AST-06 | **Partly met** (this change's share) | The damage carries collision: broken tops as stepped boxes and snag lines, the collapse's holes. "Openings are the real clear space" fails as above |
| AST-12 | **Partly met** | Every piece has its sheet at 1.5 / 10 / 50 / 200 m, an 8-view turntable and the overlay from three sides; a piece with openings gets every gap at 1.5 m and the four sides and top at 10 m. One lighting only, not day and overcast. There is no reference letter for the friend's ruins (D-088 adopts the look), so no side-by-side |
| PHY-23 | **Partly met** | Broken edges collide and carry snag; the gaps that exist are clear space (E5); the collapsed-roof ruin's interior is a porosity-1 cavity. The 37 ungapped openings are clear in the collision but unknown to a gap query |
| WLD-05 | **Partly met** (this change's share) | The village's five ruins and 28 debris have physics. Placement is W3's |

## Decisions and the brief's points

**D-089 (nothing pushed is vendor geometry or copies it): failed for the debris.**
- **Clean:**
  - `village_ruins.fbx` is in the manifest, `local_only: true`, SHA-256 `b87059ac…cb1a`, the same as the params' pin and the file in the store.
  - `House.fbx` and `Doors.fbx` are pinned under the Country House FREE entry, local only.
  - `CREDITS.md` names the cut file. `fetch.py --verify`: OK.
  - Nothing under `vendor/`, `reference/` or `review-sheets/` is tracked, and the diff holds no mesh, image or Unity cache file (E2).
- **The ruins' metadata cannot rebuild the vendor mesh.** It is boxes on a 1 cm raster, gaps, and snag lines read off that raster. Of each ruin's 1,448–4,244 mesh vertices, 2–10 (0.1–0.4 %) coincide with a box corner, by the raster's chance. No snag or gap point is a vertex. No topology, normals, UVs or textures are pushed (E4).
- **The debris' metadata can.** `_open_edges` (`generators/vendor_ruin.py`) chains the mesh's open edges from `lod0.vertices` and writes them as they are (E4):

  | Piece | Vertices reproduced exactly | Share |
  |---|---|---|
  | `Debris_WallPanel` | 18 of 18 | 100 % |
  | `Debris_RoofSheet_A` | 25 of 26 | 96 % |
  | `Debris_RoofSheet_B` | 19 of 20 | 95 % |
  | `Debris_Window` | 49 of 84 | 58 % |
  | doors | 4 each | 5–7 %, a leaf's outline only |

  - A sheet piece is its outline and a thickness. The outline is in the snag record and the plane is in the boxes, so the three sheets can be rebuilt; only the UVs are missing.
  - The fix is small: take the debris' snag edges from the raster, as the ruins' are.
- **The copies are already public.** Commits `a59acac2` (1.1) and `a399a05a` (1.2) carry them, and both are on `origin/unity/physics/physics-integration-2--w4b-ruins`, a public repository. No other remote branch contains them. A fix commit on top keeps them in history, and `gh pr merge --merge` would carry that history into the line and `unity/main`. **Orchestrator's call:** rewrite this branch before the merge (the fix folded into the two commits, then a force-push), or accept the history.

**The 20 mm zone, measured on the clear side** (the orchestrator's reading). **Of the 6,931 zone findings, 1,862 are left;** the other 5,069 are only board thickness. My tool (`clear_side.py`) works like this:
- It takes every box the audit holds over 20 mm in the zone, within 50 mm.
- If the box is a sheet (25 mm thick or less), it collapses it onto each of its broad faces in turn: 0.2 mm thick, same footprint, the edges where they were. It runs the audit's own zone search on that face, with the same covering, ground and faces.
- The better face is the clear side, and the sheet's back counts as board thickness.
- A thicker box stays a finding as it is.
- **The tool is on the audit's scale:** on the first three findings of each ruin it re-measures the audit's numbers to the micrometre (23.794, 21.957, 32.004 mm on `Ruin_Walls`).

| Ruin | Zone findings | Board thickness only | Sheets still over 20 mm | Thicker boxes | **Left** |
|---|---|---|---|---|---|
| Ruin_RoofCollapsed | 1744 | 1210 | 489 | 45 | **534** |
| Ruin_Walls | 1233 | 920 | 298 | 15 | **313** |
| Ruin_Walls_mirrored | 1268 | 950 | 306 | 12 | **318** |
| Ruin_Burnt | 1464 | 1062 | 375 | 27 | **402** |
| Ruin_Stump | 1222 | 927 | 293 | 2 | **295** |

- **About three quarters of the zone findings are only board thickness:** the outer faces follow the visual within 20 mm, and only the sheet's back is further. The orchestrator's reading clears those.
- **The sheets still over 20 mm** are mostly 20–30 mm off. Per ruin the median is 24–26 mm and the worst 46–49 mm; 404 of the 1,761 sheets are over 30 mm.
- **The floor is the largest group:** 134–156 per ruin. The collapsed-roof ruin adds roof-frame (54) and roof (33) sheets. These are the sagging floor's faces snapped to the axis family, design §10's "floor top up to 34 mm under the visual floor". On the clear side the worst is 49 mm, not 34.
- **Then door frames, partitions, window frames and trim.** The thicker boxes are mostly window frames, 30–80 mm members whose sides stand off the visual.
- **The same cause as the 241, a matter of degree.** These are faces up to 6° off their family's plane, the same mechanism as the 241 boxes over 50 mm. I report them with that debt, not as a blocker.
- **For the physics engineer:** the floor is the ruins' landing surface. A leg landing there meets a box top up to 49 mm off the drawn floor.

**The 241 boxes over 50 mm** (phase A's debt): **reported honestly.**
- My audit runs give 241, worst 97 mm, per ruin as design §8.1 says (E3).
- **By part:**

  | Part | Boxes over 50 mm | Design's account |
  |---|---|---|
  | trim | 97 | 52 in the collapsed-roof ruin and 43 in the burnt one: as stated |
  | floor | 60 | 11–14 per ruin |
  | stove | 46 | 2–13 per ruin |
  | roof | 18 | |
  | roof frame | 9 | |
  | door frame | 3 | |
  | wall parts | 8 | the "2 cm tall slabs across a wall's two skins" |

- Design's "233 of them" is the first six rows. Design names every row but door frame, and those 3 boxes are still in its total.
- 118 of the 241 are rotated boxes, a tilted family's sheets. The other 123 are axis families with faces up to 6° off.

**Out of scope, checked as reported, not fixed** (for the physics engineer, W3):
- **The thin-skin crash peak:** 2,705 m/s² on a 2 cm sheet against 1.1's 22,202. It is in design §9, and my run prints `peak 2705 m/s²` (E1).
- **The lost contact events:** 4,205 in the door scenario. It is in design §9 and §10, and my run prints `4205 lost` (E1). The door test asserts nothing about lost events; the wall test asserts 0, and gets it.
- **The village's shapes:** 20,171, in design §8 and §10. I recounted them from W2's placements on the line's tip: the houses 18,114, the 28 debris 2,057 (A ×5, B ×2, panel ×1, window ×5, doors 4, 4, 3, 4).

**Materials:**
- **Burnt wood is `timber`.** Every `…_Burnt` wood slot carries `timber`, and the burnt plaster carries `masonry`. The burnt ruin holds only `timber` and `masonry` boxes: it is roofless.
- **The roof is `asbestos_cement`:** the collapsed-roof ruin's 720 `roof` boxes.
- **The debris:** the roof sheets are `asbestos_cement`, the panel, window and doors `timber` (E3).

**The mirrored house** (design §2: a negative-scale placement uses `Ruin_Walls_mirrored`):
- **Params:** the two files differ only in `"mirror_x": true`.
- **Metadata:**
  - The bounds are the house's mirrored.
  - The 19 common gaps' polygons, mirrored, agree within 0.1 mm.
  - `Ruin_Walls` alone has `porch_wall_window9` and `window10` (0.28 m); the mirror alone has `porch_wall_window8` (0.28 m). That is as design §5 notes.
- **The collision is open at the missing panes.** I probed each missing pane's polygon, mirrored into the other variant: no box meets its clear prism there. So these are missing gap records, not closed collision.
- **W2's placements** (on the line's tip) name it `Ruin_Walls` with scale (−1, 1, 1), the other `Ruin_Walls` (1, 1, 1). Design §2 states the rule for W3's loader.

**D-088 (the look as it is):** no visual file is touched. The roof sheets' and the roof's corrugation is texture-only on a flat vendor mesh, and the collision follows that flat mesh. That is a limit of the adopted look (REAL-05's "no texture fakes"), not this change's to fix.

## Findings

**Blocking (fix first):**
- **F1, D-089.** The debris' open-edge snag records copy the vendor's boundary vertices (above). The history decision is the orchestrator's.
- **F2, REAL-05 / the spec.** 37 enclosed openings over 0.25 m both ways have no gap (E6):

  | Ruin | Ungapped openings | Where |
  |---|---|---|
  | `Ruin_RoofCollapsed` | 18 | porch glazing: 8 east, 10 west, 0.26–0.44 m |
  | `Ruin_Burnt` | 12 | back 1 and front 2 (0.40 × 0.90, 0.40 × 0.88, 0.32 m); porch 9 |
  | `Ruin_Walls` | 3 | porch, 0.28 m |
  | `Ruin_Walls_mirrored` | 4 | porch, 0.28 m |
  | `Ruin_Stump` | 0 | |

  - **The likely cause is in the method, not one value.** `_openings` takes only the intact house's openings and cuts each at its jambs' tops, probed 3 cm outside it in the ruin. An opening the ruin opens differently, or whose probe lands in a broken neighbour, is lost.
  - **These are not drone-sized** (the widest is 0.44 m against 0.574 m), but the fiber and the wind pass them.
  - **A fix:** also take the enclosed clear regions of the ruin's own wall bands, keeping the intact house's ids where they match.
- **F3, REAL-05 / AST-05.** Three roof holes' polygons overlap roof-frame members that are still there (E5):
  - `roof_hole9` (narrowest 1.47 m): a member 245 mm into it in the visual, 260 mm in collision.
  - `roof_hole18` (0.26 m): 130 mm in both, nearly closed.
  - `roof_hole36` (0.25 m): 18 mm in the visual, 41 mm in collision.

  `_skin_holes` takes the lost skin area, not the clear space. Take the kept boxes of every part out of the lost area before inscribing the rectangles.

**Should fix (not blocking):**
- **F4.** A `window_frame` box of `Ruin_Burnt` reaches 25 mm into `porch_wall_window8`'s rectangle, where the visual is clear to 4 mm. It is `collision[777]`, a 25 mm rail across the pane's foot. The audit counts it in the zone (21.0 mm). The gap's bottom edge should sit on the rail.
- **F5.** Design §2 and §10 say a placement row has a yaw only and put the debris' tilt at "≤ 6° plus the slope". W2's rows (on the line's tip) carry Unity's full quaternion, and 14 of the 28 debris tilt more than 6°, up to 24.9°. On design §2's own measure (2.3 m × sin θ), a yaw-only loader puts a door's end 0.97 m off, not 0.24 m. For W3: read `rotation_xyzw`.
- **F6.** AST-05's acceptance asks for a capsule through every gap. My probe (E5) is the only one. A test in `test_vendor_ruin.py` would keep it.

## Evidence

**E1. The sim tests.**
```
dotnet test tools/simtests --filter "FullyQualifiedName~RuinContactTests"
Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3, Duration: 10 s - Fpv450.Sim.Tests.dll (net10.0)

dotnet test tools/simtests --no-build --filter "FullyQualifiedName~RuinContactTests" --logger "console;verbosity=detailed"
first prop_fr on Ruin_RoofCollapsed (timber) at 8.85 m/s; crash (Acceleration) by motor_fr on Ruin_RoofCollapsed (timber) at 8.69 m/s, 1.59 m up; peak 2705 m/s², impulse 67.40 N·s; 0 tunnels, 0 lost
the crash's shape 319 (wall_back), point -2.840 1.594 -4.5520
  Passed IntoItsWallTheCrashNamesTheRuinThePartAndTheMaterial [2 s]
  Passed TheMetadataNamesEveryShapesPart [31 ms]
first prop_fl on Ruin_RoofCollapsed (timber) at 3.32 m/s; crash (Acceleration) by canister on  () at 4.25 m/s, 0.16 m up; peak 771 m/s², impulse 35.88 N·s; 0 tunnels, 4205 lost
first touch past the doorway: window_frame at -5.60 1.81 3.21
302 touches of the ruin past the doorway
  Passed ThroughItsDoorNothingTouchesTheDoorway [8 s]
```
- **The wall test is deterministic** (seeded, still air), so it has no false-failure rate.
- **It checks its premise with the map's own rays:** nine rays at the face.
- **Its checks:**
  - the part and the material by the crash's shape index;
  - the crash point within 30 mm of the face;
  - every contact with the ruin short of 50 mm past the face. The plaster skin stands 180 mm behind the face, so a drone through the siding's sheet would meet it and fail this.
- **The door test** asserts no touch of the ruin within 0.5 m past the doorway's wall, or before it. Its flight later crashes on the ground past the next room's window. That is outside the scenario.

**E2. Hygiene and specs.**
```
python tools/assets/fetch.py --verify
OK                                   exit=0
python tools/hygiene/precommit.py --tree HEAD
3011 files scanned, 0 findings       exit=0
openspec validate w4b-farmland-ruins --strict
Change 'w4b-farmland-ruins' is valid exit=0

git ls-files vendor review-sheets reference           -> 0 files
git diff --name-only 97686f5e..HEAD | grep -Ei '^(vendor|reference|review-sheets)/|/(Library|Temp|Logs|UserSettings)/|\.(fbx|png|jpg|blend|mp4)$'   -> nothing
git check-ignore -v vendor review-sheets/N01/Ruin_Walls.png   -> .gitignore:15:/vendor, .gitignore:68:review-sheets/
sha256 of the store's files: village_ruins.fbx b87059ac7e9a640b2d9e…, House.fbx 6508398152b475e1c7b1…, Doors.fbx 5a5d3f6da5f4cbc94302… (the params' and the manifest's pins)
```
- **Commits:** the branch's 13 commits are subject only, from 43 to 72 characters, with no body and no trailer.
- **Identities:** `Asset Artist <assets@450fpv.local>` as author and committer, and `Orchestrator` for the two planning commits.

**E3. Every piece rebuilds, and the audit's counts.** Each run was `python tools/blender/vendor_build.py <piece> --check --audit`. I ran the debris as `N02`, and the five ruins one by one; the three killed by the pause I re-ran from the start.
```
Debris_RoofSheet_A: 194 boxes (194 asbestos_cement; 1 of 195 pruned within 0.02 m) over 4 plane families; 0 gaps, 0 wind volumes, 1 snag records
Debris_RoofSheet_A: audit took 6.2 s, 0 problems
Debris_RoofSheet_A: matches unity\Assets\_Project\Art\Models\ruins\N02\Debris_RoofSheet_A.assetmeta.json
  (the same for the other seven: Debris_RoofSheet_B 140 boxes, Debris_WallPanel 5, Debris_Window 66, Door01 13, Door02 13, Door03 104, Door04 14; 0 problems; matches)
Ruin_RoofCollapsed: 7016 boxes (720 asbestos_cement, 797 masonry, 5499 timber; 661 of 7677 pruned within 0.02 m) over 93 plane families; 82 gaps, 230 wind volumes, 130 snag records
Ruin_RoofCollapsed: audit took 1965.6 s, 1949 problems
Ruin_RoofCollapsed: matches unity\Assets\_Project\Art\Models\ruins\N01\Ruin_RoofCollapsed.assetmeta.json
Ruin_Walls: 2690 boxes (470 masonry, 2220 timber; 246 of 2936 pruned within 0.02 m) over 52 plane families; 21 gaps, 124 wind volumes, 70 snag records
Ruin_Walls: audit took 429.4 s, 1303 problems
Ruin_Walls: matches unity\Assets\_Project\Art\Models\ruins\N01\Ruin_Walls.assetmeta.json
Ruin_Walls_mirrored: ... audit took 605.9 s, 1336 problems ... matches
Ruin_Burnt: 3665 boxes (618 masonry, 3047 timber; 239 of 3904 pruned within 0.02 m) over 53 plane families; 26 gaps, 136 wind volumes, 72 snag records
Ruin_Burnt: audit took 1090.9 s, 1619 problems
Ruin_Burnt: matches unity\Assets\_Project\Art\Models\ruins\N01\Ruin_Burnt.assetmeta.json
Ruin_Stump: 2033 boxes (406 masonry, 1627 timber; 160 of 2193 pruned within 0.02 m) over 49 plane families; 4 gaps, 100 wind volumes, 20 snag records
Ruin_Stump: audit took 300.8 s, 1263 problems
Ruin_Stump: matches unity\Assets\_Project\Art\Models\ruins\N01\Ruin_Stump.assetmeta.json
exit codes: 0 for every run
```
Counted per box from the audit's lines, as design §8.1 counts them (`audit_counts.py`):

| Piece | Over 50 mm (worst) | Within 50, over 20 in the zone | Mesh → collision, worst | Problems | Design §8.1 |
|---|---|---|---|---|---|
| Ruin_RoofCollapsed | 109 (97 mm) | 1744 | 43.0 mm | 1949 | 109 (97), 1744, 43, 1949: equal |
| Ruin_Walls | 26 (65 mm) | 1233 | 41.6 mm | 1303 | equal |
| Ruin_Walls_mirrored | 23 (65 mm) | 1268 | 41.6 mm | 1336 | equal |
| Ruin_Burnt | 68 (82 mm) | 1464 | 41.6 mm | 1619 | equal |
| Ruin_Stump | 15 (63 mm) | 1222 | 34.0 mm | 1263 | equal |
| the 8 debris | 0 | 0 | | 0 | equal |
| **all** | **241** | **6931** | | **7470** | equal |

- The audit's times are longer than design's because my runs shared the CPU and were capped.
- The audit's 153 gap lines (82 + 21 + 20 + 26 + 4) report no problem.

**E4. Can the pushed metadata rebuild the vendor mesh?** My script (`vertex_leak.py`) reads each FBX through the branch's own reader and placement (`vendor_ruin._read`, `_placement`, `_triangles`). It counts the mesh's vertices that the committed metadata reproduces within 2 µm: every snag point, gap corner, and the corners of the unrotated boxes.
```
Debris_RoofSheet_A: mesh 26 unique vertices, 24 polygons; snag 1 records 27 points; exact vertex hits: snag 25, gaps 0, box corners 0; all 25 = 96.2 % of the vertices
Debris_RoofSheet_B: mesh 20 unique vertices, 18 polygons; snag 1 records 21 points; exact vertex hits: snag 19, gaps 0, box corners 0; all 19 = 95.0 % of the vertices
Debris_WallPanel: mesh 18 unique vertices, 16 polygons; snag 1 records 19 points; exact vertex hits: snag 18, gaps 0, box corners 0; all 18 = 100.0 % of the vertices
Debris_Window: mesh 84 unique vertices, 58 polygons; snag 8 records 58 points; exact vertex hits: snag 49, gaps 0, box corners 0; all 49 = 58.3 % of the vertices
Door01: mesh 56 unique vertices, 61 polygons; snag 1 records 5 points; exact vertex hits: snag 4, gaps 0, box corners 0; all 4 = 7.1 % of the vertices
Door02: mesh 68 unique vertices, 73 polygons; snag 1 records 5 points; exact vertex hits: snag 4, gaps 0, box corners 0; all 4 = 5.9 % of the vertices
Door03: mesh 84 unique vertices, 89 polygons; snag 1 records 5 points; exact vertex hits: snag 4, gaps 0, box corners 0; all 4 = 4.8 % of the vertices
Door04: mesh 56 unique vertices, 61 polygons; snag 1 records 5 points; exact vertex hits: snag 4, gaps 0, box corners 0; all 4 = 7.1 % of the vertices
Ruin_RoofCollapsed: mesh 4244 unique vertices, 6084 polygons; snag 130 records 910 points; exact vertex hits: snag 0, gaps 0, box corners 4; all 4 = 0.1 % of the vertices
Ruin_Walls: mesh 2448 unique vertices, 2964 polygons; snag 70 records 754 points; exact vertex hits: snag 0, gaps 0, box corners 10; all 10 = 0.4 % of the vertices
Ruin_Walls_mirrored: mesh 2448 unique vertices, 2964 polygons; snag 68 records 808 points; exact vertex hits: snag 0, gaps 0, box corners 10; all 10 = 0.4 % of the vertices
Ruin_Burnt: mesh 2890 unique vertices, 3646 polygons; snag 72 records 924 points; exact vertex hits: snag 0, gaps 0, box corners 10; all 10 = 0.3 % of the vertices
Ruin_Stump: mesh 1448 unique vertices, 1656 polygons; snag 20 records 216 points; exact vertex hits: snag 0, gaps 0, box corners 2; all 2 = 0.1 % of the vertices

git show --stat a59acac2 / a399a05a: each adds or changes the 8 N02 metadata files
git branch -r --contains a59acac2: origin/unity/physics/physics-integration-2--w4b-ruins only
gh repo view --json visibility: PUBLIC
```

**E5. Every gap probed** (`gap_probe.py`, written without the generator's gap code).
- **The clear prism** is each gap's polygon, inset 2 mm, swept ±0.2 m along its normal. Any box or visual triangle meeting it is a finding, by an exact separating-axis test. For each one I also measured how far in it reaches.
- **The capsule:** for a gap of 0.574 m or more, a sphere of radius 0.287 m swept ±0.5 m along the normal through the polygon's centre. I measured its clearance to the boxes and to the visual.
```
SUMMARY Ruin_RoofCollapsed: 82 gaps; with a box in the clear prism 14; with a visual face in it 13; drone-size gaps 22, capsule blocked by boxes 1, by visual 0
SUMMARY Ruin_Walls: 21 gaps; with a box in the clear prism 6; with a visual face in it 4; drone-size gaps 6, capsule blocked by boxes 0, by visual 0
SUMMARY Ruin_Walls_mirrored: 20 gaps; with a box in the clear prism 5; with a visual face in it 3; drone-size gaps 6, capsule blocked by boxes 0, by visual 0
SUMMARY Ruin_Burnt: 26 gaps; with a box in the clear prism 6; with a visual face in it 5; drone-size gaps 6, capsule blocked by boxes 0, by visual 0
SUMMARY Ruin_Stump: 4 gaps; with a box in the clear prism 0; with a visual face in it 0; drone-size gaps 0, capsule blocked by boxes 0, by visual 0
over 20 mm into a gap:
Ruin_Burnt porch_wall_window8 (narrowest 0.340): worst box 25.0 mm (window_frame, n -0.007..+0.061), worst visual 4.0 mm
Ruin_RoofCollapsed roof_hole9 (narrowest 1.470): worst box 259.9 mm (roof_frame, n +0.011..+0.054), worst visual 245.3 mm, capsule boxes 197 visual 205 mm
Ruin_RoofCollapsed roof_hole18 (narrowest 0.260): worst box 130.0 mm (roof_frame, n -0.041..+0.027), worst visual 130.0 mm
Ruin_RoofCollapsed roof_hole36 (narrowest 0.250): worst box 40.6 mm (roof_frame, n -0.025..+0.054), worst visual 18.2 mm
Ruin_RoofCollapsed roof_hole14 (narrowest 1.000): nothing in the prism; capsule clearance to boxes -19 mm, to visual 1 mm
```
- **Every other intrusion is 17.4 mm or less.** They are door and window frames standing proud of the band's middle, and the visual is in the same places (7.4 mm or less).
- **`roof_hole14`'s capsule** meets something 0.5 m past the hole in both the visual (1 mm clearance) and the collision (20 mm proud of the visual, inside the 50 mm tolerance). It is not a gap error.
- **The narrowest widths check out:** every `narrowest_m` equals the audit's largest inscribed circle within 5 mm. The audit's gap lines report no problem.
- **I looked at `roof_hole9_b` at full size:** a roof-frame member, drawn as orange boxes, lies across the top right of the cyan hole.

**E6. Openings without a gap** (`wall_raster.py`).
- **The method:** each wall band (±0.12 m, the end walls ±0.25 m) is seen along its normal on a 2 cm grid. A cell is clear when no visual triangle and no box crosses the band there. A clear region that touches no side of the raster is enclosed all round.
- **The bands:** the back, front and end walls, the porch's three walls and the four hall partitions of each ruin.
```
Ruin_RoofCollapsed porch east (x 2.12): 8 enclosed clear regions without a gap: 0.28, 0.28, 0.44, 0.44, 0.26 x4 m (largest clear square)
Ruin_RoofCollapsed porch west (x -2.12): 10: 0.28 x4, 0.44 x2, 0.26 x4
Ruin_Burnt back (z -4.473): 1: u 3.14..3.54 y 1.40..2.30, 0.40 m
Ruin_Burnt front (z 3.199): 2: u -3.94..-3.54 y 1.40..2.28, 0.40 m; u -3.94..-3.10 y 2.36..2.68, 0.32 m
Ruin_Burnt porch east: 1 (0.26); porch west: 8 (0.26 x5, 0.44 x2, 0.34)
Ruin_Walls porch east: 1 (0.28); porch west: 2 (0.28 x2)
Ruin_Walls_mirrored porch east: 1 (0.28); porch west: 3 (0.28 x3)
Ruin_Stump: none.  Back, front and end walls of the others: none beyond the above.
```
- **Two partition regions are not findings.** They were flagged on the collapsed-roof ruin at x 1.891 and −2.082, z 0.70–2.00. They are `partition_door4` and `partition_door3` seen in a band that missed the gap's plane by 0.19 m.
- **The 1 cm run agrees** on the collapsed-roof ruin's east porch: the same 8, beside `porch_wall_window8`, `9` and `10`.
- **The sheets show the same:**
  - `gap_porch_wall_window8_b` at full size: only the sash's pane is cyan. The two top panes, both side panes and the bottom-left pane show the backdrop.
  - `Ruin_Burnt`'s `wall_back_window11_b` and `wall_front_window6_b`: one pane cyan, its neighbour open.
  - The mirror's `porch_wall_window8_b`: one pane gapped, its neighbour open.

**E7. Two skins** (`skins.py`).
- **The method:** horizontal rays across each outer wall band on a 0.1 m grid. A span from a face entering the visual's solid to the next face leaving it, longer than 6 cm, is a hollow wall. Its middle is tested against the boxes, and a vertical ray from it tells whether it is open to the sky.
```
Ruin_RoofCollapsed: hollow spans 11008: filled by a box 255, open between two skins 10753; open to the sky above 1385, of which filled 15
Ruin_Walls:         hollow spans 4261:  filled 58,  open 4203; open to the sky 3470, of which filled 39
Ruin_Walls_mirrored: hollow spans 4264: filled 59,  open 4205; open to the sky 3473, of which filled 40
Ruin_Burnt:         hollow spans 6548:  filled 115, open 6433; open to the sky 5633, of which filled 68
Ruin_Stump:         hollow spans 1614:  filled 15,  open 1599; open to the sky 1485, of which filled 12
```
- **97–99 % of the hollow wall is two skins, open between.** On `Ruin_Walls` I traced the filled spans: 51 of the 58 are window-frame boxes at sills, heads and jambs, where the frame stands in the wall's thickness. The other 7 are wall boxes, the 2 cm seals design §8.1 names.
- **The collapsed-roof ruin's walls are open to its attic, not to the sky,** and the attic is outside air through the collapse. That is design §3's rule.

**E8. The review sheets.**
- **What I looked at:** every tile, as downscaled mosaics of `review-sheets/N01/*` and `N02/*`. That is 13 main mosaics with 1.5 / 10 / 50 / 200 m, the three overlays and the five 10 m gap views, plus every gap tile at 256 px. I also looked at four tiles at full size: `Ruin_Walls/overlay_sw`, `Ruin_RoofCollapsed/gap_roof_hole9_b`, `gap_porch_wall_window8_b`, and the debris mosaic with five tiles each.
- **The sheets match the files:** they were rendered after the metadata was written, and the box and gap counts in each `tiles.json` equal the committed files' (13 of 13).
- **What reads right:**
  - The ruins are the friend's houses, unchanged.
  - The walls' boxes follow the broken tops as steps. Plaster is a blue lining, wood orange, the roof purple.
  - Every gap that exists sits in its pane or doorway, with its snag outline on both faces.
  - The burnt ruin and the stump are roofless and carry no roof material.
  - The mirror is the house mirrored.
  - The debris' collision follows each piece: jagged roof sheets, a flat panel, an open window frame, door leaves.
- **What doesn't:** the open panes without gaps (E6) and the frame members across roof holes (E5).
- **The stump's kitchen floor** reads as tiles on the board floor. It carries `timber`, as a floor rule; at drone scale that is a board floor. An observation only.

**E9. The Python tests.**
```
python -m pytest tools/blender tools/assets -q -p no:cacheprovider
1174 passed in 2273.32s (0:37:53)          exit=0
```
- Nothing was skipped: the store is linked here, so `test_a_rebuild_reproduces_the_committed_files` ran on every vendor piece.
- I did not re-run the full `pytest tools`: the brief made it optional, and the coordinator skipped it under the CPU cap. The developer's run of it is not evidence here.
