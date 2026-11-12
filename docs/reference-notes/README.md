# Reference notes, from the first attempt

These notes were copied verbatim from the Godot repo (`MxTriX77/450fpv`, `main`). They are the measured, OPSEC-clean analysis of the pilot's footage and photos, and they stay valid for the Unity restart. Read them alongside [../knowledge.md](../knowledge.md), which condenses them.

| File | What it holds |
|---|---|
| [terrain.md](terrain.md) | the reference set by letter; terrain, vegetation, earthworks, buildings, vehicles, small objects; micro-detail; surface table; map composition; ranked build list |
| [video-feed.md](video-feed.md) | the signal chain; noise events N1–N13 with measured rates; pixel, optics and colour traits; the OSD layout; pipeline order; the signals the feed needs |
| [wind.md](wind.md) | the frame-by-frame study of clip P (severe wind and rain); interpretation; the simulated pilot; the Severe/Windy/Calm targets T0–T12; rain traits R1–R8 |
| [airframes.md](airframes.md) | public specs of the reference airframes; class mass and thrust |
| [pilot-requirements.md](pilot-requirements.md) | PR-1 to PR-11, in the pilot's words, then interpreted |
| [ground-micro-motion.md](ground-micro-motion.md) | new for D-064: the camera's micro-motion near the ground by phase, axis and band (C, D, E, F, H, A), clip S's low cruise, the recorder's 2-2-1 field steps, and the targets for the physics |
| [stem-properties.md](stem-properties.md) | new for w9-village-physics 2.11: published stem and leaf properties per kind (green grass, dry culms, weed stalks, straw, tree leaves), the critical speed for impact-cutting a free stem, stand densities, stem drag, and what the props' stem physics should use |
| [vegetation-on-the-props.md](vegetation-on-the-props.md) | new for w9-village-physics 2.2 and 2.5: every stretch of grass, straw and weeds near the lens (D, H, A, F), the shake there, the prop blades in frame by airframe group, the pale rods as airframe parts, the targets, and what no clip shows (no cut bits, no landing in grass) |
| [fiber-exit.md](fiber-exit.md) | new for p7d-tether design 17: published friction of acrylate-coated fiber and coatings (on steel, on fiber), why the capstan law fails at a sharp exit edge and the point-contact law to use instead, real payout exits (drone nozzles, missile and torpedo eyelets, flares and fairleads), published peel and payout tension, the exit against the rear props, and the recommended lip friction |

## Things that changed since they were written
- **The drone's name** is now **"Svinoriz 10 Opto"** (the pilot, 2026-10-17). These notes say "Svinorez".
- **Engine paths:** paths like `game/src/video/`, `tools/reference/*.py` (Blender scripts) and `reference/_frames/…` refer to the old repo. The tools can be ported or re-run from the old checkout.
- **D-numbers here refer to the old log**, now [../legacy/godot-decisions.md](../legacy/godot-decisions.md). The new log is [../decisions.md](../decisions.md).
- **Coordinate frame:** the old map frame was +Z south. The new one is +Z north (D-019).
- **The pilot overrode clip P's rain reading** (2026-10-17, D-021): drops **do** sit on the lens, even in fast flight, and travel with the airflow. wind.md §7 R1/R8 ("none seen") is superseded.
- **Speed in strong wind** (wind.md Q14) is answered: normal cruise. So the low-drag reading of §5.3 applies (crosswind about 6.5–8.5 m/s).
- **Launch rails → launch stand** (D-022): four posts about 25 cm high, one under each motor.
- **New:** [fc-profiles.md](fc-profiles.md), with real Betaflight profiles of the class and the sim's default rates (D-023).
- **New:** [hover-micro-motion.md](hover-micro-motion.md), why the sim's camera is quiet above 4 Hz against clip P: P's floor split from its tracker's, the sources ranked, and the spreads (D-059).
- **New:** [rate-tracking.md](rate-tracking.md), how the drone follows its controller's setpoint on roll, pitch and yaw at 0–2 kg, what limits each axis, the pilot's model-26 flights, and the yaw I factor the sim's controller misses (D-076).
- **New:** [class-legs.md](class-legs.md), the class's leg tubes against the sim's 10/8 mm PVC-U: section, stiffness, buckling, and the material in doubt (p7g-legs).
- **Drone layout:** the payload sits on top and there are two battery packs (pilot, 2026-10-17). The mass layout in wind.md §5.5 assumed cargo forward and below, with one pack. Phase 5 re-derives it.
