# Proposal: p7g-legs, the legs take damage

**Owner:** physics-engineer. **Reviewers:** asset-artist (the legs' data and their drawn states), game-developer (the summary and the app's drawing hooks), qa-engineer.

## Why

The pilot flew the model-26 build (D-079). A wall strike bounces the drone off realistically, but nothing breaks, and real landings fall a bit more often than the sim's. They asked for a health on the legs as well as the props: a leg should "bend if the land is too harsh or not parallel to the ground enough", break, or come off the drone.

Today every leg part (`leg_*`, `tjoint_*`, `skid_*`) is an elastic spring of 10 kN/m that never yields (`drone.json`'s `legs` block). No impact, however hard or tilted, changes it. This change gives the legs the failures the class's real legs have, so a hard or crooked landing can leave a bent, cracked or missing leg, and the next landing tips more easily.

**Motivation:** VISION §1.1 (the lift-off and landing on legs) and §2.6 (landings), PHY-01, PHY-02 (the bounce from the legs' compliance), PHY-04 (unexpected ground), PHY-24 (randomness only on named physical parameters), DRN-05 (leg flex as an animation hook), LOG-01–LOG-03 (the log, replay, forensics), and the pilot's D-079.

## What Changes

- **The class's legs as data.** For each leg, the tube, the clamp under the arm and the T-joint on the skid get their material, section, strengths and stiffnesses, each labelled with its source (`drone.json`'s `legs` block). Two references disagree: T's photo shows carbon tubes, while the pilot's earlier words were "plastic, 25–35 cm, a little soft". The research settles this before anything is built.
- **A leg's state.** Each leg gets a state, its "health": intact, bent (a lasting set angle at the clamp, or along a yielding tube), cracked (weaker and softer), broken (the tube parted below the clamp), or detached (the clamp let go). A skid's T-joint can also let its skid slip or come off.
- **The damage law.** Each step reads the load on each leg part from the contact model (force, moment at the clamp, axial and side shares). Each failure mode compares that load with its own capacity: yielding, a clamp slipping or rotating, fracture, and a joint pulling out. A strength is drawn once per flight per leg, within published bounds (PHY-24).
- **The consequences.**
  - A bent leg moves its contact shapes and foot, and the drone stands crooked.
  - A cracked leg is softer.
  - A broken leg keeps only the stub above the break, and the parted piece falls as a loose body.
  - A detached leg leaves the drone with its mass, and the build's centre of mass and inertia are rebuilt.
  - The next landing feels all of it through D-067's landing law, unchanged.
- **Logged, replayed, shown.**
  - New header values, the leg-state record and events, each with an assigned number (D-072, D-074).
  - Replay is bit for bit.
  - The pause menu's summary names each damaged leg and what did it.
  - `PoseFrame` carries each leg's state, so the app can draw a bent leg or a missing one.

## Capabilities

### New Capabilities
- `leg-damage`: the legs' strengths and failure modes, the state each leg carries, how a damaged leg changes the contacts and the build, and the log, replay and summary for it.

### Modified Capabilities
None in `openspec/specs/`. The contact and landing requirements this change builds on live in the unarchived `p6-flight-core` and `p7a-ground` changes. Where this change alters their behaviour (a leg that yields instead of springing), its scenarios say so, and `p7a-ground` 1.17's leg-spread harness reads the leg state.

## Non-goals

- Prop and motor damage. That is `p7f-damage`, which this change shares its damage framework with (the strength streams, the crash rule's seam), not its scope.
- Arms, the frame, the coil or the bottle breaking.
- Repairs between flights. Every flight starts with intact legs; restart (R, D-078) resets them.
- Tuning toward a crash or fall rate. The rates follow from the loads and the strengths (D-024, D-079).
- The look of a damaged leg beyond `PoseFrame`'s state. Drawing it is an app task here.

## Impact

- **Sim core** (`unity/Packages/com.fpv450.sim`):
  - `Runtime/Contact`: the loads on each leg part, the yielded shapes
  - `Runtime/Flight`: the build's rebuild when a leg is lost, `DroneSpec`'s legs block
  - `Runtime/Log`: the header, the record and the events
  - A new `Runtime/Damage/Legs` (or beside `p7f`'s damage code, whichever has landed)
- **Data:** `assets-src/drone/drone.json`'s `legs` block, plus `physics_materials.json` rows if the legs' materials need them.
- **Tools:** `flightbench` gains a `--legs` mode; `tools/replay` reads the leg state.
- **App:** `FlightScene` draws a bent or missing leg from `PoseFrame`; the summary lists leg damage.
- **Numbers:** the change moves the model and the log format. Its numbers are assigned by the orchestrator when its first code lands (D-072, D-074).
