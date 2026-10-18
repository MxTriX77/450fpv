# Tasks: p1-g1-look-lock-gallery

Branch `unity/world/p1-g1-look-lock-gallery` off `unity/main` once the six look-lock changes are merged. Task 1.1 may start earlier on the sub-branch `unity/world/p1-g1-look-lock-gallery--look` (it touches only the look-setup files) as soon as S04 and V01 are merged, and is merged into the change branch before 1.2.

## 1. Build

- [ ] 1.1 [tech-artist] The look setup (design 4, 5): `LookSetup` with the sun arc, the gradient sky, ambient and height fog from `hour` and `cloud_cover`, the four presets as data files under `Settings/Light/`, the preset label; verify an EditMode test that day and overcast light a horizontal 0.18 grey within 5 %, a capture of the S04 patch and three V01 L at 200 m under each preset (attach the four images), and that the review capture's K01 sheet is unchanged
- [ ] 1.2 [world-builder] The gallery scene (design 1–3, 6): `Gallery_LookLock.unity` with the floor, the six stations as prefab instances with numbered markers, the `Viewpoint` set and `ViewpointKeys`, the preset keys wired to `LookSetup`, `GalleryBoot` with `-smoketest`, `Fpv450.Build.Gallery`, and `docs/gallery-g1.md`; verify the scene lists its six prefabs in the log with no missing mesh or pink material, every number key lands on its viewpoint (an EditMode test on the viewpoint table), `& $env:UNITY -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Gallery -logFile - | Out-Host` exits 0, and `Builds/Windows-Gallery/450fpv-gallery.exe -smoketest` exits 0 with the scene logged

## 2. Review and gate

- [ ] 2.1 [qa-engineer] review against spec scenarios: the gallery and look-setup scenarios, a screenshot from every viewpoint under every preset read against the stations' reference letters (REAL-03), the 200 m belt band, the frame-rate measurement on AC with vsync off and the power mode printed (PLT-07, 1 % low per viewpoint), the smoke test, hygiene (nothing from `reference/`, no text beyond numbers and letters in the scene, subject-only commits)
- [ ] 2.2 [user-review] **G1 look-lock:** the pilot runs `450fpv-gallery.exe` and takes a 10-minute look at the six stations under the presets; the orchestrator records their words in `docs/style-rules.md`, closes D-001 in `docs/decisions.md`, asks only the G1-tagged questions of `docs/pilot-questions.md`, and routes every failed look back to its asset change before mass production
