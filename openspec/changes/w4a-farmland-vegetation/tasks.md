# Tasks: w4a-farmland-vegetation

Order: 1.1, then 2.1. Branch `unity/physics/physics-integration-2--w4a-vegetation`, cut from the build line after W0 landed.

## 1. The physics data

- [ ] 1.1 [asset-artist] For every tree and bush variant the farmland uses: generate trunk and limb capsules, crown volumes (drag, snag, prop strikes) and bush soft volumes from the vendor meshes, by a script with per-variant params. Push params and metadata; keep copied geometry local-only. Write `design.md` with the params and the physics metadata per variant. A review sheet per variant. Checks: `python -m pytest tools`, `fetch.py --verify`, flightbench crown rows, `precommit.py --tree HEAD`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; look at every review sheet; the checks.
