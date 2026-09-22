# Team

The orchestrator is the main Claude session. Every other role is a subagent defined in `.claude/agents/`.
Each role commits under its own git identity, which you pass on each commit with `git -c` and never put in global config.

| Role | Agent | Git author | Branch prefix | Owns |
|---|---|---|---|---|
| Orchestrator | main session | `Orchestrator <orchestrator@450fpv.local>` | `chore/` | planning, OpenSpec changes, merges, reports, `docs/` |
| Physics Engineer | `physics-engineer` | `Physics Engineer <physics@450fpv.local>` | `physics/` | `game/src/physics/`, `game/src/telemetry/` |
| World Artist | `world-artist` | `World Artist <world@450fpv.local>` | `world/` | `blender/`, `game/assets/{models,textures,materials}/`, `game/maps/`, `game/scenes/world/`, `game/src/world/`, `tools/blender/` |
| Tech Artist | `tech-artist` | `Tech Artist <techart@450fpv.local>` | `techart/` | `game/src/video/`, `game/assets/shaders/`, lighting, sky, time of day, rendering performance |
| Game Developer | `game-developer` | `Game Developer <gamedev@450fpv.local>` | `game/` | `game/src/ui/`, `game/src/input/`, `game/scenes/ui/`, `game/scenes/drone/`, app flow, `docs/wireframes/` |
| QA Engineer | `qa-engineer` | `QA Engineer <qa@450fpv.local>` | `qa/` | `game/tests/`, reviews, perf benchmarks, physics-log forensics |

Commit command pattern:

```bash
git -c user.name="Physics Engineer" -c user.email="physics@450fpv.local" commit -m "feat(physics): add motor lag model"
```
