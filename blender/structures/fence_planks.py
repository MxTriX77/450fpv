"""Weathered plank fence, one bay between two posts: the grey village yard fence of notes B6 (clip E, feed #615e56).

Vertical boards nailed to two rails between square timber posts, the boards on the rails' north (-Z) face. The bay
runs along X and faces ±Z, with its origin on the ground midway between the posts. Posts go 0.3 m into the ground,
so bays can follow a slope. A seeded few boards are missing, broken short or leaning, as in E.

    blender -b --factory-startup --python blender/structures/fence_planks.py   (or tools/blender/export.py --build)

Collision is one box per post, rail and board, the exact boxes drawn: the 4 cm gaps between boards stay open for
the fiber, which can drop between boards and catch. No gap is a fly-through (all are far narrower than the drone).
"""
import os
import random
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402

BAY = 2.5              # post centre to post centre, m
POST = 0.12            # square post side, m
POST_TOP = 1.75        # post top above ground, m
POST_BURIED = 0.3      # m
RAIL = (0.04, 0.08)    # rail thickness (z) and height (y), m
RAIL_Y = (0.30, 1.30)  # rail centre heights, m
BOARD = (0.11, 0.02)   # board width (x) and thickness (z), m
BOARD_GAP = 0.04       # m
BOARD_Y = (0.05, 1.55) # board bottom and nominal top, m
SEED = 6
WOOD = "wood_weathered"

kit.reset()
rng = random.Random(SEED)
mesh = kit.Mesh()
shapes = []
post_face = -POST / 2                        # north face of the posts
rail_z = post_face + RAIL[0] / 2             # rails flush with the posts' north face
board_z = post_face - BOARD[1] / 2           # boards on the rails' north face

for x in (-BAY / 2, BAY / 2):
    height = POST_TOP + POST_BURIED
    shapes.append(mesh.box((x, POST_TOP - height / 2, 0), (POST, height, POST), WOOD, uv_offset=(x, 0)))
span = BAY - POST
for y in RAIL_Y:
    shapes.append(mesh.box((0, y, rail_z), (span, RAIL[1], RAIL[0]), WOOD, uv_offset=(0.3, y)))

pitch = BOARD[0] + BOARD_GAP
count = int((span + BOARD_GAP) // pitch)
missing = rng.randrange(count)
broken = rng.choice([i for i in range(count) if i != missing])
for i in range(count):
    top = BOARD_Y[1] + rng.uniform(-0.03, 0.03)
    lean = rng.uniform(-1.2, 1.2)
    if i == missing:
        continue
    if i == broken:
        top = rng.uniform(0.6, 1.0)
    x = (i - (count - 1) / 2) * pitch
    shapes.append(mesh.box((x, (BOARD_Y[0] + top) / 2, board_z), (BOARD[0], top - BOARD_Y[0], BOARD[1]), WOOD,
                           rotation_deg=(0, 0, lean), uv_offset=(rng.uniform(0, 3), rng.uniform(0, 3))))

# Wind: the bay's side-on silhouette, with the open fraction counted on a 1 cm grid (leans ignored).
volume = (BAY + POST, POST_TOP)
covered = 0
cells = [(-volume[0] / 2 + (i + 0.5) * 0.01, (j + 0.5) * 0.01) for i in range(round(volume[0] / 0.01))
         for j in range(round(volume[1] / 0.01))]
for cx, cy in cells:
    covered += any(abs(cx - s["position_m"][0]) <= s["size_m"][0] / 2 and abs(cy - s["position_m"][1]) <= s["size_m"][1] / 2
                   for s in shapes)
porosity = round(1 - covered / len(cells), 2)
back, front = board_z - BOARD[1] / 2, POST / 2  # from the boards' north face to the posts' south face

entry = {
    "type": "object",
    "scene": "res://assets/models/structures/fence_planks.glb",
    "material": "timber",
    "collision": shapes,
    "wind_volume": [{"shape": "box", "size_m": [volume[0], volume[1], round(front - back, 4)],
                     "position_m": [0, volume[1] / 2, round((front + back) / 2, 4)]}],
    "snag_hazard": True,
    "wind_porosity": porosity,
    "gaps": [],
}
kit.finish(__file__, "prop", [mesh], {WOOD: ("#615e56", 0.9)}, entry)
