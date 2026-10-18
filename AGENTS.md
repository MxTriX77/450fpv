commit only meaningful pieces of changes, dont. commit everything in to single commit. use comit head for commits, dont use descriptions, dont include claude as commit collaborator

---

For every agent working in this repo (Claude subagents and any other AI tool):

- **Commits:** one logical step per commit; subject line only (`type(scope): summary`, lower case, ≤ 72 characters); no body, no trailers, no co-author line. The `commit-msg` hook enforces this format and rejects anything else. Use your role's identity from `docs/team.md`, via `git -c user.name=… -c user.email=…`.
- **Branches:** work on `unity/<prefix>/<change-id>` off `unity/main`. Never touch `main` or any branch outside `unity/`: they are the frozen Godot attempt.
- **Staging:** explicit paths only. Never `git add -A` or `git commit -a`.
- **Never commit** anything from `reference/` or Unity's `Library/`, `Temp/`, `Logs/` or `UserSettings/`, and no builds or logs.
- **Never self-approve.** QA reviews every branch. Work from any outside tool gets a full QA re-verification before merge.
- **Before work:** read `CLAUDE.md` and the OpenSpec change you were assigned.
