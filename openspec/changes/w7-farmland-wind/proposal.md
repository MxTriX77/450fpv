# Proposal: w7-farmland-wind

**Owner:** physics-engineer · **Reviewers:** qa-engineer · **Requirements:** PHY-13, PHY-13a, PHY-14, PHY-15, PHY-18, PLT-04 · **Decisions:** D-088, D-072 · **Plan:** `docs/final-world-plan.md` §4, unit W7 · **Tier:** Standard

## Why
The wind field was built and validated on flat ground: the floor and the test range. The farmland has rolling hills, a ledge, eleven fields edged by tree belts and a village. PHY-13 already asks for terrain shelter, but nothing checks the field against hills:
- the speed-up over a crest;
- the shelter, or separated flow, in the lee of a steep slope and below the ledge;
- the shelter behind the farmland's tree belts, now that W3 1.2 loads them as wakes and canopy.

W3 2.5 also found a concrete gap. The mean profile takes the height over the ground **under the drone**, not under each point it is asked about. So a laid fiber's nodes on a hillside feel wind as if they were metres up. In Severe wind they slide at 0.04–4.7 m/s, never rest and never freeze into the trail. That costs every step, and it isn't physical.

## What Changes
- **Height over the local ground:** every wind query takes its height from the ground under that point: each rotor, the body and each fiber node. The drone's ground is no longer reused for points far from it.
- **The hills:** a crest's speed-up and the lee's shelter come from a published model of flow over low hills, chosen in design.md with its source and its validity range (slope, hill length, roughness). Where the slope is past that range, as at the ledge, the lee is treated as separated flow, again from a published source. Nothing is a tuned value.
- **The belts and the village on the farmland:** the existing wakes and canopy flow, checked against the farmland's real belts and ruins, with published shelterbelt data as the reference.
- **Wind rows on the farmland:** worldbench and flightbench rows over a crest, a lee slope, below the ledge, behind a belt and in the village. Each prints the model's value next to the published reference, and each is a golden.
- **The laid fiber on hills:** rerun 2.5's `farmland-line-step` in Severe wind. The line's far nodes rest and freeze where the physics says they should, and the step's cost is reported before and after.

## Non-goals
- No new weather levels and no change to PHY-14's target table, unless the flat-ground results change, which they must not.
- No thermals (X-12, deferred).
- No change to the friend's scene or to the store.

## Impact
- The sim package's Wind folder (`WindField`, `MapWind`, `CanopyFlow`, `WindSetup`) and the fiber's wind query in Tether.
- Worldbench and flightbench rows and goldens.
- A model number (D-072), with every moved golden audited case by case.
- The branch is `unity/physics/physics-integration-2--w7-wind`, cut from `--w3-ground` with the objects merged in. It merges onto the line after W3.
