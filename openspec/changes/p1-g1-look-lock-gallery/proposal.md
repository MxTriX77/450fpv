# Proposal: p1-g1-look-lock-gallery

**Owner:** world-builder (the gallery scene, stations and build) · **Reviewers:** tech-artist (its own task, the look setup), qa-engineer; **the pilot at G1**
**Requirements:** PRD-07 (noclip), VIS-01, VIS-02, VIS-03, VIS-04, REAL-01, REAL-02, REAL-03, AST-12, PLT-07 (the frame-rate check) · **Decisions:** D-001 (settled here), D-028, D-031 · **VISION:** §1 (the pilot judges the look against the references), §5 (the look-lock gate before mass production)
**Source:** `docs/plan.md` phase 1 steps 3 and 5 (look setup; G1: a quick gallery build of the six stations, a 10-minute look, feedback into `docs/style-rules.md`, URP vs HDRP settled), `docs/briefs/phase-1-assets.md` (write scope: world-builder, only the quick look-lock gallery), the six look-lock changes (S04, V13 + V01, H01, R01, E01, L01 + L03), `docs/requirements.md` §0 (the reference views), `unity/Assets/_Project/Settings/Review/` (the neutral rig the capture uses today).

## Why
G1 is the first thing the pilot sees of the Unity restart, and the only review before sixty more assets are built in the locked style. It needs a build they can fly through in noclip, the six look-lock stations placed as their reference letters show them, and a sky, sun and distance haze good enough to judge "does it look like my footage" (REAL-01) and "does a belt read as a dark band at 200 m" (VIS-04), with the time-of-day looks the letters cover. The D-001 URP-or-HDRP question is answered on these pictures.

## What Changes
- **The look setup** (tech-artist, the phase-1 brief's step 3 at G1 scale): a sun and sky driven by a time of day in hours (VIS-02) with four presets for the gallery (day, low sun as in G and B, overcast as in A and H, night as in L), aerial perspective tuned so the belt station at 200 m reads as a dark band against the horizon tone of the footage (VIS-04), the quality level at High with `lodBias` 1 (as the pipeline hardening set it), and the `Sunny` / `Rain` / `Severe rain` looks left for phase 4 except the overcast sky and the wet-material parameters already in the recipes.
- **The gallery scene** `Gallery_LookLock.unity` (world-builder): a flat floor of a few hundred metres with the straw cover set tiled (not a judged surface), the six stations along one axis at the spacing their sizes need, each with a numbered marker and a saved viewpoint at the reference letter's height and framing (REAL-02 at G1 scale: H for S04 and the belt, E for the houses, M for the cars, G for the trench, L and J for the poles and wires), a 200 m viewpoint on the belt row, the noclip camera starting at the first station, number keys jumping between viewpoints and function keys switching the light presets; invented names only, no text beyond the station numbers and letters.
- **The build:** `Fpv450.Build.Gallery` producing `Builds/Windows-Gallery/450fpv-gallery.exe` from the gallery scene alone, with the `-smoketest` exit, and a short `docs/gallery-g1.md` saying how to run it and what the keys do (for the pilot).
- **G1 itself:** the pilot's 10-minute look; their words go into `docs/style-rules.md` (orchestrator), D-001 is closed with a decision entry, and anything that fails by eye returns to its asset change before mass production.

## Capabilities
### New Capabilities
- `look-lock-gallery`: the G1 gallery build, its stations, viewpoints, light presets and keys.
- `look-setup`: the sky, sun and aerial perspective by time of day and the four G1 presets (grown in phase 4 into the full VIS-02/03 model).

### Modified Capabilities
- none

## Non-goals
- No gallery map package or compiler (phase 2's gallery is the map format's); no terrain, no feathering between surfaces; no TX12 input (the noclip keyboard stub is PRD-07's); no feed, no OSD; no rain look beyond overcast (phase 4); no weather or wind sway.
- No new assets: the stations hold what the six changes built, as built.

## Impact
`unity/Assets/_Project/Scenes/Gallery_LookLock.unity`, `unity/Assets/_Project/Scripts/World/{GalleryStations,ViewpointKeys}.cs`, `unity/Assets/_Project/Settings/{Sky,Light}/` and `unity/Assets/_Project/Scripts/Feed/` or `Shaders/` for the sky and haze (tech-artist), `unity/Assets/_Project/Scripts/World/LightPresets.cs`, `unity/Assets/_Project/Editor/Build.cs` (a `Gallery` method), `docs/gallery-g1.md`, `docs/style-rules.md` (new, after the gate), `docs/decisions.md` (D-001 closed). Branch: `unity/world/p1-g1-look-lock-gallery` off `unity/main` once the six look-lock changes are merged; the look-setup task can start on its sub-branch as soon as S04 and V01 exist.
