# 450fpv: project knowledge base

Everything useful learned in the first attempt (Godot), distilled for the Unity restart. Read this once, then use it as a reference.

- **First attempt:** Godot 4.7 .NET (C#), Blender 5.2 and Git, in `https://github.com/MxTriX77/450fpv`. The last state is `main` at `b684cce` (PRs #1–#33, 2026-09-23 to 2026-09-30), to be tagged `godot-final`.
- **Restart:** Unity, free assets only, under the `unity/` branch namespace of the same repo (orphan trunk `unity/main`). The plan is in [plan.md](plan.md), the requirements in [requirements.md](requirements.md), and the decisions in [decisions.md](decisions.md).
- **What carries over:** the vision, the pilot's answers, the measured reference data (terrain, video feed, wind), the physics design, the map-format and world-query design, and the lessons. Almost all of it is engine-independent.
- **Tags:** `[portable]` holds for any engine. `[Godot]` was specific to the first attempt, and is kept only as a lesson.
- **Evidence labels**, as in the old repo:
  - **pilot**: the pilot said it. If an interpretation disagrees with the pilot's words, the pilot's words win.
  - **measured**: read from the pilot's footage frames.
  - **published**: from a cited public source.
  - **derived**: computed from the above with a stated model.
  - **assumed**: a plausible engineering value with no source. It can be tuned.
- **Full detail** lives in the copied notes:
  - [reference-notes/](reference-notes/): terrain, video feed, wind, airframes, pilot requirements
  - [legacy/](legacy/): the flight-model design and specs, the map-format and world-query specs, the physics reviews, the surface table and the old decision log

> **OPSEC, read first.**
> - Clips and photos are cited by **letter only**: clips A–H, L and P; photos I–K and M–O.
> - No places, coordinates, dates, file names, on-screen-display (OSD) values, unit or callsign details, landmarks or people. Nothing specific about the pilot's own sorties.
> - The raw footage lives only in `reference/` (git-ignored). **Never commit, upload or copy anything out of it.** Never put reference file names in notes, commits or code.

## Contents

1. [The manifesto](#1-the-manifesto)
2. [The pilot and the working agreement](#2-the-pilot-and-the-working-agreement)
3. [Milestones and where the first attempt stopped](#3-milestones-and-where-the-first-attempt-stopped)
4. [Pilot requirements and first-hand facts](#4-pilot-requirements-and-first-hand-facts)
5. [The drone](#5-the-drone)
6. [Flight model architecture](#6-flight-model-architecture-designed-not-built)
7. [Ground contact and materials](#7-ground-contact-and-materials)
8. [Wind, turbulence and rain](#8-wind-turbulence-and-rain)
9. [World reference](#9-world-reference)
10. [Map format and world queries](#10-map-format-and-world-queries)
11. [Analog video feed](#11-analog-video-feed)
12. [Old decision log](#12-old-decision-log-godot-d-001-to-d-017)
13. [What the first attempt built](#13-what-the-first-attempt-built)
14. [Lessons learned](#14-lessons-learned)
15. [Open questions and known debts](#15-open-questions-and-known-debts)
16. [Pointers](#16-pointers)

---

## 1. The manifesto

The pilot's own text is [../VISION.md](../VISION.md), verbatim and never edited without asking. It is the root requirement for everything. The pilot's brief of 2026-10-17 ([pilot-brief-2026-10-17.md](pilot-brief-2026-10-17.md)) changes three things:
- the tech stack, to Unity
- the order of work
- the drone's name, to **Svinoriz 10 Opto**

### 1.1 The manifesto as a checklist `[portable]`

| # | Requirement | Where it lands |
|---|---|---|
| M1 | Liftoff shake; the effective weight varies; a leg sticking for a moment tips the horizon | §6 legs, §7 hooks and stiction |
| M2 | Unexpected ground on landing: pitfalls, loose or porous soil, grass jitter | §7, §9, §10 `SampleGround` |
| M3 | Turbulence; narrow holes; dense woods in severe wind | §8, §6 wind layers, §10 gaps |
| M4 | Slight motor asynchrony makes the drone yank; heavy-airframe inertia; "a tiny, cunning, and elegant randomness" | §6 propulsion and randomness |
| M5 | Fiber tether: smooth orbits, no sharp reverse, and the line may or may not cut | §6 tether |
| M6 | Glide landing: holding the sticks after touchdown bounces the drone off its plastic legs | §6 legs, §7 |
| M7 | An analog, 2000s-TV feed that is dynamic, never plain noise | §11 |
| M8 | Settings: payload, legs on or off (off means the launch stand), time of day, plus wind (Calm/Windy/Severe) and rain (Sunny/Rain/Severe rain) | §4 |
| M9 | One big composite map of Ukrainian terrain; one drone with 3-blade props | §9, §5 |
| M10 | RadioMaster TX12 with easy calibration; a Train / Calibrate / Exit menu | §3 |
| M11 | 60+ fps on most PCs, and ultra smooth on the dev machine | §13 |
| M12 | Physics logging good enough to tell a bug from pilot error | §6 logging |
| M13 | An orchestrator with visible subagents, role git identities, reports, and few but smart questions | §2 |

---

## 2. The pilot and the working agreement

### 2.1 Who the pilot is `[portable]`
- A combat FPV pilot who flies **10-inch heavy fiber-optic quads of the Vyriy (Вирій) 10 Opto class**.
- They fly **acro (rate) mode** with **0° camera uptilt**, on a **RadioMaster TX12**.
- They have used many simulators, and want the one thing none of them has: physics that feel like the real aircraft, "unexpected and undefined, just like real life".
- **The standing bar** (PR-3): "we must have top-notch realistic physics no one implemented in any FPV sim out there yet."

### 2.2 How the pilot wants to be involved `[portable]`
- **They review only tangible milestones:** the gallery, the world, the feed, the drone, the wireframes, the MVPs and the release. They never review sandboxes, sample patches, plans, specs or notes. In their words: "checking sample projects is not something i need to review, i need to review tangible stuff up of my specs."
- **Research, don't ask.** Airframe specs, motors, batteries, Ukrainian building construction, the pre-war look and war damage are researched online, never asked. Two quotes:
  - "motors - again bro - ive mentioned many times what brands of quads you should rely on… go ahead and research"
  - on houses (PR-7): "search online to see what structures in Ukraine actually look like"

  Asking them about plaster was a mistake.
- **Ask only what only a pilot knows:** how the aircraft behaves, what they saw on a flight, how it felt. Keep questions short and few, and never "dumb". Rough answers are enough.
- **They are impatient for visible progress** ("We need gallery asap"). Deliver a flyable or viewable thing over another planning document.
- **The map will change later:** "keep in mind that in the future i may change the map". The map format and the models must make edits cheap. The 2026-10-17 brief repeats this for models and textures.
- **"REALISM"** is their word for the vehicles and for everything else. **Their definition (2026-10-17, D-028):** "when i say realism i mean more than just abstract realism… the game should be a copy of how it looks on reference (both my flight replay references and those images)… it's also important that we do proper geometry… not only for looks but also for physics".

### 2.3 Process rules the pilot set `[portable]`
- **Orchestrator and subagents.** The main session orchestrates and delegates to role subagents. The pilot must be able to *see* the team work: announce every handoff (role, change id, goal), and summarise every handoff that comes back.
- **Reports.** When asked for status, write a dated report that covers every role. Change the ongoing work if the pilot's feedback changes direction.
- **Commits.** The pilot's `AGENTS.md`, verbatim: "commit only meaningful pieces of changes, dont. commit everything in to single commit. use comit head for commits, dont use descriptions, dont include claude as commit collaborator". In practice:
  - one logical piece per commit
  - a **subject line only**, with no body
  - **no Claude co-author trailer**
- **Role git identities**, passed on every commit with `git -c user.name=… -c user.email=…` and never put in the global config. See [team.md](team.md).
- **PRs are automatic.** Push and open a PR as soon as a piece is committed, and merge it once QA is green. Never ask the pilot to open or merge one.
  - **Merge commits, never squash** (`gh pr merge --merge`), so each role's authorship survives.
  - Only a pilot gate waits for the pilot.
- **Spec-driven (OpenSpec):** propose → assign → apply → QA review → pilot sign-off at gates → merge → archive. If the pilot changes direction, the spec changes first and the code follows.
- **Decisions** go into the decision log.
- **Simplicity applies to code, never to physical fidelity.** This is the karpathy-guidelines rule the project chose. Ponytail and caveman were considered and rejected.

### 2.4 OPSEC and licensing `[portable]`
- **Footage** never enters git, never gets uploaded, and is never copied out of `reference/`.
- **Notes** describe landscape *classes*, never places, positions, dates, text or plates in images, people, or OSD values. Clips are cited by letter.
- **Sortie details are generic** (the pilot's choice): no airframe model tied to the pilot's own flight, no standoff distance, no flight duration, no exact loadout.
- **Maps** contain no georeferencing, real place names or coordinates. Signs and labels use invented names.
- **The OSD** reads **"Svinoriz 10 Opto"** (2026-10-17; the first attempt spelled it "Svinorez") and **"! SAFE !"** (not "! ACTIVE !"). The sim must not carry a real manufacturer's brand or present itself as armed. Never revert either.
- **The repo is public** (MxTriX77/450fpv). The pilot chose to keep the manifesto's statement about themselves public. Some early OPSEC slips stay in the public history by the pilot's choice (§14.2).
- **Free assets only** (2026-10-17):
  - CC0 is preferred. CC-BY is allowed with attribution.
  - No NC or ND licences, no paid assets.
  - **No Unity Asset Store or Fab content, not even free items**, because their EULAs forbid redistribution in a public repo.

---

## 3. Milestones and where the first attempt stopped

### 3.1 The old release plan, now replaced
The old plan ran UAT-1 (map parts gallery) → UAT-2 (noclip world) → wireframes → MVP-1 (TX12 flight) → MVP-n → release. The pilot's 2026-10-17 plan keeps that spirit but reorders the work: assets → gallery → world → feed and OSD → drone → TX input and basic physics → full physics. See [plan.md](plan.md).

### 3.2 Priorities the pilot set in the first attempt `[portable]`
- **2026-10-06:** "We need gallery asap, and we move on to the more important sections - physics and control interface."
- **D-016, controller input before flight tuning.** Flight feel can't be tuned without a real stick, and the input layer can be verified on its own (an axis display, endpoints, deadband, channel order, reversal).
  - **Design this once:** how USB HID stick samples, polled far slower than the 1 kHz physics step, are timestamped and fed into the step.
- **D-017, the gallery before the remaining assets,** with reserved "not built yet" stations, so assets land in the gallery as they're finished.

### 3.3 Where the first attempt stopped (2026-09-30) `[portable summary]`
- **Done:**
  - the reference studies (terrain, video feed, wind) and the airframe research
  - the map format and a deterministic, benchmarked world-query engine
  - a Godot project with noclip, a terrain renderer and terrain holes
  - 10 surfaces with CC0 textures
  - micro-detail rendering at 0 mm parity with physics
  - LOD switching
  - the tree belt: 5 tree types, a shrub and a far impostor
  - the adobe house, cellar, shed, fence and fruit tree (built, but the art pass was unfinished: chequerboard walls)
  - a 512 m gallery with reserved stations
  - an analog-feed demo spike with an exact OSD (built outside the process, as debt)
- **Not built:**
  - the flight model (only its spec draft exists)
  - controller input, the wireframes and the menus
  - the vehicles, the five-storey block, real wires and poles, and the debris kit
  - the world map
  - the physics → video interface
- UAT-1 was **not** reached, and nothing was ever flown with a stick.

---

## 4. Pilot requirements and first-hand facts

First-hand knowledge that no footage shows well. Every item is a requirement for the flight model, the feed, the OSD or the world. `[portable]` throughout.

### 4.1 Numbered requirements (PR-1 to PR-11)

**PR-1 Throttle-punch shake after arresting a fast descent.**
- Pilot: "when I suddenly need to lower altitude and when the alt is what I need, after I suddenly compensate with heavy throttle — the image vibrates for a mere 0.3–0.7 sec."
- Sequence: rapid descent → target height reached → sharp large throttle increase → **the image shakes for 0.3–0.7 s**, then settles. The aircraft does not go anywhere uncommanded.
- Candidate mechanisms (the model may use several): rotors re-ingesting their own disturbed downwash (near vortex ring state; a heavy quad with a bottom-mounted mass sits there easily); a torque step ringing the frame at its structural frequency; a pendulum mode of the bottom-mounted spool and payload.
- Requirement: a brief, damped, high-frequency disturbance of 0.3–0.7 s, seen mainly as **camera shake** rather than a trajectory error. Its presence and size depend on descent rate, input abruptness and payload mass. It must not fire on every throttle increase, and it must vary between occurrences.
- **It couples physics to the feed:** the camera is rigidly mounted, so physics must publish a camera-shake (vibration) signal and the feed must use it.
- Open: empty vs loaded? Near the ground? Full vs nearly paid-out spool?

**PR-2 OSD: copy the real layout exactly, change two strings.**
- "You should base off of the OSD you see on my videos. Except don't use the real Viriy brand name, use 'Svinorez 10 Opto'. Also there's '! ACTIVE !' label, instead it should say '! SAFE !'. Everything else should stay as it looks."
- **Renamed 2026-10-17:** the pilot now writes the name as **"Svinoriz 10 Opto"**. Use that spelling everywhere.
- Later (PR-2a): "the layout should look exactly the same, and the fonts also, and the crosshair also." The exact metrics are in §11.6.
- The OSD is inserted after the lens, so **it stays straight while the horizon bends**. It is inserted before transmission, so it carries the same analog artifacts. Reproduce the arrangement and **invent the numbers**: no OSD values from the footage, ever.

**PR-3 Wind must rotate the aircraft, not just push it.**
- "I want to make sure that it won't JUST make the drone drift towards the wind's direction, but also cause sometimes drone swirling and swerving in yaw axis, like you know its nose may lift up a bit suddenly AND turn in yaw axis in some direction, and when you try compensate — it can cause some intermediary control issues too."
- Requirements:
  - wind applies **moments as well as forces**
  - yaw swerve with no pilot input
  - **coupled nose-up plus yaw** as one event
  - corrections can lead into awkward intermediate states
  - irregular in direction, size and duration, never periodic
- Mechanisms that produce this honestly:
  - gust gradients across the four rotors (differential drag gives yaw, differential thrust gives pitch and roll)
  - vorticity in the wind field, especially in wakes
  - asymmetric disc loading in translational flight
  - the wind-loaded tether pulling from a point off the centre of mass
- Acceptance is behavioural, and the pilot judges it. The wind targets bound the magnitudes (§8).

**PR-4 Loss of picture is staged; the first version was wrong.**
- On the shipped demo: "That occasional FULL cut off — it's too sharp now, like it just appears and just disappears, and it's simple-ahh noise."
- Short losses follow N12 (§11): precursor → blue no-signal screen → recovery, with durations randomised in the measured ranges. **There is always a tell before the picture goes.** Snow carries coloured impulse-dash texture, never flat monochrome noise. Terminal losses use the staged sequence or the hard cut, fired by events (impact, power loss, fiber break).
- **Correction (pilot):** "When it comes back there should never be AV PAL text over OSD. Only when it's blue." After a recovering dropout the picture and OSD come back **clean**. Receiver text belongs on the blue screen only.

**PR-5 Destruction detail, and it is physical.**
- "Windows, trenches etc. are fine, but they are of straight form (like window which is JUST a rectangle). Jagged chunks of brickwork jut out from the windows, and in some places, you might see a bit of rebar sticking out… in our simulator, every little texture detail will affect the physics."
- Openings carry broken edges (jagged brick, missing courses, protruding rebar) as **real geometry with collision**, not textures. The flyable opening is the **actual clear space**, not a rectangle drawn over it. So the gap model must handle openings that are neither rectangular nor upright (§10.6).
- Defects the pilot named in the first attempt: wall textures tiling into a chequerboard, and upside-down brick textures.

**PR-6 Power lines need their own strike physics, and thickness matters.**
- "if it flies into one… a drone getting tangled in thin wires behaves differently than it does with thick cables."
- Wires are physical lines, not static capsules.
  - **Thin wire** catches, wraps and tangles in the props, then snags and drags the drone or holds it.
  - **Thick cable** is stiffer and heavier: it deflects, stops or flips the drone rather than wrapping.
- Wires are catenary polylines in the map. They interact with the fiber tether, which is another line on the same aircraft.

**PR-7 Build objects from the footage, then research the real thing.** Construction, pre-war look and how structures fail when hit come from the footage notes plus online research of Ukrainian buildings and war damage. "An asset that passes its budget but does not read as rural Ukraine to this pilot is a failed asset."

**PR-8 The ground is not flat.**
- What a clip the pilot shared shows:
  - gently rolling ground: a low crest across the middle distance, shallow dips, the horizon above the base of the far treeline
  - pale **matted grass** with green showing through
  - bare, fine-twigged crowns and scattered scrub
  - low outbuildings
  - **concrete-slab tracks** across the fields
- The requirement is real relief, at tens-of-metres scale, everywhere. It matters for flight: crests hide what is behind them, dips change sight lines and where the wind separates, and a glide approach meets ground that isn't level.

**PR-9 Narrow fly-ins are sized to the drone: passable, barely.**
- "It should be a LITTLE BIT wider than the drone so it's possible to fly through yet narrow enough to be very difficult, especially since we'll have turbulence."
- The reference airframe measures **0.574 m prop tip to prop tip nose-first and 0.706 m diagonally**. The cellar doorway is therefore **0.70 m**: about 63 mm a side flying straight in, and passable up to about 35° of yaw.
- The turbulence funnelling through such openings is part of the difficulty.

**PR-10 Tree belts (посадки) carry occasional bushes among the trees**, through the whole belt and thicker at the sunlit edges. The trunk spacing (3 m in the build) stays.

**PR-11 Surface areas must never read as tiles.** Every surface area in a pilot-facing map has an **organic outline and a feathered transition** in both the visual and the physical layers (fields, yards, verges). Hard rectangles are acceptable only in test fixtures.

### 4.2 The aircraft, in the pilot's words (class level)
- **Legs** (pilot): "the legs are plastic, around 25-35cm, they are a little bit soft, that also SOMETIMES adds the stumbling effect when you land, that's why combat pilot try to land accurately and slowly, and correct their horizon right before they touch the ground."
- **Launch stand** (pilot): first described as rails "of the width that allows the drone to stay on it, like almost what the diameter of the drone is". On 2026-10-17 it became: "hiehght maybe 25cm. like a chair upside down, so each chair leg is beneath each motor. based this off of the expected size of our future svinoriz", with a photo of a typical stand (an X-shaped base with four posts, one under each arm near the motor). The spool hangs clear (D-022).
- **Batteries** (pilot): "different 20k-35k mAh, 6s4p, 6s5p +-" (Li-ion).
- **Fiber coil:** typically about 1 kg (class level).
- **Cargo:** 0.5–2 kg (class range).
- **Flight mode:** acro. **Camera uptilt:** 0°.
- **Motors and airframe specs:** research them from the named brands (§5).

### 4.3 Wind answers (from the severe-wind clip study, §8)

| # | Question | Pilot's answer |
|---|---|---|
| Q1 | Camera uptilt | 0° |
| Q2 | Wind direction and strength | "mostly it went from left, but it would randomly blow from other sides too. again - it's real life, unexpected and undefined". Strength unknown |
| Q3 | Flight mode | Acro |
| Q4 | Mass and props | A 10-inch heavy fiber quad of the reference class, with coil, cargo and battery in the class ranges. Sortie specifics are deliberately not recorded |
| Q5 | Height | Varied |
| Q6 | Duration | The clip was the toughest part of a longer flight |
| Q7 | Was the tree belt worse? | Yes: "either you yaw but lose direction OR you roll and keep direction, but lose horizon. that's why i decided to show you this short clip - that was the toughest piece of the whole flight" |
| Q8 | Cause of the picture losses | Unknown; the pilot was far from the drone. Their hypothesis: "the wind would shake the optic fiber which led to even more noisiness and flashiness. also a lot of trees + wobbling - this stretches opto" |

### 4.4 The video feed, in the pilot's words
- On the look (an early message): "note how the ref videos look very much like analog actually does - not JUST a noise, but also a bit edgy non tessellated pixel somewhere, lens distortion, and colors changing as you move closer to the object… the noises differ - sometimes noise, sometimes stripes, sometimes the image is fully noised for like microsecond etc. - analog filter should also be a part of realism simulation."
- Baseline noise is **always present**. The feed is never clean.
- Noise **may depend on throttle, but rarely**: a weak, occasional coupling only.
- **Mid-flight flashes happen at random and recover**, separately from a real loss.
- The loss sequence, in their words: "very quick picture glitch" → "brief flashy moment" → "noisy and flashy" → "finally blue". These are matched to frames in §11.3.
- **PAL only** (old D-015). **Svinoriz 10 Opto / ! SAFE !** (old D-014; spelling per the pilot, 2026-10-17).

### 4.5 Answers of 2026-10-17 (round 2)
The verbatim answers are in [pilot-brief-2026-10-17.md](pilot-brief-2026-10-17.md). The rule that came with them (D-024): **never ask the pilot what physics can calculate.**
- **Launch stand:** four posts about 25 cm high, one under each motor (D-022).
- **Rain on the lens:** drops sit on the lens **even in fast flight**, and sometimes travel with the airflow. This overrides clip P's "none seen" (D-021, FEED-13).
- **Rain is its own setting:** Sunny / Rain / Severe rain; night via time of day (D-021).
- **Rates and throttle:** the pilot's FpvPrep profiles: their flown set, leaning toward the sharper factory set, especially throttle; room for more Betaflight settings later (D-023, [reference-notes/fc-profiles.md](reference-notes/fc-profiles.md)).
- **Leg sticking:** sometimes, a little; more often on softer ground; most of the time in rain.
- **Settling into own wash:** "not a question of speed of descent but mostly how much throttle power hence pressure you give as opposed to drones weight. wobble - yes, of course".
- **Throttle-punch shake:** "doesnt happen much".
- **Speed in strong wind:** normal cruise; the effort goes into correcting course drift. So the low-drag reading applies (§8.2).
- **What cuts the fiber:** "overly sharp reverse, snagging on tress, very sharp lift off, or sudden momentum backwards after lift off".
- **Frame stutter, bounce height, yank frequency:** computable, so dropped.

### 4.6 The world, in the pilot's words
- **Terrain:** "typical посадки, домики, разрушенные из-за войны, машины, свойственные Украине растительность и поля" (tree belts, war-damaged houses, cars, Ukrainian vegetation and fields).
- **Map composition** (pilot):
  - diverse
  - **a couple of village houses with yards**, like the ones in the clips
  - **exactly one typical destroyed five-storey Stalin-era block**, like the photos
  - trenches are common and a terrain class of their own; earth banks are real
  - dark fields are both **tilled** and **burnt** (burnt by strikes, and by fallen FPV drones that explode)
- **Vehicles** (pilot): the makes **ВАЗ, Chery, Chevrolet, VW, Daewoo Lanos, ЗИЛ, Урал**. Some are **abandoned**, some **destroyed by strikes**. "REALISM".
- **Weather:** a fixed pilot choice of **Calm / Windy / Severe**, never random (D-011).
- **Editing:** the pilot will change the map later, so edits must be cheap.

---

## 5. The drone

`[portable]` throughout. The sim has **one** drone, **"Svinoriz 10 Opto"**, of the Vyriy / BOMBUS / OTU / Beshketnyk class: 10-inch, 3-blade props, a fiber spool underneath, and legs or the launch stand.

**Pilot, 2026-10-17:** "just google at typical opto fiber drones in ukraine and how they look (the coil underneath, a couple of batteries, etc, an explosive cargo on top of it, etc.)". So the payload sits **on top** and there are **two battery packs**. The mass layout below (cargo forward and below, one pack) is the first attempt's assumption, and phase 5 must re-derive it from research.

### 5.1 Published specs (public product pages)

| Airframe | Motors | Props | Frame | Mass / payload | Performance | Link |
|---|---|---|---|---|---|---|
| Vyriy (Вирій) 10, Opto build (same as "Амага 10 оптика") | Surpass Hobby Bat **S3115 900 KV**, 112 g each, 40 A max. A reseller's 4.2 kg peak thrust is optimistic | 10" | iFlight **Chimera CX10 ECO**, carbon, **452 mm wheelbase**, **383 g** (reseller figure) | Payload up to **1.8 kg** for 10 km (other listings say 2.5–4 kg) | Cruise **60–80 km/h** with 1.5 kg | Fiber, 10 km; camera CADDX Ratel 2 |
| OTU-10 | 4 electric motors (model not published) | **3-blade**, 10" | 10" | **Max take-off mass 5.35 kg ± 10 %**; payload up to **2 kg** | **12 min** with payload; up to **80 km/h**; up to **50 m** altitude | Fiber, 10 km |
| BOMBUS 10-O | not published | 10" | 10" | not published | not published | Fiber (WireDO), **10 km spool, 0.25 mm** |
| Beshketnyk (Бешкетник) | no public specs found | | | | | |
| Generic fiber module | | | | **20 km ≈ 1.75 kg** (0.27 mm fiber, with air unit) | | |

Sources used in the first attempt: dron-shop.com.ua (VYRIY 10), magura.in.ua ("Амага 10 оптика"), shop.iflight.com (Chimera CX10 ECO), oyostepper.com (Surpass S3115), otu-tech.com.ua/otu10, drones.com.ua (BOMBUS 10-O), youdrone.com.ua (fiber module).

### 5.2 Mass build (derived; nominal 4.8 kg)

| Item | Mass | Label |
|---|---|---|
| Frame, 452 mm wheelbase | 0.38 kg | reseller figure |
| 4 motors, 3115, 900 Kv, 40 A | 4 × 0.112 = 0.45 kg | published |
| ESC, FC, camera, fiber transceiver, wiring, 4 × 3-blade 10" props, legs | 0.35–0.5 kg | assumed |
| Battery, 6S Li-ion 20–25 Ah (6S4P–6S5P; 21700 cells of ≈ 69 g, + 5 %) | 1.7–2.2 kg | derived |
| Battery, 35 Ah (≈ 6S7P) | ≈ 3.0 kg | derived. Take-off stays ≤ 6.5 kg only with ≤ 1.2 kg of cargo |
| Fiber coil at take-off | ≈ 1.0 kg | class level; ≈ 10 km by the module figure |
| Cargo | 0.5–2 kg, typically ≈ 1 kg | class range; the typical value is assumed |
| **Take-off** | **4.4–6.5 kg**, typically **4.9–5.5 kg** | derived; it brackets OTU's published 5.35 kg ± 10 % |
| Coil mid-flight | 0.2–1.0 kg (an empty spool with housing is ≥ 0.2 kg) | assumed |
| **In flight** | **3.6–6.5 kg**, nominal **4.8 kg** (coil half paid out, cargo aboard) | derived |

- The mass falls by up to about 1 kg over a flight as the fiber pays out. The battery's mass doesn't change.
- **Assumed layout** (x forward, z up, origin at the arm-plane centre):
  - motors with props: 4 × 130 g on the diagonals, 0.226 m out, 2 cm above the arm plane
  - arms: 4 × 60 g rods from 3 to 22.6 cm out
  - plates: 143 g, as a 16 × 10 × 3 cm box
  - legs: 4 × 25–35 g rods hanging 30 cm, 12 cm from the centre
  - electronics: 0.18–0.29 kg, as a 16 × 10 × 6 cm box 1 cm above the arm plane
  - battery: a 14 × 9–11 × 7 cm box on top, centred 5.5 cm above
  - coil: a solid drum 14 cm across and 8 cm tall, centred 10 cm below
  - cargo: a 10 × 10 × 12 cm box, centred 10 cm forward and 8 cm below
- **Inertia** (a parallel-axis sum over that layout, at seven loadouts from 3.6 to 6.5 kg):
  - roll **0.033–0.057**, pitch **0.040–0.072** and yaw **0.043–0.060 kg·m²**; at the nominal 4.8 kg, **0.044 / 0.054 / 0.050**
  - radii of gyration ≈ 0.093–0.110 m
  - **yaw is only 1.0–1.3 × roll**: the battery above and the coil below add roll and pitch inertia, but little yaw
  - **pitch exceeds roll** because the cargo sits forward
- **Centre of mass:** with a full coil it sits 1–2 cm below the arm plane and 1–3 cm forward, and the coil's centre is **8–9 cm below the CoM**. So **a side gust on the coil is also a rolling moment**. With an empty coil the CoM rises to about 1 cm above the arm plane.

### 5.3 Thrust, margin and authority (derived)
- **Thrust law:** per motor, T = (FM · η · V · I · √(2ρA))^(2/3), with A = **0.0507 m²** for a 10" disc. With fixed pitch, T = k_T·n² and rpm ∝ throttle × pack voltage, so T ≈ T_full(V)·u².
- **Assumed values:**
  - FM 0.55–0.65 (static, 10" 3-blade) and η 0.80–0.85
  - 40 A as the realistic maximum current
  - loaded pack voltage V = 6·(OCV − I_cell·R_cell), with OCV 3.4–4.1 V per cell, **R_cell ≈ 15 mΩ**, and I_cell 40 A (4P) or 32 A (5P), giving 16.8–21.7 V
- **Maximum thrust:** a realistic **2.3–3.1 kgf per motor (nominal 2.6)**, or **9.0–12.5 kgf in total**. That is a thrust-to-weight of 1.4–2.8 at take-off, and 2.0 for a typical 5.2 kg.
- **Fast flight:** axial inflow derates the thrust to 0.80–0.95 (nominal 0.90 at 13 m/s) (assumed).
- **Margin in the severe-wind clip's flight state:**
  - the need was 1.10 W, which is **33–99 % of what was available (nominal 56 %)**
  - that is a **thrust margin of 1.0–3.0 (nominal 1.8)**, at about **three-quarter throttle**
  - the heavy corner (6.5 kg on a sagged pack) has no margin left
  - "Motors crying" fits: high rpm plus fast differential changes
- **Geometry:** the motors sit **0.160 m from the roll and pitch axes** (452 mm X frame).
- **Roll and pitch need little:** 1.0–1.5 % differential thrust covers the 95th percentile of the measured angular acceleration (151 °/s² in roll, 97 °/s² in pitch). There is no sign of saturation.
- **Yaw is the weak axis:**
  - rotor drag torque κ = 0.015–0.02 N·m per N of thrust (assumed)
  - yaw needs **5.5–10 % differential thrust at the 95th percentile and 21–38 % at the maximum**, 4–8× the roll figure
  - it saturates once the collective passes ≈ 72 %, which the heavy corner reaches
  - the mixer must saturate honestly (clip, then desaturate), or the model will feel more authoritative than the real drone

### 5.4 Geometry for gaps and the launch stand
- **Prop tip to prop tip:** **0.574 m nose-first and 0.706 m on the diagonal** (452 mm wheelbase plus 10" props). The earlier round figure was "about 0.65 m across".
- **Launch stand** (D-022, the pilot's 2026-10-17 description):
  - an X-shaped steel base with **four posts about 0.25 m high**, one under each motor, on the diagonals 0.226 m from the centre
  - the drone rests on its arms just inboard of the motors; four hard point contacts
  - the coil hangs clear
  - the stand is unanchored
  - (The first attempt's "two bars 0.26 m apart" is superseded.)
- **Flight controller and battery** of the real class: Betaflight 4.5, a 4 kHz PID loop, DShot300 bidirectional with the RPM filter, **8S Li-ion** on the OTU batch. See [reference-notes/fc-profiles.md](reference-notes/fc-profiles.md).
- **Legs:** plastic, 25–35 cm, slightly soft (pilot); four of them, about 12 cm from the centre (assumed).

---

## 6. Flight model architecture (designed, not built)

`[portable]`. This section condenses the physics engineer's draft, [legacy/flight-model/design.md](legacy/flight-model/design.md), which has seven specs with scenarios in [legacy/flight-model/specs/](legacy/flight-model/specs/). The draft predates PR-1 to PR-11, and §6.12 lists what it is missing.

### 6.1 The central claim
- Every other sim "claims good physics" and delivers something smooth and rigid. The difference is not more force terms. **The disturbances must be physical and seeded**: a motor that is genuinely 1.5 % hotter than its neighbour, or a leg that genuinely caught on a straw with a known release force.
- Noise added to the pose passes for about ten seconds and then feels wrong forever, because it doesn't respond to what the pilot does about it. Wind target T11b (§8.6) exists to fail a model that cheats this way.
- The goals:
  - each of the manifesto's six complaints becomes a requirement with a scenario QA can run
  - every disturbance is a force or a moment with a cause
  - a flight replays bit-identically from its log, without the engine
  - the model holds ≥ 1 kHz with the full world loaded
- Non-goals: blade-element aerodynamics, CFD and flexible airframes. The fidelity ceiling is momentum theory with correction factors plus a wake model on a 2 m grid.

### 6.2 Frames, state and the step
- **World frame.** It must match the world query. The first attempt used right-handed Y-up (+X east, +Z south). For the restart, see the Unity decision D-019.
- **Body origin.** At the arm-plane centre, not at the CoM, because the CoM moves as the coil pays out.
- **Attitude** is a unit quaternion. Euler angles exist only for logging and for the simulated pilot, in ZYX order.
- **State:** position and velocity; orientation; body rates; per-rotor speeds; ESC filter states; pack state of charge; per-leg plastic sink and stick anchors; hooks by element `Id`; spool paid-out length; tether node positions and velocities.
- **Integrator:** semi-implicit (symplectic) Euler at a fixed **dt = 1 ms**.
  - It is stable for every soil branch (ω·dt ≤ 0.10) and for the stiffest body contact, the frame on steel rails at ~1e6 N/m (ω·dt ≤ 0.7).
  - RK4 was rejected: four force evaluations means four sets of world queries, and it doesn't help stiff contacts.
  - There is no sub-stepping. Stiffness goes in series with the drone's own compliance instead.
- **Rotor speeds** use an exact first-order update with no transcendental functions.
- **Threading:** the model runs on its own thread and publishes a double-buffered pose, which the renderer interpolates. The engine never integrates the drone.
  - If the model falls behind, it logs a `hitch` and catches up with real steps. It never takes a longer step.

### 6.3 Mass and settings
- **What the settings touch:** the pilot sets payload, legs, time of day and weather. Only payload and legs touch the flight model, and weather drives the wind.
- **Mass and inertia** are built from the component model (§5.2) with the parallel-axis theorem. The build re-runs whenever a part's mass changes: payload set, coil paid out, cargo released.
- **The asymmetry is not cosmetic:** pitch inertia exceeds roll, and yaw is only 1.0–1.3 × roll. That is the inertia half of manifesto point 4.
- **New pilot input (2026-10-17):** the payload sits **on top**, and the drone carries **"a couple of batteries"**. The layout in §5.2 (cargo forward and below, one pack) must be re-derived from research of typical Ukrainian fiber quads in phase 5.

### 6.4 Propulsion chain
The chain is **sticks → rate controller → mixer → ESC → motor → rotor → thrust and torque**. Asymmetry is injected at the motor, so the controller partly fights it and never fully cancels it. That is the pilot's manifesto point 4.

- **Rate controller:** acro only, with a Betaflight-class PID per axis, a D-term low-pass, an I-term windup limit, and no self-levelling. The gains are assumed (O-9).
- **Mixer:** a standard X quad. It saturates honestly (clip, then desaturate the collective). Yaw saturates first (§5.3).
- **ESC and motor lag:** an ESC command filter, then the motor–rotor time constant τ_m. This lag is why a heavy quad feels "behind the sticks", and why asymmetry reads as a slow wander rather than a buzz (O-2).
- **Thrust and torque:**
  - T = k_T·n², reaching 2.3–3.1 kgf per motor at the maximum
  - Q = κ·T, with κ = 0.015–0.02
  - an inflow derate of 0.80–0.95 at speed
  - A = 0.0507 m²
- **Battery sag:** V = 6·(OCV − I·R), with R_cell ≈ 15 mΩ, and the state of charge integrated over the flight. On a sagged pack at the heavy corner, a full-collective pull to arrest a descent finds less thrust than the pilot expected.
- **Ground effect:** per rotor, T/T∞ = 1/(1 − (R/4z)²), clamped, with z from a per-rotor downward ray. It is negligible above z/R ≈ 2. Because it is per rotor, a drone over a step or rubble gets more lift on one side, which rolls it. That is part of the liftoff shake. Ceiling effect under roofs uses the same ray pointed upward.
- **Vortex ring state:** a thrust multiplier plus added unsteadiness over a band of descent rate normalised by induced velocity, with hysteresis (O-6). A straight vertical descent with cargo provokes it.
- **Prop wash recirculation near the ground:** a small inflow perturbation per rotor from the other three, scaled by ground proximity. It is one reason a close hover is never still.
- **Motor asynchrony:** per-motor Kv spread, ESC timing jitter and prop imbalance, drawn once per flight from the seed and bounded (O-1).
  - The target: with wind off on a flat world, the drone drifts and needs correction at a rate inside a band.
  - With asymmetry disabled, the drone must be visibly steadier.
  - It must never produce a buzz or a divergence.

### 6.5 Airframe aerodynamics
Rotor forces alone can't produce the pilot's horizon–heading trade-off (§8.4). The model therefore carries:
- **Body drag** on the relative wind (velocity minus local wind), with separate along and across areas
- **Coil drag** at the coil, **8–9 cm below the CoM**, so a side gust rolls the drone into the wind before the pilot touches anything
- **Rotor H-force**, which grows with airspeed
- **Weathervaning**: the offset between the centre of pressure and the CoM yaws the drone into the relative wind
- **Calibration:** about **1.8° (0.95–2.6°) of lean per m/s of crosswind** at the derived airspeed. Drag areas are assumed and tuned to hit it (O-3).

### 6.6 Legs, launch stand, bounce and stick
- **Legs:** plastic, 25–35 cm, slightly soft (pilot). Each is a spring–damper in series with the ground, so stiff soil never enters the step directly. Stiffness and damping are assumed (O-4).
- **Landing bounce** (manifesto point 6): it falls out of leg compliance plus residual thrust, with nothing scripted. The test is the pilot-visible consequence: **hold the sticks and the drone leaves the ground; cut the throttle and it stays**, with a measured separation. A probe (a 10 kg drone on the rails at 0.5 m/s with 0.95 W held) bounced 157 mm.
- **Leg sticking at liftoff** (manifesto point 1), from two physical causes:
  - a **hook**: a straw or twig with `Hooks` and a `HookRelease` force, attached by element `Id`
  - **stiction** in soil the foot has sunk into: the anchor holds up to μs·Fn, plus ploughing

  The consequence: collective rises, three feet leave, one is held, and the drone pivots about it. The horizon tips until the foot releases. The **effective weight changes** as it unsticks, because part of the weight is still on the ground; once released, the drone accelerates up more than the pilot asked for.
- **Launch stand** (legs off): four steel post tops (§5.4) as ordinary contacts with the `steel` material. An arm can catch or slide on a post, and the unanchored stand can tip or drag.
- **Non-leg contacts:** arms, body, coil and prop discs are swept capsules. An element entering a prop disc is a logged strike.

### 6.7 Wind
Three layers, each sampled bilinearly at the rotors, the body and the tether nodes:
1. **Mean profile:** a log law with z0 ≈ 0.1·h_cover and d ≈ 0.67·h_cover. Over bare ground, z0 comes from the relief.
2. **Turbulence:** seeded broadband Dryden/von Kármán, with σ_v/U ≈ 0.12–0.15, L_u = L_v ≈ 115–200 m and L_w ≈ the height. Energy must reach **2–4 Hz** at encounter frequency.
3. **Obstacle wakes:** precomputed per wind direction on the 2 m grid by marching upwind (up to 30 H, capped at 300 m) to the nearest obstacle (x, H, base B, path porosity β).
   - The deficit and the turbulence boost are functions of x/H, z/H and β.
   - Porous-fence behaviour applies for β > 0.3, and bluff-body recirculation for β < 0.1 (reverse flow within 2–3 H).
   - Speed-up over the top at about 1.2 H.
   - A jet under crowns that have a base, attenuation inside a canopy, and jets through gaps.
   - The precompute runs on a worker when the direction changes by more than 5° or the window moves.

**PR-3 adds** vorticity in the field, especially in wakes, and per-rotor gust gradients, so a gust front crossing the drone gives coupled pitch-and-yaw events. Per-flight draws and the weather levels are in §8.6.

### 6.8 Fiber tether
- **Spool:** pays out when the span tension exceeds its payout threshold, and **never takes line back**. That is why sharp reverse is dangerous: the line has nowhere to go, so it loops, snags or loads up.
- **Span:** at most 32 lumped nodes between the drone and the launch point, carrying gravity, wind drag and contact with the ground and vegetation. Twigs act as capstans of radius d/2, and straws hook the line.
- **Drag on the airframe:** the span tension acts at the spool exit, below the CoM, so a loaded line both pulls and rolls the drone. A hard turn loads the line, and the load answers back.
- **Mass:** leaves the airframe as line pays out.
- **Bend:** over edges, using the catalog material's `edge_radius_m`. Glass stress is E·r/R: about 12.6 GPa over a 0.3 mm sheet edge, 4.3 GPa over 1 mm, 2.2 GPa over 2 mm and 0.75 GPa over 6 mm. The effective radius is max(R_edge + coating, √(EI/T)).
- **Cutting is a risk, never a certainty.** Three mechanisms (tension over breaking load, bend radius below minimum, accumulated abrasion) are each compared against a **per-flight strength drawn from the seed**. The mechanism is deterministic; the line varies.
- **Rain** adds water mass and drag to the span (assumed).
- **Wedging:** a geometric wedge resolves as a tension spike (a real outcome), never a NaN.
- **Link signals** go to the feed (§11.8): tension at the spool exit, minimum bend radius, whether the span touches vegetation, span vibration from crosswind, and link state and margin.
- **Known data:** a 10 km spool of 0.25 mm fiber; about 1 kg ≈ 10 km; a 20 km module of 0.27 mm fiber weighs 1.75 kg. Breaking load, minimum bend radius, payout tension and abrasion rate are **unsourced** (O-5).

### 6.9 Controlled randomness
> Randomness enters **only** as a bounded perturbation of a named physical parameter, drawn from a seeded stream and written to the log. It never enters as a force, a moment, a pose offset or a filter on the output.

Why this rule:
- a perturbed parameter **responds to the pilot**: a hot motor's yaw bias grows with throttle, so the correction that works at hover is wrong in a climb
- it is **explicable**: the log says "motor 3 is 1.4 % hot"
- it is **replayable**

The seed architecture:
- one 64-bit flight seed, shown to the pilot and logged
- named streams derived by hashing (seed, stream name), so adding a stream never shifts the others
- **per-flight draws** at arm time, written to the header: Kv spread, ESC offsets, prop imbalance, leg stiffness spread, fiber strength, and the wind's prevailing direction and strength
- **evolving streams:** turbulence, gust arrival, payout irregularity
- **world-derived, not drawn:** everything about the ground and cover comes from the map seed through the world query, so the same landing spot behaves the same way every time. "The ground is not random, it is *unknown*, and the pilot learns it."

### 6.10 Determinism, logging and replay
- **Determinism:**
  - only correctly rounded IEEE operations (+ − × ÷ √) in the flight loop
  - no `Sin/Cos/Exp/Pow` from the runtime and no implicit FMA; use polynomial or rational approximations in the project's own `DetMath`
  - micro-detail prefetch is requested at a step chosen from state and swapped in at a fixed later step; a late worker makes the flight thread **wait** and logs a hitch, never changing a result
  - hook state is keyed by element `Id`
  - the header records the world content hash, `QueryVersion`, runtime, build and ISA flags, and replay refuses a mismatch
  - runtime objects (the launch stand) are logged as asset, pose and order
- **Log:** one file with a header, then fixed-layout binary records:
  - **full state at 1 kHz**: integrated state, per-rotor commands and speeds, pack V and I, net force and moment
  - **events at step resolution**: contacts made and broken (surface, material, `FeatureId`), hook attach and release (element `Id`), pitfall entries, tunnel events, prop strikes, mixer saturation, VRS entry and exit, payout, tether break with its mechanism, hitches, and impacts with peak acceleration and impulse
  - writes go through a ring buffer and a worker flush, within a stated budget per minute
- **Replay:** a headless tool reproduces every logged state bit-for-bit **without the engine**. This is the strongest available test: it fails if any draw is unlogged, any state uninitialised, any transcendental sneaks in, or anything reads the scene.
- **Forensics:** the log must answer, without guessing:
  - which part touched what, at what speed and with what impulse
  - whether a motor was saturated and whether the pack had sagged
  - which hook or pitfall was involved
  - whether the tether was loaded or broken

  A crash is decided in the 50 ms before it.

### 6.11 Performance plan
- **The world-query share** of a worst-case step (44 ground samples, 46 swept capsules, 8 rays) measured **34.7 µs median** (p99 72.9 µs) against a 60 µs budget, in plain C#.
- Micro-detail is prefetched on a worker at ≤ 40 Hz and never enters a step. The wake precompute also runs on a worker.
- Zero managed allocation in steady state.
- Measured on AC power, with the clock and power mode printed.

### 6.12 What the draft is missing
- **PR-1:** the throttle-punch shake and the camera-shake signal to the feed.
- **PR-3:** vorticity and per-rotor gust gradients.
- **PR-6:** wire strikes by diameter (thin wraps, thick deflects).
- **PR-9:** gap sizing against prop tips.
- **D-016:** the input-timing contract.
- The 2026-10-17 drone layout: payload on top, two batteries.
- The physics → feed interface (the signals in §11.8).

---

## 7. Ground contact and materials

`[portable]`. The full text is in [legacy/map-and-world/surfaces-review.md](legacy/map-and-world/surfaces-review.md) §1–§6, and the values in [legacy/map-and-world/surfaces.json](legacy/map-and-world/surfaces.json).

### 7.1 The leg–ground solve (per leg, per step; closed form)
1. **Sample** the ground at the foot: ground height Hg (terrain + relief + ridges + pitfalls), normal n, per-kind mat depths, support top Hs = Hg + ΣDmat, and blend weights.
2. **Penetration:** δ = (Hs − p.y)·n.y. At or below zero there is no ground force, though stems can still act.
3. **Soil:** elasto-plastic Winkler (Wong's load/unload rule).
   - Virgin loading: kl = bearing·(b_ref/b)^0.3, with b_ref = 15 mm.
   - Unload and reload: ku = unload_ratio·kl from a per-leg plastic set zp, so p = min(ku(δs − zp), kl·δs).
   - Past `max_sink`, a firm layer at 1e8 N/m³ takes over.
   - Viscous damping applies, and the force is clamped at ≥ 0.
4. **Mat** (straw, thatch, leaf litter) in series at equal force.
   - The load spreads over Am = π(b/2 + D/2)², so km = modulus·Am/D.
   - It compresses to 80 % of D, then acts as a solid.
   - Damping is 2ζ√(km·m_eff).
   - At most 3 piecewise-linear branches.
5. **Friction:** a stick-point (bristle) anchor with tangential stiffness 0.5 × the normal stiffness. It holds up to μs·Fn and slides at μk·Fn. μ blends from the soil's value to the mat's over the first 10 mm of mat.
6. **Ploughing:** a sunk foot pushes soil, Fp = kl·b·δs² against its lateral velocity. This is what trips a drone landing on soft ground with forward speed.
7. **Footprints:** zp belongs to the footprint, and a ring of recent prints makes a re-landing find compacted soil. The world stores no marks.
8. **The leg** is a drone-side spring–damper in series. It dominates the bounce on hard ground.

The stiffest soil branch at the reference foot is 26.5 kN/m, so ω·dt ≤ 0.10 at 1 kHz.

### 7.2 The surface table (starting values)
Reference foot: 25 N on a 15 mm foot. "Sink" is the static sink.

| id | bearing N/m³ | damping N·s/m³ | μs / μk | max sink | unload ratio | micro-relief amplitude @ wavelength | pitfalls /m², depth, radius | cover | foot k N/m, sink |
|---|---|---|---|---|---|---|---|---|---|
| meadow_sod | 7.0e6 | 3.7e5 | 0.55/0.45 | 0.05 | 3 | 0.02 @ 0.4 | 0.01; 0.05–0.3; 0.05–1.0 | grass 0.2–0.6 m, 400/m², mat 1–4 cm | 1237, 20 mm |
| dry_crust | 5.0e7 | 2.5e5 | 0.7/0.6 | 0.01 | 1.5 | 0.01 @ 0.8 | 0.004; 0.03–0.15; 0.05–0.3 | sparse grass 40/m² | 8836, 2.8 mm (bounce) |
| crater_spoil | 2.8e6 | 3.2e5 | 0.8/0.65 | 0.12 | 8 | 0.05 @ 0.5 | 0.02; 0.05–0.2; 0.1–0.4 | none | 495, 51 mm |
| belt_straw | 4.7e6 | 1.8e5 | 0.5/0.4 | 0.08 | 5 | 0.04 @ 0.7 | 0.02; 0.1–0.3; 0.05–0.3 | straw 250/m², mat 5–20 cm, hook p 0.15 at 1–5 N; grass; twigs | 831, 30 mm |
| belt_bare | 1.4e7 | 3.5e5 | 0.5/0.4 | 0.03 | 3 | 0.015 @ 1.0 | 0.005 | twigs, litter | 2474, 10 mm |
| tilled | 4.0e6 | 3.3e5 | 0.75/0.6 | 0.10 | 8 | 0.02 @ 0.3, plus ridges 0.05 @ 0.5 m spacing | 0.01 | straw stubble | 707, 35 mm |
| yard_litter | 3.5e7 | 4.2e5 | 0.6/0.5 | 0.015 | 2 | 0.015 @ 0.3 | 0.005 | leaf mat 1–3 cm at μ 0.35/0.25; twigs | 6185, 4 mm |
| rubble | 1.0e8 | 2.4e5 | 0.7/0.6 | 0.005 | 1.5 | 0.12 @ 0.6 | 0.05; 0.1–0.4 | jutting timbers, hook at 10–60 N | 17671, 1.4 mm |
| weeds | 1.4e7 | 4.4e5 | 0.6/0.5 | 0.04 | 3 | 0.03 @ 0.5 | 0.02 | stalks 0.5–1.5 m, 5–15 mm, hook at 5–30 N | 2474, 10 mm |
| burnt_field | 2.5e7 | 3.6e5 | 0.6/0.5 | 0.02 | 2 | 0.015 @ 0.6 | 0.006 | charred stubble, stiff and brittle; ash mat | — |

Missing and still needed: **asphalt** (rigid, slides on glass crumbs), **concrete-slab track**, and a **dirt track** with ruts.

### 7.3 Stems, twigs, hooks and props
- **Standing grass and weeds** are rooted cantilevers. A push-through d at height h gives F = 3EI/h³·d, with EI = k_tip·L³/3. The force is capped at slide-off and at buckling, π²EI/(4L²). This gives touchdown jitter, drag when skimming, and the fiber dragging through stalks.
- **Lying straw** is mostly hooking; its bulk is the mat.
- **Twigs** under a foot are rigid rollers (μ ≈ 0.05 across their axis). For the fiber, they are capstans of radius d/2.
- **Litter** is visual only; its mat carries the physics.
- **Hooks:** a `Hooks` element touching a leg or the fiber attaches at the contact. It pulls back toward its base with a spring reaching `HookRelease` over 30 mm, then lets go. It is deterministic, keyed by `Id` and logged.
  - **Sizing:** 5 N at a 0.3 m arm, held for 50–200 ms, tilts the drone 1–4° and leaves a roll rate to catch. Weeds and rubble timbers can hold harder than the excess thrust, so the drone flips.
  - **Measured rate** on flat belt_straw: about 2 straws touch each leg, and **21.5 % of landing legs hook**, about 0.9 legs per liftoff. That may read as "every time"; tune it with the pilot (O-7).
- **Props:** any element entering a prop disc is a logged strike.
- **Steep ground under one foot:** crater_spoil 31–42°, rubble 21–37°, yard litter up to 24° (against μs 0.35). Expect feet to slide; check this in the first flights.

### 7.4 Object materials (catalog)

| id | μs / μk | stiffness | ζ | edge radius | Use |
|---|---|---|---|---|---|
| steel | 0.35/0.28 | rigid | — | 1 mm | rails, bars, gates, posts |
| sheet_metal | 0.30/0.25 | 2e4 N/m | 0.03 | 0.3 mm (cuts fiber) | corrugated roofing; drums and bounces when landed on |
| timber | 0.45/0.35 | rigid (battens ≈ 1e5 later) | — | 2 mm | planks, beams, poles, trunks |
| masonry | 0.65/0.55 | rigid | — | 2 mm | brick, block, concrete, render |
| cable | 0.30/0.25 | rigid locally | — | own radius | wires |

- There is **no restitution field**. On rigid materials the drone's own compliance sets the bounce.
- **Wire compliance** comes from span tension, T = w·L²/(8·sag). For example, a 12 mm wire on a 30 m span with 0.6 m sag gives T ≈ 416 N, about 55 N/m at mid-span.
- **Torn metal is sharp,** so a fiber dragged across it is more likely to cut than on branches.

### 7.5 Swept contacts: the Time < 1 rule
- **Latch:** when a swept contact first touches during a step (`Time` < 1), **don't rewind or sub-step**. Latch the contact plane (point, normal) at the first touch and push back against it, with the part's compliance in series with the material.
- **Keep or refresh:** a later `Time` = 1 contact with a same-side normal refreshes the plane. A flipped normal keeps the latch. (Probe: the step after a pass-through reported the far-side normal; trusting it would push the part on through.)
- **Release:** when the plane distance exceeds the margin, or the pair is absent from the result.
- **Tunnel event:** if the part ends up through the object, log a `tunnel` event. That is **a physics bug, never pilot error.**
- **Grazes:** a `Time` < 1 contact that the latched plane shows as non-penetrating creates no force and no impact record.
- **Why not rewind:** the impact moves by less than 1 ms, and first-step depth is ≤ 20 mm at 20 m/s. A rewind would make the step's cost unbounded.

---

## 8. Wind, turbulence and rain

`[portable]`. The full study is [reference-notes/wind.md](reference-notes/wind.md) (about 110 KB, frame by frame). It measures one clip, **P**: 47 s of straight forward flight in severe wind and pouring rain, over open stubble fields with one tree-belt crossing. It was the pilot's chosen target: "constant and dynamic physics along with motors crying", never "wind goes south so my quad goes south smoothly".

### 8.1 Read this first
- **Every number is closed-loop.** It is the attitude *after* the pilot and the flight controller reacted, in acro mode. The true disturbance is larger, most of all below 1 Hz (1.1–2× at roll).
- So **targets are graded with a simulated pilot** flying the sim (§8.5), never on the open-loop gust response.
- The steady part of the wind shows only as the lean the pilot held.

### 8.2 Key measurements (clip P)
- **Steady lean:** roll −9.1° into the wind and pitch −22.7° (forward), with only about −5° of net heading change. A steady sideways force, not a turn.
- **Thrust:** 1.10 W on average. Its horizontal part is 0.453 W, pointing 22.6° left of the nose. The sideways part never fell below 0.034 W, so the crosswind never reversed in 47 s.
- **Derived airspeed and wind:** an **airspeed of about 13 m/s (9–25)** and a **mean crosswind of about 5 m/s (2.5–11.5)**. Each extra 1 m/s of crosswind needs **1.8° (0.95–2.6°) more lean**. **The pilot (2026-10-17): wind is flown at normal cruise speed.** So the published cruise of 17–22 m/s applies with low drag, and the crosswind is about **6.5–8.5 m/s**.
- **Pilot correction bandwidth:** decay to 1/e in **0.30 s for roll, 0.17 s for pitch and 0.27 s for yaw**, which is **0.53 Hz, 0.94 Hz and 0.59 Hz**. Excursions are 0.3–0.6 s pulses, not steps followed by slow corrections.
- **Open field against tree belt:**

  | | roll residual | pitch residual | yaw residual |
  |---|---|---|---|
  | open field | 0.51° | 0.28° | 0.44° |
  | belt, 7 s | 1.26° (×2.5) | 0.43° (×1.6) | 1.67° (×3.8) |

  - **Every one of the 7 roll events beyond 2σ fell over the belt.** The drone rocked 3–5° every 1.3–2.9 s.
  - The belt's eddies are 2.5–11 m across and 5–20 m apart, the size of the trees.
  - Shelterbelt literature reports about 2× turbulence intensity from 5 to 15 belt heights downwind, in a wake growing to 2–3 heights tall.
- **Spatial intermittency:** over open field the wobble is steady and never goes calm (every 5 s block is 0.65–1.20× its level). Open-field roll is as Gaussian as stationary noise (kurtosis 2.51). Open-field pitch comes in isolated jolts (kurtosis 3.85, about 10 per minute). The whole clip's heavy tails are the mix of the two regimes.
- **Spectrum:** 94 % of roll variance lies below 1 Hz. Pitch has 32 % of its variance at 1–4 Hz, and roll 0.11° RMS at 2–4 Hz (the "never smooth" floor).
- **Picture losses:** three blue-outs of 0.40–0.67 s and one black frame, all recovered, all over open field 12–27 s after the belt, and none over the belt.

### 8.3 What the wind model must do
1. **A steady crosswind per flight.** Each flight draws its own prevailing direction and mean strength, and the direction then changes at random. **Never a fixed vector.**
2. **Broadband background turbulence** that reaches the airframe up to 2–4 Hz. Under the simulated pilot it leaves about 0.5° of roll and 0.3° of pitch wobble over open field.
3. **Wakes behind tree belts and buildings** that multiply that wobble 1.6–4× and deliver a burst every 0.6–0.8 s. These are the places where the drone "yanks".
4. **Gusts as real sideways forces**, through drag on the body, coil and rotors, and not only as moments. This forces the horizon–heading trade-off (§8.4).
5. **Yaw authority as the weak link.**
6. **A fiber tension, bend and vibration driver** for the picture losses (§11, N12).

**The direction-change model** (starting values, tunable):
- **Prevailing direction:** drawn per flight, uniform over the compass.
- **Meander:** an Ornstein–Uhlenbeck walk (τ = 2 min) smoothed by a 30 s lag, with σ = 30° and at most about 0.5 °/s RMS.
- **Rare shifts:** 45–120°, either way, ramping over 10–30 s, about **1 per 5 min** (Poisson), each relaxing back toward the prevailing direction.
- **Local reversals** come from the wake model, near belts and buildings.
- This model passes T0c in all of 1000 seed sets. Shifts on a fixed schedule fail it.

### 8.4 The horizon–heading trade-off (the pilot's "either you yaw but lose direction, or you roll and keep direction but lose the horizon")
- With 0° uptilt and nose down by θ, rolling about the body axis never changes the camera heading, but yawing rolls the horizon: φ̇ = p + ψ̇·sin θ (exact ZYX).
- Only the roll stick tilts the thrust sideways. For small changes, the thrust's sideways tilt is **e = φ + k·ψ, with k = −sin θ ≈ 0.386** at −22.7°.
- Holding a line against a crosswind needs a fixed e, and the pilot must split it:
  - **roll and keep direction:** the horizon swings with every gust
  - **yaw and keep the horizon:** the heading swings 2.6° per degree of tilt; P's 9.1° lean held with a level horizon would need about 25° of crab
  - **hold both:** the drone drifts off its line
- **Measured split:** over the belt, 64 % of the tilt went on the horizon and 36 % on the heading. Over the field, 80 % and 20 %. Roll and yaw rate correlate **+0.77 over the belt** and +0.34 over the field. Yaw follows roll by 2 frames.
- **This can't be faked.** It needs sideways gusts that are real forces, 0° uptilt on a forward-pitched drone, and exact rate-mode kinematics.

### 8.5 The simulated pilot and the acceptance procedure
- **Simulated pilot:**
  - rate mode
  - sees the sim camera at 29.917 Hz with a **0.10 s reaction delay**
  - wants a tilt e* = ē − v/(g·τ_v), with τ_v = 0.5 s
  - splits it with s = 0.64: φ_ref = ē + s(e* − ē) and ψ_ref = ψ_course + (1 − s)(e* − ē)/k
  - holds pitch at −22.7° ± 2°
  - on each axis, commands the rate (ref − angle)/τ, with τ = 0.30 / 0.17 / 0.27 s
  - converts to body rates with the exact ZYX relations
  - holds height through a 2 s loop
  - variants: d = 0 and 0.2 s; τ_v = 0.3 and 1.0 s; s = 1 and 0 (the last pair for T11b only)
- **Test flight:**
  - 4.8 kg, with inertia 0.044 / 0.054 / 0.050
  - a straight course at 20–50 m: **32 s over open field plus 7 s** from the belt's upwind edge to 10 belt heights downwind
  - at least 5 seeds, discarding the first 5 s
  - each run's course laid with the drawn wind abeam from the left, starting where the seed's wind stays within 90° of the drawn direction for the whole run
- **Measurement:** log roll, pitch and yaw rate in the tracker's definitions at 29.917 Hz, then run the old `tools/reference/wind_stats.py` (a Blender Python script) over them.
- **Wind history (T0c):** at least **40 seeds × 120 min** at ≥ 1 Hz. Shorter histories fail a correct model.

### 8.6 Weather levels and targets
- **Levels:** the pilot picks **Calm, Windy or Severe** (D-011), and the level is never random.
- **Fixed by the level:** the strength band (centre ± 25 %), turbulence σ/U and scales, the wakes, and the direction-change parameters.
- **Drawn per flight from its seed:** the prevailing direction (uniform) and the mean strength (uniform in the band), both logged.
- **Level centres:** Severe is tuned until T0 passes. Windy is c = 0.5 of it (about 2.5 m/s of mean crosswind) and Calm c = 0.15 (about 0.8 m/s). The bands never overlap.
- **Calm** is ceilings only, flown with **the airframe's asymmetry off**. Calm's floor ("never perfectly still") belongs to the airframe, and that target is the flight model's.

| ID | Quantity | P (measured) | Severe | Windy (c = 0.5) | Calm (ceilings) |
|---|---|---|---|---|---|
| T0 | Mean lean into crosswind | −9.1° | \|mean\| 9 ± 3° | 4.6 ± 1.5° | ≤ 2° |
| T0b | Sideways thrust relative spread; minimum on the windward side | 0.27; 0.034 W | 0.14–0.40; min > 0 in ≥ 4/5 runs | same | — |
| T0c | Direction changes (40 seeds × 120 min) | definitional | draws differ (resultant ≤ 0.7); centred ±15°; RMS deviation 20–60° (≥ 10° in every seed); shifts (≥ 45° in 30 s) 0.1–0.4/min; gap CV ≥ 0.5, periodicity peak ≤ 2.4; wind within 90° of the drawn direction through 75–98 % of 40 s windows | same | same |
| T1 | Roll residual std, field | 0.51° | 0.35–0.70° | 0.18–0.35° | ≤ 0.18° |
| T2 | Pitch residual std, field | 0.28° | 0.19–0.38° | 0.10–0.19° | ≤ 0.10° |
| T3 | Roll residual std, belt | 1.26° | 0.85–1.70°, ≥ 1.8 × T1 | 0.43–0.85° | ≤ 0.43° |
| T4 | Pitch residual std, belt | 0.43° | 0.29–0.58°, ≥ 1.2 × T2 | 0.15–0.29° | ≤ 0.15° |
| T5 | Yaw residual std, field / belt | 0.44° / 1.67° | 0.22–0.66° / 0.8–2.5° | 0.11–0.33° / 0.4–1.25° | ≤ 0.11° / ≤ 0.4° |
| T6a | Roll variance share below 1 Hz | 94 % | ≥ 85 % | same | — |
| T6b | Roll RMS at 2–4 Hz | 0.114° | 0.06–0.16° | 0.03–0.08° | — |
| T6c | Pitch share at 1–4 Hz; RMS at 1–2 Hz | 32 %; 0.18° | 20–45 %; 0.11–0.25° | 20–45 %; 0.055–0.125° | — |
| T6d | RMS above 4 Hz, roll / pitch | 0.125° / 0.054° | ≤ 0.13° / ≤ 0.06° | same | same |
| T7 | Gust events beyond 2σ | 14.2/min; 7 of 7 in the belt | 7–24/min; ≥ 70 % of roll events in the belt | same | — |
| T8 | Gaps between large disturbances (pooled) | median 2.0 s, CV 0.73 | median 1–4 s, CV 0.5–1.2 | same | — |
| T9a / T9b | Kurtosis over field, roll / pitch | 2.51 / 3.85 | 2.2–3.5 / 3.3–5.5 | same | — |
| T10a | Roll ~ pitch residual correlation | +0.21 | −0.05 to +0.40 | same | — |
| T10b | Roll ~ yaw rate correlation, whole / belt | +0.57 / +0.77 | +0.35 to +0.75 / +0.55 to +0.90, belt > field | same | — |
| T11a | Thrust-tilt residual std, belt / field | 1.72° / 0.60° | 1.1–2.3° / 0.39–0.81°, belt ≥ 1.8 × field | 0.55–1.15° / 0.20–0.40° | ≤ 0.55° / ≤ 0.20° |
| T11b | Trade-off is unavoidable (variant pilots) | P flew s = 0.64 | ratios ≥ 0.8 for s = 1 and s = 0; tilt within ±25 % of nominal | same | — |
| T12 | Field wobble never goes calm: 5 s block CV; smallest block | 0.17 / 0.23; 0.65 / 0.62 | CV ≤ 0.32; smallest ≥ 0.50 | same | — |

**Checks on the targets themselves:**
- **Plain Gaussian noise must fail.** With P's spectra it failed T9 and T12. The old T9 was replaced because louder-over-the-belt Gaussian noise passed it.
- **A schedule must fail T0c**, and the starting direction model must pass it.

### 8.7 Rain (clip P only)

| ID | Trait | P shows | Sim effect |
|---|---|---|---|
| R1 | Drops on the lens | none in any frame. **The pilot overrides this (2026-10-17): drops sit on the lens even in fast flight, and travel with the airflow** | a computed lens-drop model (FEED-13) |
| R2 | Rain visible falling | none | none; folded into R3 |
| R3 | Contrast loss with distance | none (far ground darker than near) | weak rain extinction |
| R4 | Sky and ground colour | neutral grey sky; dark ground that keeps its colour | overcast sky, wet materials |
| R5 | Dim picture | frame luma about half of the dry clips' | the normal AE → grain → N13 chain; no rain filter |
| R6 | Blur | at most mild, σ ≤ 0.7 px | none; optionally a capped wet-lens blur |
| R7 | Flare and glare | none | none from rain |
| R8 | Clearing by airflow | nothing to clear | driven by airspeed at the lens, not by throttle |

- **Signals needed:** rain rate (mm/h), and air velocity at the camera in camera axes.
- **The pilot's fiber-shake hypothesis:** P shows no degraded link state before or between outages (no sparkle or grain build-up). The link margin collapses within 1–3 frames and recovers within 0.1–0.7 s. Model link dips as **events** (tension spike, snag, sharp bend), whose rate rises with wind and trees. **Never tie dropouts to attitude rate or to rain on the lens.**

---

## 9. World reference

`[portable]`. The full catalogue is [reference-notes/terrain.md](reference-notes/terrain.md). The build list for the restart is [asset-list.md](asset-list.md).

### 9.1 The reference set (letters only)

| Letter | Kind | Shows |
|---|---|---|
| A | clip | wide, gently rolling steppe meadow from height, then a long descent to the grass; tree lines on the horizon; overcast |
| B | clip | dry, sun-baked cratered field, sparse broken trees, an earth bank |
| C | clip | village yard: old fruit tree, brick-and-plank cellar entrance (flight ends inside it), sheds, leaf litter |
| D | clip | overgrown yard: tall weeds, dead trees, stone rubble, a roof edge, a sagging cable; strong backlight |
| E | clip | war-damaged adobe house, tile roof stripped to the battens; the flight enters through the door |
| F | clip | inside a dense tree belt: thin trunks, bare dusty ground, dust haze, a round pit with branches |
| G | clip | a trench along a narrow tree strip beside a bare dark field; fast and low |
| H | clip | slow, low flight along a belt edge over lodged dry grass and dark soil, past a pole; overcast |
| L | clip | night flight in black and white over rolling dark fields; only silhouettes readable |
| P | clip | severe wind and rain, open stubble fields, one belt crossing (the wind study) |
| I | photo | urban edge: burnt five-storey block, collapsed roof pile, damaged two-storey house, rubble, masonry fence |
| J | photo | village street: two damaged single-storey brick houses, stripped roofs, overhead wire, brush piles |
| K | photo | five-storey brick block with its facade torn off, large rubble mound |
| M | photo | abandoned classic small sedan on asphalt: fragment holes, flat tyre, crazed windscreen |
| N | photo | blast-wrecked small hatchback in a wide debris field |
| O | photo | burnt-out heavy bonneted truck on a slushy road |

**The tree belt (посадка) is the backbone:** it appears in 7 of the 9 flight clips.

### 9.2 Key classes and sizes (detail in terrain.md)
- **Surfaces:**
  - T1 open steppe meadow: relief 2–5 m over hundreds of metres; grass 0.2–0.6 m; mottled olive-khaki; scorch spots
  - T2 dry cratered field: craters 1–4 m across and 0.3–1 m deep, 5–20 m apart
  - T3a tilled: ridges 15–30 cm high and 0.3–0.5 m apart
  - T3b burnt: from a 2–6 m scorch disc up to whole fields; wavy fire fronts; char stumps 5–15 cm
  - T4 belt floor: lodged straw mat 5–20 cm over dark loam
  - T5 bare dusty loam under trees
  - T6 yard ground with litter
  - T7 tall weeds 0.5–1.5 m
  - T8 trampled ground with debris
  - plus asphalt and concrete-slab tracks (PR-8)
- **Vegetation:**
  - V1 tree belt: 10–30 m wide, crowns 6–15 m, trunks 5–30 cm at 1–3 m spacing, understory 1–3 m, bushes among the trees (PR-10); a porous windbreak
  - V2 dead and broken trees, 4–10 m
  - V3 yard fruit trees: 4–7 m, crowns 5–8 m wide from 1.5 m
  - V4 fallen branches and brush piles
- **Earthworks:**
  - W1 craters
  - W2 trench: 0.6–1.0 m wide, 1.2–1.8 m deep, parapets 0.3–0.5 m, winding along belt edges
  - W3 round pit: 2–3 m across, 1–1.5 m deep
  - W4 cellar entrance: head 1.2–1.5 × 1.8–2.0 × 2–3 m; door **0.70 m** wide (PR-9) × 1.6 m
- **Buildings:**
  - B1 adobe house: 8–12 × 5–7 m, walls 2.3–2.6 m high and 0.4–0.5 m thick, ridge about 5 m, door 0.8 × 1.8, windows 0.8 × 1.0
    - **plaster (research):** lime whitewash #c4bfb1 over clay render #97754f over adobe #7d6146, with a red-clay base band 0.42 m high
    - about 30 % of the whitewash gone, around openings, corners, the foot, the wall head, and blast patches
  - B2 silicate-brick house: 8–10 × 6–8 m, asbestos-slate roof, blue or turquoise frames
  - B3 sheds: 3–6 × 2–4 m
  - B4 Stalin-era five-storey block, exactly one:
    - storeys 3.0–3.4 m, so 15–17 m to the eaves, plus a 3–4 m roof
    - 12–15 m deep and 40–80 m long, in 3–5 sections
    - brick walls 0.5–0.65 m thick; windows 1.3 × 1.8 m
    - damage kit: facade torn off, hanging slabs, burnt windows with soot, roof down to its timbers
    - a rubble mound 2–4 m high spreading 15–20 m
  - B5 rubble
  - B6 fences 1–2 m
- **Vehicles:**
  - R0 states: abandoned (fragment holes with rust edges, flat tyre, crazed glass), blast-wrecked, burnt-out (sits on its rims 15–25 cm lower; soot turns rust-brown in weeks)
  - R1 classic ВАЗ: 4.07–4.17 × 1.61 × 1.44 m
  - R2 ВАЗ 2108/09/099 and the 2110 family; Niva: 3.74 × 1.68 × 1.64 m
  - R3: Lanos 4.24 × 1.68 × 1.43 m; Aveo; Lacetti; Chery Amulet and Tiggo; VW Golf and Passat
  - R4 ЗИЛ-130: 6.68 × 2.50 × 2.40 m; ЗИЛ-131
  - R5 Урал: 7.35 × 2.50–2.69 × 2.64–3.0 m
  - wrecks: debris fields 5–20 m across; burnt patches 2–6 m
- **Small objects:**
  - S1 wires: **5–15 mm, spans 10–40 m, nearly invisible on the feed**, the main surprise hazard
  - S2 poles: 20–25 cm across, 7–9 m tall, may lean 5–15°; night landmarks
  - S3 masonry and tile debris
  - S4 planks, sheets, tarps
  - S5 household contents
  - S6 dust
- **Night (L):** ground nearly black, overcast sky mid-grey and brighter than the ground. Only skyline silhouettes read, so crests, lone trees, poles and belt crowns need clean outlines at distance, and LODs must keep them.
- **Colours:** author textures at photo-like albedo. The feed adds its own warm, saturated shift. Check the result through the feed against the `feed` hex values in terrain.md.
- **Gaps in the reference:** no vehicle in any clip; no roads in clips; no burnt field close up; no pylons; no standing crops, water or winter. Research or extrapolate these.

---

## 10. Map format and world queries

`[portable]`. The specs are [legacy/map-and-world/map-format-spec.md](legacy/map-and-world/map-format-spec.md) and [world-query-spec.md](legacy/map-and-world/world-query-spec.md). They worked well and transfer to Unity unchanged, apart from the frame convention.

### 10.1 Package
- **Layout:** a folder per map, containing:
  - `map.json`: the manifest, with `format_version`, size, resolutions, scale and offset, a 32-bit `seed`, and start points
  - `height.r16`: raw uint16 LE, row-major, a vertex grid of size/res + 1 per side; metres = offset + sample × scale
  - `surface.png`: 8-bit surface index on a cell grid, typically 0.5 m; 0 is invalid
  - `cover.png`: RGBA = grass, straw, twigs, litter, each a 0–1 multiplier on the surface's cover density
  - `holes.png`: optional, 0 or 255, for cellar shafts and dugouts
  - `objects.json`: asset id, position, yaw/pitch/roll and uniform scale; wires as point lists with sag and diameter
  - `labels.json`: optional, review-only, **outside the content hash**
- **Shared files:** the surface table (`surfaces.json`) and the asset catalog (`catalog.json`).
- **Rules:**
  - side lengths are multiples of 256 m, up to 8192 m
  - row 0 is north and column 0 west; the map centre is the origin
  - no georeferencing (the validator rejects lat/lon/EPSG fields and a place blocklist)
  - the validator rejects unknown assets, materials or surfaces, out-of-range physics values, objects out of bounds, and a future major version
- **Gallery example:** the 512 m UAT-1 gallery used a 1 m height grid (513²), offset −10 m at 0.01 m per unit, and a 0.5 m surface grid (1024²).
- **Authoring (planned, never built):** an editable layout (fields, belts, roads, villages, points of interest) compiled into the package, so the pilot's map changes are edits, not rebuilds.

### 10.2 The catalog (per asset)
- scene/prefab path
- **collision as primitives** (box, sphere, capsule, cylinder); required unless `visual_only`
- a material, which any shape may override
- `snag_hazard`
- an optional `wind_volume` (primitives), used instead of collision for wind; for example a tree's crown sphere, since collision is only the trunk
- `wind_porosity` (0 = solid to 1 = open)
- named fly-through gaps
- LOD switch distances

### 10.3 Wind-obstacle grid
- On a 2 m grid: `TopM`, `BaseM` and `Porosity`.
- Porosity is the optical porosity of a 2 m horizontal path, so a path through n cells has β₁·…·βₙ.
- Per cell, β = 1 − f·(1 − β_obj^(2 m / D_obj)), where f is the covered fraction and D_obj = footprint perimeter / π.
- Overlaps multiply porosity, take the maximum top and the minimum base. Wires and shapes with porosity 1 are not obstacles.

### 10.4 Queries
All of them are allocation-free, reentrant, need no scene, and are callable from the physics thread.
- `SampleGround(points)`: terrain height (the exact rendered or collision triangle), ground height (plus relief, ridges, pitfalls), normal, mat depths, support top, cover density, the surface plus its 4-cell blend, feature, `FeatureId`, depth, flags. **Continuous across surface borders** (bilinear blend of relief and mat). Pitfalls have finite-slope walls.
- `MicroDetailNear(center, r, mask)`: every stem, straw, twig and litter element **whose segment meets the sphere**, generated by integer hashing of (cell, kind, slot, map seed) in canonical order.
  - Each element has a stable 64-bit `Id`, base, direction, length, diameter, tip stiffness, hook release and a hooks flag.
  - Lying elements lie on the support top.
  - **The renderer draws the near-camera micro-detail from the same generator, at 0 mm parity.**
  - The true count is returned, so overflow is visible.
- `StaticContacts`: sphere and capsule, swept from the previous pose, with a margin, against every catalog shape, wire and runtime object. Returns point, normal, signed distance, material, object, shape, wire parameter, `Time`. At most one contact per (object, shape), in canonical order. **A 15 mm sphere swept 60 mm per step across a 5 mm wire is always caught.**
- `Geometry(object, shape)`: kind, pose, dimensions, material; for wires also span, sag and diameter.
- `Raycast(rays)`: distance, point, normal, material, object, surface.
- `WindGrid`: whole-grid read access.
- `GapsNear(center, r)`: world-space gaps.
- **Runtime objects:** add catalog objects before a flight (the launch stand), and reset to the `objects.json` state.
- **Identity:** a 64-bit content hash (FNV-1a over the package and the referenced catalog and surface entries, CRLF read as LF), plus a `QueryVersion` integer raised whenever the query math changes.
- **Determinism:** IEEE basic operations only; a golden file with byte-identical batched and single results, in Debug and Release; a concurrency test.
- **Budgets** (dev machine, AC power):
  - 100k ground samples in the physics pattern: < 10 ms
  - `MicroDetailNear` at r = 2 m: < 0.25 ms on the densest standing cover and < 1 ms everywhere. belt_straw reached 1.1 ms, accepted because it runs off the critical path at ≤ 40 Hz.
  - 100k capsules: < 100 ms
  - 100k rays: < 100 ms
  - a worst-case step: **< 60 µs**

### 10.5 Typical batch per physics step
- 44 ground samples: 4 feet, 4 rotors, ≤ 4 body points, ≤ 32 fiber nodes.
- 46 swept capsules: 4 feet, 4 arms, 4 prop discs, 2 body or coil, ≤ 32 fiber segments.
- 8 rays: 4 rotors, up and down.
- Micro-detail at ≤ 40 Hz when the centre has moved more than 0.5 m. Gaps at ≤ 20 Hz.

### 10.6 Known limits to fix in the new design
- **Gaps were upright rectangles (yaw only).** A stripped roof bay, a gable breach, broken windows with jagged edges and the open floors of the block were therefore invisible to the sim. The new gap model must hold **any planar polygon in any orientation** (or a clear-space volume), measured from the real geometry (PR-5).
- **`wind_porosity` was one scalar per asset**, so a house with solid walls and an open roof couldn't be expressed. The new model needs porosity **per wind-volume part**.

---

## 11. Analog video feed

`[portable]`. The full notes are [reference-notes/video-feed.md](reference-notes/video-feed.md). **The feed is simulated, not filtered** (D-008): artifacts come from sim state, with measured random rates only where no cause is visible.

### 11.1 Signal chain
scene → lens → camera sensor + DSP (AE, AWB, sharpening) → **composite PAL, 50 fields/s** → flight-controller **OSD insert** → fiber TX → fiber → fiber RX → goggles/receiver (sync, no-signal screen, bob deinterlace, scale) → recorder (≈ 30 fps) → re-encode (not simulated).
- Two receiver styles:
  - **A/H/L/P:** a bright pure-blue no-signal screen with small centred text, about 275 lines, a 1 px top border
  - **B–G:** a deeper blue screen with two short text lines top-left, about 286 lines, a 4 px top border
- There is no RF path, so nothing is RF breakup.

### 11.2 Noise events

| ID | Event | Measured | Driver |
|---|---|---|---|
| N1 | Always-on grain | Elongated horizontally (one sample × one line). Day: σ about 2–4 levels live (the encode masks it). Night: 2–5× the day level. New pattern every field | camera gain from AE; weak motor-current term |
| N2 | Chroma sparkles | Coloured dashes, 1 line × 3–10 px, density follows fine texture (0–3 per thousand px); absent at night | **link margin** (fiber tension and bend); ESC spikes |
| N3 | Rolling thin dark lines | 2–5 px, 8–30 levels darker, 150–157 px apart (about 380–395 Hz), rolling at 14–60 px per frame; in 1 of 3 airframe groups, about 50/min in runs of 2–24 frames | per-flight draw; motor electrical frequency sets spacing and roll |
| N4 | Diagonal dotted interference | About 10 lines at slope 0.45–0.5, blue/white dashes with yellow/green fringes, part of a field, on about alternate frames; in 1 group, 45 % of frames, about 8.5 runs/s | per-flight draw; a sinusoid in the composite near the subcarrier |
| N5–N7 | Staged loss / hard cut | see §11.3 | link lost, power lost, impact |
| N8 | H-sync tearing | bottom 18 % of lines displaced 20–60 px in a zig-zag | link margin collapsing |
| N9 | Frame stutter | recorder artifact | not simulated unless the pilot confirms |
| N10 / P10 | Macroblocks, codec grid | re-encode | **not simulated** |
| N11 | Exposure jump | the sun hidden by foliage for one frame | AE + flare |
| N12 | Recovering dropouts | see §11.3; about 5/min in severe wind | link-margin dips |
| N13 | Dim-light level steps | the picture jumps to a new level (mostly a black-level shift, gain 0.68–0.98, offset ±13); a one-field dark dip first (gain 0.43–0.59) easing top to bottom; **the OSD dims too**; every 1–2 s, only in dim light | light-level gated; free-running |

**Rates are tunables.** They come from only 1.3 minutes of in-flight footage, and **every terminal loss in the footage followed a close approach or contact**. Losses are event-driven, not a random clock.

### 11.3 Loss state machines
- **Staged** (the pilot's four stages):
  - PICTURE
  - PARTIAL_SNOW, 1–5 fields: snow above a split line at 80–90 % of the height (may move down); the first 1–2 fields in colour
  - BLUE, 5–8 fields
  - SNOW, 10–15 fields: colour-killed, under the receiver text, level redrawn in 52–65
  - BLUE until reset
- **Hard cut:** PICTURE → [GLITCH 1–2 fields: N8 tear or about +10 % grain, in half the cases] → BLUE.
- **Which variant:** staged with p ≈ 0.8 when the margin fell through the threshold over ≥ 1 field (a fade); p ≈ 0.2 when it vanished within a field (clean break or power loss).
- **Snow texture:** near-binary white dashes on black (60–68 % below 32, 8–12 % above 224), one line tall, 6–8 px long, with a faint diagonal herringbone. **Never grey Gaussian noise.**
- **Recovering dropout (N12):**
  - **precursor** in 2 of 4 events: 2–5 fields of brightening (+25 % to +120 %) under dense bands of coloured impulse dashes, or an N8-style tear
  - **outage:** either short black for about 3–4 fields (OSD included, entering and leaving mid-field), or blue for 18–35 fields (0.37–0.70 s)
  - **recovery:** straight back to picture, sometimes with an N13 dip
  - **the picture comes back clean, with no receiver text** (PR-4 correction)
  - **rate:** about 5/min in severe wind, none in 1.3 min of calmer flight; a free-running floor of about 0.2/min
  - the margin collapses within 1–3 frames, with **no build-up beforehand**

### 11.4 Pixel, optics and colour

**Pixel:**
- **P1, field-line structure** (the pilot's "edgy pixels"): render at field resolution (about 450 × 288 effective), choose the field parity per field, and upscale with a near-nearest vertical filter. Diagonals stair-step about 4 px on 1080p.
- **P2:** a horizontal luma low-pass to about 450 samples per line.
- **P3:** horizontal unsharp mask halos, a dark undershoot of about 40 levels × 4 px, plus a weaker vertical one, applied **before** the P2 blur.
- **P4:** chroma about 5× softer than luma (about 90 samples per line), 0–3 px right of the luma.
- **P5:** cross-colour rainbow on fine detail near the subcarrier, alternating line to line and field to field.
- **P7:** a hard clip with a magenta or lilac cast in the last 10 %.
- **P8:** black borders (4 or 1 rows on top, 1–2 at the bottom).

**Optics:**
- **O1, barrel distortion:** k1 ≈ **0.33** (p_u = p_d·(1 + k1·r_d²), r normalised to the half-width); render with extra FOV (the edge needs about 1.33× the half-width).
- **O2, vignette:** 10–15 %.
- **O3, flare:** a reddish-magenta ghost opposite the sun, plus veiling glare clipped by a housing edge, occluded by depth.
- **O4, lens dirt:** builds up from contact events.
- **O5, depth of field:** sharp from 1 m to infinity.

**Colour:**
- **C1, AE:** holds the frame mean at 0.47–0.55 of full scale; τ ≈ 0.3 s brightening and 0.5–0.8 s darkening; slower at the gain limit.
- **C2, AWB** (the pilot's "colours change as you get closer"): grey-world, τ ≈ 1.2 s, gain ratio clamped to 0.7–1.4, on top of a warm, saturated camera matrix (r/g ≈ 1.13–1.16).
- **C3:** saturation falls with AE gain.
- **C4:** motion blur = angular rate × exposure time, which grows in low light.
- **C5, night:** monochrome (chroma median about 1 level), the strongest grain, sky brighter than ground, N13 on. **The same pipeline driven to its low-light end, never a separate night filter.**

### 11.5 Effective resolution
Luma about 450 samples × 286 lines per field; chroma about 90 samples; 50 fields/s. **Produce the feed at that resolution and only then upscale.** That is a large performance win.

### 11.6 OSD, exact (PR-2, PR-2a)
- **Grid:** a character OSD on a **30 × 16 grid**. The cell is **67.3 × 71.0 px** on a 1920 × 1080 frame, measured from glyph centres, with the origin inset half a cell. At that pitch the grid covers **2019 × 1136 px, larger than the picture**: the last two columns and rows fall outside. **This is correct**; don't "fix" it by dividing the frame by 30 × 16.
- **Glyph:** 5 × 8 samples inside a 14 × 18 cell. The ink is about 23–24 × 29–30 px, cap height about 30 px. White glyphs with a black outline.
- **Layout:** readouts along the edges and corners; a small crosshair or aircraft symbol and an optional dotted artificial horizon in the centre; status text across the top; flight mode on the left; battery bottom-left; name, message, timer, current and link quality along the bottom. Warnings blink about 7 frames on and ≥ 5 off.
- **Strings:** **"Svinoriz 10 Opto"** and **"! SAFE !"**. Invent every value.
- **Not lens-distorted.** Inserted before the analog stages, so it softens, fringes, tears and dims (N13) with the picture.
- **Two decoder facts:**
  - The scene smears horizontally but the OSD stays crisp, because the softness comes from the sensor aperture **before** encoding. A real receiver **traps** chroma (luma = composite minus detected chroma). Putting the blur in the decoder's luma path eats thin OSD strokes.
  - The OSD doesn't rainbow while foliage does, because a glyph is identical on every line and the PAL delay line rejects that. Band-limit the OSD into the chroma detector: fringing on OSD rows drops from 105 to 7.7, against the footage's 7.9.

### 11.7 Pipeline order
1. Render the scene with extra FOV.
2. Lens distortion, vignette, flare.
3. Camera: AE/AWB, tone curve, clip, sharpening.
4. **OSD insert.**
5. Composite encode: band limits, cross-colour, N3/N4, grain, sparkles; N13 here, after the OSD.
6. Field sampling.
7. Receiver: N8, N12, the loss state machine, borders.
8. Upscale.

Work at about 720 × 288 for stages 4–7.

### 11.8 Signals the feed needs from the simulation

| Signal | Unit | Rate | Feeds |
|---|---|---|---|
| Motor electrical frequency per motor | Hz | 50 Hz field mean | N3 spacing and roll |
| Motor current per motor and total; current step | A, A/s | 50 Hz mean and max | N3, N4, N1/N2 (weak), N12 term |
| Battery voltage under load | V | 50 Hz | brownout cut, N8, OSD |
| Camera angular velocity | rad/s | per frame | C4 blur, P5 crawl |
| Camera vibration (3 bands) | m/s² | 50 Hz | **PR-1 throttle-punch shake**; N2/N3 hooks |
| Impact events | m/s², N·s | event | loss on hard impact, jolt, lens dirt |
| Fiber tension, minimum bend radius, link state and margin | N, m, enum + dB | 50 Hz | N2, N8, N12, N5–N7 variant |
| Scene light level | cd/m² | per frame | AE → grain, saturation, exposure, N13 gate |
| Sun direction and visibility | vector, 0–1 | per frame | O3, N11 |
| Time of day | h | on change | sky, base light |
| Rain rate; air velocity at the camera | mm/h; m/s | on change; per frame | R1, R3, R4, R6, R8 |
| OSD state (30 × 16 characters, blink toggled) | chars | 50 Hz | OSD |

### 11.9 The first attempt's feed: port it (D-026)
**The pilot wants the Unity feed's noise "very much similar" to the Godot feed.** It is copied, with a port spec, in [legacy/video-feed-godot/](legacy/video-feed-godot/). Its timers (3–6 recovering dropouts per minute, about 10 quality drops per minute) are the pilot's chosen look, not a defect. The history below explains what was still missing.
- **What was built:** a mixed feed chosen from screenshots, plus staged losses, the exact OSD, and a clean recovery. **Nothing physical drove it:** a `FeedSignals` placeholder used distance from the map origin and camera speed.
- **Random rates:** in the first version, losses fired on a random timer (cuts 3.8/min at 60–160 ms, drops 7.8/min at 0.3–2.2 s). The final version keeps random recovering dropouts at the pilot's requested 3–6/min, while terminal losses are event-driven. The pilot wants that kept (D-026). Performance was unverified (about 0.33–1.0 ms at 1080p).
- **What the port adds on top, without changing the ported look:**
  - render into a low-resolution target at field resolution
  - a real AE/AWB loop
  - lens distortion
  - staged losses with the blue screens
  - the physics interface replacing the stand-ins

---

## 12. Old decision log (Godot, D-001 to D-017)

The full text is in [legacy/godot-decisions.md](legacy/godot-decisions.md). The new log ([decisions.md](decisions.md)) carries the portable ones forward.

| Old | Decision | Carry over? |
|---|---|---|
| D-001 | Engine: Godot 4.7 .NET (C#) | replaced by Unity |
| D-002 | Custom model, Jolt for contacts | superseded by D-010 |
| D-003 | Godot project in `game/` | idea kept: the engine project in a subfolder, so sources and docs stay out of the importer |
| D-004 | Reference footage never enters git | **yes** |
| D-005 | `.blend` in `blender/`, exports by script, binaries in LFS | **yes** |
| D-006 | Merge with `--no-ff`, never squash | **yes** |
| D-007 | OpenSpec | **yes** |
| D-008 | The feed is simulated, not filtered | **yes** |
| D-009 | Own quadtree terrain renderer (430 fps on 4 km); Terrain3D rejected for 4.7 | Godot-only; use Unity Terrain |
| D-010 | Custom flight model and custom contacts; the engine physics out of the flight loop | **yes**: the same reasons apply to Unity's PhysX scene queries (main-thread scene, no bit-exact replay outside the engine) |
| D-011 | Weather is a fixed pilot setting: Calm / Windy / Severe | **yes** |
| D-012 | Real models before any gallery | superseded by the 2026-10-17 plan |
| D-013 | Feed shipped as a spike, with debt | lesson: no spikes outside the process |
| D-014 | "Svinorez 10 Opto" and "! SAFE !" | **yes**, spelled **Svinoriz** from 2026-10-17 |
| D-015 | PAL only | **yes** |
| D-016 | Controller input before the flight model; HID timing designed once | **yes** |
| D-017 | Gallery first with reserved stations; labels outside the hash | **yes** |

---

## 13. What the first attempt built

`[Godot]` unless marked. It is useful as a reference implementation if a port is ever wanted.
- **Repo:** `MxTriX77/450fpv`, `main` at `b684cce`.
- **Code** (`game/src/`):
  - **world** (`WorldQuery`, `Contacts`, `MicroDetail`, `WindGrid`, `DetMath`, `ContentHash`, `HeightmapTerrain`, `Catalog`, `SurfaceTable`, plus selftests and a golden file): plain C#, portable to Unity almost as is
  - **video** (`VideoFeed`, `FeedEvents`, `FeedLoss`, `FeedOsd`, `FeedFont`, `FeedSignals`) and its shaders in `game/assets/shaders/video_*.gdshader`
  - **input:** `NoclipCamera`
  - **ui:** `PerfOverlay`
- **Tools:**
  - `tools/blender/`: `assetkit.py`, `buildingkit.py`, `treekit.py`, and `export.py` (headless build → .glb with LODs and a budget report)
  - `tools/assets/fetch_cc0.py` (Poly Haven and ambientCG by id and SHA-256) with `cc0_manifest.json`
  - `tools/map/`: `make_synthetic.py` (synthetic 4 km relief), `make_sample_patch.py`, `make_uat1_gallery.py`, and `validate_map.py` with tests and a place blocklist
  - `tools/worldbench/` (dotnet benchmarks and golden)
  - `tools/reference/`: `extract_frames.py`, `track_attitude.py`, `wind_stats.py`, `rain_stats.py`, `wind_history_fixture.py` (Blender Python, run headless)
- **Assets:**
  - **24 CC0 texture sets at 2K, plus 2 HDRI skies**, all from Poly Haven ([legacy/godot-cc0-credits.md](legacy/godot-cc0-credits.md) lists them with the purpose of each):
    - ground: brown_mud_02, dirt, dry_mud_field_001, burned_ground_01, farm_furrows, grass_ground, withered_grass, dry_decay_leaves, rubble, road_damaged, excavated_soil_wall
    - walls and roofs: rough_plaster_broken, red_brick_plaster_patch_02, white_bricks, clay_roof_tiles_02, asbestos_sheet, worn_corrugated_iron, concrete_wall_008
    - wood, bark and metal: weathered_planks, bark_willow_02, bark_willow, rusty_metal_02, rust_coarse_01
    - skies: kloofendal_overcast_puresky, kloofendal_48d_partly_cloudy_puresky
  - **Models** (built by script): tree_acacia, tree_ash, tree_maple, tree_young, tree_dead, shrub_belt, tree_fruit, house_adobe (8 named gaps), cellar_head (0.70 m door), cellar_shaft, shed_plank, gate_plank, fence_planks, launch_rails, test trench pieces, and placeholders.
- **Maps:** `sample_patch` (256 m test fixture), a synthetic 4 km terrain (602 fps, terrain only) and `uat1_gallery` (512 m, loads in 3.8 s, 347 fps at the start view).
- **Measured (dev machine, AC power):** terrain on 4 km at **430 fps** (1 % low 245, 24–34 draw calls, +96 MB VRAM); a world-query worst-case step of **34.7 µs**; micro-detail parity **0.0000 mm**.
- **Dev machine** `[portable]`: Lenovo laptop, **Ryzen 7 7735HS, RTX 4050 Laptop 6 GB, 16 GB RAM**, 1920 × 1080 at 144 Hz. **On battery**, the CPU drops to 1.5–2.4 GHz and Vulkan sees only the Radeon 680M iGPU (the empty scene falls to 177 fps). **Benchmark only on AC, with the power mode printed.** 6 GB of VRAM is the texture budget's hard ceiling.

---

## 14. Lessons learned

### 14.1 Process and agents `[portable]`
- **Usage limits stopped agents repeatedly.**
  - Commit early and often, so a stopped agent loses nothing.
  - Resume an agent only if the limit stopped it; an agent the user stopped can't be resumed.
  - Prefer **fresh agents with short briefs** over resuming 300–500k-token agents.
  - Run **at most two agents in parallel**.
  - Never let an agent poll a long job: background it and wait for the completion notice.
- **Everything important goes to disk** (notes, specs, decisions, handoffs), so compaction or a new session loses nothing.
- **Cloud sessions can't run GPU or editor verification.** Re-verify locally, and say plainly which gates couldn't run.
- **External AI work (a Gemini episode)** committed under role names and approved itself. Our QA re-verification found real errors (the night clip and the loss sequence). **Every outside contribution gets QA re-verification.**
- **Spikes outside the process become debt** (D-013). If the pilot asks for a quick demo, record the debt in the decision log immediately.
- **Spec wording matters:**
  - judge continuity against the local slope, not absolute height
  - use a finer numeric step where features are tiny (normals inside pitfalls: 1 µm)
  - state exclusions explicitly (lying straw crossing a pitfall)
  - record every accepted budget deviation (micro-detail at 1.1 ms)
- **Test the tests:**
  - every statistical target must **fail a known-wrong model**: T9 was replaced when plain Gaussian noise passed it
  - **size statistical tests by measuring their false-failure rate**: T0c was flaky until it used 40 seeds × 120 min
- **Golden-file re-records** must be audited case by case. A re-record that silently blesses drift is the worst failure available, and it is invisible if nobody checks.
- **Look at every asset.** The chequerboard house walls passed every automated check; only screenshots caught them. Do a screenshot pass at 10 / 50 / 200 m and at pilot height (1.5 m), and judge it against the reference notes.
- **Benchmarks:** only on AC power, with vsync off and the power mode and clocks printed. Battery numbers are meaningless.

### 14.2 OPSEC slips (they happened; prevent them)
- Reference file names went into a pilot-answers note (later replaced with letters).
- A Gemini `review.md` named a reference file (dropped before push via a soft reset).
- A messenger app name went into a design doc.
- **Prevention:** a pre-commit hook or QA grep for footage-like file names, extensions (`.mp4`, `.mov`, `.jpg` outside `assets/`), messenger names, coordinates and dates in docs. The public history keeps some early slips; the pilot chose not to rewrite it.

### 14.3 Assumptions the pilot corrected
- Camera uptilt was inferred at 25° from footage. **It is 0°.**
- Drone mass was first assumed at 2.3–3.35 kg. **The class is about 5 kg.** Research class specs before deriving anything.
- The pilot was asked about motors and about plaster. **Research those instead.**
- The cellar door was built at 0.78 m "to fit". **The pilot wanted 0.70 m: barely passable is the point.**
- A belt was built at 3 m trunk spacing with no understory. **Belts have bushes among the trees.**

### 14.4 Art and world `[portable]`
- **Generic box UVs** ran each panel's longest side along V, so wall textures turned 90° with panel proportions, neighbours didn't share texture, and bricks lay on end. **UVs must be in asset space, upright and continuous across panels.** One facade map per wall, plus a damage mask (R damage depth, G soot, B dirt and streaks, A base band).
- **Two similar-hue materials side by side** read as tiling. So does a patch scale close to the panel size.
- **Hard-edged surface rectangles** read as stickers from the air. Feather everything (PR-11).
- **Rubble coloured near-white** read as snow or gravel. Colour rubble from brick, tile and plaster.
- **A regular batten grid** looks fake on a shelled roof.
- **At 200 m a real belt is a dark band;** fog plus a pale floor albedo washed ours out. Don't "fix" it by darkening impostors, because matching the near LOD is what hides the switch.
- **Litter drawn as flat chips** under 4 m looks wrong. It needs leaf silhouettes.
- **Micro-detail with a hard edge** at 8 m: fix it with a near-ring fade plus a far density layer to ≥ 60 m.
- **Engine auto-LOD plus our own LODs** double-switched. Use exactly one LOD system.
- **Ground looks darker near than far** (normals and AO resolve up close). That is physically defensible; the pilot sees the ground darken on descent.
- **A blue window that was a solid plane** read as wrong. Windows are empty frames, or broken glass.

### 14.5 Physics and world `[portable]`
- **Engine physics queries allocated on every call and weren't thread-safe** (Godot's C# space state). Expect the same class of problem in any engine's scene queries, and keep the flight loop's queries in our own code.
- **Plain C# met the 60 µs per-step world-query budget.** There is no need for exotic tech.
- **The probe** (a fake drone over the real API) found the API gaps cheaply before the physics change.
- **Two limits of the first map format:** rectangle gaps and one wind porosity per asset (§10.6).

### 14.6 Godot specifics (for the record) `[Godot]`
- The csproj had to be generated through Godot's own generator.
- Terrain3D didn't support 4.7. Jolt's C# API allocated.
- vsync capped benchmark fps, and the "Best power efficiency" mode skewed numbers.
- The perf overlay was drawn through the analog feed's canvas layer.

---

## 15. Open questions and known debts

### 15.1 Pilot questions
None are open: every earlier question was answered or dropped on 2026-10-17 (§4.5). The live log, and the rule for asking anything new (D-024: never ask what physics can calculate), is [pilot-questions.md](pilot-questions.md).

### 15.2 Engineering debts carried into the restart
- The input-timing contract (HID → 1 kHz).
- The physics → feed signal interface (§11.8), including PR-1 camera shake.
- The gap model as arbitrary polygons or volumes; wind porosity per part.
- **Cross-machine determinism has never been tested:** the golden ran on one machine and one runtime. A second machine is needed.
- The feed performance budget was never measured trustworthily.
- The stem density is 5–10× below real (400 vs 2000–4000 per m² for meadow). The lateral force is negligible (≈ 0.1 N), but the jitter is spikier. Add a `stems_per_element` weight if the pilot says the grass jitter reads wrong.
- **Rubble relief** as smooth noise gives slopes of 40–50°, so feet slide into troughs. Real rubble needs flat faces with sharp edges (debris chunks).
- **Viscous soil damping overlaps the plastic loss** on soft soils. Expect to lower it in tuning.
- The drone layout must be re-derived: payload on top, two batteries (2026-10-17).

---

## 16. Pointers

- **Repo:** `https://github.com/MxTriX77/450fpv`. The Godot work is on `main` (tag `godot-final` → `b684cce`). The Unity work goes under `unity/`: the trunk is `unity/main`, and features are `unity/<prefix>/<change-id>`.
- **Unmerged old branches:**
  - `physics/build-flight-model`: the spec draft, copied into `docs/legacy/flight-model/`
  - `world/house-art-pass-wip`: the parked facade system; it does not build
  - `techart/video-feed-spike`: merged as #27
- **Old local checkout:** `C:\Users\maksy\.claude\projects\450fpv`. Its `reference/` folder holds the footage, plus `reference/_frames/`: extracted frames, `attitude.csv`, `rain_stats.json` and metrics. **The pilot moves it themselves.**
- **Old tool paths:**
  - Godot: `C:\Users\maksy\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_…\Godot_v4.7.2-stable_mono_win64_console.exe`
  - Blender 5.2 (headless scripts ran as `blender -b --factory-startup --python …`)
  - .NET 10 SDK
  - Python
  - git-lfs
  - `gh`
- **Skills and commands** (copied into this pack's `.claude/skills/` and `.claude/commands/opsx/`; see [tooling.md](tooling.md)): `karpathy-guidelines`, `openspec-propose`, `openspec-explore`, `openspec-apply-change`, `openspec-update-change`, `openspec-sync-specs`, `openspec-archive-change`; `/opsx:*`. OpenSpec CLI 1.13.1 (npm, global).
