## ADDED Requirements

### Requirement: M toggles the world map without pausing
In flight and in Explore, M SHALL toggle a map panel. While it shows, the sim's 1 kHz step, the flight controller and the pilot's input SHALL keep running exactly as when it is hidden (D-101, APP-04, PRD-07).

#### Scenario: Flying with the map open
- **WHEN** a scripted flight with fixed inputs is flown once with the map hidden and once toggling M every 2 s
- **THEN** both flight logs are bit-identical, and no step is skipped or late because of the map

#### Scenario: Explore
- **WHEN** M is pressed in Explore
- **THEN** the panel shows with the noclip camera's position and heading, and noclip keeps moving under the pilot's input

### Requirement: The panel shows the whole map and the drone live
The panel SHALL be centred and smaller than the game window on every side, show the whole map from above, and draw the drone's position and heading every frame. When the drone is outside the image, its marker SHALL sit at the panel's edge in the drone's direction.

#### Scenario: The marker follows the drone
- **WHEN** the drone flies a known path with the panel open
- **THEN** the marker's position on the image matches the drone's sim position through the stored world-to-image transform within one image pixel, and its heading within 2°

#### Scenario: The panel's size
- **WHEN** the panel shows at 1920×1080 and at the display's resolution
- **THEN** every edge of it lies inside the window with a visible margin, and the flight shows around it

### Requirement: Each map's image is generated from above
Each map's top-down image SHALL be rendered by a capture method from params per map (extent, resolution) with its world-to-image transform beside it, at build time, into a git-ignored folder carried into the player. A render of the friend's world SHALL NOT be pushed unless the change shows it is redistributable (D-089).

#### Scenario: Both maps
- **WHEN** the build runs from a clean checkout with the store linked
- **THEN** House and Village each get an image and a transform with no manual step, and `python tools/hygiene/precommit.py --tree HEAD` passes

#### Scenario: The cost
- **WHEN** the feed's frame time is measured with the panel shown and hidden (Performance mode, AC power, nobody at the PC)
- **THEN** the panel adds at most 0.2 ms a frame, and nothing while hidden
