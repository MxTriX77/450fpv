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
