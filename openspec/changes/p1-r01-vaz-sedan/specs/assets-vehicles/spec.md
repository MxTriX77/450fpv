# Spec Delta

## Purpose

The assets of the vehicles object sheet (cars R01–R07, trucks R08–R09, the car-debris kit K07): what each built vehicle must show and carry in its states, judged against its reference letter and its object sheet. One requirement per asset id.

## ADDED Requirements

### Requirement: R01 classic sedan in the A and F states
The pipeline SHALL build asset R01 (`vehicle` generator, preset R01; variants `R01_A`, `R01_F`) from its params files into Unity prefabs that copy the M car: a first-generation classic sedan of 4.07 × 1.61 × 1.44 m with twin round headlamps, a mesh grille, chrome bumpers with overriders, framed doors with vent panes, steel wheels with domed hubcaps and a tube roof rack, in M's dark green. `R01_A` SHALL show M's abandoned state: about 6 large and 10 small fragment holes on the burst side with inward petals and rust halos, the bonnet's rear corner folded up against a crazed and holed windscreen, side glass gone with crumbs, one flat front tyre dropping that corner 0.04–0.08 m with a 1–2° lean, and the car's own small parts scattered within 1–4 m. `R01_F` SHALL show the burnt state: on its rims 0.15–0.20 m lower with bead-wire rings, glass gone, chrome dulled and soot-black, the interior burnt to seat frames and springs, the roof sagging, paint soot-black and burnt to bare steel where hottest, on a feathered soot patch 2–6 m across. Every torn edge, hole of 30 mm or more, fold and the crazed pane SHALL be geometry; plates SHALL be blank or absent.

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 R01` runs on a clean checkout
- **THEN** it exits 0 for both states, writes their FBX, metadata, masks, prefabs and review sheets, and a second run leaves `git status` clean

#### Scenario: Published dimensions
- **WHEN** `R01_A`'s metadata is read
- **THEN** the body's bounds (without its scatter) are 4.073 × 1.611 m in plan within 2 %, the wheelbase 2.424 m and the tracks 1.349 / 1.305 m within 5 mm, the wheel cylinders 0.594 × 0.165 m, the roof at 1.40–1.44 m above the ground on the unflat corners, and the flat corner 0.04–0.08 m lower

#### Scenario: Matches M
- **WHEN** the review sheet's 1.5 m and 10 m tiles of `R01_A` and the turntable are compared side by side with letter M in the local comparison sheet
- **THEN** the hole pattern's side, sizes and halos, the bonnet flap against the windscreen, the crazing, the flat tyre's lean, the rack, the chrome and the paint's three readings (shade, lit, sky-lit roof) match by eye within the sheet's values

#### Scenario: Collision and landing surfaces
- **WHEN** the metadata of either state is audited
- **THEN** it holds the collision plan the 2 cm audit needs, its count per group pinned by the generator's test (84 primitives in the clean base), with every face the drone can touch within AST-05's tolerance of the visual (2 cm within 0.3 m of a landing face or an opening, 5 cm elsewhere), the roof, bonnet and boot lid boxes and the rack's side rails are landable and the flap is not, fragment holes of 0.25 m or less stay closed in collision with their petals as `edge` snags, the tyres name `rubber` and the seats `fabric` in A, and in F the body sits 0.15–0.20 m lower with the wheel cylinders at the rim radius

#### Scenario: Burnt look
- **WHEN** `R01_F`'s 1.5 m tile is read against the sheet's F description and letter O's burnt cab
- **THEN** the paint reads soot-black with bare grey-white steel over the engine bay and cabin roof, the seats are frames and springs, bead-wire rings lie at each wheel, and the soot patch under the car is feathered with no hard edge

#### Scenario: Budget
- **WHEN** the triangle counts per LOD are read
- **THEN** LOD0 is under 60 000 triangles, each further LOD at most half the previous with holes of 0.08 m or more kept to LOD1, the textures 2K or less, and the cull distance is 1 500 m (the pixel rule from the car's 1.44 m height gives 673 m, but its 4.07 m length still covers about 2.5 px at 1 500 m at 935 px/rad)

### Requirement: K07 first pieces
The pipeline SHALL build the K07 pieces R01 needs (`debris_car` generator; variants `K07_lamp_bowl`, `K07_bezel_ring`, `K07_trim_plate`, `K07_bead_wire`) with their own boxes, capsules or spheres, `steel` or `plastic`, and `edge` or `hook` snags (the lamp bowl's pressed rim an `edge`), composable into a vehicle's debris field; glass crumbs are a micro-mesh layer, not pieces.

#### Scenario: Pieces built and composed
- **WHEN** `pwsh tools/build_asset.ps1 K07` runs and then `R01` is built
- **THEN** the four pieces build with their shapes within 5 mm, `R01_A`'s scatter is made of the lamp bowl, the bezel ring and the trim plate within 1–4 m of the struck corner, densest within 2 m, and `R01_F` places a bead-wire ring at each wheel
