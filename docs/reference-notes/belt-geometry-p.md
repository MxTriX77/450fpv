# Clip P: the belt's geometry and the drone's course over it

For `p7c-wind` 1.5. K§8.5 assumed a straight course at 20–50 m that crosses a narrow belt and stays downwind of it, and the harness's 11.9 m belt at those heights leaves the course above the wake. This note measures P's belt and the drone's path across it from the picture's geometry only. **No OSD value was read or used.** Labels: *measured* (from the frames), *derived* (computed from measured values), *assumed*, *published*.

## Method
- **Frames:** f121–f400 (set B of [wind.md](wind.md) is f121–f330). Contact sheet every 15 frames, then gridded 960 × 540 stills and 3× crops at f150, f170, f265, f300, f330 and f360, with the OSD blanked. Each point was picked by eye to about ±5 px (±0.6°).
- **Angles:** the tracker's lens and attitude ([wind.md](wind.md) §1.2): k1 0.33, pinhole f = 1.005 half-widths, and each frame's roll and pitch from P's attitude log. Each picked pixel becomes an azimuth and an elevation in the level frame.
- **Scale-free geometry:** for a point on the ground, distance = z · cot(depression), where z is the camera's height above that ground. For a tree, (z − H)/z = tan(top's depression) / tan(base's depression). Everything below is in units of z or of the tall trees' height H, which needs no scale.
- **Metres** need one assumed number, the ground speed g: v/z is measured from how fast ground points approach, so z = g / (v/z).
- **Error sources:** pixel picking ±0.6°; the skyline sits up to 1° below the true horizon (wind.md §1.2); the focal length's −6 %/+15 %; flat ground assumed between the drone and each point; trunk bases partly hidden by scrub.

## What the belt is (measured)
It is not a narrow tree row. It is a **wide wooded strip**:
1. a grass margin at the field's edge;
2. scrub, bushes and small trees in loose clumps;
3. a dirt track along the far side;
4. **a line of tall trees about 1.7–1.8 z beyond the track** (two trees measured: 1.7 z and 1.8 z), with the open field beyond.

The tall trees stand out of the scrub. Some are dead and bare.

- **Orientation:**
  - The track and the tall-tree line run **perpendicular to the course within about 5°** (measured: the track projected on the ground at f300 and f330 stays within 0.3 z in range across 4.6 z of width).
  - The near edge (field to grass) is oblique, **28–33° from perpendicular**, with its right end farther (measured at f150 and f170).
- **Along-course width** from the field's edge to the track: **about 6.8 H**, and **about 8.5 H** to the tall-tree line (derived, ±20 %).

## Heights (scale-free)

| Quantity | Value | Label and method |
|---|---|---|
| Tall trees' height H, in z at f300–f330 (z_end) | **0.85–1.1 z_end** (best 1.0) | measured: tree tops at or just above the horizon at f265, f300 and f330 (el +1.4°, +0.9°, ≥ −1.7°); bases 7–12° below |
| A second, bare tree | 0.72 z (0.69–0.74) at f360 | measured: top 6.2° below the horizon, base 20.4–22.1° below |
| Approach rate v/z_end, f300–f330 | **1.0–1.05 per s** (±15 %) | measured: the track at the course's azimuth went from 2.61 z to 1.61 z in 1.0 s; a tall tree's range from 4.35 z to 3.3 z, with its sideways offset constant at 3.6–3.7 z |
| Approach rate at f150–f170 | 2.3 per s | measured: the field's edge went from 2.62 z to 1.09 z in 0.67 s |
| **Drone height over the ground, start of the strip (f150–f170)** | **0.46 z_end (0.35–0.55)**, i.e. **about 0.45 H** | derived: the ratio of the two approach rates, at the same speed |
| Drone height at f265 | 0.84 z_end | derived: a tall tree's range at f265 against f300 |
| **Drone height at f300–f330 (end of set B)** | **about 1.0 H** (0.9–1.2) | derived from the rows above |
| Drone height at f360, past set B | about 1.4 times the bare tree's height | measured |
| **Clearance over the tall crowns during set B** | **−0.55 H (below the tops) rising to 0 ± 0.15 H** | derived |
| Course over the ground | 4° ± 3° left of the nose | measured: a tall tree's sideways offset, corrected for the 2° heading change from the attitude log |

The drone **climbed through the strip**: from about 0.45 H over the field's edge to the tall crowns' height by f300, and clear of them by f360. A falling ground under the strip would give the same picture, and the footage cannot tell the two apart. Either way, the height over the local ground is as in the table.

## In metres (needs the ground speed)
- **Ground speed (assumed):** g ≈ 17 m/s (12–21). This is the 18.8 m/s cruise at P's pitch (p7c-wind 1.6) or K§8.2's 13 m/s (9–25), times 0.92–0.97 for the crab and the crosswind (derived).
- **z_end** = g / 1.0–1.05 ≈ **16–17 m (12–21)**, derived.
- **Tall trees H ≈ 15 m (11–21)**, derived. The bare tree is about 12 m.
- **Cross-check:**
  - **Published** species heights: black locust 12–30 m, pedunculate oak and ash up to 35–40 m, blackthorn up to about 5 m. Sources: <https://en.wikipedia.org/wiki/Robinia_pseudoacacia>, <https://en.wikipedia.org/wiki/Quercus_robur>, <https://en.wikipedia.org/wiki/Fraxinus_excelsior>, <https://en.wikipedia.org/wiki/Prunus_spinosa>.
  - **Assumed:** dry steppe grows trees below those maxima, so the strip's trees are about 12–20 m and its scrub 2–5 m.
  - H ≈ 15 m agrees. The scale-free ratios depend on none of this.
- **Drone height over set B:** about **7–8 m (5–10) at the strip's edge, rising to about 16 m (12–21)**, derived. That is far below K§8.5's 20–50 m for most of the stretch.

## Where the drone was relative to the belt during set B (in H, along the course)
- **f121:** 2.2 H before the near edge (derived).
- **Over the near edge:** about f184, at 0.45 H above the ground.
- **f330, the end of set B:** 5.1 H into the strip, 1.7 H before the track and 3.3 H before the tall-tree line.
- The track passes under the drone at about f378 and the tall-tree line at about f425. Both are past set B.
- Set B covers about 7.3 H of ground (7.0 s at 1.05 H per s, derived).

**Distance downwind: none.**
- K§8.2's lean puts the mean wind abeam from the left (derived there, not visible in the frames).
- The strip runs perpendicular to the course within 5°, so **the wind blew along the strip, not across it**. Its near edge is only 28–33° off that line.
- **During set B the drone was over the wooded strip itself, at 0.45–1.0 H, inside or at the top of its canopy layer.** It was never in a leeward wake 0–10 H downwind.
- If the wind had a head or tail part, the strip would take it obliquely. Even then, set B ends before the far edge, so no part of it is downwind of the strip.
- The 2.5× roll is the turbulence **within and just above a rough wooded strip**, the trees' own eddies (P's 2.5–11 m), at heights of about 0.5–1 H. It is not a belt's far wake.

## What K§8.5's test course should be
The belt stretch should be flown **through the canopy layer of a wide wooded strip, not over a belt's wake at 20–50 m**:
- **The strip (derived from P):**
  - about 7 H wide along the course from the field's edge to a track
  - scrub 2–5 m tall (assumed, published)
  - tall trees H ≈ 15 m (11–21) scattered through it and in a line 1.7 H beyond the track
  - the course perpendicular to its long axis, so the wind abeam runs along the strip
- **The 7 s:**
  - starts 2 H before the strip's near edge
  - ends 5 H into it, 1.7 H before the track
  - the height rises from about 0.45 H to 1.0 H over the ground
  - at P's 1.05 H per s, that is about 7.3 H of ground
- **If the harness keeps a single belt:** fly the belt stretch at **0.5–1.0 H** (about 7–16 m for a 15 m belt), not 1.7–4.2 H, inside the strip's turbulent layer. Make the obstacle a wide band of rough canopy, not a thin row whose wake the course meets only 5–15 H downwind.
- **The open-field stretch** was not measured here. Its height stays K§8.5's until it is.
- **For the physics engineer:** with the wind along the strip, what drives T3 is the canopy's own turbulence and its roughness change (the strip's internal boundary layer), not Perera's leeward wake. Whether the wake grid represents that is a model question, not a geometry one.

## Limits
- One crossing, one pilot. The climb and the falling ground cannot be told apart.
- The metres rest on the assumed ground speed. The ratios in H do not.
- Trunk bases hidden by scrub would make the trees' H/z up to about 10 % smaller.
- The wind's direction comes from the lean (K§8.2), not from the picture.
- Working images stay in `reference/_compare/` (local, never committed).
