## ADDED Requirements

### Requirement: Legs on the ground
Each leg SHALL be a spring-damper in series with the ground, with its stiffness and damping in `drone.json`. Its feet are a skid: load-bearing spheres at the overhang end and the T-joint on `SampleGround` and on objects, and the skid's capsule for a crossing between them (PHB-05). The landing bounce SHALL fall out of the leg compliance and the residual thrust, never scripted and with no restitution coefficient.

#### Scenario: Hold the sticks and it leaves the ground
- **WHEN** the drone lands at 0.5 m/s on flat ground (the world-query stub's floor, then the test range), once with the sticks held at the descent's sweet spot and once with the throttle cut at touchdown
- **THEN** with throttle held it separates from the ground again and logs the separation height, and with throttle cut it stays down within 2 s

### Requirement: The launch stand and object contacts
With the legs off, the drone SHALL rest on the four-post launch stand (K§5.4: posts about 0.25 m high under each motor, unanchored; static at G7) by its arm plates through ordinary contacts in `steel`, the stand a runtime object the log records. Arms, body, coil and prop discs SHALL be the contact shapes `drone.json` gives them, each with its own compliance, swept against `StaticContacts` under the Time < 1 latch rule (K§7.5, the plane refreshed only while the part's core is outside the shape), with load-bearing parts as spheres (PHB-05). An element entering a prop disc SHALL be a logged strike.

#### Scenario: Liftoff from the stand
- **WHEN** the drone on the stand takes off with a collective ramp
- **THEN** it leaves all four posts, the stand stays standing, and each contact made and broken is logged with its surface and material

### Requirement: Crashes and tunnels
A contact whose impulse or acceleration passes the crash threshold SHALL end the flight with a logged crash naming the part, the object and its material. A swept element found past a surface it should have met SHALL log a `tunnel` event as a bug.

#### Scenario: Into a pole
- **WHEN** the drone flies at 10 m/s into the straight L01 pole (its collision as a runtime object on the stub, then the test range's)
- **THEN** the flight ends in a crash naming the part that hit, `L01_straight` and `timber`, with the peak acceleration and the impulse, and no tunnel event is logged
