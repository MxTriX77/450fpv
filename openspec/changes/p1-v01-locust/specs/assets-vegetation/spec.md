# Spec Delta

## ADDED Requirements

### Requirement: The H autumn belt state
V01 and V13 SHALL each have autumn variants built from the same generators with `leaf_cover` 0.2–0.5 for V01 and 0.3–0.5 for V13 (`V13_tall` cannot be shelled below 0.3), the twig mesh kept dense, and the sheet's autumn leaf tints, so that a belt edge of V01 trees over V13 clumps copies letter H's overcast autumn view: mixed green and yellow foliage, a part-bare understory wall at 1–2 m where many dark stems and twigs show through the leaves, and no see-through at 1–2 m.

#### Scenario: Autumn variants built
- **WHEN** `pwsh tools/build_asset.ps1 V13` and `V01` run
- **THEN** each also builds its `_autumn` variants with the autumn leaf material, and every audit and budget scenario of the summer variants holds for them

#### Scenario: Matches H
- **WHEN** a row of `V13_mid_autumn` clumps in front of `V01_M_autumn` trees is rendered at the H viewpoint (1–1.5 m high, 5–10 m out, overcast) and compared side by side with letter H (stills 1–21) in the local comparison sheet
- **THEN** the understory reads as a twiggy, part-bare wall with stems showing through, the foliage mixes green and yellow as H does, and the 1–2 m band shows no sky

### Requirement: V01 black locust belt tree
The pipeline SHALL build asset V01 (`tree` generator; variants `V01_S`, `V01_M`, `V01_L`) from its params files into Unity prefabs that copy the belt locust of letters F, H and G: a sinuous deeply furrowed trunk (one stem for M and L, two to three for S), few main limbs from 2–5 m ascending at 25–40°, an open scraggly crown the sky reads through, pinnate leaves assembled from leaflet cards, pods in the autumn and bare states. Heights 4–7 / 8–12 / 12–16 m, trunk diameters 5–10 / 12–20 / 20–35 cm and crown widths 2–4 / 3–6 / 5–8 m SHALL sit inside `docs/object-sheets/vegetation.md` §V01; the physics metadata SHALL carry trunk capsules per 1–2 m, every limb and branch of 10 mm or more as a `bark` capsule, a `twig` shell of sphere and capsule pieces fitted per main limb (the fewest of 24 to 64 per fit, a limb split by its child subtrees when 64 do not pass) and audited per tree under the shell rule (standoff within 0.1 m, twigs within 0.1 m, leaf and pod cards within 0.35 m, at most 64 pieces meeting any 2 m cube), every twig a tube ringed at each kink at most 0.1 m apart, every inter-limb opening of 0.5 m or more as a gap polygon bounded by capsules and shell pieces and crossed by none, a solid trunk wind column below one crown wind volume over the twig, leaf and pod mass, never overlapping, with porosity 0.55 summer / 0.75 part-bare / 0.9 bare, snag records by the sheet's rule (every segment under 30 mm an `edge` at its capsule's radius, every free tip a `hook`, one polyline record per twig through its rings), each naming the physics material of the member it follows, and no landable surface.

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

## MODIFIED Requirements

### Requirement: V13 belt understory shrub
The pipeline SHALL build asset V13 (`shrub` generator; variants `V13_low`, `V13_mid`, `V13_tall`) from its params files into Unity prefabs that copy the understory clumps of letters H and F: many thin stems from the base, a dense leafy mass of small leaves, the low variant leafy to the ground, the tall variant bare for 1 m under the canopy. Dimensions, stem counts and colours SHALL sit inside the ranges of `docs/object-sheets/vegetation.md` §V13, and the physics metadata SHALL carry every stem of 10 mm or more as `bark` capsules with one snag record per capsule segment (an `edge` on each lower segment, a `hook` on the top one, each at its capsule's radius), one snag record per twig (an `edge` where other twigs leave its end, a `hook` where its end is free, at its mean radius), the leafy mass as a `twig` shell of sphere or capsule pieces that follows the leaf and twig mass (the fewest of 24 to 64 that pass, at most 64 meeting any 2 m cube), every snag record naming the physics material of the member it follows, one wind volume over the leafy mass with the clump's porosity, no gap and no landable surface (`review-physics.md`).

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 V13` runs on a clean checkout
- **THEN** it exits 0 for all three variants, writes their FBX, metadata, prefabs and review sheets, and a second run leaves `git status` clean

#### Scenario: Sizes inside the sheet
- **WHEN** the three variants' metadata bounds are read
- **THEN** heights are 1–1.5 m, 1.5–2.5 m and 2.5–3.5 m and widths 1–2 m, 1.5–3 m and 2–3 m, and the tall variant's lowest leaf card is at least 0.9 m above the base

#### Scenario: Reads as a wall at the belt edge
- **WHEN** the review sheet's 1.5 m and 10 m tiles of `V13_mid` are compared side by side with letter H (frames 1–21) in the local comparison sheet
- **THEN** the clump is opaque from the ground up at 1–2 m height, leaves read as small leaves with twigs visible between them at 1.5 m, and the foliage colour matches the H feed value by eye, within the sheet's range

#### Scenario: Stems and shell carry the contact
- **WHEN** the metadata of any variant is validated and audited
- **THEN** every capsule names `bark` and lies within 5 cm of its stem tube; each stem carries one record per capsule segment (lower segments `edge`, the top one a `hook` to the tip, each at its capsule's radius) and each twig one record (`edge` where other twigs leave its end, `hook` where it is free, at its mean radius); every record names its material (`bark` on stems, `twig` on twigs); the `twig` shell is the fewest of 24 to 64 sphere or capsule pieces that pass, at most 64 meeting any 2 m cube, whose surface above the ground stands within 0.1 m of the nearest leaf or twig face, with every twig vertex inside it or within 0.1 m outside and every leaf-card vertex inside it or within 0.3 m outside, leaving the bare-stem zone to the capsules; the wind volume spans the leafy mass from its lowest leaf or twig vertex, with porosity 0.35 for the summer state; `gaps` is empty and no shape is landable

#### Scenario: LODs and cull under budget
- **WHEN** the triangle counts per LOD are read from the metadata
- **THEN** LOD0 is under 12 000 triangles, each further LOD is at most half the previous, the cull distance follows the pixel rule from the clump's height, and the textures are 2K or less
