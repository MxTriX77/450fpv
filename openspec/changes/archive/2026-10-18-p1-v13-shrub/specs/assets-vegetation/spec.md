# Spec Delta

## Purpose

The assets of the vegetation object sheet (V01–V21: belt trees, yard trees, shrubs, dead wood, weeds): what each built asset must show and carry, judged against its reference letter and its object sheet. One requirement per asset id.

## ADDED Requirements

### Requirement: V13 belt understory shrub
The pipeline SHALL build asset V13 (`shrub` generator; variants `V13_low`, `V13_mid`, `V13_tall`) from its params files into Unity prefabs that copy the understory clumps of letters H and F: many thin stems from the base, a dense leafy mass of small leaves, the low variant leafy to the ground, the tall variant bare for 1 m under the canopy. Dimensions, stem counts and colours SHALL sit inside the ranges of `docs/object-sheets/vegetation.md` §V13, and the physics metadata SHALL carry every stem of 10 mm or more as `bark` capsules with one snag record per capsule segment (an `edge` on each lower segment, a `hook` on the top one, each at its capsule's radius), one snag record per twig (an `edge` where other twigs leave its end, a `hook` where its end is free, at its mean radius), the leafy mass as a `twig` shell of at most 24 sphere or capsule pieces that follows the leaf and twig mass, one wind volume over the leafy mass with the clump's porosity, no gap and no landable surface (`review-physics.md`).

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
- **THEN** every capsule names `bark` and lies within 5 cm of its stem tube; each stem carries one record per capsule segment (lower segments `edge`, the top one a `hook` to the tip, each at its capsule's radius) and each twig one record (`edge` where other twigs leave its end, `hook` where it is free, at its mean radius); the `twig` shell is at most 24 sphere or capsule pieces whose surface above the ground stands within 0.1 m of the nearest leaf or twig face, with every twig vertex inside it or within 0.1 m outside and every leaf-card vertex inside it or within 0.3 m outside, leaving the bare-stem zone to the capsules; the wind volume spans the leafy mass from its lowest leaf or twig vertex, with porosity 0.35 for the summer state; `gaps` is empty and no shape is landable

#### Scenario: LODs and cull under budget
- **WHEN** the triangle counts per LOD are read from the metadata
- **THEN** LOD0 is under 12 000 triangles, each further LOD is at most half the previous, the cull distance follows the pixel rule from the clump's height, and the textures are 2K or less
