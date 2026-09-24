# Decision log

Status is one of Proposed, Accepted or Superseded. When a decision changes, add a new entry that supersedes the old one. Don't edit old entries.

## D-001 Engine: Godot 4.7, .NET build (C#) · Accepted
The flight dynamics model has to step at 1 kHz or more with fixed timesteps and log every step. C# handles that math-heavy loop many times faster than GDScript, and it avoids the build overhead of C++ GDExtension. GDScript is still fine for UI and glue code. The .NET 10 SDK is already installed. Accepted 2026-09-23: the user raised no objection and gave the go-ahead for the Godot bootstrap.

## D-002 Flight physics: custom model, Jolt for contacts only · Proposed
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

## D-011 Weather is a fixed pilot setting: Calm, Windy or Severe · Accepted
This is the pilot's decision (2026-09-24). It adds a fourth setting to the manifesto's payload, legs and time of day: **Weather = Calm / Windy / Severe**, always chosen by the pilot, with no random option. Within the chosen level, the wind itself stays alive and unpredictable: a prevailing direction with random shifts, gusts, turbulence near obstacles, per `docs/reference-notes/wind.md`. Rain goes with the weather level. The Severe preset is held to the measured targets from clip P (wind study §6). Windy uses the derived "typical windy" band.
