## MODIFIED Requirements

### Requirement: M toggles the world map without pausing
In flight, M SHALL toggle a map panel. While it shows, the sim's 1 kHz step, the flight controller and the pilot's input SHALL keep running exactly as when it is hidden (D-101, APP-04). The farmland's noclip view is tooling's, not a pilot's screen (the pilot, 2026-11-17), so it has no map panel.

#### Scenario: Flying with the map open
- **WHEN** a scripted flight with fixed inputs is flown once with the map hidden and once toggling M every 2 s
- **THEN** both flight logs are bit-identical, and no step is skipped or late because of the map
