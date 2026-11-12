# Tasks: w7-farmland-wind

Order: 1.1, then 1.2, then 2.1. Branch `unity/physics/physics-integration-2--w7-wind`, cut from `--w3-ground` at 23e1b148 (W3 1.2's objects merged back). Merge `--w3-ground` in again before 2.1 if it has moved. It merges onto the line after W3.

## 1. The wind over the farmland

- [x] 1.1 [physics-engineer] The height over the local ground, and the hills.
  - As built (design.md, decisions 1–3; model 84):
    - **Own ground:** every wind query takes its own ground (`WindField.At(point, ground)`). The drone's points take SampleGround's height, over a hole the step's ground and frame; each fiber node and loose piece takes its own, posted with the node to the batch. A cut's frame holds a point in its opening (`CutFrame.Holds`).
    - **The hills:** Jackson and Hunt's linear theory in Hunt et al.'s layers, spectral and direction-free, built once from the world (`HillFlow`). Taylor and Lee's check: 1.77 h/L1 over a ridge (2.0 ± 15 %), 1.38 over a hill (1.6 ± 15 %).
    - **Past the range:** a step's lee is Cavity's bubble behind a step, 7.2 D.
    - **Rows** (`worldbench`, `golden/farmland-wind.txt`): the crest's ΔS is 0.329 at 2 m against JH 2-D's 0.327; the lee within 0.017 of JH 2-D; reverse flow below the ledge −0.17 U, forward again by 1.25 X_R; a laid node gets 0 against model 82's 7.42 m/s.
    - **Goldens:** the flight goldens and the four world files unchanged; `--slopes`' two line rows moved inside CrestDepth (audited).
    - **Determinism:** DetMath only, 0 B, a farmland flight replays bit for bit.
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
