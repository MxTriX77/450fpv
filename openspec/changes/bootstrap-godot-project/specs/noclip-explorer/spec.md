# Spec Delta

## Purpose

A free-flying camera the user explores the world with (M1 patch reviews and the M2 full-map walkthrough). It has predictable speeds and no collision.

## ADDED Requirements

### Requirement: Controls
The noclip camera SHALL use:
- mouse look while the mouse is captured
- W/A/S/D to move along the view direction and the view-right direction
- E or Space to move up and Q or Ctrl to move down, along world vertical
- Shift for a fast multiplier
- the scroll wheel to change base speed
- Esc to release or recapture the mouse

Mouse sensitivity SHALL be a single exported setting. Vertical look SHALL clamp at ±89°.

#### Scenario: Mouse release
- **WHEN** the user presses Esc while the mouse is captured
- **THEN** the cursor is released and mouse movement no longer turns the camera, and pressing Esc again recaptures it

#### Scenario: Look clamp
- **WHEN** the user keeps moving the mouse up
- **THEN** the pitch stops at +89° and the view never flips

### Requirement: Speeds
The default base speed SHALL be 6 m/s. Scroll SHALL change it in steps of ×1.25 within 1–60 m/s. Holding Shift SHALL multiply the current speed by 8. Movement SHALL be frame-rate independent and have no collision.

#### Scenario: Base speed
- **WHEN** W is held for 1.0 s at the default speed, looking level
- **THEN** the camera moves 6.0 m ± 5 % forward, whatever the frame rate

#### Scenario: Fast speed
- **WHEN** Shift + W is held for 1.0 s at the default speed
- **THEN** the camera moves 48 m ± 5 %

#### Scenario: Through geometry
- **WHEN** the camera flies into a wall or the ground
- **THEN** it passes through without stopping
