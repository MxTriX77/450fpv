# Workflow

All work is spec-driven through [OpenSpec](https://github.com/Fission-AI/OpenSpec). If a piece of work has no change folder under `openspec/changes/`, nobody works on it.

## The loop

1. **Request.** The user asks for something or gives feedback, and the orchestrator takes it.
2. **Spec.** The orchestrator runs `/opsx:explore` if the request is fuzzy, then `/opsx:propose <change-id>`. This creates `openspec/changes/<change-id>/` with `proposal.md`, `design.md`, `specs/` and `tasks.md`.
3. **Assign.** The orchestrator spawns the owner role's subagent with the change id. It announces the handoff (role + change id) so the user can see it.
4. **Build.** The subagent branches `<prefix>/<change-id>` off `main` and works through `tasks.md` (`/opsx:apply`). It ticks each task as it finishes and commits under its own identity.
5. **Review.** `qa-engineer` checks the branch against the spec scenarios, the tests, the performance budget and repo hygiene. It writes `openspec/changes/<change-id>/review.md` and either approves or requests changes.
6. **User sign-off.** Any task tagged `[user-review]` (visuals, flight feel, wireframes) blocks the merge until the user approves it.
7. **Merge.** The orchestrator merges with `git merge --no-ff`, which keeps each role's authorship visible (never squash). It then runs `/opsx:archive <change-id>` so the spec deltas land in `openspec/specs/`.

If the user changes direction partway, the orchestrator updates the affected change artifacts first (`/opsx:update`). The code follows the spec, never the other way round.

## Branches

- `main` must always open in Godot and run. Only the orchestrator commits to it directly, and only for bookkeeping (docs, archive).
- Every other branch is `<prefix>/<change-id>`, one per OpenSpec change. The prefixes are listed in [team.md](team.md).

## Commits

- The author is the role's identity from [team.md](team.md).
- Commit one logical step at a time. Never lump a whole change into one commit.
- **Subject line only.** No body and no trailers. Never add a Claude or co-author line (user rule, see `AGENTS.md`).
- Subject line format is `type(scope): summary`, imperative and at most 72 characters. The types are `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `chore`, `art`.
- Never commit anything from `reference/`, `.godot/`, logs or builds.

## Pull requests

Until the GitHub remote and `gh` are set up, a pull request is the branch plus `review.md`.
After that, each change gets one PR titled with the change id. The PR body holds the proposal summary, the task checklist and how the work was verified.

## Subagent handoff

Every subagent ends its turn with:

```
Change: <id> · Branch: <branch> · Tasks: N/M
Done: <bullets>
Verified by: <tests / measurements / screenshots>
Risks / questions: <bullets, or "none">
```

## Reports

When the user asks for status, the orchestrator writes `docs/reports/YYYY-MM-DD.md`. It lists what each role has done, what is in progress and what is blocked, followed by the decisions the user needs to make and the open risks.
