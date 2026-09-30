# Proposal: build-flight-model

**Owner:** physics-engineer · **Reviewers:** qa-engineer (scenarios, replay forensics, budget), world-artist (world-query consumption), game-developer (setup options, stick path), tech-artist (the signals the feed will later read)
**Motivation:** CLAUDE.md §2 is a list of six things the pilot says no simulator gets right, and every one of them is this change: the liftoff shake and a leg sticking (§2.1), unexpected ground on landing (§2.2), turbulence near woods and gaps (§2.3), motors drifting a tad bit out of sync so the drone yanks, plus the inertia of a heavy airframe (§2.4), the fiber tether changing how you fly (§2.5), and the landing bounce off plastic legs when you hold the sticks after touchdown (§2.6). §2 also sets the method: "a tiny, cunning, and elegant randomness" instead of a mainframe's worth of physics. §4.2 asks for the flight model with wind in it, and §4 asks that every straw and nook react with the quad. §3 fixes the pilot's settings: payload, legs on/off, time of day (plus weather, D-011). The manifesto's closing line requires physics logging good enough to tell a bug from pilot error.

## Why

The world is built and queryable: `world-query` already answers what is under a foot, which straws touch it, what the soil does and what blocks the wind, deterministically and fast enough for a 1 kHz step. Nothing consumes it yet. The drone does not fly.

This is also the change where the project's central claim is either earned or lost. Every other sim "claims to have good physics" and delivers something smooth and rigid. The difference is not more force terms; it is that the disturbances are **physical and seeded** — a motor that is genuinely 1.5 % hotter than its neighbour, a leg that genuinely caught on a straw with a known release force — rather than noise added to the pose. Noise on the pose is indistinguishable from the real thing for about ten seconds and then feels wrong forever, because it does not respond to what the pilot does about it. `wind.md` T11b exists precisely to fail a model that cheats this way.

Doing it now, before the world map is finished, also means the pilot's MVP-1 flights land on a model that is already structurally right, so their feedback tunes numbers instead of forcing rewrites.

## What Changes

- A **fixed-step rigid-body flight model** in `game/src/physics/`, stepping at ≥ 1 kHz, deterministic per seed, off the render thread and out of Godot's integration (D-002, D-010). Mass and inertia are built from a component model at the pilot's payload setting, not a single tuned number.
- **A propulsion chain** per rotor: rate controller → mixer → ESC → motor → rotor. Thrust and torque curves from momentum theory with a figure of merit, first-order motor and ESC lag, pack voltage sag under load, and the rotor-inflow effects that change what a heavy quad does near the ground and in descent (ground effect, vortex ring state).
- **Motor asynchrony as a physical parameter**, not a wobble: per-motor Kv spread, ESC timing jitter and prop imbalance, drawn once per flight from the seed and bounded. The yank the pilot describes (§2.4) is then the drone's honest answer to a real force offset, and the pilot's roll and yaw corrections work on it the way they do in the air.
- **Ground contact** consuming the existing surface parameters and micro-detail: the elasto-plastic soil and mat solve already signed off in `define-map-format` (`surfaces-review.md` §1), grass and twig forces, pitfalls, ploughing and stick-point friction. No new surface fields.
- **Legs** as compliant plastic members with their own stiffness and damping, plus the two behaviours the pilot named:
  - a leg **snagging or sticking at liftoff**, from a real hook on a real straw with a real release force, or from stiction in soil it has sunk into, so the horizon tips and needs an immediate correction (§2.1)
  - the **landing bounce** (§2.6): hold the sticks after touchdown and leg compliance plus residual thrust throws the drone back up, so the pilot has to catch the moment and cut throttle
- **Launch rails** for the legs-off configuration (§3), where the airframe rests on two steel bars so the spool clears the ground.
- **A wind field** driven by the existing wind-obstacle grid and the Calm / Windy / Severe presets (D-011): a log-law mean profile, a per-flight prevailing direction and strength drawn from the seed, seeded broadband turbulence, obstacle wakes and gusts, and jets through gaps. It is held to the quantitative targets in `wind.md` §6.
- **A fiber-optic tether**: a spool paying out with its own tension, a paid-out span that hangs, drags and vibrates in the wind, bend over edges, mass leaving the airframe as it unwinds, and a **risk** of the line cutting — never a certainty — from tension, bend radius or abrasion.
- **One randomness architecture** for all of the above: a documented list of every place a random number enters, each one a bounded perturbation of a named physical parameter, all drawn from one flight seed through per-stream generators, and every draw written to the log.
- **A physics log and a headless replay**: a binary log at the step rate with a readable header, a `--replay` tool that reproduces a flight bit-for-bit **without Godot** (D-010), and enough recorded state that QA can answer "why did it crash" with a cause, not a guess.

## Capabilities

### New Capabilities
- `flight-dynamics`: frames, state, the mass and inertia build from the pilot's settings, airframe aerodynamics, the fixed-step integrator, cross-machine determinism and the step budget
- `propulsion`: the rate controller and mixer, ESC and motor lag, battery sag, thrust and torque curves, per-motor asynchrony, ground effect and vortex ring state
- `ground-contact`: the soil, mat and cover contact solve, legs with snag and bounce, launch rails, pitfalls, friction and ploughing, and tunnel detection
- `wind-field`: the mean profile, seeded turbulence, obstacle wakes, gusts, gap jets, and the Calm / Windy / Severe presets against the `wind.md` §6 targets
- `fiber-tether`: the spool, payout, span drag and vibration, tension, bend, mass loss and the cut risk
- `flight-randomness`: the seed architecture, the complete catalogue of perturbations with their bounds, and reproducibility
- `physics-log`: what is recorded, at what rate, the headless replay, and the forensic questions a log must answer

### Modified Capabilities
_None._ `world-query` is consumed as it stands. If the flight model turns out to need a field the world does not expose, that is a separate change against `world-query`, not a silent edit here.

## Non-goals

- **Final numbers.** Almost every parameter here starts from a derived or assumed value, and `design.md` says which. The pilot's MVP-1 flights tune them. This change owns the structure and the acceptance procedure, not the tuning.
- **The physics → video interface** (D-008). The model computes and logs the signals `video-feed.md` §"physics inputs" lists (motor current, pack voltage, camera rates and vibration, impacts, fiber tension, bend and link state), but handing them to `game/src/video/` is the later interface change.
- **Stick input and the TX12.** The model takes normalised stick commands. Reading the radio, calibration and rate curves at the UI level belong to `game-developer`.
- **The drone's visual model, scene wiring and OSD.** Physics publishes a pose and state; `game-developer` and `world-artist` render it.
- **Damage and failure beyond the tether.** Prop strikes and impacts are logged as events with their impulse, but a damage model (a chipped prop that changes its thrust curve, a broken arm) is out of scope.
- **Flight modes other than acro.** The pilot flies rate mode (`wind.md` Q3). No self-levelling, angle mode, altitude hold or GPS anywhere in the loop.
- **Multiple airframes.** One drone, per §3. Payload, legs and weather are the only physical settings.
- **Runtime terrain deformation.** Footprint compaction lives in the flight model's own ring buffer, as `define-map-format` decided; the world stores no marks.

## Impact

- New: `game/src/physics/` (the model, propulsion, contacts, wind, tether, randomness), `game/src/telemetry/` (the log writer and reader), `tools/flightbench/` (a headless harness for the acceptance runs, the replay check and the step budget, alongside `tools/worldbench/`)
- Consumes, unchanged: `world-query` (`SampleGround`, `MicroDetailNear`, `StaticContacts`, `Raycast`, `WindGrid`, `GapsNear`, the surface, material and soil-reference lookups, `ContentHash`, `QueryVersion`), `game/maps/surfaces.json`, `game/assets/catalog.json`
- Inputs: CLAUDE.md §2–§4, `docs/reference-notes/airframes.md` (masses, motors, props, packs), `docs/reference-notes/wind.md` (§5.5 inertia and thrust, §6 the acceptance targets), `openspec/changes/archive/2026-09-29-define-map-format/surfaces-review.md` §1 (the contact solve, already signed off by this role) and `api-review.md` §4 and §6 (the swept-contact response and the replay obligations)
- Depends on `define-map-format`, which is merged and archived. It does **not** depend on `build-uat1-parts` or the world map: the acceptance runs use `sample_patch`, the synthetic terrain and purpose-built flat and single-obstacle test worlds.
- Feeds: MVP-1 (the pilot's first flight), the physics → video interface change, and QA's crash forensics
- No new third-party dependency. Everything is pure C# over `world-query`.
