"""The cellar stairwell: the hole filler under `cellar_head` (notes W4, terrain-holes F-1 to F-8).

The heightfield cannot hold an opening 0.9 m across, so the cellar mouth is a hole in it (`holes.png`) and this asset
is the ground inside: cut soil walls, the earth steps down and the lip the head stands on. Six steps of 0.30 m take
the floor to 1.80 m under the yard, which is the usual depth of a village cellar.

    blender -b --factory-startup --python blender/structures/cellar_shaft.py   (or tools/blender/export.py --build)

The stairs run the length of the head standing over them: the threshold is the inner face of the head's front wall at
z = +1.25, the first riser drops from it, and the cavity's back wall stops at z = -1.25, which is where the head's own
back wall stands. So the head never has a wall over the cavity.

It must be placed at the same position and yaw (a multiple of 90 degrees) as its `cellar_head`, on level ground, and
so that the hole band lands on surface-cell edges: the band is 1.5 m across by 3.5 m long about the origin, so the
origin's x and z are both a multiple of 0.5 plus 0.25 (see tools/map/make_sample_patch.py, CELLAR). The lip then
matches TerrainHeight exactly, which is F-3.

Walls and steps are `surface:belt_bare`, firm cut soil, as F-7 requires: contacts and rays in here carry a soil
surface and no catalog material. Every box is drawn and collided, so the cavity's open faces are the drawn ones
(F-6) to 0 mm. One LOD: ten boxes that can only be seen through a 0.78 m doorway, so a far level would save nothing.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402

THRESHOLD = 1.25        # the first riser, at the inner face of the head's front wall (cellar_head.py)
CAVITY = (0.90, 2.50)   # the open stairwell: width across, and length from the threshold to the back wall
BAND = 1.50             # the hole band's width across; its length follows from the cavity and LEAD
LEAD = 0.50             # the band past the threshold and past the back wall, so both faces are inset (F-2)
LIP_RISE = 0.005        # the filler's top over TerrainHeight: clear of the ground without breaking F-4's 10 mm
STEPS, TREAD, RISE = 6, 0.36, 0.30
BASE = -2.35            # the bottom of every box, m: 0.55 m of soil behind the floor's face (F-5)
SOIL = "soil_cut"

FLOOR = -STEPS * RISE                # the cellar floor, m under the yard
FRONT = THRESHOLD + LEAD             # the band's near z
BACK = THRESHOLD - CAVITY[1] - LEAD  # its far z
BACK_FACE = THRESHOLD - CAVITY[1]    # the cavity's back wall


def box(mesh, shapes, x0, x1, z0, z1, y0, y1, uv):
    shapes.append(mesh.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), SOIL,
                           uv_offset=uv))


kit.reset()
mesh = kit.Mesh()
shapes = []
half_cavity, half_band = CAVITY[0] / 2, BAND / 2

# The two side walls: they carry the cavity's side faces and the lip the head's side walls stand on, one box each.
for sx in (-1, 1):
    x0, x1 = sorted((sx * half_cavity, sx * half_band))
    box(mesh, shapes, x0, x1, BACK, FRONT, BASE, LIP_RISE, (0, 0))
# The wall behind the bottom step, and the apron in front of the threshold.
box(mesh, shapes, -half_cavity, half_cavity, BACK, BACK_FACE, BASE, LIP_RISE, (1.3, 0))
box(mesh, shapes, -half_cavity, half_cavity, THRESHOLD, FRONT, BASE, LIP_RISE, (2.6, 0))
# The steps. Each is a block of earth from the base up to its tread, and reaches 0.2 m into the side walls (F-5).
for k in range(STEPS):
    z1 = THRESHOLD - TREAD * k
    z0 = BACK_FACE if k == STEPS - 1 else THRESHOLD - TREAD * (k + 1)
    box(mesh, shapes, -half_cavity - 0.2, half_cavity + 0.2, z0, z1, BASE, -RISE * (k + 1), (0, k * 0.4))

entry = {
    "type": "object",
    "scene": "res://assets/models/structures/cellar_shaft.glb",
    "material": "surface:belt_bare",
    "collision": shapes,
    "snag_hazard": False,
    "wind_porosity": 1.0,
    "gaps": [],
}
kit.finish(__file__, "prop", [mesh], {SOIL: ("#5a4c3a", 0.95)}, entry)
