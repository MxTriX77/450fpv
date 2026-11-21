"""generators.track_house (w20-hunter-track task 1.3c; the spec's "three houses" scenarios): the quad's outline passes
all 23 marked openings in the built collision, each marker lies in its opening, the 19 attached objects stay on their
surfaces, the walls answer on both faces at their thickness, the openings answer nothing and every opening is a gap."""
import json
import math
from pathlib import Path

import pytest

from generators import track_house as th
from lib import audit, catalog, meta
from lib import params as params_lib

REPO = Path(__file__).resolve().parents[3]
ENTRY = json.loads((REPO / "assets-src" / "catalog.json").read_text(encoding="utf-8"))["assets"]["T02"]
LAYOUT = {o["instance_id"]: o for o in
          json.loads((REPO / "maps" / "hunter-09" / "layout.json").read_text(encoding="utf-8"))["objects"]}
# the sheet's "Frame and poses": (ox, oz, bearing of +u in degrees, floor y)
POSES = {"T02_north": (505.400, 538.957, 275.33, 18.93), "T02_north_east": (517.899, 525.704, 328.30, 20.55),
         "T02_south": (515.985, 431.658, 2.77, 20.44)}
QUAD_M = (0.574 + 0.040, 0.342 + 0.040)     # the drone's level outline with legs on and 1 kg, plus 20 mm a side
WIDENED = {416: 0.62, 394: 0.40, 32: 0.41}  # the spec's three enlargements, the most each may open
HOUSE_OBJECTS = (410, 411, 412, 413, 387, 403, 395, 419, 420, 421, 384, 385, 418, 415, 417)
BATTEN_OBJECTS = (405, 406, 407, 408)
ON_SURFACE_M = 0.05


def load(variant):
    return json.loads((REPO / "assets-src" / "track" / f"{variant}.json").read_text(encoding="utf-8"))


@pytest.fixture(scope="module")
def built():
    return {v: th.build(load(v)) for v in ENTRY["variants"]}


def house_frame(variant, p):
    """A world point in the house's asset frame (x = v, y = h, z = u)."""
    ox, oz, bearing, floor = POSES[variant]
    b = math.radians(bearing)
    dx, dz = p[0] - ox, p[2] - oz
    return (dx * math.cos(b) - dz * math.sin(b), p[1] - floor, dx * math.sin(b) + dz * math.cos(b))


def rotate_inverse(q, d):
    x, y, z, w = -q[0], -q[1], -q[2], q[3]
    t = (2 * (y * d[2] - z * d[1]), 2 * (z * d[0] - x * d[2]), 2 * (x * d[1] - y * d[0]))
    return (d[0] + w * t[0] + y * t[2] - z * t[1], d[1] + w * t[1] + z * t[0] - x * t[2],
            d[2] + w * t[2] + x * t[1] - y * t[0])


def box_axes(shape):
    """The box's three unit axes in asset space."""
    x, y, z, w = shape["rotation"]
    return ((1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w)),
            (2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w)),
            (2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y)))


def distance(p, shape):
    """From a point to a collision shape; 0 inside it."""
    d = [p[a] - shape["center"][a] for a in range(3)]
    if shape["shape"] == "sphere":
        return max(0.0, math.dist(p, shape["center"]) - shape["radius"])
    local = rotate_inverse(shape["rotation"], d)
    if shape["shape"] == "box":
        return math.sqrt(sum(max(abs(local[a]) - shape["size"][a] / 2, 0.0) ** 2 for a in range(3)))
    half = shape["height"] / 2
    if shape["shape"] == "cylinder":
        return math.hypot(max(math.hypot(local[0], local[2]) - shape["radius"], 0.0), max(abs(local[1]) - half, 0.0))
    return max(0.0, math.dist(local, (0.0, max(-half + shape["radius"], min(half - shape["radius"], local[1])), 0.0))
               - shape["radius"])


def ray_hit(shape, origin, direction):
    """The distance along a unit ray to a box's surface, or None; a ray starting inside hits at once."""
    if shape["shape"] != "box":
        return None
    o = rotate_inverse(shape["rotation"], [origin[a] - shape["center"][a] for a in range(3)])
    d = rotate_inverse(shape["rotation"], direction)
    near, far = -math.inf, math.inf
    for a in range(3):
        half = shape["size"][a] / 2
        if abs(d[a]) < 1e-12:
            if abs(o[a]) > half:
                return None
            continue
        t0, t1 = sorted(((-half - o[a]) / d[a], (half - o[a]) / d[a]))
        near, far = max(near, t0), min(far, t1)
    if near > far or far < 0:
        return None
    return max(near, 0.0)


def first_hit(shapes, origin, direction, reach):
    hits = [t for s in shapes if (t := ray_hit(s, origin, direction)) is not None and t <= reach]
    return min(hits) if hits else None


def swept_box_hits(shapes, centre, axes, half):
    """The shapes a box (centre, three unit axes, half sizes) overlaps by more than 0.1 mm, by separating axes for a
    box, by distance from the box's nearest point for any other shape."""
    hit = []
    for s in shapes:
        if s["shape"] == "box":
            b_axes, b_half = box_axes(s), [v / 2 for v in s["size"]]
            d = [s["center"][a] - centre[a] for a in range(3)]
            tests = list(axes) + list(b_axes)
            for a in axes:
                for b in b_axes:
                    c = (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
                    n = math.sqrt(sum(v * v for v in c))
                    if n > 1e-9:
                        tests.append(tuple(v / n for v in c))
            if all(abs(sum(d[k] * t[k] for k in range(3))) + 1e-4
                   < sum(half[i] * abs(sum(axes[i][k] * t[k] for k in range(3))) for i in range(3))
                   + sum(b_half[i] * abs(sum(b_axes[i][k] * t[k] for k in range(3))) for i in range(3)) for t in tests):
                hit.append(s)
        else:
            reach = math.sqrt(sum(v * v for v in half))
            if distance(centre, s) < reach:
                hit.append(s)
    return hit


def openings(variant):
    """Each marked opening of a variant as (row, its wall, its centre (s, h), width, height, tilt)."""
    params = load(variant)
    house = th.plan(params)
    out = []
    for o in params["openings"]:
        out.append((o, house.walls[o["wall"]], (o["s"], o["h"]), o["width"], o["height"], o["tilt_deg"]))
    return house, out


def test_the_catalog_entry_references_the_village_materials():
    assert ENTRY["generator"] == "track_house" and ENTRY["class"] == "track"
    assert ENTRY["variants"] == ["T02_north", "T02_north_east", "T02_south"]
    vendor = json.loads((REPO / "assets-src" / "vendor.json").read_text(encoding="utf-8"))["assets"]["N01"]["materials"]
    for slot in ("boards", "post", "frame", "floor", "roof", "brick"):
        assert ENTRY["materials"][slot] in vendor.values()
        recipe = json.loads((REPO / catalog.RECIPES / f"{ENTRY['materials'][slot]}.json").read_text(encoding="utf-8"))
        assert recipe["kind"] == "vendor" and recipe["material"].startswith("Assets/Farm_land/URP_Materials/")


@pytest.mark.parametrize("variant", ENTRY["variants"])
def test_params_conform(variant):
    assert params_lib.validate(th, load(variant)) == []


def test_one_building_type():
    site = {"floor_under_m", "ground_a_m", "ground_b_m"}
    tables = [{r["name"]: r["value"] for r in load(v)["rows"] if r["name"] not in site} for v in ENTRY["variants"]]
    assert tables[0] == tables[1] == tables[2]


def test_a_pane_off_its_window_is_refused():
    params = load("T02_south")
    next(o for o in params["openings"] if o["marker"] == 28)["s"] += 0.01
    assert any("no pane" in p for p in params_lib.validate(th, params))


def test_the_23_markers_are_all_there():
    markers = [o["marker"] for v in ENTRY["variants"] for o in load(v)["openings"]]
    assert len(markers) == 23 == len(set(markers))
    assert all(LAYOUT[m]["kind"].startswith("volume_box") for m in markers)


@pytest.mark.parametrize("variant", ENTRY["variants"])
def test_each_marker_lies_in_its_opening(variant):
    """Centred within 50 mm in the wall's plane, at least the marker's size unless it is one of the three widened."""
    house, rows = openings(variant)
    for o, wall, (s, h), width, height, tilt in rows:
        row = LAYOUT[o["marker"]]
        x, y, z = house_frame(variant, row["position_m"])
        at = (z, y) if o["wall"] == "door" else (x, y)
        t = math.radians(tilt)
        ds, dh = at[0] - s, at[1] - h
        across = ds * math.cos(t) + dh * math.sin(t)
        up = -ds * math.sin(t) + dh * math.cos(t)
        assert math.hypot(across, up) <= 0.05, o["marker"]
        size = sorted(abs(v) for v in row["scale"])  # the marker's thin axis, then its two in the wall's plane
        need = sorted((width, height))
        if o["marker"] in WIDENED:
            assert max(width, height) >= size[2] - 0.002 or min(width, height) <= WIDENED[o["marker"]] + 1e-9
        else:
            assert need[0] >= size[1] - 0.002 and need[1] >= size[2] - 0.002, o["marker"]


@pytest.mark.parametrize("variant", ENTRY["variants"])
def test_the_quad_passes_every_marked_opening(variant, built):
    """The outline, with 20 mm a side, turned with the opening and swept through the wall from 0.4 m before it to 0.4 m
    past it, touches no collision shape."""
    _, rows = openings(variant)
    shapes = built[variant]["collision"]
    for o, wall, (s, h), width, height, tilt in rows:
        assert width >= QUAD_M[0] and height >= QUAD_M[1], o["marker"]
        t = math.radians(tilt)
        e = (wall.along[0] * math.cos(t), math.sin(t), wall.along[2] * math.cos(t))
        up = (-wall.along[0] * math.sin(t), math.cos(t), -wall.along[2] * math.sin(t))
        centre = wall.at(s, h, wall.t / 2)
        hits = swept_box_hits(shapes, centre, (e, up, wall.out), (QUAD_M[0] / 2, QUAD_M[1] / 2, wall.t / 2 + 0.4))
        assert not hits, (o["marker"], [(c["center"], c["size"]) for c in hits[:3]])


def test_the_crawl_lines_clear_the_piers(built):
    """A straight line from each house's end A bay to its end B bay keeps the quad's half diagonal off every pier."""
    need = math.hypot(*QUAD_M) / 2
    for variant in ENTRY["variants"]:
        params = load(variant)
        house = th.plan(params)
        bays = {o["wall"]: o for o in params["openings"] if o["kind"] == "crawl"}
        a, b = bays["A"], bays["B"]
        for k in range(121):
            f = k / 120
            p = (a["s"] + (b["s"] - a["s"]) * f, a["h"] + (b["h"] - a["h"]) * f, house.L * f)
            for u, v in house.piers:
                lo, hi = th._pier_box(house, u, v)
                gap = math.hypot(max(lo[0] - p[0], 0.0, p[0] - hi[0]), max(lo[2] - p[2], 0.0, p[2] - hi[2]))
                assert gap >= need, (variant, p, (u, v))


@pytest.mark.parametrize("instance", HOUSE_OBJECTS)
def test_the_house_objects_stay_on_their_surfaces(instance, built):
    p = house_frame("T02_north", LAYOUT[instance]["position_m"])
    assert min(distance(p, s) for s in built["T02_north"]["collision"]) <= ON_SURFACE_M


@pytest.mark.parametrize("instance", BATTEN_OBJECTS)
def test_the_batten_objects_stay_on_the_batten(instance):
    house = th.plan(load("T02_north"))
    batten = th._birch_shapes(house)[-1]
    p = house_frame("T02_north", LAYOUT[instance]["position_m"])
    assert distance(p, batten) <= ON_SURFACE_M


@pytest.mark.parametrize("variant", ENTRY["variants"])
@pytest.mark.parametrize("name, s, h", [("door", 4.6, 1.0), ("back", 4.0, 1.0), ("A", 3.8, 1.0), ("B", 3.8, 1.0)])
def test_a_wall_answers_on_both_faces(variant, name, s, h, built):
    house = th.plan(load(variant))
    wall = house.walls[name]
    shapes = built[variant]["collision"]
    inward = th._neg(wall.out)
    outside = th._add(wall.at(s, h, 0.0), wall.out, 0.5)
    assert first_hit(shapes, outside, inward, 1.0) == pytest.approx(0.5, abs=1e-5)
    inside = th._add(wall.at(s, h, wall.t), inward, 0.5)
    assert first_hit(shapes, inside, wall.out, 1.0) == pytest.approx(0.5, abs=1e-5)
    assert wall.t == pytest.approx(0.18)


@pytest.mark.parametrize("variant", ENTRY["variants"])
def test_every_opening_is_open_and_a_known_gap(variant, built):
    """A ray along the normal through each marked opening's centre meets nothing in the wall's depth, and its gap is
    there, at least the opening's size."""
    _, rows = openings(variant)
    data = built[variant]
    names = {g["id"]: g for g in data["gaps"]}
    for o, wall, (s, h), width, height, _ in rows:
        start = th._add(wall.at(s, h, 0.0), wall.out, 0.05)
        assert first_hit(data["collision"], start, th._neg(wall.out), wall.t + 0.10) is None, o["marker"]
        gap = names[f"{o['kind']}_{o['marker']}"]
        assert gap["narrowest_m"] == pytest.approx(min(width, height), abs=0.005), o["marker"]


@pytest.mark.parametrize("variant", ENTRY["variants"])
def test_no_drawn_vertex_stands_in_a_pane(variant, built):
    """Openings hold no glass or panel: no LOD0 vertex lies inside a window pane, a door or a slot within the wall."""
    house = th.plan(load(variant))
    mesh = built[variant]["lods"][0]
    margin = 0.001
    for wall in house.walls.values():
        rects = [(hole.s, hole.h, hole.w, hole.ht, hole.tilt) for hole in wall.holes if hole.kind != "window"]
        for w in (w for w in house.windows if w["wall"] == wall.name):
            for c in (0, 1):
                for row in (0, 1):
                    s0, s1, a, b = th.pane_rect(w, c, row)
                    rects.append(((s0 + s1) / 2, (a + b) / 2, s1 - s0, b - a, 0.0))
        for x, y, z in mesh.vertices:
            if wall.name == "door":
                depth, s = x, z
            elif wall.name == "back":
                depth, s = house.W - x, z
            elif wall.name == "A":
                depth, s = z, x
            else:
                depth, s = house.L - z, x
            if not 0.0 <= depth <= wall.t:
                continue
            for cs, ch, width, height, tilt in rects:
                t = math.radians(tilt)
                across = (s - cs) * math.cos(t) + (y - ch) * math.sin(t)
                up = -(s - cs) * math.sin(t) + (y - ch) * math.cos(t)
                assert not (abs(across) < width / 2 - margin and abs(up) < height / 2 - margin), (wall.name, x, y, z)


def test_the_snag_records_stay_far_under_the_cap(built):
    for data in built.values():
        assert sum(len(s["points"]) - 1 for s in data["snag"]) <= 128


@pytest.mark.parametrize("variant", ENTRY["variants"])
def test_metadata_validates_and_passes_the_build_audit(variant, built):
    """The audit holds the collision to the drawn shape: 50 mm anywhere, 20 mm within 0.3 m of a gap or landing face."""
    params = load(variant)
    data = meta.assemble("T02", variant, "track_house", params, built[variant], dict(ENTRY["materials"]))
    assert meta.validate(data, meta.load_physics_materials(REPO)) == []
    problems, _ = audit.run(data, built[variant]["lods"][0], soft=audit.soft_materials(REPO))
    assert problems == []


def test_the_build_is_deterministic():
    a, b = th.build(load("T02_south")), th.build(load("T02_south"))
    assert [m.vertices for m in a["lods"]] == [m.vertices for m in b["lods"]]
    assert a["collision"] == b["collision"] and a["gaps"] == b["gaps"] and a["snag"] == b["snag"]
