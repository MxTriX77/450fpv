# Tasks: p1-materials-per-part

Standard tier (D-033). Branch `unity/physics/p1-materials-per-part` off `unity/main`. Every agent on Opus 5.5.

## 1. Rows

- [x] 1.1 [physics-engineer] Re-derive or confirm every row of `assets-src/physics_materials.json` per touching part (a 15 mm leg foot, an arm tube, the body or the coil: the part sizes from `docs/knowledge.md` §5 and §7), each with its label and a rationale that names the part and the sink it gives; add `razor_wire` and `sandbag` from the fortifications physics reading §4; say for each changed row which object sheets and which asset's audit read it (the soft-top rule at 1000 N/m), and fix the `fields` text; verify `python -m pytest tools -q` passes, `pwsh tools/build_asset.ps1 S04`, `V13` and `K01` exit 0 with their audit lines unchanged or every changed line explained, `python tools/hygiene/precommit.py --tree HEAD` clean, `openspec validate p1-materials-per-part --strict`

## 2. Review

- [x] 2.1 [qa-engineer] review against spec scenarios: every row read per touching part with its label and rationale, the two new rows as the reading gave them, the consumers listed and checked, the builds, hygiene
