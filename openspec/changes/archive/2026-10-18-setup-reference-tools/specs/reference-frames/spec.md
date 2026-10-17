# Spec Delta: reference-frames

## Purpose
Defines the tools that turn the pilot's local clips into stills and contact sheets for analysis, writing only into the git-ignored frames folder and never exposing reference file names.

## ADDED Requirements

### Requirement: Writes only inside reference/_frames
Both tools SHALL refuse to write anywhere except under `<repo>/reference/_frames/`, and SHALL read clips only from under `<repo>/reference/`.

#### Scenario: Output outside the frames folder is refused
- **WHEN** an output location resolving outside `reference/_frames/` is requested (including via `..` or an absolute path)
- **THEN** the tool exits non-zero before writing anything

#### Scenario: Input outside reference is refused
- **WHEN** a clip path outside `reference/` is given
- **THEN** the tool exits non-zero

### Requirement: Outputs are named by letter
Extracted stills, indexes and sheets SHALL be named by the clip letter and a sequence or timestamp only, and SHALL NOT contain the source file name anywhere in their names, metadata or index.

#### Scenario: Source name absent from outputs
- **WHEN** a synthetic clip with a distinctive file name is extracted as letter `Z` and a sheet is made
- **THEN** no output file name, index field or image metadata contains that distinctive name

### Requirement: Frame extraction
`extract_frames.py` SHALL extract stills at a requested rate (frames per second) or interval, over an optional start and duration, and write an index with each still's timestamp in seconds.

#### Scenario: Two frames per second over ten seconds
- **WHEN** a 10 s synthetic clip is extracted at 2 fps
- **THEN** 20 stills (±1) are written and the index lists their timestamps, increasing

### Requirement: Contact sheets
`contact_sheet.py` SHALL tile between 12 and 24 stills into one image, in time order, each tile labelled with the letter and its timestamp.

#### Scenario: 24-still sheet
- **WHEN** a sheet is made from 24 extracted stills with 6 columns
- **THEN** one image with a 6 × 4 grid is written, and each tile shows its label

#### Scenario: Out-of-range count is refused
- **WHEN** a sheet of 30 stills is requested
- **THEN** the tool exits non-zero with a message giving the allowed range
