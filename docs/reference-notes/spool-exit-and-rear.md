# Spool exit and the airframe's rear (p7d-tether)

Where the class's heavy 10-inch fiber-optic quads let the line out, and what sits between and behind the rear rotors. Written because the sim cuts the line on the rear rotors' inboard tips on ordinary landings and some liftoffs. The pilot says a normal liftoff or landing does not cut it.

Sources: letter T (one still, read through its registered frame and zoom crops), and public shop listings with photos of the class's canisters and of complete 10-inch fiber-optic quads. Labels: **pilot**, **measured** (T, or a public photo, scaled as stated), **published** (a cited source), **derived** (computed here), **assumed**.

Body axes as in `drone.json` (D-019): x starboard, y up, z forward, origin at the arm-plane centre. The sim's frame for comparison (`drone.json`): rear hubs at (±0.160, +0.035, −0.160), 0.254 m props, so the rear discs' inboard tips are at x ±0.033 and the gap between them is 0.066 m wide on the hub line. The sim's exit is the coil's rear cap centre, (0, −0.053, −0.100), pointing straight aft.

## 1. Answer in short

- **The class slings a long canister under the frame, and its exit is a nozzle at the canister's rear apex, below the rotors and at or behind the rear hub line.** Both complete 10-inch builds found carry it that way [1][2]. Nothing in either build sits in the gap between the rear discs at rotor height. The line never has to cross that gap: it starts 0.12–0.20 m below the arm plane and leads aft and down.
- **The exit points aft and down, not level.** The canister hangs tilted rear-down: about 7° on one build and 13–18° on the other (measured), and 20° on a retail reel's spec (published [3]). The tilt is there so that the nozzle points straight aft when the quad leans forward in cruise [4].
- **The canister is a cylinder, a cone and a tube.** The tube is a straight guide on the axis, 0.06–0.08 m long and about 15–20 mm across, with a flared mouth (measured [1][2][3]). Listings call it an aluminium outlet nozzle [5]. The fiber's fixed end leaves the canister's side through a connector to the drone's transceiver (measured [2][3]).
- **T shows none of this.** It is a different, older layout: a bare wound drum on top, no line threaded, and no nozzle or guide fitted (§3).

## 2. The class, from public photos

### 2.1 Canisters (published, and measured from listing photos)

| Item | Value | Label | Source |
|---|---|---|---|
| 10 km canister, outer size | Ø101 × 355 mm, 0.95 kg filled; outlet nozzle "aluminum alloy" | published | [5] |
| 10 km reel with bracket | 320 × 199 mm (H × D, the D includes the wire bracket), 1.2 kg loaded, 0.47 kg empty; 0.25 mm G657A2 fiber; bend limit 7.5 mm; **spool tilt angle 20°** | published | [3] |
| 10 km reel | 370 × 120 mm, 1.1 kg, 0.25 mm fiber | published | [6] |
| 10 km enclosed (inner-wound) capsule | 330 × 100 mm (5 km 290 × 90; 20 km 370 × 125); 0.26 mm fiber | published | [7] |
| Shape of [3] | a cylinder body about 180 mm long and Ø about 135 mm, a black cone about 70 mm tall, and a straight pale tube on the axis about 70 mm long and Ø about 20 mm. The fiber's fixed end leaves through a connector at the body's base | measured (scaled to its published 320 mm height, ±10 %) | [3] |
| Universal printed spool mount, 5–10" | zip-tied posts; spools up to Ø118 mm; the front post fixed at a 25° tilt, the rear post hinged | published | [8] |

### 2.2 Two complete 10-inch fiber-optic quads

Scale: neither listing gives the frame. The photos are scaled to a **0.32 m fore-aft motor spacing** (assumed: the sim's square X on the 452 mm wheelbase), ±15–20 % for the perspective.

| | Build A [1] (a near side view, plus rear and three-quarter views) | Build B [2] (an oblique side view, plus end views) |
|---|---|---|
| Canister | slung under the frame between four tall printed legs, axis fore-aft on the centreline. Its body runs from about 0.05 m behind the front motor axis to about 0.11 m aft of centre, Ø about 0.11 m. Then a short cone and an olive tube nozzle with a knob-like tip; total length about 0.33 m (measured) | hung from a black plate under the frame's centre, its front end near the centre. A white body with a black cone and a short black nozzle with a flared mouth; the cone sits at the rear legs and the nozzle passes between them (measured) |
| Tilt | rear-down, about **7°** (measured) | rear-down, **13–18°** (measured; oblique view) |
| Nozzle tip | about **0.055 m aft of the rear motor axis**, 0.12 m below the arm plane (**0.155 m below the prop plane**), about 0.085 m above the ground, centred between the rear legs (measured) | about **0.03–0.08 m aft of the rear motor axis**, **0.15–0.20 m below the arm plane**, about 0.12 m above the ground (measured) |
| Between the rear motors at rotor height | only the frame's rear plates and arms; the pack rides on top, strapped across the centre, its leads going aft. Nothing reaches aft over the gap (measured) | the stack and wiring between the plates; nothing over or behind the gap (measured) |
| Behind the rear motors | only the nozzle tube, low and centred, pointing aft-down | the cone and nozzle, low and centred, between and below the rear legs |

So on both builds the exit sits **outside the rear discs' plan** (horizontal distance from a rear hub to the tip about 0.17 m against a 0.127 m blade radius: about 0.04 m clear in plan, derived), and **0.15–0.23 m below the prop plane** (derived). The sim's exit is in the same plan position (0.044 m clear) but only **0.088 m below the prop plane**, **0.06 m forward** of the hub line, and level (derived from `drone.json`).

### 2.3 Why the tilt (published, then derived)

- A public post on one such build: the spool hangs at an angle to the body, because the quad flies leaning forward, and the angle reduces tangling [4] (a second-hand social media claim, not a spec).
- Leaning forward raises the tail. A canister tilted rear-down by the cruise pitch therefore points its nozzle straight aft, level, in cruise (derived). The 20° spec [3] and the 25° post of a printed mount [8] sit in the class's usual cruise pitch range (assumed).
- On the ground the same tilt points the nozzle aft and down at the ground behind the drone, from 0.08–0.12 m up (measured). At rest the line runs from the nozzle straight onto the ground behind, never up toward the rotors.

## 3. Letter T: what it shows and what it doesn't

**Shows** (measured, from the drone sheet's 452 mm scale; `docs/object-sheets/drone.md`):
- The spool is an **open, outer-wound drum on top** of the frame, fore-aft, Ø about 66 mm and about 0.16 m long overall. Its middle is about 0.04 m forward of centre, so its rear end is about 0.04 m aft of centre.
- Its rear end is a light grey printed cap, Ø about 58 mm, with a recessed three-spoke hub and a **central bore about 9 mm across** on the axis. The cap's foot is part of a rear bracket down to the plate. The front end is a black hoop, and two black cable-tie hoops hold the drum on a black cradle.
- Between the rear motors at arm level: the stack's standoffs, a capacitor, red and black leads and loose signal wires, and the packs' rear ends with leads to yellow connectors hanging below. Nothing reaches aft over the gap between the rear discs, at any height.

**Doesn't show:**
- **No line is threaded**, and no tube, eyelet or guide is fitted. The 9 mm bore is the only feature that could take one (assumed). So T gives no exit direction. The sheet's "axially aft from the rear cap's centre" is an assumption.
- If T's line did leave axially from that bore, the exit would sit about **0.044 m above the prop plane and 0.12 m forward of the rear hub line** (derived). That is the opposite of the class builds in §2.2, and T's layout is the one D-050 superseded (the pilot moved the coil beneath the frame).
- T is a still: no flight, no lead of the line in the air.

## 4. The line's lead from the exit

- **On the ground and at liftoff** (measured [1][2]): from a nozzle 0.08–0.12 m above the ground, aft and down onto the ground behind the drone. The span starts below and behind the rear discs.
- **In flight** (published [4], derived): the line pays out by the drone's own motion and trails aft. At cruise pitch the nozzle points level aft. No public source gives a measured lead angle in the air.
- **Bend at the mouth:** the reel's fiber is rated to a 7.5 mm bend [3]. A flared nozzle mouth (B, and the knob-like tip of A) gives the line a rounded lip to turn over when it leads off-axis (measured shape; radius not measurable at this resolution).

## 5. For the sim (derived from the above; the physics engineer's call)

The candidates in the brief, against the evidence:

| Candidate | Evidence |
|---|---|
| A coil or spool mount, payload rail or guide reaching aft **over** the rear gap | **not seen** on either build or in T |
| A nozzle or guide reaching aft **below** the rear gap | **seen on both builds**: tip 0.03–0.08 m aft of the rear motor axis, 0.12–0.20 m below the arm plane |
| An exit pointing other than straight aft | **seen**: aft and **down 7–20°** in the body frame (level in cruise) |

The sim's coil is T's drum scaled 1.25× (Ø0.083 × 0.20 m). The published 10 km canisters are longer and wider: Ø0.10–0.135 m, 0.32–0.37 m including the cone and nozzle (published [3][5][6][7]). Their length is what puts the nozzle tip at or past the rear hub line.

## 6. Not found

- No builder's text on the rear rotor gap, on fiber cut by the rear props at liftoff or landing, or on a dedicated line guide. Searched in English, Ukrainian and Russian: shop listings, printable mounts and general articles.
- No published nozzle dimensions or mouth radius.
- No measured lead angle of the line in flight.

## References

1. https://flasharmy.com.ua/en/optic-fiber-drone-10-inch-20-km-spool (shop listing, 7 photos of a 10-inch quad with a 20 km canister)
2. https://flasharmy.com.ua/en/fpv-dron-zhakh-10-na-optovolokni-z-kotushkoiu (shop listing, 8 photos of a 10-inch quad with its reel)
3. https://flasharmy.com.ua/en/optovolokonna-kotushkadlya-vidstani-10-km (10 km reel: specs and 3 photos)
4. https://x.com/GrandpaRoy2/status/1856756238459310555 (social post on a tilted spool; second-hand)
5. https://www.getfpv.com/fiber-optic-canister-10km.html (10 km canister; via search summary, the page blocks automated reads)
6. https://flasharmy.com.ua/en/kotushka-optovolokna-10km-astralexpress (10 km reel specs)
7. https://www.zion-communication.com/Inner-Winding-Capsule-FPV-Drone-Fiber-Optic-Spool-Long-Range-UAV-Optical-Tether-System-pd566419048.html (capsule sizes)
8. https://www.cgtrader.com/3d-print-models/hobby-diy/robotics/fiber-optic-spool-retainer-for-fpv-v2 (printable mount; via search summary, the page is gone)
