# Tasks: setup-asset-pipeline

## 1. Pipeline (sequential on `unity/art/setup-asset-pipeline`, one fresh agent per task)

- [x] 1.1 [asset-artist] Add `tools/assets/fetch.py`, `tools/assets/manifest.json` with K01's one CC0 brick texture set at 1k, the fetched files under `Art/ThirdParty/`, generated `CREDITS.md`, and `tools/assets/tests/`; verify `python tools/assets/fetch.py --verify` exits 0, a tampered file fails it, and `python -m pytest tools/assets` passes
- [x] 1.2 [asset-artist] Add `tools/blender/build.py`, the `debris_brick` generator, `assets-src/debris/K01_whole.json` and `assets-src/catalog.json`; verify the headless build writes `K01_whole.fbx` and `K01_whole.assetmeta.json`, with bounds 0.250 × 0.120 × 0.065 m ±1 mm, in Unity's frame
- [x] 1.3 [asset-artist] Add `CatalogImporter.cs` with its postprocessor, `AssetPhysicsMeta.cs`, the K01 material recipe and `tools/build_asset.ps1`; verify `pwsh tools/build_asset.ps1 K01` exits 0 and produces the prefab with LOD group, box collider, URP material and metadata component, and that a second run leaves `git status` clean
- [x] 1.4 [physics-engineer] Review the metadata schema v1 and K01's metadata against AST-05 and D-019; record the verdict in `openspec/changes/setup-asset-pipeline/review-physics.md`
- [x] 1.5 [asset-artist] Apply the physics review (`review-physics.md` R1–R3): add `assets-src/physics_materials.json` with the review's starting rows, enforce the R2 conventions and the closed material list in `lib/meta.validate` with one test per rule, and make the X-sign probe permanent (`tools/blender/tests/make_xsign_probe.py` → a committed fixture FBX under `unity/Assets/_Project/Tests/EditMode/Fixtures/` plus an EditMode test asserting its bounds); verify `python -m pytest tools/blender -q` passes, K01's committed metadata is unchanged, and the EditMode run passes with the probe test listed

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios
