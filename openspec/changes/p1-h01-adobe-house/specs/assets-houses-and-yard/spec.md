# Spec Delta

## Purpose

The assets of the houses-and-yard object sheet (village houses H01–H06, outbuildings O01–O06, cellars C01–C02, yard items Y01–Y11): what each built asset must show and carry, judged against its reference letter and its object sheet. One requirement per asset id.

## ADDED Requirements

### Requirement: H01 adobe house in three states
The pipeline SHALL build asset H01 (`house` generator; variants `H01_I`, `H01_D`, `H01_X`) from its params files into Unity prefabs that copy the E house: a lime-whitewashed adobe house of 10 × 6 m with 2.4 m walls 0.45 m thick, a red-clay base band, a 38° roof hipped at one end and gabled at the other with every clay tile and batten a mesh, a brick chimney, a plank door of 0.8 × 1.9 m hung inward, 0.8 × 1.0 m windows, and an interior with its main beam, joists, ceiling boards and blue whitewash. Sizes SHALL be the exact AST-04 sizes where AST-04 gives them and inside `docs/object-sheets/houses-and-yard.md` §H01 elsewhere. The D state SHALL show the E damage (roof stripped 60–90 % with tiles clustered at the eaves and the lee end, 20–40 % of batten spans broken with splintered ends, 1–2 rafters off, the door leaf hanging inward, every window out with crumbled edges, a tile heap 0.2–0.4 m deep along the foot, the ceiling down over 30–60 % of the floor, one hanging cable); the X state SHALL show the roof gone with rafters lying across the walls, the gable and one long wall down to 0.5–1.5 m, the chimney a stump and rubble 1–2 m high inboard and 1 m outboard. Every damaged edge SHALL be geometry with collision, every opening's clear space a gap polygon, and all rubble coloured from the house's own materials.

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 H01` runs on a clean checkout
- **THEN** it exits 0 for the three states, writes their FBX, metadata, mask atlases, prefabs and review sheets, and a second run leaves `git status` clean

#### Scenario: Exact sizes
- **WHEN** `H01_I`'s metadata is read
- **THEN** the door's clear opening is 0.80 × 1.90 m, each window's 0.80 × 1.00 m with its sill 0.9 m up, the chimney's bricks are 250 × 120 × 65 mm, the walls are 2.40 m to the plate and 0.45 m thick, the ridge 5.0 m, the battens 0.33 m apart and the rafters 1.0 m apart, each within 5 mm

#### Scenario: Openings against the drone in the D state
- **WHEN** `H01_D`'s gaps are read
- **THEN** the door passes at any yaw (113 mm a side nose-first, 47 mm on the diagonal), a window with its frame gone is 0.80 × 1.00 m and with its frame 0.66 × 0.86 m (passable within ±18° yaw, 43 mm a side), every roof bay without battens is 0.9–1.1 m wide and passes, no bay with intact battens (0.33 m) is a gap, and every opening's polygon is its real clear space after jags, with `narrowest_m` agreeing with the audit within 5 mm

#### Scenario: The E facade
- **WHEN** the review sheet's 1.5 m and 10 m tiles of `H01_D` are compared side by side with letter E (frames 1–12 for the facade, 13–15 for the door, 16–18 for the interior) in the local comparison sheet
- **THEN** the exposed adobe, the render remnants, the whitewash loss around openings, the base band, the stripped roof with its tile clusters and broken battens, the hanging door and the blue interior match by eye, the facade shows no tiling, chequerboard or seam at 10 m, and the UVs run upright and continuous across every wall

#### Scenario: Rubble in the house's colours
- **WHEN** the tile heap and the X rubble are read in the 1.5 m tile
- **THEN** every piece is a tile shard, an adobe block, a render patch, a batten or a board with the sheet's albedo for its material, none near-white, the heap's depth and width sit in the sheet's range, and each piece carries its own collision box within 5 cm

#### Scenario: Budget
- **WHEN** the triangle counts per LOD are read
- **THEN** `H01_D`'s LOD0 is under 150 000 triangles, each further LOD at most half the previous with the openings kept at every LOD, the wall sets are 2K and every other texture 2K or less, and the cull distance follows the pixel rule from the ridge height

### Requirement: K02 tile shards and K03 timber pieces
The pipeline SHALL build assets K02 (`debris_tile` generator; variants `K02_tile_shard`, `K02_tile_whole`) and K03 (`debris_timber` generator; variants `K03_batten_broken`, `K03_board`, `K03_rafter_broken`) from their params files into Unity prefabs matching `docs/object-sheets/lines-roads-debris.md` §K02 and §K03: a seeded shard outline 0.05–0.35 m at 14 mm, a cambered whole tile of 0.40 × 0.22 m with a nib (ГОСТ 1808-71's strip tile, 333 × 200 mm covering), a 50 × 50 mm batten and a 100 × 150 mm rafter splintered at one or both ends, a 32 × 150 mm board; each with one box (two for the cambered tile), `masonry` or `timber`, `edge` snags on fracture rims and `hook` snags on splinters and nails, and the pieces SHALL be composable by the house generator as its rubble.

#### Scenario: Kits built and composed
- **WHEN** `pwsh tools/build_asset.ps1 K02` and `K03` run and then `H01` is built
- **THEN** the five pieces build with their boxes within 5 cm and their snags present, and `H01_D`'s tile heap and `H01_X`'s rubble are made of those same pieces placed by the debris spread rule (exp(−d / λ): tiles 1.0 m, timber 2 m)
