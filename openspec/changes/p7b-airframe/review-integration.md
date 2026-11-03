# QA review: the physics integration, flight half

**Verdict: changes requested.** Superseded by "Re-check at 18a594e4" at the end: **approve**.

The physics holds:
- Every golden re-record at models 24, 25 and 26 has a cause I proved by experiment. I reproduced all six of the physics engineer's temporary-edit audits bit for bit with my own switches, and no stored input changed.
- The seam rule, the stick spring on the material's slip and D-066's build give the mechanics' numbers.
- Replay is bit for bit on my own flights, and it catches a one-bit change.
- `dotnet test` passes 395 of 395.

Four findings block the merge. All four are in tests, thresholds and texts, not in the physics code:
- On two of my four fresh seed sets, `flightbench --shake` fails.
- At model 26 the shake has a second cause the spec and D-069 leave out: D-064's motor spreads.
- The seam rule's known-wrong no longer fails any check.
- A tunnel check was loosened to excuse a class of bug.

**Scope:**
- `origin/unity/physics/physics-integration` at `2a27993b` (LogFormat.ModelVersion 26, LogFormat.Version 16), 106 commits over `unity/main`.
- My half:
  - `p6-flight-core` 1.6b's seam rule (contacts on the map) and 1.6c's landing law (D-067)
  - `p7b-airframe` 1.3d, 1.10/1.10b and 1.11 (D-064, D-066, D-068, D-069)
  - the golden re-records at models 24, 25 and 26
  - the ringless-shake criterion `ShakeScenario.PastTheKick` (`525f0a14`)
- Out of scope (the second QA agent's half): wind, world queries, Unity EditMode, `--bench`.

**How I checked:**
- I ran every command below myself in the worktree `.claude/worktrees/qa-pi-flight`. The author's output was not used. The logs are in its `.qa-logs/` (never committed).
- My temporary switches are kept as `.qa-logs/qa-temp-switches.diff`, with the harness `.qa-logs/QaAudit.cs`:
  - `ContactModel.QaOldAnchor`: model 22's stick anchor, copied from `b8b56aec^`
  - `ContactModel.QaNoSeam` and `QaNoSeamSupport`: the seam rule's two halves
  - `Propulsion.QaOldWash`: the wash on model 19's numbers and its torque at the freestream's inflow
  - `FlightCore.QaInterferenceStream`
  - the environment variable `QA_NO_SPREADS` in `ShakeScenario.Setup`
- I reverted all of them before committing, so `git status` showed only this file and the spec line.
- My seeds:
  - the shake: 701–740, 1001–1040 and 1301–1340 (plus the author's 1–40 and 301–340)
  - three recorded flights for replay
- `git merge-tree` against `unity/main` at `5574a141`: clean, no conflicts.

## Findings

### Blocking

**B1. `flightbench --shake` fails on fresh seeds.** Owner: the physics engineer (the claims), the orchestrator (the spec's numbers).

On my seeds 701–740 and 1001–1040 the mode exits 1. It passes on the author's 1–40 and 301–340, and on my 1301–1340. Two claims are written for every seed, or as a strict rise, and the physics does not keep either at model 26:
- **"Its length follows the descent: the medians rise with it":**
  - It fails at 2 kg, where the medians from 2.5 and 5.4 m/s tie at 0.06 / 0.06 s on both failing sets.
  - Both medians are the motors' spin-up kick (D-064's spreads). With the spreads off (`QA_NO_SPREADS`, seeds 1–40) they read 0.00 / 0.06 / 0.36 s and rise.
- **"40 A current protection: every arrest held at the limit (within 0.03 s)":**
  - It reads 239 of 240 on 701–740.
  - In the empty drone's 5.3 m/s cell one ESC stays on its limit 0.05 s past the arrest (limited 0.55–0.63 s, arrested 0.55–0.58 s).
  - The per-motor resistance spread gives each ESC its own limit voltage (`_limitVolts = limit · R·scale`).

Across five 40-seed sets, the mode failed on two. That false-failure rate does not let a fresh run tell a regression from a seed.

**Fix:** follow D-053's rule:
- State "every arrest held" as "nearly every" (≥ 95 %).
- Judge the length's rise past the kick, or as non-decreasing with the 2 kg low cells under the visible line.
- Re-run on at least three fresh 40-seed sets.

**B2. At model 26 the punch-out shake has two causes. The spec, D-069 and the suite's known-wrong name one.** Owner: the physics engineer (the tests' texts and sizing), the orchestrator (D-069, PHY-10's text).

Without the wash, D-064's motor spreads alone make a shake that lasts through a fast arrest. My reading, not traced: at full duty every motor saturates, so the loop cannot trim their spread.

The known-wrong (no ring) in the pilot's own cell, the empty drone's full punch from 9 m/s:

| Seeds | Ringed: in 0.3–0.7 s, median | Ringless: in 0.3–0.7 s, median |
|---|---|---|
| 1–40 | 40/40, 0.62 s | 19/40, 0.16 s |
| 301–340 | 40/40, 0.62 s | 20/40, 0.50 s |
| 701–740 (mine) | 40/40, 0.60 s | 22/40, 0.56 s |
| 1001–1040 (mine) | 38/40, 0.60 s | 24/40, 0.50 s |
| 1301–1340 (mine) | 39/40, 0.62 s | 16/40, 0.12 s |
| 1–40, the spreads off (mine) | 39/40, 0.60 s | **4/40, 0.02 s** |

Consequences:
- **`ShakeTests.TheShakeLastsAboutTheArrest`'s known-wrong half is a coin toss.**
  - Without the ring, the pilot's band on seeds 1–10 reads 6 of 10. The band needs 7.
  - On the pooled 101 of 200 ringless flights inside 0.3–0.7 s (p ≈ 0.505), a fresh 10 seeds reaches 7 about 18 % of the time. The median is then inside too, so the test fails spuriously about once in five model changes.
  - Its comment, "without the re-ingested wash the shake barely shows", is false at model 26.
  - Fix: size it to 40 seeds, or judge the known-wrong by the past-the-kick rate as `--shake` does.
- **D-069's evidence is the power branch's, not model 26's.**
  - It says "With the rings off it shows on 84 of 240 punches against 186".
  - At model 26 it shows on 149 of 240 against 206 (seeds 1–40), 152/210, 173/220, 165/215 and 148/209 on the other sets.
  - D-069 is the orchestrator's to restate or supersede. PHY-10's text in `requirements.md` still gives the ESCs' current limit (D-051, D-053) as the cause.
- **`PastTheKick`'s comment misstates what it removes.** It says the kick shows "on a full punch with or without" the wash, below 0.3 s. The spreads also push ringless shakes past 0.3 s: 13 → 37 of 240 on seeds 1–40, and 4 → 19 in the pilot's cell. Its judgement is below.

**Fix:**
- The spec says "main cause" (done here, the spec line below).
- The orchestrator restates D-069's numbers and PHY-10's cause: the wash first, the motors' spreads a second cause that no current limit trims.
- Re-size the suite's known-wrong as above.

**B3. The seam rule's known-wrong passes every check (1.6b).** Owner: the physics engineer.

The task's record says "without the seam rule the run is 1.3 µm deeper" than the plane. At model 26, with `ContactModel.Seam` returning false, E01's run rests:
- 0.443 µm deeper than the plane (tolerance 1 µm)
- with 1.00009 of the weight (tolerance 1e-3)
- on **16 soil pairs instead of 8**, 0 late events

So `RangeContactTests.OnSoilItRestsAsOnThePlane("run")` passes, as do `WithTheAirframesEffectsSoilIsStillThePlane` and `--contacts`' resting row. They assert `Soil == Pairs.Count` and `Soil >= 8`, never one pair per support.

The second half, `SameSupport`'s coplanar widening, moves nothing on either site when switched off (both sinks equal to the plane's to 1 nm).

**Fix:**
- Assert one pair per support on soil, `Pairs.Count == 8`, as the plane case already does. The rule-off run then fails (16).
- Either give the widening a case that exercises it, or say it has none.

**B4. A tunnel check excuses a class of bug.** Owner: the physics engineer.

Since `34cdd440` (the power branch's model 23), `InTheWindNoTunnelThroughARigidShape` asserts `p.OtherTunnels + p.Lost == 0`. That excuses every tunnel by a part named `leg_`, `skid_`, `support_` or `tjoint_`, through anything, not only a thin rod. A tunnel is always a bug (CLAUDE.md), and the test's name says none.

At model 26 the exception excuses nothing: all nine cases log **0 tunnels** (`.qa-logs/tunnels.log`). For example, the wire at 5.52 m in Severe: `leg_fr` first at 10.57 m/s, a crash by acceleration on `pack_right`, 0 tunnels.

**Fix:** put back `p.Tunnels + p.Lost == 0`. It passes now (verified). A future leg-tube tunnel then fails and is triaged, instead of passing silently.

### Not blocking

- **N1. Stale numbers in the spec and the test docs (model 26, seeds 1–40).**
  - Spec line 91's medians, "0.04, 0.32 and 0.60 s empty … 0.00, 0.06 and 0.36 s at 2 kg", read 0.20 / 0.38 / 0.62 and 0.04 / 0.12 / 0.38 s.
  - Line 83's "often under the visible line, from a gentle 2.5–5 m/s descent": the empty drone's 2.5 m/s full punch shows on 32 of 40.
  - `ShakeScenario.KickSeconds`' doc says 0.08 s without protection. The kick reads 0.16 s (0.20 on 701–740).
  - `Most`'s doc ("39 of 40, the median 0.60 s; from 5.3 m/s 0.32 s, 25 of 40") reads 40/40 at 0.62 s and 32/40 at 0.38 s.
  - 1.11's tip times are the power branch's. At model 26 the exact build tips from 0.1° in 1.134 s, and the placed builds in 0.632–1.394 s (median 0.884 s). That is slower because the coil now rolls instead of pivoting like an axle, consistent with `--landing`'s 1.113 s to 20°.
- **N2. `--scenarios`' ringless row is judged on 20 seeds.** From my five 40-seed sets (ringless 179 / 1200 past the kick, ringed 518 / 1200), a fresh 20 seeds fails "at most half as many" about 5 % of the time; on 40 seeds about 0.8 %. State the rate in the row, or use 40.
- **N3. The two widened E01 tolerances are no longer needed.** At model 26 the still-air cut rests at 0.419 s against the stub's 0.419, and the crosswind's held rise is 111.04 mm against the plane's 111.04. Restore 10 ms and 1 mm (`277f277c` widened them to 15 ms and 2 mm at model 24).
- **N4. Two of `--landing`'s powered columns are not explained** (not mechanics checks; G7 material):
  - Armed with the throttle cut and no descent, the drone tips at **no lateral speed** from 5°, 10° and 15° of roll, but survives up to 0.484 m/s at 20°. That is not monotonic.
  - Held at the hover's thrust on μ 0.5 with 0.5 m/s of descent, it tips at no lateral speed from 10° on.
  - 1.6c's model-26 note covers the level and 0.5 m/s-down rows only.
  - The pilot will meet these on every armed touchdown, so the physics engineer should trace the 20° row and the zeros before G7 asks about airmode.
- **N5. The slow-flight row's label.** `RotorInterferenceScenario.SlowSpeed = 2` labels the row "under 2 % at 2 m/s". The flight settles at 2.9 m/s at 1° nose down; the spec says 3 m/s. Name it by the flown speed.
- **N6. The handover's premise.** The expected `dotnet test` failures (the post-crash tunnels) do not occur. The suite is 395 of 395, and the tunnel cases log none, because the flights moved off them, not because the bug is fixed. A soft leg tube driven into a thin rigid rod at 5–13 m/s still has no law that stops it (p7f's breakage, p7d's soft wires). There is no failing case now to reproduce it.

## `ShakeScenario.PastTheKick`, judged

The question: is it sound physics or a check bent to pass?

**Not bent:**
- The 0.3 s line was set two days earlier (`4e25472e`, `KickSeconds`, the pilot's shortest shake). It already bounds the spec's own kick from the hover, so it was not chosen after the failure.
- The known-wrong fails it with margin on all five 40-seed sets. Ringless against ringed, past 0.3 s: 37/106, 34/107, 44/107, 38/99, 26/99. The worst is 2 × 44 = 88 against 107.
- The "weaker" half holds at 190–206 of 240 against 168 needed.
- It is conservative: a shake's length spans its first to last visible field, so a kick plus a late field counts against the known-wrong.

**But its stated rationale is wrong:**
- It does not isolate the kick. The spreads alone raise the ringless count past 0.3 s from 13 to 37 (seeds 1–40), and from 4 to 19 of 40 in the pilot's cell (B2).
- Per cell, the wash's share past the kick is nil in three of six cells: 2.5 m/s at both payloads and 2 kg from 5.4 m/s, for example 4/4, 1/1, 1/1 on seeds 1–40. The pass rests on the empty drone's 5–9 m/s punches and the 2 kg 9 m/s one.

**So:** sound as a rate check of the wash's share, with a comment to correct.

**The spec line, restated** (`specs/airframe-effects/spec.md`, "The visible shake is the rigid body's", line 95), so that it says what `--shake` and `--scenarios` check (`ringlessWeaker ≥ Most · arrests && 2 · ringlessLasts ≤ fullLasts`):

> without the re-ingested wash most full punches shake less, and the shake lasts past 0.3 s (the pilot's shortest shake, longer than the motors' spin-up kick from the hover) on at most half as many, so the wash is its main cause;

"Main", because the data shows a second cause (B2). `openspec validate p7b-airframe --strict` passes with the edit.

## The golden re-records at models 24, 25 and 26, audited case by case

**No stored input changed.**
- `git diff de923c64 HEAD` (model 22, the integration's base) on `GoldenFlights.cs` changes only:
  - the pins and their notes
  - the toggles (`interference`, `wake`, `motors`, `layout`)
  - `WithModel22Drone` and `WithoutD064`, and the setups' new `RotorWake` and `Interference`
- No scripted stick, seed, step count, start, wind level or map content changed (`RangeContent` stays `0x6114242E06475FB5`).
- `HarnessPilot` only gains a `Hover` mode: with it off, every line is the old one.
- `drone.json` changes only D-066's labelled numbers: the packs 0.88 kg, the electronics 0.24 kg, no-load 1.4 A, ESC limit "none".

**The kept audits pass** (`flightbench --golden`, `GoldenFlightTests`):
- on model 22's drone, model 25's five pins
- without the integration's sources over 1808 B, the power branch's powered, rain and tumble
- without the interference over 1840 B, the micro branch's four
- without either, model 19's
- main's model 15 still-air pair, and the tumble's model 13 and 14

**The temporary-edit audits, reproduced by me** (each MATCH, bit for bit, record and events):

| Audit | My switches | Drone, bytes | Result |
|---|---|---|---|
| Model 25 → 24 | old anchor | model 22's, all | contact `0xA93D2534E875790F`, range `0xAF27BC78A59ECD98`, powered `0x67788361104F58BB` |
| Model 26 → the power branch | old anchor, old wash, no interference, wake or spreads | D-066's, 1808 | contact `0xAF0C71C9193B4326`, range `0xFBAC3E4D67B45ACD` |
| Model 25 → the landing branch | old wash, no interference, wake or spreads (new spring) | model 22's, 1808 | contact `0x5B8655B7F91E4D75`, range `0x7BD729C0B5F2DFD4` |
| Model 24 → model 22 | old anchor, old wash, all off | model 22's, 1808 | contact `0x8C1E6B066F143A67`, range `0x80C73DBA130C6903` |
| Model 24 → the rotor-wake branch | old anchor, old wash, stream `wake.rotor`, no wake or spreads | model 22's, 1872 | powered `0x2C064B70711ED0ED`, rain `0x113E39E1C2C3C447`, contact `0x67D93616B88664A4`, range `0x818AFC9F9B03EFA3` |
| Model 24 → the micro branch (contact) | old anchor, no interference | model 22's, 1840 | `0xFF836F92A12B4AB0` |

**Where each move starts** (first differing record):
- contact and powered, model 26 against model 25: step 1, the lighter body
- contact against the power branch: step 3, the same step as 1.6c's spring alone on the power branch (the skids standing from step 1)
- powered against the power branch: step 21, the first armed step

These are the notes' claims.

**Every moved case keeps its course:**
- **Contact:** 5 impacts, at rest 0.2519 m on 8 pairs, no crash.
- **Range:** 4 impacts, 0.2323 m up on 21 pairs, no crash.
- **Powered:** armed, no crash, packs at 0.8941.
- **Rain:** film 9.92 g.
- **Tumble:** momentum 1.7e-12, energy 3.5e-12.

**Verdict on the re-records:** each move has a cause proven by experiment. Every pin is the parent's pin with that cause switched off, and nothing else moves.

## 1.6b's seam rule (contacts on the map)

**The law** (`ContactModel.Seam`, `SameSupport`):
- A sphere's contact whose point lies on the plane of a deeper contact on another shape (within 1 µm), or of an equally deep earlier one (within 1 nm), is that face's.
- A support's line contact on a face coplanar with its support is that support's.

I checked the geometry by hand:
- A real step, curb or rod puts the shallower contact's point off the deeper plane by about the sphere's radius, so it still counts.
- Only a flush neighbour's edge lies on the plane.
- Supports skip the plane's contact (`groundless`), and capsules keep the query's line contact.

The rule is sound physics.

**`--contacts` (39 rows), PASS** at model 26:
- S04's soil and E01's run rest as the plane (1.16 mm, 1.000 of the weight, 8 soil pairs, 0 late events).
- Their landings are the plane's (touch 0.497 m/s, held 209.5 mm, cut 2.8 mm).
- The straw sinks 18.29 mm on 18 pairs.
- The stand's launch: 1.944 s into the ramp at throttle 0.486.
- The approaches crash with 0 tunnels.

**Its guard** is B3: with the rule off, nothing fails.

## 1.6c's landing law (D-067), on D-066's build

**The law** (`ContactModel.Friction`):
- The stick spring stretches by the material's tangential slip `v_t · dt` at the contact point, projected onto the current plane each step.
- When sliding it is reset to `−f_t / k_t`, the old anchor's `p.Point + f_t / k_t` in stretch form.
- A rolling part slips nothing, so the coil rolls instead of pivoting on its axis.
- The old anchor is the same code with `Anchor` (`b8b56aec^`). My copy of it reproduces model 24's pins, which confirms the change is exactly that.

**`--landing`, PASS on D-066's build** (4.742 kg legs on, 4.626 kg legs off):
- **Tips:** roll 23.35° and pitch 42.75°.
  - By hand: atan(102.5 / 237.46) = 23.35°, and atan(219.5 / 237.46) = 42.75°.
  - μ_k 0.905 = 219.5 / 242.46.
- **Falls:** 0.798 / 0.518 / 0.396 s against the mechanics' 0.806 / 0.527 / 0.406.
- **The caught skid on dead legs:** within 0.4 % at 5–20° with 0.5 m/s of descent (0.737/0.737, 0.525/0.525, 0.324/0.323, 0.128/0.129).
- **The coil's rock:**
  - 1.113, 0.814, 0.685, 0.556 and 0.383 s against rolling's 1.115, 0.815, 0.686, 0.557 and 0.384 s
  - an axle 0.898–0.310 s
  - a frictionless coil 0.505 s from 1° (fails, as it must)
  - ζ 2 0.685 s (unchanged)

**The coil, armed and cut:** at 14.9° after 6 s. Disarmed, it reaches 20° in 0.649 s. The powered columns are N4.

## p7b-airframe 1.3d, 1.10, 1.10b, 1.11

- **1.3d (rotor interference), `--scenarios` and `RotorInterferenceTests` PASS:**
  - Coleman's tan(χ/2) within 1.48e-4.
  - −3.39e-6 v_i outside an unskewed wake.
  - The rear pair is 5.5 % under the front at 10.4 m/s and 6.5 % at 15 m/s; the known-wrong reads 0.0 % and fails.
  - A hover takes 3.89e-5 m/s.
  - Slow flight (2.9 m/s, 1°): 1.8 %.
- **1.10 (D-064), PASS:**
  - `TheOwnWakeIsThePublishedThrustUnsteadiness`: σ 2.76 % against 2.9 %, autocorrelation 0.377 at τ against 0.368.
  - `NearTheGroundTheHoverIsNeverStill`:
    - at 1.25 m, roll and pitch 3.32 / 3.28 °/s and 0.0757 / 0.0669° above 4 Hz
    - the steady rotor 0.00 / 0.01 °/s
    - at 0.3 m, the 1–4 Hz roll and pitch 0.162 / 0.298° against 0.084 / 0.161° (×1.8–1.9)
  - D-068 accepts the 4–15 Hz excess.
  - Every new draw is named, bounded and logged: `motor.resistance`, `motor.friction`, `motor.inertia`, `rotor.wake` and `rotor.interference` are in the header's 16 streams (`replay --summary`), and the per-step processes are in the record.
- **1.10b (D-066), PASS:**
  - `TheHoverStickByLoad`, on the pilot's 50/25 curve and a full pack: 36.8 % empty, 44.9 % with 1 kg, 52.8 % with 2 kg. The pilot's 45 % holds to a 1.02 kg bottle; nothing is tuned to it.
  - The mass build: 4.087 / 5.087 / 6.087 kg.
  - The cruise settles at 20.36 m/s at 22.70° (PHY-17a's 17–22).
  - The shake is B1 and B2.
- **1.11 (placement), PASS:**
  - `ThePlacementIsDrawnBoundedAndLogged`: the centre of mass across reads RMS 0.37 / 0.64 / 0.97 mm and at most 1.03 / 1.70 / 2.15 mm, inside the bounds' 1.21 / 1.96 / 2.46 mm.
  - `OffCentreTheCoilTipsAtOnce`: the exact build never tips from level in 3 s; the placed builds tip on 20 of 20, 18 of them sooner than the exact build from 0.1°.
  - `AFlightCarriesItsPlacementAndReplaysBitForBit`.
  - `replay --summary` lists the 9 `build.placement` draws first, each inside its bound.
  - It is never on the pose: `BuildTolerance.Apply` moves the mass model's parts only.

## Spec scenarios

**`p6-flight-core`, my half (the test range, 1.6b and 1.6c):**

| Scenario | Result | Evidence |
|---|---|---|
| Hold the sticks and it leaves the ground (the test range) | pass | `--contacts`: plane, S04's soil, E01's run: held 209.5 mm up, cut 2.8 mm and at rest 0.239 s; straw held 156.2 mm, cut never off |
| Liftoff from the stand (the field stand) | pass | off the four posts 1.944 s into the ramp, 8 steel contacts made and broken, the stand static |
| Into a pole (the test range) | pass | `prop_fr` on `L01_straight` (timber) at 7.72 m/s, 485 g, 82.14 N·s, 0 tunnels |
| Every draw is named and bounded (the new draws) | pass | `replay --summary`: 16 streams, 39 draws, each with its bound; `TheDrawsAreNamedBoundedLoggedAndReplayed` |
| Replay matches the flight | pass | three of my flights bit for bit; a one-bit change is caught at step 59 993 |

**`p7b-airframe` (`airframe-effects`), the parts this half touches:**

| Scenario | Result | Evidence |
|---|---|---|
| Asymmetry on and off | pass | `--scenarios` seeds 1–5; `AsymmetryOnAndOff`, `TheYankIsAsPredictedOnThirtySeeds` |
| Off centre, the coil tips at once | pass | 20 of 20 tip, 18 sooner than 1.134 s, the bounds held, replayed |
| The close hover is never still (D-064's wash) | pass | 1.36e-2–2.49e-2 rad/s per block against the 1e-3 floor; the known-wrong fails |
| The own wake is the published unsteadiness | pass | σ 2.76 % (2.9 %), autocorrelation 0.377 (0.368) |
| Near the ground the hover is never still | pass | above |
| The wake model against its published form | pass | 1.48e-4; −3.39e-6 v_i |
| The rear rotors lose thrust in forward flight | pass | 5.5 % and 6.5 %; the known-wrong 0.0 % fails |
| Hover and slow flight keep their thrust | pass | 3.89e-5 m/s; 1.8 % (N5's label) |
| Heavy on low throttle | pass | `--scenarios` seeds 101–120: 20, 20, 20 of 20 |
| Shake only when its causes meet | pass | every claim on all five 40-seed sets; the kick from the hover at most 0.16 s (0.20 on 701–740) |
| The shake lasts as long as the arrest | **fail** | the medians' rise fails on 701–740 and 1001–1040 (B1); line 91's medians are stale (N1) |
| The visible shake is the rigid body's | **fail** | the 40 A "every arrest" fails on 701–740 (B1); "the wash is its cause" overstates it (B2); the ringless rate and the rigid airframe pass on all five sets |
| The heavy corner at low charge | pass | `BatteryTests` (in the suite's 395) |
| The inertia character on the seven loadouts | pass | `MassBuildTests` and PHY-12's checks (in the suite's 395) |

## Requirements

| ID | Status | Why |
|---|---|---|
| PHY-01 | partly met | The stick-point anchor now reads the material's slip (1.6c), and the soil faces are dispatched by surface. The soil, mat and ploughing laws are `p7a-ground`'s; the ground is G7's rigid stand-in |
| PHY-02 | met | The bounce comes from the legs and the residual thrust, on the stub and the range; nothing is scripted |
| PHY-06 | met | The field stand's launch, on steel contacts, with the stand static |
| PHY-07 | met | Kv, lag, imbalance and D-064's resistance, friction and inertia: drawn, bounded, logged; the band holds; it never buzzes |
| PHY-08 | met | Cheeseman–Bennett, the ceiling, and the re-ingested wash on published numbers (Dekker et al., Healy et al.) |
| PHY-09 | met | VRS on seeds 101–120 |
| PHY-10 | partly met | The shake arises, varies and lasts the arrest. Its claims fail on fresh seeds (B1), and the cause is misstated (B2) |
| PHY-11 | met | `BatteryTests` on D-066's ESC |
| PHY-12 | met | D-066's build re-derived (`MassBuildTests`) |
| PHY-17a | met | 20.36 m/s at 22.7° |
| PHY-24 | met | `build.placement` and D-064's streams: named, bounded, logged, never on the pose; replayed bit for bit |
| LOG-02 | met | Bit for bit on my three flights; a divergence names the step and the field (`wind.y` at step 59 993) |
| PHB-05 | met | The skid's spheres and capsule on soil and seams; the probe's breaks each fail |

## Commands and output (trimmed)

```
$ git worktree add .claude/worktrees/qa-pi-flight -b unity/qa/physics-integration--flight origin/unity/physics/physics-integration
HEAD is now at 2a27993b docs(openspec): point 1.6c's merge record at p7b-airframe 1.10b
$ git lfs checkout; git rev-list --count origin/unity/main..HEAD
106
$ git merge-tree --write-tree --name-only origin/unity/main HEAD
75e5999b3af46cdc3e0c4eee65843efc81301df7          (no conflicts)

$ dotnet test tools/simtests
Passed!  - Failed:     0, Passed:   395, Skipped:     0, Total:   395, Duration: 14 m 52 s - Fpv450.Sim.Tests.dll (net10.0)

$ dotnet run -c Release --project tools/flightbench -- --golden
powered-flight: 10000 steps, hash 0x53F1D10489702031 (matches) ... PASS
contact-flight: 12000 steps, hash 0xAAEA31DBFBB13710 (matches), 5 impacts, ends 0.2519 m up on 8 touching pairs: PASS
range-flight: the test range 0x6114242E06475FB5 (as recorded), 12000 steps on S04's straw, hash 0xC79B60F487AEB8B6 (matches), 4 impacts, ends 0.2323 m up on 21 touching pairs: PASS
rain-flight: ... hash 0x53B90D4307BF1534 (matches), 18.04 mm/h, film 9.92 of 42.45 g: PASS
tumble: ... hash 0xBCBD33E6B6A94844 (matches) ...: PASS
model 22's drone ...: (model 25's, bit for bit): PASS
without the interference and D-064's sources, over 1808 bytes: ... (the power branch's, bit for bit): PASS
exit=0
$ ... --contacts          contacts: PASS (exit 0)
$ ... --probe             contact-probe: PASS (exit 0)
$ ... --probe --break latch|margin|refresh|spheres|disc
                          FAIL (4 checks) | FAIL (1) | FAIL (1) | FAIL (2) | FAIL (2), each exit 1, as they must
$ ... --landing           every row PASS (exit 0)
$ ... --scenarios         scenarios: PASS (exit 0); the ringless row: "weaker 103/120; past the kick (0.3 s) on 16/120 against 48: fails, as it must"
$ ... --shake             shake: PASS (seeds 1-40)
$ ... --shake 301         PASS
$ ... --shake 701         its length follows the descent: ... 2 kg 0.06 / 0.06 / 0.36 s: FAIL
                          40 A current protection ...: held 239/240 ...: FAIL
                          shake: FAIL, exit=1
$ ... --shake 1001        its length follows the descent: ... 2 kg 0.06 / 0.06 / 0.34 s: FAIL; exit=1
$ ... --shake 1301        PASS
$ QA_NO_SPREADS=1 ... --shake 1     (temporary switch)
                          the kick at most 0.02 s; medians empty 0.08 / 0.32 / 0.60 s, 2 kg 0.00 / 0.06 / 0.36 s: PASS
                          known-wrong, no ring: weaker 211/240; past the kick (0.3 s) on 13/240 against 93; shown at all on 86/240 against 186

$ dotnet test (QA's harness, the switches above) -- NUnit.Where=class == ...QaAudit
E1 contact 0xA93D2534E875790F MATCH; range 0xAF27BC78A59ECD98 MATCH; powered 0x67788361104F58BB MATCH
E2 contact 0xAF0C71C9193B4326 MATCH; range 0xFBAC3E4D67B45ACD MATCH
E3 contact 0x5B8655B7F91E4D75 MATCH; range 0x7BD729C0B5F2DFD4 MATCH
E4 contact 0x8C1E6B066F143A67 MATCH; range 0x80C73DBA130C6903 MATCH
E5 powered 0x2C064B70711ED0ED, rain 0x113E39E1C2C3C447, contact 0x67D93616B88664A4, range 0x818AFC9F9B03EFA3: MATCH
E6 contact 0xFF836F92A12B4AB0 MATCH
E7 first differing record: contact 26/25 index 0; powered 26/25 index 0; contact 26/power 2; spring alone 2; powered 26/power 20
E9 Seam() off, run: sink 1.161836 mm against the plane's 1.161394 (diff 0.443 um), load 1.00009, 16 pairs (16 soil), late events 0
   SameSupport's coplanar widening off, run: diff 0.000 um, 8 pairs; soil: diff 0.000 um, 8 pairs
   (E8: RangeContactTests.OnSoilItRestsAsOnThePlane("run") with Seam() off: passes)

$ dotnet test tools/simtests -- NUnit.Where=method == InTheWindNoTunnelThroughARigidShape || ...
crown at 10 m in Calm|Windy|Severe: ... 0 tunnels (), 0 lost
wire at 5.47|5.475|5.51|5.52 m in Severe, 5.475 in Windy|Still: ... 0 tunnels (), 0 lost
$ dotnet test tools/simtests -- NUnit.Where=method == WithTheAirframesEffectsSoilIsStillThePlane || method == InASteadyCrosswindSoilIsStillThePlane
cut: ... at rest 0.419 s (stub 0.419), 28 contact events (stub 26)          (E01's run, still air)
held: touch 0.4725 m/s (plane 0.4725), rose 111.04 mm (plane 111.04)        (E01's run, crosswind)

$ replay --record .qa-logs/qa-floor-windy.flog --seconds 120 --wind windy
  120000 steps, 178 events, 3 scheduler hitches, 0 log waits; landed at 0.448 m/s, 5 impacts, crashed False
$ replay .qa-logs/qa-floor-windy.flog
  match: 120000 steps and 175 events, bit for bit; exit 0 (Match)
$ replay --record .qa-logs/qa-range-severe.flog --map test-range --wind severe ; replay ...
  120000 steps, 206 events; landed at 0.441 m/s; match: 120000 steps and 203 events, bit for bit
$ replay --record .qa-logs/qa-floor-calm-rain.flog --seconds 60 --wind calm --rain severerain --realtime ; replay ...
  real time on the flight thread, 60014 steps, 10 scheduler hitches; match: 60014 steps and 54 events, bit for bit
$ replay qa-tampered.flog          (one bit flipped at byte 114504039)
  diverged at step 59993 (t = 59.993 s), wind.y: logged 0.8077070420291013, replayed 0.8077075188662595; exit 1 (Diverged)
$ replay --summary .qa-logs/qa-floor-windy.flog
  format 16, model 26; randomness: 16 streams (build.placement, ..., rotor.wake, rotor.interference), 39 per-flight draws
  build.placement / build.bottle_across_m = 0.00449942 in [-0.005, 0.005] (draw 0) ... wiring_across_m = 0.0140331 in [-0.02, 0.02] (draw 8)

$ python tools/hygiene/precommit.py --tree HEAD
2284 files scanned, 0 findings
$ openspec validate p7b-airframe --strict ; openspec validate p6-flight-core --strict
Change 'p7b-airframe' is valid
Change 'p6-flight-core' is valid
```

## Hygiene

- **The 106 commits:**
  - 102 by `Physics Engineer <physics@450fpv.local>` and 4 by `Reference Analyst <reference@450fpv.local>`, author and committer alike
  - every message one subject line, none over 72 characters
  - no body on any commit or merge, and no co-author or Claude line
- **Paths:** nothing under `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`, and no footage. `tools/reference/` is the analysts' tool source, which `.gitignore` allows.
- **OPSEC:** the tree scan finds nothing (2284 files, 0 findings).
- **This review's commit:** this file and the spec line, staged by path, under the QA identity. My harness, logs and switches stay untracked in `.qa-logs/`.

## Re-check at 18a594e4

**Verdict: approve.** The four blocking findings are fixed as asked. The fix agent's three further restatements hold under D-053's rule. Nothing below blocks the landing on `unity/main`. Four new findings (N7–N10) are not blocking; they go to the next pass.

**Scope:**
- `origin/unity/physics/physics-integration` at `18a594e4`, still `LogFormat.ModelVersion` 26. 18 commits over `2a27993b`:
  - the two reviews (`bbdaa8d4`, `f42f0421`, `c718d7fd`, the wind one merged as `b5596083`)
  - `--qa-fixes`' commits (`58453f55..0f701d73`)
  - N4's trace (`de0eb78f`), merged as `18a594e4`, which brings exactly its lines
- The runtime's only change is two comment lines in `ContactModel.cs` (no other line). No golden source changed.

**How I checked:**
- I ran everything below myself, in `.claude/worktrees/qa-pi-recheck`. Logs are in its `unity/Logs/qa-recheck/` (git-ignored, never committed).
- Unity EditMode ran in `.claude/worktrees/phys-int2` at `18a594e4`, read-only, after `git lfs checkout`.
- My temporary switches and one scratch test ran in a separate scratch worktree at `18a594e4`, since removed. They are kept beside the logs as `qa-temp-switches.diff` and `QaEndTail.cs.txt`.
- **Fresh seeds, none flown before:**
  - `--shake` on 7301–7340 and 9101–9140, then 25 more sets, 10001–10040 to 12401–12440: 27 sets, 1080 seeds
  - the shake's end after the arrest on 20001–24000, every cell
- The host was shared: other agents' `dotnet test` runs overlapped mine. None of these checks is timed.

### The blocking findings

**B1 (`--shake` fails on fresh seeds): fixed.**
- **The rise:** "its length follows the descent" is judged past the kick (`MedianPastTheKick`). Each payload's median never falls, and is longer from 9 m/s than from 2.5 m/s.
- **The hold:** "every arrest held" is now "nearly every" (at least 95 %).
- **All 27 of my fresh sets pass every claim** (`campaign-analysis.txt`). On the same sets the old claims fail: the strict rise of the whole lengths on 3 of 27, "every arrest held" on 2 of 27.
- **The rise's margins**, the medians past the kick:
  - empty: 0.00 / 0.32–0.40 / 0.58–0.64 s
  - 2 kg: 0.00 / 0.00 / 0.30–0.40 s
  - The 2 kg median from 9 m/s needs 20 of 40 past the kick, and gets 25–34.
- **The hold:** 3 misses in 6480 arrests. The worst set reads 238 of 240 (228 needed).
- **The ringless known-wrong fails the new rise.** At 2 kg it never lasts past the kick, so its three medians tie at none (seeds 9101–9140: past the kick on 3, 0 and 0 of 40).

**B2 (two causes): fixed.**
- **The test:** `TheShakeLastsAboutTheArrest` judges the known-wrong as `--shake` does, on seeds 1–40 of the six descents. It reads weaker 206/240, and past the kick on 37/240 against 106. The reading is the same on .NET, on .NET held to 3 processors and on Mono (below).
- **Its false-failure rate, from my 27 sets:**
  - weaker on 183–215 of 240 (168 needed)
  - the margin, ringed − 2 × ringless past the kick, 4–48, mean 28.8, sd 10.9
  - so about 0.4 % (the normal tail, continuity-corrected), against the author's bootstrapped 0.57 %. Both are under 1 %.
- **The comments:** `PastTheKick`, `ShakeTests` and `ShakeCase` name D-064's spreads as the second cause.
- **The orchestrator's half is on `unity/main`:** D-070 restates D-069's numbers at model 26, and PHY-10 names both causes.

**B3 (the seam rule's known-wrong): fixed.** Each soil rest now asserts 8 pairs.
- **With the rule off** (my switch `QA_NO_SEAM`: `Seam` returns false):
  - three cases fail on 16 pairs: `OnSoilItRestsAsOnThePlane("run")`, `WithTheAirframesEffectsSoilIsStillThePlane("run")` and `InASteadyCrosswindSoilIsStillThePlane("run")`
  - so does `--contacts`' "E01's run, resting, each support once" row
  - every S04 case passes either way, with one patch shape under every support
- **With the rule on,** all seven cases and `--contacts` pass.
- The widening's new case is judged below.

**B4 (the tunnel exception): fixed.**
- `InTheWindNoTunnelThroughARigidShape` asserts `p.Tunnels + p.Lost == 0` again.
- `OtherTunnels` is gone, with no reference left.
- All nine cases log 0 tunnels and 0 lost, on .NET and on Mono.

### The three further restatements, judged by D-053's rule

Each failed once on one of the author's fresh sets and became "nearly every" (at least 95 %). I judged them on my 27 other fresh sets:

| Claim | The miss that moved it | Misses on my sets | Sets the old "every" fails | Sets the new claim fails | Its known-wrong |
|---|---|---|---|---|---|
| The gentle ramp's wobble shows, from 5–9 m/s | 0.085° against the visible 0.088° (2 kg, 5.4 m/s) | 0 of 4320 ramps | 0 of 27 | 0 of 27 | The ring over 1 s stays every flight's claim (1.33 s at least); ringless rotors have none |
| A full punch from 9 m/s shows the shake | 0.0880° (2 kg) | 2 of 2160 | 2 of 27 | 0 of 27 | Ringless rotors show it on 43–63 of 80 (76 needed) |
| With 40 A protection the drone tilts further | 1.71° against 1.93° (empty, 9 m/s) | 7 of 6480; the ratio from 0.79 | 7 of 27 | 0 of 27 | A limit that changed nothing tilts further on none (the comparison is strict); the per-set median ratio is 3.97–5.04 |

**Judgement:**
- **Each is still physics.** Each miss is a continuous reading at a line, not a broken mechanism:
  - Two are a visibility line crossed by a hair (0.085° and 0.0880° against 0.088°), in the 2 kg cells, where the shake is weakest.
  - The third compares two variants seed by seed. With D-064's spreads, the drone's own tilt can beat the protected one's on a rare flight. The protected drone still tilts 4–5 times as far at the median.
- **Each false-failure rate is under 1 %:**
  - Binomial on my per-flight rates, a fresh 40 seeds fails the 9 m/s claim about 1.5e-8 of the time, and the hold and the tilt far less (under 1e-10).
  - The ramp has 1 miss in about 8800 ramps (the author's and mine). Its false-failure rate is also under 1e-10.
- **The old "every" claims failed at over 1 %,** on the same rates: 7 % (the 9 m/s showing), 23 % (the tilt), 10 % (the hold), and about 2 % (the ramp, from its single miss). So D-053's rule required each restatement. None hides a fault.
- **Each is still a check.** Each fails a known-wrong, as the table shows. Against misses of about 0.1 %, the 95 % line is loose. But it is the spec's "nearly every". The printed counts and the tilt ratio's median are what show a regression.
- **The suite** (`ShakeOnlyWhenItsCausesMeet`) now asserts per flight only what the spec says of every flight: the ramp's ring, and the protected arrest longer and ending with its shake. That is its comment's own rule; the rates are `--shake`'s.

### The spec lines 83, 87, 91 and 95

- **87 and 95** say what the code checks:
  - "nearly every" for the 9 m/s punch's showing, the ramp's wobble, the 40 A hold and its tilt
  - "every" for the ring and the longer arrest
  - "past 0.3 s (the pilot's shortest shake, longer than the motors' spin-up kick from the hover)", and "main cause"
- **91** says what `--shake` checks. Its whole-length medians are the seeds 1–40 readings of my first review (N1).
- **83** fixes N1's stale clause.
- **Wording nits in 83 and 91** are N9 below.
- `openspec validate --all --strict`: 24 passed, 0 failed.

### `Parallel.For` in `ShakeTests.TheShakeLastsAboutTheArrest`

**It is deterministic.**
- Each of the 480 flights builds its own `FlightSetup`, world (`FlightWorld.Floor()` makes a new one on each call) and `FlightCore`. Each writes only its own slot of `runs[ringless, cell, seed]`.
- The counts are taken afterwards, serially, in index order. `TestContext` is not touched inside the loop.
- The sim core's static state is read-only:
  - constant tables, set in static initialisers
  - `RotorInterferenceSpec.Table`'s cache, built under a lock
  - `DroneSpec`'s builders return new objects
- `flightbench --shake` has flown its flights the same way since it was written.
- **Shown by runs.** Three runs print the same two lines:
  - .NET on every processor
  - .NET with `DOTNET_PROCESSOR_COUNT=3`
  - Mono in EditMode
  - The lines: "10 of 10 in 0.3–0.7 s, 0.54–0.68 s, median 0.62 s"; "weaker 206/240; past the kick (0.3 s) on 37/240 against 106".

**It is allocation-safe.**
- The loop allocates only in the test (closures, results, each flight's core), never in a step.
- The suite's zero-allocation checks count `GC.GetAllocatedBytesForCurrentThread` on their own thread. NUnit runs the tests one at a time (no `Parallelizable` anywhere), so idle pool threads cannot reach them.
- Both checks pass on .NET and on Mono: `StepAndSampleAllocateNothing` and `TheSameSeedGivesTheSameWindWithoutAllocating`.

**On Mono** the test passes in 10.6 s (14.2 s on .NET, which shared the CPU).

### The new boards case and `ContactScenarios.Site.Objects`

- **`Site.Objects` changes no other site.** It is null by default, and `Effects` sets `RuntimeObjects` only when it is set, so the goldens and `--contacts` read as before. The stand's sites set their stand after `Effects`, so they keep it.
- **The case is sound.** Its geometry matches its comment:
  - three timber boards, 0.05 m thick and flush, the joints at ±0.05 m
  - the supports at ±0.1025 and ±0.22 m, all on the outer boards
  - The drone starts 1 mm over them, so every pair is made in flight and counted.
- **With the widening on:** 8 pairs on one board and on three, 2.161 mm both, 1.0000 of the weight, 0 late events.
- **With the widening off** (my switch `QA_NO_WIDEN`, which drops `SameSupport`'s `OnPlane` clause): the case fails on 12 pairs and 0.9997 of the weight, as the author recorded.

**N7 (new, not blocking): the widening also acts in E01's run, in its landings.** With it off, three more checks fail:
- `WithTheAirframesEffectsSoilIsStillThePlane("run")`: the cut comes to rest at 0.408 s against the stub's 0.419 s, 11 ms off, outside the restored 10 ms
- `InASteadyCrosswindSoilIsStillThePlane("run")`: held, it rises 107.79 mm against the plane's 111.04 mm
- `--contacts`' "E01's run, throttle cut, as the stub's" row

The rests do not move, and that is all B3 and the record measured. So two texts hold only at rest:
- the new comment on `SameSupport`: "the range's soil and run have no such piece under the skids"
- 1.6b's note: "moved nothing there"

**Fix:** say that the run's landings meet such a piece too. The widening now has four guards, and the restored 10 ms is one of them: at 15 ms the cut would pass.

### The remaining "every" claims and the mode as a whole

**N8 (not blocking): the shake's end after the arrest has a thinner margin than the record says.**
- **The record:** the shake ends "at most 0.27 s after the arrest (0.31)", on the author's 520 seeds and every set flown since.
- **My 27 sets:** the latest end is 0.30 s on 3 sets, and 0.28–0.29 s on 2.
- **My 4000 seeds a cell** (20001–24000, `QaEndTail`):
  - Every cell ends by 0.26 s, except the empty drone from 9 m/s.
  - There it reaches 0.301 s: 0.27 s or more on 11 flights, 0.29 s or more on 5, and none over 0.31 s.
- **No flight broke it,** in 5080 flights of that cell. The tail stops at 0.30 s instead of thinning past it. So it is still an "every" claim by D-053's rule.
- **But the margin is 9 ms, not 40.** Fix: record it. If a model change moves this tail, this is the next claim to judge.

**N10 (not blocking): the rigid airframe's "as long" is the tightest rate in `--shake`.**
- **The reading:** "within three fields on nearly every seed" reads 38–40 of 40 on my sets (38 needed), 11 misses in 1080.
- **Its rate:** a fresh set fails it about 0.8 % of the time (binomial). With 11 misses, the rate itself is uncertain, about 0.1–3.5 %.
- **The mode as a whole:** with the ringless row's 0.4 %, `--shake` fails spuriously about 1.2 % of the time. Every other claim is under 0.01 %.
- **Not part of this fix,** and each claim alone is under 1 %. Fix: size it (80 seeds, or the cells pooled) when the shake is next touched.

**N9 (not blocking): two wordings in the spec.**
- **Line 91:** "a shake shorter than the kick (0.3 s)". 0.3 s is the pilot's shortest shake, which bounds the kick (the kick is at most 0.20 s on my sets); line 95 says it right. And at 2 kg the two slower descents show "only the kick" or stay under the visible line.
- **Line 83:** "or at 2 kg" reads as every 2 kg descent. But the 2 kg punch from 9 m/s lasts past the kick on 25–34 of 40. It means 2 kg from 2.5–5.4 m/s.

### The earlier non-blocking findings

- **N1, done.** Lines 83 and 91, `KickSeconds`' and `Most`'s docs, and 1.11's tip times now carry model 26's numbers. The kick on my sets is at most 0.14–0.20 s, as `KickSeconds` now says.
- **N2, done.** `--scenarios`' ringless row is on seeds 1–40: "weaker 206/240; past the kick (0.3 s) on 37/240 against 106: fails, as it must".
- **N3, done.** This is the wind review's B1; see its re-check.
- **N4, traced.** The note is under `p6-flight-core` 1.6c. Its two failing tests carry `[Ignore]` with their reasons, and are skipped on both runtimes. The harness fix is a later change, not judged here.
- **N5, done.** The row reads "slow flight takes little: under 2 % at 2.9 m/s", 1.8 %.
- **N6:** unchanged, no action.

### Spec scenarios and requirements, the rows that changed

| Scenario | Result | Evidence |
|---|---|---|
| Shake only when its causes meet | pass | 27 of 27 fresh sets; the kick at most 0.20 s, the band 0.53 m/s² at least, the ramp's ring 1.33 s at least |
| The shake lasts as long as the arrest | **pass** (was fail) | the rise past the kick on 27 of 27; the pilot's band 37–40 of 40, medians 0.58–0.64 s; the end at most 0.30 s (N8) |
| The visible shake is the rigid body's | **pass** (was fail) | 40 A held on 238–240 and tilting further on 239–240 of 240; ringless weaker on 183–215, past the kick on 27–51 against 94–111; the rigid airframe 0.96–1.11 of the peak, as long on 38–40 of 40 (N10) |

| ID | Status | Why |
|---|---|---|
| PHY-10 | **met** (was partly met) | Every claim holds on 27 fresh sets, and the cause is stated as two (D-070). N8 and N10 are margins to keep an eye on |

The other rows are unchanged.

### Commands and output (trimmed)

```
$ git worktree add .claude/worktrees/qa-pi-recheck -b unity/qa/physics-integration--recheck origin/unity/physics/physics-integration
HEAD is now at 18a594e4 test(physics): take n4's trace of the landing harness
$ git diff 2a27993b HEAD -- unity/Packages/com.fpv450.sim/Runtime     (lines that are not comments: 0)
$ diff <(git diff 2a27993b de0eb78f) <(git diff 0f701d73 18a594e4)    (changed lines only: identical)

$ dotnet test tools/simtests
Total tests: 398, Passed: 396, Skipped: 2 (N4's two [Ignore]); Total time: 22.3 Minutes; exit 0
  ShakeTests.TheShakeLastsAboutTheArrest: from 9 m/s: 10 of 10 in 0.3–0.7 s, 0.54–0.68 s, median 0.62 s
                                          no ring, seeds 1–40 of the six descents: weaker 206/240; past the kick (0.3 s) on 37/240 against 106
  RangeContactTests.OnFlushBoardsEachSupportIsOnePoint: sank 2.161 mm, 1.0000 of the weight, 8 pairs (8 material), one board and three
  RangeContactTests.InTheWindNoTunnelThroughARigidShape: nine cases, each "0 tunnels (), 0 lost"
$ DOTNET_PROCESSOR_COUNT=3 dotnet test tools/simtests --filter ShakeTests.TheShakeLastsAboutTheArrest
  the same two lines; Passed

$ dotnet run -c Release --project tools/flightbench -- --shake 7301
  nearly every full punch from 9 m/s shows it: 80/80; from 2.5-9 m/s 210/240: PASS
  every gentle ramp from 5-9 m/s keeps its rotors in their ring over 1 s, and nearly every one wobbles (shown): 160/160; the ring 1.35 s at least: PASS
  every full punch is arrested and its shake ends within the loop's settling after the arrest: 240/240; the end at most +0.24 s after the arrest (Settle 0.31 s): PASS
  its length follows the descent: ... past the kick empty 0.00 / 0.36 / 0.58 s, 2 kg 0.00 / 0.00 / 0.34 s (whole: empty 0.20 / 0.36 / 0.58 s, 2 kg 0.04 / 0.10 / 0.34 s): PASS
  known-wrong, no ring ...: weaker 210/240; past the kick (0.3 s) on 37/240 against 105; shown at all on 153/240 against 210: PASS
  40 A current protection ...: held 240/240, longer 240/240, further 240/240; its tilt over the drone's at least 1.73 (median 4.70): PASS
shake: PASS (exit 0)
$ ... --shake 9101
  past the kick empty 0.00 / 0.36 / 0.62 s, 2 kg 0.00 / 0.00 / 0.40 s; ringless weaker 190/240, past the kick on 47/240 against 111;
  40 A held 240/240, further 240/240, the ratio at least 1.14 (median 3.98); the end at most +0.24 s
shake: PASS (exit 0)
$ for f in 10001 10101 ... 12401: ... --shake $f      25 of 25 exit 0
$ python parse_shake.py fb-shake7301.txt fb-shake9101.txt campaign/shake-*.txt
27 sets of 40 seeds; sets passing: 27/27
  gentle from the hover, peak at most 0.003° (0.088); the kick at most 0.20 s (0.3); Y band at least 0.53 m/s² (0.3);
  end after the arrest at most +0.30 s (0.31); ramp ring at least 1.33 s (1.0); 40 A longer 240/240; rigid peak ratio 0.96-1.11 (0.8-1.2)
  fast: 2 misses in 2160 flights; worst set 79/80 (nearly every needs 76); sets failing the old 'every 9 m/s punch shown': 2/27
  wob: 0 misses in 4320 flights; worst set 160/160 (152); sets failing the old 'every ramp shown': 0/27
  held: 3 misses in 6480 flights; worst set 238/240 (228); sets failing the old 'every arrest held': 2/27
  further: 7 misses in 6480 flights; worst set 239/240 (228); sets failing the old 'every one tilts further': 7/27
  40 A tilt ratio: least 0.79, the sets' medians 3.97-5.04
  2 kg from 9 m/s past the kick: 25-34/40 (its median > 0 needs 20)
  ringless: weaker 183-215/240 (168); margin D - 2R 4-48, mean 28.8, sd 10.9; P(D - 2R < 0) 0.37 %
  rigid as long: 38-40/40 (38), 11 misses in 1080
  the old strict rise of the whole lengths' medians fails on 3 of 27; ringless from 9 m/s shown on 43-63 of 80

$ ... --scenarios
  slow flight takes little: under 2 % at 2.9 m/s: 2.9 m/s at -1.0°: rear 1.8 % under front ...: PASS
  known-wrong: no ring ..., the six arrested descents on seeds 1-40: ... weaker 206/240; past the kick (0.3 s) on 37/240 against 106: fails, as it must
scenarios: PASS (exit 0)
$ ... --contacts
  E01's run, resting, each support once: ... 8 open pairs: 0 plane, 8 soil ...: PASS
  E01's run, throttle cut, as the stub's: ... at rest 0.419 s after it (the stub's 0.419) ...: PASS
contacts: PASS (39 rows, exit 0)
$ ... --golden
  powered 0x53F1D10489702031, contact 0xAAEA31DBFBB13710, range 0xC79B60F487AEB8B6, rain 0x53B90D4307BF1534, tumble 0xBCBD33E6B6A94844 (each matches);
  every kept audit (model 22's drone, model 15, 13 and 14, 19, the micro branch, the power branch) bit for bit: PASS (exit 0)
$ ... --landing          every row PASS, exit 0 (the falls 0.798 / 0.518 / 0.396 s against 0.806 / 0.527 / 0.406)

# the temporary switches (scratch worktree, qa-temp-switches.diff)
$ QA_NO_SEAM=1 dotnet test --filter <the seven soil and boards cases>
  Failed OnSoilItRestsAsOnThePlane("run"): Expected 8, But was 16
  Failed WithTheAirframesEffectsSoilIsStillThePlane("run"), InASteadyCrosswindSoilIsStillThePlane("run"): Expected 16 and 8, But was 16
  the four S04 cases and the boards: Passed
$ QA_NO_SEAM=1 ... --contacts      E01's run, resting, each support once: 16 open pairs: FAIL; contacts: FAIL
$ QA_NO_WIDEN=1 dotnet test --filter <the same>
  Failed OnFlushBoardsEachSupportIsOnePoint: Expected 12 and 8, But was 12 (sank 2.162 mm, 0.9997 of the weight)
  Failed WithTheAirframesEffectsSoilIsStillThePlane("run"): Expected 0.419 +/- 0.01, But was 0.408
  Failed InASteadyCrosswindSoilIsStillThePlane("run"): Expected 0.11104 +/- 0.001, But was 0.10779
$ QA_NO_WIDEN=1 ... --contacts     E01's run, throttle cut, as the stub's: FAIL; contacts: FAIL
$ (no switch) the same seven cases and --contacts: all Passed, PASS

# the shake's end after the arrest, 4000 seeds a cell (QaEndTail, scratch)
$ QA_ALL=1 QA_FIRST=20001 dotnet test --filter QaEndTail
  0 kg, drop 1.0 s: shown 4000/4000; end after the arrest max +0.301; over Settle 0.31: 0; >= 0.28: 5; >= 0.30: 2;
    histogram 0.20:237 0.21:113 0.22:63 0.23:97 0.24:137 0.25:137 0.26:57 0.27:6 0.29:3 0.30:2
  0 kg 0.3 / 0.6 s: max +0.196 / +0.259; 2 kg 0.3 / 0.6 / 1.0 s: max +0.166 / +0.143 / +0.141; none over Settle

# Unity EditMode, .claude/worktrees/phys-int2 at 18a594e4 (git lfs checkout; Unity Hub running; AC)
PS> & $env:UNITY -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults <qa-pi-recheck>/unity/Logs/qa-recheck/editmode.xml -logFile -
  1025 tests: 1017 passed, 0 failed, 8 skipped (the six [Explicit] probes, N4's two [Ignore]); 648 s; clr 4.0.30319.42000, "Unity Child Domain"
  ShakeTests.TheShakeLastsAboutTheArrest: Passed, 10.6 s; the same two lines as .NET
  powered 0x53F1D10489702031, contact 0xAAEA31DBFBB13710, range 0xC79B60F487AEB8B6, tumble 0xBCBD33E6B6A94844, the map's wind 0x0EF8A67AC398C411: each .NET's

$ python tools/hygiene/precommit.py --tree HEAD     2286 files scanned, 0 findings
$ openspec validate --all --strict                  Totals: 24 passed, 0 failed (24 items)
$ git merge-tree --write-tree origin/unity/main HEAD   (unity/main at 81f3804e) e5a85a67…, no conflict
```

### Hygiene of the 18 new commits

- **Identities, author and committer alike:**
  - 13 by `Physics Engineer <physics@450fpv.local>`, plus the wind review's merge (`b5596083`)
  - 3 by `QA Engineer <qa@450fpv.local>`
  - 1 by `Orchestrator <orchestrator@450fpv.local>`: the merge `18a594e4`
- **Messages:**
  - every one a single `type(scope): summary` subject, at most 71 characters
  - no body, no trailer, no co-author, Claude or Anthropic line
  - one nit: `de0eb78f`'s subject capitalises "N4's" against AGENTS.md's lower case (the merge above it writes "n4's")
- **"Tests and texts only" holds:** the runtime diff is two comment lines.
- **Paths:** nothing under `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`.
- **OPSEC:**
  - The tree scan is clean: 2286 files, 0 findings.
  - In the added lines, the only date-shaped string is the name of the repo's own handover file in `docs/`, quoted in the wind review.
- **This re-check's commit:** the two review files, staged by path, under the QA identity. My logs, switches and scratch test stay out of git.
