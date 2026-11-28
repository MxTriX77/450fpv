# Proposal: w21-rain-on-camera

Tier: Standard (D-033). Requirements: FEED-13, VIS-03, D-021, D-126.

## Why

The pilot, 2026-11-28: "it never shows the rain visually. also raindrops should represent current wind", then "lens drops, yes. but also, they should be affected by flight dynamics - affected by directional wind and drone's tilt. should be calculated properly" (D-126).

Rain already acts on the flight (p7c-wind 1.7: the drops' push on the airframe, the water film, wet ground and legs, the wet fiber). On camera it shows only as the overcast sky: the feed still reads a stub rain rate of 0, and neither FEED-13's lens drops nor VIS-03's wet materials exist.

## What Changes

- **The lens's water** (physics, engine-free): the drops reaching the lens from the rain rate and their velocity relative to the camera (the wind, the fall speed, the drone's own velocity) onto the lens's facing at the drone's attitude, sheltered where the airframe shades it; then each drop on the lens, held by its adhesion and moved by the air's shear and by the force in the lens's frame (gravity at the tilt, minus the camera's acceleration, plus the rotation's terms); merging, sliding, trails and shedding. It runs on the flight's published state and never feeds back.
- **The drops drawn** (feed): out-of-focus refractive blobs and smears ahead of the analog stages, so they take the feed's softness (FEED-13); the feed's rain rate from the physics, not the stub.
- **The wet look** (look): wetted materials darker and glossier by a published wet-surface optics law from each material's albedo; in Severe rain, standing-water sheen and rain extinction in the distance (VIS-03).

## Non-goals

- Falling streaks in the air (VIS-03 and K§8.7 R2 stand, D-126).
- Any change to the flight: no model number, goldens byte for byte.
- Splash on the ground, puddles' ripples, wipers or a lens hood the drone doesn't have.

## Capabilities

### New Capabilities
- `rain-on-camera`: the lens's water from the flight, its drawing in the feed, and the wet look.

### Modified Capabilities
- None. FEED-13 and VIS-03 are met as written.

## Impact

- Sim package: a lens-water model beside `Runtime/Wind/Rain.cs`, fed from published frames (as `PlantWash` is), with tests in `tools/simtests`.
- Unity: `Scripts/Feed` (the drops' pass, `FlightFeedSignals.RainRateMmPerHour` from the physics), the look's wet materials (`Scripts/Feed/Look*`, the material set).
- GPU: the drops' pass and the wet materials, measured on AC.
