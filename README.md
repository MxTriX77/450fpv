# 450fpv

A physics-first simulator of heavy fiber-optic cargo FPV drones, the kind Ukraine flies for combat logistics.
The vision and requirements are in [CLAUDE.md](CLAUDE.md).

## Layout

```
game/        Godot 4.7 (.NET) project: open game/project.godot
  src/       physics · telemetry · input · video · ui · world
  scenes/    drone · world · ui
  assets/    models · textures · materials · shaders · audio (exported, LFS)
  maps/      map data
  tests/
blender/     .blend sources (LFS): drone · structures · vehicles · vegetation · terrain
tools/       asset and data pipelines
openspec/    specs (the source of truth) and in-flight changes
docs/        team, workflow, decisions, roadmap, reports, wireframes
reference/   local-only flight footage (git-ignored)
.claude/     subagents, skills and OpenSpec commands
```

## Requirements

- Godot 4.7.2, .NET build (`winget install --id GodotEngine.GodotEngine.Mono --version 4.7.2`). Point the `GODOT` user environment variable at its `Godot_v4.7.2-stable_mono_win64_console.exe`. Scripts and agents call `$GODOT` and never hard-code a path.
- .NET SDK 8 or newer
- Blender 5.2
- Git with LFS
- Node.js, for the OpenSpec CLI (`npm i -g @fission-ai/openspec`)

## Build and run

From the repo root in PowerShell. In Git Bash, write `"$GODOT"` where these use `& $env:GODOT`.

```powershell
dotnet build game/Fpv450.sln                          # build the C# code; the first build restores NuGet packages
& $env:GODOT --headless --path game --import          # import assets: after cloning and after pulling new assets
& $env:GODOT --headless --path game --quit-after 120  # smoke run: exits 0 and prints no ERROR lines
& $env:GODOT --path game                              # open the review sandbox
& $env:GODOT --path game -- --scene res://scenes/sandbox/test_patch.tscn  # open a patch in the sandbox
```

Godot runs the last built assembly, so run `dotnet build` again after changing C# code.
A bad `--scene` path prints one `ERROR:` line and the sandbox starts with its ground placeholder.

Self-checks, which exit non-zero on failure:

```powershell
& $env:GODOT --headless --path game -- --selftest noclip  # noclip covers 6 m and 48 m in 1 s at 30 and 144 fps, ±5 %
& $env:GODOT --path game -- --selftest overlay            # opens a window for about 12 s: F3, F12 and the frame budget
& $env:GODOT --headless --path game -- --selftest worldquery  # world-query scenarios on sample_patch, about 1 min
```

Terrain-only frame budget on the synthetic 4 km map (D-009). `flypath` flies a fixed 12 s path and prints fps, 1 % low, draw calls and video memory:

```powershell
python tools/map/make_synthetic.py build/maps/synthetic_4km  # about 12 s, same bytes every run
& $env:GODOT --path game --resolution 1920x1080 -- --scene res://scenes/world/terrain_bench.tscn --package "$PWD/build/maps/synthetic_4km" --selftest flypath
```

### Sandbox controls

The noclip camera has no collision and flies through everything. Close the window to quit.

| Input | Action |
|---|---|
| Mouse | Look around while the mouse is captured |
| W / S | Forward / back along the view direction |
| A / D | Left / right |
| E or Space / Q or Ctrl | Up / down along world vertical |
| Shift (hold) | 8× speed |
| Mouse wheel | Base speed ×1.25 per step, 1 to 60 m/s (starts at 6 m/s) |
| Esc | Release or recapture the mouse |
| F3 | Performance overlay: fps, average frame time, 1 % low over 5 s, draw calls |
| F12 | Screenshot to `%APPDATA%\Godot\app_userdata\450fpv\screenshots\` (the path is printed) |

## How work happens

- [docs/workflow.md](docs/workflow.md) covers the spec → build → review → merge loop.
- [docs/team.md](docs/team.md) lists the roles and their git identities.
- [docs/decisions.md](docs/decisions.md) records architecture decisions.
- [docs/roadmap.md](docs/roadmap.md) tracks the milestones.
