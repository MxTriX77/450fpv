# Design: w20-hunter-track

## Context

See proposal.md for why. What the pilot's track file holds, surveyed read-only on 2026-11-21 (all **measured** from the file unless labelled):

| Fact | Value |
|---|---|
| Objects | 334 rows of 30 kinds: 278 drawn, 56 data only (checkpoint volumes, spawn points, one player position, one repair trigger) |
| Doubled rows | 7 rows are byte-identical copies (5 concrete blocks, 1 pylon, 1 spawn point), leaving 327 distinct objects, 272 of them drawn. The race names none of the copies |
| Extent of the placed objects | x 400–562 m, z 396–545 m, about 162 × 149 m |
| Rotations | 66 of 329 posed objects need more than a yaw; ribbons hold their points separately |
| Scale | only on 39 objects, mostly the flexible checkpoint volumes |
| Race | 1 lap, 101 passages, 56 checkpoints, one linear path, 1,132 m between checkpoint positions in straight lines |
| Ground at standing objects | 47 samples at 19.91–20.27 m; best plane slope 0.013°, residual 60 mm SD, 295 mm at worst |
| Concrete blocks | 204 rows, 199 distinct; stacked in steps of about 0.76 m (**derived** from steps of 0.8, 1.5, 2.3, 3.0, 3.8, 4.6, 5.3 and 6.1 m); neighbours most often 0.8 or 1.5 m apart (**derived**, to be confirmed); the highest at 8.8 m above the ground |
| Low objects | 11 lie below 19.8 m, 6 of them frames at 15.7–19.2 m; 9 of the 11 have no standing object within 12 m |

The repository has two placement formats. The test range's holds a yaw only. The farmland's map package (`fpv450-map-package`) holds a full quaternion and a scale per axis, on height tiles, so this map uses the package. Both live on the build branch, not on the trunk.

## Goals / Non-Goals

**Goals:** a flyable map after each portion; the file's poses unchanged; no change to the flight physics.

**Non-Goals:** see the proposal. In addition, no new generic track editor: the import serves this one file.

## Decisions

**1. Keep the file's coordinates.** The map rectangle is [0, 1000]² m like the farmland's, with the file's x, y and z unchanged. The layout already sits near its centre. *Alternative:* recentre at the origin (D-019), rejected because the farmland set the precedent and an unchanged frame makes the import auditable.

**2. The ground follows the objects, the objects do not move.** The terrain is a plane at the samples' fitted height plus a smooth local correction through the 32 distinct upright foot samples. *Alternative:* snap each object to a flat plane, rejected because stacks would have to move together and the poses would no longer be the file's.

**3. The 11 low objects are settled from clips X and Y, not guessed.** The file cannot say whether they stand in a dip or whether their origin is not at their foot. Task 1.1 decides per kind: an origin offset in the kit table, or extra height samples that make a dip in our ground. Either fits decision 2 and neither changes the tasks.

**4. Neutral kinds, matched without brand strings.** The import's kind table maps the file's ids by size and shape fragments to neutral kinds (`concrete_block`, `pylon_3m`, `frame_8x10`, and so on). The file's path is a command-line argument, so neither the path nor the source game's name is committed.

**5. One track-kit generator.** All kinds come from one parametric Blender generator with a params row per kind (AST-03), so portion 1's simple shapes and portion 2's models share ids, placements and physics metadata. Replacing a shape does not touch the layout. *Physics metadata per kind:* box or capsule collision following the visual, a surface from `physics_materials.json` (concrete, rubber, plastic, steel, fabric), gaps for gate openings, snag flags for ribbons and flags. Wind volumes for solid walls and stacks use the existing wake rules. Final parameters are recorded in `docs/object-sheets/hunter-track.md`, each labelled measured, derived or assumed.

**6. Sizes.** A name in the file gives some sizes directly (a 3 m pylon, a 2 × 4 m volume, a 300 × 220 cm gate). The frames' names give numbers without a unit, so clips X and Y settle them. Everything else is derived from spacing in the file or from the clips, and labelled.

**7. Branches.** `unity/world/w20-hunter-track` from the build branch. Finished portions merge there, and the trunk's build pointer is updated (D-122). The planning files land on the trunk.

**8. Doubled rows stand once.** The layout stays a faithful record of the file, all 334 rows. The map package places each byte-identical row once, because two coincident copies would double a collision shape and flicker on screen. *Alternative:* drop them in the import, rejected because the layout could then no longer be checked row by row against the file.

## Risks / Trade-offs

- [The 11 low objects turn out to need a real dip] → Height samples from the clips, marked as estimates; the pilot checks them on the first flight.
- [Sizes estimated from clips are off by a few percent] → The pilot asked for no 100 % copy; every estimate is one params row to change.
- [Stacked blocks add many collision shapes] → 199 boxes are well below the map's existing object counts; the frame-rate and physics-step budgets are measured in portion 1.
- [The third vendor list touches the sim's loader] → House and Village goldens must pass unchanged before merge.
- [Cyrillic on the Play card] → If the menu font lacks those glyphs, task 1.5 adds them; the card keeps the pilot's spelling.

## Open Questions

- Whether the race's direction markers (arrows, flags) should stay visible once the pilot has flown it. This is a preference to ask at the first flight, and it does not change the tasks.
