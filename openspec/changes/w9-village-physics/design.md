# Design: w9-village-physics

Every number is labelled **measured**, **published**, **carried**, **derived** or **assumed**, as in `audit.md`, which holds the findings each task starts from. Each task writes its own section below and leaves the others alone, so the sub-branches merge cleanly.

## 2.4 The soil anchors' reach (G1)

To be written by task 2.4.

## 2.6 The legs on a side slope (G2)

To be written by task 2.6.

## 2.7 The crown twig's repeated break (G5)

**Reproduced** (measured, `ObjectContactTests.AHoverInACrownBreaksNoBladeTwice`, model 85): the drone starts 1.24 m over the ground at (525.79, 612.74), inside GreenBush_Dry's crown, and the pilot holds that point for 25 s with surfaces.json. 150,239 `BladeDamaged` Break events, all with 0 kg lost, 150,231 at a station the blade had already broken at; 8 in every step. **No twig was cut** (0 `BladeStrike` on twig): the candidate, `PropStrikes.Twig`, never ran.

**The cause** (a temporary print, not committed):
- The props never turn in the crown: every rotor is resting (`ContactModel.Resting`), its blades held by the twig shells. So the breaks are `PropStrikes.RestDamage`'s, the resting blades' static loads, not a strike's.
- A resting blade's load radius is its pairs' force-weighted radius, and a pair's point is the blade's core projected onto the face's tangent plane (`ContactModel.Depth`). Deep inside a shell (0.4–0.7 m, the row's own design) that point lies on the shell's surface, not on the blade: **4.48–5.53 R** (measured), 0.4–0.6 m past the tip.
- `Breaks` took that radius as the load's: the tip's station (0.98875) then carried 25–39 N on a 0.44–0.57 m arm, 8–102 times its strength, and broke. The trim left the tip at that station's centre, which `Breaks` still counted (its span half inside), so it broke again every step: the tip unchanged, 0 kg lost. A rest break pays no work, so no energy was taken; only the log and the rotors' rebuild each step paid.

**The fix** (`PropStrikes.cs`, two lines):
1. `RestDamage`: the load's radius is held to the blade's tip (it acts on the blade's material). The flap set's `Worst` reads the same radius.
2. `Breaks`: a station whose centre is not inward of the blade's tip holds nothing beyond it, so it is no break. A tip is only ever 1 or a station's centre, so this removes exactly the no-op breaks, for every caller (the strikes' carve, crush and twig paths too: none can again pay a fracture's work for a break that takes nothing).

**After** (measured, the same 25 s): 1 break logged, at station 34 (0.876 R, its flatwise moment 1.02 of its strength under the shell's 37 N at the tip), taking mass; 0 with 0 kg; 0 at a station already broken; at most 2 `BladeDamaged` in a step. A second break, the same in the other front rotor, happens in the first step, whose events the step's 64-event buffer refuses with 23 others (every pair in the crown made at once); no event is refused after it. The log replays headless bit for bit (25,016 steps, 815 events) and the steps after the first second allocate 0 B.

**The events are bounded** by construction: a blade breaks at most once at each of its 40 stations, so at most 320 breaks a flight for the 8 blades.

**The energy books:** a rest break takes no work, before or after; a strike's break pays the fracture's work only when it removes a section. Holding the radius to the tip only shortens arms, so no load and no work is created.

**W3 2.7's crown flight re-measured** (`ADroneInACrownPaysForItsTwigsAndDoesNotHang`): unchanged to the bit: first touch at 10.89 s, 15 twigs cut for 135.4 J, 0 glances, crashed, 65.41 m from where it hung; its log replays bit for bit (25,016 steps, 906 events). The known-wrong also unchanged (8,502 glances, hangs 4.17 m up). `flightbench --contacts`' farmland bush: unchanged (109 twig contacts, 48.63 N·s, 98.55 m on).

**Goldens:** none moved (`flightbench --golden`: 61 PASS, every case and kept audit bit for bit).

**Model:** ships under 86 with 2.4's. The note's line: *a resting blade's load radius is held to its tip, and no blade breaks at a station at or past its tip (2.7).*

**For 2.3 (leaves in crowns), not fixed here:**
- In a hover inside a crown the props **stand still**: the twig shells hold each resting blade with 25–39 N (the row's 30 N/m per part over 0.4–0.7 m, the same law as a foot's) and its stick friction, more than the motor's torque. So `Twigs` never runs and the drone rests on its stopped props in the bush. A blade inside a shell standing for 7.5 mm twigs at 2 m/m³ should meet a few twigs, not a solid; the shells' push on a blade is the model to revisit with the leaves.
- A resting blade's pair point lies on the shell's plane, so its radius and `BladeStiffness`' station are the shell surface's, not the blade's (`ContactModel`, left alone here).
- The first step inside a crown overflows the 64-event buffer (24 refused here).

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

**Built:** `MapWorldStems.cs` derives the field at load from the package and the plants' numbers; `IWorldQuery.StemsNear` reads it (`QueryVersion` 9). `tools/map/validate.py` checks it, `StemFieldTests` and worldbench's three `farmland.stems.*` cases pin it. The editor check is `Fpv450.DetailInstanceCheck` (`unity/Assets/_Project/Editor/Farmland/`).

### The editor check: are `ComputeDetailInstanceTransforms` the drawn instances?

Run in batchmode on the friend's scene (read only, never saved), Unity 6000.3.26f1 on D3D12, at three 8 m windows: the densest tall cell (768.07, 917.48), Grass_A's densest cell (283.69, 969.24) and the densest cell overall (869.63, 151.86). Measured:
- **The terrain's settings:** `detailObjectDensity` 1, `InstanceCountMode`, 1,024 cells in patches of 32. Every one of the 14 prototypes is an instanced mesh with `useDensityScaling` off, `alignToGround` 0.5 and `positionJitter` 100.
- **The same instances, the same counts:** over 6 patches × 14 layers, the transforms equal the cells' counts in every patch and layer. Each cell holds exactly its count in all but 4 of the 86,016 cells checked; those 4 are two transforms on a cell's edge, each read into its neighbour.
- **The density setting doesn't matter here.**
  - At density 0.5 the transforms are the same as at 1.
  - The terrain drawn at `detailObjectDensity` 0.5 covers as many pixels as at 1, within 0.1 %. Its pixels differ from the density-1 picture by 3.7–6.7 % of those covered, against 1.4–2.3 % between two renders at the same density. The gap is likely more sway between more renders (assumed).
  - With `useDensityScaling` off, the friend's terrain draws every counted instance at any density.
- **The same places, not the same blades:** drawn from straight above, the terrain's own picture and the transforms drawn with each prototype's mesh share every tuft's centre. Their pixels still differ by 27–61 % of the covered union:
  - The yaw is in radians: in degrees, the difference is 63–84 %.
  - The terrain differs from itself between two renders by 1.4–2.3 % of its covered pixels: the shader's wind sways the blades.
  - Tilting each instance halfway to the ground's normal (`alignToGround` 0.5, my reading of it) closes part of the gap: 35 → 27 %, 61 → 55 %, 60 → 59 %.
  - So the struct carries the instance and its place, but not the tilt or the sway the renderer adds. A stem-by-stem copy of the drawn blades can't be had from it.

### Per cell, not per instance

| | Per cell and layer (built) | Per drawn instance |
|---|---|---|
| Data | `grass/map.raw` as exported, 14.0 MiB, plus per layer a table of 45–178 doubles | 2.84 M × (x, y, z, scale xz, scale y, yaw) in float32 = 68 MB (derived), plus a cell index (4 MB) |
| Package | nothing new: the file is already in it and under its SHA-256 | a new LFS file of about 68 MB; the exporter re-run in the friend's scene |
| Load | 46 ms for the read and the SHA-256, measured on a loaded machine; the tables under 5 ms (derived: 14 layers × at most 178 steps × 64 scales) | about 220 ms for the read and the SHA-256 alone (46 ms × 68 / 14, derived), then parsing |
| Player | +14.0 MiB in StreamingAssets (`MapWorld.Load`'s reads carry it) | +68 MB |
| Query, 4 rotor discs | median 3.7 µs, p99 13.5 µs, 0 B (measured, indicative) | about the same: up to 15 instances a cell to test instead of 14 bytes |

**Why per cell:**
- **An instance places a tuft, not its stems.** A tuft spans its mesh's footprint times its width scale: Grass_A 0.31 m × 2.6–3.6 = 0.80–1.12 m, GrassDry_A 0.41 m × 1.2–1.7, GrassDry_C 0.58 m × 1.0–2.2, Grass_B 1.0 m × 1.2–1.7. The footprints are measured from the store's meshes, the products derived. Spread over about a cell, a tuft's pivot says little more about where its 110–2,050 blades stand than the cell does.
- **The drawn pose isn't recoverable anyway** (the check above). Per instance would cost five times the memory and a re-export, and would still not give "what is drawn is what is touched" blade by blade.
- **The count is what 2.2 and 2.10 need:** stems per m² by kind and height, and how many lie under a disc. Per cell gives that from data already in the package.

### The rule

- **Stems per m²** of layer l in cell c = count(c, l) × stems per instance(l) / the cell's area (0.9537 m²).
  - The count comes from `grass/map.raw`.
  - The stems per instance come from 2.8's prefab rows in `assets-src/vendor_plant_numbers.json`.
  - A cell with no drawn grass has no stems.
  - The map only: the surround's grass within 170 m of the edge isn't exported (README), so outside `map_rect_m` there are no stems.
- **The kind is the group's state:**
  - Green, GreenClump and TallGreen → `GreenBlade`.
  - Dry, DryClump and TallDry → `DryCulm`.
  - Burnt and TallBurnt → `BurntCulm`.
  - This departs from the audit's suggestion (Tall* → dry culm): TallGreen draws Grass_A's green material and TallBurnt the burnt tint, so the group's name is the state.
- **The height range** is the drawn tops: `mesh_y_m[1]` × the layer's height scale range. For example, TallDry GrassDry_C reaches 0.6789 × 1.8–2.6 = 1.222–1.765 m.
- **How many reach a height h above the ground:**
  - Per instance, R(h / top), where R is 2.8's count profile turned into its upper envelope: at each plane, the most any plane at or above it crosses. So R never rises with height, and at and below the densest plane it equals the stems per instance.
  - R is linear between the 5 % planes, falls linearly from the 95 % plane to 0 at the top, and is 0 from the tallest top up.
  - The top is the mesh's top × a height scale drawn uniformly over the layer's range. Reach(h) is R's mean over 64 scales at the range's midpoints, tabled every 1 cm and read linearly between entries.

### The query (for the physics engineer's sign-off at 2.2)

```csharp
int StemsNear(XZ centre, double radius, double height, Span<StemPatch> results);
struct StemPatch { int Cell; byte Layer; StemKind Kind; double Area, Density, HeightMin, HeightMax; }
```

- **One circle in x–z and one height above the ground** serves both of the physics' asks:
  - A rotor disc at its height above the ground at the hub: 2.2's stems under the disc that reach it.
  - A leg's foot or a fiber node with its radius at height 0: 2.10's every stem there.
  - The disc's tilt and the ground's slope within a disc are the physics' to fold into the one height. A 0.127 m disc tilted 30° spans ±0.06 m of height, against tops spread over 0.2–0.5 m within a layer.
- **The result per (cell, layer):**
  - `Density`: stems per m² reaching `height`.
  - `Area`: the circle's area inside the cell, exact by four signed quadrant boxes, one Atan2 pair each.
  - The stems under the disc are Σ Density × Area, continuous as the disc crosses a cell's edge.
  - `Kind`, so 2.2 reads each kind's published stem from 2.11.
  - `HeightMin` and `HeightMax`, so the physics knows how much stem stands above the cut. A stem already cut stands lower; that state is the physics', not the world's.
  - `Cell` and `Layer`, so the physics can key that cut state.
- **Its rules:**
  - The order is canonical: row north to south, then column, then layer.
  - It returns the true count on overflow.
  - It returns 0 off the map, on the floor and on the test range.
  - Only + − × ÷, √ and `DetMath.Atan2`; allocation-free and reentrant.
- **Nothing more is built:** no stem positions, no per-stem draw and no wind cover. The cover per cell is W7's follow-up.

### Format and identity

- **No new file and no exporter change.** The field is derived at load from `map.json`'s `grass` block (file, resolution, each layer's prefab, group, `height_scale` and `mesh_y_m`), `grass/map.raw` (checked against the manifest's SHA-256) and `assets-src/vendor_plant_numbers.json` (the prefabs' `stems` and `profile`).
- **The content hash** continues over `vendor_plant_numbers.json`'s text after the material names. The farmland's hash moves from `7249f5a8adb997d7` to `5d305d09f5d09021`; the map.json hash already covered the counts.
- **`QueryVersion` 9.**
  - worldbench's golden files were re-recorded for the version line only: every earlier case of the floor, the test range and the farmland is unchanged, bit for bit, in Release and Debug.
  - `farmland.txt` gains `farmland.stems.cells`, `.random` (10,000 circles, the same bits on four threads) and `.field`.
  - A model-85 farmland log now refuses to replay (it has another query version and content hash), as with any `QueryVersion` bump.
- **`validate.py`** checks, per layer, that the plants' numbers name the same prefab and group in the same row, that the group has a kind, that the stems per instance are the profile's most, and that the drawn heights are `mesh_y_m[1]` × `height_scale` within 1 mm. It also checks that the field's stems over the map equal the plane's instances × its stems per instance. worldbench's `farmland.stems.field` repeats that last check through `StemsNear` itself, at every grass cell: all 14 layers agree.

### The numbers

| Number | Value | Label |
|---|---|---|
| Cell side, area | 0.9765625 m, 0.9537 m² | carried (README) |
| Stems per instance | 110–2,050 by prefab | measured by 2.8, carried |
| Count profile, planes at 5–95 % of the mesh top | 2.8's `profile` | measured by 2.8, carried |
| Height scale spread | uniform over the layer's range | assumed: Unity spreads it by a Perlin noise (`noiseSpread` 0.1) |
| Reach table | 1 cm steps, 64 scales | derived |
| Stems on the map | green blades 403.8 M, dry culms 178.1 M, burnt culms 60.1 M; 642 M in all, a mean of 642 /m² | derived |
| Densest cell | 5,496 /m² at (869.63, 151.86) | derived |
| Kind per group | as above | assumed, from the group's name and material |
| Rotor disc radius in the cases | 0.127 m (a 10-inch prop) | carried |

**At the three golden cells** (derived; stems per m² reaching h, and the stems in the circle):
- **The densest tall cell, (768.07, 917.48),** with 11 drawn layers and a disc of r 0.127 m (0.0507 m²):

  | h (m) | 0 | 0.3 | 0.6 | 1.0 | 1.5 |
  |---|---|---|---|---|---|
  | Stems per m² | 2,345 | 1,299 | 345 | 43 | 2 |
  | In the disc | 118.8 | 65.8 | 17.5 | 2.2 | 0.1 |

  Above 1.0 m, only the tall layers and the tall burnt culms remain.
- **A bare `tilled` cell, (229.98, 257.32):** no stems at any height.
- **The east edge's cell, (999.51, 908.69),** with a circle of r 0.6 m at (999.9, 908.69):
  - 0.685 m² of the circle lies on the map, the closed form's π r² minus the 0.446 m² segment past x = 1000. Those cells hold 2,695 stems at h = 0, 433 at 0.3 m and 15 at 0.6 m.
  - The segment holds none.

**Sanity:** the drawn grass is 236 /m² for one Grass_A instance and 944 /m² for the green edge cell, against K§7.2's assumed 400 for meadow (carried). 2.11's published stand densities will say how far "what is drawn" sits from real swards; 2.8 puts the drawing's count within about ±30 % of real tillers (assumed).

### Costs (measured on this laptop on AC, Balanced plan, best power efficiency mode, with other agents running: indicative)

- **The field's load:** 46 ms to read `grass/map.raw` and check its SHA-256. The whole farmland load was 2,620 ms under that load.
- **Memory:** 14.0 MiB held.
- **A step's four rotor discs at 0–1.8 m:** median 3.7 µs, p99 13.5 µs, 0 B, 3.2 (cell, layer) pairs a query.
- **Four leg points at 0 m:** median 2.2 µs, p99 4.5 µs, 0 B.
- **The worst case** (a disc on a cell corner in the densest field) is 4 cells × 14 layers, 56 bytes read and at most 4 area evaluations. Its maxima (881 µs and 209 µs) are this machine's scheduling under load, as the existing rows' maxima are.

### What 2.2 needs to know

- **Fold the hub's height above the ground into `height`.** Ask once per rotor per step with the disc's radius. The stems the disc meets are Σ Density × Area per kind, a mean count with no draw, as `PropStrikes.Twigs`.
- **Cut state is yours.** Key it by (`Cell`, `Layer`); a cut stem stands at the cut height.
- **Widths and stiffness come per kind from 2.11.** 2.8's drawn widths are only a check.
- **The field ends at the map's edge,** and there is no wind cover from it yet.

## 2.2 Stems on the props

Labels as above: **measured** (run here), **published** (2.11's `stem-properties.md` [n] or a source named here), **carried** (already in the repository), **derived** (computed, the formula given), **assumed** (a named, tunable constant), **numerical** (a resolution or buffer, not physics).

### What the calculation says first

The rotor's slipstream decides most of it, and it is strong.
- **The slipstream is a 19 m/s jet.** Momentum theory (published): the contracted wake moves at 2·v_i, so its dynamic pressure is q = ½ρ(2v_i)² = T/A. At the hover's T = 11.24–11.98 N a rotor (measured over the densest tall cell, 4.735 kg) and A = π·0.127² = 0.0507 m², q = 222–236 Pa (derived), the pressure of a 19 m/s wind.
- **Fine culms lie down in it before any blade reaches them.** A 1.5 mm culm (2.11's green, dry and burnt rows) leans until its top stands at most 0.12–0.16 m over the ground (the lean law below, derived). The discs of a drone standing on its legs are 0.288 m up (measured: the standing origin 0.253 m plus the hubs' 0.035 m). So **a vertical landing into Village's grass cuts no stem under the discs**: the grass is pressed flat under them, as a helicopter's is.
- **Stiff stems stand.** A 3.5 mm straw (the range's `straw` row) still stands 0.44 m and an 8 mm weed stalk 0.83 m (derived).
- **A leaning stem slips off the blade.** The blade's speed across a stem leaning φ is ω·r·cos φ. Long S04 weed stalks under the disc lean 56–76° and meet the blade at 20–60 m/s, under their 48–90 m/s cutting speed: they are pushed aside, not cut (derived, hand calculation D).
- **What is cut** is what meets the blade upright and fast: the stems a moving disc runs into at its leading edge (they reach the blade 3.4 ms after entering the slipstream, against the about 40 ms the slipstream needs to lay them over, derived), short stiff stubble, and stems standing when the rotors spin up among them.
- **This is the pilot's account.** "Straws are noticeably cut in front of my quad's camera": the front discs' leading edges, moving forward as a landing does, cut what stands in front of them, and the pieces leave there. The spec's purely vertical landing into fine grass is the one case where nothing is cut. Most stems escape, as the audit required, and the props never stall in grass.

### The law

Per rotor, per step, after the shapes' passes (`PropStems`, called from `PropStrikes.Apply`; a resting rotor is the contacts', 2.10's):

**1. The stems under the disc.**
- *Village* (the stem field): `StemsNear(hub, R, h)`, h the hub's height over the ground under it (`SampleGround`; the disc's tilt folded into one height, as 2.1 suggests). Each patch gives the stems per m² reaching h, its area in the disc, its kind and its drawn tops. Above h, within a patch, the tops are spread evenly up to its tallest top (assumed: the field holds no finer profile once h is given; it is read once a step).
- *The range* (S04's elements, `MicroDetailNear`): the straws and stems based in the disc, each with its own drawn diameter, length and shape, read into a per-rotor cache when the hub has moved 0.1 m (numerical) and placed exactly.
- Each kind's tissue is its `surfaces.json` cover row at the flight's moisture: diameter, wall share, density, modulus, cut energy. An element's modulus is drawn in its row's range by its own Id's hash, the fiber's rule (world-derived, not a flight draw).

**2. The slipstream lays them over.** The stem's load across it is w = q·C_d·d·cos²φ, q = T/A (derived above), C_d = 1.15 (published [16], [17]: 1.0–1.3), cos²φ the cross-flow's normal share as it leans (assumed: the reconfiguring plant of Gosselin [18]; a load that kept acting across it would lay stems over sooner). The stem bends as a rigid link on a base spring of 4EI/L, which gives the uniform-load cantilever's tip deflection wL⁴/8EI for small leans (derived):
- φ / cos²φ = q·C_d·d·L³ / (8·EI) = κ·L³, and its top stands L·cos φ over its base.
- So h_b = (φ·cos φ / κ)^(1/3): the most any length stands is (0.5611/κ)^(1/3), at φ = 0.860 rad (derived), and the lengths standing to a height h lie between two roots of φ·cos φ = κh³. Both are tabled once at load (256 entries, numerical) and read linearly.
- **The lag:** the stems' load follows the rotor's q with a time constant, 0.04 s pressing them down (derived: a 0.5 m culm's top driven over by w/μ ≈ 500 m/s² gets there in about 44 ms) and 0.1 s rising again (assumed: inside the 0.02–0.35 s quarter periods of 0.2 m stubble and a 0.6 m culm, derived from the first cantilever mode). A rotor spinning down after the throttle's cut has slowed below the cutting speed before the stems stand again (hand calculation A).

**3. The blade must be fast enough.** The critical speed of an unsupported stem, 2.11 §2.2's law (derived): v_c = k_edge·[F_c·√d / (2√(2π)·(EI·μ³)^¼)]^(2/3), F_c = e_s·A_w/d, with the prop's edge factor k_edge = 3 (assumed, 2–4: between a sharp mower blade [13], [11] and nylon line [14]; the blade's nose is 0.13–0.65 mm round, derived [15]).
- A stem at radius r leaning φ is cut where ω·r·cos φ ≥ v_c (derived: the blade's speed across the stem; with Tuck et al.'s 35 m/s upright it gives 83 m/s at 65°, their "over 80 m/s" [11]). Below it the blade pushes the stem aside: nothing is taken (assumed; its bending energy is of the order of 1 mJ against 10–30 mJ for a cut).
- On the field the stems lie evenly over the disc, so the share cut is (r_tip² − r_c²)/R², r_c = v_c/(ω·cos φ) at the reaching lengths' middle, never inside the blade's root (0.1 R, carried).

**4. Each cut's work** (from the rotor):
- **A fresh cut:** E = e_s·A_w + μ·h_f·u², at the blade's speed u = ω·r. The edge severs the wall's section (e_s quasi-static, 2.11 §2.3: an impact multiplier would count the fling twice), and the section's frontal height h_f = c·sin θ + t·cos θ of stem is swept and brought to the blade's speed (the carve's "mass it meets", p7f-damage design 5, carried: the twig's m_f·v² with the band a vertical stem crosses in place of a chord).
- **A slice:** a stub cut before and met again as the disc comes down by Δ costs min(1, Δ/d) of a cut (`Twigs`' rule, carried: a slice thinner than the stem is crushed) and flings μ·Δ·u², at most once per blade pass.

**5. The impulses.** Along the blade's motion the stem takes E/u, the rotor paying it all: ω' = √(ω² − 2ΣE/J), and the body gets the reaction about the axis and the impulse at the cut, as `Twig` gives them. Along the axis, the cut's share of the pitched face, E_cut·(c·cos θ/h_f)/u, pushes the blade up (the carve's `Section` split, carried: 2–4 times the in-plane push at the blade's 8–13°); the fling leaves in the plane.
- **Where:** on the field the stems are spread over the disc's azimuth, so its two blades share them evenly and their in-plane impulses cancel; the rotor slows and is pushed up. The stems a moving disc runs into lie across its leading arc, ∝ cos(ψ − ψ_lead), and each blade cuts what has entered since the last one passed: blade b takes (π/2)·max(0, cos(ψ_b − ψ_lead)) of them (derived; it sums to the entries over a turn). The sum is a steady sideways push plus a rotating one at the blade pass, π·n·J_c/4 (derived, hand calculation C). An element is met by the blade whose sweep crosses its azimuth in the step, once per pass.
- **On the arm (G8):** each corner's strikes' impulse along its rotor's axis loads that arm's mode at its tip in the step it happens (`AirframeModes.Strike`: the modal impulse of a tip impulse with the rigid body's relief, as `Advance` takes the thrust), so the camera sees the arm ring from the hit. For every strike, not only stems.

**6. A stem already cut stands lower.**
- **On the field (as built):** each rotor keeps, per kind, the height H it has cut down to and the stubs it left standing there, S (stems, the mean count). The field is read once a step, at the disc's own height:
  - the stems now reaching the disc beyond S are fresh (come within its reach as it came down, or risen as the slipstream eased), and S becomes them;
  - coming down from H to h, the S stubs are sliced by H − h;
  - fresh stems are due until the blades sweep them, a share B·ω·Δt/2π of the disc each step.
- **Every disc's trail over the field** (an entry each 1 cm of hub travel, the last 64 kept, numerical: 0.64 m, twice the 0.32 m a rear disc runs behind a front one) holds where its stems were cut and to what plane. The ground at a disc's leading edge was cut before if it lay strictly inside an earlier disc of any rotor. Then only what stands above that plane is met: a rear disc in the front one's track, a disc back over its own. Its H and S mix towards that ground's in the share of the disc it sweeps.
  - The design's first version gave the field a shared 5 cm grid. A grid's cell straddling the rim cannot say whether the ground at the rim was inside the disc, so a creep's own last steps gated its leading edge half the time. The trail's test is exact.
- **The elements** keep their cut plane in a 5 cm grid (numerical; 4,096 slots, open addressing, the home slot overwritten when full): an element's top is its bent top or its cell's plane, whichever is lower.

**7. Breaks.** Each cut's force e_s·A_w/d along the blade's motion goes through `Breaks` at its radius, as a twig's does. No 1.5 mm culm comes near: 5–8 N against hundreds.

### The numbers

| Number | Value | Label |
|---|---|---|
| Slipstream pressure on the stems | q = T/A, 222–236 Pa in the hover | derived (momentum theory) |
| Stem drag coefficient C_d | 1.15 (1.0–1.3) | published [16], [17] |
| The load's share as the stem leans | cos²φ | assumed (reconfiguration [18]) |
| The base spring | 4EI/L | derived |
| Press and rise times of the lean | 0.04 s, 0.1 s | derived, assumed |
| Critical speed law | 2.11 §2.2 | derived |
| Prop edge factor k_edge | 3 (2–4) | assumed [11], [13], [14] |
| The lean's slowing of the cut | ω·r·cos φ | derived (fits [11]'s 65° → 80 m/s) |
| Swept band h_f | c·sin θ + t·cos θ: 2.2 mm at the tip, 5.0 mm at 0.72 R | derived (drone.json's blade, carried) |
| Tops above h, within a patch | even up to its tallest | assumed |
| The discs' trails over the field | an entry each 1 cm, the last 64 a rotor | numerical |
| The elements' cut grid | 5 cm, 4,096 slots | numerical |
| Element cache | refreshed after 0.1 m | numerical |
| A stem episode's end | 0.1 s without a whole stem cut | numerical |

**The new cover rows** (`surfaces.json`, 2.11 §4; every value as 2.11 labels it). Each new row holds the `stem` row's fields plus `diameter_m`, the field having no drawn diameter. The row's modulus is one value (the field draws nothing); the wet set is the dry one's, as for the `stem` row (live tissue).

| Row | d (mm) | wall / d | ρ (kg/m³) | E (GPa) | e_s (kJ/m²) |
|---|---|---|---|---|---|
| `grass_blade` | 1.5, published [1] | 0.125, derived [1] | 1000, assumed | 1.0, assumed | 10, assumed |
| `dry_culm` | 1.5, assumed | 0.125, assumed | 500, assumed | 2.0, assumed | 15, assumed |
| `burnt_culm` | 1.5, assumed | 0.0625, assumed | 300, assumed | 2.0, assumed | 7.5, assumed |

- `straw`'s cut energy moves from 15 kJ/m² (assumed) to **22 kJ/m²** (published: Tavakoli et al. 2008's 21.85–25.74 for wheat straw, audit §5.1). Nothing read it before; its modulus, wall and density stay (the fiber reads them).
- The `stem` row (S04's weed stalks) is unchanged.
- **The log (as built): no format change.** The header's cover rows keep their layout. The new rows only add to their count, a byte already written. `Diameter`, `ContactSpec.Stems` and `AirframeModesSpec.StrikeLoad` go in a header entry of their own (tag 27, `TagStems`) after every other, and a reader skips an entry it does not know. So `LogFormat.Version` stays 72 and `LogFormat.cs` is untouched. A log without the entry (format 72 or 69, every earlier build's) reads as flown without stems and strike load.

**Per kind, at the hover's q** (derived from the rows; T 11.24 / 11.98 N):

| Kind | A_w (mm²) | EI (N·m²) | μ (g/m) | e_s·A_w (mJ) | v_c (m/s) | Tallest standing top (m) |
|---|---|---|---|---|---|---|
| green blade | 0.773 | 1.70e-4 | 0.773 | 7.73 | 53.5 | 0.126 / 0.123 |
| dry culm | 0.773 | 3.40e-4 | 0.387 | 11.6 | 88.3 | 0.159 / 0.155 |
| burnt culm | 0.414 | 2.06e-4 | 0.124 | 3.11 | 70.3 | 0.134 / 0.131 |
| straw 3.5 mm (row's 4.8–6.6 GPa) | 4.06 | 2.8e-2 | 2.03 | 89.4 | 54.3 | 0.44 |
| weed stalk 8 mm (row's 1–4 GPa) | 32.2 | 0.44 | 25.7 | 965 | 35.8 | 0.83 |

The drone stands with its discs 0.288 m up, so no Village culm under a hovering disc reaches it; S04's straw and stalks do.

### Hand calculation A: the spec's straw landing (0.5 m/s down into (768.07, 917.48))

**The cell** (measured, `StemsNear`): one field cell (86802) under all four hubs, its 11 layers. Stems in a disc reaching h: 17.5 at 0.6 m, 2.2 at 1.0 m, 69.0 at 0.288 m (14.4 green, 29.4 dry, 25.2 burnt).

**Coming down at hover thrust:** at every height from 1.77 m down to the standing 0.288 m, each kind's bent stems stand under 0.16 m. **No fresh cut and no slice under any disc.**

**At the touch:** the scripted landing cuts the throttle. The stems stand to the discs' 0.288 m again only once q has fallen to 18.5 (green), 22.4 (burnt) or 37.0 Pa (dry), 8–16 % of the hover's (derived). Even if the stems stood again at once, that needs ω ≤ 0.28–0.40 of the hover's, about 260–370 rad/s (q ∝ T ∝ ω²). The blade's tip then moves at 33–47 m/s, under the slowest kind's 53.5 m/s, and the stems rise only over 0.1 s after that. **None is cut.**

**Moving:** the only cuts left are stems the discs run into as the pilot's hold drifts, 2·R·s·D(h) for a horizontal path s inside the grass at height h. They are counted from the run's own path.

**So the prediction:**
- **Cuts:** no in-disc cut, no slice; a stem episode only if the drift runs a disc into stems.
- **The spectrum:** no line at the blade pass from the stems. The run with the stems and the run without them differ by the drift's cuts alone.
- **The 10 % check:** the run's stem work is at most the drift's cuts at their energies, within 10 %.

**The known-wrong** (the audit's law: no lean, no critical speed, every stem reaching the plane cut). At the plane's 1.0 m, coming down at 0.5 m/s, at the front rotors' ω 925.26 rad/s (the rear's 896.78):
- Every stem reaching the plane is in the cut, 0.99 of the disc outside the root.
- Fresh tops come at 0.5 × (2.745 + 8.915 + 4.59) stems/m × 0.99 = **8.04 a second**, at 7.5–35 mJ each.
- The stubs standing at the plane (0.41 + 1.13 + 0.67 = 2.20 in the disc) are sliced at min(0.2945 passes, 0.33 of a diameter) a step, flinging 0.5 mm of stem each, at u_rms = ω·√((R² + r_root²)/2) = 83.5 m/s.
- So **8.40 W** a front rotor, **8.05 W** a rear one (derived; 2.0 / 5.3 / 0.9 W of it the green, dry and burnt stubs' slicing).
- That grows as 1/(the stems' height)² down the cell. Every stem met would be cut: 69 a disc by the touch, and its stubs would take 260 W a rotor at 0.3 m. The run must give these within 10 %, and the law's run must not.

### Hand calculation C: the pilot's landing approach, a creep through the cell

The front discs creep forward at 0.3 m/s through the cell with the hubs 0.6 m up (the scripted creep: its height held, its heading +z).
- **The stems they run into** stand upright, 17.53 in a disc's area reaching 0.6 m (3.90 green, 8.86 dry, 4.77 burnt).
- **The rate:** n = 2·R·v·D(0.6) = **26.36 cuts a second** a front disc (5.86 / 13.33 / 7.18). Each is cut at the rim (u = ω·R = 117.5 m/s at ω 925.26, above every kind's v_c).
- **The work:** each costs e_s·A_w + μ·h_f·u² with the tip's h_f = 2.18 mm, 31.0 / 23.2 / 6.8 mJ. So **0.540 W** a front disc and a mean 20.5 mJ a cut (derived).
- **The impulses:** J_c = 20.5 mJ / 117.5 m/s = 1.744e-4 N·s a cut. The rotating force at the blade pass is π·n·J_c/4 = **3.61e-3 N** at 294.5 Hz, and a steady sideways one of the same size. The axial push is 7.1e-3 N, steady.
- **The rear discs** run in the front ones' cut track at the same height: **no cut** (the grid).

Pressing one stem every 38 ms through the front rotors is a light load. Village's 1.5 mm culms cost the props almost nothing: the shake the pilot feels comes from what stands stiff (straw, stalks), from the legs in the stems (2.10), and from flying into grass at speed (n grows with v).

### Hand calculation D: the range golden's straw landing (S04, 0.5 m/s down, `GoldenFlights.FlyRange`)

The S04 stalks based in the four discs at the landing (measured, `MicroDetailNear`; the moduli by their Ids' hash):
- rotor 0 has one stalk, 4.99 mm, 1.13 m long;
- rotor 1 has five, 2.15–4.21 mm, 0.57–1.03 m;
- rotor 3 has one, 2.17 mm, 0.67 m;
- rotor 2 has none.

The straws under the discs lie in the mat, their tops at 0.17 m at most.

At the hover's q:

| Rotor | d (mm) | L (m) | Lean | Bent top (m) | ω·r·cos φ (m/s) | v_c (m/s) | |
|---|---|---|---|---|---|---|---|
| 0 | 4.99 | 1.126 | 56.1° | 0.604 | 39.4 | 48.3 | escapes |
| 1 | 4.21 | 1.030 | 68.5° | 0.366 | 36.4 | 61.2 | escapes |
| 1 | 2.15 | 0.812 | 75.9° | 0.176 | 24.8 | 89.7 | escapes |
| 1 | 2.73 | 0.990 | 70.9° | 0.306 | 20.0 | 70.0 | escapes |
| 1 | 2.31 | 0.566 | 56.2° | 0.292 | 59.8 | 76.7 | escapes |
| 1 | 2.15 | 0.811 | 74.8° | 0.196 | 28.3 | 87.5 | escapes |
| 3 | 2.17 | 0.672 | 69.4° | 0.195 | 35.7 | 85.8 | escapes |

So the landing itself cuts nothing. What can be cut is at the rotors' spin-up on the straw, where the stalks stand upright under discs speeding up: they are cut wherever ω·r reaches v_c before the slipstream has laid them over. The run decides how many. **The range golden moves only by that and by G8.**

### What the runs must show (the spec's checks)

1. **The straw landing** (A): no stem cut while coming down or at the touch, beyond the drift's, within 10 %; the known-wrong's 8.40 / 8.05 W at 1.0 m within 10 %.
2. **The creep** (C): a front rotor's cut rate, power and blade-pass line within 10 % of 26.36 /s, 0.540 W and 3.61e-3 N; the rear rotors' cuts near none.
3. **A hover in grass** cuts itself clear and its episode ends.
4. **The range golden:** the move audited case by case; its log replays bit for bit.
5. **0 B** in steady state, and the step's cost (indicative here).

### The log, the feed, the switches

- **`BladeStrikes`:** per blade per step, `StrikeKind.Stem` (4, new; `Material` its cover kind + 1), the energy, the impulse's size, the kind's diameter, the edges' work and the radius. `Count` holds the whole stems the blade's running count has passed in the step: the field's fractional mean counts are counted, never drawn.
- **Event 39, `StemEpisode`:** per rotor, at its first whole stem cut (`Index` its corner) and 0.1 s after its last (`Index` | 0x80). A is the energy so far, J, B the stems, C their mean diameter, m, and Point the hub. Two events an episode, against LOG-01's budget, never one a cut.
- **`FeedSignals`:** per rotor over the field window, the stems cut, their pieces' mean length (m) and radius (m), and the rotor's angle at the last cut. For 2.5. No `Live` bit: the feed's enum is the tech-artist's.
- **The switches** (in the header's `TagStems` entry):
  - `ContactSpec.Stems` (the stems on the props) and `AirframeModesSpec.StrikeLoad` (G8) are on for every flight.
  - Off only for the kept audits of earlier models, flown as before, and for the known-wrong's comparisons.

**Model:** the next free number at the merge (the orchestrator's). The note's line: *the props meet the stems: the slipstream lays them over, a leaning or slow blade pushes them aside, the rest are cut and paid for; a strike loads its arm (2.2).*

### As built: the runs against the hand calculations

**Code.**
- `Runtime/Contact/PropStems.cs` holds the law, a part of `PropStrikes`, called for each turning disc before its pairs.
- Small edits:
  - `PropStrikes.cs`: the call, `TipImpulse`, `partial`;
  - `ContactSpec.Stems`, one field;
  - `FlightWorld.StemsNear`;
  - `BladeStrikes`: `StrikeKind.Stem`, `AddStems`;
  - `EventKind.StemEpisode` (39);
  - `FeedSignals.StemCuts…`;
  - `AirframeModesSpec.StrikeLoad` and `AirframeModes.Strike` (G8);
  - `FlightCore`: the G8 call after the strikes, the feed;
  - `LogHeader`: `TagStems`; `FlightLogReader`: event 39's text.
- `ContactModel.cs` and `LogFormat.cs` are untouched.
- `StemsNear` was used as it is: `QueryVersion` stays 9.

**Tests** (`StemTests`, the flights in `StemScenarios`), measured:

| Check | Run | Hand | |
|---|---|---|---|
| A: the straw landing, fresh and sliced under the discs before the touch | 0 | 0 | exact |
| A: stems the hold's drift ran into before the touch | 0.002 (0.04 mJ) | the drift's | |
| A: after the touch (the body settling as the props spin down) | 8.1 entries | | |
| A, the known-wrong: a front rotor's stem power at the plane's 1.0 m | 8.168 W | 8.40 W | −2.8 % |
| A, the known-wrong: a rear rotor's | 7.813 W | 8.05 W | −2.9 % |
| A, the known-wrong: fresh stems a rotor before the touch | 65.5 | 69 by the standing plane | fails the law's 0, as it must |
| C: the creep, fr in the cell (2.23 s at h 0.567 m, v 0.324 m/s, ω 929.86) | 33.35 /s, 0.6837 W, line 4.540 / 4.546 mN | 32.66 /s, 0.6703 W, 4.458 mN | +2.1 %, +2.0 %, +1.8 / +2.0 % |
| C: fl (h 0.544 m) | 36.83 /s, 0.7510 W, 4.990 / 4.993 mN | 36.07 /s, 0.7374 W, 4.906 mN | +2.1 %, +1.8 %, +1.7 / +1.8 % |
| C: the rear discs' entries against the front's | 7.2 against 203 | none | the front's track |
| A hover 0.5 m up in the cell, 10 s | 5.0–5.7 stems a rotor from the drift, the last at 7.6 s; 19 episodes begun and ended | clear | |
| The range's stalks, a creep through the golden's spot 0.4 m up | fresh 1 and 4 (rr, fr), sliced 4.5 and 3.2 as the height bobbed, 5.47 and 3.92 J; up to 3 elements under a disc | cut by the same law | |
| G8: a 3 mN·s strike at fr's tip | its hub 17.8 mm/s, rl's 2.4 mm/s, the camera 1.67 m/s²; equal to `Advance`'s thrust impulse less one step's damping, every mode to 1e-9 % | | |
| The landing's log | replays bit for bit (10,750 steps, 38 events); 0 B after the first second; no event refused | | |

The creep's hand calculation is C's formula at the run's own height, speed and ω (the pilot holds 0.54–0.57 m and 0.324 m/s, not the nominal 0.6 and 0.3). At the nominal it gives design C's 26.36 /s and 0.540 W.

**What the pilot would feel (measured, the body rate's RMS):**
- The creep, its front discs in the cell, 2 s: 4–15 Hz roll 0.0001 against 0.0000 °/s without the stems, pitch 0.0060 against 0.0065; 250–350 Hz pitch 0.0019 against 0.0006 °/s.
- The vertical landing, the last 3 s before the touch: the same bands with and without the stems, 0.00054 °/s of pitch.
- So **Village's 1.5 mm culms do not shake the drone.** Cutting one costs 7–35 mJ against the rotor's 25 J, and the slipstream lays the rest over. The stems that load the props are stiff ones: S04's stalks took 4–5 J a rotor in a 4 s creep. The pilot's shake on landing in grass with straw must come from straw-like stems, from the legs in the stems (2.10), and from flying in at speed.

**The goldens:**
- `flightbench --golden`: 61 PASS, none moved.
  - The main goldens' flights never cut a stem. The range golden's stalks lean and escape (hand calculation D), its straws lie under the discs, and its spin-up stands on the mat with the hubs above every stalk the law lets stand.
  - No golden carries an axial strike, so G8 moves none.
- Every kept audit flies `GoldenFlights.WithoutTheStems` (in `Edgewise`: every golden without the edgewise thrust is an audit of an older model), so each stays bit for bit even where a future golden would cut.

**Cost** (indicative: this laptop on AC, the CPU 68 % loaded by other agents; the two cores stepped in turn in one process so the load falls on both alike):
- The creep's `core.Advance`: median 33.4 µs with the stems, 27.7 without; p99 78.4 against 61.6 µs.
- The world's part: `SampleGround` × 4 1.0 µs and `StemsNear` × 4 3.7 µs (medians).
- PLT-04's quiet run is QA's.

### The checks, as run (on 9f1dcbe6 and 083e0b87)

```
dotnet test tools/simtests -c Release --filter "FullyQualifiedName~StemTests"
  Passed: 5 (the numbers in the table above, printed by each test)
dotnet test tools/simtests -c Release
  Passed!  - Failed: 0, Passed: 842, Skipped: 0, Total: 842, Duration: 14 m 16 s
dotnet run -c Release --project tools/flightbench -- --golden
  flightbench: 6 golden cases registered … 61 PASS, 0 FAIL (range-flight 0xAF745CC52447E017 matches, as before 2.2)
python tools/hygiene/precommit.py --tree HEAD
  3570 files scanned, 0 findings
```

### For the next tasks

- **2.3 (leaves in crowns):**
  - `PropStems.Part` is the cut's law: the edge's e_s·A, the swept band's fling, the pitched face's axial share, `Breaks`, the rotor's books, the log's `BladeStrikes` and the episode. A leaf can go through it as a stem whose wall is the lamina (its work to shear per length × the cut's length in place of e_s·A).
  - A crown's twigs still go through `Twigs`. Their shells still hold a resting blade (2.7's note).
  - `TipImpulse` already carries the twigs' axial share to the arm (G8).
- **2.5 (cut bits on camera):**
  - `FeedSignals.StemCuts`, `StemPieceLength`, `StemCutRadius` and `StemBladeAngle`, per rotor over the latest whole field, the same in a replay.
  - The pieces leave the front discs mostly at the leading edge, in the direction of flight (the entries), and below the discs (the pitched face pushes them down: the axial share is 2–4 times the in-plane).
  - The feed's `FeedSignal` enum has no index for them: they carry no `Live` bit, as `Sticks` doesn't.
  - A vertical landing in Village's grass gives the feed nothing to draw. A forward approach does: about 20 stems a front rotor in the forward landing, measured.
- **2.10 (stems on the legs and the fiber):**
  - The stem field per kind with diameters is in `surfaces.json` (`grass_blade`, `dry_culm`, `burnt_culm`, each with `diameter_m`); `FlightWorld.StemsNear` is the door.
  - The slipstream's lean (`PropStems`: q = T/A, φ/cos²φ = κL³) lays the stems under a hovering drone over. The legs at touchdown meet stems the props' slipstream has pressed, not upright ones, unless the drone came in moving.
  - The props' trails record where stems were cut and to what plane: a leg landing in the props' cut track meets stubs.

### For `unity/Assets/_Project` (not changed here)

- **Event 39 in the post-flight summary.** `Scripts/App/Session/PostFlight.cs` picks its events by kind (crashes, the line, failsafes, blade damage, legs). A stem episode is not shown until it is given a line, for example "STEMS: rotor fr cut 12 dry culms, 0.4 J". The sim's own reader already prints one (`FlightLogReader`).
- **`FlightFeedSignals` and `IFeedSignals`:** the four new `Corners` fields, for 2.5. The feed's `FeedSignal` enum needs a new index (17 is the next one the physics does not use) only if the feed wants a `Live` bit for them.
- **`AppData`** carries `surfaces.json` into the player as a file, so its new rows travel with it. Nothing there counts the cover rows.

### Open questions for the pilot (qualitative)

1. When you land in tall grass, does the grass under the quad lie flat in the prop wash before the props reach it, or does it stand into the props?
2. When straws are cut in front of your camera on landing, are you still moving forward as the props reach the grass, or coming straight down?
3. Is the "straw" in that grass stiff, cereal-like stalks, or the grass's own dry stems?

The physics computes the rest. The answers decide whether Village's tall dry layers are straw-like stalks (a world-data question for the kinds of 2.1 and the diameters of 2.11) and which approach the spec's landing scenario should fly.

(Superseded by 2.12: these are physics, and physics is never asked of the pilot, D-104. Their answers of 2026-11-13 are in `docs/pilot-questions.md`.)

## 2.12 The landing in high grass with straw

**The pilot's words outrank the law** (D-104). 2026-11-12: "in real life if i land on a high grass with straw - straws are noticeable cut in front of my quads camera and my quad is shaking". 2026-11-13 (`docs/pilot-questions.md`, on `unity/main`): "You shake … If I can see the drone is shaking too much (which means there's A LOT of straws and the straws are thicker than I thought - Better throttle up and find a bette replace to land. If shake is agreeable - I just keep landing)"; tall grass in the wash "Usually it lies flat because of prop wash power, but not always"; "Straws are quite stiff, but not extremely".

So the targets: straw is cut in front of the camera (the front discs) on a landing; the quad shakes, by an amount that grows with the straws' number and thickness, from a shake a pilot lands through to one that sends them away; grass usually, not always, lies flat in the wash; the straw is cereal-like, between a thin culm and a woody stem.

Labels as in 2.2: **measured**, **published**, **carried**, **derived**, **assumed**, **numerical**.

### What was wrong: both the inputs and the law

**1. The inputs: a 1.5 mm culm can't stand 1.2–1.8 m.** Its self-buckling height (Greenhill, a column under its own weight: L_c = 1.986·(EI/μg)^(1/3), derived) is 0.89 m dry and 0.56 m green with 2.2's rows. Village's tall layers draw stems to 1.22–1.77 m. A stem drawn standing must be able to stand.

**2. The law over-presses, and in the wrong way.**
- *A vertical jet loads an upright stem along its axis, not across it.* 2.2 took q·C_d·d across the whole stem, a horizontal wind's load. By the cross-flow principle (the normal velocity component carries the normal force) a stem leaning φ in a vertical jet takes q·C_d·d·sin²φ (derived): nothing upright, the full load lying over. So an upright stem stands until a lean or its own weight starts it, then folds through a fold point. That is the pilot's "usually it lies flat, but not always".
- *A folded stalk does not lie flat.* 2.2's rigid link puts a leaning stem's top at L·cos φ, down to the ground. A long stalk folded by the jet is an arch: its base stays upright where the jet has little grip, the outer part lies over. The arch's crest stands at 0.43–0.61 of the bending length l_b = (EI/(q·C_d·d))^(1/3), whatever the stalk's length (derived: the elastica of a stalk clamped upright, loaded w·sin²θ normal to it, its end resting on the ground, solved by shooting for L/l_b = 3–6, `tools/flightbench/handcalc/stem_elastica.py`).
- *The stems don't lie down in 40 ms together.* Each stalk follows its own first bending mode, τ = L²/(1.875²·√(EI/μ)) (derived): 0.02 s for a 0.3 m dry stem, 0.2 s for a 1.77 m one, 0.45 s for a tall green culm. A disc moving onto stems, or coming down faster than they fold, meets them standing.
- *The rotor-downwash-on-canopy check.* The published measurements I could open: rotor outwash in ground effect peaks at r/R 1.7–1.8 whatever the rotor's height (Tanner et al. 2015, NASA/AHS, abstract read); under the disc the jet still points down and only turns radial at the ground (the wall jet, George et al.'s model restated there). So under the disc a stem meets a vertical jet, and the wall jet's horizontal flow grips only its base, outside the disc. The agricultural studies of multirotor downwash in rice and wheat canopies (Li et al. 2019; Shi et al. 2021, *Biosystems Eng.* 203: 60–69) could not be opened (the reader refused them); their abstracts, as the search gives them, say the flow attenuates most at the canopy's bottom and stronger downwash bends the plants more. Neither gives a number used here.
- *A mean count can't shake.* 2.2 spread a cell's stems evenly over the disc and cut fractional means: the two blades' in-plane pushes cancel and nothing fluctuates. A few thick stalks a turn are discrete hits, each at its own radius and azimuth, and their irregular arrival is the shake. Without it the sim can give a lurch and a 290 Hz line, never a shake.

### The stalks, per stem from its own drawn length

- **The allometry (published):** over 76 herbaceous species, height grows as D^1.53 (Niklas 1995, *Ann. Bot.* 75: 133–142, abstract read via OpenAlex; its critical buckling height as D^1.19, safety factors falling as D^−0.34). So a stem's diameter is d = d₁·L^(1/1.53), L its drawn length in metres, d₁ the kind's diameter at 1 m (the cover row's `diameter_m`, its meaning changed: the diameter of a 1 m stem).
- **The anchors:**
  - *Dry and burnt (straw):* wheat's second basal internode, 3.87–4.54 mm across with a 0.475–0.586 mm wall and 17.8–25.5 mg dry a cm, over seven cultivars (Yao et al. 2012, *Aust. J. Crop Sci.* 6(5): 899–903, Table 1, full text read). At a mean 4.2 mm for a 0.9 m plant (assumed: the semi-dwarf wheats' 0.8–1.0 m; the paper gives no height), d₁ = **4.49 mm** (derived). Its wall share 0.52 / 4.2 = **0.124** (derived) confirms 0.125; its mass a cm over the wall's area gives a dry wall of **320–450 kg/m³** (derived), so the dry row's density moves from 500 (assumed) to **400** (derived, a standing dead stem's 10–15 % moisture on that).
  - *Green:* InfoFlora's 1–2 mm culms on 30–120 cm grasses (stem-properties.md [1], published), 1.5 mm at 0.75 m taken as their middle (assumed), d₁ = **1.81 mm** (derived).
- **Self-buckling, the check (derived):**
  - Dry, at the row's 2 GPa: SF = L_c/L 3.9 at 0.3 m, 2.0 at 1 m, 1.44 at the tallest drawn 1.77 m. Burnt 4.5–1.65.
  - Green at 2.11's assumed 1 GPa: SF 0.54 at the tallest drawn 1.34 m. It can't stand. So the green row's modulus becomes the least that lets the drawn culm stand, **6.4 GPa** (derived; inside wheat straw's published 4.8–6.6 GPa tensile modulus, stem-properties.md [6]). Its safety factor is then 1.0 at 1.34 m and 2.3 at 0.3 m.
  - "Quite stiff, but not extremely" (the pilot): a 1.5 m dry stalk is 5.9 mm across with EI 0.079 N·m² and stands with a margin of 1.6, between a 1.5 mm culm (EI 3.4e-4, lies down under its own weight) and an 8 mm woody stalk (EI 0.44).
- **Per stem, not per layer,** because a drawn tuft's stems span their layer's lengths: a tall dry layer's 0.3 m blades are 2 mm leaves and short stems, its 1.7 m culms 6.5 mm stalks. The field already gives each layer's length distribution (below), so the law needs no new query.

| Kind, L | d (mm) | EI (N·m²) | μ (g/m) | SF | v_c (m/s) | fold A at 230 Pa | l_b (m) | folded crest (m) | τ (s) | a cut at the tip (mJ) |
|---|---|---|---|---|---|---|---|---|---|---|
| dry 0.5 m | 2.85 | 4.5e-3 | 1.12 | 2.9 | 64 | 2.6 | 0.18 | 0.09 | 0.04 | 76 |
| dry 1.0 m | 4.49 | 2.7e-2 | 2.77 | 2.0 | 48 | 5.4 | 0.28 | 0.14 | 0.09 | 187 |
| dry 1.5 m | 5.85 | 7.9e-2 | 4.71 | 1.6 | 40 | 8.3 | 0.37 | 0.19 | 0.16 | 318 |
| dry 1.77 m | 6.52 | 0.12 | 5.84 | 1.4 | 37 | 9.9 | 0.41 | 0.21 | 0.20 | 395 |
| green 1.0 m (6.4 GPa) | 1.81 | 2.3e-3 | 1.13 | 1.3 | 34 | 26 | 0.17 | 0.08 | 0.20 | 45 |

(derived, `tools/flightbench/handcalc/stem_stalks.py`; dry at 400 kg/m³ and 2 GPa, e_s 15 kJ/m²; a cut at the tip is e_s·A_w + μ·h_f·u² at u = 117.5 m/s, h_f 2.18 mm, as 2.2.)

### The law (replacing 2.2's steps 1, 2 and 6 for the field; the cut's work, impulses, log and feed are 2.2's)

**1. The field's stems, one by one, from the world.** The field gives per (cell, layer) the stems per m² reaching a height. The law places each stem:
- The ground is cut into **0.1 m squares** (numerical). A square's stems of layer l are those of the cell under its centre (one `StemsNear` at the centre, radius 1 mm, height h_ref): K = ⌊D_l(h_ref)·0.01 m² + u⌋.
- **h_ref = 0.2 m** (numerical): the lowest a disc's plane comes over the ground while the drone stands (0.288 m, less 0.06 m for a rim tilted 30°, less a margin). Shorter stems never reach a disc.
- Stem k of K stands at the square's (u₁, u₂), leans naturally φ₀ = u₃·15° (assumed, 5–25°: the drawn blades' spread and the detail's half tilt to the ground; it sets how readily a stem starts to fold) towards the azimuth 2π·u₄, and its drawn length is T with ρ_l(T) = (k + u₅)/K, ρ_l the layer's share of stems at h_ref still reaching T, read from the field (`StemsNear` at one point at heights 5 cm apart, once per layer and flight; the share is the same in every cell, as the field is count × the layer's reach).
- Every u is the stem's own hash of (square, layer, k, which) (`FlightRandom.Mix64`): world-derived, the same in every flight, never a draw, as an element's modulus is drawn from its Id (2.2). A cell's edge is moved to its squares' centres: up to 5 cm, numerical.
- S04's elements (the range) go through the same steps 2–4 with their drawn shape.

**2. The slipstream folds them, each at its own pace.** A stem under a disc (its base within R of the hub's axis, assumed: the wake's column, its contraction and skew left out) takes q = T/A of that rotor (the most of the discs over it). As a rigid link on its base spring 4EI/L (2.2), its moment balance is
- m(φ) = (φ − φ₀) − A·sin²φ − G·sin φ, with A = q·C_d·d·L³/(8EI) the jet's lean and G = μ·g·L³/(8EI) its own weight's (derived; G alone gives the self-buckling height within 1 %);
- the lean follows **φ' = −m(φ)/τ**, τ the stem's first-mode time above (assumed: a stem in a stand is overdamped by its neighbours; the mode's time sets the pace, range ×0.5–2). There is no lag on q.
- Under the fold load the lean runs away to lying over (φ → 90°); with less load it stands again on its own. Stiff short stems stand, long or thin ones fold, and a stem the disc moves onto, or comes down on faster than τ, still stands when the blade arrives.

**3. Its top.** Standing, L·cos φ over its base. Folded, the arch's crest: max(L·cos φ, min(L·cos φ₀, c_crest·l_b)), l_b = (EI/(q·C_d·d))^(1/3), **c_crest 0.5** (derived: 0.43–0.61 over L/l_b 3–6). With no jet a folded stem rises again.

**4. The cut.** The blade at radius r meets the stem when its top is over the disc's plane there, at the blade's speed across the stem's axis u⊥ = ω·r·√(1 − (t̂·â)²), t̂ the blade's motion and â the stem's axis (derived; 2.2's ω·r·cos φ is the case of a stem leaning along the blade's motion, still Tuck et al.'s "over 80 m/s at 65°"). A folded stem meets the blade as its arch: across where it lies radially, sliding where it lies along the blade's path. Cut where u⊥ ≥ v_c (2.2's law, per stem at its own d), pushed aside below.
- Each stem keeps its own state (shared by every disc, keyed by its hash): its lean, and its length, the cut shortening it to where it crossed the plane. A cut stem is a shorter, stiffer stub: it stands, and a disc coming down slices it (2.2's slice, min(1, Δ/d) of a cut).
- Each cut is a discrete hit at its own radius and azimuth, by the blade crossing it in the step (2.2's elements' rule), with 2.2's work and impulses (`Part`).

**5. Removed from 2.2:** the field's mean counts, the per-rotor q lag (0.04 / 0.1 s), the discs' trails and the elements' 5 cm cut grid. The per-stem state replaces them.

### The numbers

| Number | Value | Label |
|---|---|---|
| Height–diameter exponent | d ∝ L^(1/1.53) | published (Niklas 1995) |
| Dry and burnt d at 1 m | 4.49 mm | derived from Yao et al. 2012 (published) and a 0.9 m wheat (assumed) |
| Green d at 1 m | 1.81 mm | derived from InfoFlora (published), 0.75 m middle (assumed) |
| Dry density | 400 kg/m³ (320–450 dry) | derived from Yao et al. 2012 |
| Dry wall share | 0.125 (0.124) | derived from Yao et al. 2012, carried |
| Green modulus | 6.4 GPa | derived: the least that lets the 1.34 m culm stand |
| Dry and burnt modulus | 2 GPa (0.47–6.6) | assumed, carried |
| Natural lean φ₀ | 0–15° by the stem's hash | assumed (5–25°) |
| Lean pace τ | the first mode's L²/(1.875²·√(EI/μ)) | derived; overdamping assumed |
| Folded crest | 0.5 l_b | derived (elastica, 0.43–0.61) |
| Squares, h_ref, ρ_l's heights | 0.1 m, 0.2 m, 5 cm | numerical |
| The stems' states | 16,384 slots, the oldest overwritten | numerical |

### Which way the pieces push, by hand (before the runs)

- **A vertical landing after a hover over the spot** cuts little: the stems under the discs folded in the hover (A 2.6–9.9 for dry 0.5–1.77 m against a fold near 1), their crests stand at 0.09–0.21 m, under the standing discs' 0.288 m. The cut comes from stems the disc drifts onto and from stems rising as the thrust eases at the touch, when the blades slow.
- **An approach** cuts what the front discs' leading edges meet upright: n = 2·R·v·D(h) stems a second a front disc (2.2's C), each a hit of 40–400 mJ (dry, 0.5–1.77 m) against 2.2's 7–35 mJ.
- **Thicker straw** raises the crest with d (l_b ∝ d): at 1.5× the diameters the dry crests reach 0.28–0.31 m, and a vertical landing cuts.
- The runs and their hand calculations follow in "As built".

## 2.3 Leaves in crowns

To be written by task 2.3.

## 2.9 Village's ground rows and the liftoff harness

To be written by task 2.9.

## 2.10 Stems on the legs and the fiber

To be written by task 2.10.

## 2.5 Cut bits on camera

To be written by task 2.5.
