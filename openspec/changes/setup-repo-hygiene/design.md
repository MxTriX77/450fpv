# Design: setup-repo-hygiene

## Context
Git for Windows runs hooks through its bundled `sh`; `python` (3.14.6, measured) is on PATH. Worktrees share the repo config, so one `core.hooksPath` setting covers every agent's worktree whose branch contains `.githooks/`.

## Decisions
1. **Thin shell hook, Python scan.** `.githooks/pre-commit` (LF, executable bit) only runs `python tools/hygiene/precommit.py` and passes its exit code through. All logic is Python, so it is testable with pytest.
2. **Scan the index, not the working tree.** File list: `git diff --cached --name-only --diff-filter=ACMR -z`. Content: `git show :<path>` (the staged blob). LFS files are scanned as pointers, so the binary rules use the path only.
3. **Rules** (one function each, each with a short id printed on failure):
   - `footage-ext`: video extensions anywhere (mp4, mov, mkv, avi, m4v, mts, m2ts, 3gp, webm, wmv, flv, mpg, mpeg, h264, h265, hevc, insv, lrv, dav).
   - `reference-path`: any path starting with `reference/`.
   - `footage-name`: camera and export name patterns (DJI_, VID_, IMG_, PXL_, MVI_, DSC, GOPR, GX…, `Screenshot_20…`, messenger export names like `photo_20YY-…`, `video_20YY-…`), matched on the file's base name.
   - `media-location`: image and audio extensions outside `assets-src/` and `unity/Assets/`.
   - `messenger`: messenger app names in text, Latin and Cyrillic, case-insensitive, as whole words. Generic words that collide with project vocabulary (for example "signal", which the feed uses constantly) are matched only in unambiguous forms (a domain or "… messenger"). The list is stored encoded (base64 or similar) so the hook's own source never contains the names in plain text.
   - `coordinates` (under `docs/`): decimal pairs with ≥ 4 decimals whose values fall in latitude/longitude ranges; D°M′S″ with a hemisphere letter; MGRS references.
   - `notes-date` (under `docs/reference-notes/` and `docs/object-sheets/`): ISO and dotted dates, unless the line contains `D-\d{3}` or `the pilot`.
4. **Messages** print the rule id, the path and the line number, never the matched text, so the hook's own output can't leak what it caught into a log someone pastes.
5. **Tests** build a throwaway repo in `tmp_path`, set `core.hooksPath` to the real `.githooks`, plant one violation per test and assert the commit fails; a clean commit must pass. They also time 200 clean files.

## Risks / Trade-offs
- [False positives on legitimate text] → narrow patterns, scoped to `docs/` where possible; a false positive is fixed in the rule, never bypassed.
- [Hook not active in a fresh clone] → `docs/environment.md` lists the one-liner; QA's review checks `git config core.hooksPath` on the dev machine.
- [The media rule blocks generated map rasters (heightfield, surface, cover and holes PNGs under `maps/`)] → follow-up for the phase-3 map-format change: allow image rasters under `maps/<map-id>/` there, with tests. Diagrams in `docs/` stay text (Markdown or Mermaid), never images.
- [Path findings could leak the very file name they catch, slip two of knowledge.md §14.2] → path rules print the folder and `*.ext` (or `reference/...`), never the name; text rules print `path:line`.
