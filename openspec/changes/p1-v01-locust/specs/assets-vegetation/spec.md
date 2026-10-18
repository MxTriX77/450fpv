# Spec Delta

## ADDED Requirements

### Requirement: The H autumn belt state
V01 and V13 SHALL each have autumn variants built from the same generators with `leaf_cover` 0.2–0.5, the twig mesh kept dense, and the sheet's autumn leaf tints, so that a belt edge of V01 trees over V13 clumps copies letter H's overcast autumn view: mixed green and yellow foliage, a part-bare understory wall at 1–2 m where many dark stems and twigs show through the leaves, and no see-through at 1–2 m.

#### Scenario: Autumn variants built
- **WHEN** `pwsh tools/build_asset.ps1 V13` and `V01` run
- **THEN** each also builds its `_autumn` variants with the autumn leaf material, and every audit and budget scenario of the summer variants holds for them

#### Scenario: Matches H
- **WHEN** a row of `V13_mid_autumn` clumps in front of `V01_M_autumn` trees is rendered at the H viewpoint (1–1.5 m high, 5–10 m out, overcast) and compared side by side with letter H (stills 1–21) in the local comparison sheet
- **THEN** the understory reads as a twiggy, part-bare wall with stems showing through, the foliage mixes green and yellow as H does, and the 1–2 m band shows no sky

### Requirement: V01 black locust belt tree
The pipeline SHALL build asset V01 (`tree` generator; variants `V01_S`, `V01_M`, `V01_L`) from its params files into Unity prefabs that copy the belt locust of letters F, H and G: a sinuous deeply furrowed trunk (one stem for M and L, two to three for S), few main limbs from 2–5 m ascending at 25–40°, an open scraggly crown the sky reads through, pinnate leaves assembled from leaflet cards, pods in the autumn and bare states. Heights 4–7 / 8–12 / 12–16 m, trunk diameters 5–10 / 12–20 / 20–35 cm and crown widths 2–4 / 3–6 / 5–8 m SHALL sit inside `docs/object-sheets/vegetation.md` §V01; the physics metadata SHALL carry trunk capsules per 1–2 m, every limb and branch of 10 mm or more as a `bark` capsule, a `twig` shell of sphere and capsule pieces following each main limb's leaf mass (the shell rule of `p1-v13-shrub`: standoff within 0.1 m, twigs within 0.1 m, cards within 0.3 m), every inter-limb opening of 0.5 m or more as a gap polygon, a solid trunk wind column and crown wind pieces with porosity 0.55 summer / 0.75 part-bare / 0.9 bare, snag records by the sheet's rule (every segment under 30 mm an `edge` at its capsule's radius, every free tip a `hook`, one record per twig), and no landable surface.

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 V01` runs on a clean checkout
- **THEN** it exits 0 for the three variants, writes their FBX, metadata, impostor atlases, prefabs and review sheets, and a second run leaves `git status` clean

#### Scenario: Sizes and crown inside the sheet
- **WHEN** the metadata bounds and the generator's printed counts are read
- **THEN** each variant's height, trunk diameter and crown width lie in its row's range, the first fork is 2–5 m up, and the capsule count of `V01_M` is printed and lies between 150 and 300

#### Scenario: Openings against the drone
- **WHEN** `V01_M`'s gaps are read
- **THEN** every gap polygon is a planar inter-limb opening with `narrowest_m` at least 0.5 m, the audit's inscribed-circle check agrees within 5 mm, and the handoff lists which pass at any yaw (≥ 0.706 m) and which nose-first only (0.574–0.706 m)

#### Scenario: The sky reads through the crown
- **WHEN** the 10 m tile of `V01_M` in the part-bare state is compared side by side with letter F (frames 1–12) in the local comparison sheet
- **THEN** the crown is open with sky visible through it, the trunks' grey-brown bark and lean match by eye, and the leaf colour sits inside the sheet's range against the F feed value

#### Scenario: A dark band at 200 m
- **WHEN** the 200 m tile of `V01_L` is read and three L trees are placed in a row in the review stage
- **THEN** the impostor crowns read as a dark green-grey mass against the sky with the sheet's canopy value as the reference, with no visible card edges or popping between the last mesh LOD and the impostor in the 50 m and 200 m tiles

#### Scenario: Budget
- **WHEN** the triangle counts per LOD are read
- **THEN** `V01_L`'s LOD0 is under 40 000 triangles, each further LOD at most half the previous, the impostor atlas is 1k, every other texture 2K or less, and the cull distance follows the pixel rule from the tree's height
