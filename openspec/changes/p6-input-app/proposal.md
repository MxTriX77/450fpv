# Proposal: p6-input-app

**Owner:** game-developer · **Reviewers:** physics-engineer (the stick samples and settings the core consumes, the timing contract), tech-artist (the FPV camera the session shows, once `p4-feed` lands), qa-engineer
**Requirements:** INP-01…07, APP-02…05 (APP-01's wireframe gate is folded into G7 by D-042), PHB-07 (the G7 flow) · **Decisions:** D-016, D-022, D-023, D-042 · **VISION:** §2 (the pilot flies with their own TX12, the feel of their own rates), §3 (a minimal, clean app)
**Source:** `docs/reference-notes/fc-profiles.md` (the pilot's profiles, the sim default column, the TX12 calibration pitfall); `p6-flight-core` design 1 (the `StickSample`, `FcSettings` and `PoseFrame` interfaces).

## Why
G7 is the pilot flying with their own RadioMaster TX12, their own rates, and a menu that gets them into the air. This change brings the radio in through a timing contract the physics can trust, calibrates it, and lets the pilot set rates and the throttle curve. It also builds the minimal app around the flight (D-042: judged at G7, no wireframe gate): Train / Calibrate / Exit, the setup, the flight session over the gallery, and the post-flight summary.

## What Changes
- **The radio** (INP-01, INP-04, INP-05, INP-06):
  - the TX12 and generic HID joysticks, read off the render thread
  - each report timestamped on arrival with a high-resolution clock and passed lock-free to the physics thread as `StickSample`
  - the report interval measured and recorded
  - stick-to-motor latency measured
  - USB loss failsafe-disarms, logged
  - a keyboard fallback for development only
- **The calibration wizard** (INP-02): centre and sweep, the channel order, reversal, endpoints and deadband, the arm switch, live raw and calibrated bars, named profiles, the health check (an axis spanning a tiny range), and radio-side curves named and flagged.
- **Rates and throttle** (INP-03, INP-07):
  - ACTUAL rates and the throttle curve with previews, edited into `FcSettings` under Betaflight CLI names
  - the defaults from `fc-profiles.md`'s sim default column
  - room for the rest of Betaflight's flight-feel settings without a redesign
- **The app** (APP-02…05):
  - the main menu: Train, Calibrate, Exit (plus Explore in review builds)
  - the setup: payload, legs, time of day, wind, rain
  - **the flight session** over the test range (`p6-test-range`, D-044):
    - the world loaded from `maps/test-range/placements.json`
    - the sim thread started
    - the pose interpolated onto the drone (a labelled stand-in until `p5-drone`)
    - the FPV camera (a clean camera with FEED-02's intrinsics until `p4-feed`'s rig lands)
    - the launch stand placed with the legs off
  - arm and disarm on the switch, pause and restart, the end conditions, the post-flight summary from the log
  - persistence in the user's profile

## Capabilities

### New Capabilities
- `tx-input`: devices, the timing contract, calibration, the settings the pilot edits, disconnect and the keyboard fallback
- `app-flow`: the menu, setup, the flight session, in-flight controls and end conditions, the post-flight summary, persistence

### Modified Capabilities
- None.

## Non-goals
- **The feed and OSD look** (`p4-feed`).
- **The drone model** (`p5-drone`).
- **Physics** (`p6-flight-core`).
- **The world queries** (`p6-world-query-gallery`).
- **Any UI beyond these screens;** polished visual design waits for the pilot's word at G7.
- **The fiber's end conditions** (7d, after G7).

## Impact
- **New code:** `unity/Assets/_Project/Scripts/{App,Input,UI}/`, `Scenes/` (the menu and the flight session), and the user-profile persistence.
- **Consumes:**
  - `p6-flight-core`'s interfaces: it writes `StickSample` and `FcSettings`, and reads `PoseFrame` and the log summary
  - `p6-world-query-gallery`'s loader
  - `p4-feed`'s camera rig when it lands
