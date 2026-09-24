# Tasks

## 1. Renderer spike

- [x] 1.1 [world-artist] Generate a synthetic 4 km `.r16` heightfield (`tools/map/make_synthetic.py`). Measure Terrain3D against a chunked mesh in the sandbox (fps, 1 % low, load time, VRAM). Verify that the chosen option meets the map-loading budget, and record D-009 in `docs/decisions.md` with the numbers

## 2. Format

- [x] 2.1 [world-artist] Write the manifest schema, `surfaces.json` (9 surfaces from notes §7) and `catalog.json` (primitive placeholder assets plus a wire). Verify with the validator
- [x] 2.2 [physics-engineer] Review the surface fields, units and ranges and sign them off in `surfaces.json` review notes. Verify that every field has a unit and a physical range
- [x] 2.3 [world-artist] Write `tools/map/validate_map.py` (stdlib only) covering every map-format scenario. Verify each failure scenario with a deliberately broken copy of the sample, created in a temp folder
- [x] 2.4 [world-artist] Add the 256 m-multiple side rule to the validator. Verify with a 300 m broken copy
- [x] 2.5 [world-artist] Apply physics review §3–§5: the new surface fields and value corrections, the material table with primitive-only collision and wind volumes in the catalog, optional start points, and the README. Extend the validator (unknown material, the new ranges, element length and diameter > 0, start point bounds). Verify with the validator on new broken-copy cases

## 3. World query

- [x] 3.1 [world-artist] Add batch `SampleGround` (W-1–W-4, W-15): triangulated terrain, relief and ridges, pitfalls with ids, mat, cover, blend and flags. Verify the matches-rendered-surface, continuity, normal, micro-relief, pitfall and outside-map scenarios in `--selftest worldquery`
- [x] 3.2 [world-artist] Add `MicroDetailNear` (W-5, W-6): segment-meets-sphere, kind mask, canonical order, element scaling and overflow. Verify the replay-identity (two processes), overlap, crossing-straw, density and overflow scenarios
- [x] 3.3 [world-artist] Add the wind grid with base heights and path-composable porosity from wind volumes, plus `GapsNear` (W-9, W-10). Verify the house, tree-crown and door-gap scenarios
- [x] 3.4 [world-artist] Add pure-C# swept static contacts and raycasts over catalog primitives and wires, plus runtime objects (W-7, W-8, W-11, D-010). Verify the tunnelling, material, roof-ray and rails scenarios
- [x] 3.6 [world-artist] Add the query benchmark (W-16) and **optimise until it passes, before 3.5 records the golden file**, because optimisations change the hashes. Plan: per-cell caches, a single-round hash, pitfall pre-rejection. Verify every timing, including the < 60 µs composite step, with zero allocations after warm-up on the dev machine. **Done** with an accepted deviation on the micro-detail budgets at the clocks Windows allowed. See the design. Measured on AC by the orchestrator
- [ ] 3.5 [world-artist] Add the surface, material and soil-reference lookups, the content hash, the determinism golden file and the concurrency check (W-12–W-14). Do this after 3.6. Verify the hash-change, golden-file and concurrent-use scenarios The determinism test also checks that batched and single-point results are byte-identical, because the 4-wide paths must match the scalar ones
- [ ] 3.7 [physics-engineer] Review the query API for use by the flight model and confirm that review §3–§5 is applied. Verify by writing a contact-probe sketch against the real API (not committed)

## 4. Loading and sample

- [ ] 4.1 [world-artist] Build the loader: terrain via the D-009 renderer, materials per surface, catalog objects (colliders tagged with their material), wires, wind volumes into the wind grid, and near-camera micro-detail from the shared generator. Verify the sample-patch and stem-parity scenarios
- [ ] 4.2 [world-artist] Add the sandbox `-- --map <id>` hook with a bad-id fallback. Verify both headless
- [ ] 4.3 [world-artist] Author `sample_patch` (256 m) covering all 9 surfaces, a few catalog objects, one wire and some relief. Verify that it passes the validator and loads
- [ ] 4.4 [world-artist] Measure performance: fly a fixed path through `sample_patch` and the synthetic 4 km map. Verify the budget numbers and power mode

## 5. Review

- [ ] 5.1 [orchestrator] Fly `sample_patch` in noclip, check the surfaces, scale and straw/stem look against the reference notes, and record the findings in `review.md`. This is not a pilot gate: the pilot sees terrain at UAT-1
- [ ] 5.2 [qa-engineer] Review against spec scenarios
