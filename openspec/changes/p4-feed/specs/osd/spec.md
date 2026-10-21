## ADDED Requirements

### Requirement: The OSD exactly as the footage shows it
The OSD SHALL be the Godot feed's character grid, font and crosshair, laid out as measured from the footage (OSD-01), with the strings "Svinoriz 10 Opto" and "! SAFE !" and every value invented (OSD-02, D-011). It SHALL be inserted before the encode, as the analog path does: hard-edged in luma, smeared in chroma with only 10 % hard-edged (OSD-04), giving the footage's fringing.

#### Scenario: The grid matches the Godot OSD
- **WHEN** the Unity and Godot OSDs are rendered at the same field
- **THEN** every glyph lies in the same cell, and the strings read "Svinoriz 10 Opto" and "! SAFE !"

### Requirement: Content from the sim state
The OSD's values SHALL come from the sim state through the OSD content model (OSD-03): pack voltage, flight time and the other OSD-03 fields, with an invented value wherever a real one would identify anything.

#### Scenario: The voltage follows the pack
- **WHEN** the pack sags in a full-collective pull
- **THEN** the OSD's voltage falls with `FeedSignals`' pack voltage within one OSD refresh
