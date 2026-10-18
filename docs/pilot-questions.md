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
| Payload | on **top** of the drone | 2026-10-17 |
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
None. Ask new questions only under the rule above, at the matching gate.

## Open, to ask at G1 (added by D-035)

| Topic | Question (brief) | Why it is the pilot's | Default until answered |
|---|---|---|---|
| Water in the world | Keep a small river with tree-lined banks and a pond or two, as your layout reference (Q) shows, or dry land only? | The references show no water; it is a look and scope preference, not a number | Keep them (WLD-01) |
