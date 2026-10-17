# 450fpv: Unity restart

> **Two codebases, one repo.** This is the **Unity** codebase (branch `unity/main`). The first, Godot attempt is frozen on [`main`](https://github.com/MxTriX77/450fpv/tree/main) (tag `godot-final`). See [docs/repository.md](docs/repository.md).

A lightweight, ultra-realistic **heavy fiber-optic cargo FPV simulator**:
- one drone, **"Svinoriz 10 Opto"** (10-inch)
- one map of about three adjacent Ukrainian villages and their land
- an analog PAL goggles feed
- physics that is physical, seeded and logged

The manifesto is [VISION.md](VISION.md).

This folder is the **starting pack** for the Unity restart: the requirements, the plan, the decisions, the team set-up and everything learned in the first (Godot) attempt. Nothing has been built in Unity yet.

## Start here (the pilot)

1. **Copy your footage folder yourself.** Copy `reference\` from the old checkout (`C:\Users\maksy\.claude\projects\450fpv\reference\`, including `_frames\` if you want the extracted frames) to `C:\.dev\450fpv\reference\`. It is git-ignored. The agents are not allowed to copy it.
2. **Open Claude Code** in `C:\.dev\450fpv`.
3. **Say:** "Start phase 0 per docs/briefs/phase-0-setup.md".

   The orchestrator will:
   - set up the Unity branches in `MxTriX77/450fpv`, all under the `unity/` prefix: the trunk `unity/main`, and feature branches like `unity/physics/<change>`
   - install and pin the tools
   - create the Unity project and the pipelines
   - **ask you before** pushing, tagging `godot-final`, or changing the default branch

   Then it runs phase 1, using the brief in `docs/briefs/phase-1-assets.md`.
4. **You'll be asked to look at things only at the gates:**

   | Gate | What you look at |
   |---|---|
   | G1 | 10-minute look-lock |
   | G2 | gallery |
   | G3 | world |
   | G4 | feed |
   | G5 | drone |
   | G6 | wireframes |
   | G7 | first flight |
   | MVP-2…n | flights |
   | G8 | release |

## What's in the pack

| File | What it is |
|---|---|
| [VISION.md](VISION.md) | The pilot's manifesto, verbatim (never edited without asking) |
| [CLAUDE.md](CLAUDE.md) | The orchestrator's operating rules: loaded every session |
| [AGENTS.md](AGENTS.md) | The pilot's commit rule, plus rules for every agent |
| [docs/pilot-brief-2026-10-17.md](docs/pilot-brief-2026-10-17.md) | The restart brief, verbatim, plus the two answers given the same day |
| [docs/requirements.md](docs/requirements.md) | **The refined requirements**, with stable IDs and acceptance tests |
| [docs/plan.md](docs/plan.md) | **Phases 0–8 and gates**, what changed from the pilot's plan and why, and the token strategy |
| [docs/asset-list.md](docs/asset-list.md) | **The gallery contents:** about 100 assets, about 215 variants and states, with priorities |
| [docs/decisions.md](docs/decisions.md) | The decision log for the restart (Unity, URP, repo, free assets, editability, physics architecture, frames…) |
| [docs/tooling.md](docs/tooling.md) | **How the AI team works:** OpenSpec (config, skills, `/opsx` commands), the karpathy-guidelines skill, the orchestrator and subagents, the agent rules, token discipline |
| [docs/repository.md](docs/repository.md) | **One repo, two codebases:** Godot on `main` (tag `godot-final`), Unity under `unity/` (trunk `unity/main`); how to switch on GitHub and locally |
| [docs/team.md](docs/team.md) | Roles, git identities, ownership |
| [docs/workflow.md](docs/workflow.md) | The OpenSpec loop, branches, commits, PRs, handoffs, reports, token rules |
| [docs/pilot-questions.md](docs/pilot-questions.md) | What is already answered (never ask again), and the open questions, by gate |
| [docs/briefs/](docs/briefs/) | Kickoff briefs for phase 0 and phase 1 |
| [docs/knowledge.md](docs/knowledge.md) | **Everything learned in the first attempt,** distilled: pilot facts, drone, physics design, contact model, wind targets, world reference, map format, feed, lessons |
| [docs/reference-notes/](docs/reference-notes/) | The measured, OPSEC-clean analysis of the footage (terrain, video feed, wind, airframes, pilot requirements), copied verbatim |
| [docs/legacy/](docs/legacy/) | **The Godot analog feed** to port faithfully (`video-feed-godot/`), plus the first attempt's flight-model design and specs, map and world specs, physics reviews, surface table and old decision log |
| [docs/object-sheets/_template.md](docs/object-sheets/_template.md) | The template phase 1 fills in per asset group |
| [openspec/config.yaml](openspec/config.yaml) | The OpenSpec project config for the Unity restart: context and rules |
| `.claude/skills/`, `.claude/commands/opsx/` | **The skills:** karpathy-guidelines and the six OpenSpec skills, plus the `/opsx:*` commands, copied from the first attempt |
| [.claude/agents/](.claude/agents/) | The seven subagents: reference-analyst, asset-artist, world-builder, tech-artist, physics-engineer, game-developer, qa-engineer |
| `.gitignore`, `.gitattributes` | `reference/` excluded from day one; Unity ignores; LFS patterns |
