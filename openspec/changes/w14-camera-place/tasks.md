# Tasks: w14-camera-place

Branch `unity/physics/physics-integration-2--camera-place`, cut from `--props-in-frame` at a31a6753. Order: 1.1, 1.2, 1.3, 1.4, then 2.1. It merges into `--w3-preview` together with `w13-props-in-frame`.

## 1. The camera's place

- [x] 1.1 [reference-analyst] **Where the camera sits,** read from the footage and the published frame. Research only: no model change.
  - **The rods.** From the clips of the sim's airframe group (`docs/reference-notes/vegetation-on-the-props.md` §3.1, the row of C, D, E, F, H, L and P): where the two pale rods sit in the picture, both ends of each, in the feed's own coordinates, with their spread across the clips.
  - **The blades.** Where and how often a blade enters in the same clips (§3.1 has the counts; add where in the picture).
  - **Which parts they are.** Which parts of letter T's airframe the rods are (`docs/object-sheets/drone.md`), and whether both builds carry them.
  - **The published frame.** Where the class frame's camera mount is against its front motors, from the maker's drawings or photos actually read.
  - **The fit.** The lens's place (y and z; x = 0, 0° uptilt) that puts those parts where the footage has them, through the feed's projection (FEED-02: f = 1.005 half-widths, k1 = 0.33, 16:9) and `drone.json`'s geometry, with its residual. Then what that place predicts for the blades: the share of a turn a front blade is in the picture, and where (`w13-props-in-frame`'s design.md §1 gives the method).
  - **If no single place fits both** the rods and the blades, say so and what else would have to differ (the parts' own place, a camera below the plates), instead of forcing a fit.
  - Written to `docs/reference-notes/camera-place.md`: the measurements, the fit, the recommended `camera.lens_m` with its label, and what in the model has to move. Letters only.
  - **As built:** `docs/reference-notes/camera-place.md`, with `tools/reference/camera_place.py` and its test. **lens_m (0, −0.020, 0.205) m, derived:** a front blade in the picture for 0 % of a turn (1.4 % at the top corners if 5 mm further back), where the old place gives 34 %. The camera sits on a nose mount under the bottom plate, 45 mm ahead of the front motors. The rods are two thin pale parts (at most about 2 mm thick) parallel to the lens's axis, not part of T; they fix ratios only, so they are placed at an assumed 0.06 m side offset. P fits a lens 55 mm further back, so the group does not share one z, and the sim takes C–H's. The published frame's pages give no camera place, and its assembly drawing could not be rendered here.
- [ ] 1.2 [asset-artist] **The camera mount and `camera.lens_m` at the fitted place.**
  - D01's generator params and `drone.json` (the lens, and any part the mount moves or adds), rebuilt with `pwsh tools/build_asset.ps1 D01`, both leg variants. Look at the review sheet.
  - The parts the note names at mid-height are geometry the first-person camera sees.
  - Checks: `python -m pytest tools`, `precommit.py --tree HEAD`.
- [ ] 1.3 [physics-engineer] **What the change moves in the physics.** The mass build and the camera point's signals (the modes' turn at the camera, the air velocity for the lens drops): every golden that moves, with its cause, in design.md. The next free model number if a flight golden moves.
  - Checks: `dotnet test tools/simtests`, `flightbench --golden`, the feed's tests (`Fpv450.Feed.Tests`).
- [ ] 1.4 [tech-artist] **The props' captures again** (`Fpv450.PropFields`), 24 fields a case, set beside the group's letters in the local-only compare folder: the blades' share and place, and the rods, against the note's numbers. Look at it.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec scenarios: the label, the blades' share against the fit, the rods' place, the golden audit, the comparison sheet, the review build's smoke PASS.
