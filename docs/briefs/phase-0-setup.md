# Phase 0 brief: setup

## Task
Turn `C:\.dev\450fpv` into the Unity restart of 450fpv on the orphan branch `unity/main` of `MxTriX77/450fpv`, with working pipelines, so phase 1 agents only make assets. This is implementation, not a planning round. One session.

## Settled: do not re-open
- **Engine:** Unity 6 LTS, URP Forward+, C# (D-001). Pin the newest 6000.x LTS available today.
- **Repo:** the existing repo, with every Unity branch under the `unity/` namespace: the orphan trunk `unity/main`, and feature branches `unity/<prefix>/<change-id>`. `main` and the old branches stay as the frozen Godot attempt (D-002, D-025).
- **Assets:** free only. CC0 preferred, CC-BY with attribution. No Asset Store or Fab items, even free ones (D-003).
- **Editability:** generators plus params, never hand-edited exports (D-004).
- **Sim core:** engine-free, as a local package with `noEngineReferences: true`, plus .NET tools compiling the same sources (D-006).
- **Frames:** +X east, +Y up, +Z north, Unity's frame (D-019).
- **Commits:** subject line only, no co-author, role identities (AGENTS.md).

## Already done: do not redo
- **This doc pack:** VISION, CLAUDE, AGENTS, requirements, plan, decisions, asset list, knowledge, the copied reference notes and the legacy designs.
- **`.gitignore`** already excludes `reference/`, and **`.gitattributes`** sets the LFS patterns. Both must be in the **first** commit.
- **The pilot fills `reference/` themselves** (copied from the old checkout). Agents never copy it.

## Read before starting, in this order
1. `CLAUDE.md`, `docs/plan.md` (phases 0–1), `docs/decisions.md`.
2. `docs/requirements.md` §2 (PLT), §3 (PRC), §4 (SEC) and §5 (AST-03, AST-05, AST-12).
3. `docs/knowledge.md` §13 (what the first attempt's tools did) and §14 (lessons).

## Flow
1. **Git:**
   - `git init`, `git remote add origin https://github.com/MxTriX77/450fpv.git`, `git fetch origin`
   - `git switch --orphan unity/main`
   - commit `.gitignore` and `.gitattributes` first, as `Orchestrator`
   - then the doc pack, in a few logical commits; the README's first line links back to the Godot codebase ([docs/repository.md](../repository.md))
   - in the Unity folder, optionally limit fetching to `unity/*` (repository.md)
   - **Ask the pilot before:** pushing `-u origin unity/main`, creating and pushing the tag `godot-final` at `b684cce`, changing the GitHub default branch to `unity/main`, (optional) a GitHub ruleset on `unity/main` that requires PRs and blocks force-pushes and deletion, and **the one banner commit on Godot's `main` README** linking to `tree/unity/main` (D-027), so it is easy to switch between the codebases on GitHub. Each one is an outward-facing change.
2. **Environment:** check and install Unity 6 LTS (via Unity Hub, with Windows build support), Blender (latest stable), Python 3.12+, the .NET SDK, git-lfs, ffmpeg and `gh`. Write `docs/environment.md` with exact versions, the `UNITY` path, and the Godot-era tools that are no longer needed.
3. **Unity skeleton** (game-developer):
   - `unity/` with URP Forward+ and the Low, Medium and High quality presets
   - `Assets/_Project/{Art,Prefabs,Scenes,Scripts/{App,Input,UI,Feed,World},Shaders,Settings,Editor,Tests}`
   - the local package `Packages/com.fpv450.sim/{Runtime,Tests}` with its asmdef set to `noEngineReferences: true`
   - an empty `Boot` scene; a noclip camera stub
   - a `Fpv450.Build.Windows` batchmode build method
4. **.NET tools:** `tools/simtests` (xunit or nunit), `tools/worldbench` and `tools/flightbench`, each compiling `unity/Packages/com.fpv450.sim/Runtime/**/*.cs`. `dotnet test` passes on an empty `DetMath` test.
5. **OpenSpec and the skills** ([docs/tooling.md](../tooling.md)):
   - check `openspec --version` (1.13.1+; install with `npm i -g @fission-ai/openspec`)
   - **keep the prepared `openspec/config.yaml`** (run `openspec init` only if the CLI demands it, and never let it overwrite the config)
   - check that Claude Code lists the copied skills (`karpathy-guidelines` and the six `openspec-*`) and the `/opsx:*` commands
   - commit `openspec/`, `.claude/skills/` and `.claude/commands/`
6. **Asset pipeline** (asset-artist):
   - `tools/assets/fetch.py`: Poly Haven and ambientCG by id, resolution and SHA-256, with `--verify`, and generating `CREDITS.md`
   - `tools/blender/build.py`: headless, params JSON → FBX plus a meta JSON (collision primitives, material ids, gaps as polygons, wind volumes, snag flags, LOD distances)
   - `unity/Assets/_Project/Editor/CatalogImporter.cs`: catalog plus meta → prefab, with LOD group, materials and physics-metadata components
   - `tools/build_asset.ps1 <ID>`: the end-to-end command
7. **Review tooling:**
   - `Fpv450.Review.Capture`: the AST-12 sheet with shots at 1.5 / 10 / 50 / 200 m, a turntable and a collision overlay, in day and overcast
   - `tools/reference/extract_frames.py` and `contact_sheet.py`, writing only to `reference/_frames/` (ignored)
8. **Hygiene:**
   - a pre-commit hook (SEC-06) that blocks media files outside `assets-src/` and `unity/Assets/`, footage extensions, messenger names, and coordinate-like strings in `docs/`
   - check the GitHub LFS quota; record it in `docs/environment.md`
9. **Pilot-facing build:** none in phase 0.

## Checks: run them, and paste the output into the handoff
- `dotnet test tools/simtests` passes.
- Unity batchmode EditMode tests pass (an empty suite is fine). The Windows build succeeds and opens the Boot scene.
- `pwsh tools/build_asset.ps1 K01` (one brick from the debris kit) produces an FBX, a prefab with collider and material, and a review sheet.
- `python tools/assets/fetch.py --verify` passes.
- The pre-commit hook blocks a planted test clip (`.mp4`).
- `openspec validate --strict` passes.

## Done means
- Everything above is green.
- `docs/environment.md` is written.
- `CLAUDE.md`'s verification block matches reality (fix any command that changed).
- The orchestrator reports to the pilot in 3 lines and opens phase 1 with [phase-1-assets.md](phase-1-assets.md).

## Write scope
Everything in the repo except `VISION.md` (the pilot's) and `reference/` (never).

## Commit
`type(scope): summary`: lower case, no body, no trailers, no attribution, one commit per coherent step, under the role's identity.
