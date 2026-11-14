# Proposal: w15-player-physics

**Owner:** game-developer · **Reviewers:** qa-engineer · **Requirements:** APP-04, PHY-03, PHY-05, PHY-24, PHY-25, LOG-01, PLT-02 · **VISION:** §2 · **Decisions:** D-104, D-106 to D-112 (the night's physics), D-089, D-100 · **Plan:** the physics ledger (`docs/reviews/physics-ledger.md`)

## Why
The pilot's build flies physics model 85. Since then the physics line (`unity/physics/physics-integration-2--w9-village`) has moved to model 99: stems cut by the props, each leg's own stiffness, the legs' corrected failure law, blades that break on a branch and meet thin branches and wires, crowns with twigs and leaves, wind wakes behind the village's ruins and hedges, the rotors' own wake at its measured level, the props' descent branch both ways, the fiber's exit law and the rotors' wash on it, a laid line that moves in the wash, and a hanging skid half that bears on the ground. None of it has been compiled or tested in Unity, and three pieces are not wired into a player's flight at all. The pilot asked how close the build is and chose to wait for the fixes in work (2026-11-13); they are in, so the player gets them.

## What Changes
- **A player's flight flies the hand-made legs:** the session's setup draws each leg's stiffness from the flight's seed, as the replay's scripted flight already does, so a leg can hold for a moment at liftoff (PHY-03, PHY-24; `w9-village-physics` 2.9).
- **The summary names what the props cut:** stems and straw (the log's `StemEpisode`), beside what it already names (PHY-05, APP-04).
- **The feed gets the stems' signals** the sim publishes, behind the same stub rule as every other signal (PHY-25).
- **Unity compiles and its EditMode tests pass on the merged tree,** including the three EditMode tests that read the allocation counter directly, which move to the probe the sim's tests use.
- **The build's pointer** (`tools/build_latest.json` on the trunk) says what the build now holds.

## Non-goals
- No new physics, and no change to the sim core's numbers: a flight in the player is the line's flight, bit for bit.
- Not the cut bits on camera (`w9-village-physics` 2.5), the props in the frame (W13) or the camera's place (W14): they stay where they are.
- No change to the menu, Play, the map or the feed's look.

## Impact
- `unity/Assets/_Project/Scripts/App/Session/FlightSession.cs` (`SessionSetup.Build`), `PostFlight.cs`, `Scripts/App/FlightFeedSignals.cs`, the EditMode tests under `unity/Assets/_Project/Tests/EditMode/`.
- `tools/flightbench` compiles `SessionSetup` too: its harnesses that fly the session's drone get the legs' draw from the same place, so `--ground` and `--tether` are re-run.
- The branch is `unity/physics/physics-integration-2--w15-player`, cut from `--w3-preview` with the physics line merged in. It merges back into `--w3-preview`, the build's base.
