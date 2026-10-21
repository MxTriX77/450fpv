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
    - **Loss:** a failed read is the loss: a failsafe sample (centred, throttle 0, disarmed, the new `StickSample.Failsafe`) stamped when it was seen. The step record's flags carry it as bit 2 (`LogFormat.Version` 2); `FlightSummary.Failsafes` lists each one's step and loss time.
    - **Keyboard:** `KeyboardSticks` (engine-free) and `KeyboardFallback` (physical keys, `Debug.isDebugBuild` only): W / S throttle at 0.5 a second, A / D yaw, arrows pitch and roll at ±0.5, Enter toggles arm, polled once a frame. With `InputSource.Keyboard` the summary prints "never tune from this flight".
    - **Numbers** (no TX12 here: a synthetic stream of EdgeTX's layout and timing, 20–200 µs wake jitter assumed):
      - render rate: on the real input and flight threads at 30, 60 and 240 fps, the stick in force at all 1200 steps (618 reports, the loss at 1.2346 s) equals the timing rule applied to the recording; a frame-bound control at 30 fps had 782 stale steps
      - cable pull: the loss at host 1 234 567 µs disarms the step at 1 235 000 µs, logged, replayed bit for bit
      - the stream's intervals over 10 s: 5000, mean 2.000 ms, 1.821–2.176 ms; the live TX12's: `radioprobe --measure` when one is plugged in
    - **For 1.4:** `timeBeginPeriod(1)` with the flight thread (p6-flight-core 1.2's note), `RadioReader.Stop` on quit and domain reload, the keyboard only when no radio is connected.
- [ ] 1.2 [game-developer] The calibration wizard (design 4):
  - **What:** centre, sweep and arm switch, the channel order, reversal, endpoints and deadband, live bars, named profiles, the health check, and radio-side curves.
  - **Verify:** both calibration scenarios, the TX12 pitfall with fc-profiles' recorded spans, and a real TX12 in under 2 minutes, by hand.
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
