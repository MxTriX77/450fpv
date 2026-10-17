# Workflow

All work is spec-driven through [OpenSpec](https://github.com/Fission-AI/OpenSpec). If a piece of work has no change folder under `openspec/changes/`, nobody works on it. The project skills in `.claude/skills/` (`openspec-explore`, `openspec-propose`, `openspec-apply-change`, `openspec-update-change`, `openspec-sync-specs`, `openspec-archive-change`) and the `/opsx:*` commands drive it. The project config is `openspec/config.yaml`. Every agent also follows the `karpathy-guidelines` skill. See [tooling.md](tooling.md).

## The loop

1. **Request.** The pilot asks for something or gives feedback, and the orchestrator takes it. The phase briefs in [briefs/](briefs/) seed the first changes of each phase.
2. **Spec.** The orchestrator proposes a change: `openspec/changes/<change-id>/` with `proposal.md`, `design.md`, `specs/` and `tasks.md`.
   - The proposal cites requirement IDs from [requirements.md](requirements.md).
   - The design labels every number **pilot / measured / published / derived / assumed**.
   - Run `openspec validate <id> --strict` before committing planning artefacts.
3. **Assign, one subagent per logical task** (D-029).
   - For **each logical task** (one reviewable unit: an asset, an object sheet group, a shader stage, a query, a mechanism, a screen, a review), the orchestrator spawns a **fresh** subagent of the owning role.
   - The brief is short: change id, task ids, files to read, checks to run.
   - It announces each handoff to the pilot (role, change id, task, goal) so they can see the team work.
   - When the agent has handed off, it is done. The next task gets a new agent.
4. **Build.** The subagent branches `unity/<prefix>/<change-id>` off `unity/main` and works through `tasks.md`.
   - It ticks each task as it finishes and commits each logical step under its own identity.
   - It runs the narrowest check per task.
5. **Review.** `qa-engineer` checks the branch against the spec scenarios, the tests, the budgets and repo hygiene. It writes `openspec/changes/<change-id>/review.md` and either approves or requests changes.
6. **Pilot sign-off at gates only.** These are G1–G8 in [plan.md](plan.md). Only tasks that deliver a gate are tagged `[user-review]`, and they block the merge until the pilot approves. Everything else is reviewed by QA and the orchestrator, never by the pilot.
7. **Merge, automatically.**
   - The orchestrator pushes the branch, opens the PR with `--base unity/main`, and merges it once QA is green, with `gh pr merge --merge` and never squash. It never asks the pilot to open or merge.
   - Once a change is complete, archive it; the spec deltas land in `openspec/specs/`.

If the pilot changes direction, update the affected change artefacts first. The code follows the spec, never the other way round.

## Branches

Everything for the Unity restart lives under the **`unity/` namespace** (D-025), so it never mixes with the Godot attempt's branches in the same repo.

| Branch | What it is |
|---|---|
| `unity/main` | **The trunk:** an orphan branch with no Godot history. It must always open in Unity and build. Only the orchestrator commits to it directly, and only for bookkeeping (docs, archive) |
| `unity/<prefix>/<change-id>` | **One per change** (for example `unity/physics/build-flight-model`, `unity/art/adobe-house`). [team.md](team.md) has the prefixes |
| `main`, and every branch outside `unity/` | The frozen Godot attempt (tag `godot-final`). Never touch them. Some share names with future Unity work (for example `physics/build-flight-model`); the namespace keeps them apart |

How to switch between the two codebases on GitHub and locally, and the gate tags: [repository.md](repository.md).

Git rules that come with the namespace:
- **Never create a branch named plain `unity`.** Git stores branch names as paths, so `unity` and `unity/...` can't coexist.
- **Listing only the Unity work:**
  - `git branch --list 'unity/*'`
  - `git branch -r --list 'origin/unity/*'`
  - `gh pr list --base unity/main`
- **Fetch only the Unity branches if the repo grows:** `git config remote.origin.fetch '+refs/heads/unity/*:refs/remotes/origin/unity/*'`. Optional.
- **Parallel agents work in separate git worktrees** (one per agent and branch), so nothing collides in the working folder.
- **After merge,** delete the feature branch (`gh pr merge --merge --delete-branch`). Its commits stay in the trunk through the merge commit.
- **Optional, with the pilot's OK (a repo setting):** a GitHub ruleset on `unity/main` that requires a PR and blocks force-pushes and deletion.

## Commits (user rule, `AGENTS.md`)

- The author is the role's identity from [team.md](team.md).
- **One logical step per commit.** Never lump a change into one commit.
- **Subject line only:** `type(scope): summary`, imperative, lower case, at most 72 characters.
  - Types: `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `chore`, `art`.
  - **No body, no trailers, no Claude or co-author line.**
- **Never commit** anything from `reference/`, Unity's `Library/`, `Temp/`, `Logs/` or `UserSettings/`, builds, logs, or local screenshots of reference frames.
- **Stage paths explicitly.** Never `git add -A`, never `git commit -a`. Another agent may be working in the repo.

## Pull requests

- **One PR per batch of work**, titled with the change id. A long change has several.
- **The body holds:**
  - the proposal summary
  - the task checklist
  - the requirement IDs covered
  - how the work was verified, with command output and screenshots or review sheets
- PRs are opened ready for review, never as drafts.

## Subagent handoff

Every subagent ends its turn with:

```
Change: <id> · Branch: <branch> · Tasks: N/M
Done: <bullets>
Verified by: <commands run, with their output pasted; screenshots or review sheets>
Requirements: <IDs met / IDs partially met>
Risks / questions: <bullets, or "none">
```

The orchestrator summarises it for the pilot in 2–5 lines.

## Reports

When the pilot asks for status, the orchestrator writes `docs/reports/YYYY-MM-DD.md`. It covers:
- what each role has done, what is in progress and what is blocked
- the decisions the pilot needs to make
- the open risks
- the next gate and its date estimate

## Verification

The exact commands live in `CLAUDE.md` (Verification). Every change runs its narrowest checks per task and the full set before review.

## Tokens and limits

- **At most 2 subagents in parallel.**
- **One fresh subagent per logical task** (D-029), with a short brief. Never reuse an agent for a second task, and don't resume agents that have grown huge.
- **Commit early and often,** so a stopped agent loses nothing.
- **Run long jobs in the background** (builds, benchmarks, batch renders) and wait for the completion notice. Never poll with sleep loops.
- **Image-heavy work happens inside subagents,** which write their findings to files. The orchestrator reads the files, not the images.
- **Resuming:** resume a subagent only to finish its own task after a usage limit stopped it. If the user stopped it, start a fresh one with a short brief.
- **Branches for task agents:** tasks of one change run sequentially on the change branch. Two parallel tasks of one change use task sub-branches `unity/<prefix>/<change-id>--<task>`, merged into the change branch. Never two agents on one branch at once, and each agent works in its own worktree.
