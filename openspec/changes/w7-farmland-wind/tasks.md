# Tasks: w7-farmland-wind

Order: 1.1, then 1.2, then 2.1. 1.1 merged into `--w3-ground` (595ca9fa), so W7 lands with W3; 1.2 merges back the same way. Branch `unity/physics/physics-integration-2--w7-wind`, cut from `--w3-ground` at 23e1b148.

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
- [x] 1.2 [physics-engineer] The belts and the village.
  - As built (design.md, decisions 4–6; model 84 extended, WakeGrid version 2):
    - **What the check found:** the farmland has no shelterbelt (no "posadka" group): its trees stand in groves and short rows, its bushes in field-edge hedges, its ruins are thin-walled shells. The wake grid kept only the nearest obstacle, so F7's hedge cut the F6|F7 trees' wake at 6 m (0.54 to 1.00); and its cell porosity rule diluted thin walls, missed them between samples and multiplied stacked courses and trunks under crowns.
    - **The lane's frontal porosity** (`WakeObstacles.Transmission`): solids opaque however thin, porous parts by Beer–Lambert over their chord, parts at different heights side by side; a cell is an obstacle where a footprint overlaps it.
    - **The overlying wake:** each cell also keeps the nearest taller obstacle upwind; the speeds multiply and the excess variances add. The two slots halve the shelter dropped (bound > 0.1 in 11–16 % of the farmland's wake cells against 24–29 %).
    - **The porous boost** scales with (1 − β), anchored at the fit's 0.45.
    - **Version 1** is kept bit for bit behind `WakeLayout.Version` (logged); the kept audits fly it.
    - **Rows** (`worldbench`, `golden/farmland-shelter.txt`) against Brandle, Zhou and Hodges 2002 (read): F7's hedge, 25 % dense, 0.63 / 0.78 / 0.84 / 0.87 / 0.92 at 5 / 10 / 15 / 20 / 30 H against 0.50 / 0.65 / 0.80 / 0.85 / 1.00, within a class step; F6|F7's short tree pairs; behind the east pair 0.72–0.74 at 2–5 H where the nearest wake alone reads 0.95; Ruin_RoofCollapsed's reverse flow to 2.0 H against Fackrell's 2.10 H; Ruin_Stump inside its cavity at −0.24 to −0.30 where the nearest wake alone reads +0.66 to +0.75.
    - **Goldens:** worldbench `wakes` re-recorded at version 2 (the fixture belt's β 0.473 → 0.466, still Perera's), `farmland-shelter` new, the rest unchanged; the range golden flight re-pinned (version 1 gives model 76's pin, version 2 at most 1.7 mm and 0.02° from it); the cut's and the map's wind pins re-pinned with version 1 kept; the house check moved to H01_I; every kept audit holds; `--slopes` the base's.
    - **Costs** (shared machine): the farmland's grid builds in 0.62 s over the cores (0.36–0.42), rebuilds in 1.15–1.39 s on the worker (0.54–0.96, inside the 3 s takeover), samples in 0.79–0.85 µs (0.49–0.59), 64 MB a grid (32).
  - Check the wakes and canopy flow behind the farmland's tree belts and in the village against published shelterbelt data. Fix what is missing from the physics, not a tuned value.
  - Rows behind a belt and in the village, as goldens.
  - Wind and world code only; the tether stays as it is (1.3).
  - Model 84, extended, with every moved golden audited case by case.
  - Checks: as 1.1, plus `dotnet test tools/inputtests`.
- 1.3 moved: the laid fiber's rest (the rest test, the shared soil cast, the Severe re-run) is W3's task 2.4 (`w3-farmland-ground`), because 2.4 owns the tether and W7 1.1 had already merged into `--w3-ground`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec, the checks, the rows' references and the golden audits, plus a quiet-machine run of the wind's cost and `farmland-line-step`.
