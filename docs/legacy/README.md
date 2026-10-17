# Legacy: designs and specs from the Godot attempt

These are copied verbatim for reuse. They are engine-independent at heart, but they name Godot paths and the old decision numbers. Port the ideas; don't follow the paths.

| Path | What it is | Use in the restart |
|---|---|---|
| [video-feed-godot/](video-feed-godot/) | **The Godot analog feed the pilot chose**: the shader (`video_feed.gdshader`), the event scheduler, the loss state machine, the OSD and its font, and the three spike candidates, verbatim, with a port spec | **Port it faithfully in phase 4** (D-026, FEED-00) |
| [flight-model/](flight-model/) | The physics engineer's `build-flight-model` proposal, design, tasks and seven specs (flight dynamics, propulsion, ground contact, wind field, fiber tether, randomness, physics log), drafted 2026-09-30 and never applied | **The starting design for phases 6–7.** It is missing PR-1, PR-3, PR-6, PR-9, the input-timing contract and the 2026-10-17 drone layout (see knowledge §6.12) |
| [map-and-world/map-format-spec.md](map-and-world/map-format-spec.md) | The archived map-format spec | Starting spec for phase 3, with the D-007/D-019 changes |
| [map-and-world/world-query-spec.md](map-and-world/world-query-spec.md) | The archived world-query spec | Starting spec for phase 3 (WLD-11) |
| [map-and-world/map-loading-spec.md](map-and-world/map-loading-spec.md) | Load-time and fallback spec | Reference |
| [map-and-world/surfaces-review.md](map-and-world/surfaces-review.md) | The physics review of the surface table: the leg–ground contact solve, materials, the world-query requirements | **The contact model** for phase 7a |
| [map-and-world/api-review.md](map-and-world/api-review.md) | The physics review of the world-query API: the Time < 1 latch rule, the contact probe results, determinism for replay | Phase 3 sign-off and phase 6 contacts |
| [map-and-world/terrain-holes-review.md](map-and-world/terrain-holes-review.md) | Terrain holes (cellar shafts, dugouts): rendering, collision, queries | Phase 3 |
| [map-and-world/surfaces.json](map-and-world/surfaces.json) | The surface table: 10 surfaces with soil, relief, pitfalls, cover and mat | Starting values for S01–S10 |
| [map-and-world/catalog-materials.json](map-and-world/catalog-materials.json) | Object contact materials (steel, sheet_metal, timber, masonry, cable) | Starting values |
| [godot-decisions.md](godot-decisions.md) | The old decision log, D-001 to D-017 | History; knowledge §12 maps it to the new log |
| [godot-last-report.md](godot-last-report.md) | The last status report (2026-09-30) | History and lessons |
| [godot-cc0-credits.md](godot-cc0-credits.md) | The CC0 texture sets the first attempt used, with their purposes | Phase 1 material shopping list |
