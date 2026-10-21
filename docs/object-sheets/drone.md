# Object sheet: drone (D01 Svinoriz 10 Opto, D02 launch stand)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (tasks 1.3 and 1.4 of `p5-drone`).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values. **No logo, brand text, label, QR code or printed strap text on any part** (D-043): T's markings are not copied. The payload is a generic body: its outline, size and mass, and nothing else (DRN-01).

**Rows:** D01 (the drone) and D02 (the launch stand), [asset-list.md](../asset-list.md) §11. **Requirements:** DRN-01…07, AST-02, AST-05, REAL-01…05, VIS-07 · **Decisions:** D-019, D-022, D-024, D-028, D-038, D-042, D-043 · **Change:** `p5-drone`, task 1.1.

## How this sheet was made

- **Letter read: T**, the pilot's photo of a typical heavy fiber-optic quad standing on its legs (D-043). One still, registered in the ignored `reference/_frames/T/` with the reference tools, plus local zoom crops and an annotated reading beside it. No clip shows the drone itself; the feed letters show only prop blades in view (video-feed.md §4: dark blade arcs in the top corners in A, blurred blue-grey blades at mid-height in B and G).
- **Reused, not re-derived:** knowledge.md §5 (K§5.1 published specs of the class, K§5.2 the first mass build, K§5.3 thrust and authority, K§5.4 the gap geometry and the stand), §4.2 (the aircraft in the pilot's words), [fc-profiles.md](../reference-notes/fc-profiles.md) (the class's flight controller; 8S Li-ion on the OTU batch) and the answered rows of `docs/pilot-questions.md`.
- **Scale.** T has no ruler. It is scaled as the 10-inch X it stands for: a pinhole fit of the four motor cans' image positions and widths to a square X of **452 mm wheelbase** (published, the class's frame) gives **1.37 px/mm at the drone's centre**, a camera **about 2.7 m away** and **about 4.4° above the arm plane**, with 1.9 px rms error (derived). Two independent checks agree: the motor can then measures **38 mm** across (a 3115 is 37.1 mm, published) and the longest blades **0.57 × the motor radius**, a **10.0–10.4-inch** prop (a 10-inch on 452 mm is 0.56, derived). These are ratios, so T alone cannot tell a 10-inch from a scaled-up class (Open points 1).
- **Which way T faces** (derived; the camera itself is hidden):
  - The spool's end cap is seen almost face-on (ellipse 0.88 → its axis 28° from the line of sight) and the payload's tail nearly edge-on (ellipse 0.40 → 66°). The frame's diagonals fit at 15.7° to the image plane, so the body axes lie at 29.3° and 60.7° from the line of sight: **the spool lies along one body axis and the payload along the other.** The skid feet run parallel to the spool (their image slope has the sign of that axis, not the other), and so do both packs.
  - Reading adopted: **the spool's axis is fore-aft, its grey cap is the rear end, and T is a rear-port three-quarter view, 29° off the tail.** It is the reading in which the fiber pays out aft along the spool's axis, the two packs ride along the sides (D-043's reading) and the skids run fore-aft, and in which the camera, which is not visible anywhere on the near side, sits at the far front. The frame's published 338 × 299 mm footprint, read as its motor spacing (its diagonal is the 452 mm wheelbase, derived) and fitted as a stretched X, puts the long spacing on the same axis (1.25 px rms error, derived), as stretched X frames carry it fore-aft.
  - **Consequence:** in T the payload hangs **crosswise**, its nose to starboard.
- **Labels:** every number is **pilot**, **measured** (from T, scaled as above), **published** (with its source), **derived** (with the derivation) or **assumed**.
- **Colours:** `photo` values are mean sRGB of hand-picked regions of T, in its bright sun; the generator authors albedo and the lighting adds the rest. There are **no feed check values**: no clip shows the drone.
- **Frames:** D-019 body axes, **x right (starboard), y up, z forward**, origin at the arm-plane centre (mid-thickness of the arms). Roll is about z, pitch about x, yaw about y.

### Research used across the group (published)

| Topic | Finding used here | Source |
|---|---|---|
| The class's frame (iFlight Chimera CX10 ECO, the frame of the Vyriy 10 Opto build, K§5.1) | wheelbase 452 mm; frame 338 × 299 × 34 mm; 6 mm arms; 3 mm bottom, 2 mm top and 3 mm upper plates; 383 g; a 30.5 mm stack pattern; 3K carbon; payload 1.5–2.5 kg | https://pyrodrone.com/collections/10-inch-frames/products/iflight-cx10-eco-10-frame-kit · https://www.robotshop.com/products/iflight-chimera-cx10-frame-kit |
| 3115-class motors, 900 Kv | Tarot 3115 900KV Φ37.1 × 32 mm, 113 g, 6S, 5 mm shaft, 9–10" props; Flash Hobby Arthur 3115 900KV Φ37.1 × 32 mm, 117 g, stator 31 × 15 mm 12N14P, M5 shaft, M3 19 mm mount, 3–6S, 9–10" props, 46 mΩ; Surpass Hobby Bat S3115 900KV 112 g, 40 A (K§5.1) | https://www.flyingtech.co.uk/product/tarot-3115-900kv-motor-tl1616/ · https://www.flyingtech.co.uk/product/flash-hobby-arthur-3115-900kv-6s-multirotor-brushless-motor/ |
| 3115 900KV thrust on 6S with 10 × 5 props (blade count not stated) | at about 25 V: 50 % throttle 1410–1588 g at 8678–9027 rpm, 12–14 A; 80 % 3108–3295 g at 12134–13011 rpm, 40–46 A; 100 % 4074–4205 g at 13363–14691 rpm, 65–72 A | https://www.flyingtech.co.uk/product/flash-hobby-arthur-3115-900kv-6s-multirotor-brushless-motor/ |
| 10-inch props | 2-blade nylon 10 × 4.5: 13.4 g, hub 6 mm thick; 3-blade carbon-nylon 10 × 5: 17 g, hub 9 mm thick | https://www.rotorama.com/product/gemfan-1045-2 · https://www.getfpv.com/discontinued/gemfan-cl-cinelifter-1050-3-blade-propeller-set-of-2.html |
| 21700 Li-ion cells | Molicel P45B: Ø21.40 × 70.00 mm, 70.0 g max, 4.5 Ah typical (4.35 min), 3.6 V nominal, 4.20 V charged, 2.5 V cut-off, 45 A continuous, 8 mΩ at AC 1 kHz; Samsung 50S: 5.0 Ah (4.8 min), 3.6 V, 2.5 V cut-off, at most Ø21.25 × 70.62 mm, about 72 g, 25 A continuous (35 A with a temperature cut-off) | https://www.akkuteile.de/en/molicel-inr21700-p45b-4500mah-45a-li-ion-battery-3-6v-3-7v_100852_3119 · https://www.kronium.cz/uploads/p45b-datasheet.pdf · https://eu.nkon.nl/samsung-inr21700-50s-5000mah-35a.html |
| Fiber spools | a retail 10 km canister: Ø101 × 355 mm, 0.95 kg with its fiber, 0.26 mm fiber, ABS shell; a 20 km module ≈ 1.75 kg with 0.27 mm fiber and its air unit; a 10 km class spool with 0.25 mm fiber (K§5.1) | https://www.getfpv.com/fiber-optic-canister-10km.html · knowledge.md §5.1 |
| Optical fiber | single-mode fiber: cladding Ø125 µm, buffer (coating) Ø250 µm | https://en.wikipedia.org/wiki/Optical_fiber |
| Fused silica | density 2.2 g/cm³ | https://en.wikipedia.org/wiki/Fused_quartz |
| FPV camera (the Vyriy build's, K§5.1) | Caddx Ratel 2: 19 × 19 × 20 mm, 5.9 g, 2.1 mm lens, 165° | https://racedayquads.com/products/caddx-ratel-2-1200tvl-16-9-4-3-ntsc-pal-micro-fpv-camera |
| Carbon-fiber composite | density about 1.55–1.6 g/cm³; stiffness depends on the layup | https://en.wikipedia.org/wiki/Carbon-fiber-reinforced_polymers |
| Steel | density 7850 kg/m³ | https://en.wikipedia.org/wiki/Steel |
| The class's airframes | Vyriy 10 Opto: S3115 900KV, 10" props, CX10 ECO 452 mm, payload to 1.8 kg; OTU-10: 3-blade 10", max take-off 5.35 kg ± 10 %, payload to 2 kg; 8S Li-ion on the OTU batch | knowledge.md §5.1 · fc-profiles.md §1 |

### Letter T: the reading (REAL-01, D-038)

**The scene.** A product-style photo of a heavy fiber-optic quad standing on its legs on a light grey folding camp table in a dry grass field, a hand radio on the table in front of it, out-of-focus trees behind. Only the drone is copied.

**The viewpoint.** About 0.2 m above the arm plane (4.4° down onto it, derived from the near and far motors' height difference), about 2.7 m from the drone (2.5–3.0 m: the square and stretched fits), a narrow lens of about 14–16° across (derived); a rear-port three-quarter view, 29° off the tail.

**The layout, as T shows it** (sizes at the 452 mm scale; every number measured unless labelled):

| Part | What T shows |
|---|---|
| Frame | an X of **flat carbon plate arms** (visible thickness 8–9 mm: a 6 mm plate, published, plus its top face at 4.4° and the motor leads along it), a central stack between plates on black standoffs, an electrolytic capacitor, open wiring on top |
| Motors | four black cans, Ø 38 mm, about 24 mm of bell above a narrower base about 6 mm tall, a silver M5 nut on top; a copper glint at the base |
| Props | **2-blade** on all four motors, black, slightly glossy (they mirror the sky blue-black), a slender scimitar planform widest near a third of the radius, **10.0–10.4 inches** tip to tip; the prop plane about 0.03 m above the arm top |
| Spool | **on top, fore-aft**, on a black cradle with two black cable-tie hoops and a light grey printed rear bracket. Its blue-grey ribbed fiber pack is Ø about 66 mm, the black front hoop Ø about 72 mm, the grey rear cap Ø about 58 mm with a recessed three-spoke hub and a central hole about 9 mm across; about 0.16 m long overall, its axis about 0.076 m above the arm top, its middle about 0.04 m forward of the centre (±0.03: it may instead sit a little to port) |
| Where the line leaves | not shown (no line is threaded). Read as **axially aft from the rear cap's centre** (assumed), the way the fiber must pay out behind the drone |
| Packs | **two, one along each side**, below the arm plane, between that side's legs: about **0.20–0.28 m long and 0.045 m tall**, their tops about 0.02 m below the arm top; a tan, paper-like wrap; two or three black hook-and-loop straps each with a purple edge (their printed text is not copied); leads leave the rear ends as red and black silicone wire to yellow connectors about 20 mm across (an XT90 class), unplugged and hanging in T |
| Payload | **slung below, between the legs, crosswise**: a body of revolution, a pale tail section Ø about 85 mm widening to about 110 mm, then a black ogive nose; about **0.24 m** long; its top level about 0.056 m below the arm top, its axis about 0.10–0.11 m below. The fixings are hidden. Its markings and everything beyond the body's outline are not copied (DRN-01) |
| Legs | **four black carbon tubes**, Ø about 8–10 mm, vertical, clamped under the arms about 0.14 m from the centre by black blocks about 25 mm square, each with a zinc-plated wing-nut bolt (quick-release, the legs-on or legs-off swap) |
| Feet | **two skids**, one per side, fore-aft carbon tubes Ø about 9–10 mm through black T-joints at the leg bottoms, running about 0.13 m aft of the rear legs, open tube ends; a lime band on one skid |
| Height | arm top to ground about **0.18 m**; foot to spool top about 0.30 m; payload bottom about 0.03 m above the table |
| Camera, antennas | the FPV camera is not visible (it faces away); **no antenna** is visible (the small silver parts are the leg clamps' wing nuts) |

**Look and feel.** A dense, workmanlike build: matte black carbon and motors, black props, black straps and cradle, set off by three light masses (the blue-grey spool on top, the pale tail of the payload, the light grey printed spool parts) and by small saturated accents (red silicone leads, yellow connectors, purple strap edges, the lime band). Clean and new, no dust or wear; cable ties and straps are tight; wires run in the open over the stack. The silhouette is tall and top-heavy: the spool rises about 0.08 m above the prop plane's centre, the payload fills the space between the legs and nearly touches the ground. Light: clear high sun from the right, crisp shadows falling left on the table; pale dry grass, dark conifers and white-barked bare trunks behind (spring or autumn).

### Reference view for this group (REAL-02)

| Letter | Scene type | Viewpoint | Framing | Light | Time of day | Weather |
|---|---|---|---|---|---|---|
| T | the legs-on drone standing on a flat light-grey surface, nothing else recreated (no table, radio or field) | 0.2 m above the arm plane, 2.7 m from the drone's centre, a 15° horizontal field of view (derived) | rear-port three-quarter, 29° off the tail; the prop tips span about 85 % of the width, the spool's rear cap facing the camera right of centre, the payload's nose pointing right | clear direct sun, high, from the right of the camera | midday | clear, dry |

The REAL-03 side-by-side for T is made in `reference/_compare/` once D01's first build exists (task 1.3); nothing exists to compare yet. T is a photo, so the view is noclip at full resolution, not through the feed.

### What T shows that the requirements and K§5.2 had wrong

| Item | Was | T shows | This sheet |
|---|---|---|---|
| Props | DRN-01, K§5: **3-blade** | **2-blade** on every motor (measured) | 2-blade default, 3-blade as an option; DRN-01's wording needs the orchestrator's update |
| Arms | D-043: "carbon plates and **tube arms**" | **flat plate arms** (the CX10 class's 6 mm plates, published); the tubes are the legs | plate arms |
| Mass layout | K§5.2: cargo forward and below, one pack on top, coil below | spool on top, two packs along the sides below the arms, payload slung below and **crosswise** | re-derived below: with a payload aboard roll now exceeds pitch, and yaw grows |
| Legs | pilot: plastic, 25–35 cm, a little soft | carbon tubes on plastic clamps and T-joints, about 0.18 m to the ground, with **skids**, not point feet | the pilot's length (0.25 m default), T's look; the feet are skids (Open points 2) |
| Coil | pilot: about 1 kg | a drum of about 0.3 litres of wound fiber: about 5 km, 0.5 kg full (derived) | the pilot's 1 kg (10 km) in T's shape, scaled up 1.25× (Open points 1) |
| Drone height for gap checks | the sheets' "about 0.55 m tall with legs" | 0.30 m (T); 0.38 m with 0.25 m legs (derived) | 0.38 m (0.31–0.48 m over the leg range) |

## D01 Svinoriz 10 Opto

**Seen in:** T (the legs-on build; every part above). The legs-off build is not in any letter: **R** (the pilot's words in D-043 and the earlier description it supersedes for the legs-on build).
**Research sources:** the table above.

### Dimensions

Positions are D-019 body axes (x starboard, y up, z forward) from the arm-plane centre. "T" in a range is T's own figure at the 452 mm scale.

| Part | Range | Chosen default | Label |
|---|---|---|---|
| Wheelbase (motor to motor, diagonal) | 0.452 m | 0.452 m | published (CX10 ECO); DRN-02 |
| Motor positions | square X: (±0.1598, ·, ±0.1598) m; stretched X (338 × 299): (±0.1495, ·, ±0.169) | square X | derived; DRN-02 keeps the square X (Open points 3) |
| Prop tip to tip | 0.574 m nose-first, 0.706 m diagonal (square X); 0.553 and 0.706 m (stretched X); T: diagonal 0.71 m ±0.01 | 0.574 / 0.706 m | DRN-02 (pilot, PR-9); T measured within its error |
| Arm | 6 mm carbon plate, 0.18 m from the body edge (r 0.045) to the mount (r 0.225); 20–32 mm wide | 6 × 25 mm, the motor mount end 32 mm wide with the M3 19 mm pattern | published (thickness, pattern); width assumed |
| Body plates and standoffs | frame height 34 mm; bottom 3, top 2, upper 3 mm plates; body about 0.20 (z) × 0.09 (x) m | 0.20 × 0.09 × 0.034 m, from y −0.003 to +0.031 | published (height, plates); plan assumed |
| Motors (4) | Φ37.1 × 32 mm; T: Ø38 mm can | Φ37.2 × 32 mm, base at y +0.003, axis at the motor positions | published; T measured |
| Props (4) | 10.0–10.4" (T); 2 blades (T); hub Ø18–20 × 6–9 mm; max chord 22–30 mm near 0.3 R; prop plane 0.03 m above the arm top (T) | 0.254 m, 2 blades, hub Ø19 × 8 mm, chord 26 mm, prop plane at y +0.035 | measured (count, size, height); published (hub thickness 6–9 mm); hub Ø and chord assumed |
| Spool, fiber pack | T: Ø0.066 m pack, 0.12 m wound length, core Ø about 0.032 m (about 5 km); 10 km needs 0.61 litres wound (derived) | **10 km: pack Ø0.082 m full, core Ø0.039 m, wound length 0.15 m** | derived (T's proportions scaled 1.25× to the pilot's 1 kg) |
| Spool, ends and cradle | T: front hoop Ø0.072, rear cap Ø0.058 with a 9 mm hole, 0.16 m overall | front hoop Ø0.090 m, rear cap Ø0.072 m, hole Ø0.011 m, overall 0.20 m; the cradle's seat at y +0.046 | measured, scaled 1.25× (derived) |
| Spool position, legs-on | T: axis 0.076 m above the arm top, middle 0.04 m forward | axis at (0, +0.087, +0.04) m along z | measured (T), raised by the larger radius (derived) |
| Spool position, legs-off | — | axis at (0, −0.053, 0) along z, hung under the bottom plate in the same cradle, inverted | assumed (D-043 reading, G5) |
| Packs (2) | T: 0.20–0.28 × 0.045 m side; width not visible | 6S2P each: 0.22 (z) × 0.047 (x) × 0.045 (y) m; inner faces at x ±0.050, centres (±0.0735, −0.043, 0) | measured (length, height, height position); width derived from the cells (Physics) |
| Payload | T: tail Ø0.085, head Ø0.110, ogive, length 0.24 m, top 0.056 m below the arm top | **at 1.0 kg: T's outline exactly**; centre (0, −0.108, 0), axis along x (crosswise), nose to starboard; scaled uniformly by (m / 1.0 kg)^(1/3), the top kept 0.05 m under the bottom plate; absent at 0 kg | measured (outline); scaling rule derived (DRN-03) |
| Payload, legs-off | — | the same body on the upper plate in the spool's place, crosswise, centre (0, +0.096, 0) | assumed (the pilot's earlier description: payload on top; G5) |
| Legs (4) | T: Ø8–10 mm, 0.18 m arm top to ground, at r 0.14 m on the diagonals (0.11–0.15) | 10/8 mm carbon tubes at (±0.099, ·, ±0.099), arm underside to ground **0.25 m**: a 0.25 m tube, about 0.21 m of it free between the clamp and the T-joint | pilot (length, 25–35 cm); measured (diameter, position) |
| Leg clamps (4) | T: about 25 × 25 × 35 mm black blocks, M4–M5 bolt and wing nut | 25 × 25 × 35 mm, wing nut 20 mm across | measured |
| Skids (2) | T: Ø9–10 mm, 0.13 m aft of the rear legs; total length 0.35–0.50 m | 10/8 mm tubes at x ±0.099, 0.45 m long centred on the legs (0.126 m past each leg), skid axis 0.005 m above the ground | measured (diameter, overhang); length assumed symmetric |
| T-joints (4) | T: about 16 mm sockets | 16 mm sockets, 30 mm along the skid | measured |
| FPV camera | 19 × 19 × 20 mm micro; 0° uptilt | at the front of the body between the plates, lens at (0, −0.005, +0.115) m | published (size); pilot (uptilt); position assumed (not visible in T) |
| Capacitor | T: Ø about 10 mm can | Ø10 × 20 mm, on the stack's rear | measured (diameter); length assumed |
| Pack leads | T: red and black silicone, 20 mm connectors | 10 AWG (Ø about 4.5 mm with its jacket), 0.10–0.15 m per lead, plugged to the drone's leads at the rear | measured (colour, connector); gauge assumed |
| Overall height, legs-on | T: 0.30 m | ground to spool top **0.38 m** (0.31 m at 0.18 m legs, 0.48 m at 0.35 m) | derived |
| Overall height, legs-off | — | spool bottom to payload top 0.24 m; the payload's top 0.11 m above the prop plane | derived |

### Construction and materials

- **Frame:** a carbon-fiber X, the CX10 ECO class: four 6 mm arms sandwiched between the bottom plate and the upper plates, black aluminium standoffs, black steel screws; the plates' cut edges show the grey laminate layers (a 0.5 mm lighter strip on every edge).
- **Motors:** black anodised aluminium bells with a ring of vent slots on top (assumed typical of the class), the shaft and a silver M5 nut, the stator's copper showing at the base; their leads run inboard along each arm's top in black sleeving (T).
- **Props:** black glass- or carbon-filled nylon, 2 blades, a 10 × 4.5 to 10 × 5 class (assumed: the thrust data's pitch), mounted on the shaft by the nut.
- **Spool:** a light grey printed rear bracket that holds the rear cap and foot in one piece, a black cradle under the drum with two black cable-tie hoops; the fiber pack itself is the visible blue-grey ribbed surface (T's ribs about 6 mm apart: the wind's pattern, a normal map); the front hoop is black.
- **Packs:** 21700 cells (Physics), nickel strip, wrapped in tan insulating paper over heat shrink (T's look), held by two black hook-and-loop straps with a purple edge, plain; red and black silicone leads with yellow connectors.
- **Payload:** a generic shell: a pale off-white tail, a black front section and nose with fine print layer lines (T); no seams, labels or parts beyond the outline.
- **Legs and skids:** roll-wrapped carbon tubes with a faint twill sheen, black printed or moulded plastic clamps and T-joints, zinc-plated steel bolts and wing nuts; open tube ends on the skids.
- **Electronics:** a 30.5 mm-pattern flight-controller and ESC stack (Betaflight 4.5 F405 class, fc-profiles.md), the capacitor, the fiber transceiver module (hidden in T: assumed in the stack or at the spool's rear bracket), the micro FPV camera, thin multicoloured signal wires; no antenna.
- **Materials (CC0):**

| Material | Candidate set (CC0) | Source page | Derived textures |
|---|---|---|---|
| carbon plates, arms, legs, skids | ambientCG `Fabric004` (a dark carbon-fiber weave; check the weave scale on fetch) | https://ambientcg.com/a/Fabric004 | tint to the photo values, a clear-coat roughness 0.3–0.4, the lighter cut-edge strip on plates |
| motor bells, standoffs | ambientCG `Metal046B` (pinned) | https://ambientcg.com/a/Metal046B | roughness 0.35 anodised; the vent slots as geometry |
| props, clamps, T-joints, cradle, connectors, payload shell, printed spool parts | ambientCG `Plastic006` | https://ambientcg.com/a/Plastic006 | tints per the colour table; a print-layer normal (0.2–0.3 mm lines) on the payload nose and the grey spool parts |
| pack wrap | ambientCG `Paper005` | https://ambientcg.com/a/Paper005 | tan tint, crease noise |
| straps | ambientCG `Fabric030` (pinned) | https://ambientcg.com/a/Fabric030 | black with a purple edge band, no print |
| silicone leads | ambientCG `Rubber004` (pinned) | https://ambientcg.com/a/Rubber004 | red and black tints |
| fiber pack | no set: a shader | — | blue-grey albedo and the wind's rib pattern as a normal map whose radius follows payout |

### Colour

| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| motor bell | #110e0f, sheen #404548 | — | measured T |
| arm side | #20252d | — | measured T |
| prop blade, upper face | #243449–#273549 (sky-lit black) | — | measured T |
| spool fiber pack, lit / shade | #6c7d8c / #2f3b4d | — | measured T |
| spool rear cap and bracket | #bcc4c8 | — | measured T |
| spool hub recess | #4a525b | — | measured T |
| spool front hoop and cradle | #09090c | — | measured T |
| payload tail, lit / underside | #949ea7 / #60676c | — | measured T |
| payload front and nose, highlight | #0e0f0d, #a8a7a6 | — | measured T |
| pack wrap (in the frame's shade) | #382918–#4a3924 | — | measured T |
| pack wrap, albedo | about #8a6a45 | — | assumed (tan insulating paper) |
| strap, edge | #06050e, #785c98 | — | measured T |
| connector | #d8a113 | — | measured T |
| red lead | #ca130f | — | measured T |
| leg tube | #464748 | — | measured T |
| clamp, T-joint | #121013, #33383d | — | measured T |
| skid band | #a7d40f | — | measured T |

The props in the feed: dark arcs (A) and blurred blue-grey blades (B, G), video-feed.md §4, no values. A 2-blade prop passes a blade 2 × rpm / 60 times a second, two thirds of a 3-blade's rate (derived; for task 1.5's blur).

### Variants and states

- **Legs-on (T):** spool on top, payload slung below, four legs and two skids.
- **Legs-off (D-043, to be confirmed at G5):** the leg tubes, T-joints and skids come off at the wing nuts; the clamps stay on the arms. The pilot's reason is that the drone "can't balance on the spool", which places the spool underneath: it hangs under the bottom plate in its cradle, inverted, the line still leaving aft. **Where the payload goes is not in T:** this sheet puts it on top, in the spool's place, after the pilot's earlier description of the class (the payload on top, the coil underneath); the alternative keeps it slung below, ahead of the spool. Both read as the pilot's words; **T supports only the legs-on build.**
- **Payload:** 0–2.0 kg, default 1.0 kg; the body is T's outline at 1.0 kg, scaled uniformly with the cube root of the mass, absent at 0.
- **Coil payout (DRN-05):** the wound pack's radius shrinks from Ø0.082 m to the Ø0.039 m core as the 0.69 kg of fiber leaves; the housing (0.30 kg) stays.
- **Packs:** 6S2P × 2 (default), 6S3P × 2 or 8S2P × 2 (Physics); the pack box follows the cell count.
- **Props:** 2-blade (T, default) or 3-blade.
- **Frame:** square X (DRN-02, default) or the stretched 338 × 299 reading (Open points 3).

### Damage pattern

None is modelled. A crash ends the sortie and is named by the physics with the part and the object (`p6-flight-core` design 7); broken props and arms are out of scope.

### Physics role

**The parts list for the parallel-axis build** (the input to `drone.json`, task 1.2). Positions are part centres; shapes are what the mass build integrates.

| Part | Mass | Shape | Centre (x, y, z) m | Label |
|---|---|---|---|---|
| motor × 4 | 0.113 kg | cylinder Φ0.0372 × 0.032, axis y | (±0.1598, +0.019, ±0.1598) | published |
| prop × 4 | 0.016 kg | 2-blade rod 0.254 m | (±0.1598, +0.035, ±0.1598) | published 13.4–17 g, default assumed; height measured |
| arm × 4 | 0.047 kg | rod 0.18 × 0.025 × 0.006 m on the diagonal, r 0.045–0.225 | r 0.135 on the diagonals, y 0 | derived (CFRP 1.55 g/cm³ × the plate) |
| plates, standoffs, hardware | 0.195 kg | box 0.09 × 0.034 × 0.20 | (0, +0.014, 0) | derived (the frame's 0.383 kg, published, less the arms) |
| electronics (stack 45 g, capacitor 10 g, camera 6 g, fiber module 50 g, wiring and connectors 100 g, straps and ties 40 g) | 0.25 kg (0.18–0.30) | box 0.06 × 0.04 × 0.10 | (0, +0.025, +0.01) | assumed (the camera's 5.9 g published) |
| packs × 2 (6S2P) | 0.91 kg each | box 0.047 × 0.045 × 0.22 | (±0.0735, −0.043, 0) | derived (12 × 0.070 kg cells + 8 % strip, wrap, leads) |
| spool, legs-on | 1.0 kg full, 0.30 kg empty | cylinder Φ0.083 × 0.20, axis z | (0, +0.087, +0.04) | pilot (full); derived (empty: 1.0 − 10 km × 0.069 kg/km) |
| spool, legs-off | as above | as above | (0, −0.053, 0) | assumed (G5) |
| payload, legs-on | 0–2.0 kg, default 1.0 | cylinder Φ0.10 × 0.24 at 1.0 kg, axis x, scaled by (m/1.0)^(1/3) | (0, −0.108, 0) at 1.0 kg, lowered 0.05 × (scale − 1) | published (K§5.1: payload to 1.8–2 kg); the spec's sweep 0–2.0; default assumed (K§5.2's typical) |
| payload, legs-off | as above | as above | (0, +0.096, 0) | assumed (G5) |
| leg tube × 4 | 0.011 kg | cylinder Φ0.010 × 0.25, axis y | (±0.099, −0.128, ±0.099) | derived (10/8 mm CFRP tube) |
| leg clamp × 4 (stay on legs-off) | 0.018 kg | box 0.025 × 0.025 × 0.025 | (±0.099, −0.012, ±0.099) | assumed (with bolt and wing nut) |
| T-joint × 4 | 0.008 kg | box 0.016 × 0.020 × 0.030 | (±0.099, −0.248, ±0.099) | assumed |
| skid × 2 | 0.020 kg | cylinder Φ0.010 × 0.45, axis z | (±0.099, −0.248, 0) | derived (10/8 mm CFRP tube) |

**Fiber mass (derived):** glass Ø125 µm at 2.2 g/cm³ plus coating to Ø250 µm at about 1.15 g/cm³ (assumed) gives **0.069 kg/km and 0.049 litres/km**; 10 km is 0.69 kg and, at a winding fill of 0.80 (assumed), 0.61 litres. The retail 10 km canister's 0.95 kg (published) leaves 0.25 kg for its shell, so a 0.30 kg housing and cradle (assumed) makes the pilot's 1 kg. T's drum holds about 0.31 litres: about 5 km, 0.5 kg full (derived).

**K§5.2 re-derived on T's layout** (derived: a parallel-axis sum of the parts above, boxes and cylinders with their own inertia). Seven loadouts per build; the legs-off build drops the leg tubes, T-joints and skids (0.12 kg).

| Loadout | Packs | Payload | Coil | Legs-on: mass, CoM y | roll / pitch / yaw kg·m² | coil above CoM | Legs-off: mass | roll / pitch / yaw kg·m² | coil above CoM |
|---|---|---|---|---|---|---|---|---|---|
| L1 | 6S4P | 0 | empty | 3.46 kg, −0.016 m | 0.039 / 0.038 / 0.055 | +0.104 m | 3.34 kg | 0.030 / 0.029 / 0.052 | −0.030 m |
| L2 | 6S4P | 0 | full | 4.16 kg, +0.001 m | 0.046 / 0.048 / 0.058 | +0.087 m | 4.04 kg | 0.031 / 0.032 / 0.055 | −0.025 m |
| L3 | 6S4P | 1.0 kg | empty | 4.46 kg, −0.037 m | 0.051 / 0.046 / 0.060 | +0.125 m | 4.34 kg | 0.046 / 0.041 / 0.057 | −0.057 m |
| **L4 nominal in flight** | 6S4P | 1.0 kg | half | **4.81 kg, −0.028 m** | **0.056 / 0.053 / 0.062** | +0.116 m | 4.69 kg | 0.048 / 0.043 / 0.059 | −0.053 m |
| **L5 nominal take-off** | 6S4P | 1.0 kg | full | **5.16 kg, −0.020 m** | 0.061 / 0.059 / 0.064 | +0.108 m | 5.04 kg | 0.049 / 0.045 / 0.060 | −0.049 m |
| L6 | 6S4P | 2.0 kg | full | 6.16 kg, −0.038 m | 0.083 / 0.072 / 0.076 | +0.126 m | 6.04 kg | 0.073 / 0.061 / 0.072 | −0.070 m |
| L7 heaviest | 6S6P | 1.4 kg | full | 6.46 kg, −0.030 m | 0.080 / 0.068 / 0.082 | +0.118 m | 6.34 kg | 0.070 / 0.057 / 0.078 | −0.051 m |

- **Ranges, legs-on:** take-off 4.16–6.46 kg, in flight 3.46–6.46 kg; roll **0.039–0.083**, pitch **0.038–0.072**, yaw **0.055–0.082 kg·m²**; radius of gyration in roll 0.105–0.116 m. K§5.2 had roll 0.033–0.057, pitch 0.040–0.072, yaw 0.043–0.060 (nominal 0.044 / 0.054 / 0.050).
- **What changed:** **roll now exceeds pitch whenever a payload is aboard** (the crosswise payload and the side packs), where K§5.2 had pitch above roll (cargo forward); without a payload the fore-aft spool keeps pitch level with roll. **Yaw grows by about a quarter** (the packs at x ±0.07 m, the legs and skids), so yaw is 0.9–1.4 × roll.
- **The CoM** sits between 0.001 m above and 0.038 m below the arm plane and 0.003–0.010 m forward (the spool's forward offset); the products of inertia are small (I_yz −0.001 to −0.005 kg·m²).
- **The coil:** **0.09–0.13 m above the CoM on the legs-on build** and 0.03–0.07 m below it on the legs-off build (`drone` spec, D-043). A side gust on the coil is a rolling moment of the opposite sense to K§5.2's on the legs-on build.
- **Thrust check:** L5's 5.16 kg is 1.29 kgf a motor; the published 6S table gives that near 45–50 % throttle with 10 × 5 props (derived), and a 2-blade prop needs more, in line with fc-profiles.md's 50–65 % liftoff check.

**The packs' options** (21700 Li-ion, two identical packs; published cells, derived packs):

| Option | Each pack | Cells, arrangement | Pack box (z × x × y) | Each | Both | Capacity, energy | Loaded R (both, cells only) |
|---|---|---|---|---|---|---|---|
| **A, default** | 6S2P, the two in parallel (6S4P) | 12 cells: 3 along, 2 across, 2 high | 0.22 × 0.047 × 0.045 m | 0.91 kg | 1.82 kg | 18–20 Ah, 389–432 Wh | 6 × R_cell / 4 = 18–24 mΩ |
| B | 6S3P, in parallel (6S6P) | 18 cells: 3 along, 3 across, 2 high | 0.22 × 0.068 × 0.045 m | 1.36 kg | 2.72 kg | 27–30 Ah, 583–648 Wh | 12–16 mΩ |
| C | 8S2P, in parallel (8S4P) | 16 cells: 4 along, 2 across, 2 high | 0.29 × 0.047 × 0.045 m | 1.21 kg | 2.42 kg | 18–20 Ah, 518–576 Wh | 24–32 mΩ |

- **Cells:** 4.5 Ah (P45B) or 5.0 Ah (50S), 3.6 V nominal, 4.20 V full, 2.5 V cut-off (fc-profiles.md's packs warn at 3.0 V and stop at 2.8 V), 70–72 g (published).
- **R_cell:** 8 mΩ at AC 1 kHz (published, P45B); **12–16 mΩ under load** (assumed: DC resistance about 1.5–2 × AC); K§5.3's 15 mΩ sits inside.
- **Why A is the default:** it is the pilot's own words (Li-ion, 20–35 Ah, 6S4P to 6S5P, "a couple of batteries") and T's pack size (0.22 m long, two cells high); the 3115 900 Kv motors are rated 3–6S (published). B is the pilot's upper capacity (27–30 Ah of their 20–35). C is the OTU batch's 8S (fc-profiles.md): at 8S the same rpm needs about 900 × 6/8 = **675 Kv** (derived), which the published 900 Kv motors are not rated for, and C's 0.29 m packs are at the long end of T's 0.20–0.28 m.

**Legs and feet.**
- **The feet are skids.** Each side's two legs stand on one fore-aft tube, so the drone meets level ground along two lines: the contact points are the skid's ends and the T-joints. A rigid four-point solve can use the four T-joints on hard ground and the skid ends on uneven ground (a proposal for task 1.2); the foot radius is the tube's, 0.005 m.
- **Compliance:** the pilot's "a little bit soft" (pilot). A 10/8 mm carbon tube with 0.21 m free bends sideways at about **6.6 kN/m** per leg (derived: 3EI/L³, I = 2.9 × 10⁻¹⁰ m⁴, with E about 70 GPa, assumed for roll-wrapped tube); axially it is stiff, so the vertical give is in the plastic clamps and T-joints and the skids' overhangs: **5–20 kN/m per leg, damping ratio 0.1–0.3** (assumed). At L5 each leg carries 12.7 N, 0.6–2.5 mm of static set (derived).

**Collision (gallery noclip only; the flight's shapes come from `drone.json` through the core):** a box per plate stack, a box per arm, a cylinder per motor, a thin disc per prop (r 0.127 m, 0.01 m thick), a cylinder each for the spool and the payload, a box per pack, a capsule per leg tube and skid. Each within 1 mm of the visual on the parts the spec measures.

**Material ids:** propose **`carbon_composite`** (arms, plates, legs, skids) for physics sign-off; until then `plastic`. Props, clamps, T-joints, spool, payload shell, pack wrap: `plastic`; motors: `steel`; leads: `cable`; straps: `fabric`.

**Gaps:** the drone is the reference every other sheet measures against: **0.574 m nose-first and 0.706 m on the diagonal** (DRN-02), footprint `w(θ) = 0.254 + 0.32·(|cos θ| + |sin θ|)` m at yaw θ (houses-and-yard sheet); every part (skids 0.45 m long, payload 0.24 m crosswise, packs at x ±0.097 outer) lies inside the prop envelope. **Height:** 0.38 m legs-on at 0.25 m legs (0.31–0.48 m); the sheets that assumed 0.55 m are stricter than the drone.

**Wind (for the body drag areas of task 1.2, derived, ±30 %):** frontal (along z) about 0.06 m², 0.024 m² of it the crosswise payload's side; side (along x) about 0.056 m², 0.017 m² of it the spool's side; top about 0.07 m² without the prop discs.

**Snag hazards (against wires and the drone's own fiber):** the props (thin wire wraps them, PR-6); the skids' open ends (`hook`); the legs and skids as a frame a wire can ride into; the crosswise payload's ends; the spool's cable-tie tails. **The drone's own line**, leaving the rear cap at y +0.087 on the centreline, crosses aft 0.05 m above the rear props' plane through the 0.066 m gap between their inner tips (derived: 0.32 − 0.254 m); legs-off it leaves below the props (phase 7d).

**Contact surfaces:** the skids on the ground (legs-on); the arms' undersides on the stand's four posts (legs-off, D02).

### Placement rules (for the world)

- At the start point: legs-on on the ground, nose to the pilot's chosen heading; legs-off on D02.
- In the gallery (task 1.6): both variants and the stand, with the turntable and T's recreated view.

### Generator parameters (`drone`, task 1.3)

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `layout` | enum | legs_on, legs_off | legs_on | legs_on: T's build; legs_off: legs, T-joints and skids off, spool under the bottom plate, payload on top |
| `payload_place_off` | enum | top, below | top | legs_off only: where the payload goes (G5) |
| `frame` | enum | square, stretched | square | motor positions: the 452 mm square X (DRN-02) or 338 × 299 mm |
| `arm_t`, `arm_w`, `arm_mount_w` | m | 0.005–0.008, 0.020–0.032, 0.028–0.036 | 0.006, 0.025, 0.032 | arm plate geometry |
| `prop_blades` | int | 2, 3 | 2 | blade count (T) |
| `prop_chord` | m | 0.022–0.030 | 0.026 | blade width at 0.3 R, tapering to a 0.008 m tip |
| `fiber_km` | km | 3–12 | 10 | spool size at build (T's drum is 5) |
| `payout` | 0–1 | 0–1 | 0 | wound radius from full to the core (DRN-05 hook) |
| `payload_kg` | kg | 0–2.0 | 1.0 | 0 removes the body; else T's outline × (m / 1.0)^(1/3) |
| `packs` | enum | A 6S2P×2, B 6S3P×2, C 8S2P×2 | A | pack boxes and strap spacing |
| `leg_len` | m | 0.18–0.35 | 0.25 | arm underside to ground |
| `skid_len` | m | 0.35–0.50 | 0.45 | skid length, centred on the legs |
| `tube_od` | m | 0.008–0.012 | 0.010 | legs and skids |
| `straps` | int | 2–3 | 2 per pack | straps per pack |
| `leads` | enum | plugged, unplugged | plugged | unplugged hangs them as in T |
| `skid_band` | bool | — | true | the lime band on one skid |
| `wear` | 0–1 | 0–1 | 0.1 | dust on blade leading edges and skids, scuffed strap edges; T is new |
| `seed` | int | — | — | any random choice (lead sag, tie tails) |

### Open points

1. **T's absolute size.** Scaled as a 10-inch, T's spool holds about 5 km and its legs stand 0.18 m; the pilot's 1 kg coil and 25–35 cm legs fit T's proportions on a frame about 1.3× larger (a 13-inch class), so T may show a bigger airframe than the Svinoriz. The Svinoriz keeps its 10-inch geometry (DRN-02) and the pilot's figures, in T's shapes: the spool scaled 1.25×, the legs at 0.25 m. G5 judges the look.
2. **Legs:** T shows carbon tubes on plastic clamps and skid feet; the pilot said plastic legs. Read here as the same assembly (the clamps and joints are the plastic, and the softness); the physics models the skids (task 1.2).
3. **Square or stretched X:** T fits both (1.9 px and 1.25 px rms); the CX10's published 338 × 299 mm would make the drone 0.553 m nose-first. DRN-02 stays 0.574 m; changing it is the orchestrator's (it sizes PR-9's cellar door).
4. **Which way T faces** is derived, not seen (the camera is hidden). If the pilot's drones carry the payload fore-aft and the spool crosswise, swap the two axes; the masses do not change, roll and pitch swap their payload terms.
5. **The legs-off layout** (spool underneath, payload on top or below) is the D-043 reading, confirmed at G5 by looking.
6. **Material id** `carbon_composite` needs the physics sign-off.

## D02 Launch stand

**Seen in:** R (the pilot's description, D-022: "like a chair upside down, so each chair leg is beneath each motor"; D-043).
**Research sources:** https://en.wikipedia.org/wiki/Steel (density, for the mass).

### Dimensions

| Part | Range | Chosen default | Label |
|---|---|---|---|
| Post height (ground to the post top) | about 0.25 m | 0.250 m | pilot (D-022) |
| Post positions | on the motor diagonals, under each motor | r **0.200 m** (x, z ±0.1414): the contact just inboard of the motor can (0.226 − 0.0186 − 0.0074 m), under the motor in plan | derived from D01 |
| Post section | a chair's leg: Ø22–25 × 1.5 mm round or 20 × 20 × 1.5 mm square tube | 20 × 20 × 1.5 mm square tube | assumed |
| Post cap | a chair-foot cap, Ø or □ 22–28 × 10–15 mm | 22 × 22 × 12 mm black plastic cap | assumed |
| Base | an X of two bars crossing at the centre, on the ground | 20 × 20 × 1.5 mm tube, each bar 0.44 m (to r 0.22 m), welded at the posts' feet | derived (D-022's X base) |
| Base height | 0.02 m | 0.020 m | derived (the tube) |
| Mass | 1.2–3.0 kg | **1.6 kg** | derived: about 1.8 m of tube at 0.871 kg/m (111 mm² × 7850 kg/m³) plus caps and welds |

**Fit to D01 (derived):** the arms rest on the four caps with their undersides at 0.250 m, the arm-plane centre at 0.253 m; the legs-off spool's lowest point (its front hoop) is 0.155 m above the ground, **0.135 m clear of the base** (the spec asks ≥ 0.020 m); a 1.0 kg payload slung below instead would clear it by 0.065 m. The leg clamps (r 0.14 m) sit inboard of the posts (r 0.20 m) and clear them.

### Construction and materials

- Steel tube, welded; black satin paint worn to bare steel at the edges and the cap seats (assumed: a household chair frame), plastic caps.
- **Materials:** ambientCG `PaintedMetal004` for the paint (candidate, https://ambientcg.com/a/PaintedMetal004), `Rust007` (pinned) in the worn spots, `Plastic006` for the caps.

### Colour

| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| paint | #1e1f21 | — | assumed |
| worn edges | #6e6a66 | — | assumed (bare steel) |
| caps | #18181a | — | assumed |

### Variants and states

One. A worn-paint dial only.

### Damage pattern

None.

### Physics role

- **Collision:** four boxes (posts), two boxes (base bars), four boxes (caps, the contact faces). Unanchored: physics computes tipping and dragging (D-022).
- **Material ids:** `steel` (posts, base); caps `plastic`.
- **Gaps, wind volumes, snags:** none that matter; the base bars are a trip for a dragging skid or line.
- **Landable:** the four cap tops (the drone's arms).
- **Mass:** 1.6 kg, CoM 0.07 m above the ground (derived).

### Placement rules (for the world)

At the start point when the legs are off, the drone on top; in the gallery's drone station.

### Generator parameters (`launch_stand`, task 1.4)

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `post_r` | m | 0.19–0.22 | 0.200, or from `drone.json`'s motor radius minus 0.026 | post positions on the diagonals |
| `post_h` | m | 0.22–0.30 | 0.250 | post top height |
| `section` | enum | square_20, round_22, round_25 | square_20 | post and base tube |
| `cap` | m | 0.010–0.015 | 0.012 | cap thickness (included in `post_h`) |
| `base_r` | m | 0.20–0.26 | 0.22 | base bar half-length |
| `paint` | sRGB | — | #1e1f21 | colour |
| `wear` | 0–1 | 0–1 | 0.3 | bare steel at edges and seats |
| `seed` | int | — | — | wear pattern |

### Open points

- The stand is a pattern, not a letter; G5 judges its look with the drone on it.
