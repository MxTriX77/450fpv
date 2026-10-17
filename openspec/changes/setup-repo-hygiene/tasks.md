# Tasks: setup-repo-hygiene

## 1. Hook

- [x] 1.1 [qa-engineer] Add `.githooks/pre-commit` and `tools/hygiene/precommit.py` with the rules in the design, plus `tools/hygiene/tests/test_precommit.py` covering every spec scenario, and verify `python -m pytest tools/hygiene` passes and a force-added test clip (`.mp4`) is rejected in a real commit attempt
- [x] 1.2 [qa-engineer] Address the review (`review.md` R1, R2) and the revised spec: the `footage-text`, `reference-stem` and widened `coordinates` and `notes-date` rules, the unambiguous-only messenger entry, the `commit-msg` hook and the git-lfs shims, each with tests; verify `python -m pytest tools/hygiene` passes, the rules give zero findings over every file on `unity/main`, and an LFS object reaches a local bare remote with `core.hooksPath` set

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios (re-review after 1.2)
