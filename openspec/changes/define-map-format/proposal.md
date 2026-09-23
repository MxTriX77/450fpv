# Proposal: define-map-format

**Owner:** world-artist · **Reviewers:** physics-engineer (surface parameters, query API), game-developer (sandbox hook), qa-engineer
**Motivation:** CLAUDE.md §4.1 says to create the map format and the map itself, and that every straw, stick and nook must react with the quad and its physics. §3 asks for a big (not humongous) map made of different terrains, at 60+ fps. §1–2 describe liftoff and landing that depend on what's underfoot: legs snagging, loose or porous soil, pitfalls, grass jitter.

## Why

Before any terrain is built, the team needs one agreed way to describe a map that serves both sides of the product. Artists need heights, surfaces, objects and cover they can author and review. Physics needs fast, deterministic answers to questions like "what is under this leg, how soft is it, which stems are touching it, and what is blocking the wind here?".
If the format is decided late, every patch built for M1 has to be rebuilt.

## What Changes

- A **map package** format under `game/maps/<id>/`:
  - a manifest
  - a heightfield
  - a surface-ID layer
  - a cover-density layer (grass, straw, twigs, litter)
  - placed objects, including wires as polylines

  Authored data stays engine-independent, and Godot builds the runtime scene from it at load time.
- A global **surface table** (`game/maps/surfaces.json`) with physical parameters in SI units for the 9 surfaces catalogued in `docs/reference-notes/terrain.md` §7. The physics engineer signs off the fields.
- An **asset catalog** (`game/assets/catalog.json`) that gives each placeable object its scene, collision and physical tags (snag hazard, wind porosity, fly-through gaps).
- **Deterministic micro-detail.** Individual stems, straws and twigs are never stored. They are generated from a hash of world position and map seed, the same for visuals and for physics, so a landing replayed from the physics log touches exactly the same straws.
- A **world query API** in C# (`game/src/world/`) that physics calls every sub-step: height, normal, surface, cover, micro-detail near a point, and wind-obstacle data.
- A **derived wind-obstacle layer**: obstacle height and porosity, generated from the terrain and objects at import. The physics engineer's future turbulence model reads it.
- A **validator** (`tools/map/validate_map.py`), and `-- --map <id>` loading in the review sandbox.
- A **sample map** `sample_patch` (256 m × 256 m) exercising every layer, for the user to fly in noclip.
- A terrain renderer **spike** (the Terrain3D addon vs our own chunked mesh) on a synthetic 4 km map, recorded as D-009.

## Capabilities

### New Capabilities
- `map-format`: the package layout, the layers and their encodings, the surface table, the asset catalog, versioning and validation
- `world-query`: the runtime API physics uses (height, surface, cover, micro-detail, wind obstacles), with its determinism and performance guarantees
- `map-loading`: building a map in the engine from a package, including the sandbox `--map` hook and the rendering budget

### Modified Capabilities
_None._

## Non-goals

- Building the main map or the M1 terrain patches from the build list. That is the next change, `build-m1-terrain-patches`.
- The flight model, contact physics or turbulence model. This change only provides the data and queries they will use.
- Final surface parameter values. These are typical starting values, and the physics engineer tunes them during flight testing.
- Streaming or open-world partitioning beyond what an 8 km map needs.

## Impact

- New: `game/maps/` (package + surface table), `game/assets/catalog.json`, `game/src/world/` (loader, query API, micro-detail generator), `tools/map/` (validator, obstacle-layer generator, synthetic map generator)
- Depends on `bootstrap-godot-project` (sandbox, build, noclip). Implementation starts after it merges.
- Input: `docs/reference-notes/terrain.md` §6–7 (on the `study-flight-references` branch). The format does not depend on the pilot's open terrain questions.
- A possible new third-party dependency (Terrain3D, MIT GDExtension), decided by the spike
