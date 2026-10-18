# Object sheet: vehicles (R01–R09, K07)

**Owner:** reference-analyst. **Reviewed for buildability by:** asset-artist (task 1.7, pending).

**OPSEC:** cite clips and photos by letter only. Describe classes, never a specific identifiable building, place, date or person. No file names, and no OSD values. Number plates, markings and any text on a vehicle are never copied: a generated plate is blank or absent.

**Rows:** R01–R09 (vehicles, [asset-list.md](../asset-list.md) §9, each with its A abandoned, B blast-wrecked and F burnt-out state kits) and K07 (glass crumbs and vehicle fragments, §10). **Requirements:** AST-02, AST-04, AST-05, AST-06, REAL-01…05, SEC-01, SEC-02, PRC-06, PRC-09 · **Decisions:** D-013, D-024, D-028 · **Change:** `p1-object-sheets`, task 1.5.

## How this sheet was made

- **Letters read:** the three vehicle photos M, N and O, each as a single still, plus local zoom crops of each (M: the front, the near side and the debris in front of it; N: the rear half, the front half and the ground; O: the truck and the ground), kept in the ignored `reference/_frames/<letter>/zoom/`. No clip shows a vehicle (terrain.md §5 and §9), so there are no contact sheets for this group.
- **Reused, not re-measured:** terrain.md §5 (R0 the two kinds of state, R1–R5 the classes) and its `photo` colours for M, N and O; knowledge.md §5.4 (the drone's widths), §7.4 (object materials), §9.2 (vehicle sizes) and §14.4 (windows are empty frames or broken glass, rubble colours); the houses-and-yard sheet's drone-against-an-opening arithmetic; the earthworks-and-surfaces sheet's S11 asphalt row (the glass-crumb cover, μ 0.30/0.20) and its wheel gauges. Where this sheet gives a number terrain.md already gives, terrain.md is the source.
- **Scale cues in the photos:** M: the wheel (165/80R13, Ø 0.594 m, derived from the published tyre size below), the classic sedan's published wheelbase 2.424 m and its headlamp ring (about 0.18 m, assumed). N: the alloy wheel (a 14-inch fitment, Ø about 0.58 m, assumed) and the door length. O: the heavy truck's published track 2.0 m and its wheel (Ø about 1.14 m, derived).
- **Labels:** every number is **measured** (from a letter), **published** (with its source), **derived** (with the derivation) or **assumed**. Published model data come from encyclopedia and manufacturer-type pages; where two sources disagree both are given.
- **Colours:** the photos M, N and O are the only references, so every measured colour is a `photo` value (mean sRGB of a hand-picked region, closer to albedo than the feed). There is **no feed check value** for any vehicle: no clip shows one. The REAL-03 comparison for this group is therefore a noclip view against the photo, not a feed frame.
- **IDs:** the road kit was renamed RD01–RD03 by the orchestrator, so R01–R09 are the vehicles only. This sheet's headings are the vehicles; see Open points.

### Research used across the group (published)

| Topic | Finding used here | Source |
|---|---|---|
| Classic ВАЗ sedan (2101) | 4073 × 1611 × 1382 mm (one table gives 1440), wheelbase 2424, track 1349 / 1305, clearance 170, mass 955 kg; a four-door sedan; a thicker-gauge bodyshell than its Italian original | https://en.wikipedia.org/wiki/VAZ-2101 · https://ru.wikipedia.org/wiki/ВАЗ-2101 |
| ВАЗ 2109 | 4006 × 1650 × 1402, wheelbase 2460, track 1400 / 1370, clearance 165, 945 kg; five-door hatchback | https://ru.wikipedia.org/wiki/ВАЗ-2109 |
| ВАЗ 2121 Niva | 3740 × 1680 × 1640, wheelbase 2200, track 1430 / 1400, clearance 220, 1150 kg; three-door | https://ru.wikipedia.org/wiki/ВАЗ-2121 |
| ВАЗ factory tyres | 2101: 13-inch, 165/70–165/80 R13; 2109: 175/70 R13 (165/70 R13 on some versions); Niva: 175/80 R16 or 185/75 R16 | https://auto.ru/catalog/cars/vaz/2101/specifications/razmer-koles/ · https://auto.ru/catalog/cars/vaz/2109/specifications/razmer-koles/ · https://auto.ru/catalog/cars/vaz/2121/specifications/razmer-koles/ |
| ВАЗ body construction (2108 family) | One welded steel shell; main panels 0.8 mm, outer door panels and front wings 0.7 mm, reinforcements 1–1.2 mm, front side members 1.8 mm; low-carbon steel, only the most corrosion-prone parts zinc-coated | https://vazbook.ru/en/09/2108/body/exterior/karkas-i-operenie-kuzova |
| Outer skin gauge, general | Roof, bonnet, tailgate and door skins 0.6–0.8 mm | https://patents.google.com/patent/US20170001670A1 |
| Daewoo Lanos | Sedan 4237, hatchback 4074 × 1678 × 1432, wheelbase 2520, track 1405 / 1422, clearance 155 (165 for the CIS), 990–1177 kg; built in Ukraine until the late 2010s, long the best-selling car there | https://ru.wikipedia.org/wiki/Daewoo_Lanos · https://en.wikipedia.org/wiki/Daewoo_Lanos |
| Chevrolet Aveo (T200/T250) | Hatchback 3880–3920, sedan 4235–4310 × 1670–1710 × 1485–1505, wheelbase 2480, track 1450 / 1410–1430, clearance 150–155, 970–1150 kg; also assembled in Ukraine | https://ru.wikipedia.org/wiki/Chevrolet_Aveo · https://en.wikipedia.org/wiki/Chevrolet_Aveo_(T200) |
| Chery Amulet (A15) | Five-door liftback, 4393 × 1682 × 1424, wheelbase 2468, track 1426 / 1419, clearance 121, 1100–1140 kg; derived from a first-generation Spanish compact; assembled in Ukraine | https://ru.wikipedia.org/wiki/Chery_Amulet · https://en.wikipedia.org/wiki/Chery_A15 |
| VW Passat B3 / B4, Golf II | B3: 4575 (text: 4670) × 1705–1720 × 1430 (sedan) / 1460 (estate), wheelbase 2625–2630, track 1485 / 1425, 1130–1380 kg; B4: sedan 4610, estate 4597 × 1714 × 1433 / 1491, wheelbase 2624; Golf II: 3985–4054 × 1665–1700 × 1415, wheelbase 2470, 3- and 5-door, a 175/70 R13 fitment | https://de.wikipedia.org/wiki/VW_Passat_B3 · https://en.wikipedia.org/wiki/Volkswagen_Passat_(B4) · https://en.wikipedia.org/wiki/Volkswagen_Golf_Mk2 |
| ЗИЛ-130 | 6675 × 2500 × 2400, wheelbase 3800, track 1800 / 1790, clearance 275, kerb 4300 kg (another table: mass 4900), payload 5–6 t; platform inside 3752 × 2326, sides 685 mm, later 575 mm; tyres 9.00R20 (260-508) | https://ru.wikipedia.org/wiki/ЗИЛ-130 · https://en.wikipedia.org/wiki/ZIL-130 · https://www.zr.ru/archive/zr/1963/11/izuchaiem-novyie-modieli · https://buhgalter911.com/spravochniki/transport/normy-resursa-pnevmaticheskih-shin/vantazhni-avtomobili/5-1-vantaghni-950813.html |
| Урал-4320 | 7366 × 2500 × 2715 (3005 with the tilt), wheelbase 3525 + 1400, track 2000, clearance 400, kerb 8050 kg; tyres 14.00-20 (370-508) with central inflation; a metal body with a tailgate, removable bows and a canopy, folding side benches | https://ru.wikipedia.org/wiki/Урал-4320 · https://en.wikipedia.org/wiki/Ural-4320 · https://truck-encyclopedia.com/coldwar/ussr/ural-4320-6x6-truck.php |
| Урал platform (secondary) | 375D: inside 4.42 × 2.32 m, sides 0.62 m, steel floor, drop tailgate; 4320-family 4×4: about 3.89 × 2.33 m | https://legion.wplus.net/guide/army/tr/ural375.shtml · https://hi-news.pp.ua/tehnka-tehnologyi/print:page,1,7088-vantazhniy-avtomobl-pdvischenoyi-prohdnost-ural-43206-tehnchn-harakteristiki.html |
| Tempered glass | Breaks into small granular, thumbnail-sized chunks; car side and rear windows are traditionally tempered | https://en.wikipedia.org/wiki/Tempered_glass |
| Laminated glass | Glass–polymer–glass; an impact that does not pierce it leaves a spider-web of radial and concentric cracks and the pane stays in place; the windscreen is laminated | https://en.wikipedia.org/wiki/Laminated_glass |
| Windscreen build-up | Two glass sheets 2.1 / 2.1 mm (or 2.1 / 1.6) bonded with PVB; PVB film 0.76 mm | https://environdec.com/library/epd11684 · https://www.environdec.com/library/epd25026 |
| Burnt vehicles | In the hottest areas all paint burns off and the bare steel rusts; aluminium parts melt (aluminium melts at 660 °C); tyres ignite from dripping burning material and then spread the fire | https://depts.washington.edu/vehfire/fire-pattern/ · https://en.wikipedia.org/wiki/Aluminium · https://saemobilus.sae.org/papers/full-scale-burn-tests-side-side-terrain-vehicles-2018-01-1437 |

### Reference views for this group (REAL-02)

Viewpoint heights and distances are estimated from the scene (the wheels, the wheelbase, the horizon against the roof line), never from the display. None of the three backgrounds is recreated: the gallery and the world place the vehicle in a generic village street, yard or field edge of the same class, and only the vehicle, its ground and its debris are compared.

| Letter | Scene type | Viewpoint | Framing | Light | Time of day | Weather |
|---|---|---|---|---|---|---|
| M | an abandoned classic sedan (R01, A) parked at the edge of a dusty asphalt street; a mesh fence and a pale masonry wall behind; bare trees | 1.3–1.6 m (hand-held; the roof line sits near the horizon), 4–7 m from the car (the front wheel about 4 m), looking down 15–20° | front three-quarter from the car's near side, 30–40° off its axis; the car fills the middle two thirds, nose to the left; the debris scatter of lamp parts and chips in the lower third | bright sun, high and beyond the car, so the near side is in shade and the bonnet and roof are lit; short hard shadows under the car | midday to early afternoon | clear, dry; late winter or early spring (bare trees) |
| N | a blast-wrecked small five-door hatchback (R04/R05 class, B) standing in a wide paved open area strewn with strike debris | 1.5–1.7 m (the horizon above the roof line), 4–6 m from the car's side | side view 20–30° off the side normal, nose to the right; the car fills the width; broken concrete blocks, planks, sheet metal, a fallen trunk and paper in the lower third; distant structures not recreated | overcast, flat, no shadows | daytime | overcast, cold, dry ground; winter |
| O | a burnt-out heavy bonneted truck (R09 cab class, F) on a slushy road with snow all around | 1.5–1.7 m, 20–30 m from the truck | front three-quarter from the near side; the truck in the upper-left third; the snow-covered verge with dark scattered fragments fills the lower half | overcast, diffuse snow light | daytime | overcast, snow on the ground, wet slush on the road; winter |

The REAL-03 side-by-side sheets for these views are made in `reference/_compare/` once the first vehicle (R01 in A and F, the look-lock asset) exists; nothing exists to compare yet.

## Rules shared by every vehicle

### One generator: a clean base plus state kits

Every model is one generator preset (`vehicle`, params per model) that builds a **clean base** from the published dimensions, then applies **one state kit** (A, B or F), then scatters the state's **debris field** from K07 (asset-list §9). The kits are physical edits of the base, never texture swaps: they move, cut, bend, remove and add geometry, and the collision, wind volumes and snags are rebuilt from the edited geometry. The same seed and params always give the same wreck.

### Geometry (REAL-05, AST-05)

- **Geometry:** the body shell with real panel thickness (0.7–0.9 mm, rendered with its edge), every opening (window, door, wheel arch, fragment hole ≥ 30 mm), torn and folded panels, sprung doors, the bonnet fold, glass that remains (crazed windscreen, shards in the frames), seats, steering wheel, dashboard, wheels with the tyre as a solid (tread as a normal map), rims, bead wires of burnt tyres, roof racks, truck frames, beds, hoops, hanging wiring, roof dents.
- **Decal or normal map only** (no physical role, below 2–3 cm): fragment holes under 30 mm, paint chips, rust halos and streaks, dust, soot film, crazing lines on the laminated windscreen (the crazed pane itself is geometry), panel lines.
- **Collision follows the visual** within ±2 cm on every face the drone can touch (roofs, bonnets, beds, door edges, pillars, torn parts). The one accepted simplification is fragment holes ≤ 0.25 m: the panel's collision stays closed over them (a hole smaller than one prop disc, 0.254 m, backed by the door's inner panel 0.08–0.12 m behind, admits nothing but a blade tip); their petalled edges are still `edge` snags. See Open points.
- **Windows are empty frames or broken glass, never a blue plane** (knowledge.md §14.4).
- **Plates are blank or absent.** No characters on any vehicle, ever (OPSEC).

### Body construction and materials

- **Cars (R01–R07):** a welded steel monocoque (unibody) of low-carbon sheet: outer skins 0.7 mm (doors, wings), main panels 0.8 mm, roof 0.7–0.8 mm, sills and pillars 1.0–1.2 mm, front side members up to 1.8 mm (published: vazbook, the 2108 family; patent US20170001670A1 for skins). The classic 2101 uses a slightly thicker shell (published: en.wikipedia). Bolted-on wings (2108 family), hinged bonnet, boot lid or tailgate, four or two side doors with frames. Bumpers: chrome-plated steel with rubber-tipped overriders on the classic ВАЗ (measured M), black plastic covers over a steel beam on the later cars (published classes, terrain.md R1–R3).
- **Glazing:** the windscreen is laminated (2.1 + 0.76 PVB + 2.1 mm, published), the side and rear windows tempered (3–5 mm, assumed typical). Laminated glass crazes in place, a spider-web of radial and concentric cracks around each impact, holed where pierced, and stays in the frame (published; M shows exactly this). Tempered glass bursts entirely into 5–15 mm cubes (published "thumbnail-sized"), which fall inside and under the window: a 0.36 m² side window of 4 mm glass is 1.44 litres, 3.6 kg, about 3600 cubes of 10 × 10 × 4 mm (derived). On the classic ВАЗ the front doors carry a small quarter vent in a chrome frame (measured M).
- **Wheels:** steel wheels with domed chrome hubcaps on the classic ВАЗ (measured M); steel or 14–15-inch alloys on the later cars (measured N: ten-spoke alloys on the hatchback class). The tyre is a rubber torus with steel bead wires at the rim seat (published general tyre construction).
- **Interior:** front seats with headrests (later cars) or without (early classics), a rear bench, a dashboard, steering wheel Ø 0.38–0.40 m (assumed), door trims; fabric or vinyl over foam over a steel frame with coil or zigzag springs (assumed typical).
- **Trucks (R08, R09):** a ladder frame of two steel channels, bonneted steel cab (three seats, bench), a separate bonnet and wings, leaf-sprung axles. R08: a timber-plank platform with drop sides on steel cross members (assumed typical of the class; the asset list's flatbed). R09: a steel platform with a drop tailgate, folding side benches, removable steel bows and a canvas tilt (published), the spare wheel carried behind the cab (assumed).

### How vehicles fail: the three state kits

The photos give one example of each state (terrain.md R0); the kits generalise them. Every change is physical.

**A abandoned (M).** The car is whole but holed and decaying.
- Fragment holes in the panels facing the burst: in M, about 6 large ones on the near side (0.08–0.25 m: a torn wing around the headlamp about 0.20 × 0.25 m, the front door low about 0.15–0.18 m, the rear door 0.09 m) and about 10 small ones (10–30 mm), measured against the wheel. Large holes have petals bent inward 10–30 mm (the burst's side) with a rust halo 1.5–2.3 × the hole (M: the rear door's 0.09 m hole sits in a 0.21 m halo). Pale primer or filler shows where paint flaked in 10–50 mm chips.
- The bonnet torn at one rear corner and folded up into a triangular flap about 0.35–0.45 × 0.6 m standing against the windscreen (M); the windscreen crazed across the whole pane, bulged inward and holed where the flap's edge struck (M).
- Side glass gone (crumbs on the seats and under the doors), the vent frames intact (M).
- One flat tyre (M: the front tyre; the sidewall folded at the patch), that corner 0.04–0.08 m lower (measured M: the hub-to-ground distance against the hub-to-top distance, about 5 cm), the body leaning 1–2° (derived: 0.05 m over a 1.35 m track and a 2.42 m wheelbase); terrain.md's general 5–10 cm and 1–3° agree.
- A small scatter of the car's own parts: a headlamp bowl, a chrome bezel ring, a flat trim plate about 0.3–0.4 × 0.2 m, chips, within 1–4 m of the struck corner, densest within 2 m (measured M).
- With time (assumed, general: terrain.md R0): a dust film on every upward surface, leaf litter in the scuttle and under the car, rust streaks running 0.05–0.3 m down from hole bottoms and seams, the paint fading and chalking, every tyre flat with cracked sidewalls, and off asphalt weeds 0.3–1.5 m around and through the body (T7).

**B blast-wrecked (N).** A burst close by tears and bends the shell.
- Doors bulged outward 0.05–0.10 m and sprung from their latches by 0.03–0.10 m at the trailing edge, their skins creased and torn open around the handle, window frames bent outward (N, both doors on the near side).
- The bonnet's outer skin gone and its inner frame, with its 0.15–0.25 m lightening holes, torn free at the front and folded up and back, standing about 0.45 m above the wing line (N).
- The roof pushed down 0.05–0.15 m behind the windscreen with a crease (N, estimated); the windscreen and all glass gone.
- Panels peppered with 10–30 mm holes, 20–40 on the burst side (N), larger tears at the corners; a tail lamp gone leaving an empty 0.15 × 0.12 m socket; the rear bumper cover hanging; wiring and loose trim hanging out of the body, a wiring loom about 10 mm thick looping 0.6–0.8 m across a door opening (N), the door mirror hanging on its cable.
- A soot-grey film over the paint from the burst (N: the paint colour can't be read) without the car burning: tyres still on and inflated, seats in place (N). The car stands at its normal height.
- Debris: the car's own parts (bumper covers, trim, lamp lenses, glass crumbs) within 2–8 m, biased away from the burst; whatever else the strike threw (N: concrete blocks, planks, sheets, a trunk, paper and plastic over tens of metres) is the shared debris kit's (K01, K03, K04, K08), not the vehicle's.

**F burnt-out (O, and research).** A fire after the strike (or after B) consumes everything combustible.
- Tyres burn away; the car sits on its rims. **Drop (derived):** loaded tyre radius minus the rim flange radius, plus spring sag: a 165/80R13 car loses 0.282 − 0.183 = 0.10 m at the tyres, plus 0.05–0.10 m as its coil springs lose temper (assumed), so **0.15–0.20 m**; on alloy wheels the rims melt at 660 °C (published) and the hub settles toward the brake disc, **0.20–0.25 m**; trucks on leaf springs lose only the tyre: ЗИЛ 9.00R20 0.47 − 0.28 = **0.19 m**, Урал 14.00-20 0.53 − 0.28 = **0.25 m** (derived from the published tyre sizes). This matches the asset list's 15–25 cm. Each burnt tyre leaves its bead wires as two loose steel rings on the ground or round the rim, and a dark ash ring 0.1–0.3 m wide.
- Paint: soot-black where the fire was coolest, burnt to bare grey-white steel where it was hottest (the engine bay, the cabin roof, the fuel tank side); bare steel turns rust-brown within weeks (published: the burnt-off areas rust; the timing assumed). O shows the fresh state: the cab soot-black.
- Glass gone, or slumped and fallen into the cabin in pieces; the laminated windscreen's PVB burnt so it falls in sheets (assumed).
- Interior burnt down to the seat frames, the springs and the steering wheel's steel core; plastics gone (dashboards, door trims, bumper covers on later cars, leaving the steel beam), lamp housings melted (assumed, terrain.md R0).
- The roof sags 0.02–0.10 m as the hot roof softens (assumed); doors may hang open (O: the cab door open).
- Truck: a canvas tilt burns away and leaves bare steel bows (terrain.md R0); a timber platform burns to char stubs on its steel cross members.
- Ground: a dark soot and melt patch 2–6 m across under and around the wreck (terrain.md R0): on asphalt a glossy black melt and soot film, on soil a scorched ring with the grass burnt to ash (Poly Haven `burned_ground_01`), on snow a melted wet patch (O: the slush around the truck merges with the road's own slush, so no separate ring is readable). Fragments thrown by the strike 5–20 m out (O: dark pieces on the snow, roughly 10–20 m around the truck).

### The drone against a vehicle's openings (gaps)

The drone is **0.574 m nose-first and 0.706 m on the diagonal**, and about **0.55 m tall with legs** (houses-and-yard sheet, How to read). A vehicle opening is only a gap if the drone can go **through** it and out again; a hole into a closed cavity is an obstacle pocket.

| Opening | Clear size | Against the drone | Gap? |
|---|---|---|---|
| Car side window, glass gone | 0.40–0.50 m tall (measured M: about 0.45 m) × 0.50–0.80 m wide | the height is below the 0.55 m envelope with legs; and the cabin behind it has no exit: the seat back, headrest and steering wheel leave 0.4–0.5 m fore-aft at window height (assumed seat geometry) | **no** (asset list: "window openings 0.4–0.5 m are too small") |
| Car windscreen or rear window, glass gone | 1.2–1.4 m wide, 0.5–0.6 m along the slope, raked 35–60° from vertical, so 0.25–0.45 m tall seen level (derived: slope × cos rake) | too low, and the dashboard and seats fill the space behind it | **no** |
| Under a car | clearance 0.12–0.22 m (published per model) | below the drone's height | **no** |
| Car cabin, both doors of the **rear row** missing or open ≥ 75° (B or F, generator option) | door openings 0.75–0.85 × 0.85–0.95 m; inside, 0.55–0.75 m fore-aft between the front seat back and the rear seat back, 0.70–0.75 m from the rear cushion to the roof (assumed seat geometry; burnt seats open it to 0.65–0.85 m) | passes nose-first where the fore-aft clear exceeds 0.574 m: at 0.65 m within ±16° yaw, 38 mm a side; the front row is blocked by the steering wheel and the dashboard | **yes, only when generated** (the default kits keep the doors on, as in N); the generator records the real clear polygon |
| Truck cab, door open, glass gone | door opening 0.8–0.9 × 1.1–1.2 m (assumed) | a pocket: the bench, the steering wheel (Ø 0.5 m) and the gear levers leave no straight path across | **no** |
| Under a truck between the axles | 0.6–0.9 m from the ground to the frame (derived from the wheel radius and spring stack) | crossed by the driveshafts, fuel tanks and spare-wheel carrier at 0.45–0.75 m (assumed chassis layout) | **no**; the generator records one only if its chassis leaves a clear crossing |
| R09 F: bare bows over the bed (tilt burnt) | side bays 0.93 × 0.83 m, rear opening 2.33 × 0.83 m above a closed tailgate (1.45 m with it down), top bays 0.93 × 2.33 m (derived in R09) | ≥ 0.706 m: **passes at any yaw**, 62 mm a side on the diagonal at 0.83 m | **yes**: a real fly-in cage (see R09; the asset list's "no gaps" holds for the cars, not for this cage) |
| R08 F: burnt timber floor | gaps between steel cross members 0.40–0.50 m (assumed) | below 0.574 m | **no** |

### Physics, shared (the pipeline's terms)

- **Collision** (meta schema 1: boxes, cylinders, capsules, spheres; each with a material and a landable flag): a car is 20–35 primitives: the lower body as 3 boxes (front, cabin, rear sections following the plan taper) from the sill to the belt line; the bonnet, boot lid and roof as thin boxes (0.02 m) on top; the A, B and C pillars as capsules (r 0.04–0.05 m); glass that remains as thin boxes (0.005 m); four wheel cylinders; bumpers as boxes; inside, the seats, headrests and dashboard as boxes and the steering wheel as a thin cylinder (they matter as soon as a window is open). Torn parts add thin boxes (folded bonnet, sprung door at its angle, a flap) and thin capsules (hanging wiring r 0.005 m, trim strips, roof-rack tubes r 0.01 m, bare bows r 0.02 m, seat springs). A truck adds the frame as two boxes, the cab as 3–5 boxes, the bonnet, the bed floor and sides as boxes, the bows as capsule arcs (5–7 segments each).
- **Material ids** (closed list in `assets-src/physics_materials.json`): body panels, roof, bonnet, doors, torn edges: `sheet_metal` (0.7 mm sheet; its edge cuts the fiber); frames, bumpers on the classics, rims, axles, bows, seat frames and springs, roof racks: `steel`; remaining glass panes and the crazed windscreen: `glass`; R08 platform boards: `timber`; hanging wiring: `cable`. **Proposed (physics sign-off, see Open points):** `rubber` (tyres), `plastic` (bumper covers, lamp housings, dashboards, trim), `fabric` (seats, headliners, the canvas tilt; the same id the houses-and-yard sheet proposes for tarps), `glass_crumbs` (the crumb layer as a ground cover).
- **Wind volumes** (bluff bodies; porosity = open-area fraction): lower body 0 in every state; the greenhouse (belt line to roof) 0 with its glass in, 0.35 with the side glass gone (A, as M: side windows about half the greenhouse side, seats and headrests blocking about a third of the passage; assumed), 0.45 with all glass gone (B), 0.55 burnt with the seats down to frames (F); the underbody gap is free air. Trucks: cab 0 (glass in) or 0.3 (glass gone, doors open); engine and bonnet 0.1 (grille); chassis between the axles 0.6; bed side boards 0; tilt 0 intact, 0.4 torn, **0.93 as bare bows** (derived: 0.04 m tubes at 0.975 m, plus the top ties). Wake (terrain.md R0): recirculation 2–3 heights behind, a gusty shadow to about 10 heights: cars 3–5 m and about 15 m, the ЗИЛ about 5–7 m and 24 m, the Урал 6–9 m and 30 m.
- **Snag hazards** (meta kinds): `edge` polylines on torn sheet edges (radius 0.35 mm, half a 0.7 mm skin), fragment-hole petals ≥ 0.08 m, the folded bonnet's edges, sprung door edges (a hemmed edge, r 1.5 mm), bumpers' ends, roof-rack tubes (r 0.01 m), bare bows (r 0.02 m), burnt rim flanges (r 3 mm); `hook` members for free ends: hanging wiring (r 3–6 mm), a whip antenna (classics, optional, r 2 mm, 0.6–1.0 m), burnt seat springs (wire r 2 mm), torn trim strips, a mirror hanging on its cable, bead-wire rings on the ground, torn canvas flaps (R09 B). Torn metal is sharp: the fiber dragged over it should cut more readily than over branches (knowledge.md §7.4).
- **Landable surfaces:** car roofs, flat bonnets and boot lids (slopes 3–12°), truck cab roofs, truck bonnets, truck beds; each `landable: true` in its collision box. A pushed-down (N) or burnt sagging roof is still landable but uneven and may carry glass crumbs. Sheet steel is hard, slippery and drums: `sheet_metal` gives 2e4 N/m, so the drone's 49 N weight (5 kg class) dents a roof about 2.5 mm before it holds (derived from the material row). Folded, torn or sprung parts are not landable.

### Materials: CC0 candidates

No set in `tools/assets/manifest.json` covers this group yet (it holds `red_brick` only). All candidates are CC0, to be fetched and pinned by the asset-artist; the ids were checked against the libraries' current listings.

| Material | Candidate set (CC0) | Source page | Derived textures |
|---|---|---|---|
| painted body steel (solid, faded) | ambientCG `PaintedMetal006` (green paint with rust), Poly Haven `green_metal_rust`, `rusty_painted_metal` | https://ambientcg.com/view?id=PaintedMetal006 · https://polyhaven.com/a/green_metal_rust · https://polyhaven.com/a/rusty_painted_metal | per-vehicle paint tint (the palette below), fade and chalk; the **vehicle damage mask** in asset space: R rust (halos, streaks, sills, arches), G soot, B dust and dirt, A burnt-off paint |
| rust (halos, streaks, burnt-off steel weeks later) | ambientCG `Rust007`, `Rust009`; Poly Haven `rust_coarse_01`, `rusty_metal_02` | https://ambientcg.com/view?id=Rust007 · https://ambientcg.com/view?id=Rust009 · https://polyhaven.com/a/rust_coarse_01 · https://polyhaven.com/a/rusty_metal_02 | blended by mask R |
| soot-black and burnt steel | ambientCG `Metal046B` (black, dirty) as the soot base; the rust sets above for the burnt-off areas | https://ambientcg.com/view?id=Metal046B | mask G (soot) and A (burnt to grey-white, then rust) |
| chrome and nickel (bumpers, trims, hubcaps) | ambientCG `Metal049A`, `Metal045A` | https://ambientcg.com/view?id=Metal049A · https://ambientcg.com/view?id=Metal045A | pitting and dust via mask B |
| tyre rubber | ambientCG `Rubber004` (black rubber) | https://ambientcg.com/view?id=Rubber004 | tread and sidewall normal maps generated from the tyre geometry; a dust and crack overlay |
| black plastic trim and bumper covers | ambientCG `Plastic006`, `Plastic003` | https://ambientcg.com/view?id=Plastic006 · https://ambientcg.com/view?id=Plastic003 | melt and soot via mask G |
| seat fabric and vinyl | ambientCG `Fabric030`, `Fabric031`; `Leather014` (old, scuffed) for vinyl | https://ambientcg.com/view?id=Fabric030 · https://ambientcg.com/view?id=Leather014 | tint; burnt state is geometry (frames and springs), not a texture |
| canvas tilt (R09) | Poly Haven `rough_linen`, tinted olive (no CC0 canvas set was found; the houses-and-yard sheet uses the same set for tarps) | https://polyhaven.com/a/rough_linen | olive tint, fade, dirt; tears are geometry |
| truck platform boards (R08) | the weathered-plank sets of the houses-and-yard sheet (Poly Haven `raw_plank_wall`, `rough_wood`) | https://polyhaven.com/a/raw_plank_wall · https://polyhaven.com/a/rough_wood | char mask for F |
| glass | no texture set: a shader (transparent, slight green tint) plus a **derived crack mask** generated from the impact points (radial plus concentric lines) for the crazed windscreen; crumbs are instanced geometry (K07) | — | crack mask per pane |
| soot patch on the ground | Poly Haven `burned_ground_01` (soil and grass); on asphalt a darkened S11 asphalt with a gloss melt mask | https://polyhaven.com/a/burned_ground_01 | feathered patch mask (PR-11) |

### Colour, group-wide

Photo values are measured on M, N and O (mean sRGB of hand-picked regions). Typical paint colours for models not in a photo are assumed, given as fresh paint; the generator applies fade, dust and the damage mask on top. No feed check values exist (no clip shows a vehicle).

| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| dark green solid paint, side in shade | #3d4b53 (terrain.md #38444a) | — | measured M |
| the same paint, bonnet lit from above | #6d7c81 (terrain.md #697c82) | — | measured M |
| the same paint, roof reflecting the sky | #8696a2 | — | measured M |
| rust halo round a fragment hole | #887465 (orange-tinted pixels only) | — | measured M |
| primer or filler in paint chips | #bdb092 | — | measured M |
| chrome bumper, dusty | #949ea5 | — | measured M |
| chrome hubcap | #9ea0a2 | — | measured M |
| radiator grille (bright mesh) | #898f96 | — | measured M |
| tyre sidewall in shade | #34383c (terrain.md tread #545455) | — | measured M |
| crazed laminated windscreen | #b9cbd3 | — | measured M |
| interior seat seen through the window | #6d7678 | — | measured M |
| engine bay under the bonnet flap | #50545b | — | measured M |
| sill and underside in shade | #3a4043 | — | measured M |
| glass crumbs and bright chips on asphalt | #e3e4e2 | — | measured M |
| dusty asphalt under the car | #bab7b2 (terrain.md #b7b2aa) | — | measured M |
| soot-grey blast-filmed panels | #3a3d3d–#3b3f41 (terrain.md #393c3d–#3a3d3f) | — | measured N |
| the same, front wing in deep shade | #1c1e1c | — | measured N |
| roof, dust over soot | #696e75 | — | measured N |
| tyre and alloy wheel | #1c1f1c, #1e201b | — | measured N |
| seat (dark fabric) | #1f2222 | — | measured N |
| bonnet inner frame | #151919 | — | measured N |
| debris ground near the car / farther | #4f514f / #747472 (terrain.md #5a5955–#6d6e6b) | — | measured N |
| broken concrete block | #90918e | — | measured N |
| burnt cab front | #303033 (terrain.md #1d1e21) | — | measured O |
| burnt cab door, lit edge | #585c67 (terrain.md body side #3d3c40) | — | measured O |
| burnt truck wheel | #18181c | — | measured O |
| snow / slush | #cdd5e2 / #525255 (terrain.md #ccd4e1 / #525256) | — | measured O |
| burnt-off steel, fresh | about #9a958d | — | assumed (grey-white oxide) |
| burnt-off steel after weeks | #6b3f26–#8a5434 | — | assumed (iron oxide browns) |
| soot patch on asphalt | #262524–#3a3836 | — | assumed |

**Typical paint palette (assumed, fresh sRGB):** white #e6e4dc, beige #c9b994, cherry red #8a2a24, sky blue #7fa3bd, dark blue #2d3d5a, dark green #2f4a40 (M's paint, measured above, is this class), olive drab (trucks) #5b5e3e, faded truck-cab blue #4e6a86, silver metallic #a3a6a8, graphite #4a4d50, black #1d1e20. Fade (assumed): over 5–15 years a solid paint loses 10–25 % chroma and gains a 5–15 % chalky white on upward faces; metallics fade less but lose clearcoat on the roof and bonnet.

### Debris field and soot patch (shared; the pieces are K07)

| State | Radius from the vehicle | Contents (K07 unless noted) | Density | Label |
|---|---|---|---|---|
| A | 1–4 m, densest within 2 m, on the struck side | lamp bowls, bezel rings, trim plates, plastic chips, glass crumbs under the windows | 5–20 pieces plus crumbs | measured M |
| B | 2–8 m for the car's own parts, to 20 m for small fragments, biased away from the burst | bumper covers, trim strips, lamp lenses, glass crumbs, panel shards, a door or bonnet in severe cases; the strike's own debris from K01, K03, K04, K08 | 30–150 pieces | measured N (car parts within the frame); terrain.md R0 (5–20 m, general) |
| F | 3–10 m of the strike's fragments, plus the 2–6 m soot patch | burnt shards, bead-wire rings at each wheel, melted plastic drips, aluminium puddles under alloy wheels, charred trim | 20–80 pieces | measured O (fragments 10–20 m on snow); terrain.md R0 |

The density falls off outward (about 1/r², assumed); a field is never a uniform disc and its outer edge is ragged. The soot patch is a feathered ground mask (PR-11), centred under the engine bay or cabin, 2–6 m across.

### Shared generator parameters (`vehicle`)

Every row uses this set; a row's table lists only its additions and defaults.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `model` | enum | R01–R09 | per row | Loads the base preset: the published dimensions below, the side and plan profiles, the greenhouse, the wheel positions |
| `body` | enum | per row | per row | Body style within the model (sedan, hatchback, estate, liftback; flatbed, tilt) |
| `length`, `width`, `height` | m | per row, ±2 % | published | Overall size; the profiles scale to it |
| `wheelbase`, `track_front`, `track_rear` | m | per row | published | Wheel positions |
| `ground_clearance` | m | per row | published | Sill and underbody height |
| `wheel` | enum | steel_hubcap, steel, alloy, truck_steel | per row | Rim geometry; F drop and melt behaviour |
| `tyre_diameter`, `tyre_width` | m | per row | derived from the tyre size | Wheel cylinders; the A flat-tyre and F drop |
| `paint` | sRGB or palette name | the palette above | per row | Body colour |
| `paint_fade` | 0–1 | 0–1 | 0.3 | Chroma loss and chalk on upward faces |
| `state` | enum | A, B, F | A | Which kit runs |
| `severity` | 0–1 | 0–1 | 0.5 | The kit's master dial; every kit item below defaults from it and can be overridden |
| `burst_dir` | azimuth, elevation (deg) | 0–360, −10–60 | from `seed` | Side and height the burst came from: hole side, petal direction, fold direction, debris bias |
| `frag_holes` | int | 0–60 | A 16, B 30, F 20 | Fragment holes on the burst side (and 0–20 % of them on the far side) |
| `frag_hole_size` | m | 0.01–0.25 | log-uniform, median 0.03 | Hole diameters; ≥ 0.03 m are cut in the mesh, ≥ 0.08 m get petals and an `edge` snag |
| `rust_halo` | × hole | 1.2–2.5 | 1.8 | Halo size in mask R |
| `months_abandoned` | months | 0–36 | 6 | Dust, rust streaks, fade, litter and flat tyres grow with it |
| `flat_tyres` | list | 0–4 wheels | A: 1 (burst side), more with `months_abandoned` | Which tyres are flat: the rim sits 0.04–0.08 m lower there, the body leans |
| `glass_side` | enum per window | intact, crazed_shards, gone | A: gone on the burst side | Crumbs placed inside and below each broken window (K07) |
| `windscreen` | enum | intact, crazed, crazed_holed, sagging, gone | A crazed_holed, B gone, F gone | The laminated pane state; crazed panes keep `glass` collision |
| `bonnet` | enum | closed, corner_folded, sprung, folded_back, standing, gone | A corner_folded, B standing | Bonnet geometry and its snag edges |
| `bonnet_flap` | m | 0.2–0.7 | 0.45 | Flap size for corner_folded |
| `doors` | enum per door | closed, ajar, sprung, open, hanging, gone | A closed, B sprung (burst side), F per `seed` | Door geometry; open or gone rear pairs trigger the cabin-gap check |
| `door_bulge` | m | 0–0.25 | B 0.08 | Outward bow of sprung doors |
| `roof_push` | m | 0–0.2 | B 0.08, F 0.04 | Roof pushed down or sagged, with a crease line |
| `panel_tears` | int | 0–10 | B 4 | Large tears at corners and around handles and lamps |
| `hanging_wiring` | int | 0–6 | B 2, F 1 | Looms and loose wires, `cable` capsules and `hook` snags |
| `bumper` | enum front and rear | on, hanging, gone | B rear hanging | Bumper pose; a hanging cover is a snag |
| `soot_film` | 0–1 | 0–1 | B 0.7 | Blast soot over the paint without burning (N) |
| `burn` | 0–1 | 0–1 | F 1.0 | Fraction of the vehicle burnt, spreading from `burn_origin` |
| `burn_origin` | enum | engine, cabin, fuel_tank, cargo | engine | Where the fire was hottest: burnt-off paint first there |
| `ride_drop` | m | 0–0.3 | derived per row (F) | Height lost on the rims (tyres gone, springs sagged) |
| `rims_melted` | bool | — | true for alloy wheels in F | Alloy rims collapse; the hub settles further |
| `weeks_since_fire` | weeks | 0–52 | 4 | Soot-black turning to rust-brown where the paint burnt off |
| `interior` | enum | intact, crumbs, burnt_frames | per state | Seats and trim, or frames and springs |
| `overgrowth` | 0–1 | 0–1 | A 0.3 off asphalt, else 0 | Weeds around and through the body (V17 instances, 0.3–1.5 m) |
| `debris_radius` | m | 1–20 | A 3, B 12, F 8 | Outer radius of the K07 scatter |
| `debris_count` | int | 0–300 | A 12, B 80, F 40 | Number of K07 pieces |
| `crumb_cover` | 0–1 | 0–1 | per state | Strength of the K07 crumb layer |
| `soot_patch` | m | 0–6 | F 4, else 0 | Diameter of the burnt ground patch |
| `ground` | enum | asphalt, soil, grass, snow | asphalt | Soot-patch look and the crumb visibility |
| `pose` | enum | upright, ditch_tilt, on_side | upright | Rare blast poses: a 5–15° roll into a ditch, or on its side (B, P2) |
| `plate` | enum | none, blank | none | Never characters |
| `seed` | int | — | — | Every random choice above |

### Placement rules (shared)

- **Where:** abandoned vehicles in yards (beside the house, nose toward the gate or fence), at road shoulders and at field edges along tracks, often overgrown; destroyed ones (B, F) on roads and tracks, at junctions and in open fields near craters (E05, 3–15 m away) or scorched ground (S07) (terrain.md R0 and §10).
- **How many** (assumed, a proposal): 4–8 vehicles per village and its fields; cars far outnumber trucks (about 8 : 1); state mix about half A, a quarter each B and F. The classic ВАЗ is the most common (terrain.md R1).
- **Orientation:** on a road, along the lane within ±10° (A) or ±30° (B, F, pushed by the burst); in a yard, parallel to the fence or the house wall within ±15°; in a field, along the nearest track within ±45°. Never closer than 3 m to another vehicle or a house wall (assumed), so the drone always has a way round.
- **Neighbours:** the state's debris field and soot patch; S11 asphalt with K07 crumbs on roads; S09 litter and V17 weeds in yards; E05 craters and S07 burnt patches near destroyed ones.

## R01 ВАЗ 2101–2107 classic sedan (roof rack option)

**Seen in:** M (the abandoned state, whole vehicle; a first-generation classic: twin round headlamps, a mesh grille, a chrome bumper with overriders, steel wheels with domed hubcaps, a steel-tube roof rack). The look-lock asset (asset-list: R01 in A and F).
**Research sources:** https://en.wikipedia.org/wiki/VAZ-2101 · https://ru.wikipedia.org/wiki/ВАЗ-2101 · https://auto.ru/catalog/cars/vaz/2101/specifications/razmer-koles/

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length | 4.07–4.17 m | 4.073 m | published (2101); the range to the later classics from knowledge.md §9.2 |
| Width | 1.61–1.62 m | 1.611 m | published |
| Height | 1.38–1.44 m | 1.44 m | published (two figures: 1382 and 1440 mm; the unladen 1.44 m is used, an abandoned car carries nothing) |
| Wheelbase | 2.424 m | 2.424 m | published |
| Track front / rear | 1.349 / 1.305 m | same | published |
| Ground clearance | 0.17 m | 0.17 m | published |
| Kerb mass | 0.945–1.05 t | 0.955 t | published (2101; 1.01 t for the 1.3 l version) |
| Tyre | 165/70–165/80 R13 | 165/80 R13 | published |
| Wheel diameter × width | 0.561–0.594 × 0.155–0.165 m | 0.594 × 0.165 m | derived from the tyre code |
| Front side window opening | 0.40–0.50 m tall × 0.70–0.80 m wide (with the vent) | 0.45 × 0.75 m | measured M (height, against the wheel); width assumed from the door |
| Rear side window opening | 0.40–0.45 × 0.50–0.55 m plus a fixed quarter | 0.43 × 0.52 m | measured M (height); assumed |
| Windscreen | 1.20–1.30 m wide, 0.50–0.55 m along a 35° rake | 1.25 × 0.52 m | assumed (rake from the side profile in M) |
| Roof panel (landable) | 1.20–1.30 × 1.10–1.20 m at 1.40–1.44 m | 1.25 × 1.15 m | assumed from the greenhouse in M |
| Bonnet | 1.00–1.10 × 1.35–1.45 m, slope 5–8° | 1.05 × 1.40 m | assumed |
| Roof rack | footprint 1.2–1.4 × 0.9–1.1 m, rails 0.08–0.12 m above the roof, tubes Ø 18–22 mm | 1.3 × 1.0 m, 0.10 m, Ø 20 mm | measured M (height above the roof); assumed (footprint, tube) |
| Panel gauge | 0.7–0.9 mm | 0.8 mm | published (shared section) |
| Fragment holes (A) | 0.01–0.25 m | median 0.03 m | measured M |
| Flat-tyre corner drop | 0.04–0.08 m | 0.05 m | measured M |
| F drop on the rims | 0.15–0.20 m | 0.17 m | derived (shared section) |

### Construction and materials
- Welded steel unibody, four doors with chrome window frames and front vent panes, a long flat bonnet, a separate boot, chrome bumpers with two overriders each, a mesh grille between twin round headlamps (2101) or four round lamps (2103, 2106) or rectangular lamps and black plastic bumpers (2105, 2107). Steel wheels with domed chrome hubcaps. Roof rack of steel tubes clamped to the rain gutters (M).
- **Materials:** paint `PaintedMetal006` / `green_metal_rust` tinted; chrome `Metal049A`; tyres `Rubber004`; interior `Fabric030` and `Leather014`; damage mask (shared).

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint (M's dark green): shade / lit / sky-lit roof | #3d4b53 / #6d7c81 / #8696a2 | — | measured M |
| Rust halo, primer chips | #887465, #bdb092 | — | measured M |
| Chrome bumper, hubcap, grille | #949ea5, #9ea0a2, #898f96 | — | measured M |
| Tyre | #34383c | — | measured M |
| Crazed windscreen | #b9cbd3 | — | measured M |
| Other paints | white, beige, cherry red, sky blue, olive | — | assumed palette (shared) |

### Variants and states
- Variants: 1 = the M car (2101 front, dark green, roof rack on); 2 = 2106-style four round lamps, beige, no rack; 3 = 2107-style rectangular lamps and black bumpers, white; the estate (2102/2104) is a later option with the same footprint and a longer roof (terrain.md R1).
- **A (M):** as the shared A kit with M's numbers: the bonnet's rear corner folded up against the windscreen, the windscreen crazed and holed, side glass gone, about 6 large and 10 small holes on the burst side, rust halos, one flat front tyre, the lamp-and-trim scatter within 1–4 m.
- **B:** doors sprung, the bonnet standing, the boot lid torn open, the roof pushed down 0.05–0.15 m, the chrome bumper torn off one mount and hanging, the rack bent; all glass gone; debris 2–8 m.
- **F:** the shared F kit: on its steel rims 0.17 m lower, hubcaps gone, chrome dulled and soot-black, the rack sagging, seat frames and springs, the roof sagging; the soot patch 2–6 m.

### Damage pattern
Glass and the struck corner first (the bonnet corner and the lamp), then the panels facing the burst; the chrome bumpers survive the blast but drop on one side; rust appears at every hole within weeks. Burnt, the classic keeps its shape best of all the cars (thicker shell, chrome steel bumpers, no plastic cladding), so its F silhouette is the base minus glass and rubber, sitting low.

### Physics role
- **Collision:** the shared car plan, 24–30 primitives; the rack as 4 capsules on 4 short posts; the folded bonnet flap a thin box at its angle; the chrome bumpers boxes (`steel`).
- **Material ids:** body `sheet_metal`; bumpers, rims, rack, seat frames `steel`; windscreen `glass` while crazed in place; tyres `proposed:rubber`; seats `proposed:fabric`.
- **Gaps:** none (side windows 0.43–0.45 m tall; shared gap table). The rear-row cabin gap applies only if the generator removes or opens both rear doors.
- **Wind volumes:** shared; height 1.44 m: recirculation 3–4 m, shadow about 15 m.
- **Snag hazards:** the bonnet flap's torn edges, hole petals, the rack tubes and their ends, the bumper overriders, a whip antenna (optional), wipers; B: sprung door edges, hanging wiring; F: seat springs, rim flanges, bead-wire rings.
- **Landable:** roof (1.25 × 1.15 m; with the rack, the rack's rails are the touch points and the legs straddle or catch them), bonnet (1.05 × 1.40 m, 5–8°), boot lid (about 0.9 × 1.3 m).

### Placement rules
The most common car: yards and shoulders in A, roads and tracks in B and F. A rack-carrying variant suits a yard.

### Generator parameters
Shared table, defaults: `model` R01, `body` sedan, `wheel` steel_hubcap, `paint` dark green (variant 1), `state` A.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `front` | enum | 2101, 2103_2106, 2105_2107 | 2101 | Lamp and grille set, chrome or plastic bumpers |
| `roof_rack` | bool | — | true (variant 1) | Tube rack on the gutters: snag and landing touch points |
| `vent_panes` | bool | — | true | Front quarter vents in chrome frames |

### Open points
- The height figure (1382 or 1440 mm): 1.44 m is used unladen; settled at the M side-by-side by the roof line against the wheel.
- The 2103–2107 sizes come from knowledge.md §9.2, not re-checked per model; the 2101 alone is the look-lock body.

## R02 ВАЗ 2109 hatchback

**Seen in:** R (research only; terrain.md R2).
**Research sources:** https://ru.wikipedia.org/wiki/ВАЗ-2109 · https://vazbook.ru/en/09/2108/body/exterior/karkas-i-operenie-kuzova · https://auto.ru/catalog/cars/vaz/2109/specifications/razmer-koles/

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length × width × height | 4.006 × 1.650 × 1.402 m (21099 sedan 4.2 m) | 4.006 × 1.650 × 1.402 m | published |
| Wheelbase | 2.460 m | 2.460 m | published |
| Track front / rear | 1.400 / 1.370 m | same | published |
| Ground clearance | 0.165 m | 0.165 m | published |
| Kerb mass | about 0.945 t | 0.945 t | published |
| Tyre | 175/70 R13 (165/70 R13 on some versions) | 175/70 R13 | published |
| Wheel diameter × width | 0.561–0.575 × 0.165–0.175 m | 0.575 × 0.175 m | derived from the tyre code |
| Side window opening | 0.40–0.45 m tall | 0.42 m | assumed (a lower greenhouse than R01) |
| Roof panel (landable) | 1.15–1.25 × 1.10–1.20 m at 1.40 m | 1.20 × 1.15 m | assumed |
| Bonnet | 0.9–1.0 × 1.35 m, slope 10–14° | 0.95 × 1.35 m | assumed (a sloping wedge nose) |
| Panel gauge | 0.7 mm skins, 0.8 mm main | as published | published |
| F drop | 0.15–0.20 m | 0.17 m | derived (175/70 R13: loaded radius 0.273 − rim flange 0.183 + sag 0.075) |

### Construction and materials
- Welded steel shell, bolted front wings, a sloping bonnet over a wedge nose with rectangular lamps, black plastic bumpers over steel beams, a five-door hatch with a large tailgate glass, steel wheels with plastic caps. Materials as shared.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint | white #e6e4dc, cherry red #8a2a24, silver #a3a6a8, dark blue #2d3d5a, dark green #2f4a40 | — | assumed palette |
| Black plastic bumpers | about #2a2b2c | — | assumed |

### Variants and states
- Variants: 1 = five-door, white; 2 = three-door (2108, the same front and length, longer doors), red; 3 = five-door, silver.
- **A / B / F:** the shared kits. The plastic bumpers crack in A and B and are gone in F, leaving their steel beams; the tailgate glass is tempered and empties into the boot.

### Damage pattern
As shared. The large tailgate glass and the wedge nose's plastic go first; the bonnet is short and folds up rather than back.

### Physics role
- **Collision:** the shared car plan with a single sloping bonnet box and the tailgate as a raked box.
- **Material ids:** shared; bumpers `proposed:plastic` (B, A), their beams `steel` (F).
- **Gaps:** none (0.42 m windows); the rear-row cabin gap only if generated (five-door).
- **Wind volumes:** shared; height 1.40 m.
- **Snag hazards:** shared; cracked bumper edges (plastic, blunt).
- **Landable:** roof 1.20 × 1.15 m; bonnet too steep at 10–14° to count (assumed limit 12°).

### Placement rules
Common in villages: yards and roads, any state.

### Generator parameters
Shared table, defaults: `model` R02, `body` hatch5, `wheel` steel, `paint` white.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `doors_count` | enum | 3, 5 | 5 | Door layout (2108 or 2109) |

### Open points
- The landable bonnet slope limit (12°) is assumed; the physics engineer sets it with the leg contact model.

## R03 ВАЗ 2121 Niva

**Seen in:** R (research only; terrain.md R2).
**Research sources:** https://ru.wikipedia.org/wiki/ВАЗ-2121 · https://auto.ru/catalog/cars/vaz/2121/specifications/razmer-koles/

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length × width × height | 3.740 × 1.680 × 1.640 m | same | published |
| Wheelbase | 2.200 m | 2.200 m | published |
| Track front / rear | 1.430 / 1.400 m (1.440 / 1.420 later) | 1.430 / 1.400 m | published |
| Ground clearance | 0.220 m | 0.220 m | published |
| Kerb mass | 1.15–1.21 t | 1.15 t | published |
| Tyre | 175/80 R16 or 185/75 R16 | 175/80 R16 | published |
| Wheel diameter × width | 0.686 × 0.175 m | same | derived |
| Side window opening | 0.45–0.50 m tall | 0.48 m | assumed (a tall, upright greenhouse) |
| Roof panel (landable) | 1.50–1.70 × 1.20–1.30 m at 1.64 m | 1.60 × 1.25 m | assumed (a long flat roof over a three-door body) |
| Bonnet | 1.0 × 1.4 m, slope 4–6° | 1.0 × 1.4 m | assumed |
| F drop | 0.15–0.20 m | 0.16 m | derived (loaded radius 0.326 − rim flange 0.221 + sag) |

### Construction and materials
- A short, tall, boxy welded steel unibody with three doors and a top-hinged tailgate, round headlamps, a flat bonnet, steel bumpers (early) or plastic (later), steel wheels; the spare wheel sits in the engine bay (assumed typical). Optional roof rack as R01. Materials as shared.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint | white #e6e4dc, olive #5b5e3e, beige #c9b994, dark blue #2d3d5a | — | assumed palette |

### Variants and states
- Variants: 1 = white, no rack; 2 = olive with a rack; 3 = beige, plastic bumpers.
- **A / B / F:** the shared kits. Its height and clearance make it the tallest car silhouette; in F it sits 0.16 m lower on 16-inch rims.

### Damage pattern
As shared. The tall flat sides take more holes; the short bonnet folds up whole.

### Physics role
- **Collision:** the shared car plan; the greenhouse box is tall (1.0–1.64 m).
- **Material ids:** shared.
- **Gaps:** none (0.48 m windows, three doors, no rear row of doors).
- **Wind volumes:** shared; height 1.64 m: recirculation 3.5–5 m, shadow about 16 m.
- **Snag hazards:** shared; the rack if present.
- **Landable:** roof 1.60 × 1.25 m, the largest flat car roof; bonnet 1.0 × 1.4 m.

### Placement rules
Suits tracks, field edges and yards (terrain.md R2); A or F at a field edge.

### Generator parameters
Shared table, defaults: `model` R03, `body` suv3, `wheel` steel, `paint` white.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `roof_rack` | bool | — | false | As R01 |

### Open points
- None beyond the shared ones.

## R04 Daewoo Lanos sedan

**Seen in:** N (class: a small five-door hatchback on alloy wheels, blast-wrecked; its make can't be read, terrain.md R3). The Lanos hatchback body (4.074 m) is the N class; the sedan is the default body.
**Research sources:** https://ru.wikipedia.org/wiki/Daewoo_Lanos · https://en.wikipedia.org/wiki/Daewoo_Lanos

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length | sedan 4.237 m, hatchback 4.074 m | 4.237 m | published |
| Width × height | 1.678 × 1.432 m | same | published |
| Wheelbase | 2.520 m | 2.520 m | published |
| Track front / rear | 1.405 / 1.422 m | same | published |
| Ground clearance | 0.155–0.165 m | 0.165 m | published (CIS) |
| Kerb mass | 0.99–1.18 t | 1.05 t | published |
| Tyre | 175/70 R13 or 185/60 R14 | 185/60 R14 | assumed typical fitment |
| Wheel diameter × width | 0.575–0.578 × 0.175–0.185 m | 0.578 × 0.185 m | derived |
| Side window opening | 0.40–0.45 m tall | 0.43 m | assumed |
| Roof panel (landable) | 1.20–1.30 × 1.10–1.20 m at 1.43 m | 1.25 × 1.15 m | assumed |
| B door bulge, roof push | 0.05–0.10 m, 0.05–0.15 m | 0.08 m, 0.08 m | measured N (class) |
| F drop | 0.20–0.25 m (alloys) or 0.15–0.20 m (steel) | 0.22 m | derived |

### Construction and materials
- Welded steel unibody, four-door sedan or three- and five-door hatchback, rounded body, body-colour plastic bumper covers over steel beams, plastic lamp units, steel wheels with plastic covers or 14-inch alloys. Materials as shared; plastic `Plastic006` tinted to the paint.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Blast-filmed panels (B) | #3a3d3d–#3b3f41 | — | measured N |
| Tyre, alloy | #1c1f1c, #1e201b | — | measured N |
| Paint (A) | silver #a3a6a8, graphite #4a4d50, dark blue #2d3d5a, cherry red #8a2a24, white #e6e4dc | — | assumed palette |

### Variants and states
- Variants: 1 = sedan, silver; 2 = five-door hatchback (the N class), graphite, alloys; 3 = sedan, dark blue.
- **B (N):** the shared B kit at N's numbers: doors bulged and sprung on the burst side, the bonnet skin gone and its inner frame standing, the roof pushed down, all glass gone, a tail lamp gone, the rear bumper cover hanging, wiring across the door opening, peppered panels, soot film, tyres on; its own parts within 2–8 m.
- **A / F:** the shared kits; the plastic covers and lamp units melt first in F, the alloys collapse.

### Damage pattern
As N: the bonnet skin peels away from its inner frame; the plastic bumper covers crack and hang rather than bend; lamp units shatter into lens shards (K07).

### Physics role
- **Collision:** the shared car plan; the standing bonnet frame as two thin boxes (frame and fold); a hanging bumper cover as a thin box at its angle.
- **Material ids:** shared; covers and lamp units `proposed:plastic`.
- **Gaps:** none by default; with both rear doors off (hatchback or sedan), the rear-row cabin gap is recorded (shared table).
- **Wind volumes:** shared; height 1.43 m.
- **Snag hazards:** shared; the bonnet frame's lightening-hole edges (`edge`), the wiring loop (`hook`), the hanging mirror, the hanging cover's torn edge.
- **Landable:** roof 1.25 × 1.15 m (pushed down in B: uneven); not the standing bonnet.

### Placement rules
The common modern car: roads and junctions in B, yards in A.

### Generator parameters
Shared table, defaults: `model` R04, `body` sedan, `wheel` steel (variant 2: alloy), `paint` silver.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `body` | enum | sedan, hatch3, hatch5 | sedan | Rear body and length (4.237 or 4.074 m) |
| `bonnet_skin_off` | bool | — | true in B | The outer skin gone, the inner frame with its holes exposed (N) |

### Open points
- N's make can't be read; R04 and R05 share the N class. Settled at the N side-by-side by silhouette, not by badge.

## R05 Chevrolet Aveo

**Seen in:** R (research only); N for the class (a small five-door hatchback).
**Research sources:** https://ru.wikipedia.org/wiki/Chevrolet_Aveo · https://en.wikipedia.org/wiki/Chevrolet_Aveo_(T200)

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length | hatchback 3.880–3.920 m, sedan 4.235–4.310 m | 3.920 m (hatchback) | published |
| Width | 1.670–1.710 m | 1.680 m | published |
| Height | 1.485–1.505 m | 1.505 m | published |
| Wheelbase | 2.480–2.489 m | 2.480 m | published |
| Track front / rear | 1.450 / 1.410–1.430 m | 1.450 / 1.410 m | published |
| Ground clearance | 0.150–0.155 m | 0.150 m | published |
| Kerb mass | 0.97–1.15 t | 1.07 t | published |
| Tyre | 185/60 R14 or 175/70 R14 | 185/60 R14 | assumed typical fitment |
| Wheel diameter × width | 0.578–0.601 × 0.175–0.185 m | 0.578 × 0.185 m | derived |
| Side window opening | 0.42–0.48 m tall | 0.45 m | assumed (a tall greenhouse) |
| Roof panel (landable) | 1.30–1.40 × 1.10–1.20 m at 1.50 m | 1.35 × 1.15 m | assumed |
| F drop | 0.20–0.25 m | 0.22 m | derived |

### Construction and materials
- Welded steel unibody, five-door hatchback (default) or four-door sedan, a tall cabin, large plastic bumper covers and lamp units. Materials as shared.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint | silver #a3a6a8, black #1d1e20, red #8a2a24, blue #2d3d5a | — | assumed palette |
| B state | as R04 (N class) | — | measured N |

### Variants and states
- Variants: 1 = hatchback, silver; 2 = sedan, black; 3 = hatchback, red.
- **A / B / F:** the shared kits; B defaults to N's look as R04.

### Damage pattern
As R04.

### Physics role
- As R04, height 1.505 m (recirculation 3–4.5 m, shadow about 15 m). **Gaps:** none by default (side windows about 0.45 m tall, below the 0.55 m drone height; shared table); the rear-row cabin gap only if generated. **Landable:** roof 1.35 × 1.15 m at 1.50 m.

### Placement rules
As R04.

### Generator parameters
Shared table, defaults: `model` R05, `body` hatch5, `wheel` steel, `paint` silver.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `body` | enum | hatch5, sedan | hatch5 | Rear body and length (3.92 or 4.31 m) |

### Open points
- None beyond the shared ones.

## R06 Chery Amulet

**Seen in:** R (research only; terrain.md R3).
**Research sources:** https://ru.wikipedia.org/wiki/Chery_Amulet · https://en.wikipedia.org/wiki/Chery_A15

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length × width × height | 4.393 × 1.682 × 1.424 m | same | published (the asset list's 4.27 × 1.69 × 1.45 m is superseded; see Open points) |
| Wheelbase | 2.468 m | 2.468 m | published |
| Track front / rear | 1.426 / 1.419 m | same | published |
| Ground clearance | 0.121 m (CIS adaptations higher, about 0.14) | 0.13 m | published (0.121); the default assumed between |
| Kerb mass | 1.10–1.14 t | 1.12 t | published |
| Tyre | 175/70 R13 or 185/60 R14 | 175/70 R13 | assumed typical fitment |
| Wheel diameter × width | 0.575 × 0.175 m | same | derived |
| Side window opening | 0.40–0.45 m tall | 0.42 m | assumed |
| Roof panel (landable) | 1.25–1.35 × 1.10–1.20 m at 1.42 m | 1.30 × 1.15 m | assumed |
| F drop | 0.15–0.20 m | 0.16 m | derived (steel wheels) |

### Construction and materials
- Welded steel unibody, a five-door liftback that reads as a sedan (the tailgate hinges at the roof and carries the rear glass), body-colour plastic bumpers, steel wheels. Materials as shared.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint | silver #a3a6a8, graphite #4a4d50, dark green #2f4a40, white #e6e4dc | — | assumed palette |

### Variants and states
- Variants: 1 = silver; 2 = graphite; 3 = white.
- **A / B / F:** the shared kits; in B the liftback's large tailgate is torn open at the roof hinges like a boot lid.

### Damage pattern
As shared.

### Physics role
- As R04, height 1.424 m. **Gaps:** none by default (side windows about 0.42 m tall; shared table); the rear-row cabin gap only if generated. **Landable:** roof 1.30 × 1.15 m at 1.42 m; the low clearance (0.12–0.14 m) leaves no space under it.

### Placement rules
As R04, less common.

### Generator parameters
Shared table, defaults: `model` R06, `body` liftback5, `wheel` steel, `paint` silver.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `tailgate` | enum | closed, torn_open, gone | closed (A), torn_open (B) | The liftback tailgate with its glass |

### Open points
- The published length (4.393 m) differs from the asset list (4.27 m); the asset list should be corrected (orchestrator).

## R07 VW Passat B3/B4 (or Golf II)

**Seen in:** R (research only; terrain.md R3).
**Research sources:** https://de.wikipedia.org/wiki/VW_Passat_B3 · https://en.wikipedia.org/wiki/Volkswagen_Passat_(B3) · https://en.wikipedia.org/wiki/Volkswagen_Passat_(B4) · https://en.wikipedia.org/wiki/Volkswagen_Golf_Mk2

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length | B3 4.575–4.58 m (one text: 4.67), B4 sedan 4.610, estate 4.597 m | 4.58 m (B3 sedan) | published |
| Width | 1.705–1.720 m | 1.71 m | published |
| Height | sedan 1.430–1.433 m, estate 1.460–1.491 m | 1.43 m | published |
| Wheelbase | 2.624–2.630 m | 2.625 m | published |
| Track front / rear | 1.485 / 1.425 m | same | published (B3) |
| Ground clearance | 0.12–0.13 m | 0.125 m | assumed |
| Kerb mass | 1.13–1.38 t | 1.25 t | published |
| Tyre | 185/65 R14 or 195/60 R14 | 185/65 R14 | assumed typical fitment |
| Wheel diameter × width | 0.596 × 0.185 m | same | derived |
| Golf II (variant) | 3.985–4.054 × 1.665–1.700 × 1.415 m, wheelbase 2.470 m, 175/70 R13 | 3.985 × 1.665 × 1.415 m | published |
| Side window opening | 0.40–0.45 m tall | 0.43 m | assumed |
| Roof panel (landable) | 1.35–1.45 × 1.15–1.25 m at 1.43 m (estate 2.0 m long) | 1.40 × 1.20 m | assumed |
| F drop | 0.15–0.25 m | 0.18 m | derived |

### Construction and materials
- Welded, partly galvanised steel unibody (assumed for the class), a long sedan or estate with a grille-less nose (B3) or a grille (B4), plastic bumpers; Golf II a short two-box hatchback with round headlamps. Materials as shared.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint | dark blue #2d3d5a, silver #a3a6a8, black #1d1e20, dark green #2f4a40, white (Golf) #e6e4dc | — | assumed palette |

### Variants and states
- Variants: 1 = Passat B3 sedan, dark blue; 2 = Passat estate, silver (the longest car, a 2.0 m roof); 3 = Golf II five-door, white.
- **A / B / F:** the shared kits.

### Damage pattern
As shared; the estate's long side glass makes a larger crumb field.

### Physics role
- As the shared car plan; height 1.43–1.49 m. The estate's roof is the largest car landing area (about 2.0 × 1.2 m). **Gaps:** none by default (side windows about 0.43 m tall; shared table); the rear-row cabin gap only if generated (the Passat's longer rear doors give 0.60–0.75 m fore-aft, assumed; the three-door Golf has no rear row).

### Placement rules
As R04; the estate suits yards (load carrier).

### Generator parameters
Shared table, defaults: `model` R07, `body` passat_sedan, `wheel` steel, `paint` dark blue.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `body` | enum | passat_sedan, passat_estate, golf2_hatch3, golf2_hatch5 | passat_sedan | Which body the preset builds (dimensions switch with it) |
| `generation` | enum | B3, B4 | B3 | Nose (grille-less or grilled) and lamps |

### Open points
- One generator preset carries two models (Passat and Golf II) as the asset list says; if the buildability review prefers, the Golf II becomes its own preset under R07.

## R08 ЗИЛ-130 flatbed truck

**Seen in:** R (research only; terrain.md R4).
**Research sources:** https://ru.wikipedia.org/wiki/ЗИЛ-130 · https://en.wikipedia.org/wiki/ZIL-130 · https://www.zr.ru/archive/zr/1963/11/izuchaiem-novyie-modieli · https://buhgalter911.com/spravochniki/transport/normy-resursa-pnevmaticheskih-shin/vantazhni-avtomobili/5-1-vantaghni-950813.html

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length × width × height | 6.675 × 2.500 × 2.400 m | same | published |
| Wheelbase | 3.800 m | 3.800 m | published |
| Track front / rear | 1.800 / 1.790 m | same | published |
| Ground clearance | 0.275 m | 0.275 m | published |
| Kerb mass | 4.3–4.9 t | 4.3 t | published (two figures) |
| Tyre | 9.00R20 (260-508), dual at the rear | 9.00R20 | published (size); dual rear assumed typical |
| Wheel diameter × width | 0.97–1.02 × 0.26 m | 1.0 × 0.26 m | derived from the tyre code |
| Platform, inside | 3.752 × 2.326 m | same | published |
| Platform sides | 0.575 m (0.685 m before 1970) | 0.575 m | published |
| Platform floor height | 1.30–1.45 m | 1.40 m | derived (loaded wheel radius 0.47 + axle and spring stack 0.35 + frame 0.25 + cross members and boards 0.3) |
| Cab | width 2.0–2.2 m, roof 1.4–1.6 × 1.3–1.5 m at 2.3–2.4 m | 2.1 m wide, roof 1.5 × 1.4 m at 2.40 m | assumed (overall height published 2.40 m; cab height 2.31 m in one table) |
| Bonnet top | 1.0–1.2 m wide, 1.2–1.4 m long, at 1.7–1.9 m | 1.1 × 1.3 m at 1.8 m | assumed |
| Cab door opening | 0.8–0.9 × 1.1–1.2 m | 0.85 × 1.15 m | assumed |
| F drop | 0.19 m | 0.19 m | derived (shared section) |

### Construction and materials
- A ladder frame, a bonneted all-steel three-seat cab with a wraparound windscreen, a wide grille between the headlamps, separate wings; a timber-plank platform with three drop sides on steel cross members (assumed typical); leaf springs; dual rear wheels. Cab panels 0.8–1.0 mm (assumed). Materials: cab `PaintedMetal006` tinted blue or green; boards `raw_plank_wall` (grey-weathered); frame and cross members `rusty_metal_02`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Cab paint | faded blue #4e6a86, green #4b5a3e, olive #5b5e3e | — | assumed palette (terrain.md R4: faded blue or green) |
| Platform boards, weathered | about #8a8478 (grey) | — | assumed (as the plank fences, houses-and-yard Y01) |
| Burnt cab | as R09 (O): #303033, deep #1d1e21 | — | measured O (class) |

### Variants and states
- Variants: 1 = blue cab, sides closed; 2 = green cab, the left side dropped; 3 = olive, one side board missing.
- **A:** glass partly crazed or gone, flat tyres, boards grey and split, rust at the cab's sills and wing edges, the bed holding litter or a few planks.
- **B:** cab glass gone, the doors sprung, the bonnet lifted, side boards blown off or split and hanging from their hinges, fragment holes in the cab, debris 2–12 m.
- **F:** tyres gone (the duals' rims on the ground, 0.19 m lower), cab soot-black and burnt to bare steel on the roof, the bench burnt to its frame, the timber platform burnt to char stubs on the steel cross members.

### Damage pattern
The timber bed fails first (it splinters in B and burns in F); the steel cab dents but keeps its shape; the bonnet lifts at its rear edge.

### Physics role
- **Collision:** frame 2 boxes; cab 4 boxes (lower, upper, roof, rear wall); bonnet and wings 3 boxes; 6 wheel cylinders (the duals as one wide cylinder per side); platform floor 1 box, sides 3 boxes (each drop side its own box at its angle); in F the floor becomes 6–9 cross-member boxes and char stubs.
- **Material ids:** cab, bonnet `sheet_metal`; frame, cross members, rims `steel`; boards `timber`; tyres `proposed:rubber`.
- **Gaps:** none (shared table: the cab is a pocket; under the truck no clear crossing; the F floor's 0.40–0.50 m gaps are below 0.574 m).
- **Wind volumes:** cab 0 (glass in) / 0.3 (glass gone); bonnet 0.1; chassis 0.6; side boards 0; F side boards gone, floor 0.8 (cross members only). Height 2.4 m: recirculation 5–7 m, shadow about 24 m.
- **Snag hazards:** split board ends and hinge pins, sprung cab door edges, the windscreen frame, B and F: hanging wiring; F: char stubs and nails on the cross members, rim flanges, bead wires.
- **Landable:** the platform floor 3.75 × 2.33 m at 1.40 m (timber, in A and B), the cab roof 1.5 × 1.4 m at 2.40 m, the bonnet 1.1 × 1.3 m; not the F floor.

### Placement rules
Rare (about one per village or farm): on roads and tracks, in a farm or large yard (P02 complex), at a belt edge on a track.

### Generator parameters
Shared table, defaults: `model` R08, `body` flatbed, `wheel` truck_steel, `paint` faded blue.

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `side_boards` | enum per side | closed, dropped, hanging, gone | closed | Drop sides; a dropped or hanging side changes the bed's collision and snags |
| `bed_load` | enum | empty, planks, litter | empty | What lies on the bed (K03 planks) |
| `bed_burn` | 0–1 | 0–1 | from `burn` | Board loss to char stubs (F) |

### Open points
- The timber platform and the dual rear wheels are assumed typical; settled by the asset-artist from public photos of the class, not by the pilot.
- The cab's exact width and the panel gauge are assumed.

## R09 Урал-4320 with canvas tilt (burnt: bare hoops)

**Seen in:** O (the burnt-out state of the heavy bonneted cab class: a vertical-slat grille, round headlamps on the wings, a long bonnet, the cab door hanging open, the windscreen gone, the cab soot-black; what is behind the cab is not read). The tilt and bows are research.
**Research sources:** https://ru.wikipedia.org/wiki/Урал-4320 · https://en.wikipedia.org/wiki/Ural-4320 · https://truck-encyclopedia.com/coldwar/ussr/ural-4320-6x6-truck.php · https://ru.wikipedia.org/wiki/Урал-375 · https://legion.wplus.net/guide/army/tr/ural375.shtml · https://hi-news.pp.ua/tehnka-tehnologyi/print:page,1,7088-vantazhniy-avtomobl-pdvischenoyi-prohdnost-ural-43206-tehnchn-harakteristiki.html

### Dimensions
| Part | Range | Chosen default | Label |
|---|---|---|---|
| Length × width | 7.35–7.37 × 2.50–2.69 m | 7.366 × 2.50 m | published |
| Height at the cab / with the tilt | 2.64–2.715 m / 2.98–3.005 m | 2.715 / 3.005 m | published |
| Wheelbase | 3.525 + 1.400 m (front axle to the first rear axle, then the bogie) | same | published |
| Track | 2.000 m | 2.000 m | published |
| Ground clearance | 0.400 m | 0.400 m | published |
| Kerb mass | 8.05–8.4 t | 8.05 t | published |
| Tyre | 14.00-20 (370-508), single all round | same | published |
| Wheel diameter × width | 1.12–1.16 × 0.37 m | 1.14 × 0.37 m | derived from the tyre code |
| Platform, inside | 3.89–4.42 × 2.32–2.33 m | 3.90 × 2.33 m | published (secondary, two figures); the default from the layout (7.37 m minus about 3.15 m of bonnet and cab and 0.45 m of spare wheel behind the cab, derived) |
| Platform sides | 0.62–0.88 m | 0.62 m | published (secondary, 375D) |
| Platform floor height | 1.40–1.55 m | 1.45 m | derived (loaded wheel radius 0.53 + spring stack 0.35 + frame 0.30 + platform sills 0.25) |
| Bows | 4–6 inverted-U steel bows, tube 0.03–0.05 m, top 1.50–1.60 m above the floor | 5 bows at 0.975 m, Ø 0.04 m, top 1.55 m above the floor | derived (tilt height 3.005 − floor 1.45); count and tube assumed |
| Cab | 2.2–2.4 m wide, roof 1.8–2.0 × 1.3–1.5 m at 2.715 m | 2.3 m, roof 1.9 × 1.4 m | assumed (height published) |
| Bonnet top | 1.1–1.3 × 1.3–1.6 m at 2.0–2.2 m | 1.2 × 1.5 m at 2.1 m | assumed |
| Cab door opening | 0.8–0.9 × 1.1–1.2 m | 0.85 × 1.15 m | assumed |
| F drop | 0.25 m | 0.25 m | derived (shared section) |

### Construction and materials
- A ladder frame on three driven axles with single wheels; a bonneted steel cab (three seats) with a two-pane flat windscreen, a long steel bonnet with a vertical-slat grille, round headlamps on the wings (O); the spare wheel behind the cab (assumed); a steel platform with a drop tailgate and folding side benches, removable steel bows and a canvas tilt (published). Materials: cab and body `PaintedMetal006` tinted olive; tilt `rough_linen` tinted olive; benches timber (assumed); frame `rusty_metal_02`.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Paint (A) | olive drab #5b5e3e | — | assumed palette (terrain.md R5: typically olive) |
| Tilt canvas | about #6b6a4a, faded #858260 | — | assumed |
| Burnt cab front (F) | #303033 (deep #1d1e21) | — | measured O; terrain.md |
| Burnt cab door, lit | #585c67 | — | measured O |
| Grille (burnt) | #39393c | — | measured O |
| Burnt wheel | #18181c | — | measured O |
| Ground: snow / slush | #cdd5e2 / #525255 | — | measured O |

### Variants and states
- Variants: 1 = tilt on, olive; 2 = tilt off, bows on (the bare frame even before any damage); 3 = no bows, an open platform.
- **A:** glass crazed or gone, tyres flat, the canvas faded, sagging between the bows and torn in 0–2 places, rust at the cab sills; litter in the bed.
- **B:** cab glass gone, cab doors sprung, holes in the cab and bonnet, the canvas shredded over 30–70 % with flaps hanging, 1–2 bows bent, the tailgate blown down; debris 2–15 m.
- **F (O):** the cab soot-black, the windscreen gone, a cab door hanging open (O), the tyres burnt away so the truck sits 0.25 m lower on its rims, the canvas burnt away leaving the **bare bows**, the benches and the wooden tilt slats burnt, the steel platform's sides warped 0.02–0.08 m (assumed), the soot patch 2–6 m, fragments to 10–20 m (O, on snow).

### Damage pattern
The canvas goes first (torn in B, burnt in F), then the glass and the cab's sheet panels; the frame, bows and platform survive as a steel skeleton, which is the burnt truck's silhouette.

### Physics role
- **Collision:** frame 2 boxes; cab 4–5 boxes; bonnet and wings 3 boxes; 6 wheel cylinders; platform floor, sides and tailgate 5 boxes; the tilt (A, B) as 3 boxes (two sides, a top) following the canvas within 2 cm; bows (F, variant 2) as capsule arcs, 5–7 segments each; torn canvas flaps as thin boxes.
- **Material ids:** cab, bonnet `sheet_metal`; frame, platform, bows, rims `steel`; tilt `proposed:fabric`; benches `timber`; tyres `proposed:rubber`.
- **Gaps (F and variant 2, bows bare):**
  - side bays between bows above the side boards: 0.975 − 0.04 = **0.935 m wide** × (1.55 − 0.62 − about 0.10 m bow shoulder) = **0.83 m tall**; narrowest 0.83 m ≥ 0.706: **passes at any yaw**, 62 mm a side on the diagonal, 128 mm nose-first; 0.83 m above the 0.55 m drone height
  - rear opening above the closed tailgate: **2.33 × 0.83 m**, passes at any yaw; with the tailgate down 2.33 × 1.45 m
  - top bays between bows: **0.935 × 2.33 m**, passes at any yaw (a descent into the bed)
  - each recorded as a gap polygon on its bay plane from the real bow geometry; bent bows (B) or warped sides (F) change the polygon and the generator records the real clear space
- **Gaps (A, B):** the tilt is closed; a tear is a gap only where its clear polygon passes (≥ 0.574 m wide and 0.55 m tall), recorded by the generator.
- **Wind volumes:** cab 0 / 0.3; bonnet 0.1; chassis 0.6; side boards 0; tilt 0 (A), 0.4 (B), **bare bows 0.93** (F). Height 2.7–3.0 m: recirculation 6–9 m, shadow about 30 m, the largest wake of any vehicle (terrain.md R5).
- **Snag hazards:** bare bows (`edge`, r 0.02 m) and their side pockets, torn canvas flaps (`hook`, soft), the tilt's ropes (B, `hook`), sprung cab doors, the open cab door (O), the grille slats, rim flanges and bead wires (F), the spare-wheel carrier.
- **Landable:** the cab roof 1.9 × 1.4 m at 2.7 m, the bonnet 1.2 × 1.5 m, the platform floor 3.90 × 2.33 m at 1.45 m (open or under bare bows; in A and B under the tilt, only through a tear); the tilt's top is fabric and not landable.

### Placement rules
Rare: on roads and tracks, at belt edges along tracks, in open fields near craters; F most often on a road (O).

### Generator parameters
Shared table, defaults: `model` R09, `body` tilt, `wheel` truck_steel, `paint` olive drab, `state` F (the O state).

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `tilt` | enum | on, torn, burnt_bows, bows_only, off | on (A), torn (B), burnt_bows (F) | The canvas and bows; drives the gap, wind and snag records |
| `tilt_tear` | 0–1 | 0–1 | B 0.5 | Fraction of canvas gone, in flaps |
| `bows` | int | 4–6 | 5 | Bow count over the platform length |
| `bow_bend` | 0–1 | 0–1 | B 0.2 | Bows bent out of line (gap polygons follow) |
| `tailgate` | enum | up, down, gone | up | Rear opening height |
| `cab_door_open` | enum per side | closed, ajar, open, hanging | F: near side open (O) | The O look |
| `platform_length` | m | 3.8–4.5 | 3.90 | Bed length (the two published figures) |

### Open points
- The platform length (3.89 or 4.42 m in two secondary sources), the side height and the bow count are not confirmed by a primary source; the default (3.90 m, 0.62 m, 5 bows) is settled by the asset-artist from public photos of the class.
- The gap rule here departs from the asset list's "no gaps" for vehicles: the bare-bow cage has openings of 0.83 m and more, so by the rule that every visible opening is a gap the sim knows, they are recorded. The orchestrator should amend asset-list §9 for R09.

## K07 Glass crumbs (decal plus micro) and vehicle fragments (doors, bumpers, wheels)

**Seen in:** M (crumbs on the seats and bright chips on the asphalt; lamp parts, a bezel ring and a trim plate within 1–4 m), N (glass gone, the car's own trim and covers among the strike debris; a hanging bumper cover), O (dark fragments on snow to 10–20 m).
**Research sources:** https://en.wikipedia.org/wiki/Tempered_glass · https://en.wikipedia.org/wiki/Laminated_glass · https://environdec.com/library/epd11684 · the body sources of the shared section.

### Dimensions
| Piece | Range | Chosen default | Label |
|---|---|---|---|
| Tempered glass crumb | 5–15 mm across, 3–5 mm thick, cubic to rounded | 10 × 10 × 4 mm | published (thumbnail-sized granules); thickness assumed |
| Crumbs per broken side window | 2000–6000 | 3600 | derived (0.36 m² × 4 mm / 0.4 cm³) |
| Crumb spread per window | 60–80 % inside and within 1 m below the opening, the rest to 3 m (A) or 8 m (B, burst-driven) | 70 % within 1 m | assumed |
| Laminated windscreen, fallen | 1.2–1.4 × 0.5–0.6 m, 4.96 mm, folded or draped | 1.30 × 0.55 m | published (build-up); size assumed per model |
| Laminated shard | 0.05–0.3 m, crazed, holding together | — | assumed |
| Headlamp bowl and bezel ring | Ø 0.16–0.20 m (round), 0.20 × 0.12 m (rectangular) | Ø 0.18 m | measured M (against the wheel) |
| Lamp lens shards (red, amber, clear plastic or glass) | 0.02–0.10 m | — | assumed |
| Trim plate or panel shard | 0.1–0.6 m, 0.7–0.9 mm, bent and torn | 0.35 × 0.2 m | measured M (the flat plate); assumed range |
| Chrome trim strip | 0.5–1.5 m × 15–25 mm | 1.0 m | assumed |
| Steel bumper (classics) | 1.55–1.60 × 0.10–0.14 m with overriders | 1.58 × 0.12 m | assumed (the R01 width) |
| Plastic bumper cover | 1.60–1.70 × 0.40–0.50 m, 3 mm shell | 1.65 × 0.45 m | assumed |
| Car door | 0.95–1.15 × 0.85–1.0 × 0.10–0.14 m | 1.05 × 0.95 × 0.12 m | assumed (from the car lengths) |
| Bonnet or boot lid | 0.9–1.4 × 1.0–1.45 m | 1.05 × 1.40 m | assumed |
| Car wheel, with tyre | Ø 0.56–0.69 × 0.155–0.185 m | Ø 0.59 × 0.165 m | derived (R01–R07 tyres) |
| Car wheel, burnt (rim and bead wires) | rim Ø 0.33–0.36 m; bead rings Ø 0.33–0.41 m, wire 5–8 mm | as the model | derived |
| Hubcap | Ø 0.30–0.33 m | Ø 0.32 m | assumed (M's domed caps) |
| Truck wheel, with tyre | Ø 1.0–1.14 × 0.26–0.37 m | as R08 / R09 | derived |
| Seat frame (burnt) | 0.5 × 0.5 × 0.9 m tubes with springs | — | assumed |
| Mirror, wiper arm, exhaust section | 0.15–1.5 m | — | assumed |

### Construction and materials
- **Crumb layer:** a feathered ground decal (PR-11) carrying the glitter density, plus instanced micro crumbs drawn within 4–6 m of the camera from the shared micro-detail generator (the same deterministic field, so the crumbs are where the decal says). Crumbs are pale green-grey glass with a sharp specular glint; on asphalt they read as bright chips (M).
- **Fragments:** each a small mesh from the vehicle generator's own parts (so a door in the field matches the car it came from), torn and bent by the same kit code; paint, chrome, plastic, rubber and soot from the shared sets.
- **Materials:** glass shader plus a crack mask; the shared paint, chrome, plastic, rubber and soot sets.

### Colour
| Surface | Photo albedo (sRGB) | Feed check value (sRGB) | Source |
|---|---|---|---|
| Crumbs and bright chips on asphalt | #e3e4e2 | — | measured M |
| Crumbs on a seat | #dce5e8 | — | measured M |
| Dark fragments and chips | #625e5a | — | measured M |
| Trim plate (bare) | #c9c8c5 | — | measured M |
| Lamp bowl | #9e9d9b | — | measured M |
| Crumbs, true albedo (edge-on glass) | about #a9bdb4 | — | assumed |
| Burnt fragments on snow | #18181c–#303033 | — | measured O (class) |

### Variants and states
- Per state the vehicle generator picks the set: A a few lamp parts, trim and crumbs; B covers, strips, shards, crumbs, sometimes a door or bonnet; F burnt shards, bead-wire rings, melt drips, char.
- A fragment carries its own state: painted (A), soot-filmed (B), burnt and rusting (F).

### Damage pattern
Not applicable (they are the damage); burnt fragments follow `weeks_since_fire` like their vehicle.

### Physics role
- **Collision:** each fragment is 1–3 primitives (a door a box; a bumper a box or capsule; a wheel a cylinder; a trim strip a capsule; a bead-wire ring 6–8 capsule segments, r 3–4 mm). Fragments are static obstacles; pieces under 0.10 m are visual only (below the leg foot's scale; assumed).
- **Material ids:** shards, doors, lids `sheet_metal`; bumpers (classic), rims, bead wires, seat frames `steel`; covers, lamps `proposed:plastic`; tyres `proposed:rubber`; the fallen windscreen `glass`.
- **The crumb layer** is not per-crumb physics (a 10 mm crumb is smaller than a leg foot): it is a ground cover, **`proposed:glass_crumbs`**, with μ 0.30/0.20 (assumed: loose glass cubes roll under a polymer foot; the same values as the earthworks sheet's S11 crumb cover), rigid on asphalt, so a landing on crumbs slides more readily than on bare asphalt (μ 0.65/0.55, S11).
- **Gaps:** none.
- **Wind volumes:** none (low pieces); a standing bonnet or door in the field is a solid thin box.
- **Snag hazards:** torn shard edges (`edge`, r 0.35 mm), bead-wire rings (`hook`/`edge`, r 3 mm), trim strips, hanging bumper covers, the fallen windscreen's torn PVB edge (`edge`, r 0.4 mm).
- **Landable:** a flat door or lid lying on the ground is landable but rocks (assumed); everything else is uneven ground.

### Placement rules
Placed only by the vehicle generator's debris field (shared section), never on its own: crumbs under and inside every broken window, the rest by state radius and burst bias. A wheel or door may lie up to the state's radius from its vehicle; a fragment never floats or intersects (it rests on the ground or on another piece).

### Generator parameters
Driven by the vehicle's `debris_radius`, `debris_count`, `crumb_cover`, `burst_dir` and `seed`; these are its own:

| Param | Type | Range | Default | Effect |
|---|---|---|---|---|
| `crumb_size` | m | 0.005–0.015 | 0.010 | Crumb edge length (instances) |
| `crumbs_per_window` | int | 2000–6000 | 3600 | Crumb count per broken tempered pane |
| `crumb_inside` | 0–1 | 0.4–0.9 | 0.7 | Fraction falling inside and within 1 m of the pane |
| `crumb_throw` | m | 1–8 | A 3, B 8 | How far the rest are thrown, along `burst_dir` |
| `fragment_set` | list | lamps, trim, shards, bumper, cover, door, lid, wheel, hubcap, seat, mirror, exhaust | per state | Which pieces the field may use |
| `big_pieces` | int | 0–3 | A 0, B 1, F 1 | Doors, lids, bumpers or wheels detached into the field |
| `falloff` | exponent | 1–3 | 2 | Density falloff with radius (integer powers only) |
| `edge_jitter` | m | 0–2 | 1.0 | Raggedness of the field's outer edge |

### Open points
- `glass_crumbs` as a ground cover (not a material on a collision shape) is the proposal; whether it lives in the surface table as a cover channel (as S11 does) or as a material row is the physics engineer's call.
- The 0.10 m visual-only threshold for small fragments is assumed; settled with the leg contact model.

## Open points for the whole sheet

**Proposed physics material ids** (none invented in a generator; the physics engineer adds them to `physics_materials.json` with values):
- `rubber`: tyres (R01–R09, K07). Compliant: a leg on a tyre sinks a few millimetres; μ high (about 0.8–1.0 for polymer on rubber, assumed); the edge radius is the tyre's own.
- `plastic`: bumper covers, lamp housings, dashboards, trim (R02–R07, K07). Rigid at drone loads, μ about 0.35/0.30 (assumed), cracked edges blunt (r 1–2 mm).
- `fabric`: seats, headliners and the canvas tilt (R01–R09). The same id the houses-and-yard sheet proposes for tarps: soft, wraps, no bounce; a torn canvas flap is a soft snag.
- `glass_crumbs`: the K07 crumb layer as a ground cover, μ 0.30/0.20 (assumed, matching the earthworks sheet's S11 cover).
- Glass breakability (a crazed windscreen giving way under a leg, a shard in a frame snapping) stays a later property, as review P3-6 says.

**For the orchestrator:**
- **ID collision (resolved by the orchestrator):** the road kit is now RD01–RD03 in the asset list; R01–R09 are the vehicles only.
- **R06 size:** the published Chery Amulet is 4.393 × 1.682 × 1.424 m, not the asset list's 4.27 × 1.69 × 1.45 m.
- **R09 gaps:** the burnt or bare-bow Урал has fly-through openings of 0.83 m and more (side bays, rear, top); asset-list §9's "no gaps" should read "no gaps for cars; the bare-bow cage of R09 has gaps".
- **Fragment holes in collision:** holes ≤ 0.25 m stay closed in the collision (shared Geometry rule); this is a deliberate deviation from "collision follows the visual" and needs the physics engineer's sign-off.

**Settled by side-by-side or by the asset-artist, not by the pilot:** R01's height figure, the N class silhouette (R04/R05), the ЗИЛ platform and dual wheels, the Урал platform length, side height and bow count. **No pilot question** comes out of this sheet: sizes are published or derived, and the looks are in the photos.
