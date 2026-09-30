# Decision log

Status is one of Proposed, Accepted or Superseded. When a decision changes, add a new entry that supersedes the old one. Don't edit old entries.

## D-001 Engine: Godot 4.7, .NET build (C#) · Accepted
The flight dynamics model has to step at 1 kHz or more with fixed timesteps and log every step. C# handles that math-heavy loop many times faster than GDScript, and it avoids the build overhead of C++ GDExtension. GDScript is still fine for UI and glue code. The .NET 10 SDK is already installed. Accepted 2026-09-23: the user raised no objection and gave the go-ahead for the Godot bootstrap.

## D-002 Flight physics: custom model, Jolt for contacts only · Superseded by D-010
The drone's motion comes from our own fixed-step model: rotors, motors, aerodynamics, wind and tether, in seeded sub-steps. Godot's stock rigid-body integration gives the "smooth and rigid" feel that the manifesto rejects. Jolt is used only for collision queries and contact geometry.

## D-003 Godot project lives in `game/` · Accepted
Godot imports everything under its project root. Keeping the root at `game/` keeps Blender sources, reference media and docs out of the importer.

## D-004 Reference footage never enters git · Accepted
Raw flight footage is an OPSEC risk. Only derived notes and stills the user has cleared go in `docs/reference-notes/`. See `reference/README.md`.

## D-005 Asset pipeline: `.blend` in `blender/`, `.glb` exports in `game/assets/`, binaries in Git LFS · Accepted
Exports come from scripts in `tools/blender/` and are never edited by hand. Nobody needs Blender installed to run the game.

## D-006 Merge with `--no-ff`, never squash · Accepted
Squash merging rewrites authorship to whoever merges. Merge commits keep each role's history visible, as the manifesto asks.

## D-007 Spec-driven workflow via OpenSpec · Accepted
Every change is written down before anyone builds it, so nothing gets lost between agents or sessions. See [workflow.md](workflow.md).

## D-008 The video feed is simulated, not filtered · Accepted
This is the user's direction. The pilot's footage is the target look. Feed artifacts come from simulation state (motor current, voltage, vibration, impacts, tether, light, frame content), with measured random rates only where no cause is visible. Physics exposes those signals to the video module through an interface change that comes later.

## D-009 Terrain renderer: our own quadtree heightmap, not Terrain3D · Accepted
Decided by the renderer spike in `define-map-format` (task 1.1) on the synthetic 4 km map from `tools/map/make_synthetic.py` (seed 7, 4097² samples at 1 m, relief −16.2 to +8.7 m, crater fields, a gully, roads and a bank).

- **Candidate A, Terrain3D, was not measured because it fails the compatibility gate.** The newest official release is v1.0.2-stable (2026-05-19). Its asset is `Terrain3D_v1.0.2-stable.zip`, 42,312,488 bytes, SHA-256 `a071850250ec5e596aa54da61c01d75768774eb379ee997584d426a45f4884a2` as published by GitHub. The release says it supports Godot "4.4-4.6+", and the docs say 4.4–4.6 "and possibly later versions". No release states Godot 4.7 support, and the spike allowed a download only for one that does, so nothing was downloaded and nothing entered the repo. Two more costs: C# can reach it only through untyped `Call`/`Set`, and it is a C++ binary to re-pin on every Godot upgrade.
- **Candidate B is our own renderer, `game/src/world/HeightmapTerrain.cs`, and it meets the budget.**
  - The heights are one R16 texture. A quadtree draws one shared 32 × 32 grid patch at 1 m spacing near the camera, doubling with distance.
  - The vertex shader puts every vertex exactly on a height sample. Skirts hide the cracks between LODs, and normals come per pixel from the heights.
  - Collision is `HeightMapShape3D` in 256 m chunks.
  - About 240 lines of C# and 40 of shader, with no third-party dependency.

Measured on the dev machine: Lenovo 83S0 laptop, Ryzen 7 7735HS, RTX 4050 Laptop GPU (Godot chose it: Vulkan, Forward+), 1920 × 1080 144 Hz screen, Windows "Balanced" power plan on AC at 100 %, Godot 4.7.2 .NET. The run is `--selftest flypath`: a 1920 × 1055 window, vsync off, 2 s warm-up, then a fixed 12 s path (a quarter orbit of radius 200 m at 15 m, then a climbing quarter orbit to 150 m, about 52 m/s). One run each.

| Run | fps avg | 1 % low | Worst frame | Draw calls avg / max | Primitives max | Video memory | Load |
|---|---|---|---|---|---|---|---|
| B, synthetic 4 km | 430 | 245 | 12.9 ms | 24 / 34 | 417 k | 247 MB (+96 MB over empty) | terrain built in 2.3 s, first frame 4.7 s after engine start |
| Empty sandbox, reference | 577 | 171 | 23.3 ms | 7 / 7 | 198 | 151 MB | first frame 3.9 s after engine start |
| Budget (`map-loading` spec) | ≥ 90 | ≥ 60 | | | | | < 10 s |

The empty sandbox's lower 1 % low is run-to-run noise: a single 12 s run on a laptop, far above budget either way.
Collision check: 2,000 downward rays onto raw samples, including chunk seams and map corners, have a largest error of 0.11 mm and no misses.

Consequences:
- Terrain video memory grows only with the heightfield, at 2 bytes per sample (134 MB for an 8 km map). Mesh memory is constant.
- Surface materials and terrain holes (cellar entrances, dugouts) are built on this renderer by the loader (task 4.1) and later changes.
- Revisit Terrain3D only if a release states Godot 4.7 support and this renderer runs out of room.

## D-010 Flight physics: custom model with custom contacts; Jolt stays out of the flight loop · Accepted
This supersedes D-002, keeping its custom fixed-step flight model (≥ 1 kHz, seeded, logged). It adds that contacts and raycasts for the flight model are pure C# over catalog primitives (box, sphere, capsule, cylinder), the triangulated heightfield and wire polylines. The reasons, raised in the Physics Engineer's review of `define-map-format`:
- Godot's C# `PhysicsDirectSpaceState3D` calls allocate on every call.
- Those calls are unsafe off the physics thread.
- A headless replay from the physics log must run without Godot.
- We need control of cross-machine determinism.

It costs about 300 lines of closest-point code. Jolt keeps render-side and gameplay collision. The fallback, `PhysicsServer3D.BodyTestMotion` with reused objects, is only revisited if the benchmark (world-query W-16) fails.

## D-011 Weather is a fixed pilot setting: Calm, Windy or Severe · Accepted
This is the pilot's decision (2026-09-24). It adds a fourth setting to the manifesto's payload, legs and time of day: **Weather = Calm / Windy / Severe**, always chosen by the pilot, with no random option. Within the chosen level, the wind itself stays alive and unpredictable: a prevailing direction with random shifts, gusts, turbulence near obstacles, per `docs/reference-notes/wind.md`. Rain goes with the weather level. The Severe preset is held to the measured targets from clip P (wind study §6). Windy uses the derived "typical windy" band.

## D-012 Real models come before any gallery; the world map is built once, with them in it · Accepted
This is the pilot's decision (2026-09-29), asked for directly: they are impatient to noclip a realistic world, and were offered three routes. They chose **real models first, then the world**.

What it means for the plan:
- Section 3 of `build-uat1-parts` (tree belt, adobe house and yard, vehicles, the five-storey block and its rubble, poles, wires and debris) is the next work, ahead of anything else. Task 2.6 (LOD switching) goes first only because the far tree impostor in 3.1 needs its mechanism.
- The 512 m UAT-1 gallery (tasks 4.1 and 4.2) is **no longer assumed**. Its purpose was to show the pilot the parts; if the parts can be reviewed in the world map itself, the gallery is a step that buys a review device rather than the product. Decide when section 3 lands, not now.
- `build-world-map` is then built once, with real assets in it from the pilot's first flight, rather than an empty layout that fills in later.

**Resolved 2026-09-30.** The pilot asked how the finished objects would be shown to them and said they had no preference, leaving the call here. The gallery stays: `uat1_gallery` is built as specified, with a station per object, a patch per terrain kind and labels within 25 m. Its cost is one generator, and it is the only way to be sure the pilot has looked at every asset before the same assets are placed thousands of times across 4 km. Objects also keep landing in `sample_patch` as they are finished, so nothing waits for the gallery to be flown.

What the pilot can fly meanwhile, offered on the same day: `sample_patch` (256 m, real ground, placeholder objects) and the synthetic 4 km terrain package (terrain only, 602 fps avg). Neither is a gate.

## D-013 The analog feed shipped as a spike, on purpose, for demos · Accepted with debt
The pilot asked on 2026-09-30 for a working analog video feed quickly, to demo the project to people, and said explicitly that this breaks the project's own rules and should be completed or redone properly later. Recorded here so the debt is visible rather than forgotten.

**What was skipped, knowingly:** no OpenSpec change, no spec scenarios, no tests, no QA review, no pilot gate. It went spike → chosen from screenshots → built → merged in one sitting.

**What it is:** one mixed feed. Baseline from candidates 1 and 2 (composite structure and colour, cleanly tunable), stochastic drops into candidate 3's degraded look, and short grain-only cuts. Rates are named constants at the top of `game/src/video/FeedEvents.cs`: cuts measured 3.8/min at 60–160 ms, drops 7.8/min at 0.3–2.2 s. Randomness is seeded and reproducible; `--video-seed` and `--video-field` pin a still.

**What is not true of it yet, and matters:**
- **Nothing physical drives it.** `FeedSignals.cs` stands in — distance from the map origin for fiber sparkle, camera speed for motor noise, a constant gain where an auto-exposure loop belongs. D-008 says the feed is simulated, not filtered, and it is not yet simulated from anything real.
- **Losses of picture are random.** `video-feed.md` measures no random rate: every loss in the footage followed a close approach, landing or contact. `FeedEvents.TriggerCut()` is the hook for the flight model. The random rate is the pilot's deliberate demo choice.
- **Performance is unverified.** The one trustworthy measurement was 345 fps clean against 310 fps for candidate 1 (~0.33 ms); the mixed feed is estimated at 0.5–0.8 ms. QA must re-measure on a quiet machine on AC before the 60 fps budget is claimed.
- **The feed is on by default in the sandbox**, so world-artist and QA screenshots now carry it unless `--video clean` is passed.

**When redone properly:** render into a SubViewport at field resolution (about a tenth of the per-pixel cost, and it removes the tap-level aperture workaround); a real AE/AWB loop, so the pilot's "colours change as you get closer" exists at all; lens distortion; the staged loss with the blue no-signal screens; and `FeedSignals` replaced by the physics interface.
