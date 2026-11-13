# Pilot questions

## The rule (D-024, PRC-11)
The pilot's words: "why tf are you asking me these questions if it's you whos going to write ultra realistic physics? youll need to fucking calculate it."
- **Never asked:** quantities (heights, rates, frequencies, speeds, sizes, how often something happens). The team computes them from physics, research and the reference footage.
- **Asked, only at gates and briefly:** qualitative experience ("does X happen at all", "what causes it in practice", "what makes it worse") and preferences (feel, rates).
- **Never asked twice:** anything already answered (below) or derivable from the references.
- **Never asked either** (the pilot, 2026-11-13: "Again physics, why are you asking me this"): how the world behaves physically, even when it is not a number. Whether grass lies flat in the prop wash, what a blade cuts, how a surface gives: the team calculates it.

## Already answered: never ask again

| Topic | Answer | Source |
|---|---|---|
| Legs' material | "usually plastic, or something … as flexible as a carbon plastic", usually hand-made; it varies per drone | 2026-11-05, D-086 |
| The roof-rack prop | the drone was resting its weight on the prop when armed | 2026-11-05, D-086 |
| The spool's exit | every spool has a hole the fiber unwinds from; the spool is mounted at an angle so the fiber leaves at about 25–30°, so a climb or throttle-up doesn't drag it straight across the hole's edge; the fiber's ground end is patched into a ground station, so tension matters | 2026-11-05, D-086 |
| The canister | not opaque | 2026-11-05, D-086 |
| Balance and loads | every build balances differently (a little nose-down, tail-down or to a side), and the cargo on top is strapped by hand with no fixed procedure; small, but tangible on a heavy quad at liftoff and landing; part of the randomness the pilot trains for | 2026-11-05, D-086 |
| ESC features, failsafe, airmode | ESC firmware unknown and doesn't matter; brake on stop, return-to-home and the like are usually off: flown manually; on the fiber, with no video you often still have control; no airmode: the pilot finds the stick's sweet spot or disarms right before touchdown, by preference and skill | 2026-11-05, D-086 |
| Camera uptilt | 0° | wind study Q1 |
| Flight mode | acro (rate) | Q3 |
| Wind character | mostly from the left, but at random from other sides too; "real life, unexpected and undefined" | Q2 |
| The tree belt in wind | the worst part: "either you yaw but lose direction OR you roll and keep direction, but lose horizon" | Q7 |
| Picture losses in wind | cause unknown; hypothesis: wind shakes the fiber, and trees plus wobble stretch it | Q8 |
| Speed in strong wind | no different from normal cruise; the effort goes into correcting course drift | 2026-10-17 |
| Batteries | Li-ion, 20–35 Ah, 6S4P to 6S5P ±; **"a couple of batteries"** on the drone. The OTU class runs 8S Li-ion (FpvPrep profiles) | pilot, fc-profiles |
| Coil | about 1 kg | pilot |
| The fiber in the first flights | fly free first; the fiber comes in the next flight build after G7 | 2026-10-21, D-042 |
| The drone's look | letter T: the spool on top, the payload slung below, two packs along the sides, carbon tube legs; legs-off flights rest on the upside-down chair, since it can't stand on the spool | 2026-10-21, D-043 |
| Menus before flying | a minimal menu (Train / Calibrate / Exit, setup), judged at G7; no wireframe gate | 2026-10-21, D-042 |
| Payload | on **top** of the drone | 2026-10-17 |
| Water in the world | yes: a pond or a small river; fiber on water keeps the signal (corrected by the pilot, D-039); graphics mildly realistic; wind makes waves | 2026-10-18 (D-036), 2026-10-19 (D-039) |
| Parallel subagents | 4 | 2026-10-18 (D-036) |
| Legs | plastic, 25–35 cm, a little soft; sometimes stumble on landing; pilots land slowly and level the horizon just before touchdown | pilot |
| **Launch stand** | about **25 cm high, "like a chair upside down"**: one post under each motor, on a base; sized to the Svinoriz (D-022) | 2026-10-17 |
| **Rates and throttle** | the pilot's FpvPrep profiles: their flown set, leaning toward the sharper factory set, especially throttle; keep room for more Betaflight settings later (D-023) | 2026-10-17 |
| **Leg sticking at liftoff** | sometimes, a little; more often on softer ground; **most of the time in rain** | 2026-10-17 |
| **Settling into own wash** | governed by **throttle (thrust) against the drone's weight** while descending, not by descent speed; it wobbles | 2026-10-17 |
| **Throttle-punch shake** | "doesn't happen much"; the rest is calculated | 2026-10-17 |
| **What cuts the fiber** | overly sharp reverse; snagging on trees; a very sharp liftoff; sudden backward momentum right after liftoff | 2026-10-17 |
| **Rain on the lens** | drops sit on the lens **even in fast flight**, and sometimes travel with the airflow; the rest is calculated | 2026-10-17 |
| **Rain setting** | its own setting: **Sunny / Rain / Severe rain**; night via time of day (D-021) | 2026-10-17 |
| Motors, airframe specs, plaster, building construction, war damage | **research them**, don't ask | pilot |
| Feed noise | always present; throttle coupling rare; mid-flight flashes that recover; the four loss stages; **losses are a mix of variants** | video notes, 2026-10-17 |
| Recovery after a dropout | the picture comes back clean; receiver text only on blue | PR-4 |
| Broadcast standard | PAL only | old D-015 |
| OSD | copy the footage exactly; "Svinoriz 10 Opto"; "! SAFE !" | PR-2, brief |
| Wind setting | a fixed choice of Calm / Windy / Severe, never random | old D-011 |
| Vehicles | ВАЗ, Chery, Chevrolet, VW, Lanos, ЗИЛ, Урал; abandoned and destroyed; "REALISM" | pilot |
| Terrain | trenches are common; burnt and tilled fields both exist; the ground is not flat; belts have bushes among the trees | pilot, PR-8, PR-10 |
| Map composition | 3 adjacent villages, rich; the one destroyed Stalin-era block | brief, pilot |
| Narrow fly-ins | a LITTLE wider than the drone (cellar door 0.70 m) | PR-9 |
| **What "realism" means** | a copy of the references (flight replays and photos), plus proper geometry for the looks and for the physics (D-028) | 2026-10-17 |
| **Feed noise** | very much similar to the Godot feed (D-026) | 2026-10-17 |
| **Subagents** | one subagent per logical task (D-029) | 2026-10-17 |
| Engine, assets, repo | Unity; free only (CC0 + team-built); the existing repo, every Unity branch under `unity/` (trunk `unity/main`) | 2026-10-17 |
| **Landing into straw** | "You shake. But again - you must calculate so the behavior matches physics." How much decides what the pilot does: "If I can see the drone is shaking too much (which means there's A LOT of straws and the straws are thicker than I thought - Better throttle up and find a bette replace to land. If shake is agreeable - I just keep landing)" | 2026-11-13 |
| **Tall grass in the prop wash** | "Usually it lies flat because of prop wash power, but not always" | 2026-11-13 |
| **The straw** | "Straws are quite stiff, but not extremely" | 2026-11-13 |
| **The physics, above all** | "We must recall all of what was discussed before so the physics of drone is top notch this time" (D-104) | 2026-11-13 |

## Dropped as computable: the team calculates these (D-024)
- **Landing-bounce height:** from leg compliance and residual thrust.
- **Lens-drop size, speed and behaviour:** from rain rate and the airflow at the lens.
- **Frame stutter before a loss:** the references show the mix; N9 is not simulated.
- **Motor-mismatch yank frequency and axis:** from researched component tolerances.
- **How often the throttle-punch shake happens:** from physics.
- **Whether tall grass lies flat in the prop wash, and whether straws are cut on the way down or only moving forward** (asked 2026-11-13, and it should not have been): from the slipstream, the stems' stiffness and the blade's speed (`w9-village-physics` 2.2 and 2.12).
- **Taken off the open list on 2026-11-13 under the same rule** (D-104), each now a calculation, a research task or something the pilot will simply feel at a gate:
  - the yaw kick in the first instant of a full yaw stick: from the rotors' inertia and the ESC's response, researched (`p6-flight-core` 1.4);
  - the roll's overshoot after a snap: from the builder's tune and the filter chain (D-045, `p7b-airframe` 1.0);
  - the jitter and tilt on a punch out of a fast descent: from the rotors' wash (D-069, `p7b-airframe` 1.4; the ledger's gap 10);
  - whether the laid fiber is dragged in at a sharp low turn: from the spool's pull and the ground's friction (`w3-farmland-ground` 2.4);
  - whether the legs part on a 10° side slope: from the legs' failure law and its sources (`w9-village-physics` 2.6);
  - where the cut bits of straw show in the picture: from where the blades cut and how the pieces leave (`w9-village-physics` 2.2, 2.5).

## Open
Ask new questions only under the rule above, at the matching gate.

| Gate | Question | Why it is the pilot's | Source |
|---|---|---|---|
| G2 | Looking at the fortifications in the gallery: which forms read as what you see today? Coils loose or on pickets, bright or rusted; teeth squat or slim; hedgehogs of rail or of angle; a made or an improvised barrier; do the coil's flat loops read right? | qualitative look; no reference shows them | F01–F04, D-035 |
| G5 | On a legs-off build, does the payload ride on top or stay slung below? | their own kit; T shows only the legs-on build | D-043, `drone.md` |
| G5 | Letter T's payload hangs crosswise under the frame, with the spool fore-aft. Is that how your drones sit? | their own kit | D-043, `drone.md` |
| G7 | In clip P, along the tree strip, did you climb to stay over it, or does the ground drop there? | their own flight; the footage reads the drone at about 0.45–1.0 of the canopy height inside the strip, and the harness's belt stretch depends on which it is (`docs/reference-notes/belt-geometry-p.md`) | `p7c-wind` 1.5 |
| G4 | The references show two no-signal screens: a bright blue with small centred text, and a deeper blue with two lines top-left. Which do your goggles show, or does it depend on the receiver? | a preference about their own kit; the footage shows both | FEED-07, gap review L12 |
| Farmland flight (D-093) | The start point is on a level hilltop meadow at the village's south-east edge, 35 m from the nearest ruin, facing it. The village's core is on an 11 m slope, with no level 2 m gap between the ruins. Is that a good place to start, or do you want it among the ruins (on a slope, or on a levelled pad)? | where they like to start; the rule (free, level, in the village) is met either way | D-092, `w1-farmland-world` review F7 |
| Play build (D-100) | With M open, the map covers the OSD's centre: the reticle and the throttle. Is that fine for a glance, or should the map leave the centre free? | a preference about flying with it open | `w11-world-map` review, F6 |
| Play build (D-100) | The cards' stills show the feed's first field: its grain, but no interference bars. Do you want more of the feed's character on the cards? | a look preference | `w10-play-maps` 1.2 |
| Props in the frame (W13, W14) | The two pale bars at mid-height at the left and right edges of your picture: what are they on your quad (the front legs, antenna tubes, something else)? | their own kit; the footage shows the bars but not what they are | `w14-camera-place` 1.1, the same note §3.3 |
| Props in the frame (W13, W14) | Does your camera sit out at the nose, under the frame, ahead of the front motors? | their own kit; the reference photo hides the camera, and the footage puts the lens there | `w14-camera-place` 1.1, `reference-notes/camera-place.md` §7 |
| Next build (the fiber) | The fiber's exit on your canister: what is it (a bare metal hole, a plastic or ceramic eyelet, a short tube), and is its edge sharp or rounded? | their own kit; whether the fiber can tear at the exit depends on the exit's material and edge, and no reference shows it closely | `p7d-tether` 1.11, design 18 |
| Next build (the fiber) | Before liftoff, how do you lay the fiber from the drone to the station: pulled straight, or with some loose line near the drone? | their own practice; loose line at the launch spot is what a hard liftoff could blow into the props, and nothing public describes a lay | `p7d-tether` 1.11b, design 18 |
| Next build (the hover) | Could you share one Betaflight blackbox log of a plain hover from your own quad, made anywhere harmless? It stays on this machine and is never committed. | their own kit; the log shows how much of a hover's heading motion is the hand on the yaw stick, which no public source sizes for this pilot | `p7c-wind` 1.5d, D-110 |
