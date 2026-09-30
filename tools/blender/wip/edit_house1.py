path = "C:/Users/maksy/.claude/projects/450fpv/.claude/worktrees/agent-ab9844693b9a3251d/blender/structures/house_adobe.py"
t = open(path, encoding="utf-8").read()


def sub(old, new, count=1):
    global t
    assert t.count(old) == count, (old, t.count(old))
    t = t.replace(old, new)


sub('''RENDER, CLAY, TILE, TIMBER, PLANK, BRICK = \\
    "render_clay", "adobe_clay", "tile_clay", "timber_rough", "wood_weathered", "brick_red"
# Per LOD: the wall panel size, the rafters drawn, whether batten runs, tile patches, the ceiling and the clutter are.
DETAIL = [
    {"panel": (1.25, 0.62), "rafter_step": 1, "battens": True, "boards": True, "clutter": True},
    {"panel": (2.50, 1.25), "rafter_step": 2, "battens": True, "boards": False, "clutter": False},
    {"panel": (99.0, 99.0), "rafter_step": 3, "battens": False, "boards": False, "clutter": False},
]
''', '''FACADE, CLAY, TILE, TIMBER, PLANK, BRICK = \\
    "house_adobe_facade", "adobe_clay", "tile_clay", "timber_rough", "wood_weathered", "brick_red"
# Per LOD: the rafters drawn, and whether batten runs, the ceiling boards and the clutter are.
DETAIL = [
    {"rafter_step": 1, "battens": True, "boards": True, "clutter": True},
    {"rafter_step": 2, "battens": True, "boards": False, "clutter": False},
    {"rafter_step": 3, "battens": False, "boards": False, "clutter": False},
]
# The facade map (buildingkit.Facade): band 0 the outside, walked from the south-west corner eastward, 32 m round;
# band 1 the inside faces of the outer walls and gables, at the same s; band 2 the partition. See `paint` below.
PERIMETER = 2 * (FOOT[0] + FOOT[1])
FACADE_MAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "game", "assets", "textures",
                          "house_adobe", "house_adobe_facade.png")
facade = bk.Facade(PERIMETER, [0.0, 5.5, 11.0, 14.0])
S_SOUTH, S_EAST = FOOT[0] / 2, FOOT[0] + FOOT[1] / 2                    # s = S_SOUTH + x, s = S_EAST - z
S_NORTH, S_WEST = 1.5 * FOOT[0] + FOOT[1], 2 * FOOT[0] + 1.5 * FOOT[1]  # s = S_NORTH - x, s = S_WEST + z
''')

sub('''    rng = random.Random(SEED)
    mesh = kit.Mesh()
    collision = shapes if shapes is not None else []
    render_patch = bk.patchiness(SEED)

    def face(u, v):
        """Bare clay where the render has come off (notes B1)."""
        return CLAY if render_patch(u, v) < -0.12 else RENDER

''', '''    rng = random.Random(SEED)
    mesh = kit.Mesh()
    collision = shapes if shapes is not None else []

''')

sub('''    bk.wall(mesh, collision, "x", long_face, 0.0, FOOT[0], WALL, WALL_H, south, detail["panel"], face)
    bk.wall(mesh, collision, "x", -long_face, 0.0, FOOT[0], WALL, WALL_H, north, detail["panel"], face, uv_seed=3.1)
    bk.wall(mesh, collision, "z", short_face, 0.0, 2 * INSIDE[1], WALL, WALL_H, east, detail["panel"], face, uv_seed=6.7)
    bk.wall(mesh, collision, "z", -short_face, 0.0, 2 * INSIDE[1], WALL, WALL_H, [], detail["panel"], face, uv_seed=1.4)
    # The inner wall, with a doorway between the two rooms.
    bk.wall(mesh, collision, "z", PARTITION[0], 0.0, 2 * INSIDE[1], PARTITION[1], WALL_H,
            [(-0.425, 0.425, 0.0, 1.95)], detail["panel"], lambda u, v: RENDER, uv_seed=8.2)
''', '''    bk.wall(mesh, collision, "x", long_face, 0.0, FOOT[0], WALL, WALL_H, south, FACADE,
            facade=facade.frame("x", long_face, WALL, 1, S_SOUTH))
    bk.wall(mesh, collision, "x", -long_face, 0.0, FOOT[0], WALL, WALL_H, north, FACADE,
            facade=facade.frame("x", -long_face, WALL, -1, S_NORTH))
    bk.wall(mesh, collision, "z", short_face, 0.0, 2 * INSIDE[1], WALL, WALL_H, east, FACADE,
            facade=facade.frame("z", short_face, WALL, 1, S_EAST))
    bk.wall(mesh, collision, "z", -short_face, 0.0, 2 * INSIDE[1], WALL, WALL_H, [], FACADE,
            facade=facade.frame("z", -short_face, WALL, -1, S_WEST))
    # The inner wall, with a doorway between the two rooms: both faces are inside, band 2.
    bk.wall(mesh, collision, "z", PARTITION[0], 0.0, 2 * INSIDE[1], PARTITION[1], WALL_H,
            [(-0.425, 0.425, 0.0, 1.95)], FACADE,
            facade=facade.frame("z", PARTITION[0], PARTITION[1], 1, INSIDE[1] + 0.45, outer_band=2, inner_band=2))
''')

sub('''    for side, top in ((-1, RIDGE_Y), (1, GABLE_BROKEN)):
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
''', '''    for side, top in ((-1, RIDGE_Y), (1, GABLE_BROKEN)):
        courses = 6 if top == RIDGE_Y else 2
        step = (top - WALL_H) / courses
        frame = facade.frame("z", side * (FOOT[0] / 2 - GABLE / 2), GABLE, side, S_EAST if side > 0 else S_WEST)
        for k in range(courses):
            y0, y1 = WALL_H + k * step, WALL_H + (k + 1) * step
            half = gable_half(y1)
            if half < 0.2:
                continue
            frame.add_solid(-half, half, y0, y1)
            collision.append(mesh.box((side * (FOOT[0] / 2 - GABLE / 2), (y0 + y1) / 2, 0.0), (GABLE, y1 - y0, 2 * half),
                                      FACADE, upright=True, uv2=frame))
''')

sub('''            collision.append(mesh.box(on_slope(side, x, along, out), (width, 0.035, height), TILE,
                                      rotation_deg=(0, side * PITCH, 0), uv_offset=(x, along)))''',
    '''            collision.append(mesh.box(on_slope(side, x, along, out), (width, 0.035, height), TILE,
                                      rotation_deg=(0, side * PITCH, 0), upright=True))''')

sub('''        mesh.box((CHIMNEY[0], (y0 + y1) / 2, CHIMNEY[1]), (CHIMNEY[2], y1 - y0, CHIMNEY[2]), BRICK,
                 uv_offset=(0, y0))
    collision.append(mesh.box((CHIMNEY[0] + 0.1, stack + 0.125, CHIMNEY[1]), (0.3, 0.25, CHIMNEY[2]), BRICK,
                              rotation_deg=(6.0, 0, 3.0)))''', '''        mesh.box((CHIMNEY[0], (y0 + y1) / 2, CHIMNEY[1]), (CHIMNEY[2], y1 - y0, CHIMNEY[2]), BRICK, upright=True)
    collision.append(mesh.box((CHIMNEY[0] + 0.1, stack + 0.125, CHIMNEY[1]), (0.3, 0.25, CHIMNEY[2]), BRICK,
                              rotation_deg=(6.0, 0, 3.0), upright=True))''')

sub('''shapes = []
kit.reset()
lods = [build(level, shapes if level == 0 else None) for level in range(3)]
''', '''shapes = []
kit.reset()
lods = [build(level, shapes if level == 0 else None) for level in range(3)]

# The facade (notes B1, and PR-7's research: a clay house plastered with clay and whitewashed with lime, its foot
# band washed with red clay). The fire was in the west room, whose ceiling came down: it vented through that room's
# two windows and blackened the room inside. The shell that took the east gable's apex left a sheet of render off
# that end and fragment pocks across it, and more by the door.
facade.paint(FACADE_MAP, SEED, loss=0.30, inner_loss=0.12,
             corners=(0.0, FOOT[0], FOOT[0] + FOOT[1], PERIMETER - FOOT[1]),
             burnt=((0, S_SOUTH - 3.8, 0.9), (0, S_NORTH + 2.6, 0.75)),
             pocks=((0, S_EAST, 1.9, 1.3, 40), (0, S_SOUTH + DOOR[0] + 0.9, 1.1, 0.7, 14)),
             blasts=((0, S_EAST + 0.6, 2.9, 1.7, 0.9), (0, S_SOUTH + 2.9, 0.6, 0.8, 0.45)),
             band_m=0.42,
             inner_soot=((1, S_SOUTH - INSIDE[0], S_SOUTH + PARTITION[0], 0.8),
                         (1, S_NORTH - PARTITION[0], S_NORTH + INSIDE[0], 0.8),
                         (1, S_WEST - FOOT[1] / 2, PERIMETER, 0.8), (2, 0.0, FOOT[1], 0.6)))
''')

sub('''    RENDER: ("#9f713b", 0.92), CLAY: ("#8a6a42", 0.95), TILE: ("#825f44", 0.78),''',
    '''    FACADE: ("#b8b0a0", 0.92), CLAY: ("#8a6a42", 0.95), TILE: ("#825f44", 0.78),''')
open(path, "w", encoding="utf-8", newline="\n").write(t)
print("ok")
