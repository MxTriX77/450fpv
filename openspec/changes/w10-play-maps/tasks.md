# Tasks: w10-play-maps

Branch `unity/physics/physics-integration-2--w10-play`, cut from `--w3-preview` at 241145ea. Order: 1.1, then 1.2, then 2.1. It merges into `--w3-preview` before w11-world-map.

## 1. Play

- [ ] 1.1 [game-developer] Play, the carousel and House.
  - Menu: Train → Play, with a true subtitle (the stale "flies the test range" goes).
  - The carousel screen: House and Village cards, keyboard, mouse and radio navigation, Esc back. Each card shows its still from `StreamingAssets/generated/stills/<map-id>.png` (in the editor, `unity/Generated/stills/<map-id>.png`) when present, else a plain card; 1.2 fills them.
  - House: bring the test range's flight back (scene in the build settings, `FlightWorlds.TestRange`, its start points, its files in the player's StreamingAssets). Find how W3 3.1 took it out (`git log` on `--w3-preview`) and restore only what the flight needs.
  - The setup, the flight, R, the summary and "again" stay on the chosen map.
  - The smoke test flies both maps from Play; a House log replays headless bit for bit.
  - Checks: EditMode for the app and the new tests, `dotnet test tools/inputtests` if it covers the app's flight, the player build (`Fpv450.Build.Windows -review`) with its smoke PASS, `python tools/hygiene/precommit.py --tree HEAD`.
- [ ] 1.2 [tech-artist] The stills.
  - A capture method that renders a map's still through the analog feed with the OSD (the flight OSD's content model, at the map's start), from a pose per map in a params file.
  - House's pose frames the test range's car, fence and house; Village's is exactly Explore's spawn frame (`FlightWorlds.FarmlandStart` and the noclip camera's spawn).
  - Rendered at build time into a git-ignored folder and copied into the player; nothing of the friend's world is pushed (D-089). The folder is shared with w11-world-map's top-down images: `unity/Generated/<kind>/` (here `unity/Generated/stills/<map-id>.png`), git-ignored, copied by the build into the player's `StreamingAssets/generated/<kind>/`.
  - Checks: the spawn-frame match (pose to the float), the build with its smoke PASS, a look at both stills, `precommit.py --tree HEAD`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec scenarios: both maps fly from Play, the House log replays bit for bit, the stills match their poses and nothing of the friend's world is pushed, the smoke PASS.
