# Proposal: build-uat1-parts

**Owner:** world-artist · **Reviewers:** physics-engineer (new surface, terrain holes, collision and materials of the new assets), tech-artist (material and lighting look), qa-engineer · **Pilot gate:** UAT-1
**Motivation:**
- CLAUDE.md §5.1: the pilot wants to see terrain parts, key objects and textures before the world is built, and to give feedback that gets incorporated.
- §3: Ukrainian посадки, war-damaged houses, typical vehicles, local vegetation and fields, as realistic as possible yet light.
- §4.1: every object detailed, with every nook reacting physically.

## Why

The map format, physics queries and loader are done (`define-map-format`), but everything on screen is still a placeholder. UAT-1 is the pilot's first chance to judge the world. The pieces UAT-2's full map will be built from must be real, reviewable and approved first, so the world is assembled from parts the pilot already trusts.

## What Changes

- An **asset pipeline**: Blender sources in `blender/`, and headless export scripts in `tools/blender/` that write `.glb` files into `game/assets/`.
  - Every asset gets a catalog entry with primitive collision, a contact material, a wind volume, fly-through gaps and snag tags.
  - Assets have LODs and per-class triangle and texture budgets.
- **Real textures** from CC0 libraries only (Poly Haven, ambientCG), fetched by a pinned, checksummed script with a credits file. Nothing licence-encumbered enters the public repo. Downloads happen only with the pilot's permission.
- **Terrain content:**
  - real textures for every surface, with tilled furrows along the ridge direction
  - a new **burnt field** surface (char and ash), signed off by the physics engineer
  - **terrain holes**, so trenches and cellar entrances exist in terrain, rendering, collision and world queries
  - micro-detail that fades with distance, plus cheap far grass instead of the hard 8 m edge
  - meadow and weeds made clearly distinct
- **Assets**, in the order of the reference notes' ranked build list:
  - tree-belt trees, shrubs and a dead tree
  - the damaged adobe house (enterable door, far window) with its yard set: cellar entrance, shed, fence and gate, fruit tree
  - the brick house variant
  - the **destroyed 5-storey Stalin-era block** with its rubble mound
  - **vehicles**, a classic ВАЗ and an Урал truck first, then a Lanos-class car and a ЗИЛ, each **abandoned** and **destroyed by strikes**
  - poles and wires, including leaning ones; a debris and rubble kit; a corrugated roof sheet
- A **UAT-1 gallery map** (`uat1_gallery`): a compact noclip map with a station per asset and a patch per terrain kind, each labelled when the camera is near. It runs within the frame budget.

## Capabilities

### New Capabilities
- `asset-pipeline`: Blender → glb export, catalog entries, budgets, LODs, CC0 sourcing and credits
- `terrain-holes`: holes in the heightfield for trenches and cellars, consistent across rendering, collision and world queries
- `uat1-gallery`: the reviewable gallery: its contents, labels, view quality (micro-detail fade, far density, distinct surfaces) and performance

### Modified Capabilities
_None._ The new `burnt_field` surface is data in `surfaces.json` under the existing map-format rules.

## Non-goals

- The full 4 km world and its authoring layout (`add-map-authoring`, `build-world-map`, for UAT-2).
- The analog camera look. It's the tech-artist's `add-analog-preview` change; the gallery gets its toggle when that lands.
- The drone model, the flight physics, the menus.
- The second vehicle pass beyond the four listed, and any second pass on other items after the pilot's feedback. Those follow the UAT-1 review.

## Impact

- New:
  - `blender/**` (.blend in LFS)
  - `tools/blender/` (export, budget checks)
  - `tools/assets/` (CC0 fetch)
  - `game/assets/{models,textures,materials}/**` (LFS)
  - `game/maps/uat1_gallery/`
  - `CREDITS.md`
- Changed:
  - `game/maps/surfaces.json` (+ `burnt_field`)
  - `game/assets/catalog.json` (new assets)
  - `game/src/world/` (holes, micro-detail fade and far layer)
  - the golden file re-recorded (new surface and holes → QueryVersion 3)
- Depends on `define-map-format` being merged. Implementation starts after it.
- **Repo size:** textures capped at 2K, meshes budgeted. Target under about 500 MB of LFS for UAT-1, reported per asset.
