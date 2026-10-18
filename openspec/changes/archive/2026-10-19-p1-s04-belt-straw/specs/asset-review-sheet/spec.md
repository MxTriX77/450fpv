# Spec Delta

## ADDED Requirements

### Requirement: Surface assets are shot from on the surface
For an asset of catalog class `surfaces`, the review capture SHALL shoot the pilot-height tile from 1.5 m above the surface at the patch centre, looking down at the surface at a steep angle with the pilot-height bar in frame ahead of the camera, instead of placing the camera outside the asset's bounds; the 10 m, 50 m and 200 m tiles, the turntable and the collision overlay keep their framing.

#### Scenario: Pilot-height tile of a ground patch
- **WHEN** the capture renders a `surfaces` asset 12 m across
- **THEN** the 1.5 m tile shows the surface filling the lower frame from under the camera to a few metres ahead, with the pilot-height bar visible, and the capture log prints the camera position and look-down angle it used

#### Scenario: Other classes unchanged
- **WHEN** the capture renders K01
- **THEN** its tiles are pixel-identical to the previous capture
