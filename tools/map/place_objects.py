"""Our own objects on the farmland (w5-farmland-objects 1.2; D-090, D-092, D-095; W2's design §4, rules 2 and 3): places
them from a seed and derives the terrain edits they need, all as data in the map package.

    python tools/map/place_objects.py maps/farmland

Run it after every export: FarmlandExport rewrites map.json and placements.json without our rows. It drops any rows of
our kinds, places them again from the seed and writes:
- placements.json: our rows after the export's, in the export's own format (its bytes kept), and their variants;
- holes/map.raw: the map tile's hole mask, uint8 per height cell, 255 a hole and 0 ground, row 0 the northmost;
- footprints.json: per object the map tile's samples it levels, as the integer samples to write over the heights;
- map.json: the counts, the map tile's "holes", the "objects" record (the placer and its seed), every file's SHA-256.
The store's terrain is never touched: the sim and the player apply the mask and the samples at load (D-095).

The placements (D-090, D-092), each object on its own SplitMix64 stream (the seed mixed with its name):
- one E01_curving per field F1-F11: a spot in the field's box and a yaw in [0, 360), drawn until its patch (the plan
  rectangle of its bounds) lies inside the field FIELD_MARGIN_M from its outline;
- H01_I, H01_D, R01_A, R01_F in that order: a spot in the village circle, drawn until the footprint (the plan rectangle of
  the variant's bounds, its spilled rubble or parts included) lies inside the circle. A house turns its front (asset -Z)
  to the lane (LANE_Z) within HOUSE_YAW_JITTER_DEG; a car takes any yaw.
- Every object keeps clear (clear()): no edit reaches a ruin's or a debris piece's plan bounds, a tree's or bush's pivot,
  the start's stand, or another of ours; a tree's or bush's bounds stays CROWN_CLEAR_M off the footprint; a house stands
  TRACK_CLEAR_M off every dirt_track sample.

The edits:
- The hole mask (W2 §4, rule 2): every cell that meets a trench's opening is a hole. The opening is drawn, not
  top_width_m (w5 design §1.8): the run's lip reaches top_w_m / 2 + width_jitter_m / 2 + lip_crumble_m from its centre
  line, a niche's its width's half; the cell is a closed square, the opening a stadium about each record's centre line.
- The footprints (D-095), as the friend's generator levels its house plots (FarmLandBuilder.Village.cs,
  LevelVillageGround): the level is the mean of the samples inside the footprint, rounded to a sample; a sample within
  LEVEL_MARGIN_M of the footprint takes it, and one farther eases back to its own over SHOULDER_M (a smoothstep). The
  margin is more than a cell's diagonal, so every cell that meets the footprint is level. The row stands at the level.

Only the standard library; validate.py checks all of it again (check()).
"""
import hashlib
import json
import math
import re
import sys
from array import array
from decimal import Decimal
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate  # noqa: E402

REPO = validate.REPO
MODELS = REPO / "unity" / "Assets" / "_Project" / "Art" / "Models"
CATALOG = REPO / "assets-src" / "catalog.json"
PARAMS = REPO / "assets-src"
DRONE = REPO / "assets-src" / "drone" / "drone.json"
PLACER = "tools/map/place_objects.py"

SEED = 90  # assumed: any fixed seed (D-090's number)
OURS = {"E01_curving": "trench", "H01_I": "house", "H01_D": "house", "R01_A": "car", "R01_F": "car"}  # D-090
TRENCH = "E01_curving"
VILLAGE = ("H01_I", "H01_D", "R01_A", "R01_F")  # placed in this order, the larger first
HOLES_FILE = "holes/map.raw"
FOOTPRINTS_FILE = "footprints.json"

LEVEL_MARGIN_M = 1.5  # published: FarmLandBuilder.Village.cs HouseGroundMargin, level ground past a house's edge
SHOULDER_M = 7.0  # published: HouseGroundShoulder, the ease back into the terrain
REACH_M = LEVEL_MARGIN_M + SHOULDER_M  # derived: no sample farther than this from a footprint changes
CROWN_CLEAR_M = 3.0  # published: HouseTreeClearance, a trunk's least distance from a house's outline
TRACK_CLEAR_M = 3.0  # published: HouseTrackClearance, a house's least distance from a track
FIELD_MARGIN_M = REACH_M + TRACK_CLEAR_M  # assumed: the edits end TRACK_CLEAR_M inside the field's outline
# The most the ground's samples under a footprint may span, by kind (assumed). A trench's or a car's 1 m keeps the cut
# and fill near 0.5 m, about E01's field parapet (0.45 m), so a levelled patch reads as spoil, not as a terrace in the
# field. A house takes 2.5 m: H01_D's footprint with its spilled rubble is 16 x 20 m, which spans at least 1.95 m
# anywhere free in the village (measured), and the friend's own plots are levelled on that hill (W2 design §1).
MAX_RELIEF_M = {"trench": 1.0, "house": 2.5, "car": 1.0}
LANE_Z = 600.0  # published: the village's centre z (VillageCentre); the friend's two rows of ruins face it
HOUSE_YAW_JITTER_DEG = 6.0  # published: the friend's five houses turn 176-186 and -5-4 degrees (VillageHouses)
START_XZ = (566.5, 569.0)  # published: FlightWorlds.FarmlandStart (D-092), checked against the C# by the tests
MAX_TRIES = 20000  # a spot is drawn at most this often before the placer gives up
TRACK_ID = 13  # dirt_track's surface id (assets-src/surfaces.json row 12, + 1)

MASK64 = (1 << 64) - 1


# ------------------------------------------------------------------------------------------------ the streams

def _mix(z):
    z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
    z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK64
    return z ^ (z >> 31)


class Stream:
    """SplitMix64 (as the height probes draw), one per object: its state the seed with the name's bytes mixed in."""

    def __init__(self, seed, name):
        state = seed & MASK64
        for b in name.encode("utf-8"):
            state = _mix(state ^ b)
        self.state = state

    def next(self):
        self.state = (self.state + 0x9E3779B97F4A7C15) & MASK64
        return _mix(self.state)

    def uniform(self):
        """A double in [0, 1): the draw's top 53 bits."""
        return (self.next() >> 11) * (1.0 / (1 << 53))


# ------------------------------------------------------------------------------------------------ plan geometry

def rotate(q, p):
    """p turned by the unit quaternion q = (x, y, z, w): p + w t + q.xyz x t, t = 2 q.xyz x p."""
    x, y, z, w = q
    px, py, pz = p
    tx, ty, tz = 2 * (y * pz - z * py), 2 * (z * px - x * pz), 2 * (x * py - y * px)
    return (px + w * tx + y * tz - z * ty, py + w * ty + z * tx - x * tz, pz + w * tz + x * ty - y * tx)


def unit(q):
    n = math.sqrt(sum(c * c for c in q))
    return tuple(c / n for c in q)


def yaw_rotation(yaw_deg):
    """Unity's Quaternion.Euler(0, yaw, 0), rounded as the export rounds (1e-7)."""
    half = math.radians(yaw_deg) / 2
    return [0, _r(math.sin(half), 7), 0, _r(math.cos(half), 7)]


def _r(v, digits):
    return round(v, digits) + 0.0  # + 0.0: never -0.0


def seg_d2(x, z, a, b):
    ux, uz = b[0] - a[0], b[1] - a[1]
    t = max(0.0, min(1.0, ((x - a[0]) * ux + (z - a[1]) * uz) / (ux * ux + uz * uz)))
    return (x - a[0] - t * ux) ** 2 + (z - a[1] - t * uz) ** 2


def _clips(square, a, b):
    """Whether segment a-b meets the closed square (x0, z0, x1, z1) (Liang-Barsky)."""
    t0, t1 = 0.0, 1.0
    for k, (lo, hi) in enumerate(((square[0], square[2]), (square[1], square[3]))):
        d = b[k] - a[k]
        if d == 0.0:
            if not lo <= a[k] <= hi:
                return False
            continue
        ta, tb = sorted(((lo - a[k]) / d, (hi - a[k]) / d))
        t0, t1 = max(t0, ta), min(t1, tb)
        if t0 > t1:
            return False
    return True


def square_to_line(square, pts):
    """The least plan distance from a closed axis-aligned square to a polyline: 0 where they meet, else the least of
    its vertices to the square and the square's corners to its segments."""
    x0, z0, x1, z1 = square
    best = math.inf
    for a, b in zip(pts, pts[1:]):
        if _clips(square, a, b):
            return 0.0
        for p in (a, b):
            best = min(best, math.hypot(max(x0 - p[0], 0.0, p[0] - x1), max(z0 - p[1], 0.0, p[1] - z1)))
        for q in ((x0, z0), (x1, z0), (x0, z1), (x1, z1)):
            best = min(best, math.sqrt(seg_d2(q[0], q[1], a, b)))
    return best


def _cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def _segments_cross(a, b, c, d):
    d1, d2, d3, d4 = _cross(c, d, a), _cross(c, d, b), _cross(a, b, c), _cross(a, b, d)
    return ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0)) and 0 not in (d1, d2, d3, d4)


def inside_polygon(x, z, poly):
    """Even-odd rule."""
    inside = False
    for (ax, az), (bx, bz) in zip(poly, poly[1:] + poly[:1]):
        if (az > z) != (bz > z) and x < ax + (z - az) * (bx - ax) / (bz - az):
            inside = not inside
    return inside


def polygon_distance(p, q):
    """The least plan distance between two closed polygons (a segment is a 2-point one): 0 when they meet."""
    pe = list(zip(p, p[1:] + p[:1])) if len(p) > 2 else [(p[0], p[1])]
    qe = list(zip(q, q[1:] + q[:1])) if len(q) > 2 else [(q[0], q[1])]
    if any(_segments_cross(a, b, c, d) for a, b in pe for c, d in qe):
        return 0.0
    if len(q) > 2 and any(inside_polygon(x, z, q) for x, z in p) or len(p) > 2 and any(inside_polygon(x, z, p) for x, z in q):
        return 0.0
    return min(min(math.sqrt(seg_d2(x, z, a, b)) for x, z in p for a, b in qe),
               min(math.sqrt(seg_d2(x, z, a, b)) for x, z in q for a, b in pe))


class Footprint:
    """A row's plan rectangle: its variant's bounds in x and z, at the row's position and yaw."""

    def __init__(self, row, bounds):
        self.x, _, self.z = row["position_m"]
        self.q = unit(row["rotation_xyzw"])
        self.inverse = (-self.q[0], -self.q[1], -self.q[2], self.q[3])
        (x0, _, z0), (x1, _, z1) = bounds["min"], bounds["max"]
        self.rect = (x0, z0, x1, z1)
        self.corners = [self.world(x, z) for x, z in ((x0, z0), (x1, z0), (x1, z1), (x0, z1))]
        xs, zs = [c[0] for c in self.corners], [c[1] for c in self.corners]
        self.box = (min(xs), min(zs), max(xs), max(zs))

    def world(self, lx, lz):
        wx, _, wz = rotate(self.q, (lx, 0.0, lz))
        return (self.x + wx, self.z + wz)

    def distance(self, x, z):
        """From the rectangle's outline, negative inside (the friend's HouseEdgeDistance)."""
        lx, _, lz = rotate(self.inverse, (x - self.x, 0.0, z - self.z))
        x0, z0, x1, z1 = self.rect
        ox = abs(lx - (x0 + x1) / 2) - (x1 - x0) / 2
        oz = abs(lz - (z0 + z1) / 2) - (z1 - z0) / 2
        return math.hypot(max(ox, 0.0), max(oz, 0.0)) + min(max(ox, oz), 0.0)


# ------------------------------------------------------------------------------------------------ the package

class Package:
    """What the placer reads: the map tile, its classes, the export's rows, the outlines and our variants' metadata."""

    def __init__(self, root):
        self.root = Path(root)
        self.manifest = json.loads((self.root / "map.json").read_text(encoding="utf-8"))
        row = next(t for t in self.manifest["tiles"] if t["ring"] == "map")
        samples = array("H")
        samples.frombytes((self.root / row["heights"]["file"]).read_bytes())
        if sys.byteorder == "big":
            samples.byteswap()
        self.tile = validate.Tile(row, samples)
        self.n = self.tile.res - 1  # cells a side: the hole grid
        self.cell = self.tile.sx / self.n
        self.diagonal = math.sqrt(2.0) * self.cell
        self.classes = (self.root / row["classes"]["file"]).read_bytes()
        self.class_res = row["classes"]["resolution"]
        placements = json.loads((self.root / "placements.json").read_text(encoding="utf-8"))
        self.exported = [r for r in placements["placements"] if r["variant"] not in OURS]
        outlines = json.loads((self.root / "outlines.json").read_text(encoding="utf-8"))
        self.fields = {f["name"]: [tuple(p) for p in f["outline_m"]] for f in outlines["fields"]}
        self.village = (tuple(outlines["village"]["centre_m"]), outlines["village"]["radius_m"])
        self.metas = {v: meta(v) for v in OURS}
        self.others = [_other(r) for r in self.exported]
        stand = json.loads(DRONE.read_text(encoding="utf-8"))["stand"]
        # derived: each post at post_radius_m on both axes (LaunchStand), its square tube's half diagonal past it
        self.stand_m = math.sqrt(2.0) * (stand["post_radius_m"]["value"] + stand["post_diameter_m"]["value"] / 2)

    def sample(self, column, row):
        """The height sample at file row `row` (0 the northmost) and column `column`."""
        return self.tile.samples[row * self.tile.res + column]

    def class_at(self, column, row):
        return self.classes[row * self.class_res + column]


def meta(variant):
    """A variant of ours: its catalog asset's .assetmeta.json (its class and asset from assets-src/catalog.json)."""
    for asset, entry in json.loads(CATALOG.read_text(encoding="utf-8"))["assets"].items():
        if variant in entry["variants"]:
            return json.loads((MODELS / entry["class"] / asset / f"{variant}.assetmeta.json").read_text(encoding="utf-8"))
    raise SystemExit(f"{variant} is in no asset of {CATALOG}")


def _vendor_bounds():
    bounds = {}
    for name in ("vendor.json", "vendor_plants.json"):
        for asset, entry in json.loads((PARAMS / name).read_text(encoding="utf-8"))["assets"].items():
            for variant in entry["variants"]:
                path = MODELS / entry["class"] / asset / f"{variant}.assetmeta.json"
                bounds[variant] = json.loads(path.read_text(encoding="utf-8"))["bounds"]
    return bounds


_VENDOR = None


def _other(row):
    """An exported row's kind, pivot and plan box: its variant's bounds scaled (a mirror's negative x included) and
    turned (tilts included), in plan."""
    global _VENDOR
    if _VENDOR is None:
        _VENDOR = _vendor_bounds()
    b, scale = _VENDOR[row["variant"]], row["scale"]
    q = unit(row["rotation_xyzw"])
    x, y, z = row["position_m"]
    xs, zs = [], []
    for cx in (0, 1):
        for cy in (0, 1):
            for cz in (0, 1):
                corner = tuple((b["max"] if c else b["min"])[k] * scale[k] for k, c in enumerate((cx, cy, cz)))
                wx, _, wz = rotate(q, corner)
                xs.append(x + wx)
                zs.append(z + wz)
    return {"id": row["id"], "kind": row["kind"], "pivot": (x, z), "box": (min(xs), min(zs), max(xs), max(zs))}


def _box_polygon(box):
    x0, z0, x1, z1 = box
    return [(x0, z0), (x1, z0), (x1, z1), (x0, z1)]


# ------------------------------------------------------------------------------------------------ placing

def clear(package, footprint, variant, ours):
    """The reasons this footprint isn't on free ground (none when it is), against the export's rows, the start and the
    footprints of ours placed before it."""
    reasons = []
    reach = REACH_M + package.diagonal  # derived: a cell any of whose corners an edit moves lies within this
    near = reach + CROWN_CLEAR_M + 20.0
    box = footprint.box
    for other in package.others:
        ox0, oz0, ox1, oz1 = other["box"]
        if ox0 > box[2] + near or ox1 < box[0] - near or oz0 > box[3] + near or oz1 < box[1] - near:
            continue
        if other["kind"] in ("ruin", "debris"):  # they rest on the ground with their whole plan
            if polygon_distance(footprint.corners, _box_polygon(other["box"])) < reach:
                reasons.append(f"an edit reaches {other['kind']} {other['id']}")
        else:  # trees and bushes stand on their pivot
            if footprint.distance(*other["pivot"]) < reach:
                reasons.append(f"an edit reaches {other['kind']} {other['id']}'s pivot")
            elif polygon_distance(footprint.corners, _box_polygon(other["box"])) < CROWN_CLEAR_M:
                reasons.append(f"{other['kind']} {other['id']} within {CROWN_CLEAR_M} m")
    if footprint.distance(*START_XZ) < reach + package.stand_m:
        reasons.append("an edit reaches the start's stand")
    for mine in ours:
        if polygon_distance(footprint.corners, mine.corners) < 2 * REACH_M:
            reasons.append("its edits meet another of ours")
    if OURS[variant] == "house":
        for column, row in _class_samples(package, footprint, TRACK_CLEAR_M):
            if package.class_at(column, row) == TRACK_ID:
                reasons.append(f"a dirt_track sample within {TRACK_CLEAR_M} m")
                break
    if not reasons and relief(package, footprint) > MAX_RELIEF_M[OURS[variant]]:
        reasons.append(f"the ground under it spans more than {MAX_RELIEF_M[OURS[variant]]} m")
    return reasons


def inside_samples(package, footprint):
    """The map tile's height samples inside the footprint (its distance <= 0), as the friend's level averages them."""
    t = package.tile
    spacing = t.sx / package.n
    x0, z0, x1, z1 = footprint.box
    return [t.sample(c, r)
            for r in range(max(0, math.floor((z0 - t.z0) / spacing)), min(package.n, math.ceil((z1 - t.z0) / spacing)) + 1)
            for c in range(max(0, math.floor((x0 - t.x0) / spacing)), min(package.n, math.ceil((x1 - t.x0) / spacing)) + 1)
            if footprint.distance(t.x0 + c * spacing, t.z0 + r * spacing) <= 0.0]


def relief(package, footprint):
    """How far the ground's samples under the footprint span, m."""
    samples = inside_samples(package, footprint)
    return (max(samples) - min(samples)) / validate.MAX_SAMPLE * package.tile.sy


def _class_samples(package, footprint, within):
    """The class samples within `within` of the footprint: (column, row), row 0 the northmost."""
    t, res = package.tile, package.class_res
    step = t.sx / (res - 1)
    x0, z0, x1, z1 = footprint.box
    for row in range(max(0, math.floor((t.z0 + t.sz - z1 - within) / step)), min(res, math.ceil((t.z0 + t.sz - z0 + within) / step) + 1)):
        z = t.z0 + t.sz - row * step
        for column in range(max(0, math.floor((x0 - within - t.x0) / step)), min(res, math.ceil((x1 + within - t.x0) / step) + 1)):
            if footprint.distance(t.x0 + column * step, z) <= within:
                yield column, row


def inside_field(footprint, outline, margin=FIELD_MARGIN_M):
    return (all(inside_polygon(x, z, outline) for x, z in footprint.corners)
            and all(polygon_distance(footprint.corners, [a, b]) >= margin for a, b in zip(outline, outline[1:] + outline[:1])))


def inside_village(footprint, village):
    (cx, cz), r = village
    return all(math.hypot(x - cx, z - cz) <= r for x, z in footprint.corners)


def _row(variant, x, z, yaw_deg):
    return {"id": None, "kind": OURS[variant], "variant": variant, "position_m": [_r(x, 4), 0.0, _r(z, 4)],
            "rotation_xyzw": yaw_rotation(yaw_deg), "scale": [1, 1, 1]}


def place(package, seed=SEED):
    """Our rows, ids after the export's, y still 0 (level() sets it): the trenches by field, then the village's."""
    rows, ours = [], []
    for name in sorted(package.fields, key=lambda f: int(f[1:])):
        outline = package.fields[name]
        xs, zs = [p[0] for p in outline], [p[1] for p in outline]
        stream = Stream(seed, f"{TRENCH}/{name}")
        for _ in range(MAX_TRIES):
            x = min(xs) + stream.uniform() * (max(xs) - min(xs))
            z = min(zs) + stream.uniform() * (max(zs) - min(zs))
            row = _row(TRENCH, x, z, stream.uniform() * 360.0)
            footprint = Footprint(row, package.metas[TRENCH]["bounds"])
            if inside_field(footprint, outline) and not clear(package, footprint, TRENCH, ours):
                break
        else:
            raise SystemExit(f"no free spot for {TRENCH} in {name} after {MAX_TRIES} draws")
        rows.append(row)
        ours.append(footprint)
    (cx, cz), radius = package.village
    for variant in VILLAGE:
        stream = Stream(seed, f"{variant}/village")
        for _ in range(MAX_TRIES):
            x = cx - radius + stream.uniform() * 2 * radius
            z = cz - radius + stream.uniform() * 2 * radius
            turn = stream.uniform()
            if OURS[variant] == "house":  # its front (asset -Z) to the lane: yaw 0 faces south, 180 north
                yaw = (0.0 if z >= LANE_Z else 180.0) + (2 * turn - 1) * HOUSE_YAW_JITTER_DEG
            else:
                yaw = turn * 360.0
            row = _row(variant, x, z, yaw % 360.0)
            footprint = Footprint(row, package.metas[variant]["bounds"])
            if inside_village(footprint, package.village) and not clear(package, footprint, variant, ours):
                break
        else:
            raise SystemExit(f"no free spot for {variant} in the village after {MAX_TRIES} draws")
        rows.append(row)
        ours.append(footprint)
    first = len(package.exported)
    for i, row in enumerate(rows):
        row["id"] = first + i
    return rows


# ------------------------------------------------------------------------------------------------ the edits

def opening_reach(record):
    """How far a hole record's drawn opening reaches from its centre line (w5 design §1.2): the run's lip with its
    widest jitter and crumble (E01's params), a niche its width's half (no jitter)."""
    if record["id"] != "run":
        return record["top_width_m"] / 2
    params = json.loads((PARAMS / "earthworks" / f"{TRENCH}.json").read_text(encoding="utf-8"))
    return params["top_w_m"] / 2 + params["width_jitter_m"] / 2 + params["lip_crumble_m"]


def openings(package, rows):
    """Each trench row's hole records in the map's plan: (row id, opening reach, band half-width, centre line)."""
    records = package.metas[TRENCH]["holes"]
    out = []
    for row in rows:
        if row["variant"] != TRENCH:
            continue
        x, _, z = row["position_m"]
        q = unit(row["rotation_xyzw"])
        for record in records:
            line = []
            for p in record["centre_line"]:
                wx, _, wz = rotate(q, (p[0], 0.0, p[2]))
                line.append((x + wx, z + wz))
            out.append((row["id"], opening_reach(record), record["band_width_m"] / 2, line))
    return out


def hole_cells(package, rows):
    """{(cx, cz): row id}: every cell (Unity's indices, cz from the south) that meets an opening (rule 2)."""
    t, cell, cells = package.tile, package.cell, {}
    for row_id, reach, _, line in openings(package, rows):
        xs, zs = [p[0] for p in line], [p[1] for p in line]
        for cx in range(math.floor((min(xs) - reach - t.x0) / cell), math.floor((max(xs) + reach - t.x0) / cell) + 1):
            for cz in range(math.floor((min(zs) - reach - t.z0) / cell), math.floor((max(zs) + reach - t.z0) / cell) + 1):
                square = (t.x0 + cx * cell, t.z0 + cz * cell, t.x0 + (cx + 1) * cell, t.z0 + (cz + 1) * cell)
                if square_to_line(square, line) <= reach:
                    cells[(cx, cz)] = row_id
    return cells


def hole_mask(package, rows):
    """holes/map.raw's bytes: N x N, row 0 the northmost, 255 a hole."""
    n = package.n
    mask = bytearray(n * n)
    for cx, cz in hole_cells(package, rows):
        mask[(n - 1 - cz) * n + cx] = 255
    return bytes(mask)


def naked_samples(package, rows, step=0.1):
    """Rule 3: the samples, every `step` m over each hole cell, edges included, that lie in no band: (x, z) each."""
    t, cell = package.tile, package.cell
    lines = openings(package, rows)
    steps = [k * step for k in range(math.ceil(cell / step))] + [cell]
    naked = []
    for cx, cz in sorted(hole_cells(package, rows)):
        x0, z0 = t.x0 + cx * cell, t.z0 + cz * cell
        square = (x0, z0, x0 + cell, z0 + cell)
        near = [(half, a, b) for _, _, half, line in lines for a, b in zip(line, line[1:]) if square_to_line(square, [a, b]) <= half]
        for u in steps:
            for v in steps:
                if not any(math.sqrt(seg_d2(x0 + u, z0 + v, a, b)) <= half for half, a, b in near):
                    naked.append((x0 + u, z0 + v))
    return naked


def footprints(package, rows):
    """footprints.json's entries, one per row of ours: its corners, the level and the samples it writes."""
    t, n = package.tile, package.tile.res - 1
    spacing = t.sx / n
    out = []
    for row in rows:
        f = Footprint(row, package.metas[row["variant"]]["bounds"])
        columns = range(max(0, math.floor((f.box[0] - REACH_M - t.x0) / spacing)), min(n, math.ceil((f.box[2] + REACH_M - t.x0) / spacing)) + 1)
        unity_rows = range(max(0, math.floor((f.box[1] - REACH_M - t.z0) / spacing)), min(n, math.ceil((f.box[3] + REACH_M - t.z0) / spacing)) + 1)
        distance = {(c, r): f.distance(t.x0 + c * spacing, t.z0 + r * spacing) for r in unity_rows for c in columns}
        inside = inside_samples(package, f)
        level = (2 * sum(inside) + len(inside)) // (2 * len(inside))  # the mean, rounded half up, in integers
        runs = {}
        for (c, r), d in sorted(distance.items(), key=lambda item: (-item[0][1], item[0][0])):
            if d >= REACH_M:
                continue
            k = min(max((REACH_M - d) / SHOULDER_M, 0.0), 1.0)
            weight = k * k * (3.0 - 2.0 * k)  # Mathf.SmoothStep(0, 1, InverseLerp(reach, margin, d))
            own = t.sample(c, r)
            runs.setdefault(n - r, []).append((c, level if weight == 1.0 else math.floor(own + (level - own) * weight + 0.5)))
        samples = []
        for file_row in sorted(runs):
            run = runs[file_row]
            assert [c for c, _ in run] == list(range(run[0][0], run[0][0] + len(run))), "a footprint's row is one run"
            samples.append([file_row, run[0][0], [s for _, s in run]])
        out.append({"placement": row["id"], "variant": row["variant"],
                    "corners_m": [[_r(x, 4), _r(z, 4)] for x, z in f.corners],
                    "level_sample": level, "level_m": _r(t.y0 + level / validate.MAX_SAMPLE * t.sy, 4),
                    "samples": samples})
    return out


def level(package, rows):
    """Stands each row at its footprint's level (in place)."""
    by_id = {e["placement"]: e for e in footprints(package, rows)}
    for row in rows:
        row["position_m"][1] = by_id[row["id"]]["level_m"]


def edited_samples(package, entries):
    """The map tile's samples with every footprint's written over them (a copy)."""
    samples = array("H", package.tile.samples)
    res = package.tile.res
    for entry in entries:
        for file_row, first, values in entry["samples"]:
            samples[file_row * res + first:file_row * res + first + len(values)] = array("H", values)
    return samples


# ------------------------------------------------------------------------------------------------ checking

BASE_TOLERANCE_M = 0.01  # the spec's 1 cm: a house's or car's base on the levelled ground


def check(package, rows, mask, document, seed):
    """Every error in our objects' data, an empty list when they pass (validate.py runs it on the package): the same
    seed gives the same rows; each lies inside its field or the village and on free ground; the mask is the one the
    trenches derive (rule 2) and no hole is naked (rule 3); the footprints are the ones the rows derive; every cell
    that meets a footprint is level, every hole cell's corners stand at its trench's level, and every row at its own."""
    errors = []
    ours = [r for r in rows if r.get("variant") in OURS]
    expected = place(package, seed)
    level(package, expected)
    if ours != expected:
        wrong = next((f"row {e['id']}" for o, e in zip(ours, expected) if o != e), f"{len(ours)} rows for {len(expected)}")
        errors.append(f"placements: our rows aren't the ones seed {seed} places ({wrong}); run {PLACER}")
        return errors  # the rest derives from the rows the seed gives
    footprints_of = {r["id"]: Footprint(r, package.metas[r["variant"]]["bounds"]) for r in ours}
    trenches = [r for r in ours if r["variant"] == TRENCH]
    for name, outline in package.fields.items():
        inside = [r["id"] for r in trenches if inside_field(footprints_of[r["id"]], outline)]
        if len(inside) != 1:
            errors.append(f"field {name}: {len(inside)} trenches inside it {FIELD_MARGIN_M} m from its outline, not 1")
    for row in ours:
        f, what = footprints_of[row["id"]], f"placement {row['id']} ({row['variant']})"
        if row["rotation_xyzw"][0] != 0 or row["rotation_xyzw"][2] != 0 or row["scale"] != [1, 1, 1]:
            errors.append(f"{what}: not a pure yaw at scale 1")
        if row["variant"] in VILLAGE and not inside_village(f, package.village):
            errors.append(f"{what}: its footprint leaves the village circle")
        others = [g for i, g in footprints_of.items() if i != row["id"]]
        errors.extend(f"{what}: {reason}" for reason in clear(package, f, row["variant"], others))
    if mask != hole_mask(package, ours):
        errors.append(f"{HOLES_FILE}: differs from the mask the trenches derive (W2 §4 rule 2); run {PLACER}")
    naked = naked_samples(package, ours)
    if naked:
        errors.append(f"{len(naked)} naked samples in the hole cells (rule 3), the first at ({naked[0][0]:.3f}, {naked[0][1]:.3f})")
    entries = footprints(package, ours)
    if document.get("footprints") != entries:
        errors.append(f"{FOOTPRINTS_FILE}: differs from the footprints the rows derive; run {PLACER}")
        return errors
    edited, res, n = edited_samples(package, entries), package.tile.res, package.n
    spacing = package.tile.sx / n
    for row, entry in zip(ours, entries):
        f, what = footprints_of[row["id"]], f"placement {row['id']} ({row['variant']})"
        if abs(row["position_m"][1] - entry["level_m"]) > BASE_TOLERANCE_M:
            errors.append(f"{what}: its base is {abs(row['position_m'][1] - entry['level_m']):.4f} m off its level")
        x0, z0, x1, z1 = f.box
        for cz in range(max(0, math.floor(z0 / spacing) - 1), min(n, math.ceil(z1 / spacing) + 1)):
            for cx in range(max(0, math.floor(x0 / spacing) - 1), min(n, math.ceil(x1 / spacing) + 1)):
                square = [(cx * spacing, cz * spacing), ((cx + 1) * spacing, cz * spacing),
                          ((cx + 1) * spacing, (cz + 1) * spacing), (cx * spacing, (cz + 1) * spacing)]
                if polygon_distance(f.corners, square) == 0.0 and any(
                        edited[(n - r) * res + c] != entry["level_sample"] for c in (cx, cx + 1) for r in (cz, cz + 1)):
                    errors.append(f"{what}: cell ({cx}, {cz}) meets the footprint but isn't level")
        if any(r in (0, n) or c == 0 or c + len(v) - 1 == n for r, c, v in entry["samples"]):
            errors.append(f"{what}: an edit reaches the tile's edge, where it meets the surround")
    level_of = {e["placement"]: e["level_sample"] for e in entries}
    for (cx, cz), row_id in sorted(hole_cells(package, ours).items()):
        if any(edited[(n - r) * res + c] != level_of[row_id] for c in (cx, cx + 1) for r in (cz, cz + 1)):
            errors.append(f"hole cell ({cx}, {cz}): its corners aren't at its trench's level")
    return errors


# ------------------------------------------------------------------------------------------------ writing

def _num(v):
    if isinstance(v, int) or v.is_integer():
        return str(int(v))
    return format(Decimal(repr(v)), "f")  # the export's fixed notation: 0.0000247, never 2.47e-05


def render_placements(document):
    """placements.json's text exactly as the export writes it (its README): one variant and one row a line."""
    header = "".join(f'  "{k}": {json.dumps(document[k])},\n' for k in ("format", "format_version", "map", "frame"))
    variants = ",\n".join(f'    {{"id": "{v["id"]}", "kind": "{v["kind"]}"}}' for v in document["variants"])
    rows = ",\n".join(
        f'    {{"id": {r["id"]}, "kind": "{r["kind"]}", "variant": "{r["variant"]}", '
        f'"position_m": [{", ".join(map(_num, r["position_m"]))}], '
        f'"rotation_xyzw": [{", ".join(map(_num, r["rotation_xyzw"]))}], '
        f'"scale": [{", ".join(map(_num, r["scale"]))}]}}' for r in document["placements"])
    return "{\n" + header + '  "variants": [\n' + variants + '\n  ],\n  "placements": [\n' + rows + "\n  ]\n}\n"


def render_footprints(map_id, entries):
    lines = ["{", '  "format": "fpv450-map-footprints",', '  "format_version": 1,', f'  "map": "{map_id}",',
             '  "frame": "unity-lh-yup",',
             '  "about": "w5-farmland-objects (D-095): per object of ours, the map tile\'s height samples it writes, '
             'each run [file row (0 the northmost), first column, samples]; written by ' + PLACER + '",',
             '  "tile": "map",', f'  "level_margin_m": {LEVEL_MARGIN_M},', f'  "shoulder_m": {SHOULDER_M},',
             '  "footprints": [']
    blocks = []
    for e in entries:
        head = (f'    {{"placement": {e["placement"]}, "variant": "{e["variant"]}", '
                f'"corners_m": {json.dumps(e["corners_m"])}, "level_sample": {e["level_sample"]}, "level_m": {_num(e["level_m"])},\n'
                '     "samples": [\n')
        runs = ",\n".join(f"      {json.dumps(run, separators=(',', ':'))}" for run in e["samples"])
        blocks.append(head + runs + "]}")
    return "\n".join(lines) + "\n" + ",\n".join(blocks) + "\n  ]\n}\n"


def _sha(data):
    return hashlib.sha256(data).hexdigest()


def render_manifest(text, counts, files, holes_resolution, seed):
    """map.json with our edits made in its text (the export's layout kept): the map tile's holes, the counts, the objects
    record and the files' digests. Applying it twice gives the same text."""
    holes = f'"holes": {{"file": "{HOLES_FILE}", "resolution": {holes_resolution}}}'
    tile = re.compile(r'(\n    \{"id": "map", "ring": "map",.*?\n     "classes": \{[^\n]*?\})(,\n     "holes": \{[^\n]*?\})?\}', re.S)
    text, found = tile.subn(lambda m: m.group(1) + ",\n     " + holes + "}", text)
    assert found == 1, "map.json: the map tile's block"
    text, found = re.subn(r'\n  "counts": [^\n]*,\n', lambda m: f'\n  "counts": {json.dumps(counts)},\n', text)
    assert found == 1, "map.json: the counts line"
    objects = f'  "objects": {{"placer": "{PLACER}", "seed": {seed}, "footprints": "{FOOTPRINTS_FILE}"}},\n'
    text = re.sub(r'  "objects": [^\n]*\n', "", text)
    text, found = re.subn(r'(\n  "height_probes": [^\n]*,\n)', lambda m: m.group(1) + objects, text)
    assert found == 1, "map.json: the height_probes line"
    listing = ",\n".join(f'    "{k}": "{files[k]}"' for k in sorted(files))
    text, found = re.subn(r'\n  "files": \{\n.*?\n  \}\n', lambda m: '\n  "files": {\n' + listing + "\n  }\n", text, flags=re.S)
    assert found == 1, "map.json: the files block"
    return text


def build(root, seed=SEED):
    """Every file the placer writes, by its path in the package, as bytes; nothing written yet."""
    package = Package(root)
    rows = place(package, seed)
    level(package, rows)
    entries = footprints(package, rows)
    placements = json.loads((package.root / "placements.json").read_text(encoding="utf-8"))
    exported = [v for v in placements["variants"] if v["id"] not in OURS]
    placements["variants"] = sorted(exported + [{"id": v, "kind": k} for v, k in OURS.items()], key=lambda v: v["id"])
    placements["placements"] = package.exported + rows
    out = {
        "placements.json": render_placements(placements).encode("utf-8"),
        HOLES_FILE: hole_mask(package, rows),
        FOOTPRINTS_FILE: render_footprints(package.manifest["map"], entries).encode("utf-8"),
    }
    counts = {k: v for k, v in package.manifest["counts"].items() if k not in OURS.values() and k != "ring_trees_left_out"}
    for kind in dict.fromkeys(OURS.values()):
        counts[kind] = sum(1 for r in rows if r["kind"] == kind)
    counts["ring_trees_left_out"] = package.manifest["counts"]["ring_trees_left_out"]
    files = dict(package.manifest["files"])
    files.update({k: _sha(v) for k, v in out.items()})
    text = (package.root / "map.json").read_text(encoding="utf-8")
    out["map.json"] = render_manifest(text, counts, files, package.n, seed).encode("utf-8")
    return out, rows, entries


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    if len(argv) != 1:
        print(__doc__.split("\n\n")[1])
        return 2
    root = Path(argv[0])
    original = (root / "placements.json").read_bytes()
    exported_only = json.loads(original)
    exported_only["placements"] = [r for r in exported_only["placements"] if r["variant"] not in OURS]
    exported_only["variants"] = [v for v in exported_only["variants"] if v["id"] not in OURS]
    if not any(r["variant"] in OURS for r in json.loads(original)["placements"]) and render_placements(exported_only).encode() != original:
        raise SystemExit("placements.json: the export's layout isn't the one render_placements writes; nothing written")
    out, rows, entries = build(root)
    for relative, data in out.items():
        (root / relative).parent.mkdir(parents=True, exist_ok=True)
        (root / relative).write_bytes(data)
    package = Package(root)
    for row, entry in zip(rows, entries):
        f = package.metas[row["variant"]]
        moved = [abs(v - package.sample(c + k, r)) for r, c, vs in entry["samples"] for k, v in enumerate(vs)]
        print(f"{row['id']} {row['variant']:12s} at ({row['position_m'][0]:.2f}, {row['position_m'][2]:.2f}), "
              f"yaw {math.degrees(2 * math.atan2(row['rotation_xyzw'][1], row['rotation_xyzw'][3])) % 360:.1f}, "
              f"level {entry['level_m']:.3f} m; {sum(len(vs) for _, _, vs in entry['samples'])} samples, "
              f"the most moved {max(moved) / validate.MAX_SAMPLE * package.tile.sy:.2f} m")
    print(f"{PLACER}: {len(rows)} rows, {out[HOLES_FILE].count(255)} hole cells, {len(entries)} footprints in {root.as_posix()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
