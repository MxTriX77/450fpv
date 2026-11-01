# Review: p6-test-range (task 2.1), with G1's code

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/world/p6-test-range` · **Tip under review:** `7d47968e` · **Range:** `bf94f75..7d47968e` (53 commits, 5 of them merges; 289 files)
**Scope:** `p6-test-range` 1.1–1.3 and the G1 code stacked under it (`p1-g1-look-lock-gallery` 1.1, 1.2, 1.4 and 1.3's R01 part: `LookSetup`, the noclip camera, the gallery scene and builder), which lands with it after this one pass (D-046, D-047). G1's gate is closed as seen and its 1.3 H01 station, 2.1 and 2.2 are dropped (D-046): its look misses are recorded, not re-judged.
**Read:** `CLAUDE.md`, both changes' proposal, design, spec deltas and tasks with every as-built note; D-040, D-044, D-046, D-047; PLT-02, PLT-06, PLT-07, WLD-04; on `refs/godot/main` at `8340138`: `game/assets/catalog.json`, `game/src/world/Catalog.cs`, `game/maps/README.md` (shapes, gaps, the Euler order), `tools/blender/assetkit.py` (`rotation`), `tools/map/make_uat1_gallery.py` (`ADOBE_YARD`).

**Status: in progress.** Committed early and updated as the checks run.

## Results so far

### The stand-ins' metadata against the Godot catalog (shape by shape)
QA's `qa_port_meta.py` (scratchpad; plain Python, no code from `tools/blender`): Godot's Euler [yaw, pitch, roll] as R = Ry·Rx·Rz (its `assetkit.rotation` and `Catalog.cs`), the frame change S = diag(1, 1, −1) as S·p and S·R·S, each asset quaternion turned back into a matrix with the textbook formula and the angle between the two; gaps rebuilt from Godot's centre, width, height and yaw ("faces ±Z turned by yaw", `Catalog.cs`) and matched corner to corner; wind by Godot's own rule (its `wind_volume`, else the collision; porosity ≥ 1 none, `MapSelfTest.cs`).
```
Z01 house_adobe   shapes 157 worst pos 0.0000 mm size 0.0000 mm angle 0.00009 deg | wind 1 ... por 0.11 | gaps 8 worst 0.0000 mm | snag True | Godot [45.0, 130.0] | asset [0.0, 45.0] cull 600.0
Z02 shed_plank    shapes  35 ... angle 0.00007 deg | wind 1 ... por 0.04 | gaps 1 worst 0.0000 mm | snag True | Godot [25.0, 70.0] | asset [0.0, 25.0] cull 600.0
Z03 cellar_head   shapes  11 ... angle 0.00006 deg | wind 11 ... por 0.0  | gaps 1 worst 0.0000 mm | snag True | Godot [22.0, 60.0] | asset [0.0, 22.0] cull 600.0
Z04 cellar_shaft  shapes  10 ... angle 0.00000 deg | wind 0 ...  por 1.0  | gaps 0 | snag False | Godot [] | asset [0.0] cull 600.0
Z05 gate_plank    shapes  14 ... angle 0.00007 deg | wind 1 ... por 0.62 | gaps 1 worst 0.0000 mm | snag True | Godot [20.0, 55.0] | asset [0.0, 20.0] cull 600.0
Z06 fence_planks  shapes  19 ... angle 0.00005 deg | wind 1 ... por 0.35 | gaps 0 | snag True | Godot [] | asset [0.0] cull 600.0
Z07 tree_fruit    shapes   5 ... angle 0.00008 deg | wind 1 ... por 0.62 | snag True | Godot [26.0, 70.0, 170.0] | asset [0.0, 26.0, 70.0] cull 600.0
Z08 tree_young    shapes   1 ... | wind 1 ... por 0.62 | snag True | Godot [22.0, 60.0, 150.0] | asset [0.0, 22.0, 60.0] cull 600.0
Z09 tree_maple    shapes   5 ... angle 0.00007 deg | wind 1 ... por 0.45 | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z10 tree_acacia   shapes   5 ... angle 0.00006 deg | wind 1 ... por 0.55 | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z11 tree_ash      shapes   6 ... angle 0.00006 deg | wind 1 ... por 0.5  | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z12 tree_dead     shapes   4 ... angle 0.00007 deg | wind 1 ... por 0.8  | snag True | Godot [30.0, 80.0, 200.0] | asset [0.0, 30.0, 80.0] cull 600.0
Z13 shrub_belt    shapes   1 ... | wind 1 ... por 0.5  | snag True | Godot [15.0, 40.0, 95.0] | asset [0.0, 15.0, 40.0] cull 600.0
total mismatches: 0
```
Every one of the 278 collision shapes, 18 wind volumes and 11 gaps matches at 0.0000 mm and under 0.0001°, every porosity, physics material and snag flag matches, and each names its source at `8340138`. The LOD switches match Godot's but its last, and the cull is 600 m rather than Godot's last switch (see findings).

### The map's data
QA's `qa_map.py` (scratchpad; reads `placements.json` and each placed variant's metadata, places every collision shape in map space by Unity's yaw and measures in plan):
```
posadka: 655 plants = 505 trees + 150 bushes
  trunk strip: x -139.711..159.497 (length 299.208 m), z -4.895..4.895 (width 9.790 m)
  principal axis -0.018 deg: length 299.207 m, width 9.799 m
  trunk nearest-neighbour: median 1.993 m, min 1.504, max 2.955
  share of V01 or V13 among plants: 24.58 % (>= 20 %)
  widest gap between plant positions along x: 13.956 m (-26.820..-12.864)
  widest gaps clear of every collision shape (any height) across the belt: 10.840 m at x -26.09..-15.25
start points:
  track  at (-178.0, -40.0) heading 90.0 legs on stand False: nearest collision 7.120 m (#9 Z06_fence_planks), nearest wire sample 5.737 m (#22 L03_medium), nearest patch edge 38.000 m
  field  at (40.0, 45.0) heading 180.0 legs off stand True: nearest collision 25.647 m (#714 S04_patch), nearest wire sample 88.317 m, nearest patch edge 25.647 m
```
The yard against `ADOBE_YARD` with (dx, dz, yaw) → (dx, −dz, −yaw) about (−170, −57): all 14 rows exact (house, shed at 14°, cellar head and shaft, gate, the seven fence sections, the fruit trees at 340° and 160°).

The generator run twice to two scratch files: both `a3718564…63e7`, byte-identical to the committed `placements.json`.

### The scene from its builder
`-batchmode -nographics -quit -executeMethod Fpv450.TestRangeScene.Build` exits 0 in 56 s and logs every span's conductors on their hooks (0.00–2.53 mm, the stub's) and every variant's count. The rewritten `TestRange.unity` differs in bytes only because the builder draws new random fileIDs: QA's `qa_scene_diff.py` (every local fileID replaced by the referenced object's name, documents compared as multisets) finds **0 documents differ** in 1 505, and flags a 1 mm edit planted in one instance's x (the known-wrong check). The working copy was restored after.

QA's `qa_pose.py` on the committed scene: 731 prefab instances for 731 rows, each named `<id> <variant>`, worst 0.0000 mm and 0.00003° against the file.

### Hygiene
- No path under `reference/`, `Library/`, `Temp/`, `Logs/`, `Builds/` or `review-sheets/` in the range. `precommit.py --tree HEAD`: 2115 files, 0 findings.
- Every commit is subject-only under its role's identity, 41–72 characters, except **`c91d0cfa`** (judged below).
- No text object in either scene (`TextMesh` and `m_Text` 0); the only on-screen text is the preset's label for a second.
