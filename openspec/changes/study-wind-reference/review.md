# Review: study-wind-reference (task 3.1)

## Round 2 (re-review at `58e7eb8`)

**Reviewer:** QA Engineer · **Branch:** `physics/study-wind-reference` at `58e7eb8` (21 commits since round 1, `b47a011`) · **Against:** `specs/reference-wind/spec.md` as amended in `4d42fda` (targets per weather level), D-011, and my round-1 findings F1–F5 and A1–A11 below

### Verdict

**Changes requested.** Three small fixes remain; everything else is approved in substance.

Round 1's five findings are fixed as asked, except that F2's new target doesn't hold up when run:
- **F1:** fixed. I ran `--gauss 5` and `--gauss 200` myself, and every cell of the §6.5 table and every 200-log range reproduces. P passes T9a, T9b and T12. Stationary Gaussian noise fails T9b and T10b, and calm/busy noise fails T9a, T12 and T10b.
- **F3, F4, F5:** fixed.
  - Windy has a band for every scaled target, and Calm has ceilings.
  - `rain_stats.py` reproduces the §7 numbers I compared, more than 60, except one transcription (A16).
  - The branch tip names no airframe as the pilot's, and gives no standoff distance and no flight duration.
- **Task 2.5:** reproduces from its stated inputs: the mass table, all seven inertia loadouts, the thrust curve, the margin, the authority and the airspeed. Two derived percentages are about one point high (A12).

The three fixes:
- **F6: T0c is not reliable as specified.** On a 5 × 10 min history, the notes' own starting-value model fails it in 39 of 200 seed sets. A shift schedule passes its "not on a schedule" test.
- **F7: what varies between flights is contradictory.** `wind.md:539` says nothing is drawn at random between flights, but `:329` wants a prevailing direction per flight.
- **F8: the spec and the notes disagree on Calm.** The amended requirement asks for an event rate and a correlation at every level. Calm, reasonably, has neither.

**Process and OPSEC note.** PR #12 (`5987385`, by the repo owner) merged this branch into `main` at `c6e4819`, again before QA approval. It came before the OPSEC fixes. So `main`'s **current tree**, not just its history, still names the airframe model as the pilot's and still gives the sortie details (see Risks).

### Round-1 findings

| ID | Status | Evidence |
|---|---|---|
| F1 | **Resolved** | Restated at `wind.md:153`, `:366–369`, `:453`, `:512` and `:630–633`. T9 is replaced by T9a and T9b (`:616–617`). T12 is the constancy target (`:622`). The self-check is `wind_stats.py:464`. See "F1 evidence". |
| F2 | **Partly resolved** (F6) | T0b is relabelled "crosswind variability" and says it doesn't test direction changes (`:603`). T0c exists (`:604`), with its log format (`:589`), a tool (`wind_stats.py:429`) and a pass rule (`:593`). Every T0c field exists in the `--wind-log` JSON. A fixed vector and a jittered fixed vector both fail it. It is not reliable as specified (F6). |
| F3 | **Resolved** (F7 and F8 are new wording issues) | The levels are named (`:539`), and "switched on" is gone (`:577`: "part of the level"). The scaled and unchanged lists are at `:640–643`. Windy has a band for every target (`:653–673`), and the Windy/Calm procedure is at `:675`. Calm is defined, with its floor sent to the flight-model change (`:647–651`). Direction changes are part of every level (`:539`, `:643`). |
| F4 | **Resolved** | `tools/reference/rain_stats.py` is committed, and its command is in §7.1 (`:730–737`). It writes only `reference/_frames/P/rain_stats.json` and `rain_stats/` (`rain_stats.py:609`, `:617`). One transcription is off (A16). See "F4 evidence". |
| F5 | **Resolved on the branch** (a residual on `main`, see Risks) | `wind.md:380`, `:525`, `:531` and `:575`, `pilot-answers.md:10` and `:14`, and `tasks.md:18` and `:22` are class-level. `airframes.md:16–23` describes the pilot only at class level, and `:3–14` names the public airframes as class research. Nothing ties an airframe to P. See "OPSEC". |
| A1 | Resolved | 1 s blocks inside and outside a segment (`wind_stats.py:53`, `wind.md:70–71`). T11a's interval is now [1.31, 1.90] and contains 1.725 (`:471`). The belt caveat is at `:475`. |
| A2 | Resolved | `finite()` writes `null` (`wind_stats.py:509`, `:547`). My round-1 sim-style log now gives 0 `NaN` tokens and parses with a parser that rejects NaN. So do P's JSON and the wind-history JSON. |
| A3 | **Open** (local only) | The local `reference/_frames/P/wind_stats.json` has the same SHA-256 as in round 1 (`b8187b7a…`), so it is still stale. |
| A4 | Resolved | `wind.md:41–42`: atan(1.33 / 1.005) = 52.93°, about 106°. |
| A5 | Resolved | `:139`: 1321 = Y 1333 less 12 held repeats. The tool prints n 1321. |
| A6 | Resolved | `:626–628`. |
| A7 | Resolved | T8 pools the gaps over runs (`:592`, `:615`), and the tool writes `gaps_s`. |
| A8 | Resolved | Kurtosis intervals are in §2 (`:137–148`) and §4.4 (`:239–240`). `:72` states which numbers carry no interval, and why. |
| A9 | Resolved | `:901–905`. |
| A10 | **Open** | `airframes.md:10` is unchanged: 383 g and the 4.2 kg peak are still not on their cited pages. `wind.md:384` now says so, but still labels the 0.38 kg "published" (A13). |
| A11 | Resolved | The five new Orchestrator subjects are 55–62 characters. |

### Findings (must fix)

| ID | Owner | Where | Problem | Fix |
|---|---|---|---|---|
| F6 | Physics Engineer | `wind.md:589`, `:604`, `:337` | **T0c is not reliable as specified.**<br>- **Too little history.** I implemented the §5.3 starting values: a mean-reverting meander of std 30° with τ = 2 min, plus Poisson shifts at 1 per 5 min of ±45–120° over 10–30 s, each relaxing with τ = 2 min. On 5 seeds × 10 min it fails T0c in **39 of 200** independent seed sets: the side kept is outside 75–98 % in 25 (p5–p95 0.818–0.990), and the gap CV is below 0.35 in 14. So `:337`'s "chosen to meet it" holds only about 80 % of the time. With 20 seeds × 10 min, or 5 × 60 min, it passes 100 of 100.<br>- **The CV floor doesn't catch a schedule.** The same meander with shifts every 3 min exactly passes 155 of 200 at 5 × 10 min and 100 of 100 at 20 × 10 min. Its gap CV stays near 0.4–0.5 (p5–p95 0.39–0.51 at 20 × 10 min, 0.40–0.50 at 5 × 60 min), because the meander adds irregular crossings. Random shifts give 0.46–0.70 and 0.60–0.81 at those lengths. At 5 × 60 min the schedule fails 52 of 100, but through the spread and side checks, not the CV.<br>- Controls work: a fixed vector fails (spread 0°), and so does a fixed vector with 5° jitter (spread 4.9°). | Raise the minimum history to at least 5 seeds × 60 min, or 20 × 10 min. Either set the CV floor where it separates random from scheduled at that length (about 0.55 at 5 × 60 min in my runs), or drop "not on a schedule" from T0c. Show the evidence beside T0c or in §6.5, the way §6.5 does for T9 and T12: run the §5.3 model through `--wind-log`. My model is one reading of `:333–334`; if yours differs, show yours. |
| F7 | Physics Engineer (question to the orchestrator) | `wind.md:539` against `:329` and `:604` | **What varies between flights is contradictory.** `:539` says "nothing about it is drawn at random between flights". `:329` asks for "a prevailing direction per flight", and T0c measures the prevailing direction against "the preset's direction". D-011 says the wind within a level "stays alive and unpredictable". Read literally, `:539` makes every Severe flight identical, or fixes the wind's direction on the map. | Say what is fixed per level (its parameters: strength, turbulence, shift rate) and what is drawn per flight (the random seed, and the prevailing direction if the orchestrator confirms). Word T0c's "preset's direction" to match. |
| F8 | Orchestrator | `specs/reference-wind/spec.md:56` against `wind.md:650`, `:663–673` | **The spec and the notes disagree on Calm.** The amended requirement asks for residual stds, a spectral band, a gust-event rate and a cross-axis correlation "for each level". Calm has ceilings on the stds and T6d, with T7 and T10 "not applied". The notes' reason holds: at Calm the airframe's own disturbances dominate, and self-normalised event rates and correlations would measure the airframe, not the wind. | Amend the requirement: Calm has ceilings only (T0, T0c, T6d and the wind-scaled spreads), and its "never still" floor belongs to the flight-model change. Otherwise the Physics Engineer adds Calm bands for T7 and T10, which I don't recommend. |

### Advisory

| ID | Owner | Where | Note |
|---|---|---|---|
| A3 | Physics Engineer | local `wind_stats.json` | Still stale (see above). Not a repo file. |
| A10 | Orchestrator | `airframes.md:10` | Still open (see above). |
| A12 | Physics Engineer | `wind.md:435`, `:451` | The nominal share of thrust used is 1.0995 × 4.8 / 9.37 = **56.3 %**, not 57 %, and the loaded pair runs at 0.563 × 1.38 = **78 %**, not 79 %. The margin (1.77, so 1.8), the throttle (0.75) and the 72 % saturation point are unchanged. |
| A13 | Physics Engineer | `wind.md:384`, `:389` | Labels. The 0.38 kg frame mass is labelled "published" while the note says it isn't on its cited page; call it assumed, or cite a page that has it. "Typically ≈ 1 kg" of cargo is labelled "class", but no cited source gives a typical value, and it sets the nominal 4.8 kg; call it assumed. |
| A14 | Physics Engineer | `wind.md:314`, `:419` | Two ranges need a qualifier.<br>- `:314`: at 22 m/s, d_r ≈ 0.10 needs C_D A = 0.035. The nominal 0.05 needs 0.06 s⁻¹, below the assumed 0.1–0.4. Add "with C_D A at its low end".<br>- `:419`: the 3 kg-pack loadout (5.8 kg) puts the centre of mass 0.3 cm *above* the arm plane. Say "with the 1.7–2.2 kg packs". |
| A15 | Physics Engineer | `wind.md:604` | `wind_history.` isn't a key: the `--wind-log` JSON has `prevailing_deg`, `spread_deg`, `runs`, `shifts` and `side_keep` at its top level. Say "fields of the `--wind-log` JSON". |
| A16 | Tech Artist | `wind.md:846` | The dry day chroma range is **19.2**–39.1 (A = 19.16 in `rain_stats`), not 19.6–39.1. If A is left out on purpose, say "B–F". P's 20.8 is inside either way. |

### How it was run

- **Everything I ran used the branch tip, `58e7eb8`, and Blender 5.2.1.** QA scratch folder: `qa-wind2`.
  - `wind_stats.py -- --letter P --segment 121-330 --json <scratch>`: 12 s.
  - `--gauss 5`: 16 s. `--gauss 200`: 86 s.
  - `rain_stats.py -- --reuse`: 5.5 s of wall time, 1 s inside the tool. Before the run I checked that all nine per-frame files are version 1, so nothing was decoded.
  - `--csv` on my round-1 sim-style log, and `--wind-log` on my own model histories.
- **QA scratch scripts (not committed):**
  - the 2.5 re-derivation from the stated inputs, including the §5.5 component layout (parallel-axis sums)
  - the T0c model and its controls, over 200 independent seed sets and 100 for each longer configuration
  - a check that every §6.3 JSON field exists, with a parser that rejects NaN
  - the round-1 OPSEC scan, extended
- **Nothing in `reference/` changed.**
  - The SHA-256 of all 14 files is identical before and after: `attitude.csv`, `attitude_k1_0.36.csv`, `wind_stats.json`, `rain_stats.json`, `metrics.csv`, and the nine `rain_stats/*.npz`.
  - The only file with a newer time is `rain_stats.json`, which `rain_stats.py` rewrote byte for byte, as documented.
  - `extract_frames.py` was not run. `wt-map-format` was not touched.
- **Performance:** this change has no runtime code, so the 60 fps budget doesn't apply. Tool times are above.

### F1 evidence (`--gauss`)

`--gauss 5` matches every cell of the §6.5 table (`wind.md:689–698`); bold fails the band:

| Family | T9a | T9b | T12 CV roll / pitch | T12 min roll / pitch | T10b | Old T9 |
|---|---|---|---|---|---|---|
| Stationary | 2.94 (2.51–3.30) | **2.91** | 0.23 / 0.10 | 0.63 / 0.83 | **0.00** | 6.51 / 3.40 |
| Calm/busy | **4.99** (3.35–6.55) | 4.66 | **0.46 / 0.39** | **0.41** / 0.56 | **−0.03** | 7.85 / 4.68 |

`--gauss 200` reproduces every range in `:705–712`:
- **Stationary, means of 5:** T9b 2.81–3.22, T9a 2.49–3.10, CV 0.14–0.25 and 0.10–0.16, smallest block 0.59–0.78 and 0.73–0.84.
- **Calm/busy, means of 5:** T9a 3.88–5.32, CV 0.37–0.52 and 0.35–0.46.
- **Single stationary logs, 5–95 %:** field roll 2.50–3.41, field pitch 2.64–3.39 (maximum 4.29), pitch CV 0.07–0.19.

**P on the new targets:** T9a 2.514 (band 2.2–3.5), T9b 3.850 (3.3–5.5), T12 CV 0.171 and 0.226 (≤ 0.32), smallest 0.653 and 0.622 (≥ 0.50). §5.4 item 3's 0.65–1.20 and 0.62–1.21 reproduce as 0.653–1.197 and 0.622–1.214.

### F2 and F3 evidence

- **Windy (c = 0.5):** every scaled value is exact.
  - T0 is atan(0.5 · tan 9.108°) = 4.58°, band 3.01–6.07°.
  - T1–T5, T6b, the T6c RMS and T11a are half of Severe. Rounding narrows T11a's field band from 0.195–0.405 to 0.20–0.40.
  - The shares, event rates, kurtosis, correlations, T11b, T12 and T6d stay as Severe, as `:641–642` says.
  - The mean crosswind is 2.55 m/s (1.23–5.76).
- **Calm (c = 0.15):**
  - Crosswind 0.76 m/s (0.37–1.73).
  - The T0 ceiling of 2° lies above the scaled 1.38° (0.90–1.83°).
  - The spread ceilings equal Windy's lower bounds.
- **§6.3 fields:** all exist in P's JSON, and every P value in the table reproduces.
  - T0b: 0.0471 / 0.1743 = 0.27, min 0.0337.
  - T6a 94.0 %, T6b 0.1141, T6c 31.9 % and 0.179, T6d 0.1256 and 0.0540.
  - T7 14.2 /min with 7 of 7 roll events inside the belt; T8's six gaps.
  - T10a +0.206, T10b +0.573 / +0.770 / +0.344, T11a 1.725 / 0.598, k 0.3860.

### Task 2.5 evidence (re-derived from the stated inputs)

| Quantity (`wind.md`) | Notes | QA |
|---|---|---|
| Pack mass, 24 / 30 / 42 cells (`:387`, `:397`) | 1.7–2.2 kg; about 3.0 kg | 1.739 / 2.174 / 3.043 kg |
| Take-off, typical (`:390`) | 4.4–6.5 kg; 4.9–5.5 kg | 4.381–6.531; 4.881–5.531 |
| During P, typical, nominal (`:392`) | 3.6–6.5; 4.1–5.5; 4.8 kg | 3.581–6.531; 4.081–5.531; 4.806 |
| Cargo with the 35 Ah pack (`:397`) | at most about 1.2 kg | 1.13 (heavy parts) to 1.28 (light parts) |
| Inertia, roll / pitch / yaw, seven loadouts (`:416`) | 0.033–0.057, 0.040–0.072, 0.043–0.060; nominal 0.044 / 0.054 / 0.050 | 0.0332–0.0566, 0.0395–0.0724, 0.0432–0.0600; 0.0442 / 0.0542 / 0.0500. Unchanged with the legs on the axes instead of the diagonals |
| r_g, and yaw ÷ roll (`:417–418`) | 0.093–0.098, 0.100–0.107, 0.094–0.110; 1.0–1.3 | identical; 0.99–1.30 |
| Centre of mass, full coil (`:419`) | 1–2 cm below, 1–3 cm forward, coil 8–9 cm below | −1.1 to −2.2 cm, +1.1 to +3.1 cm, 7.8–8.9 cm. The 3 kg pack gives +0.3 cm (A14) |
| Pack voltage; thrust per motor, total (`:429–431`) | 16.8–21.7 V; 2.3–3.1 (2.6) kgf; 9.0–12.5 kgf | 16.8 / 18.6 / 21.7 V; 2.257 / 2.602 / 3.118; 9.03–12.47 |
| Take-off thrust-to-weight (`:432`) | 1.4–2.8, typical 2.0 | 1.39–2.83; 2.00 |
| Available during P; share of it used (`:435`) | 7.2–11.9 (9.4) kgf; 33–99 %, nominal 57 % | 7.22–11.85 (9.37); 33.4–99.0 %, nominal **56.3 %** (A12) |
| Margin; throttle (`:436`) | 1.0–3.0 (1.8); about three-quarters | 1.01–2.99 (1.77); 0.751 |
| Roll / pitch differential (`:444`) | 1.3–1.5 % / 3.6–4.0 %; 1.0–1.1 % / 2.1–2.4 % | 1.32–1.47 / 3.60–4.00; 0.98–1.12 / 2.11–2.41 |
| Yaw differential (`:449`) | 5.5–10 %; 21–38 % | 5.5–10.1; 20.6–37.6 |
| Airspeed V_a (`:310`) | 13 (9–25) m/s | 13.28 (8.91–24.55); the extremes are the extremes of all corners |
| Crosswind; lean per m/s (`:311–312`) | 5 (2.5–11.5) m/s, 9–41 km/h; 23.4° / V_a, 1.8 (0.95–2.6)° | 5.09 (2.46–11.53), 8.8–41.5; 23.34, 1.76 (0.95–2.62) |
| Cruise cross-check (`:314–316`) | d_r 0.1–0.15; 32–41° pitch; 6.5–8.5 m/s | 0.153 at 17 m/s; at 22 m/s, 0.104 with C_D A 0.035 but 0.062 at the nominal (A14); 31.8–41.2°; 6.52–8.44 |
| Turbulence (`:342–353`, `:360`) | σ_v 1.4 (0.7–3.1); 1.0 (0.7–1.8); 1.7–2.8 (1.1–5.3) m/s; eddies 2.5–11 m, 5–20 m; 4–6 m, 8–11 m | 1.38 (0.66–3.11); 0.98 (0.66–1.81); 1.71–2.84 (1.15–5.26); 2.5–11.0, 5.3–19.6; 3.7–6.0, 8.0–10.6 |
| Prop wash at 4.8 kg (`:891`) | 9.7 m/s | 9.74 m/s (QA and the tool) |

**Labels.**
- Measured: 1.10 W, the accelerations and the lean.
- Derived: the mass sums, inertia, thrust, V_a and the crosswind.
- Assumed: FM, η, R_cell, the derate, the drag coefficients, the layout and the coil paid out.
- Published: the wheelbase, the motor mass and rating, OTU's MTOM and the class cruise.

These are right, except A13. "Derived" for the pack mass is fine, because the 69 g cell is marked assumed. The cruise cross-check is honest: nominal drag does not reach the published cruise, and the notes say so and ask the pilot (Q14).

### F4 evidence (`rain_stats.py --reuse`)

Every number below matches the tool's printout except one (A16).
- **§7.1:**
  - Frames with a horizon: P 1338, A 392. P's set is 1340 frames. The mask is 14.2 %.
  - The horizon fit agrees within 0.116° of roll and 0.050° of pitch (p99), on the same 1362 frames as `attitude.csv`.
  - Distances 382–2864, 94–572 and 17–107 m.
- **R1:**
  - Relative texture 0.0176. 48 stills, share < 6.1 %.
  - Maximum edge 4.28 px (< 4.5). Blobs 99.6 % and 98.0 %.
- **R2:** 0.229°, 3.86 px, one sample 4.27 px.
- **R3:**
  - Sky luma 147 against 225, chroma 1.4 against 11.5.
  - Luma ÷ sky: P 0.302 / 0.360 / 0.401, A 0.658 / 0.660 / 0.502.
  - Chroma: P 9.7 / 21.0 / 30.7, A 5.8 / 15.6 / 26.1.
  - Relative texture: P 0.188 / 0.157 / 0.166, A 0.051 / 0.037 / 0.089.
  - Far ÷ near: P 0.63 / 0.74 / 0.89, A 1.24 / 1.32 / 1.42.
  - Chroma far ÷ near 0.32 and 0.22. Relative texture far ÷ near 0.57 (A).
  - Skyline 127.1.
  - Contrast 0.588 against 0.208–0.430.
  - Koschmieder: t ≥ 0.847 at 0.57–1.43 km, extinction 0.116–0.289 km⁻¹, visibility 13.5 km.
- **R4:** r/g 1.309 and b/g 0.610, against 0.977–1.158 and 0.791–0.931.
- **R5:**
  - Luma 56.88 / 69.07 / 88.60. The dry day medians are 122.1–147.4. L is 41.8 / 53.1 / 62.6.
  - Grain 0.53 (0.46–0.59), 0.37 %, plain 1.52.
  - Chroma 20.8 against **19.16**–39.09 (A16), L 1.2.
- **R6:**
  - Edge: P 3.11 / 3.50 / 4.01 (std 0.29), A 2.30 / 3.07 / 4.18. Blur 1.68 px, σ 0.66 px, 0.15 of a sample.
  - Rod mean image 6.71 against 5.35 px. Per frame: P 3.59 / 5.05 / 6.11 in 1137 of 1340 frames, H 2.28 / 2.66 / 3.08 in 98 of 597.
  - Bursts: 5.84–6.48, 4.70–5.61, 2.87–5.69 px (f1412 = 2.866 in the per-frame file), and H 2.59–2.84 px.
  - Undershoot 0.17–0.38 against 0.15–0.38.
- **R7:** veil 2.41 / 4.52 / 11.48, 2.5 %. B–F: 18.4–45.2 levels, 8.1–23.1 %.
- **Edge bands:** left 1.5 % against C–E 0–2.8 %; right 4.6 % against 0–0.7 %; 19 of 60 gaps are 2 frames.
- **§7.4:**
  - Sparkle median 0.000; the busiest frame is f387 (0.2254).
  - The pre-onset maxima are 0.000 / 0.003 / 0.067 / 0.000, and f952 is 0.067.
  - Grain 2 s before: 0.593 / 0.526 / 0.530 / 0.567. 28 % of 2000 draws; belt 0.511 against field 0.534.
  - Edge around the outages 3.22–3.98 px, maximum 4.18.

### OPSEC

The scan covered every file of the change (`wind.md`, `airframes.md`, `pilot-answers.md`, `tasks.md`, `review.md`, `proposal.md`, `design.md` and `spec.md`), both tools, the 21 commit messages since round 1 (author, subject and body), and the diff since round 1. It is the round-1 scan (73 fragments from the 16 names in `index.md`, never printed, plus the place blocklist and the regexes), extended with airframe-model names, km distances, durations, and standoff and sortie words.

| Category | Result |
|---|---|
| File-name fragments | 0 specific hits. The only numeric hits longer than 2 digits are the year inside the two process dates in `pilot-answers.md:3` and `:10`: the answers' recording date and the date of the pilot's OPSEC decision. Neither full date matches a file name, and round 1 accepted the same kind of date. |
| Places, coordinates, grid references, clock times, unit words | 0. The "coordinate" regex hits are confidence intervals. |
| OSD values | 0. The two voltage hits (`wind.md:429`, `:431`) are model inputs: per-cell OCV and pack sag. |
| Airframe tied to P | **None on the branch tip.** `wind.md` names no model; its "model" regex hits are false positives inside "separately", and `:385`'s "3115 size, 900 Kv" is a generic part description. `airframes.md` names Vyriy, BOMBUS, OTU and Beshketnyk only as public class research (`:3–14`); the pilot appears only at class level (`:16–23`). |
| Standoff distance, flight duration | **None.** The km figures are crosswinds, cruise speeds, optical distances or published spool lengths. The durations are model parameters, tool run times or OTU's published endurance. "Far from the drone" (`pilot-answers.md:14`, `wind.md:493`) carries no number. |
| People, handles, commit messages | None. The only handle hits are the role e-mail addresses. |
| My own round-1 text | Three lines hinted at the tie between P and a specific airframe: the OPSEC row, the F5 row and the camera bullet. I redacted them in this commit; nothing else in round 1 changed except the heading levels. |

### Hygiene

- The branch diff against `origin/main` (merge base `c6e4819`) has 8 files: docs, openspec and two tools. There is nothing from `reference/`, `.godot/`, logs, builds or binaries.
- All 21 commits since round 1 have no body or trailer. Their subjects are 52–71 characters, under the Physics Engineer (9), Tech Artist (7) or Orchestrator (5) identity, and author equals committer.
- Each role stayed in its files. The Tech Artist's four `wind.md` commits touch only lines ≥ 730 (§7 starts at `:716`), and the Physics Engineer's end before §7.
- `tasks.md` is not ticked by me.

### Risks and questions

- **OPSEC on `main` (user decision).**
  - `origin/main`'s current tree, from PR #12, still carries the pre-fix `airframes.md`, `pilot-answers.md` and `wind.md`. That means the airframe model named as the pilot's, the standoff distance, the flight duration and the cargo mass.
  - Merging this branch removes them from the tree. The history, and any clone or fork of the public repository, keeps them. Only a history rewrite and force-push would remove them, and that is the user's call.
  - Until the follow-up merges, the current tree is the worst case, so merge it as soon as F6–F8 are fixed.
- **Merged before review, twice.** PR #11 and PR #12 were merged before QA approval. The follow-up PR should wait for this `review.md`.
- **T9b's jolts may be the pilot's own inputs.** `:369` says P can't tell vertical gusts from throttle and pitch inputs. T9b's floor (3.3) makes the wind produce all of P's field pitch jolts. If some were the pilot's own inputs, a human flying the sim adds his own on top, and pitch may feel jumpier than reality. Watch this in the MVP flights.
- **The thrust derate at the fast corner.** The 0.80–0.95 (`:433`) is assumed flat over 9–25 m/s. At 25 m/s the axial inflow through a disc tilted 22.7° is about 9.6 m/s, comparable to the hover induced velocity (9.7–11 m/s over 4.8–6.5 kg), so the derate there is probably lower. The heavy, fast corner may be infeasible, which would narrow the airspeed range. This is for the rotor model in the physics change, not for these notes.
- **Question for the orchestrator (F7):** is the prevailing wind direction drawn per flight, or fixed in the preset?
- **Question for the orchestrator (F8):** Calm's ceilings (T1 ≤ 0.18° and so on) cap the whole closed-loop wobble. Should the Calm test fly with the flight model's motor asymmetry and ESC jitter on? If so, those ceilings also cap them.
- **D-011** is in `docs/decisions.md` only on `chore/uat-release-plan` (`760788d`), not on `main` or this branch.

## Round 1 (reviewed at `16e4185`)

**Reviewer:** QA Engineer · **Branch:** `physics/study-wind-reference`, reviewed at `16e4185` (tasks 1.1–2.4), plus `airframes.md` from `705db14` for OPSEC and sources · **Against:** `specs/reference-wind/spec.md`, the pilot's answers, and D-011 as the orchestrator relayed it (weather is a fixed pilot setting: Calm / Windy / Severe, with no random option)

### Verdict

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

### How it was run

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

### Scenario results

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

### Numbers reproduced

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

### Do the targets enforce the pilot's requirements?

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

### D-011 presets (Calm / Windy / Severe)

- **Severe:** §6 is usable as the Severe acceptance test once F1 and F2 are fixed. Its test mass (`:528`) is pending 2.5. The wording at `:529`, "random direction changes switched on", implies a toggle. Under D-011 there is none: direction changes belong to the preset (F3).
- **Windy:** not yet usable as an automatic test.
  - §6.4 says T6b, the T6c RMS, T6d and T11a scale by c (`:576`), but the band table (`:581–583`) gives values only for T0–T5.
  - It does not say that the Windy test flies the same §6.2 procedure with T0 = 4.6 ± 1.5° setting the preset's mean crosswind.
  - Nor does it say that every target not listed stays unchanged (F3).
- **Calm:** §6 says nothing. P can't define it, but the notes should say so, so that Calm isn't tested against a wind band by mistake (F3, and a question below).
- D-011 is not yet in `docs/decisions.md` on any branch I can see.

### Rain on the feed

- **No drops on the lens: confirmed on what I viewed.** I checked the stills at 11 s and 35 s, and the lossless bursts f705–712 and f1410–1417, including the upper-right sky and skyline at 1:1 and the whole sky band. I saw no drop, refracting disc, streak or water-film edge.
- **The grey band is on the right edge too.** In f707 a second, lower image of the right blade sits against the ground near the skyline. In f708 and f709 it shows as the same translucent grey patch the notes describe at the left edge (`:756–764`). That supports the blade explanation; the notes should mention it (A9).
- **The scratch-code dependency is not acceptable as it stands (F4).**
  - R3's horizon-relative table and extinction bound, R5's levels, R6's rod edges, R7's veil, and §7.4's sparkle and grain figures all come from 15 scripts in the session scratchpad. That folder will not survive the session. The local `reference/_frames/P/rain/` holds their intermediate scans.
  - §1–§5 are held to "every number from a committed tool", and §7 feeds calibration caps for the video change (R3 ≤ 0.3 km⁻¹, R6 σ ≤ 0.7 px). §7 should meet the same bar.
  - I could reproduce only R4 and R5, approximately, from `metrics.csv`.

### OPSEC

| Category | Result |
|---|---|
| File-name fragments | 0 specific hits across all targets. Numeric hits are 2-digit tokens such as "10" and "00" inside ordinary numbers. The only longer numeric hit is the year inside the answer-recording date (`pilot-answers.md:3`, and `.openspec.yaml`). That full date matches no file name, and `study-flight-references/pilot-answers.md` carries the same kind of date. Not a finding. |
| Places, coordinates, grid references | 0. The Cyrillic in `airframes.md:3` and `:10` is product names, not places. |
| Dates and clock times | Only the recording date above. The other regex hits are CI brackets and fractions. |
| OSD values | No voltage, capacity, link, timer or altitude reading is quoted. The 20–50 m test heights are labelled assumed. **An airframe model is tied to P** (F5; wording redacted in round 2). |
| People and unit | None named. The pilot's answers now also give a sortie profile: model, battery configuration, coil and cargo masses, the standoff distance and flight time (F5). |
| Commit messages | Clean. The only handle hits are the role e-mail addresses. |

### Pending 2.5 (not scored)

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

### Findings (must fix)

| ID | Owner | Where | Problem | Fix |
|---|---|---|---|---|
| F1 | Physics Engineer | `wind.md:146`, `:349`, `:408`, `:467`, `:562` (T9), `:585` | **The heavy tails are the mix of belt and field, not intermittency.**<br>- Residual kurtosis, whole / belt / field: roll 4.74 / 2.28 / 2.51 (field [2.35, 2.76], sub-Gaussian); pitch 3.81 / 2.65 / 3.85; yaw 9.18 / 2.77 / 4.27.<br>- A Gaussian mixture at the measured belt and field stds gives 6.16 / 3.59 / 9.72.<br>- Five synthetic stationary Gaussian logs through `wind_stats` pass T9: roll 3.60–6.77 (mean 5.59), pitch 2.97–3.65 (mean 3.36).<br>- So T9 restates T3 and T4. "Intermittent gusts, not Gaussian noise" is not what P shows over the field. Yaw's kurtosis of 9 does not support "authority running out".<br>- Nothing checks "constant". | Restate §2, §5.4 item 3 and §5.6 item 4: P's burstiness is spatial (the belt wake), and the field wobble is steady and near-Gaussian (roll sub-Gaussian). Drop the kurtosis argument at `:408` and `:585`. Replace T9 with per-regime targets, or remove it. Add a constancy target on the existing `blocks` field: every open-field 5 s block within a band around P's 0.50–0.61° roll and 0.20–0.34° pitch. |
| F2 | Physics Engineer | `wind.md:550` (T0b), `:311–318` | **No target enforces Q2's random direction changes.** T0b measures the variability of the sideways thrust. By `:311` and `:325`, turbulence around a fixed vector produces that too. | Relabel T0b "crosswind variability". Add a T0c on the preset's own wind state over a 10 min history and several seeds: the spread about the prevailing direction, the rate of 45–120° shifts, and the share of 40 s windows that keep the crosswind on one side. Take the starting values from `:315–318`, marked tunable. |
| F3 | Physics Engineer | `wind.md:496`, `:529`, `:570–586` | **§6 is not complete for D-011.**<br>- "Switched on" implies a toggle that won't exist.<br>- The Windy band lists only T0–T5, although T6b, the T6c RMS, T6d and T11a scale too.<br>- The Windy procedure is not stated.<br>- Calm is not mentioned. | Name the presets as in D-011. Give a Windy column for every target, scaled or unchanged, flown with the §6.2 procedure, with T0 = 4.6 ± 1.5° setting the mean crosswind. State that direction changes are always part of Windy and Severe. State that P gives no Calm target, and point to where Calm acceptance will be defined. |
| F4 | Tech Artist | `wind.md:614` | **§7's numbers depend on scratch scripts that won't survive the session.** They can't be re-run by QA, by the video change, or later. | Commit the scan as `tools/reference/rain_stats.py`: headless Blender, letters only, writing only under `reference/_frames/<letter>/rain/`. Give its command in §7.1, as §1 does. If the orchestrator decides against it, record that in `design.md` as an accepted deviation. |
| F5 | Orchestrator (user decision) | `wind.md:362`, `:480`, `:527`; `:299`, `:366`, `:448`, `:484`; `pilot-answers.md:10`, `:14`; `airframes.md:18–23` | **The airframe model and the sortie profile are now public.**<br>- Naming the model ties P to a specific airframe, which the OPSEC rules ban (wording redacted in round 2).<br>- Together with the battery configuration, coil and cargo masses, the standoff distance and the flight time, the notes now describe the pilot's own sortie.<br>- This is already on `main` (`0a1e9df`). | Ask the pilot. If he agrees, record his OK in `pilot-answers.md`. If not, replace the name with "the 10-inch fiber cargo class" and drop the standoff distance in a follow-up. Git history keeps the old text either way. |

### Advisory

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

### Risks and questions

- **Merged before review.** The follow-up PR must carry F1–F5 and task 2.5 together, or `main` keeps a T9 that can't fail and the unscored mass values.
- **Camera field of view (for 2.5).** A published lens field of view for the class's cameras would be the first independent check of the assumed focal length, which sets the pitch and yaw scale, k and 1.10 W. Roll does not depend on it. (Wording redacted in round 2.)
- **Calm (question for the orchestrator):** where will Calm's acceptance be defined? The manifesto's motor asymmetry "yank" (§2.4) should still be felt with no wind.
- **Concurrent work:** someone else wrote into this worktree during the review, adding `airframes.md` before it was committed. Only `review.md` is in this commit.
