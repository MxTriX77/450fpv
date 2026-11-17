## ADDED Requirements

### Requirement: The OSD exactly as the footage shows it
The OSD SHALL be the Godot feed's character grid, font and crosshair, laid out as measured from the footage (OSD-01), with the strings "Svinoriz 10 Opto" and "! SAFE !" and every value invented (OSD-02, D-011). It SHALL be inserted before the encode, as the flight controller's chip does, so that every stage after the insertion acts on the picture with the OSD in it (OSD-04, the pilot 2026-11-17): the channel's softness in luma as the footage measures it, smeared in chroma with only 10 % hard-edged, giving the footage's fringing, its white at the receiver's clip, and no stage of the camera's (the lens, its vignette, the sensor) on the characters. Godot's OSD SHALL stay one switch away for the FEED-00 A/B.

#### Scenario: The grid matches the Godot OSD
- **WHEN** the Unity and Godot OSDs are rendered at the same field
- **THEN** every glyph lies in the same cell, and the strings read "Svinoriz 10 Opto" and "! SAFE !"

#### Scenario: The OSD goes through the channel with the picture
- **WHEN** the frame-metrics measure runs on the feed's OSD and on the references' OSD (A–H, L, P), alike
- **THEN** the glyphs' horizontal edges rise 10–90 % within the references' range (3.3–3.9 px on one-texel strokes, 3.2–5.3 px on wide ones), one-texel strokes peak within 0.75–0.87 of the flat ink, the flat ink sits at the receiver's clip (252–255) wherever it is in the frame, and with the OSD's cells masked the feed's output is the same as before, bit for bit

### Requirement: Content from the sim state
The OSD's values SHALL come from the sim state through the OSD content model (OSD-03): pack voltage, flight time and the other OSD-03 fields, with an invented value wherever a real one would identify anything.

#### Scenario: The voltage follows the pack
- **WHEN** the pack sags in a full-collective pull
- **THEN** the OSD's voltage falls with `FeedSignals`' pack voltage within one OSD refresh
