# Review: study-flight-references (task 4.1)

**Reviewer:** QA Engineer · **Branch:** `world/study-flight-references` at `74ce897` · **Against:** `specs/reference-frames/spec.md`, `specs/reference-notes/spec.md`, and the two accepted deviations in `design.md` (local-jump flag rule, k = 10; container frame count one higher than decoded rows on 4 clips)

## Verdict

- **4.1 (this section and everything down to "Risks"): changes requested.** F1–F3 and A1–A5 have since been fixed and re-checked in 4.3.
- **4.3 (final re-review, at the end of this file): changes requested.** Task 4.4 passes. Tasks 4.5 and 4.6 and the change's own files have findings that must be fixed before merge. The branch is **not** cleared for merge.

## How it was run

- **Two full runs** of the unmodified tool, from the repo root:
  - Command: `blender -b --factory-startup --python tools/reference/extract_frames.py`.
  - Before run 2, six stale files were planted in `reference/_frames/`: in a clip folder, in `events/`, in a fake clip folder, at the root, and as a fake still and a fake burst.
- **An independent verifier** (a QA scratch script, not committed) checked the output:
  - It parses each MP4's sample table to count container frames without Blender.
  - It reads the PNG IHDR headers and checks the JPEG sizes.
  - It rebuilds the expected still, burst and event sets from `metrics.csv` and compares them file by file.
  - It prints clip letters only.
- **A synthetic flash clip.** Blender built a seeded 90-frame 1920×1080 H.264 clip with known glitches. An unmodified copy of the tool then ran on it in an isolated scratch tree.
- **OPSEC scans.**
  - Regex scans cover every banned category.
  - A fragment scan checks the 62 fragments of the 11 local reference file names, taken from the local index. It covers both notes, the full `main..HEAD` diff, all branch commit messages, and the part of `origin/main` history that is not yet on local `main`.
  - The terms seen on the 9 contact sheets were checked by hand as well.

## Scenario results: reference-frames

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | Full run | **Pass** | Run 1: exit 0, 188 s wall (tool reports 184.0 s). A–H each have stills at 1.0 s (16/13/10/11/12/13/4/25), each within ±1 frame of its nominal time. There is one contact sheet per clip and 2 for H. |
| 2 | Consecutive bursts | **Pass** | 8/8 clips have 3 bursts × 8 contiguous native frames. They start at frame 1, at 1 + (n − 8) // 2 and at n − 7, as checked against each clip's frame count. |
| 3 | Lossless detail frames | **Pass** | All 400 PNGs are valid: 192 burst frames plus 208 event frames. Each has the PNG signature and an IHDR of 1920×1080, 8-bit RGB (colour type 2). Stills and sheets are 1920×1080 JPEG. |
| 4 | Complete metrics | **Pass** | 3016 rows in total, header as in the spec, 0 empty cells, every value finite, frame column 1..n. Container sample counts equal the rows for B, F, G and H, and are rows + 1 for A, C, D and E. That is the accepted deviation, and each of those 4 clips ends in a static run (last 3 `diff` = 0.00). |
| 5 | Events exported | **Pass** | 73 flagged frames. The 208 event PNGs match exactly the union of each flagged frame ±2, clamped: none missing, none extra. |
| 6 | Short flashes are not missed | **Pass** (synthetic) | The real footage has no in-flight flash (`video-feed.md` U1), so a seeded clip was used. It has full-frame snow on single frames 2, 45 and 90 (the last frame) and one stripe frame at 60. All 4 planted frames were flagged. The only other flags are the 3 recovery frames after them, on `diff`, as designed. 0 of the 83 clean panning frames were flagged. Events clamp correctly (1–5 and 88–90). |
| 7 | Git stays clean | **Pass** | `git status --porcelain` is empty after both runs. `git status --porcelain --ignored` is identical to the pre-run baseline. No file outside `reference/_frames/` and `.git/` is newer than the run markers. |
| 8 | Stable letters | **Pass** | The hashed letter → file map is identical across the World Artist's earlier run, run 1 and run 2 (11 of 11). |
| 9 | Second run | **Pass** | Run 2: exit 0, 186 s. All 6 planted stale files are gone, and the verifier finds only expected files. Every `metrics.csv` is byte-identical to run 1, so the tool is deterministic. |

**Performance.** A full run takes about 3 minutes, well under the 20-minute threshold in `design.md`. The 60 fps game budget does not apply, because this change has no runtime code.

## Scenario results: reference-notes

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 10 | Hygiene check (both notes) | **Pass** | See the OPSEC table below. The branch outside the notes has one hit, `design.md` (F1). |
| 11 | Every clip represented | **Pass** | Letters A–K are all cited, across 28 catalog entries. Every recurring object class on the 9 contact sheets has an entry. |
| 12 | Physical relevance stated | **Pass** | 28 of 28 entries have a **Physical role** with tags. O5 and O6 say `visual only`. |
| 13 | Ready for M1 planning | **Pass** | The file ends with a ranked build list of 12 items (at least 8 required). Each has a footage-based "Reason:" and together they cover patches, objects and textures. |
| 14 | Traits map to effects and drivers | **Pass** | Every N, P, O and C trait cites clips and names an effect and a driver. N10 and P10 are classed as re-encoding, with 16-px grid gradient evidence. The OSD layout in §5 names no driver (A4, advisory). |
| 15 | Measured event statistics | **Pass, with a deviation to record** | Every event type gives a rate and a duration in frames. The loss events N5–N8 are event-driven by D-008, and the notes explain why. From `metrics.csv` I reproduced: 2369 picture frames; the loss frames of all 7 clips; 52–108-frame blue tails; N9 = 29 near-repeats including every example frame; N11 at D f183; and the AE/AWB figures (A f70/84/100, E f196 → f241). The N2–N4 counts come from full-resolution detector scans, not from `metrics.csv` (A2, A3). I spot-checked them at native resolution: thin lines in C f37, clean sky in B f4, diagonal interference in B f5. There is one internal count mismatch (F3). |
| 16 | Full coverage | **Pass** | All five areas are present (§1–§5), and the uncertain traits are listed together (U1–U13). |
| 17 | Drivers are covered | **Fail** | O4's "dust exposure" driver has no signal (F2). Every other driver maps to a signal with a unit and a rate. |
| 18 | Gate | Open | 4.2 is pending. See the process note under Risks. |

Small measurement differences, within tolerance and not findings:
- B's in-flight luma is 138.3–141.4 against 139.6–141.4 (`video-feed.md:237`).
- G's starting r/g is 1.13–1.17 against 1.18 (`video-feed.md:252`).

Neither changes the conclusions of C1 or C2.

## OPSEC scan of both notes

| Banned category | Result | How checked |
|---|---|---|
| Place or settlement names | none | Every capitalised token in both files was reviewed. The one Cyrillic word is the generic term for a tree belt. |
| Coordinates | none | Degree, long-decimal, MGRS/UTM and lat/long patterns, and compass directions: 0 hits. |
| OSD values | none | Voltage, capacity, speed, distance, altitude, heading and link-quality patterns: 0. The OSD is described as layout only. None of the status or warning words or readouts seen on the sheets appears. |
| On-screen model names | none | Every craft or model string visible on the 9 sheets, plus spelling variants: 0 hits in the notes and 0 in the branch diff. |
| Dates or timestamps | none | Year, month-name, numeric-date and hh:mm patterns: 0 real hits. The only year-shaped hit is the resolution "1920". Frame numbers are relative to the clip. Seasons are vegetation cues, not dates. |
| Reference file names | none in the notes | Of the 62 fragments, only the ordinary words "image" and "video" and plain two-digit measurements matched. **In the branch:** `design.md` (F1). |
| Unit or callsign information | none | The only hits are "unit vector" and a table header "Unit". |
| Identifiable landmarks | none | Descriptions stay at class level: village house, cellar head, five-storey block. No names, signage or unique features. |
| Positions of troops or equipment | none | The trench, round pit and earth bank are described as terrain classes with no location. See ruling 1. |
| People | none | The only hits are "colour killed", "rigid-body" and "body rates". Content in the footage beyond terrain is described neutrally (for example "a close object"). |

**"720 × 288" (`video-feed.md:314`) is confirmed as a derived figure, not screen text.** It is the BT.601 PAL working resolution for one field: 720 active samples by 576 ÷ 2 lines. It is exactly 10.0% of 1920 × 1080, as the note says. The receiver's own screen text is a different string. The notes do not quote it (`video-feed.md:128`, "content omitted"), and neither does this review.

## Findings

Findings marked **F** must be fixed before merge. Items marked **A** are advisory.

| ID | Owner | Where | Finding | Suggested fix |
|---|---|---|---|---|
| F1 | Orchestrator | `openspec/changes/study-flight-references/design.md:5`, `:31` | Both lines contain a product name that is a distinctive fragment of 7 of the 11 local reference file names. The notes deliberately avoid it: `video-feed.md` says "messenger re-encode". The text was merged into `origin/main` with PR #1, so it is already in the remote history. | Reword both lines to "messenger re-encode(s)". Whether to rewrite the published history is the user's decision. |
| F2 | Tech Artist | `docs/reference-notes/video-feed.md:213` vs the signal list at `:339–353` and the coverage table at `:355–368` | O4 names "dust exposure" as a driver, but no signal provides it. This fails the scenario "every driver … appears in the list with a unit and a rate". | Either add a signal such as dust exposure at the camera (surface dust class × prop-wash ground proximity, 0–1, 50 Hz, physics/world), or drop dust from O4's driver and leave it with U12. |
| F3 | Tech Artist | `docs/reference-notes/video-feed.md:58`, `:101` | N3 is given as "4 events (38 frames)", but its durations of 2, 3, 24 and 12 frames add up to 41. The 27% at `:101` uses 38. | State which is right, for example "38 detected frames within runs spanning 41", and make the percentage match. |
| A1 | World Artist | `docs/reference-notes/terrain.md:381` | "core entry-target structure" reads as targeting language (see ruling 2). | Use "core fly-in structure". |
| A2 | Orchestrator | `design.md` (Decisions) | The spec says the event rates come from `metrics.csv`. N2–N4 use full-resolution detectors instead, because the per-frame metrics barely flag thin or partial-field interference (only C f37 and B f123/f131). This is a better measurement, but it is a deviation. | Record it as a third accepted deviation, next to the two already in the design. |
| A3 | Tech Artist | `docs/reference-notes/video-feed.md:107` | The N4 "sky-region scan" has no stated criterion, unlike N2 (`:83`) and N3 (`:100`). Its 79 runs and 125 frames cannot be reproduced. | Add one line giving the detector's region, measure and threshold. |
| A4 | Tech Artist | `docs/reference-notes/video-feed.md:280–301` | The OSD layout and its blinking warnings name no driver. | One line: flight and warning state from the sim (content owned by game-developer). The OSD is then drawn before the analog stages, as the note already says. |
| A5 | World Artist | `tools/reference/extract_frames.py:140–141` | `COLUMNS.index(name) - 2` ties the metrics array to the CSV column order through a magic offset. The jump comprehension is a single 124-character line. | Add a named `METRICS` tuple in `frame_metrics` order, and compute the neighbour median in a small loop or helper. This is readability only; the behaviour is verified. |
| A6 | Orchestrator | `docs/workflow.md:10`, `:27` | The ticks for tasks 2.1–2.2 and 3.1–3.2 were committed by the Orchestrator, not by the owning roles. The subject of `162131e` has no scope. | Process note, no action. `162131e` is already on `origin/main`. |

## Rulings on the two judgement calls

**1. Is the "trench" in clip G (W2, and the "earth bank" in B) a position of troops or equipment? No. Keep the word "trench".**

The ban covers *positions*, meaning where troops or equipment are. The text gives no location, orientation, length, occupancy, owner, fortification detail, or relation to other features or to a front line.

A trench beside a tree belt is a textbook landscape class of this war and is widely documented in public. The stated dimensions (0.6–1.0 m wide, 1.2–1.8 m deep, with parapets) already define the class. "Linear earthwork" would therefore carry the same information under a vaguer name, and it would make the M1 build item (rank 6) less clear to the people building it. It is also physically important, as a narrow corridor with its own turbulence.

The condition is that the entry stays at class level. That means no occupancy, no dugouts or firing points, no direction or extent, and no relation to other positions. The same applies to W3 (the round pit) and to B's earth bank. All three meet that condition today.

**2. Is describing flights into a house door (E) or a cellar door (C) tactical detail? No. It is fine as a generic flight-path description.**

Flying through doorways and into cellars is the core skill the manifesto asks the sim to train ("fly through narrow holes"), and the technique is widely published. The notes say nothing about what was inside or who was there. They don't give the purpose of a flight, or any outcome beyond "picture lost". The interiors are described only as debris, a cable and a far window.

The one phrase that leans towards targeting language is "entry-target" (A1).

Keep future edits, including the user's corrections in 4.2, at this level. Neutral wording such as "a close object" for anything in the footage that is not terrain should stay as it is.

## Hygiene

| Check | Result |
|---|---|
| `reference/` tracking | `git ls-files reference/` returns only `reference/README.md`. The ignore rule `/reference/*` with `!/reference/README.md` is in place. |
| `.godot/`, logs, builds | None in the diff. The 11 changed files are all text. |
| Binaries and LFS | `git diff --numstat main..HEAD` shows no binary entries. No LFS objects are needed, and the LFS rules exist in `.gitattributes`. |
| Commit authors | World Artist wrote `tools/reference/extract_frames.py` (2 commits), `terrain.md` and the task 1.x ticks. Tech Artist wrote `video-feed.md`. The Orchestrator wrote the OpenSpec artifacts, D-008 and the task 2.x/3.x ticks. All identities match `docs/team.md`, and author equals committer. |
| Commit format | 11 of 11 commits are subject-only, with no body and no trailers or co-author lines. The longest subject is 70 characters. One subject has no scope (A6). |
| Scope | The work matches tasks 1.1–3.2. The D-008 edits to `docs/decisions.md` and `.claude/agents/tech-artist.md` are the Orchestrator folding in the user's direction for this change. |

## Code-quality pass on `tools/reference/extract_frames.py` (karpathy-guidelines)

- **Simple and readable.** It is one 330-line file of flat functions, with the constants named and commented at the top and a docstring that explains usage and OPSEC.
- **No speculative features.**
  - The only options are `--interval`, `--burst-len` and `--k`, all asked for in the tasks.
  - The version check and the black-frame check are the "fails loudly" behaviour the design asks for.
  - The video extension list is broader than the current set, but the spec says "every video file", so that is justified.
- **Correct by test.** It is deterministic across runs, cleans up after itself, and writes nothing outside `reference/_frames/`.
- **One readability nit:** A5.
- **No automated test is committed** for the tool. The spec does not require one. The synthetic-flash scenario above would make a good regression test if the team wants one later.

## Risks

- **Merged before review.** The repository owner merged PR #1 and PR #2 from this branch into `origin/main` before this review and before the 4.2 user gate. They carried the tool, the specs, the design and D-008, but not the notes. The notes themselves are already pushed to the remote branch.
- **The user's corrections may add sensitive detail.** Answers to U7 in particular, about what B's drone carried, could bring in equipment or unit detail. QA re-runs the full OPSEC scan in 4.3.
- **The OSD layout table is a fingerprint.** The per-airframe table in `video-feed.md` (§5) is required by the spec and contains no values. Even so, a detailed layout can be matched against other footage, so the user should accept it knowingly at 4.2.

## Final re-review (task 4.3)

**Reviewer:** QA Engineer · **Branch:** `world/study-flight-references` at `4dc2d39` · **Verdict: changes requested**

This section replaces an earlier "Final re-review" that approved the branch. The QA role did not write that one. It is treated below as an outside claim, and every point in it was re-checked.

### Provenance: four commits from an outside session

While the team was paused, the user had another AI tool finish this branch. All four unpushed commits come from that one session: they were made within 6 minutes (10:34–10:40), under three different role identities.

| Commit | Identity used | Task | Re-verified here |
|---|---|---|---|
| `fc67eaf` | World Artist | 4.4, tool: stable letters and AVIF | Yes: re-run, before/after diff, code read. **Pass** |
| `a2f5c83` | World Artist | 4.6, `terrain.md` | Yes: photos M–O, L frames, OPSEC. **F4** |
| `d5b88ac` | Tech Artist | 4.5, `video-feed.md` | Yes: L and the C, D, F loss frames and metrics. **F1–F3** |
| `4dc2d39` | QA Engineer | 4.3, approval; ticks 4.3–4.6 | Superseded by this section. It named a reference file (F8) |

The session that wrote 4.5 also approved it, so that approval was not independent.

### How it was run

- **Tool re-run.**
  - Command: `blender -b --factory-startup --python tools/reference/extract_frames.py --`, from the repo root, with the tool unmodified.
  - Exit 0 in 510 s wall time (the tool reports 504.6 s) for 10 clips and 6 photos. That is under the 20-minute threshold in `design.md`.
  - It is slower than in 4.1 (184 s) only because of the new 1417-frame clip P.
- **Letter map.** Before the run, each index row was hashed as letter → hash of the path, then compared after the run. No file name left `reference/`.
- **Clip P** is the pilot's new severe-weather flight. It took the letter P as expected. It belongs to the upcoming wind study and is left out of every coverage check below.
- **Night clip L** was checked against its `metrics.csv`, its contact sheet, its bursts and every event frame. Scratch scripts (not committed) measured the lossless PNGs:
  - per-pixel chroma
  - full-resolution grain in static sky across consecutive frames
  - the rise width of OSD edges
- **Loss sequences.** The per-frame state around every loss (A–F, H) was read from `metrics.csv`. C f174 was also viewed at native resolution.
- **Photos M–O** were viewed for vehicle class, and for plates, signs, credits and people.
- **OPSEC.**
  - 44 distinctive fragments were taken from the 16 local reference file names, including the date and time strings inside them.
  - They were searched for in every tracked file, in every commit message on every branch, and in the full patch history (`git log -p --all`).
  - Regex scans covered dates, times, coordinates and OSD-value patterns.
  - Text read off the new photos and off L's OSD was searched for by hand.

### Scenario results after the user's corrections

| # | Scenario | Result | Evidence |
|---|---|---|---|
| 1 | Full run | **Pass** | Exit 0. L has 7 stills at 1.0 s and 1 sheet. A–H are unchanged from 4.1. |
| 2 | Consecutive bursts | **Pass** | L has 3 bursts of 8 contiguous frames. |
| 3 | Lossless detail frames | **Pass** | All 53 of L's PNGs are valid 1920×1080 8-bit RGB. The AVIF copies are 8-bit RGB (M 946×709, N 1200×675, O 408×272) and match Blender's decode of the source exactly (maximum difference 0). |
| 4 | Complete metrics | **Pass** | A–H and L have 3226 rows, 0 empty, all values finite, frames 1..n. L's container holds 210 video samples, equal to its rows. Every A–L `metrics.csv` is byte-identical to the previous run. |
| 5 | Events exported | **Pass** | A–H: 73 flagged frames and 208 event PNGs, the same as in 4.1. L: 9 flagged frames and 29 PNGs, exactly the flagged frames ±2. |
| 7 | Git stays clean | **Pass** | `git status --porcelain` is empty, and the `--ignored` output matches the baseline taken before the run. No file outside `reference/_frames/` and `.git/` is newer than the run marker. |
| 8 | Stable letters (task 4.4) | **Pass** | A–O map to the same files before and after the run (15 of 15). The only new row is P, which was the one unindexed file. |
| 9 | Second run | **Pass** | The previous output was replaced with no stale files left behind, and `_scan.bmp` was removed. |
| 10 | Hygiene check | **Fail** | `terrain.md` describes L's take-off point (F4). Outside the notes: `pilot-answers.md` (F5) and this file (F8, fixed). |
| 11 | Every clip represented | **Pass** | `terrain.md` cites A–O. `video-feed.md` cites A–H and L. P is cited nowhere. |
| 12 | Physical relevance stated | **Pass** | R0–R5 and the new T3a and T3b entries all state a physical role. Vehicle sizes are marked as general knowledge, separate from what the photos show. |
| 13 | Ready for M1 planning | **Pass** | 15 ranked items, each with a "Reason:". |
| 14 | Traits map to effects and drivers | **Fail** | N5–N7 lost their candidate effect and now contradict the frames (F1). N12 cites no clip (F3). |
| 15 | Measured event statistics | **Fail** | The N5–N7 stage durations disagree with `metrics.csv` (F1). N12's look, length and rate were not measured, and the note doesn't say so (F3). |
| 16 | Full coverage | **Pass** | All five areas are present, and every U-item has a status. U4 is missing its label (A2). |
| 17 | Drivers are covered | **Pass** | Each new driver maps to a signal. N12: manoeuvres → camera angular velocity, current transients → current step. C5: light level → scene light level and time of day. N5–N7 → link state, battery voltage and the impact event. |
| 18 | Gate | **Closed** | The user answered in 4.2. Under the pilot's review-scope rule, the fixes below need QA, not the pilot. |

Scenario 6 (short flashes) has not changed since 4.1 and was not re-run.

### Task 4.4: tool review (karpathy-guidelines)

**Pass, no findings.**
- `previous_letters()` and `assign_letters()` (`tools/reference/extract_frames.py:73–98`) keep the old letters and give new files the next ones.
- They fail loudly, before anything is wiped, if an indexed file has disappeared or if the letters would run past Z.
- The index is still written first, so a failed run leaves a readable map.
- `copy_photo()` (`:101–111`) is short and does one thing.
- The docstring matches the behaviour. There are no leftovers: the old 26-file check was replaced, not duplicated.
- The only gap is in the spec, not the code (F7).

### Task 4.5: night clip L, claim by claim

| Claim (`video-feed.md` C5 and N1) | Verdict | Evidence |
|---|---|---|
| Mean luma 45.6, range 10.9–55.0 | Confirmed | `L/metrics.csv` |
| Monochrome, R/G/B 45.08/45.96/44.82 | Confirmed, and stronger than stated | Per-pixel chroma magnitude, excluding black and OSD white: median 1.3 levels in L, against 34–61 in A, B, G and H |
| The sky is brighter than the ground, and silhouettes are the only landmarks | Confirmed | Sky band ≈ 75 levels, ground ≈ 24 |
| The OSD is full white and unaffected by exposure | Confirmed, but the best evidence isn't cited | OSD whites reach ≈ 246. At f163 and f200 the whole scene darkens for one frame while the OSD stays unchanged |
| The OSD is "stark, razor-sharp" | **Contradicted** | L's OSD edges rise over 5–6 px (10–90 %), and the unit glyphs have coloured fringes (f1, f162), as P2 and P4 describe for day footage |
| "Noise metric mean 8.37" shows maximum-gain grain, which "reaches 8.37 even on dark terrain" (N1) | **Wrong evidence** | 8.37 is the whole-frame mean of the 25 %-scale texture metric, and it is the **lowest** of all nine clips (A–H: 11.2–25.8). The grain is real, but other numbers show it. In static sky (f161/162, f165/166, f202/203): σ ≈ 2.0–2.4 levels, or 2.7–3.1 % of the level, with a frame-to-frame correlation of 0.23–0.31. A's and B's skies measure 0.3 % and 0.8 %, with correlations of 0.36 and 0.54 |
| "Camera AGC at maximum gain", "shutter 1/50 s" | **Not measured** | Both are stated as findings. Exposure also steps *up* at f81 (37 → 52) and f115 (46 → 50), so there was headroom left |
| The "9 diff-jump flags correspond to rapid heading/pitch adjustments", with motion blur | **Contradicted** | The 9 flags are 5 whole-frame **exposure steps**, with framing, horizon and OSD unchanged: f13 → 14 (43 → 29 → 34), f80 → 81 (37 → 52), f114 → 115 (46 → 50), f162 → 163 → 164 (52 → **18** → 42) and f199 → 200 → 201 (44 → **11** → 27). Two of them pass through a single dark frame and recover. There is no motion blur in f162–164 or f199–201. This is a real night trait, and it is missing from the notes |
| Point lights bloom into soft halos | **No evidence** | No point light appears in any still, burst or event frame of L. The only bright spot is speckled ground under the camera at take-off (f1), and it is gone by the second still |

### Findings

Findings marked **F** must be fixed before merge. Items marked **A** are advisory.

| ID | Owner | Where | Finding | Suggested fix |
|---|---|---|---|---|
| F1 | Tech Artist | `docs/reference-notes/video-feed.md:60–61`, `:124–134` | **The 4-stage loss rewrite contradicts the footage, and the measured detail it replaced was deleted.** `metrics.csv` shows the same sequence in C, D and F:<br>1. **1–2 frames of partial snow** (C f174–175, D f208–209, F f250). Their noise is 40–44, against 8–14 in picture. C f174 is ≈ 83 % coloured snow, and the picture survives only in the bottom ≈ 17 %.<br>2. **Exactly 4 solid-blue frames** (C f176–179, D f210–213, F f251–254).<br>3. **7–8 frames of full snow** (C f180–186, D f214–221, F f255–262).<br>4. Permanent blue.<br><br>The note says instead that stage 1 is "line displacement… picture remains largely visible", that stage 2 is a "white-out or luma spike", which no frame shows, and that stage 3 lasts "4–8 frames". It cites C f176–186 as monochrome snow, but f176–179 are blue.<br><br>The rewrite also removed the 4-frame blue interlude, the snow texture (hard-clipped blobs, ≈ 25 % white, luma 53–65), the receiver text drawn over the snow, and the candidate effect (the state machine). N5–N7 now have no candidate effect at all | Map the pilot's four stages onto the measured sequence. The natural fit: glitch = partial snow, flashy moment = the 4-frame blue flash, noisy and flashy = full snow, finally blue = blue.<br><br>Restore the measured durations, the texture and a candidate effect, then randomise *around* the measured values. Keep H f628 (N8) and the hard cut (A, B, E) as variants. Label anything that comes only from the pilot's words, such as a white flash, "pilot, unconfirmed, tunable" |
| F2 | Tech Artist | `docs/reference-notes/video-feed.md:275–284` (C5), `:82` (N1), `:331` (U5) | **The night characterisation is partly ungrounded.** See the claim table above:<br>- The motion-blur reading of the flags is wrong.<br>- The grain evidence is the wrong metric.<br>- Maximum gain and 1/50 s are asserted, not measured.<br>- "Razor-sharp" is contradicted by the frames.<br>- The point lights have no evidence.<br><br>The exposure-step trait that L does show is missing | Add L's exposure steps as a camera trait: discrete steps of −36 % to +41 % in mean luma, twice through one dark frame (−65 %, −75 %). Driver: light level. Stage: cam.<br><br>Replace the 8.37 argument with the static-sky figures. Mark gain and shutter as hypotheses. Drop "razor-sharp" and the point lights, or mark them unconfirmed. Reword U5, which says "fully characterised" |
| F3 | Tech Artist | `docs/reference-notes/video-feed.md:67`, `:142–146` (N12) | N12 cites no clip ("pilot confirmed; scattered"). Its look and length ("1–3 frames: snow burst, white flash, stripes") were not measured. No recovering flash exists in A–H (see 4.1). In L, the only events that recover are camera-side exposure dips (f163, f200), not link flashes | State "not in the footage; from the pilot (U1)", and mark the look, length and rate "unconfirmed, tunable". Say whether L f163 and f200 are a real kind of recovering flash (cam) |
| F4 | World Artist | `docs/reference-notes/terrain.md:34`, `:43`, `:409`, `:412` | **OPSEC.** L's entry describes the pilot's take-off point and its distinctive features: "a climb from a low ridge with a leaning pole, a bush and a small tree", a pole with a crossarm leaning about 10°, and "a single leaning pole on a ridge". L starts at that spot, close to the ground (f1).<br><br>The spec bans positions of troops, and the 4.1 rulings keep flights at class level with no link to any position. This text ties a group of landmarks to where the crew stood | Describe classes only, for example: "rolling dark fields and a tree line; poles, lone trees and bushes read only as silhouettes against the sky; poles may lean 5–15°". Drop "a climb from", the ridge-top grouping and the detail of each object at that spot. `video-feed.md:277` can stay as it is: its wording is already generic |
| F5 | Orchestrator | `openspec/changes/study-flight-references/pilot-answers.md:14`, `:24` | **OPSEC.** These two lines give real reference file names: the night clip's and those of the three vehicle photos. They were committed in `d1cf80c`, and they are **already on `origin/main`** (merged through PR #8) and on the remote branch | Refer to them as "the night clip (L)" and "the three vehicle photos (M–O)". Rewriting the published history is the user's decision |
| F6 | Orchestrator | `openspec/changes/study-flight-references/tasks.md:24–26` | The outside commit `4dc2d39` ticked 4.4, 4.5, 4.6 and 4.3 under the QA identity. 4.4 is verified. 4.5 (F1–F3), 4.6 (F4) and 4.3 (this verdict) are not done | Untick 4.3, 4.5 and 4.6 until their findings are fixed and re-checked |
| F7 | Orchestrator | `specs/reference-frames/spec.md:13`, `:50`; `design.md:5`, `:23`, `:25` | The spec and the design still describe the old lettering: "in sorted path order", "8 clips", stills "under letters I, J, K". Neither mentions `<letter>/photo.png`. Task 4.4 changed this behaviour on purpose, so archiving now would publish a capability spec that the tool contradicts | Change the requirement to "existing letters are kept; new files take the next letters", and add a scenario for an added file. Add `photo.png` for AVIF photos to the output layout |
| F8 | QA Engineer (text), Orchestrator (history) | this file, in `4dc2d39` | **OPSEC.** The outside "Final re-review" gave the night clip's real file name. **Fixed in this commit:** this file now cites clips and photos by letter only. `4dc2d39` still carries the name, but it is **not pushed yet** | Before any push, drop or rewrite `4dc2d39`, so that no commit that reaches the remote introduces the name. Its tasks.md ticks get reverted anyway (F6) |
| A1 | Tech Artist | `video-feed.md:59`, `:109`, `:122` | With motor current demoted to "rare", N3 and N4 are left without a main trigger. N4's "(pilot confirmation)" overstates what the pilot said: U7 is unanswered, and the U6 answer was about noise in general | Name the main trigger: a per-flight draw plus free-running random at the measured rates, with current as a weak modulator. Drop "(pilot confirmation)" from N4 |
| A2 | Tech Artist | `video-feed.md:26–29`, `:40–42`, `:57–59`, `:64`, `:330`, `:352` | Several statistics and labels are now out of step:<br>- L's 7 s were added to the in-flight denominator, but the N2–N4 detectors ran only on A–H.<br>- N9's 29 repeats leave out L's near-repeats (f192, f204, f210).<br>- §0 does not place L in a receiver or airframe group. L runs at exactly 30.000 fps; the others run at 29.84–29.97.<br>- U4 lacks "unconfirmed, tunable", although the pilot left it unanswered.<br>- The vibration signal lost its "hypothesis" label, although U8 is still open | Say "A–H only" or extend the scans to L. Place L in §0. Label U4, and restore "hypothesis" on the vibration row |
| A3 | World Artist | `terrain.md:349` and §6 | The object classes O1–O6 share their letter with photo O. At `:349`, "O3, O4" are classes; elsewhere "O" is the photo | Cite the photo as "photo O" in §5, or rename the class prefix |
| A4 | Orchestrator | `reference/` (local, git-ignored) | The outside session left seven working files at the top of `reference/`, including two copies of the index with real file names in them. They are ignored and local, so nothing leaks, but they sit outside `_frames/` | Delete them, or ask the user to |

### OPSEC scan (full)

| Scope | Result |
|---|---|
| `terrain.md` | F4 (the take-off point). No file-name fragments, place names, dates, coordinates, plates, signs, photo credits or people. The notes leave out N's background (signage, a crowd, landmark buildings) and O's credit line and foreground figure. |
| `video-feed.md` | No hits. No OSD values, OSD words or on-screen craft name. |
| `review.md` | One file name in the outside section. **Fixed** (F8). |
| `tasks.md` | No hits. The only date is the review date, not a reference date. |
| `pilot-answers.md` | Two real file names (F5). |
| Other tracked files | No hits. The creation date in `.openspec.yaml` equals a date inside some file names, but it is change metadata that every commit timestamp carries anyway. |
| Commit messages, all branches | No hits. |
| Full patch history, all branches | The file names in `d1cf80c` (F5) and `4dc2d39` (F8). The messenger's product name in `cbc22a1`, `7a1ccd1`, `6168b15` and `f55dfd2`. That is F1 from 4.1: it was removed from the tree, and rewriting history is the user's call. |

### Hygiene

- All four commits are subject-only (58–69 characters), with no trailers or co-author lines. Author equals committer in each.
- The identities match `docs/team.md`, but not who actually did the work (see Provenance).
- The diff is text only, with nothing from `reference/`, `.godot/`, logs or builds.
- The 4.1 fixes are still in place: F1 (`design.md:5`), F2 (`video-feed.md:220`), F3 (`video-feed.md:58`, `:105`), and A1–A5.

### Risks

- The file names in `pilot-answers.md` are public on `origin/main`. Scrubbing them takes a history rewrite and a force-push to a public `main`. That is the user's decision.
- F1 and F2 change what the feed implementation will build. If the loss sequence ships as it is written now, the feed would show a white flash the footage never shows, and it would leave out the blue flash that every snow-cycle loss shows.
