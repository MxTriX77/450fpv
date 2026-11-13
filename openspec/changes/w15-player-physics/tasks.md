# Tasks: w15-player-physics

Branch `unity/physics/physics-integration-2--w15-player`, cut from `--w3-preview` with the physics line (model 99) merged in by the orchestrator. Order: 1.1, then 2.1, then 3.1. It merges back into `--w3-preview`.

## 1. The player flies the line's physics

- [ ] 1.1 [game-developer] The session's wiring, compiled and tested in Unity.
  - `SessionSetup.Build` draws the legs' stiffness for a flight with legs on (`contacts.Legs?.DrawStiffness()`, as `tools/replay/ScriptedFlight.cs` does), from the flight's seed and logged as the sim logs it. The harnesses that add the draw themselves (`FiberCutScenarios`) do not draw twice.
  - A session's flight over the farmland's grass and straw cuts stems (the sim's stems are on in a player's flight); `PostFlight` names the cuts from the log's `StemEpisode` events in the summary's lines.
  - `FlightFeedSignals` passes on whatever stems' signals the sim's feed signals publish, behind the stub rule the other signals follow. If the sim publishes none yet, say so and add nothing.
  - The three EditMode tests that read `GC.GetAllocatedBytesForCurrentThread` directly (`FpvCameraTests`, `FiberViewTests`, `FlightOsdTests`) read through the sim's `AllocProbe` (or its equivalent on Unity's Mono, with the reason if the probe's rule does not hold there).
  - Tests (EditMode): a session's flight with legs on logs the legs' drawn stiffness and differs between two seeds; a session's landing in the farmland's straw logs `StemEpisode` and the summary names it; a session's flight replays bit for bit in Unity as it does headless (the same log hash as `tools/replay` for the same seed and sticks).
  - Checks: Unity compiles with no error and no new warning; the whole EditMode suite; `dotnet test tools/simtests -c Release`; `flightbench --golden`, `--ground`, `--tether 20` before and after the legs' draw in `SessionSetup` (they fly the session's drone); `precommit.py --tree HEAD`; `openspec validate w15-player-physics --strict`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec scenarios: the player's flight equal to the headless one bit for bit; the legs' draw in a player's flight; the summary naming the cuts; Unity's EditMode suite green and its 0 B checks honest; the player built with `tools/build_latest.ps1 -PlanFile <a plan naming this branch> -Out <a scratch folder>` and its smoke PASS; nothing of the friend's world pushed.

## 3. The build

- [ ] 3.1 [orchestrator] Merge into `--w3-preview`; `tools/build_latest.json` on the trunk says what the build holds (a chore PR); tell the pilot the command.
