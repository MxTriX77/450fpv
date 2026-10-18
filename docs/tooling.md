# Tooling: how the AI team works

Everything below is part of the project and lives in the repo. A new session in `C:\.dev\450fpv` picks it up automatically (D-030).

## 1. OpenSpec: spec-driven development
**What it is:** [OpenSpec](https://github.com/Fission-AI/OpenSpec). Every piece of work is written down as a *change* (proposal → design → specs → tasks) before anyone builds it, so nothing gets lost between agents or sessions.

**CLI:** `openspec` (npm, global). Version 1.13.1 in the first attempt; phase 0 checks it, or installs it with `npm i -g @fission-ai/openspec`.

**Project config:** [../openspec/config.yaml](../openspec/config.yaml), `schema: spec-driven`. It is already written for the Unity restart:
- **context:** the product, stack, realism definition, settings and OPSEC
- **rules:**
  - every proposal names an owner role, the requirement IDs, and non-goals
  - specs are measurable
  - visuals are judged against the references
  - designs label every number
  - one task = one subagent
  - role tags on tasks, `[user-review]` only for gates
  - the last task is always QA

**Phase 0:** run `openspec init` only if the CLI asks for it, and **keep this config.**

**Skills** (in `.claude/skills/`, loaded by Claude Code automatically):

| Skill | Use |
|---|---|
| `openspec-explore` | think a fuzzy request through before proposing |
| `openspec-propose` | create a change with proposal, design, specs and tasks in one step |
| `openspec-apply-change` | implement a change's tasks |
| `openspec-update-change` | update a change when the pilot changes direction (specs first, then code) |
| `openspec-sync-specs` | merge a change's spec deltas into `openspec/specs/` |
| `openspec-archive-change` | archive a finished change |

**Slash commands** (in `.claude/commands/opsx/`): `/opsx:explore`, `/opsx:propose`, `/opsx:apply`, `/opsx:update`, `/opsx:sync`, `/opsx:archive`.

**Validation:** `openspec validate <change> --strict` before committing planning artefacts, and `openspec validate --strict` in the checks.

**The loop:** see [workflow.md](workflow.md).

## 2. karpathy-guidelines: the prompt and behaviour guide
**What it is:** the project's "prompt optimisation" skill. It is behaviour guidelines that cut common LLM coding mistakes, derived from Andrej Karpathy's observations (MIT licence). It lives in `.claude/skills/karpathy-guidelines/SKILL.md`.

**Chosen over the alternatives** (2026-09-22): the pilot asked to compare **ponytail**, **karpathy-guidelines** and **caveman**, and chose karpathy-guidelines ("Karpathy - ok, go ahead").

**Who follows it:** the orchestrator, and **every subagent**. Each agent file loads it through `skills: [karpathy-guidelines]` in its frontmatter. Its four rules:
1. **Think before coding:** state assumptions, surface trade-offs, name confusion. Ask the pilot only if it is pilot-only knowledge (D-024).
2. **Simplicity first:** the minimum code that solves the problem, nothing speculative.
3. **Surgical changes:** touch only what the task needs, and match the existing style.
4. **Goal-driven execution:** turn tasks into verifiable checks, and loop until they pass.

**The project's one amendment:** **simplicity applies to code, never to physical fidelity.** The physics, the geometry and the feed stay complete (REAL-05, PHY).

## 3. The orchestrator and the subagents
- **[`CLAUDE.md`](../CLAUDE.md):** the orchestrator's operating rules, loaded every session. It covers the read-first list, realism, delegation, the pilot, git, OPSEC, engineering rules, tokens and verification.
- **[`.claude/agents/`](../.claude/agents/):** seven role subagents. Each file holds the role's description (for auto-selection), what it owns, its principles, its git identity and branch prefix, and the handoff block:
  - reference-analyst, asset-artist, world-builder, tech-artist, physics-engineer, game-developer, qa-engineer
- **One fresh subagent per logical task** (D-029), always on Opus 5.5, planned as independent units that can run at the same time (D-033).
- **At most 4 in parallel** (D-036).
- **Each works in its own git worktree,** using Claude Code's worktree isolation.
- **Handoff block:** every subagent ends with it ([workflow.md](workflow.md)), and the orchestrator summarises it for the pilot. The pilot can see the team work.

## 4. Repository rules for every agent
- **[`AGENTS.md`](../AGENTS.md):** the pilot's commit rule, verbatim, plus the rules for any AI tool:
  - one logical step per commit
  - subject line only, no co-author
  - role identities, explicit staging
  - never self-approve
- **Pre-commit hook** (built in phase 0, SEC-06): it blocks footage files, messenger names and coordinates in docs.
- **`.gitignore` / `.gitattributes`:** `reference/` is excluded from the first commit; Unity ignores; LFS patterns.

## 5. Token and context discipline
These are the "prompt economy" rules that kept the first attempt alive through usage limits.
- **One subagent per logical task,** with a short brief (change id, task ids, files to read, checks); at most 4 at once (D-036).
- **Image-heavy work stays inside subagents,** using contact sheets instead of single frames. Results go to files.
- **Everything persists to disk** (specs, sheets, decisions, reports), so compaction or a new session loses nothing.
- **Long jobs run in the background,** never polled.
- **Commit early.** Resume an agent only to finish its own task after a usage limit.

## 6. Other tools (installed and pinned in phase 0)
- **Unity 6 LTS** (through Unity Hub), **Blender** (headless), **Python 3.12+**, **.NET SDK**, **git-lfs**, **ffmpeg**, **`gh`**.
- **Exact versions** and the `UNITY` path go in `docs/environment.md`.
- **Optional:** a Unity MCP server, to drive the editor from Claude. Phase 0 checks whether one is available and reliable. Batchmode commands stay the verified path.
