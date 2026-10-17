# Proposal: setup-sim-tools

**Owner:** physics-engineer · **Reviewers:** world-builder (shared core), qa-engineer
**Requirements:** PLT-01, PLT-04 (determinism groundwork), PRC-10 · **Decisions:** D-005, D-006, D-019 · **VISION:** §physics ("physical, seeded, logged")

## Why
The sim core must be engine-free and testable without Unity (D-006). Phase 0 needs the package skeleton and the .NET tools that compile the very same sources, so every later physics and world change lands on a tested, Unity-independent build.

## What Changes
- The embedded package `unity/Packages/com.fpv450.sim` with `package.json`, a Runtime assembly definition set to `noEngineReferences: true`, and a Tests assembly definition for Unity's test runner.
- A first `DetMath` class: the documented home of the deterministic math rules (IEEE basic operations only, no `System.Math` transcendental calls, frame and handedness notes from D-019). Phase 0 ships only what the empty test needs.
- `.NET` tools under `tools/`: `simcore` (a `netstandard2.1` library compiling `Runtime/**/*.cs`), `simtests` (NUnit, compiling the package's `Tests/**/*.cs`), `worldbench` and `flightbench` (console apps with `--golden` and `--bench` entry points that report "no cases registered yet").
- One shared props file pinning the language version Unity accepts, so code that builds under `dotnet` also builds in Unity.

## Capabilities
### New Capabilities
- `sim-core-build`: how the engine-free sim core is laid out and built, inside Unity and outside it.

### Modified Capabilities
- none

## Non-goals
- No flight model, world queries, logging or replay (phases 3, 6, 7).
- No `tools/replay` yet (phase 6).
- No benchmarks with real cases; the bench entry points only exist so the verification commands are real.

## Impact
New files under `unity/Packages/com.fpv450.sim/` and `tools/{simcore,simtests,worldbench,flightbench}/`. The Unity skeleton change (`setup-unity-skeleton`) consumes the package and runs its tests in EditMode.
