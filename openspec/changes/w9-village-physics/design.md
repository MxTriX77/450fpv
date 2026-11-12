# Design: w9-village-physics

Every number is labelled **measured**, **published**, **carried**, **derived** or **assumed**, as in `audit.md`, which holds the findings each task starts from. Each task writes its own section below and leaves the others alone, so the sub-branches merge cleanly.

## 2.4 The soil anchors' reach (G1)

To be written by task 2.4.

## 2.6 The legs on a side slope (G2)

To be written by task 2.6.

## 2.7 The crown twig's repeated break (G5)

To be written by task 2.7.

## 2.8 The plants' numbers for the props

**Built:** `tools/blender/vendor_plant_numbers.py` writes `assets-src/vendor_plant_numbers.json` (W4a's layout: the script beside `vendor_plants.py`, the numbers beside `vendor_plants.json`). It reads the store's prefabs, meshes, materials and base maps directly (no Blender, no Unity), pins each file it read by SHA-256 in the output, and writes only derived numbers (D-089). `--check` re-runs it and compares byte for byte; `tools/blender/tests/test_vendor_plant_numbers.py` re-measures the grass from the store.

### Stems per grass instance

**What a stem is:** one blade or culm as drawn. Each grass prefab is a tuft of alpha-clipped cards (Grass_A 19 cards, Grass_B 157, GrassDry_C 9, …), and each card draws a whole tuft of blades in its texture, 0 to 27 a card. So a card is not a stem: counting cards would undercount 10–20 times.
- **The count (measured):** a horizontal plane cuts every card; along each cut the opaque texels (alpha ≥ the material's cutoff, 0.5 on all ten materials) form runs; runs that meet on an edge shared by two triangles of a card are one stem. The planes stand at 5 %, 10 % … 95 % of the mesh's top, and `profile` keeps every plane's count. A stem at height h is what a disc sweeping at h meets.
- **Stems per instance (derived):** the most any plane crosses. Low down, the clump's blades merge into wide runs; higher up, the short ones have ended. The most is therefore a floor on the blades drawn.
- **Width (measured, as drawn):** each stem's run length along the card at that plane, p10 / p50 / p90. A slanted blade reads wider, and two overlapping blades read as one wide run, which is what the p90 mostly is.
- **Height:** the mesh's top × the layer's height scale (the audit's 1.12 ranges, carried).
- The Burnt prefabs share the Dry prefabs' meshes and base map (only the tint differs), so their numbers are the Dry ones.

| Prefab (layers) | Cards | Stems / instance | At | Width p10 / p50 / p90, mesh (mm) | Drawn height (m) |
|---|---|---|---|---|---|
| Grass_A (0 Green, 10 TallGreen) | 19 | 225 | 15 % | 1.0 / 2.5 / 7.0 | 0.46–0.67; tall 0.93–1.34 |
| Grass_B (1 GreenClump) | 157 | 2,050 | 15 % | 1.0 / 2.5 / 7.0 | 0.45–0.62 |
| Grass_C (2 GreenClump) | 43 | 614 | 10 % | 1.6 / 4.5 / 8.4 | 0.60–0.84 |
| Grass_D (3 GreenClump) | 55 | 566 | 5 % | 1.2 / 4.0 / 7.5 | 0.67–0.97 |
| GrassDry_A, GrassBurnt_A (4, 7, 12) | 14 | 179 | 15 % | 0.6 / 1.4 / 5.4 | 0.53–0.90; tall 1.17–1.59 |
| GrassDry_B, GrassBurnt_B (5, 8) | 18 | 210 | 25 % | 0.6 / 1.2 / 3.5 | 0.44–0.81 |
| GrassDry_C, GrassBurnt_C (6, 9, 11, 13) | 9 | 110 | 15 % | 0.7 / 1.6 / 6.3 | 0.68–1.15; tall 1.22–1.77 |

The `layers` rows give each of the 14 layers its stems per instance, its drawn height and its width range scaled by the layer's width scale.

**The hand count (measured, 2026-11-12):** each card's clipped texture was drawn with the plane's cut on it, and the blades crossing the line counted by eye.
- GrassDry_C at 15 %: card 0, script 14, by eye 15–17 (a central bundle of 4–6 culms reads as 2–3 runs); card 5, script 20, by eye 16–20.
- Grass_A at 15 %: card 4, script 13, by eye 13–15. The small clover and leaf cards (6 of 19) cross nothing at that height, as the script says.
- So the script is within about 15 % of the eye, low where culms bundle.

**Sanity (derived):** Grass_A at the Green layer's mean 2.36 instances a 0.954 m² cell gives about 560 stems/m², against K§7.2's assumed 400 for meadow (carried). Grass_B's 2,050 a cell is a dense sward, inside the 1,000–5,000 tillers/m² of a grazed sward (assumed, order of magnitude; not looked up for this task).

### Leaf area density of the crowns

**What a leaf is:** the drawn opaque area of a foliage card, one side, by its alpha. Each foliage base map was looked at and labelled (assumed, by eye; `CARD_KIND` in the script):
- **leaf:** IslandTree's leaves and dry leaves, GreenBush, LeafyBush, RedBush;
- **needle:** the pines' branch sprays;
- **twig, no leaf:** IslandTree's twig tubes (bark, drawn whole), DeadBush's and SmallBushDry's bare twigs. These are reported as `twig_card_area_m2` and add nothing to the leaf area.

**The area (measured):** per triangle, its 3D area times the share of its UV triangle's texel centres that are opaque.

**The crown's volume (derived):** the union of the variant's twig-shell spheres in its committed metadata (W4a), integrated on 1 cm rows. That is the volume where the props already meet `twig`, so 2.3 can apply the density inside the same shell. 96.6–100 % of the drawn leaf lies inside it (`leaf_inside_shell`).

| Variant | Leaf area (m²) | Shell (m³) | LAD (m²/m³) |
|---|---|---|---|
| IslandTree / _Dry | 391.5 / 228.6 | 447.6 / 379.1 | 0.88 / 0.60 |
| Pine002_S2, _M (= _M_Dry), _L | 43.8, 99.3, 95.1 | 21.0, 64.1, 49.5 | 2.09, 1.55, 1.92 |
| Pine004_1 (= _1_Dry), _3 | 300.2, 133.2 | 273.3, 76.3 | 1.10, 1.75 |
| Pine006_01 (= _01_Dry), _02 | 9.2, 10.1 | 12.7, 48.4 | 0.73, 0.21 |
| GreenBush (= _Dry) | 12.2 | 12.5 | 0.98 |
| LeafyBush_P2 (= _Dry) | 25.2 | 26.6 | 0.95 |
| RedBush_Dry | 9.2 | 8.1 | 1.13 |
| PineDead_02, DeadBush_4 and _6, SmallBushDry_A and _B | 0 | 0.6–88.1 | 0 (twigs only) |

**Range:** 0.21–2.09 m²/m³ over the 16 leaved variants; the pines 0.21–2.09, the deciduous ones 0.60–1.13. Each _Dry twin uses its green twin's mesh and alpha, so it has the same numbers (its leaves are drawn brown, not fewer), except IslandTree_Dry, which draws fewer leaf cards.

### Limits: cards are not botany

- **A drawn blade is not a botanical stem.** A texture draws a tuft photo flattened onto a card: blades that overlap in the photo are one run, so the count is low. The crossed cards of a tuft draw the same photo twice from two angles, so it is high there. The net is about ±30 % on a real tiller count (assumed). The physics should take the count as "what is drawn", true to REAL-05, and not as botany.
- **The widths are the drawn ones.** p50 of 1.2–4.5 mm on the mesh is of the order of real blades and culms (audit §5.1's published rows). The physics should take each stem's diameter and wall per kind from §5.1 (published) and use these widths only as a check.
- **Leaf area is one side of the drawn silhouette.** Leaves that overlap within a texture, and leaves drawn foreshortened, are missing, so the drawn area is lower than a real crown's.
  - Tree crowns' LAD is commonly 0.5–2.5 m²/m³ (assumed, from general knowledge; not looked up for this task).
  - The drawn numbers sit in that range's lower half. 2.3 should look up and cite its own source. A card's drawn twig, under its leaves, counts as leaf (a few per cent, assumed).
- **Labels:**
  - measured: the counts, widths, areas and volumes;
  - derived: stems per instance (the most of the profile) and LAD (area over the shell);
  - assumed: the card kinds, which are by eye, and the shell as the crown;
  - for the physics, assumed: everything about stiffness, mass, wall and shear, which is §5.1's.

## 2.1 Village's stem field

To be written by task 2.1.

## 2.2 Stems on the props

To be written by task 2.2.

## 2.3 Leaves in crowns

To be written by task 2.3.

## 2.9 Village's ground rows and the liftoff harness

To be written by task 2.9.

## 2.10 Stems on the legs and the fiber

To be written by task 2.10.

## 2.5 Cut bits on camera

To be written by task 2.5.
