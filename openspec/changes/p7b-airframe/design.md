# Design: p7b-airframe

## Context
- `p6-flight-core` has the Betaflight controller with the builder's filter chain (D-045), the DC motors on APC's curves with the 40 A ESC limit, two packs with sag, drag, the stand-in wind and contacts. Every rotor is identical and its inflow sees only the wind.
- Randomness rule: only a bounded perturbation of a named physical parameter, from a named seeded stream, logged; never noise on the pose.

## Goals / Non-Goals
**Goals:** each PHY-07…12 mechanism physical, seeded where random, logged, with a computed band and a known-wrong model that fails it; determinism and the budget kept.
**Non-Goals:** the proposal's non-goals.

## Decisions
1. **Asymmetry from tolerances** (PHY-07): per motor Kv spread, ESC timing jitter and prop imbalance, each drawn once per flight within published or researched tolerances (labelled), named streams. The band of drift and correction rate with wind off is computed from them; with asymmetry off the drone is measurably steadier; it never buzzes or diverges.
2. **The per-rotor inflow seam** is shared with `p7c-wind`: one function gives each rotor its inflow (wind, own and neighbours' wash, ground and ceiling terms). Both changes write into it; this change defines it first.
3. **Ground and ceiling effect** (PHY-08): Cheeseman–Bennett per rotor from a ray straight down the rotor axis, combined with `SampleGround`'s relief and mat top; a `Hole` switches the ground term off. The same form under a roof for the ceiling effect. Recirculation in a close hover: a bounded, physical re-ingestion of the own wash that keeps a close hover never still.
4. **Vortex ring state** (PHY-09): onset from the descent rate normalised by the induced velocity at the current thrust, so thrust against weight drives it; hysteresis between entry and exit; thrust loss and an asymmetric, wandering moment while inside. Entry and exit are logged events.
5. **The throttle-punch shake** (PHY-10): the frame's first bending mode and the payload and spool pendulum as small physical modes, excited by wash re-ingestion after an arrested fast descent. Its occurrence and duration (0.3–0.7 s) come out of descent rate, input abruptness and payload; the filter chain (D-045) shapes what reaches the loop. It feeds `CameraVibration`.
6. **Battery and inertia checks** (PHY-11, 12): the heavy corner (6.5 kg, low charge) shown to lose its thrust margin; a low-voltage signal for the OSD; the inertia ratios and yaw saturating first, from the real `drone.json`.
7. **Labels and bands:** every new number pilot / measured / published / derived / assumed; every assumed one a named, tunable parameter; each mechanism's scenario fails its known-wrong model.

## Risks / Trade-offs
- **The shake can't be tuned to a number** (the pilot: it "doesn't happen much"). Its check is that it arises from its causes and is rare, not that it hits a rate.
- **Ground effect needs the world's rays over the test range** (`p6-world-query-gallery` 1.4). The stub floor carries the first scenarios.
- **Each mechanism moves the goldens.** One re-record per task, audited.

## Open Questions
- None for the pilot. The pilot's feel is judged at G7.
