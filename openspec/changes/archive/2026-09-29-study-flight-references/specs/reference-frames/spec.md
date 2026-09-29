# Spec Delta

## Purpose

Turns the pilot's local reference media into stills that agents can inspect, without any of that material ever leaving the git-ignored `reference/` folder.

## ADDED Requirements

### Requirement: Extraction from every clip
The tool SHALL process every video file found anywhere under `reference/`, excluding `reference/_frames/`, in one headless run. It SHALL NOT need any software beyond the installed Blender.

#### Scenario: Full run
- **WHEN** the tool runs with default settings on the current reference set
- **THEN** each clip has sampled stills at a fixed interval (default 1.0 s ± one frame), and at least one contact sheet, under `reference/_frames/<letter>/`

#### Scenario: Consecutive bursts
- **WHEN** the tool runs with default settings
- **THEN** each clip has at least 3 bursts (start, middle, end) of at least 8 consecutive native-rate frames. These let the temporal noise be studied.

#### Scenario: Lossless detail frames
- **WHEN** bursts or event frames are written
- **THEN** they are saved losslessly (PNG) at native resolution, so no new compression artifacts are added to what's studied. Stills and contact sheets may be JPEG, because they are only for overview.

### Requirement: Every frame is measured
The tool SHALL decode every frame of every clip. For each clip it SHALL write `reference/_frames/<letter>/metrics.csv` with one row per frame, holding: frame index, time in seconds, mean luma, noise level (high-frequency energy), horizontal-stripe energy (row-to-row variation), change from the previous frame, and mean R, G and B.

#### Scenario: Complete metrics
- **WHEN** a full run finishes
- **THEN** each clip's `metrics.csv` has exactly one row per decoded frame of that clip, and no empty values

### Requirement: Glitch events are captured
Frames whose noise, stripe or change metric is an outlier within their own clip SHALL be flagged in `metrics.csv` and exported losslessly together with 2 neighbouring frames on each side. The outlier threshold SHALL be configurable.

#### Scenario: Events exported
- **WHEN** a full run finishes
- **THEN** every frame flagged in `metrics.csv` has a PNG in `reference/_frames/<letter>/events/`, along with its ±2 neighbours (clamped at the clip ends)

#### Scenario: Short flashes are not missed
- **WHEN** a clip contains a single-frame full-frame noise flash
- **THEN** that frame is flagged, because the scan covers every frame and does not sample

### Requirement: Output stays local and ignored
Every file the tool writes SHALL be inside `reference/_frames/`, which git ignores. The tool SHALL NOT write anywhere else in the repo.

#### Scenario: Git stays clean
- **WHEN** the tool finishes a full run
- **THEN** `git status --porcelain` shows no new or changed paths caused by the run

### Requirement: Clips are referred to by letter
The tool SHALL give each clip and photo a stable letter (A, B, C…). Letters already in the index SHALL be kept. Files added later get the next free letters, and a first run assigns them in sorted path order. Photos the tool can't show directly (e.g. AVIF) SHALL get a lossless `photo.png` copy under their letter, inside `reference/_frames/`. It SHALL write the letter-to-filename mapping only to `reference/_frames/index.md`.

#### Scenario: Stable letters
- **WHEN** the tool runs twice on an unchanged reference set
- **THEN** each clip keeps the same letter both times

#### Scenario: New files don't reletter
- **WHEN** new clips or photos are added to `reference/` and the tool runs again
- **THEN** every existing letter maps to the same file as before, and the new files get the next free letters

### Requirement: Re-runnable
Running the tool again SHALL overwrite its previous output. It SHALL NOT fail or leave stale frames mixed in.

#### Scenario: Second run
- **WHEN** the tool runs a second time
- **THEN** it exits with code 0, and `reference/_frames/` holds only the output of the second run
