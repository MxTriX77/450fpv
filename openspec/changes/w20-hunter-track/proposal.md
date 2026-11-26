# Proposal: w20-hunter-track

**Owner:** world-builder. **Also building:** asset-artist (the objects), reference-analyst (sizes from clips X and Y), game-developer (the Play card). **Reviewer:** qa-engineer. **Tier:** Complex (D-033).
**Motivated by:** D-123, the pilot's request of 2026-11-21; VISION.md §4 (map first, objects micro-detailed) and §5 (the pilot flies it and gives feedback). **Requirements:** REAL-05, AST-02, AST-03, AST-05, WLD-08, WLD-09, WLD-11, WLD-12, PLT-02, PLT-06, APP-02.

## Why

The pilot wants their own race track, ХАНТЕР-09, as a new map: "objects layout, scales, sizes, etc, and general idea of the map, and the track itself", and "we're not making a 100% copy". Their saved track file records every object's kind, position, rotation and scale, so the layout can be read exactly instead of measured from video.

## What Changes

The map arrives in three portions, each ending in a build the pilot can fly:

- **Portion 1, the layout.** A tool reads the pilot's track file into a neutral layout in the repository. All 272 distinct drawn objects (the file's 278 drawn rows hold 6 exact doubles) stand at the file's poses as simple shapes of the right size with real collision, on ground derived from the objects' own heights. The race flies through three houses that the file does not hold, so portion 1 also builds them: our own proper buildings with the village map's textures, at the track's measured dimensions (D-124). The map is the third card in Play.
- **Portion 2, the objects.** Our own models replace the simple shapes, kind by kind, starting with the concrete blocks (199 of the 272). Every frame opening is a gap the sim knows.
- **Portion 3, the ground.** Our own autumn field around the track: surfaces, grass and gentle relief outside the layout.
- **Kept as data, not built:** the race's order (101 passages through 56 checkpoints), so checkpoints can be added later.

## Non-goals

- No checkpoints, timing, laps or race logic.
- Nothing from the other game's install: no terrain, mesh, texture, sound or catalogue data. Its ground and its objects are replaced by our own (D-123).
- No brand marks, on models or in the repository. The source game's name and item ids are not committed.
- No change to the flight physics, so no new model number and no re-recorded golden. House and Village must fly byte-identically.
- Not a 100 % copy: where a size cannot be read from the file or the clips, it is a labelled estimate.

## Capabilities

### New Capabilities

- `hunter-track`: the ХАНТЕР-09 map: importing the pilot's layout, its ground, its objects and their physics metadata, the kept race order, and flying it from Play.

### Modified Capabilities

None. Play's list of maps gains an entry; no existing requirement changes.

## Impact

- **New:** `tools/map/import_track.py`, `maps/hunter-09/`, a track-kit generator and params under `assets-src/` and `tools/blender/`, the object sheet `docs/object-sheets/hunter-track.md`, and the map's scene.
- **Changed:** `PlayMaps.cs` and `FlightWorlds.cs` (the third map), the sim's package loader only as far as a third vendor list needs, and `tools/map/validate.py` if the new package needs it.
- **Branches:** work branches from the build branch (`unity/physics/physics-integration-2--w3-preview`), which holds the map package format and Play's maps; the trunk does not (D-122).
