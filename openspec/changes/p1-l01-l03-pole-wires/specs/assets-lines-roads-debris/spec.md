# Spec Delta

## Purpose

The assets of the lines, roads and debris object sheet (poles and wires L01–L05, road kits RD01–RD03, debris kits K01–K10): what each built asset must show and carry, judged against its reference letter and its object sheet. One requirement per asset id.

## ADDED Requirements

### Requirement: L01 wooden pole
The pipeline SHALL build asset L01 (`pole` generator; variants `L01_straight`, `L01_leaning`, `L01_braced`, `L01_stub`) from its params files into Unity prefabs that copy the village wooden pole: a tapered weathered log with hooks and pin insulators in a zig-zag (or a short crossarm), the leaning variant at 9° with its short crossarm 1.2–2 m below the top and no brace, as letter L shows (task 1.3), the braced variant a line's end pole with a single brace log, the stub variant banded to a concrete stub. Dimensions SHALL sit inside `docs/object-sheets/lines-roads-debris.md` §L01 and the shared pole table; the physics metadata SHALL carry the log as tapering capsules within 10 mm of the visual, the stub as a box, the brace as a capsule, each hook as a capsule and each insulator as a cylinder with the sheet's material ids, the brace triangle as a gap polygon, a solid wind volume per log (the pole's, and the brace's on the braced variant, the triangle between them open), the sheet's `hook` and `edge` snags, named `ports` for the wires, and no landable surface.

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 L01` runs on a clean checkout
- **THEN** it exits 0 for the four variants, writes their FBX, metadata, prefabs and review sheets, and a second run leaves `git status` clean

#### Scenario: Brace gap against the drone
- **WHEN** `L01_braced`'s metadata is read
- **THEN** it holds one gap polygon for the triangle between pole, brace and ground with `narrowest_m` within 5 mm of 1.611 m (derived: the real clear space between the pole's and the brace's surfaces for a 25° brace attached at 0.65 of the 7.5 m height, AST-05's gap rule; the sheet's 1.78 m is the centre-line triangle at a rounded 4.9 m attachment, task 1.1), and the audit's inscribed-circle check agrees

#### Scenario: Night silhouette kept to 1 km
- **WHEN** the LOD distances are read
- **THEN** LOD1 takes over where a 0.075 m insulator drops under 2 px (about 35 m), LOD2 is a tapered prism of the pole alone, LOD1 and LOD2 are drawn with the thin-wire coverage (task 1.4's material: the radius in uv2, radial normals), so from about 190 m out the pole fades as its width over the distance instead of breaking into dashes, with no empty row across four quarter-pixel camera shifts at 200 m and at 1 km, and the cull distance is at least 1 000 m

#### Scenario: Matches letter L
- **WHEN** the review sheet's 10 m and 50 m tiles of `L01_leaning` and the turntable are compared side by side with letter L (frames 1–3) in the local comparison sheet
- **THEN** the lean, the short crossarm 1.2–2 m below the top (measured, L 1–3), the taper and the absence of a brace match by eye, and the weathered log's grey-silver reads against the sheet's albedo

### Requirement: L03 wire span
The pipeline SHALL build asset L03 (`wire_span` generator; variants `L03_thin`, `L03_medium`, `L03_thick`) from its params files into Unity prefabs where every conductor is a tube mesh of its real diameter (3–32 mm) hanging as a catenary between two attachment heights with the sheet's sag ratio, spacing and arrangement; the metadata SHALL carry each conductor as a `wires` record (point list at `points_per_m`, diameter, mass per metre, sag, material `cable`), no gap, no wind volume, and the whole wire as a snag. A wire SHALL never be a line primitive or a camera-facing strip standing in for geometry.

#### Scenario: Built from params by one command
- **WHEN** `pwsh tools/build_asset.ps1 L03` runs on a clean checkout
- **THEN** it exits 0 for the three variants, each FBX holds one tube per conductor, and a second run leaves `git status` clean

#### Scenario: Catenary and clearance
- **WHEN** `L03_medium` (40 m span, 4 bare conductors hung from `L01_straight`'s four ports, 7.355 m stepping down 0.5 m, sag 2 %) is built
- **THEN** every conductor's lowest point is within 10 mm of its port's height minus 0.8 m, the lowest conductor's lowest point is 5.055 m (derived: `L01_straight`'s lowest port at 5.855 m less the 0.8 m sag; the generator's `check` refuses any span whose lowest point falls under the published minimum clearances for village lines, 3.5 m bare, 2.5 m SIP, 2.75 m at a service drop, task 1.2), and the `wires` record's points reproduce the tube's centre line within 5 mm

#### Scenario: Nearly invisible, never shimmering
- **WHEN** the 10 m, 50 m and 200 m tiles of `L03_thin` are read with the thin-wire material
- **THEN** the 5 mm wire is a faint continuous line at 10 m, fainter at 50 m, and gone or near gone at 200 m, with no broken or sparkling segments, judged against letter J (the drop against the sky) in the local comparison sheet
