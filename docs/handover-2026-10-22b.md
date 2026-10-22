# Handover, 2026-10-22b (the fifth orchestrator session, halted by the pilot)

For the next orchestrator session. Read `CLAUDE.md` first, then this file. `docs/handover-2026-10-22.md` holds the older detail. The opening prompt is §7.

The pilot, at the halt: "finish the tasks and halt the work. i will continue this project later. if you have any build for me - create". The running agents were left to finish their current tasks; nothing new was started.

## 1. Where things stand
**The pilot asked for a test map (D-044):** "create a simple ahh map, so i test the flights and complex physics on something tangible". It supersedes D-042's "the test range is the G1 gallery": G4's noclip and G7's flights run over the test range (`p6-test-range`). The Godot yard and belt models are ported as stand-ins `Z01`–`Z13`.

**The flight core's base is on `unity/main`** (#124): `p6-flight-core` 1.1–1.3, the world-query interface (`p6-world-query-gallery` 1.1), and `p5-drone` 1.1–1.2 (the object sheet, the real `drone.json`). The lanes now branch from `unity/main`, not from the flight core.

| Change | State | Branch tip |
|---|---|---|
| `p1-v01-locust` | archived (#121) | — |
| `p6-flight-core` | 1.1–1.5 done (1.4: the Betaflight 4.5 controller at 4 kHz, DC motors on APC's curves, two packs; 1.5: drag, rotor H-force, the rotors' gyroscopic moment, the 40 A ESC limit, the stand-in wind; log format 3); next 1.4b (filters and sag compensation, `Runtime/Control/` only), 1.6 contacts, 1.7, QA 2.1 | `98ae449` |
| `p5-drone` | 1.1, 1.2 done and on `unity/main`; next 1.3 model and 1.4 stand (asset-artist), after merging `unity/main` | `c51eb5a` |
| `p4-feed` | 1.1, 1.2 done (the pass ported, still A/B passes over 26 stages; the lens passes); next 1.3, 1.4, 1.6, 1.7, 1.8 (1.8 decides the render size), QA, G4 | `efc01e4` |
| `p6-world-query-gallery` | 1.1 on `unity/main`; 1.2 the physics sign-off done (probe in `flightbench --probe`, findings W1–W8 in `review-physics.md`); next 1.3 after G1 merges, 1.4 (musts W1 wires, W3, W4), 1.5, QA | `b1a402d` |
| `p6-input-app` | 1.1 the radio done (raw Windows HID on its own thread, `tools/radioprobe`; no TX12 was plugged in, so it is tested on a recorded EdgeTX stream); next 1.2–1.5, QA | `46f5f16` |
| `p1-h01-adobe-house` | 1.5 done; **1.6 running** (merge `6b9d5c7` done, the twelve rebuilds); then QA 2.1, merge | worktree `1e8de2b` |
| `p1-g1-look-lock-gallery` | 1.3 waits for H01's merge (place H01); then QA 2.1, merge | `08a59e4` |
| `p6-test-range` | 1.1 the stand-ins and 1.2 the map, scene and build done (729 instances; 1 % low 138 fps or more; the build went to the pilot), stacked on G1's tip; next 1.3 (H01 as neighbours, after H01 merges), QA 2.1 | `0b7c568` |

## 2. Agents running at the halt (check their worktrees first: `git worktree list`, then each `git log` and `git status`)
Their handbacks reach the session that started them. If that session is gone, each worktree holds whatever they committed; start a fresh agent from the branch tip for any unfinished part.
- **H01 1.6** (asset-artist): `.claude/worktrees/agent-a5abaac23f09054af`, branch `unity/art/p1-h01-adobe-house`. At the halt it had committed the merge `6b9d5c7` and the rebuilds of K02, K03 and H01 (`1e8de2b`, pushed), and was building the next asset. The remaining rebuilds: R01, K07, V01, V13, K01, S04, L01, L03, E01 (1.6's list), then 1.6's checks and tick.
- **Idle worktrees kept for reuse** (each holds a Unity Library):
  - `agent-a09bfc6494fea1534` (`unity/art/p5-drone`): the drone's 1.3
  - `agent-a9924b0d589177745` (`unity/techart/p4-feed`): feed 1.3
  - `agent-a53833d3c4a0bfee1` (`unity/game/p6-input-app`): input 1.2
  - `agent-aeec90e27aebdfaba` (`unity/physics/p6-flight-core`): flight core 1.4b and 1.6
  - `agent-a2ae5a7b5570cde4e` (`unity/world/p6-test-range`): test range 1.3

## 3. Merge order and stacking
- **H01** merges after its QA. Then G1 1.3 places H01, G1 QA, G1 merges to `unity/main`.
- **The test range** is stacked on G1's branch: it merges after G1. Its 1.3 adds H01 once H01 is on its base.
- **World-query 1.3** starts once G1 is on `unity/main` (it converts the gallery's station table into the test range's placement form, design 7).
- **Flight core** sub-branches (`--aero`, then `--filters` for 1.4b) merge into `unity/physics/p6-flight-core` by orchestrator merge commits **with `-m`**. A further early landing on `unity/main` after 1.5 takes one QA pass, as #124 did.
- **`p5-drone`** and **`p6-input-app`** merge `unity/main` to get later flight-core work.

## 4. Findings to keep
- **The previous session's agents may still run at a handover.** All four of the last handover's agents were alive when this session began. Check `ListAgents` and the processes before restarting a task.
- **Memory:** two heavy Unity or Blender agents at most on this 16 GB laptop; three reached 33.2 of 34.3 GB committed. Agents check for 2.5 GB free before each Unity or Blender run.
- **Merge commits need `-m`.** `git commit --no-edit` after a conflict keeps a `# Conflicts` body, which QA blocks (the hook skips `#` lines).
- **Liftoff:** the loaded drone (6.18 kg) lifts off at 55 % on a full pack and 62.6 % at half charge, which matches the pilot's "about 62 %" for a loaded quad; nominal (4.83 kg) is 47–49 %. With no current limit a motor draws about 47 A at full duty, above its rated 40 A (1.5 sets the ESC policy).
- **The feed:** Godot's fine structure needs the full-resolution read (an FPV render of 2754 × 1549); 1.8 measures the cost and decides. The A/B sheets are local in `unity/Logs/feed-ab-1.2/`.
- **The latch rule** gained a guard (K§7.5): refresh only while the part's core is outside the shape.
- **The 40 A ESC limit** (1.5) brings full thrust to 11.93 kgf and the first 50 ms of full yaw to 1289 °/s² (steady 745 °/s²); the G7 yaw question picks the policy.
- **Drag against the pilot's cruise:** with clip P's 22.7° at 13 m/s pinned, 17 m/s needs 33.6° of pitch, while K§8.2 (cruise 17–22 m/s) points to less drag. Left to `p7c-wind`'s T0 (noted in tasks.md). `drone.json` has no camera position yet, so `AirVelocityAtCamera` is taken at the body origin.
- **The input lane's log change clashes:** `p6-input-app` 1.1 raised `LogFormat.Version` to 2 (`StickSample.Failsafe`, header tag 10 for the input source, device and report intervals), while the flight core is at format 3. Merging `unity/main` into the input branch after the core lands again needs version 4 and a physics reading of the input agent's edits to `StickSample`, `LogFormat`, `LogHeader`, `FlightSummary` and the log tests. Its one Mono failure (`ReplayMatchesTheFlight`) is the flake the core fixed in `313dc43`. Design 2 should say the interval histogram covers the 10 s before the flight. `CLAUDE.md`'s verification block lacks `dotnet test tools/inputtests`.
- **The test range:** grass (S04) lies only along the posadka's verges, since its patches read as square tiles from the air, and most of the map is bare flat floor; E01's ground patch shows as a pale rectangle. The first launch after a clean build loads in 11.9 s (PLT-06 asks ≤ 10 s); one launch once took 216 s and did not repeat. Its preset test cannot press keys in EditMode (a PlayMode test is needed). World-query 1.3 should hash only `ground`, `instances` and `start_points`. The world-builder edited the stand-ins' culls (600 m) and the asset-artist's port test and default: QA reads them.
- **Stand-ins:** the importer turns any texture name containing `_rough` into a single red channel (a trap for recipe names). The optional `stand_in` metadata record (with Godot's snag flag) needs a physics reading in world-query 1.3 or flight core 1.6.

## 5. Waiting on the pilot
- **The build:** the test range player, `unity/Builds/TestRange-preview-1/450fpv-test-range.exe` in the main checkout (local, git-ignored; keys in `docs/test-range.md` on its branch). Noclip only: no feed, no drone, no flight yet. Their word on it is the test range's first reading, recorded in `docs/style-rules.md`.
- **G7 question** (logged): the first-instant yaw kick. G4 and G5 questions as before.
- **The letter map's T row** (`reference/_frames/letters.md`, `index.md`): still unanswered.
- **The old session** "450fpv phase 1 continuation 2" is idle; archive it only on the pilot's yes.

## 6. Next steps
1. Land each handback (§2): push, merge the sub-branch with `-m`, keep or remove the worktree.
2. **Test range:** 1.2's build to the pilot; after H01 and G1 merge, 1.3 and QA.
3. **H01 → G1:** QA 2.1, merge; G1 1.3, QA, merge; archive H01 and G1.
4. **Flight core:** 1.4b beside 1.5; then 1.6 contacts (the sign-off's notes in tasks.md), 1.7, QA; land again on `unity/main`.
5. **Feed:** 1.3, 1.4, 1.6, 1.7, 1.8, QA, then G4 over the test range.
6. **Drone:** 1.3 model, 1.4 stand (merge `unity/main` first; `drone.json`'s motor fields changed in flight core 1.4, the geometry did not), 1.5, 1.6, QA, then G5.
7. **Input and app:** 1.2–1.5, QA; the app's flight session runs over the test range.
8. **World queries:** 1.3 after G1, 1.4 (W1, W3, W4), 1.5, QA.
9. Propose `p7b-airframe` and `p7c-wind` once 1.5 lands, then a G7 gate change when the lanes come together.

## 7. The next session's opening prompt

> Continue 450fpv from `docs/handover-2026-10-22b.md` (§1–§6): D-042's lanes toward G4, G5 and G7, D-044's test range, and closing G1 with H01. First check the worktrees in §2: `ListAgents` for a live earlier session, `git worktree list`, then each worktree's `git log` and `git status`.
> - Push what is committed.
> - Start a fresh agent for an unfinished task from its branch tip, naming the commits and uncommitted files it inherits.
> - Then continue §6, in parallel lanes: the flight core, world queries, input and app, the feed (G4), the drone (G5), the test range, and H01 into G1.
> - Keep the rules:
>   - D-033 tiers; D-040 (look misses become stand-ins); D-041 (look first, shared pieces first, lean notes)
>   - every subagent on Opus 5.5, one fresh subagent per unit
>   - at most four in parallel, and no more than two heavy Unity or Blender agents (memory); light .NET agents count less
>   - you open and merge every PR; merges with `-m`
>   - `git lfs checkout`, a running Unity Hub, AC power and 2.5 GB free RAM before any Unity work
>   - letters only and never a place
>   - port Godot code, never merge it
>   - call the agents "they"
