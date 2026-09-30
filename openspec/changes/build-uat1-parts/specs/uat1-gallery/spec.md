# Spec Delta

## Purpose

The pilot's UAT-1 review: a compact noclip map where every terrain kind and key object can be inspected, from far and up close, before the full world is built from them.

## ADDED Requirements

### Requirement: Gallery contents
The gallery map `uat1_gallery` SHALL contain:
- **Terrain patches**, each at least 60 m across: tree-belt edge with straw floor, open meadow, dry cratered field, tilled field, burnt field, trench along a tree strip, yard ground with litter, tall weeds, rubble
- **A relief patch** (pilot requirement PR-8): gently rolling meadow with a crest and shallow dips at the scale of tens of metres, at least 2 m from crest to dip, so the pilot judges surfaces and objects on ground that is not level. At least one tree-belt run and one object station sit on it.
- **Asset stations:**
  - the damaged adobe house with its yard set (cellar entrance, shed, fence and gate, fruit tree)
  - the brick house variant
  - the destroyed 5-storey Stalin-era block with its rubble mound
  - a classic ВАЗ and an Урал, each abandoned and destroyed by strikes
  - a Lanos-class car and a ЗИЛ, each abandoned and destroyed by strikes
  - tree-belt trees, shrubs and a dead tree
  - poles and wires, including leaning ones
  - the debris and rubble kit
  - a corrugated roof sheet
  - the launch rails

The gallery SHALL be generated from one station list, so adding an asset or moving a station is an edit and a re-run, not hand work. It SHALL be flyable before every asset exists: a station whose catalog entry does not exist yet keeps its reserved ground and shows its label marked "not built yet". Finishing an asset puts it in its station by re-running the generator.

#### Scenario: Everything present
- **WHEN** the gallery loads
- **THEN** every listed patch and station is present, the map validates, and `--selftest map` passes on it

#### Scenario: Station not built yet
- **WHEN** the generator runs while a listed asset has no catalog entry
- **THEN** the map still validates and loads, that station's ground is reserved and empty, and its label reads "not built yet"

### Requirement: Labels
When the noclip camera is within 25 m of a station or patch, its name and state (e.g. "Урал 4320, destroyed") SHALL show on screen. Labels SHALL be toggleable with a key. Labels are a review aid, drawn over the picture and never through the analog feed, so they stay readable whichever feed mode is on. The feed stays switchable in the gallery (V), so the pilot can judge each part both clean and through the goggles.

#### Scenario: Label appears
- **WHEN** the camera approaches the destroyed Урал station
- **THEN** its label appears within 25 m and disappears beyond it

### Requirement: View quality
- Near-camera micro-detail SHALL fade out smoothly at its range edge, with no visible hard circle. Beyond it, a cheap density-only layer SHALL keep grass and straw visible out to at least 60 m.
- Physics parity inside the physics radius SHALL stay exact (0 mm).
- Every surface SHALL be visibly distinct from its neighbours in a 60 m overview.
- Tilled furrows SHALL follow the physics ridge direction and SHALL NOT alias into bands at distance.
- The world artist SHALL look at screenshots of each station and patch before handing over.

#### Scenario: No hard edge
- **WHEN** the camera sits 1.5 m above the belt floor, looking along it
- **THEN** a screenshot shows grass and straw fading continuously into the distance, with no ring or line where the near layer ends

### Requirement: Performance
On the dev machine in noclip, on AC power, the gallery SHALL render at ≥ 120 fps with 1 % low ≥ 90, including the busiest station (the destroyed block and the tree belt in view together). It SHALL load in under 10 s.

#### Scenario: Busiest view
- **WHEN** QA flies the fixed gallery path with the F3 overlay
- **THEN** fps and 1 % low meet the limits, with clock and power mode recorded

### Requirement: Pilot sign-off
The pilot SHALL review the gallery in noclip (UAT-1). Their feedback SHALL be recorded in the change, and fixed or explicitly deferred, before the parts are used in the world map.

#### Scenario: Feedback loop
- **WHEN** the pilot gives feedback on any station
- **THEN** each point becomes a task, fixed or deferred with a reason, and the pilot confirms the result
