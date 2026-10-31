# Pilot questions

## The rule (D-024, PRC-11)
The pilot's words: "why tf are you asking me these questions if it's you whos going to write ultra realistic physics? youll need to fucking calculate it."
- **Never asked:** quantities (heights, rates, frequencies, speeds, sizes, how often something happens). The team computes them from physics, research and the reference footage.
- **Asked, only at gates and briefly:** qualitative experience ("does X happen at all", "what causes it in practice", "what makes it worse") and preferences (feel, rates).
- **Never asked twice:** anything already answered (below) or derivable from the references.

## Already answered: never ask again

| Topic | Answer | Source |
|---|---|---|
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

## Dropped as computable: the team calculates these (D-024)
- **Landing-bounce height:** from leg compliance and residual thrust.
- **Lens-drop size, speed and behaviour:** from rain rate and the airflow at the lens.
- **Frame stutter before a loss:** the references show the mix; N9 is not simulated.
- **Motor-mismatch yank frequency and axis:** from researched component tolerances.
- **How often the throttle-punch shake happens:** from physics.

## Open
Ask new questions only under the rule above, at the matching gate.

| Gate | Question | Why it is the pilot's | Source |
|---|---|---|---|
| G2 | Looking at the fortifications in the gallery: which forms read as what you see today? Coils loose or on pickets, bright or rusted; teeth squat or slim; hedgehogs of rail or of angle; a made or an improvised barrier; do the coil's flat loops read right? | qualitative look; no reference shows them | F01–F04, D-035 |
| G5 | On a legs-off build, does the payload ride on top or stay slung below? | their own kit; T shows only the legs-on build | D-043, `drone.md` |
| G5 | Letter T's payload hangs crosswise under the frame, with the spool fore-aft. Is that how your drones sit? | their own kit | D-043, `drone.md` |
| G7 | On a full yaw stick from hover, does your quad kick hard in the first instant and then settle to a steady yaw rate, or does yaw build up smoothly? | their own quad's feel; the model's first 50 ms rests on an assumed rotor inertia (±30 %) and an ESC with no current ramp | `p6-flight-core` 1.4 |
| G7 | When you snap a roll and stop the stick, does your quad overshoot and swing back before it holds, or does it stop clean? | their own quad's feel with the builder's tune; the model overshoots about 29 % (roll) and 26 % (pitch) with the builder's filter chain and the ESC's derived 0.4 ms lag (D-045, `p7b-airframe` 1.0) | `p6-flight-core` 1.4b |
| G7 | Which ESC firmware do your heavy quads run: BLHeli_32, AM32 or Bluejay? | a fact of their own kit; it sets whether the ESC limits current (BLHeli_32 at 40 A here; Bluejay senses none) and so the first-instant yaw kick | `p7b-airframe` 1.0 |
| G7 | In clip P, along the tree strip, did you climb to stay over it, or does the ground drop there? | their own flight; the footage reads the drone at about 0.45–1.0 of the canopy height inside the strip, and the harness's belt stretch depends on which it is (`docs/reference-notes/belt-geometry-p.md`) | `p7c-wind` 1.5 |
| G7 | When you punch out of a fast descent with a heavy payload, does your quad jitter and tilt a little on its own before it climbs, or does it climb straight? | their own quad's feel; the model's four ESCs sit at their 40 A limit through a sharp arrest while the rotors re-ingest their wash, tilting 5–16° uncommanded (D-049, `p7b-airframe` 1.4) | `p7b-airframe` 1.4 |
| G4 | The references show two no-signal screens: a bright blue with small centred text, and a deeper blue with two lines top-left. Which do your goggles show, or does it depend on the receiver? | a preference about their own kit; the footage shows both | FEED-07, gap review L12 |
