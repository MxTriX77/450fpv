# Review: bootstrap-godot-project (task 4.2)

**Reviewer:** QA Engineer · **Branch:** `game/bootstrap-godot-project` at `b1f46c3` · **Date:** 2026-09-23
**Verdict: changes requested.** I ran every spec scenario and all of them pass. The performance claims reproduce. Three small fixes are required before merge: R1, R2 and R3 below.

## Environment

- Godot `4.7.2.stable.mono.official.ed1daf0bf` (console exe), .NET SDK 10.0.301
- Ryzen 7 7735HS, RTX 4050 Laptop 6 GB, 16 GB RAM. Godot renders on the RTX 4050 (`Vulkan 1.4.312 - Forward+ - Using Device #0: NVIDIA`). The Radeon 680M drives the internal 1920×1080 144 Hz panel.
- Power: on AC (`PowerOnline True`, 100 %, not charging). Plan is Balanced (`381b4222-…`). The Windows power mode is **Best power efficiency** (overlay `961cc777-2547-4f9d-8174-7d86181b8a7a`), the most conservative mode. QA did not change it.
- All runs used fresh `git clone --branch game/bootstrap-godot-project` copies in the session scratchpad. I followed README.md literally in PowerShell with `$env:GODOT` set.

## Scenario results

| # | Capability · scenario | Result | Evidence |
|---|---|---|---|
| 1 | review-sandbox · Clean build | PASS | Fresh clone, `dotnet build game/Fpv450.sln`: exit 0, `0 Warning(s)`, `0 Error(s)`, 3.3 s |
| 2 | review-sandbox · Headless smoke run | PASS | `--headless --path game --quit-after 120`: exit 0, only the version banner, 0 `ERROR` lines. This held in README order (build → import → smoke) and in import → build order on a second fresh clone |
| 3 | review-sandbox · Load a patch | PASS | Headless: `Sandbox: loaded res://scenes/sandbox/test_patch.tscn at the origin`, 0 errors. Windowed: `TestPatch` at (0, 0, 0) and Ground hidden. The origin projects to (576, 358) in the 1152×648 view, horizontally centred and 34 px below centre. Cube1m, DoorFrame2m, Wall10m and Slab20m are all in the frustum and on screen, confirmed by screenshot |
| 4 | review-sandbox · Bad path | PASS | Three bad paths each exit 0 with exactly one `ERROR:` line naming the path: missing `res://scenes/sandbox/does_not_exist.tscn`, existing non-scene `res://src/ui/PerfOverlay.cs`, and prefix-less `scenes/nope.tscn`. Windowed: Ground visible = true, no patch, and the screenshot shows the grid placeholder |
| 5 | review-sandbox · Toggle | PASS (see R2) | `--selftest overlay` exit 0: F3 shows the overlay, then 5.0 text changes/s at 1152×648 and 4.6/s at 1920×1080, and F3 again hides it. Mutations O1 and O2 below show the show, hide and refresh checks can fail |
| 6 | review-sandbox · Budget on the dev machine | PASS with vsync off (see R3) | 1276 fps with 1 % low 364 at 1152×648; 742 fps with 1 % low 336 at 1920×1080 fullscreen. Full table below |
| 7 | review-sandbox · Capture | PASS | F12 wrote a PNG at the printed path. The PNG header says 1152×648, matching the window; fullscreen gave 1920×1080, also matching. Names like `20260923-051741-597.png` are sortable. Mutation O1 (half-size image) is caught |
| 8 | noclip-explorer · Base speed | PASS | Dev selftest: 6.000 m at 30 and 144 fps. My independent check ran the real engine loop with `--fixed-fps` 13, 30, 60, 144 and 240 and held the physical W key: 6.0000 m at every rate. At 144 fps the first hold read 6.0067 m because of one 8.06 ms engine step |
| 9 | noclip-explorer · Fast speed | PASS | Selftest 48.000 m; my check 48.0000 m at all five rates |
| 10 | noclip-explorer · Look clamp | PASS | Windowed, 40 frames × 300 px up: max pitch 89.0000° and camera `up.y > 0` on every frame, so the view never flips. Down reaches −89.0000°. 10 px back from the clamp gives exactly 88.0000°, so no overshoot is stored |
| 11 | noclip-explorer · Mouse release | PASS | Esc → mode VISIBLE. Five motion events while released leave the rotation unchanged. Esc → CAPTURED, and motion turns the camera again |
| 12 | noclip-explorer · Through geometry | PASS | I added real Jolt colliders: a 0.5 m `StaticBody3D` wall at z = −4 and a `WorldBoundaryShape3D` ground at y = 0. W for 1 s ends at z = −6.000; Q for 1 s ends at y = −3.000 |

The Controls requirement has no named scenario, so I checked it directly with physical keys:
- S, A and D each move 6.0000 m/s. E and Space move up, Q and Ctrl move down, each at 6.0000 m/s.
- W+D moves 6.0000 m, so the diagonal is not faster.
- Pitched −45°, W follows the view (dy −4.2426 m) and E stays on world vertical with no drift.
- Wheel up once gives 7.5 m/s. The floor is 1.0066 m/s and the ceiling 55.8794 m/s. Shift at the ceiling gives 447.03 m/s, which is ×8 of the current speed. 8 steps down then 8 up returns to exactly 6 m/s.
- `MouseSensitivity` is the single exported setting (0.1 °/px): 100 px turns yaw by exactly −10.0000°.

A first windowed run showed a 1.9° drift from the physical mouse. Accounting per device on a rerun showed that injected pixels alone reproduce the angles exactly.

## Budget measurements

| Run | Resolution | Vsync | fps | Avg frame | 1 % low | Draws |
|---|---|---|---|---|---|---|
| `--selftest overlay` | 1152×648 window | off | 1276 | 0.78 ms | 364 | 7 |
| `--selftest overlay --fullscreen` | 1920×1080 | off | 742 | 1.35 ms | 336 | 7 |
| Mutation runs (behaviour unchanged) | 1152×648 | off | 1277–1303 | 0.77–0.78 ms | 426–465 | 7 |
| Scratch copy, vsync line removed (two runs) | 1152×648 | on (project default), 144 Hz | 144 | 6.95–6.97 ms | **74 and 92** | 7 |
| QA frame-pacing probe, 5 s | 1152×648 | on | 143.8 (720 frames) | 6.954 ms | 100 (p99 9.86 ms, max 10.10 ms) | – |

The Game Developer's claim of about 1200 fps with a 1 % low of 330–400 is confirmed, and the budget of at least 144 fps with a 1 % low of at least 120 is met by a wide margin with vsync off.

With vsync on, no frames are dropped: 720 frames arrived in 5.000 s and no interval was 11 ms or longer. The intervals are spread evenly around 6.94 ms (49 frames between 3 and 5.5 ms, 44 between 8.5 and 11 ms). The overlay's wall-clock 1 % low picks up CPU-side present jitter, so under vsync it reads 74–100 fps. See R3.

## Robustness: do the selftests fail when they should?

All mutations were made in a scratch clone only, then reverted.

| Mutation | Selftest result | Should fail? | Caught? |
|---|---|---|---|
| M1 `DefaultSpeed` 6→7 and `FastMultiplier` 8→10 | **exit 0**: `7.000 m (expected 7 m) PASS`, `70.000 m (expected 70 m) PASS` | yes | **no** → R1 |
| M2 movement ×1.1 | exit 1: 6.600 m and 52.800 m FAIL | yes | yes |
| M3 fixed 1/60 step per frame instead of `delta` | exit 1: 3.0 and 24.0 m at 30 fps; 14.4 and 115.2 m at 144 fps | yes | yes |
| O1 refresh 300 ms, and F12 saves a half-size image | exit 1: 3.2 changes/s FAIL; 576×324 vs window 1152×648 FAIL | yes | yes |
| O2 F3 never hides the overlay | exit 1: "F3 again hides" FAIL | yes | yes |
| O3 vsync left on, behaviour otherwise unchanged | exit 1 at 3.0 text changes/s; a rerun passed at exactly 4.0/s, while a refresh counter showed the overlay really refreshed **4.8/s** | no | **false failure** → R2 |
| `--selftest bogus` | exit 2, one `ERROR:` line | yes | yes |

My throwaway speed check uses the spec's literal 6 and 48, and it failed M1, M2 and M3.

## Rulings on deviations

1. **csproj from Godot's internal generator: acceptable.** The output is exactly the SDK's own template: `Godot.NET.Sdk/4.7.2`, net8.0 (net9.0 for Android), `EnableDynamicLoading`. It builds with 0 warnings and is recorded in design.md. See N1 for a formatting nit.
2. **Extra actions `speed_up` and `speed_down`: acceptable.** The design's no-raw-keycodes rule requires the wheel to be an action as well, and the design's list ends with "…". They are bound to mouse buttons 4 and 5 (wheel up and down).
3. **Scroll range 1.007–55.9 m/s: acceptable.** Every reachable speed lies within 1–60 m/s, each step is exactly ×1.25, and 6 m/s stays on the ladder (verified). Clamping to exactly 1 and 60 would break both. See N2 for the README wording.
4. **1 % low as the average of the slowest 1 %: acceptable.** It's the stricter of the two usual definitions: in the probe it gave 100.0 fps where the 99th-percentile definition gives 101.5. It's documented at `game/src/ui/PerfOverlay.cs:7` and in the README.
5. **`Sandbox.cs` and `SandboxSelfTest.cs` in `game/scenes/sandbox/`: acceptable for this change.** The proposal's Impact lists `game/scenes/sandbox/`, the design specifies the `--selftest` mode, and the proposal leaves the test-framework choice to QA. Follow-ups are N4 and Q3.
6. **`renderer/rendering_method` dropped from project.godot: acceptable.** Godot only writes non-default values. At runtime the renderer is `Forward+` and `GetCurrentRenderingMethod()` returns `forward_plus`. `config/features` still lists "Forward Plus", and Jolt and the assembly name are kept.
7. **Raw `Key.*` in `game/scenes/sandbox/SandboxSelfTest.cs:53,77,84,103-104` (not declared): acceptable.** These are only in the test's key injector. It presses the physical key the spec names (F3, F12), so the test also checks the default binding. `NoclipCamera`, `PerfOverlay` and `Sandbox` use actions only.

## Findings

### Required

**R1 · `game/scenes/sandbox/SandboxSelfTest.cs:16` · Game Developer**
The noclip selftest computes its expected distance from the constants under test: `NoclipCamera.DefaultSpeed * (fast ? NoclipCamera.FastMultiplier : 1f)`. A change that breaks the spec therefore passes with exit 0 (M1: 7 m/s and ×10).
Fix: compare against the spec's literal values, `fast ? 48f : 6f`.

**R2 · `game/scenes/sandbox/SandboxSelfTest.cs:56-68`, with `game/src/ui/PerfOverlay.cs:45-47` · Game Developer; scenario wording · Orchestrator**
The refresh check counts label text changes. When the values are stable, consecutive refreshes produce identical text, so the check under-counts:
- 3.0/s (FAIL) and 4.0/s (borderline) under vsync, while the overlay really refreshed 4.8/s
- only 4.6/s fullscreen with vsync off, which is 23 changes against the 20 needed

The gate gives false failures and can't be trusted either way.
Fix: count refreshes directly, for example a counter on `PerfOverlay` incremented where `_lastRefresh` is set, and assert at least 20 in 5 s.
Orchestrator: reword the Toggle scenario (`specs/review-sandbox/spec.md:36`) from "its values change at least 4 times per second" to "it refreshes at least 4 times per second", so it matches the requirement sentence.

**R3 · `README.md:50,66`, `specs/review-sandbox/spec.md:38-40` · Game Developer (README), Orchestrator (spec)**
The budget holds only with vsync off, which is how `--selftest overlay` measures it (`SandboxSelfTest.cs:48`). The project default is vsync on. On this 144 Hz panel, F3 then shows 144 fps and a 1 % low of 74–100, below the spec's 120, even though no frames are dropped (see the budget measurements above). The user will see these numbers in task 4.1.
Fix: the spec states that the budget is measured with vsync off, and at which resolution. The README says the budget figure comes from `--selftest overlay` with vsync off, and that under vsync the 1 % low mostly shows present jitter.
Whether the overlay should display the vsync state is the orchestrator's call through the spec. This review doesn't ask for it.

### Nits (non-blocking)

- **N1 · `game/Fpv450.csproj:7`, `game/Fpv450.sln:1` · Game Developer.** The csproj has no final newline and the sln starts with a UTF-8 BOM, while `.editorconfig` sets `insert_final_newline` and `charset = utf-8`. Both are generator output; fix them the next time these files are touched.
- **N2 · `README.md:64` · Game Developer.** The README says "1 to 60 m/s", but the reachable range is 1.01–55.9 m/s.
- **N3 · commits `37b67c3` and `96b94fa` · Orchestrator.** Their `docs: …` subjects lack the `(scope)` that `docs/workflow.md:27` asks for. Fixing them would mean rewriting history, so this is a note for future commits only.
- **N4 · `docs/team.md:12` · Orchestrator.** No role's ownership covers `game/scenes/sandbox/`.

### QA follow-ups (QA-owned, non-blocking)

- **Q1.** The smoke run's exit code alone proves nothing. On a fresh clone before `dotnet build`, it printed three `ERROR: Cannot instantiate C# script …` lines and still exited 0. The README states both conditions correctly. QA will add a wrapper in `game/tests/` that fails on any `ERROR` line.
- **Q2.** `--selftest overlay` prints the budget but doesn't assert it. QA will own a benchmark that records the conditions (resolution, vsync, power mode).
- **Q3.** Move the selftests into `game/tests/` once QA chooses the test framework.

## Hygiene

- `.godot/` is ignored (`!! game/.godot/`) and never tracked. From `reference/`, only `README.md` is tracked. The diff has no logs, builds or binaries, so no LFS objects are needed.
- All `game/` files are LF in the index. `.cs` files use spaces with no tabs, and `.tscn`/`.godot` files use tabs. There's no trailing whitespace, and every file ends with a newline except N1.
- There are 11 commits as `Game Developer <gamedev@450fpv.local>` and 6 as `Orchestrator <orchestrator@450fpv.local>`. Each is subject-only with no body and no trailers, the longest subject is 70 characters, and each commit is one logical step. Game Developer commits touch only `game/` and README.md (task 3.4).
- Running Godot on the fresh clones created nothing outside the ignored `.godot/`. The repo's `git status` was clean after all runs.
- The screenshot folder held 11 PNGs: 5 from the Game Developer (04:59–05:04) and 6 from QA. All went to the Recycle Bin, and `%APPDATA%\Godot\app_userdata\450fpv\screenshots\` is now empty.

## Code quality (karpathy-guidelines)

- The change is 344 lines of C# in four files. There are no speculative systems or unneeded abstractions. The test hooks (`ResetPose`, `LastScreenshot`, the public overlay values) are small, and every one is used.
- Yaw and pitch are kept as floats, and the pitch is clamped before the basis is built, so the view can't flip (verified).
- The input map uses physical keycodes, so WASD also works on Ukrainian and Russian keyboard layouts.
- The grid-and-haze ground shader stays within "ground placeholder" and helps judge scale and speed.

## Re-review plan

After the fixes, I'll re-run M1 (it must now fail), O3 (no false failure), the full scenario set on a fresh clone, and the budget run.
