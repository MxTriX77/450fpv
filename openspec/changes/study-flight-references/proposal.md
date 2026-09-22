# Proposal: study-flight-references

**Owner:** world-artist · **Contributor:** tech-artist · **Reviewer:** qa-engineer
**Motivation:** CLAUDE.md §3 asks us to review `reference/terrain` carefully, because the pilot's own flights define the target look. §4.1 says the map must be built from those media materials. §5.1 wants intermediate map feedback on main terrain parts, objects and textures.

## Why

The whole map and the analog video look must come from the pilot's real flights: 8 clips of about 100 s total plus 3 photos. Right now that knowledge sits only in local video files that agents can't watch directly and that must never enter the public repo.
This change turns the footage into written reference notes that every role can build from. It is also what the first feedback round (M1) will be based on.

## What Changes

- A new headless Blender tool, `tools/reference/extract_frames.py`. It turns every clip in `reference/` into sampled stills, consecutive-frame bursts and contact sheets under the git-ignored `reference/_frames/`. It also writes a local-only index that maps clip letters to file names.
- A new `docs/reference-notes/terrain.md`. It catalogues terrain types and object classes: tree belts (посадки), war-damaged houses, typical vehicles, fields, vegetation, roads, craters, debris, wires and poles. For each it records scale, materials, colours and density, and whether it matters physically (surface, obstacle, turbulence source). It ends with a prioritised build list for M1.
- A new `docs/reference-notes/video-feed.md`. It describes the analog feed as the footage shows it: resolution feel, noise and how it changes, colour, exposure and artifacts, plus the general OSD layout (never OSD values). Each trait is mapped to a candidate shader effect. It also records which artifacts come from re-encoding the clips rather than from the real feed.
- OPSEC-safe by construction. The notes refer to clips only by letter, and contain no place names, coordinates, OSD values, dates, file names, unit information, identifiable landmarks or tactical positions.

## Capabilities

### New Capabilities
- `reference-frames`: headless extraction of stills, bursts and contact sheets from local reference media into a git-ignored folder
- `reference-notes`: the written reference catalog (terrain + video feed), its required coverage and its OPSEC rules

### Modified Capabilities
_None._

## Non-goals

- Building any assets, terrain patches or textures. That is the next M1 change.
- Defining the map format.
- Writing video shaders. `video-feed.md` only proposes candidate effects.
- Committing, copying or uploading any footage, frame or still.

## Impact

- New files: `tools/reference/extract_frames.py`, `docs/reference-notes/terrain.md`, `docs/reference-notes/video-feed.md`
- Local only (git-ignored): `reference/_frames/`
- Dependencies: Blender 5.2 (installed), with its bundled ffmpeg and numpy. Nothing new to install.
- Gates: the user reviews both notes files before any M1 asset work starts.
