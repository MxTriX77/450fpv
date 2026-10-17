# Review: enforce-commit-format

**Branch:** `unity/qa/enforce-commit-format` at `261aa16` (3 commits on `bd9e95e`) · **Reviewer:** qa-engineer (independent of the implementing agent) · **Task:** 2.1
**Requirements:** PRC-03, SEC-06 · **Spec delta:** `specs/repo-hygiene/spec.md`, requirement "Commit message format"
**Environment:** Python 3.14.6, pytest 9.1.1, git 2.55.0.windows.3, Windows 11. Every check below was run by me in this worktree; nothing was taken from the handoff.

## What the branch changes

`git diff bd9e95e..HEAD --stat`

```
 .githooks/commit-msg                            |  2 +-
 AGENTS.md                                       |  2 +-
 openspec/changes/enforce-commit-format/tasks.md |  2 +-
 tools/hygiene/precommit.py                      | 49 +++++++++++++--
 tools/hygiene/tests/test_precommit.py           | 82 ++++++++++++++++++++++++-
```

`commit_format(text)` in `tools/hygiene/precommit.py`: the first non-blank, non-`#` line is the subject; it must be ≤ 72 characters after `rstrip()`, match `^(feat|fix|perf|refactor|test|docs|chore|art)(\([^\s()]+\))?: (\S.*)$`, not start its summary with an upper-case letter, and nothing non-empty may follow it. A subject matching `^Merge (?:branch|pull request) ` skips every check. `scan_message` appends the findings as rule `commit-format` with a reason in brackets.

## Checks run

### Test suite

```
$ python -m pytest tools/hygiene -q
........................................................................ [ 18%]
........................................................................ [ 37%]
........................................................................ [ 56%]
........................................................................ [ 75%]
........................................................................ [ 94%]
....................                                                     [100%]
380 passed in 102.18s (0:01:42)
```

One integration test per spec scenario (`test_long_subject_is_rejected`, `test_wrong_shape_is_rejected` ×3, `test_body_or_trailer_is_rejected` ×2, `test_conforming_subject_passes` ×2, `test_git_merge_commit_passes` ×2), plus unit tests for accepted type/scope shapes, near misses, and comment/scissors handling. The suite does not cover git's other merge subjects (see finding A), which is how the gap got through.

### Real commits in a throwaway repo

`core.hooksPath` → this worktree's `.githooks` (so `commit-msg` runs this branch's `precommit.py`), `core.autocrlf=false`, fresh `git init -b main`. Each case stages a change and commits; "rejected" means git exited 1, the hook printed `commit-format`, and HEAD did not move.

| Scenario | Message | Result | Hook output (reason) |
|---|---|---|---|
| Long subject is rejected | `fix(review): ` + 60 × `a` (73 chars) | **PASS** rejected | `commit message:1 (the subject is 73 characters, the limit is 72)` |
| Wrong shape is rejected | `Added the thing` | **PASS** rejected | `commit message:1 (the subject must read type(scope): summary or type: summary, type one of feat, fix, perf, refactor, test, docs, chore, art)` |
| Wrong shape is rejected | `feature(x): thing` | **PASS** rejected | same reason, line 1 |
| Wrong shape is rejected | `fix(x): Thing` | **PASS** rejected | `commit message:1 (the summary must not start with an upper-case letter)` |
| Body or trailer is rejected | `-m "fix(x): thing" -m "Explains why."` | **PASS** rejected | `commit message:3 (nothing may follow the subject: no body, no trailer, no co-author line)` |
| Body or trailer is rejected | `-m "fix(x): thing" -m "Co-Authored-By: Someone <someone@example.invalid>"` | **PASS** rejected | same reason, line 3 |
| Conforming subject passes | `fix(review): keep the bar in frame` | **PASS** committed | rc=0, no output |
| Git merge commit passes | real `git merge --no-ff --no-edit feature` | **PASS** committed | rc=0, subject `Merge branch 'feature'`, 2 parents (`rev-list --parents` lists 3 ids) |
| Git merge commit passes | `-m "Merge pull request #1 from x/y" -m "feat(x): add y"` | **PASS** committed | rc=0, subject `Merge pull request #1 from x/y` with its body line |

Every spec scenario passes as written. The rejection text names the rule, the line and the length, and prints none of the message itself.

### Break attempts

| # | Attempt | What happened | Matters? |
|---|---|---|---|
| B1 | 72-char subject + 3 trailing spaces, via `-F` | committed; git stored the subject at 72 chars (it strips trailing whitespace), the hook's `rstrip()` agrees | no |
| B2 | a tab inside the summary | committed with the tab (`keep^Ithe bar`) | no: cosmetic, not a format rule |
| B3 | scope with a slash `fix(a/b):` and with dots `fix(com.fpv450.sim):` | committed | no: intended, `[^\s()]+` |
| B4 | `art: add the house` (no scope) | committed | no: `type: summary` is allowed by the spec |
| B5 | UTF-8 BOM before the subject, via `-F` | rejected (shape); the reason does not say why a correct-looking subject failed | no: safe direction, but the reason is misleading; `decode("utf-8-sig")` in `scan_message` would read it the way `decode_text` already does |
| B6 | empty message | without `--allow-empty-message` git aborts itself ("Aborting commit due to empty commit message"); **with** the flag the hook returns no finding and a commit with an empty subject is created | low: needs the explicit flag. Covered by the fix for B7 |
| B7 | `-m "# just a comment"` and `-m "#123 fix the thing"` | **committed with those subjects.** The hook drops every `#` line as a comment, but `git commit -m` uses `cleanup=whitespace`, which keeps them, so a `#`-leading first line bypasses the whole rule | **yes**, finding B |
| B8 | `fixup! …` and `squash! …` prefixes | rejected (shape) | no: the workflow has no autosquash; by spec |
| B9 | real `git merge --no-ff --no-edit origin/topic`, `… v1` (a tag), `… <sha>` | **all three rejected** (shape). git's own subjects, read from `.git/MERGE_MSG`: `Merge remote-tracking branch 'origin/topic'`, `Merge tag 'v1'`, `Merge commit '<sha>'`. `Merge branches 'a' and 'b'` (octopus shape) is also rejected. The pull shape `Merge branch 'topic' of <url>` passes | **yes**, finding A |
| B10 | trailer on line 2 with no blank line, via `-F` | rejected, `commit message:2` | no: correct |
| B11 | CRLF message, via `-F` | committed, stored without the CR; the length ignores `\r` | no: correct |
| B12 | a lone `\r` inside the subject: `fix(x): a\rCo-Authored-By: nobody` | committed as one subject line with an embedded CR | no: only reachable on purpose; `--no-verify` is the easier bypass for anyone who wants one |
| B13 | real `git revert --no-edit <sha>` | committed as `Revert "feat(x): add f"` with the body `This reverts commit …` and **no hook output**: git does not run `commit-msg` for `revert`/`cherry-pick` unless `--edit` is given, so this is git's doing, not the rule's | no, but worth knowing: a revert must be done with `--no-commit` and a formatted subject to satisfy PRC-03, and the hook will not remind anyone |
| B15–B17 | `FIX: thing`, `fix(x):  thing` (two spaces), ` fix(x): thing` (leading space) | all rejected | no: stricter than needed, all correct |
| B18 | two blank lines before the subject, via `-F` | committed; git strips them; the hook reports the real line number if it has to | no |
| B19 | hand-written `Merge branch 'x' ` + 80 × `c` (97 chars) | committed: the merge exemption skips the length check too | no: git's own merge subjects regularly exceed 72 (`Merge branch '<long branch>' into <long branch>`), so the exemption has to skip it |
| B20 | `feat(x)!: thing` | rejected | no: not the project's format |
| B21–B22 | summary starting with a digit (`perf: 1 khz step`) or a backtick | committed | no: intended |
| B23 | `git commit --amend -m "Amended the thing"` | rejected; HEAD unchanged | no: the hook covers amend |

### Trunk history

`commit_format` over `%B` of every commit on `origin/unity/main` (fetched, tip `bd9e95e`, 107 commits):

```
107 commits on origin/unity/main, 4 with commit-format findings
  e082dbf (84 chars): feat(blender): enforce the metadata conventions and the closed physics material list
      line 1: the subject is 84 characters, the limit is 72
  840f65a (81 chars): art(k01): add the brick_red recipe, its material, the prefab and the import metas
      line 1: the subject is 81 characters, the limit is 72
  3caeff5 (75 chars): docs(openspec): record cull distance and the fbx x-sign check in the design
      line 1: the subject is 75 characters, the limit is 72
  c98b88d (76 chars): docs(openspec): name the mesh lod import switch in the asset pipeline design
      line 1: the subject is 76 characters, the limit is 72
(14 merge commits exempt)
```

Exactly the four known long subjects; no shape, case, body or trailer finding anywhere else. The 14 GitHub merge commits (`Merge pull request #N from …` with the PR title as body) are exempt as intended. Nit for a later docs commit, not this branch: `proposal.md` says "three subjects over 72 characters"; there are four.

### Hygiene

```
$ git log --format='%h %an <%ae> | %s' bd9e95e..HEAD
261aa16 QA Engineer <qa@450fpv.local> | docs(openspec): tick task 1.1 of enforce-commit-format
6ab0949 QA Engineer <qa@450fpv.local> | docs(agents): say the commit-msg hook enforces the format
d243582 QA Engineer <qa@450fpv.local> | feat(hygiene): enforce the commit format in the commit-msg hook
$ git log --format='%s' bd9e95e..HEAD | awk '{ print length($0) }'
54
57
63
$ git log --format='%B' bd9e95e..HEAD | cat -A        # subject, blank line, nothing else, for all three
$ git log --format='== %h' --name-only bd9e95e..HEAD
== 261aa16  openspec/changes/enforce-commit-format/tasks.md
== 6ab0949  AGENTS.md
== d243582  .githooks/commit-msg  tools/hygiene/precommit.py  tools/hygiene/tests/test_precommit.py
$ git ls-files -s .githooks/commit-msg && git ls-files --eol .githooks/commit-msg
100755 673eef05067d4ed744743f47ed12ff686a5f352f 0	.githooks/commit-msg
i/lf    w/lf    attr/text eol=lf      	.githooks/commit-msg
$ python tools/hygiene/precommit.py --tree HEAD
323 files scanned, 0 findings
$ openspec validate enforce-commit-format --strict
Change 'enforce-commit-format' is valid
```

Subject-only, ≤ 72, QA identity, one logical step per commit, only the files the task names; nothing from `reference/`, `Library/`, `Temp/` or `Logs/`; no OPSEC strings (the tree scan is clean, and the new test messages use `example.invalid`). No third-party files, so D-003 does not apply. The `AGENTS.md` line is accurate once finding A is fixed ("rejects anything else" is currently also true of three of git's own merge messages).

## Requirements

- **PRC-03** (subject only, `type(scope): summary`, ≤ 72, no body/trailers/co-author): **partly met.** Enforced for every hand-written message. Not met for a `#`-leading `-m` subject (finding B), and the rule wrongly fires on git's own merge subjects other than `Merge branch`/`Merge pull request` (finding A).
- **SEC-06** (the hook scan): **met**; unchanged behaviour, the OPSEC rules still run first and the format findings are appended.
- **Spec requirement "Commit message format":** scenarios 5/5 pass; the requirement sentence "Git's own merge commits SHALL pass" is **partly met** (finding A).

## Findings

**A. Required. Git's own merge subjects beyond `Merge branch`/`Merge pull request` are rejected.** `MERGE_SUBJECT = ^Merge (?:branch|pull request) ` misses `Merge remote-tracking branch '…'`, `Merge tag '…'`, `Merge commit '…'` and the plural octopus forms, all written by git's `fmt-merge-msg`. Proved with real merges (B9). The realistic trigger is bringing a feature branch up to date with `git merge origin/unity/main`: the hook blocks it with the "must read type(scope)" reason, and the only ways out are an invented `-m` subject or `--no-verify`, which `precommit.py` itself says must never happen ("a false positive is fixed in the rule, never bypassed"). Fix: match git's shapes, for example `^Merge (?:(?:remote-tracking )?branch(?:es)?|tags?|commits?) '` plus `^Merge pull request #\d+ from `, keeping the hand-written `Merge the thing` near-miss rejected, and add a real `git merge origin/<branch>` case (and a tag) to `test_git_merge_commit_passes`.

**B. Required. A `-m` message whose first line starts with `#` passes untouched.** `commit_format` drops `#` lines as comments, which is right for the editor template but wrong for `git commit -m`, where git keeps them (`cleanup=whitespace`): `# just a comment` and `#123 fix the thing` became subjects (B7), and `--allow-empty-message -m ""` became a commit with no subject (B6). The spec says a subject that does not read `type(scope): summary` is rejected. Fix in the same function: treat the first non-blank line as the subject whatever it starts with (git's template never puts a comment above the subject), drop `#` lines only after it, and report "the message has no subject" when nothing is left; with the editor, git would have aborted an all-comment message anyway, so this costs nothing. One test per case.

**C. Note, no change required.** `git revert`/`git cherry-pick` without `--edit` do not invoke `commit-msg` at all (B13), so a revert lands as `Revert "…"` with a body. Not this change's bug; worth a line in `docs/workflow.md` ("revert with `--no-commit` and a formatted subject") at some point.

**D. Note.** A BOM-prefixed message is rejected with a misleading reason (B5); `decode("utf-8-sig")` in `scan_message` would fix it in one word. Optional.

Everything else in the branch is correct, minimal and well tested; the three commits are clean and can stay as they are. A fourth commit on this branch with A and B and their tests is enough for re-review.

Verdict: CHANGES REQUESTED
