# Object sheet: hunter track kit

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist.

**OPSEC:** clips are cited by letter only. The other game's art and marks are not a target: every kind below is a generic shape with no mark on it.

**Seen in:** X and Y, the pilot flying this track. The layout is the neutral `maps/hunter-09/layout.json`.

## How to read the numbers

Every number carries one label:

| Label | Meaning |
|---|---|
| **[F]** | measured from the pilot's track file: poses, stacking steps, spacing, or a size stated in the kind's id |
| **[C]** | measured on sampled frames of X or Y |
| **[D]** | derived from [F] or [C] by the stated reasoning |
| **[A]** | assumed: the file is silent and the clips do not resolve it. One params row to change |

Clip measurements use a pinhole camera fitted on the still from the spawn point, where the camera's position is known from the file: focal length 855 px at 1920 px width [C], about 97° horizontal. Check: the tops of two 3 m pylons measure 22.79 and 22.99 m against 22.94 m in the file. Expect about ±0.25 m at 10–30 m range.

The ground is **19.99 m** at the track [F]: tape points, blocks and other items resting on it share that height.

## The 24 drawn kinds

Sizes are in metres, as length × width × height unless the cell says otherwise. "Rows" counts the file's rows, with the distinct objects in brackets where rows are doubled.

| Kind | Rows | Outer size | Origin | Main material | Physics role |
|---|---|---|---|---|---|
| `concrete_block` | 204 (199) | 1.524 × 0.762 × 0.762 [F] | centre of the bottom face [F] | `concrete` | solid box, landable top, solid-wall wake |
| `pylon_3m` | 15 (14) | tapered flat sail, 3.00 high [F], 0.55 wide at the base, 0.20 at the tip [C], 0.06 thick [A] | centre of the base [F] | `fabric` | thin box along the sail |
| `cone_light` | 12 | cone, 2.70 high, base Ø0.70 [C], dark base band 0.30 high [C] | centre of the base [F] | `plastic` | cone or capsule |
| `tyre_red` | 7 | Ø1.20 [C] × 0.42 high [D], hole Ø0.70 [C] | centre of the bottom face [D] | `rubber` | ring; the hole is a real gap |
| `tyre_plain` | 1 | as `tyre_red` [A] | as `tyre_red` [A] | `rubber` | as `tyre_red` |
| `tyre_blue` | 1 | as `tyre_red` [A] | as `tyre_red` [A] | `rubber` | as `tyre_red` |
| `tyre_yellow` | 1 | as `tyre_red` [C] | as `tyre_red` [D] | `rubber` | as `tyre_red` |
| `ribbon_tape` | 5 | flat band 0.15 wide [C], 0.002 thick [A], 34–115 long per ribbon [F]; thin stake at each corner, Ø0.02 [A] × 0.45 high [C] | row at zero, points in the file [F] | `fabric` | snag flag, no hard collision on the band; stakes are thin |
| `frame_8x10` | 5 | arch, 3.05 wide × 2.44 high [D], 0.10 deep [A] | foot, centred between the posts [D] | `plastic` posts, `fabric` banner | **gate**, see below |
| `frame_square_a` | 2 | head 3.05 × 3.05 [D] on two legs 2.30 long [C]; 5.35 high in all [D] | foot of the legs, centred [D] | `plastic`, `fabric`, `steel` legs | **gate**, see below |
| `frame_square_b` | 2 | the same head [C] on legs 4.9 ± 0.5 long [C]; 7.95 high in all [D] | foot of the legs, centred [D] | `plastic`, `fabric`, `steel` legs | **gate**, see below |
| `light_gate` | 1 | ring 3.60 wide × 2.80 high × 0.45 deep [C], rim 0.30 [C] | centre of the base [F] | `plastic` | **gate**, see below |
| `steel_profile` | 4 | perforated bar 1.25 long [D], section 0.09 × 0.04 [C] | centre of the bar [A]; the long axis is local Y [F] | `steel` | thin box; the four form a grille at 0.30 pitch [F] |
| `led_flag_green` | 3 | light bar 1.20 × 0.06 × 0.03 [A] | centre [A] | `plastic` | thin box, snag flag |
| `led_flag` | 1 | as `led_flag_green` [A] | centre [A] | `plastic` | thin box, snag flag |
| `light_rig` | 3 | 0.50 × 0.10 × 0.10 [A] | centre [A] | `steel` | small box |
| `arrow_red` | 2 | plate 1.00 × 0.50 × 0.02: the 2:1 ratio [C], the scale [A] | centre [A] | `plastic` | thin box |
| `board_large` | 2 | panel 2.20 long [F, the id's number read as cm] × 1.10 wide [A] × 0.02 [A] | centre of a short edge [A] | `timber` | thin box |
| `board_small` | 1 | panel 1.00 long [F, as above] × 0.50 wide [A] × 0.02 [A] | centre of a short edge [A] | `timber` | thin box |
| `track_light` | 2 | bar 1.00 × 0.05 × 0.05 [A] | centre [A] | `plastic` | thin box |
| `led_ball` | 1 | sphere Ø0.30 [A] | centre [A] | `plastic` | sphere |
| `light_curve` | 1 | thin arc, tube Ø0.06 [A], about 3.5 long [C] | centre of the arc [A] | `plastic` | thin capsules along the arc, snag flag |
| `tent` | 1 | 3.75 × 3.75 [D] × 3.10 high [C] | centre of the footprint, on the ground [F] | `fabric` roof, `steel` legs | thin roof and four legs; open underneath |
| `notice_board` | 1 | 1.20 × 0.10 × 2.00 [A] | foot, centred [F] | `timber` | box |

How the less obvious sizes were reached:

- **Tyre height 0.42 [D].** The race's disc volumes sit 0.215 and 0.207 m above the two tyres they mark [F], which puts mid-height at 0.21. One red tyre rests exactly on a block's top face (21.514 m) [F], so the origin is at the bottom. Diameter and hole come from the tyre's proportions in X at 230 s [C].
- **Tent 3.75 [D].** Four red tyres weigh down its corners, 3.74–3.79 m apart, and their centre is within 0.03 m of the tent's origin [F].
- **Steel bar 1.25 [D].** The 0.30 m pitch [F] scales the doorway they cross to 1.22 m in X at 58 s [C].
- **The frame ids' numbers are feet [D].** 8 × 10 and 10 × 10 read as feet give 2.44 and 3.05 m. Checks: the arch's width-to-height ratio is 1.16–1.23 in frontal views [C] against 1.25; one arch stands 2.41 m tall in the spawn still [C]; a square head's apparent width at known range matches 3.05 m within 5 % [C].

## The 10 gates

| Kind | Opening, width × height | Sill above the origin | Members |
|---|---|---|---|
| `frame_8x10` | **2.07 × 1.76** [C] | 0, open to the ground [C] | two posts 0.49 wide [C], top banner 0.68 high [C] |
| `frame_square_a` | **2.07 × 1.78** [C] | 2.93 [D] | posts 0.49 wide [C], top and bottom banners 0.63 high [C], legs Ø0.05 [A] at ±1.44 [C] |
| `frame_square_b` | **2.07 × 1.78** [C] | 5.53 [D] | the same head on longer legs; the sill is ±0.5 |
| `light_gate` | **3.00 × 2.20** [F, the id's numbers in cm] | 0.30 [C] | rounded ring; the inner height measures 2.21 [C] |

All four openings pass the drone (0.574 m nose-first, 0.706 m diagonal).

Each gate on a flat 19.99 m ground, from the file's height [F] and the sizes above:

| Id | Kind | Origin y | Opening spans | Clear above the ground | Buried |
|---|---|---|---|---|---|
| 8 | `light_gate` | 19.945 | 20.24–22.44 | 2.20 | 0 |
| 329 | `frame_8x10` | 19.949 | 19.95–21.71 | 1.72 | 0.04 |
| 331 | `frame_8x10` | 19.933 | 19.93–21.69 | 1.70 | 0.06 |
| 330 | `frame_square_a` | 19.922 | 22.85–24.63 | 1.78 | 0 |
| 335 | `frame_square_a` | 18.006 | 20.94–22.72 | 1.78 | 0 |
| 333 | `frame_square_b` | 15.735 | 21.26–23.04 | 1.78 | 0 |
| 337 | `frame_square_b` | 18.030 | 23.56–25.34 | 1.78 | 0 |
| **332** | `frame_8x10` | 19.207 | 19.21–20.97 | 0.98 | **0.78** |
| **334** | `frame_8x10` | 18.925 | 18.93–20.69 | 0.70 | **1.06** |
| **336** | `frame_8x10` | 18.037 | 18.04–19.80 | **none** | **1.95** |

## Concrete block

- **Size: 1.524 × 0.762 × 0.762 m [F]**, which is 5 × 2.5 × 2.5 ft.
- **Stacking step: 0.762 m.** Mean 0.7616, median 0.762, SD 0.003, n = 59 [F]. The design's "about 0.76" is **confirmed**.
- **Neighbour spacing: 0.762 m across and 1.524 m along.** Across: mean 0.7622, n = 73. Along: mean 1.5239, n = 95 [F]. The design's "0.8 / 1.5" is **corrected** to these values.
- **Origin: centre of the bottom face [F].** Blocks laid on their side have their centre at mid-course: one at y 25.70 spans 25.32–26.08, exactly the upright course at 25.324. A side-laid block at x 561.53 also fills the column centred at x 561.909.
- **Courses:** the base course stands at 19.99 m [F]. Many blocks of the second course overhang with nothing directly below them, on purpose: the tower's wide second course shows this in X at 170–172 s [C]. Keep the poses.
- **Shape:** a box with softened edges and a 2 × 4 grid of square pockets on the top and bottom faces [C]. Pocket 0.16 square, 0.04 deep [A]; these are portion 2 detail.

## The 11 low objects

**Verdict: eight need no ground change, and three arches need a dip** to meet the spec's "whole opening above the ground". A fixed origin offset cannot explain the arches or `frame_square_a`, because each of those kinds also stands at ground level in the same file. Both `frame_square_b` stand low, so the clips cannot tell long legs from an origin about 2.6 m below a shorter leg's foot [D]. The build is the same either way: the head's lower edge sits 4.9 m above the origin.

| Id | Kind | y [F] | What holds | Evidence | Ground action |
|---|---|---|---|---|---|
| 47 | checkpoint volume | 18.54 | data only, never drawn | not one of the 24 kinds | none |
| 52 | checkpoint volume | 18.49 | data only, never drawn | as above | none |
| 333 | `frame_square_b` | 15.735 | long legs run into the ground; the head is well clear | spawn still in X: head 20.97–23.56, leg meets the ground at 19.25 [C] | none |
| 337 | `frame_square_b` | 18.030 | as 333 | X at 227–238 s: legs about 2.9 visible below the head [C] | none |
| 335 | `frame_square_a` | 18.006 | legs run into the ground, opening clear | **not seen in the clips.** Estimate from 330's proportions [D] | none |
| 417 | `board_large` | 18.893 | stands through the floor of a house the file does not hold | boards inside that house, X at 62 s [C]; which board is which is not settled | none |
| 385 | `arrow_red` | 19.642 | below the floor of the same house | its twin at 21.36 m is on the inner wall, X at 306–314 s [C] | none |
| 401 | `board_small` | 19.349 | leaning, its lower edge in the ground | beside the block pile, X at 202 s [C] | none |
| **332** | `frame_8x10` | 19.207 | **stands in a real shallow dip** | spawn still in X: foot at 19.39, top at 21.80, so the full 2.41 m shows [C]. Sunk in flat ground, only 1.66 m would show | **dip** |
| **334** | `frame_8x10` | 18.925 | **not settled**: hidden behind a tree in the only view | labelled estimate that keeps the opening flyable [D] | **dip** |
| **336** | `frame_8x10` | 18.037 | **sunk in the source**: only the banner and a stub show, about 1.0 m, on ground near 19.44 | X at 226–238 s, identified by bearing and by its foreshortened width, 1.48 m measured [C] against 1.40 expected [D] | **dip, a labelled estimate that departs from the clip** |

Dips for the world builder [D]. Each is circular around the arch's origin: a flat floor out to the inner radius, then a smooth blend up to 19.99 m at the outer radius, with a mean slope of 25° [A]. No other object or tape point lies inside any of them [F].

| Id | Centre x, z | Floor height | Depth | Inner radius | Outer radius |
|---|---|---|---|---|---|
| 332 | 416.06, 458.33 | 19.19 | 0.80 | 2.2 | 3.9 |
| 334 | 473.50, 458.11 | 18.91 | 1.08 | 2.2 | 4.5 |
| 336 | 496.18, 487.82 | 18.02 | 1.97 | 2.2 | 6.4 |

Arch 336's opening tops out at 19.80 m, below the field around it, so it can only be flown by diving into the dip. None of the three arches has a checkpoint volume within 9 m [F].

The field off the track also reads 0.55–0.75 m lower than the track at three places: 19.39 at 332, 19.25 at 333 and about 19.44 at 336 [C]. That is more than the ±0.25 m error, though the 336 value rests on an estimated camera position and the 333 value may be a near rim hiding the leg's foot. It is recorded here and changes nothing above.

## What the file does not hold

**The race flies through three houses that are not in the file.** They belong to the other game's environment, along with the fences and big trees. X and Y fly in through their windows and doors, across the rooms and out again, at 50–86 s, 122–150 s and 250–362 s in X.

- **23 of the 36 flexible checkpoint volumes mark these houses' openings** [F].
- **19 drawn objects are fixed to or inside the northern house:** the 4 steel bars, 4 LED flags, 3 light rigs, 2 arrows, 2 large boards, 2 track lights, the ball and the curve [F]. Without the house they hang in the open.

Where the houses stand, from the volumes that mark them [D]. These are the marked openings, not the walls:

| House | Volumes | Marked extent x | Marked extent z | Note |
|---|---|---|---|---|
| North | 47–52, 386, 388, 394, 396, 416 | 493.6–505.8 | 538.0–544.0 | yaw about 4° [F] |
| North-east | 39–43, 46 | 512.2–520.7 | 527.3–536.8 | yaw about 327° [F] |
| South | 27–32 | 516.3–520.0 | 431.6–443.3 | yaw about 2° [F] |

## Open points

- **The houses.** No task builds them, and the route cannot be flown as in X and Y without them. This needs a decision in the plan.
- **Arch 336.** The spec's whole-opening rule and the clip disagree. The dip above follows the spec.
- **Arch 334 and frame 335** are not visible in any sampled frame. Their rows are estimates.
- **`frame_square_b`'s leg length** is 4.9 ± 0.5 m: two views gave 4.3–5.3 m. Its feet are underground in both placements, so only the head's height matters. Even at 4.3 m both openings stay above the ground.
- **Large boards:** which axis is the long one and where the origin sits are not settled. Both stand inside the house.
- **Kinds never resolved in the clips:** LED flags, light rigs, track lights, the ball and the notice board keep assumed sizes.
