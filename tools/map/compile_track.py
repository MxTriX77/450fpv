"""Compiles maps/hunter-09/ into an fpv450-map-package (w20-hunter-track 1.4; the format is maps/farmland/README.md).

    python tools/map/compile_track.py           writes the package; stops with exit 3 if the ground changed
    python tools/map/compile_track.py --probe   the same, and runs Unity (-nographics) for the height probes

Reads layout.json and params.json (the map's labelled numbers), assets-src/catalog.json and the T02 params. The same
inputs give the same bytes. The height probes are Unity's Terrain.SampleHeight, so they are kept while the heights'
SHA-256 stands and asked again (Fpv450.MapHeightProbes) when it changes.

The ground, in order (design 2, the sheet's "The 11 low objects" and "The ground the houses need"):
- a plane fitted to the 32 distinct upright feet;
- a smooth correction (Wendland C2, compact) through those feet, the blocks and tyres resting on the ground and points
  along the ribbons' straight runs, so that everything that lies on the ground meets it and none of it is covered;
- the three arch dips, then each house's level ground with its bank.

The grass (task 3.2a): Village's 14 layers (maps/farmland/map.json's prefabs, groups, scales and mesh tops) at Village's
meadow density, each layer's mean instances per cell from params.json, drawn per cell in the package's order from a seeded
stream; none in a cell within grass_clear_m of a placed object's footprint, seen from above (its collision shapes' boxes and
its snag lines, from the kit's metadata), so none inside the houses either.
"""
import hashlib
import json
import math
import os
import random
import subprocess
import sys
from array import array
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
MAP_ID = "hunter-09"
ROOT = REPO / "maps" / MAP_ID
CATALOG = REPO / "assets-src" / "catalog.json"
SURFACES = REPO / "assets-src" / "surfaces.json"
TRACK_PARAMS = REPO / "assets-src" / "track"
VENDOR_TRACK = REPO / "assets-src" / "vendor_track.json"
MODELS = REPO / "unity" / "Assets" / "_Project" / "Art" / "Models"  # MapWorld.ModelsRoot: the kit's metadata
MAX_SAMPLE = 32766
FEET = 32        # the spec's distinct upright objects
DRAWN = 272      # the spec's distinct drawn rows
PROBES_PENDING = 3
LABELS = ("measured", "derived", "published", "assumed")


def load_params(path):
    rows = json.loads(path.read_text(encoding="utf-8"))["rows"]
    for name, row in rows.items():
        if row.get("label") not in LABELS or not row.get("why"):
            raise SystemExit(f"{path}: {name} needs a label ({', '.join(LABELS)}) and a why")
    return {name: row["value"] for name, row in rows.items()}


def distinct(objects):
    """Each byte-identical row once, in file order (design 8)."""
    seen, out = set(), []
    for row in objects:
        key = json.dumps(row, sort_keys=True)
        if key not in seen:
            seen.add(key)
            out.append(row)
    return out


def tilt_deg(q):
    x, _, z, _ = q
    return math.degrees(math.acos(max(-1.0, min(1.0, 1 - 2 * (x * x + z * z)))))


# ------------------------------------------------------------------------------------------------ the ground samples


def foot_rows(rows, p):
    feet = [o for o in rows if o["drawn"] and (o["kind"] in p["foot_kinds"] or o["instance_id"] in p["foot_frames"])]
    if len(feet) != FEET:
        raise SystemExit(f"{len(feet)} upright foot samples, the spec has {FEET}")
    return feet


def resting_rows(rows, p):
    return [o for o in rows if o["kind"] in p["resting_kinds"] and o["position_m"][1] <= p["resting_top_m"]
            and tilt_deg(o["rotation_xyzw"]) <= p["resting_tilt_deg"]]


def ribbon_runs(rows, p):
    """Each ribbon's points without the file's end marker, as lists of (x, y, z)."""
    runs = []
    for row in rows:
        if row["kind"] != "ribbon_tape":
            continue
        points = []
        for point in row["points"]:
            if not points or math.dist(point["position_m"], points[-1]) >= p["ribbon_end_m"]:
                points.append(tuple(point["position_m"]))
        runs.append(points)
    return runs


def fit_plane(points):
    """Least squares y = my + a (x - mx) + b (z - mz) over (x, y, z) points."""
    n = len(points)
    mx, my, mz = (sum(q[i] for q in points) / n for i in range(3))
    sxx = sum((q[0] - mx) ** 2 for q in points)
    szz = sum((q[2] - mz) ** 2 for q in points)
    sxz = sum((q[0] - mx) * (q[2] - mz) for q in points)
    sxy = sum((q[0] - mx) * (q[1] - my) for q in points)
    szy = sum((q[2] - mz) * (q[1] - my) for q in points)
    det = sxx * szz - sxz * sxz
    return mx, my, mz, (sxy * szz - szy * sxz) / det, (szy * sxx - sxy * sxz) / det


def solve(matrix, values):
    """Gaussian elimination with partial pivoting; the lists are consumed."""
    n = len(values)
    for k in range(n):
        pivot = max(range(k, n), key=lambda i: (abs(matrix[i][k]), -i))
        matrix[k], matrix[pivot] = matrix[pivot], matrix[k]
        values[k], values[pivot] = values[pivot], values[k]
        for i in range(k + 1, n):
            f = matrix[i][k] / matrix[k][k]
            if f != 0.0:
                row_i, row_k = matrix[i], matrix[k]
                for j in range(k, n):
                    row_i[j] -= f * row_k[j]
                values[i] -= f * values[k]
    out = [0.0] * n
    for k in range(n - 1, -1, -1):
        out[k] = (values[k] - sum(matrix[k][j] * out[j] for j in range(k + 1, n))) / matrix[k][k]
    return out


def smoothstep(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3.0 - 2.0 * t)


class Ground:
    """The ground as a function of (x, z), before it is sampled on the tile's grid."""

    def __init__(self, p, plane, dips, houses):
        self.radius = float(p["correction_radius_m"])
        self.plane = plane
        self.dips = dips
        self.houses = houses
        self.centres = []
        self.weights = []

    def plane_at(self, x, z):
        mx, my, mz, a, b = self.plane
        return my + a * (x - mx) + b * (z - mz)

    def kernel(self, d2):
        r2 = self.radius * self.radius
        if d2 >= r2:
            return 0.0
        q = math.sqrt(d2) / self.radius
        s = 1.0 - q
        return s * s * s * s * (4.0 * q + 1.0)

    def fit(self, centres):
        """The correction through (x, y, z) centres, exactly at each."""
        self.centres = centres
        matrix = [[self.kernel((a[0] - b[0]) ** 2 + (a[2] - b[2]) ** 2) for b in centres] for a in centres]
        self.weights = solve(matrix, [c[1] - self.plane_at(c[0], c[2]) for c in centres])

    def base(self, x, z):
        h = self.plane_at(x, z)
        r2 = self.radius * self.radius
        for c, w in zip(self.centres, self.weights):
            dx, dz = x - c[0], z - c[2]
            d2 = dx * dx + dz * dz
            if d2 < r2:
                h += w * self.kernel(d2)
        return h

    def at(self, x, z):
        h = self.base(x, z)
        for dip in self.dips:
            r = math.hypot(x - dip["centre_m"][0], z - dip["centre_m"][1])
            if r < dip["outer_m"]:
                h = dip["floor_m"] + (h - dip["floor_m"]) * smoothstep((r - dip["inner_m"]) / (dip["outer_m"] - dip["inner_m"]))
        for house in self.houses:
            h = house.blend(x, z, h)
        return h


class HouseGround:
    """A house's level ground: the sheet's height under its footprint, along u from end A to end B, and its bank."""

    def __init__(self, row):
        self.row = row
        self.sin = math.sin(math.radians(row["bearing_deg"]))
        self.cos = math.cos(math.radians(row["bearing_deg"]))
        params = json.loads((TRACK_PARAMS / f"{row['variant']}.json").read_text(encoding="utf-8"))
        r = {x["name"]: x["value"] for x in params["rows"]}
        self.length, self.width = r["length_m"], r["width_m"]
        self.v0 = -(r["porch_m"] + 0.5 * r["post_m"])
        self.ground_a = row["floor_m"] + r["ground_a_m"]
        self.ground_b = row["floor_m"] + r["ground_b_m"]
        self.reach = row["flat_m"] + row["bank_m"]

    def uv(self, x, z):
        dx, dz = x - self.row["ox"], z - self.row["oz"]
        return dx * self.sin + dz * self.cos, dx * self.cos - dz * self.sin

    def floor(self, u):
        return self.ground_a + (self.ground_b - self.ground_a) * min(max(u / self.length, 0.0), 1.0)

    def blend(self, x, z, h):
        u, v = self.uv(x, z)
        du = max(-u, u - self.length, 0.0)
        dv = max(self.v0 - v, v - self.width, 0.0)
        if du >= self.reach or dv >= self.reach:
            return h
        d = math.hypot(du, dv)
        if d >= self.reach:
            return h
        floor = self.floor(u)
        return floor + (h - floor) * smoothstep((d - self.row["flat_m"]) / self.row["bank_m"])


def build_ground(rows, p):
    """The Ground with its correction fitted, and how many feet and resting objects it runs through."""
    feet = [tuple(o["position_m"]) for o in foot_rows(rows, p)]
    resting = [tuple(o["position_m"]) for o in resting_rows(rows, p)]
    ground = Ground(p, fit_plane(feet), p["dips"], [HouseGround(h) for h in p["houses"]])
    # A ribbon lies on the ground along its straight runs, a hair above it (the kit lifts the band 10 mm more).
    tape = []
    for run in ribbon_runs(rows, p):
        for a, b in zip(run, run[1:]):
            n = max(1, round(math.dist(a, b) / p["ribbon_step_m"]))
            tape += [tuple(a[k] + (b[k] - a[k]) * i / n - (p["ribbon_clear_m"] if k == 1 else 0.0) for k in range(3))
                     for i in range(n)]
        tape.append((run[-1][0], run[-1][1] - p["ribbon_clear_m"], run[-1][2]))
    centres = []
    for q in feet + resting + tape:
        if all(math.hypot(q[0] - c[0], q[2] - c[2]) >= p["sample_gap_m"] for c in centres):
            centres.append(q)
    ground.fit(centres)
    return ground, len(feet), len(resting)


def sample_heights(ground, p):
    """The tile's samples, row 0 the northmost (the package's order)."""
    x0, z0, x1, z1 = p["map_rect_m"]
    res, top = p["tile_resolution"], float(p["tile_height_m"])
    step_x, step_z = (x1 - x0) / (res - 1), (z1 - z0) / (res - 1)
    margin = ground.radius + 1.0
    xs = [c[0] for c in ground.centres] + [h.row["ox"] for h in ground.houses]
    zs = [c[2] for c in ground.centres] + [h.row["oz"] for h in ground.houses]
    reach = margin + max(max(h.length, h.width) + h.reach for h in ground.houses)
    box = (min(xs) - reach, min(zs) - reach, max(xs) + reach, max(zs) + reach)
    samples = array("H", [0]) * (res * res)
    for r in range(res):
        z = z1 - r * step_z
        for c in range(res):
            x = x0 + c * step_x
            inside = box[0] <= x <= box[2] and box[1] <= z <= box[3]
            h = ground.at(x, z) if inside else ground.plane_at(x, z)
            samples[r * res + c] = min(max(round(h / top * MAX_SAMPLE), 0), MAX_SAMPLE)
    return samples


# ------------------------------------------------------------------------------------------------ the package's files


def dumps_rows(head, key, rows, tail=None):
    """JSON with one row of `key` per line, so a change reads as a line diff."""
    lines = ["{"] + [f"  {json.dumps(k)}: {json.dumps(v, ensure_ascii=False)}," for k, v in head.items()]
    lines.append(f'  "{key}": [')
    lines += ["    " + json.dumps(row, ensure_ascii=False) + ("," if i + 1 < len(rows) else "") for i, row in enumerate(rows)]
    lines.append("  ]" + ("," if tail else ""))
    if tail:
        items = list(tail.items())
        lines += [f"  {json.dumps(k)}: {json.dumps(v, ensure_ascii=False)}" + ("," if i + 1 < len(items) else "")
                  for i, (k, v) in enumerate(items)]
    lines.append("}")
    return ("\n".join(lines) + "\n").encode("utf-8")


def variant_of(row, catalog):
    kind, row_id = row["kind"], row["instance_id"]
    name = f"T01_{kind}_{row_id}" if kind in ("ribbon_tape", "board_large") else f"T01_{kind}"
    if name not in catalog["T01"]["variants"]:
        raise SystemExit(f"layout row {row_id}: {name} is not a T01 variant in {CATALOG}")
    return name


def start_point(rows, spawn_id, forward_m):
    """The race's spawn point, upright, heading along its rotation's forward (+Z), moved forward_m along that heading:
    the file's spawn stands on a concrete block, and a flight's start stands on the terrain, which would put the drone
    inside the block."""
    row = next(o for o in rows if o["kind"] == "spawn_point" and o["instance_id"] == spawn_id)
    x, y, z, w = row["rotation_xyzw"]
    heading = math.degrees(math.atan2(2 * (x * z + y * w), 1 - 2 * (x * x + y * y))) % 360.0
    px, py, pz = row["position_m"]
    along = math.radians(heading)
    position = [round(px + forward_m * math.sin(along), 6), py, round(pz + forward_m * math.cos(along), 6)]
    return {"id": "spawn", "position_m": position, "heading_deg": round(heading, 3), "legs": "on", "stand": False}


def placements_bytes(rows, p, catalog, spawn_id):
    placed = []
    for row in rows:
        if not row["drawn"]:
            continue
        if "scale" in row:
            raise SystemExit(f"layout row {row['instance_id']}: a drawn row with a scale, which the kit does not take")
        placed.append({"id": len(placed), "kind": "track", "variant": variant_of(row, catalog),
                       "layout_id": row["instance_id"], "position_m": row["position_m"],
                       "rotation_xyzw": row["rotation_xyzw"], "scale": [1.0, 1.0, 1.0]})
    if len(placed) != DRAWN:
        raise SystemExit(f"{len(placed)} distinct drawn rows, the spec has {DRAWN}")
    for house in p["houses"]:
        if house["variant"] not in catalog["T02"]["variants"]:
            raise SystemExit(f"{house['variant']} is not a T02 variant in {CATALOG}")
        half = math.radians(house["bearing_deg"]) / 2
        placed.append({"id": len(placed), "kind": "track", "variant": house["variant"],
                       "position_m": [house["ox"], house["floor_m"], house["oz"]],
                       "rotation_xyzw": [0.0, round(math.sin(half), 9), 0.0, round(math.cos(half), 9)],
                       "scale": [1.0, 1.0, 1.0]})
    variants = sorted({row["variant"] for row in placed})
    head = {"format": "fpv450-map-placements", "format_version": 1, "map": MAP_ID, "frame": "unity-lh-yup",
            "variants": [{"id": v, "kind": "track"} for v in variants]}
    return dumps_rows(head, "placements", placed, {"start_points": [start_point(rows, spawn_id, p["start_forward_m"])]}), placed


# ------------------------------------------------------------------------------------------------ the grass


def rotate(q, v):
    """v turned by the unit quaternion q = (x, y, z, w)."""
    x, y, z, w = q
    tx, ty, tz = 2 * (y * v[2] - z * v[1]), 2 * (z * v[0] - x * v[2]), 2 * (x * v[1] - y * v[0])
    return v[0] + w * tx + y * tz - z * ty, v[1] + w * ty + z * tx - x * tz, v[2] + w * tz + x * ty - y * tx


def cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def hull(points):
    """The convex hull of (x, z) points, counter-clockwise (the monotone chain); collinear points give a segment."""
    pts = sorted(set(points))
    if len(pts) <= 2:
        return pts

    def half(seq):
        out = []
        for q in seq:
            while len(out) >= 2 and cross(out[-2], out[-1], q) <= 0:
                out.pop()
            out.append(q)
        return out

    return half(pts)[:-1] + half(reversed(pts))[:-1]


def edges(poly):
    return list(zip(poly, poly[1:] + poly[:1])) if len(poly) > 1 else [(poly[0], poly[0])]


def point_segment(q, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    length2 = dx * dx + dz * dz
    t = 0.0 if length2 == 0 else min(max(((q[0] - a[0]) * dx + (q[1] - a[1]) * dz) / length2, 0.0), 1.0)
    return math.hypot(q[0] - a[0] - t * dx, q[1] - a[1] - t * dz)


def poly_distance(a, b):
    """The least distance between two convex (x, z) polygons (a segment or a point included), 0 where they meet."""
    for p, inner in ((a[0], b), (b[0], a)):
        if len(inner) >= 3 and all(cross(u, v, p) >= 0 for u, v in edges(inner)):
            return 0.0
    ea, eb = edges(a), edges(b)
    for p, q in ea:
        for r, s in eb:
            if (cross(r, s, p) > 0) != (cross(r, s, q) > 0) and (cross(p, q, r) > 0) != (cross(p, q, s) > 0) \
                    and 0 not in (cross(r, s, p), cross(r, s, q), cross(p, q, r), cross(p, q, s)):
                return 0.0
    return min(min(point_segment(p, r, s) for p in a for r, s in eb), min(point_segment(p, r, s) for p in b for r, s in ea))


def footprint(row, meta):
    """A placed row's outline seen from above, as convex (x, z) pieces with a radius: each collision shape's box (a sphere,
    cylinder or capsule by the box round it) and each snag line's segments with its radius, at the row's pose."""
    pos, rot, scale = row["position_m"], row["rotation_xyzw"], row["scale"]

    def place(v):
        x, y, z = rotate(rot, (v[0] * scale[0], v[1] * scale[1], v[2] * scale[2]))
        return pos[0] + x, pos[2] + z

    pieces = []
    for c in meta["collision"]:
        if c["shape"] == "box":
            half = [s / 2 for s in c["size"]]
        elif c["shape"] == "sphere":
            half = [c["radius"]] * 3
        else:  # a cylinder or capsule along its local y, its height end to end
            half = [c["radius"], c["height"] / 2, c["radius"]]
        turn = c.get("rotation", [0.0, 0.0, 0.0, 1.0])
        corners = []
        for k in range(8):
            local = rotate(turn, tuple(half[i] * (1 if k >> i & 1 else -1) for i in range(3)))
            corners.append(place(tuple(c["center"][i] + local[i] for i in range(3))))
        pieces.append((hull(corners), 0.0))
    for snag in meta.get("snag", []):
        line = [place(q) for q in snag["points"]]
        pieces += [(hull([u, v]), snag["radius_m"] * max(abs(s) for s in scale)) for u, v in zip(line, line[1:])]
    return pieces


def bare_cells(placed, p, catalog):
    """The grass grid's cells (row · res + column, row 0 the northmost) within grass_clear_m of a placed row's footprint."""
    x0, z0, x1, z1 = p["map_rect_m"]
    res = p["grass_resolution"]
    side = (x1 - x0) / res
    assets = {v: a for a in ("T01", "T02") for v in catalog[a]["variants"]}
    metas, bare = {}, set()
    for row in placed:
        variant = row["variant"]
        if variant not in metas:
            path = MODELS / catalog[assets[variant]]["class"] / assets[variant] / f"{variant}.assetmeta.json"
            metas[variant] = json.loads(path.read_text(encoding="utf-8"))
        for poly, radius in footprint(row, metas[variant]):
            reach = radius + p["grass_clear_m"]
            c0 = max(int(math.floor((min(q[0] for q in poly) - reach - x0) / side)), 0)
            c1 = min(int(math.floor((max(q[0] for q in poly) + reach - x0) / side)), res - 1)
            r0 = max(int(math.floor((z1 - max(q[1] for q in poly) - reach) / side)), 0)
            r1 = min(int(math.floor((z1 - min(q[1] for q in poly) + reach) / side)), res - 1)
            for r in range(r0, r1 + 1):
                zn, zs = z1 - r * side, z1 - (r + 1) * side
                for c in range(c0, c1 + 1):
                    cell = r * res + c
                    if cell in bare:
                        continue
                    xw, xe = x0 + c * side, x0 + (c + 1) * side
                    if poly_distance(poly, [(xw, zs), (xe, zs), (xe, zn), (xw, zn)]) <= reach:
                        bare.add(cell)
    return sorted(bare)


def grass_layers(p, bare):
    """Village's layers (grass_from's) with this map's counts: the plane bytes and the manifest's layer rows.

    Layer l draws one uniform number per cell from random.Random(grass_seed + l), every cell in the package's order (row 0
    the northmost, then column), so an object moved changes only its own cells; a cell holds the mean's whole part and one
    more where the number falls under its fraction, as the farmland's generator scatters (FarmLandBuilder.PaintGrass).
    """
    village = json.loads((REPO / p["grass_from"]).read_text(encoding="utf-8"))
    farm = village["grass"]
    means = p["grass_per_cell"]
    if farm["resolution"] != p["grass_resolution"] or village["map_rect_m"] != p["map_rect_m"] or len(means) != len(farm["layers"]):
        raise SystemExit(f"grass_per_cell is per cell of {p['grass_from']}'s grid and layer: its grid or layers changed")
    plane = p["grass_resolution"] ** 2
    data, rows = bytearray(), []
    for entry, mean in zip(farm["layers"], means):
        whole = math.floor(mean)
        fraction = mean - whole
        draw = random.Random(p["grass_seed"] + entry["layer"]).random
        counts = bytearray(whole + (draw() < fraction) for _ in range(plane))
        for cell in bare:
            counts[cell] = 0
        data += counts
        rows.append({**entry, "instances": sum(counts)})
    return bytes(data), rows


def probe_points(rows, p):
    """SplitMix64 as the farmland's export (FarmlandExport.Probes), at multiples of 1/1024 m."""
    cfg = p["probes"]
    state = cfg["seed"]
    mask = (1 << 64) - 1

    def uniform(low, high):
        nonlocal state
        state = (state + 0x9E3779B97F4A7C15) & mask
        z = state
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & mask
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & mask
        z ^= z >> 31
        return math.floor((low + (z >> 11) * (1.0 / (1 << 53)) * (high - low)) * 1024.0) / 1024.0

    x0, z0, x1, z1 = p["map_rect_m"]
    drawn = [o["position_m"] for o in rows if o["drawn"] and "points" not in o]
    m = cfg["track_margin_m"]
    box = (min(q[0] for q in drawn) - m, min(q[2] for q in drawn) - m, max(q[0] for q in drawn) + m, max(q[2] for q in drawn) + m)
    points = [(uniform(x0, x1), uniform(z0, z1)) for _ in range(cfg["map"])]
    points += [(uniform(box[0], box[2]), uniform(box[1], box[3])) for _ in range(cfg["track"])]
    return points


def probes_bytes(path, heights_sha, points):
    """The kept probes while they answer these heights at these points, else a request. Returns (bytes, ready)."""
    if path.is_file():
        data = path.read_bytes()
        doc = json.loads(data.decode("utf-8"))
        old = doc.get("probes", [])
        if (doc.get("heights_sha256") == heights_sha and [(q["x"], q["z"]) for q in old] == points
                and all(q["y"] is not None for q in old)):
            return data, True
    head = {"format": "fpv450-map-height-probes", "format_version": 1, "map": MAP_ID, "frame": "unity-lh-yup",
            "about": "Unity's Terrain.SampleHeight on the map tile (Fpv450.MapHeightProbes)", "heights_sha256": heights_sha}
    return dumps_rows(head, "probes", [{"ring": "map", "x": x, "z": z, "y": None} for x, z in points]), False


def vendor_bytes(catalog):
    assets = {a: {"class": catalog[a]["class"], "variants": catalog[a]["variants"]} for a in ("T01", "T02")}
    doc = {"schema": 1, "about": "The track kit and the track's houses (w20-hunter-track): our own generated assets, listed "
                                 "for the loader, which finds a package row's asset by its variant. Written by "
                                 "tools/map/compile_track.py from assets-src/catalog.json.", "assets": assets}
    return (json.dumps(doc, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def compile_package(root=ROOT, vendor=VENDOR_TRACK):
    """Writes the package under `root` and the vendor list at `vendor`; returns (ready, a report line)."""
    root = Path(root)
    p = load_params(root / "params.json")
    layout_data = (root / "layout.json").read_bytes()
    layout = json.loads(layout_data.decode("utf-8"))
    rows = distinct(layout["objects"])
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))["assets"]
    surfaces = list(json.loads(SURFACES.read_text(encoding="utf-8"))["surfaces"])

    ground, feet, resting = build_ground(rows, p)
    samples = sample_heights(ground, p)
    if sys.byteorder == "big":
        samples.byteswap()
    heights = samples.tobytes()
    heights_sha = hashlib.sha256(heights).hexdigest()
    surface_id = surfaces.index(p["ground_surface"]) + 1
    class_res = p["class_resolution"]
    classes = bytes([surface_id]) * (class_res * class_res)
    placements, placed_rows = placements_bytes(rows, p, catalog, layout["race"]["spawn_point_instance_id"])
    placed = len(placed_rows)
    bare = bare_cells(placed_rows, p, catalog)
    grass, grass_rows = grass_layers(p, bare)
    outlines = (json.dumps({"format": "fpv450-map-outlines", "format_version": 1, "map": MAP_ID, "frame": "unity-lh-yup",
                            "fields": []}, indent=2) + "\n").encode("utf-8")
    probes, ready = probes_bytes(root / "height_probes.json", heights_sha, probe_points(rows, p))

    files = {"classes/map.raw": classes, "grass/map.raw": grass, "heights/map.r16": heights, "height_probes.json": probes,
             "layout.json": layout_data,
             "outlines.json": outlines, "params.json": (root / "params.json").read_bytes(), "placements.json": placements}
    x0, z0, x1, z1 = p["map_rect_m"]
    tile = {"id": "map", "ring": "map", "position_m": [x0, 0, z0], "size_m": [x1 - x0, p["tile_height_m"], z1 - z0],
            "heights": {"file": "heights/map.r16", "resolution": p["tile_resolution"], "min_sample": min(samples),
                        "max_sample": max(samples)},
            "classes": {"file": "classes/map.raw", "resolution": class_res}}
    manifest = {"format": "fpv450-map-package", "format_version": 1, "map": MAP_ID, "frame": "unity-lh-yup",
                "source": {"compiler": "tools/map/compile_track.py"}, "map_rect_m": p["map_rect_m"], "tiles": [tile],
                "ground_classes": {"surface_table": "assets-src/surfaces.json",
                                   "layers": [{"layer": "stand_in", "surface": p["ground_surface"], "id": surface_id,
                                               "note": "assumed: one class until portion 3 dresses the ground"}]},
                "grass": {"tile": "map", "file": "grass/map.raw", "resolution": p["grass_resolution"], "layers": grass_rows},
                "counts": {"track": placed}, "height_probes": p["probes"],
                "files": {name: hashlib.sha256(files[name]).hexdigest() for name in sorted(files)}}
    for name, data in files.items():
        if name in ("layout.json", "params.json"):
            continue
        (root / name).parent.mkdir(parents=True, exist_ok=True)
        (root / name).write_bytes(data)
    (root / "map.json").write_bytes((json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode("utf-8"))
    Path(vendor).write_bytes(vendor_bytes(catalog))
    return ready, (f"{root.as_posix()}: {placed} placed, {feet} feet + {resting} resting + "
                   f"{len(ground.centres) - feet - resting} ribbon samples, heights {min(samples)}..{max(samples)}, "
                   f"{sum(r['instances'] for r in grass_rows)} grass instances, {len(bare)} bare cells")


def run_unity():
    unity = os.environ.get("UNITY", r"C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe")
    log = REPO / "unity" / "Logs" / "map-height-probes.log"
    code = subprocess.run([unity, "-batchmode", "-nographics", "-quit", "-projectPath", str(REPO / "unity"), "-executeMethod",
                           "Fpv450.MapHeightProbes.Run", "-probeMap", MAP_ID, "-logFile", str(log)]).returncode
    if code != 0:
        raise SystemExit(f"Unity exited {code}; see {log}")


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    if argv not in ([], ["--probe"]):
        print(__doc__.split("\n\n")[1])
        return 2
    ready, report = compile_package()
    if not ready and argv:
        run_unity()
        ready, report = compile_package()
    print(report)
    if not ready:
        print("the ground changed: run python tools/map/compile_track.py --probe (Unity must not have the project open)")
        return PROBES_PENDING
    return 0


if __name__ == "__main__":
    sys.exit(main())
