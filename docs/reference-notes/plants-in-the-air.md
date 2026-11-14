# Plants in the air: clip H and the published numbers (w16-plants-in-the-air 1.1)

The reference for the plants that move in the sim's own air (1.2's law, 1.3's drawn plants, 1.4's cut pieces and dust, 1.5's drag on the drone). Clip H's ending comes first, as the pilot asked; then the classes the farmland draws, and per class the published bending, damping, drag and flutter numbers; then the rotors' wash on crops, the dust it lifts and the forces of vegetation on a body pushed through it.

- **The pilot's words** (the change's proposal): "they are flexible, so they should flexibly get back to their default position if a gust is short etc., again - calculate and make it realistic"; "about grass cutting, swaying because of propwash, dusts whiffs as you land closer to the ground - refer to [clip H] - towards the end you will see short examples".
- **Labels:** *measured* (from the frames, or printed by a tool named in §1.6), *published* (a cited source), *derived* (computed here, the rule shown), *assumed*. Each published row says what was read: **full**, **abstract**, **record** (a catalogue page) or **restated** (a later work citing the original, named).
- **OPSEC:** clips by letter and by time inside the clip only. No value was read from the on-screen display; no reference file name is written.
- **Companions:** [stem-properties.md](stem-properties.md) (the stems' published section, modulus, cutting and drag numbers: not repeated here), [vegetation-on-the-props.md](vegetation-on-the-props.md) (every stretch of H, D, A and F near the lens, and the props in view), [ground-micro-motion.md](ground-micro-motion.md) (the camera's motion near the ground), [terrain.md](terrain.md) (the reference set and the surface table), and the rotors' air in `openspec/changes/p7d-tether/design.md` decisions 19 and 20.

## 1. Clip H's ending

### 1.1 The view (REAL-02)

- **Scene:** a creep along the edge of a tree belt over dead vegetation: lodged dry grass and straw lying in mats, standing clumps of pale dead grass stalks up to about 1 m, a few green weed stalks, and patches of dark bare soil between the mats. The belt's trees stand behind with their leaves on.
- **Viewpoint:** the camera 0.3–0.6 m above the mats (assumed, ±50 %, as ground-micro-motion.md §1), level, 0° uptilt; the drone creeping forward and yawing.
- **Light and weather:** overcast, flat light, no shadows; dry vegetation, while the bare soil is dark and looks damp (assumed from its colour).
- **Its end:** the last live frame is at 20.91 s. The picture is lost at 20.94 s, and a blue screen fills the clip's last 3.6 s (to 24.5 s).
- **The drone's motion** (derived from `motion.csv`'s forward term, forward speed over depth per frame, times 30 frames a second and an assumed 2 m mean depth of the picture): 0.1–0.5 m/s over 16–20.5 s. In the last 0.4 s (f617–628) the term is 0.029 a frame, and the camera pitches 30° down and yaws 22° (measured, the frames' turns summed): the drone noses into the straw mat.

### 1.2 What the frames show

Read at 30 frames a second over 8.0–20.94 s: contact sheets of every frame and every third frame, and native-resolution crops of the moments below (§1.6). Sizes are at the recorder's 1920 px width; the feed itself resolves about 450 px across, so one feed pixel is about 4.3 px here. Distances to things in view are assumed, from their blur and size, and the speeds that rest on them are derived with the camera's 965 px focal length at 1920 (wind.md §1).

| Time (s) | What moves | Read |
|---|---|---|
| 8.55–8.85 | **A pale grass tuft at the lens.** A soft-edged (out of focus, so a few cm from the lens) bunch of pale stalks rises into the bottom centre, about 13 % of the width, drifts right and leaves the bottom-right edge in 9 frames. Whole. | about 700 px in 0.3 s; at 5 cm (assumed) about 0.1 m/s: a stalk brushed under the camera as the drone creeps, not a flung piece (derived) |
| 10.36–10.72 | **A long loose straw.** One straw, about 5 px thick (0.7 m away, vegetation-on-the-props.md §1), sweeps in from the right edge across the lower third to the bottom left and back out, in 10 frames. It bends along its length as it sweeps, like a whip, and stays whole. | lifted by the outwash or dragged by the airframe; the frames cannot tell which |
| 17.23–17.70 | **A standing clump of dead stalks sways.** The clump ahead-left (about 0.5–1 m, assumed) has pale stalks whose bow changes between consecutive frames: a stalk straight in one frame is bowed in the next and straight again two frames later, around a lean that holds. Nothing breaks. | a flutter at the frame rate's limit (33–67 ms per change, so 7–15 Hz or above, aliased: measured by eye), on a slow lean |
| 17.30–17.40 | **A dry leaf lifts.** A curled brown leaf rises beside the left airframe rod and leaves the left edge within 3 frames, moving left and slightly up. | about 60 px a frame; at 0.2–0.4 m (assumed) 0.4–0.7 m/s across the view (derived); the camera's own yaw then is about 4 px a frame (measured), so the motion is the leaf's |
| 17.6–19.6 | **A green weed stalk at the right** (0.3–0.6 m, assumed) bends and unbends over a few frames, its S-shape changing; a small yellow leaf lying on the mat to its right flickers in place. | irregular, no clean period readable at 30 frames a second (measured by eye) |
| 18.47–18.84 | **A dead leaf thrown forward.** A brown leaf comes up from under the lens at the bottom edge, tumbling (its outline changes every frame: face-on, then edge-on), and flies forward and up away from the camera: its image shrinks from about 150 to 40 px in 4 frames, then it is lost among the stalk's leaves. | with a 3–6 cm leaf (assumed) it goes from about 0.2–0.4 m to 0.7–1.5 m in 0.13 s: **about 3–8 m/s** (derived), the speed of the front rotors' outwash near the ground (§4) |
| 9.0–13.8 | **No dust over the bare soil.** The drone creeps over dark soil patches at its creep height; nothing rises from them. Lighter grey bands on the soil move with the ground, not with the air: a drier crust or tyre marks. | no dust (measured by eye, every second frame) |
| 20.0–20.94 | **Into the straw.** The drone pitches down and noses into the mat (vegetation-on-the-props.md §1). Standing dry stalks at the left edge, a few cm from the lens, whip across the view; from 20.54 s single straws pass between the lens and the right rod. **The picture turns pale and soft:** over the centre (400–1500 × 300–640 px) the darkest 2 % of the luma rise from 23–25 to 31–53 of 255, the fine detail (the mean of the luma less its 5 × 5 blur) falls from 9.3 to 2.1–4.8, and the saturation from 84 to 53–64. | measured (§1.6); a puff of dust and chaff from the mat, motion blur and stalks at the lens all lower contrast this way, and the view also turns from the belt to the mat, so the stills cannot separate them |

### 1.3 The three effects the pilot named

- **Grass swaying in the wash.** Seen within about 1 m of the drone: standing dead stalks flutter frame to frame around a lean that holds while the drone is near, and a green weed bends and unbends. The lodged mats stay down; loose pieces on them move (straws, leaves). Beyond about 1.5–2 m nothing is seen to move between frames apart from the camera's own motion (by eye; the feed's resolution limits this).
- **Pieces in the air.** No piece is seen leaving a disc: the discs are out of view in this airframe group (vegetation-on-the-props.md §3). What the feed shows are **whole dry leaves and loose straws thrown up and forward from under and around the drone, tumbling, at up to several metres a second, each in view for 4–10 frames**, and straws at the lens in the last 0.4 s. A piece cut and flung at a blade's speed (tens of m/s) would cross the view within one field and show as one speck or streak (vegetation-on-the-props.md §1). So what reads as "cutting" in this feed is most likely these thrown pieces: whether a blade cut them first cannot be told.
- **Dust whiffs as the drone comes lower.** H's dark soil gives none at the creep height. The only candidate in H is the pale, soft last 0.4 s as the drone noses into the mat. **Clip F is the dust reference** (§1.4).

### 1.4 The other clips, briefly

- **F (0–8.4 s), the dust:** dry season, full sun, the drone at about 0.5–1 m (ground-micro-motion.md) among bushes over bare dry loam. The first wisps rise at mid-distance to the right at 0.7–1.0 s; by 1.4 s an ochre sheet the soil's colour covers the lower third of the view, dense at the ground and thinning upward; by 2–3 s it reaches above the horizon line (the camera's height) and veils the bushes behind (2–5 m, assumed). It is a continuous veil with drifting streaks, not separate puffs, and it lasts as long as the drone stays low (measured by eye, 3 stills a second).
- **A (11.2–14.1 s):** a fast shallow descent over a green-and-dry meadow. The grass streaks with the forward motion; no flattened patch shows ahead of the drone: a moving drone's wash lands behind the camera's view.
- **D (5.0–6.9 s):** leafy weed tops pushed aside by the body at about 1 m, whole (vegetation-on-the-props.md §1).
- **P (4–11 s):** high over stubble fields and scrub in severe wind. The crowns are a few feed pixels; their sway is not resolved.

### 1.5 Targets (REAL-03)

| Target | From | For |
|---|---|---|
| Standing stalks within about 1 m flutter frame to frame around a held lean; mats stay down | H 17.2–19.6 s | 1.3: the wash's ring at a creep height of 0.3–0.6 m |
| Loose dry leaves and straws thrown up and forward at about 1–8 m/s, tumbling, 4–10 frames in view, whole | H 17.30, 18.47–18.84, 10.36–10.72 s | 1.4: pieces from the wash, beside the cut pieces |
| No dust from damp dark soil at the creep height | H 9.0–13.8 s | 1.4: the wet case (spec: "a landing on wet or covered ground") |
| A continuous ochre veil from dry bare loam at 0.5–1 m, first wisps within 1 s, over the camera's height in 2–3 s | F 0–3 s | 1.4: the dry case |
| The view softening and paling as the drone noses into a straw mat | H 20.0–20.94 s | 1.4's side by side; low confidence |

### 1.6 Reproduce

The clip's path is passed on the command line and never written; every output lands in the git-ignored `reference/_frames/<letter>/`. Run from the main checkout, which holds `reference/` (`--root`).

```
python tools/reference/extract_frames.py <clip H under reference/> --letter H --fps 2 --out reference/_frames/H/overview
python tools/reference/extract_frames.py <clip H under reference/> --letter H --fps 30 --start 8 --duration 8.05 --out reference/_frames/H/mid
python tools/reference/extract_frames.py <clip H under reference/> --letter H --fps 30 --start 16 --out reference/_frames/H/end
python tools/reference/contact_sheet.py --letter H --frames reference/_frames/H/end --first 1 --count 24   # and --first 25, 49, 73, 97, 121
python tools/reference/extract_frames.py <clip F under reference/> --letter F --fps 3 --duration 8.4 --out reference/_frames/F/w16
```

ffmpeg decoded on the CPU (no hardware acceleration). The crops, the tone statistics of 20.0–20.94 s (luma percentiles, saturation, fine detail) and the forward term's segment medians from H's `motion.csv` (ground-micro-motion.md §6) used throwaway scripts kept outside the repo; their outputs are local only.
