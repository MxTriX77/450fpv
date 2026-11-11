# Proposal: p7h-step-budget

**Owner:** physics-engineer · **Reviewers:** game-developer (the session host's line in `Player.log`), qa-engineer
**Requirements:** PLT-04, INP-04, LOG-01 · **Decisions:** D-087, D-091 · **Plan:** `docs/final-world-plan.md` §3, units L4 and L5

## Why
The pilot feels the model-79 build turn sluggish, as if the radio lagged (D-087). `FlightScheduler.RunDue` runs every step it owes, back to back: once steps cost more than 1 ms, the sim falls behind real time and stays behind until it catches up, so the controls arrive later and later. Two gaps:
- **No measurement in the player:** step times exist only from the .NET benches. The Mono player was last measured at 400–1000 µs a step, before the house cuts.
- **No bound on the lag:** a slow stretch costs as many milliseconds of control lag as it builds up.

The acceptance gate (D-091) needs both, on any map.

## What Changes
- **L4, the step's own clock:**
  - the scheduler times each step's `Advance` with the host clock it already owns
  - it keeps the count, the overruns past 1 ms, a fixed-bin histogram (p50, p99, p99.9) and the max, with no allocation
  - the log's end record carries them (format 72)
  - the summary reader prints them
  - the session host writes one line per flight to `Player.log`
- **L5, the lag cap:**
  - when the scheduler finds itself behind by more than a bound B, it moves its time base forward so it owes at most B, instead of running the backlog
  - every step still runs as a real 1 ms step, so the motion stays continuous, and a replay stays bit-exact (the sticks are logged per step)
  - the jump is logged as a `Hitch` with a new `HitchCause`, not a new event kind
  - the pose buffer is told the new time base
  - B is derived from the control chain's latency budget (INP-04), written with its derivation, never tuned

## Impact
- `unity/Packages/com.fpv450.sim/Runtime/Flight/FlightScheduler.cs`, `IFlightRecorder.cs`, `StepTrace.cs` (the cause), `Runtime/Log/` (the end record, format 72, and the summary reader), `PoseBuffer` (the time base), and the session host's one log line.
- No physics change: no model number. Goldens are unchanged, since timing never feeds the state.
- L5 waits for L1's report (`docs/reports/lag/model-79-flights.md`) and for L4 to land, because both touch `RunDue`.
