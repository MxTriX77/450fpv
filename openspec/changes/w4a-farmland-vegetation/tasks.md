# Tasks: w4a-farmland-vegetation

Order: 1.1, then 1.2, then 2.1. Branch `unity/physics/physics-integration-2--w4a-vegetation`, cut from the build line after W0 landed.

## 1. The physics data

- [x] 1.1 [asset-artist] For every tree and bush variant the farmland uses: generate trunk and limb capsules, crown volumes (drag, snag, prop strikes) and bush soft volumes from the vendor meshes, by a script with per-variant params. Push params and metadata; keep copied geometry local-only. Write `design.md` with the params and the physics metadata per variant. A review sheet per variant. Checks: `python -m pytest tools`, `fetch.py --verify`, flightbench crown rows, `precommit.py --tree HEAD`.

- [x] 1.2 [asset-artist] QA's fix round (`review.md`):
  - (1) Read the bushes' alpha from the TIFF's fourth channel when the texture's import does, pin the `.meta` read, refuse a clipping material whose base map gives no alpha, add a 4-channel TIFF test, rebuild the 9 bushes, re-render VF06–VF10, and correct design §5, §8 and the table.
  - (2) The crown rows require twig contacts for the four foliage trees and a deadwood first touch for PineDead_02, and a shell-stripped run fails them.
  - Also: pin the shader source, the mesh and texture `.meta` files and the LOD1+ meshes; update design risk 1 (W2's rows carry rotation and a per-axis scale, `kind` plus `variant`); give Pine002_L and _M a trunk column under their crown.
  - Checks: as 1.1, with the shell-stripped run.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; look at every review sheet; the checks.
