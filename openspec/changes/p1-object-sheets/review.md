# Review: p1-object-sheets (task 2.1)

**Reviewer:** qa-engineer, independent of the six reference-analyst tasks and the asset-artist buildability pass · **Branch:** `unity/ref/p1-object-sheets` at `1340cc1`; merge base with `origin/unity/main` is its tip `e6ee5d6`, so `origin/unity/main...HEAD` is this change alone · **Requirements:** AST-02, AST-04, AST-05, AST-06, SEC-01, SEC-02, PRC-06, PRC-09 · **Decisions:** D-003, D-013, D-024, D-028

## Scope

The diff `origin/unity/main...HEAD` touches 11 files: the six sheets under `docs/object-sheets/` (7,389 lines), `docs/asset-list.md` (U01 eaves, U02b row, L03 classes, RD01–RD03, R06, vehicle gaps), `docs/reference-notes/terrain.md` (S1: C's high lines are OSD marks), and three change artefacts (`design.md` RD ids, `tasks.md`, `review-buildability.md`). The change's own commits are the 24 listed under Hygiene.

I read all six sheets in full, the change folder, the spec delta, AST-02/04/05/06, SEC-01…06 and REAL-01…05. I did not open `reference/`. Every check below was run by me from this worktree; the scripts are throwaway files in my scratchpad, not committed. Line numbers are at `1340cc1`.

## Checks run

```
$ python tools/hygiene/precommit.py --tree HEAD
363 files scanned, 0 findings                         exit=0

$ openspec validate p1-object-sheets --strict
Change 'p1-object-sheets' is valid                    exit=0

$ python coverage.py .        # IDs in asset-list §1–10 against "## <ID> " headings in the six sheets
asset-list IDs (sec 1-10): 103
sheet ## <ID> headings: 103 distinct: 103
missing: []
extras: []
duplicates: {}
per sheet: {'earthworks-and-surfaces.md': 20, 'houses-and-yard.md': 25, 'lines-roads-debris.md': 17,
            'urban-edge.md': 10, 'vegetation.md': 21, 'vehicles.md': 10}

$ python sections.py .        # every row has the 9 template sections plus "Seen in" and "Research sources"
rows checked: 103 rows with gaps: 0

$ python labels.py . <seed>   # census of every row in every "### Dimensions" table
dimension rows total: 844 {'four-label': 793, 'xref-only': 51}
earthworks-and-surfaces.md rows: 145 {measured 42, published 27, derived 20, assumed 52, carried 16}
houses-and-yard.md        rows: 165 {measured 43, published 16, derived 12, assumed 92}
lines-roads-debris.md     rows: 128 {measured 35, published 28, derived 15, assumed 78}
urban-edge.md             rows: 121 {measured 22, published 26, derived 22, assumed 66}
vegetation.md             rows: 147 {measured 44, published 65, derived  7, assumed 62}
vehicles.md               rows: 138 {measured  8, published 68, derived 26, assumed 48}
'measured' with no letter after it: 7 rows (listed under R4)

$ python materials.py . <seed>  # every material line and material-table row
material lines found: 161
domains in material lines: {'polyhaven.com': 148, 'ambientcg.com': 30, 'www.texturecan.com': 2}
flagged (store/paid/NC/ND/OFL): []

$ python params.py .          # every row of every "| Param |" table
generator tables: 75 param rows: 778
rows with empty effect: 0
rows with no range and no range-bearing type: 8   (position choices "side or none" and one fraction; S5)

$ python gaps.py              # w(θ) = 0.254 + 0.32·(|cos θ| + |sin θ|), table below
w(0) = 0.5740 m, w(45) = 0.7065 m
20 openings recomputed, 0 mismatches

$ git diff --name-only origin/unity/main...HEAD | grep -iE '^reference/|\.(png|jpe?g|gif|webp|tiff?|mp4|mov|exr|hdr)$|Library/|Temp/|Logs/'
(no output)                                            exit=1
```

Source pages I opened to judge OPSEC and licence (public pages only): the TextureCan terms (CC0 1.0, confirmed); the belt-survey record cited at `vegetation.md:307` and `:810` (it inventories one shelterbelt between a named city and a named village); the locust-stand record at `vegetation.md:188` (its title names a steppe sub-region, no settlement). Four more could not be read from here (bot wall, refused connection, HTTP 403/405): the city ruderal survey and the village survey at `vegetation.md:1014`, the crater abstract at `earthworks-and-surfaces.md:31`, the elm-stand article at `vegetation.md:362`. They are judged on the sheets' own descriptions.

## Spec scenarios

| Requirement | Scenario | Result | Evidence |
|---|---|---|---|
| One sheet per class group, every row covered | Coverage check | **PASS** | 103 of 103 IDs in asset-list §1–10, each exactly once, no extras, no duplicates; all 103 rows carry the nine template sections plus Seen in and Research sources |
| Measured, labelled numbers | Label present | **FAIL** (R4, R5) | 793 of 844 dimension rows carry a label word; 8 more are AST-04 sizes used as given (sanctioned by the requirement); 14 inherit their label from a named row; **29 cite a source with no label word**; **7 "measured" rows name no letter**; H06's door contradicts AST-04 |
| Materials and colours resolve to the pipeline | Material resolves | **PASS** | 161 material lines, every source Poly Haven (CC0), ambientCG (CC0), TextureCan (CC0, terms checked) or the pinned manifest id `red_brick` (CC0); no store, paid, NC, ND or font source; 24-line sample below |
| Physics role in the pipeline's terms | Gap stated against the drone | **PASS** | 20 openings recomputed from the formula, 0 mismatches (table below); ids are the closed list or flagged `proposed:`; physics sections present on all 103 rows |
| OPSEC-clean | Hygiene scan passes | **FAIL** (R1–R3) | the scan reports 0 findings; the read finds no reference file name, no OSD value, no person, plate, date or unit of the references, but it finds location-specific research URLs and regional framing (OPSEC section) |
| Buildable | Buildability review recorded | **PASS**, record stale (R6) | `review-buildability.md` lists per sheet what was buildable, fixed and sent back; 8 fixes spot-checked in the sheets, all present; but U02b was added after the review and has no asset-artist entry |

## Labels (Label present)

Full census instead of a sample: 844 dimension rows.

- **793 rows** carry measured, published, derived, assumed, or `carried` (defined in the earthworks sheet as assumed).
- **8 rows** are AST-04 sizes used as given, which the requirement allows: houses-and-yard.md:126, 127, 212, 274, 408, 459, 532, 593.
- **14 rows** inherit from a named row that is itself labelled (acceptable, see S1): earthworks-and-surfaces.md:299, 371; houses-and-yard.md:207; lines-roads-debris.md:833, 834, 960, 1016, 1136, 1137, 1196; urban-edge.md:382, 393, 397, 619.
- **29 rows cite a source but carry no label word** (required change R4):
  - earthworks-and-surfaces.md:237 (E01 lip, "physics constant"), 416 (E04 opening, "asset list (R)"), 479, 485 (E05), 690 (S01), 910, 914 (S05), 960, 961, 962 (S06), 1018, 1023 (S07), 1112 (S09), 1158, 1159, 1160 (S10): terrain.md or the asset list only
  - houses-and-yard.md:129 (H01 base band, "research (asset-list §4)"), 458 (H06 footprint), 1046 (Y02), 1157 (Y04), 1266 (Y06)
  - lines-roads-debris.md:483 (RD01 kit pieces)
  - urban-edge.md:75, 81, 83, 93 (U01), 317 (U02), 543, 544 (U03)
- **7 rows say "measured" but name no letter**, only terrain.md or "belt crowns" (R4): earthworks-and-surfaces.md:226 (E01 depth); vegetation.md:260 (V02), 312 (V03), 367 (V04), 514 (V07 trunk), 819 (V13 branching), 1020 (V17 stalk diameter).

Random sample (seeded, 8 per sheet, 48 rows) read by eye: all 48 labelled; every measured one in the sample names its letter (for example earthworks-and-surfaces.md:567 "measured (F 15–17)", houses-and-yard.md:1152 "measured I", lines-roads-debris.md:352 "measured D 9–12", urban-edge.md:384 "measured I: 2.9–3.5 m", vegetation.md:668 "measured (C 2)"); every published one names its source in the row or its section's research line.

**AST-04 (R5).** Bricks are 250 × 120 × 65 mm everywhere and storeys are 2.8 m (houses) and 3.0 m (block, inside 3.0–3.4). Doors are not: H06's door is 0.9 × 2.0 m "assumed (a wider later door)" (houses-and-yard.md:464), against AST-04's exact 0.8 × 1.8–1.9 m for a house. The block and the shop also carry their own door sizes (urban-edge.md:99, 102, 103, 104, 623; the H04 veranda doorway at houses-and-yard.md:374). Those may be right for their classes, but AST-04 does not say so; the cellar (0.70 × 1.6 m, PR-9) and the outhouse are separate classes the asset list sets itself.

## Materials (Material resolves)

All 161 material lines name a Poly Haven or ambientCG set id with its page, a TextureCan set with its terms page, the manifest id `red_brick` (pinned in `tools/assets/manifest.json`, CC0, with SHA-256), a derived texture, or "a plain tinted material (no set needed)". Each sheet's material section states the libraries are CC0. Nothing names a store, paid, NC, ND or Fab/Asset Store source, and no font is named: the buildability review already sends the P01/P06 lettering font to the orchestrator because SIL OFL is not CC0 or CC-BY (D-003), which is correct.

Seeded sample of 24 lines, all resolve: lines-roads-debris.md:904; houses-and-yard.md:138, 870, 1054, 1274; urban-edge.md:126, 323, 411, 412, 629; earthworks-and-surfaces.md:159, 162, 243, 314, 428, 498, 757, 802, 853, 1213; vegetation.md:377, 775, 980; vehicles.md:133.

Open but not a failure: the LeafSet ids (vegetation.md:163) and the raw-wood set (vegetation.md:162) are chosen at build from the listed pages; the asset-artist pins them.

Store and product pages appear only as sources for published dimensions, never as sources of a file: epicentrk.ua (houses-and-yard.md:51, 1094), build.rozetka.com.ua (:1041), dancovershop.com (:797), prom.uz (earthworks-and-surfaces.md:41, urban-edge.md:35), satu.kz (lines-roads-debris.md:148), catalog.onliner.by (:1069), productcenter.ru (six rows). D-003 is not involved; noted only.

## Gaps against the drone (Gap stated against the drone)

w(θ) = 0.254 + 0.32·(|cos θ| + |sin θ|): 0.574 m nose-first, 0.706 m at 45°. Every opening gives a clear width and a verdict. Recomputed:

| Opening (file:line) | W (m) | Recomputed | Sheet says | Match |
|---|---|---|---|---|
| H01 door (houses:171) | 0.80 | any yaw, 113 / 47 mm | any yaw, 113 / 47 | yes |
| H01 window, frame on (houses:172) | 0.66 | ±18.8°, 43 mm | ±18°, 43 | yes (rounded down) |
| H05 window (houses:430) | 0.70 | ±35.2°, 63 mm | ±35°, 63 | yes |
| O05 door (houses:773) | 0.65 | ±16.1°, 38 mm | ±16°, 38 | yes |
| C01 doorway, PR-9 (houses:895) | 0.70 | ±35.2°, 63 mm | ±35°, 63 | yes |
| C02 door (houses:959) | 0.75 | any yaw, 88 / 22 mm | any yaw, 88 / 22 | yes |
| O03 window, frame on (houses:671) | 0.76 | any yaw, 27 mm diag | 27 | yes |
| H03 door between stops (houses:307) | 0.77 | any yaw, 32 mm diag | 32 | yes |
| Y05 wicket (houses:1235) | 0.90 | any yaw, 97 mm diag | 97 | yes |
| Y06 wicket (houses:1289) | 0.85 | any yaw, 72 mm diag | 72 | yes |
| E02 boards (earthworks:334) | 0.74 | 83 / 17 mm | 83 / 17 | yes |
| E02 wattle (earthworks:334) | 0.84 | 133 / 67 mm | 133 / 67 | yes |
| E04 doorway (earthworks:447) | 0.80 | 113 / 47 mm | 113 / 47 | yes |
| U01 window, frame and mullion (urban:201) | 0.54 | no | no | yes |
| U01 balcony door, frame on (urban:205) | 0.65 | ±16.1°, 38 mm | ±16°, 38 | yes |
| U01 interior door (urban:206) | 0.72 | any yaw, 7 / 73 mm | 7 / 73 | yes |
| U03 pit (urban:580) | 0.78 | any yaw, 37 mm diag | 37 | yes |
| P05 phase spacing (urban:904) | 0.70 | ±35.2°, 63 mm | ±35°, 63 | yes |
| Car rear-row cabin (vehicles:112) | 0.65 | ±16.1°, 38 mm | ±16°, 38 | yes |
| R09 bare-bow side bay (vehicles:781) | 0.83 | any yaw, 62 / 128 mm | 62 / 128 | yes |

Also checked: L01's brace triangle (lines-roads-debris.md:191) recomputes to an inscribed diameter of 1.77 m, any yaw below 2.99 m and nose-first below 3.28 m (sheet: 1.78 m, 3.0 m, 3.3 m); E01's width-at-height table (earthworks-and-surfaces.md:266–270) recomputes exactly (narrow cut nose-first only in the top 0.39 m; default yawed only in the top 0.70 m). The ±18° for H01's framed window rounds the 18.8° limit down, which is the safe side.

## OPSEC (the reviewer's read)

**Clear on the hard rules:** no reference file name or camera-style name (grep for media extensions and camera prefixes: none); the only `reference/` paths are the project's own ignored folders `reference/_compare/` and `reference/_frames/<letter>/zoom/`; no OSD value (OSD elements are described only as marks and bars, with their screen behaviour); no person, unit, plate or vehicle marking (vehicles.md:64 and :246: plates blank or absent); no coordinate; no date of a reference (seasons and time of day appear because REAL-02 asks for them). Dates inside source URLs are publication or archive dates of public pages, not dates of the references.

**Findings** (anything doubtful is listed; required changes R1–R3):

| # | file:line | What | Why it is doubtful | Severity |
|---|---|---|---|---|
| O-1 | earthworks-and-surfaces.md:32 | a news photo report on army trench works in a named frontline oblast | local news with photos of military positions in a named region, cited beside the measured trench sizes | high |
| O-2 | earthworks-and-surfaces.md:34 | an article on trench construction "on the eastern front" (its URL slug) | regional military reporting | medium |
| O-3 | earthworks-and-surfaces.md:31 | a news item on craters counted in a named part of the country; a satellite-crater abstract (not readable here) | regional news; the abstract may name a region | medium |
| O-4 | vegetation.md:1014 | city-council news on ragweed control in a named city; "a city ruderal survey"; "a village survey" | local news, and by the sheet's own words each survey is of one settlement | medium |
| O-5 | vegetation.md:307, :810 | the belt-survey record | its record inventories one shelterbelt between a named city and a named village: a URL pointing at a specific site | medium |
| O-6 | vegetation.md:240 | "Locust-dominated belts … are the southern-steppe type; V01 is the default belt tree for the map" | ties the map (a copy of the references) to a sub-national zone | medium |
| O-7 | vegetation.md:188, 249 ("Northern-Steppe"), 362 ("in eastern Ukraine"), 810 ("in the Pontic steppe"), 825 ("in the south"), 296, 351, 497 ("forest-steppe belts") | regional zone names in research summaries | each is weak, but together with O-1…O-6 they point at one part of the country | low |
| O-8 | houses-and-yard.md:1161 | "shell-rock or limestone" for the D stone blocks | shell-rock is a regionally diagnostic stone; the look needs only "pale porous stone" | low |
| O-9 | houses-and-yard.md:243 ("in the pilot's villages"), 316 ("the pilot's couple of houses") | wording about the pilot's real places | rephrase to the reference views or the map's villages | low |
| O-10 | urban-edge.md:46, 377 | "a phone's portrait field", "a phone's 26 mm-equivalent main lens" | device detail of the pilot's photos; an assumed camera field is enough | low |

**Doubts for the orchestrator, not blocking:**
- **The I and K scene as a layout.** urban-edge.md:45–46 and the placement rules at :232–233 and :482 fix the I composition with distances (block 60–80 m, roof pile 22–26 m and 3–6 m from the two-storey house, fence 5–8 m), and U01's shipped kit reproduces one real building's features (torn end, burnt middle sections, a painted band, adhesive dabs). REAL-02 asks for the view; REAL-04 forbids layouts traceable to a real location. Each feature is a class feature, so I read this as allowed, but the world should place it as a composite, and the orchestrator should confirm that reading. The J street (houses-and-yard.md:24, :316) is the same question at a smaller scale.
- urban-edge.md:23 cites a mapping-project wiki page about a named foreign city's building series. Not a map link and not near the map, but a non-city catalogue would be cleaner.

## Buildability recorded

`review-buildability.md` exists (330 lines): per sheet, what is buildable as written, what was fixed (six commits) and what was sent back, by owner; the cross-sheet table; the physics ids collected once; the look-lock buildability table. Spot-checks of its fixes in the sheets:

| Fix (commit) | Where it now is | Present |
|---|---|---|
| "From `damage`" defined, I/D/X at 0, 0.55, 1.0 (`9818e85`) | houses-and-yard.md:74 | yes |
| `window_state` and `burnt` added (`9818e85`) | houses-and-yard.md:94, 105 | yes |
| `rubble_volume` 0–150 m³, removed × 1.86, λ spread (`9818e85`) | houses-and-yard.md:71, 106 | yes |
| H03 door about 0.77 m between stops, any yaw; Y05 97 mm a side (`9818e85`) | houses-and-yard.md:307, 1235 | yes |
| 19 vegetation parameters and the V20 table (`158e724`) | vegetation.md:94–100, 116–126, 143–148, 1206–1216 | yes |
| S11 cites RD01, potholes not counted twice, S04 instance kit (`ff1d2e4`) | earthworks-and-surfaces.md:1203, 1230, 879–891 | yes |
| K06 lintel 0.25 m and bars 5–14 mm, L05 to 6.5 m (`342b8a1`) | lines-roads-debris.md:1016, 1020, 411, 447 | yes |
| Vehicle body profile parameters (`ced52e9`) | vehicles.md:204–209 | yes |
| P05 insulators `masonry`, no conductor gap (`8bb1971`) | urban-edge.md:903–904 | yes |

The parameter scan confirms the review's claim: 778 rows in 75 tables, no empty effect; 8 rows give "—" as range for a position choice ("side or none"), which is a discrete choice (S5).

**Stale record (R6):** review-buildability.md:225 and :321 still say U02b is sent back and that "the coverage scenario cannot pass". U02b was added afterwards (`1340cc1`, task 1.8) and it introduces a new `roof_pile` generator (urban-edge.md:486–525). Its table is complete by my scan, but no asset-artist has said it is buildable.

## Consistency

| Figure | Sheets | Agree |
|---|---|---|
| K06 bars | slab-end bars 5–14 mm (lines-roads-debris.md:1020, urban-edge.md:173, :1003); lintel bars 8–12 mm, 2–4 per lintel (houses-and-yard.md:45, lines-roads-debris.md:1011, :1016, urban-edge.md:115, :174, :402); balcony bars 10–12 mm (urban-edge.md:175); lintel piece 0.12–0.14 × 0.25 m (houses-and-yard.md:45, lines-roads-debris.md:1016) | yes |
| Cable diameters | L03 thin 3–6, medium 7–12, thick 13–32 mm (lines-roads-debris.md:276–278 and asset-list.md:147); L04 and L05 3–15 mm cover U01's 5–10 mm (urban-edge.md:1003); Y03 top wire and Y11 line are the thin class (houses-and-yard.md:1121, 1551) | yes |
| Rubble spread | exp(−d / λ): tile 1.0, brick 1.5, timber 2, slate 4, sheet 6 m (lines-roads-debris.md:674–685, houses-and-yard.md:71, :251, urban-edge.md:397, :443); bulking 1.86 (houses-and-yard.md:71, :106, urban-edge.md:313, :355, :508); village heap mixture 40/35/20/5 % (houses-and-yard.md:71, lines-roads-debris.md:687); K mound mixture is U02's `piece_mix` (lines-roads-debris.md:687, urban-edge.md:359) | yes, one stale figure (S2) |
| Physics ids | every `proposed:` id used in the sheets (asbestos_cement, asphalt, fabric, glass_crumbs, plastic, plastic_film, rubber, soil, straw) plus bark, twig, deadwood, forb_stem, mesh, ceramic, foam is in the one table at review-buildability.md:249–266; the closed list in `assets-src/physics_materials.json` is the 8 ids the sheets cite | yes |

## Requirements

| ID | Status | Why |
|---|---|---|
| AST-02 | met | letters, research URLs, dimensions with ranges, construction, materials, colours (photo and feed), damage and physics role on all 103 rows |
| AST-04 | partly met | bricks and storeys as given; H06's door contradicts the exact door size (R5) |
| AST-05 | met (sheet level) | collision plans in primitives, closed or flagged ids, gaps with widths and verdicts, porosity per part, snag records, landable surfaces |
| AST-06 | met | I/D/X and A/B/F states as physical edits; protruding rebar; damage geometry carries collision |
| SEC-01 | met | nothing from `reference/` committed, no reference file names |
| SEC-02 | partly met | letters only and no specifics of the references; location-specific sources and regional framing remain (R1–R3) |
| PRC-06 | met | research throughout; every sheet ends "Pilot: nothing" |
| PRC-09 | met as far as git shows | early, small commits per task; parallelism is not visible in git (not testable) |
| D-003 | met | CC0 sets only; no file was added, the manifest is unchanged |

## Hygiene

```
$ git log --no-merges --format='%h %an <%ae> [%s] body=[%b]' 9c06593..HEAD -- docs openspec/changes/p1-object-sheets
24 commits: 12 Reference Analyst <reference@450fpv.local>, 7 Asset Artist <assets@450fpv.local>,
            5 Orchestrator <orchestrator@450fpv.local>; every body empty, no trailer, no co-author line
max subject length 69 (limit 72); all type(scope): summary
```

- Identities match `docs/team.md`. Two subjects carry letter names or acronyms in capitals mid-summary (`716c869`, `cc4d6c6`); the hook checks only the first letter of the summary, and both start lower case.
- The diff holds the 11 expected files and nothing else: no image, no media, nothing under `reference/`, `Library/`, `Temp/` or `Logs/`, no third-party file (the manifest is unchanged, D-003).
- `openspec validate p1-object-sheets --strict`: valid. Hygiene scan of HEAD: 0 findings.

## Required changes

- **R1 (reference-analyst, OPSEC).** Replace the location-specific sources O-1 to O-5 with sources that give the same fact without pointing at a place: an encyclopedia entry, a manual or a standard. Where none exists, keep the number and label it assumed.
  - earthworks-and-surfaces.md:31, 32, 34
  - vegetation.md:307, 810, 1014
  - Check the satellite-crater abstract (earthworks-and-surfaces.md:31) names no region before keeping it.
- **R2 (reference-analyst, OPSEC).** Remove the regional framing O-6 to O-8. The zone names change no number:
  - vegetation.md:240 becomes "V01 is the default belt tree for the map"
  - vegetation.md:188, 249, 296, 351, 362, 497, 810, 825 use class words ("shelterbelts", "belts")
  - houses-and-yard.md:1161 describes the stone by its look
  - Keep the region-level academic records at vegetation.md:188 only if the orchestrator accepts them; otherwise replace them too.
- **R3 (reference-analyst, OPSEC).** Neutral wording for O-9 and O-10: houses-and-yard.md:243 and 316 ("the reference views", "the map's villages"); urban-edge.md:46 and 377 ("a camera field of about 50°, assumed").
- **R4 (reference-analyst, labels).** Give the 29 rows listed under Labels one of the four labels (terrain.md values as "measured (terrain.md Xn, letter L)" with the letter terrain.md gives; asset-list values with the label the figure has, usually assumed or published; E01's lip "derived" or "assumed"). Give the 7 "measured" rows their letter, or relabel them published or assumed.
- **R5 (reference-analyst, or an orchestrator decision).** Set H06's door to AST-04's 0.8 × 1.8–1.9 m (houses-and-yard.md:464; its gap line at :491 still passes at any yaw). Or the orchestrator records that AST-04's door size is the village-house door, so the block, the shop and the veranda keep their own labelled sizes (urban-edge.md:99, 102–104, 623; houses-and-yard.md:374).
- **R6 (asset-artist).** Add a U02b entry to review-buildability.md (buildable as written or sent back, for the `roof_pile` generator at urban-edge.md:486–525). Close the "sent back" items at :225 and :321.

## Suggestions (not blocking)

- **S1:** the 14 inherited dimension rows could name their label too, for example "assumed (as E01)".
- **S2:** lines-roads-debris.md:1047 says K06 is "10 % by volume" in K-type mounds, but U02's `piece_mix` (the one source, urban-edge.md:359) gives concrete 0.08.
- **S3:** design.md's decision table lists urban-edge as U01–U03 and P01–P06; U02b is missing there.
- **S4:** 115 dimension rows give "—" as the chosen default. They are descriptive rows (patterns, kinds), and the generator tables carry the defaults.
- **S5:** houses-and-yard.md:1563 `tipped` has no range; it should be 0–1.
- **S6 (orchestrator):** AST-04's "storeys 2.8 m for houses" and asset-list H01's measured 2.3–2.6 m walls predate this change. A one-line decision on which wins for adobe houses would close it.
- **S7:** the S02–S13 physics sections omit "Gaps, wind volumes: none", which S01 states.

Verdict: CHANGES REQUESTED
