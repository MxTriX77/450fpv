"""The above-ground head of a village cellar entrance, notes W4: the structure clip C ends by flying into.

Sizes from W4: 1.2-1.5 m wide, 1.8-2.0 m high, 2-3 m long, a doorway of about 0.7 x 1.6 m onto a dark stairwell, red
brick under patchy lime render (feed #b97541), a weathered plank door and frame (#73623d), a sloped slab roof with
dark roofing felt, and a light-blue tarp (#899cc2) draped beside it. This one is 1.5 x 3.0 m and 1.95 m high at the
door, falling to 1.6 m at the back, and its doorway is 0.78 x 1.62 m: the narrowest fly-in on the map.

    blender -b --factory-startup --python blender/structures/cellar_head.py   (or tools/blender/export.py --build)

It is placed at the same position and yaw as `cellar_shaft`, which is the hole filler under it and holds the steps:
the head stands on the shaft's lip, so its back wall is over solid lip and never over the cavity.

Collision is one box per pier, lintel, wall, roof slab, felt layer and the open door leaf. Deliberate
simplifications: the door frame and the tarp, which are 2-6 cm of trim and soft sheet that a drone tears or snags
rather than stops against (`snag_hazard` carries them), and no glass, of which there is none.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "tools", "blender"))
import assetkit as kit  # noqa: E402
import buildingkit as bk  # noqa: E402

FOOT = (1.5, 3.0)        # outer footprint, m: 1.5 wide (W4: 1.2-1.5) and 3.0 long (W4: 2-3)
WALL = 0.25              # brick wall thickness, m
FRONT_H, BACK_H = 1.95, 1.60   # wall top at the door and at the back, m (W4: 1.8-2.0 high)
DOOR = (0.78, 1.62)      # doorway width and height, m (W4: about 0.7 x 1.6)
SLAB = 0.09              # concrete roof slab, m
FELT = 0.012             # roofing felt over it, m
EAVE = 0.12              # the slab past the walls on every side, m
LINTEL = 0.16            # the concrete lintel course over the doorway, m
SEED = 7741

BRICK, CONCRETE, FELT_MAT, PLANK, TARP = "brick_red", "concrete_rough", "roof_felt", "wood_weathered", "tarp_blue"
# The head faces asset +Z: the doorway is in the wall at z = +FOOT[1] / 2, the steps run away toward -Z.
FRONT_Z = FOOT[1] / 2
SLOPE_DEG = math.degrees(math.atan2(FRONT_H - BACK_H, FOOT[1]))
DETAIL = [{"panel": (0.50, 0.32)}, {"panel": (1.20, 0.90)}, {"panel": (99.0, 99.0)}]


def build(level, shapes):
    detail = DETAIL[level]
    mesh = kit.Mesh()
    collision = shapes if shapes is not None else []
    render_patch = bk.patchiness(SEED, scale=(1.5, 0.9))

    def face(u, v):
        """Lime render over the brick, off in patches (W4). The lintel course over the door stays concrete."""
        if v > DOOR[1] and abs(u) < DOOR[0] / 2 + 0.05:
            return CONCRETE
        return BRICK if render_patch(u, v) < 0.05 else CONCRETE

    # The front wall carries the doorway; the back and the two sides are solid, each following the roof's fall.
    bk.wall(mesh, collision, "x", FRONT_Z - WALL / 2, 0.0, FOOT[0], WALL, FRONT_H,
            [(-DOOR[0] / 2, DOOR[0] / 2, 0.0, DOOR[1])], detail["panel"], face)
    bk.wall(mesh, collision, "x", -FRONT_Z + WALL / 2, 0.0, FOOT[0], WALL, BACK_H, [], detail["panel"], face,
            uv_seed=1.7)
    side = FOOT[1] - 2 * WALL
    for sx in (-1, 1):
        for step in range(2):
            z0 = -side / 2 + side * step / 2
            z1 = -side / 2 + side * (step + 1) / 2
            top = BACK_H + (FRONT_H - BACK_H) * (z0 + z1 + side) / (2 * side)
            bk.wall(mesh, collision, "z", sx * (FOOT[0] / 2 - WALL / 2), (z0 + z1) / 2, z1 - z0, WALL, top, [],
                    detail["panel"], face, uv_seed=3.3 * (step + 1))

    # The sloped slab and its felt, both overhanging every wall.
    slab = (FOOT[0] + 2 * EAVE, math.hypot(FOOT[1], FRONT_H - BACK_H) + 2 * EAVE)
    middle = (FRONT_H + BACK_H) / 2
    collision.append(mesh.box((0.0, middle + SLAB / 2, 0.0), (slab[0], SLAB, slab[1]), CONCRETE,
                              rotation_deg=(0, -SLOPE_DEG, 0)))
    collision.append(mesh.box((0.0, middle + SLAB + FELT / 2, 0.0), (slab[0], FELT, slab[1] - 0.02), FELT_MAT,
                              rotation_deg=(0, -SLOPE_DEG, 0)))

    # The plank door, swung right back against the front wall, and the tarp thrown over the west side.
    collision.append(mesh.box((-(DOOR[0] / 2 + 0.37), (DOOR[1] - 0.04) / 2, FRONT_Z + 0.04),
                              (DOOR[0] - 0.04, DOOR[1] - 0.04, 0.045), PLANK, rotation_deg=(3.0, 0, 1.5)))
    if level < 2:
        mesh.box((-(FOOT[0] / 2 + 0.16), 0.52, -0.6), (0.34, 1.05, 1.30), TARP, rotation_deg=(0, 0, -11.0))
    return mesh


shapes = []
kit.reset()
lods = [build(level, shapes if level == 0 else None) for level in range(3)]

entry = {
    "type": "object",
    "scene": "res://assets/models/structures/cellar_head.glb",
    "material": "masonry",
    "collision": shapes,
    "snag_hazard": True,
    "wind_porosity": 0.0,
    "gaps": [{"name": "cellar_door", "center_m": [0.0, round(DOOR[1] / 2, 4), round(FRONT_Z, 4)],
              "width_m": DOOR[0], "height_m": DOOR[1], "yaw_deg": 0.0}],
    "lod_switch_m": [22.0, 60.0],
}
kit.finish(__file__, "prop", lods, {
    BRICK: ("#b97541", 0.86), CONCRETE: ("#9c9790", 0.88), FELT_MAT: ("#3a3733", 0.72),
    PLANK: ("#73623d", 0.9), TARP: ("#899cc2", 0.45),
}, entry)
