# Tasks: enforce-commit-format

## 1. Hook

- [x] 1.1 [qa-engineer] Add the `commit-format` rule to `tools/hygiene/precommit.py`'s commit-message path with one test per spec scenario, and a line in `AGENTS.md` saying the hook enforces the format; verify `python -m pytest tools/hygiene -q` passes and a real commit with a 73-character subject is rejected in a throwaway repo
- [ ] 1.2 [qa-engineer] Address the review (`review.md` A and B): accept every subject git's own `fmt-merge-msg` writes (`Merge branch`, `Merge branches`, `Merge remote-tracking branch`, `Merge tag`, `Merge commit`, `Merge pull request #N from`), and take the first non-blank line as the subject whatever it starts with (a `#` subject or an empty message is a finding), with one real-commit test per case; verify `python -m pytest tools/hygiene -q` passes and a real `git merge origin/<branch>` commits through the hook

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios (re-review after 1.2)
