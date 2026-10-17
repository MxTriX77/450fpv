# Tasks: setup-review-capture

## 1. Capture

- [x] 1.1 [tech-artist] Add `Fpv450.Review.Capture` with the review stage, the two light profiles, the cameras, the metadata overlay and the sheet composition, and append the step to `tools/build_asset.ps1`; verify the batchmode capture for K01 exits 0 and writes both sheets with all 26 labelled tiles, `-asset NOPE` exits non-zero, and `pwsh tools/build_asset.ps1 K01` exits 0
- [ ] 1.2 [tech-artist] Address the review: put the scale bar inside the pilot-height frame per design decision 2 (lower bands visible, label when the top is cut), and reword commit 86b2169's subject to ≤ 72 characters with a non-interactive history rewrite that keeps the authors; verify the K01 capture exits 0 with the bar visible in both 1.5 m tiles, every subject in `git log 0e2e6a3..HEAD` is ≤ 72 characters, and `git status` is clean

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios (re-review after 1.2)
