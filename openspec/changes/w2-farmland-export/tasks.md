# Tasks: w2-farmland-export

Order: 1.1, then 1.2, then 1.3, then 2.1. Branch `unity/physics/physics-integration-2--w2-export`, cut from the build line after W0 landed.

## 1. The export

- [x] 1.1 [world-builder] The exporter (`Fpv450.FarmlandExport`) and `maps/farmland/`: heights (map, surround, far ring), ground classes, grass density, every placement with its variant, the field and village outlines. Document the format; extend `validate.py` with tests. Checks: `python tools/map/validate.py maps/farmland`, `python -m pytest tools`, EditMode, `precommit.py --tree HEAD`.
- [x] 1.2 [physics-engineer] Sign off the heights' format for W3: exact triangulation as Unity's terrain, the hole mask's place, the units. A note in `design.md`.
- [x] 1.3 [world-builder] Apply 1.2's class rule and re-export: on any sample with Gravel weight > 0, DryMud counts toward `dirt_track` (13), not `dry_crust` (2); note the rule in `map.json`; make the README's ruin-ring wording match 1.2 (dry_crust, the gravel ring is look only). Checks: as 1.1, plus the main track cores read 100 % `dirt_track`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec; run the checks; confirm two exports are byte-identical.
