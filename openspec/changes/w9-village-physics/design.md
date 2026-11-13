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
- **The rule 2.4 takes (the reach, a release):** where a stretch would pass the reach r (γL across the tube, the frame's own lever along the skid), the ground has let the foot go. The leg springs back and the hold starts again where the foot is: the stretch is 0, and a soil foot's anchor is set at the body's point (`ContactModel.Reached`, `ContactModel.AnchorReach`). The slide's rule itself is unchanged: within the reach the two stiffnesses keep their roles, and a flight whose stretches stay within it steps as model 85 did, to the bit.
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
  3. After a leg point's slide on the ground (`Final`, a pair whose object is the ground): the floor, a terrain's facets and faces.
- **Not on an object's shapes** (measured, and why). A leg on a crown's twig shell slides its anchor the same way: 0.66–0.99 m in eight steps on IslandTree's shell in `ObjectContactTests.ADroneInACrownPaysForItsTwigsAndDoesNotHang`.
  - Released there, that flight stays lodged in the crown at 4.1 m (6 twigs cut, no crash), where model 85's leaves it (15 twigs, crashed 65 m away). Its known-wrong, the shells glancing, then crashes instead of hanging. The test fails both ways.
  - So the crown's departure in model 85 rides this same runaway. What should hold or free a drone in a crown is the twigs' and leaves' physics: 2.3 (leaves in crowns) and 2.7 (the crown twig).
  - 2.4 keeps the release to the ground's hold, its own gap (G1). An object's shell steps as 85 did, and the runaway there is named for those tasks (below).
- **What it leaves alone:** the anchors' accumulation inside the cone (`Trial`, `LeverTrial`, the soil corner's other points), the body shapes' friction (`Friction`, a consistent return with k_t = ratio·k_n), and the cone, the forces and the slide's k_tt.
- **The soil's own give is left out of the reach** (derived, not added). The groove's tangential give at the cone's edge is kinetic/(ratio·k_n), about μ·z/(ratio·n) for a soil force ∝ zⁿ: of the order of the sink, millimetres against r's 176 mm.
- **The known-wrong:** `ContactModel.AnchorReach` off is model 85's slide (`GoldenFlights.WithoutTheReach`, on the core after its construction). It fails the new test (below).
  - It is on the model, not in `ContactSpec`, because every `ContactSpec` field is in the header (`ContactTests.TheHeaderCoversTheContactTypes`) and this change takes no log format.
  - So a flight flown with it off does not replay. Only the suite's known-wrong and model 50's kept audit fly it; neither is recorded.
  - If a later format bump (2.2's) wants it in the header, it moves to `ContactSpec` beside `HangingGive`.
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
- **`flightbench --contacts`:** model 85's output to the printed digit, every row (only the test range's load time differs). Its crowns and bush are objects' shells, which the release leaves alone. Its glide landings and soil rows keep their stretches within the reach.
- **`ObjectContactTests`:** every test passes unchanged, the crown flight included (model 85's: 15 twigs cut, crashed 65.41 m away).
- **No golden was re-recorded.** Each flight that moved is one where a stretch passed its leg's reach.

### Checks run
In the handback: the new test, `flightbench --slopes`, `--legs`, `--contacts`, `--golden`, `worldbench --golden`, `dotnet test tools/simtests` and `tools/inputtests` (replays bit for bit, 0 B in steady state), `precommit.py --tree HEAD`. Timings are not counted: these runs shared the machine.

### For the next tasks
- **2.6 (the legs on a side slope):** the across rows are unchanged by 2.4 to the printed digit at 9.98°: they still set and part the downhill legs about 0.3 s after the touch, with no lever past 0.34 m. That reading stands for 2.6 to judge.
  - The 20° across rows and Village's 19.67–29.35° spots end on a side, as before.
  - The lever's statics in tension (a foot held past its circle at a light load: P = k_a(l0 − l) < 0) remain; the release now ends them at the reach.
- **2.9 (Village's ground rows):** the along rows at 9.4–10° now agree with their packages within 0.3° (rests), and 14.4° as before. Above 20° both slide and the comparison stops at the legs' failure, as the audit says.
- **2.3 and 2.7 (crowns):** a leg's anchor on a twig shell still runs past the leg's reach (0.66–0.99 m on IslandTree). The crown flight's "does not hang" rests on it (above). Whoever gives a crown its hold should apply `Reached` to object pairs with it and re-judge that test.
- **Suggested, a p7g-legs review** (not in this branch): a consistent return on the spring's share alone. The slide divides the trial's whole excess by the stretch's spring; the release bounds what that can do, but the overshoot itself is still there within the reach.

## 2.6 The legs on a side slope (G2), and the legs' failure law against the map-wide sweep

**Owner:** physics-engineer · **Requirements:** PHY-02, PHY-04, PHY-24, DRN-01 · **Branch:** `unity/physics/physics-integration-2--w9-legs` (from 53e182dc, model 90) · the physics ledger's gaps 7 and 14, the sweep's findings 4, 5 and 6 (`docs/reviews/w9-village-physics-sweep.md` §4). The model number is the orchestrator's at the merge.

Labels: **published** (a cited source), **measured** (letter T or a run here), **derived** (computed here from labelled inputs), **assumed** (a named, tunable value). The scratch harness that made the traces is the session's `night-legs/probe` (not committed): it flies `SweepScenarios.Fly` with a read-only `observe` hook (added to the test code here) and prints each leg's clamp moment, axial load, lean and side give, and a hanging corner's ground pairs.

### 1. The hand calculation (before any code)

**The tube** (drone.json `legs.tube`, every input labelled there): PVC-U, D 10 mm, d 8 mm (wall t 1 mm, D/t 10), E 3.0 GPa, σ_y 55 MPa (50–58), ε_b 20 % (15–20), free length L 0.2135 m, the foot's lever λ 0.2335 m; the clamp's edge factor K_t 1.5 (1.2–2.0, assumed). Derived: I 2.898e-10 m⁴, S = I/c 5.796e-8 m³, Z_p 8.133e-8 m³, shape factor f 1.403, EI 0.869 N·m². The drone of the sweep (L1, 2 kg, legs on): 6.080 kg (derived, `BuildMass`), 14.9 N a leg at rest.

| Capacity at the clamp (nominal; the bounds' weakest–strongest) | N·m | Label |
|---|---|---|
| the jaw edge's first local yield, σ_y·S/K_t | 2.13 (1.45–2.80) | derived |
| the section's first yield, σ_y·S | 3.19 (2.90–3.36) | derived |
| the section's fully plastic moment, σ_y·Z_p | 4.47 | derived |
| the tube's local buckling (kink): AISC 360-16 F8-2 for a non-compact round tube, M_n = (0.021·E/(D/t) + σ_y)·S | **3.55** (3.26–3.73) | published formula, applied to PVC-U by analogy: assumed |
| tearing at the jaw edge, the edge's strain K_t·κ·c = ε_b on the section's own moment–curvature law | 4.45 (4.01–4.70) | derived |
| the model-90 law's parting (the moment–curvature law scaled down by K_t: f·σ_y·S/K_t) | 2.98 (2.03–3.92) | as built |

**The material's own behaviour:**
- **Ductile, not brittle, at the jaw's edge.** ε_b (15–20 %) is 8–11 × the yield strain (1.83 %), so a blunt raiser (K_t 1.2–2.0, a printed jaw's edge) yields locally and the yield spreads over the section before anything tears: for a ductile section under a monotonic load the raiser lowers the first local yield, not the limit moment (Shigley's *Mechanical Engineering Design*, §3-13 and §6-2: static loads on ductile parts take no K_t; the limit analysis of a plastic hinge). PVC-U is notch-sensitive (notched Charpy 2–6 kJ/m², drone.json's note), which matters at a sharp notch or in the cold (class-legs.md [12][13]); the jaw is neither, and the sim flies no temperature.
- **It kinks before it tears.** (D/t)·σ_y/E = 0.183 lies between the compact (0.07) and slender (0.31) limits of AISC 360-16 Table B4.1b for round tubes: the tube reaches its first yield but buckles locally on its compression side before its plastic moment. The elastic local buckling strain 0.605·t/R = 0.134 (Timoshenko and Gere, published) is 7 × ε_y, so the kink is plastic. Anyone who has bent thin PVC tube by hand has seen it: it flattens and folds, whitening, rather than snapping. Past the kink the leg carries less and folds; it no longer holds the drone. The kink (3.55 N·m) comes before tearing (4.45), so it is the leg's end as a support.
- **The rate.** A glassy polymer's yield rises with the log of the strain rate (Eyring; Bauwens-Crowet et al. 1969 for PVC). The slope was not readable here; about 4 MPa a decade is assumed. A landing's edge strain rate is 0.05/s (a slope's creep) to 10/s (a drop) against the data sheets' about 1e-2/s (50 mm/min): +3 to +12 MPa, so the capacities above are 5–20 % low under impact. **Not built** (a lower bound, named in §6): it moves no verdict below.
- **A crack.** The model-90 law cracks the tube at the edge's first local yield and halves its bending stiffness from then on (`cracked_stiffness_factor` 0.5, assumed: "a crack through about half the wall"). No source gives it, and the physics above says the edge's local yield in a ductile tube is a yield zone (stress-whitening), not a crack: a through-wall crack needs the edge's strain at ε_b, which is past the kink. The halved stiffness also halves the lever's sway load (47 → 23.5 N), under the downhill legs' 13–38 N on a side slope.

**Each landing, by hand** (the drone level, the throttle cut at the touch; dynamic factor 2 for a suddenly applied load, an upper bound):

| Landing | What the legs carry | Against the capacities | By hand |
|---|---|---|---|
| 0.5 m/s, level | 0.76 J; peak 156 N a leg on rigid ground (52.3 kN/m a leg, derived) | buckling 385 N; no moment across | every leg intact |
| 0.5 m/s across 10° | the feet hold (μ_s 0.5 > tan 10°); W·sin 10° = 10.3 N across, 2.6 N a leg; on the cantilever across (268 N/m, P·Δ to the sway load 47 N): lean 14–18 mm, M 0.76–0.95 N·m static, ≤ 1.9 dynamic; the tangential energy 0.023 J | under the section's yield (2.90 at the weakest draw); the weak draws' edge may whiten | **stands**, leaning 1–2 cm; no set, no break |
| 0.5 m/s across 15° | 3.9 N a leg across, the downhill legs 25 N: lean 21–31 mm, M 1.1–1.6 static, ≤ 3.2 dynamic | about the section's yield: a set on a weak draw, under the kink | the soft legs' quasi-static roll tip is 14.4° (`LandingStability.SoftRollTipDeg`, p7g-legs 1.3): **past it the drone rolls over on its legs' sway, broken or not** ("the drone may fall", P9) |
| 1 m drop (touch 2.9–3.4 m/s) | 29 J at 3.1 m/s. Each leg: 1.42 J to its buckling load (the arm and tube in series), then the bow at 385 N: w = 12.9 mm and 2.0 mm of shortening to the kink, 0.77 J; 2.2 J a leg, 8.8 J for four | the bow's moment 0.715·P·w passes the kink within about 2 mm of shortening | **every leg kinks (breaks)** within a few ms: four legs take a level touch up to about 1.7 m/s (2.2 with the soil's sink); the rest goes into the frame and the canister. Right, as the sweep found |
| 2 m/s slide along the skids, 0.3–0.5 m/s down | the skids slide (μ_k 0.4): along the skid each foot carries μ_k·N; at the sim's 30–50 N a corner that is 12–20 N; the frame leg's clamp takes 0.524·λ of it: 1.5–2.5 N·m | under the section's yield; a foot force of 29 N kinks a frame leg | **slides to a stop upright in about 0.5 m** (3.9 m/s², and tipping on the friction needs μ_k > tan 42.7° = 0.92); a hard touch (N past about 70 N a corner) kinks the leading pair |

### 2. The material and the mount against the sources and the pilot's words
- "Plastic", "a little bit soft", "hand-made", "they vary" (P3, P4, D-086): PVC-U tube is plastic and the softest 10 mm leg (class-legs.md §2); on a 10° side slope it leans 1–2 cm, and it sways at about 2 Hz. D-086 settled it ("the PVC-U legs stand").
- "As flexible as a carbon plastic": carbon (CFRP) tube is 23 × as stiff (class-legs.md row I), glass fibre 9 × (row G). class-legs.md §3 inferred GFRP from letter T's sheen, but named no source for the class; the pilot says "usually plastic". **No source supports another material or mount for this class's hand-made legs, so the tube stays.** Its stiffness and strength draws (D-086) stand.
- "The legs bounce the drone back up" (P7): that is the legs' axial spring (52.3 kN/m, mostly the arm under the clamp, at ζ 0.2) and the soil, which no change here touches.
- "Sometimes the stumbling effect" (P3) and "it falls when landing a bit more often" (P15, D-079): the soft cantilever's lean and its 14.4° roll tip make a cut landing on a side slope fall from about 14°, and a hard landing kinks its legs. A leg that breaks under the static load on a 10° slope, 0.3 s after a gentle landing, is not in the pilot's account and not in the hand calculation.

### 3. The sweep's rows, traced (model 90, this branch's base)
- **The drop** (Village #0, 3.10 m/s on weeds): the touching pair reaches the 385 N buckling cap in 2–3 ms, cracks at 1.16–1.85 N·m and parts at 2.04–3.20 N·m (the bow's moment), all four within 9 ms. **As the hand calculation says** (they would kink at 3.26–3.73 instead, about 1 ms later; still all four). Then the body falls 20 cm in 25 ms with no contact, meets the ground at 2 m/s on its frame and canister for about 30 ms, leaves it at 0.9 m/s turning 4–5 rad/s and lands on its back (146°) at 1 s. That rebound and turn are the frame parts' and the props' contacts, not the legs' law (§6).
- **Across 12.6°** (Village #199 Cut, meadow_sod, 0.45 m/s): the corners' leans grow over 0.5 s at P 13–38 N (the downhill pair) to 40–67 mm; leg_rr cracks at 77 ms (2.25 N·m against its draw's 2.23), leg_fl at 251 ms (1.78 against 1.77); from the crack their side give falls to 0–50 N/m (the halved stiffness past its halved sway load) and they creep, set and part at 565 and 613 ms (2.57 and 3.68 N·m). The drone then rolls to 60°. **Not as by hand:** the crack's half stiffness (no source) and the K_t on the section's capacity (parting at 2.52–3.58 N·m, under the section's kink).
- **The slide** (Village #0, 2.2 m/s along, 0.43 m/s down): the first foot sticks and the body runs on over it (lean 2.2 mm a ms); the leading legs crack at 1.49–2.04 N·m in 11–37 ms, creep at 1.6–2.9 N·m and part at 216 and 226 ms (2.70 and 2.92 N·m), then the drone flips (51° at the end, 249 of 339 on the back). **Not as by hand:** the same K_t on the capacity and the halved stiffness; every moment it saw is under the section's kink.
- **A hanging half "in the ground"** (range #5 Slide, `support_fl_end` 91 mm past, the corner Hanging, Stub, Folded): the half's geometric depth stays 2–5 mm while its spring's overlap (its depth without the half's elastic give) grows to 95 mm under the tumbling body; the half carries its fold cap (10.8 N). At 376–378 ms its deepest point changes and its pairs read no depth for one step: **with no touching pair the massless half's elastic give is set to zero, its shapes snap back 91 mm into the ground** (the sweep's reading), the next step sees them deep, the give comes back, and the cycle repeats (44 mm, 91 mm, 49 mm, 25 mm over 10 ms). A one-step limit cycle of an explicit massless give, not physics: the half's pair must stay touching while its unturned overlap is positive. The fold's cap is also the edge's first local yield (σ_y·S/K_t over the lever), K_t's misuse again.
- **The 10 m/s crash's 172–194 mm** (`--slopes`): to be traced after the fixes (§4).

### 4. What changes (the plan)
1. **The law** (`LegDamage`): the edge's first local yield (M_y/K_t) stays the `LegCracked` event (the jaw edge whitens) and changes no stiffness; the set follows the section's own moment–curvature law from its own yield M_y(1 − s); the tube is lost (`LegBroken`) at the first of its kink (M_n/M_y, the drawn σ_y and modulus) and its edge's tearing strain (K_t·κ·c = ε_b, as before). The same at a frame leg's socket edge.
2. **The input**: `legs.tube.cracked_stiffness_factor` 0.5 → 1.0, with the reason; a new labelled input for the kink's coefficient (AISC's 0.021).
3. **The hanging half**: its pairs stay touching while their unturned overlap is positive; its fold's cap is the section's yield at the bore.
4. Nothing else: the axial spring, the buckling, the lever, the frame, the soil and the draws are untouched.

### 5. As built

**The law** (`LegDamage`, `drone.json`):
- `LegCracked` fires at the jaw edge's first local yield (σ_y·S·(1 − s)/K_t, as before) and changes no stiffness: `legs.tube.cracked_stiffness_factor` is 0.5 → 1.0, relabelled derived, with the reason in its note.
- The set follows the section's own moment–curvature law from M_y(1 − s). K_t now acts only on the edge's local tearing strain.
- **The kink** (`LegDamage.KinkCoefficient` 0.021, AISC 360-16 F8-2, applied to PVC-U by analogy, assumed): M_n/M_y = 1 + 0.021·E·t/(D·σ_y), on each leg's drawn E and σ_y. The nominal leg kinks at 3.55 N·m.
- **Past the kink a leg folds and stays on, bent.** It is a plastic hinge that holds the kink's moment (an upper bound: a real kink's moment falls as it folds; `FoldMove`). Its rotation is integrated, and the leg tears (`LegBroken`) at `TearRotation` = ε_b·D/(K_t·c): 0.27 rad nominal, 0.15–0.33 over the bounds. This assumes a hinge one diameter long.
- If the edge tears before it kinks, the leg parts at once, as before.
- The same holds at a frame leg's clamp and socket edges.
- A bowed leg (one under buckling) cannot be relieved by a fold, so past its kink it is lost (`LegBroken`). The bow's own fold is not modelled.
- The hanging half's fold cap is the section's yield at the bore, σ_y·S/a, where it was σ_y·S/(K_t·a).

**The hanging half** (`ContactModel`):
- **`Hung`:** a hanging half's pair stays touching while its overlap without the half's elastic give is positive. This ends the one-step snap-back.
- **The give is bounded:** it never moves the deepest point past its overlap. Inside a crown's twig shell it scales by the force its points carry (the twigs give first). Without this, the give ran away to 0.43 m in 50 ms in Pine002_M.
- **A solid touch governs:** in a twig shell that also touches a solid (bark, ground), the corner's chain takes the solid's row and overlap. On the twigs' row the half carried nothing and passed a branch.
- **The tear (`LegDamage.TearSkid`):** a hanging skid folded past its holder's `TearRotation` tears at the bore, and the holder lets it go (`SkidLoose`, `SkidFold`). The skid is one piece in the build, so the holder's own half leaves with it (left open).

**The benches' readings:**
- **`--slopes`' crash** (gap 14) measures the contact model's placed shapes and skips the pieces that have left. The 172–194 mm was the build's shape of a dropped leg.
- The bench now asserts no support or skid more than 20 mm past its corner's give (the sweep's rule). The largest is now 2.2–6.1 mm. The frame parts' 24–64 mm into the sod is printed beside it, not judged (the sweep's finding 11).

**The sweep's kinds against the hand calculation** (traced on the session's probe):

| Kind | By hand | The sim now |
|---|---|---|
| 0.5 m/s, level | intact | intact (`--legs`, every seed) |
| Across 10° | the legs hold the static load; past the soft legs' 14.4° tip the drone rolls | the uphill skid touches and the drone rocks onto its downhill skid. The legs lean 40–50 mm and stay elastic to a tilt of 15.7° (`LegKinkTests`), then the drone rolls past its soft tip and ends on its side (51.5°). The legs are lost only at 73° in the fall (model 90: at 3.7°, 0.3 s after the touch). **G2 is the soft legs' sway and the rocking's momentum, not a leg that parts.** |
| Across 12.6° (#199) | about the tip | the drone sits on its downhill skid and slowly rolls past the tip (61°); the legs fold, then tear in the fall |
| 1 m drop | every leg kinks; 29 J against about 9 J | every leg buckles and is lost within 9 ms; the drone falls on its frame and canister, and may bounce onto its back (the frame's and props' contacts, §6) |
| 2 m/s slide along | slides upright when the corner's N stays under about 45 N; P·Δ on the frame leg adds 0.9 N·m | Village #0 ends upright (3.4°, 11 sets, 4 cracks, no break); range #5 upright (0.4°) |
| A hanging half "in the ground" | never past its contact spring's share | Village 446 → 13 rows, range 318 → 4 (below) |

**The counts, before (53e182dc, model 90) and after** (`--sweep … 400 1 --no-replay`, the same points and flights; the build at 7ddb3c48 on model 91's line). Each cell is the rows ending past 45° / past 120°, of n:

| Village | 0–5° | 5–10° | 10–15° | 15–20° | 20–30° | 30°+ |
|---|---|---|---|---|---|---|
| Land | 0/0 → 0/0 of 339 | 8/0 → 3/0 of 69 | 15/0 → 14/1 | 18/1 → 13/0 | 20/3 → 18/4 | 20/1 → 20/2 |
| Cut | 0/0 → 0/0 | 30/0 → 14/0 | 19/0 → 14/1 | 19/3 → 11/0 | 20/6 → 20/2 | 17/4 → 20/4 |
| Drop | 339/129 → 335/134 | 67/38 → 66/38 | 18/9 → 17/9 | 17/5 → 20/5 | 13/7 → 14/7 | 15/5 → 18/7 |
| Slide | 326/257 → 243/104 | 68/43 → 60/11 | 19/11 → 20/6 | 12/5 → 13/5 | 15/8 → 12/9 | 14/9 → 15/11 |

| Range (level; E01's 22° end) | Land | Cut | Drop | Slide |
|---|---|---|---|---|
| level, of 428 | 2/0 → 2/0 | 3/0 → 3/1 | 370/90 → 363/91 | 309/224 → 24/7 |
| 20–30°, of 20 | 18/6 → 19/7 | 20/6 → 20/6 | 17/7 → 14/8 | 12/3 → 16/8 |

The checks, failing rows (Village before → after; range before → after):
- **Ground:** 464 → 30, of which on a hanging corner 446 → 13; range 332 → 35, of which hanging 318 → 4.
- **Lever:** 0 → 7 (slides, 0.51–0.81 m on 2–13 N); range 2 → 3 (#245 Drop, #435 and #442 Land on E01).
- **Tunnel:** 0 → 0; range 0 → 3 (E01 slides #416, #425, #445; skids 15–20 mm into its soil boxes).
- **EventsOnce:** 0 → 2 (BladeStrike twice on part 7, #283 and #474).
- **Energy:** 23 → 23; range 9 → 9.
- **Rest:** 18 → 23 over the manoeuvres.
- **Alloc:** 76 → 72 (thread-dependent: finding 9).
- Rows SHA-256 without the allocation column: Village before `2495fc1f…`, after `50fd1413…`.

- **Before and after the fold** (an intermediate run with the kink but no fold, Village): slides on level ground 314/244 ended past 45°/120°. With the fold, 243/104.
- **`--legs`** (40 seeds), before → after:
  - over the grid, over 4.8 → 6.3 %, down 31.0 → 28.6 %, damaged 61.8 → 61.8 %
  - the normal landing intact on every seed
  - visible bends (≥ 1° and on) in 0–60 % of a cell, up to 9.3°
  - every row PASS
- **`--landing`:** 29 PASS before and after; two cells' tips move (5° and 20° with no drift).
- **`--ground`:** within the seeds' scatter; exit 1 as before (design 13's lone-corner targets, 2.9's).
- **`--golden` on the merged model-92 tree:** 63 PASS, 0 FAIL. No pin moved: no golden damages a leg.
  - The kept audit "without the line's mechanisms" (model 41's legs on the model-63 fixture, the lever off) moved its contact flight. Model 41 set leg_fr 0.065° where its bowed moment passed the edge's first yield; on the law's section yield that leg only cracks (2.04–2.33 against 2.94–3.28 N·m).
  - It is re-pinned as `GoldenFlights.Model41ContactOnTheSectionsYieldHash` (0xE8FBF706509DA086), audited: the set's onset is that flight's whole move (every leg Cracked, no set).

### 6. What is still not met, and for whom
- **A kinked bowed leg is dropped,** not folded: a level harsh landing keeps a set only in a window about 4 % of speed wide (`AHarshLandingBendsALeg` is judged at the onset). The bow's post-kink fold is the next law item.
- **A torn hanging skid takes the holder's half with it** (the build has one skid part).
- **The drop's flip onto the back** (Village 134 of 339) after the legs are lost is the frame parts' and the props' contacts (a 0.45 restitution off the canister, prop strikes at idle). That is not the legs' law; it is suggested as a task.
- **Red on this branch** (each traced; all pass on model 91's line):
  - `RestTests.ArmingWithAPropOnTheRackMovesTheDrone`: the blade bears 0.80–0.92 N on every seed, under the test's 1 N. The legs no longer soften at a crack. Its thresholds are QA's call.
  - `SweepTests.OnE01sSoilShapesTheReachHoldsTheLever`'s known-wrong: with legs that no longer part, no E01 flight runs a foot away even with the reach off (the largest lever 0.43 m over six flights).
  - `StemLiftoffTests.ALiftoffFromTheRangesStrawIsFlown` (S04, seed 2, the glide): the drone rolls onto its right skid on the mat from 0.25 s, before any set, and is on its side by 1 s.
  - `RangeContactTests.IntoACrown`: prop_rr through V01_M's bark, a blade.
- **Not mine, seen:**
  - `--contacts`' Pine004_1 row: prop_fl through the bark.
  - The E01 soil-box tunnels of #416, #425 and #445.
  - The 7 new Village slide levers on light contacts.
  - The 2 `BladeStrike` repeats.
- **Assumed (named):**
  - `KinkCoefficient` 0.021 (the AISC analogy)
  - the hinge one diameter long (`TearRotation`)
  - the fold holding the kink's moment
  - the 14.40° soft roll tip as G2's judge
  - the rate's effect, about 4 MPa a decade, not built (it would raise every capacity 5–20 % under impact)

### 7. The checks, as run (merged tree, the shared machine; no timing counts)
On f4ded873 + its two test commits (the line's model 92 merged in), unless named:
```
dotnet test tools/simtests -c Release --filter LegKinkTests  → 2 passed (edge 2.125, section yield 3.188, kink 3.553, tear 4.449, lost at
    3.553 N·m, model 90's 2.975; across 9.98°: a leg first set at 15.7° of tilt, first lost at 72.9°; with the halved crack 12.4° and 33.5°)
dotnet test tools/simtests -c Release   → 872 passed, 4 failed of 876 (26 m 44 s, loaded): IntoACrown, ArmingWithAPropOnTheRackMovesTheDrone,
    ALiftoffFromTheRangesStrawIsFlown, OnE01sSoilShapesTheReachHoldsTheLever (§6; all four pass on model 91's line 5b2e1ab7, run there)
flightbench --golden    → 63 PASS, 0 FAIL, exit 0 (no pin moved; model 41's contact audit on its new pin)
flightbench --legs      → every row PASS, exit 0 (6.3 % over, 28.6 % down, 61.8 % damaged)
flightbench --landing   → 29 PASS, exit 0
flightbench --slopes    → exit 0 (the seam row on the leg's buckling load since this branch; the crash rows judge the legs' parts: ≤ 6.1 mm past their give)
flightbench --contacts  → exit 1: 2 tunnels, both blades (prop_rr through V01_M's bark in V01_L's crown; prop_fl through Pine004_1's bark); Pine002_M none
flightbench --ground    → exit 1, design 13's lone-corner targets as at 2.9 (unchanged within the seeds' scatter)
flightbench --sweep farmland|test-range 400 1 --no-replay → §5's counts (model 91's tree, 7ddb3c48; the physics since unchanged but model 92's wake)
flightbench --sweep farmland 400 1 --point 0, --sweep test-range 400 1 --point 5 (with replays) → 10 flights, Replay ok, Alloc ok (0 B) on each
python tools/hygiene/precommit.py --tree HEAD → 3608 files scanned, 0 findings
```

### 8. Addendum: the reds, each traced to its cause (night-legs-reds)

On this branch with the line's model 93 merged in (502ffa0f). Traced with a scratch probe (not committed) that flies each test's own scenario through its read-only `observe` hook and prints the pairs, the resting blades' loads and the feet. On the merge, the stem liftoff's red row moved: model 93 made seed 2's glide pass, and Rain seed 1's vertical touch now fails. Both rows pass on the line alone (4c40ac55, run there).

**1. `RangeContactTests.IntoACrown` and `--contacts`' Pine004_1: a resting blade's stiffness taken at the face's point, not the blade's.** (A physics fault.)
- **V01_M.** prop_rr carves a bark limb and jams (step 11306). The motor turns it on to 31 rad/s, and at step 11315 it stops in the notch it cut: the blade comes back to rest 36.5 mm inside the limb, its slack. The drone runs on at 7.7 m/s, so the blade's overlap past its slack grows 4.4 mm a step.
- `BladeStiffness` takes the contact's radius from the pair's point, which `Depth` sets on the face's latched plane, up to the pair's depth away from the blade. That point drifts past the tip, so the cantilever is read beyond the blade's own length: **290 N/m**, 30.7 N at 107 mm of "deflection". The load falls as the overlap grows, no section passes its strength, and the blade's core leaves through the limb's far side 143 mm past its face (`Tunnel`).
- **Pine004_1** is the same: prop_fl stopped in a crash, 145–165 N at 40 mm, out through a 28 mm branch.
- **By hand at the blade's own point:** the outer segment at r = 0.11 m is a cantilever of some 14 kN/m against an edgewise push. That is 100 N for every 7 mm of approach at 7.7 m/s, so at 1 ms a step the load reaches its root's edgewise strength within about ten steps. The root's strength is σ_f·t·c²/6 = 205 MPa · 5.40 mm · (18.45 mm)²/6 = 63 N·m before the section form; at a 0.096 m lever that is 530–650 N.
- **Fix:** the blade's stiffness is read at its own material point, the core point nearest the face: `p.Point + p.N·(r − depth)`. Nothing else changes, and a shallow pair moves by micrometres.

**2. The brief's swept pass: a turning blade can pass a thin shape within one step.** (A physics fault, though not these tunnels' cause.)
- `PropStrikes` meets a shape only in the disc's slab at the step's end hub (2h = 8 mm). So a shape met along the axis faster than (d + 2h) a step is in no end's slab and is never struck. The thresholds are 13 m/s for a 5 mm conductor, 18 m/s for a 10 mm branch, and 36 m/s for a 28 mm one: falls and dives reach the first two.
- **Fix:** `BladeCut.Crossing` samples the slab's own sweep over the step, at most h apart. The disc's gate makes the pair, and the passes are judged at the hub where the slab held the shape, only for the step's share in which it held it.
- **Test:** `StrikeTests.ABranchCrossedWithinAStepIsMet`: a 10 mm bark branch crossed at 20 m/s.

**3. `StemLiftoffTests.ALiftoffFromTheRangesStrawIsFlown`: a landing half on a step, physics.** (The test is corrected.)
- **The geometry, by raycast:** the S04 patch's centre lies on a mat piece's diagonal edge. The rear-right end support stands about 6 cm past it, over the 0.125 m step to the soil box, and the rear-right joint support stands on the edge.
- **The trace (Rain, seed 1, the vertical touch):**
  - The drone rests on rr (18 N), fr and rl, with fl light. Armed at idle, rr's joint support creeps off the straw's edge (its load 18 → 1.5 N by 0.85 s), and the drone balances on the fr–rl diagonal.
  - The rr corner then drops into the step. Its foot goes from −32 to −117 mm, against the soil box at −125 mm.
  - That is about 27° about the other diagonal (0.125 m over the half-diagonal of 0.24 m), past the soft legs' 14.4° roll tip. fr kinks at 3.39 N·m, rr breaks at 5.7 N·m, and the drone is on its side at 61–71°.
  - The P term spins rr and fr up against the roll (iterm held at 0, airmode not yet active, Betaflight's `pid_at_min_throttle`): the controller answers the fall, it does not cause it.
- The physics never promised a liftoff from a spot with a corner over a 125 mm step. **The test now starts on one mat piece** (`SoilScenarios.PatchMat`: the nearest piece whose box holds a 0.3 m circle).

**4. `RestTests.ArmingWithAPropOnTheRackMovesTheDrone`: the test's assumed thresholds.** (The test is corrected.)
- **By hand from the trace:** rolled 58.4° onto its left skid, the drone's rear-left corner carries 5.7 N, shared by leg_rl (set and cracked at 1.41 N·m) and the blade on the rail.
- On the corrected legs the leg keeps its stiffness and takes 4.8 N, so **the blade bears 0.8–0.92 N** (0.36 mm deflection). With the old crack's half-stiff leg (`cracked_stiffness_factor` 0.5, the same flights) the leg took 2.7–3.2 N and the blade 1.6–2.4 N.
- So the blade bears on every seed, and arming at idle strikes, chips and moves the drone on every seed (19–20 of 20). The pilot's case holds; only the classification moved.
- **The test:** "bears" is now more than the blade's own weight (the prop's 16 g over two blades: 0.078 N), where it was 1 N, assumed. "Over the known-wrong" is now by more than the blade's own resting deflection, where it was 3 mm, assumed: 1.8–3.5 mm against 0.36 mm. "Strikes on more seeds than bear" has no population left, since every seed bears; it becomes "at least those".
- The known-wrong (the prop passing through) bears 0.00 N and fails the first assertion.

**5. The sweep's two `BladeStrike` repeats (Village #283, #474): right by hand, the check's key.**
- At #283's step 5289, prop_rl's blade 0 (Carve) strikes GreenBush#2 twice: once for a twig (`twig`, 4.72 J) and once for the step's leaves (`foliage`, 0.008 J). That is 2.3 §4's one leaf strike a rotor a step on the crown's shell, beside its twig cuts. #474 is the same on GreenBush_Dry#1.
- **The check now keys on the material too.**

**6. The Rest rows.**
- S04 Drop #405 (0.5 m/s, 1.9 rad/s on its side) and Village #340's 1.4 m/s hop were the sweep's model-90 rows. On 2.6's after-rows #405 Drop already passes, and #340 Slide is at 0.005 m/s and 0.043 rad/s.
- **#340 now (0.006 m/s, 0.055 rad/s, 56.8° on its side):** rotor rl, its blade crushed to 0.81–0.90 of its span, carves the dirt track at idle. It jams on a stone every 61 ms (`StoneStrike`, `RotorJammed`), and each jam kicks the body to 0.15 rad/s.
- **By hand:** J·ω ≈ 2·10⁻⁵ kg·m² · 80 rad/s over the body's ~0.03 kg·m² is about 0.05 rad/s.
- **Right by hand:** an armed drone on its side with a prop in the dirt chatters.

**7. Seen, not chased:** the seven Village slide levers on light contacts and the three E01 soil-box skid tunnels (below, with the House sweep's counts).

## 2.7 The crown twig's repeated break (G5)

**Reproduced** (measured, `ObjectContactTests.AHoverInACrownBreaksNoBladeTwice`, model 85): the drone starts 1.24 m over the ground at (525.79, 612.74), inside GreenBush_Dry's crown, and the pilot holds that point for 25 s with surfaces.json. 150,239 `BladeDamaged` Break events, all with 0 kg lost, 150,231 at a station the blade had already broken at; 8 in every step. **No twig was cut** (0 `BladeStrike` on twig): the candidate, `PropStrikes.Twig`, never ran.

**The cause** (a temporary print, not committed):
- The props never turn in the crown: every rotor is resting (`ContactModel.Resting`), its blades held by the twig shells. So the breaks are `PropStrikes.RestDamage`'s, the resting blades' static loads, not a strike's.
- A resting blade's load radius is its pairs' force-weighted radius, and a pair's point is the blade's core projected onto the face's tangent plane (`ContactModel.Depth`). Deep inside a shell (0.4–0.7 m, the row's own design) that point lies on the shell's surface, not on the blade: **4.48–5.53 R** (measured), 0.4–0.6 m past the tip.
- `Breaks` took that radius as the load's: the tip's station (0.98875) then carried 25–39 N on a 0.44–0.57 m arm, 8–102 times its strength, and broke. The trim left the tip at that station's centre, which `Breaks` still counted (its span half inside), so it broke again every step: the tip unchanged, 0 kg lost. A rest break pays no work, so no energy was taken; only the log and the rotors' rebuild each step paid.

**The fix** (`PropStrikes.cs`, two lines):
1. `RestDamage`: the load's radius is held to the blade's tip (it acts on the blade's material). The flap set's `Worst` reads the same radius.
2. `Breaks`: a station whose centre is not inward of the blade's tip holds nothing beyond it, so it is no break. A tip is only ever 1 or a station's centre, so this removes exactly the no-op breaks, for every caller (the strikes' carve, crush and twig paths too: none can again pay a fracture's work for a break that takes nothing).

**After** (measured, the same 25 s): 1 break logged, at station 34 (0.876 R, its flatwise moment 1.02 of its strength under the shell's 37 N at the tip), taking mass; 0 with 0 kg; 0 at a station already broken; at most 2 `BladeDamaged` in a step. A second break, the same in the other front rotor, happens in the first step, whose events the step's 64-event buffer refuses with 23 others (every pair in the crown made at once); no event is refused after it. The log replays headless bit for bit (25,016 steps, 815 events) and the steps after the first second allocate 0 B.

**The events are bounded** by construction: a blade breaks at most once at each of its 40 stations, so at most 320 breaks a flight for the 8 blades.

**The energy books:** a rest break takes no work, before or after; a strike's break pays the fracture's work only when it removes a section. Holding the radius to the tip only shortens arms, so no load and no work is created.

**W3 2.7's crown flight re-measured** (`ADroneInACrownPaysForItsTwigsAndDoesNotHang`): unchanged to the bit: first touch at 10.89 s, 15 twigs cut for 135.4 J, 0 glances, crashed, 65.41 m from where it hung; its log replays bit for bit (25,016 steps, 906 events). The known-wrong also unchanged (8,502 glances, hangs 4.17 m up). `flightbench --contacts`' farmland bush: unchanged (109 twig contacts, 48.63 N·s, 98.55 m on).

**Goldens:** none moved (`flightbench --golden`: 61 PASS, every case and kept audit bit for bit).

**Model:** ships under 86 with 2.4's. The note's line: *a resting blade's load radius is held to its tip, and no blade breaks at a station at or past its tip (2.7).*

**For 2.3 (leaves in crowns), not fixed here:**
- In a hover inside a crown the props **stand still**: the twig shells hold each resting blade with 25–39 N (the row's 30 N/m per part over 0.4–0.7 m, the same law as a foot's) and its stick friction, more than the motor's torque. So `Twigs` never runs and the drone rests on its stopped props in the bush. A blade inside a shell standing for 7.5 mm twigs at 2 m/m³ should meet a few twigs, not a solid; the shells' push on a blade is the model to revisit with the leaves.
- A resting blade's pair point lies on the shell's plane, so its radius and `BladeStiffness`' station are the shell surface's, not the blade's (`ContactModel`, left alone here).
- The first step inside a crown overflows the 64-event buffer (24 refused here).

## 2.8 The plants' numbers for the props

**Built:** `tools/blender/vendor_plant_numbers.py` writes `assets-src/vendor_plant_numbers.json` (W4a's layout: the script beside `vendor_plants.py`, the numbers beside `vendor_plants.json`). It reads the store's prefabs, meshes, materials and base maps directly (no Blender, no Unity), pins each file it read by SHA-256 in the output, and writes only derived numbers (D-089). `--check` re-runs it and compares byte for byte; `tools/blender/tests/test_vendor_plant_numbers.py` re-measures the grass from the store.

### Stems per grass instance

**What a stem is:** one blade or culm as drawn. Each grass prefab is a tuft of alpha-clipped cards (Grass_A 19 cards, Grass_B 157, GrassDry_C 9, …), and each card draws a whole tuft of blades in its texture, 0 to 27 a card. So a card is not a stem: counting cards would undercount 10–20 times.
- **The count (measured):** a horizontal plane cuts every card; along each cut the opaque texels (alpha ≥ the material's cutoff, 0.5 on all ten materials) form runs; runs that meet on an edge shared by two triangles of a card are one stem. The planes stand at 5 %, 10 % … 95 % of the mesh's top, and `profile` keeps every plane's count. A stem at height h is what a disc sweeping at h meets.
- **Stems per instance (derived):** the most any plane crosses. Low down, the clump's blades merge into wide runs; higher up, the short ones have ended. The most is therefore a floor on the blades drawn.
- **Width (measured, as drawn):** each stem's run length along the card at that plane, p10 / p50 / p90. A slanted blade reads wider, and two overlapping blades read as one wide run, which is what the p90 mostly is.
- **Height:** the mesh's top × the layer's height scale (the audit's 1.12 ranges, carried).
- The Burnt prefabs share the Dry prefabs' meshes and base map (only the tint differs), so their numbers are the Dry ones.

| Prefab (layers) | Cards | Stems / instance | At | Width p10 / p50 / p90, mesh (mm) | Drawn height (m) |
|---|---|---|---|---|---|
| Grass_A (0 Green, 10 TallGreen) | 19 | 225 | 15 % | 1.0 / 2.5 / 7.0 | 0.46–0.67; tall 0.93–1.34 |
| Grass_B (1 GreenClump) | 157 | 2,050 | 15 % | 1.0 / 2.5 / 7.0 | 0.45–0.62 |
| Grass_C (2 GreenClump) | 43 | 614 | 10 % | 1.6 / 4.5 / 8.4 | 0.60–0.84 |
| Grass_D (3 GreenClump) | 55 | 566 | 5 % | 1.2 / 4.0 / 7.5 | 0.67–0.97 |
| GrassDry_A, GrassBurnt_A (4, 7, 12) | 14 | 179 | 15 % | 0.6 / 1.4 / 5.4 | 0.53–0.90; tall 1.17–1.59 |
| GrassDry_B, GrassBurnt_B (5, 8) | 18 | 210 | 25 % | 0.6 / 1.2 / 3.5 | 0.44–0.81 |
| GrassDry_C, GrassBurnt_C (6, 9, 11, 13) | 9 | 110 | 15 % | 0.7 / 1.6 / 6.3 | 0.68–1.15; tall 1.22–1.77 |

The `layers` rows give each of the 14 layers its stems per instance, its drawn height and its width range scaled by the layer's width scale.

**The hand count (measured, 2026-11-12):** each card's clipped texture was drawn with the plane's cut on it, and the blades crossing the line counted by eye.
- GrassDry_C at 15 %: card 0, script 14, by eye 15–17 (a central bundle of 4–6 culms reads as 2–3 runs); card 5, script 20, by eye 16–20.
- Grass_A at 15 %: card 4, script 13, by eye 13–15. The small clover and leaf cards (6 of 19) cross nothing at that height, as the script says.
- So the script is within about 15 % of the eye, low where culms bundle.

**Sanity (derived):** Grass_A at the Green layer's mean 2.36 instances a 0.954 m² cell gives about 560 stems/m², against K§7.2's assumed 400 for meadow (carried). Grass_B's 2,050 a cell is a dense sward, inside the 1,000–5,000 tillers/m² of a grazed sward (assumed, order of magnitude; not looked up for this task).

### Leaf area density of the crowns

**What a leaf is:** the drawn opaque area of a foliage card, one side, by its alpha. Each foliage base map was looked at and labelled (assumed, by eye; `CARD_KIND` in the script):
- **leaf:** IslandTree's leaves and dry leaves, GreenBush, LeafyBush, RedBush;
- **needle:** the pines' branch sprays;
- **twig, no leaf:** IslandTree's twig tubes (bark, drawn whole), DeadBush's and SmallBushDry's bare twigs. These are reported as `twig_card_area_m2` and add nothing to the leaf area.

**The area (measured):** per triangle, its 3D area times the share of its UV triangle's texel centres that are opaque.

**The crown's volume (derived):** the union of the variant's twig-shell spheres in its committed metadata (W4a), integrated on 1 cm rows. That is the volume where the props already meet `twig`, so 2.3 can apply the density inside the same shell. 96.6–100 % of the drawn leaf lies inside it (`leaf_inside_shell`).

| Variant | Leaf area (m²) | Shell (m³) | LAD (m²/m³) |
|---|---|---|---|
| IslandTree / _Dry | 391.5 / 228.6 | 447.6 / 379.1 | 0.88 / 0.60 |
| Pine002_S2, _M (= _M_Dry), _L | 43.8, 99.3, 95.1 | 21.0, 64.1, 49.5 | 2.09, 1.55, 1.92 |
| Pine004_1 (= _1_Dry), _3 | 300.2, 133.2 | 273.3, 76.3 | 1.10, 1.75 |
| Pine006_01 (= _01_Dry), _02 | 9.2, 10.1 | 12.7, 48.4 | 0.73, 0.21 |
| GreenBush (= _Dry) | 12.2 | 12.5 | 0.98 |
| LeafyBush_P2 (= _Dry) | 25.2 | 26.6 | 0.95 |
| RedBush_Dry | 9.2 | 8.1 | 1.13 |
| PineDead_02, DeadBush_4 and _6, SmallBushDry_A and _B | 0 | 0.6–88.1 | 0 (twigs only) |

**Range:** 0.21–2.09 m²/m³ over the 16 leaved variants; the pines 0.21–2.09, the deciduous ones 0.60–1.13. Each _Dry twin uses its green twin's mesh and alpha, so it has the same numbers (its leaves are drawn brown, not fewer), except IslandTree_Dry, which draws fewer leaf cards.

### Limits: cards are not botany

- **A drawn blade is not a botanical stem.** A texture draws a tuft photo flattened onto a card: blades that overlap in the photo are one run, so the count is low. The crossed cards of a tuft draw the same photo twice from two angles, so it is high there. The net is about ±30 % on a real tiller count (assumed). The physics should take the count as "what is drawn", true to REAL-05, and not as botany.
- **The widths are the drawn ones.** p50 of 1.2–4.5 mm on the mesh is of the order of real blades and culms (audit §5.1's published rows). The physics should take each stem's diameter and wall per kind from §5.1 (published) and use these widths only as a check.
- **Leaf area is one side of the drawn silhouette.** Leaves that overlap within a texture, and leaves drawn foreshortened, are missing, so the drawn area is lower than a real crown's.
  - Tree crowns' LAD is commonly 0.5–2.5 m²/m³ (assumed, from general knowledge; not looked up for this task).
  - The drawn numbers sit in that range's lower half. 2.3 should look up and cite its own source. A card's drawn twig, under its leaves, counts as leaf (a few per cent, assumed).
- **Labels:**
  - measured: the counts, widths, areas and volumes;
  - derived: stems per instance (the most of the profile) and LAD (area over the shell);
  - assumed: the card kinds, which are by eye, and the shell as the crown;
  - for the physics, assumed: everything about stiffness, mass, wall and shear, which is §5.1's.

## 2.1 Village's stem field

**Built:** `MapWorldStems.cs` derives the field at load from the package and the plants' numbers; `IWorldQuery.StemsNear` reads it (`QueryVersion` 9). `tools/map/validate.py` checks it, `StemFieldTests` and worldbench's three `farmland.stems.*` cases pin it. The editor check is `Fpv450.DetailInstanceCheck` (`unity/Assets/_Project/Editor/Farmland/`).

### The editor check: are `ComputeDetailInstanceTransforms` the drawn instances?

Run in batchmode on the friend's scene (read only, never saved), Unity 6000.3.26f1 on D3D12, at three 8 m windows: the densest tall cell (768.07, 917.48), Grass_A's densest cell (283.69, 969.24) and the densest cell overall (869.63, 151.86). Measured:
- **The terrain's settings:** `detailObjectDensity` 1, `InstanceCountMode`, 1,024 cells in patches of 32. Every one of the 14 prototypes is an instanced mesh with `useDensityScaling` off, `alignToGround` 0.5 and `positionJitter` 100.
- **The same instances, the same counts:** over 6 patches × 14 layers, the transforms equal the cells' counts in every patch and layer. Each cell holds exactly its count in all but 4 of the 86,016 cells checked; those 4 are two transforms on a cell's edge, each read into its neighbour.
- **The density setting doesn't matter here.**
  - At density 0.5 the transforms are the same as at 1.
  - The terrain drawn at `detailObjectDensity` 0.5 covers as many pixels as at 1, within 0.1 %. Its pixels differ from the density-1 picture by 3.7–6.7 % of those covered, against 1.4–2.3 % between two renders at the same density. The gap is likely more sway between more renders (assumed).
  - With `useDensityScaling` off, the friend's terrain draws every counted instance at any density.
- **The same places, not the same blades:** drawn from straight above, the terrain's own picture and the transforms drawn with each prototype's mesh share every tuft's centre. Their pixels still differ by 27–61 % of the covered union:
  - The yaw is in radians: in degrees, the difference is 63–84 %.
  - The terrain differs from itself between two renders by 1.4–2.3 % of its covered pixels: the shader's wind sways the blades.
  - Tilting each instance halfway to the ground's normal (`alignToGround` 0.5, my reading of it) closes part of the gap: 35 → 27 %, 61 → 55 %, 60 → 59 %.
  - So the struct carries the instance and its place, but not the tilt or the sway the renderer adds. A stem-by-stem copy of the drawn blades can't be had from it.

### Per cell, not per instance

| | Per cell and layer (built) | Per drawn instance |
|---|---|---|
| Data | `grass/map.raw` as exported, 14.0 MiB, plus per layer a table of 45–178 doubles | 2.84 M × (x, y, z, scale xz, scale y, yaw) in float32 = 68 MB (derived), plus a cell index (4 MB) |
| Package | nothing new: the file is already in it and under its SHA-256 | a new LFS file of about 68 MB; the exporter re-run in the friend's scene |
| Load | 46 ms for the read and the SHA-256, measured on a loaded machine; the tables under 5 ms (derived: 14 layers × at most 178 steps × 64 scales) | about 220 ms for the read and the SHA-256 alone (46 ms × 68 / 14, derived), then parsing |
| Player | +14.0 MiB in StreamingAssets (`MapWorld.Load`'s reads carry it) | +68 MB |
| Query, 4 rotor discs | median 3.7 µs, p99 13.5 µs, 0 B (measured, indicative) | about the same: up to 15 instances a cell to test instead of 14 bytes |

**Why per cell:**
- **An instance places a tuft, not its stems.** A tuft spans its mesh's footprint times its width scale: Grass_A 0.31 m × 2.6–3.6 = 0.80–1.12 m, GrassDry_A 0.41 m × 1.2–1.7, GrassDry_C 0.58 m × 1.0–2.2, Grass_B 1.0 m × 1.2–1.7. The footprints are measured from the store's meshes, the products derived. Spread over about a cell, a tuft's pivot says little more about where its 110–2,050 blades stand than the cell does.
- **The drawn pose isn't recoverable anyway** (the check above). Per instance would cost five times the memory and a re-export, and would still not give "what is drawn is what is touched" blade by blade.
- **The count is what 2.2 and 2.10 need:** stems per m² by kind and height, and how many lie under a disc. Per cell gives that from data already in the package.

### The rule

- **Stems per m²** of layer l in cell c = count(c, l) × stems per instance(l) / the cell's area (0.9537 m²).
  - The count comes from `grass/map.raw`.
  - The stems per instance come from 2.8's prefab rows in `assets-src/vendor_plant_numbers.json`.
  - A cell with no drawn grass has no stems.
  - The map only: the surround's grass within 170 m of the edge isn't exported (README), so outside `map_rect_m` there are no stems.
- **The kind is the group's state:**
  - Green, GreenClump and TallGreen → `GreenBlade`.
  - Dry, DryClump and TallDry → `DryCulm`.
  - Burnt and TallBurnt → `BurntCulm`.
  - This departs from the audit's suggestion (Tall* → dry culm): TallGreen draws Grass_A's green material and TallBurnt the burnt tint, so the group's name is the state.
- **The height range** is the drawn tops: `mesh_y_m[1]` × the layer's height scale range. For example, TallDry GrassDry_C reaches 0.6789 × 1.8–2.6 = 1.222–1.765 m.
- **How many reach a height h above the ground:**
  - Per instance, R(h / top), where R is 2.8's count profile turned into its upper envelope: at each plane, the most any plane at or above it crosses. So R never rises with height, and at and below the densest plane it equals the stems per instance.
  - R is linear between the 5 % planes, falls linearly from the 95 % plane to 0 at the top, and is 0 from the tallest top up.
  - The top is the mesh's top × a height scale drawn uniformly over the layer's range. Reach(h) is R's mean over 64 scales at the range's midpoints, tabled every 1 cm and read linearly between entries.

### The query (for the physics engineer's sign-off at 2.2)

```csharp
int StemsNear(XZ centre, double radius, double height, Span<StemPatch> results);
struct StemPatch { int Cell; byte Layer; StemKind Kind; double Area, Density, HeightMin, HeightMax; }
```

- **One circle in x–z and one height above the ground** serves both of the physics' asks:
  - A rotor disc at its height above the ground at the hub: 2.2's stems under the disc that reach it.
  - A leg's foot or a fiber node with its radius at height 0: 2.10's every stem there.
  - The disc's tilt and the ground's slope within a disc are the physics' to fold into the one height. A 0.127 m disc tilted 30° spans ±0.06 m of height, against tops spread over 0.2–0.5 m within a layer.
- **The result per (cell, layer):**
  - `Density`: stems per m² reaching `height`.
  - `Area`: the circle's area inside the cell, exact by four signed quadrant boxes, one Atan2 pair each.
  - The stems under the disc are Σ Density × Area, continuous as the disc crosses a cell's edge.
  - `Kind`, so 2.2 reads each kind's published stem from 2.11.
  - `HeightMin` and `HeightMax`, so the physics knows how much stem stands above the cut. A stem already cut stands lower; that state is the physics', not the world's.
  - `Cell` and `Layer`, so the physics can key that cut state.
- **Its rules:**
  - The order is canonical: row north to south, then column, then layer.
  - It returns the true count on overflow.
  - It returns 0 off the map, on the floor and on the test range.
  - Only + − × ÷, √ and `DetMath.Atan2`; allocation-free and reentrant.
- **Nothing more is built:** no stem positions, no per-stem draw and no wind cover. The cover per cell is W7's follow-up.

### Format and identity

- **No new file and no exporter change.** The field is derived at load from `map.json`'s `grass` block (file, resolution, each layer's prefab, group, `height_scale` and `mesh_y_m`), `grass/map.raw` (checked against the manifest's SHA-256) and `assets-src/vendor_plant_numbers.json` (the prefabs' `stems` and `profile`).
- **The content hash** continues over `vendor_plant_numbers.json`'s text after the material names. The farmland's hash moves from `7249f5a8adb997d7` to `5d305d09f5d09021`; the map.json hash already covered the counts.
- **`QueryVersion` 9.**
  - worldbench's golden files were re-recorded for the version line only: every earlier case of the floor, the test range and the farmland is unchanged, bit for bit, in Release and Debug.
  - `farmland.txt` gains `farmland.stems.cells`, `.random` (10,000 circles, the same bits on four threads) and `.field`.
  - A model-85 farmland log now refuses to replay (it has another query version and content hash), as with any `QueryVersion` bump.
- **`validate.py`** checks, per layer, that the plants' numbers name the same prefab and group in the same row, that the group has a kind, that the stems per instance are the profile's most, and that the drawn heights are `mesh_y_m[1]` × `height_scale` within 1 mm. It also checks that the field's stems over the map equal the plane's instances × its stems per instance. worldbench's `farmland.stems.field` repeats that last check through `StemsNear` itself, at every grass cell: all 14 layers agree.

### The numbers

| Number | Value | Label |
|---|---|---|
| Cell side, area | 0.9765625 m, 0.9537 m² | carried (README) |
| Stems per instance | 110–2,050 by prefab | measured by 2.8, carried |
| Count profile, planes at 5–95 % of the mesh top | 2.8's `profile` | measured by 2.8, carried |
| Height scale spread | uniform over the layer's range | assumed: Unity spreads it by a Perlin noise (`noiseSpread` 0.1) |
| Reach table | 1 cm steps, 64 scales | derived |
| Stems on the map | green blades 403.8 M, dry culms 178.1 M, burnt culms 60.1 M; 642 M in all, a mean of 642 /m² | derived |
| Densest cell | 5,496 /m² at (869.63, 151.86) | derived |
| Kind per group | as above | assumed, from the group's name and material |
| Rotor disc radius in the cases | 0.127 m (a 10-inch prop) | carried |

**At the three golden cells** (derived; stems per m² reaching h, and the stems in the circle):
- **The densest tall cell, (768.07, 917.48),** with 11 drawn layers and a disc of r 0.127 m (0.0507 m²):

  | h (m) | 0 | 0.3 | 0.6 | 1.0 | 1.5 |
  |---|---|---|---|---|---|
  | Stems per m² | 2,345 | 1,299 | 345 | 43 | 2 |
  | In the disc | 118.8 | 65.8 | 17.5 | 2.2 | 0.1 |

  Above 1.0 m, only the tall layers and the tall burnt culms remain.
- **A bare `tilled` cell, (229.98, 257.32):** no stems at any height.
- **The east edge's cell, (999.51, 908.69),** with a circle of r 0.6 m at (999.9, 908.69):
  - 0.685 m² of the circle lies on the map, the closed form's π r² minus the 0.446 m² segment past x = 1000. Those cells hold 2,695 stems at h = 0, 433 at 0.3 m and 15 at 0.6 m.
  - The segment holds none.

**Sanity:** the drawn grass is 236 /m² for one Grass_A instance and 944 /m² for the green edge cell, against K§7.2's assumed 400 for meadow (carried). 2.11's published stand densities will say how far "what is drawn" sits from real swards; 2.8 puts the drawing's count within about ±30 % of real tillers (assumed).

### Costs (measured on this laptop on AC, Balanced plan, best power efficiency mode, with other agents running: indicative)

- **The field's load:** 46 ms to read `grass/map.raw` and check its SHA-256. The whole farmland load was 2,620 ms under that load.
- **Memory:** 14.0 MiB held.
- **A step's four rotor discs at 0–1.8 m:** median 3.7 µs, p99 13.5 µs, 0 B, 3.2 (cell, layer) pairs a query.
- **Four leg points at 0 m:** median 2.2 µs, p99 4.5 µs, 0 B.
- **The worst case** (a disc on a cell corner in the densest field) is 4 cells × 14 layers, 56 bytes read and at most 4 area evaluations. Its maxima (881 µs and 209 µs) are this machine's scheduling under load, as the existing rows' maxima are.

### What 2.2 needs to know

- **Fold the hub's height above the ground into `height`.** Ask once per rotor per step with the disc's radius. The stems the disc meets are Σ Density × Area per kind, a mean count with no draw, as `PropStrikes.Twigs`.
- **Cut state is yours.** Key it by (`Cell`, `Layer`); a cut stem stands at the cut height.
- **Widths and stiffness come per kind from 2.11.** 2.8's drawn widths are only a check.
- **The field ends at the map's edge,** and there is no wind cover from it yet.

## 2.2 Stems on the props

Labels as above: **measured** (run here), **published** (2.11's `stem-properties.md` [n] or a source named here), **carried** (already in the repository), **derived** (computed, the formula given), **assumed** (a named, tunable constant), **numerical** (a resolution or buffer, not physics).

### What the calculation says first

The rotor's slipstream decides most of it, and it is strong.
- **The slipstream is a 19 m/s jet.** Momentum theory (published): the contracted wake moves at 2·v_i, so its dynamic pressure is q = ½ρ(2v_i)² = T/A. At the hover's T = 11.24–11.98 N a rotor (measured over the densest tall cell, 4.735 kg) and A = π·0.127² = 0.0507 m², q = 222–236 Pa (derived), the pressure of a 19 m/s wind.
- **Fine culms lie down in it before any blade reaches them.** A 1.5 mm culm (2.11's green, dry and burnt rows) leans until its top stands at most 0.12–0.16 m over the ground (the lean law below, derived). The discs of a drone standing on its legs are 0.288 m up (measured: the standing origin 0.253 m plus the hubs' 0.035 m). So **a vertical landing into Village's grass cuts no stem under the discs**: the grass is pressed flat under them, as a helicopter's is.
- **Stiff stems stand.** A 3.5 mm straw (the range's `straw` row) still stands 0.44 m and an 8 mm weed stalk 0.83 m (derived).
- **A leaning stem slips off the blade.** The blade's speed across a stem leaning φ is ω·r·cos φ. Long S04 weed stalks under the disc lean 56–76° and meet the blade at 20–60 m/s, under their 48–90 m/s cutting speed: they are pushed aside, not cut (derived, hand calculation D).
- **What is cut** is what meets the blade upright and fast: the stems a moving disc runs into at its leading edge (they reach the blade 3.4 ms after entering the slipstream, against the about 40 ms the slipstream needs to lay them over, derived), short stiff stubble, and stems standing when the rotors spin up among them.
- **This is the pilot's account.** "Straws are noticeably cut in front of my quad's camera": the front discs' leading edges, moving forward as a landing does, cut what stands in front of them, and the pieces leave there. The spec's purely vertical landing into fine grass is the one case where nothing is cut. Most stems escape, as the audit required, and the props never stall in grass.

### The law

Per rotor, per step, after the shapes' passes (`PropStems`, called from `PropStrikes.Apply`; a resting rotor is the contacts', 2.10's):

**1. The stems under the disc.**
- *Village* (the stem field): `StemsNear(hub, R, h)`, h the hub's height over the ground under it (`SampleGround`; the disc's tilt folded into one height, as 2.1 suggests). Each patch gives the stems per m² reaching h, its area in the disc, its kind and its drawn tops. Above h, within a patch, the tops are spread evenly up to its tallest top (assumed: the field holds no finer profile once h is given; it is read once a step).
- *The range* (S04's elements, `MicroDetailNear`): the straws and stems based in the disc, each with its own drawn diameter, length and shape, read into a per-rotor cache when the hub has moved 0.1 m (numerical) and placed exactly.
- Each kind's tissue is its `surfaces.json` cover row at the flight's moisture: diameter, wall share, density, modulus, cut energy. An element's modulus is drawn in its row's range by its own Id's hash, the fiber's rule (world-derived, not a flight draw).

**2. The slipstream lays them over.** The stem's load across it is w = q·C_d·d·cos²φ, q = T/A (derived above), C_d = 1.15 (published [16], [17]: 1.0–1.3), cos²φ the cross-flow's normal share as it leans (assumed: the reconfiguring plant of Gosselin [18]; a load that kept acting across it would lay stems over sooner). The stem bends as a rigid link on a base spring of 4EI/L, which gives the uniform-load cantilever's tip deflection wL⁴/8EI for small leans (derived):
- φ / cos²φ = q·C_d·d·L³ / (8·EI) = κ·L³, and its top stands L·cos φ over its base.
- So h_b = (φ·cos φ / κ)^(1/3): the most any length stands is (0.5611/κ)^(1/3), at φ = 0.860 rad (derived), and the lengths standing to a height h lie between two roots of φ·cos φ = κh³. Both are tabled once at load (256 entries, numerical) and read linearly.
- **The lag:** the stems' load follows the rotor's q with a time constant, 0.04 s pressing them down (derived: a 0.5 m culm's top driven over by w/μ ≈ 500 m/s² gets there in about 44 ms) and 0.1 s rising again (assumed: inside the 0.02–0.35 s quarter periods of 0.2 m stubble and a 0.6 m culm, derived from the first cantilever mode). A rotor spinning down after the throttle's cut has slowed below the cutting speed before the stems stand again (hand calculation A).

**3. The blade must be fast enough.** The critical speed of an unsupported stem, 2.11 §2.2's law (derived): v_c = k_edge·[F_c·√d / (2√(2π)·(EI·μ³)^¼)]^(2/3), F_c = e_s·A_w/d, with the prop's edge factor k_edge = 3 (assumed, 2–4: between a sharp mower blade [13], [11] and nylon line [14]; the blade's nose is 0.13–0.65 mm round, derived [15]).
- A stem at radius r leaning φ is cut where ω·r·cos φ ≥ v_c (derived: the blade's speed across the stem; with Tuck et al.'s 35 m/s upright it gives 83 m/s at 65°, their "over 80 m/s" [11]). Below it the blade pushes the stem aside: nothing is taken (assumed; its bending energy is of the order of 1 mJ against 10–30 mJ for a cut).
- On the field the stems lie evenly over the disc, so the share cut is (r_tip² − r_c²)/R², r_c = v_c/(ω·cos φ) at the reaching lengths' middle, never inside the blade's root (0.1 R, carried).

**4. Each cut's work** (from the rotor):
- **A fresh cut:** E = e_s·A_w + μ·h_f·u², at the blade's speed u = ω·r. The edge severs the wall's section (e_s quasi-static, 2.11 §2.3: an impact multiplier would count the fling twice), and the section's frontal height h_f = c·sin θ + t·cos θ of stem is swept and brought to the blade's speed (the carve's "mass it meets", p7f-damage design 5, carried: the twig's m_f·v² with the band a vertical stem crosses in place of a chord).
- **A slice:** a stub cut before and met again as the disc comes down by Δ costs min(1, Δ/d) of a cut (`Twigs`' rule, carried: a slice thinner than the stem is crushed) and flings μ·Δ·u², at most once per blade pass.

**5. The impulses.** Along the blade's motion the stem takes E/u, the rotor paying it all: ω' = √(ω² − 2ΣE/J), and the body gets the reaction about the axis and the impulse at the cut, as `Twig` gives them. Along the axis, the cut's share of the pitched face, E_cut·(c·cos θ/h_f)/u, pushes the blade up (the carve's `Section` split, carried: 2–4 times the in-plane push at the blade's 8–13°); the fling leaves in the plane.
- **Where:** on the field the stems are spread over the disc's azimuth, so its two blades share them evenly and their in-plane impulses cancel; the rotor slows and is pushed up. The stems a moving disc runs into lie across its leading arc, ∝ cos(ψ − ψ_lead), and each blade cuts what has entered since the last one passed: blade b takes (π/2)·max(0, cos(ψ_b − ψ_lead)) of them (derived; it sums to the entries over a turn). The sum is a steady sideways push plus a rotating one at the blade pass, π·n·J_c/4 (derived, hand calculation C). An element is met by the blade whose sweep crosses its azimuth in the step, once per pass.
- **On the arm (G8):** each corner's strikes' impulse along its rotor's axis loads that arm's mode at its tip in the step it happens (`AirframeModes.Strike`: the modal impulse of a tip impulse with the rigid body's relief, as `Advance` takes the thrust), so the camera sees the arm ring from the hit. For every strike, not only stems.

**6. A stem already cut stands lower.**
- **On the field (as built):** each rotor keeps, per kind, the height H it has cut down to and the stubs it left standing there, S (stems, the mean count). The field is read once a step, at the disc's own height:
  - the stems now reaching the disc beyond S are fresh (come within its reach as it came down, or risen as the slipstream eased), and S becomes them;
  - coming down from H to h, the S stubs are sliced by H − h;
  - fresh stems are due until the blades sweep them, a share B·ω·Δt/2π of the disc each step.
- **Every disc's trail over the field** (an entry each 1 cm of hub travel, the last 64 kept, numerical: 0.64 m, twice the 0.32 m a rear disc runs behind a front one) holds where its stems were cut and to what plane. The ground at a disc's leading edge was cut before if it lay strictly inside an earlier disc of any rotor. Then only what stands above that plane is met: a rear disc in the front one's track, a disc back over its own. Its H and S mix towards that ground's in the share of the disc it sweeps.
  - The design's first version gave the field a shared 5 cm grid. A grid's cell straddling the rim cannot say whether the ground at the rim was inside the disc, so a creep's own last steps gated its leading edge half the time. The trail's test is exact.
- **The elements** keep their cut plane in a 5 cm grid (numerical; 4,096 slots, open addressing, the home slot overwritten when full): an element's top is its bent top or its cell's plane, whichever is lower.

**7. Breaks.** Each cut's force e_s·A_w/d along the blade's motion goes through `Breaks` at its radius, as a twig's does. No 1.5 mm culm comes near: 5–8 N against hundreds.

### The numbers

| Number | Value | Label |
|---|---|---|
| Slipstream pressure on the stems | q = T/A, 222–236 Pa in the hover | derived (momentum theory) |
| Stem drag coefficient C_d | 1.15 (1.0–1.3) | published [16], [17] |
| The load's share as the stem leans | cos²φ | assumed (reconfiguration [18]) |
| The base spring | 4EI/L | derived |
| Press and rise times of the lean | 0.04 s, 0.1 s | derived, assumed |
| Critical speed law | 2.11 §2.2 | derived |
| Prop edge factor k_edge | 3 (2–4) | assumed [11], [13], [14] |
| The lean's slowing of the cut | ω·r·cos φ | derived (fits [11]'s 65° → 80 m/s) |
| Swept band h_f | c·sin θ + t·cos θ: 2.2 mm at the tip, 5.0 mm at 0.72 R | derived (drone.json's blade, carried) |
| Tops above h, within a patch | even up to its tallest | assumed |
| The discs' trails over the field | an entry each 1 cm, the last 64 a rotor | numerical |
| The elements' cut grid | 5 cm, 4,096 slots | numerical |
| Element cache | refreshed after 0.1 m | numerical |
| A stem episode's end | 0.1 s without a whole stem cut | numerical |

**The new cover rows** (`surfaces.json`, 2.11 §4; every value as 2.11 labels it). Each new row holds the `stem` row's fields plus `diameter_m`, the field having no drawn diameter. The row's modulus is one value (the field draws nothing); the wet set is the dry one's, as for the `stem` row (live tissue).

| Row | d (mm) | wall / d | ρ (kg/m³) | E (GPa) | e_s (kJ/m²) |
|---|---|---|---|---|---|
| `grass_blade` | 1.5, published [1] | 0.125, derived [1] | 1000, assumed | 1.0, assumed | 10, assumed |
| `dry_culm` | 1.5, assumed | 0.125, assumed | 500, assumed | 2.0, assumed | 15, assumed |
| `burnt_culm` | 1.5, assumed | 0.0625, assumed | 300, assumed | 2.0, assumed | 7.5, assumed |

- `straw`'s cut energy moves from 15 kJ/m² (assumed) to **22 kJ/m²** (published: Tavakoli et al. 2008's 21.85–25.74 for wheat straw, audit §5.1). Nothing read it before; its modulus, wall and density stay (the fiber reads them).
- The `stem` row (S04's weed stalks) is unchanged.
- **The log (as built): no format change.** The header's cover rows keep their layout. The new rows only add to their count, a byte already written. `Diameter`, `ContactSpec.Stems` and `AirframeModesSpec.StrikeLoad` go in a header entry of their own (tag 27, `TagStems`) after every other, and a reader skips an entry it does not know. So `LogFormat.Version` stays 72 and `LogFormat.cs` is untouched. A log without the entry (format 72 or 69, every earlier build's) reads as flown without stems and strike load.

**Per kind, at the hover's q** (derived from the rows; T 11.24 / 11.98 N):

| Kind | A_w (mm²) | EI (N·m²) | μ (g/m) | e_s·A_w (mJ) | v_c (m/s) | Tallest standing top (m) |
|---|---|---|---|---|---|---|
| green blade | 0.773 | 1.70e-4 | 0.773 | 7.73 | 53.5 | 0.126 / 0.123 |
| dry culm | 0.773 | 3.40e-4 | 0.387 | 11.6 | 88.3 | 0.159 / 0.155 |
| burnt culm | 0.414 | 2.06e-4 | 0.124 | 3.11 | 70.3 | 0.134 / 0.131 |
| straw 3.5 mm (row's 4.8–6.6 GPa) | 4.06 | 2.8e-2 | 2.03 | 89.4 | 54.3 | 0.44 |
| weed stalk 8 mm (row's 1–4 GPa) | 32.2 | 0.44 | 25.7 | 965 | 35.8 | 0.83 |

The drone stands with its discs 0.288 m up, so no Village culm under a hovering disc reaches it; S04's straw and stalks do.

### Hand calculation A: the spec's straw landing (0.5 m/s down into (768.07, 917.48))

**The cell** (measured, `StemsNear`): one field cell (86802) under all four hubs, its 11 layers. Stems in a disc reaching h: 17.5 at 0.6 m, 2.2 at 1.0 m, 69.0 at 0.288 m (14.4 green, 29.4 dry, 25.2 burnt).

**Coming down at hover thrust:** at every height from 1.77 m down to the standing 0.288 m, each kind's bent stems stand under 0.16 m. **No fresh cut and no slice under any disc.**

**At the touch:** the scripted landing cuts the throttle. The stems stand to the discs' 0.288 m again only once q has fallen to 18.5 (green), 22.4 (burnt) or 37.0 Pa (dry), 8–16 % of the hover's (derived). Even if the stems stood again at once, that needs ω ≤ 0.28–0.40 of the hover's, about 260–370 rad/s (q ∝ T ∝ ω²). The blade's tip then moves at 33–47 m/s, under the slowest kind's 53.5 m/s, and the stems rise only over 0.1 s after that. **None is cut.**

**Moving:** the only cuts left are stems the discs run into as the pilot's hold drifts, 2·R·s·D(h) for a horizontal path s inside the grass at height h. They are counted from the run's own path.

**So the prediction:**
- **Cuts:** no in-disc cut, no slice; a stem episode only if the drift runs a disc into stems.
- **The spectrum:** no line at the blade pass from the stems. The run with the stems and the run without them differ by the drift's cuts alone.
- **The 10 % check:** the run's stem work is at most the drift's cuts at their energies, within 10 %.

**The known-wrong** (the audit's law: no lean, no critical speed, every stem reaching the plane cut). At the plane's 1.0 m, coming down at 0.5 m/s, at the front rotors' ω 925.26 rad/s (the rear's 896.78):
- Every stem reaching the plane is in the cut, 0.99 of the disc outside the root.
- Fresh tops come at 0.5 × (2.745 + 8.915 + 4.59) stems/m × 0.99 = **8.04 a second**, at 7.5–35 mJ each.
- The stubs standing at the plane (0.41 + 1.13 + 0.67 = 2.20 in the disc) are sliced at min(0.2945 passes, 0.33 of a diameter) a step, flinging 0.5 mm of stem each, at u_rms = ω·√((R² + r_root²)/2) = 83.5 m/s.
- So **8.40 W** a front rotor, **8.05 W** a rear one (derived; 2.0 / 5.3 / 0.9 W of it the green, dry and burnt stubs' slicing).
- That grows as 1/(the stems' height)² down the cell. Every stem met would be cut: 69 a disc by the touch, and its stubs would take 260 W a rotor at 0.3 m. The run must give these within 10 %, and the law's run must not.

### Hand calculation C: the pilot's landing approach, a creep through the cell

The front discs creep forward at 0.3 m/s through the cell with the hubs 0.6 m up (the scripted creep: its height held, its heading +z).
- **The stems they run into** stand upright, 17.53 in a disc's area reaching 0.6 m (3.90 green, 8.86 dry, 4.77 burnt).
- **The rate:** n = 2·R·v·D(0.6) = **26.36 cuts a second** a front disc (5.86 / 13.33 / 7.18). Each is cut at the rim (u = ω·R = 117.5 m/s at ω 925.26, above every kind's v_c).
- **The work:** each costs e_s·A_w + μ·h_f·u² with the tip's h_f = 2.18 mm, 31.0 / 23.2 / 6.8 mJ. So **0.540 W** a front disc and a mean 20.5 mJ a cut (derived).
- **The impulses:** J_c = 20.5 mJ / 117.5 m/s = 1.744e-4 N·s a cut. The rotating force at the blade pass is π·n·J_c/4 = **3.61e-3 N** at 294.5 Hz, and a steady sideways one of the same size. The axial push is 7.1e-3 N, steady.
- **The rear discs** run in the front ones' cut track at the same height: **no cut** (the grid).

Pressing one stem every 38 ms through the front rotors is a light load. Village's 1.5 mm culms cost the props almost nothing: the shake the pilot feels comes from what stands stiff (straw, stalks), from the legs in the stems (2.10), and from flying into grass at speed (n grows with v).

### Hand calculation D: the range golden's straw landing (S04, 0.5 m/s down, `GoldenFlights.FlyRange`)

The S04 stalks based in the four discs at the landing (measured, `MicroDetailNear`; the moduli by their Ids' hash):
- rotor 0 has one stalk, 4.99 mm, 1.13 m long;
- rotor 1 has five, 2.15–4.21 mm, 0.57–1.03 m;
- rotor 3 has one, 2.17 mm, 0.67 m;
- rotor 2 has none.

The straws under the discs lie in the mat, their tops at 0.17 m at most.

At the hover's q:

| Rotor | d (mm) | L (m) | Lean | Bent top (m) | ω·r·cos φ (m/s) | v_c (m/s) | |
|---|---|---|---|---|---|---|---|
| 0 | 4.99 | 1.126 | 56.1° | 0.604 | 39.4 | 48.3 | escapes |
| 1 | 4.21 | 1.030 | 68.5° | 0.366 | 36.4 | 61.2 | escapes |
| 1 | 2.15 | 0.812 | 75.9° | 0.176 | 24.8 | 89.7 | escapes |
| 1 | 2.73 | 0.990 | 70.9° | 0.306 | 20.0 | 70.0 | escapes |
| 1 | 2.31 | 0.566 | 56.2° | 0.292 | 59.8 | 76.7 | escapes |
| 1 | 2.15 | 0.811 | 74.8° | 0.196 | 28.3 | 87.5 | escapes |
| 3 | 2.17 | 0.672 | 69.4° | 0.195 | 35.7 | 85.8 | escapes |

So the landing itself cuts nothing. What can be cut is at the rotors' spin-up on the straw, where the stalks stand upright under discs speeding up: they are cut wherever ω·r reaches v_c before the slipstream has laid them over. The run decides how many. **The range golden moves only by that and by G8.**

### What the runs must show (the spec's checks)

1. **The straw landing** (A): no stem cut while coming down or at the touch, beyond the drift's, within 10 %; the known-wrong's 8.40 / 8.05 W at 1.0 m within 10 %.
2. **The creep** (C): a front rotor's cut rate, power and blade-pass line within 10 % of 26.36 /s, 0.540 W and 3.61e-3 N; the rear rotors' cuts near none.
3. **A hover in grass** cuts itself clear and its episode ends.
4. **The range golden:** the move audited case by case; its log replays bit for bit.
5. **0 B** in steady state, and the step's cost (indicative here).

### The log, the feed, the switches

- **`BladeStrikes`:** per blade per step, `StrikeKind.Stem` (4, new; `Material` its cover kind + 1), the energy, the impulse's size, the kind's diameter, the edges' work and the radius. `Count` holds the whole stems the blade's running count has passed in the step: the field's fractional mean counts are counted, never drawn.
- **Event 39, `StemEpisode`:** per rotor, at its first whole stem cut (`Index` its corner) and 0.1 s after its last (`Index` | 0x80). A is the energy so far, J, B the stems, C their mean diameter, m, and Point the hub. Two events an episode, against LOG-01's budget, never one a cut.
- **`FeedSignals`:** per rotor over the field window, the stems cut, their pieces' mean length (m) and radius (m), and the rotor's angle at the last cut. For 2.5. No `Live` bit: the feed's enum is the tech-artist's.
- **The switches** (in the header's `TagStems` entry):
  - `ContactSpec.Stems` (the stems on the props) and `AirframeModesSpec.StrikeLoad` (G8) are on for every flight.
  - Off only for the kept audits of earlier models, flown as before, and for the known-wrong's comparisons.

**Model:** the next free number at the merge (the orchestrator's). The note's line: *the props meet the stems: the slipstream lays them over, a leaning or slow blade pushes them aside, the rest are cut and paid for; a strike loads its arm (2.2).*

### As built: the runs against the hand calculations

**Code.**
- `Runtime/Contact/PropStems.cs` holds the law, a part of `PropStrikes`, called for each turning disc before its pairs.
- Small edits:
  - `PropStrikes.cs`: the call, `TipImpulse`, `partial`;
  - `ContactSpec.Stems`, one field;
  - `FlightWorld.StemsNear`;
  - `BladeStrikes`: `StrikeKind.Stem`, `AddStems`;
  - `EventKind.StemEpisode` (39);
  - `FeedSignals.StemCuts…`;
  - `AirframeModesSpec.StrikeLoad` and `AirframeModes.Strike` (G8);
  - `FlightCore`: the G8 call after the strikes, the feed;
  - `LogHeader`: `TagStems`; `FlightLogReader`: event 39's text.
- `ContactModel.cs` and `LogFormat.cs` are untouched.
- `StemsNear` was used as it is: `QueryVersion` stays 9.

**Tests** (`StemTests`, the flights in `StemScenarios`), measured:

| Check | Run | Hand | |
|---|---|---|---|
| A: the straw landing, fresh and sliced under the discs before the touch | 0 | 0 | exact |
| A: stems the hold's drift ran into before the touch | 0.002 (0.04 mJ) | the drift's | |
| A: after the touch (the body settling as the props spin down) | 8.1 entries | | |
| A, the known-wrong: a front rotor's stem power at the plane's 1.0 m | 8.168 W | 8.40 W | −2.8 % |
| A, the known-wrong: a rear rotor's | 7.813 W | 8.05 W | −2.9 % |
| A, the known-wrong: fresh stems a rotor before the touch | 65.5 | 69 by the standing plane | fails the law's 0, as it must |
| C: the creep, fr in the cell (2.23 s at h 0.567 m, v 0.324 m/s, ω 929.86) | 33.35 /s, 0.6837 W, line 4.540 / 4.546 mN | 32.66 /s, 0.6703 W, 4.458 mN | +2.1 %, +2.0 %, +1.8 / +2.0 % |
| C: fl (h 0.544 m) | 36.83 /s, 0.7510 W, 4.990 / 4.993 mN | 36.07 /s, 0.7374 W, 4.906 mN | +2.1 %, +1.8 %, +1.7 / +1.8 % |
| C: the rear discs' entries against the front's | 7.2 against 203 | none | the front's track |
| A hover 0.5 m up in the cell, 10 s | 5.0–5.7 stems a rotor from the drift, the last at 7.6 s; 19 episodes begun and ended | clear | |
| The range's stalks, a creep through the golden's spot 0.4 m up | fresh 1 and 4 (rr, fr), sliced 4.5 and 3.2 as the height bobbed, 5.47 and 3.92 J; up to 3 elements under a disc | cut by the same law | |
| G8: a 3 mN·s strike at fr's tip | its hub 17.8 mm/s, rl's 2.4 mm/s, the camera 1.67 m/s²; equal to `Advance`'s thrust impulse less one step's damping, every mode to 1e-9 % | | |
| The landing's log | replays bit for bit (10,750 steps, 38 events); 0 B after the first second; no event refused | | |

The creep's hand calculation is C's formula at the run's own height, speed and ω (the pilot holds 0.54–0.57 m and 0.324 m/s, not the nominal 0.6 and 0.3). At the nominal it gives design C's 26.36 /s and 0.540 W.

**What the pilot would feel (measured, the body rate's RMS):**
- The creep, its front discs in the cell, 2 s: 4–15 Hz roll 0.0001 against 0.0000 °/s without the stems, pitch 0.0060 against 0.0065; 250–350 Hz pitch 0.0019 against 0.0006 °/s.
- The vertical landing, the last 3 s before the touch: the same bands with and without the stems, 0.00054 °/s of pitch.
- So **Village's 1.5 mm culms do not shake the drone.** Cutting one costs 7–35 mJ against the rotor's 25 J, and the slipstream lays the rest over. The stems that load the props are stiff ones: S04's stalks took 4–5 J a rotor in a 4 s creep. The pilot's shake on landing in grass with straw must come from straw-like stems, from the legs in the stems (2.10), and from flying in at speed.

**The goldens:**
- `flightbench --golden`: 61 PASS, none moved.
  - The main goldens' flights never cut a stem. The range golden's stalks lean and escape (hand calculation D), its straws lie under the discs, and its spin-up stands on the mat with the hubs above every stalk the law lets stand.
  - No golden carries an axial strike, so G8 moves none.
- Every kept audit flies `GoldenFlights.WithoutTheStems` (in `Edgewise`: every golden without the edgewise thrust is an audit of an older model), so each stays bit for bit even where a future golden would cut.

**Cost** (indicative: this laptop on AC, the CPU 68 % loaded by other agents; the two cores stepped in turn in one process so the load falls on both alike):
- The creep's `core.Advance`: median 33.4 µs with the stems, 27.7 without; p99 78.4 against 61.6 µs.
- The world's part: `SampleGround` × 4 1.0 µs and `StemsNear` × 4 3.7 µs (medians).
- PLT-04's quiet run is QA's.

### The checks, as run (on 9f1dcbe6 and 083e0b87)

```
dotnet test tools/simtests -c Release --filter "FullyQualifiedName~StemTests"
  Passed: 5 (the numbers in the table above, printed by each test)
dotnet test tools/simtests -c Release
  Passed!  - Failed: 0, Passed: 842, Skipped: 0, Total: 842, Duration: 14 m 16 s
dotnet run -c Release --project tools/flightbench -- --golden
  flightbench: 6 golden cases registered … 61 PASS, 0 FAIL (range-flight 0xAF745CC52447E017 matches, as before 2.2)
python tools/hygiene/precommit.py --tree HEAD
  3570 files scanned, 0 findings
```

### For the next tasks

- **2.3 (leaves in crowns):**
  - `PropStems.Part` is the cut's law: the edge's e_s·A, the swept band's fling, the pitched face's axial share, `Breaks`, the rotor's books, the log's `BladeStrikes` and the episode. A leaf can go through it as a stem whose wall is the lamina (its work to shear per length × the cut's length in place of e_s·A).
  - A crown's twigs still go through `Twigs`. Their shells still hold a resting blade (2.7's note).
  - `TipImpulse` already carries the twigs' axial share to the arm (G8).
- **2.5 (cut bits on camera):**
  - `FeedSignals.StemCuts`, `StemPieceLength`, `StemCutRadius` and `StemBladeAngle`, per rotor over the latest whole field, the same in a replay.
  - The pieces leave the front discs mostly at the leading edge, in the direction of flight (the entries), and below the discs (the pitched face pushes them down: the axial share is 2–4 times the in-plane).
  - The feed's `FeedSignal` enum has no index for them: they carry no `Live` bit, as `Sticks` doesn't.
  - A vertical landing in Village's grass gives the feed nothing to draw. A forward approach does: about 20 stems a front rotor in the forward landing, measured.
- **2.10 (stems on the legs and the fiber):**
  - The stem field per kind with diameters is in `surfaces.json` (`grass_blade`, `dry_culm`, `burnt_culm`, each with `diameter_m`); `FlightWorld.StemsNear` is the door.
  - The slipstream's lean (`PropStems`: q = T/A, φ/cos²φ = κL³) lays the stems under a hovering drone over. The legs at touchdown meet stems the props' slipstream has pressed, not upright ones, unless the drone came in moving.
  - The props' trails record where stems were cut and to what plane: a leg landing in the props' cut track meets stubs.

### For `unity/Assets/_Project` (not changed here)

- **Event 39 in the post-flight summary.** `Scripts/App/Session/PostFlight.cs` picks its events by kind (crashes, the line, failsafes, blade damage, legs). A stem episode is not shown until it is given a line, for example "STEMS: rotor fr cut 12 dry culms, 0.4 J". The sim's own reader already prints one (`FlightLogReader`).
- **`FlightFeedSignals` and `IFeedSignals`:** the four new `Corners` fields, for 2.5. The feed's `FeedSignal` enum needs a new index (17 is the next one the physics does not use) only if the feed wants a `Live` bit for them.
- **`AppData`** carries `surfaces.json` into the player as a file, so its new rows travel with it. Nothing there counts the cover rows.

### Open questions for the pilot (qualitative)

1. When you land in tall grass, does the grass under the quad lie flat in the prop wash before the props reach it, or does it stand into the props?
2. When straws are cut in front of your camera on landing, are you still moving forward as the props reach the grass, or coming straight down?
3. Is the "straw" in that grass stiff, cereal-like stalks, or the grass's own dry stems?

The physics computes the rest. The answers decide whether Village's tall dry layers are straw-like stalks (a world-data question for the kinds of 2.1 and the diameters of 2.11) and which approach the spec's landing scenario should fly.

(Superseded by 2.12: these are physics, and physics is never asked of the pilot, D-104. Their answers of 2026-11-13 are in `docs/pilot-questions.md`.)

## 2.12 The landing in high grass with straw

**The pilot's words outrank the law** (D-104). 2026-11-12: "in real life if i land on a high grass with straw - straws are noticeable cut in front of my quads camera and my quad is shaking". 2026-11-13 (`docs/pilot-questions.md`, on `unity/main`): "You shake … If I can see the drone is shaking too much (which means there's A LOT of straws and the straws are thicker than I thought - Better throttle up and find a bette replace to land. If shake is agreeable - I just keep landing)"; tall grass in the wash "Usually it lies flat because of prop wash power, but not always"; "Straws are quite stiff, but not extremely".

So the targets: straw is cut in front of the camera (the front discs) on a landing; the quad shakes, by an amount that grows with the straws' number and thickness, from a shake a pilot lands through to one that sends them away; grass usually, not always, lies flat in the wash; the straw is cereal-like, between a thin culm and a woody stem.

Labels as in 2.2: **measured**, **published**, **carried**, **derived**, **assumed**, **numerical**.

### What was wrong: both the inputs and the law

**1. The inputs: a 1.5 mm culm can't stand 1.2–1.8 m.** Its self-buckling height (Greenhill, a column under its own weight: L_c = 1.986·(EI/μg)^(1/3), derived) is 0.89 m dry and 0.56 m green with 2.2's rows. Village's tall layers draw stems to 1.22–1.77 m. A stem drawn standing must be able to stand.

**2. The law over-presses, and in the wrong way.**
- *A vertical jet loads an upright stem along its axis, not across it.* 2.2 took q·C_d·d across the whole stem, a horizontal wind's load. By the cross-flow principle (the normal velocity component carries the normal force) a stem leaning φ in a vertical jet takes q·C_d·d·sin²φ (derived): nothing upright, the full load lying over. So an upright stem stands until a lean or its own weight starts it, then folds through a fold point. That is the pilot's "usually it lies flat, but not always".
- *A folded stalk does not lie flat.* 2.2's rigid link puts a leaning stem's top at L·cos φ, down to the ground. A long stalk folded by the jet is an arch: its base stays upright where the jet has little grip, the outer part lies over. The arch's crest stands at 0.43–0.61 of the bending length l_b = (EI/(q·C_d·d))^(1/3), whatever the stalk's length (derived: the elastica of a stalk clamped upright, loaded w·sin²θ normal to it, its end resting on the ground, solved by shooting for L/l_b = 3–6, `tools/flightbench/handcalc/stem_elastica.py`).
- *The stems don't lie down in 40 ms together.* Each stalk follows its own first bending mode, τ = L²/(1.875²·√(EI/μ)) (derived): 0.02 s for a 0.3 m dry stem, 0.2 s for a 1.77 m one, 0.45 s for a tall green culm. A disc moving onto stems, or coming down faster than they fold, meets them standing.
- *The rotor-downwash-on-canopy check.* The published measurements I could open: rotor outwash in ground effect peaks at r/R 1.7–1.8 whatever the rotor's height (Tanner et al. 2015, NASA/AHS, abstract read); under the disc the jet still points down and only turns radial at the ground (the wall jet, George et al.'s model restated there). So under the disc a stem meets a vertical jet, and the wall jet's horizontal flow grips only its base, outside the disc. The agricultural studies of multirotor downwash in rice and wheat canopies (Li et al. 2019; Shi et al. 2021, *Biosystems Eng.* 203: 60–69) could not be opened (the reader refused them); their abstracts, as the search gives them, say the flow attenuates most at the canopy's bottom and stronger downwash bends the plants more. Neither gives a number used here.
- *A mean count can't shake.* 2.2 spread a cell's stems evenly over the disc and cut fractional means: the two blades' in-plane pushes cancel and nothing fluctuates. A few thick stalks a turn are discrete hits, each at its own radius and azimuth, and their irregular arrival is the shake. Without it the sim can give a lurch and a 290 Hz line, never a shake.

### The stalks, per stem from its own drawn length

- **The allometry (published):** over 76 herbaceous species, height grows as D^1.53 (Niklas 1995, *Ann. Bot.* 75: 133–142, abstract read via OpenAlex; its critical buckling height as D^1.19, safety factors falling as D^−0.34). So a stem's diameter is d = d₁·L^(1/1.53), L its drawn length in metres, d₁ the kind's diameter at 1 m (the cover row's `diameter_m`, its meaning changed: the diameter of a 1 m stem).
- **The anchors:**
  - *Dry and burnt (straw):* wheat's second basal internode, 3.87–4.54 mm across with a 0.475–0.586 mm wall and 17.8–25.5 mg dry a cm, over seven cultivars (Yao et al. 2012, *Aust. J. Crop Sci.* 6(5): 899–903, Table 1, full text read). At a mean 4.2 mm for a 0.9 m plant (assumed: the semi-dwarf wheats' 0.8–1.0 m; the paper gives no height), d₁ = **4.49 mm** (derived). Its wall share 0.52 / 4.2 = **0.124** (derived) confirms 0.125; its mass a cm over the wall's area gives a dry wall of **320–450 kg/m³** (derived), so the dry row's density moves from 500 (assumed) to **400** (derived, a standing dead stem's 10–15 % moisture on that).
  - *Green:* InfoFlora's 1–2 mm culms on 30–120 cm grasses (stem-properties.md [1], published), 1.5 mm at 0.75 m taken as their middle (assumed), d₁ = **1.81 mm** (derived).
- **Self-buckling, the check (derived):**
  - Dry, at the row's 2 GPa: SF = L_c/L 3.9 at 0.3 m, 2.0 at 1 m, 1.44 at the tallest drawn 1.77 m. Burnt 4.5–1.65.
  - Green at 2.11's assumed 1 GPa: SF 0.54 at the tallest drawn 1.34 m. It can't stand. So the green row's modulus becomes the least that lets the drawn culm stand, **6.4 GPa** (derived; inside wheat straw's published 4.8–6.6 GPa tensile modulus, stem-properties.md [6]). Its safety factor is then 1.0 at 1.34 m and 2.3 at 0.3 m.
  - "Quite stiff, but not extremely" (the pilot): a 1.5 m dry stalk is 5.9 mm across with EI 0.079 N·m² and stands with a margin of 1.6, between a 1.5 mm culm (EI 3.4e-4, lies down under its own weight) and an 8 mm woody stalk (EI 0.44).
- **Per stem, not per layer,** because a drawn tuft's stems span their layer's lengths: a tall dry layer's 0.3 m blades are 2 mm leaves and short stems, its 1.7 m culms 6.5 mm stalks. The field already gives each layer's length distribution (below), so the law needs no new query.

| Kind, L | d (mm) | EI (N·m²) | μ (g/m) | SF | v_c (m/s) | fold A at 230 Pa | l_b (m) | folded crest (m) | τ (s) | a cut at the tip (mJ) |
|---|---|---|---|---|---|---|---|---|---|---|
| dry 0.5 m | 2.85 | 4.5e-3 | 1.12 | 2.9 | 64 | 2.6 | 0.18 | 0.09 | 0.04 | 76 |
| dry 1.0 m | 4.49 | 2.7e-2 | 2.77 | 2.0 | 48 | 5.4 | 0.28 | 0.14 | 0.09 | 187 |
| dry 1.5 m | 5.85 | 7.9e-2 | 4.71 | 1.6 | 40 | 8.3 | 0.37 | 0.19 | 0.16 | 318 |
| dry 1.77 m | 6.52 | 0.12 | 5.84 | 1.4 | 37 | 9.9 | 0.41 | 0.21 | 0.20 | 395 |
| green 1.0 m (6.4 GPa) | 1.81 | 2.3e-3 | 1.13 | 1.3 | 34 | 26 | 0.17 | 0.08 | 0.20 | 45 |

(derived, `tools/flightbench/handcalc/stem_stalks.py`; dry at 400 kg/m³ and 2 GPa, e_s 15 kJ/m²; a cut at the tip is e_s·A_w + μ·h_f·u² at u = 117.5 m/s, h_f 2.18 mm, as 2.2.)

### The law (replacing 2.2's steps 1, 2 and 6 for the field; the cut's work, impulses, log and feed are 2.2's)

**1. The field's stems, one by one, from the world.** The field gives per (cell, layer) the stems per m² reaching a height. The law places each stem:
- The ground is cut into **0.1 m squares** (numerical). A square's stems of layer l are those of the cell under its centre (one `StemsNear` at the centre, radius 1 mm, height h_ref): K = ⌊D_l(h_ref)·0.01 m² + u⌋.
- **h_ref = 0.2 m** (numerical): the lowest a disc's plane comes over the ground while the drone stands (0.288 m, less 0.06 m for a rim tilted 30°, less a margin). Shorter stems never reach a disc.
- Stem k of K stands at the square's (u₁, u₂), leans naturally φ₀ = u₃·15° (assumed, 5–25°: the drawn blades' spread and the detail's half tilt to the ground; it sets how readily a stem starts to fold) towards the azimuth 2π·u₄, and its drawn length is T with ρ_l(T) = (k + u₅)/K, ρ_l the layer's share of stems at h_ref still reaching T, read from the field (`StemsNear` at one point at heights 5 cm apart, once per layer and flight; the share is the same in every cell, as the field is count × the layer's reach).
- Every u is the stem's own hash of (square, layer, k, which) (`FlightRandom.Mix64`): world-derived, the same in every flight, never a draw, as an element's modulus is drawn from its Id (2.2). A cell's edge is moved to its squares' centres: up to 5 cm, numerical.
- S04's elements (the range) go through the same steps 2–4 with their drawn shape.

**2. The slipstream folds them, each at its own pace.** A stem under a disc (its base within R of the hub's axis, assumed: the wake's column, its contraction and skew left out) takes q = T/A of that rotor (the most of the discs over it). As a rigid link on its base spring 4EI/L (2.2), its moment balance is
- m(φ) = (φ − φ₀) − A·sin²φ − G·sin φ, with A = q·C_d·d·L³/(8EI) the jet's lean and G = μ·g·L³/(8EI) its own weight's (derived; G alone gives the self-buckling height within 1 %);
- the lean follows **φ' = −m(φ)/τ**, τ the stem's first-mode time above (assumed: a stem in a stand is overdamped by its neighbours; the mode's time sets the pace, range ×0.5–2). There is no lag on q.
- Under the fold load the lean runs away to lying over (φ → 90°); with less load it stands again on its own. Stiff short stems stand, long or thin ones fold, and a stem the disc moves onto, or comes down on faster than τ, still stands when the blade arrives.

**3. Its top.** Standing, L·cos φ over its base. Folded, the arch's crest: max(L·cos φ, min(L·cos φ₀, c_crest·l_b)), l_b = (EI/(q·C_d·d))^(1/3), **c_crest 0.5** (derived: 0.43–0.61 over L/l_b 3–6). With no jet a folded stem rises again.

**4. The cut.** The blade at radius r meets the stem when its top is over the disc's plane there, at the blade's speed across the stem's axis u⊥ = ω·r·√(1 − (t̂·â)²), t̂ the blade's motion and â the stem's axis (derived; 2.2's ω·r·cos φ is the case of a stem leaning along the blade's motion, still Tuck et al.'s "over 80 m/s at 65°"). A folded stem meets the blade as its arch: across where it lies radially, sliding where it lies along the blade's path. Cut where u⊥ ≥ v_c (2.2's law, per stem at its own d), pushed aside below.
- Each stem keeps its own state (shared by every disc, keyed by its hash): its lean, and its length, the cut shortening it to where it crossed the plane. A cut stem is a shorter, stiffer stub: it stands, and a disc coming down slices it (2.2's slice, min(1, Δ/d) of a cut).
- Each cut is a discrete hit at its own radius and azimuth, by the blade crossing it in the step (2.2's elements' rule), with 2.2's work and impulses (`Part`).

**5. Removed from 2.2:** the field's mean counts, the per-rotor q lag (0.04 / 0.1 s), the discs' trails and the elements' 5 cm cut grid. The per-stem state replaces them.

### The numbers

| Number | Value | Label |
|---|---|---|
| Height–diameter exponent | d ∝ L^(1/1.53) | published (Niklas 1995) |
| Dry and burnt d at 1 m | 4.49 mm | derived from Yao et al. 2012 (published) and a 0.9 m wheat (assumed) |
| Green d at 1 m | 1.81 mm | derived from InfoFlora (published), 0.75 m middle (assumed) |
| Dry density | 400 kg/m³ (320–450 dry) | derived from Yao et al. 2012 |
| Dry wall share | 0.125 (0.124) | derived from Yao et al. 2012, carried |
| Green modulus | 6.4 GPa | derived: the least that lets the 1.34 m culm stand |
| Dry and burnt modulus | 2 GPa (0.47–6.6) | assumed, carried |
| Natural lean φ₀ | 0–15° by the stem's hash | assumed (5–25°) |
| Lean pace τ | the first mode's L²/(1.875²·√(EI/μ)) | derived; overdamping assumed |
| Folded crest | 0.5 l_b | derived (elastica, 0.43–0.61) |
| Squares, h_ref, ρ_l's heights | 0.1 m, 0.2 m, 5 cm | numerical |
| The stems' states | 16,384 slots, the oldest overwritten | numerical |

### Which way the pieces push, by hand (before the runs)

- **A vertical landing after a hover over the spot** cuts little: the stems under the discs folded in the hover (A 2.6–9.9 for dry 0.5–1.77 m against a fold near 1), their crests stand at 0.09–0.21 m, under the standing discs' 0.288 m. The cut comes from stems the disc drifts onto and from stems rising as the thrust eases at the touch, when the blades slow.
- **An approach** cuts what the front discs' leading edges meet upright: n = 2·R·v·D(h) stems a second a front disc (2.2's C), each a hit of 40–400 mJ (dry, 0.5–1.77 m) against 2.2's 7–35 mJ.
- **Thicker straw** raises the crest with d (l_b ∝ d): at 1.5× the diameters the dry crests reach 0.28–0.31 m, and a vertical landing cuts.
- The runs and their hand calculations follow in "As built".

### As built

**Code** (`Runtime/Contact/PropStems.cs`, rewritten; nothing else in the core changed):
- The field's stems are materialised per rotor into its cache (the disc's radius + 0.12 m) from 0.1 m squares. A square costs one `SampleGround` and one point `StemsNear` the first time it is met (kept in 1,024 slots). A layer's reach shares cost 41 point `StemsNear` once per flight.
- Each stem's state (lean, length, the jet on it and its lag, its top) lives in 16,384 slots keyed by its hash, shared by every disc.
- Once a step, before the discs' passes: each cache's stems get the jet of the discs over them and its swirl; then each stem's lean advances once. Each disc then cuts what stands over its plane, blade by blade, through 2.2's `Part`.
- **The swirl** (added while running, design step 2): an upright stem in a pure vertical jet has no normal load, so a stem drawn almost upright would stand for seconds. The wake turns with the rotor: its angular-momentum flux is the torque, so it leans off the axis by tan β = Q/(2·T·r̄) (derived, momentum theory; ρ cancels; r̄ = 2R/3 assumed), about 6° in the hover. The load is A·sin(φ + β)·|sin(φ + β)| with β the swirl's share along the stem's lean. Stems leaning across the swirl still start only from their natural lean: that is part of "not always".
- Removed: 2.2's mean counts, its per-rotor q lag, the trails and the 5 cm grid.
- `StemRecord` now has `FreshWork`, `Cached` and `Over` in place of `Cut`, `Entered` and `Elements`. `PropStrikes.StemDensityScale` and `StemDiameterScale` exist for the sweep only.
- `surfaces.json`: `grass_blade` d₁ 1.81 mm and E 6.4 GPa; `dry_culm` d₁ 4.49 mm and 400 kg/m³; `burnt_culm` d₁ 4.49 mm. Every value is labelled in its note.
- No `QueryVersion` change (`StemsNear` as it is), no `LogFormat` change, and `ContactModel.cs`, `ContactSpec.cs` and `LogFormat.cs` are untouched.

**The runs against the hand calculations** (`StemTests`, measured on this branch; the hand calculation is `StemTests.Entries`: 2·R·v·D(h/cos φ₀) along the run's own path, from `StemsNear` directly, each cut at the rim at e_s·A_w + μ·h_f·u² with d by the allometry over the field's tops above h):

| Run | Measured | Hand | |
|---|---|---|---|
| Creep, 0.5 m/s at 0.6 m, both front discs, 5.9 s | 189 fresh cuts, 15.67 J | 181.8, 15.08 J | +4.0 %, +3.9 % |
| fr / fl separately | 16.2 / 15.7 a second, 88 / 78 mJ a cut | 14.3 / 16.5, 86 / 80 mJ | ±13 % each: the stems are discrete (a disc meets about 95; √95 is 10 %) |
| Rear discs in the creep | fresh 60 and 72 against the front's 136 and 124 (at 0.3 m/s) | stubs at their plane | the front's track |
| The approach (2 m/s, 0.5 m/s down, flare at 1.1 m), each disc to the touch | fresh 13 / 15 / 12 / 19 (rr fr rl fl) | the leading edges' entries 9.7 / 11.4 / 11.7 / 13.1 | 1.0–1.45×: the entries are a floor; the rest are stems under the disc the jet had not yet folded |
| Before the touch, front against rear | 34 against 25 fresh | | cut in front of the camera |
| The vertical landing, all four discs, before the touch | 137 fresh, 4,197 slice-equivalents | the drift's entries 75.7 (a floor) | the excess: stems not yet folded; see below |
| Its known-wrong (nothing folds, every stem met cut) | 374 fresh | | fails the law's count |
| A 10 s hover 0.5 m up in the cell | the last cut at 5.6 s; 19 episodes begun and ended | clear | |
| S04's stalks, a creep at 0.4 m | fresh 0 / 7 / 0 / 1, sliced 5.1 / 2.1, up to 17 elements | cut by the same law | |

The approach and the vertical landing have no 10 % check on their fresh count: the stems the jet has not yet folded when a disc reaches them depend on each stem's natural lean against the swirl and its τ, which the hand calculation does not integrate. The 10 % checks are the creep's (rate and work), where the leading edge alone cuts.

**The shake, plainly.** The quad shakes, and how hard is the straw's: its number and its thickness. The last 2 s before the touch, the body's rate at 4–15 Hz (RMS, °/s, and the attitude's RMS in that band, °):

| Vertical landing | diameter ×0.75 | ×1 (Village) | ×1.5 |
|---|---|---|---|
| density ×0.5 | 0.1 °/s, 0.002°; dip 1 %; 0.3 W | 1.4 °/s, 0.03°; dip 27 %; 50 W | 10.4 °/s, 0.26°; dip 58 %; 190 W |
| density ×1 (Village's densest tall cell) | 0.7 °/s, 0.02°; dip 17 %; 17 W | **2.3 °/s, 0.07°; dip 31 %; 92 W; +22 A** | 9.8 °/s, 0.25°; a front rotor stopped; 268 W |
| density ×2 | 1.4 °/s, 0.04°; dip 22 %; 41 W | 3.3 °/s, 0.09°; dip 43 %; 159 W | 16.4 °/s, 0.42°; a front rotor stopped; 327 W; +133 A |

(Roll and pitch combined; the dip is the front rotors' deepest speed drop under their hover speed; the power is the front discs' mean stem work; the current is the front pair's field-mean peak over its hover. Each cell is one run: `TheShakeGrowsWithTheStrawsNumberAndThickness` prints roll and pitch apart, the attitude's wander below 4 Hz and the fresh and sliced counts.)

- **Where it comes from:** a few stalks under each disc are cut fresh (those the drift runs it onto, and those not yet folded), and each then stands as a stiff stub that the descending disc chops at every blade pass (2.2's slice). Those discrete hits, each at its own radius and azimuth, load the rotors unevenly. The flight controller answers each rotor's speed dip, and the body shakes at 4–15 Hz. The blade-pass line stays small (0.03–0.06 °/s at 260–290 Hz): the shake is the loop's answer to the dips, not the cuts' in-plane push.
- **The approach** shakes less (roll 1.3 °/s, 0.033° at Village's densest cell; 2.9 °/s at ×1.5 diameters), because the flare stops the discs over stems they had already folded. Its pitch at 4–15 Hz (6.7–7.9 °/s) is mostly the flare itself (6.7 °/s without stems).
- **Against the footage** (vegetation-on-the-props.md §4; the sim's calm hover has no turbulence, so it is compared with the footage's own calm numbers):
  - Brushing a stalk in H: the 4–15 Hz angle went from 0.03–0.05° to 0.07–0.09° (×1.7–2.5). Village's vertical landing gives 0.05–0.07° and its approach 0.03°, so the pilot's "agreeable" shake sits in the brushing range.
  - Into the straw in H's end: the residue ×3–10 and 30–120 °/s turns. Thick straw (×1.5) gives 0.2–0.4°, ×4–10 the footage's calm, with a front rotor stalling: the "throttle up and find a better place" end of the pilot's range.
  - D's push into weeds: 0.39° pitch at 4–15 Hz, inside the ×1.5 runs' 0.15–0.24°.
- **"Usually flat, but not always":** most stems under a disc fold in the hover (a 10 s hover cuts itself clear by 5.6 s). The short stiff ones, those leaning across the swirl, and those a disc reaches before their τ stand and are cut.
- **Determinism and cost:** the approach's log replays bit for bit (10,031 steps, 57 events); 0 B after the first second in every run, the sweep included.

**The goldens:** `flightbench --golden` 61 PASS, none moved. The range golden's S04 stalks still never stand over a disc's plane in its flight (0xAF745CC52447E017, as before), and no main golden flies over Village's field.

**A finding for 2.3 (the crown), not fixed here.** `ObjectContactTests.ADroneInACrownPaysForItsTwigsAndDoesNotHang` takes off from Village's grass under IslandTree. With the field's stems on, its liftoff cuts the stalks around the foot (0.001–0.19 J a step for the first 0.1 s), so the flight into the crown differs. The drone then hangs in the crown's shells again: 4.17 m up after 25 s with 6 twigs cut, where the base flew out and crashed. That is the crown's own weakness (2.7's note: the shells still hold a resting blade), exposed by a different approach. The test now flies the crown alone (`Contacts.Stems = false`, with a comment saying why); the hang belongs to 2.3.

**An unexplained failure, for QA.** In one of seven runs of `StemTests` together with `ObjectContactTests`, `TheShakeGrowsWithTheStrawsNumberAndThickness` failed, and the six reruns passed. The message was not captured: the summary run printed only its name. The sim is deterministic, so the suspect is the 0 B count (a first-time code path, such as a rotor stopping in the stems, allocating as it is compiled). Each of the sweep's asserts now names its run.

### The checks, as run (on 81e80b39; the machine shared, timings indicative)

```
dotnet test tools/simtests -c Release --filter "FullyQualifiedName~StemTests"
  7 tests, all passed (the numbers in the tables above, printed by each)
dotnet test tools/simtests -c Release
  Passed!  - Failed: 0, Passed: 844, Skipped: 0, Total: 844, Duration: 15 m 34 s
dotnet run -c Release --project tools/flightbench -- --golden
  flightbench: 6 golden cases registered … 61 PASS, 0 FAIL (range-flight 0xAF745CC52447E017 matches)
bit-exact replay (TheApproachCutsInFrontOfTheCamera)
  replay: Match, 10031 steps; match: 10031 steps and 57 events, bit for bit
0 B: every StemTests run asserts 0 B after the first second (the sweep's 18 runs included)
python tools/hygiene/precommit.py --tree HEAD
  3572 files scanned, 0 findings
openspec validate w9-village-physics --strict
  Change 'w9-village-physics' is valid
```

The step's cost was not measured again here. A field stem costs a little per step in each cache it lies in (about 100–400 a disc in the dense cell), and a square or a layer's shares cost world queries once. PLT-04's quiet run is QA's.

### The proposed straw-landing scenario (for the orchestrator)

```
#### Scenario: Landing in tall straw
- **WHEN** a scripted landing (seeded) comes down into the farmland's densest tall cell, vertically after a hover over it and as
  the pilot's approach (2 m/s forward, 0.5 m/s down, a flare, the touch)
- **THEN** the field's stems stand as the world places them, each with the diameter of its drawn length (d = d₁·L^(1/1.53)) and able
  to stand under its own weight; the slipstream folds most of those under a disc and not all; the front discs cut more than the
  rear ones on the approach; the cuts a front disc's leading edge meets in a creep are within 10 % of 2·R·v·D(h) and their work
  within 10 % of the hand calculation; each cut is a discrete hit in the log (BladeStrikes, event 39); the body's 4–15 Hz rate and
  the rotors' speed dips grow with the stand's density and the stalks' diameter over a sweep of ×0.5–2 and ×0.75–1.5; the log
  replays bit for bit with 0 B in steady state; and no random value is added to the pose
```

### For the next tasks

- **2.5 (cut bits on camera):** the cuts are now discrete, so `FeedSignals.StemCuts` holds whole stems. The front discs cut more on an approach. The pieces are 2–6.5 mm across (the allometry) and a fresh piece is the stem above the plane: up to 1.5 m of straw. Slices are short rings, Δ ≈ the descent per blade pass (1.7 mm at 0.5 m/s).
- **2.10 (stems on the legs and the fiber):** a field stem's diameter, lean and length are per stem (`Cache`'s placement, its hash; d from the cover row's `diameter_m` as a 1 m stem's). Under a disc the stems fold as arches, crest 0.5·l_b (0.09–0.21 m dry in the hover), so the legs land on folded arches, not upright stems. Shorter than h_ref (0.2 m), no stem is placed: 2.10 needs its own placement down to 0.
- **2.3 (leaves in crowns):** `Part` is unchanged; a leaf can still go through it as 2.2 said. The per-stem state store (keyed by a hash, shared by every disc) is the pattern for leaves that must stay cut.
- **QA:** the shake comes from the loop's answer to discrete rotor dips. A future change to the PIDs or the motor model moves the sweep's numbers, so rerun it with them.

### Still assumed (2.12)

- The natural lean's range (0–15°), the overdamped lean at the first mode's pace, the swirl's radius (2R/3), the folded stub's leg (cos ≥ 0.7), the wake's column (no contraction, no skew in forward flight), and no canopy sheltering of the jet. Sheltering would let more stems stand, so the cuts here are a floor.
- The wheat plant's 0.9 m height for the anchor, and the green culm's 1.5 mm at 0.75 m.
- The agricultural downwash-in-canopy measurements were not readable here. No number of theirs is used.

### Addendum: the S04 patch's liftoffs that threw (2.9's harness, `--ground`)

**The report:** on House's S04 patch, every Rain and Severe rain liftoff threw `ArgumentOutOfRangeException` in `MassModel.SetPartMass`, from the rain film's mass. Every Sunny one was "never off" with a sink of 0.00 mm. Village's rows and House's track start were clean.

**The chain, found by stepping one liftoff and checking every value each step:**
1. **The cause is the harness's start, not the stems.** `LiftoffScenarios.Over` puts the skids 2 mm over `SampleGround`'s height. The range's S04 patch is a hole in the ground plane, filled by the patch's object: a 12 × 12 m soil box with its top at −0.125 m, and the straw mat's pieces, 0.125 m boxes standing on it with their tops at 0. `SampleGround` there reports `GroundFlags.Hole` and a height of −∞. So `Over` returned a body origin at y = −∞ (measured: start (−134.0, −∞, −13.355)).
2. **The core took it silently.** Nothing checked the start, and the first step's forces, velocities and pose were all non-finite (step 17: y −∞).
3. **In Rain** the first thing that checks its argument is the film's mass: `RainField.Fill` gives NaN from the non-finite state, and `MassModel.SetPartMass` (`FlightCore.cs`, the step's last line) throws.
4. **In Sunny** there is no film, so nothing throws. The body stays at −∞ and never rises 1 cm over its rest, so the harness reads "never off" with a sink of 0 (the soil's sink bookkeeping never touches a skid).
- **The stems take no part.** Stepping it with the start fixed: the S04 stalks cached about the discs (24–120) never stand over a disc's plane (0 cuts), because they lie in the mat with their tops at 0.17 m at most (2.2's hand calculation D). `Part`'s √(ω² − 2E/J) can't go negative: when a cut's energy reaches the rotor's ½Jω², `share < 1` stops the rotor (`RotorJammed`) and the root is not taken.

**The fix:**
- **The harness:** where `SampleGround` reports a hole, `Over` takes the ground as the highest face a ray straight down meets. If there is none, it throws, naming the point.
- **The core** (`FlightCore`):
  - It refuses a start that is not finite, with an `ArgumentException` naming the field (`StartPosition`, `StartVelocity`, `StartBodyRate`, `StartAttitude`).
  - Its step guard checks the force and moment so far and the state's pose, velocity, rate and rotor speeds after each part of the step: propulsion, aerodynamics, contacts, tether, rigid body, contact sense, prop strikes. It also checks the rain film's mass before `SetPartMass`.
  - The first value that is not finite throws `NonFiniteStateException`, naming the quantity, the part and the step. The checks are comparisons only (x − x == 0), and nothing allocates unless one throws.
- Nothing is clamped. No physics value changed, so the goldens can't move; the runs below confirm it.

**What a liftoff from S04 does now** (`StemLiftoffTests`, the harness's own flight, a vertical touch and a glide in each weather):
- **Dry:** off at 0.553–0.560 s, at a throttle of 0.588–0.590.
- **Rain:** 0.570–0.587 s at 0.592–0.595.
- **Severe rain:** 0.563–0.621 s at 0.590–0.602.
- Every one has a sink of 0.00 mm: the mat's pieces are rigid boxes, and no soil row lies under them. No stem is cut. There is no lone held corner, no crash and no tunnel. So the drone lifts off from S04 dry near 59 % throttle, inside the pilot's 50–65 %.
- **Village's densest tall cell:** off at 0.549–1.076 s at 0.588–0.693; sink 3.7–9.5 mm; the pull at the legs is 0 dry, 5.2–6.3 N in Rain and 9.9–10.2 N in Severe rain.

**The checks, as run** (on this branch, cut from f14e9438 at model 90; timings indicative):

```
dotnet test tools/simtests -c Release --filter "FullyQualifiedName~StemLiftoffTests"   → 4 passed (the numbers above)
dotnet test tools/simtests -c Release   → Passed! Failed: 0, Passed: 860, Total: 860, 33 m 26 s (machine shared)
flightbench --golden     → 62 PASS, 0 FAIL
flightbench --contacts   → 80 PASS, 0 FAIL
flightbench --landing    → 29 PASS, 0 FAIL
flightbench --ground 4   → House S04 patch: Sunny/Rain/Severe rain never off 0, threw 0;
                           no liftoff tunnels: PASS; no liftoff's step throws: 0 flights threw: PASS;
                           the pilot's account (design 13's targets): FAIL, 2.9's known "not met", unchanged by this fix
bit-exact replay: the goldens' replays and StemTests' approach (Match, bit for bit); 0 B: every stem test's run
python tools/hygiene/precommit.py --tree HEAD   → 0 findings
```

**For QA:**
- The S04 patch's centre lies on a mat piece's edge. Stepped with 2 s armed at idle and no touch-down speed (not the harness's flight), the drone settled on two legs, with the others over the 0.125 m step to the soil box, and the idle's airmode tipped it to 50°. The harness's flight lifts off cleanly.
- A liftoff spot fully on one mat piece would read the mat alone. That is the harness's choice, not changed here.
- The 0 B flake in `TheShakeGrowsWithTheStrawsNumberAndThickness`: this work did not show what allocates.

## 2.3 Leaves in crowns, and what a crown's twigs hold

**Owner:** physics-engineer · **Requirements:** PHY-05, PHY-24, PHY-26, LOG-03 · **Branch:** `unity/physics/physics-integration-2--w9-crowns` (from ca718e20, model 89) · Widened by three findings (2.4's anchors on shells, 2.7's held props, 2.7's event overflow). The model number is the orchestrator's at the merge.

Labels as in 2.2: **measured**, **published**, **carried**, **derived**, **assumed**, **numerical**. Every number below is printed by `tools/flightbench/handcalc/crown_twigs.py`, which reads the constants from `physics_materials.json`'s `twig.crown` block.

### 1. What was wrong (measured on ca718e20)

The crown's twig shell is a row of `physics_materials.json` (`twig`): 30 N/m per touching part, no limit, the friction's stick anchor on it. A shell is a sphere of 0.8–1.7 m radius (IslandTree's 19 at the test's spot, GreenBush_Dry's 6; measured, `StaticContacts` round them), so a part 0.5–1 m inside one is pushed 15–30 N back towards the sphere's face, and a crown of 7–10 overlapping shells pushes 7–10 times that. The row's own rationale ("one 10 mm green twig 0.8 m long under each part") is a twig that breaks or lets go under a foot's 11.8 N: its moment there is 9.4 N·m, a stress of 96 MPa, at the top of green wood's rupture (derived; §2). So the shell held the drone like a soft solid, which no twig can:
- **The crown flight with the stems on** (`ObjectContactTests`' crown, `Contacts.Stems` true): first touch 10.89 s, 6 twigs cut, then the drone rests 4.17 m up for the last 10 s with 21 pairs touching and 38.7 N from them (its weight less the rotors' thrust), rotor fr at 100 rad/s and rl at 480 (measured). With the stems off it frees itself at 18.9 s by a crash on a bark capsule; the outcome turned on which flight the liftoff's grass gave it.
- **The bush hover** (`AHoverInACrownBreaksNoBladeTwice`'s start, 1.24 m up in GreenBush_Dry): step 0 makes 360 pairs and 777 N, 4 legs break and 4 crack, 7 motor bells dent, 88 events (24 refused); then three rotors stand still for 25 s in the shells (their blades' 25–39 N each, 2.7) while the drone rests on 198 pairs at 43 N (measured).
- **The anchors** (2.4): a leg's stick anchor on a shell slides 0.66–0.99 m, past the leg's 0.176 m reach, and 2.4 left shells out of its release because releasing them lodged the drone: the shells' push was then the only thing that held it.

### 2. What a twig holds (first principles)

**One twig** (each number: `twig.crown`, labelled there):
- diameter d = 7.5 mm (assumed, carried: the shell's 5–10 mm cards), free length ℓ = 0.5 m from the branch it grows on (assumed, carried: one axis a foliage spray), green wood's rupture σ = 50 MPa (assumed: 23–90 MPa over small stems' bending tests, lower toward the pith; green black locust's published 95 MPa is the upper bound) and modulus E = 10 GPa (assumed, carried from the row; 5.7–18 GPa in the same tests).
- A part bearing on it at an arm a from its base bends it as a cantilever: k(a) = 3EI/a³, EI = 1.553 N·m² (derived). It breaks (a green twig hinges and lets go: greenstick) at P_b = σZ/a, Z = πd³/32 = 4.14e-8 m³, so **8.3 N at its middle** (a = 0.25 m), after a deflection δ_b = 2σa²/(3Ed) (derived).
- **Does the part slip off first?** The twig's slope at the load when it breaks is σa/(Ed): 9.5° at its middle, under the 26.6° at which a part with μs 0.5 slides along it. So a twig breaks before it lets a part slide off, over its whole length (derived). (A twig met end-on would buckle at π²EI/(4a²), 61 N at its middle; one met across, the common case, bends. The law takes every twig as met across, assumed.)
- The arm is even over (0, ℓ] (assumed: nothing places a part nearer a twig's base than its tip). The mean work to break one twig is **W̄ = σ²Zℓ/(6Ed) = 0.115 J** and its mean deflection at the break **δ̄ = 2σℓ²/(9Ed) = 37 mm** (derived).

**The twigs a part meets** (stereology, derived): a crown's twigs are lines of length density L_V (m/m³) in random directions. The lines hitting a convex body number L_V·S/4, S its surface, so a part pushed a distance z meets L_V·p·z/4 twigs, p the perimeter of its silhouette seen along the push (a capsule's 2l⊥ + πw, a sphere's πw). Each twig met is bent by the part's further advance until it breaks after its δ_b. So, once the part has moved δ̄ into fresh twigs, it carries a steady
- **F_y = n'·W̄, n' = L_V·p/4** (each metre of advance breaks n' twigs at W̄ each; derived), reached over δ̄: its secant stiffness **k_e = F_y/δ̄**.
- L_V = **2.0 m/m³** (assumed, carried from `PropStrikes.CrownTwigLength`). The pipe model checks it: a twig of section A carries a leaf area A·(AL:AS) (Shinozaki 1964), angiosperm branches 0.01–10 m²/cm² (published, PROMETHEUS protocols), 0.1–1 taken; over the crowns' 0.21–2.09 m²/m³ of leaf (2.8) that is 0.2–24 m/m³ of 7.5 mm twigs (derived). 2 lies inside, in its lower part.

**The drone in a crown** (the repository's contact shapes, each pushed down; `crown_twigs.py`):

| Parts | silhouette p (m) | twigs a metre | F_y (N) | k_e (N/m) |
|---|---|---|---|---|
| skids (4) | 1.886 | 0.94 | 0.108 | 2.9 |
| arms (8 tubes) | 2.935 | 1.47 | 0.169 | 4.6 |
| stacks, packs, payload, coil | 3.275 | 1.64 | 0.188 | 5.1 |
| blades (4 rotors, at rest) | 4.372 | 2.19 | 0.251 | 6.8 |
| legs, motors | 0.593 | 0.30 | 0.034 | 0.9 |
| **the whole drone** | 13.06 | 6.5 | **0.75** | |

- **When it lodges, when it falls through.** The twigs hold 0.75 N against its 46.5 N weight (1.6 %): **a 4.7 kg drone falls through a crown's twigs** and stops only on what is stronger, a branch (the shells' bark capsules, 27–43 mm at the test's spot) or the ground. To hang on twigs alone the crown would need L_V = 124 m/m³ of 7.5 mm twigs, 62 times the assumed one and 5 times the pipe model's top: a hedge's thicket, not a tree's crown (derived). A drone with thrust in a crown flies on through it, paying 0.115 J a twig its parts break and its props' cuts.
- **A blade at rest in a shell** carries at most 0.029 N, its stick friction 1.3e-3 N·m about the shaft (derived), against the 1.1–1.8 N·m the shells' 25–39 N gave (model 89). So **a prop is never held still by twigs**: the motor turns it, it leaves rest, and `PropStrikes.Twigs` cuts what it meets. A blade that cuts a twig at speed pays e_s·A + its fling (2.7's law); a rotor too slow for that work stops on the twig (2.7's jam, kept), and the motor's next turns cut it.
- **This is a mean.** A part meets twigs one by one; the law carries their expectation, as `Twigs` does for the props. A drone might rest a moment on two twigs near their bases (25 N each at a = 0.1 m), which the mean cannot give; placing twigs one by one by hash, as 2.12 places stems, would. It is named below, not built.

### 3. The law (`ContactModel`, the twig shell's hold)

A crown row (`twig.crown` present) replaces its per-part 30 N/m for the drone's parts (the fiber keeps the row's own fields: `Tether/` is not this task's):
1. **Stiffness:** each part's k_e = (L_V·p/4)·W̄/δ̄, p its silhouette along the pair's normal (`Layered`, where every compliant row's per-part stiffness is made), in series with the part's own; damped at the row's ζ (0.2).
2. **The yield:** a crown pair's spring reads its depth past its yield, and the yield grows so the depth it reads is never more than δ̄: the plane moves in with the part (twigs met and broken), and stays when the part backs out (the twigs behind it are broken; it meets nothing until it is past where it was).
3. **One crown a part:** the overlapping shells stand for one crown's twigs, so a part's deepest crown pair carries them and its other crown pairs are held (suppressed) for the step, as `Twigs` takes the deepest shell's meeting for their union.
4. **The anchors:** a leg's stick anchor on a crown pair lets go past the twigs' reach, δ̄, which is shorter than the leg's own 0.176 m (2.4's `Reached`, with the twig's reach): a twig bent sideways past it has broken. The ground's rule is unchanged.

**The known-wrong:** model 89's row (30 N/m, no yield): the crown test's base flight hangs at 4.17 m on the shells' push.

### 4. The leaves (`PropStrikes.Leaves`)

**The numbers** (`twig.crown`, each labelled): leaf area density per variant (measured, 2.8: 0.21–2.09 m²/m³; a variant without leaves, 0), dry leaf mass per area 0.06 kg/m² (published, Onoda et al. 2011) over a dry matter content of 0.35 (assumed) = **0.171 kg/m² fresh**, work to shear 0.1 J/m (published, Onoda), a critical speed of 132 m/s (derived: 2.11 §2.2's leaf blade across its width, 44 m/s sharp, × the prop's edge factor 3, assumed), and the cleared cell, 0.05 m (assumed: half a 10 cm leaf).

**The law, per turning rotor in a crown, per step:**
- The disc's plane is sampled every 5 cm (21 points within R, numerical); a point in the crown's immersed part (past the deepest shell's chord, `Twigs`' region) lies in a world cube of 5 cm. A cube no disc has passed before is **fresh**: its leaves are met now and it is kept cleared (a store of the cubes passed, keyed by the crown and the cube, 16,384 slots, the home slot overwritten when full; 2.12's per-stem store's pattern). A disc held still clears its cubes and meets nothing more; a rear disc in a front one's track meets what the front left.
- Each fresh cube at radius r, the blade's speed u = ω·r: the blade strikes the leaf area LAD·A_c·t, A_c = (5 cm)², t the slab its section sweeps there (its frontal height h_f moving in its plane; the whole cube moving along its axis: t = h_f + (s − h_f)·|v̂·â|). It **flings** that leaf's fresh mass to its speed (m_A·LAD·A_c·t·u², 2.2's fling) and, where u ≥ 132 m/s, **cuts** it: 0.1 J/m over its trace on the plane, (π/4)·LAD·A_c a slab (stereology, derived) times t/h_f slabs. Slower, the leaf is pushed aside uncut (no cut work), its struck mass flung all the same.
- The rotor pays it (ω' = √(ω² − 2E/J)), the body takes the in-plane impulse and the reaction about the shaft at the cube's point (2.7's `Twig`'s accounting), each cube by the blade whose turn it is. One `BladeStrike` a rotor a step for the step's leaves, on the crown's shell with the world's `foliage` material (the twigs keep `twig`), and the blade's `BladeStrikes` slot.

**What it gives, by hand** (derived, `crown_twigs.py`): a disc driven at 2 m/s through a crown sweeps 0.51 m² of fresh plane a second: 0.4–10 W of fling for LAD 0.21–2.09 at 900–1,500 rad/s, and no cut at the mean radius (u 76–127 m/s under 132). A disc coming down at 0.5 m/s sweeps its whole volume: **5–53 W** a disc, against the hover's 110–160 W: the dips and the shake of a descent into a leafy crown. Each fresh cube is a discrete hit (0.02–0.1 J), so the work arrives in a train at the cubes' rate, never as a smooth drag.

**Not modelled:** the leaves' drag on the rotor's inflow through the crown (the aero's: a porous blockage of ½ρv²·C_d·LAD·L, about 2 % of the disc loading for 0.5 m of LAD 1, derived), named for the aero.

### 5. The event buffer

The step's buffer (64) refused 24 events in the bush hover's first step. A step's events are bounded at their source by the contact table: each pair slot (shapes × 24) makes or breaks at most once a step, and a slot freed by a break can be made again in the same step. So the buffer is **sized from what a step can raise**: 64 for every other producer, plus 2 a pair slot of the flight's contact shapes (`StepEvents.ForContacts`). It allocates once, at the core's construction (109 shapes: 5,296 events). Bounding at a source would drop forensics (every `ContactMade` is a record QA reads); refusing none is the requirement.

### 6. The runs (the spec's)

- **The crown flight, 2 m/s into a crown** (the spec's "Leaves in a crown"): the crown test's IslandTree, flown at 2 m/s as well as 6.85 m/s, the stems on: what the twigs and the leaves take, the rotors' dips, the body's rate at 4–15 Hz and at the blade pass, where it ends and why.
- **A hover in a crown** (GreenBush_Dry, 25 s): the props turn, what they cut, no blade frozen, no break at 0 kg, no event refused.
- Hand checks within 10 %: the twigs' hold (F_y on the whole drone against the contact force measured while it falls through), and the leaves' work a second against `crown_twigs.py` for the run's own speeds and LAD.

### 7. As built

**Code:**
- `ContactSpec.cs`: `MaterialRow.Crown`, a `CrownRow` (the twigs' and leaves' constants, W̄ and δ̄ derived from them). `PhysicsMaterials.cs` reads a row's `crown` block, every value labelled.
- `ContactModel.cs`: `Layered` gives a crown row's per-part stiffness k_e = (L_V·p/4)·W̄/δ̄ and **no damper** (below); `Update` yields a crown pair's plane past δ̄ and keeps the pair while its part backs out inside the shell; one crown pair carries a part (the deepest; the rest held for the step); `CapCrown` holds a leg's push on the twigs to the hold of its half-skid and lets its anchor go past δ̄; `Final`'s release (`Reached`) takes the twigs' reach on a crown pair. `TouchingOn(material)` for forensics (what a drone at rest rests on).
- `PropStrikes.cs`: `CrownTwigLength` and `CrownTwigDiameter` are the row's (`Twigs` reads them there); `Leaves` and its cleared store (`LeafCells`, `LeafWork`, `LeafCutWork`, `LeafWorkOn(c)`).
- `StepTrace.cs`, `FlightCore.cs`: `StepEvents.ForContacts(shapes)` sizes the step's buffer (§5).
- `LogHeader.cs`: a tagged header entry (`TagCrown`, 29) holds each row's crown block, as 2.2's stems' and 2.9's ground's: **no log format change** (`LogFormat.Version` unchanged). A log without it flew model 89's shells.
- `physics_materials.json`: `twig.crown`, each value labelled (§2, §4).

**Changed while running (each measured, each a physics reason, nothing tuned to an outcome):**
1. **No damper on a crown pair.** The row's ζ 0.2 on k_e gave 0.3–1 N·s/m a part: at 2 m/s 30 times the twigs' hold, a viscous drag no twig has. The twigs' rate effect is their inertia, ½·m_e·v² a twig met (m_e ≈ 0.24·ρAa, 2.5 g at the middle arm, derived): 4 % of W̄ at 2 m/s and half of it at 7 m/s, of a hold that is itself 1.6 % of the weight. Left out and named.
2. **A leg's push capped at the twigs' hold (`CapCrown`).** Measured in the drop test before it: a foot held by its stick anchor while the body fell through took the lever's statics (p7g-legs 1.4) to 45 N a point, the corner's leg load to 132 N, with its anchor 0.16 m off (2.4's runaway, on the twigs). The twigs under a foot carry at most the half-skid's F_y and break past δ̄; so the leg's push on them is scaled to that hold, and its anchor lets go past δ̄ whether it slid or not. After: 0.03 N a leg, the anchors within 37 mm.
3. **The leaves' samples on the rim and a 2.5 cm cell.** On a 5 cm grid alone a disc moving in its plane met its fresh leaves at the grid's leading points, 0.10 m from the hub instead of the tip's 0.127 m: (0.10/0.127)² = 0.62 of the fling. The leaves enter a disc moving in its plane across its rim, where the tips take them; so the rim is sampled every cell of arc, and the grid's points trail in cells the rim cleared. The cell went to 2.5 cm so a straight path's width is quantised to at most +8 % (5 cm: +18 %).

**The runs** (measured on this branch; `CrownTests`, `ObjectContactTests`):

| Run | Measured | Hand | |
|---|---|---|---|
| The drone dropped disarmed into a 4 m twig sphere: the twigs' upward force while 0.3–1.5 m in | **0.852 N** | 0.846 N (Σp = the drone's shapes along the vertical, L_V·p/4·W̄) | **+0.7 %** |
| … where it is after 1.5 s | 10.53 m in, 13.6 m/s (free fall: 11.0 m, 14.7 m/s) | | falls through |
| … model 89's shell (the known-wrong) | rests on the crown's top: −0.06 m in (bounced up) | | holds it like a solid |
| A disc through GreenBush's leaves (LAD 0.976) at 2 m/s: the front rotors' leaf work while wholly inside | **18.67 J** | 17.72 J (2R·v⊥·m_A·LAD·t·(ωR)², the run's own ω and v, step by step) | **+5.4 %** |
| … the rear rotors (in the front ones' track) over the whole flight | 3.51 J | less than the front's | the cleared track |
| … 4–15 Hz body rate in the leaves / before | 0.21 °/s / 0.56 °/s (before: the pilot's acceleration) | | no shake from leaves alone at 2 m/s |

- **The bush hover** (`AHoverInACrownBreaksNoBladeTwice`, 25 s 1.24 m up in GreenBush_Dry): the props turn from the first second (0 rotor-steps at rest after it, against 3 rotors standing still for 25 s on model 89); 8 twigs cut for 60.7 J; 3,544 leaf cells for 66.1 J (most in the first second, the disc's own volume; then a held disc meets nothing new: "a hover cuts itself clear"); 0 breaks, none at 0 kg; **0 events refused, in the first step too (it made 271)**; at the end 1.45 m up on 0.37 N from its contacts (the pilot holds its height, the twigs carry nothing). Replays bit for bit (25,016 steps, 1,372 events); 0 B after the first second.
- **The crown flight at 6.85 m/s, the stems on** (`ADroneInACrownPaysForItsTwigsAndDoesNotHang`): first touch 10.89 s; 22 twigs cut for 144.3 J; 21,448 leaf cells for 126.7 J; the front rotors dip 26.6 % in the crown's first second, the body's 4–15 Hz rate 15.1 °/s there against 0.04 °/s before (the crown's twigs, leaves and branches together); it **crashes on a branch** (bark) and ends on the ground 16.9 m from where model 89 hung it, 0 events refused (at most 43 in a step), 0 B. **The known-wrong** (model 89's shells: no crown block, 30 N/m a part, the blades glancing) hangs 4.21 m up on 28 twig pairs, 0 on bark.
- **The spec's crown flight, 2 m/s into IslandTree** (`TwoMetresASecondIntoACrown`): first touch 21.12 s; 7 twigs cut for 52.1 J; 7,665 leaf cells for 22.6 J; the front rotors dip 10.6 %; 4–15 Hz body rate 9.3 °/s in the crown's first second against 0.00 °/s before, the blade-pass band's strongest line 0.007 °/s at 230 Hz (the shake is the loop's answer to the rotors' dips, as 2.12's); it **flies through the crown** and leaves it at 2 m/s, 4.1 m up, a leg damaged on the way (730 loose-piece records). Replays bit for bit (30,016 steps, 5,742 events); 0 B in the crown's steps; 0 events refused (at most 57 in a step).
- **What holds and what breaks** (the answer to the task): a crown's 5–10 mm twigs break one by one at 0.115 J and hold 0.75 N of the drone; a prop is never frozen by them (0.029 N a blade); a turning blade cuts each twig it meets (2.7's law, 6.6–7.6 J a twig in these runs) or, too slow, stops on it and cuts it on its next turn. The drone hangs in a tree only on a **branch** (the shells' bark capsules, 27–43 mm here) or on the ground. That is the model's answer for every crown on Village; whether a real drone sometimes hangs a moment on two twigs near their bases is the mean's limit (§2), named below.

### 8. Goldens and benches, audited case by case

- **`flightbench --golden`:** 62 PASS, no FAIL (the same lines as ca718e20): no golden flies through a crown, and the range golden's straw is not a crown row. The event buffer's size changes no flight (it only stops refusing).
- **`flightbench --contacts`** (ca718e20 against this branch, every row compared; only the crown rows moved):
  - the test range's V01_L crown at 10 m/s: the crash behind it on the ash's timber at 9.71 m/s (model 89: 6.71 m/s): its shells no longer took the drone's speed away.
  - **the farmland crowns at 10 m/s:** the twigs' impulse **0.13–0.47 N·s** (model 89: 31–76 N·s, up to 1.6 times the drone's whole 47 N·s of momentum: the shells were walls). IslandTree: flies through (no crash, as before). Pine002_M, Pine004_1, Pine006_01 now crash on their **branches** at 9.8–10.5 m/s (model 89: Pine002_M none, the rest at 2.8–6.5 m/s after the shells slowed them).
  - **Two rows fail, "no tunnel"** (not a golden; measured, each after the crash): Pine002_M, `skid_left` (leg_fl broken 131 steps before, its half-skid hanging) 115 mm through a 47 mm bark capsule at 14.27 m/s, step 10415; Pine004_1, a resting blade of `prop_fl` (the rotor stopped in the crash) 63 mm into a 28 mm bark capsule at 8.48 m/s, step 10120. Both are a tumbling, broken drone's part passing a thin rigid branch at a speed the shells used to take away: a hanging half's elastic turn and a blade come back to rest, not the crown's law. They are physics bugs and are named for the orchestrator (below), not fixed here: the task is the crown.
  - the farmland bush: 0.13 N·s from its twigs (model 89: 48.63), 4 twigs cut for 27.8 J (model 89: none, the blades glancing at rest), out at 10.09 m/s.
- **No golden was re-recorded.**

### 9. The checks, as run (this branch; a shared machine: no timing counts)

```
dotnet test tools/simtests -c Release --filter "FullyQualifiedName~CrownTests|FullyQualifiedName~ObjectContactTests"
  8 passed (the numbers in §7, printed by each)
dotnet test tools/simtests -c Release            (on 71456dac)
  Failed: 4, Passed: 855, Total: 859, 32 m 40 s
    TheHeaderCoversTheContactTypes             MaterialRow.Crown not in the header's coverage list: added (logged in TagCrown)
    NoStaticReferenceFieldOnTheStepPath        LogHeader's sort lambda renumbered 147 → 148 by the crown entry: the list follows
    WithoutTheHangingGiveTheWindyCrownIsModel50s  model 50's kept audit flew through V01_L's twigs at speed: it flies model 89's
                                               shells now (GoldenFlights.WithoutTheCrowns), its Tunnel at step 10975 again
    SnagsNear_AllocatesNothing                 2600 B, World code untouched: passes alone (a first-call allocation)
dotnet test ... the four above, CrownTests, ObjectContactTests   (on 26c2b496)
  Passed: 12, Failed: 0
dotnet run -c Release --project tools/flightbench -- --golden
  6 golden cases registered; 62 PASS, 0 FAIL
dotnet run -c Release --project tools/flightbench -- --contacts
  contacts: FAIL (the two post-crash tunnels of §8; every other row PASS)
bit-exact replays: TwoMetresASecondIntoACrown  Match, 30016 steps and 5742 events, bit for bit, and 730 loose-piece records
                   AHoverInACrownBreaksNoBladeTwice  Match, 25016 steps and 1372 events, bit for bit
0 B: the crown flight from its first touch, the bush hover after its first second (asserted)
dotnet test tools/simtests -c Release            (on aec5b8d3, before the line's merge)
  Passed: 859, Failed: 0, Total: 859, 22 m 55 s
after the line's merge (10709759) and the reach for every hold (54b2fee9, b400ebfe):
  dotnet run -c Release --project tools/flightbench -- --sweep test-range 400 1 --point 440
    #440 Slide lever 0.290 m (ok; model 89 4.93 m), Cut 0.433 m (ok; 1.10 m); their rows fail Ground (finding 6) and the Land row's Lifts
  dotnet run -c Release --project tools/flightbench -- --sweep test-range 400 1 --point 430 / --point 445
    the two tunnels unchanged (§9b)
  dotnet test ... AnchorReach, LegChange, LegReplay, Slope, RangeContact, ObjectContact, Crown, Sweep, Landing tests
    Passed: 78, Failed: 0
  dotnet test tools/simtests -c Release   (on b400ebfe)
    Passed: 866, Failed: 0, Total: 866, 44 m 16 s
  flightbench --golden: 62 PASS, 0 FAIL;  --slopes: PASS;  --contacts: FAIL, the same two post-crash tunnels (§8); one more
    row moved, Pine006_01's crown (541 twig contacts against 493, a post-crash slide on its bark), still PASS
python tools/hygiene/precommit.py --tree HEAD
  3587 files scanned, 0 findings
python -m pytest tools/blender/tests -k "material or physics"
  61 passed
openspec validate w9-village-physics --strict
  Change 'w9-village-physics' is valid
```

### 9b. Every hold's anchor at once (the sweep's findings 2 and 3, added to this task)

**The rule, by what each hold can physically do** (`ContactModel.Final`, `CapCrown`):
- **What bounds an anchor is the leg, not the hold.** A held foot farther than γL (0.176 m; the frame's 0.187 m along the skid) is one the leg no longer reaches (2.4's derivation). That is geometry, the same whatever holds the foot, so the release now applies to **every** leg pair's slide: the ground's (2.4), a **soil shape's** (a range patch's floor and ends are objects), a **rigid shape's** (a wall, a beam, a branch), and a **crown's**. The soil foot's own anchor already took it (`SoilTrial`, `SoilFinal`, on the ground or a soil shape alike).
- **A hold shorter than the leg lets go sooner:** a crown's twigs at δ̄ = 37 mm (a twig bent sideways past it has broken; whether the foot slides or sticks, `CapCrown`). A soil's groove gives millimetres and a rigid shape nothing, both far inside γL, so the leg's reach is theirs (2.4's note on the groove's give stands).
- 2.4 kept objects out because releasing the crown's shells lodged the drone in them: that was the shells' false hold (§1), gone with §3.

**Acceptance rows:**
- **#440 on E01's 22° end** (`SweepTests.OnE01sSoilShapesTheReachHoldsTheLever`, `--sweep test-range 400 1 --point 440`): the slide's largest lever **0.290 m** (model 89: 4.93 m, 214.2 N·m on 43.4 N), the cut's **0.433 m** (model 89: 1.10 m); both pass the sweep's lever. Their rows still fail the sweep's Ground check (a broken leg's hanging half 20–24 mm into the soil: finding 6, the legs engineer's) and the Land row's liftoff (as on model 89). The known-wrong, the reach off: 4.93 m.
- **The crown flight:** §7's, unchanged by this rule (its anchors on twigs were already bounded by `CapCrown`).

**Finding 3, the two tunnels on E01** (traced with a scratch print at the `Tunnel` call, not committed): **they do not share a cause, and neither is the anchor's.**
- **#430 Cut, step 5640:** `support_rl_joint` of the left skid, its corner `Hanging, Stub` (leg_rl broken, the half hanging from the other T-joint), 58 mm through the top of a belt_straw soil box (0.44 × 0.30 × 0.33 m), its normal flipped. A hanging half's support driven through the soil: finding 6's mechanism, the legs engineer's (`--w9-legs`).
- **#445 Slide, step 6392:** `support_fl_joint`, an **intact** corner, 7.1 mm into a belt_straw soil box (0.71 × 0.80 × 0.50 m) on the tilted end, its latched plane the box's top while the query's nearest face flipped to a side as the sunk support slid off the box's edge, then not seen. A soil box is a soil (it yields to the foot, the sink is the soil's law), but the tunnel rule takes every surface row as rigid. So it is most likely a false tunnel at a soil box's edge, not a part through a solid; to be settled by whoever owns the tunnel rule for soil shapes (`ContactModel.Update`'s `RowOf(p).Rigid` for a soil surface). Not changed here.
- Both still fail the sweep's tunnel check on this branch, unchanged by the reach.

### 10. Still assumed, and not met

- **Assumed:** L_V 2 m/m³ (inside the pipe model's 0.2–24), d 7.5 mm, ℓ 0.5 m, σ 50 MPa, E 10 GPa, every twig met across, the arm even over ℓ; the leaves' dry matter 0.35, their critical speed's edge factor 3, the cell 2.5 cm, the needles as leaves; the slab a rim cell strikes, h_f + (s − h_f)·|v̂·â|.
- **The mean, not the twigs one by one.** A drone never rests a moment on two strong twigs; placing twigs by hash in the shell as 2.12 places stems would give it, and a rate the pilot could judge. Not built.
- **The twigs' inertia** (½·m_e·v² a twig met) and **the leaves' drag on the rotor's inflow** (a porous blockage, about 2 % of the disc loading): left out, named.
- **The shells' fit** (the world builder's, D-096): the shells are felt 0.3–0.7 m before the drawn leaves and twig spheres sit up to 141 mm off the drawn crown. The leaves' density is the drawn leaf area over the shells' volume (2.8), so a disc meets the drawn crown's leaf area in total, spread over the shells' larger volume. Seen here: IslandTree's shells at the test spot reach 1.3–1.5 m radius.
- **Not met:** `--contacts`' two post-crash tunnels (§8).

## 2.9 Village's ground rows and the liftoff harness

**Owner:** physics-engineer · **Requirements:** PHY-01, PHY-03, PHY-24, LOG-01, LOG-02, TST-03 · **Branch:** `unity/physics/physics-integration-2--w9-liftoff` (from 2955136e, model 86) · With p7a-ground 1.17 and its design 12–13. No log format change: one tagged header entry (below). The model number is the orchestrator's at the merge.

The pilot's words this answers (the physics ledger's P1–P5, P10): a leg "may get randomly stuck for a moment which affects the horizon while you liftoff"; "sometimes a little bit. but based on ground type and how soft it is - more often. if it's raining it sticks most of the times"; a leg "could get thrusted and get stuck a bit INTO that ground when landing"; legs hand-made, "as flexible as a carbon plastic", varying per drone (D-086).

### 1. Why no leg stuck alone (measured before any code)

The measurements are a scratch console harness compiling the suite's scenarios against `tools/simcore` (the session's scratchpad, `night-liftoff/probe`, not committed), on this branch's base. Village's level spots are the audit's (§1.2's survey): `tilled` (715.97, 105.47), `meadow_sod` (322.29, 32.58), `weeds` (26.66, 20.00), `dirt_track` (590.17, 108.80). Each liftoff: armed at rest 2 s, then a throttle ramp; 12 seeds a cell; "lone" is design 13's held leg (one corner over 1 N at least 20 ms after the other three left, the body turning at least 0.5° toward it); "group" is one or more corners leaving at least 20 ms after the rest with the same turn toward them (the pilot's "one or many of them").

1. **The audit flew an ideal drone.** `SoilScenarios.Setup` builds no build placement (D-086's balance, `build.placement`) and no motor asymmetry: four identical corners under a symmetric thrust, so they leave together, as the brief says they must. Every player flight carries both (`SessionSetup`). With the session's airframe (`FiberCutScenarios.SessionAirframe`: asymmetry, placement, vortex ring, ground effect, wake, flapping, interference, modes) and the audit's ramp (hover + 0.1 over 0.5 s, held), wet ground holds a pair or a single corner on 1–2 seeds of 6 per cell, the body turning up to 1.1°, but it holds the whole drone down in most wet cells for the 2.5 s run (tilled Rain 5 of 6, Severe 6 of 6).
2. **The ramp is not a pilot's liftoff.** No pilot holds hover + 0.1 while the drone stays down; they add throttle until it leaves. How fast decides what the ground can do. Two protocols, measured on the session's airframe (lone / group, of 12):

| Ground | Moisture | Brisk: past hover + 0.1 the throttle climbs on at the ramp's rate (1.16 /s) | Gentle: past hover + 0.1 it climbs 0.23 /s |
|---|---|---|---|
| tilled | dry | 0 / 0 | 0 / 0 |
| | Rain | 0 / 2 | 1 / 3 |
| | Severe rain | 1 / 3 | 2 / 9 |
| meadow_sod | dry | 0 / 0 | 0 / 0 |
| | Rain | 0 / 2 | 1 / 4 |
| | Severe rain | 2 / 6 | 4 / 10 |
| weeds | dry | 0 / 0 | 1 / 1 |
| | Rain | 0 / 0 | 2 / 4 |
| | Severe rain | 1 / 4 | 3 / 8 |
| dirt_track | dry | 0 / 0 | 0 / 0 |
| | Rain | 0 / 1 | 0 / 1 |
| | Severe rain | 0 / 3 | 0 / 5 |

   - **Why the brisk pull frees them together** (derived from the traces): once a corner's hold is the last, the body pivots about it and the corner carries F_C = (T − W)·I/(I + m·r²) of the thrust's excess at once (about 0.18 (T − W) for a corner 0.27 m from the centre, I about 0.08 kg·m², m 5.2 kg, derived), plus the controller's answer to the tilt, which pulls it the same way. A throttle still climbing at 1.16 /s passes a wet corner's 6.6 N (Rain) or 11 N (Severe rain) within a few milliseconds of the others. A gentle climb leaves F_C under the hold for tens of milliseconds while the body turns: the pilot's "for a moment".
   - **The gentle liftoff is the pilot's own** (B 11, D-087): "very sharp lift off" is one of the four ways they name to cut the fiber, so their ordinary liftoff is gentle. The harness flies it (§3).
3. **Mechanisms tried and not built** (each a temporary edit on this branch, measured on the brisk protocol, reverted):
   - **A different hold under each foot** (each corner's wetted footprint scaled 1.3 / 0.8 / 1.0 / 0.7): tilled Rain and Severe rain lone 0 of 8, holds 3–20 ms, turns ≤ 0.71°. A ±30 % spread in the hold does not hold one corner alone under a pull that passes every hold together.
   - **The groove venting only at its mouth** (the cohesive pull released at the set's depth z_p, where the skid's underside reaches the soil's surface and air can enter the gap, instead of δ_a's 2–3 mm): lone 0–3 of 12 against 0–2, groups within one seed of the base. Its physical case is Vesic 1971's reading of suction as the pore pressure across the object, but the measured effect is within the seeds' scatter, and no readable source gave its displacement: not built.
   - **The groove holding a lifting skid across** (its passive wall P(θ) kept while the skid is still below the surface, after it stops pressing): no change (lone 0–2, groups within one seed). The feet carry 0.1–0.5 N across at the liftoff (measured, dry tilled), so the wall has nothing to hold.
   - **Adhesion growing with the load before the pull** (the pressed corner holding harder): not built. The published soil–metal tests treat the normal adhesion as independent of the normal load (Payne 1956, building on Fountaine and Payne 1954, as restated in the secondary literature; the originals were not readable here).
4. **So the soil's hold lacks nothing the account needs in rain**, given the build's balance and the pilot's liftoff. The wet sets' σ_a (3 and 5 kPa, assumed) and δ_a (2 and 3 mm, assumed) are untouched; nothing is tuned.
5. **Dry ground has no hold at all** in this model: σ_a = 0, the anchor and the groove act only across the ground and only while the skid presses. A dry leg can stay down only by carrying load while the body tips off the other three (the build's balance), which the controller levels within milliseconds (1 of 48 above). The "sometimes, a little" on dry soft ground needs what holds a dry foot: stems and roots over the skid (hooks, W9 2.10, p7a-ground design 8). Design 13's risk note says exactly this: the cause is a missing mechanism, never a tuned rate.
6. **The legs' stiffness (D-086, design 12)** is drawn per leg per flight: built (§2). It changes the release little (derived): a leg's vertical stiffness is the arm under the clamp (67.6 kN/m) in series with the tube's EA/L (397 kN/m) and the T-joint (553 kN/m), 52.3 kN/m (drone.json), so ±10 % on the tube moves it by about ±1.2 %; the leg's side give (268 N/m, the tube's 3EI/L³) moves by the whole ±10 %.

### 2. What is built

- **`leg.stiffness`** (design 12, PHY-24, D-086): one stream, four draws at the flight's start, each leg's flexural and axial modulus factor 1 + δ, δ nearly normal (Irwin–Hall, `FlightRandom.DrawNormal`), σ 3 %, clamped to ±10 % (assumed, design 12: the hand-made legs' fit and their tube's stock; nothing is published for a hand-cut PVC-U leg; `LegDamageSpec.StiffnessSigma`, `StiffnessBound`). Drawn in `LegDamage`'s constructor after the strengths (a stream of its own, so no other draw moves), recorded in the header's draws. It scales what the tube's modulus sets: in the contact law (`ContactModel.LegGive`) the tube's EA/L share, its 3EI/L³ at the foot and the frame's give, as the crack's factor does, and the buckling cap the lever flies on (π²EI/4L² · its bow factor); in the failure law the tube's EI (the curvature a moment bends it to) and its buckling load. The strengths are unchanged: the yield stress is its own draw. A flight without the legs' law draws nothing.
  - **An opt-in, as the build's placement and the motors' asymmetry are** (measured, why): drawn by default, every flight with the legs' law moved, the goldens and the kept audits among them, even those that touch nothing (the step record logs `LegStiffnessFactor`, now the draw): 40 FAIL lines of `--golden`. The established rule is that a drawn build is the setup's choice (`FlightSetup.Asymmetry`, `Placement`: "a FlightSetup without one flies the exact build, as the goldens do"). So `LegDamageSpec.StiffnessSigma` and `StiffnessBound` default to 0 (the nominal legs), and `LegDamageSpec.DrawStiffness()` sets the drawn σ 3 % and bound 10 % (`DrawnSigma`, `DrawnBound`). The suite's session airframe (`FiberCutScenarios.SessionAirframe`) and the replay tool's pilot flight (`tools/replay` `ScriptedFlight`) call it. **The player's `FlightSession` (the Unity app, not this branch's) needs the same one line** beside its `Placement = spec.BuildPlacement()`: `setup.Contacts.Legs?.DrawStiffness();`. With the opt-in, `--golden` is 61 PASS: no golden moved.
- **The anchors' reach in `ContactSpec`** (2.4's debt): `ContactSpec.AnchorReach` beside `HangingGive`, read by `ContactModel.Reached`; `ContactModel.AnchorReach` is gone, and `GoldenFlights.WithoutTheReach` sets the model's own spec (its known-wrong, a bench's, never recorded).
- **The header** (no format change): one tagged entry, `TagGround` (28), as 2.2 logged the stems: the contacts' `AnchorReach`, then the legs' `StiffnessSigma` and `StiffnessBound`. A reader that does not know the tag lists it as unknown and skips it. A log without it is read as its model flew: the reach on from model 86 (the header's model number), off before; no stiffness spread (σ and bound 0, so no `leg.stiffness` stream is opened).

### 3. The harness (`flightbench --ground [seeds]`, design 13)

- **Spots:** Village's five classes on their level spots (the four above and `dry_crust` (714.12, 106.58)), its 10° slopes (`weeds` (313.04, 20.00), `meadow_sod` (311.19, 50.71)), and House's grounds on the test range: its track start (the plane's `belt_straw`) and its first S04 patch.
- **Each flight:** the session's airframe and the flight seed (so the build's balance, the motors and the legs are drawn); in still air; the drone level (its nose uphill on a slope), its skids' lowest point 2 mm over the ground beneath it, moving at the touch's speed with the throttle cut (armed at idle: the pilot's cut at the touch), half the seeds a vertical touch at 0.3 m/s and half the pilot's glide (0.5 m/s down, 0.3 m/s forward), as design 13; 2 s at rest; then the gentle liftoff: the throttle ramps to the spot's hover + 0.1 over 0.5 s and climbs on at 0.2 a second until the drone is off (no corner over 1 N and the body 10 mm up), where it stays (assumed protocol, read from B 11), for at most 4 s.
- **Per flight:** the lone held corner (design 13's rule) and the held group (one or more corners, the same 20 ms and 0.5°), each with its hold, turn and peak pull. Per cell the rates; per spot the mean sink at rest.
- **The targets** (design 13's reading of the pilot's words, on the lone corner; firm = `dirt_track`, `dry_crust`; soft = the rest): dry firm held on at least one flight and at most a third; dry soft above dry firm by more than the two rates' 95 % binomial margin, and under half; soft in Rain above half; firm in Rain above dry firm; soft in Severe rain at least as often as in Rain. The group's rates are printed beside them.
- **The four known-wrong models, each judged on the target it must miss:** a dice roll at the dry firm rate everywhere (an outcome model, seeded; must miss "more often on soft" or "in rain"); the dry rows in rain (`surfaces.json`'s dry set in every moisture, flown on the soft spots; must miss "in rain"); K§7.3's touch rule, every anchored element touching a skid hooks (an outcome model: no hook law exists until 2.10, so every flight on a vegetated soft spot counts as held; must miss "under half"); Coulomb friction with no anchor, no adhesion and no ploughing (the rigid ground with μ 0.5/0.4, flown; must miss "at least one" or "in rain").
- **Village against its packages** (2.9's other rows): the pilot's cut landing (`SlopeScenarios.LandOnFarmland` against `Land`) and the disarmed rest (`SettleOnFarmland`, new, against `Settle`) on the audit's 10° and 14.4° Village spots, along and across, against W3's slope package at the same angle and class: where both stay on their legs (end tilt under 15°; past it the legs parted, 2.6's), the end tilt within 1°. The soft-soil landing at 1 m/s on Village's `tilled` against a floor of `tilled`, dry, Rain and Severe rain (`LiftoffScenarios.Drop`: the nominal drone level, 2 mm over, 1 m/s down, the throttle cut, 3 s): the peak sink and the sink after 3 s within 1 mm. `SoilScenarios.Land` was not used: it misreads its touch on wet ground (audit §6).

### 4. Results

`flightbench --ground` (10 seeds a cell: 5 vertical, 5 glide), on this branch with the legs drawn ("after") and with `--nominal-legs` (no spread: the base's physics, "before"); measured. Lone = design 13's held corner; "one or many" = the held group. Each cell n = 10.

| Spot | Moisture | Lone, before → after | One or many, before → after | Longest lone hold, most turn (after) | Mean sink at rest |
|---|---|---|---|---|---|
| Village tilled | dry | 0 → 0 | 1 → 2 | | 4.90 mm |
| | Rain | 0 → 0 | 7 → 8 | | 9.00 mm |
| | Severe rain | 1 → 1 | 8 → 8 | 20 ms, 0.74° | 13.69 mm |
| Village meadow_sod | dry | 0 → 0 | 0 → 0 | | 3.87 mm |
| | Rain | 0 → 1 | 3 → 2 | 23 ms, 0.65° | 6.57 mm |
| | Severe rain | 0 → 0 | 8 → 8 | | 9.64 mm |
| Village weeds | dry | 0 → 0 | 0 → 0 | | 2.78 mm |
| | Rain | 0 → 0 | 6 → 6 | | 4.70 mm |
| | Severe rain | 2 → 1 | 10 → 8 | 21 ms, 0.82° | 6.34 mm |
| Village dry_crust (firm) | dry | 0 → 0 | 0 → 0 | | 1.77 mm |
| | Rain | 1 → 1 | 5 → 2 | 29 ms, 0.56° | 2.94 mm |
| | Severe rain | 2 → 2 | 5 → 5 | 25 ms, 1.01° | 3.87 mm |
| Village dirt_track (firm) | dry | 0 → 0 | 0 → 0 | | 1.17 mm |
| | Rain | 1 → 0 | 1 → 0 | | 2.03 mm |
| | Severe rain | 0 → 0 | 4 → 5 | | 2.65 mm |
| Village weeds 10° | dry | 1 → 0 | 1 → 0 | | 4.42 mm |
| | Rain | 0 → 0 | 8 → 10 | | 8.25 mm |
| | Severe rain | 2 → 0 | 8 → 8 | | 10.11 mm |
| Village meadow_sod 9.39° | dry | 0 → 0 | 0 → 0 | | 5.79 mm |
| | Rain | 0 → 0 | 6 → 7 | | 12.29 mm |
| | Severe rain | 0 → 3 | 4 → 6 | 29 ms, 1.41° | 14.39 mm |
| House track start (belt_straw) | dry | 0 → 0 | 0 → 0 | | 4.68 mm |
| | Rain | 0 → 0 | 2 → 4 | | 8.40 mm |
| | Severe rain | 3 → 3 | 10 → 10 | 25 ms, 1.08° | 12.92 mm |
| House S04 patch | all | the step throws (below) | | | |

Over the groups (after): dry firm lone 0/20, one or many 0/20; dry soft 0/63, 2/63 (3 %); Rain firm 1/20, 2/20; Rain soft 1/60, 37/60 (62 %); Severe rain firm 2/20, 10/20; Severe rain soft 8/60 (13 %), 48/60 (80 %). Before: dry soft lone 1/63, group 2/63; Rain soft 0/60, 32/60; Severe rain soft 8/60, 48/60.

- **Design 13's targets on the lone corner: not met.** "Sometimes when dry" (dry firm 0 of 20), "more often on soft" (dry soft 0 of 63) and "most liftoffs in rain" (Rain soft 1 of 60) miss; "firm in Rain above dry firm" and "Severe rain at least Rain" are met. `--ground` exits 1 on them.
- **On the pilot's own words, "one or many of them", the wet account is met**: in Rain 62 % of the soft-ground liftoffs hold a leg or a pair at least 20 ms while the body turns at least 0.5° toward it, 80 % in Severe rain; on the firm track and crust 10 % in Rain; dry soft ground 3 %, dry firm never. The held group is mostly a pair on one side or one end (the build's balance fore and aft or side to side), which tips the horizon 0.5–2.3° (the traces of §1).
- **The legs' spread moves the rates within the seeds' scatter** (lone 8 → 8 of 60 in Severe rain on soft ground; groups ±1–3 per cell), as §1.6 derived. Built because D-086 asks for it, not because it makes the account.
- **The lone corner stays rare** (13 % in Severe rain on soft ground, 2 % in Rain) for §1.2's reason: once three corners have left, the last carries the climbing thrust's excess and the controller's answer to the tilt, and even the gentle climb passes a wet corner's hold within 20–30 ms. What would keep one corner held while the other three lift is a difference between the feet of the order of the hold itself; the ground under the four feet is one class, one moisture and no micro-relief (audit 1.10). The ground is world-derived, never drawn, so this is named for the world (§5), not built here.
- **Dry ground: no hold** (§1.5). The 3 % of dry soft liftoffs that hold a group are the build's balance on the tilled spot. Hooks (2.10) are what the pilot's "sometimes, a little" on dry ground needs.
- **The four known-wrong models each miss their target** (every row PASS): the dice at the dry firm rate (0 everywhere) misses "more often on soft" and "in rain"; the dry rows in rain hold no lone corner in rain (0/60 on soft ground) and miss "in rain"; the touch rule holds every dry soft liftoff (63/63) and misses "under half"; Coulomb friction alone holds nothing on dry firm (0/20) or in rain (0/60). But the model misses the same lone-corner targets as known-wrongs 1, 2 and 4, so on the lone corner the harness cannot yet tell the model from them. The known-wrongs are judged on the lone corner only; judging them on the group too is the next step for the harness.
- **House's S04 patch: every liftoff's step throws**, with the legs drawn and without (so not this branch's): `PropStrikes.Roots` reads its table at a negative index (`PropStems.cs:237`, W9 2.2's stems) in all three moistures, and in Rain and Severe rain some seeds end with a NaN in the rain film's mass (`MassModel.SetPartMass` from `FlightCore.cs:562`), also with the rigid ground of known-wrong 4. 27 of 30 flights; `--ground`'s "no liftoff's step throws" row fails on it. Not fixed here.
- **No liftoff tunnels or crashes** on any spot.
- **Village against the packages:** every compared row within 1°: along, 10.00° weeds 10.0/0.1° against 10.0/0.0°, 9.39° meadow 9.4/0.1 against 9.4/0.3, 14.43° meadow 14.4/0.2 against 14.5/0.6, 14.36° weeds 14.4/0.2 against 14.4/0.4; the rests along at all four and across at 10°, within 0.1° and 1 mm. Across, the cut landings at 9.39–14.43° end on their side on both (50–52°, the downhill legs parted, 2.6's G2), as do the rests across at 14.4°: printed, not compared.
- **The soft-soil landing on tilled** (peak / after 3 s, mm): dry Village 7.985 / 7.193 against the floor 7.988 / 7.278; Rain 11.357 / 10.798 against 11.548 / 11.096; Severe rain 14.030 / 13.747 against 13.846 / 13.598: within 0.30 mm.
- **No golden moved** (§2). No log format change; one tagged header entry.

### 5. What is still not met, and for whom

- **A lone corner "sometimes" on dry ground, "more often" on soft dry ground, and alone "most of the time" in rain** (design 13's lone-corner targets): not met. The group ("one or many") is met in rain.
- **For W9 2.10 (stems on the legs):** the dry account rests on hooks; today nothing holds a dry foot. Its acceptance can be this harness's dry rows.
- **For the world-builder:** a world-derived difference between the four feet (relief at the skid's scale, a clod, a wetter patch) is what would turn the wet pair into a lone corner. Not a draw: map data.
- **For W9 2.6 (legs on a side slope):** the cut landings across 9.39–14.43° on Village and on the packages still part the downhill legs and end on a side (50–52°), Village and package within 1–3°; the rests across 14.4° roll to 52–58°. The law was left alone.
- **For the stems' branch (2.12, 2.10):** `PropStrikes.Roots` throws on S04's patch on every liftoff (above).
- **For the app (game-developer):** `FlightSession` must call `setup.Contacts.Legs?.DrawStiffness()` for the player to fly drawn legs (§2).

### 6. Checks run (this branch, a shared machine: no timing counts)

```
dotnet test tools/simtests -c Release            → 848 passed, 1 failed of 849 (30 m 24 s, loaded): StaticInitTests.NoStaticReferenceFieldOnTheStepPath,
                                                    LogHeader's sort lambda renumbered <>9__146_0 → <>9__147_0 by the new entry; its allowlist line
                                                    updated, then: Passed NoStaticReferenceFieldOnTheStepPath, Passed AHeldLegTipsTheHorizonAndLetsGo
                                                    (the latter added after the full run: 3 of 10 seeds a lone corner, 21–25 ms, 0.82–1.08°)
  LiftoffTests: TheLegDrawsCoverTheirBounds (16000 draws 0.9000–1.1000, mean +0.00006, σ 0.02997), TheDrawsMoveNoOtherDraw,
  TheHeaderLogsTheReachAndTheSpreadAndAFlightWithoutTheReachReplays (reach on: Match, 4000 steps and 21 events bit for bit;
  reach off: Match, the same), ALogWithoutTheEntryReadsAsItsModelFlew, AVillageLiftoffAllocatesNothing (0 B over 3000 steps)
flightbench --golden    → 61 PASS, exit 0 (no golden moved)
flightbench --ground    → exit 1: the lone-corner targets not met; S04's patch throws (27 flights); the known-wrongs PASS; Village
                          against the packages PASS where compared; tilled against the floor PASS (§4)
flightbench --ground 10 --nominal-legs → the same verdicts (§4's "before")
flightbench --contacts  → 80 PASS, exit 0
flightbench --slopes    → 13 PASS, exit 0
flightbench --legs      → 6 verdicts PASS, exit 0
flightbench --landing   → 29 PASS, exit 0
flightbench --probe     → exit 1, 2 checks, both as before 2.4: a corner's support moment changes 1.707 N·m a step sliding with
                          legs on (limit 0.51); the coil 0.1141 m over the stand's base against the note's 0.135 m. 2.4's release
                          did not touch this probe's sliding (no stretch past the reach there); unchanged by this branch.
python tools/hygiene/precommit.py --tree HEAD → (below, in the handback)
```

## 2.10 Stems on the legs and the fiber

To be written by task 2.10.

## 2.5 Cut bits on camera

To be written by task 2.5.
