# Tasks

Each task is its own branch off the build branch (`unity/physics/physics-integration-2--w3-preview`), merged into it for the pilot's build. Order: 1.1; then 1.2 and 1.3 (1.3 needs no 1.1); then 2.1 on the pilot's word.

## 1. Rain on camera

- [x] 1.1 [physics-engineer] **The lens's water** (D-126, FEED-13). Design first in `design.md`. No GPU. Branch `--w21-lens-water`.
  - Research: drop-on-surface adhesion (contact-angle hysteresis, the retention force), sliding under shear and body force, coalescence and shedding, for a coated glass or plastic camera lens in rain; the impingement of rain on a moving, tilted surface; each number labelled with its source.
  - The water arriving: Marshall–Palmer drops at `RainPreset`'s rate and fall speed, carried by the air at the camera (`AirVelocityAtCamera`), relative to the camera's velocity, onto the lens's facing at the attitude; the airframe's shelter from the drone's own geometry (`drone.json`).
  - The drops on the lens: each held by its adhesion and moved by the air's shear at the lens and the specific force in the lens's frame (gravity at the tilt, minus the camera's acceleration, plus the rotation's terms from `CameraAngularVelocity`); merging, trails and shedding.
  - Runs on published frames at the feed's field rate (as `PlantWash` does), deterministic from the flight's seed, 0 B steady state, bounded drop count. Publishes per field the drops (position on the lens, size, elongation, the trail) for 1.2: **its public shape committed first**.
  - The flight unchanged: no model number, `flightbench --golden` and `worldbench --golden` the same. Tests for the spec's scenarios in `tools/simtests`.
- [ ] 1.2 [tech-artist] **The drops drawn** (FEED-13). Needs the GPU. After 1.1's shape.
  - Out-of-focus refractive blobs and smears ahead of the analog stages, from 1.1's drops; `FlightFeedSignals.RainRateMmPerHour` from the physics when live, not the stub.
  - Checked in recorded rain flights (hover, forward flight, banks, into and away from the wind); the frame-rate cost on AC.
- [x] 1.3 [tech-artist] **The wet look** (VIS-03). Needs the GPU. Design in `design.md` (1.3); its frame cost on AC is still to measure (6).
  - Wetted materials darken and gloss by a published wet-surface law (for example Lekner and Dorf 1988) from each material's own albedo, at the flight's rain; Severe rain adds standing-water sheen on hard surfaces and rain extinction in the distance from the drop spectrum.
  - Captures Sunny against Rain against Severe rain at the same light; the frame-rate cost on AC.

## 2. Close

- [ ] 2.1 [qa-engineer] **Review** against the spec's scenarios, on the pilot's word.
