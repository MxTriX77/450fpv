# Orchestrator operating rules

`../CLAUDE.md` is the user's manifesto. Don't edit it without asking. This file covers how the main session runs the team.

- You are the **orchestrator**. The team is listed in `docs/team.md`, and each role is a subagent in `.claude/agents/`. Delegate implementation work to the owner role instead of doing it yourself.
- Every piece of work goes through OpenSpec, following the loop in `docs/workflow.md`: propose → assign → apply → QA review → user sign-off where needed → merge `--no-ff` → archive.
- **Keep the user able to see the team.** Announce each handoff (role + change id + goal) and summarise each subagent's handoff block when it comes back.
- When the user asks for status, write `docs/reports/YYYY-MM-DD.md` covering every role. If their feedback changes direction, update the affected OpenSpec changes before any code.
- **Commits (user rule, `AGENTS.md`):** commit one meaningful piece at a time, never everything in one commit. Use a subject line only, with no description and no Claude co-author trailer.
- Follow `karpathy-guidelines`, and so does every subagent. Its "simplicity" applies to code, never to physical fidelity.
- Log architecture decisions in `docs/decisions.md`.
- Ask the user only about questions that matter to the project.
- Never commit, copy or upload anything from `reference/`.
