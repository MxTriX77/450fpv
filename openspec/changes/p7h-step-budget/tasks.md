# Tasks: p7h-step-budget

Order: 1.1, then 2.1 once 1.1 has landed and L1's report (`docs/reports/lag/model-79-flights.md`) is in, then 3.1. The branch is `unity/physics/physics-integration-2--step-budget`, cut from the build line. Numbers come from the orchestrator (D-072, D-074): format **72** is this change's; no model number (no physics change).

## 1. The step's own clock (plan unit L4)

- [x] 1.1 [physics-engineer] Time each step's `Advance` in `FlightScheduler` with its `IHostClock`: count, overruns past 1 ms, a fixed-bin histogram (p50, p99, p99.9) and the max, 0 B. Carry them in the log's end record (format 72), print them in the summary reader, and have the session host write one line per flight to `Player.log`. Tests: the two scenarios of "The player measures its own steps". Checks: `dotnet test tools/simtests`, flightbench `--golden` (unchanged), the EditMode suite.

## 2. The lag cap (plan unit L5)

- [x] 2.1 [physics-engineer] Derive B from the control chain's latency budget (INP-04) and write the derivation in `design.md`. Add the drop to `RunDue`, a new `HitchCause`, and the pose buffer's time base. Tests: the two scenarios of "A late flight never builds growing lag". Same checks as 1.1.

## 3. Review

- [ ] 3.1 [qa-engineer] Review 1.1 and 2.1 against the spec, run the checks, confirm 0 B and bit-exact replays.
