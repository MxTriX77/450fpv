# Proposal: w11-world-map

**Owner:** game-developer · **With:** world-builder (the top-down image per map) · **Reviewers:** qa-engineer · **Requirements:** APP-04, PRD-07, PLT-02, PLT-04 · **VISION:** §2 · **Decisions:** D-101 (this change), D-089, D-100 · **Plan:** `docs/handover-2026-11-12.md` §3, unit C · **Tier:** Standard

## Why
The pilot, after flying the farmland preview (`docs/handover-2026-11-12.md` §2, item 4): "let's add M - it would open a map of the world and show where we are. it would show us in real time also. you jsut fly, and you can always toggle map, it won't pause your flight. the map should be not hover ALL of the screen - it should a little bit smaller than whole game window". D-101 records it.

## What Changes
- **M toggles a map panel** in flight and in Explore. The sim, the flight controller and the pilot's input keep running while it shows; nothing pauses.
- **The panel** is centred and a little smaller than the game window, so the flight shows around it. It is a clean UI overlay on the output, not seen through the analog feed.
- **It shows the whole map from above**, with the drone's position and heading drawn live every frame (in Explore, the noclip camera's).
- **The image of each map is generated from above** by a capture method with params per map (extent, resolution), with the world-to-image transform written beside it. Like D-100's stills, it is rendered at build time into a git-ignored folder and carried into the player, so no render of the friend's world is pushed (D-089).
- Both maps get one: Village (the farmland) and House (the test range, back in the player with w10-play-maps).

## Non-goals
- No route, fiber trail, waypoints, zoom or panning. The pilot asked for the map and where the drone is.
- No change to the sim, the feed or the OSD.
- No map on the radio's switches (M is a keyboard key, like R, C, X and V).

## Impact
- `Scripts/App` (`AppFlow`, `FlightScene`, `FarmlandExplore`), `Scripts/UI` (the map panel), `Scripts/World` and `tools/map/` or an editor capture for the image, `unity/Assets/_Project/Editor/Build.cs` (render at build time, carry into the player).
- The frame cost of one textured panel while it shows; none while hidden.
- The branch is `unity/physics/physics-integration-2--w11-map`, cut from `--w3-preview` at 241145ea. It merges into `--w3-preview` after w10-play-maps (they overlap in `AppFlow`).
