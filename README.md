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

## How work happens

- [docs/workflow.md](docs/workflow.md) covers the spec → build → review → merge loop.
- [docs/team.md](docs/team.md) lists the roles and their git identities.
- [docs/decisions.md](docs/decisions.md) records architecture decisions.
- [docs/roadmap.md](docs/roadmap.md) tracks the milestones.
