"""Writes game/maps/uat1_gallery: the UAT-1 review gallery (build-uat1-parts task 4.1, uat1-gallery spec).

    python tools/map/make_uat1_gallery.py [OUT_DIR]

A 512 m noclip map where the pilot inspects every terrain kind and every asset before the world is built from them.
What is under review comes from two lists, PATCHES and STATIONS below, so the pilot's feedback is an edit to them and
a re-run:
- PATCHES: the terrain kinds, each at least 60 m across, around the edge. The relief patch (pilot requirement PR-8) is
  one of them, with a tree-belt run and the poles-and-wires station standing on it.
- STATIONS: the assets, along two roads that cross at the map centre, each with room to orbit and fly through.
A station is built when the catalog has every asset it names. Otherwise it is reserved: its ground is laid, nothing is
placed (the loader rejects unknown asset ids) and its label says "not built yet". Finishing an asset puts it in its
station at the next run. Asset ids for work not started yet (tasks 3.3-3.5) are proposals: the builder either uses
them or edits the station.

No surface area is a rectangle (pilot, 2026-09-30: the sample's hard-edged areas read as flat tiles from the air).
Every patch and station ground has an outline that wanders (`outline`), and a feathered band along it where its cells
thin out into what is around them: in the surface layer by a per-cell dither, in the cover layer by densities that
fade across the band. Roads are straight with ragged verges.

The map edge is ringed by a two-row tree belt on a 4 m rise, so from low and from the opening view the eye stops at
trees and not at the end of the terrain.

The noclip camera opens 15 m above the map centre (the sandbox), which is the crossroads, looking north up the road
with the destroyed vehicles on the right and the rolling meadow with its power line on the left. The one start point
(for flights, once the game has them) is the crossroads itself.

labels.json holds the name, state and area of every patch and station (game/maps/README.md, labels.json), read by
game/src/world/MapLabels.cs. The same seed always gives the same bytes.
"""
import json
import math
import os
import random
import sys
from array import array
from collections import namedtuple

from mappng import GREY, RGBA, write_png

SIZE, HEIGHT_RES, CELL_RES, SEED = 512, 1.0, 0.5, 20260930
OFFSET, SCALE = -10.0, 0.01
HALF = SIZE / 2
TAU = 2 * math.pi
MEADOW_SOD, DRY_CRUST, CRATER_SPOIL, BELT_STRAW, BELT_BARE, TILLED, YARD_LITTER, RUBBLE, WEEDS, BURNT_FIELD = range(1, 11)
HERE = os.path.dirname(os.path.abspath(__file__))
CATALOG = os.path.join(HERE, "..", "..", "game", "assets", "catalog.json")

# ---------------------------------------------------------------- what is under review

# The terrain patches: kind (which painter below draws it), label name, label state, area (x0, z0, x1, z1) before its
# outline wanders, and how tall what stands on it is (m, for the label's reach). +X is east and +Z south.
PATCHES = [
    ("tilled", "Tilled field", "loose furrowed loam, straw residue along the furrows", (-234, -234, -85, -150), 0),
    ("trench", "Trench along a tree strip", "0.8 m wide, 1.5 m deep, by a dark field; placeholder walls until task 3.5",
     (-80, -234, 80, -150), 12),
    ("burnt", "Burnt field", "char and ash over firm loam, charred stubble, unburnt islands", (85, -234, 234, -150), 0),
    ("craters", "Dry cratered field", "sun-baked crust, shell craters with loose rims, a spoil bank",
     (150, -145, 234, -5), 8),
    ("meadow", "Open meadow", "sod and mixed grass, small scorch spots", (150, 5, 234, 145), 0),
    ("weeds", "Tall weeds", "an overgrown plot with saplings, a dead tree and a low sagging cable",
     (85, 150, 234, 234), 8),
    ("yard", "Yard ground with litter", "compacted soil under leaves and bits, fruit trees, fence remains",
     (-80, 150, 80, 234), 7),
    ("rubble", "Rubble", "brick, tile and timber debris in low heaps over trampled ground", (-234, 150, -85, 234), 0),
    ("belt", "Tree-belt edge", "lodged straw over dark soil, bushes among the trees", (-234, 5, -150, 145), 13),
    ("relief", "Rolling meadow", "a crest and dips, about 3 m from crest to dip, a belt and a power line on it",
     (-234, -145, -20, -20), 13),
]

# The village yard of task 3.2 about its centre, as make_sample_patch.py lays it out (notes B1, B3, B6, V3, W4): the
# house with its door to the yard, the shed, the cellar (head and shaft at one pose), two fruit trees, and the road-side
# fence with its gate. Placements are (asset, dx, dz, yaw) or (asset, dx, dz, yaw, pitch) in the station's frame.
ADOBE_YARD = [
    ("house_adobe", -2.0, -3.0, 0.0),
    ("shed_plank", -9.0, 5.0, -14.0),
    ("cellar_head", 7.25, 3.25, 0.0),
    ("cellar_shaft", 7.25, 3.25, 0.0),
    ("gate_plank", 0.0, -9.8, 0.0),
    ("tree_fruit", -1.0, 5.0, 20.0),
    ("tree_fruit", 9.5, -4.0, 200.0),
] + [("fence_planks", dx, -9.8, 0.0) for dx in (-12.7, -10.2, -7.7, -2.7, 2.6, 7.6, 12.6)]
# Five poles 30 m apart over the rolling meadow, the fourth leaning 12° across the line (notes S2: 5-15°), carrying one
# wire 7.8 m up each pole.
POLE_LINE = [("pole", dx, 0.0, 0.0, 12.0 if dx == 30.0 else 0.0) for dx in (-60.0, -30.0, 0.0, 30.0, 60.0)]

Station = namedtuple("Station", "id name state centre yaw half ground top placements wires")
Station.__new__.__defaults__ = ((),)
# The stations: id, label name, label state, centre (x, z), yaw (degrees, as object yaw), half footprint (x, z) in its
# own frame, ground (see GROUNDS), how tall its contents stand (m), placements, and wires as (asset, the placements
# they hang from, attachment height up each one's axis, sag, diameter). The yard's yaw must stay a multiple of 90° and
# its centre on the 0.5 m grid, so the cellar's hole band lands on surface-cell edges (blender/structures/cellar_shaft.py).
STATIONS = [
    # The village, south of the west road: the yard's fence line runs along the road.
    Station("adobe_house", "Adobe village house and yard", "damaged, with its cellar, shed, fence, gate and fruit trees",
            (-56.0, 16.0), 0.0, (16.0, 12.0), "yard", 7.0, ADOBE_YARD),
    Station("brick_house", "Brick village house and yard", "damaged, asbestos roof partly collapsed",
            (-112.0, 16.0), 0.0, (16.0, 12.0), "yard", 7.0, [("house_brick", 0.0, -3.0, 0.0)]),
    # The urban edge, north of the east road: the block's long face to the road, its rubble spilling toward it.
    Station("block", "Five-storey block and rubble mound", "destroyed: facade torn off, burnt out",
            (100.0, -48.0), 0.0, (44.0, 30.0), "block_site", 22.0,
            [("block_5storey", 0.0, -8.0, 0.0), ("rubble_mound", 0.0, 10.0, 0.0)]),
    # Destroyed vehicles along the north road, abandoned ones along the south and east roads (notes R0: destroyed ones
    # stand on roads and scorched ground, abandoned ones at roadsides, overgrown).
    Station("vaz_destroyed", "ВАЗ classic (2101-07)", "destroyed by a strike", (24.0, -28.0), 100.0, (11.0, 11.0),
            "scorch", 1.5, [("vaz_classic_destroyed", 0.0, 0.0, 0.0)]),
    Station("lanos_destroyed", "Lanos-class car", "destroyed by a strike", (24.0, -58.0), 80.0, (11.0, 11.0),
            "scorch", 1.5, [("lanos_destroyed", 0.0, 0.0, 0.0)]),
    Station("ural_destroyed", "Урал-4320", "destroyed by a strike", (26.0, -90.0), 95.0, (13.0, 13.0),
            "scorch", 3.0, [("ural_4320_destroyed", 0.0, 0.0, 0.0)]),
    Station("zil_destroyed", "ЗИЛ-130", "destroyed by a strike", (26.0, -122.0), 70.0, (13.0, 13.0),
            "scorch", 3.0, [("zil_130_destroyed", 0.0, 0.0, 0.0)]),
    Station("vaz_abandoned", "ВАЗ classic (2101-07)", "abandoned", (24.0, 30.0), 85.0, (11.0, 11.0),
            "overgrown", 1.5, [("vaz_classic_abandoned", 0.0, 0.0, 0.0)]),
    Station("lanos_abandoned", "Lanos-class car", "abandoned", (24.0, 62.0), 100.0, (11.0, 11.0),
            "overgrown", 1.5, [("lanos_abandoned", 0.0, 0.0, 0.0)]),
    Station("ural_abandoned", "Урал-4320", "abandoned", (64.0, 26.0), 10.0, (13.0, 13.0),
            "overgrown", 3.0, [("ural_4320_abandoned", 0.0, 0.0, 0.0)]),
    Station("zil_abandoned", "ЗИЛ-130", "abandoned", (104.0, 26.0), -8.0, (13.0, 13.0),
            "overgrown", 3.0, [("zil_130_abandoned", 0.0, 0.0, 0.0)]),
    Station("debris", "Debris and rubble kit", "bricks, tile shards, planks, sheets, tarp, branches", (24.0, 96.0),
            0.0, (11.0, 11.0), "plain", 1.5, [("debris_kit", 0.0, 0.0, 0.0)]),
    Station("roof_sheet", "Corrugated roof sheet", "fallen, bent", (24.0, 124.0), 20.0, (7.0, 7.0),
            "plain", 0.5, [("roof_sheet_corrugated", 0.0, 0.0, 0.0)]),
    # The belt's trees one by one along the south road, 20 m apart so each can be orbited.
    Station("tree_ash", "Ash", "tree-belt tree", (-22.0, 40.0), 0.0, (7.0, 7.0), "plain", 13.0,
            [("tree_ash", 0.0, 0.0, 0.0)]),
    Station("tree_acacia", "Acacia (black locust)", "tree-belt tree", (-22.0, 60.0), 0.0, (7.0, 7.0), "plain", 13.0,
            [("tree_acacia", 0.0, 0.0, 0.0)]),
    Station("tree_maple", "Maple (box elder)", "tree-belt tree", (-22.0, 80.0), 0.0, (7.0, 7.0), "plain", 13.0,
            [("tree_maple", 0.0, 0.0, 0.0)]),
    Station("tree_young", "Young tree", "self-seeded, broken", (-22.0, 100.0), 0.0, (6.0, 6.0), "plain", 9.0,
            [("tree_young", 0.0, 0.0, 0.0)]),
    Station("shrub_belt", "Shrub", "tree-belt understory", (-22.0, 120.0), 0.0, (5.0, 5.0), "plain", 3.5,
            [("shrub_belt", 0.0, 0.0, 0.0)]),
    Station("tree_dead", "Dead tree", "bare, broken top", (-22.0, 140.0), 0.0, (6.0, 6.0), "plain", 10.0,
            [("tree_dead", 0.0, 0.0, 0.0)]),
    # The power line runs from the crossroads north-west over the rolling meadow (PR-8's station on relief).
    Station("poles", "Poles and wires", "over the rolling meadow, one pole leaning; placeholder poles until task 3.5",
            (-66.0, -66.0), -45.0, (64.0, 5.0), "plain", 8.0, POLE_LINE, [("cable", (0, 1, 2, 3, 4), 7.8, 0.6, 0.012)]),
    Station("rails", "Launch rails", "the legs-off take-off stand", (-12.0, 12.0), 0.0, (3.0, 3.0), "plain", 0.3,
            [("launch_rails", 0.0, 0.0, 0.0)]),
]

# ---------------------------------------------------------------- layout constants

ROADS = [(-2.0, -150.0, 2.0, 150.0), (-150.0, -2.0, 150.0, 2.0)]  # the north-south and the east-west road, 4 m wide
RIM = (4.0, 50.0)          # the edge rise: its height and width (m)
EDGE_FLOOR = 17.0          # the lodged-straw floor of the edge belt reaches this far in from the map edge (m)
EDGE_BELT = (6.5, 12.5)    # the edge belt's two rows stand between these distances from the map edge (m)
ROW_M, STEP_M = 3.0, 3.0   # tree-belt rows and trunk spacing: sample_patch's, which the pilot kept (2026-09-30)
# Buildings that shed rubble at their wall foot, with their half footprint (m): a ring of rubble surface round them.
RUBBLE_AROUND = {"house_adobe": (5.0, 3.0)}
# The trench (notes W2), built from the test trench's placeholder pieces (task 2.3): run A of 6 m sections from its
# closed end, a 30° turn away from the strip (the corner piece is built for 30°), run B to its far closed end. It is cut
# into a levelled strip, because the pieces are flat-topped and a level lip is how they meet F-3; the spoil parapets
# rise beyond that strip, so TerrainHeight is the lip level over every hole cell.
TRENCH_START, TRENCH_RUNS, TRENCH_BEND = (-62.0, -191.0), (8, 3), 30.0
TRENCH_SECTION, TRENCH_HALF, TRENCH_LEAD = 6.0, 0.75, 0.5   # cavity length of a piece, hole band half width, lead (m)
TRENCH_FLAT, PARAPET = 3.0, (4.2, 1.0, 0.4)                 # the level strip's half width; parapet centre, half width, height
TRENCH_STRIP = (-72.0, -212.0, 72.0, -200.0)                # the narrow tree strip north of it: four rows
CELLAR_BAND = (0.75, 1.75)  # the cellar shaft's hole band about its origin: half width across, half length along (m)


def smoothstep(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


_rng = random.Random(SEED)
# Per outline: three sine phases and a turn, so neighbouring outlines never wander in step.
_PHASES = [(_rng.uniform(0, TAU), _rng.uniform(0, TAU), _rng.uniform(0, TAU), math.cos(t), math.sin(t))
           for t in [_rng.uniform(0, TAU) for _ in range(256)]]


def wobble(x, z, k, wavelength):
    """Smooth wandering in about -1..1 for outline k: three sines at unrelated angles and wavelengths."""
    p0, p1, p2, ca, sa = _PHASES[k % len(_PHASES)]
    u, v = (x * ca + z * sa) / wavelength, (z * ca - x * sa) / wavelength
    return (0.5 * math.sin(TAU * u + p0) + 0.3 * math.sin(TAU * (1.39 * u + 1.86 * v) + p1)
            + 0.2 * math.sin(TAU * (-1.33 * u + 4.55 * v) + p2))


def outline(sd, x, z, k, amp, wavelength, feather):
    """How far inside a wandering outline a point is: 0.5 on it, 1 from `feather`/2 inside, 0 from `feather`/2 out.
    `sd` is the signed distance to the shape the outline wanders about (negative inside); `amp` is how far it wanders."""
    return smoothstep(0.5 - (sd + amp * wobble(x, z, k, wavelength)) / feather)


def rect_sd(x, z, rect):
    x0, z0, x1, z1 = rect
    dx, dz = max(x0 - x, x - x1), max(z0 - z, z - z1)
    return max(dx, dz) if dx <= 0 and dz <= 0 else math.hypot(max(dx, 0.0), max(dz, 0.0))


def ellipse_sd(x, z, cx, cz, rx, rz):
    """An approximate signed distance to an ellipse: the radial one, scaled to the shorter radius."""
    return (math.hypot((x - cx) / rx, (z - cz) / rz) - 1) * min(rx, rz)


def segment_distance(px, pz, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    length2 = dx * dx + dz * dz
    t = 0.0 if length2 == 0 else min(max(((px - a[0]) * dx + (pz - a[1]) * dz) / length2, 0.0), 1.0)
    return math.hypot(px - (a[0] + t * dx), pz - (a[1] + t * dz))


def to_world(centre, yaw, dx, dz):
    """A point of a frame at `centre` turned `yaw` degrees (object yaw: +X turns toward -Z) in world x, z."""
    c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    return centre[0] + dx * c + dz * s, centre[1] - dx * s + dz * c


def sample(h):
    """A height on the r16 grid, so a levelled pad reads back as one exact value."""
    return round(OFFSET + round((h - OFFSET) / SCALE) * SCALE, 6)


def patch(kind):
    return next(p for p in PATCHES if p[0] == kind)


def is_built(station, catalog):
    return all(p[0] in catalog for p in station.placements) and all(w[0] in catalog for w in station.wires)


# ---------------------------------------------------------------- the trench

def trench_axes():
    """Each run as (closed end, yaw, direction from the closed end, far end at the corner, sections)."""
    bend = math.radians(TRENCH_BEND)
    a_dir, b_dir = (1.0, 0.0), (math.cos(bend), math.sin(bend))
    p0 = TRENCH_START
    corner = (p0[0] + TRENCH_SECTION * TRENCH_RUNS[0] * a_dir[0], p0[1] + TRENCH_SECTION * TRENCH_RUNS[0] * a_dir[1])
    p2 = (corner[0] + TRENCH_SECTION * TRENCH_RUNS[1] * b_dir[0], corner[1] + TRENCH_SECTION * TRENCH_RUNS[1] * b_dir[1])
    # Run B is placed from its own closed end, P2, back toward the corner, so its walls stop at the corner as run A's do.
    return [(p0, 0.0, a_dir, corner, TRENCH_RUNS[0]), (p2, 180.0 - TRENCH_BEND, (-b_dir[0], -b_dir[1]), corner, TRENCH_RUNS[1])]


def trench_strips():
    """Each run's hole footprint as a segment: from TRENCH_LEAD before its closed end to the corner."""
    return [((end[0] - TRENCH_LEAD * d[0], end[1] - TRENCH_LEAD * d[1]), far) for end, _, d, far, _ in trench_axes()]


def trench_distance(x, z, extend=0.0):
    """Distance to the trench's centre line, its closed ends pushed out by `extend`."""
    best = math.inf
    for end, _, d, far, _ in trench_axes():
        best = min(best, segment_distance(x, z, (end[0] - extend * d[0], end[1] - extend * d[1]), far))
    return best


TRENCH_LEVEL = sample(0.0)  # replaced below, once terrain() exists


def parapets(x, z):
    """The spoil parapets beside the trench (notes W2: 0.3-0.5 m), outside its level strip."""
    if not (-80 <= x <= 20 and -205 <= z <= -165):
        return 0.0
    q = abs(trench_distance(x, z) - PARAPET[0]) / PARAPET[1]
    return PARAPET[2] * math.cos(math.pi / 2 * q) ** 2 if q < 1 else 0.0


# ---------------------------------------------------------------- landforms

_land = random.Random(SEED + 1)


def scatter(rect, count, spacing, radius, depth, rim):
    """Craters (x, z, radius, depth, rim) spread over rect at least `spacing` apart (notes W1)."""
    found = []
    while len(found) < count:
        x, z = _land.uniform(rect[0], rect[2]), _land.uniform(rect[1], rect[3])
        if all(math.hypot(x - cx, z - cz) >= spacing for cx, cz, *_ in found):
            found.append((round(x, 2), round(z, 2), round(_land.uniform(*radius), 2), round(_land.uniform(*depth), 2),
                          round(_land.uniform(*rim), 2)))
    return found


# The dry field's shell craters, 2-4 m across and 5-20 m apart; three strike craters in the burnt field; and the small
# craters under the meadow's scorch spots (notes T1: the small form of T3b).
FIELD_CRATERS = scatter((162, -133, 224, -17), 60, 8.0, (1.0, 2.0), (0.3, 1.0), (0.1, 0.3))
BURNT_CRATERS = scatter((110, -222, 220, -165), 3, 25.0, (1.2, 2.0), (0.4, 0.9), (0.15, 0.3))
SCORCH_CRATERS = scatter((165, 20, 222, 130), 4, 25.0, (0.8, 1.2), (0.2, 0.35), (0.05, 0.1))
CRATERS = FIELD_CRATERS + BURNT_CRATERS + SCORCH_CRATERS
BANK = (157.0, -75.0, 55.0, 10.0, 3.5, 1.1)  # the dry field's spoil bank: x, centre z, half length, taper, half width, height
# The rubble patch's heaps (notes B5): x, z, radius, height.
HEAPS = [(round(_land.uniform(-220, -110), 1), round(_land.uniform(170, 222), 1), round(_land.uniform(6, 10), 1),
          round(_land.uniform(0.8, 2.2), 2)) for _ in range(6)]
# The burnt field's unburnt islands and the weeds' openings: x, z, radius.
ISLANDS = [(round(_land.uniform(110, 220), 1), round(_land.uniform(-222, -165), 1), round(_land.uniform(5, 10), 1))
           for _ in range(5)]
OPENINGS = [(round(_land.uniform(105, 222), 1), round(_land.uniform(165, 222), 1), round(_land.uniform(4, 8), 1))
            for _ in range(4)]

_crater_cells = {}
for _crater in CRATERS:
    _reach = 2 * _crater[2]
    for _gx in range(int((_crater[0] - _reach) // 16), int((_crater[0] + _reach) // 16) + 1):
        for _gz in range(int((_crater[1] - _reach) // 16), int((_crater[1] + _reach) // 16) + 1):
            _crater_cells.setdefault((_gx, _gz), []).append(_crater)


def near_craters(x, z):
    return _crater_cells.get((int(x // 16), int(z // 16)), ())


def relief(x, z):
    """The relief patch (PR-8): rolling meadow, a crest line every 80 m across the diagonal, cross swells every 50 m.
    Crest to dip is 3.2 m along the power line. It fades in over 25 m inside its wandering outline."""
    rect = patch("relief")[3]
    sd = rect_sd(x, z, rect)
    if sd > 12:
        return 0.0
    w = smoothstep(-(sd + 8 * wobble(x, z, 200, 70)) / 25)
    if w <= 0:
        return 0.0
    u, v = (x + z) / math.sqrt(2), (x - z) / math.sqrt(2)
    return w * (1.6 * math.cos(TAU * u / 80) + 0.6 * math.cos(TAU * v / 50 + 1.3))


def bank(x, z):
    bx, cz, length, taper, half, top = BANK
    d = abs(x - bx)
    if d >= half:
        return 0.0
    return top * math.cos(math.pi / 2 * d / half) ** 2 * smoothstep((length + taper - abs(z - cz)) / taper)


def terrain(x, z):
    """The ground before the levelled pads: a gentle roll, the edge rise, the relief patch, craters, the spoil bank and
    the rubble heaps."""
    h = 0.25 * math.sin(TAU * x / 260 + 0.7) * math.cos(TAU * z / 210) + 0.15 * math.sin(TAU * (x + z) / 150)
    edge = HALF - max(abs(x), abs(z))
    if edge < RIM[1]:
        h += RIM[0] * smoothstep((RIM[1] - edge) / RIM[1])
    h += relief(x, z) + bank(x, z)
    for cx, cz, radius, depth, rim in near_craters(x, z):
        q = math.hypot(x - cx, z - cz) / radius
        if q < 1:
            h += -depth + (depth + rim) * q * q
        elif q < 2:
            h += rim * (2 - q) ** 2
    if -235 <= x <= -95 and 160 <= z <= 232:
        for hx, hz, radius, top in HEAPS:
            q = math.hypot(x - hx, z - hz) / radius
            if q < 1:
                h += top * math.cos(math.pi / 2 * q) ** 2
    return h


# ---------------------------------------------------------------- levelled pads

def pad_weight(pad, x, z):
    cx, cz, hx, hz, blend = pad
    return smoothstep((hx + blend - abs(x - cx)) / blend) * smoothstep((hz + blend - abs(z - cz)) / blend)


# Every station whose ground is a yard or a building site stands on a levelled pad (a village yard is graded level, a
# house built on a level footing, and the cellar's flat-topped lip needs it, F-3). The pads never overlap.
PADS = [((s.centre[0], s.centre[1], s.half[0], s.half[1], 2.5), sample(terrain(*s.centre)))
        for s in STATIONS if s.ground in ("yard", "block_site")]
_mid = trench_axes()[0][3]
TRENCH_LEVEL = sample(terrain(_mid[0] - 20, _mid[1]))


def height(x, z):
    h = terrain(x, z)
    for pad, level in PADS:
        w = pad_weight(pad, x, z)
        if w > 0:
            h = h * (1 - w) + level * w
    if -75 <= x <= 15 and -200 <= z <= -168:
        # The trench's level strip: flat within TRENCH_FLAT of its line (its closed ends pushed out by as much, so the
        # end caps lie flat too), blending out over 3 m.
        w = smoothstep((TRENCH_FLAT + 3.0 - trench_distance(x, z, TRENCH_FLAT)) / 3.0)
        if w > 0:
            h = h * (1 - w) + TRENCH_LEVEL * w
    return h + parapets(x, z)


def slope(x, z):
    return math.hypot(height(x + 0.5, z) - height(x - 0.5, z), height(x, z + 0.5) - height(x, z - 0.5))


# ---------------------------------------------------------------- surfaces and cover

def meadow_cover(x, z):
    return (int(200 + 55 * (0.5 + 0.5 * math.sin(x / 7) * math.cos(z / 9))), 0, 0, 0)


def weeds_cover(x, z):
    return (int(200 + 55 * (0.5 + 0.5 * math.sin(x / 3) * math.cos(z / 4))), 0, 0, 0)


def crust_cover(x, z):
    return (int(150 + 105 * (0.5 + 0.5 * math.sin(x / 5) * math.cos(z / 6))), 0, 0, 0)


def tilled_cover(x, z):
    """Straw residue in streaks along the ridges, 6 m apart (the ridges run along x, azimuth 0)."""
    return (0, int(40 + 215 * (0.5 + 0.5 * math.cos(TAU * z / 6)) ** 2), 0, 0)


def burnt_cover(x, z):
    """R charred stubble, A ash flakes, both patchy (notes T3b)."""
    n = 0.5 + 0.5 * math.sin(x / 4.3 + 1.7) * math.cos(z / 5.1)
    return (int(110 + 120 * n), 0, 0, int(160 + 95 * (1 - n)))


STRAW_FLOOR, BARE_FLOOR = (160, 255, 120, 0), (0, 0, 255, 180)
LITTER, RUBBLE_COVER, TRAMPLED, ROAD = (0, 0, 90, 255), (0, 0, 255, 0), (40, 0, 60, 0), (25, 0, 0, 0)
SCORCH = (120, 0, 0, 230)


def fixed(cover):
    return lambda x, z: cover


Painter = namedtuple("Painter", "bbox member surface cover")
PAINTERS = []


def paint_area(bbox, member, surface, cover):
    """Adds a painter: `member(x, z)` gives 0-1 inside its outline, `cover(x, z)` the RGBA it lays at full strength."""
    PAINTERS.append(Painter(bbox, member, surface, cover if callable(cover) else fixed(cover)))


def organic_rect(rect, k, amp=6.0, wavelength=40.0, feather=6.0, corner=0.0):
    """A rectangle with corners rounded to `corner` m, its outline wandering by `amp`."""
    reach = amp + feather
    core = (rect[0] + corner, rect[1] + corner, rect[2] - corner, rect[3] - corner)
    return ((rect[0] - reach, rect[1] - reach, rect[2] + reach, rect[3] + reach),
            lambda x, z: outline(rect_sd(x, z, core) - corner, x, z, k, amp, wavelength, feather))


def organic_ellipse(cx, cz, rx, rz, k, amp, wavelength, feather):
    reach = amp + feather
    return ((cx - rx - reach, cz - rz - reach, cx + rx + reach, cz + rz + reach),
            lambda x, z: outline(ellipse_sd(x, z, cx, cz, rx, rz), x, z, k, amp, wavelength, feather))


def organic_disc(cx, cz, r, k, feather):
    return organic_ellipse(cx, cz, r, r, k, 0.22 * r, max(r, 3.0), feather)


def belt_floor(rect, along, k, margin):
    """A belt's floor (notes T4, T5): lodged straw out to `margin` beyond its rows, beaten bare soil under the canopy."""
    x0, z0, x1, z1 = rect
    paint_area(*organic_rect((x0 - margin, z0 - margin, x1 + margin, z1 + margin), k, 2.5, 18.0, 4.0),
               BELT_STRAW, STRAW_FLOOR)
    if along == "x":
        mid = (z0 + z1) / 2
        bare = (x0 + 1, mid - 2.5, x1 - 1, mid + 2.5)
    else:
        mid = (x0 + x1) / 2
        bare = (mid - 2.5, z0 + 1, mid + 2.5, z1 - 1)
    paint_area(*organic_rect(bare, k + 1, 1.0, 9.0, 2.0), BELT_BARE, BARE_FLOOR)


def edge_member(x, z):
    """The edge belt's floor: from the map edge to EDGE_FLOOR in, its inner side wandering."""
    return outline(HALF - max(abs(x), abs(z)) - EDGE_FLOOR, x, z, 1, 3.0, 25.0, 5.0)


BELT_PATCH = (-222.0, 12.0, -204.0, 138.0)   # the review belt: six rows (18 m) as sample_patch's
RELIEF_BELT = (-206.0, -140.0, -194.0, -30.0)  # the belt run over the relief: four rows


def build_painters():
    """Painters in the order they paint: later ones cover earlier ones where they win a cell."""
    reach = EDGE_FLOOR + 8
    for bbox in ((-HALF, -HALF, HALF, -HALF + reach), (-HALF, HALF - reach, HALF, HALF),
                 (-HALF, -HALF, -HALF + reach, HALF), (HALF - reach, -HALF, HALF, HALF)):
        paint_area(bbox, edge_member, BELT_STRAW, STRAW_FLOOR)

    paint_area(*organic_rect(patch("tilled")[3], 10, 9.0, 55.0, 7.0, 18.0), TILLED, tilled_cover)

    x0, z0, x1, z1 = patch("trench")[3]
    paint_area(*organic_rect((x0, z0, x1, -218.0), 12, 5.0, 40.0, 6.0, 6.0), TILLED, tilled_cover)  # the dark field by the strip
    belt_floor(TRENCH_STRIP, "x", 14, 4.0)
    paint_area((-75.0, -204.0, 15.0, -166.0), lambda x, z: outline(trench_distance(x, z) - 6.5, x, z, 16, 1.5, 12.0, 3.0),
               BELT_STRAW, STRAW_FLOOR)  # dry-grass parapets (notes W2)

    # The burnt field: a fire front that wanders far (notes T3b: wavy edges), with unburnt islands.
    paint_area(*organic_rect(patch("burnt")[3], 20, 12.0, 60.0, 8.0, 25.0), BURNT_FIELD, burnt_cover)
    for i, (x, z, r) in enumerate(ISLANDS):
        paint_area(*organic_disc(x, z, r, 21 + i, 3.0), MEADOW_SOD, meadow_cover)

    paint_area(*organic_rect(patch("craters")[3], 30, 9.0, 50.0, 7.0, 18.0), DRY_CRUST, crust_cover)
    for i, (x, z, r, _, _) in enumerate(FIELD_CRATERS + BURNT_CRATERS):
        paint_area(*organic_disc(x, z, 2 * r, 40 + i, 1.0), CRATER_SPOIL, (0, 0, 0, 0))
    paint_area((BANK[0] - 8, BANK[1] - BANK[2] - BANK[3] - 4, BANK[0] + 8, BANK[1] + BANK[2] + BANK[3] + 4),
               lambda x, z: 1.0 if bank(x, z) > 0.02 + 0.03 * wobble(x, z, 31, 6) else 0.0, CRATER_SPOIL, (0, 0, 0, 0))

    for i, (x, z, r, _, _) in enumerate(SCORCH_CRATERS):
        paint_area(*organic_disc(x, z, 2.2 * r + 0.8, 110 + i, 1.5), BURNT_FIELD, SCORCH)

    paint_area(*organic_rect(patch("weeds")[3], 120, 10.0, 45.0, 8.0, 22.0), WEEDS, weeds_cover)
    for i, (x, z, r) in enumerate(OPENINGS):
        paint_area(*organic_disc(x, z, r, 121 + i, 3.0), MEADOW_SOD, meadow_cover)

    x0, z0, x1, z1 = patch("yard")[3]
    paint_area(*organic_ellipse((x0 + x1) / 2, (z0 + z1) / 2 + 2, 46.0, 38.0, 130, 7.0, 30.0, 7.0), YARD_LITTER, LITTER)

    x0, z0, x1, z1 = patch("rubble")[3]
    paint_area(*organic_rect((x0 + 6, z0 + 4, x1 - 6, z1 - 4), 140, 6.0, 35.0, 8.0, 15.0), DRY_CRUST, TRAMPLED)
    paint_area(*organic_ellipse((x0 + x1) / 2, (z0 + z1) / 2, 55.0, 36.0, 141, 5.0, 30.0, 6.0), RUBBLE, RUBBLE_COVER)

    belt_floor(BELT_PATCH, "z", 150, 22.0)
    belt_floor(RELIEF_BELT, "z", 160, 8.0)

    for i, road in enumerate(ROADS):
        paint_area(*organic_rect(road, 170 + i, 0.35, 9.0, 1.2), DRY_CRUST, ROAD)

    for i, s in enumerate(STATIONS):
        station_ground(s, 180 + 3 * i)


def station_ground(s, k):
    """A station's ground (GROUNDS), laid whether or not the station is built."""
    cx, cz = s.centre
    if s.ground == "yard":
        # Beaten litter over the yard, a ragged blob not a rectangle, with weeds in its far corner.
        paint_area(*organic_ellipse(cx, cz, s.half[0] * 0.95, s.half[1] * 0.95, k, 2.0, 14.0, 3.0), YARD_LITTER, LITTER)
        wx, wz = to_world(s.centre, s.yaw, -10.5, 8.0)
        paint_area(*organic_disc(wx, wz, 4.0, k + 1, 2.0), WEEDS, weeds_cover)
    elif s.ground == "block_site":
        # Trampled bare ground round the block (notes T8), and its rubble spilling toward the road (B5).
        paint_area(*organic_ellipse(cx, cz, s.half[0], s.half[1] * 0.9, k, 3.0, 20.0, 4.0), DRY_CRUST, TRAMPLED)
        sx, sz = to_world(s.centre, s.yaw, 0.0, 16.0)
        paint_area(*organic_ellipse(sx, sz, 32.0, 9.0, k + 1, 2.5, 14.0, 3.0), RUBBLE, RUBBLE_COVER)
    elif s.ground == "scorch":
        # The soot patch a burnt wreck sits in (notes R0: 2-6 m across).
        paint_area(*organic_disc(cx, cz, 3.2, k, 1.2), BURNT_FIELD, SCORCH)
    elif s.ground == "overgrown":
        # Weeds round and through an abandoned body (notes R0).
        paint_area(*organic_disc(cx, cz, 4.5, k, 2.0), WEEDS, weeds_cover)


GROUNDS = ("yard", "block_site", "scorch", "overgrown", "plain")


def rubble_rings(objects):
    """Rubble at the wall foot of each placed building of RUBBLE_AROUND (notes B1: tile shards piled at the wall foot)."""
    for i, o in enumerate(objects):
        if o["asset"] in RUBBLE_AROUND:
            hx, hz = RUBBLE_AROUND[o["asset"]]
            x, _, z = o["position_m"]
            rect = (x - hx, z - hz, x + hx, z + hz)
            paint_area(*organic_rect(rect, 240 + i, 0.3, 4.0, 1.6), RUBBLE, RUBBLE_COVER)


def dither(r, c, k):
    h = (r * 73856093 ^ c * 19349663 ^ k * 83492791 ^ SEED) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 0x5BD1E995) & 0xFFFFFFFF
    return ((h ^ (h >> 15)) & 0xFFFFFF) / 16777216.0


def scaled_cover(cover, s):
    return tuple(int(v * s) for v in cover)


# ---------------------------------------------------------------- holes

def segment_rect_distance(a, b, x0, z0, x1, z1):
    """Distance between segment ab and the rectangle [x0, x1] x [z0, z1], 0 when they meet."""
    t0, t1, dx, dz = 0.0, 1.0, b[0] - a[0], b[1] - a[1]
    for p, q in ((-dx, a[0] - x0), (dx, x1 - a[0]), (-dz, a[1] - z0), (dz, z1 - a[1])):
        if p == 0:
            if q < 0:
                t0, t1 = 1.0, 0.0
                break
        else:
            t = q / p
            if p < 0:
                t0 = max(t0, t)
            else:
                t1 = min(t1, t)
    if t0 <= t1:
        return 0.0
    corners = ((x0, z0), (x1, z0), (x0, z1), (x1, z1))
    return min([segment_distance(cx, cz, a, b) for cx, cz in corners]
               + [math.hypot(p[0] - min(max(p[0], x0), x1), p[1] - min(max(p[1], z0), z1)) for p in (a, b)])


def hole_cells(cells, objects):
    """The hole cells as a set of (row, column): every cell that meets a trench strip, and each placed cellar shaft's
    band, whose cells are exactly the band because it is cell-aligned."""
    marked = set()

    def scan(bx0, bz0, bx1, bz1, test):
        for r in range(max(math.floor((bz0 + HALF) / CELL_RES), 0), min(math.floor((bz1 + HALF) / CELL_RES) + 1, cells)):
            for c in range(max(math.floor((bx0 + HALF) / CELL_RES), 0), min(math.floor((bx1 + HALF) / CELL_RES) + 1, cells)):
                x0, z0 = -HALF + c * CELL_RES, -HALF + r * CELL_RES
                if test(x0, z0, x0 + CELL_RES, z0 + CELL_RES):
                    marked.add((r, c))

    for a, b in trench_strips():
        reach = TRENCH_HALF + CELL_RES
        scan(min(a[0], b[0]) - reach, min(a[1], b[1]) - reach, max(a[0], b[0]) + reach, max(a[1], b[1]) + reach,
             lambda x0, z0, x1, z1, a=a, b=b: segment_rect_distance(a, b, x0, z0, x1, z1) <= TRENCH_HALF)
    for o in objects:
        if o["asset"] != "cellar_shaft":
            continue
        x, _, z = o["position_m"]
        if (x - 0.25) % 0.5 or (z - 0.25) % 0.5 or o["rotation_deg"][0] % 90:
            sys.exit(f"cellar_shaft at ({x}, {z}) yaw {o['rotation_deg'][0]}: its hole band would not lie on cell edges")
        hx, hz = CELLAR_BAND if o["rotation_deg"][0] % 180 == 0 else CELLAR_BAND[::-1]
        scan(x - hx - CELL_RES, z - hz - CELL_RES, x + hx + CELL_RES, z + hz + CELL_RES,
             lambda x0, z0, x1, z1: x0 < x + hx - 1e-9 and x1 > x - hx + 1e-9 and z0 < z + hz - 1e-9 and z1 > z - hz + 1e-9)
    return marked


# ---------------------------------------------------------------- objects

SEAT_RADIUS = {"pole": 0.12, "shrub_belt": 0.3}  # how far a base reaches out, so it can be sunk on a slope; trees 0.25


def placed(asset, x, z, yaw=0.0, pitch=0.0, scale=1.0):
    """An object standing on the ground at (x, z). Buildings and props on pads stand at the pad level; a trunk, a pole
    or a bush is sunk by its base radius times the local slope (plus 2 cm), so no side of its base floats."""
    radius = SEAT_RADIUS.get(asset, 0.25 if asset.startswith("tree_") else 0.0)
    y = height(x, z) - (radius * slope(x, z) + 0.02 if radius else 0.0)
    return {"asset": asset, "position_m": [round(x, 3), round(y, 3), round(z, 3)],
            "rotation_deg": [round(yaw, 2), round(pitch, 2), 0.0], "scale": scale}


def up_axis(o, length):
    """The point `length` m up an object's own +Y axis (YXZ order: yaw about Y, then pitch about X; no roll here)."""
    x, y, z = o["position_m"]
    yaw, pitch = math.radians(o["rotation_deg"][0]), math.radians(o["rotation_deg"][1])
    s = length * o["scale"]
    return [round(x + s * math.sin(pitch) * math.sin(yaw), 3), round(y + s * math.cos(pitch), 3),
            round(z + s * math.sin(pitch) * math.cos(yaw), 3)]


def station_objects(s):
    objects = []
    for p in s.placements:
        asset, dx, dz, yaw = p[:4]
        x, z = to_world(s.centre, s.yaw, dx, dz)
        objects.append(placed(asset, x, z, s.yaw + yaw, p[4] if len(p) > 4 else 0.0))
    for asset, hung, attach, sag, diameter in s.wires:
        objects.append({"asset": asset, "points_m": [up_axis(objects[i], attach) for i in hung], "sag_m": sag,
                        "diameter_m": diameter})
    return objects


class Trunks:
    """Trunk positions on a 4 m grid, for spacing checks."""

    def __init__(self):
        self.cells = {}

    def near(self, x, z, d):
        gx, gz = int(x // 4), int(z // 4)
        return any(math.hypot(x - tx, z - tz) < d for i in (-1, 0, 1) for j in (-1, 0, 1)
                   for tx, tz in self.cells.get((gx + i, gz + j), ()))

    def add(self, x, z):
        self.cells.setdefault((int(x // 4), int(z // 4)), []).append((x, z))


def belt(rng, rect, along, clearings=(), scrub=1.0):
    """A tree belt (notes V1) in rows along `along` ("x" or "z") over rect, as sample_patch's: rows ROW_M apart, trunks
    STEP_M × 0.62-1.38 apart along a row, planted acacia and ash inside and young trees, box elder and the odd dead tree
    in the two sunlit outer rows. `clearings` are (centre along, half width) gaps across the belt.

    Bushes (pilot, 2026-09-30: "posadkis also have occasional bushes among the trees"): in loose runs with gaps along
    the two sunlit edges, where they are thickest, and inside the belt as occasional clumps of two to four, never a
    wall. `scrub` stretches the edge runs' spacing."""
    x0, z0, x1, z1 = rect
    a0, a1, b0, b1 = (x0, x1, z0, z1) if along == "x" else (z0, z1, x0, x1)

    def at(a, b):
        return (a, b) if along == "x" else (b, a)

    def cleared(a):
        return any(abs(a - ca) < half for ca, half in clearings)

    rows = max(1, round((b1 - b0) / ROW_M))
    objects, trunks = [], Trunks()
    for row in range(rows):
        bc = b0 + ROW_M * (row + 0.5)
        edge = min(row, rows - 1 - row) == 0
        a = a0 + rng.uniform(0, STEP_M)
        while True:
            a += STEP_M * rng.uniform(0.62, 1.38)
            if a >= a1:
                break
            b = bc + rng.uniform(-ROW_M, ROW_M) / 3
            x, z = at(a, b)
            if cleared(a) or trunks.near(x, z, 1.9):
                continue
            roll = rng.random()
            if roll < (0.10 if edge else 0.04):
                asset = "tree_dead"
            elif edge:
                asset = "tree_young" if roll < 0.55 else "tree_maple"
            else:
                asset = "tree_acacia" if roll < 0.62 else "tree_ash" if roll < 0.9 else "tree_maple"
            trunks.add(x, z)
            objects.append(placed(asset, x, z, rng.uniform(0, 360), scale=round(rng.uniform(0.84, 1.16), 3)))

    def bush(a, b):
        x, z = at(a, b)
        if a0 <= a <= a1 and not cleared(a) and not trunks.near(x, z, 1.2):
            objects.append(placed("shrub_belt", x, z, rng.uniform(0, 360), scale=round(rng.uniform(0.7, 1.35), 3)))

    for edge_b, inward in ((b0, 1), (b1, -1)):
        a = a0 + rng.uniform(0, 4)
        while a < a1:
            a += scrub * rng.uniform(1.5, 7.5)
            bush(a, edge_b + inward * rng.uniform(-1.0, 2.5))
    if b1 - b0 > 6:
        for _ in range(int((a1 - a0) * (b1 - b0) / 120)):
            ca, cb = rng.uniform(a0, a1), rng.uniform(b0 + 2.5, b1 - 2.5)
            for _ in range(rng.randint(2, 4)):
                bush(ca + rng.uniform(-1.6, 1.6), cb + rng.uniform(-1.6, 1.6))
    return objects


def trench_objects():
    """Per run: its sections and the cap on its closed end, at the level strip's height; then the wedge outside the
    bend. Sections overlap their neighbours by 2 m (F-5), and each run's walls stop at the corner."""
    objects = []
    for (x, z), yaw, d, _, sections in trench_axes():
        objects.append({"asset": "test_trench_cap", "position_m": [round(x, 3), TRENCH_LEVEL, round(z, 3)],
                        "rotation_deg": [round(yaw, 2), 0.0, 0.0], "scale": 1.0})
        for k in range(sections):
            sx, sz = x + TRENCH_SECTION * k * d[0], z + TRENCH_SECTION * k * d[1]
            objects.append({"asset": "test_trench", "position_m": [round(sx, 3), TRENCH_LEVEL, round(sz, 3)],
                            "rotation_deg": [round(yaw, 2), 0.0, 0.0], "scale": 1.0})
    corner = trench_axes()[0][3]
    objects.append({"asset": "test_trench_corner", "position_m": [round(corner[0], 3), TRENCH_LEVEL, round(corner[1], 3)],
                    "rotation_deg": [90.0 - TRENCH_BEND / 2, 0.0, 0.0], "scale": 1.0})
    return objects


def patch_objects(rng):
    objects = trench_objects()
    objects += belt(rng, TRENCH_STRIP, "x", clearings=((20.0, 3.5),))
    objects += belt(rng, BELT_PATCH, "z", clearings=((75.0, 4.5),))
    objects += belt(rng, RELIEF_BELT, "z", clearings=((-85.0, 4.0),))
    # A lone ash and some scrub on the relief's crest, for the skyline (PR-8).
    objects.append(placed("tree_ash", -150.0, -76.0, 40.0))
    objects += [placed("shrub_belt", x, z, rng.uniform(0, 360), scale=round(rng.uniform(0.8, 1.2), 3))
                for x, z in ((-135.0, -88.0), (-128.0, -100.0), (-165.0, -58.0))]
    # The dry field's sparse thin broken trees (notes T2, V2).
    objects += [placed(a, x, z, rng.uniform(0, 360)) for a, x, z in
                (("tree_dead", 171.0, -118.0), ("tree_dead", 220.0, -40.0), ("tree_young", 236.0, -128.0),
                 ("tree_dead", 205.0, -86.0), ("tree_young", 168.0, -20.0))]
    # The weeds: self-seeded saplings, a leaning dead trunk in the middle (notes D, V2), and a cable sagging low across
    # them from a pole to a sapling (notes S1: 0.5-2 m up).
    saplings = [placed("tree_young", x, z, rng.uniform(0, 360), scale=round(rng.uniform(0.7, 1.05), 3)) for x, z in
                ((112.0, 176.0), (131.0, 214.0), (166.0, 170.0), (188.0, 222.0), (214.0, 184.0), (226.0, 207.0),
                 (150.0, 232.0))]
    objects += saplings
    objects.append(placed("tree_dead", 172.0, 200.0, 130.0, 9.0))
    objects += [placed("shrub_belt", x, z, rng.uniform(0, 360)) for x, z in ((121.0, 190.0), (197.0, 205.0))]
    pole = placed("pole", 140.0, 196.0)
    objects.append(pole)
    objects.append({"asset": "cable", "points_m": [up_axis(pole, 2.2), up_axis(saplings[0], 1.5)], "sag_m": 0.8,
                    "diameter_m": 0.01})
    # The yard patch: two old fruit trees and what is left of a fence (notes V3, B6).
    objects += [placed("tree_fruit", -18.0, 190.0, 75.0), placed("tree_fruit", 22.0, 214.0, 250.0)]
    objects += [placed("fence_planks", x, 168.0, 0.0) for x in (-30.0, -27.5, -20.0, -12.5, -10.0)]
    return objects


def edge_belt(rng):
    """Two rows round the whole map on its rise, so the eye stops at trees and not at the end of the terrain."""
    near, far = HALF - EDGE_BELT[1], HALF - EDGE_BELT[0]
    rects = [((-far, -far, far, -near), "x"), ((-far, near, far, far), "x"),
             ((-far, -near, -near, near), "z"), ((near, -near, far, near), "z")]
    return [o for rect, along in rects for o in belt(rng, rect, along, scrub=1.6)]


# ---------------------------------------------------------------- labels

def label_heights(centre, yaw, half, top):
    """The label's reach in height: from the lowest ground over its area to `top` above the highest."""
    hs = [height(*to_world(centre, yaw, half[0] * i / 4, half[1] * j / 4)) for i in range(-4, 5) for j in range(-4, 5)]
    return [round(min(hs), 2), round(max(hs) + top, 2)]


def labels(catalog):
    entries = []
    for kind, name, state, (x0, z0, x1, z1), top in PATCHES:
        centre, half = ((x0 + x1) / 2, (z0 + z1) / 2), ((x1 - x0) / 2, (z1 - z0) / 2)
        entries.append({"name": name, "state": state, "kind": "patch", "centre_m": list(centre), "half_m": list(half),
                        "yaw_deg": 0.0, "y_m": label_heights(centre, 0.0, half, top)})
    for s in STATIONS:
        assets = sorted({p[0] for p in s.placements} | {w[0] for w in s.wires})
        entries.append({"name": s.name, "state": s.state, "kind": "station", "assets": assets,
                        "built": is_built(s, catalog), "centre_m": list(s.centre), "half_m": list(s.half),
                        "yaw_deg": s.yaw, "y_m": label_heights(s.centre, s.yaw, s.half, s.top)})
    return {"about": "Review labels of the UAT-1 gallery, written by tools/map/make_uat1_gallery.py: the name, state and "
                     "area of every terrain patch and asset station, shown by game/src/world/MapLabels.cs within 25 m. "
                     "Not hashed: nothing physical reads them (game/maps/README.md, labels.json).",
            "labels": entries}


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "game", "maps", "uat1_gallery")
    os.makedirs(out, exist_ok=True)
    with open(CATALOG, encoding="utf-8") as f:
        catalog = set(json.load(f)["assets"])
    for s in STATIONS:
        if s.ground not in GROUNDS:
            sys.exit(f"station {s.id}: unknown ground '{s.ground}'")
    samples, cells = int(SIZE / HEIGHT_RES) + 1, int(SIZE / CELL_RES)

    rng = random.Random(SEED + 2)
    objects = []
    for s in STATIONS:
        if is_built(s, catalog):
            objects += station_objects(s)
    objects += patch_objects(rng) + edge_belt(rng)

    with open(os.path.join(out, "height.r16"), "wb") as f:
        for r in range(samples):
            row = array("H", [round((height(-HALF + c * HEIGHT_RES, -HALF + r * HEIGHT_RES) - OFFSET) / SCALE)
                              for c in range(samples)])
            if sys.byteorder == "big":
                row.byteswap()
            f.write(row.tobytes())

    build_painters()
    rubble_rings(objects)
    holes = hole_cells(cells, objects)
    rows_painters = [[] for _ in range(cells)]
    for k, p in enumerate(PAINTERS):
        for r in range(max(math.floor((p.bbox[1] + HALF) / CELL_RES), 0), min(math.ceil((p.bbox[3] + HALF) / CELL_RES), cells)):
            rows_painters[r].append((k, p))
    kinds, covers, hole_rows = [], [], []
    for r in range(cells):
        z = -HALF + (r + 0.5) * CELL_RES
        row_kinds, row_cover, row_holes = bytearray(cells), bytearray(4 * cells), bytearray(cells)
        painters = rows_painters[r]
        for c in range(cells):
            if (r, c) in holes:
                # A hole cell is bare cut soil with no cover, as in sample_patch.
                row_kinds[c], row_holes[c] = BELT_BARE, 255
                continue
            x = -HALF + (c + 0.5) * CELL_RES
            surface, cover = MEADOW_SOD, meadow_cover(x, z)
            for k, p in painters:
                if not p.bbox[0] <= x <= p.bbox[2]:
                    continue
                m = p.member(x, z)
                if m <= 0:
                    continue
                if m >= 1 or m > dither(r, c, k):
                    surface, cover = p.surface, scaled_cover(p.cover(x, z), 0.35 + 0.65 * m)
                else:
                    cover = scaled_cover(cover, 1 - 0.65 * m)
            row_kinds[c] = surface
            row_cover[4 * c:4 * c + 4] = bytes(cover)
        kinds.append(bytes(row_kinds))
        covers.append(bytes(row_cover))
        hole_rows.append(bytes(row_holes))
    write_png(os.path.join(out, "surface.png"), cells, cells, GREY, kinds)
    write_png(os.path.join(out, "cover.png"), cells, cells, RGBA, covers)
    write_png(os.path.join(out, "holes.png"), cells, cells, GREY, hole_rows)
    for layer in ("surface.png", "cover.png", "holes.png"):
        with open(os.path.join(out, layer + ".import"), "w", newline="\n") as f:
            f.write('[remap]\n\nimporter="keep"\n')

    with open(os.path.join(out, "objects.json"), "w", newline="\n") as f:
        json.dump({"objects": objects}, f, indent=2)
        f.write("\n")
    with open(os.path.join(out, "labels.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(labels(catalog), f, indent=2, ensure_ascii=False)
        f.write("\n")
    manifest = {
        "format_version": "1.1",  # holes.png
        "size_m": SIZE,
        "seed": SEED,
        "height": {"resolution_m": HEIGHT_RES, "samples_per_side": samples, "offset_m": OFFSET, "scale_m": SCALE},
        "surface": {"resolution_m": CELL_RES, "cells_per_side": cells},
        "starts": [{"position_m": [0.0, round(height(0.0, 0.0), 2), 0.0], "yaw_deg": 0.0}],
    }
    with open(os.path.join(out, "map.json"), "w", newline="\n") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    built = [s.id for s in STATIONS if is_built(s, catalog)]
    reserved = [s.id for s in STATIONS if not is_built(s, catalog)]
    print(f"wrote {os.path.normpath(out)}: {len(objects)} objects, {len(holes)} hole cells; "
          f"{len(built)} stations built ({', '.join(built)}), {len(reserved)} reserved ({', '.join(reserved)})")


if __name__ == "__main__":
    main()
