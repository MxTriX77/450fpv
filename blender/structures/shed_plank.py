"""Plank yard shed, notes B3: the grey plank outbuilding beside the house in clip E, two or three per village yard.

Sizes from B3: 3-6 x 2-4 m and 2-2.5 m high. This one is 4.2 x 2.8 m, its shed roof falling from 2.4 m at the front
to 2.0 m at the back, clad in vertical weathered planks (feed #615e56) on a timber frame with a corrugated sheet-
metal roof, one sheet lifted at a corner. The doorway stands open with no leaf, two boards are gone from the west
wall and one from the back, which is how these read in the clips.

    blender -b --factory-startup --python blender/structures/shed_plank.py   (or tools/blender/export.py --build)

The open doorway (0.9 x 1.9 m) is the one fly-through gap; the missing boards are 0.14 m slots, far too narrow.
Collision is one box per frame member, per cladding pier and lintel, and per roof sheet. The cladding panels the
plank look is drawn from sit in those piers' faces, so collision follows the visual at 0 mm (buildingkit).
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import buildingkit as bk  # noqa: E402

FOOT = (4.2, 2.8)        # outer footprint, m (B3: 3-6 x 2-4)
FRONT_H, BACK_H = 2.40, 2.00   # eave heights, m (B3: 2-2.5)
CLAD = 0.025             # cladding thickness, m
POST = 0.10              # square frame post, m
POST_BURIED = 0.25
RAIL = (0.06, 0.10)      # top rail thickness (across) and height, m
DOOR = (1.0, 0.9, 1.9)   # centre along the front wall, width, height, m
WINDOW = (-1.2, 0.5, 1.15, 0.5)  # centre along the front wall, width, sill, size (square), m (B3: a small window)
BOARD = 0.14             # cladding board width, m: a missing board leaves a slot this wide
SHEET = (1.5, 0.008)     # roof sheet width along the slope run, thickness, m
SEED = 5507

PLANK, TIMBER, SHEET_METAL = "wood_weathered", "timber_rough", "sheet_metal_worn"
SLOPE_DEG = math.degrees(math.atan2(FRONT_H - BACK_H, FOOT[1]))
DETAIL = [{"panel": (0.42, 0.80), "purlins": 3, "sheets": 3},
          {"panel": (1.05, 1.60), "purlins": 2, "sheets": 3},
          {"panel": (99.0, 99.0), "purlins": 0, "sheets": 1}]


def build(level, shapes):
    detail = DETAIL[level]
    rng = random.Random(SEED)
    mesh = kit.Mesh()
    collision = shapes if shapes is not None else []
    plank = lambda u, v: PLANK

    # ---- the frame: four posts sunk into the ground, and a top rail along each wall at its own eave height.
    for sx in (-1, 1):
        for sz, height in ((1, FRONT_H), (-1, BACK_H)):
            x = sx * (FOOT[0] / 2 - POST / 2)
            z = sz * (FOOT[1] / 2 - POST / 2)
            collision.append(mesh.box((x, (height - POST_BURIED) / 2, z), (POST, height + POST_BURIED, POST), TIMBER,
                                      uv_offset=(x, 0)))
    for sz, height in ((1, FRONT_H), (-1, BACK_H)):
        collision.append(mesh.box((0.0, height - RAIL[1] / 2, sz * (FOOT[1] / 2 - RAIL[0] / 2)),
                                  (FOOT[0] - 2 * POST, RAIL[1], RAIL[0]), TIMBER))
    # Purlins up the slope, so the roof sheets have something to sit on.
    for k in range(detail["purlins"]):
        z = FOOT[1] / 2 - 0.1 - k * (FOOT[1] - 0.2) / 2
        y = FRONT_H - (FOOT[1] / 2 - z) * (FRONT_H - BACK_H) / FOOT[1]
        collision.append(mesh.box((0.0, y + 0.05, z), (FOOT[0] + 0.3, 0.07, 0.09), TIMBER,
                                  rotation_deg=(0, -SLOPE_DEG, 0), uv_offset=(0, k)))

    # ---- the cladding: the front wall with its doorway and window, the back with a board gone, the sides.
    front = [(DOOR[0] - DOOR[1] / 2, DOOR[0] + DOOR[1] / 2, 0.0, DOOR[2]),
             (WINDOW[0] - WINDOW[1] / 2, WINDOW[0] + WINDOW[1] / 2, WINDOW[2], WINDOW[2] + WINDOW[3])]
    face_z = FOOT[1] / 2 - CLAD / 2
    bk.wall(mesh, collision, "x", face_z, 0.0, FOOT[0], CLAD, FRONT_H, front, detail["panel"], plank)
    bk.wall(mesh, collision, "x", -face_z, 0.0, FOOT[0], CLAD, BACK_H, [(0.9, 0.9 + BOARD, 0.0, BACK_H)],
            detail["panel"], plank, uv_seed=2.3)
    side = 2 * (FOOT[1] / 2 - CLAD)
    face_x = FOOT[0] / 2 - CLAD / 2
    # The side walls follow the slope, so each is cut into two steps rather than one square panel.
    for sx in (-1, 1):
        gone = [(-0.35, -0.35 + BOARD, 0.0, BACK_H), (0.15, 0.15 + BOARD, 0.0, BACK_H)] if sx < 0 else []
        for step in range(3):
            z0 = -side / 2 + side * step / 3
            z1 = -side / 2 + side * (step + 1) / 3
            top = BACK_H + (FRONT_H - BACK_H) * (step + 0.5) / 3
            cut = [(max(a, z0) - (z0 + z1) / 2, min(b, z1) - (z0 + z1) / 2, c, min(d, top))
                   for a, b, c, d in gone if a < z1 and b > z0]
            bk.wall(mesh, collision, "z", sx * face_x, (z0 + z1) / 2, z1 - z0, CLAD, top, cut, detail["panel"],
                    plank, uv_seed=4.1 * step)

    # ---- the corrugated roof: sheets across the slope, the front-west one lifted off its purlin (B3 `snag`).
    run = math.hypot(FOOT[1], FRONT_H - BACK_H) + 0.3
    sheets = detail["sheets"]
    for k in range(sheets):
        x = -FOOT[0] / 2 + FOOT[0] * (k + 0.5) / sheets
        lifted = k == 0 and sheets > 1
        collision.append(mesh.box((x, (FRONT_H + BACK_H) / 2 + 0.09 + (0.10 if lifted else 0.0), -0.05),
                                  (FOOT[0] / sheets + 0.12, SHEET[1], run), SHEET_METAL,
                                  rotation_deg=(0, -SLOPE_DEG + (5.0 if lifted else 0.0), 2.0 if lifted else 0.0),
                                  uv_offset=(x, 0)))

    # ---- a few bricks and shards trodden into the ground at the doorway (notes T6).
    if level == 0:
        bk.heap(mesh, collision, rng, (DOOR[0], 0.0, FOOT[1] / 2 + 0.3), (0.45, 0.2), 5, (0.24, 0.07, 0.11), TIMBER)
    return mesh


shapes = []
kit.reset()
lods = [build(level, shapes if level == 0 else None) for level in range(3)]
porosity = bk.silhouette_porosity(shapes, (-FOOT[0] / 2, FOOT[0] / 2), (0.0, FRONT_H + 0.2))

entry = {
    "type": "object",
    "scene": "res://assets/models/structures/shed_plank.glb",
    "material": "timber",
    "collision": shapes,
    "wind_volume": [{"shape": "box", "size_m": [FOOT[0], round(FRONT_H + 0.2, 4), FOOT[1]],
                     "position_m": [0.0, round((FRONT_H + 0.2) / 2, 4), 0.0]}],
    "snag_hazard": True,
    "wind_porosity": porosity,
    "gaps": [{"name": "door", "center_m": [DOOR[0], round(DOOR[2] / 2, 4), round(FOOT[1] / 2, 4)],
              "width_m": DOOR[1], "height_m": DOOR[2], "yaw_deg": 0.0}],
    "lod_switch_m": [25.0, 70.0],
}
kit.finish(__file__, "prop", lods, {
    PLANK: ("#615e56", 0.9), TIMBER: ("#73623d", 0.9), SHEET_METAL: ("#736867", 0.62),
}, entry)
