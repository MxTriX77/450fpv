# Pilot answers on the wind clip (P)

These are the pilot's answers to `wind.md` §5.7 (2026-09-23), close to verbatim. **They win over the notes' assumptions.** The pilot's standing direction for wind: it's real life, unexpected and undefined, and the simulator must be the same.

| # | Question | Answer | Consequence for the notes and model |
|---|---|---|---|
| Q1 | Camera uptilt | **0°** | Drop the ~25° uptilt inference. The roll–yaw rate correlation (+0.57) must be explained another way; see Q7. Re-derive the motor margin with 0° uptilt, where the earlier estimate was about 1.10× weight. The 22.7° camera-below-horizon pitch is then the airframe's forward pitch. |
| Q2 | Wind direction and strength | Unknown strength. **Mostly from the left, but it randomly blew from other sides too.** "It's real life, unexpected and undefined." | Matches the measured steady left lean. The wind model needs a prevailing direction **plus random direction changes**, never a fixed vector. |
| Q3 | Flight mode | **Acro (rate)** | The simulated pilot for the §6 targets flies in rate mode. The attitude residual is what's left after the pilot's own stabilisation, with no self-level controller. |
| Q4 | Mass and props | **Viriy 10 Opto** (10" props), with a **fiber coil of about 1 kg** and **about 800 g of cargo and battery**. The pilot noted "maybe 100–200 g", with uncertain meaning. The frame's mass is unknown. | Total take-off mass is uncertain. State it as a range, with the frame and motors from the drone class as an assumption, and propagate it to the thrust margin and inertia. |
| Q5 | Height and speed | **Varied height** | Keep the height-dependent turbulence (surface layer, tree-belt shear) as a range. |
| Q6 | Flight duration | **About 10 minutes.** The 47 s clip was **the toughest part** of the flight. | The §6 targets describe the worst stretch of a severe-wind flight, not a whole flight. Say so, and consider a milder "typical windy" band. |
| Q7 | Was the tree belt worse? | **Yes.** "I couldn't go ahead and correct my horizon. Either you yaw but lose direction, OR you roll and keep direction but lose the horizon." That's why this clip was chosen. | This explains the roll–yaw coupling as the **pilot's control trade-off** in a strong crosswind near trees. The model and targets must make that trade-off unavoidable: the roll authority needed to hold the horizon competes with holding the heading. |
| Q8 | Cause of the picture losses | Unknown; the pilot was about 10 km away. **Hypothesis: wind shakes the optic fiber, making it noisier and flashier, and trees plus wobbling stretch it.** | Feed dropouts (N12) and extra noise get a **fiber tension and vibration** driver, strongest in wind and near trees. This links the tether model to the video feed. |
