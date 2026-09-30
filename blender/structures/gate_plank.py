"""Yard gate of weathered planks between two posts, notes B6: the gates and posts along every village yard.

Sizes from B6: 1-2 m high, boards 0.10-0.15 m, posts 0.1-0.2 m, grey weathered wood (feed #615e56). The opening is
2.6 m between posts, wide enough for a cart, with a header rail over it. One leaf still hangs on its lower hinge and
stands half open; the other is gone, which is how gates read in clips E and J. So the gate is a real fly-through, and
the leaf standing in the opening is what makes the line through it off-centre. Both posts are plumb: a leaning post
would move the gap's own edges with height, and the leaning ones belong to the poles and wires of task 3.5.

    blender -b --factory-startup --python blender/structures/gate_plank.py   (or tools/blender/export.py --build)

Collision is one box per post, rail, brace and board, the boxes drawn. The 3 cm gaps between the leaf's boards stay
open for the fiber to drop into and catch (`snag_hazard`); they are far too narrow to fly.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402

OPENING = 2.60          # between the posts, m
POST = 0.14             # square post side, m (B6: 0.1-0.2)
POST_TOP = 2.10         # post top above ground, m
POST_BURIED = 0.40
HEADER = (0.09, 0.13)   # header rail thickness (across) and height, m
HEADER_Y = 2.01         # its top, m
LEAF = (1.25, 1.70)     # one leaf: width and height, m
LEAF_OPEN = 35.0        # how far the surviving leaf stands open, degrees
BOARD = (0.12, 0.022)   # leaf board width and thickness, m (B6: 0.10-0.15)
BOARD_GAP = 0.03
LEDGE = (0.09, 0.028)   # the leaf's two ledges: height and thickness, m
SEED = 4219
WOOD = "wood_weathered"

HINGE_X = -OPENING / 2 + POST / 2      # the surviving leaf hangs on the west post
COS_OPEN = math.cos(math.radians(LEAF_OPEN))
SIN_OPEN = math.sin(math.radians(LEAF_OPEN))
LEAF_EDGE = HINGE_X + LEAF[0] * COS_OPEN - LEDGE[1] / 2 * SIN_OPEN   # the open leaf's far edge, x
POST_FACE = OPENING / 2 - POST / 2                                   # the east post's inner face, x
CLEAR = POST_FACE - LEAF_EDGE          # what is left to fly through, m
DETAIL = [{"boards": True, "brace": True}, {"boards": False, "brace": True}, {"boards": False, "brace": False}]


def build(level, shapes):
    rng = random.Random(SEED)
    mesh = kit.Mesh()
    collision = shapes if shapes is not None else []

    for sx in (-1, 1):
        x = sx * (OPENING / 2)
        collision.append(mesh.box((x, (POST_TOP - POST_BURIED) / 2, 0.0), (POST, POST_TOP + POST_BURIED, POST), WOOD,
                                  uv_offset=(x, 0)))
    collision.append(mesh.box((0.0, HEADER_Y - HEADER[1] / 2, 0.0), (OPENING + POST, HEADER[1], HEADER[0]), WOOD,
                              uv_offset=(0.4, 0)))

    # The surviving leaf, turned LEAF_OPEN degrees about its hinge post. Its own frame runs along the leaf, so the
    # boards and ledges are built in leaf coordinates and then turned together.
    def at(along, y, out):
        """A point on the leaf: `along` m from the hinge across the leaf, `out` m off its face."""
        return (HINGE_X + along * COS_OPEN - out * SIN_OPEN, y, along * SIN_OPEN + out * COS_OPEN)

    ledge_y = (0.22, LEAF[1] - 0.26)
    for y in ledge_y:
        collision.append(mesh.box(at(LEAF[0] / 2, y, LEDGE[1] / 2), (LEAF[0], LEDGE[0], LEDGE[1]), WOOD,
                                  rotation_deg=(-LEAF_OPEN, 0, 0), uv_offset=(0.7, y)))
    # A diagonal brace between the ledges, which is what keeps a plank gate square until it sags.
    if DETAIL[level]["brace"]:
        brace = math.hypot(LEAF[0] - 0.1, ledge_y[1] - ledge_y[0])
        collision.append(mesh.box(at(LEAF[0] / 2, (ledge_y[0] + ledge_y[1]) / 2, LEDGE[1] / 2),
                                  (brace, LEDGE[0], LEDGE[1] * 0.9), WOOD,
                                  rotation_deg=(-LEAF_OPEN, 0, math.degrees(math.atan2(ledge_y[1] - ledge_y[0], LEAF[0] - 0.1))),
                                  uv_offset=(1.9, 0)))
    if DETAIL[level]["boards"]:
        pitch = BOARD[0] + BOARD_GAP
        count = int(LEAF[0] / pitch)
        for i in range(count):
            along = (i + 0.5) * LEAF[0] / count
            top = LEAF[1] - (0.0 if i < count - 2 else rng.uniform(0.35, 0.7))   # the last boards are broken short
            collision.append(mesh.box(at(along, top / 2, LEDGE[1] + BOARD[1] / 2), (BOARD[0], top, BOARD[1]), WOOD,
                                      rotation_deg=(-LEAF_OPEN, 0, 0), uv_offset=(rng.uniform(0, 3), 0)))
    else:
        collision.append(mesh.box(at(LEAF[0] / 2, LEAF[1] / 2, LEDGE[1] + BOARD[1] / 2),
                                  (LEAF[0], LEAF[1], BOARD[1]), WOOD, rotation_deg=(-LEAF_OPEN, 0, 0)))
    return mesh


shapes = []
kit.reset()
lods = [build(level, shapes if level == 0 else None) for level in range(3)]

entry = {
    "type": "object",
    "scene": "res://assets/models/structures/gate_plank.glb",
    "material": "timber",
    "collision": shapes,
    # Wind: the gate's own plane, a bay the wind sees straight through beside the leaf.
    "wind_volume": [{"shape": "box", "size_m": [round(OPENING + POST, 4), POST_TOP, round(LEAF[0] * SIN_OPEN + 0.2, 4)],
                     "position_m": [0.0, round(POST_TOP / 2, 4), round(LEAF[0] * SIN_OPEN / 2, 4)]}],
    "snag_hazard": True,
    "wind_porosity": 0.62,
    # The gap is what the surviving leaf leaves of the opening, 1.5 cm in from the leaf's edge and the far post and 2 cm
    # under the header, so the declared rectangle is clear of the mesh and no more than 5 cm inside it.
    "gaps": [{"name": "gate", "center_m": [round((LEAF_EDGE + POST_FACE) / 2, 4), round((HEADER_Y - HEADER[1] - 0.02) / 2, 4), 0.0],
              "width_m": round(CLEAR - 0.03, 4), "height_m": round(HEADER_Y - HEADER[1] - 0.02, 4), "yaw_deg": 0.0}],
    "lod_switch_m": [20.0, 55.0],
}
kit.finish(__file__, "prop", lods, {WOOD: ("#615e56", 0.9)}, entry)
