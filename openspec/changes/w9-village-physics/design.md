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

## 2.6 The legs on a side slope (G2)

To be written by task 2.6.

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

## 2.3 Leaves in crowns

To be written by task 2.3.

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
- **The anchors' reach in `ContactSpec`** (2.4's debt): `ContactSpec.AnchorReach` beside `HangingGive`, read by `ContactModel.Reached`; `ContactModel.AnchorReach` is gone, and `GoldenFlights.WithoutTheReach` sets the model's own spec (its known-wrong, a bench's, never recorded).
- **The header** (no format change): one tagged entry, `TagGround` (28), as 2.2 logged the stems: the contacts' `AnchorReach`, then the legs' `StiffnessSigma` and `StiffnessBound`. A reader that does not know the tag lists it as unknown and skips it. A log without it is read as its model flew: the reach on from model 86 (the header's model number), off before; no stiffness spread (σ and bound 0, so no `leg.stiffness` stream is opened).

### 3. The harness (`flightbench --ground [seeds]`, design 13)

- **Spots:** Village's five classes on their level spots (the four above and `dry_crust` (714.12, 106.58)), its 10° slopes (`weeds` (313.04, 20.00), `meadow_sod` (311.19, 50.71)), and House's grounds on the test range: its track start (the plane's `belt_straw`) and its first S04 patch.
- **Each flight:** the session's airframe and the flight seed (so the build's balance, the motors and the legs are drawn); in still air; the drone's skids 0.1 m over the spot, falling with the throttle cut (armed at idle), half the seeds a vertical touch at 0.3 m/s and half the pilot's glide (0.5 m/s down, 0.3 m/s forward), as design 13; 2 s at rest; then the gentle liftoff: the throttle ramps to the spot's hover + 0.1 over 0.5 s and climbs on at 0.2 a second until the drone is off (no corner over 1 N and the body 10 mm up), where it stays (assumed protocol, read from B 11), for at most 4 s.
- **Per flight:** the lone held corner (design 13's rule) and the held group (one or more corners, the same 20 ms and 0.5°), each with its hold, turn and peak pull. Per cell the rates; per spot the mean sink at rest.
- **The targets** (design 13's reading of the pilot's words, on the lone corner; firm = `dirt_track`, `dry_crust`; soft = the rest): dry firm held on at least one flight and at most a third; dry soft above dry firm by more than the two rates' 95 % binomial margin, and under half; soft in Rain above half; firm in Rain above dry firm; soft in Severe rain at least as often as in Rain. The group's rates are printed beside them.
- **The four known-wrong models, each judged on the target it must miss:** a dice roll at the dry firm rate everywhere (an outcome model, seeded; must miss "more often on soft" or "in rain"); the dry rows in rain (`surfaces.json`'s dry set in every moisture, flown on the soft spots; must miss "in rain"); K§7.3's touch rule, every anchored element touching a skid hooks (an outcome model: no hook law exists until 2.10, so every flight on a vegetated soft spot counts as held; must miss "under half"); Coulomb friction with no anchor, no adhesion and no ploughing (the rigid ground with μ 0.5/0.4, flown; must miss "at least one" or "in rain").
- **Village against its packages** (2.9's other rows): the pilot's cut landing and the disarmed rest on the audit's 10° and 14.4° Village spots, along and across, against W3's slope package at the same angle and class: where both hold, the end tilt within 1°. The soft-soil landing at 1 m/s on Village's `tilled` against a floor of `tilled`, dry, Rain and Severe rain: the peak and resting sinks within 1 mm.

### 4. Results

To be filled by the runs.

## 2.10 Stems on the legs and the fiber

To be written by task 2.10.

## 2.5 Cut bits on camera

To be written by task 2.5.
