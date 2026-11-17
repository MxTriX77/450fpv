# Proposal: w17-four-legs

**Owners:** physics-engineer (the law), asset-artist (the model), game-developer (the player) · **Reviewers:** qa-engineer · **Requirements:** DRN-01, DRN-03, PHY-01, PHY-02, PHY-03, PHY-24, REAL-05, LOG-01, PLT-04 · **Decisions:** D-024, D-086, D-104, D-117 · **Tier:** Complex

## Why
The pilot, 2026-11-17, after about 30 flights on the model 103 build (their words are in Russian in D-117; rendered here):
- **The legs at liftoff look wrong.** There are "two parallel rails" on both sides of the legs. With them in the ground, at low throttle the drone "kind of slides along the ground", and with a little more throttle "it jumps up". It feels "not as if the legs stuck in the ground but as if the drone was pulled to the ground by a magnet. That is not it at all."
- **"These two rails are not needed. In life it is just four legs."** And that is where the sticking comes from: "when you land roughly enough, the legs can dig into the soil with their points".
- **The start:** "at the game's start they should be only slightly dug into the ground. And which leg how deep must be random, so that a liftoff on careful throttle feels different every time, so the pilot gets used to the craft on every flight."
- **The landing:** the drone behaves as heavy in flight, at liftoff and in its shake, "but at landing it still feels as if it were a small light plastic toy plane". Flight at low throttle is "fully realistic": "improve the current physics, but break nothing in it: it is made quite finely now."

The drone was modelled from letter T: four legs standing on two fore-aft skids (DRN-01). The pilot's own kit has four separate legs, and their account outranks the reading of a photo (D-104).

**The pilot's word on the legs' shape** (2026-11-17, D-119, which replaces D-118). Task 1.2 built four straight tubes 0.25 m tall. Photo U was then read as showing loops of bent stock about 0.10–0.13 m tall, the pilot answered "Copy the photo" when asked, and D-118 planned the loops. Shown that reading, the pilot corrected it at once with a new photo (W): the legs "shouldn't be bent loops … like on this photo - narrow tubes pointing perpendicularly into the ground". So the legs stay straight tubes, and photo W is their reference.

## What Changes
- **Four separate legs, as photo W shows them.** The two skid rails leave the model and `drone.json`. Each leg is a narrow straight tube under its arm, pointing perpendicularly into the ground and ending in its own tip. The collision follows what is drawn (REAL-05).
- **A leg's tip digs into soil by the soil's own law.**
  - Under the drone's weight and at a touchdown the tip penetrates by the surface's bearing resistance. A rough landing drives it deeper; hard ground takes none.
  - A dug-in tip resists sliding by the soil in front of it, so the drone does not run along the ground like a sled.
- **A dug-in leg holds without a magnet.** On the way out the hold is friction and adhesion on the shaft still in the soil. It falls to nothing as the tip comes out, and it stores no energy, so there is no jump when it lets go. Each leg leaves at its own moment: the drone tips toward the legs still held, as PHY-03 asks.
- **The start is each flight's own.** Each leg stands slightly in the ground to its own depth, a bounded draw of a named physical parameter from a seeded stream, logged (PHY-24). The same seed replays the same liftoff.
- **A landing has the drone's weight.** The energy of a touchdown goes into the soil under the tips, the legs and their clamps as the physics gives it. What the pilot calls "light" is measured first on their own flights (the rebound, the slide, the time to rest), then compared with the hand calculation for this mass on these legs.
- **Everything else stays.** The flight model is untouched. Every golden is re-recorded once, audited: with the old drone file and the old ground law each is the previous model's pin.

## Non-goals
- No change to the rotors, the air, the controller, the fiber or the wind.
- No new leg material or failure law (`p7g-legs` stays; a broken leg has no neighbour to hang from any more, so the hanging half leaves with the skids).
- Only the legs follow photo W. Its other differences from the model stay as they are.
- No tuning to a feel: every number is measured, published, derived or assumed and labelled.
- The stems' push on the legs (`w16-plants-in-the-air` 1.5) lands first; this change builds on it.

## Impact
- The asset: `tools/blender/generators/drone.py`, `assets-src/drone/D01_legs_on.json`, `assets-src/drone/drone.json` (the parts, the legs' contact shapes), the drone's object sheet, the Unity prefab and its review sheet.
- The sim core: `Runtime/Contact/` (the legs' contact with soil, the start's embedment, the events), `Runtime/Log/` (a header entry and the draws), the kept audits (the skids' drone as a fixture).
- The player: the drone's view and leg placement, the leg shots, the summary's names, the smoke test.
- Every legs-on golden moves with the drone's mass (the skids and their joints leave, the legs' place and size follow photo W) and with the law: one model number.
- The branch is `unity/physics/physics-integration-2--w17-four-legs`, cut from the physics line. It merges into the line, then the pilot's build branch.
