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
A collision shape whose material is a crown contact material (`twig` or `foliage`) is a shell. The audit SHALL require (a) every point of the surface of the union of the shell pieces above the ground, not inside another piece, to lie within the envelope tolerance (0.1 m, derived in `p1-v13-shrub` review-physics) of the nearest face of the shell's slots; (b) every LOD0 vertex of a geometry slot (an opaque recipe, such as twig tubes) to lie inside the union or within 0.1 m outside it; and (c) every LOD0 vertex of a card slot (an alpha-clipped recipe, such as leaf cards) to lie inside the union or within 0.3 m outside it (one sprig length plus the tolerance: cards have no contact role, their twigs carry snag records). It SHALL print each piece's largest standoff and where it is, and per slot the largest outside distance, and fail beyond each tolerance naming the piece or slot, the distance and the tolerance. This relaxes QA's first scenario (8 cm for a card corner) on the physics review's grounds: on a ragged crown no shell meets 5 cm for every card and 0.1 m standoff at once.

#### Scenario: Pieces hugging the mass pass
- **WHEN** two overlapping `twig` spheres hug a leaf mass, every twig vertex inside them and every card corner within 0.25 m outside
- **THEN** the audit passes the shell and prints each piece's largest standoff and each slot's largest outside distance

#### Scenario: Shell too large for its mass
- **WHEN** a `twig` piece's radius is 0.3 m larger than the leaf mass it carries
- **THEN** the audit fails rule (a) naming the piece, its largest standoff and the 0.1 m tolerance

#### Scenario: Twig outside the shell
- **WHEN** one twig vertex lies 0.15 m outside the `twig` shell
- **THEN** the audit fails rule (b) naming the slot, the distance and the 0.1 m tolerance

#### Scenario: Card outside the shell
- **WHEN** one card's corner lies 0.35 m outside the `twig` shell
- **THEN** the audit fails rule (c) naming the slot, the distance and the 0.3 m allowance
