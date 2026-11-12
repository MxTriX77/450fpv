# QA review: w4a-farmland-vegetation (task 2.1)

**Final check (after 1.3): merge.** The three should-fixes are closed: no pushed point lies within 0.1 mm of a vendor vertex, the trunk columns stand on their stems, and the rebuild test covers every family. The merged tree passes every check. See [Final check (after 1.3)](#final-check-after-13) at the end.

**Verdict after the re-review (1.2): approve.** Both required fixes and the three should-fixes are closed, the branch merges cleanly onto the line, and nothing pushed can rebuild a vendor mesh. Three should-fixes remain, none blocking: the first is single vendor vertices copied into the snag lines. See [Re-review (after 1.2)](#re-review-after-12) at the end; the first pass follows as it was.

**First pass (1.1): fix first.** One defect blocks the merge: **every bush is fitted to its cards' whole quads, not to the leaves and twigs Unity draws.**
- **What:** all 9 bush variants (1,352 of the map's 1,646 plants) take their base map from a TIFF whose 4th channel is the opacity. PIL opens these files as RGB, because their `ExtraSamples` tag says "unspecified", and drops that channel. `lib.vendor.Alpha` then has no alpha, and its `opaque()` returns True everywhere, without a word.
- **Unity does use that channel:** every one of these textures imports it as alpha (`alphaUsage: 1` in its `.meta`), and the friend's leaf shader clips at `base.a - _Cutoff`. Only 2–18 % of these textures pass their cutoff, so most of each card's area is fitted as foliage that isn't drawn.
- **The effect, measured** by rebuilding the 9 with the channel read (E5): the porosity rises by 0.05–0.10 on every bush, the crown boxes narrow by up to 0.38 m, and LeafyBush_P2's shell halves, from 97 pieces to 50.
- **The sheets show it:** VF06–VF10's "as drawn" rows draw solid quads (the dead bush as dark shards), and the white samples fill whole rectangles. Design §8 says the Terrain Sample bush's base map "has no alpha and imports none". Its file has four channels and its `.meta` imports the fourth as alpha.

The rest holds:
- **The trees are right.** Their alpha comes from Unity's own texture assets, which `lib.vendor` decodes correctly.
- **The rebuild is byte for byte:** all 22 variants, from the store (E1).
- **D-089 holds:** nothing pushed lets anyone rebuild a vendor mesh (E8).
- **The 22 variant ids are exactly the merged placements' `variant` ids** (E6).
- **The crown rows pass:** 0 tunnels and 0 lost pairs, with the crowns' drag and prop strikes logged (E4).
- **The AST-05 exception (D-096) is reported honestly** per variant, by the build and the design table (E1).

**Required before merge:**
1. **Read the bush alpha** (`tools/blender/lib/vendor.py`, `vendor_plants.write_bundle`).
   - Take the TIFF's 4th sample as alpha when the texture's import does (`alphaUsage: 1`), and pin the `.meta` that this reads.
   - **Refuse** a material that clips (cutoff found) when its base map gives no alpha, instead of treating it as opaque.
   - Use the same reader for the review bundle's PNGs.
   - Rebuild the 9 bush variants and re-render VF06–VF10's sheets.
   - Correct design §5, §8 (the Terrain Sample sentence) and the per-variant table.
   - A test: a 4-channel TIFF with an unspecified extra sample reads its alpha.
2. **Make the crown rows prove the crown is felt.**
   - Today a row passes on `p.Touch && p.FirstObject == variant && p.Tunnels == 0 && p.Lost == 0`, which a first touch of the trunk satisfies. A known-wrong model, with the shell stripped out, still passes (E4b).
   - Require twig contacts for the four foliage trees, and a deadwood first touch for PineDead_02, as the row's own summary says.

**Scope:**
- `unity/physics/physics-integration-2--w4a-vegetation` at `1081ba2f`, task 1.1 and the proposal.
- The diff against `origin/unity/physics/physics-integration-2` is 88 files:
  - the params and `assets-src/vendor_plants.json`
  - `tools/blender/{vendor_plants.py, vendor_review.py, lib/plant.py, lib/vendor.py}` and its tests
  - `tools/flightbench/ContactsCase.cs`
  - 22 metadata files with their `.meta` (10 folders)
  - the change folder
- No file under the sim package changed, so `dotnet test tools/simtests` was not needed.
- The branch was cut before W2 merged. It doesn't contain `849da02d`, but it merges with it cleanly (`git merge-tree`: no conflicts).

**How I checked:**
- Every command below was run by me in this worktree.
- Scratch scripts stayed in my session's scratchpad. The bush experiment (E5) builds in memory and writes nothing. E4b changed two metadata files for one bench run and restored them with `git checkout`.
- The worktree is clean apart from this file.
- I didn't open the store's project in Unity, and I rendered nothing on the GPU: the 44 sheets I looked at are the asset artist's. Each one's bundle carries metadata identical to the committed file (E7).

## Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| **Collision follows the trunk** | **Failed as written; accepted as D-096's measured exception** | Only Pine002_S2 holds 3 cm on its members of 5 cm or more (26 mm). The others miss by 2–3 mm (Pine006) up to 526 mm (Pine002_L's root flare), and the island trees by 493 mm at their lobed base and fork. The build prints both AST-05 lines for every variant, refuses nothing on them, and the numbers match the design table (E1). The sheets show the capsules at 1.5, 10, 50 and 200 m (E7) |
| **A crown is felt** | **Passed** (the row's assertion is weak: required fix 2) | 5 rows at 10 m/s, seeded: 0 tunnels, 0 lost, `contacts: PASS`. The log shows 127–271 twig contacts taking 30.5–83.9 N·s on the four foliage trees, and 15–26 prop strikes; PineDead_02's 7 prop strikes are on its deadwood (E4) |

## Requirements

| ID | Status | Why |
|---|---|---|
| AST-05 | **Partly met** | Met: capsules and spheres with a material per shape (`bark`, `deadwood`, `twig`); snag records on twigs, branch ends and sprays; wind volumes with a measured porosity; LOD switch distances; nothing landable. Not met: ±5 cm on scanned wood, accepted under D-096; the bushes' shell and porosity (required fix 1). No gaps in crowns (design risk 3) |
| AST-09 | **This change's share met** | The dead and broken variants (PineDead_02, the dead bush) carry `deadwood`; crowns carry twig hooks. The look, the sway and the belts are not this change's |
| AST-12 | **Partly met** | 44 sheets: day and overcast, 1.5 / 10 / 50 / 200 m, turntable, collision, wind and snag overlays, the samples. VF06–VF10's "as drawn" rows draw solid quads (required fix 1). No reference side-by-side: the vendor look is adopted as it is (D-088), so it doesn't apply |
| PHY-05 | **This change's share met** | Twigs under 10 mm are `twig` snag records, sprays are hooks, and the crown rows log prop strikes with the material struck. The cut energy is the sim's (X-6) |
| REAL-05 | **Partly met** | Trees: the collision overlay follows the drawn wood within D-096's exception, and the shells wrap the drawn needles and leaves. Bushes: fitted to quads, so their boxes and shells reach into air the cards don't draw, up to 0.38 m wider than with the alpha read (required fix 1). No gaps in crowns (risk 3) |

## Decisions

- **D-089 (nothing pushed copies vendor geometry):** met (E8).
  - The pushed metadata holds 4,861 capsules, 1,661 spheres, 27 wind boxes and 7,166 snag polylines, plus bounds, LOD names and triangle counts.
  - No triangle, vertex array, UV or texel is pushed.
  - The snag polylines run through ring centres 10 cm apart and along card centre lines: 113–6,535 points per variant.
  - From these files someone could rebuild a stick-and-ball figure of each plant, not its mesh: no surface, no bark lobes, no card layout, no texture.
  - The params push store paths and SHA-256 digests only.
  - `review-sheets/` (where the mesh and textures are copied) and `vendor/` are ignored, and 0 files under them are tracked.
- **D-096 (AST-05 exception on scanned vendor wood, to be recorded):**
  - The build reports and never refuses on it: `audit_wood` returns no problems, only the lines.
  - The per-variant numbers are honest. The rebuild prints them and they equal the design table (E1).
  - I saw the misses on the sheets: Pine002_L's root flare stands as round capsules up to half a metre outside the drawn roots, and the island tree's lobes stand out at its fork.
- **The 8-pair limit and the coarse shells** are reported, not fixed:
  - `ContactModel.PairsPerShape` is still 8, and the sim is untouched.
  - The build prints `OVER the pair rule` for the 9 variants that keep 7–11 shell pieces within a prop's reach. My recount from the committed files gives the same 9 and the same numbers (E9).
  - Design risk 4 hands both to the physics engineer.
- **D-088 (as it is):** nothing in the store is written. The generator reads it, and pins the prefab, the mesh and the foliage materials and base maps.

## Should fix (not blocking; for the fix round or the next unit)

1. **The pins don't cover every store file read.** These are read unpinned:
   - the shader source, which decides whether a material clips
   - the mesh's `.meta`: `globalScale`, `useFileScale` and the fileID name table
   - the texture `.meta` files: their guids, and after fix 1 the alpha import
   - the LOD1+ meshes (their triangle counts are in the metadata)

   The frame check catches a scale or transform change, and the byte-for-byte rebuild catches the rest, but only when someone runs it. The test runs one variant (SmallBushDry_A).
2. **Design risk 1 is out of date.**
   - W2's merged rows carry `scale` per axis, from 0.46 to 1.60 (x = z ≠ y), and a `rotation_xyzw`. The metadata is at scale 1.
   - The rows name a `kind` (tree or bush) and a `variant`, with no `class` and no `asset`. `vendor_plants.py`'s docstring says MapWorld finds a file "by the map row's class, asset and variant", which is true of today's `fpv450-placements` loader, not of W2's package.
   - W3 needs the variant → `vegetation/VFxx` lookup from `assets-src/vendor_plants.json`. The ids are unique there, and the test asserts it.
3. **The crown box reaches the ground when one sample does.**
   - On Pine002_L and Pine002_M, a twig vertex or shell piece at the root gives `lo.y = 0`.
   - So these trees get no trunk column, and their bare trunk zone takes the crown's porosity.
   - This is minor. It can go with the next fit.

## Evidence

**E1. The rebuild, all 22 variants, from the store.**
```
python tools/blender/vendor_plants.py all          (16:00 → 16:45, real 44m23s)
refused 0 · problems 0 · wrote 22 · "frame: every LOD renderer's box matches … within 2 mm" 22 · "OVER the pair rule" 9
exit 0
git status --porcelain   →   (only this review.md, untracked): all 22 metadata files byte-identical to the commit
```
The audit lines, per variant: lib.audit.run's findings over 5 cm, capsule → mesh at most, and mesh → collision at most (mm); then the spec's 3 cm on the capsules of members 5 cm or more across.

| Variant | AST-05 over 5 cm · cap→mesh · mesh→cap (where) | Spec 3 cm (capsules) | Design table |
|---|---|---|---|
| IslandTree, _Dry | 81 · 493.4 · 511.0 (y −0.06, the base) | 493.4 (170) | equal |
| Pine002_S2 | 0 · 25.8 · 31.5 | 25.8 (10) | equal |
| Pine002_M, _Dry | 5 · 141.5 · 78.7 (y −0.08) | 141.5 (133) | equal |
| Pine002_L | 41 · 525.9 · 293.3 (y +0.02) | 525.9 (288) | equal |
| Pine004_1, _Dry | 1 · 42.0 · 54.7 (y −0.64) | 42.0 (35) | equal |
| Pine004_3 | 2 · 69.3 · 108.4 (y −0.25) | 69.3 (24) | equal |
| Pine006_01, _Dry | 0 · 33.0 · 24.9 | 33.0 (4) | equal |
| Pine006_02 | 0 · 32.0 · 22.9 | 32.0 (5) | equal |
| PineDead_02 | 12 · 169.4 · 171.1 (y −0.03) | 169.4 (45) | equal |
| DeadBush_4 | 3 · 63.1 · 141.1 (y −0.04) | – (0) | equal |
| LeafyBush_P2, _Dry | 3 · 59.6 · 38.0 | – (0) | equal |
| DeadBush_6, GreenBush(_Dry), RedBush_Dry, SmallBushDry_A/B | "audit: no wood capsules" | – | equal |

Every farthest-wood point over 5 cm lies at the ground (y −0.64 to +0.02), as design §"Per variant" says. The metadata-derived columns of the design table also match the committed files, for all 22 variants: capsules, shell pieces, porosity, edges and hooks (a script over the files: 0 mismatches).

**E2. Tests.**
```
python -m pytest tools/blender/tests/test_vendor_plants.py -q
9 passed in 5.03s                       (the store is linked, so the SmallBushDry_A rebuild test ran, not skipped)
python -m pytest tools/blender tools/assets -q
1156 passed in 1837.63s (0:30:37)                        exit 0
```
I didn't run the full `pytest tools` (about 45 minutes): the branch changes only `tools/blender`, `tools/flightbench` and data, and the fix round's QA pass will run it.

**E3. Licences, hygiene, spec.**
```
python tools/assets/fetch.py --verify                      OK                                        exit 0
python tools/hygiene/precommit.py --tree HEAD              3039 files scanned, 0 findings            exit 0
openspec validate w4a-farmland-vegetation --strict         Change 'w4a-farmland-vegetation' is valid exit 0
git log origin/unity/physics/physics-integration-2..HEAD   12 commits as Asset Artist <assets@450fpv.local>, 1 as Orchestrator;
                                                           subject only (53–69 chars), 0 body lines, no trailer
git ls-files review-sheets vendor                          0 files
```

**E4. The crown rows.**
```
dotnet run -c Release --project tools/flightbench -- --contacts        (real 1m38s, exit 0)
the farmland crown (IslandTree, 10 m/s at 6.98 m), felt, no tunnel, no pair lost: 127 twig contacts taking 73.63 N·s, 18 prop strikes (twig); first touch 'prop_fr' on 'IslandTree' (twig) by PropStrike at 11.21 m/s; no crash; 0 tunnels, 0 lost: PASS
the farmland crown (Pine002_M, 10 m/s at 5.74 m) …: 195 twig contacts taking 83.92 N·s, 24 prop strikes (twig); first touch 'skid_left' (twig); no crash; 0 tunnels, 0 lost: PASS
the farmland crown (Pine004_1, 10 m/s at 9.93 m) …: 271 twig contacts taking 30.51 N·s, 15 prop strikes (bark, twig); first touch 'prop_fl' (twig); crash by 'leg_fl' on bark at 2.81 m/s; 0 tunnels, 0 lost: PASS
the farmland crown (Pine006_01, 10 m/s at 3.24 m) …: 164 twig contacts taking 68.92 N·s, 26 prop strikes (bark, twig); first touch 'prop_fr' (twig); no crash; 0 tunnels, 0 lost: PASS
the farmland crown (PineDead_02, 10 m/s at 5.84 m) …: 0 twig contacts, 7 prop strikes (deadwood); first touch 'prop_fr' (deadwood); crash by 'arm_fr' on deadwood at 7.90 m/s (589 g); 0 tunnels, 0 lost: PASS
contacts: PASS
```
The rows are seeded and deterministic, so there is no false-failure rate to size. The PineDead_02 crash is the drone flown straight into a bare trunk at 10 m/s: pilot error by construction, not a physics bug, and no tunnel.

**E4b. The crown rows against a known-wrong model.**
- I removed every `twig` shape from IslandTree, Pine002_M, Pine004_1 and Pine006_01 (274→171, 448→347, 805→689 and 97→53 shapes) and ran the bench again.
- Then I restored the four files with `git checkout` (status clean afterwards).
```
IslandTree … 0 twig contacts, 0 prop strikes; touched nothing; 0 tunnels, 0 lost: FAIL
Pine002_M  … 0 twig contacts, 6 prop strikes (bark, ground); first touch 'skid_right' (bark); crash on bark at 9.58 m/s (422 g); 0 tunnels, 0 lost: PASS
Pine004_1  … 0 twig contacts, 15 prop strikes (bark); first touch 'prop_fl' (bark); crash on bark at 3.48 m/s; 0 tunnels, 0 lost: PASS
Pine006_01 … 0 twig contacts, 11 prop strikes (bark, ground); first touch 'prop_fl' (bark); crash on bark at 9.72 m/s (395 g); 0 tunnels, 0 lost: PASS
contacts: FAIL   (the IslandTree row alone)
```
Three of the four foliage rows pass with no crown at all, which is required fix 2.

**E5. The bush alpha: the cause and its size.**
- **Each base map is a 4-channel TIFF, but PIL drops the fourth channel.**
  - `Image.open` gives mode `RGB` for every bush map, with rawmode `RGBX`, `RGBX;16L` or `RGBX;16N`.
  - Their tags: `SamplesPerPixel 4`, `ExtraSamples (0,)` (unspecified), 8 or 16 bits.
- **Unity reads that channel as alpha.** Each one's `.meta` has `alphaUsage: 1` and `grayScaleToAlpha: 0`; `BushDry_BaseColor.tif` is the same.
- **The friend's leaf shader clips on it.** `FarmLandTreeLeaves.shader` calls `clip(base.a - _Cutoff)` in all three passes, and every bush material uses that shader with `_Cutoff` 0.3 or 0.5.
- **The channel is the opacity mask.** It reads as leaf and twig silhouettes (viewed). Its share at or above the cutoff:

  | Texture | Share passing the cutoff |
  |---|---|
  | GreenBush | 12.0 % |
  | RedBush | 10.5 % |
  | qilgP2 | 17.5 % |
  | BushDry | 12.7 % |
  | Bush_Dead_Br_01 | 2.1 % |

  The trees' maps are Unity `Texture2D` assets (`.dds.asset`) or an RGBA PNG. They read their alpha correctly: 77–87 % of their texels are below 128.
- **The experiment:** `vendor_plants.build` again, with `lib.vendor.Alpha` reading the TIFF's 4th sample as alpha. It ran in memory and wrote nothing.

| Variant | Drawn samples, committed → alpha read | Shell pieces | Porosity | Crown box (m) |
|---|---|---|---|---|
| DeadBush_4 | 334,683 → 30,163 | 7 → 5 | 0.416 → 0.518 | 3.62 × 3.02 × 3.57 → 3.43 × 2.94 × 3.42 |
| DeadBush_6 | 707,884 → 43,068 | 19 → 18 | 0.497 → 0.591 | 5.03 × 4.96 × 7.20 → 4.65 × 4.64 × 6.97 |
| GreenBush (+_Dry) | 154,644 → 37,387 | 6 → 6 | 0.512 → 0.563 | 2.90 × 1.93 × 3.29 → 2.83 × 1.87 × 3.21 |
| LeafyBush_P2 (+_Dry) | 175,177 → 48,175 | 97 → 50 | 0.450 → 0.499 | 5.02 × 3.09 × 5.30 → 4.69 × 3.06 × 5.16 |
| RedBush_Dry | 109,902 → 23,931 | 13 → 12 | 0.469 → 0.525 | 3.52 × 1.71 × 2.85 → 3.42 × 1.67 × 2.80 |
| SmallBushDry_A | 34,071 → 8,233 | 6 → 6 | 0.406 → 0.487 | 1.46 × 1.61 × 1.17 → 1.32 × 1.56 × 1.02 |
| SmallBushDry_B | 56,229 → 11,455 | 7 → 6 | 0.480 → 0.553 | 1.92 × 1.75 × 1.72 → 1.71 × 1.73 × 1.59 |

- The snag hooks change too: GreenBush 93 → 86, RedBush 85 → 74, DeadBush_4 558 → 544.
- DeadBush_6 stays over the pair rule (10 at a prop), and LeafyBush_P2 too (7). These remain reported items for W3.

**E6. The variant ids.**
- On `origin/unity/physics/physics-integration-2` (`849da02d`, W2 merged), `maps/farmland/placements.json` lists 22 variants of `kind` tree or bush.
- Those 22 equal the 22 metadata files' variants exactly: none missing either way.
- Instances: 294 tree and 1,352 bush. Each variant's count equals design §1's table, and every row's `kind` equals its variant's.
- No row or variant carries `class`.
- The rows carry `scale` per axis and `rotation_xyzw` (should fix 2).

**E7. The review sheets: all 44 looked at.**
- I looked at all 22 day sheets at full size, and the overcast ones in montages. I zoomed on the island tree's fork and on Pine002_L's root flare.
- Every bundle's `metadata` equals the committed file (22 of 22), so the sheets show what is pushed.
- **The trees:** the capsules follow the trunks and limbs. The visible misses are the island tree's lobes at its fork and base, and Pine002_L's round capsules over its flat spreading roots: D-096's exception, sized as E1 says.
  - The shells wrap the drawn needles and leaves, standing off by their half depth (0.3–0.7 m).
  - Pine002_S2 (7 pieces) and Pine006_02 (15 pieces at 0.7 m) are coarse envelopes. Pine006_02's wraps a mostly bare dead pine as a solid column (design risk 4, for W3).
  - The branch capsules under pine_004's and pine_006's cards follow the drawn branches. PineDead_02's deadwood capsules follow its bare limbs.
  - The dry copies' physics equals the green copies', apart from the material names (checked in the files); IslandTree_Dry has its own mesh.
- **The bushes (VF06–VF10, 9 variants):** the "as drawn" rows draw opaque quads, and the samples fill whole card rectangles. Required fix 1.

**E8. D-089.**
- A scan of every key in the 22 files finds only these:
  - `collision[]`: shape, center, radius, height, rotation, material, landable
  - `snag[]`: kind, points, radius_m, material
  - `wind_volumes[]`: shape, center, size, rotation, porosity
  - `lods[]`: mesh, tris, switch_distance_m, thin_wire
  - bounds, materials, ids, `params_sha256`, and empty `gaps`, `holes`, `ports`, `wires`, `micro`, `textures`
- **Totals:** 4,861 capsules, 1,661 spheres and 7,166 snag records. Their points per variant run from 113 to 6,535. For comparison, LOD0 meshes hold 640–17,523 triangles.
- The `.meta` guids are derived from the path. There are 32 `.meta` files (22 files, 10 folders), with no duplicate guid in the repository.

**E9. The pair and cube rules, recomputed from the committed shells** (`lib.plant.most_near` at 0.127 m, and `most_in_cube`):
- Over 6 at a prop:

  | Variant | Pieces at a prop |
  |---|---|
  | IslandTree, IslandTree_Dry | 9 |
  | Pine004_1, Pine004_1_Dry | 7 |
  | Pine004_3 | 8 |
  | DeadBush_4 | 7 |
  | DeadBush_6 | 11 |
  | LeafyBush_P2, LeafyBush_P2_Dry | 9 |

  These are the same 9 variants and numbers as the build's `OVER` lines and design risk 4.
- In a 2 m cube, at most 55 (LeafyBush_P2), within the 64 rule.
- `ContactModel.PairsPerShape = 8` is unchanged.
- In E4, no row lost a pair on its line.

## Re-review (after 1.2)

**Verdict: approve.** Both required fixes are closed, and the three should-fixes with them.
- **The bushes are fitted to the leaves and twigs Unity draws.** The 9 rebuilt bushes equal the first review's in-memory E5 experiment in every number (R3), and their sheets draw clipped cards (R8).
- **The crown rows now fail a crown-less tree.** With the twig shells stripped, all four foliage rows fail; with PineDead_02's deadwood stripped, its row fails (R5).
- **Every store file the build reads is pinned,** and a flipped pin is refused (R4).
- **New, D-089 vertex reproduction: met, with a should-fix.** No pushed file lets a vendor mesh be rebuilt: not one triangle has all three corners in the pushed points, in this version or in any earlier pushed one. But the snag lines do copy single vendor vertices exactly, 1,252 of them at `0aa509ba` and 2,351 in 1.1's push (R6). The W4b ruins removed every such point after their review; the plants should match that bar (should fix 1).
- **The branch merges cleanly onto the line** (`origin/unity/physics/physics-integration-2`, W1 merged), and the merged result merges cleanly with `origin/unity/main`. So the `.gitignore` and `test_precommit.py` conflict behind `test_existing_tree_is_clean[origin/unity/main merged with HEAD]` is gone once the branch is on the line (R9, by `git merge-tree`; I didn't run that test on the merged tree).

**Scope:**
- `unity/physics/physics-integration-2--w4a-vegetation` at `0aa509ba`: task 1.2's commits `8e0d2cb1`, `7266d50a`, `a21bb7b1` and `0aa509ba`, all as Asset Artist.
- They change `tools/blender/{lib/vendor.py, vendor_plants.py, tests/test_vendor_plants.py}`, `tools/flightbench/ContactsCase.cs`, the 22 params (the new pins), the 22 metadata files, `design.md` and `tasks.md`.
- The 10 tree files other than Pine002_L, _M and _M_Dry changed only in `params_sha256` (the pins), so their 1.1 sheets still show what is pushed.

**How I checked:**
- Every command below was run by me in this worktree. Scratch scripts and logs stayed in my session's scratchpad.
- The pin and alpha experiments built in memory and wrote nothing. The stripped bench runs changed metadata files in place for one run each and were restored with `git checkout`; `git status` was clean afterwards.
- I didn't open the friend's store project in Unity, and I rendered nothing: the 26 sheets I looked at are the asset artist's re-renders. Each bundle's `metadata` equals the committed file (13 of 13).

### Required fix 1, the bush alpha: closed
- **The reader.** `lib.vendor.texture` reads a texture as Unity imports it:
  - `alphaUsage` 0 takes no alpha, 1 takes the file's, and any other import is refused.
  - A TIFF of 4 samples whose 4th is marked unspecified (`ExtraSamples` 0) has that sample read as alpha, where Pillow alone opens it as RGB.
- **The refusal.** `Alpha` refuses a material that clips when its texture gives no alpha as imported; one that doesn't clip draws whole.
- **The review bundle** writes its PNGs through the same reader. Its bush textures now carry E5's shares at the cutoff: GreenBush 12.0 %, RedBush 10.5 %, qilgP2 17.5 %, BushDry 12.7 %, Bush_Dead_Br_01 2.1 %.
- **The tests:** a 4-channel TIFF with an unspecified 4th sample, at 8 and 16 bits, reads its alpha; an RGB PNG, and an RGBA PNG imported with `alphaUsage` 0, are refused under a clipping material and draw whole under a non-clipping one (R2).
- **The rebuild:** the 9 bushes' committed numbers equal E5's in-memory experiment exactly (R3).
- **The design:** §5, §8 (the Terrain Sample sentence) and the per-variant table are corrected. The table matches the committed files: 22 rows, 0 mismatches (R3).

### Required fix 2, the crown rows: closed
- A row now passes only on `p.Touch && p.FirstObject == variant && p.FirstMaterial == felt && (felt != "twig" || twigContacts > 0) && p.Tunnels == 0 && p.Lost == 0`, with `felt` = `twig` for the four foliage trees and `deadwood` for PineDead_02.
- A first touch of the trunk no longer passes, nor a first touch in `twig` with no twig contact after it.
- **Against the known-wrong models (R5):**
  - **Twig shells stripped** from all five trees (IslandTree 274→171 shapes, Pine002_M 448→347, Pine004_1 805→689, Pine006_01 97→53, PineDead_02 515→387): the four foliage rows **fail**. IslandTree touches nothing; the three pines are first touched on `bark` with 0 twig contacts, the three rows that passed this model in the first pass (E4b). PineDead_02's row still passes, on its deadwood, as it should.
  - **Deadwood stripped** from PineDead_02 (515→128 shapes): its row **fails** (first touch on its `twig` shell, 9 twig contacts).
  - In both runs no other row of the 79 changed, and `contacts: FAIL`, exit 1.

### The should-fixes: closed
1. **The pins.**
   - All 22 params pin the mesh's `.meta`, and each foliage slot its shader's source and its base map's `.meta`; every pin matches the store today (R4).
   - Every LOD renderer of all 22 prefabs reads the pinned mesh file (2 or 3 LODs; 1 on DeadBush_6 and the Terrain Sample bushes), and `_lods` now refuses one that doesn't. So the LOD1+ triangle counts come from a pinned file.
   - Known-wrong: flipping each new pin on SmallBushDry_A, and pinning its shader as `''` (outside the store), refuses the build each time (R4).
2. **Design risk 1** matches the line's `maps/farmland/placements.json`: 1,646 tree and bush rows with keys `id, kind, position_m, rotation_xyzw, scale, variant`, scale 0.465–1.599 per axis with x = z ≠ y on every row, no `class` or `asset`. The 22 placed variants equal `assets-src/vendor_plants.json`'s, each unique there. `vendor_plants.py`'s docstring says the same.
3. **Pine002_L and _M stand on a trunk column** (R7).
   - Pine002_M (and _Dry): the crown box now starts at 2.70 m, at the lowest drawn foliage, and a column 0.244 m across stands under it. The root twigs no longer pull the crown box to the ground. The porosity moves from 0.711 to 0.633, which design's table records.
   - Pine002_L: its crown box is unchanged (it started at 2.68 m already), and the column, 0.369 m across, is now the trunk's, not the first tube system's.
   - The column widths are within 2 cm of the straight trunk capsules under the crown (0.260 m on Pine002_M from 0.56 to 2.47 m; 0.376–0.388 m on Pine002_L from 0.34 to 2.18 m). The column's axis stands 5–10 cm off those capsules' axes, because its centre is the mean of the trunk rings under the crown, root flare included. For a wind blocker that is immaterial (should fix 2).
   - The other 19 variants' wind volumes: the 9 bushes' changed with the alpha; the 10 other trees' are unchanged since `416b078f`.

### New: D-089, does any pushed point reproduce a vendor vertex?
- **The method** (`plant_vertex_leak.py`, after the W4b reviewer's `vertex_leak.py`):
  - Each variant's LOD0 mesh is read as `vendor_plants.build` reads it, through the branch's own `lib/vendor.py`: the pinned prefab and mesh, the LOD0 renderer's fileID, its matrix.
  - Every pushed point is measured against its nearest unique LOD0 vertex (Euclidean): capsule centres and both segment ends (rebuilt from centre, rotation, height and radius), sphere centres, every snag polyline point, wind box centres and corners, and the bounds' min, max and corners. `gaps`, `holes`, `ports`, `wires`, `micro` and `textures` are empty in every file.
  - A hit is a point within 2 µm (the files round to 1 µm), and again within 0.1 mm.
  - "Triangles" counts the LOD0 triangles whose three corners are all hit: what a rebuild would need.
- **At `0aa509ba`: 1,252 hits on 1,250 vertices, 0 triangles.** Every hit within 0.1 mm is also within 2 µm: they are copies, not near misses.

  | Variant | Unique LOD0 vertices | Vertices hit | Share | Triangles | What hits |
  |---|---|---|---|---|---|
  | IslandTree | 17,572 | 516 | 2.94 % | 0 | snag hooks' first point 426, last point 90 |
  | IslandTree_Dry | 13,696 | 516 | 3.77 % | 0 | the same |
  | Pine002_S2 | 7,461 | 32 | 0.43 % | 0 | snag hooks' first 18, last 14 |
  | Pine002_M, _M_Dry | 8,954 | 50 | 0.56 % | 0 | snag hooks' first point |
  | Pine002_L | 9,801 | 42 | 0.43 % | 0 | snag hooks' first 32, last 10 |
  | Pine006_01, _01_Dry | 1,368 | 1 | 0.07 % | 0 | a snag hook's last point |
  | PineDead_02 | 8,430 | 26 | 0.31 % | 0 | 28 points: twig hooks' and edges' first points 26, a deadwood hook's last point, a capsule end |
  | DeadBush_4 | 8,423 | 13 | 0.15 % | 0 | snag hooks' first point |
  | SmallBushDry_A | 580 | 3 | 0.52 % | 0 | snag hooks' first point |
  | Pine004_1, _1_Dry, Pine004_3, Pine006_02, DeadBush_6, GreenBush, _Dry, LeafyBush_P2, _Dry, RedBush_Dry, SmallBushDry_B | | 0 | | 0 | none within 0.1 mm |

- **What the hits are:** single corners of single cards, and the root vertex of a few tube systems.
  - Most are the first point of a spray's snag line: `plant.spine` starts it at the drawn sample nearest the trunk, and the sampling lattice includes each triangle's corners, so that sample is often a card corner exactly. A line whose last bin holds one corner sample ends on it.
  - Per mesh piece: on the island tree, 342 cards have 1 corner hit and 87 have 2 of their 4; on Pine002_M and PineDead_02, 1 vertex per piece.
  - No capsule centre, sphere centre, wind box or bounds point is a vertex; one capsule end on PineDead_02 is.
- **Can a mesh be rebuilt? No.** No triangle has all its corners in the files, no piece more than 2 of its vertices, and nothing pushed carries a UV, a normal or the faces' connectivity. With a card's spray line, which lies in the card's plane, someone could guess where a card stood, as the first pass said of the stick-and-ball figure. Not the mesh. So not a blocker by the bar this re-review was given: only hits that let a mesh be rebuilt block.
- **The earlier pushed versions,** checked the same way (the plant metadata was pushed in three commits, all only on this branch; none is on the line or on `unity/main`):
  - `416b078f` (1.1): 2,351 hits, 0 triangles. The quad-fitted bushes hit more: DeadBush_6 651 of 3,180 vertices (20.5 %; 598 of its 795 cards, at most 2 corners each), DeadBush_4 147, GreenBush and _Dry 75, RedBush_Dry 79, LeafyBush_P2 and _Dry 32, SmallBushDry_A 11 and _B 13; the trees as now.
  - `8e0d2cb1` (1.2's alpha fix): 1,252, as now. `7266d50a` (the columns): 1,252, as now.
  - None of them can rebuild a mesh either, so I see no case for rewriting the branch's history. The history decision is the orchestrator's.
- This is should fix 1: W4b's fix round set the bar at no pushed point on a vendor vertex, and the plants miss it by these single corners.

### Standing decisions, not blocked on
- D-096: the scanned wood's AST-05 misses are unchanged (the tree wood didn't change), and reported as in E1.
- The pair rule: 8 variants keep 7–10 shell pieces within a prop's reach (IslandTree and _Dry 9, Pine004_1 and _Dry 7, Pine004_3 8, DeadBush_6 10, LeafyBush_P2 and _Dry 7), my recount from the committed shells (`lib.plant.most_near` at 0.127 m), as design risk 4 says. DeadBush_4 now keeps the rule (5). Every variant keeps the 64-piece cube rule (at most 52, Pine004_3). For the physics engineer in W3.
- The coarse bush shells (5–50 pieces at 0.3–0.7 m): seen on the sheets, as designed.

### Spec scenarios (updated)

| Scenario | Result | Evidence |
|---|---|---|
| **Collision follows the trunk** | **Failed as written; accepted under D-096** (unchanged) | The tree wood is byte-identical to 1.1's apart from `params_sha256`; the rebuild prints the same AST-05 lines (R1) |
| **A crown is felt** | **Passed** | 5 rows, seeded: each first touch in its crown's material, twig contacts on the four foliage trees, 0 tunnels, 0 lost. The row now fails both known-wrong models (R5) |

### Requirements (updated)

| ID | Status | Why |
|---|---|---|
| AST-05 | **Partly met** | Met: everything listed in the first pass, and now the bushes' shells and porosity from the leaves Unity draws. Not met: ±5 cm on scanned wood, accepted under D-096. No gaps in crowns (design risk 3) |
| AST-09 | **This change's share met** | Unchanged |
| AST-12 | **This change's share met** | The bushes' "as drawn" rows now draw what Unity draws; VF02's show the new columns. No reference side-by-side: the vendor look is adopted as it is (D-088) |
| PHY-05 | **This change's share met** | Unchanged; the crown rows now prove the shell is what is met |
| REAL-05 | **Partly met** | Bushes: the boxes, shells and samples follow the drawn leaves and twigs. Trees: within D-096's exception. No gaps in crowns (risk 3) |

### Should fix (not blocking)
1. **Keep vendor vertices out of the pushed points** (R6), as W4b's fix round did for the ruins (`test_no_pushed_point_is_a_vendor_vertex`).
   - The cause: `plant.spine` starts a spray's line at its drawn sample nearest the trunk, and `plant.opaque_samples`' lattice includes each triangle's corners, so the line's first point is often a card corner exactly. A last bin holding one corner sample does the same at the far end. A tube system that starts at one vertex puts that vertex in its twig line or capsule.
   - The fix is small: start the line at its first bin's mean (or leave the corners out of the lattice), and a plant test like W4b's at 0.1 mm. It moves every spray line's first point, and with them the branch capsules on pine_004 and pine_006, so all 22 variants need a rebuild and their sheets a re-render.
   - The earlier pushed versions stay in this branch's history: none can rebuild a mesh, so nothing calls for a history rewrite.
2. **The trunk column's axis** stands 5–10 cm off the trunk capsules (R7). Centring it on the trunk's rings above the flare would put it on the wood. Minor; with the next fit.
3. **The byte-for-byte rebuild test still runs one variant** (SmallBushDry_A), as the first pass noted. The pins now make a store change fail loudly at build time, so this is only about catching a generator change: `vendor_plants.py all` before every push of a generator change does it.

### Evidence

**R1. The rebuild, all 22 variants, from the store.** Run alongside R2's pytest, so slower than the first pass's 44 minutes.
```
python tools/blender/vendor_plants.py all          (23:58 → 01:01, real 62m34s)
refused 0 · problems 0 · wrote 22 · "frame: every LOD renderer's box matches … within 2 mm" 22 · "OVER the pair rule" 8
   (IslandTree, IslandTree_Dry, Pine004_1, Pine004_1_Dry, Pine004_3, DeadBush_6, LeafyBush_P2, LeafyBush_P2_Dry)
exit 0
git status --porcelain   →   (empty): all 22 metadata files byte-identical to 0aa509ba
```
The AST-05 lines equal E1's for every tree, e.g. `IslandTree: … 81 findings over 5 cm; capsule -> mesh at most 493.4 mm; mesh->collision 511.006 mm`, `Pine002_L: … 41 … 525.9 … 293.348`, `PineDead_02: … 12 … 169.4 … 171.062`.

**R2. Tests.**
```
python -m pytest tools/blender tools/assets -q
1159 passed in 2390.08s (0:39:50)                       exit 0     (1.1's 1156 and the 3 new)
python -m pytest tools/blender/tests/test_vendor_plants.py -v
12 passed in 6.02s       (the store is linked: test_a_rebuild_from_the_store_gives_the_committed_text ran)
  test_a_tiff_whose_fourth_sample_is_unspecified_reads_it_as_alpha[8]   PASSED
  test_a_tiff_whose_fourth_sample_is_unspecified_reads_it_as_alpha[16]  PASSED
  test_a_clipping_material_on_a_texture_without_alpha_is_refused        PASSED
```
The new tests against two mutants (in memory, nothing written):
```
mutant A, texture() without the TIFF branch:      the TIFF test [8] FAILED, [16] FAILED
mutant B, 1.1's Alpha (Pillow's RGB, no refusal): the TIFF test [8] FAILED, [16] FAILED;
                                                  the refusal test FAILED (DID NOT RAISE VendorError)
```

**R3. The bushes against E5, and the design table against the files.** From the committed metadata and the R1 log:

| Variant | Drawn samples | Shell pieces | Porosity | Crown box (m) | Snag records | E5 |
|---|---|---|---|---|---|---|
| DeadBush_4 | 30,163 | 5 | 0.518 | 3.43 × 2.94 × 3.42 | 544 | equal |
| DeadBush_6 | 43,068 | 18 | 0.591 | 4.65 × 4.64 × 6.97 | 688 | equal |
| GreenBush, _Dry | 37,387 | 6 | 0.563 | 2.83 × 1.87 × 3.21 | 86 | equal |
| LeafyBush_P2, _Dry | 48,175 | 50 | 0.499 | 4.69 × 3.06 × 5.16 | 329 | equal |
| RedBush_Dry | 23,931 | 12 | 0.525 | 3.42 × 1.67 × 2.80 | 74 | equal |
| SmallBushDry_A | 8,233 | 6 | 0.487 | 1.32 × 1.56 × 1.02 | 8 | equal |
| SmallBushDry_B | 11,455 | 6 | 0.553 | 1.71 × 1.73 × 1.59 | 20 | equal |

```
design_table_qa.py: the per-variant table's wood shapes, shell pieces, porosity and snag edges / hooks
against the 22 committed files → 22 rows, 0 mismatches
```

**R4. The pins.**
```
every params file: prefab, mesh, mesh .meta, and per foliage slot base map and base map .meta → all match the store
   (7 of 7 on the island trees, with two foliage slots; 5 of 5 on the others; 3 of 3 on PineDead_02);
   shader pins: cbeddb6e… (the friend's leaf shader) on every leaf slot, 995c7fa4… on the island tree's twig ribbons
every prefab's LOD renderers (3 LODs; 2 on pine_006 and PineDead_02; 1 on DeadBush_6 and the Terrain Sample bushes)
   read the pinned mesh file: 22 of 22
known-wrong, SmallBushDry_A built in memory with one pin changed each time:
   mesh_meta_sha256 flipped      → refused: …BushDry_A.asset.meta hashes to a6ee27c7…
   shader_sha256 flipped         → refused: …FarmLandTreeLeaves.shader hashes to cbeddb6e…
   shader_sha256 ''              → refused: …FarmLandTreeLeaves.shader hashes to cbeddb6e…
   base_map_meta_sha256 flipped  → refused: …BushDry_BaseColor.tif.meta hashes to 82abb481…
   unchanged                     → built, 6 shapes
```

**R5. The crown rows.**
```
dotnet run -c Release --project tools/flightbench -- --contacts        (real 1m24s, exit 0)
the farmland crown (IslandTree, 10 m/s at 6.98 m), felt first by its twig, no tunnel, no pair lost: 127 twig contacts taking 73.63 N·s, 18 prop strikes (twig); first touch 'prop_fr' on 'IslandTree' (twig) by PropStrike at 11.21 m/s; no crash; 0 tunnels, 0 lost: PASS
the farmland crown (Pine002_M, 10 m/s at 7.09 m) …: 80 twig contacts taking 61.97 N·s, 11 prop strikes (twig); first touch 'prop_fl' (twig); no crash; 0 tunnels, 0 lost: PASS
the farmland crown (Pine004_1, 10 m/s at 9.93 m) …: 271 twig contacts taking 30.51 N·s, 15 prop strikes (bark, twig); first touch 'prop_fl' (twig); crash by 'leg_fl' on bark at 2.81 m/s; 0 tunnels, 0 lost: PASS
the farmland crown (Pine006_01, 10 m/s at 3.24 m) …: 164 twig contacts taking 68.92 N·s, 26 prop strikes (bark, twig); first touch 'prop_fr' (twig); no crash; 0 tunnels, 0 lost: PASS
the farmland crown (PineDead_02, 10 m/s at 5.84 m), felt first by its deadwood …: 0 twig contacts, 7 prop strikes (deadwood); first touch 'prop_fr' (deadwood); crash by 'arm_fr' on deadwood at 7.90 m/s; 0 tunnels, 0 lost: PASS
contacts: PASS          (79 rows PASS)

python strip.py <worktree> twig VF01/IslandTree VF02/Pine002_M VF03/Pine004_1 VF04/Pine006_01 VF05/PineDead_02
dotnet run -c Release --project tools/flightbench -- --contacts        (exit 1)
IslandTree:  0 twig contacts, 0 prop strikes; touched nothing; no crash: FAIL
Pine002_M:   0 twig contacts, 7 prop strikes (bark, ground); first touch 'prop_fl' (bark); crash on bark at 10.51 m/s: FAIL
Pine004_1:   0 twig contacts, 15 prop strikes (bark); first touch 'prop_fl' (bark); crash on bark at 3.48 m/s: FAIL
Pine006_01:  0 twig contacts, 11 prop strikes (bark, ground); first touch 'prop_fl' (bark); crash on bark at 9.72 m/s: FAIL
PineDead_02: 0 twig contacts, 7 prop strikes (deadwood); first touch 'prop_fr' (deadwood): PASS
contacts: FAIL          (only these four of the 79 rows differ from the clean run)
git checkout -- unity/Assets/_Project/Art/Models/vegetation   →   status clean

python strip.py <worktree> deadwood VF05/PineDead_02
dotnet run -c Release --project tools/flightbench -- --contacts        (exit 1)
PineDead_02: 9 twig contacts taking 0.75 N·s, 2 prop strikes (twig); first touch 'prop_fl' (twig); no crash; 0 tunnels, 0 lost: FAIL
contacts: FAIL          (only this row differs)
git checkout -- unity/Assets/_Project/Art/Models/vegetation   →   status clean
```
- The rows are seeded and deterministic: IslandTree, Pine004_1, Pine006_01 and PineDead_02 print E4's numbers to the last digit. Pine002_M moved because its crown box did: it is flown at the box's new middle height, 7.09 m instead of 5.74 m.
- Crash forensics: Pine004_1 (leg on bark at 2.81 m/s, after 271 twig contacts) and PineDead_02 (arm on deadwood at 7.90 m/s) are the drone flown straight into a tree at 10 m/s: pilot error by construction. No tunnel in any run.
- No statistical target here, so no false-failure rate to size.

**R6. D-089, vendor vertices.**
```
python plant_vertex_leak.py <worktree>             (HEAD)      total hits: 2 um 1252, 100 um 1252; 0 triangles
python plant_vertex_leak.py <worktree> 7266d50a                total hits: 2 um 1252, 100 um 1252; 0 triangles
python plant_vertex_leak.py <worktree> 8e0d2cb1                total hits: 2 um 1252, 100 um 1252; 0 triangles
python plant_vertex_leak.py <worktree> 416b078f                total hits: 2 um 2351, 100 um 2351; 0 triangles
IslandTree (HEAD): LOD0 17572 unique vertices, 17523 triangles; 3555 pushed points; nearest 0.0001 mm
    within 2 um: 516 pushed points on 516 vertices (2.94 % of LOD0's), triangles with all 3 corners hit 0;
    by kind {'snag hook (twig) first point': 426, 'snag hook (twig) last point': 90}
per mesh piece (welded): HEAD IslandTree 342 cards with 1 vertex hit, 87 with 2 (of 4);
                         416b078f DeadBush_6 545 cards with 1, 53 with 2 (of 4)
git branch -r --contains 416b078f   →   origin/unity/physics/physics-integration-2--w4a-vegetation only
```

**R7. The wind volumes** (`columns_qa.py`, against `416b078f`):
```
Pine002_M: crown box from y 2.703 (was 0) to 11.473, porosity 0.633 (was 0.711); column y 0..2.703, 0.244 m across
   upright wood capsules in it: r 0.130 from 0.56 to 2.47 m (axis 4.8 cm off the column's), r 0.146–0.179 below (the flare)
Pine002_L: crown box unchanged (2.684..15.505, porosity 0.695); column y 0..2.684, 0.369 m across
   upright wood capsules in it: r 0.188–0.194 from 0.34 to 3.68 m (axes 4.9–10.4 cm off), the flare's below
every other tree: wind volumes unchanged since 416b078f; every bush: changed (R3)
```

**R8. The sheets.** I looked at all 13 re-rendered day sheets (half size, and full-size crops of the bush 1.5 m and sample tiles and of VF02's wind tiles) and the 13 overcast ones in montages. Every bundle's `metadata` equals the committed file (13 of 13).
- **The bushes (VF06–VF10):** every "as drawn" tile, day and overcast, at 1.5, 10, 50 and 200 m and on the turntable, draws the cards clipped to their leaves and twigs, the dead bushes as bare twigs. No solid quad is left. The white samples sit on the drawn leaves and twigs. The shells wrap the drawn bush as round blobs (the coarse shells), and the wind boxes and snag hooks follow it.
- **VF02:** Pine002_M (and _Dry, pixel-identical) and Pine002_L stand on a thin column under the crown box, on the drawn trunk. Pine002_M's root-twig shell piece still sits at its foot, as a shell piece. Pine002_S2 is unchanged.
- LeafyBush_P2_Dry's and Pine002_M_Dry's sheets are pixel-identical to their green copies (design §8's clamped tint); GreenBush_Dry's differs only in tint.

**R9. Merge, licences, hygiene, spec.**
```
git merge-tree --write-tree HEAD origin/unity/physics/physics-integration-2 (4dd71f7a, W1 merged)   → clean, exit 0
git merge-tree --write-tree <that merge> origin/unity/main (3a674244)                              → clean, exit 0
git merge-tree --write-tree HEAD origin/unity/main                                                 → CONFLICT in .gitignore and
   tools/hygiene/tests/test_precommit.py (the branch's base predates W1; the line itself merges cleanly with unity/main)
python tools/assets/fetch.py --verify                      OK                                        exit 0
python tools/hygiene/precommit.py --tree HEAD              3040 files scanned, 0 findings            exit 0
openspec validate w4a-farmland-vegetation --strict         Change 'w4a-farmland-vegetation' is valid exit 0
git log 9f004519..0aa509ba     4 commits as Asset Artist <assets@450fpv.local>, subject only (55–69 chars), no body, no trailer
git diff --name-only 1081ba2f..HEAD | grep reference/ Library/ Temp/ Logs/ UserSettings/ review-sheets/ vendor/   → none
git ls-files review-sheets vendor                          0 files
```

## Final check (after 1.3)

**Verdict: merge.** The three should-fixes are closed, the rest of 1.3 is sound, the rename is complete, and the merged tree passes every check.
- **D-089:** no pushed point lies within 0.1 mm of a vendor vertex: 0 on all 22 files, against LOD0 and against every LOD1+ mesh. The new test fails on the 1.2 files with exactly my 1,252 (F1).
- **The trunk columns** stand on their stems: Pine002_M's and _L's are 0.6 cm and 0.3 cm from the trunk capsule (4.8 and 6.1 cm before). Every crown box is unchanged (F2).
- **The rebuild test** runs one variant per family, 10 cases, and every case rebuilds byte for byte from the store on the merged tree (F3).
- **One note, not blocking**, the one W4b's review made of its own guard. The guard moves a point 0.25 mm off a vertex on each axis, and design §9 publishes that step. So 297 points in 9 variants stand 0.37–0.44 mm from a vertex, most at exactly √3 × 0.25 = 0.433 mm (F1). Each is a single vertex: no triangle, down from 1,252 exact copies. The spine fix removed the rest at the source.

**Scope:**
- `unity/physics/physics-integration-2--w4a-vegetation` at `a4568ca3`.
- Task 1.3, as Asset Artist: `3a185897`, `8ad447d0`, `9e9f64b0`, `271b0275` and `ec4272db`.
- By the Orchestrator: the rename `345f80c1` and the line's merge `a4568ca3` (line at `3230bb48`, W4b merged).
- `a4568ca3`'s tree equals `git merge-tree 345f80c1 3230bb48`: a clean merge, no hand resolution.

**How I checked:**
- I ran every command below in this worktree, with scripts and logs in my session's scratchpad (`qa-w4a-final/`).
- The experiments built in memory or read copies. One bench run had two 1.2 metadata files put back and was restored with `git checkout`. Every other write was byte-identical: the plant and ruin rebuilds, and the sheet runs. `git status` was clean after each.
- I didn't open the friend's store project in Unity.

### F1. Should fix 1, vendor vertices: closed
- **The fix:**
  - `plant.spine` starts a spray's line at its first bin's mean.
  - `_Clear` keeps every capsule end and middle, sphere centre, wind box, snag point and the bounds more than 0.2 mm off any LOD0 vertex on some axis.
  - `test_no_pushed_point_is_a_vendor_vertex` fails on any point within 0.1 mm on every axis.
- **Three probes, all clean on HEAD:** my script, the same check against every LOD1+ mesh, and the new test, which fails on both earlier pushes:
```
python plant_vertex_leak.py <worktree>              (HEAD)   total hits: 2 um 0, 100 um 0      (22 of 22 variants 0)
python plant_vertex_alllods.py <worktree>           (HEAD)   LOD1+ meshes: total hits: 2 um 0, 100 um 0
python plant_vertex_alllods.py <worktree> c6002875           LOD1+ meshes: total hits: 2 um 369, 100 um 369
python vertex_test_at.py <worktree> c6002875   (HEAD's test on 1.2's files)  FAILED: 1252 pushed points on a vendor vertex
python vertex_test_at.py <worktree> 416b078f   (1.1's files)                 FAILED: 2351 pushed points on a vendor vertex
python vertex_test_at.py <worktree> a4568ca3                                 PASSED
python plant_vertex_nearest.py <worktree>  (1 mm search)  within 0.2 mm 0; within 0.5 mm 297 (IslandTree and _Dry 89 each,
   Pine002_S2 30, Pine002_M and _M_Dry 21 each, Pine002_L 16, PineDead_02 29, Pine006_01 and _01_Dry 1 each); nearest 0.366 mm
```
- The 297 are the leftovers design §9 names, which the guard moved: snag records' first or last points (a first or last bin holding one corner sample, a twig line starting at its attachment vertex) and one capsule end on PineDead_02. 0 triangles have all three corners among them.

### F2. Should fix 2, the trunk columns: closed
From `columns_qa.py`, HEAD against `c6002875`:

| Variant | Column across (m), 1.2 → 1.3 | Axis moved | Axis to the trunk capsules above the flare |
|---|---|---|---|
| Pine002_M, _M_Dry | 0.244 → 0.254 | 4.9 cm | 0.6 cm (r 0.130, 0.56–2.47 m); the flare's 4.4–6.6 cm |
| Pine002_L | 0.369 → 0.371 | 6.1 cm | 0.3 cm (r 0.188, 1.06–2.18 m); 3.6 and 9.1 cm below |
| Pine004_3 | 0.559 → 0.579 | 2.3 cm | 0.6–2.6 cm; 3.5 cm at the foot |
| PineDead_02 | 0.342 → 0.342 | 2.9 cm | 1.9–2.4 cm; 7.8 cm at the flare |
| IslandTree, _Dry | 1.299 → 1.299 | 3.6 cm | 3–33 cm: the stem leans (design §7) |

- Every crown box is unchanged, in all 22 files. So are Pine002_S2's column and the bushes', pine_004_1's and pine_006's volumes. The design's numbers match.
- On the sheets, I compared before-and-after crops of the wind tile. Pine002_M's and _L's columns stood beside the drawn trunk and now wrap it.

### F3. Should fix 3, the rebuild test: closed
```
python -m pytest tools/blender/tests/test_vendor_plants.py -v            24 passed in 524.93s, exit 0
  test_every_family_has_a_rebuilt_variant                                    PASSED
  test_a_rebuild_from_the_store_gives_the_committed_text[VF01] … [VF10]      10 PASSED (VF01 278 s, VF03 150 s, the rest 4–22 s)
  test_a_spray_line_starts_at_its_first_bins_mean_not_on_a_card_corner      PASSED
  test_no_pushed_point_is_a_vendor_vertex                                   PASSED
```
These ran on the merged tree, so the line's changes to `lib/meta.py` and `lib/fbxread.py` move no byte of the plants.

### F4. The rest of 1.3: sound
- **The hook and branch counts are the spine change alone.** `spine_experiment.py` builds 5 variants in memory with HEAD's code and again with 1.2's `spine`:
  - Each gives the same number of spray lines both ways.
  - 1.2's spine reproduces `c6002875`'s counts, and 1.3's reproduces HEAD's.
  - What changes: the crossed-card rule keeps fewer lines, and lines just over 1 m drop under the branch length.
```
Pine002_L    600 lines; kept 311 → 277; hooks 415 → 381          (= c6002875 → HEAD)
Pine004_3    548 lines; kept 305 → 304; branches 161 → 146 (269 → 255 capsules); edges / hooks 19/312 → 20/311
Pine006_01    97 lines; kept 74 → 74; branches 43 → 42; hooks 75 → 75
GreenBush    339 lines; kept 86 → 82        DeadBush_6  795 lines; kept 688 → 683
```
- **Everything else is still or sub-millimetre:**
  - Every shell sphere is unchanged, in all 22 files.
  - Wood capsule centres move at most 0.433 mm (the guard), and the bounds don't move.
  - `design_table_qa.py`: 22 rows, 0 mismatches.
- **The crown rows** (`flightbench --contacts`):
  - IslandTree, Pine002_M and PineDead_02 print 1.2's numbers to the last digit.
  - Pine004_1 now takes 252 twig contacts and 30.79 N·s (271 and 30.51); Pine006_01 takes 142 and 75.75 N·s, 38 prop strikes (164, 68.92, 26).
  - **The cause, by experiment:** with those two files put back to `c6002875`, both rows print 1.2's rows character for character. Every other row is unchanged but for a load time. Their only changed shapes are the branch capsules: shells, wood and bounds are identical. So the branches' new starts move the rows.
  - **The known-wrong models, rerun on HEAD** (`strip.py`, restored with `git checkout`):
    - Twig shells stripped from the five trees: the four foliage rows fail. IslandTree touches nothing, and the three pines are first touched on bark with 0 twig contacts. PineDead_02 passes on its deadwood.
    - Deadwood stripped from PineDead_02: its row fails, first touched on its twig shell.
    - Both runs exit 1, and no other row changes.
- **Crash forensics, Pine006_01: pilot error.**
  - The drone is flown at 10 m/s into the pine at 3.24 m.
  - After 142 twig contacts and prop strikes on bark and twig, it loses lift and falls. The canister hits the ground at 6.10 m/s, at 0.15 m (peak 103 g, 44.61 N·s).
  - `on '' ()` is the stub's floor, as the test range's L03 climb row has printed it since 1.2.
  - 0 tunnels and 0 lost pairs. Pine004_1's crash (leg on bark at 2.80 m/s) is as before.
- **The sheets.** I looked at all 44, re-rendered for 1.3: day sheets at half size, overcast sheets in montages, and full-size crops of the row-3 tiles of VF02, pine_004, pine_006, GreenBush, DeadBush_6 and the island tree.
  - Every bundle's metadata equals the committed file (22 of 22).
  - The spray lines run along the drawn cards, the branches start by the trunk, and the hooks follow the bushes' drawn twigs.
  - The shells and crown boxes look as at 1.2. Nothing is missing or broken in any tile.

### F5. The rename: complete
- `git grep vendor_review HEAD` finds W4b's own `tools/blender/vendor_review.py` and `tools/vendor_asset.ps1`, and the first pass's scope line above, a record of 1.1. Nothing else.
- `vendor_plants.py` calls `vendor_plant_review.py`, and design §8 names it.
- Both scripts were run (Blender 5.2, EEVEE):
```
python tools/blender/vendor_plants.py SmallBushDry_A --sheets     exit 0, 34 s: "[review] SmallBushDry_A day … overcast";
   metadata byte-identical; both sheets pixel-identical to 1.3's (0 of 7,077,888 and 0 of 4,718,592 pixels differ)
pwsh tools/vendor_asset.ps1 Debris_RoofSheet_A                    exit 0, 19 s: "15 tiles", "sheet … (2048x2096)",
   "vendor_asset: OK"; metadata byte-identical; the sheet draws the sheet and its collision overlay
```

### F6. The merged tree
```
python -m pytest tools -q                                          1798 passed in 2241.53s (0:37:21), exit 0 (nothing skipped)
dotnet run -c Release --project tools/flightbench -- --contacts    contacts: PASS (78 rows, all PASS), exit 0, 1m17s
dotnet test tools/simtests --filter "FullyQualifiedName~RuinContactTests"   Passed 3, Failed 0, exit 0
python tools/assets/fetch.py --verify                              OK, exit 0
python tools/hygiene/precommit.py --tree HEAD                      3478 files scanned, 0 findings, exit 0
openspec validate w4a-farmland-vegetation --strict                 Change 'w4a-farmland-vegetation' is valid, exit 0
git merge-tree --write-tree HEAD origin/unity/physics/physics-integration-2 (3230bb48)   1507b776 = HEAD's tree, exit 0
                                                                   (the line is an ancestor: a fast-forward)
git merge-tree --write-tree HEAD origin/unity/main (ba2a7f9c)      clean, 2775bf8e, exit 0
                                                                   (the re-review's .gitignore and test_precommit.py conflict is gone)
git log c6002875..a4568ca3 (branch side)   5 Asset Artist and 2 Orchestrator commits, subject only (59–69 chars), no body or trailer
git diff --name-only c6002875..345f80c1, and 3230bb48..a4568ca3    nothing under reference/, Library/, Temp/, Logs/,
                                                                   UserSettings/, review-sheets/ or vendor/; git ls-files of those: 0
```

**Spec scenarios and requirements:** as in the re-review.
- *Collision follows the trunk*: accepted under D-096. The wood moved by at most 0.433 mm.
- *A crown is felt*: passed, and the rows still fail both known-wrong models (F4).
- AST-05: partly met. AST-09, AST-12 and PHY-05: this change's share met. REAL-05: partly met.
