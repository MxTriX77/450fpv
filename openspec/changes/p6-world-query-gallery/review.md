# QA review: p6-world-query-gallery (task 2.1)

**Verdict: changes requested.** The queries are right where I could check them against an independent answer, and they are deterministic:
- The grid, the wire search and the gap model equal a brute-force scan bit for bit on 50 000 capsules and 50 000 rays.
- Pose parity holds in the sim and in the scene.
- Nothing falls through a patch or a band off its exact edge lines.
- The goldens hold in Debug and in Release, four threads get one thread's bits, and the queries allocate 0 B.
- The floor golden's re-record is justified case by case: all 10 moved cases come from the box minimiser alone, and each is now exact.

Three findings block the merge:
- **B1:** the budget scenario fails on my runs.
- **B2:** a merge with today's `unity/main` breaks the test range golden's content hash.
- **B3:** the wire scenario's 1 µm clause fails on grazing crossings, and the give-up behind it reaches every kind of shape except a box's face.

**Scope:**
- The change's own 11 commits, `dd98d012`…`97f353f1`: 22 files, tasks 1.4 and 1.5. Task 1.1's golden and 1.2's probe were re-run.
- The branch also carries `p6-test-range` (content equal to `7d47968e`) and the G1 gallery stack. Neither is on `unity/main` or approved by QA, and both are out of this review (see "Merge conditions").

**How I checked:**
- Every command below was run by me; the author's output was not used.
- **A scratch harness, never committed.** It compiles the package's `Runtime/` sources into one assembly with my checks, at four commits:
  - `3bb49a1`: QueryVersion 1
  - `26145af`: Clear only
  - `40bc591`: QueryVersion 2
  - `HEAD`
  - plus a copy of `40bc591` with the box minimiser switched off
- **Exact arithmetic.** A Python check in rationals for the golden audit.
- **Mutation runs and the merge check** ran in throwaway worktrees.
- **Unity.** The Library was copied from the world builder's worktree at the same commit; nothing was imported anew.
- No tracked file in this worktree was edited before this one. `git status` was clean.

## Findings

### Blocking

**B1. The budget scenario fails on my runs: K§10.5's batch along E01's run reads 67.7–72.4 µs at the median against 60 µs.** Owner: the world builder, or the orchestrator for the scenario.
- **Three runs** on AC, with the Balanced plan, best power efficiency mode and 3201/3201 MHz. Other agents' load, read from WMI, was 45–57 % CPU.

  | median / p99, µs | run 1 | run 2 | run 3 | the builder's |
  |---|---|---|---|---|
  | floor step: unchanged code since 1.1, the load gauge | 15.6 / 46.0 | 10.3 / 29.6 | 14.0 / 38.3 | 9.7 |
  | K§10.5, inside V01_L's crown | 58.8 / 152.8 | 58.6 / 151.8 | 55.7 / 151.1 | 53.9 / 144.4 |
  | K§10.5, round H01_D | 41.2 / 100.0 | 40.0 / 116.1 | 35.8 / 101.7 | 31.9 / 88.2 |
  | **K§10.5, along E01's run** | **72.4** / 234.9 | **71.0** / 265.6 | **67.7** / 206.9 | 58.1 / 196.1 |
  | K§10.5, posadka at 2 m | 20.1 / 39.7 | 16.8 / 49.2 | 19.5 / 51.1 | 13.4 / 36.6 |
  | physics' pattern, crown | 20.3 / 85.1 | 22.0 / 88.8 | 16.2 / 81.1 | 16.7 / 85.4 |
  | physics' pattern, E01 | 20.7 / 149.2 | 19.8 / 148.1 | 17.9 / 124.0 | 14.5 / 117.5 |

- **The load does not explain it.** Run 2's floor step was within 6 % of the builder's, and E01 still read 71.0 µs, 22 % above the builder's 58.1. A pass by 3 % against a median that moves ±30 % from run to run (the builder's own note) is not a budget met.
- **Where the cost is.** The fiber's 32 laid segments dominate it (task 1.5's Open note).
- **The pattern 1.6b flies at G7** (physics' pattern) holds with a margin of nearly 3: a median of at most 22 µs everywhere.
- **Fix:** either
  - bring K§10.5's E01 median under 60 µs with a margin on a run like mine, or
  - the orchestrator restates the scenario with a decision: judge physics' pattern now, and K§10.5's fiber at phase 7d, which decides how the fiber queries.

  Either way, the p99 (up to 266 µs) goes to 1.6b's whole-step PLT-04 check.

**B2. A merge with today's `unity/main` moves the test range's content hash, so `worldbench --golden` goes red on the trunk.** Owner: the world builder, before the PR.
- `unity/main`'s `94c638e7` appends `carbon_composite` to `assets-src/physics_materials.json`. The material names are hashed (design 6).
- On a throwaway merge of `origin/unity/main` into `97f353f1`:
  - `test-range/content_hash` fails, `6114242e06475fb5` against the golden's `d6de238216ceb4b9`
  - `test-range.ground`, `.contacts`, `.contacts_overflow` and `.rays` pass bit for bit: the new row is last, so no material id moved
  - simtests pass 280 of 280, the trunk's flight goldens at model 15 included
- **Fix:** merge `unity/main` into the branch and re-record only `golden/test-range.txt`'s `content_hash` line. QueryVersion stays 2, since no query math changes. Name this cause where the re-record is recorded; QA re-checks that only that line moved.

**B3. A "first touch" is not always a touch: after 64 steps the sweep gives up on every kind of shape except a box's face.** Owner: the world builder; the physics engineer says what 1.6b needs.
- **The spec's wire scenario fails its clause "Distance within 1 µm of 0".**
  - 4 of 39 248 crossings fail, all grazing (at 0.990–0.998 of R + r, 26–132 µm deep at the deepest).
  - Each reports Time 0.0003–0.005 of the step before the true first touch, with Distance 1.4–13 µm.
  - Every one of the 39 248 is still caught; none crosses unseen.
- **The same give-up elsewhere** (`ShapeMath.Sweep`, `MaxAdvance`; `Clear` serves only boxes):

  | case | what the query returns |
  |---|---|
  | a skid 0.1 mm over the open plane at 10, 20 and 30 m/s, never touching | a fresh first touch every step: Time 0.64, 0.32 and 0.213, Distance 0.1 mm |
  | a 5 mm sphere sliding 20 mm a step 0.2 mm off Z07's trunk cylinder | Time 0.836, Distance 0.34 mm |
  | the floor golden | 3 of its 330 first touches are give-ups: a box corner at 2.67 mm (case 1381), the plane at 0.139 mm |
  | 40 000 capsules near test range shapes at physics' 30 mm moves | box 10 of 613 first touches, capsule 6 of 375; at 0.3 m moves, box 95 of 6243, capsule 77 of 3694 |

- **Why it matters.**
  - `IWorldQuery` promises "the first t at which it touches the shape (within 1 µm)".
  - Physics latches every Time < 1 contact (review-physics P1) and calls a latched pair that leaves while its plane penetrates a tunnel event. A tangent plane latched at an edge, a corner or a trunk reaches past the shape, so a phantom latch can become a false tunnel event, and a tunnel event is always logged as a bug.
  - Each give-up also costs 64 closest approaches, a p99 cost.
- **Fix:**
  - close the plane's sweep exactly (its distance is linear in t)
  - give spheres, capsules, cylinders and wire segments a bound that ends the creep, as `Clear` does for boxes
  - add tests for the 0.1 mm skid at 20 m/s and for a grazing wire crossing at 0.998 of R + r
  - if a give-up must stay, state it in `IWorldQuery` and in the scenario: Time early, Distance up to the step's move over 64

### Should

**S1. The line-contact rule on a box face (review-physics W5) has no test, and physics' skid rule relies on it.**
- **Two mutations pass all 32 world tests and both goldens:** `BoxMinimizer` answering a flat piece at its end (`u1` for `u0`), and a tie keeping the last piece (`f <= best`).
- **The code is right today.** My check puts the point at A's foot on an axis-aligned and on a yawed box, in both orders and with A off or over the face, within 6.7e-16 m.
- **Add a unit test.** `Raycast`'s gap rule has none either: keeping the plane inside patches and bands passes the unit tests, and only the golden's hash catches it.

**S2. The spec's 100 000-capsule and 100 000-ray budgets were restated by the implementer (`0cdaf6e`).** Owner: the orchestrator, to accept or reject.
- K§10.4 says "100k capsules: < 100 ms", with no pattern. The restatement reads them "of physics' pattern".
- Random-pose capsules in the golden's spot boxes take 243–270 ms, and rays 103–136 ms (my three runs and the builder's).
- Physics' pattern gives 66.8–74.6 ms and 38.9–44.8 ms. Reading it in the physics pattern follows K§10.4's own qualifier on ground samples and is reasonable, but it relaxes a budget. That needs the orchestrator's acceptance, not only the implementer's edit.

### Low

**L1. A ray lying exactly on a boundary line falls through.**
- **Where:** 2420 of 2 902 737 down rays on a 5 cm grid over every patch and band find nothing. All lie on lines where a box face meets a gap's edge or another box face flush:
  - E01's footprint at x 44.0 and 79.25
  - S04's z edges
  - Z04's band at x −163.5
  - a few seams between flush boxes
- **Off those lines nothing falls:** 188 617 552 rays, off the lines by 0.3 mm (2 mm grid over E01, 1 cm elsewhere), all hit.
- **A foot is held there:**
  - 2379 of 2420 feet on those exact points are supported
  - the 41 on Z04's band edge meet only the side of its wall, which stands 5 mm proud
  - 55 344 feet across the patch edges at ±0.5, ±2 and ±5 mm are all supported
- **Fix:** count a ray lying in a face's plane as a hit, or make the gap's edge open.

**L2. The parity tests check less than they say.**
- `TestRangeWorldTests` does not assert a capsule's `direction`, a collider's `center` or its scale (radii are compared without `lossyScale`), or any cylinder mesh's size.
- `PoseParity_…` in the sim checks `AxisY` only.
- All of it holds today:
  - every collider in the 44 prefabs has centre 0 (4856 boxes, 4602 capsules, 367 spheres)
  - every capsule's direction is 1, and every transform's scale is 1
  - the scene overrides none of these
  - my recomputation of all three axes reads 1.56e-15
- A regression would pass unseen.

**L3. `worldbench --bench` prints the power source, plan and mode but not the CPU clocks that PLT-07 asks for.** I read 3201/3201 MHz from WMI.

### Outside this change (for the orchestrator)
- **`flightbench --probe` fails on `unity/main` itself** since p7b's electronics fit. Its geometry check finds the coil at y −0.053 with the legs on, and the probe expects +0.087. It passes on this branch's `HEAD` and fails on the merge. Owner: the physics engineer.
- **The inherited merge commit `c91d0cfa`** (Tech Artist, G1 `--look`) has a `# Conflicts:` body and a 95-character subject. It is already on 5 other branches, so it is not this change's to fix.

## Merge conditions (D-047's order)
- Land `p6-test-range` first, after its own QA, with the G1 code. This branch merged it whole, so a PR now would land about 290 unreviewed files. After that the PR's diff is the 22 files reviewed here.
- Then B2's merge and re-record.

## Spec scenarios

| Scenario | Result | Evidence (mine) |
|---|---|---|
| A swept arm meets the pole | **pass** | The author's test, plus 96 sweeps over all 4 `L01_straight` poles, 8 headings and 3 heights: one timber contact each, Time within 1.3e-14 of the analytic, Distance ≤ 1.1e-14 m. |
| A small fast sphere never crosses a wire unseen | **fails one clause** (B3) | 39 248 crossings over 20 conductors and the 5 mm wire (24 000 at a joint or a chain end), any direction ≥ 25° off the segment, up to 0.999 of R + r: none unseen, each once with Time in (0, 1). WireParam is within 5.6e-16 of the contact's own place on the chain. 4 report Distance 1.4–13 µm. At rest inside, Distance is the true signed one: 4000 spheres, error 0. |
| A rotor ray re-cast from inside a conductor goes on | pass | 998 down rays onto all 20 conductors. Re-cast from 1 mm inside, none hits its wire, and each equals the brute force. 998 rays along a segment from inside hit nothing of it. |
| The launch stand is added and reset | pass | `LaunchStand.Build` at the "field" start gets id 731. Each post is met once at 3 points of its top. A drop from 5 cm latches once per post at the analytic Time. Gone after the reset, it gets the same id again. |
| Batched equals single | pass | `--golden` in Debug and in Release: all 11 cases pass with the same hashes, batched equal to single. |
| Threads share one world | pass | The author's test, plus 4 threads × 3 × 20 000 capsules, rays and samples with the stand: one hash, `3db2edafe263ab1f`. |
| The sim sees what the scene shows | pass | **Scene:** EditMode `TestRangeWorldTests`: 731 objects, 43 677 colliders, worst 0.015 mm and 0.0000°. **Sim:** my matrix recomputation finds the worst position 2.85e-14 m, the axes (X, Y, Z) 1.56e-15 and the sizes exact. **Hash:** it differs for a 1 mm row move, a 1 mm metadata centre move and a reordered material table, and stays the same on reload and with CRLF. |
| A foot in the trench | pass | 102 drops at every centre-line point and midpoint of E01's run and niches. Each samples `Hole`, meets no plane, and its first contact is always an E01 soil shape: 87 at the run's floor (−1.5, `belt_bare`), 12 on spoil lumps lying on it (`crater_spoil`), 3 at a band's end lip (0, `belt_straw`). |
| A patch is the ground once | pass | 46 patches and 8970 straw strips: each foot meets its strip and the soil once each, never the plane. |
| A capsule straddling a band's edge | pass | 12 capsules on both long edges of Z04's band at 3 places: the plane answers only where the lower end lies outside, and the wall always answers. |
| The worst-case step over the busiest spots | **fail** (B1) | K§10.5 along E01's run: 67.7–72.4 µs on 3 runs. |

**Beyond the scenarios:**
- The grid, `WireRange` and `InGap` equal a brute-force scan with an independent gap model bit for bit. The scan covered 30 000 capsules over the whole map and past its edge, 20 000 within 0.35 m of wires (6973 wire contacts), 50 000 rays and 30 000 samples. Nothing differs, and nothing is out of canonical order.
- 100 000 `StaticContacts` (buffers of 64 and of 1), 50 000 rays, 50 000 samples and 100 `Geometry` calls with the stand added allocate 0 B.

## Requirements
- **WLD-11, its G7 subset (`SampleGround`, `StaticContacts`, `Raycast`, `Geometry`, runtime objects, hash, `QueryVersion`, determinism): partly met.**
  - Missed: the 1 µm first touch (B3), and the budget (B1).
  - Also partly met: "judged per surface" is judged per spot.
- **WLD-16 at the test range's scale: met.** Patches and bands; the asset's shapes are the only ground there. L1 is a measure-zero edge.
- **PHB-05, the world's side: met.** The probe passes on `HEAD`.
- **PLT-04, the world-query share: partly met.** Physics' pattern has medians of at most 22 µs. K§10.5's misses (B1), and the p99 of up to 266 µs is open for 1.6b's whole step.
- **AST-05, the collision the queries return: met.** Pose parity holds in the sim and in the scene.

## The golden re-record (QueryVersion 2), audited case by case
- **No stored input changed.**
  - My harness copies worldbench's floor inputs and reproduces both recorded `floor.contacts` hashes: `62bc302cba80b5e0` at `3bb49a1` and `6f6ca03d23e8cf6b` at `40bc591`.
  - `floor.ground` and `floor.rays` did not move.
  - The 10 000 dumped inputs are byte-identical between versions.
- **`26145af` (Clear) alone moves no case:** its dump equals version 1's.
- **`40bc591` moves 10 of 10 000 capsules, one contact each, all on the probe's two boxes.**
  - Counts, ids, materials, surfaces, WireParam and the Time branch are unchanged.
  - Point moves by at most 1.37e-9 m, Normal by 4.2e-8, Distance by 2.8e-16 m and Time by 8.9e-15.
- **The cause, by experiment:** `40bc591` with `BoxMinimizer` switched off reproduces version 1's dump bit for bit (`62bc302cba80b5e0`).
- **Better, not just different.** For each case I solved the box's closest approach in exact rationals on the same doubles, at each version's own pose:

  | case | box | Time | v1's Point off the exact | v2's | \|ΔDistance\| |
  |---|---|---|---|---|---|
  | 602 | yawed | < 1 | 4.9e-11 | 1.4e-16 | 0 |
  | 1381 | yawed | < 1 (a give-up in both, 2.67 mm: B3) | 1.1e-16 | 1.2e-16 | 2.8e-16 |
  | 1682 | yawed | < 1 | 6.2e-11 | 5.6e-17 | 8.3e-17 |
  | 1794 | yawed | < 1 | 3.2e-10 | 3.5e-17 | 1.0e-17 |
  | 3553 | yawed | < 1 | 9.1e-10 | 1.5e-16 | 6.3e-17 |
  | 3575 | soil | 1 | 0 | 0 | 5.6e-17 (normal 1.6e-8) |
  | 4197 | yawed | 1 | 1.0e-9 | 1.2e-16 | 6.9e-18 |
  | 7993 | soil | < 1 | 1.4e-9 | 2.0e-16 | 2.1e-16 |
  | 8530 | soil | 1 | 1.4e-9 | 1.0e-16 | 2.8e-17 |
  | 9786 | yawed | < 1 | 1.1e-11 | 1.4e-16 | 1.4e-17 |

  Both versions' distances lie within 1.1e-16 m of the exact one. Version 2's points lie within 2.0e-16 m of the exact closest point, against version 1's 1.4e-9 m: the golden-section bracket.
- **No flight golden moved:** simtests pass 230 of 230 on `HEAD` and 280 of 280 on the merge with `unity/main`.
- **Verdict on the re-record: justified, and it blesses no drift.**

## Hygiene
- **Commits:** the change's 11 commits are all by `World Builder <world@450fpv.local>`, subject only, at most 65 characters, with no body or trailer.
- **Paths:** the diff holds only its 22 files. Nothing from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/` is tracked.
- **Godot history:** the branch's history has only `unity/main`'s orphan root. The credit lines (`ShapeMath.cs`: `Shapes.cs`' wire case, `Contacts.cs`, `WorldObjects.cs`, `WorldQuery.cs`; `MapWorld.cs`; `WorldHash.cs`: `ContentHash.cs`) name files that exist in `refs/godot/main`. The new code is rewritten, never merged.
- **Determinism rules:** no `System.Math` in `Runtime/World` (only `DetMath`). The one `stackalloc` (8 doubles) is on the stack.
- **OPSEC and licences:** `precommit.py --tree HEAD` scans 2124 files with 0 findings. `fetch.py --verify` is OK, and the change adds no third-party file.

## Commands and output
```
dotnet test tools/simtests                                         Passed! Failed 0, Passed 230 (1 m 40 s)
dotnet run -c Debug --project tools/worldbench -- --golden          worldbench: ALL PASS (floor 5 cases, test-range 6 cases), exit 0
dotnet run -c Release --project tools/worldbench -- --golden        worldbench: ALL PASS, the same hashes, exit 0
  test-range.contacts: 3965 of 10000 capsules in contact (up to 19), 4140 contacts with Time < 1; 1098 on the plane,
  4731 on soil shapes, 56 on wires, 215 on the stand; 974 overflow a buffer of 2
dotnet run -c Release --project tools/flightbench -- --probe        contact-probe: PASS, exit 0
dotnet run -c Release --project tools/worldbench -- --bench (x3)    exit 1 each: "K10.5 ... along and over E01's run ...
                                                                    median 72.40 / 71.00 / 67.70 us (OVER the 60 us budget)"; 0 B everywhere
python -m pytest tools -q                                           1451 passed in 1609.28s
python tools/hygiene/precommit.py --tree HEAD                       2124 files scanned, 0 findings, exit 0
python tools/assets/fetch.py --verify                               OK, exit 0
openspec validate p6-world-query-gallery --strict                   Change 'p6-world-query-gallery' is valid, exit 0
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile - | Out-Host
                                                                    851 of 851 passed, exit 0 (241 s); TestRangeWorldTests:
                                                                    "731 objects, 43677 colliders: worst position 0.015 mm, worst turn 0.0000°"
merge of origin/unity/main (de8d7399), throwaway:
  dotnet test tools/simtests                                        Passed 280 of 280
  worldbench --golden (Release)                                     FAIL test-range/content_hash 6114242e06475fb5 (golden d6de238216ceb4b9); the rest PASS
  flightbench --probe                                               FAIL (1 check: geometry), as on unity/main alone
mutations (World tests: 32)                                         M1 WireRange drops a segment: 3 fail; M7 WireParam from the core: 2 fail;
                                                                    M8 wire ray from inside: 1 fails; M9 plane kept in gaps: 3 fail;
                                                                    M6 ray keeps the plane in gaps: 0 fail (golden rays fails);
                                                                    flat piece at its end / tie keeps the last: 0 fail, goldens pass
```
