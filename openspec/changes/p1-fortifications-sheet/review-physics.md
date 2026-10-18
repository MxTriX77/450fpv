# Physics reading: the fortifications sheet (F01–F04)

**Reviewer:** physics-engineer · **Task:** 1.3 · **Branch:** `unity/physics/p1-fortifications-sheet--reading` · **From tip:** `c520ea6` (`unity/ref/p1-fortifications-sheet`)
**Against:** AST-05, PHY-13, PHY-19, PHY-20, PHY-21, PHY-22, D-024, D-035; knowledge §5.4, §6.7, §6.8, §7, §8.7, §10.3–§10.5; the merged materials sign-off (`archive/2026-10-18-p1-pipeline-hardening/review-materials.md`, rules 1–5); the V01 shell-cap reading (`p1-v01-locust/review-physics.md` §2, §4, N-b: records carry `material`, the binned 2 m cache, the 64-piece neighbourhood cap)
**Read:** `CLAUDE.md`, `AGENTS.md`, `docs/workflow.md`; this change's proposal, design 3–5 and tasks; `docs/object-sheets/fortifications.md` (F01–F04, "Proposed physics ids", "Open points across the sheet"); `assets-src/physics_materials.json`; `tools/blender/lib/meta.py` (what a snag record and a shape carry today); `docs/legacy/flight-model/` (design §7, `specs/fiber-tether.md`); `docs/legacy/map-and-world/surfaces-review.md` §5–§6 and `api-review.md` §2–§4

I consume these numbers: the 1 kHz contact law (4 legs, 4 arms, 4 prop discs, body and coil against shapes and snag records), the fiber's bend, capstan and cut-risk model (PHY-18, PHY-19), prop strikes (PHY-05, PHY-22), the wind grid (PHY-13) and the log. I rule by calculation (D-024); I do not edit the sheet. Every number is labelled **published / derived / assumed**; every assumed number is a named, tunable parameter. The arithmetic ran in plain Python scripts in my scratch folder (not committed); §10 has their output. No Unity, no Blender.

## Verdict

**The sheet is sound and buildable for physics after ten changes (§8).** None changes a gap verdict or the coil's porosity; two change material values, one changes F02's collision primitive.

1. **F01 records:** one `edge` record per turn stays. It carries both radii: `radius_m` 0.008 is the barbs' reach, and two new optional fields carry the blade and the barbs: `edge_radius_m` 0.00025 and `barb_pitch_m` 0.035. This is additive and stays schema 1; every existing record keeps its meaning (§1).
2. **Cut or hold:** both. A barb holds a slack line and cuts a loaded one. It cuts when the bend stress at the blade reaches the flight's drawn strength, at **T > T_c = EI·σ_f² / (4 (E·r)² sin²(θ/4))** (derived). At a 90° turn of the line that is **0.034 N** on the weakest draw (0.69 GPa) and **1.0 N** at 3.8 GPa. While the drone flies on, the line at a barb carries 0.03–0.5 N. So a line pulled round a barb is a probable cut; a line draped over a run (≤ 45° a turn) holds on all but the weakest draws (§2).
3. **Porosity:** 0.95 / 0.94 / 0.88 / 0.75 **accepted** (§3).
4. **Ids:** `razor_wire` and `sandbag` **accepted as new ids**, both with corrected stiffness, because the contact law applies a row **per touching part**, not per drone. `razor_wire` is **60 N/m** (one loop under a part), not 200. `sandbag` is **900 N/m** (a 15 mm foot punches into loose fill), not 1.6e5. Full rows are in §4.
5. **Gaps:** every F02–F04 verdict **confirmed**. One correction: the sandbag entrance's 0.70 m must be the clear width between the bulging bag faces (§5).
6. **Teeth:** 11 stacked boxes hold ±2 cm on the faces (18.7 mm) but **not at the arrises (24.4 mm)**, which bound the diagonal lane. They also replace a 65.8° face with 41 mm ledges that a foot can rest on, and they cannot represent the triangular variant. **The asset change needs a `convex` primitive**; §6 lists what the world-query core must add.
7. **Budget:** the records never go through a per-step scan. Through the binned 2 m cache, the densest default stack (1,096 segments in a 2 m cube) costs **2.4–4.8 µs** a step (derived). Phase 3 must add a spatial index over records and gaps, a record query feeding the cache, and a **2,048-segment cap per 2 m cube** (§7).

## Constants this reading uses

| Quantity | Value | Label |
|---|---|---|
| Fiber glass | 125 µm silica, r = 62.5 µm; coating to 245–250 µm, c = 62.5 µm | published (ITU-T G.652 / G.657 class; the 0.25 mm spool fiber, knowledge §5.1) |
| Young's modulus of the glass | E = 73 GPa | published (fused silica, 72–73 GPa); it reproduces the legacy E·r/R figures (12.6 GPa over 0.3 mm, 4.3 over 1 mm) |
| Bending stiffness | EI = 8.75e-7 N·m² (glass only; the coating adds ≤ 15 %, left out, which overstates the stress) | derived |
| E·r | 4.56e6 N/m | derived |
| Linear mass | 0.0675 g/m, so 6.6e-4 N/m | derived (2200 kg/m³ glass, ≈ 1100 kg/m³ coating, assumed); the spool's "1 kg ≈ 10 km" includes the bobbin |
| Proof stress (every metre survived it) | 0.69 GPa (100 kpsi), so a tension break of **8.5 N** | published (Telcordia GR-20 / FOTP-31 proof level; sources below); the break derived |
| Pristine strength | bending Weibull means 6.5–7.6 GPa; Weibull modulus 30–100 for telecom fiber, 3–7 for abraded glass | published (sources below) |
| Per-flight strength draw σ_f | bounded **0.69–5.0 GPa**; the distribution is the phase-7 tether change's (O-5) | floor published (proof); ceiling assumed under the published pristine bending strength |
| μ of the fiber's acrylate on steel | 0.35 / 0.28 | assumed (the `steel` row; surfaces-review: "the fiber's acrylate coating" is a polymer part) |
| Drone | 0.574 m nose-first, 0.706 m diagonal; legs a 0.24 m square, envelope 0.55 m; 4.8 kg nominal | derived (knowledge §5.4), assumed (legs, as the sheets) |
| Step costs | 30 ns a narrow test, 5 ns a box reject, 50 ns a part's bins; an 8.3 µs share inside the 60 µs world-query budget | assumed (the V01 reading's figures, reused) |

## 1. F01's snag records

**Ruling: one `edge` record per turn, carrying three physical values.**

| Field | Value (default profile) | Meaning | Label |
|---|---|---|---|
| `radius_m` | **0.008** | The member's radius: how far the barbs reach from the core. Props, legs and the fiber touch the record within it, and it inflates the record's bounds | published (half of the 15–21 mm across the barbs) |
| `edge_radius_m` (new, optional) | **0.00025** | The radius the fiber bends over at this record, where the member carries a sharper edge than its own radius. Absent means `radius_m` | derived (half the published 0.5 mm tape) |
| `barb_pitch_m` (new, optional, `edge` only) | **0.035** | The spacing of the barb clusters. A fiber pressed onto the record is locked along it to within half a pitch (17.5 mm) of where it touched: it sits in the nearest notch. It can still slip along its own length (the capstan, §2). Absent means a smooth member | published |
| `material` | `razor_wire` on turns; `steel` on clips, tie-wire tails and pickets | Friction for the capstan (V01 reading §4) | — |
| points | 24 a turn, closed | unchanged (the sheet's sagitta rule against the 8 mm reach) | derived |

The default turn record reads `{"kind": "edge", "points": [24 points], "radius_m": 0.008, "edge_radius_m": 0.00025, "barb_pitch_m": 0.035, "material": "razor_wire"}`. The generator writes `edge_radius_m` = `tape_thickness_m` / 2 and `barb_pitch_m` from the profile.

**Why not the other two forms:**
- **A split into tape and barb records.** There are 81 clusters a turn (π · 0.90 / 0.035, derived), so 4,524 records per 15 m coil and 302 per metre of run against 3.7. That is 80 times the records, for nothing the pitch field does not already carry.
- **The blade on the material, with `radius_m` the tape's.** Materials rule 4 says a snag's `radius_m` overrides the material's edge, so this needs an exception keyed on one material. A general "smaller of the two" rule would change existing records: a 5 mm `twig` record would bend the fiber over `twig`'s 2.5 mm. The two published blade thicknesses (0.25 and 0.30 mm) would also need two ids.

**The rule text the asset change writes:**
- **`physics_materials.json` `fields.edge_radius_m`:** the sentence ends "… and a snag's `radius_m` overrides both, unless the record carries its own `edge_radius_m`, which the fiber then bends over".
- **The pipeline spec's metadata requirement:** each snag record has "a kind, at least two points, a radius (its reach), the physics material of the member it follows, and optionally an edge radius and a barb pitch".
- **`meta.validate`:** both fields optional and positive; `edge_radius_m` ≤ `radius_m`; `barb_pitch_m` only on `edge` records. Today the validator passes unknown record keys unchecked, so nothing breaks before then.

**The cluster length is not recorded.** The sim treats the whole tape as blade (derived, conservative):
- In the 13 mm between clusters, the line bears on the crimped band: R ≈ 1.75 mm (the 1.25 mm core plus 0.5 mm of strip, derived), which caps the bend stress at 2.52 GPa.
- At a 90° turn, the band and the blade give the same stress below 0.46 N, because the fiber's own stiffness sets the bend there (§2).
- So the difference appears only on draws above 2.5 GPa at loads above 0.46 N. That does not earn a fourth field.

## 2. Cut or hold

**Ruling:** a barb is a hold-and-cut contact.
- **It holds** the line by geometry (the notch, `barb_pitch_m`) and by friction (the capstan e^(μθ)).
- **It cuts** when the bend stress at the blade reaches the flight's drawn strength σ_f, at **T > T_c(θ, σ_f) = EI·σ_f² / (4 (E·r)² sin²(θ/4))** (derived).
- **It abrades** the coating wherever the line rubs.

### 2.1 The bend over a blade (PHY-19's mechanism, made exact for the deflection)

- **The model.** Knowledge §6.8 bends the fiber at R_eff = max(R_edge + c, √(EI/T)). Over a point edge, the planar elastica of a tensioned rod gives EI·κ²/2 = T·(1 − cos φ). At the apex φ = θ/2, with θ the line's turn at the contact (derived). So:
  - κ = (2/λ)·sin(θ/4), with λ = √(EI/T)
  - **R_eff = max(R_edge + c, λ / (2 sin(θ/4)))**, and σ_b = E·r / R_eff
  - At θ ≈ 2 rad this is the legacy √(EI/T); for the gentle turns of a line draped over a coil it is up to four times gentler.
  - It is DetMath-safe: cos θ = −â·b̂ from the two span directions, cos(θ/2) = √((1 + cos θ)/2), sin(θ/4) = √((1 − cos(θ/2))/2). Only + − × ÷ √.
- **Over a blade, the edge never protects.** R_edge + c = 0.3125 mm, which caps the bend at **14.6 GPa**, above every possible draw (derived). The fiber's stiffness sets the bend at every tension below about 9 N. Compare the caps elsewhere: a twig (2.5 mm) 1.78 GPa, `steel` (1 mm) 4.29, `concrete` (3 mm) 1.49, `fabric` (5 mm) 0.90, a sandbag shoulder (50 mm) 0.09.
- **Below about 0.2 N at a 90° turn, a barb and a twig bend the line the same way.** The razor only differs once the twig's cap would bind.

**Threshold tension T_c (N)**, with the bend radius at T_c in brackets (derived):

| σ_f \ θ | 15° | 30° | 45° | 90° | 180° |
|---|---|---|---|---|---|
| 0.69 GPa (the floor, proof) | 1.17 (6.6 mm) | 0.29 | 0.13 | **0.034** | 0.010 |
| 1.38 GPa | 4.68 (3.3 mm) | 1.17 | 0.53 | 0.14 | 0.040 |
| 3.8 GPa | 35.5 (1.2 mm) | 8.9 | 4.0 | **1.04** | 0.30 |
| 5.0 GPa (the ceiling) | 61 (0.9 mm) | 15.4 | 6.9 | 1.79 | 0.53 |

**Bend stress σ_b at the blade (GPa)** (derived):

| T \ θ | 15° | 30° | 45° | 90° | 180° |
|---|---|---|---|---|---|
| 0.05 N | 0.14 | 0.28 | 0.43 | 0.83 | 1.54 |
| 0.2 N | 0.29 | 0.57 | 0.85 | 1.67 | 3.09 |
| 0.5 N | 0.45 | 0.90 | 1.35 | 2.64 | 4.88 |
| 2 N | 0.90 | 1.80 | 2.69 | 5.28 | 9.76 |

### 2.2 The tension the line carries at a barb

- **On the drone side,** the spool pays out once the line reaches its payout tension. **`payout_tension_n` is 0.05–0.5 N** (assumed; O-5, a named parameter of the tether change). On top of that:
  - the momentum flux ρ_l·v²: 0.007, 0.027 and 0.061 N at 10, 20 and 30 m/s (derived)
  - a span hanging 50 m: 0.033 N (derived)
  - crosswind drag: 0.012 N per metre of span at 8 m/s (derived; C_d 1.2 at Re 136, assumed)
- **On the far side,** the line lying back toward the launch point holds by ground friction: 0.033 N per 100 m at μ 0.5 (derived; μ assumed).
- **The capstan decides which side feeds the line.** At μ_s 0.35 the ratio e^(μθ) is 1.20 at 30°, 1.73 at 90° and 3.00 at 180° (derived). The line slips through the notch when T_drone > T_far·e^(μθ); otherwise the barb holds and the spool pays out instead. Either way, **the line at a barb carries about 0.03–0.5 N in flight**.
- **Spikes beyond that** come only when the line is caught on the drone itself (a leg or a prop: PHY-19's "backward momentum after liftoff") or wedged. Then the tension climbs to the tension break, 8.5 N at the floor, and the barb no longer matters.

### 2.3 What the two cases give

| Case | θ at a barb | T | σ_b | Outcome |
|---|---|---|---|---|
| A line draped over a run while the drone flies on | 15–45° a turn (a 45–90° wrap over the coil's top shared by 2–4 turns, assumed) | 0.05–0.5 N | 0.14–1.35 GPa | **holds**, except on draws below σ_b |
| A line snagged and pulled round a barb (a turn, a reverse, a drag along the run) | 90–180° | 0.05–0.5 N | 0.83–4.88 GPa | **a probable cut**: every draw below σ_b cuts |

This is the pilot's account (D-024, qualitative): snags cut sometimes, a sharp reverse cuts, a smooth line survives. It comes from one deterministic law and one per-flight draw.

### 2.4 Abrasion

- **The blade scores the coating at any loaded contact.** The crossed-cylinder Hertz pressure between the 0.25 mm blade and the coating is 72–123 MPa mean at 0.1–0.5 N on a 90° turn (derived; coating E 1 GPa, ν 0.4, assumed). That is above an acrylate's hardness of 0.05–0.1 GPa (assumed).
- **The wear rate:** Archard with k = 0.01–0.05 (assumed) gives 0.7–3.5 µm a pass at 0.5 N. So 18–88 rubs of the **same** fiber section wear through the 62.5 µm coating (derived).
- **What that means:** a line vibrating against a barb in wind, or sawing back and forth, wears through. A line drawn once through a notch does not.
- **Once through,** the glass at that spot is abraded glass (Weibull modulus 3–7 against 30–100, published), far below the draw. The next loaded bend there breaks it.
- **Rule for phase 7:** the abrasion integral counts rubbing of one fiber section at one contact, not the length slipped past it, at a rate set by the contact pressure. The blade's small radius makes it the fastest in the world. The rate constant stays O-5's, assumed.

### 2.5 What the sim does on each barb contact, each step (phase 7)

1. **Lock.** The contact point moves along the record by at most half a `barb_pitch_m` from where it touched. On a record without a pitch it slides freely.
2. **Capstan.** The line slips along its own length only when the high side exceeds the low side × e^(μ_s·θ), and slides at μ_k.
3. **Bend.** If σ_b(T_max, θ) ≥ σ_f, the line breaks. The `tether_break` event is logged with mechanism `bend`, the object, the record index, θ, T, R_eff, σ_b and σ_f (PHY-21).
4. **Abrasion.** While the section rubs, the integral grows. At its threshold, the break is logged with mechanism `abrasion`.
5. **Hold.** Otherwise the barb is a fixed anchor of the span: the tether solve treats it as a node held by steps 1–2.

**Three related numbers** (non-blocking):
- **Link margin (PHY-20, knowledge §8.7).** Every loaded barb contact is a snag and sharp-bend event, with R_eff of 0.9–6.6 mm at the thresholds above. The feed's link-dip model reads it.
- **Props (PHY-22).** A prop disc inside a turn's 8 mm reach strikes a 2.5 mm high-tensile core that breaks at 7.4 kN (derived at 1500 MPa, assumed). That is PHY-22's thin-wire class: it catches and wraps. The strike law takes the class from the material (`razor_wire`'s core is fixed at 2.5 mm by the published profiles), never from the 8 mm reach, which would read as a thick cable.
- **Legs.** A barb hooked into a printed leg lets go at about 50 N: the plastic moment of a 0.5 mm blade on a 5 mm root, σ_y·b·t²/4 = 0.156 N·m (σ_y 500 MPa assumed), at a 3 mm lever (assumed). The drone's excess thrust is 41–76 N (derived from knowledge §5.3 less 4.8 kg), so a leg on two barbs stays held at full throttle. The sheet's "held by the barbs" stands.

## 3. Porosity

**Ruling: accepted as written.** The sheet's relation β = exp(−G·a·s) with a = 4w/(D·p) is the open-area fraction side-on, which is what knowledge §10.3 asks of a wind volume (the legacy API review: "optical porosity side-on").

| Case | Sheet | Recomputed (G 0.5, w 8 mm) | With the tape's real orientation | Verdict |
|---|---|---|---|---|
| 0.90 m coil, stretched | 0.95 | 0.954 (axis 0.942) | 0.960 | **0.95 accepted** |
| 0.45 m fence-top coil | 0.94 | 0.938 (axis 0.922) | — | **accepted** |
| 0.90 m coil, bunched to 0.10 m | 0.88 | 0.882 (axis 0.852) | — | **accepted** |
| Trampled mat, across its 1.41 m | 0.75 | 0.755 (0.971 through its height) | 0.81 (loops lying flat) | **accepted** (immaterial: a 0.15 m layer under the 2 m grid and under every rotor) |

**The orientation check** (derived): the barbs lie flat in the loop plane, which leans 31° from a cross-run wind. The round band projects 2/π of its 3.5 mm, and the 3.93 mm mean of barbs projects sin 31° · 2/π. That gives 3.52 mm of shadow per metre of tape against the sheet's G·w = 4.0 mm, a difference of 0.006 in β.

**The pickets** (50 mm × 1.0 m every 3.8 m) shade 0.013 m² per metre of run against the coil's 0.041. Folded in, they would give 0.94. They are thin collision shapes, not wind volumes, so leave them out.

**Against the wake model** (knowledge §6.7): β > 0.3 is the porous-fence regime, and the deficit scales with 1 − β. So a run makes no measurable wake, which is the sheet's "not a windbreak". F03's envelope porosity of 0.6 was also checked (π/4 against a mean member projection of 0.80 at 35.3°): accepted.

## 4. The proposed ids

**Both are accepted as new ids. Each value is corrected to the contact law's own definition:** a row's `stiffness_n_per_m` is "the object's local stiffness under a point load … in series with the drone part" (surfaces-review §5), applied once per touching part (api-review §4). The sheet derived both figures for the whole drone.

### `razor_wire`

- **The stiffness is one loop under a part.** A loop is a ring under a diametral load, k = f·EI/((π/4 − 2/π)·R³), with EI 0.383 N·m² for the 2.5 mm core (E 200 GPa assumed). That gives 28.3 N/m bare and **57 N/m** with the crimped tape (f = 2, the sheet's figure, assumed). If the wrapped band were a bonded 3.5/2.5 mm annulus, f would reach 3.8 (derived).
- **The row takes 60 N/m.** A drone on four legs then meets 240 N/m and sinks **0.20 m** (derived). That is the sheet's 230 N/m of about four loops under the drone, now spread over the parts that touch.
- **At the sheet's 200 N/m per part,** four legs would meet 800 N/m and the drone would sink 0.06 m.
- **A smaller coil is stiffer** as 1/R³. The **shape carries its own `stiffness_n_per_m`** = 60 · (0.45 / R_coil)³, with R_coil = `coil_diameter_m` / 2: **480 N/m** for the 0.45 m fence-top coil (25 mm sink on four legs) and 44 N/m for a 1.0 m coil (derived). This optional per-shape override is additive in schema 1 and was already anticipated by the `timber` row's "a batten asset may override". Its damping ratio comes from the material. The asset change adds it to `meta.validate` (positive, and only on a shape whose material is compliant).
- **The fiber ignores `razor_wire` shapes** (the coil volume, a sagged coil, a trampled mat). It reads only the turn records, as it ignores the V13 and V01 crown shells. Otherwise it would wrap a 0.45 m capsule that is 95 % air. The airframe feels the shapes; the props, legs and fiber also meet the records.

```json
"razor_wire": {
  "friction_static": 0.35,
  "friction_kinetic": 0.28,
  "stiffness_n_per_m": 60.0,
  "damping_ratio": 0.3,
  "edge_radius_m": 0.00025,
  "label": "derived",
  "rationale": "concertina razor tape (F01): the coil body, a sagged coil and a trampled mat; μ as steel (the barbs hold through the turn records, not μ); one loop under a part as a ring under a diametral load, k = f·EI/((π/4 − 2/π)R³) with the 2.5 mm core (EI 0.383 N·m² at an assumed 200 GPa) and f = 2 for the crimped tape (assumed, 2–3.8): 57 N/m for a 0.90 m coil, so a drone on four legs meets about 240 N/m and sinks 0.20 m; a coil of another diameter writes its own shape stiffness_n_per_m = 60·(0.45/R)³; ζ assumed for loop-on-loop friction and the clips; the edge is half the published 0.5 mm blade; the core (7.4 kN at an assumed 1500 MPa) never breaks under the drone; the fiber ignores razor_wire shapes and bends over the turn records' edge_radius_m (review-physics.md of p1-fortifications-sheet)"
}
```

### `sandbag`

- **The sheet's elastic punch is the wrong law here.** k = E·D/(1 − ν²) = 1.6e5 N/m is the small-strain stiffness of a half-space. But a 15 mm foot carrying 11.8 N presses at 67 kPa, while loose fill's surface bearing capacity under a 15 mm footing is only 1.6–3.5 kPa (0.5·γ·B·N_γ with a shape factor 0.6; φ 30–35°, γ 16 kN/m³, assumed). So the foot punches in, and the fill behaves like the map's loose-soil rows.
- **Tamped fill at a bearing of 5.0e6 N/m³** (assumed: between `tilled` 4.0e6 and `meadow_sod` 7.0e6) under the 15 mm reference foot gives k = bearing·A = **884 N/m, rounded to 900** (derived).
- **The sinks** (derived): 13 mm static per leg at 4.8 kg, 18 mm at 6.5 kg, 28 mm under the 25 N reference foot, and **39–79 mm in a 3–6 g landing**. So the "20–80 mm" band holds from the reference load to a hard landing. At 1.6e5 N/m the sink would be 0.07–0.44 mm, which is concrete.
- **The sheet's argument against `fabric` was per drone.** At 600 N/m, a leg sinks 20 mm, not 80 mm, and the stiffness lies in the same band. A separate id still earns its place:
  - **The edge:** `fabric`'s 5 mm hem caps the bend at 0.90 GPa, above the 0.69 GPa floor, so a line hauled over a sandbag wall would carry a cut risk on the weakest draws. A bag's 50 mm shoulder caps it at 0.09 GPa: none (derived).
  - **The damping:** the fill dissipates. ζ 0.94 comes from the soil rows' 3.2–3.7e5 N·s/m³ at the reference foot with a quarter of 4.8 kg (derived), so the row takes 0.9.
- **Not `surface:<id>`.** A sandbag wall is a built object (materials rule 2), and woven polypropylene carries none of D-021's wet adhesion.
- **One deviation is recorded:** an elastic row returns the foot, so no footprint stays.

```json
"sandbag": {
  "friction_static": 0.40,
  "friction_kinetic": 0.30,
  "stiffness_n_per_m": 900.0,
  "damping_ratio": 0.9,
  "edge_radius_m": 0.05,
  "label": "derived",
  "rationale": "filled sandbags (F04): μ as fabric (a polymer foot on woven polypropylene or hessian, assumed); a 15 mm foot at 67 kPa punches into loose fill (surface bearing capacity 2–4 kPa), so the fill acts as a loose-soil row: tamped fill at 5e6 N/m³ (assumed, between tilled and meadow_sod) under the 15 mm reference foot, k = bearing·A = 884 N/m, so a leg sinks 13 mm static and 28 mm at 25 N; ζ from those rows' damping at a quarter of 4.8 kg, 0.94; the edge is the rounded shoulder of a 0.12 m bag (derived), so a line over a wall top never cuts; elastic, so no footprint stays (a deviation from the soil law, accepted); no wet adhesion on polypropylene (review-physics.md of p1-fortifications-sheet)"
}
```

Both rows are added to `assets-src/physics_materials.json` in the asset change, with the `fields` sentence of §1 and the shape override above. They are not added here (this change's non-goal).

## 5. Gaps F02–F04 against the drone

Each gap was recomputed from the sheet's geometry, using the yaw window θmax = asin((W − 0.254)/0.4525) − 45° (houses sheet).

| Gap | Sheet | Check (derived) | Verdict |
|---|---|---|---|
| F02 in a row, between neighbours | 0.80 m at the ground to 1.70 m at the tops; inscribed circle 1.0 m | 2.0 − w(z), w = 1.2 − 0.9z: 0.80 / 1.07 / 1.25 / 1.70; the circle is limited by the 1.0 m height (its centre stands 0.57 m from each sloped side, more than its 0.5 m radius) | **confirmed: passes at any yaw** |
| F02 along the rows | the same widths | the corridor between staggered rows is 2.0 − w(z) | **confirmed** |
| F02 straight across the rows | closed below 0.86 m; any yaw from 1.01 m | 1.0 − w(z) with alternate rows every 1.0 m: −0.20 / 0.07 / 0.25 / 0.70 | **confirmed: not passable below the tops** |
| F02 diagonal lane at 63.4° | 0.18 / 0.54 / 0.78 / 1.39 m; nose-first from 0.33 m, any yaw from 0.44 m | lane spacing 4/√5 = 1.789 m less w·(cos 63.4° + sin 63.4°) = 1.342w; thresholds z ≥ 0.327 and 0.437 m | **confirmed** |
| F03 lower windows | 0.44–0.45 m clear | inradius of the right-isosceles window with only the two arm sides offset by the half-section d: (L/2 − 2d)(2 − √2) = 0.527 − 1.17d, d 0.066–0.076 | **confirmed: not passable** (a gusset in a window only shrinks it) |
| F03 under the node | 0.44 m, closed round | 0.52 − 0.076 m, enclosed by the three windows | **confirmed: not passable** |
| F03 pair at 2.0 m | worst 0.40 m at the ground and the tips, 1.87 m at the node; any yaw for a prop plane at 0.11–0.93 m; 0.18–0.86 m at 1.8 m centres | every row of the table reproduced; the inscribed circle is 1.04 m at the node (1.06 m at 0.53 m) | **confirmed** |
| F04 under the barrier arm | 1.0 × 3.8 m | 0.45 m above the 0.55 m envelope | **confirmed: any yaw** |
| F04 chicane lane, block joints, firing ports | 3.2 m; 0.1 m; 0.20 × 0.12 m | — | **confirmed** |
| F04 sandbag entrance | 0.70 m, nose-first within ±35° | ±35.3° at a clear 0.70 m. But bag faces bulge 20–40 mm (the sheet), so a nominal 0.70 m between wall ends is 0.62–0.66 m clear: ±9–19° | **corrected (§8, item 8):** `entrance_m` is the clear width between the bulges, the `narrowest_m` the generator writes. At the entrance the wall-end boxes follow each course, because AST-05's ±2 cm at openings is tighter than the 20–40 mm bulge |

## 6. Collision for the teeth

**Ruling:** stacked boxes do not hold AST-05 at the openings, and they give the wrong contact. **The asset change needs a `convex` primitive.**

**Stacked axis-aligned boxes against the 1.0 m frustum** (derived; distances to the frustum by dense sampling, §10):

| Boxes | At a face (out / in) | At an arris corner | Ledge left on each face |
|---|---|---|---|
| 5 | 41.0 / 31.0 mm | 53.7 mm | 90 mm |
| **11 (the sheet)** | **18.7 / 14.1 mm** | **24.4 mm** | **41 mm** |
| 14 | 14.7 / 11.1 mm | 19.2 mm | 32 mm |

Three findings:
- **The diagonal lane misses AST-05 at 11 boxes.** The sheet's 0.205/n holds on the faces, so the in-row gap is within ±2 cm at 11 boxes. But a box's top corner stands out from the arris by 1.31 × that, because the face normals are 80° apart. The diagonal lane is bounded by arrises, so 11 boxes put its walls 24.4 mm out. 14 boxes would hold.
- **The contact is wrong at any count.** The real face slopes at 65.8°, far above the 31° on which a foot holds on concrete (atan 0.60). The boxes replace it with vertical walls and horizontal ledges 32–41 mm wide. A 15 mm foot can rest on a ledge the real tooth lacks, and a sliding arm or coil gets a vertical normal and a stair to bump down.
- **The triangular variant cannot be built from axis boxes** within ±2 cm at all: its plan is a triangle in every layer.

**What the asset change writes:**
- One `{"shape": "convex", "points": [...], "material": "concrete", "landable": false}` per tooth: 8 points for the frustum, 4–6 for the triangular pyramid. A toppled or shifted tooth is the same hull moved.
- The `collision_boxes` parameter goes.
- The pipeline adds the kind to `meta.SHAPES`, with these checks: 4–16 points (assumed cap), every point a vertex of their hull within 1 mm, a volume above zero.
- The audit measures the hull against the visual (exact for a tooth), and the Unity review overlay draws it. The metadata is data, so the asset can carry it before phase 3 implements the queries.

**What the phase-3 world-query core must add** (world-builder, with my sign-off):
1. **Load-time precompute** per hull: the face planes (at most 2n − 4) and the distinct edge directions.
2. **`StaticContacts`**, swept sphere and capsule against a hull: point, normal, signed distance and `Time`, by separating axes over the face normals and the capsule axis × edge directions, in a fixed axis order, with IEEE basic operations only. A box is the 3-face, 3-edge case of the same test, so this generalises the box path. The Time < 1 latch is unchanged.
3. **`Raycast`** by half-space clipping.
4. **`Geometry(object, shape)`** returns the points.
5. **The fiber at a hull edge** bends over the material's `edge_radius_m` (concrete's 3 mm), as at a box arris.
6. **Golden cases**, a worldbench case `teeth_field`, and a raised `QueryVersion`.

At the assumed timings, one hull (6 faces, 12 edge directions, 18 axes) costs less than 11–14 boxes, and a field of 50–200 teeth holds 50–200 shapes instead of 550–2,800.

## 7. The snag budget

**Ruling:**
- **Not per step.** Per-step `StaticContacts` over shapes cannot take the records, and it never sees them.
- **Through the cache.** Records are served like micro-detail: prefetched at ≤ 40 Hz into the binned 2 m cache around the drone, and 1 m caches at the fiber's touch points (the V13 and V01 readings, N-b). There they fit the step.
- **What phase 3 adds:** an index, a record query and a cap.

**Measured on the generator's geometry** (derived): planar turns of 24 points at the sheet's pitches and leans, plus 5 clips a turn. N is the most record segments whose bounds (inflated by the 8 mm reach) meet a 2 m cube. T is the worst step's narrow tests: the 14 parts' boxes (+30 mm, the knowledge §5.2 layout) against those bounds, at yaw 0° and 45°, on a 0.1 m grid through the stack.

| Layout | N (lean atan / asin) | T (lean atan / asin; most per part) | Binned (worst T·30 ns + 0.7 µs) | Unbinned (14·N·5 ns + T·30 ns) |
|---|---|---|---|---|
| Single 0.90 m | 225 / 228 | 62 / 58 (10) | 2.4–2.6 µs | 17.8 µs |
| Fence-top 0.45 m | 307 / 307 | 113 / 138 (17) | 4.1–4.8 µs | 25.6 µs |
| Double-stacked | 450 / 456 | 108 / 109 (17) | 4.0 µs | 35.2 µs |
| Triple (2 + 1) | 675 / 684 | 82 / 87 (12) | 3.2–3.3 µs | 50.5 µs |
| 3 rows × 2 tiers (the generator's maximum) | 1,082 / 1,096 | 108 / 109 (17) | 4.0 µs | 80.0 µs |

The two lean rules (the sheet's atan(2p/D), and asin(2p/D) for planar loops, §8 item 4) differ by under 2 % in N and under 25 % in T.

**What the table shows:**
- **T is bounded by geometry, not by the count.** Even the densest default stack costs 4.0 µs binned, inside an 8.3 µs share.
- **Unbinned, a single run already breaks the share,** so the consumer bins the cache (as N-b ruled for shells).
- **The fiber** tests its ≤ 32 segments against the touch-point caches, and the bench counts that too.
- **The coil volume's capsules** (26 per 100 m) are ordinary shapes behind the shape index and cost nothing extra.

**`GapsNear`** has nothing from F01: a coil has no gap. A teeth field writes about three gaps a tooth (in-row and two diagonal lanes), so 150–600 a field. Over a map of about 10⁴ gaps (assumed: houses and fields), a linear scan costs 50–100 µs a call. At ≤ 20 Hz on the flight thread, that would break the 60 µs budget one step in fifty. With an index, a 20 m query in a field returns about 100 gaps in under 2 µs.

**What the phase-3 world-query spec must add:**
1. **A load-time spatial index** (a uniform grid) over shapes, snag records (bounds inflated by `radius_m`) and gaps. It returns results in canonical order. It is the same index for `StaticContacts`, the record prefetch and `GapsNear`, or `GapsNear` runs on the worker.
2. **A record query,** for example `SnagsNear(center, halfSize, out)`. It returns segments with the object, record index, segment index, kind, `radius_m`, `edge_radius_m`, `barb_pitch_m` and material, into a caller-provided span, with the true count returned. It feeds the 40 Hz caches and never runs in the step.
3. **A neighbourhood cap: at most 2,048 record segments meeting any 2 m cube**, per asset at build (the shells' rule (d) extended to records: printed, failing beyond) and over all objects in the map compiler (derived: about twice the densest default stack's 1,096, to leave a bunched span or a crossing run, rounded to the power of two that sizes the fixed cache, under 0.1 MB). The generator keeps 24 points a turn on stacks and bunched spans.
4. **A worldbench case `wire_runs`:** the 14 parts at 1 kHz along a path into a single run, a 3 × 2 stack and a fence-top coil, plus a fiber laid across a run. It reports µs a step (median and p99, AC power, power mode printed) against the 8.3 µs share. If the bench misses the share, the map cap comes down first, as for shells.

## 8. Sent back to the analyst (task 1.4)

1. **F01, snag table and generator table.** `radius_m` 0.008 is described as the barbs' reach. The "Blade edge" row becomes the record's `edge_radius_m` 0.00025, and the "Barb pitch" row the record's `barb_pitch_m` 0.035. Add `material`: `razor_wire` on turns; `steel` on clips, tie-wire tails and pickets. In the generator table:
   - `tape_thickness_m`: "its half is every turn record's `edge_radius_m`"
   - `barb_pitch_m`: "written into every turn record as `barb_pitch_m`"
   
   Close the open point "Which radius the fiber bends over" with a pointer to §1.
2. **F01, cut or hold.** Replace "Whether a barb cuts the fiber or only holds it is not ruled here" with the ruling. Say in one sentence that it holds below T_c and is a probable cut above, 0.034–1.0 N at a 90° turn, with a pointer to §2. Close the open point.
3. **F01, collision and landing.**
   - The coil volume is 60 N/m per touching part (about 240 N/m on four legs), not 200.
   - For coils other than 0.90 m, the generator writes a shape `stiffness_n_per_m` = 60 · (0.45 / R_coil)³ (480 N/m for the fence-top coil).
   - The fiber ignores `razor_wire` shapes and reads only the records.
   - Landable: "sinks about 0.20 m on four legs" (25 mm on the fence-top coil).
   - Remove "If the physics reading maps the tape to `steel` instead …".
4. **F01, `loop_lean_deg`** (geometry only; no number in this reading depends on it). For a planar loop, the two-pitch extent along the run is D·sin(lean), so the default is asin(2p/D) (36.6° and 60.6°), not atan(2p/D) (31° and 42°). Alternatively, say that the stretched loops are not planar. The lattice openings between loops of the same lean stay at 2p·cos(lean) = 0.43–0.46 m: closed.
5. **F02, collision.** Replace the 11 stacked boxes with one `convex` hull per tooth (8 points; 4–6 for the triangular variant). Remove `collision_boxes`. Close the open point with a pointer to §6: the asset change adds the kind, phase 3 its queries.
6. **F03, landable.** The arms stand at **54.7° from vertical** (35.3° from horizontal), not "35° from vertical". A foot slides on them (35.3° is above atan 0.35 = 19.3°), so "none" stands.
7. **F04, sandbags.** The material is `sandbag` with §4's values (900 N/m, ζ 0.9, edge 0.05 m). Strike "A filled bag is rigid against the drone", the "80 mm" argument (per leg it is 13 mm static and 28 mm at 25 N), and "Until the physics reading rules …".
8. **F04, the entrance.** State `entrance_m` 0.70 m as the clear width between the bulging bag faces (the gap's `narrowest_m`), so the nominal opening is 0.70 m plus the bulges. At the entrance, the wall-end boxes follow each course to hold ±2 cm. If 0.70 m were nominal, the clear 0.62–0.66 m would pass nose-first only within ±9–19°.
9. **"Proposed physics ids" table.** Mark both as accepted, with §4's rows: `razor_wire` 60 N/m per part (band 40–110), label derived; `sandbag` replaced.
10. **"Open points across the sheet".** Close the four physics items (cut or hold, the radius, the two ids, the snag budget) and the F02 primitive with pointers to §1, §2, §4, §6 and §7.

## 9. Notes for later owners (non-blocking)

- **N-1, physics-engineer (the materials table): two rows are per drone.** `fabric` (600 N/m, "50–100 mm sink of a mattress under the drone's 49 N") and `mesh` (800 N/m, "a drone-sized point load", r 0.3 m) were derived for the whole drone. The contact law applies them per part, so four legs on a mattress sink 20 mm. A per-contact pass over the stiffness rows belongs in the next materials sign-off. This reading does not touch them.
- **N-2, world-builder and physics (phase 3 wind compiler).** Knowledge §10.3 scales an object's porosity to the 2 m cell with D_obj = footprint perimeter / π. For an elongated volume (a wire span 0.9 m thick and 3.8 m long, a hedge, a porous fence), D_obj must be its thickness across the wind. With perimeter/π the grid reads a coil cell as 0.985 instead of 0.951. That is harmless at β 0.95, but not for a hedge.
- **N-3, physics-engineer (phase 7 tether).**
  - Use §2.1's elastica R_eff in place of √(EI/T).
  - Make `payout_tension_n` a named, logged parameter.
  - Count abrasion per fiber section at a contact.
  - Read `barb_pitch_m` as a lock.
  - Let the fiber skip compliant homogenised shapes (`twig`, `foliage`, `razor_wire`).
- **N-4, qa-engineer.** The two cases of §2.3 are scenarios QA can run in phase 7:
  - (a) a line draped over a run on 20 seeds: no cut on draws above 1.35 GPa
  - (b) the drone reversing round a snagged barb: cuts on every draw below σ_b, each logged with mechanism `bend` and the record index

## 10. Evidence

Scripts in my scratch folder: `fiber.py`, `coil.py`, `teeth.py` and `checks.py`, run with `python -I`. They are not committed.

```
$ python -I fiber.py
I 1.1984e-17 m4  EI 8.7485e-07 N m2  E*r 4.5625e+06 N/m  A_glass 1.2272e-08 m2
linear mass 0.0675 g/m  weight 6.621e-04 N/m
sigma 0.69 GPa: tension break 8.47 N
cap over blade (R 0.3125 mm): 14.60 GPa; conforms above T = 8.96 N (theta~2 rad)
cap over band 1.75 mm: 2.517 GPa | steel 1 mm: 4.294 | twig 2.5 mm: 1.780 | concrete 3 mm: 1.490 | fabric 5 mm: 0.901 | sandbag 50 mm: 0.091
T_c (N) [R at T_c, mm]       15            30            45            90           180
 0.69 GPa           1.169 (6.61)    0.294 (6.61)    0.131 (6.61)    0.034 (6.61)    0.010 (6.61)
 1.38 GPa           4.678 (3.31)    1.174 (3.31)    0.526 (3.31)    0.137 (3.31)    0.040 (3.31)
 3.80 GPa          35.468 (1.20)    8.905 (1.20)    3.986 (1.20)    1.036 (1.20)    0.303 (1.20)
 5.00 GPa          61.406 (0.91)   15.417 (0.91)    6.901 (0.91)    1.794 (0.91)    0.525 (0.91)
capstan 30/90/180 deg: 1.201 / 1.733 / 3.003 (mu_s 0.35)
payout momentum flux at 10/20/30 m/s: 0.0067 / 0.0270 / 0.0607 N; crosswind drag at 8 m/s 0.0118 N/m (Re 136)
span hanging 50 m 0.0331 N; back friction 100 m 0.0331 N, 300 m 0.0993 N
Hertz T 0.5 N theta 90: mean p 123 MPa; T 0.1 N: 72 MPa; Archard k 0.01/0.05: 0.7/3.5 um a pass, 88/18 passes
barb plastic moment 1.56e-01 N m, release at 3 mm lever 52 N

$ python -I coil.py atan        # and asin
single 0.90        2 m cube max 225 segments; worst-step narrow tests T 62 (most per part 10)   | asin 228; 58 (10)
fence-top 0.45     2 m cube max 307 segments; T 113 (14)                                       | asin 307; 138 (17)
double-stacked     2 m cube max 450 segments; T 108 (16)                                       | asin 456; 109 (17)
triple 2+1         2 m cube max 675 segments; T 82 (12)                                        | asin 684; 87 (11)
3 rows x 2 tiers   2 m cube max 1082 segments; T 108 (16)                                      | asin 1096; 109 (17)

$ python -I teeth.py
face angle 65.77 deg
n  5: outward at a face  41.0 mm, at an arris corner  53.7 mm; inward <= 31.0 mm; ledge 90.0 mm
n 11: outward at a face  18.7 mm, at an arris corner  24.4 mm; inward <= 14.1 mm; ledge 40.9 mm
n 14: outward at a face  14.7 mm, at an arris corner  19.2 mm; inward <= 11.1 mm; ledge 32.1 mm
foot rests on a tooth face only below atan(mu_s concrete 0.60) = 31.0 deg; face 65.8

$ python -I checks.py
0.90 stretched: a 0.133, silhouette 0.954, through the axis 0.942
0.45 fence top: a 0.363, silhouette 0.938, through the axis 0.922
0.90 bunched: a 0.356, silhouette 0.882, through the axis 0.852
trampled mat: a 0.399; across 1.41 m 0.755; through 0.15 m 0.971
projected per tape length, cross-run wind 3.52 mm (sheet G w = 4.0 mm); orientation-corrected 0.960; mat lying flat 0.809
pickets: 0.0132 m2 per m of run against the coil's 0.0414; with them 0.939
EI core 0.383 N m2; band annulus 1.090 N m2 -> f up to 3.84
D 0.45: core loop 226.3 N/m; D 0.90: 28.3; D 1.00: 20.6
drone on a 0.90 coil on 4 legs at 60 N/m each: sink 0.196 m; at the sheet's 200 per contact: 0.059 m
0.45 coil per contact 480 N/m; 4 legs: sink 25 mm
bearing 4.0e6 / 5.0e6 / 7.0e6 N/m3: k 707 / 884 / 1237 N/m
  static per leg 11.8 N -> 13.1 mm; 6.5 kg 15.9 N -> 17.7 mm; 25 N -> 27.8 mm; 3 g 35.3 N -> 39.2 mm; 6 g 70.6 N -> 78.5 mm (at 1.6e5: 0.07-0.44 mm)
damping from the soil rows: c 61.9 N s/m, zeta 0.94 at m_eff 1.2 kg
the sheet's elastic punch 1.648e+05 N/m; foot pressure at 11.8 N 67 kPa; surface bearing capacity 1.6 (phi 30) to 3.5 kPa (phi 35)
arm from vertical 54.7 deg, from horizontal 35.3 deg
yaw window: 0.60 +-4.9, 0.62 +-9.0, 0.66 +-18.8, 0.70 +-35.3 deg
```

**Generic sources for the published fiber numbers** (no place, unit or date):
- the proof-test level 0.69 GPa (100 kpsi): https://stl.tech/wp-content/uploads/2023/04/Optical_Fiber_Proof_Testing.pdf and https://ro.prysmian.com/sites/ro.prysmian.com/files/media/documents/Product%20lifetime.pdf
- bending strength of pristine fiber, 6.5–7.6 GPa Weibull means: https://nanophotonics.spiedigitallibrary.org/conference-proceedings-of-spie/4215.toc
- Weibull moduli, 30–100 for telecom fiber and 3–7 for abraded glass: https://arxiv.org/abs/cond-mat/9606158
