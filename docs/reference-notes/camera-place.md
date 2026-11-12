# Where the camera sits, as the footage shows it

**Change:** `w14-camera-place`, task 1.1 (research only) · **Requirements:** FEED-02, VIS-07, DRN-04, DRN-03, REAL-01 · **Decisions:** D-028, D-103 · **Letters:** C, D, E, F, H, L, P (the sim's airframe group, [vegetation-on-the-props.md](vegetation-on-the-props.md) §3.1) and T (the airframe, [../object-sheets/drone.md](../object-sheets/drone.md)).

Labels: **measured** (read off the letters), **published** (a source read), **derived** (computed from those), **assumed**. Body axes as `drone.json`: x right, y up, z forward, metres. Picture coordinates as the feed's lens (FEED-02): u right and v down from the centre, in half-widths (the picture spans u ±1, v ±0.5625); "undistorted" means after p_u = p_d (1 + k1 r_d²), k1 0.33, and a pinhole of f = 1.005 half-widths.

## 0. The answer

- **Recommended `camera.lens_m`: (0, −0.020, 0.205) m, derived** (§5). That is 45 mm ahead of the front motors' axis (z 0.160) and 55 mm below the prop plane (y 0.035): a micro camera on a nose mount **below the bottom plate's front**, not between the plates. Error: y −0.020 ± 0.005 m; z 0.205, with 0.202 the least that keeps every blade out (derived).
- **The fit.** At that place a front blade is in the picture for **0 % of a turn** (derived). If the lens sits 5 mm further back (z 0.200), the share is 1.4 % at the top corners (u ≥ 0.98, the top 3 % of the height). That is D's two frames of thin slivers at the top edges. C, E, F, H and L show no blade.
- **Before, at (0, 0.015, 0.115):** 34 % of a turn at the side edges, 17–96 % of the half-height above the centre (derived, the same as `w13-props-in-frame` design §1).
- **The rods are not part of letter T** (§3). They are two thin pale parts, at most about 2 mm thick, parallel to the camera's axis within 1°, one at each side below the lens. Their front tips are bent and sit ahead of the lens. They are most likely wire antennas or wire guards (assumed). The rods fix ratios, not a scale: each sits **0.19 × its side offset below the lens**, and its tip lies **1.57 (left) / 1.74 (right) × its side offset ahead of the lens** (measured, derived).
- **One place does not fit every letter.** C, D, E, F, H and L fit the recommended place. P's blade (a line in the top quarter of the picture at the right in every frame) fits **(0, −0.020, 0.150)**, with a residual under 2 % of the height. P's lens therefore sits about 55 mm further back on its build, or that build's props reach further. The sim takes the C–H place: it is most of the group and the proposal's "a blade almost never" (§5.3).
- **The published frame does not say** where the camera sits (§4). The maker's pages give the envelope only. The assembly drawing could not be read here.

## 1. The rods (measured)

**Method.** For each letter, a median of 14–25 stills over the clip was taken in two crops at the sides of the lower half. The rods are camera-fixed, so the median keeps them and averages out the scene. Each rod's ridge was then traced from the side edge inward to its tip (contrast against its ±7–9 px rows, centroid to subpixel). Traces were made at 1920 × 1080 and converted to half-widths. The local sheets are in `reference/_compare/w14-camera/` and are never committed.

**Where they sit in the picture.** Each rod enters at the picture's side edge and runs inward, rising, to a bent tip. The table gives the edge crossing as a share of the height from the top edge, and the tip as (u, share of the height from the top).

| Letter | Left rod: edge → tip | Right rod: edge → tip |
|---|---|---|
| C | 66 % → (−0.62, 60 %) | 69 % → (+0.53, 59 %) |
| D | 67 % → (−0.56, 62 %) | 69 % → (+0.48, 60 %) |
| E | 67 % → (−0.63, 62 %) | 67 % → (+0.54, 59 %) |
| F | 74 % → (−0.57, 66 %) | 74 % → (+0.55, 67 %) |
| H | 63 % → (−0.53, 54 %) (tip by eye; the trace ran past it) | 61 % → (+0.54, 52 %) |
| L | (too dark to trace) | 71 % → (+0.56, 66 %) |
| P | 64 % → (−0.55, 58 %) | 65 % → (+0.50, 56 %) |
| **Spread** | edge 63–74 %, tip u −0.53 to −0.63 at 54–66 % | edge 61–74 %, tip u +0.48 to +0.56 at 52–67 % |

**Their lines, once undistorted** (a straight line fitted to the straight 80 % from the edge; residual 0.6–2.9 px rms at 1920 wide, except H's left at 5.6 px):
- **Slope**, v per |u|: left 0.171–0.216, mean 0.185; right 0.176–0.216, mean 0.195. Over both sides, **0.19 ± 0.015**.
- **Offset from the centre:**
  - In C, D, E and P each rod's line passes within **0.007–0.023 half-widths of the centre**. A line through the centre is a rod parallel to the camera's axis, so in those clips the camera's tilt against the rods is **−1.2° to +0.9°** (derived from where the two lines cross). That fits the pilot's 0° uptilt, if the rods run along the body.
  - **F** crosses 0.10 below the centre (+5.7°) and **H** 0.06 above (−3.6°). Either the camera's tilt varies by about ±5° between flights or builds, or the rods are bent.
- **Tips:**
  - Undistorted |u| at the tip: left 0.58–0.72 (mean 0.64), right 0.51–0.63 (mean 0.58).
  - A rod at side offset a has its tip at dz = f·a/|u| ahead of the lens: **left 1.57 a (1.40–1.73), right 1.74 a (1.60–1.97)** (derived).
  - Each rod leaves the picture at the side edge (undistorted |u| ≈ 1.34), so it runs back to **dz ≤ 0.75 a** or further, out of view.
- **The bend:**
  - C's right tip hooks up toward the centre.
  - D's left tip flattens off and drops slightly.
  - The other tips bend a little, in no common direction.
  - So these are individual bends of a thin, bendable part, not a moulded shape.
- **Width:** a FWHM of 9–14 px at 1920 wide, about 3 field lines, so near the feed's resolution. Undistorted, that is 0.015 half-widths where the rod is at dz ≈ 1.03 a. The diameter is therefore **at most about 0.016 a**: under 1 mm at a = 0.06 m and under 2.4 mm at a = 0.15 m (derived, an upper bound because the feed blurs).
- **Look:** pale grey to white against grass and walls. They turn dark when the light changes (H, vegetation-on-the-props.md §3.3), and straws pass in front of them (H). Thin dark rims on both sides are the feed's sharpening halo (video-feed.md P3).

## 2. The blades (measured, from §3.1's counts plus where)

| Letters | How often | Where |
|---|---|---|
| C, E, F, H, L | none in the stills read | — |
| D | 2 frames (D f138–139, video-feed.md) | thin dark slivers at the **top edges** |
| P | every frame of the opening 48 (24 read, every second one); a left wedge in 4 of 24 in the 31.5–32.3 s stretch | a thin dark line, nearly level, across the **top right**: from u ≈ +0.5 to the edge, 0–23 % of the height from the top; a new height every frame |

**What the projection says** (`tools/reference/camera_place.py`, derived). It takes the blade's centre line from the hub to the tip, in the hub's plane:
- the last part of the disc to leave the picture as the lens moves forward is at the **side edge** while the lens is within 45 mm below the hubs (y ≥ −0.010);
- it is at the **top corners** once the lens is lower;
- so D's top-edge slivers and P's top-quarter line both say **the lens sits below y ≈ −0.010**;
- B and G's blades at the side edges, 25–45 % down, are what a lens at the plates' height gives.

## 3. Which parts the rods are

**Not any part of letter T** (measured, T and the drone sheet):
- **The legs and skids** are 10 mm tubes. A rod 10 mm thick at this width would sit 0.6 m or more to the side, outside the frame. The legs are vertical, and the skids run 0.26 m below the plates: from any lens on the frame they lie below the picture or appear steep, never as near-level lines through the centre.
- **The arms** run at 45° in plan. Their image lines would converge at (±1.0, 0) undistorted, not through the centre, and from the recommended lens they lie behind it.
- **The packs, canister, bottle and motors** are blocks behind the lens.
- T shows **no antenna** and no wire at the front, but T's front is hidden: the camera faces away (drone.md). So T can neither confirm nor rule out the rods.

**What they most likely are** (assumed):
- **Thin wire-like parts** reaching **forward past the lens at each side**, mounted with or near the camera. Likely candidates are the receiver's wire antennas in pale sleeves, or bent-wire guards that protect the lens.
- Both fit "thin, pale, straight, hand-bent tips". The pilot's group has them in every clip, so **both builds carry them** (they are on the camera end, which the legs-on/legs-off swap does not touch, assumed).
- This is the one qualitative question for the pilot (§7).

**For the model** (derived from §1, scale assumed). Take a side offset **a = 0.060 m** (assumed: just outside a 19 mm camera and its mount's cheeks). Each rod is then:
- at x = ±0.060, y = y_lens − 0.19 × 0.060 = **−0.031 m**;
- running fore-aft from at least z_lens + 0.045 = **0.250** (out of view behind that) to the tips at **0.299 (left) and 0.309 (right)**;
- **at most 1 mm across** (take 1 mm);
- each with a short hand bend at the tip.

A larger a scales every one of those offsets and the diameter bound alike. The picture is unchanged, but the rods reach further and thicker, and stems meet them sooner. 0.06 m is the smallest offset that clears the camera.

## 4. The published frame (read)

- **iFlight CX10 ECO frame kit** (https://shop.iflight.com/CX10-ECO-Frame-kit-Pro2081) and the **CX10 ECO 6S** build page (https://shop.iflight.com/Chimera-CX10-ECO-6S-Pro2067), both read: wheelbase 452 mm; frame 338 × 299 × 34 mm; 6 mm arms; 3/2/3 mm plates; a 30.5 mm stack; a vertical antenna mount in the box; a TPU set (GPS, buzzer and action-camera mounts) not included in the ECO build. **Neither gives the FPV camera's mount, its size or its place.** 338 × 299 has a diagonal of 451 mm, the wheelbase, so it is the motor spacing and not the plates' length (derived). It says nothing about how far the plates or a camera reach forward.
- **The frame's assembly drawing** (the "Frame Assembly Guide V1.0" on the kit page) was fetched. It is an exploded drawing exported as a PDF with no text layer, and no PDF renderer is available on this machine, so **it was not read**. The product photos on the maker's image server refused the request. Reading that drawing is the one published check still open (§6).
- **Search** (iFlight, reseller pages, reviews) found no drawing or review that gives the CX10's camera place, and no page for the class build's camera mount. The class's camera is the 19 × 19 mm Caddx Ratel 2 (published, drone.md's research table).
- So the place in §5 rests on the footage alone. It is consistent with what the class's builds are known to do: heavy fiber builds hang the camera on a nose bracket under the frame, ahead of the stack, because the canister and payload take the space between and below the plates (assumed, not from a source read here).

## 5. The fit

### 5.1 Method (derived)
- The feed's projection as above: the front hubs at (±0.15981, 0.035, 0.15981), 0.254 m props, 2 blades, a 9.5 mm hub radius (`drone.json`).
- For a lens at (0, y, z) facing +z at 0° uptilt, the share of a turn in which any point of one blade's centre line projects inside the picture, as in `w13-props-in-frame` design §1. The tool reproduces that note's **34 %** at the old place.
- The share is never fitted to pixels. The targets are the conditions in §2: none or almost none in C–H, slivers only at the top edges (D), and P's band.

### 5.2 The scan (derived, share of a turn in %)

| lens y \ z (m) | 0.115 | 0.145 | 0.175 | 0.190 | 0.200 | 0.205 |
|---|---|---|---|---|---|---|
| +0.020 | 34.4 | 26.9 | 17.8 | 10.6 | 0 | 0 |
| +0.010 | 33.6 | 25.8 | 17.5 | 11.1 | 0 | 0 |
| 0.000 | 31.9 | 23.6 | 15.6 | 10.3 | 3.5 | 0 |
| −0.010 | 29.7 | 21.7 | 13.3 | 8.3 | 3.3 | 0 |
| −0.020 | 26.9 | 19.7 | 11.1 | 6.1 | 1.4 | 0 |
| −0.030 | 25.0 | 17.5 | 8.9 | 3.6 | 0 | 0 |
| −0.040 | 23.1 | 15.3 | 6.1 | 0.3 | 0 | 0 |

The least z with no blade is **0.198–0.203 m for any y from −0.030 to +0.030** (bisection). **Lowering the lens alone cannot clear the blades:** at the old z, even 55 mm lower still leaves 23 %. The side edges see ±53° across and ±38° up and down, so a disc beside the lens and ahead of it stays in view. The lens has to sit level with or ahead of the discs' inner arcs. The height decides only **where** the last sliver shows: the side edge for y ≥ −0.010, the top corners below that.

### 5.3 The place

| Target (measured) | Condition (derived) | Result |
|---|---|---|
| C, E, F, H, L: no blade | share 0 | z ≥ 0.202 at y −0.020 |
| D: 2 frames of thin slivers at the top edges | share near 0, entering at the top corners | z ≈ 0.200, y ≤ −0.010 |
| P's band (another lens place on its build, below) | the band's bottom at 23 % of the height, in from u ≈ 0.5 | y −0.020 gives 0–23 % and u ≥ 0.56 at z 0.150; y −0.015 gives 0–25 %, y −0.025 gives 0–20 % |
| The rods | parallel to the axis (C, D, E, P) | any place: they fix ratios only (§1) |

- **y = −0.020 ± 0.005 m (derived).** It comes from P's band, whose bottom edge moves 2–3 % of the height per 5 mm of lens height, and is bounded by D's top-edge slivers (y ≤ −0.010). Residual on P's band: 0 % of the height at the bottom edge, and 0.06 half-widths at its inner end.
- **z = 0.205 m (derived)**, 3 mm past the 0.202 that clears every blade, as C–H show. Residual: none of C–H's blade-free stills is contradicted; D's rare slivers are the 1.4 % at z 0.200.
- **P's own place** is (0, −0.020, 0.150) by the same fit: the same height and **55 mm further back**. Its rods sit where every other letter's do (§1), and they are scale-free. So P's build has the camera further back against its props, or longer props. **The group does not share one z.** The sim takes C–H's: five of the six letters with a reading, and the proposal's target.
- **Sensitivity** (derived):
  - z_lens tracks the front hubs' z one for one: the lens sits **0.045 m ahead of the front motors' axis**.
  - Another 5 mm of blade below the hub plane (the trailing edge at pitch) moves the least z by under 1 mm.
  - The stretched-X reading of the frame (drone.md Open points 3) moves the hubs, so it would need its own run of the tool.

### 5.4 What it predicts (derived)
- **At (0, −0.020, 0.205):** a front blade in the picture for **0 % of a turn**, so no blade in a level hover. Over 24 fields, 0 fields.
- **Over the error:**
  - at z 0.200: 1.4 % of a turn, only at the top corners (u ≥ 0.98, the top 3 % of the height);
  - at y −0.015 and z 0.200: 2.5 %, the top 7 %.
- **Through the camera's read**, any share above 0 shows in most fields (`w13-props-in-frame` §1: 5–6 passes a field). The QA test in the spec should therefore read "0 of 24 fields, or slivers only in the top corners" against this note.
- **The rods** at the a = 0.060 m placing land where §1 has them: slope 0.19, tips at u ≈ ±0.64 / ±0.58 undistorted (±0.56 / ±0.51 in the picture), at 58–60 % of the height. The model can check that by projection, the same way.

## 6. What has to move in the model (for 1.2 and 1.3)

- **`drone.json` `camera.lens_m`** moves from (0, 0.015, 0.115) assumed to **(0, −0.020, 0.205), labelled derived**, its note citing this note's §5.3.
- **The camera mount (D01's generator).** The 19 mm camera, its centre about 10 mm behind the lens at (0, −0.020, 0.195), hangs **under the bottom plate's front** on a nose bracket:
  - the bracket reaches from the plate (its underside at y −0.003) about 0.10 m forward of the plates' front edge (z 0.10), its top about 7 mm under the plate;
  - its shape, material and mass are assumed (a printed or carbon bracket, about 10–15 g).
  - The camera and bracket must clear the canister's front (`coil`, drone.md: its front face's top 0.023 m under the bottom plate) and the props: the front discs reach to x ±0.041 at z 0.205, 55 mm above the lens.
- **The rods (VIS-07)** are geometry at the §3 placing: two thin pale rods (1 mm, a light grey or sleeve-white palette, assumed) from the bracket forward, with hand bends at their tips. They are seen by the FPV camera and are snag parts for stems (vegetation-on-the-props.md §3.3, §4).
- **Mass (1.3):**
  - the camera's 5.9 g moves 0.09 m forward and 0.035 m down from its old place in the electronics part;
  - the bracket and rods add about 15 g at the nose (assumed);
  - the mass build's CoM moves forward by roughly 0.5 mm and the pitch inertia rises slightly (derived, order of magnitude: 5.9 g × 0.09 m plus 15 g × 0.15 m over 5.08 kg).
  - The camera point's signals (the modes' turn at the camera, the air velocity for the lens drops) now act at the new place: 90 mm further forward of the CoM.
- **Nothing else moves:** the hubs, props, wheelbase, lens intrinsics and the 0° uptilt stay (the spec's non-goals). The rods also confirm the 0° tilt in C, D, E and P (§1).
- **Unread, for whoever revisits:** the CX10 ECO's assembly drawing (§4) could confirm or bound the bracket. If it shows a camera cage between the plates, the C–H blades say the class's builds still carry the camera forward of it.

## 7. Pilot question

For `docs/pilot-questions.md`, at the gate that shows the new camera place:

> In your goggles there are two thin light rods low at each side of the picture. What are they on your drones: antennas, wire guards, or something else? And does your camera sit out at the nose, under the frame?

(Qualitative, about the pilot's own builds. The answer settles §3's identification and the rods' scale, a. It does not change the lens's place, which the blades fix.)

## 8. Reproduce

The tool reads no reference file. It needs only the geometry above.

```
python tools/reference/camera_place.py                          # the scan of §5.2
python tools/reference/camera_place.py --lens 0 -0.020 0.205    # the recommended place: 0 %
python tools/reference/camera_place.py --lens 0 0.015 0.115     # the old place: 34 %, w13's number
python tools/reference/camera_place.py --lens 0 -0.020 0.150    # P's place: its band
```

The rods' traces (§1) came from per-letter median crops made in the scratchpad and the git-ignored `reference/_compare/w14-camera/`. They are not kept as a tool: the measurement is the table above.
