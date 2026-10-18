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

## Re-review (task 2.1, after task 1.2)

**Branch:** `unity/qa/enforce-commit-format` at `6c8516b` (8 commits on `bd9e95e`) · **Reviewer:** qa-engineer, a fresh agent independent of the one that wrote the fix (`09a5aaa`, `e6ffafd`) and of the one that wrote the first review · **Contract:** the spec delta as revised by the orchestrator in `507adc4` (merge scenario widened to every `fmt-merge-msg` shape, new scenario "Comment-like subject is not a bypass", "the subject is the first non-blank line").
**Environment:** Python 3.14.6, pytest 9.1.1, git 2.55.0.windows.3, Windows 11. Every check below was run by me in this worktree, on a throwaway repo whose `core.hooksPath` is this worktree's `.githooks` (so this branch's `precommit.py` runs; the main checkout sits on `unity/main`, whose `precommit.py` has no `commit_format` at all, which is why the fixer's own commits went through no format check; I checked them by hand below).

### What the fix changes

`git show 09a5aaa --stat`: `tools/hygiene/precommit.py` (+13 −4), `tools/hygiene/tests/test_precommit.py` (+69 −7).

- `MERGE_SUBJECT` is now `^Merge (?:(?:remote-tracking )?branch(?:es)?|tags?|commits?) '` or `^Merge pull request #\d+ from `. That is every title `fmt-merge-msg` writes (`Merge <kind> '<name>'`, kinds joined by `, `, then ` of <url>` and ` into <branch>`), and GitHub's PR shape. The hand-written near misses (`Merge the thing`, `Merge branch main`, `Merge pull request from x`) stay rejected because the quote and the `#N from` are required.
- `commit_format` takes the first non-blank line as the subject whatever it starts with, drops `#` lines only after it (git's editor template), and returns `(1, "the message has no subject")` for an empty or all-blank message.
- Tests: `test_git_merge_commit_passes` now runs a real `git merge --no-ff --no-edit` for branch, branches (octopus), remote-tracking (local bare remote), tag and bare sha, plus the PR shape; `test_comment_like_or_empty_subject_is_rejected` (3 real commits); 13 exempt merge subjects and 5 more near misses in the unit tests; `test_commit_format_takes_the_first_non_blank_line_as_the_subject`.

### Finding A (merge subjects): PASS

Real merges in the throwaway repo, each `rc=0`, HEAD moved, the subject exactly what git wrote, the parent count right:

| Command | Subject stored | Parents |
|---|---|---|
| `git merge --no-ff --no-edit topic` | `Merge branch 'topic'` | 2 |
| `git merge --no-ff --no-edit origin/rt` (local bare remote) | `Merge remote-tracking branch 'origin/rt'` | 2 |
| `git merge --no-ff --no-edit v1` (lightweight tag) | `Merge tag 'v1'` | 2 |
| `git merge --no-ff --no-edit v2` (annotated tag; the tag message becomes the body) | `Merge tag 'v2'` + body | 2 |
| `git merge --no-ff --no-edit <40-char sha>` | `Merge commit '<sha>'` | 2 |
| `git merge --no-ff --no-edit o1 o2` | `Merge branches 'o1' and 'o2'` | 3 |
| on `feature`: `git merge --no-ff --no-edit main` | `Merge branch 'main' into feature` | 2 |
| conflicted merge, resolved, `git commit --no-edit` | `Merge branch 'cf'` | 2 |
| `-m "Merge pull request #7 from x/unity/y" -m "feat(x): add y"` | `Merge pull request #7 from x/unity/y` + body | 1 |

Rejected, HEAD unchanged, reason "must read type(scope): summary": `-m "Merge the thing"`, `-m "Merge branch main"`, `-m "Merge pull request from x"`, and a real `git merge --no-ff -m "Merge the thing" mm` ("Not committing merge", `MERGE_HEAD` left for a proper subject).

Trunk: the rule over `%B` of all 126 commits on `origin/unity/main` (`9c06593`) finds the same four long subjects as before and nothing else; all 18 merge commits are exempt.

### Finding B (comment-like subject): PASS

Real commits through the hook, each `rc=1`, HEAD unchanged:

| Command | Reason printed |
|---|---|
| `git commit --allow-empty-message -m "# just a comment"` | `commit message:1 (the subject must read type(scope): summary …)` |
| `git commit -m "#123 fix the thing"` | same, line 1 |
| `git commit --allow-empty-message -m ""` | `commit message:1 (the message has no subject)` |
| `git commit -m ""` (no flag) | same; the hook runs before git's own abort |
| `git commit -F <file>` with `# a comment above` on line 1 and `fix(x): thing` on line 2 | line 1 shape, line 2 "nothing may follow the subject" (git keeps the `#` line under `cleanup=whitespace`, so line 1 would have been the subject) |

Editor path still works: a fake `GIT_EDITOR` that writes `fix(x): editor path` above git's template and leaves the 8 template lines in place commits with no body (the hook ignores `#` lines after the subject). A `#` line typed above the subject in the editor is rejected, by the spec's "whatever it starts with"; git would have stripped it, so this is the accepted trade-off.

### Test suite

```
$ python -m pytest tools/hygiene -q
........................................................................ [ 17%]
........................................................................ [ 35%]
........................................................................ [ 53%]
........................................................................ [ 70%]
........................................................................ [ 88%]
..............................................                           [100%]
406 passed in 127.55s (0:02:07)
```

### Spec scenarios

Each run twice: as a real commit through the hook in the throwaway repo, and as `python tools/hygiene/precommit.py --commit-msg <file>` on this branch (rc and reason identical both ways).

| Scenario | Evidence | Result |
|---|---|---|
| Long subject is rejected | `fix(review): ` + 60 × `a` (73 chars) → `rc=1`, `commit message:1 (the subject is 73 characters, the limit is 72)`; `test_long_subject_is_rejected` | **PASS** |
| Wrong shape is rejected | `Added the thing`, `feature(x): thing` → shape reason; `fix(x): Thing` → `the summary must not start with an upper-case letter`; `test_wrong_shape_is_rejected` ×3 | **PASS** |
| Body or trailer is rejected | `-m "fix(x): thing" -m "Explains why."` and `… -m "Co-Authored-By: Someone <someone@example.invalid>"` → `commit message:3 (nothing may follow the subject …)`; `test_body_or_trailer_is_rejected` ×2 | **PASS** |
| Conforming subject passes | `fix(review): keep the bar in frame` → `rc=0`, committed, no output; `test_conforming_subject_passes` ×2 | **PASS** |
| Git merge commit passes | the nine real merges above; `test_git_merge_commit_passes` ×6; `test_commit_format_exempts_every_git_merge_subject` ×13; 18/18 trunk merges exempt | **PASS** |
| Comment-like subject is not a bypass | `# just a comment`, `#123 fix the thing`, `""`, `"\n\n"` → `rc=1`, line 1; `test_comment_like_or_empty_subject_is_rejected` ×3 | **PASS** |

### Hygiene

```
$ git log --format='%h %an <%ae> | %s' bd9e95e..HEAD
6c8516b Orchestrator <orchestrator@450fpv.local> | docs(openspec): align the commit-format proposal with the spec
e6ffafd QA Engineer <qa@450fpv.local> | docs(openspec): tick task 1.2 of enforce-commit-format
09a5aaa QA Engineer <qa@450fpv.local> | fix(hygiene): accept every git merge subject, close the comment bypass
507adc4 Orchestrator <orchestrator@450fpv.local> | docs(openspec): widen the merge and subject rules after review
4db0779 QA Engineer <qa@450fpv.local> | docs(openspec): review enforce-commit-format
261aa16 QA Engineer <qa@450fpv.local> | docs(openspec): tick task 1.1 of enforce-commit-format
6ab0949 QA Engineer <qa@450fpv.local> | docs(agents): say the commit-msg hook enforces the format
d243582 QA Engineer <qa@450fpv.local> | feat(hygiene): enforce the commit format in the commit-msg hook
$ git log --format='%s' bd9e95e..HEAD | awk '{ print length($0) }'
62 54 70 62 44 54 57 63
$ git log --format='%B' bd9e95e..HEAD | cat -A        # subject, blank, nothing else, all eight
$ git diff bd9e95e..HEAD --name-only | grep -iE '^reference/|Library/|/Temp/|/Logs/'   # nothing
$ git ls-files --eol .githooks/commit-msg tools/hygiene/precommit.py tools/hygiene/tests/test_precommit.py
i/lf    w/lf    attr/text eol=lf      	(all three)
$ python tools/hygiene/precommit.py --tree HEAD
324 files scanned, 0 findings
$ openspec validate enforce-commit-format --strict
Change 'enforce-commit-format' is valid
```

Each commit touches only the files its subject names (`09a5aaa`: the hook and its tests; `e6ffafd`: `tasks.md`; the orchestrator's two: proposal and spec). No OPSEC strings (tree scan clean; the new tests use `example.invalid` and a temp bare remote). No third-party files, so D-003 does not apply.

### Residuals, no change required

- **R1.** A `#`-leading line **after** the subject given with `-m`, `-F` or `--cleanup=verbatim` is stored as a body: `git commit -m "fix(x): thing" -m "#12 see the issue"` commits with body `#12 see the issue`, likewise `-m "#Co-Authored-By: …"` and an `-F` file with `# Conflicts:` lines. The hook cannot see the cleanup mode, and it must ignore `#` lines after the subject for every editor commit (above), so this is the price of the editor path; an indented ` # x` line is rejected. Only reachable on purpose (`--no-verify` is easier), and the realistic threats (a prose body, a real `Co-Authored-By:` trailer) are caught. Suggest one sentence in the requirement text, "`#` lines after the subject are git's editor template and are ignored", so the spec says what the code does. Optional.
- **R2.** `git pull <url>` with no branch named merges a remote HEAD, and `fmt-merge-msg` writes `Merge <url>` (no kind word, no quotes): rejected (length and shape). Not among the six shapes the spec lists and not a project flow (agents merge `origin/unity/main`; PRs merge on GitHub). Widen `MERGE_SUBJECT` only if that flow is ever wanted.
- **R3.** After a conflicted merge, `git commit --no-edit` stores git's own `# Conflicts:` / `#<tab>file` lines as the merge body (git keeps them under `cleanup=whitespace`; observed). The hook exempts the merge subject, so it passes; this is git's doing, like finding C. A line in `docs/workflow.md` ("after resolving, `git commit` with the editor or `--cleanup=strip`") would avoid it.
- **D** from the first review stands: a BOM-prefixed `-F` message is rejected with the shape reason (safe direction, misleading text). Optional.

### Requirements

- **PRC-03:** **met.** Every hand-written message is held to the format; git's own merge subjects pass; no `#` or empty bypass of the subject.
- **SEC-06:** **met.** The OPSEC line rules on the message run unchanged, the format findings are appended.
- **Spec requirement "Commit message format":** **met**, scenarios 6/6 pass.

Verdict: APPROVED
