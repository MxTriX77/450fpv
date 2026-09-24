# Review: study-wind-reference (task 3.1)

**Reviewer:** QA Engineer · **Branch:** `physics/study-wind-reference`, reviewed at `16e4185` (tasks 1.1–2.4), plus `airframes.md` from `705db14` for OPSEC and sources · **Against:** `specs/reference-wind/spec.md`, the pilot's answers, and D-011 as the orchestrator relayed it (weather is a fixed pilot setting: Calm / Windy / Severe, with no random option)

## Verdict

**Changes requested.**

The tracker and the attitude statistics are solid:
- the synthetic test reproduces byte for byte
- all 20 overlays I checked, and 5 frames I chose myself, hold the horizon within 1°
- every one of the 1157 values in the local statistics file reproduces exactly

The problems are in what the targets enforce, and in reproducibility and OPSEC around them:
- F1: the "intermittent, not Gaussian" reading of the kurtosis is wrong. T9 passes plain Gaussian noise, and nothing enforces "constant".
- F2: no target enforces the pilot's random wind-direction changes (Q2).
- F3: §6 is not yet complete for the D-011 Windy preset, and says nothing on Calm.
- F4: the rain numbers come from scratch code that will disappear.
- F5: the airframe model and the pilot's sortie profile are now public, which needs the user's decision.

Everything that depends on the take-off mass is marked **pending 2.5** below, not scored.

**Process note.** PR #11 was merged into `main` (`0a1e9df`) by the repo owner before this review. The merged content equals `16e4185`, the commit reviewed here, so the verdict applies to what is on `main`. The fixes need a follow-up PR from this branch, which now also carries `705db14` and `c6e4819`.

## How it was run

- **Synthetic test:** the unmodified tool, `track_attitude.py --selftest`, into a QA scratch folder. It took 337 s.
- **Statistics:** `wind_stats.py --csv reference/_frames/P/attitude.csv --segment 121-330 --json <scratch>` took 15 s. Its JSON was compared field by field with the local `reference/_frames/P/wind_stats.json`.
- **Spot check:**
  - I viewed the tracker's overlays and 1.5× zooms.
  - For my own frames, a QA script imports the tracker as a module (its `main()` does not run) and draws the horizon with the tool's own `project()` at native resolution. It reads the clip path from the index without printing it.
- **QA scratch scripts (not committed):**
  - kurtosis per terrain
  - the §6 procedure on five synthetic Gaussian "sim" logs through `wind_stats.analyse`
  - the existence of every §6.3 JSON field
  - a strict-JSON parse
  - R4 and R5 from `metrics.csv`
- **OPSEC:** a fragment scan built from the 16 names in `reference/_frames/index.md` (73 fragments, never printed), the real-place blocklist from `tools/map/place_blocklist.txt`, and regex scans for dates, clock times, coordinates, MGRS grid references, voltage, capacity, link values, unit words, Cyrillic and handles.
- **Nothing in `reference/` was written.**
  - The SHA-256 of `attitude.csv`, `attitude_k1_0.36.csv`, `wind_stats.json` and `metrics.csv` is identical before and after.
  - No file in `reference/_frames/P/` is newer than the start of the review.
  - The temporary overlays and crops sat in the QA scratch folder and were deleted after viewing.
  - `extract_frames.py` was not run.
- **Sources:** three of the pages cited in `airframes.md` were read: the frame maker's, OTU's, and the motor listing.

## Scenario results

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | Synthetic accuracy | **Pass** | 120 of 120 frames reported. Roll RMS 0.050° (max 0.23°), pitch RMS 0.099° (max 0.15°), yaw rate RMS 0.42 °/s. The k1 = 0.30 / 0.36 rows match `wind.md:83`. The self-test's `attitude.csv` and `truth.csv` are byte-identical to the Physics Engineer's run, so the tool is deterministic. |
| 2 | No guessing | **Pass** | Synthetic: highest confidence 0.076 (ground), 0.050 (sky), 0.000 (blue, snow). P: all 50 no-picture frames have confidence 0 and empty values. Only 2 other frames fall below 0.2, f956 and f957 (0.044 and 0.043): f956 is half black after the f955 loss, and the tracker's +15° pitch there is marked, not trusted. f914 (0.265, sync-tear bands) and f952 (0.327, impulse dashes) are also out. The statistics use U = 1348 = the 1362 high-confidence frames minus 14 guard frames next to losses. |
| 3 | Real-frame spot check | **Pass** | **11 of the 20 overlays:** f38, 116, 197, 338, 422, 575, 701, 886, 1050, 1176 and 1336, at left, centre and right. **5 of my own:** f222 (least bank, −1.79°), f279 (belt rock, −10.35°), f897 (least pitch, −19.12°), f1217 (largest bank, −14.42°) and f1411 (lowest included confidence, 0.502). In all 16 the skyline lies inside the ±1° band. f1217 and f1411 are marginal on the right, where near-belt crowns reach the +1° line and the fit runs between crown and base, as `wind.md:89` says. I also viewed the rejected f914, f952 and f956; all three are rightly low. |
| 4 | Complete measurement set | **Pass** (A5, A8) | Every statistic the spec lists is present for roll, pitch and yaw, with units and frame sets (§1.6). My re-run matches all 1157 values of the local JSON exactly. That file is stale (A3), and the fields it lacks match the notes too. See "Numbers reproduced". |
| 5 | Limits stated | **Pass** (labels); content: F1 | Every bullet in §5 and §7 carries measured, derived or assumed. The closed-loop limit (§5.1, `wind.md:253`) comes before the targets (§6, `:494`). |
| 6 | Testable targets | **Format: pass. Content: changes requested** (F1–F3) | Every target has a number, unit, tolerance, JSON field and procedure. Every JSON field named in §6.3 exists in P's JSON. The §6.2 step 5 command runs on a sim-style CSV (conf 1, no `edge_px`) with exit 0, but writes bare `NaN` (A2). |
| 7 | Rain traits mapped | **Pass** (A9); reproducibility: F4 | R1–R8 each cite P frames, a candidate effect and a driver. §7.4 cross-references N12 and the pilot's fiber hypothesis. My check of the lens is below. |
| 8 | OPSEC hygiene scan | **Open** (F5) | 0 file-name fragments, 0 real places, 0 coordinates or grid references, 0 clock times, and 0 OSD voltage, capacity or link values. The scan covered all change files, `wind.md`, `airframes.md`, both tools, all 20 commit messages with author lines, and the full diff. The remaining question is the airframe model and the sortie profile (F5). |
| 9 | Repo hygiene | **Pass** (A11) | The diff has 9 files at `16e4185` and 10 with `airframes.md`. There is nothing from `reference/`, `.godot/`, logs, builds or binaries. All 20 commits are subject-only, with no trailer, under the Orchestrator, Physics Engineer or Tech Artist identity. 19 are 34–71 characters; `c6e4819` is 76. Each role stayed in its files. The Tech Artist's three commits touch only `wind.md` lines ≥ 590 (§7 starts at `:588`). |

**Performance.** This change has no runtime code, so the 60 fps budget does not apply. Tool times: the tracker on P takes 238 s (from the Physics Engineer's log), the self-test 337 s, and `wind_stats` 15 s.

## Numbers reproduced

All values come from my `wind_stats` re-run, unless the row says otherwise. Values in brackets are the 16–84 % intervals.

| # | Quantity (`wind.md` line) | Notes | Re-run |
|---|---|---|---|
| 1 | Thrust to hold height (`:282`, `:383`) | 1.10 W (p95 1.13, max 1.14) | 1.0995 (1.1319, 1.1444) |
| 2 | Horizontal thrust (`:283`) | 0.418 W forward, 0.174 W left, 0.453 W at 22.6° | tan 22.708° = 0.4185; tan 9.108° / cos 22.708° = 0.1738; √ = 0.4532; atan = 22.56° |
| 3 | Sideways thrust per frame (`:284`) | 0.174 [0.165, 0.184], 90 % in 0.091–0.252, min 0.034 | 0.1743 [0.165, 0.184], 0.0911–0.2523, 0.0337 |
| 4 | Thrust direction (`:285`) | 12.5–31.0° (90 %), 4.9–33.4° (all) | 12.54–30.98°, 4.87–33.44° |
| 5 | Share of the tilt on the horizon (`:426–433`) | belt 0.64, field 0.80, whole 0.70; so 64 / 36 | 0.64, 0.80, 0.70 |
| 6 | Belt vs field roll~yaw coupling (`:426–427`) | residual +0.88 [0.85, 0.90] / +0.51 [0.39, 0.63]; rate +0.77 [0.73, 0.79] / +0.34 [0.25, 0.42] | +0.877 [0.847, 0.90] / +0.509 [0.39, 0.628]; +0.770 [0.73, 0.79] / +0.344 [0.249, 0.42] |
| 7 | T11a thrust-tilt residual std, belt / field (`:565`) | 1.72 [1.63, 1.72] / 0.60 [0.54, 0.64] | 1.725 [1.63, 1.72] / 0.598 [0.542, 0.642]; ratio 2.9 (≥ 1.8 holds) |
| 8 | k, and body p~r rate (`:416`, `:431`) | 0.386; +0.77 [0.70, 0.81] | 0.3860; +0.771 [0.704, 0.812] |
| 9 | Roll angle (`:130`, `:144`) | −9.11 [−9.59, −8.65], std 2.41, never above −1.8° | −9.108 [−9.59, −8.65], 2.412; highest −1.794° at f222 |
| 10 | Pitch range (`:145`) | −27.8 to −19.1° | −27.76° (f675) to −19.12° (f897) |
| 11 | Residual std and kurtosis (`:133–135`) | 0.71 / 0.31 / 0.83°; 4.74 / 3.81 / 9.18 | 0.707 / 0.311 / 0.826; 4.74 / 3.81 / 9.18 |
| 12 | Rates: std, p95 of \|x\| (`:136–138`) | 3.70 / 2.47 / 3.96; 8.37 / 5.19 / 8.64 °/s | identical |
| 13 | Body accelerations at p95 (max) (`:398`, `:405`) | roll 151 (412), pitch 97 (208), yaw 77 (288) °/s² | 151.5 (411.9), 96.7 (208.2), 77.1 (287.9) |
| 14 | Spectral bands, T6a–T6d (`:159–164`, `:556–559`) | 94 %; 0.114°; 32 % and 0.179°; 0.125° and 0.054° | 94.0 %; 0.1141; 31.9 % and 0.1791; 0.1256 and 0.0540 |
| 15 | Events: roll / pitch / yaw / either (`:195–198`) | 7 / 11 / 5 / 9, at 11.1 / 17.4 / 9.7 / 14.2 per min | identical, frame lists included |
| 16 | T8 gaps (`:205`) | median 2.04 s, CV 0.73 | 2.04, 0.73 |
| 17 | Whole-clip correlations (`:218–220`) | roll~pitch +0.21; roll~yaw residual +0.73, rate +0.57 | +0.206, +0.733, +0.573 |
| 18 | ACF 1/e time (`:224`) | 0.30 / 0.17 / 0.33 s | 0.301 / 0.167 / 0.334 |
| 19 | Belt / field residual std (`:232–234`) | 1.26 / 0.51, 0.43 / 0.28, 1.67 / 0.44; ratios 2.5, 1.6, 3.8 | 1.255 / 0.505, 0.433 / 0.276, 1.674 / 0.437; 2.49, 1.57, 3.83 |
| 20 | Coverage (`:106–111`) | 1362 / 1330 tracked; U 1348, Y 1333, R 1135, Ry 927 | identical, run lists included |
| 21 | 5 s blocks (`:239`) | 0.54/0.20, 1.39/0.46, then 0.50–0.61 / 0.21–0.34 | identical |
| 22 | Maximum rate in the second before each loss (`:244–247`) | four triplets | identical |
| 23 | Horizon edge width (`:184–185`) | 3.53 px (p5 3.11, p95 4.01, std 0.29); ≤ 0.15° of shake | identical. Quadrature 2.49 px ÷ 17.3 px/° (my derivation at 22.7° elevation) = 0.14° |
| 24 | Closed-loop gain and bandwidths (`:258`, `:268–270`) | \|S\| 0.50 / 0.69 / 0.88 / 0.97; 0.53 / 0.94 / 0.59 Hz | 0.504 / 0.686 / 0.883 / 0.967; 0.531 / 0.936 / 0.589 Hz |
| 25 | R5 and R4 frame luma and colour (`:697`, `:706`) | P 57 / 69 / 89, r/g 1.31, b/g 0.61; dry day medians 122–147 | From `metrics.csv` (unmasked): 57 / 69 / 88, 1.30, 0.62; 120–149. Close; the method differs by the mask. "About half" holds. |

## Do the targets enforce the pilot's requirements?

**"Constant and dynamic, never smooth"** is only partly enforced.
- **Dynamic: yes.** The floors of T1 and T2 over open field, T6b (2–4 Hz roll), the pitch shelf of T6c, and the belt bursts of T3, T4 and T7 all fail a sim that drifts smoothly.
- **Never periodic: yes,** through T8's CV, though it is noisy (A7).
- **Intermittent: no.** T9 is met by stationary Gaussian noise whenever the belt is louder than the field, and within the field P's roll is sub-Gaussian (F1).
- **Constant: nothing checks it.** A sim that is calm for 20 s and busy for 12 s can still meet T1's standard deviation. P's field is steady: every open-field 5 s block has a roll residual of 0.50–0.61° (F1).

**The yaw-versus-roll trade-off (T10b, T11a, T11b): enforced.**
- A sim whose gusts are only moments fails the s = 0 heading ratio of T11b and the belt ≥ 1.8 × field condition of T11a.
- The derivation e = φ + k·ψ with k = −sin θ is exact: I checked it against the ZYX kinematics and the §6.2 logging formulas.
- Two parts are weaker than they look (A6). The s = 1 ratio is close to an identity, and T10b partly measures the scripted pilot's fixed split s = 0.64.

**T0b and Q2 ("never a fixed vector"): not enforced.** The notes themselves say P cannot show direction changes (`:311`). They attribute the same 0.27 spread to turbulence and course changes (`:325`). A fixed-vector wind with enough turbulence therefore passes T0b (F2).

## D-011 presets (Calm / Windy / Severe)

- **Severe:** §6 is usable as the Severe acceptance test once F1 and F2 are fixed. Its test mass (`:528`) is pending 2.5. The wording at `:529`, "random direction changes switched on", implies a toggle. Under D-011 there is none: direction changes belong to the preset (F3).
- **Windy:** not yet usable as an automatic test.
  - §6.4 says T6b, the T6c RMS, T6d and T11a scale by c (`:576`), but the band table (`:581–583`) gives values only for T0–T5.
  - It does not say that the Windy test flies the same §6.2 procedure with T0 = 4.6 ± 1.5° setting the preset's mean crosswind.
  - Nor does it say that every target not listed stays unchanged (F3).
- **Calm:** §6 says nothing. P can't define it, but the notes should say so, so that Calm isn't tested against a wind band by mistake (F3, and a question below).
- D-011 is not yet in `docs/decisions.md` on any branch I can see.

## Rain on the feed

- **No drops on the lens: confirmed on what I viewed.** I checked the stills at 11 s and 35 s, and the lossless bursts f705–712 and f1410–1417, including the upper-right sky and skyline at 1:1 and the whole sky band. I saw no drop, refracting disc, streak or water-film edge.
- **The grey band is on the right edge too.** In f707 a second, lower image of the right blade sits against the ground near the skyline. In f708 and f709 it shows as the same translucent grey patch the notes describe at the left edge (`:756–764`). That supports the blade explanation; the notes should mention it (A9).
- **The scratch-code dependency is not acceptable as it stands (F4).**
  - R3's horizon-relative table and extinction bound, R5's levels, R6's rod edges, R7's veil, and §7.4's sparkle and grain figures all come from 15 scripts in the session scratchpad. That folder will not survive the session. The local `reference/_frames/P/rain/` holds their intermediate scans.
  - §1–§5 are held to "every number from a committed tool", and §7 feeds calibration caps for the video change (R3 ≤ 0.3 km⁻¹, R6 σ ≤ 0.7 px). §7 should meet the same bar.
  - I could reproduce only R4 and R5, approximately, from `metrics.csv`.

## OPSEC

| Category | Result |
|---|---|
| File-name fragments | 0 specific hits across all targets. Numeric hits are 2-digit tokens such as "10" and "00" inside ordinary numbers. The only longer numeric hit is the year inside the answer-recording date (`pilot-answers.md:3`, and `.openspec.yaml`). That full date matches no file name, and `study-flight-references/pilot-answers.md` carries the same kind of date. Not a finding. |
| Places, coordinates, grid references | 0. The Cyrillic in `airframes.md:3` and `:10` is product names, not places. |
| Dates and clock times | Only the recording date above. The other regex hits are CI brackets and fractions. |
| OSD values | No voltage, capacity, link, timer or altitude reading is quoted. The 20–50 m test heights are labelled assumed. **The airframe model name, which is also the on-screen craft name in P, is quoted** (F5). |
| People and unit | None named. The pilot's answers now also give a sortie profile: model, battery configuration, coil and cargo masses, the standoff distance and flight time (F5). |
| Commit messages | Clean. The only handle hits are the role e-mail addresses. |

## Pending 2.5 (not scored)

The following depend on the take-off mass and will be re-derived in task 2.5:
- mass table and layout (`:358–380`)
- collective thrust and margin (`:382–395`)
- roll, pitch and yaw authority percentages (`:397–409`)
- airspeed and crosswind (`:288–304`)
- crosswind fluctuations and eddy sizes (`:334–345`)
- §5.6 items 1 and 5 (`:464`, `:468`)
- §6.2 test mass (`:528`)
- R1 and R8's airspeed and prop-wash figures (`:644`, `:653`, `:747–753`)

Two things to carry into 2.5:
- The table's upper bound during P, 3.35 kg (`:367`), needs the 1.1 kg coil. With the stated 0.2–1.0 kg coil it is 3.25 kg.
- `airframes.md:19` gives 20–35 Ah as 6S4P or 6S5P. 21700 cells of about 5 Ah give 20–25 Ah (30 Ah at 6 Ah), so 35 Ah implies more cells or larger ones, and a heavier pack than the 1.7–2.2 kg at `:32`.

The non-mass numbers in §5.5 (1.10 W, 0.453 W, the 64 / 36 split, the coupling, T11a) are fractions of W or attitude statistics. They reproduce and won't change with mass.

## Findings (must fix)

| ID | Owner | Where | Problem | Fix |
|---|---|---|---|---|
| F1 | Physics Engineer | `wind.md:146`, `:349`, `:408`, `:467`, `:562` (T9), `:585` | **The heavy tails are the mix of belt and field, not intermittency.**<br>- Residual kurtosis, whole / belt / field: roll 4.74 / 2.28 / 2.51 (field [2.35, 2.76], sub-Gaussian); pitch 3.81 / 2.65 / 3.85; yaw 9.18 / 2.77 / 4.27.<br>- A Gaussian mixture at the measured belt and field stds gives 6.16 / 3.59 / 9.72.<br>- Five synthetic stationary Gaussian logs through `wind_stats` pass T9: roll 3.60–6.77 (mean 5.59), pitch 2.97–3.65 (mean 3.36).<br>- So T9 restates T3 and T4. "Intermittent gusts, not Gaussian noise" is not what P shows over the field. Yaw's kurtosis of 9 does not support "authority running out".<br>- Nothing checks "constant". | Restate §2, §5.4 item 3 and §5.6 item 4: P's burstiness is spatial (the belt wake), and the field wobble is steady and near-Gaussian (roll sub-Gaussian). Drop the kurtosis argument at `:408` and `:585`. Replace T9 with per-regime targets, or remove it. Add a constancy target on the existing `blocks` field: every open-field 5 s block within a band around P's 0.50–0.61° roll and 0.20–0.34° pitch. |
| F2 | Physics Engineer | `wind.md:550` (T0b), `:311–318` | **No target enforces Q2's random direction changes.** T0b measures the variability of the sideways thrust. By `:311` and `:325`, turbulence around a fixed vector produces that too. | Relabel T0b "crosswind variability". Add a T0c on the preset's own wind state over a 10 min history and several seeds: the spread about the prevailing direction, the rate of 45–120° shifts, and the share of 40 s windows that keep the crosswind on one side. Take the starting values from `:315–318`, marked tunable. |
| F3 | Physics Engineer | `wind.md:496`, `:529`, `:570–586` | **§6 is not complete for D-011.**<br>- "Switched on" implies a toggle that won't exist.<br>- The Windy band lists only T0–T5, although T6b, the T6c RMS, T6d and T11a scale too.<br>- The Windy procedure is not stated.<br>- Calm is not mentioned. | Name the presets as in D-011. Give a Windy column for every target, scaled or unchanged, flown with the §6.2 procedure, with T0 = 4.6 ± 1.5° setting the mean crosswind. State that direction changes are always part of Windy and Severe. State that P gives no Calm target, and point to where Calm acceptance will be defined. |
| F4 | Tech Artist | `wind.md:614` | **§7's numbers depend on scratch scripts that won't survive the session.** They can't be re-run by QA, by the video change, or later. | Commit the scan as `tools/reference/rain_stats.py`: headless Blender, letters only, writing only under `reference/_frames/<letter>/rain/`. Give its command in §7.1, as §1 does. If the orchestrator decides against it, record that in `design.md` as an accepted deviation. |
| F5 | Orchestrator (user decision) | `wind.md:362`, `:480`, `:527`; `:299`, `:366`, `:448`, `:484`; `pilot-answers.md:10`, `:14`; `airframes.md:18–23` | **The airframe model and the sortie profile are now public.**<br>- The model name is also the craft name in P's OSD. The `study-flight-references` review counted on-screen model names as a banned category and found 0.<br>- Together with the battery configuration, coil and cargo masses, the standoff distance and the flight time, the notes now describe the pilot's own sortie.<br>- This is already on `main` (`0a1e9df`). | Ask the pilot. If he agrees, record his OK in `pilot-answers.md`. If not, replace the name with "the 10-inch fiber cargo class" and drop the standoff distance in a follow-up. Git history keeps the old text either way. |

## Advisory

| ID | Owner | Where | Note |
|---|---|---|---|
| A1 | Physics Engineer | `wind.md:426`, `:565`; `wind_stats.py:48` | The belt intervals rest on 3 blocks of 60 frames, which allows only 10 distinct resamples. T11a's interval [1.63, 1.72] excludes its own value, 1.725. Use shorter blocks for segment intervals, or mark them unresolved. The ±35 % bands don't depend on them. |
| A2 | Physics Engineer | `wind_stats.py:420` | A sim-style log (no `edge_px`) gives 15 bare `NaN` tokens. Strict parsers, including .NET's default, reject them. Write `null`, or skip the edge block when the column is missing. |
| A3 | Physics Engineer | local `reference/_frames/P/wind_stats.json` | The file predates `e412f33`. It lacks the `airframe` block and the segment `tilt` and `corr` fields, while all 1157 shared values match. Regenerate it locally; it is not a repo file. |
| A4 | Physics Engineer | `wind.md:39` | "Frame edge at 57°, about 114°" describes an equidistant lens. The tool's own model (polynomial undistortion, then a pinhole with f = 1.005) puts the edge at 52.9° (about 106°). There is no effect near the horizon (< 0.1° at 22.7°). Describe what the tool computes. |
| A5 | Physics Engineer | `wind.md:132` | "Y 1321" labels the tracked yaw rate, but set Y is 1333. The 1321 leave out the held repeats. |
| A6 | Physics Engineer | `wind.md:564`, `:566` | T11b's s = 1 ratio is close to an identity once ψ is held. Only the s = 0 ratio and the ±25 % tilt condition discriminate. T10b's size partly reflects the scripted s = 0.64. Say so beside the table. |
| A7 | Physics Engineer | `wind.md:561` | T8's CV comes from about 6 gaps per run: my five Gaussian runs gave 1.09–1.34. Pool the gaps over the runs before taking the CV. |
| A8 | Physics Engineer | `wind.md:128–141`, `:193–224`, `:232–234` | Maxima, kurtosis, duration and gap percentiles, ACF times and the belt/field ratios carry no uncertainty, but the spec says every number. At least give kurtosis while it is a target: roll 4.74 [3.99, 5.16], pitch 3.81 [3.39, 4.26] (my bootstrap, same method). |
| A9 | Tech Artist | `wind.md:756–764` | The translucent blade image also appears at the right edge, under the right blade near the skyline (f707–f709). Add it. |
| A10 | Orchestrator | `airframes.md:10` | Two numbers are not on their cited pages. The frame page (the maker's) gives the 452 mm wheelbase but not 383 g; its 1706 g all-up weight is the maker's own build with other motors. The motor page is a reseller: it gives 112 g and 40 A, but not the 4.2 kg peak, and it recommends 9" props. Cite the pages that carry these numbers, or label them. The OTU-10 figures match their page. |
| A11 | Orchestrator | commit `c6e4819` | The subject is 76 characters, over the 72 in `docs/workflow.md`. It is already committed; keep future ones shorter. |

## Risks and questions

- **Merged before review.** The follow-up PR must carry F1–F5 and task 2.5 together, or `main` keeps a T9 that can't fail and the unscored mass values.
- **Camera field of view (for 2.5).** `airframes.md:10` lists a CADDX Ratel 2 for this build. If that is P's camera, its published lens field of view is the first independent check of the assumed focal length, which sets the pitch and yaw scale, k and 1.10 W. Roll does not depend on it.
- **Calm (question for the orchestrator):** where will Calm's acceptance be defined? The manifesto's motor asymmetry "yank" (§2.4) should still be felt with no wind.
- **Concurrent work:** someone else wrote into this worktree during the review, adding `airframes.md` before it was committed. Only `review.md` is in this commit.
