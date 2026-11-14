## ADDED Requirements

### Requirement: The plants move in the air the drone flies in
The drawn plants SHALL move in the sim's own air, sampled where each plant stands, in real time: the wind field the drone flies in (its prevailing direction, the direction's meander and shifts, its turbulence by height and the obstacles' wakes) and the props' wash (AST-09, VIS-05, PHY-13).

#### Scenario: A gust passes a row of trees
- **WHEN** a gust of the flight's wind field passes a row of trees and the grass before it
- **THEN** each plant bends along the local wind at its own place and height, the gust reaches each plant when the field brings it there, and the drone flying by feels the same gust

#### Scenario: The wind turns
- **WHEN** the field's direction meanders or shifts during a flight
- **THEN** the plants lean along the local wind's new direction, never along a fixed one

### Requirement: Plants are flexible bodies with published properties
Each plant class SHALL bend as damped modes whose natural frequencies, damping and drag reconfiguration come from published plant biomechanics, each number labelled measured, published, derived or assumed (REAL-01).

#### Scenario: A short gust
- **WHEN** a gust shorter than a plant's natural period passes it
- **THEN** the plant bends, springs back past its rest and settles with its own period and decay, as its damping ratio gives

#### Scenario: A steady wind
- **WHEN** the wind holds steady at a plant
- **THEN** the plant leans by its drag over its stiffness, with its reconfiguration, and moves about that lean with the turbulence

### Requirement: The props' wash moves the plants near the drone
The drawn plants SHALL move in the props' wash as the sim computes it (the near wake, the merged jet and the wall jet), and the plants that touch the drone SHALL be the physics' own (AST-10, D-102).

#### Scenario: A hover over grass
- **WHEN** the drone hovers over grass at 0.5, 1, 2 and 4 m
- **THEN** the grass under the discs flattens by the wash's pressure, a ring of grass beyond it leans outward with the wall jet and ripples, and the flattened area shrinks with the height as the wash's law gives

#### Scenario: Through a crown
- **WHEN** the drone flies into a tree's crown
- **THEN** the leaves and twigs near the discs flutter in the wash and the ones the drone pushes bend away from it

### Requirement: What the props cut is seen
The pieces the props cut SHALL be drawn leaving the discs, from the sim's cut events, so a replay draws the same pieces (VIS-06).

#### Scenario: A landing in straw
- **WHEN** the props cut straw on a landing
- **THEN** pieces leave the discs at the blade's speed in the feed, and their number follows the log's cuts

### Requirement: The wash raises dust from dry ground
The props' wash SHALL lift dust from dry ground where its speed along the ground passes the surface's published threshold for lifting grains, and the dust SHALL be drawn in the feed (VIS-06).

#### Scenario: A landing on dry ground
- **WHEN** the drone descends to land on a dry surface
- **THEN** no dust lifts while the wash at the ground stays under the surface's threshold, whiffs of dust start where it passes it, and they grow as the drone comes lower

#### Scenario: A landing on wet or covered ground
- **WHEN** the drone lands in rain or on a surface whose threshold the wash never reaches
- **THEN** no dust lifts

### Requirement: The drone feels what it flies through
Plants SHALL act on the drone's legs, body and props by their stiffness and drag (PHY-05), so skimming grass drags the drone and a crown's twigs and leaves hold it more than grass does, computed, not tuned.

#### Scenario: Skimming weeds
- **WHEN** the drone skims weeds with its legs or body
- **THEN** it feels their drag and a touchdown in them jitters, and the log records the stems' forces

#### Scenario: Into a crown
- **WHEN** the drone flies into a tree's crown at the speed it skimmed the weeds
- **THEN** it is slowed more than in the weeds, by the twigs' and leaves' own forces
