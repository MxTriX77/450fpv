# Tasks: w10-play-maps

Branch `unity/physics/physics-integration-2--w10-play`, cut from `--w3-preview` at 241145ea. Order: 1.1, then 1.2, then 2.1. It merges into `--w3-preview` before w11-world-map.

## 1. Play

- [x] 1.1 [game-developer] Play, the carousel and House.
  - Menu: Train → Play, with a true subtitle (the stale "flies the test range" goes).
  - The carousel screen: House and Village cards, keyboard, mouse and radio navigation, Esc back. Each card shows its still from `StreamingAssets/generated/stills/<map-id>.png` (in the editor, `unity/Generated/stills/<map-id>.png`) when present, else a plain card; 1.2 fills them.
  - House: bring the test range's flight back (scene in the build settings, `FlightWorlds.TestRange`, its start points, its files in the player's StreamingAssets). Find how W3 3.1 took it out (`git log` on `--w3-preview`) and restore only what the flight needs.
  - The setup, the flight, R, the summary and "again" stay on the chosen map.
  - The smoke test flies both maps from Play; a House log replays headless bit for bit.
  - Checks: EditMode for the app and the new tests, `dotnet test tools/inputtests` if it covers the app's flight, the player build (`Fpv450.Build.Windows -review`) with its smoke PASS, `python tools/hygiene/precommit.py --tree HEAD`.
  - **As built:** `Scripts/App/PlayMaps.cs` lists House (`test-range`, scene TestRange, the houses' start) and Village (`farmland`, FarmLand, the village start) with each card's line and still path; a missing or unreadable still draws a plain card with the map's name. `Scripts/UI/MapCarousel.cs` draws the carousel in `AppStyle`'s look: the chosen card 960 × 540 at 1080 lines, its neighbour at 0.62 and dimmed at the side, an eased slide, a dash per card, Back and Choose at the foot. `Scripts/App/PlayScreen.cs` reads ← → and Enter or Space once a frame, and the radio through the active calibration (the uncalibrated AETR map without one): roll past 0.6 moves one card, pitch forward past 0.6 chooses, and both sticks must come back within 0.25 before the next (`StickNav`). A click on a side card moves to it, on the middle one chooses it. Esc and Back return to the menu, the setup's Back returns to the carousel, and the carousel opens on the map chosen last (House at the start). The menu reads Play ("House or Village"); Explore's hint is "Village in noclip"; the setup is headed by the map's name; the summary's title names the map ("Flight ended · House"). `FlightSessionHost` flies its scene's map (`PlayMaps.ForScene`). TestRange is back in the build settings (`ProjectSetup`; `Build.Windows` refuses a build without it), and `AppData.PlayerFiles` carries the test range's files again. The smoke test chooses Village with the carousel's keys, as Train did, then House. In a review build, House waits for the radio first, then the smoke's own keys fly it: the summary names House, the log replays in the player, and Fly again and R stay on House. The keys are pressed only in the player's smoke: in edit mode `wasPressedThisFrame` never reads true (AppTests' note). **Checks:** EditMode `AppTests` and `PlayTests` 19/19; `inputtests` `TheFlightCarriesTheGroundTheCutsAndTheLine` passed; the review build has 0 missing references in its 3 scenes; its smoke PASS, 0 checks failed, 0 errors; `tools/replay` matches the House log bit for bit (4515 steps, 64 events, Mono → .NET 10); `precommit.py --tree HEAD` 0 findings.
- [ ] 1.2 [tech-artist] The stills.
  - A capture method that renders a map's still through the analog feed with the OSD (the flight OSD's content model, at the map's start), from a pose per map in a params file.
  - House's pose frames the test range's car, fence and house; Village's is exactly Explore's spawn frame (`FlightWorlds.FarmlandStart` and the noclip camera's spawn).
  - Rendered at build time into a git-ignored folder and copied into the player; nothing of the friend's world is pushed (D-089). The folder is shared with w11-world-map's top-down images: `unity/Generated/<kind>/` (here `unity/Generated/stills/<map-id>.png`), git-ignored, copied by the build into the player's `StreamingAssets/generated/<kind>/`.
  - Checks: the spawn-frame match (pose to the float), the build with its smoke PASS, a look at both stills, `precommit.py --tree HEAD`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec scenarios: both maps fly from Play, the House log replays bit for bit, the stills match their poses and nothing of the friend's world is pushed, the smoke PASS.
