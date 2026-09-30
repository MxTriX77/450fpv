# Design

Every number below is labelled **published** (a cited public source), **pilot** (the pilot said it), **derived** (computed here or in the notes from published or pilot figures), **assumed** (a plausible engineering value with no source) or **constant** (a modelling choice, not a measurement). §12 collects every assumed figure that matters into open questions, and marks the ones only the pilot can answer.

## Context

- `world-query` is merged and archived. It answers ground, cover, contacts, rays, the wind grid and gaps, deterministically, allocation-free, in pure C#, with a content hash and a `QueryVersion`. Its measured worst-case composite step is 34.7 µs median against a 60 µs budget. Nothing consumes it.
- This role has already signed off two pieces of this model inside `define-map-format`, and they are carried forward rather than redesigned:
  - the leg–ground contact solve (`surfaces-review.md` §1): the elasto-plastic Winkler soil with an unload ratio, the mat in series, stick-point friction, ploughing, and the footprint ring
  - the response to a swept contact with `Time` < 1 (`api-review.md` §4): no rewind and no sub-stepping; fix the contact plane at first touch, refresh it only on a same-side `Time` = 1 contact, release on separation, and log a `tunnel` event if a part ends up through an object
- `wind.md` §6 is a ready-made acceptance suite: 20 numbered targets with bands, a scripted simulated pilot, a test route and a measurement script. The wind part of this change is graded against it rather than against new criteria.
- D-011 fixes the pilot's weather setting to Calm / Windy / Severe, with the wind inside a level still alive and drawn per flight.
- `wind.md` §6.4 explicitly hands one thing to this change: Calm's floor, "never perfectly still", is the airframe's own asymmetry, and it belongs to this change's acceptance, not the wind notes'.

## Goals / Non-Goals

**Goals**
- Each of the manifesto's six complaints is a requirement with a scenario a QA engineer can run.
- Disturbances are physical: a force or a moment with a cause, never a term added to the pose or a filter on the output.
- Bit-identical replay from a log, on another machine, without Godot.
- The whole model, with the full world loaded, holds ≥ 1 kHz inside its budget on the dev machine.

**Non-Goals**
- Blade-element aerodynamics, a CFD wake, or a flexible airframe. Momentum theory with correction factors, plus a wake model on the 2 m grid, is the fidelity ceiling here.
- Tuning to the pilot's taste before the pilot has flown it.

## 1. Frames, state and the step

**Frames.** World is Godot's: right-handed, **Y up**, metres, matching `world-query` (`surfaces-review.md` §6.2). Body axes are x forward, y up, z right, origin at the arm-plane centre — not the centre of mass, which moves as the coil pays out. Attitude is a unit quaternion; Euler angles exist only for logging and for the simulated pilot, in the ZYX order `wind.md` §6.1 uses, so the `roll_deg` / `pitch_deg` / `yaw_rate_dps` columns match the tracker's definitions exactly.

**State** (the integrated part): position and velocity in world, orientation quaternion, body angular velocity, per-rotor angular speed, ESC command filter states, pack state of charge, per-leg plastic sink and stick-point anchors, hook attachments by element `Id`, spool length paid out, tether node positions and velocities.

**Integrator.** Semi-implicit (symplectic) Euler at a fixed `dt = 1/1000 s`, chosen because `surfaces-review.md` §1 already showed the contact stiffnesses are stable with it: the stiffest soil branch gives ω·dt ≤ 0.10, and the stiffest body contact (frame on steel rails, ~1e6 N/m) gives ω·dt ≤ 0.7. RK4 was rejected: it needs four force evaluations, so four sets of world queries, for a step this small, and it does not help stiff contacts.

**Rotor speeds** integrate with the same step but use an exact exponential-free first-order update, so no transcendental function enters the loop (see §9).

**Renderer decoupling.** The model runs on its own thread and publishes a double-buffered pose. The renderer interpolates between the two most recent published states. Godot's `_physics_process` never integrates the drone. If the model thread falls behind, it logs a `hitch` event and catches up with real steps — it never takes a longer step, because a variable step is not reproducible.

**Sub-stepping.** None. Stiffness that would demand it is instead put in series with the drone's own compliance, as `surfaces-review.md` §1 requires (rigid materials never enter the step directly).

## 2. Mass, inertia and the pilot's settings

The pilot sets **payload**, **legs on/off**, **time of day** and **weather**. Only the first two touch the flight model.

Mass and inertia are **built from the component model of `wind.md` §5.5** (assumed layout, derived inertia) rather than tuned as three numbers, for three reasons: the centre of mass moves as the coil pays out, the payload sits forward and below so it changes pitch inertia more than yaw, and the legs-off configuration removes four members. The build is a sum over parts with the parallel-axis theorem, re-evaluated whenever a part's mass changes (payload set, coil paid out, cargo released).

Reference values it must reproduce (all **derived** in `wind.md` §5.5 from an **assumed** layout):

| Quantity | Nominal (4.8 kg) | Class range |
|---|---|---|
| Take-off mass | 4.9–5.5 kg typical | 4.4–6.5 kg |
| Roll inertia | 0.044 kg·m² | 0.033–0.057 |
| Pitch inertia | 0.054 kg·m² | 0.040–0.072 |
| Yaw inertia | 0.050 kg·m² | 0.043–0.060 |
| Centre of mass | 1–2 cm below the arm plane, 1–3 cm forward | rises ~1 cm above it with an empty coil |

Pitch exceeds roll because the cargo is forward; yaw is only 1.0–1.3 × roll because the battery above and the coil below add little to it. **This asymmetry is not cosmetic** — it is why the heavy quad answers a pitch input differently from a roll input, which is the inertia half of manifesto §2.4.

**Mass loss in flight.** The coil sheds up to about 1 kg over a flight (`airframes.md`, derived). The pack's mass does not change.

## 3. Propulsion

The chain per rotor is **sticks → rate controller → mixer → ESC → motor → rotor → thrust and torque**. The order matters: asymmetry injected at the motor is something the controller partly fights and never fully cancels, which is exactly the pilot's §2.4 experience. Asymmetry injected at the output would be cancelled by the controller and vanish.

**Rate controller.** Rate (acro) mode only. Sticks command body rates p, q, r; a PID per axis drives the mixer. This is modelled on a Betaflight-class controller because that is what these airframes fly: a PID with a D-term low-pass, an I-term windup limit, and no self-levelling. Gains are **assumed** (§12 O-9).

**Mixer.** Standard X quad. Motors sit 0.160 m from the roll and pitch axes for a 452 mm wheelbase X frame (**derived**, `wind.md` §5.5). Yaw comes from the torque difference between the two counter-rotating pairs, and `wind.md` §5.5 shows yaw is the axis that saturates first — it needs 4–8 × the differential thrust roll does. The mixer must therefore saturate honestly (clip, and desaturate by scaling the collective) rather than silently exceed a motor's limit, or the model will feel more authoritative than the real drone.

**ESC and motor lag.** A command reaches the rotor through two first-order lags: an ESC command filter and the motor–rotor mechanical time constant τ_m = J_rotor · ω / T. This lag is the reason a heavy quad feels "behind the sticks" and it is the reason asymmetry produces a slow wander rather than a buzz. Both constants are **assumed** (§12 O-2).

**Thrust and torque.** From momentum theory with a static figure of merit, as `wind.md` §5.5 derives:
- T = k_T · n², with k_T set so that at full throttle on a loaded pack the motor gives its derived maximum
- per-motor realistic maximum **2.3–3.1 kgf** (nominal 2.6), **9.0–12.5 kgf** total (**derived**; FM 0.55–0.65 and η 0.80–0.85 **assumed**)
- rotor drag torque Q = κ · T, with **κ = 0.015–0.02 N·m per newton** (**assumed**)
- thrust falls with axial inflow: derate 0.80–0.95 at 9–25 m/s through discs tilted 22.7° (**assumed**), which is what makes a fast cruise cost more throttle
- disc area A = 0.0507 m² for a 10 inch rotor (**derived**)

**Battery sag.** V = 6 · (OCV − I_cell · R_cell), with OCV 3.4–4.1 V per cell across the usable charge and **R_cell ≈ 15 mΩ** (**assumed**, `wind.md` §5.5). Sag is not a detail: at the heavy corner on a sagged pack there is no thrust margin left, and a pilot who pulls full collective to arrest a descent finds less thrust than they expected. The pack's state of charge integrates current over the flight, so a long flight ends heavier-feeling than it started.

**Ground effect.** Thrust rises as a rotor nears a surface. The model uses the Cheeseman–Bennett form T/T∞ = 1/(1 − (R/4z)²), clamped, where z is the rotor's height above the surface found by the downward ray `world-query` already provides per rotor. It is per rotor, not per airframe, so a drone over a step or a rubble pile gets **more lift on one side than the other** — a rolling moment near the ground, which is part of the liftoff shake. Above about z/R = 2 the effect is negligible. Ceiling effect over a roof uses the same ray upward.

**Vortex ring state.** A rotor descending into its own wake loses thrust and the loss is unsteady. This is modelled as a thrust multiplier and an added unsteadiness that switch on over a band of descent rate normalised by induced velocity, with hysteresis so it does not chatter at the boundary. VRS matters for a cargo quad because the natural way to lose height with a heavy load is a straight vertical descent, which is the condition that provokes it. The onset band is **assumed** (§12 O-6).

**Prop wash.** Each rotor's wake is a downward jet. Where the jet hits the ground it spreads, so in close hover a rotor sees the neighbouring rotors' recirculation. This is modelled as a small inflow perturbation per rotor from the other three, scaled by proximity to the ground, and it is one of the reasons hover near the ground is never still.

## 4. Airframe aerodynamics

Rotor forces alone cannot produce the pilot's horizon–heading trade-off. `wind.md` §5.5 and target T11b are explicit: a gust must be a **real sideways force**, not a moment, or the drone can hold its line without tilting the thrust and the trade-off disappears. So the model carries:

- **Body drag** on the relative wind (velocity minus local wind), with separate along and across areas, applied at the body's centre of pressure
- **Coil drag** applied at the coil, which sits **8–9 cm below the centre of mass** (**derived**, `wind.md` §5.5). A side gust on the coil is therefore also a rolling moment — the drone rolls into the wind on its own, before the pilot touches anything
- **Rotor edgewise drag** (H-force) on the discs, which grows with airspeed and is part of why cruise needs forward tilt
- **Weathervaning**: the offset between the centre of pressure and the centre of mass yaws the airframe toward the relative wind

The reference for the whole set is `wind.md` §5.3: about **1.8° (0.95–2.6°) of lean per m/s of crosswind** at the derived airspeed (**derived**). Drag areas and coefficients are **assumed** and tuned to hit that (§12 O-3).

## 5. Ground contact

The soil, mat, cover, friction, ploughing and footprint model is **exactly** `surfaces-review.md` §1, already signed off in `define-map-format`, consuming `surfaces.json` and `MicroDetailNear` with no new fields. It is not restated here; the spec delta references it and the tasks implement it. What this change adds around it:

**Legs.** Four plastic legs, **25–35 cm, slightly soft** (**pilot**), modelled as a spring–damper in series with the ground so the stiff soil branches never enter the step directly. Their stiffness and damping are **assumed** (§12 O-4) and they dominate the bounce on hard ground.

**The landing bounce (manifesto §2.6).** Nothing special is added for this: it falls out of leg compliance plus residual thrust. Touch down on a firm surface with the sticks held at the descent sweet spot and the legs compress, store energy, and return it while the rotors are still producing near-hover thrust — so the drone leaves the ground again. Cut the throttle and it stays. The spec's scenario therefore tests the **pilot-visible consequence**: holding leaves, cutting stays, with a measured separation. That is the honest test, because any tuning that removes the bounce also removes the pilot's skill.

**A leg sticking at liftoff (manifesto §2.1).** Two physical causes, both already available:
- a **hook**: a straw or twig with `Hooks` true and a `HookRelease` force from the world's own draw, attached by element `Id`. `api-review.md` §7 measured the `belt_straw` hook rate at 21.5 % per landing leg, about **0.9 hooked legs per liftoff** (**derived, and due for re-measurement**)
- **stiction** in soil the foot has sunk into: the stick-point anchor holds up to μs·Fn, and a foot that has sunk 2–5 cm into tilled soil or crater spoil has soil ahead of it to plough as well

Neither is a scripted event. The consequence is: collective rises, three feet leave, one is still held, so the airframe rotates about that foot and the horizon tips until the hook releases or the leg slides out. The pilot must correct immediately. The **effective weight changing as the airframe unsticks** is the same mechanism seen through the load path: while a foot is held, part of the weight is still on the ground and the thrust needed is less than hover, so the moment it lets go the drone accelerates up more than the pilot asked for.

**Launch rails (legs off).** Two steel tubes, **0.26 m apart, 0.25 m high, 0.6 m long, 25 mm square** (**derived** in `define-map-format` from the pilot's "about the drone's diameter"; which part of the drone rests on them is still open, §12 O-8). Rails are a `world-query` runtime object with the steel material, so they are ordinary contacts. A rail is a narrow, hard, low-friction line contact: the drone can slip sideways off it, which is its own liftoff hazard, and the spool hangs clear beneath.

**Non-leg contacts.** Arms, the body, the coil and the prop discs are swept capsules against `StaticContacts`, following the `api-review.md` §4 plane rule. An element entering a prop disc is a logged strike.

## 6. Wind

**Structure.** Three layers, all bilinearly sampled at the rotors, the body and the tether nodes:
1. **Mean profile**: a log law with roughness z0 ≈ 0.1 h_cover and displacement d ≈ 0.67 h_cover, derived from the surfaces and cover under the drone (`surfaces-review.md` §6.5). Over bare ground z0 comes from the relief amplitude.
2. **Turbulence**: seeded broadband gusts of Dryden form, with σ/U and length scales from `wind.md` §5.4 — σ_v/U ≈ 0.12–0.15 and L_u = L_v ≈ 115–200 m, L_w ≈ the height (**assumed**, MIL-F-8785C over farmland). Energy must reach the airframe up to **2–4 Hz encounter frequency** (**derived**), not just slow gusts, or T6b and T6c fail.
3. **Obstacle wakes**: precomputed per wind direction on the existing 2 m grid by marching upwind, as `surfaces-review.md` §6.5 sets out — deficit and turbulence boost as functions of x/H, z/H and path porosity β, porous-fence behaviour for β > 0.3, bluff-body recirculation for β < 0.1, speed-up at ~1.2 H over the top, a jet under crowns with a base, attenuation inside a canopy, and jets through `GapsNear` openings.

**Why the wake layer carries the change's weight.** The measured difference between open field and tree belt is the whole point of manifesto §2.3: roll wobble ×2.5, yaw ×3.8, and every single 2σ roll event in the clip fell over the belt (**measured**, `wind.md` §4.4). Spatial intermittency — bursts where the obstacles are, a background that never goes calm — is a target (T7, T9, T12), and a model with uniform turbulence fails it.

**Per-flight draws** (D-011 and `wind.md` §6): the level fixes the strength band, σ/U, the scales, the wake parameters and the direction-change parameters. Each flight draws its **prevailing direction** uniform over the compass and its **mean strength** uniform within the level's band, both logged. The direction then meanders and shifts at random during the flight — never a fixed vector — which T0c checks over 40 seeds × 120 min.

**Level centres** (**derived**, `wind.md` §6.4): Severe's centre is tuned until T0 gives 9 ± 3° of lean; Windy is c = 0.5 of it (~2.5 m/s mean crosswind) and Calm c = 0.15 (~0.8 m/s). Bands are centre ± 25 %, and they never overlap.

**Acceptance.** The wind is graded by `wind.md` §6 as written: the §6.1 simulated pilot, the §6.2 route (32 s open field + 7 s belt, 20–50 m, ≥ 5 seeds, first 5 s discarded), `tools/reference/wind_stats.py` for the statistics, the §6.3 Severe bands and the §6.4 Windy and Calm columns. Two obligations this change owns:
- **Calm flies with airframe asymmetry off** (`wind.md` §6.4), so the ceilings measure the wind and not the motors.
- **Calm's floor is ours.** "Never perfectly still" is the airframe's, so this change adds its own target: with Calm wind and asymmetry **on**, the roll and pitch wobble must stay above a floor. That is the one number `wind.md` deliberately left to us.

## 7. The fiber tether

This is the least documented subsystem and the one with the most open questions. What is known: a **10 km spool of 0.25 mm fiber** (**published**, BOMBUS 10-O), a coil of **about 1 kg ≈ 10 km** (**pilot** and **derived**), a 20 km module at 1.75 kg with 0.27 mm fiber (**published**), and the pilot's account that you fly smooth orbits, avoid sharp reverse, and the line **may or may not** cut (**pilot**, manifesto §2.5).

**Model.** A lumped-node chain, not a full cable solve:
- a **spool** at the drone that pays out when the span tension exceeds the spool's own payout threshold, and cannot take line back in. One-way payout is what makes sharp reverse dangerous: fly backwards and the line already out has nowhere to go, so it loops, snags or loads up.
- a **paid-out span** as a small number of nodes (≤ 32, matching the world-query budget of ≤ 32 node ground samples and ≤ 32 swept segments per step) between the drone and the launch point, carrying gravity, wind drag along their length, and ground or vegetation contact through the same `StaticContacts` and `MicroDetailNear` calls the legs use. Twigs act as capstans of radius d/2 and straws hook it (`surfaces-review.md` §6.4).
- **drag on the airframe** as the span's tension at the spool exit, applied at the exit point below the centre of mass, so a loaded tether both pulls and rolls the drone. This is the part that changes the piloting envelope: a hard turn loads the line, and the load answers back.
- **mass** leaving the airframe as line pays out, fed into the §2 inertia build.
- **bend** over contact edges using the `edge_radius_m` the catalog material table already carries for this purpose.

**Cutting is a risk, never a certainty.** Three physical mechanisms, each compared against a per-flight strength drawn from the seed within a bounded range, so two identical manoeuvres on different seeds can end differently — as the pilot describes:
- **tension** above the fiber's breaking load
- **bend radius** below the fiber's minimum, over a sharp edge
- **abrasion**: accumulated rubbing against an edge or vegetation under load, integrating toward a threshold

The per-flight strength draw is what makes it honest. A fixed threshold would make the game deterministic-feeling ("that manoeuvre always cuts"); noise on the outcome would make it arbitrary. A drawn strength means the mechanism is deterministic and the **line** varies, which is what a real spool of fiber does.

**Link signals.** Tension, minimum bend radius along the span, whether the span touches vegetation, the span's wind-driven vibration and the link state are computed and logged, because `video-feed.md` N12 needs exactly these as drivers (**derived**, `wind.md` §5.5). Nothing is handed to the video module in this change.

Breaking load, minimum bend radius, payout tension and the abrasion rate are all **unsourced** (§12 O-5), and the pilot can narrow the qualitative half.

## 8. Controlled randomness

The manifesto asks for "a tiny, cunning, and elegant randomness" (§2.4). The rule this change adopts, and which the spec enforces:

> Randomness enters **only** as a bounded perturbation of a named physical parameter, drawn from a seeded stream and written to the log. It never enters as a force, a moment, a pose offset or a filter on the output.

Three reasons this is not pedantry. A perturbed parameter **responds to the pilot** — a hot motor's yaw bias grows with throttle, so the correction that works at hover is wrong in a climb, and that is what makes real flying feel alive. It is **explicable** — the log says "motor 3 is 1.4 % hot", so QA can tell a bug from the model working. And it is **replayable**, because a draw is a number in the header rather than a stream of noise.

**Seed architecture.** One 64-bit flight seed, shown to the pilot and recorded in the log header. From it, independent named streams by hashing (seed, stream name), so adding a new perturbation later never shifts an existing stream's sequence and old logs keep replaying. Streams split into:
- **per-flight draws**, made once at arm time and written to the header: motor Kv spread, ESC timing offsets, prop imbalance, leg stiffness spread, fiber strength, wind prevailing direction and mean strength
- **evolving streams**, stepped during the flight: turbulence, gust arrival, spool payout irregularity
- **world-derived, not drawn at all**: everything about the ground and the cover. Hook release forces, pitfalls, straw positions and soil come from the map seed through `world-query`, so the same landing spot behaves the same way every time. This is deliberate: the ground is not random, it is *unknown*, and the pilot learns it.

**The asymmetry budget** (all **assumed**, §12 O-1): per-motor Kv spread within a bounded band, ESC timing jitter, and prop thrust imbalance, drawn once per flight. Together they must produce the pilot's mild yank — a slow wander needing roll and yaw correction — and must **not** produce a buzz or a divergence. The spec grades this by its consequence: with wind off, on a flat world, the drone must drift and need correction, at a rate inside a band, and a run with the asymmetry disabled must be visibly steadier.

## 9. Determinism

`api-review.md` §6 lists the obligations, and they are requirements here:
- only correctly rounded IEEE operations (+ − × ÷ √) in the flight loop: **no** `Math.Sin`, `Cos`, `Exp` or `Pow`, and no implicit FMA. Trigonometric and exponential needs are met by polynomial or rational approximations in the model's own `DetMath`, matching `world-query`'s W-13 rule, so the same code gives the same bits on any x64 machine.
- micro-detail prefetch is requested at a step chosen from state (the centre moved > 0.5 m) and swapped in at a fixed later step. A late worker makes the flight thread **wait** and logs a hitch; it never changes a result.
- hook state is keyed by element `Id` so it survives a cache swap.
- every seeded draw is logged.
- the log header records the world content hash, `QueryVersion`, the runtime version and the ISA flags, and a replay refuses a mismatch.
- runtime objects (the rails) are logged as asset, pose and order, because `api-review.md` §6 notes they are not in the content hash.

## 10. Logging and replay

**Two streams, one file.** A header with everything fixed (seed, all per-flight draws, the pilot's settings, mass and inertia, the world hash, `QueryVersion`, build and ISA), then fixed-layout binary records:
- **full state at 1 kHz**: the integrated state plus the per-rotor commands and speeds, pack voltage and current, and the net force and moment. This is the replay's input and the forensic record.
- **events, timestamped at the step rate**: contacts made and broken with their surface, material and `FeatureId`, hook attach and release with the element `Id`, pitfall entries, tunnel events, prop strikes, mixer saturation, VRS entry and exit, spool payout, tether break with its mechanism, hitches, and impacts with their peak acceleration and impulse.

A 1 kHz full-state record is the expensive choice and it is the right one: the manifesto's requirement is that the pilot can tell a bug from pilot error, and a crash is decided in the 50 ms before it. Size is bounded by writing packed fixed-layout records to a ring buffer and flushing on a worker thread, with a stated budget per minute of flight.

**Replay.** `tools/flightbench/ --replay <log>` re-runs the model from the header **without Godot**, and must reproduce every logged state bit-for-bit. This is the strongest available test of the whole model: it fails if any draw is unlogged, any state is uninitialised, any transcendental sneaks in, or anything reads the scene tree.

**Forensics.** The spec requires that a log answers, without guessing: which part touched what, at what speed, with what impulse; whether a motor was saturated; whether the pack had sagged; whether a hook or a pitfall was involved and which one; and whether the tether was loaded or broken. QA's job is to name a cause, and the log has to make that possible.

## 11. Performance

The budget is the manifesto's: ≥ 1 kHz with the full world loaded, 60+ fps on mid-range PCs, on a separate thread from the renderer. `world-query`'s measured share of a worst-case step is 34.7 µs median (p99 72.9 µs from OS jitter) against its own 60 µs budget, so the model's own math has to be cheap. The plan:
- world queries are batched exactly as `surfaces-review.md` §6.1 tabulates (44 ground samples, 46 swept capsules, 8 rays per step) and are the dominant cost
- micro-detail is prefetched on a worker at ≤ 40 Hz and never enters a step
- the wake precompute runs on a worker when the direction changes by > 5° or the window moves, never in a step
- zero managed allocation in steady state, checked the way `world-query` checks it
- measured on AC power, with the clock and power mode printed, following the precedent set in D-009 and accepted in `define-map-format` 3.6

## 12. Open questions

These are the numbers this change cannot source. Each is listed with what it affects and what a rough answer would settle. **P** marks the ones the pilot can answer from experience; the rest need a measurement, a test flight or a decision.

| # | Unknown | Affects | Current stand-in |
|---|---|---|---|
| O-1 **P** | **How large is the asynchrony, in its effect?** How often does the drone yank — every few seconds, or occasionally? Is it felt mainly as roll, as yaw, or both? Does it get worse at high throttle? | The whole of manifesto §2.4, and the Kv / ESC / imbalance bounds | A bounded Kv spread and timing jitter tuned to give a mild wander. Entirely **assumed**; no public figure exists for matched-set motor spread on these airframes |
| O-2 | **Motor and ESC time constants.** τ_m for a 3115 900 Kv motor with a 3-blade 10" prop, and the ESC's command filter | How far "behind the sticks" a heavy quad feels; whether asymmetry reads as a wander or a buzz | **Assumed** from rotor inertia and thrust. A bench measurement or a manufacturer curve would settle it |
| O-3 | **Airframe drag areas and coefficients**, along and across, for body, coil and rotor discs | Cruise trim, and the lean-per-m/s that T0 and T11a grade | **Assumed**, tuned to `wind.md` §5.3's derived 1.8° (0.95–2.6°) per m/s. The spread on that derived figure is itself wide |
| O-4 **P** | **Leg stiffness and damping**, and foot diameter. The pilot's "plastic, 25–35 cm, slightly soft" is the only source | The landing bounce (§2.6) entirely, and the liftoff shake | **Assumed**. The pilot can bound it: from about a metre up on a slow glide, holding the sticks, how far does it bounce — a few centimetres, or clear of the ground? |
| O-5 **P** | **Fiber mechanics.** Breaking tension of 0.25–0.27 mm coated fiber, its minimum bend radius, the spool's payout tension, and the abrasion rate | Whether and when the line cuts (§2.5) | **Assumed**. The pilot can answer the qualitative half: what actually cuts it in practice — sharp reverse, snagging on vegetation, a tight bend over an edge, or rubbing? Does it break more often just after take-off or far out? |
| O-6 **P** | **Vortex ring state onset** for a 10" rotor at this disc loading | Whether a straight vertical descent with a load sinks unexpectedly | **Assumed** band. The pilot can say whether they have felt a heavy cargo quad fall through its own wash on a vertical descent, and roughly how fast they were coming down |
| O-7 **P** | **How often does a leg actually stick at liftoff**, and how far does the horizon tip before it frees? | Manifesto §2.1's acceptance band | **Derived** 0.9 hooked legs per liftoff on `belt_straw` (`api-review.md` §7, due for re-measurement after F1). Whether that matches the pilot's experience is unknown, and it is surface-specific |
| O-8 **P** | **Which part of the drone rests on the launch rails**, and their real dimensions. Still open from `define-map-format` review Q1 | The legs-off liftoff | **Derived** rails, 0.26 m apart, 0.25 m high |
| O-9 **P** | **The pilot's rate settings** — roughly what rates and expo they fly, and whether the drone feels sharp or soft to them | The rate controller's gains and stick scaling | Betaflight-class defaults, **assumed**. Strictly this is `game-developer`'s curve, but the controller behind it is ours |
| O-10 | **Airframe inertia is derived from an assumed layout**, not measured (`wind.md` §5.5). The ranges are wide: roll 0.033–0.057, pitch 0.040–0.072, yaw 0.043–0.060 kg·m² | How the drone answers every input — the inertia half of §2.4 | The component build, which at least keeps the parts physically consistent. Only a real airframe measurement would close it |
| O-11 **P** | `wind.md` Q14, still open: **in strong wind, do you fly slower than your usual cruise**, or was that stretch at cruise speed? | The airspeed the crosswind estimate rests on, so T0 and T11a | 9–25 m/s **derived** range |
| O-12 | **Calm's floor**, the "never perfectly still" number. `wind.md` §6.4 hands it to this change and gives no value | Whether Calm reads as dead-still, which the pilot would notice at once | Set from the asymmetry model once O-1 is bounded, then confirmed by the pilot's MVP-1 flights |

## 13. Risks / Trade-offs

- [The asymmetry is tuned until it "feels right" and ends up as disguised noise] → The spec forbids noise structurally (§8) and grades asymmetry by a measurable drift rate with an on/off comparison. A reviewer can read the code and see that every random number is a parameter, not a force.
- [1 kHz full-state logging is too big or too slow] → Packed fixed-layout records, a ring buffer, a worker flush, and a stated size budget per minute. If the budget fails, the fallback is to keep events at the step rate and decimate full state to 250 Hz **for the forensic stream only**, keeping 1 kHz for the replay stream — but that costs bit-exact replay, so it is a last resort.
- [The wind acceptance suite is 20 targets and may not pass in one pass] → It is a suite built to be iterated against, and `wind.md` §6.2 says the bands are "acceptance bands for a first model" with the pilot's MVP flights having the final word. The tasks treat tuning to the targets as its own step, after the structure works.
- [The whole model is built on assumed parameters, so it could pass every target and still feel wrong to the pilot] → This is the real risk of the change and it cannot be designed away. The mitigation is structural honesty: get the mechanisms right and the parameters adjustable, then let MVP-1 tune them. Every assumed figure is labelled, so a "that feels wrong" from the pilot maps to a parameter rather than a rewrite.
- [Contact instability at some surface and leg combination] → `surfaces-review.md` §1 already checked ω·dt over every surface, with the drone's compliance in series. The remaining risk is the legs-off rail contact at ~1e6 N/m (ω·dt ≤ 0.7), which is stable with semi-implicit Euler but has the least margin. If it misbehaves, the leg or frame compliance in series with it is the knob, not a smaller step.
- [Cross-machine determinism has never actually been tested] → `api-review.md` §6 flagged this: the golden file has only ever run on one machine and one runtime. The replay test here inherits that gap. A second machine is needed, and it is a QA task.
- [VRS with hysteresis is a switch, and switches can chatter or be exploited] → The hysteresis band and the unsteadiness are logged on entry and exit, so QA can see whether a crash involved it. If it reads as a cliff, the fix is a wider blend, not a deeper model.
- [The tether's one-way payout can wedge geometrically — a loop with nowhere to go] → Detected as a node configuration that cannot resolve, and logged. A wedge that would otherwise explode the solve resolves as a tension spike, which is a real outcome (it is how fiber breaks), rather than a NaN.
- [The prevailing wind draw could hand a flight an unflyable direction] → It cannot, at the flight level: the level's band is bounded and the pilot chose the level. The §6.2 acceptance runs additionally require a start where the seed's wind stays within 90° of the drawn direction, which is a test-procedure rule, not a flight rule.
