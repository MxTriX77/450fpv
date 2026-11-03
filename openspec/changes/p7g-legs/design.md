# Design: p7g-legs

## Context

See proposal.md for why.

**The legs as built** (`drone.json`, `p6-flight-core` 1.6):
- Four `leg_*` cylinders: Ø 0.010 m, 0.25 m long, carbon, 11 g each. The look is letter T's; the material is labelled published for CFRP.
- Four `tjoint_*` boxes: polymer, 8 g each.
- Two `skid_*` cylinders: 0.45 m, 20 g each.
- One spring per leg: stiffness 10 kN/m (assumed, "the plastic clamps, T-joints and skid overhangs, 5–20 kN/m"), damping 45 N·s/m (derived, ζ 0.2).
- The contact model (1.6b's seam rule, D-067's stick spring, `p7a-ground` 1.10's soil) carries each part's force, but no part ever yields.

**The references disagree on the material:**
- Letter T: black carbon tubes in 25 mm clamp blocks with wing-nut bolts (quick release), plastic T-joints and carbon skids.
- The pilot's words (the drone sheet's table): "plastic, 25–35 cm, a little soft".
- The pilot's words beat an interpretation, so the material is a plastic, researched for the class. Letter T keeps the shape and look, since a black plastic tube looks like T's.

**Sim rules:**
- 1 kHz fixed step, DetMath only, 0 B in steady state.
- Randomness only as bounded perturbations of named physical parameters, logged (PHY-24).
- Model and log numbers assigned once (D-072, D-074).

## Goals / Non-Goals

**Goals:**
- Each failure mode the class's legs have, from published material data and simple structural laws.
- A damaged leg that changes the contacts and the build exactly, through the laws that already exist.
- Replay bit for bit, 0 B, inside the budget.

**Non-Goals:**
- Fatigue across flights, or partial damage healing.
- Finite-element detail.
- A leg's own vibration modes. The airframe's modes (`AirframeModes`) stay as they are, re-solved when a leg leaves, as for the coil.

## Decisions

1. **Material from the pilot's words; numbers researched (1.1).**
   - Candidates to research, each with published modulus, yield, ultimate and elongation, and density:
     - the class's plastic leg tubes and printed legs (PA6, PP, PVC, PETG, PA12)
     - the clamps (PLA, PETG, ABS or PA printed blocks; aluminium)
     - the T-joints
   - The choice and its source go in `drone.json`'s `legs` block. Every value is labelled; none is asked (D-024).
   - *Alternative:* keep T's carbon. Rejected: carbon is brittle and stiff, so it would almost never bend, against the pilot's "a little soft" and their D-079 account.

2. **Loads from the contact model, no new contact code.**
   - Each step the contact model already sums each leg part's force. From the foot force **F** (at the T-joint) and the clamp point, the law forms:
     - the moment at the clamp, M = |r × F|, with r from the clamp to the foot (about 0.21 m out of the clamp, derived from the 0.25 m tube)
     - the axial force F_a = F·û along the leg
     - the twist about the leg from the skid's lever (the skid's 0.126 m overhang, derived)
   - *Alternative:* a separate impulse estimate at touchdown. Rejected: it misses sustained loads (a skid caught in a groove while the drone drifts) that the contact model already has.

3. **The failure modes and their laws** (each capacity labelled; strengths drawn per flight, step 6):
   - **Yield of a plastic leg tube.**
     - Elastic-perfectly-plastic bending of a hollow circular section: M_y = σ_y·I/c, M_p = σ_y·Z_p, with I = π(D⁴ − d⁴)/64 and Z_p = (D³ − d³)/6 (closed forms).
     - Past M_y, the curvature beyond the elastic one stays as a set angle θ_set at the clamp's root, integrated over the step. On unloading, the elastic part springs back.
     - θ_set moves the leg's axis, so its tube and foot shapes rotate about the clamp.
   - **Clamp rotation and slip.**
     - Friction capacity from the wing-nut bolt's preload, P = T/(K·d): T is the hand-tight torque (published range for wing nuts), K the nut factor (published), d the bolt's diameter.
     - Slip along the tube when |F_a| > 2μP (two jaws). Rotation of the leg in the clamp when M > μ·P·D (the jaws' couple).
     - A rotation leaves a set angle, as yield does. Slip past the clamp's length means the leg comes off (detach).
     - μ is a published plastic-on-plastic friction value.
   - **Buckling** (added after 1.1: it is the first mode of a level or gripping landing, 385 N along the leg on 1.1's PVC-U tube).
     - A leg loaded along its axis past its buckling load (the clamped–pinned column's Euler load from E, I and the free length, its end fixity labelled) bows sideways.
     - Past the bow's own plastic hinge (M ≥ M_p at the bow's peak, from the bow's amplitude), it keeps a set angle as yield does. Below that, it springs back elastically.
     - Without this mode, "A harsh landing bends a leg" cannot happen on a level landing.
   - **Crack, then parting** (restated after 1.1: the tube is ductile PVC-U, so it tears by strain, not by a brittle stress).
     - A crack at the clamp's edge, the stress raiser (K_t, 1.1's labelled bounds), starts at the edge's first yield, M ≥ M_y/K_t. It halves the section's stiffness (1.1's labelled factor) and lowers M_y.
     - The tube parts when the strain at the clamp's edge, the set curvature's κ·c times K_t, passes the material's elongation at break (1.1's published value and bounds). It never parts at a stress ahead of its set, as a brittle law would for any K_t above 1.45.
   - **The foot's side give in series with the ground's grip** (added after 1.1). The leg's own side stiffness at the foot (262 N/m, 1.1) sits in series with D-067's stick anchor (0.5 × the normal spring, about 26 kN/m), so a gripping foot loads the clamp through the leg's compliance and never through a spring 100× stiffer than the leg.
     - This is a contact-law change: the model moves and the log format with it, by the numbers assigned at 1.2.
     - The leg contact shapes' penetration spring (6600 N/m) stays as it is.
   - **T-joint.** Pull-out and rotation of the skid in the T-joint, by the same friction law; a plastic T-joint's fracture past its section's capacity.
   - **The axial load softens the side give (P·Δ, added after 1.2).** A leg carrying an axial load P loses side stiffness by its geometric stiffness, P over the leg's free length (with the end-fixity factor of the buckling law). At about 47 N a leg's side give would vanish, and every landing's peak passes that. So a heavily loaded leg is free to sway or fold sideways, and the side give never goes below zero: past it the leg is in the buckled branch.
   - **The joints' friction damps the sway (added after 1.2).** The clamp jaws and the T-joint slip a little under side load. Their Coulomb friction, from 1.1's preloads and μ, is a damping source in series with the tube's own loss factor (η 0.03, published). It is modelled, never replaced by a tuned ζ. The sway's decay after a landing in wind is printed, against 1.2's 4.6 s with the tube's loss alone.
   - All laws are closed forms in + − × ÷ √. No transcendental functions are needed.

4. **A broken or detached piece is a loose body.**
   - The parted piece (the tube below the break, with its T-joint, and its skid if the T-joint lets go) leaves with the drone's velocity at its centre of mass.
   - It flies as a free rigid body under gravity and drag, through the existing free-body integrator (`flightbench`'s free body), and rests on the ground through the soil contact.
   - It is logged at 50 Hz in the side record (as `p7d`'s line is), never part of the drone's step.
   - *Alternative:* delete it. Rejected: the pilot sees a leg drop, and replay must show where it went.

5. **The build is rebuilt exactly when mass leaves** (the coil's rule, `p7d-tether` 1.6):
   - mass, centre of mass and inertia from the remaining parts' mass build
   - the body's momentum and angular momentum kept through the change
   - `AirframeModes.Rebuild` called

6. **Randomness.**
   - One stream per leg, `leg.strength.<leg>`, drawn once per flight.
   - Each leg's σ_y, σ_u, clamp preload and μ is scaled within its published spread (uniform, bounded, the bounds labelled).
   - Logged in the header with the bounds.
   - `p7a-ground` 1.17's `leg.stiffness` stream stays its own; the two never draw from each other.

7. **Logging** (numbers assigned when the code lands, D-072 and D-074):
   - The header holds the legs' table hash and each leg's draws.
   - The step record holds each leg's state (intact, set, cracked, broken, detached), θ_set and stiffness factor.
   - Events: `LegSet`, `LegCracked`, `LegBroken`, `LegDetached` and `SkidLoose`, each with the load, the mode and the contact's object.

8. **A damaged leg in flight.** The contact model reads each leg's current shapes and stiffness:
   - A set angle rotates the leg's tube, T-joint and skid end about the clamp.
   - A broken leg keeps its stub above the break, as a shorter tube with no foot.
   - A detached leg's parts leave the contact set.
   - D-067's landing law and the soil (`p7a`) then act on what is left, unchanged.

## Risks / Trade-offs

- **The material choice decides everything.** A soft plastic bends often; a stiff one breaks. 1.1's research must show that the class's legs really are what the pilot describes, and print the landing speeds at which each mode starts, so QA can judge them against the pilot's D-079 account ("falls a bit more often"). Nothing is tuned to a rate.
- **The clamp's preload is hand-set.** A wing nut's torque varies widely. Its bound is the main uncertainty for clamp rotation and detachment; 1.4's harness prints the sensitivity.
- **The stress concentration at the clamp's edge** is borrowed from published clamped-tube values. The same uncertainty goes for the crack threshold.
- **Interaction with `p7a-ground`'s leg spread (1.17).** That task draws each leg's stiffness; this change multiplies it by the leg's damage factor. The two draws stay separate streams.
- **Cost:** one moment and a few comparisons per leg per step, plus a loose body at 50 Hz while one exists. It should be well under a microsecond; 1.4 measures it.
