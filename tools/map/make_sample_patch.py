"""Writes game/maps/sample_patch: the 256 m test patch for the map format, the loader and the world query.

    python tools/map/make_sample_patch.py [OUT_DIR]

A readable test patch built from the reference notes' vocabulary, not final art. +X is east and +Z south.
- North of the tree belt: a tilled field (west, T3a) with straw streaks along its ridges, and a dry cratered field
  (east, T2) with three craters. A 1.2 m spoil bank runs along the belt between them.
- The tree belt (z -64 to -44, T4/T5): lodged straw with a bare strip under the canopy, and a row of tree proxies along
  its south edge.
- The meadow (T1): gentle roll, a crater, a power line on poles and a dirt road (dry crust) to the yard gate.
- The yard (T6): a house, a shed, the gate, household junk and a rubble heap. South of it is an overgrown garden of weeds
  (T7) with a low cable sagging across it (S1).
- The test trench (task 2.3), south-west of the meadow: a straight section and one turned 30 degrees, 0.8 m wide and
  1.5 m deep, cut into a levelled pad. It is the terrain-holes fixture: holes.png marks its cells and four placed
  objects of placeholder boxes fill them (see TRENCH below).
- The village yard (task 3.2), south of the dirt road at the west end: the damaged adobe house of notes B1 with its
  yard set, on its own levelled pad (see YARD). Yard litter over the pad, rubble in and around the house, weeds in
  the far corner, and the cellar entrance, whose stairwell is the second terrain hole on the map.
- Two start points for the launch rails: on the meadow and in the yard.

The golden file (game/src/world/worldquery_golden.json) probes the features that came first at fixed points: the first
ten objects, the belt, the yard, the meadow crater, height() and the start on the meadow. Keep them where they are and
add new content where no probe relies on it, so a re-record changes only hashes and counts.
"""
import json
import math
import os
import random
import sys
from array import array

from mappng import GREY, RGBA, write_png

SIZE, HEIGHT_RES, CELL_RES, SEED = 256, 1.0, 0.5, 20260923
OFFSET, SCALE = -10.0, 0.01
CRATERS = [  # x, z, radius, depth, rim (m); spoil reaches 2 radii out
    (-60.0, 60.0, 2.0, 0.6, 0.2),     # meadow
    (40.0, -100.0, 1.8, 0.8, 0.25),   # dry field
    (78.0, -86.0, 1.2, 0.5, 0.15),
    (104.0, -112.0, 2.0, 1.0, 0.3),
]
BANK = (50.0, 5.0, -70.0, 3.5, 1.2)  # full-height half length, taper, centre z, half width, height (m)
ROAD_Z = (17.0, 22.0)                # the dirt road from the west edge to the gate
# The tree belt (notes V1): 18 m of it, five rows across, with two clearings cut through (x centre, half width).
BELT_Z, BELT_X, BELT_ROWS, BELT_ROW_M, BELT_STEP_M = (-63.0, -45.0), (-118.0, 118.0), 6, 3.0, 3.0
BELT_CLEARINGS, BELT_SHRUBS = ((2.0, 5.0), (-74.0, 4.5)), 620
MEADOW_SOD, DRY_CRUST, CRATER_SPOIL, BELT_STRAW, BELT_BARE, TILLED, YARD_LITTER, RUBBLE, WEEDS = range(1, 10)

# The test trench. Its pad is levelled so that TerrainHeight is one value over the whole trench: the filler is
# flat-topped placeholder boxes, and a flat lip then meets rule F-3 (within 0.03 m of TerrainHeight) exactly. A trench
# on sloping ground needs a shape per step instead; that is for the real trench-section asset (task 3.5).
PAD = (-21.5, 90.25, 11.0, 5.5, 2.0)  # centre x, z, half x, half z, blend width (m)
TRENCH_A = (-27.0, 88.75)             # the straight section's closed end: its asset origin, and the cavity's start
TRENCH_LENGTH = 6.0                   # cavity length of each section, m
TRENCH_BEND_DEG = 30.0                # the second section turns this far, toward +z
TRENCH_HALF = 0.75                    # half width of the hole band: the 0.4 m cavity plus 0.35 m of lip (F-2)
TRENCH_LEAD = 0.5                     # the hole band runs this far past each closed end, so the end wall is inset

# The village yard (task 3.2), on its own levelled pad for the same reason as the trench's: a village yard is graded
# level and the house is built on a level footing, and a flat pad is what lets the cellar's flat-topped lip meet F-3.
YARD = (-56.0, 33.0, 14.0, 10.5, 2.5)   # centre x, z, half x, half z, blend width (m)
HOUSE = (-58.0, 30.0, 5.0, 3.0)         # the adobe house: centre x, z and half footprint (m). Set back from the
                                        # fence line so a drone can line up on the door and on the far window.
FENCE_Z = 23.2                          # the road-side fence and gate line (m)
FENCE_X = (-68.7, -66.2, -63.7, -58.7, -53.4, -48.4, -43.4)   # surviving fence bays (notes B6: fence remains)
SHED, GATE_X = (-65.0, 38.0, -14.0), -56.0
FRUIT = ((-57.0, 38.0, 20.0), (-46.5, 29.0, 200.0))           # notes V3: one or two per yard
# The cellar entrance. Its origin must put the hole band on surface-cell edges, so both x and z are a multiple of 0.5
# plus 0.25 (blender/structures/cellar_shaft.py); the band is then exactly 3 x 7 cells, centred on the origin.
CELLAR = (-48.75, 36.25)
CELLAR_BAND = (1.5, 3.5)                # the hole band: width across and length along, both about the origin (m)


def smoothstep(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


def trench_axes():
    """The two cavity axes as (origin, yaw_deg, direction, far end): the asset's local +X runs from the closed end."""
    bend = math.radians(TRENCH_BEND_DEG)
    a_dir = (1.0, 0.0)
    b_dir = (math.cos(bend), math.sin(bend))
    p0 = TRENCH_A
    p1 = (p0[0] + TRENCH_LENGTH * a_dir[0], p0[1] + TRENCH_LENGTH * a_dir[1])
    p2 = (p1[0] + TRENCH_LENGTH * b_dir[0], p1[1] + TRENCH_LENGTH * b_dir[1])
    # Section B is placed at its own closed end, P2, running back toward the corner: yaw turns +X toward -Z.
    return [(p0, 0.0, a_dir, p1), (p2, 180.0 - TRENCH_BEND_DEG, (-b_dir[0], -b_dir[1]), p1)]


def trench_strips():
    """Each section's hole footprint as a segment: from TRENCH_LEAD before the closed end to the corner."""
    strips = []
    for origin, _, direction, far in trench_axes():
        strips.append(((origin[0] - TRENCH_LEAD * direction[0], origin[1] - TRENCH_LEAD * direction[1]), far))
    return strips


def segment_distance(px, pz, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    length2 = dx * dx + dz * dz
    t = 0.0 if length2 == 0 else min(max(((px - a[0]) * dx + (pz - a[1]) * dz) / length2, 0.0), 1.0)
    return math.hypot(px - (a[0] + t * dx), pz - (a[1] + t * dz))


def cellar_band():
    """The cellar's hole band as (x0, z0, x1, z1): cell-aligned, so its cells are exactly the band."""
    return (CELLAR[0] - CELLAR_BAND[0] / 2, CELLAR[1] - CELLAR_BAND[1] / 2,
            CELLAR[0] + CELLAR_BAND[0] / 2, CELLAR[1] + CELLAR_BAND[1] / 2)


def is_hole(x0, z0, x1, z1):
    """Whether the cell [x0, x1] x [z0, z1] meets a trench strip or the cellar band, so that the hole cells cover the
    whole of each. The cellar test is strict, so a cell that only touches the band's edge stays ground."""
    bx0, bz0, bx1, bz1 = cellar_band()
    if x0 < bx1 - 1e-9 and x1 > bx0 + 1e-9 and z0 < bz1 - 1e-9 and z1 > bz0 + 1e-9:
        return True
    for a, b in trench_strips():
        steps = max(2, int(math.hypot(b[0] - a[0], b[1] - a[1]) / 0.01))
        for i in range(steps + 1):
            t = i / steps
            px, pz = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
            near_x = min(max(px, x0), x1)
            near_z = min(max(pz, z0), z1)
            if math.hypot(px - near_x, pz - near_z) <= TRENCH_HALF:
                return True
    return False


def pad_weight(pad, x, z):
    cx, cz, hx, hz, blend = pad
    return smoothstep((hx + blend - abs(x - cx)) / blend) * smoothstep((hz + blend - abs(z - cz)) / blend)


def bank(x, z):
    """Height of the spoil bank: a cos² profile across it, tapering off at its ends."""
    length, taper, cz, half, top = BANK
    d = abs(z - cz)
    if d >= half:
        return 0.0
    return top * math.cos(math.pi / 2 * d / half) ** 2 * smoothstep((length + taper - abs(x)) / taper)


def relief(x, z):
    h = 0.012 * x - 0.006 * z + 0.8 * math.sin(2 * math.pi * x / 180) * math.cos(2 * math.pi * z / 150) + bank(x, z)
    for cx, cz, radius, depth, rim in CRATERS:
        q = math.hypot(x - cx, z - cz) / radius
        if q < 1:
            h += -depth + (depth + rim) * q * q
        elif q < 2:
            h += rim * (2 - q) ** 2
    return h


def sample(h):
    """A height on the r16 grid, so a levelled pad reads back as one exact value."""
    return round(OFFSET + round((h - OFFSET) / SCALE) * SCALE, 6)


PAD_HEIGHT = sample(relief(PAD[0], PAD[1]))
YARD_HEIGHT = sample(relief(YARD[0], YARD[1]))
LEVELLED = ((PAD, PAD_HEIGHT), (YARD, YARD_HEIGHT))   # the pads do not overlap, so they blend one after the other


def height(x, z):
    h = relief(x, z)
    for pad, level in LEVELLED:
        w = pad_weight(pad, x, z)
        if w > 0:
            h = h * (1 - w) + level * w
    return h


def surface(x, z):
    if any(math.hypot(x - cx, z - cz) <= 2 * r for cx, cz, r, _, _ in CRATERS):
        return CRATER_SPOIL
    if -64 <= z <= -44:
        return BELT_BARE if abs(z + 54) <= 3 else BELT_STRAW
    if z < -64:
        if bank(x, z) > 0.02:
            return CRATER_SPOIL
        return TILLED if x < 0 else DRY_CRUST
    if 60 <= x <= 68 and 36 <= z <= 44:
        return RUBBLE
    if 30 <= x <= 74 and 8 <= z <= 52:
        return YARD_LITTER
    if 30 <= x <= 74 and 52 < z <= 78:
        return WEEDS
    # The village yard: beaten litter over the pad, rubble in and around the house, weeds in the far south-west corner.
    if abs(x - YARD[0]) <= YARD[2] and abs(z - YARD[1]) <= YARD[3]:
        if abs(x - HOUSE[0]) <= HOUSE[2] + 0.8 and abs(z - HOUSE[1]) <= HOUSE[3] + 0.8:
            return RUBBLE
        return WEEDS if x < YARD[0] - 8.5 and z > YARD[1] + 4.5 else YARD_LITTER
    if ROAD_Z[0] <= z <= ROAD_Z[1] and x < 30:
        return DRY_CRUST
    return MEADOW_SOD


def cover(kind, x, z):
    """RGBA multipliers: R grass, G lodged straw, B twigs, A leaf litter."""
    if kind == MEADOW_SOD:
        return (int(200 + 55 * (0.5 + 0.5 * math.sin(x / 7) * math.cos(z / 9))), 0, 0, 0)
    if kind == DRY_CRUST:  # sparse dead tufts, almost none on the beaten road
        on_road = ROAD_Z[0] <= z <= ROAD_Z[1]
        return (25 if on_road else int(150 + 105 * (0.5 + 0.5 * math.sin(x / 5) * math.cos(z / 6))), 0, 0, 0)
    if kind == TILLED:  # straw residue in streaks along the ridges, 6 m apart
        return (0, int(40 + 215 * (0.5 + 0.5 * math.cos(2 * math.pi * z / 6)) ** 2), 0, 0)
    if kind == WEEDS:
        return (int(200 + 55 * (0.5 + 0.5 * math.sin(x / 3) * math.cos(z / 4))), 0, 0, 0)
    return {BELT_STRAW: (160, 255, 120, 0), BELT_BARE: (0, 0, 255, 180), YARD_LITTER: (0, 0, 90, 255),
            RUBBLE: (0, 0, 255, 0), CRATER_SPOIL: (0, 0, 0, 0)}[kind]


def hole_cells(cells, half):
    """The hole cells as a set of (row, column), scanned over each hole's own bounding box only."""
    reach = TRENCH_HALF + CELL_RES
    xs = [p[0] for strip in trench_strips() for p in strip]
    zs = [p[1] for strip in trench_strips() for p in strip]
    band = cellar_band()
    boxes = [(min(xs) - reach, min(zs) - reach, max(xs) + reach, max(zs) + reach),
             (band[0] - CELL_RES, band[1] - CELL_RES, band[2] + CELL_RES, band[3] + CELL_RES)]
    marked = set()
    for bx0, bz0, bx1, bz1 in boxes:
        c0 = max(math.floor((bx0 + half) / CELL_RES), 0)
        c1 = min(math.floor((bx1 + half) / CELL_RES) + 1, cells - 1)
        r0 = max(math.floor((bz0 + half) / CELL_RES), 0)
        r1 = min(math.floor((bz1 + half) / CELL_RES) + 1, cells - 1)
        for r in range(r0, r1 + 1):
            for c in range(c0, c1 + 1):
                x0, z0 = -half + c * CELL_RES, -half + r * CELL_RES
                if is_hole(x0, z0, x0 + CELL_RES, z0 + CELL_RES):
                    marked.add((r, c))
    return marked


def trench_objects():
    """One section and one end cap per placement, at the pad height, and the wedge outside the bend.

    Each section's sides stop at the corner, so neither blocks the other's cavity; their floors run 2 m past it, so the
    ground there is still sealed. The wedge between the two sides' ends closes the outside of the turn.
    """
    objects = []
    for (x, z), yaw, _, _ in trench_axes():
        for asset in ("test_trench", "test_trench_cap"):
            objects.append({"asset": asset, "position_m": [x, PAD_HEIGHT, z], "rotation_deg": [yaw, 0.0, 0.0], "scale": 1.0})
    corner = (TRENCH_A[0] + TRENCH_LENGTH, TRENCH_A[1])
    objects.append({"asset": "test_trench_corner", "position_m": [corner[0], PAD_HEIGHT, corner[1]],
                    "rotation_deg": [90.0 - TRENCH_BEND_DEG / 2, 0.0, 0.0], "scale": 1.0})
    return objects


def yard_objects():
    """The village yard of task 3.2 (notes B1, B3, B6, V3, W4): the adobe house with its door facing south into the
    yard, the plank shed, the cellar entrance (head and shaft at one pose), two fruit trees, and what is left of the
    road-side fence with its gate. The cellar's shaft is the hole filler for the cells `holes.png` marks."""
    objects = [placed("house_adobe", HOUSE[0], HOUSE[1]),
               placed("shed_plank", SHED[0], SHED[1], SHED[2]),
               placed("cellar_head", CELLAR[0], CELLAR[1]),
               placed("cellar_shaft", CELLAR[0], CELLAR[1]),
               placed("gate_plank", GATE_X, FENCE_Z)]
    objects += [placed("tree_fruit", x, z, yaw) for x, z, yaw in FRUIT]
    objects += [placed("fence_planks", x, FENCE_Z) for x in FENCE_X]
    return objects


def placed(asset, x, z, yaw=0.0):
    return {"asset": asset, "position_m": [x, round(height(x, z), 3), z], "rotation_deg": [yaw, 0.0, 0.0], "scale": 1.0}


def at(p, up, dx=0.0, dz=0.0):
    """A point `up` m above an object's origin, moved dx and dz in the world."""
    x, y, z = p["position_m"]
    return [round(x + dx, 3), round(y + up, 3), round(z + dz, 3)]


def belt(placed_first):
    """The tree belt (notes V1): rows of trees across the belt band with a shrub understory between them.

    V1 puts trunks 1-3 m apart in a belt 10-30 m wide. This one is 18 m wide in five rows about 3.6 m apart, with
    trunks about 3.8 m along a row: a belt thinned by shelling (notes B, "thinned and broken"), which is also what
    keeps it inside the frame budget. Two clearings are left open (V1's `gap`: clearings inside the belt), so the
    drone has somewhere to cross the belt other than over its crowns.

    The species mix follows V1: planted acacia and ash through the middle, self-seeded box elder and young broken
    trees at the margins where the light gets in, and a dead tree here and there (V2).
    """
    rng = random.Random(SEED)
    fixed = [(t["position_m"][0], t["position_m"][2]) for t in placed_first]  # the golden file's three trees
    objects, trunks = [], []
    for row in range(BELT_ROWS):
        z0 = BELT_Z[0] + BELT_ROW_M * (row + 0.5)
        edge = min(row, BELT_ROWS - 1 - row) == 0  # the two outer rows get the light, and the scrub
        x = BELT_X[0] + rng.uniform(0, BELT_STEP_M)
        while x < BELT_X[1]:
            x += BELT_STEP_M * rng.uniform(0.62, 1.38)
            z = z0 + rng.uniform(-BELT_ROW_M, BELT_ROW_M) / 3
            if any(abs(x - cx) < half for cx, half in BELT_CLEARINGS):
                continue
            if any(math.hypot(x - tx, z - tz) < 7.0 for tx, tz in fixed)                     or any(math.hypot(x - tx, z - tz) < 1.9 for tx, tz in trunks):
                continue
            roll = rng.random()
            if roll < (0.10 if edge else 0.04):
                asset = "tree_dead"
            elif edge:
                asset = "tree_young" if roll < 0.55 else "tree_maple"
            else:
                asset = "tree_acacia" if roll < 0.62 else "tree_ash" if roll < 0.9 else "tree_maple"
            trunks.append((x, z))
            objects.append(scaled(placed(asset, round(x, 2), round(z, 2), round(rng.uniform(0, 360), 1)),
                                  round(rng.uniform(0.84, 1.16), 3)))
    # The understory. V1 puts it through the whole belt, and it is thickest at the two edges where the light gets in;
    # it is also what stops the drone seeing straight under the canopy and out the other side.
    for _ in range(BELT_SHRUBS):
        x = rng.uniform(*BELT_X)
        z = rng.choice(BELT_Z) + rng.uniform(0, BELT_ROW_M * 1.3) * (1 if rng.random() < 0.5 else -1) \
            if rng.random() < 0.45 else rng.uniform(*BELT_Z)
        if not BELT_Z[0] <= z <= BELT_Z[1] or any(abs(x - cx) < half for cx, half in BELT_CLEARINGS):
            continue
        if any(math.hypot(x - tx, z - tz) < 1.2 for tx, tz in trunks + fixed):
            continue
        objects.append(scaled(placed("shrub_belt", round(x, 2), round(z, 2), round(rng.uniform(0, 360), 1)),
                              round(rng.uniform(0.7, 1.35), 3)))
    return objects


def scaled(object, scale):
    object["scale"] = scale
    return object


def objects():
    # The first ten are the golden file's: keep them and their order (indices).
    pole_a, pole_b = placed("pole", -30.0, 0.0), placed("pole", 0.0, 0.0)
    shed = placed("shed_box", 38.0, 44.0, -10.0)
    first = [
        placed("house_box", 52.0, 30.0, 15.0),
        shed,
        placed("gate_frame", 30.5, 20.0, 90.0),
        placed("household_junk", 58.0, 38.0, 30.0),
        placed("tree_acacia", -40.0, -50.0),
        placed("tree_ash", -10.0, -56.0),
        placed("tree_maple", 20.0, -48.0),
        pole_a,
        pole_b,
        {"asset": "cable", "points_m": [at(p, 7.8) for p in (pole_a, pole_b)], "sag_m": 0.6, "diameter_m": 0.012},
    ]
    trees = belt(first[4:7])
    # The power line goes on west from pole_a.
    line = [placed("pole", x, 0.0) for x in (-60.0, -90.0, -120.0)]
    # Shed east wall, 2.2 m up: the shed is 4 m wide, turned -10 degrees.
    wall = at(shed, 2.2, 2.0 * math.cos(math.radians(10)), 2.0 * math.sin(math.radians(10)))
    garden_pole = placed("pole", 66.0, 72.0)
    # The trench fillers and then the yard come last, so every index before them stays where the golden file has it.
    return first + trees + line + [
        {"asset": "cable", "points_m": [at(p, 7.8) for p in [pole_a] + line], "sag_m": 0.6, "diameter_m": 0.012},
        placed("household_junk", 44.0, 14.0, 70.0),
        placed("household_junk", 35.0, 30.0, 110.0),
        placed("household_junk", 68.0, 48.0, -20.0),
        garden_pole,
        {"asset": "cable", "points_m": [wall, at(garden_pole, 2.0)], "sag_m": 1.0, "diameter_m": 0.01},
    ] + trench_objects() + yard_objects()


def start(x, z, yaw):
    return {"position_m": [x, round(height(x, z), 2), z], "yaw_deg": yaw}


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "game", "maps", "sample_patch")
    os.makedirs(out, exist_ok=True)
    samples, cells, half = int(SIZE / HEIGHT_RES) + 1, int(SIZE / CELL_RES), SIZE / 2

    with open(os.path.join(out, "height.r16"), "wb") as f:
        for r in range(samples):
            row = array("H", [round((height(-half + c * HEIGHT_RES, -half + r * HEIGHT_RES) - OFFSET) / SCALE) for c in range(samples)])
            if sys.byteorder == "big":
                row.byteswap()
            f.write(row.tobytes())

    # A hole cell is bare cut soil with no cover, so the mat and relief taper toward the trench instead of stepping at it.
    holes = hole_cells(cells, half)
    kinds, covers, hole_rows = [], [], []
    for r in range(cells):
        z = -half + (r + 0.5) * CELL_RES
        row_kinds = [BELT_BARE if (r, c) in holes else surface(-half + (c + 0.5) * CELL_RES, z) for c in range(cells)]
        kinds.append(bytes(row_kinds))
        covers.append(bytes(v for c, k in enumerate(row_kinds)
                            for v in ((0, 0, 0, 0) if (r, c) in holes else cover(k, -half + (c + 0.5) * CELL_RES, z))))
        hole_rows.append(bytes(255 if (r, c) in holes else 0 for c in range(cells)))
    write_png(os.path.join(out, "surface.png"), cells, cells, GREY, kinds)
    write_png(os.path.join(out, "cover.png"), cells, cells, RGBA, covers)
    write_png(os.path.join(out, "holes.png"), cells, cells, GREY, hole_rows)
    for layer in ("surface.png", "cover.png", "holes.png"):
        with open(os.path.join(out, layer + ".import"), "w", newline="\n") as f:
            f.write('[remap]\n\nimporter="keep"\n')

    with open(os.path.join(out, "objects.json"), "w", newline="\n") as f:
        json.dump({"objects": objects()}, f, indent=2)
        f.write("\n")

    manifest = {
        "format_version": "1.1",  # holes.png
        "size_m": SIZE,
        "seed": SEED,
        "height": {"resolution_m": HEIGHT_RES, "samples_per_side": samples, "offset_m": OFFSET, "scale_m": SCALE},
        "surface": {"resolution_m": CELL_RES, "cells_per_side": cells},
        # The golden file's launch rails stand on the first start.
        "starts": [start(12.0, -30.0, 90.0), start(66.0, 20.0, 0.0)],
    }
    with open(os.path.join(out, "map.json"), "w", newline="\n") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"wrote {os.path.normpath(out)}")


if __name__ == "__main__":
    main()
