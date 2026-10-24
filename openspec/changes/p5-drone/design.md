# Design: p5-drone

## Context
- **What K§5.2 gives:** a derived mass build at nominal 4.8 kg in flight (4.4–6.5 kg at take-off) on an assumed layout with the cargo forward and below and one pack. The pilot has since said the payload is on top and the drone carries "a couple of batteries".
- **What K§5.4 gives:** the prop-tip geometry and the four-post stand.
- **What `p6-flight-core` provides:** the `drone.json` schema and a bootstrap file, which this change replaces.

## Goals / Non-Goals
- **Goals:** the real component spec; the model and the stand built from it; G5.
- **Non-Goals:** the proposal's.

## Decisions
1. **Research first, from letter T:** the pilot's reference photo (D-043) is the look; public sources fill what it does not show. The object sheet (`docs/object-sheets/drone.md`, in the `object-sheets` format) from public sources on typical Ukrainian heavy 10-inch fiber-optic quads, with every number labelled published, derived or assumed.
   - **The payload** is a generic box sized and massed by the class (0.5–2 kg), with no functional detail.
   - **OPSEC:** no unit, place or sortie detail; product names only where public.
2. **`drone.json`** (the physics engineer's):
   - K§5.2 re-derived on letter T's layout (D-043: the spool on top, the payload slung below, two packs along the sides; the spool underneath on the legs-off build), by the parallel-axis theorem: inertia ranges at seven loadouts, the CoM, and the coil's offset below it.
   - **The packs:** 6S or 8S Li-ion chosen from the research and `fc-profiles.md` (the OTU class runs 8S), each pack's cells and R_cell recorded.
   - The motors' Kv and the prop's k_T and κ from published data where it exists.
3. **The generator:** `tools/blender/generators/drone.py`, reading `drone.json` plus a look params file (colours, decals none, wear).
   - **Geometry:** the frame plates and arms with real thickness, the motors, 3-blade props as geometry, the coil drum and housing, the two packs, the payload box, the camera, the antennas and transceiver, the legs.
   - **Outputs:** LODs, the review sheet, and collision metadata for the gallery's noclip only; the flight's shapes come from `drone.json` through the core.
4. **The stand:** a `launch_stand` generator from `drone.json`'s motor positions and the coil's bottom, X base and posts in steel, with metadata (collision, `steel`, mass).
5. **In-view parts** (the tech-artist's):
   - **The props:** blades drawn as blurred discs blending to geometry by rpm (the blur's opacity from the blade-passage rate at the camera's exposure, derived).
   - **The parts:** the airframe parts in the camera's frustum checked against the FPV rig of `p4-feed`.
   - **The hooks:** leg flex and coil payout read from `PoseFrame` when `p6-flight-core` publishes them.
6. **The station:** both variants, a turntable and the stand, placed through `maps/gallery-g1/stations.json` (`p6-world-query-gallery`).

## Risks / Trade-offs
- **Public data on these airframes is thin.** Every gap is filled from the class (published components) and labelled assumed, and nothing is asked of the pilot that physics can compute (D-024).

## Migration Plan
- **The bootstrap:** `drone.json` replaces it, and `p6-flight-core`'s mass-build test is re-pinned to the new figures in the same commit.

## Open Questions
- None before G5. The look is the pilot's at G5.
