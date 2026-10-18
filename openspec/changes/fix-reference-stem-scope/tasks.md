# Tasks: fix-reference-stem-scope

## 1. Fix

- [ ] 1.1 [qa-engineer] Skip `_`-prefixed folders under `reference/` in the stem walk of `tools/hygiene/precommit.py`, with the "Tool output stems are not footage" test; verify `python -m pytest tools/hygiene -q` passes and `python tools/hygiene/precommit.py --tree origin/unity/main` reports 0 findings on the dev machine

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios
