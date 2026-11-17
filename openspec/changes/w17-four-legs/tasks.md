# Tasks: w17-four-legs

Branch `unity/physics/physics-integration-2--w17-four-legs`, cut from the physics line. Order: 1.1 and 1.2 together; then 1.3 (after `w16-plants-in-the-air` 1.5's follow-up has landed in `Runtime/Contact/`); then 1.4; then 2.1; then 3.1.

## 1. The four legs

- [ ] 1.1 [physics-engineer] **The baseline from the pilot's flights, and the design.** No change to a flight yet.
  - **Measure the pilot's own flights** (the logs since the model 103 build, about 30). For every liftoff:
    - the throttle and the thrust over weight at the first motion;
    - how far the drone slid before it left the ground;
    - its vertical speed as the last contact let go (the "jump");
    - which contact held it, and with what force.
  - For every touchdown:
    - the touch speed and the peak acceleration;
    - each rebound's height;
    - the slide and the time to rest;
    - what the legs did.
  - Say in numbers what "a magnet" and "a light plastic plane" are in these flights.
  - **Design** (`design.md`):
    - a leg's tip in each surface's soil: its penetration under the weight and at a touchdown, from the surface table's own soil law;
    - the hold on the way out (the shaft's friction and adhesion, storing nothing);
    - a dug-in tip's resistance to sliding;
    - the start's embedment per leg as a bounded draw of a named physical parameter;
    - what the legs' failure law reads without the skids' frame.
  - **Hand calculations** for this drone's mass:
    - a careful liftoff from sod, tilled soil, dry crust and asphalt (each leg's release);
    - touchdowns at 0.5 and 1.5 m/s on the same four (the penetration, the rebound, the energy's split);
    - the same for a wet set.
  - Each number labelled measured, published, derived or assumed.
- [ ] 1.2 [asset-artist] **The model on four separate legs.**
  - The skid rails leave the generator, the params and `drone.json`.
  - Each leg ends in its own tip as photos T and U show it.
  - The legs' contact shapes and masses are derived.
  - Both variants (legs on, legs off) still build with one command.
  - The object sheet and the review sheet are redone, with a side-by-side against photos T and U (local only).
- [ ] 1.3 [physics-engineer] **The law, built,** after 1.1 and 1.2.
  - The tips' penetration, hold and sliding resistance in `Runtime/Contact/`; the start's embedment drawn and logged.
  - A switch that flies the previous model's ground law with the skids' drone (a fixture) for the kept audits, in a header entry of its own.
  - Tests against 1.1's hand calculations:
    - two seeds start differently, and a replay is bit for bit;
    - a careful liftoff releases leg by leg and adds no upward speed at release;
    - a touchdown's rebound and slide stay within the calculation.
  - The goldens are re-recorded once and audited case by case.
  - The pilot's account in PHY-03 holds (sometimes on firm dry ground, more often on softer ground, most of the time in rain).
  - 0 B in steady state; the step's cost is measured against PLT-04.
- [ ] 1.4 [game-developer] **The player on four legs,** after 1.3.
  - The drone's view and leg placement without skids.
  - The leg shots of the smoke test.
  - The summary's names.
  - The start standing in the ground as the flight drew it.
- [ ] Checks for 1.3 and 1.4: `dotnet test tools/simtests -c Release`; `flightbench --golden`, `--landing`, `--legs`, `--ground`, `--slopes`, `--contacts`; Unity's EditMode suite; `python tools/hygiene/precommit.py --tree HEAD`; `openspec validate w17-four-legs --strict`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec's scenarios. Re-fly the pilot's liftoffs and touchdowns on their own sticks and compare with 1.1's baseline. Check that nothing outside the ground contact moved beyond the drone's new mass. Look at the drone on its legs in the game.

## 3. The build

- [ ] 3.1 [orchestrator] Merge into the line with its model number, then the pilot's build branch; the build pointer; tell the pilot.
