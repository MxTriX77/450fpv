# Pilot answers (task 4.2)

The pilot's answers to the open questions in `terrain.md` and `video-feed.md`, given 2026-09-23. They are close to verbatim, lightly cleaned, and OPSEC-safe. The notes owners fold them into the notes (tasks 4.4 and 4.5). Where an answer contradicts a note, **the answer wins**.

The pilot's standing direction: **REALISM above all.** Every effect varies randomly, the way it does in real life, and never repeats identically.

## Video feed (tech-artist)

- **U1, mid-flight flashes:** they happen at random. They are separate from signal loss and they recover.
- **Baseline noise:** it is **always** present on analog, at least mildly. It is never a clean picture.
- **Variety:** noise and flashes differ from one another. Sometimes a little noise, sometimes stripes, and every instance differs. Simulate real-life noise so it looks super realistic, never a repeating pattern.
- **U6, throttle:** noise may depend on throttle, but **rarely**. So the throttle coupling is weak and occasional, not a dominant driver.
- **U2 and U10, signal loss / fiber break sequence:** a **very quick picture glitch → a brief flashy moment → noisy and flashy → finally blue**. Randomise the timing and the look of each stage so no two losses look the same.
- **U5, low light:** the pilot has added **night footage**, `reference/terrain/night.mp4`. Characterise it.
- U3, U4, U7, U8, U9, U11, U12 and U13 weren't answered. Keep the notes' current best estimate and mark them "unconfirmed, tunable".

## Terrain and world (world-artist)

- **Vehicles:** typical Ukrainian cars:
  - ВАЗ (Lada) models
  - low to mid-class cars: Chery, Chevrolet, VW, Daewoo Lanos
  - ЗИЛ and Урал trucks

  Some are **abandoned** and some are **destroyed by strikes**. The pilot has added reference images: `reference/terrain/destroyedcar1–3.avif`.
- **Trenches:** common in this war and **confirmed** as a terrain class. Earthworks like clip B's bank are real.
- **Dark fields:** both kinds exist. Some are **burnt**, from strikes or from FPV drones that fall and explode. Others are **tilled**. The map has both.
- **Map composition:**
  - diverse
  - a couple of village houses with yards, like the one in the clips
  - one typical **destroyed 5-storey Stalin-era block**, like the pilot's photos
- **Future edits:** the pilot will change the map in future. Map format and modelling must make changes smooth, so this gets planned as its own authoring change.

## Review scope (all roles)

The pilot reviews only tangible milestone deliverables (UAT-1 map parts, UAT-2 noclip world, MVP flights, release), never sandboxes, sample patches or planning documents. See `docs/roadmap.md`.

## Still open

- **Launch rails** (for legs = FALSE): spacing, height, length, profile and finish, and which part of the drone rests on them. Asked; no answer yet.
