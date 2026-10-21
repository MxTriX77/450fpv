# Tasks: p6-input-app

Branch `unity/game/p6-input-app` off `unity/main`, after `p6-flight-core`'s task 1.1 (the interfaces) is merged into `unity/main`, or into this branch from its sub-branch. Tasks 1.1 to 1.3 run in sequence. 1.4 needs `p6-world-query-gallery`'s loader (its task 1.3), and 1.5 runs last.

## 1. Build

- [ ] 1.1 [game-developer] The radio (design 1, 2):
  - **What:** HID reading off the render thread for the TX12 and generic joysticks, arrival timestamps, the lock-free ring to the physics thread as `StickSample`, the interval histogram, the USB-loss failsafe, and the keyboard fallback marked in the log.
  - **Verify:** the "render rate does not touch the sticks" scenario with a recorded stream, and the disconnect scenario.
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
