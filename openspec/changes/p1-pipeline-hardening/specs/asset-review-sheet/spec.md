# Spec Delta: asset-review-sheet

## ADDED Requirements

### Requirement: An empty stage fails the capture
The capture SHALL exit non-zero when the asset's renderers have no mesh or no texture at the 1.5 m view, naming the missing thing, instead of writing a sheet of empty tiles.

#### Scenario: LFS pointer instead of an FBX
- **WHEN** the asset's FBX is an LFS pointer file
- **THEN** the capture exits non-zero and writes no sheet
