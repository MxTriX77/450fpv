# Fore-aft balance of the class's heavy 10-inch fiber-optic quads (p5-drone)

How the class places its slung canister and its packs fore and aft, how builders set the centre of gravity (CG), and what tolerance they aim for. Written because D01's centre of mass sits 2–9 mm ahead of the hubs' centre across the loadouts. The front motors then carry 1–6 % over the mean, and a hard arrest saturates the front ESCs.

Sources: letter T (one still, read through its registered frame and a projected grid), the two complete 10-inch builds already measured in [spool-exit-and-rear.md](spool-exit-and-rear.md) §2.2 (Builds A and B), public CG-balancing guides and forum threads, and `drone.json` on the build line (model 64). Labels: **pilot**, **measured** (T or a public photo, scaled as stated), **published** (a cited source), **derived** (computed here), **assumed**.

Body axes as in `drone.json` (D-019): x starboard, y up, z forward, origin at the arm-plane centre, which is the hubs' centre (hubs at z ±0.15981 m).

## 1. Answer in short

- **Builders balance on the hubs' centre, and the tolerance they quote is about ±1 cm**, tighter on heavy quads near their thrust limit (published [1][2]). No public source gives a figure for the class itself.
- **The class's canister body sits under the hubs' centre, or aft of it, never forward.** On Build A it runs from about 0.11 m ahead of the centre to 0.11 m behind it, so it is centred to within about 0.02 m (measured). On Build B it hangs from the centre aft (measured, coarse). D01's canister body is centred 0.023 m forward of the hubs' centre and its coil 0.038 m forward. **That coil is the whole offset.**
- **The coil is the one mass that changes in flight.** Wherever it sits off the CG, the CG walks as the line pays out: D01's walks 3.6 mm aft over a full spool's payout. Aircraft designers keep the fuel inside the CG range for that reason [5]. A pack or payload move can zero only one spool state. A canister move zeroes them all.
- **Recommended (derived): slide the canister, with its coil, 0.035 m aft along z.** The CG then stays within 0.6 mm of the hubs' centre at every payload (0–2 kg) and every spool fill. The nozzle tip moves to 0.080 m aft of the rear motors' axis, the aft edge of the class's measured 0.03–0.08 m band.
- **T doesn't contradict this, but it can't decide it.** T's packs sit about 0.04 m forward and its slung payload about 0.03 m aft (measured, ±0.03 m), and its spool is on top. That is a different, superseded layout (D-050), and T's masses are unknown (§4).

## 2. What builders do (published)

| Point | What the sources say | Label | Source |
|---|---|---|---|
| Target | the CG on the frame's centre; balance the quad on two supports under the arms, slide the pack in its strap until it sits level, then check a hands-off hover at about 1 m | published | [1] |
| Tolerance | a battery about 1 cm out of place is "not noticeable", but it limits top performance; the CG only needs to be "somewhat near the center" on light, high thrust-to-weight minis | published (forum) | [2] |
| Heavy quads | the CG is "a lot more critical" with a heavy pack, a large build and motors running near full throttle | published (forum) | [2] |
| Who copes | the flight controller handles an offset, but the motors and ESCs carry it; better if they don't have to correct for it in the hover | published (forum) | [2] |
| Tuning | with an off-centre CG one axis responds differently forward and back, so it can't be tuned perfectly | published (forum) | [2] |
| How it's read in logs | front-heavy shows as the front motors' outputs sitting above the rears' in a steady hover; the fix is to move the battery or other parts until the outputs match, and a small imbalance is acceptable while there's headroom | published (forum) | [3] |
| Flight controller placement | "within a few centimeters" of the middle, which is not a CG tolerance | published | [4] |
| Fuel that burns | keep all the fuel inside the allowable CG range, and check the CG from empty to full | published (aircraft design) | [5] |

No public source on the class itself was found: no builder's text on CG with a fiber canister, no listing that gives pack or canister positions, no mention of balance (searched in English, Ukrainian and Russian; §6). What the class's photos show is in §3.

## 3. What the class's builds show (measured)

From [spool-exit-and-rear.md](spool-exit-and-rear.md) §2.2, scaled to a 0.32 m fore-aft motor spacing (assumed), ±15–20 %:

| | Build A | Build B | D01 (`drone.json`) |
|---|---|---|---|
| Canister body, fore-aft | from about +0.11 to −0.11 m, **centre about 0 (±0.02)** | its front end near the centre, the cone at the rear legs: **centre aft of the hubs' centre**. The note's readings don't close: a reel of B's published 0.32 m length with its tip 0.03–0.08 m behind the rear motors would put its front at about +0.10 m. So B's centre is somewhere between about −0.06 and +0.01 m | +0.138 to −0.092 m, **centre +0.023 m**; the coil (bobbin and line) **+0.038 m** |
| Tilt | 7° rear-down | 13–18° | 15° |
| Nozzle tip aft of the rear motors' axis | about 0.055 m | 0.03–0.08 m | 0.045 m |
| Pack | one, on top, strapped across the centre | not readable | two, along the sides, centred at z 0 |

So both builds hang the canister from the centre aft. Neither puts its body ahead of the centre, as D01 does (measured, coarse).

## 4. Letter T: what it shows and what it doesn't

T's camera pose was refitted to the four motor cans (rear-port, 29° off the tail, 3.9° above the arm plane, 1.36 px/mm at the centre, 4.7 px rms; derived). Fore-aft positions were then read on a grid projected onto the packs' faces (measured, ±0.015 m on the reading, ±0.03 m with the pose).

**Shows:**
- **The port pack, the near one, runs from about +0.125 m to −0.04 m** on its outer face, about 0.012 m ahead of the front legs and about 0.06 m short of the rear legs. **Its centre is about +0.04 m**, so it sits forward. Its leads leave the rear end. The starboard pack's rear end is behind the stack and leads, and its position is not readable.
- **The slung payload, crosswise, is centred at about −0.03 m (±0.03)**, read from the midpoint of its 0.24 m image span.
- **The spool on top is centred about +0.04 m** (the drone sheet).
- So T carries its packs and spool forward and its payload aft. That is a builder balancing a slung load against the packs, the opposite way round from D01's problem.

**Doesn't show:**
- T's masses: the payload's and the spool's are unknown, so T's CG can't be computed. With a 1–2 kg payload it would land within about ±15 mm of the centre (derived, from the ±0.03 m positions). That says nothing finer than the ±1 cm the guides quote.
- A canister: T's layout is the one D-050 superseded. T has no slung canister and no bottle on top, so it can't place D01's canister.
- **Side finding:** on this projection the port pack measures **about 0.165 m long**, not the drone sheet's 0.20–0.28 m. The sheet's figure appears to ignore the 29° view's foreshortening along z, which is 0.66 px/mm against 1.36 across (derived). Twelve 21700 cells in 0.165 m fit 6 along × 2 high, crosswise, rather than D01's 3 along × 2 across × 2 high. That's for the drone sheet's owner, and it doesn't change the balance, because the packs stay centred either way.

## 5. Where D01 needs to change (derived)

The masses and positions are `drone.json`'s on the build line (model 64), legs on. The only parts off z 0 are the coil (0.08 kg bobbin + 0.69 kg × fill, at +0.0385 m), the canister (0.23 kg, at +0.023 m) and the electronics (0.24 kg, at +0.010 m). The payload is at z 0. So **the forward moment doesn't depend on the payload**: 37.3 / 24.0 / 10.8 kg·mm at a full, half and empty spool. The payload only dilutes it.

A front motor's share of the thrust is **(1 + z_CG / 0.160) × the mean**, about **0.63 % per mm** of CG offset (derived from the moment balance about the CG).

CG z in mm, payload / spool fill:

| Layout | 0 kg full | 0 half | 0 empty | 1 kg full | 1 half | 1 empty | 2 kg full | 2 half | 2 empty | Worst front-motor load |
|---|---|---|---|---|---|---|---|---|---|---|
| As built | +9.1 | +6.4 | +3.2 | +7.3 | **+5.1** | +2.5 | +6.1 | +4.2 | +2.0 | +5.7 % |
| As built, the canister's cone and nozzle at their own place (its mass centre 0.025 m aft of the part's, as the part's note says) | +7.7 | +4.9 | +1.5 | +6.2 | +3.9 | +1.1 | +5.2 | +3.2 | +0.9 | +4.8 % |
| Packs 13.7 mm aft (zero at half spool) | +3.2 | 0.0 | −3.9 | +2.6 | 0.0 | −3.0 | +2.2 | 0.0 | −2.5 | ±2.4 % |
| Packs 21.2 mm aft (zero at full spool) | 0.0 | −3.6 | −7.8 | 0.0 | −2.8 | −6.0 | 0.0 | −2.3 | −4.9 | −4.9 % (now the rears) |
| **Canister and coil 35 mm aft** | +0.6 | +0.3 | 0.0 | +0.5 | +0.2 | 0.0 | +0.4 | +0.2 | 0.0 | **+0.4 %** |
| The same, with the cone and nozzle at their own place | −0.8 | −1.2 | −1.7 | −0.7 | −1.0 | −1.3 | −0.6 | −0.8 | −1.1 | −1.1 % (rears) |
| Canister and coil 38.5 mm aft (the coil on the hubs' centre) | −0.3 | −0.3 | −0.3 | −0.2 | −0.3 | −0.3 | −0.2 | −0.2 | −0.2 | −0.2 % |

Legs off, every row is within 0.3 mm of these.

**The three levers, against the evidence:**

| Lever | What it takes | Evidence |
|---|---|---|
| **Canister and coil aft** | **0.035 m** (0.035–0.0385). The nozzle tip goes from 0.045 to 0.080 m aft of the rear motors' axis, the band's aft edge (0.03–0.08, measured), and its height is unchanged. The body becomes +0.103 to −0.127 m, centre −0.012, still under the packs and inside the legs (x ±0.05 vs the legs' ±0.1025). The tether's exit moves 0.035 m aft with it | **favoured.** Build A centres its canister body on the hubs, and Build B hangs its canister from the centre aft (measured). It fixes every fill state, because the coil is the mass that changes in flight [5] |
| Packs aft | 0.014–0.021 m, and right only at one fill: the CG still walks 3.6–6 mm over the payout | **against T**, whose packs sit forward (+0.04 m, measured). The guides' pack slide [1] is the right tool for a fixed mass, not for a spool that empties |
| Payload | the forward moment is payload-independent, so the shift needed is 24 kg·mm / m_payload: 24 mm at 1 kg, and no fix at all at 0 kg | **against.** The bottle is centred by D-050's reading (assumed), and no reference places it |

**Recommended (derived):** move the `canister` and `coil` parts 0.035 m aft along z, by the same amount (`coil` +0.0385 → +0.0035, `canister` +0.023 → −0.012). Their hangers, the canister's contact shapes and the tether's exit go with them. That leaves the CG within 0.6 mm of the hubs' centre at every loadout, a 0.4 % front-motor excess, which is far inside the builders' ±1 cm. If the physics engineer also moves the cone's and nozzle's 0.045 kg to their own place (the part's own note), 0.030 m aft does the same, with the CG between +0.4 and −1.3 mm over the loadouts (derived). Whether the residual 1–6 % ever mattered in the real class is the pilot's experience, not something to compute (§6).

## 6. Not found / open

- No builder's text, video or listing on the class's CG with a fiber canister. No listing gives pack or canister fore-aft positions or a balance step. Searched in English, Ukrainian and Russian; the class's listings give only overall size, pack type and payload.
- No published CG tolerance for heavy quads beyond the forum's ±1 cm and "more critical when heavy" [2].
- Build B's canister position is coarse (§3), and Build A's photos are scaled to an assumed motor spacing (±15–20 %).
- **Pilot-only (qualitative, for the matching gate):** does the real quad hover hands-off without a nose-down drift with a full spool, and does it change as the spool empties? (A yes on both supports the canister move; a nose-heavy feel with a full spool would mean the class lives with some offset.)

## References

1. https://www.unmannedtechshop.co.uk/blogs/knowledge-base/fpv-drone-center-of-gravity-balance-guide (CG balancing guide: two-support balance, pack slide, hover check at about 1 m)
2. https://intofpv.com/archive/index.php/thread-23059.html (forum thread: ±1 cm battery tolerance; CG more critical for heavy, large quads near full throttle; the motors carry the offset)
3. https://discuss.ardupilot.org/t/pixhawk-motor-balance-warn/33051 and https://discuss.ardupilot.org/t/rcout-not-all-equal/17422 (forum threads: reading front- or rear-heaviness from hover motor outputs; fix by moving the battery or other parts)
4. https://ardupilot.org/copter/docs/common-mounting-the-flight-controller.html (autopilot placement near the CG, "within a few centimeters")
5. https://www.kitplanes.com/wind-tunnel-89/ (aircraft design: keep the fuel inside the allowable CG range; compute the CG from empty to full)
6. Builds A and B, and the class's canisters: [spool-exit-and-rear.md](spool-exit-and-rear.md), references 1–3 and 5–7 there.
