# Spec Delta

## ADDED Requirements

### Requirement: Link loss from the physics
The receiver SHALL take the physics' link margin and state (PHY-20, PHY-21, PHY-25): while the margin is below its threshold the picture SHALL be lost as a recovering dropout and come back clean when the physics' dip ends; a break SHALL play the staged loss, or the hard cut, chosen by how fast the margin fell (over a field or more: staged; within a field: K§11.3's branch), and stay blue until reset. Each loss SHALL carry its physics cause (the dip's or the break's event) into the review overlay and the log.

#### Scenario: A dip and a break on the feed
- **WHEN** a recorded flight with a physics dip of 0.5 s and then a fiber break is played through the feed
- **THEN** the dropout starts in the field of the dip's start and the picture returns clean in the field after its end, the break's loss starts within one field of the break with the stages and durations of the Godot feed's machine, the screen stays blue to the flight's end, and the overlay names each loss's cause
