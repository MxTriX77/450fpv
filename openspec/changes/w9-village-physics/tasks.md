# Tasks: w9-village-physics

Branch `unity/physics/physics-integration-2--w9-village`, cut from `--w3-ground` at 241145ea (the pilot's preview, model 85). Order: 1.1, then the orchestrator updates 2.x from the audit's findings, then 2.x as the audit orders them, then 3.1.

## 1. The audit

- [ ] 1.1 [physics-engineer] The audit, `audit.md` in this change. No code.
  - List every ground and vegetation interaction the test range and the model-79 flights have:
    - slopes that tip the drone, and legs that snag;
    - soft soil that legs sink into and stick in (p7a-ground, p7g-legs);
    - straw and grass (the test range's S04 straw boxes, flight 37), micro detail, furrows;
    - prop strikes on the ground and on grass, and the fiber on the ground;
    - anything else the code holds that the list misses.
  - For each: what data it reads, and whether the farmland feeds it that data (surface per cell from `classes/`, soil softness, grass and straw volumes, micro detail). Show each answer with a test, a headless run or a code path, never a guess.
  - The known gaps, each confirmed or corrected: furrows aren't in the physics (W3 2.2); S04-type straw counts only as wind roughness on the farmland (W7 1.1); a cut landing across a side slope rolls from 10° (W3 2.2).
  - What W2's export can give for a per-cell grass and straw field (`grass/map.raw`, `classes/`, the friend's terrain detail layers), and what is missing.
  - For the new physics (stems, straw and leaves on the props): the published stem properties to use (diameter, density, cutting energy or shear strength per kind), with sources; how the impulse reaches the airframe; what the log needs (format and event kinds from `docs/handover-2026-11-12.md` §5).
  - End with the work as ordered tasks: owner role, what, the checks, and which golden each could move.

## 2. The physics (provisional, rewritten from the audit)

- [ ] 2.1 [world-builder] The farmland's stem field: per-cell grass and straw (height, stem density, stem kind) from W2's export, in the package and the world query; `validate.py` checks it.
- [ ] 2.2 [physics-engineer] Stems on the props: blade strikes and cuts of grass and straw from published stem properties, the impulse through the airframe, the cut events logged. Write design.md.
- [ ] 2.3 [physics-engineer] Leaves in crowns: extend `PropStrikes.Twigs` to leaves, and move the twig and leaf constants into `physics_materials.json`.
- [ ] 2.4 [physics-engineer] The ground gaps the audit lists (furrows, soft soil, slopes), each fixed in the physics or the data, or shown physical.
- [ ] 2.5 [tech-artist] Cut bits drawn in front of the camera from the sim's cut events.

## 3. Review

- [ ] 3.1 [qa-engineer] Review against the spec scenarios: scripted landings on Village on a slope, on soft soil, and in tall grass and straw, plus a crown flight; bit-exact replays and 0 B; the step budget on a quiet machine; every moved golden audited.
