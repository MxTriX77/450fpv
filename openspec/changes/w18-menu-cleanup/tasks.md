# Tasks: w18-menu-cleanup

Branch `unity/game/physics-integration-2--menu-cleanup`, cut from the pilot's build branch at 88acbec1. One agent, in order. Captures go to the session's `feedback-1117/menu/` folder, `before/` and `after/`.

## 1. The menu and the screens

- [ ] 1.1 [game-developer] This change: the proposal, the tasks and the deltas on `app-flow`, `play-maps`, `world-map` and `farmland-world`; `openspec validate w18-menu-cleanup --strict`.
- [ ] 1.2 [game-developer] The smoke test's shots of the screens it never shot: the loading screen, and the Calibrate screen's Radio and Rates tabs (opened from the menu, closed by Back). A review player built before 1.3 and its `-smoketest -smokeshots` give the `before/` captures.
- [ ] 1.3 [game-developer] No Explore.
  - The menu offers Play, Calibrate and Exit in every build (`MenuScreen`, `AppFlow`).
  - List what still reaches the farmland's noclip view; delete what only the menu's item used.
  - The smoke test opens the noclip view through the entry that remains, keeping each check about the farmland there.
- [ ] 1.4 [game-developer] No describing labels: a verdict on every text the app draws (`Scripts/UI`, `Scripts/App`) by the rule in `app-flow`'s new requirement; the layout closes up where a text went.
- [ ] 1.5 [game-developer] The checks.
  - The EditMode tests that pin the menu and the screens, at the new texts.
  - A review player (`Fpv450.Build.Windows -review`) and its `-smoketest` exit 0, with `-smokeshots` giving the `after/` captures at 1920 × 1080: the menu, the carousel, the setup, the loading screen, the flight's first seconds and the Calibrate screen.
  - `python tools/hygiene/precommit.py --tree HEAD`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec scenarios: no Explore in any build, every check the smoke test made in noclip still made or replaced, every removed and kept text against the rule, the captures before and after.
