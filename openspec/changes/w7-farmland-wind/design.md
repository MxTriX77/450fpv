# Design: w7-farmland-wind, tasks 1.1 (the wind's height over the local ground, and the hills) and 1.2 (the belts and the village)

**Owner of this part:** physics-engineer · **For:** 1.2 (physics-engineer), 2.1 (qa-engineer) · **Requirements:** PHY-13, PHY-13a, PHY-15, PHY-18, PLT-04 · **Decisions:** D-072, D-088 · **Model:** 84 (83 is W3's merge-back) · **QueryVersion:** 8, unchanged (no world code changed)

Numbers are labelled: **measured** (run here, the command named), **published** (cited), **recalled** (a published rule, not re-read in this task), **derived** (computed from those), **assumed** (a named constant, with its reason), **numerical** (the method's own). "Before" is the branch's base, 31ab9ed3 (model 82), built in a temporary worktree with the same scratch probes.

## Context
- **W3 2.5's finding:** the mean profile took its height over the ground under the drone for every point. A laid node on a hillside felt the wind as if it stood metres up, and in Severe wind the far line slid at 0.04–4.7 m/s and never rested.
- **The farmland's relief** (measured, `worldbench --golden`): the map rises from 12 to 43 m, its hills 20–30 m over valleys 300–700 m apart. Over the 16 m grid of decision 2 the steepest slope is 0.217 (12°); no node passes 0.32. The village's levelled plots reach 37.6° (W2), but only over their 7 m banks. The ledge (W2, W3) is a vertical step of 1.40–1.61 m around the surround's outer edge.
- **What 1.1 leaves to 1.2:** the belts' and the village's wakes and canopy against the shelterbelt data, the farmland line's full rerun and its freeze.

## Decisions

### 1. Every wind query takes its own ground (`WindField.At(point, ground)`)
- **The rule:** a point's profile, wakes and canopy read its height over the ground under it, never the drone's.
  - **The drone's points** (each hub, the centre of pressure, the coil, the body, the camera; `FlightCore.WindGround`) take SampleGround's height under each. Over a terrain that is the terrain's own surface, hills and slopes included.
  - **Each fiber node** takes its own ground, the tether's: SampleGround's height, or over a hole the soil under the node (`FlightWorld.SoilUnder`). `FiberLine.AddWind` samples it for the nodes it posts to the wind's batch; `Loads` reads the node's `_groundHeight`. Both give the same bits.
  - **A loose piece's tubes** each take their own ground (`LoosePieces.Ground`).
- **Over a hole, the drone's points keep the step's ground and cut frame** (model 20's soil under the origin, design 10's frame). The API marks this with a NaN ground.
  - Why: S04's straw patch is a mosaic of boxes and gaps. Within 1.5 m of the range flight's foot, 388 of 3,600 points differ from the centre's soil, from −0.125 to +0.129 m (measured, a scratch probe).
  - That is under the cover's still layer (d + z0 = 0.301 m): roughness, not terrain. Reading it at each hub moved the range golden for no physical gain. It also cost a ray cast per point.
  - A fiber node far from the drone reads its own soil, so a node in a trench is never given the drone's.
- **A cut's frame holds a point** when the point lies within its opening (W/2 + `OpeningReach` across the axis, along it within the opening) and its own ground lies under the lip (`CutFrame.Holds`). This is the world's own test, as `Locate`'s is for the origin. Elsewhere, as at a laid node 50 m away, the open field applies over the point's own ground. `At(point)` keeps the framed rule for the tests on a plane.
- **The batch** (`WindBatch.Add(point, ground)`) carries each point's ground to the worker. The field is still a pure function of the step's frame and the point, so a point's wind is the same bits on either thread.
- **The known-wrong is model 82's rule.** A node lying on the farmland's highest crest, with the drone over the village, gets 0.000 m/s from its own ground and 7.42 m/s from the drone's (`farmland_wind.node`).

### 2. The low-hill model: linear theory, spectral (`HillFlow`)
- **Source** (published): the linear theory of a turbulent boundary layer over low hills.
  - Jackson and Hunt (1975), *Q. J. R. Meteorol. Soc.* 101.
  - In Hunt, Leibovich and Richards (1988)'s layers (*Q. J. R. Meteorol. Soc.* 114).
  - Taken to three dimensions by Mason and Sykes (1979).
  - Evaluated on real terrain in spectral form, as MS3DJH (Walmsley, Salmon and Taylor 1982) and WAsP (Troen and Petersen 1989) do.
  - Lubitz and White (2007, *J. Wind Eng. Ind. Aerodyn.* 95, read) call it "essentially the only satisfactory analytical method" for speed-up over hills.
- **Per Fourier mode k of the heights** (L = 1/|k|, recalled: the spectral models' scale):
  - **The outer layer's potential flow:** the horizontal perturbation per unit speed (k_i k_j/|k|)·ĥ and the streamlines' slope i·k_i·ĥ, each falling as e^(−|k|ζ) (derived: Laplace's equation over the ground).
  - **The inner layer's depth** l: (l/L)·ln(l/z0) = 2κ² (recalled, Jackson and Hunt).
  - **The middle layer's height** h_m: (h_m/L)·√ln(h_m/z0) = 1 (recalled, Hunt et al.).
  - **The fractional speed-up** ΔS = σ·A(ζ), ζ = z − d (recalled: Jackson and Hunt's ΔS ∝ U0²(h_m)/U0²(ζ) below h_m):
    - in the middle layer, A = e^(−|k|ζ)·(ln(h_m/z0)/ln(ζ/z0))², the perturbation's momentum held
    - through the inner layer, A holds its value at l
    - in the outer layer, A = e^(−|k|ζ)·ln(h_m/z0)/ln(ζ/z0)
  - z0 and d are the field's own: 0.10 m (Davenport–Wieringa class 4) and 0.201 m.
- **The wind's direction enters linearly.** For an undisturbed mean u, the along-ground perturbation is S·u and the upflow is w = G·u. S_ij is the inverse transform of ĥ·A·k_i k_j/|k|, and G_i that of ĥ·e^(−|k|ζ)·i·k_i. So five grids a level serve every bearing, nothing is rebuilt when the wind turns, and no worker is needed. The vertical component makes the flow follow the ground: 5 m over a 2.5 % slope the streamline's slope is 0.0244 (HillFlowTests).
- **The composition:** the hills scale the local undisturbed mean, whatever shapes it: `Hill(_mean·speed)` after the profile, the wakes' speed and the canopy's share (`WindField.Open`), and in a cut's drive. The turbulence keeps its σ and its taper over each point's own ground (not modelled: the turbulence's distortion over a crest).
- **Validity** (published: Taylor and Lee 1984's guidelines, as Lubitz and White give them):
  - Slopes under 0.3. Finnigan (1988)'s compilation: separation never below 0.27, always from 0.32.
  - L1/z0 over 100, L1 under 2 km.
  - A mode of L under 2·100·z0 is rolled off by a raised cosine and gone at L = 10 m (numerical: no ringing). The domain's longest mode is 652 m.
  - On the farmland every grid slope lies under 0.27 (0.217 at most). The main crest's L1/z0 is 1,800.
- **The grid** (numerical):
  - 256² nodes 16 m apart about the map's centre, so 4,096 m from (−1548, −1548): the map, its surround and 548 m of the far ring.
  - Each node is the mean of 4 × 4 ground samples over its cell, so the 1 m terrain does not alias.
  - The heights, less their mean, taper to it over 256 m at the edge (the FFT's period), and the output fades to 0 there.
  - 9 levels from ζ = z0 to the profile's 100 m top, in equal steps of ln(ζ/z0) (the field's own log). Read linearly between levels, bilinearly between nodes. Stored in double precision: the step's data takes no single precision (StepPurityTests).
  - Radix-2 FFTs on DetMath's twiddles, basic operations only.
  - Built once per world and kept for the next flight (`HillFlow.Of`, by content hash; listed as load-time only in StaticInitTests): 386–499 ms (measured), about 24 MB.
- **Where it comes from:** `FlightWorld.Hills`, the map's, for a pilot's level (FlightCore). A replay rebuilds it from the world it opens; the log's world hash and model 84 name it, so the format is unchanged. A wrapper world (a bench's timer) passes its map: `new FlightWorld(clock, map)`.
- **The check against the published guidelines** (measured, `HillFlowTests.ARidgeAndAHill_SpeedUpAsTheGuidelines`, h/L1 0.05, L1 200 m). Taylor and Lee (1984) give ΔS_max = 2.0 h/L1 over a 2-D ridge and 1.6 h/L1 over a 3-D axisymmetric hill, ±15 % (Kaimal and Finnigan 1994; Lubitz and White 2007, read), falling as e^(−A z/L1) with A = 3 and 4 (recalled).

  | Shape | Model's peak | Published | At 20 m: model / guideline | Lee foot (√3·L1) over the crest |
  |---|---|---|---|---|
  | Witch of Agnesi ridge | 1.77 h/L1 | 2.0 ± 15 % | 0.058 / 0.074 | −0.153 (its potential flow's −0.125) |
  | Axisymmetric hill, ĥ ∝ e^(−\|k\|a) | 1.38 h/L1 | 1.6 ± 15 % | 0.041 / 0.054 | −0.215 |
  | Known-wrong: the potential flow alone | 1.00 h/L1 | out of the band | | |

### 3. Past the range: the separated lee of a step
- **Where the slope is past 0.32** (Finnigan 1988), the flow separates, so the linear theory's lee no longer holds there.
- **On the farmland** only the ledge and the far seams are such steps. The far seams (at most 99 mm) lie under the cover's still layer, so a step separates only where its face exceeds d + z0 (`HillFlow.Steps`): the ledge's 4 edges.
- **The model** (`WindField.InLee`) is Cavity's closed bubble behind a step, already the trench's (p7c-wind design 10):
  - **The bubble:** reattachment at X_R = 7.2·D (measured, Hotta et al. 1998's step; the literature's 7.0–7.5). D is the lip's height less the point's own ground.
  - **Below the lip, Roshko (1955)'s cell over the bubble:** u = A·cos(πη/D)·sin(πξ/X_R) across the step and w = A·(D/X_R)·cos(πξ/X_R)·sin(πη/D), with A = 0.33·U.
  - **U is the field's own mean across the step** at 1.5·D over the lip (Cavity's drive), hills included.
  - **The component along the step** is the open field's over the floor (the independence principle: a swept separation takes the normal component).
  - **The fluctuation** is the floor's own (the closed regime's τ = 1).
  - **The recovery:** over one bubble length (`Cavity.RecoveryLengths`, assumed as Cavity's) the separated share falls linearly to 0, the cell held at its reattachment value, so the field is continuous at X_R and at 2·X_R.
  - **Above the lip,** the field over the lip blends into the field over the floor by the same share.
  - **Within the floor's cover** (d + z0) the air is still, as on any ground.
- **Not modelled:**
  - The windward face: the bubble ahead of a forward-facing step and on its top. The linear model's smoothed escarpment stands for it, and `AStepsLee` checks that a face to the wind is the open field to the bit.
  - The village plots' banks (37.6°, under 7 m long). They are shorter than the model's validity (L1/z0 > 100 asks for 10 m), so the grid's roll-off smooths them, and their lee is the ruins' wake grid's.
  - Stability and thermals (X-12).

## The rows (`worldbench --golden`, `golden/farmland-wind.txt`)
All with the wind from the west (toward +x); measured; ΔS along the wind; JH 2-D is Jackson and Hunt in two dimensions on the measured profile along the wind. That reference is computed in the bench, independently of the grids: the potential flow by quadrature of the profile's slope (Poisson's kernel at each height), the layers at the hill's own L1.

**The crest:** the map's highest point (86, 924), 42.83 m. h 28.96 m over the upwind foot 746 m away, L1 180 m, h/L1 0.161, L1/z0 1,800, steepest slope from foot to foot 0.250 (inside the range).

| Height | ΔS model | JH 2-D on the profile | Taylor and Lee, 3-D hill – 2-D ridge, ±15 % |
|---|---|---|---|
| 2 m | 0.329 | 0.327 | 0.246–0.311 |
| 5 m | 0.272 | 0.327 | 0.230–0.296 |
| 10 m | 0.193 | 0.327 | 0.206–0.272 |
| 20 m | 0.121 | 0.238 | 0.165–0.231 |

- **At the surface** the 3-D, multi-scale model gives Jackson and Hunt's 2-D value (1.01×), inside the guideline's band to 10 m.
- **Higher up it falls faster:** 0.51 of JH 2-D, and 14 % under the guideline's band, at 20 m. That is the theory's own answer on this terrain:
  - the crest's short scales have thin inner layers and fall as e^(−|k|z), where the single-scale formula holds L1's 12 m inner layer;
  - the 3-D relief lets the flow pass round.

**The lee slope:** downwind of the crest. The lee's L1 is 76 m; its slope at the half-height point is −0.184 (10.4°), inside the range, so no separation.

| Point | 2 m | 5 m | 10 m | 20 m |
|---|---|---|---|---|
| Half height, 76 m downwind: model / JH 2-D | −0.011 / −0.014 | 0.009 / −0.014 | 0.022 / 0.006 | 0.022 / 0.024 |
| Twice as far, 152 m: model / JH 2-D | −0.097 / −0.101 | −0.084 / −0.101 | −0.056 / −0.073 | −0.029 / −0.044 |

The lee's shelter, about 10 % at 2 m, agrees with JH 2-D within 0.017 at 2 and 5 m.

**Below the ledge:** the east step at z = 500, lip 23.974 m over the far ring's 22.466 m (D 1.508 m), X_R 10.9 m. The wind blows out over it (seed 36, 2.8° off east); the drive is 4.45 m/s at 1.5 D over the lip. u is across the step, over the drive.

| ξ/X_R | 0.1 | 0.25 | 0.5 | 0.75 | 1.0 | 1.25 | 1.5 | 2.0 | 2.5 |
|---|---|---|---|---|---|---|---|---|---|
| 0.5 m over the floor | −0.05 | −0.12 | −0.17 | −0.13 | −0.03 | 0.06 | 0.12 | 0.25 | 0.33 |
| Known-wrong, unseparated | 0.36 | 0.36 | 0.35 | 0.35 | 0.34 | 0.34 | 0.34 | 0.33 | 0.33 |
| 1.0 m over the floor | 0.05 | 0.11 | 0.15 | 0.10 | 0.02 | 0.11 | 0.24 | 0.47 | 0.62 |

- **The reverse flow** is −0.17 U at mid-bubble 0.5 m over the floor (the cell's −0.33·cos(πη/D)). The step literature's maximum backflow is about 0.2 of the free stream (recalled: Eaton and Johnston 1981).
- **The flow turns forward** between 1.0 and 1.25 X_R (published: 7.2 D, 7 ± 1 D in the literature).
- **The open field returns** by 2·X_R, to the bit past it (HillFlowTests).

**An open field:** no spot on the farmland is flat. The flattest of the map and its surround is (−100, 280), with 7.08 m of relief within 200 m.

| Height | 2 m | 5 m | 10 m | 20 m |
|---|---|---|---|---|
| ΔS model | −0.063 | −0.047 | −0.033 | −0.024 |
| JH 2-D on its profile, layers at L 200 m (assumed: the farmland's hill scale) | −0.052 | −0.052 | −0.052 | −0.036 |

The plane gives 0. The spot lies in the lee of the hills upwind, and the two agree within 0.02.

**A laid node:** on the crest, 5.1 m above the drone's ground (over the village). Its own ground gives 0.000 m/s (the cover's still air). The drone's ground, the known-wrong, gives 7.42 m/s.

## Golden files, audited case by case
- **`flightbench --golden`:** 60 PASS lines, every case and kept audit unchanged.
  - Every golden flight flies a plane. Its points' ground there is the origin's, and over S04's patch (a hole) the drone's points keep the step's ground (decision 1).
  - A first build read S04's soil at every hub. It moved the range flight and every kept audit holding a range hash (about 25 lines). That is how decision 1's hole rule was chosen. Nothing was re-recorded.
- **`worldbench --golden`:** floor 5, test range 14, wakes 11 and farmland 12 pass unchanged. `farmland-wind` is new: 7 cases, recorded at model 84 (its header: re-record only with a model bump). Release and Debug give the same bits.
- **`flightbench --slopes`:** PASS before and after. Every row is the base's to the printed digit but the two lines', which fly Calm over a terrain. Their laid nodes now lie in the cover's still air, where the drone's ground had blown them:

  | Row | Deepest segment over the flight | Crests' nodes | Other |
  |---|---|---|---|
  | The line over the ridge | 0.0746 → 0.1690 m | 6 → 9 | the crest's node 0.083 → 0.069 m off the crest line |
  | The line over the ledge | 0.1049 → 0.1736 m | 2 → 2 | the fastest node 3.50 → 3.49 m/s; a crest's node on the lip 3,319 → 3,417 steps |

  Both stay inside the 0.2 m `CrestDepth` that FiberLine guarantees between its tests, whole, with 0 wedges.
- **The model** goes to 84 (`LogFormat.ModelVersion`'s note). On a plane a flight steps as in 82 while its body's origin, its fiber's nodes and its loose pieces lie over the plane itself.

## Determinism, allocation, replay
- **IEEE basic operations and DetMath only:** the FFT's twiddles are DetMath.SinCos, l and h_m come from fixed Newton passes, and every value is a double. Two builds from the same ground give the same bits (HillFlowTests, 2,000 samples).
- **0 B:** a sample allocates nothing (10,000 samples), and a 6,000-step Severe flight over the farmland's highest hill with its line allocates 0 B after its first step.
- **The same flight replays bit for bit** (`FlightReplay`: 6,000 steps, 979 events, 600 loose-piece records). The replay rebuilds the hills from the world it opens.

## Costs (measured on AC, Balanced plan, a shared machine: indications only)
- **The wind's own work at 42 points** (34 nodes and 8 at the drone, the farmland line at 46 s; a scratch probe, not committed):
  - their ground samples: 3.3–3.7 µs a step on the flight thread (82–88 ns a point)
  - a hill sample: 70 ns, so about 3 µs a step
  - the field with the hills: 70–76 µs a step, against 67–72 µs without (most of it on the batch's worker)
  - model 82's rule: 78–84 µs, because it lifts the laid nodes out of the still layer into the full turbulence sum
  - over a hole a node's soil is cast twice, once for the batch (`AddWind`) and once by the tether's own ground pass. On the test range's line flight (`houses-line-step`, 60 s, 1.5 M nodes posted) no node lay over a hole, and the nodes' ground cost 0.71 µs a step (a scratch counter, not committed)
- **The farmland line's step, its first 46 s from the climb's end** (14–46 s, CPU load 3–52 % as each run began):

  | | p50 | p90 | p99 |
  |---|---|---|---|
  | Before (model 82) | 116–122 µs | 154–166 µs | 267–332 µs |
  | After (84) | 121–137 µs | 187–216 µs | 348–387 µs |
  | Per-point ground without the hills (a scratch build) | 125–128 µs | 297–420 µs | 772–937 µs |

  - The step's cost follows the line's state more than the wind's work: that flight's resting laid nodes made a dearer line.
  - The p90 and p99 rises are the tether's, not the wind's (1.2's freeze; below).
  - PLT-04's quiet-machine run is QA's (2.1).

## The farmland line over the hills (1.1's view; 1.2 reruns it whole)
- **Measured** with a scratch probe, both builds, the line's first 46 s: the far laid nodes, more than 30 m from the drone and within 5 cm of their ground.

  | | Before (model 82) | After (84) |
  |---|---|---|
  | Wind at the laid nodes | 3.1–7.4 m/s: the drone's ground, up to 21 m below theirs | 0.000 m/s, the cover's still air |
  | Nodes' speed over the hills before the belt, to 34 s | up to 1.87 m/s | at most 0.18 m/s |
  | Nodes' speed across the belt, 36–42 s | up to 2.91 m/s | up to 1.85 m/s (the line dragged, not blown) |
  | Nodes' speed after it, 44–46 s | 0.35–4.08 m/s median, 3.3–4.7 m/s max | 0.008–0.047 median, 0.06 max |

- **The trail holds 1 point in both:** the resting nodes do not freeze yet (1.2).

## For the next units (suggested, not this task's)
- **1.2, the freeze:** the laid nodes now rest (≤ 0.06 m/s after the belt) but never freeze into the trail. In the per-point build without the hills the step's p99 rose to 0.8–0.9 ms. A rest test for laid nodes would take both the cost and the tests away.
- **1.2, the belts and the village:** the hills now scale the wake grid's and the canopy's mean. Their rows can take the crest's and the lee's numbers above as the open terrain's.
- **The windward face of the ledge** (a forward-facing step) and the village's banks, if a gate's flight shows a need: published data exist for both (the step literature, Kim, Patel and Lee 1997 for steep hills).
- **QA (2.1):** a quiet run of the farmland line's step.

# Task 1.2: the belts and the village

**Owner of this part:** physics-engineer · **For:** 1.3 (physics-engineer), 2.1 (qa-engineer) · **Requirements:** PHY-13, PHY-13a, PLT-04 · **Decisions:** D-072 · **Model:** 84, extended · **WakeGrid.Version:** 2 (was 1) · **QueryVersion:** 8, unchanged

Labels as in 1.1. "Before" is the branch's base, 52ce0ac0, built in a temporary worktree with the same scratch probes.

## Context: what the farmland has (measured, `maps/farmland/placements.json`, `MapWind.Of`)
- **No shelterbelt.** None of the 1,679 rows has a group, so `MapWind` finds no "posadka" belt and no canopy strip: all 2,589 wind volumes are wake obstacles. The 294 trees stand in groves of 4–9 (20–30 m across) and short rows; the 1,352 bushes make hedges along the field edges.
- **The F6|F7 boundary** is a hedge along each field's edge (F6's at z ≈ 712, F7's at z ≈ 773, bushes 2–3 m tall) and, between them at z ≈ 758, a row of four 12 m trees in two pairs 55 m apart.
- **The village's five ruins** are vendor shells described by 100–230 thin opaque boxes each (wall courses 0.24 m thick, stacked 0.25–0.5 m high).

## What the check found (measured: scratch probes on the base, and `worldbench`)
1. **A low obstacle hid a tall one's wake.** The wake grid kept only the nearest obstacle on a cell's path. Behind F6|F7's east pair, wind from the south, F7's hedge stands 14 m behind the trees: at 6 m the trees' shelter (u/u0 0.54) ended at the hedge, and the field above it read 0.92–1.00 from there on.
2. **The obstacle grid's porosity rule mis-read thin and stacked parts.** Version 1 gave each 2 m cell β = Π 1 − f·(1 − β^(2 m / D)), f the volume's covered fraction on 4 × 4 samples, multiplied over overlapping volumes. So:
   - a 0.24 m solid wall covered f 0 or 0.25 of its cells: diluted to 0.75, or missed between the samples (the ruins' wall cells had gaps);
   - stacked courses of one wall multiplied as if they stood in series (Ruin_RoofCollapsed's 229 boxes read 0.000);
   - a trunk under its crown multiplied the crown's porosity over the whole height (lanes through a trunk cell 0.33–0.41 against 0.6–0.7 beside it);
   - a thin fence (Z06's planks, 0.1 m, porosity 0.35) read about 0.8.
3. **The turbulence boost ignored the porosity.** A lane grazing a crown's edge (β 0.97–0.99) boosted σ as much as the belt the boost was fitted on. With exact lanes and the overlying wake such grazes are common.

## Decisions

### 4. An obstacle's porosity is its frontal porosity across the lane (`WakeObstacles.Transmission`)
- **The rule** (derived: K§10.3's "optical porosity of a horizontal path" made exact): for the run of obstacle cells the march crosses, the volumes overlapping them, seen along the wind through a 2 m lane centred on the march, averaged over 8 lines across it and over the height from the run's lowest base to its highest top:
  - on each line at each height the volumes crossed there multiply;
  - a solid volume passes nothing, however thin;
  - a porous one passes β^(chord / D), D = footprint perimeter / π: Beer–Lambert over its chord, with D the mean width its β was measured over (`tools/blender/lib/plant.py`: β = 1 − opaque area / (D × height), averaged over 4 bearings);
  - parts at different heights stand side by side: the average over the height is exact, between the parts' own bases and tops.
- **A cell is an obstacle** where a volume's footprint overlaps it (exact: separating axes for a box, the distance to the square for a cylinder), so no thin part falls between samples. An obstacle whose lane misses every volume is no obstacle there.
- **Published support:** Cornelis, Gabriels and Lauwaerts (USDA-ARS wind erosion symposium proceedings, read): model windbreaks of stems without a canopy reduced the wind by under 10 %, and a dense lower part sheltered most near the barrier. Optical porosity is the open share of the side view (Zhou, Brandle, Mize and Takle 2004, *Agroforestry Systems* 61, read).
- **Numerical:** 8 lines, 0.25 m apart (a 0.24 m wall along the wind is met by about 1 line in 8, its share of the lane).
- **Checks:**
  - `WakeGridTests.AThinWallBlocksItsLaneAndAPartUnderACrownStandsBesideIt`: a 0.24 m wall between the old samples is opaque across and passes 7/8 along; a trunk and crown average by height.
  - `TheObstacleGridFollowsK10_3`: a maple's lane is the chord formula to 1e-12, the house's 0.
  - `MapWindTests`: a lane across the posadka's section reads its α to 1e-7 (its axis is 0.012° off east).
- **Not modelled:** a porosity that changes with height enters the wake as one β over the obstacle's height. A house with solid walls under a stripped roof (the test range's H01_D: walls 0–2.4 m, the roof's battens, β 0.25) has no standing bubble in the single-β model, though its walls would shed one. Version 1 made it solid to 5 m by multiplying its 60 wall-head boxes.

### 5. The overlying wake
- **The rule:** past a cell's nearest obstacle the march goes on, on its own path, to the nearest obstacle taller than it whose wake reaches the cell (x ≤ 30 H, 300 m) and whose lane is not clear. A lower obstacle never hides a taller one's wake.
- **The composition** (derived: each obstacle's wake is relative to the flow that reaches it, as porosities multiply along a path, K§10.3):
  - the speeds multiply; in the overlying obstacle's reverse flow, which the nearer one cannot turn, that reverse flow;
  - the turbulence's excess variances add (independent eddies), the eddies at the height of the obstacle carrying the larger excess.
- **The truncation** (measured, a scratch probe over every open cell's own path): a cell keeps two wakes. A third reaching obstacle, or a farther one no taller than the nearest, is dropped. The dropped shelter's upper bound (Perera's peak 9.75·0.51·(1 − β)/max(X, 5) at φ 1, the bleed 1 − β below 5 H):

  | Wind from | Cells that drop one | Dropped bound > 0.10 | > 0.20 | Version 1 (the nearest alone): > 0.10 | > 0.20 |
  |---|---|---|---|---|---|
  | south | 25.9 % | 11.4 % | 3.7 % | 23.8 % | 9.6 % |
  | 30° | 36.2 % | 15.6 % | 5.5 % | 29.3 % | 12.8 % |
  | west | 26.6 % | 12.2 % | 4.3 % | 24.5 % | 11.0 % |

  The second slot halves what is dropped. Each further slot costs a cell array (32 MB on the farmland).
- **Check:** `WakeGridTests.ATallerObstaclesWakeGoesOnOverALowerOneBehindIt`. Behind a hedge 15 m downwind of a 10 m row, the speeds are the hedge's times the row's alone, bit for bit, and the excess variances add to 1e-12. The hedge's wake alone (the known-wrong) reads 0.99 at 6 m; the composite reads under 0.8.

### 6. The porous boost scales with the obstacle's deficit
- **The rule:** the porous amplitude A = 1.15·(1 − β)/(1 − 0.45), 0.45 the porosity of the fixture's belt it was fitted on. Derived: the wake's excess turbulence is made by its momentum deficit, (1 − β) as Perera's; denser windbreaks make more turbulence (Brandle, Zhou and Hodges 2002, read). The bluff body's 2.0 is unchanged.
- **Check:** the fixture's belt keeps the literature's 2×: mean T over 5–15 H 2.012 (2.043 before; β 0.466 against 0.473, `worldbench --wakes`).

### The wake grid's version is logged and switchable
- `WakeLayout.Version` (2, or 1) rides in the log's wind entry, so a log of version 1 replays its own grid.
- Version 1's rules are kept behind it, bit for bit, for the kept audits: every golden flown over an older model's record or in airmode flies version 1 (`GoldenFlights.Wakes`).

## The rows (`worldbench --golden`, `golden/farmland-shelter.txt`)
u/u0 is the wake grid's speed over the open field's at the same height (the hills scale both alike), at a cell's centre, over lanes every metre.

**The reference** (published, read): Brandle, Zhou and Hodges (2002), *How Windbreaks Work*, University of Nebraska–Lincoln Extension EC02-1763, Figure 2, the percent of the open wind behind windbreaks by density:

| Density | 5 H | 10 H | 15 H | 20 H | 30 H |
|---|---|---|---|---|---|
| 25–35 % (deciduous) | 50 | 65 | 80 | 85 | 100 |
| 40–60 % (conifer) | 30 | 50 | 60 | 75 | 95 |
| 60–80 % (multi-row) | 25 | 35 | 65 | 85 | 95 |
| 100 % (solid fence) | 25 | 70 | 90 | 95 | 100 |

- Their text: H is the tallest row's height; under 20 % density a windbreak gives little protection; it shelters fully only when at least 10 H long; gaps funnel the wind.
- **The spread:** Zhou et al. (2004, read) re-analysed the field studies. Optical porosity explains under 52 % of the variance of the least relative speed and of the mean reduction over 25 H, so one curve per class has a wide spread. Neighbouring classes differ by 0.15–0.20 at 5–10 H.
- Below about 0.5 H the relative speed hardly depends on height (Heisler and DeWalle 1988's field data, as Zhou et al. 2004 give them, read).

**The hedge:** F7's south hedge, 11 bushes 72 m long (31 H), H 2.30 m, wind from the south; the lanes along its middle half. Its frontal porosity is 0.746, so 25 % dense (version 1's cell rule 0.735: for bushes the two rules agree).

| u/u0 at 0.25 and 0.5 H | 2 H | 5 H | 10 H | 15 H | 20 H | 30 H |
|---|---|---|---|---|---|---|
| Model, mean (the lanes' least–most) | 0.60 (0.36–0.94) | 0.63 (0.38–0.95) | 0.78 (0.64–0.97) | 0.84 (0.74–0.97) | 0.87 (0.79–0.97) | 0.92 (0.85–0.99) |
| Brandle et al., 25–35 % | | 0.50 | 0.65 | 0.80 | 0.85 | 1.00 |

- The model reads the same at 0.25 and 0.5 H, as the field data do.
- It lies within one class step of the curve: +0.13 at 5 and 10 H, +0.02–0.04 at 15–20 H, on the weak side. At 30 H it keeps 8 % of shelter that the curve has lost (Perera's far wake falls as 1/x).

**The trees:** F6|F7's row, the wind from the south, the lanes behind each pair. The pairs are 2.0–2.5 H long, far short of the 10 H a belt needs, so their wake closes sooner than a belt's curve.

| u/u0 at 0.5 H | 2 H | 5 H | 10 H | 15 H | 20 H |
|---|---|---|---|---|---|
| West pair (19 % dense, 2.5 H long) | 0.59 | 0.63 | 0.86 | 0.96 | 1.00 |
| East pair (25 %, 2.0 H long), the hedge 14 m behind it | 0.72 | 0.74 | 0.90 | 0.94 | 1.00 |
| Known-wrong: the nearest wake alone, east pair | 0.95 | 0.95 | 0.90 | 0.94 | 1.00 |
| Brandle et al., 25–35 %, a long belt | | 0.50 | 0.65 | 0.80 | 0.85 |

At 1 H (12.8 m) behind the east pair the field reads 0.87 at 2 H and 0.85 at 5 H; the nearest wake alone reads 1.00 and 0.99.

**The village** (each ruin alone, across its middle third):

| Ruin | Frontal porosity, wind from the north (version 1) | From the west (version 1) | H from the north / west, m |
|---|---|---|---|
| Ruin_RoofCollapsed | 0.066 (0.000) | 0.108 (0.000) | 7.9 / 7.2 |
| Ruin_Burnt | 0.257 (0.262) | 0.070 (0.131) | 3.3 / 3.1 |
| Ruin_Walls, mirrored | 0.348 (0.242) | 0.078 (0.197) | 2.7 / 2.6 |
| Ruin_Stump | 0.045 (0.388) | 0.288 (0.362) | 1.2 / 1.6 |
| Ruin_Walls | 0.300 (0.267) | 0.114 (0.139) | 2.6 / 2.8 |

- Version 1's values moved either way with how the vendor split the walls. Ruin_Stump's low solid walls read 0.388 (porous, no bubble) and now 0.045.
- Recirculation is full at β ≤ 0.1 and gone at 0.3 (Perera 1981, recalled; the model's band).

**Behind the ruins**, wind from the north, u/u0 at 0.25 H across the middle third:

| X/H | 0.25 | 0.5 | 1.0 | 1.5 | 2.0 | 2.5 | 3.0 | 4.0 |
|---|---|---|---|---|---|---|---|---|
| Ruin_RoofCollapsed (β 0.066, H 7.9 m) | −0.02 | −0.10 | −0.21 | −0.25 | −0.23 | 0.51 | 0.48 | 0.94 |
| Ruin_Walls (β 0.300, H 2.6 m) | 0.15 | 0.15 | 0.58 | 0.58 | 0.40 | 0.34 | 0.34 | 0.30 |

- Ruin_RoofCollapsed's reverse flow reaches 2.0 H against Fackrell (1984)'s 2.10 H for its W, L and H (recalled), at most −0.25 (cube wakes −0.3 to −0.4, Martinuzzi and Tropea 1993, recalled).
- Ruin_Walls' broken walls (to 3.9 m, mostly lower) leave it 30 % open: no bubble. At 0.5 H its lee reads 0.32 at 5 H and 0.68 at 10 H, a long solid fence's 0.25 and 0.70 (Brandle et al.).

**Among the ruins:** Ruin_Stump stands 31 m south of Ruin_RoofCollapsed, inside its cavity. On x 537, wind from the north:

| z | 600 | 594 | 590 | 582 |
|---|---|---|---|---|
| u/u0 at 1 m | −0.24 | −0.30 | −0.24 | 0.01 |
| Known-wrong: the nearest wake alone (the stump's) | 0.75 | 0.75 | 0.66 | 0.33 |

## Golden files, audited case by case
- **`worldbench --golden`:** floor, test range, farmland and farmland-wind pass unchanged. `wakes` (11 cases) is re-recorded with WakeGrid.Version 2, its header's rule. Its rows (`worldbench --wakes`, before → after):
  - the fixture belt's lane β 0.473 → 0.466; u/u0 at 0.5 H 0.480 → 0.473 at 5 H and 0.738 → 0.735 at 10 H, still Perera's at its β;
  - the mean T over 5–15 H 2.043 → 2.012;
  - the gap's jet within 0.007; the house's rows identical (solid both ways).
- `farmland-shelter` is new: 5 cases, recorded at WakeGrid.Version 2 (its header: re-record only with a version bump).
- **The range golden flight** (`GoldenFlights.RangeHash`) moved and is re-pinned. It stands 2 m behind the posadka's west section, in its near wake. Version 2 reads the section's porosity along its length (1e-4 before, 0 now) and widens its group by the cells it only grazes.
  - With version 1 it is model 76's pin, every record and event (`GoldenFlightTests.WithVersion1WakesTheRangeGoldenIsModel76s`).
  - With version 2 it leaves that path at step 176: at most 1.7 mm and 0.02° apart, 4 impacts against 3, at rest 0.2452 m up on 8 pairs, 0.9 mm from 76's rest.
- **The kept audits** of older models fly version 1 and keep their pins, every one (32 range lines in `GoldenFlightTests`). The two edgewise audits (models 75 and 78, flown on the current record without airmode) take version 1 with the edgewise flow off, in `GoldenFlightTests` and in flightbench.
- **`flightbench --golden`:** 61 PASS lines: 1.1's 60 with the range re-pinned, and the new one, the range on version 1 as model 76's pin.
- **`flightbench --slopes`:** PASS, every row the base's to the printed digit (the two outputs diffed, timings aside).
- **`MapWindTests`:**
  - the cut's wind in E01's run is re-pinned; with version 1 it is the old pin (`TheCutsWindIsTheSameBitsOnEveryRuntime` checks both).
  - The house check moves to H01_I, the intact house: β 0.004, Perera's far wake (bias −0.001), reattaching at 1.10 X_R, the boost 1.11 of its own. H01_D now reads β 0.25 (its stripped roof) and stands in the posadka's lee, whose wake the overlying slot now carries over its own.

## Determinism, allocation, costs
- **Determinism:** DetMath and WakeGridMath only (WakeGridMath's Exp and Ln for the chords' Beer–Lambert). A build over the cores equals a rebuild on one thread (`worldbench --wakes`: the worker's grid at 10° equals a direct build, 4dafd556623d5f5f). Release and Debug give the same bits (`worldbench --golden` in both). Each thread of a build has its own lane memory; the worker's rebuild uses its grid's.
- **0 B:** the samples and the worker's rebuild allocate nothing (`worldbench --wakes`: the flight thread 0 B, 0 GCs, 0 stalls).
- **Costs** (measured on AC, a shared machine at 19 % load: indications only; the farmland's grid, 500 × 500 cells, the wind from 30°; a scratch probe on both builds):

  | | Before (version 1) | After (version 2) |
  |---|---|---|
  | Build over the cores, before a flight | 363–422 ms | 623 ms |
  | Rebuild on one thread, the worker's as the wind turns past 5° | 0.54–0.96 s | 1.15–1.39 s (the takeover comes 3 s after the request) |
  | One sample (2 M on a lattice over the map, 0.5–40 m) | 0.49–0.59 µs | 0.79–0.85 µs |
  | A grid's cells | 32 MB | 64 MB, and the volumes' lists |

  At 1.1's 42 points a step (the drone's 8, the farmland line's 34) the samples cost up to about 13 µs more a step, most of it on the batch's worker. PLT-04's quiet-machine run is QA's (2.1).

## For the next units (suggested, not this task's)
- **A layered obstacle:** one β per lane loses a house's bubble under a porous roof (the test range's H01_D). A second slot by height (the solid lower part as its own obstacle) would carry it.
- **The side probes' false gap:** a march point just short of an obstacle's ragged face finds the obstacle's own cells on both sides and takes a gap jet (seen on one lane behind Ruin_RoofCollapsed). Asking the two sides to be different groups would stop it; pre-existing, now met more often because exact cells are more ragged.
- **Recovery by porosity:** Perera's single form recovers alike at every porosity; Brandle et al.'s classes recover faster behind dense barriers (a solid fence's 0.70 at 10 H against Perera's 0.50). A porosity-dependent recovery wants a primary dataset read in full (Raine and Stevenson 1977; Heisler and DeWalle 1988).
- **The wake's approach roughness** is the stubble's 0.03 m (`WakeGrid.ApproachZ0`); the field's is 0.10 m.
- **The obstacles and z0:** the landscape's class-4 z0 already holds the scattered obstacles' bulk drag, and the explicit wakes add theirs on top (pre-existing; the overlying slot adds a little).
- **The farmland's groves** have no canopy layer (MapWind reads only the "posadka" group): a drone through a grove at crown height meets the obstacles' bleed, not a canopy's flow.
- **More slots:** a third wake per cell would drop the truncation's 11–16 % further, at 32 MB a slot on the farmland.
- **QA (2.1):** the samples' cost on a quiet machine; the Unity EditMode run of the two cross-runtime pins (PinnedMapWind, PinnedCutWind) at version 2.

## On the line: model 88 (`unity/physics/physics-integration-2--w9-wind`)
1.2 was built as model 84 extended on `--w7-wind` (four commits on 52ce0ac0) and never merged. It is merged onto the physics line at 2955136e (model 86, QueryVersion 9) as **model 88**, assigned by D-072 (87 is the stems' line, landing first). Labels as above; "before" is the line at 2955136e.

**The merge.** One conflict, `LogFormat.cs`'s notes: the line's 84-extended, 85 and 86 notes are kept as they stand, 1.2's "84 extended" paragraph becomes model 88's own note (what moved and why), and `ModelVersion` is 88. Everything else merged by itself. What the line gained since 52ce0ac0 touches none of the wind's code (`Runtime/Wind` and `WakeGrid*` unchanged on the line): the stem field (`IWorldQuery.StemsNear`, QueryVersion 9, the farmland's content hash 0x5D305D09F5D09021), the stems on the props, the anchors' reach, the crown fix, and the laid line's rest (w3 2.4) with its sliding-trail audit. Two merged spots composed by hand, both comments only: `GoldenFlights.Wakes`' summary had landed on `Edgewise`, and the "84 extended" names in the tests' comments now say 88. `GoldenFlights.Edgewise` on the line also drops the stems for every kept audit; the edgewise audits that fly the range pass `wakes2: !old` as well, so each flies the line's pin's physics.

**Golden files, audited case by case** (`worldbench --golden`, Release and Debug):
- `farmland-wind`: only `farmland_wind.version` moves, 1.86 → 1.88 (HillFlow 1, model 88); its six hashes are the line's.
- `farmland-shelter`: only `farmland_shelter.version` moves, 2.84 → 2.88 (WakeGrid 2, model 88); its four hashes are the ones 1.2 recorded on its own branch, so the line's stems, QueryVersion 9 and content hash change no wind volume.
- `floor`, `test-range`, `farmland` (with the line's stem cases), `wakes` (recorded at version 2 by 1.2): pass unchanged.
- **Flight goldens:** the range golden is 1.2's re-pin, 0x9731F9263E4E25B1, and with version 1 model 76's pin 0xAF745CC52447E017, which the line held through 86; every other flight golden is the line's (`flightbench --golden`: 62 PASS lines, the line's 61 and 1.2's version-1 audit of the range).

**The rows, before (the line, version 1) and after (model 88, version 2)** (`worldbench --golden`; "before" from a scratch copy of `FarmlandShelter.cs` with the layout's version set to 1, the line's grid bit for bit; u/u0 at 0.5 H):

| Behind F7's hedge (25 % dense) | 2 H | 5 H | 10 H | 15 H | 20 H | 30 H |
|---|---|---|---|---|---|---|
| Before | 0.70 | 0.71 | 0.85 | 0.91 | 0.93 | 0.95 |
| After | 0.60 | 0.63 | 0.78 | 0.84 | 0.87 | 0.92 |
| Brandle et al., 25–35 % | | 0.50 | 0.65 | 0.80 | 0.85 | 1.00 |

| Behind F6\|F7's east tree pair, F7's hedge 14 m behind | 2 H | 5 H | 10 H | 15 H | 20 H |
|---|---|---|---|---|---|
| Before | 0.95 | 0.95 | 0.90 | 0.94 | 1.00 |
| After | 0.72 | 0.74 | 0.90 | 0.94 | 1.00 |

| Behind Ruin_RoofCollapsed, wind from the north, u/u0 at 0.25 H | 0.25 H | 0.5 H | 1.0 H | 1.5 H | 2.0 H | 2.5 H | 3.0 H | 4.0 H |
|---|---|---|---|---|---|---|---|---|
| Before (version 1: its wall cells have gaps, finding 2) | 1.00 | 1.02 | 1.05 | 0.96 | 0.75 | 0.76 | 0.99 | 0.73 |
| After (β 0.066) | −0.02 | −0.10 | −0.21 | −0.25 | −0.23 | 0.51 | 0.48 | 0.94 |

Reverse flow before: none; after: to 2.0 H against Fackrell's 2.10 H. Behind Ruin_Walls (β 0.300 after, 0.267 before) at 0.25 H: before 0.71 / 0.86 / 0.84 / 0.61 at 0.5 / 1 / 2 / 4 H, after 0.15 / 0.58 / 0.40 / 0.30. Among the ruins (x 537, 1 m up, wind from the north): z 600 1.05 → −0.24, z 594 0.75 → −0.30, z 590 0.71 → −0.24, z 582 0.87 → 0.01.

**The "Behind a belt" scenario:** the farmland has no shelterbelt, so its belt is F7's hedge (31 H long). At 0.25 and 0.5 H it reads 0.63 / 0.78 / 0.84 / 0.87 / 0.92 at 5 / 10 / 15 / 20 / 30 H against Brandle et al.'s 25–35 % class 0.50 / 0.65 / 0.80 / 0.85 / 1.00: +0.13, +0.13, +0.04, +0.02, −0.08, inside one class step (0.15–0.20, Zhou et al. 2004's spread). Before it read 0.71 / 0.85 / 0.91 / 0.93 / 0.95: +0.21 and +0.20 at 5 and 10 H, past a class step.

**Determinism and replay** (measured):
- Release and Debug give the same bits (`worldbench --golden` in both: every case's line identical).
- A Village flight in Windy replays bit for bit with the headless tool: the scripted flight (120 s) from a foot between the ruins, (525, 34.006, 595), heading 0, in Ruin_RoofCollapsed's lee for the drawn wind from 46.3°, on wake grid version 2: 120,000 steps and 718 events, bit for bit, and 6,948 loose-piece records (`replay <log>`, exit 0). The same flight on version 1 (the log's wind entry says 1): 120,000 steps and 721 events, bit for bit, 18,309 loose-piece records, so a version-1 log replays its own grid. `tools/replay --record` refuses a map without a legs-on start point and the farmland has none, so the flight was recorded with a scratch copy of the tool that takes `--foot` alone (the core and the pilot unchanged); the replay is the repository's tool. The flight is not clean: a leg buckles at step 1, because the scripted start stands the drone level over a sloping foot. That is the scripted start's, not the wind's.
- 0 B: `worldbench --wakes`: the worker's rebuild with the flight thread 0 B allocated, 0 GCs, 0 stalls, PASS; `WakeGridTests.StepAndSampleAllocateNothing` and `MapWindTests.TheSameSeedGivesTheSameWindWithoutAllocating` pass; `MapWindTests.InE01sRunTheSameSeedGivesTheSameWindWithoutAllocating`: 0 B on the step; `HillFlowTests.AFlightOverTheFarmlandsHills_Replays`: 0 B after the first step, 6,000 steps and 1,160 events bit for bit.
- The wind's cost per step is owed on a quiet machine (QA, 2.1): this machine was shared, so no timing here counts. 1.2's own shared-machine figures stand above.

**Checks run:**
- `dotnet test tools/simtests -c Release`: "Passed! - Failed: 0, Passed: 847, Skipped: 0, Total: 847, Duration: 17 m 45 s".
- `MapWindTests`, `WakeGridTests` and the two audits again with their output (`--filter`, detailed): 27 passed. The test range's village house: `BehindH01ITheWakeIsFackrellsAndPererasAtSevere`, H01_I β 0.0040, far wake 5–15 H u/U 0.612 against Perera's 0.613 (bias −0.001), the cavity's deepest −0.194 at 0.25 H, forward again at 1.10 X_R, the boost 1.107; the known-wrong without the map's obstacles 1.000 (bias +0.387). The cross-runtime pins (`TheMapsWindIsTheSameBitsOnEveryRuntime`, `TheCutsWindIsTheSameBitsOnEveryRuntime`, version 2 and version 1) pass; `WithVersion1WakesTheRangeGoldenIsModel76s` passes.
- `dotnet run -c Release --project tools/worldbench -- --golden` and the same with `-c Debug`: "worldbench: ALL PASS" both, 58 PASS lines each, identical.
- `dotnet run -c Release --project tools/flightbench -- --golden`: 62 PASS lines, exit 0 (the line at 2955136e: 61, exit 0); "range-flight with the wake grid's version 1: 0xAF745CC52447E017 (model 76's, bit for bit): PASS".
- `dotnet run -c Release --project tools/flightbench -- --scenarios`: "scenarios: FAIL", exit 1, on the line's two checks: the "no ring" known-wrong ("PASSES: the target cannot tell it from the model (FAIL)") and the rain at rest ("2.278 mN against 2.241 mN: FAIL"). The merge changes neither: the whole output is the line's, byte for byte (diffed against the same run at 2955136e).
- `tools/replay`: the Village flight in Windy and its version-1 twin, each "match ... bit for bit", exit 0.
- `python tools/hygiene/precommit.py --tree HEAD`: on the commit (in the handback).

**What is still owed for the wind** (seen, changed neither):
- **The levels' targets** (the ledger's gap 3): Severe meets 9 of 21 targets and the pitch response is about 2.4–3 times low. The next task, not this one.
- **The cover per cell:** the profile's still layer and displacement come from one assumed cover, `WindPreset.CoverHeight` 0.3 m (d = 0.67·h = 0.2 m) everywhere. The stem field now on the line (`IWorldQuery.StemsNear`: per grass cell and layer, stems per m² and the drawn tops' height range) could give each cell its own cover height, and from the stems' frontal area its d and z0. Not done here.
- The suggestions above for 1.2 still stand (the layered obstacle, the side probes' false gap, recovery by porosity, the groves' canopy, a third slot).
