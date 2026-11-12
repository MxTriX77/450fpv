# Design: w9-village-physics

Every number is labelled **measured**, **published**, **carried**, **derived** or **assumed**, as in `audit.md`, which holds the findings each task starts from. Each task writes its own section below and leaves the others alone, so the sub-branches merge cleanly.

## 2.4 The soil anchors' reach (G1)

To be written by task 2.4.

## 2.6 The legs on a side slope (G2)

To be written by task 2.6.

## 2.7 The crown twig's repeated break (G5)

**Reproduced** (measured, `ObjectContactTests.AHoverInACrownBreaksNoBladeTwice`, model 85): the drone starts 1.24 m over the ground at (525.79, 612.74), inside GreenBush_Dry's crown, and the pilot holds that point for 25 s with surfaces.json. 150,239 `BladeDamaged` Break events, all with 0 kg lost, 150,231 at a station the blade had already broken at; 8 in every step. **No twig was cut** (0 `BladeStrike` on twig): the candidate, `PropStrikes.Twig`, never ran.

**The cause** (a temporary print, not committed):
- The props never turn in the crown: every rotor is resting (`ContactModel.Resting`), its blades held by the twig shells. So the breaks are `PropStrikes.RestDamage`'s, the resting blades' static loads, not a strike's.
- A resting blade's load radius is its pairs' force-weighted radius, and a pair's point is the blade's core projected onto the face's tangent plane (`ContactModel.Depth`). Deep inside a shell (0.4–0.7 m, the row's own design) that point lies on the shell's surface, not on the blade: **4.48–5.53 R** (measured), 0.4–0.6 m past the tip.
- `Breaks` took that radius as the load's: the tip's station (0.98875) then carried 25–39 N on a 0.44–0.57 m arm, 8–102 times its strength, and broke. The trim left the tip at that station's centre, which `Breaks` still counted (its span half inside), so it broke again every step: the tip unchanged, 0 kg lost. A rest break pays no work, so no energy was taken; only the log and the rotors' rebuild each step paid.

**The fix** (`PropStrikes.cs`, two lines):
1. `RestDamage`: the load's radius is held to the blade's tip (it acts on the blade's material). The flap set's `Worst` reads the same radius.
2. `Breaks`: a station whose centre is not inward of the blade's tip holds nothing beyond it, so it is no break. A tip is only ever 1 or a station's centre, so this removes exactly the no-op breaks, for every caller (the strikes' carve, crush and twig paths too: none can again pay a fracture's work for a break that takes nothing).

**After** (measured, the same 25 s): 1 break logged, at station 34 (0.876 R, its flatwise moment 1.02 of its strength under the shell's 37 N at the tip), taking mass; 0 with 0 kg; 0 at a station already broken; at most 2 `BladeDamaged` in a step. A second break, the same in the other front rotor, happens in the first step, whose events the step's 64-event buffer refuses with 23 others (every pair in the crown made at once); no event is refused after it. The log replays headless bit for bit (25,016 steps, 815 events) and the steps after the first second allocate 0 B.

**The events are bounded** by construction: a blade breaks at most once at each of its 40 stations, so at most 320 breaks a flight for the 8 blades.

**The energy books:** a rest break takes no work, before or after; a strike's break pays the fracture's work only when it removes a section. Holding the radius to the tip only shortens arms, so no load and no work is created.

**W3 2.7's crown flight re-measured** (`ADroneInACrownPaysForItsTwigsAndDoesNotHang`): unchanged to the bit: first touch at 10.89 s, 15 twigs cut for 135.4 J, 0 glances, crashed, 65.41 m from where it hung; its log replays bit for bit (25,016 steps, 906 events). The known-wrong also unchanged (8,502 glances, hangs 4.17 m up). `flightbench --contacts`' farmland bush: unchanged (109 twig contacts, 48.63 N·s, 98.55 m on).

**Goldens:** none moved (`flightbench --golden`: 61 PASS, every case and kept audit bit for bit).

**Model:** ships under 86 with 2.4's. The note's line: *a resting blade's load radius is held to its tip, and no blade breaks at a station at or past its tip (2.7).*

**For 2.3 (leaves in crowns), not fixed here:**
- In a hover inside a crown the props **stand still**: the twig shells hold each resting blade with 25–39 N (the row's 30 N/m per part over 0.4–0.7 m, the same law as a foot's) and its stick friction, more than the motor's torque. So `Twigs` never runs and the drone rests on its stopped props in the bush. A blade inside a shell standing for 7.5 mm twigs at 2 m/m³ should meet a few twigs, not a solid; the shells' push on a blade is the model to revisit with the leaves.
- A resting blade's pair point lies on the shell's plane, so its radius and `BladeStiffness`' station are the shell surface's, not the blade's (`ContactModel`, left alone here).
- The first step inside a crown overflows the 64-event buffer (24 refused here).

## 2.8 The plants' numbers for the props

To be written by task 2.8.

## 2.1 Village's stem field

To be written by task 2.1.

## 2.2 Stems on the props

To be written by task 2.2.

## 2.3 Leaves in crowns

To be written by task 2.3.

## 2.9 Village's ground rows and the liftoff harness

To be written by task 2.9.

## 2.10 Stems on the legs and the fiber

To be written by task 2.10.

## 2.5 Cut bits on camera

To be written by task 2.5.
