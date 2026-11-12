# 450fpv

> **Two codebases, one repo.** This is the **Unity** codebase (branch `unity/main`). The first, Godot attempt is frozen on [`main`](https://github.com/MxTriX77/450fpv/tree/main) (tag `godot-final`). See [docs/repository.md](docs/repository.md).

A lightweight, ultra-realistic **heavy fiber-optic cargo FPV simulator** in Unity:
- **One drone:** the 10-inch "Svinoriz 10 Opto", flown in acro on a RadioMaster TX12.
- **Two maps:** House (a test range with a car, a fence and houses) and Village (1 × 1 km of fields, tracks, tree belts and a ruined village on rolling hills).
- **An analog PAL goggles feed** with its OSD.
- **Physics that is physical, seeded and logged:** every flight can be replayed bit for bit and explained.

The manifesto is [VISION.md](VISION.md). Realism here means a copy of the pilot's references, and physics that is calculated, never tuned to look right.

## Where it stands (2026-11-13)

**Playable now** (physics model 85):
- **Play** opens a carousel of the two maps. Then comes the setup (the load's weight, legs on or off, time of day, wind, rain, start point), then the flight.
- **The flight** is seen through the analog feed with the OSD, or from a chase view. It ends in a summary of what happened, and it writes a flight log.
- **Explore** (review builds) flies a free camera through Village.

**In the physics today:**
- a 1 kHz fixed step on its own thread, engine-free and deterministic
- rotors, motors, ESCs and the battery from published component data, under a Betaflight-4.5-style flight controller with the pilot's rates
- wind with gusts, faster over the hills' crests and sheltered in their lee, and rain
- ground contact on the terrain: slopes, soft soil that takes a leg in, and legs that set, crack and break
- props that strike, chip and break, motors that heat and fail, and crashes that create no energy
- the fiber: paid out from the spool, laid over the ground, snagged and cut, with the link's failsafe
- tree crowns that cost the props their real work, and walls that stop the drone

**In work: the physics, proven against everything the pilot has said** (D-104). The [physics ledger](docs/reviews/physics-ledger.md) lists every statement of the pilot's about the drone's physics, every physics requirement and every open task, each with its status and the test that proves it. On 2026-11-13, of the pilot's 48 statements, 13 are proven by a test, 5 are built but untested, 21 are met in part, 2 are missing and 7 are contradicted by the sim. The contradicted ones come first:
- a landing on a slope tumbles from a contact bug
- a landing in high grass with straw cuts nothing and shakes nothing
- the fiber never tears at the canister's exit, and a sharp liftoff never cuts it
- no leg ever sticks alone at liftoff

**The trunk lags the game.** `unity/main` holds the documents and the code reviewed and merged so far, which is an older physics model. The newest playable state lives on an integration branch far ahead of it. [tools/build_latest.json](tools/build_latest.json) names that branch and says what the build contains, and one command builds it (below).

## Build and fly

**You need** Windows 11, Unity 6000.3.26f1 (with Unity Hub running, for the licence), the .NET 10 SDK, PowerShell 7 and Git LFS. Python 3.14 runs the tools' tests, and Blender 5.2 rebuilds the assets. The pinned versions and the once-per-clone setup are in [docs/environment.md](docs/environment.md).

**One command builds the newest playable state:**

```powershell
pwsh -File tools/build_latest.ps1
```

- It reads `tools/build_latest.json` from `origin/unity/main`, checks the named branch out in its own worktree, builds it, runs the game's smoke test and puts the result in `unity\Builds\Latest\`. Run `450fpv.exe` there.
- **Village needs asset packs that are not in this repository.** Its trees, grass and ruins come from third-party packs whose licences don't allow redistribution (D-089). They live in a local store on the dev machine and are linked into each checkout. Without them the build stops and says so.

## Controls

The drone is flown on the radio: calibrate it once from the menu. A keyboard fallback exists for development.

| Key | In flight |
|---|---|
| **V** | first-person feed or chase view |
| **M** | the map of the whole world, with the drone's position live; nothing pauses |
| **R** | restart the flight |
| **C** | the feed's noise, on or off |
| **X** | the stick overlay on the OSD, on or off |
| **Esc** | pause |
| **F** | the analog feed, off or on (review builds) |

Explore's free camera: mouse look, **WASD**, **Shift** to go faster, **E** or **Space** up, **Q** or **Ctrl** down, and the mouse wheel for speed.

## How the physics is kept honest

- **The sim core is engine-free.** It lives in `unity/Packages/com.fpv450.sim` with no Unity references, and the .NET tools compile the same sources. So every test, bench and replay runs without Unity.
- **It is deterministic.** Only IEEE basic operations, seeded named random streams with every draw logged, and no allocation in steady state. Randomness is only a bounded change of a named physical parameter, never noise on the pose.
- **Every flight writes a log,** and `tools/replay` replays it outside Unity, record by record, bit for bit.
- **Recorded reference flights** ("goldens") pin each physics model. Any re-record is audited case by case.
- **Every number has a label:** measured, published, derived or assumed.

```powershell
dotnet test tools/simtests -c Release                              # the sim core's tests
dotnet test tools/inputtests -c Release                            # the radio and the flight controller's inputs
dotnet run -c Release --project tools/flightbench -- --golden     # the reference flights, bit for bit
dotnet run -c Release --project tools/worldbench -- --golden      # the world queries
dotnet run -c Release --project tools/replay -- <flight log>      # replay one flight
python -m pytest tools                                             # assets, map tools, hygiene
```

## What's in the repository

| Folder | What it holds |
|---|---|
| `unity/` | The Unity project (URP). The game's own code and scenes are under `Assets/_Project/` |
| `unity/Packages/com.fpv450.sim/` | The sim core: flight, contact, wind, the fiber, the world queries, the log |
| `tools/` | The headless tools on the same sources (`simtests`, `inputtests`, `flightbench`, `worldbench`, `replay`), the map compiler and validator, the Blender asset generators, the asset manifest and the OPSEC scan |
| `maps/` | The map packages |
| `assets-src/` | The parameters everything is built from: the drone's spec (`drone/drone.json`), the surfaces, the physics materials, the asset generators' params |
| `openspec/` | The spec and task list of every change |
| `docs/` | Requirements, plan, decisions, reviews, reference notes and handovers |

## Documents

| File | What it is |
|---|---|
| [VISION.md](VISION.md) | The pilot's manifesto, verbatim |
| [docs/reviews/physics-ledger.md](docs/reviews/physics-ledger.md) | **Everything asked of the physics,** with each item's status, its test and the ranked gaps |
| [docs/requirements.md](docs/requirements.md) | The requirements, with stable IDs and acceptance tests |
| [docs/plan.md](docs/plan.md), [docs/final-world-plan.md](docs/final-world-plan.md) | The phases and gates, and the plan for the final world |
| [docs/decisions.md](docs/decisions.md) | The decision log |
| [docs/pilot-questions.md](docs/pilot-questions.md) | What the pilot has answered (never asked again) and what is still open |
| [docs/reference-notes/](docs/reference-notes/), [docs/object-sheets/](docs/object-sheets/) | The measured, OPSEC-clean reading of the references, and the sheets every asset is built from |
| [docs/knowledge.md](docs/knowledge.md) | Everything learned in the first attempt |
| [docs/environment.md](docs/environment.md) | The dev machine, the pinned tools and the per-clone setup |
| [docs/repository.md](docs/repository.md) | One repo, two codebases: how the branches are laid out |
| [docs/team.md](docs/team.md), [docs/workflow.md](docs/workflow.md), [docs/tooling.md](docs/tooling.md) | The roles, the spec-driven loop and the tools the team works with |
| [CLAUDE.md](CLAUDE.md), [AGENTS.md](AGENTS.md) | The orchestrator's operating rules and the rules for every agent |
| `docs/handover-*.md` | The state at the end of each working session |
| [CREDITS.md](CREDITS.md) | Every third-party file in the repository, with its licence |

## Assets and licences

- **Third-party files in this repository are free:** CC0, or CC-BY with attribution. Each one is pinned by its SHA-256 in `tools/assets/manifest.json`, and [CREDITS.md](CREDITS.md) is generated from it.
- **Nothing whose licence forbids redistribution is pushed,** and nothing that copies its geometry (D-089).
- **Nothing comes from the pilot's references.** They stay on the dev machine, git-ignored, and are cited by letter only.
- **Our own assets are rebuilt, never hand-edited:** a generator, its params and one command.

## How the work is organised

The pilot sets the direction and reviews only tangible builds at the gates (G1 to G8 in [docs/plan.md](docs/plan.md)). An orchestrator plans each piece of work as a spec with a task list and hands it to seven roles: reference analyst, asset artist, world builder, tech artist, physics engineer, game developer and QA engineer. Every change is reviewed by QA against its spec before it merges.
