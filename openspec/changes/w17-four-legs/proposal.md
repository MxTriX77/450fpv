# Proposal: w17-four-legs

**Owners:** physics-engineer (the law), asset-artist (the model), game-developer (the player) · **Reviewers:** qa-engineer · **Requirements:** DRN-01, DRN-03, PHY-01, PHY-02, PHY-03, PHY-24, REAL-05, LOG-01, PLT-04 · **Decisions:** D-024, D-086, D-104, D-117 · **Tier:** Complex

## Why
The pilot, 2026-11-17, after about 30 flights on the model 103 build (their words are in Russian in D-117; rendered here):
- **The legs at liftoff look wrong.** There are "two parallel rails" on both sides of the legs. With them in the ground, at low throttle the drone "kind of slides along the ground", and with a little more throttle "it jumps up". It feels "not as if the legs stuck in the ground but as if the drone was pulled to the ground by a magnet. That is not it at all."
- **"These two rails are not needed. In life it is just four legs."** And that is where the sticking comes from: "when you land roughly enough, the legs can dig into the soil with their points".
- **The start:** "at the game's start they should be only slightly dug into the ground. And which leg how deep must be random, so that a liftoff on careful throttle feels different every time, so the pilot gets used to the craft on every flight."
- **The landing:** the drone behaves as heavy in flight, at liftoff and in its shake, "but at landing it still feels as if it were a small light plastic toy plane". Flight at low throttle is "fully realistic": "improve the current physics, but break nothing in it: it is made quite finely now."

The drone was modelled from letter T: four legs standing on two fore-aft skids (DRN-01). The pilot's own kit has four separate legs, and their account outranks the reading of a photo (D-104).

**The pilot's decision on the legs' shape** (2026-11-17, D-118). Task 1.2 first built four straight tubes 0.25 m tall. Photo U, the pilot's own kit, shows something else: each leg is a loop of bent stock fixed under its arm at two points and ending in a point, standing only about 0.10–0.13 m tall. Asked which the sim's drone should have, with the cost named (a new leg law, and a shorter canister under the frame so it clears the ground), the pilot answered: "Copy the photo".

## What Changes
- **Four separate legs, the pilot's kit's own.** The two skid rails leave the model and `drone.json`. Each leg is a loop of bent stock fixed under its arm and ending in a point, standing as low as photo U shows. The collision follows what is drawn (REAL-05).
- **The canister clears the ground.** What hangs under the frame is re-proportioned to what photo U shows, with the coil's mass unchanged, so that only the legs' points touch level ground. The fiber's exit follows the canister.
- **A leg's tip digs into soil by the soil's own law.**
  - Under the drone's weight and at a touchdown the tip penetrates by the surface's bearing resistance. A rough landing drives it deeper; hard ground takes none.
  - A dug-in tip resists sliding by the soil in front of it, so the drone does not run along the ground like a sled.
- **A dug-in leg holds without a magnet.** On the way out the hold is friction and adhesion on the shaft still in the soil. It falls to nothing as the tip comes out, and it stores no energy, so there is no jump when it lets go. Each leg leaves at its own moment: the drone tips toward the legs still held, as PHY-03 asks.
- **The start is each flight's own.** Each leg stands slightly in the ground to its own depth, a bounded draw of a named physical parameter from a seeded stream, logged (PHY-24). The same seed replays the same liftoff.
- **A landing has the drone's weight.** The energy of a touchdown goes into the soil under the tips, the legs and their clamps as the physics gives it. What the pilot calls "light" is measured first on their own flights (the rebound, the slide, the time to rest), then compared with the hand calculation for this mass on these legs.
- **Everything else stays.** The flight model is untouched. Every golden is re-recorded once, audited: with the old drone file and the old ground law each is the previous model's pin.

## Non-goals
- No change to the rotors, the air, the controller, the fiber or the wind.
- No new leg material (D-086: plastic). The loop's bending and failure law is derived from its shape by `p7g-legs`' method. A broken leg has no neighbour to hang from any more, so the hanging half leaves with the skids.
- Only the legs and the canister follow photo U. Its other differences from the model stay as they are.
- No tuning to a feel: every number is measured, published, derived or assumed and labelled.
- The stems' push on the legs (`w16-plants-in-the-air` 1.5) lands first; this change builds on it.

## Impact
- The asset: `tools/blender/generators/drone.py`, `assets-src/drone/D01_legs_on.json`, `assets-src/drone/drone.json` (the parts, the legs' contact shapes), the drone's object sheet, the Unity prefab and its review sheet.
- The sim core: `Runtime/Contact/` (the legs' contact with soil, the start's embedment, the events), `Runtime/Log/` (a header entry and the draws), the kept audits (the skids' drone as a fixture).
- The player: the drone's view and leg placement, the leg shots, the summary's names, the smoke test.
- Every legs-on golden moves with the drone's mass and layout (the skids and their joints leave, the legs and the canister change shape) and with the law: one model number.
- The fiber's exit moves with the canister (`p7d-tether`'s exit design reads its place), and the launch stand is checked against the new canister (DRN-07).
- The branch is `unity/physics/physics-integration-2--w17-four-legs`, cut from the physics line. It merges into the line, then the pilot's build branch.
