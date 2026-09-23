# Spec Delta

## Purpose

A buildable Godot project with a sandbox scene where any work-in-progress scene can be loaded, flown through and measured. Agents, QA and the user all rely on it.

## ADDED Requirements

### Requirement: Project builds and runs headless
The Godot project in `game/` SHALL build its C# solution with `dotnet build`. It SHALL run headless with Godot 4.7.2 .NET without errors. Neither step may need the editor to be opened by hand first.

#### Scenario: Clean build
- **WHEN** `dotnet build game/Fpv450.sln` runs on a fresh clone
- **THEN** it exits 0 with no errors

#### Scenario: Headless smoke run
- **WHEN** Godot runs headless on the project for 120 frames and quits
- **THEN** it exits 0 and prints no `ERROR` or `SCRIPT ERROR` lines

### Requirement: Sandbox loads a scene for review
The main scene SHALL be the review sandbox: sky, sun and a ground placeholder. When a scene path is given as a user command-line argument (`-- --scene res://…`), the sandbox SHALL instance that scene at the origin. Otherwise it SHALL show the ground placeholder.

#### Scenario: Load a patch
- **WHEN** the sandbox starts with `-- --scene res://scenes/sandbox/test_patch.tscn`
- **THEN** the test patch is visible at the origin and the camera starts looking at it

#### Scenario: Bad path
- **WHEN** the given scene path does not exist
- **THEN** the sandbox prints one clear error naming the path and still starts with the ground placeholder

### Requirement: Performance overlay
The sandbox SHALL toggle an overlay with F3 showing fps, average frame time in ms, 1 % low fps over the last 5 s, and draw calls. The overlay SHALL refresh at least 4 times per second.

#### Scenario: Toggle
- **WHEN** the user presses F3 twice
- **THEN** the overlay appears and then disappears, and while shown it refreshes at least 4 times per second (counted as refreshes, not as text changes)

#### Scenario: Budget on the dev machine
- **WHEN** the sandbox shows only the ground placeholder on the dev machine with **vsync off**
- **THEN** the overlay reports at least 144 fps (1 % low at least 120), so the empty baseline leaves headroom above the 60 fps target. With vsync on, fps is capped at the display rate and the 1 % low reflects frame pacing, not headroom

### Requirement: Screenshot capture
Pressing F12 SHALL save a PNG of the current frame to `user://screenshots/` with a sortable name, and print the saved path.

#### Scenario: Capture
- **WHEN** the user presses F12
- **THEN** a new PNG at the window's resolution exists at the printed path
