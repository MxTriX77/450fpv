# Spec Delta

## Purpose

The G1 look-lock gallery: a Windows build in which the pilot flies in noclip through the six look-lock stations under the reference letters' light, from saved viewpoints that recreate those letters' framing, so the look is judged against the references before mass production.

## ADDED Requirements

### Requirement: Six stations from the look-lock assets
The gallery scene SHALL place the built look-lock assets as six stations along one axis on a flat floor: the S04 patch; the belt section (three V13 clumps among V01 S, M and L); H01 in I, D and X side by side; R01 in A and F; the E01 run; L01 straight, leaning and stub with L03 spans between two poles 40 m apart. Each station SHALL carry a numbered marker and nothing else that is text; no name, place or sign.

#### Scenario: Stations present
- **WHEN** the gallery scene is opened in the build
- **THEN** all six stations show their prefabs as the asset changes committed them, with no missing mesh or pink material, spaced so that the 10 m view of one station shows no other, and the log lists the prefab of each station

#### Scenario: Nothing added by hand
- **WHEN** an asset is rebuilt by `pwsh tools/build_asset.ps1 <ID>`
- **THEN** its station shows the rebuilt prefab with no edit to the scene

### Requirement: Saved viewpoints at the letters' framing
The scene SHALL hold one saved viewpoint per station at the height and framing of the reference letter the station copies (H for S04 and the belt, E for the houses, M for the cars, G for the trench, L for the poles and J for the wires) plus a 200 m viewpoint looking at the belt row, and the number keys SHALL move the noclip camera to them; the noclip camera SHALL start at the S04 viewpoint.

#### Scenario: Jump to a viewpoint
- **WHEN** the pilot presses the key of a station
- **THEN** the camera moves to that viewpoint's position and heading within one frame and noclip control continues from there

#### Scenario: The 200 m belt view
- **WHEN** the pilot jumps to the 200 m viewpoint under the day and overcast presets
- **THEN** the belt row reads as a dark band against the horizon, judged against letters A and H in the review

### Requirement: Light presets by key
The scene SHALL offer four light presets, day, low sun, overcast and night, switched by function keys, each driven by the look setup's time of day and sky, and SHALL show the active preset's name on screen for one second.

#### Scenario: Switching presets
- **WHEN** the pilot presses the key of a preset
- **THEN** the sun, sky, ambient and haze change to that preset within one frame, every station remains lit as the preset defines, and the preset name is shown briefly

### Requirement: The gallery build
`Fpv450.Build.Gallery` SHALL build `Builds/Windows-Gallery/450fpv-gallery.exe` from the gallery scene alone, the build SHALL exit on `-smoketest` after its first frame, and `docs/gallery-g1.md` SHALL tell the pilot how to run it and what the keys do.

#### Scenario: Build and smoke test
- **WHEN** the build method runs in batchmode and the exe runs with `-smoketest`
- **THEN** both exit 0 and the player log shows the gallery scene loaded

#### Scenario: Frame rate
- **WHEN** QA runs the build on AC power with vsync off, the power mode printed, and flies the six viewpoints for one minute each
- **THEN** the 1 % low frame time is under 16.7 ms at 1080p, or the handoff names the station that breaks it
