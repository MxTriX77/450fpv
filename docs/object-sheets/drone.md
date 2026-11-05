# Object sheet: drone (D01 Svinoriz 10 Opto, D02 launch stand)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (tasks 1.3 and 1.4 of `p5-drone`).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values. **No logo, brand text, label, QR code or printed strap text on any part** (D-043): T's markings are not copied. The payload is a generic body: its outline, size and mass, and nothing else (DRN-01).

**Rows:** D01 (the drone) and D02 (the launch stand), [asset-list.md](../asset-list.md) §11. **Requirements:** DRN-01…07, AST-02, AST-05, REAL-01…05, VIS-07 · **Decisions:** D-019, D-022, D-024, D-028, D-038, D-042, D-043, D-050, D-079 · **Change:** `p5-drone`, task 1.1; brought up to the build from `p5-drone` 1.3–1.4, `p7g-legs` 1.1–1.4 and `p7d-tether` 1.14.

## How this sheet was made

- **Letter read: T**, the pilot's photo of a typical heavy fiber-optic quad standing on its legs (D-043). One still, registered in the ignored `reference/_frames/T/` with the reference tools, plus local zoom crops and an annotated reading beside it. No clip shows the drone itself; the feed letters show only prop blades in view (video-feed.md §4: dark blade arcs in the top corners in A, blurred blue-grey blades at mid-height in B and G).
- **Reused, not re-derived:** knowledge.md §5 (K§5.1 published specs of the class, K§5.2 the first mass build, K§5.3 thrust and authority, K§5.4 the gap geometry and the stand), §4.2 (the aircraft in the pilot's words), [fc-profiles.md](../reference-notes/fc-profiles.md) (the class's flight controller; 8S Li-ion on the OTU batch) and the answered rows of `docs/pilot-questions.md`.
- **Brought up to the build** (no new letter read): where the model, the stand and the flight now differ from the first reading, the rows below give the built value and cite its file. The sources: `assets-src/drone/drone.json` (the parts, `legs`, `legs_off`, `coil`, `payload`, `stand`, `contacts`; the coil's `canister` part and its contacts after [spool-exit-and-rear.md](../reference-notes/spool-exit-and-rear.md)), the look params `assets-src/drone/D01_legs_on.json`, `D01_legs_off.json`, `D01_bottle.json` and `D02_launch_stand.json`, the generators `tools/blender/generators/drone.py` and `launch_stand.py`, the palettes `assets-src/materials/drone_bottle.json` and `stand_steel.json`, the as-built notes of `p5-drone` 1.3–1.4, `p7g-legs` 1.1–1.4 (design 3) and `p7d-tether` 1.14 (its note on the branches below), [class-legs.md](../reference-notes/class-legs.md), and the player's art now on the physics line: the coil pack's own mesh (`CoilPack.cs`) and each leg's parts as their own meshes.
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
| Legs | pilot: plastic, 25–35 cm, a little soft | carbon tubes on plastic clamps and T-joints, about 0.18 m to the ground, with **skids**, not point feet | the pilot's length (0.25 m default), T's look and 10 mm section; the feet are skids; built as plastic, PVC-U tube (`p7g-legs`), the material still open (Open points 2) |
| Coil | pilot: about 1 kg | a drum of about 0.3 litres of wound fiber: about 5 km, 0.5 kg full (derived) | the pilot's 1 kg (10 km) in T's shape, scaled up 1.25× (Open points 1) |
| Drone height for gap checks | the sheets' "about 0.55 m tall with legs" | 0.30 m (T); 0.38 m with 0.25 m legs (derived) | 0.36 m on the built D-050 layout with the 1 kg bottle (0.30–0.38 m over 0–2 kg; Dimensions, Overall height) |

## D01 Svinoriz 10 Opto

**Seen in:** T (the legs-on build; every part above). The legs-off build is not in any letter: **R** (the pilot's words in D-043 and the earlier description it supersedes for the legs-on build).

**Layout (D-050, the pilot, 2026-11-01; supersedes D-043's layout):** a simpler drone, **the fiber coil beneath the frame** and **a plain plastic bottle strapped rigidly on top**, the only load, its filled mass the setup's additional weight. No slung payload, no spool on top, no other load or fitting. The frame, arms, motors, props, the two packs and the legs (on or off) stay, and still follow T. Where the coil and the bottle sit, how large the bottle is and how it follows the mass are `drone.json`'s (the physics engineer's); the rows below that give the D-043 positions are superseded by it.
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
| Spool, fiber pack | T: Ø0.066 m pack, 0.12 m wound length, core Ø about 0.032 m (about 5 km); 10 km needs 0.61 litres wound (derived) | **10 km: pack Ø0.083 m full (`drone.json`'s coil part), core Ø0.039 m, wound length 0.15 m**, ribs 7.5 mm apart and 0.6 mm deep (`D01_*.json` `coil`) | derived (T's proportions scaled 1.25× to the pilot's 1 kg); the pack's volume holds 10 km of 0.25 mm line at a square lay within 1.2 % (`p7d-tether` 1.14, derived) |
| Spool, ends | T: front hoop Ø0.072, rear cap Ø0.058 with a 9 mm hole, 0.16 m overall | front hoop Ø0.090 m × 0.015 m, rear cap Ø0.072 m, hole Ø0.011 m, overall 0.20 m: the bobbin inside the canister, hidden by it | measured, scaled 1.25× (derived) |
| Canister | T: none (an open drum on top) | **the class's canister** ([spool-exit-and-rear.md](../reference-notes/spool-exit-and-rear.md)): a body **Ø0.101 × 0.238 m** (`drone.json` `canister` part), a black cone 0.047 m long to a Ø24 mm collar, an aluminium nozzle 0.070 m long, Ø18 mm, a Ø12 mm bore, its mouth flared to Ø26 mm over a 4 mm rounded lip; **0.355 m overall**; a lid seam 0.02 m behind the front face (`D01_*.json` `canister`) | body Ø and overall length published (a retail 10 km canister, Ø101 × 355 mm); cone from the class's measured 39° to its axis (derived); nozzle length and Ø measured (the class: 0.06–0.08 m, 15–20 mm); bore, mouth and lip assumed |
| Canister mount | T: none | two black printed hangers (44 × 20 mm) from the bottom plate's underside down to the body, 0.04 and 0.17 m behind its front face, a cable tie round the body through each; the fiber's connector on the body's top between them, its 3 mm lead up to the plate (`drone.py` `_canister_mount`) | the class's slung canisters (measured: a plate or posts under the frame); sizes assumed |
| Canister position, the line's exit | T: on top (D-043, superseded) | **slung beneath the frame on both builds** (D-050), on the centreline, **tilted 15° rear-down**: the body's centre (0, −0.109, 0.023) m, the coil part (the bobbin) on its axis 0.016 m ahead of it; the body's top 4.4 mm under the packs (with their straps) and its front face's top 0.023 m under the bottom plate. **The nozzle tip, the line's exit, at (0, −0.1701, −0.2050) m, pointing (0, −0.2588, −0.9659): 0.045 m aft of the rear motors' axis, 0.170 m below the arm plane** (0.205 m below the prop plane, outside the rear discs' plan by 0.04 m), 0.083 m above level ground legs-on; the metadata's port `fiber_exit` | the class: tip 0.03–0.08 m aft of the rear motor axis, 0.12–0.20 m below the arm plane, tilt 7–20° (measured, published); this position assumed inside that band, derived from the packs' and plate's fit |
| Packs (2) | T: 0.20–0.28 × 0.045 m side; width not visible | 6S2P each: 0.22 (z) × 0.047 (x) × 0.045 (y) m; inner faces at x ±0.050, centres (±0.0735, −0.043, 0) | measured (length, height, height position); width derived from the cells (Physics) |
| Bottle (the only load) | — (T's slung body is not copied, D-050) | **a plain plastic bottle on the upper plate**, strapped rigidly by two straps through the plate, lying fore-aft over the centre, the cap aft. At 1 kg (`drone.json` payload part): body Ø0.080 m, axis at (0, 0.068, 0), its underside on the upper plate's top at y 0.028; the mass part is the water as a solid Ø0.080 × 0.20 m cylinder (1.0 L), the drawn bottle 0.256 m base to cap (sized to hold it below its cap), the flight's capsule 0.26 m. Drawn: a rounded base (r 0.012), a 0.06 m shoulder to a 25 mm neck with its support ring, a 30 × 15 mm cap (`D01_bottle.json`). Other masses scale it uniformly by (m / 1 kg)^(1/3) about its seat at (0, 0.028, 0): 0.46–1.26 over 0.1–2.0 kg; absent at 0 kg | pilot (D-050); the 1 L PET bottle and its Ø75–85 mm body assumed; size derived (`p5-drone` 1.4) |
| Legs (4) | T: Ø8–10 mm, 0.18 m arm top to ground, at r 0.14 m on the diagonals (0.11–0.15) | **10/8 mm PVC-U tubes** (rigid PVC is sold 10 mm OD × 8 mm ID) at (±0.1025, ·, ±0.1025), r 0.145 m: T's r 0.14 moved out 3.5 mm so the tube clears the pack's outer face at x 0.097. Arm underside (y −0.003) to ground (y −0.253) **0.25 m**: a 0.25 m tube, **0.2135 m free** between the clamp's underside (y −0.0195) and the T-joint's top (y −0.233) (`drone.json` legs, `legs.tube`) | pilot (length, 25–35 cm); measured (OD, position); published (ID); derived (free length, the 3.5 mm move); material: Open points 2 |
| Leg clamps (4) | T: about 25 × 25 × 35 mm black blocks, M4–M5 bolt and wing nut | printed PETG blocks 25 × 16.5 × 25 mm (the height trimmed to stand 1 mm clear of the pack's top), centred at y −0.01125, grip 16.5 mm of tube; **M4** bolt, its wing nut 20 mm across (DIN 315's M4) | measured (plan, nut); derived (height, M4); `legs.clamp` |
| Skids (2) | T: Ø9–10 mm, 0.13 m aft of the rear legs; total length 0.35–0.50 m | the legs' 10/8 mm PVC-U stock at x ±0.1025, 0.45 m long centred on the legs (0.1225 m past each leg), skid axis at y −0.248, 0.005 m above the ground, open ends | measured (diameter, overhang); length assumed symmetric |
| T-joints (4) | T: about 16 mm sockets | printed PETG, 16 × 20 × 30 mm (16 mm socket, 30 mm along the skid), flat-bottomed on the ground, centre y −0.243; an M3 pinch screw holds the skid in the bore (T shows none) | measured (size); assumed (screw); `legs.tjoint` |
| Leg parts as drawn | — | each legs-on corner is four meshes of its own per LOD (16 / 16 / 16 / 12, no T-joint at LOD3), cut where the sim cuts: the **stub** in the clamp, the **leg** below it (the tube cut at the clamp's underside, y −0.0195, its broken end the 10/8 mm bore at LOD0–1), the **T-joint**, and the **skid half** (each skid cut at z 0, midway between its two bores, the lime band with the port rear half). They keep the drone's origin; the player turns, hides or drops each from the sim's pivots (`unity/art/p5-drone--legs-readable`, `drone.py` LEG_KINDS) | derived (the sim's cuts) |
| FPV camera | 19 × 19 × 20 mm micro; 0° uptilt | at the front of the body between the plates, lens at (0, −0.005, +0.115) m | published (size); pilot (uptilt); position assumed (not visible in T) |
| Capacitor | T: Ø about 10 mm can | Ø10 × 20 mm, on the stack's rear | measured (diameter); length assumed |
| Pack leads | T: red and black silicone, 20 mm connectors | 10 AWG (Ø about 4.5 mm with its jacket), 0.10–0.15 m per lead, plugged to the drone's leads at the rear | measured (colour, connector); gauge assumed |
| Overall height | T: 0.30 m | ground (legs-on, y −0.253) or the canister's rear rim (legs-off, y −0.189) to the bottle's top (y +0.108 at 1 kg): **0.36 m legs-on, 0.30 m legs-off**; with no bottle 0.30 m legs-on (the model's top at y +0.0455, the `D01_legs_on` metadata's bounds), with 2 kg 0.38 m (the bottle × 1.26 about its seat) | derived (`drone.json`'s D-050 positions) |

### Construction and materials

- **Frame:** a carbon-fiber X, the CX10 ECO class: four 6 mm arms sandwiched between the bottom plate and the upper plates, black aluminium standoffs, black steel screws; the plates' cut edges show the grey laminate layers (a 0.5 mm lighter strip on every edge).
- **Motors:** black anodised aluminium bells with a ring of vent slots on top (assumed typical of the class), the shaft and a silver M5 nut, the stator's copper showing at the base; their leads run inboard along each arm's top in black sleeving (T).
- **Props:** black glass- or carbon-filled nylon, 2 blades, a 10 × 4.5 to 10 × 5 class (assumed: the thrust data's pitch), mounted on the shaft by the nut.
- **Spool:** inside the canister (hidden): T's bobbin, its light grey rear cap and black front hoop, the blue-grey ribbed fiber pack (T's ribs about 6 mm apart).
- **Canister:** a light grey moulded body (the class: a white body; T's printed grey), a black cone and an aluminium outlet nozzle (published: "aluminum alloy"), its flared mouth the line's lip; two black printed hangers and black cable ties hold it under the bottom plate; the fiber's fixed end leaves the body's top by a black connector to the transceiver (measured on the class).
- **Packs:** 21700 cells (Physics), nickel strip, wrapped in tan insulating paper over heat shrink (T's look), held by two black hook-and-loop straps with a purple edge, plain; red and black silicone leads with yellow connectors.
- **Bottle (D-050):** a plain plastic (PET) bottle and its cap, no label or print; held by two plain black straps through the upper plate. Built as its own variant, `D01_bottle` (layout `bottle`: the bottle and its straps alone, its origin at its seat on the upper plate), so one prefab serves every mass; `D01_legs_on` and `D01_legs_off` draw none (`payload_kg` 0).
- **Legs and skids** (as built, `p7g-legs` 1.1): one stock of smooth **rigid PVC-U tube, 10 × 8 mm**, black (T's look: T read as carbon from its sheen, the pilot says plastic), in **printed PETG** clamp blocks and T-joints; a zinc-plated M4 bolt and 20 mm wing nut at each clamp (hand-tight, the quick release), an M3 pinch screw in each T-joint (assumed); open tube ends on the skids, a lime band on the port skid aft of the rear T-joint. Their material is the open point (Open points 2).
- **Electronics:** a 30.5 mm-pattern flight-controller and ESC stack (Betaflight 4.5 F405 class, fc-profiles.md), the capacitor, the fiber transceiver module (hidden in T: assumed in the stack or at the spool's rear bracket), the micro FPV camera, thin multicoloured signal wires; no antenna.
- **Materials (CC0):**

| Material | Candidate set (CC0) | Source page | Derived textures |
|---|---|---|---|
| carbon plates, arms; legs and skids (drawn in the carbon palette's `tube` colour, T's look, though built as PVC-U) | ambientCG `Fabric004` (a dark carbon-fiber weave; check the weave scale on fetch) | https://ambientcg.com/a/Fabric004 | tint to the photo values, a clear-coat roughness 0.3–0.4, the lighter cut-edge strip on plates |
| motor bells, standoffs | ambientCG `Metal046B` (pinned) | https://ambientcg.com/a/Metal046B | roughness 0.35 anodised; the vent slots as geometry |
| props, clamps, T-joints, cradle, connectors, payload shell, printed spool parts | ambientCG `Plastic006` | https://ambientcg.com/a/Plastic006 | tints per the colour table; a print-layer normal (0.2–0.3 mm lines) on the payload nose and the grey spool parts |
| pack wrap | ambientCG `Paper005` | https://ambientcg.com/a/Paper005 | tan tint, crease noise |
| straps | ambientCG `Fabric030` (pinned) | https://ambientcg.com/a/Fabric030 | black with a purple edge band, no print |
| silicone leads | ambientCG `Rubber004` (pinned) | https://ambientcg.com/a/Rubber004 | red and black tints |
| fiber pack | no set: the `drone_plastic` palette's `fiber` colour (#6c7d8c, smoothness 0.3; the trailing line uses the same) | — | as built the ribs are geometry (7.5 mm pitch, 0.6 mm deep), not a normal map; the pack is its own readable mesh whose radius follows payout (Variants and states) |
| bottle | no set: the `drone_bottle` palette | — | body and cap colours (Colour) at smoothness 0.85 |

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
| canister body | #bcc4c8 (the printed grey) | — | T's printed grey for the class's white body (assumed) |
| canister cone, hangers, ties | #09090c | — | the class's black cones (measured) |
| canister nozzle | #a3a6a8 (the zinc column) | — | bare aluminium (published material; colour assumed) |
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
| bottle body, cap | #b9c7cc, #2f5d9e | — | assumed (`drone_bottle` palette: a clear PET body, a blue cap; no reference shows the bottle) |

The props in the feed: dark arcs (A) and blurred blue-grey blades (B, G), video-feed.md §4, no values. A 2-blade prop passes a blade 2 × rpm / 60 times a second, two thirds of a 3-blade's rate (derived; for task 1.5's blur).

### Variants and states

- **Legs-on:** the coil in its canister beneath, the bottle on top (D-050), four legs and two skids (T).
- **Legs-off:** the leg tubes, T-joints and skids come off at the wing nuts (`drone.json` `legs_off.removed`, 0.1094 kg); the clamps stay on the arms. The canister stays beneath and the bottle on top (D-050); the drone rests by its arms on the stand (D02), so the canister never touches the ground.
- **Bottle:** 0–2.0 kg filled (the setup's additional weight; `drone.json` payload `mass_min_kg` 0 assumed, `mass_max_kg` 2.0 published), drawn by the `D01_bottle` prefab scaled by (m / 1 kg)^(1/3) about its seat, so its water, its collision and the flight's capsule follow the mass together; absent at 0.
- **Coil payout (DRN-05):** the wound pack's radius shrinks from Ø0.083 m to the Ø0.039 m core as the 0.69 kg of fiber leaves; the bobbin (0.08 kg, `drone.json` `coil.spool_mass_kg`) and the canister (0.23 kg, its part) stay. As built (`p7d-tether` 1.14, `CoilPack`/`CoilShape.D01`): the pack's volume falls linearly with the line, **r_p = √(r_c² + (R² − r_c²)·L / L_full)** (R 41.5 mm, r_c 19.5 mm; `drone.py` `coil_radii`, the same law): 32.42 mm at the nominal half, 19.5 mm empty, derived. The model is drawn full; the pack (its rear face, the ribbed winding, the hoop's rear face) is its own **readable mesh per LOD**, `<variant>_LOD<n>_coil_pack` (lathe segments 32 / 20 / 10 / 6), the only part of the model with a CPU copy; at run time every vertex on the pack's outer surface (within the ribs' 0.6 mm, between the rear face and the hoop) moves radially to r_p, so the ribs keep their depth and the faces stay closed, at the line the physics' coil mass carries (stepped every 10 m, `span.coil_rebuild_m`). Without a readable import the pack stays full. The pack now sits inside the canister and is hidden by it; it is still drawn and shrunk so the player's pack, the line's mass and the model agree (the coil part keeps `CoilShape.D01`'s Ø0.083 × 0.20 m, moved and tilted).
- **Leg damage states (`p7g-legs`):** intact, set (bent about the clamp's underside), cracked, broken (a stub left in the clamp) or detached, per leg, with a skid half hanging from the other leg or dropped; drawn from the leg parts above (the player's side is `p7g-legs` 1.5, not built yet).
- **Packs:** 6S2P × 2 (default), 6S3P × 2 or 8S2P × 2 (Physics); the pack box follows the cell count.
- **Props:** 2-blade (T, default) or 3-blade.
- **Frame:** square X (DRN-02, default) or the stretched 338 × 299 reading (Open points 3).

### Damage pattern

A crash ends the flight and is named by the physics with the part and the object (`p6-flight-core` design 7). **The legs bend, crack, break and come off** (D-079, `p7g-legs`; the law under Physics role): a set leg keeps its turn about the clamp, a broken one its stub in the clamp, and a parted or detached piece (the tube below the break with its T-joint, and its skid if that lets go) falls as a loose body and rests on the ground. The props' blade strikes are `p7f-damage`'s, not covered here.

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
| spool (the coil part) | 0.77 kg full, 0.08 kg empty (0.425 kg in `drone.json`, half paid out) | cylinder Φ0.083 × 0.20, the canister's axis | (0, −0.1049, 0.0385), in the canister | pilot (1.0 kg with the canister); derived (the bobbin, the line 10 km × 0.069 kg/km) |
| canister | 0.23 kg | cylinder Φ0.101 × 0.238 (the body; the cone and nozzle lumped in, their 0.045 kg 0.12–0.20 m aft: 1.2 mm of the CoM, about 0.001 kg·m² of pitch and yaw) | (0, −0.109, 0.023), axis (0, 0.2588, 0.9659) | derived (the 1.0 kg less the line and bobbin; bottom-up 0.227 kg) |
| bottle | 0–2.0 kg filled, default 1.0 | the water: cylinder Φ0.080 × 0.20 at 1 kg, axis z, scaled by the mass's cube root about its seat | (0, 0.068, 0) at 1 kg, on the upper plate (D-050) | pilot (D-050); the spec's sweep 0–2.0; size assumed (a 1 L PET bottle) |
| leg tube × 4 | 0.0102 kg | cylinder Φ0.010 × 0.25, axis y | (±0.1025, −0.128, ±0.1025) | derived (10/8 mm PVC-U at 1440 kg/m³, `p7g-legs` 1.1; was 11 g of CFRP) |
| leg clamp × 4 (stay on legs-off) | 0.018 kg | box 0.025 × 0.0165 × 0.025 | (±0.1025, −0.01125, ±0.1025) | assumed (with bolt and wing nut) |
| T-joint × 4 | 0.008 kg | box 0.016 × 0.020 × 0.030 | (±0.1025, −0.243, ±0.1025) | assumed |
| skid × 2 | 0.0183 kg | cylinder Φ0.010 × 0.45, axis z | (±0.1025, −0.248, 0) | derived (10/8 mm PVC-U, `p7g-legs` 1.1; was 20 g of CFRP) |

**Fiber mass (derived):** glass Ø125 µm at 2.2 g/cm³ plus coating to Ø250 µm at about 1.15 g/cm³ (assumed) gives **0.069 kg/km and 0.049 litres/km**; 10 km is 0.69 kg and, at a winding fill of 0.80 (assumed), 0.61 litres. The retail 10 km canister's 0.95 kg (published) leaves 0.25 kg for its shell, so a 0.30 kg housing and cradle (assumed) makes the pilot's 1 kg. T's drum holds about 0.31 litres: about 5 km, 0.5 kg full (derived).

**Superseded by D-050:** the table below is D-043's layout (the spool on top, the payload slung below); `drone.json`'s mass build re-derives it for the coil beneath and the bottle on top (the physics engineer's, D-048 still holds).

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
- **The feet are skids.** Each side's two legs stand on one fore-aft tube, so the drone meets level ground along two lines: the contact points are the skid's ends and the T-joints. As built each half-skid bears on spheres at its overhang end and under its T-joint (Contact surfaces); the foot radius is the tube's, 0.005 m.
- **Compliance, as built** (`drone.json` `legs`, `p7g-legs` 1.1–1.4; the first reading's carbon tube at 6.6 kN/m and the assumed 5–20 kN/m are replaced). The pilot's "a little bit soft" (pilot) on a 10/8 mm PVC-U tube, E 3.0 GPa (3.0–3.3, published), I 290 mm⁴, 0.2135 m free:
  - **Along the leg 52.3 kN/m** (derived, in series: the tube's EA/L 397 kN/m, the arm bending out of its plane under the clamp 67.6 kN/m, the skid's ring in the T-joint 553 kN/m; a push bears on the arm, never on the clamp's grip), damper **103 N·s/m** (ζ 0.2 on a quarter of the 5.08 kg take-off mass, assumed ζ). At L5 each leg carries 12.7 N (derived).
  - **Across the leg at the foot 268 N/m** (the tube's 3EI/L³; 262 N/m with the jaws' turn, 12.3 kN/m at the foot), in series with the ground's stick anchor; its damping the tube's own loss factor **η 0.03** (published, rigid PVC), ζ = η/2, with the jaws' Coulomb friction as the other source (never a tuned ζ). The axial load softens it, k_t·(1 − P/P_sway), **P_sway = π²EI/(4L²) = 47.06 N** (derived, fixed–free).
  - **The lever** (design 3, 1.4): past small deflections the leg is the pseudo-rigid-body link (Howell and Midha 1995, published), **γL = π²L/12 = 175.6 mm** on a torsional spring K = k_t·(γL)², derived so that it keeps the 268 N/m and the 47.06 N exactly (γ 0.822, K_Θ 2.467); a leaning foot rises on the link's circle, and past the sway load the leg vaults the body over a held foot.
  - **The sway** after a cut landing on soil in a 5 m/s crosswind: 2.04 Hz, decaying in about 3.9 s (`p7g-legs` 1.3's merge note, model 46, before the lever). Mass build at L5 5.080 kg; the rigid roll tip 23.33°, 14.40° quasi-static on the soft legs (the feet 41.5 mm in).
- **The failure law** (`p7g-legs` design 3, `LegDamage.cs`; the bending capacities at the clamp's underside, all nominal, derived from `legs.tube`/`clamp`/`tjoint`):

| Mode | Capacity | Law |
|---|---|---|
| crack (the edge's first yield) | 2.13 N·m | σ_y·I/(c·K_t), less the axial share; halves the tube's bending stiffness (factor 0.5, assumed) |
| set (yield) | 3.19 N·m (14.9 N across the foot), full hinge 4.47 N·m | σ_y·I/c, then the moment–curvature law; the set angle turns the tube, T-joint and skid half about the clamp |
| parting | about 2.98 N·m on a cracked edge (the set then about 6°) | by strain: K_t·κ·c past the elongation at break, never by a brittle stress |
| buckling | 385 N along the leg | clamped–pinned Euler, 20.19·EI/L²; the bow takes the excess and sets past its own hinge |
| clamp slip, twist | 576 N of pull; 2.88 N·m of twist | preload P = T/(K·d) on the M4 bolt; slip at 2μP (past the 16.5 mm grip the leg is off), twist at μ·P·D |
| T-joint | skid slip 500 N; socket 6.13 N·m | the M3 screw's preload by the same friction law; the socket's PETG interlayer strength |

- **The strengths are drawn once a flight per leg** (`leg.strength.<leg>`, 8 draws, uniform within the labelled bounds, logged): σ_y 50–58 MPa (55, published), elongation at break 0.15–0.20 (0.20, published), edge factor K_t 1.2–2.0 (1.5, assumed), the wing nut's hand torque 0.54–0.995 N·m (0.768, published pinch torque), nut factor 0.16–0.30 (0.20, published), μ 0.2–0.4 (0.3, assumed), the T-joint screw's torque 0.3–0.7 N·m (0.5, assumed), PETG interlayer 14–22 MPa (18, published). The modulus is not drawn (`p7a-ground`'s `leg.stiffness`).
- **What it gives** (`flightbench --legs`, 40 seeds a cell, 1.4): the pilot's normal landing (level, 0.5 m/s down) leaves every leg intact and upright; a drift from 0.30 m/s (first seed) or 0.35 m/s (half the seeds) damages the leading pair; over the grid (0.5–1.5 m/s down, 0–20° roll, 0–0.6 m/s drift) 3.4 % of flights go over, 32.5 % end down, 64.4 % with a damaged leg; no leg comes off in a landing (the pull needs a hook). The tube's yield moves the shares most; the soft legs tip less than D-079's "a bit more often" (the section decides, nothing is tuned: `p7g-legs` 1.4, for QA).
- **The skid is a portal in pitch** (class-legs.md §2, derived): two legs on one skid with rigid T-joints cannot sway fore-aft as free cantilevers, so fore-aft they sway clamped–guided, **12EI/L³ = 1073 N/m and 188 N** for the PVC-U tube, 4 × the sideways 268 N/m and 47 N. Design 3 uses one value for both planes; class-legs.md recommends modelling the portal before tuning any value (Open points 2).

**Collision (gallery noclip only; the flight's shapes come from `drone.json` through the core):** a box per plate stack, a box per arm, a cylinder per motor, a thin disc per prop (r 0.127 m, 0.01 m thick), a cylinder for the canister's body, three for its cone, one each for its nozzle and mouth, a box per hanger, cylinders for the bottle, a box per pack, a capsule per leg tube and skid. Each within 1 mm of the visual on the parts the spec measures; the canister's within 2 mm of its bounds. The flight's (`drone.json` `contacts`, the canister part): `coil` (the body's capsule, its flat ends' rims 21 mm inside it), `coil_aft` and `coil_fore` (the load-bearing spheres), `coil_cone` (within 5.5 mm of the cone) and `coil_nozzle` (its aft end the nozzle's tip, within 4 mm of the tube).

**Material ids:** **`carbon_composite`** for the arms and plates, signed off by physics (`p7b-airframe`). In the flight's contact shapes (`drone.json` `contacts`) the legs, skids, packs, bottle and coil are `polymer` and the motors `aluminium`. In the model's gallery collision (`drone.py` PHYSICS by paint slot) the carbon slot is `carbon_composite`, which still covers the legs and skids drawn in its `tube` colour; props, clamps, T-joints, spool, canister, bottle, pack wrap: `plastic`; motors and the canister's nozzle: `steel` (the table has no aluminium); leads: `cable`; straps: `fabric`.

**Gaps:** the drone is the reference every other sheet measures against: **0.574 m nose-first and 0.706 m on the diagonal** (DRN-02), footprint `w(θ) = 0.254 + 0.32·(|cos θ| + |sin θ|)` m at yaw θ (houses-and-yard sheet); every part (skids 0.45 m long at x ±0.1025, packs at x ±0.097 outer, the coil and the bottle on the centreline, the 2 kg bottle 0.32 m long) lies inside the prop envelope. **Height:** 0.36 m legs-on with the 1 kg bottle (0.30–0.38 m over 0–2 kg, derived from `drone.json`); the sheets that assumed 0.55 m are stricter than the drone.

**Wind (for the body drag areas of task 1.2, derived, ±30 %):** frontal (along z) about 0.06 m², 0.024 m² of it the crosswise payload's side; side (along x) about 0.056 m², 0.017 m² of it the spool's side; top about 0.07 m² without the prop discs.

**Snag hazards (against wires and the drone's own fiber):** the props (thin wire wraps them, PR-6); the skids' open ends (`hook`); the legs and skids as a frame a wire can ride into; the canister's cable-tie tails. **The drone's own line** leaves the canister's nozzle tip, 0.205 m below the props' plane and 0.045 m aft of the rear hub line, pointing aft and 15° down, on both builds (D-050; [spool-exit-and-rear.md](../reference-notes/spool-exit-and-rear.md)). `tether.json`'s `spool.exit_position_m` still names the old rear cap (the physics engineer's).

**Contact surfaces:** the skids on the ground (legs-on): as built each skid is a capsule split at its middle, a half to each corner's leg, with load-bearing spheres of the tube's 5 mm radius at its overhang end (z ±0.22) and under its T-joint (z ±0.1025), y −0.248 (`drone.json` `contacts`); the arms' undersides on the stand's four posts (legs-off, D02).

### Placement rules (for the world)

- At the start point: legs-on on the ground, nose to the pilot's chosen heading; legs-off on D02.
- The gallery station (task 1.6) was dropped (D-047: the test range is the final map, G5 folds into G7); T's recreated view stays the REAL-03 check.

### Generator parameters (`drone`, task 1.3)

As built (`tools/blender/generators/drone.py` PARAMS; the variants `D01_legs_on`, `D01_legs_off`, `D01_bottle`): **every dimension and position comes from `drone.json`** (DRN-03), so the leg length, skid length, tube diameter, packs, frame, prop count and spool size are the spec's, not params. The params hold only the look:

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `spec` | path | a component spec under `assets-src` | `assets-src/drone/drone.json` | every dimension |
| `layout` | enum | legs_on, legs_off, bottle | legs_on | legs_off: the spec's `legs_off` build (legs, T-joints and skids off); bottle: the bottle and its straps alone, its origin at its seat on the upper plate (D-050) |
| `arm.thickness_m`, `arm.width_m`, `arm.mount_width_m` | m | 0.005–0.008, 0.020–0.032, 0.028–0.040 | 0.006, 0.025, 0.032 | arm plate and its round motor mount |
| `plates.bottom_m`, `top_m`, `upper_m`, `corner_r_m` | m | 0.002–0.004, 0.0015–0.004, 0.002–0.004, 0–0.02 | 0.003, 0.002, 0.003, 0.010 | plate gauges and corners |
| `motor.base_d_m`, `motor.base_h_m` | m | 0.025–0.037, 0.003–0.010 | 0.034, 0.006 | the motor's narrower base (T) |
| `prop.hub_d_m`, `hub_h_m`, `chord_m`, `tip_chord_m`, `sweep_m`, `thickness_m` | m | 0.015–0.025, 0.005–0.010, 0.022–0.030, 0.004–0.012, 0–0.02, 0.002–0.006 | 0.019, 0.008, 0.026, 0.008, 0.012, 0.004 | the blades' planform (the count is the spec's `props.blades`) |
| `prop.rest_deg.<corner>` | deg | 0–360 | the variants: rr 178, fr 13, rl 132, fl 6 (T's, fitted) | blade angle at rest |
| `coil.wound_m`, `hoop_len_m`, `hoop_d_m`, `cap_d_m`, `core_d_m`, `hole_d_m` | m | 0.08–0.18, 0.005–0.03, 0.06–0.11, 0.04–0.09, 0.02–0.06, 0.005–0.02 | 0.15, 0.015, 0.090, 0.072, 0.039, 0.011 | the spool's stations; the pack's full diameter is the spec's coil part |
| `coil.rib_pitch_m`, `coil.rib_depth_m` | m | 0.003–0.012, 0–0.002 | 0.0075, 0.0006 | the winding's ribs (T's 6 mm at 1.25×) |
| `canister.cone_m`, `collar_d_m`, `nozzle_m`, `nozzle_d_m`, `bore_d_m`, `lip_r_m`, `mouth_d_m` | m | 0.02–0.09, 0.015–0.04, 0.04–0.10, 0.010–0.025, 0.004–0.020, 0.001–0.010, 0.012–0.040 | 0.047, 0.024, 0.070, 0.018, 0.012, 0.004, 0.026 | the cone and the nozzle aft of the spec's body (the flight's `coil_cone` and `coil_nozzle` follow them, `test_drone`) |
| `canister.lid_m`, `hanger_front_m`, `hanger_rear_m`, `hanger_len_m` | m | 0.005–0.05, 0–0.3, 0–0.3, 0.01–0.04 | 0.020, 0.040, 0.170, 0.020 | the lid's seam and the hangers' stations from the body's front face |
| `payout` | 0–1 | 0–1 | 0 | the pack's radius at build (the variants are drawn full; the player moves it, `CoilPack`) |
| `payload_kg` | kg | 0–5.0 | 1.0 (0 on the legs variants, 1.0 on `D01_bottle`) | 0 leaves the bottle off; else the spec's bottle scaled by the cube root of the mass ratio |
| `bottle.neck_d_m`, `cap_d_m`, `cap_h_m`, `shoulder_m`, `base_r_m` | m | 0.018–0.040, 0.020–0.045, 0.008–0.025, 0.02–0.12, 0.002–0.03 | 0.025, 0.030, 0.015, 0.06, 0.012 | the bottle's shape (D-050) |
| `pack.corner_r_m` | m | 0–0.0107 | 0.008 | the packs' long edges |
| `straps` | int | 2–3 | 2 | straps per pack |
| `skid_band` | bool | — | true | the lime band on the port skid (T) |
| `camera.lens_y_m`, `camera.lens_z_m` | m | 0–0.03, 0.09–0.14 | 0.015, 0.115 | the FPV lens's height and front |
| `lod.lod1_switch_m`, `lod2_switch_m`, `lod3_switch_m`, `cull_m` | m | — | 3, 10, 35, 330 (`D01_bottle` culled at 120) | LOD switches by the feed's pixel |
| `seed` | int | — | 1 | the cable ties' turn |

The first proposal's `leads` and `wear` are not params: the leads are drawn plugged, and nothing is worn.

### Open points

1. **T's absolute size.** Scaled as a 10-inch, T's spool holds about 5 km and its legs stand 0.18 m; the pilot's 1 kg coil and 25–35 cm legs fit T's proportions on a frame about 1.3× larger (a 13-inch class), so T may show a bigger airframe than the Svinoriz. The Svinoriz keeps its 10-inch geometry (DRN-02) and the pilot's figures, in T's shapes: the spool scaled 1.25×, the legs at 0.25 m. G7 judges the look (G5 folded into it, D-047).
2. **Legs: the section is settled, the material is not.** T shows black tubes on plastic clamps and skid feet; the pilot said plastic legs. As built (`p7g-legs` 1.1) the tube is the pilot's plastic, **10/8 mm PVC-U**, in T's shape. [class-legs.md](../reference-notes/class-legs.md) finds **10 mm OD right** (T's 8–10 mm against its 25 mm block and 16 mm socket, and the class's sold 10 mm leg kits, published) but the **material unsettled**: 10/8 mm PVC-U is the softest leg 10 mm allows (268 N/m, sway-buckling at 47 N, 3.7 × its L5 share at 0.2135 m free, 1.7 × at the pilot's 35 cm), firm at 25 cm and floppy rather than "a little soft" at 35 cm; the leg most consistent with T, the pilot's "plastic" and a leg that stands firm yet fails on a bad landing is a **glass-fibre (GFRP) tube, 10 mm OD** (about 2.4 kN/m, 424 N sway buckling, a splintering fracture near 80 N across the foot; assumed: no public source names the class's material). The fore-aft portal (Physics role) is to be modelled before any value is tuned. **Gate question** (qualitative, the pilot's own kit, which no public source shows): were your legs fibreglass rod or tube, or plain plastic pipe; and did they ever sag or wobble on the ground before take-off?
3. **Square or stretched X:** T fits both (1.9 px and 1.25 px rms); the CX10's published 338 × 299 mm would make the drone 0.553 m nose-first. DRN-02 stays 0.574 m; changing it is the orchestrator's (it sizes PR-9's cellar door).
4. **Which way T faces** is derived, not seen (the camera is hidden). If the pilot's drones carry the payload fore-aft and the spool crosswise, swap the two axes; the masses do not change, roll and pitch swap their payload terms.
5. **The legs-off layout** is settled by D-050: the coil beneath and the bottle on top on both builds.
6. **The canister's look** is the class's ([spool-exit-and-rear.md](../reference-notes/spool-exit-and-rear.md)), not T's: its colours (a light grey body, a black cone, a bare nozzle) and whether the pilot's canisters are opaque, which hides the pack, are G7's question for the pilot's eye.
7. **Material id** `carbon_composite` is signed off for the arms and plates (`p7b-airframe`); the gallery collision still tags the PVC-U legs and skids with it (Physics role), where the flight's contacts use `polymer`.

## D02 Launch stand

**Seen in:** R (the pilot's description, D-022: "like a chair upside down, so each chair leg is beneath each motor"; D-043).
**Research sources:** https://en.wikipedia.org/wiki/Steel (density, for the mass).
**As built** (`p5-drone` 1.4): `tools/blender/generators/launch_stand.py`, variant `D02_launch_stand`, draws the physics' runtime stand (`LaunchStand.Build` at foot 0, yaw 0) from `drone.json`'s `stand` and `motors`, so the drawn stand is the flown one; review sheets in `review-sheets/D02/`.

### Dimensions

| Part | Range | Chosen default | Label |
|---|---|---|---|
| Post height (ground to the post top) | about 0.25 m | 0.250 m | pilot (D-022) |
| Post positions | on the motor diagonals, under each motor | r **0.200 m** (x, z ±0.1414): the contact just inboard of the motor can (0.226 − 0.0186 − 0.0074 m), under the motor in plan; the sides along body x and z (`stand.post_radius_m`) | derived from D01 |
| Post section | a chair's leg: Ø22–25 × 1.5 mm round or 20 × 20 × 1.5 mm square tube | 20 × 20 × 1.5 mm square tube (`stand.post_diameter_m` 0.020, its width), cold-formed corners r 0.003 m (`D02_launch_stand.json`) | assumed |
| Post top | a chair-foot cap, Ø or □ 22–28 × 10–15 mm | **as built no cap**: the tube's top closed by a welded end plate, steel to the top where the arm rests (the physics' posts are steel; the sheet's assumed plastic cap is not drawn) | as built |
| Base | an X of two bars crossing at the centre, on the ground | 20 × 20 × 1.5 mm tube, one bar along each diagonal, each **0.42 m** (2 × (r 0.200 + 0.010), so its ends run into the posts' feet) | derived (D-022's X base; `launch_stand.py`) |
| Base height | 0.02 m | 0.020 m | derived (the tube) |
| Mass | 1.2–3.0 kg | **1.6 kg** | derived: about 1.8 m of tube at 0.871 kg/m (111 mm² × 7850 kg/m³) plus welds (`stand.mass_kg`) |

**Fit to D01 (derived; checked by `test_launch_stand.py`):** the arms rest on the four post tops with their undersides at 0.250 m (0 mm gap), the arm-plane centre at 0.253 m (`RestingOrigin`), each post under its arm's strip, no drone vertex inside the steel; the legs-off canister's lowest point (its body's rear rim) is 0.064 m above the ground, **0.044 m clear of the base bars** (the spec asks ≥ 0.020 m). The leg clamps (r 0.145 m) sit inboard of the posts (r 0.20 m) and clear them.

### Construction and materials

- Steel tube, welded; black satin paint worn to bare steel at the edges and on the post tops where the arms seat (assumed: a household chair frame).
- **Materials, as built:** the `stand_steel` palette (paint and worn colours, smoothness 0.45), no texture set; the worn pieces are 3 cm lengths of the tubes' edges, 30 % of them chosen by the seed, and every post top. The first proposal's candidates (ambientCG `PaintedMetal004`, https://ambientcg.com/a/PaintedMetal004, `Rust007` in the worn spots, `Plastic006` for caps) are not used.
- **LODs:** 2088 / 168 / 72 triangles (rounded corners, chamfered, plain boxes) at 0 / 3 / 10 m, culled with the drone at 330 m.

### Colour

| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| paint | #1e1f21 | — | assumed (`stand_steel`) |
| worn edges and post tops | #6e6a66 | — | assumed (bare steel, `stand_steel`) |

### Variants and states

One. A worn-paint dial only.

### Damage pattern

None.

### Physics role

- **Collision:** the physics' six boxes exactly, four posts and two base bars (the drawn tube's 3 mm corners lie inside them: 1.35 mm box to mesh, 0.95 mm mesh to box). Unanchored: physics computes tipping and dragging (D-022); the metadata carries `mass_kg` 1.6, an optional key for a loose runtime object.
- **Material ids:** `steel` for all of it (`stand.material`).
- **Gaps, wind volumes:** none. **Snags:** the two base bars as edge snags (a dragged line or skid trips there).
- **Landable:** the four post tops (the drone's arms).
- **Mass:** 1.6 kg, CoM 0.07 m above the ground (derived).

### Placement rules (for the world)

At the start point when the legs are off, the drone on top. The gallery's drone station was dropped (D-047: the test range is the final map).

### Generator parameters (`launch_stand`, task 1.4)

As built (`launch_stand.py` PARAMS): **every dimension is `drone.json`'s `stand` and `motors`** (`post_radius_m` 0.200 derived, `post_height_m` 0.25 pilot, `post_diameter_m` 0.020 assumed, `mass_kg` 1.6 derived, `material` steel); the params hold only the look:

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `spec` | path | a component spec under `assets-src` | `assets-src/drone/drone.json` | every dimension: its stand and motors |
| `corner_r_m` | m | 0–0.005 | 0.003 | the square tube's outer corner radius (cold-formed, about twice its 1.5 mm wall) |
| `wear` | 0–1 | 0–1 | 0.3 | the share of the tubes' edge pieces worn to bare steel |
| `wear_piece_m` | m | 0.01–0.2 | 0.03 | an edge piece that wears as one |
| `lod.lod1_switch_m`, `lod2_switch_m`, `cull_m` | m | — | 3, 10, 330 | LOD switches, culled with the drone |
| `seed` | int | — | 1 | which edge pieces are worn |

### Open points

- The stand is a pattern, not a letter; G7 judges its look with the drone on it (G5 folded into it, D-047).
