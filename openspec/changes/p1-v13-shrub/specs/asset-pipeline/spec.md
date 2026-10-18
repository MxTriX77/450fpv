# Spec Delta

## ADDED Requirements

### Requirement: Alpha-clipped two-sided recipes
A material recipe MAY declare `alpha_clip` (a cutoff in 0–1) and `two_sided: true`; the importer SHALL build a URP/Lit material that clips by the diffuse alpha at that cutoff and renders both faces, and the texture import SHALL keep the alpha channel.

#### Scenario: Leaf card material
- **WHEN** a recipe names a LeafSet with `alpha_clip` 0.5 and `two_sided` true
- **THEN** the imported material has alpha clipping on at 0.5 and culls no face, and a leaf card renders its silhouette from both sides in the review sheet

### Requirement: Compliant shells in the collision audit
A collision shape whose material is a crown contact material (`twig` or `foliage`) is a shell. The audit SHALL require every LOD0 vertex of the shell's slots to lie inside the shell or within 5 cm outside it, and SHALL NOT require the shell's surface to lie near the mesh; it SHALL print each shell with the largest outside distance it found.

#### Scenario: Shell encloses the cards
- **WHEN** a variant's leaf cards all lie inside its `twig` sphere
- **THEN** the audit passes the shell and prints its name with the largest outside distance 0

#### Scenario: Card outside the shell
- **WHEN** one card's corner lies 8 cm outside the `twig` shell
- **THEN** the audit fails naming the shell, the distance and the tolerance
