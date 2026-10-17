# Object sheet: <group name>

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist.

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values.

## <ID> <Asset name>

**Seen in:** letters (and frames by number only if useful), or **R**: research only.
**Research sources:** URLs of general, non-identifying references (construction, typical look, war damage).

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| … | … m | … m | measured / published / derived / assumed |

### Construction and materials
- What it is built from, layer by layer. For example: adobe core, clay render, lime whitewash, red-clay base band.
- **Materials:** CC0 set id per material, plus any derived texture needed (tint, facade map, damage mask).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|

### Variants and states
- The variant parameters (what differs between variants 1, 2, 3…).
- States (I/D/X or A/B/F): what changes, physically, in each.

### Damage pattern (if any)
- What breaks first, how it fails, and what lies around it (rubble composition and colour, soot, debris spread).

### Physics role
- **Collision:** the primitives plan; which surfaces the drone can touch.
- **Material ids** per part.
- **Gaps:** each opening's polygon, its narrowest width, and whether it is passable by the drone (0.574 m nose-first, 0.706 m diagonal).
- **Wind volumes:** parts and porosity per part.
- **Snag hazards:** where they are.
- **Landable surfaces.**

### Placement rules (for the world)
- Where it occurs, how often, with what neighbours, at what orientation.

### Generator parameters
| Param | Type | Range | Default | Effect |
|---|---|---|---|---|

### Open points
- Anything uncertain, with how it will be settled. Research first; ask the pilot only if only a pilot can know.
