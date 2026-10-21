"""house: a village house from the shared house table (object sheet docs/object-sheets/houses-and-yard.md §Shared
"Shared house generator parameters" and §H01; p1-h01-adobe-house, design decisions 1, 3-6, 8, 9 and 11). Plain
Python: lib.walls (the plan, the walls and their UVs), lib.joinery (doors and windows), lib.roof (the carpentry, the
tiles, the chimney and the ceiling), lib.masks (the damage-mask atlas).

Params (assets-src/houses/<variant>.json) are PARAMS below; `check` holds them to AST-04, to the drone and to what
is built. Built: an adobe house with a clay-tile roof, gabled or hipped at either end, its door on the front or back,
intact (I) or damaged (D, task 1.4): the roof stripped with its tiles left in clusters, battens broken with K03's
splinters, rafters off the plate (lib.roof), windows crumbled round their frames and breaches through the walls
(lib.walls), the ceiling down in the rooms, cables hanging in the hall, and the rubble of it all (lib.rubble).
Refused as "not built yet": the X state (`damage` above DAMAGE_X, `collapse`, `gable_breach`; after G1 by D-040),
fire, floor debris other than the ceiling's, a leaf blown `inside`, a crumbled edge round an opening with no frame,
the other wall materials, covers and end shapes, and a veranda. Every item takes its explicit value from the params
file (lib.params requires every key): `damage` documents the state, the items are set per state.

Frame (asset space, lib.frame): the house centred on the origin, its length along X, the front (door) wall facing
-Z, the walls on the ground at y = 0 (PLANTED: the ground covers what reaches below it). LODs (design decision 8,
from the feed's 935 px/rad): LOD0 everything; LOD1 from `lod.lod1_switch_m` (a 14 mm tile edge under half a pixel)
with each tile course one slab under its tiles' tops, the chimney as boxes, no nibs; LOD2 from `lod.lod2_switch_m`
(the battens and rafter tails under a pixel) the walls with their openings and reveals, the floor and ceiling, the
door's leaf, each slope's cover as one slab and the chimney as one box; LOD3 from `lod.lod3_switch_m` (a reveal under
a pixel) the outer faces with their openings over a dark liner, the covers' tops and the chimney; culled past
`lod.cull_m`, where the ridge drops under 2 px (935 * ridge / 2, the debris kit's rule).
"""
import math
import random

from lib import debris_tile, joinery, kits_vehicle, masks, meta, roof, rubble, walls
from lib.geometry import Mesh

PLANTED = True
SIDES = ("none", "front", "back", "left", "right")
WALL_MATERIALS = ("adobe", "wattle", "silicate", "red_brick")
END_TYPES = ("gable", "hip", "half_hip", "boarded_gable")
COVERS = ("clay_tile", "slate", "sheet_metal", "none")
WINDOWS = tuple(f"w{k}" for k in range(1, 9))
BREACHES = ("b1", "b2", "b3")
COLLAPSES = ("c1", "c2")
DRONE_M = (0.574, 0.706, 0.254, 0.4525, 0.55)  # nose-first, diagonal, and w(theta) = 0.254 + 0.4525 sin(theta + 45)
                                               # (knowledge.md §5.4, the sheet's "How to read"); the vertical envelope
AST04_DOOR_M = (0.80, (1.80, 1.90))  # D-032: the village houses' entrance door
AST04_WINDOW_M = (0.80, 1.00)        # D-032: the adobe houses' window
HEAD_LAP_M = (0.06, 0.10)            # ГОСТ 1808-71's 67 mm and the sheet's 70-90 mm
TAIL_MAX_M = 0.2                     # the eaves course past the rafter tails (assumed)
DAMAGE_I = 0.25                      # the sheet: I below 0.25
DAMAGE_X = 0.8                       # the sheet: X above 0.8
SLOTS = {"facade": "masonry", "interior": "masonry", "clay": "masonry", "tile": "masonry",
         "tile_fracture": "masonry", "tile_far": "masonry", "brick": "masonry", "timber": "timber", "door": "timber", "glass": "glass",
         "iron": "steel", "timber_fracture": "timber", "adobe": "masonry", "heap": "masonry", "cable": "cable"}
CABLE_R_M = 0.004                    # a two-core PVC cable, about 8 mm (assumed)
CABLE_SAG_M = (0.5, 0.9)             # its loop below the hall's joists (assumed: E 16, down to about head height)
CABLE_SEGMENTS = 16
CABLE_SIDES = 6
CABLE_SLOT = "cable"
CHANNELS = ["render_depth", "soot", "dirt", "base_band"]


def _p(type_, range_, default, effect):
    return {"type": type_, "range": range_, "default": default, "effect": effect}


PARAMS = {
    "seed": _p("int", (0, 2**31 - 1), 1, "every random choice, each item from its own named stream"),
    "length": _p("float", (6.0, 12.0), 10.0, "footprint along X, outer faces (H01: measured E)"),
    "width": _p("float", (4.0, 10.0), 6.0, "footprint along Z, outer faces"),
    "wall_height": _p("float", (2.2, 3.0), 2.4, "to the plate from grade (AST-04's 2.3-2.6 m for H01-H02)"),
    "wall_thickness": _p("float", (0.25, 0.60), 0.45, "the walls' mass and the reveals' depth"),
    "wall_material": _p("str", ", ".join(WALL_MATERIALS), "adobe", "adobe built; the others not yet"),
    "render_loss": _p("float", (0.0, 0.8), 0.3, "share of the render gone (mask R 1), round openings, corners, foot "
                                                "and head"),
    "whitewash_loss": _p("float", (0.0, 1.0), 0.3, "share of the whitewash gone on the render left (mask R 0.5)"),
    "interior_render_loss": _p("float", (0.0, 0.8), 0.1, "as render_loss, on the rooms' walls (E 16-17: flakes off "
                                                         "the blue whitewash)"),
    "interior_whitewash_loss": _p("float", (0.0, 1.0), 0.2, "as whitewash_loss, on the rooms' walls"),
    "base_band_height": _p("float", (0.0, 0.6), 0.42, "the red-clay band at the foot (mask A)"),
    "roof_type_front": _p("str", ", ".join(END_TYPES), "gable", "the -X end (left seen from the door side)"),
    "roof_type_back": _p("str", ", ".join(END_TYPES), "hip", "the +X end"),
    "roof_pitch": _p("float", (18.0, 45.0), 38.4, "slope; sets the ridge (the rafters' apex)"),
    "roof_cover": _p("str", ", ".join(COVERS), "clay_tile", "clay_tile built; the others not yet"),
    "batten_pitch": _p("float", (0.30, 0.75), 0.333, "batten gauge, the tile's covering length (ГОСТ 1808-71)"),
    "tile_m.length": _p("float", (0.33, 0.40), 0.40, "overall, along the slope: gauge + head lap"),
    "tile_m.width": _p("float", (0.20, 0.24), 0.22, "overall, across"),
    "tile_m.cover_width": _p("float", (0.18, 0.24), 0.20, "covering width (the side interlock under the next)"),
    "tile_m.thickness": _p("float", (0.012, 0.016), 0.014, "the clay"),
    "tile_m.camber": _p("float", (0.0, 0.02), 0.008, "the middle above the ends (K02)"),
    "rafter_pitch": _p("float", (0.8, 1.2), 1.0, "rafter spacing; a roof bay's width once the battens are gone"),
    "rafter_m.w": _p("float", (0.08, 0.15), 0.10, "rafter width"),
    "rafter_m.d": _p("float", (0.10, 0.20), 0.15, "rafter depth (the sheet: 0.10-0.15 m)"),
    "eaves_overhang": _p("float", (0.2, 0.6), 0.4, "rafter tails past the walls; their snag edge"),
    "verge_overhang": _p("float", (0.0, 0.4), 0.10, "battens and tiles past a gable's face (assumed)"),
    "chimney.present": _p("bool", "true or false", True, "a brick chimney"),
    "chimney.pos": _p("float", (0.0, 1.0), 0.65, "along the length from -X, snapped to the nearest rafter bay"),
    "chimney.offset_m": _p("float", (-1.5, 1.5), 0.1, "its near face from the ridge line, + towards the back"),
    "chimney.height_above_ridge_m": _p("float", (0.3, 1.0), 0.6, "the cap's top above the ridge (the sheet: 0.5-0.8)"),
    "door_side": _p("str", ", ".join(SIDES[1:]), "front", "front or back built"),
    "door_pos": _p("float", (0.0, 1.0), 0.5, "along the wall seen from outside, left to right"),
    "door_m.width": _p("float", (0.6, 1.2), 0.8, "clear opening (AST-04: 0.8 m)"),
    "door_m.height": _p("float", (1.6, 2.2), 1.9, "clear opening (AST-04: 1.8-1.9 m)"),
    "door_leaf_state": _p("str", ", ".join(joinery.DOOR_STATES), "hanging", "where the leaf is; inside not yet"),
    "hall_width": _p("float", (1.5, 3.0), 2.0, "the hall (сіни) at the door, partition to partition centre"),
    "veranda": _p("bool", "true or false", False, "a closed porch at the door; not built yet"),
    "damage": _p("float", (0.0, 1.0), 0.0, "the state: I below 0.25, D 0.25-0.8, X above"),
    "roof_strip": _p("float", (0.0, 1.0), 0.03, "share of the tiles gone: up to 5 % slipped, scattered; the rest "
                                                "stripped, the tiles left in clusters"),
    "tile_cluster_bias": _p("float", (0.0, 1.0), 0.7, "how the tiles left cluster at the eaves and the lee end"),
    "burst_azimuth_deg": _p("float", (0.0, 360.0), 180.0, "where the burst lay seen from the house, degrees from +X "
                                                          "towards +Z (assumed): the lee end, the rubble's wide side"),
    "batten_break": _p("float", (0.0, 0.6), 0.0, "share of the batten spans between two commons broken, splintered"),
    "rafters_broken": _p("int", (0, 4), 0, "commons off the plate, in pairs with their collar, fallen into the attic"),
    "opening_jag": _p("float", (0.0, 0.4), 0.0, "how far a window's crumble or a breach's blocks reach (the sheet's "
                                                "0.1-0.3 m at 0.2)"),
    "gable_breach": _p("float", (0.0, 1.0), 0.0, "how much of a gable is missing (the X state, not built yet)"),
    "soot_openings": _p("int", (0, 16), 0, "openings with a soot plume above (mask G)"),
    "burnt": _p("float", (0.0, 1.0), 0.0, "fire (not built yet)"),
    "rubble_volume": _p("float", (-1.0, 150.0), 0.0, "bulked rubble at the foot and inside, m3; -1 takes the rule: "
                                                     "the removed solid volume x 1.86"),
    "interior_debris": _p("float", (0.0, 1.0), 0.0, "furniture and contents on the floor (not built yet)"),
    "interior_ceiling_down": _p("float", (0.0, 1.0), 0.0, "share of the ceiling fallen, in the rooms but the hall"),
    "shard_heap_depth": _p("float", (0.0, 0.5), 0.0, "the deepest metre of the heaps along the feet"),
    "hanging_cables": _p("int", (0, 3), 0, "cables looping from the hall's joists"),
    "lod.lod1_switch_m": _p("float", (5.0, 100.0), 25.0, "LOD1 from here"),
    "lod.lod2_switch_m": _p("float", (20.0, 400.0), 100.0, "LOD2 from here"),
    "lod.lod3_switch_m": _p("float", (50.0, 1500.0), 400.0, "LOD3 from here"),
    "lod.cull_m": _p("float", (300.0, 3000.0), 2340.0, "culled beyond: 935 * ridge / 2"),
}
for _w in WINDOWS:
    PARAMS.update({
        f"windows.{_w}.side": _p("str", ", ".join(SIDES), "none", "its wall, or none"),
        f"windows.{_w}.pos": _p("float", (0.0, 1.0), 0.5, "its centre along the wall seen from outside"),
        f"windows.{_w}.w": _p("float", (0.4, 1.6), 0.8, "rough opening width (AST-04: 0.8 m)"),
        f"windows.{_w}.h": _p("float", (0.4, 1.6), 1.0, "rough opening height (AST-04: 1.0 m)"),
        f"windows.{_w}.sill": _p("float", (0.3, 1.5), 0.9, "the opening's bottom above grade"),
        f"windows.{_w}.state": _p("str", ", ".join(joinery.WINDOW_STATES), "shards", "what fills it"),
    })
for _b in BREACHES:
    PARAMS.update({
        f"wall_breaches.{_b}.side": _p("str", ", ".join(SIDES), "none", "a blast hole's wall, or none"),
        f"wall_breaches.{_b}.pos": _p("float", (0.0, 1.0), 0.5, "its centre along the wall"),
        f"wall_breaches.{_b}.w": _p("float", (0.5, 2.0), 0.9, "its width"),
        f"wall_breaches.{_b}.h": _p("float", (0.5, 2.0), 1.2, "its height"),
    })
for _c in COLLAPSES:
    PARAMS.update({
        f"collapse.{_c}.wall": _p("str", ", ".join(SIDES), "none", "a wall down, or none (X, not built yet)"),
        f"collapse.{_c}.height": _p("float", (0.0, 1.5), 1.0, "what is left standing"),
    })

NOT_BUILT = ("gable_breach", "burnt", "interior_debris")  # the X state's and fire's items (D-040: after G1)


def check(params):
    """What is built, AST-04's sizes, the drone against the battens, the openings in their walls and rooms, the
    chimney in its bay and the LODs' order."""
    p = params
    problems = []
    for key, allowed in (("wall_material", WALL_MATERIALS), ("roof_type_front", END_TYPES),
                         ("roof_type_back", END_TYPES), ("roof_cover", COVERS), ("door_side", SIDES[1:]),
                         ("door_leaf_state", joinery.DOOR_STATES)):
        if p[key] not in allowed:
            problems.append(f"'{key}' = {p[key]!r} must be one of {', '.join(allowed)}")
    for w in WINDOWS:
        if p["windows"][w]["side"] not in SIDES or p["windows"][w]["state"] not in joinery.WINDOW_STATES:
            problems.append(f"'windows.{w}': side must be one of {', '.join(SIDES)} and state one of "
                            f"{', '.join(joinery.WINDOW_STATES)}")
    if problems:
        return problems
    built = {"wall_material": ("adobe",), "roof_cover": ("clay_tile",), "roof_type_front": ("gable", "hip"),
             "roof_type_back": ("gable", "hip"), "door_side": ("front", "back"),
             "door_leaf_state": joinery.DOOR_BUILT}
    for key, ok in built.items():
        if p[key] not in ok:
            problems.append(f"'{key}' = {p[key]!r} is not built yet (built: {', '.join(ok)})")
    if p["veranda"]:
        problems.append("'veranda' is not built yet")
    if p["damage"] > DAMAGE_X:
        problems.append(f"'damage' = {p['damage']} is the X state (above {DAMAGE_X}), not built yet (after G1, D-040)")
    for key in NOT_BUILT:
        if p[key] != 0:
            problems.append(f"'{key}' = {p[key]} is not built yet (the X state and fire, after G1); set it to 0")
    for c in COLLAPSES:
        if p["collapse"][c]["wall"] != "none":
            problems.append(f"'collapse.{c}' is the X state, not built yet (after G1, D-040); set its wall to none")
    if p["rafters_broken"] % 2:
        problems.append(f"'rafters_broken' = {p['rafters_broken']}: a common off the plate alone is not built yet (they "
                        "come off in pairs, lib.roof)")
    if p["opening_jag"] > 0:
        for w in WINDOWS:
            if p["windows"][w]["side"] != "none" and p["windows"][w]["state"] not in walls.CRUMBLE_STATES:
                problems.append(f"'windows.{w}' is {p['windows'][w]['state']}: a crumbled edge round an opening "
                                "with no frame is not built yet")
    if p["wall_material"] == "adobe":  # AST-04, D-032
        dw, (dh0, dh1) = AST04_DOOR_M
        if abs(p["door_m"]["width"] - dw) > 1e-9 or not dh0 - 1e-9 <= p["door_m"]["height"] <= dh1 + 1e-9:
            problems.append(f"the door's clear {p['door_m']['width']} x {p['door_m']['height']} m is not AST-04's "
                            f"{dw} x {dh0}-{dh1} m for an adobe house")
        for w in WINDOWS:
            win = p["windows"][w]
            if win["side"] != "none" and (abs(win["w"] - AST04_WINDOW_M[0]) > 1e-9 or abs(win["h"] - AST04_WINDOW_M[1]) > 1e-9):
                problems.append(f"'windows.{w}' {win['w']} x {win['h']} m is not AST-04's {AST04_WINDOW_M[0]} x "
                                f"{AST04_WINDOW_M[1]} m for an adobe house")
    clear = p["batten_pitch"] - roof.BATTEN_M[0]
    if clear >= DRONE_M[0]:
        problems.append(f"'batten_pitch' = {p['batten_pitch']} leaves {clear:.3f} m between battens, which passes the "
                        f"drone ({DRONE_M[0]} m nose-first): intact battens must block it")
    if p["roof_cover"] == "clay_tile":
        lap = p["tile_m"]["length"] - p["batten_pitch"]
        if not HEAD_LAP_M[0] - 1e-9 <= lap <= HEAD_LAP_M[1] + 1e-9:
            problems.append(f"the tile's head lap {lap:.3f} m (length less gauge) lies outside {list(HEAD_LAP_M)}")
        if p["tile_m"]["cover_width"] > p["tile_m"]["width"]:
            problems.append("'tile_m.cover_width' exceeds 'tile_m.width'")
    lod = p["lod"]
    order = [lod["lod1_switch_m"], lod["lod2_switch_m"], lod["lod3_switch_m"], lod["cull_m"]]
    if order != sorted(order) or len(set(order)) < 4:
        problems.append(f"the LOD switches and the cull must increase, got {order}")
    if problems:
        return problems
    plan = walls.Plan(p)
    problems += _check_layout(plan, p)
    return problems


def _check_layout(plan, p):
    """The rooms wide enough, every opening inside its wall with piers, clear of the partitions and of the others,
    the chimney in a room and clear of the hips, the eaves course's overhang."""
    problems = []
    for space in plan.spaces:
        if space.x1 - space.x0 < 1.5:
            problems.append(f"{space.name} is {space.x1 - space.x0:.2f} m wide, under 1.5 m: move the door or the hall")
    for name in walls.SIDES:
        side = plan.sides[name]
        lo, hi = plan.T + walls.PIER_M, side.length - plan.T - walls.PIER_M
        ops = plan.openings_on(name)
        for o in ops:
            if o.a0 < lo - 1e-9 or o.a1 > hi + 1e-9:
                problems.append(f"{o.name} on the {name} wall reaches within {walls.PIER_M} m of a corner")
            if o.y1 > plan.H - 0.15:
                problems.append(f"{o.name} reaches {o.y1:.2f} m, within 0.15 m of the wall head")
            if name in ("front", "back"):
                for xp in plan.partitions:
                    a = (xp - side.origin[0]) / side.along[0]
                    if o.a0 - walls.PIER_M < a + walls.PARTITION_M / 2 and o.a1 + walls.PIER_M > a - walls.PARTITION_M / 2:
                        if o.kind != "door":
                            problems.append(f"{o.name} lies within {walls.PIER_M} m of a partition")
        for a, b in zip(ops, ops[1:]):
            if b.a0 - a.a1 < walls.PIER_M - 1e-9:
                problems.append(f"{a.name} and {b.name} leave under {walls.PIER_M} m of wall between them")
        # a crumbled window's band (lib.walls) stays in its wall, clear of a corner, a partition and its neighbours
        reach = {o.name: walls.CRUMBLE[1] * p["opening_jag"] if o.kind == "window" and p["opening_jag"] > 0 else 0.0
                 for o in ops}
        for o in ops:
            r = reach[o.name]
            if not r:
                continue
            if o.a0 - r < plan.T + 0.05 or o.a1 + r > side.length - plan.T - 0.05 or o.y0 - r < 0.1 or \
                    o.y1 + r > plan.H - 0.1:
                problems.append(f"{o.name}'s crumble ({r:.2f} m) reaches a corner, the ground or the wall head")
            if name in ("front", "back"):
                for xp in plan.partitions:
                    a = (xp - side.origin[0]) / side.along[0]
                    if o.a0 - r < a + walls.PARTITION_M / 2 + 0.05 and o.a1 + r > a - walls.PARTITION_M / 2 - 0.05:
                        problems.append(f"{o.name}'s crumble ({r:.2f} m) reaches a partition")
        for a, b in zip(ops, ops[1:]):
            if b.a0 - a.a1 < reach[a.name] + reach[b.name] + 0.05:
                problems.append(f"{a.name} and {b.name}: their crumbled edges meet")
    door = plan.door
    hall = plan.spaces[1]
    xs = sorted(plan.sides[door.side].point(a, 0, 0)[0] for a in (door.a0, door.a1))
    if xs[0] < hall.x0 + 0.05 or xs[1] > hall.x1 - 0.05:
        problems.append("the door does not fit its hall: widen 'hall_width'")
    n_open = len(plan.openings)
    if p["soot_openings"] > n_open:
        problems.append(f"'soot_openings' = {p['soot_openings']} exceeds the {n_open} openings")
    commons, _, _ = roof.rafter_layout(plan, p["rafter_pitch"])
    ch = roof.chimney_place(plan, p, commons)
    if ch:
        if not any(s.x0 + 0.05 <= ch["x0"] and ch["x1"] <= s.x1 - 0.05 and s.z0 + 0.05 <= ch["z0"] and
                   ch["z1"] <= s.z1 - 0.05 for s in plan.spaces):
            problems.append("the chimney does not stand inside a room, 0.05 m clear of its walls")
        if abs(p["chimney"]["offset_m"]) < roof.RIDGE_BOARD_M[0] / 2 + 0.02:
            problems.append("'chimney.offset_m' puts the chimney through the ridge board")
        for e in (-1, 1):
            if plan.ends[e] == "hip":
                edge = plan.hip_ridge_end(e) + e * min(abs(ch["z0"]), abs(ch["z1"]))  # the hip nearest it, in plan
                gap = e * (edge - (ch["x1"] if e > 0 else ch["x0"]))
                if gap < 0.25:
                    problems.append(f"the chimney stands {gap:.2f} m from a hip, under 0.25 m")
    layout = roof.Layout(plan, p, roof.Slope(plan, "front").S)
    if layout.e > TAIL_MAX_M:
        problems.append(f"the eaves course overhangs the rafter tails by {layout.e:.3f} m, over {TAIL_MAX_M} m: "
                        "change 'eaves_overhang' or 'roof_pitch'")
    return problems


def verdict(width, height):
    """The drone against a clear width and height (the sheet's "How to read")."""
    nose, diag, base, k, envelope = DRONE_M
    if height < envelope:
        return f"no (under the {envelope} m envelope)"
    if width >= diag:
        return f"passes at any yaw, {(width - diag) / 2 * 1000:.0f} mm a side on the diagonal"
    if width > nose:
        theta = math.floor(math.degrees(math.asin((width - base) / k)) - 45)  # whole degrees, never more than holds
        return f"passes within +-{theta} deg yaw, {(width - nose) / 2 * 1000:.0f} mm a side nose-first"
    return "no"


def build(params):
    seed = params["seed"]

    def rngs(what):
        return random.Random(f"house {seed} {what}")

    plan = walls.Plan(params)
    walls.damage(plan, params, rngs)
    lods = [Mesh() for _ in range(4)]
    regions = walls.build(plan, lods[0])
    walls.build(plan, lods[1])
    walls.build(plan, lods[2], damaged=False)
    walls.outer_and_liner(plan, lods[3])
    for mesh in lods[:3]:
        walls.floor(plan, mesh)
    collision = walls.boxes(plan) + [walls.floor_box(plan)]
    wind = [_wind(b, 0.0) for b in walls.boxes(plan, damaged=False)]
    gaps, snag, lines = [], [], []
    for o in plan.openings:
        if o.kind == "breach":
            gaps.append(walls.breach_gap(plan, o))
            continue
        rng = rngs(o.name)
        res = joinery.door(plan, o, lods, rng) if o.kind == "door" else joinery.window(plan, o, lods, rng)
        collision += res.collision
        gaps += res.gaps
        snag += res.snag
    gaps += [joinery.doorway_gap(plan, d) for d in plan.doorways]
    built = roof.build(plan, params, lods, rngs)
    collision += built["collision"]
    wind += built["wind_volumes"]
    snag += built["snag"]
    gaps += built["gaps"]
    lines += built["lines"]
    for k in range(params["hanging_cables"]):
        cable = _cable(plan, lods, rngs(f"cable {k}"))
        collision += cable["collision"]
        snag += cable["snag"]
        lines.append(cable["line"])
    heap = _rubble(plan, params, built, rngs)
    for mesh, extra in zip(lods, heap.lods):
        kits_vehicle.append_mesh(mesh, extra)
    collision += heap.collision
    snag += heap.snag
    wind.append(_cavity(plan, params, gaps))
    raster, mask_lines = masks.facade_atlas(regions, params["render_loss"], params["whitewash_loss"],
                                           params["base_band_height"], params["soot_openings"], rngs, walls.ATLAS_M,
                                           (params["interior_render_loss"], params["interior_whitewash_loss"]))
    png = raster.png()
    textures = [{"role": "damage_mask", "slot": slot, "name": "damage", "uv_min": [0.0, 0.0],
                 "uv_max": list(walls.ATLAS_M), "channels": CHANNELS, "png": png} for slot in ("facade", "interior")]
    lines += _lines(plan, params, built, gaps) + _rubble_lines(heap) + mask_lines
    used = sorted(set().union(*(m.slots() for m in lods)))
    lod = params["lod"]
    return {
        "lods": lods,
        "switch_distances_m": [0.0, lod["lod1_switch_m"], lod["lod2_switch_m"], lod["lod3_switch_m"]],
        "thin_wire": [False] * 4,
        "cull_distance_m": lod["cull_m"],
        "materials": [{"slot": s, "physics": SLOTS[s]} for s in used],
        "collision": collision,
        "gaps": gaps,
        "wind_volumes": wind,
        "holes": [],
        "textures": textures,
        "snag": snag,
        "lines": lines,
        "rubble": heap,
    }


def _wind(box, porosity):
    w = {k: v for k, v in box.items() if k not in ("material", "landable")}
    w["porosity"] = porosity
    return w


def _cavity(plan, params, gaps):
    """The interior as one sheltered volume (porosity 1, `cavity`), its draught through the door and the far
    window: the window on the wall opposite the door nearest across from it (design decision 9), and through every
    breach and every hole the ceiling has (task 1.4)."""
    x0, x1, z0, z1 = plan.inner
    box = {"shape": "box", "center": [(x0 + x1) / 2, (plan.F + plan.H) / 2, (z0 + z1) / 2],
           "size": [x1 - x0, plan.H - plan.F, z1 - z0], "rotation": [0.0, 0.0, 0.0, 1.0], "porosity": 1.0,
           "cavity": True}
    opposite = {"front": "back", "back": "front"}[plan.door.side]
    door_x = plan.sides[plan.door.side].point((plan.door.a0 + plan.door.a1) / 2, 0, 0)[0]
    far = [o for o in plan.openings_on(opposite) if o.kind == "window"]
    names = []
    if far:
        o = min(far, key=lambda o: abs(plan.sides[opposite].point((o.a0 + o.a1) / 2, 0, 0)[0] - door_x))
        names.append(o.name)
    ids = [g["id"] for g in gaps if g["id"] == "door" or any(g["id"] == n or g["id"].startswith(n + "_pane")
                                                             for n in names)
           or g["id"].startswith("breach_") or g["id"].startswith("ceiling_")]
    box["openings"] = ids
    return box


def _cable(plan, lods, rng):
    """A cable hanging in the hall (the sheet: the ceiling still in place over the door room, one cable looping
    down from it, E 16): a catenary between two of the hall's joists' undersides, a bay or two apart across the hall,
    sagging CABLE_SAG_M; a CABLE_SIDES-sided tube at CABLE_R_M in LOD0, a capsule per segment (`cable`) and one
    `hook` snag along it."""
    hall = next(s for s in plan.spaces if s.name == "hall")
    n = int((hall.z1 - hall.z0) / roof.JOIST_PITCH_M)
    zs = [((k + 0.5) - n / 2) * roof.JOIST_PITCH_M + (hall.z0 + hall.z1) / 2 for k in range(n)]
    i = rng.randrange(len(zs) - 1)
    j = min(len(zs) - 1, i + rng.choice((1, 2)))
    x = rng.uniform(hall.x0 + 0.25, hall.x1 - 0.25)
    y_top = plan.H - roof.JOIST_M[1]
    sag = rng.uniform(*CABLE_SAG_M)
    span = zs[j] - zs[i]
    lo, hi = 1e-3, 100.0  # the catenary's parameter a: sag = a (cosh(span / 2a) - 1), by bisection
    for _ in range(100):
        a = (lo + hi) / 2
        lo, hi = (a, hi) if a * (math.cosh(span / 2 / a) - 1) > sag else (lo, a)
    zm = (zs[i] + zs[j]) / 2
    points = [(x, y_top - sag + a * (math.cosh((z - zm) / a) - 1), z)
              for z in (zs[i] + span * t / CABLE_SEGMENTS for t in range(CABLE_SEGMENTS + 1))]
    rings = []
    for k, p in enumerate(points):
        q0, q1 = points[max(0, k - 1)], points[min(len(points) - 1, k + 1)]
        t = [q1[c] - q0[c] for c in range(3)]
        length = math.sqrt(sum(c * c for c in t))
        t = [c / length for c in t]
        e1 = (1.0, 0.0, 0.0)  # the cable runs in a plane of constant x
        e2 = (t[1] * e1[2] - t[2] * e1[1], t[2] * e1[0] - t[0] * e1[2], t[0] * e1[1] - t[1] * e1[0])
        rings.append([lods[0].vertex(tuple(p[c] + CABLE_R_M * (math.cos(f) * e1[c] + math.sin(f) * e2[c])
                                           for c in range(3)))
                      for f in (2 * math.pi * s / CABLE_SIDES for s in range(CABLE_SIDES))])
    along = 0.0
    for k in range(len(points) - 1):
        seg = math.dist(points[k], points[k + 1])
        for s in range(CABLE_SIDES):
            s1 = (s + 1) % CABLE_SIDES
            quad = [rings[k][s], rings[k][s1], rings[k + 1][s1], rings[k + 1][s]]
            mid = [sum(lods[0].vertices[v][c] for v in quad) / 4 for c in range(3)]
            axis = [(points[k][c] + points[k + 1][c]) / 2 for c in range(3)]
            arc = 2 * math.pi * CABLE_R_M / CABLE_SIDES
            lods[0].face(quad, tuple(mid[c] - axis[c] for c in range(3)), CABLE_SLOT,
                         [(s * arc, along), ((s + 1) * arc, along), ((s + 1) * arc, along + seg), (s * arc, along + seg)])
        along += seg
    collision = [meta.capsule(points[k], points[k + 1], CABLE_R_M, "cable") for k in range(len(points) - 1)]
    snag = [{"kind": "hook", "points": [list(p) for p in points], "radius_m": CABLE_R_M, "material": "cable"}]
    low = min(p[1] for p in points)
    return {"collision": collision, "snag": snag,
            "line": f"cable: in the hall at x {x:.2f} m between the joists at z {zs[i]:.2f} and {zs[j]:.2f} m, "
                    f"{span:.2f} m apart, sagging {sag:.2f} m to {low:.2f} m above grade, {CABLE_R_M * 2000:g} mm, "
                    f"{len(collision)} capsules"}


def _rubble(plan, params, built, rngs):
    """What came down (lib.rubble): the tiles off each slope and its fallen battens at the foot of its eaves' wall,
    the adobe of each crumbled window's face and each breach at its wall's foot outside or in, the ceiling's boards and
    clay in each room under its holes, the rafters off the plate and their collars in the attic (on the ceiling where
    it still holds them, else on the floor). `rubble_volume` -1 takes the rule; another value scales the heaps so the
    rubble's bulked volume is that."""
    rb = rubble.Rubble()
    if params["rubble_volume"] == 0:
        return rb
    tx, tz = rubble.travel(params)
    feet = {}

    def foot(name, inner):
        if (name, inner) not in feet:
            side = plan.sides[name]
            p = side.point(0.0, 0.0, plan.T if inner else 0.0)
            out = (-side.normal[0], -side.normal[1]) if inner else side.normal
            keep = [(plan.door.a0 - 0.1, plan.door.a1 + 0.1)] if name == plan.door.side else []
            feet[name, inner] = rubble.Foot(f"{name} {'inner' if inner else 'outer'}", (p[0], p[2]), side.along, out,
                                            side.length, plan.F if inner else 0.0, keep,
                                            max(0.0, out[0] * tx + out[1] * tz))
        return feet[name, inner]

    def a_on(f, x, z):
        return (x - f.origin[0]) * f.along[0] + (z - f.origin[1]) * f.along[1]

    scatters, placements = [], []
    layout = built["layout"]
    tile = debris_tile.Tile(layout.L, layout.w, layout.t, layout.camber)
    wall_of = {"front": "front", "back": "back", "end-1": "left", "end1": "right"}
    by_slope = {}
    for pc in built["gone_tiles"]:
        by_slope.setdefault(pc["slope"].name, []).append(pc)
    for name, gone in sorted(by_slope.items()):  # the tiles: each slope's at the foot of its eaves' wall
        f = foot(wall_of[name], False)
        rng = rngs(f"rubble tiles {name}")
        at = []
        for pc in gone:
            a = sum(q[0] for q in pc["poly"]) / len(pc["poly"])
            s_ = sum(q[1] for q in pc["poly"]) / len(pc["poly"])
            x, _, z = pc["slope"].point(a, s_)
            at.append(a_on(f, x, z))
        solid = sum(roof._area2(pc["poly"]) / math.cos(layout.alpha) * layout.t for pc in gone)
        pieces = [rubble.tile_piece(rng, tile, s) for s in
                  rubble.sizes(rng, round(rubble.PIECES_PER_TILE * len(gone)), *rubble.SHARD_SIZE_M)]
        where = [(m0, m1, sum(m0 <= a < m1 for a in at)) for m0, m1 in f.metres]
        _drop(rb, f, "tile", solid, pieces, where)
        scatters.append((f, "tile", pieces, where, rng, False))
    fallen = built["fallen"]
    for name in ("front", "back"):  # the battens: off the eaves with the tiles
        battens = [b for b in fallen if b["kind"] == "batten" and b["slope"] == name]
        if not battens:
            continue
        f = foot(name, False)
        rng = rngs(f"rubble battens {name}")
        pieces = [rubble.timber_piece(rng, b["section"], b["length"], 2) for b in battens
                  if b["length"] >= rubble.MIN_TIMBER_M]
        at = [a_on(f, sum(b["a"]) / 2, 0.0) for b in battens]  # a side slope's a is x
        where = [(m0, m1, sum(m0 <= a < m1 for a in at)) for m0, m1 in f.metres]
        _drop(rb, f, "timber", sum(b["volume_m3"] for b in battens), pieces, where)
        scatters.append((f, "timber", pieces, where, rng, True))
    for entry in walls.removed(plan):  # the adobe: a crumbled face or a breach at its wall's foot
        f = foot(entry["side"], entry["face"] == "inner")
        rng = rngs(f"rubble adobe {entry['opening']} {entry['face']}")
        pieces, solid = [], 0.0
        for size in rubble.sizes(rng, 64, *rubble.ADOBE_SIZE_M):
            piece = rubble.adobe_piece(rng, size)
            if solid + piece["volume_m3"] > rubble.ADOBE_PIECE_SHARE * entry["volume_m3"]:
                break
            pieces.append(piece)
            solid += piece["volume_m3"]
        a0, a1 = entry["a"]
        where = [(max(0.0, a0 - 0.3), min(f.length, a1 + 0.3), 1.0)]
        _drop(rb, f, "adobe", entry["volume_m3"], pieces,
              [(m0, m1, max(0.0, min(m1, a1 + 0.3) - max(m0, a0 - 0.3))) for m0, m1 in f.metres])
        scatters.append((f, "adobe", pieces, where, rng, False))
    layers = []
    for si, space in enumerate(plan.spaces):  # the ceiling: its boards and clay under its holes
        holes = [h["rect"] for h in built["holes"] if h["space"] == si]
        if not holes:
            continue
        rng = rngs(f"rubble ceiling {space.name}")
        boards = [b for b in fallen if b["kind"] == "board" and b["space"] == si]
        clay = sum(c["volume_m3"] for c in fallen if c["kind"] == "clay" and c["space"] == si)
        wood = sum(b["volume_m3"] for b in boards)
        pieces = [(rubble.timber_piece(rng, b["section"], b["length"] - 0.02, 2), b) for b in boards
                  if b["length"] >= rubble.MIN_TIMBER_M]
        slabs, solid = [], 0.0
        for size in rubble.sizes(rng, 64, *rubble.SLAB_SIZE_M):
            piece = rubble.clay_slab(rng, size, roof.CLAY_M * rng.uniform(*rubble.SLAB_THICK))
            if solid + piece["volume_m3"] > rubble.CLAY_PIECE_SHARE * clay:
                break
            slabs.append(piece)
            solid += piece["volume_m3"]
        rb.note("clay", removed=clay)
        rb.note("timber", removed=wood)
        core = {"clay": rubble.BULKING * (clay - solid),
                "timber": rubble.BULKING * (wood - sum(p["volume_m3"] for p, _ in pieces))}
        layers.append((si, holes, core, pieces, slabs, rng))
    attic, attic_rng = _attic_pieces(rb, plan, built, rngs)
    loose = [p["volume_m3"] for _, _, ps, _, _, _ in scatters for p in ps] + [p["volume_m3"] for p, _ in attic]
    _scale(params, feet, layers, loose)
    for f in feet.values():
        f.lay(params["shard_heap_depth"])
        rb.heap(f)
    tops = {}
    for si, holes, core, pieces, slabs, rng in layers:
        area = sum((x1 - x0) * (z1 - z0) for x0, x1, z0, z1 in holes)
        total = sum(max(0.0, v) for v in core.values())
        h = total / area
        shares = {k: max(0.0, v) / total for k, v in core.items()} if total > 0 else {"clay": 1.0}
        for rect in holes:
            rb.layer(rect, plan.F, h, shares)
        tops[si] = (holes, plan.F + h)
        for piece, b in pieces:  # each board where it fell, turned a little
            x, z = sum(b["x"]) / 2 + rng.uniform(-0.05, 0.05), sum(b["z"]) / 2
            rb.place(piece, rubble.rest(piece, x, z, plan.F + h, math.pi / 2 + rng.uniform(-0.25, 0.25)), "timber")
        for k, piece in enumerate(slabs):  # the slabs spread over the holes
            x0, x1, z0, z1 = holes[k % len(holes)]
            r = min(rubble.reach(piece), (x1 - x0) / 2, (z1 - z0) / 2)
            x, z = rng.uniform(x0 + r, x1 - r), rng.uniform(z0 + r, z1 - r)
            rb.place(piece, rubble.rest(piece, x, z, plan.F + h, rng.uniform(0, 2 * math.pi)), "clay")
    for f, cls, pieces, where, rng, lean in scatters:
        rubble.scatter(rb, f, cls, pieces, where, rng, lean)
    _attic_place(rb, plan, attic, tops, attic_rng)
    return rb


def _drop(rb, foot, cls, solid, pieces, weights):
    """A class's fall at a foot: its removed solid volume noted, the pieces' bulk taken out of it, the rest of its
    bulk on the foot's heap by `weights` [(a0, a1, weight)] over its metres."""
    rb.note(cls, removed=solid)
    core = rubble.BULKING * max(0.0, solid - sum(p["volume_m3"] for p in pieces))
    w = {(m0, m1): wt for m0, m1, wt in weights}
    foot.add(cls, core, lambda m0, m1: w.get((m0, m1), 0.0))


def _scale(params, feet, layers, loose):
    """`rubble_volume` above 0 scales every heap and layer so the bulked rubble is that volume (-1 keeps the rule);
    `loose` the solid volumes of the pieces outside the layers."""
    target = params["rubble_volume"]
    if target < 0:
        return
    pieces = rubble.BULKING * (sum(loose) + sum(v for _, _, _, ps, slabs, _ in layers for v in
                                                [p["volume_m3"] for p, _ in ps] + [s["volume_m3"] for s in slabs]))
    cores = sum(sum(b.values()) for f in feet.values() for b in f.bulk) + \
        sum(max(0.0, v) for _, _, core, _, _, _ in layers for v in core.values())
    k = max(0.0, target - pieces) / cores if cores else 0.0
    for f in feet.values():
        f.bulk = [{c: v * k for c, v in b.items()} for b in f.bulk]
    for _, _, core, _, _, _ in layers:
        for c in core:
            core[c] *= k


def _attic_pieces(rb, plan, built, rngs):
    """The rafters off the plate and their collars as K03 pieces, each with the x it fell at; their timber noted as
    removed."""
    rng = rngs("rubble attic")
    z_in0, z_in1 = plan.inner[2], plan.inner[3]
    out = []
    for item in built["fallen"]:
        if item["kind"] not in ("rafter", "collar"):
            continue
        x = item["x"] + (0.25 if item["kind"] == "collar" else 0.0) + rng.uniform(-0.05, 0.05)
        length = min(item["length"], z_in1 - z_in0 - 0.3)
        out.append((rubble.timber_piece(rng, item["section"], length, 1 if item["kind"] == "rafter" else 2,
                                        nails=2 if item["kind"] == "rafter" else 1, splinter_len=0.2), x))
        rb.note("timber", removed=item["volume_m3"])
    return out, rng


def _attic_place(rb, plan, attic, tops, rng):
    """Each rafter off the plate lies across the attic where it stood, on the ceiling's clay when the ceiling holds
    under both its ends, else on its room's floor (or the rubble there); its collar beside it."""
    ceiling = plan.H + roof.BOARD_M[1] + roof.CLAY_M
    for piece, x in attic:
        half = max(abs(v[0]) for v in piece["lods"][0].vertices)
        ends = [(x, -half + 0.3), (x, half - 0.3)]
        si = next((k for k, s in enumerate(plan.spaces) if s.x0 <= x <= s.x1), None)
        holes, floor = tops.get(si, ([], plan.F))
        held = not any(h[0] <= ex <= h[1] and h[2] <= ez <= h[3] for h in holes for ex, ez in ends)
        rb.place(piece, rubble.rest(piece, x, 0.0, ceiling if held or si is None else floor,
                                    math.pi / 2 + rng.uniform(-0.1, 0.1)), "timber")


def _rubble_lines(rb):
    """The rubble's report: the rule's volumes per class and the pieces laid."""
    if not rb.removed:
        return []
    removed, pieces, core = (sum(t.values()) for t in (rb.removed, rb.pieces, rb.core))
    rule, laid = rubble.BULKING * removed, core + rubble.BULKING * pieces
    by = ", ".join(f"{c} {rb.removed[c]:.3f}" for c in sorted(rb.removed))
    counts = ", ".join(f"{c} {rb.counts[c]}" for c in sorted(rb.counts))
    return [f"rubble: removed {removed:.3f} m3 solid ({by}) x {rubble.BULKING:g} = {rule:.3f} m3; laid {laid:.3f} m3 "
            f"(heaps {core:.3f} + {rubble.BULKING:g} x pieces {pieces:.3f}), {100 * (laid / rule - 1):+.2f} %",
            f"rubble pieces: {counts}; {len(rb.collision)} shapes, {len(rb.snag)} snags"]


def _lines(plan, params, built, gaps):
    """The build's report: the sizes and where they come from, the tiles, the damage, and every gap against the
    drone."""
    layout, tiles = built["layout"], built["tiles"]
    p = params
    commons = built["commons"]
    bays = [b - a for a, b in zip(commons, commons[1:])]
    out = [f"walls: {plan.L:g} x {plan.W:g} m outer, {plan.H:g} m to the plate, {plan.T:g} m thick (H01: measured E, "
           "AST-04)",
           f"roof: pitch {p['roof_pitch']:g} deg, ridge (the rafters' apex) {plan.apex:.4f} m, eaves {plan.eaves:g} m, "
           f"rafters {plan.rafter_w:g} x {plan.rafter_d:g} m at {p['rafter_pitch']:g} m (bays {min(bays):.3f}-"
           f"{max(bays):.3f} m centre to centre, the narrow one at the gable)",
           f"battens: {roof.BATTEN_M[0]:g} x {roof.BATTEN_M[1]:g} m at {layout.gauge:g} m, {len(layout.battens)} per "
           f"slope and the eaves fillet {layout.fillet * 1000:.1f} mm high",
           f"tiles: {layout.L:g} x {p['tile_m']['width']:g} m overall, {layout.gauge:g} x {layout.cover:g} m covering "
           f"(ГОСТ 1808-71), head lap {layout.lap * 1000:.0f} mm, tilt rise {layout.rise * 1000:.1f} mm, eaves course "
           f"{layout.e * 1000:.0f} mm past the tails; {tiles['whole']} whole and {tiles['cut']} cut laid, "
           f"{tiles['removed']} gone (roof_strip {p['roof_strip']:g}), {tiles['boxes']} course boxes"]
    if p["damage"] >= DAMAGE_I:
        out.append(f"damage: {tiles['breaks']} of {tiles['spans']} batten spans between commons broken (batten_break "
                   f"{p['batten_break']:g}), {tiles['rafters_off']} rafters off the plate; ceiling down "
                   f"{built.get('ceiling_down_m2', 0.0):.2f} of {built.get('ceiling_m2', 0.0):.2f} m2 "
                   f"(interior_ceiling_down {p['interior_ceiling_down']:g})")
    for g in gaps:  # the drone through each: the narrowest across, the polygon's height (a flue's other side)
        w, h = joinery.extent(g)
        out.append(f"gap {g['id']}: {w:.3f} x {h:.3f} m, narrowest {g['narrowest_m']:.3f} m: "
                   f"{verdict(g['narrowest_m'], max(w, h) if g['id'] == 'flue' or g['id'].startswith('ceiling_') else h)}")
    return out
