# Review: fix-reference-stem-scope

**Reviewer:** qa-engineer (independent of the implementing QA agent) · **Branch:** `unity/qa/fix-reference-stem-scope` at `09e29a0`, off `unity/main` at `e4f78d5` (merge base verified) · **Requirements:** SEC-01, SEC-06 · **Decision:** D-013

## Scope

One rule change: `reference_stems()` in `tools/hygiene/precommit.py` prunes the folders directly under `reference/` whose name starts with `_` from the stem walk (6 lines), plus one integration test, `test_tool_output_stems_are_not_footage` (17 lines). The diff `e4f78d5..HEAD` touches six files: the four change artefacts, the rule and its test. Nothing else.

Every check below was run by me on this machine, from this worktree, with the main checkout's `reference/_frames/` present (the condition that made the old rule misfire).

## Checks run

```
$ python -m pytest tools/hygiene -q
407 passed in 127.94s (0:02:07)

$ python tools/hygiene/precommit.py --tree origin/unity/main
351 files scanned, 0 findings        rc=0
$ python tools/hygiene/precommit.py --tree HEAD
355 files scanned, 0 findings        rc=0

$ openspec validate fix-reference-stem-scope --strict
Change 'fix-reference-stem-scope' is valid
```

The suite includes `test_existing_tree_is_clean` for both `HEAD` and "origin/unity/main merged with HEAD" (no skips in the run), so the branch also merges cleanly and leaves the trunk scan at zero findings.

## Probes in throwaway repos

A scratch script builds fresh repos with `core.hooksPath` pointing at this worktree's `.githooks`, plants a git-ignored fake `reference/` and tries real commits through both hooks. Every planted name is built at run time; the script and this review hold no literal stem. The pilot-style stem is a hyphenated word-and-number name; the sheet-style stems are a two-letter prefix, an underscore and a four-digit range, the shape the contact-sheet tool writes. In every probe a pilot-style clip sits directly under `reference/`, so the `reference-stem` rule is live throughout (a pass is never the rule being switched off for lack of stems).

```
[OK ] P1 _frames sheet stem in note: expect=pass got=pass rc=0 rules=- leaked=False head=True
[OK ] P1c control: pilot stem in note (same state): expect=block got=block rc=1 rules=['reference-stem'] leaked=False head=False
[OK ] P2 same name directly under reference/: expect=block got=block rc=1 rules=['reference-stem'] leaked=False head=False
[OK ] P3 _compare stem in note: expect=pass got=pass rc=0 rules=- leaked=False head=True
[OK ] P4 photos/ (no underscore) stem in note: expect=block got=block rc=1 rules=['reference-stem'] leaked=False head=False
[OK ] P5 reference/a/_b/ stem in note (not a direct child): expect=block got=block rc=1 rules=['reference-stem'] leaked=False head=False
[OK ] P6 force-add a file under reference/_frames/: expect=block got=block rc=1 rules=- leaked=False head=False
[OK ] P7a commit message names a _frames sheet stem: expect=pass got=pass rc=0 rules=- leaked=False head=True
[OK ] P7b commit message names the pilot stem: expect=block got=block rc=1 rules=['reference-stem'] leaked=False head=False
[OK ] M1 pre-fix rule: _frames sheet stem in note: expect=block got=block rc=1 rules=['reference-stem'] leaked=False head=False

10/10 probes as expected
```

Notes:
- `leaked` checks the stem against the hook's stderr, case-insensitively: no finding printed a stem.
- P6 is blocked by the path rule (`reference-path`): the probe's rule parser lists only `path:line` findings, so that line shows as `-`; rc=1 and nothing was committed. The skip loosens the stem walk only; files under `reference/_frames/` still cannot be committed.
- M1 runs the same P1 commit against a byte-for-byte copy of `precommit.py` from `e4f78d5` (confirmed: `git diff --no-index --stat` against the branch file shows the 6-line fix as the only difference, and the copy has no `startswith("_")`). The pre-fix rule blocks it.

## Does the pass half of the new test prove the scenario's intent?

Yes, for three reasons, two of them in the test itself and one shown by experiment:
1. The pass half plants the pilot-style clip next to the `_frames/X/sheets/` sheet, so the `reference-stem` rule is in `line_rules` while the note commits. Without that clip the pass would be vacuous (no stems, rule absent). The docstring states this on purpose ("even while the rule is live for the pilot's clip next to it").
2. The block half re-plants the same stem directly under `reference/` and asserts `reference-stem`, the `file:3` location, no stem in the output, and HEAD unchanged. The only variable between the halves is the folder, which is exactly what the scenario specifies ("while a pilot-style file directly under `reference/` with the same stem would").
3. M1: with the pre-fix rule the pass half's commit is blocked, so the test fails without the fix. It is a real regression test, not a tautology.

The planted sheet stem has the shape the tool writes (letter prefix, underscore, four-digit range, 12 characters, so it clears the 8-character and digit thresholds of `distinctive_stem`), and no other line rule touches it (not a camera pattern, a date or a coordinate), so the pass is attributable to the stem rule alone.

## Spec scenarios (delta, MODIFIED requirement "No footage names inside text")

| Scenario | Result | Evidence |
|---|---|---|
| Camera-style name written in a review is blocked | PASS | `test_camera_style_name_in_review_is_blocked` (4 cases) in the 407 |
| Reference file stem in a note is blocked | PASS | `test_reference_file_stem_in_a_note_is_blocked` (2), `..._in_a_linked_worktree`; probes P1c, P2, P4, P5 |
| Footage name in a commit message is blocked | PASS | `test_footage_name_in_commit_message_is_blocked` (4); probe P7b |
| Extension patterns pass | PASS | `test_extension_patterns_pass` (3) |
| Tool output stems are not footage | PASS | `test_tool_output_stems_are_not_footage`; probes P1, P2, P3, P7a; mutation M1 |

Requirement text: the skip applies to folders directly under `reference/` whose name starts with `_` (P1, P3), a folder without the underscore is still walked (P4), and a `_` folder deeper down is still walked (P5). Stems are still read at scan time only and never printed (`leaked=False` throughout).

## Requirements

- **SEC-01** (nothing from the pilot's references in the repo): met. The path rule is untouched (P6), the stem rule still guards every file outside the tools' own folders, and the hook prints no stem.
- **SEC-06** (hygiene scan): met. False positive fixed in the rule, not bypassed; the trunk and branch scans are at zero findings on a machine with the real `_frames/` tree.
- Other rules: unchanged (the diff touches only `reference_stems()`); the full suite is green.

## Hygiene

- One commit for the fix, `09e29a0`, author `QA Engineer <qa@450fpv.local>`, subject `fix(hygiene): skip the tools' own folders in the reference stem walk` (68 characters), no body, no trailer, no co-author line. The proposal commit `3e147c3` is the orchestrator's.
- The commit touches exactly `tools/hygiene/precommit.py` and `tools/hygiene/tests/test_precommit.py`; nothing from `reference/`, `Library/`, `Temp/` or `Logs/` in the diff.
- No file names, places, dates of sorties or OSD values in the diff; the new test constant is joined at run time (`test_own_sources_hold_no_planted_value` passes over the changed test file).
- No third-party files; D-003 not applicable.

## Residual risk and suggestions (not blockers)

1. Any pilot file dropped into `reference/_frames/` or `reference/_compare/` is no longer stem-guarded (it still cannot be committed: P6). The tools name their outputs by letter only, and the analysts read the clips from `reference/` itself, so this is acceptable, but it is the one thing the fix gives up.
2. Spec wording: the delta says "Folders under `reference/` whose name starts with `_`"; the code and its docstring, the brief and probe P5 all mean *directly* under. The code is the stricter (safer) reading. Suggest adding the word "directly" to the requirement when the delta is synced, so the archived spec matches the behaviour exactly.

Verdict: APPROVED
