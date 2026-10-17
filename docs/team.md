# Team

The **orchestrator** is the main Claude session. Every other role is a subagent defined in `.claude/agents/<role>.md`.

**Roles are agent types, not long-lived workers** (D-029). Each logical task (one asset, one object sheet group, one shader stage, one query, one mechanism, one screen, one review) gets its **own fresh subagent** of the owning role. It does that task, commits, hands off and ends.

**Git identity:** each role commits under its own identity, passed on each commit with `git -c` and never set in the global config.

| Role | Agent | Git author | Branch prefix (under `unity/`) | Owns |
|---|---|---|---|---|
| Orchestrator | main session | `Orchestrator <orchestrator@450fpv.local>` | `unity/chore/` | planning, OpenSpec changes, merges, reports, `docs/` (except the role-owned doc paths below) |
| Reference Analyst | `reference-analyst` | `Reference Analyst <reference@450fpv.local>` | `unity/ref/` | footage and photo analysis, online research, `docs/object-sheets/`, `docs/reference-notes/`, `tools/reference/` |
| Asset Artist | `asset-artist` | `Asset Artist <assets@450fpv.local>` | `unity/art/` | Blender generators and sources, textures and materials, LODs and impostors, per-asset physics metadata, the drone model; `assets-src/`, `blender/`, `tools/blender/`, `tools/assets/`, `unity/Assets/_Project/Art/`, `unity/Assets/_Project/Prefabs/` |
| World Builder | `world-builder` | `World Builder <world@450fpv.local>` | `unity/world/` | map format, layout and compiler, validator, gallery and world maps, Unity terrain, vegetation and instancing, micro-detail rendering, the world-query core (with physics review); `maps/`, `tools/map/`, `unity/Assets/_Project/Scripts/World/`, `unity/Packages/com.fpv450.sim/Runtime/World/` |
| Tech Artist | `tech-artist` | `Tech Artist <techart@450fpv.local>` | `unity/techart/` | URP setup, lighting, sky, time of day, weather look, post-processing, rendering performance, the analog feed and the OSD rendering; `unity/Assets/_Project/Scripts/Feed/`, `unity/Assets/_Project/Shaders/`, `unity/Assets/_Project/Settings/` |
| Physics Engineer | `physics-engineer` | `Physics Engineer <physics@450fpv.local>` | `unity/physics/` | the flight model, contacts, wind and turbulence, tether, wires, randomness, logging and replay, `drone.json` mass build, sign-off of the world-query API; `unity/Packages/com.fpv450.sim/Runtime/{Flight,Contact,Wind,Tether,Log}/`, `tools/flightbench/`, `tools/replay/` |
| Game Developer | `game-developer` | `Game Developer <gamedev@450fpv.local>` | `unity/game/` | Unity skeleton, app flow, menus, setup, wireframes, TX12 input and calibration, noclip and review controls, OSD content model, the drone scene wiring, builds; `unity/Assets/_Project/Scripts/{App,Input,UI}/`, `unity/Assets/_Project/Scenes/`, `docs/wireframes/` |
| QA Engineer | `qa-engineer` | `QA Engineer <qa@450fpv.local>` | `unity/qa/` | reviews of every branch, tests, benchmarks (PLT-07), screenshot reviews, golden audits, determinism checks, crash forensics; `unity/Assets/_Project/Tests/`, `tests/`, review files in each change |

Commit command pattern:

```bash
git -c user.name="Asset Artist" -c user.email="assets@450fpv.local" commit -m "art(house): add adobe house generator"
```

## Who reviews what

| Work | Reviewed by, before QA |
|---|---|
| Object sheets | asset-artist (are they buildable?) |
| Catalog physics metadata | physics-engineer |
| World-query API | physics-engineer (contact probe) |
| Feed signals interface | physics-engineer |
| OSD content model | tech-artist |
| `drone.json` | asset-artist and physics-engineer together |

QA reviews everything before merge.
