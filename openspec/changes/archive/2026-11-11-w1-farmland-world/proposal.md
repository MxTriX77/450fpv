# Proposal: w1-farmland-world

**Owner:** world-builder (scene, terrain, vegetation, village) · **Also building:** game-developer (the menu, the start point, the session) · **Reviewers:** tech-artist (our pipeline and the feed), qa-engineer; **the pilot when they fly the farmland (D-093)**
**Requirements:** WLD-01, WLD-02, WLD-04, WLD-05, WLD-06, WLD-08 (as D-092), AST-09, PLT-02, PLT-05, PLT-06 · **Decisions:** D-088, D-089, D-092, D-093 · **VISION:** §2 (flying low over real ground, among trees and ruins), §3 (one map, light on the machine) · **Plan:** `docs/final-world-plan.md` §4, unit W1

## Why
The pilot adopted a friend's 1 × 1 km farmland as the map, as it is (D-088). It is a separate Unity project in the local vendor store (W0). The player still flies the test range.

## What Changes
- **The world in our project** (world-builder): the scene, the terrain tiles (the map, the surround and the far ring), the grass, trees and bushes, and the ruined village come into `unity/`.
  - `tools/vendor/link.ps1` links the third-party packs into `unity/Assets` and keeps them local-only (D-089). The friend's own text is pushed: code, shaders, scenes, prefabs and materials.
  - Our URP pipeline asset and renderer stay (the feed). Packages only as the world needs them.
  - The friend's generator (`FarmLandBuilder`) comes along, so the world can still be rebuilt from its seed.
- **The player on the farmland** (game-developer): the menu opens the farmland in noclip. One start point, in the village on free level ground (D-092), carries the launch stand when legs are off. Flight stays on the test range until W3 gives the sim the hills; the switch and the test range's exit from the player come with W3.

## Non-goals
- No physics data for the trees, bushes or ruins (W4a, W4b), no map export (W2), no hilly ground in the sim (W3), no look or frame-rate tuning beyond a working scene (W6).
- No change to the friend's look: the pilot wants it as it is.

## Impact
`unity/Assets` (a new world folder, scenes, the menu), `tools/vendor/link.ps1`, `.gitignore`, `tools/assets/manifest.json`, `unity/Packages/manifest.json`. The repo grows by the friend's text only; every third-party binary stays local.
