## ADDED Requirements

### Requirement: The log fixes the whole flight
One binary log per flight SHALL hold a header, then fixed-layout records.
- **The header:** the seed, every per-flight draw, `drone.json`'s hash, the flight-controller settings, the world content hash and query version, the contacts with `physics_materials.json`'s hash, the runtime objects the flight placed (name, world-space shapes, order and id: the launch stand), and the build, runtime and ISA flags.
- **At 1 kHz:** the full integrated state (the wind's gust among it), the stick inputs, per-rotor commands and speeds, pack voltage and current, the net force and moment, the air's and the contacts' share of them, each leg's load and compression, the crash flag, and the flight controller's filter outputs and battery reading (its filters' delay lines stay in the controller, rebuilt by a replay from the flight's start, D-045).
- **Events at step resolution:** contacts made and broken (with surface and material), prop strikes, impacts with their peak acceleration and impulse, mixer saturation, the ESCs' current limit, crashes, tunnels and hitches.

It SHALL be written through a ring buffer and a worker, stored under the user's profile, and hold sim data only (LOG-05).

#### Scenario: A truncated log still reads
- **WHEN** a log is cut mid-record
- **THEN** the summary reader prints the flight up to the last whole record and says where it was cut, and a replay of it matches to the cut, even when the cut falls between a step's record and its events

### Requirement: Headless bit-exact replay
`tools/replay` SHALL reproduce every logged state bit-for-bit without Unity. It SHALL refuse a log whose world, drone or version differs, naming the first diverging step and field.

#### Scenario: Replay matches the flight
- **WHEN** a 120 s flight with contacts and a landing is replayed headless
- **THEN** every state record matches bit-for-bit, and a replay against a changed `drone.json` is refused, naming the hash
