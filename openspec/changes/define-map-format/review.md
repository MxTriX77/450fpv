# QA review: define-map-format (task 5.2)

**Reviewer:** QA Engineer · **Date:** 2026-09-25 · **Reviewed:** `main` at `4ea8e66`
**Against:** the three spec deltas (map-format, world-query, map-loading), the accepted deviations in `design.md`, `surfaces-review.md`, `api-review.md` and `orchestrator-check.md`.

The pilot merged the change through PRs #4, #7, #10 and #14 before this review. On `main`, the change's code and data are those of `f9ff61f`: the only later commits on `main` add `openspec/changes/build-uat1-parts/`. So this review covers the code as merged, and any fixes go in a follow-up.

## Verdict: approve

- **All 42 spec scenarios pass**, 41 by running the committed checks and one (Corner lookup) by a QA probe against the real API.
- "Golden file" passes on this machine in Debug and Release, under both worldbench and Godot. The "any other machine" part can't be run here (see Risks).
- The benchmark passes on AC at the nominal clock. Where it falls short, the gap is one of the accepted deviations.
- The accepted deviations are recorded, and both physics reviews are satisfied.
- Hygiene, determinism, OPSEC, LFS and identities are clean.
- Nothing blocks. The findings below are follow-ups.

## Machine and conditions

- AMD Ryzen 7 7735HS (8 cores, 16 threads, 3.2 GHz nominal), GeForce RTX 4050 Laptop, Windows 11 build 26200.
- Software: Godot 4.7.2 mono, .NET SDK 10.0.301 on runtime 10.0.9 (the only runtime installed), Python 3.14.6.
- **Power source:** AC for every run (battery 58–65 %, charging).
- **Windows power mode:** "Best power efficiency" (overlay `961cc777…`) on the Balanced scheme. QA does not change system settings, so the "Best performance" re-measure that `design.md:91` asks for ran in this mode. Benchmark run 2 held 3.13–3.41 GHz throughout, which is the nominal clock the ruling is after.

## Runs

| Command | Result |
|---|---|
| `python tools/map/validate_map.py game/maps/sample_patch` | exit 0, "valid map package (format 1.0, 256 m)" |
| `python tools/map/test_validate_map.py <scratch>` | 52/52 cases pass |
| `dotnet build game/Fpv450.sln` | 0 warnings, 0 errors |
| `godot --headless --path game --import` | exit 0; tree still clean afterwards |
| Smoke: `--headless --path game --quit-after 120` | exit 0, 0 `ERROR` lines |
| `-- --map sample_patch --selftest map` | ALL PASS, 17 checks, exit 0 |
| `-- --map nope --selftest map-fallback` | PASS, exactly one `ERROR:` line, placeholder visible |
| `-- --selftest worldquery` | ALL PASS, 97 s, exit 0 |
| `-- --selftest noclip` | 4/4 PASS |
| `dotnet run -c Debug --project tools/worldbench -- --golden` | ALL PASS: 84 PASS / 0 FAIL, content hash `3635cf0a10155cc0`, QueryVersion 2, concurrency 10 s with 1,796 + 1,058 cases and 0 differing |
| the same with `-c Release` | ALL PASS: 84 / 0, same hashes, concurrency 8,201 + 5,065 cases, 0 differing |
| `dotnet run -c Release --project tools/worldbench`, run 1 | FAILED on one line: the clock fell to 2.0–2.4 GHz after the first workload (see Benchmark) |
| the same, run 2 | **ALL PASS** at 3.13–3.41 GHz |
| `-- --map sample_patch --selftest map-view --view 0,60,110,0,-28`, windowed | PASS, 745 fps average, 1 % low 641 at 1152×648. I looked at the screenshot, plus two close views (belt `0,14,-28,0,-40`, yard `64,14,64,0,-45`). |
| `flypath` on `sample_patch` and on the synthetic 4 km map, 1920×1080 | See "Budget check" |

## Scenarios

### map-format

| Scenario | Result | Evidence |
|---|---|---|
| Complete package | pass | The validator exits 0 on `sample_patch`, and test case "complete package" passes |
| Bad map size | pass | 300 m and 8448 m both exit 1 with "size_m … breaks the side rule: a multiple of 256 m … and at most 8192". The loader case gives a one-line error. |
| Missing layer | pass | 5 cases exit 1, each naming its file. The loader case names `cover.png`. |
| Corner lookup | pass (QA probe) | A console probe (not committed) compiled `game/src/world` and queried an in-memory 256 m world with only its corner samples and cells marked: (−128, −128) → row 0, column 0 (height, surface, grass channel); (+128, −128) → row 0, last column; (−128, +128) → last row, column 0. On `sample_patch`, (−128, −128) gives terrain −0.3000002 m = `height.r16`[0] and surface 6 = `surface.png`[0]. **No committed test: finding 2.** |
| Size mismatch | pass | "height.r16 is 132100 bytes, expected 132098". The loader reports its own size too. |
| Unknown surface | pass | "unknown surface index 42, first at row 10, column 20 (world x=−117.75, z=−122.75)". Index 0 is rejected too. |
| Required fields | pass | 15 broken-copy cases (missing field, negative bearing, unload 0.5, mat min > max, kinetic > static on a mat and on a material, zero element length and diameter, inverted range, missing hook release, missing soil reference, azimuth 200, and others). Each names the surface or material and the field. |
| Unknown asset | pass | "object 3: unknown asset 'tank_hull'". The loader rejects it too. |
| Unknown material | pass | Asset material `concrete`, a shape material `brass`, and a collider without a material are all rejected with the asset and id |
| Out of bounds | pass | Object 0 at x=131 and a wire point at z=−140, each named |
| Start outside the map | pass | "start point 1 at x=20, z=129 is outside the map". A start inside exits 0. |
| Future major | pass | Validator: "format_version 2.0 is not supported; this validator reads 1.x". Loader: "format_version is 2.0, and this build reads 1.x". The sandbox falls back through the one `LoadMap` error path (`game/scenes/sandbox/Sandbox.cs:102-109`), which map-fallback shows as one `ERROR:` line with the placeholder visible. |
| Georeference scan | pass | 12 rejection cases: latitude, `origin_lat`, EPSG, `geoRef`, Latin and Cyrillic place names, a place name in the catalog, a coordinate pair, MGRS, PNG `tEXt`, EXIF, and a GIS sidecar. `geometry` passes. |

### world-query

| Scenario | Result | Evidence |
|---|---|---|
| Matches the rendered surface | pass | 10,000 points (random, diagonals, cell edges, chunk and map edges): 0.000000 mm from the independent renderer triangle, 0.0761 mm from a Jolt ray, 0 misses |
| Continuous across surface borders | pass | 4,088 border edges, 6.1 M samples at 1 mm: jump beyond the slope 0.439 mm, and where the slope is ≤ 1 the step is 0.999 mm (limit 1 mm). See finding 3 for the log wording. |
| Normal accuracy | pass | 9,947 points: worst 2.23e-4 rad. Inside pitfalls, at a 1 µm step: 0.000000 rad (limit 1e-3) |
| Micro-relief bounded | pass | All 9 surfaces on uniform worlds plus meadow on `sample_patch`, about 40,000 points at 5 cm each: −1.7 % to +2.6 % of the expected RMS (limit ±20 %) |
| Pitfall is identifiable | pass | 41 pitfalls in 3,600 m². Every hit has an id and a depth, and the depth equals the drop vs a world without pitfalls within 1.5e-5 mm |
| Outside the map | pass | 9 points (edges, corners, ±1e12, NaN, ∞): flag set, terrain and surface are the edge cell's, nothing throws |
| Replay identity | pass | Two processes, the second a separate Godot process: `c73649404d899d79-146d42b3e19fa28e` in both |
| Overlapping queries agree | pass | 3 pairs, 0 missing, 0 differing, same order |
| Crossing straw is found | pass | A 0.960 m straw rooted 0.768 m out is returned for a 0.1 m sphere |
| Lying elements lie on the mat | pass | 25,200 elements: ends on `SupportTop` within 0.00024 mm. Those clear of pitfalls stay within +238 / −248 mm (limit ±250). 252 pitfall crossings are reported separately. |
| Density matches the surface | pass | belt_straw: 0.00 % error on each kind, in both the uniform world and the `sample_patch` band |
| Overflow reported | pass | A 128-element buffer returns count 386, and the buffer equals the first 128 of the full list |
| No tunnelling through a wire | pass | 180,000 sweeps (15 mm and 7.5 mm spheres, 60 mm per step, across a 5 mm wire at 3,000 phases): 0 missed. A static-only query would miss 61.7 %. |
| Material reported | pass | A capsule on the rails gives `steel` on (46, 0) and (46, 1). The gate bar is steel, the house masonry, the shed timber and the cable `cable`. |
| Wire geometry | pass | `sample_patch` cable: span 30.018474 m, sag 0.6 m, diameter 0.012 m, attachment points within 0.000000 mm of `objects.json` (limit 1 mm) |
| Roof under a rotor | pass | 1,000 rays at 1 m ± 8.9e-16 m, `sheet_metal`, object 46. It uses the test-only roof asset, an accepted deviation (`design.md:85`). |
| House shadow | pass | TopM 6 m, BaseM 0, porosity 0 (limit ≤ 0.1). Open cells: 0 m, porosity 1 |
| Tree crown | pass | BaseM 4 m, TopM 10 m. Path porosity 0.508 / 0.535 / 0.512 at three placements vs `wind_porosity` 0.5 |
| Door gap | pass | The gate gap is found from 5 m in front at r 6 m, with centre error 9.5e-9 m and size 1.6 × 1.98 m |
| Reset between flights | pass | After a reset: 46 objects, the loaded wind grid, 1 contact per bar (2 without the reset), and a wind grid bit-identical to a single addition |
| Rails added | pass | Object 46: two `steel` contacts at −0.1 mm, normal y 1 |
| Any change is detected | pass | A one-byte change on disk changes the hash in 7 of 7 files. 69,804 in-memory single-byte changes: 0 leave it unchanged. |
| Golden file | pass on this machine | 41 cases and 84 PASS lines, cold and warm, in worldbench Debug and Release and in Godot's Debug build. Batched results equal single-point results. Not yet run on a second machine (Risks). |
| Concurrent use | pass | 10 s each in Godot (1,628 + 962), worldbench Debug and worldbench Release: 0 differing |
| Benchmark | pass (run 2, on AC) | See Benchmark |

### map-loading

| Scenario | Result | Evidence |
|---|---|---|
| Sample patch | pass | 43 objects within 0.000 mm and 3 wires within 0.008 mm (limit 10 mm). 42 bodies, 361 shapes, 0 wrong material tags. 9 surfaces, each with its own layer. In the screenshots, all 9 are visibly distinct: meadow vs weeds only subtly, as `orchestrator-check.md` item 2 already records for UAT-1. |
| Visual and physical stems agree | pass | 1 m over belt_straw: 7,395 bases within 3 m, largest error 0.0000 mm, 0 not drawn and 0 extra |
| Fly the sample | pass | The camera starts at (0, 15, 0), 15 m above the terrain at the centre, with the placeholder hidden |
| Budget check | pass | On AC, "Best power efficiency", 1920×1055 window, vsync off, 12 s fixed path. `sample_patch`: 383 fps, 1 % low 230 (budget 120 / 90). Synthetic 4 km: 447 fps, 1 % low 346 (budget 90 / 60), terrain built in 2.08 s, first frame 4.83 s after engine start (budget < 10 s). VRAM 155 MB and 247 MB. |

## Benchmark

`dotnet run -c Release --project tools/worldbench`: medians of 21 runs on the pinned core. Every timed section allocated 0 B.

| Budget | Run 2 (3.13–3.41 GHz) | Run 1 |
|---|---|---|
| 100k ground samples in the physics pattern, < 10 ms | **8.561 PASS** | 8.762 PASS (3.23–3.37 GHz) |
| `MicroDetailNear` on the densest surface (meadow), < 0.25 ms | **0.203 PASS** | 0.285 FAIL at 2.13–2.36 GHz (accepted deviation, `design.md:87-91`) |
| `MicroDetailNear` on every surface, < 1 ms (printed, not judged: finding 1) | belt_straw 1.085 (accepted deviation, `design.md:61`), yard_litter 0.868, others ≤ 0.34 | belt_straw 1.686, yard_litter 1.144 |
| 100k swept capsules, < 100 ms | **36.97 PASS** | 52.65 PASS |
| 100k rays of 2 m, < 100 ms | **18.06 PASS** | 25.85 PASS |
| Composite step, < 60 µs | **22.4 PASS** (single steps: p50 19.6, p99 63.7, max 152.8) | 31.7 PASS |

The single-step p99 above 60 µs is OS jitter, already accepted at `design.md:89`. The 60 µs budget is judged on the step average.

## Physics reviews and accepted deviations

- **`surfaces-review.md` §3–§5.** A script compared `surfaces.json` and `catalog.json` with every value the review proposed: 0 mismatches. That covers the soil reference 0.015 m, the 9 unload ratios, no `soil.porosity`, the 3 mats, the 12 hook-release ranges, tilled straw 5 N/m, yard friction 0.60/0.50, tilled relief 0.02 m @ 0.3 m with ridges 0.05 / 0.5 / 0°, the 5 materials, the asset material assignments, junk as visual only, and the tree crown's wind volume.
- **`api-review.md` F1–F8:**
  - F1: "Lying elements lie on the mat"
  - F2: "Reset between flights"
  - F3: "Wire geometry" plus 24 random primitives within 1.8e-15 m
  - F4: the in-memory soil reference is 0.015 m
  - F5: `Material(NoMaterial)` is null
  - F6: the golden file checks `QueryVersion` 2
  - F7: `DetMath.Pow` within 4 ulp and identical at the physics ratios
  - F8: the graze wording is in `game/maps/README.md:247`
- **Accepted deviations**, each recorded:
  - micro-detail at throttled clocks (`design.md:87-91`)
  - belt_straw at 1.07–1.15 ms (`design.md:61`; measured 1.085)
  - lying elements crossing a pitfall (`design.md:60`, and the spec excludes them)
  - derived launch-rail dimensions (`design.md:81`)
  - the test-only sheet-metal roof (`design.md:85`)

## Hygiene, determinism, OPSEC, git

- **Hygiene.**
  - No path from `reference/`, `.godot/`, logs or build output is in the change. `tools/worldbench/.gitignore` ignores `bin/` and `obj/`, and `__pycache__/` is ignored.
  - There is no TODO or FIXME, no commented-out code, and no debug print in the runtime world files.
  - `tasks.md` matches what was built. Nothing extra: the `flypath` and `terrain_bench` tools serve tasks 1.1 and 4.4.
  - `openspec validate define-map-format --strict` reports the change as valid.
- **Determinism (W-13, W-14).**
  - In `Catalog`, `Contacts`, `DetMath`, `MicroDetail`, `Shapes`, `SurfaceTable`, `WindGrid`, `WorldObjects` and `WorldQuery`: no `Math.Sin/Cos/Tan/Exp/Pow/Log/Atan`, `MathF`, `Fma` or `FusedMultiplyAdd`.
  - Trigonometry goes through `DetMath.SinCosTurns`. Powers go through `DetMath.Pow`, whose `Math.ScaleB` is exact.
  - Vectors are the scalar `Double3`. The `Vector256` paths are checked bit for bit against `ScalarReference` in the golden file.
  - There are no static mutable fields. The instance fields are set only at load.
  - No Godot type appears in the query path.
- **OPSEC.** I ran a masked comparison of the change's diff (87 files) and all 110 commit messages against `reference/_frames/index.md`:
  - 0 of its dates, times, clip ids or file names appear.
  - The only overlaps are generic words, such as a file extension in `.gitattributes`.
  - There is no user name, e-mail or local path.
  - The place names and one coordinate string in `tools/map/place_blocklist.txt` and `test_validate_map.py` are public blocklist fixtures, not taken from the reference.
- **LFS.** `height.r16`, `surface.png` and `cover.png` are LFS pointers, and the change adds no other binary blob. The PNG `.import` sidecars are `importer="keep"`.
- **Identities.**
  - 110 non-merge commits: World Artist 79, Orchestrator 29, Physics Engineer 2. Author and committer are the same for each. Each role's files are within its tasks: the World Artist's edits to the sandbox, worldbench, `README.md` and D-009 are tasks 4.2, 3.6 and 1.1.
  - No commit has a body or a co-author trailer, and every subject is `type(scope): summary`.
  - **Over 72 characters:** `9ee2b64` (74), `docs(openspec): add lying-element, reset and geometry rules to world-query` (Orchestrator). It is already on `main`, so this is recorded only.

## Findings

None blocks. Severity is "should" (fix in a follow-up) or "nit".

| # | Severity | Where | Finding | Owner |
|---|---|---|---|---|
| 1 | should | `tools/worldbench/Program.cs:279-282` | The benchmark judges only the densest surface. The spec also requires "under 1 ms on every surface" (`specs/world-query/spec.md:169`), but the tool prints the other surfaces for information only. yard_litter is at 0.868 ms at full clock, so it could regress past 1 ms with no FAIL. Judge every covered surface at < 1 ms, and give belt_straw's accepted deviation (`design.md:61`) its own explicit limit, so the benchmark guards it too. | World Artist |
| 2 | should | `specs/map-format/spec.md:34-36`, `game/src/world/WorldQuerySelfTest.cs` | "Corner lookup" has no committed test on the query side. The validator's row and column mapping is tested, but the renderer and the query checks would still agree if both were mirrored. The QA probe passes. Add an asymmetric in-memory world check, as in the probe, to `--selftest worldquery`. | QA (follow-up) |
| 3 | nit | `game/src/world/WorldQuerySelfTest.cs:245-246` | The continuity line ends "largest SupportTop step 16.989 mm (limit 1 mm) PASS". It reads as if an over-limit value passed. The check at line 241 is correct, because the limit applies to the gentle-slope step and the jump. Move "(limit 1 mm)" next to those values, and mark the SupportTop step as information. | World Artist |
| 4 | should | `origin/world/define-map-format` (`ff726b4`, `77ddb2c`) | Two commits landed on the change branch after PR #14 merged: an earlier QA `review.md` for 5.2 and the 5.2 tick in `tasks.md`. They aren't on `main`, and they would conflict (add/add) with this file if that branch were merged again. Keep one review and tick 5.2 on `main`. Don't re-merge the branch as it is. | Orchestrator |

## Risks

- **Cross-machine golden.** The golden file has only run on this CPU (Zen 3+, AVX2 and FMA, no AVX-512) and on .NET 10.0.9. An exported build bundles its own runtime (the project targets net8.0). Before physics logs are replayed across machines, run `--golden` on a second machine, for example an Intel CI runner, and in the exported build. Owner: QA and Orchestrator.
- **Tight lying-element margin.** The band is at 248 mm below the mat top against a 250 mm limit on uniform belt_straw. `build-uat1-parts` changes the data and can trip it without any code fault. Owner: World Artist, when re-recording.
- **Benchmark clock.** In "Best power efficiency" the clock can drop to 2.0–2.4 GHz part-way through a run, and the meadow line then fails, as it did in run 1. Judge only runs whose lines show about 3.2 GHz or more.
