# Tasks

## 1. Pipeline and sourcing

- [ ] 1.1 [world-artist] Write `tools/blender/export.py` (headless builder → `.blend` → `.glb`, LODs, budget report, fails on excess) and the catalog-entry conventions. Verify the one-command and budget-report scenarios on one simple prop
- [ ] 1.2 [world-artist] Write `tools/assets/fetch_cc0.py` (Poly Haven and ambientCG by id, resolution and SHA-256) and `CREDITS.md`. Fetch the core texture set from notes §11 item 11, **only after the pilot's download permission**. Verify the credits scenario

## 2. Terrain

- [ ] 2.1 [world-artist] Add the `burnt_field` surface (values from the notes and soil mechanics). Verify with the validator
- [ ] 2.2 [physics-engineer] Sign off `burnt_field` and the terrain-holes design (material resolution of trench walls, hole handling in contacts and rays). Verify by review notes in the change
- [ ] 2.3 [world-artist] Add terrain holes (format 1.1 `holes.png`, renderer discard, holed collision, `SampleGround` Hole flag, `surface:<id>` materials, QueryVersion 3, golden re-record). Verify the trench-open, trench-wall and golden scenarios
- [ ] 2.4 [world-artist] Real surface textures for all 10 surfaces, with tilled furrows along the ridge azimuth, filtered with distance. Verify with screenshots looked at, and that meadow and weeds are visibly distinct
- [ ] 2.5 [world-artist] Add the micro-detail near-ring fade and a far density layer out to at least 60 m. Verify the no-hard-edge scenario with a screenshot, and that parity stays at 0 mm

## 3. Assets (in order)

- [ ] 3.1 [world-artist] Tree belt: 3–4 tree types (per notes V1), shrubs, a dead tree, and a far impostor. Verify budgets, catalog wind volumes, and screenshots
- [ ] 3.2 [world-artist] Damaged adobe house (stripped roof, enterable door, far window) plus yard set (cellar entrance with a hole, shed, fence and gate, fruit tree). Verify the door-gap and collision-follows-visual scenarios
- [ ] 3.3 [world-artist] Vehicles, first pass: a classic ВАЗ and an Урал, each abandoned and destroyed. Verify budgets, collision and screenshots against photos M–O and notes R0–R5
- [ ] 3.4 [world-artist] Destroyed 5-storey Stalin-era block plus rubble mound, with room openings as gaps. Verify budgets, gaps and screenshots against photos I and K
- [ ] 3.5 [world-artist] Brick house variant; a Lanos-class car and a ЗИЛ, each in both states; poles and wires (including leaning ones); debris kit; corrugated roof sheet; a trench section mesh. Verify budgets and collision

## 4. Gallery

- [ ] 4.1 [world-artist] Generator for `uat1_gallery` (512 m): patches, stations, and labels (key-toggleable). Verify the everything-present and label scenarios
- [ ] 4.2 [world-artist] Fly the gallery path and look at a screenshot per station and patch. Verify the busiest-view performance and load time, with clock and power mode

## 5. Review

- [ ] 5.1 [physics-engineer] Review every new catalog entry: collision, materials, wind volumes, gaps, snag flags. Verify by a probe (a drone-sized capsule through every gap; contacts on each material)
- [ ] 5.2 [qa-engineer] Review against spec scenarios before the pilot sees it
- [ ] 5.3 [user-review] **UAT-1:** the pilot flies the gallery in noclip and gives feedback. Every point becomes a task, fixed or deferred with a reason
- [ ] 5.4 [qa-engineer] Review against spec scenarios after the pilot's feedback is applied, then clear for merge
