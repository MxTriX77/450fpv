# Proposal: bootstrap-godot-project

**Owner:** game-developer · **Reviewer:** qa-engineer
**Motivation:** CLAUDE.md §3 sets the target of 60+ fps and "ultra smooth" on the dev machine. §5.1 wants the user to see terrain parts, objects and textures during M1. §5.2 wants noclip exploration with mouse + WASD and Shift for much faster movement.

## Why

`game/project.godot` is still a hand-written placeholder that has never been opened. There is no C# solution, no scene, and no way to build, run or measure anything. Every later change (map format, terrain patches, physics, the video feed) needs a project that builds and runs headless for agents and QA. The user also needs a way to look at work in the engine.
Pulling the noclip camera forward from M2 means that same tool becomes how the user reviews each M1 terrain patch.

## What Changes

- The Godot 4.7.2 .NET project opens and builds. The C# solution (`Fpv450`) is generated and committed. `dotnet build` and a headless Godot run both succeed.
- A **review sandbox** main scene: sky, sun and ground placeholder. It loads any scene passed on the command line, so artists can drop a patch in and have the user fly it.
- **Noclip explorer:** mouse look, WASD, vertical movement, Shift for a much faster speed, scroll-wheel base speed, and Esc to release the mouse.
- **Performance overlay** (F3): fps, frame time, 1 % low and draw calls. The 60 fps budget is visible from day one.
- **Screenshot key** (F12) for review reports.
- D-001 (Godot .NET / C#) moves from Proposed to Accepted.

## Capabilities

### New Capabilities
- `review-sandbox`: the project builds and runs headless, loads a given scene for review, and shows a performance overlay and screenshot capture
- `noclip-explorer`: free-flying review camera with defined speeds and controls

### Modified Capabilities
_None._

## Non-goals

- The main menu (Train / Calibrate / Exit). It needs wireframes approved by the user first.
- RadioMaster input and calibration, the drone, flight physics, the analog video feed, the map format.
- A test framework choice beyond the headless smoke run. QA picks that when the physics work needs it.

## Impact

- New: `game/Fpv450.csproj`, `game/Fpv450.sln`, `game/scenes/sandbox/`, `game/src/ui/`, `game/src/input/` (camera controls)
- `game/project.godot` becomes engine-generated (main scene, input map, display settings)
- Machine prerequisite: Godot 4.7.2 .NET must be (re)installed. It's missing even though a PATH entry still points to it.
- Dev machine for performance numbers: Ryzen 7 7735HS, RTX 4050 Laptop (6 GB), 16 GB RAM
