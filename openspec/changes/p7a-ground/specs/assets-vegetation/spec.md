# Spec Delta

## ADDED Requirements

### Requirement: Crowns and limbs carry their physics records
V01's and V13's generators SHALL write, for every variant (`p7a-ground`, D-059), the twigs and leaflets their crowns draw as `micro_elements` (a twig with its centre line, diameter and the element or capsule it grows from; a leaflet with its centre line, area and the twig it grows from), the very ones drawn, and on every `bark` capsule a `cantilever` record, the chain length from the member's root (where it leaves a thicker member, or the ground for a trunk) to the capsule's end A. The looks, the collision shapes, the shells, the snags, the gaps and the wind volumes SHALL be unchanged.

#### Scenario: Crown elements are the drawn ones
- **WHEN** each variant's `micro_elements` are compared with its LOD0 twig tubes and leaf cards
- **THEN** every drawn twig and leaflet has exactly one record (a twig on its tube's centre line within 1e-6 m, a leaflet on its card's midrib within 1e-6 m with the card's area), every parent index names the twig or capsule the element leaves, and the record counts by kind are printed

#### Scenario: A limb knows its root
- **WHEN** each `bark` capsule's `cantilever` record is checked against the generator's member chain
- **THEN** its length equals the chain length from the member's root to the capsule's end A within 1 mm, a trunk's or a base stem's first capsule records 0, and the rebuild leaves the meshes and every other metadata record bit for bit
