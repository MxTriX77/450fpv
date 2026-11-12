# Design: w7-farmland-wind, task 1.1 (the wind's height over the local ground, and the hills)

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
