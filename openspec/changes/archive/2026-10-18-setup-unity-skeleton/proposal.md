# Proposal: setup-unity-skeleton

**Owner:** game-developer · **Reviewers:** tech-artist (URP settings), qa-engineer
**Requirements:** PLT-01, PLT-02 (quality presets groundwork), PRD-07 (noclip stub) · **Decisions:** D-001, D-005, D-006, D-017, D-019 · **VISION:** platform

## Why
Every later phase builds inside one Unity project. It must exist, pinned to the newest Unity 6 LTS, with URP Forward+, the agreed folder layout, a scene that builds for Windows, and batchmode entry points the verification commands can call.

## What Changes
- The Unity project at `unity/`, pinned to 6000.3.26f1, with URP (Forward+), and the quality levels Low, Medium and High, each with its own URP asset.
- The folder layout `Assets/_Project/{Art,Prefabs,Scenes,Scripts/{App,Input,UI,Feed,World},Shaders,Settings,Editor,Tests}`, with one assembly definition per script folder, an Editor assembly and an EditMode test assembly.
- An empty `Boot` scene (camera, light, nothing else) as the only scene in the build, with a smoke-test switch so a built player proves it opened the scene.
- A noclip camera stub (mouse look, WASD, Shift fast, E/Q down and up, wheel for speed) on the Boot camera.
- `Fpv450.Build.Windows`: a batchmode build of the Windows x64 player.
- The sim package from `setup-sim-tools` compiles in Unity, and its tests run in the EditMode run.

## Capabilities
### New Capabilities
- `unity-project`: the Unity project's version, render pipeline, layout, scenes and batchmode entry points.

### Modified Capabilities
- none

## Non-goals
- No look work (lighting, sky, post): tech-artist, phase 1.
- No menus, input layer or TX12: phase 6.
- No IL2CPP: Mono is enough until profiling says otherwise.

## Impact
New `unity/` (ProjectSettings, Packages/manifest.json, Assets/_Project). Unblocks `setup-asset-pipeline` task 1.3 and `setup-review-capture`.
