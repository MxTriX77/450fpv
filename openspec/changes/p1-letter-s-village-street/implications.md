# Implications: what letter S means for each asset

**Task:** 1.1 of `p1-letter-s-village-street` (reference-analyst). **Decisions:** D-028, D-038.

S's full reading is [terrain.md](../../../docs/reference-notes/terrain.md) §12: the scene, the camera path, the street's cross-section, what stands where, and the look and feel. The object sheets cite S row by row. This file lists, for each asset built, in flight or next, what S shows that the asset or its sheet doesn't have, or has differently. Each item is a concrete difference with its stills, for the orchestrator to route as tasks. "None seen" means S shows nothing of that asset.

**What S is:** the typical village of the world (D-038), flown at about 12.5 m and 10–12 m/s (derived), on a bright, damp autumn day under high overcast. It shows:
- a fallow field and back gardens;
- a house flattened to its footprint and burnt;
- a pale asphalt street about 6 m wide;
- an open green with a memorial square and a two-storey public building;
- a row of single-storey D-state houses with stripped hip slate roofs.

S shows no vehicle and no person. Its feed renders the scene cool and saturated: neutral surfaces go lilac-blue and greens go strong.

## Built, in flight, next

### S04 `belt_straw` (merged)
- **None seen.** S has no belt floor: belts stand only as bands at the horizon.
- The fallow's standing grass (S 1–14) is V17 and S08, not a lodged mat.
- Nothing in S04 changes. Its feed check stays H's: under S's cool feed the pale grass heads read #8d8e8a (S 10), not H's warm straw.

### V13 belt understory shrub (merged)
- **Out of the belt.** S shows the same kind of shrub standing alone on a village green: rounded, closed leafy clumps about 2–3 m across (S 25–27, 38–41).
  - These are V13's summer domes (`V13_mid`, 2.0 × 2.2 m), the form H's twiggy wall did not want.
  - So the green's lone shrubs reuse `V13_mid` and `V13_tall` as built.
- **Placement V13 lacks:** a free-standing shrub or young tree every 15–25 m on a green (S 24–40, rough). V13's placement rules cover belts only.
- **Autumn, S's form:** one clump yellowing while still fully leaved (S 25–26).
  - This differs from the autumn variants `p1-v01-locust` adds for H, which are part-bare (`leaf_cover` 0.2–0.5).
  - S wants `leaf_cover` about 0.9 with the sheet's autumn tint on a share of the clumps.
- Front-garden thickets 2–4 m (S 46–50) are V14's, not V13's.

### V01 black locust (in flight)
- **No belt in the village.** At S, V01 appears only as the horizon belt band, which is its impostor's job.
- **Village trees differ from the belt tree.** S's tall trees beside the houses have a clean trunk to about 4–6 m (by eye) and a compact, tufted crown (S 43–47; species not readable).
  - That is not V01's open, scraggly belt crown.
  - If V01 is also the village's street and yard locust, it needs a single-tree form: a clean bole and a compact crown. Otherwise V05 or V10–V12 take that role.
  - Its placement rules need single trees in yards and along the street.
- **Season:** S's trees are in full leaf with the first yellowing. That is between V01's G1 states (0.6 part-bare, 0.9 summer), closest to 0.9 with a little autumn tint.
- The pale, bark-stripped dead trees on the square (S 40–43, 4.7–5.9 m, derived) are V08's, a V01 non-goal.

### L01 wooden pole (merged)
- **S's street line is concrete (L02), and L02 isn't built.**
  - Wood shows once: a thin dark pole at the green's street edge (S 33).
  - Perhaps a second: a pale pole leaning about 10° with a dark stub at its foot (S 20–22), either `L01_stub` or concrete, not readable.
  - The S street can't be recreated without L02.
- **Whitewashed butt (S 47–49), which no pole generator has:**
  - The bottom 0.5–1 m of each street pole is lime-white, the brightest spot on the verge.
  - The shared pole table has no such item. Add a butt whitewash band (height 0.5–1 m) for L01 and L02.
- **Placement, which S contradicts:**
  - S puts the line in the house-side verge, 3–5 m from the road edge and 3–5 m in front of the fence (derived S 46–47).
  - L01's rule says 0.5–1.5 m in from the road edge or along the fence line (lines-roads-debris.md, L01 placement rules, which now flags the conflict).
  - L02 and RD01 already give S's 3–5 m. L01 as merged stays as built: placement isn't a generator parameter, so no asset changes.
  - **To route:** one rule for the typical village's street line, S's 3–5 m, written into L01's placement rules (reference analyst) and applied when the world-builder places the lines (phase 3). The 0.5–1.5 m rule can stay for side streets and lanes, where S shows nothing.
- **Already covered:**
  - The lean (about 10°, S 49) matches `L01_leaning` (9°).
  - The raking strut (S 43–44) is on a concrete pole: `L01_braced` is the wood form; the concrete form is L02's.
- **Street lamp:** a lamp on a short bracket (S 33). The `lamp` item exists and is off in every L01 param set; S's street wants it on some poles.

### L03 wires (merged)
- **None to measure.** From about 12 m up against grass, the street line's conductors and drops don't read (S 49–50). There is one faint 1 px line beside a pole, probably a drop fallen across the verge (L04 variant 2, S 49).
- **A check for the S side-by-side:** at the S 47 framing, no conductor may read stronger than S's (the coverage rule, no minimum width). Seen from above, a wire has only the ground behind it.
- **Drop lengths in the S layout (derived):**
  - house side: 8–12 m, from a pole 3–5 m off the road to a house front 12–16 m off it;
  - across the street to a house set back the same way: 21–27 m (3–5 m to the road, 6 m across it, 12–16 m to the house).
  - The house-side drops are well within the 25 m drop rule. The cross-street drops reach it, so far-side houses set back more than about 14 m need their own pole (ПУЕ cl. 2.4.17, lines-roads-debris.md). The S street's L03 drops should use these lengths.

### K01 bricks (merged)
- **Read as heaps, not bricks.** At 10–20 m a 0.25 m brick is 2–3 feed px. The flattened plot reads by colour and heap shape (S 16–19), carried by K01 clusters, the `rubble` surface and their colour: red-brick rubble #957266 (S 18), mixed #908188 (S 17).
- **A piece K01 lacks:** the masonry stove block standing about 1–1.5 m in the flattened plot (S 17–18). It is a bonded brick block of roughly 1 × 0.8 m (by eye) with its flue, larger than K01's 0.5–0.8 m clusters. It is the one upright thing in an X-state footprint.
- **Two brick colours:** pale wall stubs lie among the red brick (S 17), so the X heap needs K01 in silicate as well as red.

### E01 trench (in flight)
- **None seen.** S shows no trench.
- The turned strip on the green (S 31–35: 5–10 × 25–30 m, derived, with a narrow groove on its fence side) is dug or churned ground with no walls and no parapets. It belongs to S06, not E01.

### H01 adobe house (next)
H01 stays E's copy. The difference is the shared `house` generator that H01's change writes first, because S's house is the next one it must make.
- **S's houses are not H01.** The typical village (D-038) is houses that are:
  - single-storey, pale rendered, with white frames;
  - hip slate roofs;
  - a red-brick annex at the street end;
  - about 12 × 8.5 m (derived, S 49–50).
  - The houses sheet proposes them as H02 variant 3. No adobe-and-tile house appears in S.
- **D state with a ceiling, which the shared table lacks (S 45–50):**
  - The slate and most rafters are gone; the wall plate and a few hip rafters stand.
  - The ceiling is intact and carries the fallen slate, boards and debris.
  - The chimney and a TV mast stand above it.
  - The rubble at the foot is light.
  - The table has `interior_ceiling_down` but no item that keeps roof debris on the ceiling, and no mast. Proposed: `ceiling_debris` (0–1, the share of roof debris resting on the ceiling instead of the ground) and `tv_mast` (position, height).
- **White frames that stay (S 47–50):** frames mostly stay in their openings with the sashes and glass gone (`window_state` `frame_empty`), painted white. The frame paint must be a shared item: H03 has `frame_paint` with white, H01 doesn't.
- **A flatter X (S 15–19):**
  - walls down to stubs under 1 m over the whole footprint;
  - the stove block standing;
  - burnt, with a char patch about 5 × 9 m (derived, S 17).
  - The table's X leaves walls at 0.5–1.5 m. The typical village needs `collapse` down to 0–0.8 m on every wall, plus the standing stove (K01 above).
- **Render colour:** S's render reads #a8a8a8–#9e9ea4 through S's feed (S 49–50). H01's whitewash recipe (#c4bfb1) stays E's. The S house takes a greyed lime render (about #c4c2bc, assumed).
- **Order:** H02 variant 3 is the typical village's house, so it should follow H01 closely.

### R01 ВАЗ sedan and the other vehicles (next)
- **None seen.** S shows no vehicle. Its one vehicle sign is paired tyre arcs pressed into the green's grass (S 28–30).
- **Where S leaves room for D-038's cars:**
  - on the 7–9 m house-side verge by the gates;
  - inside the gates in the yards;
  - on the green, where the tyre arcs are;
  - at the road edge.
- **States (a proposal from S's damage level, not seen):**
  - mostly abandoned (A) in yards and on verges;
  - burnt (F) near a flattened, burnt plot.
- **Overgrowth:** S's verges carry a weed fringe 0.5–1.5 m wide along the fences (S 43–48). A car parked on the verge stands in V17 there, and R01's change leaves overgrowth out (V17 not built).
- **Colour:** under S's feed, neutral greys go lilac-blue. R01's paint is compared against M, never against S's cast.

## Objects S shows that no sheet has
None has an ID in the asset list, so each needs one before it can have a row. All are proposals for the orchestrator.
1. **Village memorial on a paved square (S 31–42).**
   - A dark sculpture on a pale stone pedestal, its facing partly broken off.
   - A square paved with concrete tiles of about 0.3–0.4 m (by eye), grown through with weeds, with narrow kerbs and a low border.
   - Pale dead trees and thin young trees around it.
   - Proposed as urban-edge P07. OPSEC: built as a generic class (a plain pedestal with a generic figure), never a copy of a particular memorial.
2. **Two-storey public building (S 22–24 far, 31–42).**
   - H06's construction in pale brick (`feed` #c9bcca, S 39), 6.5–7.5 m to a flat roof partly torn off.
   - Every window blown to its bare opening, and an entrance canopy fallen onto its props.
   - Proposed as H06 variant 3 or urban-edge P08 (a village council or club class).
3. **Slate-sheet fence (S 31–36).** Corrugated asbestos-cement sheets set upright on rails and thin dark posts, knocked flat beside its posts. Proposed as Y04 variant 5 or a new Y12.
4. **Concrete-slab footpath with narrow kerbs (S 43–46).** About 1 m wide, 5–6 m from the road edge on the green side, broken and grown over. Proposed as an RD01 option.
5. **Yard conifer (S 43–45).** A dark conifer-like tree in a front garden (an uncertain read). No conifer row exists. Proposed as a new V22 (a front-garden conifer).
6. **TV mast on a house (S 50).** A shared house item; see H01 above.
7. **Stove block standing in an X footprint (S 17–18).** See K01 above.

Not proposed, too uncertain to class:
- an open-topped masonry box in a yard (S 49–50);
- a long single-storey red-brick building with a row of dark openings far along the street (S 36–44): U03 garages or P02.

## Other roles
- **Tech-artist (the feed):**
  - S is the first overcast daylight clip with strong colour. The feed renders it cool and saturated: road #bbb7c8, pale brick #c7bfce, grass #5f8d4c, char #323c51 (terrain.md §12.5).
  - This is unlike B and G (warm, low sun) and A and H (flat grey).
  - The feed setup should reproduce S's grade from neutral albedos. S's colour table is its check.
- **World-builder (WLD-01, WLD-14, REAL-04):** the typical village copies S's look, never its layout (terrain.md §12.6).
  - **Copied:** the street's cross-section (§12.3), the classes and states along it (§12.4) and the look (§12.5).
  - **S's views as scene types,** each where the composite map puts it: a street of D-state houses (S 47), back gardens with greenhouse hoops (S 10), a flattened, burnt plot (S 18), a green with craters (S 33–36), a paved square with a public building (S 40), a stripped roof from above (S 50).
  - **Never copied:** S's order of parts, the street's curve, the turn, and the relative positions of the green, the square, the public building and the flattened plot. The square's shape, size and orientation and the building's footprint are invented.
  - **The recreated views** fly the §12.2 height, pitch and speed (about 12.5 m, 30° down, 10–12 m/s) over any street built to §12.3, for the REAL-03 side-by-side.
- **Not touched, none seen:** fortifications (F01–F04), the urban-edge rows U01–U03 and P01–P06, and the vehicles sheet.
