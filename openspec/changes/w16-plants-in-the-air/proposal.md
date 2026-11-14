# Proposal: w16-plants-in-the-air

**Owners:** reference-analyst (research), physics-engineer (the law, the drone's side), tech-artist (the drawn side, the cut pieces) · **Reviewers:** qa-engineer · **Requirements:** AST-09, AST-10, VIS-05, VIS-06, PHY-05, PHY-13, PLT-02, PLT-04, REAL-01 to REAL-05 · **Decisions:** D-088, D-102, D-104, D-111, D-114 · **Tier:** Complex

## Why
The pilot, 2026-11-14, after flying the model 100 build:
- "grass still doesn't get affected by propwash, neither do leaves and twigs. they should. and there's not effect of cutting grass straws leaves when props cut them."
- "the drone is not affected by grass, and it should be affected even more heavily when thicker stuff (like twigs and tree leaves)."
- "let's do that sway matches real wind behavior real time." On the gusts' direction: "it should match this also".
- "when i say swayed by wind - i mean reaslistically. they are flexible, so they should flexibly get back to their default position if a gust is short etc., again - calculate and make it realistic"
- "about grass cutting, swaying because of propwash, dusts whiffs as you land closer to the ground - refer to [clip H] - towards the end you will see short examples"

Today the drawn plants move by their shaders' own wind, which has nothing to do with the air the drone flies in, and not at all in the props' wash. What the props cut exists only in the physics, the log and the summary. The stems act on the props but not on the legs or the body (`w9-village-physics` 2.10 is open), a crown's leaves act only on the blades, and the stems' wash is built and switched off (D-111, `w9-village-physics` 2.13 and 2.14).

## What Changes
- **One air for everything.** The drawn plants move in the sim's own air, sampled where each plant stands, in real time:
  - the wind field the drone flies in: its prevailing direction, the direction's meander and shifts, its turbulence by height, and the belts' and ruins' wakes (`WindField`, model 100);
  - the props' wash: the near wake, the merged jet and the wall jet along the ground (`RotorAir`).
- **Plants as flexible bodies, calculated.** Each plant class bends as damped modes with its own natural frequency, damping and drag reconfiguration, from published plant biomechanics. The classes are grass blades and culms, straw, weeds, the hedges' shrubs, and the trees' trunks, branches, twigs and leaves.
  - A short gust bends a plant and it springs back with its own period and decay; a steady wind leans it.
  - The wash flattens the grass under the drone and ripples it outward.
  - Every number is labelled measured, published, derived or assumed.
- **What the props cut is seen.** Cut stems, straw and leaves leave the discs as pieces at the blade's speed, from the sim's cut events (`w9-village-physics` 2.5 moves here).
- **The wash raises dust.** Low over dry ground, whiffs of dust lift where the wash's speed along the ground passes the soil's threshold for lifting grains, and grow as the drone comes lower (VIS-06). The thresholds come from published dust-entrainment and rotorcraft brownout research, per surface in the surface table.
- **Clip H's ending is the check.** The pilot's clip H shows short examples of all three: grass cut by the props, grass swaying in the wash, and dust whiffs as the drone lands.
- **The drone feels what it flies through:**
  - stems on the legs and the body (`w9-village-physics` 2.10);
  - a crown's leaves and twigs on the body and the props;
  - the stems' wash in the physics, after `w9-village-physics` 2.14.

  So skimming grass drags the drone, and a crown holds it more than grass does. The plants that touch the drone are the physics' (one stem field, D-102), and the drawn ones match them (AST-10).

## Non-goals
- No change to the wind field's law (`p7c-wind`) or the wash's law (`p7d-tether` designs 19 and 20; `w9-village-physics` 2.14 owns the near wake): this change reads them.
- No new plant species and no new placement: the friend's world stays as it is (D-088).
- No turning props or camera move (W13, W14).

## Impact
- A research note `docs/reference-notes/plants-in-the-air.md` and a design in this change's `design.md`.
- The sim core: an engine-free law of the plants' motion in the air (deterministic, DetMath, 0 B), shared by the physics where plants touch the drone and by the drawn side.
- The Unity side: the friend's vegetation shaders take the sim's air instead of their own wind (the material overrides live in the project, never in the local store, D-089). The cut pieces are drawn in front of the camera. The frame rate must hold (PLT-02), and the physics step must stay within its budget (PLT-04, already over it in the pilot's flights: `w9-village-physics` step-budget work).
- The branch is `unity/physics/physics-integration-2--w16-plants`, cut from `--w3-preview` (the pilot's build at model 100). It merges back into `--w3-preview`; its sim-core parts go to the physics line first.
- The drawn side and its captures need the GPU: they wait for the pilot's word that it is free.
