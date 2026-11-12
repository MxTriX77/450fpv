# Design: w9-village-physics

Every number is labelled **measured**, **published**, **carried**, **derived** or **assumed**, as in `audit.md`, which holds the findings each task starts from. Each task writes its own section below and leaves the others alone, so the sub-branches merge cleanly.

## 2.4 The soil anchors' reach (G1)

**Owner:** physics-engineer · **Requirements:** PHY-01, PHY-04 · **Model:** 86 · **Branch:** `unity/physics/physics-integration-2--w9-anchors` · No log format, event kind or `QueryVersion` change.

### The line (measured, a scratch print of each corner's stretch, not committed)
The 9.98° weeds package, nose uphill, the pilot's cut landing. Corners rr and rl (the soil's sliding feet, `SkidSoil.Sliding`) after the touch:

| Step | fn (N) | trial's tangential size (N) | k_tt (N/m) | stretch before → after the slide (m, along the force) |
|---|---|---|---|---|
| 143 ms | 0.23 | 0.48 | 9.81 | 0 → +0.037 |
| 144 ms | 0.19 | 2.11 | 9.67 | −0.037 → +0.172 |
| 145 ms | 0.10 | 32.6 | 9.49 | +0.172 → +3.60 |
| 146 ms | 0.12 | 91.0 | 9.26 | +3.60 → +13.4 |
| 155 ms | 18.6 (soil) | | | 39.2; the moment 1,455 N·m on 41.5 N |

- **The line is the soil foot's slide,** `SoilFinal` (`ContactModel.cs`, the `Holds` branch): `q.Stretch += t·(size − kinetic)/k_tt`. The leg points' slide in `Final` is the same rule.
- **Why it runs** (derived from the print): the corner carries 0.1–0.2 N, so the groove's anchor (`FrictionStiffnessRatio`·k_n at a 0.3 mm sink) is nothing and the side give in series with it is 0 (`sp.Across` 0). k_tt is then the axial spring along the slip alone, k_a·u_t² = 5.23e4 × 0.0137² ≈ 9.8 N/m.
- **The trial is not that spring.** It is the lever's statics (p7g-legs 1.4) with the dampers. At 144 ms the foot is held 37 mm across the leg at no axial push, so the link of γL must lengthen 3.9 mm (its rise, δ²/(l + h)) and carries −202 N along it, a tension. Only 0.36 N of the trial's 2.11 N is the stretch's own spring (k_tt·|s|).
- **So the slide overshoots.** It divides the whole excess by the spring's 9.7 N/m and moves the anchor 0.21 m, past zero to +0.172 m along the force. The next trial's lean is larger, its tension larger, and each step multiplies the stretch: 3.6, 13.4, 39 m.
- **The foot is the moment's lever.** The force acts at the contact point less the stretch (`q.Point − q.Stretch`), so the moment grows with the stretch while the force stays at a few newtons. On Village (311.19, 50.71) the same: 6,814 N·m at 116 N, a 58.7 m lever.
- `NilStiffness` (W3 2.2 §3) only stops k_tt → 0. At 9.8 N/m it is far above its 1e-6 of the axial share (0.05 N/m), and the stretch still runs.

### W3 2.2 §3's question, settled
"The trial's force and the cone's slide use different stiffnesses."
- **Each is right for what it describes.** k_tt = (k_a·u_t² + k_q·(1 − u_t²))·share (+ the frame's share along the skid) is the derivative of the trial with respect to the anchor's stretch at the leg's rest: the anchor's own spring along the slip. The trial is the leg's whole force: the lever's statics (nonlinear in the lean, in tension once the foot is held past the link's circle), the dampers, the law's rest offsets.
- **The return map is exact only where the excess is the spring's.** There the slide puts the stretch where the spring alone is at the cone's edge. Where the excess comes from the lever's statics or the dampers, no move of the anchor relieves it, and dividing it by the spring sends the anchor past zero and on. That is the overshoot above.
- **What the stretch can be is bounded by geometry, not by either stiffness.** The stretch is the foot's move from the body's point to where the ground holds it. The leg reaches a held foot only by turning its lever: the pseudo-rigid-body link of γL about its characteristic pivot (`ContactSpec.LegLeverLength`, p7g-legs 1.4). Turned square, its foot is γL across, the most any turn of it moves the foot across the leg. A held foot farther than that is one the leg no longer reaches.
- **The rule 2.4 takes (the reach, a release):** where a stretch would pass the reach r (γL across the tube, the frame's own lever along the skid), the ground has let the foot go. The leg springs back and the hold starts again where the foot is: the stretch is 0, and a soil foot's anchor is set at the body's point (`ContactModel.Reached`, `ContactSpec.AnchorReach`). The slide's rule itself is unchanged: within the reach the two stiffnesses keep their roles, and a flight whose stretches stay within it steps as model 85 did, to the bit.
- **Three rules were measured** on this branch (each a whole run of the slope rows, the leg tests and the glide landings):
  1. **The stretch held at the reach** (scaled back to r). It meets the moment's bound on every row, but it holds the foot at the lever's square, where its statics are in tension, step after step. On the dirt track the pilot's glide landing then pitched 39.1° (model 85: 0.707°). The 9.98° weeds package hopped at 237 ms on 341 N and turned over at 1.4 s. **Dropped: a foot at the reach is not held.**
  2. **A slide held within what the stretch's own spring carries** (Δ ≤ −s·t − kinetic/k_tt: no slide past the spring's cone edge, none along the force). This is W3's second bound. It moved the 20° meadow landing along from resting to its side (53.4 / 51.6°), the ridge line's replay and a crosswind rest. **Dropped:** it holds an anchor whose spring is not the excess while the body slides, so the legs take side loads no ground gives.
  3. **The release (taken).** The dirt-track and tilled glide landings are model 85's to the printed digit. The 9.98° weeds package rests (10.0 / 0.0°), within 0.1° of Village's 10.00° weeds spot (10.0 / 0.1°).
- **The alternative of a fully consistent return** (the slide on the spring's share alone) is rule 2's family. It changes every sliding step of every flight, so it stays a p7g-legs review (suggested below).
- **Why W3's two wider bounds moved p7g's scenarios, and why that is the fix here** (measured, audited below): the scenarios they moved are the ones the reach moves. In each one an anchor passes γL in one step, which is this bug: 2–5.8 m behind a hanging support, or 0.21 m in one step on the drifting touch. W3 dropped its bounds because those scenarios were pinned on their outcome. They are re-pinned here on the physics.

### The model and its numbers
Reach across the tube: **r = γL = π²L/12** (derived, p7g-legs 1.4: `LegSwayLoad / LegTubeSideStiffness`). L = 0.2135 m is the tube's free length (measured, drone.json), so **r = 0.1756 m** (derived).

Reach along the skid's frame: **r_f = P_f/k_f = 146.6 / 784 = 0.187 m** (derived, p7g-legs model 54: `LegFrameLeverLength`). It is blended by the squared cosine of the stretch's way to the frame, as the slide's stiffness is:

  r(s) = r + (r_f − r)·(s·f)²/|s|²   (only on a leg whose frame holds, `_frame[c]`; r alone otherwise)

  s ← 0   when |s| > r(s)   (the hold lets go; a soil foot's anchor is set at the body's point)

- **Where it is applied:**
  1. A soil corner's trial (`SoilTrial`), on the stretch it forms from the anchor, so this step's force is the leg's at a held foot or at its rest.
  2. After the soil foot's slide (`SoilFinal`), so this step's moment acts at a foot the leg reaches.
  3. After a leg point's slide (`Final`).
- **What it leaves alone:** the anchors' accumulation inside the cone (`Trial`, `LeverTrial`, the soil corner's other points), the body shapes' friction (`Friction`, a consistent return with k_t = ratio·k_n), and the cone, the forces and the slide's k_tt.
- **The soil's own give is left out of the reach** (derived, not added). The groove's tangential give at the cone's edge is kinetic/(ratio·k_n), about μ·z/(ratio·n) for a soil force ∝ zⁿ: of the order of the sink, millimetres against r's 176 mm.
- **The known-wrong:** `ContactSpec.AnchorReach` off is model 85's slide (`GoldenFlights.WithoutTheReach`). It fails the new test (below).
- **No new parameter.** r and r_f are the legs' existing derived levers. No number is assumed.
- **The test's reach, 0.5 m** (derived, the spec's): the drone is 0.6 m across, so no contact point lies more than about 0.5 m from the centre of mass. The test bounds |M| by 0.5·|ΣF|, the vector sum's size, which is never more than the sum of the sizes: a step under it is under the spec's bound.
- **What is logged:** nothing new. The step record's contact force and moment (`StepTrace.ContactForce`, `ContactMoment`, already logged) are what the test reads. A forensic reader judges a step by their ratio: a lever past 0.5 m is a physics bug. A release is not an event: it is geometry, as a foot's rise is.

### The new test (`AnchorReachTests`)
- **`OnEverySlopeRow_TheContactMomentHasAForceBehindIt`:** the pilot's cut landing along and across every row of `flightbench --slopes` (meadow_sod 0, 9.98, 20.00, 30.03, 38.03°), the audit's package rows (weeds 9.98 and 14.36°, meadow_sod 9.43, 14.47 and 19.70°) and the audit's seven farmland spots (1.1's slope spots, without the 35.65° one inside a bush). 34 landings in all; in no step after the touch does |M| pass 0.5·|F|.
- **`WithoutTheReach_TheWeedsLandingHasAMomentWithNoForceBehindIt`:** model 85's rule, its known-wrong, fails the same bound on the audit's row.
- `SlopeScenarios.Land` takes a surface and an edit, and `LandOnFarmland` lands at a farmland spot nose uphill. Both record the largest lever (`Touchdown.Lever`); `LandingScenarios.Outcome.Lever` records the same.

Measured, the largest lever after the touch, model 85 → 86 (along / across; m), and how the landing ends (tilt from the normal, max / end):

| Row | Lever, 85 | Lever, 86 | Ends, 85 | Ends, 86 |
|---|---|---|---|---|
| meadow 9.98° | 0.244 / 0.239 | the same | rests / on its side | the same |
| meadow 20.00° | 0.217 / 0.256 | the same | 20.0/3.9 / 64.6/51.5 | 20.0/1.3 / the same |
| meadow 30.03° | **74.1** / **63.3** | 0.334 / 0.253 | 171.8/31.7 / 171.7/77.1 | 52.8/51.5 / 54.0/44.3 |
| meadow 38.03° | **80.4** / 0.335 | 0.344 / 0.273 | 66.5/51.5 / 55.7/51.5 | 176.3/95.5 / 57.4/51.5 |
| weeds 9.98° | **35.0** (1,455 N·m on 41.5 N) / 0.334 | 0.304 / 0.334 | 54.9/51.5, 514 m/s² / on its side | **10.0/0.0, 26 m/s²** / the same |
| meadow 19.70° | **11.4** / 0.258 | 0.217 / 0.258 | 167.0/42.1 / on its side | **19.7/1.0** / the same |
| farmland (313.04, 20.00) 10.00° weeds | **27.6** / **4.17** | 0.304 / 0.334 | 140.3/140.3, 994 m/s² / 53.2/51.1 | **10.0/0.1, 27 m/s²** / 53.7/51.1 |
| farmland (311.19, 50.71) 9.39° meadow | **58.7** (6,814 N·m on 116 N) / 0.335 | 0.284 / 0.335 | crash, 176.8/110.4, 1,898 m/s² / 52.9/51.5 | **9.4/0.0, 24 m/s²** / the same |
| farmland (507.29, 572.78) 19.67° | **7.8** / 0.328 | 0.356 / 0.328 | 56.4/34.4 / 65.5/43.7 | 58.8/46.8 / the same |
| farmland (522.83, 587.58) 24.77° | **86.8** / 0.338 | 0.357 / 0.288 | 171.1/48.6, 762 m/s² / 69.6/35.1 | 55.4/33.8 / 62.5/33.8 |
| farmland (523.94, 596.09) 29.35° | **14.4** / **9.1** | 0.357 / 0.278 | 93.3/65.3 / 52.7/31.7 | 63.4/35.5 / 68.6/30.4 |
| the rest (0°, 9.43°, 14.4–14.5° rows and spots) | 0.19–0.34 | the same | | the same |

- **The 10° rows now agree, Village against the package** (the audit's §1.6 table): weeds 10.00 / 9.98° both rest (10.0/0.1° and 10.0/0.0°), meadow 9.39 / 9.43° both rest (9.4/0.0° and 9.4/0.3°). The audit's "within a degree of 10° the outcome flips" was this bug.
- **The 20° rows hold or slide as before,** less 2.6° of end tilt along. Past 24° they slide and go down a side, as model 85 did, but without the tumbles the spikes drove (762–1,898 m/s² before, 26–431 m/s² now). 38.03° along turns over at 1.2 s (431 m/s²): there it slides 14.5 m down the fall line at up to 8.7 m/s, a slide's tumble with a 0.344 m lever, not this bug.

### Goldens and pins, audited case by case
Before is f8ca8fe0 (model 85), run in a scratch worktree with the same benches; after is this branch's head.
- **`flightbench --golden`:** all 6 cases and every kept audit pass unchanged, 61 PASS lines and no FAIL: the range golden too. No golden flight's stretch passes its leg's reach, so each steps as 85 did, to the bit.
- **`worldbench --golden`:** ALL PASS. One stamp moved: `farmland_wind.version` 1.85 → 1.86, the model number it pins (D-072). Its six data cases (grid, crest, lee, ledge, open, node) keep their hashes: no wind or world code changed.
- **`LegChangeTests.TheNextLandingFeelsADamagedLeg`** (re-pinned):
  - What moved: the missing-leg build no longer tips under 4 m/s of drift toward its side (model 85: 2–3 m/s); intact and bent never did.
  - Why it is the fix: measured over every leg and both ways, model 85's tips at 2–4 m/s carried levers of 1.97–11.2 m (a hanging support's anchor left behind it as the body slid). The tips under 2 m/s had levers of 0.28–0.32 m and stay.
  - The assertion now reads: toward its side the missing leg tips at a lower drift, or, where neither build tips, it leans farther at 0.3 m/s (missing 4.8–19.1° against intact 0.1–0.8°, measured; the bisection reads ∞ when 4 m/s does not tip, though leg_fr toward x+ still goes over at 1 m/s, as in 85).
- **`LegReplayTests.ReplayThroughABrokenAndADetachedLeg`, the drifting touch** (re-pinned):
  - Model 85's 10° and (0.6, −0.5) m/s bent leg_fr 1.92° only after a support's anchor jumped 0.21 m in one step (step 331, past the 0.176 m reach; its largest lever stayed 0.30 m). With the release that touch bends leg_rr 0.30°.
  - The touch is now 12° and (1.0, −0.7) m/s: leg_rr bent 1.67° and on, nothing broken (a scan of 18 touches, measured). Its log replays bit for bit.
  - The damage flight above it (4,000 steps, 149 events) is unchanged.
- **`RangeContactTests.WithoutTheHangingGiveTheWindyCrownIsModel50s`** (a kept audit of model 50):
  - With the reach, leg_fr parts at step 10339 and the flight leaves 50's path; it logs two tunnels, not 50's one.
  - Like every rule since 50 it flies with the reach off: `GoldenFlights.WithoutTheReach` joins its edit, "since 86". It is model 50's flight again: its Tunnel at step 10975, 0.115 m deep.
- **`SlopeTests`:** every test passes unchanged: the 38° landing (peak under 490 m/s²), the rests, the ridge's replay. The landing table is `flightbench --slopes`' and moved as below.
- **`flightbench --slopes`:** PASS. Rows that moved (85 → 86; peak m/s², slide m, tilt max/end):
  - along 20.00°: 31 → 31, 0.81 → 0.78, 20.0/3.9 → 20.0/1.3
  - along 30.03°: 311 → 73, 10.67 → 10.43, 171.8/31.7 over → 52.8/51.5
  - along 38.03°: 367 → 431, 15.81 → 14.54, 66.5/51.5 over → 176.3/95.5 over
  - across 30.03°: 592 → 114, 171.7/77.1 over → 54.0/44.3
  - across 38.03°: 85 → 135, 55.7/51.5 → 57.4/51.5
  - At rest, Sunny along 30°: 5.48 → 6.05 m, and 38°: 14.39 → 10.81 m. Rain along 38°: 12.93 → 12.95 m; Rain across 38°: 14.83 → 14.81 m.
  - Every row from 0 to 20° at rest, the holding slopes (24.89 / 25.36 / 25.73°), the crashes, the strikes and the lines are unchanged.
  - Each moved row is a slide down 30–38°, past the friction's bound, where an anchor slid past the reach in model 85 (levers 63–80 m on 30.03° and 38.03° along).
- **`flightbench --legs`:** no FAIL.
  - The grid moved by one or two seeds in a few cells: over 7.8 % → 7.3 %, down 30.8 % → 29.1 %, damaged 61.6 % → 61.8 %. Its largest set moved where a drifting cell's anchors slide (0.5 m/s, 10°, 0.6 m/s: 2.38° → 4.51°).
  - The tipped landings on the soil (2.5 m/s across the skids): over in 5 of 8 → 3 of 8 with the frame. The dirt track and S04 with the throttle cut were 164–178° tumbles in 85 and are now on their side (54.7–57.2°).
- **`flightbench --contacts`:** no FAIL.
  - The crown flights through twigs moved a little: the test range's crown impact 949 → 748 m/s², the farmland crowns' twig impulse within 0.1–26 N·s, the bush 48.63 → 46.45 N·s. Every one is still no tunnel, no pair lost, PASS.
  - The glide landings (tilled, dirt track) and the soil rows are model 85's to the printed digit.
- **No golden was re-recorded.** Each flight that moved is one where a stretch passed its leg's reach.

### Checks run
In the handback: the new test, `flightbench --slopes`, `--legs`, `--contacts`, `--golden`, `worldbench --golden`, `dotnet test tools/simtests` and `tools/inputtests` (replays bit for bit, 0 B in steady state), `precommit.py --tree HEAD`. Timings are not counted: these runs shared the machine.

### For the next tasks
- **2.6 (the legs on a side slope):** the across rows are unchanged by 2.4 to the printed digit at 9.98°: they still set and part the downhill legs about 0.3 s after the touch, with no lever past 0.34 m. That reading stands for 2.6 to judge.
  - The 20° across rows and Village's 19.67–29.35° spots end on a side, as before.
  - The lever's statics in tension (a foot held past its circle at a light load: P = k_a(l0 − l) < 0) remain; the release now ends them at the reach.
- **2.9 (Village's ground rows):** the along rows at 9.4–10° now agree with their packages within 0.3° (rests), and 14.4° as before. Above 20° both slide and the comparison stops at the legs' failure, as the audit says.
- **Suggested, a p7g-legs review** (not in this branch): a consistent return on the spring's share alone. The slide divides the trial's whole excess by the stretch's spring; the release bounds what that can do, but the overshoot itself is still there within the reach.

## 2.6 The legs on a side slope (G2)

To be written by task 2.6.

## 2.7 The crown twig's repeated break (G5)

To be written by task 2.7.

## 2.8 The plants' numbers for the props

To be written by task 2.8.

## 2.1 Village's stem field

To be written by task 2.1.

## 2.2 Stems on the props

To be written by task 2.2.

## 2.3 Leaves in crowns

To be written by task 2.3.

## 2.9 Village's ground rows and the liftoff harness

To be written by task 2.9.

## 2.10 Stems on the legs and the fiber

To be written by task 2.10.

## 2.5 Cut bits on camera

To be written by task 2.5.
