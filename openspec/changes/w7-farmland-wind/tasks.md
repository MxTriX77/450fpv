# Tasks: w7-farmland-wind

Order: 1.1, then 1.2, then 2.1. Branch `unity/physics/physics-integration-2--w7-wind`, cut from `--w3-ground` at 23e1b148 (W3 1.2's objects merged back). Merge `--w3-ground` in again before 2.1 if it has moved. It merges onto the line after W3.

## 1. The wind over the farmland

- [ ] 1.1 [physics-engineer] The height over the local ground, and the hills.
  - Every wind query (rotors, body, fiber nodes) takes its height from the ground under that point, through `SampleGround` or the fiber's own ground, never from the drone's ground.
  - Choose a published model for the speed-up over low hills and for the lee, with its source and validity range in design.md, and separated flow where the slope is past that range (the ledge).
  - Rows on the farmland: a crest, a lee slope, below the ledge, an open field. Each prints the model's value against the published reference.
  - Flat-ground answers unchanged: the floor and test-range wind goldens hold, or each moved case is audited and explained.
  - Determinism (IEEE basic operations, `DetMath`), 0 B in steady state, bit-exact replays, and the wind's cost within PLT-04.
  - Write design.md.
  - Checks: `dotnet test tools/simtests`, worldbench and flightbench `--golden`, flightbench `--slopes`, `python tools/hygiene/precommit.py --tree HEAD`.
- [ ] 1.2 [physics-engineer] The belts, the village and the laid fiber.
  - Check the wakes and canopy flow behind the farmland's tree belts and in the village against published shelterbelt data. Fix what is missing from the physics, not a tuned value.
  - Rows behind a belt and in the village, as goldens.
  - Rerun `flightbench --bench farmland-line-step` in Severe wind. Report the far nodes' speeds, how many freeze, and the step's p99 before and after.
  - Model number (D-072), with every moved golden audited case by case.
  - Checks: as 1.1, plus `dotnet test tools/inputtests`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec, the checks, the rows' references and the golden audits, plus a quiet-machine run of the wind's cost and `farmland-line-step`.
