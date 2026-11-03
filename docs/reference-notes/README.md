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
- **Drone layout:** the payload sits on top and there are two battery packs (pilot, 2026-10-17). The mass layout in wind.md §5.5 assumed cargo forward and below, with one pack. Phase 5 re-derives it.
