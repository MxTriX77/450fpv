## ADDED Requirements

### Requirement: The farmland's trees and bushes have physics
Every tree and bush variant the farmland uses SHALL carry physics metadata generated from its mesh: trunk and main limbs as capsules that follow the visual, the crown as volumes with drag, snag and prop strikes, and bushes as soft volumes. The params and metadata SHALL be pushed; anything that copies vendor geometry SHALL stay local-only.

#### Scenario: Collision follows the trunk
- **WHEN** each tree variant's capsules are compared with its mesh
- **THEN** every trunk and main limb wider than 5 cm (assumed) is covered by a capsule whose surface lies within 3 cm (assumed) of the mesh's surface, and the review sheet shows them at 1.5, 10, 50 and 200 m

#### Scenario: A crown is felt
- **WHEN** flightbench flies the drone through a farmland tree's crown at 10 m/s (seeded)
- **THEN** the log shows the crown's drag and any prop strikes, and the crown rows pass
