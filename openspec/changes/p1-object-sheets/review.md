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

## Re-review (after 1.9 and 1.10)

**Reviewer:** qa-engineer, a fresh agent, independent of the rework · **Branch:** `unity/ref/p1-object-sheets` at `5744340`. `origin/unity/main` has moved on, but the merge base is still `e6ee5d6`, so `origin/unity/main...HEAD` is this change alone · **Rework read:** the 11 commits after `70812fb` (`d066f1a` D-032 and AST-04; `6bdf6fc`, `d1f7db5`, `f9ccc55`, `8921aa4`, `7f7bb1b`, `4ca3e95` sheets; `19dbfa3` AST-04 rewording; `5fd5cd6` tasks; `6c6b19e`, `5744340` U02b buildability), D-032, AST-04 as reworded, all six sheets in full, `asset-list.md` and `review-buildability.md` · **Not opened:** `reference/`. Line numbers are at `5744340`.

Every check below was run again by me. The scripts are new throwaway files in my scratchpad (run with `python -I`, not committed).

### Checks run

```
$ python tools/hygiene/precommit.py --tree HEAD
364 files scanned, 0 findings                                          exit=0

$ openspec validate p1-object-sheets --strict
Change 'p1-object-sheets' is valid                                     exit=0

$ python -I coverage.py .        # asset-list §1–10 IDs against "## <ID> " headings; 9 template sections + Seen in + Research sources per row
asset-list IDs (sec 1-10): 103 distinct: 103
sheet ## <ID> headings: 103 distinct: 103
missing: []   extras: []   duplicates: {}
per sheet: earthworks-and-surfaces 20, houses-and-yard 25, lines-roads-debris 17, urban-edge 10, vegetation 21, vehicles 10
rows checked: 103 rows with gaps: 0

$ python -I labels.py . 20261 --measured   # every row of every "### Dimensions" table (a row with two label words counts under both)
earthworks-and-surfaces.md rows 145  measured 54 published 27 derived 20 assumed  60 carried 16  no-label 0
houses-and-yard.md         rows 165  measured 50 published 17 derived 13 assumed 101 carried  0  no-label 0
lines-roads-debris.md      rows 128  measured 35 published 29 derived 15 assumed  86 carried  0  no-label 0
urban-edge.md              rows 121  measured 31 published 27 derived 24 assumed  71 carried  0  no-label 0
vegetation.md              rows 147  measured 45 published 63 derived  7 assumed  63 carried  0  no-label 0
vehicles.md                rows 138  measured  8 published 68 derived 26 assumed  48 carried  0  no-label 0
dimension rows total: 844 {'labelled': 844}
rows with no label word: (none)
'measured' with no letter A-P in the label cell: (none)
measured rows whose only letter match is A, D or I: 43, all read by eye: each is the letter (none is a diameter "D" or an article "A")

$ python -I materials.py . 20262  # "**Materials" bullets and their sub-bullets, and the group CC0 candidate tables
material lines found: 156
domains in material lines: {'polyhaven.com': 148, 'ambientcg.com': 30, 'www.texturecan.com': 2}
flagged (store/paid/NC/ND/OFL): []
(the domain counts equal the first pass's exactly; the line count differs only in how sub-bullets are split)

$ python -I params.py . U02b      # every row of every "| Param |" table
generator tables: 75 param rows: 778
rows with empty effect: 0
rows with no range and no range-bearing type: 2
  houses-and-yard.md:1563 `tipped` type=[per prop] range=[—]     (first-pass S5, still open)
  urban-edge.md:290 `mound` type=[seed] range=[—]                 (a seed: fine)
table under ## U02b Collapsed timber roof pile at urban-edge.md 495 rows 28

$ python -I urls.py . --all       # every URL in the six sheets
URL occurrences: 384 distinct URLs: 269 hosts: 74
(the 99 distinct URLs outside polyhaven.com, ambientcg.com and en.wikipedia.org were read one by one; judgements under R1–R3)

$ python -I gaps.py               # w(θ) = 0.254 + 0.32·(|cos θ| + |sin θ|); θmax = asin((W − 0.254)/0.4525) − 45°, checked by brute force
w(0) = 0.5740 m, w(45) = 0.7065 m
first pass's 20 openings: unchanged results; their sheet lines are untouched by the rework (git diff 70812fb..HEAD)
U02b: rafter L = 3.5 / cos 25° = 3.862 m; rise 1.632 m; L + 0.4 / cos 25° = 4.30 m
  held ridge: rise 2.2 m, run √(L² − rise²) = 3.174 m, flank 34.7°
  rafter_spread 0.30: L² − (b+s)² = +0.474 -> ridge 0.688 m above the feet
  rafter_spread 0.36: +0.014 -> 0.119 m
  rafter_spread 0.40: -0.296 -> no real ridge height
  rafter_spread 1.00: -5.336 -> no real ridge height
  spread limit L − b: H03 0.362, H02 0.310, H04 0.597, H01 0.807 m
  brick removed 30.5 × 0.38 × 2.4 − 4.9 − 1.8 = 21.12 m³ × 1.86 = 39.3 m³ (centreline perimeter of 9 × 7 m at 0.38 m: 30.48 m)
  ceiling fill 51 × 0.2 = 10.2 m³; core 40 + 10.2 ≈ 50 m³ over 80 m² = 0.63 m mean
  timber 1.29 + 0.43 + 0.8 + 2.5 + 1.6 = 6.62 m³; void 1 − 6.6/80 = 0.917; battens 2 slopes × 9.8 m × 9 rows = 176 m (sheet: 173 m)
  lean-to mouth 6.5 × 2 m: inscribed diameter 1.84 m (isosceles) / 1.70 m (right triangle); sheet: 1.8 m
  heap body 10 × 8 m seen 30–45° off its axis: 12.66–12.73 m across (sheet: 12.7–12.8 m)

$ git log --no-merges --format='%h %an <%ae> [%s] body=[%b]' e6ee5d6..HEAD
36 commits (11 since the first pass): 18 Reference Analyst <reference@450fpv.local>, 9 Asset Artist <assets@450fpv.local>,
8 Orchestrator <orchestrator@450fpv.local>, 1 QA Engineer <qa@450fpv.local>; every body empty; 0 co-author lines; longest subject 72 (the limit)
rework subjects and lengths: 49 docs(ref): record u02b buildability in the review
  69 docs(ref): fix u02b source brick, pitch, core volume and gap verdicts · 62 docs(openspec): record the qa rework tasks of p1-object-sheets
  60 docs: ast-04 wording for cellar and hut sizes per the sheets · 60 docs(ref): cite the door standard for the block's flat doors
  70 docs(ref): drop regional surveys and framing from the vegetation sheet · 71 docs(ref): drop a city catalogue and label rows in the urban edge sheet
  72 docs(ref): label inherited rows and replace a pole source in lines sheet · 70 docs(ref): neutral wording, d-032 sizes and labels in the houses sheet
  66 docs(ref): generic sources and full labels in the earthworks sheet · 64 docs: d-032, measured sizes beat ast-04 and views are composites

$ git diff --name-only origin/unity/main...HEAD
docs/asset-list.md  docs/decisions.md  docs/requirements.md  docs/reference-notes/terrain.md
docs/object-sheets/{earthworks-and-surfaces,houses-and-yard,lines-roads-debris,urban-edge,vegetation,vehicles}.md
openspec/changes/p1-object-sheets/{design,review-buildability,review,tasks}.md
$ git diff --name-only origin/unity/main...HEAD | grep -iE '^reference/|\.(png|jpe?g|gif|webp|tiff?|mp4|mov|mkv|avi|exr|hdr|wav|mp3)$|(^|/)(Library|Temp|Logs|UserSettings)/'
(no output)                                                            exit=1
```

Identities match `docs/team.md`. The diff holds 14 expected docs and change files; the manifest is untouched (D-003).

Public pages I opened to judge sources (read only): the trench guide at thinkdefence.co.uk (earthworks-and-surfaces.md:31) is a generic field-construction guide that names no site, region or front; the tree-restoration article at trees.org.uk (vegetation.md:229, :508, :610) names the country only; the ragweed article at zaxid.net (vegetation.md:1014) is general advice with no sub-national place in its body; the clay-building item at repo.snau.edu.ua (houses-and-yard.md:37) is a conference paper on natural wall materials in general (the cited bitstream URL itself returns 404, S9); the two hut overviews at unian.ua and osvita.ua (houses-and-yard.md:398) describe the type across the country, regions only in passing; the SIP file on the procurement portal (lines-roads-debris.md:76) is a cable maker's catalogue with no buyer, place or site. The substation item at ena.lpnu.ua (urban-edge.md:34) returned HTTP 500 twice and is judged on the sheet's own generic description.

### R1–R6

| # | Status | Evidence |
|---|---|---|
| R1 location-specific sources | **closed** | earthworks-and-surfaces.md:30–33: the news photo report, the regional trench article and the crater-count news and abstract are gone; trenches cite an encyclopedia mirror and a generic guide, dugouts an encyclopedia (:33). vegetation.md:188, :307, :810: the stand records, the belt survey and the community forestry PDF are gone; the belt structure, the belt mix and the shrub mix are labelled assumed. vegetation.md:1014: the city news, the city survey and the village survey are gone (Wikipedia plus a generic advice article). The satellite abstract is gone, so its region question is moot. Beyond R1's list, lines-roads-debris.md:212 dropped a registry abstract and urban-edge.md's research table (the row after :22) a city's building-series catalogue. Host-by-host read of the remaining 99 non-library URLs: standards hosts, encyclopedias, manufacturer and store product pages, journals, university repositories and general how-to articles; none is about a named place, oblast, front or site (D-032). |
| R2 regional framing | **closed, one remainder** | vegetation.md:240 now "V01 is the default belt tree for the map"; the zone names at :188, :249, :296, :351, :362, :497, :810, :825 are gone (grep for steppe sub-zones, "eastern", "southern", "forest-steppe", oblast and city names: none); houses-and-yard.md:1161 "pale porous stone (a stone chosen for the invented map)". **Remainder:** vegetation.md:398 "more in the drier south", a zone phrase of O-7's class that the first pass also missed (R8 f). |
| R3 pilot and device wording | **closed** | houses-and-yard.md:243 "the reference views … on the map", :316 "the second village house type of the map"; urban-edge.md:45 "a camera field of about 50° in portrait, the photo's lens, assumed", :376 "the photo's lens, assumed". Grep for device and place wording ("phone", camera and lens makers, "pilot's villages"): none left; the remaining "phone" hits are phone cables and wires. |
| R4 labels | **closed** | 844 of 844 rows carry a label word; no "measured" row lacks its letter. Each relabelled row was checked against terrain.md: T1 → A, T3a → G, T5 → F, T6 → C, E, T7 → D, T8 → I, K, B4 → I, K, B5 → I, K, B6 → E, I, J, V1 → F, H, V2 → B, D, G, V4 → J, W2 → G, S6 → F: every letter the sheets now name is one terrain.md gives. |
| R5 doors against AST-04 | **partly closed** | H06's door is published (GOST 6629-88's 21-10 mark, a 0.9 × 2.0 m leaf, houses-and-yard.md:464) and still passes at any yaw (:491, 97 mm a side on the diagonal); U01's interior and flat doors are published (urban-edge.md:102, :103); D-032 and the reworded AST-04 record that measured and published sizes beat AST-04's assumed ones. **Open:** D-032 and AST-04 disagree with each other and with the houses sheet on the single-storey figure and on the doors (R7, R8). |
| R6 U02b buildability | **closed, one defect** | review-buildability.md:3, :216 (the U02b entry), :225–231 (the five `6c6b19e` fixes), :233 (the stale "sent back" note closed), :326–336 (the sent-back table no longer lists U02b). The 28 rows each have a range or a discrete choice and an effect. The fixes are in the sheet and recompute: source H03 in red brick (urban-edge.md:381, :497), 25° and 3.86 m rafters (:386), the 50 m³ core (:389), the gap verdicts (:459, :460). K03 sections (lines-roads-debris.md:832–835) and U02's bulking, core and voids (urban-edge.md:312–317) agree. **Defect:** `rafter_spread` (:509) runs to 1.0 m, past the domain of the formula it states (R9). |

The first pass's doubt about the I and K scene as a layout is answered by D-032 (reference views are composites, never site layouts). S6 is answered by D-032 too, but the answer opened R7.

### Gaps against the drone (Gap stated against the drone)

The first pass's 20 openings recompute unchanged and their lines did not move in content. New or changed verdicts:

| Opening (file:line) | W (m) | Recomputed | Sheet says | Match |
|---|---|---|---|---|
| H06 door, published leaf (houses:491) | 0.90 | any yaw, 97 mm diag | any yaw | yes |
| H04 veranda doorway (houses:374) | 0.90 | any yaw, 97 mm diag | any yaw | yes (its size is R7 c, R8 c) |
| U01 flat entrance door, leaf open (urban:206) | 0.80 | any yaw, 47 mm diag | 47 mm | yes |
| U02b rafter bay, battens gone (urban:458) | 0.74–1.1 (0.84–0.90 at H03) | any yaw, 17–197 mm diag (67–97 at H03) | any yaw | yes |
| U02b battened slate bay, 0.60–0.70 clear (urban:459) | 0.60–0.70 | ±4.9° 13 mm to ±35.2° 63 mm | ±5° to ±35°, 13–63 mm | yes |
| U02b battened slate at H03's 0.5 m battens (urban:459) | 0.45 | no | no | yes |
| U02b crossed members (urban:460) | 0.574–0.60 | none at 0.574; ±4.9°, 13 mm at 0.60 | ±5°, 13 mm at 0.6 | yes |
| U02b lean-to mouth (urban:461) | triangle 6.5 × 2 m | inscribed 1.70–1.84 m | 1.8 m | yes |

### Spec scenarios (updated)

| Requirement | Scenario | First pass | Re-review | Evidence |
|---|---|---|---|---|
| One sheet per class group, every row covered | Coverage check | PASS | **PASS** | 103 of 103, each once, no extras; 103 rows with all 11 markers |
| Measured, labelled numbers | Label present | FAIL (R4, R5) | **FAIL** (R7, R8) | 844 of 844 rows labelled and every measured row names its letter (R4 closed); but the AST-04 sizes are not "used as given" while AST-04, D-032 and the houses sheet disagree: single-storey walls (H03 2.9 m, H04 3.0 m against "walls 2.0–2.8 m"), H04's 0.9 m veranda door against the H01–H04 exact door, two block doors assumed where AST-04 says published; and one "published" claim lost its source (earthworks-and-surfaces.md:916) |
| Materials and colours resolve to the pipeline | Material resolves | PASS | **PASS** | 156 lines, CC0 hosts only, the manifest id `red_brick`, nothing flagged |
| Physics role in the pipeline's terms | Gap stated against the drone | PASS | **PASS** | 20 + 8 openings recomputed, 0 mismatches |
| OPSEC-clean | Hygiene scan passes | FAIL (R1–R3) | **PASS**, one low remainder (R8 f) | scan 0 findings; R1–R3 closed; no reference file name, OSD value, person, plate, coordinate or reference date; one zone phrase left at vegetation.md:398 |
| Buildable | Buildability review recorded | PASS, record stale (R6) | **PASS**, one defect in a fixed row (R9) | U02b entry and closures present; the sheet reflects all five fixes; `rafter_spread`'s range exceeds the effect it states |

### Requirements (updated)

| ID | First pass | Re-review | Why |
|---|---|---|---|
| AST-02 | met | met | unchanged content on all 103 rows; research lines now generic |
| AST-04 | partly met | **partly met** (R7, R8) | bricks, storeys of H06 and the block, the H01–H03 doors and H06's published door hold; the single-storey wall figure, H04's veranda door and two block doors do not match AST-04's own text |
| AST-05 | met (sheet level) | met (sheet level) | U02b's gap verdicts now follow the formula; polygons, porosities and snag records per the conventions |
| AST-06 | met | met | unchanged |
| SEC-01 | met | met | nothing from `reference/` in the diff, no reference file names |
| SEC-02 | partly met | **met**, one low remainder (R8 f) | location-specific sources, regional framing and device wording removed |
| PRC-06 | met | met | research throughout; every sheet ends "Pilot: nothing" |
| PRC-09 | met as far as git shows | met as far as git shows | small commits per sheet and per fix |
| D-003 | met | met | CC0 sets only; manifest untouched |
| D-032 | — | **partly applied** (R7, R8) | composite views and generic sources applied; the size rule is stated two ways (decisions.md:297, requirements.md:268) and the houses sheet follows the first wording |

### New findings

1. **D-032 and AST-04 disagree.** D-032 (decisions.md:297) says "AST-04 now reads 2.3–2.8 m for single-storey village houses" and ends "single-storey village walls are 2.0–2.8 m"; it lets "larger houses, shops and the block use published sizes". AST-04 (requirements.md:268), as reworded by `19dbfa3`, says "walls 2.0–2.8 m for single-storey village houses", names no shops, and says "Larger houses and the block use published door sizes".
2. **The houses sheet follows the first D-032 wording**, which `19dbfa3` replaced: houses-and-yard.md:11 "storeys 2.3–2.8 m" and village-house doors without the H01–H04 scope; :120 "inside AST-04's amended 2.3–2.8 m"; :464 "AST-04's exact … door is for the village houses H01–H05 and the outbuildings (D-032)"; :532 and :593 "AST-04's exact door (outbuildings)". The sizes are fine; the attributions are stale.
3. **H03 and H04 walls exceed AST-04's single-storey figure.** H03 2.7–3.1 m, default 2.9 m (houses-and-yard.md:266), H04 2.9–3.1 m, default 3.0 m (:343), each "derived: 2.8 m storey plus the cornice / a plinth step". They fit if the figure is a storey and the cornice or step sits above it; they do not fit "walls 2.0–2.8 m".
4. **H04's veranda doorway** is 0.9 × 2.0 m (houses-and-yard.md:374, in the Gaps line only, no Dimensions row, no label), while AST-04 fixes 0.8 × 1.8–1.9 m for H01–H04 doors. R5 named it; the rework did not reach it.
5. **Two block doors are assumed** where AST-04 says the block uses published door sizes: the section entrance (urban-edge.md:98) and the balcony door (:101). The shop's two doors (:622, :623) are assumed; D-032 lists shops, AST-04 does not.
6. **A "published" claim lost its source.** earthworks-and-surfaces.md:916: "no herb layer (published in vegetation.md: the herb layer is almost absent inside dense belts)". That source was the belt survey removed under R1; vegetation.md:1055 now reads the bare floor as measured (F, terrain.md T5).
7. **One regional phrase remains:** vegetation.md:398 "more in the drier south" (V04 placement). Not in the first pass's list; same class as O-7.
8. **`rafter_spread` exceeds its formula's domain.** urban-edge.md:509 gives 0–1.0 m and states the unheld ridge sits √(L² − (b + spread)²) above the feet, "never below the core". For the default H03 source (L 3.86 m, b 3.5 m) the root is real only up to spread = L − b = 0.36 m; at 0.4 m it is √(−0.30), and the range runs to 1.0. The limit is 0.31 m for H02, 0.60 m for H04 and 0.81 m for H01, so 1.0 m exceeds every source. "Never below the core" bounds a real height; it does not define one.

### Required changes

- **R7 (orchestrator, D-032 and AST-04).** One text in both, decisions.md:297 and requirements.md:268:
  - **a.** The single-storey figure: 2.3–2.8 or 2.0–2.8 m, and what it measures (the wall to the plate, or the storey without a cornice or plinth step), so that H03's 2.9 m and H04's 3.0 m walls either fit or are named as allowed.
  - **b.** The doors of the larger buildings: whether the block (and shops, if they stay in D-032) must use published sizes, or "published or assumed, labelled in their sheets".
  - **c.** Whether AST-04's exact door for H01–H04 is the entrance door only (H04's veranda doorway is 0.9 × 2.0 m).
- **R8 (reference-analyst, the sheets).**
  - **a.** houses-and-yard.md:11 and :120 use R7 a's figure and word; H03 (:266) and H04 (:343) sit inside it or cite the allowance.
  - **b.** The door attributions at houses-and-yard.md:11, :464, :532, :593 follow AST-04 as reworded: H01–H04 exact; H05, outbuildings and cellars their sheet's labelled size. The sizes stay.
  - **c.** H04's veranda doorway (:374) gets a Dimensions row with a label: AST-04's 0.8 × 1.9 m (any yaw, 47 mm a side on the diagonal) or its own size if R7 c allows it.
  - **d.** If R7 b keeps "published": published sizes for urban-edge.md:98 and :101 (and :622, :623 if shops stay).
  - **e.** earthworks-and-surfaces.md:916: relabel the bare belt floor measured (F, terrain.md T5), as vegetation.md:1055 now does.
  - **f.** vegetation.md:398: drop "more in the drier south", or use a class or map word ("more at dry belt edges").
- **R9 (asset-artist, U02b `rafter_spread`).** urban-edge.md:509: cap the range at the source's L − b (0.36 m for H03), or state what the generator does beyond it (the pair lies flat on the core at L − b; more spread parts the ridge).

### Suggestions (not blocking)

- **S8:** lines-roads-debris.md:464 cites the lane widths through a government press item whose URL slug carries an official's name; cite the ДБН text itself (dnaop.com hosts the norms). On the same line, "an unmaintained frontline road" reads as well as "an unmaintained road".
- **S9:** houses-and-yard.md:37's repository bitstream URL returns 404; the item's handle page (`/handle/123456789/7568`) is the stable link.
- **S10:** houses-and-yard.md:895 calls C01's 0.70 m doorway "the narrowest fly-in on the map", and :773 calls O05's door "the yard's second-narrowest fly-in after the cellar". O05's 0.65 m door (±16°), U01's framed balcony door (0.65 m) and a car's rear-row cabin are narrower. Say "the pilot's reference fly-in (PR-9)".
- **S11:** urban-edge.md:507 calls `rubble_volume` "the masonry core's volume" (default 40 m³); the core is 50 m³ with the ceiling fill (:389). Name it the brick part of the core.
- **S12:** urban-edge.md:466 derives the frame's porosity from 50 mm battens at 0.33 m (H01's tile), while the default H03 has battens at 0.5 m (0.81 open, not 0.77). The 0.6–0.8 range still holds.
- **S13:** vegetation.md:195 labels V01's size L height 12–16 m as measured against belt crowns of 6–15 m; the 16 m end is above the measurement (assumed).
- **S14:** review-buildability.md:233 cites the physics sign-off of `p1-pipeline-hardening`. That sign-off (`b67d084`, nine ids) is on its own unmerged branch, so the sheets correctly still write `proposed:`. Re-sync them when that change merges.
- **S15:** the first pass's S2 (lines-roads-debris.md:1047 "10 %" against U02's 0.08), S3 (design.md:14 lacks U02b), S5 (houses-and-yard.md:1563 `tipped` has no range) and S7 are still open.

Verdict: CHANGES REQUESTED

## Third pass (after R7–R9)

**Reviewer:** qa-engineer, a fresh agent, independent of the rework · **Branch:** `unity/ref/p1-object-sheets` at `449cd30`; the merge base with `origin/unity/main` is still `e6ee5d6` · **Scope:** R7, R8 a–f, R9 and S5, plus the cheap whole-tree checks. Coverage, materials, gaps and params were verified in full by the first two passes and are not redone; the rework touches no `##` heading, no material line and no gap verdict except the new H04 row. · **Rework read:** `git diff c3a9af3..HEAD` in full: `154474a` (D-032's "Wall and door sizes", AST-04), `df42c51` (the sheets, R8, R9, S5), `449cd30` (tasks 1.11) · **Not opened:** `reference/`. Line numbers are at `449cd30`.

### Checks run

```
$ python tools/hygiene/precommit.py --tree HEAD
364 files scanned, 0 findings                                          exit=0

$ openspec validate p1-object-sheets --strict
Change 'p1-object-sheets' is valid                                     exit=0

$ git log --format='%h %an <%ae> [%s] body=[%b]' c3a9af3..HEAD
449cd30 Reference Analyst <reference@450fpv.local> [docs(openspec): record qa rework task 1.11 of p1-object-sheets] body=[]
df42c51 Reference Analyst <reference@450fpv.local> [docs(ref): align the sheets with the settled wall and door rules] body=[]
154474a Orchestrator <orchestrator@450fpv.local> [docs: settle the ast-04 and d-032 wall and door rules per class] body=[]
subject lengths 62, 64, 63 (limit 72); co-author lines 0; lines in the bodies beyond the subjects: none

$ git diff --name-only origin/unity/main...HEAD
docs/asset-list.md  docs/decisions.md  docs/requirements.md  docs/reference-notes/terrain.md
docs/object-sheets/{earthworks-and-surfaces,houses-and-yard,lines-roads-debris,urban-edge,vegetation,vehicles}.md
openspec/changes/p1-object-sheets/{design,review-buildability,review,tasks}.md
$ ... | grep -iE '^reference/|\.(png|jpe?g|gif|webp|tiff?|mp4|mov|mkv|avi|exr|hdr|wav|mp3|fbx|blend)$|(^|/)(Library|Temp|Logs|UserSettings)/'
(no output)                                                            exit=1

$ git diff -U0 c3a9af3..HEAD -- docs/object-sheets   # the part, range and default cells of every changed table row, old against new
unchanged: H01 wall 2.3–2.6 / 2.4; H03 wall 2.7–3.1 / 2.9; H04 wall 2.9–3.1 / 3.0; H05 door 0.8 × 1.8; H06 door 0.9 × 2.0;
           O01, O02 door 0.8 × 1.8; O03 door 0.8 × 1.9 and windows; `rafter_spread` 0–1.0 / 0.3
changed:   `tipped` range — → 0–1 (S5); added: H04 "Veranda doorway | 0.9 × 2.0 m | —" (R8 c)

$ grep -niE 'south|north|east|west|steppe|oblast|region' <the six sheets>
judged one by one: "least", "stepped", pixel "regions" and "cards region" are not place words; houses-and-yard.md:609
("moss on the north face"), :1503 and :1513 (a greenhouse's axis and door) are orientations; :1186 ("in the south of
the map") places a fence variant on the invented map; earthworks-and-surfaces.md:50 "open rolling steppe meadow"
describes A's land cover (S18). No zone phrase of O-7's class is left.

$ python -I (arithmetic for R7 and R9)
L = 3.5 / cos 25° = 3.8618 m; L − b = 0.3618 m (sheet 0.36); H02 0.310, H04 0.597, H01 0.807 (sheet 0.31, 0.60, 0.81)
spread 0.30: L² − (b+s)² = +0.474 -> ridge 0.688 m above the feet; 0.36: +0.014 -> 0.119 m
spread 1.00: −5.336 -> clamped flat; each top b + s − L = 0.638 m short of the ridge line; parted width 1.276 m (sheet 1.28)
H03 standing wall, `standing_wall_h` 0.9: × (2.9 + 0.4) = 2.97 m (sheet "3.0 m"); × 2.9 = 2.61 m; at 1.0 with the plinth added: 3.3 m
H03 ridge at its defaults (9 × 7 m hip, 25°): 2.9 + 3.5 tan 25° = 4.53 m (plinth inside); 4.93 m (plinth added); J measures 5–6 m
```

Public pages opened to judge R8 d (read only, standards hosts): GOST 11214-86 at meganorm.ru (wooden windows and balcony doors with double glazing for residential and public buildings; its balcony-door marks are БС/БР 22-7,5, 22-9, 24-7,5 and 24-9, openings 2.2 or 2.4 m high); GOST 24698-81 at meganorm.ru (wooden external doors for residential and public buildings, types Н entrance and С service, single and two-leaf, marks such as ДН21-9 and 21-13); GOST 6629-88 at meganorm.ru, the source the sheets already cite (wooden **internal** doors for residential and public buildings, including type У doors into flats; it does not cover external doors).

### R7 (D-032 and AST-04)

| Item | Status | Evidence |
|---|---|---|
| a. the wall figure and what it measures | **closed** | decisions.md:297 and requirements.md:268 give the same five classes, the same numbers and the same measure, "to the plate, from grade outside": H05 2.0–2.3 m; H01–H02 2.3–2.6 m (E); H03–H04 2.6–3.1 m including plinth and cornice (J); two-storey 2.8 m a storey; the block 3.0–3.4 m a storey. The sheets' numbers sit inside: H01 2.4 (houses-and-yard.md:120), H02 as H01, H03 2.9 (:266), H04 3.0 (:343), H05 2.1 (:405), H06 2 × 2.8 m (:460), U01 3.0 m (urban-edge.md:75). The "(J)" provenance of the H03–H04 range is not true (R10). |
| b. the doors of the larger buildings | **closed in the rule** | both texts: "takes the size its sheet gives, labelled published where a standard exists, else measured or assumed", naming shops and the block's section entrances and balcony doors. The sheets do not yet follow the "published" branch (R8 d, R11). |
| c. the exact door is the entrance door only | **closed** | both texts: the entrance door of H01–H04 is exact at 0.8 × 1.8–1.9 m; H04's veranda doorway is named among "every other door". |
| no sentence contradicts the other | **holds** | Sentence by sentence: bricks (AST-04 only, unchanged); the entrance door (identical); the walls (identical but for "E" against "measured in E"); the other doors (identical lists; D-032 spells out the block's door kinds, AST-04 says "the block"). AST-04 also fixes the adobe windows at 0.8 × 1.0 m, which D-032 does not mention: silence, not a contradiction, and H01's measured E 7 window (:127) is that size (S19 for the wording). |

### R8 a–f, R9, S5

| # | Status | Evidence |
|---|---|---|
| R8 a | **closed** (the label it brings in is R10) | houses-and-yard.md:11 now restates AST-04's class ranges "to the plate, from grade outside"; :120 cites the H01–H02 range; :266 and :343 cite the H03–H04 range and sit inside it. |
| R8 b | **closed** | :11 (H01–H04 entrance door exact; the rest labelled), :409 (H05), :465 (H06), :533 (O01), :594 (O02), :653 (O03, "AST-04;" dropped from the label): each now says the exact door is H01–H04's entrance door and the others take their row's size. No size changed. A whole-docs grep for the stale forms ("2.3–2.8", "2.0–2.8", "village-house doors", "H01–H05", "amended 2") finds only the bus stop's unrelated 2.3–2.8 m clear height (urban-edge.md:815, :852); "AST-04 as amended" at :460 is still true. |
| R8 c | **closed** | :349 "Veranda doorway \| 0.9 × 2.0 m \| — \| assumed: a typical veranda door, sized by this sheet; AST-04's exact door is H04's entrance door only (D-032)"; the gap line :375 agrees (0.9 m ≥ 0.706: any yaw, 97 mm a side on the diagonal). |
| R8 d | **open** (R11) | The settled rule kept "published where a standard exists", and standards exist: urban-edge.md:98 (section entrance, 1.3 × 2.1 m double door) and :101 (balcony door, 0.75 × 2.1 m) are still "assumed", as are the shop's doors :622 and :623 (shops stayed in the rule). GOST 24698-81 covers external doors of residential and public buildings; GOST 11214-86 covers residential balcony doors, and none of its marks is 2.1 m high. Also, houses-and-yard.md:465 labels H06's door published from GOST 6629-88, the internal-door standard; the door is the house's outside door (the fly-in at :492). |
| R8 e | **closed** | earthworks-and-surfaces.md:916 "(measured F, terrain.md T5: the floor is beaten bare inside dense belts)"; terrain.md:112–117 T5 says so: clip F, "fine dry loam, beaten bare", "most of the ground in F"; it matches vegetation.md:1055. |
| R8 f | **closed** | vegetation.md:398 "more at dry belt edges"; the six-sheet grep above leaves no zone phrase. |
| R9 | **closed** | urban-edge.md:509 keeps 0–1.0 m and states the clamp: the root is real up to L − b (0.36 m for H03, 0.31 H02, 0.60 H04, 0.81 H01), the pair lies flat there, and beyond it each rafter's top stops b + spread − L short of the ridge line (1.28 m parted at 1.0 m for H03). Every figure recomputes (block above). The reference-analyst made the fix rather than the asset-artist the re-review named; the clamp is a plain geometric rule, buildable as written, and review-buildability.md needs no new entry. |
| S5 | **closed** | houses-and-yard.md:1564 `tipped` range 0–1. |

### Spec scenarios (updated)

| Requirement | Scenario | Re-review | Third pass | Evidence |
|---|---|---|---|---|
| One sheet per class group, every row covered | Coverage check | PASS | **PASS** | no heading touched by the rework (`git diff c3a9af3..HEAD` holds no `##` line) |
| Measured, labelled numbers | Label present | FAIL (R7, R8) | **FAIL** (R10, R11) | every row still carries a label word and the new H04 row is labelled; but the H03–H04 wall range is called measured in J with no J measurement behind it (R10), and five door rows do not follow AST-04's settled door rule: four assumed where a covering standard exists, one published from a standard that does not cover it (R11) |
| Materials and colours resolve to the pipeline | Material resolves | PASS | **PASS** | no material line changed |
| Physics role in the pipeline's terms | Gap stated against the drone | PASS | **PASS** | the new veranda row and its gap line agree; no other verdict changed |
| OPSEC-clean | Hygiene scan passes | PASS, one low remainder (R8 f) | **PASS** | scan 0 findings; R8 f closed; the regional grep judged hit by hit; no reference file name, place, date or OSD value in the rework |
| Buildable | Buildability review recorded | PASS, one defect (R9) | **PASS** | R9 closed; S5 closed |

### Requirements (updated)

| ID | Re-review | Third pass | Why |
|---|---|---|---|
| AST-02 | met | met | unchanged |
| AST-04 | partly met (R7, R8) | **partly met** (R10, R11) | one text with D-032, and every sheet number inside its class range; but the H03–H04 range carries a false "measured in J", and the block's, the shop's and H06's doors do not follow the door rule it now states |
| AST-05 | met (sheet level) | met (sheet level) | unchanged; the new row's gap verdict follows the formula |
| AST-06 | met | met | unchanged |
| SEC-01 | met | met | nothing from `reference/` in the diff, no reference file names |
| SEC-02 | met, one low remainder | **met** | R8 f closed |
| PRC-06 | met | met | research throughout; nothing asked of the pilot |
| PRC-09 | met as far as git shows | met as far as git shows | three small commits, one per role and step |
| D-003 | met | met | manifest untouched |
| D-032 | partly applied (R7, R8) | **partly applied** (R10, R11) | the wording is settled and the sheets follow its walls and its exact door; the H03–H04 provenance and the "published where a standard exists" branch are not yet true |

### New findings

1. **The H03–H04 wall range is labelled measured, and nothing measured it.** requirements.md:268 "(measured in J)", decisions.md:297 "(J)", houses-and-yard.md:11 "(measured J)", :266 and :343 "(measured J, D-032)". J measures H03's footprint and ridge only (terrain.md B2: "single storey, footprint 8–10 × 6–8 m, ridge 5–6 m"; houses-and-yard.md:265, :269); H03's own wall row is "derived: 2.8 m storey plus the cornice" and its plinth "assumed" (:268); H04 is in no letter at all (:336 "Seen in: R"). The range's floor, 2.6 m, is below both rows' ranges (2.7 and 2.9 m). D-032's lead "the sheets measured them" holds for the entrance door (E 7) and the adobe walls (E), not for this class. J's one height measurement, the ridge, does not support the range either at H03's defaults (S16).
2. **The door rule's "published where a standard exists" is not applied** (R8 d). See R8 d above: four rows assumed where GOST 24698-81 or GOST 11214-86 covers the door; H06's outside door published from the internal-door standard. The village-built doors (H04's veranda, H05, the outbuildings) are a different case: the measured village doors (0.8 × 1.8–1.9 m) match no standard mark (the door standards' openings are 2.1 m high and up), so the sheets' "assumed" there is defensible, but the rule's wording does not say where its "published" branch binds.

### Required changes

- **R10 (orchestrator and reference-analyst, the H03–H04 label).** Relabel the H03–H04 class range as what it is, for example "derived: the 2.8 m storey plus the plinth step or the cornice (the H03 and H04 rows)", in requirements.md:268, decisions.md:297 (and scope its "the sheets measured them" to the door and the adobe walls), houses-and-yard.md:11, :266 and :343. No number changes. Or measure H03's wall in J with its still and give that row; H04 stays derived either way.
- **R11 (reference-analyst, or an orchestrator decision; R8 d).** Either:
  - **a.** the rows follow the settled rule: urban-edge.md:98 and :622, :623 cite GOST 24698-81's mark for the door they show, and :101 cites GOST 11214-86's (a 22 dm high mark, so the balcony opening becomes 0.75 × 2.2 m; the gap verdicts at :203–204 do not change); or
  - **b.** the orchestrator states in AST-04 and D-032 where "published where a standard exists" binds (for example, the block and the shop only, or "where the sheet cites a standard"), and the sheets follow that.
  - Either way, houses-and-yard.md:465 drops GOST 6629-88 for H06's outside door: cite GOST 24698-81's mark, or label the 0.9 × 2.0 m leaf assumed. The gap verdict (:492, any yaw) does not change.

### Suggestions (not blocking)

- **S16 (orchestrator's known item, judged):** the plinth. U02b reads H03's 2.9 m wall as standing on the 0.4 m plinth (urban-edge.md:381 "walls 0.38 m to 2.9 m on a 0.4 m plinth"; :503 `standing_wall_h` "× wall height plus plinth", 3.0 m for H03 = 0.9 × 3.3), and so does H03's own derivation (:266 "2.8 m storey plus the cornice" beside a separate plinth row, :268); AST-04 now puts the plinth inside the 2.6–3.1 m. At `standing_wall_h` 1.0 the U02b reading gives 3.3 m, outside AST-04. H03's envelope does not close at its defaults under either reading: 2.9 + 3.5 tan 25° = 4.53 m to the ridge (4.93 m with the plinth added) against J's measured 5–6 m and the 5.5 m default; only walls of 3.0–3.1 m with pitches of 29–30° reach 5.0 m. Not a scenario failure (no generator reads these yet; every row is labelled), but it must be settled before H03's or U02b's generator is written, or the REAL-03 sheet against J will show a low roof.
- **S17 (orchestrator's known item, judged):** the shared `wall_height` range (houses-and-yard.md:79, 2.2–3.0 m, "Eaves height") is narrower than AST-04's classes (2.0–3.1 m), and H05's preset already sits outside it (`wall_height` 2.1, :440). Widen it to 2.0–3.1 m and call it "to the plate, from grade" when the shared house generator is proposed.
- **S18:** earthworks-and-surfaces.md:50 "open rolling steppe meadow" describes A's land cover and names no place, but "steppe" is also a zone word; "open rolling grassland" gives the same look.
- **S19:** requirements.md:268 "and their windows 0.8 × 1.0 m for the adobe houses" reads as all H01–H04 windows, then narrows; houses-and-yard.md:11's "the windows of the adobe houses H01–H02 0.8 × 1.0 m" is clearer. D-032 could name the windows too, since AST-04 now fixes them.
- **S8–S14** of the re-review stay open, and from S15 the first pass's S2, S3 and S7 (S5 is closed).

Verdict: CHANGES REQUESTED
