## MODIFIED Requirements

### Requirement: The menu and the setup
The main menu SHALL offer Play, Calibrate and Exit, in every build, plainly built and readable (APP-02; judged at G7 by D-042); a review build adds no item (the pilot, 2026-11-17). The setup SHALL offer payload 0–2.0 kg in 0.1 kg steps, legs on or off, the time of day (continuous, night included), wind Calm / Windy / Severe and rain Sunny / Rain / Severe rain and the start point (APP-03), with no other settings.

#### Scenario: Into the air in a few steps
- **WHEN** the pilot starts the app with a calibrated profile
- **THEN** Play, a map's card, the setup's Fly and arming the switch take them to a flight over that map with no other screen in between

#### Scenario: A review build's menu
- **WHEN** a review build opens to the main menu
- **THEN** it offers Play, Calibrate and Exit, as the pilot's build does, and nothing opens the farmland in noclip

## ADDED Requirements

### Requirement: A screen shows only what the pilot operates it by
Every text the app draws on its screens (the menu, Play's carousel, the setup, the loading screen, the Calibrate screen and its Rates tab, the hints over the flight, the pause and the summary) SHALL be one the pilot needs to operate that screen: the name of an item, an option or a control; a key or stick prompt; a value or a state that changes (the radio's line, the build's version, the map loading, the wizard's step); or a problem or an error. A text that describes what an item, a map or an option is SHALL NOT be drawn, and no empty gap SHALL stay where one was (the pilot, 2026-11-17). The OSD is not one of these texts: it reads "Svinoriz 10 Opto" and "! SAFE !" as before (OSD-02).

#### Scenario: The menu
- **WHEN** the menu shows
- **THEN** it reads the title, the drone's name "Svinoriz 10 Opto", Play, Calibrate and Exit, the radio's line and the build's version, and no hint beside any item

#### Scenario: The setup
- **WHEN** a map's setup shows
- **THEN** it reads the map's name, the rows' names (Payload, Legs, Time of day, Wind, Rain, and Start when the map has more than one) with their values and their options' names alone (the legs On and Off), Back and Fly; a note shows only when the saved setup could not be read

#### Scenario: Loading
- **WHEN** a map loads for a flight
- **THEN** the loading screen reads "Loading " and the map's name, and nothing else

#### Scenario: The flight's first seconds
- **WHEN** the flight starts and the pilot has not armed yet
- **THEN** the hint at the foot reads how to arm on the flight's input and the keys V, M and Esc, or the wait for the radio with Esc, or the problem that stopped the flight with Esc

#### Scenario: Calibrate
- **WHEN** the Calibrate screen shows, before Start and on its Rates tab
- **THEN** it reads the tabs, the radio's state, Start, the profiles, the bars, the rates' names, curves and values and Defaults, and no line on what a calibration takes or what the rates are
