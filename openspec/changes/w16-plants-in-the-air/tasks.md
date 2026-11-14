# Tasks: w16-plants-in-the-air

The change lives on the physics line, `unity/physics/physics-integration-2`, from cbe016ee (model 100, the step-budget fixes and 1.1's research); each task on a branch of the line (1.2: `--w16-plant-law`). Order: 1.1; then 1.2 and 1.5; then 1.3 and 1.4; then 2.1; then 3.1. The line merges into `--w3-preview` for the pilot's build.

## 1. The plants in the air

- [x] 1.1 [reference-analyst] **The plants' numbers, published.** For each plant class the farmland draws (its grass, straw and weed prefabs, the hedges' shrubs, and each tree species' trunk, branches, twigs and leaves):
  - the first bending modes' natural frequencies and damping ratios, the flexural rigidity, the drag's reconfiguration (Vogel's exponent), leaf flutter, and how each scales with size;
  - the rotor downwash on crops and grass from agricultural UAV research: the downwash field and the canopy's deflection under it, by height and thrust;
  - the dust the wash lifts from dry ground: published threshold friction velocities per soil (dust entrainment, rotorcraft brownout), and how the wall jet's speed at the ground relates to them;
  - each number with its source, labelled measured / published / derived / assumed.
  - **Clip H's ending** (the pilot pointed to it: "towards the end you will see short examples"): what it shows of grass cut by the props, grass swaying in the wash, and dust whiffs as the drone lands: the heights, the extents, the timing and the motion, measured where the frames allow, by letter only.
  Writes `docs/reference-notes/plants-in-the-air.md`. No GPU.
  - **As built** (461e3013, 9bb54524, e2f513a8 and the link): clip H's ending re-extracted at 30 frames a second on the CPU. It shows no piece leaving a disc; it shows standing dead stalks fluttering within about 1 m of a 0.3–0.6 m creep, dead leaves and loose straws thrown up and forward at 1–8 m/s, tumbling, 4–10 frames each, no dust from its damp dark soil, and a pale, soft last 0.4 s nosing into a straw mat (dust, chaff or blur: not separable). Clip F is the dust reference: a continuous ochre veil from dry loam at 0.5–1 m. The note gives per class the first mode from the section (grass culms 0.4–2.4 Hz, straw with an ear 1.0–1.9, weed stalks 0.6–13, twigs 3.5–15, branches 1.0–1.5, pines 0.4–1.3 by the published dbh/H² law, broadleaves 0.3–0.6 assumed), the published damping (wheat shoot 0.08; broadleaves 0.086 in leaf, 0.039 leafless; conifers' internal under 0.05) and the air's own damping (grass overdamped from about 3 m/s), the Vogel exponents (−2/3 blades, −1 cones and dense crowns), leaf flutter (6 ± 1 Hz), the wash's flattened patch and ring by hover height, the dust thresholds per surface with the wall jet's friction velocity and the onset heights, and each element's push on the drone (a twig about 900 times a green culm). The gaps and the assumptions proposed are its §7; what 1.2 needs first, §8.
- [x] 1.2 [physics-engineer] **The law,** design first, after 1.1. No GPU.
  - Each plant class is damped modes driven by the drag of the sim's air (`WindField` and `RotorAir`) at its height, with reconfiguration; give its response to a step gust and to the wash.
  - A stem's fold under the wash (`PropStems`) and its drawn bend stay one law.
  - The code: an engine-free model in the sim core that the drawn side samples each frame and the physics uses where a plant touches the drone (deterministic, DetMath, 0 B).
  - Hand calculations at a gust and at a hover over grass.
  - **As built** (design.md "## 1.2"; `tools/flightbench/handcalc/plants_air.py`):
    - **The law** (`PlantLaw`, `PlantMechanics`): each element is one bending mode, the props' stems' own rigid link on 4EI/L (stems from the cover rows; twigs, branches and trees labelled in the design). It is driven by the quasi-steady drag on the air relative to it, so ζ = ζ_s + ζ_a comes out of the drag and one law holds both regimes. The link's projected drag gives Vogel's −2/3 for culms near 0.5 rad; foliage takes −1 past 4 m/s. The step is exact-exponential and linearly implicit.
    - **The wind** (`PlantAir`): in closed form, each mode of the frozen field taken at its encounter frequency, statistically linearised (Borgman's closed form for bare stems, Gauss–Hermite with foliage). A sample is stateless. The plants' reading of the field matches `WindField.At` within 1.2e-12 m/s over the farmland, and the closed form correlates 0.99 with the link driven step by step in the field.
    - **The wash** (`PlantWash`): a world-anchored grid around the drone, 25 Hz ticks from the published poses (`PoseFrame.RotorThrust` added; no flight result moves). It runs the line's `RotorAir` at 11 heights a node; a fine and a coarse grid together show the columns' patch and the ring.
    - **The dust** (`DustLaw`): the wall jet's u* against each surface's threshold, Fécan's wet factor, White's flux.
    - **The hand calculations hold:** the 1 s gusts within 0.0001 rad at 1 ms (0.02 as the grid steps them); the hover at 0.5, 1, 2 and 4 m within 0.03 rad (0.05 where the columns fold it); the dust's onset heights per surface, dry and wet, exactly.
    - **Cost:** 10–20 µs a CPU sample, and about 8 ms a tick for a 32² grid of 4 bands (20 % of a core at 25 Hz, while the wash lands at 3 m/s or more), 0 B, deterministic.
    - **Not met** (design.md §10): clip H's flutter at a creep (design 19's near field: 2.14's); dust from wet bare loam below 0.5–2 m against H's damp soil and the spec's "no dust in rain" (the orchestrator's call); leaf flutter's amplitude; the dense drawn field needs the closed form ported to the GPU (1.3).
    - **PropStems is unchanged:** design.md §6 says what would move were it to take this law's second-order dynamics.
- [ ] 1.3 [tech-artist] **The drawn plants move in that air,** after 1.2. Needs the GPU.
  - The friend's vegetation shaders (grass, leaves, bark) take the sim's field and wash instead of their own wind, near and far: a field texture around the camera and each plant's modal response.
  - The wash's flattening ring under the drone.
  - The frame rate holds (PLT-02).
- [ ] 1.4 [tech-artist] **The cut pieces and the dust on camera** (the pieces were `w9-village-physics` 2.5). Needs the GPU.
  - From `FeedSignals`' cuts per rotor, pieces leave the front discs at the blade's speed, drawn in front of the camera; a replay draws the same pieces.
  - Dust whiffs lift from dry ground where the wash's speed along it passes the surface's threshold (1.2 gives the law and the thresholds), and grow as the drone comes lower.
  - Checked against clip H's ending.
- [ ] 1.5 [physics-engineer] **The drone feels what it flies through** (`w9-village-physics` 2.10, extended), after 1.2. No GPU.
  - The stems' forces on the legs and the body: touchdown jitter and drag when skimming weeds (PHY-05).
  - A crown's leaves and twigs on the body and the props.
  - The stems' wash on, after `w9-village-physics` 2.14.
  - The pilot's "more heavily" for twigs and leaves than for grass is computed, not tuned.

## 2. Review

- [ ] 2.1 [qa-engineer] Review against the spec's scenarios:
  - a gust's bend and return against the law's period and decay;
  - the wash's ring under a hover at 0.5, 1, 2 and 4 m;
  - the drawn plants against the physics' (AST-10);
  - the drone's drag in grass and in a crown;
  - the frame rate and the step budget;
  - side by side with the pilot's references where they still exist.

## 3. The build

- [ ] 3.1 [orchestrator] Merge into `--w3-preview`; the build pointer on the trunk; tell the pilot the command.
