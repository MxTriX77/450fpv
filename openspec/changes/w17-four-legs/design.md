# Design: w17-four-legs

Task 1.1 (physics-engineer): the baseline from the pilot's flights, and the law of four separate legs whose tips dig into the soil. No flight changes here; the law is built in 1.3. Every number is labelled **measured** (the pilot's logs, model 103), **published**, **derived** or **assumed**. The hand numbers are `python tools/flightbench/handcalc/four_legs.py`, which prints every number quoted below that is not measured.

## Context

- **What flies now (model 103).** Each corner is a half-skid: a Ø10 mm PVC-U tube lying on the soil from the skid's middle to its outer end, three nodes under Winkler's law at Bekker's modulus for the tube's width, Wong's unloading, a stick anchor holding to μs·Fn + c_a·S + P(θ) (P the groove's passive wall), and in Rain and Severe rain a cohesive pull over the wetted footprint (`p7a-ground` designs 2, 3, 7). The legs are `p7g-legs`' PVC-U cantilevers on two skids' frame (model 54), with the hanging half and its depth law (`w9-village-physics` 2.6, 2.6b) and the anchors' reach (2.4).
- **The pilot's words** are in the proposal. Their account outranks a calculation (D-104).

## 1. The baseline from the pilot's flights (measured)

**The flights.** 30 logs written since the model 103 build, all model 103 on the farmland map, read in place by a scratch reader of their step records and events (every number below is a logged step's own; no replay needed). Cited F01–F30 in time order. F01–F15: Sunny, Calm, 1 kg payload (5.08 kg). F16–F30: Severe rain, Severe wind, 2 kg payload (6.08 kg). Every liftoff from the start point is from `meadow_sod`.

### 1.1 Liftoffs (28; 23 from rest on meadow are summarised)

| Measure | Sunny, 12 liftoffs | Severe rain, 11 liftoffs |
|---|---|---|
| T/W at the first motion (median) | 0.92 | 0.96 |
| T/W as the last contact lets go | 1.03–1.23 (median 1.10) | **1.31–1.71 (median 1.47)** |
| Upward speed as it lets go | 0.05–0.18 m/s (median 0.08) | **0.34–0.52 m/s (median 0.46)** |
| Height 0.3 s after | 0.04–0.17 m | **0.26–0.45 m** |
| Slide before leaving | 0.4–9.6 mm | **11–196 mm**, at up to 0.07–0.17 m/s |
| First to last corner leaving | 17–379 ms | 35–113 ms |
| Pull on a corner (leg load below 0) | none | **on every corner, −3.0 to −10.7 N (median −9.1)** |
| T/W climb while it rises (median) | 0.72 /s | 0.41 /s |
| Tilt as it lets go | 0.25–2.8° | 2.1–8.2° |

The other five: three from twig contacts in crowns after a lodged touch (slides 1–4 m, not ground), one from `dry_crust` in Severe rain after a 7 s rest (T/W 1.25, up 0.29 m/s, slid 0.16 m, pulls −7.8 to −10.2 N), one tumbling off a slope.

### 1.2 What "a magnet" is in these flights

- **What holds it:** the four half-skids' wet grooves, by their cohesive pull (`Soil.Cohesion` over each node's wetted width). Hand: σ_a 5 kPa (saturated, assumed in `surfaces.json`) over a half-skid's 2.2e-3 m² footprint is **11.0 N a corner**, four 44 N = **0.74 W** (derived); measured −9.1 to −10.7 N a corner, and the contact force pulled F23 down by up to **26.9 N (0.45 W)** while its throttle rose from 0.38 to 0.66 over 2.4 s with the drone not rising a millimetre.
- **What it stores:** almost nothing. The leg's stretch is 10 N / 52.3 kN/m = 0.19 mm; the cohesive zone's elastic part is σ_a/k_u ≈ 1.2 mm at ½·11 N, about 7 mJ a corner, 27 mJ for four, worth 0.09 m/s (derived). **The jump is the thrust's excess, not stored energy:** the bonds fail within 35–113 ms of each other at T/W 1.31–1.71, and the drone leaves at 0.3–0.7 g of excess thrust.
- **Why it runs along the ground:** a skid held up by its pull presses nothing, and the anchor acts only while the skid presses (`SoilFriction` returns nothing at fn ≤ 0; `p7a-ground` design 7). So a drone pulled down 13–27 N has no friction at all: in the Severe wind F23 slid 0.20 m at 0.10–0.14 m/s while the horizontal contact force stayed mostly 0.1–0.5 N. That is "at low throttle the drone kind of slides along the ground, and with a little more throttle it jumps up".
- **Sunny:** no magnet. Nothing holds a dry skid (σ_a = 0); the drone leaves at T/W 1.03–1.23 at 0.05–0.18 m/s, the speed its thrust's excess gives over the 0.1–0.6 s it takes.

### 1.3 Touchdowns (63)

29 are the legs brushing crowns' twigs (no load), 14 are crashes, 10 touch-and-go skims at T/W ≥ 0.95, 10 landings. The landings (|v_y| ≤ 1.9 m/s):

| Flight | Ground | Touch down / along m/s | Peak | Rebound | Contacts lost | Slide | Rocking | End |
|---|---|---|---|---|---|---|---|---|
| F05 | dirt_track, Sunny | 0.85 / 1.96 | 5.7 g, 163 N a leg | 0.18 m/s up (e 0.21), then held off 1.3 s at hover | 0 | 0.10 m | | flew on |
| F05 | dirt_track, Sunny | 1.00 / 1.49 | 6.4 g, 197 N | 2.5 mm, then to 57 mm | 8 | **1.84 m** | 81 reversals | lifted after 3.8 s |
| F06 | meadow_sod, Sunny, cut | 1.52 / 0.45 | 13.3 g, 202 N | 7 mm | 7 | 0.47 m | 17 | **on its side 54.6°**, legs cracked and set |
| F16 | dirt_track, Severe rain | 1.79 / 1.83 | 7.8 g, 336 N | to 39 mm | 22 | **4.38 m** | 27 | 45°, legs set and broken |
| F18 | meadow_sod, Severe rain, cut | 1.90 / 1.18 | 19.9 g | to 58 mm | 3 | 2.33 m | 13 | **137°** |
| F24 | dry_crust, Severe rain | 1.57 / 0.75 | 10.6 g, 242 N | **0.49 m/s up, 21 mm** | 0 | 0.16 m | 17, swaying 1.4 s | rested 7 s |
| F29 | masonry, Severe rain | 0.24 / 0.64 | 12 g, 311 N | to 21 mm | 11 | 0.38 m | 19 | **on its side 53.4°** |

The skims add F15 (meadow, 0.18 / 1.89 m/s: **slid 3.5 m**), F29 (timber: 16 contacts lost, bounces to 134 mm, slid 3.85 m), F05 (dirt track: 9 lost, 2.03 m).

### 1.4 What "a small light plastic toy plane" is in these flights

1. **It skates.** Slides of 0.4–4.4 m after touching at 0.5–2 m/s along, at an effective friction of 0.04–0.35 (measured: the horizontal deceleration over the first 0.6 s over g·(1 − T/W), against the table's μk 0.27–0.6): a sled on two rails, its normal force flickering.
2. **It chatters.** Up to 22 losses of contact in a landing, bounces after the first to 7–58 mm (134 mm in a skim on timber). Hand for a 6 kg drone on four tips: on soil one rebound of 0–25 mm and at most 4 contacts lost (§3 below).
3. **It rocks.** Diagonal pairs of legs loaded in turn for 1–4 s (13–81 reversals of the pitch or roll rate), the legs leaning 5–29 mm: the body sways at about 2 Hz on its legs' 268 N/m cantilevers (derived: √(4·268/6.08)/2π = 2.1 Hz).
4. **It goes over.** Four of seven landings ended off their legs, two of them cut landings at 1.5–1.9 m/s on meadow, after legs cracked and set (`p7g-legs`).

What a 6 kg drone on four plastic legs does instead (hand, §3): its tips dig 10–23 mm into dry meadow sod at 0.5–1.5 m/s with a single rebound of 1–9 mm, the soil taking 88–92 % of the energy; once in, the tips hold 15–117 N sideways together, so it stops where it touched or turns about the tips that hold, and with tips that catch it goes over the front ones past **0.74 m/s** along (the skids' span held to 1.85 m/s).

## 2. Decisions

### 1. A leg's tip is a small penetrometer

- **The tip** (until 1.2 gives its shape): the leg tube's own cut end, taken as a **plugged flat disc** of the tube's outer diameter, 10 mm, A_t = 7.854e-5 m² (assumed: a small open tube plugs within a few bores by its plug's arching, Randolph, Leong and Houlsby 1991; a cap or foot from 1.2 is a disc anyway).
- **The resistance** it meets is the soil's penetration resistance, which grows from nothing at the loose surface to the published topsoil value: q(z) = q_t·z/z_ref, z_ref = 0.025 m (derived: the middle of the 0–5 cm layer field readings average over; the linear form is Bekker's n = 1 line, assumed), so the tip's loading line is **k_t·A_t·z, k_t = q_t/z_ref**.
- **q_t per surface** (new labelled `surfaces.json` field `tip_resistance_pa`, the dry set; assumed, inside the published order of field penetrometer readings: roots slow past 1.5 MPa and stop near 2.5 MPa at field capacity, grazed pasture 0.8–3 MPa at 4–8 cm, trafficked passes 2.3–5.4 MPa): meadow_sod 1.5, belt_bare 1.5, weeds 1.5 MPa; tilled 0.4; crater_spoil 0.4; belt_straw 0.8; burnt_field 2.0; yard_litter 2.5; dry_crust 3.0; dirt_track 4.0; rubble 5.0 MPa. Asphalt and concrete_slab stay rigid: no tip enters.
- **Wet and saturated** take the table's own `wetting.bearing_wet` ×0.3 and `bearing_saturated` ×0.15 (derived; the published direction, penetration resistance falling severalfold as a soil wets).
- **Why not the table's Bekker moduli** (the known-wrong): at the tip's width they are the reference foot's soft feel (K§7.2: a 15 mm foot at 25 N sinks 20 mm). Under a tip's static 14.8 N they sink it 22 mm into dry meadow, 72 mm wet and 144 mm saturated, and a 1.5 m/s touchdown 9–33 cm (derived). That is neither "only slightly" at the start nor "a rough landing digs in": every rest digs in deep.
- **Unloading:** Wong's rule on the tip's own lines. The tip keeps an elastic return of Sneddon's rigid punch, k_e = 2aE/(1 − ν²) on E = α_E·q(z_m), α_E = 2.5 (published, Schmertmann, Hartman and Brown 1978), ν = 0.3 (assumed): a fixed **2.86 mm** whatever the depth (derived), so the unloading slope is max(k_t·A_t, k_t·A_t·z_m/2.86 mm). The firm layer past `max_sink_m` and the soil's damping c_s·A_t are the table's.
- **In series with the leg** at equal force, as now (the series solve, one node instead of three). The leg's axial spring without the skid's ring is the tube's EA/L (363 kN/m at its 0.2335 m free length) in series with the arm (67.6 kN/m): **57.0 kN/m** (derived; 52.3 with the ring).

| Ground (2 kg loadout, 14.82 N a leg) | k_t N/m³ | Static sink | 0.5 m/s cut touchdown | 1.5 m/s |
|---|---|---|---|---|
| meadow_sod dry / wet / saturated | 6.0e7 / 1.8e7 / 9.0e6 | 3.2 / 10.5 / 21.0 mm | 9.6 / 19.1 / 28.8 mm | 22.8 / 38.6 / 50.9 mm |
| tilled | 1.6e7 / 4.8e6 / 2.4e6 | 11.8 / 39.3 / 78.6 mm | 21.2 / 45.2 / 73.4 mm | 42.1 / 68.9 / 91.7 mm |
| dry_crust | 1.2e8 / 3.6e7 / 1.8e7 | 1.6 / 5.2 / 10.5 mm | 6.7 / 14.0 / 21.1 mm | 15.5 / 27.6 / 31.8 mm |
| asphalt | rigid | 0 | 0 | 0 |

### 2. The hold on the way out stores nothing

All of it is friction and adhesion on what is still in the soil, each falling to nothing as the tip comes out:
- **The shaft's grip, dry and wet:** τ = r_s·q(z) over the embedded shaft, so **H_g = r_s·k_t·π·D·z_e²/2**, r_s = 0.01 (assumed, a new named parameter: CPT friction ratios of silt mixtures are 2–5 %, Robertson 1990, halved for a shaft pulled near the free surface: a shaft in tension carries 0.5–0.8 of its compression friction, De Nicola and Randolph 1993, and the push's radial stress is unconfined above it). It resists the shaft's motion both ways, so it also adds to the tip's resistance on the way in. Its known-wrong lower bound, friction under the at-rest stress μ·K0·ρg·πD·z²/2, is 0.7 mN on meadow at rest (derived).
- **Wet:** the tip's suction-adhesion by the table's cohesive law over A_t (σ_a 3 / 5 kPa, δ_a 2 / 3 mm: 0.24 / 0.39 N at its peak) and the shaft's interface adhesion c_a·π·D·z_e (c_a 2 / 1 kPa).
- **Nothing is stored:** the leg's stretch at a 2.67 N hold is 0.05 mm, 62 µJ, all four worth 0.009 m/s (derived). The hold falls with the remaining embedment, so a held leg lets go over its depth, never by a snap.

**A careful liftoff** (the start's depth at the set-down's 1.5×; the last held corner carries I_d/(I_d + m·r²) = **0.345** of the thrust's excess, derived; the pilot's measured T/W climb 0.72 /s Sunny, 0.41 /s rain):

| Ground | A leg's hold | The last corner lets go at | Held after T = W |
|---|---|---|---|
| meadow dry / wet / saturated | 0.13 / 1.78 / 2.67 N | T/W 1.007 / 1.087 / 1.130 | 9 / 212 / 318 ms |
| tilled dry / wet / saturated | 0.70 / 6.42 / 9.23 N | 1.034 / 1.314 / 1.451 | 48 / 765 / 1100 ms |
| dry_crust dry / wet / saturated | 0.05 / 0.94 / 1.47 N | 1.002 / 1.046 / 1.072 | 3 / 112 / 176 ms |
| asphalt | 0 | 1.000 | 0 |

Model 103 on wet meadow: 9–11 N a corner, T/W 1.31–1.71 (measured). **After a touchdown** the hold is the depth's: on dry meadow 0.56 N a leg after 0.5 m/s (T/W 1.027 at the last), **3.88 N after 1.5 m/s (T/W 1.189)**; wet meadow 2.24 / 6.31 N; dry tilled 1.00 / 4.04 N; dry crust 0.39 / 2.61 N. So the sticking comes from the points a rough landing drove in, more on soft ground, most in rain: the pilot's order, from depth and wetness alone. r_s's band 0–0.04 moves the dry meadow hold after 1.5 m/s from 0 to 15.5 N; 1.3 prints this sensitivity beside PHY-03's rates.

### 3. A dug-in tip resists sliding; hard ground does not

- **The soil in front of the shaft:** Matlock 1970's lateral resistance (published), p_u = (3 + J·z/D)·c_u·D up to 9·c_u·D, J = 0.5, with c_u = q(z)/N_kt, N_kt = 15 (published, 10–20, Lunne, Robertson and Powell 1997): **P(z_e) = (k_t·D/N_kt)(1.5·z_e² + J·z_e³/(3D))** (derived). It is the same for every direction (a round shaft), it acts whether or not the tip presses, and it falls to nothing as the tip comes out.
- **The anchor** keeps design 3's form, its cone μs·Fn + c_a·A_t + P(z_e): the groove's P(θ) is replaced by the shaft's P, and it holds while the tip is in the soil (z_e > 0), not only while it presses. Its spring is `FrictionStiffnessRatio` (0.5, carried) times the tip's unloading stiffness.
- **Numbers (2 kg):** at the start (1.5× set-down) 0.89 N a leg on dry meadow, 4.6 wet, 11.2 saturated; after a 1.5 m/s touchdown four tips hold 117 N (1.98 W) on dry meadow, 141 N on dry tilled, 76 N on dry crust. The impact's own friction stops 0.28–0.45 m/s along at 0.5 m/s down and 0.80–1.31 m/s at 1.5 m/s down (derived: μk times the vertical speed's change).
- **So the drone does not sled:** with the tips in, a push below the weight holds it, or it turns about the tips that hold (the spec's "no sliding like a sled").
- **Hard ground:** asphalt, a slab and the dirt track's 1.2 mm take no tip worth the name: Coulomb on the tip's face alone (μs 0.5 dry asphalt), no hold at liftoff.
- **Going over:** a drone whose tips catch goes over the front tips past **0.74 m/s** along and 0.73 m/s across (2 kg; 0.77 for 1 kg), its centre of mass 0.249 m over a 0.205 m square, rising 20 mm to the balance point (derived, rigid legs, throttle cut, no controller); friction alone can trip it, μ > a/h = 0.41. The skids' 0.45 m held 1.85 m/s along. The legs soften it: 0.74 m/s on four held tips bends them 63 mm, 12.9 N a tip, just under the yield's 13.7 N (derived), so a fast touch bends legs before it flips. This is the pilot's own kit's stance, and D-067/D-079's "falls more often" in life.

### 4. The start: each leg's set-down, drawn

- **The named parameter:** `leg.setdown`, each tip's set-down load over its static share, **uniform in [1, 1.5]** (assumed: a careful hand lets go as the tips touch, pressing a leg at most half its share again). Four draws at the flight's start, in the header with their bounds (PHY-24); a stream of its own, so no other draw moves.
- **The depth follows from the soil:** each tip is pushed to λ_i·F_i on its loading line (z_m) and comes back to its share on its unloading line (the start's set). Static shares differ with the build's balance (`build.placement`). Meadow dry 3.2–3.8 mm, wet 10.5–14.8, saturated 21–31; dry crust 1.6; the dirt track 1.2; tilled dry 11.8–16.7; asphalt 0 (derived). "Only slightly dug in" on the start ground, deeper where the soil is soft or wet, and never on hard ground.
- **The start's pose** is the plane the four drawn tips stand at (1.4 places the drone so).
- **Same seed, same start, same liftoff**; two seeds differ per leg.

### 5. The legs' failure law without the skids' frame

- **Each leg is a cantilever from its clamp both ways.** The frame (model 54: `LegFrameSideStiffness`, `LegFrameSwayLoad`, `LegFrameLeverLength`, `Frame`, the socket's share) leaves; `Frame(leg)` is false for every leg. The tube's laws stay (`p7g-legs` design 3: the edge's first yield, the set, the kink and tearing strain, buckling, the lever, the jaws' friction), on the new free length 0.2335 m (derived; 1.2 may move it): side give **205 N/m** (268 at 0.2135 m), first local yield at the clamp's edge **9.1 N** across at the tip, yield 13.7 N, the plastic hinge 19.2 N; the anchors' reach π²L/12 = **0.192 m**.
- **Buckling:** a tip held in soil pins the column (clamped–pinned, **322 N**); a tip free to slide (hard ground) leaves it clamped–free, **39.3 N** (derived). So legs splay on asphalt and stand on soil, as the tip's hold decides.
- **A stuck tip in a sliding touchdown:** the tip held by P(z_e) while the body runs on bends the leg on its 205 N/m until the first of: the soil gives (the tip ploughs at P(z_e)), or the clamp's edge yields (9.1 N across: a crack, 13.7 N a set). On dry meadow a tip 7.7 mm deep (after 0.5 m/s) gives first (3.9 N); after 1.5 m/s (20 mm deep, 30 N) the leg yields first: a rough sliding landing bends legs.
- **The T-joints' law** (pull-out, rotation, fracture of the skid in its socket) leaves with the T-joints if 1.2 removes them; a tip part from 1.2 that is held by friction on the tube would take the clamp's slip law (open, §7).

### 6. What leaves with the skids, and what stays for the kept audits

- **Leaves the flown model:** the half-skid soil corner (`Skid`, three nodes, `SoilCorner`'s tube law and its groove P(θ)), the groove ring of half-skids (replaced by a print per tip: its point and z_m, so a re-landing in the same hole finds compacted soil, within the tip's radius, design 3's rule), the cling of a half-skid on a wet layer (`Cling`, now over the tip's area), the frame, the hanging half (`Hang`, `HangDepth`, `HolderGive`, `SkidFold`, `CornerState.Hanging`/`SkidGone`, `SkidLoose`): a broken leg has no neighbour to hang from (the proposal's non-goal). The step record's `SkidFold` and the skid corner states read 0.
- **Stays for the kept audits:** all of it, behind one switch in a header entry of its own (the next free tag, assigned at 1.3, D-072/D-074), flown only with the skids' drone file as a fixture (`assets-src/drone/` keeps it beside the new one, or the suite's fixture folder). A log without the entry reads as its model flew (model 103 and earlier: the skids). Every kept audit and model 103's pins fly with the switch.
- **The goldens move once** (the drone's 36.6 g and the law), each re-recorded and audited case by case against its model 103 pin, with the switch.

### 7. What task 1.2 (asset-artist) must give

1. **The tip's shape** as photos T and U show it: the cut tube's end, a cap, a ball or a foot; its outer diameter; whether it is closed (a cap) or open (the default is plugged, assumed); any part below the tube (its length, material and mass).
2. **The leg's free length:** the clamp's bottom to the tip's lowest point (0.2335 m by default, derived from the parts as they stand).
3. **The contact shapes:** per leg the tube's capsule ending at the tip, and one support at the tip (a sphere of the tip's radius, or a disc's rim) as the corner's lowest point; no skid capsule, no T-joint box unless the photos keep one. Every drawn leg has its shape (REAL-05).
4. **Masses:** the tip's, and what leaves (two skids 36.6 g; T-joints 4 × 8 g if they go).
5. **Both variants** (legs on, legs off) and the skids' drone kept as a fixture file for the audits (§6).

### 8. The log, determinism and the budget

- **Header:** the tip law's entry (the switch, z_ref, α_E, ν, r_s, N_kt, J, the tip's area and diameter); `surfaces.json`'s new `tip_resistance_pa` rows under the table's hash; the `leg.setdown` draws with bounds.
- **Step record:** per leg the tip's embedment and deepest sink (in `LegCompression`'s place for a tip corner, or a new column at 1.3's format number), its vertical hold and lateral hold this step.
- **Events:** `TipFreed` (corner, its hold's peak, its embedment as it let go, the cause: grip, suction or adhesion) and `TipPloughed` (corner, the push across, the depth), so QA can tell a stuck leg from a pilot's sharp liftoff.
- **Closed forms only** (+ − × ÷ √): one node a corner instead of three, no Newton beyond the series solve's; 0 B in steady state; the step is cheaper than the half-skid's.

## 3. The hand calculations (`tools/flightbench/handcalc/four_legs.py`)

- **The drone on four legs** (derived from drone.json and the logged headers): 2 kg loadout 6.0438 kg without the skids, centre of mass 0.2493 m over the tips, I_xx 0.0726, I_zz 0.0612 kg·m², a leg's share 14.82 N; 1 kg loadout 5.0438 kg, 0.2311 m, 12.37 N.
- **Touchdowns, level, throttle cut** (one leg's quarter, its spring and damper ζ 0.2 at 57 kN/m (117 N·s/m) in series with the tip; thrust 0, the idle's few per cent left out, assumed):

| Ground, set | Down | Peak a leg | Peak | Depth (end) | Rebound | Energy in → soil, soil damping, leg damping, rebound |
|---|---|---|---|---|---|---|
| meadow dry | 0.5 m/s | 48 N | 3.2 g | 9.6 (7.7) mm | e 0.27, 1.0 mm | 0.33 J → 0.17, 0.12, 0.01, 0.01 |
| meadow dry | 1.5 | 117 N | 7.9 g | 22.8 (20.3) | e 0.28, 9.2 mm | 2.04 → 1.16, 0.72, 0.14, 0.14 |
| meadow wet | 0.5 / 1.5 | 31 / 67 N | 2.1 / 4.5 g | 19.1 / 38.6 | 0 / e 0.12, 1.7 mm | 2.27 → 1.06, 1.16, 0.03, 0.03 at 1.5 |
| meadow saturated | 0.5 / 1.5 | 25 / 51 N | 1.7 / 3.4 g | 28.8 / 50.9 | none | |
| tilled dry | 0.5 / 1.5 | 30 / 65 N | 2.0 / 4.4 g | 21.2 / 42.1 | 0 / e 0.12 | |
| tilled wet / saturated | 0.5 / 1.5 | 19–41 N | 1.3–2.8 g | 45–92 | none | |
| dry_crust dry | 0.5 / 1.5 | 65 / 195 N | 4.4 / 13.2 g | 6.7 / 15.5 | e 0.51 / 0.47, 3.3 / 24.8 mm | 1.93 → 1.05, 0.49, 0.36, 0.37 at 1.5 |
| dry_crust wet / saturated | 1.5 | 143 / 142 N | 9.7 / 9.6 g | 27.6 / 31.8 | e 0.39 / 0.49, 17 / 27 mm | |
| asphalt | 0.5 / 1.5 | 131 / 372 N | 8.9 / 25 g | 0 | e 0.50 / 0.55, 3.2 / 34.7 mm | the leg's damping alone |

  A rougher landing digs deeper on every soil (meadow 9.6 → 22.8 mm dry). On soil one rebound at most, 0–25 mm, and at rest within 0.2–0.8 s; firm crust and asphalt keep the legs' own bounce (P7: "the plastic landing legs will bounce the drone back up"). Model 103 on saturated crust at 1.57 m/s rebounded 0.49 m/s (measured), the tip law 0.73 m/s at 1.5 m/s: the bounce on firm ground is the leg's spring, not the soil's.
- **Careful liftoffs, each leg's release, wet sets, the liftoff after a touchdown, sliding:** §2 decisions 2 and 3.

## 4. The targets 1.3 is tested against

1. **Two seeds start differently, one replays:** per-leg start depths differ between seeds by the draw; a replay is bit for bit.
2. **No magnet** (the spec's careful liftoff): from the start on meadow in Severe rain, the ground's pull on the drone never exceeds the four holds' sum (4 × 2.67 N, against model 103's 26.9 N measured); the last leg lets go at T/W ≤ 1.13 + the controller's share (against 1.31–1.71), the legs leave one by one (the first to the last at least the hold's 0.1–0.3 s at the pilot's 0.41 /s climb), and the upward speed as the last lets go is under 0.1 m/s (against 0.34–0.52). Known-wrong: model 103's skids fail it (the switch).
3. **Hard ground:** on asphalt and a slab no tip enters and no leg is held at the next liftoff (T/W ≤ 1.01 at the last release).
4. **No sled:** with the tips in (after a 0.5 m/s touchdown on meadow), the throttle below hover and a push across of 4 N (a Severe wind's order), the drone moves less than the legs' bending (under 0.05 m), against F23's 0.20 m. Known-wrong: Coulomb on the tips' faces alone slides.
5. **A rougher landing digs deeper:** at 1.5 m/s the tips end deeper than at 0.5 m/s on meadow, tilled and crust by the table above (±20 %, for the series solve's damping split).
6. **A landing on sod has the drone's weight:** level, 0.5 m/s, throttle cut, dry meadow: one rebound at most 2 mm, at most 2 contacts lost, a slide under 0.05 m at up to 0.3 m/s along, at rest within 1 s. Known-wrong: model 103 (F05: 8 lost, 1.84 m).
7. **PHY-03** (`flightbench --ground`, design 13's targets read on the group, "one or many", as w9 2.9 found): dry firm held on at least one flight and at most a third; dry soft more often; Rain soft above half; Severe rain at least Rain. The tip law gives the order from depth and wetness (§2.2); whether dry firm ground holds "sometimes" depends on the depth a landing left and on r_s, printed with its band. A miss means a missing mechanism (roots and stems, W9 2.10), never a tuned rate.
8. **The kept audit:** with the switch and the skids' fixture every golden is model 103's pin, every record and event.
9. **0 B in steady state**, the step's cost against PLT-04.

## Risks / not settled

- **r_s carries the dry account.** Assumed 0.01 inside a published band of 0–0.04 that moves the dry hold after a rough landing 0–15.5 N a leg. It is the one number nobody can measure from the logs; 1.3 prints PHY-03's rates against it.
- **Wet tilled soil and crater spoil are mud.** With the table's wetting factors (×0.3, ×0.15, assumed) a tip stands 39–117 mm deep there at rest and holds 6–9 N, the last corner letting go at T/W 1.31–1.45: a stuck drone in mud, physical, and not "only slightly" at a start there. Every pilot liftoff in the baseline was from meadow; the start points on tilled ground in rain are checked at 1.3.
- **The soil's viscous damping** (the table's, carried) takes 35–70 % of a touchdown's energy on soil at the tip's speed; surfaces-review §4 expected it to fall where it overlaps the plastic loss. 1.3 prints the split without it.
- **The stance.** On four legs the drone goes over at 0.74 m/s along when its tips catch, against the skids' 1.85 m/s; the baseline's landings touched at 0.45–2 m/s along. Physical for the pilot's kit, and the reason the legs' bending and the controller's answer matter; 1.3's landing scenarios report it.
- **The q_t rows** are assumed within published ranges, not measured on this ground; the static depths at the start (3–31 mm on meadow) are their consequence.
- **Plugged or coring:** a cut open tube cores soft soil before it plugs and would go deeper with less force; 1.2's tip may moot it.

## Questions about the pilot's kit (for the orchestrator to ask; never how the world behaves)

1. What is at the very end of each leg: the cut tube, a cap or plug, a rubber foot, a ball? (1.2 may read it from photos T and U.)
2. Are the four legs the same length on their drone, or set by hand to level it?

## Sources

Randolph, M.F., Leong, E.C., Houlsby, G.T. (1991) One-dimensional analysis of soil plugs in pipe piles, *Géotechnique* 41(4) · Schmertmann, J.H., Hartman, J.P., Brown, P.R. (1978) Improved strain influence factor diagrams, *J. Geotech. Eng. Div. ASCE* 104(GT8) · Sneddon, I.N. (1965) the rigid punch on an elastic half-space, *Int. J. Eng. Sci.* 3 · Robertson, P.K. (1990) Soil classification using the cone penetration test, *Can. Geotech. J.* 27 · De Nicola, A., Randolph, M.F. (1993) Tensile and compressive shaft capacity of piles in sand, *J. Geotech. Eng. ASCE* 119(12) · Lunne, T., Robertson, P.K., Powell, J.J.M. (1997) *Cone Penetration Testing in Geotechnical Practice*, Blackie · Matlock, H. (1970) Correlations for design of laterally loaded piles in soft clay, OTC 1204 · Jaky, J. (1944) the coefficient of earth pressure at rest · Bekker, M.G. (1969) and Wong, J.Y. (2008) as `p7a-ground` · penetrometer readings: [soilquality.org.au, soil compaction (Tasmania)](https://www.soilquality.org.au/factsheets/soil-compaction-tas) (1.5 and 2.5 MPa, read); the Wisconsin pasture compaction study (0.8–3 MPa at 4–8 cm) and the University of Debrecen wheel-track study (2.33 and 5.36 MPa), both as their search summaries gave them, the full texts not read.
