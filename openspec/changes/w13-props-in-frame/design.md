# Design: w13-props-in-frame

## Context
The flight scene draws D01's props as built: static meshes at the generator's `prop.rest_deg` (rear right 178°, front right 13°, rear left 132°, front left 6°). The sim integrates each rotor's angle (`FlightState.RotorAngle`, blade 0 along the body's (cos θ, 0, s·sin θ), s = −1 for a clockwise rotor, as `PropStrikes` places it) and the feed already models the camera's exposure (`RollingShutter.ExposureS`) and its line-by-line read (`RollingShutter.RowReadUs`). Labels: measured / published / derived / assumed, as in drone.json.

## 1. Why a blade sat in the upper left of every frame
Geometry (drone.json, derived from it): the lens at body (0, 0.015, 0.115) m, the front hubs at (±0.160, 0.035, 0.160) m, the props 0.254 m across. So each front disc lies 20 mm above the lens and its hub 45 mm ahead and 160 mm to the side; the disc reaches 287 mm forward, 172 mm ahead of the lens, and to within 33 mm of the lens's axis sideways.
- **The parked blade** (derived, from the feed's own projection: f = 1.005 half-widths, k1 = 0.33, 16:9): front left's rest angle, 6°, points its blade along +x, straight across the front of the lens 45–58 mm ahead of it. Its outer third (r = 88–127 mm) lands in the picture from the left edge to 26 % of the width in, at 50–55 % of the half-height above the centre: the sharp grey blade QA saw, the same at any throttle because nothing turned it. Front right's 13° parks both its blades outside the picture (one beside the lens, the other outboard), so the right side showed nothing.
- **The share of a turn** (derived, the same projection, the blade's centre line from the hub to the tip, no near plane): one blade of a front prop is in the picture for **34 %** of a turn; with two blades, **69 %** of fields would show a blade of each front prop; only **10 %** of fields put it in the top half (the rest is the disc's far arc low at the side edges, 6–25° above the horizon line).
- **Through the camera's read** (measured on the captures, `Editor/Stills/PropFields`): the rows a front disc's visible arc covers take a few ms to read, and at 9100–9300 rpm a blade passes 5–6 times in each 18 ms field, so every field catches a blade in that band once or more: **24 of 24 fields** at noon and under overcast, on House and on Village, show a thin dark bar from each side edge, two or three a side, 18–40 % of the height down and up to a quarter of the width long, each slightly slanted, thickening into a wedge at the edge where the wide root crosses.
- **Against the reference note's counts** (§3.1): the C–H group, the pilot's quad class, shows a blade in almost no frame (D 2 frames, H none, P mostly a top-right line). The sim's geometry, a blade in every field at both side edges above the horizon, is the **B/G group** (right ≈ 20 of 24, left ≈ 16 of 24), and with two or three bars a side rather than B's one. So the camera's place against the discs, not only the stillness, is wrong for the sim's airframe. Moving the lens alone does not fix it at this frame: raising the discs by 60 mm still leaves 22 % of a turn in the picture; the lens has to sit at or ahead of z ≈ 0.20 m (85 mm forward of where it is, near the discs' front arcs) before no blade enters (derived). That is a model and camera question for the asset artist and the pilot's build, reported here and not changed (non-goal).

## 2. What is drawn
- **The pose** gains `RotorAngle` (render-side; `FlightCore.GetPose` copies the state's; `PoseFrame.Lerp` turns each rotor the short way across 2π). No step reads it; nothing logged moves.
- **The props** (`App/PropView`, `Shaders/PropDisc.shader`): the model's `_prop_<corner>` parts (every LOD) are hidden, and each rotor is drawn over a bounding cylinder at its hub by a shader that, per pixel, takes the camera's ray across the disc's plane and asks for what share of the exposure the sim's blade (drone.json's `props.blade`: chord, thickness and section angle at 40 stations; the hub) lies on that ray. That share is the pixel's alpha. The section is a rectangle (chord along the section angle, leading edge ahead and up, thickness across), so a blade seen nearly edge-on keeps its pitched height.
- **The time.** A CMOS sensor reads its rows in turn: row r at (25 + r + ½) × 64 µs into the field (published PAL timing, RollingShutter). In the FPV view each pixel takes the rotor's angle at its own row's read, the row found from its ray through the feed's lens, from the history's 1 ms steps (`PropView.Samples`, unwrapped from the field's reference instant). At 9000 rpm a rotor turns 3.5° in one line's 64 µs (derived), so a blade crossing 30 rows is drawn across 100° of its turn: the "sliver at a new angle and length" is what the read makes of a blade, not a drawn shape. The chase view, and any camera off the field latch, reads every row at the pose's instant with the speed held (`PropView.Draw`).
- **The smear**: each row's exposure window ends at its read, so the blade sweeps S = ω × T before it; a pixel the sweep crosses for part of the window is that part covered. No disc, texture or blur is added.
- **At rest**: the samples are all equal, S = 0, and the pixel test is the plain inside test: the blade drawn still, where the sim holds it.
- **The look of the blade**: the model's own prop material (the "prop" palette's one colour, copied by property block), the face toward the camera lit by the main light and the ambient probe. Seen from below, as the FPV camera sees the front discs, that face is the blade's underside: no sun on it, only the sky and ground bounce, so it reads dark.

## 3. Numbers
| Number | Value | Label |
|---|---|---|
| Lens, front hubs, prop diameter, blades | drone.json | assumed / derived / measured / measured (drone.json's own labels) |
| Blade chord, thickness, section angle (40 stations), hub 19 mm × 8 mm | drone.json `props.blade` | assumed (drone.json's labels: the sheet's chord law, the class's) |
| Row read, (25 + r + ½) × 64 µs; 287 rows | RollingShutter | published (PAL), read in step with the output assumed |
| Exposure T = K·N²/(L·S), K 12.5, f/2.0, ISO 100, clamped 1/100 000–1/50 s | RollingShutter | published / assumed / assumed / published |
| Scene luminance L | 0: unmeasured, so T takes sunny-16's 4096 cd/m² | derived; the AE loop (p4-feed 1.7) is not built |
| T now, noon and overcast alike | 122 µs | derived |
| Hover rotor speed | 9000 rpm ≈ 942 rad/s at the nominal loadout | derived (drone.json's k_T note) |
| Smear at hover, T = 122 µs | 942 × 1.22e-4 = 0.115 rad = **6.6°** | derived |
| A line's turn at hover | 942 × 64e-6 = 0.060 rad = **3.5°** | derived |
| Lens k1 0.33, f 1.005 half-widths, 16:9 | FeedSettings | measured / derived / published (FEED-02) |
| Bound's margin past the blade, 0.05 m | PropView.BoundMarginM | assumed: bounding only, nothing drawn depends on it |

**Noon against overcast.** The exposure the feed uses today does not follow the light: `SceneLuminance` is a stub at 0 for every sky, so T is 122 µs at noon and under overcast and the smear is the same 6.6° in both. A wider wedge in dull light follows by itself once the AE loop measures the scene (p4-feed 1.7): at overcast's roughly 1000 cd/m² T would be 0.5 ms and the smear 27° (derived), the note's 15–30° wedge. Nothing here is tuned to fake it.

## 4. What is approximated
- The blade is the sim's, straight and radial: the model's 12 mm tip sweep (D01's `prop.sweep_m`) is not drawn (5° at the tip).
- A section's shape is its chord × thickness rectangle (the sim's fill is under 1), and its station is read where the ray crosses the hub's plane: a ray grazing the disc meets the blade at a slightly different radius.
- The angle within one row's exposure is linear between the 1 ms samples.
- The feed's rolling-shutter track turns each row's read by the camera's own rotation; the props ride the camera, so that turn moves them with the scene (the pilot's 300 °/s over half a field is 2.7°): in a hover it is nil.
- No shadow on the blade, no fog.

## 5. Risks
- **The blade is drawn in every other field of a hover at this camera place** (§1): correct for the geometry, wrong for the airframe group. The fix is the camera's place or the frame's, a separate decision.
- **Aliasing with the field rate**: 9000 rpm is 300 blade passes a second, exactly 6 a field, so at a steady 9000 rpm the blade would look parked from field to field. That is what a 50 Hz camera sees at that speed. The captures' hover runs at 9080–9290 rpm, and each blade's angle moves 10–35° on (modulo the blade spacing) from one field to the next (measured): a slow drift at any one row, while the read's 5–6 passes a field put the bars at new rows every field. The note's "unrelated phase every field" assumed 290 passes a second, which is also a drift (36° a field back); the read is what makes the bars jump.

## 6. The captures (local only: `reference/_compare/w13-props/`, never committed)
`-executeMethod Fpv450.PropFields.Run -out <folder>`: 24 consecutive fields of the sim's hover (1.66 m up, 9080–9290 rpm) through the feed, at noon and under overcast on House and on Village, and a chase frame of each. The sheet sets 12 of each case beside 12 consecutive frames of letters A, B, G, P, D and H.
- **The look of a blade** is close to B's: a thin, dark, sharp bar in from a side edge, slightly slanted, a new set of rows every field. The sim's bars are straighter than B's (B's bends a little), and where the blade's wide root reaches the picture's edge the sim's bar thickens into a short wedge.
- **How often** is wrong for the airframe group: the sim shows two or three bars a side in every field; B shows about one a side in most frames, A a top-corner sliver in about half, P a top-right line, and D and H (the sim's group) almost none. That is the camera's place (§1), not the drawing.
- **Noon and overcast** are the same smear, 6.7–6.8° (122 µs at 9200 rpm), as §3 says; the overcast range is lit flatter and the bars stay as dark.
- **Village's** light is the friend's whatever the setup (D-088), so its noon and overcast cases are the same picture.
- **The chase view** shows each prop as a thin dark blade at its own angle in the arm plane.
- **Cost**: four bounds and one fragment program over the pixels they cover; not measured (the machine is shared and loaded, so no timing counts): QA's PLT-07 run takes it.
