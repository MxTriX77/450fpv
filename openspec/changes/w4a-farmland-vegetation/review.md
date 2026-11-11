# QA review: w4a-farmland-vegetation (task 2.1)

**Verdict: fix first.** One defect blocks the merge: **every bush is fitted to its cards' whole quads, not to the leaves and twigs Unity draws.**
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
