# Spec Delta

## Purpose

The sky, sun and aerial perspective of the game by time of day and weather look: at G1 the four presets the reference letters cover; grown in phase 4 into the full time-of-day and rain model (VIS-02, VIS-03).

## ADDED Requirements

### Requirement: Sun and sky by time of day
The look setup SHALL position the sun and set the sky, ambient and horizon tone from a time of day in hours and a cloud-cover value, and SHALL expose four G1 presets: day (sun about 45° up, as the review rig), low sun (sun under 12° up, warm, as letters G and B), overcast (no sun disc, flat ambient equal in horizontal illuminance to day, as letters A and H), night (a moonless sky with the horizon faintly lighter than the ground, as letter L).

#### Scenario: Equal illuminance of day and overcast
- **WHEN** a horizontal 0.18 grey surface is rendered under day and under overcast
- **THEN** its mean luminance differs by less than 5 %

#### Scenario: Night silhouette
- **WHEN** the L01 station is viewed under night from the L viewpoint
- **THEN** the pole reads as a dark silhouette against a sky brighter than the ground, with the sheet's values as the reference (pole near black against a sky around sRGB 93)

### Requirement: Aerial perspective
The look setup SHALL apply distance haze whose strength and colour follow the preset so that a belt at 200 m reads as a dark band and the horizon matches the footage's tone (VIS-04), and the haze parameters SHALL be recorded with the preset.

#### Scenario: Belt at 200 m
- **WHEN** the belt row is rendered from the 200 m viewpoint under overcast
- **THEN** the crowns' mean luminance is below the sky's by at least the margin letter H shows between its belt and sky (the comparison sheet is the evidence), and the band is continuous, not a row of separate cards
