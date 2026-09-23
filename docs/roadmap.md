# Roadmap

This follows the pilot's release plan (CLAUDE.md §5). **The pilot reviews only the gates below.** Everything between gates is checked by QA and the orchestrator.

| Gate | What the pilot gets | Pilot does | Status |
|---|---|---|---|
| **UAT-1: map parts** | Representative terrain parts, key objects and textures, shown in a noclip gallery | Reviews them and gives feedback | in progress |
| **UAT-2: the world** | The full composite map (about 4 × 4 km) in noclip: mouse + WASD, Shift for fast | Explores every nook and gives final map feedback | not started |
| **Wireframes** (§4.3) | Screen wireframes: main menu (Train / Calibrate / Exit), setup, calibration | Approves or rejects | not started |
| **MVP-1: first flight** | Launch → connect the TX12 → calibrate → fly, with physics logging | Flies and gives feedback | not started |
| **MVP-2..n** | Builds from the flight feedback | Flies and gives feedback | not started |
| **Release** | | | not started |

## Changes on the way to each gate

**UAT-1**
- `study-flight-references`: footage → reference notes. Folding in the pilot's answers.
- `bootstrap-godot-project`: Godot project, review sandbox, noclip. Done, in QA re-check.
- `define-map-format`: the map package, physical surfaces and materials, world queries, loader. In progress.
- `build-uat1-parts`, planned. It covers:
  - **terrain parts:** tree-belt edge with straw floor, meadow, burnt field, tilled field, crater field, trench
  - **objects:** village house with yard, destroyed 5-storey Stalin-era block, vehicles (ВАЗ, Lanos-class cars, ЗИЛ, Урал; abandoned and destroyed), trees, poles and wires
  - **the core texture set**
  - **all of it laid out in a UAT-1 gallery map**

**UAT-2**
- `add-map-authoring`, planned. The map is described as an editable layout (fields, tree belts, roads, settlements, points of interest, objects) and compiled into the package, so the pilot's future changes are edits rather than rebuilds.
- `build-world-map`, planned: the composite 4 km map.

**Wireframes and MVP-1**
- The game app: wireframes, then menus, setup (payload, legs, time of day), TX12 input and calibration.
- The drone model: Viriy, Bombus, OTU or Beshketnyk class, 3-blade props, fiber spool, legs or rails.
- Physics: flight model, contacts (legs, soil, straw, rails), wind and turbulence, fiber tether, physics logging.
- The analog video feed, simulation-driven (D-008).
