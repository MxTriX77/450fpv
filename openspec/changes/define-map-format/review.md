# QA review: define-map-format (task 5.2)

**Reviewer:** QA Engineer · **Date:** 2026-09-25 · **Reviewed:** `world/define-map-format` at `f9ff61f`

## Verdict: approve

- All 42 spec scenarios pass. "Golden file" passes on this machine only, because the "any other machine" part can't be run here (see Risks).
- Every test passes, and the benchmark passes on AC at the nominal clock.
- The three accepted deviations are recorded in the design.
- The Physics Engineer's two reviews are satisfied.
- Hygiene, OPSEC, LFS and commit identities are clean.
- Nothing blocks.

PR #14 merged this exact commit into `main` (`423fc31`, 12:23) while this review was running. Every path of this change on `origin/main` is byte-identical to `f9ff61f`, so this review covers `main` as it stands.

## Machine and power

- **Hardware and tools:** AMD Ryzen 7 7735HS (8 cores, 16 threads), RTX 4050 Laptop, Windows 11 build 26200, Godot 4.7.2 mono, Python 3.14.6.
- **.NET:** SDK 10.0.301. Runtime 10.0.9 is the only one installed, so Godot and worldbench both run on it.
- **Windows power mode:** "Best power efficiency" on both AC and battery, with the Balanced scheme. QA doesn't change system settings, so the re-measure that design.md:91 asks for ran in this mode rather than in "Best performance". AC run 1 held 3.26–3.42 GHz anyway, which is the nominal clock the ruling is after.
- **Power source:** battery (56 % → 45 %) for the Godot runs and the first five benchmarks, then AC for the two judged benchmarks.

## Runs

| Command | Result |
|---|---|
| `python tools/map/validate_map.py game/maps/sample_patch` | exit 0, "valid map package (format 1.0, 256 m)" |
| `python tools/map/test_validate_map.py` | 52/52 broken-copy cases pass |
| `dotnet build game/Fpv450.sln` (also `--no-incremental`) | 0 warnings, 0 errors |
| `dotnet run -c Release --project tools/worldbench -- --golden` | ALL PASS: 84 PASS lines (cold and warm), content hash `3635cf0a10155cc0`, QueryVersion 2, concurrency 10 s (7,970 + 4,981 cases, 0 differing) |
| the same with `-c Debug` | ALL PASS, same hashes |
| `dotnet run -c Release --project tools/worldbench`, AC run 1 | **ALL PASS**, see Benchmark |
| the same, AC run 2 | Ground PASS. Meadow micro-detail FAIL at a throttled 2.55–2.64 GHz, which is the accepted deviation. |
| the same, 3 runs on battery | Reported, not judged |
| Smoke run: `--headless --path game --quit-after 120` | exit 0, 0 `ERROR` lines |
| `-- --map sample_patch --selftest map` | ALL PASS (17 checks) |
| `-- --map nope --selftest map-fallback` | PASS: one `ERROR:` line, placeholder visible |
| `-- --selftest worldquery` | ALL PASS (93 PASS lines, 98 s) |
| `-- --selftest noclip` | 4/4 PASS (6 m and 48 m at 30 and 144 fps) |
| `-- --map sample_patch --selftest map-view --view 0,60,110,0,-28`, windowed | PASS: screenshot saved; 632 fps average, 1 % low 278 at 1152×648 |
| `flypath` on `sample_patch` and on the synthetic 4 km map, windowed at 1920×1055, on battery | See "Budget check" |

## Benchmark (world-query "Benchmark" scenario)

Medians of 21 runs, on the worldbench's pinned core. Every timed section allocated 0 B.

| Budget | AC run 1 (3.26–3.42 GHz) | AC run 2 | Battery, 3 runs (2.1–2.45 GHz) |
|---|---|---|---|
| 100k ground samples in the physics pattern, < 10 ms | **9.219 PASS** | **9.631 PASS** (3.31–3.35 GHz) | 13.4–15.1 |
| `MicroDetailNear` r 2 m on meadow, < 0.25 ms | **0.199 PASS** | 0.294 at 2.55–2.64 GHz (deviation) | 0.251–0.419 |
| `MicroDetailNear` on every surface, < 1 ms (the tool prints these for information) | belt_straw 1.087 (deviation), yard_litter 0.797, others ≤ 0.24 | belt_straw 1.606, yard_litter 1.071 | belt_straw 1.53–2.15 |
| 100k swept capsules, < 100 ms | **36.4 PASS** | **50.1 PASS** | 68.1 (first run) |
| 100k rays of 2 m, < 100 ms | **17.7 PASS** | **24.3 PASS** | 29.9 (first run) |
| Composite step, < 60 µs | **21.9 PASS** (p99 41.4) | **28.3 PASS** | 30.1–36.0 |

**Judgement, per the design's rulings:**
- Only AC runs are judged (world-query spec:175, design.md:86). AC run 1 passes every judged line.
- Micro-detail over budget at throttled clocks is an accepted deviation (design.md:87-91), and so is belt_straw above 1 ms at the full clock (design.md:61, "1.07–1.15 ms"). Physics prefetches micro-detail on a worker thread.
- The battery lines aren't judged, even though the tool marks them FAIL (finding 3).

**Regression check.** I ran benchmarks back to back on battery, under the same conditions, to see whether the change got slower after 3.6 was accepted:

| Code | Ground (physics pattern) |
|---|---|
| 3.6 as accepted (`17edace`) | 12.6–13.0 ms |
| 3.8 (`d78e2e2`) | 14.9 ms |
| HEAD | 13.4–15.1 ms |

`SampleGround`'s code hasn't changed since 3.6. The gap is run-to-run noise (about ±7 %) plus the 4.3 `sample_patch`, whose flights now cross more mat surfaces. The composite step didn't move (27–31 µs).

## Scenarios

### map-format

| Scenario | Result | Evidence |
|---|---|---|
| Complete package | pass | The validator exits 0 on `sample_patch`, and test case "complete package" passes |
| Bad map size | pass | 300 m and 8448 m both exit 1 with "size_m … breaks the side rule". The loader case "bad map size" gives a one-line error. |
| Missing layer | pass | 5 cases, each naming its file. The loader case "missing layer" names `cover.png`. |
| Corner lookup | pass (QA spot-check) | A console probe built from `game/src/world` queried all four corners. `SampleGround` equals the raw bytes Python reads: NW −0.300 m, surface 6, straw 21.647/m² (channel 69 × 80/m²); NE 1.830 m, surface 2; SW −1.830 m, surface 1; SE 0.300 m, surface 1. So row 0 is north and column 0 is west. |
| Size mismatch | pass | "height.r16 is 132100 bytes, expected 132098". The loader case "height size" also passes. |
| Unknown surface | pass | "unknown surface index 42, first at row 10, column 20", and index 0 is rejected |
| Required fields | pass | 12 surface cases: missing field, negative stiffness, unload ratio < 1, soil reference missing, azimuth, mat depth min > max, mat kinetic > static, zero element length, zero element diameter, hook release, cover field, inverted range. Plus 3 catalog range cases: material kinetic > static, material stiffness out of range, and a wind volume that isn't a primitive. Each names the surface or asset and the field. |
| Unknown asset | pass | "object 3: unknown asset 'tank_hull'" |
| Unknown material | pass | Asset-level and shape-level cases, plus a collider with no material |
| Out of bounds | pass | An object case and a wire-point case, each naming the object |
| Start outside the map | pass | "start point 1 at x=20, z=129 is outside the map". A start inside passes. |
| Future major | pass | The validator rejects 2.0 naming both versions and accepts 1.3. The loader case "future major" gives one line and no map. `Sandbox.LoadMap` → `MapScene.Load` → `LoadPackage` (MapScene.cs:45) is the path `map-fallback` checks. |
| Georeference scan | pass | 13 cases: lat, nested lat, EPSG, geo fields, Latin and Cyrillic place names, a coordinate pair, MGRS, a catalog text, PNG tEXt, EXIF and a GIS sidecar. `geometry` is correctly allowed. |

### world-query

| Scenario | Result | Evidence (`--selftest worldquery` unless noted) |
|---|---|---|
| Matches the rendered surface | pass | 10,000 points: 0.000000 mm from the renderer's triangle, 0.0761 mm from the Jolt ray, 0 misses |
| Continuous across surface borders | pass | 4,088 border edges, 6.14 M samples. Largest step where the slope is ≤ 1 is 0.999 mm, and the largest jump is 0.439 mm (limit 1 mm). |
| Normal accuracy | pass | 9,947 points, worst 2.23e-4 rad. Inside pitfalls at a 1 µm step: 0 rad. |
| Micro-relief bounded | pass | All 9 surfaces are within −1.7 % to +2.6 % of the expected RMS (limit ±20 %), and so is `sample_patch` meadow |
| Pitfall is identifiable | pass | 41 pitfalls. Every hit has an id and a depth, and the depth matches within 1.5e-5 mm. |
| Outside the map | pass | 9 points, including ±1e12, NaN and ∞: flag set, clamped to the edge, nothing thrown |
| Replay identity | pass | A second Godot process gave the same hash, `c73649404d899d79-146d42b3e19fa28e` |
| Overlapping queries agree | pass | 3 sites: 0 missing, 0 differing, same order |
| Crossing straw is found | pass | A 0.960 m straw rooted 0.768 m from a 0.1 m sphere is returned |
| Lying elements lie on the mat | pass | Ends within 0.00024 mm of `SupportTop`. The 24,948 elements that touch no pitfall stay within +238 / −248 mm (limit 250 mm). 252 pitfall crossings are reported separately, 28 of them with an end in a pit (0.11 %), which is the accepted limitation. On the sample band: 0.00032 mm, +237 / −236 mm. |
| Density matches the surface | pass | belt_straw 60 / 250 / 2 per m², exactly as in the table (±0.00 %) |
| Overflow reported | pass | Buffer 128, count 386, and the prefix equals the first 128 of the full list |
| No tunnelling through a wire | pass | 180,000 phased sweeps and 5,000 random ones: 0 missed |
| Material reported | pass | A capsule on a launch rail gets `steel`. 9 shapes, 0 wrong. |
| Wire geometry | pass | Sample cable: span 30.018474 m, sag 0.6 m, diameter 0.012 m, all within 0.000000 mm of `objects.json` |
| Roof under a rotor | pass | 1,000 rays hit at 1 m within 8.9e-16 m, with `sheet_metal` and the runtime object's index. The roof is a test-only asset (design risk). |
| House shadow | pass | 6 m house yawed 15°: TopM 6, BaseM 0, porosity 0. Open cells report 0 m and porosity 1. |
| Tree crown | pass | BaseM 4 m and TopM 10 m. The path porosity across the crown is 0.508–0.535, against 0.5. |
| Door gap | pass | The gate's gap centre is within 9.5e-9 m, 1.6 × 1.98 m |
| Reset between flights | pass | One contact per bar after a reset (two without it), and the wind grid is bit-equal |
| Rails added | pass | 2 steel contacts, and a ray from 1 m reads 1.000 m on steel |
| Any change is detected | pass | 69,804 single-byte changes, 0 of them unchanged. A change on disk alters the hash in 7 of 7 files. QA recomputed the README's Python rule independently: `3635cf0a10155cc0`. |
| Golden file | pass (this machine) | Worldbench Release and Debug on .NET 10.0.9, and Godot (41 golden lines). There is no second machine or runtime to run it on (see Risks). |
| Concurrent use | pass | 10 s with 0 differing, in worldbench (Release and Debug) and in Godot |
| Benchmark | pass | AC run 1 ALL PASS (see above) |

### map-loading

| Scenario | Result | Evidence |
|---|---|---|
| Sample patch | pass | 43 objects at 0.000 mm (limit 10 mm). 3 wires with 320 pieces at 0.008 mm. 42 bodies and 361 shapes with 0 wrong material tags, and the Jolt rays land on the roof and the cable. Each of the 9 surfaces has its own material layer. In the screenshot the meadow, road, yard, rubble, crater, belt, north fields and weeds read as separate areas. Meadow and weeds are only just distinguishable, which is 5.1 item 2 and is already routed to `build-uat1-parts`. |
| Visual and physical stems agree | pass | Over belt_straw: 7,395 bases within 3 m, 0 not drawn, 0 extra, largest error 0.0000 mm |
| Fly the sample | pass | Camera at (0, 15, 0), 15.000 m above the terrain at the centre, with the placeholder hidden |
| Budget check | pass | On battery, "Best power efficiency", vsync off, 12 s paths. `sample_patch`: 326 fps average, 1 % low 158 (limit 120 / 90). 4 km: 393 fps average, 1 % low 254 (limit 90 / 60), first frame 8.1 s after engine start (limit 10 s). |

## Reviews and deviations

- **`surfaces-review.md` §3–§5.** Every value is in the shipped tables:
  - `soil_reference_diameter_m` is 0.015
  - the unload ratios are 3, 1.5, 8, 5, 3, 8, 2, 1.5, 3
  - no `soil.porosity` is left
  - tilled straw stiffness is 5, and tilled relief is 0.02 m at 0.3 m with 0.05 / 0.5 / 0° ridges
  - yard soil friction is 0.60 / 0.50
  - the three mats are present, and every cover has `hook_release_n`
  - the five materials have the proposed values
  - the tree crown has its `wind_volume`, and the rails are steel

  The §7 spec gaps are carried into the specs, as confirmed in api-review §1.
- **`api-review.md` F1–F8.** Every item is resolved:
  - F1: lying elements lie mat to mat (see that scenario), and the golden file was re-recorded at QueryVersion 2
  - F2: runtime objects reset between flights (see that scenario)
  - F3: the geometry lookup works (see "Wire geometry")
  - F4: an in-memory world reports 0.015 m
  - F5: `Material(NoMaterial)` returns null
  - F6: the golden file checks QueryVersion
  - F7: `DetMath.Pow` works for any b > 0, and physics' (b_ref/b)^0.3 comes out identical
  - F8: README "Contacts" explains grazes
- **`orchestrator-check.md` (5.1)** is recorded. Its four rendering items go to `build-uat1-parts`, and none of them changes physics.
- **The accepted deviations are recorded:**
  - micro-detail over budget at the measured clocks, prefetched on a worker: design.md:61 and 87-91
  - lying elements crossing pitfalls: design.md:60 and world-query spec:74
  - benchmarks judged on AC: design.md:86 and world-query spec:175
- **Game Developer review.** The proposal names the Game Developer as reviewer of the sandbox hook, but no task or review is recorded. QA ran both of the hook's scenarios ("Fly the sample" and the fallback).

## Hygiene, OPSEC and git

- **Determinism (W-13).** The query-path files never call `Math.Sin/Cos/Exp/Pow` or FMA. Angles and powers go through `DetMath.SinCosTurns` and `DetMath.Pow`, which use only + − × ÷, floor and an exact `ScaleB`. `Math` appears only in the selftests.
- **Dead code.** There are no TODOs, scratch files or debug prints. The one unused item is a constant, `Feet` (tools/worldbench/Program.cs:354), which is a nit. `ScalarReference` (MicroDetail.cs:49) is the golden file's scalar-path hook, which the API review accepted.
- **Tracked files.** Nothing from `reference/` is tracked except the allowed `reference/README.md`, which is already on main. Nothing from `.godot/`, `build/` or logs is tracked either.
- **LFS.** The three binary layers are LFS pointers, and `.gitattributes` adds `*.r16`. There are no other binaries.
- **OPSEC.** I scanned the whole branch since its fork (`a0514ca..f9ff61f`, 126 commits: diff, commit messages and paths) against the reference index, with every name masked:
  - 0 full paths, 0 file names, 0 long numbers and 0 recording timestamps
  - the remaining hits are 4–7-letter ordinary words that also appear in code identifiers
  - no personal names, account names or local user paths
  - real place names appear only as validator fixtures (the blocklist, 4 public test inputs, 1 docstring and 1 README example), and none of them is in the reference index
- **Git.**
  - The author equals the committer on every commit, and every identity is in team.md: World Artist 79, Orchestrator 34, Game Developer 11 (from the bootstrap merge), Physics Engineer 2.
  - There are no bodies, trailers or co-authors, and every non-merge subject is `type(scope):`.
  - One subject is over 72 characters: `9ee2b64` (74).
  - The merge into `origin/main` was clean when checked before PR #14.

## Findings

None of these blocks the merge.

| # | Severity | Where | Finding | Owner |
|---|---|---|---|---|
| 1 | process | PR #14, `423fc31` | The branch merged before 5.2 finished. This file and the 5.2 tick live only on the branch. Bring them onto `main` before `/opsx:archive`. | Orchestrator |
| 2 | advisory | commit `9ee2b64` | The subject is 74 characters. It's on `main` now, so leave the history as it is. | Orchestrator |
| 3 | advisory | tools/worldbench/Program.cs:166-172 | `Judge` scores battery runs, prints FAIL and exits 1. The spec says battery runs are "reported but not judged", so an unplugged run reads as a regression. Mark battery lines "not judged" and don't fail on them. | World Artist |

## Risks

- **Ground budget margin.** On AC at the nominal clock it's 4–8 % (9.22 and 9.63 ms against 10 ms). When "Best power efficiency" drops the clock to about 2.5 GHz, ground would go over the budget. The flight model adds its own cost on top, so it should profile its step early.
- **Golden file on one runtime.** It has run only on this machine and on .NET 10.0.9. The game targets `net8.0`, so a machine that has .NET 8 would run a runtime the golden file has never seen. Run `--golden` on a second machine or runtime before crash forensics relies on replay.
- **Micro-detail prefetch.** belt_straw takes 1.09 ms at the full clock. The physics change must honour the worker prefetch (design.md:61).
