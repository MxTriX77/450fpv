# Review: p1-letter-s-village-street (task 2.1)

**Reviewer:** qa-engineer, a fresh agent that did not write task 1.1 (D-034) · **Branch:** `unity/ref/p1-letter-s-village-street` at `743f7a3`, 11 commits on `unity/main`'s `9ba7fc5` · **Requirements:** REAL-01–REAL-04, WLD-01, WLD-14, AST-02, SEC-01, SEC-02 · **Decisions:** D-028, D-038

**Verdict: CHANGES REQUESTED** (R1–R4 below). The reading is thorough, and I recomputed its geometry independently from the scene: the camera, the street's cross-section, the colours and the damage states hold. Three things block the merge:
- **One OPSEC problem.** The world instruction copies S's layout, its order of parts and its turn, into the map. REAL-04 forbids that.
- **One missing citation.** S03 `crater_spoil` doesn't cite S.
- **Two pole-placement inconsistencies.**

Each is a short edit by the reference analyst.

## Scope

`git diff 9ba7fc5..743f7a3` touches 7 files (+415 −69):
- `docs/reference-notes/terrain.md`: §12, plus S in the at-a-glance table, the class entries, §9 and §10
- four object sheets: `houses-and-yard.md`, `lines-roads-debris.md`, `vegetation.md`, `earthworks-and-surfaces.md`
- this folder's `implications.md` and `tasks.md`

I read the proposal, the tasks, the delta spec, `implications.md`, D-038, the requirement texts and the whole diff. I checked the claims against S's three contact sheets and 9 single stills (S 17, 22, 33, 40, 42, 46, 47, 49, 50). I opened no clip, no index and no letters file.

For pixel coordinates, I wrote 5 crops of single stills to the session scratchpad and deleted them after use. Nothing was written under `reference/`, and nothing from it is in this review. `origin/unity/main` is 2 commits ahead (D-039), and those commits touch none of this change's files.

## Checks run

```
$ git fetch origin && git checkout -B qa-s origin/unity/ref/p1-letter-s-village-street
Switched to a new branch 'qa-s'

$ python tools/hygiene/precommit.py --tree HEAD
775 files scanned, 0 findings                                   exit=0

$ openspec validate p1-letter-s-village-street --strict
Change 'p1-letter-s-village-street' is valid                    exit=0

$ git ls-files reference/ | wc -l
0

$ git diff 9ba7fc5..743f7a3 | grep -cE "S_[0-9]{4}|\.png|\.jpg|\.mp4|_frames|index\.md|letters\.md"
2        (both are the folder path in the task 1.1 text, pre-existing from the proposal; no file name)

$ git log --format='%h|%an <%ae>|%s|body=[%b]' 9ba7fc5..743f7a3
743f7a3|Reference Analyst <reference@450fpv.local>|docs(ref): tick task 1.1 of p1-letter-s-village-street|body=[]
2a54901|Reference Analyst <reference@450fpv.local>|docs(ref): list what letter s implies for each asset|body=[]
31c2d9d|Reference Analyst <reference@450fpv.local>|docs(ref): add letter s to the earthworks and surfaces sheet|body=[]
1314858|Reference Analyst <reference@450fpv.local>|docs(ref): measure the char patch in the houses sheet|body=[]
368290d|Reference Analyst <reference@450fpv.local>|docs(ref): measure the char patch of letter s|body=[]
aec1985|Reference Analyst <reference@450fpv.local>|docs(ref): measure letter s craters and turned strip|body=[]
4a073df|Reference Analyst <reference@450fpv.local>|docs(ref): add letter s to the vegetation sheet|body=[]
7b00172|Reference Analyst <reference@450fpv.local>|docs(ref): add letter s to the lines, roads and debris sheet|body=[]
f123b1a|Reference Analyst <reference@450fpv.local>|docs(ref): add letter s to the houses and yard sheet|body=[]
228005e|Reference Analyst <reference@450fpv.local>|docs(ref): correct letter s wall height and house footprint|body=[]
2e58312|Reference Analyst <reference@450fpv.local>|docs(ref): read letter s into the terrain notes|body=[]
```

**Hygiene:** subject-only commits under the role identity, with no body, no trailers and no co-author. Only docs were touched.

## Spot checks against the stills

**Method:**
- The pinned FEED-02 model: native 16:9 with square pixels, `p_u = p_d·(1 + 0.33·r_d²)` normalised to the half-width, then a pinhole of f = 1.005.
- The world's up vector comes from 3 horizon points per still.
- A height comes from the foot and top depression of a vertical object.
- Ground positions come from intersecting rays with the ground plane, or with a 2.8 m plane for the eaves.
- Colours are the mean sRGB of boxes I chose myself, away from the OSD.

The scripts are throwaway files in the scratchpad.

| # | Claim (where) | My reading | Result |
|---|---|---|---|
| 1 | Frame centre about 30° below the horizon (§12.2) | 29.5° (S 40), 29.9° (S 46), 30.2° (S 47), 30.1° (S 42) | **holds** |
| 2 | Camera 11–14 m, about 12.5 m (§12.2) | **Poles:** the same street pole in S 46 and S 47, with foot and top depression ratio 1.84–2.06, gives 13–14.5 m for a 7.0–7.5 m pole. **Building corner** (S 40): ratio 1.66–1.85, which gives 11.6–13.8 m for 6.5–7.5 m | **holds** within the range; the pole reads sit at its top (S1) |
| 3 | Ground speed 10–12 m/s (§12.2) | That pole's foot closes 5.0–5.4 m between S 46 and S 47 (0.47 s apart), so 10.6–11.5 m/s at 12.5–13.5 m | **holds** |
| 4 | Carriageway 5–7 m, about 6 m (§12.3, RD01) | **S 22**, edges on 9 columns, edge to edge: 7.3 m at 12.5 m, 6.4 m at 11 m. **S 47**, asphalt-only edges on 7 rows: 5.2 m at 12.5 m, 5.6 m at 13.5 m | **holds** |
| 5 | Poles 3–5 m from the road edge, fence 7–9 m, house front 12–16 m (§12.3, L02) | S 47 at 12.5–13.5 m: the pole is 4.5–4.9 m from the asphalt edge, the street-fence posts 7.8–9.5 m, the house wall feet about 12–17 m | **holds** (the fence at the top end) |
| 6 | Poles on "one side of the street only", measured S 43–50 (§12.3) | The line runs in the house-side verge in S 43–50. But S 33 shows the thin dark wooden pole and the lamp pole on the **green** side of the street, and S 42 shows a thin pole on the green-side verge by the square | **partly**: R4 |
| 7 | Whitewashed pole butts (S 47–49; L02) | A white foot band on the S 46, 47 and 49 poles, about 1/7 of the visible pole in S 47 (the sheet says about a fifth) | **holds** |
| 8 | House D state with a ceiling (S 45–50): slate and most rafters gone, debris on the ceiling, chimney and mast standing, white frames in with the sashes gone | Seen in S 47, 49 and 50. S 50 also shows a hole through the ceiling | **holds** (S6) |
| 9 | `feed` colours (§12.5 and the sheets) | Each of my readings, against the claim:<br>- road, near (S 48): #b9becd against #bac1d3<br>- render (S 49): #aaaba7 and #a6a5a6 against #a8a8a8<br>- annex brick (S 49): #95837b and #9f7061 against #9f7a6e<br>- pale brick (S 40): #c8bccc against #c9bcca<br>- paving (S 40): #a7a4a7 against #a3a0a0<br>- turned soil (S 33): #725b6a against #725b6a<br>- char (S 17): #334157 and #393648 against #323c51<br>- fallow heads (S 10): #8a8c85 against #8d8e8a | **holds** |
| 10 | Green grass #5f8d4c (S 36), #6d864d (S 24) | Whole-patch means: #617c4e and #66784f (S 36), #758760 (S 24). The hue is the same, but G is 10–17 levels lower | **partly**: the claims are the greenest picks (S2) |
| 11 | Char patch about 5 × 9 m (S 17; H shared section, S07) | 9.6 × 5.4–5.8 m at 12.5 m (the near edge is cut by the frame) | **holds** |
| 12 | House about 12 × 8–9.5 m (S 49–50; H02 variant 3) | Eaves corners on a 2.8 m plane: 12.8 × 7.3 m at 12.5 m, 14.1 × 8.0 m at 13.5 m | **holds**, roughly |
| 13 | Dead trees on the square 4.7–5.9 m (S 42; V08) | Foot and top ratio 1.88–1.94, which gives 5.8–7.4 m at 11–14 m. The foot may be hidden in the shrubs, and the trunk leans | **partly**: likely about 1 m low (S3) |

**Also seen and holding:**
- the greenhouse hoop frames (S 7–12)
- the curled roof sheets and the standing stove block (S 16–18)
- the paved square with weeds, kerbs and a low border (S 39–42)
- the green-side slab footpath between kerbs (S 46)
- the yellow-topped verge weeds (S 46–47)
- the blue metal yard item (S 47–49)
- the braced pole (S 42–44)
- the red-brick street wall with debris behind it (S 47)

**Not seen, as claimed:** no vehicle, no person and no ditch.

## Spec scenarios

| Scenario | Result | Evidence |
|---|---|---|
| **Letter S read in** | **FAIL** (one row) | The notes hold every part:<br>- §12.1 the scene<br>- §12.2 the height, path and attitude, derived from the scene and recomputed above<br>- §12.3 the layout with labelled sizes<br>- §12.4 what stands where<br>- §12.5 composition, light, season, a 25-row colour table with stills, wear, clutter and vegetation density, near, middle and far<br>- §12.6 the view<br><br>Every S citation in the four sheets carries still numbers. **But S03 `crater_spoil`, a row whose object S shows, doesn't cite S (R2).** The S numbers are labelled derived or measured with their stills; a few say "(by eye)" (S4). The implications list covers every asset named in the proposal. |
| **Nothing of the place** | **PASS** | The scan finds 0 findings over 775 files, and 0 files are tracked under `reference/`. No file name, place, date or OSD value is in the diff. No number in the reading needs the display: each one I checked comes back from scene geometry alone. |

## Requirements

| ID | Status | Why |
|---|---|---|
| REAL-01 | met | S is read as the reference for the typical village, against its own stills |
| REAL-02 | met for this change | §12.6 sets the viewpoint, framing (key frame S 47 and four gallery frames), light and weather. REAL-02's table itself has no S row (S7) |
| REAL-03 | not testable | Nothing is built yet. The §12.5 colour table is the future check |
| REAL-04 | **partly met** | The descriptions stay at class level (see OPSEC), but the world instruction copies S's layout (R1) |
| WLD-01 | met | The typical village is defined from S, with room for cars (D-038) |
| WLD-14 | **partly met** | S's view is defined, but its placement in the world copies the sequence (R1) |
| AST-02 | **partly met** | S is added to the sheets with feed values. S03 is missing (R2); the L01 placement conflict is not flagged (R3); "one side only" is overstated (R4) |
| SEC-01 | met | Nothing under `reference/` is tracked, and no file name appears |
| SEC-02 | met | No OSD value, place, date or person. The wording is neutral; the turned strip is explicitly "not a trench" |

## OPSEC

**Class level: passes.**
- **The memorial** (§12.4; implications item 1) is "a dark sculpture on a pale stone pedestal, the pedestal's facing partly broken off". It has no subject, pose, emblem, inscription, date or shape. P07 is proposed as "a plain pedestal with a generic figure, never a copy".
- **The public building** has only its construction and damage: pale brick, two storeys, a flat roof partly torn off, the windows blown, the canopy fallen. It has no footprint and no sign, and its function is named only as a class ("a village council or club class").
- **The square** has only its tile size, kerbs and border. It has no shape, size or orientation.
- No sign text, name or mural appears anywhere. The other distinctive items (the conifer, the blue yard item, the long red-brick building) are classes.

**Arrangement: fails (R1).**
- terrain.md:693 (§12.6 "For the world") asks the typical village for "at least one street laid out as 12.3, with S's sequence along it (fallow, back gardens, a flattened plot, the street, a green with the square and the public building, a row of D-state houses), so the 12.2 path can be flown and compared side by side with S". implications.md:138 repeats it.
- Together with §12.2's path (across the street, over the green and the square, a right turn onto a street that curves away), that is a replica of a 250–300 m stretch of one real village: its order of parts, its turn and the relation of the square, the building, the green and the flattened plot to the street.
- REAL-04 says "never reproduced: … layouts traceable to a real location"; D-038 says "a pattern to copy, never a place"; SEC-03 keeps the map a composite.
- Reading the path in the notes is needed for the viewpoint and can stay. Copying it into the world can't.

## Consistency

- **Material ids:** the diff adds no physics material id (`assets-src/physics_materials.json` is unchanged and none of its ids is misused). `red_brick` is the existing texture-set and `wall_material` value, and `rubble` is the S10 surface id.
- **Params cited:** `window_state`/`frame_empty`, `frame_paint` (H03), `interior_ceiling_down`, `collapse`, `film_loss`, `leaf_cover` (V13 params have it), `lamp` (false in all 4 L01 params) and `L01_leaning`'s 9° all exist as cited. `ceiling_debris` and `tv_mast` are marked proposed.
- **IDs:**
  - Every cited ID exists in `docs/asset-list.md`.
  - The new P07, P08, Y12, V22, Y04 variant 5 and H06 variant 3 appear only as proposals in `implications.md`.
  - H02 variant 3 is marked "proposed by S" in the sheet.
- **Labels:** S numbers are derived (with the derivation) or measured (with stills). Some use "(by eye)" instead of the four-label set (S4). The H shared section says outright that S's X state is flatter than the table's.
- **Merged assets:**
  - S04, V13, K01 and L03 get additions only.
  - **L01 conflicts and the sheet doesn't say so (R3):** its placement rule (lines-roads-debris.md:199, "0.5–1.5 m in from the road edge or along the fence line") stands unchanged. Meanwhile L02 (:228) and RD01 (:526) now give S's 3–5 m from the road edge, 3–5 m in front of the fence. Only `implications.md` names the conflict.
- **Implications:** each item is a concrete difference with stills and is routable. The only exception is the world-builder item (R1). The drop-length arithmetic and the 25 m rule (cl. 2.4.17) check out against the lines sheet.

## Required changes

1. **R1 (OPSEC, REAL-04)**
   - **Where:** rewrite terrain.md:693 (§12.6 "For the world") and implications.md:138.
   - **Keep:** the typical village takes S's cross-section (§12.3) and S's classes, states, densities and look (§12.4–12.5). S's views are recreated as scene types (key frame S 47; S 10, 18, 40, 50), each where the composite puts it.
   - **Never copied:** S's order of parts along one path, the street's curve, the turn, and the relative positions of the green, the square, the public building and the flattened plot.
   - **Invented:** the square's shape, size and orientation and the building's footprint.
   - **Flying the path:** the §12.2 height, speed and attitude can be flown over any street built to §12.3.
   - Cite REAL-04 in the paragraph.
2. **R2 (scenario):** add S to S03 `crater_spoil`'s "Seen in" (earthworks-and-surfaces.md:789): the dark spoil discs 3.5–6.5 m on the green (S 29–37; derived S 36). Also fill the empty feed cell of its "shallow spoil, dark" row with #5b4f5c (S 36, damp, on grass), as E05's table already does.
3. **R3 (merged L01):** at lines-roads-debris.md:199, say that S measures the typical village's line 3–5 m from the road edge and 3–5 m in front of the fence (derived S 46–47, terrain.md §12.3). Point to the implications item, so the sheet no longer holds two silent rules. The parameter change itself stays a routed task.
4. **R4 (measured claim):** at terrain.md:584, and in the L02 position row if it repeats it, replace "one side of the street only". Say instead: the line runs in the house-side verge (S 43–50), while by the square a thin dark wooden pole and the lamp pole stand on the green side (S 33, 42). Make §12.4's S 33 sentence (terrain.md:598) name the side.

## Suggestions (non-blocking)

- **S1:** The two street-pole reads (S 46–47) give 13–14.5 m, so 12.5 m sits low in the 11–14 m range. Note that the pole reads favour 13–14 m, or recentre the default; derived lengths would move +5–15 %.
- **S2:** In §12.5 and the S01 and vegetation tables, say the grass values are pure-grass picks. Whole-patch means run #617c4e–#758760, so the tech-artist shouldn't over-saturate the green to match the picks.
- **S3:** Re-check the S 42 dead-tree height (my read: 5.8–7.4 m), in V08's "Seen in" and the implications V01 item.
- **S4:** Write "(by eye)" as "measured, rough (S n)", so the four-label vocabulary holds. It appears in V01, V08, V14, K04, S01, S10, E05, §12.4 and implications K01.
- **S5:** "A red-brick annex at the street end" (implications.md:81, houses-and-yard.md:218, terrain.md §12.4): in S 47 and 49 the annex is one end of the street-side facade, not a gable facing the street. Say "at one end of the street side".
- **S6:** "The intact ceiling" (H shared section, §12.4, implications H01): S 50 shows a hole through it. Write "mostly intact, holed".
- **S7 (orchestrator):** REAL-02's letter table and GAL-05 in `docs/requirements.md` have no S, while WLD-14 lists S. Add S's view row.
- **S8:** Y04 variant 3's range was widened to 2.5 m on a read the sheet itself calls unclassed ("if it is a fence"). Keep 1.5–2 m until it is classed.

**Verdict: CHANGES REQUESTED** (R1–R4).
