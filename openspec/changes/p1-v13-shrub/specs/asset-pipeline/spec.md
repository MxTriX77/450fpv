# Spec Delta

## ADDED Requirements

### Requirement: Alpha-clipped two-sided recipes
A material recipe MAY declare `alpha_clip` (a cutoff in 0–1) and `two_sided: true`; the importer SHALL build a lit material that clips by the diffuse alpha at that cutoff and renders both faces, a back face lit with its own (flipped) normal, and the texture import SHALL keep the alpha channel and preserve the alpha coverage at the cutoff in every mip level.

#### Scenario: Leaf card material
- **WHEN** a recipe names a LeafSet with `alpha_clip` 0.5 and `two_sided` true
- **THEN** the imported material has alpha clipping on at 0.5 and culls no face, and a leaf card renders its silhouette from both sides in the review sheet

#### Scenario: Back faces lit as leaves
- **WHEN** a two-sided leaf card is seen from its back under the day light of the review rig
- **THEN** each sRGB channel of its mean over the opaque pixels is within 15 % of the same card seen from its front with the sun behind the camera, so the back reads green like the front; a back face lit with the front's normal (URP/Lit with cull off) fails it with its grey sheen

#### Scenario: Coverage kept at distance
- **WHEN** the clipped base map is imported
- **THEN** its importer preserves mip coverage at the recipe's cutoff, and every mip down to 4 px keeps opaque texels after clipping (without preservation the 4 px mip has none)

### Requirement: Compliant shells in the collision audit
A collision shape whose material is a crown contact material (`twig` or `foliage`) is a shell. The audit SHALL require every LOD0 vertex of the shell's slots to lie inside the union of the shell pieces or within 5 cm outside it, and every point of the shell's surface above the ground to lie within the envelope tolerance (0.1 m, the vegetation sheet's group rule, unless the physics review sets and labels another) of the nearest vertex of its slots; it SHALL print each shell with the largest outside distance and the largest surface-to-mass distance it found.

#### Scenario: Shell encloses the cards
- **WHEN** a variant's leaf cards all lie inside its `twig` sphere
- **THEN** the audit passes the shell and prints its name with the largest outside distance 0

#### Scenario: Shell too large for its mass
- **WHEN** a `twig` piece's radius is 0.3 m larger than the leaf mass it carries
- **THEN** the audit fails naming the shell piece, its largest surface-to-mass distance and the envelope tolerance

#### Scenario: Card outside the shell
- **WHEN** one card's corner lies 8 cm outside the `twig` shell
- **THEN** the audit fails naming the shell, the distance and the tolerance
