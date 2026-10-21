# Interim QA review: the flight core's base

**Verdict: blocking.** One hygiene finding (1): a commit-message reword on this branch, minutes of work. The code passes every check I ran. That includes Mono parity beyond the brief: logs recorded on CoreCLR replay bit for bit in Unity's Mono.

**Scope:** tip `e5b4541` of `unity/physics/p6-flight-core`.
- `p6-flight-core` 1.1, 1.2 and 1.3
- `p6-world-query-gallery` 1.1
- `p5-drone` 1.1 and 1.2
- the orchestrator's merges `f38a809` and `162e7dd`, and its fix `e5b4541`

Out of scope: 1.4 to 1.7 and every change's 2.1 review.

**How I checked:**
- A scratch harness (never committed) uses only the core's public API: my seeds, loadouts, start states and stick scripts, and real flight, feeder and renderer threads.
- An independent Python parallel-axis build and seed-stream implementation, written from the specs.
- A scratch Mono probe, run once in the EditMode suite and then deleted (`git status` clean after).

## Findings

1. **Blocking: hygiene.** Merge `162e7dd` has a body: git's `# Conflicts:` list, two lines under the subject (`git log -1 --format=%B 162e7dd`). That breaks AGENTS.md's subject-only rule.
   - **Cause:** `tools/hygiene/precommit.py:264` ignores `#` lines after the subject, taking them for git's editor template. `git commit --no-edit` uses cleanup mode `whitespace` and keeps them (`commit.cleanup` is unset).
   - **Fix now:** only this branch contains `162e7dd` and `e5b4541` (`git branch -r --contains`). Reword the merge, re-apply `e5b4541`, and force-push this one branch. No other lane is touched.
   - **Suggestion (orchestrator):** set `commit.cleanup=strip` per clone, or make the hook reject `#` lines in a merge message.
2. **Not blocking: the rigid body gains kinetic energy in a torque-free tumble** (owner: physics engineer).
   - `RigidBody.cs:35` turns the attitude at the step's starting rate ω* = I⁻¹·h. That keeps |h| exact: world momentum holds within 1e-11 over 60 s on every loadout.
   - Energy is not held. On the L1 legs-off body, a 4 rad/s tumble gains **+11.3 %** in 60 s at dt 1 ms and +4.85 % at 0.5 ms (first order), which is **+0.14 % per second**. At 2.6 rad/s the gain over 60 s is 0.2–3.9 % across the seven loadouts.
   - No scenario asks for energy, and the closed loop will mask it in flight. It still shapes a disarmed or crash tumble, which drifts toward the minor axis.
   - **Suggestion for 1.7:** add an energy bound to the free-flight test, or move the rotation to a midpoint rate.
3. **Not blocking: only one test guards the gyroscopic term** (owner: physics engineer). Experiment: I replaced `RigidBody.cs:39` with `rate = I⁻¹·h` (no transport) and ran the checks, then reverted.
   - **The free-body golden:** its hash moved, but every physical check it prints still passed (position 5.0e-11 m, norm 2.2e-16, attitude 1.0e-5 rad, rate drift 7.6e-16). Its body is an isotropic cube (`FreeBodyCase.cs:31`), where ω × I·ω ≡ 0, so a re-record audit that leans on those checks would bless the change.
   - **The spec's conservation scenario** (principal-axis spins) passes the wrong model too.
   - **Only `ATumbleNeedsTheGyroscopicCoupling` failed**, 1 of 6 rigid-body tests.
   - **Suggestion:** a second golden case on drone.json's L4 body (with its I_yz), tumbling off its principal axes.
4. **Not blocking, latent: a log cut between a step and its events will report a divergence** (owner: physics engineer, before 1.6).
   - `FlightReplay.cs:157` checks `matched < expected` after the loop whether or not the log is complete. A cut that falls after a step record but before that step's events then exits 1 (Diverged) instead of 5 (MatchCut).
   - Nothing triggers it today, since no producer makes step events before 1.4 and 1.6 (my seven cuts all read as MatchCut).
5. **Not blocking, for 1.6: runtime objects are not in the log** (owners: world builder and physics engineer).
   - `FloorWorld.cs:10` says runtime objects stay outside the content hash because "the log records them". The header has no entry for them (`LogHeader.cs:81`, tags 1–9).
   - A flight with the stand would then replay against a world without it under the same hash. Design 7 asks for the stand to be logged as asset, pose and order.
6. **Nits.**
   - `DetMath.cs:71` says `Cbrt` lands "within an ulp". I measured up to 2 ulp from `Math.Cbrt` over 200 000 values in 1e-20 to 1e20 (cbrt 2 comes out one ulp high). Exact cubes are exact, and determinism is unaffected.
   - The object sheet's packs are 0.91 kg and its empty spool 0.30 kg; drone.json has 0.92 and 0.31 kg, from the notes' 71 g P50B cells and 1.0 − 0.69 kg. Every loadout's mass comes out 0.3–0.9 % over the sheet, inside the 1 % test. One of the two should carry the other's figures.

## Spec scenarios

| Scenario | Result | Evidence (mine) |
|---|---|---|
| Render rate does not touch the flight | pass | Real threads at 30.1, 60.1 and 240 fps, plus a 250 ms renderer stall and a 250 ms flight-thread stall (one 250-step catch-up). Headers identical; 12 000 step records identical with `stick.host_us` taken from each flight's start; the only differences are hitches; all five logs replay bit for bit. |
| Nothing outside the state enters a step | pass | The suite's scan passes. My greps over all of `Runtime/` (World included) find no `float`, `Math.` outside DetMath, FMA, Numerics or intrinsics. The one `HashSet` (`FloorWorld.cs:44`) is membership-only, at construction. |
| Every draw is named and bounded | not testable | No model draws before 1.5. The mechanism holds: 10 000 `Draw` calls all stay inside their bounds and are recorded with stream, parameter and index; reopening a stream is refused. |
| Adding a stream shifts nothing | pass | A new stream opened first with 500 draws, the other two reopened in reverse order: their 2 000 draws are bit-identical. Seed + 1 gives 0 of 1 000 equal. Keys and draws match my Python for 9 seed and name pairs and the suite's 3 pinned rows. |
| The bootstrap drone matches K§5.2 | pass | 4.808 kg (+0.17 %); roll / pitch / yaw 0.04432 / 0.05429 / 0.05007 (+0.73, +0.54, +0.14 %); pitch above roll; yaw / roll 1.130. Python and C# agree. |
| Free flight conserves what it should | pass | 14 bodies (L1 to L7, legs on and off) × 3 principal axes × 60 s at 2 rad/s: worst momentum change 1.0e-11, worst norm error 3.3e-16, the CoM still within 1e-15 m. Energy: finding 2. |
| A truncated log still reads | pass | Seven cuts. Inside the prologue or the header: unreadable (4). After the header, mid-record, inside a block header, after a whole record, or with no end block: the summary reads to the last whole record and names the byte, and replay exits 5. |
| Replay matches the flight | partly | My 120 s and 30 s flights, virtual and real time, replay bit for bit (seeds 0xA11CE5EEDF00D001, 0xFFFFFFFFFFFFFFFF, 0x0, 0x7777777777777777; L5 legs on, 2 kg legs off with an empty coil, no payload; tweaked FcSettings). A changed drone.json is refused, naming both hashes (exit 3); a CRLF copy still matches; one-ulp flips in state or trace fields are named by step and field (exit 1). Contacts and a landing come with 1.6. |
| Re-derived for letter T's layout (p5-drone) | pass | All 14 loadouts sit on the sheet (mass within 1 %, inertia within 3 %, the coil within 1.3 mm). Python and C# agree to 1.4e-15. Over 0–2 kg × three coil states, the spool sits 0.086–0.141 m above the CoM legs on and 0.025–0.079 m below it legs off. 181 numbers are labelled; the only bare number is `schema`. |

`p6-world-query-gallery`'s own scenarios belong to its 1.3–1.5. Its 1.1 checks pass: the goldens, the tests, and the EditMode compile.

## Requirements

- **Met:** PHB-02, DRN-03 (the physics half; the generator is p5-drone 1.3), LOG-02 (free body), LOG-05 (no user, machine, path or date in any of my logs' headers).
- **Partly met:**
  - PHB-01: the worker forced early is 1.7's.
  - LOG-01: the event producers come with 1.4–1.6. The size is 21.36 MB a minute raw, under 50.
  - PLT-04: the world-query share comes with 1.6.
  - INP-04: the report-interval measurement is p6-input-app's.
  - LOG-04: Mono matches here; the second machine is QA's before release.

## Interfaces (design 1)

| Interface | Result | Detail |
|---|---|---|
| `StickSample` | as fixed | µs host stamp; roll, pitch and yaw in −1…1, throttle in 0…1, the arm switch. The sample in force is the latest stamped at or before the step, and it is logged every step. |
| `FcSettings` | as fixed | Betaflight CLI names. The sim-default column of fc-profiles.md, the builder's PIDs, and Betaflight 4.5 defaults otherwise, checked field by field. |
| `PoseFrame` | as fixed | Step, position, attitude, velocity, body rate, rotor speeds and the arm state, plus p5-drone's hooks. Interpolated, never extrapolated. No torn or backward pairs in about 5 400 frames of the render-rate check. |
| `FeedSignals` | as fixed | Field for field with p4-feed's `IFeedSignals`; the `Live` bits equal the `FeedSignal` indices. |
| `IWorldQuery` seam | as fixed | `FlightWorld` wraps p6-world-query-gallery's interface. |

The frames check out by hand: +Y takes north to east, +x puts the nose down, +z lifts the right side. Motor order and spin are Betaflight's Quad X with props in.

## Commands and output (trimmed)

```
dotnet test tools/simtests                       Passed 136/136
dotnet test -c Release tools/simtests            Passed 136/136
dotnet run [-c Release] --project tools/flightbench -- --golden
  free-body: hash 0x50ED4B05A5D02F20 (matches), Debug and Release: PASS
dotnet run [-c Release] --project tools/worldbench -- --golden
  ALL PASS (5 cases), Debug and Release
dotnet run -c Release --project tools/flightbench -- --bench    (run twice)
  AC, Balanced plan, best power efficiency mode, CPU 3201/3201 MHz
  free-body: p50 1.60 / 1.40 µs, p99 3.20 / 3.00 µs, 0 B: PASS
  logged-step: with its record p50 2.80 / 1.90 µs, p99 30.50 / 17.90 µs, flight thread 0 B, 0 ring waits: PASS
dotnet run -c Release --project tools/worldbench -- --bench
  AC; floor.step median 9.70 µs, p99 31.60 µs, 0 B; 100 000 capsules 55.25 ms, rays 13.19 ms
replay <log> --drone <drone.json>        match: 120000 steps and 0 events, bit for bit   (each of my flights)
replay qa-D (bootstrap) --drone assets-src/drone/drone.json
  refused: drone.json differs: the log was flown with drone.json 0x8976130B806EBB0E, this replay has 0xA3015F07E918911F
replay <log with position.y flipped by one ulp>
  diverged at step 54321 (t = 54.321 s), position.y: logged ...F488, replayed ...F489
Unity EditMode (battery: the RAM window came first; a correctness run, no timing)
  247/247 passed; Fpv450.Sim.Tests 141/141 with the probe's 5
  QA-PROBE free-body Mono 6.13.0 X64: 0x50ED4B05A5D02F20
  QA-PROBE mass bits Mono 6.13.0 X64: 0x0178F9EE2664823D (CoreCLR Debug and Release: 0x0178F9EE2664823D)
  QA-PROBE replay of qa-A, qa-B, qa-A-realtime (recorded on .NET 10.0.9): match, 120000 / 120000 / 120013 steps, bit for bit
python tools/hygiene/precommit.py --tree HEAD      1456 files scanned, 0 findings
openspec validate p6-flight-core --strict          valid   (p5-drone, p6-world-query-gallery: valid)
```

**Other hygiene:**
- The package is engine-free (`noEngineReferences: true`, no `UnityEngine` in Runtime).
- No GPL or licence markers in the diff. The ported files name their `refs/godot/main` source.
- Nothing from `reference/`, `Library/`, `Temp/` or `Logs/` is committed.
- Every commit carries its role identity and a single-line message, except `162e7dd` (finding 1).
