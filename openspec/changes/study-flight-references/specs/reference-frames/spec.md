# Spec Delta

## Purpose

Turns the pilot's local reference media into stills that agents can inspect, without any of that material ever leaving the git-ignored `reference/` folder.

## ADDED Requirements

### Requirement: Extraction from every clip
The tool SHALL process every video file found anywhere under `reference/`, excluding `reference/_frames/`, in one headless run. It SHALL NOT need any software beyond the installed Blender.

#### Scenario: Full run
- **WHEN** the tool runs with default settings on the current reference set (8 clips)
- **THEN** each clip has sampled stills at a fixed interval (default 1.0 s ± one frame), and at least one contact sheet, under `reference/_frames/<letter>/`

#### Scenario: Consecutive bursts
- **WHEN** the tool runs with default settings
- **THEN** each clip has at least 3 bursts (start, middle, end) of at least 8 consecutive native-rate frames. These let the temporal noise be studied.

### Requirement: Output stays local and ignored
Every file the tool writes SHALL be inside `reference/_frames/`, which git ignores. The tool SHALL NOT write anywhere else in the repo.

#### Scenario: Git stays clean
- **WHEN** the tool finishes a full run
- **THEN** `git status --porcelain` shows no new or changed paths caused by the run

### Requirement: Clips are referred to by letter
The tool SHALL give each clip a stable letter (A, B, C…) in sorted path order. It SHALL write the letter-to-filename mapping only to `reference/_frames/index.md`.

#### Scenario: Stable letters
- **WHEN** the tool runs twice on an unchanged reference set
- **THEN** each clip keeps the same letter both times

### Requirement: Re-runnable
Running the tool again SHALL overwrite its previous output. It SHALL NOT fail or leave stale frames mixed in.

#### Scenario: Second run
- **WHEN** the tool runs a second time
- **THEN** it exits with code 0, and `reference/_frames/` holds only the output of the second run
