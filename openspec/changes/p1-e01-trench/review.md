# Review: p1-e01-trench (task 2.1)

**Reviewer:** qa-engineer, fresh (did none of the work, D-034) · **Branch:** `unity/art/p1-e01-trench` · **Tip:** `1a7f3ca` · **Range:** `7d54230..1a7f3ca` (27 commits, 98 files)
**Requirements cited:** AST-02, AST-03, AST-05, AST-07, AST-08, AST-10, AST-12, REAL-01, REAL-03, REAL-05, SEC-04, PR-9 · **Decisions:** D-003, D-004, D-020, D-021, D-028, D-038

_In progress: the sections below are filled as the checks finish._

## 2. The hole contract F-1…F-8, run by QA

### The build's own lines (QA's first build, `pwsh tools/build_asset.ps1 E01`, exit 0, 21 min)
```
E01_curving: holes F-1 sealed below: 41813 vertical rays on a 5 cm grid over the bands and 0.71 m past their edges, 0 miss every filler
E01_curving: holes F-2 the cavity inside the bands: 138022 samples of the cut's faces (slots wall), the least 0.6500 m inside the bands at (3.1912, -1.3707, 1.6522), at least 0.65 m
E01_curving: holes F-3 level lips: 1286 samples along the bands' edges, the fillers' top within 0.0 mm of the ground, tolerance 30 mm
E01_curving: holes F-4 nothing proud: the highest filler point at y = 0.0008 m, at most 0.01 m
E01_curving: holes F-5 thick: 269 fillers, every box at least 0.300 m across, at least 0.3 m
E01_curving: holes F-7 surfaces: 269 fillers surface:belt_bare, 0 landable; 324 surface:belt_straw, 324 landable; 24 surface:crater_spoil, 0 landable
E01_curving: holes F-8 no wind obstacle: 0 wind volumes
48 F-5 joint lines, overlap 0.200-0.200 m                      (QA's count over the log)
F-6: 715 audit lines; worst 38.758 mm (mesh->collision at (27.75, 0.0388, -4.25), a margin's relief over its column, tolerance 50 mm);
     worst in the 2 cm zone 16.067 mm (collision[434] near (16.1876, 0.0001, 0.1861), tolerance 20 mm)
```

### QA's own check (`qa_holes.py`, written apart from `lib/holes.py` and `lib/audit.py`)
It reads the shipped `E01_curving.assetmeta.json`. A vertical line meets a box when its plan point lies in the convex hull of the box's eight corners in plan; a filler's top comes from bisection on point-in-box; the band distance is its own. LOD0 is rebuilt in plain Python only for F-2's faces, and its collision, holes and gaps are first checked identical to the shipped file.
```
shapes 617, filler boxes 269, filler non-boxes 0, holes [('run', 50, 0.8, 1.5, 2.3), ('niche_0', 2, 0.8, 1.35, 2.1), ('niche_1', 2, 0.8, 1.35, 2.1)]
F-1 5 cm: 41813 rays within 0.71 m past the bands, 0 miss every filler box
F-1 5 cm +2.5 cm: 41833 rays within 0.71 m past the bands, 0 miss every filler box
F-1 2 cm: 261366 rays within 0.71 m past the bands, 0 miss every filler box
F-3 band edges: 859 samples, filler top worst 0.00 mm off y = 0
F-4: highest filler corner y = 0.00077 m
F-5: thinnest filler box 0.3000 m
F-7: belt_bare not landable 269 boxes; belt_straw landable 320 boxes + 4 cylinders; crater_spoil not landable 24 spheres
F-8: wind volumes 0 snag 0
rebuilt in plain Python (12 s): collision, holes and gaps identical to the shipped file: True
F-2: 15180 samples of the wall slot's faces, the least 0.65000 m inside the bands at (3.1867, -1.2697, 1.6651)
F-2 on the lip vertices (y = 0): least 0.65009 m
F-2 below 0.1 m (niches, walls): least 0.65000 m
F-6 spot check on 2640 random wall points: farthest outside every box 5.8 mm; deepest inside a box 18.2 mm at (17.104, -0.837, 1.717)
```
- **F-1** holds on the contract's 5 cm grid, on a grid offset by half a step and on a 2 cm grid: no sliver between boxes at the mitred joints or the backing's corners.
- **F-2** reaches exactly 0.65 m at both edge cases the brief names: the widest crumbled lip (0.65009 m) and a niche (0.65000 m, `niche_1`'s side at y −1.27).
- **F-6 cross-check of the new clipped audit:** QA's deepest point (a wall point 17.6–18.2 mm inside `collision[202]`, `niche_1`'s far side face inside the next segment's lip box, which reaches 0.117 m back past the joint at that depth because its frame follows the lip line) is the audit's own `collision[202] box: box->mesh 17.798 mm at (17.0823, -0.8383, 1.711), tolerance 50 mm`, 0.687 m from the nearest gap, so outside the 2 cm zone.

### Mutations (`qa_mutations.py`: the generator changed, the build's checks run)
```
M0 unmutated: NOT CAUGHT at build time                      (expected: no problem)
M1 band margin 0.60 (F-2): CAUGHT by lib.holes: F-2 ... the least 0.6000 m inside the bands
M2 backing 0.65 past the band (F-1): CAUGHT by lib.holes: F-1 ... 590 miss every filler, the first at (-1.810, -0.410)
M3 overlap 0.05 a side (F-5 overlap): NOT CAUGHT at build time
M4 fillers 0.25 thick (F-5 thickness): CAUGHT by lib.holes: F-5 thick: ... every box at least 0.250 m across
M5 lip boxes 4 cm proud (F-3, F-4): CAUGHT by lib.holes: F-3 ... within 40.0 mm ... | F-4 ... y = 0.0400 m
M6 the first floor box removed (F-1): CAUGHT by lib.holes: F-1 ... 153 miss every filler, the first at (0.040, 0.240)
M3 under pytest: CAUGHT by test_neighbours_overlap_at_every_joint: joint 1 at 0.89 m, probe (...): 0 boxes straddle it
```
Five of six caught at build time; the sixth (F-5's overlap) only by pytest, because the generator's joint check compares against its own `OVERLAP_M` (S3).
