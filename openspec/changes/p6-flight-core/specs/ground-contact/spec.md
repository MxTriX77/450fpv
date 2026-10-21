## ADDED Requirements

### Requirement: Legs on the ground
Each leg SHALL be a spring-damper in series with the ground, read from `SampleGround` at its foot, with its stiffness and damping in `drone.json` (assumed until `p5-drone`). The landing bounce SHALL fall out of the leg compliance and the residual thrust, never scripted.

#### Scenario: Hold the sticks and it leaves the ground
- **WHEN** the bootstrap drone lands at 0.5 m/s on flat gallery ground, once with hover throttle held and once with throttle cut at touchdown
- **THEN** with throttle held it separates from the ground again and logs the separation height, and with throttle cut it stays down within 2 s

### Requirement: The launch stand and object contacts
With the legs off, the drone SHALL rest on the four-post launch stand (K§5.4: posts about 0.25 m high under each motor, unanchored) through ordinary contacts in `steel`. Arms, body, coil and prop discs SHALL be swept spheres or capsules against `StaticContacts` under the Time < 1 latch rule (K§7.5), with load-bearing parts as spheres (PHB-05). An element entering a prop disc SHALL be a logged strike.

#### Scenario: Liftoff from the stand
- **WHEN** the drone on the stand takes off with a collective ramp
- **THEN** it leaves all four posts, the stand stays standing, and each contact made and broken is logged with its surface and material

### Requirement: Crashes and tunnels
A contact whose impulse or acceleration passes the crash threshold SHALL end the flight with a logged crash naming the part, the object and its material. A swept element found past a surface it should have met SHALL log a `tunnel` event as a bug.

#### Scenario: Into a pole
- **WHEN** the drone flies at 10 m/s into the gallery's straight L01 pole
- **THEN** the flight ends in a crash naming the part that hit, `L01_straight` and `timber`, with the peak acceleration and the impulse, and no tunnel event is logged
