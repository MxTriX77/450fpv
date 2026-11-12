# Tasks: w11-world-map

Branch `unity/physics/physics-integration-2--w11-map`, cut from `--w3-preview` at 241145ea. Order: 1.1, then 1.2, then 2.1. It merges into `--w3-preview` after w10-play-maps.

## 1. The map

- [ ] 1.1 [world-builder] The top-down image per map.
  - A capture method that renders a map from above (orthographic, the friend's light for Village, the range's for House), from params per map: the extent in the sim's frame and the resolution. It writes `<map-id>.png` and its world-to-image transform (`<map-id>.json`: the extent in the sim's x and z, metres, and the image size).
  - The extent covers the map the pilot flies (Village: the 1 × 1 km map, and the surround if it is drawn; House: the range). Say what you chose and why.
  - Rendered at build time into a git-ignored folder and copied into the player; nothing of the friend's world is pushed (D-089). The folder is shared with w10-play-maps' stills: `unity/Generated/<kind>/` (here `unity/Generated/topdown/<map-id>.png` and `.json`), git-ignored, copied by the build into the player's `StreamingAssets/generated/<kind>/`.
  - The test range isn't in the player until w10-play-maps lands: render House's image from the test range's scene in the editor anyway.
  - Checks: a known point (Village's start, a placed object) lands on the image within one pixel through the transform; the build carries both; a look at both images; `precommit.py --tree HEAD`.
- [ ] 1.2 [game-developer] M and the panel.
  - M toggles the panel in flight (`FlightScene`) and in Explore (`FarmlandExplore`). Nothing pauses: the sim, the flight controller and the input run on.
  - The panel: centred, smaller than the window on every side, the map's image with the drone's (or the noclip camera's) marker and heading drawn every frame; an edge marker when the drone is off the image.
  - Reads the image and transform from 1.1's folder in the player (and in the editor); a plain panel with the marker if the image is missing.
  - A test that a fixed-input flight logs bit-identically with and without toggling M; a test of the transform.
  - Checks: EditMode for the new tests, the player build with its smoke PASS, `precommit.py --tree HEAD`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec scenarios: the identical logs with M toggled, the marker within a pixel, the panel's margins, nothing of the friend's world pushed, the panel's frame cost on a quiet machine, the smoke PASS.
