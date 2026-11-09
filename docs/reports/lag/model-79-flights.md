# The model-79 lag: three flights read from their logs (L1)

QA, unit L1 of the final world's plan (phase L). Build line `unity/physics/physics-integration-2` at `42dbbcda` (model 79, log format 69), the line the pilot's build was made from. Videos by letter only: A is flight 35, B flight 37, C flight 38 of the session's 39 flights. Every log replays bit for bit on this line (below), so every number here is the pilot's flight, not a re-flight.

## Verdict

**The sim fell behind real time in all three. Neither the render, the input nor a physics state caused it.** The fiber's contact queries pushed the player's step past its 1 ms budget, and the scheduler then ran every late step without dropping any. So the delay kept growing for as long as the line stayed where it was.

- **The query that costs:** `FiberContacts.Prepare` runs `ShapeHits` on every segment of the line that lies within the 0.5 m clearance margin of a non-ground shape. That is one `StaticContacts` query per segment, every step.
- **What the query costs:** it grows with the number of shapes in the segment's reach. The reach is the segment's bounding box, and a far segment of the chain is 20 to 200 m long.
- **How it feels:** the FPV feed shows a new PAL field only once the sim has passed it (`FlightScene.Latch`). So at 1.4 ms per step the picture runs at about 0.7× speed, and at about 35 fields a second instead of 50, while the render loop keeps its 40 to 48 fps. Every stick input also lands as late as the backlog. Those are the pilot's three words: sluggish, strange controls, a dropping frame rate.

| Flight (video) | Bug (wall, ±2 s) | The sim fell behind at | What the line was on | Player step (Mono) | .NET step, same seconds | Worst backlog |
|---|---|---|---|---|---|---|
| 35 (A) | ~158 s | 158.5 s, as the drone left the yard | the yard's gate (`Z05_gate_plank`) and house (`Z01_house_adobe`), wrapped round them by a 130–155 s hover, and a 21 m segment from the house corner | 1.27 → 1.56 ms, then 1.40 ms to the end | 543 → 814 µs, then ~500 µs | **25.6 s** (18,070 stick reports dropped) |
| 37 (B) | ~12 s | 13.0 s, as the drone sank into the posadka's south straw verge | S04 straw: 28 of 32 nodes over the patch, about 25 contact slots held by standing stems | 1.71 ms | 1,056 µs | 0.98 s |
| 38 (C) | ~63 s | 64.4 s, the drone about 280 m away over open ground | S04 straw, at the laid end: one 76 to 199 m segment from the track start lies across the north verge's west corner, 61 shapes within the margin | 4.7 ms for ~170 steps, then 1.30 ms | 713 → 615 µs | 1.13 s (when the flight ended) |

**General, not straw-specific.** Two of the three met S04's straw. Flight 35 never touched straw: its line lay on the yard's gate and house.

- **The path is the same in all three:** uncertified segments are re-queried every step, at a cost set by the shapes in each segment's reach.
- **S04 is just the densest field of shapes on the test range,** and it lines both edges of the posadka along its whole 300 m, not only the patches near the start.
- **The farmland will meet this path everywhere** a laid line rests on trees, bushes, the village or its debris.

So by the plan's rule **L6 stands and folds into W3.** L5's lag cap is needed whatever L6 does: a step over 1 ms must cost a bounded hiccup, not a delay that grows to 25 or 48 s.

**Partly confirmed, the suspect:** "the fiber across the S04 patches keeps querying after the drone has left".
- **Confirmed:** the line keeps querying after the drone has gone. In C the queries come from the laid end at the start; in A from the yard, after the crash.
- **Not confirmed:** that it is about straw. A is the yard's gate and house.

## How the lag reaches the pilot

- **The scheduler** (`FlightScheduler.RunDue`) has step k due at start + k ms. A pass runs every due step back to back and logs one `Hitch` when it found two or more. So a mean step of c > 1 ms adds (c − 1 ms) of delay per step, and the delay only shrinks once c < 1 ms.
- **The sticks** (`StickInbox`): each step takes the sample in force at its due time. A backlog of L ms is therefore L ms of extra control latency. The ring holds 1,024 reports, about 4.1 s at 250 Hz. Beyond that, new reports are refused and counted.
  - The end count also takes in any report refused after the flight thread stopped. So the small counts (38 in flight 30, 137 in flight 33) may be the shutdown's.
  - The large ones, flight 35's 18,070 and flight 36's 30,749 (72 and 123 s of reports), are the backlog's.
- **The picture:** in the FPV view the feed drives its field from the sim's latest step. With no new field, "the field shown is held" (`FlightScene.Latch`). So the feed advances at the sim's pace, not the wall clock's, and the render counter never shows it.
- **The cost estimate:** the player's own cost per step comes from consecutive late passes. A pass of n steps that started L₁ late, followed at once by one L₂ late, gives (n·1000 + L₂ − L₁)/n µs per step. Only passes of n ≥ 5 count: those end still behind, so no yield or sleep falls between the two.

## The session: one line per flight

`lagprobe hitches` over the 39 logs `Player.log` lists. All 39 were flown from the track start, 2 kg, legs on, Severe wind, Sunny, 14:00. "Behind >50 ms" and ">1 s" are seconds of wall time spent that far behind. The player's cost is from passes of 5 steps or more.

```
#  | flight seed          | s flown | passes | steps caught up | max late ms | behind >50 ms: s, first at | behind >1 s: s | player us/step in passes p50/p90 | pauses | log waits | sticks dropped
1  | 0x51BC404D8591F308 |    10.1 |    578 |            3531 |       303.2 |     1.7,     0.1 s |            0.0 |  843 / 1043 |      0 |         0 | 0
2  | 0x3FDF718B6F154298 |     6.9 |    236 |            2508 |       219.8 |     1.4,     2.3 s |            0.0 | 1136 / 1484 |      0 |         0 | 0
3  | 0x6F59C5B293A2A6E7 |    12.0 |    396 |            3043 |       449.6 |     1.2,    10.9 s |            0.0 |  982 / 1209 |      0 |         0 | 0
4  | 0x91BA94864118BDA5 |    17.6 |    843 |            4880 |       170.2 |     1.4,    13.9 s |            0.0 |  893 / 1394 |      0 |         0 | 0
5  | 0xBAC448155EA7A33F |     5.9 |    374 |            1756 |        20.0 |     0.0,         - |            0.0 |  934 / 1238 |      0 |         0 | 0
6  | 0xCC9DD6F3F60DC19D |    16.3 |    445 |           10162 |      3325.0 |     8.2,     6.5 s |            3.3 | 1069 / 1357 |      0 |         0 | 0
7  | 0x9633648AF7D66B5E |    15.3 |    640 |            7595 |       907.0 |     4.7,     1.7 s |            0.0 | 1013 / 1295 |      0 |         0 | 0
8  | 0xB9697BBD31BBA930 |    18.1 |   1120 |           13687 |      5701.6 |    10.3,     0.2 s |            5.7 |  976 / 1472 |      0 |         0 | 820
9  | 0x4344249DEC847CED |    16.1 |    588 |            6528 |      1055.5 |     2.0,    14.4 s |            0.0 |  979 / 1276 |      0 |         0 | 0
10 | 0x458A6C9B737366C7 |     0.7 |      0 |               0 |         0.0 |     0.0,         - |            0.0 |           - |      0 |         0 | 0
11 | 0x2645AE8CC89A6356 |     0.7 |      0 |               0 |         0.0 |     0.0,         - |            0.0 |           - |      0 |         0 | 0
12 | 0x67FE01033BAB9026 |     1.2 |      0 |               0 |         0.0 |     0.0,         - |            0.0 |           - |      0 |         0 | 0
13 | 0xFA6DB0DD46C614A3 |     0.2 |     24 |             165 |        30.0 |     0.0,         - |            0.0 | 1248 / 1440 |      0 |         0 | 0
14 | 0xAD1007F9D9ACAFE4 |     0.0 |      1 |              29 |        28.8 |     0.0,         - |            0.0 |           - |      0 |         0 | 0
15 | 0x5FCD3B92ABE12082 |     0.0 |      2 |              47 |        23.5 |     0.0,         - |            0.0 |   951 / 951 |      0 |         0 | 0
16 | 0xB014005B542BF6CB |    10.7 |    428 |            4146 |       332.8 |     2.0,     0.7 s |            0.0 | 1072 / 1297 |      0 |         0 | 0
17 | 0x651C486FEEA133D1 |    13.0 |    371 |            5408 |       794.8 |     3.6,     3.4 s |            0.0 | 1066 / 1561 |      0 |         0 | 0
18 | 0xAFCC574EE72DA9A2 |     8.2 |    142 |            1288 |        88.0 |     0.6,     7.4 s |            0.0 |  937 / 1258 |      0 |         0 | 0
19 | 0x8DC5594365D1ACAF |    12.3 |    605 |            4949 |       172.8 |     2.7,     0.2 s |            0.0 |  854 / 1243 |      0 |         0 | 0
20 | 0x30E396A010477299 |    18.3 |   1093 |           18897 |      7617.8 |    13.7,    10.5 s |            7.6 |  925 / 1266 |      0 |         0 | 1186
21 | 0xAD3233AF9C731D22 |    16.5 |    491 |           12096 |      1817.3 |     7.9,     6.7 s |            1.8 | 1004 / 1231 |      0 |         0 | 0
22 | 0xD12011C9A6CFD36A |    17.0 |    669 |           10437 |      1982.4 |     7.9,     3.6 s |            2.0 | 1008 / 1412 |      0 |         0 | 0
23 | 0xCB81A371D968FA80 |    21.8 |    990 |           15690 |      1937.2 |    11.2,     0.2 s |            3.4 | 1007 / 1233 |      0 |         0 | 0
24 | 0x48C08ECA53395FF2 |    52.0 |    368 |           23891 |       486.2 |    21.1,     0.6 s |            0.0 |  973 / 1236 |      0 |         0 | 0
25 | 0xC11F6E78E345528F |     7.1 |      8 |              18 |         3.3 |     0.0,         - |            0.0 |           - |      0 |         0 | 0
26 | 0x2623CA423615F73A |     6.3 |     23 |             105 |        16.1 |     0.0,         - |            0.0 | 1246 / 1380 |      0 |         0 | 0
27 | 0x6CFB619E46158E75 |     6.4 |      1 |               3 |         2.4 |     0.0,         - |            0.0 |           - |      0 |         0 | 0
28 | 0x29CA2A083C854636 |     5.4 |     17 |              41 |         3.1 |     0.0,         - |            0.0 |           - |      0 |         0 | 0
29 | 0xF3F6166D0CF599FE |    48.4 |    827 |           10666 |      2893.7 |     6.5,    34.2 s |            2.9 |  997 / 1331 |      0 |         0 | 0
30 | 0x0DBDA966824E60C2 |    64.0 |    238 |            5155 |      1246.5 |     3.9,    60.4 s |            0.0 | 1004 / 1381 |      0 |         0 | 38
31 | 0x9BA21A5EFC19AF47 |    47.3 |    692 |           10384 |       634.1 |     5.6,    14.5 s |            0.0 | 1041 / 1390 |      0 |         0 | 0
32 | 0x6AC11CB5ED810D76 |    38.7 |   1025 |           17208 |       519.1 |    11.0,     3.9 s |            0.0 | 1001 / 1347 |      0 |         0 | 0
33 | 0xFEAAF7EE06E2C9DD |    26.1 |    492 |            8791 |      2474.6 |     5.9,     2.7 s |            0.0 | 1073 / 1424 |      0 |         0 | 137
34 | 0xF033E59A8D6ED47B |   110.3 |   6334 |           62380 |      3028.9 |    36.4,     5.5 s |            3.0 |  961 / 1288 |      0 |         0 | 0
35 | 0x9D23ED2E9DC5DD9C |   234.9 |   3288 |          110723 |     25597.8 |    97.1,     1.1 s |           81.2 |  997 / 1440 |      0 |         0 | 18070
36 | 0xA1851319BD1EAE82 |   105.8 |    327 |           94400 |     47773.0 |    92.5,    31.6 s |           88.2 | 1036 / 1427 |      0 |         0 | 30749
37 | 0x86ED282CEC75BE1E |    18.1 |     76 |            5478 |       982.2 |     5.1,    13.3 s |            0.0 | 1200 / 1915 |      0 |         0 | 0
38 | 0xDE0B2688923DC240 |    66.9 |     38 |            2873 |      1128.2 |     2.6,    64.6 s |            0.0 | 4611 / 4786 |      0 |         0 | 0
39 | 0x892C8D96DA2F46C4 |    30.9 |     25 |              58 |         6.7 |     0.0,         - |            0.0 |   492 / 492 |      0 |         0 | 0
```

- **14 of the 39 flights fell more than a second behind.** Flight 36, which is not on video, is the worst: 47.8 s behind.
  - It flew low along the south straw verge and crashed in it, and its player step ran at 2.0–2.5 ms.
  - It logged 2,868 `WorldBug` events between 39.3 and 42.2 s, all of cause 6, `FiberShapesFull`: a segment's query found 66–68 shapes even at the 0.1 m keep gap, over the 64 the buffer holds. Flights 35, 37 and 38 have none.
- **Nobody paused, and the log writer never made the flight thread wait.** Every one of the 39 flights has 0 pauses and 0 log waits.
- **The player's own cost in late passes sits at 0.84–1.25 ms (p50) in all but two flights (38 and 39).** So even in calm stretches the player's step uses most of its budget, a long way from .NET's figures (below).

## Flight 35 (video A)

Timeline from the log (`lagprobe timeline`, 5 s bins), against the .NET replay of the same steps (`lagprobe phases`, the final run):

| Flight s | Wall s | Late at end, ms | Player µs/step | .NET mean µs | .NET tether µs (its world queries) | Drone (x, y, z) m | Line |
|---|---|---|---|---|---|---|---|
| 0–120 | | ≤ 227 (passes recover) | | 112–386 | 31–126 | on the track start, rotors at rest | 16 nodes on the ground |
| 130 | 135.0 | 0 | | 247 | 109 (7) | (−169.7, 0.7, −48.4), the yard | 27 nodes, first touches on the gate |
| 135–150 | 140–155 | 0–2 | (kept up) | 317–377 | 220–260 (107–128) | hovering at the house, (−169, 0.4–1.3, −54) | 20–25 nodes held by `Z05_gate_plank` and `Z01_house_adobe` shapes |
| 155 | 160.4 | 394 | 1,265 | 543 | 405 (228) | leaving north, (−149.6, 0.4, −30.1) | line paid out from the house corner |
| 160 | 168.2 | 3,214 | 1,563 | 814 | 599 (430) | the crash at about 162.5 s, (−147, 0.3, 0.5) | still on the gate and the house |
| 165–235 | 175.6–244 | 5,590 → 25,598 | 1,403–1,436 | 470–755 | 380–570 (290–415) | crashed, armed, at (−147.3, 0.1, 1.2) | unchanged: 23–26 nodes held by `Shape`, 9 touching |

- **The bug at about 158 s wall is step 158,000, still on time.** The lateness passes 50 ms at 159.1 s wall and 1 s at 162 s. The crash at about 162.5 s came when the sim was already 1.2 s behind the sticks.
- **The line at step 200,000:**
  - Segments 0–18 lie on the gate, each 0.0–3.1 m long with 6–13 shapes within the margin, at 3–7 µs a query.
  - Segment 19 runs 21 m from the house corner (−167.2, 1.0, −53.6). Its reach is 10.7 × 19.6 m and holds 38–46 shapes, at 200–232 µs a query.
  - None of these segments holds a clearance certificate. Their sum, about 290 µs, is the 297 µs of tether queries the replay timed there.
- **The window ±10 s round 158 s holds:**
  - `FiberSlip` on `Z01_house_adobe` ×1,473 and on `Z05_gate_plank` ×241: the line sliding round the house and the gate in the Severe wind.
  - The crash's events: `PropStrike`, `BladeStrike`, `BladeDamaged`, `LegSet/Cracked/Broken`, `MotorBound`, `RotorJammed` and one `BladeLost`, all after 162 s.
- **After the crash** the windings heat from 72 °C to 300 °C and `MotorDamaged` (heat bands) follows. The pilot left the drone armed. This costs nothing measurable: the flight phase takes 30–57 µs.
- **The flight ended inside a catch-up pass** that had started 25.6 s late. The last ~20 s of flight time never reached the screen in real time.

## Flight 37 (video B)

| Flight s | Wall s | Late at end (max), ms | Player µs/step | .NET mean µs | .NET tether µs (queries) | Drone (x, y, z) m | Nodes over straw / held by |
|---|---|---|---|---|---|---|---|
| 7–10 | 8–11 | 0 | (kept up) | 197–287 | 94–145 (1) | climbing out east, 1.0 m up, 7–8 m/s | 0 |
| 11 | 12.0 | 0 | (kept up) | 271 | 142 (36) | (−136.5, 1.3, −17.2), over the south verge | 8 |
| 12 | 13.0 | 0 (5) | 861 | 415 | 266 (87) | (−132.1, 0.8, −15.6) | 21.9 / Stem 5.2 |
| 13 | 14.7 | 696 (603) | **1,710** | **1,056** | **853 (305)**, prepare 617 | (−129.5, 0.3, −14.7), touching the straw | 28.3 / Stem 24.6 |
| 14 | 15.9 | 931 (982) | 906 | 759 | 558 (294) | (−127.9, 0.9, −14.2) | 23.2 / Layer 4.8, Stem 2.7, Straw 2.6 |
| 15–16 | 16.6–17.2 | 650 → 230 | 574–589 (catching up) | 342–356 | 207–221 (59–95) | climbing to 3.2–3.7 m | 22–26 / ground, drone parts |
| 17 | 18.4 | 419 (601) | 1,613 | 1,146 | 758 (197) | the crash into the straw, between 17 and 18 s | 27.6 / Stem 9.1, Straw 7.0 |

- **The bug at about 12 s wall falls on steps 12,900–13,700.** Passes there grow geometrically, each about 1.3 times the last: 3, 4, 5, 7, 9, 11, 15, 19, 24, 32, 39, 53, 73, 140 ms. That is a step near 1.9 ms.
- **The line at step 13,500:** segments 3–9 and 18–30 are uncertified, 0.1–4.8 m long, with 7–26 S04 shapes within the margin each, at 6–30 µs a query. That sums to the 305 µs the replay timed.
- **The drone's hubs over the patch** add `SoilUnder` rays through the straw boxes: 62–64 µs.
- **The window's contacts:**
  - `FiberSlip` on stems ×446 and on straw ×106.
  - `FiberContact` on stems ×242 and on straw ×111.
  - The drone's own parts ×300.
  - All on `S04_patch`.
- **The lag eased once the line lifted** (15–16 s): the player's step fell below 1 ms and the backlog shrank from 931 to 230 ms. Back in the straw it grew again. The crash, between 17 and 18 s, came 0.4–0.6 s behind the sticks.

## Flight 38 (video C)

| Flight s | Wall s | Late at end (max), ms | Player µs/step | .NET mean µs | .NET tether µs (queries) | Drone (x, y, z) m |
|---|---|---|---|---|---|---|
| 40–63 | 41–64 | 0 | (kept up) | 183–342 | 87–190 (1–2) | flying the circuit, 1–6 m up, 9–24 m/s, north then east |
| 64 | 65.8 | 788 (631) | **2,070** (4.6–4.8 ms for the first ~170 steps) | **713** (max 12.2 ms) | **613 (444)**, prepare 513 | (140.9, 4.1, 64.9), 21 m/s, nothing touching |
| 65 | 67.1 | 1,106 (865) | 1,304 | 615 | 498 (334) | (150.1, 5.0, 50.3) |
| 66 | (flight ends at 66.94) | 1,128 | | 488 | 391 (255) | (161.3, 1.4, 38.7) |

- **The bug at about 63 s wall is step 64,404, 64.4 s wall.** The passes run 6 × 5 ms, then 29 × 28 ms, 137 × 137 ms, 632 × 631 ms, 866 × 865 ms and 1,129 × 1,128 ms.
- **The drone and its near line were clean.** No contact, no strike, motors sound, 5 m/s of wind. Its 31 near segments all held their certificates.
- **The cost was the laid end at the track start:**
  - At step 64,500, segment 0 runs 199 m from (−181.2, 0, −40.0) to (−54.4, 0, 113.9). That crosses the posadka's north verge where it begins, near x −140…−134, z 7…19.
  - Its reach is 128 × 155 m, with 61 shapes within the margin. It is uncertified, and one query cost 1.35 ms cold.
  - At step 66,000 the chain has re-spaced it. Segment 0 runs 76 m (−181, −40 → −133, 18.5), 61 shapes, 101 µs. Segment 1 runs 127 m from a node lying on the verge (`Layer S04_patch`), 38 µs.
- **Why that segment is so long:** the chain keeps 32 nodes and spaces them by `spacing_growth` 1.5 beyond the first metre, so with 400 m paid out the far segments are 50 to 200 m. The pilot flew far from the straw, but the line did not.
- **Logged contacts:** none in the window. A layer the line lies on is ground to the fiber, so taking it makes no `FiberContact` event; only the replay shows it.

## The cost, phase by phase (.NET)

From the final `lagprobe phases` runs. Means in µs per step; "q" is the world queries' share inside the phase.

| Stretch | Step | Wind | Flight (FC, rotors, aero) | Contacts (q) | Strikes | Tether (q) | of which prepare | solve | Scheduler's pose, signals, record |
|---|---|---|---|---|---|---|---|---|---|
| 35, on the ground, quiet (60–105 s) | 112–129 | 2–3 | 17–20 | 58–66 (17–19) | 1 | 31–37 (0) | 9–10 | 8–9 | 5–6 |
| 37, open flight (7–11 s) | 197–287 | 4–14 | 55–95 | 38–56 (8–16) | 2–5 | 94–145 (1–36) | 16–32 | 17–28 | 14–26 |
| 38, open flight (60–63 s) | 183–240 | 4–5 | 40–55 | 29–41 (7–8) | 2 | 106–132 (2) | 41–47 | 18–25 | 9–13 |
| 35, the yard hover (135–150 s) | 317–377 | 4–5 | 47–59 | 35–47 (8–12) | 3–7 | 220–260 (107–128) | 116–139 | 25–30 | 12–16 |
| 37, in the straw (13 s) | 1,056 | 15 | 83 | 94 (32) | 5 | 853 (305) | 617 | 46 | 26 |
| 38, the laid end on the verge (64 s) | 713 | 5 | 54 | 36 (7) | 2 | 613 (444) | 513 | 23 | 13 |
| 35, after the drone left (160–165 s) | 755–814 | 8–9 | 57–70 | 46–73 (9–25) | 13–17 | 571–599 (415–430) | 439–455 | 33–36 | 16–20 |

- **The tether's prepare is the lag.** In every lag stretch it grows by 300–600 µs, and its world queries are most of that.
  - `StaticContacts` takes 235–445 µs a step, at 87–137 calls.
  - In open flight it takes 7–16 µs, at the same 45 calls: there nearly every segment holds its certificate.
  - Everything else is flat across the flights.
- **The chain's own solve stays at 20–50 µs:** 32 nodes at most, never more.
- **PLT-04 is missed:**
  - in every lag stretch: world queries 255–444 µs a step, against "< 60 µs worst case", and a p99 of 1.7–4.2 ms, against ≤ 300 µs
  - already in open flight: p99 of 0.4–2 ms on this run

## Mono against .NET

The player's step and the replay's step at the same flight seconds:

| Stretch | Player (Mono), µs | .NET, µs | Factor |
|---|---|---|---|
| 37 at 13 s | 1,710 | 1,056 | 1.6 |
| 37 at 17 s | 1,613 | 1,146 | 1.4 |
| 38 at 65 s | 1,304 | 615 | 2.1 |
| 35 at 160–165 s | 1,403–1,563 | 755–814 | 1.9 |
| 35 at 170–200 s | 1,407–1,436 | 480–505 | 2.8–2.9 |
| 35 on the ground, 15–18 s (a burst of late passes) | 680–1,530 | 112–129 (quiet run) | 6–12 |

- **The player stepped about 2× slower than .NET** (1.4–2.9×) wherever its own passes measure it. The .NET replay is faster than the player, so read every .NET figure here as a lower bound.
- **The threshold:** the player kept up while the .NET step stayed under about 400 µs (35's yard hover, 37 at 12 s) and fell behind above about 450 µs.
- **The same factor makes open flight safe.** At .NET 180–290 µs the player's step is about 0.4–0.6 ms, 40–60 % of the budget.
- **One stretch does not fit.** On the ground at the start of flight 35, the player ran bursts at 0.7–1.5 ms a step for a state .NET steps in 0.12 ms. That is 6–12×, not 2×.
  - It is not the scheduler's own per-step work: pose, signals and record encode cost 5–26 µs in .NET.
  - It looks like the flight thread losing its core to other load. This laptop's .NET replay shows the same kind of stalls, single steps of up to 285 ms, in its noisy first minute.
  - It did not cause the three bugs, which all sit on a real cost rise, but the logs cannot name it. L4's in-player step timing has to measure it.

## The alternatives, tested

| Candidate | Evidence | Verdict |
|---|---|---|
| A render drop | The pilot's videos hold 40–48 fps. The feed's fields follow the sim (`FlightScene.Latch`), so a late sim looks like a dropping frame rate without one. | no |
| Input | The TX12's report interval before each flight: mean 4.0 ms, p99 4.2–4.3 ms, max 6.3–7.2 ms. Stick reports were dropped only once the sim was more than 4.1 s behind (35, 36), as a consequence. | no |
| Mono step cost in general | The open-flight step is about 0.4–0.6 ms in the player: no lag (flight 39 ran 31 s with a 6.7 ms worst). The margin is thin, though, and the unexplained idle bursts belong to L4. | contributes, not the trigger |
| The fiber's chain, or contacts elsewhere | Solve 20–50 µs; 32 nodes throughout; the drone's own contacts 29–105 µs, the top only while it touched the straw itself | no |
| The fiber's contact queries | +300–600 µs a step in .NET in each bug window, matched segment by segment (above) | **the cause** |
| Prop straw cuts | `BladeStrike`, `StoneStrike` and `PropStrike` appear only at the crashes, after each lag began; strikes cost 2–17 µs | no |
| Motor health and dirt (D-083) | Dirt 1.16 g only after crashes; heat only after flight 35's crash; flight phase 17–116 µs | no |
| Severe wind | The flight thread's wind share is 2–15 µs; the worker's batch runs beside it; the wind is the same in every flight, and the lag follows the line, not the wind | no |
| The log writer | 0 log waits (`LogFull`) in all 39 flights; the record's encode is inside the 5–43 µs scheduler column | no |

## Classification

| Flight | Class | Cause | Straw? |
|---|---|---|---|
| 35 (A) | the sim behind real time | the fiber's per-step queries on the uncertified segments wrapped round the yard's gate and house, worst a 21 m segment from the house corner; Mono step 1.3–1.6 ms; no shedding, so 25.6 s behind | no |
| 37 (B) | the sim behind real time | the fiber's queries on 20+ short segments lying in S04's straw mat and stems, plus the hubs' soil rays over the patch; Mono step 1.7 ms | yes |
| 38 (C) | the sim behind real time | the fiber's query on the laid end's 76–199 m segment lying across the north verge's corner at the start; Mono step 4.7 ms, then 1.3 ms | yes, at the far end of the line |

**Requirements:**
- **PLT-04 is missed:** the world queries take 4–7× their 60 µs worst case in every lag stretch. A late step is logged as a hitch and caught up as specified, but with no bound on the catch-up.
- **INP-04 holds as written:** stick reading never depends on the render. But the end-to-end stick latency grows with the backlog.
- **LOG-02 is met:** all three logs replay bit for bit, through this report's timed core as well.

## Method

**The tool: `tools/lagprobe` (new, tools only, no runtime change).** It reads the logs with the sim core's own reader. It has four modes:
- `hitches`: one line per flight
- `timeline`: per bin of flight time, the lateness, the player's cost, the drone, the line and the events
- `window`: the state each second, the events and the line's contacts by object, within ±S s of a wall time
- `phases`: the timed replay

**How the timed replay is built:**
- The probe compiles the runtime's sources itself. It replaces three files with timed copies that `timed_sources.py` generates at build time and never commits: `FlightCore`, `FiberLine` and `FlightWorld`.
  - The copies are the runtime's own text with clock marks inserted at fixed anchors.
  - Each anchor must occur exactly once, or the build fails.
- The replay checks every step record bit for bit against the log, so the timings belong to the pilot's flight: 18,147, 66,941 and 234,901 records, all equal.
- `--dump` prints the line's segments at chosen steps. For each: where it lies, its certificate, what its node holds, and its own `ShapeHits` query, re-timed between steps. A query changes no state; the bit-exact check after the dump steps confirms it.

**The wall time of a step:** its due time, plus the pauses before it, plus the lateness interpolated across the passes. The bug times come from file times (±2 s); each mapped to steps inside a lag onset: 158.5 s (A), 12.9–13.7 s (B), 64.4 s (C).

**Conditions:**
- On AC power (battery status 2 before and after), with the Balanced scheme, a Ryzen 7 7735HS at 3,201 of 3,201 MHz. vsync does not apply: headless, no render.
- Other sessions were running on the machine, a test host among them. So .NET p99 and max figures are noisy; the means are repeatable.
- A second run of flight 35 put the lag stretch at 570–760 µs against the final run's 470–814 µs, and its yard hover at 440–530 µs against 317–377 µs. Both are above the player's keep-up threshold in the lag stretch.
- The player cost needs no machine of mine: it is computed from the pilot's own logged hitches.

No video frames were needed: the logs decided every case.

## Commands run (worktree `.claude/worktrees/lag-logs`, branch `unity/qa/lag-logs` from `origin/unity/physics/physics-integration-2` at `42dbbcda`)

```
git lfs checkout
dotnet build -c Release tools/replay              # Build succeeded, 0 warnings, 0 errors
dotnet build -c Release tools/lagprobe            # Build succeeded (timed_sources.py ran: 19 marks, 9 timed world calls)
F=%LOCALAPPDATA%\450fpv\flights

tools/replay/bin/Release/net10.0/replay.exe  $F/flight-86ed282cec75be1e-1.flightlog
  match: 18147 steps and 1892 events, bit for bit, and 102 loose-piece records
  note: recorded on runtime Mono 6.13.0 (Visual Studio built mono) X64, replayed on .NET 10.0.9 X64
  replayed in 11.02 s on .NET 10.0.9 X64; exit 0 (Match)

replay.exe --summary  (flights 35, 37, 38)
  35: 234901 step records; complete (234901 steps, 34387 events, 3288 hitches, 0 log waits, 18070 stick reports dropped, 0 events lost)
  37:  18147 step records; complete ( 18147 steps,  1968 events,   76 hitches, 0 log waits,     0 stick reports dropped, 0 events lost)
  38:  66941 step records; complete ( 66941 steps,  3746 events,   38 hitches, 0 log waits,     0 stick reports dropped, 0 events lost)

lagprobe hitches <the 39 logs Player.log lists>            # the table above
lagprobe timeline <35> --bin 5 ; <37> --bin 1 ; <38> --bin 1 ; <36> --bin 5
lagprobe window <35> --wall 158 --span 10 ; <37> --wall 12 ; <38> --wall 63 ; <36> --wall 45 --span 6
  35: FiberSlip Shape on Z01_house_adobe x1473; FiberSlip Shape on Z05_gate_plank x241; ...
  37: FiberSlip Stem on S04_patch x446; FiberContact Part on the drone x259; FiberContact Stem on S04_patch x242; FiberContact Straw x111; FiberSlip Straw x106
  38: (no contacts or slips in the window)
  late passes, 38: 64404:6x5ms 64410:29x28ms 64439:137x137ms 64576:632x631ms 65208:866x865ms 66074:1129x1128ms
lagprobe phases <37> --bin 1 ; <38> --bin 1 --from 34 ; <35> --bin 5    # the final runs, sequential, nothing else heavy running
  checked 18147 / 66941 / 234901 step records bit for bit against the log: all equal
  35: world calls on the flight thread over the flight: Static 24475001 calls, 126.6-151.2 us/step; Geometry 6488664 calls, 1.0-1.6 us/step; Ground 978512 calls
lagprobe phases <38> --bin 1 --from 62 --dump 63,64.5,66
  step 64500, seg 0: (-181.2, 0.00, -40.0) 199.4 m, reach 127.8 x 154.9 m, not certified, 61 shapes, 1354.9 us
  step 66000, seg 0: 75.9 m, 61 shapes, 100.9 us; seg 1: 126.6 m, holds Layer S04_patch, 3 shapes, 38.4 us
lagprobe phases <37> --bin 1 --from 11 --dump 11.5,13.5,14.5
  step 13500: segments 3-9 and 18-30 not certified, 7-26 shapes each, 5.7-30.4 us a query
lagprobe phases <35> --bin 5 --from 125 --dump 145,158,165,200
  step 200000: segments 0-18 on Z05_gate_plank, 6-13 shapes, 2.6-7.4 us; seg 19 from the house corner 21.0 m, 38 shapes, 201.6 us
```

## For the next units

- **L4 (the step time inside the player):**
  - the idle bursts on the ground at the start of flight 35 (6–12× .NET)
  - the steady 0.85–1.25 ms p50 of late passes across the session
- **L5 (the lag cap):** without it, any stretch over 1 ms becomes a growing delay. The session reached 25.6 and 47.8 s behind, with 18,070 and 30,749 stick reports refused.
- **L6, folded into W3:** the cost path is `ShapeHits`'s `StaticContacts` on long or resting segments.
  - Each map item's tree is walked against the segment's bounding box, not its capsule (`MapWorld.SweepItem`). A 100–200 m diagonal segment's box takes in everything for a hundred metres around.
  - A segment resting on any shape loses its certificate and is queried every step.
  - The farmland's 1,650 trees and bushes, the village and its debris will meet both.
