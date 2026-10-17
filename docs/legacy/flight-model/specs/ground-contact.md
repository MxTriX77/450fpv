# Spec Delta

## Purpose

Everything that happens when the drone touches the world: the soil under a foot, the straw mat above it, the stems and twigs that jitter and hook, the holes a leg drops into, the compliant plastic legs that bounce, and the steel rails the drone lifts off from when the legs are off. This is manifesto §2.1 (a leg sticking at liftoff and the effective weight changing), §2.2 (pitfalls, loose soil, grass jitter) and §2.6 (the landing bounce) — three of the pilot's six complaints.

The contact law itself is not redesigned here. It is the one this role already signed off in `define-map-format` (`surfaces-review.md` §1), consuming `surfaces.json` and `world-query` with **no new surface fields**.

## ADDED Requirements

### Requirement: Soil, mat and cover contact as signed off
The contact model SHALL implement `surfaces-review.md` §1 as signed off: an elasto-plastic Winkler soil with the load/unload rule and the per-foot plastic set, foot-size scaling of the modulus by (b_ref/b)^0.3, the firm layer past `max_sink_m`, the compressible mat in series at equal force with its spreading load area, stick-point (bristle) friction with μ blending from soil to mat over the first 10 mm of mat, ploughing against lateral velocity, and the footprint ring that lets a re-landing find compacted soil.
Cover forces SHALL follow `surfaces-review.md` §6.4: standing stems as rooted cantilevers with slide-off and buckling caps, lying straw as light cantilevers, twigs as rigid rollers with low cross-axis friction, and litter carried by the mat only (queried with a `KindMask` that excludes it).
The model SHALL read these parameters from `surfaces.json` and `MicroDetailNear`, blended with `SampleGround`'s four surface blend weights, and SHALL NOT introduce new surface or cover fields.

#### Scenario: Static sink matches the signed-off table
- **WHEN** a 15 mm foot is loaded with 25 N on each of the nine surfaces
- **THEN** the static sink matches the `surfaces-review.md` §1 table within 10 %: meadow_sod 20 mm, dry_crust 2.8 mm, crater_spoil 51 mm, belt_straw 30 mm, belt_bare 10 mm, tilled 35 mm, yard_litter 4.0 mm, rubble 1.4 mm, weeds 10 mm

#### Scenario: Soil keeps a plastic set
- **WHEN** a foot is loaded to 25 N on crater_spoil and unloaded, three times in the same footprint
- **THEN** the sink recovered on each unload is smaller than the sink applied, the retained set grows and then stabilises, and the second and third loadings are stiffer than the first by the surface's unload stiffness ratio

#### Scenario: No new fields
- **WHEN** the flight model's surface and cover parameter reads are reviewed against `game/maps/surfaces.json`
- **THEN** every value read is an existing field, and `surfaces.json` and the map format are unchanged by this change

#### Scenario: Grass jitter at touchdown
- **WHEN** the drone is set down slowly on meadow_sod and on belt_bare, with wind and motor asymmetry disabled
- **THEN** the meadow touchdown shows a measurably larger high-frequency force variation at the feet than belt_bare, arising from individual stem contacts, and each contributing element is identifiable in the log by its `Id`

### Requirement: Uncertain ground
The model SHALL consume `SampleGround`'s micro-relief, ridges and pitfalls so that the ground under a foot is not the ground the pilot sees:
- a foot over a **pitfall** SHALL find no support until the hole's floor, and the event SHALL be logged with the `FeatureId` and depth the query returns
- **directional ridges** SHALL tilt the support so that crossing a tilled field's furrows rocks the drone and running along them does not
- the support normal SHALL come from the query, so relief and creases tilt and slide the foot at touchdown and liftoff

#### Scenario: A leg drops into a hole
- **WHEN** the drone lands with one foot over a pitfall on meadow_sod
- **THEN** that foot passes the surrounding ground level with no support force, the drone tips toward it, and the log names the pitfall's `FeatureId` and depth

#### Scenario: Furrows depend on heading
- **WHEN** the drone is taxied at the same slow speed across a tilled surface's furrows and then along them
- **THEN** the across-furrow run shows a larger periodic roll disturbance than the along-furrow run, at the wavelength the surface's ridge spacing predicts

#### Scenario: Loose soil swallows a fast landing
- **WHEN** the drone lands at the same descent rate on dry_crust and on crater_spoil
- **THEN** the crater_spoil landing sinks further, decelerates over a longer distance with a lower peak force, and retains a plastic set, while dry_crust stops short with a higher peak force

### Requirement: Compliant legs
When the legs setting is on, the drone SHALL have four plastic legs, each a spring–damper member in series between the airframe and the ground contact, so that no rigid material stiffness enters the step directly. Leg stiffness and damping SHALL be per-leg parameters with a bounded per-flight spread drawn from the flight seed and logged.
When the legs setting is off, the legs SHALL be absent from the mass, inertia and contact model.

#### Scenario: Legs carry the compliance
- **WHEN** the drone rests on rubble, the stiffest surface in the table
- **THEN** the dominant compliance in the load path is the leg's, not the soil's, and the contact is stable at the fixed step with no growing oscillation over 30 s

#### Scenario: Legs off removes them
- **WHEN** the legs setting is off
- **THEN** total mass falls by the legs' mass, the inertia build reflects their absence, and no leg contact is ever reported

### Requirement: The landing bounce
Holding the sticks after touchdown SHALL throw the drone back into the air, and cutting the throttle at touchdown SHALL keep it down (manifesto §2.6). This SHALL emerge from leg compliance and residual thrust, and SHALL NOT be a scripted impulse or a restitution coefficient.

#### Scenario: Hold the sticks and it bounces
- **WHEN** the drone descends on the slow glide path the pilot describes — slightly down and slightly forward from about 1 m — and the sticks are held unchanged through touchdown on a firm surface
- **THEN** the drone leaves the ground again after the first contact, with a measurable positive vertical velocity and a stated minimum separation of all four feet, and the log records the leg compression that returned the energy

#### Scenario: Cut the throttle and it stays
- **WHEN** the same approach is flown and the collective is cut to zero within 100 ms of the first contact
- **THEN** the drone settles with no foot leaving the ground again, and comes to rest within a stated time

#### Scenario: The bounce needs the legs
- **WHEN** the same hold-the-sticks approach is flown with the legs setting off, onto the launch rails
- **THEN** the bounce behaviour differs from the legs-on case, because the compliance in the load path is different, and the difference is visible in the logged normal force history

#### Scenario: No restitution coefficient
- **WHEN** the contact code is reviewed
- **THEN** there is no restitution or bounciness parameter anywhere in it, and the rebound is produced only by stored elastic energy and the thrust still being applied

### Requirement: A leg sticking at liftoff
One or more legs SHALL be able to stay held as the drone lifts off, so the airframe rotates about the held foot and the horizon tips until the leg frees itself, requiring an immediate pilot correction (manifesto §2.1). The hold SHALL come only from physical causes already in the world:
- a **hook** on a cover element whose `Hooks` flag is set, releasing at the element's own `HookRelease` force over the 30 mm the world spec defines, keyed by the element's `Id` so it survives a micro-detail cache swap
- **stiction and ploughing** at a foot that has sunk into soft soil, through the stick-point anchor and the ploughing term

There SHALL be no scripted "stick a leg" event, no random draw made at liftoff, and no probability parameter for the occurrence itself.

#### Scenario: A hooked leg tips the horizon
- **WHEN** the drone lifts off from belt_straw on a seed whose world places a hooking straw under one foot, at a normal liftoff collective ramp with no pilot correction
- **THEN** three feet leave the ground before the fourth, the airframe rolls or pitches toward the held foot by a measurable angle, the hook releases at its logged `HookRelease` force, and the log names the element `Id`, the foot and the release force

#### Scenario: Whether it happens is the world's business, not a dice roll
- **WHEN** the same liftoff is repeated from the same position on the same map with ten different **flight** seeds
- **THEN** the same elements are under the same feet with the same release forces every time, because they come from the map seed, and any difference between runs traces to a logged flight-seed draw such as leg stiffness, never to a fresh draw about the hook itself

#### Scenario: The hold is surface-dependent
- **WHEN** liftoffs are run from many positions on belt_straw and on dry_crust
- **THEN** belt_straw produces materially more held-leg liftoffs than dry_crust, consistent with the two surfaces' cover densities and hook probabilities

#### Scenario: Effective weight changes as it unsticks
- **WHEN** a liftoff with one held foot is logged
- **THEN** while the foot is held the total ground normal force is non-zero and the collective needed to begin rising is below the free-hover value, and at release the net vertical force jumps, producing a vertical acceleration larger than the pilot's input alone would give

### Requirement: Launch rails when the legs are off
With the legs setting off, the drone SHALL rest on two parallel steel bars added as `world-query` runtime objects at the start point, with the steel material, so the fiber spool beneath the airframe clears the ground (manifesto §3). Rail contacts SHALL be ordinary static contacts with the steel material's friction, and the drone SHALL be able to slide or slip sideways off a bar.

#### Scenario: The spool clears the ground
- **WHEN** the drone is placed on the rails at a start point
- **THEN** every rail contact reports the steel material, the lowest point of the coil is above the ground height beneath it, and no part of the coil is in contact

#### Scenario: A rail is a narrow hard contact
- **WHEN** the drone sits on the rails and is given a small sideways disturbance
- **THEN** it can slide along and off the bar, governed by the steel material's friction, and leaving the bar is logged as a contact break

### Requirement: Non-leg contacts follow the swept-contact rule
Arms, the body, the coil and the prop discs SHALL be tested as swept capsules through `StaticContacts` every step. The response to a contact with `Time` < 1 SHALL follow the decision recorded in `api-review.md` §4: no rewind and no sub-stepping; fix the contact plane at first touch, measure later penetration against that plane, refresh it only when a same-side `Time` = 1 contact returns, and release it on separation or when the pair disappears.
A part that ends a step on the far side of an object SHALL be logged as a `tunnel` event, which means a physics bug.
A cover element entering a prop disc SHALL be logged as a strike with the element's `Id`.

#### Scenario: A fast arm does not pass through a wire
- **WHEN** an arm is swept across a 5 mm wire at the highest speed the drone can reach, at many phases within the step
- **THEN** a contact is reported and responded to at every phase, and no `tunnel` event is logged

#### Scenario: The contact plane is fixed at first touch
- **WHEN** a swept contact is first reported with `Time` < 1
- **THEN** the response uses the point and normal at first touch, the step is not rewound, and the plane is kept until a same-side `Time` = 1 contact refreshes it or the parts separate

#### Scenario: A prop strike is logged
- **WHEN** the drone flies a prop disc through standing weeds
- **THEN** each element entering the disc is logged as a strike with its `Id`, kind and the disc that hit it
