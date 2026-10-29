# Tasks: p6-input-app

Branch `unity/game/p6-input-app` off `unity/main`, after `p6-flight-core`'s task 1.1 (the interfaces) is merged into `unity/main`, or into this branch from its sub-branch. Tasks 1.1 to 1.3 run in sequence. 1.4 needs `p6-world-query-gallery`'s loader (its task 1.3), and 1.5 runs last.

## 1. Build

- [x] 1.1 [game-developer] The radio (design 1, 2):
  - **What:** HID reading off the render thread for the TX12 and generic joysticks, arrival timestamps, the lock-free ring to the physics thread as `StickSample`, the interval histogram, the USB-loss failsafe, and the keyboard fallback marked in the log.
  - **Verify:** the "render rate does not touch the sticks" scenario with a recorded stream, and the disconnect scenario.
  - **As built** (`Scripts/Input/Radio/`, the engine-free `Fpv450.Input.Radio`; `tools/inputcore`, `tools/inputtests`, `tools/radioprobe`):
    - **The path:** Windows' HID class driver, not the Input System, which hands events to the main thread only (design 1's fallback, taken up front). `RadioReader` is the input thread (Highest priority). `WinHidDevice` waits on an overlapped `ReadFile`, stamps `HostClock.NowUs()` when the wait returns, then decodes with `HidP_GetData`. `RawStickMap` makes the `StickSample`, written to the flight's `StickInbox` (`RadioReader.Inbox`, set per flight, the thread its only producer). Nothing allocates per report.
    - **Devices:** HID page 1, usage 4, 5 or 8; an EdgeTX or OpenTX radio (1209:4F54 or 0483:5710) is `InputSource.Radio` and comes first; a rescan every 0.5 s while none is open. Controls are ordered by their place in the report, found with `HidP_SetData`, because Windows numbers one main item's data indices backwards (measured on a mouse here: X at bit 16 is index 6, Y at bit 32 index 5) and EdgeTX declares its 8 axes in one main item.
    - **The TX12** (EdgeTX 2.8's source): 24 buttons (ch 9–32), 8 axes 0–2047 (ch 1–8, output + 1024), the mixer every 2 ms in joystick mode, a 1 ms endpoint, so about 2 ms between reports. Raw map until 1.2: AETR on axes 1–4, arm on ch5 above centre (button 1 on a device of fewer axes).
    - **Intervals (design 2, changed):** the header is written before the first step, so it records the reports of the 10 s **before** the flight (`RadioReader.Intervals`, a ring of 32 768 arrival stamps) in a new header entry with the source and the product name (`LogHeader.Input*`, 100 µs bins to 20 ms), which `FlightSummary` prints.
    - **Loss:** a failed read is the loss: a failsafe sample (centred, throttle 0, disarmed, the new `StickSample.Failsafe`) stamped when it was seen. The step record's flags carry it as bit 3 (bit 2 is the core's crash); `FlightSummary.Failsafes` lists each one's step and loss time.
    - **Keyboard:** `KeyboardSticks` (engine-free) and `KeyboardFallback` (physical keys, `Debug.isDebugBuild` only): W / S throttle at 0.5 a second, A / D yaw, arrows pitch and roll at ±0.5, Enter toggles arm, polled once a frame. With `InputSource.Keyboard` the summary prints "never tune from this flight".
    - **Numbers** (no TX12 here: a synthetic stream of EdgeTX's layout and timing, 20–200 µs wake jitter assumed):
      - render rate: on the real input and flight threads at 30, 60 and 240 fps, the stick in force at all 1200 steps (618 reports, the loss at 1.2346 s) equals the timing rule applied to the recording; a frame-bound control at 30 fps had 782 stale steps
      - cable pull: the loss at host 1 234 567 µs disarms the step at 1 235 000 µs, logged, replayed bit for bit
      - the stream's intervals over 10 s: 5000, mean 2.000 ms, 1.821–2.176 ms; the live TX12's: `radioprobe --measure` when one is plugged in
    - **Merged the flight core at 2a99cbc:** `LogFormat.Version` 6 (`ModelVersion` 5, the 1296 B record unchanged), the failsafe in flags bit 3, the input header entry at tag 15 (the core holds 10–14); the goldens did not move.
    - **For 1.4:** `timeBeginPeriod(1)` with the flight thread (p6-flight-core 1.2's note), `RadioReader.Stop` on quit and domain reload, the keyboard only when no radio is connected.
- [x] 1.2 [game-developer] The calibration wizard (design 4) (ticked for its build; the by-hand run is 1.2b):
  - **What:** centre, sweep and arm switch, the channel order, reversal, endpoints and deadband, live bars, named profiles, the health check, and radio-side curves.
  - **Verify:** both calibration scenarios, the TX12 pitfall with fc-profiles' recorded spans, and a real TX12 in under 2 minutes, by hand.
  - **As built** (`Scripts/Input/Radio/`: `CalibrationWizard`, `CalibrationProfile` with `CalibratedStickMap`, `CalibrationStore`, `MiniJson`, `ReportTap`, `IStickMap`; `Scripts/UI/CalibrateScreen.cs`; tests in `Tests/EditMode/Radio/CalibrationTests.cs`):
    - **The flow:** the wizard is engine-free and fed every report with its arrival stamp through `RadioReader.Tap`, so neither the frame rate nor a key enters after Start.
      - centre: every axis still within 2 % for 1 s (sticks let go, throttle down, arm off) gives each rest and its noise
      - the sweep: throttle up, then yaw, pitch and roll, each from one end slowly to the other and held (0.3 s at 80 % or more of the radio's travel, otherwise 2 s, so a pause mid-sweep rarely ends a step)
      - the arm switch on, then off: the axis (a change of 25 % or more) or button that changes, at the midpoint
      - the axis that travels most, at least twice the runner-up, is the stick's: that gives the channel order (any order, written in Betaflight's letters, AETR or TAER), the reversal (the sweep's direction) and the endpoints (its extremes)
      - the deadband is the rest noise in raw counts; Betaflight's own `deadband` still applies in the core
    - **The TX12 pitfall** (EdgeTX 2.8's `evalInputs`, read): fc-profiles' 79 / 1 and 85 / 0 are the radio's own calibration spans. EdgeTX divides by max(100, span), so such an axis reaches full output about 100 ADC counts from centre, of its 683–903, and in the reports it jumps from end to end over the full HID range. Design 4's rule alone (a span under 200 of the HID range) misses that, so the health check has two rules, each "it behaves like a switch: recalibrate the radio", and either blocks saving:
      - a span under 200 of 2048 (design 4)
      - a sweep under 0.7 s from 10 % to 90 % of its travel, twice running; the first time, the pilot is asked to go slower
    - **Radio-side curves:** both are named with what to switch off, and the profile is still saved.
      - a curve: the 10–90 % sweep off its fitted line by more than 3 % of the travel (design 4): expo or a curve
      - a short endpoint (a radio only): more than 5 % short of the full range: dual rates or the weight
    - **The map:** `CalibratedStickMap` replaces `RawStickMap` once a profile is active, and `RadioReader.Map` swaps it whole. It runs piecewise from the centre to each endpoint, with the deadband but no step, and the throttle 0–1 between its endpoints. It keeps a copy of the profile and allocates nothing.
    - **Profiles:** `calibration.json` in `persistentDataPath` holds the active name and the named profiles; it is written to a temporary file and swapped in.
      - a clean calibration is saved under the radio's name and made active at once; renaming and picking take the mouse, after the calibration
      - `MiniJson` is the app's small JSON reader, for 1.3's settings too
    - **The screen:** `CalibrateScreen` is plain IMGUI. 1.4's menu opens it with the app's reader; without one, it opens its own. It shows the prompt, raw bars for every axis and the buttons, calibrated bars through the reader's map, the findings and the profiles.
    - **Numbers** (no TX12 here: a synthetic TX12 through EdgeTX's pipeline and a synthetic pilot, 0.8 s to react and 2 s sweeps):
      - healthy, AETR: done in 19.0 s, endpoints 0–2047, deadbands 1 count
      - TAER, with pitch and throttle reversed on the radio and the arm on a button: all found, 19.0 s
      - the pitfall (spans 79 / 1 and 85 / 0 on roll and pitch, 683–903 on yaw and throttle): roll and pitch named, the other two pass, nothing saved, 23.3 s
      - spans of 79 and 85 in the reports themselves: "spans only 81 / 87 of 2048" (with ±1 of jitter), 22.3 s
      - yaw expo 30 % named, and roll at weight 70 % named as 70 % of the travel
      - a hasty first sweep (0.4 s) of each stick: asked again, then passed, 28.2 s
    - **Assumed thresholds** (2 %, 0.3 / 2 s, 80 %, 0.7 s, 3 %, 5 %, 25 %) are checked at 1.2b. A human's 2 s sweep is less steady than the synthetic one, so the 3 % line is the likeliest to misfire.
- [ ] 1.2b The pilot's, at G7 (with the gate's flights, not a merge blocker), by hand: the pilot calibrates their own TX12 from the wizard's first screen in under 2 minutes, with the radio alone after Start. Any assumed threshold that misfires is retuned from that run.
- [ ] 1.3 [game-developer] Rates and throttle (design 5):
  - **What:** the Calibrate screen's ACTUAL rates and throttle curve with previews from the core's rate function, `FcSettings` under CLI names, the sim default column as defaults, and persistence.
  - **Verify:** the defaults scenario and the persistence scenario.
- [ ] 1.4 [game-developer] The app and the flight session (design 6, 7):
  - **What:** the menu (Train, Calibrate, Exit; Explore in review builds), the setup, the flight session over the test range (D-044) with the world loader, the physics thread, the stand-in drone, the FEED-02 camera and the launch stand; arm and disarm, pause and restart, the end conditions, and the post-flight summary.
  - **Verify:** every `app-flow` scenario, and a built player flying the stand-in from the stand and from the ground with the keyboard fallback.
- [ ] 1.5 [game-developer] Latency (design 3):
  - **What:** the latency test mode.
  - **Verify:** the latency scenario with a TX12, both figures in the header.

## 2. Review

- [ ] 2.1 [qa-engineer] review against spec scenarios: every `tx-input` and `app-flow` scenario run by QA; a TX12 calibration timed; the timing contract with recorded streams at three render caps; the failsafe; the summary against the log; persistence; hygiene
