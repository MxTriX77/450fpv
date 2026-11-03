# QA review: the physics integration, flight half

**Verdict: changes requested.**

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
