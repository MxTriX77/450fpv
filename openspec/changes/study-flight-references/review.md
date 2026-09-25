# Review: study-flight-references (task 4.1)

**Reviewer:** QA Engineer · **Branch:** `world/study-flight-references` at `74ce897` · **Against:** `specs/reference-frames/spec.md`, `specs/reference-notes/spec.md`, and the two accepted deviations in `design.md` (local-jump flag rule, k = 10; container frame count one higher than decoded rows on 4 clips)

## Verdict: changes requested

The tool passes every scenario, including a synthetic single-frame flash test. Both notes pass the OPSEC scan in every banned category.
Three small fixes are needed before merge:
- **F1:** a reference file-name fragment in `design.md`.
- **F2:** one trait driver has no signal in the list.
- **F3:** an internal count mismatch in `video-feed.md`.

None of them changes what the notes say, so the user's read-through (4.2) can go ahead now. The fixes can land together with the user's corrections and be re-checked in 4.3.

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
