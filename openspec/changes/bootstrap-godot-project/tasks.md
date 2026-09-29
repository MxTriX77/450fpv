# Tasks

## 1. Engine

- [x] 1.1 [orchestrator] With the user's permission, reinstall Godot 4.7.2 .NET via WinGet and record the console exe path as `GODOT` in the README. Verify that `& $env:GODOT --version` prints `4.7.2.stable.mono`

## 2. Project

- [x] 2.1 [game-developer] Generate the C# solution with the headless editor, keeping the placeholder settings. Commit `Fpv450.csproj` and `Fpv450.sln` (not `.godot/`). Verify that `dotnet build game/Fpv450.sln` exits 0
- [x] 2.2 [game-developer] Add the input-map actions to `project.godot` and verify they're listed in the file

## 3. Sandbox

- [x] 3.1 [game-developer] Add the sandbox main scene (sky, sun, ground placeholder), the `--scene` argument loading and `test_patch.tscn`. Verify the load and bad-path scenarios through headless runs
- [x] 3.2 [game-developer] Add the noclip camera script and the `--selftest noclip` mode. Verify the base and fast speed scenarios within ±5 % at 30 and 144 fps
- [x] 3.3 [game-developer] Add the F3 performance overlay and the F12 screenshot. Verify the toggle and capture in a windowed run, and record the empty-sandbox fps and 1 % low on the dev machine
- [x] 3.4 [game-developer] README: how to build, smoke-run and open a patch in the sandbox. Verify by following it from a fresh clone

## 4. Review

- [x] 4.1 [waived] User camera sign-off waived: the pilot reviews only UAT and MVP deliverables (2026-09-23). Camera feel gets judged at UAT-2 in the real world
- [x] 4.2 [qa-engineer] Review against spec scenarios: build, smoke run, load/bad path, speeds, overlay, budget and screenshot. Write `review.md` with the verdict
