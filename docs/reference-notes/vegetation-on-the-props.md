# Vegetation meeting the props, as the footage shows it

The reference for w9-village-physics 2.2 (stems cut by the props, the shake) and 2.5 (cut bits drawn in front of the camera). It reads every stretch where the drone is in or just over grass, straw or weeds, and every clip for the props in frame.

- **The pilot's words:** "in real life if i land on a high grass with straw - straws are noticeable cut in front of my quads camera and my quad is shaking".
- **Labels:** *measured* (from the frames or printed by the tools in §6), *derived* (from measured numbers by a stated rule), *assumed*.
- **OPSEC:** clips by letter and by time inside the clip only. No value was read from the on-screen display. Frames are 1-based at each clip's own rate (29.84–30.02 fps).
- **Companions:** [ground-micro-motion.md](ground-micro-motion.md) (the camera's motion near the ground by phase; its numbers are reused here, not re-measured), [stem-properties.md](stem-properties.md) (the published stem data), [video-feed.md](video-feed.md) §1.4 and [wind.md](wind.md) R8 (the first sightings of blades in view).

## 0. The answer

1. **No clip shows the props cutting anything, and no clip shows a landing in tall grass or straw.** Every stem that comes near the lens is whole and is brushed aside or swept through the picture. No cut piece, chaff or spray is seen in any frame. The pilot's account is the only reference for the cut bits (2.5).
2. **The nearest thing to it is H's last second (20.0–20.94 s).** The drone tips down and turns into a lodged straw mat at about 0.3–0.6 m (assumed). From 20.54 s the straw is at the lens, the picture softens with motion blur, and straws pass in front of the airframe's right rod. The picture is lost at 20.94 s. Whether that is a touchdown, a crash or a link loss cannot be told.
3. **The shake there:** the camera turns at up to 30–120 °/s in pitch and 35–100 °/s in yaw, frame to frame (measured, low confidence: the fit fails in 3 of the last 5 frames). Its high-frequency residue rises 3–10× over the 2 s before (0.21 / 0.35 / 0.45° against 0.07 / 0.06 / 0.04°, roll / pitch / yaw). D's push into weeds remains the cleanest contact jolt: 0.39° and 21 °/s of pitch at 4–15 Hz, 10–30× the hover's (ground-micro-motion.md §4.1).
4. **The props do show,** but only in some airframe groups, and never as a sharp static blade:
   - **A:** thin dark blade slivers in both top corners, at a new angle in every frame, in about half the frames per corner; now and then a wider, translucent grey wedge.
   - **B and G:** dark blue-grey blades at the left and right edges, a quarter to half-way down, in most frames, again at a new angle each frame; sometimes a blurred wedge or a lens-shaped dark smudge.
   - **P:** a thin dark line high in the top-right corner in most frames, the top-left in some; a translucent grey wedge at the left edge in about one frame in six.
   - **C, D, E, F, H:** none in view at this scale, except the two dark slivers at D 4.6 s (video-feed.md §1.4). **S:** none.
   - So the sim's sharp, static blade in the upper-left corner is wrong. A blade in view must be redrawn at a new phase every field, mostly as a thin dark sliver, sometimes a soft translucent wedge (§3).
5. **The pale rods at mid-height left and right** (C–H, L, P; faint in B and G) are not props, and not on-screen display either: they are airframe parts in front of the lens (§3.3).
6. **The question for the pilot** is in §5.

## 1. The stretches

Heights are scene impressions (±50 %, assumed) as in ground-micro-motion.md §1. *Motion* is the camera's turn from `motion_bands.py` (rotation fit; *real* = the band less the fit's noise and parallax, §2.4 of that note): angle RMS below 1 / 1–4 / 4–15 Hz, and *h*, the per-frame residue h = x[i] − (x[i−1] + x[i+1])/2 that needs no spectrum.

| Letter, time (frames) | Scene, height | What the frame shows near the lens | Motion (measured) |
|---|---|---|---|
| **A** 13.3–14.1 s (f400–423) | the last of the descent to a green-and-dry meadow, about 1–3 m up (assumed) | Nothing: the grass stays below, sharp, no stem rises to the lens. Blades cross both top corners (§3). Picture lost at 14.12 s (f424). | Rotation fit poor over grass (14 of 33 frames usable): rate sd 3.2 / 8.8 / 1.4 °/s, the same as the 5 s before (3.1 / 5.3 / 1.6). No jolt before the loss. |
| **D** 5.0–6.9 s (f151–207) | a push forward through 1–1.5 m weeds at about 1 m, then a dive onto a stone pile | Leafy weed tops and stalks fill the lower half and pass the lens at about 0.2–0.5 m (assumed). They stay sharp and are pushed aside; no stem breaks or flies. A red flare ghost (video-feed.md O3) sits mid-frame from 5.7 s. Picture lost at 6.89 s on the stones. | ground-micro-motion.md §4.1: rate RMS 31 / 15 / 13 °/s against 2.9 / 4.5 / 3.0 in the hover before; **4–15 Hz real 0.16–0.22 / 0.39 / 0.19°**, rate 8–11 / 21 / 10 °/s. |
| **F** 7.0–8.4 s (f211–252) | a steep descent into a round pit inside a tree belt | No grass: bare dust, the rotors' dust cloud and fallen branches; the branches come up to the lens unbroken. Not vegetation on the props. | ground-micro-motion.md §4.1: 33 / 11 / 25 °/s RMS in the last 1.2 s. |
| **H** 0–7.6 s (f2–228) | the creep along a belt edge over lodged dry grass and straw, 0.3–0.6 m up | Lodged straw below the lower edge, sharp; nothing crosses the lens. | f170–228: real 0.82 / 0.52 / 0.031–0.035° (roll), h 0.061 / 0.042 / 0.032°. |
| **H** 7.6–9.6 s (f229–290) | a tall branched dry weed stalk at the right, about 0.5–1 m (assumed) | The stalk enters at the right, bends and sways leftward over about 1 s, and leaves whole. **8.57–8.74 s (f258–263):** a pale grass tuft or seed head rises into the bottom centre, right at the lens: soft-edged, about 250 px of the 1920 (13 % of the width), moving right, then out the bottom. 6 frames. Whole, not cut. | 4–15 Hz real roll 0.070–0.089° (from 0.027–0.035 before, ×2.5), pitch 0.080–0.085° (from 0.042–0.053, ×1.7); rate sd 16.0 / 8.4 / 8.9 °/s (from 9.9 / 8.6 / 3.0). |
| **H** 10.4–10.7 s (f313–321) | the creep, over a bare patch | One long straw, whole, sweeps through the lower third from the right edge to the bottom-left and back. Sharp, about 5 px thick at 1920 (about 1 px of the feed), 8 frames (0.27 s). Lifted by the downwash or dragged by the airframe (cannot be told). | inside the 7 s creep f330–539: real 1.52–1.55 / 0.54 / 0.051–0.078° (roll). |
| **H** 18.0–19.9 s (f540–599) | toward a standing dry grass clump at the left | The clump's pale stalks loom at the left edge, sharp, moving with the scene: not caught on the drone. | real 1–4 Hz 0.26 / 0.43 / 0.37°, 4–15 Hz 0.054–0.079 / 0.044–0.067 / 0.050°; h 0.072 / 0.063 / 0.043°. |
| **H** 20.0–20.94 s (f600–628) | **tips down and turns into the straw mat** | 20.0–20.5 s: the view tilts down onto the lodged straw. From 20.54 s (f617) the straw fills the lower half at a few cm, the picture softens (motion blur, diagonal smear), and from 20.74 s (f623) single straws pass between the lens and the right rod. f628 tears at its bottom rows; the picture is lost at 20.94 s (f629). No cut piece, no spray, no flying chaff in any of the 29 frames. | Per frame: pitch −1.0 to −4.1°, yaw −1.2 to −3.4° a frame from f617 (**30–120 °/s pitch, 35–100 °/s yaw**); forward-over-depth rises ×5–10 (0.003–0.008 → 0.02–0.05 a frame: closing fast). h **0.21 / 0.35 / 0.45°**, 3–10× the 2 s before. The fit fails at f624, f626, f627 and its inlier share drops to 0.6–0.85: low confidence. |
| **S** whole clip | low cruise at 6–9 m | Nothing near the lens. | ground-micro-motion.md §5. |

**What the frames say about cutting (measured, by eye on every frame of D's push and H's creep and end, every second frame of all of H):**
- Every stem near the lens is **whole**: the weed tops in D, the stalk, the tuft and the long straw in H. They bend, sway and leave.
- **Nothing is cut in frame, and no cut bit is ever seen**, including in H's last 0.4 s in the straw.
- **Why the feed may hide them** (derived): the feed resolves about 450 px across, so one feed pixel is about 4.3 px at 1920. With the focal length of 965 px at 1920 (wind.md §1), a 3.5 mm straw is one feed pixel wide at d = 3.5 mm × 965 / 4.3 ≈ 0.8 m, and thinner beyond. H's long straw (5 px at 1920) is therefore about 0.7 m away. A cut piece flung at 20–80 m/s (stem-properties.md §2, assumed) crosses the picture's near field in under one 20 ms field. The blades' sharpness (§3.2) says the exposure is short, so a piece would show as a sharp speck or short streak in one field only, and the recorder keeps only 3 of every 5 fields (ground-micro-motion.md §2.3).

## 2. The motion against the seconds before

- **Brushing a stalk (H 7.6–9.6 s)** doubles the 4–15 Hz roll and pitch (×1.7–2.5) and triples the yaw rate's spread. The bands below 4 Hz barely move (measured): a brushed stem adds a high-frequency rattle, not a lurch.
- **Pushing through weeds (D)** is the strongest measured contact: 4–15 Hz pitch 0.39°, 10–30× the hover, mostly in pitch.
- **Going into the straw (H's end)** is a large, fast turn (30–120 °/s) and a 3–10× rise in the per-frame residue. It is closed loop and includes the pilot's input and the impact: it cannot be split into the props' cutting and the body's contact.
- **What no clip shows:** a hover or a slow descent with the props in standing grass, where the cutting would be the only source.

## 3. The props in frame

### 3.1 By clip (measured, by eye on consecutive frames)

| Airframe group (video-feed.md's three set-ups) | Clip, stretch read | Where | How it looks | How often |
|---|---|---|---|---|
| A | A 13.3–14.1 s (24 frames) | both **top corners**, crossing in from the edge, 15–25 % of the width long | a thin dark sliver, sharp-edged, at a different angle and length in each frame; in f421–422 a wider translucent grey wedge at the top left | top left 11 of 24 frames, top right 12 of 24 |
| B, G | B 3.7–4.5 s (24 frames); G by its median and transients | the **left and right edges**, 25–45 % of the height down (above the horizon), 20–33 % of the width long | dark blue-grey; mostly a thin sharp bar, sometimes a broad blurred wedge or a dark lens-shaped smudge; angle changes every frame | right about 20 of 24, left about 16 of 24 |
| C, D, E, F, H, L, P | P 31.5–32.3 s (24 frames); every second frame of H; all of D's push | P: **top-right corner**, high, sometimes top left; C–H: none at this scale | P: a thin dark line; at the left edge a translucent grey band (wind.md R8) | P: top right in most frames, left wedge 4 of 24; D: 2 frames (video-feed.md §1.4); H: none |
| S | the whole clip (median and transients) | none | — | — |

### 3.2 What that means for the first-person view

- **The blade is never static.** At 290 blade passes a second (audit §5.2) against 50 fields, each field catches the blade at an unrelated phase (derived), so its angle and length jump from frame to frame. A field that misses the blade shows nothing in that corner.
- **It is mostly sharp.** A thin sliver means the exposure covers a few degrees of rotation at most: at about 145 rev/s, 5° is 0.1 ms (derived, rpm assumed from audit §5.2). The occasional translucent wedge spans 15–30°, about 0.3–0.6 ms: a longer exposure in duller light (P, overcast) or the other blade of the pair. Overcast H shows no blade at all, so in that airframe group the discs sit above or beside the picture except when the body pitches (P).
- **Where depends on the airframe:** top corners (A, P) or side edges near the horizon (B, G). The sim's drone matches the C–H group (the pilot's quad class). So a blade should appear only now and then, high in a top corner, a thin dark sliver at a new phase every field, and not at all in most frames. The current sharp, static blade in the upper left is wrong in both its stillness and its constancy.

### 3.3 The pale rods are airframe parts, not props and not the display

[object-sheets/houses-and-yard.md](../object-sheets/houses-and-yard.md) reads the two pale bars at mid-height as the display's artificial-horizon bars; wind.md calls them "two airframe parts". The frames settle it for the parts (measured, H f608–627):
- the right rod turns from pale grey to dark as the light on it changes, which a drawn display element cannot do;
- in f623–627 single straws pass **in front of** it.
- Their shape (a straight bar, the left one with a bent tip) is the same in every frame and every clip of the group (the clips' medians).

They are camera-fixed parts of the airframe, about mid-height at each side (most likely the front legs or antenna masts, assumed). They matter for 2.5: a stem at the lens can pass in front of them.

## 4. Targets for 2.2 and 2.5 (REAL-03)

| Target | What the footage gives | Use |
|---|---|---|
| **Brushing one stalk at the creep (2.2)** | H 7.6–9.6 s: ×1.7–2.5 at 4–15 Hz in roll and pitch, below 4 Hz unchanged | a creep at 0.3–0.6 m past a lone 5–10 mm weed stalk (assumed): the side-by-side and the band ratio |
| **Pushing through weeds (2.2, 2.10)** | D 5.0–6.9 s: 4–15 Hz 0.16–0.22 / 0.39 / 0.19° | the push at 1 m through weeds of the D scene |
| **Into straw (2.2)** | H 20.0–20.94 s: 30–120 °/s turns, residue ×3–10, straw at the lens, the rod occluded | the sequence's look only (tilt, blur, straws in front of the rods); the numbers are low confidence and include the pilot |
| **Stems at the lens (2.5's neighbourhood)** | H f258–263 (a soft tuft rising at the bottom, 13 % of the width, 6 frames); H f313–321 (a whole straw sweeping through the lower third, 8 frames); D's leafy tops | how close, soft and slow a stem near the lens looks through this feed; whole stems must stay possible and common |
| **Blades in view (VIS-07)** | A, B, P (§3.1) | per-field phase, sliver against wedge, corner and frequency |

**What has no footage:**
- props cutting anything: no cut piece, chaff or spray in any clip;
- a landing, a hover or a slow descent with the props in standing grass or straw (2.2's main case);
- a touchdown of any kind (ground-micro-motion.md §0);
- the cut bits' look, size, number, direction and duration in front of the lens (2.5): only the pilot's account.

## 5. Pilot question

For `docs/pilot-questions.md`, at the gate that shows 2.5:

> When you land in tall grass or straw, where do the cut bits show in your goggles: up near the props at the top of the picture, or across the lower half, and are they quick streaks or pieces you can make out?

(Qualitative, about the pilot's own flights. A recording of such a landing, cropped and cited by a new letter only, would answer 2.2 and 2.5 at once; ground-micro-motion.md §7 Q1 already asks for one.)

## 6. Reproduce

The clip's path is passed on the command line and never written; every output lands in the git-ignored `reference/_frames/<letter>/`.

```
python tools/reference/motion_bands.py --letter H --segment pre_stalk=170-228 --segment stalk=229-290 --segment mid=330-539 --segment pre_end=540-599 --segment end=600-628
python tools/reference/motion_bands.py --letter A --segment low=241-390 --segment final=391-423
python tools/reference/extract_frames.py <clip D under reference/> --letter D --fps 30 --start 4.5 --duration 2.6 --out reference/_frames/D/veg
python tools/reference/contact_sheet.py --letter D --frames reference/_frames/D/veg --first 1 --count 24
```

The frame-by-frame reading (every frame of H at 640 × 360, native crops of H f255–330 and f530–630, consecutive-frame sheets of A, B, P and D, and each clip's temporal median for camera-fixed parts) used throwaway scripts kept outside the repo; their outputs are local only.
