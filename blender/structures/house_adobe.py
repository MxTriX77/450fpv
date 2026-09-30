"""Damaged clay (adobe) village house, notes B1: the house clip E flies into through the door and sees daylight
through a window on the far side. The first of the pilot's couple of village houses, and the core fly-in structure.

Sizes from B1: footprint 10 x 6 m, walls 2.45 m high and 0.45 m thick, gable ridge 5 m, door 0.8 x 1.8 m, window
openings about 0.8 x 1.0 m, a brick chimney about 0.5 m square. The damage is B1's: the roof stripped to its battens
and rafters with clay tiles left in patches, the lime-clay render off the walls in patches over the bare clay-and-
straw core, tile shards heaped at the wall foot, the apex of the east gable taken off, and inside a dark room with a
fallen ceiling board heap and joists across the gap the boards left.

    blender -b --factory-startup --python blender/structures/house_adobe.py   (or tools/blender/export.py --build)

The way in and out, which is the point of the asset (B1 `gap`): the door on the south wall at x +1.8 and the north
wall's window at the same x, so the line straight through the room from the door and out of the far window is clear
at about 1.3 m. The inner doorway of the partition wall, the other three windows and the breach in the east gable
are gaps too. The stripped roof is open between the surviving batten runs, but a roof opening lies in a sloping
plane and the catalog's gaps are upright rectangles (yaw only), so it carries no gap entry.

Collision is one box per pier, lintel, gable course, plate, ridge, rafter, batten run, tile patch, ceiling board
panel, joist, shard and fallen board. The wall panels the render patches are drawn from sit exactly in the faces of
the pier and lintel boxes, so collision follows the visual at 0 mm with a tenth of the shapes (buildingkit).
Deliberate simplifications, all thinner than 3 cm or not solid: the window and door frames, the hanging cable of
E (8 mm, carried by `snag_hazard` instead) and the glass, of which none is left.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import buildingkit as bk  # noqa: E402

FOOT = (10.0, 6.0)      # outer footprint, m (B1: 8-12 x 5-7)
WALL = 0.45             # wall thickness, m (B1: 0.4-0.5)
WALL_H = 2.45           # wall top above ground, m (B1: 2.3-2.6)
RIDGE_Y = 5.0           # gable ridge above ground, m (B1: about 5)
PLATE = 0.12            # square wall plate on top of each long wall, m
GABLE = 0.30            # gable thickness, m
GABLE_BROKEN = 3.30     # the east gable stands only this high; its apex is gone
PARTITION = (-1.9, 0.25)  # the inner wall's centre x and thickness, m
DOOR = (1.8, 0.8, 1.8)  # centre x, width, height, m (B1)
WINDOW = (0.8, 1.0)     # opening, m (B1); sill at SILL
SILL = 0.95
FAR_WINDOW = (1.8, 0.8, 0.85, 2.0)  # the exit of clip E: centre x, width, sill, head; its sill course is broken out
CHIMNEY = (-3.3, -1.5, 0.5, 5.35)   # x, z, square side, top above ground, m (B1: about 0.5 m square)
CEILING = (2.41, 2.45)  # the ceiling boards' underside and top, m
JOIST = (0.08, 0.14)    # ceiling joist section, m
CEILING_HOLE = (-3.1, 0.2, 1.8)     # boards gone west of this x, between these z (E: fallen boards)
BATTEN = (0.03, 0.05, 0.33)         # depth, width along the slope, spacing along the slope, m (B1: 0.3-0.4 apart)
RAFTER = (0.06, 0.16, 0.98)         # width along X, depth, spacing along X, m
OVERHANG = 0.30         # the roof past each gable, m
SEED = 3102

INSIDE = (FOOT[0] / 2 - WALL, FOOT[1] / 2 - WALL)    # interior half extents, m
EAVE = (WALL_H + PLATE, FOOT[1] / 2 - PLATE / 2)     # the rafter seat: its height and its |z|
RISE = RIDGE_Y - 0.08 - EAVE[0]                      # rafter seat to the ridge beam's centre, m
PITCH = math.degrees(math.atan2(RISE, EAVE[1]))      # roof pitch, degrees
SLOPE = math.hypot(RISE, EAVE[1])                    # rafter length, m
SIN_P, COS_P = math.sin(math.radians(PITCH)), math.cos(math.radians(PITCH))

RENDER, CLAY, TILE, TIMBER, PLANK, BRICK = \
    "render_clay", "adobe_clay", "tile_clay", "timber_rough", "wood_weathered", "brick_red"
# Per LOD: the wall panel size, the rafters drawn, whether batten runs, tile patches, the ceiling and the clutter are.
DETAIL = [
    {"panel": (1.25, 0.62), "rafter_step": 1, "battens": True, "boards": True, "clutter": True},
    {"panel": (2.50, 1.25), "rafter_step": 2, "battens": True, "boards": False, "clutter": False},
    {"panel": (99.0, 99.0), "rafter_step": 3, "battens": False, "boards": False, "clutter": False},
]


def on_slope(side, x, along, out):
    """A point on one roof slope: `side` +1 south or -1 north, `along` m up the slope from the rafter seat, `out` m
    out along the slope's normal (0 is the rafter's centre line)."""
    return (x, EAVE[0] + along * SIN_P + out * COS_P, side * (EAVE[1] - along * COS_P + out * SIN_P))


def gable_half(y):
    """Half the gable triangle's width at height y: the apex is the ridge, the base the wall top."""
    return max(0.0, FOOT[1] / 2 * (RIDGE_Y - y) / (RIDGE_Y - WALL_H))


def runs(kept):
    """Contiguous runs of True in `kept`, as (first, last) index pairs: a batten row that shelling left in pieces is
    drawn and collided as one box per surviving run, so the gaps between them are real openings."""
    out, start = [], None
    for i, on in enumerate(list(kept) + [False]):
        if on and start is None:
            start = i
        elif not on and start is not None:
            out.append((start, i - 1))
            start = None
    return out


def build(level, shapes):
    """One LOD. `shapes` collects the collision boxes; pass None for a level that is not LOD0."""
    detail = DETAIL[level]
    rng = random.Random(SEED)
    mesh = kit.Mesh()
    collision = shapes if shapes is not None else []
    render_patch = bk.patchiness(SEED)

    def face(u, v):
        """Bare clay where the render has come off (notes B1)."""
        return CLAY if render_patch(u, v) < -0.12 else RENDER

    # ---- the four outer walls. Openings are (u0, u1, v0, v1) in the wall's own coordinates, u from its centre.
    south = [(DOOR[0] - DOOR[1] / 2, DOOR[0] + DOOR[1] / 2, 0.0, DOOR[2])]
    for x in (-3.8, -1.2, 3.8):
        south.append((x - WINDOW[0] / 2, x + WINDOW[0] / 2, SILL, SILL + WINDOW[1]))
    north = [(FAR_WINDOW[0] - FAR_WINDOW[1] / 2, FAR_WINDOW[0] + FAR_WINDOW[1] / 2, FAR_WINDOW[2], FAR_WINDOW[3])]
    north.append((-2.6 - WINDOW[0] / 2, -2.6 + WINDOW[0] / 2, SILL, SILL + WINDOW[1]))
    east = [(-WINDOW[0] / 2, WINDOW[0] / 2, SILL, SILL + WINDOW[1])]
    long_face = FOOT[1] / 2 - WALL / 2
    short_face = FOOT[0] / 2 - WALL / 2
    bk.wall(mesh, collision, "x", long_face, 0.0, FOOT[0], WALL, WALL_H, south, detail["panel"], face)
    bk.wall(mesh, collision, "x", -long_face, 0.0, FOOT[0], WALL, WALL_H, north, detail["panel"], face, uv_seed=3.1)
    bk.wall(mesh, collision, "z", short_face, 0.0, 2 * INSIDE[1], WALL, WALL_H, east, detail["panel"], face, uv_seed=6.7)
    bk.wall(mesh, collision, "z", -short_face, 0.0, 2 * INSIDE[1], WALL, WALL_H, [], detail["panel"], face, uv_seed=1.4)
    # The inner wall, with a doorway between the two rooms.
    bk.wall(mesh, collision, "z", PARTITION[0], 0.0, 2 * INSIDE[1], PARTITION[1], WALL_H,
            [(-0.425, 0.425, 0.0, 1.95)], detail["panel"], lambda u, v: RENDER, uv_seed=8.2)

    # ---- gables: horizontal courses under the roof slope, the east one broken off above GABLE_BROKEN.
    for side, top in ((-1, RIDGE_Y), (1, GABLE_BROKEN)):
        courses = 6 if top == RIDGE_Y else 2
        step = (top - WALL_H) / courses
        for k in range(courses):
            y0, y1 = WALL_H + k * step, WALL_H + (k + 1) * step
            half = gable_half(y1)
            if half < 0.2:
                continue
            collision.append({"shape": "box", "size_m": [round(GABLE, 4), round(y1 - y0, 4), round(2 * half, 4)],
                              "position_m": [round(side * (FOOT[0] / 2 - GABLE / 2), 4), round((y0 + y1) / 2, 4), 0.0]})
            for a0, a1, b0, b1 in bk.panels((-half, half, y0, y1), detail["panel"]):
                mesh.box((side * (FOOT[0] / 2 - GABLE / 2), (b0 + b1) / 2, (a0 + a1) / 2),
                         (GABLE, b1 - b0, a1 - a0), face(a0 + side * 5, b0), uv_offset=(a0, b0))

    # ---- the roof frame: wall plates, ridge beam, rafters.
    span = FOOT[0] + 2 * OVERHANG
    for side in (-1, 1):
        collision.append(mesh.box((0.0, WALL_H + PLATE / 2, side * EAVE[1]), (FOOT[0], PLATE, PLATE), TIMBER))
    collision.append(mesh.box((0.0, RIDGE_Y - 0.08, 0.0), (span, 0.16, 0.10), TIMBER))
    count = int(FOOT[0] / RAFTER[2]) + 1
    for k in range(0, count, detail["rafter_step"]):
        x = -FOOT[0] / 2 + 0.1 + k * (FOOT[0] - 0.2) / (count - 1)
        for side in (-1, 1):
            centre = on_slope(side, x, SLOPE / 2, 0.0)
            collision.append(mesh.box(centre, (RAFTER[0], RAFTER[1], SLOPE), TIMBER,
                                      rotation_deg=(0, side * PITCH, 0), uv_offset=(k * 0.7, 0)))

    # ---- battens across the rafters, in the pieces shelling left them (B1: a stripped roof). One box per run.
    pieces = 7
    if detail["battens"]:
        rows = int((SLOPE - 0.3) / BATTEN[2]) + 1
        for side in (-1, 1):
            for row in range(rows):
                along = 0.2 + row * BATTEN[2]
                kept = [rng.random() > 0.30 for _ in range(pieces)]
                # The bay over the ceiling hole is stripped right through: that is where the sky shows from inside.
                if side == 1 and 3 <= row <= 8:
                    kept[0] = kept[1] = False
                if level > 0:
                    kept = [any(kept)] * pieces
                for first, last in runs(kept):
                    u0 = -span / 2 + span * first / pieces
                    u1 = -span / 2 + span * (last + 1) / pieces
                    centre = on_slope(side, (u0 + u1) / 2, along, (RAFTER[1] + BATTEN[0]) / 2)
                    collision.append(mesh.box(centre, (u1 - u0, BATTEN[0], BATTEN[1]), TIMBER,
                                              rotation_deg=(0, side * PITCH, 0), uv_offset=(u0, along)))

    # ---- clay tiles left in patches on the battens (B1).
    out = (RAFTER[1] + 2 * BATTEN[0] + 0.035) / 2 + BATTEN[0] / 2
    for side in (-1, 1):
        patches = 5 if level < 2 else 2
        for _ in range(patches):
            width = rng.uniform(1.4, 3.0) * (1 if level < 2 else 2.4)
            height = rng.uniform(0.7, 1.5) * (1 if level < 2 else 2.0)
            x = rng.uniform(-FOOT[0] / 2 + width / 2, FOOT[0] / 2 - width / 2)
            along = rng.uniform(height / 2 + 0.2, SLOPE - height / 2 - 0.1)
            if side == 1 and x < -2.2 and 1.0 < along < 3.0:
                continue  # the stripped bay stays bare
            collision.append(mesh.box(on_slope(side, x, along, out), (width, 0.035, height), TILE,
                                      rotation_deg=(0, side * PITCH, 0), uv_offset=(x, along)))

    # ---- the brick chimney, its top course knocked askew.
    stack = CHIMNEY[3] - 0.25
    collision.append({"shape": "box", "size_m": [CHIMNEY[2], round(stack - 2.0, 4), CHIMNEY[2]],
                      "position_m": [CHIMNEY[0], round((2.0 + stack) / 2, 4), CHIMNEY[1]]})
    courses = max(1, round((stack - 2.0) / 0.42)) if level < 2 else 1
    for k in range(courses):
        y0 = 2.0 + (stack - 2.0) * k / courses
        y1 = 2.0 + (stack - 2.0) * (k + 1) / courses
        mesh.box((CHIMNEY[0], (y0 + y1) / 2, CHIMNEY[1]), (CHIMNEY[2], y1 - y0, CHIMNEY[2]), BRICK,
                 uv_offset=(0, y0))
    collision.append(mesh.box((CHIMNEY[0] + 0.1, stack + 0.125, CHIMNEY[1]), (0.3, 0.25, CHIMNEY[2]), BRICK,
                              rotation_deg=(6.0, 0, 3.0)))

    # ---- the ceiling: joists across the house, boards over them, gone at the west end (E: fallen boards).
    joists = int(2 * INSIDE[0] / 0.9) + 1
    for k in range(joists):
        x = -INSIDE[0] + 0.2 + k * (2 * INSIDE[0] - 0.4) / (joists - 1)
        if -4.2 < x < -3.4:
            continue  # two joists came down with the boards; one of them lies on the floor below
        collision.append(mesh.box((x, (CEILING[0] - JOIST[1] / 2), 0.0), (JOIST[0], JOIST[1], FOOT[1] - 2 * WALL + 0.4),
                                  TIMBER, uv_offset=(k * 0.5, 0)))
    board_rects = [(CEILING_HOLE[0], INSIDE[0], -INSIDE[1], INSIDE[1]),
                   (-INSIDE[0], CEILING_HOLE[0], -INSIDE[1], CEILING_HOLE[1]),
                   (-INSIDE[0], CEILING_HOLE[0], CEILING_HOLE[2], INSIDE[1])]
    for x0, x1, z0, z1 in board_rects:
        collision.append({"shape": "box", "size_m": [round(x1 - x0, 4), round(CEILING[1] - CEILING[0], 4), round(z1 - z0, 4)],
                          "position_m": [round((x0 + x1) / 2, 4), round((CEILING[0] + CEILING[1]) / 2, 4), round((z0 + z1) / 2, 4)]})
        strips = max(1, round((x1 - x0) / 0.24)) if detail["boards"] else 1
        for k in range(strips):
            a0, a1 = x0 + (x1 - x0) * k / strips, x0 + (x1 - x0) * (k + 1) / strips
            mesh.box(((a0 + a1) / 2, (CEILING[0] + CEILING[1]) / 2, (z0 + z1) / 2),
                     (a1 - a0, CEILING[1] - CEILING[0], z1 - z0), PLANK, uv_offset=(0, a0))

    # ---- the clutter that decides how a landing goes: shards at the wall foot, boards on the floor, the blown door.
    if detail["clutter"]:
        bk.heap(mesh, collision, rng, (-1.0, 0.0, FOOT[1] / 2 + 0.35), (2.2, 0.22), 7, (0.30, 0.05, 0.22), TILE)
        bk.heap(mesh, collision, rng, (3.6, 0.0, -FOOT[1] / 2 - 0.30), (1.6, 0.20), 6, (0.28, 0.05, 0.20), TILE)
        # The east gable's apex came down outside that end, clay lumps and shards together (notes B5).
        bk.heap(mesh, collision, rng, (FOOT[0] / 2 + 0.9, 0.0, 0.0), (0.8, 1.6), 8, (0.34, 0.20, 0.30), CLAY)
        for _ in range(8):
            x = rng.uniform(-INSIDE[0] + 0.5, INSIDE[0] - 0.5)
            z = rng.uniform(-INSIDE[1] + 0.4, INSIDE[1] - 0.4)
            collision.append(mesh.box((x, 0.025, z), (rng.uniform(1.2, 2.4), 0.03, 0.22), PLANK,
                                      rotation_deg=(rng.uniform(0, 180), 0, rng.uniform(-2, 2)),
                                      uv_offset=(rng.uniform(0, 3), rng.uniform(0, 3))))
        # The fallen joist, and the door leaf blown out of its frame onto the ground.
        collision.append(mesh.box((-3.8, 0.07, 0.4), (0.09, 0.14, 4.6), TIMBER, rotation_deg=(6.0, 0, 0)))
        collision.append(mesh.box((DOOR[0] + 0.35, 0.02, FOOT[1] / 2 + 0.75), (0.76, 0.04, 1.76), PLANK,
                                  rotation_deg=(24.0, 0.0, 0.0)))
    return mesh


shapes = []
kit.reset()
lods = [build(level, shapes if level == 0 else None) for level in range(3)]

# Wind: the whole house as one bluff body, with the open fraction of its long face counted on a 5 cm grid. One box
# and one porosity, so the stripped roof and the solid walls share a figure; the side-on silhouette is what the wake
# model reads, and a per-height porosity is not something the catalog can say (see the handoff of task 3.2).
porosity = bk.silhouette_porosity(shapes, (-FOOT[0] / 2, FOOT[0] / 2), (0.0, RIDGE_Y))

entry = {
    "type": "object",
    "scene": "res://assets/models/structures/house_adobe.glb",
    "material": "masonry",
    "collision": shapes,
    "wind_volume": [{"shape": "box", "size_m": [FOOT[0], RIDGE_Y, FOOT[1]], "position_m": [0.0, RIDGE_Y / 2, 0.0]}],
    "snag_hazard": True,
    "wind_porosity": porosity,
    "gaps": [
        {"name": "door", "center_m": [DOOR[0], round(DOOR[2] / 2, 4), FOOT[1] / 2], "width_m": DOOR[1],
         "height_m": DOOR[2], "yaw_deg": 0.0},
        {"name": "window_south_west", "center_m": [-3.8, round(SILL + WINDOW[1] / 2, 4), FOOT[1] / 2],
         "width_m": WINDOW[0], "height_m": WINDOW[1], "yaw_deg": 0.0},
        {"name": "window_south_mid", "center_m": [-1.2, round(SILL + WINDOW[1] / 2, 4), FOOT[1] / 2],
         "width_m": WINDOW[0], "height_m": WINDOW[1], "yaw_deg": 0.0},
        {"name": "window_south_east", "center_m": [3.8, round(SILL + WINDOW[1] / 2, 4), FOOT[1] / 2],
         "width_m": WINDOW[0], "height_m": WINDOW[1], "yaw_deg": 0.0},
        {"name": "window_far", "center_m": [FAR_WINDOW[0], round((FAR_WINDOW[2] + FAR_WINDOW[3]) / 2, 4), -FOOT[1] / 2],
         "width_m": FAR_WINDOW[1], "height_m": round(FAR_WINDOW[3] - FAR_WINDOW[2], 4), "yaw_deg": 180.0},
        {"name": "window_north_west", "center_m": [-2.6, round(SILL + WINDOW[1] / 2, 4), -FOOT[1] / 2],
         "width_m": WINDOW[0], "height_m": WINDOW[1], "yaw_deg": 180.0},
        {"name": "window_gable_east", "center_m": [FOOT[0] / 2, round(SILL + WINDOW[1] / 2, 4), 0.0],
         "width_m": WINDOW[0], "height_m": WINDOW[1], "yaw_deg": 90.0},
        {"name": "inner_door", "center_m": [PARTITION[0], 0.975, 0.0], "width_m": 0.85, "height_m": 1.95,
         "yaw_deg": 0.0},
        {"name": "gable_breach_east", "center_m": [FOOT[0] / 2, round(GABLE_BROKEN + 0.45, 4), 0.0], "width_m": 1.8,
         "height_m": 0.9, "yaw_deg": 90.0},
    ],
    "lod_switch_m": [45.0, 130.0],
}
kit.finish(__file__, "house", lods, {
    RENDER: ("#9f713b", 0.92), CLAY: ("#8a6a42", 0.95), TILE: ("#825f44", 0.78),
    TIMBER: ("#967056", 0.9), PLANK: ("#615e56", 0.9), BRICK: ("#b97541", 0.86),
}, entry)
