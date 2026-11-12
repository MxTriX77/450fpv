# Tasks: w4a-farmland-vegetation

Order: 1.1, then 1.2, then 1.3, then 2.1. Branch `unity/physics/physics-integration-2--w4a-vegetation`, cut from the build line after W0 landed.

## 1. The physics data

- [x] 1.1 [asset-artist] For every tree and bush variant the farmland uses: generate trunk and limb capsules, crown volumes (drag, snag, prop strikes) and bush soft volumes from the vendor meshes, by a script with per-variant params. Push params and metadata; keep copied geometry local-only. Write `design.md` with the params and the physics metadata per variant. A review sheet per variant. Checks: `python -m pytest tools`, `fetch.py --verify`, flightbench crown rows, `precommit.py --tree HEAD`.

- [x] 1.2 [asset-artist] QA's fix round (`review.md`):
  - (1) Read the bushes' alpha from the TIFF's fourth channel when the texture's import does, pin the `.meta` read, refuse a clipping material whose base map gives no alpha, add a 4-channel TIFF test, rebuild the 9 bushes, re-render VF06–VF10, and correct design §5, §8 and the table.
  - (2) The crown rows require twig contacts for the four foliage trees and a deadwood first touch for PineDead_02, and a shell-stripped run fails them.
  - Also: pin the shader source, the mesh and texture `.meta` files and the LOD1+ meshes; update design risk 1 (W2's rows carry rotation and a per-axis scale, `kind` plus `variant`); give Pine002_L and _M a trunk column under their crown.
  - Checks: as 1.1, with the shell-stripped run.

- [x] 1.3 [asset-artist] The re-review's should-fixes (`review.md`, re-review section):
  - (1, D-089) No pushed point equals a vendor vertex: start each spray line at its first bin's mean, not at a drawn sample, and add a guard and a test like W4b's (any pushed coordinate within 0.1 mm of a vendor LOD0 vertex fails). Today 1,252 single points match: card corners and tube roots.
  - (2) Stand the Pine002 trunk columns on the trunk's axis, not the root flare's mean.
  - (3) The byte-for-byte rebuild test covers one variant per family, not only SmallBushDry_A.
  - Rebuild all 22, re-render the sheets whose physics changed, and look at them. Checks: as 1.2, with QA's `plant_vertex_leak.py` at 0 hits.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; look at every review sheet; the checks.
