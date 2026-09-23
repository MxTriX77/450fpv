# Tasks

## 1. Tracker

- [x] 1.1 [physics-engineer] Confirm the wind clip is indexed under its letter by the stable-letter extractor (re-run it if needed) and has stills, sheets, bursts, `metrics.csv` and events. Verify that the letter is in `reference/_frames/index.md` and earlier letters are unchanged
- [x] 1.2 [physics-engineer] Write `tools/reference/track_attitude.py`: undistort, horizon fit, pitch through FOV, yaw rate by phase correlation, confidence and gating. Verify the synthetic-accuracy and no-guessing scenarios with a synthetic test set, generated in the scratchpad
- [x] 1.3 [physics-engineer] Run it on every frame of the wind clip and write `attitude.csv`. Verify the real-frame spot check: 20 random high-confidence frames with the horizon overlaid (overlays stay inside `reference/_frames/`), within 1°

## 2. Wind notes

- [x] 2.1 [physics-engineer] Write `docs/reference-notes/wind.md` §1–§4: the method, coverage, and all measurements per axis with units, frame ranges and uncertainties. Verify that every number reproduces from `attitude.csv`
- [x] 2.2 [physics-engineer] Add §5, the interpretation and closed-loop limit (the pilot's correction bandwidth, turbulence implications, motor margin and saturation, each labelled measured, derived or assumed), and §6, the testable severe-wind targets. Verify that each target has a number, unit, tolerance and procedure
- [ ] 2.3 [tech-artist] Add the rain-on-the-feed section to `wind.md`: drops, streaks, contrast and colour, blur, flare, and clearing by prop wash or airspeed, each with frames, an effect and a driver. Verify that every trait cites frames and names a driver

## 3. Review

- [ ] 3.1 [qa-engineer] Review against spec scenarios: tracker accuracy on the synthetic set, a re-run of the spot check, reproducibility of every statistic, the target format, and the OPSEC scan of `wind.md` and commit messages. Write `review.md` with the verdict
