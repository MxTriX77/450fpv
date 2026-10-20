# Spec Delta

## ADDED Requirements

### Requirement: Earthworks are shot along the run
For an asset of catalog class `earthworks` with a `holes` record, the review capture SHALL shoot the pilot-height tile from 1.5 m above the lip at the first point of the hole's centre line, looking along the run and 20° down, with the pilot-height bar in frame beside the cut; the 10 m, 50 m and 200 m tiles, the turntable and the collision overlay keep their framing.

#### Scenario: Pilot-height tile of a trench
- **WHEN** the capture renders an `earthworks` asset with a 24 m run
- **THEN** the 1.5 m tile shows the cut running away from the camera between its parapets with the bar visible, and the capture log prints the camera position and look direction it used

#### Scenario: Other classes unchanged
- **WHEN** the capture renders K01 and S04
- **THEN** their tiles are pixel-identical to the previous capture
