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

# QA re-check of the fixes (task 1.6, at `6a497302`)

**Verdict: changes requested.** One new blocking finding, B4. Everything else from my review is closed or accepted:
- **B2:** closed. An experiment shows the content hash moves for `carbon_composite` alone.
- **B3:** closed for what it reported. No false first touches remain in 32 190 fresh grazing sweeps; the QV2 code made 4 054 on the same sweeps. The golden re-record is justified case by case.
- **S1:** closed. The three mutations now fail their tests.
- **L2 and L3:** closed.
- **B1 and S2:** closed by D-054, and the restated budget holds on my runs.
- **The World Builder's pushback** (S04 in the grid, a tree only above 256 shapes): accepted.

B4 is a side effect of B3's fix: the give-up now drops true first touches when the sweep is turning.

**The PR also needs one more merge (M1).** The branch no longer merges cleanly into today's `unity/main`.

**Scope:**
- The fix commits `ace2e02`…`6a49730`, 8 of them, and the merge `48ab26e`. That is 16 files.
- The review branch merges `6a49730` (`eb01afc`), so the checks below ran on the fixes plus this file.

**How I checked:**
- Every command below was run by me.
- **A scratch harness, never committed:**
  - It uses the package's public API only and has its own truth.
  - The truth is the signed distance to each primitive and to a wire's chain, written with `System.Math`. A golden-section search runs along the core, which is exact because the distance is convex there.
  - Each sweep's first touch and least gap come from:
    - a closed form on the plane
    - convexity for a translation (per segment on a wire)
    - a Lipschitz branch and bound for a turning sweep (resolution 1e-10 of t, no search left unresolved)
- **Builds:** the harness was built three times, on `6a49730`, on `f8e67f2` (QV2) and on an instrumented copy of `6a49730`.
- **The instrumented copy** logs each sweep's exit. Its results equal the pristine build's on the whole fresh set.
- **Throwaway worktrees:**
  - patched worldbench copies at `f8e67f2` and `6a49730` dump every golden contact
  - the mutations
  - `p6-flight-core--range` with these fixes laid over it
- **Unity:** used this worktree's Library from my first review; Unity imported the changed files.

## B4 (blocking): a turning sweep's first touch is dropped after 128 steps
Owner: the World Builder; the Physics Engineer says what 1.6b needs.

**What happens.** A core touching a curved shape (a twig, a trunk, a wire, a box edge) near its middle while it pitches:
- Its far end swings towards the plane of `Bound` at ω·h, while its middle approaches slowly.
- Neither the distance / speed step nor the bound's step outruns the creep.
- After 128 steps the sweep counts no first touch. It returns the current contact at Time 1, although the current pose already lies inside the shape.

**Evidence, a pitching set:**
- **The set:**
  - a 0.1–0.3 m core, r 5–10 mm, its middle on a random point of one of 4 probe shapes, 73 trunks or the 20 wires
  - pitching at 290–2000 °/s at 1 kHz, about its middle
  - its middle approaching at 0.2–10 % of its ends' swing, sliding up to twice that
- **Results:**

  | | sweeps | true touches | missed (no Time < 1) | deepest at the step's end | the fastest missed approach |
  |---|---|---|---|---|---|
  | `6a49730`, seed 7 | 4000 | 2620 | **658** (25 %) | 1.06 mm | 0.154 mm a step |
  | `6a49730`, seed 99 | 8000 | 5161 | **1311** (25 %) | 1.38 mm | 0.167 mm a step |
  | `f8e67f2` (QV2), seed 7 | 4000 | 2620 | 0 | | |

- **Every miss is a touch by the query's own answer.** The sweep starts more than 5 µm clear, and the query's current contact is within 1 µm, mostly inside.
- **Every miss is the 128-step give-up,** by the instrumented copy.
- **No contact is lost:** the current contact is always returned.

**The random fresh set shows it too.** In its turning quarter (rotation up to 0.05 rad a step about the contact point), 19 of 2 837 true touches are missed (≤ 8.9 µm deep). In straight sweeps, none of 8 080 is missed.

**Why it matters:**
- Physics latches every Time < 1 contact (review-physics P1), and K§7.5 makes every impact one Time < 1 record.
- A missed first touch is a contact up to ~1.4 mm deep that physics meets unlatched. It also goes unrecorded as an impact.
- The QV2 give-up erred early, a phantom latch. This one errs towards a missed latch, which is the unsafe side for a collision query. `IWorldQuery` now states it, but the spec scenarios and physics have not accepted it.

**Fix:**
1. **A proven touch is still a first touch.** When the advancement gives up but the current pose is within 1 µm, the step has touched in (t, 1]. Find that touch, by carrying the advancement on or by bisecting between the last advanced t and 1. Report it with Distance within 1 µm.
2. **Optionally,** a bound that does not take a far end's swing as an approach, such as one per end against the shape's bounding sphere.
3. **Add a test:**
   - a 0.3 m skid pitching at 2000 °/s across a twig cylinder and an `L03_medium` conductor
   - its middle approaching at 10–100 µm a step
   - it passes with one Time < 1 contact, not after the brute-force touch, with Distance within 1 µm
4. **State the remainder in `IWorldQuery` and the scenario:** a graze that dips and comes back clear may count none. That is ≤ 6 µm deep in my sets: none of the pitching misses and 10 of the fresh set's 19.
5. **Physics signs off** the rule.

## Merge condition
**M1. A merge with today's `unity/main` conflicts in `tools/worldbench/Program.cs`.** Owner: the World Builder, before the PR.
- **Cause:** p7c-wind's wake grid (`04b7c92f`, `59dec58f`, `50d7fff1`) landed after `7d72db8`. It adds `--wakes`, `--wakes-range` and a third golden (`golden/wakes.txt`), and splits `Golden` per world. 1.6 restructured the same code.
- **Effect:** pytest's merge check fails.
- **Fix:** merge `unity/main` with `-m` and keep both sides. After the merge:
  - `--golden` passes the floor, the test range and the wakes, with no hash moved
  - `--bench` and `--wakes` still print the power line with the clock
  - the merge check passes
- QA re-checks it with B4.

## Should
**S3. No unit test guards the bound's safety on a primitive.** These mutations make the bound unsafe, so it can step past a touch:
- a sphere's support without its radius
- a box's without its z extent
- a cylinder's without its radius

All 37 world tests pass on each of them; only the goldens' hashes move. The wire's mutation fails `GrazingSpheres_…`. Since a golden can be re-recorded, an unsafe bound could be blessed by one.

Add fast-sphere crossings for the primitives, as `AssertFastSpheresCaught` does for wires, each not after its analytic touch:
- a sphere
- a capsule
- a cylinder (side, cap, rim)
- a box (face, edge, corner)

## Low
**L4. A cylinder's first touch can land a hair inside.**
- **Size:** 7 of 5 126 straight touches on round shapes, all cylinders, are up to 74 nm of travel after the true touch, and 0.1–2.3 nm deep at Time.
- **Cause:** the cylinder's closest approach is `MinimizeAlong`'s golden search, whose bracket is 4e-9 of the core. It reads up to ~1e-9 m long, so a distance / speed step can pass the touch by that much.
- **Impact:** within `IWorldQuery`'s 1 µm, and as before (1.4). Change it only if physics needs "never after".

**L5. The as-built note's shown rows did not reproduce.** These rows are shown, not judged (D-054):

| row | my 3 runs | the note |
|---|---|---|
| K§10.5, E01 median | 64.4–75.0 µs | 35.5 |
| K§10.5, crown median | 15.5–35.6 µs | ≤ 14.0 |
| random-pose capsules | 182–205 ms | 90.6 |
| random-pose rays | 111–119 ms | 57.9 |

Physics' pattern matches the note. Don't cite those four as the state at 7d.

**L1 (open, unchanged):** a ray lying exactly on a seam line falls through, as the note says.

## The earlier findings
| | state | evidence (mine) |
|---|---|---|
| B1 | closed by D-054 | Physics' pattern on 3 runs (below): every row 0 B, p99 ≤ 59.8 µs (E01 53.6–59.8), medians ≤ 11.8 µs, under PLT-04's 300 µs and at the 60 µs target. |
| B2 | closed | `ace2e02` changes only the `content_hash` line. My harness builds the test range with and without the row: `6114242e06475fb5` with it, `d6de238216ceb4b9` without, so the row alone moves it. No query hash moved at QV2: the patched `f8e67f2` passes the QV2 golden. |
| B3 | closed (B4 is new) | The golden audit and the fresh set below. 0 false first touches in 32 190 sweeps; on the same sweeps the QV2 code makes 4 054. The plane's first touches are exact (Distance ≤ 4.9e-17 m). Every new first touch lies within 1 µm of the shape and never after the true one. |
| S1 | closed | Each mutation fails its test (below). |
| S2 | closed by D-054 | |
| L2 | closed | `TestRangeWorldTests` now reads each collider's centre through its transform, its scale, a capsule's `direction` and a cylinder mesh's bounds; the sim's parity checks all three axes. EditMode: 908 of 908, the scene's parity at 731 objects and 43 677 colliders, worst 0.015 mm and 0.0000°. |
| L3 | closed | `worldbench --bench` prints "AC, Balanced plan, balanced mode, CPU 3201/3201 MHz". |

## The golden re-record (QueryVersion 3), audited case by case
**No stored input changed.** Patched copies of `f8e67f2` (QV2) and `6a49730` dumped every contact of both goldens. All 20 000 cases have bit-identical inputs. Each copy reproduces its own recorded hashes.

**What moved, each pair against my truth:**

| | floor | test range |
|---|---|---|
| (object, shape) pairs | 1113 | 8097 |
| unchanged, bit for bit | 890 | 7832 |
| the plane's first touch, within 1 µm before → exact | 220 | 220 |
| the plane, a give-up → exact first touch | 2 | 6 |
| a box or wire give-up → a first touch | 0 | 34 boxes, 1 wire |
| a give-up → Time 1 or none; truth never within 1 µm | 1 (case 1381, least 0.58 mm) | 4 (least 1.06 µm to 0.96 mm) |
| Time < 1 contacts, old → new | 330 → 329 | 4140 → 4136 |

**Results:**
- 0 of the 488 moved pairs fail the check.
- Every new first touch lies within 1 µm of the shape: ≤ 0.86 µm by my truth, and the Distance agrees with my truth within 7.4e-15 m.
- None is after the true touch; the earliest is 12.4 µm of travel before it. The old answers lay up to 15.3 mm early.
- This matches the as-built table row for row.

**The cause, by experiment.** The mutation "no bound, a give-up counted a touch" (1.5's loop on QV3's code) reproduces the QV2 hashes exactly (`6f6ca03d23e8cf6b`, `5d4bb3a711df30e7`, `344c7ae53fbd1f21`). The re-record moved only through the bound and the give-up rule.

**The dense objects' trees and `f8e67f2` keep the bits.** The QV3 goldens pass at `6a49730` in Debug and Release, and `f8e67f2` passes QV2's.

**Verdict on the re-record: justified, and it blesses no drift.**

## Fresh grazing sets (not the golden's inputs)
**How the sweeps are made:**
- Each sweep aims its least gap at −20 mm to +0.2 mm of a target, log-spread, 10 % of them at 1 µm ± 0.1 %.
- Its motion is tangential, with a normal part of up to 5 %.
- r is 3–60 mm, the core 0–0.3 m, the move 5–600 mm a step. A quarter of the sweeps turn by up to 0.05 rad a step.
- Margin 0.05. A sweep that starts within 5 µm is left out.
- **The targets:**
  - the floor's plane and its 5 probe shapes
  - the test range's plane, where a foot lies off every patch and band
  - 40 random shapes of each primitive kind
  - all 20 wires

**Results** (seed 2026, 32 190 sweeps, 21 228 starting clear):

| | true touches | within 1 µm only | clear | false first touches | missed | after the true touch |
|---|---|---|---|---|---|---|
| plane, straight / turning | 448 / 187 | 235 / 101 | 439 / 162 | 0 / 0 | 0 / 0 | 0 / 0 |
| boxes | 1880 / 676 | 875 / 299 | 930 / 278 | 0 / 0 | 0 / **5** | 0 / 0 |
| spheres, capsules, cylinders | 5126 / 1755 | 2458 / 826 | 2252 / 732 | 0 / 0 | 0 / **14** | 7 (L4) / 0 |
| wires | 626 / 219 | 284 / 74 | 271 / 95 | 0 / 0 | 0 / 0 | 0 / 0 |
| **`f8e67f2` (QV2), all** | | | | **4 054** | 0 | |

**Checks on every first touch:**
- Every reported first touch lies ≤ 1.0 µm from the shape by my truth.
- Its Distance agrees with my truth within 1e-9 m.

## Mutations (37 world tests, then both goldens in Release)
| mutation | world tests | goldens |
|---|---|---|
| S1: the flat piece answered at its end (`u1`) | 2 fail (both W5 tests) | pass |
| S1: a tie keeps the last piece (`f <= best`) | 2 fail (both W5 tests) | pass |
| S1: the ray keeps the plane in gaps (M6) | 1 fails (`Raycast_OverAPatchOrInAHoleBand_…`) | rays fail |
| B3: no bound, a give-up counted a touch | 4 fail | the QV2 hashes, exactly |
| B3: no bound, a give-up counted none | 2 fail | contacts fail |
| the plane not stepped by its bound (`Flat` false) | 0 fail | contacts fail (bits only: the bound phase still lands on the exact touch) |
| unsafe bound: a sphere, a box, a cylinder (S3) | **0 fail** | contacts fail |
| unsafe bound: a wire segment without its radius | 1 fails | contacts fail |

## D-054's restatement
- **The spec's budget requirement and scenario now read as D-054 sets it:**
  - physics' pattern, judged inside PLT-04's 300 µs at p99 with 0 B
  - the 100 000-capsule and ray budgets read the same way
  - the 60 µs target printed on every row
  - K§10.5's batch with the fiber shown and judged at 7d
- `worldbench --bench` judges exactly that, and every judged row passes on my 3 runs.
- The scenario's last clause (`p6-flight-core`'s `flightbench --bench` on the straw rest and the stand's launch) is not testable on this branch.

## The World Builder's pushback: S04 in the grid, a tree only above 256 shapes
**Accepted.** Their numbers:
- On physics' straw stream, a tree over S04's 195 long, crossing strips visits 24 nodes and 9 shapes a query, where the grid walks 20 shapes: 20 % slower.
- The straw's world queries fell anyway, by the narrow phase and the bound: physics' p99 went from 67.6–138.3 to 40.1–88.4 µs, and the replay's p99 from 33.5 to 23.9 µs.

Mine:
- S04 has 199 shapes and V01_M 386; no test range object lies between them, so the threshold of 256 splits exactly these.
- The objects with trees: H01_D 4047, H01_I 1207, E01 831, V01_L 511 (×7) and V01_M 386 (×2).
- The straw row runs at 7.6–7.8 / 12.5–14.4 µs median / p99, and the stand at 2.0 / 8.9–9.7. That is under the 60 µs target with the stand checked by its object's bounds.
- The bits are unchanged.

**For the orchestrator:**
- D-054's last sentence still says the straw patch and the stand get a broad phase inside each object. Supersede it with what was built and why.
- The 256 is untested between 200 and 385 shapes; phase 3's map should re-measure it.

## The merge note
- `p6-flight-core--range` (now `c2cf4023`) still has `QueryVersion_IsTwo`, asserting 2. Its `Runtime/World` and `Tests/World` are this branch's before 1.6.
- At the merge, take this branch's `WorldHashTests` (`QueryVersion_IsThree`).
- `FlightReplay` refuses a log of another query version. The range branch commits no flight log, so nothing stored breaks.
- **My overlay run:** I laid this branch's `Runtime/World`, `Tests/World` and `tools/worldbench` over `c2cf4023`.
  - simtests pass 311 of 311, `GoldenFlights.RangeHash` `0x292C0858705902B6` included, so the range golden is unchanged at QV3
  - `worldbench --golden` passes
  - `flightbench --probe` passes, its coil check included

## Spec scenarios (re-run)
| Scenario | Result | Evidence (mine) |
|---|---|---|
| A swept arm meets the pole | pass | simtests 287 of 287 |
| A small fast sphere never crosses a wire unseen | pass | The test now checks Distance within 1 µm and Time not after the analytic touch, and the new test adds 900 grazes at 0.990–0.998 of R + r. My wire set: 845 true touches, none missed or late. |
| A first touch is a touch (new) | pass, as written | Its 4 tests pass, and each fails its mutation. My fresh set has 0 false first touches. It does not cover turning sweeps (B4). |
| A rotor ray re-cast from inside a conductor goes on | pass | simtests |
| The launch stand is added and reset | pass | simtests; `flightbench --probe`'s stand checks PASS |
| Batched equals single | pass | `--golden` in Debug and Release: 11 cases each, the same hashes |
| Threads share one world | pass | simtests |
| The sim sees what the scene shows | pass | EditMode `TestRangeWorldTests`, now reading centre, scale, direction and the cylinder's size: 731 objects, 43 677 colliders, worst 0.015 mm and 0.0000° |
| A foot in the trench, A patch is the ground once, A capsule straddling a band's edge | pass | simtests; their code paths keep their bits (goldens) |
| The worst-case step over the busiest spots (restated) | pass, except one clause not testable here | `worldbench --bench` ×3 on AC; the `flightbench --bench` clause is `p6-flight-core`'s |

## Requirements
- **WLD-11, its G7 subset: partly met.** B4: a first touch can be missed on a turning sweep.
- **PLT-04, the world-query share: met,** as D-054 judges it: p99 ≤ 59.8 µs, 0 B.
- **WLD-16: met.** L1 stays a measure-zero edge.
- **PHB-05, the world's side: met.**
  - On this branch the probe's contact checks pass; its one red is the trunk's coil geometry, outside this change.
  - On the range overlay the whole probe passes.
- **AST-05: met.** Scene parity holds with the L2 fields read.

## Hygiene
- **Commits:** the 8 fix commits and the merge are by `World Builder <world@450fpv.local>`, subject only, at most 71 characters, with no body or trailer.
- **Paths:** nothing from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`.
- **Determinism:** no `System.Math` in `Runtime/World`. The trees' build sorts with an index tie-break, so every runtime builds the same tree. The query stack is a `stackalloc` of 50 ints, and the build throws past depth 48.
- **OPSEC and licences:**
  - `precommit.py --tree HEAD` scans 2168 files with 0 findings.
  - `fetch.py --verify` is OK, and no third-party file is added.

## Commands and output (logs in this worktree's `unity/Logs/qa21b-*`)
```
dotnet test tools/simtests                                          Passed! Failed 0, Passed 287 (4 m 51 s)
dotnet run -c Debug --project tools/worldbench -- --golden          worldbench: ALL PASS, exit 0 (floor 5, test-range 6 cases;
                                                                    query_version 3, content 6114242e06475fb5)
dotnet run -c Release --project tools/worldbench -- --golden        worldbench: ALL PASS, the same hashes, exit 0
  test-range.contacts: 3964 of 10000 capsules in contact (up to 19), 4136 contacts with Time < 1; 1098 on the plane,
  4729 on soil shapes, 56 on wires, 215 on the stand; 974 overflow a buffer of 2
dotnet run -c Release --project tools/flightbench -- --probe        contact-probe: FAIL (1 checks), exit 1: "geometry: ... coil r 0.0415 m
                                                                    at y -0.053 (legs off -0.053)" (the trunk's coil); every contact check PASS
dotnet run -c Release --project tools/worldbench -- --bench (x3)    exit 0 each, "AC, Balanced plan, balanced mode, CPU 3201/3201 MHz",
                                                                    Windows CPU load 50 / 28 / 11 % before each; physics' pattern
                                                                    median / p99 us: crown 5.6-5.9 / 26.7-30.0, H01_D 3.5-3.6 / 14.8-18.0,
                                                                    E01 11.3-11.8 / 53.6-59.8, posadka 4.5-4.6 / 8.0-9.5,
                                                                    straw 7.6-7.8 / 12.5-14.4, stand 2.0 / 8.9-9.7; 0 B everywhere;
                                                                    100000 capsules 24.1-25.9 ms, rays 13.8-14.8 ms, samples 1.76-2.21 ms;
                                                                    "worldbench: bench done, every budget met"
& $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/qa21b-editmode.xml -logFile - | Out-Host
                                                                    908 of 908 passed, exit 0 (270 s); TestRangeWorldTests:
                                                                    "731 objects, 43677 colliders: worst position 0.015 mm, worst turn 0.0000°"
python -m pytest tools -q   (BLENDER the real 5.2)                  1 failed, 1450 passed in 1727.60s, exit 1: test_existing_tree_is_clean
                                                                    [origin/unity/main merged with HEAD]: "CONFLICT (content): Merge conflict
                                                                    in tools/worldbench/Program.cs" (M1)
python tools/hygiene/precommit.py --tree HEAD                       2168 files scanned, 0 findings, exit 0
python tools/assets/fetch.py --verify                               OK, exit 0
openspec validate p6-world-query-gallery --strict                   Change 'p6-world-query-gallery' is valid, exit 0
harness hash                                                        content_hash with the row 6114242e06475fb5, without it d6de238216ceb4b9
harness audit (dumps of f8e67f2 and 6a49730)                        inputs differing 0; moved pairs 488: bad 0
harness fresh 120 2026 (6a49730 / f8e67f2)                          32190 sweeps: false first 0 / 4054; missed 19 / 0; after the touch 7 / 0
harness pitch 4000 7 (6a49730 / f8e67f2); 8000 99 (6a49730)         missed 658 of 2620 / 0 of 2620; 1311 of 5161, deepest 1.38 mm
mutations (37 world tests, goldens)                                 as tabled above
range overlay (c2cf4023 + this branch's World, Tests/World, worldbench):
  dotnet test tools/simtests                                        Passed 311 of 311 (RangeHash 0x292C0858705902B6 included)
  worldbench --golden (Release)                                     ALL PASS
  flightbench --probe                                               contact-probe: PASS
```

## For the orchestrator, outside this change
- Supersede D-054's last sentence with the built broad phase.
- Merge `p6-flight-core--range` with this branch's `QueryVersion_IsThree`.
- Task 2.1 stays open on B4 and M1. Once both are fixed, my re-check is:
  - the pitching set, the fresh set and the new tests' mutations
  - the merge's goldens and pytest's merge check
- **GPU checks:** Unity EditMode, `--bench` and pytest with the real Blender all finished before the request to stop GPU use, so none is owed.
