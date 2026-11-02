# Spec Delta

## ADDED Requirements

### Requirement: Physics material rows say what a blade meets
Every row of `assets-src/physics_materials.json` SHALL carry `hardness_pa` (the indentation hardness the blade's hardness is compared with), `crush_strength_pa` (the pressure the material resists a blade carving it, null where it never yields to a blade) and `density_kg_m3` (the mass a carving blade flings), each with a label (published, derived or assumed) and a rationale naming its source or calculation, and `lib/meta.validate` SHALL refuse a row without them; a compliant thin row (straw, foliage, twig, fabric, mesh) MAY state its crush strength as null, meaning a blade passes it without damage from this row (its plants' cutting is the ground's elements).

#### Scenario: Every row says what a blade meets
- **WHEN** `physics_materials.json` is validated, and a copy whose `concrete` row lacks `hardness_pa`
- **THEN** every row has the three fields with labels and rationales, concrete, masonry, steel and glass are harder than the blade's `hardness_pa` in `drone.json`, timber, bark and rubber are softer, and the copy is refused naming the row and the missing field

#### Scenario: The materials hash covers the new fields
- **WHEN** one row's `hardness_pa` changes
- **THEN** the materials file's hash in the log header changes and a replay of an earlier log refuses it
