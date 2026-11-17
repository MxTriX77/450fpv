# Tasks: w19-nose-whiskers

Branch `unity/art/physics-integration-2--nose-whiskers`, cut from the physics line. Order: 1.1 and 1.2 together; then 1.3; then 1.4; then 2.1; then 3.1. The pilot's clips and photos are cited by letter only.

## 1. The whiskers and the props

- [ ] 1.1 [reference-analyst] **The geometry, from the two photos and the footage's bars.** No GPU.
  - From photos U and V, for each whisker:
    - its root on the frame, measured from the camera's lens;
    - its length and its diameter class;
    - its direction: the toe-in toward the centre line and the rise;
    - its colour and finish, and what holds it.
  - From the flight clips, by letter:
    - where the two bars sit in the picture (their outer and inner ends, as shares of the width and the height);
    - how they converge;
    - how they move at speed and in a gust (the amplitude in the picture and the frequency).
  - **The fit:** the whiskers' geometry in the drone's body frame that puts the drawn bars where the footage's are, through the feed's lens (FEED-02) with the camera where `drone.json` has it. Where the photos and the fit disagree, say by how much, and which is trusted for what.
  - The props in the same clips: how much of them shows at the picture's top right and left.
  - Writes `docs/reference-notes/nose-whiskers.md`: shape and look only, each number labelled measured, derived or assumed.
- [ ] 1.2 [tech-artist] **The turning props, brought up to the line.** Needs the GPU.
  - `w13-props-in-frame` (its branch `unity/physics/physics-integration-2--props-in-frame`, built on an older base) merged with the line; its tests and its captures run again with the camera where it is.
  - A sheet of the props in the picture against the pilot's account ("a little visible, at the right and left of the picture's top") and 1.1's reading, local only.
  - Its own task list gains the as-built note; its line about the camera's move is replaced by D-117.
- [ ] 1.3 [asset-artist] **The whiskers on the model,** after 1.1.
  - The generator and the params build them as visual-only parts: the mass build and the contact shapes skip them, and `drone.json`'s totals do not move.
  - Both variants (legs on, legs off) still build with one command; the whiskers survive every level of detail the first-person camera sees.
  - The object sheet and the review sheet are redone, with a side-by-side against photos U and V (local only).
- [ ] 1.4 [tech-artist] **The whiskers in the picture,** after 1.3, design first. Needs the GPU.
  - **The shake:** a thin wire cantilever's first bending mode from its size and material, driven by the relative air at the nose and by the frame's vibration, both from what the sim publishes.
  - **The break:** a whisker that meets the world bends away; past its limit it stays bent or parts. A replay shows the same break in the same field.
  - A physics engineer reads anything the sim core gains: no step may read it.
  - The feed's captures with the whiskers beside the footage's bars, and the same flight's log with and without them, identical.
- [ ] Checks for 1.2 to 1.4: Unity's EditMode suite; `dotnet test tools/simtests -c Release` and `flightbench --golden` if the sim core gains anything; `pwsh tools/build_asset.ps1` for the drone's variants; `-smoketest` on a review player; `python tools/hygiene/precommit.py --tree HEAD`; `openspec validate w19-nose-whiskers --strict`.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec's scenarios: the bars' place and convergence against the references, the identical logs, the shake against the wire's own frequency, a break and its replay, the props against the pilot's account, the frame rate.

## 3. The build

- [ ] 3.1 [orchestrator] Merge into the line and the pilot's build branch (no model number: if any flight result moves, stop); the build pointer; tell the pilot.
