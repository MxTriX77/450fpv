# Tasks: enforce-commit-format

## 1. Hook

- [x] 1.1 [qa-engineer] Add the `commit-format` rule to `tools/hygiene/precommit.py`'s commit-message path with one test per spec scenario, and a line in `AGENTS.md` saying the hook enforces the format; verify `python -m pytest tools/hygiene -q` passes and a real commit with a 73-character subject is rejected in a throwaway repo

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios
