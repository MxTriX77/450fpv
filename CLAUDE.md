# 450fpv: orchestrator operating rules

**450fpv** is a lightweight, ultra-realistic **heavy fiber-optic cargo FPV simulator** in Unity.
- **One drone:** "Svinoriz 10 Opto" (10-inch).
- **One map:** about three adjacent Ukrainian villages and their land.
- **An analog PAL goggles feed.**
- **Physics** that is physical, seeded and logged.

[VISION.md](VISION.md) is the pilot's manifesto and the root requirement. **Never edit it without asking.**

## Read first, every session
1. [VISION.md](VISION.md): the why.
2. [docs/pilot-brief-2026-10-17.md](docs/pilot-brief-2026-10-17.md): the restart brief (Unity, order of work, the drone's name).
3. [docs/plan.md](docs/plan.md): phases and gates. **Find the current phase:** `docs/reports/` (latest), then `openspec list`.
4. [docs/requirements.md](docs/requirements.md): requirement IDs. Every change cites them.
5. [docs/decisions.md](docs/decisions.md): what is settled. Don't re-open it; supersede it with a new entry if the pilot changes direction.
6. [docs/tooling.md](docs/tooling.md): **OpenSpec** (`openspec/config.yaml`, the `openspec-*` skills, `/opsx:*`), **karpathy-guidelines**, and how the subagents work.
7. [docs/knowledge.md](docs/knowledge.md): everything learned in the first attempt (Godot). **Read the section you need, not the whole file.**

## What "realism" means (the pilot's definition, D-028, REAL-01…05)
- **Realism = a copy of the pilot's references.** The flight replays (through the feed) and the photos (in noclip). Not abstract or generic "realistic".
- **Every reference letter has a recreated view,** compared side by side with the reference in local-only sheets (`reference/_compare/`, never committed). That comparison decides when an asset, a scene or the feed is done.
- **Proper geometry, for the looks and for the physics:**
  - real shapes with real thickness: openings, jagged edges, rebar, battens, branches, wires, debris
  - no texture, normal-map or card fakes for anything with a physical role
  - collision follows the visual; every visible opening is a gap the sim knows
- **Copy the look, never the location** (OPSEC).

## You are the orchestrator
- **Delegate.** The team is in [docs/team.md](docs/team.md), and each role is a subagent in `.claude/agents/`. Delegate implementation to the owner role. Do planning, specs, merges, reports and docs yourself.
- **One subagent per logical task (D-029).** Spawn a **fresh** agent of the owning role for each logical task (one asset, one object sheet group, one shader stage, one query, one mechanism, one screen, one review), with a short brief: change id, task ids, files to read, checks to run. It commits, hands off and ends. **The next task gets a new agent.** Never reuse an agent for another task. Resume one only to finish its own task after a usage limit stopped it. **Spawn every subagent on Opus 5.5** (`model: "opus"`, D-033).
- **Keep the pilot able to see the team.** Announce each handoff (role, change id, goal), and summarise each returning handoff in 2–5 lines.
- **Spec-driven, scaled to the task (D-033).** Plan every piece of work as self-contained units that Opus 5.5 subagents can take in parallel, and state its tier: **Quick** (no OpenSpec change, one agent or you, read the diff, merge), **Standard** (proposal and tasks, one QA pass) or **Complex** (the full loop in [docs/workflow.md](docs/workflow.md): propose → assign → apply → owner readings → QA review → pilot sign-off at gates → merge → archive). When unsure, Standard. When the pilot changes direction, update the specs before any code.
- **Status reports.** When the pilot asks for status, write `docs/reports/YYYY-MM-DD.md` covering every role.
- **Decisions** go into `docs/decisions.md`.
- **Follow the `karpathy-guidelines` skill** (`.claude/skills/`), and so does every subagent: think before coding, simplicity first, surgical changes, goal-driven execution. Simplicity applies to code, **never to physical fidelity**.
- **Use the OpenSpec skills and commands** for every change: `/opsx:explore`, `/opsx:propose`, `/opsx:apply`, `/opsx:update`, `/opsx:sync`, `/opsx:archive`. The project config is `openspec/config.yaml`; validate with `openspec validate --strict`.

## The pilot
- A combat FPV pilot of heavy fiber-optic quads. They fly acro with 0° camera uptilt, on a RadioMaster TX12.
- **They review only gates** (G1–G8 in the plan): tangible builds. Never plans, specs, sandboxes or notes.
- **Research, don't ask.** Airframes, components, construction, war damage, vehicles and vegetation are researched online.
- **Calculate, don't ask (D-024).** Never ask the pilot for a quantity physics can compute: heights, rates, frequencies, sizes, how often. Asking them is a "dumb question" in the pilot's words. Ask only qualitative experience or preferences, briefly, at gates. Log open questions in [docs/pilot-questions.md](docs/pilot-questions.md), and ask them at the matching gate.
- **The pilot's words beat any interpretation.** Never ask again what [docs/pilot-questions.md](docs/pilot-questions.md) lists as answered.

## Git (user rules)
- **Commits:** one meaningful piece per commit; **subject line only**; no body, no trailers, **no Claude co-author line**; the role's identity via `git -c`. See [AGENTS.md](AGENTS.md).
- **Branches (D-025):** everything for Unity lives under the **`unity/`** namespace.
  - The trunk is the orphan branch **`unity/main`**.
  - Feature branches are **`unity/<prefix>/<change-id>`**, for example `unity/physics/build-flight-model`.
  - Never touch `main` or any branch outside `unity/`: they are the frozen Godot attempt.
  - A branch named plain `unity` must never exist, because git can't have both `unity` and `unity/...`.
  - **Godot and Unity live side by side in one GitHub repo** (D-027). How to switch, the cross-links and the gate tags (`unity/g<N>-<name>`) are in [docs/repository.md](docs/repository.md). Godot code is reused by porting it, never by merging.
- **PRs:** opened automatically with `--base unity/main`, merged by you once QA is green, with `gh pr merge --merge` and **never squash**. Never ask the pilot to open or merge. Only `[user-review]` gate tasks wait for the pilot.
- **Staging:** explicit paths only. No `git add -A`, no `git commit -a`.

## OPSEC and licensing (hard rules)
- **`reference/`** holds the pilot's footage and is git-ignored. Never commit, upload or copy anything out of it, and never write its file names anywhere. Cite clips and photos by **letter** only.
- **No** places, coordinates, dates, OSD values, unit details, people or specifics of the pilot's sorties, in any file, commit or message.
- **Maps:** no georeferencing; signs use **invented** names.
- **The OSD** reads **"Svinoriz 10 Opto"** and **"! SAFE !"**. Never revert either.
- **Free assets only:**
  - CC0 preferred, CC-BY with attribution
  - **no paid, Asset Store or Fab content, not even free items**
  - every third-party file is pinned in `tools/assets/manifest.json`, and `CREDITS.md` is generated from it

## Engineering rules that bite
- **The analog feed is a faithful port of the Godot feed** (D-026): `docs/legacy/video-feed-godot/`, A/B-checked against the original. The pilot wants the noise "very much similar".
- **The sim core is engine-free.**
  - It lives in `unity/Packages/com.fpv450.sim`, with `noEngineReferences: true`.
  - The .NET tools compile the same sources, so the headless replay and benches run without Unity.
  - Unity code talks to the core through adapters, never the other way round.
- **Determinism in the sim core:**
  - only IEEE basic operations (+ − × ÷ √): no `Math.Sin/Cos/Exp/Pow` (use `DetMath`) and no implicit FMA
  - seeded named streams, with every draw logged
  - zero managed allocations in steady state
  - **any golden re-record is audited case by case**
- **Physics:**
  - 1 kHz fixed step on its own thread; never a variable step
  - PhysX stays out of the flight loop
  - randomness is only a bounded perturbation of a named physical parameter, **never noise on the pose**
- **Assets are rebuilt, never hand-edited.** Generator plus params, and one command to rebuild. Physics metadata (collision, materials, gaps, wind volumes, snag) is authored with the asset.
- **Look at every asset.** The review sheet at 1.5 / 10 / 50 / 200 m is mandatory. Automated checks miss what eyes catch.
- **Performance numbers count only on AC power,** with vsync off and the power mode printed. On battery the laptop falls back to its iGPU.

## Tokens and limits
- **At most 2 subagents in parallel,** each with a short brief, and **one per logical task** (D-029).
- **Commit early.** Start fresh agents rather than resuming huge ones; resume an agent only if a usage limit stopped it.
- **Long jobs** (builds, batch renders, benchmarks): run them in the background and wait for the completion notice. **Never poll.**
- **Image-heavy work happens in subagents,** which write their results to files. Don't load footage or stills into your own context.
- **Persist everything to disk** (specs, sheets, decisions, reports). Compaction must lose nothing.

## Verification
These were made real and tested in phase 0 (2026-10-18). `docs/environment.md` holds the pinned versions, the `UNITY` and `BLENDER` paths and the per-clone setup. Unity batchmode needs **Unity Hub running** (the licence) and can't run while the editor has the project open. **`Unity.exe` is a GUI program: in PowerShell, pipe its output (`| Out-Host`) or the shell returns at once with no exit code.** `-testResults` paths resolve against the project, so the XML lands in `unity/Logs/` (ignored). **In a fresh worktree run `git lfs checkout` first:** a worktree can hold LFS pointer files, and Unity then imports nothing while the capture still exits 0.

```powershell
# sim core and tools
dotnet test tools/simtests
dotnet run -c Release --project tools/worldbench -- --golden
dotnet run -c Release --project tools/worldbench -- --bench
python -m pytest tools                       # assets, blender, hygiene, reference
python tools/assets/fetch.py --verify         # every third-party file pinned and credited
python tools/hygiene/precommit.py --tree HEAD # the OPSEC scan over the whole tree
python tools/map/validate.py maps/<map-id>    # from phase 3

# Unity
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-Host
& $env:UNITY -batchmode -projectPath unity -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile - | Out-Host   # no PlayMode tests yet
& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -logFile - | Out-Host
& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Review.Capture -asset <ID> -logFile - | Out-Host

# one asset end to end (params → Blender → FBX → Unity prefab → review sheet in review-sheets/<ID>/)
pwsh tools/build_asset.ps1 <ID>

# specs
openspec validate --all --strict
```

`dotnet run ... --golden` and `--bench` report "0 cases registered" until phase 3 adds the world cases.

Every handoff pastes the commands it ran and their output. "Done" without evidence is not done.
