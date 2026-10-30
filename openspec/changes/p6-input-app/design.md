# Design: p6-input-app

## Context
- **What exists:** the Unity app has a `Boot` scene, the G1 gallery and the test range (D-044) with `NoclipCamera`. There is no input layer, menu or flight session yet.
- **What it builds on:** `p6-flight-core` fixes `StickSample`, `FcSettings` and `PoseFrame` in its task 1.1, and `p6-world-query-gallery` provides the loader for the test range's `maps/test-range/placements.json` (D-044).

## Goals / Non-Goals
- **Goals:** the radio in, trusted to the millisecond; the pilot's rates; the shortest path into the air; the summary.
- **Non-Goals:** the proposal's.

## Decisions
1. **Reading the radio:**
   - **The source:** Unity's Input System HID path on Windows, read on a dedicated input thread (or the Input System's background polling), never in `Update`.
   - **Timestamps:** taken on arrival with `Stopwatch` ticks converted to µs on the sim's clock.
   - **The handoff:** samples go to the physics thread through a single-producer single-consumer ring.
   - **Consumption:** the physics step takes the latest sample at or before its time (`p6-flight-core` design 1).
2. **Measuring the interval:** the input thread records the report interval's histogram over the first 10 s of every flight, into the header.
3. **Measuring latency:** a test mode drives a known stick step through the HID path (a recorded report stream injected at the device boundary) and times the motor command's response in the physics. The real TX12's own latency, from gimbal to USB, is outside this; the TX12's report interval is recorded instead. **As built (1.5):** the step goes into a recording's or a live radio's reports at the device boundary, keeping their arrival stamps. Two figures from each step report's arrival: to the first step that uses it, and to the motor command's response (the ESCs' mean duty halfway through the change). The input header entry holds both and the flight's step-0 host time.
4. **Calibration:**
   - **The flow:** centre, sweep, then the arm switch. Channel order, reversal, endpoints and deadband are detected from the sweep.
   - **The health check:** an axis span under 200 of the HID range, of the roughly 683–903 that healthy axes show (fc-profiles), is "too narrow, recalibrate the radio" (assumed threshold). **As built (1.2):** a second rule catches the real TX12 pitfall, which the span rule misses: EdgeTX divides by `max(100, span)`, so a radio calibrated on spans like 79 / 1 reaches full output about 100 counts from centre and its USB axis jumps end to end over the full HID range. A sweep that crosses 10 % to 90 % of its travel in under 0.7 s twice in a row is "too narrow" too (the first time the pilot is asked to go slower). The wizard's other thresholds (2 % centre for 1 s, a 0.3 s hold at 80 % or a 2 s hold, 25 % for the arm, 3 % off a line for a curve, 5 % short for dual rates) are assumed and checked at 1.2b. **A third rule (QA's finding 2, then finding 9 and gap 2a), the smallest step:** a radio's axis whose sweep, slow enough for the jump rule, never changes by less than `PitfallStep` between two consecutive reports inside 10–90 % of its travel, on either side of its centre, is too narrow at any speed. `PitfallStep` is half EdgeTX's floor gain (1024 / 100 per ADC count on a span under 100), 5 HID counts, derived: the pitfall's smallest step is 10 at any sweep, jitter or filter (EdgeTX's integer division), a healthy 683–903 span's is 1 or 2, and a still ADC at the jump rule's fastest accepted sweep (80 % of the 2048 in 0.7 s, a report every 2 ms) moves 4.7 a report. **The update guard (QA's finding 10):** that 4.7 assumes an update every 2 ms, and a radio that updates every 3–4 ms moves 5–9 an update on a quick healthy sweep. So a side of the sweep as coarse as `PitfallStep` is judged only if its part of the band held at least its counts / (`PitfallStep` − 1) of the radio's updates (about 205 a side, 410 over the band): a healthy radio's mean step is then 4 or less, so some step is under 5 (pigeonhole, with a count's margin). Fewer, and the pilot is asked to go slower ("Too fast", then "Still too fast"), as often as it takes, never refused. The update interval comes from the reports' values, not their stamps: the shortest mean interval between 16 consecutive changes of any one axis, so a 1 ms stream repeating a 4 ms output counts 4 ms. At 2 ms a healthy radio is never coarse on a sweep the jump rule accepts, so nothing changes for it; the pitfall crossing its band in 0.7 s to about 0.82 s is asked once more before it is named. It covers the throttle (split at the range's middle, EdgeTX's 0) and a pitfall on one side only, and catches radio spans up to about 205 (QA's E9w E caught 205 too: 1024 · d / 205 steps by 5 except about once in 205 counts, harmless); a partial calibration of 250–300 passes. It replaces the rest-spread rule, dropped: a healthy radio's rest of 8–10 counts (jitter, or a thumb left on the sticks) was refused. An axis that keeps the centre step from settling for 5 s (the pitfall with 3 ADC counts of jitter or more spreads over the 2 % band) is named in the prompt, "Channel N keeps moving". 1.2b records each axis's rest spread and smallest step on the real TX12, with its filter setting.
   - **Radio-side curves:** a sweep is linear if the channel-versus-time trace of a steady sweep fits a line within 3 % (assumed). Otherwise, name the axis and tell the pilot to switch off radio expo or dual rates.
5. **Settings:**
   - **The record:** `FcSettings` holds Betaflight CLI names.
   - **The screen:** the Calibrate screen edits ACTUAL rates and the throttle curve, with previews drawn from the same math the core uses (`p6-flight-core`'s rate function, called, never copied).
   - **Profiles:** stored as JSON under the user's profile folder.
6. **The flight session:**
   - **The scene:** one scene loads the test range, the world loader and a `FlightSession` component, which owns the physics thread's lifecycle.
   - **The drone:** a stand-in mesh at the interpolated pose (labelled), replaced by `p5-drone`'s prefab.
   - **The camera:** a plain camera with FEED-02's intrinsics (106° horizontal, 16:9, 0° uptilt by the pilot's answer) until `p4-feed`'s rig.
   - **Restart:** a new seed, a new log.
7. **The summary:** read through `p6-flight-core`'s summary reader, never by parsing the log in the UI.

## Risks / Trade-offs
- **The Input System's HID timing on Windows** may batch reports. The interval histogram shows it, and the fallback is a raw HID read on our own thread.

## Migration Plan
- None: new code.

## Open Questions
- None. The minimal menu's look is judged at G7.
