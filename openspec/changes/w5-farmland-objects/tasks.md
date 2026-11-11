# Tasks: w5-farmland-objects

Order: 1.1 now; 1.2 and 1.3 once W3 (`w3-farmland-ground`) has merged onto the line, since they need its terrain code; then 2.1. Branch `unity/physics/physics-integration-2--w5-objects`, cut from the build line after W4b merged; merge the line into it before 1.2.

## 1. Our objects in the farmland

- [x] 1.1 [asset-artist] Widen `E01_curving`'s filler band to at least 3.6 m (a half-width of at least 1.781 m about the centre line, the niches included) for the farmland's 0.977 m hole cells (W2 design §4, rules 2 and 3). Rebuild it with its generator. Its metadata, review sheet and trench physics rows stay true. Every other placement of E01 (the test range, the gallery) still validates. Checks: `python -m pytest tools`, the asset's review sheet, flightbench and worldbench goldens with any re-record audited case by case, `fetch.py --verify`, `precommit.py --tree HEAD`.
- [ ] 1.2 [world-builder] Place our objects (D-090) in `maps/farmland/`:
  - one `E01_curving` per field F1–F11, at a seeded random spot and yaw, fully inside the field with a margin;
  - `H01_I`, `H01_D`, `R01_A` and `R01_F` in the village on free ground, clear of the ruins, debris, trees and the start point's footprint (D-092);
  - the hole mask (`holes/<tile>.raw`), derived from the trenches as W2 design §4 says;
  - the flattened footprints, as data (D-095).
  `validate.py` recomputes the mask, requires equality, and samples every hole cell every 0.1 m against the filler bands (no naked hole). A review shot of each spot. Checks: `validate.py`, `pytest tools/map`, worldbench goldens, `precommit.py --tree HEAD`.
- [ ] 1.3 [world-builder] Apply at load (D-095). The sim's heightfield takes the hole mask and the flattened footprints. The player applies the same to a runtime copy of the drawn terrain: holes through Unity's inverted sense, checked by a one-cell probe; heights through the same footprints. The store's terrain stays untouched. The tech artist confirms the drawn hole edge is the cell edge. Checks: `dotnet test tools/simtests`, worldbench goldens and bench, EditMode, a review build and the smoke test.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; the checks; fly a capsule into each trench and onto each flattened footprint.
