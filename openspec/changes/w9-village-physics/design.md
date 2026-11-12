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

To be written by task 2.2.

## 2.3 Leaves in crowns

To be written by task 2.3.

## 2.9 Village's ground rows and the liftoff harness

To be written by task 2.9.

## 2.10 Stems on the legs and the fiber

To be written by task 2.10.

## 2.5 Cut bits on camera

To be written by task 2.5.
