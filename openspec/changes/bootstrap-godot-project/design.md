# Design

## Context

- `game/project.godot` is a placeholder written by hand (Forward+, Jolt, assembly `Fpv450`). It has never been opened, and there's no `.csproj` or scene yet.
- Godot 4.7.2 .NET must be reinstalled: the PATH entry points to a WinGet folder that no longer exists. The .NET 10 SDK is installed. Godot 4.7's `Godot.NET.Sdk` targets net8.0, which SDK 10 builds after restoring reference packs from NuGet.
- The dev machine is a Ryzen 7 7735HS with an RTX 4050 Laptop GPU (6 GB) and 16 GB RAM.

## Goals / Non-Goals

**Goals:**
- One command each for build, headless smoke run and "open this patch in the sandbox". Agents and QA script these.
- Keep it small: a sandbox scene, a camera script, an overlay script. Nothing else.

**Non-Goals:**
- Menus, input devices other than keyboard and mouse, the scene streaming or world architecture (that belongs to map-format).

## Decisions

- **Let Godot generate the project.** Godot 4.7.2's `--editor --quit` and `--build-solutions` never create a *missing* csproj, and the build step just skips it. Instead the solution came from Godot's own generator (the code behind the editor's "Create C# solution"), invoked once from a throwaway tool. The output is `Godot.NET.Sdk/4.7.2`, net8.0, and `--build-solutions` accepts it unchanged. We still don't hand-write the csproj, because its SDK version must match the engine.
- **C# everywhere, including the sandbox glue.** D-001 allows GDScript for glue, but one language means one toolchain and one debugger, and it keeps `dotnet build` as the single check. We'll revisit only if GDScript clearly helps UI iteration later.
- **The `GODOT` environment variable** points to the console executable (`Godot_v4.7.2-stable_mono_win64_console.exe`), which gives agents readable stdout. The README documents it. Nothing hard-codes a machine path.
- **Scene argument** comes from `OS.GetCmdlineUserArgs()` (`-- --scene res://…`). Loading happens in the sandbox's `_Ready`. On failure it prints one error and falls back.
- **Noclip:** a `Camera3D` with a C# script. Yaw and pitch are kept as separate floats (no accumulated basis drift). Movement uses `delta` in `_Process`, so it's independent of the frame rate. Speeds follow the spec. It doesn't need a physics body, so there's no collision.
- **Overlay:** a `CanvasLayer` with a `Label`. It reads `Performance` monitors (FPS, frame time, draw calls) and keeps a 5 s ring buffer of frame times for the 1 % low.
- **Input map:** actions are defined in `project.godot` (`move_forward`, …, `toggle_overlay`, `screenshot`), not as raw keycodes in scripts, so the later calibration and options work can rebind them.
- **Test patch:** `scenes/sandbox/test_patch.tscn` is a few primitive meshes with known sizes (a 1 m cube, a 2 m door frame, a 10 m wall). It's the fixture for the load scenario and a quick scale check for the user.
- **Movement verification:** a headless test mode (`-- --selftest noclip`) injects input actions for 1.0 s of simulated time, at a fixed 30 fps and at 144 fps, prints the distance travelled, and exits non-zero if it's outside ±5 %.

## Risks / Trade-offs

- [The WinGet reinstall needs the user's permission (it's a download) and may prompt for elevation] → The orchestrator asks the user first. Task 1 blocks the rest.
- [First NuGet restore needs internet] → Expected once. After that it's cached.
- [The headless editor import can print warnings about missing assets] → The smoke run only fails on `ERROR` or `SCRIPT ERROR` lines.
- [The 144 fps baseline figure depends on laptop power mode] → QA measures on mains power in the default Windows power plan and records the mode.
