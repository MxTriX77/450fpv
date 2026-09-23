# Tasks

## 1. Renderer spike

- [ ] 1.1 [world-artist] Generate a synthetic 4 km `.r16` heightfield (`tools/map/make_synthetic.py`). Measure Terrain3D against a chunked mesh in the sandbox (fps, 1 % low, load time, VRAM). Verify that the chosen option meets the map-loading budget, and record D-009 in `docs/decisions.md` with the numbers

## 2. Format

- [ ] 2.1 [world-artist] Write the manifest schema, `surfaces.json` (9 surfaces from notes §7) and `catalog.json` (primitive placeholder assets plus a wire). Verify with the validator
- [ ] 2.2 [physics-engineer] Review the surface fields, units and ranges and sign them off in `surfaces.json` review notes. Verify that every field has a unit and a physical range
- [ ] 2.3 [world-artist] Write `tools/map/validate_map.py` (stdlib only) covering every map-format scenario. Verify each failure scenario with a deliberately broken copy of the sample, created in a temp folder

## 3. World query

- [ ] 3.1 [world-artist] Load the C# layers and add terrain height, ground height, normal, surface and cover queries. Verify the bilinear-accuracy and micro-relief scenarios in a `--selftest worldquery` run
- [ ] 3.2 [world-artist] Add the hash-based `MicroDetailNear`. Verify the replay-identity, overlap and density scenarios, with two processes for replay identity
- [ ] 3.3 [world-artist] Add the wind-obstacle grid derived at load. Verify the house-shadow scenario
- [ ] 3.4 [world-artist] Add the query benchmark. Verify the timings and zero allocations on the dev machine
- [ ] 3.5 [physics-engineer] Review the query API for use by the flight model (signatures, units, determinism). Verify by writing a 20-line contact-probe sketch against it (not committed)

## 4. Loading and sample

- [ ] 4.1 [world-artist] Build the loader: terrain via the D-009 renderer, materials per surface, catalog objects, wires, and near-camera micro-detail from the shared generator. Verify the sample-patch and stem-parity scenarios
- [ ] 4.2 [world-artist] Add the sandbox `-- --map <id>` hook with a bad-id fallback. Verify both headless
- [ ] 4.3 [world-artist] Author `sample_patch` (256 m) covering all 9 surfaces, a few catalog objects, one wire and some relief. Verify that it passes the validator and loads
- [ ] 4.4 [world-artist] Measure performance: fly a fixed path through `sample_patch` and the synthetic 4 km map. Verify the budget numbers and power mode

## 5. Review

- [ ] 5.1 [user-review] The pilot flies `sample_patch` in noclip and comments on the surfaces, the scale and the straw/stem look. This is a format and feel check, not final art
- [ ] 5.2 [qa-engineer] Review against spec scenarios
