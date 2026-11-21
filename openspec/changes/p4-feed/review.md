# Review: p4-feed 1.12 (the OSD through the analog channel)

**Reviewer:** QA Engineer · **Branch:** `unity/feed/physics-integration-2--osd-channel` at ccf26748 · **Commits reviewed:** ef10589b, 393d93e7, 53af572d, ccf26748 (on 88acbec1) · **Requirements:** OSD-01, OSD-02, OSD-04, FEED-00, FEED-04, FEED-10, REAL-03 · **The pilot, 2026-11-17 (D-117)**

The branch was merged raw into the pilot's build branch at the pilot's word while this review ran (6cbb6c9d, inside 3c318e09). This review covers the branch itself, and its findings apply there too.

## Verdict: **approve**

The picture did not move. Outside the pixels the pass's own OSD window reaches, the output of the base and of this branch is the same bit for bit, in every one of 208 untorn fields over six ranges. Two of those ranges are mine: a top tear into a blue dropout (fields 3565–3612) and a hard cut's bottom tear across the dense bottom rows (603–612). In the 11 torn fields, every changed pixel lies within the torn OSD's reach. With `-feedGodot`, the old OSD comes back bit for bit in all 208 untorn fields. The 11 torn fields differ by the tear's black wedge alone: every differing pixel is black, in the wedge's columns and the tear band.

The OSD now goes through the channel the way the clips show it:
- **Horizontal edges:** 3.48 px on one-texel strokes and 4.82 px on wide ones, against 3.25–3.88 and 3.37–4.84 in the clips.
- **Thin strokes:** they peak at 0.77 of the flat ink.
- **The white:** sits on the receiver's clip, 254.9 against the clips' 252–255.
- **The ink's noise:** 0.52, against 0.39–0.63.
- **Fringing:** 6.4, against 2.4–8.8.

Each stage the pass applies to the characters sits after the insertion on the signal's path, and each camera stage the pass used to apply to them is now taken off. On the sheets the change reads as a clear step toward the clips: the hard pixel-art sides are gone and the ink is the clips' white. Nothing on the sheets is dimmer than the clips or more coloured than them. The sim's soft sides sit at the soft end of the clips' range (F4). The review player builds, and its smoke test passes with exit 0. The cost (FEED-10) is the one check still owed: its run waits for the Unity lock.

Nothing blocks the merge. Four findings should be fixed soon. Two of them are what still makes the OSD read as laid over, next to the clips:
- **F1:** by day the outline is 26–39 levels darker than the clips'.
- **F2:** the top and bottom edges are still one-line steps.

Neither was this task's to fix without moving the picture or the ROM. The other two:
- **F3:** the battery mark's empty part is see-through, where every letter shows it black.
- **F4:** the kernel lands softer than its own fit.

The rest are notes.

## Scenarios

My runs, all at ccf26748 in this worktree, with the base 88acbec1's `Feed.shader` swapped in for one capture only (below):
- **Captures:** three `FeedCapture.Stills` runs over the same six ranges: the base, this branch, and this branch with `-feedGodot`. The ranges are the tech artist's four (600-700, 260-275, 1360-1372, 1405-1435) and two of mine (3565-3612, and 603-612+hardcut), 219 fields each.
- **The measures:** the tech artist's frame-metrics measure, re-run from a copy of the scripts on letters B, D, L and P (read in place, output under `reference/_compare/osd-qa/`) and on my captures.
- **Tests:** EditMode, filtered to the three assemblies.
- **Players:** the review player built, and its smoke test.
- **The cost:** a development test-range player built, and `-feedcost`.

The commands and their output are at the end.

| Spec · scenario | Result | Evidence |
|---|---|---|
| osd · The OSD goes through the channel with the picture | **pass** | Edges 3.48 px thin, 4.82 px wide (mean profile 5.11), against 3.3–3.9 and 3.2–5.3. Thin strokes peak at 0.771 of the flat ink, against 0.75–0.87. The flat ink is 254.9, against 252–255. The brightest ink (the 99th percentile in a cell, fields 600, 650, 664 and 700) is 254.0–255.0 in three corners and at the centre; the base gave 203–243 by position. The top-right cells hold only one-texel strokes, which peak at 196 (0.77, as above). Masked A/B: 0 px differ outside the OSD's window in 208 of 208 untorn fields. The 11 torn fields differ only within the torn OSD's reach (below). |
| osd · The grid matches the Godot OSD | **pass** (by the switch) | `-feedGodot` gives the base's frames bit for bit, OSD included, in all 208 untorn fields. Cells, strings and ROM are untouched (no change under `Core/`); EditMode `FeedOsdTests` pass. |
| analog-feed · the still and schedule A/B against Godot (FEED-00) | **pass, unchanged** | The default's picture outside the OSD window equals the base's, and `-feedGodot` equals the base's frames (the wedge apart), so 1.2's and 1.4's A/B against Godot still stands. No core file changed; `dotnet test tools/feedtests` 85 of 85. |
| analog-feed · the cost (FEED-10) | **not yet run by QA** | The development test-range player is built at ccf26748; its `-feedcost` waits for the Unity lock, which the pilot's build holds. The tech artist's numbers (not taken as evidence here): 0.651 and 0.642 ms against the base's 0.620 and 0.584, within 1.0 ms. |

**Requirement IDs:**
- **OSD-04: met as the pilot redirected it** (D-117). The OSD goes in before the analog stages, with no lens. It tears with N8, and the black dropout takes it. It is band-limited into the chroma detector (fringing 6.4, against the clips' 2.4–8.8). It is no longer crisp, by the pilot's word. The text of `docs/requirements.md` OSD-04 ("the OSD stays crisp") and K§11.6's two decoder facts are stale; the as-built leaves them to the orchestrator.
- **OSD-01: partly met.** The grid, cells and strings are unchanged. "The font matches the footage" misses on the battery mark's empty part (F3). This miss predates 1.12.
- **OSD-02: met:** "Svinoriz 10 Opto" and "! SAFE !". Every value is the stub's; no value from the footage's OSD is in any file.
- **FEED-00: met:** Godot's OSD is one switch away, bit for bit.
- **FEED-04: met** for the traits this task touches.
- **FEED-10: not yet checked by QA** (the cost run waits for the lock).
- **REAL-03: met:** the side-by-side sheets against B, C, D, L and P, local only (below).

## 1. The picture did not move

**Before is the base.** The base capture came from 88acbec1's `Feed.shader`, byte for byte (`git show`). It was copied over this branch's shader for that one Unity run, and the branch's own file was put back in the same command: its SHA-256 equals HEAD's blob's, and `git status` is clean. The log shows Unity reimporting the shader for that run.
- My base frames equal the tech artist's `before/` frames: 161 of 161 bit for bit.
- My branch frames equal their `after/`: 161 of 161.

So their captures were the base and the branch, and they are reproducible.

**The mask is computed, not grown by hand.** For each screen pixel, `qa_ab.py` (my own) evaluates `Feed.shader`'s `has_osd`:
- the output pass's bob takes the screen row to its field line and `sy`
- `cx_lo` and `cx_hi` at ±0.2 cell come from the column
- the codes come from the capture log's OSD layout line

That is 14.38 % of the frame. In a torn field, the read moves by at most `amp × 60 px × tear_band(sy)`. The torn allowance is `has_osd` anywhere within that reach on each line.

| Pair | Untorn fields | Outside the window | Torn fields |
|---|---|---|---|
| base · branch | 208 | **0 px in 208 of 208** | 11: every changed pixel lies within the torn OSD's reach: 1367 (121 px), 3572 (34), 3573 (32), 603 (256), 604 (373), 605 (71), 606 (204), 607 (740). 1365, 1366 and 1368 change only inside the window. |
| base · `-feedGodot` | 208, **all bit for bit, OSD included** | 0 | 11: every one of the 4 472–10 266 differing pixels per field is black in Godot's frame, right of 1 − 0.1·amp and in the tear band: the wedge alone. |

**The exception (the torn OSD beyond its cells) is right.** N8's tear is the receiver's time-base error, so it moves the read of the whole line, the characters with it (`px`, before the insertion's taps). Where the torn glyph lands, the new OSD differs from the old OSD. In every torn field, nothing beyond the torn OSD's reach changed, so no picture pixel moved.
- The as-built calls the field "1365"; it is **1367** (N1).
- My bottom-tear range shows the exception at its largest, up to 740 px: the hard cut's 0.95 tear runs through rows 11–13.

Inside the window the base and the branch differ by up to 113–155 levels: the new OSD.

## 2. The OSD against the clips

### The measures

My re-run of the tech artist's measure reproduces their footage numbers to the third decimal on B, D and L, and on P within 0.002 px (380 of P's first 390 frames carry the OSD in my run, 390 in theirs). The script was copied, and only its output folder was changed. My captures reproduce their sim numbers. The measure tells the old OSD from the clips: the base's Godot OSD gives 0.83 px edges and a thin peak of 1.04, against the clips' 3.3–3.9 px and 0.75–0.87. A target that a known-wrong model also passed would prove nothing; this one fails the old OSD.

| Source | thin 10–90 px | wide 10–90 px | mean profile px | overshoot | vertical rows | flat ink | outline min | thin peak | ink noise | fringing (K§11) | ink chroma | edge wobble px | line to line px | scene |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| B (mine) | 3.267 | 4.266 | 4.699 | −0.052 | 2.827 | 253.8 | 92.2 | 0.754 | 0.553 | 6.90 | 1.90 | 0.774 | 0.248 | 162 |
| D (mine) | 3.370 | 4.014 | 4.109 | −0.053 | 2.648 | 253.4 | 71.1 | 0.835 | 0.517 | 5.58 | 1.39 | 0.771 | 0.364 | 156 |
| L, night (mine) | 3.786 | 4.840 | 4.874 | −0.027 | 2.781 | 254.7 | 4.5 | 0.830 | 0.387 | 2.44 | 1.06 | 0.642 | 0.300 | 21 |
| P, N13 (mine) | 3.874 | 3.819 | 3.705 | −0.038 | 3.268 | 231.4 | 3.9 | 1.034 | 0.619 | 6.66 | 1.90 | 0.517 | 0.135 | 143 |
| A–H, L, P (the tech artist's, not re-run) | 3.25–3.88 | 3.37–4.84 | 3.18–5.31 | ≤ 0 | 2.53–5.17 | 252.4–254.7 (P 231) | 3.9–92.8 | 0.754–0.873 (P 1.04) | 0.39–0.63 | 2.44–8.84 | 1.06–2.11 | 0.50–1.00 | 0.13–0.46 | 21–233 |
| **the base** (Godot OSD), 600–700 | 0.828 | 0.845 | 0.854 | −0.009 | 0.805 | 205.6 | 15.8 | 1.037 | 1.285 | 5.63 | 2.77 | 0.401 | 0.399 | 135 |
| **the branch**, 600–700 | **3.483** | **4.824** | **5.109** | −0.006 | 0.804 | **254.9** | 52.4 | **0.771** | **0.521** | **6.37** | 0.51 | 0.233 | 0.178 | 135 |

How the branch reads against the clips:
- **Edges.** The thin strokes' 3.48 px sits mid-range. The wide strokes' 4.82 px sits at the clips' softest (L 4.84), and the thin peak's 0.771 sits at the clips' lowest (B 0.754). This is F4.
- **White.** On the clip, as in every clip with flat ink.
- **The ink's noise.** In range.
- **Colour.** The ink's own chroma, 0.5, is below the clips' 1.06–1.90. The fringing (6.4) is mid-range.
- **Outline.** Still darker than the clips' by day (F1).
- **Vertical edges and wobble.** One-line vertical transitions (0.80 rows against the clips' 2.65–3.27; F2), and less edge wobble (0.23 px against 0.52–0.77; N9).

**The outline against the scene beside it.** `outline.py`, re-run, gives the outline's minimum by the scene level beyond it, as p50:
- **B:** 84 at a scene of 80–120 and 94 at 120–160. The fit is 62.1 + 0.209·scene.
- **D:** 75 and 81. The fit is 43.2 + 0.252·scene.
- **L, at night:** 3 at a scene under 40. The fit is 5.0 + 0.189·scene.
- **The branch:** 3.9 + 0.312·scene; its p50 is 55 at 120–160, where B's is 94 and D's 81.

### What I saw on the sheets

I looked at the tech artist's two sheets row by row at 4×. I also looked at my own pair, `reference/_compare/osd-qa/sheets/qa-left.png` and `qa-middle.png`. They are 1:1 crops (shown at 2×, nearest) of frames I chose (B 60, C 60, D 60, L 60, P 200) beside my captures:
- the base and the branch at 664
- the branch in the deep drop (1422)
- the branch at the bottom tear (605) and the top tear (3573)

All are local only.

**Better than before, plainly:**
- The Godot OSD's pixel-art sides are gone; each stroke's left and right edges now melt over 3–5 px as the clips' do.
- The ink is the clips' white, the same in the corner as at the centre. Before, the bottom-left battery mark was visibly greyer than the name.
- The thin strokes of the "4" and the "1" read a little greyer than the wide ones, as in C, D and L.
- The fringing is a faint lilac and green on the strokes' sides at 4×, invisible at 1:1, as in the clips.
- Torn, the characters shear with the picture's lines, white blocks and all.
- In the deep drop the ink greys a little with the picture (N2).

**Still unlike the clips, in the order a pilot would notice:**
1. **The outline (F1).** Every sim glyph carries a solid black ring about one texel wide all round. It makes the digits and the name look bolder, and a size larger, than in B, C and D, whose dark edge is faint and partly lost in the scene. The ink itself, about 24 px wide, is the same size in both. At night (L) and in P the clips' ring is as dark as the sim's, which is the tech artist's "it follows the light".
2. **The tops and bottoms (F2).** In the sim, the glyphs' horizontal strokes and the outline's horizontal bars start and stop on one field line, a hard step at full contrast. In every clip the tops and bottoms are rounded over about one field line. Soft sides with hard tops read as "scanline text", the last trace of a digital overlay at 4×. At 1:1 it is subtler, but still visible on the name's horizontal strokes.
3. **The battery mark (F3).** In all five letters, the empty top of both battery marks is filled black under a white terminal bump. In the sim it is see-through: the scene shows inside a black ring.

**Nothing is softer than the clips overall, since the clips are soft in both directions.** Nothing is dimmer by day, and nothing is more coloured.

The test scene's flat grey sky behind the OSD exaggerates how "clean" the sim's OSD looks next to the clips' busy fields. The review smoke test's `18_air_house` (the flight over House at 16 m, FPV) shows the OSD over ground, a track and a tree belt. Read at 1:1 and 2×, the same three differences stand out, and N3's dark lines run across the characters as across the picture.

## 3. Each stage in its place

`feed_field`, read in order, against K§11.1's path:

> scene → lens → camera (AE, AWB, sharpening) → composite → **the OSD's insertion** → fiber → receiver (sync, decode, bob, scale) → recorder

| Pass stage (in order) | On the OSD? | Where it belongs | Verdict |
|---|---|---|---|
| The camera's read: rolling shutter and exposure (`shutter_row`) | no | camera | right |
| P1, field sampling (`line_`, `sy`, parity) | yes | the field structure of the whole signal | right |
| N8, tear (`px`) | yes, the OSD's taps read at `px` | receiver | right |
| O1 lens, apertures, the 920-sample read, the 17 taps | no (the ROM is read directly) | lens and camera | right |
| P3, sharpening | no (the OSD is inserted on `sharp[]`) | camera | right |
| **1.12: the OSD's insertion, with the channel's kernel on the luma** | — | the chip, then the fiber and the decoder | right |
| The PAL encode, with the delay line's chroma masks | yes | the camera's composite. The chip's switch replaces it under a glyph; a neutral glyph has u = v = 0, so the result is the same (N4) | right in effect |
| N1, grain | yes | channel by K§11.7. Its gain term is the camera's (N3) | right today |
| N2, impulses; N3; N4 | yes | after the insertion by K§11.7 | right |
| Decode: chroma trap, U/V detection | yes | receiver | right |
| C3, saturation | yes (the fringing's amplitude) | camera | minor (N3) |
| The quality drop's gain, black and grain | yes | design 9 calls it the camera's; design 11 puts it after the insertion | inconsistent (N2) |
| The tell's brightening | yes | link and receiver | right |
| Lift and contrast | inverted for the OSD's levels (`osd_w`, `osd_b`) | camera | right (wording, N4) |
| Banding | yes (±0.006, mostly clipped at the white) | camera | negligible |
| O2, vignette | **no** (1.12 divides it out of the inserted levels) | lens | **fixed** |
| P7, highlight cast | **only the scene's share** (1.12) | camera | **fixed** |
| Loss states: the black dropout takes the OSD; blue and snow replace it | yes | receiver | right |
| The receiver's own text on blue and snow | its own glyphs | receiver | right |
| P8 border; bob deinterlace (output pass) | yes | receiver | right |
| **Missing:** the receiver's vertical scaler; the time-base wobble; the outline's level by day | no, on neither | receiver or recorder | open (F2, N9, F1) |

## 4. The gaps left open, and the battery mark

- **Vertical edges** (the sim 0.80 rows; the clips 2.65–3.27 in my re-run, 2.53–3.69 in the tech artist's but E's 5.17). **Leaving it open in 1.12 is right.** The scaler is the receiver's and acts on the whole frame, so adding it to the OSD alone would make the OSD a separate layer again, and adding it to both moves the picture the pilot called great. **It does still make the OSD read as laid over** (sheets, item 2). Fix soon, as its own change: F2.
- **The outline's level by daylight** (the sim 55 at a scene of 120–160; B 94 and D 81 there). **Leaving it to "1.7's AE and N13" is not right as written.** K§11.7 puts AE in the camera, before the insertion, so the camera's AE cannot lift a black that the chip inserts after it, and N13 acts only in dim light. Whatever lifts the outline by day sits after the insertion, or is in the chip's own levels. It is the largest remaining difference on the sheets: F1.
- **The edge wobble** (the sim 0.23 px per edge frame to frame; the clips 0.52–0.77 in my re-run). **Leaving it open is right:** it is a time-base trait of the whole line, and it would move the picture. At 1:1, on stills, it is no laid-over cue; in motion it might be (N9).
- **The battery mark's hollow part** (see-through in the sim, black in B, C, D, L and P). **Leaving it out of 1.12 is right:** it is the ROM's glyph (`FeedFont`), not the channel. **It does read as a different font** on the most prominent glyph in the corner: F3.

## Findings

### Blocks the merge
None.

### Fix soon
- **F1: by day the outline is 26–39 levels darker than the clips'.**
  - **Measured:** the outline minimum's p50 beside a scene of 120–160 is 55 for the branch, against 94 for B and 81 for D (at 80–120: B 84, D 75). At night L's is 3 beside a scene under 40, where the branch's would be 15.
  - **Seen:** on the sheets the solid ring makes the sim's text bolder and larger than B's, C's and D's.
  - **The fix:** record it as an open OSD item with its target (by day 75–94 beside 80–160; at night near black), and find a cause after the insertion. It can't be 1.7's camera AE. Owner: tech-artist.
- **F2: the OSD's tops and bottoms are one-line steps** (0.80 rows; the clips 2.53–3.69, E 5.17).
  - **The conflict:** K§11.4 P1 says "a near-nearest vertical filter", yet the clips' OSD shows about one field line of interpolation (0.8 × 3.76 rows ≈ 3.0).
  - **The fix:** first measure the picture's own horizontal edges in the clips, to confirm it is a whole-frame trait. Then make it its own change: an A/B of the picture, and the pilot's look at the gate, since it moves the picture. Owner: tech-artist.
- **F3: the battery mark's empty part is black in every letter** (B, C, D, L, P) and see-through in the sim. It is `FeedFont`'s glyph, which predates 1.12 (OSD-01, "the font matches the footage"). Owner: tech-artist.
- **F4: the kernel lands softer than its own fit.**
  - **Fit against pass:** σ = 2.0 was fitted on a model of the pass (wide 4.10, thin 3.27, peak 0.82). The pass itself measures 4.82, 3.48 and 0.77: at the clips' softest for wide strokes (L 4.84) and their lowest thin peak (B 0.754).
  - **What it means:** the scenario passes, but the OSD sits at the soft edge of the clips' range rather than at its middle (wide about 4.2, peak about 0.83).
  - **The fix:** re-fit σ on the pass's own captures.

### Notes
- **N1:** the as-built's torn exception is field 1367, not 1365 (121 px, the same count).
- **N2: the quality drop's place.**
  - **What happens:** the drop dims the OSD and adds its grain. The branch's ink is 255 at rest and 231.6 at field 1424 (the tech artist's `levels-after`).
  - **The as-built's "×0.982":** that ratio is relative to field 1405, already at degrade 0.63, so it understates the dimming.
  - **The conflict:** design 9 calls the drops "the camera's own excursions" (before the insertion), and design 11 lists them after it. The pilot's "all of this has to be applied to the OSD too" fits either reading, and the clips show no drop to measure.
  - **The fix:** pick one, and write it in both decisions.
- **N3: N1's grain scales with the camera's gain** (`GRAIN · _Gain`), and the drop's term also reaches the characters.
  - The clips' ink noise does not grow at night, though the scene's grain does: L 0.39, against B 0.55 and D 0.52.
  - It is harmless while the gain is the stub's 1. Keep it in mind when 1.7 drives the gain.
  - C3's gain term on the OSD's fringing is the same kind of thing.
- **N4: design 11's wording.**
  - Its stage list puts "the PAL encode" and "the lift, contrast and banding" after the insertion.
  - The pass treats lift and contrast as the camera's: it inverts them for the inserted levels.
  - K§11.1 has the camera's composite before the insert.
  - The result is right; the wording is not.
- **N5: the scenario's ranges are rounded tighter than the clips.** "3.3–3.9" excludes B (3.27) and F (3.25), and "0.75–0.87" excludes F (0.873). Use the clips' own numbers at the archive.
- **N6: no automated guard for "the picture keeps its bits".** The claim rests on captures. A capture check would keep it from regressing: the default against `-feedGodot`, outside `has_osd`, on a fixed range with a tear (QA can add it; `qa_ab.py` is the method).
- **N7: `tools/feedbench` does not build on this branch, nor on its base.** This is confirmed, and it is not this change's doing; no file under `tools/` or `unity/Packages/` differs between 88acbec1 and ccf26748.
  - **The cause:** `feedbench.csproj` (tech-artist, 2431b5ed) links four files of the sim's `Tests/`. `TetherScenarios.cs` has since come to need `GoldenFlights`, `FiberCutScenarios` and `AllocProbe` (physics-engineer, 1d7e7279, aacb6086, 4bad45a6). The two lines first met in the orchestrator's merge 5d01373a.
  - **Adding the three files is not enough:** they pull in `RangeScenarios`, `SoilScenarios` and `ElementWorld` (a scratch project with the three links fails on those).
  - **The fix:** feedbench should compile `Tests/**/*.cs` as `tools/simtests` does. The csproj is the tech artist's to fix.
- **N8:** `docs/requirements.md` OSD-04 and K§11.6 still say the OSD stays crisp (left to the orchestrator in the as-built).
- **N9: the edge wobble** (0.23 px against the clips' 0.52–0.77) stays open with the picture's time base; watch for it at the gate in motion.
- **N10: the review sheets at 1.5 / 10 / 50 / 200 m do not apply.** This is a feed change, with no asset; the OSD sheets above are its review.

## Hygiene

- **Commits:** four, each subject-only, 59–67 characters, by `Tech Artist <techart@450fpv.local>` as author and committer, with no body and no trailer.
- **Files:** only `openspec/changes/p4-feed/`, `Scripts/Feed/` (five files), `Shaders/Feed.shader` and one test. Nothing from `reference/`, `Library/`, `Temp/`, `Logs/` or `UserSettings/`.
- **OPSEC:** `precommit.py --tree HEAD` finds nothing. The strings are the project's own, and no footage value or file name appears.
- **Licences:** none touched.
- **Spec:** `openspec validate p4-feed --strict` is valid.

## Commands and output

The worktree is `C:\.dev\450fpv\.claude\worktrees\w9-levels` at ccf26748. Every Unity and player run below held the session's Unity lock for that run alone. The machine is on AC, Balanced plan; `nvidia-smi` printed the GPU's enforced limit with each player run (45 W at 21:50, 20 W during the smoke test).

```text
# Before anything: the lock, the power, the GPU
> unity-lock.ps1 -Status; BatteryStatus.PowerOnline; powercfg /getactivescheme; nvidia-smi --query-gpu=name,enforced.power.limit,...
free; Unity or the game running: False
PowerOnline True, Charging False
Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
NVIDIA GeForce RTX 4050 Laptop GPU, 45.00 W, [N/A], 3105 MHz

# The feed's .NET tests
> dotnet test tools/feedtests
Passed!  - Failed:     0, Passed:    85, Skipped:     0, Total:    85, Duration: 1 s - Fpv450.Feed.Tests.dll (net10.0)

# feedbench (N7)
> dotnet build -c Release tools/feedbench
Build FAILED.
unity\Packages\com.fpv450.sim\Tests\TetherScenarios.cs(41,66): error CS0103: The name 'GoldenFlights' does not exist in the current context
  … (53,32) GoldenFlights; (55,54), (56,52), (343,28) 'FiberCutScenarios'; (467,31), (469,46) 'AllocProbe'
> git diff --stat 88acbec1..HEAD -- tools/ unity/Packages        → (nothing: this change touches neither)
> git log -S<name> --reverse -- …/Tests/TetherScenarios.cs
GoldenFlights: 1d7e7279 Physics Engineer 2026-11-05 test(physics): re-derive the tests and goldens on the canister, audited
FiberCutScenarios: aacb6086 Physics Engineer 2026-11-13 test(physics): the capstan lip known-wrong, the golden's audit, probes
AllocProbe: 4bad45a6 Physics Engineer 2026-11-13 test(physics): a lying line's hold by hand and the trail in the wash
> git log -- tools/feedbench/feedbench.csproj      → 2431b5ed Tech Artist 2026-11-05 feat(feedbench): run the physics' link through the receiver per field
> first commit holding both 2431b5ed and 1d7e7279 → 5d01373a Orchestrator 2026-11-05 feat(physics): merge d01's canister onto the line, model 64
> a scratch csproj (outside the repo) = feedbench + GoldenFlights.cs + FiberCutScenarios.cs + AllocProbe.cs
Build FAILED: 'RangeScenarios' (FiberCutScenarios.cs 474, GoldenFlights.cs 2378…), 'SoilScenarios' (GoldenFlights.cs 2248), 'ElementWorld' (FiberCutScenarios.cs 1172)
> grep Compile tools/simtests/*.csproj  → <Compile Include="../../unity/Packages/com.fpv450.sim/Tests/**/*.cs" />

# My schedule dump (my own 4-line console over tools/feedcore), to choose my ranges
> dotnet run -c Release -- 1 4000   (seeds 1-3)
seed 1: 266 tell → 270 Blue; 1365-1368 top tear 0.636 → 1369 Black; 1984 tell → 1986 Blue; 3572-3573 top tear → 3574 Blue; 3842 tell → 3846 Blue

# Hold 1: the branch
> capture.ps1 -Name after   (Unity -batchmode -projectPath unity -executeMethod Fpv450.Feed.FeedCapture.Stills
    -feedField 600-700,260-275,1360-1372,1405-1435,3565-3612,603-612+hardcut -feedSeed 1 -feedOut qa-osd\after -logFile … | Out-Host)
Unity exit 0 after 90 s; PNG files: 219
[Feed] FPV camera: 2754x1549 render, … no tear wedge (D-065), the OSD through the channel (p4-feed 1.12). …
# Hold 2: the branch with -feedGodot
Unity exit 0 after 42 s; PNG files: 219
[Feed] FPV camera: … Godot's tear wedge (the A/B, D-026), Godot's OSD (the A/B). …
# Hold 3: the base's shader, for this run only
> git show 88acbec1:unity/Assets/_Project/Shaders/Feed.shader > Feed.shader.base  (byte for byte), copied over the branch's, run, copied back
swapped in the base shader: C5B179E7…918BF5 (HEAD's F1247000…CED617)
 unity/Assets/_Project/Shaders/Feed.shader | 87 ++-----------------------------
Start importing Assets/_Project/Shaders/Feed.shader using Guid(298c2d176c7cae849b0b43264a8d21ff) (ShaderImporter)
Unity exit 0 after 52 s
restored HEAD's shader: F1247000…CED617, same as before: True
(git status: clean)
PNG files: 219

# The masked A/B (qa_ab.py, my own: has_osd per pixel from the pass; qa_torn_all.py classifies every torn pixel)
> qa_ab.py before after after-capture.log
has_osd region: 298203 / 299927 px (even/odd field), 14.38 % of the frame
  1360-1372: 13 fields, 1 identical; unexplained differences in 0; inside has_osd max 113 levels
    field 1367 torn (amp 0.64, top 1): 121 px outside has_osd, rows 55-76, max 49; within the torn OSD's reach: 121; beyond it: 0
  1405-1435: 31 fields, 0 identical; unexplained differences in 0; inside has_osd max 105 levels
  260-275: 16 fields, 6 identical; unexplained differences in 0; inside has_osd max 155 levels
  3565-3612: 48 fields, 18 identical; unexplained differences in 0; inside has_osd max 113 levels
    field 3572 torn (amp 0.59, top 1): 34 px … within the torn OSD's reach: 34; beyond it: 0
    field 3573 torn (amp 0.59, top 1): 32 px … within the torn OSD's reach: 32; beyond it: 0
  600-700: 101 fields, 0 identical; unexplained differences in 0; inside has_osd max 113 levels
  603-612_hardcut: 10 fields, 5 identical; unexplained differences in 0; inside has_osd max 113 levels
    fields 603/604/605/606/607 torn (amp 0.95, bottom): 256/373/71/204/740 px outside has_osd, all within the torn OSD's reach; beyond it: 0
total 219 fields, 30 bit for bit, 8 torn with differences outside has_osd, 0 with unexplained differences
  (the identical fields are the blue, black and loss fields, where no OSD shows)
> qa_ab.py before after-godot after-godot-capture.log --wedge
  1405-1435: 31 fields, 31 identical · 260-275: 16, 16 identical · 600-700: 101, 101 identical
  1360-1372: 13 fields, 9 identical · 3565-3612: 48, 46 identical · 603-612_hardcut: 10, 5 identical (the other 5 torn)
total 219 fields, 208 bit for bit, 11 torn with differences outside has_osd, 0 with unexplained differences
> qa_torn_all.py before after-godot …     (every differing pixel of each torn field)
1365: 4472 px differ … wedge-black 4472 · 1366: 5859 / 5859 · 1367: 5643 / 5643 · 1368: 5109 / 5109
3572: 5831 / 5831 · 3573: 6698 / 6698 · 603: 8396 / 8396 · 604: 6750 / 6750 · 605: 7499 / 7499 · 606: 8886 / 8886 · 607: 10266 / 10266
> qa_ab.py before ..\osd\before before-capture.log   → total 161 fields, 161 bit for bit   (mine = the tech artist's base frames)
> qa_ab.py after ..\osd\after after-capture.log      → total 161 fields, 161 bit for bit   (mine = their branch frames)

# Hold 4: EditMode, the three assemblies only
> Unity -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode
    -assemblyNames "Fpv450.Feed.Tests;Fpv450.Feed.RigTests;Fpv450.Tests.Osd" -testResults qa-osd\editmode.xml -logFile … | Out-Host
Unity exit 0 after 134 s
test-run: total 112, passed 112, failed 0, skipped 0, result Passed
Passed: Fpv450.Feed.Tests.FpvCameraTests.TheOsdGoesThroughTheChannelByDefault

# Hold 5: the review player
> Unity -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.Windows -review -logFile build-review.log | Out-Host
[Build] Windows: Boot.unity / TestRange.unity / FarmLand.unity: 0 missing references
Build Finished, Result: Success.
[Build] Windows: Succeeded, 0 errors, 1 warnings, 963944732 bytes in 00:30:34.5   (a cold shader-variant cache)
Unity exit 0 after 1936 s
the one warning: Shader warning in 'Hidden/Fpv450/Feed': use of potentially uninitialized variable (screen_tex) at Feed.shader(212)
  (the base's build log has the same warning at line 189: the same line, 23 lines lower; not this change's)
the player's Fpv450.Feed.dll holds "_OsdChannel" and "the OSD through the channel" (both players)
# Hold 6: its smoke test
> 450fpv.exe -smoketest -windowed -smokeshots qa-osd\smoke-shots -logFile smoke-review.log
power: Balanced; AC: True
NVIDIA GeForce RTX 4050 Laptop GPU, 20.00 W, 3105 MHz, P8
smoke-review exit 0 after 84 s
[Feed] FPV camera: … the OSD through the channel (p4-feed 1.12) …   (noclip and flight rigs alike)
[Smoke] ok   the OSD reads DISARMED while waiting: 'DISARMED >0m' / '[00:00 0MAH' / '$4.20в SVINORIZ 10 OPTO 0.00' / '$25.2в 0.00a'
[Smoke] PASS: 0 checks failed, 0 errors logged          (55 checks ok)
# Hold 7: the development test-range player for -feedcost
> Unity -batchmode -quit -projectPath unity -executeMethod Fpv450.Build.TestRange -developmentBuild -logFile … | Out-Host
[Build] TestRange: Succeeded, 0 errors, 1 warnings (the same shader warning), 514884640 bytes in 00:01:12.9
[Build] the development player's connection: loopback only, no multicast
Unity exit 0 after 111 s
# -feedcost: not run yet; it waits for the Unity lock, which the pilot's build holds

# The measures (the tech artist's scripts, copied; output only under reference/_compare/osd-qa/; letters only)
> survey.py, present.py and profiles.py on B (260 frames), D (260), L (all), P (390); present/profiles --sim on my after\600-700 and before\600-700
B, D, L: every number equals the tech artist's; P within 0.002 px; my a600 and b600 equal theirs (table above)
> outline.py B 260 / D 260 / L 260 / --sim after\600-700 a600
B fit outline = 62.1 + 0.209 * scene; scene 80-120 p50 84.0; 120-160 p50 94.0
D fit outline = 43.2 + 0.252 * scene; scene 80-120 p50 75.0; 120-160 p50 81.0
L fit outline = 5.0 + 0.189 * scene; scene 0-40 p50 3.0
a600 fit outline = 3.9 + 0.312 * scene; scene 120-160 p50 55.0
> levels.py after a600 600-700 1405-1435
600 254.7 (ink) · 1405 236.4 · 1422 232.1 · 1424 231.6 · 1435 234.0   (the quality drop's degrade 0.63 at 1405, rising)
> qa_sheet.py ..  → reference/_compare/osd-qa/sheets/qa-left.png (1232x1980), qa-middle.png (1016x1760)

# Hygiene
> python tools/hygiene/precommit.py --tree HEAD
3695 files scanned, 0 findings
> openspec validate p4-feed --strict
Change 'p4-feed' is valid
> per commit 88acbec1..HEAD: author | committer | subject length | body lines | trailers
ccf26748 | Tech Artist <techart@450fpv.local> | same | 59 | 0 | 0
53af572d | Tech Artist <techart@450fpv.local> | same | 61 | 0 | 0
393d93e7 | Tech Artist <techart@450fpv.local> | same | 67 | 0 | 0
ef10589b | Tech Artist <techart@450fpv.local> | same | 62 | 0 | 0
> git diff --name-only 88acbec1..HEAD | grep reference/|Library/|Temp/|Logs/|UserSettings   → nothing
```

Outputs, all local and none committed, are in `feedback-1117/qa-osd/` in the session's scratchpad: the captures and logs, `qa_ab.py`, `qa_torn_all.py`, the A/B outputs, `editmode.xml`, both builds' and the smoke's logs, `smoke-shots/`, and `summary-qa.txt`. The footage-derived measures and the sheets are in `reference/_compare/osd-qa/`, local only.
