# Tasks: p1-pipeline-hardening

## 1. Pipeline

- [x] 1.1 [asset-artist] Parameter ranges per generator, validate-before-export, and the automated collision audit and `narrowest_m` check in `build.py`, with tests; verify the K01 build is unchanged and the two negative scenarios fail as specified
- [ ] 1.2 [asset-artist] Derived packed mask maps by script from the material recipe, wired into URP/Lit; K01's faces UV'd into one brick cell; verify `pwsh tools/build_asset.ps1 K01` exits 0, the review sheet shows one brick surface without mortar joints across a face, and the mask is regenerated when the recipe changes
- [ ] 1.3 [asset-artist] Cylinder primitives as convex mesh colliders in `CatalogImporter`, with an EditMode test on a throwaway metadata file; verify the prefab builds
- [ ] 1.4 [tech-artist] Capture fails on an empty stage (LFS pointer scenario) and prints the LOD bias; `lodBias` 1.0 on Low, Medium and High with the reason recorded; verify the K01 capture still exits 0 and a pointer FBX makes it exit 1
- [ ] 1.5 [physics-engineer] Sign off the physics ids the six object sheets propose (collected in `openspec/changes/p1-object-sheets/review-buildability.md`): add, map or decline each in `assets-src/physics_materials.json` with labelled values and a `review-materials.md` in this change folder; accept `surface:<surface-id>` (the thirteen S01–S13 names) in `tools/blender/lib/meta.validate` with a test; verify `python -m pytest tools/blender -q` passes and K01's outputs are unchanged

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios
