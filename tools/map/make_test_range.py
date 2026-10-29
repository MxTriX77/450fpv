"""Writes maps/test-range/placements.json: the test range (p6-test-range design 5-9, D-044).

    python tools/map/make_test_range.py [OUT]       (default: maps/test-range/placements.json)

A small flat map for G4's noclip through the feed and G7's first flights, made of what exists: the Godot yard
(make_uat1_gallery.py's ADOBE_YARD, its Z flipped), a posadka of the Godot belt trees and shrub mixed with V01 and V13,
S04 grass along both edges of the belt, R01's two cars, an L01/L03 power line along the track, E01's trench along
the belt and the start points. Every size, spacing and count is a row of TABLE, labelled (docs/workflow.md), so the
pilot's change is an edit there and a re-run. The same seed gives the same bytes.

The file is the one placement record both the scene builder (Fpv450.TestRangeScene) and the sim's world loader read
(design 7). Its frame is Unity's (D-019): +X east, +Y up, +Z north, metres, the map's centre the origin; a yaw is in
degrees about +Y, positive turning north towards east (Quaternion.Euler(0, yaw, 0)). Every number is rounded to the
millimetre and the thousandth of a degree.
- instances: one row per placed prefab (asset, variant, catalog class, position, yaw), in a fixed order; a wire span's
  row says which ports it hangs from, and its pose puts its conductors on them (the scene builder checks within 5 mm).
- ground: the plane y = 0 is the ground except over a ground patch's footprint (S04, E01), where the patch's own
  collision shapes are the only ground (review-physics W4, option a), and inside a hole band (Z04's shaft, E01's run
  and niches), where only the shapes round it answer, decided per contact point (W3). floor_cuts_m are the rectangles
  the scene cuts from its drawn floor: each patch footprint and each band's bounding rectangle outside a patch.
- start_points (WLD-08, D-022) and noclip_start (the spec's noclip start).
- review: the noclip camera's number-key views and the PLT-07 frame-rate legs. The sim reads nothing under it.
"""
import json
import math
import random
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "maps" / "test-range" / "placements.json"
MODELS = REPO / "unity" / "Assets" / "_Project" / "Art" / "Models"
CATALOG = REPO / "assets-src" / "catalog.json"

# ------------------------------------------------------------------------------------------------ the table
# name: (value, label, why). Positions are metres in the map frame unless a name says "rel" (from the yard's centre).
TABLE = {
    "seed": (450, "assumed", "any fixed seed: it draws the belt and the grass"),
    "map_side_m": (512.0, "pilot", "about 512 m square on flat ground (D-044)"),
    # the yard and the track
    "yard_centre": ((-170.0, -57.0), "assumed", "the map's west end, south of the track"),
    "yard_half_m": ((16.0, 12.0), "published", "the Godot yard station's half footprint (make_uat1_gallery.py)"),
    "track_z": (-40.0, "assumed", "the track runs east-west 7.2 m north of the yard's road-side fence"),
    "plot_offset_m": (48.0, "assumed", "H01's two neighbour plots west and east of the yard along the track"),
    # the power line
    "pole_line_x": (-200.0, "assumed", "the line's first pole, by the yard"),
    "pole_offset_m": (6.0, "assumed", "the poles stand this far north of the track: the legs-on start clears them"),
    "pole_spacing_m": (40.0, "published", "L03_medium's 40 m chord (assets-src/lines/L03_medium.json)"),
    "poles": (("L01_braced", "L01_straight", "L01_straight", "L01_stub", "L01_straight", "L01_straight"),
              "assumed", "six poles, the strutted terminal first (the spec: at least four)"),
    # the cars
    "car_a": ((9.5, 13.0, 180.0), "assumed", "rel, yaw: R01_A by the yard's gate, its struck side to the track"),
    "car_f": ((-95.0, -40.0, 8.0), "assumed", "x, z, yaw: R01_F burnt on the track, askew"),
    # the posadka
    "belt_x": ((-140.0, 160.0), "assumed", "300 m: a few hundred metres (D-044); the spec asks at least 200"),
    "belt_z": (0.0, "assumed", "its centre line runs east-west, 40 m north of the track"),
    "belt_rows": (4, "assumed", "planted rows"),
    "row_spacing_m": (3.0, "published", "Godot's ROW_M, which the pilot kept (2026-09-30)"),
    "trunk_step_m": ((1.5, 3.0), "assumed", "along a row, drawn uniformly: trunks 1-3 m apart (WLD-04)"),
    "row_jitter_m": (0.4, "assumed", "each trunk off its row line, uniformly up to this"),
    "gap": ((-20.0, 12.0), "assumed", "x, width: a field-track gap through the belt; 12 m between trunks leaves a "
                                      "passage between crowns up to 4.9 m in radius (Z11)"),
    "tree_mix": ((("Z10", "Z10_tree_acacia", 30), ("Z11", "Z11_tree_ash", 20), ("Z09", "Z09_tree_maple", 18),
                  ("Z08", "Z08_tree_young", 14), ("Z12", "Z12_tree_dead", 8), ("V01", "V01_S", 4),
                  ("V01", "V01_M", 4), ("V01", "V01_L", 2)), "assumed",
                 "weights by count: acacia first; a tenth V01, whose 200-540 collision shapes each load slowly"),
    "edge_shrub_offset_m": (1.5, "assumed", "the edge bushes stand this far outside the outer rows"),
    "edge_shrub_step_m": ((5.0, 9.0), "assumed", "along each edge, drawn uniformly"),
    "inner_shrub_step_m": ((8.0, 16.0), "assumed", "bushes among the trees (PR-10), along each row gap"),
    "shrub_mix": ((("Z13", "Z13_shrub_belt", 40), ("V13", "V13_low", 20), ("V13", "V13_mid", 25),
                   ("V13", "V13_tall", 15)), "assumed", "weights by count"),
    "plant_clearance_m": (1.0, "assumed", "no two plants' bases closer: the 1 m low end of WLD-04"),
    # the trench
    "trench_x": (50.0, "assumed", "E01's run starts here and runs 23 m east along the belt's south edge"),
    "trench_clearance_m": (1.0, "assumed", "between E01's belt-side footprint edge and the belt's southmost base"),
    # the grass
    "patch_m": (12.0, "published", "S04_patch's footprint (assets-src/surfaces/S04_patch.json)"),
    "patch_clearance_m": (1.0, "assumed", "the edge strips' patches start this far outside the belt's outer bases"),
    "patch_fill": (1.0, "measured", "share of the candidate cells that get a patch, set by the frame rate"),
    # the start points
    "legs_on_start": ((-8.0, 17.0, 90.0), "assumed", "rel, yaw: on the track west of the gate, facing east"),
    "legs_off_start": ((40.0, 45.0, 180.0), "assumed", "x, z, yaw: in the north field 39 m from the belt, facing it"),
    "start_clearance_m": (5.0, "pilot", "each start at least 5 m from any collision shape, in plan (the spec)"),
    # the noclip camera
    "noclip_start": ((-24.0, 25.0, -22.0, 27.0, -8.0), "assumed", "rel x, height, rel z, pitch down, yaw off the "
                     "bearing to the belt's far end: above the yard's corner, the yard low left, the belt running away"),
    "leg_seconds": (32.0, "published", "PLT-07: 2 s warm-up and at least 30 s measured"),
}


def v(name):
    return TABLE[name][0]


# The Godot yard about its centre, Godot's frame (+Z south) and yaw (positive turns +X towards -Z), copied from
# refs/godot/main:tools/map/make_uat1_gallery.py at 8340138 (ADOBE_YARD): (asset, dx, dz, yaw).
ADOBE_YARD = [
    ("house_adobe", -2.0, -3.0, 0.0),
    ("shed_plank", -9.0, 5.0, -14.0),
    ("cellar_head", 7.25, 3.25, 0.0),
    ("cellar_shaft", 7.25, 3.25, 0.0),
    ("gate_plank", 0.0, -9.8, 0.0),
    ("tree_fruit", -1.0, 5.0, 20.0),
    ("tree_fruit", 9.5, -4.0, 200.0),
] + [("fence_planks", dx, -9.8, 0.0) for dx in (-12.7, -10.2, -7.7, -2.7, 2.6, 7.6, 12.6)]
STAND_INS = {"house_adobe": "Z01_house_adobe", "shed_plank": "Z02_shed_plank", "cellar_head": "Z03_cellar_head",
             "cellar_shaft": "Z04_cellar_shaft", "gate_plank": "Z05_gate_plank", "fence_planks": "Z06_fence_planks",
             "tree_fruit": "Z07_tree_fruit"}
# H01's intact and damaged houses as the yard's neighbours: (asset, variant, dx, dz, yaw) about the plot's centre,
# Unity's frame. Assumed: each at its plot's centre, turned 180° so its front (door and most windows, asset -Z) faces
# the track like a village street; the damaged one east, by the burnt car. H01_D's front rubble then ends 5.3 m short
# of the track's line (its bounds, z -45.28 against -40), and both starts stay over 30 m away.
NEIGHBOUR_PLOTS = {"west": [("H01", "H01_I", 0.0, 0.0, 180.0)], "east": [("H01", "H01_D", 0.0, 0.0, 180.0)]}
PLANTS = ("V01", "V13", "Z07", "Z08", "Z09", "Z10", "Z11", "Z12", "Z13")
FOUR_HOOKS = ["hook_0", "hook_1", "hook_2", "hook_3"]

# ------------------------------------------------------------------------------------------------ geometry


def r3(x):
    """Millimetres and thousandths of a degree; never -0.0."""
    return round(x, 3) + 0.0


def rotate(yaw, x, z):
    """Asset-space (x, z) turned by a yaw in Unity's frame: +Z turns towards +X."""
    c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    return x * c + z * s, -x * s + z * c


def to_world(row, p):
    x, z = rotate(row["yaw_deg"], p[0], p[2])
    return [row["position_m"][0] + x, row["position_m"][1] + p[1], row["position_m"][2] + z]


def quat_matrix(q):
    x, y, z, w = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]


def shape_box(shape):
    """A collision shape's asset-space bounding box (min, max), conservative for capsules and cylinders."""
    c = shape["center"]
    m = quat_matrix(shape.get("rotation", [0.0, 0.0, 0.0, 1.0]))
    if shape["shape"] == "box":
        h = [0.5 * s for s in shape["size"]]
        half = [sum(abs(m[i][j]) * h[j] for j in range(3)) for i in range(3)]
    elif shape["shape"] == "sphere":
        half = [shape["radius"]] * 3
    else:  # capsule, cylinder: the axis along the rotated Y, its ends grown by the radius every way
        half = [abs(m[i][1]) * 0.5 * shape["height"] + shape["radius"] for i in range(3)]
    return [c[i] - half[i] for i in range(3)], [c[i] + half[i] for i in range(3)]


def plan_rect(row, lo, hi):
    """An asset-space box's plan footprint in the map, [xmin, zmin, xmax, zmax], under the row's yaw."""
    corners = [to_world(row, (x, 0.0, z)) for x in (lo[0], hi[0]) for z in (lo[2], hi[2])]
    xs, zs = [p[0] for p in corners], [p[2] for p in corners]
    return [min(xs), min(zs), max(xs), max(zs)]


def rect_distance(px, pz, rect):
    dx = max(rect[0] - px, 0.0, px - rect[2])
    dz = max(rect[1] - pz, 0.0, pz - rect[3])
    return math.hypot(dx, dz)


def overlaps(a, b, margin=0.0):
    return a[0] < b[2] + margin and b[0] < a[2] + margin and a[1] < b[3] + margin and b[1] < a[3] + margin


def segment_distance(px, pz, a, b):
    dx, dz = b[0] - a[0], b[2] - a[2]
    length2 = dx * dx + dz * dz
    t = 0.0 if length2 == 0 else min(max(((px - a[0]) * dx + (pz - a[2]) * dz) / length2, 0.0), 1.0)
    return math.hypot(px - (a[0] + t * dx), pz - (a[2] + t * dz))


# ------------------------------------------------------------------------------------------------ assets


class Assets:
    """Each variant's catalog class and .assetmeta.json, read once."""

    def __init__(self):
        self.catalog = json.loads(CATALOG.read_text(encoding="utf-8"))["assets"]
        self.metas = {}

    def klass(self, asset):
        return self.catalog[asset]["class"]

    def meta(self, asset, variant):
        if variant not in self.metas:
            if variant not in self.catalog[asset]["variants"]:
                raise SystemExit(f"{variant} is not a variant of {asset} in {CATALOG}")
            path = MODELS / self.klass(asset) / asset / f"{variant}.assetmeta.json"
            self.metas[variant] = json.loads(path.read_text(encoding="utf-8"))
        return self.metas[variant]


class Map:
    def __init__(self, assets):
        self.assets = assets
        self.rows = []

    def place(self, group, asset, variant, x, z, yaw, **extra):
        self.assets.meta(asset, variant)  # an unknown variant or a missing build fails here
        row = {"id": len(self.rows), "group": group, "asset": asset, "variant": variant,
               "class": self.assets.klass(asset), "position_m": [r3(x), 0.0, r3(z)], "yaw_deg": r3(yaw % 360.0)}
        row.update(extra)
        self.rows.append(row)
        return row

    def collision_rect(self, row):
        """The plan footprint of every collision shape of the row's variant, or None for a shapeless span."""
        shapes = self.assets.meta(row["asset"], row["variant"])["collision"]
        if not shapes:
            return None
        rects = [plan_rect(row, *shape_box(s)) for s in shapes]
        return [min(r[0] for r in rects), min(r[1] for r in rects), max(r[2] for r in rects), max(r[3] for r in rects)]

    def bounds_rect(self, row):
        b = self.assets.meta(row["asset"], row["variant"])["bounds"]
        return plan_rect(row, b["min"], b["max"])

    def is_patch(self, row):
        return row["class"] in ("surfaces", "earthworks")


# ------------------------------------------------------------------------------------------------ the layout


def yard(m):
    yx, yz = v("yard_centre")
    for key, dx, dz, yaw in ADOBE_YARD:  # Godot (dx, dz, yaw) -> Unity (dx, -dz, -yaw)
        variant = STAND_INS[key]
        m.place("yard", variant[:3], variant, yx + dx, yz - dz, -yaw)
    for name, side in (("west", -1.0), ("east", 1.0)):
        px = yx + side * v("plot_offset_m")
        for asset, variant, dx, dz, yaw in NEIGHBOUR_PLOTS[name]:
            m.place(f"plot_{name}", asset, variant, px + dx, yz + dz, yaw)


def power_line(m):
    """The poles along the track and a span between each pair. A pole's yaw is the line's heading less 90°, so its
    hooks stand across the line; a span takes the pole's yaw and sits with its first conductor on the first pole's
    hook_0 seat (the port plus one conductor radius along the port), so its far end lands one 40 m chord on."""
    z = v("track_z") + v("pole_offset_m")
    yaw = 0.0  # heading 90° (east) less 90°
    poles = [m.place("line", "L01", variant, v("pole_line_x") + i * v("pole_spacing_m"), z, yaw)
             for i, variant in enumerate(v("poles"))]
    for a, b in zip(poles, poles[1:]):
        span = m.assets.meta("L03", "L03_medium")
        first = span["wires"][0]
        port = next(p for p in m.assets.meta("L01", a["variant"])["ports"] if p["id"] == "hook_0")
        seat = [port["position"][i] + port["direction"][i] * 0.5 * first["diameter_m"] for i in range(3)]
        offset = [seat[i] - first["points"][0][i] for i in range(3)]  # asset space, both at yaw 0 about the pole
        sx, sz = rotate(yaw, offset[0], offset[2])
        if abs(offset[1]) > 1e-6:
            raise SystemExit(f"{a['variant']} hook_0 and L03_medium's first conductor differ in height")
        m.place("line", "L03", "L03_medium", a["position_m"][0] + sx, a["position_m"][2] + sz, yaw,
                hang={"from": a["id"], "from_ports": FOUR_HOOKS, "to": b["id"], "to_ports": FOUR_HOOKS})


def cars(m):
    yx, yz = v("yard_centre")
    dx, dz, yaw = v("car_a")
    m.place("cars", "R01", "R01_A", yx + dx, yz + dz, yaw)
    x, z, yaw = v("car_f")
    m.place("cars", "R01", "R01_F", x, z, yaw)


def belt_rows_z():
    n = v("belt_rows")
    return [v("belt_z") + (i - 0.5 * (n - 1)) * v("row_spacing_m") for i in range(n)]


def trench(m):
    """E01 along the belt's south edge, its belt side (the run's left, +Z at yaw 0) towards the trees: its footprint's
    north edge one clearance south of the belt's southmost possible base."""
    meta = m.assets.meta("E01", "E01_curving")
    south = min(belt_rows_z()) - v("edge_shrub_offset_m") - v("row_jitter_m")
    z = south - v("trench_clearance_m") - meta["bounds"]["max"][2]
    return m.place("trench", "E01", "E01_curving", v("trench_x"), z, 0.0, ground_patch=True)


def in_gap(x):
    centre, width = v("gap")
    return abs(x - centre) < 0.5 * width


def posadka(m, rng, keep_out):
    """Trees along the rows at jittered steps, then bushes along both edges and along each row gap (PR-10). A plant
    whose base falls in the gap, in a keep-out footprint or within the clearance of another is not placed."""
    x0, x1 = v("belt_x")
    lo, hi = v("trunk_step_m")
    bases = []

    def clear(x, z):
        if in_gap(x) or any(rect_distance(x, z, r) < v("plant_clearance_m") for r in keep_out):
            return False
        return all(math.hypot(x - bx, z - bz) >= v("plant_clearance_m") for bx, bz in bases)

    def draw(mix):
        asset, variant, _ = rng.choices(mix, weights=[w for _, _, w in mix])[0]
        return asset, variant

    for z_row in belt_rows_z():
        x = x0 + rng.uniform(0.0, hi)
        while x <= x1:
            asset, variant = draw(v("tree_mix"))
            z = z_row + rng.uniform(-v("row_jitter_m"), v("row_jitter_m"))
            yaw = rng.uniform(0.0, 360.0)
            if clear(x, z):
                m.place("posadka", asset, variant, x, z, yaw)
                bases.append((x, z))
            x += rng.uniform(lo, hi)

    rows = belt_rows_z()
    lines = [(min(rows) - v("edge_shrub_offset_m"), v("edge_shrub_step_m")),
             (max(rows) + v("edge_shrub_offset_m"), v("edge_shrub_step_m"))]
    lines += [(0.5 * (a + b), v("inner_shrub_step_m")) for a, b in zip(rows, rows[1:])]
    for z_line, (lo, hi) in lines:
        x = x0 + rng.uniform(0.0, hi)
        while x <= x1:
            asset, variant = draw(v("shrub_mix"))
            z = z_line + rng.uniform(-v("row_jitter_m"), v("row_jitter_m"))
            yaw = rng.uniform(0.0, 360.0)
            if clear(x, z):
                m.place("posadka", asset, variant, x, z, yaw)
                bases.append((x, z))
            x += rng.uniform(lo, hi)


def grass(m, rng):
    """S04 patches on 12 m cells in one strip along each belt edge, a lodged-straw verge (letter H's low flight along
    a belt edge). A cell is dropped where it would carry a structure's collision shapes, a plant's base (within a
    metre) or another patch. Not round the yard and the starts: from the air a cluster of patches reads as square
    tiles on the floor (the first captures), where a strip along the belt reads as a verge."""
    size = v("patch_m")
    half = 0.5 * size
    cells = []
    posadka_bases = [r["position_m"] for r in m.rows if r["group"] == "posadka"]
    south = min(p[2] for p in posadka_bases) - v("patch_clearance_m")
    north = max(p[2] for p in posadka_bases) + v("patch_clearance_m")
    x0, x1 = v("belt_x")
    n = int(math.floor((x1 - x0) / size))
    for i in range(n):
        x = x0 + (i + 0.5) * size
        cells += [(x, south - half), (x, north + half)]

    blocked = []
    for row in m.rows:
        if row["asset"] in PLANTS:
            x, z = row["position_m"][0], row["position_m"][2]
            blocked.append([x - 1.0, z - 1.0, x + 1.0, z + 1.0])
        elif m.is_patch(row):
            blocked.append(m.bounds_rect(row))
        else:
            rect = m.collision_rect(row)
            if rect is not None:
                blocked.append(rect)
    for cx, cz in cells:
        cell = [cx - half, cz - half, cx + half, cz + half]
        if any(overlaps(cell, b) for b in blocked) or rng.random() >= v("patch_fill"):
            continue
        m.place("grass", "S04", "S04_patch", cx, cz, 90.0 * rng.randrange(4), ground_patch=True)
        blocked.append(cell)


# ------------------------------------------------------------------------------------------------ ground and starts


def ground(m):
    patches, holes, cuts = [], [], []
    patch_rects = []
    for row in m.rows:
        if row.get("ground_patch"):
            if row["yaw_deg"] % 90.0 != 0.0:
                raise SystemExit(f"{row['variant']} #{row['id']}: a ground patch stands at a multiple of 90°")
            rect = m.bounds_rect(row)
            patches.append({"instance": row["id"], "footprint_m": [r3(c) for c in rect]})
            patch_rects.append(rect)
            cuts.append([r3(c) for c in rect])
    for row in m.rows:
        for hole in m.assets.meta(row["asset"], row["variant"])["holes"]:
            line = [to_world(row, p) for p in hole["centre_line"]]
            holes.append({"instance": row["id"], "record": hole["id"],
                          "centre_line_m": [[r3(c) for c in p] for p in line],
                          "band_width_m": hole["band_width_m"], "top_width_m": hole["top_width_m"],
                          "depth_m": hole["depth_m"]})
            r = 0.5 * hole["band_width_m"]
            rect = [min(p[0] for p in line) - r, min(p[2] for p in line) - r,
                    max(p[0] for p in line) + r, max(p[2] for p in line) + r]
            inside = any(p[0] <= rect[0] and p[1] <= rect[1] and rect[2] <= p[2] and rect[3] <= p[3]
                         for p in patch_rects)
            if not inside:
                if row["yaw_deg"] % 90.0 != 0.0:
                    raise SystemExit(f"{row['variant']} #{row['id']}: a floor cut stands at a multiple of 90°")
                cuts.append([r3(c) for c in rect])
    return {"plane": {"y_m": 0.0, "surface": "belt_straw"}, "patches": patches, "holes": holes, "floor_cuts_m": cuts}


def start_points(m):
    yx, yz = v("yard_centre")
    dx, dz, yaw_on = v("legs_on_start")
    x, z, yaw_off = v("legs_off_start")
    starts = [{"id": "track", "position_m": [r3(yx + dx), 0.0, r3(yz + dz)], "heading_deg": yaw_on, "legs": "on",
               "stand": False},
              {"id": "field", "position_m": [r3(x), 0.0, r3(z)], "heading_deg": yaw_off, "legs": "off",
               "stand": True}]
    for start in starts:
        clearance = start_clearance(m, start["position_m"])
        if clearance < v("start_clearance_m"):
            raise SystemExit(f"start {start['id']} is {clearance:.2f} m from a collision shape, under "
                             f"{v('start_clearance_m')} m")
    return starts


def start_clearance(m, p):
    """The plan distance from a point to the nearest collision shape, wire or ground patch of the map."""
    best = math.inf
    for row in m.rows:
        if m.is_patch(row):
            best = min(best, rect_distance(p[0], p[2], m.bounds_rect(row)))
            continue
        meta = m.assets.meta(row["asset"], row["variant"])
        for shape in meta["collision"]:
            best = min(best, rect_distance(p[0], p[2], plan_rect(row, *shape_box(shape))))
        for wire in meta["wires"]:
            pts = [to_world(row, q) for q in wire["points"]]
            best = min(best, min(segment_distance(p[0], p[2], a, b) for a, b in zip(pts, pts[1:])))
    return best


def heading(a, b):
    return math.degrees(math.atan2(b[0] - a[0], b[1] - a[1])) % 360.0


def views_and_legs(m, starts):
    yx, yz = v("yard_centre")
    dx, height, dz, pitch, turn = v("noclip_start")
    x0, x1 = v("belt_x")
    rows = belt_rows_z()
    cam = (yx + dx, yz + dz)
    noclip = {"name": "yard", "position_m": [r3(cam[0]), height, r3(cam[1])],
              "yaw_deg": r3(heading(cam, (x1, v("belt_z"))) + turn), "pitch_deg": pitch}
    edge_z = min(rows) - v("edge_shrub_offset_m") - 4.0
    trench_row = next(r for r in m.rows if r["asset"] == "E01")
    lip = to_world(trench_row, m.assets.meta("E01", "E01_curving")["holes"][0]["centre_line"][0])
    views = [noclip]
    for start in starts:
        views.append({"name": f"start {start['id']}", "position_m": [start["position_m"][0], 1.5,
                                                                    start["position_m"][2]],
                      "yaw_deg": start["heading_deg"], "pitch_deg": 0.0})
    views += [
        {"name": "posadka edge", "position_m": [r3(x0 - 10.0), 2.0, r3(edge_z)], "yaw_deg": 90.0, "pitch_deg": 0.0},
        {"name": "trench", "position_m": [r3(lip[0] - 1.0), 1.5, r3(lip[2] - 1.5)], "yaw_deg": 70.0,
         "pitch_deg": 20.0},
        {"name": "map 50 m", "position_m": [-250.0, 50.0, 150.0], "yaw_deg": r3(heading((-250.0, 150.0), (0.0, -20.0))),
         "pitch_deg": 9.0},
    ]
    legs = [
        {"name": "yard", "from_m": [r3(yx - 40.0), 12.0, r3(yz - 40.0)], "to_m": [r3(yx + 40.0), 12.0, r3(yz + 30.0)],
         "pitch_deg": 30.0, "seconds": v("leg_seconds")},
        {"name": "posadka 2 m", "from_m": [r3(x0 - 10.0), 2.0, r3(edge_z)], "to_m": [r3(x1 + 5.0), 2.0, r3(edge_z)],
         "pitch_deg": 0.0, "seconds": v("leg_seconds")},
        {"name": "fields 50 m", "from_m": [-240.0, 50.0, 200.0], "to_m": [200.0, 50.0, -120.0], "pitch_deg": 20.0,
         "seconds": v("leg_seconds")},
    ]
    return noclip, {"views": views, "bench_legs": legs}


# ------------------------------------------------------------------------------------------------ output


def build():
    rng = random.Random(v("seed"))
    m = Map(Assets())
    yard(m)
    power_line(m)
    cars(m)
    e01 = trench(m)
    posadka(m, rng, [m.bounds_rect(e01)])
    grass(m, rng)
    starts = start_points(m)
    noclip, review = views_and_legs(m, starts)
    half = 0.5 * v("map_side_m")
    for row in m.rows:
        x, _, z = row["position_m"]
        if not (-half <= x <= half and -half <= z <= half):
            raise SystemExit(f"{row['variant']} #{row['id']} at ({x}, {z}) lies outside the map")
    return {
        "format": "fpv450-placements",
        "format_version": 1,
        "map": "test-range",
        "generator": "tools/map/make_test_range.py",
        "seed": v("seed"),
        "frame": "unity-lh-yup",
        "extent_m": [-half, -half, half, half],
        "ground": ground(m),
        "instances": m.rows,
        "start_points": starts,
        "noclip_start": noclip,
        "review": review,
    }


def dumps(data):
    """Two-space JSON with one instance, patch, hole or cut per line, so a change reads as a line diff."""
    one = lambda x: json.dumps(x, ensure_ascii=False)  # noqa: E731
    lines = ["{"]
    keys = list(data)
    for k, key in enumerate(keys):
        end = "," if k + 1 < len(keys) else ""
        value = data[key]
        if key == "instances":
            lines.append(f'  "{key}": [')
            lines += [f"    {one(r)}" + ("," if i + 1 < len(value) else "") for i, r in enumerate(value)]
            lines.append(f"  ]{end}")
        elif key in ("ground", "review"):
            lines.append(f'  "{key}": {{')
            subkeys = list(value)
            for s, sub in enumerate(subkeys):
                send = "," if s + 1 < len(subkeys) else ""
                item = value[sub]
                if isinstance(item, list):
                    lines.append(f'    "{sub}": [')
                    lines += [f"      {one(r)}" + ("," if i + 1 < len(item) else "") for i, r in enumerate(item)]
                    lines.append(f"    ]{send}")
                else:
                    lines.append(f'    "{sub}": {one(item)}{send}')
            lines.append(f"  }}{end}")
        elif key == "start_points":
            lines.append(f'  "{key}": [')
            lines += [f"    {one(r)}" + ("," if i + 1 < len(value) else "") for i, r in enumerate(value)]
            lines.append(f"  ]{end}")
        else:
            lines.append(f'  "{key}": {one(value)}{end}')
    lines.append("}")
    return "\n".join(lines) + "\n"


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    out = Path(argv[0]) if argv else OUT
    text = dumps(build())
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(text.encode("utf-8"))
    data = json.loads(text)
    counts = {}
    for row in data["instances"]:
        counts[row["variant"]] = counts.get(row["variant"], 0) + 1
    print(f"wrote {out}: {len(data['instances'])} instances, {len(data['ground']['floor_cuts_m'])} floor cuts, "
          f"{len(data['ground']['holes'])} hole bands, {len(data['start_points'])} start points")
    print("  " + ", ".join(f"{k} {n}" for k, n in sorted(counts.items())))
    return 0


if __name__ == "__main__":
    sys.exit(main())
