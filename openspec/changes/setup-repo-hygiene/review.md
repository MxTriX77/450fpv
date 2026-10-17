# Review: setup-repo-hygiene

**Reviewer:** qa-engineer (a fresh agent, not the author) · **Task:** 2.1 · **Branch:** `unity/qa/setup-repo-hygiene` · **Range:** `13f598c..10d76dd` (7 commits, 8 files)
**Requirements cited:** SEC-01, SEC-02, SEC-06, PRC-10 · **Decision:** D-013 · **Lessons:** knowledge.md §14.2

QA re-ran every check below itself and did not use the author's output. The hand checks ran in throwaway repos under the session scratch folder (`<scratch>`), with `core.hooksPath` set to this worktree's `.githooks`. The real repo's `core.hooksPath` was not touched, and `reference/` was never opened. Messenger strings were built from the hook's encoded list and never printed. This review refers to them by list index only, and it writes footage-like names as patterns, never literally.

## Environment

```
$ python --version; python -m pytest --version
Python 3.14.6
pytest 9.1.1
$ git lfs version
git-lfs/3.7.1 (GitHub; windows amd64; go 1.25.1; git b84b3384)
$ Get-CimInstance Win32_Battery; powercfg /getactivescheme; PowerStatus; Win32_Processor
BatteryStatus=2 (2 = on AC) Charge=79%
Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
PowerLineStatus=Online
CPU=AMD Ryzen 7 7735HS with Radeon Graphics  CurrentClockMHz=2502 MaxClockMHz=3201
$ git config --get core.hooksPath        # the shared repo config of the dev machine
(empty) rc=1
```

## Test suite

```
$ python -m pytest tools/hygiene -q
........................................................................ [ 34%]
........................................................................ [ 69%]
................................................................         [100%]
208 passed in 16.50s
real 0m17.898s

$ python -m pytest tools/hygiene -q -s -k 200_files      # on AC, Balanced plan
200 staged files: scan 0.561, 0.371, 0.384 s; full commit through the hook 0.798 s
1 passed, 207 deselected in 4.78s
```
An earlier run gave 0.349, 0.348 and 0.379 s for the scan and 0.632 s for the full commit.

```
$ openspec validate setup-repo-hygiene --strict
Change 'setup-repo-hygiene' is valid
```

## Spec scenarios (hand-run, independent of the suite)

Script: `<scratch>/qa/scenarios.py`. Each case gets a fresh repo with a `.gitignore` for `reference/`, `*.mp4` and `*.mov`. The script writes the files, stages them (`-f` where the scenario says force-added), commits through the hook, and reports the exit code, whether `HEAD` exists and the hook's stderr. `hidden` strings (the planted name or value) are asserted absent from the output. The hook's two boilerplate lines (`pre-commit: blocked by the repo hygiene scan (SEC-06). Matched text is not printed.` and `Unstage or fix these. ...`) are trimmed from the paste below.

```
$ python <scratch>/qa/scenarios.py spec; python <scratch>/qa/scenarios.py spec-rest
[OK ] S1 force-added test clip (.mp4) at root: expect=block got=block rc=1 head=False
      |   footage-ext     *.mp4
[OK ] S2 png under docs: expect=block got=block rc=1 head=False
      |   media-location  docs/*.png
[OK ] S3 png under unity/Assets: expect=pass got=pass rc=0 head=True
[OK ] S4 encoded-list messenger in md: expect=block got=block rc=1 head=False
      |   messenger       docs/chat.md:3
[OK ] S5 open-ocean pair in docs: expect=block got=block rc=1 head=False
      |   coordinates     docs/spot.md:1
[OK ] S6 pilot date in reference-notes: expect=pass got=pass rc=0 head=True
[OK ] S7 clean commit: expect=pass got=pass rc=0 head=True
[OK ] S8 mov under unity/Assets: expect=block got=block rc=1 head=False
      |   footage-ext     unity/Assets/_Project/Art/*.mov
[OK ] S9 camera-style name (DJI_ + digits, .txt) in docs: expect=block got=block rc=1 head=False
      |   footage-name    docs/*.txt
[OK ] S10 camera-style name (VID_ + date_time, .md) at root: expect=block got=block rc=1 head=False
      |   footage-name    *.md
[OK ] S11 anything under reference: expect=block got=block rc=1 head=False
      |   reference-path  reference/...
[OK ] S12 cyrillic messenger upper case: expect=block got=block rc=1 head=False
      |   messenger       docs/a.md:2
[OK ] S13 hook's own sources: expect=pass got=pass rc=0 head=True
[OK ] S14 sortie date in object sheet: expect=block got=block rc=1 head=False
      |   notes-date      docs/object-sheets/fence.md:3
```
S5 uses the spec's exact open-ocean pair, and S4 uses Latin term #3 in title case. S6 is the spec's literal line, `(the pilot, 2026-10-17)` in `docs/reference-notes/x.md`.

| Requirement · Scenario | Result | Evidence |
|---|---|---|
| Footage never commits · Planted test clip is blocked (rule + masked location, nothing committed) | **PASS** | S1: `footage-ext *.mp4`, no `HEAD`, name not in output; suite `test_planted_mp4_is_blocked` |
| Footage never commits · Footage under unity/Assets is still blocked | **PASS** | S8; suite |
| Footage never commits · Camera-style file name is blocked | **PASS** | S9, S10 (both spec examples); suite, 21 name patterns |
| Footage never commits · Anything under reference is blocked | **PASS** | S11; B5 (upper-case `REFERENCE/`) too |
| Media only where assets live · Image in docs is blocked | **PASS** | S2 (`.png`), B3 (`.JPG`, nested); suite (`.png`, `.jpg`) |
| Media only where assets live · Texture in unity/Assets passes | **PASS** | S3 |
| No OPSEC strings in text · Messenger name is blocked (file and line named) | **PASS** | S4 (Latin), S12 (Cyrillic, upper case), P2 (Cyrillic, inflected); `docs/chat.md:3` |
| No OPSEC strings in text · Decimal coordinates in docs are blocked | **PASS** | S5 (the spec's exact pair) |
| No OPSEC strings in text · The hook's own blocklist does not trip the hook | **PASS** | S13; raw blocklist audit below, 0 hits |
| No dates in notes · Sortie-like date in an object sheet is blocked | **PASS** | S14 |
| No dates in notes · Decision date is allowed | **PASS** | S6 |
| Clean commits pass quickly · Ordinary code commit passes | **PASS** | S7 (`.cs`, `.py`, `.md`, with the word "signal" in the `.md`) |
| Clean commits pass quickly · scan < 2 s for 200 files (start-up excluded) | **PASS** | 0.35–0.56 s on AC (above); the whole commit through `sh` and `python` took 0.63–0.80 s |

**All 12 scenarios and the timing budget: PASS.**

Two side checks also passed. **Fail-closed:** a copy of the hook whose `../tools/hygiene/precommit.py` doesn't resolve blocks the commit (`rc=1`, seen in the first LFS probe). **Whole tree is clean:** the trial merge with `unity/main` gives no findings, so the hook won't block edits to any existing file:
```
$ git merge-tree --write-tree --name-only origin/unity/main HEAD
a0939fb33a93f896d7a2763a8fa0da585d942e1e          (rc=0, no conflicts)
$ python <scratch>/qa/scan_tree.py a0939fb       # the hook's own rules over every blob of that tree
129 entries scanned, 0 findings
```

## Break attempts

Expectation = what OPSEC needs (block a leak, pass legitimate text). `MISMATCH` = the hook does otherwise.

```
$ python <scratch>/qa/scenarios.py break
[OK ] B1 upper-case name and .MP4 extension at root: expect=block got=block rc=1 head=False
      |   footage-ext     *.MP4
[OK ] B2 .mkv five folders deep: expect=block got=block rc=1 head=False
      |   footage-ext     a/b/c/d/e/*.mkv
[OK ] B3 upper-case .JPG two folders deep in docs: expect=block got=block rc=1 head=False
      |   media-location  docs/x/y/*.JPG
[OK ] B4 wrong-case exempt root Unity/Assets/t.png: expect=block got=block rc=1 head=False
      |   media-location  Unity/Assets/*.png
[OK ] B5 upper-case REFERENCE/x.txt: expect=block got=block rc=1 head=False
      |   reference-path  REFERENCE/...
[MISMATCH] B6 renamed video docs/clip.dat (mp4 bytes): expect=block got=pass rc=0 head=True
[MISMATCH] B7 archive docs/clips.zip: expect=block got=pass rc=0 head=True
[MISMATCH] B8 messenger name in a file name: expect=block got=pass rc=0 head=True
[MISMATCH] B9 phone-edited name (IMG_E + digits, .txt): expect=block got=pass rc=0 head=True
[MISMATCH] B10 frame force-added under unity/Assets/reference/: expect=block got=pass rc=0 head=True
[MISMATCH] B11 sheet force-added under assets-src/_compare/: expect=block got=pass rc=0 head=True
[OK ] T1 coords in a fenced code block in docs: expect=block got=block rc=1 head=False
      |   coordinates     docs/a.md:2
[MISMATCH] T2 JSON lat/lng in docs: expect=block got=pass rc=0 head=True
[MISMATCH] T3 hemisphere-letter-prefixed pair in docs: expect=block got=pass rc=0 head=True
[MISMATCH] T4 decimal-comma pair in docs: expect=block got=pass rc=0 head=True
[MISMATCH] T5 three-decimal pair in docs: expect=block got=pass rc=0 head=True
[MISMATCH] T6 D-M-S pair with no hemisphere letter: expect=block got=pass rc=0 head=True
[MISMATCH] T7 coords in openspec/ (outside docs/): expect=block got=pass rc=0 head=True
[OK ] T8 maps-url @lat,lon,zoom in docs: expect=block got=block rc=1 head=False
      |   coordinates     docs/a.md:1
[MISMATCH] T9 camelCase messenger identifier in .cs: expect=block got=pass rc=0 head=True
[OK ] T10 snake_case messenger identifier in .py: expect=block got=block rc=1 head=False
      |   messenger       tools/a/b.py:1
[MISMATCH] T11 camera file name written in docs text: expect=block got=pass rc=0 head=True
[MISMATCH] T12 export file name written in openspec review: expect=block got=pass rc=0 head=True
[OK ] T13 export name in reference-notes (dated): expect=block got=block rc=1 head=False
      |   notes-date      docs/reference-notes/a.md:1
[MISMATCH] T14 sortie date next to the word pilot, in notes: expect=block got=pass rc=0 head=True
[MISMATCH] T15 slash date in notes: expect=block got=pass rc=0 head=True
[MISMATCH] T16 month-name date in notes: expect=block got=pass rc=0 head=True
[MISMATCH] T17 compact date in notes: expect=block got=pass rc=0 head=True
[MISMATCH] T18 lower-case MGRS in docs: expect=block got=pass rc=0 head=True
[MISS] T19 coords in the commit subject: rc=0 head=True
[MISS] T20 messenger name in the commit subject: rc=0 head=True
[OK ] F1 shader constants in docs: expect=pass got=pass rc=0 head=True
[MISMATCH] F2 drag coefficients pair in docs: expect=pass got=block rc=1 head=False
      |   coordinates     docs/a.md:1
[MISMATCH] F3 English noun that is a list term: expect=pass got=block rc=1 head=False
      |   messenger       docs/a.md:1
[OK ] F4 version and section numbers in notes: expect=pass got=pass rc=0 head=True
[OK ] F5 coords pattern outside docs (C# test data): expect=pass got=pass rc=0 head=True
[OK ] F6 table of 4-decimal values in docs: expect=pass got=pass rc=0 head=True
[OK ] P1 clean file, spaces + Cyrillic in path: expect=pass got=pass rc=0 head=True
[OK ] P2 same path, encoded-list Cyrillic term inside: expect=block got=block rc=1 head=False
      |   messenger       docs/нотатки з польоту.md:1
[MISMATCH] P3 UTF-16 LE without BOM, Latin term: expect=block got=pass rc=0 head=True
```

**Caught (14):**
- **Paths:** upper-case extensions, deep nesting, a wrong-case exempt root (correctly stricter) and an upper-case `REFERENCE/` (B1–B5).
- **Coordinates:** a pair inside a code fence, and a map URL `@lat,lon,zoom` (T1, T8).
- **Messenger names:** a snake_case identifier (T10).
- **Dates:** an export name carrying an ISO date, inside the notes folders (T13).
- **Path encoding:** a path with spaces and Cyrillic (P1 and P2).
- **No false positives:** shader constants, version and section numbers, a table of 4-decimal values, and coordinate-like test data outside `docs/` all pass (F1, F4, F5, F6).

**Does each miss matter for OPSEC?**

| Miss | Matters? | Why |
|---|---|---|
| **T11, T12:** a camera or export file name written *inside* text (a doc, an openspec `review.md`) | **Yes, high. Required change R1** | These are slips 1 and 2 of knowledge.md §14.2, the reason this change exists ("three OPSEC slips that a hook would have stopped"). SEC-01 says "never write reference file names anywhere", and its *Accept* line points at this scan. Only the notes folders catch one, and only when the name embeds an ISO date and the line lacks "pilot" (T13). Camera names without a separated date (`DJI_` + digits) pass everywhere |
| **T14:** a sortie date on a line that mentions the pilot | **Yes, medium** (spec-sanctioned, Suggestion 1) | The excuse is any whole word "pilot". On this branch 201 of 1,994 non-blank note lines contain it (33 of 105 in `pilot-requirements.md`), and the date rule is off on all of them |
| **T19, T20:** OPSEC strings in a commit subject | **Yes, medium** (Suggestion 2) | Subjects are public and permanent, and the pilot chose not to rewrite history. No `commit-msg` hook exists |
| **T7:** coordinates in `openspec/` (and root `*.md`) | Medium (Suggestion 3) | Reviews and designs live there and are just as public; slip 2 was in a `review.md` |
| **T2, T3, T4, T6, T18:** labelled JSON `lat`/`lng`, hemisphere-prefixed, decimal comma, D-M-S without a letter, lower-case MGRS | Medium-low (Suggestion 4) | T2 is how a value pasted from a map API looks; T4 is the Ukrainian decimal separator. The rest are rarer |
| **T15–T17:** slash, month-name and compact dates in notes | Medium-low (Suggestion 5) | Month-name dates are the natural form in English prose |
| **T5:** three-decimal pairs | Low; accepted by design (≥ 4 decimals) | Lowering the threshold would hit ordinary measurements |
| **B8, T9:** a messenger name in a file name, or in a camelCase identifier | Low (Suggestion 6) | Public all the same, but unlikely by accident |
| **B6, B7, B9, B10, B11, P3:** a renamed video, an archive, `IMG_E` names, nested `reference/` or `_compare/` folders, BOM-less UTF-16 | Low (Suggestion 6) | Deliberate evasion or rare. `.gitignore` already ignores `**/reference/`, so B10 needs `-f` |
| **F2, F3 (false positives):** a pair of 4-decimal coefficients in 1–90 in `docs/`; list term #7, which is also an ordinary English noun | Low (Suggestion 7) | The rule says to fix false positives in the rule. Neither occurs in the tree today (0 findings) |

**R1 is feasible with a small blast radius.** `<scratch>/qa/r1_probe.py` runs two candidate text rules over the trial-merge tree:
- (a) the existing `FOOTAGE_NAME` pattern at a token start
- (b) `<stem>.<video ext>` tokens

The only hits are in this change's own spec, tasks and tests, and in one line of `docs/briefs/phase-0-setup.md`:
```
camera/export name: 9 hits   spec.md:20, test_precommit.py:80,170,231-235,343
<stem>.<video ext>: 9 hits   docs/briefs/phase-0-setup.md:64, spec.md:11,12, tasks.md:5, test_precommit.py:71,76,225,343,344
```

## Git LFS and `core.hooksPath` (found while probing)

`docs/environment.md` on `unity/main` runs `git lfs install --local` and then `git config core.hooksPath .githooks`. Once `hooksPath` is set, git stops running the LFS hooks in `.git/hooks`. The first LFS command in a clone then writes git-lfs's four shims into the hooks path, which is the tracked `.githooks/` folder of that worktree:

```
$ python <scratch>/qa/lfs_steps.py env-doc     # scratch mirror of .githooks + tools/hygiene
  after lfs install --local          hooks dir: ['pre-commit']
  after config core.hooksPath        hooks dir: ['pre-commit']
  after git add (clean filter)       hooks dir: ['post-checkout', 'post-commit', 'post-merge', 'pre-commit', 'pre-push']
  after git commit (rc=0)            hooks dir: [... same five ...]
  after git push (rc=0)              hooks dir: [... same five ...]
  LFS objects on the remote: 1
```
This actually happened to this review's worktree in the first probe: `git status --short` showed four untracked files, `?? .githooks/post-checkout`, `post-commit`, `post-merge` and `pre-push`. QA deleted them. Without such a shim, pushes don't upload LFS objects at all:
```
$ python <scratch>/qa/lfs_push.py       # GIT_TRACE on push, bare file:// remote
[default]   trace: run_command: .git/hooks/pre-push ... exec: git-lfs pre-push ...   LFS objects on the remote: 1
[hookspath] trace: run_command: <worktree>/.githooks/pre-push ... exec: git-lfs pre-push   LFS objects on the remote: 1   (the auto-written shim)
[nohook]    (no pre-push hook)                                                            LFS objects on the remote: 0
```
**What this means once the hook is activated:**
- Every worktree that touches LFS files (all of phase 1) gets four untracked files under `.githooks/`.
- Such a worktree is no longer "unchanged", so it is likely kept rather than auto-cleaned.
- An agent may stage those files to "clean up".
- A clone that pushes LFS commits without ever running an LFS command locally uploads no objects.

This is required change R2.

## Design conformance

- **Decision 1, thin shell hook:** met. Three lines, `exec python …/precommit.py`, so the exit code passes through.
- **Decision 2, scan the index:** met. It uses `git diff --cached --raw -z --no-renames --diff-filter=ACMRT` plus one `git cat-file --batch`, rather than the design's `--name-only` with `git show :path`. The blob ids are the same, the call is faster and it adds `T`. The design text is slightly stale; that doesn't matter.
- **Decision 3, the rules:** all seven are present.
  - The footage extensions match the design's 19 exactly.
  - The name patterns are a superset: they add `DJIG`, `GH`, a messenger's `-WA` exports and bare `YYYYMMDD_HHMMSS`.
  - Coordinate pairs with both |values| < 1 are exempt (shader constants). That trade-off is documented in the code.
- **Decision 4, messages:** met. The output gives the rule id, a masked path for path rules (`*.mp4`, `reference/...`), and `path:line` for text rules. In every case the hidden strings were absent from the output.
  - Caveat: text findings print the full path, so a sensitive word in a file or folder name would show.
  - On Windows, non-ASCII paths print as `\u` escapes (P2), because stderr is cp1252. That's cosmetic.
- **Decision 5, tests:** met. They use real commits in `tmp_path` with `core.hooksPath` set to the real `.githooks`, and the 200-file timing is included.
- **Risk "hook not active in a fresh clone":** `core.hooksPath` is **unset** on the dev machine (checked above). That is expected before the merge, but no task covers activation (see after-merge item A1).

## Hygiene

```
$ git log --format='%h | %an <%ae> | %cn <%ce> | %s | body_len=%b' 13f598c..HEAD | awk -F'body_len=' '{print $1 "body_chars=" length($2)}'
10d76dd | Orchestrator <orchestrator@450fpv.local> | Orchestrator <orchestrator@450fpv.local> | docs(notes): drop a build date from pilot requirements | body_chars=0
4402e30 | Orchestrator <orchestrator@450fpv.local> | Orchestrator <orchestrator@450fpv.local> | docs(openspec): align repo-hygiene spec with the reviewed design | body_chars=0
922ba1a | QA Engineer <qa@450fpv.local> | QA Engineer <qa@450fpv.local> | docs(openspec): tick setup-repo-hygiene task 1.1 | body_chars=0
f7ccebf | QA Engineer <qa@450fpv.local> | QA Engineer <qa@450fpv.local> | test(hygiene): keep the blocklist out of test strings | body_chars=0
72781d2 | QA Engineer <qa@450fpv.local> | QA Engineer <qa@450fpv.local> | test(hygiene): cover every repo-hygiene scenario | body_chars=0
ffdd76b | QA Engineer <qa@450fpv.local> | QA Engineer <qa@450fpv.local> | feat(hygiene): add pre-commit opsec scan | body_chars=0
bff8db8 | QA Engineer <qa@450fpv.local> | QA Engineer <qa@450fpv.local> | chore(git): keep hook scripts lf | body_chars=0
$ git log --format='%h %(trailers:only)' 13f598c..HEAD     # all empty
$ git log --format='%s' 13f598c..HEAD | awk '{print length($0)}'
54 64 48 53 48 40 32
$ git diff --name-only 13f598c..HEAD | grep -E '^(reference/|unity/(Library|Temp|Logs|UserSettings)/)'
(no output) rc=1
$ git ls-files --eol -s .githooks/pre-commit tools/hygiene/precommit.py tools/hygiene/tests/test_precommit.py
100755 0ff40965… 0  i/lf  w/lf  attr/text eol=lf  .githooks/pre-commit
100644 0997430f… 0  i/lf  w/lf  attr/text eol=lf  tools/hygiene/precommit.py
100644 ae480e19… 0  i/lf  w/lf  attr/text eol=lf  tools/hygiene/tests/test_precommit.py
$ python <scratch>/qa/blocklist_audit.py     # raw case-insensitive substring, stricter than the hook
32 terms decoded (20 Latin, 12 Cyrillic); empty terms: 0
  .gitattributes, .githooks/pre-commit, docs/reference-notes/pilot-requirements.md,
  design.md, spec.md, tasks.md, tools/hygiene/precommit.py, tools/hygiene/tests/test_precommit.py: 0 raw hits each
  commit subjects 13f598c..HEAD: 0 raw hits
```
- **Commits:** subject-only, `type(scope): summary`, lower case, at most 72 characters, no trailers and no co-author line. The identities match `docs/team.md`. Each commit touches only its own files (`--name-status`), which is consistent with explicit staging.
- **Blocklist in history:** the only raw near-miss was in the earlier test file: a method call on the generic word (a vocabulary test line) that contains a domain term from the list as a substring. `f7ccebf` split that string, and it was never a messenger mention.
- **OPSEC in the diff:**
  - The coordinates are open ocean (South Pacific and South Atlantic, plus MGRS zone 25G).
  - The test dates are neutral, and the only real dates are pilot-statement and decision dates.
  - `10d76dd` removed a build date from a notes line that the new rule would otherwise have blocked on its next edit.
- **Licences:** no third-party files and no dependencies beyond the standard library and pytest.

## Requirements

- **SEC-06: partly met.**
  - Met: media outside asset roots, footage extensions and names of staged files, messenger names in text, and coordinates and dates in their specified formats.
  - Missed: footage-like names written inside text (T11, T12), and commit subjects (T19, T20).
- **SEC-01: partly met.** Files under `reference/` and footage files can't commit. The rule "never write reference file names anywhere", whose *Accept* line is this scan, is not enforced (T11, T12).
- **SEC-02: partly met** (as a backstop). ISO and dotted dates in notes and decimal, D-M-S and MGRS coordinates in `docs/` are blocked. The pilot-word excuse and several common formats let dates and coordinates through (T2–T7, T14–T18).
- **PRC-10: met** by this review (commands and output above). The author's handoff wasn't part of this brief, so QA did not judge it.

## Required changes

**R1. Block footage-like file names written inside staged text.**
- **Spec first (orchestrator):** add a scenario such as "WHEN a staged text file contains a camera or messenger-export file name (the `footage-name` patterns, or any `<name>.<video extension>` token) THEN the commit is rejected, and the message names the file and line, not the name".
- **Rule:** a line rule (for example `footage-name-text`). It reuses `FOOTAGE_NAME` with a token-start guard, plus `<stem>.<video ext>` tokens.
- **Scope:** all staged text, or at least `docs/` and `openspec/`, because slip 2 was a `review.md`.
- **Tests:** they build such names from parts, as they already do for messenger names. The spec's own examples (`spec.md:11,12,20`, `tasks.md:5`) and `docs/briefs/phase-0-setup.md:64` are rephrased as patterns (r1_probe hits above).
- **Optional, the orchestrator's call:** also match the base names actually present in the local, git-ignored `reference/` folder. The hook would read the names only, keep them in memory and never print them. That gives exact matches and zero false positives, but it means the hook lists that folder.

**R2. Commit git-lfs's hook shims into `.githooks/`.**
- **What:** `pre-push`, `post-checkout`, `post-commit` and `post-merge`, LF, mode 100755, byte-identical to the four files git-lfs 3.7.1 installs (as in the main checkout's `.git/hooks/`, or as it wrote them in the probe above).
- **Why:** activating `core.hooksPath` then leaves trees clean, and LFS uploads never depend on which local command ran first.
- **Test:** each shim exists, is LF and calls `git lfs <hook>`.
- **Design:** one line under Decisions or Risks.

**After merge (orchestrator, not a branch change):**
- **A1:** set `core.hooksPath .githooks` on the dev machine (it is unset now). Then show in a real worktree that a planted test clip is blocked: plan.md phase 0 "Done means". Consider adding this as task 3.1 so it can't be forgotten.

## Suggestions (non-blocking, for the owner or orchestrator)

1. **Tighten the pilot-word date excuse (T14).**
   - Today: any line with the whole word "pilot" may carry any date.
   - Every existing dated note line has the date right after an attribution: `pilot, <date>`, `pilot said** (<date>`, `pilot** (<date>`, or a `D-###`. The one exception is a "read <date>" source line in `fc-profiles.md`, which can cite its decision.
   - An excuse anchored to those forms keeps the other 11 existing lines legal and blocks "the pilot flew this clip on <date>".
   - This needs a spec edit, because `4402e30` chose the loose form.
2. **Add a `commit-msg` hook (T19, T20).** It would run the text rules (messenger, coordinates, R1's names) on the subject line. Commit subjects are the most permanent public text in the repo.
3. **Widen the coordinate scope (T7)** to `openspec/` and root `*.md` files.
4. **Add coordinate formats (T2, T3, T4, T6, T18):**
   - labelled pairs (`lat`/`lng`/`lon` keys with up to a few characters between)
   - hemisphere letters before the number
   - decimal commas with `;` or whitespace separators
   - D-M-S pairs without a letter
   - case-insensitive MGRS
5. **Add date formats in notes (T15–T17):** `dd/mm/yyyy`, `mm/dd/yyyy`, `yyyy.mm.dd`, compact `20yymmdd`, and English and Ukrainian month names with a year.
6. **Low-priority path and content gaps:**
   - messenger names in file paths (B8)
   - a camelCase boundary for messenger names (T9)
   - `IMG_E` names (B9)
   - magic-byte sniffing for video and archive containers in files that aren't otherwise media (B6, B7)
   - BOM-less UTF-16 detection (P3)
   - a `reference` or `_compare` path component below the root (B10, B11). It must not catch `docs/reference-notes/` or the planned `tools/reference/`.
7. **False positives (F2, F3):**
   - Move list term #7, an ordinary English noun, to unambiguous forms only (a domain or "... server"), as was done for the generic word.
   - Accept F2. A unit or label between the numbers already avoids it.
8. **Cosmetics:**
   - `sys.stderr.reconfigure(encoding="utf-8", errors="backslashreplace")`, so Cyrillic paths print readably (P2).
   - Align `test_decimal_coordinates_in_docs_are_blocked` with the spec's current example pair (S5 proves that pair by hand).
9. **Outside this change (heads-up for `setup-reference-tools`):** `.gitignore`'s `**/reference/` also ignores the planned `tools/reference/`:
   ```
   $ git check-ignore -v tools/reference/extract_frames.py
   .gitignore:3:**/reference/	tools/reference/extract_frames.py
   ```
   That change needs a `!tools/reference/` negation, with the hook still blocking the top-level `reference/`.

Verdict: CHANGES REQUESTED

---

# Re-review (task 2.1, after the fixes for R1 and R2)

**Reviewer:** qa-engineer (a fresh agent: not the author of the fixes, not the first reviewer) · **Range:** `f06bd33..d5f07aa` (9 commits: spec revisions `3523223`, `ed70009`, `d5f07aa`; fixes `d4ec7eb..51cdbc9`; 12 files) · **Contract:** the revised `specs/repo-hygiene/spec.md` at `d5f07aa`

Every check below was re-run by this agent; nothing was taken from the author's handoff or the first review. Hand checks ran in throwaway repos under the session scratch folder (`<scratch>/qa/cases/`), with `core.hooksPath` set to this worktree's `.githooks` (absolute) unless a case says otherwise. This agent never opened the real `reference/`. Every planted name, value and term was built at run time from parts and is referred to here by pattern or list index only. The dev machine's `core.hooksPath` is still unset and was not touched.

## Environment

```
$ python --version; python -m pytest --version; git --version; git lfs version
Python 3.14.6 / pytest 9.1.1 / git version 2.55.0.windows.3 / git-lfs/3.7.1 (GitHub; windows amd64; go 1.25.1)
$ Win32_Battery: BatteryStatus=2 (on AC) Charge=75%; PowerLineStatus=Online; scheme Balanced
  AMD Ryzen 7 7735HS, CurrentClockSpeed 2502 MHz, MaxClockSpeed 3201 MHz
$ git config --get core.hooksPath        # shared repo config, unchanged
(empty) rc=1
$ openspec validate setup-repo-hygiene --strict
Change 'setup-repo-hygiene' is valid
```

## Test suite

```
$ time python -m pytest tools/hygiene -q
.............................................F.......................... [ 20%]
........................................................................ [ 41%]
........................................................................ [ 61%]
........................................................................ [ 82%]
...............................................................          [100%]
FAILED tools/hygiene/tests/test_precommit.py::test_existing_tree_is_clean[origin/unity/main merged with HEAD]
  AssertionError: 244 files scanned, 13 findings   (all footage-text, openspec/changes/setup-reference-tools/review.md)
1 failed, 350 passed in 73.36s
real 1m14.762s
```
The one failure is outside the author's diff: the trunk moved during this review (see "Zero findings over the tree"). The timing test, run alone three times afterwards (AC, Balanced, nothing else running):
```
$ python -m pytest tools/hygiene -q -s -k 200_files
200 staged files: scan 0.778, 0.830, 0.774 s; full commit through both hooks 2.124 s
200 staged files: scan 0.738, 0.721, 0.704 s; full commit through both hooks 1.918 s
200 staged files: scan 0.754, 0.697, 0.711 s; full commit through both hooks 1.902 s
```
The scan figure includes the Python start-up and the walk of a planted 1,000-file `reference/`; the spec's 2 s budget excludes start-up. Within budget.

## Zero findings over the tree

```
$ python tools/hygiene/precommit.py --tree HEAD
119 files scanned, 0 findings                                    rc=0
$ git merge-tree --write-tree origin/unity/main HEAD              # origin/unity/main = 78e1aac when this review began
1f9b467fa602a4adcfad286d586203c9378c6a64
$ python tools/hygiene/precommit.py --tree 1f9b467fa602a4adcfad286d586203c9378c6a64
237 files scanned, 0 findings                                    rc=0
```

**The trunk moved while the suite ran.** PR #39 (`setup-reference-tools`, trunk now `6f990e5`) landed `openspec/changes/setup-reference-tools/review.md`, and 13 of its lines hold `<name>.<video extension>` tokens:
```
$ git reflog show origin/unity/main -2
6f990e5 refs/remotes/origin/unity/main@{0}: pull -q --ff-only: fast-forward
78e1aac refs/remotes/origin/unity/main@{1}: pull --ff-only origin unity/main: fast-forward
$ git merge-tree --write-tree origin/unity/main HEAD              # with 6f990e5
95e3e4dbf18da59e40994a56a8510a143b773e39
$ python tools/hygiene/precommit.py --tree 95e3e4dbf18da59e40994a56a8510a143b773e39
  footage-text        openspec/changes/setup-reference-tools/review.md:5
  footage-text        openspec/changes/setup-reference-tools/review.md:27
  footage-text        openspec/changes/setup-reference-tools/review.md:35
  footage-text        openspec/changes/setup-reference-tools/review.md:85   (and 86, 87, 117, 129, 130, 131, 132, 133, 135)
244 files scanned, 13 findings                                   rc=1
$ python tools/hygiene/precommit.py --tree origin/unity/main
235 files scanned, 21 findings                                   rc=1
```
The 21 on the trunk alone are those 13 plus 8 lines this branch already rewords (`spec.md:11,12,20,46`, `tasks.md:5`, `docs/briefs/phase-0-setup.md:64`, `docs/reference-notes/fc-profiles.md:3`, `docs/reference-notes/pilot-requirements.md:24`), so after the merge exactly 13 remain. QA read those 13 lines: every token is the name of a synthetic ffmpeg `testsrc` probe clip (an invented word plus a video extension), or a `git check-ignore` probe path of the same shape. **No real reference name is on the trunk.** For OPSEC they are false positives, but the revised spec blocks every `<name>.<video extension>` token in staged Markdown on purpose (slip 1 of knowledge.md §14.2 was a real name of exactly that shape), so the lines must change, not the rule. If the branch merged as is:
- `python -m pytest tools/hygiene` is red on `unity/main` at once (`test_existing_tree_is_clean[HEAD]` sees the 13 findings).
- Once `core.hooksPath` is active, `setup-reference-tools` cannot be archived: `--no-renames` lists a folder move as an add, and the content is scanned. Proved in a throwaway repo, with such a token committed while hooks were off and the folder then moved with hooks on:
```
$ python <scratch>/qa/rename_probe.py
index as the hook lists it: [':000000 100644 0000000 21194c9 A openspec/changes/archive/2026-10-18-x/review.md']
archive commit rc=1 committed=False token_in_output=False
  |   footage-text        openspec/changes/archive/2026-10-18-x/review.md:3
```

## Spec scenarios, hand-run

Script: `<scratch>/qa/rr_scenarios.py` (73 cases, 3 min 50 s). Each case gets a fresh repo (a `.gitignore` for `reference/`, `*.mp4`, `*.mov`), writes and stages the planted files (`-f` where the scenario says force-added) and commits once through both hooks. The script checks the exit code, whether `HEAD` moved, the rule id and `path:line` in stderr, and that every hidden string (the planted name, stem, value or term) is absent from the output. The hook's two boilerplate lines are trimmed from the paste.

```
$ python <scratch>/qa/rr_scenarios.py spec
[OK ] S1 force-added test clip (.mp4) at root: expect=block got=block rc=1 committed=False
      |   footage-ext         *.mp4
[OK ] S2 .mov under unity/Assets: expect=block got=block rc=1 committed=False
      |   footage-ext         unity/Assets/_Project/Art/*.mov
[OK ] S3a camera-style file name (maker prefix + frame no., .txt) in docs: expect=block got=block rc=1 committed=False
      |   footage-name        docs/*.txt
[OK ] S3b phone-style file name (phone prefix + date and time, .md) at root: expect=block got=block rc=1 committed=False
      |   footage-name        *.md
[OK ] S4 any file under reference/ force-added: expect=block got=block rc=1 committed=False
      |   reference-path      reference/...
[OK ] S5a R1: camera-style name built from parts in openspec/.../review.md: expect=block got=block rc=1 committed=False
      |   footage-text        openspec/changes/x/review.md:3
[OK ] S5b R1: messenger-export name in openspec/.../review.md: expect=block got=block rc=1 committed=False
      |   footage-text        openspec/changes/x/review.md:3
[OK ] S5c R1: <name>.<video ext> token in a doc: expect=block got=block rc=1 committed=False
      |   footage-text        docs/notes.md:3
[OK ] S6a R1: fake reference/ with a distinctive media stem, stem in a note: expect=block got=block rc=1 committed=False
      |   reference-stem      docs/reference-notes/wind.md:3
[OK ] S6b R1: same stem, different case, in a .cs file: expect=block got=block rc=1 committed=False
      |   reference-stem      tools/a/notes.cs:1
[OK ] S7a R1: <name>.<video ext> in the commit message: expect=block got=block rc=1 committed=False
      |   footage-text        commit message:1
[OK ] S7b R1: reference stem in the commit message: expect=block got=block rc=1 committed=False
      |   reference-stem      commit message:1
[OK ] S8a .gitignore with bare *.mp4 and **/*.MOV patterns: expect=pass got=pass rc=0 committed=True
[OK ] S8b doc with `*.mp4`, `*.mkv` and 'a .webm file': expect=pass got=pass rc=0 committed=True
[OK ] S9 .png under docs: expect=block got=block rc=1 committed=False
      |   media-location      docs/*.png
[OK ] S10 .png under unity/Assets/_Project/Art: expect=pass got=pass rc=0 committed=True
[OK ] S11a messenger term #0 upper case in md: expect=block got=block rc=1 committed=False
      |   messenger           docs/chat.md:3
[OK ] S11b Cyrillic messenger term #0 title case, inflected, in md: expect=block got=block rc=1 committed=False
      |   messenger           docs/chat.md:3
[OK ] S12 7 ordinary words that are also messenger names, bare: expect=pass got=pass rc=0 committed=True
[OK ] S13a regional decimal pair in docs/: expect=block got=block rc=1 committed=False
      |   coordinates         docs/place.md:1
[OK ] S13b regional decimal pair in openspec/ (T7): expect=block got=block rc=1 committed=False
      |   coordinates         openspec/changes/x/design.md:1
[OK ] S13c regional decimal pair in a root .md: expect=block got=block rc=1 committed=False
      |   coordinates         NOTES.md:1
[OK ] S14 JSON lat key with a number (T2): expect=block got=block rc=1 committed=False
      |   labelled-coordinate tools/map/points.json:3
[OK ] S15 physics coefficient pairs outside the box in a design (F2): expect=pass got=pass rc=0 committed=True
[OK ] S16 the hook's own eight source files staged: expect=pass got=pass rc=0 committed=True
[OK ] S17 spec's sortie date in an object sheet: expect=block got=block rc=1 committed=False
      |   notes-date          docs/object-sheets/fence.md:3
[OK ] S18 spec's 'the pilot flew there on 14 March 2025' (T14): expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] S19 spec's '(the pilot, 2026-10-17)': expect=pass got=pass rc=0 committed=True
[OK ] S22 ordinary .cs, .py, .md commit: expect=pass got=pass rc=0 committed=True
```
S6a/S6b/S7b plant a fake `reference/` holding one `.mp4` whose stem is 13 characters with hyphens and a digit (built at run time); the stem was absent from every output. S12 and F3 use the 7 list heads that are stored only in unambiguous forms, in three forms each (bare, title case, plural), and pass. S16 stages the six hooks, the scan and its tests. Both hooks also fire through the relative `core.hooksPath .githooks` that `docs/environment.md` prescribes, with the hooks tracked in the repo:
```
$ python <scratch>/qa/relative_hookspath.py
pre-commit via relative hooksPath: rc=1 committed=False
  | pre-commit: blocked by the repo hygiene scan (SEC-06). Matched text is not printed.
  |   media-location      docs/*.png
commit-msg via relative hooksPath: rc=1 committed=False token_in_output=False
  | commit-msg: blocked by the repo hygiene scan (SEC-06). Matched text is not printed.
  |   footage-text        commit message:1
```

### Git LFS (R2)

Three runs of the `docs/environment.md` set-up (`git lfs install --local`, then `core.hooksPath`), a 16 KiB `*.bin` tracked by LFS, a bare `file://` remote, and `GIT_TRACE=1` on the push:
```
$ python <scratch>/qa/rr_scenarios.py lfs
== R2 LFS [tracked] hooksPath=.githooks                     # the six hooks and the scan committed in the repo
      lfs install --local: rc=0 / config core.hooksPath: rc=0 / add hooks+attrs: rc=0 / commit hooks: rc=0
      add data.bin: rc=0 / commit data.bin: rc=0 / remote add: rc=0 / push: rc=0
      HEAD:data.bin is an LFS pointer: True
      untracked under .githooks after commit: ''
      pre-push trace: ['... run_command: .githooks/pre-push origin file:///...', '... run_command: git-lfs pre-push origin file:///...']
      LFS object on the bare remote: True  (remote.git/lfs/objects/a1/f2/a1f259d4365...)
      git status after push: ''
      this worktree's .githooks untracked: ''
== R2 LFS [worktree] hooksPath=C:/.dev/450fpv/.claude/worktrees/<agent>/.githooks    # this worktree's hooks
      (same eight steps rc=0)
      HEAD:data.bin is an LFS pointer: True
      untracked under .githooks after commit: ''
      pre-push trace: ['... run_command: C:/.dev/450fpv/.claude/worktrees/<agent>/.githooks/pre-push ...', '... run_command: git-lfs pre-push ...']
      LFS object on the bare remote: True
      git status after push: ''
      this worktree's .githooks untracked: ''
== R2 LFS [control] hooksPath=.githooks                     # same, with the four shims left out
      untracked under .githooks after commit: '?? .githooks/post-checkout\n?? .githooks/post-commit\n?? .githooks/post-merge\n?? .githooks/pre-push'
      LFS object on the bare remote: True   (git-lfs wrote its own shims into the tracked folder, as the first review found)
      git status after push: '?? .githooks/post-checkout\n?? .githooks/post-commit\n?? .githooks/post-merge\n?? .githooks/pre-push'
```
The four shims are byte-identical to what git-lfs 3.7.1 itself prints (`git lfs update --manual`, compared by eye for all four bodies, and by the suite's `test_lfs_shims_match_what_the_installed_git_lfs_writes`).

### Scenario table

| Requirement · Scenario | Result | Evidence |
|---|---|---|
| Footage never commits · Planted test clip is blocked | **PASS** | S1: rule id, masked `*.mp4`, no `HEAD`, name absent |
| · Footage under unity/Assets is still blocked | **PASS** | S2 |
| · Camera-style file name is blocked | **PASS** | S3a, S3b; suite, 21 name patterns |
| · Anything under reference is blocked | **PASS** | S4 |
| No footage names inside text · Camera-style name written in a review is blocked | **PASS** | S5a (camera), S5b (messenger export), T12: `openspec/changes/x/review.md:3`, name absent |
| · Reference file stem in a note is blocked | **PASS** | S6a (note), S6b (`.cs`, other case), N7 (linked worktree, `reference/` only in the main checkout), N9 (UTF-16), N13 (image stem), N14 (non-media stem with a digit); stem absent from every output |
| · Footage name in a commit message is blocked | **PASS** | S7a (token), S7b (stem), N6 (upper-case extension on line 3 → `commit message:3`), T19, T20 |
| · Extension patterns pass | **PASS** | S8a (`.gitignore` itself), S8b |
| Media only where assets live · Image in docs is blocked | **PASS** | S9 |
| · Texture in unity/Assets passes | **PASS** | S10 |
| No OPSEC strings in text · Messenger name is blocked | **PASS** | S11a (Latin, upper), S11b (Cyrillic, title, inflected), N10 (`.yaml`, `.json`) |
| · Ordinary word that is also a messenger name passes | **PASS** | S12, F3 |
| · Regional decimal coordinates are blocked | **PASS** | S13a `docs/`, S13b `openspec/`, S13c root `.md` |
| · Labelled coordinate in JSON is blocked | **PASS** | S14, T2; N8 (`.cs`) |
| · Physics coefficients pass | **PASS** | S15, F2 |
| · The hook's own blocklist does not trip the hook | **PASS** | S16; raw audit under Hygiene, 0 hits |
| No dates in notes · Sortie-like date in an object sheet is blocked | **PASS** | S17 |
| · Date on a line that merely mentions the pilot is blocked | **PASS** | S18 (the spec's line), T14, T14b |
| · Attributed decision date is allowed | **PASS** | S19, N5 |
| Git LFS keeps working · Push uploads LFS objects | **PASS** | `[tracked]` and `[worktree]`: object on the bare remote, trace shows `pre-push` → `git-lfs pre-push` |
| · No untracked hook files appear | **PASS** | `git status --porcelain --untracked-files=all -- .githooks` empty after commit and after push; the control shows the four `??` files the shims prevent |
| Clean commits pass quickly · Ordinary code commit passes | **PASS** | S22; N18 |
| · scan under 2 s for 200 files | **PASS** | 0.70–0.83 s including start-up and a 1,000-file `reference/` |
| · Existing tree is clean | **FAIL** (since PR #39) | 0 findings on HEAD and on the merge with `78e1aac`; 13 on the merge with the current trunk `6f990e5`, all in `setup-reference-tools/review.md` |

## First review's break table, re-run

Every miss the revised spec now covers, and both false positives:
```
$ python <scratch>/qa/rr_scenarios.py break
[OK ] T2 JSON lat/lng pair in docs: expect=block got=block rc=1 committed=False
      |   labelled-coordinate docs/a.json:1
[OK ] T3 hemisphere-prefixed pair in docs: expect=block got=block rc=1 committed=False
      |   coordinates         docs/a.md:1
[OK ] T3b hemisphere-prefixed single value in docs: expect=block got=block rc=1 committed=False
      |   coordinates         docs/a.md:1
[OK ] T4 decimal-comma pair in docs: expect=block got=block rc=1 committed=False
      |   coordinates         docs/a.md:1
[OK ] T4b decimal-comma pair, space separated: expect=block got=block rc=1 committed=False
      |   coordinates         docs/a.md:1
[OK ] T6 D-M-S pair with no hemisphere letter in docs: expect=block got=block rc=1 committed=False
      |   coordinates         docs/a.md:1
[OK ] T6b D-M-S with ASCII quotes and no letter: expect=block got=block rc=1 committed=False
      |   coordinates         docs/a.md:1
[OK ] T7 decimal pair in openspec/ (not docs/): expect=block got=block rc=1 committed=False
      |   coordinates         openspec/changes/y/proposal.md:1
[OK ] T11 camera file name written in docs text: expect=block got=block rc=1 committed=False
      |   footage-text        docs/a.md:1
[OK ] T12 export file name written in an openspec review: expect=block got=block rc=1 committed=False
      |   footage-text        openspec/changes/x/review.md:1
[OK ] T14 sortie date next to the word pilot, in notes: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T14b attributed date then a second date on the same line: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T15 slash date in notes: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T15b Y/M/D date in notes: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T16 English month-name date in notes: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T16b Ukrainian month-name date in notes: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T16c Russian month-name date in notes: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T16d abbreviated month with ordinal, in an object sheet: expect=block got=block rc=1 committed=False
      |   notes-date          docs/object-sheets/a.md:1
[OK ] T17 compact YYYYMMDD date in notes: expect=block got=block rc=1 committed=False
      |   notes-date          docs/reference-notes/a.md:1
[OK ] T18 lower-case MGRS in docs: expect=block got=block rc=1 committed=False
      |   coordinates         docs/a.md:1
[OK ] T19 regional pair in the commit subject: expect=block got=block rc=1 committed=False
      |   coordinates         commit message:1
[OK ] T20 messenger term #1 in the commit subject: expect=block got=block rc=1 committed=False
      |   messenger           commit message:1
[OK ] F2 drag coefficient pair (4 decimals, outside the box) in docs: expect=pass got=pass rc=0 committed=True
[OK ] F3 ordinary-noun list heads (7) in docs, bare: expect=pass got=pass rc=0 committed=True
```
All 14 former misses are blocked and both false positives are gone. T5 (three-decimal pairs, N16 below) still passes, as the spec and the first review accept.

## New attempts on the revised rules

```
$ python <scratch>/qa/rr_scenarios.py new          # one stray placeholder line of the script (an empty commit) removed
[MISMATCH] N1 reference stem used as a FILE NAME in notes (not in content): expect=block got=pass rc=0 committed=True
[MISMATCH] N2 file named <name>.<video ext>.md: expect=block got=pass rc=0 committed=True
[OK ] N3 camera name in a .cs comment (out of footage-text scope by spec): expect=pass got=pass rc=0 committed=True
[OK ] N4 Windows path with a camera name and .MP4 in a .md: expect=block got=block rc=1 committed=False
      |   footage-text        docs/a.md:1
[OK ] N5 spec form '**What the pilot said** (<date>' is allowed: expect=pass got=pass rc=0 committed=True
[OK ] N6 upper-case <name>.MOV on line 3 of the commit message: expect=block got=block rc=1 committed=False
      |   footage-text        commit message:3
[OK ] N7 stem in a .yaml in a linked worktree (reference/ only in the main checkout): expect=block got=block rc=1 committed=False leaked=False
      |   reference-stem      docs/a.yaml:1
[OK ] N8 labelled lon value in a .cs: expect=block got=block rc=1 committed=False
      |   labelled-coordinate unity/Assets/a.cs:1
[OK ] N9 UTF-16 LE with BOM .txt containing the stem: expect=block got=block rc=1 committed=False
      |   reference-stem      docs/a.txt:1
[OK ] N10 messenger term #2 in a .yaml and a .json: expect=block got=block rc=1 committed=False
      |   messenger           tools/a.yaml:1
      |   messenger           tools/b.json:1
[OK ] N11 non-media reference file stem without a digit is NOT a stem (spec): expect=pass got=pass rc=0 committed=True
[OK ] N12 reference media stem shorter than 8 chars is NOT a stem (spec): expect=pass got=pass rc=0 committed=True
[OK ] N13 image stem (jpg) in reference/, cited in a .txt: expect=block got=block rc=1 committed=False
      |   reference-stem      docs/a.txt:1
[OK ] N14 non-media reference file WITH a digit, cited in a .md: expect=block got=block rc=1 committed=False
      |   reference-stem      docs/a.md:1
[OK ] N15 regional pair in a .cs (out of coordinates scope by spec): expect=pass got=pass rc=0 committed=True
[OK ] N16 three-decimal pair in docs (below the 4-place floor, by design): expect=pass got=pass rc=0 committed=True
[OK ] N17 a 45° bank and 0° uptilt in notes, no date: expect=pass got=pass rc=0 committed=True
== 71/73 as expected
```
N1 and N2 are outside the spec's text (it says "contain", so content), and go to the suggestions. N3 and N15 pass by the spec's scopes, as intended. In all 73 cases no hidden string appeared in the hook's output.

## Design conformance of the new pieces

- **`commit-msg` hook:** three lines, `exec python …/precommit.py --commit-msg "$1"`, exit code passed through. The scissors line of `commit -v` is honoured (suite). Fires with both the absolute and the relative `core.hooksPath` (N18 above).
- **git-lfs shims:** the standard four, LF, 100755, byte-identical to git-lfs 3.7.1's own text.
- **`reference-stem`:** `reference_stems()` walks `<worktree>/reference` and `<main checkout>/reference` (via `--git-common-dir`); N7 proves the worktree case the spec was settled on. The stems live only in `ReferenceStems.by_run` and the compiled pattern; no code path prints them, and every stem case checked the output. Stems are indexed by their longest letter-digit run, so the per-file cost follows the text, not the folder (suite, 20,000 stems under 0.2 s).
- **Prefilters (`4ed9c42`):** read against their rules. `date_hint` (a year, a two-digit dotted date or a month word) covers every `DATE` alternative; `COORDINATES_HINT` (a 4-decimal number, `°`, or an MGRS zone and band) covers every coordinate form; `messenger_hint` uses the first word of every term. The suite asserts the date hint for every caught date.
- **Messages:** path rules print the folder and `*.ext` or `reference/...`; text rules print `path:line`; the commit message prints `commit message:N`. Matched text never.

## Hygiene

```
$ git log --format='%h | %an <%ae> | %cn <%ce> | %s | body_len=%b' f06bd33..HEAD    (body_chars=0 for all nine)
d5f07aa | Orchestrator <orchestrator@450fpv.local> | docs(openspec): settle the two repo-hygiene spec readings
51cdbc9 | QA Engineer <qa@450fpv.local>            | docs(openspec): tick setup-repo-hygiene task 1.2
4ed9c42 | QA Engineer <qa@450fpv.local>            | perf(hygiene): prefilter the date rule with a year and month-word hint
9e562c1 | QA Engineer <qa@450fpv.local>            | test(hygiene): cover the revised repo-hygiene spec
a093b90 | QA Engineer <qa@450fpv.local>            | feat(hygiene): carry the git-lfs hook shims in .githooks
6fe7979 | QA Engineer <qa@450fpv.local>            | feat(hygiene): add a commit-msg hook that scans the message
d4ec7eb | QA Engineer <qa@450fpv.local>            | feat(hygiene): widen the scan per the revised repo-hygiene spec
ed70009 | Orchestrator <orchestrator@450fpv.local> | docs: reword two lines the stricter scan would block
3523223 | Orchestrator <orchestrator@450fpv.local> | docs(openspec): revise repo-hygiene spec after review
$ git log --format='%h [%(trailers:only)]' f06bd33..HEAD      all empty
$ git log --format='%s' f06bd33..HEAD | awk '{print length($0)}'
57 48 70 50 56 59 63 52 53
$ git log --name-status f06bd33..HEAD      each commit touches only its own files (hooks, scan, tests, spec, docs)
$ git diff --name-only f06bd33..HEAD | grep -E '^(reference/|unity/(Library|Temp|Logs|UserSettings)/)'
(no output) rc=1
$ git ls-files -s --eol .githooks tools/hygiene
100755 i/lf w/lf attr/text eol=lf   .githooks/commit-msg, post-checkout, post-commit, post-merge, pre-commit, pre-push
100644 i/lf w/lf attr/text eol=lf   tools/hygiene/precommit.py, tools/hygiene/tests/test_precommit.py
$ PowerShell byte scan of the six hooks: CR=0 in each, each starts with #!/bin/sh
$ python <scratch>/qa/blocklist_audit.py HEAD f06bd33..HEAD       # raw case-insensitive substring, no word boundaries
35 terms decoded (23 Latin, 12 Cyrillic); empty: 0
tree HEAD: 119 entries, 119 text candidates, raw hits in 0 files
commit messages f06bd33..HEAD: 0 raw hits
$ python <scratch>/qa/blocklist_audit.py 95e3e4d f06bd33..HEAD     # the merge with the current trunk
tree 95e3e4dbf18d: 244 entries, 244 text candidates, raw hits in 0 files
$ git status --porcelain --untracked-files=all                     # this worktree, after every run above
(empty)
```
- Commits: subject-only, `type(scope): summary`, lower case, at most 70 characters, no trailers, no co-author. Identities match `docs/team.md` (QA Engineer for the code and tests, Orchestrator for the spec and docs).
- OPSEC in the diff: the tests compute their regional point and build every name from parts; the only literal dates are the spec's own examples; the messenger list stays encoded and no decoded term appears anywhere in the tree or the messages.
- Licences: still the standard library and pytest only.

## Requirements

- **SEC-06: met** on this branch. Media outside the asset roots, footage extensions and names, messenger names, coordinates (all listed forms, in `docs/`, `openspec/`, root Markdown and commit messages) and dates in notes (all listed forms), plus footage names and reference stems inside text and commit messages.
- **SEC-01: met.** Files under `reference/` and footage files cannot commit; reference stems and footage-like names cannot be written into staged text or a commit message. R1 is fixed.
- **SEC-02: met** as a backstop, with the attribution anchor the spec settled on.
- **Git LFS keeps working: met.** R2 is fixed.
- **PRC-10: met** by this review.
- **"Clean commits pass quickly", clause "zero findings over the files already on `unity/main`": not met against the trunk as it stands today** (13 findings after PR #39). This is the one open item.

## Required change

**R3. Reword the 13 flagged lines of `openspec/changes/setup-reference-tools/review.md` (on `unity/main` since PR #39, `23ee053`)** so that no `<name>.<video extension>` token remains: write the synthetic probe clip as "the probe clip (`.mp4`)" and the `check-ignore` probe paths as patterns (`*.mp4`, `*.MOV`, `reference/…`), the way `ed70009` reworded `docs/briefs/phase-0-setup.md`. The rule stays as it is.
- **Where:** the orchestrator's bookkeeping commit on `unity/main` (docs, as `workflow.md` allows), which leaves this branch untouched; or merge `origin/unity/main` into this branch and reword there.
- **Then:** `python tools/hygiene/precommit.py --tree $(git merge-tree --write-tree origin/unity/main HEAD)` prints `0 findings`, and `python -m pytest tools/hygiene -q` is green. That re-check is small; every other result of this re-review stands.

## Suggestions (non-blocking)

1. **N1:** a reference stem used as a *file name* (`docs/reference-notes/<stem>.md`) commits, because the stem rule scans content only. A path check against the same in-memory stems, with the masked output the path rules already use, would close it. Needs one spec line.
2. **N2:** a staged file named `<name>.<video ext>.md` passes the path rules (only the last extension counts), while the same token in content is blocked. Low.
3. **`docs/environment.md` on the trunk** (line 47) names only `.githooks/pre-commit`. After the merge it should also name the `commit-msg` hook and say that git-lfs's hooks live in `.githooks/` (the order it already gives, `git lfs install --local` before `core.hooksPath`, is the right one).
4. **A1 from the first review stands:** `core.hooksPath` is unset on the dev machine. With the relative `.githooks` value, a worktree whose branch predates this change has no hooks at all: no scan, and no LFS upload on push (the first review's `[nohook]` probe). Branches open today should merge `unity/main` once this lands.
5. **P2 from the first review, still open (cosmetic):** stderr is cp1252 on Windows, so non-ASCII paths print as escapes.

Verdict: CHANGES REQUESTED
