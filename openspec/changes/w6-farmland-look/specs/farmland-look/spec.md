## ADDED Requirements

### Requirement: The farmland looks like the friend's world
Our pipeline SHALL render the farmland like the friend's own project (D-088, REAL-01), changing only our pipeline assets, renderers and volumes, never the friend's scene or the store (D-089), and never the feed's ported passes (D-026).

#### Scenario: The friend's cameras match
- **WHEN** our player's noclip, with the feed off, renders each of the friend's 10 saved camera views
- **THEN** each frame's mean luma is within 2 (of 255) of the friend's own render at the same view, and both show soft shadows, ambient occlusion and the distance haze

### Requirement: The farmland flies at frame rate
The farmland SHALL meet PLT-02, PLT-05 and PLT-06 on the dev machine, measured by the PLT-07 protocol on AC power with vsync off.

#### Scenario: The feed over the busiest places
- **WHEN** the scripted camera path crosses the village, a tree belt and open fields with the feed on at 1080p
- **THEN** the 1 % low is at least 60 fps and the average at least 100 fps

#### Scenario: Noclip over the busiest places
- **WHEN** the same path runs in noclip with the feed off at 1080p
- **THEN** the 1 % low is at least 60 fps

#### Scenario: Memory and load
- **WHEN** Explore opens the farmland and the path runs
- **THEN** VRAM stays at most 4.5 GB, RAM at most 8 GB, and the first flyable frame comes at most 20 s after the press
