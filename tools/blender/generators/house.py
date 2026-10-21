"""house: a village house from the shared house table (object sheet docs/object-sheets/houses-and-yard.md §Shared
"Shared house generator parameters" and §H01; p1-h01-adobe-house, design decisions 1, 3-6, 8, 9 and 11). Plain
Python: lib.walls (the plan, the walls and their UVs), lib.joinery (doors and windows), lib.roof (the carpentry, the
tiles, the chimney and the ceiling), lib.masks (the damage-mask atlas).

Params (assets-src/houses/<variant>.json) are PARAMS below; `check` holds them to AST-04, to the drone and to what
is built. Built so far: the intact state of an adobe house with a clay-tile roof, gabled or hipped at either end, its
door on the front or back. Refused as "not built yet": the D and X states (`damage` 0.25 and up, and every damage item
above zero: stripping past the I state's 2-5 %, broken battens and rafters, jagged openings, breaches, collapse,
fire, rubble, the ceiling down, hanging cables, the tile heap, a leaf blown `inside`: task 1.4), the other wall
materials, covers and end shapes, and a veranda. Every item takes its explicit value from the params file (lib.params
requires every key): `damage` documents the state, the items are set per state.

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

from lib import joinery, masks, roof, walls
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
STRIP_I = 0.05                       # the sheet: I has 2-5 % of its tiles slipped
SLOTS = {"facade": "masonry", "interior": "masonry", "clay": "masonry", "tile": "masonry",
         "tile_fracture": "masonry", "brick": "masonry", "timber": "timber", "door": "timber", "glass": "glass",
         "iron": "steel"}
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
    "roof_strip": _p("float", (0.0, 1.0), 0.03, "share of the tiles gone (I: 2-5 %, scattered)"),
    "tile_cluster_bias": _p("float", (0.0, 1.0), 0.7, "how the tiles left cluster at the eaves and lee end (task 1.4)"),
    "batten_break": _p("float", (0.0, 0.6), 0.0, "share of batten spans broken (task 1.4)"),
    "rafters_broken": _p("int", (0, 4), 0, "rafters snapped or off the plate (task 1.4)"),
    "opening_jag": _p("float", (0.0, 0.4), 0.0, "how far broken edges jut into each opening (task 1.4)"),
    "gable_breach": _p("float", (0.0, 1.0), 0.0, "how much of a gable is missing (task 1.4)"),
    "soot_openings": _p("int", (0, 16), 0, "openings with a soot plume above (mask G)"),
    "burnt": _p("float", (0.0, 1.0), 0.0, "fire (task 1.4)"),
    "rubble_volume": _p("float", (0.0, 150.0), 0.0, "rubble at the foot and inside (task 1.4)"),
    "interior_debris": _p("float", (0.0, 1.0), 0.0, "boards, plaster and tile on the floor (task 1.4)"),
    "interior_ceiling_down": _p("float", (0.0, 1.0), 0.0, "share of the ceiling fallen (task 1.4)"),
    "shard_heap_depth": _p("float", (0.0, 0.5), 0.0, "the tile heap along the foot (task 1.4)"),
    "hanging_cables": _p("int", (0, 3), 0, "cables looping from the ceiling (task 1.4)"),
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
        f"wall_breaches.{_b}.side": _p("str", ", ".join(SIDES), "none", "a blast hole's wall, or none (task 1.4)"),
        f"wall_breaches.{_b}.pos": _p("float", (0.0, 1.0), 0.5, "its centre along the wall"),
        f"wall_breaches.{_b}.w": _p("float", (0.5, 2.0), 0.9, "its width"),
        f"wall_breaches.{_b}.h": _p("float", (0.5, 2.0), 1.2, "its height"),
    })
for _c in COLLAPSES:
    PARAMS.update({
        f"collapse.{_c}.wall": _p("str", ", ".join(SIDES), "none", "a wall down, or none (task 1.4)"),
        f"collapse.{_c}.height": _p("float", (0.0, 1.5), 1.0, "what is left standing"),
    })

NOT_BUILT = ("batten_break", "rafters_broken", "opening_jag", "gable_breach", "burnt", "rubble_volume",
             "interior_debris", "interior_ceiling_down", "shard_heap_depth", "hanging_cables")


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
    if p["damage"] >= DAMAGE_I:
        problems.append(f"'damage' = {p['damage']} is a D or X state (from {DAMAGE_I}), not built yet (task 1.4)")
    if p["roof_strip"] > STRIP_I:
        problems.append(f"'roof_strip' = {p['roof_strip']} strips past the I state's {STRIP_I:g}: not built yet "
                        "(task 1.4's clusters)")
    for key in NOT_BUILT:
        if p[key] != 0:
            problems.append(f"'{key}' = {p[key]} is a damage item, not built yet (task 1.4); set it to 0")
    for b in BREACHES:
        if p["wall_breaches"][b]["side"] != "none":
            problems.append(f"'wall_breaches.{b}' is a damage item, not built yet (task 1.4); set its side to none")
    for c in COLLAPSES:
        if p["collapse"][c]["wall"] != "none":
            problems.append(f"'collapse.{c}' is a damage item, not built yet (task 1.4); set its wall to none")
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
    lods = [Mesh() for _ in range(4)]
    regions = walls.build(plan, lods[0])
    walls.build(plan, lods[1])
    walls.build(plan, lods[2])
    walls.outer_and_liner(plan, lods[3])
    for mesh in lods[:3]:
        walls.floor(plan, mesh)
    collision = walls.boxes(plan) + [walls.floor_box(plan)]
    wind = [_wind(b, 0.0) for b in walls.boxes(plan)]
    gaps, snag, lines = [], [], []
    for o in plan.openings:
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
    wind.append(_cavity(plan, params, gaps))
    raster, mask_lines = masks.facade_atlas(regions, params["render_loss"], params["whitewash_loss"],
                                           params["base_band_height"], params["soot_openings"], rngs, walls.ATLAS_M)
    png = raster.png()
    textures = [{"role": "damage_mask", "slot": slot, "name": "damage", "uv_min": [0.0, 0.0],
                 "uv_max": list(walls.ATLAS_M), "channels": CHANNELS, "png": png} for slot in ("facade", "interior")]
    lines += _lines(plan, params, built, gaps) + mask_lines
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
    }


def _wind(box, porosity):
    w = {k: v for k, v in box.items() if k not in ("material", "landable")}
    w["porosity"] = porosity
    return w


def _cavity(plan, params, gaps):
    """The interior as one sheltered volume (porosity 1, `cavity`), its draught through the door and the far
    window: the window on the wall opposite the door nearest across from it (design decision 9)."""
    x0, x1, z0, z1 = plan.inner
    box = {"shape": "box", "center": [(x0 + x1) / 2, (plan.F + plan.H) / 2, (z0 + z1) / 2],
           "size": [x1 - x0, plan.H - plan.F, z1 - z0], "rotation": [0.0, 0.0, 0.0, 1.0], "porosity": 1.0,
           "cavity": True}
    opposite = {"front": "back", "back": "front"}[plan.door.side]
    door_x = plan.sides[plan.door.side].point((plan.door.a0 + plan.door.a1) / 2, 0, 0)[0]
    far = [o for o in plan.openings_on(opposite)]
    names = []
    if far:
        o = min(far, key=lambda o: abs(plan.sides[opposite].point((o.a0 + o.a1) / 2, 0, 0)[0] - door_x))
        names.append(o.name)
    ids = [g["id"] for g in gaps if g["id"] == "door" or any(g["id"] == n or g["id"].startswith(n + "_pane")
                                                             for n in names)]
    box["openings"] = ids
    return box


def _lines(plan, params, built, gaps):
    """The build's report: the sizes and where they come from, the tiles, and every gap against the drone."""
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
    for g in gaps:  # the drone through each: the narrowest across, the polygon's height (a flue's other side)
        w, h = joinery.extent(g)
        out.append(f"gap {g['id']}: {w:.3f} x {h:.3f} m, narrowest {g['narrowest_m']:.3f} m: "
                   f"{verdict(g['narrowest_m'], max(w, h) if g['id'] == 'flue' else h)}")
    return out
