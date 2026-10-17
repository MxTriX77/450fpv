# Phase 1 brief: reference analysis and assets

## Task
Produce every asset in `docs/asset-list.md` (P1 first, then P2) from editable sources, each with physics metadata, judged against the pilot's footage and photos and against research. This is the most resource-heavy phase, and it runs first on purpose: after it, nothing later needs to open the footage again, apart from feed A/B stills in phase 4.

## Settled: do not re-open
- **Content and counts:** `docs/asset-list.md`. Ids are stable.
- **Free assets only** (D-003). Realism comes from parametric models dressed in CC0 photoscanned materials and calibrated to the footage (D-020).
- **Realism = a copy of the references, with proper geometry** (D-028, REAL-01…05). An asset is done when it matches its reference letter side by side (local sheet in `reference/_compare/`, never committed) and passes the geometry audit (no physical feature texture-only; collision follows the visual; every opening probed).
- **Rebuilt to match, never extracted.** The footage is PAL at about 450 × 286 and is used for dimensions, materials, colours, damage and density.
- **Editability** (D-004): generator plus params per variant; one command rebuilds; exports are never hand-edited.
- **Physics metadata is authored now** (AST-05):
  - collision primitives within ±5 cm (±2 cm at openings and landing surfaces)
  - material ids
  - gaps as **planar polygons in any orientation**, measured from the real clear space
  - wind volumes with **per-part porosity**
  - snag flags, landable flags and LOD distances
- **Damage is physical** (PR-5): jagged openings, broken courses, rebar, broken battens, all with collision.
- **Already measured and not to be re-derived:**
  - terrain classes, sizes and colours: `docs/reference-notes/terrain.md`, `knowledge.md` §9
  - surface physics starting values: `knowledge.md` §7.2
  - plaster recipe: `knowledge.md` §9.2
  - the feed and wind studies (not needed in this phase)

## Read before starting, in this order
1. `docs/plan.md` (phase 1), `docs/asset-list.md`, `docs/requirements.md` §5 (AST) and §8 (VIS).
2. `docs/reference-notes/terrain.md` (the whole file), and `docs/knowledge.md` §9 and §14.4 (art lessons: UVs, tiling, feathering, rubble colour, belt at distance).
3. `docs/reference-notes/pilot-requirements.md` PR-5, PR-7, PR-8, PR-9, PR-10 and PR-11.
4. `docs/legacy/godot-cc0-credits.md`: the CC0 sets the first attempt used, as a starting list.

## The constraints that bite
- **OPSEC.**
  - Read the footage only through `reference/_frames/` contact sheets, inside subagents.
  - Never write file names, places, dates or OSD values.
  - Cite letters only.
  - Object sheets describe classes, never a specific identifiable building.
- **Tokens.**
  - One reference-analyst subagent per class group (vegetation, houses and yard, earthworks, vehicles, urban edge, lines and debris), at most 2 in parallel.
  - Contact sheets, not single frames.
  - Findings go into `docs/object-sheets/<group>.md`.
  - The orchestrator reads sheets, not images.
- **Budgets** (AST-08): textures 2K at most; 3–4 LODs; tree impostors; the world must fit 4.5 GB of VRAM later.
- **Look rules** (AST-07):
  - UVs in asset space, upright and continuous; facade maps plus damage masks
  - no visible tiling at 10 m
  - rubble coloured from its own materials
  - belts read as dark bands at 200 m
  - feathered surface edges

## Flow
1. **Object sheets**, one per group, using the template `docs/object-sheets/_template.md`. Each one is reviewed by asset-artist for buildability.
2. **Materials:** fetch the CC0 sets; build URP materials, derived textures and the 13 ground surfaces (S01–S13).
3. **Look setup** (tech-artist): sky and light by time of day and rain setting (dry, rain, severe rain, with wet materials), aerial perspective, the vegetation wind shader.
4. **Look-lock assets:** S04, V01 + V13, H01 in I/D/X, R01 in A/F, E01, L01 + L03, each with its review sheet.
5. **G1 look-lock** `[user-review]`: a quick gallery build of those six stations. The pilot takes a 10-minute look. Write their feedback into `docs/style-rules.md`, and decide URP vs HDRP (D-001) here.
6. **Mass production** in asset-list order, P1 then P2. Per asset: generator plus params, LODs, metadata, catalog entry, review sheet, QA approval. Each finished asset goes into its gallery station.
7. **Micro-detail generator and renderer,** deterministic and shared with physics (AST-10).
8. **Gap probe:** a drone-sized capsule (0.574 m nose-first, 0.706 m diagonal) through every gap, and a contact on every material.

## Checks: run them, and paste the output into the handoff
- `pwsh tools/build_asset.ps1 <ID>` for each new asset; the review sheet is attached.
- **The editability test:** QA changes one parameter on 3 random assets, rebuilds, and sees the change in Unity with no hand step.
- The budget report (triangles per LOD, textures, VRAM estimate).
- The gap probe report.
- **The side-by-side sheet against the reference letter** for each asset (REAL-03), and the **geometry audit** (REAL-05).
- `python tools/assets/fetch.py --verify`.

## Done means
- Every P1 asset is QA-approved against its object sheet, with its review sheet.
- G1 feedback is applied and recorded in `docs/style-rules.md`.
- The editability, budget and gap-probe checks are green.
- P2 continues into phase 2/3 without blocking G2.

## Write scope
| Role | Paths |
|---|---|
| reference-analyst | `docs/object-sheets/`, `tools/reference/` |
| asset-artist | `assets-src/`, `blender/`, `tools/blender/`, `tools/assets/`, `unity/Assets/_Project/{Art,Prefabs}/`, `CREDITS.md` |
| tech-artist | `unity/Assets/_Project/{Shaders,Settings}/`, lighting and sky assets |
| world-builder | only the quick look-lock gallery |
| QA | reviews, tests |

## Commit
`type(scope): summary`: lower case, no body, no trailers, no attribution. One commit per asset or coherent step, for example:
- `art(house): add adobe house generator with three damage states`
- `docs(ref): add object sheet for village houses`
