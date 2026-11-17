# Proposal: w18-menu-cleanup

**Owner:** game-developer · **Reviewers:** qa-engineer · **Requirements:** PRD-05, PRD-07, APP-02, APP-03, APP-04, INP-02, PLT-06 · **VISION:** §3 ("prettier than uglier") · **Tier:** Standard

## Why
The pilot, 2026-11-17 (rendered from Russian): "Delete the Explore section. And also remove the small labels across the project (like next to Train you write the two maps side by side, and similar things: remove them everywhere)."

Two things follow:
- **No Explore.** The menu loses its review-build item that opened the farmland in noclip. The noclip view itself stays where tooling needs it, reached by a command-line argument or an editor method, never by the pilot: the farmland's frame-rate bench (`-farmbench`), the smoke test's world checks in a review build (`-smoketest`), and the carousel's still of Village (`MapStills`, which is the noclip view's spawn frame).
- **No describing labels.** A text on a screen stays only where the pilot needs it to operate that screen: the name of an item, an option or a control; a key or stick prompt; a value or a state that changes (the radio's line, the build's version, the map loading); a problem or an error. A text that describes what an item, a map or an option is goes, and the layout closes up where it was.

## What Changes
- **The menu** reads Play, Calibrate and Exit in every build, each by its name alone: Play's "House or Village" and Explore's "Village in noclip" go, and the line under the title keeps the drone's name only.
- **The noclip view** is no longer a pilot's screen: Esc's way back to the menu from it, its radio-driven throttle element (`ExploreThrottle`) and its map panel (M) go, since only a pilot in Explore used them. `-farmbench`, the smoke test and `MapStills` open it as before (`FarmlandExplore.Open`).
- **The smoke test** opens the noclip view through that same entry instead of the menu's item, and keeps every check it made there that is about the farmland: the stand on the terrain at the village's start, the first frame within PLT-06's 20 s, the feed on, and the drawn terrain within 1 mm of the sim's round the stand. Its check of M in noclip goes with the panel; M over the farmland stays checked in the farmland flight.
- **The carousel's cards** show the map's still and name, without the line on what the map is.
- **The setup's legs** read On and Off, without "· launch stand".
- **The loading screen** reads "Loading House…" or "Loading Village…", without "arm once the drone is in".
- **The Calibrate screen** loses "About a minute, with the radio alone after Start." and the Rates tab its footnote "Betaflight 4.5, ACTUAL rates. Changes are saved as you make them."
- Every other text keeps its place; the as-built note in `tasks.md` lists each with its reason.

## Non-goals
- No change to the OSD: it reads "Svinoriz 10 Opto" and "! SAFE !" (OSD-02), and its throttle element stays in the flight's OSD (D-080).
- No change to the feed (`Scripts/Feed`), its noclip overlay or the sim.
- No new screen, no new setting, no restyle.
- The gallery's and the test range's own builds keep their noclip (PRD-07 for gallery review).
- `docs/requirements.md` and `docs/decisions.md` are the orchestrator's: the new wording of PRD-05, PRD-07 and APP-02 is handed back, not edited here.

## Impact
- `Scripts/UI`: `MenuScreen`, `MapCarousel`, `SetupScreen`, `CalibrateScreen`, `RatesPanel`, `MapPanel` (a comment).
- `Scripts/App`: `AppFlow` (the menu, the loading line, the smoke test), `PlayMaps`, `PlayScreen`, `FarmlandExplore`, `FarmlandBench` (comments), `AppData` (a comment); `ExploreThrottle` is deleted.
- `Editor/Build.cs` (a comment).
- EditMode tests: `PlayTests`, `AppTests`.
- Specs: `app-flow`, `play-maps`, `world-map` and `farmland-world` as this change's deltas state. The first three exist only as `p6-input-app`'s, `w10-play-maps`' and `w11-world-map`'s added requirements, so this change archives after those three. `w10-play-maps` names Village's still "Explore's spawn frame": that frame is the noclip view's, `FarmlandExplore.Open`, which stays, so its requirement holds as written.
- The branch is `unity/game/physics-integration-2--menu-cleanup`, cut from the pilot's build branch at 88acbec1.
