# Tasks: w13-props-in-frame

Branch `unity/physics/physics-integration-2--props-in-frame`, cut from `--w3-preview` at 3f1fe31e (the pilot's build of 2026-11-12 plus QA's review of Play). Order: 1.1, then 2.1. It merges back into `--w3-preview`.

## 1. The props

- [ ] 1.1 [tech-artist] The props in the frame.
  - First find out why a blade sits in the upper left of every frame today: where the camera is against the front discs, which blade, and what share of a turn it spends inside the picture. Say whether that share fits the reference note's counts for the sim's airframe group, or whether the camera's place or the model's props need a second look (report it; don't move the camera here).
  - The pose gains each rotor's angle (`FlightState.RotorAngle` into `PoseFrame`, render-side; nothing in the physics, the log or the goldens moves).
  - The view turns each prop to the sim's angle at each field's exposure, in the first-person and the chase view; a rotor at rest shows its blade still.
  - The smear: the rotor's speed times the exposure time the feed already uses (`RollingShutter`), drawn so a sliver reads in bright light and a wedge in dull light. Each number labelled measured / published / derived / assumed.
  - A capture of 24 consecutive fields in a hover, at noon and under overcast, on House and on Village, compared side by side with the reference note's letters in the local-only compare folder. Look at it.
  - Tests: the drawn angle against the sim's over consecutive fields; a rotor at rest; a fixed-input flight logs identically with the model drawn and hidden.
  - Checks: the new tests and the feed's (`Fpv450.Feed.Tests`), `dotnet test tools/simtests` (the pose's new field), `dotnet run -c Release --project tools/flightbench -- --golden`, the review build with its smoke PASS, `python tools/hygiene/precommit.py --tree HEAD`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec scenarios: the angles over consecutive fields, the rest case, the identical logs, the side-by-side sheet against the reference letters, the feed's captures unchanged, the smoke PASS; a physics-engineer reading of the pose's new field.
